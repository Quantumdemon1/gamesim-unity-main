using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
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
    /// D2: every panel the season puts up can be walked with the keyboard and committed with Enter.
    ///
    /// <para>One test, one season, no mouse. Each panel is checked the same way: focus starts inside
    /// it, actual Tab presses visit every interactable control and nothing else, nothing outside it is
    /// reachable, Tab moves one step, and the decision is committed by <c>Submit</c> on the selected
    /// control — the event Enter raises through the input module — never by invoking a click.
    /// The conversation, notebook and settings panels, the weekly recap and the season report are
    /// walked as well as every phase panel, because "every action" includes the ones that are not
    /// ceremonies.</para>
    ///
    /// <para>The recap and report earned their place here: both went up without taking focus, so
    /// the selection stayed on a HUD control behind the scrim and Enter would have pressed that.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string ModalRoot = "Episode panel";
        private const string RecapRoot = "Gamesim Weekly Recap";
        private const string ReportRoot = "Gamesim Season Report";
        private const string CompetitionResultRoot = "Gamesim Competition Result";

        [UnityTest]
        public IEnumerator Conversation_ArrowKeysMoveSpatiallyWhileTabKeepsReadingOrder()
        {
            var maya=SceneComponents<HouseNpc>().Single(npc=>npc.Id==ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            var grid=director.GetComponentsInChildren<RectTransform>().Single(rect=>rect.name==EpisodeHud.DialName);
            var buttons=grid.GetComponentsInChildren<Button>().Where(button=>button.IsInteractable()).ToArray();
            Assert.That(buttons.Length,Is.GreaterThan(5));
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);yield return null;
            yield return PressKey(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[1].gameObject));
            yield return PressKey(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[5].gameObject));
            yield return PressKey(Key.Tab,shift:true);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[4].gameObject));
            director.ClosePanels();yield return null;
        }

        [UnityTest]
        public IEnumerator Conversation_GridBoundaryArrowsReachTheFollowingRowsWithoutMovingSideways()
        {
            var maya=SceneComponents<HouseNpc>().Single(npc=>npc.Id==ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            var grid=director.GetComponentsInChildren<RectTransform>().Single(rect=>rect.name==EpisodeHud.DialName);
            var buttons=grid.GetComponentsInChildren<Button>().Where(button=>button.IsInteractable()).ToArray();
            Assert.That(buttons.Length,Is.EqualTo(7),"The final row has three cells and an empty fourth column.");
            var following=ButtonWithCaption(EpisodeHud.DiscussGameCaption);
            var revision=director.Snapshot.revision;
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);yield return null;

            // Only this initial focus is staged. Every transition below is real input through
            // the UI module, including both the first and middle cells of the bottom row.
            yield return PressKey(Key.LeftArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[0].gameObject));
            yield return PressKey(Key.RightArrow);
            yield return PressKey(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[5].gameObject));
            yield return PressKey(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(following.gameObject),"Down exits the middle bottom cell to the rows below.");
            yield return PressKey(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[6].gameObject),"The first row returns to the grid's last topic.");
            yield return PressKey(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[6].gameObject));
            yield return PressKey(Key.LeftArrow);yield return PressKey(Key.LeftArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[4].gameObject));
            yield return PressKey(Key.LeftArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[4].gameObject));
            yield return PressKey(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(following.gameObject),"Down must not move sideways to the next bottom-row topic.");
            yield return PressKey(Key.Tab,shift:true);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[6].gameObject),"Shift+Tab retains reading order.");
            yield return PressKey(Key.Tab);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(following.gameObject));
            yield return PressKey(Key.UpArrow);yield return PressKey(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[2].gameObject));
            yield return PressKey(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(buttons[3].gameObject));
            yield return PressKey(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(following.gameObject),"An empty cell below also exits vertically rather than wrapping left.");
            Assert.That(director.Snapshot.revision,Is.EqualTo(revision),"Moving focus must not commit a conversation.");
            director.ClosePanels();yield return null;
        }

        [UnityTest]
        public IEnumerator Accessibility_EveryPanelIsWalkableAndCommittableByKeyboard()
        {
            Assert.That(EventSystem.current.currentInputModule, Is.TypeOf<InputSystemUIInputModule>(),
                "Enter reaches a control as Submit only through the input-system UI module.");
            player.Agent.speed = 25; player.Agent.acceleration = 100;

            // The world chrome, then the three panels that are not ceremonies.
            yield return KeyboardSubmit("Notebook [J]");
            yield return AssertKeyboardRing("notebook", ModalRoot);
            yield return KeyboardSubmit("Close  [Esc]");
            Assert.That(director.IsPanelOpen, Is.False, "Enter on Close must close the notebook.");

            yield return KeyboardSubmit("Settings");
            yield return AssertKeyboardRing("settings", ModalRoot);
            yield return KeyboardSubmit("Close  [Esc]");
            Assert.That(director.IsPanelOpen, Is.False, "Enter on Close must close the settings.");

            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            yield return AssertKeyboardRing("conversation", ModalRoot);
            var talked = director.Snapshot;
            yield return KeyboardSubmit("Spend time together");
            Assert.That(director.Snapshot.revision, Is.EqualTo(talked.revision + 1), "Enter on a conversation control must commit it.");
            director.ClosePanels();
            yield return null;

            // The season, one decision per panel, each committed with Enter.
            var walked = new HashSet<string>();
            var previous = director.Snapshot;
            for (int guard = 0; guard < 200 && director.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                if (SceneComponents<CompetitionResult>().Any(result => result.IsPlaying))
                {
                    yield return ContinueCompetitionResults(byKeyboard: true);
                    walked.Add("competition results");
                }
                yield return AwaitRecap(previous, director.Snapshot);
                if (director.IsWeeklyRecapOpen)
                {
                    yield return AssertKeyboardRing("weekly recap", RecapRoot);
                    var shown = director.Snapshot.revision;
                    yield return KeyboardSubmit(WeeklyRecapScreen.ContinueCaption);
                    Assert.That(director.IsWeeklyRecapOpen, Is.False, "Enter on Continue must dismiss the recap.");
                    Assert.That(director.Snapshot.revision, Is.EqualTo(shown), "Dismissing the recap commits nothing.");
                    // The house comes back to a houseguest who is still in it; a juror watching the
                    // recap from the jury house has no walk to get back to.
                    bool inTheHouse = director.Snapshot.Find(director.Snapshot.playerId).status == ContestantStatus.Active;
                    Assert.That(player.InputEnabled, Is.EqualTo(inTheHouse), "Dismissing the recap must hand the house back to a player who is still in it.");
                    walked.Add("weekly recap");
                    previous = director.Snapshot;
                    continue;
                }

                if (!director.IsPanelOpen)
                {
                    WarpPlayer(director.StationPosition);
                    Assert.That(director.TryOpenPhasePanel(), Is.True, "The station must open in " + director.Snapshot.phase);
                    yield return null; yield return null;
                }
                var state = director.Snapshot;
                yield return AssertKeyboardRing("phase " + state.phase, ModalRoot);
                walked.Add(state.phase.ToString());

                foreach (var caption in KeyboardCaptions(state))
                    yield return KeyboardSubmit(caption);
                Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision + 1),
                    "Enter must commit exactly one decision in " + state.phase + " (" + string.Join(", ", KeyboardCaptions(state)) + ").");
                // A player cannot press anything while the vote reveal narrates the eviction, and a
                // commit made under it would make the director drop the recap as an interruption -
                // correctly. Let the beat finish the way a player has to.
                yield return SettleReveal();
                previous = state;
            }

            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "The keyboard alone must carry a season to its end.");
            Assert.That(walked, Does.Contain("weekly recap"), "A regular eviction owes a recap, and the walk must have met one.");
            Assert.That(walked, Does.Contain("competition results"), "Committed results need their own keyboard dismissal before the phase can continue.");
            Assert.That(walked, Does.Contain(EpisodePhase.Nomination.ToString()).And.Contain(EpisodePhase.Eviction.ToString())
                .And.Contain(EpisodePhase.JuryQuestioning.ToString()));

            // The finale's own panel, and the report it opens.
            if (!director.IsPanelOpen)
            {
                WarpPlayer(director.StationPosition);
                Assert.That(director.TryOpenPhasePanel(), Is.True);
                yield return null; yield return null;
            }
            yield return AssertKeyboardRing("finale", ModalRoot);
            yield return KeyboardSubmit("Season report");
            var report = director.GetComponentsInChildren<SeasonReport>(true).Single();
            Assert.That(report.IsShowing, Is.True, "Enter on the report control must show the report.");
            yield return AssertKeyboardRing("season report", ReportRoot);
            yield return KeyboardSubmit("Close");
            Assert.That(report.IsShowing, Is.False, "Enter on Close must dismiss the report.");
        }

        /// <summary>
        /// A regular eviction owes a recap once the reveal has finished. The commit that resolved
        /// the vote is the one that queues it; the phases that end the season are the ones the
        /// director itself declines to recap over.
        /// </summary>
        private IEnumerator AwaitRecap(EpisodeState before, EpisodeState after)
        {
            bool resolved = before.phase == EpisodePhase.Eviction && !before.evictionResolved && after.evictionResolved;
            if (!resolved) yield break;
            yield return SettleReveal();
            var now = director.Snapshot.phase;
            bool ending = now == EpisodePhase.FinalEviction || now == EpisodePhase.JuryQuestioning
                || now == EpisodePhase.FinalSpeeches || now == EpisodePhase.Jury || now == EpisodePhase.Finished;
            if (ending) yield break;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!director.IsWeeklyRecapOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsWeeklyRecapOpen, Is.True, "The week " + before.week + " recap never opened after its eviction.");
        }

        /// <summary>Waits out a vote reveal or ceremony card, on the wall clock: the reveal holds for seconds even headless.</summary>
        private IEnumerator SettleReveal()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline
                   && (SceneComponents<VoteReveal>().Any(reveal => reveal.IsPlaying)
                       || SceneComponents<CeremonyTakeover>().Any(card => card.IsPlaying)
                       || SceneComponents<KeyCeremony>().Any(key => key.IsPlaying)))
                yield return null;
        }

        /// <summary>Presses the persistent result's real Continue control before touching its underlying phase.</summary>
        private IEnumerator ContinueCompetitionResults(bool byKeyboard)
        {
            var result = SceneComponents<CompetitionResult>().SingleOrDefault(card => card.IsPlaying);
            if (result == null) yield break;
            var before = director.Snapshot;
            bool hadPanel = director.IsWeeklyRecapOpen || director.GetComponentsInChildren<RectTransform>(true)
                .Any(rect => rect.name == ModalRoot && rect.gameObject.activeInHierarchy);
            Assert.That(player.InputEnabled, Is.False, "The results scrim must keep house movement disabled.");
            Assert.That(cameraRig.ControlsEnabled, Is.False, "The result also owns camera input.");
            yield return AssertKeyboardRing("competition results", CompetitionResultRoot);
            // The opening press is deliberately ignored for 250 ms so resolving a competition
            // cannot instantly dismiss its results with the same Enter or controller press.
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(result.IsPlaying, Is.True, "Results must wait for an explicit Continue.");
            var button = result.GetComponentsInChildren<Button>().Single(item => item.name == "Continue from competition results");
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(button.gameObject));
            if (byKeyboard) yield return PressKey(Key.Enter);
            else
            {
                var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                Assert.That(ExecuteEvents.Execute(button.gameObject, click, ExecuteEvents.pointerClickHandler), Is.True);
                Assert.That(player.InputEnabled, Is.False, "The dismissal click must not become a house movement click in the same frame.");
                Assert.That(cameraRig.ControlsEnabled, Is.False);
            }
            yield return null; yield return null;
            Assert.That(result.IsPlaying, Is.False, "Continue must dismiss the results.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "Dismissing results must not submit the next decision.");
            Assert.That(director.Snapshot.phase, Is.EqualTo(before.phase), "The dismissal key must not leak into the phase underneath.");
            Assert.That(director.IsPanelOpen, Is.EqualTo(hadPanel), "Continue restores the context beneath the result.");
            Assert.That(player.InputEnabled, Is.EqualTo(!hadPanel && before.Find(before.playerId).status == ContestantStatus.Active),
                "House input returns only when no underlying panel owns it and the player is still active.");
            Assert.That(cameraRig.ControlsEnabled, Is.EqualTo(!hadPanel));
        }

        /// <summary>
        /// What the keyboard presses to make the same decision the command driver would. Several
        /// captions mean several presses that together commit once, as nominations do.
        /// </summary>
        private static IEnumerable<string> KeyboardCaptions(EpisodeState state)
        {
            if (state.pendingDiary != null) { yield return EpisodeHud.DiarySkipReflectionCaption; yield break; }
            if (state.phase == EpisodePhase.Social && HouseEvents.Pending(state) != null)
            { yield return EpisodeHud.EventChoiceCaption(HouseEvents.Pending(state).choices[0].label); yield break; }
            if (EpisodeEngine.IsCompetition(state.phase))
            {
                yield return state.competitionResolved ? "Continue to the next ceremony"
                    : EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer)
                        ? "Accessible alternative: steady 1-point bonus" : "Watch eligible housemates compete";
                yield break;
            }
            if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                foreach (var candidate in EpisodeEngine.NominationCandidates(state).Take(2)) yield return candidate.name;
                yield return "Commit nominations";
                yield break;
            }
            if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && (state.vetoHolderId == state.playerId || state.hohId == state.playerId && EpisodeEngine.NpcVetoSave(state) != null))
            {
                var replacement = EpisodeEngine.ReplacementCandidates(state).FirstOrDefault();
                bool use = replacement != null && !EpisodeEngine.VetoIsLockedAtFinalFour(state);
                if (!use) yield return "Do not use the veto";
                else if (state.hohId == state.playerId) yield return replacement.name;
                else yield return "Save " + state.Find(state.nominees[0]).name + " (HoH chooses replacement)";
                yield break;
            }
            if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches
                && state.nominees.Contains(state.playerId)
                && !state.evictionSpeeches.Any(speech => speech.speakerId == state.playerId))
            { yield return EpisodeHud.EvictionSpeechSkipCaption; yield break; }
            if (state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && (state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker)
                && !state.votes.Any(vote => vote.voterId == state.playerId)
                && (EpisodeEngine.Voters(state).Any(actor => actor.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state)))
            { yield return "Vote to evict " + state.Find(state.nominees[0]).name; yield break; }
            if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            { yield return "Evict " + state.Active.First(actor => !actor.isPlayer).name; yield break; }
            if (state.phase == EpisodePhase.JuryQuestioning)
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.completed) yield return EpisodeHud.JuryContinueCaption;
                else if (exchange.finalistId == state.playerId) yield return "A · " + exchange.optionA;
                else
                {
                    var option = WebJuryQuestioning.GetJurorQuestionOptions(state.juryQuestionIndex).Single(item => item.tone == "neutral");
                    yield return option.tone + " · " + option.text;
                }
                yield break;
            }
            if (state.phase == EpisodePhase.FinalSpeeches)
            {
                yield return state.Active.Any(actor => actor.isPlayer) && !state.finalSpeeches.Any(speech => speech.speakerId == state.playerId)
                    ? EpisodeHud.SpeechSkipCaption : EpisodeHud.SpeechContinueCaption;
                yield break;
            }
            if (state.phase == EpisodePhase.Jury && !state.Active.Any(actor => actor.isPlayer) && !state.votes.Any(vote => vote.voterId == state.playerId))
            { yield return "Vote for " + state.Active.First().name + " to win"; yield break; }
            yield return state.phase == EpisodePhase.Social ? "Begin the next competition"
                : state.phase == EpisodePhase.Campaign ? "Close campaigning and open voting" : "Continue episode";
        }

        /// <summary>
        /// The panel under <paramref name="rootName"/> owns the keyboard: focus starts inside it, the
        /// Down ring from that focus visits every interactable control in it and nothing else, no
        /// control outside it is reachable, and Tab — through the HUD's own handler — moves one step.
        /// </summary>
        private IEnumerator AssertKeyboardRing(string label, string rootName)
        {
            yield return null;
            var root = SceneComponents<RectTransform>()
                .FirstOrDefault(rect => rect.name == rootName && rect.gameObject.activeInHierarchy);
            // When this fails it matters a great deal whether the director thinks a panel is open -
            // a panel that never opened and a panel that opened and was torn down again are two
            // different bugs, and the bare "expected not null" cannot tell them apart.
            if (root == null)
            {
                var hud = director.GetComponentsInChildren<Canvas>(true)
                    .FirstOrDefault(canvas => canvas.name == "Gamesim Episode HUD");
                string children = hud == null ? "no HUD canvas"
                    : string.Join(", ", hud.transform.Cast<Transform>().Where(child => child.gameObject.activeSelf).Select(child => child.name));
                Assert.Fail(label + ": expected an active '" + rootName + "'. The director reports IsPanelOpen="
                    + director.IsPanelOpen + ", selection=" + (EventSystem.current.currentSelectedGameObject != null
                        ? EventSystem.current.currentSelectedGameObject.name : "none")
                    + ", the last press went " + lastSubmit
                    + ", and the HUD is carrying: " + children + ".");
            }

            // Scrollbars are not controls: the panel scrolls to the selection, and the ScrollRect
            // auto-hides them, which is exactly how one ended up inactive inside the ring once.
            var eligible = root.GetComponentsInChildren<Selectable>()
                .Where(item => item.IsActive() && item.IsInteractable() && !(item is Scrollbar)).ToList();
            Assert.That(eligible, Is.Not.Empty, label + ": a panel with nothing to press cannot be walked.");

            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null, label + ": keyboard focus must start on something.");
            Assert.That(selected.transform.IsChildOf(root), Is.True,
                label + ": focus started on '" + selected.name + "', outside the panel.");

            var start = selected.GetComponent<Selectable>();
            Assert.That(start, Is.Not.Null, label + ": the focused object must be selectable.");
            var ring = new List<Selectable> { start };
            for (int step = 0; step < eligible.Count; step++)
            {
                yield return PressKey(Key.Tab);
                var selectedAfterTab=EventSystem.current.currentSelectedGameObject;
                Assert.That(selectedAfterTab,Is.Not.Null,label+": Tab must retain a focused control.");
                var next=selectedAfterTab.GetComponent<Selectable>();
                if(next==start)break;
                ring.Add(next);
            }
            Assert.That(ring.Select(item => item.name), Is.EquivalentTo(eligible.Select(item => item.name)),
                label + ": actual Tab input must visit every control in the panel exactly once and return.");
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(start.gameObject),label+": Tab wraps to its starting control.");

            foreach (var outside in director.GetComponentsInChildren<Selectable>(true)
                .Where(item => item.IsActive() && item.IsInteractable() && !(item is Scrollbar) && !item.transform.IsChildOf(root)))
                Assert.That(outside.navigation.mode, Is.EqualTo(Navigation.Mode.None),
                    label + ": '" + outside.name + "' lies outside the panel and must not be keyboard-reachable.");

            if (eligible.Count > 1)
            {
                yield return PressKey(Key.Tab);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(ring[1].gameObject),
                    label + ": Tab must move to the next control in the ring.");
                yield return PressKey(Key.Tab, shift: true);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(start.gameObject),
                    label + ": Shift+Tab must move back.");
            }
        }

        /// <summary>Selects the control by caption and raises Submit on it — what Enter does.</summary>
        private IEnumerator KeyboardSubmit(string caption)
        {
            var target = ControlCarrying(caption);
            Assert.That(target, Is.Not.Null, "Expected a keyboard-reachable control: " + caption);
            Assert.That(target.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None), caption + " must be in the keyboard ring.");
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            yield return null;
            // Whatever carries the caption now. The HUD rebuilds itself when a houseguest's body
            // finishes assembling, which destroys the control that was selected and leaves the
            // selection pointing at it for a frame while it is already inactive - a submit sent
            // there is delivered to nothing. A player pressing Enter presses the control that is on
            // screen, so find it again and keep the keyboard on it.
            target = ControlCarrying(caption);
            Assert.That(target, Is.Not.Null, caption + " left the screen between selecting it and pressing it.");
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(target.gameObject), "Selection must hold on " + caption);
            bool delivered = ExecuteEvents.Execute(target.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            // What the press did, frame by frame. A panel that never opened and a panel that opened
            // and was shut again by the next Update are different bugs, and by the time the ring is
            // walked both look the same.
            lastSubmit = caption + " on '" + target.name + "' (delivered " + delivered + ", active " + target.IsActive()
                + ", interactable " + target.IsInteractable() + ", enabled " + target.enabled + ") -> open " + director.IsPanelOpen;
            yield return null;
            lastSubmit += ", then " + director.IsPanelOpen;
            yield return null;
            lastSubmit += ", then " + director.IsPanelOpen;
        }

        /// <summary>What the last <see cref="KeyboardSubmit"/> did to the panels, for a failure message.</summary>
        private string lastSubmit = "nothing pressed yet";

        /// <summary>The live, pressable control showing this caption, or null.</summary>
        private Button ControlCarrying(string caption) => director.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.IsActive() && button.IsInteractable()
                && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == caption));

        private IEnumerator PressKey(Key key, bool shift = false)
        {
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, shift ? new KeyboardState(key, Key.LeftShift) : new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
        }
    }
}
