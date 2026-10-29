using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// A houseguest's UMA body, once destroyed, leaves nothing of itself held: not on UMA's
    /// generator, which outlives every scene, and not through its avatar's events.
    ///
    /// <para>Both pins were found by reading the code while tracing what the batch runs' heap held.
    /// UMAGeneratorBuiltin is compiled with UMA_DEBUG and lists every avatar it builds in <c>umaDatasGenerated</c>, never
    /// removing one, so every destroyed avatar's managed shell stayed reachable. The body's tint took
    /// its handlers off the avatar only while the avatar did not yet read as destroyed, so a shell
    /// kept that way kept the tint, and all it holds, as well.</para>
    /// </summary>
    public sealed class UmaBodyRetentionPlayModeTests
    {
        private const double BuildSeconds = 30;
        private const string GeneratedField = "umaDatasGenerated";
        private const string TintType = "Gamesim.Uma.UmaBodyTint";

        private GameObject cast;
        private readonly List<GameObject> actors = new List<GameObject>();

        private sealed class Body
        {
            public GameObject Actor;
            public CharacterPresentation Presentation;
            public DynamicCharacterAvatar Avatar;
            public Component Tint;
        }

        [SetUp]
        public void CreateCast()
        {
            cast = new GameObject("UMA retention cast", typeof(GamesimUmaCast));
        }

        [UnityTearDown]
        public IEnumerator DestroyCast()
        {
            foreach (var actor in actors) if (actor != null) Object.Destroy(actor);
            actors.Clear();
            if (cast != null) Object.Destroy(cast);
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Null, "The cast component must unregister itself.");
        }

        /// <summary>
        /// A house torn down with its cast, the way a scene unload takes it, leaves none of its
        /// avatars on UMA's generator, nor any other destroyed one: each body prunes the list as it
        /// goes. Before the fix nothing ever took an avatar off it.
        ///
        /// <para>It also logs whether, while each body's object was torn down, its avatar already read
        /// as destroyed beside the tint. That is what the tint's old <c>avatar != null</c> guard got
        /// wrong; when it never happens here, the old guard failed only on an avatar destroyed ahead of
        /// its object (<see cref="ATintLetsGoOfAnAvatarDestroyedBeforeIt"/>).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ATornDownHouseLeavesNoneOfItsAvatarsOnUmasGenerator()
        {
            yield return null;
            var generated = Generated();
            var house = new List<Body>();
            foreach (var houseguest in ContentCatalog.Create(1).contestants.Take(2)) house.Add(Attach(houseguest));
            foreach (var body in house) yield return Settle(body);

            var witnessed = new Witnessed();
            foreach (var body in house)
            {
                Assert.That(generated.Any(data => ReferenceEquals(data, body.Avatar)), Is.True,
                    "UMA lists the avatar it built; without that there is nothing here to prune.");
                Assert.That(Listeners(body.Avatar.CharacterUpdated, body.Tint), Is.GreaterThan(0),
                    "The probe sees the tint listening to CharacterUpdated while the body stands.");
                Assert.That(Listeners(body.Avatar.CharacterBegun, body.Tint), Is.GreaterThan(0),
                    "The probe sees the tint listening to CharacterBegun while the body stands.");
                var witness = body.Avatar.gameObject.AddComponent<TeardownWitness>();
                witness.Avatar = body.Avatar;
                witness.Into = witnessed;
            }

            foreach (var body in house) Object.Destroy(body.Actor);
            Object.Destroy(cast);
            yield return null;
            yield return null;

            Assert.That(witnessed.Bodies, Is.EqualTo(house.Count), "Every body was torn down.");
            Debug.Log("[Gamesim.Uma] Retention: in " + witnessed.AvatarGone + " of " + witnessed.Bodies
                + " body teardowns the avatar already read as destroyed beside its tint.");
            int ours = house.Count(body => generated.Any(data => ReferenceEquals(data, body.Avatar)));
            Assert.That(ours, Is.Zero,
                ours + " of the house's " + house.Count + " destroyed avatars are still listed on UMA's generator.");
            int destroyed = generated.Count(data => data == null);
            Assert.That(destroyed, Is.Zero, destroyed + " destroyed avatars are still listed on UMA's generator.");
            foreach (var body in house)
                Assert.That(Listeners(body.Avatar.CharacterUpdated, body.Tint) + Listeners(body.Avatar.CharacterBegun, body.Tint),
                    Is.Zero, "A torn-down avatar's events still call into its tint.");
        }

        /// <summary>
        /// A tint takes its handlers off its avatar even when the avatar has gone first: here the
        /// avatar is destroyed on its own the instant before its object, so the tint's teardown finds
        /// it reading as null, as Unity's overloaded equality reports every destroyed object. The old
        /// <c>avatar != null</c> guard then skipped the unsubscribe; UMA does not clear the events
        /// when an avatar is destroyed, so its managed shell, which UMA's generator kept, went on
        /// holding the tint.
        /// </summary>
        [UnityTest]
        public IEnumerator ATintLetsGoOfAnAvatarDestroyedBeforeIt()
        {
            yield return null;
            var body = Attach(ContentCatalog.Create(1).contestants.First(contestant => !contestant.isPlayer));
            yield return Settle(body);
            Assert.That(Listeners(body.Avatar.CharacterUpdated, body.Tint), Is.GreaterThan(0),
                "The probe sees the tint listening to CharacterUpdated while the body stands.");
            Assert.That(Listeners(body.Avatar.CharacterBegun, body.Tint), Is.GreaterThan(0),
                "The probe sees the tint listening to CharacterBegun while the body stands.");

            var avatar = body.Avatar;
            Object.DestroyImmediate(avatar);
            Assert.That(avatar == null, Is.True, "The avatar reads as destroyed...");
            Assert.That(body.Tint != null, Is.True, "...while its tint is still to go.");
            Object.DestroyImmediate(body.Actor);
            Assert.That(body.Tint == null, Is.True, "The tint has gone with its object.");

            Assert.That(Listeners(avatar.CharacterUpdated, body.Tint), Is.Zero,
                "The destroyed avatar's CharacterUpdated still calls into the destroyed tint, and holds it.");
            Assert.That(Listeners(avatar.CharacterBegun, body.Tint), Is.Zero,
                "The destroyed avatar's CharacterBegun still calls into the destroyed tint, and holds it.");
            yield return null;
        }

        /// <summary>
        /// A body whose tint never heard a build begin still comes off UMA's generator. The tint learns
        /// which generator built its avatar from CharacterBegun, but UMA lists an avatar before it
        /// validates it, so a body whose builds never validate - one that times out to the primitive
        /// rig - is listed and never begun. Here a built body's tint is made to forget its generator,
        /// as if it had never heard, and is torn down at once, before any build could begin again: its
        /// teardown prunes the generator in effect. Before, it pruned nothing, and the avatar stayed
        /// listed until some other body's teardown swept it.
        /// </summary>
        [UnityTest]
        public IEnumerator ABodyWhoseBuildNeverBeganStillComesOffUmasGenerator()
        {
            yield return null;
            var generated = Generated();
            var body = Attach(ContentCatalog.Create(1).contestants.First(contestant => !contestant.isPlayer));
            yield return Settle(body);
            Assert.That(generated.Any(data => ReferenceEquals(data, body.Avatar)), Is.True,
                "UMA lists the avatar it built; without that there is nothing here to prune.");
            var learnt = body.Tint.GetType().GetField("generator", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(learnt, Is.Not.Null, "The tint keeps the generator its avatar's build began on.");
            Assert.That(learnt.GetValue(body.Tint), Is.InstanceOf<UMAGeneratorBase>(),
                "The tint learnt its generator when the build began, so forgetting it below is a change.");

            learnt.SetValue(body.Tint, null);
            var avatar = body.Avatar;
            Object.DestroyImmediate(body.Actor);
            Assert.That(avatar == null, Is.True, "The body was torn down.");

            Assert.That(generated.Any(data => ReferenceEquals(data, avatar)), Is.False,
                "A destroyed avatar whose tint never heard its build begin is still listed on UMA's generator.");
            int destroyed = generated.Count(data => data == null);
            Assert.That(destroyed, Is.Zero, destroyed + " destroyed avatars are still listed on UMA's generator.");
            yield return null;
        }

        private Body Attach(ContestantState houseguest)
        {
            var actor = new GameObject("Retention houseguest " + actors.Count);
            actor.transform.position = new Vector3(actors.Count * 1.5f, 0f, 0f);
            actors.Add(actor);
            return new Body { Actor = actor, Presentation = CharacterPresentation.Attach(actor, houseguest, Color.white) };
        }

        /// <summary>Waits for a body to finish building, then finds its avatar and the tint riding on it.</summary>
        private static IEnumerator Settle(Body body)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + BuildSeconds;
            while (!Settled(body.Presentation) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(Settled(body.Presentation), Is.True, "The body finished building.");
            yield return null;
            body.Avatar = body.Actor.GetComponentInChildren<DynamicCharacterAvatar>(true);
            Assert.That(body.Avatar != null, Is.True, "The houseguest stands on a UMA body.");
            var tintType = typeof(GamesimUmaCast).Assembly.GetType(TintType);
            Assert.That(tintType, Is.Not.Null, TintType + " exists.");
            body.Tint = body.Avatar.GetComponent(tintType);
            Assert.That(body.Tint != null, Is.True, "Every UMA body carries its tint on the avatar's own object.");
        }

        private static bool Settled(CharacterPresentation presentation)
        {
            if (presentation.IsBodyAssembling || presentation.VisualRoot == null) return false;
            var state = presentation.VisualRoot.GetComponentInChildren<CharacterBodyBuildState>();
            return state != null && state.Ready;
        }

        /// <summary>
        /// UMA's list of the avatars its generator has built. Read by name, as the fix reaches it,
        /// so this keeps compiling if UMA drops it; a UMA without it keeps no body alive this way,
        /// and there is nothing to test.
        /// </summary>
        private static List<UMAData> Generated()
        {
            var indexer = UMAAssetIndexer.Instance;
            Assert.That(indexer != null, Is.True, "The Global Library must be present for any of this to work.");
            var generator = indexer.Generator;
            Assert.That(generator != null, Is.True, "UMA provides a generator.");
            var field = generator.GetType().GetField(GeneratedField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var generated = field == null ? null : field.GetValue(generator) as List<UMAData>;
            if (generated == null) Assert.Ignore("This UMA keeps no " + GeneratedField + " list, so no built avatar is held by it.");
            return generated;
        }

        /// <summary>
        /// How many of an event's runtime listeners call into <paramref name="target"/>. UnityEvent
        /// keeps them privately (UnityEventBase.m_Calls, an InvokableCallList whose lists hold
        /// InvokableCall objects wrapping delegates), so this walks the event's own objects - those of
        /// UnityEngine.Events and the collections among them, never a scene object - for delegates,
        /// counting each listener once however many of the lists hold it. Every test sees the tint
        /// here before it tears anything down, so a probe a Unity update has blinded fails, not passes.
        /// </summary>
        private static int Listeners(UnityEventBase evt, object target)
        {
            if (evt == null) return 0;
            int found = 0;
            var seen = new HashSet<object>(SameObject.Instance);
            var pending = new Stack<object>();
            pending.Push(evt);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!seen.Add(node)) continue;
                if (node is System.Delegate handler)
                {
                    foreach (var single in handler.GetInvocationList())
                        if (ReferenceEquals(single.Target, target)) found++;
                    continue;
                }
                if (node is IEnumerable items)
                {
                    foreach (var item in items) if (Followed(item)) pending.Push(item);
                    continue;
                }
                for (var type = node.GetType(); type != null && type != typeof(object); type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.FieldType.IsValueType) continue;
                        var value = field.GetValue(node);
                        if (Followed(value)) pending.Push(value);
                    }
            }
            return found;
        }

        /// <summary>An event's own machinery: delegates, plain collections and the types of UnityEngine.Events.</summary>
        private static bool Followed(object value) =>
            value != null && !(value is Object) && !(value is string)
            && (value is System.Delegate || value is IEnumerable || value is UnityEventBase
                || value.GetType().Namespace == "UnityEngine.Events");

        private sealed class SameObject : IEqualityComparer<object>
        {
            public static readonly SameObject Instance = new SameObject();
            bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);
            int IEqualityComparer<object>.GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        private sealed class Witnessed
        {
            public int Bodies, AvatarGone;
        }

        /// <summary>
        /// Added to a body after its tint, so torn down in the same pass: notes whether the avatar
        /// beside it already read as destroyed, which is what the tint saw in its own teardown.
        /// </summary>
        private sealed class TeardownWitness : MonoBehaviour
        {
            internal DynamicCharacterAvatar Avatar;
            internal Witnessed Into;

            private void OnDestroy()
            {
                if (Into == null) return;
                Into.Bodies++;
                if (Avatar == null) Into.AvatarGone++;
            }
        }
    }
}
