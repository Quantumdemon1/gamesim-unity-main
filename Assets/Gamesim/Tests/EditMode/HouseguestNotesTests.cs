using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The notes the notebook keeps on each houseguest: every line a row the engine wrote, newest
    /// first, with a few words for a card; the filters by kind; nothing for the player or for
    /// somebody you have nothing on.
    /// </summary>
    public sealed class HouseguestNotesTests
    {
        private static EpisodeState Season(uint seed = 21)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            s.strategyRulesStartWeek = 1; s.blocRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableAgency(s);
            s.week = 4;
            return s;
        }

        private static ContestantState Npc(EpisodeState s, int index) => s.contestants.Where(c => !c.isPlayer).ElementAt(index);

        [Test]
        public void EveryKindOfRecordBecomesALineNewestFirst()
        {
            var s = Season();
            var a = Npc(s, 0); var b = Npc(s, 1); var c = Npc(s, 2);
            string first = a.name.Split(' ')[0];
            s.promises.Add(new PromiseState { id = "p1", fromId = a.id, toId = s.playerId, kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 2 });
            s.deals.Add(new DealState { id = "deal-ask-1", type = DealKind.Partnership, proposerId = a.id, recipientId = s.playerId, status = DealStatus.Proposed, week = 4 });
            s.ledger.claims.Add(new ClaimRow { week = 3, voterId = a.id, targetId = b.id, source = ClaimSource.Told, status = ClaimStatus.Lied });
            s.ledger.standings.Add(new StandingRow { week = 1, fromId = a.id, toId = s.playerId, source = ClaimSource.Read, score = 30 });
            s.ledger.replies.Add(new ReplyRow { week = 3, cardId = "reply-1", kind = ReplyCards.Confrontation, fromId = a.id, replyKey = "apologize", toThem = 10 });
            s.ledger.calls.Add(new BlocCallRow { week = 2, allianceId = "alliance-x", callerId = s.playerId, targetId = c.id, followed = new List<string> { a.id } });
            s.alliances.Add(new AllianceState { id = "alliance-x", name = "The Pact", members = new List<string> { s.playerId, a.id }, active = true });
            s.ledger.alliances.Add(new AllianceRow { id = "alliance-x", startedWeek = 2 });
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = a.id, week = 1, text = first + " talked about me to somebody in week 1.", isPrivate = true });

            var notes = HouseguestNotes.For(s, a.id);
            Assert.That(notes.Select(n => n.week), Is.Ordered.Descending, "Newest first.");
            Assert.That(notes.Count, Is.EqualTo(8));
            var texts = notes.Select(n => n.text).ToList();
            Assert.That(texts, Has.Some.EqualTo(first + " promised you safety · broken"));
            Assert.That(texts, Has.Some.EqualTo(first + " put a " + DealKind.Title(DealKind.Partnership).ToLowerInvariant() + " to you · waiting on you"));
            Assert.That(texts, Has.Some.EqualTo(first + " told you: evict " + b.name + " · a lie"));
            Assert.That(texts, Has.Some.EqualTo("You read " + first + ": warm on you"), "An older read carries no agenda.");
            Assert.That(texts, Has.Some.EqualTo(first + " confronted you · you: apologize"));
            Assert.That(texts, Has.Some.EqualTo(first + " followed your call to evict " + c.name));
            Assert.That(texts, Has.Some.EqualTo("You are both in The Pact"));
            Assert.That(texts, Has.Some.EqualTo(first + " talked about me to somebody in week 1."));
            Assert.That(HouseguestNotes.Brief(s, a.id), Is.EqualTo("An offer waiting on you"), "The card says the latest thing.");

            Assert.That(notes.Where(HouseguestNotes.IsTheirWord).Select(n => n.kind), Is.EquivalentTo(new[] { HouseguestNotes.Kinds.Offer, HouseguestNotes.Kinds.Word, HouseguestNotes.Kinds.Word, HouseguestNotes.Kinds.Pact }));
            Assert.That(notes.Where(HouseguestNotes.IsTheVote).Select(n => n.text), Is.EqualTo(new[] { first + " told you: evict " + b.name + " · a lie" }));
            Assert.That(notes.Where(HouseguestNotes.IsYourRead).Select(n => n.kind), Is.EquivalentTo(new[] { HouseguestNotes.Kinds.Came, HouseguestNotes.Kinds.Read, HouseguestNotes.Kinds.Memory }));
        }

        [Test]
        public void AReadThisWeekSaysWhatTheyAreUpTo()
        {
            var s = Season();
            var a = Npc(s, 0);
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            s.ledger.standings.Add(new StandingRow { week = s.week, fromId = a.id, toId = s.playerId, source = ClaimSource.Read, score = -30 });
            var agenda = NpcAgendas.Of(s, a.id);
            Assume.That(agenda, Is.Not.Null);
            var read = HouseguestNotes.For(s, a.id).Single(n => n.kind == HouseguestNotes.Kinds.Read);
            Assert.That(read.text, Is.EqualTo("You read " + a.name.Split(' ')[0] + ": cold on you. " + NpcAgendas.Describe(s, a.id, agenda)));
            Assert.That(read.brief, Is.EqualTo("Read: cold on you"));
        }

        [Test]
        public void MissesDeflectionsAndOverheardClaimsHaveTheirOwnWords()
        {
            var s = Season();
            var a = Npc(s, 0); var b = Npc(s, 1);
            string first = a.name.Split(' ')[0];
            s.ledger.standings.Add(new StandingRow { week = 2, fromId = a.id, toId = s.playerId, source = ClaimSource.Missed });
            s.ledger.standings.Add(new StandingRow { week = 3, fromId = a.id, toId = s.playerId, source = ClaimSource.Deflected });
            s.ledger.claims.Add(new ClaimRow { week = 3, voterId = a.id, targetId = b.id, source = ClaimSource.Overheard, status = ClaimStatus.Kept });
            var texts = HouseguestNotes.For(s, a.id).Select(n => n.text).ToList();
            Assert.That(texts, Is.EqualTo(new[] { "Overheard: " + first + " is voting out " + b.name + " · and did", first + " wouldn't say where their vote is", "You couldn't get a read on " + first }));
            Assert.That(HouseguestNotes.Brief(s, a.id), Is.EqualTo("Told you the truth about the vote"));
        }

        [Test]
        public void NothingOnThePlayerOrOnSomebodyUntouched()
        {
            var s = Season();
            Assert.That(HouseguestNotes.For(s, s.playerId), Is.Empty);
            Assert.That(HouseguestNotes.For(s, Npc(s, 3).id), Is.Empty, "A fresh season: nothing has happened between you.");
            Assert.That(HouseguestNotes.Brief(s, Npc(s, 3).id), Is.Null);
            Assert.That(HouseguestNotes.For(s, "nobody"), Is.Empty);
            Assert.That(HouseguestNotes.For(null, "nobody"), Is.Empty);
        }

        [Test]
        public void TheWordsForWhereThingsStand()
        {
            Assert.That(HouseguestNotes.PromiseStanding(PromiseStatus.Fulfilled), Is.EqualTo("kept"));
            Assert.That(HouseguestNotes.PromiseStanding(PromiseStatus.Active), Is.EqualTo("still standing"));
            Assert.That(HouseguestNotes.DealStanding(DealStatus.Proposed, true), Is.EqualTo("waiting on you"));
            Assert.That(HouseguestNotes.DealStanding(DealStatus.Proposed, false), Is.EqualTo("waiting on them"));
            Assert.That(HouseguestNotes.DealStanding(DealStatus.Declined, true), Is.EqualTo("you declined"));
            Assert.That(HouseguestNotes.DealStanding(DealStatus.Fulfilled, true), Is.EqualTo("honoured"));
            Assert.That(HouseguestNotes.PromiseWord(PromiseKind.FinalTwo), Is.EqualTo("a final two"));
        }
    }
}
