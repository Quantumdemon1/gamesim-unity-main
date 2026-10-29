// Frozen29 (SCH30): the schema-29 whole reader, carried from FrozenV28Data.cs and then by exactly the live
// validators' changes from 515246bf to b208d46d (ALL-STAR-HISTORIES-PLAN 8.3). Provenance comments below
// describe the schema-28 image this copy started from.
// Fixed data-only schema29: Frozen28's HEAD515246bf/status61 snapshot plus schema 29's storage as b208d46d
// declares it (dialogueRulesVersion, dialogue and conditionalVeto, appended in their declaration order).
// No current DTOs, factories, getters, methods or field initializers are used.
// Every omitted payload field is rejected before local hydration; CLR defaults never repair input.
using System;
using System.Collections.Generic;

namespace Gamesim.Persistence.Frozen29Data
{
    public enum ContestantStatus { Active = 0, Evicted = 1, Jury = 2, Winner = 3, RunnerUp = 4, Expelled = 5 }
    public enum EpisodePhase { Social = 0, HoH = 1, Nomination = 2, VetoSelection = 3, Veto = 4, VetoMeeting = 5, Campaign = 6, Eviction = 7, FinalHoHPart1 = 8, FinalHoHPart2 = 9, FinalHoHPart3 = 10, FinalEviction = 11, Jury = 12, Finished = 13, JuryQuestioning = 14, FinalSpeeches = 15 }
    public enum EvictionStage { Interaction = 0, Speeches = 1, Voting = 2, Tiebreaker = 3, Results = 4 }
    public enum PromiseKind { Safety = 0, Vote = 1, FinalTwo = 2, AllianceLoyalty = 3, Information = 4 }
    public enum PromiseStatus { Active = 0, Fulfilled = 1, Broken = 2, Expired = 3 }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:144 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class AllianceRow
    {
        public string id;
        public string why;
        public int startedWeek;
        public int endedWeek;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:185 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class AllianceState
    {
        public string id;
        public string name;
        public List<string> members;
        public bool active;
        public bool playerJoined;
    }

    // Assets/Gamesim/Simulation/CharacterAppearance.cs:98 SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] public sealed class AppearanceColor
    {
        public string id;
        public float r;
        public float g;
        public float b;
        public float a;
    }

    // Assets/Gamesim/Simulation/CharacterAppearance.cs:96 SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] public sealed class AppearanceValue
    {
        public string id;
        public float value;
    }

    // Assets/Gamesim/Simulation/CharacterAppearance.cs:100 SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] public sealed class AppearanceWardrobe
    {
        public string slot;
        public string itemId;
    }

    // Assets/Gamesim/Simulation/WebRelationshipArcs.cs:7 SHA256 2c76c0a618bd6ab8fc23b6e97777fb7d4c1ec98d9f8e8bb3dd98b1c1f34d1bb4.
    [Serializable] public sealed class ArcHistory
    {
        public int week;
        public double delta;
        public string reason;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:122 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class BallotRow
    {
        public int week;
        public string voterId;
        public string targetId;
        public string readBefore;
        public bool correct;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:184 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class BlocCallRow
    {
        public int week;
        public string allianceId;
        public string callerId;
        public string targetId;
        public List<string> followed;
        public List<string> defected;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:131 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class BondState
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

    // Assets/Gamesim/Simulation/CharacterAppearance.cs:12 SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] public sealed class CharacterAppearance
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

    // Assets/Gamesim/Simulation/CharacterAppearance.cs:102 SHA256 facbb3ff6d4be6084ab0845a12f7febcabb719b2e9a31c18200658cec6675a27.
    [Serializable] public sealed class CharacterOutfit
    {
        public string id;
        public List<AppearanceWardrobe> wardrobe;
        public List<AppearanceColor> colors;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:135 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class ClaimRow
    {
        public int week;
        public string voterId;
        public string targetId;
        public string source;
        public string status;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:94 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class CompetitionRow
    {
        public int week;
        public int field;
        public int placement;
        public string kind;
        public string entry;
        public double performance;
        public double expectedWin;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:223 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class CompetitionScore
    {
        public string contestantId;
        public double score;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:183 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class ConditionalVetoResolutionState
    {
        public string reason;
        public string departedId;
        public string departedStatus;
        public int revision;
        public int sequence;
        public int vetoOccasionWeek;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:168 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class ConditionalVetoTermsState
    {
        public string statementId;
        public string consentEventId;
        public int consentPathIndex;
        public int consentRevision;
        public ConditionalVetoResolutionState resolution;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:173 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class ConductState
    {
        public string contestantId;
        public int strikes;
        public int lastStrikeWeek;
        public int cleanWeeks;
        public int sitsOutWeek;
        public int pushedBack;
        public List<string> reasons;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:150 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class ContactState
    {
        public string npcId;
        public int rapport;
        public int weekCount;
        public int lastWeek;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:46 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class ContestantState
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

    // Assets/Gamesim/Simulation/EpisodeState.cs:38 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class ContestantStats
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

    // Assets/Gamesim/Simulation/DealState.cs:20 SHA256 cf7239c522e55e47bbde1747da472e549a3fd54568d65047b15f598b155baeea.
    [Serializable] public sealed class DealState
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
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:54 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueEntryReceiptState
    {
        public string kind;
        public string grantId;
        public int revision;
        public int windowActionBefore;
        public int windowActionAfter;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:30 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueEpisodeState
    {
        public string id;
        public string npcId;
        public string occasionKind;
        public string occasionId;
        public string entryEventId;
        public int contentVersion;
        public int week;
        public int window;
        public int startedRevision;
        public int startedSequence;
        public EpisodePhase startedPhase;
        public DialogueEntryReceiptState entry;
        public List<DialogueResponseState> responses;
        public List<DialogueEvidenceState> evidence;
        public DialogueStatementState statement;
        public DialogueTimeReceiptState time;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:70 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueEvidenceState
    {
        public string id;
        public string kind;
        public string eventId;
        public int pathIndex;
        public int capturedRevision;
        public PowerRow power;
        public MemoryState memory;
        public string statementId;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:62 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueResponseState
    {
        public string eventId;
        public string speakerId;
        public string responseKey;
        public int pathIndex;
        public int revision;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:103 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueRoleGrantState
    {
        public string id;
        public string ownerId;
        public string role;
        public string sourceKind;
        public string status;
        public string consumedEpisodeId;
        public string expiryReason;
        public int week;
        public int sourceRevision;
        public int sourceSequence;
        public int consumedRevision;
        public int expiredRevision;
        public EpisodePhase sourcePhase;
        public List<string> sourceNominees;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:13 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueState
    {
        public List<DialogueEpisodeState> episodes;
        public List<DialogueRoleGrantState> grants;
        public List<DialogueVetoOccasionState> vetoOccasions;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:88 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueStatementState
    {
        public string id;
        public string eventId;
        public string speakerId;
        public string targetId;
        public string answer;
        public string conditionKind;
        public int pathIndex;
        public int week;
        public int revision;
        public int sequence;
        public List<string> audienceIds;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:119 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueTimeReceiptState
    {
        public int revision;
        public int sequence;
        public int window;
        public int proposedDelta;
        public int appliedDelta;
        public string reason;
        public int spentRevision;
        public int spentSequence;
        public int spentWindowAction;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:148 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueVetoDecisionState
    {
        public int revision;
        public int sequence;
        public List<string> activeIds;
        public List<string> nominees;
        public List<string> replacementCandidates;
        public bool used;
        public string savedId;
        public string replacementId;
    }

    // Assets/Gamesim/Simulation/DialogueState.cs:129 SHA256 ff95835ef4c5460e1edc9bc7d05e83fd120804f3ce7b8bf790a7722058dca99f (b208d46d, schema 29).
    [Serializable] public sealed class DialogueVetoOccasionState
    {
        public int week;
        public int resultRevision;
        public int resultSequence;
        public string hohId;
        public string holderId;
        public List<string> participants;
        public List<string> nomineesAtResult;
        public DialogueVetoDecisionState decision;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:157 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class DiaryPromptState
    {
        public string id;
        public string trigger;
        public string evictedId;
        public int week;
        public bool isNominee;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:231 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class EpisodeEvent
    {
        public int sequence;
        public int week;
        public EpisodePhase phase;
        public string kind;
        public string text;
        public List<string> audienceIds;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:245 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class EpisodeState
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
        public List<UnifiedVoteRevealState> unifiedVoteReveals;
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
        public int allianceLeakRulesStartWeek;
        public int pactPlanRulesStartWeek;
        public int allWeekRulesStartWeek;
        // Schema 29 (9d87b59e; Assets/Gamesim/Simulation/EpisodeState.cs SHA256 3708eb23baba000b4b5586a27fb3c18905a0ccbd251453365d676e00c7c4716f at b208d46d).
        public int dialogueRulesVersion;
        public DialogueState dialogue;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:124 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class EvictionSpeechState
    {
        public string speakerId;
        public string text;
        public int week;
        public bool isPlayerAuthored;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:145 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class FinalArgumentState
    {
        public string theme;
        public List<string> momentRefs;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:132 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class FinalSpeechState
    {
        public string speakerId;
        public string text;
        public bool isPlayerAuthored;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:106 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class GrudgeState
    {
        public string holderId;
        public string targetId;
        public string cause;
        public double severity;
        public int originWeek;
        public int count;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:140 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class HookState
    {
        public string id;
        public string holderId;
        public string overId;
        public string factId;
        public int week;
        public bool spent;
    }

    // Assets/Gamesim/Simulation/HouseEventState.cs:83 SHA256 9a98d41358f1fdd72fc66ecfb9019bb7765eddb20d8a096cd3fa2da099618b28.
    [Serializable] public sealed class HouseEventChoice
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

    // Assets/Gamesim/Simulation/HouseEventState.cs:169 SHA256 9a98d41358f1fdd72fc66ecfb9019bb7765eddb20d8a096cd3fa2da099618b28.
    [Serializable] public sealed class HouseEventImpact
    {
        public string targetId;
        public double amount;
    }

    // Assets/Gamesim/Simulation/HouseEventState.cs:20 SHA256 9a98d41358f1fdd72fc66ecfb9019bb7765eddb20d8a096cd3fa2da099618b28.
    [Serializable] public sealed class HouseEventState
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
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:116 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class HouseFactState
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

    // Assets/Gamesim/Simulation/EpisodeState.cs:102 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class JuryExchangeState
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

    // Assets/Gamesim/Simulation/StrategyRules.cs:558 SHA256 39f50b702dd0412e1664d3eb68a9c693443edbd46b8bce5e61ecb0654b19b368.
    [Serializable] public sealed class LobbyState
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

    // Assets/Gamesim/Simulation/Story/StoryState.cs:159 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class LoreCastState
    {
        public string contestantId;
        public string sheetKey;
        public string source;
        public List<string> factIds;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:207 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class MemoryState
    {
        public string ownerId;
        public string subjectId;
        public string text;
        public int week;
        public bool isPrivate;
    }

    // Assets/Gamesim/Simulation/NpcSocialState.cs:89 SHA256 fcf32f32a200f4e0ca3bfb441d653f9b2c118c40c80f6603f59b74d5056cd757.
    [Serializable] public sealed class NpcActState
    {
        public string id;
        public string kind;
        public string actorId;
        public string partnerId;
        public string subjectId;
        public string room;
        public int week;
        public int window;
        public int firedTick;
        public bool sighted;
        public bool overheard;
    }

    // Assets/Gamesim/Simulation/NpcSocialState.cs:100 SHA256 fcf32f32a200f4e0ca3bfb441d653f9b2c118c40c80f6603f59b74d5056cd757.
    [Serializable] public sealed class NpcConversationState
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

    // Assets/Gamesim/Simulation/NpcSocialState.cs:111 SHA256 fcf32f32a200f4e0ca3bfb441d653f9b2c118c40c80f6603f59b74d5056cd757.
    [Serializable] public sealed class NpcCooldownState
    {
        public string npcId;
        public long untilTick;
    }

    // Assets/Gamesim/Simulation/NpcSocialState.cs:119 SHA256 fcf32f32a200f4e0ca3bfb441d653f9b2c118c40c80f6603f59b74d5056cd757.
    [Serializable] public sealed class NpcPairMemoryState
    {
        public string fromId;
        public string toId;
        public string lastTopic;
        public int count;
        public long lastStartTick;
    }

    // Assets/Gamesim/Simulation/NpcSocialState.cs:10 SHA256 fcf32f32a200f4e0ca3bfb441d653f9b2c118c40c80f6603f59b74d5056cd757.
    [Serializable] public sealed class NpcSocialState
    {
        public int rulesStartWeek;
        public long clockTick;
        public long nextScanTick;
        public long nextConversationSequence;
        public uint randomState;
        public List<NpcConversationState> pending;
        public List<NpcCooldownState> cooldowns;
        public List<NpcPairMemoryState> pairMemory;
        public int beatWeek;
        public int beatWindow;
        public int beatsFired;
        public int beatSeats;
        public List<string> beatPlan;
        public List<NpcActState> acts;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:69 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class OpportunityRow
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

    // Assets/Gamesim/Simulation/SeasonLedger.cs:85 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class OpportunityStep
    {
        public string at;
        public string choice;
        public bool matchedRead;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:204 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class PactPlanRow
    {
        public int week;
        public string allianceId;
        public string throughId;
        public string stance;
        public List<string> present;
        public List<string> cameRound;
        public List<string> followed;
        public List<PlanSay> says;
        public string counterId;
        public string targetId;
        public string callerId;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:228 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class PlanSay
    {
        public string memberId;
        public string targetId;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:104 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class PowerRow
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

    // Assets/Gamesim/Simulation/EpisodeState.cs:166 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class PromiseState
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
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:210 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class ReckoningState
    {
        public string npcId;
        public string cause;
        public bool betrayerIsPlayer;
        public int week;
    }

    // Assets/Gamesim/Simulation/WebRelationshipArcs.cs:15 SHA256 2c76c0a618bd6ab8fc23b6e97777fb7d4c1ec98d9f8e8bb3dd98b1c1f34d1bb4.
    [Serializable] public sealed class RelationshipArcState
    {
        public string npcId;
        public string npcName;
        public string arcType;
        public double intensity;
        public int escalationLevel;
        public List<ArcHistory> weeklyHistory;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:93 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class RelationshipEventState
    {
        public int sequence;
        public int week;
        public string type;
        public string description;
        public double impactScore;
        public bool decayable;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:78 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class RelationshipState
    {
        public string fromId;
        public string toId;
        public double score;
        public int lastInteractionWeek;
        public List<string> notes;
        public List<RelationshipEventState> events;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:192 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class RemovalState
    {
        public string contestantId;
        public string reasonId;
        public int week;
    }

    // Assets/Gamesim/Simulation/ReplyCards.cs:210 SHA256 0211d93df912973cd6550887ab9b72910cbb089995b6662d67daa7698b6795a9.
    [Serializable] public sealed class ReplyCardState
    {
        public string id;
        public int week;
        public string kind;
        public string fromId;
        public string aboutId;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:170 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class ReplyRow
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

    // Assets/Gamesim/Simulation/SeasonLedger.cs:17 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class SeasonLedger
    {
        public List<OpportunityRow> opportunities;
        public List<CompetitionRow> competitions;
        public List<PowerRow> power;
        public List<BallotRow> ballots;
        public List<ClaimRow> claims;
        public List<AllianceRow> alliances;
        public List<StandingRow> standings;
        public List<ReplyRow> replies;
        public List<BlocCallRow> calls;
        public int dropped;
        public List<PactPlanRow> plans;
    }

    // Assets/Gamesim/Simulation/SeasonLedger.cs:156 SHA256 793f896d9a099708f9104305a0efe57274730e5c562dc4b4216f69b083404fbc.
    [Serializable] public sealed class StandingRow
    {
        public int week;
        public string fromId;
        public string toId;
        public string source;
        public double score;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:201 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class StoryCooldownState
    {
        public string key;
        public int untilWeek;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:253 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class StoryEffectState
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

    // Assets/Gamesim/Simulation/StorylineState.cs:111 SHA256 0e72d761fd93cdfe30ed1adbfb94152eb1e3e5330e522daf5fdddffd25748029.
    [Serializable] public sealed class StoryModifierState
    {
        public string id;
        public string name;
        public string description;
        public int weeksLeft;
        public double competitionBonus;
        public double socialBonus;
        public string ownerId;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:220 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class StoryRoleState
    {
        public string role;
        public string contestantId;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:228 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class StoryStepState
    {
        public string beatId;
        public string optionId;
        public string result;
        public int week;
        public int logSequence;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:237 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class StoryVarState
    {
        public string key;
        public int value;
    }

    // Assets/Gamesim/Simulation/Story/StoryState.cs:24 SHA256 f019fccd659ef0b153701d34fedb543f9dec090eacc1c5497fa4557e34382642.
    [Serializable] public sealed class StoryWorldState
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

    // Assets/Gamesim/Simulation/StorylineState.cs:21 SHA256 0e72d761fd93cdfe30ed1adbfb94152eb1e3e5330e522daf5fdddffd25748029.
    [Serializable] public sealed class StorylineState
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

    // Assets/Gamesim/Simulation/UnifiedCommitments.cs:14 SHA256 100482c693ec0e9b75c9a2ee067f0ad75cdbe88fd9aaafe1f4c1b10cb52cd683.
    [Serializable] public sealed class UnifiedCommitmentState
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
        public string targetId;
        public string subtype;
        public int voteBindingWeek;
        public int voteFirstRevealWeek;
        // Schema 29 (Assets/Gamesim/Simulation/UnifiedCommitments.cs SHA256 331033adfcad94fcc84494ba98ddb593dc129c815910c52ac0b1034ac4ab06ba at b208d46d).
        public ConditionalVetoTermsState conditionalVeto;
    }

    // Assets/Gamesim/Simulation/UnifiedCommitmentHearings.cs:10 SHA256 09293fd821e4647ef0c5fcb901431636abe9a84a56bae9f892ea0c872a29c0d5.
    [Serializable] public sealed class UnifiedHearingEvidenceState
    {
        public string incidentKey;
        public HouseFactState fact;
    }

    // Assets/Gamesim/Simulation/UnifiedCommitmentHearings.cs:20 SHA256 09293fd821e4647ef0c5fcb901431636abe9a84a56bae9f892ea0c872a29c0d5.
    [Serializable] public sealed class UnifiedHearingReceiptState
    {
        public string incidentKey;
        public string listenerId;
        public string factId;
        public string kind;
        public int heardWeek;
    }

    // Assets/Gamesim/Simulation/UnifiedVoteRevealState.cs:28 SHA256 49627dc92693a9b130f7c2b6bf48313084f497f2fb1baf2981ae30458e290cb2.
    [Serializable] public sealed class UnifiedVoteBallotState
    {
        public string voterId;
        public string targetId;
    }

    // Assets/Gamesim/Simulation/UnifiedVoteRevealState.cs:12 SHA256 49627dc92693a9b130f7c2b6bf48313084f497f2fb1baf2981ae30458e290cb2.
    [Serializable] public sealed class UnifiedVoteRevealState
    {
        public int week;
        public List<UnifiedVoteBallotState> ballots;
    }

    // Assets/Gamesim/Simulation/HaveNots.cs:178 SHA256 9655af805c22c5d2fa48489ff6c9bde3b14207859dec961c8efaa3f830e99fb8.
    [Serializable] public sealed class VetoPrizeState
    {
        public int week;
        public string contestantId;
        public string prizeId;
    }

    // Assets/Gamesim/Simulation/EpisodeState.cs:216 SHA256 201953bf68b67a5c78141feb2a83f75cd5fa922f421deb6ef7d0d1d586b2476f.
    [Serializable] public sealed class VoteState
    {
        public string voterId;
        public string targetId;
        public string reason;
    }

    // Assets/Gamesim/Simulation/WebJurySentiment.cs:14 SHA256 b199d162ef46aeb00be927bcfcd4ffc1d89877888a35c8e03cd8cb7f9d269223.
    [Serializable] public sealed class WebJurorSentiment
    {
        public string jurorId;
        public string jurorName;
        public double sentiment;
        public List<WebJurySentimentEvent> events;
    }

    // Assets/Gamesim/Simulation/WebJurySentiment.cs:7 SHA256 b199d162ef46aeb00be927bcfcd4ffc1d89877888a35c8e03cd8cb7f9d269223.
    [Serializable] public sealed class WebJurySentimentEvent
    {
        public int week;
        public double delta;
        public string reason;
    }

    // Assets/Gamesim/Simulation/WebJurySentiment.cs:24 SHA256 b199d162ef46aeb00be927bcfcd4ffc1d89877888a35c8e03cd8cb7f9d269223.
    [Serializable] public sealed class WebJurySentimentState
    {
        public List<WebJurorSentiment> jurors;
        public double overallSentiment;
    }

    // Assets/Gamesim/Simulation/WebLoyaltyOaths.cs:8 SHA256 58adf12b0623deeb32f177b0b108e6732eef9f76e08397201fcd434d3b06a1ef.
    [Serializable] public sealed class WebOathRecord
    {
        public string playerId;
        public string targetId;
        public int week;
        public long timestamp;
    }

    // Assets/Gamesim/Simulation/WebDiaryRoom.cs:14 SHA256 41ae1d0436a224a44d2a4473c9a3d3d1c22a1a09abb15d7ece89d64151a563b1.
    [Serializable] public sealed class WebPersonaHistory
    {
        public string persona;
        public int week;
    }

    // Assets/Gamesim/Simulation/WebDiaryRoom.cs:8 SHA256 41ae1d0436a224a44d2a4473c9a3d3d1c22a1a09abb15d7ece89d64151a563b1.
    [Serializable] public sealed class WebPersonaScore
    {
        public string persona;
        public int score;
    }

    // Assets/Gamesim/Simulation/WebDiaryRoom.cs:20 SHA256 41ae1d0436a224a44d2a4473c9a3d3d1c22a1a09abb15d7ece89d64151a563b1.
    [Serializable] public sealed class WebPersonaState
    {
        public string current;
        public List<WebPersonaScore> scores;
        public List<WebPersonaHistory> history;
    }
}

namespace Gamesim.Persistence.Frozen29Components
{
    // Literal allowlist of this sealed local carrier graph. Never resolves a current game type.
    internal static class FrozenV29Types
    {
        internal static Type Resolve(string name)
        {
            switch (name)
            {
                case "AllianceRow": return typeof(Gamesim.Persistence.Frozen29Data.AllianceRow);
                case "AllianceState": return typeof(Gamesim.Persistence.Frozen29Data.AllianceState);
                case "AppearanceColor": return typeof(Gamesim.Persistence.Frozen29Data.AppearanceColor);
                case "AppearanceValue": return typeof(Gamesim.Persistence.Frozen29Data.AppearanceValue);
                case "AppearanceWardrobe": return typeof(Gamesim.Persistence.Frozen29Data.AppearanceWardrobe);
                case "ArcHistory": return typeof(Gamesim.Persistence.Frozen29Data.ArcHistory);
                case "BallotRow": return typeof(Gamesim.Persistence.Frozen29Data.BallotRow);
                case "BlocCallRow": return typeof(Gamesim.Persistence.Frozen29Data.BlocCallRow);
                case "BondState": return typeof(Gamesim.Persistence.Frozen29Data.BondState);
                case "CharacterAppearance": return typeof(Gamesim.Persistence.Frozen29Data.CharacterAppearance);
                case "CharacterOutfit": return typeof(Gamesim.Persistence.Frozen29Data.CharacterOutfit);
                case "ClaimRow": return typeof(Gamesim.Persistence.Frozen29Data.ClaimRow);
                case "CompetitionRow": return typeof(Gamesim.Persistence.Frozen29Data.CompetitionRow);
                case "CompetitionScore": return typeof(Gamesim.Persistence.Frozen29Data.CompetitionScore);
                case "ConditionalVetoResolutionState": return typeof(Gamesim.Persistence.Frozen29Data.ConditionalVetoResolutionState);
                case "ConditionalVetoTermsState": return typeof(Gamesim.Persistence.Frozen29Data.ConditionalVetoTermsState);
                case "ConductState": return typeof(Gamesim.Persistence.Frozen29Data.ConductState);
                case "ContactState": return typeof(Gamesim.Persistence.Frozen29Data.ContactState);
                case "ContestantState": return typeof(Gamesim.Persistence.Frozen29Data.ContestantState);
                case "ContestantStats": return typeof(Gamesim.Persistence.Frozen29Data.ContestantStats);
                case "DealState": return typeof(Gamesim.Persistence.Frozen29Data.DealState);
                case "DialogueEntryReceiptState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueEntryReceiptState);
                case "DialogueEpisodeState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueEpisodeState);
                case "DialogueEvidenceState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueEvidenceState);
                case "DialogueResponseState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueResponseState);
                case "DialogueRoleGrantState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueRoleGrantState);
                case "DialogueState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueState);
                case "DialogueStatementState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueStatementState);
                case "DialogueTimeReceiptState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueTimeReceiptState);
                case "DialogueVetoDecisionState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueVetoDecisionState);
                case "DialogueVetoOccasionState": return typeof(Gamesim.Persistence.Frozen29Data.DialogueVetoOccasionState);
                case "DiaryPromptState": return typeof(Gamesim.Persistence.Frozen29Data.DiaryPromptState);
                case "EpisodeEvent": return typeof(Gamesim.Persistence.Frozen29Data.EpisodeEvent);
                case "EpisodeState": return typeof(Gamesim.Persistence.Frozen29Data.EpisodeState);
                case "EvictionSpeechState": return typeof(Gamesim.Persistence.Frozen29Data.EvictionSpeechState);
                case "FinalArgumentState": return typeof(Gamesim.Persistence.Frozen29Data.FinalArgumentState);
                case "FinalSpeechState": return typeof(Gamesim.Persistence.Frozen29Data.FinalSpeechState);
                case "GrudgeState": return typeof(Gamesim.Persistence.Frozen29Data.GrudgeState);
                case "HookState": return typeof(Gamesim.Persistence.Frozen29Data.HookState);
                case "HouseEventChoice": return typeof(Gamesim.Persistence.Frozen29Data.HouseEventChoice);
                case "HouseEventImpact": return typeof(Gamesim.Persistence.Frozen29Data.HouseEventImpact);
                case "HouseEventState": return typeof(Gamesim.Persistence.Frozen29Data.HouseEventState);
                case "HouseFactState": return typeof(Gamesim.Persistence.Frozen29Data.HouseFactState);
                case "JuryExchangeState": return typeof(Gamesim.Persistence.Frozen29Data.JuryExchangeState);
                case "LobbyState": return typeof(Gamesim.Persistence.Frozen29Data.LobbyState);
                case "LoreCastState": return typeof(Gamesim.Persistence.Frozen29Data.LoreCastState);
                case "MemoryState": return typeof(Gamesim.Persistence.Frozen29Data.MemoryState);
                case "NpcActState": return typeof(Gamesim.Persistence.Frozen29Data.NpcActState);
                case "NpcConversationState": return typeof(Gamesim.Persistence.Frozen29Data.NpcConversationState);
                case "NpcCooldownState": return typeof(Gamesim.Persistence.Frozen29Data.NpcCooldownState);
                case "NpcPairMemoryState": return typeof(Gamesim.Persistence.Frozen29Data.NpcPairMemoryState);
                case "NpcSocialState": return typeof(Gamesim.Persistence.Frozen29Data.NpcSocialState);
                case "OpportunityRow": return typeof(Gamesim.Persistence.Frozen29Data.OpportunityRow);
                case "OpportunityStep": return typeof(Gamesim.Persistence.Frozen29Data.OpportunityStep);
                case "PactPlanRow": return typeof(Gamesim.Persistence.Frozen29Data.PactPlanRow);
                case "PlanSay": return typeof(Gamesim.Persistence.Frozen29Data.PlanSay);
                case "PowerRow": return typeof(Gamesim.Persistence.Frozen29Data.PowerRow);
                case "PromiseState": return typeof(Gamesim.Persistence.Frozen29Data.PromiseState);
                case "ReckoningState": return typeof(Gamesim.Persistence.Frozen29Data.ReckoningState);
                case "RelationshipArcState": return typeof(Gamesim.Persistence.Frozen29Data.RelationshipArcState);
                case "RelationshipEventState": return typeof(Gamesim.Persistence.Frozen29Data.RelationshipEventState);
                case "RelationshipState": return typeof(Gamesim.Persistence.Frozen29Data.RelationshipState);
                case "RemovalState": return typeof(Gamesim.Persistence.Frozen29Data.RemovalState);
                case "ReplyCardState": return typeof(Gamesim.Persistence.Frozen29Data.ReplyCardState);
                case "ReplyRow": return typeof(Gamesim.Persistence.Frozen29Data.ReplyRow);
                case "SeasonLedger": return typeof(Gamesim.Persistence.Frozen29Data.SeasonLedger);
                case "StandingRow": return typeof(Gamesim.Persistence.Frozen29Data.StandingRow);
                case "StoryCooldownState": return typeof(Gamesim.Persistence.Frozen29Data.StoryCooldownState);
                case "StoryEffectState": return typeof(Gamesim.Persistence.Frozen29Data.StoryEffectState);
                case "StoryModifierState": return typeof(Gamesim.Persistence.Frozen29Data.StoryModifierState);
                case "StoryRoleState": return typeof(Gamesim.Persistence.Frozen29Data.StoryRoleState);
                case "StoryStepState": return typeof(Gamesim.Persistence.Frozen29Data.StoryStepState);
                case "StoryVarState": return typeof(Gamesim.Persistence.Frozen29Data.StoryVarState);
                case "StoryWorldState": return typeof(Gamesim.Persistence.Frozen29Data.StoryWorldState);
                case "StorylineState": return typeof(Gamesim.Persistence.Frozen29Data.StorylineState);
                case "UnifiedCommitmentState": return typeof(Gamesim.Persistence.Frozen29Data.UnifiedCommitmentState);
                case "UnifiedHearingEvidenceState": return typeof(Gamesim.Persistence.Frozen29Data.UnifiedHearingEvidenceState);
                case "UnifiedHearingReceiptState": return typeof(Gamesim.Persistence.Frozen29Data.UnifiedHearingReceiptState);
                case "UnifiedVoteBallotState": return typeof(Gamesim.Persistence.Frozen29Data.UnifiedVoteBallotState);
                case "UnifiedVoteRevealState": return typeof(Gamesim.Persistence.Frozen29Data.UnifiedVoteRevealState);
                case "VetoPrizeState": return typeof(Gamesim.Persistence.Frozen29Data.VetoPrizeState);
                case "VoteState": return typeof(Gamesim.Persistence.Frozen29Data.VoteState);
                case "WebJurorSentiment": return typeof(Gamesim.Persistence.Frozen29Data.WebJurorSentiment);
                case "WebJurySentimentEvent": return typeof(Gamesim.Persistence.Frozen29Data.WebJurySentimentEvent);
                case "WebJurySentimentState": return typeof(Gamesim.Persistence.Frozen29Data.WebJurySentimentState);
                case "WebOathRecord": return typeof(Gamesim.Persistence.Frozen29Data.WebOathRecord);
                case "WebPersonaHistory": return typeof(Gamesim.Persistence.Frozen29Data.WebPersonaHistory);
                case "WebPersonaScore": return typeof(Gamesim.Persistence.Frozen29Data.WebPersonaScore);
                case "WebPersonaState": return typeof(Gamesim.Persistence.Frozen29Data.WebPersonaState);
                default: throw new System.IO.InvalidDataException("Unknown fixed29 component type.");
            }
        }
    }
}
