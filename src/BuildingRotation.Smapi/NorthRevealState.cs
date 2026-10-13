using BuildingRotation.Core;

namespace BuildingRotation.Smapi;

// Visual state only. These distances never participate in door actions or collision.
internal sealed class NorthRevealState
{
    public const double TileSize = 64;
    public const double SideReach = 1.5 * TileSize;
    public const double OutwardReach = 3 * TileSize;
    public const double InnerReach = .25 * TileSize;
    public const double ExitMargin = .5 * TileSize;
    public const double FadeInSeconds = .18, FadeOutSeconds = .25;
    private TileRectangle? previousDoor;
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
        IsTriggered = false;
        Amount = 0;
    }

    public void Update(Facing facing, TileRectangle worldDoor, PixelPoint feet, double elapsedSeconds)
    {
        if (facing != Facing.North) { Reset(); return; }
        if (!worldDoor.Equals(previousDoor)) Reset();
        previousDoor = worldDoor;
        double margin = IsTriggered ? ExitMargin : 0;
        double left = worldDoor.X * TileSize - SideReach - margin;
        double right = worldDoor.Right * TileSize + SideReach + margin;
        double top = worldDoor.Y * TileSize - OutwardReach - margin;
        double bottom = worldDoor.Y * TileSize + InnerReach + margin;
        IsTriggered = feet.X >= left && feet.X <= right && feet.Y >= top && feet.Y <= bottom;

        // Cap a long frame so a pause/window switch cannot skip the whole transition.
        double elapsed = Math.Clamp(elapsedSeconds, 0, .1);
        float step = (float)(elapsed / (IsTriggered ? FadeInSeconds : FadeOutSeconds));
        Amount = IsTriggered ? Math.Min(1, Amount + step) : Math.Max(0, Amount - step);
    }
}
