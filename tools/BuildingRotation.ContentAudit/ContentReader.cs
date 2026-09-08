using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingRotation.Core;
using BuildingRotation.Data;

namespace BuildingRotation.ContentAudit;

public sealed record TextureInfo(int Width, int Height, string? Sha256 = null);
public sealed record ContentEntry(BuildingDataSnapshot Data, TextureInfo Texture);
public sealed record ContentCatalog(string InputKind, string BuildingsJsonSha256,
    IReadOnlyDictionary<string, ContentEntry> Buildings);

// Offline .NET 8 reader. The game adapter will pass live typed fields to the .NET Standard data layer.
// This intentionally reads only layout and first-frame art metadata, not the full game schema.
public static class ContentReader
{
    public static ContentCatalog ReadZip(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var matches = archive.Entries.Where(e => e.FullName == "Data/Buildings.json" ||
            e.FullName.EndsWith("/Data/Buildings.json", StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Expected exactly one Data/Buildings.json in the archive.");
        var jsonEntry = matches[0];
        string root = jsonEntry.FullName[..^"Data/Buildings.json".Length];
        using var input = jsonEntry.Open();
        byte[] json = ReadLimited(input, 16 * 1024 * 1024);
        return ReadRaw(json, asset =>
        {
            string assetPath = asset.Replace('\\', '/');
            if (assetPath.StartsWith('/') || assetPath.Split('/').Any(p => p is ".." or "." or "") || assetPath.Contains(':'))
                throw new InvalidDataException("Unsafe texture asset path.");
            var images = archive.Entries.Where(e => e.FullName == root + assetPath + ".png").ToArray();
            if (images.Length != 1)
                throw new InvalidDataException($"Expected one PNG for asset {asset}.");
            using var image = images[0].Open();
            return ReadPngInfo(ReadLimited(image, 32 * 1024 * 1024));
        });
    }

    public static ContentCatalog ReadRaw(byte[] json, Func<string, TextureInfo> getTexture)
    {
        ArgumentNullException.ThrowIfNull(getTexture);
        using var doc = JsonDocument.Parse(json);
        RejectDuplicateProperties(doc.RootElement);
        var entries = new Dictionary<string, ContentEntry>(StringComparer.Ordinal);
        foreach (var pair in doc.RootElement.EnumerateObject())
        {
            JsonElement item = pair.Value;
            string texture = RequiredString(item, "Texture");
            TextureInfo size = getTexture(texture);
            ValidateTextureSize(size);
            entries.Add(pair.Name, new ContentEntry(ReadBuilding(pair.Name, item, item,
                OptionalString(item, "CollisionMap"), texture), size));
        }
        return new ContentCatalog("raw-content", Hash(json), new ReadOnlyDictionary<string, ContentEntry>(entries));
    }

    public static ContentCatalog ReadFacts(byte[] json)
    {
        using var doc = JsonDocument.Parse(json);
        RejectDuplicateProperties(doc.RootElement);
        var root = doc.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported facts schema version.");
        var source = root.GetProperty("source");
        string sha = RequiredString(source, "buildingsJsonSha256");
        if (sha.Length != 64 || sha.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Invalid source hash.");
        var entries = new Dictionary<string, ContentEntry>(StringComparer.Ordinal);
        foreach (var pair in root.GetProperty("buildings").EnumerateObject())
        {
            var item = pair.Value;
            var collision = item.GetProperty("Collision");
            string? raw = RequiredString(collision, "mode") switch
            {
                "solid" => null,
                "explicit" => string.Join("\n", collision.GetProperty("normalizedRows").EnumerateArray()
                    .Select(r => r.GetString() ?? throw new InvalidDataException("Null collision row."))),
                _ => throw new InvalidDataException("Unknown collision mode.")
            };
            var sprite = item.GetProperty("Sprite");
            var png = sprite.GetProperty("pngSize");
            var info = new TextureInfo(png.GetProperty("Width").GetInt32(), png.GetProperty("Height").GetInt32(),
                OptionalString(sprite, "pngSha256"));
            ValidateTextureSize(info);
            var data = ReadBuilding(pair.Name, item, sprite, raw, RequiredString(sprite, "textureAsset"));
            // Check redundant facts, so editing a summary cannot silently hide outside blocking.
            var outside = collision.GetProperty("blockedOutsideFootprint").EnumerateArray().Select(Point).ToArray();
            if (!outside.SequenceEqual(data.Collision.BlockedOutsideFootprint))
                throw new InvalidDataException($"Inconsistent exterior collision facts for {pair.Name}.");
            var lengths = collision.GetProperty("rowLengths");
            if (raw == null ? lengths.ValueKind != JsonValueKind.Null :
                !lengths.EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(data.Collision.Rows.Select(x => x.Length)))
                throw new InvalidDataException($"Inconsistent collision row lengths for {pair.Name}.");
            entries.Add(pair.Name, new ContentEntry(data, info));
        }
        if (entries.Count != source.GetProperty("buildingCount").GetInt32())
            throw new InvalidDataException("Building count does not match the facts header.");
        return new ContentCatalog("facts-summary", sha, new ReadOnlyDictionary<string, ContentEntry>(entries));
    }

    private static BuildingDataSnapshot ReadBuilding(string id, JsonElement item, JsonElement sprite,
        string? collision, string texture)
    {
        var size = HasValue(item, "Size", out var sizeValue) ? Point(sizeValue) : new TilePoint(1, 1);
        var human = HasValue(item, "HumanDoor", out var humanValue) ? Point(humanValue) : new TilePoint(-1, -1);
        var animal = HasValue(item, "AnimalDoor", out var animalValue) ? Rectangle(animalValue) : new ContentRectangle(-1, -1, 0, 0);
        var requirements = new List<PlacementRequirement>();
        if (HasValue(item, "AdditionalPlacementTiles", out var additional))
            foreach (var entry in additional.EnumerateArray())
                requirements.Add(new PlacementRequirement(Rectangle(entry.GetProperty("TileArea")).ToTileRectangle(),
                    OptionalBool(entry, "OnlyNeedsToBePassable")));
        var layers = new List<SpriteLayerSnapshot>();
        if (HasValue(sprite, "DrawLayers", out var layerValues))
            foreach (var layer in layerValues.EnumerateArray())
                layers.Add(new SpriteLayerSnapshot(RequiredString(layer, "Id"), OptionalString(layer, "Texture"),
                    Rectangle(layer.GetProperty("SourceRect")), Vector(layer.GetProperty("DrawPosition")),
                    HasValue(layer, "AnimalDoorOffset", out var doorOffset) ? Vector(doorOffset) : new PixelPoint(0, 0),
                    OptionalBool(layer, "DrawInBackground"), OptionalDouble(layer, "SortTileOffset")));
        var source = HasValue(sprite, "SourceRect", out var sourceValue) ? Rectangle(sourceValue) : default;
        var draw = HasValue(sprite, "DrawOffset", out var drawValue) ? Vector(drawValue) : new PixelPoint(0, 0);
        var season = HasValue(sprite, "SeasonOffset", out var seasonValue) ? Point(seasonValue) : new TilePoint(0, 0);
        var spriteData = new SpriteDataSnapshot(texture, source, draw, season, OptionalDouble(sprite, "SortTileOffset"), layers);
        return new BuildingDataSnapshot(id, new Footprint(size.X, size.Y), collision, human, animal, spriteData,
            requirements, HasValue(item, "AdditionalTilePropertyRadius", out var radius) ? radius.GetInt32() : 0,
            OptionalString(item, "IndoorMap"), OptionalString(item, "BuildingToUpgrade"));
    }

    public static TextureInfo ReadPngInfo(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(signature) ||
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8, 4)) != 13 ||
            Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR")
            throw new InvalidDataException("Missing PNG IHDR header.");
        var result = new TextureInfo(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)), Hash(bytes));
        ValidateTextureSize(result);
        return result; // Metadata only: this does not validate image decoding or CRCs.
    }

    private static byte[] ReadLimited(Stream stream, int limit)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (output.Length + count > limit)
                throw new InvalidDataException("Content entry exceeds the inspection size limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static void ValidateTextureSize(TextureInfo info)
    {
        if (info.Width <= 0 || info.Height <= 0)
            throw new InvalidDataException("Texture dimensions must be positive.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool HasValue(JsonElement obj, string name, out JsonElement value) =>
        obj.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;
    private static string RequiredString(JsonElement obj, string name) =>
        OptionalString(obj, name) ?? throw new InvalidDataException($"Missing {name}.");
    private static string? OptionalString(JsonElement obj, string name) =>
        HasValue(obj, name, out var value) ? value.GetString() : null;
    private static bool OptionalBool(JsonElement obj, string name) =>
        HasValue(obj, name, out var value) && value.GetBoolean();
    private static double OptionalDouble(JsonElement obj, string name) =>
        HasValue(obj, name, out var value) ? value.GetDouble() : 0;
    private static TilePoint Point(JsonElement value) =>
        new(value.GetProperty("X").GetInt32(), value.GetProperty("Y").GetInt32());
    private static ContentRectangle Rectangle(JsonElement value) => new(value.GetProperty("X").GetInt32(),
        value.GetProperty("Y").GetInt32(), value.GetProperty("Width").GetInt32(), value.GetProperty("Height").GetInt32());
    private static PixelPoint Vector(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return new PixelPoint(value.GetProperty("X").GetDouble(), value.GetProperty("Y").GetDouble());
        string[] parts = (value.GetString() ?? "").Split(',');
        if (parts.Length != 2)
            throw new InvalidDataException("Expected an X, Y pixel offset.");
        return new PixelPoint(double.Parse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture),
            double.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Duplicate JSON property: {property.Name}.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                RejectDuplicateProperties(item);
    }
}
