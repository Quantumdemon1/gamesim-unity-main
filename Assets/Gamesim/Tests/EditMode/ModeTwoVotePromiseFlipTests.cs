using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The flip pair on a vote promise (the V5 review's finding 8; the lead's decision for V6): a broken vote promise is
    /// knowable to the player only where that voter's ballot is known (<see cref="KnownBallots.PromiseOutcomeKnown"/>).
    ///
    /// <para>The partner's only vote promise to the player a season can hold is a story's (EpisodeEngine.StoryPromise: no
    /// target, so any ballot of theirs breaks it); a houseguest's own word on the vote is never given to the player, and the
    /// family's validation refuses one. So the pair is the vote deal's flip pair (<see cref="ModeTwoReaderSweep.Flip"/>) with
    /// that word given too: their one hidden ballot breaks the word in both seasons, and the deal with it in one. Under Rule2
    /// the two breaches are one incident there and apart in the other, so a reader that counted the word only where its
    /// incident had no deal (D1) told the hidden ballot - unless it also asks whether the player can know the word's ending.
    /// Every player-facing reader the vote family moved reads the two seasons alike; with the partner's ballot told and judged
    /// (the control) the finalist's record differs.</para>
    /// </summary>
    public sealed class ModeTwoVotePromiseFlipTests
    {
        private static ModeTwoReaderSweep.FlipPair blind, told;

        private static ModeTwoReaderSweep.FlipPair Blind() => blind ??= ModeTwoReaderSweep.Flip(false, word: true);
        private static ModeTwoReaderSweep.FlipPair Told() => told ??= ModeTwoReaderSweep.Flip(true, word: true);

        private static readonly string[] Moves = { Negotiation.Remind, Negotiation.Demand, Negotiation.Threaten, Negotiation.MendFences, Negotiation.VetoForAPrice };

        /// <summary>Every player-facing reader the vote family's flip pairs read, as the player sees it of the pair's partner.</summary>
        private static object Reader(string reader, ModeTwoReaderSweep.FlipPair pair, EpisodeState s)
        {
            string partner = pair.PartnerId;
            switch (reader)
            {
                case "FinalistRead": case "FinalCaseResume": case "JuryHouseRead": case "GameSense": case "YourWeek": case "ReceiptLine":
                    return ModeTwoFinaleReaderTests.Reader(reader, s, partner);
                case "CommitmentsRead.Of": case "CommitmentsRead.With": case "CommitmentsRead.AtStake": case "CommitmentsRead.ByTheRules":
                case "CommitmentsRead.Warning": case "AllianceRead": case "HouseDialogue":
                    return ModeTwoPageReaderTests.Reader(reader, s, partner);
                case "HouseguestNotes": return HouseguestNotes.For(s, partner);
                case "Negotiation.BreachesAgainst": return Negotiation.BreachesAgainst(s, partner);
                case "Negotiation.MendRefusal": return Negotiation.MendRefusal(s, partner);
                case "Negotiation.BreachWords": return Negotiation.BreachWords(s, partner);
                case "Negotiation.Chance": return Moves.Select(move => Negotiation.Chance(s, partner, move, true)).ToList();
                case "KnownOdds.History": return new object[] { KnownOdds.History(s, partner), KnownOdds.Unknowns(s, partner) };
                case "KnownOdds.Deal": return KnownOdds.Deal(s, partner, DealKind.VoteTogether, null);
                case "VoteRead.FactorKnown":
                    return VoteRead.FactorKnown(s, partner, pair.EvictId, new WebVoteFactor { code = "deal", evidenceIds = new List<string> { pair.DealId } });
                case "NpcDeals.Pending": return NpcDeals.Pending(s);
                case "KnownBallots.Read": return KnownBallots.Read(s, s.week);
                case "KnownBallots.PlayerMemories": return KnownBallots.PlayerMemories(s).Where(m => m.subjectId == partner).ToList();
                case "EpisodeEngine.LeverTerms": return EpisodeEngine.LeverTerms(s, partner);
                case "StoryOdds":
                    return new object[] { StoryOdds.Terms(s, null, new HouseEventChoice { subjectId = partner, checkBase = 50, approach = "charm" }),
                        StoryOdds.PlayerBrokeTheirWord(s, partner) };
                default: throw new ArgumentException(reader);
            }
        }

        [TestCase("FinalistRead")] [TestCase("FinalCaseResume")] [TestCase("JuryHouseRead")] [TestCase("GameSense")] [TestCase("YourWeek")]
        [TestCase("ReceiptLine")] [TestCase("CommitmentsRead.Of")] [TestCase("CommitmentsRead.With")] [TestCase("CommitmentsRead.AtStake")]
        [TestCase("CommitmentsRead.ByTheRules")] [TestCase("CommitmentsRead.Warning")] [TestCase("AllianceRead")] [TestCase("HouseDialogue")]
        [TestCase("HouseguestNotes")] [TestCase("Negotiation.BreachesAgainst")] [TestCase("Negotiation.MendRefusal")] [TestCase("Negotiation.BreachWords")]
        [TestCase("Negotiation.Chance")] [TestCase("KnownOdds.History")] [TestCase("KnownOdds.Deal")] [TestCase("VoteRead.FactorKnown")]
        [TestCase("NpcDeals.Pending")] [TestCase("KnownBallots.Read")] [TestCase("KnownBallots.PlayerMemories")] [TestCase("EpisodeEngine.LeverTerms")]
        [TestCase("StoryOdds")]
        public void AReaderShowsNothingOfTheBallotThatBrokeTheWord(string reader)
        {
            var pair = Blind();
            pair.AssertBlind(reader, s => Reader(reader, pair, s.Clone()));
        }

        /// <summary>
        /// The pair as built: the word broken in both, and the Rule2 incidents that differ - the word and the deal one incident
        /// where the deal broke, the word alone (and the deal kept) where it held.
        /// </summary>
        [Test]
        public void TheHiddenBallotGroupsTheWordWithTheDealOrApart()
        {
            var pair = Blind();
            string player = pair.Broken.playerId;
            var together = UnifiedVoteHistory.Incidents(pair.Broken).Single(i => i.ActorId == pair.PartnerId && i.WrongedId == player);
            Assert.That(together.EvidenceIds, Is.EquivalentTo(new[] { pair.WordId, pair.DealId }), "One incident of the word and the deal.");
            Assert.That(together.DealId, Is.EqualTo(pair.DealId));
            var apart = UnifiedVoteHistory.Incidents(pair.Kept).Single(i => i.ActorId == pair.PartnerId && i.WrongedId == player);
            Assert.That((apart.OwnerId, apart.DealId), Is.EqualTo((pair.WordId, (string)null)), "The word alone.");
            Assert.That(UnifiedVoteHistory.Fulfillments(pair.Kept).Single().OwnerId, Is.EqualTo(pair.DealId), "And the deal kept.");
            foreach (var state in new[] { pair.Kept, pair.Broken })
            {
                Assert.That(KnownBallots.Knows(state, state.week, pair.PartnerId), Is.False, "Fixture: the partner's ballot is hidden.");
                Assert.That(KnownBallots.PromiseOutcomeKnown(state, CommitmentReferences.FindPromise(state, pair.WordId)), Is.False,
                    "So the word's ending is not the player's to know.");
            }
        }

        /// <summary>
        /// The finalist's record, which counted the word once its incident had no deal: with the ballot hidden it says nothing of
        /// the word either way; told and judged, it says the word was broken where it broke alone, and once, as the deal, where the
        /// deal broke with it (D1).
        /// </summary>
        [Test]
        public void TheFinalistsRecordCountsTheWordOnlyWhereTheirBallotIsKnown()
        {
            var pair = Blind();
            foreach (var state in new[] { pair.Kept, pair.Broken })
                Assert.That(FinalistRead.TowardYou(state, pair.PartnerId).value, Does.Not.Contain("promise").And.Not.Contain("deal"),
                    "Nothing of a word or a deal whose ending the player cannot know.");
            var control = Told();
            Assert.That(FinalistRead.TowardYou(control.Kept, control.PartnerId).value, Does.Contain("Broke a promise to you").And.Not.Contain("deal"));
            Assert.That(FinalistRead.TowardYou(control.Broken, control.PartnerId).value, Does.Contain("Broke a deal with you").And.Not.Contain("promise"),
                "One incident, counted once, as its deal (D1).");
            control.AssertControl("FinalistRead", s => Reader("FinalistRead", control, s.Clone()));
        }
    }
}
