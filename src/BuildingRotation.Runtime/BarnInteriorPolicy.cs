using System.Collections.Generic;
using System.Linq;
using BuildingRotation.Core;

namespace BuildingRotation.Runtime
{
    /// <summary>Room-object check for the empty Barn prototype; never moves or removes contents.</summary>
    public static class BarnInteriorPolicy
    {
        // The game adds a fixed Feed Hopper when constructing even an otherwise empty Barn.
        // Read its tile from the live building data, not from a hard-coded room layout.
        public static bool HasOnlyBuiltInObjects(
            IEnumerable<(TilePoint Tile, string QualifiedItemId, bool Indestructible)> objects,
            TilePoint? fixedFeedHopperTile)
            => objects.All(item => fixedFeedHopperTile.HasValue
                && item.Tile == fixedFeedHopperTile.Value
                && item.QualifiedItemId == "(BC)99" && item.Indestructible);
    }
}
