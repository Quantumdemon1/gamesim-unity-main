using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// A houseguest storming off (the art brief's R6): taken from a story scene, a stroll or an
    /// activity and walked, fast, to a room away from the row, where they stand with their back to
    /// it a while.
    ///
    /// <para>Like the walk out it holds the one actor by a reservation token of its own
    /// (<c>storm:&lt;id&gt;:&lt;n&gt;</c>), so the coordinator knows the route is legitimate, and it is
    /// exempt from the house's pause. One storm at a time; presentation only, never saved. It
    /// yields to everything that has a better claim: a meeting, a ceremony, the opening, a
    /// competition or a departure takes the actor back first.</para>
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private Actor storming;
        private string stormToken;
        private int stormLegs;

        /// <summary>Who is storming off, or null.</summary>
        public string StormingActorId => storming?.id;

        public bool IsStorming(string id) => id != null && storming != null && storming.id == id;

        /// <summary>
        /// Takes a houseguest from a scene, a stroll or an activity and sends them to
        /// <paramref name="spot"/> at <paramref name="speed"/> metres a second (the house's own pace
        /// for zero): false, and nothing changed, when somebody else is storming, the house is staged
        /// for something else, a meeting, a conversation, a staged act or any other borrow has them,
        /// or there is no route there. All of that is asked before they are taken from anything: a
        /// storm with nowhere to go leaves them in their scene, their stroll, their activity and their seat.
        /// </summary>
        public bool TryStormOff(string id, Vector3 spot, float speed = 0f)
        {
            if (disposed || !IsReady || HasCompetitionStage || HasOpeningStage || HasCeremonyStage || departing != null
                || id == null || !actors.TryGetValue(id, out var actor) || !eligible.Contains(id)
                || actor.motion == null || !actor.motion.IsBound) return false;
            if (storming != null && storming != actor) return false;
            foreach (var lease in leases.Values)
                if (lease.FirstId == id || lease.SecondId == id) return false;
            if (TalkHoldsActor(id) || ActHoldsActor(id)) return false;
            // A lease nothing below lets go of (anybody else's borrow) refuses here, not after the yields.
            var motion = actor.motion;
            if (motion.LeaseId != null && !(storming == actor && motion.LeaseId == stormToken)
                && !SceneOwnsMotion(motion) && !WanderOwnsMotion(motion) && !ActivityOwnsMotion(motion)) return false;
            if (!motion.CanRouteTo(spot)) return false;
            ReleaseFromSceneStage(id);
            YieldWander(id);
            YieldActivity(id);
            // A body on a seat gets up at it first, as a meeting's pair does.
            StandUp(actor.motion);
            if (storming == actor && stormToken != null) actor.motion.Release(stormToken);
            stormToken = null;
            if (actor.motion.LeaseId != null) { storming = null; return false; }
            string token = "storm:" + id + ":" + (++stormLegs);
            if (token.Length > 128) token = "storm:" + id.GetHashCode() + ":" + stormLegs;
            actor.motion.SetPaused(false);
            if (!actor.motion.TryReserveAndPath(token, spot))
            {
                storming = null;
                actor.motion.SetPaused(paused);
                return false;
            }
            // After the route is taken: taking a route puts the house's pace back.
            if (speed > 0f) actor.motion.SetSpeed(speed);
            storming = actor;
            stormToken = token;
            return true;
        }

        /// <summary>Whether the one storming off has reached where they were going.</summary>
        public bool StormArrived() =>
            storming != null && stormToken != null && storming.motion != null && storming.motion.HasArrivedAt(stormToken);

        /// <summary>Lets them go: their route released and the house's pause theirs again.</summary>
        public void EndStorm()
        {
            if (storming?.motion != null)
            {
                if (stormToken != null) storming.motion.Release(stormToken);
                storming.motion.SetPaused(paused);
            }
            storming = null;
            stormToken = null;
        }

        /// <summary>Something with a better claim wants them: the storm ends where they stand.</summary>
        private void YieldStorm(string id)
        {
            if (IsStorming(id)) EndStorm();
        }

        private bool StormHoldsActor(string id) => IsStorming(id);

        private bool StormOwnsMotion(HouseNpcMotion motion) =>
            storming != null && storming.motion == motion && stormToken != null && motion.LeaseId == stormToken;
    }
}
