using System;

namespace BuildingRotation.Core
{
    // Location/building identity and mouse anchoring belong to the game adapter.
    public readonly struct PlacementPose : IEquatable<PlacementPose>
    {
        public TilePoint Origin { get; }
        public Facing Direction { get; }

        public PlacementPose(TilePoint origin, Facing direction)
        {
            FacingExtensions.Validate(direction);
            Origin = origin;
            Direction = direction;
        }

        public bool Equals(PlacementPose other) => Origin == other.Origin && Direction == other.Direction;
        public override bool Equals(object? obj) => obj is PlacementPose other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Origin, Direction);
        public override string ToString() => $"{Origin}, {Direction}";
    }
}
