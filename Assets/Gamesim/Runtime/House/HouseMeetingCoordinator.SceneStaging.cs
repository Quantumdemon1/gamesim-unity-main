using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// A story scene's borrow of the cast (plan §5.1, <c>BeginSceneStage</c>): the houseguests a
    /// staged beat is about, walked to the room it happens in and held there while it plays.
    ///
    /// <para>Like the arena's stage and the opening's it only borrows idle actors - a meeting lease,
    /// a saved conversation or another stage always wins - and, unlike them, it takes whoever is
    /// free: a scene with fewer people in it than it names is still a scene, and the story's card
    /// never waits on a walk. It is presentation only and never saved, so a reload simply lets them
    /// go. The house's pause holds them where they stand, which is the tableau the card opens over.</para>
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class SceneLease
        {
            public Actor actor;
            public string token;
        }
        private readonly Dictionary<string, SceneLease> sceneLeases = new Dictionary<string, SceneLease>();

        public bool HasSceneStage => sceneLeases.Count > 0;

        /// <summary>What the stage is for - the director's key for the beat - while one is up.</summary>
        public string SceneStageKey { get; private set; }

        public int SceneStageCount => sceneLeases.Count;

        /// <summary>
        /// Borrows the free ones among these houseguests and walks each to its place. Any earlier
        /// scene is let go first. Returns how many were staged; none when the house is busy with a
        /// stage of its own, paused, or holding a meeting.
        /// </summary>
        public int BeginSceneStage(string key, IReadOnlyList<string> ids, IReadOnlyList<Vector3> places)
        {
            EndSceneStage();
            if (string.IsNullOrEmpty(key) || !IsReady || paused || HasCompetitionStage || HasOpeningStage || leases.Count != 0
                || ids == null || places == null || ids.Count != places.Count) return 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == null || sceneLeases.ContainsKey(ids[i]) || !actors.TryGetValue(ids[i], out var actor) || !eligible.Contains(ids[i])
                    || actor.motion == null || !actor.motion.IsBound) continue;
                YieldActivity(ids[i]);YieldWander(ids[i]);
                if (actor.motion.LeaseId != null) continue;
                string token = "scene:" + key + ":" + i;
                if (token.Length > 128) token = "scene:" + i + ":" + key.GetHashCode();
                actor.motion.SetPaused(false);
                if (!actor.motion.TryReserveAndPath(token, places[i])) { actor.motion.SetPaused(paused); continue; }
                sceneLeases[ids[i]] = new SceneLease { actor = actor, token = token };
            }
            SceneStageKey = sceneLeases.Count > 0 ? key : null;
            return sceneLeases.Count;
        }

        /// <summary>Whether a staged houseguest has reached their place in the scene.</summary>
        public bool SceneActorArrived(string id) =>
            sceneLeases.TryGetValue(id, out var lease) && lease.actor.motion != null && lease.actor.motion.HasArrivedAt(lease.token);

        public bool SceneHoldsActor(string id) => sceneLeases.ContainsKey(id);

        /// <summary>Lets everybody the scene borrowed go back to their own day.</summary>
        public void EndSceneStage()
        {
            foreach (var lease in sceneLeases.Values)
                if (lease.actor.motion != null)
                {
                    lease.actor.motion.Release(lease.token);
                    lease.actor.motion.SetPaused(paused);
                }
            sceneLeases.Clear();
            SceneStageKey = null;
        }

        private bool SceneOwnsMotion(HouseNpcMotion motion)
        {
            foreach (var lease in sceneLeases.Values)
                if (lease.actor.motion == motion && motion.LeaseId == lease.token) return true;
            return false;
        }
    }
}
