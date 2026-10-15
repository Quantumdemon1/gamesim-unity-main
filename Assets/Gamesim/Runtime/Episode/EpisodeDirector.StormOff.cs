using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Episode
{
    /// <summary>
    /// A storm-off walked (the art brief's R6): the houseguest the story-reaction table has storm
    /// off plays the beat, and then goes - fast, not running - to a room away from the row, turns
    /// their back on it, looks at the floor a while, and is the house's again.
    ///
    /// <para>Where (decision D4): their own room; the Bedroom when the row was in their own room (or
    /// their room is the diary room or the Head of Household's, neither a place to sulk); the Yard
    /// when the row was in the Bedroom. When (decision D5): the veto meeting's nominee once the scene
    /// that staged them ends, so Step in still finds them in the living room; the one the house
    /// turned on once the fallout's card has gone.</para>
    ///
    /// <para>Presentation only: nothing is saved, nothing is committed, and a reload drops the
    /// storm. Never the player's body. Under reduced motion, in a batch run (unless
    /// <see cref="StormOffsInBatchRuns"/>, as <see cref="WalkOutsInBatchRuns"/>), or while a
    /// ceremony, a competition, the opening or a walk out has the house, it is a look away only; with
    /// no spot or no route there, a turn away where they stand.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>Whether a storm walks in a batch run. Off by default: the audited season walks none.</summary>
        public bool StormOffsInBatchRuns { get; set; }

        /// <summary>How long the beat plays before the walk; the walk's pace; how long onlookers watch it go.</summary>
        public const float StormStartSeconds = .9f, StormSpeed = 2.6f, StormLookSeconds = 3f;
        /// <summary>How far a storm's spot stands from anybody else; how long the walk may take before they stop where they are.</summary>
        public const float StormClearOfOthers = 2f, StormWalkSeconds = 25f;
        /// <summary>
        /// How much farther than the body's own capsule a storm's spot keeps clear of furniture: the
        /// agent stops up to its stopping distance (0.15 m) short of where it was sent.
        /// </summary>
        public const float StormSpotMargin = .15f;

        /// <summary>How long a storm sulks once there (12 s; a test may shorten it).</summary>
        public float StormSulkSeconds { get; set; } = 12f;

        /// <summary>How long the walk may take before they stop where they are (<see cref="StormWalkSeconds"/>; a test may shorten it).</summary>
        public float StormWalkLimitSeconds { get; set; } = StormWalkSeconds;

        public enum StormStage { None, Beat, Walking, Sulking }

        private StormStage stormStage;
        private string stormId, stormRoom, stormFromRoom;
        private float stormClock;
        private long stormGeneration;
        private GameObject stormGaze;
        private string stormAfterCard, stormAfterCardRoom;
        private long stormAfterCardGeneration;

        /// <summary>Who is storming off, or null; for the tests.</summary>
        public string StormingId => stormStage == StormStage.None ? null : stormId;
        /// <summary>Where they are going; for the tests.</summary>
        public string StormRoom => stormStage == StormStage.None ? null : stormRoom;
        public StormStage StormNow => stormStage;
        /// <summary>Whether the house holds the storm's walk (its storm: lease); for the tests.</summary>
        public bool StormLeaseHeld => npcMeetings != null && stormId != null && npcMeetings.IsStorming(stormId);
        /// <summary>Whether anyone at all is held by a storm lease; for the tests.</summary>
        public bool AnyStormLease => npcMeetings != null && npcMeetings.StormingActorId != null;

        /// <summary>The room a storm goes to, by decision D4, from the room the row was in.</summary>
        public static string StormRoomFor(string homeRoom, string fromRoom)
        {
            string home = homeRoom;
            if (string.IsNullOrEmpty(home) || home == "Private" || home == "HoH") home = "Bedroom";
            if (home != fromRoom) return home;
            return fromRoom != "Bedroom" ? "Bedroom" : "Yard";
        }

        /// <summary>Why a storm cannot walk now, or null.</summary>
        private string StormRefusal(EpisodeState state, string id)
        {
            if (reducedMotion) return "motion is reduced";
            if (Application.isBatchMode && !StormOffsInBatchRuns) return "storm-offs are off in batch runs";
            if (npcMeetings == null || !npcMeetings.IsReady || state == null) return "the house is not ready";
            if (IsCeremonyStaged || npcMeetings.HasCeremonyStage || npcMeetings.HasCompetitionStage || npcMeetings.HasOpeningStage
                || OpeningOwnsHouse || challengeActive) return "the house is staged for something else";
            if (!string.IsNullOrEmpty(departingId) || !string.IsNullOrEmpty(WalkingOutId)) return "somebody is leaving";
            if (id == state.playerId) return "the player's body acts only on the player's word";
            if (state.Find(id)?.status != ContestantStatus.Active) return id + " is not in the house";
            return null;
        }

        /// <summary>The one the house turned on storms off once the fallout's card has gone.</summary>
        private void StormAfterTheCard(EpisodeState state, string id, string contentId)
        {
            if (state == null || string.IsNullOrEmpty(id)) return;
            var beat = state.houseEvents.LastOrDefault(item => item.IsStory && item.contentId == contentId);
            stormAfterCard = id;
            stormAfterCardRoom = VenueRoom(beat?.venue);
            stormAfterCardGeneration = loadGeneration;
        }

        /// <summary>
        /// Begins a storm: the beat (unless it has played already), and the walk
        /// <see cref="StormStartSeconds"/> later; or the look away, where it cannot walk.
        /// </summary>
        private void BeginStorm(string id, string fromRoom, bool playBeat)
        {
            var state = projected;
            if (state == null || string.IsNullOrEmpty(id) || id == state.playerId) return;
            var body = BodyFor(id);
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual == null) return;
            string room = StormRoomFor(state.Find(id)?.homeRoom, fromRoom);
            if (StormRefusal(state, id) != null)
            {
                LookAway(visual, room);
                return;
            }
            EndStormHere();
            if (playBeat) ReactStory(id, CharacterPresentation.Reaction.StormOff, null);
            stormId = id;
            stormRoom = room;
            stormFromRoom = fromRoom;
            stormStage = StormStage.Beat;
            stormClock = Time.unscaledTime + StormStartSeconds;
            stormGeneration = loadGeneration;
        }

        private void TickStorm()
        {
            if (stormAfterCard != null)
            {
                if (stormAfterCardGeneration != loadGeneration) stormAfterCard = null;
                else if (takeover == null || !takeover.IsPlaying)
                {
                    string id = stormAfterCard;
                    stormAfterCard = null;
                    BeginStorm(id, stormAfterCardRoom, false);
                }
            }
            if (stormStage == StormStage.None) return;
            var state = projected;
            // A season replaced, a fixture installed, the world handed over: the storm is history.
            if (stormGeneration != loadGeneration || StormRefusal(state, stormId) != null) { EndStormHere(); return; }
            var body = BodyFor(stormId);
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual == null) { EndStormHere(); return; }
            switch (stormStage)
            {
                case StormStage.Beat:
                    if (Time.unscaledTime < stormClock) return;
                    if (!TryPickStormSpot(body, stormRoom, out var spot) || !npcMeetings.TryStormOff(stormId, spot, StormSpeed))
                    {
                        // Nowhere to go, or no way there: a turn away from the row, where they stand.
                        TurnAwayFrom(visual, body, stormFromRoom);
                        EndStormHere();
                        return;
                    }
                    visual.SetRunning(false);
                    foreach (var npc in housemates ?? Enumerable.Empty<HouseNpc>())
                    {
                        if (npc == null || npc.Id == stormId || !npc.gameObject.activeInHierarchy) continue;
                        if ((npc.transform.position - body.position).sqrMagnitude > 64f) continue;
                        var onlooker = npc.GetComponent<CharacterPresentation>();
                        if (onlooker != null) onlooker.LookAt(body, StormLookSeconds);
                    }
                    stormStage = StormStage.Walking;
                    stormClock = Time.unscaledTime + StormWalkLimitSeconds;
                    break;
                case StormStage.Walking:
                    // A meeting, a ceremony or a departure took them back: the storm is over.
                    if (!npcMeetings.IsStorming(stormId)) { EndStormHere(); return; }
                    bool arrived = npcMeetings.StormArrived();
                    if (!arrived && Time.unscaledTime < stormClock) return;
                    // Out of time short of the spot: they stop where they stand and sulk there, rather
                    // than sulk while the route walks them on. Where they cannot be held, the storm is over.
                    if (!arrived && !npcMeetings.StormHoldHere()) { EndStormHere(); return; }
                    Sulk(visual, body);
                    stormStage = StormStage.Sulking;
                    stormClock = Time.unscaledTime + StormSulkSeconds;
                    break;
                case StormStage.Sulking:
                    if (!npcMeetings.IsStorming(stormId) || Time.unscaledTime >= stormClock) EndStormHere();
                    break;
            }
        }

        /// <summary>Their back to the room's middle and their eyes on the floor in front of them.</summary>
        private void Sulk(CharacterPresentation visual, Transform body)
        {
            var marker = RoomMarker(stormRoom);
            var away = marker != null ? body.position - marker.transform.position : body.forward;
            away.y = 0f;
            if (away.sqrMagnitude < .01f) away = body.forward;
            away.y = 0f;
            if (away.sqrMagnitude < .01f) return;
            away.Normalize();
            visual.SetFacing(Quaternion.LookRotation(away).eulerAngles.y);
            if (stormGaze == null) stormGaze = new GameObject("Storm-off gaze");
            stormGaze.transform.position = body.position + away * 1.1f;
            visual.LookAtPoint(stormGaze.transform, StormSulkSeconds);
        }

        private void LookAway(CharacterPresentation visual, string room)
        {
            var marker = room != null ? RoomMarker(room) : null;
            if (marker != null) visual.LookAt(marker.transform, StormLookSeconds);
        }

        private void TurnAwayFrom(CharacterPresentation visual, Transform body, string room)
        {
            var marker = room != null ? RoomMarker(room) : null;
            if (marker == null) return;
            var away = body.position - marker.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > .01f) visual.SetFacing(Quaternion.LookRotation(away).eulerAngles.y);
        }

        /// <summary>The storm's end, whatever ended it: the lease let go, the gaze cleared.</summary>
        private void EndStormHere()
        {
            if (npcMeetings != null && stormId != null && npcMeetings.IsStorming(stormId)) npcMeetings.EndStorm();
            if (stormGaze != null) Destroy(stormGaze);
            stormGaze = null;
            stormStage = StormStage.None;
            stormId = stormRoom = stormFromRoom = null;
        }

        /// <summary>For the tests: the spot a storm by this houseguest to this room would walk to now, picked as the walk picks it.</summary>
        public bool StormSpotForDiagnostics(string id, string room, out Vector3 spot)
        {
            spot = default;
            var body = BodyFor(id);
            return body != null && TryPickStormSpot(body, room, out spot);
        }

        private static readonly Collider[] stormFurniture = new Collider[16];

        /// <summary>
        /// Whether a body standing here would stand in furniture. The clearance query reads the sight
        /// layers, which leave the furniture out, and the NavMesh has no hole under a low prop or a
        /// seat cloned at run time; so the furniture layer itself is asked, with the body's own
        /// capsule and <see cref="StormSpotMargin"/>, five centimetres off the floor as the ceremony
        /// marks ask it. The body's own colliders do not count (a parked body stands on that layer).
        /// </summary>
        private bool StormSpotInFurniture(Vector3 feet, float radius, float height, Transform body)
        {
            Physics.SyncTransforms();
            float reach = radius + StormSpotMargin;
            var bottom = feet + Vector3.up * (reach + .05f);
            var top = feet + Vector3.up * Mathf.Max(reach + .05f, height - radius);
            int count = gameObject.scene.GetPhysicsScene().OverlapCapsule(bottom, top, reach,
                stormFurniture, 1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore);
            // An exactly-full buffer is unknown, not evidence of clear floor.
            if (count == stormFurniture.Length) return true;
            for (int i = 0; i < count; i++)
                if (stormFurniture[i] != null && !stormFurniture[i].transform.IsChildOf(body)) return true;
            return false;
        }

        /// <summary>
        /// A spot in the room to storm to: on that room's own floor, the body clear of everything -
        /// the furniture included (<see cref="StormSpotInFurniture"/>) - and
        /// <see cref="StormClearOfOthers"/> from everybody else; a spiral out from the room's
        /// marker, never the marker itself, so there is a middle to turn their back on.
        /// </summary>
        private bool TryPickStormSpot(Transform body, string room, out Vector3 spot)
        {
            spot = default;
            var marker = room != null ? RoomMarker(room) : null;
            if (marker == null || !HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out _)) return false;
            var capsule = body.GetComponent<CapsuleCollider>();
            float radius = capsule != null ? capsule.radius : .35f, height = capsule != null ? capsule.height : 1.9f;
            var agent = player != null ? player.Agent : null;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas,
            };
            var others = new List<Vector3>();
            foreach (var npc in housemates ?? Enumerable.Empty<HouseNpc>())
                if (npc != null && npc.transform != body && npc.gameObject.activeInHierarchy) others.Add(npc.transform.position);
            if (player != null) others.Add(player.transform.position);
            var origin = marker.transform.position;
            for (int step = 1; step < 33; step++)
            {
                float angle = step * 137.5f * Mathf.Deg2Rad;
                float reach = .8f + (step % 8) * .4f;
                var wanted = origin + new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
                if (!rooms.TrySampleFloor(wanted, radius, filter, .25f, out var feet, out var name) || name != room) continue;
                if (!rooms.HasCapsuleClearance(feet, radius, height, body)) continue;
                if (StormSpotInFurniture(feet, radius, height, body)) continue;
                bool crowded = false;
                foreach (var other in others)
                {
                    var gap = other - feet; gap.y = 0f;
                    if (gap.sqrMagnitude < StormClearOfOthers * StormClearOfOthers) { crowded = true; break; }
                }
                if (crowded) continue;
                spot = feet;
                return true;
            }
            return false;
        }
    }
}
