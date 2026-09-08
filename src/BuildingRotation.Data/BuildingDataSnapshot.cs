using System;
using System.Collections.Generic;
using BuildingRotation.Core;

namespace BuildingRotation.Data
{
    // A copied, immutable subset of content fields. This is not a mutable game BuildingData.
    public sealed class BuildingDataSnapshot
    {
        public string Id { get; }
        public Footprint Footprint { get; }
        public ContentCollisionMap Collision { get; }
        public TilePoint HumanDoor { get; }
        public ContentRectangle AnimalDoor { get; }
        public IReadOnlyList<PlacementRequirement> AdditionalPlacementTiles { get; }
        public int AdditionalTilePropertyRadius { get; }
        public string? IndoorMap { get; }
        public string? BuildingToUpgrade { get; }
        public SpriteDataSnapshot Sprite { get; }

        public BuildingDataSnapshot(string id, Footprint footprint, string? collisionMap,
            TilePoint humanDoor, ContentRectangle animalDoor, SpriteDataSnapshot sprite,
            IEnumerable<PlacementRequirement>? additionalPlacementTiles = null,
            int additionalTilePropertyRadius = 0, string? indoorMap = null, string? buildingToUpgrade = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A building ID is required.", nameof(id));
            if (additionalTilePropertyRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(additionalTilePropertyRadius));
            Id = id;
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            Collision = new ContentCollisionMap(footprint, collisionMap);
            HumanDoor = humanDoor;
            AnimalDoor = animalDoor;
            Sprite = sprite ?? throw new ArgumentNullException(nameof(sprite));
            AdditionalTilePropertyRadius = additionalTilePropertyRadius;
            IndoorMap = indoorMap;
            BuildingToUpgrade = buildingToUpgrade;
            var requirements = new List<PlacementRequirement>();
            if (additionalPlacementTiles != null)
                foreach (var item in additionalPlacementTiles)
                    requirements.Add(item ?? throw new ArgumentException("Null placement requirement.", nameof(additionalPlacementTiles)));
            AdditionalPlacementTiles = requirements.AsReadOnly();
        }

        public BuildingLayoutDefinition ToLayoutDefinition(
            IReadOnlyDictionary<Facing, IReadOnlyDictionary<string, DoorRegion>>? orientedDoorOverrides = null)
        {
            var collision = Collision.ToCoreMask();
            var doors = new Dictionary<string, DoorRegion>(StringComparer.Ordinal);
            if (HumanDoor != new TilePoint(-1, -1))
            {
                if (HumanDoor.X < 0 || HumanDoor.Y < 0)
                    throw new NotSupportedException("Unrecognized human-door sentinel.");
                doors.Add("human", new DoorRegion(new TileRectangle(HumanDoor.X, HumanDoor.Y, 1, 1), Facing.South));
            }
            bool animalDisabled = AnimalDoor.X == -1 && AnimalDoor.Y == -1 && AnimalDoor.Width == 0 && AnimalDoor.Height == 0;
            if (!animalDisabled)
            {
                if (AnimalDoor.X < 0 || AnimalDoor.Y < 0 || AnimalDoor.Width <= 0 || AnimalDoor.Height <= 0)
                    throw new NotSupportedException("Unrecognized animal-door sentinel or dimensions.");
                doors.Add("animal", new DoorRegion(AnimalDoor.ToTileRectangle(), Facing.South));
            }
            return new BuildingLayoutDefinition(collision, doors, orientedDoorOverrides: orientedDoorOverrides,
                sourcePlacementRequirements: AdditionalPlacementTiles, additionalTilePropertyRadius: AdditionalTilePropertyRadius);
        }
    }
}
