using System;

namespace BuildingRotation.Core
{
    public sealed class Footprint
    {
        public int Width { get; }
        public int Height { get; }

        public Footprint(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        public bool Contains(TilePoint point) =>
            point.X >= 0 && point.Y >= 0 && point.X < Width && point.Y < Height;

        // This footprint is the unrotated (south-facing) source rectangle.
        public Footprint RotateTo(Facing facing)
        {
            FacingExtensions.Validate(facing);
            return facing == Facing.East || facing == Facing.West
                ? new Footprint(Height, Width)
                : this;
        }

        // Transform a source cell, not an arbitrary pixel anchor or a point outside the footprint.
        public TilePoint TransformCell(TilePoint source, Facing facing)
        {
            FacingExtensions.Validate(facing);
            if (!Contains(source))
                throw new ArgumentOutOfRangeException(nameof(source));

            return facing switch
            {
                Facing.South => source,
                Facing.East => new TilePoint(source.Y, Width - 1 - source.X),
                Facing.North => new TilePoint(Width - 1 - source.X, Height - 1 - source.Y),
                Facing.West => new TilePoint(Height - 1 - source.Y, source.X),
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }
    }
}
