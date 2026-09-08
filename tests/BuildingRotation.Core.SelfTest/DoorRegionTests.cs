using BuildingRotation.Core;
using static SelfTestAssert;

internal static class DoorRegionTests
{
    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("multicell doors, approach strips and combined passages rotate together", () =>
        {
            var shape = new Footprint(7, 4);
            var source = new DoorRegion(new TileRectangle(1, 3, 2, 1), Facing.South);
            var doors = new[]
            {
                new TileRectangle(1, 3, 2, 1), new TileRectangle(3, 4, 1, 2),
                new TileRectangle(4, 0, 2, 1), new TileRectangle(0, 1, 1, 2)
            };
            var approaches = new[]
            {
                new TileRectangle(1, 4, 2, 2), new TileRectangle(4, 4, 2, 2),
                new TileRectangle(4, -2, 2, 2), new TileRectangle(-2, 1, 2, 2)
            };
            foreach (Facing facing in Enum.GetValues<Facing>())
            {
                var rotated = source.RotateTo(shape, facing);
                Equal(facing, rotated.Direction);
                Equal(doors[(int)facing], rotated.Area);
                Equal(approaches[(int)facing], rotated.ApproachArea(2));
                Equal(shape.TransformArea(source.PassageArea(2), facing), rotated.PassageArea(2));
                Equal(rotated.Area, rotated.PassageArea(0));
                Check(!rotated.Area.Intersects(rotated.ApproachArea(2)), "Door and approach overlap.");
                Check(rotated.PassageArea(2).Contains(rotated.Area), "Passage omitted the door.");
                Check(rotated.PassageArea(2).Contains(rotated.ApproachArea(2)), "Passage omitted clearance.");
            }
        });

        yield return ("a source side entrance rotates its own outward direction", () =>
        {
            var shape = new Footprint(7, 4);
            var source = new DoorRegion(new TileRectangle(6, 1, 1, 2), Facing.East);
            var rotated = source.RotateTo(shape, Facing.East);
            Equal(Facing.North, rotated.Direction);
            Equal(new TileRectangle(1, 0, 2, 1), rotated.Area);
            Equal(new TileRectangle(1, -1, 2, 1), rotated.ApproachArea(1));
            Equal(shape.TransformArea(source.ApproachArea(1), Facing.East), rotated.ApproachArea(1));
        });

        yield return ("recessed doors do not silently carve holes through collision data", () =>
        {
            var sourceDoor = new DoorRegion(new TileRectangle(1, 1, 1, 1), Facing.South);
            var definition = new BuildingLayoutDefinition(CollisionMask.Solid(new Footprint(4, 4)),
                new Dictionary<string, DoorRegion> { ["human"] = sourceDoor });
            foreach (Facing facing in Enum.GetValues<Facing>())
            {
                var layout = definition.CreateLayout(new PlacementPose(new TilePoint(5, 5), facing));
                var door = layout.LocalDoors["human"];
                Check(!door.TryGetExit(layout.Pose.Origin, 16, new PixelRectangle(2, 3, 8, 8), 1, 0,
                    bounds => !layout.IntersectsWorld(bounds, 16), out var exit), "Exit was placed inside its building's wall.");
                Check(exit is null, "Failed exit returned a position.");
            }
        });

        yield return ("layout inputs and per-facing reconstructed door overrides are copied", () =>
        {
            var sourceHuman = new DoorRegion(new TileRectangle(1, 3, 1, 1), Facing.South);
            var sourceAnimal = new DoorRegion(new TileRectangle(3, 3, 2, 1), Facing.South);
            var reconstructed = new DoorRegion(new TileRectangle(0, 0, 1, 1), Facing.North);
            var doors = new Dictionary<string, DoorRegion> { ["human"] = sourceHuman, ["animal"] = sourceAnimal };
            var north = new Dictionary<string, DoorRegion> { ["human"] = reconstructed };
            var overrides = new Dictionary<Facing, IReadOnlyDictionary<string, DoorRegion>> { [Facing.North] = north };
            var clearance = new[] { new TileRectangle(-1, 4, 2, 1) };
            var definition = new BuildingLayoutDefinition(CollisionMask.Solid(new Footprint(7, 4)), doors, clearance, overrides);
            doors.Clear(); north.Clear(); overrides.Clear(); clearance[0] = new TileRectangle(100, 100, 1, 1);

            var back = definition.CreateLayout(new PlacementPose(new TilePoint(20, 30), Facing.North));
            Equal(reconstructed.Area, back.LocalDoors["human"].Area);
            Equal(Facing.North, back.LocalDoors["human"].Direction);
            Equal(new TileRectangle(2, 0, 2, 1), back.LocalDoors["animal"].Area);
            Equal(new TileRectangle(20, 29, 1, 1), back.LocalDoors["human"].WorldApproach(back.Pose.Origin, 1));
            var side = back.At(new PlacementPose(back.Pose.Origin, Facing.East));
            Equal(new TileRectangle(3, 5, 1, 1), side.LocalDoors["human"].Area);
            Equal(new TileRectangle(4, 6, 1, 2), side.LocalClearance[0]);
            Equal(sourceHuman.Area, definition.SourceDoors["human"].Area);
            Equal(new TileRectangle(-1, 4, 2, 1), definition.SourceClearance[0]);
            Throws<NotSupportedException>(() => ((IDictionary<string, DoorRegion>)back.LocalDoors).Clear());
            Throws<NotSupportedException>(() => ((IList<TileRectangle>)side.LocalClearance).Clear());
        });

        yield return ("exit bodies keep their dimensions and use actor-local offsets in all four facings", () =>
        {
            var shape = new Footprint(7, 4);
            var source = new DoorRegion(new TileRectangle(1, 3, 2, 1), Facing.South);
            var origin = new TilePoint(10, 20);
            var actor = new PixelRectangle(8, -10, 40, 24);
            var expected = new[]
            {
                new PixelRectangle(364, 770, 40, 24), new PixelRectangle(450, 788, 40, 24),
                new PixelRectangle(460, 614, 40, 24), new PixelRectangle(278, 692, 40, 24)
            };
            foreach (Facing facing in Enum.GetValues<Facing>())
            {
                var door = source.RotateTo(shape, facing);
                int checks = 0;
                Check(door.TryGetExit(origin, 32, actor, 2, 2, bounds =>
                {
                    checks++;
                    Equal(expected[(int)facing], bounds);
                    return true;
                }, out var exit), "Valid exit was rejected.");
                Equal(1, checks);
                Equal(expected[(int)facing], exit!.Bounds);
                Equal(new PixelPoint(exit.Bounds.X - 8, exit.Bounds.Y + 10), exit.Position);
                Equal(facing, exit.OutwardDirection);
                Check(!door.WorldArea(origin).ToPixels(32).Intersects(exit.Bounds), "Actor overlaps door after exiting.");
                Check(door.WorldApproach(origin, 2).ToPixels(32).Contains(exit.Bounds), "Actor extends past its permitted approach.");
            }
        });

        yield return ("insufficient door width or approach depth rejects the exit before world validation", () =>
        {
            var door = new DoorRegion(new TileRectangle(0, 0, 1, 1), Facing.South);
            foreach (var actor in new[] { new PixelRectangle(0, 0, 17, 8), new PixelRectangle(0, 0, 8, 17) })
                Check(!door.TryGetExit(new TilePoint(0, 0), 16, actor, 1, 0,
                    _ => throw new InvalidOperationException("Oversized actor reached world validation."), out _), "Oversized actor was squeezed through.");
            Check(!door.TryGetExit(new TilePoint(0, 0), 16, new PixelRectangle(0, 0, 8, 16), 1, 1,
                _ => throw new InvalidOperationException("Gap exceeding clearance was ignored."), out _), "Gap did not count towards clearance.");
        });

        yield return ("blocked exits pass the full body to validation and produce no stale fallback", () =>
        {
            var door = new DoorRegion(new TileRectangle(1, 1, 1, 1), Facing.South);
            var actor = new PixelRectangle(3, 4, 8, 8);
            var obstacle = new PixelRectangle(20, 32, 1, 1); // Hits a corner, not the body's center.
            Check(!door.TryGetExit(new TilePoint(0, 0), 16, actor, 1, 0,
                bounds => !bounds.Intersects(obstacle), out var exit), "Corner obstruction did not prevent placement.");
            Check(exit is null, "A rejected exit returned a fallback.");
            Check(door.TryGetExit(new TilePoint(0, 0), 16, actor, 1, 0, _ => true, out exit), "Cleared obstacle still blocked exit.");
            Check(exit != null, "Accepted exit has no position.");
            Throws<InvalidOperationException>(() => door.TryGetExit(new TilePoint(0, 0), 16, actor, 1, 0,
                _ => throw new InvalidOperationException("world unavailable"), out exit));
            Check(exit is null, "Validation exception left a stale successful result.");
        });

        yield return ("map boundaries can reject north and west exits without teleporting elsewhere", () =>
        {
            var map = new PixelRectangle(0, 0, 160, 160);
            foreach (Facing facing in new[] { Facing.North, Facing.West })
            {
                var door = new DoorRegion(new TileRectangle(0, 0, 1, 1), facing);
                Check(!door.TryGetExit(new TilePoint(0, 0), 16, new PixelRectangle(0, 0, 8, 8), 1, 0,
                    map.Contains, out var exit), "Map-edge exit was allowed outside the world.");
                Check(exit is null, "Out-of-map exit returned a fallback.");
            }
        });

        yield return ("fractional pixel centers are preserved and not mixed with tile coordinates", () =>
        {
            var door = new DoorRegion(new TileRectangle(0, 0, 1, 1), Facing.South);
            Check(door.TryGetExit(new TilePoint(0, 0), 16, new PixelRectangle(-3, 5, 15, 9), 1, 0,
                _ => true, out var exit), "Odd-sized actor was rejected.");
            Equal(new PixelRectangle(0.5, 16, 15, 9), exit!.Bounds);
            Equal(new PixelPoint(3.5, 11), exit.Position);
        });

        yield return ("door definitions reject missing IDs, invalid variants and nonfinite exit inputs", () =>
        {
            var mask = CollisionMask.Solid(new Footprint(2, 2));
            var door = new DoorRegion(new TileRectangle(0, 1, 1, 1), Facing.South);
            Throws<ArgumentException>(() => new BuildingLayoutDefinition(mask, new Dictionary<string, DoorRegion> { [" "] = door }));
            Throws<ArgumentException>(() => new BuildingLayoutDefinition(mask, orientedDoorOverrides:
                new Dictionary<Facing, IReadOnlyDictionary<string, DoorRegion>> { [Facing.North] = new Dictionary<string, DoorRegion> { ["missing"] = door } }));
            Throws<ArgumentOutOfRangeException>(() => new DoorRegion(door.Area, (Facing)4));
            Throws<ArgumentOutOfRangeException>(() => door.ApproachArea(0));
            Throws<ArgumentOutOfRangeException>(() => door.PassageArea(-1));
            var actor = new PixelRectangle(0, 0, 8, 8);
            foreach (double gap in new[] { -1, double.NaN, double.PositiveInfinity })
                Throws<ArgumentOutOfRangeException>(() => door.TryGetExit(new TilePoint(0, 0), 16, actor, 1, gap, _ => true, out _));
            Throws<ArgumentNullException>(() => door.TryGetExit(new TilePoint(0, 0), 16, actor, 1, 0, null!, out _));
            Throws<ArgumentOutOfRangeException>(() => door.TryGetExit(new TilePoint(0, 0), 0, actor, 1, 0, _ => true, out _));
            Throws<ArgumentOutOfRangeException>(() => new PixelRectangle(double.NaN, 0, 1, 1));
            Throws<ArgumentOutOfRangeException>(() => new PixelRectangle(0, 0, double.PositiveInfinity, 1));
            Throws<ArgumentOutOfRangeException>(() => new PixelRectangle(0, 0, 0, 1));
            Throws<ArgumentOutOfRangeException>(() => new PixelRectangle(double.MaxValue, 0, 1, 1));
        });
    }
}
