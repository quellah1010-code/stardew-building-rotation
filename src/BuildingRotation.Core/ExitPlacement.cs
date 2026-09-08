namespace BuildingRotation.Core
{
    // A validated candidate only; no actor or game state is changed by computing it.
    public sealed class ExitPlacement
    {
        public PixelPoint Position { get; }
        public PixelRectangle Bounds { get; }
        public Facing OutwardDirection { get; }

        internal ExitPlacement(PixelPoint position, PixelRectangle bounds, Facing outwardDirection)
        {
            Position = position;
            Bounds = bounds;
            OutwardDirection = outwardDirection;
        }
    }
}
