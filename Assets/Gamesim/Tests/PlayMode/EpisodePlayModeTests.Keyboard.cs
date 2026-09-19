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
    /// it, the Down ring visits every interactable control and nothing else, nothing outside it is
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
            var root = director.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == rootName && rect.gameObject.activeInHierarchy);
            Assert.That(root, Is.Not.Null, label + ": expected an active '" + rootName + "'.");

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
            var cursor = start;
            for (int step = 0; step < eligible.Count + 1; step++)
            {
                var next = cursor.navigation.selectOnDown;
                if (next == null || next == start) break;
                ring.Add(next);
                cursor = next;
            }
            Assert.That(ring.Select(item => item.name), Is.EquivalentTo(eligible.Select(item => item.name)),
                label + ": the Down ring must visit every control in the panel exactly once and return.");

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
            var buttons = director.GetComponentsInChildren<Button>(true).Where(button => button.IsActive() && button.IsInteractable()
                && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == caption)).ToList();
            Assert.That(buttons, Is.Not.Empty, "Expected a keyboard-reachable control: " + caption);
            var target = buttons[0];
            Assert.That(target.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None), caption + " must be in the keyboard ring.");
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(target.gameObject), "Selection must hold on " + caption);
            ExecuteEvents.Execute(target.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null; yield return null;
        }

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
