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

internal sealed class TavernMinionStats
{
	private const string Url = "https://hsreplay.net/analytics/query/battlegrounds_minion_list/?BattlegroundsMMRPercentile=TOP_50_PERCENT&BattlegroundsTimeRange=CURRENT_BATTLEGROUNDS_PATCH";
	private readonly Dictionary<(int Id, bool Golden, int Turn), Round> _rounds = new();
	private readonly string _missing;
	private DateTime _expires;
	private Task<TavernMinionStats>? _request;
	private int? _account;

	private TavernMinionStats(string missing, TimeSpan lifetime)
	{
		_missing = missing;
		_expires = DateTime.UtcNow + lifetime;
	}

	// Accessed from the overlay dispatcher. A completed request belongs only to the
	// account that started it, and never updates an individual hover control.
	internal TavernMinionStats() : this("StatsNoRound", TimeSpan.Zero) { }

	internal string Describe(int id, bool golden, int turn)
	{
		var account = HSReplayNetOAuth.AccountData;
		if(account?.IsTier7 != true)
		{
			_request = null;
			_account = null;
			return RecruitPlan.Text("StatsAccess");
		}

		if(_account != account.Id || _request == null || (_request.IsCompleted && _request.Result._expires <= DateTime.UtcNow))
		{
			_account = account.Id;
			_request = Fetch();
		}

		if(!_request.IsCompleted)
			return RecruitPlan.Text("StatsLoading");
		var result = _request.Result;
		return result.Find(id, golden, turn)?.Describe() ?? RecruitPlan.Text(result._missing);
	}

	private static async Task<TavernMinionStats> Fetch()
	{
		var delay = TimeSpan.FromMinutes(2);
		try
		{
			using var resp = await HSReplayNetOAuth.SendAsyncWithAuth(() => new HttpRequestMessage(HttpMethod.Get, Url));
			if(resp?.IsSuccessStatusCode == true)
			{
				var body = await resp.Content.ReadAsStringAsync();
				return await Task.Run(() => Parse(body));
			}
			var retry = resp?.Headers.RetryAfter;
			var wait = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow);
			if(wait > delay)
				delay = wait!.Value > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : wait.Value;
			return new TavernMinionStats(resp?.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
				? "StatsDenied" : "StatsOffline", delay);
		}
		catch(Exception ex)
		{
			Log.Warn($"Battlegrounds minion statistics unavailable ({ex.GetType().Name})");
			return new TavernMinionStats("StatsOffline", delay);
		}
	}

	internal Round? Find(int id, bool golden, int turn) => _rounds.TryGetValue((id, golden, turn), out var row) ? row : null;

	internal static TavernMinionStats Parse(string json)
	{
		var cards = JObject.Parse(json)["series"]?["data"] as JArray ?? throw new InvalidDataException("Missing minion data");
		var table = new TavernMinionStats("StatsNoRound", TimeSpan.FromHours(1));
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
