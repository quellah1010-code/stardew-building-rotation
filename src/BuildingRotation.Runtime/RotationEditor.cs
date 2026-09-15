using System;
using BuildingRotation.Core;

namespace BuildingRotation.Runtime
{
    // The sampled button only rotates. Placement is a separate explicit provider action.
    // The provider suppresses the rotation button whenever IsHandled is true.
    public sealed class RotationEditor
    {
        private readonly IRotationHost host;
        private readonly MoveModeGesture gesture;
        private readonly int positiveDragSteps;
        private MoveModeContext? context;
        public RotationSession? Session { get; private set; }

        public RotationEditor(IRotationHost host, long holdMilliseconds, int dragPixels, int positiveDragSteps)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            if (positiveDragSteps != 1 && positiveDragSteps != -1)
                throw new ArgumentOutOfRangeException(nameof(positiveDragSteps));
            this.positiveDragSteps = positiveDragSteps;
            gesture = new MoveModeGesture(holdMilliseconds, dragPixels);
        }

        public MoveGestureResult Sample(MoveModeContext? current, TilePoint hoverOrigin,
            bool pointerDown, int screenX, int screenY, long milliseconds)
        {
            if (current != null && (!current.CanEdit || !host.Read(current.HeldBuildingId!).SupportsPrototype)) current = null;
            bool same = context == null ? current == null : current != null
                && context.ProviderId == current.ProviderId && context.SessionId == current.SessionId
                && context.HeldBuildingId == current.HeldBuildingId;
            if (!same)
            {
                if (Session?.State == PlacementState.Editing) Session.Cancel();
                context = current;
                Session = current == null ? null : new RotationSession(host, current.HeldBuildingId!);
            }
            // Even if the provider's observation lags, don't allow another edit/commit in
            // the just-completed session. A fresh provider session ID is required.
            var input = gesture.Sample(current, pointerDown, screenX, screenY, milliseconds);
            if (Session != null && Session.State != PlacementState.Editing) return input;
            if (Session == null) return input;
            Session.MoveTo(hoverOrigin);
            switch (input.Intent)
            {
                case GestureIntent.RotatePositive: Session.RotateSteps(positiveDragSteps); break;
                case GestureIntent.RotateNegative: Session.RotateSteps(-positiveDragSteps); break;
                // A short rotation-button click has no placement meaning.
            }
            return input;
        }

        public bool TryPlace(int pixelsPerTile)
            => Session?.State == PlacementState.Editing && Session.TryPlace(pixelsPerTile);

        // Focus loss, cancellation, location change or returned-to-title.
        public void Reset()
        {
            if (Session?.State == PlacementState.Editing) Session.Cancel();
            Session = null; context = null; gesture.Reset();
        }
    }
}
