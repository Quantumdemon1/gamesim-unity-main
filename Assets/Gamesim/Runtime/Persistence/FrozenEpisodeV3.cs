using System.Collections.Generic;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Recursive schema-3 persistence shape. Shared older nested shapes reference only
    /// the already-frozen v2 contract, never the growing runtime simulation DTOs.
    /// </summary>
    internal static class FrozenEpisodeV3
    {
#pragma warning disable 0649
        internal sealed class PersonaScore { public string persona; public int score; }
        internal sealed class PersonaHistory { public string persona; public int week; }
        internal sealed class Persona
        {
            public string current;
            public List<PersonaScore> scores;
            public List<PersonaHistory> history;
        }
        internal sealed class SentimentEvent { public int week; public double delta; public string reason; }
        internal sealed class JurorSentiment
        {
            public string jurorId, jurorName;
            public double sentiment;
            public List<SentimentEvent> events;
        }
        internal sealed class JurySentiment { public List<JurorSentiment> jurors; public double overallSentiment; }
        internal sealed class DiaryPrompt { public string id, trigger, evictedId; public int week; public bool isNominee; }
        internal sealed class Oath { public string playerId, targetId; public int week; public long timestamp; }
        internal sealed class State
        {
            public int schemaVersion;
            public string sessionId;
            public uint seed, randomState;
            public int revision, week, nextSequence, socialActions, phase;
            public string playerId, hohId, previousHohId, vetoHolderId, winnerId, runnerUpId;
            public string finalPart1WinnerId, finalPart2WinnerId;
            public bool competitionResolved, vetoResolved, evictionResolved;
            public List<FrozenEpisodeV2.Contestant> contestants;
            public List<FrozenEpisodeV2.Relationship> relationships;
            public List<FrozenEpisodeV2.Promise> promises;
            public List<FrozenEpisodeV2.Alliance> alliances;
            public List<FrozenEpisodeV2.Memory> memories;
            public List<string> nominees, vetoPlayers;
            public List<FrozenEpisodeV2.Vote> votes;
            public List<FrozenEpisodeV2.CompetitionScore> competitionScores;
            public List<FrozenEpisodeV2.Event> events;
            public List<string> acceptedCommandIds;
            public List<FrozenEpisodeV2.JuryExchange> juryExchanges;
            public int juryQuestionIndex;
            public List<FrozenEpisodeV2.FinalSpeech> finalSpeeches;
            public List<FrozenEpisodeV2.RelationshipArc> relationshipArcs;
            public Persona playerPersona;
            public JurySentiment jurySentiment;
            public DiaryPrompt pendingDiary;
            public int lastDiaryRoomWeek, phaseEventSocialBonus, phaseEventCompBonus;
            public List<string> resolvedDiaryIds;
            public List<Oath> loyaltyOaths;
            public List<string> oathOpportunities, shownOathMilestones;
        }
#pragma warning restore 0649
    }
}
