using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The opening as the director plays it: it owns the house while it is on, the keys it answers
    /// to, the music under it, and the introductions it commits through the engine.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static IEnumerator RealSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private Button OpeningButton(string caption) =>
            director.Opening.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption));

        /// <summary>From the start of the opening straight to the introductions, the way the skip control does it.</summary>
        private IEnumerator ToTheIntroductions()
        {
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            Assert.That(director.Opening.IsPlaying, Is.True, "The opening plays on the first night.");
            yield return null;
            director.SkipOpening();
            float until = Time.realtimeSinceStartup + 5f;
            while (!director.Opening.IsMeeting && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(director.Opening.IsMeeting, Is.True, "Skipping the show stops at the introductions.");
            yield return Frames(2);
        }

        /// <summary>
        /// The opening is a panel while it plays: the player cannot walk and the camera is the
        /// show's, through every beat it records - a commit re-projects the house, and it used to
        /// hand both back after the first one.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_TheOpeningIsAPanelWhileItPlays()
        {
            var opening = director.Opening;
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False, "The player does not walk under the titles.");
            Assert.That(cameraRig.ControlsEnabled, Is.False, "The camera is the show's.");

            // Through the title, every reveal, the group card and the fade: the intro is recorded.
            for (int i = 0; i < 40 && opening.CurrentBeat == OpeningBeat.Intro; i++) { opening.Advance(); yield return Frames(2); }
            Assert.That(director.Snapshot.openingBeatsSeen, Does.Contain(OpeningBeat.Intro), "The intro was recorded.");
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False, "Still not walking after a beat's commit.");
            Assert.That(cameraRig.ControlsEnabled, Is.False, "Still the show's camera after a beat's commit.");

            opening.Skip();
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True, "The player walks again once it is over.");
            Assert.That(cameraRig.ControlsEnabled, Is.True);
        }

        /// <summary>
        /// The house stands still while the opening plays and carries on once it is over. Watching
        /// the titles used to let the houseguests tick underneath them, so a player who watched
        /// met a different house from one who skipped.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_TheHouseStandsStillWhileTheOpeningPlays()
        {
            float until = Time.realtimeSinceStartup + 8f;
            while (!director.NpcAutonomyReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(director.NpcAutonomyReady, Is.True, "The house is running before the opening.");

            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            long tick = director.Snapshot.npcSocial.clockTick;
            long revision = director.Snapshot.revision;
            yield return RealSeconds(1.6f);
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(tick), "No houseguest ticks while the opening plays.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Nothing commits while a card holds.");

            director.Opening.Skip();
            yield return Frames(2);
            long after = director.Snapshot.npcSocial.clockTick;
            until = Time.realtimeSinceStartup + 6f;
            while (director.Snapshot.npcSocial.clockTick == after && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.GreaterThan(after), "The house carries on once it is over.");
        }

        /// <summary>
        /// Escape skips the show but keeps the introductions, and during them only puts the
        /// keyboard on "Skip Introductions": a key that forfeits a choice in one press is too easy
        /// to hit by habit. It never reaches the panels underneath.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_EscapeSkipsTheShowButKeepsTheIntroductions()
        {
            var opening = director.Opening;
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            float until = Time.realtimeSinceStartup + 5f;
            while (!opening.IsMeeting && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(opening.IsMeeting, Is.True, "Escape skipped the show and stopped at the introductions.");
            CollectionAssert.AreEqual(OpeningBeat.InOrder.Take(4), director.Snapshot.openingBeatsSeen);
            Assert.That(director.IsPhasePanelOpen, Is.False, "Nothing underneath opened.");

            yield return Frames(2);
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(opening.IsPlaying, Is.True, "Escape during the introductions does not end them.");
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.name, Is.EqualTo(OpeningSequence.SkipIntroductionsCaption), "It puts the keyboard on Skip Introductions.");
            opening.SkipIntroductions();
            yield return Frames(3);
            Assert.That(opening.IsPlaying, Is.False);
        }

        /// <summary>Space moves the show on, from the title to the first houseguest's reveal.</summary>
        [UnityTest]
        public IEnumerator Director_SpaceAdvances()
        {
            var opening = director.Opening;
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(3);
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(opening.CurrentGuestId, Is.Null, "The title is up.");
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(3);
            Assert.That(opening.CurrentGuestId, Is.EqualTo(director.Snapshot.playerId), "Space moved the title on to the player's reveal.");
            opening.Skip();
            yield return null;
        }

        /// <summary>The theme under the intro and the season's bed from the house entry on, as the reference build plays them.</summary>
        [UnityTest]
        public IEnumerator Director_TheThemePlaysUnderTheIntroOnly()
        {
            var audio = Object.FindFirstObjectByType<HouseAudio>();
            Assert.That(audio, Is.Not.Null);
            var opening = director.Opening;
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Theme), "The theme from the first frame of the titles.");
            for (int i = 0; i < 40 && opening.CurrentBeat == OpeningBeat.Intro; i++) { opening.Advance(); yield return Frames(2); }
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry));
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The season's bed from the house entry on.");
            opening.Skip();
            yield return null;
        }

        /// <summary>The HUD steps aside for the show and the introductions, and comes back for the tour and afterwards.</summary>
        [UnityTest]
        public IEnumerator Director_TheHudStepsAsideForTheOpening()
        {
            var hud = director.GetComponent<EpisodeHud>();
            Assert.That(hud, Is.Not.Null);
            var opening = director.Opening;
            var plan = director.OpeningPlan(stage: false, holdUntilAdvanced: true, verification: true);
            bool sawTour = false, tourVisible = false;
            plan.RunTutorial = done => { sawTour = true; tourVisible = !hud.IsCinematic; done(); };
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(hud.IsCinematic, Is.True, "The HUD is out of the titles.");
            opening.Skip();
            yield return Frames(2);
            Assert.That(hud.IsCinematic, Is.False, "and back once the opening is over.");

            // The tour beat, on its own: the HUD shows for it, because the tour points at it.
            opening.Play(OpeningBeat.InOrder.Take(3), plan);
            yield return Frames(4);
            Assert.That(sawTour, Is.True);
            Assert.That(tourVisible, Is.True, "The HUD is up for the tour.");
            opening.Skip();
            yield return null;
        }

        /// <summary>
        /// An introduction goes through the engine as its own command: the house moves by the
        /// reference build's bonus, the week's interactions and the season's generator are
        /// untouched, and the introductions are not over until the player says so.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_IntroductionsGoThroughTheEngine()
        {
            var before = director.Snapshot;
            var guest = before.Active.First(person => !person.isPlayer);
            var approach = WebIntroductions.Find(WebIntroductions.Calculated);
            int bonus = WebIntroductions.Bonus(WebIntroductions.Judge(approach, guest.traits));
            int spent = EpisodeEngine.SocialActionsSpent(before);
            uint seed = before.randomState;

            yield return ToTheIntroductions();
            Assert.That(director.Opening.CurrentGuestId, Is.EqualTo(guest.id), "The first card is the first houseguest.");
            var calculated = OpeningButton("Calculated");
            Assert.That(calculated, Is.Not.Null, "The card offers Calculated.");
            calculated.onClick.Invoke();
            yield return Frames(2);

            var after = director.Snapshot;
            Assert.That(after.Score(after.playerId, guest.id) - before.Score(before.playerId, guest.id), Is.EqualTo(bonus).Within(1e-9),
                "The player's side moves by the reference build's bonus.");
            Assert.That(EpisodeEngine.HasIntroduced(after, guest.id), Is.True);
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent), "An introduction costs no interaction.");
            Assert.That(after.randomState, Is.EqualTo(seed), "and draws nothing from the season's generator.");
            Assert.That(after.openingBeatsSeen, Does.Not.Contain(OpeningBeat.MeetAndGreet), "The introductions are still open.");
            director.Opening.SkipIntroductions();
            yield return Frames(3);
        }

        /// <summary>Recording a beat is bookkeeping, and makes no sound: it used to click under every card of the titles.</summary>
        [UnityTest]
        public IEnumerator Director_MarkingABeatMakesNoSound()
        {
            var audio = Object.FindFirstObjectByType<HouseAudio>();
            var opening = director.Opening;
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            var cue = audio.LastCue;
            for (int i = 0; i < 40 && opening.CurrentBeat == OpeningBeat.Intro; i++) { opening.Advance(); yield return Frames(2); }
            Assert.That(director.Snapshot.openingBeatsSeen, Does.Contain(OpeningBeat.Intro));
            Assert.That(audio.LastCue, Is.EqualTo(cue), "No cue for the mark.");
            opening.Skip();
            yield return null;
        }

        /// <summary>
        /// No opening outside the first night: a season arriving with none of it seen - an import,
        /// a save from before it existed - is past the premiere, and offering introductions to a
        /// house that has already competed would be wrong.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_NoOpeningOutsideTheFirstNight()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True, "The episode screen opens.");
            yield return null;
            director.ContinueEpisode();
            yield return Frames(2);
            director.ClosePanels();
            yield return null;
            Assert.That(EpisodeEngine.IsFirstNight(director.Snapshot), Is.False, "The first competition has begun.");
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return null;
            Assert.That(director.Opening.IsPlaying, Is.False);
            Assert.That(director.Snapshot.openingBeatsSeen, Is.Empty, "and nothing was recorded.");
        }

        /// <summary>
        /// When it ends, the camera is the player's again: no shot held, following the player, and
        /// answering to their controls.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_FinishingGivesBackTheCameraAndTheKeys()
        {
            yield return ToTheIntroductions();
            director.Opening.SkipIntroductions();
            yield return Frames(3);
            Assert.That(director.Opening.IsPlaying, Is.False);
            Assert.That(cameraRig.HasShot, Is.False, "No shot is left holding the camera.");
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(player.transform), "The camera is on the player.");
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            Assert.That(player.InputEnabled, Is.True);
        }

        /// <summary>The tour takes the keyboard on "Next": with "Skip Tutorial" first, Enter left the tour.</summary>
        [UnityTest]
        public IEnumerator Tutorial_KeyboardFocusLandsOnNext()
        {
            var tutorial = director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseTutorial>(true)).Single();
            tutorial.Show(_ => null);
            yield return Frames(3);
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.name, Is.EqualTo("Next"));
            Assert.That(selected.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Next"), "The control that says Next, not the skip.");
            tutorial.Skip();
            yield return null;
        }
    }
}
