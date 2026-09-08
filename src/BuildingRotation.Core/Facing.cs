using System;

namespace BuildingRotation.Core
{
    // Entrance direction. This order is not a mouse-binding or clockwise promise.
    public enum Facing
    {
        South = 0,
        East = 1,
        North = 2,
        West = 3
    }

    public static class FacingExtensions
    {
        public static Facing RotateSteps(this Facing facing, int steps)
        {
            Validate(facing);
            return (Facing)(((int)facing + steps % 4 + 4) % 4);
        }

        public static TilePoint OutwardStep(this Facing facing)
        {
            return facing switch
            {
                Facing.South => new TilePoint(0, 1),
                Facing.East => new TilePoint(1, 0),
                Facing.North => new TilePoint(0, -1),
                Facing.West => new TilePoint(-1, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }

        internal static void Validate(Facing facing)
        {
            if ((int)facing < 0 || (int)facing > 3)
                throw new ArgumentOutOfRangeException(nameof(facing));
        }
    }
}
