using System.Windows.Controls.Primitives;
using Hearthstone_Deck_Tracker.Controls.Tooltips;

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides.Comps;

public partial class LobbyCoreCardTooltip : IPlacementAware
{
	public LobbyCoreCardTooltip() => InitializeComponent();

	public void SetPlacement(PlacementMode placement) => Preview.SetPlacement(placement);
}
