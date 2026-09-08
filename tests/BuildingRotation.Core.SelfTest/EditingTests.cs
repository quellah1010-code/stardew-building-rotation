using BuildingRotation.Core;
using static SelfTestAssert;

internal static class EditingTests
{
    // These numbers are test fixtures, not approved user-facing defaults.
    private static EditGesture Gesture() => new EditGesture(250, 8);
    private static PlacementPose Pose(int x = 10, int y = 20, Facing facing = Facing.South) =>
        new PlacementPose(new TilePoint(x, y), facing);

    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("preview edits preserve the original and cancel restores both fields", () =>
        {
            var original = Pose();
            var draft = new PlacementDraft(original);
            draft.MoveTo(new TilePoint(30, 40));
            draft.RotateSteps(1);
            Equal(original, draft.Original);
            Equal(Pose(30, 40, Facing.East), draft.Preview);
            Equal(original, draft.Cancel());
            Equal(original, draft.Preview);
            Equal(PlacementState.Cancelled, draft.State);
            Equal(original, draft.Cancel());
            Throws<InvalidOperationException>(() => draft.RotateSteps(1));
            Throws<InvalidOperationException>(() => draft.TryPlace(_ => true));
        });
        yield return ("failed placement remains editable and a corrected position can succeed", () =>
        {
            var world = Pose();
            var draft = new PlacementDraft(world);
            draft.MoveTo(new TilePoint(-1, 4));
            draft.RotateSteps(2);
            bool Apply(PlacementPose candidate)
            {
                if (candidate.Origin.X < 0) return false;
                world = candidate;
                return true;
            }
            Check(!draft.TryPlace(Apply), "Invalid position was accepted.");
            Equal(Pose(), world);
            Equal(Pose(-1, 4, Facing.North), draft.Preview);
            Equal(PlacementState.Editing, draft.State);
            draft.MoveTo(new TilePoint(30, 40));
            Check(draft.TryPlace(Apply), "Valid position was rejected.");
            Equal(Pose(30, 40, Facing.North), world);
            Equal(PlacementState.Placed, draft.State);
            Throws<InvalidOperationException>(() => draft.Cancel());
            Throws<InvalidOperationException>(() => draft.MoveTo(new TilePoint(1, 1)));
        });
        yield return ("a later cancelled move does not undo an earlier successful placement", () =>
        {
            var world = Pose();
            var first = new PlacementDraft(world);
            first.MoveTo(new TilePoint(35, 45));
            first.RotateSteps(-1);
            first.TryPlace(candidate => { world = candidate; return true; });
            var second = new PlacementDraft(world);
            second.RotateSteps(2);
            second.MoveTo(new TilePoint(80, 90));
            Equal(world, second.Cancel());
            Equal(Pose(35, 45, Facing.West), world);
        });
        yield return ("an exception during apply leaves the draft recoverable", () =>
        {
            var draft = new PlacementDraft(Pose());
            draft.MoveTo(new TilePoint(30, 40));
            Throws<ArgumentNullException>(() => draft.TryPlace(null!));
            Throws<InvalidOperationException>(() => draft.TryPlace(_ => throw new InvalidOperationException("Adapter failed before mutation.")));
            Equal(PlacementState.Editing, draft.State);
            Equal(Pose(30, 40), draft.Preview);
            Equal(Pose(), draft.Cancel());
        });
        yield return ("callbacks cannot change the candidate while it is being applied", () =>
        {
            var draft = new PlacementDraft(Pose());
            int calls = 0;
            Check(draft.TryPlace(candidate =>
            {
                calls++;
                Equal(PlacementState.Applying, draft.State);
                Throws<InvalidOperationException>(() => draft.MoveTo(new TilePoint(2, 2)));
                Throws<InvalidOperationException>(() => draft.RotateSteps(1));
                Throws<InvalidOperationException>(() => draft.Cancel());
                Throws<InvalidOperationException>(() => draft.TryPlace(_ => true));
                Equal(Pose(), candidate);
                return true;
            }), "Apply failed.");
            Throws<InvalidOperationException>(() => draft.TryPlace(_ => { calls++; return true; }));
            Equal(1, calls);
        });
        yield return ("the mouse-up used to pick up a building cannot immediately place it", () =>
        {
            var gesture = Gesture();
            var draft = new PlacementDraft(Pose());
            // Creating the draft consumes the pickup click; Begin is only for a subsequent press.
            Equal(GestureIntent.None, gesture.Release(100, 100, 500));
            Equal(PlacementState.Editing, draft.State);
        });
        yield return ("a short click places only on release, tolerating small pointer jitter", () =>
        {
            var gesture = Gesture();
            gesture.Begin(100, 100, 1000);
            Equal(GestureIntent.None, gesture.Update(102, 99, 1100));
            Equal(GestureIntent.Place, gesture.Release(103, 101, 1249));
            Equal(GestureIntent.None, gesture.Release(103, 101, 1250));
            Check(!gesture.IsPressed, "Gesture remained pressed.");
        });
        yield return ("a long stationary hold or vertical drag does not accidentally place", () =>
        {
            var gesture = Gesture();
            gesture.Begin(0, 0, 0);
            Equal(GestureIntent.None, gesture.Release(0, 0, 250));
            gesture.Begin(0, 0, 1000);
            Equal(GestureIntent.None, gesture.Update(0, 20, 1300));
            Equal(GestureIntent.None, gesture.Release(0, 20, 1350));
        });
        yield return ("rotation needs both the hold and horizontal distance thresholds", () =>
        {
            var gesture = Gesture();
            gesture.Begin(0, 0, 0);
            Equal(GestureIntent.None, gesture.Update(8, 0, 249));
            Equal(GestureIntent.RotatePositive, gesture.Update(8, 0, 250));
            Equal(GestureIntent.None, gesture.Release(8, 0, 251));
            gesture.Begin(0, 0, 1000);
            Equal(GestureIntent.None, gesture.Update(7, 0, 1250));
            Equal(GestureIntent.RotateNegative, gesture.Update(-8, 0, 1251));
        });
        yield return ("each held drag rotates once despite reversal or further movement", () =>
        {
            var gesture = Gesture();
            gesture.Begin(50, 50, 0);
            Equal(GestureIntent.RotatePositive, gesture.Update(70, 50, 300));
            Equal(GestureIntent.None, gesture.Update(-100, 50, 500));
            Equal(GestureIntent.None, gesture.Update(500, 50, 700));
            Equal(GestureIntent.None, gesture.Release(500, 50, 800));
            gesture.Begin(500, 50, 900);
            Equal(GestureIntent.RotateNegative, gesture.Update(490, 50, 1200));
            Equal(GestureIntent.None, gesture.Release(490, 50, 1300));
        });
        yield return ("release evaluates its final position without requiring an intermediate tick", () =>
        {
            var gesture = Gesture();
            gesture.Begin(0, 0, 0);
            Equal(GestureIntent.RotateNegative, gesture.Release(-10, 0, 300));
            Check(!gesture.IsPressed, "Released gesture remained active.");
        });
        yield return ("a quick drag returning to its starting point is not mistaken for a click", () =>
        {
            var gesture = Gesture();
            gesture.Begin(0, 0, 0);
            Equal(GestureIntent.None, gesture.Update(12, 0, 50));
            Equal(GestureIntent.None, gesture.Release(0, 0, 100));
            gesture.Begin(0, 0, 200);
            Equal(GestureIntent.None, gesture.Release(0, 12, 250));
        });
        yield return ("vertical-dominant and exact-diagonal drags are not horizontal rotations", () =>
        {
            var gesture = Gesture();
            gesture.Begin(0, 0, 0);
            Equal(GestureIntent.None, gesture.Update(12, 20, 300));
            Equal(GestureIntent.None, gesture.Release(12, 12, 350));
        });
        yield return ("cancelling a held gesture consumes a delayed release", () =>
        {
            var gesture = Gesture();
            gesture.Begin(0, 0, 0);
            gesture.Reset();
            gesture.Reset();
            Equal(GestureIntent.None, gesture.Update(20, 0, 300));
            Equal(GestureIntent.None, gesture.Release(20, 0, 400));
            gesture.Begin(0, 0, 500);
            Equal(GestureIntent.Place, gesture.Release(0, 0, 600));
        });
        yield return ("invalid parameters and non-monotonic event time are rejected", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => new EditGesture(0, 8));
            Throws<ArgumentOutOfRangeException>(() => new EditGesture(250, 0));
            Throws<ArgumentOutOfRangeException>(() => Pose(facing: (Facing)4));
            var gesture = Gesture();
            Throws<ArgumentOutOfRangeException>(() => gesture.Begin(0, 0, -1));
            gesture.Begin(0, 0, 100);
            Throws<InvalidOperationException>(() => gesture.Begin(0, 0, 200));
            Equal(GestureIntent.None, gesture.Update(0, 0, 150));
            Throws<ArgumentOutOfRangeException>(() => gesture.Release(0, 0, 149));
            gesture.Reset();
        });
        yield return ("large valid screen deltas and timestamps do not overflow", () =>
        {
            var gesture = Gesture();
            gesture.Begin(int.MaxValue, 0, long.MaxValue - 300);
            Equal(GestureIntent.RotateNegative, gesture.Release(int.MinValue, 0, long.MaxValue));
        });
        yield return ("rotate then release stays suspended; a separate short click places", () =>
        {
            var world = Pose();
            var draft = new PlacementDraft(world);
            var gesture = Gesture();
            int writes = 0;
            void Route(GestureIntent intent)
            {
                // Example adapter mapping, not a final mouse-direction preference.
                if (intent == GestureIntent.RotatePositive) draft.RotateSteps(1);
                if (intent == GestureIntent.RotateNegative) draft.RotateSteps(-1);
                if (intent == GestureIntent.Place)
                    draft.TryPlace(candidate => { writes++; world = candidate; return true; });
            }
            gesture.Begin(0, 0, 0);
            Route(gesture.Update(20, 0, 300));
            Route(gesture.Release(20, 0, 400));
            Equal(0, writes);
            Equal(Pose(), world);
            Equal(Pose(facing: Facing.East), draft.Preview);
            Equal(PlacementState.Editing, draft.State);
            draft.MoveTo(new TilePoint(40, 50));
            gesture.Begin(20, 0, 500);
            Route(gesture.Release(20, 0, 600));
            Equal(1, writes);
            Equal(Pose(40, 50, Facing.East), world);
        });
        yield return ("rejected placement followed by cancellation leaves simulated world intact", () =>
        {
            var world = Pose();
            var draft = new PlacementDraft(world);
            draft.RotateSteps(3);
            draft.MoveTo(new TilePoint(-50, 10));
            var gesture = Gesture();
            gesture.Begin(0, 0, 0);
            Equal(GestureIntent.Place, gesture.Release(0, 0, 100));
            Check(!draft.TryPlace(_ => false), "Rejected placement succeeded.");
            gesture.Begin(0, 0, 200);
            gesture.Reset();
            Equal(world, draft.Cancel());
            Equal(GestureIntent.None, gesture.Release(20, 0, 600));
            Equal(Pose(), world);
        });
    }
}
