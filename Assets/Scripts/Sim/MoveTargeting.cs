using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>Works out who a move affects and where it lands, without changing anything.</summary>
    public static class MoveTargeting
    {
        /// <summary>Finds everyone the move would affect.</summary>
        /// <param name="w">The world.</param>
        /// <param name="user">Who uses the move.</param>
        /// <param name="move">The move.</param>
        /// <param name="dx">Direction x (-1, 0, 1). Only Line moves use it.</param>
        /// <param name="dy">Direction y (-1, 0, 1). Only Line moves use it.</param>
        /// <param name="into">Cleared, then filled with everyone affected.</param>
        public static void FindTargets(World w, Entity user, MoveDef move, int dx, int dy, List<Entity> into) =>
            Resolve(w, user, move, dx, dy, into, out _, out _);

        /// <summary>Finds everyone the move would affect, and the tile it lands on.</summary>
        /// <param name="w">The world.</param>
        /// <param name="user">Who uses the move.</param>
        /// <param name="move">The move.</param>
        /// <param name="dx">Direction x (-1, 0, 1). Only Line moves use it.</param>
        /// <param name="dy">Direction y (-1, 0, 1). Only Line moves use it.</param>
        /// <param name="into">Cleared, then filled with everyone affected.</param>
        /// <param name="impactX">Where it lands: the first actor or wall hit, the end of the range, or the user.</param>
        /// <param name="impactY">See impactX.</param>
        public static void Resolve(World w, Entity user, MoveDef move, int dx, int dy, List<Entity> into,
                                   out int impactX, out int impactY)
        {
            into.Clear();
            var p = w.Positions.Get(user.Id);
            impactX = p.X;
            impactY = p.Y;

            switch (move.Area)
            {
                case MoveArea.Self:
                    into.Add(user);
                    return;

                case MoveArea.Team:
                    for (int i = 0; i < w.Factions.Count; i++)
                    {
                        var other = w.HandleOf(w.Factions.IdAt(i));
                        if (!Rules.IsHostile(w, user, other) && Matches(w, user, move.Filter, other)) into.Add(other);
                    }
                    return;

                case MoveArea.Around:
                    AddArea(w, user, move, p.X, p.Y, Math.Max(1, move.Range), into);
                    return;
            }

            // Line: walk forward until something stops it.
            if (dx == 0 && dy == 0) return;
            if (!Rules.CornerClear(w.Map, p.X, p.Y, dx, dy)) return;   // fizzles against the corner

            int x = p.X, y = p.Y;
            for (int step = 1; step <= Math.Max(1, move.Range); step++)
            {
                x += dx; y += dy;
                impactX = x; impactY = y;
                if (w.Map.IsWall(x, y)) break;

                var other = w.Map.OccupantAt(x, y);
                if (other.IsNone) continue;
                if (Matches(w, user, move.Filter, other)) into.Add(other);
                if (!move.Pierce) break;   // stops at the first actor, friend or foe
            }

            // A burst replaces the line's own hits with everyone around the impact tile.
            if (move.BurstRadius > 0)
            {
                into.Clear();
                AddArea(w, user, move, impactX, impactY, move.BurstRadius, into);
            }
        }

        /// <summary>Adds everyone within radius of (cx, cy) that the filter allows, in a fixed order.</summary>
        static void AddArea(World w, Entity user, MoveDef move, int cx, int cy, int radius, List<Entity> into)
        {
            for (int oy = -radius; oy <= radius; oy++)
            for (int ox = -radius; ox <= radius; ox++)
            {
                // Right next to the center, wall corners still block (the Mystery Dungeon rule).
                bool adjacent = Math.Max(Math.Abs(ox), Math.Abs(oy)) == 1;
                if (adjacent && !Rules.CornerClear(w.Map, cx, cy, ox, oy)) continue;

                var other = w.Map.OccupantAt(cx + ox, cy + oy);
                if (!other.IsNone && Matches(w, user, move.Filter, other)) into.Add(other);
            }
        }

        /// <summary>True if the filter lets this move affect other.</summary>
        static bool Matches(World w, Entity user, TargetFilter filter, Entity other)
        {
            bool enemy = Rules.IsHostile(w, user, other);
            bool self = other == user;
            switch (filter)
            {
                case TargetFilter.Enemies: return enemy;
                case TargetFilter.Allies: return !enemy && !self;
                case TargetFilter.AlliesAndSelf: return !enemy;
                default: return !self;   // Everyone
            }
        }
    }
}
