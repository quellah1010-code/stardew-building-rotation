using System;
using System.Collections.Generic;
using BuildingRotation.Core;

namespace BuildingRotation.Data
{
    // Source-frame metadata, not a renderer or an animation/condition implementation.
    public sealed class SpriteLayerSnapshot
    {
        public string Id { get; }
        public string? Texture { get; }
        public ContentRectangle SourceRect { get; }
        public PixelPoint DrawPosition { get; }
        public PixelPoint AnimalDoorOffset { get; }
        public bool DrawInBackground { get; }
        public double SortTileOffset { get; }

        public SpriteLayerSnapshot(string id, string? texture, ContentRectangle sourceRect,
            PixelPoint drawPosition, PixelPoint animalDoorOffset, bool drawInBackground, double sortTileOffset)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A layer ID is required.", nameof(id));
            if (double.IsNaN(sortTileOffset) || double.IsInfinity(sortTileOffset))
                throw new ArgumentOutOfRangeException(nameof(sortTileOffset));
            Id = id;
            Texture = texture;
            SourceRect = sourceRect;
            DrawPosition = drawPosition;
            AnimalDoorOffset = animalDoorOffset;
            DrawInBackground = drawInBackground;
            SortTileOffset = sortTileOffset;
        }
    }

    public sealed class SpriteDataSnapshot
    {
        public string Texture { get; }
        public ContentRectangle SourceRect { get; }
        public PixelPoint DrawOffset { get; }
        public TilePoint SeasonOffset { get; }
        public double SortTileOffset { get; }
        public IReadOnlyList<SpriteLayerSnapshot> Layers { get; }

        public SpriteDataSnapshot(string texture, ContentRectangle sourceRect, PixelPoint drawOffset,
            TilePoint seasonOffset, double sortTileOffset, IEnumerable<SpriteLayerSnapshot>? layers = null)
        {
            if (string.IsNullOrWhiteSpace(texture))
                throw new ArgumentException("A texture asset name is required.", nameof(texture));
            if (double.IsNaN(sortTileOffset) || double.IsInfinity(sortTileOffset))
                throw new ArgumentOutOfRangeException(nameof(sortTileOffset));
            Texture = texture;
            SourceRect = sourceRect;
            DrawOffset = drawOffset;
            SeasonOffset = seasonOffset;
            SortTileOffset = sortTileOffset;
            var copy = new List<SpriteLayerSnapshot>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (layers != null)
                foreach (var layer in layers)
                {
                    if (layer is null || !ids.Add(layer.Id))
                        throw new ArgumentException("Layers need unique, non-null IDs.", nameof(layers));
                    copy.Add(layer);
                }
            Layers = copy.AsReadOnly();
        }

        // Source coordinates remain in their atlas; they are never geometrically rotated.
        public PixelRectangle ResolveSourceRect(int textureWidth, int textureHeight, int season = 0)
        {
            if (textureWidth <= 0 || textureHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(textureWidth));
            if (season < 0 || season > 3)
                throw new ArgumentOutOfRangeException(nameof(season));
            ContentRectangle source = SourceRect.IsAllZero
                ? new ContentRectangle(0, 0, textureWidth, textureHeight) : SourceRect;
            var rect = new PixelRectangle(checked(source.X + SeasonOffset.X * season),
                checked(source.Y + SeasonOffset.Y * season), source.Width, source.Height);
            if (rect.X < 0 || rect.Y < 0 || rect.Right > textureWidth || rect.Bottom > textureHeight)
                throw new ArgumentOutOfRangeException(nameof(SourceRect), "Source region is outside the texture.");
            return rect;
        }
    }
}
