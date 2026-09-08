using System;

namespace BuildingRotation.Core
{
    // World pixel units, before any camera/zoom transform. Fractions are intentional.
    public readonly struct PixelPoint : IEquatable<PixelPoint>
    {
        public double X { get; }
        public double Y { get; }

        public PixelPoint(double x, double y)
        {
            RequireFinite(x, nameof(x));
            RequireFinite(y, nameof(y));
            X = x;
            Y = y;
        }

        internal static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }

        public bool Equals(PixelPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is PixelPoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y}) pixels";
    }
}
