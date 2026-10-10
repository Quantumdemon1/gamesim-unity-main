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
    /// Vote family V7: the vote family's pages in a season the unified vote rules govern (mode 2, every fresh season since V6),
    /// each with its mode-1 twin - a season recorded before V6 keeps mode 1 and must read as it did. Every fixture is a fresh
    /// season as the director starts one (<see cref="ShippedRules.ApplyFresh"/>; the twin set back to mode 1 before its first
    /// command), walked by public commands - the player's own word on the vote and a vote deal among them - and loaded as a
    /// player loads a save; nothing is manufactured. The Your word page lists the word as the record holds it, a vote deal the
    /// partner's hidden ballot settled unresolved, and the week's recap tells it the same way; the jury flow asks about the word
    /// the player broke, its receipt over the question in the record's words, and the jury house reads the jury. Captions are
    /// the screens' own; nothing here adds or renames one. The twins' words are held to each other: the page reads as before.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The page in both modes: a vote promise and a vote deal standing in a campaign, then settled by the reveal.</summary>
        [UnityTest]
        public IEnumerator UnifiedVotePages_TheYourWordPageListsTheWordAndKeepsAHiddenBallotUnresolved([Values(2, 1)] int mode)
        {
            HoldTheHouseForTheFixture();
            var fixture = UvpWordFixture(mode);
            var twin = UvpWordFixture(3 - mode);
            Assert.That(twin.Seed, Is.EqualTo(fixture.Seed), "Fixture: the twin plays the same season.");
            foreach (var pair in new[] { (fixture.Campaign, twin.Campaign), (fixture.Settled, twin.Settled) })
            {
                Assert.That(CommitmentsRead.Of(pair.Item1).Select(CommitmentsRead.Line), Is.EqualTo(CommitmentsRead.Of(pair.Item2).Select(CommitmentsRead.Line)),
                    "The page's lines read as the twin's.");
                Assert.That(YourWeek.Build(pair.Item1, fixture.Week).word.Select(line => line.ToString()),
                    Is.EqualTo(YourWeek.Build(pair.Item2, fixture.Week).word.Select(line => line.ToString())), "The week's word reads as the twin's.");
            }

            // The campaign: the word and the deal stand, under STILL OPEN.
            yield return UvpInstall(fixture.Campaign);
            UvpAssertWhereTheWordLives(director.Snapshot, mode, fixture.PromiseId, fixture.DealId);
            var standing = CommitmentsRead.Of(director.Snapshot);
            Assert.That(standing.Single(c => c.id == fixture.PromiseId).IsOpen && standing.Single(c => c.id == fixture.DealId).IsOpen, Is.True,
                "Fixture: both stand.");
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                string where = "Mode " + mode + ", the campaign" + (larger ? " at the larger text" : "");
                yield return UvpOpenYourWord(where);
                UvpAssertCard(EpisodeHud.WordOpenCardPrefix, fixture.PromisedId, standing.Single(c => c.id == fixture.PromiseId), where);
                UvpAssertCard(EpisodeHud.WordOpenCardPrefix, fixture.PartnerId, standing.Single(c => c.id == fixture.DealId), where);
                AssertEveryLabelDraws(ActiveRect("Episode panel"), where);
                if (!larger && Application.isBatchMode) yield return CaptureFraming("unified-vote-your-word-open-mode-" + mode);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // The next week: the reveal kept the word and settled the deal by a ballot the player cannot know.
            yield return UvpInstall(fixture.Settled);
            var state = director.Snapshot;
            UvpAssertWhereTheWordLives(state, mode, fixture.PromiseId, fixture.DealId);
            var settled = CommitmentsRead.Of(state);
            var word = settled.Single(c => c.id == fixture.PromiseId);
            var deal = settled.Single(c => c.id == fixture.DealId);
            Assert.That((word.outcome, word.status), Is.EqualTo((CommitmentsRead.Outcomes.Kept, "kept")), "The player's own ballot kept their word.");
            Assert.That((deal.outcome, deal.status, deal.brokenById), Is.EqualTo((CommitmentsRead.Outcomes.Unresolved, KnownBallots.Unresolved, (string)null)),
                "The partner's hidden ballot settled the deal: unresolved, blaming nobody.");
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                string where = "Mode " + mode + ", settled" + (larger ? " at the larger text" : "");
                yield return UvpOpenYourWord(where);
                UvpAssertCard(EpisodeHud.WordSettledCardPrefix, fixture.PromisedId, word, where);
                var card = UvpAssertCard(EpisodeHud.WordSettledCardPrefix, fixture.PartnerId, deal, where);
                string cardWords = Words(card);
                Assert.That(cardWords, Does.Not.Contain("broken by").And.Not.Contain("honoured"), where + ": the card tells nothing of the hidden ballot.");
                var line = card.GetComponentsInChildren<TMP_Text>().Single(label => label.text == CommitmentsRead.Line(deal));
                var ink = EpisodeDirector.WordInk(CommitmentsRead.Outcomes.Unresolved);
                Assert.That(new Vector3(line.color.r - ink.r, line.color.g - ink.g, line.color.b - ink.b).magnitude, Is.LessThan(0.01f), where + ": in the unresolved ink.");
                AssertEveryLabelDraws(ActiveRect("Episode panel"), where);
                if (!larger && Application.isBatchMode) yield return CaptureFraming("unified-vote-your-word-settled-mode-" + mode);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // The week's recap, looked back on: Your week tells the word and the deal as the page does.
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var mine = YourWeek.Build(state, fixture.Week);
            Assert.That(mine.word.Select(line => (line.aboutId, line.verdict)), Is.EquivalentTo(new[]
                { (fixture.PartnerId, YourWeek.Verdicts.NotKnown), (fixture.PromisedId, YourWeek.Verdicts.Kept) }), "Fixture: the week's word, one line a row.");
            screen.Review(state, fixture.Week);
            yield return null;
            ButtonWithCaption(WeeklyRecapScreen.YourWeekCaption).onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var words = LabelsUnder(LastActive(WeeklyRecapScreen.TabBodyName));
            foreach (var line in mine.word)
            {
                Assert.That(words, Does.Contain(line.text), "Mode " + mode + ": the recap's Your week tells " + line.text);
                Assert.That(words, Does.Contain(WeeklyRecapScreen.VerdictWord(line.verdict)), line.text);
            }
            Assert.That(LastActive(WeeklyRecapScreen.WordName).GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains("broke the vote-to-evict")
                || label.text.Contains("kept the vote-to-evict")), Is.False, "Mode " + mode + ": nor does the recap tell the hidden ballot.");
            screen.Hide();
            yield return null;
        }

        /// <summary>
        /// The log and the record in both modes (vote family V7): the player proposed a vote to evict and kept it, and the partner's
        /// ballot - which the player cannot know - kept it too. The reveal writes the keep to the player's log and their record of
        /// the partner, a Rule2 incident's owner's line, and its words tell that ballot. In mode 2 the story so far, the relationship
        /// web's history of the partner and the week's recap leave it out while the Your word page reads the deal unresolved; the
        /// mode-1 twin tells it on each, as its recorded seasons did.
        /// </summary>
        [UnityTest]
        public IEnumerator UnifiedVotePages_TheLogAndTheRecordKeepAHiddenBallotsKeepToThemselves([Values(2, 1)] int mode)
        {
            HoldTheHouseForTheFixture();
            var fixture = UvpWordFixture(mode, keptDeal: true);
            var twin = UvpWordFixture(3 - mode, keptDeal: true);
            Assert.That(twin.Seed, Is.EqualTo(fixture.Seed), "Fixture: the twin plays the same season.");
            var settled = fixture.Settled;
            string partner = settled.Find(fixture.PartnerId).name;
            string keep = settled.Find(settled.playerId).name + " honoured a " + DealKind.Title(DealKind.VoteEvict).ToLowerInvariant() + " with " + partner + ".";
            Assert.That(settled.events.Any(e => e.week == fixture.Week && e.kind == "deal-outcome" && e.text == keep && e.audienceIds.Contains(settled.playerId)),
                Is.True, "Fixture: the reveal wrote the keep to the player's log.");
            Assert.That(KnownBallots.Knows(settled, fixture.Week, fixture.PartnerId), Is.False, "Fixture: the partner's ballot is hidden.");
            Assert.That(CommitmentsRead.Of(settled).Single(c => c.id == fixture.DealId).outcome, Is.EqualTo(CommitmentsRead.Outcomes.Unresolved),
                "Fixture: the Your word page reads the deal unresolved.");
            bool tells = mode == 1;
            string told = tells ? "tells the keep, as before" : "leaves the keep out";

            yield return UvpInstall(settled);
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(VisibleLines().Any(line => line.Contains(keep)), Is.EqualTo(tells), "Mode " + mode + ": the story so far " + told + ".");
            director.ClosePanels();
            yield return null;

            RelationshipWeb.ClearSelection();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return Frames(2);
            Under(Section(), RelationshipWeb.GraphName).GetComponentsInChildren<Button>(true)
                .Single(node => node.name == RelationshipWeb.NodeName(partner)).onClick.Invoke();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(RelationshipWeb.Selected, Is.EqualTo(fixture.PartnerId), "Fixture: the web reads the partner.");
            string column = Copy(Under(Section(), RelationshipWeb.ColumnName));
            Assert.That(column, Does.Contain(RelationshipWeb.HistoryHeading), "Fixture: the partner's history is on screen.");
            Assert.That(column.Contains(keep), Is.EqualTo(tells), "Mode " + mode + ": the web's history " + told + ".");
            director.ClosePanels();
            RelationshipWeb.ClearSelection();
            yield return null;

            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            screen.Review(director.Snapshot, fixture.Week);
            yield return null;
            var recap = new List<string>();
            foreach (string tab in WeeklyRecapScreen.TabCaptions)
            {
                ButtonWithCaption(tab).onClick.Invoke();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                recap.AddRange(VisibleLines());
            }
            Assert.That(recap.Any(line => line.Contains(keep)), Is.EqualTo(tells), "Mode " + mode + ": the week's recap " + told + ".");
            screen.Hide();
            yield return null;
        }

        /// <summary>
        /// The jury flow in both modes: a juror the player gave their word to on the vote and broke it asks about it at the
        /// finale. The receipt over the question is the record's - the canonical Vote row in mode 2, the raw promise in mode 1 -
        /// in the same words; the jury house reads the jury; the answer is the one commit, and the receipt stays with it.
        /// </summary>
        [UnityTest]
        public IEnumerator UnifiedVotePages_TheJuryAsksAboutTheWordThePlayerBrokeInTheRecordsWords([Values(2, 1)] int mode)
        {
            HoldTheHouseForTheFixture();
            var fixture = UvpJuryFixture(mode, out uint seed);
            var twin = UvpJuryFixture(3 - mode, out uint twinSeed);
            Assert.That(twinSeed, Is.EqualTo(seed), "Fixture: the twin plays the same season.");
            var asked = fixture.juryExchanges[fixture.juryQuestionIndex];
            var twinAsked = twin.juryExchanges[twin.juryQuestionIndex];
            Assert.That(new[] { asked.category, asked.question, FinaleQuestions.Kicker(fixture, asked), FinaleQuestions.ReceiptLine(fixture, asked) },
                Is.EqualTo(new[] { twinAsked.category, twinAsked.question, FinaleQuestions.Kicker(twin, twinAsked), FinaleQuestions.ReceiptLine(twin, twinAsked) }),
                "The juror asks the twin's question from the twin's receipt, in its words.");

            yield return UvpInstall(fixture);
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            var exchange = state.juryExchanges[state.juryQuestionIndex];
            Assert.That((exchange.finalistId, exchange.category, exchange.receiptKind), Is.EqualTo((state.playerId, FinaleQuestions.Accountability, FinaleQuestions.PromiseReceipt)),
                "Fixture: the player is asked about a promise they broke.");
            var receipt = CommitmentReferences.FindPromise(state, exchange.receiptId);
            Assert.That((receipt.kind, receipt.status, receipt.fromId, receipt.toId), Is.EqualTo((PromiseKind.Vote, PromiseStatus.Broken, state.playerId, exchange.questionerId)),
                "Fixture: the player's own word on the vote to the juror, broken.");
            var row = CommitmentReferences.FindCanonical(state, exchange.receiptId);
            if (mode == 2)
            {
                Assert.That((row?.kind, row?.status), Is.EqualTo((UnifiedVoteTogether.Vote, DealStatus.Broken)), "Mode 2: the word is a canonical Vote row,");
                Assert.That(UnifiedVoteHistory.Incidents(state).Any(i => i.OwnerId == row.id && i.ActorId == state.playerId && i.WrongedId == exchange.questionerId),
                    Is.True, "the owner of the player's breach of the juror.");
                Assert.That(state.promises.Any(p => p.id == exchange.receiptId), Is.False, "No raw mirror.");
            }
            else
            {
                Assert.That(row, Is.Null, "Mode 1: the word is the raw promise.");
                Assert.That(state.promises.Any(p => p.id == exchange.receiptId), Is.True);
            }
            string kicker = FinaleQuestions.Kicker(state, exchange);
            Assert.That(kicker, Does.EndWith("you gave them your word, and broke it"));

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                string where = "Mode " + mode + (larger ? " at the larger text" : "");
                Assert.That(LastActive("Jury question").GetComponent<TMP_Text>().text, Is.EqualTo(exchange.question), where + ": the saved question, word for word.");
                var over = LastActive(EpisodeHud.JuryKickerName);
                Assert.That(over?.GetComponent<TMP_Text>().text, Is.EqualTo(kicker), where + ": the receipt's kicker over the question.");
                Assert.That(over.IsChildOf(LastActive(EpisodeHud.JuryQuestionPanelName)), Is.True, where + ": in the question's panel.");
                Assert.That(LastActive(EpisodeHud.JuryReceiptLineName)?.GetComponent<TMP_Text>().text, Is.EqualTo(FinaleQuestions.ReceiptLine(state, exchange)),
                    where + ": the receipt line, in the record's words.");
                var grid = LastActive(EpisodeHud.JuryResponsesName);
                foreach (var response in FinaleQuestions.Offered(exchange.category, exchange.receiptKind))
                    Assert.That(ButtonWithCaption(FinaleQuestions.Caption(response)).transform.IsChildOf(grid), Is.True, where + ": " + response);
                AssertDecisionCopyFits(LastActive(EpisodeHud.JuryReceiptColumnName));
                AssertDecisionCopyFits(LastActive(EpisodeHud.JuryAskerColumnName));
                if (!larger && Application.isBatchMode) yield return CaptureFraming("unified-vote-jury-receipt-mode-" + mode, settle: false);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // The jury house, from the questioning's own door: observe only, the jury as the read gives it.
            yield return OpenFinalePanel();
            var beforeTheHouse = director.Snapshot;
            ButtonWithCaption(EpisodeDirector.JuryHouseCaption).onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertTheJuryHouse(beforeTheHouse);
            UvEquivalent(beforeTheHouse, director.Snapshot);
            ButtonWithCaption(EpisodeDirector.LeaveJuryHouseCaption).onClick.Invoke();
            yield return null; yield return null;

            // The answer is the one commit; the receipt stays with the question it was asked from.
            yield return OpenFinalePanel();
            ButtonWithCaption(FinaleQuestions.Caption(FinaleQuestions.Own)).onClick.Invoke();
            yield return Frames(2);
            var after = director.Snapshot;
            var answered = after.juryExchanges[after.juryQuestionIndex];
            Assert.That((answered.answerChoice, answered.receiptKind, answered.receiptId), Is.EqualTo((FinaleQuestions.Own, exchange.receiptKind, exchange.receiptId)));
            Assert.That(LastActive(EpisodeHud.JuryReactionName)?.GetComponent<TMP_Text>().text, Is.EqualTo(after.events.Last(e => e.kind == "jury-answer").text),
                "The engine's own note.");
            Assert.That(FinaleQuestions.Kicker(after, answered), Is.EqualTo(kicker), "The receipt reads the same after the answer.");
        }

        // ------------------------------------------------------------ fixtures

        private sealed class UvpWord
        {
            internal EpisodeState Campaign, Settled;
            internal string PromiseId, DealId, PromisedId, PartnerId;
            internal int Week;
            internal uint Seed;
        }

        /// <summary>A fresh season as the director starts one, in <paramref name="mode"/>: mode 2, or set back to mode 1 before its first command.</summary>
        private static EpisodeState UvpFresh(uint seed, int mode)
        {
            var s = ContentCatalog.Create(seed);
            ShippedRules.ApplyFresh(s);
            if (mode == 1) s.unifiedCommitmentRulesVersion = 1;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }

        private static EpisodeCommand UvpCommand(EpisodeState s, EpisodeCommandKind kind, string target, string second = null, string text = null) => new EpisodeCommand
        {
            id = "unified-vote-pages-" + kind + "-" + s.revision + "-" + target, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind, targetId = target, secondTargetId = second, text = text,
        };

        /// <summary>The walk's command: <see cref="NextCommand"/>, a finalist answering a question with a response it offers under the finale rules.</summary>
        private static EpisodeCommand UvpNext(EpisodeState s)
        {
            var command = NextCommand(s);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(s))
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex];
                if (exchange.finalistId == s.playerId) command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0];
            }
            return command;
        }

        /// <summary>The seeds a fixture searches: the one the lane's probe found first, then the rest in order.</summary>
        private static IEnumerable<uint> UvpSeeds(uint first, int last) =>
            new[] { first }.Concat(Enumerable.Range(1, last).Select(n => (uint)n)).Distinct();

        /// <summary>
        /// The page's season: the first campaign the player votes in with two actions left, where they give a voter their word to
        /// evict the first nominee and strike a vote-to-evict deal on the same nominee with another (agreed by the season's own
        /// draw); then the walk to the next week's campaign, past a reveal where the player's ballot kept the word and the
        /// partner's ballot - which the player cannot know - settled the deal. With <paramref name="keptDeal"/>, a season where
        /// that ballot kept the deal: the player's keep then tells it (vote family V7's gate on the record and the log).
        /// </summary>
        private static UvpWord UvpWordFixture(int mode, bool keptDeal = false)
        {
            foreach (uint seed in keptDeal ? UvpSeeds(350, 400) : UvpSeeds(23, 40))
            {
                var engine = new EpisodeEngine(UvpFresh(seed, mode));
                EpisodeState s = null;
                for (int guard = 0; guard < 600 && s == null; guard++)
                {
                    var now = engine.Snapshot;
                    if (now.phase == EpisodePhase.Campaign && now.pendingDiary == null && UvPlayerVotes(now)
                        && EpisodeEngine.Voters(now).Count(c => !c.isPlayer) >= 2
                        && EpisodeEngine.SocialActionsSpent(now) + 2 <= EpisodeEngine.SocialActionBudget(now)) s = now;
                    else if (now.phase == EpisodePhase.Finished || now.Find(now.playerId).status != ContestantStatus.Active || !engine.Apply(UvpNext(now)).accepted) break;
                }
                if (s == null) continue;
                var voters = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id).ToList();
                var found = new UvpWord { Seed = seed, PromisedId = voters[0], PartnerId = voters[1], Week = s.week };
                if (!engine.Apply(UvpCommand(s, EpisodeCommandKind.PromiseVote, voters[0], s.nominees[0])).accepted) continue;
                s = engine.Snapshot;
                found.PromiseId = CommitmentReferences.Promises(s).Single(p => p.kind == PromiseKind.Vote && p.fromId == s.playerId && p.toId == voters[0]).id;
                if (!engine.Apply(UvpCommand(s, EpisodeCommandKind.ProposeDeal, voters[1], s.nominees[0], DealKind.VoteEvict)).accepted) continue;
                found.Campaign = engine.Snapshot;
                found.DealId = CommitmentReferences.Deals(found.Campaign).FirstOrDefault(d => d.type == DealKind.VoteEvict && d.recipientId == voters[1]
                    && d.status == DealStatus.Active)?.id;
                if (found.DealId == null) continue;
                for (int guard = 0; guard < 600 && found.Settled == null; guard++)
                {
                    var now = engine.Snapshot;
                    if (now.week == found.Week + 1 && now.phase == EpisodePhase.Campaign && now.pendingDiary == null) found.Settled = now;
                    else if (now.phase == EpisodePhase.Finished || now.Find(now.playerId).status != ContestantStatus.Active || !engine.Apply(UvpNext(now)).accepted) break;
                }
                if (found.Settled == null) continue;
                var deal = CommitmentReferences.Deals(found.Settled).Single(d => d.id == found.DealId);
                var word = CommitmentReferences.Promises(found.Settled).Single(p => p.id == found.PromiseId);
                if ((deal.status == DealStatus.Fulfilled || deal.status == DealStatus.Broken) && !KnownBallots.DealOutcomeKnown(found.Settled, deal)
                    && word.status == PromiseStatus.Fulfilled && (!keptDeal || deal.status == DealStatus.Fulfilled))
                {
                    Debug.Log("[Gamesim] Unified vote pages: the word fixture" + (keptDeal ? " (a kept deal)" : "") + " is seed " + seed + " in mode " + mode + ".");
                    return found;
                }
            }
            Assert.Fail("No bounded public fixture in mode " + mode + ": a kept word and a deal a hidden ballot " + (keptDeal ? "kept." : "settled."));
            return null;
        }

        /// <summary>
        /// The jury's season: a fresh season the player walks to the finale, giving two voters a week their word on the vote -
        /// to evict the first nominee, then the second, so one is kept and one broken by the ballot the walk casts - answering
        /// each question with the first response it offers, until a juror asks the player finalist about a vote promise of
        /// theirs (in mode 2, a canonical Vote row).
        /// </summary>
        private static EpisodeState UvpJuryFixture(int mode, out uint found)
        {
            foreach (uint seed in UvpSeeds(11, 60))
            {
                var engine = new EpisodeEngine(UvpFresh(seed, mode));
                var spent = new HashSet<int>();
                for (int guard = 0; guard < 2000; guard++)
                {
                    var s = engine.Snapshot;
                    if (s.phase == EpisodePhase.Finished || (s.phase == EpisodePhase.JuryQuestioning && !s.Active.Any(c => c.isPlayer))) break;
                    if (s.phase == EpisodePhase.JuryQuestioning)
                    {
                        var exchange = s.juryExchanges[s.juryQuestionIndex];
                        if (!exchange.completed && exchange.finalistId == s.playerId && exchange.receiptKind == FinaleQuestions.PromiseReceipt
                            && CommitmentReferences.FindPromise(s, exchange.receiptId)?.kind == PromiseKind.Vote
                            && (mode != 2 || CommitmentReferences.FindCanonical(s, exchange.receiptId)?.kind == UnifiedVoteTogether.Vote))
                        {
                            found = seed;
                            Debug.Log("[Gamesim] Unified vote pages: the jury fixture is seed " + seed + " in mode " + mode + ".");
                            return s;
                        }
                    }
                    var word = UvpWordOnTheVote(s, spent);
                    if (word != null)
                    {
                        if (!engine.Apply(word).accepted) spent.Add(s.week);
                        continue;
                    }
                    var result = engine.Apply(UvpNext(s));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.Fail("No bounded public fixture in mode " + mode + ": a juror asking the player about a vote promise.");
            found = 0;
            return null;
        }

        /// <summary>The player's word on the vote, two voters a week while actions remain: to evict the first nominee, then the second.</summary>
        private static EpisodeCommand UvpWordOnTheVote(EpisodeState s, HashSet<int> spent)
        {
            if (s.phase != EpisodePhase.Campaign || s.pendingDiary != null || !UvPlayerVotes(s) || spent.Contains(s.week)
                || EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            var given = CommitmentReferences.Promises(s).Where(p => p.kind == PromiseKind.Vote && p.fromId == s.playerId && p.week == s.week).ToList();
            string voter = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id).FirstOrDefault(id => given.All(p => p.toId != id));
            if (given.Count >= 2 || voter == null) { spent.Add(s.week); return null; }
            return UvpCommand(s, EpisodeCommandKind.PromiseVote, voter, s.nominees[given.Count % 2]);
        }

        /// <summary>A fixture saved to this test's slot and loaded as a player loads it, the house held.</summary>
        private IEnumerator UvpInstall(EpisodeState fixture)
        {
            director.FreezeForReloadForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels();
            UvEquivalent(fixture, director.Snapshot);
            yield return SettleCast();
        }

        /// <summary>Where the season keeps the word: in mode 2 canonical Vote rows and no raw mirror, in mode 1 the raw lists.</summary>
        private static void UvpAssertWhereTheWordLives(EpisodeState s, int mode, string promiseId, string dealId)
        {
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(mode), "The season keeps its mode.");
            foreach (string id in new[] { promiseId, dealId })
            {
                bool canonical = s.unifiedCommitments.Any(row => row.id == id && row.kind == UnifiedVoteTogether.Vote);
                bool raw = s.promises.Any(p => p.id == id) || s.deals.Any(d => d.id == id);
                Assert.That((canonical, raw), Is.EqualTo((mode == 2, mode != 2)), id + ": mode " + mode + (mode == 2 ? " holds a canonical Vote row" : " holds the raw row") + ".");
            }
        }

        /// <summary>The notes page's door to Your word, pressed by its caption.</summary>
        private IEnumerator UvpOpenYourWord(string where)
        {
            yield return OpenNotebook();
            ButtonWithCaption(EpisodeDirector.YourWordCaption).onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), where + ": Your word is open.");
        }

        /// <summary>A houseguest's card in a section of the page carries the commitment's line, every label fitting.</summary>
        private RectTransform UvpAssertCard(string prefix, string id, CommitmentsRead.Commitment commitment, string where)
        {
            var card = ActiveRect(prefix + director.Snapshot.Find(id).name);
            Assert.That(card, Is.Not.Null, where + ": " + director.Snapshot.Find(id).name + " has a card under " + prefix + ".");
            Assert.That(Words(card), Does.Contain(CommitmentsRead.Line(commitment)), where + ": the card says " + CommitmentsRead.Line(commitment));
            foreach (var label in card.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, where + ": '" + label.text + "' fits its card.");
            }
            return card;
        }
    }
}
