using System.Reflection;
using BuildingRotation.Core;
using BuildingRotation.Runtime;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace BuildingRotation.Smapi;

internal static class RotationPatches
{
    private static RotationController? controller;
    private static IMonitor? monitor;
    [ThreadStatic] private static Building? ignoredBuilding;
    [ThreadStatic] internal static Farmer? IgnoredFarmer;
    private static readonly HashSet<Building> reported = new();

    private sealed class Scope : IDisposable
    {
        private readonly Building? previous;
        public Scope(Building b) { previous = ignoredBuilding; ignoredBuilding = b; }
        public void Dispose() => ignoredBuilding = previous;
    }
    public static IDisposable Ignore(Building b) => new Scope(b);
    public static void Clear() { reported.Clear(); ignoredBuilding = null; IgnoredFarmer = null; }

    public static void Install(RotationController value, LetsMoveItProbe mover, IMonitor log)
    {
        const string id = "quellah.BuildingRotation";
        var harmony = new Harmony(id);
        controller = value; monitor = log;
        void Patch(Type type, string name, Type[]? args, string? prefix = null, string? postfix = null)
        {
            MethodInfo target = AccessTools.Method(type, name, args) ?? throw new MissingMethodException(type.FullName, name);
            harmony.Patch(target, prefix == null ? null : new HarmonyMethod(typeof(RotationPatches), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(RotationPatches), postfix));
        }
        try
        {
            Patch(typeof(Building), "occupiesTile", new[] { typeof(int), typeof(int), typeof(bool) }, nameof(OccupiesPrefix));
            Patch(typeof(Building), "isTilePassable", new[] { typeof(Vector2) }, nameof(PassablePrefix));
            Patch(typeof(Building), "intersects", new[] { typeof(Rectangle) }, nameof(IntersectsPrefix));
            Patch(typeof(Building), "draw", new[] { typeof(SpriteBatch) }, nameof(DrawPrefix));
            Patch(typeof(Building), "doAction", new[] { typeof(Vector2), typeof(Farmer) }, nameof(ActionPrefix));
            Patch(typeof(Building), "LoadFromBuildingData", null, postfix: nameof(ReapplyPostfix));
            Patch(typeof(Building), "load", Type.EmptyTypes, postfix: nameof(ReapplyPostfix));
            Patch(typeof(Building), "updateInteriorWarps", new[] { typeof(GameLocation) }, postfix: nameof(WarpsPostfix));
            Patch(typeof(Game1), "performWarpFarmer", new[] { typeof(LocationRequest), typeof(int), typeof(int), typeof(int) }, nameof(WarpPrefix));
            Type entry = mover.ModInstance.GetType();
            Patch(entry, "SelectTargetAction", new[] { typeof(ButtonPressedEventArgs) }, postfix: nameof(SelectedPostfix));
            Patch(entry, "SingleTargetAction", new[] { typeof(ButtonPressedEventArgs) }, nameof(PlacePrefix));
            Patch(entry, "ClearSelection", Type.EmptyTypes, postfix: nameof(ClearPostfix));
            Patch(mover.TargetType, "Render", new[] { typeof(SpriteBatch), typeof(GameLocation), typeof(Vector2) }, nameof(TargetRenderPrefix));
        }
        catch { harmony.UnpatchAll(id); controller = null; throw; }
    }

    private static BuildingLayout? Layout(Building b)
    {
        try { return controller?.Host.ManagedLayout(b); }
        catch (Exception ex)
        {
            if (reported.Add(b)) monitor?.Log($"Rotation layout unavailable: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    private static bool OccupiesPrefix(Building __instance, ref bool __result)
    {
        if (__instance != ignoredBuilding) return true;
        __result = false; return false;
    }
    private static bool PassablePrefix(Building __instance, Vector2 tile, ref bool __result)
    {
        if (__instance == ignoredBuilding) { __result = true; return false; }
        BuildingLayout? layout = Layout(__instance);
        if (layout == null) return true;
        if (__instance.isUnderConstruction() && __instance.occupiesTile(tile)) { __result = false; return false; }
        __result = !layout.BlocksWorld(new TilePoint((int)tile.X, (int)tile.Y));
        return false;
    }
    private static bool IntersectsPrefix(Building __instance, Rectangle boundingBox, ref bool __result)
    {
        if (__instance == ignoredBuilding) { __result = false; return false; }
        BuildingLayout? layout = Layout(__instance);
        if (layout == null) return true;
        __result = layout.IntersectsWorld(new PixelRectangle(boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height), 64);
        return false;
    }
    private static bool DrawPrefix(Building __instance, SpriteBatch b)
    {
        BuildingLayout? layout = Layout(__instance);
        if (layout == null || __instance.isMoving || __instance.isUnderConstruction()) return true;
        RotationController.DrawLayout(b, layout, Color.SandyBrown, false);
        return false;
    }
    private static bool TargetRenderPrefix(object __instance) => !ReferenceEquals(controller?.ActiveTarget, __instance);
    private static void SelectedPostfix() => controller?.SelectionChanged();
    private static bool PlacePrefix() => controller?.AllowOriginalPlacement() ?? true;
    private static void ClearPostfix() => controller?.Cancel(false);

    private static void ReapplyPostfix(Building __instance)
    {
        BuildingLayout? layout = Layout(__instance);
        if (layout != null) GameRotationHost.WriteLayout(__instance, layout);
    }
    private static void WarpsPostfix(Building __instance, GameLocation? interior)
    {
        BuildingLayout? layout = Layout(__instance);
        if (layout != null) GameRotationHost.UpdateWarps(__instance, layout, interior);
    }

    private static bool ActionPrefix(Building __instance, Vector2 tileLocation, Farmer who, ref bool __result)
    {
        BuildingLayout? layout = Layout(__instance);
        if (layout == null || !layout.LocalDoors["human"].WorldArea(layout.Pose.Origin).Contains(new TilePoint((int)tileLocation.X, (int)tileLocation.Y))) return true;
        var query = new RotationQueries(controller!.Host);
        string? interior = query.EntryInterior(controller.Host.Identify(__instance), who.currentLocation.NameOrUniqueName,
            new TilePoint((int)tileLocation.X, (int)tileLocation.Y), new TilePoint(who.TilePoint.X, who.TilePoint.Y), GameRotationHost.CoreFacing(who.FacingDirection));
        if (interior != null) return true; // Preserve the original mount, lock, sound and warp rules.
        __result = false; return false;
    }

    private static bool WarpPrefix(LocationRequest locationRequest, ref int tileX, ref int tileY, ref int facingDirectionAfterWarp)
    {
        if (controller == null || !Context.IsWorldReady || Context.IsMultiplayer) return true;
        GameLocation source = Game1.currentLocation;
        foreach (Building b in Game1.getFarm().buildings)
        {
            BuildingLayout? layout = Layout(b);
            if (layout == null) continue;
            GameLocation? interior = b.GetIndoors();
            if (interior == null) continue;
            if (source == b.GetParentLocation() && locationRequest.Name == interior.NameOrUniqueName)
            { facingDirectionAfterWarp = 0; return true; } // The unrotated room is entered facing north.
            if (source != interior || locationRequest.Name != b.GetParentLocation().NameOrUniqueName) continue;
            int requestedX = tileX, requestedY = tileY;
            if (!interior.warps.Any(w => w.TargetName == locationRequest.Name && w.TargetX == requestedX && w.TargetY == requestedY)) continue;
            var actor = Game1.player;
            Rectangle bounds = actor.GetBoundingBox();
            var localBounds = new PixelRectangle(bounds.X - actor.Position.X, bounds.Y - actor.Position.Y, bounds.Width, bounds.Height);
            string id = controller.Host.Identify(b);
            var queries = new RotationQueries(controller.Host);
            if (!queries.TryReturn(id, interior.NameOrUniqueName, 64, localBounds, out WarpTarget? target))
            { Game1.showRedMessage("The outside entrance is blocked."); return false; }
            tileX = (int)Math.Floor(target!.Position.X / 64); tileY = (int)Math.Floor(target.Position.Y / 64);
            facingDirectionAfterWarp = GameRotationHost.GameFacing(target.Direction);
            locationRequest.OnWarp += () =>
            {
                if (Game1.currentLocation != b.GetParentLocation()) return;
                Farmer? previous = IgnoredFarmer;
                IgnoredFarmer = actor;
                try
                {
                    Rectangle now = actor.GetBoundingBox();
                    var offsets = new PixelRectangle(now.X - actor.Position.X, now.Y - actor.Position.Y, now.Width, now.Height);
                    if (queries.TryReturn(id, interior.NameOrUniqueName, 64, offsets, out WarpTarget? fresh))
                    {
                        actor.Position = new Vector2((float)fresh!.Position.X, (float)fresh.Position.Y);
                        actor.faceDirection(GameRotationHost.GameFacing(fresh.Direction));
                    }
                    else
                    {
                        // Something moved into the exit during the fade; return to the normal indoor arrival.
                        Warp entry = interior.warps[0];
                        Game1.warpFarmer(interior.NameOrUniqueName, entry.X, entry.Y - 1, 0, true);
                        Game1.showRedMessage("The outside entrance became blocked; returned indoors.");
                    }
                }
                finally { IgnoredFarmer = previous; }
            };
            return true;
        }
        return true;
    }
}
