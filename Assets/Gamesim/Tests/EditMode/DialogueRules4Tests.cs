using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4 (DIALOGUE-PLAN D4, §4.2): the veto question and the conditional promise, played on genuine seasons
    /// enrolled by the literal 4 (<see cref="DialogueRules4Season"/>). Rules 4 is defined and not offered. Shared cases: the
    /// dotnet subset runs them.
    /// </summary>
    public sealed class DialogueRules4Tests
    {
        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command) => DialogueRules2Season.Accepted(engine, command);

        [Test]
        public void RulesFourIsDefinedAndNotOfferedAndItsTemplateIsClosedFreshAndWithinTheShapeBounds()
        {
            Assert.That(DialogueRules.Rules4, Is.EqualTo(4));
            Assert.That(DialogueRules.IsDefined(4), Is.True);
            Assert.That(DialogueRules.Offered, Is.EqualTo(3), "The chip moves to 4 only in the stack's last slice.");
            var first = DialogueRules.ContentFor(4, DialogueVetoQuestion.TemplateId, DialogueVetoQuestion.ContentVersion);
            var second = DialogueRules.ContentFor(4, DialogueVetoQuestion.TemplateId, DialogueVetoQuestion.ContentVersion);
            Assert.That(first, Is.Not.Null);
            Assert.That(ReferenceEquals(first, second), Is.False, "A fresh definition on every read.");
            Assert.That(DialogueRules.ContentFor(3, DialogueVetoQuestion.TemplateId, DialogueVetoQuestion.ContentVersion), Is.Null, "Rules 3 never plays it.");
            Assert.That(DialogueRules.ContentFor(4, DialogueVetoQuestion.TemplateId, 2), Is.Null);
            Assert.That(DialogueRules.ContentFor(4, DialogueCampaignTrees.TemplateId, DialogueCampaignTrees.ContentVersion), Is.Not.Null, "Rules 4 keeps rules 3's trees.");
            Assert.That(DialogueRules.IsDialogueTemplate(DialogueVetoQuestion.TemplateId), Is.True);
            Assert.That(StoryCatalog.All.Any(t => t.id == DialogueVetoQuestion.TemplateId), Is.False, "Never an ambient-pool arc.");
            Assert.That(first.beats.Length, Is.EqualTo(1 + DialogueVetoQuestion.Answers.Length));
            foreach (var beat in first.beats)
            {
                Assert.That(beat.options.Length, Is.InRange(2, 4), beat.id);
                Assert.That(beat.options.Any(o => o.id == DialogueVetoQuestion.Leave), Is.True, beat.id + ": every node has a neutral exit.");
                Assert.That(beat.options.Any(o => o.id == DialogueVetoQuestion.AskPromise), Is.EqualTo(beat.id == DialogueVetoQuestion.ReplyBeat(DialogueVetoQuestion.Yes)),
                    beat.id + ": the promise is asked for only after a yes.");
            }
            Assert.That(DealKind.IsKnown(DealKind.VetoIf), Is.True);
            Assert.That(DealKind.All, Does.Not.Contain(DealKind.VetoIf), "Kept out of the list the readers and digests walk.");
            Assert.That(DealKind.DefaultTrust(DealKind.VetoIf), Is.EqualTo(DealTrust.Critical));
            Assert.That(PlayerDeals.CanPropose(DialogueRules4Season.Stops()[0].State, DialogueRules4Season.Stops()[0].People[0], DealKind.VetoIf, null, out _),
                Is.False, "Never proposed by the player.");
            Assert.That(Negotiation.IsVetoWord(DealKind.VetoIf), Is.False, "The player is its beneficiary, never its maker.");
        }

        /// <summary>The question costs the window's one action, draws nothing from the season's stream, and its answer is the statement.</summary>
        [Test]
        public void TheQuestionCostsOneActionDrawsNothingAndItsAnswerIsTheStatement()
        {
            var stop = DialogueRules4Season.Stops()[0];
            var before = stop.State;
            string npc = stop.People[0];
            var engine = new EpisodeEngine(before.Clone());
            var opened = Accepted(engine, DialogueRules4Season.Ask(before, npc));
            Assert.That(opened.randomState, Is.EqualTo(before.randomState), "The answer is keyed: nothing comes off the season's stream.");
            Assert.That(EpisodeEngine.SocialActionsSpent(opened), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1), "One action.");
            Assert.That(opened.windowActions[Windows.AfterNominations], Is.EqualTo(before.windowActions[Windows.AfterNominations] + 1));
            var episode = DialogueRules4Season.Question(opened, npc);
            Assert.That((episode.occasionKind, episode.occasionId, episode.window, episode.startedPhase),
                Is.EqualTo((DialogueVetoQuestion.Kind, "week:" + before.week + ":veto-question:" + npc, Windows.AfterNominations, EpisodePhase.Veto)));
            var statement = episode.statement;
            Assert.That(statement, Is.Not.Null);
            Assert.That(DialogueVetoQuestion.Answers, Does.Contain(statement.answer));
            Assert.That((statement.speakerId, statement.targetId, statement.conditionKind, statement.week),
                Is.EqualTo((npc, before.playerId, DialogueVetoQuestion.ConditionKind, before.week)));
            Assert.That(statement.audienceIds, Is.EqualTo(new[] { before.playerId }));
            Assert.That(DialogueRules4Season.Suffix(episode.responses[0]), Is.EqualTo(statement.answer));
            Assert.That(DialogueRules3Season.Beat(opened), Is.EqualTo(DialogueVetoQuestion.ReplyBeat(statement.answer)));
            // The same question keyed on the same occasion gives the same answer: asked again from the same state.
            var again = Accepted(new EpisodeEngine(before.Clone()), DialogueRules4Season.Ask(before, npc));
            Assert.That(DialogueRules4Season.Question(again, npc).statement.answer, Is.EqualTo(statement.answer));
            var left = Accepted(engine, DialogueRules4Season.Choice(opened, DialogueVetoQuestion.Leave));
            Assert.That(DialogueRules.Active(left), Is.Null);
            Assert.That(left.randomState, Is.EqualTo(before.randomState));
            Assert.That(DialogueRules.CanStart(left, npc, out string why), Is.False, "One occasion a week.");
            Assert.That(DialogueRules.CanAskVetoQuestion(left, npc), Is.False);
            if (DialogueRules2Season.Remaining(left) > 0) Assert.That(why, Is.EqualTo("This veto occasion has already been used."));
        }

        /// <summary>"Can we make that a promise?" after a yes writes one veto_if deal, deal-ifwin-n, n the promise step's log sequence.</summary>
        [Test]
        public void AYesMadeAPromiseWritesOneConditionalVetoDealOfItsExactShape()
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault();
            Assert.That(promise, Is.Not.Null, "A yes to make a promise of.");
            var s = promise.Promised;
            var episode = DialogueRules4Season.Question(s, promise.Maker);
            var cycle = DialogueRules.Cycle(s, episode);
            var deal = s.deals.Single(d => d.type == DealKind.VetoIf);
            Assert.That(deal.id, Is.EqualTo("deal-ifwin-" + cycle.path[1].logSequence));
            Assert.That((deal.proposerId, deal.recipientId, deal.targetId, deal.status, deal.week, deal.expiresWeek, deal.trustImpact),
                Is.EqualTo((promise.Maker, s.playerId, s.playerId, DealStatus.Active, s.week, s.week, DealTrust.Critical)));
            Assert.That(deal.linkedDealId, Is.Null);
            Assert.That((cycle.status, cycle.endingId, episode.time.reason), Is.EqualTo((StorylineStatus.Completed, "answered", "answered")));
            Assert.That(s.randomState, Is.EqualTo(promise.Stop.State.randomState), "Asking and promising draw nothing.");
            Assert.That(EpisodeEngine.SocialActionsSpent(s), Is.EqualTo(EpisodeEngine.SocialActionsSpent(promise.Asked)), "The promise is free.");
            Assert.That(DialogueRules3Season.Moved(promise.Asked, s), Is.Empty, "Promising moves nobody.");
            Assert.That(promise.Thanked.deals.Any(d => d.type == DealKind.VetoIf), Is.False, "Thanks write no promise.");
            Assert.That(DialogueRules.TryValidateConditionalVeto(s, out string error), Is.True, error);
        }

        /// <summary>No yes, no promise: the promise is not on the node and its choice cannot be built.</summary>
        [Test]
        public void WithoutAYesThereIsNoPromiseToAskFor()
        {
            foreach (var stop in DialogueRules4Season.Stops())
                foreach (string npc in stop.People)
                {
                    var opened = Accepted(new EpisodeEngine(stop.State.Clone()), DialogueRules4Season.Ask(stop.State, npc));
                    if (DialogueRules4Season.Question(opened, npc).statement.answer == DialogueVetoQuestion.Yes) continue;
                    var cycle = DialogueRules.Cycle(opened, DialogueRules.Active(opened));
                    Assert.That(DialogueRules3Season.Options(opened), Is.EqualTo(new[] { DialogueVetoQuestion.Thank, DialogueVetoQuestion.Leave }));
                    Assert.That(DialogueRules.TryBuildChoiceCommand(opened, cycle.eventId, DialogueVetoQuestion.AskPromise, out _), Is.False);
                    return;
                }
            Assert.Fail("Every veto player the walk asked said yes.");
        }

        /// <summary>Below rules 4, and off the block, there is no veto question.</summary>
        [Test]
        public void TheQuestionIsOnlyARulesFourNomineesBeforeTheCompetition()
        {
            var stop = DialogueRules4Season.Stops()[0];
            string npc = stop.People[0];
            var rules3 = stop.State.Clone();
            rules3.dialogueRulesVersion = DialogueRules.Rules3;
            rules3.dialogue.vetoOccasions.Clear(); // Earlier weeks' occasions are rules 4's own rows.
            Assert.That(EpisodeValidation.TryValidate(rules3, out string error), Is.True, error);
            Assert.That(DialogueRules.CanAskVetoQuestion(rules3, npc), Is.False);
            Assert.That(DialogueRules.TryBuildEntryCommand(rules3, npc, out _), Is.False, "Rules 3 has no conversation in the veto competition.");
            var played = Accepted(new EpisodeEngine(stop.State.Clone()), DialogueRules4Season.Compete(stop.State));
            Assert.That(DialogueRules.CanAskVetoQuestion(played, npc), Is.False, "Not once the competition is played.");
            Assert.That(DialogueRules.CanStart(played, npc, out string why), Is.False);
            Assert.That(why, Is.EqualTo(DialogueRules.VetoQuestionRefusal));
            Assert.That(DialogueRules.CanAskVetoQuestion(stop.State, stop.State.playerId), Is.False, "Never of yourself.");
        }

        /// <summary>A question left open lapses at the result through its own exit, drawing nothing: the competition plays as it would have.</summary>
        [Test]
        public void AnOpenQuestionLapsesAtTheResultAndTheCompetitionPlaysAsItWouldHave()
        {
            var stop = DialogueRules4Season.Stops()[0];
            string npc = stop.People[0];
            var engine = new EpisodeEngine(stop.State.Clone());
            var opened = Accepted(engine, DialogueRules4Season.Ask(stop.State, npc));
            var twin = new EpisodeEngine(opened.Clone());
            Accepted(twin, DialogueRules4Season.Choice(opened, DialogueVetoQuestion.Leave));
            var lapsed = Accepted(engine, DialogueRules4Season.Compete(opened));
            var episode = DialogueRules4Season.Question(lapsed, npc);
            var cycle = DialogueRules.Cycle(lapsed, episode);
            Assert.That((cycle.status, cycle.endingId, episode.time.reason), Is.EqualTo((StorylineStatus.Completed, "lapsed", "occasion-closed")));
            Assert.That(lapsed.competitionResolved, Is.True);
            var played = Accepted(twin, DialogueRules4Season.Compete(twin.Snapshot));
            Assert.That(lapsed.randomState, Is.EqualTo(played.randomState), "The lapse draws nothing.");
            Assert.That(lapsed.vetoHolderId, Is.EqualTo(played.vetoHolderId), "And the competition is won by the same houseguest.");
            Assert.That(lapsed.vetoHolderId, Is.EqualTo(stop.Winner));
        }

        /// <summary>
        /// The veto meeting's opening writes the week's occasion; a promise whose maker did not win lapses neutrally: expired,
        /// no score, no line.
        /// </summary>
        [Test]
        public void AnUnmetPromiseLapsesNeutrallyAndTheOccasionSaysWhy()
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault(p => p.Maker != p.Stop.Winner);
            Assert.That(promise, Is.Not.Null, "A promise by a veto player who then lost.");
            var engine = new EpisodeEngine(promise.Promised.Clone());
            var played = Accepted(engine, DialogueRules4Season.Compete(promise.Promised));
            Assert.That(played.dialogue.vetoOccasions, Is.Empty, "The occasion is written as the meeting opens.");
            var opening = Accepted(engine, EpisodeEngineTests.NextCommand(played));
            Assert.That(opening.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            var occasion = opening.dialogue.vetoOccasions.Single();
            Assert.That((occasion.week, occasion.holderId, occasion.hohId), Is.EqualTo((opening.week, opening.vetoHolderId, opening.hohId)));
            Assert.That(occasion.participants, Is.EqualTo(opening.vetoPlayers));
            Assert.That(occasion.nomineesAtResult, Is.EqualTo(opening.nominees));
            Assert.That(occasion.holderId, Is.Not.EqualTo(promise.Maker), "The maker did not win: the condition is not met.");
            var deal = opening.deals.Single(d => d.type == DealKind.VetoIf);
            Assert.That((deal.status, deal.settledWeek, deal.brokenById), Is.EqualTo((DealStatus.Expired, 0, (string)null)));
            Assert.That(DialogueRules4Season.Score(opening, opening.playerId, promise.Maker), Is.EqualTo(DialogueRules4Season.Score(played, played.playerId, promise.Maker)));
            Assert.That(opening.events.Any(e => e.text != null && e.text.Contains("promise to use it on you")), Is.False, "No line.");
        }

        /// <summary>
        /// §4.2 point 4's test: a veto meeting that settles a broken promise against the same meeting without it (the yes
        /// thanked instead). The decision is the same, randomState is the same, and so is every score between two
        /// houseguests; only the player's view of the holder moves, by -45, with the breach, the memory and one private line.
        /// </summary>
        [Test]
        public void ABrokenPromiseSettlesInItsOwnStepWithNoSpreadAndNoDraw()
        {
            var (promise, with, without) = DialogueRules4Season.Settled(DealStatus.Broken);
            for (int i = 0; i < with.Length; i++)
                Assert.That(with[i].randomState, Is.EqualTo(without[i].randomState), "step " + i + ": the promise draws nothing.");
            var decided = with[with.Length - 1];
            var control = without[without.Length - 1];
            Assert.That(decided.nominees, Is.EqualTo(control.nominees), "The same decision.");
            Assert.That(DialogueRules4Season.NpcScores(decided), Is.EqualTo(DialogueRules4Season.NpcScores(control)), "No spread: no pair of houseguests moves.");
            string player = decided.playerId, holder = promise.Maker;
            Assert.That(DialogueRules4Season.Score(decided, holder, player), Is.EqualTo(DialogueRules4Season.Score(control, holder, player)),
                "The holder's view of the player does not move.");
            double expected = Math.Max(-100, Math.Min(100, DialogueRules4Season.Score(control, player, holder) - 45));
            Assert.That(DialogueRules4Season.Score(decided, player, holder), Is.EqualTo(expected).Within(1e-9), "The player's view of the holder: -45.");
            var deal = decided.deals.Single(d => d.type == DealKind.VetoIf);
            Assert.That((deal.status, deal.brokenById, deal.settledWeek), Is.EqualTo((DealStatus.Broken, holder, decided.week)));
            var occasion = decided.dialogue.vetoOccasions.Single(o => o.week == decided.week);
            Assert.That(occasion.decision, Is.Not.Null);
            Assert.That(occasion.decision.used && occasion.decision.savedId == player, Is.False);
            var line = decided.events.Last(e => e.kind == "deal-outcome" && e.text.Contains("broke their promise"));
            Assert.That(line.audienceIds, Is.EqualTo(new[] { holder, player }), "A line to the two of them only.");
            Assert.That(decided.memories.Any(m => m.ownerId == player && m.subjectId == holder && m.text == line.text), Is.True);
            Assert.That(control.events.Any(e => e.kind == "deal-outcome" && e.text.Contains("promise to use it on you")), Is.False);
        }

        /// <summary>A promise kept: the holder used the veto on the player, and the player thinks the better of them, +24.</summary>
        [Test]
        public void AKeptPromiseIsFulfilledAndThePlayerThinksTheBetterOfTheHolder()
        {
            var (promise, with, without) = DialogueRules4Season.Settled(DealStatus.Fulfilled);
            var decided = with[with.Length - 1];
            string player = decided.playerId, holder = promise.Maker;
            var deal = decided.deals.Single(d => d.type == DealKind.VetoIf);
            Assert.That((deal.status, deal.brokenById, deal.settledWeek), Is.EqualTo((DealStatus.Fulfilled, (string)null, decided.week)));
            var occasion = decided.dialogue.vetoOccasions.Single(o => o.week == decided.week);
            Assert.That((occasion.decision.used, occasion.decision.savedId), Is.EqualTo((true, player)));
            Assert.That(decided.nominees, Does.Not.Contain(player));
            Assert.That(decided.events.Any(e => e.kind == "deal-outcome" && e.text == "" + decided.Find(holder).name
                + " won the veto and kept their promise to use it on you."), Is.True);
            for (int i = 0; i < with.Length - 1; i++)
                Assert.That(with[i].randomState, Is.EqualTo(without[i].randomState), "step " + i + ": up to the decision nothing differs in the stream.");
        }

        /// <summary>At the meeting, the holder's pull counts a met promise once, as a veto commitment's (§4.2 point 5).</summary>
        [Test]
        public void TheHoldersPullCountsAMetPromiseOnce()
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault(p => p.Maker == p.Stop.Winner);
            Assert.That(promise, Is.Not.Null, "A promise by the veto's winner.");
            var with = DialogueRules4Season.ToTheDecision(new EpisodeEngine(promise.Promised.Clone()));
            var without = DialogueRules4Season.ToTheDecision(new EpisodeEngine(promise.Thanked.Clone()));
            var meeting = with.First(x => x.phase == EpisodePhase.VetoMeeting && !x.vetoResolved);
            var control = without.First(x => x.phase == EpisodePhase.VetoMeeting && !x.vetoResolved);
            Assert.That(meeting.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(DialogueRules.HoldsMetVetoIf(meeting, promise.Maker), Is.True);
            Assert.That(DialogueRules.HoldsMetVetoIf(control, promise.Maker), Is.False);
            double pull = StrategyRules.VetoWillingness(meeting, promise.Maker, meeting.playerId)
                - StrategyRules.VetoWillingness(control, promise.Maker, control.playerId);
            bool veto = NpcDeals.Between(control, promise.Maker, control.playerId).Any(d => d.type == DealKind.VetoUse);
            Assert.That(pull, Is.EqualTo(veto ? 0 : StrategyRules.VetoCommitmentPull).Within(1e-9), "Once, and never twice with a veto commitment.");
            Assert.That(DialogueRules.HoldsMetVetoIf(with[with.Length - 1], promise.Maker), Is.False, "Settled at the decision.");
        }

        /// <summary>A promise given while its maker holds a binding word on the veto this week is refused in its own words; the statement stands.</summary>
        [Test]
        public void APromiseIsRefusedWhileItsMakerHasGivenAnotherWordOnTheVeto()
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault();
            Assert.That(promise, Is.Not.Null, "A yes to make a promise of.");
            var asked = promise.Asked.Clone();
            string other = asked.Active.First(c => !c.isPlayer && c.id != promise.Maker).id;
            asked.deals.Add(new DealState { id = "deal-" + asked.nextSequence, type = DealKind.VetoUse, proposerId = other,
                recipientId = promise.Maker, status = DealStatus.Active, week = asked.week, expiresWeek = asked.week,
                trustImpact = DealKind.DefaultTrust(DealKind.VetoUse) });
            asked.nextSequence++;
            Assert.That(EpisodeValidation.TryValidate(asked, out string error), Is.True, error);
            var refused = Accepted(new EpisodeEngine(asked), DialogueRules4Season.Choice(asked, DialogueVetoQuestion.AskPromise));
            var episode = DialogueRules4Season.Question(refused, promise.Maker);
            Assert.That(DialogueRules4Season.Suffix(episode.responses[1]), Is.EqualTo(DialogueVetoQuestion.PromiseRefused));
            Assert.That(refused.deals.Any(d => d.type == DealKind.VetoIf), Is.False);
            Assert.That(episode.statement.answer, Is.EqualTo(DialogueVetoQuestion.Yes), "The statement stands.");
            Assert.That(refused.events.Last(e => e.kind == "dialogue-promise").text, Does.EndWith(DialogueRules.PromiseConflictRefusal));
        }

        /// <summary>The promise survives a save's JSON round trip, mid-question and mid-promise, and the season goes on to settle it.</summary>
        [Test]
        public void ASeasonMidQuestionAndMidPromiseRoundTripsAndContinues()
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault(p => p.Maker == p.Stop.Winner);
            Assert.That(promise, Is.Not.Null, "A promise by the veto's winner.");
            var asked = DialogueRules2Season.RoundTrip(promise.Asked);
            Assert.That(DialogueRules2Season.Json(asked), Is.EqualTo(DialogueRules2Season.Json(promise.Asked)));
            var promised = Accepted(new EpisodeEngine(asked), DialogueRules4Season.Choice(asked, DialogueVetoQuestion.AskPromise));
            Assert.That(DialogueRules2Season.Json(promised), Is.EqualTo(DialogueRules2Season.Json(promise.Promised)), "The loaded season promises as the live one did.");
            var loaded = DialogueRules2Season.RoundTrip(promised);
            var settled = DialogueRules4Season.ToTheDecision(new EpisodeEngine(loaded));
            var live = DialogueRules4Season.ToTheDecision(new EpisodeEngine(promise.Promised.Clone()));
            Assert.That(DialogueRules2Season.Json(settled[settled.Length - 1]), Is.EqualTo(DialogueRules2Season.Json(live[live.Length - 1])));
            Assert.That(settled[settled.Length - 1].deals.Single(d => d.type == DealKind.VetoIf).status,
                Is.AnyOf(DealStatus.Fulfilled, DealStatus.Broken, DealStatus.Expired));
        }

        [TestCase("below-rules-4")] [TestCase("id")] [TestCase("maker")] [TestCase("trust")] [TestCase("unlinked-prefix")]
        [TestCase("statement-answer")] [TestCase("occasion-below-rules-4")] [TestCase("settled-without-decision")]
        public void TheValidatorRefusesAConditionalPromiseOrQuestionThatStrays(string defect)
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault(p => p.Maker == p.Stop.Winner);
            Assert.That(promise, Is.Not.Null, "A promise by the veto's winner.");
            var source = promise.Promised;
            if (defect == "occasion-below-rules-4" || defect == "settled-without-decision")
            {
                var engine = new EpisodeEngine(source.Clone());
                Accepted(engine, DialogueRules4Season.Compete(source));
                source = Accepted(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(source.dialogue.vetoOccasions, Has.Count.EqualTo(1));
            }
            Assert.That(EpisodeValidation.TryValidate(source, out string error), Is.True, error);
            var bad = source.Clone();
            var deal = bad.deals.Single(d => d.type == DealKind.VetoIf);
            switch (defect)
            {
                case "below-rules-4": bad.dialogueRulesVersion = DialogueRules.Rules3; break;
                case "id": deal.id = DialogueRules.IfWinId(deal.week * 1000 + 7); break;
                case "maker": deal.proposerId = bad.Active.First(c => !c.isPlayer && c.id != promise.Maker).id; break;
                case "trust": deal.trustImpact = DealTrust.High; break;
                case "unlinked-prefix": bad.deals.Add(new DealState { id = "deal-ifwin-x", type = DealKind.VetoUse, proposerId = promise.Maker,
                    recipientId = bad.playerId, status = DealStatus.Proposed, week = bad.week, expiresWeek = bad.week, trustImpact = DealTrust.Critical }); break;
                case "statement-answer": DialogueRules4Season.Question(bad, promise.Maker).statement.answer = DialogueVetoQuestion.No; break;
                case "occasion-below-rules-4":
                    bad.deals.Remove(deal);
                    bad.dialogue.episodes.Clear();
                    bad.storylines.RemoveAll(c => c.templateId == DialogueVetoQuestion.TemplateId);
                    bad.houseEvents.RemoveAll(e => e.contentId != null && e.contentId.StartsWith(DialogueVetoQuestion.TemplateId + ":", StringComparison.Ordinal));
                    bad.dialogueRulesVersion = DialogueRules.Rules3;
                    break;
                default: deal.status = DealStatus.Fulfilled; deal.settledWeek = bad.week; break;
            }
            Assert.That(EpisodeValidation.TryValidate(bad, out error), Is.False, defect);
            TestContext.Out.WriteLine(defect + ": " + error);
        }
    }
}
