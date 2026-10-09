using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The front door and a season with no mouse (PLAN A, A6): the main menu to a season by the
    /// keyboard's Down and Enter and by the pad's d-pad, A and B; the D2 walk's twin on the pad, its
    /// focus moved by real d-pad presses and every commit a real A through the UI module; a fresh,
    /// rules-on season's first week from the front door on a pad; and the block speech a pad gives
    /// without typing.
    ///
    /// <para>Walks navigate with Down alone: the ring wraps, and the pad's d-pad up is also the
    /// overview's button with nothing open (Risk R9), its right the moves card.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The live control the event system has selected, or null.</summary>
        private static GameObject Selected() => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        private static string SelectedName() => Selected() != null ? Selected().name : "nothing";

        /// <summary>Whether the selected control carries exactly these words, or words starting so when <paramref name="prefix"/>.</summary>
        private static bool SelectedCarries(string caption, bool prefix = false)
        {
            var selected = Selected();
            return selected != null && selected.activeInHierarchy
                && selected.GetComponentsInChildren<TMP_Text>(true).Any(text => prefix ? text.text.StartsWith(caption) : text.text == caption);
        }

        /// <summary>One step down the ring: the keyboard's Down arrow, or the pad's d-pad down.</summary>
        private IEnumerator StepDown(bool pad)
        {
            if (pad) yield return PressOnPad(GamepadButton.DpadDown);
            else yield return PressKey(Key.DownArrow);
        }

        /// <summary>The press: the keyboard's Enter, or the pad's A, both the UI module's Submit.</summary>
        private IEnumerator PressSubmit(bool pad)
        {
            if (pad) yield return PressOnPad(GamepadButton.South);
            else yield return PressKey(Key.Enter);
        }

        /// <summary>
        /// Walks down the ring to the control carrying <paramref name="caption"/> and presses it - by
        /// real presses only, never by selecting it from the test - and fails naming where the walk
        /// ended when the ring never reaches it.
        /// </summary>
        private IEnumerator ReachAndPress(string caption, bool pad, bool prefix = false, int limit = 80)
        {
            for (int step = 0; step < limit && !SelectedCarries(caption, prefix); step++) yield return StepDown(pad);
            Assert.That(SelectedCarries(caption, prefix), Is.True, "The " + (pad ? "d-pad" : "Down key") + " never reached '" + caption
                + "'; the walk ended on '" + SelectedName() + "'.");
            lastSubmit = caption + " on '" + SelectedName() + "' by " + (pad ? "the pad's A" : "Enter");
            yield return PressSubmit(pad);
            yield return Frames(2);
        }

        /// <summary>Walks down the ring to the first control <paramref name="wanted"/> accepts, and presses it.</summary>
        private IEnumerator ReachAndPress(System.Func<GameObject, bool> wanted, string what, bool pad, int limit = 80)
        {
            for (int step = 0; step < limit && !(Selected() != null && wanted(Selected())); step++) yield return StepDown(pad);
            Assert.That(Selected() != null && wanted(Selected()), Is.True, "The ring never reached " + what + "; it ended on '" + SelectedName() + "'.");
            yield return PressSubmit(pad);
            yield return Frames(2);
        }

        /// <summary>A cast card on the cast screen: a control named for one of the rosters' houseguests.</summary>
        private static bool IsCastCard(GameObject control) =>
            control != null && control.activeInHierarchy && control.GetComponent<Button>() != null
            && CastTemplates.Everyone.Any(template => template.Name == control.name);

        /// <summary>The menu, the cast screen and the creator are all gone and a new season is in the house.</summary>
        private void AssertASeasonStarted(string sessionBefore, string how)
        {
            Assert.That(Menu().IsShowing || CastScreen().IsShowing || Creator().IsShowing, Is.False, how + ": no front-door screen is left up.");
            Assert.That(director.Snapshot.sessionId, Is.Not.EqualTo(sessionBefore), how + ": a new season is in the house.");
            Assert.That(director.SeasonInProgress, Is.True);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator FrontDoor_TheKeyboardStartsASeasonFromTheMainMenu()
        {
            // The first route: a card, then Play as.
            string before = director.Snapshot.sessionId;
            director.OpenMainMenu();
            yield return Frames(2);
            Assert.That(Menu().IsShowing, Is.True);
            yield return ReachAndPress(MainMenu.NewSeasonCaption, pad: false);
            Assert.That(CastScreen().IsShowing, Is.True, "Enter on Start a new season opens the cast screen: " + lastSubmit);
            yield return ReachAndPress(IsCastCard, "a cast card", pad: false);
            Assert.That(SelectedCarries("Play as ", prefix: true), Is.True, "Enter on a card puts the keyboard on Play as, not on " + SelectedName() + ".");
            yield return PressSubmit(false);
            yield return Frames(3);
            AssertASeasonStarted(before, "The keyboard's Play as");

            // The second: Customize, the name typed (typing is the allowed raw read), then Start.
            yield return FreshEpisode();
            before = director.Snapshot.sessionId;
            director.OpenMainMenu();
            yield return Frames(2);
            yield return ReachAndPress(MainMenu.NewSeasonCaption, pad: false);
            yield return ReachAndPress(IsCastCard, "a cast card", pad: false);
            yield return ReachAndPress("Customize ", pad: false, prefix: true);
            Assert.That(Creator().IsShowing, Is.True, "Enter on Customize opens the creator: " + lastSubmit);
            CreatorEntryName("Keyboard Kim");
            yield return null;
            yield return ReachAndPress(CharacterCreator.StartCaption, pad: false);
            yield return Frames(3);
            AssertASeasonStarted(before, "The keyboard's Customize");
            Assert.That(director.Snapshot.Find(director.Snapshot.playerId).name, Is.EqualTo("Keyboard Kim"), "The typed name is the player's.");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator FrontDoor_APadStartsASeasonFromTheMainMenu()
        {
            string before = director.Snapshot.sessionId;
            director.OpenMainMenu();
            yield return Frames(2);
            yield return ReachAndPress(MainMenu.NewSeasonCaption, pad: true);
            Assert.That(CastScreen().IsShowing, Is.True, "A on Start a new season opens the cast screen: " + lastSubmit);

            // B backs out to the menu, as Escape does, and nothing is built.
            yield return PressOnPad(GamepadButton.East);
            yield return Frames(2);
            Assert.That(CastScreen().IsShowing, Is.False, "B leaves the cast screen,");
            Assert.That(Menu().IsShowing, Is.True, "for the menu it came from.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before), "Backing out builds nothing.");

            yield return ReachAndPress(MainMenu.NewSeasonCaption, pad: true);
            yield return ReachAndPress(IsCastCard, "a cast card", pad: true);
            Assert.That(SelectedCarries("Play as ", prefix: true), Is.True, "A on a card puts the pad on Play as, not on " + SelectedName() + ".");
            yield return PressSubmit(true);
            yield return Frames(3);
            AssertASeasonStarted(before, "The pad's Play as");
            Assert.That(director.HintsForPad, Is.True, "The house speaks of the pad it was started with.");
        }

        // ---------------------------------------------------------------- the D2 walk's twin

        /// <summary>
        /// The panel under <paramref name="rootName"/> owns the pad: focus starts inside it, and real
        /// d-pad downs visit every interactable control in it once and come back to the first.
        /// </summary>
        private IEnumerator AssertPadRing(string label, string rootName)
        {
            yield return null;
            var root = SceneComponents<RectTransform>().FirstOrDefault(rect => rect.name == rootName && rect.gameObject.activeInHierarchy);
            Assert.That(root, Is.Not.Null, label + ": expected an active '" + rootName + "' (IsPanelOpen " + director.IsPanelOpen
                + ", the last press " + lastSubmit + ").");
            var eligible = root.GetComponentsInChildren<Selectable>()
                .Where(item => item.IsActive() && item.IsInteractable() && !(item is Scrollbar)).ToList();
            Assert.That(eligible, Is.Not.Empty, label + ": a panel with nothing to press cannot be walked.");
            var start = Selected() != null ? Selected().GetComponent<Selectable>() : null;
            Assert.That(start, Is.Not.Null, label + ": the pad's focus must start on something.");
            Assert.That(start.transform.IsChildOf(root), Is.True, label + ": focus started on '" + start.name + "', outside the panel.");
            var ring = new List<Selectable> { start };
            for (int step = 0; step < eligible.Count + 1; step++)
            {
                yield return PressOnPad(GamepadButton.DpadDown);
                var next = Selected() != null ? Selected().GetComponent<Selectable>() : null;
                Assert.That(next, Is.Not.Null, label + ": the d-pad must keep a control focused.");
                if (next == start) break;
                if (!ring.Contains(next)) ring.Add(next);
            }
            Assert.That(ring, Is.EquivalentTo(eligible), label + ": the d-pad visits every control in the panel once and comes back. Visited: ["
                + string.Join(" | ", ring.Select(item => item != null ? item.name : "<destroyed>")) + "].");
            Assert.That(Selected(), Is.EqualTo(start.gameObject), label + ": down the ring wraps to its first control.");
        }

        /// <summary>Opens the episode screen by the pad: A on the rail's way there, then A again at the screen, or X once there on foot.</summary>
        private IEnumerator PadOpenTheStation()
        {
            yield return ReachAndPress("Go to episode screen", pad: true);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!director.IsPhasePanelOpen && Time.realtimeSinceStartup < deadline
                   && Vector3.Distance(player.transform.position, director.StationPosition) > 2.5f)
                yield return null;
            if (!director.IsPhasePanelOpen && director.LastTravel == EpisodeDirector.TravelKind.Warp)
                yield return ReachAndPress("Go to episode screen", pad: true);
            // On foot, the screen is the interact button's while the player is heading to it.
            if (!director.IsPhasePanelOpen) { yield return PressOnPad(GamepadButton.West); yield return Frames(2); }
            Assert.That(director.IsPhasePanelOpen, Is.True, "The pad opens the episode screen in " + director.Snapshot.phase
                + " (travel " + director.LastTravel + ", " + Vector3.Distance(player.transform.position, director.StationPosition).ToString("0.0") + " m away).");
        }

        /// <summary>Continues the competition's results by the pad's A, after the quarter second the card lets the press that raised it go by.</summary>
        private IEnumerator PadContinueCompetitionResults()
        {
            var result = SceneComponents<CompetitionResult>().SingleOrDefault(card => card.IsPlaying);
            if (result == null) yield break;
            var before = director.Snapshot;
            float until = Time.realtimeSinceStartup + 5f;
            while (!result.TakesAPress && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(SelectedCarries("Continue", prefix: true) || Selected() != null && Selected().name == "Continue from competition results", Is.True,
                "The results put the pad on Continue, not on " + SelectedName() + ".");
            yield return PressOnPad(GamepadButton.South);
            yield return Frames(2);
            Assert.That(result.IsPlaying, Is.False, "A continues from the results.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "Continuing commits nothing.");
        }

        /// <summary>
        /// D7: the D2 walk on the pad. The same season, the same decisions; every focus move a real
        /// d-pad press and every commit a real A through the UI module, never an event raised by the
        /// test. Competitions go through the accessible alternative (the lead's decision A-6).
        /// </summary>
        [UnityTest, Timeout(1500000)]
        public IEnumerator Accessibility_EveryPanelIsWalkableAndCommittableByPad()
        {
            Assert.That(EventSystem.current.currentInputModule, Is.TypeOf<InputSystemUIInputModule>(), "A reaches a control as Submit only through the UI module.");
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            director.SetCeremonyPace(CeremonyPace.Quick);

            // The notebook by Select, the settings by Start: both closed by B.
            yield return PressOnPad(GamepadButton.Select);
            yield return Frames(2);
            yield return AssertPadRing("notebook", ModalRoot);
            yield return PressOnPad(GamepadButton.East);
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "B closes the notebook.");
            yield return PressOnPad(GamepadButton.Start);
            yield return Frames(2);
            yield return AssertPadRing("settings", ModalRoot);
            yield return PressOnPad(GamepadButton.East);
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "B closes the settings.");

            var walked = new HashSet<string>();
            var previous = director.Snapshot;
            for (int guard = 0; guard < 200 && director.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                if (SceneComponents<CompetitionResult>().Any(result => result.IsPlaying))
                {
                    yield return PadContinueCompetitionResults();
                    walked.Add("competition results");
                }
                yield return AwaitRecap(previous, director.Snapshot);
                if (director.IsWeeklyRecapOpen)
                {
                    yield return AssertPadRing("weekly recap", RecapRoot);
                    var shown = director.Snapshot.revision;
                    yield return ReachAndPress(WeeklyRecapScreen.ContinueCaption, pad: true);
                    Assert.That(director.IsWeeklyRecapOpen, Is.False, "A on Continue dismisses the recap.");
                    Assert.That(director.Snapshot.revision, Is.EqualTo(shown), "Dismissing the recap commits nothing.");
                    walked.Add("weekly recap");
                    previous = director.Snapshot;
                    continue;
                }

                if (!director.IsPanelOpen)
                {
                    if (director.Snapshot.Find(director.Snapshot.playerId).status == ContestantStatus.Active) yield return PadOpenTheStation();
                    else
                    {
                        // Out of the house there is no walk to take: the screen opens where they are.
                        Assert.That(director.TryOpenPhasePanel(), Is.True, "The station opens for a player out of the house in " + director.Snapshot.phase);
                        yield return Frames(2);
                    }
                }
                var state = director.Snapshot;
                yield return AssertPadRing("phase " + state.phase, ModalRoot);
                walked.Add(state.phase.ToString());
                foreach (var caption in KeyboardCaptions(state)) yield return ReachAndPress(caption, pad: true);
                Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision + 1),
                    "A must commit exactly one decision in " + state.phase + " (" + string.Join(", ", KeyboardCaptions(state)) + "); " + lastSubmit);
                yield return SettleReveal();
                previous = state;
            }

            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "The pad alone carries a season to its end.");
            Assert.That(walked, Does.Contain("weekly recap").And.Contain("competition results"));
            Assert.That(walked, Does.Contain(EpisodePhase.Nomination.ToString()).And.Contain(EpisodePhase.Eviction.ToString())
                .And.Contain(EpisodePhase.JuryQuestioning.ToString()));

            if (!director.IsPanelOpen)
            {
                Assert.That(director.TryOpenPhasePanel(), Is.True);
                yield return Frames(2);
            }
            yield return AssertPadRing("finale", ModalRoot);
            yield return ReachAndPress("Season report", pad: true);
            var report = director.GetComponentsInChildren<SeasonReport>(true).Single();
            Assert.That(report.IsShowing, Is.True, "A on the report control shows the report.");
            yield return PressOnPad(GamepadButton.East);
            yield return Frames(2);
            Assert.That(report.IsShowing, Is.False, "B closes the report.");
        }

        // ---------------------------------------------------------------- the testers' first week (E2)

        /// <summary>
        /// The captions of every live control in the open panel, for a failure that says what a
        /// rules-on week put up which the walk's chooser does not yet know (Risk R8).
        /// </summary>
        private string PanelCaptions()
        {
            var root = SceneComponents<RectTransform>().FirstOrDefault(rect => rect.name == ModalRoot && rect.gameObject.activeInHierarchy);
            if (root == null) return "(no panel)";
            return string.Join(" | ", root.GetComponentsInChildren<Button>().Where(button => button.IsActive() && button.IsInteractable())
                .Select(button => string.Join(" ", button.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text))));
        }

        /// <summary>
        /// D7's second half, the testers' E2 path (the lead's decision 11): from the main menu on a pad
        /// into a fresh season with every shipped rule on - stories, the economy, commitments, war rooms
        /// and leaks - and through its first week to the eviction's recap, every decision by d-pad and
        /// A. Each panel's captions are written to the log, so a surface the chooser does not know is
        /// named by the failure rather than guessed at.
        /// </summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator FrontDoor_APadPlaysAFreshSeasonsFirstWeekToItsEviction()
        {
            string before = director.Snapshot.sessionId;
            director.OpenMainMenu();
            yield return Frames(2);
            yield return ReachAndPress(MainMenu.NewSeasonCaption, pad: true);
            yield return ReachAndPress(IsCastCard, "a cast card", pad: true);
            yield return PressSubmit(true);
            yield return Frames(3);
            AssertASeasonStarted(before, "The pad's Play as");
            director.SuspendNpcAutonomyForDiagnostics();
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            director.SetCeremonyPace(CeremonyPace.Quick);
            Assert.That(director.Snapshot.week, Is.EqualTo(1));

            var previous = director.Snapshot;
            bool evicted = false;
            for (int guard = 0; guard < 120 && !evicted; guard++)
            {
                if (SceneComponents<CompetitionResult>().Any(result => result.IsPlaying)) yield return PadContinueCompetitionResults();
                yield return AwaitRecap(previous, director.Snapshot);
                if (director.IsWeeklyRecapOpen)
                {
                    yield return ReachAndPress(WeeklyRecapScreen.ContinueCaption, pad: true);
                    evicted = true;
                    break;
                }
                // A scene card or reply card put up by the week: answered by its first choice, or closed.
                if (director.IsSceneCardOpen)
                {
                    TestContext.WriteLine("first week: scene card - " + PanelCaptions());
                    yield return PressOnPad(GamepadButton.East);
                    yield return Frames(2);
                    continue;
                }
                if (!director.IsPanelOpen) yield return PadOpenTheStation();
                var state = director.Snapshot;
                TestContext.WriteLine("first week: " + state.phase + " (rev " + state.revision + ") - " + PanelCaptions());
                var captions = KeyboardCaptions(state).ToList();
                foreach (var caption in captions)
                {
                    Assert.That(ControlCarrying(caption), Is.Not.Null, "The rules-on week put up a panel the walk does not know in " + state.phase
                        + ": it wanted '" + caption + "' and the panel offers " + PanelCaptions() + ".");
                    yield return ReachAndPress(caption, pad: true);
                }
                Assert.That(director.Snapshot.revision, Is.GreaterThan(state.revision), "A commits a decision in " + state.phase + "; " + lastSubmit);
                yield return SettleReveal();
                previous = state;
            }
            Assert.That(evicted, Is.True, "The pad reached week 1's eviction recap.");
            Assert.That(director.Snapshot.contestants.Count(actor => actor.status != ContestantStatus.Active), Is.GreaterThanOrEqualTo(1),
                "Somebody left the house in week 1.");
        }

        // ---------------------------------------------------------------- the block speech without typing

        /// <summary>
        /// A pad cannot type, so the block speech has a third way (the lead's decision 8): "Deliver a
        /// prepared speech", after "Say nothing", carrying the chosen approach with its authored line.
        /// Reached and pressed on the pad, from Say nothing by one d-pad down; the receipt's approach
        /// is the one chosen, the text that approach's line, never Quiet, and nothing is rolled.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator BlockSpeeches_APreparedSpeechCarriesTheApproachWithoutTyping()
        {
            foreach (string key in BlockSpeeches.Approaches)
            {
                yield return InstallBlockSpeechHouse();
                yield return OpenBlockSpeech(privateRoom: false);
                var before = director.Snapshot;
                Assert.That(BlockSpeeches.RulesOn(before), Is.True, "The fixture plays the block speech's rules.");
                ButtonWithCaption(BlockSpeeches.Label(key)).onClick.Invoke();
                yield return null;
                var prepared = ButtonWithCaption(EpisodeHud.PreparedSpeechCaption);
                Assert.That(prepared.IsInteractable(), Is.True, key + ": the prepared speech is offered.");
                // From Say nothing, one d-pad down is the prepared speech: it stands after it.
                EventSystem.current.SetSelectedGameObject(ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).gameObject);
                yield return null;
                yield return PressOnPad(GamepadButton.DpadDown);
                Assert.That(SelectedCarries(EpisodeHud.PreparedSpeechCaption), Is.True, key + ": down from Say nothing is the prepared speech, not " + SelectedName());
                yield return PressOnPad(GamepadButton.South);
                yield return Frames(2);
                string text = EpisodeDirector.PreparedSpeech(key);
                Assert.That(text, Is.Not.Empty);
                AssertBlockSpeechCommit(before, text, key);
                var speech = director.Snapshot.evictionSpeeches.Single(item => item.speakerId == director.Snapshot.playerId);
                Assert.That(BlockSpeeches.Approach(director.Snapshot, speech), Is.EqualTo(key), "The receipt carries the chosen approach.");
                Assert.That(BlockSpeeches.Approach(director.Snapshot, speech), Is.Not.EqualTo(BlockSpeeches.Quiet));
            }
            // And in the diary, where a block speech can be given too.
            yield return InstallBlockSpeechHouse();
            yield return OpenBlockSpeech(privateRoom: true);
            var diaryBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.PreparedSpeechCaption).onClick.Invoke();
            AssertBlockSpeechCommit(diaryBefore, EpisodeDirector.PreparedSpeech(LobbyApproach.Emotional), LobbyApproach.Emotional);
        }

        // ---------------------------------------------------------------- one press, one stop (Risk R6)

        /// <summary>The timing bar's count as its caption says it: "Attempt 2 of 3 · Aim for the center".</summary>
        private string TimingBarAttemptLine()
        {
            var line = director.GetComponentsInChildren<TMP_Text>()
                .LastOrDefault(text => text.isActiveAndEnabled && text.text.StartsWith("Attempt ") && text.text.Contains(" of 3"));
            return line != null ? line.text : "(no attempt line)";
        }

        /// <summary>Puts the focus on the live STOP control, as the pad's A finds it: the last copy, since the HUD can rebuild.</summary>
        private void FocusTheStop()
        {
            var stop = director.GetComponentsInChildren<Button>()
                .LastOrDefault(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == InputGlossary.HouseChallengeStopCaption));
            Assert.That(stop, Is.Not.Null, "The timing bar offers '" + InputGlossary.HouseChallengeStopCaption + "'.");
            EventSystem.current.SetSelectedGameObject(stop.gameObject);
        }

        /// <summary>
        /// Risk R6 (PLAN A, A6): on the timing bar the pad's A, with "STOP marker  [Space]" focused, is
        /// both the shortcuts' Hit and the UI's Submit on the control, and each stopped the marker - two
        /// of the three attempts in one press, and a ranked attempt's Compete committed on the second.
        /// One press is one attempt: the pad's A, the keyboard's Space (the Hit alone) and Enter on the
        /// control (the Submit alone); and a ranked attempt commits on its third A, once. No category a
        /// season deals plays the bar any more, so it is started through its seam.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Challenge_OnePadPressIsOneAttempt()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            // The pad is the device before the bar goes up, so the bar's paragraph is the pad's.
            yield return PressOnPad(GamepadButton.RightStick);
            yield return Frames(2);
            Assert.That(director.HintsForPad, Is.True, "A pad press makes the pad the device.");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var before = director.Snapshot;

            Assert.That(director.StartTimingBarForDiagnostics(practice: true), Is.True, "The timing bar starts from the competition's briefing.");
            yield return Frames(2);
            Assert.That(director.GetComponentsInChildren<TMP_Text>().Any(text => text.isActiveAndEnabled && text.text == InputGlossary.ChallengeLine(true)),
                Is.True, "On a pad the bar's paragraph names the pad's buttons (A4f).");
            FocusTheStop();
            yield return null;
            Assert.That(TimingBarAttemptLine(), Does.StartWith("Attempt 1 of 3"));

            yield return PressOnPad(GamepadButton.South);
            Assert.That(TimingBarAttemptLine(), Does.StartWith("Attempt 2 of 3"),
                "One A on the focused STOP is one attempt, though it is the shortcuts' Hit and the UI's Submit at once.");
            yield return PressKey(Key.Space);
            Assert.That(TimingBarAttemptLine(), Does.StartWith("Attempt 3 of 3"), "Space, the Hit alone, is one attempt.");
            FocusTheStop();
            yield return null;
            yield return PressKey(Key.Enter);
            yield return Frames(2);
            Assert.That(director.IsChallengeActive, Is.False, "Enter on the STOP, the Submit alone, is the third attempt: the practice is over.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A practice commits nothing.");

            // Ranked: two presses leave the attempt open, and the third commits its Compete, once.
            Assert.That(director.IsPhasePanelOpen, Is.True, "The briefing is back after the practice.");
            Assert.That(director.StartTimingBarForDiagnostics(practice: false), Is.True, "A ranked bar starts from the briefing too.");
            yield return Frames(2);
            for (int press = 1; press <= 2; press++)
            {
                FocusTheStop();
                yield return null;
                yield return PressOnPad(GamepadButton.South);
                Assert.That(director.IsChallengeActive, Is.True, "The ranked attempt is still open after A number " + press + ": " + TimingBarAttemptLine());
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "and nothing is committed yet.");
            }
            FocusTheStop();
            yield return null;
            yield return PressOnPad(GamepadButton.South);
            yield return Frames(2);
            Assert.That(director.IsChallengeActive, Is.False, "The third A ends the ranked attempt");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1), "and commits its Compete, once.");
            Assert.That(director.Snapshot.competitionResolved, Is.True, "The competition is decided.");
        }
    }
}
