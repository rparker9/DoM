using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>
    /// Deliberately simple greedy AI. Pure function of world state + world RNG.
    /// Fighting always works the same way: use the first move in the actor's list that can hit something.
    /// </summary>
    public static class AiBrain
    {
        public const int MonsterSight = 6;
        public const int AllyEngageRange = 3;
        public const int AllyLeash = 4;

        static readonly List<Entity> _targets = new List<Entity>();

        public static GameAction Decide(World w, Entity self)
        {
            var mode = w.Controllers.Has(self.Id) ? w.Controllers.Get(self.Id).Mode : ControlMode.Monster;
            return mode == ControlMode.Party ? DecidePartyMember(w, self) : DecideMonster(w, self);
        }

        static GameAction DecideMonster(World w, Entity self)
        {
            if (TryUseMoveOn(w, self, Entity.None, out var attack)) return attack;

            var target = NearestHostile(w, self, MonsterSight);
            if (!target.IsNone)
            {
                var tp = w.Positions.Get(target.Id);
                return TryStepToward(w, self, tp.X, tp.Y, out var step) ? step : GameAction.Wait();
            }
            return Wander(w, self);
        }

        /// <summary>Party AI. "Follow" means follow whoever is leader right now.</summary>
        static GameAction DecidePartyMember(World w, Entity self)
        {
            var orders = w.Orders.Get(self.Id);
            switch (orders.Type)
            {
                case OrderType.Hold:
                    return TryUseMoveOn(w, self, Entity.None, out var holdAttack) ? holdAttack : GameAction.Wait();

                case OrderType.AttackTarget:
                    if (w.IsAlive(orders.Target))
                    {
                        if (TryUseMoveOn(w, self, orders.Target, out var targetAttack)) return targetAttack;
                        var tp = w.Positions.Get(orders.Target.Id);
                        if (TryStepToward(w, self, tp.X, tp.Y, out var chase)) return chase;
                        return TryUseMoveOn(w, self, Entity.None, out var fallbackAttack) ? fallbackAttack : GameAction.Wait();
                    }
                    goto default;   // target dead: behave as Follow

                default: // Follow
                {
                    if (TryUseMoveOn(w, self, Entity.None, out var followAttack)) return followAttack;

                    var me = w.Positions.Get(self.Id);
                    var lp = w.IsAlive(w.Leader) ? w.Positions.Get(w.Leader.Id) : me;
                    int leaderDist = Rules.Chebyshev(me.X, me.Y, lp.X, lp.Y);

                    var foe = NearestHostile(w, self, AllyEngageRange);
                    if (!foe.IsNone && leaderDist <= AllyLeash)
                    {
                        var fp = w.Positions.Get(foe.Id);
                        if (TryStepToward(w, self, fp.X, fp.Y, out var engage)) return engage;
                    }
                    if (leaderDist > 1 && TryStepToward(w, self, lp.X, lp.Y, out var regroup)) return regroup;
                    return GameAction.Wait();
                }
            }
        }

        /// <summary>
        /// Finds the first move (in list order) and direction that hits an enemy.
        /// If <paramref name="mustHit"/> is set, the move has to hit that entity.
        /// </summary>
        static bool TryUseMoveOn(World w, Entity self, Entity mustHit, out GameAction action)
        {
            action = default;
            if (!w.Kinds.Has(self.Id)) return false;
            var moves = w.Kinds.Get(self.Id).Def.Moves;

            for (int slot = 0; slot < moves.Count; slot++)
            {
                var move = moves[slot];
                if (move.Shape == MoveShape.AllAround)
                {
                    MoveTargeting.FindTargets(w, self, move, 0, 0, _targets);
                    if (Hits(mustHit)) { action = GameAction.UseMove(slot); return true; }
                    continue;
                }
                for (int i = 0; i < 8; i++)
                {
                    int dx = Rules.DirX[i], dy = Rules.DirY[i];
                    MoveTargeting.FindTargets(w, self, move, dx, dy, _targets);
                    if (Hits(mustHit)) { action = GameAction.UseMove(slot, dx, dy); return true; }
                }
            }
            return false;
        }

        static bool Hits(Entity mustHit) => mustHit.IsNone ? _targets.Count > 0 : _targets.Contains(mustHit);

        static Entity NearestHostile(World w, Entity self, int range)
        {
            var p = w.Positions.Get(self.Id);
            int bestDist = int.MaxValue, bestId = -1;
            for (int i = 0; i < w.Factions.Count; i++)
            {
                int id = w.Factions.IdAt(i);
                if (id == self.Id || !w.Positions.Has(id)) continue;
                var other = w.HandleOf(id);
                if (!Rules.IsHostile(w, self, other)) continue;
                var op = w.Positions.Get(id);
                int d = Rules.Chebyshev(p.X, p.Y, op.X, op.Y);
                if (d > range) continue;
                if (d < bestDist || (d == bestDist && id < bestId)) { bestDist = d; bestId = id; }
            }
            return bestId < 0 ? Entity.None : w.HandleOf(bestId);
        }

        /// <summary>Greedy step that reduces Chebyshev distance (ties broken by Euclidean distance).</summary>
        static bool TryStepToward(World w, Entity self, int gx, int gy, out GameAction action)
        {
            var p = w.Positions.Get(self.Id);
            int bestD = Rules.Chebyshev(p.X, p.Y, gx, gy);
            int bestSq = Sq(p.X - gx) + Sq(p.Y - gy);
            int bestDir = -1;
            for (int i = 0; i < 8; i++)
            {
                int dx = Rules.DirX[i], dy = Rules.DirY[i];
                if (!Rules.CanStep(w, self, dx, dy)) continue;
                int nx = p.X + dx, ny = p.Y + dy;
                int d = Rules.Chebyshev(nx, ny, gx, gy);
                int sq = Sq(nx - gx) + Sq(ny - gy);
                if (d < bestD || (d == bestD && sq < bestSq)) { bestD = d; bestSq = sq; bestDir = i; }
            }
            if (bestDir < 0) { action = default; return false; }
            action = GameAction.Step(Rules.DirX[bestDir], Rules.DirY[bestDir]);
            return true;
        }

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
