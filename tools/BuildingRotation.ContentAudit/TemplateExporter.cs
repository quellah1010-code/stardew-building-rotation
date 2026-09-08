using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingRotation.Core;
using BuildingRotation.Data;

namespace BuildingRotation.ContentAudit;

public static class TemplateExporter
{
    public static ArtTemplate[] CreateTemplates(ContentEntry entry)
    {
        const int tilePixels = 16; // Source-art convention for this uploaded Content; not world pixels.
        var data = entry.Data;
        var source = data.Sprite.ResolveSourceRect(entry.Texture.Width, entry.Texture.Height);
        if (!data.Sprite.DrawOffset.Equals(new PixelPoint(0, 0)) || source.X != 0 || source.Y != 0 ||
            source.Width != data.Footprint.Width * tilePixels || source.Height < data.Footprint.Height * tilePixels)
            throw new NotSupportedException("Automatic art templates currently require a zero-offset, footprint-wide source sprite.");
        int topMargin = checked((int)source.Height - data.Footprint.Height * tilePixels);
        var definition = data.ToLayoutDefinition();
        return Enum.GetValues<Facing>().Select(facing => new ArtTemplate(
            definition.CreateLayout(new PlacementPose(new TilePoint(0, 0), facing)), tilePixels, topMargin)).ToArray();
    }

    public static void Export(ContentCatalog catalog, string directory)
    {
        string[] ids = ["Barn", "Shed", "Stable"];
        // Prepare everything before writing, so unsupported input doesn't leave half a template set.
        var entries = ids.Select(id => catalog.Buildings[id]).ToArray();
        var sets = entries.Select(CreateTemplates).ToArray();
        var svgs = entries.Select((entry, i) => RenderSvg(entry.Data, sets[i])).ToArray();
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, sourceBuildingsJsonSha256 = catalog.BuildingsJsonSha256,
            status = "ground-plane-guides-only; no production raster or verified game draw transform",
            canvasPolicy = "16 source pixels per tile; fixed upper margin inferred from the south source crop; redraw each elevation",
            layers = new[] { "ground-guide", "shadow", "base-outline", "upper-occluder", "human-door-and-sign", "animal-door-moving-parts" },
            revealPolicy = "north only; upper-occluder can fade; door, sign and base stay legible; parameters not selected",
            buildings = entries.Select((entry, i) => new
            {
                id = entry.Data.Id, textureAsset = entry.Data.Sprite.Texture, png = entry.Texture,
                sourceRegion = entry.Data.Sprite.ResolveSourceRect(entry.Texture.Width, entry.Texture.Height),
                rawDrawOffset = entry.Data.Sprite.DrawOffset, rawSortTileOffset = entry.Data.Sprite.SortTileOffset,
                sourceFrameLayers = entry.Data.Sprite.Layers, orientations = sets[i]
            })
        }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });
        Directory.CreateDirectory(directory);
        for (int i = 0; i < ids.Length; i++)
            File.WriteAllText(Path.Combine(directory, ids[i].ToLowerInvariant() + "-guides.svg"), svgs[i], new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "rotation-template-anchors.json"), json + "\n", new UTF8Encoding(false));
    }

    public static string RenderSvg(BuildingDataSnapshot data, IReadOnlyList<ArtTemplate> templates)
    {
        int panelWidth = 200;
        int height = templates.Max(t => t.CanvasHeight) + 140;
        var b = new StringBuilder();
        b.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1280\" height=\"{height * 1.6:0}\" viewBox=\"0 0 800 {height}\" role=\"img\" aria-labelledby=\"title desc\">");
        b.AppendLine($"<title id=\"title\">{Escape(data.Id)} four-direction ground and doorway guides</title>");
        b.AppendLine("<desc id=\"desc\">Construction guides, not building artwork. Gray tiles block. Cyan is a human door; orange is an animal door. Door overlays do not carve collision. Dashed rectangles are one-tile approaches.</desc>");
        b.AppendLine($"<rect width=\"800\" height=\"{height}\" fill=\"#f8fafc\"/><g font-family=\"sans-serif\" fill=\"#0f172a\">");
        b.AppendLine($"<text x=\"24\" y=\"23\" font-size=\"15\" font-weight=\"bold\">{Escape(data.Id)} / art alignment guides / 16 source px per tile</text>");
        for (int i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            var layout = data.ToLayoutDefinition().CreateLayout(new PlacementPose(new TilePoint(0, 0), t.Facing));
            b.AppendLine($"<g transform=\"translate({i * panelWidth + 32} 64)\">");
            b.AppendLine($"<text x=\"0\" y=\"-15\" font-size=\"12\">{t.Facing} / {t.CanvasWidth} x {t.CanvasHeight}</text>");
            Rect(b, new PixelRectangle(0, 0, t.CanvasWidth, t.CanvasHeight), "fill=\"#edf2f7\" stroke=\"#94a3b8\" stroke-dasharray=\"3 3\"");
            for (int y = 0; y < layout.Footprint.Height; y++)
            for (int x = 0; x < layout.Footprint.Width; x++)
                Rect(b, new PixelRectangle(x * t.TilePixels, y * t.TilePixels + t.GroundOrigin.Y, t.TilePixels, t.TilePixels),
                    $"fill=\"{(layout.BlocksLocal(new TilePoint(x, y)) ? "#cbd5e1" : "#ffffff")}\" stroke=\"#64748b\" stroke-width=\"0.5\"");
            foreach (var door in t.Doors)
            {
                string color = door.Key == "human" ? "#0891b2" : "#ea580c";
                Rect(b, door.Value.Area, $"fill=\"{color}\" fill-opacity=\"0.25\" stroke=\"{color}\" stroke-width=\"1.5\"");
                Rect(b, door.Value.Approach, $"fill=\"none\" stroke=\"{color}\" stroke-dasharray=\"3 2\"");
                b.AppendLine($"<circle cx=\"{F(door.Value.Threshold.X)}\" cy=\"{F(door.Value.Threshold.Y)}\" r=\"2\" fill=\"{color}\"/>");
            }
            b.AppendLine($"<circle cx=\"0\" cy=\"{F(t.GroundOrigin.Y)}\" r=\"2\" fill=\"#111827\"/>");
            b.AppendLine("</g>");
        }
        b.AppendLine($"<text x=\"24\" y=\"{height - 38}\" font-size=\"11\">Gray: blocked / White: open / Cyan: human door / Orange: animal door / Dot: threshold</text>");
        b.AppendLine($"<text x=\"24\" y=\"{height - 18}\" font-size=\"11\">Ground-plane guides only. Redraw the building elevations; no raster rotation. North reveal must use the real door.</text>");
        b.AppendLine("</g></svg>");
        return b.ToString();
    }

    private static string Escape(string s) => SecurityElement.Escape(s) ?? "";
    private static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    private static void Rect(StringBuilder b, PixelRectangle r, string attributes) =>
        b.AppendLine($"<rect x=\"{F(r.X)}\" y=\"{F(r.Y)}\" width=\"{F(r.Width)}\" height=\"{F(r.Height)}\" {attributes}/>");
}
