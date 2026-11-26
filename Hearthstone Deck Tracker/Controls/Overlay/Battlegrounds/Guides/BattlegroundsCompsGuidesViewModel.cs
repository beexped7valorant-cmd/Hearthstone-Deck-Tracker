using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HearthDb.Enums;
using HearthMirror;
using Hearthstone_Deck_Tracker.Commands;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides.Comps;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility.Analytics;
using Hearthstone_Deck_Tracker.Utility.Battlegrounds;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.MVVM;
using HSReplay.Requests;

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides;

public enum CompGuideListState
{
	Loading,
	BaseFeature,
	Tier7Feature,
	Empty,
	Error
}

public class BattlegroundsCompsGuidesViewModel : ViewModel
{
	public BattlegroundsCompsGuidesViewModel()
	{
		ToggleTargetCommand = new Command(() => {
			if(SelectedComp != null && HasLobbyCoreCards)
				TargetComp = TargetComp == SelectedComp ? null : SelectedComp;
		});
		ToggleGroupingCommand = new Command(() => {
			_groupByRank = !_groupByRank;
			OnPropertyChanged(nameof(GroupingLabel));
			OnPropertyChanged(nameof(CoreGroups));
		});
		RetryCommand = new Command(async () => await RetryLoadCompGuides());
		HSReplayNetOAuth.AccountDataUpdated += OnAccountChanged;
		HSReplayNetOAuth.LoggedOut += OnAccountChanged;
	}

	private void OnAccountChanged()
	{
		if(IsPreLobby)
			OnPreLobby();
		else
			RefreshLobbyCoreCards();
	}

	public ICommand RetryCommand { get; }

	public bool IsRetrying
	{
		get => GetProp(false);
		private set => SetProp(value);
	}

	public List<BattlegroundsCompGuideViewModel>? Comps
	{
		get => GetProp<List<BattlegroundsCompGuideViewModel>?>(null);
		private set
		{
			SetProp(value);
			UpdateState();
		}
	}

	public Dictionary<int, TieredComps>? CompsByTier
	{
		get => GetProp<Dictionary<int, TieredComps>?>(null);
		private set
		{
			SetProp(value);
			UpdateState();
		}
	}

	public CompGuideListState CurrentState
	{
		get => GetProp(CompGuideListState.Loading);
		private set
		{
			SetProp(value);
			// Notify all visibility properties when state changes
			OnPropertyChanged(nameof(LoadingVisibility));
			OnPropertyChanged(nameof(BaseFeatureVisibility));
			OnPropertyChanged(nameof(Tier7FeatureVisibility));
			OnPropertyChanged(nameof(EmptyStateVisibility));
			OnPropertyChanged(nameof(ErrorVisibility));
		}
	}

	private void UpdateState()
	{
		if (HasError)
		{
			CurrentState = CompGuideListState.Error;
		}
		else if (CompsByTier != null)
		{
			var hasComps = CompsByTier.Values.Any(tier => tier.Comps?.Any() == true);
			CurrentState = hasComps ? CompGuideListState.Tier7Feature : CompGuideListState.Empty;
		}
		else if (Comps != null)
		{
			CurrentState = Comps.Any() ? CompGuideListState.BaseFeature : CompGuideListState.Empty;
		}
		else
		{
			CurrentState = CompGuideListState.Loading;
		}
	}

	public bool HasError
	{
		get => GetProp(false);
		private set
		{
			SetProp(value);
			UpdateState();
		}
	}

	public bool HasRetriedAndFailed
	{
		get => GetProp(false);
		private set => SetProp(value);
	}

	public async Task<List<BattlegroundsCompGuideViewModel>?> GetCompGuides()
	{
		try
		{
			var compsData = await ApiWrapper.GetCompsGuides(Helper.GetCardLanguage());
			var viewModelData = compsData?
				.OrderBy(comp => comp.Name)
				.Select(comp => new BattlegroundsCompGuideViewModel(comp, IsPreLobby))
				.ToList();
			return viewModelData;
		}
		catch (Exception ex)
		{
			Log.Error($"Error loading comps data: {ex.Message}");
			throw;
		}
	}

	public async Task<Dictionary<int,TieredComps>?> GetPremiumCompGuides(string? token, BattlegroundsCompGuidesParams parameters)
	{
		try
		{
			var compsData = token != null
				? await ApiWrapper.GetPremiumCompsGuides(token, parameters)
				: await HSReplayNetOAuth.MakeRequest(c => c.GetTier7CompsGuides(parameters));

			if(compsData?.ByTier == null)
				return null;

			var result = new Dictionary<int, TieredComps>();

			var compsByAdjustedTier = compsData.ByTier
				.SelectMany(t => t.Value)
				.GroupBy(comp => comp.AdjustedTier ?? comp.Tier)
				.OrderBy(g => g.Key);

			foreach(var group in compsByAdjustedTier)
			{
				var tier = group.Key;
				// demoted comps go to the top of their adjusted tier, promoted ones to the bottom
				var comps = group
					.OrderBy(comp => Math.Sign(comp.Tier - tier))
					.ThenBy(comp => comp.Tier)
					.ThenBy(comp => comp.TierRank)
					.ToList();

				var viewModels = comps.Select(comp => new BattlegroundsCompGuideViewModel(comp, IsPreLobby)).ToList();

				result[tier] = new TieredComps
				{
					TierLetter = GetCompText(tier),
					TierColor = GetTierColor(tier),
					Comps = viewModels
				};
			}

			return result;
		}
		catch (Exception ex)
		{
			Log.Error($"Error loading comps data: {ex.Message}");
			throw;
		}
	}


	public BattlegroundsCompGuideViewModel? SelectedComp
	{
		get => GetProp<BattlegroundsCompGuideViewModel?>(null);
		set
		{
			SetProp(value);
			OnPropertyChanged(nameof(IsCompSelected));
			OnPropertyChanged(nameof(TargetLabel));
		}
	}

	public bool IsCompSelected => SelectedComp != null;

	private readonly SemaphoreSlim _updateCompGuidesSemaphore = new (1, 1);

	private async Task RetryLoadCompGuides()
	{
		if (IsRetrying)
			return;

		try
		{
			IsRetrying = true;
			HasRetriedAndFailed = false;
			await _updateCompGuidesSemaphore.WaitAsync();
			await UpdateCompGuides();

			if (HasError)
			{
				HasRetriedAndFailed = true;
			}
		}
		finally
		{
			_updateCompGuidesSemaphore.Release();
			IsRetrying = false;
		}
	}

	public bool IsPreLobby
	{
		get => GetProp(false);
		private set => SetProp(value);
	}

	private Guid? _guidesGameId;

	public IReadOnlyList<LobbyCoreCard> LobbyCoreCards
	{
		get => GetProp<IReadOnlyList<LobbyCoreCard>>(Array.Empty<LobbyCoreCard>())!;
		private set
		{
			SetProp(value);
			if(value.Count == 0)
				TargetComp = null;
			OnPropertyChanged(nameof(HasLobbyCoreCards));
			OnPropertyChanged(nameof(CoreGroups));
		}
	}

	public bool HasLobbyCoreCards => LobbyCoreCards.Count > 0;

	public ICommand ToggleTargetCommand { get; }
	public ICommand ToggleGroupingCommand { get; }
	private bool _groupByRank;
	public string GroupingLabel => RecruitPlan.Text(_groupByRank ? "ByRank" : "ByTavern");
	public string TargetLabel => RecruitPlan.Text(TargetComp != null && TargetComp == SelectedComp ? "UnpinComp" : "PinComp");
	public BattlegroundsCompGuideViewModel? TargetComp
	{
		get => GetProp<BattlegroundsCompGuideViewModel?>(null);
		private set { SetProp(value); OnPropertyChanged(nameof(TargetLabel)); }
	}

	public IEnumerable<IGrouping<string, LobbyCoreCard>> CoreGroups
	{
		get
		{
			if(!_groupByRank)
				return LobbyCoreCards.GroupBy(c => RecruitPlan.Text("TavernTier", c.Tier));
			return LobbyCoreCards.SelectMany(c => c.Guides.Select(g => g.CompGuide.AdjustedTier ?? g.CompGuide.Tier)
					.Distinct().Select(rank => (rank, card: c)))
				.OrderBy(x => x.rank).ThenBy(x => x.card.Tier).ThenBy(x => x.card.Card.LocalizedName)
				.GroupBy(x => GetCompText(x.rank) + " · " + RecruitPlan.Text("TavernTier", x.card.Tier), x => x.card);
		}
	}

	internal static int NormalId(Hearthstone.Card card)
	{
		var normal = card.GetTag(GameTag.BACON_TRIPLED_BASE_MINION_ID);
		return normal > 0 ? normal : card.DbfId;
	}

	internal BattlegroundsCompGuideViewModel? SuggestedComp(IEnumerable<int> owned)
	{
		if(!HasLobbyCoreCards || TargetComp != null)
			return null;
		var ids = new HashSet<int>(owned);
		var matches = LobbyCoreCards.Where(c => ids.Contains(c.Card.DbfId)).SelectMany(c => c.Guides)
			.GroupBy(g => g).OrderByDescending(g => g.Count()).Take(2).ToList();
		return matches.Count > 0 && matches[0].Count() >= 2 && (matches.Count == 1 || matches[0].Count() > matches[1].Count())
			? matches[0].Key : null;
	}

	internal string? HeroFit(Hearthstone.Card card)
	{
		var hero = Core.Game.Player.Hero?.Card;
		if(hero == null)
			return null;
		var guide = Core.Overlay.BattlegroundsHeroGuideListViewModel.GetHeroGuide(hero.BattlegroundsSkinParentId > 0 ? hero.BattlegroundsSkinParentId : hero.DbfId);
		if(guide?.FavorableTribes is not { Count: > 0 } tribes || guide.Hidden)
			return null;
		var available = BattlegroundsUtils.GetAvailableRaces();
		var matches = tribes.Select(r => (Race)r).Where(r => available?.Contains(r) == true && card.HasRace(r))
			.Select(HearthDbConverter.GetLocalizedBattlegroundsRace).Where(name => !string.IsNullOrEmpty(name)).ToList();
		return matches.Count == 0 ? null : RecruitPlan.Text("HeroFit", hero.LocalizedName ?? "", string.Join(", ", matches));
	}

	internal string? CardFit(Hearthstone.Card card, BattlegroundsCompGuideViewModel? suggestion)
	{
		var row = LobbyCoreCards.FirstOrDefault(c => c.Card.DbfId == NormalId(card));
		var focus = TargetComp ?? suggestion;
		var comp = row == null ? null : focus != null && row.Guides.Contains(focus)
			? RecruitPlan.Text(TargetComp != null ? "TargetCore" : "SuggestedCore", focus.CompGuide.Name)
			: RecruitPlan.Text("OtherCore", row.Compositions);
		return string.Join("\n", new[] {comp, HeroFit(card)}.Where(line => line != null));
	}

	public sealed class LobbyCoreCard
	{
		public Hearthstone.Card Card { get; }
		public int Tier { get; }
		public string Compositions { get; }

		public CardAssetViewModel Art { get; }
		public IReadOnlyList<BattlegroundsCompGuideViewModel> Guides { get; }
		internal LobbyCoreCard(Hearthstone.Card card, int tier, IEnumerable<BattlegroundsCompGuideViewModel> guides)
		{
			Card = card;
			Tier = tier;
			Guides = guides.Distinct().OrderBy(g => g.CompGuide.AdjustedTier ?? g.CompGuide.Tier).ThenBy(g => g.CompGuide.Name).ToList();
			Compositions = string.Join(" · ", Guides.Select(g => $"{g.TierText} {g.CompGuide.Name}"));
			Art = new CardAssetViewModel(card, Utility.Assets.CardAssetType.Tile);
		}
	}

	private void RefreshLobbyCoreCards()
	{
		if(IsPreLobby || HasError || _guidesGameId == null || _guidesGameId != Core.Game.CurrentGameStats?.GameId
			|| CompsByTier == null || !(HSReplayNetOAuth.AccountData?.IsTier7 == true || Tier7Trial.IsTrialForCurrentGameActive(Core.Game.MetaData.ServerInfo?.GameHandle))
			|| BattlegroundsDbSingleton.Current.LobbyCardTiers is not { } pool)
		{
			LobbyCoreCards = Array.Empty<LobbyCoreCard>();
			return;
		}

		var guides = CompsByTier.Values.SelectMany(group => group.Comps ?? Enumerable.Empty<BattlegroundsCompGuideViewModel>());
		LobbyCoreCards = guides.SelectMany(g => g.CompGuide.CoreCards.Select(id => (id, guide: g)))
			.Where(entry => pool.ContainsKey(entry.id))
			.GroupBy(entry => entry.id)
			.Select(group => Database.GetCardFromDbfId(group.Key, false) is { IsKnownCard: true } card
				? new LobbyCoreCard(card, pool[group.Key], group.Select(entry => entry.guide)) : null)
			.OfType<LobbyCoreCard>()
			.OrderBy(row => row.Tier).ThenBy(row => row.Card.LocalizedName).ToList();
		if(TargetComp is { } target)
			TargetComp = guides.FirstOrDefault(g => g.CompGuide.Name == target.CompGuide.Name);
	}

	public void OnMinionPoolChanged()
	{
		var comps = CompsByTier?.Values.Where(v => v.Comps != null).SelectMany(v => v.Comps!) ?? Comps;
		foreach(var comp in comps ?? Enumerable.Empty<BattlegroundsCompGuideViewModel>())
			comp.OnMinionPoolChanged();
		RefreshLobbyCoreCards();
	}

	public async void OnMatchStart()
	{
		_guidesGameId = null;
		LobbyCoreCards = Array.Empty<LobbyCoreCard>();
		if(IsPreLobby)
		{
			IsPreLobby = false;
			// the guides are rebuilt below, so a comp selected in the lobby would point at a stale view model
			SelectedComp = null;
		}

		if(Core.Game.Spectator)
			await Task.Delay(1500);

		try
		{
			await _updateCompGuidesSemaphore.WaitAsync();
			await UpdateCompGuides();
		}
		finally
		{
			_updateCompGuidesSemaphore.Release();
		}
	}

	public async void OnPreLobby()
	{
		_guidesGameId = null;
		LobbyCoreCards = Array.Empty<LobbyCoreCard>();
		IsPreLobby = true;
		try
		{
			await _updateCompGuidesSemaphore.WaitAsync();
			SelectedComp = null;
			await UpdateCompGuides();
		}
		finally
		{
			_updateCompGuidesSemaphore.Release();
		}
	}

	private async Task UpdateCompGuides()
	{
		// Always refresh the comp guides
		await TrySetCompsGuides();
	}

	private async Task TrySetCompsGuides()
	{
		if(IsPreLobby)
		{
			// no trial is activated here, spending one before the player has even queued is not worth it
			if(HSReplayNetOAuth.AccountData?.IsTier7 ?? false)
				// there is no minion pool before a match, so ask for the unfiltered list
				await SetPremiumCompGuides(null, new BattlegroundsCompGuidesParams
				{
					MinionTypes = Array.Empty<int>(),
					LanguageCode = Helper.GetCardLanguage(),
				});
			else
				await SetFreeCompGuides();
			return;
		}

		if(!Tier7Trial.IsAvailable)
		{
			await SetFreeCompGuides();
			return;
		}

		HashSet<Race>? availableRaces;
		try
		{
			availableRaces = await BattlegroundsUtils.WaitForAvailableRaces();
		}
		catch(OperationCanceledException)
		{
			return;
		}

		if(availableRaces == null)
		{
			HandleCompGuidesError("NoRaces", "Unable to get available races");
			return;
		}

		var access = await Tier7Trial.GetAccess();
		if(access != null)
			await SetPremiumCompGuides(access.TrialToken, new BattlegroundsCompGuidesParams
			{
				MinionTypes = availableRaces.Cast<int>().ToArray(),
				DeityDbfId = BattlegroundsUtils.GetBattlegroundsDeityDbfId(Core.Game.GameEntity),
				AnomalyDbfId = BattlegroundsUtils.GetBattlegroundsAnomalyDbfId(Core.Game.GameEntity),
				LanguageCode = Helper.GetCardLanguage(),
				GameType = (int)HearthDbConverter.GetBnetGameType(Core.Game.CurrentGameType, Core.Game.CurrentFormat),
			});
		else
			await SetFreeCompGuides();
	}

	private async Task SetPremiumCompGuides(string? token, BattlegroundsCompGuidesParams parameters)
	{
#if(DEBUG)
		Log.Debug("Fetching Premium Battlegrounds Comp Guides...");
#endif

		var gameId = IsPreLobby ? null : Core.Game.CurrentGameStats?.GameId;
		Dictionary<int, TieredComps>? guides = null;
		try
		{
			guides = await GetPremiumCompGuides(token, parameters);
		}
		catch(Exception e)
		{
			HandleCompGuidesError(e.GetType().Name, e.Message);
		}

		if(guides is not null)
		{
			// UpdateState prefers CompsByTier, so the free list has to go before it is set
			Comps = null;
			CompsByTier = guides;
			HasError = false;
			_guidesGameId = gameId;
			RefreshLobbyCoreCards();
		}
		else if(!HasError)
			HandleCompGuidesError("NoData", "Premium comp guides request returned no data");
	}

	private async Task SetFreeCompGuides()
	{
		_guidesGameId = null;
		LobbyCoreCards = Array.Empty<LobbyCoreCard>();
#if(DEBUG)
		Log.Debug("Fetching Battlegrounds Comp Guides...");
#endif

		List<BattlegroundsCompGuideViewModel>? guides = null;
		try
		{
			guides = await GetCompGuides();
		}
		catch(Exception e)
		{
			HandleCompGuidesError(e.GetType().Name, e.Message);
		}

		if(guides is not null)
		{
			CompsByTier = null;
			Comps = guides;
			HasError = false;
		}
		else if(!HasError)
			HandleCompGuidesError("NoData", "Comp guides request returned no data");
	}

	public void OnMatchEnd()
	{
		_guidesGameId = null;
		LobbyCoreCards = Array.Empty<LobbyCoreCard>();
		IsPreLobby = false;
		CompsByTier = null;
		SelectedComp = null;
		HasError = false;
		HasRetriedAndFailed = false;
	}

	public class TieredComps
	{
		public string? TierLetter { get; set; }
		public LinearGradientBrush? TierColor { get; set; }
		public List<BattlegroundsCompGuideViewModel>? Comps { get; set; }
	}

	private string GetCompText(int tier)
	{
		return tier switch
		{
			1 => "S",
			2 => "A",
			3 => "B",
			4 => "C",
			5 => "D",
			_ => "?"
		};
	}


	private LinearGradientBrush GetTierColor(int tier)
	{
		LinearGradientBrush CreateLinearGradientBrush(Color color1, Color color2)
		{
			var brush = new LinearGradientBrush
			{
				StartPoint = new Point(0, 0.5),
				EndPoint = new Point(1, 0.5)
			};
			brush.GradientStops.Add(new GradientStop(color1, 0.0));
			brush.GradientStops.Add(new GradientStop(color2, 1.0));

			return brush;
		}

		return tier switch
		{
			1 => CreateLinearGradientBrush(Color.FromRgb(64, 138, 191), Color.FromRgb(56, 95, 122)),
			2 => CreateLinearGradientBrush(Color.FromRgb(107, 160, 54), Color.FromRgb(88, 121, 55)),
			3 => CreateLinearGradientBrush(Color.FromRgb(146, 160, 54), Color.FromRgb(104, 121, 55)),
			4 => CreateLinearGradientBrush(Color.FromRgb(160, 124, 54), Color.FromRgb(121, 95, 55)),
			5 => CreateLinearGradientBrush(Color.FromRgb(160, 72, 54), Color.FromRgb(121, 66, 55)),
			_ => CreateLinearGradientBrush(Color.FromRgb(112, 112, 112), Color.FromRgb(64, 64, 64))
		};
	}

	private void HandleCompGuidesError(string type, string message)
	{
		_guidesGameId = null;
		LobbyCoreCards = Array.Empty<LobbyCoreCard>();
		HasError = true;
		Influx.OnGetBattlegroundsCompositionGuidesError(type, message);
	}

	// Exclusive state visibility properties
	public Visibility LoadingVisibility => CurrentState == CompGuideListState.Loading ? Visibility.Visible : Visibility.Collapsed;
	public Visibility BaseFeatureVisibility => CurrentState == CompGuideListState.BaseFeature ? Visibility.Visible : Visibility.Collapsed;
	public Visibility Tier7FeatureVisibility => CurrentState == CompGuideListState.Tier7Feature ? Visibility.Visible : Visibility.Collapsed;
	public Visibility EmptyStateVisibility => CurrentState == CompGuideListState.Empty ? Visibility.Visible : Visibility.Collapsed;
	public Visibility ErrorVisibility => CurrentState == CompGuideListState.Error ? Visibility.Visible : Visibility.Collapsed;

	public void Reset()
	{
		_guidesGameId = null;
		LobbyCoreCards = Array.Empty<LobbyCoreCard>();
		IsPreLobby = false;
		Comps = null;
		CompsByTier = null;
		SelectedComp = null;
		HasError = false;
		HasRetriedAndFailed = false;
	}
}
