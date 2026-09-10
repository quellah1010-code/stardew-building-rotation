using System;
using System.Collections.Generic;
using BuildingRotation.Core;

namespace BuildingRotation.Runtime
{
    // Intended for each game's Building.modData. Coordinates stay owned by the game.
    public static class FacingData
    {
        public const string Key = "quellah.BuildingRotation/facing";

        public static Facing Read(IReadOnlyDictionary<string, string> modData)
        {
            if (modData is null) throw new ArgumentNullException(nameof(modData));
            if (!modData.TryGetValue(Key, out string value)) return Facing.South;
            return value switch
            {
                "1:south" => Facing.South,
                "1:east" => Facing.East,
                "1:north" => Facing.North,
                "1:west" => Facing.West,
                _ => throw new NotSupportedException("Unknown building orientation data: " + value)
            };
        }

        // Build a proposed replacement; do not mutate live modData before placement succeeds.
        public static IReadOnlyDictionary<string, string> WithFacing(
            IReadOnlyDictionary<string, string> original, Facing facing)
        {
            string value = facing switch
            {
                Facing.South => "1:south", Facing.East => "1:east",
                Facing.North => "1:north", Facing.West => "1:west",
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in original) copy.Add(pair.Key, pair.Value);
            copy[Key] = value;
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(copy);
        }
    }
}
