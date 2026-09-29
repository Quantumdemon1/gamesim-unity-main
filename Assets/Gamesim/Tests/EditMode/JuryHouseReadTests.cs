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
