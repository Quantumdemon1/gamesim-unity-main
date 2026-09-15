using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public static partial class EpisodeValidation
    {
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
            double overall = s.jurySentiment.jurors.Count == 0 ? 0 : WebRules.JsRound(s.jurySentiment.jurors.Average(j => j.sentiment));
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
    }
}
