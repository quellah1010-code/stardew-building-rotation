using BuildingRotation.Core;

namespace BuildingRotation.Smapi;

// Explicit ground calibration for the selected North Barn art. The reserved
// 7x4 area stays intact; the rear row is a real path, with the wall one row in.
internal static class BarnArtLayout
{
    public static BuildingLayoutDefinition Apply(BuildingLayoutDefinition source)
    {
        if (source.Collision.Footprint.Width != 7 || source.Collision.Footprint.Height != 4)
            return source;
        // Do not replace a content mod's special passability or door arrangement.
        if (!source.SourceDoors.TryGetValue("human", out DoorRegion? human)
            || !human.Area.Equals(new TileRectangle(1, 3, 1, 1)) || human.Direction != Facing.South)
            return source;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 7; x++)
                if (!source.Collision.IsBlocked(new TilePoint(x, y))) return source;

        var doors = new Dictionary<string, DoorRegion>();
        foreach (var pair in source.SourceDoors)
        {
            DoorRegion north = pair.Value.RotateTo(source.Collision.Footprint, Facing.North);
            doors.Add(pair.Key, new DoorRegion(north.Area.Translate(new TilePoint(0, 1)), Facing.North));
        }
        return new BuildingLayoutDefinition(source.Collision, source.SourceDoors, source.SourceClearance,
            new Dictionary<Facing, IReadOnlyDictionary<string, DoorRegion>> { [Facing.North] = doors },
            source.SourcePlacementRequirements, source.AdditionalTilePropertyRadius,
            new Dictionary<Facing, CollisionMask>
            {
                [Facing.North] = CollisionMask.FromRows("OOOOOOO", "XXXXXXX", "XXXXXXX", "XXXXXXX")
            });
    }

}
