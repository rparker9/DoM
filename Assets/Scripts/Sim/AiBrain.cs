using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>
    /// Picks actions for everyone the player isn't controlling.
    /// Every actor picks moves the same way: try them all, score each from its effects, use the best.
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

        /// <summary>Use the best move if any is worth it, else chase the nearest enemy in sight, else wander.</summary>
        static GameAction DecideMonster(World w, Entity self)
        {
            if (TryBestMove(w, self, Entity.None, out var move)) return move;

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
                if (TryBestMove(w, self, orders.Target, out var hitTarget)) return hitTarget;
                var tp = w.Positions.Get(orders.Target.Id);
                if (TryStepToward(w, self, tp.X, tp.Y, out var chase)) return chase;
            }

            // Every order fights back, or heals, when there's something worth doing.
            if (TryBestMove(w, self, Entity.None, out var move)) return move;
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

        /// <summary>Tries every move in every direction and picks the one with the best score.</summary>
        /// <param name="w">The world.</param>
        /// <param name="self">The actor deciding.</param>
        /// <param name="mustHit">If set, only moves that reach this entity count. Entity.None = any.</param>
        /// <param name="action">The UseMove action found.</param>
        /// <returns>False if no move scores above 0 (nothing worth doing).</returns>
        static bool TryBestMove(World w, Entity self, Entity mustHit, out GameAction action)
        {
            action = default;
            int bestScore = 0;
            var moves = w.Kinds.Get(self.Id).Def.Moves;

            for (int slot = 0; slot < moves.Count; slot++)
            {
                // Only Line moves care about direction; the rest are tried once.
                int directions = moves[slot].Area == MoveArea.Line ? 8 : 1;
                for (int i = 0; i < directions; i++)
                {
                    int dx = directions == 8 ? Rules.DirX[i] : 0;
                    int dy = directions == 8 ? Rules.DirY[i] : 0;
                    MoveTargeting.FindTargets(w, self, moves[slot], dx, dy, _targets);
                    if (!mustHit.IsNone && !_targets.Contains(mustHit)) continue;

                    int score = Score(w, self, moves[slot]);
                    if (score <= bestScore) continue;   // ties keep the earlier move
                    bestScore = score;
                    action = directions == 8 ? GameAction.UseMove(slot, dx, dy) : GameAction.UseMove(slot);
                }
            }
            return bestScore > 0;
        }

        /// <summary>
        /// How good the move is against the targets from the last FindTargets call.
        /// Reads the same effect list the sim runs, so new moves need no AI code.
        /// </summary>
        static int Score(World w, Entity self, MoveDef move)
        {
            int attack = w.Kinds.Get(self.Id).Def.Attack;
            int score = 0;
            foreach (var target in _targets)
            {
                bool enemy = Rules.IsHostile(w, self, target);
                var hp = w.Healths.Get(target.Id);
                foreach (var fx in move.Effects)
                {
                    int value = 0;
                    switch (fx.Kind)
                    {
                        case EffectKind.Damage:
                            int damage = Math.Min(hp.Hp, Math.Max(1, fx.Amount + attack));
                            value = enemy ? damage : -2 * damage;   // hurting friends is twice as bad
                            break;
                        case EffectKind.Heal:
                            bool badlyHurt = hp.Hp * 2 <= hp.Max;   // only heal at half HP or less
                            value = enemy ? -fx.Amount : badlyHurt ? 2 * Math.Min(fx.Amount, hp.Max - hp.Hp) : 0;
                            break;
                        case EffectKind.Knockback:
                            value = enemy ? 1 : -1;
                            break;
                    }
                    score += value * fx.Chance / 100;
                }
            }
            return score * Math.Max(1, move.Hits);
        }

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
