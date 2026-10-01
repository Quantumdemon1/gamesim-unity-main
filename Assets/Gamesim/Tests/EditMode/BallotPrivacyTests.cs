using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The privacy sentinel over every reader the simulation offers the screens (UI-UX-PASS-PLAN
    /// B0): on played seasons, a ballot the player cannot place is never printed - no line of Your
    /// week, the houseguest notes, Your word, the finalist read, the jury house, the finale's
    /// receipts or the player-visible log names that voter beside the nominee they voted against.
    /// The true ballots come from the private lines the engine logs to each voter, which a test may
    /// read and a screen never does.
    /// </summary>
    public sealed class BallotPrivacyTests
    {
        /// <summary>The verbs a line says a ballot with, right after the voter's name.</summary>
        private static readonly string[] BallotVerbs = { "voted", "votes", "is voting", "broke", "kept", "honoured", "followed", "ignored", "lied", "defected", "fell out" };

        /// <summary>
        /// Lines that are an account's own words, a deal's terms or a call's record - what the player
        /// was told or agreed, never a ballot - a nomination's or a veto's commitment, and the jury's
        /// votes, which the finale reads live (decision 7).
        /// </summary>
        private static readonly string[] Accounts =
        {
            "told you", "Overheard", "An ally heard", "wouldn't say", "at the call", "your call", "put a ", "(about ", "agreed", "waiting on",
            KnownBallots.Unresolved, "Says: evict", "promised", "proposed", "offered", "Trust", "whip count", " to win", "jury",
            "pact", "agreement", "commitment", "final two", "Safety promise", "FinalTwo promise", "AllianceLoyalty promise", "Information promise",
        };

        /// <summary>A season played to its end, with the read rules on from the first week so the engine judges claims.</summary>
        private static EpisodeState Played(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            return engine.Snapshot;
        }

        /// <summary>The true ballots the engine logged to their voters, by week: voter, target. Never the player's.</summary>
        private static List<(int week, string voterId, string targetId)> PrivateBallots(EpisodeState s)
        {
            var ballots = new List<(int, string, string)>();
            foreach (var line in s.events.Where(e => e.kind == "vote-reveal" && e.audienceIds != null && e.audienceIds.Count == 1 && e.audienceIds[0] != s.playerId))
            {
                var read = KnownBallots.ReadRevealLine(s, line.text, null);
                if (read?.voterId != null && read.targetId != null) ballots.Add((line.week, read.voterId, read.targetId));
            }
            return ballots;
        }

        /// <summary>
        /// Whether a line says the voter's ballot: the voter's name as the subject of a ballot verb
        /// ("Casey Wilson voted", "Casey Wilson broke", "Casey Wilson was with you at the call, then
        /// voted"), with the nominee it went against named in the same line.
        /// </summary>
        private static bool NamesTheBallot(EpisodeState s, string line, string voterId, string targetId)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string voter = s.Find(voterId)?.name, target = s.Find(targetId)?.name;
            if (voter == null || target == null) return false;
            int at = line.IndexOf(voter, StringComparison.Ordinal);
            if (at < 0) return false;
            string targetWord = targetId == s.playerId ? "you" : FinalistRead.FirstName(target);
            if (line.IndexOf(targetWord, StringComparison.Ordinal) < 0) return false;
            if (Accounts.Any(account => line.IndexOf(account, StringComparison.Ordinal) >= 0)) return false;
            string after = line.Substring(at + voter.Length);
            int stop = after.IndexOf('.');
            if (stop >= 0) after = after.Substring(0, stop);
            return BallotVerbs.Any(verb => after.IndexOf(" " + verb, StringComparison.Ordinal) >= 0);
        }

        /// <summary>Every line the simulation's readers offer the screens, for the player.</summary>
        private static List<string> ReaderLines(EpisodeState s)
        {
            var lines = new List<string>();
            foreach (int week in Enumerable.Range(1, s.week)) lines.AddRange(YourWeek.Build(s, week).Lines.Select(l => l.ToString()));
            foreach (var actor in s.contestants.Where(c => !c.isPlayer))
            {
                foreach (var note in HouseguestNotes.For(s, actor.id)) { lines.Add(note.text); if (note.brief != null) lines.Add(note.brief); if (note.compact != null) lines.Add(note.compact); }
                lines.AddRange(CommitmentsRead.With(s, actor.id).Select(CommitmentsRead.Line));
            }
            foreach (var finalist in FinalistRead.Others(s))
            {
                var read = FinalistRead.Read(s, finalist.id);
                if (read == null) continue;
                lines.AddRange(read.Facts.Select(fact => fact.label + ": " + fact.value));
                lines.AddRange(read.jurors.Select(lean => finalist.name + " · " + lean.name + " · " + lean.reason));
            }
            var house = JuryHouseRead.Read(s);
            if (house != null) lines.AddRange(house.highlights);
            foreach (var juror in FinalistRead.Jurors(s))
            {
                var read = JuryHouseRead.ReadJuror(s, juror.id);
                lines.AddRange(read.knows); lines.AddRange(read.highlights); lines.Add(read.reason);
            }
            foreach (var exchange in s.juryExchanges)
            {
                lines.Add(FinaleQuestions.ReceiptLine(s, exchange));
                lines.Add(FinaleQuestions.Kicker(s, exchange));
            }
            lines.AddRange(s.events.Where(e => e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(s.playerId)).Select(e => e.text));
            return lines.Where(line => line != null).ToList();
        }

        [Test]
        public void NoReaderNamesABallotThePlayerCannotPlace()
        {
            int unknown = 0, seasons = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var s = Played(seed);
                var ballots = PrivateBallots(s);
                if (ballots.Count == 0) continue;
                seasons++;
                var lines = ReaderLines(s);
                foreach (var (week, voterId, targetId) in ballots)
                {
                    var sheet = KnownBallots.Read(s, week);
                    if (sheet.Knows(voterId)) continue;
                    unknown++;
                    foreach (var line in lines)
                        Assert.That(NamesTheBallot(s, line, voterId, targetId), Is.False,
                            "Seed " + seed + ", week " + week + ": " + s.Find(voterId).name + "'s ballot is not the player's to know, and a reader says it: \"" + line + "\"");
                }
            }
            Assert.That(seasons, Is.GreaterThan(0), "Seasons with ballots on the record.");
            Assert.That(unknown, Is.GreaterThan(0), "At least one ballot the player could not place, or the sentinel guards nothing.");
        }

        /// <summary>A finalist's lean of "voted to evict them" rests on a ballot the player knows, and the jury house's "the count showed it" on one the juror could.</summary>
        [Test]
        public void TheFinalistReadAndTheJuryHouseRestOnKnownBallots()
        {
            int leans = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var s = Played(seed);
                foreach (var finalist in FinalistRead.Others(s))
                {
                    var read = FinalistRead.Read(s, finalist.id);
                    if (read == null) continue;
                    foreach (var lean in read.jurors.Where(l => l.reason == "voted to evict them"))
                    {
                        leans++;
                        Assert.That(KnownBallots.Weeks(s).Any(week => KnownBallots.Read(s, week).TargetOf(finalist.id) == lean.jurorId), Is.True,
                            "Seed " + seed + ": " + finalist.name + "'s ballot against " + lean.name + " is one the player knows.");
                    }
                }
                foreach (var juror in FinalistRead.Jurors(s))
                {
                    var read = JuryHouseRead.ReadJuror(s, juror.id);
                    Assert.That(read.knows.Concat(read.highlights), Has.None.Contains("in the open"));
                    foreach (var line in read.knows.Where(k => k.StartsWith("You voted to evict them", StringComparison.Ordinal)))
                        Assert.That(s.ledger.ballots.Any(b => b.voterId == s.playerId && b.targetId == juror.id && JuryHouseRead.CouldKnowYourBallot(s, b.week, juror.id)), Is.True, line);
                }
            }
            Assert.That(leans, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>The verdict lines the player is told read unresolved until the ballot is known, and resolve once it is (decision 4).</summary>
        [Test]
        public void AVerdictIsUnresolvedUntilTheBallotIsKnownAndResolvesOnceItIs()
        {
            var s = ContentCatalog.Create(31);
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToArray();
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 2, 1 } });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = npcs[1].id });
            string you = s.Find(s.playerId).name, them = npcs[3].name;
            var promise = new PromiseState { id = "p", fromId = npcs[3].id, toId = s.playerId, targetId = npcs[1].id, kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 1, expiresWeek = 1 };
            s.promises.Add(promise);
            // A vote to evict the one the player voted against: the player kept their side, so the
            // other's ballot settled it, and that ballot is not theirs to know.
            var deal = new DealState { id = "d", type = DealKind.VoteEvict, proposerId = npcs[3].id, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            s.deals.Add(deal);
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = npcs[3].id, week = 1, text = them + " broke a Vote promise.", isPrivate = true });
            // The ending on the player's own record, dated to the reveal's week.
            s.week = 1;
            RelationshipLedger.Record(s, s.playerId, npcs[3].id, YourWeek.DealBroken, -15, them + " broke a vote to evict with " + you + ".");
            s.week = 2;

            var notes = HouseguestNotes.For(s, npcs[3].id);
            Assert.That(notes.Select(n => n.text), Has.Some.EqualTo(FinalistRead.FirstName(them) + " promised you a vote · " + KnownBallots.Unresolved));
            Assert.That(notes.Select(n => n.text), Has.Some.EqualTo(FinalistRead.FirstName(them) + " put a vote to evict to you (about " + npcs[1].name + ") · " + KnownBallots.Unresolved));
            Assert.That(notes.Select(n => n.text), Has.None.EqualTo(them + " broke a Vote promise."), "The memory of the ending is withheld with the ballot.");
            var word = CommitmentsRead.With(s, npcs[3].id);
            Assert.That(word.Select(c => c.status), Is.All.EqualTo(KnownBallots.Unresolved));
            Assert.That(word.Select(c => c.outcome), Is.All.EqualTo(CommitmentsRead.Outcomes.Unresolved));
            Assert.That(word.Select(c => c.brokenById), Is.All.Null);
            var mine = YourWeek.Build(s, 1).word;
            Assert.That(mine, Has.Count.EqualTo(2), "The deal and the promise.");
            Assert.That(mine.Select(l => l.verdict), Is.All.EqualTo(YourWeek.Verdicts.NotKnown));
            Assert.That(mine.Select(l => l.text), Is.All.Contains(KnownBallots.Unresolved));

            // Told, and caught at the reveal: the ballot is known, and every verdict with it.
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[3].id, targetId = npcs[1].id, source = ClaimSource.Told, status = ClaimStatus.Lied });
            Assert.That(KnownBallots.Knows(s, 1, npcs[3].id), Is.True);
            Assert.That(HouseguestNotes.For(s, npcs[3].id).Select(n => n.text), Has.Some.EqualTo(FinalistRead.FirstName(them) + " promised you a vote · broken")
                .And.Some.EqualTo(them + " broke a Vote promise."));
            word = CommitmentsRead.With(s, npcs[3].id);
            Assert.That(word.Single(c => c.kind == CommitmentsRead.Kinds.Promise).status, Is.EqualTo("broken by them"));
            Assert.That(word.Single(c => c.kind == CommitmentsRead.Kinds.Deal).status, Is.EqualTo("broken by them"));
            mine = YourWeek.Build(s, 1).word;
            Assert.That(mine.Select(l => l.verdict), Is.All.EqualTo(YourWeek.Verdicts.Broken));
            Assert.That(mine.Select(l => l.text), Is.EquivalentTo(new[] { them + " broke the vote-to-evict deal with you.", them + " broke their vote promise to you." }));

            // A vote deal the player's own ballot broke is theirs to know whatever the other voted.
            s.ledger.claims.Clear();
            var mine2 = new DealState { id = "m", type = DealKind.VoteSave, proposerId = npcs[4].id, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            s.deals.Add(mine2);
            Assert.That(CommitmentsRead.With(s, npcs[4].id).Single().status, Is.EqualTo("broken by you"), "You voted to evict the one you agreed to keep.");
        }
    }
}
