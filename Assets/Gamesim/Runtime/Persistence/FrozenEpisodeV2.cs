using System.Collections.Generic;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Recursive public-field contract frozen from work/unity-v2-source-20260910,
    /// the schema-2 baseline verified with 132 Edit Mode and 24 Play Mode tests.
    /// Do not reference growing simulation DTOs or add future fields here.
    /// Numeric enum maxima are checked by the versioned migration entry point.
    /// </summary>
    internal static class FrozenEpisodeV2
    {
#pragma warning disable 0649
        internal sealed class Stats
        {
            public double physical, mental, endurance, social, luck, competition, strategic, loyalty;
        }
        internal sealed class Contestant
        {
            public string id, name, pronouns, motive, homeRoom, mood, stressLevel;
            public bool isPlayer;
            public int status;
            public Stats stats;
            public List<string> traits;
            public int hohWins, vetoWins, timesNominated;
            public List<int> nominationWeeks;
        }
        internal sealed class Relationship
        {
            public string fromId, toId;
            public double score;
            public int lastInteractionWeek;
            public List<string> notes;
            public List<RelationshipEvent> events;
        }
        internal sealed class RelationshipEvent
        {
            public int sequence, week;
            public string type, description;
            public double impactScore;
            public bool decayable;
        }
        internal sealed class JuryExchange
        {
            public string questionerId, finalistId, tone, question, optionA, optionB, correctChoice;
            public string answerChoice, answer, opponentAnswer;
            public bool completed;
        }
        internal sealed class FinalSpeech { public string speakerId, text; public bool isPlayerAuthored; }
        internal sealed class ArcHistory { public int week; public double delta; public string reason; }
        internal sealed class RelationshipArc
        {
            public string npcId, npcName, arcType;
            public double intensity;
            public int escalationLevel;
            public List<ArcHistory> weeklyHistory;
        }
        internal sealed class Promise
        {
            public string id, fromId, toId, targetId;
            public int kind, status, week, expiresWeek;
            public string impact;
        }
        internal sealed class Alliance { public string id, name; public List<string> members; public bool active; }
        internal sealed class Memory { public string ownerId, subjectId, text; public int week; public bool isPrivate; }
        internal sealed class Vote { public string voterId, targetId, reason; }
        internal sealed class CompetitionScore { public string contestantId; public double score; }
        internal sealed class Event
        {
            public int sequence, week, phase;
            public string kind, text;
            public List<string> audienceIds;
        }
        internal sealed class State
        {
            public int schemaVersion;
            public string sessionId;
            public uint seed, randomState;
            public int revision, week, nextSequence, socialActions, phase;
            public string playerId, hohId, previousHohId, vetoHolderId, winnerId, runnerUpId;
            public string finalPart1WinnerId, finalPart2WinnerId;
            public bool competitionResolved, vetoResolved, evictionResolved;
            public List<Contestant> contestants;
            public List<Relationship> relationships;
            public List<Promise> promises;
            public List<Alliance> alliances;
            public List<Memory> memories;
            public List<string> nominees, vetoPlayers;
            public List<Vote> votes;
            public List<CompetitionScore> competitionScores;
            public List<Event> events;
            public List<string> acceptedCommandIds;
            public List<JuryExchange> juryExchanges;
            public int juryQuestionIndex;
            public List<FinalSpeech> finalSpeeches;
            public List<RelationshipArc> relationshipArcs;
        }
#pragma warning restore 0649
    }
}
