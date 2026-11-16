namespace Dungeon.Sim
{
    public enum SimEventType { Stepped, Faced, UsedMove, Damaged, Died }

    /// <summary>
    /// A record of one thing that happened. The only thing the view ever sees.
    /// Every event fills From/To with the tiles it concerns.
    ///   Stepped:  Actor walks From -> To.
    ///   Faced:    Actor turns to Dir (free action). From = To = actor tile.
    ///   UsedMove: Actor (at From) uses Move toward Dir. To = the tile in front.
    ///             Targets = every enemy hit (may be empty). UsesMove = true.
    ///   Damaged:  Target (at From) takes Damage from Actor; HpAfter = HP after.
    ///   Died:     Actor dies at From.
    /// </summary>
    public sealed class SimEvent
    {
        static readonly Entity[] NoTargets = new Entity[0];

        public int Id;
        public long Tick;
        public SimEventType Type;
        public Entity Actor = Entity.None;
        public Entity Target = Entity.None;
        public Entity[] Targets = NoTargets;
        public MoveDef Move;
        public int FromX, FromY, ToX, ToY;
        public int DirX, DirY;
        public int Damage, HpAfter;
        public bool UsesMove;
        public int CausedByEventId = -1;   // Id of the event this one follows from (-1 = none)

        public override string ToString() =>
            $"#{Id} t{Tick} {Type} actor={Actor} target={Target} ({FromX},{FromY})->({ToX},{ToY})" +
            (Move != null ? $" move={Move.Name}" : "") +
            (UsesMove ? " [uses move]" : "") + (CausedByEventId >= 0 ? $" cause=#{CausedByEventId}" : "");
    }
}
