using System;

namespace Dungeon.Sim
{
    /// <summary>Small shared rule checks.</summary>
    public static class Rules
    {
        // The 8 directions, starting up and going clockwise. A fixed order keeps the AI deterministic.
        public static readonly int[] DirX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        public static readonly int[] DirY = { 1, 1, 0, -1, -1, -1, 0, 1 };

        /// <summary>Distance in tiles when diagonal steps count as 1.</summary>
        public static int Chebyshev(int ax, int ay, int bx, int by) =>
            Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        /// <summary>A diagonal step or attack can't cut around a wall corner.</summary>
        /// <param name="map">The map.</param>
        /// <param name="x">Starting tile x.</param>
        /// <param name="y">Starting tile y.</param>
        /// <param name="dx">Direction x (-1, 0, 1).</param>
        /// <param name="dy">Direction y (-1, 0, 1).</param>
        public static bool CornerClear(GameMap map, int x, int y, int dx, int dy) =>
            dx == 0 || dy == 0 || (!map.IsWall(x + dx, y) && !map.IsWall(x, y + dy));

        /// <summary>True if the entity can walk one tile in this direction.</summary>
        /// <param name="w">The world.</param>
        /// <param name="e">Who is stepping.</param>
        /// <param name="dx">Direction x (-1, 0, 1).</param>
        /// <param name="dy">Direction y (-1, 0, 1).</param>
        public static bool CanStep(World w, Entity e, int dx, int dy)
        {
            if (dx == 0 && dy == 0) return false;
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1) return false;
            var p = w.Positions.Get(e.Id);
            return w.Map.IsFree(p.X + dx, p.Y + dy) && CornerClear(w.Map, p.X, p.Y, dx, dy);
        }

        /// <summary>True if the two entities are on different teams.</summary>
        public static bool IsHostile(World w, Entity a, Entity b) =>
            w.Factions.Has(a.Id) && w.Factions.Has(b.Id) &&
            w.Factions.Get(a.Id).Team != w.Factions.Get(b.Id).Team;
    }
}
