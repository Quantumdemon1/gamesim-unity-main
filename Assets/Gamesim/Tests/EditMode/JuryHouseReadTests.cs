using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// ENDGAME-PLAN F4's jury house: each juror's band from the player's last read of them and
    /// what the house saw the player do since, never from the vote's own terms. Unity-free, so
    /// the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class JuryHouseReadTests
    {
        /// <summary>A Final 3 in its window: the player and two finalists, three jurors.</summary>
        private static EpisodeState FinalThree(uint seed = 41)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, seed);
            s.week = 8;
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.Social;
            return s;
        }

        private static ContestantState Juror(EpisodeState s, int index) => FinalistRead.Jurors(s)[index];
        private static ContestantState Finalist(EpisodeState s, int index) => FinalistRead.Others(s)[index];

        private static void Read(EpisodeState s, string jurorId, int week, double score, string source = ClaimSource.Read) =>
            s.ledger.standings.Add(new StandingRow { week = week, fromId = jurorId, toId = s.playerId, source = source, score = score });

        private static JuryHouseRead.Juror Of(EpisodeState s, string jurorId) => JuryHouseRead.ReadJuror(s, jurorId);

        [Test]
        public void WithNoReadEveryJurorIsUnknownAndTheJurorRowIsNeverRead()
        {
            var s = FinalThree();
            var juror = Juror(s, 0);
            Read(s, juror.id, 5, 90, ClaimSource.Juror);
            Read(s, juror.id, 4, 0, ClaimSource.Missed);
            Read(s, juror.id, 3, 0, ClaimSource.Deflected);
            var read = Of(s, juror.id);
            Assert.That(read.band, Is.EqualTo(JuryHouseRead.Unknown), "The juror row is the vote's own term; a miss teaches nothing.");
            Assert.That(read.reason, Is.EqualTo("You never read them."));
            Assert.That(read.readWeek, Is.Null);
            Assert.That(JuryHouseRead.Read(s).jurors.Select(j => j.band), Is.All.EqualTo(JuryHouseRead.Unknown));
        }

        [Test]
        public void TheLastReadSetsTheBand()
        {
            var s = FinalThree();
            var juror = Juror(s, 0);
            Read(s, juror.id, 2, 40);
            Read(s, juror.id, 4, -30);
            Assert.That(Of(s, juror.id).band, Is.EqualTo(JuryHouseRead.Skeptical), "The last read, not the first.");
            Assert.That(Of(s, juror.id).reason, Is.EqualTo("Your read in week 4: cold on you."));
            Read(s, juror.id, 5, 10);
            Assert.That(Of(s, juror.id).band, Is.EqualTo(JuryHouseRead.Open));
            Read(s, juror.id, 6, 25);
            Assert.That(Of(s, juror.id).band, Is.EqualTo(JuryHouseRead.Supportive), "25 is warm, as the read's own line has it.");
            Assert.That(Of(s, juror.id).readWeek, Is.EqualTo(6));
        }

        [Test]
        public void AWarmReadCooledSinceIsWaveringAndAKeptDealKeepsItSupportive()
        {
            var s = FinalThree();
            var a = Juror(s, 0); var b = Juror(s, 1); var c = Juror(s, 2);
            foreach (var j in new[] { a, b, c }) Read(s, j.id, 3, 45);
            s.deals.Add(new DealState { id = "kept", type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = a.id, status = DealStatus.Fulfilled, week = 4 });
            Assert.That(Of(s, a.id).band, Is.EqualTo(JuryHouseRead.Supportive), "A kept deal cools nothing.");

            s.promises.Add(new PromiseState { id = "broken", fromId = s.playerId, toId = a.id, kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 4 });
            var cooled = Of(s, a.id);
            Assert.That(cooled.band, Is.EqualTo(JuryHouseRead.Wavering));
            Assert.That(cooled.reason, Does.EndWith("Then you broke a promise to them since."));

            s.ledger.replies.Add(new ReplyRow { week = 5, cardId = "reply-1", kind = ReplyCards.Plea, fromId = b.id, replyKey = "refuse", toThem = -5 });
            Assert.That(Of(s, b.id).highlights, Does.Contain("Week 5 · they pleaded with you; you answered refuse"));
            Assert.That(Of(s, b.id).band, Is.EqualTo(JuryHouseRead.Wavering), "A plea refused.");

            s.ledger.ballots.Add(new BallotRow { week = 5, voterId = s.playerId, targetId = c.id });
            Assert.That(Of(s, c.id).band, Is.EqualTo(JuryHouseRead.Wavering), "A vote to evict them, in the open.");

            var before = FinalThree();
            var d = Juror(before, 0);
            Read(before, d.id, 6, 45);
            before.promises.Add(new PromiseState { id = "old", fromId = before.playerId, toId = d.id, kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 2 });
            Assert.That(Of(before, d.id).band, Is.EqualTo(JuryHouseRead.Supportive), "A promise broken before the read is already in it.");
        }

        [Test]
        public void OnlyWhatProvablyCameAfterTheReadCoolsIt()
        {
            // A safety promise broken at the week-6 nomination, then a warm read in the week-6 campaign:
            // the read already holds the breach.
            var s = FinalThree();
            var j = Juror(s, 0);
            s.promises.Add(new PromiseState { id = "safety", fromId = s.playerId, toId = j.id, kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 5, expiresWeek = 6 });
            Read(s, j.id, 6, 30);
            Assert.That(Of(s, j.id).band, Is.EqualTo(JuryHouseRead.Supportive), "The breach came before the read.");
            var earlier = FinalThree();
            var e = Juror(earlier, 0);
            earlier.promises.Add(new PromiseState { id = "safety", fromId = earlier.playerId, toId = e.id, kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 5, expiresWeek = 6 });
            Read(earlier, e.id, 5, 30);
            Assert.That(Of(earlier, e.id).band, Is.EqualTo(JuryHouseRead.Wavering), "A read the week before: the break may be after it.");

            // A vote the same week as the read cannot be placed against it.
            var vote = FinalThree();
            var v = Juror(vote, 0);
            vote.ledger.ballots.Add(new BallotRow { week = 5, voterId = vote.playerId, targetId = v.id });
            Read(vote, v.id, 5, 40);
            Assert.That(Of(vote, v.id).band, Is.EqualTo(JuryHouseRead.Supportive));

            // A Final 2 agreement breaks only at the final eviction, after every read.
            var deal = FinalThree();
            var d = Juror(deal, 0);
            deal.deals.Add(new DealState { id = "f2", type = DealKind.FinalTwo, proposerId = deal.playerId, recipientId = d.id, status = DealStatus.Broken, week = 3 });
            Read(deal, d.id, 5, 40);
            Assert.That(Of(deal, d.id).band, Is.EqualTo(JuryHouseRead.Wavering));
            Assert.That(Of(deal, d.id).reason, Does.EndWith("Then a deal between you broke since."));
        }

        [Test]
        public void ThePlayerPuttingThemUpIsBitterUnlessALaterReadSaysOtherwise()
        {
            var s = FinalThree();
            var juror = Juror(s, 0); var other = Juror(s, 1);
            s.ledger.power.Add(new PowerRow { week = 4, hohId = s.playerId, nominees = new List<string> { juror.id, other.id }, evicteeId = other.id, tally = new List<int> { 1, 3 } });
            var bitter = Of(s, juror.id);
            Assert.That(bitter.band, Is.EqualTo(JuryHouseRead.Bitter));
            Assert.That(bitter.reason, Is.EqualTo("Week 4: you nominated them."));

            Read(s, juror.id, 3, 60);
            Assert.That(Of(s, juror.id).band, Is.EqualTo(JuryHouseRead.Bitter), "A warm read before the nomination does not outlast it.");
            Read(s, juror.id, 4, 60);
            Assert.That(Of(s, juror.id).band, Is.EqualTo(JuryHouseRead.Supportive), "A read the same week came after the ceremony.");

            var replaced = FinalThree();
            var r = Juror(replaced, 0);
            replaced.ledger.power.Add(new PowerRow { week = 5, hohId = replaced.playerId, vetoUsed = true, replacementId = r.id, savedId = Juror(replaced, 1).id,
                nominees = new List<string> { r.id, Juror(replaced, 2).id } });
            Assert.That(Of(replaced, r.id).reason, Is.EqualTo("Week 5: you named them the replacement."));

            var vetoed = FinalThree();
            var v = Juror(vetoed, 0);
            vetoed.ledger.power.Add(new PowerRow { week = 5, hohId = Finalist(vetoed, 0).id, vetoHolderId = vetoed.playerId, vetoUsed = true, replacementId = v.id,
                nominees = new List<string> { v.id, Juror(vetoed, 1).id } });
            Assert.That(Of(vetoed, v.id).reason, Is.EqualTo("Week 5: your veto put them on the block."));
        }

        [Test]
        public void EvictedByThePlayerIsBitterWhateverTheRead()
        {
            var s = FinalThree();
            var last = Juror(s, 0); var tied = Juror(s, 1);
            Read(s, last.id, 8, 80);
            s.ledger.power.Add(new PowerRow { week = 9, hohId = s.playerId, nominees = new List<string> { last.id, Finalist(s, 0).id }, evicteeId = last.id });
            Assert.That(Of(s, last.id).band, Is.EqualTo(JuryHouseRead.Bitter));
            Assert.That(Of(s, last.id).reason, Is.EqualTo("Week 9: you evicted them at the final eviction."));

            Read(s, tied.id, 6, 80);
            s.ledger.power.Add(new PowerRow { week = 6, hohId = s.playerId, vetoHolderId = Finalist(s, 1).id, nominees = new List<string> { tied.id, Juror(s, 2).id },
                evicteeId = tied.id, tally = new List<int> { 2, 2 } });
            s.ledger.ballots.Add(new BallotRow { week = 6, voterId = s.playerId, targetId = tied.id });
            Assert.That(Of(s, tied.id).reason, Is.EqualTo("Week 6: you broke the tie to evict them."));
            Assert.That(Of(s, tied.id).highlights, Does.Contain("Week 6 · left on your nomination"));
        }

        [Test]
        public void AGrudgeRowNeverMakesThemBitter()
        {
            var s = FinalThree();
            var juror = Juror(s, 0);
            s.story.grudges.Add(new GrudgeState { holderId = juror.id, targetId = s.playerId, cause = GrudgeCauses.DealBroken, severity = 80, originWeek = 4, count = 1 });
            s.story.grudges.Add(new GrudgeState { holderId = Juror(s, 1).id, targetId = s.playerId, cause = GrudgeCauses.Nominated, severity = 70, originWeek = 4, count = 1 });
            Assert.That(Of(s, juror.id).band, Is.EqualTo(JuryHouseRead.Unknown), "A private grudge is the vote model's input.");
            Assert.That(Of(s, Juror(s, 1).id).band, Is.EqualTo(JuryHouseRead.Unknown), "Bitterness is read from the public record, not the grudge row.");
        }

        [Test]
        public void WhatTheyKnowWhatTheyMissedAndWhatWasBetweenYou()
        {
            var s = FinalThree();
            var juror = Juror(s, 0);
            var hoh = Finalist(s, 0);
            s.alliances.Add(new AllianceState { id = "ours", name = "The Pact", active = false, members = new List<string> { s.playerId, juror.id } });
            s.deals.Add(new DealState { id = "d", type = DealKind.FinalTwo, proposerId = juror.id, recipientId = s.playerId, status = DealStatus.Active, week = 3 });
            s.promises.Add(new PromiseState { id = "p", fromId = juror.id, toId = s.playerId, kind = PromiseKind.Safety, status = PromiseStatus.Fulfilled, week = 3 });
            s.ledger.ballots.Add(new BallotRow { week = 4, voterId = s.playerId, targetId = juror.id });
            s.ledger.power.Add(new PowerRow { week = 4, hohId = hoh.id, nominees = new List<string> { juror.id, Juror(s, 1).id }, evicteeId = juror.id, tally = new List<int> { 3, 1 } });
            s.ledger.power.Add(new PowerRow { week = 6, hohId = s.playerId, nominees = new List<string> { Juror(s, 1).id, Juror(s, 2).id }, evicteeId = Juror(s, 2).id, tally = new List<int> { 1, 2 } });
            s.ledger.competitions.Add(new CompetitionRow { week = 7, kind = "Veto", placement = 1, entry = "played" });
            s.ledger.competitions.Add(new CompetitionRow { week = 3, kind = "HoH", placement = 1, entry = "played" });
            Read(s, juror.id, 3, 30);

            var read = Of(s, juror.id);
            Assert.That(read.leftWeek, Is.EqualTo(4));
            Assert.That(read.knows, Does.Contain("You shared The Pact, now ended."));
            Assert.That(read.knows, Does.Contain("Final Two Deal: agreed."));
            Assert.That(read.knows, Does.Contain("They promised you safety: kept."));
            Assert.That(read.knows, Does.Contain("You voted to evict them once, in the open."));
            Assert.That(read.missing, Does.Contain("Week 6: you held the house."));
            Assert.That(read.missing, Does.Contain("Week 7: you won the veto."));
            Assert.That(read.missing.Any(line => line.StartsWith("Week 3")), Is.False, "Before they left, they saw it.");
            Assert.That(read.highlights, Is.EqualTo(new[] { "Week 3 · your read: warm on you", "Week 4 · left in " + FinalistRead.FirstName(hoh.name) + "'s week",
                "Week 4 · you voted to evict them" }), "Oldest first.");
            Assert.That(read.band, Is.EqualTo(JuryHouseRead.Wavering), "Warm, then the player voted them out.");
        }

        [Test]
        public void WhatMattersCountsTheTraitsTheJuryLeadsWith()
        {
            var s = FinalThree();
            var jurors = FinalistRead.Jurors(s);
            jurors[0].traits = new List<string> { "Loyal", "Funny" };
            jurors[1].traits = new List<string> { "Loyal" };
            jurors[2].traits = new List<string> { "Strategic" };
            var house = JuryHouseRead.Read(s);
            Assert.That(house.jurors, Has.Count.EqualTo(3));
            Assert.That(house.matters.First(), Is.EqualTo("2 of 3 lead with Loyal."));
            Assert.That(house.jurors.Single(j => j.id == jurors[2].id).trait, Is.EqualTo("Strategic"));
            jurors[1].traits = new List<string> { "Funny" };
            Assert.That(JuryHouseRead.Read(s).matters, Is.EqualTo(new[] { "No one trait leads this jury." }));
        }

        [Test]
        public void WhatTheySawOfYourGameIsTheExactComplementOfWhatTheyMissed()
        {
            var s = FinalThree();
            var juror = Juror(s, 0);
            // Juror 2 goes in week 2 on the player's nominations, juror 1 in week 3 after the
            // player's veto, juror 0 in week 4. The player holds the house again in week 6, uses
            // the veto in week 7, and evicts at the final eviction in week 9.
            s.ledger.power.Add(new PowerRow { week = 2, hohId = s.playerId, nominees = new List<string> { Juror(s, 2).id, Finalist(s, 0).id }, evicteeId = Juror(s, 2).id, tally = new List<int> { 3, 0 } });
            s.ledger.power.Add(new PowerRow { week = 3, hohId = Finalist(s, 0).id, vetoHolderId = s.playerId, vetoUsed = true, savedId = Finalist(s, 1).id,
                replacementId = Juror(s, 1).id, nominees = new List<string> { Juror(s, 1).id, Finalist(s, 0).id }, evicteeId = Juror(s, 1).id, tally = new List<int> { 2, 0 } });
            s.ledger.power.Add(new PowerRow { week = 4, hohId = Finalist(s, 1).id, nominees = new List<string> { juror.id, Finalist(s, 0).id }, evicteeId = juror.id, tally = new List<int> { 2, 0 } });
            s.ledger.power.Add(new PowerRow { week = 6, hohId = s.playerId, nominees = new List<string> { Finalist(s, 0).id, Finalist(s, 1).id } });
            s.ledger.power.Add(new PowerRow { week = 7, hohId = Finalist(s, 0).id, vetoHolderId = s.playerId, vetoUsed = true, nominees = new List<string> { Finalist(s, 1).id } });
            s.ledger.power.Add(new PowerRow { week = 9, hohId = s.playerId, nominees = new List<string> { Finalist(s, 0).id, Finalist(s, 1).id }, evicteeId = Finalist(s, 0).id });
            s.ledger.competitions.Add(new CompetitionRow { week = 2, kind = "HoH", placement = 1, entry = "played" });
            s.ledger.competitions.Add(new CompetitionRow { week = 3, kind = "Veto", placement = 1, entry = "played" });
            s.ledger.competitions.Add(new CompetitionRow { week = 4, kind = "Veto", placement = 2, entry = "played" });
            s.ledger.competitions.Add(new CompetitionRow { week = 6, kind = "HoH", placement = 1, entry = "played" });

            var read = Of(s, juror.id);
            Assert.That(read.leftWeek, Is.EqualTo(4));
            Assert.That(read.knows, Does.Contain("Your 2 wins, 1 week as Head of Household and 1 veto used before they left."),
                "Up to and including the week they left: what they missed is everything after it.");
            Assert.That(read.missing, Is.EquivalentTo(new[] { "Week 6: you held the house.", "Week 7: you used the veto.", "Week 6: you won Head of Household." }),
                "The final eviction's row is in neither.");

            var early = Of(s, Juror(s, 2).id);
            Assert.That(early.knows, Does.Contain("Your 1 win and 1 week as Head of Household before they left."), "Week 2 is theirs: they left in it.");

            var quiet = FinalThree();
            Assert.That(Of(quiet, Juror(quiet, 0).id).knows.Any(line => line.EndsWith("before they left.")), Is.False, "No record of when they left, nothing to say.");
        }

        [Test]
        public void TheGoodbyeTheyWatchedIsTheChoiceNeverItsEffect()
        {
            var s = FinalThree();
            var juror = Juror(s, 0); var skipped = Juror(s, 1);
            StorylineState Goodbye(string jurorId, string option) => new StorylineState
            {
                id = "goodbye-" + jurorId, templateId = JuryHouseRead.GoodbyeTemplate, week = 5, status = StorylineStatus.Completed,
                cast = new List<StoryRoleState> { new StoryRoleState { role = "EVICTEE", contestantId = jurorId } },
                path = new List<StoryStepState> { new StoryStepState { beatId = "message", optionId = option, week = 5 } },
            };
            s.storylines.Add(Goodbye(juror.id, "classy"));
            s.storylines.Add(Goodbye(skipped.id, "skip"));
            var read = Of(s, juror.id);
            Assert.That(read.knows, Does.Contain("They watched your goodbye: you kept it classy."));
            Assert.That(read.highlights, Does.Contain("Week 5 · they watched your goodbye: you kept it classy"));
            Assert.That(read.knows.Concat(read.highlights).Any(line => line.Contains("+") || line.Contains("view")), Is.False, "Never the effect it applied.");
            Assert.That(Of(s, skipped.id).knows.Any(line => line.Contains("goodbye")), Is.False, "A skipped message is no message.");
            Assert.That(Of(s, Juror(s, 2).id).highlights.Any(line => line.Contains("goodbye")), Is.False, "Only the juror it was recorded for.");

            s.storylines.Add(Goodbye(Juror(s, 2).id, "rub-it-in"));
            Assert.That(Of(s, Juror(s, 2).id).knows, Does.Contain("They watched your goodbye: you rubbed it in."));
        }

        [Test]
        public void TheCalloutIsTheirRecordedWordsDatedElseTheReason()
        {
            var s = FinalThree();
            var juror = Juror(s, 0);
            Read(s, juror.id, 3, 40);
            var reason = JuryHouseRead.Line(s, juror.id);
            Assert.That(reason.when, Is.Null);
            Assert.That(reason.quoted, Is.False, "The reason is the player's evidence, never their words.");
            Assert.That(reason.Text, Is.EqualTo(Of(s, juror.id).reason));

            // A plea somebody else heard in private is not one the player can quote.
            s.events.Add(new EpisodeEvent { sequence = 900, week = 4, kind = "eviction-speech", text = juror.name + ": Not for you.", audienceIds = new List<string> { Juror(s, 1).id } });
            Assert.That(JuryHouseRead.Line(s, juror.id).quoted, Is.False);
            s.events.Add(new EpisodeEvent { sequence = 901, week = 5, kind = "eviction-speech", text = juror.name + ": I've played fair. Keep me." });
            var plea = JuryHouseRead.Line(s, juror.id);
            Assert.That(plea.when, Is.EqualTo("Week 5, from the block"));
            Assert.That(plea.Text, Is.EqualTo("Week 5, from the block: “I've played fair. Keep me.”"));

            // At the Final 2 their question comes first, with the engine's note once answered.
            var exchange = new JuryExchangeState { questionerId = juror.id, finalistId = s.playerId, question = "Why did you put me up?",
                category = FinaleQuestions.Ownership, receiptKind = FinaleQuestions.PowerReceipt, receiptId = "4" };
            s.juryExchanges.Add(exchange);
            var tonight = JuryHouseRead.Line(s, juror.id);
            Assert.That(tonight.when, Is.EqualTo("Tonight"));
            Assert.That(tonight.note, Is.Null, "Nothing says how an answer will land before it is given.");
            Assert.That(tonight.Text, Is.EqualTo("Tonight: “Why did you put me up?”"));
            Assert.That(JuryHouseRead.Read(s).highlights.First(), Is.EqualTo("Tonight · " + JuryHouseRead.ShortName(s, juror.id) + " asked about week 4"));
            exchange.answerChoice = FinaleQuestions.Own; exchange.completed = true;
            string note = FinaleQuestions.Note(juror.name, FinaleQuestions.Landed(s, exchange));
            Assert.That(JuryHouseRead.Line(s, juror.id).Text, Is.EqualTo("Tonight: “Why did you put me up?” " + note));
            Assert.That(JuryHouseRead.Read(s).highlights.First(), Is.EqualTo("Tonight · " + JuryHouseRead.ShortName(s, juror.id) + " asked about week 4 · "
                + note.Substring(juror.name.Length + 1).TrimEnd('.')));

            // The catalogue's question, without the finale rules: its own note, and no receipt.
            var other = Juror(s, 1);
            s.juryExchanges.Add(new JuryExchangeState { questionerId = other.id, finalistId = s.playerId, question = "Who are you?", optionA = "a", optionB = "b",
                correctChoice = "A", answerChoice = "A", completed = true });
            Assert.That(JuryHouseRead.Line(s, other.id).note, Is.EqualTo(other.name + " was impressed by your response during jury questioning."));
            Assert.That(JuryHouseRead.Read(s).highlights, Does.Contain("Tonight · " + JuryHouseRead.ShortName(s, other.id)
                + " asked you a question · was impressed by your response during jury questioning"));

            foreach (var j in FinalistRead.Jurors(s))
                Assert.That(JuryHouseRead.Line(s, j.id).Text, Does.Not.Contain("feel"), "Never worded as how they feel about you.");
        }

        [Test]
        public void TheHouseMergesEveryJurorsLinesNewestFirstByName()
        {
            var s = FinalThree();
            var a = Juror(s, 0); var b = Juror(s, 1); var c = Juror(s, 2);
            a.name = "Casey Lee"; b.name = "Robin Diaz"; c.name = "Casey Moore";
            Read(s, a.id, 3, 40);
            s.ledger.ballots.Add(new BallotRow { week = 5, voterId = s.playerId, targetId = b.id });
            Assert.That(JuryHouseRead.ShortName(s, a.id), Is.EqualTo("Casey Lee"), "Two Caseys on the jury: full names.");
            Assert.That(JuryHouseRead.ShortName(s, b.id), Is.EqualTo("Robin"));
            Assert.That(JuryHouseRead.Read(s).highlights, Is.EqualTo(new[] { "Week 5 · Robin · you voted to evict them", "Week 3 · Casey Lee · your read: warm on you" }));
        }

        [Test]
        public void EveryThemeHasAValueToNameInWhatMatters()
        {
            var values = FinalArgument.Themes.Select(FinalArgument.Value).ToList();
            Assert.That(values, Has.None.Null);
            Assert.That(values.All(value => value.Length > 0), Is.True);
            Assert.That(values.Distinct().Count(), Is.EqualTo(FinalArgument.Themes.Length));
            Assert.That(FinalArgument.Value("unknown"), Is.Null);
        }

        [Test]
        public void ReadingTheJuryChangesNothing()
        {
            var s = FinalThree();
            foreach (var j in FinalistRead.Jurors(s)) Read(s, j.id, 4, 33);
            s.ledger.power.Add(new PowerRow { week = 5, hohId = s.playerId, nominees = FinalistRead.Jurors(s).Take(2).Select(j => j.id).ToList() });
            string before = JsonConvert.SerializeObject(s);
            uint random = s.randomState;
            JuryHouseRead.Read(s);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            Assert.That(s.randomState, Is.EqualTo(random));
        }
    }
}
