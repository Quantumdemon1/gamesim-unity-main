using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5d: the player's bargaining and the player's word read mode 2 as they read mode 1 - a move's odds
    /// (<see cref="Negotiation.Chance"/>), the promises owed and their call-in, a safety promise held at the nominations, the
    /// breaches the player can mend and the words for them, a deal's acceptance and the refusal's reason, the history the notes
    /// count, the word the house has heard and the vote read's knowledge of a deal term. <see cref="KnownBallots.DealOutcomeKnown"/>
    /// stays on every vote row: a breach the player cannot know of is none they can mend. Breach counts take a vote breach once
    /// per Rule2 incident (the lead's decision D1).
    /// </summary>
    public sealed class ModeTwoBargainReaderTests
    {
        private static readonly string[] Moves = { Negotiation.Remind, Negotiation.Demand, Negotiation.Threaten, Negotiation.MendFences, Negotiation.VetoForAPrice };

        /// <summary>
        /// The player's bargaining at one of a walk's moments in free time or the campaign (<see cref="ModeTwoReaderParityTests"/>):
        /// for every houseguest, each move's odds both ways, the promises owed and their call-in, the breaches to mend and their
        /// words, the history, each deal's acceptance, reason and shown odds; the word the house has heard; a safety promise held.
        /// The breach counts only where no incident has two rows (<paramref name="overlap"/>, D1).
        /// </summary>
        internal static void CheckBargain(EpisodeState mode1, EpisodeState mode2, string where, bool overlap)
        {
            foreach (var npc in mode1.Active.Where(c => !c.isPlayer).Select(c => c.id))
            {
                foreach (var move in Moves)
                {
                    Assert.That(Negotiation.Chance(mode2, npc, move, true), Is.EqualTo(Negotiation.Chance(mode1, npc, move, true)), where + ": shown odds of " + move + " with " + npc);
                    Assert.That(Negotiation.Chance(mode2, npc, move, false), Is.EqualTo(Negotiation.Chance(mode1, npc, move, false)), where + ": odds of " + move + " with " + npc);
                }
                var owed = Negotiation.Owed(mode1, npc);
                Assert.That(Json(Negotiation.Owed(mode2, npc)), Is.EqualTo(Json(owed)), where + ": promises " + npc + " owes");
                foreach (var promise in owed)
                    Assert.That(Negotiation.CallInRefusal(mode2, npc, promise.id, Negotiation.Remind), Is.EqualTo(Negotiation.CallInRefusal(mode1, npc, promise.id, Negotiation.Remind)),
                        where + ": calling in " + promise.id);
                Assert.That(Negotiation.BreachWords(mode2, npc), Is.EqualTo(Negotiation.BreachWords(mode1, npc)), where + ": the breach a mend is about, with " + npc);
                // Designed (V5d, the knowledge gate): mode 2's history leaves out the player's memories that tell a ballot they
                // cannot know, which mode 1 counts; the odds' unknowns read it, so they are compared where there are none.
                int hidden = HiddenMemories(mode1, npc);
                Assert.That(KnownOdds.History(mode2, npc), Is.EqualTo(KnownOdds.History(mode1, npc) - hidden), where + ": the history with " + npc);
                Assert.That(Negotiation.SafetyHeld(mode2, npc, mode2.playerId), Is.EqualTo(Negotiation.SafetyHeld(mode1, npc, mode1.playerId)), where + ": safety held by " + npc);
                if (overlap) continue;
                Assert.That(Negotiation.BreachesAgainst(mode2, npc), Is.EqualTo(Negotiation.BreachesAgainst(mode1, npc)), where + ": breaches against " + npc);
                Assert.That(Negotiation.MendRefusal(mode2, npc), Is.EqualTo(Negotiation.MendRefusal(mode1, npc)), where + ": mending with " + npc);
                foreach (var kind in DealKind.All)
                {
                    string about = kind == DealKind.VoteSave || kind == DealKind.VoteEvict ? mode1.nominees.FirstOrDefault(id => id != npc)
                        : kind == DealKind.TargetAgreement ? mode1.Active.FirstOrDefault(c => !c.isPlayer && c.id != npc)?.id : null;
                    Assert.That(PlayerDeals.AcceptanceChance(mode2, npc, kind, about), Is.EqualTo(PlayerDeals.AcceptanceChance(mode1, npc, kind, about)),
                        where + ": acceptance of " + kind + " by " + npc);
                    Assert.That(PlayerDeals.Reasoning(mode2, npc, kind, false), Is.EqualTo(PlayerDeals.Reasoning(mode1, npc, kind, false)), where + ": a no to " + kind);
                    if (hidden == 0)
                        Assert.That(Json(KnownOdds.Deal(mode2, npc, kind, about)), Is.EqualTo(Json(KnownOdds.Deal(mode1, npc, kind, about))), where + ": the shown odds of " + kind);
                    else
                        Assert.That(KnownOdds.Deal(mode2, npc, kind, about).chance, Is.EqualTo(KnownOdds.Deal(mode1, npc, kind, about).chance), where + ": the shown chance of " + kind);
                }
            }
            Assert.That(Json(YourWord.Breaches(mode2)), Is.EqualTo(Json(YourWord.Breaches(mode1))), where + ": the word the house heard");
            Assert.That(YourWord.Cost(mode2), Is.EqualTo(YourWord.Cost(mode1)), where + ": what the player's word costs");
            Assert.That(YourWord.Word(mode2), Is.EqualTo(YourWord.Word(mode1)), where + ": the player's word");
        }

        /// <summary>The player's memories of a houseguest that tell a ballot the player cannot know (KnownBallots.PlayerMemories leaves them out).</summary>
        private static int HiddenMemories(EpisodeState s, string npcId) =>
            s.memories.Count(m => m.ownerId == s.playerId && m.subjectId == npcId && !string.IsNullOrEmpty(m.text))
            - KnownBallots.PlayerMemories(s).Count(m => m.subjectId == npcId && !string.IsNullOrEmpty(m.text));

        [Test]
        public void TheHistoryTheOddsReadLeavesOutAMemoryThatTellsAHiddenBallot()
        {
            // The flip pair's partner broke the deal, and the player remembers it - a memory that tells the partner's ballot,
            // which the player cannot know. Mode 1's history counts it; mode 2's, through the knowledge gate, does not.
            var blind = ModeTwoReaderSweep.Flip(false);
            Assert.That(HiddenMemories(blind.Broken, blind.PartnerId) - HiddenMemories(blind.Kept, blind.PartnerId), Is.EqualTo(1),
                "Fixture: the breach is remembered, and hidden - mode 1's count of raw memories would tell it.");
            Assert.That(KnownOdds.History(blind.Broken, blind.PartnerId), Is.EqualTo(KnownOdds.History(blind.Kept, blind.PartnerId)));
            Assert.That(KnownOdds.Unknowns(blind.Broken, blind.PartnerId), Is.EqualTo(KnownOdds.Unknowns(blind.Kept, blind.PartnerId)));
        }

        // ------------------------------------------------------------ calling in a canonical Safety promise

        private static EpisodeState owedCampaign;

        /// <summary>
        /// A first campaign in which a houseguest's promise of safety to the player stands, canonical as Safety is in mode 1. A
        /// story files such a promise (EpisodeEngine.StoryPromise, origin story-promise); it is filed here as a story files it -
        /// its id the season's next sequence, consumed - on the public mode-1 copy, which public validation then accepts.
        /// </summary>
        private static EpisodeState OwedCampaign()
        {
            if (owedCampaign == null)
            {
                var s = ProspectiveVoteTwins.Find("a first campaign", seed => ProspectiveVoteTwins.Walk(seed, x => x.phase == EpisodePhase.Campaign));
                string npc = s.Active.First(c => !c.isPlayer && c.id != s.hohId).id;
                s.unifiedCommitments.Add(new UnifiedCommitmentState
                {
                    id = "promise-" + s.nextSequence, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy,
                    origin = UnifiedCommitments.StoryPromise, makerId = npc, beneficiaryId = s.playerId, reciprocal = false,
                    createdWeek = s.week, expiresWeek = s.week + 1, status = DealStatus.Active, trustImpact = DealTrust.Medium,
                });
                s.nextSequence++;
                owedCampaign = ProspectiveVoteTwins.Valid(s);
            }
            return owedCampaign.Clone();
        }

        [Test]
        public void TheCanonicalSafetyPromiseAHouseguestOwesIsCalledInAsInModeOne()
        {
            var s = OwedCampaign();
            var row = s.unifiedCommitments.Single(r => r.kind == UnifiedCommitments.Safety && r.origin == UnifiedCommitments.StoryPromise);
            var twin = ProspectiveVoteTwins.Twin(s);
            Assert.That(Json(Negotiation.Owed(twin, row.makerId)), Is.EqualTo(Json(Negotiation.Owed(s, row.makerId))));
            Assert.That(Negotiation.Owed(twin, row.makerId).Select(p => p.id), Does.Contain(row.id), "Owed in mode 2 too.");
            Assert.That(Negotiation.CallInRefusal(twin, row.makerId, row.id, Negotiation.Demand), Is.Null);
            var (legacy, prospective) = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Negotiate, row.makerId, row.id,
                Negotiation.CallIn(Negotiation.Demand)));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, "Mode 2 calls it in: " + prospective.reason);
            ProspectiveVoteTwins.AssertParity(legacy.state, prospective.state, "Calling in a canonical Safety promise");
            // Held, it weighs at the nominations as mode 1 weighs it.
            Assert.That(Negotiation.SafetyHeld(prospective.state, row.makerId, s.playerId),
                Is.EqualTo(Negotiation.SafetyHeld(legacy.state, row.makerId, s.playerId)));
        }

        // ------------------------------------------------------------ amends for a canonical Vote breach

        [Test]
        public void TheFenceAPlayerBrokeByACanonicalVoteBreachIsMendedAsInModeOne()
        {
            ModeTwoReaderSweep.Injected mend = null;
            for (uint seed = 1; seed <= 32 && mend == null; seed++)
                mend = ModeTwoReaderSweep.LockstepUntil(seed, 8, true, s =>
                {
                    if (s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign) return null;
                    if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
                    var wronged = s.promises.Where(p => p.kind == PromiseKind.Vote && p.status == PromiseStatus.Broken && p.fromId == s.playerId)
                        .Select(p => p.toId).Concat(s.deals.Where(d => KnownBallots.IsVoteDeal(d.type) && d.status == DealStatus.Broken && d.brokenById == s.playerId)
                            .Select(d => d.proposerId == s.playerId ? d.recipientId : d.proposerId))
                        .FirstOrDefault(id => s.Find(id)?.status == ContestantStatus.Active && Negotiation.MendRefusal(s, id) == null);
                    return wronged == null ? null : ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Negotiate, wronged, text: Negotiation.MendFences);
                });
            Assert.That(mend, Is.Not.Null, "A walk reaches a fence the player broke by a vote breach, still to mend.");
            Assert.That(mend.Legacy.accepted, Is.True, mend.Legacy.reason);
            Assert.That(mend.Prospective.accepted, Is.True, "Mode 2 mends it: " + mend.Prospective.reason);
            ProspectiveVoteTwins.AssertProjection(mend.Projection, mend.Mode2, "Mending a fence broken by a canonical vote breach");
            string npc = mend.Command.targetId;
            Assert.That(Negotiation.MendsTried(mend.Mode2, npc), Is.EqualTo(Negotiation.MendsTried(mend.Projection, npc)));
        }

        // ------------------------------------------------------------ a hidden ballot (P3)

        [Test]
        public void TheBargainingShowsNothingOfABallotThePlayerCannotKnow()
        {
            var blind = ModeTwoReaderSweep.Flip(false);
            string partner = blind.PartnerId;
            blind.AssertBlind("Negotiation.BreachesAgainst", s => Negotiation.BreachesAgainst(s, partner));
            blind.AssertBlind("Negotiation.MendRefusal", s => Negotiation.MendRefusal(s, partner));
            blind.AssertBlind("Negotiation.BreachWords", s => Negotiation.BreachWords(s, partner));
            blind.AssertBlind("The shown odds of every move", s => Moves.Select(move => Negotiation.Chance(s, partner, move, true)).ToList());
            blind.AssertBlind("KnownOdds.History", s => KnownOdds.History(s, partner));
            blind.AssertBlind("The vote read's deal term", s => VoteRead.FactorKnown(s, partner, blind.EvictId,
                new WebVoteFactor { code = "deal", evidenceIds = new List<string> { blind.DealId } }));
            blind.AssertBlind("The shown odds of a deal", s => KnownOdds.Deal(s, partner, DealKind.VoteTogether, null));
        }

        private static string Json(object value) => PinnedVoteSeason.Json(value);
    }
}
