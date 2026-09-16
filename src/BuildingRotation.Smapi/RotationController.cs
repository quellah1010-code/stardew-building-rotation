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
    private string lastSelectionReport = "no selection attempt observed since load";
    public string DiagnosticStatus => $"active={ActiveBuilding != null}; last attempt: {lastSelectionReport}";
    public void ResetDiagnostics() => lastSelectionReport = "no selection attempt observed since load";

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
        try
        {
            if (heldTarget != null && !ReferenceEquals(heldTarget, mover.SelectedTarget)) Cancel(false);
            if (ActiveBuilding != null) return;
            if (mover.SelectedBuilding is not Building b)
            {
                lastSelectionReport = "no building target";
                return;
            }
            if (!mover.IsSingleMove)
            { SelectionRejected("single-move settings: " + mover.Read()); return; }
            if (!mover.UsesLeftMouse)
            { SelectionRejected("MoveKey=" + mover.MoveKeyDescription + "; expected a single MouseLeft binding."); return; }
            if (mover.RightMouseConflict is string conflict)
            { SelectionRejected($"MouseRight is already used by Let's Move It {conflict}; release that binding before using right-drag rotation."); return; }
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
            { SelectionRejected($"worldReady={Context.IsWorldReady}; playerFree={Context.IsPlayerFree}"); return; }
            if (mover.TargetLocation != Game1.currentLocation)
            { SelectionRejected($"target location={mover.TargetLocation?.NameOrUniqueName}; current location={Game1.currentLocation?.NameOrUniqueName}"); return; }
            if (!GameRotationHost.IsEmpty(b))
            { SelectionRejected("building eligibility: " + GameRotationHost.DescribeEligibility(b)); return; }

            string id = Host.Identify(b);
            BuildingSnapshot snapshot = Host.Read(id);
            if (!snapshot.SupportsPrototype)
            {
                SelectionRejected($"world eligibility: multiplayer={Context.IsMultiplayer}; mainPlayer={Context.IsMainPlayer}; parentIsFarm={ReferenceEquals(b.GetParentLocation(), Game1.getFarm())}; playerAtParent={ReferenceEquals(Game1.currentLocation, b.GetParentLocation())}; " + GameRotationHost.DescribeEligibility(b));
                return;
            }
            Vector2 grab = mover.GrabOffset;
            sourceGrab = snapshot.Definition.Collision.Footprint.InverseTransformCell(new TilePoint((int)grab.X, (int)grab.Y), snapshot.Pose.Direction);
            heldTarget = mover.SelectedTarget;
            ActiveBuilding = b;
            context = new MoveModeContext("Exblosis.LetsMoveIt/0.6.20", Guid.NewGuid().ToString("N"), id, true, true, true);
            Sample(); // Sample the separate rotation button; never reinterpret the pickup click.
            if (ActiveBuilding == null)
            {
                lastSelectionReport = "session ended during its initial sample; see the preceding warning";
                return;
            }
            lastSelectionReport = $"accepted {b.buildingType.Value}; MoveKey={mover.MoveKeyDescription}; rotate=MouseRight drag";
            monitor.Log("Rotation preview active: hold RIGHT mouse 350ms and drag sideways to turn; LEFT click to place. Right release keeps the preview. Cancel with the mover's cancel key.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Cancel(false);
            SelectionRejected("selection exception: " + ex.Message);
            Warn(ex.Message);
        }
    }

    private void SelectionRejected(string reason)
    {
        lastSelectionReport = "rejected: " + reason;
        monitor.Log("Rotation selection " + lastSelectionReport, LogLevel.Info);
    }

    // A Harmony prefix calls this BEFORE Let's Move It's press-to-place method.
    public bool AllowOriginalPlacement()
    {
        SelectionChanged();
        if (ActiveBuilding != null)
        {
            helper.Input.Suppress(SButton.MouseLeft);
            try
            {
                Sample(); // Update the cursor anchor and any final rotation before committing.
                if (ActiveBuilding != null)
                {
                    if (editor.TryPlace(64))
                    {
                        monitor.Log($"Rotation committed: {ActiveBuilding.buildingType.Value} / {editor.Session!.Preview.Pose.Direction}.", LogLevel.Info);
                        Game1.playSound("axchop");
                        Cancel(true);
                    }
                    else Warn(editor.Session?.LastRejection ?? "The rotation session ended; pick up the Barn again.");
                }
            }
            catch (Exception ex) { Cancel(true); Warn($"Rotation placement failed: {ex.Message}"); }
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

    // Suppress the normal game interaction on the press/release event, not a later tick.
    // Outside an active supported selection, right mouse keeps its usual meaning.
    public void RightMouseChanged()
    {
        if (ActiveBuilding == null) return;
        helper.Input.Suppress(SButton.MouseRight);
        Tick();
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
        bool rightDown = mouse.RightButton == ButtonState.Pressed;
        MoveGestureResult input = editor.Sample(context, origin, rightDown,
            mouse.X, mouse.Y, Environment.TickCount64);
        if (input.IsHandled || rightDown) helper.Input.Suppress(SButton.MouseRight);
        if (editor.Session?.State == PlacementState.Editing)
        {
            Facing next = editor.Session.Preview.Pose.Direction;
            if (next != facing)
            {
                editor.Session.MoveTo(cursor - source.TransformCell(sourceGrab, next));
                monitor.Log($"Rotation preview turned: {next}.", LogLevel.Info);
            }
            previewValid = editor.Session.Validate(64);
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
        Vector2 top = Game1.GlobalToLocal(new Vector2(layout.Pose.Origin.X * 64, layout.Pose.Origin.Y * 64));
        batch.DrawString(Game1.smallFont, "Right drag: turn | Left click: place", new Vector2(top.X, top.Y - 60), Color.White);
    }

    public static void DrawLayout(SpriteBatch batch, BuildingLayout layout, Color color, bool preview)
    {
        // The world batch sorts FrontToBack. Equal depths don't preserve submission order,
        // so the footprint could otherwise be sorted over the cyan door as the batch changes.
        // Keep this diagnostic ground overlay below actors while ordering its own layers.
        const float backgroundDepth = .000001f, gridDepth = .000002f;
        const float doorDepth = .000003f, approachDepth = .000004f, labelDepth = .000005f;
        Vector2 top = Game1.GlobalToLocal(new Vector2(layout.Pose.Origin.X * 64, layout.Pose.Origin.Y * 64));
        var box = new Rectangle((int)top.X, (int)top.Y, layout.Footprint.Width * 64, layout.Footprint.Height * 64);
        Fill(batch, box, color * (preview ? .25f : .65f), backgroundDepth);
        for (int y = 0; y < layout.Footprint.Height; y++)
            for (int x = 0; x < layout.Footprint.Width; x++)
                if (layout.BlocksLocal(new TilePoint(x, y)))
                    Outline(batch, new Rectangle(box.X + x * 64, box.Y + y * 64, 64, 64), color * .8f, 2, gridDepth);
        Outline(batch, box, color, 4, gridDepth);
        var door = layout.LocalDoors["human"];
        Rectangle DoorBox(TileRectangle r) => new(box.X + r.X * 64, box.Y + r.Y * 64, r.Width * 64, r.Height * 64);
        Fill(batch, DoorBox(door.Area), Color.Cyan * .9f, doorDepth);
        Outline(batch, DoorBox(door.ApproachArea(1)), Color.Cyan, 3, approachDepth);
        batch.DrawString(Game1.smallFont, $"BARN / {layout.Pose.Direction.ToString().ToUpperInvariant()}",
            new Vector2(box.X, box.Y - 32), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, labelDepth);
    }

    private static void Fill(SpriteBatch batch, Rectangle box, Color color, float depth)
        => batch.Draw(Game1.staminaRect, box, null, color, 0f, Vector2.Zero, SpriteEffects.None, depth);

    private static void Outline(SpriteBatch batch, Rectangle box, Color color, int width, float depth)
    {
        Fill(batch, new Rectangle(box.X, box.Y, box.Width, width), color, depth);
        Fill(batch, new Rectangle(box.X, box.Bottom - width, box.Width, width), color, depth);
        Fill(batch, new Rectangle(box.X, box.Y, width, box.Height), color, depth);
        Fill(batch, new Rectangle(box.Right - width, box.Y, width, box.Height), color, depth);
    }
}
