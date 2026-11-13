using System;
using System.Collections.Generic;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Geometry fixtures, not character art or visual acceptance. The oracle projects analytically
    /// skinned source vertices through Unity's camera; it never calls the production bounds/fitter.
    /// This source also compiles against the old preview: the optional activation field is absent
    /// there, so the same projection assertions exercise the old renderer-bounds behavior.
    /// </summary>
    public sealed class CharacterStudioFramingTests
    {
        private CharacterStudioPreview preview;
        private RenderTexture ownedTexture;
        private Mesh ownedSample;
        private SingleBoneProvider provider;
        private ICharacterBodyProvider previousProvider;

        [SetUp]
        public void SetUp()
        {
            previousProvider = CharacterBodySource.Provider;
            provider = new SingleBoneProvider();
            CharacterBodySource.Register(provider);
            preview = CharacterStudioPreview.Create("Creator framing geometry fixture");
            ownedTexture = (RenderTexture)preview.Texture;
            EnableCandidateSampling(true);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (preview != null)
                {
                    RetainOwnedSample();
                    Invoke("OnDestroy");
                }
            }
            finally
            {
                // Match the explicit EditMode lifecycle used by CharacterStudioShutdownTests.
                // A failed projection, shutdown assertion or callback must still release native resources.
                try
                {
                    if (preview != null) Object.DestroyImmediate(preview.gameObject);
                }
                finally
                {
                    CharacterBodySource.Unregister(provider);
                    if (ownedSample != null) Object.DestroyImmediate(ownedSample);
                    if (ownedTexture != null) { ownedTexture.Release(); Object.DestroyImmediate(ownedTexture); }
                    foreach (var mesh in provider.Meshes) if (mesh != null) Object.DestroyImmediate(mesh);
                    Assert.That(CharacterBodySource.Provider, Is.SameAs(previousProvider));
                }
            }
        }

        [TestCase(0f, false)]
        [TestCase(90f, false)]
        [TestCase(0f, true)]
        [TestCase(90f, true)]
        public void FullBodyContainsTheDeformedCrownWhenRendererBoundsAreStale(float turn, bool nonUniform)
        {
            provider.NonUniform = nonUniform;
            Build(CharacterAppearance.Preset("player"));
            preview.View(turn);

            var points = AnalyticWorldVertices();
            float realTop = float.MinValue;
            foreach (var point in points) realTop = Mathf.Max(realTop, point.y);
            Assert.That(realTop, Is.GreaterThan(provider.Skin.bounds.max.y + .2f),
                "The fixture must actually expose a deformed crown outside its deliberately stale culling bounds.");
            AssertAllVerticesInFrame(points);
            Assert.That(provider.Skin.localBounds, Is.EqualTo(provider.OriginalCullingBounds),
                "Framing must not rewrite the provider's culling bounds to make its measurement pass.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FullBodyAlsoFitsWidthAndDepthWhenItsBoundsAreAccurate()
        {
            provider.HeightScale = 1f;
            provider.UseStaleWorldBounds = false;
            provider.HalfWidth = .9f;
            provider.HalfDepth = .55f;
            Build(CharacterAppearance.Preset("player"));
            AssertAllVerticesInFrame(AnalyticWorldVertices());
            preview.View(90f);
            AssertAllVerticesInFrame(AnalyticWorldVertices());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnExplicitViewSamplesTheCurrentPoseAndReusesItsOwnedBuffers()
        {
            Build(CharacterAppearance.Preset("player"));
            var sample = CandidateField("framingMesh");
            var vertices = CandidateField("framingVertices");
            var position = Camera.transform.position;
            provider.Bone.localScale = new Vector3(1f, 2f, 1f);
            Invoke("Update");
            Assert.That(Camera.transform.position, Is.EqualTo(position), "Idle Update adds no new bake/render loop.");

            preview.View(90f);

            AssertAllVerticesInFrame(AnalyticWorldVertices());
            Assert.That(CandidateField("framingMesh"), Is.SameAs(sample));
            Assert.That(CandidateField("framingVertices"), Is.SameAs(vertices));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NewAppearanceRetiresTheOldRosterAndFitsItsNewBody()
        {
            var appearance = CharacterAppearance.Preset("player");
            provider.HeightScale = 1.05f;
            Build(appearance);
            var oldRoot = provider.Root;
            var oldSkin = provider.Skin;
            var sample = CandidateField("framingMesh");
            provider.HeightScale = 1.8f;
            var changed = appearance.Clone();
            AppearanceEditing.SetValue(changed, "height", .29f);

            Build(changed);
            preview.View(90f);

            Assert.That(oldRoot == null && oldSkin == null, Is.True);
            Assert.That(preview.CompletedKey, Is.EqualTo(changed.ContentKey()));
            AssertAllVerticesInFrame(AnalyticWorldVertices());
            Assert.That(CandidateField("framingMesh"), Is.SameAs(sample), "A revision reuses the studio's sample mesh.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ZoomAndFaceFocusRemainUserControlled()
        {
            Build(CharacterAppearance.Preset("player"));
            var points = AnalyticWorldVertices();
            float normalHeight = ProjectedHeight(points);
            preview.Zoom(.4f);
            Assert.That(ProjectedHeight(points), Is.GreaterThan(normalHeight));
            var closest = Camera.transform.position;
            preview.Zoom(10f);
            Assert.That(Camera.transform.position, Is.EqualTo(closest), "The existing upper zoom clamp remains.");
            preview.Zoom(-10f);
            Assert.That(ProjectedHeight(points), Is.LessThan(normalHeight));
            preview.Zoom(.3f);
            preview.FocusFace(true);
            Assert.That(ProjectedHeight(points), Is.GreaterThan(normalHeight * 1.5f), "Face focus keeps its deliberate close crop.");
            preview.FocusFace(false);
            AssertAllVerticesInFrame(points);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DefaultSharedStudioKeepsTheCastTurnFreeOfBakedSamples()
        {
            EnableCandidateSampling(false);
            Build(CharacterAppearance.Preset("player"));
            for (int i = 0; i < 12; i++) preview.Rotate(3f);
            Assert.That(CandidateField("framingMesh"), Is.Null,
                "CastSelect's per-frame slow turn uses this default path; no sample mesh may be created there.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ShutdownDestroysTheOwnedSampleButPreservesBorrowedSourceGeometry()
        {
            Build(CharacterAppearance.Preset("player"));
            RetainOwnedSample();
            var source = provider.Skin.sharedMesh;
            var borrowedBounds = source.bounds;
            // Ordinary MonoBehaviours need explicit lifecycle dispatch in this EditMode fixture.
            // Assert production cleanup before teardown's failure-safe fallback can destroy anything.
            Invoke("OnDestroy");
            Assert.That(ownedSample == null, Is.True);
            Assert.That(ownedTexture == null, Is.True);
            Assert.That(preview.Texture == null, Is.True);
            Assert.That(source != null, Is.True);
            Assert.That(source.bounds, Is.EqualTo(borrowedBounds));
            LogAssert.NoUnexpectedReceived();
        }

        private Camera Camera => preview.GetComponentInChildren<Camera>();

        private void EnableCandidateSampling(bool enabled)
        {
            typeof(CharacterStudioPreview).GetField("fitDeformedBody", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(preview, enabled);
        }

        private object CandidateField(string name) => typeof(CharacterStudioPreview)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(preview);

        private void RetainOwnedSample()
        {
            var sample = CandidateField("framingMesh") as Mesh;
            if (sample != null) ownedSample = sample;
        }

        private void Build(CharacterAppearance appearance)
        {
            preview.Show(appearance);
            try
            {
                Invoke("Build");
                Invoke("Update");
            }
            finally { RetainOwnedSample(); }
            Assert.That(preview.IsBuilding, Is.False);
            Assert.That(preview.CompletedKey, Is.EqualTo(appearance.ContentKey()));
        }

        private void Invoke(string name)
        {
            var method = typeof(CharacterStudioPreview).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(preview, null);
        }

        private Vector3[] AnalyticWorldVertices()
        {
            // Every fixture vertex has exactly weight 1 on bone 0. Linear skinning therefore has
            // this exact independent world-space expression, with no BakeMesh/production helper.
            var world = new Vector3[provider.RestVertices.Length];
            var matrix = provider.Bone.localToWorldMatrix * provider.BindPose;
            for (int i = 0; i < world.Length; i++) world[i] = matrix.MultiplyPoint3x4(provider.RestVertices[i]);
            return world;
        }

        private void AssertAllVerticesInFrame(Vector3[] world)
        {
            foreach (var point in world)
            {
                var viewport = Camera.WorldToViewportPoint(point);
                Assert.That(viewport.z, Is.InRange(Camera.nearClipPlane, Camera.farClipPlane), "Camera depth: " + viewport);
                Assert.That(viewport.x, Is.InRange(.02f, .98f), "Horizontal framing: " + viewport);
                Assert.That(viewport.y, Is.InRange(.02f, .98f), "Crown/feet framing: " + viewport);
            }
        }

        private float ProjectedHeight(Vector3[] world)
        {
            float low = float.MaxValue, high = float.MinValue;
            foreach (var point in world)
            {
                float y = Camera.WorldToViewportPoint(point).y;
                low = Mathf.Min(low, y); high = Mathf.Max(high, y);
            }
            return high - low;
        }

        private sealed class SingleBoneProvider : IModularCharacterBodyProvider
        {
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public bool NonUniform;
            public bool UseStaleWorldBounds = true;
            public float HeightScale = 1.6f, HalfWidth = .14f, HalfDepth = .07f;
            public GameObject Root;
            public Transform Bone;
            public SkinnedMeshRenderer Skin;
            public Vector3[] RestVertices;
            public Matrix4x4 BindPose;
            public Bounds OriginalCullingBounds;
            public ICharacterAppearanceCatalog Catalog => null;

            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color color, out CharacterBody body)
            {
                Root = new GameObject("Single-bone geometry fixture");
                Root.transform.SetParent(parent, false);
                if (NonUniform) Root.transform.localScale = new Vector3(1.2f, 1.08f, .75f);
                Bone = new GameObject("Fixture bone").transform;
                Bone.SetParent(Root.transform, false);
                var rendered = new GameObject("Fixture skin");
                rendered.transform.SetParent(Root.transform, false);
                if (NonUniform)
                {
                    rendered.transform.localScale = new Vector3(1.15f, .9f, 1.6f);
                    rendered.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
                }
                RestVertices = new Vector3[8];
                var weights = new BoneWeight[8];
                for (int i = 0; i < 8; i++)
                {
                    RestVertices[i] = new Vector3((i & 1) == 0 ? -HalfWidth : HalfWidth,
                        (i & 2) == 0 ? 0f : 1.7f, (i & 4) == 0 ? -HalfDepth : HalfDepth);
                    weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                }
                BindPose = Bone.worldToLocalMatrix * rendered.transform.localToWorldMatrix;
                var mesh = new Mesh { name = "Borrowed single-bone fixture geometry" };
                mesh.vertices = RestVertices;
                mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6,
                    0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5, 2, 6, 3, 3, 6, 7, 0, 1, 4, 1, 5, 4 };
                mesh.boneWeights = weights;
                mesh.bindposes = new[] { BindPose };
                mesh.RecalculateBounds();
                Meshes.Add(mesh);
                Skin = rendered.AddComponent<SkinnedMeshRenderer>();
                Skin.sharedMesh = mesh;
                Skin.rootBone = Bone;
                Skin.bones = new[] { Bone };
                Skin.quality = SkinQuality.Bone1;
                Skin.localBounds = mesh.bounds;
                OriginalCullingBounds = Skin.localBounds;
                if (UseStaleWorldBounds)
                {
                    // Pin the deliberately old WORLD culling bound. A rootBone scale can otherwise
                    // carry a local bound along with the deformation and defeat this stale fixture.
                    var oldWorld = new Bounds(rendered.transform.TransformPoint(RestVertices[0]), Vector3.zero);
                    foreach (var point in RestVertices) oldWorld.Encapsulate(rendered.transform.TransformPoint(point));
                    Skin.bounds = oldWorld;
                }
                Bone.localScale = new Vector3(1f, HeightScale, 1f);
                var state = Root.AddComponent<CharacterBodyBuildState>();
                state.Revision = request.Revision;
                state.Ready = true;
                body = new CharacterBody(Root, null, deferred: true);
                return true;
            }

            public bool TryCreate(string id, Transform parent, Color color, out CharacterBody body)
            {
                body = default;
                throw new InvalidOperationException("The fixture expects the studio's appearance request.");
            }

            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}
