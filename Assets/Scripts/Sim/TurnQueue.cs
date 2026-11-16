using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>One upcoming turn in the queue.</summary>
    public struct TurnEntry
    {
        public long Tick;            // when the turn happens
        public int Initiative;       // tiebreak: lower goes first on the same tick
        public long InsertionOrder;  // last tiebreak, so the order is always the same
        public Entity Entity;
    }

    /// <summary>
    /// Who acts next: a min-heap sorted by (Tick, Initiative, InsertionOrder).
    /// Dead entities' turns stay in the queue; the Simulation skips them when it reaches them.
    /// </summary>
    public sealed class TurnQueue
    {
        readonly List<TurnEntry> _heap = new List<TurnEntry>();
        long _nextInsertionOrder;

        public int Count => _heap.Count;

        /// <summary>Adds a turn.</summary>
        /// <param name="e">Who acts.</param>
        /// <param name="tick">When they act.</param>
        /// <param name="initiative">Tiebreak for turns on the same tick: lower goes first.</param>
        public void Schedule(Entity e, long tick, int initiative)
        {
            _heap.Add(new TurnEntry { Tick = tick, Initiative = initiative, InsertionOrder = _nextInsertionOrder++, Entity = e });
            SiftUp(_heap.Count - 1);
        }

        /// <summary>Removes and returns the soonest turn.</summary>
        /// <param name="entry">The turn taken.</param>
        /// <returns>False if the queue is empty.</returns>
        public bool TryTakeNext(out TurnEntry entry)
        {
            if (_heap.Count == 0) { entry = default; return false; }
            entry = _heap[0];
            RemoveAt(0);
            return true;
        }

        /// <summary>Removes an entity's queued turn.</summary>
        /// <param name="e">Whose turn to remove.</param>
        /// <param name="entry">The turn removed.</param>
        /// <returns>False if the entity has no turn in the queue.</returns>
        public bool TryRemove(Entity e, out TurnEntry entry)
        {
            for (int i = 0; i < _heap.Count; i++)
            {
                if (_heap[i].Entity != e) continue;
                entry = _heap[i];
                RemoveAt(i);
                return true;
            }
            entry = default;
            return false;
        }

        /// <summary>Copies every queued turn into a list, soonest first. Includes dead entities' turns.</summary>
        /// <param name="into">Cleared, then filled.</param>
        public void CopyOrdered(List<TurnEntry> into)
        {
            into.Clear();
            into.AddRange(_heap);
            into.Sort((a, b) => Less(a, b) ? -1 : (Less(b, a) ? 1 : 0));
        }

        // ---- heap internals ----

        static bool Less(TurnEntry a, TurnEntry b)
        {
            if (a.Tick != b.Tick) return a.Tick < b.Tick;
            if (a.Initiative != b.Initiative) return a.Initiative < b.Initiative;
            return a.InsertionOrder < b.InsertionOrder;
        }

        /// <summary>Removes the item at index i, moving the last item into its place and restoring heap order.</summary>
        void RemoveAt(int i)
        {
            int last = _heap.Count - 1;
            _heap[i] = _heap[last];
            _heap.RemoveAt(last);
            if (i < _heap.Count) { SiftUp(i); SiftDown(i); }
        }

        void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Less(_heap[i], _heap[parent])) break;
                Swap(i, parent);
                i = parent;
            }
        }

        void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < n && Less(_heap[left], _heap[smallest])) smallest = left;
                if (right < n && Less(_heap[right], _heap[smallest])) smallest = right;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
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
