using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The ledger's writers (STRATEGY-LOOP-PLAN.md §6): a season leaves a record of every competition
    /// with the odds the player had, every week's power, every alliance from its start to its end,
    /// each juror's view of the player as they left, and every chance offered — deals, pleas, plays,
    /// the read — with what came of it. The verdict reads these rows, so they must be there.
    /// </summary>
    public sealed class SeasonLedgerWriterTests
    {
        private static EpisodeEngine TryReach(EpisodeState start, Func<EpisodeState, bool> until, int limit = 900)
        {
            var engine = new EpisodeEngine(start);
            for (int i = 0; i < limit && !until(engine.Snapshot); i++)
                if (!engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted) return null;
            return until(engine.Snapshot) ? engine : null;
        }

        private static EpisodeEngine Reach(uint seed, Func<EpisodeState, bool> until, int limit = 900)
        {
            var engine = TryReach(ContentCatalog.Create(seed), until, limit);
            Assert.That(engine, Is.Not.Null, "The season never reached the state the test needs.");
            return engine;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string targetId = null, string secondTargetId = null, string text = null) =>
            new EpisodeCommand
            {
                id = "ledger-" + kind + "-" + s.revision + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId, kind = kind,
                targetId = targetId, secondTargetId = secondTargetId, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        [Test]
        public void ACompetitionIsOnTheRecordWithTheOddsThePlayerHad()
        {
            var s = Reach(31, x => x.phase == EpisodePhase.HoH && x.competitionResolved).Snapshot;
            var row = s.ledger.competitions.Single();
            Assert.That(row.week, Is.EqualTo(1));
            Assert.That(row.kind, Is.EqualTo("HoH"));
            Assert.That(row.field, Is.EqualTo(s.competitionScores.Count), "The field is everybody who played.");
            Assert.That(row.placement, Is.InRange(1, row.field), "The player placed somewhere in it.");
            Assert.That(row.entry, Is.EqualTo(CompetitionEntry.Played));
            Assert.That(row.performance, Is.EqualTo(0.5), "The walker competes at half effort.");
            Assert.That(row.expectedWin, Is.GreaterThan(0).And.LessThan(1), "The odds are odds, not a certainty.");
            bool won = s.hohId == s.playerId;
            Assert.That(row.placement == 1, Is.EqualTo(won), "First place is the win.");
        }

        [Test]
        public void ExpectedWinsShareTheFieldAndFavourTheStronger()
        {
            var s = ContentCatalog.Create(31);
            var players = EpisodeEngine.CompetitionPlayers(s).ToList();
            double total = players.Sum(p => EpisodeEngine.ExpectedWin(s, players, CompetitionRules.Mental, p.id));
            Assert.That(total, Is.EqualTo(1).Within(1e-9), "The field's chances sum to one.");
            var twin = players.Select(p => { var c = p.Clone(); c.stats = players[0].stats; return c; }).ToList();
            foreach (var p in twin) Assert.That(EpisodeEngine.ExpectedWin(s, twin, CompetitionRules.Mental, p.id), Is.EqualTo(1.0 / twin.Count).Within(1e-9), "A field of equals is one in n.");
            var strongest = players.OrderByDescending(p => p.stats.mental).First();
            var weakest = players.OrderBy(p => p.stats.mental).First();
            if (strongest.stats.mental > weakest.stats.mental)
                Assert.That(EpisodeEngine.ExpectedWin(s, players, CompetitionRules.Mental, strongest.id), Is.GreaterThan(EpisodeEngine.ExpectedWin(s, players, CompetitionRules.Mental, weakest.id)));
        }

        [Test]
        public void AWatchedCompetitionIsOnTheRecordToo()
        {
            for (uint seed = 1; seed < 40; seed++)
            {
                var engine = TryReach(SeasonBuilder.Create(new SeasonBuilder.Choice(), seed),
                    x => x.ledger.competitions.Any(c => c.entry == CompetitionEntry.Watched), 300);
                if (engine == null) continue;
                var row = engine.Snapshot.ledger.competitions.First(c => c.entry == CompetitionEntry.Watched);
                Assert.That(row.placement, Is.Zero, "Nobody places in a competition they watched.");
                Assert.That(row.expectedWin, Is.Zero);
                Assert.That(row.performance, Is.Zero);
                Assert.That(row.field, Is.GreaterThan(0));
                return;
            }
            Assert.Fail("No eight-house season had the player watch a competition within 300 commands.");
        }

        [Test]
        public void TheWeeksPowerIsOnTheRecordAtTheReveal()
        {
            var s = Reach(31, x => x.evictionResolved).Snapshot;
            var row = s.ledger.power.Single(p => p.week == s.week);
            Assert.That(row.hohId, Is.EqualTo(s.hohId));
            Assert.That(row.vetoHolderId, Is.EqualTo(s.vetoHolderId));
            Assert.That(row.nominees, Is.EqualTo(s.nominees), "The final block.");
            var juror = s.contestants.Single(c => c.status == ContestantStatus.Jury);
            Assert.That(row.evicteeId, Is.EqualTo(juror.id));
            Assert.That(row.tally.Sum(), Is.EqualTo(s.votes.Count(v => v.voterId != s.hohId)), "The tally is the house's votes, the Head of Household's tie-break aside.");
            bool used = s.events.Any(e => e.kind == "veto" && !e.text.Contains("declines") && !e.text.Contains("decline to use"));
            Assert.That(row.vetoUsed, Is.EqualTo(used), "Whether the veto was used matches what the house saw.");
            if (used) Assert.That(row.savedId, Is.Not.Null.And.Not.EqualTo(row.evicteeId), "Whoever it saved did not go.");
            Assert.That(row.backdoorTargetId, Is.Null, "No plan, no backdoor.");
        }

        [Test]
        public void AnAllianceIsOnTheRecordFromItsFormationToItsEnd()
        {
            var s = Reach(31, x => x.phase == EpisodePhase.Campaign && x.nominees.Count == 2).Snapshot;
            s.socialActions = 0; s.outOfPhaseSocialActions = 0;
            var friend = s.Active.First(c => !c.isPlayer && !s.Allied(s.playerId, c.id));
            SetScore(s, friend.id, s.playerId, 30);
            var engine = new EpisodeEngine(s);
            var formed = engine.Apply(Command(s, EpisodeCommandKind.FormAlliance, friend.id));
            Assert.That(formed.accepted, Is.True, formed.reason);
            var after = engine.Snapshot;
            var alliance = after.alliances.Last(a => a.active && a.members.Contains(after.playerId) && a.members.Contains(friend.id));
            var row = after.ledger.alliances.Single(r => r.id == alliance.id);
            Assert.That(row.startedWeek, Is.EqualTo(after.week));
            Assert.That(row.endedWeek, Is.Zero);
            Assert.That(row.why, Is.EqualTo("player"), "Where it came from.");
            var left = engine.Apply(Command(after, EpisodeCommandKind.LeaveAlliance, friend.id));
            Assert.That(left.accepted, Is.True, left.reason);
            row = engine.Snapshot.ledger.alliances.Single(r => r.id == alliance.id);
            Assert.That(row.endedWeek, Is.EqualTo(after.week), "and when it ended");
            Assert.That(row.why, Does.StartWith("player/"), "and why.");
        }

        [Test]
        public void AJurorsViewOfYouIsKeptAsTheyLeave()
        {
            var s = Reach(31, x => x.evictionResolved).Snapshot;
            var juror = s.contestants.Single(c => c.status == ContestantStatus.Jury);
            var row = s.ledger.standings.Single(r => r.source == ClaimSource.Juror);
            Assert.That(row.fromId, Is.EqualTo(juror.id));
            Assert.That(row.toId, Is.EqualTo(s.playerId));
            Assert.That(row.week, Is.EqualTo(s.week));
            Assert.That(row.score, Is.EqualTo(s.Score(juror.id, s.playerId)), "Their view of you, not yours of them.");
        }

        [Test]
        public void DealsPleasAndTheReadAreChancesOnTheRecord()
        {
            var s = Reach(31, x => x.phase == EpisodePhase.Campaign && x.nominees.Count == 2 && x.hohId != x.playerId).Snapshot;
            EpisodeEngine.EnableLevers(s); s.strategyRulesStartWeek = 1;
            var voter = EpisodeEngine.Voters(s).First(v => !v.isPlayer);
            var nominee = s.Find(s.nominees.First(id => id != s.playerId));
            s.deals.Add(new DealState
            {
                id = "deal-ask-test", type = DealKind.VoteSave, proposerId = nominee.id, recipientId = s.playerId, targetId = nominee.id,
                status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteSave),
            });
            s.lobbies.Add(new LobbyState
            {
                week = s.week, phase = EpisodePhase.Campaign, deciderId = voter.id, ask = LobbyAsk.Vote, subjectId = s.playerId,
                approach = LobbyApproach.Emotional, response = LobbyResponse.Receptive, influence = 50,
            });
            var engine = new EpisodeEngine(s);
            var declined = engine.Apply(Command(s, EpisodeCommandKind.RespondToDeal, "deal-ask-test", text: "decline"));
            Assert.That(declined.accepted, Is.True, declined.reason);
            int week = s.week;
            var later = TryReach(engine.Snapshot, x => x.week == week + 1, 60);
            Assert.That(later, Is.Not.Null, "The week turned.");
            var rows = later.Snapshot.ledger.opportunities;
            string present = "rows: " + string.Join(", ", rows.Select(o => o.kind + ":" + o.id + ":" + o.response));
            var deal = rows.SingleOrDefault(o => o.id == "deal-ask-test");
            Assert.That(deal, Is.Not.Null, present);
            Assert.That(deal.kind, Is.EqualTo(OpportunityKinds.Deal));
            Assert.That(deal.response, Is.EqualTo(OpportunityResponse.Declined));
            Assert.That(deal.outcome, Is.EqualTo(OpportunityOutcome.NotApplicable));
            var plea = rows.SingleOrDefault(o => o.kind == OpportunityKinds.Plea);
            Assert.That(plea, Is.Not.Null, present);
            Assert.That(plea.response, Is.EqualTo(OpportunityResponse.Taken));
            Assert.That(plea.outcome, Is.EqualTo(OpportunityOutcome.Won), "A receptive hearing is a plea won.");
            Assert.That(plea.payoff, Is.EqualTo(50));
            var read = rows.SingleOrDefault(o => o.id == "read-" + week);
            Assert.That(read, Is.Not.Null, present);
            Assert.That(read.kind, Is.EqualTo(OpportunityKinds.Read));
            Assert.That(read.response, Is.EqualTo(OpportunityResponse.Ignored), "Nothing was asked, read or overheard.");
        }

        [Test]
        public void APlayIsAChanceOnTheRecordWithItsStepsAndOutcome()
        {
            var s = Reach(31, x => x.phase == EpisodePhase.Campaign && x.nominees.Count == 2).Snapshot;
            var arc = StoryCatalog.All.First(a => a.play != null);
            s.storylines.Add(new StorylineState
            {
                id = "cycle-test", templateId = arc.id, title = arc.title, week = s.week, status = StorylineStatus.Completed, endingId = PlayEndings.Won,
                path =
                {
                    new StoryStepState { beatId = "offer", optionId = PlayOptions.TakeItOn, result = "plain", week = s.week },
                    new StoryStepState { beatId = "step-1", optionId = "make-your-case", result = "success", week = s.week },
                },
            });
            EpisodeEngine.ReconcileOpportunities(s);
            var row = s.ledger.opportunities.Single(o => o.id == "cycle-test");
            Assert.That(row.kind, Is.EqualTo(OpportunityKinds.Play));
            Assert.That(row.source, Is.EqualTo(arc.id));
            Assert.That(row.anchor, Is.EqualTo(arc.play.deadline));
            Assert.That(row.response, Is.EqualTo(OpportunityResponse.Taken));
            Assert.That(row.outcome, Is.EqualTo(OpportunityOutcome.Won));
            Assert.That(row.steps.Select(step => step.choice), Is.EqualTo(new[] { "make-your-case" }), "The steps, without the offer's own option.");
            // The same play, turned down at the offer.
            s.storylines[s.storylines.Count - 1].endingId = PlayEndings.Declined;
            s.storylines[s.storylines.Count - 1].path.Clear();
            s.storylines[s.storylines.Count - 1].path.Add(new StoryStepState { beatId = "offer", optionId = PlayOptions.NotNow, result = "plain", week = s.week });
            EpisodeEngine.ReconcileOpportunities(s);
            row = s.ledger.opportunities.Single(o => o.id == "cycle-test");
            Assert.That(row.response, Is.EqualTo(OpportunityResponse.Declined));
            Assert.That(row.outcome, Is.EqualTo(OpportunityOutcome.NotApplicable));
        }
    }
}
