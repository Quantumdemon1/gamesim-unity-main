using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Recursive schema-4 persistence shape. Shared older nested shapes reference only
    /// the already-frozen v2 contract, never the growing runtime simulation DTOs.
    /// </summary>
    internal static class FrozenEpisodeV4
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
            public int playerStudyBonus;
        }
#pragma warning restore 0649

        // Explicit numeric/collection contract from accepted v4. Do not widen when later runtime rules grow.
        internal static void ValidateScalarRanges(State s)
        {
            if (s == null) throw Invalid("state");
            Range(s.week, 1, 100, "week"); Range(s.revision, 0, 1000000, "revision");
            Range(s.nextSequence, 1, 1000000, "nextSequence"); Range(s.socialActions, 0, 18, "socialActions");
            Range(s.playerStudyBonus, 0, 5, "playerStudyBonus");
            Range(s.phaseEventSocialBonus, 0, 1000, "phaseEventSocialBonus");
            Range(s.phaseEventCompBonus, 0, 1000, "phaseEventCompBonus");
            Range(s.lastDiaryRoomWeek, 0, s.week, "lastDiaryRoomWeek");
            foreach (var c in Rows(s.contestants, 6, 6, "contestants"))
            {
                Range(c.hohWins, 0, 100, "hohWins"); Range(c.vetoWins, 0, 100, "vetoWins");
                Range(c.timesNominated, 0, 100, "timesNominated");
                Rows(c.traits, 0, 20, "traits");
                foreach (var week in Rows(c.nominationWeeks, 0, 100, "nominationWeeks")) Range(week, 1, s.week, "nominationWeeks[]");
                if (c.stats == null) throw Invalid("stats");
                foreach (double value in new[] { c.stats.physical, c.stats.mental, c.stats.endurance, c.stats.social,
                    c.stats.luck, c.stats.competition, c.stats.strategic, c.stats.loyalty }) Range(value, 0, 10, "stats");
            }
            foreach (var edge in Rows(s.relationships, 0, 36, "relationships"))
            {
                Range(edge.score, -100, 100, "relationship.score");
                Range(edge.lastInteractionWeek, 0, s.week, "relationship.lastInteractionWeek");
                Rows(edge.notes, 0, 256, "relationship.notes");
                foreach (var item in Rows(edge.events, 0, 512, "relationship.events"))
                {
                    Range(item.sequence, 1, s.nextSequence - 1, "relationship.event.sequence");
                    Range(item.week, 1, s.week, "relationship.event.week"); Range(item.impactScore, -200, 200, "relationship.event.impactScore");
                }
            }
            Rows(s.nominees, 0, 2, "nominees"); Rows(s.vetoPlayers, 0, 6, "vetoPlayers");
            foreach (var p in Rows(s.promises, 0, 200, "promises"))
            { Range(p.week, 1, s.week, "promise.week"); Range(p.expiresWeek, 0, 101, "promise.expiresWeek"); }
            foreach (var a in Rows(s.alliances, 0, 100, "alliances")) Rows(a.members, 2, 6, "alliance.members");
            foreach (var m in Rows(s.memories, 0, 180, "memories")) Range(m.week, 1, s.week, "memory.week");
            Rows(s.votes, 0, 6, "votes");
            foreach (var c in Rows(s.competitionScores, 0, 6, "competitionScores")) Range(c.score, 0, 1030, "competition.score");
            foreach (var e in Rows(s.events, 0, 256, "events"))
            { Range(e.sequence, 1, s.nextSequence - 1, "event.sequence"); Range(e.week, 1, s.week, "event.week"); }
            Rows(s.acceptedCommandIds, 0, 256, "acceptedCommandIds");
            Rows(s.juryExchanges, 0, 4, "juryExchanges"); Range(s.juryQuestionIndex, 0, s.juryExchanges.Count, "juryQuestionIndex");
            Rows(s.finalSpeeches, 0, 2, "finalSpeeches");
            foreach (var arc in Rows(s.relationshipArcs, 0, 5, "relationshipArcs"))
            {
                Range(arc.intensity, 0, 100, "arc.intensity"); Range(arc.escalationLevel, 0, 4, "arc.escalationLevel");
                foreach (var h in Rows(arc.weeklyHistory, 0, 2000, "arc.weeklyHistory"))
                { Range(h.week, 1, s.week, "arc.history.week"); Range(h.delta, -200, 200, "arc.history.delta"); }
            }
            if (s.playerPersona == null) throw Invalid("playerPersona");
            foreach (var score in Rows(s.playerPersona.scores, 5, 5, "persona.scores")) Range(score.score, 0, 100, "persona.score");
            foreach (var h in Rows(s.playerPersona.history, 0, 100, "persona.history")) Range(h.week, 1, s.week, "persona.history.week");
            if (s.jurySentiment == null) throw Invalid("jurySentiment");
            Range(s.jurySentiment.overallSentiment, -100, 100, "jurySentiment.overall");
            foreach (var j in Rows(s.jurySentiment.jurors, 0, 4, "jurySentiment.jurors"))
            {
                Range(j.sentiment, -100, 100, "juror.sentiment");
                foreach (var e in Rows(j.events, 1, 512, "juror.events"))
                { Range(e.week, 0, s.week, "juror.event.week"); Range(e.delta, -200, 200, "juror.event.delta"); }
            }
            if (s.pendingDiary != null) Range(s.pendingDiary.week, s.week, s.week, "pendingDiary.week");
            Rows(s.resolvedDiaryIds, 0, 100, "resolvedDiaryIds");
            foreach (var o in Rows(s.loyaltyOaths, 0, 5, "loyaltyOaths"))
            { Range(o.week, 1, s.week, "oath.week"); Range(o.timestamp, 1, s.nextSequence - 1, "oath.timestamp"); }
            Rows(s.oathOpportunities, 0, 5, "oathOpportunities"); Rows(s.shownOathMilestones, 0, 5, "shownOathMilestones");
        }

        private static List<T> Rows<T>(List<T> rows, int min, int max, string field)
        {
            if (rows == null || rows.Count < min || rows.Count > max || rows.Any(row => (object)row == null)) throw Invalid(field);
            return rows;
        }
        private static void Range(double value, double min, double max, string field)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max) throw Invalid(field);
        }
        private static InvalidDataException Invalid(string field) => new InvalidDataException("Historical v4 scalar/collection contract is invalid: " + field + ".");

    }
}
