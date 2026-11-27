using System.Collections.Generic;
using System.Linq;
using static Hearthstone_Deck_Tracker.Utility.Battlegrounds.BattlegroundsLastGames;

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Session;

public sealed class SessionPlacements
{
	public int Games { get; }
	public int Firsts { get; }
	public int TopFinishes { get; }
	public double Mean { get; }
	public string TopRange { get; }

	private SessionPlacements(int[] placements, int cutoff)
	{
		Games = placements.Length;
		Firsts = placements.Count(place => place == 1);
		TopFinishes = placements.Count(place => place <= cutoff);
		Mean = placements.Average();
		TopRange = $"1–{cutoff}";
	}

	internal static SessionPlacements? FromGames(IEnumerable<GameItem> games, bool duos)
	{
		var places = duos ? 4 : 8;
		var placements = games.Where(g => g.Duos == duos && !g.FriendlyGame && g.Placement >= 1 && g.Placement <= places)
			.Select(g => g.Placement).ToArray();
		return placements.Length == 0 ? null : new SessionPlacements(placements, places / 2);
	}
}
