namespace DispatchCacheABTest;

internal class TestResult
{
	public string GraphName { get; set; }

	// Baseline (no cache) metrics
	public double BaselineMeanMs { get; set; }
	public double BaselineStdDevMs { get; set; }
	public double BaselineColdMs { get; set; }
	public double BaselineWarmMeanMs { get; set; }
	public double BaselineWarmStdDevMs { get; set; }

	// Cached metrics
	public double CachedMeanMs { get; set; }
	public double CachedStdDevMs { get; set; }
	public double CachedColdMs { get; set; }
	public double CachedWarmMeanMs { get; set; }
	public double CachedWarmStdDevMs { get; set; }

	// Improvement metrics
	public double ImprovementPercent { get; set; }
	public double ColdImprovementPercent { get; set; }
	public double WarmImprovementPercent { get; set; }

	// Cache statistics
	public long CacheHits { get; set; }
	public long CacheMisses { get; set; }
	public double CacheHitRatio { get; set; }
	public int CacheSize { get; set; }

	// Warm run cache statistics (excludes cold run)
	public long WarmCacheHits { get; set; }
	public long WarmCacheMisses { get; set; }
	public double WarmCacheHitRatio { get; set; }
}
