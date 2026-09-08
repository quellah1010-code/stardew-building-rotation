using BuildingRotation.Core;
using static SelfTestAssert;

var tests = new (string Name, Action Run)[]
{
    ("orientation cycling, negative and large steps", () =>
    {
        Equal(Facing.East, Facing.South.RotateSteps(1));
        Equal(Facing.West, Facing.South.RotateSteps(-1));
        foreach (Facing facing in Enum.GetValues<Facing>())
        {
            Equal(facing, facing.RotateSteps(4));
            Equal(facing, facing.RotateSteps(-4));
            Equal(facing, facing.RotateSteps(int.MinValue));
            Equal(facing.RotateSteps(-1), facing.RotateSteps(int.MaxValue));
        }
    }),
    ("rectangular footprint swaps dimensions on side facings", () =>
    {
        var source = new Footprint(7, 4);
        foreach (Facing facing in Enum.GetValues<Facing>())
        {
            var rotated = source.RotateTo(facing);
            bool side = facing == Facing.East || facing == Facing.West;
            Equal(side ? 4 : 7, rotated.Width);
            Equal(side ? 7 : 4, rotated.Height);
        }
    }),
    ("asymmetric source door follows the correct edge", () =>
    {
        var source = new Footprint(7, 4);
        var door = new TilePoint(1, 3);
        Equal(new TilePoint(1, 3), source.TransformCell(door, Facing.South));
        Equal(new TilePoint(3, 5), source.TransformCell(door, Facing.East));
        Equal(new TilePoint(5, 0), source.TransformCell(door, Facing.North));
        Equal(new TilePoint(0, 1), source.TransformCell(door, Facing.West));
    }),
    ("every cell maps in bounds and bijectively, including thin rectangles", () =>
    {
        foreach (var size in new[] { (1, 1), (1, 7), (7, 1), (7, 4), (4, 7), (5, 5) })
        {
            var source = new Footprint(size.Item1, size.Item2);
            foreach (Facing facing in Enum.GetValues<Facing>())
            {
                var rotated = source.RotateTo(facing);
                var seen = new HashSet<TilePoint>();
                for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    var point = source.TransformCell(new TilePoint(x, y), facing);
                    Check(rotated.Contains(point), "Transformed cell outside footprint.");
                    Check(seen.Add(point), "Two source cells mapped to the same tile.");
                }
                Equal(source.Width * source.Height, seen.Count);
            }
        }
    }),
    ("four successive quarter turns restore dimensions and every cell", () =>
    {
        var source = new Footprint(7, 4);
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
        {
            var original = new TilePoint(x, y);
            var point = original;
            var shape = source;
            for (int turn = 0; turn < 4; turn++)
            {
                point = shape.TransformCell(point, Facing.East);
                shape = shape.RotateTo(Facing.East);
            }
            Equal(original, point);
            Equal(source.Width, shape.Width);
            Equal(source.Height, shape.Height);
        }
    }),
    ("door approaches are exactly one tile outside each entrance edge", () =>
    {
        var shape = new Footprint(7, 4);
        var expected = new[]
        {
            (Facing.South, new TilePoint(1, 3), new TilePoint(1, 4)),
            (Facing.East, new TilePoint(3, 5), new TilePoint(4, 5)),
            (Facing.North, new TilePoint(5, 0), new TilePoint(5, -1)),
            (Facing.West, new TilePoint(0, 1), new TilePoint(-1, 1))
        };
        foreach (var item in expected)
        {
            var oriented = shape.RotateTo(item.Item1);
            var doorway = new Doorway(oriented, item.Item1, item.Item2);
            Equal(item.Item3, doorway.LocalApproach);
            Check(!oriented.Contains(doorway.LocalApproach), "Approach inside footprint.");
            var origin = new TilePoint(20, 30);
            Equal(origin + item.Item2, doorway.WorldDoor(origin));
            Equal(origin + item.Item3, doorway.WorldApproach(origin));
        }
    }),
    ("per-orientation door placement need not use a transformed source door", () =>
    {
        var doorway = new Doorway(new Footprint(7, 4), Facing.North, new TilePoint(2, 0));
        Equal(new TilePoint(2, -1), doorway.LocalApproach);
    }),
    ("invalid dimensions and source cells are rejected", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => new Footprint(0, 4));
        Throws<ArgumentOutOfRangeException>(() => new Footprint(7, -1));
        var shape = new Footprint(7, 4);
        foreach (var cell in new[] { new TilePoint(-1, 0), new TilePoint(0, -1), new TilePoint(7, 0), new TilePoint(0, 4) })
        {
            Check(!shape.Contains(cell), "Invalid cell accepted.");
            Throws<ArgumentOutOfRangeException>(() => shape.TransformCell(cell, Facing.South));
        }
    }),
    ("undefined orientations are rejected", () =>
    {
        foreach (Facing invalid in new[] { (Facing)(-1), (Facing)4, (Facing)int.MaxValue })
        {
            var shape = new Footprint(7, 4);
            Throws<ArgumentOutOfRangeException>(() => invalid.RotateSteps(1));
            Throws<ArgumentOutOfRangeException>(() => invalid.OutwardStep());
            Throws<ArgumentOutOfRangeException>(() => shape.RotateTo(invalid));
            Throws<ArgumentOutOfRangeException>(() => shape.TransformCell(new TilePoint(0, 0), invalid));
            Throws<ArgumentOutOfRangeException>(() => new Doorway(shape, invalid, new TilePoint(0, 0)));
        }
    }),
    ("invalid door edges, out-of-bounds doors and missing layouts are rejected", () =>
    {
        var shape = new Footprint(7, 4);
        foreach (Facing facing in Enum.GetValues<Facing>())
            Throws<ArgumentException>(() => new Doorway(shape, facing, new TilePoint(1, 1)));
        Throws<ArgumentOutOfRangeException>(() => new Doorway(shape, Facing.South, new TilePoint(7, 3)));
        Throws<ArgumentNullException>(() => new Doorway(null!, Facing.South, new TilePoint(1, 3)));
    }),
    ("point equality, hashing and coordinate overflow", () =>
    {
        var point = new TilePoint(3, 4);
        Check(point == new TilePoint(3, 4), "Equal points differ.");
        Check(point != new TilePoint(4, 3), "Distinct points compare equal.");
        Equal(point.GetHashCode(), new TilePoint(3, 4).GetHashCode());
        Throws<OverflowException>(() => { _ = new TilePoint(int.MaxValue, 0) + new TilePoint(1, 0); });
    })
};

tests = tests.Concat(EditingTests.Cases()).Concat(MoveModeTests.Cases())
    .Concat(LayoutTests.Cases()).Concat(DoorRegionTests.Cases()).ToArray();
int failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
    }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} checks passed.");
return failed == 0 ? 0 : 1;
