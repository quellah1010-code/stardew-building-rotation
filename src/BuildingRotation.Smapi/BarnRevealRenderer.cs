using BuildingRotation.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace BuildingRotation.Smapi;

internal sealed class BarnRevealRenderer
{
    private readonly Texture2D? roof, wall, foundation, entrance, stoneFloor;
    public bool Ready => roof != null && wall != null && foundation != null && entrance != null && stoneFloor != null;
    public string Status => Ready ? "North Reveal layers ready" : "North Reveal unavailable; normal art retained";

    public BarnRevealRenderer(IModHelper helper, IMonitor monitor)
    {
        try
        {
            roof = Load("barn-north-roof.png", 113, 107);
            wall = Load("barn-north-wall.png", 113, 107);
            foundation = Load("barn-north-base.png", 113, 107);
            entrance = Load("barn-north-entrance.png", 16, 28);
            stoneFloor = Load("barn-reveal-stone-floor.png", 32, 32);
        }
        catch (Exception ex) { monitor.Log($"North Reveal art could not load: {ex.Message}", LogLevel.Warn); }

        Texture2D Load(string file, int width, int height)
        {
            Texture2D texture = helper.ModContent.Load<Texture2D>("assets/" + file);
            if (texture.Width != width || texture.Height != height)
                throw new InvalidOperationException($"Unexpected dimensions for {file}.");
            return texture;
        }
    }

    public void Draw(SpriteBatch batch, BuildingLayout layout, Vector2 imageTop, Color tint, float depth, float amount)
    {
        // The three disjoint masks reconstruct the normal North sprite exactly at amount=0.
        // Character and ground draws are untouched; only the occluding building is faded.
        batch.Draw(roof!, imageTop, null, tint * MathHelper.Lerp(1f, .22f, amount), 0f, Vector2.Zero, 4f, SpriteEffects.None, depth);
        batch.Draw(wall!, imageTop, null, tint * MathHelper.Lerp(1f, .38f, amount), 0f, Vector2.Zero, 4f, SpriteEffects.None, depth);
        // The base fades with the wall; do not leave a hard opaque red band.
        batch.Draw(foundation!, imageTop, null, tint * MathHelper.Lerp(1f, .38f, amount), 0f, Vector2.Zero, 4f, SpriteEffects.None, depth);

        TileRectangle door = layout.LocalDoors["human"].WorldArea(layout.Pose.Origin);
        Vector2 ground = Game1.GlobalToLocal(new Vector2(layout.Pose.Origin.X * 64, layout.Pose.Origin.Y * 64));
        // Anchor (8,26) is the sill's centre on the real north edge, not the near wall.
        // The far-wall door sits behind a farmer standing in its approach tile.
        float entranceDepth = Math.Max(.00002f, ((door.Y - 1) * 64f - 1) / 10000f);
        PixelRectangle entranceBounds = NorthRevealState.EntranceBounds(door);
        Vector2 entranceTop = Game1.GlobalToLocal(new Vector2((float)entranceBounds.X, (float)entranceBounds.Y));
        batch.Draw(entrance!, entranceTop, null, Color.White * amount,
            0f, Vector2.Zero, 4f, SpriteEffects.None, entranceDepth);

        // Flat paving lies on the ground under the building, never at door-top
        // height. Use actual blocked cells, not the entire reserved placement box.
        // It is only a draw layer: no terrain, collision or saved items are added.
        const float groundDepth = .000006f;
        for (int y = 0; y < layout.Footprint.Height; y++)
            for (int x = 0; x < layout.Footprint.Width; x++)
                if (layout.BlocksLocal(new TilePoint(x, y))) DrawStone(x, y, .82f);
        TileRectangle approach = layout.LocalDoors["human"].ApproachArea(1);
        for (int y = approach.Y; y < approach.Bottom; y++)
            for (int x = approach.X; x < approach.Right; x++)
                DrawStone(x, y, .65f);

        void DrawStone(int x, int y, float opacity)
        {
            var source = new Rectangle((x & 1) * 16, (y & 1) * 16, 16, 16);
            batch.Draw(stoneFloor!, ground + new Vector2(x * 64, y * 64), source,
                Color.White * (opacity * amount), 0f, Vector2.Zero, 4f, SpriteEffects.None, groundDepth);
        }
    }
}
