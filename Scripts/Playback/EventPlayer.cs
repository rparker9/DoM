using System;
using System.Collections.Generic;
using Dungeon.Sim;

namespace Dungeon.Playback
{
    /// <summary>Implemented by the view layer. Animations must be driven by t, not real time,
    /// so playback can fast-forward without breaking them.</summary>
    public interface IEventAnimator
    {
        double DurationOf(SimEvent e);     // seconds at 1x
        void StartAnim(SimEvent e);
        void UpdateAnim(SimEvent e, float t); // t in 0..1
        void FinishAnim(SimEvent e);
    }

    /// <summary>
    /// Owns the playback clock. Sim events are appended continuously (no round boundaries);
    /// each is timed by the PlaybackScheduler, then started and finished in time order.
    /// </summary>
    public sealed class EventPlayer
    {
        public readonly PlaybackScheduler Scheduler = new PlaybackScheduler();
        readonly IEventAnimator _animator;
        readonly List<TimedEvent> _notStarted = new List<TimedEvent>();  // sorted by PlaysAt, then event Id
        readonly List<TimedEvent> _playing = new List<TimedEvent>();

        /// <summary>Events this returns false for play instantly (duration 0). Default: everything is visible.</summary>
        public Func<SimEvent, bool> IsVisible = e => true;

        public double PlaybackTime { get; private set; }
        public bool Idle => _notStarted.Count == 0 && _playing.Count == 0;
        public double TimeLeftToPlay => Math.Max(0, Scheduler.EverythingDoneAt - PlaybackTime);

        public EventPlayer(IEventAnimator animator) { _animator = animator; }

        public void Add(SimEvent e)
        {
            bool visible = IsVisible(e);
            double duration = visible ? Math.Max(0, _animator.DurationOf(e)) : 0;
            var s = Scheduler.Schedule(e, duration, PlaybackTime);
            s.Visible = visible;

            int i = _notStarted.Count;
            while (i > 0 && _notStarted[i - 1].PlaysAt > s.PlaysAt) i--;
            _notStarted.Insert(i, s);
        }

        public void Update(double dt)
        {
            PlaybackTime += Math.Max(0, dt);

            // Process every start/end boundary up to PlaybackTime in chronological order, so a large dt
            // (fast-forward) still finishes A before starting B when B waits on a lock A holds.
            while (true)
            {
                double nextStart = _notStarted.Count > 0 ? _notStarted[0].PlaysAt : double.PositiveInfinity;

                int endIdx = -1;
                double nextEnd = double.PositiveInfinity;
                for (int i = 0; i < _playing.Count; i++)
                {
                    var a = _playing[i];
                    if (a.DoneAt < nextEnd || (endIdx >= 0 && a.DoneAt == nextEnd && a.Event.Id < _playing[endIdx].Event.Id))
                    {
                        nextEnd = a.DoneAt;
                        endIdx = i;
                    }
                }

                if (endIdx >= 0 && nextEnd <= nextStart && nextEnd <= PlaybackTime)
                {
                    var done = _playing[endIdx];
                    _playing.RemoveAt(endIdx);
                    _animator.UpdateAnim(done.Event, 1f);
                    _animator.FinishAnim(done.Event);
                    continue;
                }
                if (nextStart <= PlaybackTime)
                {
                    var next = _notStarted[0];
                    _notStarted.RemoveAt(0);
                    _animator.StartAnim(next.Event);
                    _playing.Add(next);
                    continue;
                }
                break;
            }

            for (int i = 0; i < _playing.Count; i++)
            {
                var a = _playing[i];
                double len = a.DoneAt - a.PlaysAt;
                float t = len <= 0 ? 1f : (float)Math.Min(1.0, Math.Max(0.0, (PlaybackTime - a.PlaysAt) / len));
                _animator.UpdateAnim(a.Event, t);
            }

            if (Idle) Scheduler.ForgetExpiredLocks(PlaybackTime);
        }
    }
}
