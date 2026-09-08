using BuildingRotation.Core;

namespace BuildingRotation.Data
{
    // Raw content can contain zero-sized/negative sentinel rectangles. Do not lose them on read.
    public readonly struct ContentRectangle
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsAllZero => X == 0 && Y == 0 && Width == 0 && Height == 0;

        public ContentRectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public TileRectangle ToTileRectangle() => new TileRectangle(X, Y, Width, Height);
        public PixelRectangle ToPixelRectangle() => new PixelRectangle(X, Y, Width, Height);
    }
}
