using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Who holds what against whom: the substrate every "you nominated me, now it's war" story
    /// stands on.
    ///
    /// <para><b>Why a store and not ledger types.</b> <see cref="RelationshipLedger.Record"/> writes
    /// both directions, so a "nominated" entry would make the Head of Household resent the nominee
    /// too; and every ledger entry flows into <see cref="ThreatAssessment.TrustScore"/> for every
    /// reader. A directional store keeps the blast radius to the consumers named in
    /// <see cref="StoryConsumers"/>, and it carries the web's severity, stacking and forgiveness,
    /// which the ledger's fade curve cannot express. It also fixes a long-standing hole:
    /// <see cref="RelationshipLedger.HoldsAGrudge"/> has always read false, because no code ever
    /// records a negative permanent ledger type.</para>
    ///
    /// <para>The numbers are the web's (<c>grudge-system.ts</c> and the Big Brother build's live
    /// consequence fold, <c>accepted-consequences/social-reactions.ts</c>): stacking at half the
    /// incoming severity, a hundred at most, two points of decay a week, and forgiveness below ten
    /// once the holder likes the target again (above forty). Nothing here draws a roll.</para>
    /// </summary>
    public static class Grudges
    {
        public const double Ceiling = 100;
        public const double DecayPerWeek = 2;
        public const double ForgiveBelow = 10;
        public const double ForgiveWhenScoreAbove = 40;

        /// <summary>The web's allied multiplier: a betrayal by somebody in an active alliance with you cuts deeper.</summary>
        public const double AlliedMultiplier = 1.2;

        /// <summary>How much a holder resents a target right now; zero when there is nothing.</summary>
        public static double Severity(EpisodeState state, string holderId, string targetId)
        {
            var list = state?.story?.grudges;
            if (list == null) return 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].holderId == holderId && list[i].targetId == targetId) return list[i].severity;
            return 0;
        }

        /// <summary>Everybody still in the house who holds at least this much against a target.</summary>
        public static List<string> HoldersAgainst(EpisodeState state, string targetId, double atLeast) =>
            state?.story?.grudges == null ? new List<string>()
                : state.story.grudges.Where(g => g.targetId == targetId && g.severity >= atLeast
                                                 && state.Find(g.holderId)?.status == ContestantStatus.Active)
                    .Select(g => g.holderId).OrderBy(id => id, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Adds a grudge, or stacks one that exists at half the incoming severity.
        ///
        /// <para>Clamped to a hundred. A holder and a target in an active alliance together take the
        /// web's ×1.2 when the caller says the cause is a betrayal between allies.</para>
        /// </summary>
        public static GrudgeState Add(EpisodeState state, string holderId, string targetId, double severity, string cause,
            bool alliedMultiplier = false)
        {
            if (state?.story == null || holderId == null || targetId == null || holderId == targetId || severity <= 0) return null;
            var holder = state.Find(holderId);
            var target = state.Find(targetId);
            // Only houseguests hold grudges: the player's feelings are the player's to act on.
            if (holder == null || target == null || holder.isPlayer) return null;
            if (alliedMultiplier && state.Allied(holderId, targetId)) severity *= AlliedMultiplier;
            var existing = state.story.grudges.FirstOrDefault(g => g.holderId == holderId && g.targetId == targetId);
            if (existing != null)
            {
                existing.severity = Math.Min(Ceiling, existing.severity + severity * 0.5);
                existing.count = Math.Min(100, existing.count + 1);
                existing.cause = GrudgeCauses.IsKnown(cause) ? cause : existing.cause;
                return existing;
            }
            if (state.story.grudges.Count >= 256) return null;
            var grudge = new GrudgeState
            {
                holderId = holderId, targetId = targetId,
                cause = GrudgeCauses.IsKnown(cause) ? cause : GrudgeCauses.Story,
                severity = Math.Min(Ceiling, Math.Round(severity, 2)), originWeek = state.week, count = 1,
            };
            state.story.grudges.Add(grudge);
            return grudge;
        }

        /// <summary>Makes a grudge smaller. At or below zero it is gone.</summary>
        public static void Ease(EpisodeState state, string holderId, string targetId, double amount)
        {
            if (state?.story == null || amount <= 0) return;
            var existing = state.story.grudges.FirstOrDefault(g => g.holderId == holderId && g.targetId == targetId);
            if (existing == null) return;
            existing.severity = Math.Max(0, existing.severity - amount);
            if (existing.severity <= 0) state.story.grudges.Remove(existing);
        }

        /// <summary>
        /// A week older: every grudge fades by two, and one below ten is forgiven once the holder
        /// likes the target again. At two a week a nomination's seventy outlasts a whole season,
        /// which is the point - "you put me up in week one" is a season-long memory - so real
        /// forgiveness comes through story beats rather than time.
        /// </summary>
        public static void Age(EpisodeState state)
        {
            if (state?.story == null) return;
            foreach (var grudge in state.story.grudges.ToList())
            {
                grudge.severity = Math.Max(0, grudge.severity - DecayPerWeek);
                bool forgiven = grudge.severity <= 0
                    || (grudge.severity < ForgiveBelow && state.Score(grudge.holderId, grudge.targetId) > ForgiveWhenScoreAbove);
                if (forgiven) state.story.grudges.Remove(grudge);
            }
        }

        /// <summary>Drops every grudge held by or against somebody production removed.</summary>
        public static void Forget(EpisodeState state, string id)
        {
            state?.story?.grudges.RemoveAll(g => g.holderId == id || g.targetId == id);
        }

        /// <summary>
        /// The web's rule for a relationship drop (<c>grudge-reactions.ts</c>, and the live fold):
        /// a fall of eight or more leaves a grudge of five times the fall, and a fall of fifteen or
        /// more leaves half of it the other way too. Applied to story effects only - the engine's
        /// ordinary social traffic keeps its own rules.
        /// </summary>
        public static void FromDrop(EpisodeState state, string actorId, string affectedId, double delta)
        {
            if (delta > -8) return;
            double severity = Math.Min(Ceiling, Math.Abs(delta) * 5);
            Add(state, affectedId, actorId, severity, GrudgeCauses.Story);
            if (delta <= -15) Add(state, actorId, affectedId, Math.Round(severity * 0.5), GrudgeCauses.Story);
        }

        /// <summary>
        /// The web fold's threat scaling for a broken word: severity × (1 + threat/200), where the
        /// threat is how dangerous the breaker looks to the holder, capped at a hundred.
        /// </summary>
        public static double ThreatScaled(EpisodeState state, double severity, string holderId, string breakerId)
            => ThreatScaledBeforeSafetyEffects(state, severity, holderId, breakerId, null);

        internal static double ThreatScaledBeforeSafetyEffects(EpisodeState state, double severity, string holderId,
            string breakerId, IReadOnlyList<string> excludedSafetyEffects)
            => ThreatScaledBeforeCommitmentEffects(state, severity, holderId, breakerId, excludedSafetyEffects, null);

        /// <summary>The same, a reveal's own Vote effects excluded from the breaker's reputation too (vote family V5c).</summary>
        internal static double ThreatScaledBeforeCommitmentEffects(EpisodeState state, double severity, string holderId,
            string breakerId, IReadOnlyList<string> excludedSafetyEffects, IReadOnlyCollection<string> excludedVoteEffects)
        {
            double threat = Math.Max(0, Math.Min(100,
                ThreatAssessment.TotalBeforeCommitmentEffects(state, holderId, breakerId, excludedSafetyEffects, excludedVoteEffects)));
            return Math.Round(severity * (1 + threat / 200));
        }
    }
}
