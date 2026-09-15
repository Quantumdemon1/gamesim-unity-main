using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Frozen schema-5 semantic/storage guards. Never call the evolving current validator or engine.
    /// Source jury-catalog validation intentionally retains its versioned, unchanged WebJuryQuestioning leaf.
    /// A runtime EpisodeState is only a detached data carrier after recursive FrozenEpisodeV5 shape checking.
    /// </summary>
    internal static class FrozenV5Validator
    {
        public static bool TryValidate(EpisodeState s, out string error)
        {
            error = null;
            if (s == null || s.schemaVersion != 5) return Fail(out error, "Unsupported episode schema.");
            if (!Text(s.sessionId, 160) || s.week < 1 || s.week > 100 || s.revision < 0 || s.revision > 1000000 ||
                s.nextSequence < 1 || s.nextSequence > 1000000 || s.socialActions < 0 || s.socialActions > 18 || !Defined(s.phase))
                return Fail(out error, "Invalid session counters or phase.");
            if (s.blocRulesStartWeek < 1 || s.blocRulesStartWeek > 101 || s.blocRulesStartWeek > s.week + 1)
                return Fail(out error, "Voting-bloc activation week must be within the saved season boundary.");
            if (s.playerStudyBonus < 0 || s.playerStudyBonus > 5) return Fail(out error, "Study preparation must be between zero and five.");
            if (s.contestants == null || s.contestants.Count != 6 || s.contestants.Any(c => c == null)) return Fail(out error, "The house format requires six stored contestants.");
            if (s.contestants.Select(c => c.id).Distinct(StringComparer.Ordinal).Count() != 6 || s.contestants.Count(c => c.isPlayer) != 1 ||
                !s.contestants.Any(c => c.isPlayer && c.id == s.playerId)) return Fail(out error, "Cast/player identity is invalid.");
            foreach (var c in s.contestants)
            {
                if (!Text(c.id, 100) || !Text(c.name, 100) || !Defined(c.status) || c.stats == null || c.traits == null ||
                    c.traits.Count > 20 || c.traits.Any(t => !Text(t, 100)) || c.nominationWeeks == null || c.nominationWeeks.Count > 100 ||
                    c.nominationWeeks.Any(w => w < 1 || w > s.week) || c.timesNominated < 0 || c.hohWins < 0 || c.vetoWins < 0 ||
                    c.timesNominated > 100 || c.hohWins > 100 || c.vetoWins > 100)
                    return Fail(out error, "Invalid contestant data.");
                var stats = new[] { c.stats.physical, c.stats.mental, c.stats.endurance, c.stats.social, c.stats.luck, c.stats.competition, c.stats.strategic, c.stats.loyalty };
                if (stats.Any(x => !Finite(x) || x < 0 || x > 10)) return Fail(out error, "Stats must be finite in the supported 0–10 range.");
            }
            bool Id(string id) => s.contestants.Any(c => c.id == id);
            bool Optional(string id) => string.IsNullOrEmpty(id) || Id(id);
            if (!Optional(s.hohId) || !Optional(s.previousHohId) || !Optional(s.vetoHolderId) || !Optional(s.winnerId) ||
                !Optional(s.runnerUpId) || !Optional(s.finalPart1WinnerId) || !Optional(s.finalPart2WinnerId)) return Fail(out error, "Unknown role identity.");
            if (s.relationships == null || s.relationships.Count > 36 || s.relationships.Any(r => r == null || !Id(r.fromId) || !Id(r.toId) || !Finite(r.score) || Math.Abs(r.score) > 100) ||
                s.relationships.GroupBy(r => new { r.fromId, r.toId }).Any(g => g.Count() > 1)) return Fail(out error, "Invalid directed relationship graph.");
            if (s.nominees == null || s.nominees.Count > 2 || s.nominees.Any(id => !Id(id)) || s.nominees.Distinct().Count() != s.nominees.Count ||
                s.vetoPlayers == null || s.vetoPlayers.Count > 6 || s.vetoPlayers.Any(id => !Id(id)) || s.vetoPlayers.Distinct().Count() != s.vetoPlayers.Count)
                return Fail(out error, "Invalid nomination or veto participant references.");
            if (s.promises == null || s.promises.Count > 200 || s.promises.Any(p => p == null || !Text(p.id, 160) || !Id(p.fromId) || !Id(p.toId) || p.fromId == p.toId ||
                !Optional(p.targetId) || !Defined(p.kind) || !Defined(p.status) || p.week < 1 || p.week > s.week || p.expiresWeek < 0 || p.expiresWeek > 101) ||
                s.promises.GroupBy(p => p.id).Any(g => g.Count() > 1)) return Fail(out error, "Invalid promise data.");
            if (s.alliances == null || s.alliances.Count > 100 || s.alliances.Any(a => a == null || !Text(a.id, 160) || !Text(a.name, 100) || a.members == null ||
                a.members.Count < 2 || a.members.Count > 6 || a.members.Any(id => !Id(id)) || a.members.Distinct().Count() != a.members.Count) ||
                s.alliances.GroupBy(a => a.id).Any(g => g.Count() > 1)) return Fail(out error, "Invalid alliance data.");
            if (s.memories == null || s.memories.Count > 180 || s.memories.Any(m => m == null || !Id(m.ownerId) || !Id(m.subjectId) || !Text(m.text, 2000) || m.week < 1 || m.week > s.week))
                return Fail(out error, "Invalid memory data.");
            if (s.votes == null || s.votes.Count > 6 || s.votes.Any(v => v == null || !Id(v.voterId) || !Id(v.targetId) || v.voterId == v.targetId) ||
                s.votes.GroupBy(v => v.voterId).Any(g => g.Count() > 1)) return Fail(out error, "Invalid votes.");
            // Schema4 simulation may add the existing counter1000 + study5 + base12.5 + clutch5 (+ luck3).
            // Preserve legal v3 counters without clipping source bonus arithmetic at the former 1000 result ceiling.
            if (s.competitionScores == null || s.competitionScores.Count > 6 || s.competitionScores.Any(c => c == null || !Id(c.contestantId) || !Finite(c.score) || c.score < 0 || c.score > 1030) ||
                s.competitionScores.GroupBy(c => c.contestantId).Any(g => g.Count() > 1)) return Fail(out error, "Invalid competition results.");
            if (s.events == null || s.events.Count > 256 || s.events.Any(e => e == null || e.sequence < 1 || e.sequence >= s.nextSequence || e.week < 1 || e.week > s.week ||
                !Defined(e.phase) || !Text(e.kind, 100) || !Text(e.text, 4000) || e.audienceIds == null || e.audienceIds.Any(id => !Id(id))) ||
                s.events.GroupBy(e => e.sequence).Any(g => g.Count() > 1)) return Fail(out error, "Invalid event history.");
            if (s.acceptedCommandIds == null || s.acceptedCommandIds.Count > 256 || s.acceptedCommandIds.Any(id => !Text(id, 160)) ||
                s.acceptedCommandIds.Distinct().Count() != s.acceptedCommandIds.Count) return Fail(out error, "Invalid command receipts.");
            var activeCount = s.Active.Count();
            if (s.phase == EpisodePhase.Finished)
            {
                if (activeCount != 0 || s.contestants.Count(c => c.status == ContestantStatus.Winner) != 1 || s.contestants.Count(c => c.status == ContestantStatus.RunnerUp) != 1 ||
                    s.Find(s.winnerId)?.status != ContestantStatus.Winner || s.Find(s.runnerUpId)?.status != ContestantStatus.RunnerUp) return Fail(out error, "Invalid terminal result.");
            }
            else if (activeCount < 2 || s.contestants.Any(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp)) return Fail(out error, "Invalid active cast.");
            if ((s.phase == EpisodePhase.Jury || s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.FinalSpeeches) && activeCount != 2)
                return Fail(out error, "Jury requires two finalists.");
            if (s.phase == EpisodePhase.Social && activeCount < 3) return Fail(out error, "Free time requires at least three active contestants.");
            if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 1) return Fail(out error, "Nomination requires an empty or complete pair.");
            if ((s.phase == EpisodePhase.FinalHoHPart1 || s.phase == EpisodePhase.FinalHoHPart2 || s.phase == EpisodePhase.FinalHoHPart3 || s.phase == EpisodePhase.FinalEviction) && activeCount != 3)
                return Fail(out error, "Final HoH requires three contestants.");
            if ((s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.VetoSelection || s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !Id(s.hohId))
                return Fail(out error, "This phase requires a HoH.");
            if ((s.phase == EpisodePhase.VetoSelection || s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && s.nominees.Count != 2)
                return Fail(out error, "This phase requires two nominees.");
            if ((s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !Id(s.vetoHolderId)) return Fail(out error, "This phase requires a veto holder.");
            bool Live(string id) => s.Active.Any(c => c.id == id);
            bool weekly = s.phase == EpisodePhase.HoH || s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.VetoSelection ||
                s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction;
            if (weekly && !(s.phase == EpisodePhase.Eviction && s.evictionResolved) && activeCount < 4) return Fail(out error, "Regular weeks require at least four active houseguests.");
            if (weekly && s.phase != EpisodePhase.HoH && !Live(s.hohId)) return Fail(out error, "The current HoH must be active.");
            if (weekly && s.nominees.Contains(s.hohId)) return Fail(out error, "The HoH cannot be nominated.");
            if (weekly && !s.evictionResolved && s.nominees.Any(id => !Live(id))) return Fail(out error, "Current nominees must be active.");
            if ((s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || (s.phase == EpisodePhase.Eviction && !s.evictionResolved)) &&
                (s.vetoPlayers.Count != activeCount || s.vetoPlayers.Any(id => !Live(id)))) return Fail(out error, "The six-person format requires the complete active veto lineup.");
            if ((s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !s.vetoPlayers.Contains(s.vetoHolderId)) return Fail(out error, "The veto holder must have competed.");
            if ((s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !s.vetoResolved) return Fail(out error, "Campaigning requires a committed veto decision.");
            if (s.competitionResolved && IsCompetition(s.phase))
            {
                var participants = CompetitionPlayers(s).Select(c => c.id).ToArray();
                if (participants.Length == 0 || s.competitionScores.Count != participants.Length || s.competitionScores.Any(c => !participants.Contains(c.contestantId))) return Fail(out error, "Resolved competition scores do not match the participants.");
                var winner = s.competitionScores.OrderByDescending(c => c.score).First().contestantId;
                string recorded = s.phase == EpisodePhase.Veto ? s.vetoHolderId : s.phase == EpisodePhase.FinalHoHPart1 ? s.finalPart1WinnerId : s.phase == EpisodePhase.FinalHoHPart2 ? s.finalPart2WinnerId : s.hohId;
                if (recorded != winner || !Live(recorded)) return Fail(out error, "Resolved competition is missing its coherent winner.");
            }
            if ((s.phase == EpisodePhase.FinalHoHPart2 || s.phase == EpisodePhase.FinalHoHPart3 || s.phase == EpisodePhase.FinalEviction) && !Live(s.finalPart1WinnerId)) return Fail(out error, "Final stage is missing the first-part winner.");
            if ((s.phase == EpisodePhase.FinalHoHPart3 || s.phase == EpisodePhase.FinalEviction) && (!Live(s.finalPart2WinnerId) || s.finalPart1WinnerId == s.finalPart2WinnerId)) return Fail(out error, "Final stage requires two distinct qualifying winners.");
            if (s.phase == EpisodePhase.FinalEviction && (!Live(s.hohId) || (s.hohId != s.finalPart1WinnerId && s.hohId != s.finalPart2WinnerId))) return Fail(out error, "Final HoH must be a qualified finalist.");
            if (s.phase == EpisodePhase.Eviction && s.votes.Any(v => !s.nominees.Contains(v.targetId) || s.nominees.Contains(v.voterId))) return Fail(out error, "Invalid eviction ballot eligibility.");
            if (s.phase == EpisodePhase.Eviction && s.votes.Any(v => !Live(v.voterId))) return Fail(out error, "Only active houseguests may cast eviction ballots.");
            if (s.phase == EpisodePhase.Eviction && s.votes.Any(v => v.voterId == s.hohId))
            {
                var regular = Voters(s).ToArray();
                if (!regular.All(c => s.votes.Any(v => v.voterId == c.id)) || s.nominees.Select(id => s.votes.Count(v => v.voterId != s.hohId && v.targetId == id)).Distinct().Count() != 1)
                    return Fail(out error, "HoH ballots are valid only after a complete tied vote.");
            }
            if (s.phase == EpisodePhase.Jury && s.votes.Any(v => !Live(v.targetId) || Live(v.voterId))) return Fail(out error, "Invalid jury ballot eligibility.");
            return TryValidateV2(s, out error) && TryValidateV3(s, out error);
        }

        private static bool Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
        private static bool Defined<T>(T value) where T : struct
        {
            int number = Convert.ToInt32(value);
            int maximum = typeof(T) == typeof(EpisodePhase) ? 15 :
                typeof(T) == typeof(ContestantStatus) ? 4 : typeof(T) == typeof(PromiseKind) ? 4 :
                typeof(T) == typeof(PromiseStatus) ? 3 : -1;
            return number >= 0 && number <= maximum;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Fail(out string error, string message) { error = message; return false; }
        private static bool TryValidateV2(EpisodeState s, out string error)
        {
            error = null;
            bool Id(string id) => s.contestants.Any(c => c.id == id);
            bool Finalist(string id) => s.contestants.Any(c => c.id == id && (c.status == ContestantStatus.Active || c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp));
            bool Juror(string id) => s.contestants.Any(c => c.id == id && (c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted));
            foreach (var actor in s.contestants)
                if (!new[] { "Angry", "Upset", "Neutral", "Content", "Happy" }.Contains(actor.mood) ||
                    !new[] { "Relaxed", "Normal", "Tense", "Stressed", "Overwhelmed" }.Contains(actor.stressLevel))
                    return Fail(out error, "Unsupported mood or stress level.");
            foreach (var edge in s.relationships)
            {
                if (edge.lastInteractionWeek < 0 || edge.lastInteractionWeek > s.week || edge.notes == null || edge.notes.Count > 256 || edge.notes.Any(n => !Text(n, 4000)) ||
                    edge.events == null || edge.events.Count > 512 || edge.events.Any(e => e == null || e.sequence < 1 || e.sequence >= s.nextSequence || e.week < 1 || e.week > s.week ||
                        !Text(e.type, 100) || !Text(e.description, 4000) || !Finite(e.impactScore) || Math.Abs(e.impactScore) > 200))
                    return Fail(out error, "Invalid relationship history.");
            }
            if (s.relationshipArcs == null || s.relationshipArcs.Count > 5 || s.relationshipArcs.Any(a => a == null || !Id(a.npcId) || a.npcId == s.playerId || !Text(a.npcName, 100) ||
                !new[] { "neutral", "friendship", "rivalry" }.Contains(a.arcType) || !Finite(a.intensity) || a.intensity < 0 || a.intensity > 100 ||
                a.escalationLevel != GetEscalationLevel(a.intensity) || a.weeklyHistory == null || a.weeklyHistory.Count > 2000 ||
                a.weeklyHistory.Any(h => h == null || h.week < 1 || h.week > s.week || !Finite(h.delta) || Math.Abs(h.delta) > 200 || !Text(h.reason, 4000))) ||
                s.relationshipArcs.GroupBy(a => a.npcId).Any(g => g.Count() > 1)) return Fail(out error, "Invalid relationship arcs.");
            foreach (var arc in s.relationshipArcs)
            {
                double sentiment = arc.weeklyHistory.Sum(h => h.delta);
                string type = sentiment <= -15 ? "rivalry" : sentiment >= 15 ? "friendship" : "neutral";
                double intensity = Math.Min(100, arc.weeklyHistory.Sum(h => Math.Min(Math.Abs(h.delta) * 1.5, 15)));
                if (type != arc.arcType || Math.Abs(intensity - arc.intensity) > 0.0000001) return Fail(out error, "Arc summary does not match its history.");
            }
            if (s.juryExchanges == null || s.juryExchanges.Count > 4 || s.juryQuestionIndex < 0 || s.juryQuestionIndex > s.juryExchanges.Count ||
                s.finalSpeeches == null || s.finalSpeeches.Count > 2 || s.finalSpeeches.Any(x => x == null || !Finalist(x.speakerId) || x.text == null || x.text.Length > 4000 ||
                    x.isPlayerAuthored != (x.speakerId == s.playerId)) || s.finalSpeeches.GroupBy(x => x.speakerId).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid finale records.");
            bool finale = s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.FinalSpeeches || s.phase == EpisodePhase.Jury || s.phase == EpisodePhase.Finished;
            if (!finale && (s.juryExchanges.Count != 0 || s.juryQuestionIndex != 0 || s.finalSpeeches.Count != 0)) return Fail(out error, "Finale records cannot precede the final eviction.");
            bool playerFinalist = Finalist(s.playerId);
            var questioners = s.contestants.Where(c => Juror(c.id)).Select(c => c.id).ToArray();
            var finalists = s.contestants.Where(c => Finalist(c.id)).Select(c => c.id).ToArray();
            int total = playerFinalist ? questioners.Length : 2;
            if (s.juryExchanges.Count > total) return Fail(out error, "Too many jury exchanges.");
            for (int i = 0; i < s.juryExchanges.Count; i++)
            {
                var q = s.juryExchanges[i];
                if (q == null || !Juror(q.questionerId) || !Finalist(q.finalistId) ||
                    q.questionerId != (playerFinalist ? questioners[i] : s.playerId) ||
                    q.finalistId != (playerFinalist ? s.playerId : finalists[i]) || (!q.completed && i != s.juryQuestionIndex))
                    return Fail(out error, "Invalid jury exchange order or identities.");
                if (playerFinalist)
                {
                    var saved = new WebJuryQuestion { tone = q.tone, question = q.question, optionA = q.optionA, optionB = q.optionB,
                        correctIs = q.correctChoice, opponentAnswer = q.opponentAnswer, trait = WebJuryQuestioning.GetPrimaryTrait(s.Find(q.questionerId).traits) };
                    if (!WebJuryQuestioning.MatchesSource(saved, s.Find(q.questionerId).traits, i) || (q.completed &&
                        ((q.answerChoice != "A" && q.answerChoice != "B") || q.answer != (q.answerChoice == "A" ? q.optionA : q.optionB))))
                        return Fail(out error, "The saved finalist question does not match its source catalogue.");
                }
                else
                {
                    if (q.optionA != null || q.optionB != null || q.correctChoice != null || q.opponentAnswer != null)
                        return Fail(out error, "Player jurors cannot store finalist answer keys.");
                    if (q.completed && (!WebJuryQuestioning.GetJurorQuestionOptions(i).Any(o => o.tone == q.tone && o.text == q.question && q.answerChoice == o.tone) || !Text(q.answer, 4000)))
                        return Fail(out error, "Invalid saved juror question.");
                    if (!q.completed && (q.tone != null || q.question != null)) return Fail(out error, "Unasked juror questions must not have a selected tone.");
                }
                if (!q.completed && (q.answer != null || q.answerChoice != null)) return Fail(out error, "Pending jury answer is partially committed.");
            }
            if (s.phase == EpisodePhase.JuryQuestioning)
            {
                if (s.juryExchanges.Count != s.juryQuestionIndex + 1 || s.juryQuestionIndex >= total || s.finalSpeeches.Count != 0 || s.votes.Count != 0)
                    return Fail(out error, "Questioning requires exactly one current exchange and no early speeches or ballots.");
            }
            else if (s.juryQuestionIndex != s.juryExchanges.Count || s.juryExchanges.Any(q => !q.completed))
                return Fail(out error, "Only questioning may retain an unanswered exchange.");
            if (s.phase == EpisodePhase.FinalSpeeches && (s.votes.Count != 0 || s.Active.Any(c => !c.isPlayer && !s.finalSpeeches.Any(x => x.speakerId == c.id))))
                return Fail(out error, "Final speeches require the saved NPC speeches before voting.");
            // Legacy v1 Jury/Finished saves legitimately have no questioning/speech records.
            return true;
        }
        private static bool TryValidateV3(EpisodeState s, out string error)
        {
            error = null;
            var personas = new[] { "Neutral", "Remorseful", "Ruthless", "Calculated", "Social Butterfly" };
            bool Npc(string id) => s.contestants.Any(c => c.id == id && !c.isPlayer);
            if (s.playerPersona == null || s.playerPersona.scores == null || s.playerPersona.scores.Count != personas.Length ||
                s.playerPersona.scores.Any(x => x == null || !personas.Contains(x.persona) || x.score < 0 || x.score > 100) ||
                s.playerPersona.scores.Select(x => x.persona).Distinct().Count() != personas.Length || s.playerPersona.history == null || s.playerPersona.history.Count > 100 ||
                s.playerPersona.history.Any(x => x == null || !personas.Contains(x.persona) || x.week < 1 || x.week > s.week))
                return Fail(out error, "Invalid persona history or scores.");
            foreach (var score in s.playerPersona.scores)
                if (score.score != s.playerPersona.history.Count(h => h.persona == score.persona)) return Fail(out error, "Persona scores must match the recorded choices.");
            var dominant = s.playerPersona.scores.OrderByDescending(x => x.score).First();
            if (s.playerPersona.current != (dominant.score >= 2 ? dominant.persona : "Neutral")) return Fail(out error, "Persona label does not match the ordered score history.");
            if (s.phaseEventSocialBonus < 0 || s.phaseEventSocialBonus > 1000 || s.phaseEventCompBonus < 0 || s.phaseEventCompBonus > 1000 ||
                s.lastDiaryRoomWeek < 0 || s.lastDiaryRoomWeek > s.week || s.resolvedDiaryIds == null || s.resolvedDiaryIds.Count > 100 ||
                s.resolvedDiaryIds.Any(id => !Text(id, 160)) || s.resolvedDiaryIds.Distinct().Count() != s.resolvedDiaryIds.Count)
                return Fail(out error, "Invalid diary counters or receipts.");
            int lastResolved = 0;
            foreach (var id in s.resolvedDiaryIds)
            {
                const string prefix = "diary-post_eviction-";
                if (!id.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(id.Substring(prefix.Length), out var week) || week < 1 || week > s.week || id != prefix + week)
                    return Fail(out error, "Unsupported resolved diary identity.");
                lastResolved = Math.Max(lastResolved, week);
            }
            if (s.lastDiaryRoomWeek != lastResolved || s.playerPersona.history.Any(h => !s.resolvedDiaryIds.Contains("diary-post_eviction-" + h.week)) ||
                s.playerPersona.history.GroupBy(h => h.week).Any(g => g.Count() > 1)) return Fail(out error, "Diary history must refer to once-per-week resolved reflections.");
            var prompt = s.pendingDiary;
            if (prompt != null && (prompt.trigger != "post_eviction" || prompt.week != s.week || prompt.id != "diary-post_eviction-" + prompt.week ||
                prompt.isNominee || s.Find(prompt.evictedId)?.status != ContestantStatus.Jury || s.Find(s.playerId).status != ContestantStatus.Active ||
                s.lastDiaryRoomWeek >= prompt.week || s.resolvedDiaryIds.Contains(prompt.id) ||
                (s.phase != EpisodePhase.Social && !(s.phase == EpisodePhase.Eviction && s.evictionResolved))))
                return Fail(out error, "Invalid or stale pending diary reflection.");
            if (s.jurySentiment == null || s.jurySentiment.jurors == null || s.jurySentiment.jurors.Count > 4 || !Finite(s.jurySentiment.overallSentiment) ||
                Math.Abs(s.jurySentiment.overallSentiment) > 100 || s.jurySentiment.jurors.Any(j => j == null ||
                    !(s.Find(j.jurorId)?.status == ContestantStatus.Jury || s.Find(j.jurorId)?.status == ContestantStatus.Evicted) || !Text(j.jurorName, 100) ||
                    !Finite(j.sentiment) || Math.Abs(j.sentiment) > 100 || j.events == null || j.events.Count < 1 || j.events.Count > 512 ||
                    j.events.Any(e => e == null || e.week < 0 || e.week > s.week || !Finite(e.delta) || Math.Abs(e.delta) > 200 || !Text(e.reason, 4000))) ||
                s.jurySentiment.jurors.GroupBy(j => j.jurorId).Any(g => g.Count() > 1)) return Fail(out error, "Invalid jury impression ledger.");
            double overall = s.jurySentiment.jurors.Count == 0 ? 0 : JsRound(s.jurySentiment.jurors.Average(j => j.sentiment));
            if (s.jurySentiment.overallSentiment != overall) return Fail(out error, "Jury impression average does not match the recorded jurors.");
            if (s.loyaltyOaths == null || s.loyaltyOaths.Count > 5 || s.loyaltyOaths.Any(o => o == null || o.playerId != s.playerId || !Npc(o.targetId) ||
                o.week < 1 || o.week > s.week || o.timestamp < 1 || o.timestamp >= s.nextSequence) || s.loyaltyOaths.GroupBy(o => o.targetId).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid structured loyalty declaration.");
            if (s.oathOpportunities == null || s.shownOathMilestones == null || s.oathOpportunities.Count > 5 || s.shownOathMilestones.Count > 5 ||
                s.shownOathMilestones.Any(id => !Npc(id)) || s.shownOathMilestones.Distinct().Count() != s.shownOathMilestones.Count ||
                s.oathOpportunities.Any(id => !Npc(id) || s.Find(id).status != ContestantStatus.Active || !s.shownOathMilestones.Contains(id) || s.loyaltyOaths.Any(o => o.targetId == id)) ||
                s.oathOpportunities.Distinct().Count() != s.oathOpportunities.Count)
                return Fail(out error, "Invalid or duplicate loyalty opportunity.");
            if (s.loyaltyOaths.Any(o => !s.shownOathMilestones.Contains(o.targetId))) return Fail(out error, "A native loyalty declaration requires its recorded milestone.");
            return true;
        }
        public static void Validate(EpisodeState state)
        {
            if (!TryValidate(state, out var reason)) throw new InvalidDataException(reason);
            Require(state.schemaVersion == 5, "Unsupported simulation schema version.");
            Require(state.sessionId != null && state.sessionId.Length <= 256, "Session identifier is too long.");
            Require(state.week <= 10000 && state.promises.Count <= 10000 && state.alliances.Count <= 1000
                && state.memories.Count <= 100000 && state.events.Count <= 100000
                && state.acceptedCommandIds.Count <= 100000, "Save exceeds supported collection limits.");
            Require(state.contestants.All(actor => actor.id.Length <= 128 && actor.name.Length <= 128
                && actor.traits.Count <= 64 && actor.traits.All(trait => trait != null && trait.Length <= 128)),
                "Contestant text exceeds supported limits.");
            Require(state.memories.All(memory => memory.text != null && memory.text.Length <= 16384)
                && state.events.All(item => item.text != null && item.text.Length <= 16384),
                "Saved history text is missing or too long.");
            // Simulation validation owns phase/reference/enum semantics and exact
            // per-record invariants. Persistence additionally bounds saved prose.
            Require(state.relationships.All(edge => edge.notes.All(note => note.Length <= 16384)
                && edge.events.All(item => item.description.Length <= 16384)), "Relationship history text is too long.");
            Require(state.relationshipArcs.All(arc => arc.weeklyHistory.All(item => item.reason.Length <= 16384)),
                "Relationship arc history text is too long.");
            Require(state.finalSpeeches.All(speech => speech.text.Length <= 16384), "Final speech is too long.");
            // randomState is deliberately not normalized: zero is a valid wrapped generator state.
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }

        private static bool IsCompetition(EpisodePhase phase) => phase == EpisodePhase.HoH || phase == EpisodePhase.Veto ||
            phase == EpisodePhase.FinalHoHPart1 || phase == EpisodePhase.FinalHoHPart2 || phase == EpisodePhase.FinalHoHPart3;
        private static IEnumerable<ContestantState> CompetitionPlayers(EpisodeState s)
        {
            if (s.phase == EpisodePhase.Veto) return s.Active.Where(c => s.vetoPlayers.Contains(c.id));
            if (s.phase == EpisodePhase.FinalHoHPart2) return s.Active.Where(c => c.id != s.finalPart1WinnerId);
            if (s.phase == EpisodePhase.FinalHoHPart3) return s.Active.Where(c => c.id == s.finalPart1WinnerId || c.id == s.finalPart2WinnerId);
            return s.Active.Where(c => s.Active.Count() <= 3 || c.id != s.previousHohId);
        }
        private static IEnumerable<ContestantState> Voters(EpisodeState s) => s.Active.Where(c => c.id != s.hohId && !s.nominees.Contains(c.id));
        private static int GetEscalationLevel(double intensity) => intensity >= 90 ? 4 : intensity >= 75 ? 3 : intensity >= 50 ? 2 : intensity >= 25 ? 1 : 0;
        private static double JsRound(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value;
            var floor = Math.Floor(value);
            return value - floor < 0.5 ? floor : floor + 1;
        }
    }
}
