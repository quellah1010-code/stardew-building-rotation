using System;
using BuildingRotation.Core;

namespace BuildingRotation.Runtime
{
    // Read committed state afresh. Never use the moving preview for daily walking or return warps.
    public sealed class RotationQueries
    {
        private readonly IRotationHost host;
        public RotationQueries(IRotationHost host) { this.host = host ?? throw new ArgumentNullException(nameof(host)); }

        public bool Blocks(string buildingId, string exteriorId, TilePoint tile)
        {
            var current = host.Read(buildingId);
            return current.ExteriorId == exteriorId && current.Layout.BlocksWorld(tile);
        }

        // Identifies a real door action; the game adapter still checks normal action rules and
        // selects the original indoor arrival point. This never moves a player or carves a wall.
        public string? EntryInterior(string buildingId, string exteriorId,
            TilePoint actionTile, TilePoint actorTile, Facing actorFacing)
        {
            var current = host.Read(buildingId);
            if (current.ExteriorId != exteriorId || current.BuildingType != "Barn") return null;
            var layout = current.Layout;
            if (!layout.LocalDoors.TryGetValue("human", out DoorRegion door)) return null;
            return door.WorldArea(current.Pose.Origin).Contains(actionTile)
                && door.WorldApproach(current.Pose.Origin, 1).Contains(actorTile)
                && actorFacing == door.Direction.RotateSteps(2) ? current.InteriorId : null;
        }

        public bool TryReturn(string buildingId, string fromInteriorId, int pixelsPerTile,
            PixelRectangle actorLocalBounds, out WarpTarget? target)
        {
            target = null;
            var current = host.Read(buildingId);
            // A newly occupied room cannot be rotated again, but that must not trap its player.
            if (current.InteriorId != fromInteriorId || current.BuildingType != "Barn") return false;
            var layout = current.Layout;
            if (!layout.LocalDoors.TryGetValue("human", out DoorRegion door)) return false;
            // Compute first, then round the actor POSITION (not its foot-box offset) and run
            // all collision checks against exactly the position the adapter will apply.
            if (!door.TryGetExit(current.Pose.Origin, pixelsPerTile, actorLocalBounds,
                1, 0, _ => true, out ExitPlacement? raw)) return false;
            var position = new PixelPoint(Math.Floor(raw!.Position.X), Math.Floor(raw.Position.Y));
            var bounds = new PixelRectangle(position.X + actorLocalBounds.X,
                position.Y + actorLocalBounds.Y, actorLocalBounds.Width, actorLocalBounds.Height);
            if (!door.WorldApproach(current.Pose.Origin, 1).ToPixels(pixelsPerTile).Contains(bounds)
                || layout.IntersectsWorld(bounds, pixelsPerTile) || !host.CanOccupy(current, bounds)) return false;
            target = new WarpTarget(current.ExteriorId, position, door.Direction);
            return true;
        }
    }
}
