using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// <see cref="SeasonAutopsy"/> (BALANCE plan B1): hand-built seasons give the counts they were built
    /// with, a played season's report is consistent with itself, and the autopsy never changes what it reads.
    /// </summary>
    public sealed class SeasonAutopsyTests
    {
        // ---------------------------------------------------------------- played seasons

        /// <summary>
        /// A shipped season walked to its end by the engine tests' walker, with every phase change kept. A busy
        /// player also buys a conversation each week (from whoever the house picks) and spends every seat on talk.
        /// </summary>
        internal static (List<SeasonAutopsy.PhaseChange> changes, EpisodeState final) Walk(int size, uint seed, bool busy = false)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            ShippedRules.ApplyFresh(fresh);
            var engine = new EpisodeEngine(fresh);
            var changes = new List<SeasonAutopsy.PhaseChange>();
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var before = engine.Snapshot;
                var command = (busy ? Busy(before) : null) ?? EpisodeEngineTests.NextCommand(before);
                if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(before)
                    && before.juryExchanges[before.juryQuestionIndex].finalistId == before.playerId)
                {
                    var exchange = before.juryExchanges[before.juryQuestionIndex];
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
                }
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "week " + before.week + " " + before.phase + " " + command.kind + ": " + result.reason);
                if (SeasonAutopsy.IsPhaseChange(before, result.state)) changes.Add(new SeasonAutopsy.PhaseChange(before, result.state));
            }
            var final = engine.Snapshot;
            Assert.That(final.phase, Is.EqualTo(EpisodePhase.Finished));
            return (changes, final);
        }

        private static EpisodeCommand Busy(EpisodeState s)
        {
            if (s.pendingDiary != null || s.Find(s.playerId).status != ContestantStatus.Active
                || (s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign)) return null;
            EpisodeCommand Make(EpisodeCommandKind kind, string target, string text = null)
            {
                var c = EpisodeEngineTests.Command(s, kind);
                c.id = "autopsy-" + s.revision; c.targetId = target; c.text = text;
                return c;
            }
            if (s.phase == EpisodePhase.Campaign && s.boughtActionPoints == 0)
                return Make(EpisodeCommandKind.BuyActionPoint, null, WebSocialVocabulary.BurnOne);
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            return Make(EpisodeCommandKind.Talk, s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).First().id);
        }

        [Test]
        public void TheAutopsyNeverChangesWhatItReadsAndGivesTheSameReportTwice()
        {
            var (changes, final) = Walk(8, 3301u, busy: true);
            string before = Json(changes) + Json(final);
            var first = SeasonAutopsy.Of(changes, final);
            var second = SeasonAutopsy.Of(changes, final);
            Assert.That(Json(changes) + Json(final), Is.EqualTo(before), "Every state it read is byte for byte as it was.");
            Assert.That(Json(second), Is.EqualTo(Json(first)), "The same season, the same report.");
            Assert.That(first.economy.seatsSpent, Is.GreaterThan(0), "The season it read spent seats...");
            Assert.That(first.economy.purchases, Is.GreaterThan(0), "...and bought time...");
            Assert.That(first.competitions, Is.Not.Empty, "...and played its competitions.");
        }

        [TestCase(6, 3302u)]
        [TestCase(8, 3303u)]
        public void APlayedSeasonsReportAgreesWithItself(int size, uint seed)
        {
            var (changes, final) = Walk(size, seed);
            var r = SeasonAutopsy.Of(changes, final);
            Assert.That(r.finished, Is.True);
            Assert.That(r.houseSize, Is.EqualTo(size));
            Assert.That(r.houseguests.Select(h => h.placement).OrderBy(p => p), Is.EqualTo(Enumerable.Range(1, size)), "Every place from first to last is somebody's.");
            Assert.That(r.placement, Is.EqualTo(r.houseguests.Single(h => h.isPlayer).placement));
            Assert.That(r.outcome == SeasonAutopsy.Outcomes.Winner, Is.EqualTo(r.placement == 1));
            Assert.That(r.outcome == SeasonAutopsy.Outcomes.Evicted, Is.EqualTo(r.playerOutWeek > 0));
            Assert.That(r.weeksPlayed.Count(w => w.evicteeId != null), Is.EqualTo(size - 2), "Everybody but the final two leaves by an eviction.");
            Assert.That(r.weeksPlayed.Count(w => w.finalEviction), Is.EqualTo(1), "The final Head of Household's choice is one of them.");
            int regularWeeks = r.weeksPlayed.Count(w => !w.finalEviction);
            Assert.That(r.competitions.Count(c => c.phase == "HoH"), Is.EqualTo(regularWeeks), "One Head of Household competition a regular week.");
            Assert.That(r.competitions.Count(c => c.phase == "Veto"), Is.EqualTo(regularWeeks));
            Assert.That(r.competitions.Count(c => c.phase.StartsWith("FinalHoHPart", StringComparison.Ordinal)), Is.EqualTo(3));
            foreach (var week in r.weeksPlayed.Where(w => !w.finalEviction))
            {
                Assert.That(r.competitions.Single(c => c.week == week.week && c.phase == "HoH").winnerId, Is.EqualTo(week.hohId), "week " + week.week);
                Assert.That(r.competitions.Single(c => c.week == week.week && c.phase == "Veto").winnerId, Is.EqualTo(week.vetoHolderId), "week " + week.week);
                Assert.That(week.votes, Is.GreaterThan(0));
                Assert.That(week.evicteeThreatRank, Is.GreaterThan(0), "The evictee's threat was read as the campaign closed.");
            }
            foreach (var c in r.competitions)
            {
                Assert.That(c.winnerStatRank, Is.InRange(1, c.field));
                Assert.That(c.playerInField, Is.EqualTo(c.playerPlacement > 0));
                if (c.playerInField) Assert.That(c.playerPerformance, Is.EqualTo(0.5), "The walker plays at half marks.");
            }
            Assert.That(r.houseguests.Sum(h => h.hohWins), Is.EqualTo(r.competitions.Count(c => c.phase == "HoH") + 1), "Every weekly Head of Household, and the final one.");
            Assert.That(r.houseguests.Sum(h => h.vetoWins), Is.EqualTo(r.competitions.Count(c => c.phase == "Veto")));
            Assert.That(r.weeksPlayed.Single(w => w.finalEviction).hohId, Is.EqualTo(r.competitions.Single(c => c.phase == "FinalHoHPart3").winnerId));
            Assert.That(r.jury.jurors, Is.EqualTo(r.jury.winnerVotes + r.jury.runnerUpVotes));
            Assert.That(r.jury.margin, Is.GreaterThanOrEqualTo(0), "The winner has at least the runner-up's votes.");
            Assert.That(r.economy.windows, Is.Not.Empty, "The player's windows were read.");
            Assert.That(r.economy.seatsSpent, Is.Zero, "The walker spends no seat.");
            Assert.That(r.economy.seatsWasted, Is.EqualTo(r.economy.seatsOffered));
            Assert.That(r.rules, Has.Member("economyRulesVersion=1"));
            Assert.That(r.warmest.mutual, Is.GreaterThanOrEqualTo(r.coldest.mutual));
        }

        // ---------------------------------------------------------------- hand-built seasons

        private static List<string> Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        /// <summary>
        /// A six-person season finished by hand: npc[0] wins, the player is runner-up. The order out is npc[4],
        /// npc[3], npc[2], npc[1] - the last by the final Head of Household's choice - and every juror's
        /// evictor, ballot and pact is set so the counts are known.
        /// </summary>
        private static EpisodeState FinishedByHand()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 77u);
            var npc = Npcs(s);
            string me = s.playerId;
            s.phase = EpisodePhase.Finished; s.week = 5;
            s.winnerId = npc[0]; s.runnerUpId = me;
            s.Find(npc[0]).status = ContestantStatus.Winner;
            s.Find(me).status = ContestantStatus.RunnerUp;
            foreach (var id in npc.Skip(1)) s.Find(id).status = ContestantStatus.Jury;
            s.Find(me).nominationWeeks = new List<int> { 2 };
            s.Find(me).hohWins = 1;
            s.Find(npc[0]).hohWins = 2;
            s.ledger.power = new List<PowerRow>
            {
                new PowerRow { week = 1, hohId = npc[0], evicteeId = npc[4], nominees = new List<string> { npc[4], npc[3] }, tally = new List<int> { 3, 0 } },
                new PowerRow { week = 2, hohId = npc[1], evicteeId = npc[3], nominees = new List<string> { npc[3], me }, tally = new List<int> { 1, 1 } },
                new PowerRow { week = 3, hohId = me, evicteeId = npc[2], nominees = new List<string> { npc[2], npc[1] }, tally = new List<int> { 1, 0 } },
                new PowerRow { week = 4, hohId = npc[0], evicteeId = npc[1], nominees = new List<string> { npc[0], me } },
            };
            // Jurors: npc[4] (evicted under npc[0], a finalist) votes for me - bitter; npc[3] (under npc[1], not a
            // finalist) votes npc[0]; npc[2] (under me) votes npc[0] - bitter; npc[1] (under npc[0]) votes npc[0].
            s.votes = new List<VoteState>
            {
                new VoteState { voterId = npc[4], targetId = me },
                new VoteState { voterId = npc[3], targetId = npc[0] },
                new VoteState { voterId = npc[2], targetId = npc[0] },
                new VoteState { voterId = npc[1], targetId = npc[0] },
            };
            s.deals = new List<DealState>
            {
                new DealState { id = "d1", type = DealKind.VoteEvict, proposerId = me, recipientId = npc[1], status = DealStatus.Fulfilled, week = 1 },
                new DealState { id = "d2", type = DealKind.FinalTwo, proposerId = npc[2], recipientId = me, status = DealStatus.Broken, week = 2 },
                new DealState { id = "d3", type = DealKind.VoteSave, proposerId = npc[1], recipientId = npc[2], status = DealStatus.Fulfilled, week = 2 },
                new DealState { id = "d4", type = DealKind.VoteSave, proposerId = npc[3], recipientId = npc[4], status = DealStatus.Declined, week = 1 },
            };
            s.promises = new List<PromiseState>
            {
                new PromiseState { id = "p1", fromId = me, toId = npc[0], kind = PromiseKind.Safety, status = PromiseStatus.Fulfilled, week = 1 },
                new PromiseState { id = "p2", fromId = me, toId = npc[1], kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 2 },
            };
            s.alliances = new List<AllianceState>
            {
                new AllianceState { id = "alliance-1", members = new List<string> { me, npc[0] }, active = false },
                new AllianceState { id = "alliance-npc-1", members = new List<string> { npc[1], npc[2] }, active = false },
            };
            s.ledger.alliances = new List<AllianceRow>
            {
                new AllianceRow { id = "alliance-1", startedWeek = 1, endedWeek = 4, why = "player/finale" },
                new AllianceRow { id = "alliance-npc-1", startedWeek = 1, endedWeek = 3, why = "npc/departure" },
            };
            foreach (var r in s.relationships) r.score = 0;
            void Set(string a, string b, double v) => s.relationships.Single(r => r.fromId == a && r.toId == b).score = v;
            Set(npc[1], npc[2], 40); Set(npc[2], npc[1], 35);
            Set(npc[3], me, -30); Set(me, npc[3], -25);
            return s;
        }

        [Test]
        public void AHandBuiltSeasonGivesTheCountsItWasBuiltWith()
        {
            var s = FinishedByHand();
            var npc = Npcs(s);
            var r = SeasonAutopsy.Of(new List<SeasonAutopsy.PhaseChange>(), s);

            Assert.That(r.outcome, Is.EqualTo(SeasonAutopsy.Outcomes.RunnerUp));
            Assert.That(r.placement, Is.EqualTo(2));
            Assert.That(r.playerOutWeek, Is.Zero);
            Assert.That(r.houseguests.Single(h => h.id == npc[4]).placement, Is.EqualTo(6), "First out, last place.");
            Assert.That(r.houseguests.Single(h => h.id == npc[1]).placement, Is.EqualTo(3), "The final eviction.");
            Assert.That(r.houseguests.Single(h => h.id == npc[0]).placement, Is.EqualTo(1));
            Assert.That(r.playerHohWins, Is.EqualTo(1));

            Assert.That(r.weeksPlayed.Select(w => w.evicteeId), Is.EqualTo(new[] { npc[4], npc[3], npc[2], npc[1] }));
            Assert.That(r.weeksPlayed.Select(w => w.playerNominated), Is.EqualTo(new[] { false, true, false, false }));
            Assert.That(r.weeksPlayed.Select(w => w.playerHoh), Is.EqualTo(new[] { false, false, true, false }));
            Assert.That(r.weeksPlayed.Select(w => w.tie), Is.EqualTo(new[] { false, true, false, false }));
            Assert.That(r.weeksPlayed.Select(w => w.finalEviction), Is.EqualTo(new[] { false, false, false, true }));
            Assert.That(r.weeksPlayed.Select(w => w.votes), Is.EqualTo(new[] { 3, 2, 1, 0 }));

            Assert.That(r.jury.jurors, Is.EqualTo(4));
            Assert.That(r.jury.winnerVotes, Is.EqualTo(3));
            Assert.That(r.jury.runnerUpVotes, Is.EqualTo(1));
            Assert.That(r.jury.margin, Is.EqualTo(2));
            Assert.That(r.jury.playerFinalist, Is.True);
            Assert.That(r.jury.playerVotes, Is.EqualTo(1));
            Assert.That(r.jury.jurorsWithEvictorFinalist, Is.EqualTo(3), "npc[4] and npc[1] under npc[0], npc[2] under the player.");
            Assert.That(r.jury.bitterJurors, Is.EqualTo(2), "npc[4] and npc[2] voted against their evictor.");

            var c = r.commitments;
            Assert.That(c.playerDeals, Is.EqualTo(2)); Assert.That(c.playerDealsKept, Is.EqualTo(1)); Assert.That(c.playerDealsBroken, Is.EqualTo(1));
            Assert.That(c.npcDeals, Is.EqualTo(2)); Assert.That(c.npcDealsKept, Is.EqualTo(1)); Assert.That(c.npcDealsBroken, Is.Zero);
            Assert.That(c.deals["npc:" + DealKind.VoteSave + ":" + DealStatus.Declined], Is.EqualTo(1));
            Assert.That(c.deals["player:" + DealKind.FinalTwo + ":" + DealStatus.Broken], Is.EqualTo(1));
            Assert.That(c.playerPromises, Is.EqualTo(2)); Assert.That(c.playerPromisesKept, Is.EqualTo(1)); Assert.That(c.playerPromisesBroken, Is.EqualTo(1));
            Assert.That(c.playerPacts, Is.EqualTo(1)); Assert.That(c.npcPacts, Is.EqualTo(1));
            Assert.That(c.playerPactsEnded, Is.EqualTo(1)); Assert.That(c.npcPactsEnded, Is.EqualTo(1));
            Assert.That(c.pactEndings["npc:departure"], Is.EqualTo(1));

            Assert.That(new[] { r.warmest.a, r.warmest.b }, Is.EquivalentTo(new[] { npc[1], npc[2] }));
            Assert.That(r.warmest.mutual, Is.EqualTo(75));
            Assert.That(new[] { r.coldest.a, r.coldest.b }, Is.EquivalentTo(new[] { npc[3], s.playerId }));
            Assert.That(r.coldest.mutual, Is.EqualTo(-55));
        }

        [Test]
        public void ACompetitionIsReadFromThePhaseChangeThatClosedIt()
        {
            var before = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 78u);
            ShippedRules.ApplyFresh(before);
            var npc = Npcs(before);
            string me = before.playerId;
            before.phase = EpisodePhase.HoH; before.competitionResolved = true;
            // On paper npc[0] is strongest and npc[1] next; npc[1] wins, the player is third.
            foreach (var c in before.contestants) c.stats = new ContestantStats { physical = 3, mental = 3, endurance = 3, social = 3, luck = 3, competition = 3 };
            before.Find(npc[0]).stats = new ContestantStats { physical = 9, mental = 9, endurance = 9, social = 9, luck = 9, competition = 9 };
            before.Find(npc[1]).stats = new ContestantStats { physical = 7, mental = 7, endurance = 7, social = 7, luck = 7, competition = 7 };
            before.competitionScores = before.contestants.Select((c, i) => new CompetitionScore { contestantId = c.id, score = c.id == npc[1] ? 20 : c.id == npc[0] ? 15 : c.id == me ? 10 : i }).ToList();
            before.ledger.competitions.Add(new CompetitionRow { week = before.week, kind = "HoH", field = 6, placement = 3, entry = CompetitionEntry.Played, performance = 0.75, expectedWin = 0.1 });
            var after = before.Clone();
            after.phase = EpisodePhase.Nomination; after.hohId = npc[1];

            var r = SeasonAutopsy.Of(new List<SeasonAutopsy.PhaseChange> { new SeasonAutopsy.PhaseChange(before, after) }, after);
            var result = r.competitions.Single();
            Assert.That(result.phase, Is.EqualTo("HoH"));
            Assert.That(result.category, Is.EqualTo(EpisodeEngine.CompetitionCategory(before)));
            Assert.That(result.field, Is.EqualTo(6));
            Assert.That(result.winnerId, Is.EqualTo(npc[1]));
            Assert.That(result.winnerStatRank, Is.EqualTo(2), "The second strongest on paper won.");
            Assert.That(result.playerInField, Is.True);
            Assert.That(result.playerPlacement, Is.EqualTo(3));
            Assert.That(result.playerPerformance, Is.EqualTo(0.75));
            Assert.That(result.playerEntry, Is.EqualTo(CompetitionEntry.Played));
        }

        [Test]
        public void TheWindowsAndBoughtTimeAreReadAsEachPhaseCloses()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 79u);
            ShippedRules.ApplyFresh(s);
            var npc = Npcs(s);
            var campaign = s.Clone();
            campaign.phase = EpisodePhase.Campaign; campaign.week = 2;
            campaign.windowActions = new List<int> { 0, 0, 1, 0 };
            campaign.events.Add(new EpisodeEvent { sequence = 900, week = 2, kind = "bought-action", text = "You bought yourself more time, and it cost you with " + campaign.Find(npc[0]).name + "." });
            campaign.events.Add(new EpisodeEvent { sequence = 901, week = 2, kind = "bought-action", text = "You bought yourself more time, and the whole house felt it." });
            campaign.haveNots = new List<string> { campaign.playerId };
            var eviction = campaign.Clone();
            eviction.phase = EpisodePhase.Eviction;

            var r = SeasonAutopsy.Of(new List<SeasonAutopsy.PhaseChange> { new SeasonAutopsy.PhaseChange(campaign, eviction) }, eviction);
            var window = r.economy.windows.Single();
            Assert.That(window.window, Is.EqualTo(Windows.AfterVeto));
            Assert.That(window.week, Is.EqualTo(2));
            Assert.That(window.budget, Is.EqualTo(EpisodeEngine.SocialActionBudget(campaign)));
            Assert.That(window.spent, Is.EqualTo(1));
            Assert.That(r.economy.seatsWasted, Is.EqualTo(window.budget - 1));
            Assert.That(r.economy.purchases, Is.EqualTo(2), "Each line counted once, though two states hold it.");
            Assert.That(r.economy.purchasesFromOne, Is.EqualTo(1));
            Assert.That(r.economy.purchasesFromEveryone, Is.EqualTo(1));
            Assert.That(r.economy.goodwillPaid, Is.EqualTo(8 + 3 * 5), "Eight with one, three with each of the other five.");
            Assert.That(r.economy.haveNotWeeks, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- helpers

        private static string Json(object value)
            => JsonConvert.SerializeObject(value, new JsonSerializerSettings { ContractResolver = new PublicFields() });

        private sealed class PublicFields : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
                => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
        }
    }
}
