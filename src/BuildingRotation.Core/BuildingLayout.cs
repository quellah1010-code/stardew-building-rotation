using System;
using System.Collections.Generic;

namespace BuildingRotation.Core
{
    // One immutable pose snapshot. Building identity, persistence and live-world writes are external.
    public sealed class BuildingLayout
    {
        public BuildingLayoutDefinition Definition { get; }
        public PlacementPose Pose { get; }
        public Footprint Footprint { get; }
        public IReadOnlyDictionary<string, DoorRegion> LocalDoors { get; }
        public IReadOnlyList<TileRectangle> LocalClearance { get; }
        public IReadOnlyList<PlacementRequirement> LocalPlacementRequirements { get; }
        public int AdditionalTilePropertyRadius => Definition.AdditionalTilePropertyRadius;

        internal BuildingLayout(BuildingLayoutDefinition definition, PlacementPose pose)
        {
            Definition = definition;
            Pose = pose;
            Footprint = definition.Collision.Footprint.RotateTo(pose.Direction);
            LocalDoors = definition.OrientDoors(pose.Direction);
            var clearance = new List<TileRectangle>();
            foreach (TileRectangle area in definition.SourceClearance)
                clearance.Add(definition.Collision.Footprint.TransformArea(area, pose.Direction));
            LocalClearance = clearance.AsReadOnly();
            var requirements = new List<PlacementRequirement>();
            foreach (var requirement in definition.SourcePlacementRequirements)
                requirements.Add(requirement.RotateTo(definition.Collision.Footprint, pose.Direction));
            LocalPlacementRequirements = requirements.AsReadOnly();
        }

        public BuildingLayout At(PlacementPose pose) => Definition.CreateLayout(pose);

        // Query the immutable source through an inverse transform, without rebuilding the mask.
        public bool BlocksLocal(TilePoint orientedCell) => Footprint.Contains(orientedCell) &&
            Definition.Collision.IsBlocked(Definition.Collision.Footprint.InverseTransformCell(orientedCell, Pose.Direction));

        // A far-away world cell should return false, not overflow during origin subtraction.
        public bool BlocksWorld(TilePoint worldCell)
        {
            long x = (long)worldCell.X - Pose.Origin.X;
            long y = (long)worldCell.Y - Pose.Origin.Y;
            return x >= 0 && y >= 0 && x < Footprint.Width && y < Footprint.Height &&
                BlocksLocal(new TilePoint((int)x, (int)y));
        }

        // Only this building's mask is considered; the caller still composes world collision rules.
        public bool IntersectsWorld(PixelRectangle bounds, int pixelsPerTile)
        {
            if (bounds is null)
                throw new ArgumentNullException(nameof(bounds));
            if (pixelsPerTile <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerTile));

            double originX = (double)Pose.Origin.X * pixelsPerTile;
            double originY = (double)Pose.Origin.Y * pixelsPerTile;
            // Clip before converting to integer cell indices. Touching edges do not overlap.
            double left = Math.Max(0, (bounds.X - originX) / pixelsPerTile);
            double top = Math.Max(0, (bounds.Y - originY) / pixelsPerTile);
            double right = Math.Min(Footprint.Width, (bounds.Right - originX) / pixelsPerTile);
            double bottom = Math.Min(Footprint.Height, (bounds.Bottom - originY) / pixelsPerTile);
            if (left >= right || top >= bottom)
                return false;
            int endX = (int)Math.Ceiling(right);
            int endY = (int)Math.Ceiling(bottom);
            for (int y = (int)Math.Floor(top); y < endY; y++)
                for (int x = (int)Math.Floor(left); x < endX; x++)
                    if (BlocksLocal(new TilePoint(x, y)))
                        return true;
            return false;
        }
    }
}
