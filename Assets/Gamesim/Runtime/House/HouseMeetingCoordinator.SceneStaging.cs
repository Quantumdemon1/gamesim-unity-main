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
        /// Borrows the free ones among these houseguests and walks each to the first of the offered
        /// places it can reserve: on the floor, with its own body clear of other actors and of what
        /// blocks sight. Furniture is not part of that test; the director's places have already been
        /// kept clear of it. Any earlier scene is let go first. Returns how many were staged; none
        /// when the house is busy with a stage of its own - a ceremony's included - paused, or
        /// holding a meeting.
        /// </summary>
        public int BeginSceneStage(string key, IReadOnlyList<string> ids, IReadOnlyList<Vector3> places)
        {
            EndSceneStage();
            if (string.IsNullOrEmpty(key) || !IsReady || paused || HasCompetitionStage || HasOpeningStage || HasCeremonyStage
                || leases.Count != 0 || ids == null || places == null || places.Count == 0) return 0;
            var taken = new bool[places.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == null || sceneLeases.ContainsKey(ids[i]) || !actors.TryGetValue(ids[i], out var actor) || !eligible.Contains(ids[i])
                    || actor.motion == null || !actor.motion.IsBound) continue;
                YieldActivity(ids[i]);YieldWander(ids[i]);YieldAct(ids[i]);
                if (actor.motion.LeaseId != null) continue;
                string token = "scene:" + key + ":" + i;
                if (token.Length > 128) token = "scene:" + i + ":" + key.GetHashCode();
                actor.motion.SetPaused(false);
                bool placed = false;
                for (int p = 0; p < places.Count && !placed; p++)
                {
                    if (taken[p] || !actor.motion.TryReserveAndPath(token, places[p])) continue;
                    taken[p] = placed = true;
                }
                if (!placed) { actor.motion.SetPaused(paused); continue; }
                sceneLeases[ids[i]] = new SceneLease { actor = actor, token = token };
            }
            SceneStageKey = sceneLeases.Count > 0 ? key : null;
            return sceneLeases.Count;
        }

        /// <summary>Whether a staged houseguest has reached their place in the scene.</summary>
        public bool SceneActorArrived(string id) =>
            sceneLeases.TryGetValue(id, out var lease) && lease.actor.motion != null && lease.actor.motion.HasArrivedAt(lease.token);

        public bool SceneHoldsActor(string id) => sceneLeases.ContainsKey(id);

        /// <summary>
        /// Walks two of the scene's people to stand <paramref name="apart"/> metres apart about
        /// <paramref name="centre"/>, along the line between where they stand now - the "come
        /// together" step before an embrace (the art brief's R5). Both must be held by this scene,
        /// and both re-path or neither does. Returns whether both are on their way.
        /// </summary>
        public bool TryBringTogether(string key, string first, string second, Vector3 centre, float apart)
        {
            if (string.IsNullOrEmpty(key) || SceneStageKey != key || first == second
                || !sceneLeases.TryGetValue(first ?? "", out var a) || !sceneLeases.TryGetValue(second ?? "", out var b)
                || a.actor.motion == null || b.actor.motion == null) return false;
            var line = b.actor.motion.transform.position - a.actor.motion.transform.position;
            line.y = 0f;
            if (line.sqrMagnitude < 1e-4f) line = Vector3.right;
            line.Normalize();
            var aAt = centre - line * (apart * .5f);
            var bAt = centre + line * (apart * .5f);
            string aToken = a.token + ":together", bToken = b.token + ":together";
            if (aToken.Length > 128 || bToken.Length > 128) return false;
            a.actor.motion.Release(a.token);
            b.actor.motion.Release(b.token);
            bool aOk = a.actor.motion.TryReserveAndPath(aToken, aAt);
            bool bOk = aOk && b.actor.motion.TryReserveAndPath(bToken, bAt);
            if (!bOk)
            {
                // Neither, then: each goes back to holding where it stands under its own token.
                if (aOk) a.actor.motion.Release(aToken);
                a.actor.motion.TryReserveAndPath(a.token, a.actor.motion.transform.position);
                b.actor.motion.TryReserveAndPath(b.token, b.actor.motion.transform.position);
                return false;
            }
            a.token = aToken; b.token = bToken;
            return true;
        }

        /// <summary>Whether both of two people the scene holds stand on the places it last walked them to.</summary>
        public bool SceneActorsArrived(string first, string second) => SceneActorArrived(first) && SceneActorArrived(second);

        /// <summary>
        /// Holds the scene's people while two of them embrace: the director's stale check leaves a
        /// held scene up, so nobody is walked off mid-embrace, until the hold is let go.
        /// </summary>
        public bool SceneHeldForEmbrace { get; private set; }

        public void HoldSceneForEmbrace(bool hold) => SceneHeldForEmbrace = hold && sceneLeases.Count > 0;

        /// <summary>Lets everybody the scene borrowed go back to their own day.</summary>
        public void EndSceneStage()
        {
            SceneHeldForEmbrace = false;
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
