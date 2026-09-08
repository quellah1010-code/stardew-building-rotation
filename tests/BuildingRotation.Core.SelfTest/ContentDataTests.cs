using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using BuildingRotation.Core;
using BuildingRotation.Data;
using BuildingRotation.ContentAudit;
using static SelfTestAssert;

internal static class ContentDataTests
{
    internal static byte[] FactBytes()
    {
        using var resource = typeof(ContentDataTests).Assembly.GetManifestResourceStream("BuildingRotation.ContentFacts.json")
            ?? throw new InvalidOperationException("The facts fixture was not embedded.");
        using var output = new MemoryStream();
        resource.CopyTo(output);
        return output.ToArray();
    }

    internal static ContentCatalog Facts() => ContentReader.ReadFacts(FactBytes());
    internal static BuildingDataSnapshot Data(string id) => Facts().Buildings[id].Data;

    internal static IEnumerable<(string Name, Action Run)> Cases(string[] args)
    {
        yield return ("facts fixture retains 25 source definitions and original JSON hash", () =>
        {
            var facts = Facts();
            Equal(25, facts.Buildings.Count);
            Equal("d53f2c590d249d8cb8b6da73c3aeff40d697f5a19c2fc12e3479293cbc44fd20", facts.BuildingsJsonSha256);
            Equal("facts-summary", facts.InputKind);
        });
        yield return ("real Barn has solid 7x4 geometry and correctly oriented human and 2-cell animal doors", () =>
        {
            var definition = Data("Barn").ToLayoutDefinition();
            var human = new[] { new TileRectangle(1, 3, 1, 1), new TileRectangle(3, 5, 1, 1), new TileRectangle(5, 0, 1, 1), new TileRectangle(0, 1, 1, 1) };
            var animal = new[] { new TileRectangle(3, 3, 2, 1), new TileRectangle(3, 2, 1, 2), new TileRectangle(2, 0, 2, 1), new TileRectangle(0, 3, 1, 2) };
            foreach (var facing in Enum.GetValues<Facing>())
            {
                var layout = definition.CreateLayout(new PlacementPose(new TilePoint(40, 50), facing));
                Equal(human[(int)facing], layout.LocalDoors["human"].Area);
                Equal(animal[(int)facing], layout.LocalDoors["animal"].Area);
                Equal(facing, layout.LocalDoors["animal"].Direction);
                for (int y = 0; y < layout.Footprint.Height; y++)
                for (int x = 0; x < layout.Footprint.Width; x++)
                    Check(layout.BlocksLocal(new TilePoint(x, y)), "A door must not carve default collision.");
                Equal(28, layout.Footprint.Width * layout.Footprint.Height);
            }
        });
        yield return ("real Shed has 7x3 footprint, one centered human door and no animal door", () =>
        {
            var data = Data("Shed");
            Equal(7, data.Footprint.Width); Equal(3, data.Footprint.Height);
            var east = data.ToLayoutDefinition().CreateLayout(new PlacementPose(default, Facing.East));
            Equal(3, east.Footprint.Width); Equal(7, east.Footprint.Height);
            Equal(1, east.LocalDoors.Count);
            Equal(new TileRectangle(2, 3, 1, 1), east.LocalDoors["human"].Area);
        });
        yield return ("real Stable whitespace collision matches four independent directional row oracles", () =>
        {
            var data = Data("Stable");
            string[][] expected = [["XXXX", "XOOX"], ["XX", "XO", "XO", "XX"], ["XOOX", "XXXX"], ["XX", "OX", "OX", "XX"]];
            var definition = data.ToLayoutDefinition();
            foreach (var facing in Enum.GetValues<Facing>())
            {
                var layout = definition.CreateLayout(new PlacementPose(new TilePoint(-4, 9), facing));
                Equal(0, layout.LocalDoors.Count);
                var rows = expected[(int)facing];
                for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    Equal(rows[y][x] == 'X', layout.BlocksWorld(new TilePoint(x - 4, y + 9)));
            }
        });
        yield return ("raw collision parsing trims indentation and LF CRLF CR without using FromRows", () =>
        {
            foreach (var separator in new[] { "\n", "\r\n", "\r" })
            {
                var raw = separator + "  XXXX  " + separator + "\tXOOX " + separator;
                var map = new ContentCollisionMap(new Footprint(4, 2), raw);
                Equal("XOOX", map.Rows[1]); Equal(6, map.BlockedCells.Count);
                Check(!map.ToCoreMask().IsBlocked(new TilePoint(1, 1)), "Passable cell lost.");
            }
            Throws<ArgumentException>(() => CollisionMask.FromRows("  XXXX", "  XOOX"));
        });
        yield return ("null solid and explicit all-open maps remain distinct", () =>
        {
            var footprint = new Footprint(2, 1);
            Check(new ContentCollisionMap(footprint, null).ToCoreMask().IsBlocked(default), "Default must be solid.");
            Check(!new ContentCollisionMap(footprint, "OO").ToCoreMask().IsBlocked(default), "Explicit open must remain open.");
        });
        yield return ("ambiguous collision text fails explicitly instead of inventing padding", () =>
        {
            foreach (string raw in new[] { "", "  ", "XX\n\nXX", "X?", "xo", "X X" })
                Throws<ArgumentException>(() => new ContentCollisionMap(new Footprint(2, 2), raw));
            foreach (string raw in new[] { "XX\nX", "XX", "OOO\nOO" })
                Throws<NotSupportedException>(() => new ContentCollisionMap(new Footprint(2, 2), raw).ToCoreMask());
        });
        yield return ("real Farmhouse exterior block and both placement flags survive reading but mapping is rejected", () =>
        {
            var data = Data("Farmhouse");
            Equal(9, data.Footprint.Width); Equal(10, data.Collision.Rows[4].Length);
            Equal(new TilePoint(9, 4), data.Collision.BlockedOutsideFootprint.Single());
            Equal(1, data.AdditionalTilePropertyRadius);
            Equal(new TileRectangle(9, 4, 1, 1), data.AdditionalPlacementTiles[0].Area);
            Check(!data.AdditionalPlacementTiles[0].OnlyNeedsToBePassable, "Buildability check lost.");
            Check(data.AdditionalPlacementTiles[1].OnlyNeedsToBePassable, "Passability check lost.");
            Throws<NotSupportedException>(() => data.ToLayoutDefinition());
        });
        yield return ("real Greenhouse additional passability area rotates beyond each footprint edge", () =>
        {
            var definition = Data("Greenhouse").ToLayoutDefinition();
            var expected = new[] { new TileRectangle(2, 6, 3, 2), new TileRectangle(6, 2, 2, 3), new TileRectangle(2, -2, 3, 2), new TileRectangle(-2, 2, 2, 3) };
            foreach (var facing in Enum.GetValues<Facing>())
            {
                var layout = definition.CreateLayout(new PlacementPose(new TilePoint(10, 20), facing));
                Equal(2, layout.AdditionalTilePropertyRadius);
                var requirement = layout.LocalPlacementRequirements.Single();
                Equal(expected[(int)facing], requirement.Area);
                Check(requirement.OnlyNeedsToBePassable, "Flag lost during rotation.");
                Equal(requirement.Area.Translate(layout.Pose.Origin), requirement.Translate(layout.Pose.Origin).Area);
                Check(!layout.BlocksLocal(new TilePoint(requirement.Area.X, requirement.Area.Y)), "Placement area became collision.");
                Equal(0, layout.LocalClearance.Count);
            }
        });
        yield return ("additional placement definitions copy input and isolate instances and check categories", () =>
        {
            var list = new List<PlacementRequirement> { new(new TileRectangle(4, 0, 1, 1), false), new(new TileRectangle(1, 3, 1, 1), true) };
            var definition = new BuildingLayoutDefinition(CollisionMask.Solid(new Footprint(4, 3)), sourcePlacementRequirements: list, additionalTilePropertyRadius: 2);
            list.Clear();
            var south = definition.CreateLayout(new PlacementPose(default, Facing.South));
            var north = definition.CreateLayout(new PlacementPose(default, Facing.North));
            Equal(2, south.LocalPlacementRequirements.Count);
            Equal(new TileRectangle(-1, 2, 1, 1), north.LocalPlacementRequirements[0].Area);
            Equal(new TileRectangle(4, 0, 1, 1), south.LocalPlacementRequirements[0].Area);
            Check(!north.LocalPlacementRequirements[0].OnlyNeedsToBePassable && north.LocalPlacementRequirements[1].OnlyNeedsToBePassable, "Categories collapsed.");
            Throws<NotSupportedException>(() => ((IList<PlacementRequirement>)south.LocalPlacementRequirements).Clear());
            Throws<ArgumentOutOfRangeException>(() => new BuildingLayoutDefinition(definition.Collision, additionalTilePropertyRadius: -1));
        });
        yield return ("base and upgraded Barn retain different animal-door columns and shared source independence", () =>
        {
            var barn = Data("Barn").ToLayoutDefinition();
            var big = Data("Big Barn").ToLayoutDefinition();
            Equal(3, barn.SourceDoors["animal"].Area.X); Equal(4, big.SourceDoors["animal"].Area.X);
            var a = barn.CreateLayout(new PlacementPose(default, Facing.East));
            var b = barn.CreateLayout(new PlacementPose(new TilePoint(9, 9), Facing.South));
            _ = a.At(new PlacementPose(default, Facing.North));
            Equal(Facing.South, b.Pose.Direction); Equal(3, barn.SourceDoors["animal"].Area.X);
        });
        yield return ("reader distinguishes missing disabled doors from malformed partial sentinels", () =>
        {
            var minimal = Encoding.UTF8.GetBytes("{\"test\":{\"Texture\":\"Test\"}}");
            var data = ContentReader.ReadRaw(minimal, _ => new TextureInfo(16, 16)).Buildings["test"].Data;
            Equal(0, data.ToLayoutDefinition().SourceDoors.Count); Equal(1, data.Footprint.Width);
            var bad = new BuildingDataSnapshot("bad", new Footprint(1, 1), null, new TilePoint(-1, 0), new ContentRectangle(-1, -1, 0, 0), data.Sprite);
            Throws<NotSupportedException>(() => bad.ToLayoutDefinition());
            var badAnimal = new BuildingDataSnapshot("bad", new Footprint(1, 1), null, new TilePoint(-1, -1), default, data.Sprite);
            Throws<NotSupportedException>(() => badAnimal.ToLayoutDefinition());
        });
        yield return ("reader rejects duplicate JSON keys and malformed texture dimensions", () =>
        {
            Throws<InvalidDataException>(() => ContentReader.ReadRaw(Encoding.UTF8.GetBytes("{\"x\":{},\"x\":{}}"), _ => new TextureInfo(1, 1)));
            Throws<InvalidDataException>(() => ContentReader.ReadRaw(Encoding.UTF8.GetBytes("{\"x\":{\"Texture\":\"a\",\"Texture\":\"b\"}}"), _ => new TextureInfo(1, 1)));
            Throws<InvalidDataException>(() => ContentReader.ReadRaw(Encoding.UTF8.GetBytes("{\"x\":{\"Texture\":\"a\"}}"), _ => new TextureInfo(0, 1)));
            Throws<InvalidDataException>(() => ContentReader.ReadPngInfo(new byte[33]));
        });
        yield return ("facts reader detects inconsistent exterior-cell summaries and counts", () =>
        {
            var json = JsonNode.Parse(FactBytes())!;
            json["buildings"]!["Farmhouse"]!["Collision"]!["blockedOutsideFootprint"] = new JsonArray();
            Throws<InvalidDataException>(() => ContentReader.ReadFacts(Encoding.UTF8.GetBytes(json.ToJsonString())));
            json = JsonNode.Parse(FactBytes())!;
            json["source"]!["buildingCount"] = 24;
            Throws<InvalidDataException>(() => ContentReader.ReadFacts(Encoding.UTF8.GetBytes(json.ToJsonString())));
        });
        yield return ("raw vector parsing is invariant under comma-decimal culture", () =>
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var json = Encoding.UTF8.GetBytes("{\"x\":{\"Texture\":\"T\",\"DrawOffset\":\"-1.5, 2.25\"}}");
                var data = ContentReader.ReadRaw(json, _ => new TextureInfo(16, 16)).Buildings["x"].Data;
                Equal(new PixelPoint(-1.5, 2.25), data.Sprite.DrawOffset);
            }
            finally { CultureInfo.CurrentCulture = original; }
        });
        yield return ("all 25 facts are readable but Farmhouse is not advertised as a mapped layout", () =>
        {
            var rejected = new List<string>();
            int mapped = 0;
            foreach (var entry in Facts().Buildings.Values)
            {
                try { _ = entry.Data.ToLayoutDefinition(); mapped++; }
                catch (NotSupportedException) { rejected.Add(entry.Data.Id); }
            }
            Equal(24, mapped); Equal("Farmhouse", rejected.Single());
        });
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] != "--content-zip")
                throw new ArgumentException("Optional self-test input: --content-zip <uploaded-archive>.");
            yield return ("uploaded raw Content matches persisted layout and source-frame facts for all 25 definitions", () =>
            {
                var raw = ContentReader.ReadZip(args[1]);
                var facts = Facts();
                Equal(facts.BuildingsJsonSha256, raw.BuildingsJsonSha256); Equal(25, raw.Buildings.Count);
                foreach (var pair in raw.Buildings)
                {
                    var a = pair.Value.Data;
                    var b = facts.Buildings[pair.Key].Data;
                    Equal(b.Footprint.Width, a.Footprint.Width); Equal(b.Footprint.Height, a.Footprint.Height);
                    Equal(b.HumanDoor, a.HumanDoor); Equal(b.AnimalDoor, a.AnimalDoor);
                    Equal(string.Join("/", b.Collision.Rows), string.Join("/", a.Collision.Rows));
                    Equal(b.AdditionalTilePropertyRadius, a.AdditionalTilePropertyRadius);
                    Equal(b.AdditionalPlacementTiles.Count, a.AdditionalPlacementTiles.Count);
                    for (int i = 0; i < a.AdditionalPlacementTiles.Count; i++)
                    {
                        Equal(b.AdditionalPlacementTiles[i].Area, a.AdditionalPlacementTiles[i].Area);
                        Equal(b.AdditionalPlacementTiles[i].OnlyNeedsToBePassable, a.AdditionalPlacementTiles[i].OnlyNeedsToBePassable);
                    }
                    Equal(b.IndoorMap, a.IndoorMap); Equal(b.BuildingToUpgrade, a.BuildingToUpgrade);
                    Equal(b.Sprite.SourceRect, a.Sprite.SourceRect); Equal(b.Sprite.DrawOffset, a.Sprite.DrawOffset);
                    Equal(b.Sprite.SeasonOffset, a.Sprite.SeasonOffset); Equal(b.Sprite.SortTileOffset, a.Sprite.SortTileOffset);
                    Equal(facts.Buildings[pair.Key].Texture, pair.Value.Texture);
                    Equal(b.Sprite.Layers.Count, a.Sprite.Layers.Count);
                    for (int i = 0; i < a.Sprite.Layers.Count; i++)
                    {
                        var actual = a.Sprite.Layers[i]; var expected = b.Sprite.Layers[i];
                        Equal(expected.Id, actual.Id); Equal(expected.SourceRect, actual.SourceRect);
                        Equal(expected.DrawPosition, actual.DrawPosition); Equal(expected.AnimalDoorOffset, actual.AnimalDoorOffset);
                        Equal(expected.DrawInBackground, actual.DrawInBackground); Equal(expected.SortTileOffset, actual.SortTileOffset);
                    }
                    // Exercise the raw-to-core path too, rather than only comparing source fields.
                    if (pair.Key == "Farmhouse") Throws<NotSupportedException>(() => a.ToLayoutDefinition());
                    else
                    {
                        var da = a.ToLayoutDefinition(); var db = b.ToLayoutDefinition();
                        foreach (var facing in Enum.GetValues<Facing>())
                        {
                            var la = da.CreateLayout(new PlacementPose(default, facing));
                            var lb = db.CreateLayout(new PlacementPose(default, facing));
                            for (int y = 0; y < la.Footprint.Height; y++)
                            for (int x = 0; x < la.Footprint.Width; x++)
                                Equal(lb.BlocksLocal(new TilePoint(x, y)), la.BlocksLocal(new TilePoint(x, y)));
                        }
                    }
                }
            });
        }
    }
}
