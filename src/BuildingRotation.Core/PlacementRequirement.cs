using System;

namespace BuildingRotation.Core
{
    // Placement validation is not collision. Preserve the game's two check categories.
    public sealed class PlacementRequirement
    {
        public TileRectangle Area { get; }
        public bool OnlyNeedsToBePassable { get; }

        public PlacementRequirement(TileRectangle area, bool onlyNeedsToBePassable)
        {
            Area = area ?? throw new ArgumentNullException(nameof(area));
            OnlyNeedsToBePassable = onlyNeedsToBePassable;
        }

        public PlacementRequirement RotateTo(Footprint source, Facing facing) =>
            new PlacementRequirement(source.TransformArea(Area, facing), OnlyNeedsToBePassable);

        public PlacementRequirement Translate(TilePoint origin) =>
            new PlacementRequirement(Area.Translate(origin), OnlyNeedsToBePassable);
    }
}
