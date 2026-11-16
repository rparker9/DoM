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
        public const long SharedMoveLock = 3L << 56;

        public static long LockFor(Entity e) =>
            EntityTag | ((long)(e.Gen & 0xFFFFFFF) << 24) | (long)(e.Id & 0xFFFFFF);

        public static long LockFor(int x, int y) =>
            TileTag | ((long)(x & 0xFFFFFF) << 24) | (long)(y & 0xFFFFFF);

        public static void GetLocks(SimEvent e, List<long> into)
        {
            switch (e.Type)
            {
                case SimEventType.Stepped:
                    into.Add(LockFor(e.Actor));
                    into.Add(LockFor(e.FromX, e.FromY));
                    into.Add(LockFor(e.ToX, e.ToY));
                    break;
                case SimEventType.Faced:
                    into.Add(LockFor(e.Actor));
                    break;
                case SimEventType.UsedMove:
                    // The user and everyone it hits.
                    into.Add(LockFor(e.Actor));
                    for (int i = 0; i < e.Targets.Length; i++) into.Add(LockFor(e.Targets[i]));
                    break;
                case SimEventType.Damaged:
                    into.Add(LockFor(e.Target));
                    break;
                case SimEventType.Died:
                    // The tile too: anyone stepping into this tile later waits for the death to play.
                    into.Add(LockFor(e.Actor));
                    into.Add(LockFor(e.FromX, e.FromY));
                    break;
            }
        }
    }
}
