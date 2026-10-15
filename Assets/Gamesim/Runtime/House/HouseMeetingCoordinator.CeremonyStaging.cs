using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// A ceremony's borrow of the cast (CEREMONY-CUTSCENES-PLAN §1): the house gathered to its seats
    /// for the keys, the veto meeting or the vote, and let go when the beat is over.
    ///
    /// <para>Like the arena's stage and the opening's it holds each houseguest by a reservation token
    /// of its own, so the coordinator knows the lease is legitimate. Like the story's scene stage it
    /// takes whoever is free rather than all or none: a ceremony with somebody missing is still the
    /// ceremony, and the card never waits on a walk. A meeting lease always wins - the phase change
    /// that opens a ceremony cancels the house's conversations, and the stage asks again once the
    /// house has let them go. Like the opening it is exempt from the house's pause: the show is on,
    /// and the people in it still have to walk.</para>
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class CeremonyLease
        {
            public Actor actor;
            public HouseInteractionAnchor place;
            public string token;
        }
        private readonly Dictionary<string, CeremonyLease> ceremonyLeases = new Dictionary<string, CeremonyLease>();

        public bool HasCeremonyStage => ceremonyLeases.Count > 0;
        public int CeremonyStageCount => ceremonyLeases.Count;

        /// <summary>How many of the borrowed have reached their place.</summary>
        public int CeremonyArrivals
        {
            get
            {
                int count = 0;
                foreach (var lease in ceremonyLeases.Values)
                    if (lease.actor.motion != null && lease.actor.motion.IsOnMark(lease.token)) count++;
                return count;
            }
        }

        /// <summary>
        /// Borrows the free houseguests among <paramref name="ids"/> and walks each to its place's
        /// approach. Any earlier ceremony is let go first. Returns how many were sent; whoever was
        /// left out, and why, is named in <paramref name="reason"/>. None while the arena or the
        /// opening has the house.
        /// </summary>
        public int BeginCeremonyStage(IReadOnlyList<string> ids, IReadOnlyList<HouseInteractionAnchor> places, out string reason)
        {
            reason = null;
            EndCeremonyStage();
            if (!Usable || HasCompetitionStage || HasOpeningStage || ids == null || places == null || ids.Count != places.Count)
            { reason = "The house is not free for a ceremony."; return 0; }
            EndWandering();
            EndSceneStage();
            EndActStaging();
            ReleaseActivities();
            var left = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                var place = places[i];
                if (id == null || place == null || !place.isActiveAndEnabled || place.gameObject.scene != rooms.Scene
                    || ceremonyLeases.ContainsKey(id) || !actors.TryGetValue(id, out var actor) || !eligible.Contains(id)
                    || actor.motion == null || !actor.motion.IsBound)
                { left.Add(id + " (no body to walk)"); continue; }
                YieldActivity(id); YieldWander(id);
                if (actor.motion.LeaseId != null) { left.Add(actor.npc.DisplayName + " (in a conversation)"); continue; }
                string token = "ceremony:" + i + ":" + System.Guid.NewGuid().ToString("N");
                actor.motion.SetPaused(false);
                if (!actor.motion.TryReserveAndPath(token, place.Approach))
                {
                    actor.motion.SetPaused(paused);
                    left.Add(actor.npc.DisplayName + " (no route to " + place.VenueId + " " + place.Slot
                        + (actor.motion.LastRouteFailure != null ? ": " + actor.motion.LastRouteFailure : "") + ")");
                    continue;
                }
                ceremonyLeases[id] = new CeremonyLease { actor = actor, place = place, token = token };
            }
            if (left.Count > 0) reason = "Not gathered: " + string.Join("; ", left) + ".";
            return ceremonyLeases.Count;
        }

        /// <summary>
        /// Sends one more houseguest to a place, into a ceremony already gathering: whoever was left
        /// out at the summons because their route was blocked - by somebody still standing where
        /// they were about to walk - is asked again once the house has moved.
        /// </summary>
        public bool JoinCeremonyStage(string id, HouseInteractionAnchor place, out string reason)
        {
            reason = null;
            if (!Usable || HasCompetitionStage || HasOpeningStage || id == null || place == null || !place.isActiveAndEnabled
                || place.gameObject.scene != rooms.Scene) { reason = "The house is not free for a ceremony."; return false; }
            if (ceremonyLeases.ContainsKey(id)) return true;
            if (!actors.TryGetValue(id, out var actor) || !eligible.Contains(id) || actor.motion == null || !actor.motion.IsBound)
            { reason = id + " has no body to walk."; return false; }
            YieldActivity(id); YieldWander(id); YieldAct(id);
            if (actor.motion.LeaseId != null) { reason = actor.npc.DisplayName + " is in a conversation."; return false; }
            string token = "ceremony:" + ceremonyLeases.Count + ":" + System.Guid.NewGuid().ToString("N");
            actor.motion.SetPaused(false);
            if (!actor.motion.TryReserveAndPath(token, place.Approach))
            {
                actor.motion.SetPaused(paused);
                reason = actor.npc.DisplayName + " has no route to " + place.VenueId + " " + place.Slot
                    + (actor.motion.LastRouteFailure != null ? " (" + actor.motion.LastRouteFailure + ")" : "") + ".";
                return false;
            }
            ceremonyLeases[id] = new CeremonyLease { actor = actor, place = place, token = token };
            return true;
        }

        /// <summary>
        /// The least a ceremony needs of the coordinator: it exists and has a generation. Not
        /// <see cref="IsReady"/>, which wants every eligible body bound - and the evicted, re-bound
        /// the frame the stage was created, is still Binding on it, so every eviction's summons
        /// was refused whole and the house only moved on the retry (measured 2026-09-28). The
        /// unbound are left out by name instead, and the retries send them.
        /// </summary>
        private bool Usable => !disposed && Generation != null;

        /// <summary>
        /// Whether a body could be sent to stand here: the floor sampled and the clearance the
        /// arrival test asks for, in the coordinator's own terms, with the reason when not. A stage
        /// asks before it hands out a standing mark, so nobody is placed where the summons would
        /// refuse them every time (measured 2026-09-28: a mark on a NavMesh edge, refused at every
        /// retry, left one of sixteen standing by the screen). <paramref name="self"/> is the local
        /// actor the clearance leaves out, the player's transform as the jury bench passes it.
        /// </summary>
        public bool CanStandAt(Vector3 at, Transform self, out string why)
        {
            why = null;
            if (!Usable) { why = "the house is not up"; return false; }
            if (!rooms.TrySampleFloor(at, 0.3f, filter, 0.25f, out var sampled, out _))
            { why = "no floor within 0.25 m" + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : ""); return false; }
            if (!rooms.HasCapsuleClearance(sampled, 0.3f, 1.8f, self))
            { why = "no room to stand" + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : ""); return false; }
            return true;
        }

        /// <summary>How far a borrowed houseguest's route runs, in metres, or -1 when they hold none: the summons is keyed to the longest.</summary>
        public float CeremonyRouteLength(string id) =>
            ceremonyLeases.TryGetValue(id, out var lease) && lease.actor.motion != null ? lease.actor.motion.RouteLength : -1f;

        /// <summary>
        /// Whether a borrowed houseguest has reached their place: standing on it and stopped,
        /// whoever's capsule is against theirs. The place is theirs, and what touches a body on it
        /// is a neighbour not yet seated or somebody walking past. Asking for the clearance as
        /// well deadlocked a full table (endgame-f34b, 2026-09-29): a walker going round was held
        /// against a body waiting on its approach, and the walker's touch withheld the arrival
        /// that would have seated the body and parked its agent, so neither ever moved again.
        /// </summary>
        public bool CeremonyActorArrived(string id) =>
            ceremonyLeases.TryGetValue(id, out var lease) && lease.actor.motion != null && lease.actor.motion.IsOnMark(lease.token);

        /// <summary>Whether a borrowed houseguest is still bound and holding the ceremony's route.</summary>
        public bool CeremonyActorHolds(string id) =>
            ceremonyLeases.TryGetValue(id, out var lease) && lease.actor.motion != null && lease.actor.motion.IsBound
            && lease.actor.motion.LeaseId == lease.token;

        /// <summary>The place a borrowed houseguest was sent to, or null.</summary>
        public HouseInteractionAnchor CeremonyPlace(string id) => ceremonyLeases.TryGetValue(id, out var lease) ? lease.place : null;

        /// <summary>Keeps the borrowed walking whatever the house's pause says - it is re-applied on every reconcile.</summary>
        public void ResumeCeremonyActors()
        {
            foreach (var lease in ceremonyLeases.Values)
                if (lease.actor.motion != null) lease.actor.motion.SetPaused(false);
        }

        /// <summary>Lets one borrowed houseguest go early: the evicted, whom the walk-out takes next.</summary>
        public void ReleaseCeremonyActor(string id)
        {
            if (id == null || !ceremonyLeases.TryGetValue(id, out var lease)) return;
            if (lease.actor.motion != null)
            {
                lease.actor.motion.Release(lease.token);
                lease.actor.motion.SetPaused(paused);
            }
            ceremonyLeases.Remove(id);
        }

        /// <summary>Lets everybody the ceremony borrowed go back to their own day.</summary>
        public void EndCeremonyStage()
        {
            foreach (var lease in ceremonyLeases.Values)
                if (lease.actor.motion != null)
                {
                    lease.actor.motion.Release(lease.token);
                    lease.actor.motion.SetPaused(paused);
                }
            ceremonyLeases.Clear();
        }

        private bool CeremonyHoldsActor(string id) => ceremonyLeases.ContainsKey(id);

        private bool CeremonyOwnsMotion(HouseNpcMotion motion)
        {
            foreach (var lease in ceremonyLeases.Values)
                if (lease.actor.motion == motion && motion.LeaseId == lease.token) return true;
            return false;
        }
    }
}
