using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5c: the threat reader and the Story readers read mode 2 as they read mode 1, with the approved policy's
    /// current-reveal exclusion. <see cref="ThreatAssessment"/>'s reputation counts a breaker's broken vote promises row by row
    /// as mode 1's raw list did - from the rows alone, so inside a reveal too - and its Safety incidents wherever Safety is
    /// canonical; a reveal's own recipes leave that reveal's Vote breaches out (a typed list beside the Safety one), so the
    /// grudge a breach draws is at most one lighter than mode 1's, and an earlier reveal's breach counts in both. The Story
    /// readers - the odds the player sees, a conversation's story chance, the house remembers, plays, ported and spine - read
    /// the views or flip their gates.
    /// </summary>
    public sealed class ModeTwoThreatStoryTests
    {
        /// <summary>
        /// The threat and Story readers at one of a walk's moments (<see cref="ModeTwoReaderParityTests"/>): every pair a standing,
        /// kept or broken word joins and every pair with the player - threat and its grudge scaling; and for every houseguest,
        /// the story odds' terms and whether the player broke their word to them.
        /// </summary>
        internal static void CheckThreatStory(EpisodeState mode1, EpisodeState mode2, string where, IEnumerable<(string, string)> pairs, bool overlap)
        {
            foreach (var (a, b) in pairs)
            {
                Assert.That(ThreatAssessment.Total(mode2, a, b), Is.EqualTo(ThreatAssessment.Total(mode1, a, b)), where + ": threat " + a + ">" + b);
                Assert.That(Grudges.ThreatScaled(mode2, 60, a, b), Is.EqualTo(Grudges.ThreatScaled(mode1, 60, a, b)), where + ": a grudge's scaling " + a + ">" + b);
                // A Head of Household's whole weight: reluctance, the story's preference, their Safety overlap and threat (V5b,
                // V5c) - its broken-deal term by incident, so not where an incident has two rows (D1).
                if (!overlap)
                    Assert.That(EpisodeEngine.NominationWeight(mode2, a, b), Is.EqualTo(EpisodeEngine.NominationWeight(mode1, a, b)), where + ": NominationWeight " + a + ">" + b);
            }
            foreach (var npc in mode1.contestants.Where(c => !c.isPlayer))
            {
                Assert.That(StoryOdds.PlayerBrokeTheirWord(mode2, npc.id), Is.EqualTo(StoryOdds.PlayerBrokeTheirWord(mode1, npc.id)),
                    where + ": the player broke their word to " + npc.id);
                Assert.That(Json(StoryOdds.Terms(mode2, null, Choice(npc.id))), Is.EqualTo(Json(StoryOdds.Terms(mode1, null, Choice(npc.id)))),
                    where + ": the story odds about " + npc.id);
            }
        }

        /// <summary>An option whose odds the player sees, about one houseguest.</summary>
        private static HouseEventChoice Choice(string subjectId) => new HouseEventChoice { subjectId = subjectId, checkBase = 50, approach = "charm" };

        // ------------------------------------------------------------ the current-reveal exclusion, at a second reveal

        private sealed class TwoReveals
        {
            internal EpisodeState Mode1, Mode2, Projection;
            internal string First, Second, Wronged;
        }

        private static TwoReveals twoReveals;

        /// <summary>
        /// A season the player breaks a vote promise in at two reveals running: each campaign they promise a voter to vote the
        /// first nominee out, and every ballot - theirs too, by the real command - goes against the second. The second reveal
        /// is played through both games: the public one, and its projection through the seam.
        /// </summary>
        private static TwoReveals BrokenTwice()
        {
            if (twoReveals != null) return twoReveals;
            for (uint seed = 1; seed <= 32; seed++)
            {
                var season = new PinnedVoteSeason(seed, PinnedVoteSeason.Fresh(seed));
                var promised = new List<string>();
                bool ready = false;
                for (int step = 0; step < 600 && !ready; step++)
                {
                    var s = season.State;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active || !season.Supported) break;
                    bool promisedThisWeek = promised.Count > 0 && s.promises.Any(p => p.id == promised.Last() && p.week == s.week);
                    if (s.phase == EpisodePhase.Campaign && PinnedVoteSeason.PlayerVotes(s) && s.nominees.Count == 2 && !promisedThisWeek)
                    {
                        var promise = season.Apply(ProspectiveVoteTwins.Command(s, EpisodeCommandKind.PromiseVote, PinnedVoteSeason.NpcVoters(s).First(), s.nominees[0]));
                        if (!promise.accepted) break;
                        promised.Add(season.State.promises.Last(p => p.kind == PromiseKind.Vote).id);
                        continue;
                    }
                    if (promisedThisWeek && PinnedVoteSeason.OpenVote(s) && PinnedVoteSeason.PlayerVotes(s) && s.votes.Count == 0)
                    {
                        // The first reveal, in the public game: every ballot against the nominee the promise spares.
                        if (promised.Count == 1) { season.PlayPinnedVote(s.nominees[1]); continue; }
                        ready = true;
                        continue;
                    }
                    season.Step(EpisodeEngineTests.NextCommand(s));
                }
                if (!ready) continue;
                var open = season.State;
                string evict = open.nominees[1];
                season.RunNpcBatch();
                season.PinCast(PinnedVoteSeason.NpcVoters(season.State).ToDictionary(id => id, id => evict, StringComparer.Ordinal));
                var pinned = season.State;
                var engine = ProspectiveVoteFacade.Engine(PinnedVoteSeason.Project(pinned, season.Owners, season.Frames));
                var cast = ProspectiveVoteTwins.Command(pinned, EpisodeCommandKind.CastVote, evict);
                season.Step(cast);
                Assert.That(engine.Apply(cast).accepted, Is.True);
                var count = ProspectiveVoteTwins.Command(season.State, EpisodeCommandKind.Advance, tag: "second-reveal");
                var mode1 = season.Step(count);
                var mode2 = engine.Apply(count);
                Assert.That(mode2.accepted, Is.True, mode2.reason);
                TestContext.Out.WriteLine("Broken at two reveals: seed " + seed + ", weeks " + string.Join(", ", promised) + ".");
                return twoReveals = new TwoReveals { Mode1 = mode1, Mode2 = mode2.state, Projection = PinnedVoteSeason.Project(mode1, season.Owners, season.Frames),
                    First = promised[0], Second = promised[1], Wronged = mode1.promises.Single(p => p.id == promised[1]).toId };
            }
            Assert.Fail("No seed in 1..32 breaks the player's vote promise at two reveals running.");
            return null;
        }

        [Test]
        public void ASecondRevealCountsTheEarlierBreachAsModeOneDoesAndLeavesOutOnlyItsOwn()
        {
            var two = BrokenTwice();
            var s = two.Mode2; string player = s.playerId, wronged = two.Wronged;
            var first = s.unifiedCommitments.Single(r => r.id == two.First);
            var second = s.unifiedCommitments.Single(r => r.id == two.Second);
            Assert.That((first.status, second.status, first.brokenById, second.brokenById, second.settledWeek),
                Is.EqualTo((DealStatus.Broken, DealStatus.Broken, player, player, s.week)), "Fixture: broken at two reveals, the second this one.");
            Assert.That(first.settledWeek, Is.LessThan(second.settledWeek));
            // Outside a reveal the reader counts both, as mode 1 does.
            Assert.That(ThreatAssessment.Total(s, wronged, player), Is.EqualTo(ThreatAssessment.Total(two.Mode1, wronged, player)));
            // Inside it, leaving out this reveal's own is exactly not counting it yet; the earlier one still counts.
            var undecided = Undecide(s, second.id);
            var neither = Undecide(undecided, first.id);
            double excluded = ProspectiveVoteFacade.ThreatBeforeCommitmentEffects(s, wronged, player, null, new[] { second.settlementEffectKey });
            Assert.That(excluded, Is.EqualTo(ThreatAssessment.Total(undecided, wronged, player)), "The reveal's own breach is left out.");
            Assert.That(ThreatAssessment.Assess(undecided, wronged, player).Reputation, Is.GreaterThan(ThreatAssessment.Assess(neither, wronged, player).Reputation),
                "The earlier reveal's breach still counts.");
            // The grudge that reveal drew: mode 1's counted its own breach too, so it is the heavier, by at most one.
            var g1 = two.Projection.story.grudges.Single(g => g.holderId == wronged && g.targetId == player);
            var g2 = s.story.grudges.Single(g => g.holderId == wronged && g.targetId == player);
            Assert.That(g1.severity - g2.severity, Is.InRange(0, 1));
        }

        /// <summary>A detached copy with one Vote row as it stood before a reveal decided it.</summary>
        private static EpisodeState Undecide(EpisodeState s, string id)
        {
            var copy = s.Clone();
            var row = copy.unifiedCommitments.Single(r => r.id == id);
            row.status = DealStatus.Active; row.settledWeek = 0; row.brokenById = null; row.settlementEffectKey = null;
            return copy;
        }

        // ------------------------------------------------------------ two typed exclusions

        private static (EpisodeState mode1, EpisodeState mode2) safetyBreach;

        [Test]
        public void TheSafetyAndVoteExclusionsStaySeparate()
        {
            if (safetyBreach.mode2 == null)
                safetyBreach = ModeTwoReaderSweep.Find("a Safety breach on the record", (mode1, mode2) =>
                    UnifiedCommitmentHistory.Breaches(mode2).Any() && mode2.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote
                        && r.sourcePolicy == UnifiedCommitments.PromisePolicy));
            var s = safetyBreach.mode2.Clone();
            var incident = UnifiedCommitmentHistory.Breaches(s).First();
            string breaker = incident.ActorId;
            // A broken vote promise of the same breaker, constructed on this detached copy for the reader alone: no owner
            // files it here (a promise's maker breaks it at a reveal).
            var vote = s.unifiedCommitments.First(r => r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.PromisePolicy);
            vote.makerId = breaker;
            if (vote.beneficiaryId == breaker) vote.beneficiaryId = s.contestants.First(c => c.id != breaker).id;
            if (vote.targetId == breaker) vote.targetId = null;
            vote.status = DealStatus.Broken; vote.settledWeek = s.week; vote.brokenById = breaker;
            vote.settlementEffectKey = UnifiedVoteHistory.Key(vote, s.week);
            Assert.That(UnifiedVoteFamilyValidation.TryValidateStorage(s, out var error), Is.True, error);
            string evaluator = s.contestants.First(c => c.id != breaker && c.id != incident.WrongedId && c.id != vote.beneficiaryId).id;
            var safetyKey = new[] { incident.EffectKey };
            var voteKey = new[] { vote.settlementEffectKey };
            double Threat(IReadOnlyList<string> safety, IReadOnlyCollection<string> votes) =>
                ProspectiveVoteFacade.ThreatBeforeCommitmentEffects(s, evaluator, breaker, safety, votes);
            double Reputation(EpisodeState state) => ThreatAssessment.Assess(state, evaluator, breaker).Reputation;
            double both = Threat(null, null);
            Assert.That(both, Is.EqualTo(ThreatAssessment.Total(s, evaluator, breaker)));
            Assert.That(Threat(voteKey, null), Is.EqualTo(both), "A Vote identity in the Safety list leaves nothing out.");
            Assert.That(Threat(null, safetyKey), Is.EqualTo(both), "A Safety identity in the Vote list leaves nothing out.");
            Assert.That(Threat(null, voteKey), Is.EqualTo(ThreatAssessment.Total(Undecide(s, vote.id), evaluator, breaker)), "The Vote list leaves out the Vote breach.");
            Assert.That(Threat(safetyKey, voteKey), Is.LessThanOrEqualTo(Threat(null, voteKey)));
            Assert.That(Threat(safetyKey, null), Is.LessThanOrEqualTo(both));
            Assert.That(Reputation(s), Is.GreaterThan(Reputation(Undecide(s, vote.id))), "The Vote breach counts unless it is left out.");
        }

        // ------------------------------------------------------------ a hidden ballot (P3)

        [Test]
        public void TheStoryOddsShowNothingOfABallotThePlayerCannotKnow()
        {
            var blind = ModeTwoReaderSweep.Flip(false);
            blind.AssertBlind("StoryOdds.Terms", s => StoryOdds.Terms(s, null, Choice(blind.PartnerId)));
            blind.AssertBlind("StoryOdds.PlayerBrokeTheirWord", s => StoryOdds.PlayerBrokeTheirWord(s, blind.PartnerId));
        }

        private static string Json(object value) => PinnedVoteSeason.Json(value);
    }
}
