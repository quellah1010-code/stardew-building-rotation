using BuildingRotation.Core;
using BuildingRotation.Smapi;
using static SelfTestAssert;

internal static class RevealTests
{
    private static readonly TileRectangle Door = new(15, 10, 1, 1);
    private static readonly PixelPoint Near = new(15.5 * 64, 9.5 * 64);
    private static readonly PixelPoint Far = new(15.5 * 64, 5 * 64);
    private static void Settle(NorthRevealState state, PixelPoint feet)
    { for (int i = 0; i < 60; i++) state.Update(Facing.North, Door, feet, 1d / 60); }

    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("Reveal: north door approach fades in; other facings stay opaque", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near); Equal(1f, s.Amount);
            foreach (Facing f in new[] { Facing.East, Facing.West, Facing.South })
            { s.Update(f, Door, Near, .1); Equal(0f, s.Amount); Check(!s.IsTriggered, "Non-North Reveal"); }
        });
        yield return ("Reveal: near the south wall cannot trigger the north entrance", () =>
        {
            var s = new NorthRevealState(); Settle(s, new PixelPoint(15.5 * 64, 14 * 64));
            Equal(0f, s.Amount); Check(!s.IsTriggered, "Triggered on wrong side");
        });
        yield return ("Reveal: enter at three tiles outward; beyond that starts opaque", () =>
        {
            var s = new NorthRevealState();
            s.Update(Facing.North, Door, new PixelPoint(Near.X, 7 * 64 - 1), .016);
            Check(!s.IsTriggered, "Entered too far away");
            s.Update(Facing.North, Door, new PixelPoint(Near.X, 7 * 64), .016);
            Check(s.IsTriggered && s.Amount > 0 && s.Amount < 1, "No partial entrance transition");
        });
        yield return ("Reveal: boundary jitter stays active until wider exit boundary", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near);
            double enterX = Door.X * 64 - NorthRevealState.SideReach;
            for (int i = 0; i < 20; i++)
            {
                s.Update(Facing.North, Door, new PixelPoint(enterX + (i % 2 == 0 ? -1 : 1), Near.Y), .016);
                Check(s.IsTriggered, "Flicker at activation boundary");
            }
            s.Update(Facing.North, Door, new PixelPoint(enterX - NorthRevealState.ExitMargin - 1, Near.Y), .016);
            Check(!s.IsTriggered && s.Amount > 0 && s.Amount < 1, "Exit did not start smoothly");
        });
        yield return ("Reveal: leaving restores full artwork, re-entering reverses smoothly", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near);
            s.Update(Facing.North, Door, Far, .05); float leaving = s.Amount;
            s.Update(Facing.North, Door, Near, .01);
            Check(s.Amount > leaving && s.Amount < 1, "Re-entry snapped or kept fading out");
            Settle(s, Far); Equal(0f, s.Amount);
        });
        yield return ("Reveal: moving the door discards previous activation", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near);
            s.Update(Facing.North, Door.Translate(new TilePoint(20, 0)), Near, .016);
            Equal(0f, s.Amount); Check(!s.IsTriggered, "Old location state carried into new pose");
        });
        yield return ("Reveal: two buildings have independent transitions", () =>
        {
            var first = new NorthRevealState(); var second = new NorthRevealState(); Settle(first, Near);
            second.Update(Facing.North, Door.Translate(new TilePoint(20, 0)), Near, .1);
            Equal(1f, first.Amount); Equal(0f, second.Amount);
        });
        yield return ("Reveal: transition duration follows elapsed time instead of frame count", () =>
        {
            var a = new NorthRevealState(); var b = new NorthRevealState();
            for (int i = 0; i < 5; i++) a.Update(Facing.North, Door, Near, .01);
            for (int i = 0; i < 10; i++) b.Update(Facing.North, Door, Near, .005);
            Check(Math.Abs(a.Amount - b.Amount) < .00001, "Frame-rate-dependent fade");
            float previous = a.Amount; a.Update(Facing.North, Door, Near, 0); Equal(previous, a.Amount);
        });
        yield return ("Reveal: reset for pickup, title or location change discards visibility", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near); s.Reset();
            Equal(0f, s.Amount); Check(!s.IsTriggered, "Reset retained visibility");
        });
        yield return ("Reveal: trigger follows actual multi-cell door instead of hardcoded Barn position", () =>
        {
            var door = new TileRectangle(50, 30, 2, 1); var s = new NorthRevealState();
            s.Update(Facing.North, door, new PixelPoint(53 * 64, 29 * 64), .1);
            Check(s.IsTriggered, "Door width not included");
            s.Reset(); s.Update(Facing.North, door, Near, .1);
            Check(!s.IsTriggered, "Old fixed Barn coordinates used");
        });
        yield return ("Reveal: north entrance and original side annex follow the existing transforms", () =>
        {
            var footprint = new Footprint(7, 4);
            var human = new TilePoint(1, 3);
            Equal(new TilePoint(5, 0), footprint.TransformCell(human, Facing.North));
            var annex = new TilePoint(6, 1); // Original front has annex on image right.
            Equal(new TilePoint(1, 0), footprint.TransformCell(annex, Facing.East)); // Far side.
            Equal(new TilePoint(2, 6), footprint.TransformCell(annex, Facing.West)); // Near side.
        });
        yield return ("Reveal: clicking the visible door still requires the exact approach and facing", () =>
        {
            var s = new NorthRevealState(); Settle(s, Near);
            PixelRectangle bounds = NorthRevealState.EntranceBounds(Door);
            var click = new PixelPoint(bounds.X + 32, bounds.Y + 40);
            Check(s.CanClickEntrance(Door, click, new TilePoint(15, 9), Facing.South), "Visible door not clickable");
            Check(!s.CanClickEntrance(Door, click, new TilePoint(15, 8), Facing.South), "Reveal range became warp range");
            Check(!s.CanClickEntrance(Door, click, new TilePoint(14, 9), Facing.South), "Side approach accepted");
            Check(!s.CanClickEntrance(Door, click, new TilePoint(15, 9), Facing.North), "Facing away accepted");
        });
        yield return ("Reveal: invisible and out-of-sprite door clicks keep ordinary input", () =>
        {
            var s = new NorthRevealState(); PixelRectangle bounds = NorthRevealState.EntranceBounds(Door);
            var click = new PixelPoint(bounds.X + 32, bounds.Y + 40); var actor = new TilePoint(15, 9);
            Check(!s.CanClickEntrance(Door, click, actor, Facing.South), "Invisible door consumed input");
            Settle(s, Near);
            Check(!s.CanClickEntrance(Door, new PixelPoint(bounds.Right + 1, bounds.Y), actor, Facing.South), "Outside sprite consumed input");
        });
        yield return ("Reveal: visual sill matches the north boundary after a move", () =>
        {
            var moved = Door.Translate(new TilePoint(-7, 23));
            PixelRectangle bounds = NorthRevealState.EntranceBounds(moved);
            Equal((moved.X + .5) * 64, bounds.X + 8 * 4);
            Equal(moved.Y * 64d, bounds.Y + 26 * 4);
        });
    }
}
