using BuildingRotation.Core;
using static SelfTestAssert;

internal static class LayoutTests
{
    // Deliberately asymmetric fixtures; none are asserted to be real game building data.
    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("asymmetric blocking rows match independently specified four-facing layouts", () =>
        {
            var definition = new BuildingLayoutDefinition(CollisionMask.FromRows("XOOX", "OXOO", "XXOX"));
            var expected = new[]
            {
                new[] { "XOOX", "OXOO", "XXOX" },
                new[] { "XOX", "OOO", "OXX", "XOX" },
                new[] { "XOXX", "OOXO", "XOOX" },
                new[] { "XOX", "XXO", "OOO", "XOX" }
            };
            foreach (Facing facing in Enum.GetValues<Facing>())
            {
                var layout = definition.CreateLayout(new PlacementPose(new TilePoint(10, 20), facing));
                string[] rows = expected[(int)facing];
                Equal(rows[0].Length, layout.Footprint.Width);
                Equal(rows.Length, layout.Footprint.Height);
                for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    bool blocked = rows[y][x] == 'X';
                    Equal(blocked, layout.BlocksLocal(new TilePoint(x, y)));
                    Equal(blocked, layout.BlocksWorld(new TilePoint(10 + x, 20 + y)));
                    Equal(blocked, layout.IntersectsWorld(new PixelRectangle((10 + x) * 16 + 1,
                        (20 + y) * 16 + 1, 14, 14), 16));
                }
            }
        });

        yield return ("inverse transforms cover thin footprints and exterior offsets", () =>
        {
            foreach (var size in new[] { (1, 1), (1, 7), (7, 1), (7, 4) })
            {
                var shape = new Footprint(size.Item1, size.Item2);
                foreach (Facing facing in Enum.GetValues<Facing>())
                for (int y = -2; y < shape.Height + 2; y++)
                for (int x = -2; x < shape.Width + 2; x++)
                {
                    var point = new TilePoint(x, y);
                    var rotated = shape.TransformOffset(point, facing);
                    Equal(point, shape.InverseTransformOffset(rotated, facing));
                    if (shape.Contains(point))
                        Equal(point, shape.InverseTransformCell(rotated, facing));
                    else
                        Throws<ArgumentOutOfRangeException>(() => shape.InverseTransformCell(rotated, facing));
                }
            }
        });

        yield return ("external rectangle edges rotate without a one-cell shift or clipping", () =>
        {
            var shape = new Footprint(7, 4);
            var source = new TileRectangle(-1, 3, 3, 2);
            var expected = new[]
            {
                source, new TileRectangle(3, 5, 2, 3),
                new TileRectangle(5, -1, 3, 2), new TileRectangle(-1, -1, 2, 3)
            };
            foreach (Facing facing in Enum.GetValues<Facing>())
            {
                var rotated = shape.TransformArea(source, facing);
                Equal(expected[(int)facing], rotated);
                Equal(source, shape.InverseTransformArea(rotated, facing));
                var mapped = new HashSet<TilePoint>();
                for (int y = source.Y; y < source.Bottom; y++)
                for (int x = source.X; x < source.Right; x++)
                    mapped.Add(shape.TransformOffset(new TilePoint(x, y), facing));
                Equal(rotated.Width * rotated.Height, mapped.Count);
                Check(mapped.All(rotated.Contains), "Area lost a transformed source tile.");
            }
        });

        yield return ("four incremental turns restore an area spanning beyond the footprint", () =>
        {
            var shape = new Footprint(7, 4);
            var source = new TileRectangle(-2, -1, 3, 6);
            var area = source;
            for (int i = 0; i < 4; i++)
            {
                area = shape.TransformArea(area, Facing.East);
                shape = shape.RotateTo(Facing.East);
            }
            Equal(source, area);
            Equal(7, shape.Width);
            Equal(4, shape.Height);
        });

        yield return ("tile grab anchoring preserves the selected world cell through all turns", () =>
        {
            var source = new Footprint(7, 4);
            var anchor = new TilePoint(1, 2);
            var original = new PlacementPose(new TilePoint(20, 30), Facing.South);
            Equal(new PlacementPose(new TilePoint(19, 27), Facing.East), source.ReorientKeepingCell(original, Facing.East, anchor));
            var pose = original;
            for (int i = 0; i < 4; i++)
            {
                pose = source.ReorientKeepingCell(pose, pose.Direction.RotateSteps(1), anchor);
                Equal(new TilePoint(21, 32), pose.Origin + source.TransformCell(anchor, pose.Direction));
            }
            Equal(original, pose);
        });

        yield return ("source rows and cell collections are copied before callers can mutate them", () =>
        {
            var rows = new[] { "XO", "OX" };
            var parsed = CollisionMask.FromRows(rows);
            rows[0] = "OO";
            Check(parsed.IsBlocked(new TilePoint(0, 0)), "Row array mutation changed the mask.");
            var cells = new List<TilePoint> { new TilePoint(1, 0) };
            var copied = new CollisionMask(new Footprint(2, 2), cells);
            cells.Clear();
            cells.Add(new TilePoint(0, 1));
            Check(copied.IsBlocked(new TilePoint(1, 0)), "Cell list mutation changed the mask.");
            Check(!copied.IsBlocked(new TilePoint(0, 1)), "New caller cells leaked into the mask.");
        });

        yield return ("two building poses share source data without sharing mutable rotation", () =>
        {
            var definition = new BuildingLayoutDefinition(CollisionMask.FromRows("XOOX", "OXOO", "XXOX"));
            var original = definition.CreateLayout(new PlacementPose(new TilePoint(1, 2), Facing.South));
            var neighbour = definition.CreateLayout(new PlacementPose(new TilePoint(20, 30), Facing.North));
            var changed = original;
            for (int i = 0; i < 4; i++)
                changed = changed.At(new PlacementPose(changed.Pose.Origin, changed.Pose.Direction.RotateSteps(1)));
            Equal(original.Pose, changed.Pose);
            Equal(Facing.South, original.Pose.Direction);
            Equal(Facing.North, neighbour.Pose.Direction);
            Check(ReferenceEquals(definition, changed.Definition), "Layout unexpectedly replaced its immutable source.");
            Check(neighbour.BlocksWorld(new TilePoint(20, 30)), "Neighbour's blocked cell changed.");
            Check(!neighbour.BlocksWorld(new TilePoint(21, 30)), "Neighbour's opening changed.");
        });

        yield return ("solid and empty masks distinguish occupancy from this building's obstruction", () =>
        {
            var shape = new Footprint(100000, 3);
            var solid = CollisionMask.Solid(shape);
            var empty = new CollisionMask(shape, Array.Empty<TilePoint>());
            Check(solid.IsBlocked(new TilePoint(99999, 2)), "Solid mask omitted its far corner.");
            Check(!solid.IsBlocked(new TilePoint(100000, 2)), "Solid mask blocked outside itself.");
            Check(!empty.IsBlocked(new TilePoint(99999, 2)), "Empty mask blocked a tile.");
        });

        yield return ("far world queries do not overflow origin subtraction", () =>
        {
            var definition = new BuildingLayoutDefinition(CollisionMask.Solid(new Footprint(2, 3)));
            var left = definition.CreateLayout(new PlacementPose(new TilePoint(int.MinValue, int.MinValue), Facing.East));
            var right = definition.CreateLayout(new PlacementPose(new TilePoint(int.MaxValue, int.MaxValue), Facing.West));
            Check(!left.BlocksWorld(new TilePoint(int.MaxValue, int.MaxValue)), "Far positive tile collided.");
            Check(!right.BlocksWorld(new TilePoint(int.MinValue, int.MinValue)), "Far negative tile collided.");
            Check(left.BlocksWorld(new TilePoint(int.MinValue, int.MinValue)), "Origin failed to collide.");
        });

        yield return ("pixel collision handles negative origins, touching edges and corner overlaps", () =>
        {
            var layout = new BuildingLayoutDefinition(CollisionMask.FromRows("OX", "XO"))
                .CreateLayout(new PlacementPose(new TilePoint(-2, -3), Facing.South));
            Check(!layout.IntersectsWorld(new PixelRectangle(-20, -30, 10, 10), 10), "Touching blocked neighbours collided.");
            Check(layout.IntersectsWorld(new PixelRectangle(-10, -30, 1, 1), 10), "Blocked tile did not collide.");
            Check(layout.IntersectsWorld(new PixelRectangle(-10.25, -29, 0.5, 1), 10), "Fractional overlap missed a wall.");
            Check(!layout.IntersectsWorld(new PixelRectangle(0, -30, 1, 10), 10), "Right outside edge collided.");
            Check(!layout.IntersectsWorld(new PixelRectangle(-20, -10, 10, 1), 10), "Bottom outside edge collided.");
            Check(layout.IntersectsWorld(new PixelRectangle(-100, -100, 200, 200), 10), "Clipped large box missed all walls.");
        });

        yield return ("a clear center does not make a body overlapping surrounding walls passable", () =>
        {
            var layout = new BuildingLayoutDefinition(CollisionMask.FromRows("XXX", "XOX", "XXX"))
                .CreateLayout(new PlacementPose(new TilePoint(0, 0), Facing.South));
            Check(!layout.IntersectsWorld(new PixelRectangle(16, 16, 16, 16), 16), "The exact opening was blocked.");
            Check(layout.IntersectsWorld(new PixelRectangle(15, 16, 18, 16), 16), "Only the center of the actor was checked.");
        });

        yield return ("malformed normalized masks are rejected rather than silently opening holes", () =>
        {
            Throws<ArgumentNullException>(() => CollisionMask.FromRows(null!));
            foreach (string[] rows in new[] { Array.Empty<string>(), new[] { "" }, new[] { "XO", "X" }, new[] { "XO", "X " }, new[] { "xo" }, new[] { "X", null! } })
                Throws<ArgumentException>(() => CollisionMask.FromRows(rows));
            Throws<ArgumentNullException>(() => CollisionMask.Solid(null!));
            Throws<ArgumentNullException>(() => new CollisionMask(new Footprint(2, 2), null!));
            Throws<ArgumentOutOfRangeException>(() => new CollisionMask(new Footprint(2, 2), new[] { new TilePoint(2, 0) }));
        });

        yield return ("new geometry rejects undefined facings and coordinate overflow", () =>
        {
            var shape = new Footprint(7, 4);
            var area = new TileRectangle(0, 0, 1, 1);
            Throws<ArgumentOutOfRangeException>(() => shape.InverseTransformCell(new TilePoint(0, 0), (Facing)4));
            Throws<ArgumentOutOfRangeException>(() => shape.TransformArea(area, (Facing)(-1)));
            Throws<ArgumentOutOfRangeException>(() => shape.InverseTransformArea(area, (Facing)4));
            Throws<ArgumentNullException>(() => shape.TransformArea(null!, Facing.South));
            Throws<ArgumentOutOfRangeException>(() => new TileRectangle(0, 0, 0, 1));
            Throws<OverflowException>(() => new TileRectangle(int.MaxValue, 0, 1, 1));
            Throws<OverflowException>(() => shape.TransformOffset(new TilePoint(int.MinValue, 0), Facing.East));
            Throws<OverflowException>(() => shape.TransformArea(new TileRectangle(int.MinValue, 0, 1, 1), Facing.East));
            Throws<OverflowException>(() => { _ = new TilePoint(int.MinValue, 0) - new TilePoint(1, 0); });
        });
    }
}
