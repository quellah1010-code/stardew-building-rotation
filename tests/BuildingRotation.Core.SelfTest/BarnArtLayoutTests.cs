using BuildingRotation.Core;
using BuildingRotation.Runtime;
using BuildingRotation.Smapi;
using static SelfTestAssert;

internal static class BarnArtLayoutTests
{
    private static BuildingLayoutDefinition Source() => new(CollisionMask.Solid(new Footprint(7, 4)),
        new Dictionary<string, DoorRegion> {
            ["human"] = new(new TileRectangle(1,3,1,1), Facing.South),
            ["animal"] = new(new TileRectangle(3,3,2,1), Facing.South)
        });
    private static BuildingLayout North(TilePoint origin = default)
        => BarnArtLayout.Apply(Source()).CreateLayout(new PlacementPose(origin, Facing.North));

    private sealed class Host : IRotationHost
    {
        public BuildingSnapshot Snapshot;
        public bool Clear = true;
        public Host()
        {
            Snapshot = new BuildingSnapshot("test", 1, "Farm", "Inside", "Barn", true,
                BarnArtLayout.Apply(Source()), new TilePoint(10,20),
                FacingData.WithFacing(new Dictionary<string,string>(), Facing.North));
        }
        public BuildingSnapshot Read(string id) => Snapshot;
        public bool CanPlace(BuildingSnapshot current, BuildingLayout candidate, out string reason)
        { reason = ""; return Clear; }
        public bool CanOccupy(BuildingSnapshot current, PixelRectangle box) => Clear;
        public bool TryApply(BuildingSnapshot expected, BuildingLayout candidate, IReadOnlyDictionary<string,string> metadata, out string reason)
        {
            reason = "";
            Snapshot = new BuildingSnapshot("test", 2, "Farm", "Inside", "Barn", true,
                expected.Definition, candidate.Pose.Origin, metadata);
            return true;
        }
    }

    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("North path: 48x32 actor can cross the old north edge, from both sides", () =>
        {
            var fixedLayout = North(new TilePoint(10,20));
            var old = Source().CreateLayout(fixedLayout.Pose);
            // Feet/bounding box straddle the old first-row boundary, matching the
            // user's blocked horizontal path. Move the whole actor, not a point.
            int hitsBefore = 0;
            for (int x = 10*64-48; x <= 17*64; x += 4)
            {
                var body = new PixelRectangle(x,20*64-16,48,32);
                if (old.IntersectsWorld(body,64)) hitsBefore++;
                Check(!fixedLayout.IntersectsWorld(body,64), "Rear crossing still blocked");
            }
            Check(hitsBefore > 0,"Fixture did not reproduce old obstruction");
            for (int x = 17*64; x >= 10*64-48; x -= 4)
                Check(!fixedLayout.IntersectsWorld(new PixelRectangle(x,20*64+16,48,32),64),"Reverse passage blocked");
        });
        yield return ("North path: the next row blocks the whole actor at the exact wall edge", () =>
        {
            var n = North();
            for (int x = 0; x <= 7*64-48; x += 4)
            {
                Check(!n.IntersectsWorld(new PixelRectangle(x,32,48,32),64),"Touching edge blocks");
                Check(n.IntersectsWorld(new PixelRectangle(x,33,48,32),64),"Crossed solid wall");
            }
        });
        yield return ("North path: tile passability and rectangle collision agree, reserved size stays 7x4", () =>
        {
            var n = North(); Equal(7,n.Footprint.Width);Equal(4,n.Footprint.Height);
            for(int y=0;y<4;y++)for(int x=0;x<7;x++)
            {
                Equal(y>0,n.BlocksLocal(new TilePoint(x,y)));
                Equal(y>0,n.IntersectsWorld(new PixelRectangle(x*64+8,y*64+16,48,32),64));
            }
        });
        yield return ("North path: ordinary other directions and source definitions are unchanged", () =>
        {
            var source=Source(); var tuned=BarnArtLayout.Apply(source);
            foreach(var facing in new[]{Facing.South,Facing.East,Facing.West})
            {
                var before=source.CreateLayout(new PlacementPose(default,facing));
                var after=tuned.CreateLayout(before.Pose);
                for(int y=0;y<after.Footprint.Height;y++)for(int x=0;x<after.Footprint.Width;x++)
                    Equal(before.BlocksLocal(new TilePoint(x,y)),after.BlocksLocal(new TilePoint(x,y)));
                foreach(var pair in before.LocalDoors) Equal(pair.Value.Area,after.LocalDoors[pair.Key].Area);
            }
            Check(source.CreateLayout(new PlacementPose(default,Facing.North)).BlocksLocal(new TilePoint(0,0)),"Global source mutated");
        });
        yield return ("North path: both doors, visual sill and approach agree with the new wall", () =>
        {
            var n=North(); Equal(new TileRectangle(5,1,1,1),n.LocalDoors["human"].Area);
            Equal(new TileRectangle(2,1,2,1),n.LocalDoors["animal"].Area);
            Equal(new TileRectangle(5,0,1,1),n.LocalDoors["human"].ApproachArea(1));
            var visual=NorthRevealState.EntranceBounds(n.LocalDoors["human"].Area);
            Equal(64d,visual.Y+104);Equal(352d,visual.X+32);
        });
        yield return ("North path: existing facing metadata reloads into the calibrated entrance", () =>
        {
            var host=new Host(); var saved=host.Snapshot;
            host.Snapshot=new BuildingSnapshot(saved.Id,0,saved.ExteriorId,saved.InteriorId,
                saved.BuildingType,true,BarnArtLayout.Apply(Source()),saved.Pose.Origin,saved.ModData);
            var q=new RotationQueries(host);
            Equal("Inside",q.EntryInterior("test","Farm",new TilePoint(15,21),new TilePoint(15,20),Facing.South)!);
            Check(q.EntryInterior("test","Farm",new TilePoint(15,20),new TilePoint(15,19),Facing.South)==null,"Old door stayed active");
        });
        yield return ("North path: exit fits the same walkway, blocked exits still reject", () =>
        {
            var host=new Host();var q=new RotationQueries(host);
            var actor=new PixelRectangle(8,0,48,32);
            Check(q.TryReturn("test","Inside",64,actor,out var target),"Exit failed");
            var box=new PixelRectangle(target!.Position.X+8,target.Position.Y,48,32);
            Check(host.Snapshot.Layout.LocalDoors["human"].WorldApproach(host.Snapshot.Pose.Origin,1).ToPixels(64).Contains(box),"Outside path");
            Check(!host.Snapshot.Layout.IntersectsWorld(box,64),"Returned into wall");
            host.Clear=false;Check(!q.TryReturn("test","Inside",64,actor,out target)&&target==null,"Blocked exit accepted");
        });
        yield return ("North path: rotating preview and cancel retain the saved location and layout", () =>
        {
            var host=new Host();var original=host.Snapshot;var session=new RotationSession(host,"test");
            session.RotateSteps(1); session.MoveTo(new TilePoint(30,40));session.Cancel();
            Equal(original.Pose,session.Preview.Pose);Check(ReferenceEquals(original,host.Snapshot),"Cancel wrote live data");
            Check(!session.Preview.BlocksLocal(new TilePoint(0,0)),"North path lost on cancel");
        });
        yield return ("North path: applying new pose retains the path at the moved origin", () =>
        {
            var host=new Host();var session=new RotationSession(host,"test");session.MoveTo(new TilePoint(40,50));
            Check(session.TryPlace(64),"Placement rejected");
            var n=host.Snapshot.Layout;Check(!n.BlocksWorld(new TilePoint(40,50)),"Moved path blocked");
            Check(n.BlocksWorld(new TilePoint(40,51)),"Moved wall absent");
            Equal(new TileRectangle(45,51,1,1),n.LocalDoors["human"].WorldArea(n.Pose.Origin));
        });
        yield return ("North path: content-mod collision or nonstandard doors are not replaced", () =>
        {
            var source=new BuildingLayoutDefinition(CollisionMask.FromRows("OXXXXXX","XXXXXXX","XXXXXXX","XXXXXXX"),Source().SourceDoors);
            Check(ReferenceEquals(source,BarnArtLayout.Apply(source)),"Custom openings overwritten");
            var custom=new BuildingLayoutDefinition(Source().Collision,new Dictionary<string,DoorRegion>{{"human",new(new TileRectangle(2,3,1,1),Facing.South)}});
            Check(ReferenceEquals(custom,BarnArtLayout.Apply(custom)),"Custom door overwritten");
        });
        yield return ("North path: explicit mask override rejects mismatched size and remains immutable", () =>
        {
            var s=Source();var masks=new Dictionary<Facing,CollisionMask>{{Facing.North,CollisionMask.Solid(new Footprint(2,2))}};
            Throws<ArgumentException>(()=>new BuildingLayoutDefinition(s.Collision,s.SourceDoors,null,null,null,0,masks));
            masks[Facing.North]=CollisionMask.FromRows("OOOOOOO","XXXXXXX","XXXXXXX","XXXXXXX");
            var d=new BuildingLayoutDefinition(s.Collision,s.SourceDoors,null,null,null,0,masks);masks.Clear();
            Check(!d.CreateLayout(new PlacementPose(default,Facing.North)).BlocksLocal(new TilePoint(0,0)),"Caller changed stored mask");
        });
        yield return ("North path: left end of passage triggers Reveal before reaching the door", () =>
        {
            var n=North(new TilePoint(10,20));var state=new NorthRevealState();
            var door=n.LocalDoors["human"].WorldArea(n.Pose.Origin);var path=new TileRectangle(n.Pose.Origin.X,n.Pose.Origin.Y,n.Footprint.Width,n.Footprint.Height);
            for(int i=0;i<20;i++)state.Update(Facing.North,door,new PixelPoint(10*64,20*64+32),.016,path);
            Check(state.IsTriggered&&state.Amount==1,"Left passage concealed");
            for(int i=0;i<20;i++)state.Update(Facing.North,door,new PixelPoint(10*64,27*64),.016,path);
            Equal(0f,state.Amount);
        });
        yield return ("North path: roof above the left approach cannot hide the approaching farmer", () =>
        {
            var n=North(new TilePoint(10,20));var state=new NorthRevealState();
            var door=n.LocalDoors["human"].WorldArea(n.Pose.Origin);var path=new TileRectangle(n.Pose.Origin.X,n.Pose.Origin.Y,n.Footprint.Width,n.Footprint.Height);
            state.Update(Facing.North,door,new PixelPoint(10.5*64,18*64),.1,path);
            Check(state.IsTriggered,"Roof obscures left approach before Reveal");
            state.Reset();state.Update(Facing.North,door,new PixelPoint(10.5*64,16*64),.1,path);
            Check(!state.IsTriggered,"Distant player triggers whole building");
        });
    }
}
