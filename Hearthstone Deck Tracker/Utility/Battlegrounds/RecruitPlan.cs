using System;
using System.Resources;
using System.Collections.Generic;
using System.Linq;

namespace Hearthstone_Deck_Tracker.Utility.Battlegrounds;

internal static class RecruitPlan
{
	private static readonly ResourceManager Strings = new("Hearthstone_Deck_Tracker.Properties.RecruitPlan", typeof(RecruitPlan).Assembly);
	internal static string Text(string key, params object[] args)
		=> string.Format(LocUtil.Culture, Strings.GetString(key, LocUtil.Culture) ?? key, args);

	// Standard curve. The late windows leave room to stabilize before committing gold.
	internal static (int Start, int End) Window(int nextTier) => nextTier switch
	{
		2 => (2, 2), 3 => (5, 5), 4 => (6, 7), 5 => (8, 9), 6 => (10, 12), _ => (0, 0)
	};

	internal static string? Level(int turn, int tier, int maxTier, int? gold, int? cost, int life)
	{
		var next = tier + 1;
		var (start, end) = Window(next);
		if(turn < 1 || tier < 1 || next > maxTier || start == 0)
			return tier == 0 && turn > 0 ? Text("LevelUnknown", turn) : null;

		var range = start == end ? start.ToString() : $"{start}–{end}";
		var heading = Text("LevelWindow", turn, next, range);
		if(cost is >= 0)
			heading += " · " + (gold.HasValue && gold >= cost.Value ? Text("LevelBudget", cost.Value, gold.Value - cost.Value) : Text("LevelPrice", cost.Value));
		if(turn < start)
			return heading + " · " + Text("InTurns", start - turn);
		if(gold.HasValue && cost.HasValue && gold.Value < cost.Value)
			return heading + " · " + Text("NeedGold", cost.Value - gold.Value);
		if(life > 0 && life <= 15 && next >= 4)
			return heading + " · " + Text("Stabilize");
		return heading + " · " + Text(turn > end ? "Late" : "Consider");
	}

	// Prefer a single clear fit. A tie or incomplete offer leaves all choices unmarked.
	internal static int PreferredGift(IReadOnlyList<int> fit)
	{
		if(fit.Count != 3)
			return -1;
		var best = fit.Max();
		if(best <= 0 || fit.Count(x => x == best) != 1)
			return -1;
		return Enumerable.Range(0, fit.Count).First(i => fit[i] == best);
	}

	internal static string? Gift(int turn, int tier, int? gold, int cost, int? charges, bool locked, int? levelCost, int life, string? shopCore = null)
	{
		if(turn < 3 || locked || charges == 0)
			return null;
		if(!gold.HasValue)
			return Text("GiftPrice", cost);
		if(gold.Value < cost)
			return Text("GiftGold", cost - gold.Value);
		if(shopCore != null)
			return Text("GiftShopCore", shopCore);
		if(life > 0 && life <= 15)
			return Text("GiftTempo");
		var window = Window(tier + 1);
		if(window.Start > 0 && turn >= window.Start && turn <= window.End && levelCost <= gold && gold - cost < levelCost)
			return Text("GiftAfterLevel");
		return Text(turn >= 7 ? "GiftTribe" : "GiftEarly");
	}
}
