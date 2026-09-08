using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BuildingRotation.Core
{
    // Share this immutable source definition; never store a building instance's facing here.
    public sealed class BuildingLayoutDefinition
    {
        public CollisionMask Collision { get; }
        public IReadOnlyDictionary<string, DoorRegion> SourceDoors { get; }
        public IReadOnlyList<TileRectangle> SourceClearance { get; }
        private readonly Dictionary<Facing, IReadOnlyDictionary<string, DoorRegion>> doorOverrides;

        public BuildingLayoutDefinition(CollisionMask collision,
            IReadOnlyDictionary<string, DoorRegion>? sourceDoors = null,
            IEnumerable<TileRectangle>? sourceClearance = null,
            IReadOnlyDictionary<Facing, IReadOnlyDictionary<string, DoorRegion>>? orientedDoorOverrides = null)
        {
            Collision = collision ?? throw new ArgumentNullException(nameof(collision));
            SourceDoors = CopyDoors(sourceDoors ?? new Dictionary<string, DoorRegion>());
            var clearance = new List<TileRectangle>();
            if (sourceClearance != null)
                foreach (TileRectangle area in sourceClearance)
                    clearance.Add(area ?? throw new ArgumentException("Clearance areas cannot be null.", nameof(sourceClearance)));
            SourceClearance = clearance.AsReadOnly();

            doorOverrides = new Dictionary<Facing, IReadOnlyDictionary<string, DoorRegion>>();
            if (orientedDoorOverrides != null)
            {
                foreach (var entry in orientedDoorOverrides)
                {
                    FacingExtensions.Validate(entry.Key);
                    var copy = CopyDoors(entry.Value ?? throw new ArgumentException("Door overrides cannot be null.", nameof(orientedDoorOverrides)));
                    foreach (string id in copy.Keys)
                        if (!SourceDoors.ContainsKey(id))
                            throw new ArgumentException("An override must refer to an existing door ID.", nameof(orientedDoorOverrides));
                    doorOverrides.Add(entry.Key, copy);
                }
            }
        }

        private static IReadOnlyDictionary<string, DoorRegion> CopyDoors(IReadOnlyDictionary<string, DoorRegion> source)
        {
            var copy = new Dictionary<string, DoorRegion>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                    throw new ArgumentException("Each door needs a nonempty ID and a region.", nameof(source));
                copy.Add(pair.Key, pair.Value);
            }
            return new ReadOnlyDictionary<string, DoorRegion>(copy);
        }

        internal IReadOnlyDictionary<string, DoorRegion> OrientDoors(Facing facing)
        {
            var result = new Dictionary<string, DoorRegion>(StringComparer.Ordinal);
            doorOverrides.TryGetValue(facing, out var overrides);
            foreach (var pair in SourceDoors)
            {
                // Explicit art reconstruction coordinates are already oriented; don't rotate twice.
                result.Add(pair.Key, overrides != null && overrides.TryGetValue(pair.Key, out var replacement)
                    ? replacement
                    : pair.Value.RotateTo(Collision.Footprint, facing));
            }
            return new ReadOnlyDictionary<string, DoorRegion>(result);
        }

        public BuildingLayout CreateLayout(PlacementPose pose) => new BuildingLayout(this, pose);
    }
}
