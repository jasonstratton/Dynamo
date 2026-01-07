using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Dynamo.Applications;
using Dynamo.Models;
using ProtoCore;

namespace DispatchCacheABTest;

internal class Program
{
	private static DynamoModel model;

	private static bool evalComplete;

	private static void Main(string[] args)
	{
		Console.WriteLine("========================================");
		Console.WriteLine("Dispatch Cache A/B Performance Test");
		Console.WriteLine("   (Cold vs Warm Cache Analysis)");
		Console.WriteLine("========================================");
		Console.WriteLine();
		if (args.Length == 0)
		{
			PrintUsage();
			return;
		}
		List<string> list = new List<string>();
		int iterations = 3;
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == "-i" || args[i] == "--iterations")
			{
				if (i + 1 < args.Length && int.TryParse(args[i + 1], out var result))
				{
					iterations = result;
					i++;
				}
				continue;
			}
			if (args[i] == "-d" || args[i] == "--directory")
			{
				if (i + 1 < args.Length && Directory.Exists(args[i + 1]))
				{
					list.AddRange(Directory.GetFiles(args[i + 1], "*.dyn"));
					i++;
				}
				continue;
			}
			if (args[i] == "-h" || args[i] == "--help")
			{
				PrintUsage();
				return;
			}
			if (File.Exists(args[i]) && args[i].EndsWith(".dyn"))
			{
				list.Add(args[i]);
			}
		}
		if (list.Count == 0)
		{
			Console.WriteLine("Error: No valid .dyn files found.");
			PrintUsage();
			return;
		}

		// Ensure we have at least 2 iterations to measure cold vs warm
		if (iterations < 2)
		{
			Console.WriteLine("Warning: Increasing iterations to 2 (minimum for cold vs warm analysis)");
			iterations = 2;
		}

		Console.WriteLine($"Found {list.Count} graph(s) to test");
		Console.WriteLine($"Iterations per test: {iterations} (1 cold + {iterations - 1} warm)");
		Console.WriteLine();
		try
		{
			Console.WriteLine("Initializing Dynamo...");
			InitializeDynamo();
			Console.WriteLine("Dynamo initialized successfully.");
			Console.WriteLine();
			List<TestResult> results = new List<TestResult>();
			foreach (string graphPath in list)
			{
				Console.WriteLine("Testing: " + Path.GetFileName(graphPath));
				Console.WriteLine(new string('-', 50));
				TestResult testResult = RunABTest(graphPath, iterations);
				results.Add(testResult);
				PrintResult(testResult);
				Console.WriteLine();
			}
			PrintSummary(results);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
			Console.WriteLine(ex.StackTrace);
		}
		finally
		{
			model?.Dispose();
		}
	}

	private static void PrintUsage()
	{
		Console.WriteLine("Usage: DispatchCacheABTest [options] <graph.dyn> [graph2.dyn] ...");
		Console.WriteLine();
		Console.WriteLine("Options:");
		Console.WriteLine("  -d, --directory <path>  Test all .dyn files in directory");
		Console.WriteLine("  -i, --iterations <n>    Number of iterations per test (default: 3, min: 2)");
		Console.WriteLine("  -h, --help              Show this help");
		Console.WriteLine();
		Console.WriteLine("Example:");
		Console.WriteLine("  DispatchCacheABTest -d graphs/ -i 5");
		Console.WriteLine("  DispatchCacheABTest myGraph.dyn");
		Console.WriteLine();
		Console.WriteLine("Output:");
		Console.WriteLine("  Cold = First run (cache empty, all misses)");
		Console.WriteLine("  Warm = Subsequent runs (cache populated, potential hits)");
	}

	private static void InitializeDynamo()
	{
		model = StartupUtils.MakeCLIModel(new StartupUtils.CommandLineArguments
		{
			DisableAnalytics = true,
			NoNetworkMode = true
		});
	}

	private static TestResult RunABTest(string graphPath, int iterations)
	{
		TestResult result = new TestResult
		{
			GraphName = Path.GetFileName(graphPath)
		};

		// Warm-up run (not measured, just to load assemblies etc.)
		Console.Write("  Warm-up run... ");
		CallSite.DisableDispatchCaching();
		RunGraph(graphPath);
		Console.WriteLine("done");

		// ===================
		// BASELINE (no cache)
		// ===================
		Console.Write($"  Baseline (no cache) x{iterations}... ");
		CallSite.DisableDispatchCaching();
		CallSite.ClearDispatchCache();

		List<double> baselineTimes = new List<double>();
		for (int i = 0; i < iterations; i++)
		{
			Stopwatch sw = Stopwatch.StartNew();
			RunGraph(graphPath);
			sw.Stop();
			baselineTimes.Add(sw.Elapsed.TotalMilliseconds);
		}

		// Baseline metrics
		result.BaselineColdMs = baselineTimes[0];
		result.BaselineMeanMs = baselineTimes.Average();
		result.BaselineStdDevMs = StdDev(baselineTimes);

		if (baselineTimes.Count > 1)
		{
			var warmTimes = baselineTimes.Skip(1).ToList();
			result.BaselineWarmMeanMs = warmTimes.Average();
			result.BaselineWarmStdDevMs = StdDev(warmTimes);
		}
		else
		{
			result.BaselineWarmMeanMs = result.BaselineColdMs;
			result.BaselineWarmStdDevMs = 0;
		}

		Console.WriteLine($"{result.BaselineMeanMs:F2}ms (cold: {result.BaselineColdMs:F2}ms, warm: {result.BaselineWarmMeanMs:F2}ms)");

		// ===================
		// WITH CACHE
		// ===================
		Console.Write($"  With cache x{iterations}... ");
		CallSite.EnableABTesting();
		CallSite.ClearDispatchCache();

		List<double> cachedTimes = new List<double>();

		// Cold run (first iteration - cache is empty)
		Stopwatch swCold = Stopwatch.StartNew();
		RunGraph(graphPath);
		swCold.Stop();
		cachedTimes.Add(swCold.Elapsed.TotalMilliseconds);

		// Capture stats after cold run
		var coldStats = CallSite.GetDispatchCacheStats();

		// Warm runs (subsequent iterations - cache is populated)
		for (int i = 1; i < iterations; i++)
		{
			Stopwatch sw = Stopwatch.StartNew();
			RunGraph(graphPath);
			sw.Stop();
			cachedTimes.Add(sw.Elapsed.TotalMilliseconds);
		}

		// Capture final stats (includes all runs)
		var finalStats = CallSite.GetDispatchCacheStats();

		// Cached metrics
		result.CachedColdMs = cachedTimes[0];
		result.CachedMeanMs = cachedTimes.Average();
		result.CachedStdDevMs = StdDev(cachedTimes);

		if (cachedTimes.Count > 1)
		{
			var warmTimes = cachedTimes.Skip(1).ToList();
			result.CachedWarmMeanMs = warmTimes.Average();
			result.CachedWarmStdDevMs = StdDev(warmTimes);
		}
		else
		{
			result.CachedWarmMeanMs = result.CachedColdMs;
			result.CachedWarmStdDevMs = 0;
		}

		Console.WriteLine($"{result.CachedMeanMs:F2}ms (cold: {result.CachedColdMs:F2}ms, warm: {result.CachedWarmMeanMs:F2}ms)");

		// Cache statistics
		result.CacheHits = finalStats.Hits;
		result.CacheMisses = finalStats.Misses;
		result.CacheHitRatio = finalStats.HitRatio;
		result.CacheSize = finalStats.Size;

		// Warm run cache statistics (total minus cold run stats)
		result.WarmCacheHits = finalStats.Hits - coldStats.Hits;
		result.WarmCacheMisses = finalStats.Misses - coldStats.Misses;
		long warmTotal = result.WarmCacheHits + result.WarmCacheMisses;
		result.WarmCacheHitRatio = warmTotal > 0 ? (double)result.WarmCacheHits / warmTotal : 0;

		// Improvement calculations
		result.ImprovementPercent = (result.BaselineMeanMs - result.CachedMeanMs) / result.BaselineMeanMs * 100.0;
		result.ColdImprovementPercent = (result.BaselineColdMs - result.CachedColdMs) / result.BaselineColdMs * 100.0;
		result.WarmImprovementPercent = (result.BaselineWarmMeanMs - result.CachedWarmMeanMs) / result.BaselineWarmMeanMs * 100.0;

		return result;
	}

	private static void RunGraph(string graphPath)
	{
		evalComplete = false;
		model.OpenFileFromPath(graphPath, forceManualExecutionMode: true);
		model.EvaluationCompleted += OnEvaluationCompleted;
		model.ExecuteCommand(new DynamoModel.RunCancelCommand(showErrors: false, cancelRun: false));
		while (!evalComplete)
		{
			Thread.Sleep(50);
		}
		model.EvaluationCompleted -= OnEvaluationCompleted;
	}

	private static void OnEvaluationCompleted(object sender, EvaluationCompletedEventArgs args)
	{
		evalComplete = true;
	}

	private static void PrintResult(TestResult result)
	{
		Console.WriteLine();
		Console.WriteLine("  Results for: " + result.GraphName);
		Console.WriteLine();
		Console.WriteLine("                         Baseline      Cached    Improvement");
		Console.WriteLine($"    Cold (1st run):    {result.BaselineColdMs,10:F2}ms {result.CachedColdMs,10:F2}ms {result.ColdImprovementPercent,10:F1}%");
		Console.WriteLine($"    Warm (subsequent): {result.BaselineWarmMeanMs,10:F2}ms {result.CachedWarmMeanMs,10:F2}ms {result.WarmImprovementPercent,10:F1}%");
		Console.WriteLine($"    Overall average:   {result.BaselineMeanMs,10:F2}ms {result.CachedMeanMs,10:F2}ms {result.ImprovementPercent,10:F1}%");
		Console.WriteLine();
		Console.WriteLine("  Cache Statistics:");
		Console.WriteLine($"    Total:  {result.CacheHits,8} hits, {result.CacheMisses,8} misses ({result.CacheHitRatio:P1} hit ratio)");
		Console.WriteLine($"    Warm:   {result.WarmCacheHits,8} hits, {result.WarmCacheMisses,8} misses ({result.WarmCacheHitRatio:P1} hit ratio)");
		Console.WriteLine($"    Cache size: {result.CacheSize} entries");
	}

	private static void PrintSummary(List<TestResult> results)
	{
		Console.WriteLine("========================================");
		Console.WriteLine("SUMMARY - Cold vs Warm Analysis");
		Console.WriteLine("========================================");
		Console.WriteLine();

		// Cold results
		Console.WriteLine("COLD (First Run - Cache Empty):");
		Console.WriteLine($"{"Graph",-40} {"Baseline",12} {"Cached",12} {"Improve",10}");
		Console.WriteLine(new string('-', 80));
		foreach (TestResult result in results)
		{
			string name = result.GraphName.Length > 38 ? result.GraphName.Substring(0, 35) + "..." : result.GraphName;
			Console.WriteLine($"{name,-40} {result.BaselineColdMs,10:F2}ms {result.CachedColdMs,10:F2}ms {result.ColdImprovementPercent,9:F1}%");
		}
		double avgColdImprovement = results.Average(r => r.ColdImprovementPercent);
		Console.WriteLine(new string('-', 80));
		Console.WriteLine($"{"AVERAGE",-40} {"",-12} {"",-12} {avgColdImprovement,9:F1}%");
		Console.WriteLine();

		// Warm results
		Console.WriteLine("WARM (Subsequent Runs - Cache Populated):");
		Console.WriteLine($"{"Graph",-40} {"Baseline",12} {"Cached",12} {"Improve",10} {"Hit Ratio",10}");
		Console.WriteLine(new string('-', 95));
		foreach (TestResult result in results)
		{
			string name = result.GraphName.Length > 38 ? result.GraphName.Substring(0, 35) + "..." : result.GraphName;
			Console.WriteLine($"{name,-40} {result.BaselineWarmMeanMs,10:F2}ms {result.CachedWarmMeanMs,10:F2}ms {result.WarmImprovementPercent,9:F1}% {result.WarmCacheHitRatio,9:P0}");
		}
		double avgWarmImprovement = results.Average(r => r.WarmImprovementPercent);
		double avgWarmHitRatio = results.Average(r => r.WarmCacheHitRatio);
		Console.WriteLine(new string('-', 95));
		Console.WriteLine($"{"AVERAGE",-40} {"",-12} {"",-12} {avgWarmImprovement,9:F1}% {avgWarmHitRatio,9:P0}");
		Console.WriteLine();

		// Overall summary
		Console.WriteLine("========================================");
		Console.WriteLine("CONCLUSION");
		Console.WriteLine("========================================");
		Console.WriteLine();

		Console.WriteLine($"  Cold run improvement:  {avgColdImprovement,6:F1}% (expected: ~0% or negative due to cache overhead)");
		Console.WriteLine($"  Warm run improvement:  {avgWarmImprovement,6:F1}% (this is the true cache benefit)");
		Console.WriteLine($"  Average hit ratio:     {avgWarmHitRatio,6:P1} (on warm runs)");
		Console.WriteLine();

		if (avgWarmImprovement > 5.0)
		{
			Console.WriteLine($"  RESULT: Cache provides {avgWarmImprovement:F1}% benefit on repeated executions.");
		}
		else if (avgWarmImprovement > 0)
		{
			Console.WriteLine($"  RESULT: Cache provides marginal {avgWarmImprovement:F1}% benefit on repeated executions.");
		}
		else
		{
			Console.WriteLine($"  RESULT: Cache adds {-avgWarmImprovement:F1}% overhead even on repeated executions.");
		}

		// Identify best/worst performers
		var bestWarm = results.OrderByDescending(r => r.WarmImprovementPercent).First();
		var worstWarm = results.OrderBy(r => r.WarmImprovementPercent).First();

		Console.WriteLine();
		Console.WriteLine($"  Best performer:  {bestWarm.GraphName} ({bestWarm.WarmImprovementPercent:F1}% improvement)");
		Console.WriteLine($"  Worst performer: {worstWarm.GraphName} ({worstWarm.WarmImprovementPercent:F1}% improvement)");
	}

	private static double StdDev(List<double> values)
	{
		if (values.Count <= 1)
		{
			return 0.0;
		}
		double avg = values.Average();
		return Math.Sqrt(values.Sum(v => (v - avg) * (v - avg)) / (values.Count - 1));
	}
}
