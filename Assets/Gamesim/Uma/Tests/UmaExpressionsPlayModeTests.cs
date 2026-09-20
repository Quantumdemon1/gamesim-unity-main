using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The UMA face: mood and stress reaching real brows and a real mouth, a lip flap while the
    /// houseguest has the floor, and none of it moving when motion is reduced.
    ///
    /// <para>The mapping is asserted on its own first, because it is a pure function of two words
    /// and an assertion about it should not have to wait nine hundred frames for UMA to atlas a
    /// character. The wiring is asserted afterwards on a real body, because a correct mapping that
    /// never reaches an expression player is a face nobody sees.</para>
    /// </summary>
    public sealed class UmaExpressionsPlayModeTests
    {
        private const int BuildTimeoutFrames = 900;
        private const int SettleFrames = 90;

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

        // ------------------------------------------------------------------ the mapping alone

        [Test]
        public void Neutral_IsARestingFace()
        {
            Assert.That(UmaExpressions.For("Neutral", "Normal").Magnitude, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Happiness_TurnsTheMouthUp_AndAngerTurnsItDown()
        {
            Assert.That(UmaExpressions.For("Happy", "Normal").Smile, Is.GreaterThan(0.5f));
            Assert.That(UmaExpressions.For("Content", "Normal").Smile, Is.GreaterThan(0f));
            Assert.That(UmaExpressions.For("Angry", "Normal").Smile, Is.LessThan(0f));
            Assert.That(UmaExpressions.For("Upset", "Normal").Smile, Is.LessThan(0f));
        }

        /// <summary>
        /// The one difference that separates hurt from furious on a face this size: which way the
        /// brows go. Everything else about the two moods is close enough to read as the same.
        /// </summary>
        [Test]
        public void AngerLowersTheBrows_AndBeingUpsetRaisesThem()
        {
            Assert.That(UmaExpressions.For("Angry", "Normal").BrowUp, Is.LessThan(0f));
            Assert.That(UmaExpressions.For("Upset", "Normal").BrowUp, Is.GreaterThan(0f));
        }

        /// <summary>
        /// Mood and stress are separate fields on a houseguest and separate parts of the face here,
        /// so someone can be happy and coming apart at the same time — which the simulation allows
        /// and a face that could only be one of them would deny.
        /// </summary>
        [Test]
        public void Stress_ColoursAMoodRatherThanReplacingIt()
        {
            var calm = UmaExpressions.For("Happy", "Normal");
            var strained = UmaExpressions.For("Happy", "Overwhelmed");
            Assert.That(strained.Smile, Is.EqualTo(calm.Smile).Within(0.0001f), "Still happy.");
            Assert.That(strained.EyeOpen, Is.GreaterThan(calm.EyeOpen), "And still overwhelmed.");

            Assert.That(UmaExpressions.For("Neutral", "Tense").BrowsIn, Is.GreaterThan(0f));
            Assert.That(UmaExpressions.For("Neutral", "Stressed").BrowsIn,
                Is.GreaterThan(UmaExpressions.For("Neutral", "Tense").BrowsIn));
        }

        [Test]
        public void EveryChannel_StaysInUmasOwnRange()
        {
            foreach (var mood in new[] { "Angry", "Upset", "Neutral", "Content", "Happy" })
            foreach (var stress in new[] { "Relaxed", "Normal", "Tense", "Stressed", "Overwhelmed" })
            {
                var pose = UmaExpressions.For(mood, stress);
                Assert.That(pose.Smile, Is.InRange(-1f, 1f), mood + "/" + stress);
                Assert.That(pose.BrowUp, Is.InRange(-1f, 1f), mood + "/" + stress);
                Assert.That(pose.BrowsIn, Is.InRange(0f, 1f), mood + "/" + stress);
                Assert.That(pose.EyeOpen, Is.InRange(-1f, 1f), mood + "/" + stress);
                Assert.That(pose.Sneer, Is.InRange(0f, 1f), mood + "/" + stress);
                Assert.That(pose.JawOpen, Is.InRange(-1f, 1f), mood + "/" + stress);
            }
        }

        [Test]
        public void AWordTheSimulationNeverSays_IsARestingFaceRatherThanAnException()
        {
            Assert.That(UmaExpressions.For(null, null).Magnitude, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(UmaExpressions.For("Delighted", "Fine").Magnitude, Is.EqualTo(0f).Within(0.0001f));
        }

        // ------------------------------------------------------------------ on a built body

        [UnityTest]
        public IEnumerator AMoodChange_MovesAnExpressionValueOnTheBody()
        {
            yield return BuildHouseguest();
            var face = Expressions();
            Assert.That(face, Is.Not.Null, "A UMA body should carry the expression component.");
            Assert.That(face.Player, Is.Not.Null, "UMA should have given the body an expression player.");

            var presentation = actor.GetComponent<CharacterPresentation>();
            presentation.SetMood("Neutral", "Normal");
            for (int i = 0; i < SettleFrames; i++) yield return null;
            float resting = face.Player.leftMouthSmile_Frown;

            presentation.SetMood("Happy", "Normal");
            for (int i = 0; i < SettleFrames; i++) yield return null;

            Assert.That(face.Player.leftMouthSmile_Frown, Is.GreaterThan(resting + 0.25f),
                "Turning happy should have turned the mouth up; it is still at " + face.Player.leftMouthSmile_Frown + ".");
            Assert.That(face.Player.rightMouthSmile_Frown,
                Is.EqualTo(face.Player.leftMouthSmile_Frown).Within(0.0001f), "Both corners, or neither.");

            presentation.SetMood("Angry", "Stressed");
            for (int i = 0; i < SettleFrames; i++) yield return null;
            Assert.That(face.Player.leftBrowUp_Down, Is.LessThan(0f), "An angry brow comes down.");
            Assert.That(face.Player.browsIn, Is.GreaterThan(0.5f), "A stressed brow comes together.");
        }

        /// <summary>
        /// The lip flap. It rides on the jaw rather than the mouth corners so it survives whatever
        /// mood the houseguest is in — people talk while angry.
        /// </summary>
        [UnityTest]
        public IEnumerator HavingTheFloor_OpensAndClosesTheJaw()
        {
            yield return BuildHouseguest();
            var face = Expressions();
            var presentation = actor.GetComponent<CharacterPresentation>();

            presentation.SetTalking(false);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Mathf.Abs(face.Player.jawOpen_Close), Is.LessThan(0.02f), "A silent houseguest's jaw is shut.");

            presentation.SetTalking(true);
            presentation.SetSpeaking(true);
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                low = Mathf.Min(low, face.Player.jawOpen_Close);
                high = Mathf.Max(high, face.Player.jawOpen_Close);
            }
            Assert.That(high, Is.GreaterThan(0.1f), "The jaw never opened.");
            Assert.That(high - low, Is.GreaterThan(0.05f), "The jaw opened and stayed open; that is a gape, not speech.");

            // Listening is not speaking: the body without the floor keeps its mouth shut.
            presentation.SetSpeaking(false);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(Mathf.Abs(face.Player.jawOpen_Close), Is.LessThan(0.02f));
        }

        /// <summary>
        /// Reduced motion holds the face still — at rest, not at the current mood. A face is the
        /// smallest motion a body makes and the hardest to look away from.
        /// </summary>
        [UnityTest]
        public IEnumerator ReducedMotion_HoldsTheFaceStill()
        {
            yield return BuildHouseguest();
            var face = Expressions();
            var presentation = actor.GetComponent<CharacterPresentation>();

            presentation.SetReducedMotion(true);
            presentation.SetTalking(true);
            presentation.SetSpeaking(true);
            presentation.SetMood("Angry", "Overwhelmed");
            for (int i = 0; i < SettleFrames; i++) yield return null;

            Assert.That(face.Current.Magnitude, Is.EqualTo(0f).Within(0.0001f),
                "Reduced motion should have left the face at rest.");
            Assert.That(Mathf.Abs(face.Player.leftMouthSmile_Frown), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(face.Player.leftBrowUp_Down), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(face.Player.jawOpen_Close), Is.LessThan(0.0001f), "And no lip flap either.");

            // And it comes back when the setting does.
            presentation.SetReducedMotion(false);
            for (int i = 0; i < SettleFrames; i++) yield return null;
            Assert.That(face.Player.leftBrowUp_Down, Is.LessThan(-0.1f));
        }

        private UmaExpressions Expressions() => actor.GetComponentInChildren<UmaExpressions>(true);

        private IEnumerator BuildHouseguest()
        {
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Not.Null, "The scene should be on UMA bodies.");
            CharacterPresentation.Attach(actor, Houseguest(), Color.white);

            SkinnedMeshRenderer mesh = null;
            for (int frame = 0; frame < BuildTimeoutFrames && mesh == null; frame++)
            {
                yield return null;
                mesh = UmaMesh();
            }
            Assert.That(mesh, Is.Not.Null, "UMA never produced a body to put a face on.");
            // The expression player is initialised on CharacterUpdated, which the proportion pass
            // raises a second time; give it both.
            for (int i = 0; i < 120; i++) yield return null;
        }

        private SkinnedMeshRenderer UmaMesh()
        {
            var body = actor.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "UMA Body");
            return body == null ? null : body.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        private static ContestantState Houseguest() =>
            ContentCatalog.Create(1).contestants.First(contestant => !contestant.isPlayer);
    }
}
