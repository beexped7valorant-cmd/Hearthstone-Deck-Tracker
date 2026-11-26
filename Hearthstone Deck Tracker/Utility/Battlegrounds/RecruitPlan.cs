using System;
using System.Resources;

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

	internal static string? Level(int turn, int tier, int maxTier, int gold, int? cost, int life)
	{
		var next = tier + 1;
		var (start, end) = Window(next);
		if(turn < 1 || tier < 1 || next > maxTier || start == 0)
			return null;

		var range = start == end ? start.ToString() : $"{start}–{end}";
		var heading = Text("LevelWindow", turn, next, range);
		if(turn < start)
			return heading + " · " + Text("InTurns", start - turn);
		if(cost.HasValue && gold < cost.Value)
			return heading + " · " + Text("NeedGold", cost.Value - gold);
		if(life > 0 && life <= 15 && next >= 4)
			return heading + " · " + Text("Stabilize");
		return heading + " · " + Text(turn > end ? "Late" : "Consider");
	}

	internal static string? Gift(int turn, int tier, int gold, int cost, int? charges, bool locked, int? levelCost, int life)
	{
		if(turn < 3 || locked || charges == 0)
			return null;
		if(gold < cost)
			return Text("GiftGold", cost - gold);
		if(life > 0 && life <= 15)
			return Text("GiftTempo");
		var window = Window(tier + 1);
		if(window.Start > 0 && turn >= window.Start && turn <= window.End && levelCost <= gold && gold - cost < levelCost)
			return Text("GiftAfterLevel");
		return Text(turn >= 7 ? "GiftTribe" : "GiftEarly");
	}
}
