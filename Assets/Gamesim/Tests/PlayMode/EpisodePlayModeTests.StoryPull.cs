using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Pull and the scene card (plan §5.1): a story moment offered out in the house without a
    /// panel, turned down without losing it, and answered where the player stands.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A season where a houseguest has just won Head of Household and wants a word before the
        /// nominations: a legal history played with the story system off, switched on at that point
        /// so nothing else is open, and the beat started by the engine's own seam.
        /// </summary>
        private IEnumerator InstallStoryPullFixture()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 60 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 80; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Nomination && current.nominees.Count == 0
                        && current.hohId != current.playerId && current.Find(current.playerId).status == ContestantStatus.Active)
                    {
                        EpisodeEngine.EnableStory(current, current.week);
                        if (EpisodeEngine.StartStory(current, "after-the-comp", StoryAnchors.HohCrowned)
                            && EpisodeEngine.OpenStoryBeats(current).Count == 1) fixture = current;
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    Assert.That(engine.Apply(NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture with a Head of Household's word found.");
            // The house's own clock must neither write over the fixture nor commit under the test: the
            // outgoing director can autosave an NPC tick over a file just written, and a tick mid-test
            // rebuilds the chrome the keyboard is walking. Neither is what these tests are about.
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator StoryPull_AHouseguestsWordIsOfferedAndAnsweredWhereYouStand()
        {
            yield return InstallStoryPullFixture();
            Assert.That(director.PullOffered, Is.EqualTo(EpisodeHud.HearThemOutCaption), "An approach is offered as a Pull.");
            var card = ActiveRect(EpisodeHud.PullCardName);
            Assert.That(card, Is.Not.Null, "The Pull is on screen.");
            Assert.That(ActiveRect(EpisodeHud.HouseVibeCardName), Is.Null, "It takes the week card's place.");
            var beat = EpisodeEngine.OpenStoryBeats(director.Snapshot).Single();
            var hoh = director.Snapshot.Find(director.Snapshot.hohId);
            Assert.That(card.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text.Contains(hoh.name)), Is.False,
                "The Pull names nobody until you are there.");

            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.HearThemOutCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsSceneCardOpen, Is.True, "Hearing them out opens the card where the player stands.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "Opening the card writes nothing.");
            Assert.That(ActiveRect(EpisodeHud.StoryChoicesName), Is.Not.Null, "The card carries the beat's choices.");
            Assert.That(ActiveRect(EpisodeHud.PullCardName), Is.Null, "The Pull steps aside for the card.");

            var option = beat.choices.First(c => !c.locked && !c.pickPerson && !c.conduct && !c.costsAction && c.optionId != beat.lapseOptionId);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(option.label)).onClick.Invoke();
            yield return null;
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot), Is.Empty, "The answer committed.");
            Assert.That(director.IsSceneCardOpen, Is.True, "The card stays to say what came of it.");
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsPanelOpen, Is.False, "And hands the player back to the house.");
            Assert.That(director.PullOffered, Is.Null);
            Assert.That(ActiveRect(EpisodeHud.HouseVibeCardName), Is.Not.Null, "The week card comes back.");
        }

        /// <summary>
        /// Every new control is on the ring (plan §5.6): Tab reaches the Pull, and Enter answers it.
        /// The press goes through <see cref="KeyboardSubmit"/>, as every keyboard test's does: a body
        /// finishing its assembly rebuilds the HUD, and a synthetic Enter sent to the control it
        /// replaced is delivered to nothing. The walk finds the control on screen after every press
        /// for the same reason.
        /// </summary>
        [UnityTest]
        public IEnumerator StoryPull_TheKeyboardReachesItAndEnterAnswersIt()
        {
            yield return InstallStoryPullFixture();
            var answer = FindButton(EpisodeHud.HearThemOutCaption);
            for (int presses = 0; presses < 60 && EventSystem.current.currentSelectedGameObject != answer.gameObject; presses++)
            {
                yield return PressKey(Key.Tab);
                answer = FindButton(EpisodeHud.HearThemOutCaption);
            }
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(answer.gameObject), "Tab reaches the Pull.");
            var before = director.Snapshot;
            yield return KeyboardSubmit(EpisodeHud.HearThemOutCaption);
            Assert.That(director.IsSceneCardOpen, Is.True, "Enter answers it: " + lastSubmit);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "Opening the card writes nothing.");
        }

        [UnityTest]
        public IEnumerator StoryPull_NotNowTurnsItDownWithoutLosingIt()
        {
            yield return InstallStoryPullFixture();
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.NotNowCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.PullOffered, Is.Null, "Turned down, the Pull goes.");
            Assert.That(ActiveRect(EpisodeHud.PullCardName), Is.Null);
            Assert.That(ActiveRect(EpisodeHud.HouseVibeCardName), Is.Not.Null);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "Turning it down writes nothing.");
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot), Has.Count.EqualTo(1), "The moment still waits at the episode screen.");
        }
    }
}
