using System;
using System.Linq;

namespace Hearthstone_Deck_Tracker.Utility.Battlegrounds;

internal sealed class PlacementRates
{
	public double First { get; }
	public double TopHalf { get; }

	private PlacementRates(double first, double topHalf)
	{
		First = first;
		TopHalf = topHalf;
	}

	// Solo places are 1–8, Duos team places are 1–4. Some responses pad Duos with four zeros.
	internal static PlacementRates? FromDistribution(double[]? weights, bool duos)
	{
		var places = duos ? 4 : 8;
		if(weights == null || (weights.Length != places && !(duos && weights.Length == 8)))
			return null;
		if(weights.Any(x => x < 0 || double.IsNaN(x) || double.IsInfinity(x)) || weights.Skip(places).Any(x => x != 0))
			return null;

		var total = weights.Sum();
		if(total <= 0 || double.IsInfinity(total))
			return null;

		// Compare at the precision shown on the cards so identical labels receive the same treatment.
		double Percent(double weight) => Math.Round(weight / total * 100, 1, MidpointRounding.AwayFromZero);
		return new PlacementRates(Percent(weights[0]), Percent(weights.Take(places / 2).Sum()));
	}
}
