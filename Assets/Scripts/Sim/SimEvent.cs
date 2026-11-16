namespace Dungeon.Sim
{
    public enum SimEventType
    {
        Stepped,     // Actor walked From -> To
        Faced,       // Actor turned to Dir (free action)
        MoveCue,     // a visual beat of a move (see CuePhase); changes nothing in the game
        Damaged,     // Target took Damage from Actor
        Healed,      // Target got Healing HP back from Actor
        Displaced,   // Actor was pushed From -> To (knockback)
        Died         // Actor died at From
    }

    /// <summary>The beats of a move, in order. Travel only happens for projectiles and beams.</summary>
    public enum CuePhase { WindUp, Travel, Impact }

    /// <summary>A record of one thing that happened in the sim. The only thing playback ever sees.</summary>
    public sealed class SimEvent
    {
        static readonly Entity[] NoTargets = new Entity[0];

        public int Id;                          // set by World.Record, in the order things happened
        public long Tick;                       // sim time it happened
        public SimEventType Type;
        public Entity Actor = Entity.None;      // who did it (Died, Displaced: who it happened to)
        public Entity Target = Entity.None;     // Damaged, Healed: who was affected
        public int FromX, FromY, ToX, ToY;      // tiles involved; From = To when only one tile matters
        public int DirX, DirY;                  // facing direction
        public int Damage, Healing, HpAfter;    // Damaged / Healed
        public int CausedByEventId = -1;        // the event this one follows from, or -1

        // MoveCue only
        public CuePhase Phase;
        public MoveDef Move;
        public Delivery Delivery;
        public int Radius;                      // Impact: tiles around To that the impact covers
        public Entity[] Targets = NoTargets;    // Impact: everyone affected
        public bool UsesMove;                   // true for every MoveCue; the input gate waits for these
        public int MoveGroup = -1;              // Id of the move's WindUp, shared by all its cues

        public override string ToString() =>
            $"#{Id} t{Tick} {Type}{(Type == SimEventType.MoveCue ? " " + Phase : "")} actor={Actor} target={Target} " +
            $"({FromX},{FromY})->({ToX},{ToY})" +
            (Move != null ? $" move={Move.Name}" : "") +
            (CausedByEventId >= 0 ? $" cause=#{CausedByEventId}" : "");
    }
}
