namespace Dungeon.Sim
{
    public sealed class GameMap
    {
        public readonly int Width, Height;
        readonly bool[] _walls;
        readonly Entity[] _occupant;

        public GameMap(int width, int height)
        {
            Width = width;
            Height = height;
            _walls = new bool[width * height];
            _occupant = new Entity[width * height];
            for (int i = 0; i < _occupant.Length; i++) _occupant[i] = Entity.None;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        int Index(int x, int y) => y * Width + x;

        public bool IsWall(int x, int y) => !InBounds(x, y) || _walls[Index(x, y)];
        public void SetWall(int x, int y, bool wall) { if (InBounds(x, y)) _walls[Index(x, y)] = wall; }

        public Entity OccupantAt(int x, int y) => InBounds(x, y) ? _occupant[Index(x, y)] : Entity.None;
        internal void SetOccupant(int x, int y, Entity e) { if (InBounds(x, y)) _occupant[Index(x, y)] = e; }

        public bool IsFree(int x, int y) => !IsWall(x, y) && OccupantAt(x, y).IsNone;
    }
}
