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
        public IReadOnlyList<PlacementRequirement> SourcePlacementRequirements { get; }
        public int AdditionalTilePropertyRadius { get; }
        private readonly Dictionary<Facing, IReadOnlyDictionary<string, DoorRegion>> doorOverrides;
        private readonly Dictionary<Facing, CollisionMask> collisionOverrides;

        public BuildingLayoutDefinition(CollisionMask collision,
            IReadOnlyDictionary<string, DoorRegion>? sourceDoors = null,
            IEnumerable<TileRectangle>? sourceClearance = null,
            IReadOnlyDictionary<Facing, IReadOnlyDictionary<string, DoorRegion>>? orientedDoorOverrides = null,
            IEnumerable<PlacementRequirement>? sourcePlacementRequirements = null,
            int additionalTilePropertyRadius = 0)
            : this(collision, sourceDoors, sourceClearance, orientedDoorOverrides,
                sourcePlacementRequirements, additionalTilePropertyRadius, null) { }

        // Keep the original constructor for existing adapters. Overrides describe an
        // explicit art layout, in oriented coordinates, without changing reserved size.
        public BuildingLayoutDefinition(CollisionMask collision,
            IReadOnlyDictionary<string, DoorRegion>? sourceDoors,
            IEnumerable<TileRectangle>? sourceClearance,
            IReadOnlyDictionary<Facing, IReadOnlyDictionary<string, DoorRegion>>? orientedDoorOverrides,
            IEnumerable<PlacementRequirement>? sourcePlacementRequirements,
            int additionalTilePropertyRadius,
            IReadOnlyDictionary<Facing, CollisionMask>? orientedCollisionOverrides)
        {
            Collision = collision ?? throw new ArgumentNullException(nameof(collision));
            SourceDoors = CopyDoors(sourceDoors ?? new Dictionary<string, DoorRegion>());
            var clearance = new List<TileRectangle>();
            if (sourceClearance != null)
                foreach (TileRectangle area in sourceClearance)
                    clearance.Add(area ?? throw new ArgumentException("Clearance areas cannot be null.", nameof(sourceClearance)));
            SourceClearance = clearance.AsReadOnly();

            if (additionalTilePropertyRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(additionalTilePropertyRadius));
            AdditionalTilePropertyRadius = additionalTilePropertyRadius;
            var requirements = new List<PlacementRequirement>();
            if (sourcePlacementRequirements != null)
                foreach (var requirement in sourcePlacementRequirements)
                    requirements.Add(requirement ?? throw new ArgumentException("Placement requirements cannot be null.", nameof(sourcePlacementRequirements)));
            SourcePlacementRequirements = requirements.AsReadOnly();

            collisionOverrides = new Dictionary<Facing, CollisionMask>();
            if (orientedCollisionOverrides != null)
                foreach (var entry in orientedCollisionOverrides)
                {
                    FacingExtensions.Validate(entry.Key);
                    Footprint size = Collision.Footprint.RotateTo(entry.Key);
                    if (entry.Value == null || entry.Value.Footprint.Width != size.Width
                        || entry.Value.Footprint.Height != size.Height)
                        throw new ArgumentException("An oriented collision mask must match the reserved footprint.", nameof(orientedCollisionOverrides));
                    collisionOverrides.Add(entry.Key, entry.Value);
                }

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

        internal bool BlocksOriented(TilePoint cell, Facing facing)
            => collisionOverrides.TryGetValue(facing, out CollisionMask? mask)
                ? mask.IsBlocked(cell)
                : Collision.IsBlocked(Collision.Footprint.InverseTransformCell(cell, facing));
    }
}
