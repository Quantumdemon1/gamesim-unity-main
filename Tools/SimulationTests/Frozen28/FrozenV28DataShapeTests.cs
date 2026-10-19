// Exhaustive source-declared fixed28 component matrix. No current game/runtime/corpus oracle.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Gamesim.Persistence.Frozen28Components;

namespace Gamesim.Tests.Frozen28Components
{
    [TestFixture]
    public sealed class FrozenV28DataShapeTests
    {
        // Independent test expectation tape emitted from historical declarations, not Shape's registry.
        private static readonly TypeSpec[] ExpectedTypes =
        {
            new TypeSpec("AllianceRow", "id:string", "why:string", "startedWeek:int", "endedWeek:int"),
            new TypeSpec("AllianceState", "id:string", "name:string", "members:List<string>", "active:bool", "playerJoined:bool"),
            new TypeSpec("AppearanceColor", "id:string", "r:float", "g:float", "b:float", "a:float"),
            new TypeSpec("AppearanceValue", "id:string", "value:float"),
            new TypeSpec("AppearanceWardrobe", "slot:string", "itemId:string"),
            new TypeSpec("ArcHistory", "week:int", "delta:double", "reason:string"),
            new TypeSpec("BallotRow", "week:int", "voterId:string", "targetId:string", "readBefore:string", "correct:bool"),
            new TypeSpec("BlocCallRow", "week:int", "allianceId:string", "callerId:string", "targetId:string", "followed:List<string>", "defected:List<string>"),
            new TypeSpec("BondState", "id:string", "kind:string", "aId:string", "bId:string", "status:string", "cycleId:string", "sinceWeek:int", "endedWeek:int"),
            new TypeSpec("CharacterAppearance", "version:int", "provider:string", "presetId:string", "bodyId:string", "fallbackId:string", "activeOutfit:string", "dna:List<AppearanceValue>", "colors:List<AppearanceColor>", "outfits:List<CharacterOutfit>"),
            new TypeSpec("CharacterOutfit", "id:string", "wardrobe:List<AppearanceWardrobe>", "colors:List<AppearanceColor>"),
            new TypeSpec("ClaimRow", "week:int", "voterId:string", "targetId:string", "source:string", "status:string"),
            new TypeSpec("CompetitionRow", "week:int", "field:int", "placement:int", "kind:string", "entry:string", "performance:double", "expectedWin:double"),
            new TypeSpec("CompetitionScore", "contestantId:string", "score:double"),
            new TypeSpec("ConductState", "contestantId:string", "strikes:int", "lastStrikeWeek:int", "cleanWeeks:int", "sitsOutWeek:int", "pushedBack:int", "reasons:List<string>"),
            new TypeSpec("ContactState", "npcId:string", "rapport:int", "weekCount:int", "lastWeek:int"),
            new TypeSpec("ContestantState", "id:string", "name:string", "pronouns:string", "motive:string", "homeRoom:string", "occupation:string", "archetype:string", "hometown:string", "bio:string", "sourceTemplateId:string", "appearance:CharacterAppearance", "age:int", "mood:string", "stressLevel:string", "isPlayer:bool", "status:ContestantStatus", "stats:ContestantStats", "traits:List<string>", "hohWins:int", "vetoWins:int", "timesNominated:int", "nominationWeeks:List<int>"),
            new TypeSpec("ContestantStats", "physical:double", "mental:double", "endurance:double", "social:double", "luck:double", "competition:double", "strategic:double", "loyalty:double"),
            new TypeSpec("DealState", "id:string", "type:string", "proposerId:string", "recipientId:string", "targetId:string", "status:string", "week:int", "expiresWeek:int", "trustImpact:string", "brokenById:string", "settledWeek:int", "linkedDealId:string"),
            new TypeSpec("DiaryPromptState", "id:string", "trigger:string", "evictedId:string", "week:int", "isNominee:bool"),
            new TypeSpec("EpisodeEvent", "sequence:int", "week:int", "phase:EpisodePhase", "kind:string", "text:string", "audienceIds:List<string>"),
            new TypeSpec("EpisodeState", "schemaVersion:int", "competitionRulesVersion:int", "sessionId:string", "seed:uint", "randomState:uint", "revision:int", "week:int", "nextSequence:int", "socialActions:int", "phase:EpisodePhase", "playerId:string", "hohId:string", "previousHohId:string", "vetoHolderId:string", "winnerId:string", "runnerUpId:string", "finalPart1WinnerId:string", "finalPart2WinnerId:string", "competitionResolved:bool", "vetoResolved:bool", "evictionResolved:bool", "contestants:List<ContestantState>", "relationships:List<RelationshipState>", "promises:List<PromiseState>", "alliances:List<AllianceState>", "memories:List<MemoryState>", "nominees:List<string>", "vetoPlayers:List<string>", "votes:List<VoteState>", "ledger:SeasonLedger", "readRulesStartWeek:int", "leverRulesStartWeek:int", "weekRulesStartWeek:int", "windowActions:List<int>", "economyRulesVersion:int", "moveInExtrasSpent:int", "agencyRulesStartWeek:int", "finaleRulesStartWeek:int", "commitmentRulesStartWeek:int", "unifiedCommitmentRulesVersion:int", "unifiedCommitments:List<UnifiedCommitmentState>", "unifiedHearingRulesVersion:int", "unifiedHearingEvidence:List<UnifiedHearingEvidenceState>", "unifiedHearingReceipts:List<UnifiedHearingReceiptState>", "unifiedVoteReveals:List<UnifiedVoteRevealState>", "competitionScores:List<CompetitionScore>", "events:List<EpisodeEvent>", "acceptedCommandIds:List<string>", "juryExchanges:List<JuryExchangeState>", "juryQuestionIndex:int", "finalSpeeches:List<FinalSpeechState>", "finalArgument:FinalArgumentState", "relationshipArcs:List<RelationshipArcState>", "playerPersona:WebPersonaState", "jurySentiment:WebJurySentimentState", "pendingDiary:DiaryPromptState", "lastDiaryRoomWeek:int", "phaseEventSocialBonus:int", "phaseEventCompBonus:int", "resolvedDiaryIds:List<string>", "loyaltyOaths:List<WebOathRecord>", "oathOpportunities:List<string>", "shownOathMilestones:List<string>", "playerStudyBonus:int", "blocRulesStartWeek:int", "npcSocial:NpcSocialState", "evictionStage:EvictionStage", "evictionSpeeches:List<EvictionSpeechState>", "backdoorTargetId:string", "outOfPhaseSocialActions:int", "openingBeatsSeen:List<string>", "socialBudgetRulesStartWeek:int", "deals:List<DealState>", "dealRulesStartWeek:int", "boughtActionPoints:int", "houseEvents:List<HouseEventState>", "eventRulesStartWeek:int", "storylines:List<StorylineState>", "activeModifiers:List<StoryModifierState>", "storyRulesStartWeek:int", "haveNotRulesStartWeek:int", "haveNots:List<string>", "haveNotPasses:List<string>", "punishedHaveNots:List<string>", "vetoPrizes:List<VetoPrizeState>", "strategyRulesStartWeek:int", "lobbies:List<LobbyState>", "replyCards:List<ReplyCardState>", "story:StoryWorldState", "allianceLeakRulesStartWeek:int", "pactPlanRulesStartWeek:int", "allWeekRulesStartWeek:int"),
            new TypeSpec("EvictionSpeechState", "speakerId:string", "text:string", "week:int", "isPlayerAuthored:bool"),
            new TypeSpec("FinalArgumentState", "theme:string", "momentRefs:List<string>"),
            new TypeSpec("FinalSpeechState", "speakerId:string", "text:string", "isPlayerAuthored:bool"),
            new TypeSpec("GrudgeState", "holderId:string", "targetId:string", "cause:string", "severity:double", "originWeek:int", "count:int"),
            new TypeSpec("HookState", "id:string", "holderId:string", "overId:string", "factId:string", "week:int", "spent:bool"),
            new TypeSpec("HouseEventChoice", "label:string", "description:string", "risk:string", "impacts:List<HouseEventImpact>", "trustChange:double", "optionId:string", "glyph:string", "approach:string", "checkBase:double", "subjectId:string", "lapse:bool", "costsAction:bool", "conduct:bool", "pickPerson:bool", "eligibleIds:List<string>", "locked:bool", "lockReason:string", "bonusTraits:List<string>", "against:List<string>", "effects:List<StoryEffectState>", "backfire:List<StoryEffectState>", "next:string", "nextOnBackfire:string"),
            new TypeSpec("HouseEventImpact", "targetId:string", "amount:double"),
            new TypeSpec("HouseEventState", "id:string", "kind:string", "title:string", "narrative:string", "involvedIds:List<string>", "choices:List<HouseEventChoice>", "week:int", "resolved:bool", "chosenIndex:int", "outcome:string", "contentId:string", "cycleId:string", "closesAnchor:string", "surface:string", "venue:string", "lapseOptionId:string", "cast:List<StoryRoleState>"),
            new TypeSpec("HouseFactState", "id:string", "kind:string", "actorId:string", "subjectId:string", "refId:string", "visibility:string", "week:int", "knowers:List<string>"),
            new TypeSpec("JuryExchangeState", "questionerId:string", "finalistId:string", "tone:string", "question:string", "optionA:string", "optionB:string", "correctChoice:string", "answerChoice:string", "answer:string", "opponentAnswer:string", "category:string", "receiptKind:string", "receiptId:string", "completed:bool"),
            new TypeSpec("LobbyState", "week:int", "phase:EpisodePhase", "deciderId:string", "ask:string", "subjectId:string", "approach:string", "response:string", "influence:double"),
            new TypeSpec("LoreCastState", "contestantId:string", "sheetKey:string", "source:string", "factIds:List<string>"),
            new TypeSpec("MemoryState", "ownerId:string", "subjectId:string", "text:string", "week:int", "isPrivate:bool"),
            new TypeSpec("NpcActState", "id:string", "kind:string", "actorId:string", "partnerId:string", "subjectId:string", "room:string", "week:int", "window:int", "firedTick:int", "sighted:bool", "overheard:bool"),
            new TypeSpec("NpcConversationState", "sequence:long", "startedTick:long", "firstId:string", "secondId:string", "topic:string", "rendezvousId:string", "week:int", "phase:EpisodePhase", "durationMs:double"),
            new TypeSpec("NpcCooldownState", "npcId:string", "untilTick:long"),
            new TypeSpec("NpcPairMemoryState", "fromId:string", "toId:string", "lastTopic:string", "count:int", "lastStartTick:long"),
            new TypeSpec("NpcSocialState", "rulesStartWeek:int", "clockTick:long", "nextScanTick:long", "nextConversationSequence:long", "randomState:uint", "pending:List<NpcConversationState>", "cooldowns:List<NpcCooldownState>", "pairMemory:List<NpcPairMemoryState>", "beatWeek:int", "beatWindow:int", "beatsFired:int", "beatSeats:int", "beatPlan:List<string>", "acts:List<NpcActState>"),
            new TypeSpec("OpportunityRow", "id:string", "kind:string", "anchor:string", "source:string", "response:string", "outcome:string", "currency:string", "note:string", "week:int", "payoff:double", "steps:List<OpportunityStep>"),
            new TypeSpec("OpportunityStep", "at:string", "choice:string", "matchedRead:bool"),
            new TypeSpec("PactPlanRow", "week:int", "allianceId:string", "throughId:string", "stance:string", "present:List<string>", "cameRound:List<string>", "followed:List<string>", "says:List<PlanSay>", "counterId:string", "targetId:string", "callerId:string"),
            new TypeSpec("PlanSay", "memberId:string", "targetId:string"),
            new TypeSpec("PowerRow", "week:int", "hohId:string", "vetoHolderId:string", "savedId:string", "replacementId:string", "evicteeId:string", "backdoorTargetId:string", "backdoorResult:string", "vetoUsed:bool", "nominees:List<string>", "tally:List<int>"),
            new TypeSpec("PromiseState", "id:string", "fromId:string", "toId:string", "targetId:string", "kind:PromiseKind", "status:PromiseStatus", "week:int", "expiresWeek:int", "impact:string", "brokenById:string", "settledWeek:int"),
            new TypeSpec("ReckoningState", "npcId:string", "cause:string", "betrayerIsPlayer:bool", "week:int"),
            new TypeSpec("RelationshipArcState", "npcId:string", "npcName:string", "arcType:string", "intensity:double", "escalationLevel:int", "weeklyHistory:List<ArcHistory>"),
            new TypeSpec("RelationshipEventState", "sequence:int", "week:int", "type:string", "description:string", "impactScore:double", "decayable:bool"),
            new TypeSpec("RelationshipState", "fromId:string", "toId:string", "score:double", "lastInteractionWeek:int", "notes:List<string>", "events:List<RelationshipEventState>"),
            new TypeSpec("RemovalState", "contestantId:string", "reasonId:string", "week:int"),
            new TypeSpec("ReplyCardState", "id:string", "week:int", "kind:string", "fromId:string", "aboutId:string"),
            new TypeSpec("ReplyRow", "week:int", "cardId:string", "kind:string", "fromId:string", "listenerId:string", "replyKey:string", "promised:bool", "toThem:double"),
            new TypeSpec("SeasonLedger", "opportunities:List<OpportunityRow>", "competitions:List<CompetitionRow>", "power:List<PowerRow>", "ballots:List<BallotRow>", "claims:List<ClaimRow>", "alliances:List<AllianceRow>", "standings:List<StandingRow>", "replies:List<ReplyRow>", "calls:List<BlocCallRow>", "dropped:int", "plans:List<PactPlanRow>"),
            new TypeSpec("StandingRow", "week:int", "fromId:string", "toId:string", "source:string", "score:double"),
            new TypeSpec("StoryCooldownState", "key:string", "untilWeek:int"),
            new TypeSpec("StoryEffectState", "kind:string", "fromId:string", "toId:string", "thirdId:string", "type:string", "text:string", "amount:double", "weeks:int"),
            new TypeSpec("StoryModifierState", "id:string", "name:string", "description:string", "weeksLeft:int", "competitionBonus:double", "socialBonus:double", "ownerId:string"),
            new TypeSpec("StoryRoleState", "role:string", "contestantId:string"),
            new TypeSpec("StoryStepState", "beatId:string", "optionId:string", "result:string", "week:int", "logSequence:int"),
            new TypeSpec("StoryVarState", "key:string", "value:int"),
            new TypeSpec("StoryWorldState", "rulesStartWeek:int", "rulesVersion:int", "productionStrictness:int", "romanceStorylines:bool", "grudges:List<GrudgeState>", "facts:List<HouseFactState>", "bonds:List<BondState>", "hooks:List<HookState>", "contacts:List<ContactState>", "lore:List<LoreCastState>", "knownFacts:List<string>", "conduct:List<ConductState>", "removals:List<RemovalState>", "pendingRemovalId:string", "cooldowns:List<StoryCooldownState>", "reckonings:List<ReckoningState>"),
            new TypeSpec("StorylineState", "id:string", "templateId:string", "category:string", "title:string", "eventId:string", "status:string", "week:int", "endedWeek:int", "beatId:string", "lane:string", "variant:int", "cast:List<StoryRoleState>", "path:List<StoryStepState>", "vars:List<StoryVarState>", "nextWeek:int", "nextAnchor:string", "endingId:string"),
            new TypeSpec("UnifiedCommitmentState", "id:string", "kind:string", "sourcePolicy:string", "origin:string", "makerId:string", "beneficiaryId:string", "reciprocal:bool", "createdWeek:int", "expiresWeek:int", "status:string", "settledWeek:int", "brokenById:string", "trustImpact:string", "linkedCommitmentId:string", "settlementEffectKey:string", "targetId:string", "subtype:string", "voteBindingWeek:int", "voteFirstRevealWeek:int"),
            new TypeSpec("UnifiedHearingEvidenceState", "incidentKey:string", "fact:HouseFactState"),
            new TypeSpec("UnifiedHearingReceiptState", "incidentKey:string", "listenerId:string", "factId:string", "kind:string", "heardWeek:int"),
            new TypeSpec("UnifiedVoteBallotState", "voterId:string", "targetId:string"),
            new TypeSpec("UnifiedVoteRevealState", "week:int", "ballots:List<UnifiedVoteBallotState>"),
            new TypeSpec("VetoPrizeState", "week:int", "contestantId:string", "prizeId:string"),
            new TypeSpec("VoteState", "voterId:string", "targetId:string", "reason:string"),
            new TypeSpec("WebJurorSentiment", "jurorId:string", "jurorName:string", "sentiment:double", "events:List<WebJurySentimentEvent>"),
            new TypeSpec("WebJurySentimentEvent", "week:int", "delta:double", "reason:string"),
            new TypeSpec("WebJurySentimentState", "jurors:List<WebJurorSentiment>", "overallSentiment:double"),
            new TypeSpec("WebOathRecord", "playerId:string", "targetId:string", "week:int", "timestamp:long"),
            new TypeSpec("WebPersonaHistory", "persona:string", "week:int"),
            new TypeSpec("WebPersonaScore", "persona:string", "score:int"),
            new TypeSpec("WebPersonaState", "current:string", "scores:List<WebPersonaScore>", "history:List<WebPersonaHistory>"),
        };
        private static readonly EnumSpec[] ExpectedEnums =
        {
            new EnumSpec("ContestantStatus", "Active:0", "Evicted:1", "Jury:2", "Winner:3", "RunnerUp:4", "Expelled:5"),
            new EnumSpec("EpisodePhase", "Social:0", "HoH:1", "Nomination:2", "VetoSelection:3", "Veto:4", "VetoMeeting:5", "Campaign:6", "Eviction:7", "FinalHoHPart1:8", "FinalHoHPart2:9", "FinalHoHPart3:10", "FinalEviction:11", "Jury:12", "Finished:13", "JuryQuestioning:14", "FinalSpeeches:15"),
            new EnumSpec("EvictionStage", "Interaction:0", "Speeches:1", "Voting:2", "Tiebreaker:3", "Results:4"),
            new EnumSpec("PromiseKind", "Safety:0", "Vote:1", "FinalTwo:2", "AllianceLoyalty:3", "Information:4"),
            new EnumSpec("PromiseStatus", "Active:0", "Fulfilled:1", "Broken:2", "Expired:3"),
        };

        private sealed class TypeSpec
        {
            internal readonly string Name;
            internal readonly FieldSpec[] Fields;
            internal TypeSpec(string name, params string[] fields)
            {
                Name = name;
                Fields = fields.Select(text => text.Split(':'))
                    .Select(parts => new FieldSpec(name, parts[0], parts[1])).ToArray();
            }
        }
        private sealed class FieldSpec
        {
            internal readonly string Owner, Name, Type;
            internal FieldSpec(string owner, string name, string type) { Owner = owner; Name = name; Type = type; }
        }
        private sealed class EnumSpec
        {
            internal readonly string Name;
            internal readonly string[] Entries;
            internal EnumSpec(string name, params string[] entries) { Name = name; Entries = entries; }
        }

        private static IEnumerable<TestCaseData> Types()
        {
            foreach (var type in ExpectedTypes) yield return new TestCaseData(type.Name);
        }
        private static IEnumerable<TestCaseData> Fields()
        {
            foreach (var type in ExpectedTypes)
                foreach (var field in type.Fields)
                    yield return new TestCaseData(type.Name, field.Name, field.Type);
        }
        private static IEnumerable<TestCaseData> IntegralFields()
        {
            foreach (var type in ExpectedTypes)
                foreach (var field in type.Fields)
                    if ((field.Type == "int" || field.Type == "uint" || field.Type == "long")
                        && !(field.Owner == "EpisodeState" && field.Name == "schemaVersion"))
                        yield return new TestCaseData(type.Name, field.Name, field.Type);
        }
        private static IEnumerable<TestCaseData> NumericFields()
        {
            foreach (var type in ExpectedTypes)
                foreach (var field in type.Fields)
                    if (field.Type == "double" || field.Type == "float")
                        yield return new TestCaseData(type.Name, field.Name, field.Type);
        }
        private static IEnumerable<TestCaseData> ListFields()
        {
            foreach (var type in ExpectedTypes)
                foreach (var field in type.Fields)
                    if (field.Type.StartsWith("List<", StringComparison.Ordinal))
                        yield return new TestCaseData(type.Name, field.Name, field.Type);
        }
        private static IEnumerable<TestCaseData> Enums()
        {
            foreach (var entry in ExpectedEnums) yield return new TestCaseData(entry.Name);
        }
        private static bool IsEnum(string name) => ExpectedEnums.Any(item => item.Name == name);
        private static bool IsReference(string type) => type == "string"
            || type.StartsWith("List<", StringComparison.Ordinal) || ExpectedTypes.Any(item => item.Name == type);
        private static TypeSpec Spec(string name) => ExpectedTypes.Single(item => item.Name == name);

        private static JToken LiteralValue(string type)
        {
            if (type == "bool") return new JValue(false);
            if (type == "double" || type == "float") return new JValue(.25);
            if (type == "int" || type == "uint" || type == "long" || IsEnum(type)) return new JValue(0);
            if (type.StartsWith("List<", StringComparison.Ordinal)) return new JArray();
            return JValue.CreateNull();
        }
        private static JObject Component(string name)
        {
            var obj = new JObject(Spec(name).Fields.Select(field => new JProperty(field.Name, LiteralValue(field.Type))));
            if (name == "EpisodeState") obj["schemaVersion"] = 28;
            return obj;
        }
        private static JToken WrongValue(string type)
        {
            if (type == "string" || type == "bool") return new JObject();
            if (type == "double" || type == "float" || type == "int" || type == "uint" || type == "long" || IsEnum(type))
                return new JValue("1");
            return new JValue(false);
        }
        private static void Preserve(JToken original, Action operation, bool refuses)
        {
            var text = original.ToString(Formatting.None);
            var nodes = ((JContainer)original).DescendantsAndSelf().ToArray();
            var parents = nodes.Select(node => node.Parent).ToArray();
            if (refuses) Assert.Throws<InvalidDataException>(() => operation());
            else operation();
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(text));
            var after = ((JContainer)original).DescendantsAndSelf().ToArray();
            Assert.That(after.Length, Is.EqualTo(nodes.Length));
            for (int i = 0; i < nodes.Length; i++)
            {
                Assert.That(ReferenceEquals(after[i], nodes[i]), Is.True, "Original node was replaced.");
                Assert.That(ReferenceEquals(after[i].Parent, parents[i]), Is.True, "Original parent changed.");
            }
        }
        private static string LocalSpelling(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return "List<" + LocalSpelling(type.GetGenericArguments()[0]) + ">";
            Assert.That(type.Namespace, Is.EqualTo("Gamesim.Persistence.Frozen28Data"));
            return type.Name;
        }

        [Test]
        public void DeclarationTapeHasExactClosedHistoricalInventory()
        {
            Assert.That(ExpectedTypes.Length, Is.EqualTo(77));
            Assert.That(ExpectedTypes.Sum(type => type.Fields.Length), Is.EqualTo(583));
            Assert.That(ExpectedEnums.Length, Is.EqualTo(5));
            Assert.That(Spec("EpisodeState").Fields.Length, Is.EqualTo(92));
            Assert.That(Spec("UnifiedCommitmentState").Fields.Length, Is.EqualTo(19));
            Assert.That(ExpectedTypes.Select(type => type.Name).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(77));
            var singles = ExpectedTypes.SelectMany(type => type.Fields)
                .Where(field => field.Type == "float").Select(field => field.Owner + "." + field.Name).ToArray();
            Assert.That(singles, Is.EquivalentTo(new[]
            { "AppearanceValue.value", "AppearanceColor.r", "AppearanceColor.g", "AppearanceColor.b", "AppearanceColor.a" }));
        }

        [TestCaseSource(nameof(Types))]
        public void LocalCarrierMatchesEveryLiteralFieldWithoutAdditionalSavedMembers(string name)
        {
            var original = Component(name);
            FrozenV28Shape.CheckComponent(original, name); // Literal shape precedes local reflection.
            var type = FrozenV28Types.Resolve(name);
            Assert.That(type.Namespace, Is.EqualTo("Gamesim.Persistence.Frozen28Data"));
            Assert.That(type.IsSealed, Is.True);
            Assert.That(type.IsDefined(typeof(SerializableAttribute), false), Is.True);
            var actual = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Assert.That(actual.Length, Is.EqualTo(Spec(name).Fields.Length));
            Assert.That(type.GetProperties(BindingFlags.Instance | BindingFlags.Public), Is.Empty);
            foreach (var expected in Spec(name).Fields)
            {
                var match = actual.Single(field => field.Name == expected.Name);
                Assert.That(LocalSpelling(match.FieldType), Is.EqualTo(expected.Type), name + "." + expected.Name);
            }
        }

        [TestCaseSource(nameof(Types))]
        public void EveryLiteralComponentHydratesWithoutMutatingOriginal(string name)
        {
            var original = Component(name);
            Preserve(original, () =>
            {
                    var snapshot = FrozenV28Input.HydrateComponentShapeOnly(original, name);
                    Assert.That(snapshot.GetType(), Is.EqualTo(FrozenV28Types.Resolve(name)));
                    foreach (var field in Spec(name).Fields)
                    {
                        var actual = snapshot.GetType().GetField(field.Name).GetValue(snapshot);
                        if (original[field.Name].Type == JTokenType.Null) Assert.That(actual, Is.Null);
                        else if (field.Type.StartsWith("List<", StringComparison.Ordinal))
                            Assert.That(((System.Collections.ICollection)actual).Count, Is.EqualTo(0));
                        else if (field.Type == "bool") Assert.That(actual, Is.EqualTo(false));
                        else if (field.Type == "float" || field.Type == "double")
                            Assert.That(Convert.ToDouble(actual, CultureInfo.InvariantCulture), Is.EqualTo(.25));
                        else Assert.That(Convert.ToInt64(actual, CultureInfo.InvariantCulture),
                            Is.EqualTo((long)original[field.Name]));
                    }
            }, false);
        }

        [TestCaseSource(nameof(Fields))]
        public void MissingFieldIsRefusedRatherThanRepaired(string owner, string name, string type)
        {
            var original = Component(owner);
            original.Property(name, StringComparison.Ordinal).Remove();
            Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), true);
        }

        [TestCaseSource(nameof(Fields))]
        public void CorrectFieldReplacedByUnknownFieldIsRefused(string owner, string name, string type)
        {
            var original = Component(owner);
            var value = original[name].DeepClone();
            original.Property(name, StringComparison.Ordinal).Remove();
            original.Add("notSaved_" + name, value);
            Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), true);
        }

        [TestCaseSource(nameof(Fields))]
        public void EveryFieldRejectsItsWrongTokenKind(string owner, string name, string type)
        {
            var original = Component(owner);
            original[name] = WrongValue(type);
            Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), true);
        }

        [TestCaseSource(nameof(Fields))]
        public void ExplicitNullKeepsTheHistoricalReferenceOrValueTypeDistinction(string owner, string name, string type)
        {
            var original = Component(owner);
            original[name] = JValue.CreateNull();
            Preserve(original, () =>
            {
                var snapshot = FrozenV28Input.HydrateComponentShapeOnly(original, owner);
                Assert.That(snapshot.GetType().GetField(name).GetValue(snapshot), Is.Null);
            }, !IsReference(type));
        }

        [TestCaseSource(nameof(Fields))]
        public void AdditionalUnknownFieldNeverDisappearsDuringHydration(string owner, string name, string type)
        {
            var original = Component(owner);
            original.Add("extra_" + name, JValue.CreateNull());
            Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), true);
        }

        [TestCaseSource(nameof(Types))]
        public void PropertyOrderDoesNotChangeTheComponentJudgmentOrInputOrder(string name)
        {
            var original = Component(name);
            var reversed = new JObject(original.Properties().Reverse()
                .Select(property => new JProperty(property.Name, property.Value.DeepClone())));
            Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, name), false);
            Preserve(reversed, () => FrozenV28Input.HydrateComponentShapeOnly(reversed, name), false);
            Assert.That(reversed.Properties().Select(property => property.Name),
                Is.EqualTo(original.Properties().Select(property => property.Name).Reverse()));
        }

        [TestCaseSource(nameof(ListFields))]
        public void EveryListElementRejectsItsWrongTokenKind(string owner, string name, string type)
        {
            string element = type.Substring(5, type.Length - 6);
            var original = Component(owner);
            original[name] = new JArray(WrongValue(element));
            Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), true);
        }

        [TestCaseSource(nameof(ListFields))]
        public void EveryListElementRetainsTheReferenceNullDistinction(string owner, string name, string type)
        {
            string element = type.Substring(5, type.Length - 6);
            var original = Component(owner);
            original[name] = new JArray(JValue.CreateNull());
            Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), !IsReference(element));
        }

        [TestCaseSource(nameof(IntegralFields))]
        public void IntegralWidthsAreEnforcedByLocalHydrationNotDefaultRepair(string owner, string name, string type)
        {
            BigInteger minimum = type == "uint" ? BigInteger.Zero
                : type == "long" ? new BigInteger(long.MinValue) : new BigInteger(int.MinValue);
            BigInteger maximum = type == "uint" ? new BigInteger(uint.MaxValue)
                : type == "long" ? new BigInteger(long.MaxValue) : new BigInteger(int.MaxValue);
            foreach (var value in new[] { minimum, maximum })
            {
                var original = Component(owner);
                original[name] = new JValue((long)value);
                Preserve(original, () =>
                {
                    var snapshot = FrozenV28Input.HydrateComponentShapeOnly(original, owner);
                    var actual = snapshot.GetType().GetField(name).GetValue(snapshot);
                    Assert.That(Convert.ToString(actual, CultureInfo.InvariantCulture),
                        Is.EqualTo(value.ToString(CultureInfo.InvariantCulture)));
                }, false);
            }
            foreach (var value in new[] { minimum - BigInteger.One, maximum + BigInteger.One, BigInteger.Pow(10, 1000) })
            {
                var original = Component(owner);
                original[name] = new JValue(value);
                Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), true);
            }
        }

        [TestCaseSource(nameof(NumericFields))]
        public void NumericFieldsRejectAllNonfiniteTokensAndRetainFiniteIntegerAndFloatInputs(string owner, string name, string type)
        {
            foreach (var number in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var original = Component(owner);
                original[name] = new JValue(number);
                Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), true);
            }
            foreach (var number in new[] { 0d, -.25d, .25d, 1d })
            {
                var original = Component(owner);
                original[name] = new JValue(number);
                Preserve(original, () => FrozenV28Input.HydrateComponentShapeOnly(original, owner), false);
            }
            var integer = Component(owner);
            integer[name] = new JValue(1);
            Preserve(integer, () => FrozenV28Input.HydrateComponentShapeOnly(integer, owner), false);
        }

        [TestCaseSource(nameof(Enums))]
        public void EveryFixedEnumNameAndOrdinalMatchesTheLiteralSource(string name)
        {
            var spec = ExpectedEnums.Single(item => item.Name == name);
            var field = ExpectedTypes.SelectMany(item => item.Fields).First(item => item.Type == name);
            var original = Component(field.Owner);
            FrozenV28Shape.CheckComponent(original, field.Owner);
            var actual = FrozenV28Types.Resolve(field.Owner).GetField(field.Name).FieldType;
            Assert.That(actual.IsEnum, Is.True);
            Assert.That(Enum.GetUnderlyingType(actual), Is.EqualTo(typeof(int)));
            var names = spec.Entries.Select(entry => entry.Split(':')[0]).ToArray();
            Assert.That(Enum.GetNames(actual), Is.EqualTo(names));
            foreach (var entry in spec.Entries)
            {
                var parts = entry.Split(':');
                Assert.That(Convert.ToInt32(Enum.Parse(actual, parts[0]), CultureInfo.InvariantCulture),
                    Is.EqualTo(int.Parse(parts[1], CultureInfo.InvariantCulture)));
            }
        }

        [Test]
        public void EnumMembershipRemainsAWholeSemanticObligationNotInventedByShape()
        {
            var original = Component("ContestantState");
            original["status"] = 127;
            Preserve(original, () =>
            {
                var snapshot = (Gamesim.Persistence.Frozen28Data.ContestantState)
                    FrozenV28Input.HydrateComponentShapeOnly(original, "ContestantState");
                Assert.That((int)snapshot.status, Is.EqualTo(127));
            }, false);
        }

        [Test]
        public void HydratedListsPreserveMeaningfulOrderWithoutAliasingOriginalTokens()
        {
            var original = Component("EpisodeState");
            original["acceptedCommandIds"] = new JArray("last", "first", "last");
            var snapshot = FrozenV28Input.HydrateRootShapeOnly(original);
            Assert.That(snapshot.acceptedCommandIds, Is.EqualTo(new[] { "last", "first", "last" }));
            snapshot.acceptedCommandIds[0] = "detached";
            Assert.That((string)original["acceptedCommandIds"][0], Is.EqualTo("last"));
        }

        [Test]
        public void LocalCarriersContainNoHistoricalFactoryDefaults()
        {
            var original = Component("EpisodeState");
            original["playerPersona"] = JValue.CreateNull();
            original["npcSocial"] = JValue.CreateNull();
            original["story"] = JValue.CreateNull();
            var snapshot = FrozenV28Input.HydrateRootShapeOnly(original);
            Assert.That(snapshot.playerPersona, Is.Null);
            Assert.That(snapshot.npcSocial, Is.Null);
            Assert.That(snapshot.story, Is.Null);
            Assert.That(snapshot.week, Is.EqualTo(0)); // Shape-only support; not a valid season.
        }

        // F28-S2 SOURCE-ONLY probes. Historical SaveJson.CheckDtoShape at the retained
        // EpisodeSaveStore.cs:263-269 checks finiteness as Double, not Single range.
        // Newtonsoft.Json13.0.3 EnsureType uses Convert.ChangeType for this Double->Single
        // path. The declared local net10 API routes through Double.IConvertible.ToSingle
        // and Convert.ToSingle(double), which narrows by a float cast.
        // Literal IEEE754 words below predict that LOCAL path; no historical CLR/native
        // comparison has run. Infinity after narrowing is observed, not clamped/refused
        // by a new policy, and is NOT evidence that a historical save is valid.
        private static readonly FieldSpec[] SingleLeaves =
        {
            new FieldSpec("AppearanceColor", "r", "float"),
            new FieldSpec("AppearanceColor", "g", "float"),
            new FieldSpec("AppearanceColor", "b", "float"),
            new FieldSpec("AppearanceColor", "a", "float"),
            new FieldSpec("AppearanceValue", "value", "float"),
        };

        private sealed class SingleBoundary
        {
            internal readonly string Label;
            internal readonly ulong InputBits;
            internal readonly uint ExpectedBits;
            internal SingleBoundary(string label, ulong inputBits, uint expectedBits)
            {
                Label = label; InputBits = inputBits; ExpectedBits = expectedBits;
            }
        }

        private static readonly SingleBoundary[] SingleBoundaries =
        {
            new SingleBoundary("positive_zero", 0x0000000000000000UL, 0x00000000U),
            new SingleBoundary("negative_zero", 0x8000000000000000UL, 0x80000000U),
            new SingleBoundary("positive_one", 0x3ff0000000000000UL, 0x3f800000U),
            new SingleBoundary("negative_one", 0xbff0000000000000UL, 0xbf800000U),
            new SingleBoundary("below_one_midpoint", 0x3ff000000fffffffUL, 0x3f800000U),
            new SingleBoundary("one_midpoint_ties_even", 0x3ff0000010000000UL, 0x3f800000U),
            new SingleBoundary("above_one_midpoint", 0x3ff0000010000001UL, 0x3f800001U),
            new SingleBoundary("next_single_above_one", 0x3ff0000020000000UL, 0x3f800001U),
            new SingleBoundary("next_midpoint_ties_even_up", 0x3ff0000030000000UL, 0x3f800002U),
            new SingleBoundary("negative_one_midpoint", 0xbff0000010000000UL, 0xbf800000U),
            new SingleBoundary("negative_above_one_midpoint", 0xbff0000010000001UL, 0xbf800001U),
            new SingleBoundary("single_max", 0x47efffffe0000000UL, 0x7f7fffffU),
            new SingleBoundary("below_single_max", 0x47efffffdfffffffUL, 0x7f7fffffU),
            new SingleBoundary("finite_just_above_single_max_rounds_back", 0x47efffffe0000001UL, 0x7f7fffffU),
            new SingleBoundary("below_overflow_midpoint", 0x47efffffefffffffUL, 0x7f7fffffU),
            new SingleBoundary("overflow_midpoint", 0x47effffff0000000UL, 0x7f800000U),
            new SingleBoundary("above_overflow_midpoint", 0x47effffff0000001UL, 0x7f800000U),
            new SingleBoundary("double_max", 0x7fefffffffffffffUL, 0x7f800000U),
            new SingleBoundary("negative_single_max", 0xc7efffffe0000000UL, 0xff7fffffU),
            new SingleBoundary("finite_just_below_negative_single_max_rounds_back", 0xc7efffffe0000001UL, 0xff7fffffU),
            new SingleBoundary("negative_overflow_midpoint", 0xc7effffff0000000UL, 0xff800000U),
            new SingleBoundary("negative_double_max", 0xffefffffffffffffUL, 0xff800000U),
            new SingleBoundary("double_smallest_positive", 0x0000000000000001UL, 0x00000000U),
            new SingleBoundary("double_smallest_negative", 0x8000000000000001UL, 0x80000000U),
            new SingleBoundary("single_smallest_positive", 0x36a0000000000000UL, 0x00000001U),
            new SingleBoundary("single_smallest_half_ties_zero", 0x3690000000000000UL, 0x00000000U),
            new SingleBoundary("above_single_smallest_half", 0x3690000000000001UL, 0x00000001U),
            new SingleBoundary("below_single_smallest_half", 0x368fffffffffffffUL, 0x00000000U),
            new SingleBoundary("single_smallest_normal", 0x3810000000000000UL, 0x00800000U),
            new SingleBoundary("single_largest_subnormal", 0x380fffffc0000000UL, 0x007fffffU),
            new SingleBoundary("single_smallest_negative", 0xb6a0000000000000UL, 0x80000001U),
            new SingleBoundary("negative_smallest_half_ties_zero", 0xb690000000000000UL, 0x80000000U),
            new SingleBoundary("below_negative_smallest_half", 0xb690000000000001UL, 0x80000001U),
        };

        private static IEnumerable<TestCaseData> SingleBoundaryCases()
        {
            foreach (var field in SingleLeaves)
                foreach (var boundary in SingleBoundaries)
                    yield return new TestCaseData(field.Owner, field.Name, boundary.Label,
                        boundary.InputBits, boundary.ExpectedBits)
                        .SetName("Single_LocalApi_NeedsNativeComparison_" + field.Owner + "_" + field.Name + "_" + boundary.Label);
        }

        private static IEnumerable<TestCaseData> SingleIntegralBoundaryCases()
        {
            foreach (var field in SingleLeaves)
            {
                yield return new TestCaseData(field.Owner, field.Name, 0L, 0x00000000U);
                yield return new TestCaseData(field.Owner, field.Name, -1L, 0xbf800000U);
                yield return new TestCaseData(field.Owner, field.Name, 16777217L, 0x4b800000U);
                yield return new TestCaseData(field.Owner, field.Name, 16777219L, 0x4b800002U);
                yield return new TestCaseData(field.Owner, field.Name, long.MaxValue, 0x5f000000U);
                yield return new TestCaseData(field.Owner, field.Name, long.MinValue, 0xdf000000U);
            }
        }

        [Test]
        public void SingleLiteralInventoryIsExactlyTheFiveHistoricalFields()
        {
            Assert.That(SingleLeaves.Select(field => field.Owner + "." + field.Name),
                Is.EquivalentTo(new[] { "AppearanceColor.r", "AppearanceColor.g", "AppearanceColor.b",
                    "AppearanceColor.a", "AppearanceValue.value" }));
            Assert.That(ExpectedTypes.SelectMany(type => type.Fields).Where(field => field.Type == "float")
                .Select(field => field.Owner + "." + field.Name),
                Is.EquivalentTo(SingleLeaves.Select(field => field.Owner + "." + field.Name)));
            Assert.That(SingleBoundaries.Length, Is.EqualTo(33));
        }

        [TestCaseSource(nameof(SingleBoundaryCases))]
        public void SingleFiniteDoubleBoundaryProbe_LocalApiExpectedBits_NeedsNativeComparison(
            string owner, string name, string label, ulong inputBits, uint expectedBits)
        {
            double number = BitConverter.Int64BitsToDouble(unchecked((long)inputBits));
            Assert.That(double.IsNaN(number) || double.IsInfinity(number), Is.False,
                "These cases exercise finite Double grammar, including narrowing overflow.");
            var original = Component(owner);
            original[name] = new JValue(number);
            Assert.That(original[name].Type, Is.EqualTo(JTokenType.Float));
            AssertSingleNarrowingWithImmutableInput(original, owner, name, label, expectedBits);
            Assert.That(unchecked((ulong)BitConverter.DoubleToInt64Bits((double)original[name])), Is.EqualTo(inputBits));
        }

        [TestCaseSource(nameof(SingleIntegralBoundaryCases))]
        public void SingleIntegralBoundaryProbe_LocalApiExpectedBits_NeedsNativeComparison(
            string owner, string name, long number, uint expectedBits)
        {
            var original = Component(owner);
            original[name] = new JValue(number);
            Assert.That(original[name].Type, Is.EqualTo(JTokenType.Integer));
            AssertSingleNarrowingWithImmutableInput(original, owner, name,
                "integer_" + number.ToString(CultureInfo.InvariantCulture), expectedBits);
            Assert.That((long)original[name], Is.EqualTo(number));
        }

        private static void AssertSingleNarrowingWithImmutableInput(
            JObject original, string owner, string name, string label, uint expectedBits)
        {
            var text = original.ToString(Formatting.None);
            var nodes = original.DescendantsAndSelf().ToArray();
            var parents = nodes.Select(node => node.Parent).ToArray();
            object backing = ((JValue)original[name]).Value;
            try
            {
                // Source-backed grammar transcribed narrowly for this numeric leaf only;
                // this is not another whole historical validator or acceptance oracle.
                Assert.That(original[name].Type == JTokenType.Float || original[name].Type == JTokenType.Integer, Is.True);
                double numeric = (double)original[name];
                Assert.That(double.IsNaN(numeric) || double.IsInfinity(numeric), Is.False);
                FrozenV28Shape.CheckComponent(original, owner);
                object snapshot;
                try { snapshot = FrozenV28Input.HydrateComponentShapeOnly(original, owner); }
                catch (Exception error)
                {
                    TestContext.Progress.WriteLine("LOCAL_ONLY_SINGLE_REFUSAL; needsNativeComparison=true; "
                        + owner + "." + name + "; case=" + label + "; refusal=" + error.GetType().FullName);
                    throw;
                }
                var field = snapshot.GetType().GetField(name);
                Assert.That(field.FieldType, Is.EqualTo(typeof(float)));
                var actual = (float)field.GetValue(snapshot);
                uint actualBits = unchecked((uint)BitConverter.SingleToInt32Bits(actual));
                TestContext.Progress.WriteLine("LOCAL_ONLY_SINGLE_OBSERVATION; needsNativeComparison=true; "
                    + owner + "." + name + "; case=" + label
                    + "; inputDoubleBits=" + unchecked((ulong)BitConverter.DoubleToInt64Bits(numeric)).ToString("X16", CultureInfo.InvariantCulture)
                    + "; expectedSingleBits=" + expectedBits.ToString("X8", CultureInfo.InvariantCulture)
                    + "; actualSingleBits=" + actualBits.ToString("X8", CultureInfo.InvariantCulture));
                Assert.That(actualBits, Is.EqualTo(expectedBits),
                    "Local net10/Newtonsoft API expectation only; no historical CLR/native parity is asserted.");
            }
            finally
            {
                Assert.That(original.ToString(Formatting.None), Is.EqualTo(text));
                var after = original.DescendantsAndSelf().ToArray();
                Assert.That(after.Length, Is.EqualTo(nodes.Length));
                for (int i = 0; i < nodes.Length; i++)
                {
                    Assert.That(ReferenceEquals(after[i], nodes[i]), Is.True);
                    Assert.That(ReferenceEquals(after[i].Parent, parents[i]), Is.True);
                }
                Assert.That(ReferenceEquals(((JValue)original[name]).Value, backing), Is.True,
                    "The numeric token backing object was replaced.");
            }
        }

        [Test]
        public void UnknownComponentNameCannotResolveAnotherAssemblyOrType()
        {
            Assert.Throws<InvalidDataException>(() => FrozenV28Types.Resolve("Gamesim.Simulation.EpisodeState"));
            Assert.Throws<InvalidDataException>(() => FrozenV28Types.Resolve("System.Object"));
            Assert.Throws<InvalidDataException>(() => FrozenV28Types.Resolve(null));
            Assert.Throws<InvalidDataException>(() => FrozenV28Shape.CheckComponent(JValue.CreateNull(), "notDeclared"));
        }
    }
}
