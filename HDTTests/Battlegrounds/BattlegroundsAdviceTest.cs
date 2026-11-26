using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hearthstone_Deck_Tracker.Utility.Battlegrounds;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Session;
using static Hearthstone_Deck_Tracker.Utility.Battlegrounds.BattlegroundsLastGames;

namespace HDTTests.Battlegrounds
{
	[TestClass]
	public class BattlegroundsAdviceTest
	{
		[TestMethod]
		public void SoloAndDuosUseTheCorrectHalfOfTheLobby()
		{
			var solo = PlacementRates.FromDistribution(new double[] { 1, 2, 3, 4, 3, 3, 2, 2 }, false);
			Assert.AreEqual(50.0, solo.TopHalf);
			Assert.AreEqual(5.0, solo.First);
			var duos = PlacementRates.FromDistribution(new double[] { 10, 20, 30, 40, 0, 0, 0, 0 }, true);
			Assert.AreEqual(30.0, duos.TopHalf);
			Assert.IsNull(PlacementRates.FromDistribution(new double[8], false));
			Assert.IsNull(PlacementRates.FromDistribution(new double[] { 10, 20, 30, 40, 1, 0, 0, 0 }, true));
		}

		[TestMethod]
		public void RecapFiltersUnknownPlacementsFriendliesAndOtherLadders()
		{
			var games = new[] {
				new GameItem { Duos = true, Placement = 1 }, new GameItem { Duos = true, Placement = 4 },
				new GameItem { Duos = true, Placement = 0 }, new GameItem { Placement = 1 },
				new GameItem { Duos = true, FriendlyGame = true, Placement = 1 }
			};
			var summary = SessionPlacements.FromGames(games, true);
			Assert.AreEqual(2, summary.Games);
			Assert.AreEqual(1, summary.TopFinishes);
			Assert.AreEqual(1, summary.Firsts);
			Assert.AreEqual(2.5, summary.Mean);
		}

		[TestMethod]
		public void UpgradeWindowTracksCurrentTierAndRealCost()
		{
			Assert.IsNull(RecruitPlan.Level(8, 4, 4, 10, 9, 35));
			StringAssert.Contains(RecruitPlan.Level(4, 2, 6, 6, 6, 35), RecruitPlan.Text("InTurns", 1));
			StringAssert.Contains(RecruitPlan.Level(5, 2, 6, 2, 4, 35), RecruitPlan.Text("NeedGold", 2));
			StringAssert.Contains(RecruitPlan.Level(8, 4, 6, 10, 8, 12), RecruitPlan.Text("Stabilize"));
		}

		[TestMethod]
		public void GiftChecksUsesGoldDangerAndLevelBudget()
		{
			Assert.IsNull(RecruitPlan.Gift(2, 1, 4, 3, 3, false, null, 30));
			Assert.IsNull(RecruitPlan.Gift(8, 4, 10, 3, 1, true, null, 30));
			Assert.IsNull(RecruitPlan.Gift(8, 4, 10, 3, 0, false, null, 30));
			Assert.AreEqual(RecruitPlan.Text("GiftGold", 1), RecruitPlan.Gift(5, 2, 2, 3, 2, false, 4, 12));
			Assert.AreEqual(RecruitPlan.Text("GiftTempo"), RecruitPlan.Gift(5, 2, 6, 3, 2, false, 4, 12));
			Assert.AreEqual(RecruitPlan.Text("GiftAfterLevel"), RecruitPlan.Gift(5, 2, 6, 3, 2, false, 4, 35));
			Assert.AreEqual(RecruitPlan.Text("GiftTribe"), RecruitPlan.Gift(7, 4, 8, 3, 2, false, null, 35));
		}
	}
}
