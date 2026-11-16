namespace Dungeon.Sim
{
    /// <summary>The tile grid: which tiles are walls and who stands where.</summary>
    public sealed class GameMap
    {
        public readonly int Width, Height;
        readonly bool[] _walls;
        readonly Entity[] _occupants;

        /// <param name="width">Tiles across.</param>
        /// <param name="height">Tiles down.</param>
        public GameMap(int width, int height)
        {
            Width = width;
            Height = height;
            _walls = new bool[width * height];
            _occupants = new Entity[width * height];
            for (int i = 0; i < _occupants.Length; i++) _occupants[i] = Entity.None;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        int Index(int x, int y) => y * Width + x;

        /// <summary>True for walls and for anything outside the map.</summary>
        public bool IsWall(int x, int y) => !InBounds(x, y) || _walls[Index(x, y)];

        /// <param name="x">Tile x.</param>
        /// <param name="y">Tile y.</param>
        /// <param name="wall">True to make it a wall, false to make it floor.</param>
        public void SetWall(int x, int y, bool wall)
        {
            if (InBounds(x, y)) _walls[Index(x, y)] = wall;
        }

        /// <returns>Who stands on the tile, or Entity.None.</returns>
        public Entity OccupantAt(int x, int y) => InBounds(x, y) ? _occupants[Index(x, y)] : Entity.None;

        /// <summary>Only World calls this, so position and occupancy never disagree.</summary>
        internal void SetOccupant(int x, int y, Entity e)
        {
            if (InBounds(x, y)) _occupants[Index(x, y)] = e;
        }

        /// <summary>Not a wall and nobody standing there.</summary>
        public bool IsFree(int x, int y) => !IsWall(x, y) && OccupantAt(x, y).IsNone;
    }
}
