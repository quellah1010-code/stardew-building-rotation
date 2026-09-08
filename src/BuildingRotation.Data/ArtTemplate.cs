using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BuildingRotation.Core;

namespace BuildingRotation.Data
{
    // Exact ground-plane guides for NEW artwork; not a rotated bitmap or confirmed runtime draw transform.
    public sealed class ArtDoorGuide
    {
        public PixelRectangle Area { get; }
        public PixelRectangle Approach { get; }
        public PixelPoint Threshold { get; }
        public Facing Direction { get; }

        internal ArtDoorGuide(DoorRegion door, int tilePixels, int topMargin)
        {
            Direction = door.Direction;
            Area = InCanvas(door.Area, tilePixels, topMargin);
            Approach = InCanvas(door.ApproachArea(1), tilePixels, topMargin);
            switch (Direction)
            {
                case Facing.South: Threshold = new PixelPoint(Area.X + Area.Width / 2, Area.Bottom); break;
                case Facing.East: Threshold = new PixelPoint(Area.Right, Area.Y + Area.Height / 2); break;
                case Facing.North: Threshold = new PixelPoint(Area.X + Area.Width / 2, Area.Y); break;
                case Facing.West: Threshold = new PixelPoint(Area.X, Area.Y + Area.Height / 2); break;
                default: throw new ArgumentOutOfRangeException(nameof(door));
            }
        }

        internal static PixelRectangle InCanvas(TileRectangle area, int tilePixels, int topMargin) =>
            new PixelRectangle((double)area.X * tilePixels, (double)area.Y * tilePixels + topMargin,
                (double)area.Width * tilePixels, (double)area.Height * tilePixels);
    }

    public sealed class ArtTemplate
    {
        public Facing Facing { get; }
        public int TilePixels { get; }
        public int CanvasWidth { get; }
        public int CanvasHeight { get; }
        public PixelPoint GroundOrigin { get; }
        public PixelRectangle GroundFootprint { get; }
        public IReadOnlyDictionary<string, ArtDoorGuide> Doors { get; }

        public ArtTemplate(BuildingLayout layout, int tilePixels, int topMargin)
        {
            if (layout is null)
                throw new ArgumentNullException(nameof(layout));
            if (tilePixels <= 0 || topMargin < 0)
                throw new ArgumentOutOfRangeException(nameof(tilePixels));
            Facing = layout.Pose.Direction;
            TilePixels = tilePixels;
            CanvasWidth = checked(layout.Footprint.Width * tilePixels);
            CanvasHeight = checked(layout.Footprint.Height * tilePixels + topMargin);
            GroundOrigin = new PixelPoint(0, topMargin);
            GroundFootprint = new PixelRectangle(0, topMargin, CanvasWidth, CanvasHeight - topMargin);
            var doors = new Dictionary<string, ArtDoorGuide>(StringComparer.Ordinal);
            foreach (var pair in layout.LocalDoors)
                doors.Add(pair.Key, new ArtDoorGuide(pair.Value, tilePixels, topMargin));
            Doors = new ReadOnlyDictionary<string, ArtDoorGuide>(doors);
        }
    }
}
