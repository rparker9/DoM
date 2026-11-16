using System;
using System.Collections.Generic;
using Dungeon.Sim;
using Dungeon.Playback;

namespace Dungeon.Api
{
    /// <summary>A read-only snapshot of one actor, for HUDs and for creating views.</summary>
    public struct ActorInfo
    {
        public Entity Entity;
        public bool IsAlive;
        public ActorDef Def;          // what it was spawned from: stats, moves, color
        public string Name;
        public Team Team;
        public int Hp, MaxHp;
        public int X, Y;
        public int FacingX, FacingY;
        public bool IsLeader;         // the party member the player controls right now
        public OrderType Orders;      // party members: what they do when they aren't the leader
    }

    /// <summary>One upcoming turn in the sim's turn queue.</summary>
    public struct TurnInfo
    {
        public Entity Actor;
        public string Name;
        public long Tick;
    }

    /// <summary>
    /// The whole game behind one class. Unity (or anything else) only needs this.
    ///
    /// <para><b>What's under the hood</b> (same names, so you can follow along):</para>
    /// <list type="bullet">
    ///   <item><see cref="World"/>: all game state: entities, map, turn queue.</item>
    ///   <item><see cref="Simulation"/>: the rules. <c>RunUntilPlayerTurn()</c> resolves every turn,
    ///         instantly, until the leader (the party member you control) is up. Everything that happens is recorded as a
    ///         <see cref="SimEvent"/>.</item>
    ///   <item><see cref="EventPlayer"/>: plays those events back on screen over time, through your
    ///         <see cref="IEventAnimator"/>. Events that lock no entity or tile in common play at the same time.</item>
    /// </list>
    ///
    /// <para><b>How to use it</b></para>
    /// <code>
    /// var game = new DungeonGame(seed: 1, width: 26, height: 18, animator: this);
    /// game.SurroundWithWalls();
    /// game.SpawnPartyMember(knightAsset.ToDef(), 4, 4);   // the first member starts as leader
    /// game.SpawnPartyMember(scoutAsset.ToDef(), 3, 4);
    /// game.SpawnMonster(slimeAsset.ToDef(), 12, 8);
    /// game.Begin();
    ///
    /// // every frame:
    /// if (stepKeyPressed) game.Step(1, 0);
    /// game.Update(Time.deltaTime);
    /// </code>
    /// </summary>
    public sealed class DungeonGame
    {
        // ---- the three parts under the hood ----
        readonly World _world;
        readonly Simulation _sim;
        readonly EventPlayer _eventPlayer;

        readonly List<Entity> _actors = new List<Entity>();
        readonly Dictionary<Entity, ActorDef> _defOf = new Dictionary<Entity, ActorDef>();  // kept after death
        readonly List<SimEvent> _newEvents = new List<SimEvent>();
        readonly List<TurnEntry> _turnScratch = new List<TurnEntry>();

        bool _begun;
        bool _hasQueuedAction;
        GameAction _queuedAction;

        // =====================================================================================
        //  Settings (change any time)
        // =====================================================================================

        /// <summary>Events farther than this many tiles from the leader play instantly.</summary>
        public int ViewRadius = 6;

        /// <summary>Seconds of animation that can be waiting before playback starts speeding up.</summary>
        public float CatchUpThreshold = 0.5f;
        /// <summary>How quickly playback speeds up for each second waiting beyond the threshold.</summary>
        public float CatchUpGain = 2f;
        /// <summary>The fastest that automatic catch-up plays (1 = normal speed).</summary>
        public float MaxCatchUpSpeed = 4f;
        /// <summary>Playback speed while <see cref="FastForward"/> is on or an action is queued.</summary>
        public float FastForwardSpeed = 4f;

        /// <summary>Set to true while the player holds a fast-forward key.</summary>
        public bool FastForward;

        /// <summary>When true, moves play one at a time, even between unrelated actors.</summary>
        public bool MovesPlayOneAtATime
        {
            get => _eventPlayer.Scheduler.MovesPlayOneAtATime;
            set => _eventPlayer.Scheduler.MovesPlayOneAtATime = value;
        }

        // =====================================================================================
        //  1. Create the floor
        // =====================================================================================

        /// <param name="animator">Whatever draws the game. It gets told when to start, update and finish
        /// each event's animation.</param>
        public DungeonGame(int seed, int width, int height, IEventAnimator animator)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            _world = new World(new GameMap(width, height), new Rng((ulong)seed));
            _sim = new Simulation(_world);
            _eventPlayer = new EventPlayer(animator) { IsVisible = IsNearLeader };
        }

        public int Width => _world.Map.Width;
        public int Height => _world.Map.Height;

        public bool IsWall(int x, int y) => _world.Map.IsWall(x, y);
        /// <summary>Not a wall and nobody standing there.</summary>
        public bool IsFree(int x, int y) => _world.Map.IsFree(x, y);

        public void SetWall(int x, int y, bool isWall = true)
        {
            RequireNotBegun(nameof(SetWall));
            _world.Map.SetWall(x, y, isWall);
        }

        /// <summary>Turns the outer edge of the map into walls.</summary>
        public void SurroundWithWalls()
        {
            for (int x = 0; x < Width; x++) { SetWall(x, 0); SetWall(x, Height - 1); }
            for (int y = 0; y < Height; y++) { SetWall(0, y); SetWall(Width - 1, y); }
        }

        /// <summary>Places walls on random tiles, keeping a square area around (keepClearX, keepClearY) open.</summary>
        public void AddRandomWalls(int count, int keepClearX, int keepClearY, int keepClearRadius = 2)
        {
            for (int i = 0; i < count; i++)
            {
                int x = RandomNumber(2, Width - 2), y = RandomNumber(2, Height - 2);
                if (Rules.Chebyshev(x, y, keepClearX, keepClearY) <= keepClearRadius) continue;
                if (!IsFree(x, y)) continue;
                SetWall(x, y);
            }
        }

        /// <summary>Random whole number from min up to (not including) maxExclusive. Same seed = same numbers.</summary>
        public int RandomNumber(int min, int maxExclusive) => _world.Rng.Range(min, maxExclusive);

        /// <summary>Finds a random free tile at least <paramref name="minDistance"/> tiles from (awayFromX, awayFromY).</summary>
        public bool TryFindFreeTile(int awayFromX, int awayFromY, int minDistance, out int x, out int y)
        {
            for (int attempt = 0; attempt < 500; attempt++)
            {
                x = RandomNumber(1, Width - 1);
                y = RandomNumber(1, Height - 1);
                if (IsFree(x, y) && Rules.Chebyshev(x, y, awayFromX, awayFromY) >= minDistance) return true;
            }
            x = y = -1;
            return false;
        }

        // =====================================================================================
        //  2. Spawn actors (before Begin)
        // =====================================================================================

        /// <summary>
        /// Adds a member to your party. All members are equal: whoever you control is the leader,
        /// and the rest use the party AI. The first member spawned starts as leader.
        /// </summary>
        public Entity SpawnPartyMember(ActorDef def, int x, int y) => Spawn(def, Team.Party, ControlMode.Party, x, y);

        public Entity SpawnMonster(ActorDef def, int x, int y) => Spawn(def, Team.Monsters, ControlMode.Monster, x, y);

        Entity Spawn(ActorDef def, Team team, ControlMode mode, int x, int y)
        {
            RequireNotBegun("Spawn");
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (!IsFree(x, y)) throw new ArgumentException($"Can't spawn {def.Name} at ({x},{y}): the tile is a wall or taken.");

            var e = _world.SpawnActor(def, team, mode, x, y);
            _actors.Add(e);
            _defOf[e] = def;
            return e;
        }

        // =====================================================================================
        //  3. Start
        // =====================================================================================

        /// <summary>Starts the game: runs turns until the first player turn.</summary>
        public void Begin()
        {
            if (_begun) return;
            if (_world.Party.Count == 0) 
                throw new InvalidOperationException("Spawn at least one party member before calling Begin().");
            _begun = true;
            _sim.RunUntilPlayerTurn();
            PlayNewEvents();
        }

        // =====================================================================================
        //  4. The player's turn
        //
        //  Step, UseMove and Wait can be called at any time. If the player can't act yet
        //  (animations still playing), the action is queued and runs as soon as they can.
        //  Only one action is queued; a newer one replaces it.
        // =====================================================================================

        /// <summary>
        /// The input gate. True when the controlled actor has finished all its animations
        /// and every move recorded so far has finished playing.
        /// </summary>
        public bool CanPlayerAct
        {
            get
            {
                if (_sim.Status != SimStatus.AwaitingInput || _sim.ActorWaitingForInput.IsNone) return false;
                var scheduler = _eventPlayer.Scheduler;
                double controlledActorFreeAt = scheduler.EntityBusyUntil(_sim.ActorWaitingForInput);
                double allMovesDoneAt = scheduler.AllMovesDoneAt;
                return _eventPlayer.PlaybackTime >= Math.Max(controlledActorFreeAt, allMovesDoneAt);
            }
        }

        /// <summary>The actor whose turn it is in the sim. Always the leader while the game is running.</summary>
        public Entity WhoseTurn => _sim.ActorWaitingForInput;

        public bool IsGameOver => _sim.Status == SimStatus.GameOver;
        public bool HasQueuedAction => _hasQueuedAction;

        /// <summary>Walk one tile. Stepping into a wall or someone just turns you (no turn used).</summary>
        public void Step(int dx, int dy) => DoOrQueue(GameAction.Step(dx, dy));

        /// <summary>Use a move in the direction you're facing. Slot 0 = first move.</summary>
        public void UseMove(int slot)
        {
            if (!_sim.HasMove(_sim.ActorWaitingForInput, slot)) return;   // no move in that slot
            DoOrQueue(GameAction.UseMove(slot));
        }

        /// <summary>Skip a turn.</summary>
        public void Wait() => DoOrQueue(GameAction.Wait());

        /// <summary>Turn without moving. Free, but only works when <see cref="CanPlayerAct"/> is true.</summary>
        public void Face(int dx, int dy)
        {
            if (!CanPlayerAct) return;
            _sim.Face(dx, dy);
            PlayNewEvents();
        }

        void DoOrQueue(GameAction action)
        {
            if (!_begun || IsGameOver) return;
            if (CanPlayerAct) Execute(action);
            else { _queuedAction = action; _hasQueuedAction = true; }
        }

        void Execute(GameAction action)
        {
            if (_sim.ActorWaitingForInput.IsNone) return;

            bool blockedStep = action.Type == ActionType.Step
                            && !Rules.CanStep(_world, _sim.ActorWaitingForInput, action.Dx, action.Dy);
            if (blockedStep)
            {
                _sim.Face(action.Dx, action.Dy);
                PlayNewEvents();
                return;
            }

            // Resolves this action AND every other turn up to the next player turn.
            if (_sim.DoPlayerAction(action)) PlayNewEvents();
        }

        // =====================================================================================
        //  5. The party (free: no turn used, works any time)
        //
        //  Every member is equal. The leader is just whoever you control right now.
        //  Everyone else follows their orders, and "Follow" means follow the current leader.
        // =====================================================================================

        /// <summary>Every party member ever spawned, in spawn order (check <see cref="IsAlive"/>).</summary>
        public IReadOnlyList<Entity> Party => _world.Party;

        /// <summary>The party member you control right now.</summary>
        public Entity Leader => _world.Leader;

        /// <summary>
        /// True when you're allowed to switch leader: it's the leader's turn and nothing is
        /// animating anywhere on screen.
        /// </summary>
        public bool CanSwitchLeader => _begun && CanPlayerAct && _eventPlayer.TimeLeftToPlay <= 0;

        /// <summary>
        /// Free action: hand the current turn to another party member, who becomes the leader
        /// and acts right now. The old leader takes the new leader's place in the turn queue and
        /// goes back to the party AI. Only works when <see cref="CanSwitchLeader"/> is true.
        /// Returns false if the switch didn't happen.
        /// </summary>
        public bool SetLeader(Entity member)
        {
            if (!CanSwitchLeader) return false;
            if (!_sim.SwapLeader(member)) return false;
            _hasQueuedAction = false;   // a queued action was meant for the old leader
            return true;
        }

        /// <summary>Switch to the next living party member (wraps around). Same rules as <see cref="SetLeader"/>.</summary>
        public bool SwitchToNextMember()
        {
            var party = _world.Party;
            int current = party.IndexOf(_world.Leader);
            for (int i = 1; i < party.Count; i++)
            {
                var next = party[(current + i) % party.Count];
                if (_world.IsAlive(next)) return SetLeader(next);
            }
            return false;
        }

        /// <summary>Give the whole party an order: Follow (the current leader) or Hold.
        /// Everyone gets it, the leader too, so it applies to them once you switch away.</summary>
        public void OrderParty(OrderType order)
        {
            foreach (var member in _world.Party) OrderMember(member, order);
        }

        /// <summary>Tell the whole party to go after one enemy.</summary>
        public void OrderPartyToAttack(Entity target)
        {
            foreach (var member in _world.Party) OrderMemberToAttack(member, target);
        }

        /// <summary>Give one party member its own order: Follow or Hold.</summary>
        public void OrderMember(Entity member, OrderType order)
        {
            if (!_world.IsAlive(member)) return;
            _sim.SetOrders(member, order, Entity.None);
        }

        /// <summary>Tell one party member to go after one enemy.</summary>
        public void OrderMemberToAttack(Entity member, Entity target)
        {
            if (!_world.IsAlive(member) || !_world.IsAlive(target)) return;
            _sim.SetOrders(member, OrderType.AttackTarget, target);
        }

        /// <summary>Closest enemy to the leader, or Entity.None.</summary>
        public Entity NearestEnemyToLeader()
        {
            if (!_world.IsAlive(_world.Leader)) return Entity.None;
            var leaderPos = _world.Positions.Get(_world.Leader.Id);
            int bestDistance = int.MaxValue;
            var nearest = Entity.None;
            foreach (var e in _actors)
            {
                if (!_world.IsAlive(e) || !Rules.IsHostile(_world, _world.Leader, e)) continue;
                var pos = _world.Positions.Get(e.Id);
                int distance = Rules.Chebyshev(leaderPos.X, leaderPos.Y, pos.X, pos.Y);
                if (distance < bestDistance) { bestDistance = distance; nearest = e; }
            }
            return nearest;
        }

        // =====================================================================================
        //  6. Every frame
        // =====================================================================================

        /// <summary>Plays animations forward and runs a queued action once the player can act.
        /// Call once per frame.</summary>
        public void Update(float deltaTime)
        {
            PlaybackSpeed = ChoosePlaybackSpeed();
            _eventPlayer.Update(deltaTime * PlaybackSpeed);

            if (_hasQueuedAction && CanPlayerAct)
            {
                _hasQueuedAction = false;
                Execute(_queuedAction);
            }
        }

        /// <summary>Current playback speed (1 = normal).</summary>
        public float PlaybackSpeed { get; private set; } = 1f;
        /// <summary>Playback clock in seconds. Not real time: it runs faster when playback speeds up.</summary>
        public double PlaybackTime => _eventPlayer.PlaybackTime;
        /// <summary>Seconds of animation still waiting to play.</summary>
        public double TimeLeftToPlay => _eventPlayer.TimeLeftToPlay;

        float ChoosePlaybackSpeed()
        {
            float speed = 1f;
            float timeLeft = (float)_eventPlayer.TimeLeftToPlay;
            if (timeLeft > CatchUpThreshold)
                speed = Math.Min(MaxCatchUpSpeed, 1f + (timeLeft - CatchUpThreshold) * CatchUpGain);
            if (FastForward || _hasQueuedAction) speed = Math.Max(speed, FastForwardSpeed);
            return speed;
        }

        /// <summary>Hands every newly recorded event to the EventPlayer, in the order they happened.</summary>
        void PlayNewEvents()
        {
            _newEvents.Clear();
            _world.TakeNewEvents(_newEvents);
            for (int i = 0; i < _newEvents.Count; i++) _eventPlayer.Add(_newEvents[i]);
        }

        /// <summary>Unseen events still play, but instantly, so far-off monsters don't make you wait.
        /// "Seen" means near the current leader.</summary>
        bool IsNearLeader(SimEvent e)
        {
            if (!_world.IsAlive(_world.Leader)) return true;
            var lp = _world.Positions.Get(_world.Leader.Id);
            return Rules.Chebyshev(lp.X, lp.Y, e.FromX, e.FromY) <= ViewRadius
                || Rules.Chebyshev(lp.X, lp.Y, e.ToX, e.ToY) <= ViewRadius;
        }

        public void EndGame()
        {
            _sim.EndGame();
            PlayNewEvents();
        }

        // =====================================================================================
        //  7. Reading the game state
        //
        //  These read the SIM, which runs ahead of the screen. Good for HUDs and setup.
        //  To draw actors moving, use the animation callbacks instead.
        // =====================================================================================

        /// <summary>Everyone ever spawned, including the dead (check <see cref="IsAlive"/>).</summary>
        public IReadOnlyList<Entity> AllActors => _actors;
        public bool IsAlive(Entity e) => _world.IsAlive(e);
        /// <summary>The sim's clock, in ticks. 100 ticks = one normal turn.</summary>
        public long Tick => _world.Tick;

        public ActorInfo GetActor(Entity e)
        {
            _defOf.TryGetValue(e, out var def);
            var info = new ActorInfo { Entity = e, Def = def, Name = def != null ? def.Name : e.ToString() };
            if (!_world.IsAlive(e)) return info;

            var pos = _world.Positions.Get(e.Id);
            var hp = _world.Healths.Get(e.Id);
            var facing = _world.Facings.Get(e.Id);
            info.IsAlive = true;
            info.Name = _world.NameOf(e);
            info.Team = _world.Factions.Get(e.Id).Team;
            info.Hp = hp.Hp; info.MaxHp = hp.Max;
            info.X = pos.X; info.Y = pos.Y;
            info.FacingX = facing.Dx; info.FacingY = facing.Dy;
            info.IsLeader = e == _world.Leader;
            info.Orders = _world.Orders.Get(e.Id).Type;
            return info;
        }

        /// <summary>The next turns in the sim, soonest first. Doesn't include <see cref="WhoseTurn"/>.</summary>
        public void GetUpcomingTurns(List<TurnInfo> into, int max)
        {
            into.Clear();
            _world.Turns.CopyOrdered(_turnScratch);
            foreach (var entry in _turnScratch)
            {
                if (into.Count >= max) break;
                if (!_world.IsAlive(entry.Entity)) continue;   // dead actors' turns stay queued until reached
                into.Add(new TurnInfo { Actor = entry.Entity, Name = _world.NameOf(entry.Entity), Tick = entry.Tick });
            }
        }

        void RequireNotBegun(string what)
        {
            if (_begun) throw new InvalidOperationException($"{what} only works before Begin() is called.");
        }
    }
}
