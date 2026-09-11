using BuildingRotation.Core;
using BuildingRotation.Data;
using BuildingRotation.Runtime;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using Facing = BuildingRotation.Core.Facing;

namespace BuildingRotation.Smapi;

internal sealed class GameRotationHost : IRotationHost
{
    private sealed class Entry
    {
        public readonly string Id = Guid.NewGuid().ToString("N");
        public string Source = "";
        public BuildingLayoutDefinition? Definition;
        public string State = "";
        public GameLocation? Exterior;
        public GameLocation? Interior;
        public long Revision;
    }

    private readonly Dictionary<Building, Entry> entries = new();
    private readonly Dictionary<string, Building> buildings = new();
    public void Clear() { entries.Clear(); buildings.Clear(); }

    public string Identify(Building b)
    {
        if (!entries.TryGetValue(b, out Entry? entry))
        {
            entries.Add(b, entry = new Entry());
            buildings.Add(entry.Id, b);
        }
        return entry.Id;
    }

    public Building Find(string id) => buildings[id];

    public BuildingLayoutDefinition Definition(Building b)
    {
        Identify(b);
        Entry entry = entries[b];
        var d = b.GetData() ?? throw new NotSupportedException("Building data is unavailable.");
        string key = $"{b.buildingType.Value}|{d.Size}|{d.CollisionMap}|{d.HumanDoor}|{d.AnimalDoor}|{d.AdditionalTilePropertyRadius}|{d.AllowsFlooringUnderneath}|"
            + string.Join(";", d.AdditionalPlacementTiles?.Select(p => $"{p.TileArea}:{p.OnlyNeedsToBePassable}") ?? Enumerable.Empty<string>());
        if (entry.Definition == null || entry.Source != key)
        {
            if (d.AdditionalTilePropertyRadius != 0)
                throw new NotSupportedException("This prototype does not rotate extended tile properties.");
            var sprite = new SpriteDataSnapshot(d.Texture, Rect(d.SourceRect),
                new PixelPoint(d.DrawOffset.X, d.DrawOffset.Y), new TilePoint(d.SeasonOffset.X, d.SeasonOffset.Y), d.SortTileOffset);
            var data = new BuildingDataSnapshot(b.buildingType.Value, new Footprint(d.Size.X, d.Size.Y), d.CollisionMap,
                new TilePoint(d.HumanDoor.X, d.HumanDoor.Y), Rect(d.AnimalDoor), sprite,
                d.AdditionalPlacementTiles?.Select(p => new PlacementRequirement(TileRect(p.TileArea), p.OnlyNeedsToBePassable)),
                indoorMap: d.IndoorMap);
            entry.Definition = data.ToLayoutDefinition();
            entry.Source = key;
        }
        return entry.Definition;
    }

    public static Dictionary<string, string> Metadata(Building b)
        => b.modData.Pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    public BuildingSnapshot Read(string id)
    {
        Building b = Find(id);
        BuildingLayoutDefinition definition = Definition(b);
        Entry entry = entries[b];
        GameLocation exterior = b.GetParentLocation() ?? throw new InvalidOperationException("Building has no parent location.");
        GameLocation interior = b.GetIndoors() ?? throw new NotSupportedException("Building has no interior.");
        var metadata = Metadata(b);
        bool ready = IsEmpty(b) && !Context.IsMultiplayer && Context.IsMainPlayer
            && ReferenceEquals(exterior, Game1.getFarm()) && ReferenceEquals(Game1.currentLocation, exterior);
        string state = $"{b.buildingType.Value}|{b.tileX.Value},{b.tileY.Value}|{b.tilesWide.Value},{b.tilesHigh.Value}|{b.humanDoor.Value}|{b.animalDoor.Value}|{ready}|{entry.Source}|"
            + string.Join(";", metadata.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key.Length}:{p.Key}{p.Value.Length}:{p.Value}"))
            + "|warps:" + string.Join(";", interior.warps.Select(w => $"{w.X},{w.Y}:{w.TargetName}:{w.TargetX},{w.TargetY}"));
        if (state != entry.State || !ReferenceEquals(exterior, entry.Exterior) || !ReferenceEquals(interior, entry.Interior))
        {
            entry.Revision++;
            entry.State = state; entry.Exterior = exterior; entry.Interior = interior;
        }
        return new BuildingSnapshot(id, entry.Revision, exterior.NameOrUniqueName, interior.NameOrUniqueName,
            b.buildingType.Value, ready, definition, new TilePoint(b.tileX.Value, b.tileY.Value), metadata);
    }

    public static bool IsEmpty(Building b)
        => b.buildingType.Value == "Barn" && b.GetType() == typeof(Building)
            && b.daysOfConstructionLeft.Value == 0 && b.daysUntilUpgrade.Value == 0
            && b.GetIndoors() is AnimalHouse house && house.animalsThatLiveHere.Count == 0
            && !house.animals.Pairs.Any() && !house.objects.Pairs.Any() && house.furniture.Count == 0
            && house.characters.Count == 0 && !house.farmers.Any() && !house.terrainFeatures.Pairs.Any();

    public BuildingLayout? ManagedLayout(Building b)
    {
        if (b.buildingType.Value != "Barn" || !b.modData.ContainsKey(FacingData.Key)) return null;
        return Definition(b).CreateLayout(new PlacementPose(new TilePoint(b.tileX.Value, b.tileY.Value), FacingData.Read(Metadata(b))));
    }

    public bool CanPlace(BuildingSnapshot current, BuildingLayout candidate, out string reason)
    {
        Building b = Find(current.Id);
        GameLocation loc = b.GetParentLocation();
        if (!current.SupportsPrototype || loc != Game1.currentLocation || !loc.IsBuildableLocation())
        { reason = "Only an empty ordinary Barn on the current single-player farm can rotate."; return false; }
        using var ignored = RotationPatches.Ignore(b);
        bool TileAllowed(int x, int y, bool passable)
        {
            var tile = new Vector2(x, y);
            var box = new Rectangle(x * 64, y * 64, 64, 64);
            if (!InBounds(loc, box) || !loc.isBuildable(tile, passable)) return false;
            // Avoid vanilla placement's destructive grass/artifact cleanup in this prototype.
            if (!passable && (loc.objects.ContainsKey(tile) || (loc.terrainFeatures.TryGetValue(tile, out TerrainFeature terrain)
                && (!(terrain is Flooring) || !b.GetData().AllowsFlooringUnderneath)))) return false;
            return ActorsClear(loc, box);
        }
        for (int y = 0; y < candidate.Footprint.Height; y++)
            for (int x = 0; x < candidate.Footprint.Width; x++)
                if (!TileAllowed(candidate.Pose.Origin.X + x, candidate.Pose.Origin.Y + y, false))
                { reason = "The footprint is blocked or not buildable; clear the ground first."; return false; }
        foreach (var requirement in candidate.LocalPlacementRequirements)
        {
            TileRectangle area = requirement.Area.Translate(candidate.Pose.Origin);
            for (int y = area.Y; y < area.Bottom; y++)
                for (int x = area.X; x < area.Right; x++)
                    if (!TileAllowed(x, y, requirement.OnlyNeedsToBePassable))
                    { reason = "An additional placement area is blocked."; return false; }
        }
        reason = b.isThereAnythingtoPreventConstruction(loc, new Vector2(candidate.Pose.Origin.X, candidate.Pose.Origin.Y)) ?? "";
        return reason.Length == 0;
    }

    public bool CanOccupy(BuildingSnapshot current, PixelRectangle worldBounds)
    {
        Building b = Find(current.Id);
        GameLocation loc = b.GetParentLocation();
        Rectangle box = PixelRect(worldBounds);
        if (!InBounds(loc, box) || !ActorsClear(loc, box)) return false;
        using var ignored = RotationPatches.Ignore(b);
        return !loc.isCollidingPosition(box, Game1.viewport, false, 0, false, null, true,
            ignoreCharacterRequirement: true, skipCollisionEffects: true);
    }

    public bool TryApply(BuildingSnapshot expected, BuildingLayout candidate,
        IReadOnlyDictionary<string, string> proposedModData, out string reason)
    {
        BuildingSnapshot current = Read(expected.Id);
        if (current.Revision != expected.Revision || !current.Pose.Equals(expected.Pose) || !current.SupportsPrototype)
        { reason = "The building changed; cancel and pick it up again."; return false; }
        if (!CanPlace(current, candidate, out reason)) return false;
        PixelRectangle approach = candidate.LocalDoors["human"].WorldApproach(candidate.Pose.Origin, 1).ToPixels(64);
        if (candidate.IntersectsWorld(approach, 64) || !CanOccupy(current, approach))
        { reason = "The entrance is blocked."; return false; }

        Building b = Find(expected.Id);
        var old = new LiveValues(b);
        try
        {
            // No global data edits or vanilla construction calls that remove world objects.
            b.modData[FacingData.Key] = proposedModData[FacingData.Key];
            WriteLayout(b, candidate);
            reason = "";
            return true;
        }
        catch
        {
            old.Restore(b);
            throw;
        }
    }

    public void Reapply(Building b)
    {
        BuildingLayout? layout = ManagedLayout(b);
        if (layout != null) WriteLayout(b, layout);
    }

    public static void WriteLayout(Building b, BuildingLayout layout)
    {
        b.tileX.Value = layout.Pose.Origin.X; b.tileY.Value = layout.Pose.Origin.Y;
        b.tilesWide.Value = layout.Footprint.Width; b.tilesHigh.Value = layout.Footprint.Height;
        TileRectangle door = layout.LocalDoors["human"].Area;
        b.humanDoor.Value = new Point(door.X, door.Y);
        if (layout.LocalDoors.TryGetValue("animal", out DoorRegion? animal))
            b.animalDoor.Value = new Point(animal.Area.X, animal.Area.Y);
        UpdateWarps(b, layout);
    }

    public static void UpdateWarps(Building b, BuildingLayout layout, GameLocation? interior = null)
    {
        interior ??= b.GetIndoors();
        if (interior == null) return;
        GameLocation? parent = b.GetParentLocation();
        TileRectangle approach = layout.LocalDoors["human"].WorldApproach(layout.Pose.Origin, 1);
        foreach (Warp warp in interior.warps)
            if (warp.TargetName == "Farm" || warp.TargetName == parent?.NameOrUniqueName)
            {
                warp.TargetName = parent?.NameOrUniqueName ?? warp.TargetName;
                warp.TargetX = approach.X + approach.Width / 2; warp.TargetY = approach.Y + approach.Height / 2;
            }
    }

    private sealed class LiveValues
    {
        private readonly int x, y, width, height;
        private readonly Point human, animal;
        private readonly string? facing;
        private readonly (Warp warp, string name, int x, int y)[] warps;
        public LiveValues(Building b)
        {
            x = b.tileX.Value; y = b.tileY.Value; width = b.tilesWide.Value; height = b.tilesHigh.Value;
            human = b.humanDoor.Value; animal = b.animalDoor.Value;
            facing = b.modData.TryGetValue(FacingData.Key, out string value) ? value : null;
            warps = b.GetIndoors()?.warps.Select(w => (w, w.TargetName, w.TargetX, w.TargetY)).ToArray() ?? Array.Empty<(Warp, string, int, int)>();
        }
        public void Restore(Building b)
        {
            var failures = new List<Exception>();
            void RestoreOne(Action action) { try { action(); } catch (Exception ex) { failures.Add(ex); } }
            // Attempt every original field even if a third-party change listener throws.
            RestoreOne(() => b.tileX.Value = x); RestoreOne(() => b.tileY.Value = y);
            RestoreOne(() => b.tilesWide.Value = width); RestoreOne(() => b.tilesHigh.Value = height);
            RestoreOne(() => b.humanDoor.Value = human); RestoreOne(() => b.animalDoor.Value = animal);
            RestoreOne(() => { if (facing == null) b.modData.Remove(FacingData.Key); else b.modData[FacingData.Key] = facing; });
            foreach (var old in warps)
            {
                RestoreOne(() => old.warp.TargetName = old.name);
                RestoreOne(() => old.warp.TargetX = old.x); RestoreOne(() => old.warp.TargetY = old.y);
            }
            if (failures.Count > 0) throw new AggregateException("Rotation rollback reported errors; do not save this test session.", failures);
        }
    }

    public static bool InBounds(GameLocation loc, Rectangle box)
        => box.Left >= 0 && box.Top >= 0 && box.Right <= loc.Map.Layers[0].LayerWidth * 64 && box.Bottom <= loc.Map.Layers[0].LayerHeight * 64;

    public static bool ActorsClear(GameLocation loc, Rectangle box)
        => !loc.farmers.Any(f => f != RotationPatches.IgnoredFarmer && f.GetBoundingBox().Intersects(box))
            && !loc.characters.Any(c => c.GetBoundingBox().Intersects(box))
            && !loc.animals.Values.Any(a => a.GetBoundingBox().Intersects(box));

    public static int GameFacing(Facing facing) => facing switch { Facing.North => 0, Facing.East => 1, Facing.South => 2, _ => 3 };
    public static Facing CoreFacing(int facing) => facing switch { 0 => Facing.North, 1 => Facing.East, 2 => Facing.South, _ => Facing.West };
    public static Rectangle PixelRect(PixelRectangle r) => new((int)Math.Floor(r.X), (int)Math.Floor(r.Y), (int)Math.Ceiling(r.Right) - (int)Math.Floor(r.X), (int)Math.Ceiling(r.Bottom) - (int)Math.Floor(r.Y));
    private static ContentRectangle Rect(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
    private static TileRectangle TileRect(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}
