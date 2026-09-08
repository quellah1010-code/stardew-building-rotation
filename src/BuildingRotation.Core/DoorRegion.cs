using System;

namespace BuildingRotation.Core
{
    // Tile-aligned, possibly recessed/multicell entrance. This does not carve the collision mask.
    public sealed class DoorRegion
    {
        public TileRectangle Area { get; }
        public Facing Direction { get; }

        // Both arguments already describe the same orientation; no footprint-edge restriction.
        public DoorRegion(TileRectangle area, Facing direction)
        {
            Area = area ?? throw new ArgumentNullException(nameof(area));
            FacingExtensions.Validate(direction);
            Direction = direction;
        }

        // Transform from the source building layout. Door direction rotates along with its area.
        public DoorRegion RotateTo(Footprint sourceFootprint, Facing facing)
        {
            if (sourceFootprint is null)
                throw new ArgumentNullException(nameof(sourceFootprint));
            return new DoorRegion(sourceFootprint.TransformArea(Area, facing), Direction.RotateSteps((int)facing));
        }

        public TileRectangle ApproachArea(int depth)
        {
            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth));
            return Direction switch
            {
                Facing.South => new TileRectangle(Area.X, Area.Bottom, Area.Width, depth),
                Facing.East => new TileRectangle(Area.Right, Area.Y, depth, Area.Height),
                Facing.North => new TileRectangle(Area.X, checked(Area.Y - depth), Area.Width, depth),
                Facing.West => new TileRectangle(checked(Area.X - depth), Area.Y, depth, Area.Height),
                _ => throw new InvalidOperationException()
            };
        }

        // Door plus its outward extension; keep distinct from the approach-only clearance.
        public TileRectangle PassageArea(int outwardDepth)
        {
            if (outwardDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(outwardDepth));
            return Direction switch
            {
                Facing.South => new TileRectangle(Area.X, Area.Y, Area.Width, checked(Area.Height + outwardDepth)),
                Facing.East => new TileRectangle(Area.X, Area.Y, checked(Area.Width + outwardDepth), Area.Height),
                Facing.North => new TileRectangle(Area.X, checked(Area.Y - outwardDepth), Area.Width, checked(Area.Height + outwardDepth)),
                Facing.West => new TileRectangle(checked(Area.X - outwardDepth), Area.Y, checked(Area.Width + outwardDepth), Area.Height),
                _ => throw new InvalidOperationException()
            };
        }

        public TileRectangle WorldArea(TilePoint buildingOrigin) => Area.Translate(buildingOrigin);
        public TileRectangle WorldApproach(TilePoint buildingOrigin, int depth) => ApproachArea(depth).Translate(buildingOrigin);

        // Places an unchanged actor collision box just outside the door, centered across its width.
        // canOccupy must synchronously check the FULL world bounds (map, this building, objects,
        // actors, etc.). Success is a geometric candidate, not a warp or a path-connectivity proof.
        // The adapter must apply its actual pixel rounding before validation if the game rounds.
        public bool TryGetExit(TilePoint buildingOrigin, int pixelsPerTile,
            PixelRectangle actorLocalBounds, int approachDepth, double gapPixels,
            Func<PixelRectangle, bool> canOccupy, out ExitPlacement? placement)
        {
            placement = null;
            if (actorLocalBounds is null)
                throw new ArgumentNullException(nameof(actorLocalBounds));
            if (canOccupy is null)
                throw new ArgumentNullException(nameof(canOccupy));
            PixelPoint.RequireFinite(gapPixels, nameof(gapPixels));
            if (gapPixels < 0)
                throw new ArgumentOutOfRangeException(nameof(gapPixels));

            PixelRectangle door = WorldArea(buildingOrigin).ToPixels(pixelsPerTile);
            PixelRectangle approach = WorldApproach(buildingOrigin, approachDepth).ToPixels(pixelsPerTile);
            double x = door.X + (door.Width - actorLocalBounds.Width) / 2;
            double y = door.Y + (door.Height - actorLocalBounds.Height) / 2;
            switch (Direction)
            {
                case Facing.South: y = door.Bottom + gapPixels; break;
                case Facing.East: x = door.Right + gapPixels; break;
                case Facing.North: y = door.Y - gapPixels - actorLocalBounds.Height; break;
                case Facing.West: x = door.X - gapPixels - actorLocalBounds.Width; break;
            }
            var bounds = new PixelRectangle(x, y, actorLocalBounds.Width, actorLocalBounds.Height);
            if (!approach.Contains(bounds))
                return false;

            // Position uses the caller-supplied collision-box offset, not sprite centering.
            var position = new PixelPoint(x - actorLocalBounds.X, y - actorLocalBounds.Y);
            if (!canOccupy(bounds))
                return false;
            placement = new ExitPlacement(position, bounds, Direction);
            return true;
        }
    }
}
