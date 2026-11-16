using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    public enum SimStatus { AwaitingInput, GameOver }

    /// <summary>
    /// The turn engine. Resolves turns instantly and records each change as a SimEvent.
    /// It runs AI turns until it's the leader's turn, then waits for the player.
    /// </summary>
    public sealed class Simulation
    {
        public readonly World World;

        /// <summary>The leader, while the sim waits for the player. None otherwise.</summary>
        public Entity ActorWaitingForInput { get; private set; } = Entity.None;

        public SimStatus Status { get; private set; } = SimStatus.AwaitingInput;

        const int MaxTurnsPerRun = 100000;   // safety net against an endless loop
        readonly List<Entity> _targets = new List<Entity>();

        /// <param name="world">The state this sim reads and changes.</param>
        public Simulation(World world) { World = world; }

        /// <summary>Only the leader is player-controlled. Everyone else uses AI.</summary>
        public bool IsPlayerControlled(Entity e) => e == World.Leader;

        // =====================================================================================
        //  Running turns
        // =====================================================================================

        /// <summary>Resolves turns in order until it's the leader's turn or the party is gone.</summary>
        /// <returns>AwaitingInput, or GameOver.</returns>
        public SimStatus RunUntilPlayerTurn()
        {
            for (int i = 0; i < MaxTurnsPerRun; i++)
            {
                if (!World.IsAlive(World.Leader) && !PickNewLeader()) return EndGame();
                if (!ActorWaitingForInput.IsNone) return Status = SimStatus.AwaitingInput;
                if (!World.Turns.TryTakeNext(out var turn)) return EndGame();
                if (!World.IsAlive(turn.Entity)) continue;   // a dead actor's leftover turn

                World.Tick = turn.Tick;

                if (IsPlayerControlled(turn.Entity)) ActorWaitingForInput = turn.Entity;   // stop next loop
                else ResolveAction(turn.Entity, AiBrain.Decide(World, turn.Entity));
            }
            throw new InvalidOperationException("RunUntilPlayerTurn exceeded its turn limit.");
        }

        /// <summary>Ends the game right away.</summary>
        /// <returns>GameOver, so RunUntilPlayerTurn can return it directly.</returns>
        public SimStatus EndGame()
        {
            ActorWaitingForInput = Entity.None;
            return Status = SimStatus.GameOver;
        }

        /// <summary>When the leader dies, the first living party member takes over.</summary>
        /// <returns>False if the whole party is down.</returns>
        bool PickNewLeader()
        {
            foreach (var member in World.Party)
                if (World.IsAlive(member)) { World.Leader = member; return true; }
            World.Leader = Entity.None;
            return false;
        }

        // =====================================================================================
        //  Player commands
        // =====================================================================================

        /// <summary>Resolves the leader's action, then runs everyone else until the leader's next turn.</summary>
        /// <param name="action">What the leader does.</param>
        /// <returns>False if it isn't the player's turn or the action isn't possible.</returns>
        public bool DoPlayerAction(GameAction action)
        {
            if (Status != SimStatus.AwaitingInput || ActorWaitingForInput.IsNone) return false;
            var actor = ActorWaitingForInput;
            if (action.Type == ActionType.Step && !Rules.CanStep(World, actor, action.Dx, action.Dy)) return false;
            if (action.Type == ActionType.UseMove && !HasMove(actor, action.Slot)) return false;

            ActorWaitingForInput = Entity.None;
            ResolveAction(actor, action);
            RunUntilPlayerTurn();
            return true;
        }

        /// <summary>Free action: the leader turns without using its turn. Records nothing if already facing that way.</summary>
        /// <param name="dx">Direction x (-1, 0, 1).</param>
        /// <param name="dy">Direction y (-1, 0, 1).</param>
        public void Face(int dx, int dy)
        {
            var e = ActorWaitingForInput;
            if (e.IsNone || (dx == 0 && dy == 0)) return;
            var f = World.Facings.Get(e.Id);
            if (f.Dx == dx && f.Dy == dy) return;

            SetFacing(e, dx, dy);
            var p = World.Positions.Get(e.Id);
            World.Record(new SimEvent
            {
                Type = SimEventType.Faced, Actor = e,
                FromX = p.X, FromY = p.Y, ToX = p.X, ToY = p.Y, DirX = dx, DirY = dy
            });
        }

        /// <summary>
        /// Free action on the leader's turn: another party member becomes leader and acts now.
        /// The two swap places in the turn queue, so nobody gains or loses a turn.
        /// </summary>
        /// <param name="member">A living party member who isn't the leader.</param>
        /// <returns>False if the swap isn't allowed.</returns>
        public bool SwapLeader(Entity member)
        {
            if (Status != SimStatus.AwaitingInput || ActorWaitingForInput.IsNone) return false;
            if (!World.IsAlive(member) || !World.Party.Contains(member) || member == World.Leader) return false;
            if (!World.Turns.TryRemove(member, out var theirTurn)) return false;

            var oldLeader = ActorWaitingForInput;
            World.Turns.Schedule(oldLeader, theirTurn.Tick, World.Actors.Get(oldLeader.Id).Initiative);
            World.Leader = member;
            ActorWaitingForInput = member;
            return true;
        }

        /// <summary>Free action: sets a party member's orders, used whenever it isn't the leader.</summary>
        /// <param name="member">The party member.</param>
        /// <param name="type">Follow, Hold or AttackTarget.</param>
        /// <param name="target">The enemy to attack (AttackTarget only).</param>
        public void SetOrders(Entity member, OrderType type, Entity target)
        {
            if (!World.IsAlive(member) || !World.Orders.Has(member.Id)) return;
            World.Orders.Get(member.Id) = new Orders { Type = type, Target = target };
        }

        /// <param name="e">The actor.</param>
        /// <param name="slot">Move slot, 0 = first.</param>
        /// <returns>True if the actor is alive and has a move in that slot.</returns>
        public bool HasMove(Entity e, int slot)
        {
            if (!World.IsAlive(e) || !World.Kinds.Has(e.Id)) return false;
            return slot >= 0 && slot < World.Kinds.Get(e.Id).Def.Moves.Count;
        }

        // =====================================================================================
        //  Resolving actions
        // =====================================================================================

        /// <summary>Carries out an action, then schedules the actor's next turn based on its cost.</summary>
        void ResolveAction(Entity e, GameAction a)
        {
            int cost = ActionCosts.Wait;   // Wait, and anything that fails, costs a normal turn
            if (a.Type == ActionType.Step && Rules.CanStep(World, e, a.Dx, a.Dy))
            {
                DoStep(e, a.Dx, a.Dy);
                cost = ActionCosts.Step;
            }
            else if (a.Type == ActionType.UseMove && HasMove(e, a.Slot))
            {
                cost = DoUseMove(e, a.Slot, a.Dx, a.Dy);
            }

            if (World.IsAlive(e)) ScheduleNextTurn(e, cost);
        }

        /// <summary>Next turn = now + cost * 100 / speed. Faster actors come back sooner.</summary>
        void ScheduleNextTurn(Entity e, int cost)
        {
            int speed = Math.Max(1, World.Speeds.Get(e.Id).Percent);
            long delay = Math.Max(1L, (long)cost * 100 / speed);
            World.Turns.Schedule(e, World.Tick + delay, World.Actors.Get(e.Id).Initiative);
        }

        void SetFacing(Entity e, int dx, int dy) => World.Facings.Set(e.Id, new Facing { Dx = dx, Dy = dy });

        /// <summary>Moves one tile. The caller has already checked CanStep.</summary>
        void DoStep(Entity e, int dx, int dy)
        {
            var p = World.Positions.Get(e.Id);
            int tx = p.X + dx, ty = p.Y + dy;
            SetFacing(e, dx, dy);
            World.Place(e, tx, ty);
            World.Record(new SimEvent
            {
                Type = SimEventType.Stepped, Actor = e,
                FromX = p.X, FromY = p.Y, ToX = tx, ToY = ty, DirX = dx, DirY = dy
            });
        }

        /// <summary>Uses a move and damages every enemy it hits. The caller has already checked HasMove.</summary>
        /// <param name="e">The user.</param>
        /// <param name="slot">Move slot, 0 = first.</param>
        /// <param name="dx">Direction x, or 0 with dy = 0 to use the current facing.</param>
        /// <param name="dy">Direction y.</param>
        /// <returns>The move's energy cost.</returns>
        int DoUseMove(Entity e, int slot, int dx, int dy)
        {
            var def = World.Kinds.Get(e.Id).Def;
            var move = def.Moves[slot];

            if (dx == 0 && dy == 0)
            {
                var f = World.Facings.Get(e.Id);
                dx = f.Dx; dy = f.Dy;
            }
            SetFacing(e, dx, dy);

            var p = World.Positions.Get(e.Id);
            MoveTargeting.FindTargets(World, e, move, dx, dy, _targets);
            var targets = _targets.ToArray();   // copied: the event keeps it after _targets is reused

            var used = World.Record(new SimEvent
            {
                Type = SimEventType.UsedMove, Actor = e, Move = move, Targets = targets,
                FromX = p.X, FromY = p.Y, ToX = p.X + dx, ToY = p.Y + dy, DirX = dx, DirY = dy,
                UsesMove = true
            });

            if (move.Effect == MoveEffect.Heal)
            {
                foreach (var ally in targets) Heal(e, ally, move.Power, used.Id);
            }
            else
            {
                int damage = Math.Max(1, move.Power + def.Attack);
                foreach (var victim in targets) DealDamage(e, victim, damage, used.Id);
            }

            return Math.Max(1, move.EnergyCost);
        }

        /// <summary>Lowers HP, and destroys the victim if it reaches 0.</summary>
        /// <param name="attacker">Who dealt the damage.</param>
        /// <param name="victim">Who takes it.</param>
        /// <param name="damage">How much.</param>
        /// <param name="causeEventId">The UsedMove event, so playback shows the hit first.</param>
        void DealDamage(Entity attacker, Entity victim, int damage, int causeEventId)
        {
            if (!World.IsAlive(victim)) return;
            var pos = World.Positions.Get(victim.Id);

            ref var hp = ref World.Healths.Get(victim.Id);
            hp.Hp = Math.Max(0, hp.Hp - damage);
            int hpAfter = hp.Hp;   // read now: Destroy below removes the component

            var hurt = World.Record(new SimEvent
            {
                Type = SimEventType.Damaged, Actor = attacker, Target = victim,
                FromX = pos.X, FromY = pos.Y, ToX = pos.X, ToY = pos.Y,
                Damage = damage, HpAfter = hpAfter, CausedByEventId = causeEventId
            });

            if (hpAfter > 0) return;
            World.Record(new SimEvent
            {
                Type = SimEventType.Died, Actor = victim,
                FromX = pos.X, FromY = pos.Y, ToX = pos.X, ToY = pos.Y, CausedByEventId = hurt.Id
            });
            World.Destroy(victim);
        }

        /// <summary>Restores HP, never above max.</summary>
        /// <param name="healer">Who used the move.</param>
        /// <param name="ally">Who gets the HP.</param>
        /// <param name="amount">How much to restore.</param>
        /// <param name="causeEventId">The UsedMove event, so playback shows the move first.</param>
        void Heal(Entity healer, Entity ally, int amount, int causeEventId)
        {
            if (!World.IsAlive(ally)) return;
            var pos = World.Positions.Get(ally.Id);

            ref var hp = ref World.Healths.Get(ally.Id);
            int before = hp.Hp;
            hp.Hp = Math.Min(hp.Max, hp.Hp + amount);

            World.Record(new SimEvent
            {
                Type = SimEventType.Healed,
                Actor = healer,
                Target = ally,
                FromX = pos.X,
                FromY = pos.Y,
                ToX = pos.X,
                ToY = pos.Y,
                Healing = hp.Hp - before,
                HpAfter = hp.Hp,
                CausedByEventId = causeEventId
            });
        }
    }
}
