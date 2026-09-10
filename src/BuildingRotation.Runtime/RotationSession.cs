using System;
using BuildingRotation.Core;

namespace BuildingRotation.Runtime
{
    public sealed class RotationSession
    {
        private readonly IRotationHost host;
        private readonly BuildingSnapshot original;
        private readonly PlacementDraft draft;
        public PlacementState State => draft.State;
        public BuildingLayout Preview => original.Definition.CreateLayout(draft.Preview);
        public string LastRejection { get; private set; } = "";

        // Created only AFTER a recognized move provider has acquired input ownership.
        public RotationSession(IRotationHost host, string buildingId)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            original = host.Read(buildingId);
            if (!original.SupportsPrototype) throw new NotSupportedException("Only a ready, empty ordinary Barn is supported.");
            draft = new PlacementDraft(original.Pose);
        }

        public void MoveTo(TilePoint origin) => draft.MoveTo(origin);
        public void RotateSteps(int steps) => draft.RotateSteps(steps);
        public void Cancel() => draft.Cancel(); // Preview never wrote to the host.

        public bool Validate(int pixelsPerTile)
        {
            if (State != PlacementState.Editing) throw new InvalidOperationException("Session is no longer editing.");
            return ValidateCurrent(Preview, pixelsPerTile);
        }

        private bool ValidateCurrent(BuildingLayout candidate, int pixelsPerTile)
        {
            if (pixelsPerTile <= 0) throw new ArgumentOutOfRangeException(nameof(pixelsPerTile));
            BuildingSnapshot current = host.Read(original.Id);
            if (current.Revision != original.Revision || !current.Pose.Equals(original.Pose)
                || current.ExteriorId != original.ExteriorId || current.InteriorId != original.InteriorId
                || !current.SupportsPrototype)
            { LastRejection = "Building changed after pickup; start a new move."; return false; }
            if (!host.CanPlace(current, candidate, out string reason))
            { LastRejection = reason; return false; }
            if (!candidate.LocalDoors.TryGetValue("human", out DoorRegion door))
            { LastRejection = "Human entrance is missing."; return false; }
            PixelRectangle approach = door.WorldApproach(candidate.Pose.Origin, 1).ToPixels(pixelsPerTile);
            if (candidate.IntersectsWorld(approach, pixelsPerTile) || !host.CanOccupy(current, approach))
            { LastRejection = "Entrance approach is blocked."; return false; }
            LastRejection = "";
            return true;
        }

        public bool TryPlace(int pixelsPerTile)
        {
            return draft.TryPlace(pose =>
            {
                BuildingLayout layout = original.Definition.CreateLayout(pose);
                if (!ValidateCurrent(layout, pixelsPerTile)) return false;
                bool applied = host.TryApply(original, layout,
                    FacingData.WithFacing(original.ModData, pose.Direction), out string reason);
                LastRejection = applied ? "" : reason;
                return applied;
            });
        }
    }
}
