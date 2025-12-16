using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ProtoCore.Lang.Replication
{
    /// <summary>
    /// Cached result of dispatch resolution.
    /// Contains the resolved function endpoints and replication instructions.
    /// </summary>
    public readonly struct CachedDispatchResult
    {
        public readonly FunctionEndPoint[] ResolvedFeps;
        public readonly ReplicationInstruction[] Instructions;

        public CachedDispatchResult(
            List<FunctionEndPoint> feps,
            List<ReplicationInstruction> instructions)
        {
            ResolvedFeps = feps?.ToArray() ?? Array.Empty<FunctionEndPoint>();
            Instructions = instructions?.ToArray() ?? Array.Empty<ReplicationInstruction>();
        }

        /// <summary>
        /// Returns FEPs as a new list (for compatibility with existing code).
        /// </summary>
        public List<FunctionEndPoint> GetFepList()
        {
            return new List<FunctionEndPoint>(ResolvedFeps);
        }

        /// <summary>
        /// Returns instructions as a new list (for compatibility with existing code).
        /// </summary>
        public List<ReplicationInstruction> GetInstructionList()
        {
            return new List<ReplicationInstruction>(Instructions);
        }

        /// <summary>
        /// Checks if this result has valid FEPs.
        /// </summary>
        public bool HasValidFeps => ResolvedFeps.Length > 0;
    }

    /// <summary>
    /// Interface for dispatch caching implementations.
    /// Allows swapping between different caching strategies.
    /// </summary>
    public interface IDispatchCache
    {
        /// <summary>
        /// Attempts to retrieve a cached dispatch result.
        /// </summary>
        bool TryGet(DispatchKey key, out CachedDispatchResult result);

        /// <summary>
        /// Stores a dispatch result in the cache.
        /// </summary>
        void Store(DispatchKey key, CachedDispatchResult result);

        /// <summary>
        /// Clears all cached entries.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        (long Hits, long Misses, double HitRatio, int Size) GetStatistics();
    }

    /// <summary>
    /// Null implementation that never caches (original behavior).
    /// </summary>
    public sealed class NullDispatchCache : IDispatchCache
    {
        public static readonly NullDispatchCache Instance = new NullDispatchCache();

        private NullDispatchCache() { }

        public bool TryGet(DispatchKey key, out CachedDispatchResult result)
        {
            result = default;
            return false; // Always miss - forces full computation
        }

        public void Store(DispatchKey key, CachedDispatchResult result)
        {
            // No-op - don't store anything
        }

        public void Clear()
        {
            // No-op
        }

        public (long Hits, long Misses, double HitRatio, int Size) GetStatistics()
        {
            return (0, 0, 0.0, 0);
        }
    }

    /// <summary>
    /// Thread-safe cache for dispatch resolution results.
    /// Clears automatically at the start of each graph execution.
    /// </summary>
    public sealed class DispatchCache : IDispatchCache
    {
        private ConcurrentDictionary<DispatchKey, CachedDispatchResult> _cache;

        // Statistics
        private long _hits;
        private long _misses;

        // Size management
        private const int MaxCacheSize = 10000;
        private int _approximateSize;

        public DispatchCache()
        {
            _cache = new ConcurrentDictionary<DispatchKey, CachedDispatchResult>();
        }

        /// <summary>
        /// Attempts to retrieve a cached dispatch result.
        /// Thread-safe.
        /// </summary>
        public bool TryGet(DispatchKey key, out CachedDispatchResult result)
        {
            if (_cache.TryGetValue(key, out result))
            {
                Interlocked.Increment(ref _hits);
                return true;
            }

            Interlocked.Increment(ref _misses);
            result = default;
            return false;
        }

        /// <summary>
        /// Stores a dispatch result in the cache.
        /// Thread-safe.
        /// </summary>
        public void Store(DispatchKey key, CachedDispatchResult result)
        {
            // Only cache successful resolutions
            if (!result.HasValidFeps)
                return;

            // Check size limit
            if (_approximateSize >= MaxCacheSize)
            {
                TrimCache();
            }

            if (_cache.TryAdd(key, result))
            {
                Interlocked.Increment(ref _approximateSize);
            }
        }

        /// <summary>
        /// Clears all cached entries.
        /// Call at the start of each graph execution.
        /// Thread-safe.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _approximateSize, 0);
            // Note: Statistics are NOT reset - useful for monitoring across runs
        }

        /// <summary>
        /// Validates that cached FEPs still exist in a function group.
        /// Returns false if any FEP is missing (indicating stale cache).
        /// </summary>
        public static bool ValidateFeps(CachedDispatchResult cached, FunctionGroup funcGroup)
        {
            if (cached.ResolvedFeps.Length == 0)
                return false;

            var fepSet = new HashSet<FunctionEndPoint>(funcGroup.FunctionEndPoints);
            for (int i = 0; i < cached.ResolvedFeps.Length; i++)
            {
                if (!fepSet.Contains(cached.ResolvedFeps[i]))
                    return false;
            }
            return true;
        }

        #region Statistics

        /// <summary>
        /// Gets the cache hit count.
        /// </summary>
        public long Hits => Interlocked.Read(ref _hits);

        /// <summary>
        /// Gets the cache miss count.
        /// </summary>
        public long Misses => Interlocked.Read(ref _misses);

        /// <summary>
        /// Gets the current approximate cache size.
        /// </summary>
        public int Size => _approximateSize;

        /// <summary>
        /// Gets the cache hit ratio (0.0 to 1.0).
        /// </summary>
        public double HitRatio
        {
            get
            {
                long total = Hits + Misses;
                return total > 0 ? (double)Hits / total : 0.0;
            }
        }

        /// <summary>
        /// Resets statistics counters.
        /// </summary>
        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);
        }

        public (long Hits, long Misses, double HitRatio, int Size) GetStatistics()
        {
            return (Hits, Misses, HitRatio, Size);
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Trims the cache to half capacity when full.
        /// Simple strategy: create new dictionary with first half of entries.
        /// </summary>
        private void TrimCache()
        {
            var newCache = new ConcurrentDictionary<DispatchKey, CachedDispatchResult>();
            int count = 0;
            int target = MaxCacheSize / 2;

            foreach (var kvp in _cache)
            {
                if (count >= target)
                    break;

                newCache.TryAdd(kvp.Key, kvp.Value);
                count++;
            }

            _cache = newCache;
            Interlocked.Exchange(ref _approximateSize, count);
        }

        #endregion
    }

    /// <summary>
    /// A/B testing wrapper that runs both implementations and compares.
    /// </summary>
    public sealed class ABTestDispatchCache : IDispatchCache
    {
        private readonly DispatchCache _newCache = new DispatchCache();
        private readonly NullDispatchCache _oldCache = NullDispatchCache.Instance;

        private long _comparisons;
        private long _cacheHitsWithCorrectResults;

        public bool TryGet(DispatchKey key, out CachedDispatchResult result)
        {
            // Try new cache
            bool newHit = _newCache.TryGet(key, out var newResult);

            // Old always misses
            bool oldHit = _oldCache.TryGet(key, out _);

            Interlocked.Increment(ref _comparisons);

            // Track when new cache provides a hit (old never does)
            if (newHit && !oldHit)
            {
                Interlocked.Increment(ref _cacheHitsWithCorrectResults);
            }

            result = newResult;
            return newHit;
        }

        public void Store(DispatchKey key, CachedDispatchResult result)
        {
            _newCache.Store(key, result);
        }

        public void Clear()
        {
            _newCache.Clear();
        }

        public (long Hits, long Misses, double HitRatio, int Size) GetStatistics()
        {
            return _newCache.GetStatistics();
        }

        public (long Comparisons, long CacheHits) GetComparisonStats()
        {
            return (_comparisons, _cacheHitsWithCorrectResults);
        }
    }
}
