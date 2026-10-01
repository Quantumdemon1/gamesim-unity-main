using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The nomination as a screen of steps (PACK8-PASS-PLAN B1, mockup 72): four status cards and a
    /// tracker of the week's steps in the strategy stage's header, one step's body in the column -
    /// a story beat, the Head of Household's picker, the ceremony or what came of it - and the way
    /// on pinned in the footer beside what comes next. The owner's screenshots 66 to 69 were two
    /// beats, the ceremony, the faces and the way on in one scroll; each step here holds without a
    /// scroll at both text sizes, in a house of eight and of sixteen.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Who is on the block in a nomination fixture: nobody yet, two houseguests, or the player and a houseguest.</summary>
        private enum NominationBlock { Open, Houseguests, Player }

        /// <summary>
        /// A story beat waiting at the nomination with <paramref name="options"/> options, the last of
        /// them its lapse: a beat the engine answers by option id and lapses at the nominations,
        /// drawn from its saved words because no catalogue arc is behind it.
        /// </summary>
        private static HouseEventState NominationBeat(EpisodeState state, int index, int options)
        {
            string[] labels = { "Listen to their pitch", "Counter-lobby", "Make your case", "Ask them to go to bat for you", "Offer them your safety" };
            string[] risks = { HouseEventRisk.Low, HouseEventRisk.High, HouseEventRisk.Medium, HouseEventRisk.Low, HouseEventRisk.High };
            var beat = new HouseEventState
            {
                id = "nomination-beat-" + index, kind = HouseEventKind.Story, contentId = "nomination-screen-test:beat-" + index,
                cycleId = "nomination-cycle-" + index, title = index == 0 ? "A Word Before Nominations" : "Second Thoughts",
                narrative = "Somebody catches you before the Head of Household names anybody. They want to talk about what "
                    + "happens next, and they want to do it now, before the house gathers for the ceremony.",
                week = state.week, closesAnchor = StoryAnchors.NomsSet, surface = StorySurfaces.Conversation, lapseOptionId = "not-now",
            };
            for (int i = 0; i < options - 1; i++)
                beat.choices.Add(new HouseEventChoice
                {
                    label = labels[i], optionId = "option-" + i, risk = risks[i],
                    description = "Say it plainly and see where it lands before the names are said.",
                });
            beat.choices.Add(new HouseEventChoice { label = "Not now", optionId = "not-now", description = "Let the moment pass.", lapse = true });
            return beat;
        }

        /// <summary>
        /// A story beat waiting at the nomination whose options open the views behind a press: one
        /// that names somebody, from everybody in the house but the player - the most a WHO? grid
        /// ever holds - and a rule break, which asks to be pressed twice; then its lapse.
        /// </summary>
        private static HouseEventState NominationAsk(EpisodeState state)
        {
            var beat = NominationBeat(state, 0, 1);
            beat.choices.Insert(0, new HouseEventChoice
            {
                label = "Invite someone up", optionId = "invite-one", pickPerson = true,
                description = "Pick one person to share it with. Everyone will notice who.",
                eligibleIds = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList(),
            });
            beat.choices.Insert(1, new HouseEventChoice
            {
                label = "Read the letter from home", optionId = "read-letter", conduct = true, risk = HouseEventRisk.High,
                description = "It is not yours to read, and production will see you do it.",
            });
            return beat;
        }

        /// <summary>
        /// A house of <paramref name="houseguests"/> at its nomination: a houseguest or the player at
        /// the head of the house, the block as given, the week's windows, the strategy rules and the
        /// story system on, and one waiting beat for each entry of <paramref name="beats"/> with that
        /// many options. The house's own clock is held, so nothing commits under the test.
        /// </summary>
        private IEnumerator InstallNomination(int houseguests, bool playerHoh, NominationBlock block, params int[] beats) =>
            InstallNomination(houseguests, playerHoh, block, house => Enumerable.Range(0, beats.Length).Select(i => NominationBeat(house, i, beats[i])));

        /// <summary>The same house, with the waiting beats <paramref name="beats"/> makes for it.</summary>
        private IEnumerator InstallNomination(int houseguests, bool playerHoh, NominationBlock block,
            System.Func<EpisodeState, IEnumerable<HouseEventState>> beats)
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(4401, houseguests);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.Nomination;
            state.hohId = playerHoh ? state.playerId : npcs[0];
            state.nominees = block == NominationBlock.Open ? new List<string>()
                : block == NominationBlock.Player ? new List<string> { state.playerId, npcs[1] }
                : new List<string> { npcs[1], npcs[2] };
            state.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableWeek(state, state.week);
            EpisodeEngine.EnableStory(state, state.week);
            state.houseEvents.RemoveAll(item => item.IsStory && !item.resolved);
            state.houseEvents.AddRange(beats(state).ToList());
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;
        }

        /// <summary>
        /// The nomination open now: on the strategy stage, its four status cards and its tracker in
        /// the header, one step in the column that holds without a scroll, exactly the pinned
        /// controls given, and every label on the panel drawing some of its words.
        /// </summary>
        private void AssertNominationScreen(string where, params string[] pinned)
        {
            Canvas.ForceUpdateCanvases();
            var hud = director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy), where + " takes the strategy stage.");
            var panel = ActiveRect("Episode panel");
            AssertOnTheStage(panel);
            var header = panel.Find(EpisodeHud.StrategyHeaderName);
            Assert.That(header, Is.Not.Null, where + " has its header.");
            Assert.That(header.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Status card · ")).Select(rect => rect.name),
                Is.EqualTo(new[] { "Status card · HOH", "Status card · PHASE", "Status card · CONVERSATIONS LEFT", "Status card · OBJECTIVE" }),
                where + ": the four status cards, in the mockup's order.");
            Assert.That(header.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == EpisodeHud.StepTrackerName), Is.True, where + " has its tracker.");
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                where + " holds its step without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
            var controls = panel.Cast<Transform>().Select(child => child.GetComponent<Button>())
                .Where(button => button != null && button.IsActive() && button.name != "Close  [Esc]").Select(button => button.name).ToArray();
            Assert.That(controls, Is.EquivalentTo(pinned), where + " pins " + string.Join(" and ", pinned) + ", and nothing else.");
            AssertEveryLabelDraws(panel, where);
        }

        /// <summary>The word a tracker step shows for where it stands, by the step's numbered name.</summary>
        private string StepStanding(string label)
        {
            var tracker = ActiveRect(EpisodeHud.StepTrackerName);
            Assert.That(tracker, Is.Not.Null, "The nomination has a tracker.");
            var cell = tracker.Cast<Transform>().FirstOrDefault(child => child.name == label || child.name == "Step · " + label);
            Assert.That(cell, Is.Not.Null, "The tracker has a step '" + label + "': it holds "
                + string.Join(", ", tracker.Cast<Transform>().Select(child => child.name)) + ".");
            return cell.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Step status").text;
        }

        /// <summary>What one of the nomination's status cards says, by its label.</summary>
        private string StatusValue(string label)
        {
            var card = ActiveRect("Status card · " + label);
            Assert.That(card, Is.Not.Null, "The nomination has a '" + label + "' card.");
            return card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Value").text;
        }

        /// <summary>The footer strip's words, whichever they are: what comes next, or what moving on lets pass.</summary>
        private TMP_Text FooterWords()
        {
            var strip = ActiveRect(EpisodeHud.StrategyStripName);
            Assert.That(strip, Is.Not.Null, "The footer has its strip.");
            return strip.GetComponentsInChildren<TMP_Text>().Single();
        }

        /// <summary>
        /// The outcome's footer (ACTIONS-DEALS-ALLIANCES-PLAN V2). Continuing moves the week on to the
        /// veto's window, and under the week's windows this one's seats do not carry: the fixture
        /// spent none of them, so what that costs holds the strip, outranking what comes next. What
        /// comes next keeps its words, the outcome's own.
        /// </summary>
        private void AssertOutcomeFooter(string upNext, string where)
        {
            var state = director.Snapshot;
            Assert.That(EpisodeDirector.NominationUpNext(state, state.Find(state.hohId)), Is.EqualTo(upNext), where + ": what comes next.");
            Assert.That(WaitingOnYou.AdvanceNote(state), Is.EqualTo(EpisodeEngine.AfterHoHSeats + " unused actions will be lost."),
                where + ": continuing closes the window the fixture spent none of.");
            var words = FooterWords();
            Assert.That(words.name, Is.EqualTo(EpisodeDirector.MovingOnCostsName), where + ": what moving on costs outranks what comes next.");
            Assert.That(words.text, Is.EqualTo(WaitingOnYou.AdvanceNote(state)), where);
            Assert.That(words.color, Is.EqualTo(UiTheme.Warning), where + ": in the warning colour.");
        }

        /// <summary>
        /// The ceremony before the names, in a house of eight and of sixteen at both text sizes: the
        /// Head of Household's step done and the ceremony current in the tracker, the crown on the
        /// HOH card, the house's pointer in window wording, every houseguest a face with the crown
        /// on its Head, and the way on pinned once and focused, with what comes next beside it.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_TheCeremonyFitsTheFrameInAHouseOfEightOrSixteen()
        {
            foreach (int houseguests in new[] { 8, 16 })
            {
                yield return InstallNomination(houseguests, false, NominationBlock.Open);
                var state = director.Snapshot;
                var hoh = state.Find(state.hohId);
                string pointer = EpisodeDirector.WindowLine(state);
                Assert.That(pointer, Does.EndWith("left in this window."), "Under the week's windows the count is the window's, and says so.");
                yield return AtBothTextSizes(larger =>
                {
                    string where = "The ceremony in a house of " + houseguests + (larger ? " at the larger text" : "");
                    AssertNominationScreen(where, "Continue episode");
                    Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Nomination Ceremony"), where);
                    Assert.That(StepStanding("1. " + NominationSteps.HeadOfHouseholdTitle), Is.EqualTo("Complete"), where);
                    Assert.That(StepStanding("2. " + NominationSteps.CeremonyTitle), Is.EqualTo("Current"), where);
                    Assert.That(StepStanding("3. " + NominationSteps.OutcomeTitle), Is.EqualTo("Next"), where);
                    Assert.That(StatusValue("HOH"), Is.EqualTo(hoh.name), where + ": the Head of Household's card.");
                    Assert.That(StatusValue("OBJECTIVE"), Is.EqualTo("Stay off the block"), where + ": an objective true by the rules alone.");
                    Assert.That(ShownText(), Does.Contain(pointer), where + " points the player at the Head of Household.");
                    var faces = ActiveRect(EpisodeHud.CeremonyFacesName);
                    Assert.That(faces.IsChildOf(ActiveRect("Episode content")), Is.True, where + ": the faces are the step's.");
                    foreach (var actor in state.Active)
                        Assert.That(faces.Find("Face · " + actor.name), Is.Not.Null, where + ": " + actor.name + " stands on the screen.");
                    var crown = faces.Find("Face · " + hoh.name).GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Role");
                    Assert.That(crown.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("HOH"), where + ": the crown on its Head.");
                    var next = FooterWords();
                    Assert.That(next.name, Is.EqualTo(EpisodeHud.UpNextName), where + ": nothing lapses, so the strip says what comes next.");
                    Assert.That(next.text, Does.StartWith("Up next: find out who " + hoh.name + " nominates."), where);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(FindButton("Continue episode").gameObject),
                        where + " opens on the way on: the header holds no control.");
                });
                if (Application.isBatchMode)
                {
                    yield return OpenStation();
                    yield return CaptureFraming("nomination-ceremony-" + houseguests);
                    director.ClosePanels();
                    yield return null;
                }
            }
        }

        /// <summary>
        /// A beat waiting at the nomination is the step, not a band: its words beside its tiles, three
        /// options or six, in a house of eight and of sixteen at both text sizes - inside the stage's
        /// column, every tile under its own label and on the panel, the ceremony waiting behind it,
        /// and what moving on lets pass in the footer's strip.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_AStoryBeatIsTheStepWithThreeOrSixOptions()
        {
            foreach (int houseguests in new[] { 8, 16 })
            foreach (int options in new[] { 3, 6 })
            {
                yield return InstallNomination(houseguests, false, NominationBlock.Open, options);
                var beat = EpisodeEngine.OpenStoryBeats(director.Snapshot).Single();
                yield return AtBothTextSizes(larger =>
                {
                    string where = "A beat of " + options + " options in a house of " + houseguests + (larger ? " at the larger text" : "");
                    AssertNominationScreen(where, "Continue episode");
                    Assert.That(StepStanding("2. " + beat.title), Is.EqualTo("Current"), where + ": the beat is the step.");
                    Assert.That(StepStanding("3. " + NominationSteps.CeremonyTitle), Is.EqualTo("Next"), where);
                    Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName), Is.Null, where + ": one step at a time, the ceremony behind it.");
                    var choices = ActiveRect(EpisodeHud.StoryChoicesName);
                    Assert.That(choices, Is.Not.Null, where + ": the beat's options are on the screen.");
                    Assert.That(choices.IsChildOf(ActiveRect("Episode content")), Is.True, where + ": inside the stage's column.");
                    Assert.That(choices.GetComponentsInChildren<Button>().Select(tile => tile.name),
                        Is.EqualTo(beat.choices.Select(choice => EpisodeHud.EventChoiceCaption(choice.label))), where + ": every option under its own label.");
                    var stage = ScreenRect(ActiveRect("Episode panel"));
                    foreach (var tile in choices.GetComponentsInChildren<Button>())
                        AssertInside(stage, (RectTransform)tile.transform, where + ": '" + tile.name + "'");
                    var warning = FooterWords();
                    Assert.That(warning.name, Is.EqualTo(EpisodeDirector.AdvanceWarningName), where + ": the strip warns under the name it always had.");
                    Assert.That(warning.text, Is.EqualTo("Moving on lets 1 storyline pass: " + beat.title + " (Not now)."), where);
                    Assert.That(warning.color, Is.EqualTo(UiTheme.Warning), where + ": in the warning colour.");
                });
                if (Application.isBatchMode && options == 6)
                {
                    yield return OpenStation();
                    yield return CaptureFraming("nomination-beat-" + houseguests);
                    director.ClosePanels();
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Two beats waiting at once (the owner's week 2): only the current one's tiles are live, so
        /// "Not now" is one control; the other waits on the tracker and opens from it without a
        /// commit; answering the one on screen commits once, marks it done and hands the step back;
        /// and "Continue episode" still lets the one left pass, saying so.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_OneStoryStepAtATimeAndTheOtherOpensFromTheTracker()
        {
            yield return InstallNomination(8, false, NominationBlock.Open, 3, 3);
            var beats = EpisodeEngine.OpenStoryBeats(director.Snapshot);
            Assert.That(beats, Has.Count.EqualTo(2), "The fixture has two beats waiting.");
            yield return OpenStation();
            yield return null;
            AssertNominationScreen("Two beats waiting", "Continue episode");
            Assert.That(StepStanding("1. " + beats[0].title), Is.EqualTo("Current"), "The first beat is the step.");
            Assert.That(StepStanding("2. " + beats[1].title), Is.EqualTo("Waiting"), "The other waits on the tracker.");
            Assert.That(FindButton("Not now"), Is.Not.Null, "One live 'Not now': the step's own.");

            int revision = director.Snapshot.revision;
            ButtonWithCaption("2. " + beats[1].title).onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Opening a beat from the tracker commits nothing.");
            AssertNominationScreen("The second beat opened from the tracker", "Continue episode");
            Assert.That(StepStanding("2. " + beats[1].title), Is.EqualTo("Current"), "The opened beat is the step,");
            Assert.That(StepStanding("1. " + beats[0].title), Is.EqualTo("Waiting"), "and the first waits in its place.");
            Assert.That(FindButton("Not now"), Is.Not.Null, "Still one live 'Not now'.");

            ButtonWithCaption(EpisodeHud.EventChoiceCaption(beats[1].choices[0].label)).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "Answering the beat on screen commits it once.");
            Assert.That(after.houseEvents.Single(item => item.id == beats[1].id).resolved, Is.True);
            Assert.That(after.houseEvents.Single(item => item.id == beats[0].id).resolved, Is.False, "and nothing else.");
            if (!director.IsPanelOpen) yield return OpenStation();
            yield return null;
            AssertNominationScreen("After the answer", "Continue episode");
            Assert.That(StepStanding("2. " + beats[1].title), Is.EqualTo("Complete"), "The answered story is done,");
            Assert.That(StepStanding("1. " + beats[0].title), Is.EqualTo("Current"), "and the one left is the step again.");
            var warning = FooterWords();
            Assert.That(warning.name, Is.EqualTo(EpisodeDirector.AdvanceWarningName));
            Assert.That(warning.text, Is.EqualTo("Moving on lets 1 storyline pass: " + beats[0].title + " (Not now)."),
                "Continue lets the one left pass, and says so.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The player Head of Household on the strategy stage with a story waiting (The Invite List's
        /// week): the picker is the current step, so the names and "Commit nominations" are pressed at
        /// once; the candidates are cards fitted to the step in a house of eight and of sixteen at
        /// both text sizes; the commit is pinned beside the comparison; and the strip says what
        /// committing lets pass - once a silent lapse. Then the picks survive the comparison, the story
        /// opened over the picker and the way back, and the commit names them and lets the story pass.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_TheHeadOfHouseholdPicksOnTheStageAndCommittingSaysWhatItLetsPass()
        {
            EpisodeState state = null;
            HouseEventState beat = null;
            EpisodeHud.Option[] candidates = null;
            foreach (int houseguests in new[] { 8, 16 })
            {
                yield return InstallNomination(houseguests, true, NominationBlock.Open, 3);
                state = director.Snapshot;
                beat = EpisodeEngine.OpenStoryBeats(state).Single();
                candidates = EpisodeEngine.NominationCandidates(state).Select(c => new EpisodeHud.Option(c.id, c.name)).ToArray();
                var you = state.Find(state.playerId);
                var names = candidates.Select(c => c.Label).ToArray();
                yield return AtBothTextSizes(larger =>
                {
                    string where = "The picker in a house of " + houseguests + (larger ? " at the larger text" : "");
                    AssertNominationScreen(where, "Commit nominations", EpisodeHud.ShowCandidateContextCaption);
                    Assert.That(StepStanding("2. " + NominationSteps.PickerTitle), Is.EqualTo("Current"), where + ": the picker is current while a story waits.");
                    Assert.That(StepStanding("1. " + beat.title), Is.EqualTo("Waiting"), where + ": the story waits on the tracker.");
                    var grid = ActiveRect(EpisodeHud.NomineeGridName);
                    Assert.That(grid, Is.Not.Null, where + ": the candidates are a grid.");
                    Assert.That(grid.GetComponentsInChildren<Button>().Select(card => card.name), Is.EquivalentTo(names),
                        where + ": one card per candidate, named by the candidate.");
                    var stage = ScreenRect(ActiveRect("Episode panel"));
                    foreach (var card in grid.GetComponentsInChildren<Button>())
                        AssertInside(stage, (RectTransform)card.transform, where + ": '" + card.name + "'");
                    Assert.That(FindButton("Commit nominations").transform.parent, Is.SameAs(ActiveRect("Episode panel")), where + ": the commit is pinned.");
                    var warning = FooterWords();
                    Assert.That(warning.name, Is.EqualTo(EpisodeDirector.AdvanceWarningName), where + ": the strip warns under the warning's name.");
                    Assert.That(warning.text, Is.EqualTo("Committing lets 1 storyline pass: " + beat.title + " (Not now)."), where);
                    Assert.That(StatusValue("HOH"), Is.EqualTo(HudPrimitives.WithYou(you.name, true)), where);
                    Assert.That(StatusValue("OBJECTIVE"), Is.EqualTo("Name two nominees"), where);
                    var focus = EventSystem.current.currentSelectedGameObject;
                    Assert.That(focus != null && focus.transform.IsChildOf(grid), Is.True, where + " opens on a candidate, not on the header.");
                });
                if (Application.isBatchMode)
                {
                    yield return OpenStation();
                    yield return CaptureFraming("nomination-picker-" + houseguests);
                    director.ClosePanels();
                    yield return null;
                }
            }

            // In the house of sixteen: two picks, lit in place.
            yield return OpenStation();
            yield return null;
            var first = ButtonWithCaption(candidates[0].Label);
            var second = ButtonWithCaption(candidates[1].Label);
            first.onClick.Invoke();
            second.onClick.Invoke();
            foreach (var card in new[] { first, second })
            {
                var picked = card.transform.Find("Picked");
                Assert.That(picked != null && picked.gameObject.activeSelf, Is.True, card.name + " is marked picked.");
            }
            // The comparison takes the grid's place in the step and rebuilds nothing.
            ButtonWithCaption(EpisodeHud.ShowCandidateContextCaption).onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(DecisionUiRoot(EpisodeHud.CandidateContextName), Is.Not.Null, "The comparison is up,");
            Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Null, "in the grid's place.");
            ButtonWithCaption(EpisodeHud.HideCandidateContextCaption).onClick.Invoke();
            yield return null;
            Assert.That(ButtonWithCaption(candidates[0].Label), Is.SameAs(first), "The comparison rebuilt nothing.");

            // The story opened over the picker: its step, with the way back in the footer.
            int revision = director.Snapshot.revision;
            ButtonWithCaption("1. " + beat.title).onClick.Invoke();
            yield return null;
            AssertNominationScreen("The story over the picker", EpisodeHud.BackToNomineesCaption);
            Assert.That(ActiveRect(EpisodeHud.StoryChoicesName), Is.Not.Null, "The story's options are the step.");
            Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Null, "and the picker waits behind it.");
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Going there and back commits nothing.");
            foreach (var option in candidates.Take(2))
            {
                var picked = ButtonWithCaption(option.Label).transform.Find("Picked");
                Assert.That(picked != null && picked.gameObject.activeSelf, Is.True, option.Label + " is still picked after the way back.");
            }
            Assert.That(ActiveRect(EpisodeHud.SelectedNomineesName).GetComponent<TMP_Text>().text,
                Is.EqualTo("Selected: " + candidates[0].Label + " and " + candidates[1].Label));

            // The commit names the two, and lets the story pass, as the strip said.
            ButtonWithCaption("Commit nominations").onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "The commit is one command.");
            Assert.That(after.nominees, Is.EquivalentTo(new[] { candidates[0].Id, candidates[1].Id }), "It names the two picked.");
            Assert.That(after.houseEvents.Single(item => item.id == beat.id).resolved, Is.True, "Committing let the story pass.");
        }

        /// <summary>
        /// The views a step opens behind a press hold without a scroll as the steps themselves do, in
        /// a house of sixteen - the most names any of them holds - at both text sizes: the picker's
        /// backdoor plan and its comparison, each in the grid's place, and a story's WHO? grid of
        /// everybody it can name and its rule break's confirm. Each keeps the footer its step has
        /// and a way back, and going there and back commits nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_TheViewsBehindAPressHoldWithoutAScroll()
        {
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";

                // The Head of Household's picker: the backdoor plan behind its door, and the way back.
                yield return InstallNomination(16, true, NominationBlock.Open);
                yield return ApplyTextSize(larger);
                yield return OpenStation();
                yield return null;
                var candidates = EpisodeEngine.NominationCandidates(director.Snapshot).ToList();
                int revision = director.Snapshot.revision;
                ButtonWithCaption(EpisodeHud.PlanBackdoorCaption).onClick.Invoke();
                yield return null;
                AssertNominationScreen("The backdoor plan" + size, EpisodeHud.BackToNomineesCaption);
                var aims = ActiveRect(EpisodeHud.BackdoorAimsName);
                Assert.That(aims, Is.Not.Null, "The backdoor plan" + size + " is a grid.");
                Assert.That(aims.GetComponentsInChildren<Button>().Select(aim => aim.name),
                    Is.EquivalentTo(candidates.Select(candidate => "Aim this week at " + candidate.name)), "One aim for each candidate" + size + ".");
                ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke();
                yield return null;
                AssertNominationScreen("Back on the picker" + size, "Commit nominations", EpisodeHud.ShowCandidateContextCaption);

                // Two picks compared, in the grid's place. The toggle swaps the views in place without
                // a render, so the screen is measured in the same frame, before a body finishing its
                // assembly can re-render it.
                ButtonWithCaption(candidates[0].name).onClick.Invoke();
                ButtonWithCaption(candidates[1].name).onClick.Invoke();
                ButtonWithCaption(EpisodeHud.ShowCandidateContextCaption).onClick.Invoke();
                AssertNominationScreen("The comparison" + size, "Commit nominations", EpisodeHud.HideCandidateContextCaption);
                Assert.That(ActiveRect(EpisodeHud.CandidateContextName), Is.Not.Null, "The comparison" + size + " is up,");
                Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Null, "in the grid's place.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "The picker's views commit nothing" + size + ".");
                director.ClosePanels();
                yield return null;

                // A story's WHO? and its rule break's confirm, under a houseguest at the head of the house.
                yield return InstallNomination(16, false, NominationBlock.Open, house => new[] { NominationAsk(house) });
                yield return ApplyTextSize(larger);
                yield return OpenStation();
                yield return null;
                var state = director.Snapshot;
                var beat = EpisodeEngine.OpenStoryBeats(state).Single();
                var people = beat.choices[0].eligibleIds.Select(id => state.Find(id).name).ToList();
                Assert.That(people, Has.Count.EqualTo(state.Active.Count() - 1), "The WHO? holds everybody but the player:");
                Assert.That(people.Count, Is.GreaterThanOrEqualTo(15), "fifteen names in a house of sixteen, the most a WHO? holds.");
                revision = state.revision;
                ButtonWithCaption(EpisodeHud.EventChoiceCaption(beat.choices[0].label)).onClick.Invoke();
                yield return null;
                AssertNominationScreen("The WHO? of the whole house" + size, "Continue episode");
                var grid = ActiveRect(EpisodeHud.StoryPeopleName);
                Assert.That(grid, Is.Not.Null, "Everybody it can name is a grid" + size + ".");
                Assert.That(grid.IsChildOf(ActiveRect("Episode content")), Is.True, "inside the stage's column,");
                Assert.That(grid.GetComponentsInChildren<Button>().Select(cell => cell.name), Is.EquivalentTo(people),
                    "one cell for each, named by the name" + size + ".");
                foreach (string id in beat.choices[0].eligibleIds)
                {
                    var face = grid.GetComponentsInChildren<Button>().Single(button => button.name == state.Find(id).name);
                    Assert.That(face.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Reading").text,
                        Is.EqualTo(RelationshipWeb.StandingWord(RelationshipWeb.KindOf(state, id)) + " · Trust " + state.Score(state.playerId, id).ToString("0")),
                        face.name + "'s cell keeps the player's own reading of them, as the list it replaced did" + size + ".");
                }
                ButtonWithCaption(EpisodeHud.StoryBackCaption).onClick.Invoke();
                yield return null;
                ButtonWithCaption(EpisodeHud.EventChoiceCaption(beat.choices[1].label)).onClick.Invoke();
                yield return null;
                AssertNominationScreen("The rule break's confirm" + size, "Continue episode");
                Assert.That(FindButton(EpisodeHud.StoryConfirmCaption), Is.Not.Null, "A rule break asks to be pressed twice" + size + ".");
                ButtonWithCaption(EpisodeHud.StoryBackCaption).onClick.Invoke();
                yield return null;
                Assert.That(ActiveRect(EpisodeHud.StoryChoicesName), Is.Not.Null, "The way back is the beat" + size + ",");
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "and going there and back commits nothing.");
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// What came of it, in a house of eight and of sixteen at both text sizes: "Nominated for
        /// Eviction" with the Head of Household's decision in their own pronoun, the block and the
        /// crown as faces, the ceremony done and the outcome current, and what comes next. The player
        /// on the block reads their name marked as theirs and the objective their seat gives them;
        /// the player who nominated reads their decision in the second person.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_TheOutcomeSaysWhoIsOnTheBlockAndWhoPutThemThere()
        {
            foreach (int houseguests in new[] { 8, 16 })
            foreach (var block in new[] { NominationBlock.Houseguests, NominationBlock.Player })
            {
                yield return InstallNomination(houseguests, false, block);
                var state = director.Snapshot;
                var hoh = state.Find(state.hohId);
                var you = state.Find(state.playerId);
                var nominees = state.nominees.Select(state.Find).ToList();
                string line = hoh.name + " has made " + StoryPeople.Pronouns(hoh).their + " decision. "
                    + string.Join(" and ", nominees.Select(c => HudPrimitives.WithYou(c.name, c.isPlayer))) + " are on the block.";
                yield return AtBothTextSizes(larger =>
                {
                    string where = "The outcome" + (block == NominationBlock.Player ? " with the player on the block" : "")
                        + " in a house of " + houseguests + (larger ? " at the larger text" : "");
                    AssertNominationScreen(where, "Continue episode");
                    Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Nominated for Eviction"), where);
                    Assert.That(StepStanding("2. " + NominationSteps.CeremonyTitle), Is.EqualTo("Complete"), where);
                    Assert.That(StepStanding("3. " + NominationSteps.OutcomeTitle), Is.EqualTo("Current"), where);
                    Assert.That(ShownText(), Does.Contain(line), where);
                    var faces = ActiveRect(EpisodeHud.CeremonyFacesName);
                    foreach (var nominee in nominees)
                    {
                        var pill = faces.Find("Face · " + nominee.name).GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Role");
                        Assert.That(pill.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("NOM"), where + ": " + nominee.name + " is on the block.");
                    }
                    Assert.That(faces.Find("Face · " + hoh.name), Is.Not.Null, where + ": and who put them there.");
                    if (block != NominationBlock.Player)
                    {
                        AssertOutcomeFooter("Up next: the Power of Veto player selection. The Head of Household and both nominees play by right.", where);
                        return;
                    }
                    Assert.That(StatusValue("OBJECTIVE"), Is.EqualTo("Get off the block"), where);
                    AssertOutcomeFooter("Up next: the Power of Veto player selection. Nominees play by right.", where);
                    var own = faces.Find("Face · " + you.name).Cast<Transform>().Select(child => child.GetComponent<TMP_Text>()).First(text => text != null);
                    Assert.That(own.text, Is.EqualTo(HudPrimitives.WithYou(you.name, true)), where + ": the player's face says it is theirs, and keeps the name it is found by.");
                });
            }

            // The player's own nominations, said in the second person.
            yield return InstallNomination(8, true, NominationBlock.Houseguests);
            yield return OpenStation();
            yield return null;
            AssertNominationScreen("The player's own nominations", "Continue episode");
            Assert.That(ShownText(), Does.Contain("You have made your decision."));
            Assert.That(StepStanding("2. " + NominationSteps.PickerTitle), Is.EqualTo("Complete"));
            Assert.That(StepStanding("4. " + NominationSteps.OutcomeTitle), Is.EqualTo("Current"));
            director.ClosePanels();
            yield return null;
        }
    }
}
