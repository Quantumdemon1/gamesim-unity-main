using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Canonical proposal/history consumers; source gateways are not production activation or save acceptance.</summary>
    public sealed class UnifiedSafetyNegotiationReaderTests
    {
        [TestCase(1, false, false)] [TestCase(1, false, true)]
        [TestCase(3, false, false)] [TestCase(3, false, true)]
        [TestCase(5, true, false)] [TestCase(5, true, true)]
        public void DealReputationAndMendAllowanceCountTheActualIncidentOnce(int copies, bool reversed, bool promise)
        {
            var s = State(); string npc = Other(s);
            for (int i = 0; i < copies; i++)
                s.unifiedCommitments.Add(Row(s, false, "deal-" + i, reversed && i % 2 == 0 ? npc : s.playerId,
                    reversed && i % 2 == 0 ? s.playerId : npc));
            if (promise) s.unifiedCommitments.Add(Row(s, true, "promise"));
            Break(s, "nomination", s.playerId, npc); string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(1));
            Assert.That(NpcDeals.BrokenDeals(s, npc), Is.Zero);
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.EqualTo(1));
            Assert.That(Negotiation.MendRefusal(s, npc), Is.Null);
            Assert.That(NpcDeals.Adjusted(s, npc, s.playerId), Is.EqualTo(s.Score(npc, s.playerId) - NpcDeals.BrokenDealPenalty));
            Assert.That(PlayerDeals.WordPenalty(s), Is.EqualTo(PlayerDeals.BrokenDealPenalty));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(3)] [TestCase(5)]
        public void PromiseOnlyBreachIsMendableButDoesNotBecomeADealPenalty(int copies)
        {
            var s = State();
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Row(s, true, "promise-" + i));
            Break(s, "nomination", s.playerId, Other(s)); string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.Zero);
            Assert.That(PlayerDeals.WordPenalty(s), Is.Zero);
            Assert.That(Negotiation.BreachesAgainst(s, Other(s)), Is.EqualTo(1));
            Assert.That(Negotiation.BreachWords(s, Other(s)), Is.EqualTo("the promise of safety you broke"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void WrongedPlayerIsNeverTheBreakerInReciprocalSafety(bool reverse)
        {
            var s = State(); string npc = Other(s);
            s.unifiedCommitments.Add(Row(s, false, "deal", reverse ? npc : s.playerId, reverse ? s.playerId : npc));
            s.unifiedCommitments.Add(Row(s, true, "npc-promise", npc, s.playerId));
            Break(s, "nomination", npc, s.playerId); string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, npc), Is.EqualTo(1));
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.Zero);
            Assert.That(PlayerDeals.WordPenalty(s), Is.Zero);
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.Zero);
            Assert.That(Negotiation.BreachWords(s, npc), Is.EqualTo("the word you broke"));
            Assert.That(Negotiation.MendRefusal(s, npc), Does.StartWith("You have broken no word"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void DifferentDecisionsAgainstTheSamePersonEachPermitOneMend()
        {
            var s = State(); s.week = 2;
            s.unifiedCommitments.Add(Row(s, true, "first")); Break(s, "nomination", s.playerId, Other(s));
            s.week = 3; s.unifiedCommitments.Add(Row(s, true, "second"));
            s.unifiedCommitments.Add(Row(s, false, "second-deal")); Break(s, "replacement", s.playerId, Other(s));
            string before = Json(s);
            Assert.That(Negotiation.BreachesAgainst(s, Other(s)), Is.EqualTo(2));
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(1), "Only the second incident has deal provenance.");
            Assert.That(Json(s), Is.EqualTo(before));
            Edge(s, Other(s), s.playerId).events.Add(new RelationshipEventState { week = 3, type = Negotiation.AmendsType });
            Assert.That(Negotiation.MendRefusal(s, Other(s)), Is.Null);
            Edge(s, Other(s), s.playerId).events.Add(new RelationshipEventState { week = 3, type = Negotiation.RebuffType });
            before = Json(s);
            Assert.That(Negotiation.MendRefusal(s, Other(s)), Does.StartWith("You have tried to make amends for every word"));
            Assert.That(Negotiation.BreachesAgainst(s, Other(s)), Is.EqualTo(2));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void DifferentWrongedPartiesKeepSeparatePairAllowances()
        {
            var s = State(); string a = Other(s), b = Other(s, 1);
            s.unifiedCommitments.Add(Row(s, false, "a", s.playerId, a));
            s.unifiedCommitments.Add(Row(s, false, "b", s.playerId, b));
            Break(s, "nomination", s.playerId, a, b); string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(2));
            Assert.That(Negotiation.BreachesAgainst(s, a), Is.EqualTo(1));
            Assert.That(Negotiation.BreachesAgainst(s, b), Is.EqualTo(1));
            Assert.That(Negotiation.BreachesAgainst(s, Other(s, 2)), Is.Zero);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(DealStatus.Active)] [TestCase(DealStatus.Proposed)] [TestCase(DealStatus.Declined)]
        [TestCase(DealStatus.Expired)] [TestCase(DealStatus.Fulfilled)]
        public void UnbrokenProvenanceNeverBecomesABreach(string status)
        {
            var s = State(); var row = Row(s, false, "unbroken"); row.status = status;
            if (status == DealStatus.Fulfilled) row.settledWeek = s.week;
            s.unifiedCommitments.Add(row); string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.Zero);
            Assert.That(Negotiation.BreachesAgainst(s, Other(s)), Is.Zero);
            Assert.That(Negotiation.BreachWords(s, Other(s)), Is.EqualTo("the word you broke"));
            Assert.That(KnownOdds.History(s, Other(s)), Is.EqualTo(1), "History includes an offer even if it never bound.");
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(DealKind.FinalTwo)] [TestCase(DealKind.VetoUse)]
        [TestCase(DealKind.InformationSharing)] [TestCase(DealKind.TargetAgreement)]
        public void OtherLegacyDealFamiliesKeepTheirSeparateSourcePenalties(string kind)
        {
            var s = State(); string npc = Other(s);
            s.deals.Add(LegacyDeal(s, "legacy", kind, s.playerId, npc));
            s.unifiedCommitments.Add(Row(s, false, "canonical")); Break(s, "nomination", s.playerId, npc);
            string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(2));
            Assert.That(NpcDeals.BrokenDeals(s, npc), Is.Zero);
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.EqualTo(2));
            Assert.That(PlayerDeals.WordPenalty(s), Is.EqualTo(2 * PlayerDeals.BrokenDealPenalty));
            Assert.That(KnownOdds.History(s, npc), Is.EqualTo(2));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(Negotiation.Remind, false, false)] [TestCase(Negotiation.Remind, true, false)]
        [TestCase(Negotiation.Remind, false, true)] [TestCase(Negotiation.Remind, true, true)]
        [TestCase(Negotiation.Demand, false, false)] [TestCase(Negotiation.Demand, true, false)]
        [TestCase(Negotiation.Demand, false, true)] [TestCase(Negotiation.Demand, true, true)]
        [TestCase(Negotiation.Threaten, false, false)] [TestCase(Negotiation.Threaten, true, false)]
        [TestCase(Negotiation.Threaten, false, true)] [TestCase(Negotiation.Threaten, true, true)]
        [TestCase(Negotiation.MendFences, false, false)] [TestCase(Negotiation.MendFences, true, false)]
        [TestCase(Negotiation.MendFences, false, true)] [TestCase(Negotiation.MendFences, true, true)]
        [TestCase(Negotiation.VetoForAPrice, false, false)] [TestCase(Negotiation.VetoForAPrice, true, false)]
        [TestCase(Negotiation.VetoForAPrice, false, true)] [TestCase(Negotiation.VetoForAPrice, true, true)]
        public void MovePenaltyRetainsDealFamilyAndKnownActualActionPolicy(string move, bool known, bool hasDeal)
        {
            var s = State(); string npc = Other(s);
            s.unifiedCommitments.Add(Row(s, true, "promise"));
            if (hasDeal) for (int i = 0; i < 3; i++) s.unifiedCommitments.Add(Row(s, false, "deal-" + i));
            Break(s, "nomination", s.playerId, npc); string before = Json(s);
            Assert.That(Negotiation.Chance(s, npc, move, known), Is.EqualTo(Negotiation.Base(move) - (hasDeal ? 30 : 0)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void AnotherPairOrAnotherActorCannotSupplyThePlayersMovePenalty(bool npcActs)
        {
            var s = State(); string npc = Other(s), outsider = Other(s, 1);
            s.unifiedCommitments.Add(Row(s, false, "private", npcActs ? npc : s.playerId, outsider));
            Break(s, "nomination", npcActs ? npc : s.playerId, outsider); string before = Json(s);
            Assert.That(Negotiation.Chance(s, npc, Negotiation.MendFences, false), Is.EqualTo(Negotiation.Base(Negotiation.MendFences)));
            Assert.That(Negotiation.Chance(s, npc, Negotiation.MendFences, true), Is.EqualTo(Negotiation.Base(Negotiation.MendFences)));
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.Zero);
            Assert.That(KnownOdds.History(s, npc), Is.Zero);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void BreachCaptionUsesActualIncidentOwnerNotAgreementOrder(bool promise, bool reverse)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, false, "deal"));
            if (promise) s.unifiedCommitments.Add(Row(s, true, "promise"));
            if (reverse) s.unifiedCommitments.Reverse(); Break(s, "nomination", s.playerId, Other(s));
            string before = Json(s);
            Assert.That(UnifiedCommitmentHistory.Breaches(s).Single().EffectOwnerId, Is.EqualTo(promise ? "promise" : "deal"));
            Assert.That(Negotiation.BreachWords(s, Other(s)), Is.EqualTo(promise ? "the promise of safety you broke"
                : "the " + DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant() + " you broke"));
            Assert.That(Negotiation.MendLine(s, Other(s), true), Does.Contain(Negotiation.BreachWords(s, Other(s))));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void CanonicalCaptionUsesSettlementChronologyRatherThanOldCreationWeek(bool promise)
        {
            var s = State(); s.week = 2; var row = Row(s, promise, "older-proposal");
            if (!promise) { row.origin = UnifiedCommitments.Lobby; row.expiresWeek = 3; }
            s.unifiedCommitments.Add(row); s.week = 3; Break(s, "nomination", s.playerId, Other(s));
            var earlier = LegacyDeal(s, "earlier", DealKind.InformationSharing, s.playerId, Other(s));
            earlier.week = 2; earlier.settledWeek = 2; s.deals.Add(earlier); string before = Json(s);
            Assert.That(Negotiation.BreachWords(s, Other(s)), Is.EqualTo(promise ? "the promise of safety you broke"
                : "the " + DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant() + " you broke"));
            Assert.That(Json(s), Is.EqualTo(before));
            s.deals.Clear(); s.deals.Add(earlier); s.deals[0].week = 3; s.deals[0].settledWeek = 3;
            before = Json(s);
            Assert.That(Negotiation.BreachWords(s, Other(s)), Is.EqualTo("the " + DealKind.Title(DealKind.InformationSharing).ToLowerInvariant() + " you broke"),
                "An equal week retains the established legacy choice.");
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void CanonicalBreachDoesNotOverrideALaterOtherFamilyOutcome(bool promise)
        {
            var s = State(); s.week = 2; s.unifiedCommitments.Add(Row(s, promise, "old"));
            Break(s, "nomination", s.playerId, Other(s)); s.week = 3;
            s.promises.Add(new PromiseState { id = "later", fromId = s.playerId, toId = Other(s), kind = PromiseKind.FinalTwo,
                status = PromiseStatus.Broken, week = 1, settledWeek = 3, brokenById = s.playerId });
            string before = Json(s);
            Assert.That(Negotiation.BreachWords(s, Other(s)), Is.EqualTo("the promise of a final two you broke"));
            Assert.That(Negotiation.BreachesAgainst(s, Other(s)), Is.EqualTo(2));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void SecretLegacyBallotStillSeparatesShownAndRolledPenalty(bool withCanonical)
        {
            var s = State(); string npc = Other(s, 3), evicted = Other(s, 1), nominee = Other(s, 2);
            s.ledger.power.Add(new PowerRow { week = 1, hohId = Other(s), evicteeId = evicted,
                nominees = new List<string> { evicted, nominee }, tally = new List<int> { 2, 1 } });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = evicted });
            var bloc = new DealState { id = "secret", type = DealKind.VoteTogether, proposerId = s.playerId,
                recipientId = npc, status = DealStatus.Broken, week = 1, expiresWeek = 1, settledWeek = 1,
                trustImpact = DealTrust.Medium }; s.deals.Add(bloc);
            if (withCanonical) { s.unifiedCommitments.Add(Row(s, true, "promise", s.playerId, npc)); Break(s, "nomination", s.playerId, npc); }
            string before = Json(s);
            Assert.That(KnownBallots.DealOutcomeKnown(s, bloc), Is.False);
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.EqualTo(withCanonical ? 1 : 0));
            Assert.That(Negotiation.Chance(s, npc, Negotiation.MendFences, true), Is.EqualTo(55));
            Assert.That(Negotiation.Chance(s, npc, Negotiation.MendFences, false), Is.EqualTo(25));
            Assert.That(Json(s), Is.EqualTo(before));
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npc, targetId = nominee, source = ClaimSource.Told, status = ClaimStatus.Kept });
            before = Json(s);
            Assert.That(KnownBallots.DealOutcomeKnown(s, bloc), Is.True);
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.EqualTo(withCanonical ? 2 : 1));
            Assert.That(Negotiation.Chance(s, npc, Negotiation.MendFences, true), Is.EqualTo(25));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void AcceptancePenaltyIsOncePerDealIncidentWithoutKnowledge(bool reverse)
        {
            var s = State(); string npc = Other(s); double clean = PlayerDeals.AcceptanceChance(s, npc, DealKind.SafetyAgreement, null);
            for (int i = 0; i < 5; i++) s.unifiedCommitments.Add(Row(s, false, "deal-" + i));
            s.unifiedCommitments.Add(Row(s, true, "promise")); if (reverse) s.unifiedCommitments.Reverse();
            Break(s, "nomination", s.playerId, npc); string before = Json(s);
            Assert.That(PlayerDeals.AcceptanceChance(s, npc, DealKind.SafetyAgreement, null), Is.EqualTo(clean - PlayerDeals.BrokenDealPenalty));
            Assert.That(KnownOdds.Deal(s, npc, DealKind.SafetyAgreement, null).chance, Is.EqualTo(clean - PlayerDeals.BrokenDealPenalty));
            Assert.That(KnownOdds.History(s, npc), Is.EqualTo(6), "Six agreements, one actual incident.");
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void KnowledgeModeNeverSubstitutesPrivateHistoryForAnAudiblePenalty(bool promise)
        {
            var s = State(); EnableKnowledge(s); string npc = Other(s), observer = Other(s, 1);
            double clean = PlayerDeals.AcceptanceChance(s, observer, DealKind.SafetyAgreement, null);
            s.unifiedCommitments.Add(Row(s, promise, "private")); Break(s, "nomination", s.playerId, npc);
            string before = Json(s);
            Assert.That(YourWord.On(s), Is.True); Assert.That(PlayerDeals.WordPenalty(s), Is.Zero);
            Assert.That(PlayerDeals.AcceptanceChance(s, observer, DealKind.SafetyAgreement, null), Is.EqualTo(clean));
            Assert.That(KnownOdds.Deal(s, observer, DealKind.SafetyAgreement, null).chance, Is.EqualTo(clean));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(FactVisibility.Private)] [TestCase(FactVisibility.Whispered)] [TestCase(FactVisibility.Public)]
        public void AudibleKnowledgeOwnsAcceptanceAndNeverStacksAgreementAliases(string visibility)
        {
            var s = State(); EnableKnowledge(s); string npc = Other(s);
            double clean = PlayerDeals.AcceptanceChance(s, npc, DealKind.SafetyAgreement, null);
            for (int i = 0; i < 4; i++) s.unifiedCommitments.Add(Row(s, false, "deal-" + i));
            Break(s, "nomination", s.playerId, npc);
            for (int i = 0; i < 4; i++) s.story.facts.Add(new HouseFactState { id = "fact-" + i, kind = FactKinds.BrokenWord,
                refId = "deal-" + i, actorId = s.playerId, subjectId = npc, week = 3, visibility = visibility,
                knowers = new List<string> { s.playerId, npc, Other(s, 1) } });
            int hearings = visibility == FactVisibility.Private ? 0 : visibility == FactVisibility.Public
                ? Math.Min(YourWord.WholeHouse, s.contestants.Count - 1) : 2;
            double penalty = hearings * YourWord.PerHearing; string before = Json(s);
            Assert.That(PlayerDeals.WordPenalty(s), Is.EqualTo(penalty));
            Assert.That(PlayerDeals.AcceptanceChance(s, npc, DealKind.SafetyAgreement, null), Is.EqualTo(Math.Max(PlayerDeals.MinimumChance, clean - penalty)));
            Assert.That(KnownOdds.Deal(s, npc, DealKind.SafetyAgreement, null).chance, Is.EqualTo(Math.Max(PlayerDeals.MinimumChance, clean - penalty)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void RefusalTrackRecordUsesActualPlayerPromiseEvenWithoutDealKnowledge(bool knowledge, bool playerBreaks)
        {
            var s = State(); if (knowledge) EnableKnowledge(s); string npc = Other(s);
            Set(s, s.playerId, npc, 30); Set(s, npc, s.playerId, 30);
            s.unifiedCommitments.Add(Row(s, true, "promise", playerBreaks ? s.playerId : npc, playerBreaks ? npc : s.playerId));
            Break(s, "nomination", playerBreaks ? s.playerId : npc, playerBreaks ? npc : s.playerId); string before = Json(s);
            Assert.That(PlayerDeals.Reasoning(s, npc, DealKind.SafetyAgreement, false), Is.EqualTo(playerBreaks
                ? "Your track record concerns me." : "I'm not sure this is the right move for me."));
            Assert.That(PlayerDeals.WordPenalty(s), Is.Zero, "Promise track-record words are not a deal-table penalty or new gossip fact.");
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void UnheardDealHistoryCannotBecomeAHearsayRefusal(bool publicFact)
        {
            var s = State(); EnableKnowledge(s); string npc = Other(s, 2);
            Set(s, s.playerId, npc, 30); Set(s, npc, s.playerId, 30);
            // Distinct actual weeks, not three aliases of one nomination: the source's hearsay
            // refusal requires more than twenty points of the public reading.
            for (int week = 1; week <= 3; week++)
            {
                s.week = week; string id = "deal-" + week; s.unifiedCommitments.Add(Row(s, false, id));
                Break(s, "nomination", s.playerId, Other(s));
                s.story.facts.Add(new HouseFactState { id = "fact-" + week, kind = FactKinds.BrokenWord, refId = id,
                    actorId = s.playerId, subjectId = Other(s), week = week, visibility = publicFact ? FactVisibility.Public : FactVisibility.Private,
                    knowers = new List<string> { s.playerId, Other(s) } });
            }
            string before = Json(s);
            Assert.That(UnifiedCommitmentHistory.Breaches(s).Count, Is.EqualTo(3));
            if (publicFact) Assert.That(PlayerDeals.WordPenalty(s), Is.GreaterThan(20));
            else Assert.That(PlayerDeals.WordPenalty(s), Is.Zero);
            Assert.That(PlayerDeals.Reasoning(s, npc, DealKind.SafetyAgreement, false), Is.EqualTo(publicFact
                ? "I've heard you've broken deals before. I can't trust that." : "I'm not sure this is the right move for me."));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void OnePublicIncidentUsesTrackRecordWithoutExaggeratedHearsay()
        {
            var s = State(); EnableKnowledge(s); string npc = Other(s, 2);
            Set(s, s.playerId, npc, 30); Set(s, npc, s.playerId, 30);
            s.unifiedCommitments.Add(Row(s, false, "deal")); Break(s, "nomination", s.playerId, Other(s));
            s.story.facts.Add(new HouseFactState { id = "fact", kind = FactKinds.BrokenWord, refId = "deal",
                actorId = s.playerId, subjectId = Other(s), week = 3, visibility = FactVisibility.Public,
                knowers = new List<string> { s.playerId, Other(s) } }); string before = Json(s);
            Assert.That(PlayerDeals.WordPenalty(s), Is.GreaterThan(0).And.LessThanOrEqualTo(20));
            Assert.That(PlayerDeals.Reasoning(s, npc, DealKind.SafetyAgreement, false), Is.EqualTo("Your track record concerns me."));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void HistoryRetainsEveryOwnAgreementNotJustEachIncident(bool reverse)
        {
            var s = State(); string npc = Other(s);
            for (int i = 0; i < 3; i++) s.unifiedCommitments.Add(Row(s, true, "promise-" + i,
                reverse ? npc : s.playerId, reverse ? s.playerId : npc));
            for (int i = 0; i < 4; i++) s.unifiedCommitments.Add(Row(s, false, "deal-" + i,
                reverse ? npc : s.playerId, reverse ? s.playerId : npc));
            Break(s, "nomination", reverse ? npc : s.playerId, reverse ? s.playerId : npc);
            s.unifiedCommitments.Add(Row(s, false, "other-own", s.playerId, Other(s, 1)));
            s.unifiedCommitments.Add(Row(s, true, "npc-private", Other(s, 1), npc)); string before = Json(s);
            Assert.That(KnownOdds.History(s, npc), Is.EqualTo(7));
            Assert.That(KnownOdds.History(s, Other(s, 1)), Is.EqualTo(1));
            Assert.That(KnownOdds.History(s, Other(s, 2)), Is.Zero);
            Assert.That(KnownOdds.Unknowns(s, npc), Is.EqualTo(KnownOdds.Some));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void HistoryKeepsAllOtherEvidenceAlongsideCanonicalProvenance()
        {
            var s = State(); string npc = Other(s); s.unifiedCommitments.Add(Row(s, true, "promise"));
            s.unifiedCommitments.Add(Row(s, false, "deal")); s.deals.Add(LegacyDeal(s, "old-deal", DealKind.FinalTwo, s.playerId, npc));
            s.promises.Add(new PromiseState { id = "old-promise", fromId = npc, toId = s.playerId, kind = PromiseKind.Vote });
            s.ledger.replies.Add(new ReplyRow { fromId = npc });
            s.ledger.calls.Add(new BlocCallRow { callerId = s.playerId, followed = new List<string> { npc } });
            s.alliances.Add(new AllianceState { id = "pact", members = new List<string> { s.playerId, npc } });
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = npc, text = "An actual remembered line." });
            s.memories.Add(new MemoryState { ownerId = npc, subjectId = s.playerId, text = "Their private memory." });
            string before = Json(s);
            Assert.That(KnownOdds.History(s, npc), Is.EqualTo(8));
            Assert.That(KnownOdds.Deal(s, npc, DealKind.SafetyAgreement, null).history, Is.EqualTo(8));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void LegacyRulesKeepBothPartyAttributionAndOriginalCaptionOrder(bool commitmentRules)
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0; s.commitmentRulesStartWeek = commitmentRules ? 1 : 0;
            string npc = Other(s); s.deals.Add(LegacyDeal(s, "first", DealKind.InformationSharing, npc, s.playerId));
            s.deals[0].brokenById = npc; s.deals.Add(LegacyDeal(s, "second", DealKind.FinalTwo, s.playerId, npc));
            s.deals.Add(LegacyDeal(s, "third", DealKind.TargetAgreement, s.playerId, npc));
            string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(commitmentRules ? 2 : 3));
            Assert.That(NpcDeals.BrokenDeals(s, npc), Is.EqualTo(commitmentRules ? 1 : 3));
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.EqualTo(commitmentRules ? 2 : 0));
            Assert.That(Negotiation.BreachWords(s, npc), Is.EqualTo("the " + DealKind.Title(commitmentRules ? DealKind.FinalTwo : DealKind.InformationSharing).ToLowerInvariant() + " you broke"));
            Assert.That(KnownOdds.History(s, npc), Is.EqualTo(3));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void RulesZeroDoesNotStartValidatingOrReadingAnUnenabledStore()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            s.unifiedCommitments.Add(new UnifiedCommitmentState { id = "malformed-disabled-row" }); string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.Zero);
            Assert.That(Negotiation.BreachesAgainst(s, Other(s)), Is.Zero);
            Assert.That(Negotiation.BreachWords(s, Other(s)), Is.EqualTo("the word you broke"));
            Assert.That(KnownOdds.History(s, Other(s)), Is.Zero);
            Assert.That(PlayerDeals.WordPenalty(s), Is.Zero);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualNominationGatewaySuppliesCanonicalEvidenceWithoutWritableMirrors(bool promise)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, false, "deal"));
            if (promise) s.unifiedCommitments.Add(Row(s, true, "promise"));
            Invoke("ResolveUnifiedSafetyNomination", s, "nomination", s.playerId, new[] { Other(s) });
            Assert.That(s.events.Any(e => e.kind == "promise-outcome")
                || s.relationships.Any(r => r.events.Any(e => e.type == YourWeek.DealBroken)), Is.True);
            string before = Json(s);
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(1));
            Assert.That(Negotiation.BreachesAgainst(s, Other(s)), Is.EqualTo(1));
            int agreements = promise ? 2 : 1;
            Assert.That(s.unifiedCommitments.Count, Is.EqualTo(agreements));
            Assert.That(s.unifiedCommitments.Select(row => row.id), Is.EquivalentTo(promise ? new[] { "deal", "promise" } : new[] { "deal" }));
            Assert.That(s.unifiedCommitments.All(row => row.status == DealStatus.Broken
                && row.brokenById == s.playerId && row.settledWeek == s.week), Is.True);
            var ownMemories = s.memories.Where(memory => memory.ownerId == s.playerId
                && memory.subjectId == Other(s) && !string.IsNullOrEmpty(memory.text)).ToList();
            Assert.That(ownMemories.Count, Is.EqualTo(promise ? 1 : 0),
                "The promise source remembers the outcome for both parties; the deal source remembers it only for the wronged one.");
            if (promise)
            {
                var memory = ownMemories.Single();
                Assert.That(memory.subjectId, Is.EqualTo(Other(s)));
                Assert.That(memory.week, Is.EqualTo(s.week)); Assert.That(memory.isPrivate, Is.True);
                Assert.That(memory.text, Is.EqualTo(s.Find(s.playerId).name + " broke a Safety promise."));
                Assert.That(s.events.Single(line => line.kind == "promise-outcome").text, Is.EqualTo(memory.text));
            }
            // History includes both durable agreement provenance and the player's own actual
            // remembered outcome. That memory is neither another agreement nor another betrayal.
            Assert.That(KnownOdds.History(s, Other(s)), Is.EqualTo(agreements + ownMemories.Count));
            Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualMendGatewayConsumesOneIncidentAllowanceButNeverErasesEvidence(bool lands)
        {
            var s = State(); string npc = Other(s); s.unifiedCommitments.Add(Row(s, true, "promise"));
            for (int i = 0; i < 3; i++) s.unifiedCommitments.Add(Row(s, false, "deal-" + i));
            Invoke("ResolveUnifiedSafetyNomination", s, "nomination", s.playerId, new[] { npc });
            double chance = Negotiation.Chance(s, npc, Negotiation.MendFences, false);
            s.randomState = Draw(lands, chance); var stream = new SeededRandom(s.randomState); stream.NextDouble();
            double score = s.Score(npc, s.playerId); string evidence = JsonConvert.SerializeObject(s.unifiedCommitments);
            Invoke("MendFences", s, s.Find(npc));
            Assert.That(s.randomState, Is.EqualTo(stream.State));
            Assert.That(s.Score(npc, s.playerId), Is.EqualTo(WebRules.ClampScore(score + (lands ? Negotiation.MendRepair : Negotiation.MendRebuff))));
            Assert.That(Negotiation.MendsTried(s, npc), Is.EqualTo(1));
            Assert.That(Negotiation.BreachesAgainst(s, npc), Is.EqualTo(1));
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(1));
            Assert.That(JsonConvert.SerializeObject(s.unifiedCommitments), Is.EqualTo(evidence));
            string once = Json(s); Assert.Catch<Exception>(() => Invoke("MendFences", s, s.Find(npc)));
            Assert.That(Json(s), Is.EqualTo(once), "Repeated gateway entry is refused before another roll or effect.");
        }

        [TestCase("identity")] [TestCase("effect")]
        public void CanonicalMechanicalReadersRejectMalformedEvidenceWithoutMutation(string fault)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, false, "deal")); Break(s, "nomination", s.playerId, Other(s));
            if (fault == "identity") s.unifiedCommitments[0].id = "";
            else s.unifiedCommitments[0].settlementEffectKey = "safety:not-the-recorded-incident";
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => NpcDeals.BrokenDeals(s, s.playerId));
            Assert.Throws<ArgumentException>(() => Negotiation.BreachesAgainst(s, Other(s)));
            Assert.Throws<ArgumentException>(() => Negotiation.BreachWords(s, Other(s)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.hohId = s.playerId; s.phase = EpisodePhase.Nomination;
            s.unifiedCommitmentRulesVersion = 1; s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            s.story.rulesStartWeek = 0; s.story.rulesVersion = 0; s.agencyRulesStartWeek = 0;
            s.promises.Clear(); s.deals.Clear(); s.alliances.Clear(); s.memories.Clear(); s.events.Clear();
            foreach (var person in s.contestants) person.traits.Clear();
            foreach (var edge in s.relationships) { edge.score = 0; edge.events.Clear(); }
            return s;
        }

        private static string Other(EpisodeState s, int index = 0) => s.contestants.Where(c => c.id != s.playerId).Skip(index).First().id;
        private static void EnableKnowledge(EpisodeState s) { s.story.rulesVersion = StoryRules.Current; s.story.rulesStartWeek = 1; }

        private static UnifiedCommitmentState Row(EpisodeState s, bool promise, string id, string maker = null, string beneficiary = null) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal,
            makerId = maker ?? s.playerId, beneficiaryId = beneficiary ?? Other(s), reciprocal = !promise,
            createdWeek = s.week, expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static DealState LegacyDeal(EpisodeState s, string id, string kind, string maker, string beneficiary) => new DealState {
            id = id, type = kind, proposerId = maker, recipientId = beneficiary, status = DealStatus.Broken,
            week = s.week, settledWeek = s.week, expiresWeek = s.week, trustImpact = DealTrust.Medium, brokenById = maker,
        };

        private static void Break(EpisodeState s, string decision, string actor, params string[] wronged)
        {
            s.hohId = actor; var evaluation = UnifiedCommitments.EvaluateNomination(s, decision, actor, wronged);
            Assert.That(evaluation.Changes, Is.Not.Empty);
            foreach (var change in evaluation.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }

        private static RelationshipState Edge(EpisodeState s, string from, string to)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) { edge = new RelationshipState { fromId = from, toId = to }; s.relationships.Add(edge); }
            return edge;
        }

        private static void Set(EpisodeState s, string from, string to, double score) => Edge(s, from, to).score = score;

        private static uint Draw(bool lands, double chance)
        {
            for (uint seed = 1; seed < 10000; seed++)
                if ((new SeededRandom(seed).NextDouble() * 100 < chance) == lands) return seed;
            throw new InvalidOperationException("A bounded source seed search could not produce the requested roll.");
        }

        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
