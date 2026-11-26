using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json.Linq;

namespace Hearthstone_Deck_Tracker.Utility.Battlegrounds;

internal sealed class TavernStatsCache
{
	internal const string QueryUrl = "https://hsreplay.net/analytics/query/battlegrounds_minion_list/?BattlegroundsMMRPercentile=TOP_50_PERCENT&BattlegroundsTimeRange=CURRENT_BATTLEGROUNDS_PATCH";
	internal sealed record Result(TavernMinionStats? Stats, string Missing, DateTime Expires, bool Imported = false);

	private readonly Func<Task<Result>> _fetch;
	private Task<Result>? _request;
	private int? _account;
	internal TavernStatsCache(Func<Task<Result>>? fetch = null) => _fetch = fetch ?? Fetch;

	internal void Clear()
	{
		_request = null;
		_account = null;
	}

	// All changes to the active request happen on the overlay dispatcher. Fetch
	// returns a table and never writes to an individual card's hover control.
	internal Result? Get(int? account)
	{
		if(account == null)
		{
			Clear();
			return new Result(null, "StatsAccess", DateTime.UtcNow);
		}
		if(_account != account || _request == null || (_request.IsCompleted && _request.Result.Expires <= DateTime.UtcNow))
		{
			_account = account;
			_request = _fetch();
		}
		return _request.IsCompleted ? _request.Result : null;
	}

	internal async Task<bool> Import(string path)
	{
		var account = HSReplayNetOAuth.AccountData;
		if(account?.IsTier7 != true)
			return false;
		var snapshot = await Task.Run(() => {
			using var stream = File.OpenRead(path);
			if(stream.Length > 32 * 1024 * 1024)
				throw new InvalidDataException();
			using var reader = new StreamReader(stream);
			return TavernMinionStats.Parse(reader.ReadToEnd());
		});
		if(!snapshot.IsRecentSnapshot(DateTimeOffset.UtcNow) || HSReplayNetOAuth.AccountData?.Id != account.Id || HSReplayNetOAuth.AccountData?.IsTier7 != true)
			return false;
		_account = account.Id;
		_request = Task.FromResult(new Result(snapshot, "StatsNoRound", DateTime.UtcNow.AddHours(1), true));
		return true;
	}

	private static async Task<Result> Fetch()
	{
		var delay = TimeSpan.FromMinutes(2);
		try
		{
			using var resp = await HSReplayNetOAuth.SendAsyncWithAuth(() => new HttpRequestMessage(HttpMethod.Get, QueryUrl));
			if(resp?.IsSuccessStatusCode == true)
			{
				var body = await resp.Content.ReadAsStringAsync();
				var stats = await Task.Run(() => TavernMinionStats.Parse(body));
				return new Result(stats, "StatsNoRound", DateTime.UtcNow.AddHours(1));
			}
			var retry = resp?.Headers.RetryAfter;
			var wait = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow);
			if(wait.HasValue && wait.Value > delay)
				delay = wait.Value;
			return new Result(null, resp?.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
				? "StatsDenied" : "StatsOffline", DateTime.UtcNow + delay);
		}
		catch(Exception ex)
		{
			Log.Warn($"Battlegrounds minion statistics unavailable ({ex.GetType().Name})");
			return new Result(null, "StatsOffline", DateTime.UtcNow.AddMinutes(2));
		}
	}
}

internal sealed class TavernMinionStats
{
	private readonly Dictionary<(int Id, bool Golden, int Turn), Round> _rounds = new();
	internal DateTimeOffset? AsOf { get; private set; }
	internal bool IsRecentSnapshot(DateTimeOffset now) => _rounds.Count > 0 && AsOf >= now.AddDays(-1) && AsOf <= now.AddMinutes(5);

	internal Round? Find(int id, bool golden, int turn) => _rounds.TryGetValue((id, golden, turn), out var row) ? row : null;

	internal static TavernMinionStats Parse(string json)
	{
		var root = JObject.Parse(json);
		var cards = root["series"]?["data"] as JArray ?? throw new InvalidDataException("Missing minion data");
		var table = new TavernMinionStats();
		if(root["as_of"] is { Type: JTokenType.Date } timestamp)
			table.AsOf = timestamp.ToObject<DateTimeOffset>();
		foreach(var card in cards.OfType<JObject>())
		{
			var id = Number(card, "minion_dbf_id");
			if(id is null or <= 0 or > int.MaxValue)
				continue;
			foreach(var golden in new[] {false, true})
			{
				if(card[golden ? "premium_aggregates" : "normal_aggregates"] is not JArray rounds)
					continue;
				foreach(var row in rounds.OfType<JObject>())
				{
					var turn = Number(row, "combat_round");
					if(turn is null or < 1 or > 100)
						continue;
					table._rounds[((int)id.Value, golden, (int)turn.Value)] = new Round(row);
				}
			}
		}
		return table;
	}

	private static long? Number(JObject row, string key)
		=> row[key] is { Type: JTokenType.Integer } token && long.TryParse(token.ToString(), out var n) && n >= 0 ? n : null;

	internal sealed class Round
	{
		public double? Impact { get; }
		public double? WinRate { get; }
		private readonly long? _games;
		private readonly long? _combats;

		internal Round(JObject row)
		{
			_games = Number(row, "count_of_games_with_minion");
			var with = Average(_games, Number(row, "sum_of_placements_for_players_with_minion"));
			var without = Average(Number(row, "count_of_games_without_minion"), Number(row, "sum_of_placements_for_players_without_minion"));
			Impact = without - with;
			var wins = Number(row, "total_wins");
			var losses = Number(row, "total_losses");
			if(wins.HasValue && losses.HasValue && wins.Value <= long.MaxValue - losses.Value)
			{
				_combats = wins + losses;
				if(_combats >= 30)
					WinRate = (double)wins.Value / _combats.Value;
			}
		}

		private static double? Average(long? games, long? sum)
		{
			if(games is null or < 30 || !sum.HasValue)
				return null;
			var mean = (double)sum.Value / games.Value;
			return mean is >= 1 and <= 8 ? mean : null;
		}

		internal string Describe()
		{
			var lines = new List<string>();
			if(Impact.HasValue)
				lines.Add(RecruitPlan.Text("StatsImpact", Impact.Value, _games ?? 0));
			if(WinRate.HasValue)
				lines.Add(RecruitPlan.Text("StatsWins", WinRate.Value, _combats ?? 0));
			return lines.Count > 0 ? string.Join("\n", lines) : RecruitPlan.Text("StatsNoRound");
		}
	}
}
