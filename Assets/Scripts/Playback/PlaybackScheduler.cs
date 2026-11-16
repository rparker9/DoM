using System;
using System.Collections.Generic;
using Dungeon.Sim;

namespace Dungeon.Playback
{
    public sealed class TimedEvent
    {
        public SimEvent Event;
        public double PlaysAt, DoneAt;
        public bool Visible;
    }

    /// <summary>
    /// Decides when each event plays. An event locks the entities and tiles it uses until it's done.
    ///   start = max(playbackTime, when each of its locks frees up, when its cause is done)
    ///   end   = start + duration; its locks are then held until end.
    /// Events that need no common lock play at the same time; events that do play in sim order.
    /// </summary>
    public sealed class PlaybackScheduler
    {
        /// <summary>When true, every move also takes one shared lock, so moves play one at a time.</summary>
        public bool MovesPlayOneAtATime = true;

        readonly Dictionary<long, double> _lockedUntil = new Dictionary<long, double>();
        readonly Dictionary<int, double> _doneAtByEventId = new Dictionary<int, double>();
        readonly List<long> _locks = new List<long>();
        readonly List<long> _expiredLocks = new List<long>();
        readonly List<int> _expiredEventIds = new List<int>();
        double _globalLockedUntil;

        /// <summary>When the last move scheduled so far finishes (the input gate waits for it).</summary>
        public double AllMovesDoneAt { get; private set; }
        /// <summary>When the last event scheduled so far finishes.</summary>
        public double EverythingDoneAt { get; private set; }

        public TimedEvent Schedule(SimEvent e, double duration, double playbackTime, bool isGlobal = false)
        {
            _locks.Clear();
            AnimationLocks.GetLocks(e, _locks);
            if (e.UsesMove && MovesPlayOneAtATime) _locks.Add(AnimationLocks.SharedMoveLock);

            double start = Math.Max(playbackTime, _globalLockedUntil);
            if (isGlobal) start = Math.Max(start, EverythingDoneAt);   // global: wait for everything
            for (int i = 0; i < _locks.Count; i++)
                if (_lockedUntil.TryGetValue(_locks[i], out var t) && t > start) start = t;
            if (e.CausedByEventId >= 0 && _doneAtByEventId.TryGetValue(e.CausedByEventId, out var causeEnd) && causeEnd > start)
                start = causeEnd;

            double end = start + duration;
            for (int i = 0; i < _locks.Count; i++) _lockedUntil[_locks[i]] = end;
            if (isGlobal) _globalLockedUntil = end;
            _doneAtByEventId[e.Id] = end;
            if (e.UsesMove && end > AllMovesDoneAt) AllMovesDoneAt = end;
            if (end > EverythingDoneAt) EverythingDoneAt = end;

            return new TimedEvent { Event = e, PlaysAt = start, DoneAt = end };
        }

        public double LockedUntil(long key) => _lockedUntil.TryGetValue(key, out var t) ? t : 0;
        public double EntityBusyUntil(Entity e) => LockedUntil(AnimationLocks.LockFor(e));

        /// <summary>Drops locks that have already expired; they can no longer delay anything.</summary>
        public void ForgetExpiredLocks(double playbackTime)
        {
            _expiredLocks.Clear();
            foreach (var kv in _lockedUntil) if (kv.Value <= playbackTime) _expiredLocks.Add(kv.Key);
            foreach (var k in _expiredLocks) _lockedUntil.Remove(k);

            _expiredEventIds.Clear();
            foreach (var kv in _doneAtByEventId) if (kv.Value <= playbackTime) _expiredEventIds.Add(kv.Key);
            foreach (var id in _expiredEventIds) _doneAtByEventId.Remove(id);
        }
    }
}
