using BuildingRotation.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace BuildingRotation.Smapi;

internal sealed class BarnRevealRenderer
{
    private readonly Texture2D? roof, wall, foundation, entrance, farWall;
    public bool Ready => roof != null && wall != null && foundation != null && entrance != null && farWall != null;
    public string Status => Ready ? "North Reveal layers ready" : "North Reveal unavailable; normal art retained";

    public BarnRevealRenderer(IModHelper helper, IMonitor monitor)
    {
        try
        {
            roof = Load("barn-north-roof.png", 113, 107);
            wall = Load("barn-north-wall.png", 113, 107);
            foundation = Load("barn-north-base.png", 113, 107);
            entrance = Load("barn-north-entrance.png", 16, 28);
            farWall = Load("barn-north-far-wall.png", 112, 28);
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
        batch.Draw(foundation!, imageTop, null, tint, 0f, Vector2.Zero, 4f, SpriteEffects.None, depth);

        TileRectangle door = layout.LocalDoors["human"].WorldArea(layout.Pose.Origin);
        Vector2 ground = Game1.GlobalToLocal(new Vector2(layout.Pose.Origin.X * 64, layout.Pose.Origin.Y * 64));
        Vector2 threshold = Game1.GlobalToLocal(new Vector2((door.X + door.Width / 2f) * 64, door.Y * 64));
        // Anchor (8,26) is the sill's centre on the real north edge, not the near wall.
        // The far-wall door sits behind a farmer standing in its approach tile.
        float entranceDepth = Math.Max(.00002f, ((door.Y - 1) * 64f - 1) / 10000f);
        PixelRectangle entranceBounds = NorthRevealState.EntranceBounds(door);
        Vector2 entranceTop = Game1.GlobalToLocal(new Vector2((float)entranceBounds.X, (float)entranceBounds.Y));
        batch.Draw(farWall!, new Vector2(ground.X, entranceTop.Y), null, tint * amount,
            0f, Vector2.Zero, 4f, SpriteEffects.None, Math.Max(.00001f, entranceDepth - .000001f));
        batch.Draw(entrance!, entranceTop, null, Color.White * amount,
            0f, Vector2.Zero, 4f, SpriteEffects.None, entranceDepth);

        // Ground-only cues: outside planks and a low SOLID boundary with a door gap.
        // They are visuals, never objects or additional placement/collision tiles.
        Color timber = new Color(112, 76, 43) * (.8f * amount);
        const float groundDepth = .000006f;
        for (int row = 0; row < 3; row++)
        {
            var plank = new Rectangle((int)threshold.X - 24, (int)threshold.Y - 48 + row * 16, 48, 8);
            Fill(batch, plank, timber, groundDepth);
            Fill(batch, new Rectangle(plank.X, plank.Y, plank.Width, 3), new Color(201, 157, 93) * amount, groundDepth + .000001f);
        }
        int inset = layout.LocalDoors["human"].Area.Y * 64;
        int x = (int)ground.X, y = (int)ground.Y + inset, w = layout.Footprint.Width * 64, h = layout.Footprint.Height * 64 - inset;
        int gapStart = (door.X - layout.Pose.Origin.X) * 64, gapEnd = gapStart + door.Width * 64;
        Color edge = new Color(109, 78, 48) * (.8f * amount);
        if (gapStart > 0) Fill(batch, new Rectangle(x, y, gapStart, 4), edge, groundDepth);
        if (gapEnd < w) Fill(batch, new Rectangle(x + gapEnd, y, w - gapEnd, 4), edge, groundDepth);
        Fill(batch, new Rectangle(x, y, 4, h), edge, groundDepth);
        Fill(batch, new Rectangle(x + w - 4, y, 4, h), edge, groundDepth);
        Fill(batch, new Rectangle(x, y + h - 4, w, 4), edge, groundDepth);
    }

    private static void Fill(SpriteBatch batch, Rectangle rect, Color color, float depth)
        => batch.Draw(Game1.staminaRect, rect, null, color, 0f, Vector2.Zero, SpriteEffects.None, depth);
}
