using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// Proves the UMA pipeline actually produces a houseguest, end to end, through the same seam the
    /// episode uses — rather than proving only that it compiles.
    ///
    /// This assembly is constrained on GAMESIM_UMA, so on a clone without the UMA package it is
    /// excluded along with the rest of the integration and these tests simply do not exist.
    /// </summary>
    public sealed class UmaCastPlayModeTests
    {
        // UMA atlases overlays and combines meshes across several frames; on a cold first build it
        // also warms its shaders. Generous, because a flaky timeout would be worse than a slow test.
        private const int BuildTimeoutFrames = 900;

        private GameObject cast, actor;

        [SetUp]
        public void CreateCast()
        {
            cast = new GameObject("Gamesim UMA cast", typeof(GamesimUmaCast));
            actor = new GameObject("UMA houseguest");
        }

        [UnityTearDown]
        public IEnumerator DestroyCast()
        {
            if (actor != null) Object.Destroy(actor);
            if (cast != null) Object.Destroy(cast);
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Null, "The cast component must unregister itself.");
        }

        [UnityTest]
        public IEnumerator GamesimUmaCast_RegistersTheProvider()
        {
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Not.Null,
                "A scene carrying the component opts into UMA bodies.");
            Assert.That(CharacterBodySource.Provider, Is.TypeOf<UmaBodyProvider>());
        }

        [UnityTest]
        public IEnumerator EveryCastLook_ResolvesItsRaceAndWardrobe()
        {
            yield return null;
            var indexer = UMAAssetIndexer.Instance;
            Assert.That(indexer, Is.Not.Null, "The Global Library must be present for any of this to work.");

            foreach (var appearanceId in UmaCastLibrary.AppearanceIds)
            {
                Assert.That(UmaCastLibrary.TryGet(appearanceId, out var look), Is.True);
                Assert.That(indexer.GetRace(look.Race), Is.Not.Null,
                    appearanceId + " names race '" + look.Race + "', which is not in the Global Library. " +
                    "Races are keyed by race name, not by asset file name.");

                foreach (var recipeName in look.Wardrobe)
                    Assert.That(indexer.GetAsset<UMAWardrobeRecipe>(recipeName), Is.Not.Null,
                        appearanceId + " wears '" + recipeName + "', which is not in the Global Library.");
            }
        }

        /// <summary>
        /// Every DNA name the cast library uses must exist on the race. A misspelling is silently
        /// ignored by UMA, so without this the house proportions would simply not apply and the cast
        /// would quietly go back to looking realistic — the same failure mode as the race name that
        /// cost a day, where the wrong string produced a misleading error instead of no effect.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryDnaNameUsedByTheCastLibraryExists()
        {
            yield return null;
            CharacterPresentation.Attach(actor, Houseguest(), Color.white);

            UMA.CharacterSystem.DynamicCharacterAvatar avatar = null;
            for (int frame = 0; frame < BuildTimeoutFrames && avatar == null; frame++)
            {
                yield return null;
                if (UmaMesh() != null) avatar = actor.GetComponentInChildren<UMA.CharacterSystem.DynamicCharacterAvatar>(true);
            }
            Assert.That(avatar, Is.Not.Null, "UMA never built a body to read DNA from.");

            var available = avatar.GetDNA();
            var used = new HashSet<string>(UmaCastLibrary.HouseProportions.Keys);
            foreach (var appearanceId in UmaCastLibrary.AppearanceIds)
                if (UmaCastLibrary.TryGet(appearanceId, out var look))
                    foreach (var name in look.Dna.Keys) used.Add(name);

            var unknown = used.Where(name => !available.ContainsKey(name)).ToArray();
            Assert.That(unknown, Is.Empty,
                "These DNA names do not exist on " + avatar.activeRace.name + " and would be ignored: "
                + string.Join(", ", unknown));
        }

        [UnityTest]
        public IEnumerator Houseguest_BuildsARiggedBodyOfHumanProportions()
        {
            yield return null;
            var houseguest = Houseguest();
            CharacterPresentation.Attach(actor, houseguest, new Color(0.26f, 0.76f, 0.65f));

            SkinnedMeshRenderer mesh = null;
            for (int frame = 0; frame < BuildTimeoutFrames && mesh == null; frame++)
            {
                yield return null;
                mesh = UmaMesh();
            }

            // The house proportions are applied once the character exists and trigger a second
            // build, so the first mesh to appear is the un-stylised one. Wait for the rebuild.
            for (int i = 0; i < 240; i++) yield return null;
            mesh = UmaMesh();

            var avatar = actor.GetComponentInChildren<UMA.CharacterSystem.DynamicCharacterAvatar>(true);
            var dna = avatar.GetDNA();
            // The effective set is the house proportions with this persona's overrides on top, which
            // is what the provider composes — asserting the house values alone would fail on anyone
            // who overrides one.
            foreach (var entry in EffectiveProportions(houseguest))
            {
                if (!dna.TryGetValue(entry.Key, out var setter)) continue;
                Assert.That(setter.Value, Is.EqualTo(entry.Value).Within(0.01f),
                    "Proportion '" + entry.Key + "' did not take; UMA is still at " + setter.Value + ".");
            }

            Assert.That(mesh, Is.Not.Null, "UMA never produced a mesh within " + BuildTimeoutFrames + " frames.");
            Assert.That(mesh.sharedMesh, Is.Not.Null, "The renderer arrived without a mesh.");
            Assert.That(mesh.sharedMesh.vertexCount, Is.GreaterThan(1000), "A UMA body is not a placeholder.");
            Assert.That(mesh.bones.Length, Is.GreaterThan(100), "The body should arrive fully rigged.");

            // Bounds are unreliable straight after generation; bake the pose and measure the vertices.
            var baked = new UnityEngine.Mesh();
            mesh.BakeMesh(baked, true);
            var verts = baked.vertices;
            float low = verts.Min(v => v.y), high = verts.Max(v => v.y);
            Object.Destroy(baked);
            Debug.Log("[Gamesim.Uma] stylised houseguest height: " + (high - low).ToString("F3") + " m");
            // Deliberately shorter than anatomical: the house proportions trade realism for a
            // silhouette that reads against Kenney furniture.
            // Measured 1.717 m for this houseguest. UMA's un-stylised default for the same character
            // is 2.05 m, so a result back near that means the DNA has silently stopped applying.
            Assert.That(high - low, Is.GreaterThan(1.5f).And.LessThan(1.85f),
                "The houseguest should stand at the house's stylised height, not UMA's default.");
        }

        [UnityTest]
        public IEnumerator BuiltBody_IsFlattenedTowardTheHouseLook()
        {
            yield return null;
            CharacterPresentation.Attach(actor, Houseguest(), Color.cyan);

            SkinnedMeshRenderer mesh = null;
            for (int frame = 0; frame < BuildTimeoutFrames && mesh == null; frame++)
            {
                yield return null;
                mesh = UmaMesh();
            }
            Assert.That(mesh, Is.Not.Null);

            // The stylize pass runs on CharacterUpdated, which is the same frame the mesh appears.
            foreach (var material in mesh.sharedMaterials.Where(m => m != null))
            {
                if (material.HasProperty("_Smoothness"))
                    Assert.That(material.GetFloat("_Smoothness"), Is.LessThanOrEqualTo(0.1f),
                        material.name + " is still glossy; the house is flat-shaded.");
                if (material.HasProperty("_Metallic"))
                    Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0f).Within(0.001f));
                if (material.HasProperty("_BumpScale"))
                    Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(0f).Within(0.001f),
                        material.name + " still carries normal-map detail the set does not have.");
            }
        }

        [UnityTest]
        public IEnumerator BuiltBody_ReplacesThePrimitiveRigRatherThanJoiningIt()
        {
            yield return null;
            CharacterPresentation.Attach(actor, Houseguest(), Color.white);

            for (int frame = 0; frame < BuildTimeoutFrames; frame++)
            {
                yield return null;
                if (UmaMesh() != null) break;
            }

            var visual = actor.transform.Find("Gamesim Character Visual");
            Assert.That(visual, Is.Not.Null, "The named visual root is a test contract.");
            Assert.That(visual.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Head pivot"),
                Is.False, "A UMA body replaces the primitive rig; it must not stack on top of it.");
            Assert.That(visual.GetComponentsInChildren<Transform>(true).Any(t => t.name == "UMA Body"),
                Is.True);
        }

        /// <summary>
        /// A houseguest must have something to draw from the frame they are attached, not from the
        /// frame UMA finishes. Otherwise the cast pops in half a second after the scene is running.
        /// </summary>
        [UnityTest]
        public IEnumerator Houseguest_IsNeverInvisibleWhileUmaAssemblesTheBody()
        {
            yield return null;
            CharacterPresentation.Attach(actor, Houseguest(), Color.white);

            // Same frame: a stand-in should already be drawable.
            Assert.That(VisibleRenderers(), Is.GreaterThan(0),
                "The houseguest had no visible body on the frame they were attached.");

            bool built = false;
            for (int frame = 0; frame < BuildTimeoutFrames && !built; frame++)
            {
                Assert.That(VisibleRenderers(), Is.GreaterThan(0),
                    "The houseguest went invisible on frame " + frame + " while UMA was assembling.");
                yield return null;
                built = UmaMesh() != null;
            }
            Assert.That(built, Is.True, "UMA never produced a body.");

            // Once the real body exists the stand-in must go, or every houseguest is drawn twice.
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(actor.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Stand-in"),
                Is.False, "The stand-in outlived the body it was standing in for.");
        }

        private int VisibleRenderers() =>
            actor.GetComponentsInChildren<Renderer>(true).Count(renderer => renderer.enabled);

        /// <summary>
        /// The UMA body's mesh specifically. Scoping matters: while UMA assembles, an authored
        /// stand-in with its own skinned mesh is also under the houseguest, and a whole-hierarchy
        /// search will happily return that one and assert against the wrong body.
        /// </summary>
        private SkinnedMeshRenderer UmaMesh()
        {
            var body = actor.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "UMA Body");
            return body == null ? null : body.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        /// <summary>House proportions with this houseguest's own overrides applied, as the provider composes them.</summary>
        private static Dictionary<string, float> EffectiveProportions(ContestantState contestant)
        {
            var merged = new Dictionary<string, float>(UmaCastLibrary.HouseProportions);
            var appearanceId = CharacterPresentation.AppearanceId(contestant, ContentCatalog.CanonicalId(contestant.id));
            if (UmaCastLibrary.TryGet(appearanceId, out var look))
                foreach (var entry in look.Dna) merged[entry.Key] = entry.Value;
            return merged;
        }

        private static ContestantState Houseguest() =>
            ContentCatalog.Create(1).contestants.First(contestant => !contestant.isPlayer);
    }
}
