using System;

namespace Dungeon.Sim
{
    /// <summary>
    /// A handle to one thing in the game. Holds no data itself; components are looked up by <see cref="Id"/>.
    /// </summary>
    public readonly struct Entity : IEquatable<Entity>
    {
        /// <summary>Index into the component stores. Reused after the entity dies.</summary>
        public readonly int Id;

        /// <summary>Bumped each time an Id is reused, so an old handle never matches a new entity.</summary>
        public readonly int Gen;

        /// <param name="id">Index into the component stores.</param>
        /// <param name="gen">Generation of that index.</param>
        public Entity(int id, int gen) { Id = id; Gen = gen; }

        /// <summary>"No entity". Use <see cref="IsNone"/> to check for it.</summary>
        public static readonly Entity None = new Entity(-1, 0);
        public bool IsNone => Id < 0;

        public bool Equals(Entity other) => Id == other.Id && Gen == other.Gen;
        public override bool Equals(object obj) => obj is Entity e && Equals(e);
        public override int GetHashCode() => (Id * 397) ^ Gen;
        public static bool operator ==(Entity a, Entity b) => a.Equals(b);
        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);
        public override string ToString() => IsNone ? "None" : $"E{Id}.{Gen}";
    }
}
