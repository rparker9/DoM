using System.Collections.Generic;
using Dungeon.Sim;

namespace Dungeon.Playback
{
    /// <summary>
    /// Which locks an event holds while its animation plays: the entities and tiles it uses.
    /// Locks are long keys: a tag in the top byte, then entity or tile bits.
    /// </summary>
    public static class AnimationLocks
    {
        const long EntityTag = 1L << 56;
        const long TileTag = 2L << 56;

        /// <summary>The lock key for an entity.</summary>
        public static long LockFor(Entity e) =>
            EntityTag | ((long)(e.Gen & 0xFFFFFFF) << 24) | (long)(e.Id & 0xFFFFFF);

        /// <summary>The lock key for a tile.</summary>
        public static long LockFor(int x, int y) =>
            TileTag | ((long)(x & 0xFFFFFF) << 24) | (long)(y & 0xFFFFFF);

        /// <summary>Adds every lock the event holds while it plays.</summary>
        /// <param name="e">The event.</param>
        /// <param name="into">Locks are added to the end.</param>
        public static void GetLocks(SimEvent e, List<long> into)
        {
            switch (e.Type)
            {
                case SimEventType.Stepped:
                case SimEventType.Displaced:
                    into.Add(LockFor(e.Actor));
                    into.Add(LockFor(e.FromX, e.FromY));
                    into.Add(LockFor(e.ToX, e.ToY));
                    break;

                case SimEventType.Faced:
                    into.Add(LockFor(e.Actor));
                    break;

                case SimEventType.MoveCue:
                    AddCueLocks(e, into);
                    break;

                case SimEventType.Damaged:
                case SimEventType.Healed:
                    into.Add(LockFor(e.Target));
                    break;

                case SimEventType.Died:
                    // The tile too: anyone stepping into it later waits for the death to play.
                    into.Add(LockFor(e.Actor));
                    into.Add(LockFor(e.FromX, e.FromY));
                    break;
            }
        }

        /// <summary>
        /// A move's beats lock by phase, never the targets themselves: each target's reaction
        /// (Damaged, Healed...) locks it instead, and can start at the impact's hit frame.
        /// </summary>
        static void AddCueLocks(SimEvent e, List<long> into)
        {
            switch (e.Phase)
            {
                case CuePhase.WindUp:
                    into.Add(LockFor(e.Actor));   // the user is busy
                    break;

                case CuePhase.Travel:
                    into.Add(LockFor(e.ToX, e.ToY));   // nobody steps into the landing spot mid-flight
                    break;

                case CuePhase.Impact:
                    for (int oy = -e.Radius; oy <= e.Radius; oy++)
                    for (int ox = -e.Radius; ox <= e.Radius; ox++)
                        into.Add(LockFor(e.ToX + ox, e.ToY + oy));
                    break;
            }
        }
    }
}
