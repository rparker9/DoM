using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>
    /// Carries out any move from its data. Records the move's beats as MoveCue events
    /// (WindUp, then per hit: Travel and Impact) and each effect's result as its own event.
    /// Every event points at the one before it, so playback keeps them in order.
    /// </summary>
    public sealed class MoveRunner
    {
        readonly World _world;
        readonly List<Entity> _targets = new List<Entity>();

        /// <param name="world">The state moves read and change.</param>
        public MoveRunner(World world) { _world = world; }

        /// <summary>Uses a move. The caller has already checked the actor has it.</summary>
        /// <param name="user">Who uses the move.</param>
        /// <param name="move">The move.</param>
        /// <param name="dx">Direction x, or 0 with dy = 0 to use the current facing.</param>
        /// <param name="dy">Direction y.</param>
        /// <returns>The move's energy cost.</returns>
        public int Run(Entity user, MoveDef move, int dx, int dy)
        {
            if (dx == 0 && dy == 0)
            {
                var f = _world.Facings.Get(user.Id);
                dx = f.Dx; dy = f.Dy;
            }
            _world.Facings.Set(user.Id, new Facing { Dx = dx, Dy = dy });

            var p = _world.Positions.Get(user.Id);
            var windUp = Cue(CuePhase.WindUp, user, move, p.X, p.Y, p.X + dx, p.Y + dy, dx, dy, -1, -1);
            int cause = windUp.Id;

            for (int hit = 0; hit < Math.Max(1, move.Hits); hit++)
            {
                if (!_world.IsAlive(user)) break;   // recoil can end a multi-hit move early
                p = _world.Positions.Get(user.Id);

                MoveTargeting.Resolve(_world, user, move, dx, dy, _targets, out int ix, out int iy);
                var targets = _targets.ToArray();   // copied: _targets is reused

                if (move.Delivery != Delivery.Contact)
                    cause = Cue(CuePhase.Travel, user, move, p.X, p.Y, ix, iy, dx, dy, cause, windUp.Id).Id;

                var impact = Cue(CuePhase.Impact, user, move, p.X, p.Y, ix, iy, dx, dy, cause, windUp.Id);
                impact.Targets = targets;
                impact.Radius = ImpactRadius(move);

                ApplyEffects(user, move, targets, impact.Id);
                cause = impact.Id;   // the next hit follows this impact
            }
            return Math.Max(1, move.EnergyCost);
        }

        /// <summary>How many tiles around the impact tile the impact covers, for locks and visuals.</summary>
        static int ImpactRadius(MoveDef move)
        {
            if (move.Area == MoveArea.Around) return Math.Max(1, move.Range);
            if (move.Area == MoveArea.Line) return move.BurstRadius;
            return 0;
        }

        /// <summary>Records one beat of a move.</summary>
        /// <param name="moveGroup">The WindUp's Id, or -1 when recording the WindUp itself.</param>
        SimEvent Cue(CuePhase phase, Entity user, MoveDef move, int fromX, int fromY, int toX, int toY,
                     int dx, int dy, int cause, int moveGroup)
        {
            var ev = _world.Record(new SimEvent
            {
                Type = SimEventType.MoveCue, Phase = phase, Actor = user, Move = move, Delivery = move.Delivery,
                FromX = fromX, FromY = fromY, ToX = toX, ToY = toY, DirX = dx, DirY = dy,
                CausedByEventId = cause, UsesMove = true
            });
            ev.MoveGroup = moveGroup < 0 ? ev.Id : moveGroup;
            return ev;
        }

        // =====================================================================================
        //  Effects
        // =====================================================================================

        /// <summary>Applies each effect to each target, then the effects that land on the user.</summary>
        /// <param name="user">Who used the move.</param>
        /// <param name="move">The move.</param>
        /// <param name="targets">Everyone the impact reached.</param>
        /// <param name="cause">The Impact event.</param>
        void ApplyEffects(Entity user, MoveDef move, Entity[] targets, int cause)
        {
            int dealt = 0;   // damage this hit dealt, for Drain and Recoil

            foreach (var target in targets)
            foreach (var fx in move.Effects)
            {
                if (!_world.IsAlive(target)) break;   // already down: skip its remaining effects
                if (fx.Kind == EffectKind.Drain || fx.Kind == EffectKind.Recoil) continue;   // user effects, below
                if (_world.Rng.Range(0, 100) >= fx.Chance) continue;

                switch (fx.Kind)
                {
                    case EffectKind.Damage:
                        int attack = _world.Kinds.Get(user.Id).Def.Attack;
                        dealt += DealDamage(user, target, Math.Max(1, fx.Amount + attack), cause);
                        break;
                    case EffectKind.Heal:
                        Heal(user, target, fx.Amount, cause);
                        break;
                    case EffectKind.Knockback:
                        Knockback(user, target, fx.Amount, cause);
                        break;
                }
            }

            foreach (var fx in move.Effects)
            {
                if (dealt == 0 || !_world.IsAlive(user)) break;
                int share = Math.Max(1, dealt * fx.Amount / 100);
                if (fx.Kind == EffectKind.Drain) Heal(user, user, share, cause);
                else if (fx.Kind == EffectKind.Recoil) DealDamage(user, user, share, cause);
            }
        }

        /// <summary>Lowers HP, and destroys the victim at 0.</summary>
        /// <param name="attacker">Who dealt it.</param>
        /// <param name="victim">Who takes it.</param>
        /// <param name="damage">How much.</param>
        /// <param name="cause">The event that caused it.</param>
        /// <returns>The HP actually lost.</returns>
        int DealDamage(Entity attacker, Entity victim, int damage, int cause)
        {
            var pos = _world.Positions.Get(victim.Id);
            ref var hp = ref _world.Healths.Get(victim.Id);
            int lost = Math.Min(hp.Hp, damage);
            hp.Hp -= lost;
            int hpAfter = hp.Hp;   // read now: Destroy below removes the component

            var hurt = _world.Record(new SimEvent
            {
                Type = SimEventType.Damaged, Actor = attacker, Target = victim,
                FromX = pos.X, FromY = pos.Y, ToX = pos.X, ToY = pos.Y,
                Damage = damage, HpAfter = hpAfter, CausedByEventId = cause
            });

            if (hpAfter == 0)
            {
                _world.Record(new SimEvent
                {
                    Type = SimEventType.Died, Actor = victim,
                    FromX = pos.X, FromY = pos.Y, ToX = pos.X, ToY = pos.Y, CausedByEventId = hurt.Id
                });
                _world.Destroy(victim);
            }
            return lost;
        }

        /// <summary>Restores HP, never above max.</summary>
        /// <param name="healer">Who healed.</param>
        /// <param name="ally">Who gets the HP.</param>
        /// <param name="amount">How much.</param>
        /// <param name="cause">The event that caused it.</param>
        void Heal(Entity healer, Entity ally, int amount, int cause)
        {
            var pos = _world.Positions.Get(ally.Id);
            ref var hp = ref _world.Healths.Get(ally.Id);
            int before = hp.Hp;
            hp.Hp = Math.Min(hp.Max, hp.Hp + amount);

            _world.Record(new SimEvent
            {
                Type = SimEventType.Healed, Actor = healer, Target = ally,
                FromX = pos.X, FromY = pos.Y, ToX = pos.X, ToY = pos.Y,
                Healing = hp.Hp - before, HpAfter = hp.Hp, CausedByEventId = cause
            });
        }

        /// <summary>Pushes the target away from the user, stopping at walls and actors.</summary>
        /// <param name="user">Who pushes.</param>
        /// <param name="target">Who is pushed.</param>
        /// <param name="tiles">How far, at most.</param>
        /// <param name="cause">The event that caused it.</param>
        void Knockback(Entity user, Entity target, int tiles, int cause)
        {
            var from = _world.Positions.Get(target.Id);
            var up = _world.Positions.Get(user.Id);
            int dx = Math.Sign(from.X - up.X), dy = Math.Sign(from.Y - up.Y);
            if (dx == 0 && dy == 0) return;   // can't push yourself

            int x = from.X, y = from.Y;
            for (int i = 0; i < tiles; i++)
            {
                if (!_world.Map.IsFree(x + dx, y + dy) || !Rules.CornerClear(_world.Map, x, y, dx, dy)) break;
                x += dx; y += dy;
            }
            if (x == from.X && y == from.Y) return;   // blocked right away

            _world.Place(target, x, y);
            _world.Record(new SimEvent
            {
                Type = SimEventType.Displaced, Actor = target,
                FromX = from.X, FromY = from.Y, ToX = x, ToY = y, CausedByEventId = cause
            });
        }
    }
}
