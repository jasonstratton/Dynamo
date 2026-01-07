# DispatchCacheABTest

A performance testing tool for measuring the effectiveness of the Replication Dispatch Cache in Dynamo's DesignScript VM.

## Why This Project Exists

Dynamo's DesignScript VM performs "dispatch resolution" when calling functions - determining which function endpoint (FEP) to invoke based on argument types and replication rules. This process can be expensive, especially for graphs with many function calls.

The **Replication Dispatch Cache** (`src/Engine/ProtoCore/Lang/Replication/IDispatchCache.cs`) caches these dispatch resolutions to avoid redundant computation. However, caching has overhead (key computation, dictionary lookups, memory), so it's not always beneficial.

This tool measures whether the cache actually improves performance by comparing:
- **Baseline**: Running graphs with caching disabled (`NullDispatchCache`)
- **Cached**: Running graphs with caching enabled (`DispatchCache` via `ABTestDispatchCache`)

## What It Does

The tool runs each test graph multiple times and measures:

1. **Cold Run Performance** (1st iteration)
   - Cache is empty
   - All dispatch lookups are misses
   - Measures the overhead of populating the cache

2. **Warm Run Performance** (subsequent iterations)
   - Cache is populated from cold run
   - Dispatch lookups can hit the cache
   - Measures the actual benefit of caching

3. **Cache Statistics**
   - Hit count, miss count, hit ratio
   - Cache size (number of entries)
   - Separate stats for cold vs warm phases

## Design

### Test Flow

```
For each graph:
1. Warm-up run (not measured) - loads assemblies, JIT compilation
2. Baseline test (caching disabled):
   - Run N iterations
   - Record timing for each iteration
   - Calculate cold (1st) vs warm (2nd+) averages
3. Cached test (caching enabled):
   - Clear cache
   - Run 1st iteration (cold) - cache populates
   - Capture cache stats after cold run
   - Run remaining iterations (warm) - cache benefits
   - Capture final cache stats
4. Calculate improvements:
   - Cold improvement = (baseline_cold - cached_cold) / baseline_cold
   - Warm improvement = (baseline_warm - cached_warm) / baseline_warm
   - Overall improvement = average of all iterations
```

### Key Insight

Previous analysis averaged cold and warm runs together, showing **-5.6% overhead** (cache hurts). The cold/warm split reveals:
- Cold runs: **-13.1%** (expected overhead from cache population)
- Warm runs: **+5.6%** (real benefit on repeated executions)

This matters because typical user workflows involve repeated graph executions (editing, iterating), so warm run performance is what users actually experience.

### Architecture

```
Program.cs
├── Main() - Entry point, argument parsing
├── InitializeDynamo() - Creates DynamoModel in CLI mode
├── RunABTest() - Runs baseline and cached tests for one graph
├── RunGraph() - Executes a single graph and waits for completion
├── PrintResult() - Outputs per-graph results
└── PrintSummary() - Outputs aggregate results with cold/warm split

TestResult.cs
└── Data class holding all metrics for one graph test
```

### Cache Control

The tool uses static methods on `ProtoCore.CallSite`:
- `CallSite.DisableDispatchCaching()` - Uses `NullDispatchCache` (always misses)
- `CallSite.EnableABTesting()` - Uses `ABTestDispatchCache` (wraps real cache)
- `CallSite.ClearDispatchCache()` - Resets cache between test phases
- `CallSite.GetDispatchCacheStats()` - Returns (Hits, Misses, HitRatio, Size)

## Prerequisites

- Dynamo must be built first (Release configuration recommended)
- .NET 10.0 SDK
- Test graphs in `tools/Performance/DynamoPerformanceTests/graphs/`

## How to Build

### Using MSBuild (recommended)

```bash
# From repository root
msbuild tools/Performance/DispatchCacheABTest/DispatchCacheABTest.csproj ^
    -t:Restore ^
    -p:Configuration=Release ^
    -p:SolutionDir="E:/dev/Dynamo/"

msbuild tools/Performance/DispatchCacheABTest/DispatchCacheABTest.csproj ^
    -p:Configuration=Release ^
    -p:SolutionDir="E:/dev/Dynamo/"
```

### Output Location

The executable is built to `bin/AnyCPU/Release/DispatchCacheABTest.exe` alongside other Dynamo binaries.

## How to Execute

**Important:** Run from the Dynamo bin directory so the tool can find required assemblies and node libraries.

```bash
cd bin/AnyCPU/Release

# Test a single graph
./DispatchCacheABTest.exe path/to/graph.dyn

# Test all graphs in a directory
./DispatchCacheABTest.exe -d path/to/graphs/

# Specify iteration count (default: 3, minimum: 2)
./DispatchCacheABTest.exe -d path/to/graphs/ -i 5

# Show help
./DispatchCacheABTest.exe --help
```

### Typical Usage

```bash
cd bin/AnyCPU/Release
./DispatchCacheABTest.exe -d ../../../tools/Performance/DynamoPerformanceTests/graphs -i 4
```

## Understanding the Output

### Per-Graph Results

```
Results for: HomogeneousInputs.dyn

                         Baseline      Cached    Improvement
    Cold (1st run):         81.99ms      87.29ms       -6.5%
    Warm (subsequent):     121.95ms      93.28ms       23.5%
    Overall average:       111.96ms      91.78ms       18.0%

  Cache Statistics:
    Total:         7 hits,        9 misses (43.8% hit ratio)
    Warm:          6 hits,        6 misses (50.0% hit ratio)
    Cache size: 3 entries
```

- **Cold improvement**: Negative is expected (cache population overhead)
- **Warm improvement**: Positive means cache is helping
- **Warm hit ratio**: Higher is generally better, but not guaranteed (see caveats)

### Summary Tables

The tool outputs two summary tables:
1. **COLD** - First run performance (cache overhead visible)
2. **WARM** - Subsequent run performance (cache benefit visible)

### Interpreting Results

| Warm Improvement | Interpretation |
|------------------|----------------|
| > 10% | Cache is clearly beneficial for this graph |
| 0-10% | Marginal benefit, cache is worth keeping |
| -5% to 0% | Neutral, cache overhead roughly equals benefit |
| < -5% | Cache is hurting performance for this graph |

## Sample Output

```
========================================
CONCLUSION
========================================

  Cold run improvement:   -13.1% (expected: ~0% or negative due to cache overhead)
  Warm run improvement:     5.6% (this is the true cache benefit)
  Average hit ratio:      57.5% (on warm runs)

  RESULT: Cache provides 5.6% benefit on repeated executions.

  Best performer:  HeterogeneousInputsFirst.dyn (36.1% improvement)
  Worst performer: GeometryDisposeLarge.dyn (-34.1% improvement)
```

## Test Graphs

The standard test graphs are in `tools/Performance/DynamoPerformanceTests/graphs/`:

| Graph | Characteristics |
|-------|-----------------|
| Point.Pruneduplicates.dyn | Very high replication (300K+ calls) |
| HomogeneousInputs.dyn | Uniform type lists |
| HeterogeneousInputs*.dyn | Mixed type lists |
| Pottery.dyn, Vase.dyn | Complex geometry operations |
| CodeBlocks.dyn | Many small operations |
| Python*.dyn | Python node performance |

## Related Files

- `src/Engine/ProtoCore/Lang/Replication/IDispatchCache.cs` - Cache implementation
- `src/Engine/ProtoCore/Lang/CallSite.cs` - Cache integration point
- `test/Engine/ProtoTest/Replication/DispatchCacheTests.cs` - Unit tests
- `strattj/DynaNotes/DynamoDeveloperNotebook/ReplicationDispatchCache_ABTest_RESULTS.md` - Original results
- `strattj/DynaNotes/DynamoDeveloperNotebook/ReplicationDispatchCache_ColdWarm_ABTest_RESULTS.md` - Cold/warm analysis

## Caveats and Known Issues

1. **High hit ratio doesn't guarantee improvement**
   - Some graphs show 90%+ hit ratio but negative improvement
   - The cached dispatch may already be very fast, making cache overhead significant

2. **Variability between runs**
   - Results can vary 5-10% between test runs due to system load, GC, etc.
   - Use more iterations (-i 5 or higher) for more stable averages

3. **GeometryDisposeLarge anomaly**
   - Shows 100% hit ratio but -34% warm degradation
   - Needs investigation

4. **Must run from bin directory**
   - Dynamo requires its `nodes/` directory and other resources
   - Running from project directory will fail

## History

- **December 2025**: Original A/B test tool created, showed -5.6% average overhead
- **January 2026**: Updated with cold/warm analysis, revealed +5.6% warm benefit
- **January 2026**: Project cleanup, standardized build output location
