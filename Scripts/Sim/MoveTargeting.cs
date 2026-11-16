using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>Works out who a move would hit. Used both to resolve moves and by the AI to choose them.</summary>
    public static class MoveTargeting
    {
        /// <summary>
        /// Fills <paramref name="into"/> with the enemies <paramref name="user"/> would hit
        /// by using <paramref name="move"/> facing (dx, dy). AllAround moves ignore the direction.
        /// </summary>
        public static void FindTargets(World w, Entity user, MoveDef move, int dx, int dy, List<Entity> into)
        {
            into.Clear();
            var p = w.Positions.Get(user.Id);

            if (move.Shape == MoveShape.AllAround)
            {
                for (int i = 0; i < 8; i++)
                {
                    int ddx = Rules.DirX[i], ddy = Rules.DirY[i];
                    if (!Rules.CornerClear(w.Map, p.X, p.Y, ddx, ddy)) continue;
                    var other = w.Map.OccupantAt(p.X + ddx, p.Y + ddy);
                    if (!other.IsNone && Rules.IsHostile(w, user, other)) into.Add(other);
                }
                return;
            }

            // Line: walk forward until something blocks it.
            if (dx == 0 && dy == 0) return;
            if (!Rules.CornerClear(w.Map, p.X, p.Y, dx, dy)) return;

            int x = p.X, y = p.Y;
            int range = Math.Max(1, move.Range);
            for (int step = 1; step <= range; step++)
            {
                x += dx; y += dy;
                if (w.Map.IsWall(x, y)) return;
                var other = w.Map.OccupantAt(x, y);
                if (other.IsNone) continue;
                if (Rules.IsHostile(w, user, other)) into.Add(other);
                return;   // the line stops at the first entity, friend or foe
            }
        }
    }
}
