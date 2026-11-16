namespace Dungeon.Sim
{
    // Components: plain data attached to entities. No logic here.

    /// <summary>Tile the entity stands on.</summary>
    public struct Position { public int X, Y; }

    /// <summary>Direction the entity faces, each -1, 0 or 1.</summary>
    public struct Facing { public int Dx, Dy; }

    public struct Health { public int Hp, Max; }

    /// <summary>100 = normal. Copied from the ActorDef so effects can change it later.</summary>
    public struct Speed { public int Percent; }

    /// <summary>The ActorDef this entity was spawned from: its name, stats and moves.</summary>
    public struct Kind { public ActorDef Def; }

    public enum Team { Party, Monsters }

    /// <summary>Which side the entity is on. Party members use the party AI; monsters use monster AI.</summary>
    public struct Faction { public Team Team; }

    public enum OrderType { Follow, Hold, AttackTarget }

    /// <summary>What a party member does when it isn't the leader.</summary>
    public struct Orders
    {
        public OrderType Type;
        public Entity Target;   // AttackTarget only
    }

    /// <summary>Breaks ties in the turn queue: lower initiative acts first on the same tick.</summary>
    public struct Actor { public int Initiative; }
}
