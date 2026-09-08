namespace BuildingRotation.Core
{
    public readonly struct MoveGestureResult
    {
        // True also for a captured press/release that produces no command.
        // The adapter must prevent the mover from also acting on that input.
        public bool IsHandled { get; }
        public GestureIntent Intent { get; }

        internal MoveGestureResult(bool isHandled, GestureIntent intent)
        {
            IsHandled = isHandled;
            Intent = intent;
        }
    }

    // The game adapter's gesture entry point. No global activation key and no
    // dependencies on drawing, collision, doors, Reveal, or persisted orientation.
    public sealed class MoveModeGesture
    {
        private readonly EditGesture gesture;
        private MoveModeContext? activeContext;
        private bool pointerWasDown;

        public MoveModeGesture(long holdMilliseconds, int dragPixels)
        {
            gesture = new EditGesture(holdMilliseconds, dragPixels);
        }

        // Sample on pointer transitions AND each tick, using the current mode and
        // physical button state. null means no recognized cooperative move mode.
        // Sample once when selection changes, including the pickup event itself.
        public MoveGestureResult Sample(MoveModeContext? current, bool pointerIsDown,
            int screenX, int screenY, long timestampMilliseconds)
        {
            if (current != null && !current.CanEdit)
                current = null;

            bool sameTarget = activeContext == null
                ? current == null
                : current != null && activeContext.HasSameTarget(current);
            if (!sameTarget)
            {
                gesture.Reset();
                activeContext = current;
                pointerWasDown = pointerIsDown;
                // A press already down on entry belongs to pickup/the old owner.
                // Wait for a release and a fresh press; never inherit that gesture.
                return default;
            }

            bool wasDown = pointerWasDown;
            pointerWasDown = pointerIsDown;
            if (activeContext == null)
                return default;

            if (pointerIsDown)
            {
                if (!wasDown)
                    gesture.Begin(screenX, screenY, timestampMilliseconds);
                if (!gesture.IsPressed)
                    return default;
                return new MoveGestureResult(true,
                    gesture.Update(screenX, screenY, timestampMilliseconds));
            }

            bool wasHandled = gesture.IsPressed;
            return new MoveGestureResult(wasHandled,
                gesture.Release(screenX, screenY, timestampMilliseconds));
        }

        // Use on focus loss, cancellation, location/save changes, or loss of the
        // owning adapter. This discards input only; the adapter handles its draft.
        public void Reset()
        {
            gesture.Reset();
            activeContext = null;
            pointerWasDown = false;
        }
    }
}
