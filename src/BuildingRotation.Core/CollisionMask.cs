using System;
using System.Collections.Generic;

namespace BuildingRotation.Core
{
    // Immutable south-facing, footprint-local blocking data. This is not world passability.
    public sealed class CollisionMask
    {
        // Null represents a solid footprint without allocating one entry per tile.
        private readonly HashSet<TilePoint>? blocked;
        public Footprint Footprint { get; }

        public CollisionMask(Footprint footprint, IEnumerable<TilePoint> blockedCells)
        {
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            if (blockedCells is null)
                throw new ArgumentNullException(nameof(blockedCells));
            blocked = new HashSet<TilePoint>();
            foreach (TilePoint cell in blockedCells)
            {
                if (!footprint.Contains(cell))
                    throw new ArgumentOutOfRangeException(nameof(blockedCells), "Blocking cells must be inside the source footprint.");
                blocked.Add(cell);
            }
        }

        private CollisionMask(Footprint footprint)
        {
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
        }

        public static CollisionMask Solid(Footprint footprint) => new CollisionMask(footprint);

        // Strict normalized rows: X blocks, O is unblocked by this building.
        // This helper is not the game's CollisionMap parser: no padding, trimming or unknown tokens.
        public static CollisionMask FromRows(params string[] rows)
        {
            if (rows is null)
                throw new ArgumentNullException(nameof(rows));
            if (rows.Length == 0 || string.IsNullOrEmpty(rows[0]))
                throw new ArgumentException("Provide at least one nonempty row.", nameof(rows));
            var footprint = new Footprint(rows[0].Length, rows.Length);
            var cells = new List<TilePoint>();
            for (int y = 0; y < rows.Length; y++)
            {
                string row = rows[y];
                if (row is null || row.Length != footprint.Width)
                    throw new ArgumentException("Every row must match the first row's width.", nameof(rows));
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x] == 'X')
                        cells.Add(new TilePoint(x, y));
                    else if (row[x] != 'O')
                        throw new ArgumentException("Only X and O are valid normalized cells.", nameof(rows));
                }
            }
            return new CollisionMask(footprint, cells);
        }

        // False outside the footprint means this mask contributes no obstruction there.
        public bool IsBlocked(TilePoint sourceCell) =>
            Footprint.Contains(sourceCell) && (blocked is null || blocked.Contains(sourceCell));
    }
}
