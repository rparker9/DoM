using System;

namespace Dungeon.Sim
{
    /// <summary>Lets the World remove a dead entity from every store without knowing their types.</summary>
    public interface IComponentStore
    {
        void Remove(int id);
    }

    /// <summary>
    /// Holds one kind of component for every entity that has it.
    /// Data is packed in a dense array (fast to loop over); a sparse array maps entity Id to its slot.
    /// </summary>
    /// <typeparam name="T">The component struct, e.g. Position.</typeparam>
    public sealed class ComponentStore<T> : IComponentStore where T : struct
    {
        int[] _slotOf = new int[64];    // entity Id -> dense slot + 1 (0 = not stored)
        int[] _idAt = new int[64];      // dense slot -> entity Id
        T[] _data = new T[64];          // dense slot -> component

        /// <summary>How many entities have this component.</summary>
        public int Count { get; private set; }

        /// <param name="id">Entity Id.</param>
        /// <returns>True if that entity has this component.</returns>
        public bool Has(int id) => id >= 0 && id < _slotOf.Length && _slotOf[id] != 0;

        /// <summary>The entity's component, by reference: changes to it are stored.</summary>
        /// <param name="id">Entity Id. Throws if the entity doesn't have this component.</param>
        public ref T Get(int id)
        {
            if (!Has(id)) throw new InvalidOperationException($"{typeof(T).Name} missing on entity {id}");
            return ref _data[_slotOf[id] - 1];
        }

        /// <summary>Adds the component, or replaces it if the entity already has one.</summary>
        /// <param name="id">Entity Id.</param>
        /// <param name="value">The component value.</param>
        public void Set(int id, T value)
        {
            if (Has(id)) { _data[_slotOf[id] - 1] = value; return; }

            if (id >= _slotOf.Length) Array.Resize(ref _slotOf, Math.Max(id + 1, _slotOf.Length * 2));
            if (Count == _data.Length)
            {
                Array.Resize(ref _data, Count * 2);
                Array.Resize(ref _idAt, Count * 2);
            }

            _data[Count] = value;
            _idAt[Count] = id;
            Count++;
            _slotOf[id] = Count;
        }

        /// <summary>Removes the component. Does nothing if the entity doesn't have it.</summary>
        /// <param name="id">Entity Id.</param>
        public void Remove(int id)
        {
            if (!Has(id)) return;

            // Fill the gap with the last item, so the dense array stays packed.
            int slot = _slotOf[id] - 1;
            int last = Count - 1;
            if (slot != last)
            {
                _data[slot] = _data[last];
                _idAt[slot] = _idAt[last];
                _slotOf[_idAt[slot]] = slot + 1;
            }
            _data[last] = default;
            Count--;
            _slotOf[id] = 0;
        }

        /// <summary>Entity Id in a dense slot. Loop i from 0 to Count - 1 to visit every entity in this store.</summary>
        /// <param name="slot">Dense slot index.</param>
        public int IdAt(int slot) => _idAt[slot];
    }
}
