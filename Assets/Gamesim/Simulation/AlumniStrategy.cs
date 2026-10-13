#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>Bounded characterization inputs; pure readers, never private-outcome publication.</summary>
    public static class AlumniStrategy
    {
        public static double CareerRisk(EpisodeState state, string id)
        {
            if (!AlumniHistory.TryProfile(state, id, out var profile)) return 0;
            switch (profile.TemplateId)
            {
                case "dan-gheesling": case "dr-will-kirby": case "derrick-levasseur": return 6;
                default: return 3;
            }
        }

        /// <summary>Public historical strong pairs, counted once unless a known current alliance already covers them.</summary>
        public static double KnownPairRisk(EpisodeState state, string evaluatorId, string targetId)
        {
            if (!AlumniHistory.On(state) || !state.playerHistoriesEnabled
                || !AlumniHistory.TryProfile(state, targetId, out var target)
                || state.Find(targetId).status != ContestantStatus.Active) return 0;
            double total = 0;
            foreach (var history in AlumniCatalog.PairHistories.Where(h => h.LeftToRight.Relationship >= 45
                && h.RightToLeft.Relationship >= 45 && (h.LeftTemplateId == target.TemplateId || h.RightTemplateId == target.TemplateId)))
            {
                string partner = AlumniHistory.SubjectId(state, history, targetId);
                if (partner == null || partner == evaluatorId || targetId == evaluatorId
                    || state.Find(partner)?.status != ContestantStatus.Active) continue;
                if (!Knowledge.Knows(Knowledge.Of(state, AlumniHistory.PriorHistory, history.FactId), evaluatorId)) continue;
                if (state.alliances.Any(a => a.active && a.members.Contains(targetId) && a.members.Contains(partner)
                    && Knowledge.AllianceVisibleTo(state, a, evaluatorId))) continue;
                total = Math.Max(total, history.FactId == "hitmen-bb16" ? 8 : 5);
            }
            return total;
        }

        /// <summary>A bigger trusted threat may shield an early-game player; not a forced alliance or endgame protection.</summary>
        public static double ShieldValue(EpisodeState state, string evaluatorId, string targetId)
        {
            if (!AlumniHistory.On(state) || state.Active.Count() <= 6 || evaluatorId == targetId
                || state.Find(evaluatorId)?.status != ContestantStatus.Active || state.Find(targetId)?.status != ContestantStatus.Active
                || state.phase == EpisodePhase.FinalEviction || state.phase == EpisodePhase.Jury || state.phase == EpisodePhase.Finished
                || ThreatAssessment.TrustScore(state, targetId, evaluatorId) < 60) return 0;
            double self = ThreatAssessment.Total(state, evaluatorId, evaluatorId);
            double target = ThreatAssessment.Total(state, evaluatorId, targetId);
            return target <= self + 4 ? 0 : Math.Min(12, (target - self - 4) * .5);
        }

        public static bool ShouldThrow(EpisodeState state, string npcId, bool isFinal, string category)
        {
            if (!AlumniHistory.TryProfile(state, npcId, out var profile) || state.Find(npcId).isPlayer
                || isFinal || state.Active.Count() <= 6 || state.nominees.Contains(npcId)
                || state.phase == EpisodePhase.FinalHoHPart1 || state.phase == EpisodePhase.FinalHoHPart2
                || state.phase == EpisodePhase.FinalHoHPart3 || string.IsNullOrEmpty(category)) return false;
            // This first policy is only the player whose explicit selective-throwing history is audited.
            if (profile.TemplateId != "dr-will-kirby") return false;
            var protection = UnifiedVoteStore.Deals(state).Where(d => d.status == DealStatus.Active
                && (d.proposerId == npcId || d.recipientId == npcId)
                && (d.type == DealKind.FinalTwo || d.type == DealKind.SafetyAgreement || d.type == DealKind.Partnership)).ToList();
            if (protection.Any(d => state.nominees.Contains(d.proposerId == npcId ? d.recipientId : d.proposerId))) return false;
            var ownAllies = state.alliances.Where(a => a.active && a.members.Contains(npcId))
                .SelectMany(a => a.members).Where(id => id != npcId);
            if (ownAllies.Any(state.nominees.Contains)) return false;
            return protection.Select(d => d.proposerId == npcId ? d.recipientId : d.proposerId).Concat(ownAllies).Distinct(StringComparer.Ordinal)
                .Any(partner => state.Find(partner)?.status == ContestantStatus.Active
                    && ThreatAssessment.TrustScore(state, partner, npcId) >= 60);
        }

        public static double CompetitionEffort(EpisodeState state, string npcId, bool isFinal, string category) =>
            ShouldThrow(state, npcId, isFinal, category) ? Math.Min(.5, CompetitionRules.ThrowShare(EpisodeEngine.CompetitionPlayers(state).Count())) : 1;

        public static IReadOnlyList<NpcActionKind> Repertoire(EpisodeState state, ContestantState npc)
        {
            if (!AlumniHistory.TryProfile(state, npc?.id, out var profile)) return NpcSocialActions.Repertoire(NpcSocialActions.LeadTrait(npc));
            switch (profile.TemplateId)
            {
                case "dr-will-kirby": return new[] { NpcActionKind.Talk, NpcActionKind.SpreadInfo, NpcActionKind.Eavesdrop };
                case "dan-gheesling": return new[] { NpcActionKind.Talk, NpcActionKind.Eavesdrop, NpcActionKind.SpreadInfo };
                case "derrick-levasseur": case "cody-calafiore": case "chelsie-baham": case "xavier-prather":
                    return new[] { NpcActionKind.Talk, NpcActionKind.AllianceMeeting, NpcActionKind.SpreadInfo };
                case "vanessa-rousso": case "danielle-reyes": case "tyler-crispen":
                    return new[] { NpcActionKind.Eavesdrop, NpcActionKind.Talk, NpcActionKind.SpreadInfo };
                case "jun-song": return new[] { NpcActionKind.Talk, NpcActionKind.Eavesdrop, NpcActionKind.AllianceMeeting };
                default: return new[] { NpcActionKind.Talk, NpcActionKind.Confront, NpcActionKind.AllianceMeeting };
            }
        }
    }
}
#endif
