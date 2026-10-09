using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5b: the house's decisions read mode 2 as they read mode 1 - the broken deals held against somebody
    /// (<see cref="NpcDeals.BrokenDeals"/>), the NPC vote (<see cref="WebEvictionVoting.EvaluateNative"/>: its promises, deals,
    /// Safety terms and threat), the levers' obligations, a Head of Household's reluctance and nomination weight, the story's
    /// Safety word, a ballot's betrayal of an ally, the jury's obligations and a crisis's cast.
    ///
    /// <para>Pattern P2: each reader's answer on the exact mode-2 projection of a public mode-1 season equals its mode-1
    /// answer at every command of seeds 1 to 32. One designed difference, the lead's decision D1: a breach term counts a vote
    /// breach once per Rule2 incident, so where one ballot broke two of a pair's rows (an incident with two rows) the breach
    /// terms may differ, and the sweep compares them only where no incident has more than one row; the difference itself is
    /// pinned by <see cref="OneBallotBreakingTwoDealsIsOneBreachInEveryBreachTerm"/>.</para>
    /// </summary>
    public sealed class ModeTwoHouseReaderTests
    {
        /// <summary>
        /// The house's readers at one of a walk's moments (<see cref="ModeTwoReaderParityTests"/> runs it at every phase change
        /// and count of seeds 1 to 32): for every pair a standing, kept or broken word joins and every pair with the player, the
        /// breaches held, warmth, a Head of Household's reluctance, the jury's obligations and the story's Safety word; at the
        /// open vote, every NPC voter's levers and ballot; and a crisis's cast. The breach terms only where no incident has two
        /// rows (<paramref name="overlap"/>): there they differ by design (D1).
        /// </summary>
        internal static void CheckHouse(EpisodeState mode1, EpisodeState mode2, string where, bool overlap)
        {
            // A pair no canonical word joins reads the same raw rows in either mode; an ended word reads as it did when it ended.
            var pairs = mode2.unifiedCommitments.Where(r => r.status != DealStatus.Expired && r.status != DealStatus.Declined)
                .Select(r => (r.makerId, r.beneficiaryId))
                .Concat(mode1.contestants.Where(c => !c.isPlayer).Select(c => (c.id, mode1.playerId)))
                .SelectMany(pair => new[] { pair, (pair.Item2, pair.Item1) }).Distinct()
                .OrderBy(pair => pair.Item1 + "|" + pair.Item2, StringComparer.Ordinal).ToList();
            if (!overlap)
                foreach (string a in pairs.Select(pair => pair.Item1).Distinct())
                    Assert.That(NpcDeals.BrokenDeals(mode2, a), Is.EqualTo(NpcDeals.BrokenDeals(mode1, a)), where + ": BrokenDeals " + a);
            foreach (var (a, b) in pairs)
            {
                if (!overlap)
                {
                    Assert.That(NpcDeals.Adjusted(mode2, a, b), Is.EqualTo(NpcDeals.Adjusted(mode1, a, b)), where + ": Adjusted " + a + ">" + b);
                    Assert.That(StrategyRules.NominationReluctance(mode2, a, b), Is.EqualTo(StrategyRules.NominationReluctance(mode1, a, b)),
                        where + ": NominationReluctance " + a + ">" + b);
                    Assert.That(WebJuryVoting.Obligations(mode2, a, b), Is.EqualTo(WebJuryVoting.Obligations(mode1, a, b)), where + ": jury Obligations " + a + ">" + b);
                }
                Assert.That(StoryConsumers.PromisedSafety(mode2, a, b), Is.EqualTo(StoryConsumers.PromisedSafety(mode1, a, b)), where + ": PromisedSafety");
                Assert.That(StoryConsumers.TheirWord(mode2, a, b), Is.EqualTo(StoryConsumers.TheirWord(mode1, a, b)), where + ": TheirWord");
                Assert.That(StoryConsumers.SafetyPreference(mode2, a, b), Is.EqualTo(StoryConsumers.SafetyPreference(mode1, a, b)), where + ": SafetyPreference");
            }
            // The ballots, where they are cast: the open vote.
            if (mode1.nominees.Count == 2 && mode1.phase == EpisodePhase.Eviction && !mode1.evictionResolved)
                foreach (var voter in EpisodeEngine.Voters(mode1).Where(v => !v.isPlayer))
                {
                    Assert.That(Json(EpisodeEngine.LeverTerms(mode2, voter.id)), Is.EqualTo(Json(EpisodeEngine.LeverTerms(mode1, voter.id))), where + ": LeverTerms " + voter.id);
                    if (!overlap)
                        Assert.That(Json(WebEvictionVoting.EvaluateNative(mode2, voter.id)), Is.EqualTo(Json(WebEvictionVoting.EvaluateNative(mode1, voter.id))),
                            where + ": the NPC vote of " + voter.id);
                }
            Assert.That(Json(HouseEventSources.Crisis(mode2, 0.37, mode2.nextSequence)), Is.EqualTo(Json(HouseEventSources.Crisis(mode1, 0.37, mode1.nextSequence))),
                where + ": the crisis cast");
        }

        // ------------------------------------------------------------ D1, the designed difference

        /// <summary>
        /// One ballot that breaks two of a pair's vote deals is one breach (the lead's decision D1): the Rule2 plan wrote one
        /// breach, and every breach term counts it once in mode 2 - the deals held against the breaker, their warmth, a Head of
        /// Household's reluctance, the NPC vote's threat and the jury's obligations - where mode 1 counts each row. The same facts,
        /// projected: only the counting differs. A deal kept is still each row.
        /// </summary>
        [Test]
        public void OneBallotBreakingTwoDealsIsOneBreachInEveryBreachTerm()
        {
            var s = ModeTwoReaderSweep.Campaign();
            string npc = PinnedVoteSeason.NpcVoters(s).First(), x = s.nominees[0], y = s.nominees[1], p = s.playerId;
            // Keep x and evict y: one duty, to vote y out, two rows. The houseguest's ballot for x breaks both.
            s = ModeTwoReaderSweep.Strike(ModeTwoReaderSweep.Strike(s, npc, DealKind.VoteSave, x), npc, DealKind.VoteEvict, y);
            var revealed = ModeTwoReaderSweep.Reveal(s, y, new Dictionary<string, string> { [npc] = x });
            var mode1 = revealed.Mode1; var mode2 = revealed.Projection;
            Assert.That(mode1.deals.Count(d => KnownBallots.IsVoteDeal(d.type) && d.status == DealStatus.Broken && d.brokenById == npc), Is.EqualTo(2),
                "Fixture: one ballot broke both rows.");
            var incident = UnifiedVoteHistory.Breaches(mode2).Single(i => i.ActorId == npc);
            Assert.That((incident.WrongedId, incident.EvidenceIds.Count, incident.DealId), Is.EqualTo((p, 2, incident.OwnerId)), "One incident, two rows, a deal owns it.");
            Assert.That(NpcDeals.BrokenDeals(mode1, npc), Is.EqualTo(2));
            Assert.That(NpcDeals.BrokenDeals(mode2, npc), Is.EqualTo(1), "Held against the breaker once.");
            Assert.That(NpcDeals.Adjusted(mode2, p, npc) - NpcDeals.Adjusted(mode1, p, npc), Is.EqualTo(NpcDeals.BrokenDealPenalty), "Read one breach warmer.");
            Assert.That(WebJuryVoting.Obligations(mode2, p, npc), Is.GreaterThan(WebJuryVoting.Obligations(mode1, p, npc)), "The jury holds one breach.");
            Assert.That(StrategyRules.NominationReluctance(mode1, p, npc) - StrategyRules.NominationReluctance(mode2, p, npc),
                Is.EqualTo(StrategyRules.Apply(mode1) ? StrategyRules.BrokenDealWeight : 0), "A Head of Household holds one breach.");
            // The NPC vote holds the breach once: of the two broken rows only the incident's owner weighs in the deal term and
            // only its deal in the threat term; mode 1's rows carry no marks and weigh each.
            var web2 = WebEvictionVoting.FromNative(mode2, p).state.deals.Where(d => d.status == DealStatus.Broken && d.brokenById == npc).ToList();
            Assert.That(web2.Select(d => (d.id, Json(d.ownerOf), Json(d.dealOf))), Is.EquivalentTo(new[] {
                (incident.OwnerId, Json(new[] { npc }), Json(new[] { npc })),
                (incident.EvidenceIds.Single(id => id != incident.OwnerId), "[]", "[]") }));
            Assert.That(WebEvictionVoting.FromNative(mode1, p).state.deals.Where(d => d.status == DealStatus.Broken).All(d => d.ownerOf == null && d.dealOf == null),
                Is.True);
            // And in a mode-2 game itself: the reveal's own Rule2 plan, the same one incident.
            var played = UnifiedVoteHistory.Breaches(revealed.Mode2).Single(i => i.ActorId == npc);
            Assert.That(Json(played.EvidenceIds), Is.EqualTo(Json(incident.EvidenceIds)));
            Assert.That(played.OwnerId, Is.EqualTo(incident.OwnerId));
        }

        // ------------------------------------------------------------ a Head of Household's own word

        private static (EpisodeState mode1, EpisodeState mode2) safeword;

        /// <summary>A nomination an NPC Head of Household is about to make, having promised a houseguest safety the walk saw them promise.</summary>
        private static (EpisodeState mode1, EpisodeState mode2) Safeword() => safeword.mode2 != null ? safeword
            : safeword = ModeTwoReaderSweep.Find("an NPC Head of Household about to nominate, their safety pact standing", (mode1, mode2) =>
                mode2.phase == EpisodePhase.Nomination && mode2.nominees.Count == 0 && mode2.hohId != null && mode2.hohId != mode2.playerId
                && Word(mode2) != null);

        /// <summary>The Head of Household's standing safety pact with a houseguest still in the house: a deal's protection holds whole.</summary>
        private static UnifiedCommitmentState Word(EpisodeState s) => s.unifiedCommitments.FirstOrDefault(r => r.kind == UnifiedCommitments.Safety
            && r.sourcePolicy == UnifiedCommitments.DealPolicy && r.status == DealStatus.Active && (r.makerId == s.hohId || r.beneficiaryId == s.hohId)
            && s.Find(r.makerId == s.hohId ? r.beneficiaryId : r.makerId)?.status == ContestantStatus.Active);

        [Test]
        public void AnNpcHeadOfHouseholdWeighsTheSafetyTheyPromisedInModeTwo()
        {
            var (mode1, mode2) = Safeword();
            string hoh = mode2.hohId;
            var word = Word(mode2);
            string promised = word.makerId == hoh ? word.beneficiaryId : word.makerId;
            double strength = UnifiedCommitments.StrongestProtection(mode2, hoh, promised).Strength;
            Assert.That(strength, Is.GreaterThan(0), "Their word protects " + promised + ".");
            Assert.That(StrategyRules.NominationReluctance(mode2, hoh, promised), Is.EqualTo(StrategyRules.NominationReluctance(mode1, hoh, promised)));
            Assert.That(EpisodeEngine.NominationWeight(mode2, hoh, promised), Is.EqualTo(EpisodeEngine.NominationWeight(mode1, hoh, promised)));
            Assert.That(StoryConsumers.PromisedSafety(mode2, hoh, promised) || StoryConsumers.TheirWord(mode2, hoh, promised), Is.True);
            // Without the word the weight falls by its protection, less the story's own overlap with it.
            var unpromised = mode2.Clone();
            unpromised.unifiedCommitments.Single(r => r.id == word.id).status = DealStatus.Expired;
            Assert.That(StrategyRules.NominationReluctance(mode2, hoh, promised) - StrategyRules.NominationReluctance(unpromised, hoh, promised),
                Is.EqualTo(strength).Within(1e-9), "The canonical Safety word is the Head of Household's reluctance.");
            // And the real nomination, through the seam, is mode 1's.
            var command = ProspectiveVoteTwins.Command(mode2, EpisodeCommandKind.Advance);
            var one = new EpisodeEngine(mode1).Apply(command);
            var two = ProspectiveVoteFacade.Engine(mode2).Apply(command);
            Assert.That(one.accepted && two.accepted, Is.True, one.reason + " / " + two.reason);
            Assert.That(two.state.nominees, Is.EqualTo(one.state.nominees), "The same names, in the same order.");
        }

        // ------------------------------------------------------------ an ally's ballot

        /// <summary>
        /// An ally who breaks a vote deal with the player by their ballot turns on their pact (C2), in mode 2 as in mode 1: the
        /// reveal reads the canonical deal it stamped (EpisodeEngine.BallotBetrayals) and writes the betrayal on the player's
        /// record, its line to the betrayer alone while the ballot is hidden. The whole reveal equals mode 1's after projection.
        /// </summary>
        [Test]
        public void AnAllyWhoBreaksACanonicalVoteDealByTheirBallotTurnsOnThePact()
        {
            var s = ModeTwoReaderSweep.Campaign();
            string ally = PinnedVoteSeason.NpcVoters(s).First(), x = s.nominees[0], y = s.nominees[1];
            // The pact, struck by the real command on the season's next draw (the negotiation fixtures' constructed fact).
            s.randomState = ProspectiveVoteTwins.Draw(true, 1);
            var pact = new EpisodeEngine(ProspectiveVoteTwins.Valid(s)).Apply(ProspectiveVoteTwins.Command(s, EpisodeCommandKind.FormAlliance, ally));
            Assert.That(pact.accepted && pact.state.alliances.Any(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(ally)), Is.True,
                "Fixture: the pact was formed. " + pact.reason);
            s = ModeTwoReaderSweep.Strike(pact.state, ally, DealKind.VoteEvict, x);
            var revealed = ModeTwoReaderSweep.Reveal(s, x, new Dictionary<string, string> { [ally] = y });
            Assert.That(revealed.Mode2.relationships.Single(r => r.fromId == s.playerId && r.toId == ally).events.Any(e => e.type == Allegiance.BetrayedType),
                Is.True, "The betrayal is on the player's record.");
            ProspectiveVoteTwins.AssertProjection(revealed.Projection, revealed.Mode2, "The ally's breach of a canonical vote deal");
        }

        // ------------------------------------------------------------ a hidden ballot (P3)

        [Test]
        public void TheLeversShowNothingOfABallotThePlayerCannotKnow()
        {
            var blind = ModeTwoReaderSweep.Flip(false);
            blind.AssertBlind("EpisodeEngine.LeverTerms", s => EpisodeEngine.LeverTerms(s, blind.PartnerId));
            blind.AssertBlind("The lever lines", s => s.events.Where(e => e.kind == "lever").Select(e => e.text).ToList());
        }

        private static string Json(object value) => PinnedVoteSeason.Json(value);
    }
}
