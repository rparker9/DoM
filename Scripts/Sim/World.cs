using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>All simulation state: entities, component stores, map, scheduler, RNG, pending events.</summary>
    public sealed class World
    {
        public readonly GameMap Map;
        public readonly Rng Rng;
        public readonly TurnQueue Turns = new TurnQueue();
        public long Tick { get; internal set; }
        /// <summary>The party member the player controls right now. Any party member can be the leader.</summary>
        public Entity Leader = Entity.None;

        /// <summary>Every party member ever spawned, in spawn order (check IsAlive).</summary>
        public readonly List<Entity> Party = new List<Entity>();

        public readonly ComponentStore<Position> Positions = new ComponentStore<Position>();
        public readonly ComponentStore<Facing> Facings = new ComponentStore<Facing>();
        public readonly ComponentStore<Health> Healths = new ComponentStore<Health>();
        public readonly ComponentStore<Kind> Kinds = new ComponentStore<Kind>();
        public readonly ComponentStore<Speed> Speeds = new ComponentStore<Speed>();
        public readonly ComponentStore<Faction> Factions = new ComponentStore<Faction>();
        public readonly ComponentStore<Controller> Controllers = new ComponentStore<Controller>();
        public readonly ComponentStore<Orders> Orders = new ComponentStore<Orders>();
        public readonly ComponentStore<Actor> Actors = new ComponentStore<Actor>();
        public readonly ComponentStore<Name> Names = new ComponentStore<Name>();

        readonly List<IComponentStore> _stores;
        readonly List<int> _gens = new List<int>();
        readonly List<bool> _alive = new List<bool>();
        readonly Stack<int> _freeIds = new Stack<int>();
        readonly List<SimEvent> _newEvents = new List<SimEvent>();
        int _nextEventId;
        int _nextInitiative;

        public World(GameMap map, Rng rng)
        {
            Map = map;
            Rng = rng;
            _stores = new List<IComponentStore>
            {
                Positions, Facings, Healths, Kinds, Speeds, Factions, Controllers, Orders, Actors, Names
            };
        }

        // ---------- entities ----------

        public Entity Create()
        {
            int id;
            if (_freeIds.Count > 0) { id = _freeIds.Pop(); _alive[id] = true; }
            else { id = _gens.Count; _gens.Add(0); _alive.Add(true); }
            return new Entity(id, _gens[id]);
        }

        public bool IsAlive(Entity e) =>
            e.Id >= 0 && e.Id < _gens.Count && _alive[e.Id] && _gens[e.Id] == e.Gen;

        /// <summary>Current handle for an id found while iterating a store.</summary>
        public Entity HandleOf(int id) => new Entity(id, _gens[id]);

        public void Destroy(Entity e)
        {
            if (!IsAlive(e)) return;
            if (Positions.Has(e.Id))
            {
                var p = Positions.Get(e.Id);
                if (Map.OccupantAt(p.X, p.Y) == e) Map.SetOccupant(p.X, p.Y, Entity.None);
            }
            foreach (var s in _stores) s.Remove(e.Id);
            _alive[e.Id] = false;
            _gens[e.Id]++;
            _freeIds.Push(e.Id);
        }

        public void Place(Entity e, int x, int y)
        {
            if (Positions.Has(e.Id))
            {
                var old = Positions.Get(e.Id);
                if (Map.OccupantAt(old.X, old.Y) == e) Map.SetOccupant(old.X, old.Y, Entity.None);
            }
            Positions.Set(e.Id, new Position { X = x, Y = y });
            Map.SetOccupant(x, y, e);
        }

        /// <summary>Creates an actor from its definition and gives it a turn right away (at the current tick).</summary>
        public Entity SpawnActor(ActorDef def, Team team, ControlMode mode, int x, int y)
        {
            var e = Create();
            int initiative = _nextInitiative++;
            Names.Set(e.Id, new Name { Value = def.Name });
            Kinds.Set(e.Id, new Kind { Def = def });
            Factions.Set(e.Id, new Faction { Team = team });
            Controllers.Set(e.Id, new Controller { Mode = mode });
            Orders.Set(e.Id, new Orders { Type = OrderType.Follow, Target = Entity.None });
            Healths.Set(e.Id, new Health { Hp = def.MaxHp, Max = def.MaxHp });
            Speeds.Set(e.Id, new Speed { Percent = def.SpeedPercent });
            Facings.Set(e.Id, new Facing { Dx = 0, Dy = -1 });
            Actors.Set(e.Id, new Actor { Initiative = initiative });
            Place(e, x, y);
            Turns.Schedule(e, Tick, initiative);

            if (team == Team.Party)
            {
                Party.Add(e);
                if (Leader.IsNone) Leader = e;   // the first party member starts as leader
            }
            return e;
        }

        public string NameOf(Entity e) => IsAlive(e) && Names.Has(e.Id) ? Names.Get(e.Id).Value : e.ToString();

        // ---------- events ----------

        public SimEvent Record(SimEvent ev)
        {
            ev.Id = _nextEventId++;
            ev.Tick = Tick;
            _newEvents.Add(ev);
            return ev;
        }

        /// <summary>Moves every event recorded since the last call into the list, in the order they happened.</summary>
        public void TakeNewEvents(List<SimEvent> into)
        {
            into.AddRange(_newEvents);
            _newEvents.Clear();
        }
    }
}
