using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
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
    /// The ceremonies keep their secret until they tell it: the chrome steps aside while a reveal
    /// counts, the evicted houseguest is still in the room for their own eviction, a card's keys are
    /// its own, the reveal plays at the chosen pace, the veto card tells the meeting's story, and
    /// reduced motion cuts to the room instead of skipping it.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Commands until a live eviction is on screen; fails if none comes.</summary>
        private IEnumerator PlayUntilTheVoteReveal(VoteReveal reveal)
        {
            for (int guard = 0; guard < 400 && !reveal.IsPlaying; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                director.Submit(NextCommand(before));
                yield return null;
            }
            Assert.That(reveal.IsPlaying, Is.True, "The episode never reached a live eviction.");
        }

        private EpisodeHud Hud => director.GetComponentInChildren<EpisodeHud>();

        /// <summary>
        /// Gets past a key ceremony or a vote reveal the way a player who skips it does, for walks
        /// that press the house's controls. The chrome steps aside while either plays, so a player
        /// has to skip the card or sit it out before those controls are there to press; a walk that
        /// pressed them under a reveal was pressing something nobody could see. This lands the
        /// skip's end state - the keys and clicks that skip are the reveal tests' subject - and
        /// waits for the chrome to come back, rebuilt, before anything reaches for it. The
        /// endgame's cards are skipped with them: the chrome stands aside for those as well
        /// (MOCKUP-PASS-PLAN M2), so a walk pressing the house's controls under one would be
        /// pressing controls nobody can see. So is the veto meeting's card on the HUD frame, which
        /// holds the chrome as they do (UI-UX-PASS-PLAN V0); never the meeting on a set's screen,
        /// whose stage the staged walks skip as a stage.
        /// </summary>
        private IEnumerator SkipReveals()
        {
            var playing = SceneComponents<KeyCeremony>().Where(card => card.IsPlaying).ToList();
            var reveals = SceneComponents<VoteReveal>().Where(card => card.IsPlaying).ToList();
            var jury = SceneComponents<JuryReveal>().Where(card => card.IsPlaying).ToList();
            var endgame = SceneComponents<CeremonyTakeover>().Where(card => EpisodeDirector.IsEndgameCard(card.PlayingKind)
                || card.PlayingKind == CeremonySting.VetoKind && card.Surface == null).ToList();
            if (playing.Count == 0 && reveals.Count == 0 && jury.Count == 0 && endgame.Count == 0) yield break;
            foreach (var card in playing) card.Cancel();
            foreach (var card in reveals) card.Cancel();
            foreach (var card in jury) card.Cancel();
            foreach (var card in endgame) card.Cancel();
            // One frame for the director to hand the chrome back and redraw it, one for the copy it
            // replaced to be destroyed.
            yield return Frames(2);
            // A staged eviction keeps the chrome aside past its card, through the goodbye and the
            // walk out to the door shut behind them (MOCKUP-PASS-PLAN M19); its own tests follow
            // the exit, and the chrome comes back at its end.
            if (!director.StagedExitRunning)
                Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the reveal is skipped.");
        }

        /// <summary>
        /// The chrome steps aside while the keys are dealt and comes back when the card is gone. It is
        /// drawn from the committed result, and it named the nominees while the keys were still
        /// coming out.
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremonies_TheChromeStepsAsideWhileTheKeysAreDealt()
        {
            var keys = SceneComponents<KeyCeremony>().Single();
            yield return PlayUntilTheKeyCeremony(keys);
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome is aside while the keys are dealt.");
            Assert.That(director.IsSubmitHeldForCeremony, Is.True,
                "and the UI's Submit waits, so the key that moves the card on presses nothing under it.");

            keys.Cancel();
            yield return Frames(3);
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the card is over.");
            Assert.That(director.IsSubmitHeldForCeremony, Is.False, "and so is Submit.");
        }

        /// <summary>
        /// The evicted houseguest is in the room while their eviction is read out, and leaves with the
        /// card. The body was switched off at the commit, so the room turned to look at an empty spot.
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremonies_TheEvictedHouseguestStaysForTheirReveal()
        {
            var reveal = SceneComponents<VoteReveal>().Single();
            yield return PlayUntilTheVoteReveal(reveal);
            yield return null;
            string evicted = director.DepartingId;
            Assert.That(evicted, Is.Not.Null.And.Not.EqualTo(director.Snapshot.playerId), "A houseguest is being evicted.");
            Assert.That(director.Snapshot.Find(evicted).status, Is.Not.EqualTo(ContestantStatus.Active), "The save has them out already.");
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == evicted);
            Assert.That(body.gameObject.activeInHierarchy, Is.True, "Their body is still in the room while the votes are read.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "and the chrome does not name them first.");

            reveal.Cancel();
            yield return Frames(3);
            Assert.That(director.DepartingId, Is.Null);
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "They leave with the card.");
        }

        /// <summary>
        /// Escape while a card is up is the card's: it skips the reveal, and does not also close the
        /// notebook (or open the settings) underneath it.
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremonies_EscapeOnACardLeavesTheHouseAlone()
        {
            var keys = SceneComponents<KeyCeremony>().Single();
            yield return PlayUntilTheKeyCeremony(keys);
            director.OpenJournal();
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.True, "The notebook is open under the ceremony.");
            Assert.That(keys.IsPlaying, Is.True);

            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            // Past the card's read-first delay.
            float readable = Time.unscaledTime + 1f;
            while (Time.unscaledTime < readable) yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.True, "Escape on the card did not close the notebook underneath it.");
            keys.Cancel();
            yield return Frames(3);
        }

        /// <summary>
        /// The house's shortcuts wait while a card is up: the pad's X speeds a reveal up and is also
        /// Interact, so a press meant for the card acted in the house underneath it. J, the notebook,
        /// stands in for all of them - and works again once the card is gone.
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremonies_TheHousesShortcutsWaitForTheCard()
        {
            var keys = SceneComponents<KeyCeremony>().Single();
            yield return PlayUntilTheKeyCeremony(keys);
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "Nothing is open under the ceremony.");
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();

            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.J));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "The notebook key waited for the card.");

            keys.Cancel();
            yield return Frames(3);
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.J));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.True, "and opens the notebook once the card is gone.");
        }

        /// <summary>The reveal plays at the pace the settings chose, and the switch says what it does.</summary>
        [UnityTest]
        public IEnumerator Ceremonies_TheKeysPlayAtThePaceTheSettingsChose()
        {
            Assert.That(director.CeremonyPaceSetting, Is.EqualTo(CeremonyPace.Suspenseful), "Suspense is the default.");
            director.SetCeremonyPace(CeremonyPace.Quick);
            var keys = SceneComponents<KeyCeremony>().Single();
            yield return PlayUntilTheKeyCeremony(keys);
            Assert.That(keys.Pace, Is.EqualTo(CeremonyPace.Quick), "A quick setting makes a quick ceremony.");
            keys.Cancel();
            director.SetCeremonyPace(CeremonyPace.Suspenseful);
            yield return Frames(2);
        }

        /// <summary>
        /// The veto card tells the meeting's story: the holder, whoever came off the block, whoever went
        /// up in their place, and whoever stayed. Every face said "NOMINATED".
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremonies_TheVetoCardNamesTheHolderTheSavedAndTheReplacement()
        {
            yield return null;
            var state = director.Snapshot;
            var ids = state.Active.Where(person => !person.isPlayer).Select(person => person.id).ToList();
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(4));
            string kept = ids[0], saved = ids[1], replacement = ids[2], holder = ids[3];
            state.nominees = new List<string> { kept, replacement };
            state.vetoHolderId = holder;
            var before = new HashSet<string> { kept, saved };
            var method = typeof(EpisodeDirector).GetMethod("CeremonySubjects", BindingFlags.Instance | BindingFlags.NonPublic);
            var subjects = (List<CeremonyTakeover.Subject>)method.Invoke(director,
                new object[] { state, CeremonySting.VetoKind, new HashSet<string>(ids), before });
            string Badge(string id) => subjects.Single(subject => subject.Name == state.Find(id).name).Badge;
            Assert.That(Badge(holder), Is.EqualTo("VETO"));
            Assert.That(Badge(saved), Is.EqualTo("SAVED"));
            Assert.That(Badge(replacement), Is.EqualTo("REPLACEMENT"));
            Assert.That(Badge(kept), Is.EqualTo("NOMINATED"));
        }

        /// <summary>
        /// Reduced motion cuts to the ceremony's room instead of keeping whatever shot the viewer had:
        /// less motion, not less of the episode. The setting is switched on from Settings, where the
        /// player sets it: the fixture's reduced motion is the bodies' own, and the director reads
        /// the player's from preferences it leaves alone under a test save root.
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremonies_ReducedMotionCutsToTheRoom()
        {
            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Reduce character motion").onClick.Invoke();
            yield return null;
            director.ClosePanels();
            yield return null;
            var marker = SceneComponents<HouseRoomMarker>().First(room => room.RoomName == "Nomination");
            director.FrameCeremony(CeremonySting.NominationKind);
            yield return Frames(2);
            Assert.That(director.IsFramingCeremony, Is.True, "The ceremony is framed under reduced motion too.");
            var focus = cameraRig.DesiredFocus;
            Assert.That(new Vector2(focus.x - marker.transform.position.x, focus.z - marker.transform.position.z).magnitude, Is.LessThan(0.5f),
                "The camera is on the nomination room.");
            Assert.That(cameraRig.HasArrived(), Is.True, "and it cut there: a 1.2 second move would still be under way two frames in.");
        }
    }
}
