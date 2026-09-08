using System;

namespace BuildingRotation.Core
{
    // Nonempty, immutable, tile-aligned area. Right and Bottom are exclusive.
    public sealed class TileRectangle : IEquatable<TileRectangle>
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int Right { get; }
        public int Bottom { get; }

        public TileRectangle(int x, int y, int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            Right = checked(x + width);
            Bottom = checked(y + height);
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Contains(TilePoint point) =>
            point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

        public bool Contains(TileRectangle other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));
            return other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
        }

        public bool Intersects(TileRectangle other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));
            return X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
        }

        public TileRectangle Translate(TilePoint offset) =>
            new TileRectangle(checked(X + offset.X), checked(Y + offset.Y), Width, Height);

        public PixelRectangle ToPixels(int pixelsPerTile)
        {
            if (pixelsPerTile <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerTile));
            return new PixelRectangle((double)X * pixelsPerTile, (double)Y * pixelsPerTile,
                (double)Width * pixelsPerTile, (double)Height * pixelsPerTile);
        }

        public bool Equals(TileRectangle? other) => other != null &&
            X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object? obj) => obj is TileRectangle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"({X}, {Y}, {Width}, {Height}) tiles";
    }
}
