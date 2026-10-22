// Finite authored controls, not genuine save/corpus provenance. UNEXECUTED.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Gamesim.Persistence.Frozen28Data;

namespace Gamesim.Persistence.Frozen28ReadOnly
{
    [TestFixture]
    internal sealed class FrozenV28VocabularyTests
    {
        private static IEnumerable<TestCaseData> Cases()
        {
            yield return new TestCaseData("BondKinds", "ride-or-die", true);
            yield return new TestCaseData("BondKinds", "confidant", true);
            yield return new TestCaseData("BondKinds", "nemesis", true);
            yield return new TestCaseData("BondKinds", "showmance", true);
            yield return new TestCaseData("BondKinds", (string)null, false);
            yield return new TestCaseData("BondKinds", "__not_a_historical_value__", false);
            yield return new TestCaseData("BondKinds", "RIDE-OR-DIE", false);
            yield return new TestCaseData("BondStatus", "private", true);
            yield return new TestCaseData("BondStatus", "public", true);
            yield return new TestCaseData("BondStatus", "apart", true);
            yield return new TestCaseData("BondStatus", "ended", true);
            yield return new TestCaseData("BondStatus", "betrayed", true);
            yield return new TestCaseData("BondStatus", (string)null, false);
            yield return new TestCaseData("BondStatus", "__not_a_historical_value__", false);
            yield return new TestCaseData("BondStatus", "PRIVATE", false);
            yield return new TestCaseData("ClaimSource", "told", true);
            yield return new TestCaseData("ClaimSource", "overheard", true);
            yield return new TestCaseData("ClaimSource", "ally", true);
            yield return new TestCaseData("ClaimSource", "read", true);
            yield return new TestCaseData("ClaimSource", "juror", true);
            yield return new TestCaseData("ClaimSource", "missed", true);
            yield return new TestCaseData("ClaimSource", "deflected", true);
            yield return new TestCaseData("ClaimSource", (string)null, false);
            yield return new TestCaseData("ClaimSource", "__not_a_historical_value__", false);
            yield return new TestCaseData("ClaimSource", "TOLD", false);
            yield return new TestCaseData("ClaimStatus", "open", true);
            yield return new TestCaseData("ClaimStatus", "kept", true);
            yield return new TestCaseData("ClaimStatus", "lied", true);
            yield return new TestCaseData("ClaimStatus", (string)null, false);
            yield return new TestCaseData("ClaimStatus", "__not_a_historical_value__", false);
            yield return new TestCaseData("ClaimStatus", "OPEN", false);
            yield return new TestCaseData("CompetitionEntry", "played", true);
            yield return new TestCaseData("CompetitionEntry", "assist", true);
            yield return new TestCaseData("CompetitionEntry", "simulated", true);
            yield return new TestCaseData("CompetitionEntry", "thrown", true);
            yield return new TestCaseData("CompetitionEntry", "watched", true);
            yield return new TestCaseData("CompetitionEntry", (string)null, false);
            yield return new TestCaseData("CompetitionEntry", "__not_a_historical_value__", false);
            yield return new TestCaseData("CompetitionEntry", "PLAYED", false);
            yield return new TestCaseData("DealKind", "target_agreement", true);
            yield return new TestCaseData("DealKind", "safety_agreement", true);
            yield return new TestCaseData("DealKind", "vote_together", true);
            yield return new TestCaseData("DealKind", "vote_save", true);
            yield return new TestCaseData("DealKind", "vote_evict", true);
            yield return new TestCaseData("DealKind", "veto_use", true);
            yield return new TestCaseData("DealKind", "information_sharing", true);
            yield return new TestCaseData("DealKind", "final_two", true);
            yield return new TestCaseData("DealKind", "partnership", true);
            yield return new TestCaseData("DealKind", "alliance_invite", true);
            yield return new TestCaseData("DealKind", "final_three", true);
            yield return new TestCaseData("DealKind", (string)null, false);
            yield return new TestCaseData("DealKind", "__not_a_historical_value__", false);
            yield return new TestCaseData("DealKind", "TARGET_AGREEMENT", false);
            yield return new TestCaseData("DealStatus", "proposed", true);
            yield return new TestCaseData("DealStatus", "accepted", true);
            yield return new TestCaseData("DealStatus", "active", true);
            yield return new TestCaseData("DealStatus", "fulfilled", true);
            yield return new TestCaseData("DealStatus", "broken", true);
            yield return new TestCaseData("DealStatus", "declined", true);
            yield return new TestCaseData("DealStatus", "expired", true);
            yield return new TestCaseData("DealStatus", (string)null, false);
            yield return new TestCaseData("DealStatus", "__not_a_historical_value__", false);
            yield return new TestCaseData("DealStatus", "PROPOSED", false);
            yield return new TestCaseData("DealTrust", "low", true);
            yield return new TestCaseData("DealTrust", "medium", true);
            yield return new TestCaseData("DealTrust", "high", true);
            yield return new TestCaseData("DealTrust", "critical", true);
            yield return new TestCaseData("DealTrust", (string)null, false);
            yield return new TestCaseData("DealTrust", "__not_a_historical_value__", false);
            yield return new TestCaseData("DealTrust", "LOW", false);
            yield return new TestCaseData("FactKinds", "alliance", true);
            yield return new TestCaseData("FactKinds", "couple", true);
            yield return new TestCaseData("FactKinds", "broken-word", true);
            yield return new TestCaseData("FactKinds", "strike", true);
            yield return new TestCaseData("FactKinds", "outcome", true);
            yield return new TestCaseData("FactKinds", "secret", true);
            yield return new TestCaseData("FactKinds", "plan", true);
            yield return new TestCaseData("FactKinds", "conversation", true);
            yield return new TestCaseData("FactKinds", (string)null, false);
            yield return new TestCaseData("FactKinds", "__not_a_historical_value__", false);
            yield return new TestCaseData("FactKinds", "ALLIANCE", false);
            yield return new TestCaseData("FactVisibility", "private", true);
            yield return new TestCaseData("FactVisibility", "whispered", true);
            yield return new TestCaseData("FactVisibility", "known", true);
            yield return new TestCaseData("FactVisibility", "public", true);
            yield return new TestCaseData("FactVisibility", (string)null, false);
            yield return new TestCaseData("FactVisibility", "__not_a_historical_value__", false);
            yield return new TestCaseData("FactVisibility", "PRIVATE", false);
            yield return new TestCaseData("GrudgeCauses", "nominated", true);
            yield return new TestCaseData("GrudgeCauses", "replacement", true);
            yield return new TestCaseData("GrudgeCauses", "replacement-veto", true);
            yield return new TestCaseData("GrudgeCauses", "alliance-betrayed", true);
            yield return new TestCaseData("GrudgeCauses", "deal-broken", true);
            yield return new TestCaseData("GrudgeCauses", "promise-broken", true);
            yield return new TestCaseData("GrudgeCauses", "oath-broken", true);
            yield return new TestCaseData("GrudgeCauses", "lie-discovered", true);
            yield return new TestCaseData("GrudgeCauses", "voted-against", true);
            yield return new TestCaseData("GrudgeCauses", "story", true);
            yield return new TestCaseData("GrudgeCauses", "pile-on", true);
            yield return new TestCaseData("GrudgeCauses", (string)null, false);
            yield return new TestCaseData("GrudgeCauses", "__not_a_historical_value__", false);
            yield return new TestCaseData("GrudgeCauses", "NOMINATED", false);
            yield return new TestCaseData("HouseEventKind", "phase", true);
            yield return new TestCaseData("HouseEventKind", "house", true);
            yield return new TestCaseData("HouseEventKind", "proximity", true);
            yield return new TestCaseData("HouseEventKind", "ambient", true);
            yield return new TestCaseData("HouseEventKind", "emergent", true);
            yield return new TestCaseData("HouseEventKind", "crisis", true);
            yield return new TestCaseData("HouseEventKind", "story", true);
            yield return new TestCaseData("HouseEventKind", "activity", true);
            yield return new TestCaseData("HouseEventKind", (string)null, false);
            yield return new TestCaseData("HouseEventKind", "__not_a_historical_value__", false);
            yield return new TestCaseData("HouseEventKind", "PHASE", false);
            yield return new TestCaseData("HouseEventRisk", "low", true);
            yield return new TestCaseData("HouseEventRisk", "medium", true);
            yield return new TestCaseData("HouseEventRisk", "high", true);
            yield return new TestCaseData("HouseEventRisk", (string)null, false);
            yield return new TestCaseData("HouseEventRisk", "__not_a_historical_value__", false);
            yield return new TestCaseData("HouseEventRisk", "LOW", false);
            yield return new TestCaseData("LobbyApproach", "emotional", true);
            yield return new TestCaseData("LobbyApproach", "strategic", true);
            yield return new TestCaseData("LobbyApproach", "deal", true);
            yield return new TestCaseData("LobbyApproach", "pressure", true);
            yield return new TestCaseData("LobbyApproach", (string)null, false);
            yield return new TestCaseData("LobbyApproach", "__not_a_historical_value__", false);
            yield return new TestCaseData("LobbyApproach", "EMOTIONAL", false);
            yield return new TestCaseData("LobbyAsk", "spare", true);
            yield return new TestCaseData("LobbyAsk", "target", true);
            yield return new TestCaseData("LobbyAsk", "save", true);
            yield return new TestCaseData("LobbyAsk", "keep", true);
            yield return new TestCaseData("LobbyAsk", "vote", true);
            yield return new TestCaseData("LobbyAsk", (string)null, false);
            yield return new TestCaseData("LobbyAsk", "__not_a_historical_value__", false);
            yield return new TestCaseData("LobbyAsk", "SPARE", false);
            yield return new TestCaseData("LobbyResponse", "receptive", true);
            yield return new TestCaseData("LobbyResponse", "open", true);
            yield return new TestCaseData("LobbyResponse", "skeptical", true);
            yield return new TestCaseData("LobbyResponse", "hostile", true);
            yield return new TestCaseData("LobbyResponse", (string)null, false);
            yield return new TestCaseData("LobbyResponse", "__not_a_historical_value__", false);
            yield return new TestCaseData("LobbyResponse", "RECEPTIVE", false);
            yield return new TestCaseData("LoreSources", "authored", true);
            yield return new TestCaseData("LoreSources", "legacy", true);
            yield return new TestCaseData("LoreSources", "derived", true);
            yield return new TestCaseData("LoreSources", (string)null, false);
            yield return new TestCaseData("LoreSources", "__not_a_historical_value__", false);
            yield return new TestCaseData("LoreSources", "AUTHORED", false);
            yield return new TestCaseData("NpcActKinds", "talk", true);
            yield return new TestCaseData("NpcActKinds", "build", true);
            yield return new TestCaseData("NpcActKinds", "hold", true);
            yield return new TestCaseData("NpcActKinds", "court", true);
            yield return new TestCaseData("NpcActKinds", "hunt", true);
            yield return new TestCaseData("NpcActKinds", "campaign", true);
            yield return new TestCaseData("NpcActKinds", "pact", true);
            yield return new TestCaseData("NpcActKinds", "promise", true);
            yield return new TestCaseData("NpcActKinds", "meet", true);
            yield return new TestCaseData("NpcActKinds", "rumour", true);
            yield return new TestCaseData("NpcActKinds", "confront", true);
            yield return new TestCaseData("NpcActKinds", "eavesdrop", true);
            yield return new TestCaseData("NpcActKinds", (string)null, false);
            yield return new TestCaseData("NpcActKinds", "__not_a_historical_value__", false);
            yield return new TestCaseData("NpcActKinds", "TALK", false);
            yield return new TestCaseData("OpportunityKinds", "play", true);
            yield return new TestCaseData("OpportunityKinds", "lobby", true);
            yield return new TestCaseData("OpportunityKinds", "plea", true);
            yield return new TestCaseData("OpportunityKinds", "deal", true);
            yield return new TestCaseData("OpportunityKinds", "alliance", true);
            yield return new TestCaseData("OpportunityKinds", "vote", true);
            yield return new TestCaseData("OpportunityKinds", "comp", true);
            yield return new TestCaseData("OpportunityKinds", "read", true);
            yield return new TestCaseData("OpportunityKinds", (string)null, false);
            yield return new TestCaseData("OpportunityKinds", "__not_a_historical_value__", false);
            yield return new TestCaseData("OpportunityKinds", "PLAY", false);
            yield return new TestCaseData("OpportunityOutcome", "won", true);
            yield return new TestCaseData("OpportunityOutcome", "part", true);
            yield return new TestCaseData("OpportunityOutcome", "lost", true);
            yield return new TestCaseData("OpportunityOutcome", "na", true);
            yield return new TestCaseData("OpportunityOutcome", (string)null, false);
            yield return new TestCaseData("OpportunityOutcome", "__not_a_historical_value__", false);
            yield return new TestCaseData("OpportunityOutcome", "WON", false);
            yield return new TestCaseData("OpportunityResponse", "taken", true);
            yield return new TestCaseData("OpportunityResponse", "declined", true);
            yield return new TestCaseData("OpportunityResponse", "ignored", true);
            yield return new TestCaseData("OpportunityResponse", "expired", true);
            yield return new TestCaseData("OpportunityResponse", (string)null, false);
            yield return new TestCaseData("OpportunityResponse", "__not_a_historical_value__", false);
            yield return new TestCaseData("OpportunityResponse", "TAKEN", false);
            yield return new TestCaseData("PactPlanStance", "open", true);
            yield return new TestCaseData("PactPlanStance", "agreed", true);
            yield return new TestCaseData("PactPlanStance", "countered", true);
            yield return new TestCaseData("PactPlanStance", "low", true);
            yield return new TestCaseData("PactPlanStance", "lapsed", true);
            yield return new TestCaseData("PactPlanStance", "void", true);
            yield return new TestCaseData("PactPlanStance", (string)null, false);
            yield return new TestCaseData("PactPlanStance", "__not_a_historical_value__", false);
            yield return new TestCaseData("PactPlanStance", "OPEN", false);
            yield return new TestCaseData("PayoffCurrency", "trust", true);
            yield return new TestCaseData("PayoffCurrency", "intel", true);
            yield return new TestCaseData("PayoffCurrency", "alliance", true);
            yield return new TestCaseData("PayoffCurrency", "power", true);
            yield return new TestCaseData("PayoffCurrency", (string)null, false);
            yield return new TestCaseData("PayoffCurrency", "__not_a_historical_value__", false);
            yield return new TestCaseData("PayoffCurrency", "TRUST", false);
            yield return new TestCaseData("Personality.Approach", "warm", true);
            yield return new TestCaseData("Personality.Approach", "calculated", true);
            yield return new TestCaseData("Personality.Approach", "bold", true);
            yield return new TestCaseData("Personality.Approach", "candid", true);
            yield return new TestCaseData("Personality.Approach", "playful", true);
            yield return new TestCaseData("Personality.Approach", "yield", true);
            yield return new TestCaseData("Personality.Approach", "hardball", true);
            yield return new TestCaseData("Personality.Approach", (string)null, false);
            yield return new TestCaseData("Personality.Approach", "__not_a_historical_value__", false);
            yield return new TestCaseData("Personality.Approach", "WARM", false);
            yield return new TestCaseData("ReplyCards", "confrontation", true);
            yield return new TestCaseData("ReplyCards", "gossip", true);
            yield return new TestCaseData("ReplyCards", "plea", true);
            yield return new TestCaseData("ReplyCards", "pitch", true);
            yield return new TestCaseData("ReplyCards", (string)null, false);
            yield return new TestCaseData("ReplyCards", "__not_a_historical_value__", false);
            yield return new TestCaseData("ReplyCards", "CONFRONTATION", false);
            yield return new TestCaseData("StoryAnchors", "hoh-crowned", true);
            yield return new TestCaseData("StoryAnchors", "noms-set", true);
            yield return new TestCaseData("StoryAnchors", "veto-won", true);
            yield return new TestCaseData("StoryAnchors", "block-set", true);
            yield return new TestCaseData("StoryAnchors", "eviction-eve", true);
            yield return new TestCaseData("StoryAnchors", "eviction-night", true);
            yield return new TestCaseData("StoryAnchors", "social-close", true);
            yield return new TestCaseData("StoryAnchors", "conversation", true);
            yield return new TestCaseData("StoryAnchors", (string)null, false);
            yield return new TestCaseData("StoryAnchors", "__not_a_historical_value__", false);
            yield return new TestCaseData("StoryAnchors", "HOH-CROWNED", false);
            yield return new TestCaseData("StoryEffects", "move", true);
            yield return new TestCaseData("StoryEffects", "view", true);
            yield return new TestCaseData("StoryEffects", "receipt", true);
            yield return new TestCaseData("StoryEffects", "grudge", true);
            yield return new TestCaseData("StoryEffects", "grudge-ease", true);
            yield return new TestCaseData("StoryEffects", "bond", true);
            yield return new TestCaseData("StoryEffects", "bond-end", true);
            yield return new TestCaseData("StoryEffects", "alliance", true);
            yield return new TestCaseData("StoryEffects", "alliance-end", true);
            yield return new TestCaseData("StoryEffects", "promise", true);
            yield return new TestCaseData("StoryEffects", "deal", true);
            yield return new TestCaseData("StoryEffects", "fact", true);
            yield return new TestCaseData("StoryEffects", "fact-spread", true);
            yield return new TestCaseData("StoryEffects", "reveal", true);
            yield return new TestCaseData("StoryEffects", "hook", true);
            yield return new TestCaseData("StoryEffects", "modifier", true);
            yield return new TestCaseData("StoryEffects", "stress", true);
            yield return new TestCaseData("StoryEffects", "strike", true);
            yield return new TestCaseData("StoryEffects", "have-not", true);
            yield return new TestCaseData("StoryEffects", "memory", true);
            yield return new TestCaseData("StoryEffects", "reckoning", true);
            yield return new TestCaseData("StoryEffects", "expel", true);
            yield return new TestCaseData("StoryEffects", "var", true);
            yield return new TestCaseData("StoryEffects", "backdoor", true);
            yield return new TestCaseData("StoryEffects", "told", true);
            yield return new TestCaseData("StoryEffects", "trust", true);
            yield return new TestCaseData("StoryEffects", "phase-bonus", true);
            yield return new TestCaseData("StoryEffects", "hook-spend", true);
            yield return new TestCaseData("StoryEffects", "snub", true);
            yield return new TestCaseData("StoryEffects", "alliance-leave", true);
            yield return new TestCaseData("StoryEffects", "push-back", true);
            yield return new TestCaseData("StoryEffects", (string)null, false);
            yield return new TestCaseData("StoryEffects", "__not_a_historical_value__", false);
            yield return new TestCaseData("StoryEffects", "MOVE", false);
            yield return new TestCaseData("StoryLanes", "personal", true);
            yield return new TestCaseData("StoryLanes", "conflict", true);
            yield return new TestCaseData("StoryLanes", "game", true);
            yield return new TestCaseData("StoryLanes", "production", true);
            yield return new TestCaseData("StoryLanes", "moment", true);
            yield return new TestCaseData("StoryLanes", "play", true);
            yield return new TestCaseData("StoryLanes", "thread", true);
            yield return new TestCaseData("StoryLanes", (string)null, false);
            yield return new TestCaseData("StoryLanes", "__not_a_historical_value__", false);
            yield return new TestCaseData("StoryLanes", "PERSONAL", false);
            yield return new TestCaseData("StorylineStatus", "active", true);
            yield return new TestCaseData("StorylineStatus", "completed", true);
            yield return new TestCaseData("StorylineStatus", "abandoned", true);
            yield return new TestCaseData("StorylineStatus", (string)null, false);
            yield return new TestCaseData("StorylineStatus", "__not_a_historical_value__", false);
            yield return new TestCaseData("StorylineStatus", "ACTIVE", false);
            yield return new TestCaseData("StoryResults", "plain", true);
            yield return new TestCaseData("StoryResults", "success", true);
            yield return new TestCaseData("StoryResults", "backfire", true);
            yield return new TestCaseData("StoryResults", "lapsed", true);
            yield return new TestCaseData("StoryResults", "npc", true);
            yield return new TestCaseData("StoryResults", (string)null, false);
            yield return new TestCaseData("StoryResults", "__not_a_historical_value__", false);
            yield return new TestCaseData("StoryResults", "PLAIN", false);
            yield return new TestCaseData("StorySurfaces", "scene", true);
            yield return new TestCaseData("StorySurfaces", "approach", true);
            yield return new TestCaseData("StorySurfaces", "conversation", true);
            yield return new TestCaseData("StorySurfaces", "summons", true);
            yield return new TestCaseData("StorySurfaces", "meeting", true);
            yield return new TestCaseData("StorySurfaces", "npc", true);
            yield return new TestCaseData("StorySurfaces", (string)null, false);
            yield return new TestCaseData("StorySurfaces", "__not_a_historical_value__", false);
            yield return new TestCaseData("StorySurfaces", "SCENE", false);
        }

        [TestCaseSource(nameof(Cases))]
        public void ExactHistoricalValueAndOrdinalRefusals(string owner, string value, bool expected)
        {
            Assert.That(Known(owner, value), Is.EqualTo(expected));
        }

        private static bool Known(string owner, string value)
        {
            switch (owner)
            {
                case "BondKinds": return BondKinds.IsKnown(value);
                case "BondStatus": return BondStatus.IsKnown(value);
                case "ClaimSource": return ClaimSource.IsKnown(value);
                case "ClaimStatus": return ClaimStatus.IsKnown(value);
                case "CompetitionEntry": return CompetitionEntry.IsKnown(value);
                case "DealKind": return DealKind.IsKnown(value);
                case "DealStatus": return DealStatus.IsKnown(value);
                case "DealTrust": return DealTrust.IsKnown(value);
                case "FactKinds": return FactKinds.IsKnown(value);
                case "FactVisibility": return FactVisibility.IsKnown(value);
                case "GrudgeCauses": return GrudgeCauses.IsKnown(value);
                case "HouseEventKind": return HouseEventKind.IsKnown(value);
                case "HouseEventRisk": return HouseEventRisk.IsKnown(value);
                case "LobbyApproach": return LobbyApproach.IsKnown(value);
                case "LobbyAsk": return LobbyAsk.IsKnown(value);
                case "LobbyResponse": return LobbyResponse.IsKnown(value);
                case "LoreSources": return LoreSources.IsKnown(value);
                case "NpcActKinds": return NpcActKinds.IsKnown(value);
                case "OpportunityKinds": return OpportunityKinds.IsKnown(value);
                case "OpportunityOutcome": return OpportunityOutcome.IsKnown(value);
                case "OpportunityResponse": return OpportunityResponse.IsKnown(value);
                case "PactPlanStance": return PactPlanStance.IsKnown(value);
                case "PayoffCurrency": return PayoffCurrency.IsKnown(value);
                case "Personality.Approach": return Personality.Approach.IsKnown(value);
                case "ReplyCards": return ReplyCards.IsKnown(value);
                case "StoryAnchors": return StoryAnchors.IsKnown(value);
                case "StoryEffects": return StoryEffects.IsKnown(value);
                case "StoryLanes": return StoryLanes.IsKnown(value);
                case "StorylineStatus": return StorylineStatus.IsKnown(value);
                case "StoryResults": return StoryResults.IsKnown(value);
                case "StorySurfaces": return StorySurfaces.IsKnown(value);
                default: throw new ArgumentException("Unknown finite test owner.");
            }
        }

        [TestCase("veto_use", "critical")]
        [TestCase("final_two", "critical")]
        [TestCase("target_agreement", "high")]
        [TestCase("safety_agreement", "high")]
        [TestCase("alliance_invite", "high")]
        [TestCase("final_three", "high")]
        [TestCase("information_sharing", "low")]
        [TestCase("vote_together", "medium")]
        [TestCase("vote_save", "medium")]
        [TestCase("vote_evict", "medium")]
        [TestCase("partnership", "medium")]
        [TestCase(null, "medium")]
        [TestCase("unknown", "medium")]
        public void CompleteDefaultTrustIncludingFallback(string kind, string expected) =>
            Assert.That(DealKind.DefaultTrust(kind), Is.EqualTo(expected));
    }

    [TestFixture]
    internal sealed class FrozenV28GeneralCoreTests
    {
        internal static EpisodeState BeforeCast() => new EpisodeState
        {
            schemaVersion = 28, week = 1, revision = 0, nextSequence = 1,
            sessionId = "fixed-28-test", phase = EpisodePhase.Social,
            unifiedCommitmentRulesVersion = 0, unifiedCommitments = new List<UnifiedCommitmentState>(),
            unifiedHearingRulesVersion = 0, unifiedHearingEvidence = new List<UnifiedHearingEvidenceState>(),
            unifiedHearingReceipts = new List<UnifiedHearingReceiptState>(),
            unifiedVoteReveals = new List<UnifiedVoteRevealState>(), competitionRulesVersion = 1,
            blocRulesStartWeek = 1, socialBudgetRulesStartWeek = 1, windowActions = new List<int> { 0, 0, 0, 0 },
            contestants = new List<ContestantState>()
        };

        [Test]
        public void NullWholeCoreHasOriginalRefusal()
        {
            Assert.That(EpisodeValidation.TryValidate(null, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Unsupported episode schema."));
        }

        [TestCase(0)] [TestCase(25)] [TestCase(27)] [TestCase(29)] [TestCase(int.MaxValue)]
        public void LiteralSchemaGuardPrecedesLegacyStorage(int schema)
        {
            var state = new EpisodeState { schemaVersion = schema };
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Unsupported episode schema."));
            Assert.That(state.unifiedCommitments, Is.Null);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(3)]
        public void UnsupportedOrMissingLegacyAuthorityIsNotRepaired(int mode)
        {
            var state = new EpisodeState { schemaVersion = 28, unifiedCommitmentRulesVersion = mode };
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Unified commitments are not enabled in this build."));
            Assert.That(state.unifiedCommitments, Is.Null);
        }

        [Test]
        public void PublicSafetyRequiresWordBeforeBroaderCore()
        {
            var state = new EpisodeState { schemaVersion = 28, unifiedCommitmentRulesVersion = 1 };
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Unified Safety requires active commitment and story knowledge rules."));
            Assert.That(state.story, Is.Null);
        }

        [Test]
        public void PublicVoteRequiresWordAndFirstWeekDealRule()
        {
            var state = BeforeCast();
            state.unifiedCommitmentRulesVersion = 2;
            state.commitmentRulesStartWeek = 1;
            state.story = new StoryWorldState { rulesStartWeek = 1, rulesVersion = 5 };
            state.dealRulesStartWeek = 2;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("The unified vote rules require the house's deal pass from the first week."));
            Assert.That(state.dealRulesStartWeek, Is.EqualTo(2));
        }

        [Test]
        public void ValidPrefixReachesExactCastGuardWithoutRepair()
        {
            var state = BeforeCast();
            var original = state.contestants;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("A house holds between 3 and 16 contestants."));
            Assert.That(state.contestants, Is.SameAs(original));
            Assert.That(state.contestants, Is.Empty);
        }

        [TestCase(-1)] [TestCase(5)] [TestCase(int.MaxValue)]
        public void WindowVectorBoundsRemainSourceChecks(int value)
        {
            var state = BeforeCast();
            state.windowActions = new List<int> { 0, 0, 0, 0 };
            if (value == 5) state.windowActions.Add(0); else state.windowActions[0] = value;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Invalid window action counts."));
            Assert.That(state.windowActions.Count, Is.EqualTo(value == 5 ? 5 : 4));
        }

        [TestCase(0)] [TestCase(5)] [TestCase(int.MaxValue)]
        public void UnsupportedCompetitionVersionRefusesBeforeCounters(int version)
        {
            var state = BeforeCast(); state.competitionRulesVersion = version;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Unsupported competition rules version."));
        }

        [TestCase(0)] [TestCase(101)]
        public void WeekBoundsRemainChecked(int week)
        {
            var state = BeforeCast(); state.week = week;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Invalid session counters or phase."));
        }

        [Test]
        public void InputStateCollectionsAndOrderSurviveRefusalAndRepeat()
        {
            var state = BeforeCast();
            var counts = state.windowActions;
            var commitments = state.unifiedCommitments;
            Assert.That(EpisodeValidation.TryValidate(state, out var first), Is.False);
            Assert.That(EpisodeValidation.TryValidate(state, out var second), Is.False);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(state.windowActions, Is.SameAs(counts));
            Assert.That(state.unifiedCommitments, Is.SameAs(commitments));
            Assert.That(counts, Is.EqualTo(new[] {0, 0, 0, 0}));
        }
    }

    [TestFixture]
    internal sealed class FrozenV28LiteralBehaviorTests
    {
        private static CharacterAppearance Appearance() => new CharacterAppearance
        {
            version = 1, provider = "source", presetId = "", bodyId = "", fallbackId = "base",
            activeOutfit = "plain", dna = new List<AppearanceValue>(),
            colors = new List<AppearanceColor>(), outfits = new List<CharacterOutfit>()
        };

        [Test]
        public void CompleteEmptyAppearanceListsAreValidAndNotReplaced()
        {
            var value = Appearance(); var dna = value.dna;
            Assert.That(value.TryValidate(out _), Is.True);
            Assert.That(value.dna, Is.SameAs(dna));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        [TestCase(-0.01f)] [TestCase(1.01f)]
        public void AppearanceRejectsNonfiniteOrOutOfUnitValue(float number)
        {
            var value = Appearance(); value.dna.Add(new AppearanceValue { id = "x", value = number });
            Assert.That(value.TryValidate(out _), Is.False);
            Assert.That(value.dna.Count, Is.EqualTo(1));
        }

        [TestCase(0f)] [TestCase(1f)] [TestCase(0.5f)]
        public void AppearanceIncludesUnitEndpoints(float number)
        {
            var value = Appearance(); value.dna.Add(new AppearanceValue { id = "x", value = number });
            Assert.That(value.TryValidate(out _), Is.True);
        }

        [Test]
        public void AppearanceDuplicateIdsUseOrdinal()
        {
            var value = Appearance();
            value.dna.Add(new AppearanceValue { id = "x", value = 0.1f });
            value.dna.Add(new AppearanceValue { id = "X", value = 0.2f });
            Assert.That(value.TryValidate(out _), Is.True);
            value.dna[1].id = "x";
            Assert.That(value.TryValidate(out _), Is.False);
        }

        [Test]
        public void ReadOnlyRowsPreserveDeferredSourceOrder()
        {
            var state = new EpisodeState { contestants = new List<ContestantState> {
                new ContestantState { id = "second", status = ContestantStatus.Active },
                new ContestantState { id = "out", status = ContestantStatus.Evicted },
                new ContestantState { id = "first", status = ContestantStatus.Active }
            }};
            var view = LiteralRows.Active(state);
            state.contestants.Add(new ContestantState { id = "later", status = ContestantStatus.Active });
            Assert.That(view.Select(c => c.id), Is.EqualTo(new[] { "second", "first", "later" }));
            Assert.That(state.Find("first"), Is.SameAs(state.contestants[2]));
        }

        [Test]
        public void RevealClonePreservesNullsAndDetachesEachPresentBallot()
        {
            var row = new UnifiedVoteRevealState { week = 3, ballots = new List<UnifiedVoteBallotState> {
                new UnifiedVoteBallotState { voterId = "a", targetId = "b" }, null
            }};
            var clone = row.Clone();
            Assert.That(clone, Is.Not.SameAs(row));
            Assert.That(clone.ballots, Is.Not.SameAs(row.ballots));
            Assert.That(clone.ballots[0], Is.Not.SameAs(row.ballots[0]));
            Assert.That(clone.ballots[1], Is.Null);
            clone.ballots[0].targetId = "c";
            Assert.That(row.ballots[0].targetId, Is.EqualTo("b"));
            row.ballots = null;
            Assert.That(row.Clone().ballots, Is.Null);
        }

        [Test]
        public void FreshFallbackLedgersAreIndependentAndNeverAttachToState()
        {
            var a = LiteralRows.NewSeasonLedger(); var b = LiteralRows.NewSeasonLedger();
            a.power.Add(new PowerRow());
            Assert.That(b.power, Is.Empty);
            var state = new EpisodeState { playerId = "p", ledger = null };
            Assert.That(FinalArgument.Resolves(state, "hoh:1"), Is.False);
            Assert.That(FinalArgument.Resolves(state, "record:unnominated"), Is.True);
            Assert.That(state.ledger, Is.Null);
        }

        [Test]
        public void LocalKnownAndCertainPropertiesPreserveOriginalTruthTable()
        {
            var row = new KnownBallots.Ballot { basis = KnownBallots.Basis.Own, verdict = ClaimStatus.Kept };
            Assert.That(row.Known, Is.False); Assert.That(row.Certain, Is.False);
            row.targetId = "target";
            Assert.That(row.Known, Is.True); Assert.That(row.Certain, Is.True);
            row.basis = KnownBallots.Basis.Unknown;
            Assert.That(row.Certain, Is.False);
            row.basis = KnownBallots.Basis.Own; row.verdict = ClaimStatus.Open;
            Assert.That(row.Certain, Is.False);
        }

        [TestCase(EpisodePhase.Social, true)] [TestCase(EpisodePhase.Campaign, true)]
        [TestCase(EpisodePhase.HoH, false)] [TestCase(EpisodePhase.Eviction, false)]
        [TestCase(EpisodePhase.Finished, false)] [TestCase((EpisodePhase)999, false)]
        public void NpcEligiblePhasesAreLiteral(EpisodePhase phase, bool expected) =>
            Assert.That(NpcSocialRules.IsEligiblePhase(phase), Is.EqualTo(expected));

        [Test]
        public void ProductionReadSpecializationDoesNotCreateConductRows()
        {
            var state = new EpisodeState { week = 2, phase = EpisodePhase.HoH,
                story = new StoryWorldState { conduct = new List<ConductState>() }};
            Assert.That(Production.SitsOut(state, "p"), Is.False);
            Assert.That(state.story.conduct, Is.Empty);
            state.story.conduct.Add(new ConductState { contestantId = "p", sitsOutWeek = 2 });
            Assert.That(Production.SitsOut(state, "p"), Is.True);
            Assert.That(state.story.conduct.Count, Is.EqualTo(1));
        }

        [TestCase("", 2166136261u)] [TestCase("a", 3826002220u)] [TestCase("abc", 440920331u)]
        public void SeedHashIsUncheckedUtf16(string value, uint expected) =>
            Assert.That(SeededRandom.HashSeed(value), Is.EqualTo(expected));

        [Test]
        public void SeedHashRetainsItsNullException() =>
            Assert.Throws<ArgumentNullException>(() => SeededRandom.HashSeed(null));
    }

    [TestFixture]
    internal sealed class FrozenV28PrivacyReferenceTests
    {
        [Test]
        public void FinalArgumentReadsOnlyMatchingRows()
        {
            var state = new EpisodeState { playerId = "p", ledger = LiteralRows.NewSeasonLedger() };
            state.ledger.power.Add(new PowerRow { week = 2, hohId = "p", vetoHolderId = "other" });
            Assert.That(FinalArgument.Resolves(state, "hoh:2"), Is.True);
            Assert.That(FinalArgument.Resolves(state, "hoh:1"), Is.False);
            Assert.That(FinalArgument.Resolves(state, "veto:2"), Is.False);
            Assert.That(FinalArgument.Resolves(state, "block:2"), Is.True);
            Assert.That(FinalArgument.Resolves(state, "hoh:+2"), Is.False);
            Assert.That(state.ledger.power.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownReferenceDoesNotBecomeEvidence()
        {
            var state = new EpisodeState { playerId = "p", ledger = null };
            Assert.That(FinalArgument.Resolves(state, null), Is.False);
            Assert.That(FinalArgument.Resolves(state, ""), Is.False);
            Assert.That(FinalArgument.Resolves(state, "unknown:1"), Is.False);
            Assert.That(FinalArgument.Resolves(state, new string('x', 201)), Is.False);
            Assert.That(state.ledger, Is.Null);
        }

        [Test]
        public void LocalHelperDefaultsAreExplicit()
        {
            Assert.That(new KnownBallots.Ballot().basis, Is.EqualTo(KnownBallots.Basis.Unknown));
            Assert.That(new KnownBallots.Frame().nominees, Is.Empty);
            Assert.That(new KnownBallots.Sheet().ballots, Is.Empty);
            Assert.That(new ReplyCards.Reply().Risk, Is.EqualTo(HouseEventRisk.Low));
            Assert.That(new FinaleQuestions.Receipt().id, Is.Null);
        }
    }
}
