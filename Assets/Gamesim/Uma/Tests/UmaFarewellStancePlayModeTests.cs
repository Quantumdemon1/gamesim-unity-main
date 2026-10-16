using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The farewell's planted stance on a UMA body: taken only from a settled Idle, held through the
    /// take and the first frame back in Idle, and let go - once the body has settled, or at once when
    /// another beat takes the body.
    /// </summary>
    public sealed class UmaFarewellStancePlayModeTests
    {
        // Wall-clock bounds on waits that end on a state. They decide only when a wait gives up, so they
        // are generous: one stalled frame (a GC, an atlas, a shader) must not read as a behaviour failure.
        private const float StateWait = 3f, TakeWait = 10f;

        private GameObject cast, holder;
        private CharacterPresentation body;
        private Animator animator;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            cast = new GameObject("Farewell stance UMA cast", typeof(GamesimUmaCast));
            yield return null;
            holder = new GameObject("Farewell stance alex-chen");
            holder.transform.position = new Vector3(2200f, 0f, 2200f);
            var person = ContentCatalog.Create(1).contestants.First(p => !p.isPlayer);
            person.id = "alex-chen";
            body = CharacterPresentation.Attach(holder, person, Color.white);
            body.SetReducedMotion(false);
            // Bounded by the clock alone: a batchmode frame can be under a millisecond, so a frame count
            // would give up long before a UMA body has assembled.
            float by = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < by)
            {
                if (body.TryReadFarewell(out var pose) && pose.Idle) break;
                yield return null;
            }
            Assert.That(body.TryReadFarewell(out var idle) && idle.Idle && !body.IsPrimitiveBody, Is.True,
                "An assembled UMA body standing in its Idle.");
            animator = body.VisualRoot.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (holder != null) Object.Destroy(holder);
            if (cast != null) Object.Destroy(cast);
            yield return null;
        }

        private static bool Holding(CharacterPresentation body) => (bool)typeof(CharacterPresentation)
            .GetProperty("HoldsFarewellStance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(body);

        private static float HoldWeight(CharacterPresentation body) => (float)typeof(CharacterPresentation)
            .GetProperty("FarewellStanceWeight", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(body);

        private bool Playing(string state) => animator.GetCurrentAnimatorStateInfo(0).IsName(state)
            || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state);

        [UnityTest]
        public IEnumerator TheStanceIsHeldThroughTheTakeAndLetGoOnceTheBodyIsBackInIdle()
        {
            body.React(CharacterPresentation.Reaction.Farewell);
            Assert.That(Holding(body), Is.True, "A farewell cued on a settled Idle takes hold of the stance.");
            bool entered = false, back = false;
            float by = Time.realtimeSinceStartup + TakeWait;
            while (Time.realtimeSinceStartup < by)
            {
                yield return null;
                Assert.That(body.TryReadFarewell(out var pose), Is.True);
                entered |= pose.InTake;
                Assert.That(Holding(body), Is.True, "Held from the cue until the body is back in Idle.");
                if (entered && pose.Idle) { back = true; break; }
            }
            Assert.That(entered && back, Is.True, "The actual take played and handed back to Idle.");
            // Still held on that first frame back: FarewellPair completes the transaction on it.
            by = Time.realtimeSinceStartup + TakeWait;
            while (Holding(body) && Time.realtimeSinceStartup < by)
            {
                Assert.That(body.TryReadFarewell(out var pose) && pose.Idle, Is.True, "Let go in Idle, not before.");
                yield return null;
            }
            Assert.That(Holding(body), Is.False, "The legs are the Idle's again once the body has settled.");
        }

        [UnityTest]
        public IEnumerator TheFirstFrameBackInIdleIsHeldInFullAtSlowFrames()
        {
            // FarewellPair completes the beat on the first frame both bodies are back in Idle, and reads
            // the feet on it. A slow frame outlasts the 0.1 s settle, and must not start the release.
            Assert.That(body.TryReadFarewell(out var baseline) && baseline.Idle, Is.True);
            float pinned = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .12f;
                body.React(CharacterPresentation.Reaction.Farewell);
                bool entered = false, back = false;
                for (int frame = 0; frame < 120 && !back; frame++)
                {
                    yield return null;
                    Assert.That(body.TryReadFarewell(out var pose), Is.True);
                    entered |= pose.InTake;
                    back = entered && pose.Idle;
                    if (!back) continue;
                    var before = new[] { baseline.LeftFoot, baseline.RightFoot, baseline.LeftToes, baseline.RightToes };
                    var now = new[] { pose.LeftFoot, pose.RightFoot, pose.LeftToes, pose.RightToes };
                    for (int i = 0; i < 4; i++)
                        Assert.That(Vector3.Distance(before[i], now[i]), Is.LessThanOrEqualTo(FarewellPair.FootTolerance),
                            "Foot landmark " + i + " on the first 0.12 s frame back in Idle.");
                    Assert.That(Holding(body), Is.True);
                }
                Assert.That(entered && back, Is.True, "The take played and handed back to Idle at slow frames.");
            }
            finally { Time.captureDeltaTime = pinned; }
        }

        [UnityTest]
        public IEnumerator AFarewellCuedAgainWhileSettlingHoldsItsOwnFirstFrameBackInFull()
        {
            // A second cue while the first one's stance is still held keeps that stance, and counts from
            // its own take: the settle the first take began is not carried into the second, whose first
            // frame back in Idle is the one FarewellPair completes the beat on and reads the feet of.
            Assert.That(body.TryReadFarewell(out var baseline) && baseline.Idle, Is.True);
            var before = new[] { baseline.LeftFoot, baseline.RightFoot, baseline.LeftToes, baseline.RightToes };
            float pinned = Time.captureDeltaTime;
            try
            {
                // 0.04 s frames: two of them settle 0.08 s of the 0.1 s, and one more begins the release.
                Time.captureDeltaTime = .04f;
                body.React(CharacterPresentation.Reaction.Farewell);
                bool entered = false, back = false;
                for (int frame = 0; frame < 300 && !back; frame++)
                {
                    yield return null;
                    Assert.That(body.TryReadFarewell(out var pose), Is.True);
                    entered |= pose.InTake;
                    back = entered && pose.Idle;
                }
                Assert.That(entered && back, Is.True, "The first take played and handed back to Idle.");
                for (int frame = 0; frame < 2; frame++)
                {
                    yield return null;
                    Assert.That(body.TryReadFarewell(out var settling) && settling.Idle && Holding(body), Is.True,
                        "Settling in Idle after the first take, still held.");
                }
                body.React(CharacterPresentation.Reaction.Farewell);
                Assert.That(Holding(body), Is.True, "The repeated cue keeps the stance it already has.");
                entered = back = false;
                for (int frame = 0; frame < 300 && !back; frame++)
                {
                    yield return null;
                    Assert.That(body.TryReadFarewell(out var pose), Is.True);
                    entered |= pose.InTake;
                    back = entered && pose.Idle;
                    if (!back) continue;
                    var now = new[] { pose.LeftFoot, pose.RightFoot, pose.LeftToes, pose.RightToes };
                    float worst = 0f;
                    for (int i = 0; i < 4; i++) worst = Mathf.Max(worst, Vector3.Distance(before[i], now[i]));
                    // The weight is the exact proof: how far a partial release moves a foot depends on how
                    // far the Idle has drifted from the cue's phase, which can be under the 1 mm tolerance.
                    Assert.That(Holding(body) && HoldWeight(body) >= 1f, Is.True,
                        "Held in full on the second take's first frame back in Idle (weight " + HoldWeight(body)
                        + ", worst foot landmark " + (worst * 1000f).ToString("F2") + " mm).");
                    for (int i = 0; i < 4; i++)
                        Assert.That(Vector3.Distance(before[i], now[i]), Is.LessThanOrEqualTo(FarewellPair.FootTolerance),
                            "Foot landmark " + i + " on the second take's first frame back in Idle.");
                }
                Assert.That(entered && back, Is.True, "The second take played and handed back to Idle.");
            }
            finally { Time.captureDeltaTime = pinned; }
        }

        [UnityTest]
        public IEnumerator AnotherBeatTakesTheLegsBackAtOnce()
        {
            body.React(CharacterPresentation.Reaction.Farewell);
            float by = Time.realtimeSinceStartup + StateWait;
            while ((!body.TryReadFarewell(out var pose) || !pose.InTake) && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(body.TryReadFarewell(out var entered) && entered.InTake && Holding(body), Is.True, "The take is playing, held.");
            Assert.That(body.Supports(CharacterPresentation.Reaction.Shocked), Is.True);
            body.React(CharacterPresentation.Reaction.Shocked);
            by = Time.realtimeSinceStartup + StateWait;
            while (!Playing("ReactShocked") && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(Playing("ReactShocked"), Is.True, "The newer beat actually took the body.");
            by = Time.realtimeSinceStartup + StateWait;
            while (Holding(body) && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(Holding(body), Is.False, "The shocked beat has the legs, not the farewell's stance.");
            Assert.That(Playing("ReactShocked"), Is.True, "Let go while the other beat plays, not after it.");
        }

        [UnityTest]
        public IEnumerator AFarewellCuedOutsideASettledIdleHoldsNothing()
        {
            body.React(CharacterPresentation.Reaction.Shocked);
            float by = Time.realtimeSinceStartup + StateWait;
            while (!Playing("ReactShocked") && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(Playing("ReactShocked"), Is.True);
            body.React(CharacterPresentation.Reaction.Farewell);
            Assert.That(Holding(body), Is.False, "Only a body standing in its Idle has a stance to keep.");
            yield return null;
            Assert.That(Holding(body), Is.False);
        }
    }
}
