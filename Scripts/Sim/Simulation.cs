using System;
using System.Collections.Generic;

namespace Dungeon.Sim
{
    public enum SimStatus { AwaitingInput, GameOver }

    /// <summary>
    /// Resolves turns instantly. RunUntilPlayerTurn() runs AI turns until the leader
    /// (the party member the player controls) is up, then stops.
    /// The view takes the recorded events and plays them back on its own clock.
    /// </summary>
    public sealed class Simulation
    {
        public readonly World World;
        public Entity ActorWaitingForInput { get; private set; } = Entity.None;
        public SimStatus Status { get; private set; } = SimStatus.AwaitingInput;

        const int MaxTurnsPerRun = 100000;
        readonly List<Entity> _targets = new List<Entity>();   // reused by DoUseMove

        public Simulation(World world) { World = world; }

        /// <summary>Only the current leader is player-controlled. Everyone else uses AI.</summary>
        public bool IsPlayerControlled(Entity e) => e == World.Leader;

        public SimStatus RunUntilPlayerTurn()
        {
            for (int i = 0; i < MaxTurnsPerRun; i++)
            {
                // The leader fell: control passes to the next living party member.
                if (!World.IsAlive(World.Leader) && !PickNewLeader())
                {
                    ActorWaitingForInput = Entity.None;
                    return Status = SimStatus.GameOver;   // the whole party is down
                }

                if (!ActorWaitingForInput.IsNone) return Status = SimStatus.AwaitingInput;

                if (!World.Turns.TryTakeNext(out var turn)) return Status = SimStatus.GameOver;
                if (!World.IsAlive(turn.Entity)) continue;   // stale entry for a dead entity

                World.Tick = turn.Tick;

                if (IsPlayerControlled(turn.Entity)) { ActorWaitingForInput = turn.Entity; continue; }

                ResolveAction(turn.Entity, AiBrain.Decide(World, turn.Entity));
            }
            throw new InvalidOperationException("RunUntilPlayerTurn exceeded turn limit.");
        }

        public void EndGame() { ActorWaitingForInput = Entity.None; Status = SimStatus.GameOver; }

        // ---------- player commands ----------

        /// <summary>Resolves a timed action for the waiting actor, then runs until the next player turn. False if invalid.</summary>
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

        /// <summary>Free action: turn the waiting actor without spending time.</summary>
        public void Face(int dx, int dy)
        {
            if (ActorWaitingForInput.IsNone || (dx == 0 && dy == 0)) return;
            var f = World.Facings.Get(ActorWaitingForInput.Id);
            if (f.Dx == dx && f.Dy == dy) return;
            SetFacing(ActorWaitingForInput, dx, dy);
            var p = World.Positions.Get(ActorWaitingForInput.Id);
            World.Record(new SimEvent
            {
                Type = SimEventType.Faced, Actor = ActorWaitingForInput,
                FromX = p.X, FromY = p.Y, ToX = p.X, ToY = p.Y, DirX = dx, DirY = dy
            });
        }

        /// <summary>
        /// Free action, only on the leader's turn: hand the current turn to another living party member.
        /// The two swap places in the turn queue. The new leader acts now, and the old leader takes
        /// the new leader's next turn (and goes back to party AI). Nobody gains or loses a turn.
        /// </summary>
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

        /// <summary>Makes the first living party member the leader. False if none are left.</summary>
        bool PickNewLeader()
        {
            foreach (var member in World.Party)
                if (World.IsAlive(member)) { World.Leader = member; return true; }
            World.Leader = Entity.None;
            return false;
        }

        /// <summary>Free action: change a party member's orders. They apply whenever that member isn't the leader.</summary>
        public void SetOrders(Entity member, OrderType type, Entity target)
        {
            if (!World.IsAlive(member) || !World.Orders.Has(member.Id)) return;
            World.Orders.Get(member.Id) = new Orders { Type = type, Target = target };
        }

        // ---------- resolution ----------

        /// <summary>True if the actor has a move in this slot (0 = first move).</summary>
        public bool HasMove(Entity e, int slot)
        {
            if (!World.IsAlive(e) || !World.Kinds.Has(e.Id)) return false;
            var moves = World.Kinds.Get(e.Id).Def.Moves;
            return slot >= 0 && slot < moves.Count;
        }

        void ResolveAction(Entity e, GameAction a)
        {
            int cost;
            switch (a.Type)
            {
                case ActionType.Step:
                    if (Rules.CanStep(World, e, a.Dx, a.Dy)) { DoStep(e, a.Dx, a.Dy); cost = ActionCosts.Step; }
                    else cost = ActionCosts.Wait;   // blocked step degrades to waiting
                    break;
                case ActionType.UseMove:
                    cost = DoUseMove(e, a.Slot, a.Dx, a.Dy);
                    break;
                default:
                    cost = ActionCosts.Wait;
                    break;
            }
            if (World.IsAlive(e)) ScheduleNextTurn(e, cost);
        }

        void ScheduleNextTurn(Entity e, int cost)
        {
            int speed = Math.Max(1, World.Speeds.Get(e.Id).Percent);
            long delay = Math.Max(1L, (long)cost * 100 / speed);
            World.Turns.Schedule(e, World.Tick + delay, World.Actors.Get(e.Id).Initiative);
        }

        void SetFacing(Entity e, int dx, int dy) =>
            World.Facings.Set(e.Id, new Facing { Dx = dx, Dy = dy });

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

        /// <summary>Uses one of the actor's moves. Returns its energy cost.</summary>
        int DoUseMove(Entity e, int slot, int dx, int dy)
        {
            if (!HasMove(e, slot)) return ActionCosts.Wait;
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
            var targets = _targets.ToArray();

            var used = World.Record(new SimEvent
            {
                Type = SimEventType.UsedMove, Actor = e, Move = move, Targets = targets,
                FromX = p.X, FromY = p.Y, ToX = p.X + dx, ToY = p.Y + dy, DirX = dx, DirY = dy,
                UsesMove = true
            });

            int damage = Math.Max(1, move.Power + def.Attack);
            foreach (var victim in targets) DealDamage(e, victim, damage, used.Id);

            return Math.Max(1, move.EnergyCost);
        }

        void DealDamage(Entity attacker, Entity victim, int damage, int causeEventId)
        {
            if (!World.IsAlive(victim)) return;
            var pos = World.Positions.Get(victim.Id);

            ref var hp = ref World.Healths.Get(victim.Id);
            hp.Hp = Math.Max(0, hp.Hp - damage);
            int hpAfter = hp.Hp;

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
    }
}
