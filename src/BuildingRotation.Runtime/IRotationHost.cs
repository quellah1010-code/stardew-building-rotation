using System.Collections.Generic;
using BuildingRotation.Core;

namespace BuildingRotation.Runtime
{
    // This is the unimplemented game boundary, not a claim of SMAPI/Harmony integration.
    // Call synchronously on the game thread. Never run these operations concurrently.
    public interface IRotationHost
    {
        BuildingSnapshot Read(string buildingId);

        // Replaces the target's old footprint with candidate for the query. Check the whole
        // footprint, both additional-placement categories, map bounds, terrain, objects,
        // characters, other buildings and game placement rules. Do not ignore other buildings.
        bool CanPlace(BuildingSnapshot current, BuildingLayout candidate, out string reason);

        // All world collision EXCEPT this building's old geometry; the runtime composes
        // candidate geometry itself. Includes location bounds and actors, not just one corner.
        bool CanOccupy(BuildingSnapshot current, PixelRectangle worldBounds);

        // Recheck revision, eligibility and world placement at write time. Atomically apply
        // origin, footprint, required door/warp caches and modData. On false or exception leave
        // everything unchanged; a concrete adapter MUST implement rollback, not just return false.
        bool TryApply(BuildingSnapshot expected, BuildingLayout candidate,
            IReadOnlyDictionary<string, string> proposedModData, out string reason);
    }
}
