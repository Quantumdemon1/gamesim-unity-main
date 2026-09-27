using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// A houseguest walking out: the evicted, borrowed for the walk to the front door and through it.
    ///
    /// <para>Like the opening's borrow it holds the one actor by a reservation token of its own, so
    /// the coordinator knows the lease is legitimate, and it is exempt from the house's pause: the
    /// show is on, and the person leaving still has to walk. Whatever they were doing - a meeting, an
    /// activity - ends first. One at a time; the house evicts one person a week.</para>
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private Actor departing;
        private string departureToken;
        private int departureLegs;

        /// <summary>Who is walking out, or null.</summary>
        public string DepartingActorId => departing?.id;

        /// <summary>
        /// The houseguest being brought back to walk out, while their body takes its navigation back.
        /// A binding that fails for them lets them go - out of the house's world, as an evictee
        /// always was - rather than failing the house: nobody else's movement depends on their walk.
        /// </summary>
        public string DepartureCandidate
        {
            get => departureCandidate;
            set { departureCandidate = value; droppedCandidate = null; }
        }
        private string departureCandidate, droppedCandidate;

        /// <summary>Whether the house let this candidate go because their body could not take its navigation back.</summary>
        public bool CandidateDropped(string id) => id != null && id == droppedCandidate;

        /// <summary>Lets a walk out's candidate go when their body cannot bind; true when that is who it was.</summary>
        private bool DropFailedCandidate(Actor actor)
        {
            if (actor == null || actor.id == null || actor.id != departureCandidate) return false;
            if (actor.motion != null) actor.motion.Unbind();
            eligible.Remove(actor.id);
            droppedCandidate = actor.id;
            return true;
        }

        /// <summary>Whether this houseguest's body is bound and could walk: eligible and holding a navigation binding.</summary>
        public bool CanWalk(string id) =>
            !disposed && id != null && eligible.Contains(id) && actors.TryGetValue(id, out var actor)
            && actor.motion != null && actor.motion.IsBound;

        /// <summary>Borrows one houseguest for their walk out, ending whatever they were doing.</summary>
        public bool BeginDeparture(string id, out string reason)
        {
            reason = null;
            if (disposed || HasCompetitionStage || HasOpeningStage || departing != null)
            { reason = "The house cannot spare anyone to walk out now."; return false; }
            if (!CanWalk(id)) { reason = "That houseguest cannot walk now."; return false; }
            YieldWander(id);
            var actor = actors[id];
            leaseBuffer.Clear(); leaseBuffer.AddRange(leases.Values);
            foreach (var lease in leaseBuffer)
                if (lease.FirstId == id || lease.SecondId == id) Retire(lease, HouseMeetingStatus.Released, null);
            YieldActivity(id);
            if (actor.motion.LeaseId != null) { reason = "That houseguest is still held by something else."; return false; }
            departing = actor;
            departureToken = null;
            departureLegs = 0;
            actor.motion.SetPaused(false);
            return true;
        }

        /// <summary>Sends the houseguest walking out somewhere new, letting go of wherever they were going.</summary>
        public bool RouteDeparture(Vector3 destination, out string reason)
        {
            reason = null;
            if (departing == null || departing.motion == null || !departing.motion.IsBound)
            { reason = "Nobody is walking out."; return false; }
            var motion = departing.motion;
            if (departureToken != null) motion.Release(departureToken);
            departureToken = null;
            motion.SetPaused(false);
            string token = "departure:" + departing.id + ":" + (++departureLegs);
            if (!motion.TryReserveAndPath(token, destination))
            { reason = "There is no route out for " + departing.npc.DisplayName + "."; return false; }
            departureToken = token;
            return true;
        }

        public bool DepartureArrived() =>
            departing != null && departureToken != null && departing.motion != null && departing.motion.HasArrivedAt(departureToken);

        /// <summary>Lets the houseguest go: their route released and the house's pause theirs again.</summary>
        public void EndDeparture()
        {
            if (departing?.motion != null)
            {
                if (departureToken != null) departing.motion.Release(departureToken);
                departing.motion.SetPaused(paused);
            }
            departing = null;
            departureToken = null;
        }

        private bool DepartureHoldsActor(string id) => departing != null && departing.id == id;

        private bool DepartureOwnsMotion(HouseNpcMotion motion) =>
            departing != null && departing.motion == motion && departureToken != null && motion.LeaseId == departureToken;
    }
}
