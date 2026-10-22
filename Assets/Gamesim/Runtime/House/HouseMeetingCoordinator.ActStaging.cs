using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// One of the house's acts staged where it happened (WAVE-D-NPC-PACTS-PLAN §4.4, D2-S3): the two of it walked
    /// to two places in its room and held there while it can still be seen, so the player who walks in sees them.
    ///
    /// <para>A lease of its own, never one of the two pair slots a conversation uses, and exempt from the house's
    /// pause, as a stroll is: the windows after the Head of Household and the nominations run while the house is
    /// paused. It is the least of the house's claims. It borrows only somebody nobody else has - a conversation, a
    /// stage, the opening, a walk out, a talk spot - takes them off furniture or a stroll as a meeting would, and
    /// yields them to every one of those: a saved conversation's reunion, a ceremony, a scene, the arena, the
    /// opening, the walk out and the player's talk all take them back first. Presentation only, never saved: a
    /// reload stages the act again while it is still open.</para>
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class ActLease
        {
            public Actor first, second;
            public string firstToken, secondToken;
        }
        private readonly Dictionary<string, ActLease> actLeases = new Dictionary<string, ActLease>(System.StringComparer.Ordinal);

        /// <summary>How many acts are staged.</summary>
        public int StagedActCount => actLeases.Count;

        /// <summary>The ids of the acts staged.</summary>
        public IEnumerable<string> StagedActIds => actLeases.Keys;

        /// <summary>Whether this act is staged.</summary>
        public bool IsActStaged(string actId) => actId != null && actLeases.ContainsKey(actId);

        /// <summary>Whether a staged act holds this houseguest.</summary>
        public bool ActHoldsActor(string id)
        {
            if (id == null) return false;
            foreach (var lease in actLeases.Values)
                if (lease.first.id == id || lease.second.id == id) return true;
            return false;
        }

        /// <summary>
        /// Borrows the two for an act and walks each to their place. False, and nobody disturbed, when the house
        /// is busy with a stage of its own, the opening, a walk out or a scene; when either is anybody else's -
        /// a conversation, a stage, a talk - or has no route there.
        /// </summary>
        public bool TryStageAct(string actId, string firstId, string secondId, Vector3 firstPlace, Vector3 secondPlace)
        {
            if (disposed || !IsReady || actId == null || actLeases.ContainsKey(actId) || HasCompetitionStage || HasOpeningStage
                || HasCeremonyStage || HasSceneStage || departing != null || firstId == null || secondId == null || firstId == secondId
                || ActHoldsActor(firstId) || ActHoldsActor(secondId)
                || !actors.TryGetValue(firstId, out var first) || !actors.TryGetValue(secondId, out var second)
                || !eligible.Contains(firstId) || !eligible.Contains(secondId)
                || first.motion == null || second.motion == null || !first.motion.IsBound || !second.motion.IsBound) return false;
            // Furniture and a stroll give way, as they do to a meeting; anything else holding them wins.
            if (!Free(first) || !Free(second)) return false;
            YieldActivity(firstId); YieldActivity(secondId); YieldWander(firstId); YieldWander(secondId);
            if (first.motion.LeaseId != null || second.motion.LeaseId != null) return false;
            string token = "act:" + System.Guid.NewGuid().ToString("N");
            string firstToken = token + ":a", secondToken = token + ":b";
            first.motion.SetPaused(false); second.motion.SetPaused(false);
            if (!first.motion.TryReserveAndPath(firstToken, firstPlace))
            {
                first.motion.SetPaused(paused); second.motion.SetPaused(paused);
                return false;
            }
            if (!second.motion.TryReserveAndPath(secondToken, secondPlace))
            {
                first.motion.Release(firstToken);
                first.motion.SetPaused(paused); second.motion.SetPaused(paused);
                return false;
            }
            actLeases[actId] = new ActLease { first = first, second = second, firstToken = firstToken, secondToken = secondToken };
            return true;
        }

        /// <summary>Whether nothing but furniture or a stroll holds this actor.</summary>
        private bool Free(Actor actor) => actor.motion.LeaseId == null || ActivityOwnsMotion(actor.motion) || WanderOwnsMotion(actor.motion);

        /// <summary>Whether both of an act's two stand at their places.</summary>
        public bool ActArrived(string actId) =>
            actId != null && actLeases.TryGetValue(actId, out var lease) && lease.first.motion != null && lease.second.motion != null
            && lease.first.motion.IsOnMark(lease.firstToken) && lease.second.motion.IsOnMark(lease.secondToken);

        /// <summary>Lets an act's two go back to their own day.</summary>
        public void ReleaseAct(string actId)
        {
            if (actId == null || !actLeases.TryGetValue(actId, out var lease)) return;
            actLeases.Remove(actId);
            Let(lease.first, lease.firstToken);
            Let(lease.second, lease.secondToken);
        }

        /// <summary>Lets go every act whose two are no longer both the house's to hold: unbound, ineligible, or taken by something else.</summary>
        public void ReleaseInvalidActs()
        {
            foreach (var entry in new List<KeyValuePair<string, ActLease>>(actLeases))
                if (!eligible.Contains(entry.Value.first.id) || !eligible.Contains(entry.Value.second.id)
                    || !ValidActor(entry.Value.first, entry.Value.firstToken) || !ValidActor(entry.Value.second, entry.Value.secondToken))
                    ReleaseAct(entry.Key);
        }

        /// <summary>Lets every staged act go.</summary>
        public void EndActStaging()
        {
            foreach (string actId in new List<string>(actLeases.Keys)) ReleaseAct(actId);
        }

        /// <summary>The act holding this houseguest lets both of its two go: something else wants one of them.</summary>
        private void YieldAct(string id)
        {
            if (id == null) return;
            foreach (var entry in new List<KeyValuePair<string, ActLease>>(actLeases))
                if (entry.Value.first.id == id || entry.Value.second.id == id) ReleaseAct(entry.Key);
        }

        private void Let(Actor actor, string token)
        {
            // Only a body still on this act's route: one something else has taken is that thing's to pause.
            if (actor.motion == null || actor.motion.LeaseId != token) return;
            actor.motion.Release(token);
            string id = actor.id;
            actor.motion.SetPaused(paused && !OpeningHoldsActor(id) && !CeremonyHoldsActor(id) && !DepartureHoldsActor(id)
                && !WanderHoldsActor(id) && !TalkHoldsActor(id));
        }

        private bool ActOwnsMotion(HouseNpcMotion motion)
        {
            foreach (var lease in actLeases.Values)
                if (lease.first.motion == motion && motion.LeaseId == lease.firstToken
                    || lease.second.motion == motion && motion.LeaseId == lease.secondToken) return true;
            return false;
        }
    }
}
