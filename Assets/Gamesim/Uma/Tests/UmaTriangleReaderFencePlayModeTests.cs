#if GAMESIM_UMA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UMA;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    [TestFixture]
    public sealed class UmaTriangleReaderFencePlayModeTests
    {
        [TestCase(false, false, false, false, false, TestName = "Readers_Direct_U16_Plain_Base_Cache")]
        [TestCase(false, false, false, false, true, TestName = "Readers_Direct_U16_Plain_Base_Global")]
        [TestCase(false, false, false, true, false, TestName = "Readers_Direct_U16_Plain_LodSlice_Cache")]
        [TestCase(false, false, false, true, true, TestName = "Readers_Direct_U16_Plain_LodSlice_Global")]
        [TestCase(false, false, true, false, false, TestName = "Readers_Direct_U16_Masked_Base_Cache")]
        [TestCase(false, false, true, false, true, TestName = "Readers_Direct_U16_Masked_Base_Global")]
        [TestCase(false, false, true, true, false, TestName = "Readers_Direct_U16_Masked_LodSlice_Cache")]
        [TestCase(false, false, true, true, true, TestName = "Readers_Direct_U16_Masked_LodSlice_Global")]
        [TestCase(false, true, false, false, false, TestName = "Readers_Direct_U32_Plain_Base_Cache")]
        [TestCase(false, true, false, false, true, TestName = "Readers_Direct_U32_Plain_Base_Global")]
        [TestCase(false, true, false, true, false, TestName = "Readers_Direct_U32_Plain_LodSlice_Cache")]
        [TestCase(false, true, false, true, true, TestName = "Readers_Direct_U32_Plain_LodSlice_Global")]
        [TestCase(false, true, true, false, false, TestName = "Readers_Direct_U32_Masked_Base_Cache")]
        [TestCase(false, true, true, false, true, TestName = "Readers_Direct_U32_Masked_Base_Global")]
        [TestCase(false, true, true, true, false, TestName = "Readers_Direct_U32_Masked_LodSlice_Cache")]
        [TestCase(false, true, true, true, true, TestName = "Readers_Direct_U32_Masked_LodSlice_Global")]
        [TestCase(true, false, false, false, false, TestName = "Readers_Staged_U16_Plain_Base_Cache")]
        [TestCase(true, false, false, false, true, TestName = "Readers_Staged_U16_Plain_Base_Global")]
        [TestCase(true, false, false, true, false, TestName = "Readers_Staged_U16_Plain_LodSlice_Cache")]
        [TestCase(true, false, false, true, true, TestName = "Readers_Staged_U16_Plain_LodSlice_Global")]
        [TestCase(true, false, true, false, false, TestName = "Readers_Staged_U16_Masked_Base_Cache")]
        [TestCase(true, false, true, false, true, TestName = "Readers_Staged_U16_Masked_Base_Global")]
        [TestCase(true, false, true, true, false, TestName = "Readers_Staged_U16_Masked_LodSlice_Cache")]
        [TestCase(true, false, true, true, true, TestName = "Readers_Staged_U16_Masked_LodSlice_Global")]
        [TestCase(true, true, false, false, false, TestName = "Readers_Staged_U32_Plain_Base_Cache")]
        [TestCase(true, true, false, false, true, TestName = "Readers_Staged_U32_Plain_Base_Global")]
        [TestCase(true, true, false, true, false, TestName = "Readers_Staged_U32_Plain_LodSlice_Cache")]
        [TestCase(true, true, false, true, true, TestName = "Readers_Staged_U32_Plain_LodSlice_Global")]
        [TestCase(true, true, true, false, false, TestName = "Readers_Staged_U32_Masked_Base_Cache")]
        [TestCase(true, true, true, false, true, TestName = "Readers_Staged_U32_Masked_Base_Global")]
        [TestCase(true, true, true, true, false, TestName = "Readers_Staged_U32_Masked_LodSlice_Cache")]
        [TestCase(true, true, true, true, true, TestName = "Readers_Staged_U32_Masked_LodSlice_Global")]
        [Timeout(180000)]
        public void RealPreparationReadersRetireBeforeCacheDisposal(
            bool staged, bool wide, bool masked, bool lod, bool global)
        {
            WithFixture(fixture =>
            {
                var pending = fixture.Prepare(staged, wide, masked, lod, false);
                AssertScheduledReader(pending, fixture.Cache, lod);
                if (global)
                {
                    AssertOnlyOwnedCacheRegistered(fixture.Cache);
                    Assert.DoesNotThrow(SubMeshTriangles.DisposeAllNativeTriangles,
                        "Exact owned source readers must retire before global cache disposal.");
                }
                else
                {
                    Assert.DoesNotThrow(() => fixture.Cache.DisposeNativeTriangles(true),
                        "Exact owned source readers must retire before cache disposal.");
                }
                Assert.That(fixture.Cache.nativeTriangles.IsCreated, Is.False);
                Assert.That(SubMeshTriangles.nativeTrianglesAllocated.Contains(fixture.Cache), Is.False);
                AssertOutput(pending, wide, masked, lod);
            });
        }

        [TestCase(false, TestName = "Readers_Direct_TwoBorrowers_SharedDeepCopyOwner")]
        [TestCase(true, TestName = "Readers_Staged_TwoBorrowers_SharedDeepCopyOwner")]
        [Timeout(180000)]
        public void BothActualBorrowersOfTheSameCacheAreRetired(bool staged)
        {
            WithFixture(fixture =>
            {
                var first = fixture.Prepare(staged, false, false, true, false);
                var second = fixture.Prepare(!staged, true, true, true, true);
                AssertScheduledReader(first, fixture.Cache, true);
                AssertScheduledReader(second, fixture.Cache, true);
                Assert.DoesNotThrow(() => fixture.Cache.DisposeNativeTriangles(true),
                    "Both real pending combines share this exact cache owner.");
                Assert.That(fixture.Cache.nativeTriangles.IsCreated, Is.False);
                AssertOutput(first, false, false, true);
                AssertOutput(second, true, true, true);
            });
        }

        [TestCase(false, TestName = "Readers_Direct_SetTriangles_Reallocate")]
        [TestCase(true, TestName = "Readers_Staged_SetTriangles_Reallocate")]
        [Timeout(180000)]
        public void ReplacingTheManagedSourceRetiresTheOldGeneration(bool staged)
        {
            WithFixture(fixture =>
            {
                var first = fixture.Prepare(staged, false, false, false, false);
                AssertScheduledReader(first, fixture.Cache, false);
                Assert.DoesNotThrow(() => fixture.Cache.SetTriangles(new[] { 2, 1, 0, 2, 3, 1 }),
                    "SetTriangles must retire old readers before replacing managed source data.");
                Assert.That(fixture.Cache.nativeTriangles.IsCreated, Is.False);
                AssertOutput(first, false, false, false);
                var replacement = fixture.Prepare(!staged, true, false, false, false);
                AssertScheduledReader(replacement, fixture.Cache, false, 6);
                Assert.DoesNotThrow(() => fixture.Cache.DisposeNativeTriangles(true));
                replacement.CompleteJobs();
                CollectionAssert.AreEqual(new[] { 2, 1, 0, 2, 3, 1 },
                    replacement.MeshDataArray[0].GetIndexData<int>().ToArray());
            });
        }

        [Test, Timeout(180000)]
        public void Readers_DefaultEmptyCacheDisposalRemainsValid()
        {
            var empty = new SubMeshTriangles();
            Assert.DoesNotThrow(() => empty.DisposeNativeTriangles(true));
            Assert.DoesNotThrow(() => empty.SetTriangles(Array.Empty<int>()));
            Assert.That(empty.nativeTriangles.IsCreated, Is.False);
            Assert.That(SubMeshTriangles.nativeTrianglesAllocated.Contains(empty), Is.False);
        }

        [Test, Timeout(180000)]
        public void Readers_SynchronousLodReuseRetainsTheSameOwnedCache()
        {
            var cache = new SubMeshTriangles(new[] { 0, 1, 2, 1, 3, 2 });
            try
            {
                cache.SetLodRanges(new List<UMALodRange>
                {
                    new UMALodRange(0, 6), new UMALodRange(3, 3)
                });
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 1, 3, 2 },
                    cache.GetTriangles(0).ToArray());
                CollectionAssert.AreEqual(new[] { 1, 3, 2 }, cache.GetTriangles(1).ToArray());
                Assert.That(SubMeshTriangles.nativeTrianglesAllocated.FindAll(
                    item => ReferenceEquals(item, cache)).Count, Is.EqualTo(1));
                cache.SetLodRanges(new List<UMALodRange> { new UMALodRange(0, 3) });
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, cache.GetTriangles(0).ToArray());
                Assert.DoesNotThrow(() => cache.DisposeNativeTriangles(true));
            }
            finally
            {
                cache.DisposeNativeTriangles(true);
            }
        }

        private static void AssertScheduledReader(
            SkinnedMeshCombinerMeshAPI.PendingCombine pending,
            SubMeshTriangles cache, bool lod, int expectedCacheLength = 9)
        {
            Assert.That(pending, Is.Not.Null, "Real preparation must return owned pending output.");
            Assert.That(pending.JobsScheduled, Is.True, "No useful old-red exists without a scheduled real reader.");
            Assert.That(pending.IndexValidationCount, Is.EqualTo(1),
                "This fixture schedules exactly one actual copy-index job.");
            Assert.That(cache.nativeTriangles.IsCreated, Is.True);
            Assert.That(cache.nativeTriangles.Length, Is.EqualTo(expectedCacheLength));
            Assert.That(cache.GetTriangles(lod ? 1 : 0).Length, Is.EqualTo(6));
            Assert.That(cache.GetLODRange(lod ? 1 : 0).offset, Is.EqualTo(lod ? 3U : 0U));
            Assert.That(SubMeshTriangles.nativeTrianglesAllocated.Contains(cache), Is.True);
            // Deliberately no Complete/IsCompleted wait or output read before tested disposal.
        }

        private static void AssertOnlyOwnedCacheRegistered(SubMeshTriangles owned)
        {
            var registry = SubMeshTriangles.nativeTrianglesAllocated;
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.Count, Is.EqualTo(1),
                "Foreign UMA caches make this global-disposal fixture unqualified; never clear them.");
            Assert.That(ReferenceEquals(registry[0], owned), Is.True,
                "Global disposal is permitted only for the exact test-owned cache instance.");
        }

        private static void AssertOutput(
            SkinnedMeshCombinerMeshAPI.PendingCombine pending,
            bool wide, bool masked, bool lod)
        {
            // Complete remaining unrelated output jobs only AFTER the tested source disposal.
            pending.CompleteJobs();
            int[] expected = lod ? (masked ? new[] { 1, 3, 2 } : new[] { 1, 3, 2, 0, 2, 3 }) :
                masked ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 1, 3, 2 };
            Assert.That(pending.SubWrite[0], Is.EqualTo(expected.Length));
            Assert.That(pending.IndexValidation[0], Is.EqualTo(0));
            if (wide)
                CollectionAssert.AreEqual(expected, pending.MeshDataArray[0].GetIndexData<int>().ToArray());
            else
            {
                var actual = pending.MeshDataArray[0].GetIndexData<ushort>().ToArray();
                var converted = Array.ConvertAll(actual, value => (int)value);
                CollectionAssert.AreEqual(expected, converted);
            }
        }

        private static void WithFixture(Action<OwnedFixture> action)
        {
            var fixture = new OwnedFixture();
            Exception failure = null;
            try
            {
                action(fixture);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            try
            {
                fixture.Dispose();
            }
            catch (Exception cleanup)
            {
                failure = failure == null ? cleanup : new AggregateException(failure, cleanup);
            }
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private sealed class OwnedFixture : IDisposable
        {
            internal readonly SubMeshTriangles Cache =
                new SubMeshTriangles(new[] { 0, 1, 2, 1, 3, 2, 0, 2, 3 });
            private GameObject root;
            private UMAData data;
            private SkinnedMeshRenderer renderer;
            private readonly List<SlotDataAsset> assets = new List<SlotDataAsset>();
            private readonly List<SkinnedMeshCombinerMeshAPI.PendingCombine> pending =
                new List<SkinnedMeshCombinerMeshAPI.PendingCombine>();

            internal SkinnedMeshCombinerMeshAPI.PendingCombine Prepare(
                bool staged, bool wide, bool masked, bool lod, bool copiedOwner)
            {
                if (root == null)
                {
                    root = new GameObject("TriangleReaderFenceOwnedFixture");
                    data = root.AddComponent<UMAData>();
                    renderer = root.AddComponent<SkinnedMeshRenderer>();
                    Assert.That(data.umaRecipe != null, Is.True,
                        "Bare UMAData must initialize its recipe without a generator factory.");
                    Cache.SetLodRanges(new List<UMALodRange>
                    {
                        new UMALodRange(0, 6), new UMALodRange(3, 6)
                    });
                }
                data.force32bit = wide;
                data.currentLODLevel = lod ? 1 : 0;
                var source = new UMAMeshData
                {
                    SlotName = "TriangleReaderFenceOwnedSlot",
                    vertexCount = 4, subMeshCount = 1,
                    vertices = new[]
                    {
                        Vector3.zero, Vector3.right, Vector3.up, Vector3.one
                    },
                    submeshes = new[] { Cache },
                    ManagedBonesPerVertex = new byte[4],
                    ManagedBoneWeights = Array.Empty<BoneWeight1>(),
                    boneNameHashes = Array.Empty<int>(),
                    bindPoses = Array.Empty<Matrix4x4>(),
                    umaBones = Array.Empty<UMATransform>()
                };
                if (copiedOwner)
                {
                    source = source.DeepCopy();
                    Assert.That(ReferenceEquals(source.submeshes[0], Cache), Is.True,
                        "The actual vendor copy preserves this cache owner identity.");
                }
                var asset = ScriptableObject.CreateInstance<SlotDataAsset>();
                assets.Add(asset);
                asset.meshData = source;
                var instance = new SkinnedMeshCombiner.CombineInstance
                {
                    meshData = source,
                    targetSubmeshIndices = new[] { 0 },
                    slotData = new SlotData(asset),
                    applyMeshModifiersInJobs = false,
                    triangleMask = masked
                        ? new[] { new BitArray(new[] { false, true }) } : null
                };
                var batch = new SkinnedMeshCombinerMeshAPI.RendererBatch
                {
                    Renderer = renderer, Sources = new[] { instance },
                    AtlasResolution = 1, SkipSkeletonUpdate = true
                };
                SkinnedMeshCombinerMeshAPI.PendingCombine result;
                if (!staged)
                {
                    result = SkinnedMeshCombinerMeshAPI.PrepareIncrementalCombine(
                        batch, data, new Dictionary<string, float>(),
                        false, false, Quaternion.identity);
                }
                else
                {
                    using (var preparation =
                        SkinnedMeshCombinerMeshAPI.BeginIncrementalCombinePreparation(
                            batch, data, new Dictionary<string, float>(),
                            false, false, Quaternion.identity))
                    {
                        int steps = 0;
                        while (!preparation.IsComplete && steps++ < 64)
                            preparation.Step();
                        Assert.That(preparation.IsComplete, Is.True,
                            "Finite real incremental preparation must finish before testing cache disposal.");
                        Assert.That(preparation.Error == null, Is.True,
                            "Preparation failure is not a useful source-cache regression red.");
                        result = preparation.TakeResult();
                    }
                }
                pending.Add(result);
                return result;
            }

            public void Dispose()
            {
                var failures = new List<Exception>();
                // Own pending combines finish before any retry of own-cache disposal, including old-red unwind.
                foreach (var item in pending)
                    TryCleanup(() => item.Dispose(), failures);
                TryCleanup(() => Cache.DisposeNativeTriangles(true), failures);
                foreach (var asset in assets)
                {
                    // SlotDataAsset.OnDestroy otherwise globally frees foreign caches.
                    asset.meshData = null;
                    TryCleanup(() => Object.DestroyImmediate(asset), failures);
                }
                if (root != null)
                    TryCleanup(() => Object.DestroyImmediate(root), failures);
                if (failures.Count > 0)
                    throw new AggregateException("Owned reader-fence fixture cleanup failed.", failures);
            }

            private static void TryCleanup(Action action, List<Exception> failures)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }
        }
    }
}
#endif
