using System;

namespace BuildingRotation.Core
{
    public readonly struct TilePoint : IEquatable<TilePoint>
    {
        public int X { get; }
        public int Y { get; }

        public TilePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TilePoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is TilePoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
        public static bool operator ==(TilePoint left, TilePoint right) => left.Equals(right);
        public static bool operator !=(TilePoint left, TilePoint right) => !left.Equals(right);
        public static TilePoint operator +(TilePoint left, TilePoint right) =>
            new TilePoint(checked(left.X + right.X), checked(left.Y + right.Y));
    }
}
