#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        // Runs only on a detached accepted-command candidate. A knowledge reader never clears
        // history, and a hidden ballot never supplies the evidence needed to clear it.
        private static void ReconcileAlumniKnownBreaches(EpisodeState state)
        {
            if (!AlumniHistory.On(state) || !state.playerHistoriesEnabled) return;
            // Mode 2 keeps Safety exclusively in its canonical family, outside RawPromises/RawDeals.
            // Its binding breach is a nomination the wronged person observes in the house.
            if (UnifiedCommitments.SafetyAuthorityOn(state))
                foreach (var incident in UnifiedCommitmentHistory.Breaches(state))
                    AlumniHistory.InvalidatePositivePrior(state, incident.WrongedId, incident.ActorId);
            foreach (var deal in CommitmentReferences.RawDeals(state).Where(d => d.status == DealStatus.Broken))
            {
                string actor = Breaches.DealBreaker(state, deal);
                if (actor == null) continue;
                string wronged = DealResolution.Partner(deal, actor);
                if (wronged == null) continue;
                bool known = !KnownBallots.SettledByABallot(deal)
                    || (wronged == state.playerId ? KnownBallots.DealOutcomeKnown(state, deal)
                        : AlumniBallotKnown(state, wronged, actor, deal.settledWeek));
                if (known) AlumniHistory.InvalidatePositivePrior(state, wronged, actor);
            }
            foreach (var promise in CommitmentReferences.RawPromises(state).Where(p => p.status == PromiseStatus.Broken))
            {
                string actor = Breaches.PromiseBreaker(promise);
                bool known = promise.kind != PromiseKind.Vote
                    || (promise.toId == state.playerId ? KnownBallots.PromiseOutcomeKnown(state, promise)
                        : AlumniBallotKnown(state, promise.toId, actor, promise.settledWeek));
                if (known) AlumniHistory.InvalidatePositivePrior(state, promise.toId, actor);
            }
            foreach (var npc in state.contestants.Where(c => c.id != state.playerId))
                if (Allegiance.KnownBetrayed(state, npc.id))
                    AlumniHistory.InvalidatePositivePrior(state, state.playerId, npc.id);
        }

        private static bool AlumniBallotKnown(EpisodeState state, string observer, string voter, int week)
        {
            if (week < 1 || voter == null || observer == null) return false;
            if (observer == voter || KnownBallots.ProvenFor(state, week, observer).ContainsKey(voter)) return true;
            return state.events.Where(e => e.week == week && e.kind == "vote-reveal"
                    && (e.audienceIds.Count == 0 || e.audienceIds.Contains(observer)))
                .Any(e => KnownBallots.ReadRevealLine(state, e.text)?.voterId == voter);
        }
    }
}
#endif
