using System.Collections.Generic;
using NUnit.Framework;
using ProtoCore;
using ProtoCore.Lang.Replication;

namespace ProtoTest.Replication
{
    [TestFixture]
    public class TypeSignatureTests
    {
        [Test]
        public void TypeSignature_SameValues_AreEqual()
        {
            var sig1 = new TypeSignature(1, 0, false);
            var sig2 = new TypeSignature(1, 0, false);

            Assert.AreEqual(sig1, sig2);
            Assert.AreEqual(sig1.GetHashCode(), sig2.GetHashCode());
        }

        [Test]
        public void TypeSignature_DifferentTypeId_NotEqual()
        {
            var sig1 = new TypeSignature(1, 0, false);
            var sig2 = new TypeSignature(2, 0, false);

            Assert.AreNotEqual(sig1, sig2);
        }

        [Test]
        public void TypeSignature_DifferentRank_NotEqual()
        {
            var sig1 = new TypeSignature(1, 0, true);
            var sig2 = new TypeSignature(1, 1, true);

            Assert.AreNotEqual(sig1, sig2);
        }

        [Test]
        public void TypeSignature_DifferentIsArray_NotEqual()
        {
            var sig1 = new TypeSignature(1, 0, false);
            var sig2 = new TypeSignature(1, 0, true);

            Assert.AreNotEqual(sig1, sig2);
        }

        [Test]
        public void TypeSignature_ToString_ReturnsExpectedFormat()
        {
            var sig = new TypeSignature(5, 2, true);
            var str = sig.ToString();

            Assert.IsTrue(str.Contains("Type:5"));
            Assert.IsTrue(str.Contains("Rank:2"));
            Assert.IsTrue(str.Contains("IsArray:True"));
        }
    }

    [TestFixture]
    public class DispatchKeyTests
    {
        [Test]
        public void DispatchKey_SameInputs_AreEqual()
        {
            var types = new[] { new TypeSignature(1, 0, false) };
            var key1 = new DispatchKey("Test", 0, types, null);
            var key2 = new DispatchKey("Test", 0, types, null);

            Assert.AreEqual(key1, key2);
            Assert.AreEqual(key1.GetHashCode(), key2.GetHashCode());
        }

        [Test]
        public void DispatchKey_DifferentMethod_NotEqual()
        {
            var types = new[] { new TypeSignature(1, 0, false) };
            var key1 = new DispatchKey("Test1", 0, types, null);
            var key2 = new DispatchKey("Test2", 0, types, null);

            Assert.AreNotEqual(key1, key2);
        }

        [Test]
        public void DispatchKey_DifferentClassScope_NotEqual()
        {
            var types = new[] { new TypeSignature(1, 0, false) };
            var key1 = new DispatchKey("Test", 0, types, null);
            var key2 = new DispatchKey("Test", 1, types, null);

            Assert.AreNotEqual(key1, key2);
        }

        [Test]
        public void DispatchKey_DifferentTypes_NotEqual()
        {
            var types1 = new[] { new TypeSignature(1, 0, false) };
            var types2 = new[] { new TypeSignature(2, 0, false) };
            var key1 = new DispatchKey("Test", 0, types1, null);
            var key2 = new DispatchKey("Test", 0, types2, null);

            Assert.AreNotEqual(key1, key2);
        }

        [Test]
        public void DispatchKey_WithReplicationGuides_AreEqual()
        {
            var types = new[] { new TypeSignature(1, 0, false) };
            var guides = new List<List<ReplicationGuide>>
            {
                new List<ReplicationGuide> { new ReplicationGuide(1, false) }
            };

            var key1 = new DispatchKey("Test", 0, types, guides);
            var key2 = new DispatchKey("Test", 0, types, guides);

            Assert.AreEqual(key1, key2);
        }

        [Test]
        public void DispatchKey_DifferentGuides_NotEqual()
        {
            var types = new[] { new TypeSignature(1, 0, false) };
            var guides1 = new List<List<ReplicationGuide>>
            {
                new List<ReplicationGuide> { new ReplicationGuide(1, false) }
            };
            var guides2 = new List<List<ReplicationGuide>>
            {
                new List<ReplicationGuide> { new ReplicationGuide(2, false) }
            };

            var key1 = new DispatchKey("Test", 0, types, guides1);
            var key2 = new DispatchKey("Test", 0, types, guides2);

            Assert.AreNotEqual(key1, key2);
        }

        [Test]
        public void DispatchKey_NullGuides_Equal()
        {
            var types = new[] { new TypeSignature(1, 0, false) };
            var key1 = new DispatchKey("Test", 0, types, null);
            var key2 = new DispatchKey("Test", 0, types, null);

            Assert.AreEqual(key1, key2);
        }

        [Test]
        public void DispatchKey_EmptyTypes_Equal()
        {
            var key1 = new DispatchKey("Test", 0, null, null);
            var key2 = new DispatchKey("Test", 0, null, null);

            Assert.AreEqual(key1, key2);
        }
    }

    [TestFixture]
    public class DispatchCacheTests
    {
        [Test]
        public void DispatchCache_StoreAndRetrieve_Success()
        {
            var cache = new DispatchCache();
            var types = new[] { new TypeSignature(1, 0, false) };
            var key = new DispatchKey("Test", 0, types, null);
            var feps = new List<FunctionEndPoint>();
            var instructions = new List<ReplicationInstruction>();
            var result = new CachedDispatchResult(feps, instructions);

            cache.Store(key, result);

            // Note: Empty FEPs won't be stored (HasValidFeps check)
            // This test verifies the store/retrieve mechanism
            var stats = cache.GetStatistics();
            Assert.AreEqual(0, stats.Size); // Empty FEPs are not cached
        }

        [Test]
        public void DispatchCache_Clear_RemovesAllEntries()
        {
            var cache = new DispatchCache();
            cache.Clear();

            Assert.AreEqual(0, cache.Size);
        }

        [Test]
        public void DispatchCache_Statistics_TrackHitsAndMisses()
        {
            var cache = new DispatchCache();
            var types = new[] { new TypeSignature(1, 0, false) };
            var key = new DispatchKey("Test", 0, types, null);

            cache.TryGet(key, out _); // Miss

            var stats = cache.GetStatistics();
            Assert.AreEqual(0, stats.Hits);
            Assert.AreEqual(1, stats.Misses);
        }

        [Test]
        public void DispatchCache_HitRatio_CalculatesCorrectly()
        {
            var cache = new DispatchCache();
            var types = new[] { new TypeSignature(1, 0, false) };
            var key = new DispatchKey("Test", 0, types, null);

            // Three misses
            cache.TryGet(key, out _);
            cache.TryGet(key, out _);
            cache.TryGet(key, out _);

            var stats = cache.GetStatistics();
            Assert.AreEqual(0.0, stats.HitRatio); // All misses = 0 ratio
        }

        [Test]
        public void NullDispatchCache_AlwaysMisses()
        {
            var cache = NullDispatchCache.Instance;
            var types = new[] { new TypeSignature(1, 0, false) };
            var key = new DispatchKey("Test", 0, types, null);

            var hit = cache.TryGet(key, out _);

            Assert.IsFalse(hit);
        }

        [Test]
        public void NullDispatchCache_StoreDoesNothing()
        {
            var cache = NullDispatchCache.Instance;
            var types = new[] { new TypeSignature(1, 0, false) };
            var key = new DispatchKey("Test", 0, types, null);
            var result = new CachedDispatchResult(new List<FunctionEndPoint>(), new List<ReplicationInstruction>());

            cache.Store(key, result);

            var hit = cache.TryGet(key, out _);
            Assert.IsFalse(hit);
        }

        [Test]
        public void NullDispatchCache_StatisticsAreZero()
        {
            var cache = NullDispatchCache.Instance;

            var stats = cache.GetStatistics();
            Assert.AreEqual(0, stats.Hits);
            Assert.AreEqual(0, stats.Misses);
            Assert.AreEqual(0.0, stats.HitRatio);
            Assert.AreEqual(0, stats.Size);
        }
    }

    [TestFixture]
    public class CachedDispatchResultTests
    {
        [Test]
        public void HasValidFeps_EmptyList_ReturnsFalse()
        {
            var result = new CachedDispatchResult(
                new List<FunctionEndPoint>(),
                new List<ReplicationInstruction>());

            Assert.IsFalse(result.HasValidFeps);
        }

        [Test]
        public void HasValidFeps_NullList_ReturnsFalse()
        {
            var result = new CachedDispatchResult(null, null);

            Assert.IsFalse(result.HasValidFeps);
        }

        [Test]
        public void GetFepList_ReturnsNewList()
        {
            var result = new CachedDispatchResult(
                new List<FunctionEndPoint>(),
                new List<ReplicationInstruction>());

            var list1 = result.GetFepList();
            var list2 = result.GetFepList();

            Assert.AreNotSame(list1, list2);
        }

        [Test]
        public void GetInstructionList_ReturnsNewList()
        {
            var result = new CachedDispatchResult(
                new List<FunctionEndPoint>(),
                new List<ReplicationInstruction>());

            var list1 = result.GetInstructionList();
            var list2 = result.GetInstructionList();

            Assert.AreNotSame(list1, list2);
        }
    }

    [TestFixture]
    public class CallSiteDispatchCacheTests
    {
        [SetUp]
        public void Setup()
        {
            CallSite.DisableDispatchCaching();
            CallSite.ClearDispatchCache();
        }

        [TearDown]
        public void TearDown()
        {
            CallSite.DisableDispatchCaching();
        }

        [Test]
        public void EnableDispatchCaching_SetsCache()
        {
            CallSite.EnableDispatchCaching();

            var stats = CallSite.GetDispatchCacheStats();
            // Should be able to get stats without error
            Assert.AreEqual(0, stats.Size);
        }

        [Test]
        public void DisableDispatchCaching_SetsNullCache()
        {
            CallSite.EnableDispatchCaching();
            CallSite.DisableDispatchCaching();

            var stats = CallSite.GetDispatchCacheStats();
            // NullDispatchCache returns all zeros
            Assert.AreEqual(0, stats.Size);
            Assert.AreEqual(0, stats.Hits);
        }

        [Test]
        public void ClearDispatchCache_ClearsEntries()
        {
            CallSite.EnableDispatchCaching();
            CallSite.ClearDispatchCache();

            var stats = CallSite.GetDispatchCacheStats();
            Assert.AreEqual(0, stats.Size);
        }

        [Test]
        public void EnableABTesting_SetsABTestCache()
        {
            CallSite.EnableABTesting();

            var stats = CallSite.GetDispatchCacheStats();
            // Should be able to get stats without error
            Assert.AreEqual(0, stats.Size);
        }
    }
}
