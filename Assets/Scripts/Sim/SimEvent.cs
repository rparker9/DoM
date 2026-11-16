namespace Dungeon.Sim
{
    public enum SimEventType
    {
        Stepped,    // Actor walked From -> To
        Faced,      // Actor turned to Dir (free action)
        UsedMove,   // Actor used Move toward Dir; Targets = every enemy hit (may be empty)
        Damaged,    // Target took Damage from Actor
        Healed,     // Target got Healing HP back from Actor
        Died        // Actor died at From
    }

    /// <summary>A record of one thing that happened in the sim. The only thing playback ever sees.</summary>
    public sealed class SimEvent
    {
        static readonly Entity[] NoTargets = new Entity[0];

        public int Id;                          // set by World.Record, in the order things happened
        public long Tick;                       // sim time it happened
        public SimEventType Type;
        public Entity Actor = Entity.None;      // who did it (for Died: who died)
        public Entity Target = Entity.None;     // Damaged only: who was hit
        public Entity[] Targets = NoTargets;    // UsedMove only
        public MoveDef Move;                    // UsedMove only
        public int FromX, FromY, ToX, ToY;      // tiles involved; From = To when only one tile matters
        public int DirX, DirY;                  // facing direction, for Stepped, Faced and UsedMove
        public int Damage, HpAfter;             // Damaged only
        public int Healing;                     // Healed only: HP actually restored
        public bool UsesMove;                   // true for UsedMove; the input gate waits for these
        public int CausedByEventId = -1;        // the event this one follows from, or -1

        public override string ToString() =>
            $"#{Id} t{Tick} {Type} actor={Actor} target={Target} ({FromX},{FromY})->({ToX},{ToY})" +
            (Move != null ? $" move={Move.Name}" : "") +
            (CausedByEventId >= 0 ? $" cause=#{CausedByEventId}" : "");
    }
}
