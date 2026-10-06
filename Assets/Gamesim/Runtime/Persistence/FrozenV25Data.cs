// Literal data-only schema25 carrier at f4566b69afc10c1008d64e112c12ed39241caa36.
// These types deliberately never use current simulation DTOs, methods, enums or constructors.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Persistence.Frozen25Data
{
#pragma warning disable 0649
    internal enum EpisodePhase { Social = 0, HoH = 1, Nomination = 2, VetoSelection = 3, Veto = 4, VetoMeeting = 5, Campaign = 6, Eviction = 7, FinalHoHPart1 = 8, FinalHoHPart2 = 9, FinalHoHPart3 = 10, FinalEviction = 11, Jury = 12, Finished = 13, JuryQuestioning = 14, FinalSpeeches = 15 }
    internal enum EvictionStage { Interaction = 0, Speeches = 1, Voting = 2, Tiebreaker = 3, Results = 4 }
    internal enum ContestantStatus { Active = 0, Evicted = 1, Jury = 2, Winner = 3, RunnerUp = 4, Expelled = 5 }
    internal enum PromiseKind { Safety = 0, Vote = 1, FinalTwo = 2, AllianceLoyalty = 3, Information = 4 }
    internal enum PromiseStatus { Active = 0, Fulfilled = 1, Broken = 2, Expired = 3 }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class EpisodeState
    {
        public int schemaVersion;
        public int competitionRulesVersion;
        public string sessionId;
        public uint seed;
        public uint randomState;
        public int revision;
        public int week;
        public int nextSequence;
        public int socialActions;
        public EpisodePhase phase;
        public string playerId;
        public string hohId;
        public string previousHohId;
        public string vetoHolderId;
        public string winnerId;
        public string runnerUpId;
        public string finalPart1WinnerId;
        public string finalPart2WinnerId;
        public bool competitionResolved;
        public bool vetoResolved;
        public bool evictionResolved;
        public List<ContestantState> contestants;
        public List<RelationshipState> relationships;
        public List<PromiseState> promises;
        public List<AllianceState> alliances;
        public List<MemoryState> memories;
        public List<string> nominees;
        public List<string> vetoPlayers;
        public List<VoteState> votes;
        public SeasonLedger ledger;
        public int readRulesStartWeek;
        public int leverRulesStartWeek;
        public int weekRulesStartWeek;
        public List<int> windowActions;
        public int economyRulesVersion;
        public int moveInExtrasSpent;
        public int agencyRulesStartWeek;
        public int finaleRulesStartWeek;
        public int commitmentRulesStartWeek;
        public int unifiedCommitmentRulesVersion;
        public List<UnifiedCommitmentState> unifiedCommitments;
        public int unifiedHearingRulesVersion;
        public List<UnifiedHearingEvidenceState> unifiedHearingEvidence;
        public List<UnifiedHearingReceiptState> unifiedHearingReceipts;
        public List<CompetitionScore> competitionScores;
        public List<EpisodeEvent> events;
        public List<string> acceptedCommandIds;
        public List<JuryExchangeState> juryExchanges;
        public int juryQuestionIndex;
        public List<FinalSpeechState> finalSpeeches;
        public FinalArgumentState finalArgument;
        public List<RelationshipArcState> relationshipArcs;
        public WebPersonaState playerPersona;
        public WebJurySentimentState jurySentiment;
        public DiaryPromptState pendingDiary;
        public int lastDiaryRoomWeek;
        public int phaseEventSocialBonus;
        public int phaseEventCompBonus;
        public List<string> resolvedDiaryIds;
        public List<WebOathRecord> loyaltyOaths;
        public List<string> oathOpportunities;
        public List<string> shownOathMilestones;
        public int playerStudyBonus;
        public int blocRulesStartWeek;
        public NpcSocialState npcSocial;
        public EvictionStage evictionStage;
        public List<EvictionSpeechState> evictionSpeeches;
        public string backdoorTargetId;
        public int outOfPhaseSocialActions;
        public List<string> openingBeatsSeen;
        public int socialBudgetRulesStartWeek;
        public List<DealState> deals;
        public int dealRulesStartWeek;
        public int boughtActionPoints;
        public List<HouseEventState> houseEvents;
        public int eventRulesStartWeek;
        public List<StorylineState> storylines;
        public List<StoryModifierState> activeModifiers;
        public int storyRulesStartWeek;
        public int haveNotRulesStartWeek;
        public List<string> haveNots;
        public List<string> haveNotPasses;
        public List<string> punishedHaveNots;
        public List<VetoPrizeState> vetoPrizes;
        public int strategyRulesStartWeek;
        public List<LobbyState> lobbies;
        public List<ReplyCardState> replyCards;
        public StoryWorldState story;
        public IEnumerable<ContestantState> Active => contestants.Where(c => c.status == ContestantStatus.Active);
        public ContestantState Find(string id) => contestants.FirstOrDefault(c => c.id == id);
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class ContestantState
    {
        public string id;
        public string name;
        public string pronouns;
        public string motive;
        public string homeRoom;
        public string occupation;
        public string archetype;
        public string hometown;
        public string bio;
        public string sourceTemplateId;
        public CharacterAppearance appearance;
        public int age;
        public string mood;
        public string stressLevel;
        public bool isPlayer;
        public ContestantStatus status;
        public ContestantStats stats;
        public List<string> traits;
        public int hohWins;
        public int vetoWins;
        public int timesNominated;
        public List<int> nominationWeeks;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class RelationshipState
    {
        public string fromId;
        public string toId;
        public double score;
        public int lastInteractionWeek;
        public List<string> notes;
        public List<RelationshipEventState> events;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class PromiseState
    {
        public string id;
        public string fromId;
        public string toId;
        public string targetId;
        public PromiseKind kind;
        public PromiseStatus status;
        public int week;
        public int expiresWeek;
        public string impact;
        public string brokenById;
        public int settledWeek;
        public PromiseState Clone() => (PromiseState)MemberwiseClone();
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class AllianceState
    {
        public string id;
        public string name;
        public List<string> members;
        public bool active;
        public bool playerJoined;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class MemoryState
    {
        public string ownerId;
        public string subjectId;
        public string text;
        public int week;
        public bool isPrivate;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class VoteState
    {
        public string voterId;
        public string targetId;
        public string reason;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class SeasonLedger
    {
        public List<OpportunityRow> opportunities = new List<OpportunityRow>();
        public List<CompetitionRow> competitions = new List<CompetitionRow>();
        public List<PowerRow> power = new List<PowerRow>();
        public List<BallotRow> ballots = new List<BallotRow>();
        public List<ClaimRow> claims = new List<ClaimRow>();
        public List<AllianceRow> alliances = new List<AllianceRow>();
        public List<StandingRow> standings = new List<StandingRow>();
        public List<ReplyRow> replies = new List<ReplyRow>();
        public List<BlocCallRow> calls = new List<BlocCallRow>();
        public int dropped;
        public const int MostRows = 512;
    }

    // UnifiedCommitments.cs SHA256 ae53fa7fb6aca525bae8ed67bdb984b43cbec3fe33143204b65674f5d4e8627c.
    [Serializable] internal sealed class UnifiedCommitmentState
    {
        public string id;
        public string kind;
        public string sourcePolicy;
        public string origin;
        public string makerId;
        public string beneficiaryId;
        public bool reciprocal;
        public int createdWeek;
        public int expiresWeek;
        public string status;
        public int settledWeek;
        public string brokenById;
        public string trustImpact;
        public string linkedCommitmentId;
        public string settlementEffectKey;
        public UnifiedCommitmentState Clone() => (UnifiedCommitmentState)MemberwiseClone();
    }

    // UnifiedCommitmentHearings.cs SHA256 56f9696c56f416c65d0ed18db379e611d47db6ba7d9e1599247d1a2d551dc346.
    [Serializable] internal sealed class UnifiedHearingEvidenceState
    {
        public string incidentKey;
        public HouseFactState fact;
    }

    // UnifiedCommitmentHearings.cs SHA256 56f9696c56f416c65d0ed18db379e611d47db6ba7d9e1599247d1a2d551dc346.
    [Serializable] internal sealed class UnifiedHearingReceiptState
    {
        public string incidentKey;
        public string listenerId;
        public string factId;
        public string kind;
        public int heardWeek;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class CompetitionScore
    {
        public string contestantId;
        public double score;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class EpisodeEvent
    {
        public int sequence;
        public int week;
        public EpisodePhase phase;
        public string kind;
        public string text;
        public List<string> audienceIds;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class JuryExchangeState
    {
        public string questionerId;
        public string finalistId;
        public string tone;
        public string question;
        public string optionA;
        public string optionB;
        public string correctChoice;
        public string answerChoice;
        public string answer;
        public string opponentAnswer;
        public string category;
        public string receiptKind;
        public string receiptId;
        public bool completed;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class FinalSpeechState
    {
        public string speakerId;
        public string text;
        public bool isPlayerAuthored;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class FinalArgumentState
    {
        public string theme;
        public List<string> momentRefs;
    }

    // WebRelationshipArcs.cs SHA256 2c76c0a618bd6ab8fc23b6e97777fb7d4c1ec98d9f8e8bb3dd98b1c1f34d1bb4.
    [Serializable] internal sealed class RelationshipArcState
    {
        public string npcId;
        public string npcName;
        public string arcType;
        public double intensity;
        public int escalationLevel;
        public List<ArcHistory> weeklyHistory;
    }

    // WebDiaryRoom.cs SHA256 41ae1d0436a224a44d2a4473c9a3d3d1c22a1a09abb15d7ece89d64151a563b1.
    [Serializable] internal sealed class WebPersonaState
    {
        public string current;
        public List<WebPersonaScore> scores;
        public List<WebPersonaHistory> history;
    }

    // WebJurySentiment.cs SHA256 b199d162ef46aeb00be927bcfcd4ffc1d89877888a35c8e03cd8cb7f9d269223.
    [Serializable] internal sealed class WebJurySentimentState
    {
        public List<WebJurorSentiment> jurors;
        public double overallSentiment;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class DiaryPromptState
    {
        public string id;
        public string trigger;
        public string evictedId;
        public int week;
        public bool isNominee;
    }

    // WebLoyaltyOaths.cs SHA256 58adf12b0623deeb32f177b0b108e6732eef9f76e08397201fcd434d3b06a1ef.
    [Serializable] internal sealed class WebOathRecord
    {
        public string playerId;
        public string targetId;
        public int week;
        public long timestamp;
    }

    // NpcSocialState.cs SHA256 9f6f9dcca0ee289cb4b3404532740c3308b66916fb75c556e1e11d3e8b83d454.
    [Serializable] internal sealed class NpcSocialState
    {
        public int rulesStartWeek;
        public long clockTick;
        public long nextScanTick;
        public long nextConversationSequence;
        public uint randomState;
        public List<NpcConversationState> pending;
        public List<NpcCooldownState> cooldowns;
        public List<NpcPairMemoryState> pairMemory;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class EvictionSpeechState
    {
        public string speakerId;
        public string text;
        public int week;
        public bool isPlayerAuthored;
    }

    // DealState.cs SHA256 cf7239c522e55e47bbde1747da472e549a3fd54568d65047b15f598b155baeea.
    [Serializable] internal sealed class DealState
    {
        public string id;
        public string type;
        public string proposerId;
        public string recipientId;
        public string targetId;
        public string status;
        public int week;
        public int expiresWeek;
        public string trustImpact;
        public string brokenById;
        public int settledWeek;
        public string linkedDealId;
        public DealState Clone() => (DealState)MemberwiseClone();
    }

    // HouseEventState.cs SHA256 9a98d41358f1fdd72fc66ecfb9019bb7765eddb20d8a096cd3fa2da099618b28.
    [Serializable] internal sealed class HouseEventState
    {
        public string id;
        public string kind;
        public string title;
        public string narrative;
        public List<string> involvedIds;
        public List<HouseEventChoice> choices;
        public int week;
        public bool resolved;
        public int chosenIndex;
        public string outcome;
        public string contentId;
        public string cycleId;
        public string closesAnchor;
        public string surface;
        public string venue;
        public string lapseOptionId;
        public List<StoryRoleState> cast;

        // Frozen f456 read-only getter; HouseEventKind.Story was the literal "story".
        public bool IsStory => kind == "story";
    }

    // StorylineState.cs SHA256 0e72d761fd93cdfe30ed1adbfb94152eb1e3e5330e522daf5fdddffd25748029.
    [Serializable] internal sealed class StorylineState
    {
        public string id;
        public string templateId;
        public string category;
        public string title;
        public string eventId;
        public string status;
        public int week;
        public int endedWeek;
        public string beatId;
        public string lane;
        public int variant;
        public List<StoryRoleState> cast;
        public List<StoryStepState> path;
        public List<StoryVarState> vars;
        public int nextWeek;
        public string nextAnchor;
        public string endingId;
    }

    // StorylineState.cs SHA256 0e72d761fd93cdfe30ed1adbfb94152eb1e3e5330e522daf5fdddffd25748029.
    [Serializable] internal sealed class StoryModifierState
    {
        public string id;
        public string name;
        public string description;
        public int weeksLeft;
        public double competitionBonus;
        public double socialBonus;
        public string ownerId;
    }

    // HaveNots.cs SHA256 9655af805c22c5d2fa48489ff6c9bde3b14207859dec961c8efaa3f830e99fb8.
    [Serializable] internal sealed class VetoPrizeState
    {
        public int week;
        public string contestantId;
        public string prizeId;
    }

    // StrategyRules.cs SHA256 8c21aaf54828a18d455db3013b715934bfdb7b1f1b04a7e99be3c785afedd3a1.
    [Serializable] internal sealed class LobbyState
    {
        public int week;
        public EpisodePhase phase;
        public string deciderId;
        public string ask;
        public string subjectId;
        public string approach;
        public string response;
        public double influence;
    }

    // ReplyCards.cs SHA256 0211d93df912973cd6550887ab9b72910cbb089995b6662d67daa7698b6795a9.
    [Serializable] internal sealed class ReplyCardState
    {
        public string id;
        public int week;
        public string kind;
        public string fromId;
        public string aboutId;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class StoryWorldState
    {
        public int rulesStartWeek;
        public int rulesVersion;
        public int productionStrictness;
        public bool romanceStorylines;
        public List<GrudgeState> grudges;
        public List<HouseFactState> facts;
        public List<BondState> bonds;
        public List<HookState> hooks;
        public List<ContactState> contacts;
        public List<LoreCastState> lore;
        public List<string> knownFacts;
        public List<ConductState> conduct;
        public List<RemovalState> removals;
        public string pendingRemovalId;
        public List<StoryCooldownState> cooldowns;
        public List<ReckoningState> reckonings;
    }

    // CharacterAppearance.cs SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] internal sealed class CharacterAppearance
    {
        public int version;
        public string provider;
        public string presetId;
        public string bodyId;
        public string fallbackId;
        public string activeOutfit;
        public List<AppearanceValue> dna;
        public List<AppearanceColor> colors;
        public List<CharacterOutfit> outfits;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class ContestantStats
    {
        public double physical;
        public double mental;
        public double endurance;
        public double social;
        public double luck;
        public double competition;
        public double strategic;
        public double loyalty;
    }

    // EpisodeState.cs SHA256 e0582a57238e9705cd069ae2cf54ffc64c2e80a003240cf64f9d8b8435702901.
    [Serializable] internal sealed class RelationshipEventState
    {
        public int sequence;
        public int week;
        public string type;
        public string description;
        public double impactScore;
        public bool decayable;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class OpportunityRow
    {
        public string id;
        public string kind;
        public string anchor;
        public string source;
        public string response;
        public string outcome;
        public string currency;
        public string note;
        public int week;
        public double payoff;
        public List<OpportunityStep> steps;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class CompetitionRow
    {
        public int week;
        public int field;
        public int placement;
        public string kind;
        public string entry;
        public double performance;
        public double expectedWin;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class PowerRow
    {
        public int week;
        public string hohId;
        public string vetoHolderId;
        public string savedId;
        public string replacementId;
        public string evicteeId;
        public string backdoorTargetId;
        public string backdoorResult;
        public bool vetoUsed;
        public List<string> nominees;
        public List<int> tally;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class BallotRow
    {
        public int week;
        public string voterId;
        public string targetId;
        public string readBefore;
        public bool correct;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class ClaimRow
    {
        public int week;
        public string voterId;
        public string targetId;
        public string source;
        public string status;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class AllianceRow
    {
        public string id;
        public string why;
        public int startedWeek;
        public int endedWeek;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class StandingRow
    {
        public int week;
        public string fromId;
        public string toId;
        public string source;
        public double score;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class ReplyRow
    {
        public int week;
        public string cardId;
        public string kind;
        public string fromId;
        public string listenerId;
        public string replyKey;
        public bool promised;
        public double toThem;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class BlocCallRow
    {
        public int week;
        public string allianceId;
        public string callerId;
        public string targetId;
        public List<string> followed;
        public List<string> defected;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class HouseFactState
    {
        public string id;
        public string kind;
        public string actorId;
        public string subjectId;
        public string refId;
        public string visibility;
        public int week;
        public List<string> knowers;
    }

    // WebRelationshipArcs.cs SHA256 2c76c0a618bd6ab8fc23b6e97777fb7d4c1ec98d9f8e8bb3dd98b1c1f34d1bb4.
    [Serializable] internal sealed class ArcHistory
    {
        public int week;
        public double delta;
        public string reason;
    }

    // WebDiaryRoom.cs SHA256 41ae1d0436a224a44d2a4473c9a3d3d1c22a1a09abb15d7ece89d64151a563b1.
    [Serializable] internal sealed class WebPersonaScore
    {
        public string persona;
        public int score;
    }

    // WebDiaryRoom.cs SHA256 41ae1d0436a224a44d2a4473c9a3d3d1c22a1a09abb15d7ece89d64151a563b1.
    [Serializable] internal sealed class WebPersonaHistory
    {
        public string persona;
        public int week;
    }

    // WebJurySentiment.cs SHA256 b199d162ef46aeb00be927bcfcd4ffc1d89877888a35c8e03cd8cb7f9d269223.
    [Serializable] internal sealed class WebJurorSentiment
    {
        public string jurorId;
        public string jurorName;
        public double sentiment;
        public List<WebJurySentimentEvent> events;
    }

    // NpcSocialState.cs SHA256 9f6f9dcca0ee289cb4b3404532740c3308b66916fb75c556e1e11d3e8b83d454.
    [Serializable] internal sealed class NpcConversationState
    {
        public long sequence;
        public long startedTick;
        public string firstId;
        public string secondId;
        public string topic;
        public string rendezvousId;
        public int week;
        public EpisodePhase phase;
        public double durationMs;
    }

    // NpcSocialState.cs SHA256 9f6f9dcca0ee289cb4b3404532740c3308b66916fb75c556e1e11d3e8b83d454.
    [Serializable] internal sealed class NpcCooldownState
    {
        public string npcId;
        public long untilTick;
    }

    // NpcSocialState.cs SHA256 9f6f9dcca0ee289cb4b3404532740c3308b66916fb75c556e1e11d3e8b83d454.
    [Serializable] internal sealed class NpcPairMemoryState
    {
        public string fromId;
        public string toId;
        public string lastTopic;
        public int count;
        public long lastStartTick;
    }

    // HouseEventState.cs SHA256 9a98d41358f1fdd72fc66ecfb9019bb7765eddb20d8a096cd3fa2da099618b28.
    [Serializable] internal sealed class HouseEventChoice
    {
        public string label;
        public string description;
        public string risk;
        public List<HouseEventImpact> impacts;
        public double trustChange;
        public string optionId;
        public string glyph;
        public string approach;
        public double checkBase;
        public string subjectId;
        public bool lapse;
        public bool costsAction;
        public bool conduct;
        public bool pickPerson;
        public List<string> eligibleIds;
        public bool locked;
        public string lockReason;
        public List<string> bonusTraits;
        public List<string> against;
        public List<StoryEffectState> effects;
        public List<StoryEffectState> backfire;
        public string next;
        public string nextOnBackfire;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class StoryRoleState
    {
        public string role;
        public string contestantId;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class StoryStepState
    {
        public string beatId;
        public string optionId;
        public string result;
        public int week;
        public int logSequence;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class StoryVarState
    {
        public string key;
        public int value;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class GrudgeState
    {
        public string holderId;
        public string targetId;
        public string cause;
        public double severity;
        public int originWeek;
        public int count;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class BondState
    {
        public string id;
        public string kind;
        public string aId;
        public string bId;
        public string status;
        public string cycleId;
        public int sinceWeek;
        public int endedWeek;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class HookState
    {
        public string id;
        public string holderId;
        public string overId;
        public string factId;
        public int week;
        public bool spent;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class ContactState
    {
        public string npcId;
        public int rapport;
        public int weekCount;
        public int lastWeek;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class LoreCastState
    {
        public string contestantId;
        public string sheetKey;
        public string source;
        public List<string> factIds;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class ConductState
    {
        public string contestantId;
        public int strikes;
        public int lastStrikeWeek;
        public int cleanWeeks;
        public int sitsOutWeek;
        public int pushedBack;
        public List<string> reasons;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class RemovalState
    {
        public string contestantId;
        public string reasonId;
        public int week;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class StoryCooldownState
    {
        public string key;
        public int untilWeek;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class ReckoningState
    {
        public string npcId;
        public string cause;
        public bool betrayerIsPlayer;
        public int week;
    }

    // CharacterAppearance.cs SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] internal sealed class AppearanceValue
    {
        public string id;
        public float value;
    }

    // CharacterAppearance.cs SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] internal sealed class AppearanceColor
    {
        public string id;
        public float r;
        public float g;
        public float b;
        public float a;
    }

    // CharacterAppearance.cs SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] internal sealed class CharacterOutfit
    {
        public string id;
        public List<AppearanceWardrobe> wardrobe;
        public List<AppearanceColor> colors;
    }

    // SeasonLedger.cs SHA256 1b67d0582e3b69a43b29c07ca823f19a35781ac0cd0fadd365c3e3b1d393c1d0.
    [Serializable] internal sealed class OpportunityStep
    {
        public string at;
        public string choice;
        public bool matchedRead;
    }

    // WebJurySentiment.cs SHA256 b199d162ef46aeb00be927bcfcd4ffc1d89877888a35c8e03cd8cb7f9d269223.
    [Serializable] internal sealed class WebJurySentimentEvent
    {
        public int week;
        public double delta;
        public string reason;
    }

    // HouseEventState.cs SHA256 9a98d41358f1fdd72fc66ecfb9019bb7765eddb20d8a096cd3fa2da099618b28.
    [Serializable] internal sealed class HouseEventImpact
    {
        public string targetId;
        public double amount;
    }

    // StoryState.cs SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] internal sealed class StoryEffectState
    {
        public string kind;
        public string fromId;
        public string toId;
        public string thirdId;
        public string type;
        public string text;
        public double amount;
        public int weeks;
    }

    // CharacterAppearance.cs SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] internal sealed class AppearanceWardrobe
    {
        public string slot;
        public string itemId;
    }

    internal sealed class WebJuryResponsePair { public string optionA, optionB, correctIs, trait; }
    internal sealed class WebJuryQuestion { public string tone, question, optionA, optionB, correctIs, trait, opponentAnswer; }
    internal sealed class WebJuryQuestionOption { public string tone, text; }
    internal sealed class UnifiedCommitmentIncident
    {
        public readonly string EffectKey, ActorId, WrongedId, EffectOwnerId;
        public readonly double SourceConsequence;
        public readonly IReadOnlyList<string> EvidenceIds;
        public UnifiedCommitmentIncident(string key, string actor, string wronged, string owner, double consequence, IEnumerable<string> ids)
        { EffectKey = key; ActorId = actor; WrongedId = wronged; EffectOwnerId = owner; SourceConsequence = consequence;
          EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()); }
    }
    internal sealed class UnifiedCommitmentFulfillment
    {
        public readonly int SettledWeek;
        public readonly string FirstId, SecondId, EffectOwnerId;
        public readonly IReadOnlyList<string> EvidenceIds;
        public UnifiedCommitmentFulfillment(int week, string first, string second, string owner, IEnumerable<string> ids)
        { SettledWeek = week; FirstId = first; SecondId = second; EffectOwnerId = owner;
          EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()); }
    }
#pragma warning restore 0649
}
