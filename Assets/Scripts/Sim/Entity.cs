using System;

namespace Dungeon.Sim
{
    /// <summary>
    /// Entity handle. Id indexes component stores; Gen catches stale handles
    /// (the view holds handles to entities the sim has already destroyed).
    /// </summary>
    public readonly struct Entity : IEquatable<Entity>
    {
        public readonly int Id;
        public readonly int Gen;

        public Entity(int id, int gen) { Id = id; Gen = gen; }

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
