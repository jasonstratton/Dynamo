using System;
using System.Runtime.CompilerServices;
using ProtoCore.DSASM;

namespace ProtoCore.Lang.Replication
{
    /// <summary>
    /// Compact representation of a StackValue's type for caching purposes.
    /// Captures TypeId, Rank (for arrays), and IsArray flag.
    /// </summary>
    public readonly struct TypeSignature : IEquatable<TypeSignature>
    {
        public readonly int TypeId;
        public readonly int Rank;
        public readonly bool IsArray;

        /// <summary>
        /// Creates a TypeSignature from a StackValue.
        /// </summary>
        /// <param name="sv">The StackValue to extract type info from.</param>
        /// <param name="runtimeCore">Runtime core for array rank computation.</param>
        public TypeSignature(StackValue sv, RuntimeCore runtimeCore)
        {
            TypeId = sv.metaData.type;
            IsArray = sv.IsArray;
            // Only compute rank for arrays (expensive operation)
            Rank = IsArray ? Replicator.GetMaxReductionDepth(sv, runtimeCore) : 0;
        }

        /// <summary>
        /// Creates a TypeSignature with explicit values (for testing).
        /// </summary>
        public TypeSignature(int typeId, int rank, bool isArray)
        {
            TypeId = typeId;
            Rank = rank;
            IsArray = isArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                // FNV-1a inspired hash
                int hash = TypeId;
                hash = (hash * 397) ^ Rank;
                hash = (hash * 397) ^ (IsArray ? 1 : 0);
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TypeSignature other)
        {
            return TypeId == other.TypeId &&
                   Rank == other.Rank &&
                   IsArray == other.IsArray;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeSignature other && Equals(other);
        }

        public static bool operator ==(TypeSignature left, TypeSignature right) => left.Equals(right);
        public static bool operator !=(TypeSignature left, TypeSignature right) => !left.Equals(right);

        public override string ToString()
        {
            return $"Type:{TypeId}, Rank:{Rank}, IsArray:{IsArray}";
        }
    }
}
