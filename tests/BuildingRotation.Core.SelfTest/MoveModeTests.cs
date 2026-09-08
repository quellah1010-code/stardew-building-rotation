using BuildingRotation.Core;
using static SelfTestAssert;

internal static class MoveModeTests
{
    // Fixture values only; these are not final settings or real adapter IDs.
    private static MoveModeGesture Gesture() => new MoveModeGesture(250, 8);
    private static MoveModeContext Context(string provider = "native", string session = "move-1",
        string? building = "barn-1", bool moving = true, bool supported = true, bool ownsInput = true) =>
        new MoveModeContext(provider, session, building, moving, supported, ownsInput);

    private static void Expect(MoveGestureResult result, bool handled, GestureIntent intent = GestureIntent.None)
    {
        Equal(handled, result.IsHandled);
        Equal(intent, result.Intent);
    }

    internal static IEnumerable<(string Name, Action Run)> Cases()
    {
        yield return ("walking, construction, empty selection and uncooperative movers keep their inputs", () =>
        {
            foreach (var context in new MoveModeContext?[]
            {
                null, Context(moving: false), Context(building: null),
                Context(supported: false), Context(ownsInput: false)
            })
            {
                var gesture = Gesture();
                Expect(gesture.Sample(context, false, 0, 0, 0), false);
                Expect(gesture.Sample(context, true, 0, 0, 10), false);
                Expect(gesture.Sample(context, false, 0, 0, 100), false);
                Expect(gesture.Sample(context, true, 0, 0, 200), false);
                Expect(gesture.Sample(context, true, 20, 0, 500), false);
                Expect(gesture.Sample(context, false, 20, 0, 510), false);
            }
        });
        yield return ("a supported held building accepts a fresh click without an activation key", () =>
        {
            var gesture = Gesture();
            var context = Context();
            Expect(gesture.Sample(context, false, 0, 0, 0), false);
            Expect(gesture.Sample(context, true, 0, 0, 10), true);
            Expect(gesture.Sample(context, false, 0, 0, 100), true, GestureIntent.Place);
            Expect(gesture.Sample(context, false, 0, 0, 110), false);
        });
        yield return ("the pickup press cannot become a rotation or placement", () =>
        {
            var gesture = Gesture();
            Expect(gesture.Sample(Context(building: null), true, 0, 0, 0), false);
            var context = Context();
            Expect(gesture.Sample(context, true, 0, 0, 0), false);
            Expect(gesture.Sample(context, true, 20, 0, 500), false);
            Expect(gesture.Sample(context, false, 20, 0, 510), false);
            Expect(gesture.Sample(context, true, 20, 0, 600), true);
            Expect(gesture.Sample(context, false, 20, 0, 650), true, GestureIntent.Place);
        });
        yield return ("rotation owns its release without placing and the next click can place", () =>
        {
            var gesture = Gesture();
            var context = Context();
            gesture.Sample(context, false, 0, 0, 0);
            Expect(gesture.Sample(context, true, 0, 0, 10), true);
            Expect(gesture.Sample(context, true, 8, 0, 260), true, GestureIntent.RotatePositive);
            Expect(gesture.Sample(context, true, -30, 0, 400), true);
            Expect(gesture.Sample(context, false, -30, 0, 500), true);
            Expect(gesture.Sample(context, true, -30, 0, 600), true);
            Expect(gesture.Sample(context, false, -30, 0, 650), true, GestureIntent.Place);
        });
        yield return ("leaving moving mode in the same menu cancels the gesture immediately", () =>
        {
            var gesture = Gesture();
            var context = Context();
            gesture.Sample(context, false, 0, 0, 0);
            gesture.Sample(context, true, 0, 0, 10);
            Expect(gesture.Sample(Context(moving: false), true, 20, 0, 500), false);
            Expect(gesture.Sample(Context(moving: false), false, 20, 0, 510), false);
            Expect(gesture.Sample(context, false, 20, 0, 600), false);
            Expect(gesture.Sample(context, true, 20, 0, 610), true);
            Expect(gesture.Sample(context, false, 20, 0, 620), true, GestureIntent.Place);
        });
        yield return ("exiting the mode ignores late movement and mouse-up", () =>
        {
            var gesture = Gesture();
            var context = Context();
            gesture.Sample(context, false, 0, 0, 0);
            gesture.Sample(context, true, 0, 0, 10);
            Expect(gesture.Sample(null, true, 20, 0, 500), false);
            Expect(gesture.Sample(null, false, 20, 0, 510), false);
            Expect(gesture.Sample(context, false, 20, 0, 600), false);
            Expect(gesture.Sample(context, false, 20, 0, 610), false);
        });
        yield return ("changing building, provider or move session never transfers a held gesture", () =>
        {
            foreach (var next in new[]
            {
                Context(building: "barn-2"), Context(provider: "compatible-mover"),
                Context(session: "move-2")
            })
            {
                var gesture = Gesture();
                var first = Context();
                gesture.Sample(first, false, 0, 0, 0);
                gesture.Sample(first, true, 0, 0, 10);
                Expect(gesture.Sample(next, true, 20, 0, 500), false);
                Expect(gesture.Sample(next, true, 40, 0, 600), false);
                Expect(gesture.Sample(next, false, 40, 0, 610), false);
                Expect(gesture.Sample(next, true, 40, 0, 700), true);
                Expect(gesture.Sample(next, false, 40, 0, 750), true, GestureIntent.Place);
            }
        });
        yield return ("fresh observations of the same target preserve an ongoing gesture", () =>
        {
            var gesture = Gesture();
            gesture.Sample(Context(), false, 0, 0, 0);
            Expect(gesture.Sample(Context(), true, 0, 0, 10), true);
            Expect(gesture.Sample(Context(), true, 0, 0, 200), true);
            Expect(gesture.Sample(Context(), false, -8, 0, 260), true, GestureIntent.RotateNegative);
        });
        yield return ("losing input control or selection discards the gesture before reconnection", () =>
        {
            foreach (var interrupted in new[]
            {
                Context(ownsInput: false), Context(supported: false), Context(building: null)
            })
            {
                var gesture = Gesture();
                var context = Context();
                gesture.Sample(context, false, 0, 0, 0);
                gesture.Sample(context, true, 0, 0, 10);
                Expect(gesture.Sample(interrupted, true, 20, 0, 500), false);
                Expect(gesture.Sample(context, true, 20, 0, 510), false);
                Expect(gesture.Sample(context, true, 40, 0, 900), false);
                Expect(gesture.Sample(context, false, 40, 0, 910), false);
                Expect(gesture.Sample(context, true, 40, 0, 1000), true);
                Expect(gesture.Sample(context, false, 40, 0, 1050), true, GestureIntent.Place);
            }
        });
        yield return ("focus or location reset requires a fresh press even for the same target", () =>
        {
            var gesture = Gesture();
            var context = Context();
            gesture.Sample(context, false, 0, 0, 0);
            gesture.Sample(context, true, 0, 0, 10);
            gesture.Reset();
            Expect(gesture.Sample(context, true, 20, 0, 500), false);
            Expect(gesture.Sample(context, false, 20, 0, 510), false);
            Expect(gesture.Sample(context, true, 20, 0, 600), true);
            Expect(gesture.Sample(context, false, 20, 0, 650), true, GestureIntent.Place);
        });
        yield return ("ending editing leaves the last committed orientation available to gameplay", () =>
        {
            var world = new PlacementPose(new TilePoint(10, 20), Facing.South);
            var draft = new PlacementDraft(world);
            var context = Context();
            var gesture = Gesture();
            void Apply(MoveGestureResult result)
            {
                if (result.Intent == GestureIntent.RotatePositive) draft.RotateSteps(1);
                if (result.Intent == GestureIntent.Place)
                    draft.TryPlace(candidate => { world = candidate; return true; });
            }
            Apply(gesture.Sample(context, false, 0, 0, 0));
            Apply(gesture.Sample(context, true, 0, 0, 10));
            Apply(gesture.Sample(context, true, 8, 0, 260));
            Apply(gesture.Sample(context, false, 8, 0, 300));
            Apply(gesture.Sample(context, true, 8, 0, 400));
            Apply(gesture.Sample(context, false, 8, 0, 450));
            Equal(PlacementState.Placed, draft.State);
            Equal(Facing.East, world.Direction);
            gesture.Reset();
            Apply(gesture.Sample(null, true, 0, 0, 500));
            Apply(gesture.Sample(null, true, 20, 0, 900));
            Apply(gesture.Sample(null, false, 20, 0, 910));
            Equal(Facing.East, world.Direction);
            var doorway = new Doorway(new Footprint(4, 7), world.Direction, new TilePoint(3, 5));
            Equal(new TilePoint(14, 25), doorway.WorldApproach(world.Origin));
        });
    }
}
