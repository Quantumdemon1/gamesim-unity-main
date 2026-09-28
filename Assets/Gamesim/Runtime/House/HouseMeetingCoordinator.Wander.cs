using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// The house between its conversations (playtest, 2026-09-27). Outside free time and campaigning
    /// the house is paused, and nothing moved anybody: between competitions and ceremonies the cast
    /// stood wherever the last thing left them. A houseguest nobody has a use for strolls to a spot
    /// the director picks, stands there a while, and goes on.
    ///
    /// <para>Presentation only, like the opening's stage it is modelled on: a reservation token of
    /// its own so the coordinator knows the route is legitimate, exempt from the house's pause, and
    /// never in anything's way - a meeting, an activity, a stage, the opening or a departure takes
    /// the actor back first. Nothing is saved and nothing draws on the season's generator.</para>
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class WanderLease
        {
            public Actor actor;
            public string token;
            public int legs;
        }
        private readonly Dictionary<string, WanderLease> wanderLeases = new Dictionary<string, WanderLease>();

        /// <summary>How many houseguests are out walking.</summary>
        public int WanderingCount => wanderLeases.Count;

        public bool IsWandering(string id) => id != null && wanderLeases.ContainsKey(id);

        /// <summary>
        /// Sends an idle houseguest somewhere: false, and nothing changed, when they are anybody
        /// else's - a meeting, an activity, a stage - or there is no route there.
        /// </summary>
        public bool TryWander(string id, Vector3 destination)
        {
            if (disposed || !IsReady || HasCompetitionStage || HasOpeningStage || HasSceneStage || departing != null
                || id == null || !actors.TryGetValue(id, out var actor) || !eligible.Contains(id)
                || actor.motion == null || !actor.motion.IsBound || TryGetActivity(id, out _)) return false;
            wanderLeases.TryGetValue(id, out var lease);
            if (actor.motion.LeaseId != null && (lease == null || actor.motion.LeaseId != lease.token)) return false;
            if (lease == null) lease = new WanderLease { actor = actor };
            if (lease.token != null) actor.motion.Release(lease.token);
            lease.token = null;
            actor.motion.SetPaused(false);
            string token = "wander:" + id + ":" + (++lease.legs);
            if (!actor.motion.TryReserveAndPath(token, destination))
            {
                wanderLeases.Remove(id);
                actor.motion.SetPaused(paused);
                return false;
            }
            lease.token = token;
            wanderLeases[id] = lease;
            return true;
        }

        /// <summary>Whether a wandering houseguest has reached where they were going.</summary>
        public bool WanderArrived(string id) =>
            wanderLeases.TryGetValue(id, out var lease) && lease.token != null && lease.actor.motion != null
            && lease.actor.motion.HasArrivedAt(lease.token);

        /// <summary>Everybody stops where they are, and is the house's again - paused if the house is.</summary>
        public void EndWandering()
        {
            foreach (var lease in wanderLeases.Values) StopWandering(lease);
            wanderLeases.Clear();
        }

        /// <summary>One houseguest stops where they are: something else wants them.</summary>
        private void YieldWander(string id)
        {
            if (id == null || !wanderLeases.TryGetValue(id, out var lease)) return;
            StopWandering(lease);
            wanderLeases.Remove(id);
        }

        private void StopWandering(WanderLease lease)
        {
            if (lease.actor.motion == null) return;
            if (lease.token != null) lease.actor.motion.Release(lease.token);
            lease.actor.motion.SetPaused(paused && !OpeningHoldsActor(lease.actor.id) && !CeremonyHoldsActor(lease.actor.id) && !DepartureHoldsActor(lease.actor.id));
        }

        private bool WanderHoldsActor(string id) => wanderLeases.ContainsKey(id);

        private bool WanderOwnsMotion(HouseNpcMotion motion)
        {
            foreach (var lease in wanderLeases.Values)
                if (lease.actor.motion == motion && lease.token != null && motion.LeaseId == lease.token) return true;
            return false;
        }
    }
}
