using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>
    /// Picks actions for everyone the player isn't controlling.
    /// Fighting always works the same way: use the first move in the list that can hit an enemy.
    /// </summary>
    public static class AiBrain
    {
        public const int MonsterSight = 6;     // monsters chase enemies within this many tiles
        public const int AllyEngageRange = 3;  // party members chase enemies within this many tiles...
        public const int AllyLeash = 4;        // ...while staying this close to the leader

        static readonly List<Entity> _targets = new List<Entity>();

        /// <summary>Chooses an action for an AI-controlled actor.</summary>
        /// <param name="w">The world.</param>
        /// <param name="self">The actor whose turn it is.</param>
        public static GameAction Decide(World w, Entity self)
        {
            bool isParty = w.Factions.Get(self.Id).Team == Team.Party;
            return isParty ? DecidePartyMember(w, self) : DecideMonster(w, self);
        }

        /// <summary>Attack if possible, else chase the nearest enemy in sight, else wander.</summary>
        static GameAction DecideMonster(World w, Entity self)
        {
            if (TryUseMoveOn(w, self, Entity.None, out var attack)) return attack;

            var target = NearestEnemy(w, self, MonsterSight);
            if (target.IsNone) return Wander(w, self);

            var tp = w.Positions.Get(target.Id);
            return TryStepToward(w, self, tp.X, tp.Y, out var step) ? step : GameAction.Wait();
        }

        /// <summary>Follows the member's orders. "Follow" means follow whoever is leader right now.</summary>
        static GameAction DecidePartyMember(World w, Entity self)
        {
            var orders = w.Orders.Get(self.Id);

            // AttackTarget: go after that enemy. If it's dead, fall through to Follow.
            if (orders.Type == OrderType.AttackTarget && w.IsAlive(orders.Target))
            {
                if (TryUseMoveOn(w, self, orders.Target, out var hitTarget)) return hitTarget;
                var tp = w.Positions.Get(orders.Target.Id);
                if (TryStepToward(w, self, tp.X, tp.Y, out var chase)) return chase;
            }

            // Every order fights back when an enemy is in reach.
            if (TryUseMoveOn(w, self, Entity.None, out var attack)) return attack;
            if (orders.Type == OrderType.Hold) return GameAction.Wait();

            // Follow: engage nearby enemies without straying from the leader, otherwise regroup.
            var me = w.Positions.Get(self.Id);
            var lp = w.IsAlive(w.Leader) ? w.Positions.Get(w.Leader.Id) : me;
            int leaderDist = Rules.Chebyshev(me.X, me.Y, lp.X, lp.Y);

            var foe = NearestEnemy(w, self, AllyEngageRange);
            if (!foe.IsNone && leaderDist <= AllyLeash)
            {
                var fp = w.Positions.Get(foe.Id);
                if (TryStepToward(w, self, fp.X, fp.Y, out var engage)) return engage;
            }
            if (leaderDist > 1 && TryStepToward(w, self, lp.X, lp.Y, out var regroup)) return regroup;
            return GameAction.Wait();
        }

        /// <summary>Finds the first move (in list order) and direction that hits an enemy.</summary>
        /// <param name="w">The world.</param>
        /// <param name="self">The actor.</param>
        /// <param name="mustHit">If set, the move has to hit this entity. Entity.None = any enemy.</param>
        /// <param name="action">The UseMove action found.</param>
        /// <returns>False if no move can hit.</returns>
        static bool TryUseMoveOn(World w, Entity self, Entity mustHit, out GameAction action)
        {
            var moves = w.Kinds.Get(self.Id).Def.Moves;
            for (int slot = 0; slot < moves.Count; slot++)
            {
                if (moves[slot].Shape == MoveShape.AllAround)
                {
                    MoveTargeting.FindTargets(w, self, moves[slot], 0, 0, _targets);
                    if (Hits(mustHit)) { action = GameAction.UseMove(slot); return true; }
                    continue;
                }
                for (int i = 0; i < 8; i++)
                {
                    int dx = Rules.DirX[i], dy = Rules.DirY[i];
                    MoveTargeting.FindTargets(w, self, moves[slot], dx, dy, _targets);
                    if (Hits(mustHit)) { action = GameAction.UseMove(slot, dx, dy); return true; }
                }
            }
            action = default;
            return false;
        }

        /// <summary>Did the last FindTargets call hit mustHit (or anyone, if mustHit is None)?</summary>
        static bool Hits(Entity mustHit) => mustHit.IsNone ? _targets.Count > 0 : _targets.Contains(mustHit);

        /// <summary>Closest enemy within range, ties broken by lowest Id.</summary>
        /// <param name="w">The world.</param>
        /// <param name="self">The actor looking.</param>
        /// <param name="range">How far it looks, in tiles.</param>
        /// <returns>The enemy, or Entity.None.</returns>
        static Entity NearestEnemy(World w, Entity self, int range)
        {
            var p = w.Positions.Get(self.Id);
            int bestDist = int.MaxValue, bestId = -1;
            for (int i = 0; i < w.Factions.Count; i++)
            {
                int id = w.Factions.IdAt(i);
                if (id == self.Id || !Rules.IsHostile(w, self, w.HandleOf(id))) continue;
                var op = w.Positions.Get(id);
                int d = Rules.Chebyshev(p.X, p.Y, op.X, op.Y);
                if (d > range) continue;
                if (d < bestDist || (d == bestDist && id < bestId)) { bestDist = d; bestId = id; }
            }
            return bestId < 0 ? Entity.None : w.HandleOf(bestId);
        }

        /// <summary>Picks the free neighboring tile closest to the goal. No pathfinding: it can get stuck behind walls.</summary>
        /// <param name="w">The world.</param>
        /// <param name="self">The actor moving.</param>
        /// <param name="gx">Goal tile x.</param>
        /// <param name="gy">Goal tile y.</param>
        /// <param name="action">The Step action found.</param>
        /// <returns>False if no step gets closer.</returns>
        static bool TryStepToward(World w, Entity self, int gx, int gy, out GameAction action)
        {
            var p = w.Positions.Get(self.Id);
            int bestDist = Rules.Chebyshev(p.X, p.Y, gx, gy);
            int bestSq = Sq(p.X - gx) + Sq(p.Y - gy);   // straight-line distance², breaks ties
            int bestDir = -1;
            for (int i = 0; i < 8; i++)
            {
                int dx = Rules.DirX[i], dy = Rules.DirY[i];
                if (!Rules.CanStep(w, self, dx, dy)) continue;
                int nx = p.X + dx, ny = p.Y + dy;
                int d = Rules.Chebyshev(nx, ny, gx, gy);
                int sq = Sq(nx - gx) + Sq(ny - gy);
                if (d < bestDist || (d == bestDist && sq < bestSq)) { bestDist = d; bestSq = sq; bestDir = i; }
            }

            action = bestDir < 0 ? default : GameAction.Step(Rules.DirX[bestDir], Rules.DirY[bestDir]);
            return bestDir >= 0;
        }

        /// <summary>Half the time waits; otherwise steps in a random open direction.</summary>
        static GameAction Wander(World w, Entity self)
        {
            if (w.Rng.Range(0, 2) == 0) return GameAction.Wait();
            int start = w.Rng.Range(0, 8);
            for (int k = 0; k < 8; k++)
            {
                int i = (start + k) % 8;
                if (Rules.CanStep(w, self, Rules.DirX[i], Rules.DirY[i]))
                    return GameAction.Step(Rules.DirX[i], Rules.DirY[i]);
            }
            return GameAction.Wait();
        }

        static int Sq(int v) => v * v;
    }
}
