using System;
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

		public BattlegroundsSingleHeroViewModel(BattlegroundsHeroPickStats.BattlegroundsSingleHeroPickStats? stats, Action<bool> onPlacementHover)
			: this(stats, onPlacementHover, false)
		{
		}

		internal BattlegroundsSingleHeroViewModel(BattlegroundsHeroPickStats.BattlegroundsSingleHeroPickStats? stats, Action<bool> onPlacementHover, bool duos)
		{
			HeroDbfId = stats?.HeroDbfId;
			var rates = PlacementRates.FromDistribution(stats?.PlacementDistribution, duos);
			TopRange = duos ? "1–2" : "1–4";
			TopHalfPercent = rates?.TopHalf;
			FirstPercent = rates?.First;
			var chart = new double[8];
			if(stats?.PlacementDistribution is { } distribution)
			{
				for(var i = 0; i < Math.Min(distribution.Length, chart.Length); i++)
				{
					if(distribution[i] >= 0 && distribution[i] <= 100)
						chart[i] = distribution[i];
				}
			}
			BgsHeroHeaderVM = new(stats?.Tier, stats?.AvgPlacement, stats?.PickRate, chart, onPlacementHover);
		}
	}
}
