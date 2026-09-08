using System;

namespace BuildingRotation.Core
{
    public enum PlacementState { Editing, Applying, Placed, Cancelled }

    // One placement transaction. It never directly changes a game building.
    public sealed class PlacementDraft
    {
        public PlacementPose Original { get; }
        public PlacementPose Preview { get; private set; }
        public PlacementState State { get; private set; } = PlacementState.Editing;

        public PlacementDraft(PlacementPose original)
        {
            Original = original;
            Preview = original;
        }

        public void MoveTo(TilePoint origin)
        {
            RequireEditing();
            Preview = new PlacementPose(origin, Preview.Direction);
        }

        public void RotateSteps(int steps)
        {
            RequireEditing();
            Preview = new PlacementPose(Preview.Origin, Preview.Direction.RotateSteps(steps));
        }

        // The adapter must validate AND apply atomically. On false/exception it
        // must leave the game unchanged; this class cannot roll back external writes.
        public bool TryPlace(Func<PlacementPose, bool> tryApply)
        {
            RequireEditing();
            if (tryApply is null)
                throw new ArgumentNullException(nameof(tryApply));

            State = PlacementState.Applying;
            try
            {
                bool applied = tryApply(Preview);
                State = applied ? PlacementState.Placed : PlacementState.Editing;
                return applied;
            }
            catch
            {
                State = PlacementState.Editing;
                throw;
            }
        }

        public PlacementPose Cancel()
        {
            if (State == PlacementState.Cancelled)
                return Original;
            RequireEditing();
            Preview = Original;
            State = PlacementState.Cancelled;
            return Original;
        }

        private void RequireEditing()
        {
            if (State != PlacementState.Editing)
                throw new InvalidOperationException($"Cannot edit a draft in state {State}.");
        }
    }
}
