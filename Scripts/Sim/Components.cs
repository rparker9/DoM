namespace Dungeon.Sim
{
    public struct Position { public int X, Y; }
    public struct Facing { public int Dx, Dy; }
    public struct Health { public int Hp, Max; }

    /// <summary>100 = normal. Turn delay = energyCost * 100 / Percent.</summary>
    public struct Speed { public int Percent; }

    /// <summary>Which ActorDef this entity was spawned from (its stats and moves).</summary>
    public struct Kind { public ActorDef Def; }

    public enum Team { Party, Monsters }
    public struct Faction { public Team Team; }

    /// <summary>Which AI an actor uses when the player isn't controlling it.</summary>
    public enum ControlMode
    {
        Monster,   // monster AI
        Party      // party AI: follows the team's orders. Whichever member is the leader is player-controlled.
    }
    public struct Controller { public ControlMode Mode; }

    public enum OrderType { Follow, Hold, AttackTarget }
    public struct Orders { public OrderType Type; public Entity Target; }

    /// <summary>Lower initiative acts first when two actors share a tick.</summary>
    public struct Actor { public int Initiative; }

    public struct Name { public string Value; }
}
