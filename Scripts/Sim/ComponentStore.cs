using System;

namespace Dungeon.Sim
{
    public interface IComponentStore
    {
        void Remove(int id);
    }

    /// <summary>
    /// Sparse set: dense arrays for fast iteration, sparse lookup for O(1) Has/Get/Remove.
    /// Keyed by entity Id; the World is responsible for generation checks.
    /// </summary>
    public sealed class ComponentStore<T> : IComponentStore where T : struct
    {
        int[] _sparse = new int[64];   // entity id -> dense index + 1 (0 = absent)
        int[] _denseIds = new int[64];
        T[] _dense = new T[64];

        public int Count { get; private set; }

        public bool Has(int id) => id >= 0 && id < _sparse.Length && _sparse[id] != 0;

        public ref T Get(int id)
        {
            if (!Has(id)) throw new InvalidOperationException($"{typeof(T).Name} missing on entity {id}");
            return ref _dense[_sparse[id] - 1];
        }

        public void Set(int id, T value)
        {
            if (Has(id)) { _dense[_sparse[id] - 1] = value; return; }

            if (id >= _sparse.Length) Array.Resize(ref _sparse, Math.Max(id + 1, _sparse.Length * 2));
            if (Count == _dense.Length)
            {
                Array.Resize(ref _dense, Count * 2);
                Array.Resize(ref _denseIds, Count * 2);
            }

            _dense[Count] = value;
            _denseIds[Count] = id;
            Count++;
            _sparse[id] = Count;
        }

        public void Remove(int id)
        {
            if (!Has(id)) return;
            int idx = _sparse[id] - 1;
            int last = Count - 1;
            if (idx != last)
            {
                _dense[idx] = _dense[last];
                _denseIds[idx] = _denseIds[last];
                _sparse[_denseIds[idx]] = idx + 1;
            }
            _dense[last] = default;
            Count--;
            _sparse[id] = 0;
        }

        /// <summary>Entity id stored at a dense index (for iteration: 0..Count-1).</summary>
        public int IdAt(int denseIndex) => _denseIds[denseIndex];
        public ref T AtDense(int denseIndex) => ref _dense[denseIndex];
    }
}
