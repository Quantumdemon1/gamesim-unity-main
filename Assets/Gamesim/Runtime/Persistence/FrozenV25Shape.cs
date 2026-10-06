// Literal recursive schema25 shape at f4566b69afc10c1008d64e112c12ed39241caa36.
// Never derive this map from a current DTO or extend it when a future schema is added.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    internal static class FrozenV25Shape
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Shapes =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            { "EpisodeState", Fields("schemaVersion:int", "competitionRulesVersion:int", "sessionId:string", "seed:uint", "randomState:uint", "revision:int", "week:int", "nextSequence:int", "socialActions:int", "phase:EpisodePhase", "playerId:string", "hohId:string", "previousHohId:string", "vetoHolderId:string", "winnerId:string", "runnerUpId:string", "finalPart1WinnerId:string", "finalPart2WinnerId:string", "competitionResolved:bool", "vetoResolved:bool", "evictionResolved:bool", "contestants:List<ContestantState>", "relationships:List<RelationshipState>", "promises:List<PromiseState>", "alliances:List<AllianceState>", "memories:List<MemoryState>", "nominees:List<string>", "vetoPlayers:List<string>", "votes:List<VoteState>", "ledger:SeasonLedger", "readRulesStartWeek:int", "leverRulesStartWeek:int", "weekRulesStartWeek:int", "windowActions:List<int>", "economyRulesVersion:int", "moveInExtrasSpent:int", "agencyRulesStartWeek:int", "finaleRulesStartWeek:int", "commitmentRulesStartWeek:int", "unifiedCommitmentRulesVersion:int", "unifiedCommitments:List<UnifiedCommitmentState>", "unifiedHearingRulesVersion:int", "unifiedHearingEvidence:List<UnifiedHearingEvidenceState>", "unifiedHearingReceipts:List<UnifiedHearingReceiptState>", "competitionScores:List<CompetitionScore>", "events:List<EpisodeEvent>", "acceptedCommandIds:List<string>", "juryExchanges:List<JuryExchangeState>", "juryQuestionIndex:int", "finalSpeeches:List<FinalSpeechState>", "finalArgument:FinalArgumentState", "relationshipArcs:List<RelationshipArcState>", "playerPersona:WebPersonaState", "jurySentiment:WebJurySentimentState", "pendingDiary:DiaryPromptState", "lastDiaryRoomWeek:int", "phaseEventSocialBonus:int", "phaseEventCompBonus:int", "resolvedDiaryIds:List<string>", "loyaltyOaths:List<WebOathRecord>", "oathOpportunities:List<string>", "shownOathMilestones:List<string>", "playerStudyBonus:int", "blocRulesStartWeek:int", "npcSocial:NpcSocialState", "evictionStage:EvictionStage", "evictionSpeeches:List<EvictionSpeechState>", "backdoorTargetId:string", "outOfPhaseSocialActions:int", "openingBeatsSeen:List<string>", "socialBudgetRulesStartWeek:int", "deals:List<DealState>", "dealRulesStartWeek:int", "boughtActionPoints:int", "houseEvents:List<HouseEventState>", "eventRulesStartWeek:int", "storylines:List<StorylineState>", "activeModifiers:List<StoryModifierState>", "storyRulesStartWeek:int", "haveNotRulesStartWeek:int", "haveNots:List<string>", "haveNotPasses:List<string>", "punishedHaveNots:List<string>", "vetoPrizes:List<VetoPrizeState>", "strategyRulesStartWeek:int", "lobbies:List<LobbyState>", "replyCards:List<ReplyCardState>", "story:StoryWorldState") },
            { "ContestantState", Fields("id:string", "name:string", "pronouns:string", "motive:string", "homeRoom:string", "occupation:string", "archetype:string", "hometown:string", "bio:string", "sourceTemplateId:string", "appearance:CharacterAppearance", "age:int", "mood:string", "stressLevel:string", "isPlayer:bool", "status:ContestantStatus", "stats:ContestantStats", "traits:List<string>", "hohWins:int", "vetoWins:int", "timesNominated:int", "nominationWeeks:List<int>") },
            { "RelationshipState", Fields("fromId:string", "toId:string", "score:double", "lastInteractionWeek:int", "notes:List<string>", "events:List<RelationshipEventState>") },
            { "PromiseState", Fields("id:string", "fromId:string", "toId:string", "targetId:string", "kind:PromiseKind", "status:PromiseStatus", "week:int", "expiresWeek:int", "impact:string", "brokenById:string", "settledWeek:int") },
            { "AllianceState", Fields("id:string", "name:string", "members:List<string>", "active:bool", "playerJoined:bool") },
            { "MemoryState", Fields("ownerId:string", "subjectId:string", "text:string", "week:int", "isPrivate:bool") },
            { "VoteState", Fields("voterId:string", "targetId:string", "reason:string") },
            { "SeasonLedger", Fields("opportunities:List<OpportunityRow>", "competitions:List<CompetitionRow>", "power:List<PowerRow>", "ballots:List<BallotRow>", "claims:List<ClaimRow>", "alliances:List<AllianceRow>", "standings:List<StandingRow>", "replies:List<ReplyRow>", "calls:List<BlocCallRow>", "dropped:int") },
            { "UnifiedCommitmentState", Fields("id:string", "kind:string", "sourcePolicy:string", "origin:string", "makerId:string", "beneficiaryId:string", "reciprocal:bool", "createdWeek:int", "expiresWeek:int", "status:string", "settledWeek:int", "brokenById:string", "trustImpact:string", "linkedCommitmentId:string", "settlementEffectKey:string") },
            { "UnifiedHearingEvidenceState", Fields("incidentKey:string", "fact:HouseFactState") },
            { "UnifiedHearingReceiptState", Fields("incidentKey:string", "listenerId:string", "factId:string", "kind:string", "heardWeek:int") },
            { "CompetitionScore", Fields("contestantId:string", "score:double") },
            { "EpisodeEvent", Fields("sequence:int", "week:int", "phase:EpisodePhase", "kind:string", "text:string", "audienceIds:List<string>") },
            { "JuryExchangeState", Fields("questionerId:string", "finalistId:string", "tone:string", "question:string", "optionA:string", "optionB:string", "correctChoice:string", "answerChoice:string", "answer:string", "opponentAnswer:string", "category:string", "receiptKind:string", "receiptId:string", "completed:bool") },
            { "FinalSpeechState", Fields("speakerId:string", "text:string", "isPlayerAuthored:bool") },
            { "FinalArgumentState", Fields("theme:string", "momentRefs:List<string>") },
            { "RelationshipArcState", Fields("npcId:string", "npcName:string", "arcType:string", "intensity:double", "escalationLevel:int", "weeklyHistory:List<ArcHistory>") },
            { "WebPersonaState", Fields("current:string", "scores:List<WebPersonaScore>", "history:List<WebPersonaHistory>") },
            { "WebJurySentimentState", Fields("jurors:List<WebJurorSentiment>", "overallSentiment:double") },
            { "DiaryPromptState", Fields("id:string", "trigger:string", "evictedId:string", "week:int", "isNominee:bool") },
            { "WebOathRecord", Fields("playerId:string", "targetId:string", "week:int", "timestamp:long") },
            { "NpcSocialState", Fields("rulesStartWeek:int", "clockTick:long", "nextScanTick:long", "nextConversationSequence:long", "randomState:uint", "pending:List<NpcConversationState>", "cooldowns:List<NpcCooldownState>", "pairMemory:List<NpcPairMemoryState>") },
            { "EvictionSpeechState", Fields("speakerId:string", "text:string", "week:int", "isPlayerAuthored:bool") },
            { "DealState", Fields("id:string", "type:string", "proposerId:string", "recipientId:string", "targetId:string", "status:string", "week:int", "expiresWeek:int", "trustImpact:string", "brokenById:string", "settledWeek:int", "linkedDealId:string") },
            { "HouseEventState", Fields("id:string", "kind:string", "title:string", "narrative:string", "involvedIds:List<string>", "choices:List<HouseEventChoice>", "week:int", "resolved:bool", "chosenIndex:int", "outcome:string", "contentId:string", "cycleId:string", "closesAnchor:string", "surface:string", "venue:string", "lapseOptionId:string", "cast:List<StoryRoleState>") },
            { "StorylineState", Fields("id:string", "templateId:string", "category:string", "title:string", "eventId:string", "status:string", "week:int", "endedWeek:int", "beatId:string", "lane:string", "variant:int", "cast:List<StoryRoleState>", "path:List<StoryStepState>", "vars:List<StoryVarState>", "nextWeek:int", "nextAnchor:string", "endingId:string") },
            { "StoryModifierState", Fields("id:string", "name:string", "description:string", "weeksLeft:int", "competitionBonus:double", "socialBonus:double", "ownerId:string") },
            { "VetoPrizeState", Fields("week:int", "contestantId:string", "prizeId:string") },
            { "LobbyState", Fields("week:int", "phase:EpisodePhase", "deciderId:string", "ask:string", "subjectId:string", "approach:string", "response:string", "influence:double") },
            { "ReplyCardState", Fields("id:string", "week:int", "kind:string", "fromId:string", "aboutId:string") },
            { "StoryWorldState", Fields("rulesStartWeek:int", "rulesVersion:int", "productionStrictness:int", "romanceStorylines:bool", "grudges:List<GrudgeState>", "facts:List<HouseFactState>", "bonds:List<BondState>", "hooks:List<HookState>", "contacts:List<ContactState>", "lore:List<LoreCastState>", "knownFacts:List<string>", "conduct:List<ConductState>", "removals:List<RemovalState>", "pendingRemovalId:string", "cooldowns:List<StoryCooldownState>", "reckonings:List<ReckoningState>") },
            { "CharacterAppearance", Fields("version:int", "provider:string", "presetId:string", "bodyId:string", "fallbackId:string", "activeOutfit:string", "dna:List<AppearanceValue>", "colors:List<AppearanceColor>", "outfits:List<CharacterOutfit>") },
            { "ContestantStats", Fields("physical:double", "mental:double", "endurance:double", "social:double", "luck:double", "competition:double", "strategic:double", "loyalty:double") },
            { "RelationshipEventState", Fields("sequence:int", "week:int", "type:string", "description:string", "impactScore:double", "decayable:bool") },
            { "OpportunityRow", Fields("id:string", "kind:string", "anchor:string", "source:string", "response:string", "outcome:string", "currency:string", "note:string", "week:int", "payoff:double", "steps:List<OpportunityStep>") },
            { "CompetitionRow", Fields("week:int", "field:int", "placement:int", "kind:string", "entry:string", "performance:double", "expectedWin:double") },
            { "PowerRow", Fields("week:int", "hohId:string", "vetoHolderId:string", "savedId:string", "replacementId:string", "evicteeId:string", "backdoorTargetId:string", "backdoorResult:string", "vetoUsed:bool", "nominees:List<string>", "tally:List<int>") },
            { "BallotRow", Fields("week:int", "voterId:string", "targetId:string", "readBefore:string", "correct:bool") },
            { "ClaimRow", Fields("week:int", "voterId:string", "targetId:string", "source:string", "status:string") },
            { "AllianceRow", Fields("id:string", "why:string", "startedWeek:int", "endedWeek:int") },
            { "StandingRow", Fields("week:int", "fromId:string", "toId:string", "source:string", "score:double") },
            { "ReplyRow", Fields("week:int", "cardId:string", "kind:string", "fromId:string", "listenerId:string", "replyKey:string", "promised:bool", "toThem:double") },
            { "BlocCallRow", Fields("week:int", "allianceId:string", "callerId:string", "targetId:string", "followed:List<string>", "defected:List<string>") },
            { "HouseFactState", Fields("id:string", "kind:string", "actorId:string", "subjectId:string", "refId:string", "visibility:string", "week:int", "knowers:List<string>") },
            { "ArcHistory", Fields("week:int", "delta:double", "reason:string") },
            { "WebPersonaScore", Fields("persona:string", "score:int") },
            { "WebPersonaHistory", Fields("persona:string", "week:int") },
            { "WebJurorSentiment", Fields("jurorId:string", "jurorName:string", "sentiment:double", "events:List<WebJurySentimentEvent>") },
            { "NpcConversationState", Fields("sequence:long", "startedTick:long", "firstId:string", "secondId:string", "topic:string", "rendezvousId:string", "week:int", "phase:EpisodePhase", "durationMs:double") },
            { "NpcCooldownState", Fields("npcId:string", "untilTick:long") },
            { "NpcPairMemoryState", Fields("fromId:string", "toId:string", "lastTopic:string", "count:int", "lastStartTick:long") },
            { "HouseEventChoice", Fields("label:string", "description:string", "risk:string", "impacts:List<HouseEventImpact>", "trustChange:double", "optionId:string", "glyph:string", "approach:string", "checkBase:double", "subjectId:string", "lapse:bool", "costsAction:bool", "conduct:bool", "pickPerson:bool", "eligibleIds:List<string>", "locked:bool", "lockReason:string", "bonusTraits:List<string>", "against:List<string>", "effects:List<StoryEffectState>", "backfire:List<StoryEffectState>", "next:string", "nextOnBackfire:string") },
            { "StoryRoleState", Fields("role:string", "contestantId:string") },
            { "StoryStepState", Fields("beatId:string", "optionId:string", "result:string", "week:int", "logSequence:int") },
            { "StoryVarState", Fields("key:string", "value:int") },
            { "GrudgeState", Fields("holderId:string", "targetId:string", "cause:string", "severity:double", "originWeek:int", "count:int") },
            { "BondState", Fields("id:string", "kind:string", "aId:string", "bId:string", "status:string", "cycleId:string", "sinceWeek:int", "endedWeek:int") },
            { "HookState", Fields("id:string", "holderId:string", "overId:string", "factId:string", "week:int", "spent:bool") },
            { "ContactState", Fields("npcId:string", "rapport:int", "weekCount:int", "lastWeek:int") },
            { "LoreCastState", Fields("contestantId:string", "sheetKey:string", "source:string", "factIds:List<string>") },
            { "ConductState", Fields("contestantId:string", "strikes:int", "lastStrikeWeek:int", "cleanWeeks:int", "sitsOutWeek:int", "pushedBack:int", "reasons:List<string>") },
            { "RemovalState", Fields("contestantId:string", "reasonId:string", "week:int") },
            { "StoryCooldownState", Fields("key:string", "untilWeek:int") },
            { "ReckoningState", Fields("npcId:string", "cause:string", "betrayerIsPlayer:bool", "week:int") },
            { "AppearanceValue", Fields("id:string", "value:float") },
            { "AppearanceColor", Fields("id:string", "r:float", "g:float", "b:float", "a:float") },
            { "CharacterOutfit", Fields("id:string", "wardrobe:List<AppearanceWardrobe>", "colors:List<AppearanceColor>") },
            { "OpportunityStep", Fields("at:string", "choice:string", "matchedRead:bool") },
            { "WebJurySentimentEvent", Fields("week:int", "delta:double", "reason:string") },
            { "HouseEventImpact", Fields("targetId:string", "amount:double") },
            { "StoryEffectState", Fields("kind:string", "fromId:string", "toId:string", "thirdId:string", "type:string", "text:string", "amount:double", "weeks:int") },
            { "AppearanceWardrobe", Fields("slot:string", "itemId:string") }
        };

        private static Dictionary<string, string> Fields(params string[] definitions) =>
            definitions.Select(value => value.Split(':')).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);

        internal static void Validate(JToken token) => Check(token, "EpisodeState", "state(v25)", 0);

        private static void Check(JToken token, string type, string path, int depth)
        {
            if (token == null || depth > 64) throw Invalid(path, "missing or too deeply nested");
            bool valueType = type == "bool" || type == "int" || type == "uint" || type == "long"
                || type == "float" || type == "double" || type == "EpisodePhase" || type == "EvictionStage"
                || type == "ContestantStatus" || type == "PromiseKind" || type == "PromiseStatus";
            if (token.Type == JTokenType.Null)
            {
                if (valueType) throw Invalid(path, "cannot be null");
                return;
            }
            if (type == "string") { if (token.Type != JTokenType.String) throw Invalid(path, "must be text"); return; }
            if (type == "bool") { if (token.Type != JTokenType.Boolean) throw Invalid(path, "must be boolean"); return; }
            if (type == "float" || type == "double")
            {
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer) throw Invalid(path, "must be numeric");
                double value = (double)token;
                if (double.IsNaN(value) || double.IsInfinity(value)) throw Invalid(path, "must be finite");
                return;
            }
            if (valueType)
            {
                if (token.Type != JTokenType.Integer) throw Invalid(path, "must be an integer");
                if (type == "uint")
                { if (!uint.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out _)) throw Invalid(path, "exceeds its numeric contract"); }
                else if (type == "long")
                { if (!long.TryParse(token.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)) throw Invalid(path, "exceeds its numeric contract"); }
                else if (!int.TryParse(token.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    throw Invalid(path, "exceeds its numeric contract");
                return;
            }
            if (type.StartsWith("List<", StringComparison.Ordinal))
            {
                if (!(token is JArray array)) throw Invalid(path, "must be an array");
                string element = type.Substring(5, type.Length - 6);
                foreach (var row in array) Check(row, element, path + "[]", depth + 1);
                return;
            }
            if (!(token is JObject obj) || !Shapes.TryGetValue(type, out var fields)) throw Invalid(path, "must be a frozen object");
            if (obj.Properties().Count() != fields.Count || fields.Keys.Any(name => obj.Property(name) == null))
                throw Invalid(path, "contains missing or unknown schema25 fields");
            foreach (var field in fields) Check(obj[field.Key], field.Value, path + "." + field.Key, depth + 1);
        }

        private static InvalidDataException Invalid(string path, string reason) => new InvalidDataException(path + " " + reason + ".");
    }
}
