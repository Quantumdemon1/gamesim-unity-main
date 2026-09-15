using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public static partial class EpisodeValidation
    {
        public static bool TryValidate(EpisodeState s, out string error)
        {
            error = null;
            if (s == null || s.schemaVersion != 6) return Fail(out error, "Unsupported episode schema.");
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
            if (s.competitionResolved && EpisodeEngine.IsCompetition(s.phase))
            {
                var participants = EpisodeEngine.CompetitionPlayers(s).Select(c => c.id).ToArray();
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
                var regular = EpisodeEngine.Voters(s).ToArray();
                if (!regular.All(c => s.votes.Any(v => v.voterId == c.id)) || s.nominees.Select(id => s.votes.Count(v => v.voterId != s.hohId && v.targetId == id)).Distinct().Count() != 1)
                    return Fail(out error, "HoH ballots are valid only after a complete tied vote.");
            }
            if (s.phase == EpisodePhase.Jury && s.votes.Any(v => !Live(v.targetId) || Live(v.voterId))) return Fail(out error, "Invalid jury ballot eligibility.");
            return TryValidateV2(s, out error) && TryValidateV3(s, out error) && TryValidateNpcSocial(s, out error);
        }

        private static bool Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
        private static bool Defined<T>(T value) where T : struct => Enum.IsDefined(typeof(T), value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Fail(out string error, string message) { error = message; return false; }
    }
}
