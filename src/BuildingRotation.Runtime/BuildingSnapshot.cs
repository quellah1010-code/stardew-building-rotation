using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BuildingRotation.Core;

namespace BuildingRotation.Runtime
{
    // A host captures this on the game thread. Revision must change on any state relevant
    // to placement, definition, occupants, pose, location, or modData; save changes invalidate IDs.
    public sealed class BuildingSnapshot
    {
        public string Id { get; }
        public long Revision { get; }
        public string ExteriorId { get; }
        public string InteriorId { get; }
        public string BuildingType { get; }
        public bool IsEmptyAndReady { get; }
        public BuildingLayoutDefinition Definition { get; }
        public PlacementPose Pose { get; }
        public IReadOnlyDictionary<string, string> ModData { get; }

        public BuildingSnapshot(string id, long revision, string exteriorId, string interiorId,
            string buildingType, bool isEmptyAndReady, BuildingLayoutDefinition definition,
            TilePoint origin, IReadOnlyDictionary<string, string> modData)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(exteriorId)
                || string.IsNullOrWhiteSpace(interiorId)) throw new ArgumentException("Location and instance identities are required.");
            Id = id; Revision = revision; ExteriorId = exteriorId; InteriorId = interiorId;
            BuildingType = buildingType; IsEmptyAndReady = isEmptyAndReady;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in modData) copy.Add(pair.Key, pair.Value);
            ModData = new ReadOnlyDictionary<string, string>(copy);
            Pose = new PlacementPose(origin, FacingData.Read(ModData));
        }

        public bool SupportsPrototype => BuildingType == "Barn" && IsEmptyAndReady;
        public BuildingLayout Layout => Definition.CreateLayout(Pose);
    }

    public sealed class WarpTarget
    {
        public string LocationId { get; }
        public PixelPoint Position { get; }
        public Facing Direction { get; }
        public WarpTarget(string locationId, PixelPoint position, Facing direction)
        { LocationId = locationId; Position = position; Direction = direction; }
    }
}
