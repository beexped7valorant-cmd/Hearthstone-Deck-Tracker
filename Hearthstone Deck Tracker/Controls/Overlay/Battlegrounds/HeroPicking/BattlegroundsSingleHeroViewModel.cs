using System;
using System.Linq;
using Hearthstone_Deck_Tracker.Utility.Battlegrounds;
using Hearthstone_Deck_Tracker.Utility.MVVM;
using HSReplay.Responses;

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.HeroPicking
{
	public class BattlegroundsSingleHeroViewModel : ViewModel
	{
		public BattlegroundsHeroHeaderViewModel BgsHeroHeaderVM { get; }

		public int? HeroDbfId { get; }

		public double? TopHalfPercent { get; }
		public double? FirstPercent { get; }
		public string TopRange { get; }

		public bool LeadsTopHalf { get => GetProp(false); internal set => SetProp(value); }
		public bool LeadsFirst { get => GetProp(false); internal set => SetProp(value); }

		public BattlegroundsSingleHeroViewModel(BattlegroundsHeroPickStats.BattlegroundsSingleHeroPickStats? stats, Action<bool> onPlacementHover, bool duos = false)
		{
			HeroDbfId = stats?.HeroDbfId;
			var rates = PlacementRates.FromDistribution(stats?.PlacementDistribution, duos);
			TopRange = duos ? "1–2" : "1–4";
			TopHalfPercent = rates?.TopHalf;
			FirstPercent = rates?.First;
			BgsHeroHeaderVM = new(stats?.Tier, stats?.AvgPlacement, stats?.PickRate, rates != null ? stats!.PlacementDistribution!.Concat(Enumerable.Repeat(0.0, 8)).Take(8).ToArray() : new double[8], onPlacementHover);
		}
	}
}
