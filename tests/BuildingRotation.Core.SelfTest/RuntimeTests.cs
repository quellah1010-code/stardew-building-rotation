using BuildingRotation.Core;
using BuildingRotation.Runtime;
using static SelfTestAssert;

internal static class RuntimeTests
{
    private static BuildingSnapshot Barn(string id = "a", Facing facing = Facing.South,
        TilePoint? origin = null, long revision = 1, bool ready = true, string type = "Barn") =>
        new(id, revision, "Farm", "BarnInterior:" + id, type, ready,
            ContentDataTests.Data("Barn").ToLayoutDefinition(), origin ?? new TilePoint(10, 10),
            FacingData.WithFacing(new Dictionary<string, string> { ["another.mod/key"] = "retain" }, facing));

    // In-memory contract implementation ONLY. This does not emulate SMAPI, Harmony or a game save.
    private sealed class Host : IRotationHost
    {
        public Dictionary<string, BuildingSnapshot> Buildings = new();
        public bool PlaceAllowed = true, OccupyAllowed = true, ApplyThrows;
        public int Writes;
        public PixelRectangle? LastBounds;
        public Host() { Buildings.Add("a", Barn()); Buildings.Add("b", Barn("b", origin: new TilePoint(40, 40))); }
        public BuildingSnapshot Read(string id) => Buildings[id];
        public bool CanPlace(BuildingSnapshot current, BuildingLayout candidate, out string reason)
        { reason = "test-world placement blocked"; return PlaceAllowed; }
        public bool CanOccupy(BuildingSnapshot current, PixelRectangle bounds)
        { LastBounds = bounds; return OccupyAllowed; }
        public bool TryApply(BuildingSnapshot expected, BuildingLayout candidate,
            IReadOnlyDictionary<string, string> data, out string reason)
        {
            reason = "test-host state changed";
            if (ApplyThrows) throw new InvalidOperationException("test-host failure before write");
            if (Read(expected.Id).Revision != expected.Revision || !PlaceAllowed) return false;
            Buildings[expected.Id] = new BuildingSnapshot(expected.Id, expected.Revision + 1,
                expected.ExteriorId, expected.InteriorId, expected.BuildingType, expected.IsEmptyAndReady,
                expected.Definition, candidate.Pose.Origin, data);
            Writes++;
            return true;
        }
    }

    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("runtime editor pickup rotate release place pipeline commits exactly once", () =>
        {
            var host = new Host(); var editor = new RotationEditor(host, 300, 20, 1);
            var context = new MoveModeContext("test-mover", "session-1", "a", true, true, true);
            var origin = new TilePoint(20, 20);
            editor.Sample(context, origin, true, 0, 0, 0, 64);
            editor.Sample(context, origin, false, 0, 0, 10, 64);
            Equal(0, host.Writes);
            Check(editor.Sample(context, origin, true, 0, 0, 20, 64).IsHandled, "Press not owned.");
            editor.Sample(context, origin, true, 25, 0, 320, 64);
            Check(editor.Sample(context, origin, false, 25, 0, 330, 64).IsHandled, "Release not owned.");
            Equal(0, host.Writes); Equal(Facing.East, editor.Session!.Preview.Pose.Direction);
            editor.Sample(context, origin, true, 25, 0, 400, 64);
            editor.Sample(context, origin, false, 25, 0, 410, 64);
            Equal(1, host.Writes); Equal(Facing.East, host.Read("a").Pose.Direction);
            editor.Sample(context, origin, false, 25, 0, 420, 64); Equal(1, host.Writes);
            Check(editor.Sample(context, origin, true, 25, 0, 430, 64).IsHandled, "Stale provider received new press.");
            Check(editor.Sample(context, origin, false, 25, 0, 440, 64).IsHandled, "Stale provider received release.");
            Equal(1, host.Writes);
        });
        yield return ("runtime editor reset and input ownership loss abandon only uncommitted previews", () =>
        {
            var host = new Host(); var editor = new RotationEditor(host, 300, 20, -1);
            var context = new MoveModeContext("test-mover", "one", "a", true, true, true);
            editor.Sample(context, default, false, 0, 0, 0, 64);
            editor.Session!.RotateSteps(2); editor.Reset(); Equal(0, host.Writes);
            Check(editor.Session == null, "Reset retained session.");
            editor.Sample(context, default, false, 0, 0, 10, 64);
            editor.Sample(new MoveModeContext("test-mover", "one", "a", true, true, false), default, false, 0, 0, 20, 64);
            Check(editor.Session == null, "Loss of ownership retained editor."); Equal(0, host.Writes);
        });
        yield return ("runtime facing codec preserves other mod keys and rejects unknown saved versions", () =>
        {
            var data = new Dictionary<string, string> { ["other"] = "value" };
            Equal(Facing.South, FacingData.Read(data));
            foreach (Facing f in Enum.GetValues<Facing>())
            {
                var saved = FacingData.WithFacing(data, f);
                Equal(f, FacingData.Read(saved)); Equal("value", saved["other"]);
                Check(!data.ContainsKey(FacingData.Key), "Preview mutated original metadata.");
            }
            data[FacingData.Key] = "2:west";
            Throws<NotSupportedException>(() => FacingData.Read(data));
            Equal("2:west", data[FacingData.Key]);
        });
        yield return ("runtime preview and cancel preserve original game pose and metadata", () =>
        {
            var host = new Host(); host.Buildings["a"] = Barn(facing: Facing.East);
            var original = host.Read("a"); var session = new RotationSession(host, "a");
            session.MoveTo(new TilePoint(20, 30)); session.RotateSteps(1);
            Equal(Facing.North, session.Preview.Pose.Direction);
            Check(ReferenceEquals(original, host.Read("a")), "Preview wrote to host.");
            session.Cancel(); Equal(original.Pose, session.Preview.Pose); Equal(0, host.Writes);
            Throws<InvalidOperationException>(() => session.TryPlace(64));
        });
        yield return ("runtime real Barn four-facing commit reload collision entry and return stay aligned", () =>
        {
            foreach (Facing facing in Enum.GetValues<Facing>())
            {
                var host = new Host(); var other = host.Read("b");
                var session = new RotationSession(host, "a");
                session.MoveTo(new TilePoint(20, 30)); session.RotateSteps((int)facing);
                Check(session.Validate(64), "Valid preview rejected.");
                Check(session.TryPlace(64), "Commit failed."); Equal(PlacementState.Placed, session.State);
                var saved = host.Read("a");
                // Recreate snapshot from metadata and host-owned location, as at a load boundary.
                host.Buildings["a"] = new BuildingSnapshot(saved.Id, 0, saved.ExteriorId, saved.InteriorId,
                    saved.BuildingType, true, saved.Definition, saved.Pose.Origin,
                    new Dictionary<string, string>(saved.ModData));
                var queries = new RotationQueries(host); var live = host.Read("a");
                Equal(facing, live.Pose.Direction); Equal("retain", live.ModData["another.mod/key"]);
                Check(ReferenceEquals(other, host.Read("b")), "Other instance changed.");
                Check(!queries.Blocks("a", "Farm", new TilePoint(10, 10)), "Old collision remained.");
                Check(queries.Blocks("a", "Farm", new TilePoint(20, 30)), "New collision missing.");
                Check(!queries.Blocks("a", "Town", new TilePoint(20, 30)), "Collision leaked across locations.");
                var door = live.Layout.LocalDoors["human"];
                var area = door.WorldArea(live.Pose.Origin); var approach = door.WorldApproach(live.Pose.Origin, 1);
                Equal(live.InteriorId, queries.EntryInterior("a", "Farm", new TilePoint(area.X, area.Y),
                    new TilePoint(approach.X, approach.Y), facing.RotateSteps(2)));
                Equal<string?>(null, queries.EntryInterior("a", "Farm", new TilePoint(11, 13),
                    new TilePoint(11, 14), Facing.North));
                Check(queries.TryReturn("a", live.InteriorId, 64, new PixelRectangle(0, 32, 32, 32), out var exit), "Return rejected.");
                Equal("Farm", exit!.LocationId); Equal(facing, exit.Direction);
                Check(approach.ToPixels(64).Contains(host.LastBounds!), "Return outside approach.");
            }
        });
        yield return ("runtime invalid placement and blocked doorway perform no writes", () =>
        {
            var host = new Host(); var original = host.Read("a"); var session = new RotationSession(host, "a");
            host.PlaceAllowed = false; Check(!session.TryPlace(64), "Invalid land accepted.");
            host.PlaceAllowed = true; host.OccupyAllowed = false;
            Check(!session.TryPlace(64), "Blocked entrance accepted.");
            Equal(0, host.Writes); Equal(PlacementState.Editing, session.State);
            Check(ReferenceEquals(original, host.Read("a")), "Failed placement changed host.");
        });
        yield return ("runtime rejects changes made after pickup without undoing those changes", () =>
        {
            var host = new Host(); var session = new RotationSession(host, "a");
            var external = Barn(origin: new TilePoint(50, 50), revision: 2);
            host.Buildings["a"] = external; Check(!session.TryPlace(64), "Stale edit accepted.");
            session.Cancel(); Check(ReferenceEquals(external, host.Read("a")), "Cancel overwrote external move.");
        });
        yield return ("runtime prototype gates upgraded occupied and unfinished buildings", () =>
        {
            var host = new Host(); host.Buildings["a"] = Barn(ready: false);
            Throws<NotSupportedException>(() => new RotationSession(host, "a"));
            host.Buildings["a"] = Barn(type: "Big Barn");
            Throws<NotSupportedException>(() => new RotationSession(host, "a"));
            host.Buildings["a"] = Barn(ready: false, facing: Facing.East);
            Check(new RotationQueries(host).TryReturn("a", "BarnInterior:a", 64,
                new PixelRectangle(0, 32, 32, 32), out _), "Occupancy change trapped returning player.");
        });
        yield return ("runtime host exception keeps draft editable and original metadata intact", () =>
        {
            var host = new Host { ApplyThrows = true }; var before = host.Read("a");
            var session = new RotationSession(host, "a"); session.RotateSteps(1);
            Throws<InvalidOperationException>(() => session.TryPlace(64));
            Equal(PlacementState.Editing, session.State); Check(ReferenceEquals(before, host.Read("a")), "State changed.");
            host.ApplyThrows = false; Check(session.TryPlace(64), "Retry failed.");
        });
        yield return ("runtime return rechecks obstacles and clears earlier successful destination", () =>
        {
            var host = new Host(); var queries = new RotationQueries(host);
            var actor = new PixelRectangle(0, 32, 32, 32);
            Check(queries.TryReturn("a", "BarnInterior:a", 64, actor, out var target), "Initial return failed.");
            host.OccupyAllowed = false;
            Check(!queries.TryReturn("a", "BarnInterior:a", 64, actor, out target), "Blocked return accepted.");
            Check(target == null, "Stale destination retained."); host.OccupyAllowed = true;
            Check(!queries.TryReturn("a", "BarnInterior:b", 64, actor, out target), "Wrong interior accepted.");
        });
        yield return ("runtime validates exact rounded actor bounds with nonzero foot-box offsets", () =>
        {
            var host = new Host(); var q = new RotationQueries(host);
            Check(q.TryReturn("a", "BarnInterior:a", 64, new PixelRectangle(.25, 32, 31.5, 32), out var t), "Return failed.");
            Equal(Math.Floor(t!.Position.X), t.Position.X);
            Equal(t.Position.X + .25, host.LastBounds!.X);
            Equal(t.Position.Y + 32, host.LastBounds.Y);
        });
        yield return ("runtime repeat move cancel and return follow latest committed location", () =>
        {
            var host = new Host(); var first = new RotationSession(host, "a");
            first.MoveTo(new TilePoint(25, 25)); first.RotateSteps(3); Check(first.TryPlace(64), "First failed.");
            var second = new RotationSession(host, "a"); second.RotateSteps(1); second.Cancel();
            Equal(Facing.West, host.Read("a").Pose.Direction);
            var third = new RotationSession(host, "a"); third.MoveTo(new TilePoint(35, 35)); Check(third.TryPlace(64), "Third failed.");
            var q = new RotationQueries(host);
            Check(q.TryReturn("a", "BarnInterior:a", 64, new PixelRectangle(0, 32, 32, 32), out var exit), "Return failed.");
            Check(exit!.Position.X > 30 * 64, "Old building location reused.");
        });
    }
}
