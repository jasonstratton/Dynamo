using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ProtoCore.Lang.Replication
{
    /// <summary>
    /// Immutable key for dispatch cache lookups.
    /// Uniquely identifies a dispatch scenario based on function identity,
    /// argument types, and replication guides.
    /// </summary>
    public readonly struct DispatchKey : IEquatable<DispatchKey>
    {
        // Function identity
        private readonly string _methodName;
        private readonly int _classScope;

        // Type signatures (pre-hashed for fast comparison)
        private readonly int _argTypesHash;
        private readonly TypeSignature[] _argTypes;

        // Replication guides (flattened for efficiency)
        private readonly int _guidesHash;
        private readonly int[] _flattenedGuides;

        /// <summary>
        /// Creates a DispatchKey for cache lookup.
        /// </summary>
        /// <param name="methodName">The method being called.</param>
        /// <param name="classScope">The class scope (-1 for global).</param>
        /// <param name="argTypes">Type signatures of all arguments.</param>
        /// <param name="guides">Replication guides (may be null).</param>
        public DispatchKey(
            string methodName,
            int classScope,
            TypeSignature[] argTypes,
            List<List<ReplicationGuide>> guides)
        {
            _methodName = methodName ?? string.Empty;
            _classScope = classScope;
            _argTypes = argTypes ?? Array.Empty<TypeSignature>();
            _argTypesHash = ComputeTypesHash(_argTypes);
            _flattenedGuides = FlattenGuides(guides);
            _guidesHash = ComputeGuidesHash(_flattenedGuides);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261; // FNV offset basis
                hash = (hash ^ _methodName.GetHashCode()) * 16777619;
                hash = (hash ^ _classScope) * 16777619;
                hash = (hash ^ _argTypesHash) * 16777619;
                hash = (hash ^ _guidesHash) * 16777619;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DispatchKey other)
        {
            // Fast path: check hashes and identity first
            if (_classScope != other._classScope ||
                _argTypesHash != other._argTypesHash ||
                _guidesHash != other._guidesHash ||
                _methodName != other._methodName)
            {
                return false;
            }

            // Slow path: full array comparison
            return TypeSignaturesEqual(_argTypes, other._argTypes) &&
                   GuidesEqual(_flattenedGuides, other._flattenedGuides);
        }

        public override bool Equals(object obj)
        {
            return obj is DispatchKey other && Equals(other);
        }

        public static bool operator ==(DispatchKey left, DispatchKey right) => left.Equals(right);
        public static bool operator !=(DispatchKey left, DispatchKey right) => !left.Equals(right);

        #region Private helpers

        private static int ComputeTypesHash(TypeSignature[] types)
        {
            if (types == null || types.Length == 0)
                return 0;

            unchecked
            {
                int hash = types.Length;
                for (int i = 0; i < types.Length; i++)
                {
                    hash = (hash * 31) ^ types[i].GetHashCode();
                }
                return hash;
            }
        }

        private static int ComputeGuidesHash(int[] guides)
        {
            if (guides == null || guides.Length == 0)
                return 0;

            unchecked
            {
                int hash = guides.Length;
                for (int i = 0; i < guides.Length; i++)
                {
                    hash = (hash * 31) ^ guides[i];
                }
                return hash;
            }
        }

        /// <summary>
        /// Flattens replication guides into a single int array for fast comparison.
        /// Format: [argCount, arg0GuideCount, guide0Num, guide0IsLongest, guide1Num, ...]
        /// </summary>
        private static int[] FlattenGuides(List<List<ReplicationGuide>> guides)
        {
            if (guides == null || guides.Count == 0)
                return Array.Empty<int>();

            var result = new List<int>(guides.Count * 3);
            result.Add(guides.Count);

            for (int i = 0; i < guides.Count; i++)
            {
                var argGuides = guides[i];
                if (argGuides == null || argGuides.Count == 0)
                {
                    result.Add(0);
                }
                else
                {
                    result.Add(argGuides.Count);
                    for (int j = 0; j < argGuides.Count; j++)
                    {
                        result.Add(argGuides[j].GuideNumber);
                        result.Add(argGuides[j].IsLongest ? 1 : 0);
                    }
                }
            }

            return result.ToArray();
        }

        private static bool TypeSignaturesEqual(TypeSignature[] a, TypeSignature[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }
            return true;
        }

        private static bool GuidesEqual(int[] a, int[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        #endregion
    }
}
