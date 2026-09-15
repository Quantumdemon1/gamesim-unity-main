using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public static partial class EpisodeValidation
    {
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
                a.escalationLevel != WebRelationshipArcs.GetEscalationLevel(a.intensity) || a.weeklyHistory == null || a.weeklyHistory.Count > 2000 ||
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
    }
}
