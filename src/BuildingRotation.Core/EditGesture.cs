using System;

namespace BuildingRotation.Core
{
    // Positive/negative describe screen X movement, NOT a fixed facing order.
    public enum GestureIntent { None, Place, RotatePositive, RotateNegative }

    // Feed screen pixels and monotonic real elapsed milliseconds (not game time).
    // No default thresholds: these are prototype parameters supplied by the caller.
    // Low-level recognizer; game input should go through MoveModeGesture's gate.
    public sealed class EditGesture
    {
        private readonly long holdMilliseconds;
        private readonly int dragPixels;
        private int pressX;
        private int pressY;
        private long startedAt;
        private long lastTimestamp;
        private bool dragged;
        private bool rotated;

        public bool IsPressed { get; private set; }

        public EditGesture(long holdMilliseconds, int dragPixels)
        {
            if (holdMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(holdMilliseconds));
            if (dragPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(dragPixels));
            this.holdMilliseconds = holdMilliseconds;
            this.dragPixels = dragPixels;
        }

        public void Begin(int screenX, int screenY, long timestampMilliseconds)
        {
            if (IsPressed)
                throw new InvalidOperationException("A pointer gesture is already active.");
            if (timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
            pressX = screenX;
            pressY = screenY;
            startedAt = lastTimestamp = timestampMilliseconds;
            dragged = rotated = false;
            IsPressed = true;
        }

        // Call each update tick, even when the mouse is stationary. A qualifying
        // horizontal drag emits one rotation, at most once per press/release pair.
        public GestureIntent Update(int screenX, int screenY, long timestampMilliseconds)
        {
            if (!IsPressed)
                return GestureIntent.None;
            if (timestampMilliseconds < lastTimestamp)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds), "Time must be monotonic.");
            lastTimestamp = timestampMilliseconds;

            long dx = (long)screenX - pressX;
            long dy = (long)screenY - pressY;
            long distanceX = Math.Abs(dx);
            long distanceY = Math.Abs(dy);
            dragged |= distanceX >= dragPixels || distanceY >= dragPixels;

            if (rotated || timestampMilliseconds - startedAt < holdMilliseconds
                || distanceX < dragPixels || distanceX <= distanceY)
                return GestureIntent.None;

            rotated = true;
            return dx > 0 ? GestureIntent.RotatePositive : GestureIntent.RotateNegative;
        }

        public GestureIntent Release(int screenX, int screenY, long timestampMilliseconds)
        {
            if (!IsPressed)
                return GestureIntent.None;

            GestureIntent intent = Update(screenX, screenY, timestampMilliseconds);
            if (intent == GestureIntent.None && !rotated && !dragged
                && timestampMilliseconds - startedAt < holdMilliseconds)
                intent = GestureIntent.Place;

            Reset();
            return intent;
        }

        // Also use on cancellation, move-mode/location changes and loss of focus.
        // A late mouse-up after reset must not commit the discarded preview.
        public void Reset()
        {
            IsPressed = false;
            dragged = rotated = false;
        }
    }
}
