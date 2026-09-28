using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The verdict (STRATEGY-LOOP-PLAN.md §5): Game Sense is 0-100 with three faces, every point
    /// traces to a ledger row, a win against the odds counts more than a win with them, a chance
    /// let pass costs, and the house's view of the player is the social face, not the player's
    /// view of the house.
    /// </summary>
    public sealed class GameSenseTests
    {
        private static EpisodeState Fresh(uint seed = 31) => ContentCatalog.Create(seed);

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        [Test]
        public void AnEmptyLedgerIsTheMiddleOfEveryFace()
        {
            var s = Fresh();
            foreach (var npc in s.contestants.Where(c => !c.isPlayer)) SetScore(s, npc.id, s.playerId, 0);
            var report = GameSense.Evaluate(s);
            Assert.That(report.competitions, Is.EqualTo(50));
            Assert.That(report.strategy, Is.EqualTo(50));
            Assert.That(report.social, Is.EqualTo(50));
            Assert.That(report.score, Is.EqualTo(50));
            Assert.That(report.moments, Is.Empty);
            Assert.That(report.missed, Is.Empty);
        }

        [Test]
        public void AWinAgainstTheOddsCountsMoreThanAWinWithThem()
        {
            var s = Fresh();
            s.ledger.competitions.Add(new CompetitionRow { week = 1, kind = "HoH", field = 6, placement = 1, entry = CompetitionEntry.Played, performance = 0.8, expectedWin = 0.15 });
            var underdog = GameSense.Evaluate(s).competitions;
            s.ledger.competitions[0].expectedWin = 0.7;
            var favourite = GameSense.Evaluate(s).competitions;
            Assert.That(underdog, Is.GreaterThan(favourite), "Beating one-in-seven odds is worth more than beating even ones.");
            Assert.That(underdog, Is.EqualTo(50 + 26).Within(1), "(1 - 0.15) x 30, rounded.");
            var note = GameSense.Evaluate(s).notes.Single(n => n.face == GameSense.Competitions);
            Assert.That(note.rowKind, Is.EqualTo("competition"));
            Assert.That(note.text, Does.Contain("Week 1").And.Contain("won the HoH"));
        }

        [Test]
        public void AThrowThatServedAPurposeAndOneThatPutYouUp()
        {
            var s = Fresh();
            s.week = 2;
            var npc = s.contestants.First(c => !c.isPlayer);
            s.ledger.competitions.Add(new CompetitionRow { week = 1, kind = "HoH", field = 6, placement = 6, entry = CompetitionEntry.Thrown });
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npc.id, evicteeId = npc.id, nominees = new List<string> { npc.id, s.contestants[2].id } });
            Assert.That(GameSense.Evaluate(s).notes.Single(n => n.rowKind == "competition").points, Is.EqualTo(8), "Thrown, and off the block: it served.");
            s.ledger.power[0].nominees[0] = s.playerId;
            Assert.That(GameSense.Evaluate(s).notes.Single(n => n.rowKind == "competition").points, Is.EqualTo(-12), "Thrown, and on the block: it cost.");
        }

        [Test]
        public void TheStrategyFaceWeighsWhatWasDoneAgainstWhatWasOffered()
        {
            var s = Fresh();
            s.week = 3;
            var npc = s.contestants.First(c => !c.isPlayer);
            s.ledger.opportunities.Add(new OpportunityRow { id = "play-1", kind = OpportunityKinds.Play, week = 1, source = "the-secret-alliance", response = OpportunityResponse.Taken, outcome = OpportunityOutcome.Won });
            s.ledger.opportunities.Add(new OpportunityRow { id = "play-2", kind = OpportunityKinds.Play, week = 2, source = "stay-off-the-block", response = OpportunityResponse.Expired, outcome = OpportunityOutcome.NotApplicable });
            s.ledger.opportunities.Add(new OpportunityRow { id = "deal-1", kind = OpportunityKinds.Deal, week = 2, source = "vote_evict:" + npc.id, response = OpportunityResponse.Taken, outcome = OpportunityOutcome.Lost });
            s.ledger.opportunities.Add(new OpportunityRow { id = "read-2", kind = OpportunityKinds.Read, week = 2, response = OpportunityResponse.Ignored, outcome = OpportunityOutcome.NotApplicable });
            var report = GameSense.Evaluate(s);
            var strategy = report.notes.Where(n => n.face == GameSense.Strategy).ToList();
            Assert.That(strategy.Single(n => n.rowId == "play-1").points, Is.EqualTo(6), "A play won.");
            Assert.That(strategy.Single(n => n.rowId == "play-2").points, Is.EqualTo(-2), "A play let pass.");
            Assert.That(strategy.Single(n => n.rowId == "deal-1").points, Is.EqualTo(-5), "A deal broken.");
            Assert.That(strategy.Single(n => n.rowId == "read-2").points, Is.EqualTo(-2), "A vote nobody was asked about.");
            Assert.That(report.strategy, Is.EqualTo(50 + 6 - 2 - 5 - 2));
            Assert.That(report.moments.Select(n => n.rowId), Does.Contain("play-1"), "The moment that made the difference.");
            Assert.That(report.missed.Select(n => n.rowId), Does.Contain("deal-1").And.Contain("play-2"), "and the chances missed.");
        }

        [Test]
        public void TheReadsAccuracyAndTheWeeksPowerAreScored()
        {
            var s = Fresh();
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 3, 1 } });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = npcs[1].id, readBefore = npcs[1].id, correct = true });
            var right = GameSense.Evaluate(s);
            Assert.That(right.notes.Single(n => n.rowKind == "ballot" && n.text.Contains("whip count")).points, Is.EqualTo(5), "A whip count that was right.");
            Assert.That(right.notes.Single(n => n.rowKind == "ballot" && n.text.Contains("with the house")).points, Is.EqualTo(2));
            Assert.That(right.notes.Single(n => n.rowKind == "power").points, Is.EqualTo(3), "Off the block, no wins yet.");
            s.ledger.ballots[0].readBefore = npcs[2].id; s.ledger.ballots[0].correct = false;
            var wrong = GameSense.Evaluate(s);
            Assert.That(wrong.notes.Single(n => n.rowKind == "ballot" && n.text.Contains("whip count")).points, Is.EqualTo(-5), "and one that was wrong.");
            Assert.That(wrong.strategy, Is.LessThan(right.strategy));
        }

        [Test]
        public void TheSocialFaceIsHowTheHouseSeesYou()
        {
            var s = Fresh();
            foreach (var npc in s.contestants.Where(c => !c.isPlayer)) { SetScore(s, npc.id, s.playerId, 40); SetScore(s, s.playerId, npc.id, -80); }
            var liked = GameSense.Evaluate(s);
            Assert.That(liked.social, Is.EqualTo(70), "Their view of you, +40 across the house, not yours of them.");
            foreach (var npc in s.contestants.Where(c => !c.isPlayer)) SetScore(s, npc.id, s.playerId, -40);
            Assert.That(GameSense.Evaluate(s).social, Is.EqualTo(30));
            // A juror's view is the one they left with, whatever the score says now.
            var juror = s.contestants.First(c => !c.isPlayer);
            juror.status = ContestantStatus.Jury;
            s.ledger.standings.Add(new StandingRow { week = 1, fromId = juror.id, toId = s.playerId, source = ClaimSource.Juror, score = 60 });
            var remembered = GameSense.Evaluate(s);
            Assert.That(remembered.social, Is.GreaterThan(30), "The juror left liking you, and that is what counts.");
        }

        [Test]
        public void AllianceEndingsBondsAndGrudgesMoveTheSocialFace()
        {
            var s = Fresh();
            s.week = 4;
            var npc = s.contestants.First(c => !c.isPlayer);
            s.alliances.Add(new AllianceState { id = "alliance-1", name = "The Pair", members = new List<string> { s.playerId, npc.id }, active = false });
            s.ledger.alliances.Add(new AllianceRow { id = "alliance-1", startedWeek = 1, endedWeek = 3, why = "player/turned" });
            var report = GameSense.Evaluate(s);
            Assert.That(report.notes.Single(n => n.rowKind == "alliance").points, Is.EqualTo(-6), "An alliance that turned on you.");
            s.ledger.alliances[0].endedWeek = 0; s.alliances[0].active = true;
            Assert.That(GameSense.Evaluate(s).notes.Single(n => n.rowKind == "alliance").points, Is.EqualTo(4), "One that held.");
        }

        [Test]
        public void AFinishedSeasonHasAVerdictThatTracesToRows()
        {
            var engine = new EpisodeEngine(Fresh(31));
            for (int i = 0; i < 900 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Finished));
            var report = GameSense.Evaluate(s);
            Assert.That(report.score, Is.InRange(0, 100));
            Assert.That(report.notes, Is.Not.Empty, "A whole season leaves something to score.");
            Assert.That(report.notes.All(n => !string.IsNullOrEmpty(n.rowKind) && !string.IsNullOrEmpty(n.text)), Is.True, "Every point traces to a row, in words.");
            Assert.That(report.moments.Count, Is.LessThanOrEqualTo(3));
            Assert.That(report.missed.Count, Is.LessThanOrEqualTo(3));
            var again = GameSense.Evaluate(s);
            Assert.That(again.score, Is.EqualTo(report.score), "The referee is deterministic.");
            Assert.That(again.notes.Select(n => n.text), Is.EqualTo(report.notes.Select(n => n.text)));
        }
    }
}
