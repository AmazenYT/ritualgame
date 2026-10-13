#if !UNITY_6000_2_OR_NEWER
using System;

namespace RealPhysicsPlacer.EditorTools
{
    /// <summary>
    /// Compatibility representation for Unity object IDs before EntityId was
    /// introduced in Unity 6.2. Keeping the same name lets the editor code use
    /// the native 64-bit EntityId on newer Unity versions and an instance ID on
    /// older versions without maintaining two implementations.
    /// </summary>
    internal readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        private readonly int value;

        internal static readonly EntityId None = new EntityId(0);

        private EntityId(int value)
        {
            this.value = value;
        }

        public int CompareTo(EntityId other)
        {
            return value.CompareTo(other.value);
        }

        public bool Equals(EntityId other)
        {
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value;
        }

        public static implicit operator int(EntityId id)
        {
            return id.value;
        }

        public static implicit operator EntityId(int value)
        {
            return new EntityId(value);
        }
    }

    internal static class RealPhysicsPlacerEntityIdCompatibility
    {
        internal static EntityId GetEntityId(this UnityEngine.Object target)
        {
            return target != null ? target.GetInstanceID() : EntityId.None;
        }
    }
}
#endif
