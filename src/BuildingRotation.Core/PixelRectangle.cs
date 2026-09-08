using System;

namespace BuildingRotation.Core
{
    // Axis-aligned collision bounds in world pixels, not a sprite rectangle.
    public sealed class PixelRectangle : IEquatable<PixelRectangle>
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right { get; }
        public double Bottom { get; }

        public PixelRectangle(double x, double y, double width, double height)
        {
            PixelPoint.RequireFinite(x, nameof(x));
            PixelPoint.RequireFinite(y, nameof(y));
            PixelPoint.RequireFinite(width, nameof(width));
            PixelPoint.RequireFinite(height, nameof(height));
            if (width <= 0 || double.IsInfinity(x + width) || x + width <= x)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0 || double.IsInfinity(y + height) || y + height <= y)
                throw new ArgumentOutOfRangeException(nameof(height));
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Right = x + width;
            Bottom = y + height;
        }

        public bool Contains(PixelRectangle other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));
            return other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
        }

        public bool Intersects(PixelRectangle other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));
            return X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
        }

        public bool Equals(PixelRectangle? other) => other != null &&
            X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object? obj) => obj is PixelRectangle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"({X}, {Y}, {Width}, {Height}) pixels";
    }
}
