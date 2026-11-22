#if !UNITY_5_3_OR_NEWER
// Unity-free only (Tools/SimulationTests): whole seasons, which Unity's batch runner would also run as [Explicit] ones.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A final two deal from minds level 1 (<see cref="FinalTwoDeals"/>) over whole seasons the game starts.
    /// <list type="bullet">
    /// <item>The lockstep: a season at the level plays exactly as the same season at level 0 - the same draws, the same
    /// mints, the same state at every turn of the week - until a final two deal first can act: one with the player, or the
    /// final Head of Household's with a finalist at the final choice. The level reads nothing else.</item>
    /// <item>The soak (by name): the player strikes a final two deal with the housemate warmest on them as the endgame opens,
    /// then the house plays on; how each deal ended, at level 0 and at the level.</item>
    /// </list>
    /// </summary>
    public sealed class FinalTwoDealsSeasonSoak
    {
        [TestCase(5101u)] [TestCase(5102u)] [TestCase(5103u)]
        public void ALevelSeasonPlaysLevelZerosSeasonUntilAFinalTwoDealCanAct(uint seed)
        {
            var below = new EpisodeEngine(Fresh(seed, MindRules.Off));
            var at = new EpisodeEngine(Fresh(seed, FinalTwoDeals.Level));
            bool diverged = false, couldAct = false;
            EpisodePhase lastPhase = EpisodePhase.Social;
            for (int step = 0; step < 20000; step++)
            {
                var a = below.Snapshot;
                var b = at.Snapshot;
                if (a.phase == EpisodePhase.Finished && b.phase == EpisodePhase.Finished) break;
                if (!diverged)
                {
                    bool same = a.randomState == b.randomState && a.nextSequence == b.nextSequence && a.revision == b.revision && a.phase == b.phase;
                    if (same && (a.phase == EpisodePhase.Social || a.phase == EpisodePhase.FinalEviction || a.phase == EpisodePhase.Finished)) same = Json(a) == Json(b);
                    if (!same)
                    {
                        // A step before this one parted them: a final two deal had been able to act by then.
                        Assert.That(couldAct, Is.True, "seed " + seed + " step " + step + ": the level moved a season before any final two deal could act (" + lastPhase + ").");
                        diverged = true;
                    }
                    couldAct |= CanAct(a);
                    lastPhase = a.phase;
                }
                if (a.phase != EpisodePhase.Finished) Apply(below, a);
                if (b.phase != EpisodePhase.Finished) Apply(at, b);
            }
            Assert.That(below.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "Level 0's season finished.");
            Assert.That(at.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "The level's season finished.");
            Assert.That(EpisodeValidation.TryValidate(at.Snapshot, out string error), Is.True, error);
            TestContext.WriteLine("seed " + seed + (diverged ? ": diverged where a final two deal could act" : ": byte-equal to its end"));
        }

        private const string Why = "Whole seasons: the final two deal soak, two arms of two hundred, the player keeping their side. Run by name.";

        [Test, Explicit(Why)]
        public void FinalTwoDealSoak()
        {
            foreach (int level in new[] { MindRules.Off, FinalTwoDeals.Level })
            {
                var tally = new Dictionary<string, int>(StringComparer.Ordinal);
                for (uint seed = 5201; seed < 5401; seed++) Season(seed, level, tally);
                TestContext.Progress.WriteLine("F2SOAK level " + level + ": " + string.Join(", ", tally.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)));
                Assert.That(tally.TryGetValue("finished", out int finished) ? finished : 0, Is.EqualTo(200));
            }
        }

        // ------------------------------------------------------------------ the walk

        private static void Season(uint seed, int level, Dictionary<string, int> tally)
        {
            var engine = new EpisodeEngine(Fresh(seed, level));
            string partner = null, finalHoh = null;
            for (int step = 0; step < 20000; step++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished) { End(s, partner, finalHoh, tally); return; }
                if (partner == null && s.phase == EpisodePhase.Social && s.Active.Count() <= NpcDeals.EndgameSize
                    && s.Find(s.playerId).status == ContestantStatus.Active)
                {
                    partner = s.Active.Where(c => !c.isPlayer).OrderByDescending(c => s.Score(c.id, s.playerId)).ThenBy(c => c.id, StringComparer.Ordinal).First().id;
                    s.deals.Add(PlayerDeals.Draft(s, partner, DealKind.FinalTwo, null, "deal-soak-final-two"));
                    Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
                    engine = new EpisodeEngine(s);
                    continue;
                }
                if (s.phase == EpisodePhase.FinalEviction) finalHoh = s.hohId;
                Apply(engine, s, partner);
            }
            Assert.Fail("seed " + seed + ": the season did not finish.");
        }

        private static void End(EpisodeState s, string partner, string finalHoh, Dictionary<string, int> tally)
        {
            Add(tally, "finished");
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            if (partner == null) { Add(tally, "no deal (player out before the endgame)"); return; }
            Add(tally, "deals");
            var deal = s.deals.Single(d => d.id == "deal-soak-final-two");
            bool lapsed = s.events.Any(e => e.text == FinalTwoDeals.LapseLine(s.Find(partner).name));
            bool together = new[] { s.winnerId, s.runnerUpId }.OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(new[] { s.playerId, partner }.OrderBy(id => id, StringComparer.Ordinal));
            string how;
            if (deal.status == DealStatus.Fulfilled) how = "kept at the final choice";
            else if (deal.status == DealStatus.Broken) how = deal.brokenById == s.playerId ? "broken by the player" : "broken by the partner at the final choice";
            else if (deal.status == DealStatus.Expired) how = lapsed ? "lapsed: the partner nominated the player" : "ended: one of the two left the house";
            else how = "standing at the end (" + deal.status + ")";
            Add(tally, how);
            if (together) Add(tally, "held to the end: the two sat the final two together");
            if (finalHoh == partner) Add(tally, "partner was the final HoH");
            if (finalHoh == partner && deal.status == DealStatus.Fulfilled) Add(tally, "partner as final HoH took the player");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Whether a final two deal can act at this turn: one with the player, or one the final Head of Household holds with a finalist.</summary>
        private static bool CanAct(EpisodeState s)
        {
            bool withPlayer = s.deals.Any(d => d.type == DealKind.FinalTwo && DealStatus.Binds(d.status) && (d.proposerId == s.playerId || d.recipientId == s.playerId))
                || s.promises.Any(p => p.kind == PromiseKind.FinalTwo && p.status == PromiseStatus.Active && (p.fromId == s.playerId || p.toId == s.playerId));
            if (withPlayer) return true;
            if (s.phase != EpisodePhase.FinalEviction) return false;
            var finalists = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            return s.deals.Any(d => d.type == DealKind.FinalTwo && DealStatus.Binds(d.status) && finalists.Contains(DealResolution.Partner(d, s.hohId) ?? ""))
                || s.promises.Any(p => p.kind == PromiseKind.FinalTwo && p.status == PromiseStatus.Active && p.fromId == s.hohId && finalists.Contains(p.toId));
        }

        private static EpisodeState Fresh(uint seed, int level)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.Regular }, seed);
            ShippedRules.ApplyFresh(s);
            s.story.mindRules = level;
            return s;
        }

        private static string Json(EpisodeState s)
        {
            s.story.mindRules = MindRules.Off;
            return JsonConvert.SerializeObject(s);
        }

        private static void Apply(EpisodeEngine engine, EpisodeState s, string partner = null)
        {
            var command = EpisodeEngineTests.NextCommand(s);
            if (partner != null) KeepTheDeal(s, command, partner);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(s))
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex];
                if (exchange.finalistId == s.playerId)
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
            }
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, s.phase + ": " + result.reason);
        }

        /// <summary>
        /// The soak's player keeps their side of the deal, as a player who made one would: never nominates the partner or
        /// names them as the replacement while somebody else can go up, saves them with a veto they hold, and takes them to
        /// the final two. Everything else is the plain walk's.
        /// </summary>
        private static void KeepTheDeal(EpisodeState s, EpisodeCommand command, string partner)
        {
            if (s.Find(partner)?.status != ContestantStatus.Active) return;
            switch (command.kind)
            {
                case EpisodeCommandKind.Nominate:
                    var pool = EpisodeEngine.NominationCandidates(s).Select(c => c.id).Where(id => id != partner).ToList();
                    if (pool.Count >= 2) { command.targetId = pool[0]; command.secondTargetId = pool[1]; }
                    break;
                case EpisodeCommandKind.ResolveVeto:
                    if (s.vetoHolderId == s.playerId && s.nominees.Contains(partner) && command.useVeto) command.targetId = partner;
                    if (s.hohId == s.playerId && command.useVeto)
                    {
                        string other = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).FirstOrDefault(id => id != partner);
                        if (other != null) command.secondTargetId = other;
                    }
                    break;
                case EpisodeCommandKind.FinalEvict:
                    command.targetId = s.Active.First(c => !c.isPlayer && c.id != partner).id;
                    break;
            }
        }

        private static void Add(Dictionary<string, int> tally, string key) => tally[key] = (tally.TryGetValue(key, out int n) ? n : 0) + 1;
    }
}
#endif
