using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public enum EpisodePhase
    {
        Social, HoH, Nomination, VetoSelection, Veto, VetoMeeting, Campaign,
        Eviction, FinalHoHPart1, FinalHoHPart2, FinalHoHPart3, FinalEviction, Jury, Finished,
        JuryQuestioning, FinalSpeeches // Append: version-one ordinal values remain stable.
    }

    /// <summary>
    /// Where eviction night has got to.
    ///
    /// <para>A stage inside <see cref="EpisodePhase.Eviction"/> rather than five new phases.
    /// <see cref="EpisodePhase"/> ordinals are frozen — every historical save stores the number, and
    /// a parity test pins that all sixteen survive a round trip — so the night is modelled as state
    /// within the phase it already had.</para>
    ///
    /// <para>Append only, for the same reason.</para>
    /// </summary>
    public enum EvictionStage { Interaction, Speeches, Voting, Tiebreaker, Results }

    public enum ContestantStatus { Active, Evicted, Jury, Winner, RunnerUp }
    public enum PromiseKind { Safety, Vote, FinalTwo, AllianceLoyalty, Information }
    public enum PromiseStatus { Active, Fulfilled, Broken, Expired }

    [Serializable]
    public sealed class ContestantStats
    {
        public double physical = 5, mental = 5, endurance = 5, social = 5, luck = 5;
        public double competition = 5, strategic = 5, loyalty = 5;
        public ContestantStats Clone() => (ContestantStats)MemberwiseClone();
    }

    [Serializable]
    public sealed class ContestantState
    {
        public string id, name, pronouns, motive, homeRoom;
        // Card copy: who this person is outside the game. Optional by construction — a save written
        // before these existed deserialises them empty, and every surface treats empty as "omit the
        // line" rather than printing a blank field.
        public string occupation, archetype;
        // Schema 8: the rest of the creator's card. Optional in the same way as the fields above —
        // a season built before the creator existed has neither, and every surface omits the line
        // rather than printing a blank one.
        public string hometown, bio;
        public int age;
        public string mood = "Neutral", stressLevel = "Normal";
        public bool isPlayer;
        public ContestantStatus status;
        public ContestantStats stats = new ContestantStats();
        public List<string> traits = new List<string>();
        public int hohWins, vetoWins, timesNominated;
        public List<int> nominationWeeks = new List<int>();
        public ContestantState Clone()
        {
            var copy = (ContestantState)MemberwiseClone();
            copy.stats = stats.Clone(); copy.traits = new List<string>(traits);
            copy.nominationWeeks = new List<int>(nominationWeeks);
            return copy;
        }
    }

    [Serializable]
    public sealed class RelationshipState
    {
        public string fromId, toId;
        public double score;
        public int lastInteractionWeek;
        public List<string> notes = new List<string>();
        public List<RelationshipEventState> events = new List<RelationshipEventState>();
        public RelationshipState Clone()
        {
            var copy = (RelationshipState)MemberwiseClone();
            copy.notes = new List<string>(notes); copy.events = events.Select(item => item.Clone()).ToList();
            return copy;
        }
    }

    [Serializable] public sealed class RelationshipEventState
    {
        public int sequence, week;
        public string type, description;
        public double impactScore;
        public bool decayable;
        public RelationshipEventState Clone() => (RelationshipEventState)MemberwiseClone();
    }

    [Serializable] public sealed class JuryExchangeState
    {
        public string questionerId, finalistId, tone, question, optionA, optionB, correctChoice;
        public string answerChoice, answer, opponentAnswer;
        public bool completed;
        public JuryExchangeState Clone() => (JuryExchangeState)MemberwiseClone();
    }

    /// <summary>
    /// A nominee addressing the house on eviction night.
    ///
    /// <para>Separate from <see cref="FinalSpeechState"/>, which is the finale's plea to the jury.
    /// They read alike and are not: this one is given by someone who may still be saved, is given
    /// every week, and carries the week it belongs to.</para>
    /// </summary>
    [Serializable] public sealed class EvictionSpeechState
    {
        public string speakerId, text;
        public int week;
        public bool isPlayerAuthored;
        public EvictionSpeechState Clone() => (EvictionSpeechState)MemberwiseClone();
    }

    [Serializable] public sealed class FinalSpeechState
    {
        public string speakerId, text;
        public bool isPlayerAuthored;
        public FinalSpeechState Clone() => (FinalSpeechState)MemberwiseClone();
    }

    [Serializable] public sealed class DiaryPromptState
    {
        public string id, trigger, evictedId;
        public int week;
        public bool isNominee;
        public DiaryPromptState Clone() => (DiaryPromptState)MemberwiseClone();
    }

    [Serializable]
    public sealed class PromiseState
    {
        public string id, fromId, toId, targetId;
        public PromiseKind kind;
        public PromiseStatus status;
        public int week, expiresWeek;
        public string impact = "medium";
        public PromiseState Clone() => (PromiseState)MemberwiseClone();
    }

    [Serializable]
    public sealed class AllianceState
    {
        public string id, name;
        public List<string> members = new List<string>();
        public bool active = true;
        public AllianceState Clone()
        {
            var copy = (AllianceState)MemberwiseClone(); copy.members = new List<string>(members); return copy;
        }
    }

    [Serializable]
    public sealed class MemoryState
    {
        public string ownerId, subjectId, text;
        public int week;
        public bool isPrivate;
        public MemoryState Clone() => (MemoryState)MemberwiseClone();
    }

    [Serializable]
    public sealed class VoteState
    {
        public string voterId, targetId, reason;
        public VoteState Clone() => (VoteState)MemberwiseClone();
    }

    [Serializable]
    public sealed class CompetitionScore
    {
        public string contestantId;
        public double score;
        public CompetitionScore Clone() => (CompetitionScore)MemberwiseClone();
    }

    [Serializable]
    public sealed class EpisodeEvent
    {
        public int sequence, week;
        public EpisodePhase phase;
        public string kind, text;
        public List<string> audienceIds = new List<string>();
        public EpisodeEvent Clone()
        {
            var copy = (EpisodeEvent)MemberwiseClone(); copy.audienceIds = new List<string>(audienceIds); return copy;
        }
    }

    /// <summary>Portable save DTO. No Unity objects, wall-clock reads, network tokens or frame state.</summary>
    [Serializable]
    public sealed class EpisodeState
    {
        public int schemaVersion = 11;
        public string sessionId;
        public uint seed, randomState;
        public int revision, week = 1, nextSequence = 1, socialActions;
        public EpisodePhase phase = EpisodePhase.Social;
        public string playerId, hohId, previousHohId, vetoHolderId, winnerId, runnerUpId;
        public string finalPart1WinnerId, finalPart2WinnerId;
        public bool competitionResolved, vetoResolved, evictionResolved;
        public List<ContestantState> contestants = new List<ContestantState>();
        public List<RelationshipState> relationships = new List<RelationshipState>();
        public List<PromiseState> promises = new List<PromiseState>();
        public List<AllianceState> alliances = new List<AllianceState>();
        public List<MemoryState> memories = new List<MemoryState>();
        public List<string> nominees = new List<string>();
        public List<string> vetoPlayers = new List<string>();
        public List<VoteState> votes = new List<VoteState>();
        public List<CompetitionScore> competitionScores = new List<CompetitionScore>();
        public List<EpisodeEvent> events = new List<EpisodeEvent>();
        public List<string> acceptedCommandIds = new List<string>();
        public List<JuryExchangeState> juryExchanges = new List<JuryExchangeState>();
        public int juryQuestionIndex;
        public List<FinalSpeechState> finalSpeeches = new List<FinalSpeechState>();
        public List<RelationshipArcState> relationshipArcs = new List<RelationshipArcState>();
        public WebPersonaState playerPersona = WebDiaryRoom.CreateInitialPersonaState();
        public WebJurySentimentState jurySentiment = WebJurySentiment.CreateInitial();
        public DiaryPromptState pendingDiary;
        public int lastDiaryRoomWeek, phaseEventSocialBonus, phaseEventCompBonus;
        public List<string> resolvedDiaryIds = new List<string>();
        public List<WebOathRecord> loyaltyOaths = new List<WebOathRecord>();
        public List<string> oathOpportunities = new List<string>();
        public List<string> shownOathMilestones = new List<string>();
        // Persistent preparation; separate from phase-event counters and native precision input.
        public int playerStudyBonus;
        // Native rule-version boundary: existing/imported seasons retain their complete current week.
        public int blocRulesStartWeek = 1;
        public NpcSocialState npcSocial = NpcSocialState.Create(0);

        // ---------------------------------------------------------------- schema 8
        /// <summary>How far eviction night has got, so a reload resumes rather than rewinds.</summary>
        public EvictionStage evictionStage = EvictionStage.Interaction;
        public List<EvictionSpeechState> evictionSpeeches = new List<EvictionSpeechState>();
        /// <summary>A nominee the Head of Household means to backdoor; not itself a nomination.</summary>
        public string backdoorTargetId;
        /// <summary>
        /// Social actions spent outside the social week. The reference build counts these against
        /// the same budget but tracks them separately, because the in-phase counter resets on the
        /// phase and this one does not.
        /// </summary>
        public int outOfPhaseSocialActions;
        /// <summary>Opening beats already played, so the intro does not replay on every load.</summary>
        public List<string> openingBeatsSeen = new List<string>();

        // ---------------------------------------------------------------- schema 9
        /// <summary>
        /// The week the social-action budget starts following the cast.
        ///
        /// <para>The allowance used to be a flat eighteen and is now half the active house, rounded
        /// up. A season already underway keeps its old allowance for the week it is in, because the
        /// alternative is telling someone mid-week that actions they have already legally spent have
        /// put them over a limit that did not exist when they spent them.</para>
        ///
        /// <para>The same rule-version boundary <see cref="blocRulesStartWeek"/> and
        /// <see cref="NpcSocialState.rulesStartWeek"/> already use. A fresh season starts at week
        /// one, so new play is under the ported rule from the first conversation.</para>
        /// </summary>
        public int socialBudgetRulesStartWeek = 1;

        // ---------------------------------------------------------------- schema 10
        /// <summary>
        /// Deals between houseguests.
        ///
        /// <para>The eviction vote has weighed deals since it was written —
        /// <c>WebEvictionVoting.DealObligation</c> reads this list, scores an active
        /// <c>vote_save</c> at +35 and a broken one at −35, and <c>PairDealValue</c> has a table
        /// running from information sharing at 10 to a final two at 50. It has been reading an empty
        /// list the whole time, because nothing in the project could make a deal. This is the same
        /// shape as alliances and promises before Phase C: the consumer shipped, the producer did
        /// not.</para>
        ///
        /// <para>Deals sit above promises deliberately. The interaction table this project already
        /// implements scores <c>deal_fulfilled</c> at +35 and <c>deal_broken</c> at −50, against
        /// +25 and −40 for a promise — a deal is the heavier commitment, and breaking one is the
        /// worst thing in the table short of betraying an alliance.</para>
        /// </summary>
        public List<DealState> deals = new List<DealState>();

        /// <summary>
        /// The week deals start being made, so a season already under way is not handed a system it
        /// was not played under. The same boundary <see cref="blocRulesStartWeek"/> uses.
        /// </summary>
        public int dealRulesStartWeek = 1;

        // ---------------------------------------------------------------- schema 11
        //
        // One version carrying two systems, deliberately. The social vocabulary and the event layer
        // both need persisted state, and the save format checks stored objects field for field — so
        // adding them separately would cost two migrations, two frozen contracts and two sweeps of
        // every fixture, for one week's work either way.

        /// <summary>
        /// Extra social actions the player has bought this phase.
        ///
        /// <para>The reference's <c>buy_action_point</c> trades relationship damage for another
        /// action. The budget here has been a hard ceiling with no way past it, which makes a week
        /// where the house moves faster than the allowance simply unplayable rather than expensive.
        /// </para>
        ///
        /// <para>Counted separately from <see cref="socialActions"/> rather than deducted from it,
        /// because the two answer different questions: one is what you spent, this is what you paid
        /// to be allowed to spend it, and a screen that showed the second as the first would be
        /// telling the player they had actions left when they had bought them.</para>
        /// </summary>
        public int boughtActionPoints;

        /// <summary>
        /// Things that have happened to the house.
        ///
        /// <para>Every week in this port happens because the player pressed something. The event
        /// layer is what the reference uses to make one week feel unlike the last — six systems'
        /// worth of situations that arrive on their own and sometimes ask a question.</para>
        ///
        /// <para>Resolved events stay in the list. They are the record of what the season did to the
        /// player, which the weekly recap reads and which a screen cannot reconstruct once it is
        /// gone.</para>
        /// </summary>
        public List<HouseEventState> houseEvents = new List<HouseEventState>();

        /// <summary>
        /// The week the house starts having things happen to it, so a season already under way is
        /// not suddenly handed a system it was not played under. The fifth use of this boundary,
        /// after <see cref="blocRulesStartWeek"/>, <see cref="NpcSocialState.rulesStartWeek"/>,
        /// <see cref="socialBudgetRulesStartWeek"/> and <see cref="dealRulesStartWeek"/>.
        /// </summary>
        public int eventRulesStartWeek = 1;

        public ContestantState Find(string id) => contestants.FirstOrDefault(c => c.id == id);
        public IEnumerable<ContestantState> Active => contestants.Where(c => c.status == ContestantStatus.Active);
        public double Score(string from, string to) => relationships.FirstOrDefault(r => r.fromId == from && r.toId == to)?.score ?? 0;
        public bool Allied(string a, string b) => alliances.Any(x => x.active && x.members.Contains(a) && x.members.Contains(b));

        public EpisodeState Clone()
        {
            var copy = (EpisodeState)MemberwiseClone();
            copy.contestants = contestants.Select(x => x.Clone()).ToList();
            copy.relationships = relationships.Select(x => x.Clone()).ToList();
            copy.promises = promises.Select(x => x.Clone()).ToList();
            copy.alliances = alliances.Select(x => x.Clone()).ToList();
            copy.memories = memories.Select(x => x.Clone()).ToList();
            copy.nominees = new List<string>(nominees); copy.vetoPlayers = new List<string>(vetoPlayers);
            copy.votes = votes.Select(x => x.Clone()).ToList();
            copy.competitionScores = competitionScores.Select(x => x.Clone()).ToList();
            copy.events = events.Select(x => x.Clone()).ToList();
            copy.acceptedCommandIds = new List<string>(acceptedCommandIds);
            copy.juryExchanges = juryExchanges.Select(x => x.Clone()).ToList();
            copy.finalSpeeches = finalSpeeches.Select(x => x.Clone()).ToList();
            copy.relationshipArcs = relationshipArcs.Select(x => x.Clone()).ToList();
            copy.playerPersona = playerPersona.Clone();
            copy.jurySentiment = jurySentiment.Clone();
            copy.pendingDiary = pendingDiary?.Clone();
            copy.resolvedDiaryIds = new List<string>(resolvedDiaryIds);
            copy.loyaltyOaths = loyaltyOaths.Select(x => new WebOathRecord { playerId = x.playerId, targetId = x.targetId, week = x.week, timestamp = x.timestamp }).ToList();
            copy.oathOpportunities = new List<string>(oathOpportunities);
            copy.shownOathMilestones = new List<string>(shownOathMilestones);
            copy.npcSocial = npcSocial.Clone();
            copy.evictionSpeeches = evictionSpeeches.Select(x => x.Clone()).ToList();
            copy.openingBeatsSeen = new List<string>(openingBeatsSeen);
            copy.deals = deals.Select(x => x.Clone()).ToList();
            copy.houseEvents = houseEvents.Select(x => x.Clone()).ToList();
            return copy;
        }
    }

    public enum EpisodeCommandKind
    {
        Advance, Compete, Nominate, ResolveVeto, CastVote, FinalEvict,
        Talk, PromiseSafety, PromiseVote, PromiseFinalTwo, FormAlliance, LeaveAlliance, ShareInformation,
        AnswerJury, SkipQuestioning, SubmitSpeech, ReflectDiary, SkipDiary, SwearLoyalty, DeclineLoyalty,
        StudyHouse, SimulateCompetition,
        SubmitEvictionSpeech,
        AskForIntel, Eavesdrop, SpreadLie, VentAbout, SchemeAgainst,
        SetBackdoorPlan,
        MarkOpeningBeat,
        ProposeDeal,
        RespondToDeal,
        // The social vocabulary, appended in the reference's own order. Talk stays where it is:
        // its ordinal is pinned by recorded seasons, and it remains the plain conversation these
        // five are variations on.
        SmallTalk,
        PersonalChat,
        DiscussGame,
        StrategicDiscussion,
        RelationshipBuilding,
        ShareSecret,
        SpreadRumor,
        HouseMeeting,
        BuyActionPoint // Append: preserve every pre-v4 command ordinal.
    }

    /// <summary>
    /// The five beats that run once, in order, before the first Head of Household competition.
    ///
    /// <para>Named rather than numbered because <see cref="EpisodeState.openingBeatsSeen"/> stores
    /// the names, and a season saved halfway through the opening has to be able to say which of them
    /// it has already played after the list has been reordered or added to.</para>
    /// </summary>
    public static class OpeningBeat
    {
        public const string Intro = "intro";
        public const string HouseEntry = "house-entry";
        public const string WalkIn = "house-walk-in";
        public const string Tutorial = "tutorial";
        public const string MeetAndGreet = "meet-and-greet";

        /// <summary>In the order the reference build plays them.</summary>
        public static readonly string[] InOrder = { Intro, HouseEntry, WalkIn, Tutorial, MeetAndGreet };

        public static bool IsKnown(string beat) =>
            beat != null && Array.IndexOf(InOrder, beat) >= 0;
    }

    [Serializable]
    public sealed class EpisodeCommand
    {
        public string id, actorId, targetId, secondTargetId;
        public string text;
        public int expectedRevision;
        public EpisodePhase expectedPhase;
        public EpisodeCommandKind kind;
        public bool useVeto;
        // Optional human minigame input. The rules layer documents whether it affects scoring.
        public double performance;
    }

    public sealed class CommandResult
    {
        public bool accepted, duplicate;
        public string reason;
        public EpisodeState state;
    }
}
