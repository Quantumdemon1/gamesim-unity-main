using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Recursive schema-6 persistence shape, frozen the moment schema 7 was cut.
    ///
    /// <para>Schema 6 is v5 plus the NPC social subsystem, so the state it describes is the v5 shape
    /// with one extra root object. Its nested rows reference the already-frozen v2 and v5 contracts
    /// rather than the runtime simulation DTOs, which is the whole point: the runtime types keep
    /// growing and a frozen contract that followed them would validate nothing.</para>
    ///
    /// <para><b>Schema 6 spans two eras and this accepts both.</b> It was cut when a house held
    /// exactly six, and later widened to three-to-sixteen without a version bump because nothing
    /// about the stored shape changed — only the rules did. So the bounds here are the widest thing
    /// a v6 save ever legally held: three to sixteen houseguests, and every cast-derived collection
    /// bounded by sixteen rather than by six.</para>
    /// </summary>
    internal static class FrozenEpisodeV6
    {
        /// <summary>The largest cast schema 6 ever accepted. Not a design number; a frozen bound.</summary>
        private const int MaxCast = 16;
        private const int MinCast = 3;

#pragma warning disable 0649
        internal sealed class Conversation
        {
            public long sequence, startedTick;
            public string firstId, secondId, topic, rendezvousId;
            public int week;
            public int phase;
            public double durationMs;
        }
        internal sealed class Cooldown { public string npcId; public long untilTick; }
        internal sealed class PairMemory { public string fromId, toId, lastTopic; public int count; public long lastStartTick; }
        internal sealed class NpcSocial
        {
            public int rulesStartWeek;
            public long clockTick, nextScanTick, nextConversationSequence;
            public uint randomState;
            public List<Conversation> pending;
            public List<Cooldown> cooldowns;
            public List<PairMemory> pairMemory;
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
            public FrozenEpisodeV5.Persona playerPersona;
            public FrozenEpisodeV5.JurySentiment jurySentiment;
            public FrozenEpisodeV5.DiaryPrompt pendingDiary;
            public int lastDiaryRoomWeek, phaseEventSocialBonus, phaseEventCompBonus;
            public List<string> resolvedDiaryIds;
            public List<FrozenEpisodeV5.Oath> loyaltyOaths;
            public List<string> oathOpportunities, shownOathMilestones;
            public int playerStudyBonus;
            public int blocRulesStartWeek;
            public NpcSocial npcSocial;
        }
#pragma warning restore 0649

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 6)
                throw new InvalidDataException("Expected simulation schema version 6.");
            SaveJson.CheckDtoShape(original, typeof(State), "state(v6)");
            try
            {
                ValidateScalarRanges(original.ToObject<State>(SaveJson.Serializer()));
            }
            catch (Exception error) when (error is JsonException || error is OverflowException)
            {
                throw new InvalidDataException("Historical v6 data exceeds its frozen numeric or serialization contract.", error);
            }
        }

        /// <summary>
        /// The numeric and collection contract accepted v6 data had to satisfy. Do not widen it when
        /// later runtime rules grow — that is what a frozen contract is for.
        /// </summary>
        internal static void ValidateScalarRanges(State s)
        {
            if (s == null) throw Invalid("state");
            Range(s.week, 1, 100, "week"); Range(s.revision, 0, 1000000, "revision");
            Range(s.nextSequence, 1, 1000000, "nextSequence"); Range(s.socialActions, 0, 18, "socialActions");
            Range(s.playerStudyBonus, 0, 5, "playerStudyBonus");
            Range(s.blocRulesStartWeek, 1, Math.Min(101, s.week + 1), "blocRulesStartWeek");
            Range(s.phaseEventSocialBonus, 0, 1000, "phaseEventSocialBonus");
            Range(s.phaseEventCompBonus, 0, 1000, "phaseEventCompBonus");
            Range(s.lastDiaryRoomWeek, 0, s.week, "lastDiaryRoomWeek");

            var cast = Rows(s.contestants, MinCast, MaxCast, "contestants");
            foreach (var c in cast)
            {
                Range(c.hohWins, 0, 100, "hohWins"); Range(c.vetoWins, 0, 100, "vetoWins");
                Range(c.timesNominated, 0, 100, "timesNominated");
                Rows(c.traits, 0, 20, "traits");
                foreach (var week in Rows(c.nominationWeeks, 0, 100, "nominationWeeks")) Range(week, 1, s.week, "nominationWeeks[]");
                if (c.stats == null) throw Invalid("stats");
                foreach (double value in new[] { c.stats.physical, c.stats.mental, c.stats.endurance, c.stats.social,
                    c.stats.luck, c.stats.competition, c.stats.strategic, c.stats.loyalty }) Range(value, 0, 10, "stats");
            }
            foreach (var edge in Rows(s.relationships, 0, MaxCast * (MaxCast - 1), "relationships"))
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
            Rows(s.nominees, 0, 2, "nominees"); Rows(s.vetoPlayers, 0, EpisodeEngine.VetoLineupSize, "vetoPlayers");
            foreach (var p in Rows(s.promises, 0, 200, "promises"))
            { Range(p.week, 1, s.week, "promise.week"); Range(p.expiresWeek, 0, 101, "promise.expiresWeek"); }
            foreach (var a in Rows(s.alliances, 0, 100, "alliances")) Rows(a.members, 2, MaxCast, "alliance.members");
            foreach (var m in Rows(s.memories, 0, 30 * MaxCast, "memories")) Range(m.week, 1, s.week, "memory.week");
            Rows(s.votes, 0, MaxCast, "votes");
            foreach (var c in Rows(s.competitionScores, 0, MaxCast, "competitionScores")) Range(c.score, 0, 1030, "competition.score");
            foreach (var e in Rows(s.events, 0, 256, "events"))
            { Range(e.sequence, 1, s.nextSequence - 1, "event.sequence"); Range(e.week, 1, s.week, "event.week"); }
            Rows(s.acceptedCommandIds, 0, 256, "acceptedCommandIds");
            Rows(s.juryExchanges, 0, MaxCast - 2, "juryExchanges"); Range(s.juryQuestionIndex, 0, s.juryExchanges.Count, "juryQuestionIndex");
            Rows(s.finalSpeeches, 0, 2, "finalSpeeches");
            foreach (var arc in Rows(s.relationshipArcs, 0, MaxCast - 1, "relationshipArcs"))
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
            foreach (var j in Rows(s.jurySentiment.jurors, 0, MaxCast - 2, "jurySentiment.jurors"))
            {
                Range(j.sentiment, -100, 100, "juror.sentiment");
                foreach (var e in Rows(j.events, 1, 512, "juror.events"))
                { Range(e.week, 0, s.week, "juror.event.week"); Range(e.delta, -200, 200, "juror.event.delta"); }
            }
            if (s.pendingDiary != null) Range(s.pendingDiary.week, s.week, s.week, "pendingDiary.week");
            Rows(s.resolvedDiaryIds, 0, 100, "resolvedDiaryIds");
            foreach (var o in Rows(s.loyaltyOaths, 0, MaxCast - 1, "loyaltyOaths"))
            { Range(o.week, 1, s.week, "oath.week"); Range(o.timestamp, 1, s.nextSequence - 1, "oath.timestamp"); }
            Rows(s.oathOpportunities, 0, MaxCast - 1, "oathOpportunities");
            Rows(s.shownOathMilestones, 0, MaxCast - 1, "shownOathMilestones");

            ValidateNpcSocial(s);
        }

        /// <summary>The subsystem schema 6 added, and the only part of this shape v5 has nothing to say about.</summary>
        private static void ValidateNpcSocial(State s)
        {
            var npc = s.npcSocial;
            if (npc == null) throw Invalid("npcSocial");
            Range(npc.rulesStartWeek, 1, Math.Min(101, s.week + 1), "npcSocial.rulesStartWeek");
            Range(npc.clockTick, 0, long.MaxValue, "npcSocial.clockTick");
            Range(npc.nextScanTick, 0, long.MaxValue, "npcSocial.nextScanTick");
            Range(npc.nextConversationSequence, 1, long.MaxValue, "npcSocial.nextConversationSequence");
            foreach (var pending in Rows(npc.pending, 0, MaxCast, "npcSocial.pending"))
            {
                Range(pending.sequence, 1, npc.nextConversationSequence - 1, "npcSocial.pending.sequence");
                Range(pending.startedTick, 0, npc.clockTick, "npcSocial.pending.startedTick");
                Range(pending.week, 1, s.week, "npcSocial.pending.week");
                Range(pending.durationMs, 0, 600000, "npcSocial.pending.durationMs");
            }
            foreach (var cooldown in Rows(npc.cooldowns, 0, MaxCast, "npcSocial.cooldowns"))
                Range(cooldown.untilTick, 0, long.MaxValue, "npcSocial.cooldown.untilTick");
            foreach (var pair in Rows(npc.pairMemory, 0, MaxCast * (MaxCast - 1), "npcSocial.pairMemory"))
            {
                Range(pair.count, 0, 1000000, "npcSocial.pair.count");
                Range(pair.lastStartTick, 0, long.MaxValue, "npcSocial.pair.lastStartTick");
            }
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

        private static InvalidDataException Invalid(string field)
            => new InvalidDataException("Historical v6 scalar/collection contract is invalid: " + field + ".");
    }
}
