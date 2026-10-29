// Frozen29 (SCH30): the schema-29 whole reader, carried from FrozenV28Shape.cs and then by exactly the live
// validators' changes from 515246bf to b208d46d (ALL-STAR-HISTORIES-PLAN 8.3). Provenance comments below
// describe the schema-28 image this copy started from.
// Literal schema29 shape component, not a whole-save validator.
// Expected fields come from retained source declarations, never reflection or current DTOs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence.Frozen29Components
{
    internal static class FrozenV29Shape
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Shapes =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            { "AllianceRow", Fields("id:string", "why:string", "startedWeek:int", "endedWeek:int") },
            { "AllianceState", Fields("id:string", "name:string", "members:List<string>", "active:bool", "playerJoined:bool") },
            { "AppearanceColor", Fields("id:string", "r:float", "g:float", "b:float", "a:float") },
            { "AppearanceValue", Fields("id:string", "value:float") },
            { "AppearanceWardrobe", Fields("slot:string", "itemId:string") },
            { "ArcHistory", Fields("week:int", "delta:double", "reason:string") },
            { "BallotRow", Fields("week:int", "voterId:string", "targetId:string", "readBefore:string", "correct:bool") },
            { "BlocCallRow", Fields("week:int", "allianceId:string", "callerId:string", "targetId:string", "followed:List<string>", "defected:List<string>") },
            { "BondState", Fields("id:string", "kind:string", "aId:string", "bId:string", "status:string", "cycleId:string", "sinceWeek:int", "endedWeek:int") },
            { "CharacterAppearance", Fields("version:int", "provider:string", "presetId:string", "bodyId:string", "fallbackId:string", "activeOutfit:string", "dna:List<AppearanceValue>", "colors:List<AppearanceColor>", "outfits:List<CharacterOutfit>") },
            { "CharacterOutfit", Fields("id:string", "wardrobe:List<AppearanceWardrobe>", "colors:List<AppearanceColor>") },
            { "ClaimRow", Fields("week:int", "voterId:string", "targetId:string", "source:string", "status:string") },
            { "CompetitionRow", Fields("week:int", "field:int", "placement:int", "kind:string", "entry:string", "performance:double", "expectedWin:double") },
            { "CompetitionScore", Fields("contestantId:string", "score:double") },
            { "ConditionalVetoResolutionState", Fields("reason:string", "departedId:string", "departedStatus:string", "revision:int", "sequence:int", "vetoOccasionWeek:int") },
            { "ConditionalVetoTermsState", Fields("statementId:string", "consentEventId:string", "consentPathIndex:int", "consentRevision:int", "resolution:ConditionalVetoResolutionState") },
            { "ConductState", Fields("contestantId:string", "strikes:int", "lastStrikeWeek:int", "cleanWeeks:int", "sitsOutWeek:int", "pushedBack:int", "reasons:List<string>") },
            { "ContactState", Fields("npcId:string", "rapport:int", "weekCount:int", "lastWeek:int") },
            { "ContestantState", Fields("id:string", "name:string", "pronouns:string", "motive:string", "homeRoom:string", "occupation:string", "archetype:string", "hometown:string", "bio:string", "sourceTemplateId:string", "appearance:CharacterAppearance", "age:int", "mood:string", "stressLevel:string", "isPlayer:bool", "status:ContestantStatus", "stats:ContestantStats", "traits:List<string>", "hohWins:int", "vetoWins:int", "timesNominated:int", "nominationWeeks:List<int>") },
            { "ContestantStats", Fields("physical:double", "mental:double", "endurance:double", "social:double", "luck:double", "competition:double", "strategic:double", "loyalty:double") },
            { "DealState", Fields("id:string", "type:string", "proposerId:string", "recipientId:string", "targetId:string", "status:string", "week:int", "expiresWeek:int", "trustImpact:string", "brokenById:string", "settledWeek:int", "linkedDealId:string") },
            { "DialogueEntryReceiptState", Fields("kind:string", "grantId:string", "revision:int", "windowActionBefore:int", "windowActionAfter:int") },
            { "DialogueEpisodeState", Fields("id:string", "npcId:string", "occasionKind:string", "occasionId:string", "entryEventId:string", "contentVersion:int", "week:int", "window:int", "startedRevision:int", "startedSequence:int", "startedPhase:EpisodePhase", "entry:DialogueEntryReceiptState", "responses:List<DialogueResponseState>", "evidence:List<DialogueEvidenceState>", "statement:DialogueStatementState", "time:DialogueTimeReceiptState") },
            { "DialogueEvidenceState", Fields("id:string", "kind:string", "eventId:string", "pathIndex:int", "capturedRevision:int", "power:PowerRow", "memory:MemoryState", "statementId:string") },
            { "DialogueResponseState", Fields("eventId:string", "speakerId:string", "responseKey:string", "pathIndex:int", "revision:int") },
            { "DialogueRoleGrantState", Fields("id:string", "ownerId:string", "role:string", "sourceKind:string", "status:string", "consumedEpisodeId:string", "expiryReason:string", "week:int", "sourceRevision:int", "sourceSequence:int", "consumedRevision:int", "expiredRevision:int", "sourcePhase:EpisodePhase", "sourceNominees:List<string>") },
            { "DialogueState", Fields("episodes:List<DialogueEpisodeState>", "grants:List<DialogueRoleGrantState>", "vetoOccasions:List<DialogueVetoOccasionState>") },
            { "DialogueStatementState", Fields("id:string", "eventId:string", "speakerId:string", "targetId:string", "answer:string", "conditionKind:string", "pathIndex:int", "week:int", "revision:int", "sequence:int", "audienceIds:List<string>") },
            { "DialogueTimeReceiptState", Fields("revision:int", "sequence:int", "window:int", "proposedDelta:int", "appliedDelta:int", "reason:string", "spentRevision:int", "spentSequence:int", "spentWindowAction:int") },
            { "DialogueVetoDecisionState", Fields("revision:int", "sequence:int", "activeIds:List<string>", "nominees:List<string>", "replacementCandidates:List<string>", "used:bool", "savedId:string", "replacementId:string") },
            { "DialogueVetoOccasionState", Fields("week:int", "resultRevision:int", "resultSequence:int", "hohId:string", "holderId:string", "participants:List<string>", "nomineesAtResult:List<string>", "decision:DialogueVetoDecisionState") },
            { "DiaryPromptState", Fields("id:string", "trigger:string", "evictedId:string", "week:int", "isNominee:bool") },
            { "EpisodeEvent", Fields("sequence:int", "week:int", "phase:EpisodePhase", "kind:string", "text:string", "audienceIds:List<string>") },
            { "EpisodeState", Fields("schemaVersion:int", "competitionRulesVersion:int", "sessionId:string", "seed:uint", "randomState:uint", "revision:int", "week:int", "nextSequence:int", "socialActions:int", "phase:EpisodePhase", "playerId:string", "hohId:string", "previousHohId:string", "vetoHolderId:string", "winnerId:string", "runnerUpId:string", "finalPart1WinnerId:string", "finalPart2WinnerId:string", "competitionResolved:bool", "vetoResolved:bool", "evictionResolved:bool", "contestants:List<ContestantState>", "relationships:List<RelationshipState>", "promises:List<PromiseState>", "alliances:List<AllianceState>", "memories:List<MemoryState>", "nominees:List<string>", "vetoPlayers:List<string>", "votes:List<VoteState>", "ledger:SeasonLedger", "readRulesStartWeek:int", "leverRulesStartWeek:int", "weekRulesStartWeek:int", "windowActions:List<int>", "economyRulesVersion:int", "moveInExtrasSpent:int", "agencyRulesStartWeek:int", "finaleRulesStartWeek:int", "commitmentRulesStartWeek:int", "unifiedCommitmentRulesVersion:int", "unifiedCommitments:List<UnifiedCommitmentState>", "unifiedHearingRulesVersion:int", "unifiedHearingEvidence:List<UnifiedHearingEvidenceState>", "unifiedHearingReceipts:List<UnifiedHearingReceiptState>", "unifiedVoteReveals:List<UnifiedVoteRevealState>", "competitionScores:List<CompetitionScore>", "events:List<EpisodeEvent>", "acceptedCommandIds:List<string>", "juryExchanges:List<JuryExchangeState>", "juryQuestionIndex:int", "finalSpeeches:List<FinalSpeechState>", "finalArgument:FinalArgumentState", "relationshipArcs:List<RelationshipArcState>", "playerPersona:WebPersonaState", "jurySentiment:WebJurySentimentState", "pendingDiary:DiaryPromptState", "lastDiaryRoomWeek:int", "phaseEventSocialBonus:int", "phaseEventCompBonus:int", "resolvedDiaryIds:List<string>", "loyaltyOaths:List<WebOathRecord>", "oathOpportunities:List<string>", "shownOathMilestones:List<string>", "playerStudyBonus:int", "blocRulesStartWeek:int", "npcSocial:NpcSocialState", "evictionStage:EvictionStage", "evictionSpeeches:List<EvictionSpeechState>", "backdoorTargetId:string", "outOfPhaseSocialActions:int", "openingBeatsSeen:List<string>", "socialBudgetRulesStartWeek:int", "deals:List<DealState>", "dealRulesStartWeek:int", "boughtActionPoints:int", "houseEvents:List<HouseEventState>", "eventRulesStartWeek:int", "storylines:List<StorylineState>", "activeModifiers:List<StoryModifierState>", "storyRulesStartWeek:int", "haveNotRulesStartWeek:int", "haveNots:List<string>", "haveNotPasses:List<string>", "punishedHaveNots:List<string>", "vetoPrizes:List<VetoPrizeState>", "strategyRulesStartWeek:int", "lobbies:List<LobbyState>", "replyCards:List<ReplyCardState>", "story:StoryWorldState", "allianceLeakRulesStartWeek:int", "pactPlanRulesStartWeek:int", "allWeekRulesStartWeek:int", "dialogueRulesVersion:int", "dialogue:DialogueState") },
            { "EvictionSpeechState", Fields("speakerId:string", "text:string", "week:int", "isPlayerAuthored:bool") },
            { "FinalArgumentState", Fields("theme:string", "momentRefs:List<string>") },
            { "FinalSpeechState", Fields("speakerId:string", "text:string", "isPlayerAuthored:bool") },
            { "GrudgeState", Fields("holderId:string", "targetId:string", "cause:string", "severity:double", "originWeek:int", "count:int") },
            { "HookState", Fields("id:string", "holderId:string", "overId:string", "factId:string", "week:int", "spent:bool") },
            { "HouseEventChoice", Fields("label:string", "description:string", "risk:string", "impacts:List<HouseEventImpact>", "trustChange:double", "optionId:string", "glyph:string", "approach:string", "checkBase:double", "subjectId:string", "lapse:bool", "costsAction:bool", "conduct:bool", "pickPerson:bool", "eligibleIds:List<string>", "locked:bool", "lockReason:string", "bonusTraits:List<string>", "against:List<string>", "effects:List<StoryEffectState>", "backfire:List<StoryEffectState>", "next:string", "nextOnBackfire:string") },
            { "HouseEventImpact", Fields("targetId:string", "amount:double") },
            { "HouseEventState", Fields("id:string", "kind:string", "title:string", "narrative:string", "involvedIds:List<string>", "choices:List<HouseEventChoice>", "week:int", "resolved:bool", "chosenIndex:int", "outcome:string", "contentId:string", "cycleId:string", "closesAnchor:string", "surface:string", "venue:string", "lapseOptionId:string", "cast:List<StoryRoleState>") },
            { "HouseFactState", Fields("id:string", "kind:string", "actorId:string", "subjectId:string", "refId:string", "visibility:string", "week:int", "knowers:List<string>") },
            { "JuryExchangeState", Fields("questionerId:string", "finalistId:string", "tone:string", "question:string", "optionA:string", "optionB:string", "correctChoice:string", "answerChoice:string", "answer:string", "opponentAnswer:string", "category:string", "receiptKind:string", "receiptId:string", "completed:bool") },
            { "LobbyState", Fields("week:int", "phase:EpisodePhase", "deciderId:string", "ask:string", "subjectId:string", "approach:string", "response:string", "influence:double") },
            { "LoreCastState", Fields("contestantId:string", "sheetKey:string", "source:string", "factIds:List<string>") },
            { "MemoryState", Fields("ownerId:string", "subjectId:string", "text:string", "week:int", "isPrivate:bool") },
            { "NpcActState", Fields("id:string", "kind:string", "actorId:string", "partnerId:string", "subjectId:string", "room:string", "week:int", "window:int", "firedTick:int", "sighted:bool", "overheard:bool") },
            { "NpcConversationState", Fields("sequence:long", "startedTick:long", "firstId:string", "secondId:string", "topic:string", "rendezvousId:string", "week:int", "phase:EpisodePhase", "durationMs:double") },
            { "NpcCooldownState", Fields("npcId:string", "untilTick:long") },
            { "NpcPairMemoryState", Fields("fromId:string", "toId:string", "lastTopic:string", "count:int", "lastStartTick:long") },
            { "NpcSocialState", Fields("rulesStartWeek:int", "clockTick:long", "nextScanTick:long", "nextConversationSequence:long", "randomState:uint", "pending:List<NpcConversationState>", "cooldowns:List<NpcCooldownState>", "pairMemory:List<NpcPairMemoryState>", "beatWeek:int", "beatWindow:int", "beatsFired:int", "beatSeats:int", "beatPlan:List<string>", "acts:List<NpcActState>") },
            { "OpportunityRow", Fields("id:string", "kind:string", "anchor:string", "source:string", "response:string", "outcome:string", "currency:string", "note:string", "week:int", "payoff:double", "steps:List<OpportunityStep>") },
            { "OpportunityStep", Fields("at:string", "choice:string", "matchedRead:bool") },
            { "PactPlanRow", Fields("week:int", "allianceId:string", "throughId:string", "stance:string", "present:List<string>", "cameRound:List<string>", "followed:List<string>", "says:List<PlanSay>", "counterId:string", "targetId:string", "callerId:string") },
            { "PlanSay", Fields("memberId:string", "targetId:string") },
            { "PowerRow", Fields("week:int", "hohId:string", "vetoHolderId:string", "savedId:string", "replacementId:string", "evicteeId:string", "backdoorTargetId:string", "backdoorResult:string", "vetoUsed:bool", "nominees:List<string>", "tally:List<int>") },
            { "PromiseState", Fields("id:string", "fromId:string", "toId:string", "targetId:string", "kind:PromiseKind", "status:PromiseStatus", "week:int", "expiresWeek:int", "impact:string", "brokenById:string", "settledWeek:int") },
            { "ReckoningState", Fields("npcId:string", "cause:string", "betrayerIsPlayer:bool", "week:int") },
            { "RelationshipArcState", Fields("npcId:string", "npcName:string", "arcType:string", "intensity:double", "escalationLevel:int", "weeklyHistory:List<ArcHistory>") },
            { "RelationshipEventState", Fields("sequence:int", "week:int", "type:string", "description:string", "impactScore:double", "decayable:bool") },
            { "RelationshipState", Fields("fromId:string", "toId:string", "score:double", "lastInteractionWeek:int", "notes:List<string>", "events:List<RelationshipEventState>") },
            { "RemovalState", Fields("contestantId:string", "reasonId:string", "week:int") },
            { "ReplyCardState", Fields("id:string", "week:int", "kind:string", "fromId:string", "aboutId:string") },
            { "ReplyRow", Fields("week:int", "cardId:string", "kind:string", "fromId:string", "listenerId:string", "replyKey:string", "promised:bool", "toThem:double") },
            { "SeasonLedger", Fields("opportunities:List<OpportunityRow>", "competitions:List<CompetitionRow>", "power:List<PowerRow>", "ballots:List<BallotRow>", "claims:List<ClaimRow>", "alliances:List<AllianceRow>", "standings:List<StandingRow>", "replies:List<ReplyRow>", "calls:List<BlocCallRow>", "dropped:int", "plans:List<PactPlanRow>") },
            { "StandingRow", Fields("week:int", "fromId:string", "toId:string", "source:string", "score:double") },
            { "StoryCooldownState", Fields("key:string", "untilWeek:int") },
            { "StoryEffectState", Fields("kind:string", "fromId:string", "toId:string", "thirdId:string", "type:string", "text:string", "amount:double", "weeks:int") },
            { "StoryModifierState", Fields("id:string", "name:string", "description:string", "weeksLeft:int", "competitionBonus:double", "socialBonus:double", "ownerId:string") },
            { "StoryRoleState", Fields("role:string", "contestantId:string") },
            { "StoryStepState", Fields("beatId:string", "optionId:string", "result:string", "week:int", "logSequence:int") },
            { "StoryVarState", Fields("key:string", "value:int") },
            { "StoryWorldState", Fields("rulesStartWeek:int", "rulesVersion:int", "productionStrictness:int", "romanceStorylines:bool", "grudges:List<GrudgeState>", "facts:List<HouseFactState>", "bonds:List<BondState>", "hooks:List<HookState>", "contacts:List<ContactState>", "lore:List<LoreCastState>", "knownFacts:List<string>", "conduct:List<ConductState>", "removals:List<RemovalState>", "pendingRemovalId:string", "cooldowns:List<StoryCooldownState>", "reckonings:List<ReckoningState>") },
            { "StorylineState", Fields("id:string", "templateId:string", "category:string", "title:string", "eventId:string", "status:string", "week:int", "endedWeek:int", "beatId:string", "lane:string", "variant:int", "cast:List<StoryRoleState>", "path:List<StoryStepState>", "vars:List<StoryVarState>", "nextWeek:int", "nextAnchor:string", "endingId:string") },
            { "UnifiedCommitmentState", Fields("id:string", "kind:string", "sourcePolicy:string", "origin:string", "makerId:string", "beneficiaryId:string", "reciprocal:bool", "createdWeek:int", "expiresWeek:int", "status:string", "settledWeek:int", "brokenById:string", "trustImpact:string", "linkedCommitmentId:string", "settlementEffectKey:string", "targetId:string", "subtype:string", "voteBindingWeek:int", "voteFirstRevealWeek:int", "conditionalVeto:ConditionalVetoTermsState") },
            { "UnifiedHearingEvidenceState", Fields("incidentKey:string", "fact:HouseFactState") },
            { "UnifiedHearingReceiptState", Fields("incidentKey:string", "listenerId:string", "factId:string", "kind:string", "heardWeek:int") },
            { "UnifiedVoteBallotState", Fields("voterId:string", "targetId:string") },
            { "UnifiedVoteRevealState", Fields("week:int", "ballots:List<UnifiedVoteBallotState>") },
            { "VetoPrizeState", Fields("week:int", "contestantId:string", "prizeId:string") },
            { "VoteState", Fields("voterId:string", "targetId:string", "reason:string") },
            { "WebJurorSentiment", Fields("jurorId:string", "jurorName:string", "sentiment:double", "events:List<WebJurySentimentEvent>") },
            { "WebJurySentimentEvent", Fields("week:int", "delta:double", "reason:string") },
            { "WebJurySentimentState", Fields("jurors:List<WebJurorSentiment>", "overallSentiment:double") },
            { "WebOathRecord", Fields("playerId:string", "targetId:string", "week:int", "timestamp:long") },
            { "WebPersonaHistory", Fields("persona:string", "week:int") },
            { "WebPersonaScore", Fields("persona:string", "score:int") },
            { "WebPersonaState", Fields("current:string", "scores:List<WebPersonaScore>", "history:List<WebPersonaHistory>") },
        };

        private static Dictionary<string, string> Fields(params string[] definitions) =>
            definitions.Select(definition => definition.Split(':'))
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        internal static void CheckComponent(JToken original, string typeName)
        {
            if (!KnownType(typeName)) throw Invalid("component", "unknown fixed29 type");
            Check(original, typeName, "component(" + typeName + ")", 0);
        }

        private static bool KnownType(string name)
        {
            if (name == null) return false;
            if (IsValueType(name) || name == "string" || Shapes.ContainsKey(name)) return true;
            if (!name.StartsWith("List<", StringComparison.Ordinal) || !name.EndsWith(">", StringComparison.Ordinal))
                return false;
            var element = name.Substring(5, name.Length - 6);
            return IsValueType(element) || element == "string" || Shapes.ContainsKey(element);
        }

        private static bool IsEnum(string name) =>
            name == "ContestantStatus" || name == "EpisodePhase" || name == "EvictionStage" || name == "PromiseKind" || name == "PromiseStatus";

        private static bool IsValueType(string name) =>
            name == "bool" || name == "int" || name == "uint" || name == "long"
            || name == "float" || name == "double" || IsEnum(name);

        private static void Check(JToken token, string type, string path, int depth)
        {
            if (token == null || depth > 64) throw Invalid(path, "missing or too deeply nested");
            if (token.Type == JTokenType.Null)
            {
                if (IsValueType(type)) throw Invalid(path, "cannot be null");
                return;
            }
            if (type == "string")
            {
                if (token.Type != JTokenType.String) throw Invalid(path, "must be text");
                return;
            }
            if (type == "bool")
            {
                if (token.Type != JTokenType.Boolean) throw Invalid(path, "must be boolean");
                return;
            }
            if (type == "float" || type == "double")
            {
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                    throw Invalid(path, "must be numeric");
                double number;
                try { number = (double)token; }
                catch (Exception error) when (error is OverflowException || error is FormatException
                    || error is InvalidCastException || error is ArgumentException)
                { throw Invalid(path, "cannot represent its numeric token"); }
                if (double.IsNaN(number) || double.IsInfinity(number)) throw Invalid(path, "must be finite");
                return;
            }
            // Historical CheckDtoShape gates integer tokens, not enum membership or CLR widths.
            // Those conversions belong to local hydration; full enum/range semantics remain unimplemented.
            if (type == "int" || type == "uint" || type == "long" || IsEnum(type))
            {
                if (token.Type != JTokenType.Integer) throw Invalid(path, "must be an integer");
                return;
            }
            if (type.StartsWith("List<", StringComparison.Ordinal))
            {
                if (!(token is JArray array)) throw Invalid(path, "must be an array");
                string element = type.Substring(5, type.Length - 6);
                foreach (var item in array) Check(item, element, path + "[]", depth + 1);
                return;
            }
            if (!(token is JObject obj) || !Shapes.TryGetValue(type, out var fields))
                throw Invalid(path, "must be a fixed29 object");
            if (obj.Properties().Count() != fields.Count || fields.Keys.Any(name => obj.Property(name, StringComparison.Ordinal) == null))
                throw Invalid(path, "contains missing or unknown schema fields");
            foreach (var field in fields) Check(obj[field.Key], field.Value, path + "." + field.Key, depth + 1);
        }

        private static InvalidDataException Invalid(string path, string reason) =>
            new InvalidDataException(path + " " + reason + ".");
    }
}
