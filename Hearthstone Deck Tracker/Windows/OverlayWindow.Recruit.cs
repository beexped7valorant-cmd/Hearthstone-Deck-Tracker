using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HearthDb.Enums;
using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Battlegrounds;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;

namespace Hearthstone_Deck_Tracker.Windows;

public partial class OverlayWindow
{
	internal TavernStatsCache TavernStats { get; } = new();
	private Guid? _shopMatch;
	private Card? _hoveredShopCard;
	private IReadOnlyList<Card> _shop = Array.Empty<Card>();
	internal IEnumerable<Card> CurrentShopCards => _shopMatch != null && _shopMatch == Core.Game.CurrentGameStats?.GameId ? _shop : Array.Empty<Card>();

	internal void UpdateRecruitShop(IReadOnlyList<BoardCard> cards)
	{
		_shopMatch = Core.Game.CurrentGameStats?.GameId;
		if(_game.IsBattlegroundsCombatPhase || IsViewingBGsTeammate || _shopMatch == null)
		{
			_hoveredShopCard = null;
			_shop = Array.Empty<Card>();
			return;
		}
		_shop = cards.Select(c => Database.GetCardFromId(c?.CardId)).OfType<Card>().ToList();
		var id = cards.FirstOrDefault(c => c?.Hovered == true)?.CardId;
		_hoveredShopCard = _shop.FirstOrDefault(c => c.Id == id);
	}

	private void UpdateRecruitPanels()
	{
		var game = Core.Game;
		if(!game.IsBattlegroundsMatch || game.IsInMenu || game.IsBattlegroundsCombatPhase || IsViewingBGsTeammate || game.Spectator
			|| game.PlayerEntity is not { } player || player.GetTag(GameTag.PLAYSTATE) is not (0 or (int)PlayState.PLAYING) || game.GetTurnNumber() < 1)
		{
			RecruitAdvicePanel.Visibility = TavernHoverPanel.Visibility = Visibility.Collapsed;
			_shopMatch = null;
			_hoveredShopCard = null;
			return;
		}

		var scale = Math.Min(CanvasInfo.ActualWidth / 1440, CanvasInfo.ActualHeight / 900);
		if(scale <= 0)
			return;
		var vm = game.BattlegroundsSessionViewModel;
		SetRecruitLine(RecruitLevel, vm.LevelAdvice);
		SetRecruitLine(RecruitDirection, vm.DirectionAdvice);
		SetRecruitLine(RecruitGift, vm.GiftAdvice);
		RecruitAdvicePanel.Visibility = Config.Instance.ShowBattlegroundsRecruitAdvice && (vm.LevelAdvice != null || vm.GiftAdvice != null || vm.DirectionAdvice != null)
			? Visibility.Visible : Visibility.Collapsed;
		RecruitAdviceScale.ScaleX = RecruitAdviceScale.ScaleY = scale;
		Canvas.SetLeft(RecruitAdvicePanel, Math.Min(CanvasInfo.ActualWidth - RecruitAdvicePanel.Width * scale - 4, CanvasInfo.ActualWidth * 0.67));
		Canvas.SetTop(RecruitAdvicePanel, Math.Max(0, CanvasInfo.ActualHeight * 0.94 - RecruitAdvicePanel.ActualHeight * scale));

		var card = _hoveredShopCard;
		if(!Config.Instance.ShowBattlegroundsShopStats || !Config.Instance.EnableBattlegroundsTier7Overlay || _shopMatch == null
			|| _shopMatch != game.CurrentGameStats?.GameId || card?.IsKnownCard != true || game.Player.OfferedEntityIds.Count > 0
			|| GetCursorPos() is not { } pos || pos.X < 0 || pos.X > CanvasInfo.ActualWidth || pos.Y < 0 || pos.Y > CanvasInfo.ActualHeight)
		{
			TavernHoverPanel.Visibility = Visibility.Collapsed;
			return;
		}

		var turn = game.GetTurnNumber();
		var golden = card.GetTag(GameTag.BACON_TRIPLED_BASE_MINION_ID) > 0;
		SetRecruitLine(HoverTitle, card.LocalizedName);
		string? sample = null;
		string stats;
		if(game.IsBattlegroundsDuosMatch)
			stats = RecruitPlan.Text("StatsDuos");
		else if(card.TypeEnum != CardType.MINION)
			stats = RecruitPlan.Text("StatsSpell");
		else
		{
			var account = HSReplayNetOAuth.AccountData;
			var result = TavernStats.Get(account?.IsTier7 == true ? account.Id : (int?)null);
			sample = RecruitPlan.Text(golden ? "GoldenSample" : "NormalSample", turn);
			if(result?.Imported == true)
				sample = RecruitPlan.Text(golden ? "GoldenSaved" : "NormalSaved", turn, result.Stats?.AsOf?.ToLocalTime() ?? DateTimeOffset.Now);
			stats = result?.Stats?.Find(BattlegroundsCompsGuidesViewModel.NormalId(card), golden, turn)?.Describe()
				?? RecruitPlan.Text(result?.Missing ?? "StatsLoading");
		}
		SetRecruitLine(HoverSample, sample);
		SetRecruitLine(HoverStats, stats);
		SetRecruitLine(HoverContext, BattlegroundsCompsGuidesVM.CardFit(card, vm.Suggestion));
		TavernHoverScale.ScaleX = TavernHoverScale.ScaleY = scale;
		Canvas.SetLeft(TavernHoverPanel, Math.Max(2, Math.Min(pos.X + 16 * scale, CanvasInfo.ActualWidth - TavernHoverPanel.Width * scale - 4)));
		Canvas.SetTop(TavernHoverPanel, Math.Max(0, Math.Min(pos.Y + 34 * scale, CanvasInfo.ActualHeight - TavernHoverPanel.ActualHeight * scale)));
		TavernHoverPanel.Visibility = Visibility.Visible;
	}

	private static void SetRecruitLine(TextBlock line, string? text)
	{
		if(line.Text != (text ?? ""))
			line.Text = text ?? "";
		line.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
	}
}
