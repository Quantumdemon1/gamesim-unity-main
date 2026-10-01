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
    /// nomination picker's footer, the veto decision's strip, the ballot's line and the final
    /// choice's column. Each warning is there exactly when the dry run says the choice breaks
    /// something and not otherwise; drawing it commits nothing and draws nothing from the season's
    /// stream; and every caption the walks press is pressed here, and does what the warning said.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static string WordFirst(EpisodeState state, string id) => FinalistRead.FirstName(state.Find(id).name);

        /// <summary>A deal as the engine writes one, this week's; a weekly kind's term is this week unless <paramref name="expires"/> says otherwise.</summary>
        private static DealState WordDeal(EpisodeState state, string id, string type, string proposer, string recipient, string status = DealStatus.Active,
            int expires = -1) => new DealState
        {
            id = id, type = type, proposerId = proposer, recipientId = recipient, status = status, week = state.week,
            expiresWeek = expires >= 0 ? expires : state.week, trustImpact = DealKind.DefaultTrust(type),
        };

        /// <summary>
        /// The page: a deal and a promise of the player's, each in a card under its houseguest with
        /// its line as the reader says it, a kept deal under SETTLED, and nothing of a deal between
        /// two houseguests. Reached from the notes page and back, at both text sizes, every label
        /// drawing its words.
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

            // The door is on the notes page, where "Notebook [J]" opens.
            yield return OpenNotebook();
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes));
            ButtonWithCaption(EpisodeDirector.YourWordCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), "The notes page opens Your word.");

            foreach (bool larger in new[] { false, true })
            {
                if (larger)
                {
                    director.ClosePanels();
                    yield return ApplyTextSize(true);
                    director.ShowNotebookSection(EpisodeDirector.NotebookSection.Word);
                    yield return null; yield return null;
                }
                string where = "Your word" + (larger ? " at the larger text" : "");
                Canvas.ForceUpdateCanvases();
                Assert.That(NotebookText(), Does.Contain("Your word"), where + " names itself.");
                Assert.That(ActiveRect(EpisodeDirector.NotebookSection.Word), Is.Not.Null, where + " carries its mark.");
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
            }

            ButtonWithCaption(EpisodeDirector.BackToYourNotesCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "and back to the notes.");
            director.ClosePanels();
            yield return null;
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// A house of eight at its nomination with the player at its head and a safety deal with one
        /// of the candidates: the strategy rules and the week's windows on, the house held.
        /// </summary>
        private IEnumerator InstallYourWordNomination(string dealId)
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(4401, 8);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.Nomination;
            state.hohId = state.playerId;
            state.nominees = new List<string>();
            state.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableWeek(state, state.week);
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
        /// picked, the dry run's words in the warning colour, in place of what the strip said, which
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
            Assert.That(expected, Is.EqualTo("Nominating " + WordFirst(before, safe) + " breaks your safety deal with "
                + StoryPeople.Pronouns(before.Find(safe)).them + "."));
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
                Assert.That(strip.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.BreachWarningName).text,
                    Is.EqualTo(expected), where + ": the dry run's words.");
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
        /// The ballot warns of the vote that would break the player's word, before either card is
        /// pressed: a promise to vote out one nominee makes the other card the one named. Pressing the
        /// card the promise names casts the ballot and breaks nothing; and with no vote promised, no
        /// warning.
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
            var warning = ActiveRect(EpisodeHud.BreachWarningName);
            Assert.That(warning, Is.Not.Null, "The ballot warns before a card is pressed.");
            Assert.That(warning.GetComponent<TMP_Text>().text, Is.EqualTo(expected));
            Assert.That(warning.GetComponent<TMP_Text>().color, Is.EqualTo(UiTheme.Warning));
            Assert.That(warning.GetComponentInParent<Button>(), Is.Null, "A warning is a label, never a control.");
            foreach (var nominee in state.nominees)
                Assert.That(FindButton("Vote to evict " + state.Find(nominee).name), Is.Not.Null, "Both cards are still there.");
            AssertEveryLabelDraws(ActiveRect("Episode panel"), "The ballot with a vote promised");
            Assert.That((director.Snapshot.revision, director.Snapshot.randomState), Is.EqualTo((installed.revision, installed.randomState)),
                "Drawing the warning commits nothing and draws nothing from the season's stream.");

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

        /// <summary>A bounded legal season walked to its final eviction with the player as the final Head of Household, shaped, saved and loaded.</summary>
        private IEnumerator InstallYourWordFinalChoice(System.Action<EpisodeState> shape)
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 120 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 150; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.FinalEviction && current.hohId == current.playerId) { fixture = current; break; }
                    if (current.phase == EpisodePhase.Finished) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture with the player as the final Head of Household.");
            shape(fixture);
            Assert.That(EpisodeValidation.TryValidate(fixture, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
        }

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
