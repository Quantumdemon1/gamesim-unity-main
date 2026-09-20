using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The opening sequence, driven directly.
    ///
    /// <para>The director will not start it in batchmode — a sequence that waits is the only thing
    /// that could hold up an automated season — so these drive <see cref="OpeningSequence.Play"/>
    /// the way the tour's own tests drive <c>HouseTutorial.Show</c>.</para>
    ///
    /// <para>What is asserted is sequence <i>state</i> and never pixels. Batchmode renders without
    /// presenting, so a screenshot of any of this is a black rectangle; what actually matters is
    /// which beats were recorded, that the camera was given back, and that skipping is honoured.
    /// </para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private OpeningSequence Opening() => director.GetComponentInChildren<OpeningSequence>(true);

        [UnityTest]
        public IEnumerator Tutorial_IsolatedSessionDoesNotReadOrWritePlayerCompletion()
        {
            var tutorial = director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseTutorial>(true)).Single();
            bool hadKey = PlayerPrefs.HasKey(HouseTutorial.SeenKey);
            int previous = PlayerPrefs.GetInt(HouseTutorial.SeenKey, -1);
            Assert.That(tutorial.RememberCompletion, Is.False);
            Assert.That(tutorial.HasSeen, Is.False, "An isolated first session has its own completion state.");
            tutorial.Show(_ => null);
            yield return null;
            Assert.That(tutorial.IsShowing, Is.True);
            tutorial.Skip();
            Assert.That(tutorial.IsShowing, Is.False);
            Assert.That(tutorial.HasSeen, Is.True, "Skipping is still remembered for this isolated session.");
            Assert.That(PlayerPrefs.HasKey(HouseTutorial.SeenKey), Is.EqualTo(hadKey));
            Assert.That(PlayerPrefs.GetInt(HouseTutorial.SeenKey, -1), Is.EqualTo(previous));
        }

        /// <summary>
        /// The camera rig, which the director holds a reference to but does not own — it is a scene
        /// object beside the house, so it is not in the director's subtree.
        /// </summary>
        private House.HouseCameraRig Rig() =>
            director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<House.HouseCameraRig>(true))
                .FirstOrDefault();

        private static OpeningSequence.Settings Plan(List<string> recorded, bool reducedMotion = false) =>
            new OpeningSequence.Settings
            {
                MarkBeat = recorded.Add,
                // No tour: in batchmode there is nobody to show it to, and a beat that waits for a
                // click nobody can make would hang the run.
                RunTutorial = done => done(),
                ReducedMotion = reducedMotion,
            };

        private static IEnumerator Until(OpeningSequence sequence)
        {
            for (int frame = 0; frame < 600 && sequence.IsPlaying; frame++) yield return null;
            Assert.That(sequence.IsPlaying, Is.False, "The sequence should have finished by now.");
        }

        [UnityTest]
        public IEnumerator Opening_PlaysEveryBeatOnceAndRecordsThemInOrder()
        {
            var sequence = Opening();
            Assert.That(sequence, Is.Not.Null, "The director must attach an opening sequence.");

            var recorded = new List<string>();
            sequence.Play(new string[0], Plan(recorded));
            yield return Until(sequence);

            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded);
        }

        /// <summary>The ordinary case for every load after the first: there is nothing left to play.</summary>
        [UnityTest]
        public IEnumerator Opening_ASeasonThatHasSeenItAllPlaysNothing()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            bool finished = false;

            var plan = Plan(recorded);
            plan.Finished = () => finished = true;
            sequence.Play(OpeningBeat.InOrder, plan);
            yield return null;

            Assert.That(sequence.IsPlaying, Is.False);
            Assert.That(recorded, Is.Empty);
            Assert.That(finished, Is.True, "The caller still has to be told it is over.");
        }

        [UnityTest]
        public IEnumerator Opening_AHalfSeenSequenceResumesWhereItStopped()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            sequence.Play(new[] { OpeningBeat.Intro, OpeningBeat.HouseEntry }, Plan(recorded));
            yield return Until(sequence);

            CollectionAssert.AreEqual(
                new[] { OpeningBeat.WalkIn, OpeningBeat.Tutorial, OpeningBeat.MeetAndGreet },
                recorded);
        }

        /// <summary>
        /// Skipping records the beats it skipped. Leaving them pending would offer the intro again
        /// on the next load to somebody who has already said no to it.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_SkippingRecordsEverythingItSkipped()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            sequence.Play(new string[0], Plan(recorded));
            yield return null;

            sequence.Skip();
            yield return null;

            Assert.That(sequence.IsPlaying, Is.False);
            CollectionAssert.AreEquivalent(OpeningBeat.InOrder, recorded);
        }

        [UnityTest]
        public IEnumerator Opening_ReducedMotionStillPlaysEveryBeat()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            sequence.Play(new string[0], Plan(recorded, reducedMotion: true));
            yield return Until(sequence);

            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded,
                "The preference removes movement, not information.");
        }

        /// <summary>
        /// The camera is taken for the sequence and given back afterwards. A player left unable to
        /// move the camera after the intro would have no way to work out why.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheCameraIsGivenBackWhenItIsOver()
        {
            var rig = Rig();
            Assert.That(rig, Is.Not.Null, "The scene should have a camera rig.");
            rig.ControlsEnabled = true;

            var sequence = Opening();
            var recorded = new List<string>();
            var plan = Plan(recorded);
            plan.Rig = rig;
            plan.RoomStops = new List<KeyValuePair<string, Vector3>>
            {
                new KeyValuePair<string, Vector3>("Kitchen", new Vector3(2f, 0f, 1f)),
                new KeyValuePair<string, Vector3>("Yard", new Vector3(-3f, 0f, 4f)),
            };

            sequence.Play(new string[0], plan);
            yield return null;
            Assert.That(rig.ControlsEnabled, Is.False, "The sequence drives the camera.");

            yield return Until(sequence);
            Assert.That(rig.ControlsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator Opening_SkippingAlsoGivesTheCameraBack()
        {
            var rig = Rig();
            rig.ControlsEnabled = true;

            var sequence = Opening();
            var plan = Plan(new List<string>());
            plan.Rig = rig;
            sequence.Play(new string[0], plan);
            yield return null;

            sequence.Skip();
            yield return null;
            Assert.That(rig.ControlsEnabled, Is.True);
        }

        /// <summary>
        /// The skip control is a real button with the caption the tour and the tests look for, on
        /// every beat rather than only the first — somebody who decides to skip during the walk-in
        /// should not have to wait for a beat that offers it.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheSkipControlIsReachableWhileItIsPlaying()
        {
            var sequence = Opening();
            sequence.Play(new string[0], Plan(new List<string>()));
            yield return null;

            var skip = sequence.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Where(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMPro.TMP_Text>(true)
                        .Any(label => label.text == OpeningSequence.SkipCaption))
                .ToArray();

            Assert.That(skip, Has.Length.EqualTo(1));
            skip[0].onClick.Invoke();
            yield return null;
            Assert.That(sequence.IsPlaying, Is.False);
        }
    }
}
