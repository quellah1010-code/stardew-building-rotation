using System.Xml.Linq;
using BuildingRotation.Core;
using BuildingRotation.Data;
using BuildingRotation.ContentAudit;
using static SelfTestAssert;

internal static class ArtTemplateTests
{
    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("Barn body uses 112x112 crop, not its 112x128 atlas including door frames", () =>
        {
            var entry = ContentDataTests.Facts().Buildings["Barn"];
            Equal(128, entry.Texture.Height);
            Equal(new PixelRectangle(0, 0, 112, 112), entry.Data.Sprite.ResolveSourceRect(112, 128));
            Equal(4, entry.Data.Sprite.Layers.Count);
            var bottom = entry.Data.Sprite.Layers[0];
            Equal(new ContentRectangle(0, 112, 32, 16), bottom.SourceRect);
            Equal(new PixelPoint(48, 96), bottom.DrawPosition);
            Equal(new PixelPoint(0, -19), bottom.AnimalDoorOffset);
            Equal(0.02, bottom.SortTileOffset);
            Check(bottom.SourceRect.Y >= entry.Data.Sprite.ResolveSourceRect(112, 128).Bottom, "Door frame was included in the main crop.");
        });
        yield return ("Shed and Stable zero source rectangles resolve to full textures without changing raw data", () =>
        {
            foreach (var id in new[] { "Shed", "Stable" })
            {
                var entry = ContentDataTests.Facts().Buildings[id];
                Check(entry.Data.Sprite.SourceRect.IsAllZero, "Fixture source should use default.");
                Equal(new PixelRectangle(0, 0, entry.Texture.Width, entry.Texture.Height),
                    entry.Data.Sprite.ResolveSourceRect(entry.Texture.Width, entry.Texture.Height));
                Check(entry.Data.Sprite.SourceRect.IsAllZero, "Resolution mutated raw source.");
            }
        });
        yield return ("source atlas default, season shifts, bounds and malformed empty rectangles are explicit", () =>
        {
            var sprite = new SpriteDataSnapshot("T", new ContentRectangle(4, 2, 16, 8), default, new TilePoint(0, 8), 0);
            Equal(new PixelRectangle(4, 26, 16, 8), sprite.ResolveSourceRect(32, 40, 3));
            Throws<ArgumentOutOfRangeException>(() => sprite.ResolveSourceRect(32, 32, 3));
            Throws<ArgumentOutOfRangeException>(() => sprite.ResolveSourceRect(32, 40, 4));
            Throws<ArgumentOutOfRangeException>(() => sprite.ResolveSourceRect(0, 40));
            var malformed = new SpriteDataSnapshot("T", new ContentRectangle(1, 0, 0, 0), default, default, 0);
            Throws<ArgumentOutOfRangeException>(() => malformed.ResolveSourceRect(32, 40));
        });
        yield return ("all three four-view art templates have independently specified canvas dimensions and upper margins", () =>
        {
            var cases = new[] { ("Barn", 112, 112, 64, 160, 48), ("Shed", 112, 128, 48, 192, 80), ("Stable", 64, 96, 32, 128, 64) };
            foreach (var (id, frontWidth, frontHeight, sideWidth, sideHeight, margin) in cases)
            {
                var templates = TemplateExporter.CreateTemplates(ContentDataTests.Facts().Buildings[id]);
                Equal(4, templates.Length);
                foreach (var t in templates)
                {
                    bool side = t.Facing is Facing.East or Facing.West;
                    Equal(side ? sideWidth : frontWidth, t.CanvasWidth);
                    Equal(side ? sideHeight : frontHeight, t.CanvasHeight);
                    Equal(new PixelPoint(0, margin), t.GroundOrigin);
                    Equal((double)t.CanvasHeight, t.GroundFootprint.Bottom);
                }
            }
        });
        yield return ("Barn four-view human thresholds and animal door widths agree with oriented geometry", () =>
        {
            var templates = TemplateExporter.CreateTemplates(ContentDataTests.Facts().Buildings["Barn"]);
            PixelPoint[] human = [new(24, 112), new(64, 136), new(88, 48), new(0, 72)];
            PixelPoint[] animal = [new(64, 112), new(64, 96), new(48, 48), new(0, 112)];
            for (int i = 0; i < templates.Length; i++)
            {
                Equal(human[i], templates[i].Doors["human"].Threshold);
                Equal(animal[i], templates[i].Doors["animal"].Threshold);
                Equal(512d, templates[i].Doors["animal"].Area.Width * templates[i].Doors["animal"].Area.Height);
            }
            Equal(new PixelRectangle(80, 32, 16, 16), templates[2].Doors["human"].Approach);
        });
        yield return ("art door guides honor reconstructed north overrides and do not modify collision", () =>
        {
            var overrides = new Dictionary<Facing, IReadOnlyDictionary<string, DoorRegion>>
            {
                [Facing.North] = new Dictionary<string, DoorRegion> { ["human"] = new(new TileRectangle(2, 0, 1, 1), Facing.North) }
            };
            var layout = ContentDataTests.Data("Barn").ToLayoutDefinition(overrides).CreateLayout(new PlacementPose(new TilePoint(100, 100), Facing.North));
            var template = new ArtTemplate(layout, 16, 48);
            Equal(new PixelPoint(40, 48), template.Doors["human"].Threshold);
            Check(layout.BlocksLocal(new TilePoint(2, 0)), "A guide must not make the tile passable.");
        });
        yield return ("sprite metadata copies input and rejects duplicate layer IDs", () =>
        {
            var layer = new SpriteLayerSnapshot("door", "OtherAtlas", new ContentRectangle(0, 0, 16, 16), default, default, false, 0);
            var list = new List<SpriteLayerSnapshot> { layer };
            var sprite = new SpriteDataSnapshot("T", default, default, default, 0, list);
            list.Clear(); Equal(1, sprite.Layers.Count); Equal("OtherAtlas", sprite.Layers[0].Texture);
            Throws<NotSupportedException>(() => ((IList<SpriteLayerSnapshot>)sprite.Layers).Clear());
            Throws<ArgumentException>(() => new SpriteDataSnapshot("T", default, default, default, 0, new[] { layer, layer }));
        });
        yield return ("template generator rejects unverified nonzero-offset alignment instead of guessing draw units", () =>
        {
            var data = ContentDataTests.Data("Barn");
            var sprite = new SpriteDataSnapshot(data.Sprite.Texture, data.Sprite.SourceRect, new PixelPoint(-16, 2), default, 0);
            var altered = new BuildingDataSnapshot("offset", data.Footprint, null, data.HumanDoor, data.AnimalDoor, sprite);
            Throws<NotSupportedException>(() => TemplateExporter.CreateTemplates(new ContentEntry(altered, new TextureInfo(112, 128))));
            Throws<ArgumentOutOfRangeException>(() => new ArtTemplate(data.ToLayoutDefinition().CreateLayout(new PlacementPose(default, Facing.South)), 0, 1));
        });
        yield return ("generated SVGs are well-formed four-panel guides with no embedded game raster assets", () =>
        {
            foreach (var id in new[] { "Barn", "Shed", "Stable" })
            {
                var entry = ContentDataTests.Facts().Buildings[id];
                string svg = TemplateExporter.RenderSvg(entry.Data, TemplateExporter.CreateTemplates(entry));
                var xml = XDocument.Parse(svg);
                XNamespace ns = "http://www.w3.org/2000/svg";
                Equal(0, xml.Descendants(ns + "image").Count());
                Equal(4, xml.Descendants(ns + "g").Count(g => g.Attribute("transform") != null));
                Check(svg.Contains("Ground-plane guides only."), "Template status is missing.");
            }
        });
    }
}
