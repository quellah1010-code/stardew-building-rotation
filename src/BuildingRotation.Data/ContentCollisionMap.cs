using System;
using System.Collections.Generic;
using BuildingRotation.Core;

namespace BuildingRotation.Data
{
    // Parse the documented whitespace format separately from the strict core row helper.
    // Retain every explicit cell, including cells outside the nominal footprint.
    public sealed class ContentCollisionMap
    {
        public Footprint Footprint { get; }
        public bool UsesDefaultSolid { get; }
        public IReadOnlyList<string> Rows { get; }
        public IReadOnlyList<TilePoint> BlockedCells { get; }
        public IReadOnlyList<TilePoint> BlockedOutsideFootprint { get; }
        public bool HasExactFootprintRows { get; }

        public ContentCollisionMap(Footprint footprint, string? raw)
        {
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            UsesDefaultSolid = raw is null;
            var rows = new List<string>();
            var blocked = new List<TilePoint>();
            var outside = new List<TilePoint>();
            bool exact = true;
            if (raw != null)
            {
                string normalized = raw.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
                if (normalized.Length == 0)
                    throw new ArgumentException("An empty CollisionMap is ambiguous; use null for default solid.", nameof(raw));
                foreach (string line in normalized.Split('\n'))
                {
                    string row = line.Trim();
                    int y = rows.Count;
                    if (row.Length == 0)
                        throw new ArgumentException("Interior empty collision rows are unsupported.", nameof(raw));
                    rows.Add(row);
                    exact &= row.Length == footprint.Width;
                    for (int x = 0; x < row.Length; x++)
                    {
                        if (row[x] != 'X' && row[x] != 'O')
                            throw new ArgumentException("CollisionMap only supports X and O cells.", nameof(raw));
                        if (row[x] == 'X')
                        {
                            var cell = new TilePoint(x, y);
                            blocked.Add(cell);
                            if (!footprint.Contains(cell))
                                outside.Add(cell);
                        }
                    }
                }
                exact &= rows.Count == footprint.Height;
            }
            HasExactFootprintRows = exact;
            Rows = rows.AsReadOnly();
            BlockedCells = blocked.AsReadOnly();
            BlockedOutsideFootprint = outside.AsReadOnly();
        }

        public CollisionMask ToCoreMask()
        {
            if (BlockedOutsideFootprint.Count != 0)
                throw new NotSupportedException("CollisionMap contains blocking cells outside the footprint; mapping would discard collision.");
            if (!HasExactFootprintRows)
                throw new NotSupportedException("Ragged or non-footprint collision rows are preserved but not mapped; no implicit padding or clipping.");
            return UsesDefaultSolid ? CollisionMask.Solid(Footprint) : new CollisionMask(Footprint, BlockedCells);
        }
    }
}
