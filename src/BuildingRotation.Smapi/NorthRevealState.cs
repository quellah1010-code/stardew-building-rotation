using BuildingRotation.Core;

namespace BuildingRotation.Smapi;

// Visual state only. These distances never participate in door actions or collision.
internal sealed class NorthRevealState
{
    public const double TileSize = 64;
    public const double ProximityReach = 2 * TileSize;
    public const double ExitMargin = .5 * TileSize;
    public const double FadeInSeconds = .18, FadeOutSeconds = .25;
    private TileRectangle? previousDoor;
    private TileRectangle? previousFootprint;
    public bool IsTriggered { get; private set; }
    public float Amount { get; private set; }

    public static PixelRectangle EntranceBounds(TileRectangle worldDoor)
        => new((worldDoor.X + worldDoor.Width / 2d) * TileSize - 32,
            worldDoor.Y * TileSize - 104, 64, 112);

    public bool CanClickEntrance(TileRectangle worldDoor, PixelPoint cursor, TilePoint actorTile, Facing actorFacing)
    {
        if (Amount < .5f || !worldDoor.Equals(previousDoor) || actorFacing != Facing.South) return false;
        // Visible-door hit area is taller, but the required PLAYER position stays
        // exactly the existing one-tile north approach. No distant click-to-warp.
        if (actorTile.Y != worldDoor.Y - 1 || actorTile.X < worldDoor.X || actorTile.X >= worldDoor.Right) return false;
        PixelRectangle bounds = EntranceBounds(worldDoor);
        return cursor.X >= bounds.X && cursor.X < bounds.Right && cursor.Y >= bounds.Y && cursor.Y < bounds.Bottom;
    }

    public void Reset()
    {
        previousDoor = null;
        previousFootprint = null;
        IsTriggered = false;
        Amount = 0;
    }

    public void Update(Facing facing, TileRectangle worldDoor, PixelPoint feet, double elapsedSeconds,
        TileRectangle worldFootprint)
    {
        if (facing != Facing.North) { Reset(); return; }
        if (!worldDoor.Equals(previousDoor) || !worldFootprint.Equals(previousFootprint)) Reset();
        previousDoor = worldDoor;
        previousFootprint = worldFootprint;
        double margin = IsTriggered ? ExitMargin : 0;
        // One two-tile rectangular ring around the WHOLE building. Neither the
        // entrance side nor the player's facing restricts proximity visibility.
        double left = worldFootprint.X * TileSize - ProximityReach - margin;
        double right = worldFootprint.Right * TileSize + ProximityReach + margin;
        double top = worldFootprint.Y * TileSize - ProximityReach - margin;
        double bottom = worldFootprint.Bottom * TileSize + ProximityReach + margin;
        IsTriggered = feet.X >= left && feet.X <= right && feet.Y >= top && feet.Y <= bottom;

        // Cap a long frame so a pause/window switch cannot skip the whole transition.
        double elapsed = Math.Clamp(elapsedSeconds, 0, .1);
        float step = (float)(elapsed / (IsTriggered ? FadeInSeconds : FadeOutSeconds));
        Amount = IsTriggered ? Math.Min(1, Amount + step) : Math.Max(0, Amount - step);
    }
}
