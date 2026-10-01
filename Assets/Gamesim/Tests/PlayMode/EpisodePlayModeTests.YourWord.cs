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
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Your word (ACTIONS-DEALS-ALLIANCES-PLAN V1): the notebook page of every commitment the player
    /// is a party to, and the warnings the decision screens give before a choice breaks one - the
    /// nomination picker's footer, the veto decision's strip, the ballot's line, the diary's reviews
    /// and the final choice's column. Each warning is there exactly when the dry run says the choice
    /// breaks something and not otherwise; drawing it commits nothing and draws nothing from the
    /// season's stream; and every caption the walks press is pressed here, and does what the
    /// warning said.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static string WordFirst(EpisodeState state, string id) => FinalistRead.FirstName(state.Find(id).name);

        private static string WordThem(EpisodeState state, string id) => StoryPeople.Pronouns(state.Find(id)).them;

        /// <summary>A deal as the engine writes one, this week's; a weekly kind's term is this week unless <paramref name="expires"/> says otherwise.</summary>
        private static DealState WordDeal(EpisodeState state, string id, string type, string proposer, string recipient, string status = DealStatus.Active,
            int expires = -1) => new DealState
        {
            id = id, type = type, proposerId = proposer, recipientId = recipient, status = status, week = state.week,
            expiresWeek = expires >= 0 ? expires : state.week, trustImpact = DealKind.DefaultTrust(type),
        };

        /// <summary>A promise the player gave this week, standing until next week.</summary>
        private static PromiseState WordPromise(EpisodeState state, string id, PromiseKind kind, string to, string about = null) => new PromiseState
        {
            id = id, kind = kind, fromId = state.playerId, toId = to, targetId = about, status = PromiseStatus.Active,
            week = state.week, expiresWeek = state.week + 1,
        };

        /// <summary>The player's loyalty declaration to a houseguest as the engine records one: the oath, its milestone and the note on the player's edge.</summary>
        private static void WordOath(EpisodeState state, string npcId)
        {
            if (!state.shownOathMilestones.Contains(npcId)) state.shownOathMilestones.Add(npcId);
            state.loyaltyOaths.Add(new WebOathRecord { playerId = state.playerId, targetId = npcId, week = state.week, timestamp = state.nextSequence++ });
            state.relationships.Single(edge => edge.fromId == state.playerId && edge.toId == npcId).notes.Add("loyalty-oath");
        }

        /// <summary>A breach strip's words, by the name every breach warning carries.</summary>
        private static string StripWords(RectTransform strip) =>
            strip.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.BreachWarningName).text;

        // ---------------------------------------------------------------- the page

        /// <summary>
        /// The page: a deal and a promise of the player's, each in a card under its houseguest with
        /// its line as the reader says it, a kept deal under SETTLED, and nothing of a deal between
        /// two houseguests. Its door is a button in the notes page's head, never a filter among the
        /// filters, and its way back is a door in its own head; at both text sizes each door clears
        /// Close and says all of its caption, and every label draws its words.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_ThePageListsASeededDealAndPromiseAndNothingOfAnybodyElses()
        {
            HoldTheHouseForTheFixture();
            string dealWith = null, promisedTo = null, kept = null, outsider = null;
            yield return InstallStrategySeason(61, state =>
            {
                var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
                dealWith = npcs[0]; promisedTo = npcs[1]; kept = npcs[2]; outsider = npcs[3];
                state.deals.Add(WordDeal(state, "deal-your-word", DealKind.SafetyAgreement, state.playerId, dealWith));
                state.deals.Add(WordDeal(state, "deal-kept-word", DealKind.Partnership, kept, state.playerId, DealStatus.Fulfilled, 0));
                state.deals.Add(WordDeal(state, "deal-between-others", DealKind.FinalTwo, outsider, npcs[4], DealStatus.Active, 0));
                state.promises.Add(new PromiseState
                {
                    id = "promise-your-word", kind = PromiseKind.Safety, fromId = state.playerId, toId = promisedTo,
                    status = PromiseStatus.Active, week = state.week, expiresWeek = state.week + 1,
                });
            });
            HoldTheHouseForTheFixture();
            yield return SettleCast();
            var seeded = director.Snapshot;
            var read = CommitmentsRead.Of(seeded);
            Assert.That(read.Select(c => c.id), Is.EquivalentTo(new[] { "promise-your-word", "deal-your-word", "deal-kept-word" }),
                "The reader holds the player's own word and nothing between two houseguests.");

            foreach (bool larger in new[] { false, true })
            {
                if (larger)
                {
                    director.ClosePanels();
                    yield return ApplyTextSize(true);
                }
                string size = larger ? " at the larger text" : "";

                // The door is in the notes page's head, where "Notebook [J]" opens.
                yield return OpenNotebook();
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes));
                AssertPageDoor(EpisodeDirector.YourWordCaption, "The notes page" + size);
                var filters = ActiveRect("Notes filters");
                Assert.That(filters == null || !FindButton(EpisodeDirector.YourWordCaption).transform.IsChildOf(filters), Is.True,
                    "Your word is a page of its own, never a filter among the notes' filters" + size + ".");
                ButtonWithCaption(EpisodeDirector.YourWordCaption).onClick.Invoke();
                yield return null; yield return null;
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), "The notes page opens Your word" + size + ".");

                string where = "Your word" + size;
                Canvas.ForceUpdateCanvases();
                Assert.That(NotebookText(), Does.Contain("Your word"), where + " names itself.");
                Assert.That(ActiveRect(EpisodeDirector.NotebookSection.Word), Is.Not.Null, where + " carries its mark.");
                AssertPageDoor(EpisodeDirector.BackToYourNotesCaption, where);
                var dealCard = ActiveRect(EpisodeHud.WordOpenCardPrefix + seeded.Find(dealWith).name);
                Assert.That(dealCard, Is.Not.Null, where + ": the deal's partner has a card under STILL OPEN.");
                Assert.That(Words(dealCard), Does.Contain(CommitmentsRead.Line(read.Single(c => c.id == "deal-your-word"))), where + ": the deal's line.");
                var promiseCard = ActiveRect(EpisodeHud.WordOpenCardPrefix + seeded.Find(promisedTo).name);
                Assert.That(promiseCard, Is.Not.Null, where + ": so does the one promised.");
                Assert.That(Words(promiseCard), Does.Contain(CommitmentsRead.Line(read.Single(c => c.id == "promise-your-word"))), where + ": the promise's line.");
                var keptCard = ActiveRect(EpisodeHud.WordSettledCardPrefix + seeded.Find(kept).name);
                Assert.That(keptCard, Is.Not.Null, where + ": a kept deal is settled.");
                Assert.That(Words(keptCard), Does.Contain(CommitmentsRead.Line(read.Single(c => c.id == "deal-kept-word"))));
                Assert.That(ScreenRect(dealCard).yMin, Is.GreaterThanOrEqualTo(ScreenRect(keptCard).yMax - 1f), where + ": the open ones come first.");
                foreach (var name in new[] { EpisodeHud.WordOpenCardPrefix, EpisodeHud.WordSettledCardPrefix })
                    Assert.That(ActiveRect(name + seeded.Find(outsider).name), Is.Null, where + ": a deal between two houseguests is theirs.");
                var panel = ActiveRect("Episode panel");
                AssertEveryLabelDraws(panel, where);
                foreach (var card in new[] { dealCard, promiseCard, keptCard })
                    foreach (var label in card.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing, Is.False, where + ": '" + label.text + "' fits its card.");
                    }
                if (!larger && Application.isBatchMode) yield return CaptureFraming("your-word-page");

                ButtonWithCaption(EpisodeDirector.BackToYourNotesCaption).onClick.Invoke();
                yield return null; yield return null;
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "and back to the notes" + size + ".");
            }
            director.ClosePanels();
            yield return null;
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// A door in the notebook page's head: a control under its own caption, a child of the head
        /// and not of the page's column, clear of Close, inside the panel, and saying all its words.
        /// </summary>
        private void AssertPageDoor(string caption, string where)
        {
            Canvas.ForceUpdateCanvases();
            var door = (RectTransform)FindButton(caption).transform;
            var head = ActiveRect(EpisodeHud.NotebookHeaderName);
            Assert.That(head, Is.Not.Null, where + " has its head.");
            Assert.That(door.IsChildOf(head), Is.True, where + ": '" + caption + "' is a door in the page's head,");
            Assert.That(door.IsChildOf(ActiveRect("Episode content")), Is.False, "not a row of the page.");
            var close = ActiveRect("Close  [Esc]");
            if (close != null) Assert.That(ScreenRect(door).Overlaps(ScreenRect(close)), Is.False, where + ": '" + caption + "' clears Close.");
            AssertInside(ScreenRect(ActiveRect("Episode panel")), door, where + ": '" + caption + "'");
            foreach (var label in door.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, where + ": '" + label.text + "' fits its door.");
            }
            AssertEveryLabelDraws(door, where + "'s door");
        }

        // ---------------------------------------------------------------- the nomination picker

        /// <summary>
        /// A house of eight at its nomination with the player at its head and a safety deal with one
        /// of the candidates: the strategy rules and the week's windows on, the house held. With
        /// <paramref name="beatOptions"/>, a story beat waits too, which committing lets pass.
        /// </summary>
        private IEnumerator InstallYourWordNomination(string dealId, int beatOptions = 0)
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(4401, 8);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.Nomination;
            state.hohId = state.playerId;
            state.nominees = new List<string>();
            state.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableWeek(state, state.week);
            if (beatOptions > 0)
            {
                // As the nomination screen's own fixture waits a beat (EpisodePlayModeTests.NominationScreen).
                EpisodeEngine.EnableStory(state, state.week);
                state.houseEvents.RemoveAll(item => item.IsStory && !item.resolved);
                state.houseEvents.Add(NominationBeat(state, 0, beatOptions));
            }
            state.deals.Add(WordDeal(state, dealId, DealKind.SafetyAgreement, state.playerId, npcs[0]));
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;
        }

        /// <summary>
        /// The picker's footer warns exactly when the two picked would break the player's word: not
        /// with nothing picked, not with one, not with two that break nothing; with the deal's partner
        /// picked, the dry run's words in the warning colour, in place of what comes next, which
        /// comes back when the pick is undone. A render keeps the picks and the warning. Nothing of
        /// it commits or draws from the season's stream, and "Commit nominations" breaks the deal as
        /// the warning said.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_TheNominationPickerWarnsExactlyWhenThePicksWouldBreakYourWord()
        {
            yield return InstallYourWordNomination("deal-safe");
            yield return OpenStation();
            yield return null;
            var before = director.Snapshot;
            string safe = before.deals.Single(d => d.id == "deal-safe").recipientId;
            var others = EpisodeEngine.NominationCandidates(before).Where(c => c.id != safe).ToList();
            AssertNominationScreen("The picker with a deal at stake", "Commit nominations", EpisodeHud.ShowCandidateContextCaption);
            var resting = FooterWords();
            string restingName = resting.name, restingText = resting.text;
            var restingColour = resting.color;
            Assert.That(restingName, Is.Not.EqualTo(EpisodeHud.BreachWarningName), "Nothing picked, nothing warned of.");

            ButtonWithCaption(others[0].name).onClick.Invoke();
            Assert.That(FooterWords().name, Is.EqualTo(restingName), "One pick is not a nomination: nothing is warned of.");
            ButtonWithCaption(others[1].name).onClick.Invoke();
            Assert.That(CommitmentsRead.WouldBreak(director.Snapshot, CommitmentsRead.Decision.Nominate(others[0].id, others[1].id)), Is.Empty);
            Assert.That((FooterWords().name, FooterWords().text), Is.EqualTo((restingName, restingText)), "Two picks that break nothing are not warned of.");

            // Swap the second for the deal's partner.
            ButtonWithCaption(others[1].name).onClick.Invoke();
            ButtonWithCaption(before.Find(safe).name).onClick.Invoke();
            string expected = CommitmentsRead.Warning(director.Snapshot,
                CommitmentsRead.WouldBreak(director.Snapshot, CommitmentsRead.Decision.Nominate(others[0].id, safe)));
            Assert.That(expected, Is.EqualTo("Nominating " + WordFirst(before, safe) + " breaks your safety deal with " + WordThem(before, safe) + "."));
            var warning = FooterWords();
            Assert.That(warning.name, Is.EqualTo(EpisodeHud.BreachWarningName), "The strip warns, under the warning's own name,");
            Assert.That(warning.text, Is.EqualTo(expected), "in the dry run's words,");
            Assert.That(warning.color, Is.EqualTo(UiTheme.Warning), "in the warning colour.");
            Assert.That(ActiveRect(EpisodeHud.StrategyStripName).GetComponentsInChildren<Button>(), Is.Empty, "A warning is a label, never a control.");
            AssertEveryLabelDraws(ActiveRect(EpisodeHud.StrategyStripName), "The breach warning");
            var now = director.Snapshot;
            Assert.That((now.revision, now.randomState), Is.EqualTo((before.revision, before.randomState)),
                "Drawing the warning commits nothing and draws nothing from the season's stream.");

            // A render keeps the picks, and the warning with them.
            RenderHudForTheCurrentCanvas();
            Assert.That((FooterWords().name, FooterWords().text), Is.EqualTo((EpisodeHud.BreachWarningName, expected)), "A render keeps the warning.");
            if (Application.isBatchMode) yield return CaptureFraming("breach-warning-nomination");

            // Undoing the pick gives the strip back what it said.
            ButtonWithCaption(before.Find(safe).name).onClick.Invoke();
            Assert.That((FooterWords().name, FooterWords().text), Is.EqualTo((restingName, restingText)), "Unpicked, the strip says what it said.");
            Assert.That(FooterWords().color, Is.EqualTo(restingColour), "in the colour it said it in.");

            // The walk's caption still commits, and breaks what the warning said it would.
            ButtonWithCaption(before.Find(safe).name).onClick.Invoke();
            int revision = director.Snapshot.revision;
            ButtonWithCaption("Commit nominations").onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "One command.");
            Assert.That(after.nominees, Is.EquivalentTo(new[] { others[0].id, safe }));
            Assert.That(after.deals.Single(d => d.id == "deal-safe").status, Is.EqualTo(DealStatus.Broken), "The commit broke the deal the warning named.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// With a story waiting, the picker's strip already warns what committing lets pass, and a
        /// breach never takes that away: picking the deal's partner makes the one warning line say
        /// the breach first and then what committing lets pass, under the name that warning has
        /// always carried - or, where the two cannot share the strip, the short form that counts
        /// both - and nothing overflows, at both text sizes. Unpicked, the line is the lapse alone.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_ThePickerSaysTheBreachThenWhatCommittingLetsPassInTheOneWarningLine()
        {
            foreach (bool larger in new[] { false, true })
            {
                yield return InstallYourWordNomination("deal-lapse", 3);
                yield return ApplyTextSize(larger);
                yield return OpenStation();
                yield return null;
                string where = "The picker with a story waiting" + (larger ? " at the larger text" : "");
                var state = director.Snapshot;
                var beat = EpisodeEngine.OpenStoryBeats(state).Single();
                string lapse = "Committing lets 1 storyline pass: " + beat.title + " (Not now).";
                var resting = FooterWords();
                Assert.That((resting.name, resting.text), Is.EqualTo((EpisodeDirector.AdvanceWarningName, lapse)),
                    where + ": before a pick the strip says what committing lets pass.");

                string safe = state.deals.Single(d => d.id == "deal-lapse").recipientId;
                var other = EpisodeEngine.NominationCandidates(state).First(c => c.id != safe);
                ButtonWithCaption(other.name).onClick.Invoke();
                ButtonWithCaption(state.Find(safe).name).onClick.Invoke();
                string breach = CommitmentsRead.Warning(state, CommitmentsRead.WouldBreak(state, CommitmentsRead.Decision.Nominate(other.id, safe)));
                Assert.That(breach, Is.EqualTo("Nominating " + WordFirst(state, safe) + " breaks your safety deal with " + WordThem(state, safe) + "."));
                string brief = "Committing these breaks 1 of your commitments (see " + EpisodeDirector.YourWordCaption + ") and lets 1 storyline pass.";

                Canvas.ForceUpdateCanvases();
                var strip = ActiveRect(EpisodeHud.StrategyStripName);
                var line = strip.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeDirector.AdvanceWarningName);
                Assert.That(line, Is.SameAs(FooterWords()), where + ": one warning line, under the name it is found by,");
                Assert.That(line.text == breach + " " + lapse || line.text == brief, Is.True,
                    where + ": the breach first and then what committing lets pass, or the short form of both - not '" + line.text + "'.");
                Assert.That(line.color, Is.EqualTo(UiTheme.Warning), where + ": in the warning colour.");
                line.ForceMeshUpdate();
                Assert.That(line.isTextOverflowing, Is.False, where + ": '" + line.text + "' fits the strip.");
                AssertEveryLabelDraws(strip, where + "'s strip");
                var onward = ScreenRect((RectTransform)FindButton("Commit nominations").transform);
                Assert.That(ScreenRect(strip).Overlaps(onward), Is.False, where + ": the strip and the commit share the row without meeting.");
                if (!larger && Application.isBatchMode) yield return CaptureFraming("breach-warning-nomination-lapse");

                ButtonWithCaption(state.Find(safe).name).onClick.Invoke();
                Assert.That((FooterWords().name, FooterWords().text, FooterWords().color), Is.EqualTo((EpisodeDirector.AdvanceWarningName, lapse, UiTheme.Warning)),
                    where + ": unpicked, the line is what committing lets pass, as it was.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision), where + ": picking commits nothing.");
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        // ---------------------------------------------------------------- the veto

        /// <summary>
        /// The veto decision warns exactly when a choice breaks the player's word: with no deal at
        /// stake, no strip; with a nominee's veto ask accepted, a strip that says saving the other
        /// or keeping the block breaks it, measured into the step so it still fits at both text
        /// sizes, and the walks' captions are all still there. Keeping the block breaks the deal.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_TheVetoDecisionWarnsExactlyWhenAChoiceWouldBreakYourWord()
        {
            yield return InstallStrategySeason(43, state => AtVetoMeeting(state, true));
            HoldTheHouseForTheFixture();
            yield return OpenStation();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.BreachStripName), Is.Null, "Nothing at stake, nothing warned of.");
            director.ClosePanels();
            yield return null;

            yield return InstallStrategySeason(43, state =>
            {
                AtVetoMeeting(state, true);
                state.deals.Add(WordDeal(state, "deal-veto-word", DealKind.VetoUse, state.nominees[0], state.playerId));
            });
            HoldTheHouseForTheFixture();
            var seeded = director.Snapshot;
            string partner = seeded.nominees[0], other = seeded.nominees[1];
            var choices = new[]
            {
                CommitmentsRead.Decision.Veto(true, partner), CommitmentsRead.Decision.Veto(true, other), CommitmentsRead.Decision.Veto(false),
            };
            string expected = CommitmentsRead.Warning(seeded, choices.SelectMany(choice => CommitmentsRead.WouldBreak(seeded, choice)));
            Assert.That(expected, Is.EqualTo("Saving " + WordFirst(seeded, other) + " or not using the veto breaks your veto deal with "
                + WordFirst(seeded, partner) + "."));
            yield return AtBothTextSizes(larger =>
            {
                string where = "The veto decision with a deal at stake" + (larger ? " at the larger text" : "");
                AssertTheMeetingFits(where);
                var strip = ActiveRect(EpisodeHud.BreachStripName);
                Assert.That(strip, Is.Not.Null, where + " warns.");
                Assert.That(StripWords(strip), Is.EqualTo(expected), where + ": the dry run's words.");
                Assert.That(strip.GetComponentsInChildren<Button>(), Is.Empty, "A warning is a label, never a control.");
                foreach (var nominee in seeded.nominees)
                    Assert.That(FindButton("Save " + seeded.Find(nominee).name + " (HoH chooses replacement)"), Is.Not.Null, where + ": the save is still there.");
                Assert.That(FindButton("Do not use the veto"), Is.Not.Null, where + ": and keeping the block.");
                var panel = ActiveRect("Episode panel");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Reading the warning decides nothing.");
            Assert.That((director.Snapshot.revision, director.Snapshot.randomState), Is.EqualTo((seeded.revision, seeded.randomState)),
                "Drawing the warning at both text sizes commits nothing and draws nothing from the season's stream.");

            yield return OpenStation();
            yield return null;
            if (Application.isBatchMode) yield return CaptureFraming("breach-warning-veto");
            int revision = director.Snapshot.revision;
            ButtonWithCaption("Do not use the veto").onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1));
            Assert.That(after.vetoResolved, Is.True);
            Assert.That(after.deals.Single(d => d.id == "deal-veto-word").status, Is.EqualTo(DealStatus.Broken),
                "Keeping the block broke the deal, as the warning said.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The player as Head of Household naming the replacement for a houseguest's save: the strip
        /// stands over the names and says which of them would break the player's word - their own
        /// nomination, never the holder's choice - at both text sizes, inside a step that still fits,
        /// with every name still there; and pressing that name breaks the deal, as it said.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_TheHeadOfHouseholdNamingAReplacementForAHouseguestsSaveIsWarnedOfTheNameThatBreaksTheirWord()
        {
            string holder = null, named = null;
            yield return InstallStrategySeason(51, state =>
            {
                AtVetoMeeting(state, false);
                state.hohId = state.playerId;
                holder = state.vetoHolderId = state.nominees[0];
                named = EpisodeEngine.ReplacementCandidates(state).First().id;
                state.deals.Add(WordDeal(state, "deal-named", DealKind.SafetyAgreement, state.playerId, named));
            });
            HoldTheHouseForTheFixture();
            var before = director.Snapshot;
            Assert.That(EpisodeEngine.NpcVetoSave(before), Is.EqualTo(holder), "Precondition: a holder on the block saves themselves.");
            var candidates = EpisodeEngine.ReplacementCandidates(before).Select(c => c.id).ToList();
            string expected = CommitmentsRead.Warning(before,
                candidates.SelectMany(id => CommitmentsRead.WouldBreak(before, CommitmentsRead.Decision.Veto(true, holder, id))));
            Assert.That(expected, Is.EqualTo("Naming " + WordFirst(before, named) + " as the replacement breaks your safety deal with " + WordThem(before, named) + "."));
            yield return AtBothTextSizes(larger =>
            {
                string where = "Naming the replacement with a deal at stake" + (larger ? " at the larger text" : "");
                AssertTheMeetingFits(where);
                var strip = ActiveRect(EpisodeHud.BreachStripName);
                Assert.That(strip, Is.Not.Null, where + " warns.");
                Assert.That(StripWords(strip), Is.EqualTo(expected), where + ": the dry run's words.");
                Assert.That(strip.GetComponentsInChildren<Button>(), Is.Empty, "A warning is a label, never a control.");
                foreach (var id in candidates)
                {
                    var name = (RectTransform)FindButton(before.Find(id).name).transform;
                    Assert.That(ScreenRect(name).yMax, Is.LessThanOrEqualTo(ScreenRect(strip).yMin + .5f), where + ": " + before.Find(id).name + " is offered under the warning.");
                }
                var panel = ActiveRect("Episode panel");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            Assert.That((director.Snapshot.revision, director.Snapshot.randomState), Is.EqualTo((before.revision, before.randomState)),
                "Drawing the warning commits nothing and draws nothing from the season's stream.");

            yield return OpenStation();
            yield return null;
            ButtonWithCaption(before.Find(named).name).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.vetoResolved, Is.True, "The name is the decision.");
            Assert.That(after.nominees, Does.Contain(named));
            Assert.That(after.deals.Single(d => d.id == "deal-named").status, Is.EqualTo(DealStatus.Broken), "Naming them broke the deal, as the warning said.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The player is Head of Household and holder: the strip under the rule names each
        /// replacement that would break the player's word - a deal with one, a promise to another,
        /// a sentence each - whichever nominee they pick to save, at both text sizes in a step that
        /// still fits. Picking the other nominee commits nothing and keeps the warning, and naming
        /// the one promised breaks that promise and nothing else.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_TheHeadOfHouseholdHoldingTheVetoIsWarnedOfEachReplacementWhicheverNomineeIsSaved()
        {
            string dealWith = null, promisedTo = null;
            yield return InstallStrategySeason(50, state =>
            {
                AtVetoMeeting(state, true);
                state.hohId = state.playerId;
                var candidates = EpisodeEngine.ReplacementCandidates(state).Select(c => c.id).ToList();
                dealWith = candidates[0]; promisedTo = candidates[1];
                state.deals.Add(WordDeal(state, "deal-replace", DealKind.SafetyAgreement, dealWith, state.playerId));
                state.promises.Add(WordPromise(state, "promise-replace", PromiseKind.Safety, promisedTo));
            });
            HoldTheHouseForTheFixture();
            var before = director.Snapshot;
            var ids = EpisodeEngine.ReplacementCandidates(before).Select(c => c.id).ToList();
            string Expected(string saved) => CommitmentsRead.Warning(before,
                ids.SelectMany(id => CommitmentsRead.WouldBreak(before, CommitmentsRead.Decision.Veto(true, saved, id)))
                    .Concat(CommitmentsRead.WouldBreak(before, CommitmentsRead.Decision.Veto(false))));
            string expected = Expected(before.nominees[0]);
            Assert.That(Expected(before.nominees[1]), Is.EqualTo(expected), "Whom a name would break is the same whichever nominee is saved.");
            Assert.That(expected, Is.EqualTo(
                "Naming " + WordFirst(before, dealWith) + " as the replacement breaks your safety deal with " + WordThem(before, dealWith) + ". "
                + "Naming " + WordFirst(before, promisedTo) + " as the replacement breaks your promise of safety to " + WordThem(before, promisedTo) + "."));
            yield return AtBothTextSizes(larger =>
            {
                string where = "The Head of Household's veto decision with a word at stake" + (larger ? " at the larger text" : "");
                AssertTheMeetingFits(where);
                var strip = ActiveRect(EpisodeHud.BreachStripName);
                Assert.That(strip, Is.Not.Null, where + " warns.");
                Assert.That(StripWords(strip), Is.EqualTo(expected), where + ": a sentence for each name that breaks something.");
                var rule = ActiveRect(EpisodeHud.MeetingInfoStripName);
                Assert.That(ScreenRect(strip).yMax, Is.LessThanOrEqualTo(ScreenRect(rule).yMin + .5f), where + ": under the rule,");
                foreach (var id in ids)
                    Assert.That(ScreenRect((RectTransform)FindButton(before.Find(id).name).transform).yMax, Is.LessThanOrEqualTo(ScreenRect(strip).yMin + .5f),
                        where + ": and over " + before.Find(id).name + ".");
                foreach (var nominee in before.nominees)
                    Assert.That(FindButton(EpisodeDirector.VetoSavePickCaption(before.Find(nominee).name)), Is.Not.Null, where + ": each nominee can still be picked.");
                Assert.That(FindButton("Do not use the veto"), Is.Not.Null, where + ": and the block kept.");
                var panel = ActiveRect("Episode panel");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });

            yield return OpenStation();
            yield return null;
            ButtonWithCaption(EpisodeDirector.VetoSavePickCaption(before.Find(before.nominees[1]).name)).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Picking whom to save decides nothing.");
            Assert.That(StripWords(ActiveRect(EpisodeHud.BreachStripName)), Is.EqualTo(expected), "The other nominee picked, the same names warned of.");
            if (Application.isBatchMode) yield return CaptureFraming("breach-warning-veto-replacement");
            ButtonWithCaption(before.Find(promisedTo).name).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.vetoResolved, Is.True, "A name uses the veto.");
            Assert.That(after.nominees, Is.EquivalentTo(new[] { before.nominees[0], promisedTo }), "on the nominee picked, and puts the name up.");
            Assert.That(after.promises.Single(p => p.id == "promise-replace").status, Is.EqualTo(PromiseStatus.Broken), "Naming them broke the promise, as the warning said,");
            Assert.That(after.deals.Single(d => d.id == "deal-replace").status, Is.EqualTo(DealStatus.Active), "and nothing it did not name.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The largest house, the player Head of Household and holder with their word given to three
        /// who could go up: the strip names all three and, with the column at its top, stands in
        /// the scroll's window under the rule with the decision above it, at both text sizes -
        /// thirteen names still listed under it, each once - and no label on the panel runs past
        /// its box.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_InTheLargestHouseTheReplacementStripStaysInViewAtBothTextSizes()
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(53, EpisodeValidation.MaximumCast);
            AtVetoMeeting(state, true);
            state.hohId = state.playerId;
            var ids = EpisodeEngine.ReplacementCandidates(state).Select(c => c.id).ToList();
            state.deals.Add(WordDeal(state, "deal-largest", DealKind.SafetyAgreement, ids[0], state.playerId));
            state.promises.Add(WordPromise(state, "promise-largest", PromiseKind.Safety, ids[1]));
            WordOath(state, ids[2]);
            Assert.That(EpisodeValidation.TryValidate(state, out var invalid), Is.True, invalid);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            var before = director.Snapshot;
            Assert.That(ids, Has.Count.EqualTo(EpisodeValidation.MaximumCast - 3), "Everybody but the player and the block can go up.");
            string expected = CommitmentsRead.Warning(before,
                ids.SelectMany(id => CommitmentsRead.WouldBreak(before, CommitmentsRead.Decision.Veto(true, before.nominees[0], id))));
            Assert.That(ids.Take(3).All(id => expected.Contains("Naming " + WordFirst(before, id) + " as the replacement breaks ")), Is.True,
                "Three names, a sentence each: " + expected);
            yield return AtBothTextSizes(larger =>
            {
                string where = "The strip in a house of " + EpisodeValidation.MaximumCast + (larger ? " at the larger text" : "");
                var content = ActiveRect("Episode content");
                content.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
                var window = ScreenRect((RectTransform)content.parent);
                var strip = ActiveRect(EpisodeHud.BreachStripName);
                Assert.That(strip, Is.Not.Null, where + " warns.");
                Assert.That(StripWords(strip), Is.EqualTo(expected), where + ": the dry run's words.");
                AssertInside(window, ActiveRect(EpisodeHud.CeremonyTitleName), where + ": the title");
                AssertInside(window, (RectTransform)FindButton("Do not use the veto").transform, where + ": 'Do not use the veto'");
                AssertInside(window, ActiveRect(EpisodeHud.MeetingInfoStripName), where + ": the rule");
                AssertInside(window, strip, where + ": the warning");
                foreach (var id in ids)
                {
                    var offered = director.GetComponentsInChildren<Button>(true).Where(button => button.IsActive()
                        && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == before.Find(id).name)).ToArray();
                    Assert.That(offered, Has.Length.EqualTo(1), where + " offers " + before.Find(id).name + " once.");
                    Assert.That(ScreenRect((RectTransform)offered[0].transform).yMax, Is.LessThanOrEqualTo(ScreenRect(strip).yMin + .5f),
                        where + " lists " + before.Find(id).name + " under the warning.");
                }
                var panel = ActiveRect("Episode panel");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Reading the warning decides nothing.");
        }

        // ---------------------------------------------------------------- the ballot

        /// <summary>
        /// The ballot warns of the vote that would break the player's word, before either card is
        /// pressed and over the cards: a promise to vote out one nominee makes the other card the one
        /// named. Pressing the card the promise names casts the ballot and breaks nothing; and with
        /// no vote promised, no warning.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_TheBallotWarnsExactlyWhenAVoteWouldBreakYourWord()
        {
            string keep = null, evict = null, promisedTo = null;
            System.Action<EpisodeState> atTheVote = state =>
            {
                var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
                state.phase = EpisodePhase.Eviction;
                state.evictionStage = EvictionStage.Voting;
                state.hohId = npcs[0];
                state.nominees = new List<string> { npcs[1], npcs[2] };
                state.vetoHolderId = npcs[3];
                state.vetoResolved = true;
                state.vetoPlayers = state.Active.Select(actor => actor.id).Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).ToList();
                keep = npcs[1]; evict = npcs[2]; promisedTo = npcs[3];
            };
            yield return InstallStrategySeason(45, atTheVote);
            HoldTheHouseForTheFixture();
            yield return OpenStation();
            yield return null;
            Assert.That(FindButton("Vote to evict " + director.Snapshot.Find(keep).name), Is.Not.Null, "The ballot is on the episode screen.");
            Assert.That(ActiveRect(EpisodeHud.BreachWarningName), Is.Null, "No vote promised, nothing warned of.");
            director.ClosePanels();
            yield return null;

            yield return InstallStrategySeason(45, state =>
            {
                atTheVote(state);
                state.promises.Add(new PromiseState
                {
                    id = "promise-vote-word", kind = PromiseKind.Vote, fromId = state.playerId, toId = promisedTo, targetId = evict,
                    status = PromiseStatus.Active, week = state.week, expiresWeek = state.week,
                });
            });
            HoldTheHouseForTheFixture();
            var installed = director.Snapshot;
            yield return OpenStation();
            yield return null;
            var state = director.Snapshot;
            string expected = CommitmentsRead.Warning(state, state.nominees.SelectMany(id => CommitmentsRead.WouldBreak(state, CommitmentsRead.Decision.Vote(id))));
            Assert.That(expected, Is.EqualTo("Voting to evict " + WordFirst(state, keep) + " breaks your promise to " + WordFirst(state, promisedTo)
                + " to vote out " + WordFirst(state, evict) + "."));
            Canvas.ForceUpdateCanvases();
            var warning = ActiveRect(EpisodeHud.BreachWarningName);
            Assert.That(warning, Is.Not.Null, "The ballot warns before a card is pressed.");
            Assert.That(warning.GetComponent<TMP_Text>().text, Is.EqualTo(expected));
            Assert.That(warning.GetComponent<TMP_Text>().color, Is.EqualTo(UiTheme.Warning));
            Assert.That(warning.GetComponentInParent<Button>(), Is.Null, "A warning is a label, never a control.");
            Assert.That(ScreenRect(warning).yMin, Is.GreaterThanOrEqualTo(ScreenRect(LastActive(EpisodeHud.BallotRowName)).yMax - .5f),
                "It stands over the cards it is about.");
            foreach (var nominee in state.nominees)
                Assert.That(FindButton("Vote to evict " + state.Find(nominee).name), Is.Not.Null, "Both cards are still there.");
            AssertEveryLabelDraws(ActiveRect("Episode panel"), "The ballot with a vote promised");
            Assert.That((director.Snapshot.revision, director.Snapshot.randomState), Is.EqualTo((installed.revision, installed.randomState)),
                "Drawing the warning commits nothing and draws nothing from the season's stream.");
            if (Application.isBatchMode) yield return CaptureFraming("breach-warning-ballot");

            int revision = director.Snapshot.revision;
            ButtonWithCaption("Vote to evict " + state.Find(evict).name).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "One ballot.");
            Assert.That(after.votes.Single(vote => vote.voterId == after.playerId).targetId, Is.EqualTo(evict));
            Assert.That(after.promises.Single(p => p.id == "promise-vote-word").status, Is.EqualTo(PromiseStatus.Active),
                "A ballot is judged at the reveal; voting as promised is not a breach.");
            director.ClosePanels();
            yield return null;
        }

        // ---------------------------------------------------------------- the diary

        /// <summary>A bounded legal season walked to where <paramref name="reached"/> holds, shaped, saved and loaded: the decisions on it are real ones.</summary>
        private IEnumerator InstallWalkedYourWord(System.Func<EpisodeState, bool> reached, System.Action<EpisodeState> shape, string description)
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 120 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 150; guard++)
                {
                    var current = engine.Snapshot;
                    if (reached(current)) { fixture = current; break; }
                    if (current.phase == EpisodePhase.Finished) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture: " + description + ".");
            shape(fixture);
            Assert.That(EpisodeValidation.TryValidate(fixture, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
        }

        /// <summary>The review's warning: its words, in the warning colour, a label and not a control, over the Confirm a walk presses.</summary>
        private TMP_Text AssertWarnedOverConfirm(string where, string expected)
        {
            Canvas.ForceUpdateCanvases();
            var warning = LastActive(EpisodeHud.BreachWarningName);
            Assert.That(warning, Is.Not.Null, where + " warns.");
            var label = warning.GetComponent<TMP_Text>();
            Assert.That(label.text, Is.EqualTo(expected), where + ": the dry run's words.");
            Assert.That(label.color, Is.EqualTo(UiTheme.Warning), where + ": in the warning colour.");
            Assert.That(warning.GetComponentInParent<Button>(), Is.Null, where + ": a label, never a control.");
            var confirm = ScreenRect((RectTransform)FindButton(EpisodeHud.DiaryConfirmCaption).transform);
            Assert.That(ScreenRect(warning).yMin, Is.GreaterThanOrEqualTo(confirm.yMax - .5f), where + ": over Confirm.");
            return label;
        }

        /// <summary>
        /// The diary's reviews say what the decision under review would break, over Confirm: the
        /// Head of Household's nominations, the holder keeping the block, and a ballot - whose
        /// warning stands over the cards with Confirm still straight under them and in view.
        /// Reviewing and going back commits nothing; confirming breaks what the review said.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_TheDiaryReviewsSayWhatTheDecisionWouldBreakOverConfirm()
        {
            // The Head of Household's nominations under review.
            yield return InstallWalkedYourWord(s => s.phase == EpisodePhase.Nomination && s.hohId == s.playerId && s.nominees.Count == 0,
                s => s.deals.Add(WordDeal(s, "deal-diary-safe", DealKind.SafetyAgreement, s.playerId, EpisodeEngine.NominationCandidates(s).First().id)),
                "player HoH nominations");
            var before = director.Snapshot;
            var picks = EpisodeEngine.NominationCandidates(before).Take(2).Select(c => c.id).ToArray();
            string expected = CommitmentsRead.Warning(before, CommitmentsRead.WouldBreak(before, CommitmentsRead.Decision.Nominate(picks[0], picks[1])));
            Assert.That(expected, Does.Contain("Nominating " + WordFirst(before, picks[0]) + " breaks your safety deal with"), "The fixture's nomination breaks the deal.");
            yield return OpenDiaryFixturePanel();
            int revision = director.Snapshot.revision;
            yield return ReviewDiaryNominations(before);
            AssertWarnedOverConfirm("The nominations under review", expected);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reviewing commits nothing.");
            ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.deals.Single(d => d.id == "deal-diary-safe").status, Is.EqualTo(DealStatus.Broken),
                "Confirming broke what the review said it would.");
            director.ClosePanels();
            yield return null;

            // The holder keeping the block under review: a nominee - never the player, who can hold
            // it from the block - asked for the veto and was told yes.
            string asked = null;
            yield return InstallWalkedYourWord(s => s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.vetoHolderId == s.playerId,
                s =>
                {
                    asked = s.nominees.First(id => id != s.playerId);
                    s.deals.Add(WordDeal(s, "deal-diary-veto", DealKind.VetoUse, asked, s.playerId));
                }, "player veto holder");
            before = director.Snapshot;
            expected = CommitmentsRead.Warning(before, CommitmentsRead.WouldBreak(before, CommitmentsRead.Decision.Veto(false)));
            Assert.That(expected, Does.Contain("ot using the veto breaks your veto deal with " + WordFirst(before, asked)));
            yield return OpenDiaryFixturePanel();
            revision = director.Snapshot.revision;
            ButtonWithCaption("Do not use the veto").onClick.Invoke();
            yield return null; yield return null;
            AssertWarnedOverConfirm("Keeping the block under review", expected);
            ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reviewing and going back commits nothing.");
            director.ClosePanels();
            yield return null;

            // A ballot under review: over the cards, and Confirm still straight under them, in view.
            yield return InstallWalkedYourWord(s => s.phase == EpisodePhase.Eviction && !s.evictionResolved && s.evictionStage == EvictionStage.Voting
                    && EpisodeEngine.Voters(s).Any(voter => voter.isPlayer) && !s.votes.Any(vote => vote.voterId == s.playerId),
                // Promised to another voter or, where the player votes alone at four, to the Head of Household.
                s => s.promises.Add(WordPromise(s, "promise-diary-vote", PromiseKind.Vote,
                    EpisodeEngine.Voters(s).Where(voter => !voter.isPlayer).Select(voter => voter.id).DefaultIfEmpty(s.hohId).First(), s.nominees[1])),
                "eligible private eviction ballot");
            before = director.Snapshot;
            string evict = before.nominees[0];
            expected = CommitmentsRead.Warning(before, CommitmentsRead.WouldBreak(before, CommitmentsRead.Decision.Vote(evict)));
            Assert.That(expected, Does.Contain("Voting to evict " + WordFirst(before, evict) + " breaks your promise to"));
            yield return OpenDiaryFixturePanel();
            revision = director.Snapshot.revision;
            Assert.That(ActiveRect(EpisodeHud.BreachWarningName), Is.Null, "In the diary a card stages the choice: nothing is warned of until one is chosen.");
            ButtonWithCaption("Vote to evict " + before.Find(evict).name).onClick.Invoke();
            yield return null; yield return null;
            var warning = AssertWarnedOverConfirm("The ballot under review", expected);
            var cards = ScreenRect(LastActive(EpisodeHud.BallotRowName));
            var confirm = ScreenRect((RectTransform)FindButton(EpisodeHud.DiaryConfirmCaption).transform);
            Assert.That(ScreenRect(warning.rectTransform).yMin, Is.GreaterThanOrEqualTo(cards.yMax - .5f), "over the cards it is about,");
            Assert.That(cards.yMin - confirm.yMax, Is.LessThan(confirm.height), "with Confirm still straight under the cards,");
            var viewport = ScreenRect((RectTransform)LastActive("Episode content").parent);
            Assert.That(confirm.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - 1f), "and in view, not past the fold.");
            if (Application.isBatchMode) yield return CaptureFraming("breach-warning-diary-ballot");
            ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reviewing and going back commits nothing.");
            director.ClosePanels();
            yield return null;
        }

        // ---------------------------------------------------------------- the final choice

        /// <summary>A bounded legal season walked to its final eviction with the player as the final Head of Household, shaped, saved and loaded.</summary>
        private IEnumerator InstallYourWordFinalChoice(System.Action<EpisodeState> shape) =>
            InstallWalkedYourWord(s => s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId, shape,
                "the player as the final Head of Household");

        /// <summary>
        /// The final choice warns in the column whose control breaks the player's word, and only
        /// there: a Final 2 deal with one finalist is broken by taking the other. The columns stay
        /// level side by side, and "Evict {partner}" - the walks' caption - breaks the deal as the
        /// warning said.
        /// </summary>
        [UnityTest]
        public IEnumerator YourWord_TheFinalChoiceWarnsOverTheControlThatWouldBreakYourWord()
        {
            HoldTheHouseForTheFixture();
            string partner = null, other = null;
            yield return InstallYourWordFinalChoice(state =>
            {
                var finalists = FinalistRead.Others(state);
                partner = finalists[0].id; other = finalists[1].id;
                state.deals.Add(WordDeal(state, "deal-final-word", DealKind.FinalTwo, partner, state.playerId, DealStatus.Active, 0));
            });
            HoldTheHouseForTheFixture();
            yield return PutAwayTheCards();
            var seeded = director.Snapshot;
            string expected = CommitmentsRead.Warning(seeded, CommitmentsRead.WouldBreak(seeded, CommitmentsRead.Decision.FinalEviction(partner)));
            Assert.That(expected, Is.EqualTo("Taking " + WordFirst(seeded, other) + " to the Final 2 breaks your final two deal with " + WordFirst(seeded, partner) + "."));
            Assert.That(CommitmentsRead.WouldBreak(seeded, CommitmentsRead.Decision.FinalEviction(other)), Is.Empty, "Taking the partner keeps it.");

            yield return OpenFinalePanel();
            yield return Frames(3);
            Canvas.ForceUpdateCanvases();
            var breaking = LastActive("Finalist column · " + seeded.Find(other).name);
            var keeping = LastActive("Finalist column · " + seeded.Find(partner).name);
            Assert.That(breaking.GetComponentsInChildren<TMP_Text>().Where(label => label.name == EpisodeHud.BreachWarningName).Select(label => label.text),
                Is.EqualTo(new[] { expected }), "The column whose control breaks the deal says so.");
            Assert.That(keeping.GetComponentsInChildren<TMP_Text>().Any(label => label.name == EpisodeHud.BreachWarningName), Is.False,
                "The column that keeps it says nothing.");
            var breakingControl = (RectTransform)FindButton("Evict " + seeded.Find(partner).name).transform;
            var keepingControl = (RectTransform)FindButton("Evict " + seeded.Find(other).name).transform;
            Assert.That(breakingControl.IsChildOf(breaking), Is.True, "The warning stands in the column of the control it is about.");
            if (ScreenRect(breaking).xMin >= ScreenRect(keeping).xMax - 1f || ScreenRect(keeping).xMin >= ScreenRect(breaking).xMax - 1f)
            {
                Assert.That(Mathf.Abs(ScreenRect(breakingControl).yMin - ScreenRect(keepingControl).yMin), Is.LessThan(1f),
                    "Side by side, the two controls stay level with the warning over one of them.");
                foreach (var part in new[] { EpisodeHud.FinalistHeadlineName, EpisodeHud.FinalistWarningName })
                {
                    float a = ScreenRect(breaking.GetComponentsInChildren<TMP_Text>().Single(label => label.name == part).rectTransform).yMin;
                    float b = ScreenRect(keeping.GetComponentsInChildren<TMP_Text>().Single(label => label.name == part).rectTransform).yMin;
                    Assert.That(Mathf.Abs(a - b), Is.LessThan(1f), "The " + part + " lines still share a baseline.");
                }
            }
            AssertEveryLabelDraws(LastActive("Episode panel"), "The final choice with a deal at stake");
            Assert.That(director.Snapshot.revision, Is.EqualTo(seeded.revision), "Reading the warning commits nothing.");
            if (Application.isBatchMode) yield return CaptureFraming("breach-warning-final-choice");

            ButtonWithCaption("Evict " + seeded.Find(partner).name).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(seeded.revision + 1), "One command.");
            Assert.That(after.deals.Single(d => d.id == "deal-final-word").status, Is.EqualTo(DealStatus.Broken),
                "Taking the other finalist broke the deal, as the warning said.");
            director.ClosePanels();
            yield return null;
        }
    }
}
