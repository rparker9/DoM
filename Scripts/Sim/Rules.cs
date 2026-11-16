using System;

namespace Dungeon.Sim
{
    public static class Rules
    {
        // 8 directions, fixed order for deterministic AI scans.
        public static readonly int[] DirX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        public static readonly int[] DirY = { 1, 1, 0, -1, -1, -1, 0, 1 };

        public static int Chebyshev(int ax, int ay, int bx, int by) =>
            Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        /// <summary>Mystery Dungeon rule: no moving or attacking diagonally around a wall corner.</summary>
        public static bool CornerClear(GameMap map, int x, int y, int dx, int dy) =>
            dx == 0 || dy == 0 || (!map.IsWall(x + dx, y) && !map.IsWall(x, y + dy));

        public static bool CanStep(World w, Entity e, int dx, int dy)
        {
            if (dx == 0 && dy == 0) return false;
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1) return false;
            var p = w.Positions.Get(e.Id);
            return w.Map.IsFree(p.X + dx, p.Y + dy) && CornerClear(w.Map, p.X, p.Y, dx, dy);
        }

        public static bool IsHostile(World w, Entity a, Entity b) =>
            w.Factions.Has(a.Id) && w.Factions.Has(b.Id) &&
            w.Factions.Get(a.Id).Team != w.Factions.Get(b.Id).Team;
    }
}
