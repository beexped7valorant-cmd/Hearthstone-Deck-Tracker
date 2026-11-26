using System;
using System.IO;
using HearthDb.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
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
		public void IgnoresPreviousSubscriberRequestAndDeduplicatesHovers()
		{
			var requests = new List<TaskCompletionSource<TavernStatsCache.Result>>();
			var cache = new TavernStatsCache(() => {
				var work = new TaskCompletionSource<TavernStatsCache.Result>();
				requests.Add(work);
				return work.Task;
			});
			Assert.IsNull(cache.Get(10));
			Assert.IsNull(cache.Get(10));
			Assert.AreEqual(1, requests.Count);
			Assert.IsNull(cache.Get(11));
			requests[0].SetResult(new TavernStatsCache.Result(null, "first account", DateTime.UtcNow.AddHours(1)));
			Assert.IsNull(cache.Get(11));
			requests[1].SetResult(new TavernStatsCache.Result(null, "second account", DateTime.UtcNow.AddHours(1)));
			Assert.AreEqual("second account", cache.Get(11).Missing);
			Assert.AreEqual("StatsAccess", cache.Get(null).Missing);
			Assert.IsNull(cache.Get(11));
			Assert.AreEqual(3, requests.Count);
		}

		[TestMethod]
		public void ValidatesTimestampOfSavedData()
		{
			var now = new DateTimeOffset(2026, 10, 20, 21, 0, 0, TimeSpan.Zero);
			var stats = TavernMinionStats.Parse("{'as_of':'2026-10-20T20:00:00Z','series':{'data':[" +
				"{'minion_dbf_id':123,'normal_aggregates':[" + Row + "]}]}}");
			Assert.IsTrue(stats.IsRecentSnapshot(now));
			Assert.IsFalse(stats.IsRecentSnapshot(now.AddDays(2)));
			Assert.IsFalse(stats.IsRecentSnapshot(now.AddDays(-1)));
			Assert.IsFalse(Load(Row, "").IsRecentSnapshot(now));
		}

		[TestMethod]
		public void OnlyMarksAUniqueGiftThatFitsThePlayer()
		{
			Assert.AreEqual(1, RecruitPlan.PreferredGift(new[] { 1, 2, 0 }));
			Assert.AreEqual(-1, RecruitPlan.PreferredGift(new[] { 2, 2, 0 }));
			Assert.AreEqual(-1, RecruitPlan.PreferredGift(new[] { 0, 0, 0 }));
			Assert.AreEqual(-1, RecruitPlan.PreferredGift(new[] { 2, 0 }));
			Assert.AreEqual(RecruitPlan.Text("GiftPrice", 3), RecruitPlan.Gift(8, 3, null, 3, 1, false, 5, 20));
		}

		[TestMethod]
		public void RejectsMissingSeries()
		{
			Assert.ThrowsException<InvalidDataException>(() => TavernMinionStats.Parse("{}"));
		}

		[TestMethod]
		public void GiftTimingFollowsBoardMajorityOnlyFromTurnSeven()
		{
			Assert.IsNull(RecruitPlan.LeadingTribe(new[] { (Race.DRAGON, 2), (Race.BEAST, 2) }));
			Assert.IsNull(RecruitPlan.LeadingTribe(new[] { (Race.DRAGON, 0) }));
			Assert.AreEqual(Race.BEAST, RecruitPlan.LeadingTribe(new[] { (Race.DRAGON, 1), (Race.BEAST, 2) }));
			Assert.AreEqual(RecruitPlan.Text("GiftEarly"), RecruitPlan.Gift(6, 4, 10, 3, 1, false, null, 30, null, "A", "B"));
			Assert.AreEqual(RecruitPlan.Text("GiftDifferent", "A", "B"), RecruitPlan.Gift(7, 4, 10, 3, 1, false, null, 30, null, "A", "B"));
			Assert.AreEqual(RecruitPlan.Text("GiftLeader", "A"), RecruitPlan.Gift(7, 4, 10, 3, 1, false, null, 30, null, "A", null));
			Assert.AreEqual(RecruitPlan.Text("GiftTempo"), RecruitPlan.Gift(7, 4, 10, 3, 1, false, null, 12, null, "A", "B"));
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
