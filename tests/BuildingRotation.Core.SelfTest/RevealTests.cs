using BuildingRotation.Core;
using BuildingRotation.Smapi;
using static SelfTestAssert;

internal static class RevealTests
{
    private static readonly TileRectangle Door = new(15, 10, 1, 1);
    private static readonly TileRectangle Building = new(10, 9, 7, 4);
    private static readonly PixelPoint Near = new(15.5 * 64, 9.5 * 64);
    private static readonly PixelPoint Far = new(15.5 * 64, 5 * 64);
    private static void Tick(NorthRevealState state, PixelPoint feet, double seconds)
        => state.Update(Facing.North, Door, feet, seconds, Building);
    private static void Settle(NorthRevealState state, PixelPoint feet)
    { for (int i = 0; i < 60; i++) Tick(state, feet, 1d / 60); }

    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("Reveal: North building fades; other building orientations retain their existing rendering", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near); Equal(1f, s.Amount);
            foreach (Facing f in new[] { Facing.East, Facing.West, Facing.South })
            { s.Update(f, Door, Near, .1, Building); Equal(0f, s.Amount); Check(!s.IsTriggered, "Non-North Reveal"); }
        });
        yield return ("Reveal: approach from any of four sides triggers, including right and south away from door", () =>
        {
            foreach (var p in new[] { new PixelPoint(9*64,11*64),new PixelPoint(18*64,11*64),
                new PixelPoint(13*64,8*64),new PixelPoint(13*64,14*64) })
            { var s=new NorthRevealState();Settle(s,p);Equal(1f,s.Amount); }
        });
        yield return ("Reveal: two-tile square ring includes all four outer corners", () =>
        {
            foreach(int x in new[]{8,19})foreach(int y in new[]{7,15})
            { var s=new NorthRevealState();Tick(s,new PixelPoint(x*64,y*64),.016);Check(s.IsTriggered,"Corner missed"); }
        });
        yield return ("Reveal: exact two-tile edges enter; one pixel outside each edge does not", () =>
        {
            var inside=new[]{new PixelPoint(8*64,11*64),new PixelPoint(19*64,11*64),new PixelPoint(13*64,7*64),new PixelPoint(13*64,15*64)};
            var outside=new[]{new PixelPoint(8*64-1,11*64),new PixelPoint(19*64+1,11*64),new PixelPoint(13*64,7*64-1),new PixelPoint(13*64,15*64+1)};
            for(int i=0;i<4;i++)
            {
                var s=new NorthRevealState();Tick(s,outside[i],.016);Check(!s.IsTriggered,"Entered outside two tiles");
                Tick(s,inside[i],.016);Check(s.IsTriggered&&s.Amount>0&&s.Amount<1,"Edge failed smooth entry");
            }
        });
        yield return ("Reveal: boundary jitter stays active until wider exit boundary", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near);
            double enterX = Building.X * 64 - NorthRevealState.ProximityReach;
            for (int i = 0; i < 20; i++)
            { Tick(s,new PixelPoint(enterX+(i%2==0?-1:1),Near.Y),.016);Check(s.IsTriggered,"Boundary flicker"); }
            Tick(s,new PixelPoint(enterX-NorthRevealState.ExitMargin-1,Near.Y),.016);
            Check(!s.IsTriggered&&s.Amount>0&&s.Amount<1,"Exit snapped");
        });
        yield return ("Reveal: leaving restores artwork and re-entry reverses smoothly", () =>
        {
            var s=new NorthRevealState();Settle(s,Near);Tick(s,Far,.05);float leaving=s.Amount;
            Tick(s,Near,.01);Check(s.Amount>leaving&&s.Amount<1,"Re-entry snapped");Settle(s,Far);Equal(0f,s.Amount);
        });
        yield return ("Reveal: moving the footprint and door discards previous activation", () =>
        {
            var s=new NorthRevealState();Settle(s,Near);var shift=new TilePoint(20,0);
            s.Update(Facing.North,Door.Translate(shift),Near,.016,Building.Translate(shift));
            Equal(0f,s.Amount);Check(!s.IsTriggered,"Old location retained");
        });
        yield return ("Reveal: building bounds, not door proximity, determine trigger", () =>
        {
            var s=new NorthRevealState();var shiftedDoor=Door.Translate(new TilePoint(-5,0));
            s.Update(Facing.North,shiftedDoor,new PixelPoint(18*64,12*64),.1,Building);
            Check(s.IsTriggered,"Right edge still gated by door");
        });
        yield return ("Reveal: two buildings have independent transitions", () =>
        {
            var first=new NorthRevealState();var second=new NorthRevealState();Settle(first,Near);var shift=new TilePoint(20,0);
            second.Update(Facing.North,Door.Translate(shift),Near,.1,Building.Translate(shift));
            Equal(1f,first.Amount);Equal(0f,second.Amount);
        });
        yield return ("Reveal: transition duration follows elapsed time instead of frame count", () =>
        {
            var a=new NorthRevealState();var b=new NorthRevealState();
            for(int i=0;i<5;i++)Tick(a,Near,.01);for(int i=0;i<10;i++)Tick(b,Near,.005);
            Check(Math.Abs(a.Amount-b.Amount)<.00001,"Frame-rate-dependent fade");
            float previous=a.Amount;Tick(a,Near,0);Equal(previous,a.Amount);
        });
        yield return ("Reveal: lifecycle reset discards visibility", () =>
        {
            var s=new NorthRevealState();Settle(s,Near);s.Reset();Equal(0f,s.Amount);Check(!s.IsTriggered,"Reset retained visibility");
        });
        yield return ("Reveal: changed footprint resets the old hysteresis even with the same door", () =>
        {
            var s=new NorthRevealState();Settle(s,new PixelPoint(19*64,11*64));
            s.Update(Facing.North,Door,new PixelPoint(19*64,11*64),.016,new TileRectangle(10,9,6,4));
            Equal(0f,s.Amount);Check(!s.IsTriggered,"Old size retained");
        });
        yield return ("Reveal: north entrance and side annex retain existing source transforms", () =>
        {
            var footprint=new Footprint(7,4);Equal(new TilePoint(5,0),footprint.TransformCell(new TilePoint(1,3),Facing.North));
            Equal(new TilePoint(1,0),footprint.TransformCell(new TilePoint(6,1),Facing.East));
            Equal(new TilePoint(2,6),footprint.TransformCell(new TilePoint(6,1),Facing.West));
        });
        yield return ("Reveal: visible door still requires the exact approach and facing", () =>
        {
            var s=new NorthRevealState();Settle(s,Near);var bounds=NorthRevealState.EntranceBounds(Door);
            var click=new PixelPoint(bounds.X+32,bounds.Y+40);
            Check(s.CanClickEntrance(Door,click,new TilePoint(15,9),Facing.South),"Door not clickable");
            Check(!s.CanClickEntrance(Door,click,new TilePoint(15,8),Facing.South),"Ring became warp range");
            Check(!s.CanClickEntrance(Door,click,new TilePoint(18,11),Facing.South),"Right-side Reveal enabled remote entry");
            Check(!s.CanClickEntrance(Door,click,new TilePoint(15,9),Facing.North),"Facing away accepted");
        });
        yield return ("Reveal: invisible and out-of-sprite clicks retain ordinary input", () =>
        {
            var s=new NorthRevealState();var b=NorthRevealState.EntranceBounds(Door);var click=new PixelPoint(b.X+32,b.Y+40);var actor=new TilePoint(15,9);
            Check(!s.CanClickEntrance(Door,click,actor,Facing.South),"Invisible click consumed");Settle(s,Near);
            Check(!s.CanClickEntrance(Door,new PixelPoint(b.Right+1,b.Y),actor,Facing.South),"Outside sprite consumed");
        });
        yield return ("Reveal: visual sill follows moved door boundary", () =>
        {
            var moved=Door.Translate(new TilePoint(-7,23));var b=NorthRevealState.EntranceBounds(moved);
            Equal((moved.X+.5)*64,b.X+8*4);Equal(moved.Y*64d,b.Y+26*4);
        });
    }
}
