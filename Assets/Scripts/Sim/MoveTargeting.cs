using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>Works out who a move would hit. Used to resolve moves, and by the AI to choose them.</summary>
    public static class MoveTargeting
    {
        /// <summary>Finds every enemy the move would hit.</summary>
        /// <param name="w">The world.</param>
        /// <param name="user">Who uses the move.</param>
        /// <param name="move">The move.</param>
        /// <param name="dx">Direction x (-1, 0, 1). Ignored by AllAround moves.</param>
        /// <param name="dy">Direction y (-1, 0, 1). Ignored by AllAround moves.</param>
        /// <param name="into">Cleared, then filled with the enemies hit.</param>
        public static void FindTargets(World w, Entity user, MoveDef move, int dx, int dy, List<Entity> into)
        {
            into.Clear();
            var p = w.Positions.Get(user.Id);

            if (move.Shape == MoveShape.AllAround)
            {
                for (int i = 0; i < 8; i++)
                    AddIfEnemy(w, user, p.X, p.Y, Rules.DirX[i], Rules.DirY[i], into);
                return;
            }

            // Line: walk forward until something blocks it.
            if (dx == 0 && dy == 0) return;
            if (!Rules.CornerClear(w.Map, p.X, p.Y, dx, dy)) return;

            int x = p.X, y = p.Y;
            for (int step = 1; step <= Math.Max(1, move.Range); step++)
            {
                x += dx; y += dy;
                if (w.Map.IsWall(x, y)) return;
                var other = w.Map.OccupantAt(x, y);
                if (other.IsNone) continue;
                if (Rules.IsHostile(w, user, other)) into.Add(other);
                return;   // stops at the first actor, friend or foe
            }
        }

        /// <summary>Adds whoever stands one tile away in (dx, dy), if it's an enemy and no wall corner is in the way.</summary>
        static void AddIfEnemy(World w, Entity user, int x, int y, int dx, int dy, List<Entity> into)
        {
            if (!Rules.CornerClear(w.Map, x, y, dx, dy)) return;
            var other = w.Map.OccupantAt(x + dx, y + dy);
            if (!other.IsNone && Rules.IsHostile(w, user, other)) into.Add(other);
        }
    }
}
