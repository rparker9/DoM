using System;
using System.Collections.Generic;
using Dungeon.Sim;

namespace Dungeon.Playback
{
    /// <summary>An event with the time it plays.</summary>
    public sealed class TimedEvent
    {
        public SimEvent Event;
        public double PlaysAt, DoneAt;
        public bool Visible;
    }

    /// <summary>
    /// Decides when each event plays.
    ///   start = max(playbackTime, when each of its locks frees up, its cause's release time)
    ///   end   = start + duration; its locks are held until end.
    /// An event's release time is its hit frame: when the events it causes may start.
    /// </summary>
    public sealed class PlaybackScheduler
    {
        /// <summary>When true, one move plays at a time: a move's beats wait for the previous move's beats.</summary>
        public bool MovesPlayOneAtATime = true;

        readonly Dictionary<long, double> _lockedUntil = new Dictionary<long, double>();
        readonly Dictionary<int, double> _releaseAtByEventId = new Dictionary<int, double>();
        readonly List<long> _locks = new List<long>();
        readonly List<long> _expiredLocks = new List<long>();
        readonly List<int> _expiredEventIds = new List<int>();
        double _globalLockedUntil;

        // The move currently "on stage" when moves play one at a time.
        int _stageMoveGroup = -1;
        double _stageBusyUntil;

        /// <summary>When the last move beat scheduled so far finishes (the input gate waits for it).</summary>
        public double AllMovesDoneAt { get; private set; }
        /// <summary>When the last event scheduled so far finishes.</summary>
        public double EverythingDoneAt { get; private set; }

        /// <summary>Gives an event its start and end times, and marks its locks busy.</summary>
        /// <param name="e">The event, scheduled in sim order.</param>
        /// <param name="duration">How long it plays, in seconds.</param>
        /// <param name="release">Fraction of the duration (0 to 1) after which the events it causes may start.</param>
        /// <param name="playbackTime">Now, on the playback clock. Nothing starts before this.</param>
        /// <param name="isGlobal">True to wait for everything and make everything wait for it.</param>
        public TimedEvent Schedule(SimEvent e, double duration, double release, double playbackTime, bool isGlobal = false)
        {
            _locks.Clear();
            AnimationLocks.GetLocks(e, _locks);

            double start = Math.Max(playbackTime, _globalLockedUntil);
            if (isGlobal) start = Math.Max(start, EverythingDoneAt);
            for (int i = 0; i < _locks.Count; i++)
                if (_lockedUntil.TryGetValue(_locks[i], out var t) && t > start) start = t;
            if (e.CausedByEventId >= 0 && _releaseAtByEventId.TryGetValue(e.CausedByEventId, out var causeReady))
                start = Math.Max(start, causeReady);

            // One move at a time: a new move waits until the previous move's beats have all played.
            bool onStage = e.UsesMove && MovesPlayOneAtATime;
            if (onStage && e.MoveGroup != _stageMoveGroup) start = Math.Max(start, _stageBusyUntil);

            double end = start + duration;
            for (int i = 0; i < _locks.Count; i++) _lockedUntil[_locks[i]] = end;
            if (isGlobal) _globalLockedUntil = end;
            _releaseAtByEventId[e.Id] = start + duration * Math.Min(1.0, Math.Max(0.0, release));

            if (onStage)
            {
                _stageMoveGroup = e.MoveGroup;
                _stageBusyUntil = Math.Max(_stageBusyUntil, end);
            }
            if (e.UsesMove) AllMovesDoneAt = Math.Max(AllMovesDoneAt, end);
            EverythingDoneAt = Math.Max(EverythingDoneAt, end);

            return new TimedEvent { Event = e, PlaysAt = start, DoneAt = end };
        }

        /// <summary>When a lock frees up (0 if it's free now).</summary>
        public double LockedUntil(long key) => _lockedUntil.TryGetValue(key, out var t) ? t : 0;

        /// <summary>When an entity's animations are all done.</summary>
        public double EntityBusyUntil(Entity e) => LockedUntil(AnimationLocks.LockFor(e));

        /// <summary>Drops locks and release times already in the past; they can no longer delay anything.</summary>
        public void ForgetExpiredLocks(double playbackTime)
        {
            _expiredLocks.Clear();
            foreach (var kv in _lockedUntil) if (kv.Value <= playbackTime) _expiredLocks.Add(kv.Key);
            foreach (var k in _expiredLocks) _lockedUntil.Remove(k);

            _expiredEventIds.Clear();
            foreach (var kv in _releaseAtByEventId) if (kv.Value <= playbackTime) _expiredEventIds.Add(kv.Key);
            foreach (var id in _expiredEventIds) _releaseAtByEventId.Remove(id);
        }
    }
}
