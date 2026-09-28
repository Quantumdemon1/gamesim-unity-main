using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Where the opening meets the rest of the house: the click an introduction makes, a dance
    /// that must not outlive the show, and the tour offered to a season that has no opening.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// An introduction clicks once, as the reference build clicks on the choice. The commit
        /// clicked for every decision and the opening's own hook clicked again, so every
        /// introduction played the same click twice in one frame, at twice the loudness.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_AnIntroductionClicksOnce()
        {
            yield return ToTheIntroductions();
            var audio = Object.FindFirstObjectByType<HouseAudio>();
            Assert.That(audio, Is.Not.Null);
            int before = audio.CuesAsked;
            string guest = director.Opening.CurrentGuestId;
            var warm = SequenceButtons(director.Opening, "Warm").Single();
            warm.onClick.Invoke();
            yield return Frames(2);
            Assert.That(EpisodeEngine.HasIntroduced(director.Snapshot, guest), Is.True, "The introduction was committed.");
            Assert.That(audio.CuesAsked - before, Is.EqualTo(1), "One click for one introduction.");
            Assert.That(audio.LastCue, Is.EqualTo(HouseAudio.Cue.Button), "and it is the button's click.");
            director.Opening.SkipIntroductions();
            yield return Frames(3);
        }

#if GAMESIM_UMA
        // UMA only: an introduction's dance is a mocap take on the humanoid controller, which only a UMA body wears.
        /// <summary>
        /// A houseguest who dances because an introduction landed stops when the opening ends, even
        /// when the introductions are skipped in the middle of the dance: the dance's own clock ran on
        /// past the show and into free time.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_ADanceForAnIntroductionEndsWithTheOpening()
        {
            yield return ToTheIntroductions();
            string guest = director.Opening.CurrentGuestId;
            var visual = SceneComponents<House.HouseNpc>().Single(npc => npc.Id == guest).GetComponent<CharacterPresentation>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.CanAct(CharacterPresentation.BodyActivity.Dancing), Is.True,
                "The fixture's bodies can dance, so a landed introduction is a dance rather than a cheer.");
            // How the house reacts is the director's; a landed introduction is played directly, so
            // the test does not depend on which approach suits whom.
            typeof(EpisodeDirector).GetMethod("ReactToIntroduction", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(director, new object[] { guest, WebIntroductions.Outcome.Match });
            yield return null;
            Assert.That(visual.Activity, Is.EqualTo(CharacterPresentation.BodyActivity.Dancing), "The houseguest dances.");

            director.Opening.SkipIntroductions();
            yield return Frames(3);
            Assert.That(director.Opening.IsPlaying, Is.False);
            Assert.That(visual.Activity, Is.Not.EqualTo(CharacterPresentation.BodyActivity.Dancing),
                "The dance ended with the opening, not a second and a half into free time.");
        }
#endif

        /// <summary>
        /// The disc under the player goes down for the opening with the name plates and comes back
        /// with them: it stood in the doorway under the shut front door, and at the player's feet
        /// through every reveal.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_ThePlayersMarkerStepsAsideForTheOpening()
        {
            var disc = player.GetComponentsInChildren<Transform>(true)
                .Where(part => part.name == EpisodeDirector.PlayerMarkerName)
                .Select(part => part.GetComponent<Renderer>()).SingleOrDefault(renderer => renderer != null);
            Assert.That(disc, Is.Not.Null, "The scene gives the player a selection disc.");
            Assert.That(disc.enabled, Is.True, "It is drawn in the house.");
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(director.Opening.IsPlaying, Is.True);
            Assert.That(disc.enabled, Is.False, "It is not drawn while the opening plays.");
            director.Opening.Skip();
            yield return Frames(3);
            Assert.That(director.Opening.IsPlaying, Is.False);
            Assert.That(disc.enabled, Is.True, "It is back when the opening ends.");
        }

        /// <summary>
        /// The tour offered outside the opening - to a season imported part-way through - closes on
        /// Escape, as its own card says. It dims the house and takes the pointer, and Escape went on
        /// to the panels underneath instead, leaving the player under a dim with no key out.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_EscapeClosesTheTourOfferedOutsideTheOpening()
        {
            var tour = DirectorTour();
            Assert.That(director.Opening.IsPlaying, Is.False, "No opening: this is the tour a season gets on its own.");
            tour.Show(TourChrome);
            yield return Frames(2);
            Assert.That(tour.IsShowing, Is.True);

            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(tour.IsShowing, Is.False, "Escape closed the tour.");
            Assert.That(director.IsPhasePanelOpen, Is.False, "and opened nothing underneath it.");
        }
    }
}
