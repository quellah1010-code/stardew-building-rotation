using BuildingRotation.Core;
using BuildingRotation.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;

namespace BuildingRotation.Smapi;

internal sealed class RotationController
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly LetsMoveItProbe mover;
    private readonly RotationEditor editor;
    private MoveModeContext? context;
    private object? heldTarget;
    private TilePoint sourceGrab;
    private bool previewValid;
    private string lastError = "";
    public GameRotationHost Host { get; }
    public Building? ActiveBuilding { get; private set; }
    public object? ActiveTarget => heldTarget;
    public BuildingLayout? Preview => editor.Session?.State == PlacementState.Editing ? editor.Session.Preview : null;

    public RotationController(IModHelper helper, IMonitor monitor, LetsMoveItProbe mover, GameRotationHost host)
    {
        this.helper = helper; this.monitor = monitor; this.mover = mover; Host = host;
        editor = new RotationEditor(host, 350, 24, 1);
    }

    public void SelectionChanged()
    {
        if (heldTarget != null && !ReferenceEquals(heldTarget, mover.SelectedTarget)) Cancel(false);
        if (ActiveBuilding != null || mover.SelectedBuilding is not Building b || !mover.IsSingleMove || !mover.UsesLeftMouse) return;
        if (!Context.IsWorldReady || !Context.IsPlayerFree || mover.TargetLocation != Game1.currentLocation || !GameRotationHost.IsEmpty(b)) return;
        try
        {
            string id = Host.Identify(b);
            BuildingSnapshot snapshot = Host.Read(id);
            if (!snapshot.SupportsPrototype) return;
            Vector2 grab = mover.GrabOffset;
            sourceGrab = snapshot.Definition.Collision.Footprint.InverseTransformCell(new TilePoint((int)grab.X, (int)grab.Y), snapshot.Pose.Direction);
            heldTarget = mover.SelectedTarget;
            ActiveBuilding = b;
            context = new MoveModeContext("Exblosis.LetsMoveIt/0.6.20", Guid.NewGuid().ToString("N"), id, true, true, true);
            Sample(); // The held pickup press is consumed by the existing gesture gate.
            monitor.Log("Rotation preview active: hold left mouse 350ms and drag sideways, release, then click to place. Cancel with the mover's cancel key.", LogLevel.Info);
        }
        catch (Exception ex) { Cancel(false); Warn(ex.Message); }
    }

    // A Harmony prefix calls this BEFORE Let's Move It's press-to-place method.
    public bool AllowOriginalPlacement()
    {
        SelectionChanged();
        if (ActiveBuilding != null)
        {
            helper.Input.Suppress(SButton.MouseLeft);
            Sample();
            return false;
        }
        if (mover.SelectedBuilding is Building b && b.modData.ContainsKey(FacingData.Key))
        {
            Warn("This rotated Barn needs single selection, MouseLeft, no copy mode, and an empty room to move.");
            helper.Input.Suppress(SButton.MouseLeft);
            return false;
        }
        return true;
    }

    public void Tick()
    {
        if (ActiveBuilding == null) return;
        if (!Context.IsWorldReady || !Context.IsPlayerFree || !Game1.game1.IsActive || !mover.IsSingleMove
            || !mover.UsesLeftMouse || !ReferenceEquals(heldTarget, mover.SelectedTarget)
            || !ReferenceEquals(ActiveBuilding, mover.SelectedBuilding) || mover.TargetLocation != Game1.currentLocation)
        { Cancel(true); return; }
        try { Sample(); }
        catch (Exception ex) { Cancel(true); Warn($"Rotation edit cancelled: {ex.Message}"); }
    }

    private void Sample()
    {
        if (ActiveBuilding == null || context == null) return;
        MouseState mouse = Mouse.GetState();
        Facing facing = Preview?.Pose.Direction ?? Host.Read(context.HeldBuildingId!).Pose.Direction;
        Footprint source = Host.Definition(ActiveBuilding).Collision.Footprint;
        TilePoint cursor = new((int)Game1.currentCursorTile.X, (int)Game1.currentCursorTile.Y);
        TilePoint origin = cursor - source.TransformCell(sourceGrab, facing);
        MoveGestureResult input = editor.Sample(context, origin, mouse.LeftButton == ButtonState.Pressed,
            mouse.X, mouse.Y, Environment.TickCount64, 64);
        if (input.IsHandled) helper.Input.Suppress(SButton.MouseLeft);
        if (editor.Session?.State == PlacementState.Placed)
        {
            monitor.Log($"Rotation committed: {ActiveBuilding.buildingType.Value} / {editor.Session.Preview.Pose.Direction}.", LogLevel.Info);
            Game1.playSound("axchop");
            Cancel(true);
            return;
        }
        if (editor.Session?.State == PlacementState.Editing)
        {
            Facing next = editor.Session.Preview.Pose.Direction;
            if (next != facing) editor.Session.MoveTo(cursor - source.TransformCell(sourceGrab, next));
            previewValid = editor.Session.Validate(64);
            if (input.Intent == GestureIntent.Place && !previewValid) Warn(editor.Session.LastRejection);
        }
        else if (editor.Session == null)
        {
            Cancel(true);
            Warn("The building is no longer eligible; the rotation preview was cancelled.");
        }
    }

    public void Cancel(bool clearMover)
    {
        bool hadTarget = heldTarget != null;
        ActiveBuilding = null; heldTarget = null; context = null;
        editor.Reset(); previewValid = false; lastError = "";
        if (clearMover && hadTarget) mover.ClearSelection();
    }

    private void Warn(string message)
    {
        if (message == lastError) return;
        lastError = message;
        monitor.Log(message, LogLevel.Warn);
        if (Context.IsWorldReady) Game1.showRedMessage(message);
    }

    public void DrawPreview(SpriteBatch batch)
    {
        if (Preview is not BuildingLayout layout) return;
        DrawLayout(batch, layout, previewValid ? Color.LimeGreen : Color.OrangeRed, true);
    }

    public static void DrawLayout(SpriteBatch batch, BuildingLayout layout, Color color, bool preview)
    {
        Vector2 top = Game1.GlobalToLocal(new Vector2(layout.Pose.Origin.X * 64, layout.Pose.Origin.Y * 64));
        var box = new Rectangle((int)top.X, (int)top.Y, layout.Footprint.Width * 64, layout.Footprint.Height * 64);
        batch.Draw(Game1.staminaRect, box, color * (preview ? .25f : .65f));
        for (int y = 0; y < layout.Footprint.Height; y++)
            for (int x = 0; x < layout.Footprint.Width; x++)
                if (layout.BlocksLocal(new TilePoint(x, y)))
                    Outline(batch, new Rectangle(box.X + x * 64, box.Y + y * 64, 64, 64), color * .8f, 2);
        Outline(batch, box, color, 4);
        var door = layout.LocalDoors["human"];
        Rectangle DoorBox(TileRectangle r) => new(box.X + r.X * 64, box.Y + r.Y * 64, r.Width * 64, r.Height * 64);
        batch.Draw(Game1.staminaRect, DoorBox(door.Area), Color.Cyan * .9f);
        Outline(batch, DoorBox(door.ApproachArea(1)), Color.Cyan, 3);
        batch.DrawString(Game1.smallFont, $"BARN / {layout.Pose.Direction.ToString().ToUpperInvariant()}", new Vector2(box.X, box.Y - 32), Color.White);
    }

    private static void Outline(SpriteBatch batch, Rectangle box, Color color, int width)
    {
        batch.Draw(Game1.staminaRect, new Rectangle(box.X, box.Y, box.Width, width), color);
        batch.Draw(Game1.staminaRect, new Rectangle(box.X, box.Bottom - width, box.Width, width), color);
        batch.Draw(Game1.staminaRect, new Rectangle(box.X, box.Y, width, box.Height), color);
        batch.Draw(Game1.staminaRect, new Rectangle(box.Right - width, box.Y, width, box.Height), color);
    }
}
