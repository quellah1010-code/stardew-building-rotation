using System;

namespace BuildingRotation.Core
{
    // An adapter's current observation, not a detector for game menus or other mods.
    public sealed class MoveModeContext
    {
        public string ProviderId { get; }
        public string SessionId { get; }
        public string? HeldBuildingId { get; }
        public bool IsMoving { get; }
        public bool SupportsRotation { get; }
        public bool OwnsGestureInput { get; }

        // Input ownership requires cooperation with the mover's placement handler;
        // seeing a menu or finding an installed mod does not establish ownership.
        public bool CanEdit => IsMoving && HeldBuildingId != null
            && SupportsRotation && OwnsGestureInput;

        public MoveModeContext(string providerId, string sessionId, string? heldBuildingId,
            bool isMoving, bool supportsRotation, bool ownsGestureInput)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                throw new ArgumentException("A provider identity is required.", nameof(providerId));
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A move session identity is required.", nameof(sessionId));
            if (heldBuildingId != null && string.IsNullOrWhiteSpace(heldBuildingId))
                throw new ArgumentException("Use null when no building is held.", nameof(heldBuildingId));

            ProviderId = providerId;
            SessionId = sessionId;
            HeldBuildingId = heldBuildingId;
            IsMoving = isMoving;
            SupportsRotation = supportsRotation;
            OwnsGestureInput = ownsGestureInput;
        }

        internal bool HasSameTarget(MoveModeContext other) =>
            string.Equals(ProviderId, other.ProviderId, StringComparison.Ordinal)
            && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
            && string.Equals(HeldBuildingId, other.HeldBuildingId, StringComparison.Ordinal);
    }
}
