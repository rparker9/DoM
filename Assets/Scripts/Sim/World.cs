using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>All game state: entities and their components, the map, the turn queue, and new events.</summary>
    public sealed class World
    {
        public readonly GameMap Map;
        public readonly Rng Rng;
        public readonly TurnQueue Turns = new TurnQueue();

        /// <summary>Sim time. 100 ticks = one normal turn.</summary>
        public long Tick { get; internal set; }

        /// <summary>The party member the player controls right now.</summary>
        public Entity Leader = Entity.None;

        /// <summary>Every party member ever spawned, in spawn order. Includes the dead.</summary>
        public readonly List<Entity> Party = new List<Entity>();

        // Component stores: one per component type.
        public readonly ComponentStore<Position> Positions = new ComponentStore<Position>();
        public readonly ComponentStore<Facing> Facings = new ComponentStore<Facing>();
        public readonly ComponentStore<Health> Healths = new ComponentStore<Health>();
        public readonly ComponentStore<Kind> Kinds = new ComponentStore<Kind>();
        public readonly ComponentStore<Speed> Speeds = new ComponentStore<Speed>();
        public readonly ComponentStore<Faction> Factions = new ComponentStore<Faction>();
        public readonly ComponentStore<Orders> Orders = new ComponentStore<Orders>();
        public readonly ComponentStore<Actor> Actors = new ComponentStore<Actor>();

        readonly IComponentStore[] _stores;
        readonly List<int> _gens = new List<int>();      // current generation of each Id
        readonly List<bool> _alive = new List<bool>();
        readonly Stack<int> _freeIds = new Stack<int>(); // Ids of dead entities, ready to reuse
        readonly List<SimEvent> _newEvents = new List<SimEvent>();
        int _nextEventId;
        int _nextInitiative;

        /// <param name="map">The tile grid.</param>
        /// <param name="rng">Random numbers for everything in the sim.</param>
        public World(GameMap map, Rng rng)
        {
            Map = map;
            Rng = rng;
            _stores = new IComponentStore[] { Positions, Facings, Healths, Kinds, Speeds, Factions, Orders, Actors };
        }

        // =====================================================================================
        //  Entities
        // =====================================================================================

        /// <summary>Makes a new entity with no components, reusing a dead entity's Id if one is free.</summary>
        public Entity Create()
        {
            int id;
            if (_freeIds.Count > 0) { id = _freeIds.Pop(); _alive[id] = true; }
            else { id = _gens.Count; _gens.Add(0); _alive.Add(true); }
            return new Entity(id, _gens[id]);
        }

        /// <returns>False for Entity.None, destroyed entities, and old handles to a reused Id.</returns>
        public bool IsAlive(Entity e) =>
            e.Id >= 0 && e.Id < _gens.Count && _alive[e.Id] && _gens[e.Id] == e.Gen;

        /// <summary>The current handle for an Id, e.g. one found by looping over a store with IdAt.</summary>
        /// <param name="id">Entity Id.</param>
        public Entity HandleOf(int id) => new Entity(id, _gens[id]);

        /// <summary>Removes the entity from the map and every store, and frees its Id.</summary>
        public void Destroy(Entity e)
        {
            if (!IsAlive(e)) return;
            ClearTile(e);
            foreach (var store in _stores) store.Remove(e.Id);
            _alive[e.Id] = false;
            _gens[e.Id]++;   // old handles to this Id now fail IsAlive
            _freeIds.Push(e.Id);
        }

        /// <summary>Moves the entity to a tile, keeping the map's occupancy in sync.</summary>
        /// <param name="e">Who moves.</param>
        /// <param name="x">Destination x.</param>
        /// <param name="y">Destination y.</param>
        public void Place(Entity e, int x, int y)
        {
            ClearTile(e);
            Positions.Set(e.Id, new Position { X = x, Y = y });
            Map.SetOccupant(x, y, e);
        }

        /// <summary>Takes the entity off the tile it stands on, if any.</summary>
        void ClearTile(Entity e)
        {
            if (!Positions.Has(e.Id)) return;
            var p = Positions.Get(e.Id);
            if (Map.OccupantAt(p.X, p.Y) == e) Map.SetOccupant(p.X, p.Y, Entity.None);
        }

        /// <summary>Creates an actor from its definition. Its first turn is right away, at the current tick.</summary>
        /// <param name="def">Name, stats and moves.</param>
        /// <param name="team">Party or Monsters. The first party member spawned becomes the leader.</param>
        /// <param name="x">Tile x. Must be free.</param>
        /// <param name="y">Tile y. Must be free.</param>
        public Entity SpawnActor(ActorDef def, Team team, int x, int y)
        {
            var e = Create();
            int initiative = _nextInitiative++;   // spawn order breaks ties

            Kinds.Set(e.Id, new Kind { Def = def });
            Factions.Set(e.Id, new Faction { Team = team });
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
                if (Leader.IsNone) Leader = e;
            }
            return e;
        }

        /// <returns>The actor's name, or its handle as text if it's dead.</returns>
        public string NameOf(Entity e) => IsAlive(e) && Kinds.Has(e.Id) ? Kinds.Get(e.Id).Def.Name : e.ToString();

        // =====================================================================================
        //  Events
        // =====================================================================================

        /// <summary>Stamps the event with an Id and the current tick, and keeps it for TakeNewEvents.</summary>
        /// <param name="ev">The event to record.</param>
        /// <returns>The same event, so its Id can be used as a cause.</returns>
        public SimEvent Record(SimEvent ev)
        {
            ev.Id = _nextEventId++;
            ev.Tick = Tick;
            _newEvents.Add(ev);
            return ev;
        }

        /// <summary>Moves every event recorded since the last call into the list, in the order they happened.</summary>
        /// <param name="into">Events are added to the end.</param>
        public void TakeNewEvents(List<SimEvent> into)
        {
            into.AddRange(_newEvents);
            _newEvents.Clear();
        }
    }
}
