using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// The opening's borrow of the cast: every idle houseguest, walked through the front door and
    /// home again on routes the opening chooses one at a time.
    ///
    /// <para>Like the arena's stage it only borrows idle actors - a meeting lease or a saved
    /// conversation always wins - and it holds each one by a reservation token of its own, so the
    /// coordinator knows the lease is legitimate. Unlike the arena's it is exempt from the house's
    /// pause: the opening plays with the house paused, because the show is on and nobody should be
    /// wandering off, and the people in it still have to walk.</para>
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class OpeningLease
        {
            public Actor actor;
            public string token;
            public int legs;
        }
        private readonly Dictionary<string, OpeningLease> openingLeases = new Dictionary<string, OpeningLease>();

        public bool HasOpeningStage => openingLeases.Count > 0;

        /// <summary>Borrows these houseguests for the opening. All or none: a stage missing somebody is not staged.</summary>
        public bool BeginOpeningStage(IReadOnlyList<string> ids, out string reason)
        {
            reason = null;
            EndWandering();
            if (!IsReady || HasCompetitionStage || HasOpeningStage || leases.Count != 0 || ids == null || ids.Count == 0)
            { reason = "Houseguests are not available for the opening."; return false; }
            var unique = new HashSet<string>();
            foreach (var id in ids)
                if (!unique.Add(id) || !actors.TryGetValue(id, out var actor) || !eligible.Contains(id)
                    || actor.motion == null || !actor.motion.IsBound || actor.motion.LeaseId != null)
                { reason = "The opening asked for an unavailable houseguest."; return false; }
            ReleaseActivities();
            foreach (var id in ids)
            {
                var actor = actors[id];
                openingLeases[id] = new OpeningLease { actor = actor };
                actor.motion.SetPaused(false);
            }
            return true;
        }

        /// <summary>Sends a borrowed houseguest somewhere new, letting go of wherever they were going.</summary>
        public bool RouteOpeningActor(string id, Vector3 destination, out string reason)
        {
            reason = null;
            if (!openingLeases.TryGetValue(id, out var lease) || lease.actor.motion == null || !lease.actor.motion.IsBound)
            { reason = "That houseguest is not on the opening's stage."; return false; }
            var motion = lease.actor.motion;
            if (lease.token != null) motion.Release(lease.token);
            lease.token = null;
            motion.SetPaused(false);
            string token = "opening:" + id + ":" + (++lease.legs);
            if (!motion.TryReserveAndPath(token, destination))
            { reason = "There is no route for " + lease.actor.npc.DisplayName + " there."; return false; }
            lease.token = token;
            return true;
        }

        public bool OpeningActorArrived(string id) =>
            openingLeases.TryGetValue(id, out var lease) && lease.token != null && lease.actor.motion != null
            && lease.actor.motion.HasArrivedAt(lease.token);

        /// <summary>Whether a borrowed houseguest is still bound and holding the opening's route, or idle on it.</summary>
        public bool OpeningActorValid(string id) =>
            openingLeases.TryGetValue(id, out var lease) && lease.actor.motion != null && lease.actor.motion.IsBound
            && (lease.actor.motion.LeaseId == null || lease.actor.motion.LeaseId == lease.token);

        /// <summary>Keeps the borrowed walking whatever the house's pause says - it is re-applied on every reconcile.</summary>
        public void ResumeOpeningActors()
        {
            foreach (var lease in openingLeases.Values)
                if (lease.actor.motion != null) lease.actor.motion.SetPaused(false);
        }

        public void EndOpeningStage()
        {
            foreach (var lease in openingLeases.Values)
                if (lease.actor.motion != null)
                {
                    if (lease.token != null) lease.actor.motion.Release(lease.token);
                    lease.actor.motion.SetPaused(paused);
                }
            openingLeases.Clear();
        }

        private bool OpeningHoldsActor(string id) => openingLeases.ContainsKey(id);

        private bool OpeningOwnsMotion(HouseNpcMotion motion)
        {
            foreach (var lease in openingLeases.Values)
                if (lease.actor.motion == motion && lease.token != null && motion.LeaseId == lease.token) return true;
            return false;
        }
    }
}
