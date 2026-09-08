using System;

namespace BuildingRotation.Core
{
    // A single-cell doorway layout, not a game warp, interaction hitbox or animal door.
    public sealed class Doorway
    {
        public Facing Direction { get; }
        public TilePoint LocalDoor { get; }
        public TilePoint LocalApproach => LocalDoor + Direction.OutwardStep();

        // The rectangle and door must both already use the desired orientation.
        public Doorway(Footprint orientedFootprint, Facing facing, TilePoint localDoor)
        {
            if (orientedFootprint is null)
                throw new ArgumentNullException(nameof(orientedFootprint));
            FacingExtensions.Validate(facing);
            if (!orientedFootprint.Contains(localDoor))
                throw new ArgumentOutOfRangeException(nameof(localDoor));

            bool onEntranceEdge = facing switch
            {
                Facing.South => localDoor.Y == orientedFootprint.Height - 1,
                Facing.East => localDoor.X == orientedFootprint.Width - 1,
                Facing.North => localDoor.Y == 0,
                Facing.West => localDoor.X == 0,
                _ => false
            };
            if (!onEntranceEdge)
                throw new ArgumentException("Door must lie on the entrance-facing edge.", nameof(localDoor));

            Direction = facing;
            LocalDoor = localDoor;
        }

        public TilePoint WorldDoor(TilePoint footprintOrigin) => footprintOrigin + LocalDoor;
        public TilePoint WorldApproach(TilePoint footprintOrigin) => footprintOrigin + LocalApproach;
    }
}
