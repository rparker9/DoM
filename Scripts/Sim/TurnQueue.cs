using System.Collections.Generic;

namespace Dungeon.Sim
{
    public struct TurnEntry
    {
        public long Tick;
        public int Initiative;
        public long InsertionOrder;   // final, fully stable tiebreak
        public Entity Entity;
    }

    /// <summary>
    /// Min-heap of upcoming turns keyed by (Tick, Initiative, InsertionOrder).
    /// Dead entities are not removed eagerly; the Simulation skips them when it takes them.
    /// </summary>
    public sealed class TurnQueue
    {
        readonly List<TurnEntry> _heap = new List<TurnEntry>();
        long _nextInsertionOrder;

        public int Count => _heap.Count;

        static bool Less(TurnEntry a, TurnEntry b)
        {
            if (a.Tick != b.Tick) return a.Tick < b.Tick;
            if (a.Initiative != b.Initiative) return a.Initiative < b.Initiative;
            return a.InsertionOrder < b.InsertionOrder;
        }

        public void Schedule(Entity e, long tick, int initiative)
        {
            _heap.Add(new TurnEntry { Tick = tick, Initiative = initiative, InsertionOrder = _nextInsertionOrder++, Entity = e });
            SiftUp(_heap.Count - 1);
        }

        public bool TryTakeNext(out TurnEntry entry)
        {
            if (_heap.Count == 0) { entry = default; return false; }
            entry = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0) SiftDown(0);
            return true;
        }

        /// <summary>Takes an entity's queued turn out of the queue. False if it has none.</summary>
        public bool TryRemove(Entity e, out TurnEntry entry)
        {
            for (int i = 0; i < _heap.Count; i++)
            {
                if (_heap[i].Entity != e) continue;
                entry = _heap[i];
                int last = _heap.Count - 1;
                _heap[i] = _heap[last];
                _heap.RemoveAt(last);
                if (i < _heap.Count) { SiftUp(i); SiftDown(i); }   // restore heap order around the gap
                return true;
            }
            entry = default;
            return false;
        }

        /// <summary>Upcoming turns in order (includes dead entries; filter with World.IsAlive).</summary>
        public void CopyOrdered(List<TurnEntry> into)
        {
            into.Clear();
            into.AddRange(_heap);
            into.Sort((a, b) => Less(a, b) ? -1 : (Less(b, a) ? 1 : 0));
        }

        void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!Less(_heap[i], _heap[p])) break;
                Swap(i, p);
                i = p;
            }
        }

        void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < n && Less(_heap[l], _heap[m])) m = l;
                if (r < n && Less(_heap[r], _heap[m])) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
        }

        void Swap(int a, int b)
        {
            var t = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = t;
        }
    }
}
