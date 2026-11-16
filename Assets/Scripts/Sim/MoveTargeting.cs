using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>This static class finds the targets of a move (who it would affect) without actually doing any damage or healing.</summary>
    public static class MoveTargeting
    {
        /// <summary>Finds everyone the move would affect: enemies for damage moves, allies for heal moves.</summary>
        /// <param name="w">The world.</param>
        /// <param name="user">Who uses the move.</param>
        /// <param name="move">The move.</param>
        /// <param name="dx">Direction x (-1, 0, 1). Ignored by AllAround moves.</param>
        /// <param name="dy">Direction y (-1, 0, 1). Ignored by AllAround moves.</param>
        /// <param name="into">Cleared, then filled with everyone affected.</param>
        public static void FindTargets(World w, Entity user, MoveDef move, int dx, int dy, List<Entity> into)
        {
            into.Clear();
            var p = w.Positions.Get(user.Id);

            if (move.Shape == MoveShape.AllAround)
            {
                if (move.Effect == MoveEffect.Heal) into.Add(user);   // an all-around heal includes the user
                for (int i = 0; i < 8; i++)
                    AddIfTarget(w, user, move, p.X, p.Y, Rules.DirX[i], Rules.DirY[i], into);
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
                if (IsTarget(w, user, move, other)) into.Add(other);
                return;   // stops at the first actor, friend or foe
            }
        }

        /// <summary>Damage moves affect enemies; heal moves affect allies.</summary>
        static bool IsTarget(World w, Entity user, MoveDef move, Entity other)
        {
            bool enemy = Rules.IsHostile(w, user, other);
            return move.Effect == MoveEffect.Heal ? !enemy : enemy;
        }

        /// <summary>Adds whoever stands one tile away in (dx, dy), if the move affects them and no wall corner is in the way.</summary>
        static void AddIfTarget(World w, Entity user, MoveDef move, int x, int y, int dx, int dy, List<Entity> into)
        {
            if (!Rules.CornerClear(w.Map, x, y, dx, dy)) return;
            var other = w.Map.OccupantAt(x + dx, y + dy);
            if (!other.IsNone && IsTarget(w, user, move, other)) into.Add(other);
        }
    }
}
