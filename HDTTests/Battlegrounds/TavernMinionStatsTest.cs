using System.IO;
using Hearthstone_Deck_Tracker.Utility.Battlegrounds;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HDTTests.Battlegrounds
{
	[TestClass]
	public class TavernMinionStatsTest
	{
		private const string Row = "{'combat_round':7,'count_of_games_with_minion':40,'count_of_games_without_minion':50," +
			"'sum_of_placements_for_players_with_minion':120,'sum_of_placements_for_players_without_minion':200," +
			"'total_wins':30,'total_losses':10}";

		private static TavernMinionStats Load(string normal, string golden)
		{
			return TavernMinionStats.Parse("{'series':{'data':[{'minion_dbf_id':123,'normal_aggregates':[" + normal +
				"],'premium_aggregates':[" + golden + "]}]}}");
		}

		[TestMethod]
		public void UsesExactCardTurnAndGoldenVariant()
		{
			var stats = Load(Row, Row.Replace("'total_wins':30", "'total_wins':90"));
			Assert.AreEqual(1.0, stats.Find(123, false, 7).Impact.Value);
			Assert.AreEqual(0.75, stats.Find(123, false, 7).WinRate.Value);
			Assert.AreEqual(0.9, stats.Find(123, true, 7).WinRate.Value);
			Assert.IsNull(stats.Find(123, true, 6));
			Assert.IsNull(stats.Find(456, false, 7));
		}

		[TestMethod]
		public void ValidatesPlacementAndCombatSamplesIndependently()
		{
			var stats = Load(Row.Replace(":120", ":20"), Row.Replace("'total_wins':30", "'total_wins':0"));
			Assert.IsNull(stats.Find(123, false, 7).Impact);
			Assert.AreEqual(0.75, stats.Find(123, false, 7).WinRate.Value);
			Assert.AreEqual(1.0, stats.Find(123, true, 7).Impact.Value);
			Assert.IsNull(stats.Find(123, true, 7).WinRate);
			var small = Load("{'combat_round':7,'total_wins':0,'total_losses':0}", "");
			Assert.IsNull(small.Find(123, false, 7).Impact);
			Assert.IsNull(small.Find(123, false, 7).WinRate);
			Assert.IsNull(small.Find(123, true, 7));
		}

		[TestMethod]
		public void RejectsMissingSeries()
		{
			Assert.ThrowsException<InvalidDataException>(() => TavernMinionStats.Parse("{}"));
		}

		[TestMethod]
		public void RecognizesShopCoreAndUnknownTavern()
		{
			Assert.AreEqual(RecruitPlan.Text("GiftShopCore", "X"), RecruitPlan.Gift(7, 3, 8, 3, 1, false, 7, 20, "X"));
			Assert.IsNull(RecruitPlan.Gift(7, 3, 8, 3, 0, false, 7, 20, "X"));
			Assert.AreEqual(RecruitPlan.Text("LevelUnknown", 4), RecruitPlan.Level(4, 0, 6, 6, null, 30));
		}
	}
}
