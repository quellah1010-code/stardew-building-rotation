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

            return TransformOffset(source, facing);
        }

        // Use for tile offsets outside the footprint too (e.g. door approaches).
        // Cell coordinates are not continuous pixel anchors or rectangle edges.
        public TilePoint TransformOffset(TilePoint source, Facing facing)
        {
            FacingExtensions.Validate(facing);
            return facing switch
            {
                Facing.South => source,
                Facing.East => new TilePoint(source.Y, checked(Width - 1 - source.X)),
                Facing.North => new TilePoint(checked(Width - 1 - source.X), checked(Height - 1 - source.Y)),
                Facing.West => new TilePoint(checked(Height - 1 - source.Y), source.X),
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }

        public TilePoint InverseTransformCell(TilePoint oriented, Facing facing)
        {
            FacingExtensions.Validate(facing);
            bool side = facing == Facing.East || facing == Facing.West;
            if (oriented.X < 0 || oriented.Y < 0 ||
                oriented.X >= (side ? Height : Width) || oriented.Y >= (side ? Width : Height))
                throw new ArgumentOutOfRangeException(nameof(oriented));
            return InverseTransformOffset(oriented, facing);
        }

        public TilePoint InverseTransformOffset(TilePoint oriented, Facing facing)
        {
            FacingExtensions.Validate(facing);
            return facing switch
            {
                Facing.South => oriented,
                Facing.East => new TilePoint(checked(Width - 1 - oriented.Y), oriented.X),
                Facing.North => new TilePoint(checked(Width - 1 - oriented.X), checked(Height - 1 - oriented.Y)),
                Facing.West => new TilePoint(oriented.Y, checked(Height - 1 - oriented.X)),
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }

        // Rectangles have exclusive right/bottom edges; don't transform their origin as a cell.
        // Areas may extend outside the footprint (or lie entirely outside it).
        public TileRectangle TransformArea(TileRectangle source, Facing facing)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            FacingExtensions.Validate(facing);
            return facing switch
            {
                Facing.South => source,
                Facing.East => new TileRectangle(source.Y, checked(Width - source.Right), source.Height, source.Width),
                Facing.North => new TileRectangle(checked(Width - source.Right), checked(Height - source.Bottom), source.Width, source.Height),
                Facing.West => new TileRectangle(checked(Height - source.Bottom), source.X, source.Height, source.Width),
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }

        public TileRectangle InverseTransformArea(TileRectangle oriented, Facing facing)
        {
            if (oriented is null)
                throw new ArgumentNullException(nameof(oriented));
            FacingExtensions.Validate(facing);
            return RotateTo(facing).TransformArea(oriented, Facing.South.RotateSteps(-(int)facing));
        }

        // Optional tile-level grab policy; the game adapter still chooses its mouse/pixel anchor.
        // The same source cell stays at the same world tile while the bounding origin shifts.
        public PlacementPose ReorientKeepingCell(PlacementPose current, Facing target, TilePoint sourceAnchor)
        {
            TilePoint worldAnchor = current.Origin + TransformCell(sourceAnchor, current.Direction);
            return new PlacementPose(worldAnchor - TransformCell(sourceAnchor, target), target);
        }
    }
}
