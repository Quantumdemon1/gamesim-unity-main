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
    /// The evicted houseguest's walk out, and the jury in the living room on finale night.
    ///
    /// <para><b>The walk out.</b> Once the cards narrating an eviction are done, the evicted walks
    /// out through the opening's front door: across the yard to the door, which opens for them,
    /// through it, and off the deck behind the facade, with a goodbye line on the strip - the
    /// reference's own, from a goodbye it wrote and never shows (<c>PostEvictionGoodbyeDialog.tsx</c>,
    /// lost behind an event type it spells differently from its writer). They used to vanish the
    /// frame the last card ended. The house borrows them for the walk the way the opening borrows
    /// its cast; anything that goes wrong - no route, the house busy, a walk that runs long - and
    /// they go as they used to. A press skips it, and a competition started meanwhile ends it: the
    /// arena takes the yard they cross. Reduced motion never plays it, and a batch run does not
    /// unless asked, as the opening's stage is not. The final eviction has none: it opens finale
    /// night, and the new juror joins the jury in the living room.</para>
    ///
    /// <para><b>The jury.</b> From jury questioning to the end of the season, the jurors are back in
    /// the living room, standing along it, facing the middle: the jury the finalists face and the
    /// winner is read out to. Placed while they have no navigation - the house moves only the people
    /// it routes, and a juror is not one of them - and never on furniture or on each other.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        // The opening stage's marks, walked the other way: inside the yard, and the deck behind the facade.
        private static readonly Vector3 WalkOutInside = new Vector3(-1.6f, 0f, 14.1f);
        private static readonly Vector3 WalkOutDeck = new Vector3(-5.3f, 0f, 13.8f);

        /// <summary>The most a walk out may take, in real seconds, before the house lets them go.</summary>
        public const float WalkOutSeconds = 40f;

        /// <summary>How far the door must have swung before they walk through it: the opening's own threshold.</summary>
        private const float WalkOutDoorClear = 0.35f;

        private string walkingOutId;
        private int walkOutLeg;
        private float walkOutUntil, walkOutPressGuard;
        private OpeningDoorSet walkOutDoor;

        /// <summary>For tests: walk the evicted out even in a batch run, where it is otherwise skipped as the opening's stage is.</summary>
        public bool WalkOutsInBatchRuns { get; set; }

        /// <summary>Who is walking out, or null.</summary>
        public string WalkingOutId => walkingOutId;

        /// <summary>Whether the walk out is at the door or past it: the door is open for them.</summary>
        public bool WalkOutAtTheDoor => walkingOutId != null && walkOutLeg >= 2;

        /// <summary>
        /// Starts the walk out for the houseguest the cards were about, when this house can play it.
        /// Returns false when it cannot - reduced motion, a batch run not asking, the house busy - and
        /// the caller lets them go as it always has.
        /// </summary>
        private bool TryBeginWalkOut(string id)
        {
            if (walkingOutId != null || reducedMotion || (Application.isBatchMode && !WalkOutsInBatchRuns)) return false;
            if (npcMeetings == null || npcWorldFailed || npcDiagnosticsSuspended || competitionArenaStaging) return false;
            if (openingStage != null && openingStage.Active) return false;
            // The final Head of Household's choice opens finale night: the new juror joins the jury in
            // the living room rather than walking out of the door to come straight back in.
            if (projected == null || FinaleNight(projected.phase)) return false;
            if (housemates == null || !housemates.Any(npc => npc != null && npc.Id == id && npc.gameObject.activeInHierarchy)) return false;
            walkingOutId = id;
            walkOutLeg = 0;
            walkOutUntil = Time.unscaledTime + WalkOutSeconds;
            // A press already in flight - the one that closed the last card - does not skip the walk.
            walkOutPressGuard = Time.unscaledTime + 0.35f;
            // Back into the house's world for the walk: it moves only the people it routes. If their
            // body cannot take its navigation back, the house lets them go instead of stopping.
            npcMeetings.DepartureCandidate = id;
            ReconcileNpcSocialWorld();
            return true;
        }

        /// <summary>Each frame: the walk out kept moving, leg by leg, and let go when it is done, skipped or stuck.</summary>
        private void TickWalkOut()
        {
            if (walkingOutId == null) return;
            if (Time.unscaledTime > walkOutUntil || npcMeetings == null || npcWorldFailed) { FinishWalkOut(); return; }
            if (Time.unscaledTime >= walkOutPressGuard && CeremonyTakeover.SkipPressed()) { FinishWalkOut(); return; }
            switch (walkOutLeg)
            {
                case 0:
                    // Waiting for the body to take its navigation back - or for the house to have let
                    // them go because it could not.
                    if (npcMeetings.CandidateDropped(walkingOutId)) { FinishWalkOut(); return; }
                    if (!npcMeetings.CanWalk(walkingOutId)) return;
                    if (!npcMeetings.BeginDeparture(walkingOutId, out _) || !TryWalkOutTo(WalkOutInside)) { FinishWalkOut(); return; }
                    walkOutDoor = OpeningDoorSet.Build(gameObject.scene);
                    var body = BodyFor(walkingOutId);
                    if (body != null && cameraRig != null) cameraRig.FocusSubject(body, false);
                    if (sting != null && projected != null) sting.Play(CeremonySting.WalkOutKind, GoodbyeLine(projected, walkingOutId), reducedMotion);
                    walkOutLeg = 1;
                    return;
                case 1:
                    if (!npcMeetings.DepartureArrived()) return;
                    if (walkOutDoor != null) walkOutDoor.Open(reducedMotion);
                    walkOutLeg = 2;
                    return;
                case 2:
                    if (walkOutDoor != null && walkOutDoor.Openness < WalkOutDoorClear) return;
                    if (!TryWalkOutTo(WalkOutDeck)) { FinishWalkOut(); return; }
                    walkOutLeg = 3;
                    return;
                default:
                    if (npcMeetings.DepartureArrived()) FinishWalkOut();
                    return;
            }
        }

        private bool TryWalkOutTo(Vector3 mark)
        {
            if (player == null || player.Agent == null) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };
            if (!NavMesh.SamplePosition(mark, out var hit, 0.75f, filter)) return false;
            return npcMeetings.RouteDeparture(hit.position, out _);
        }

        /// <summary>The walk out over: the house lets them go, the door comes down, and the body goes with them.</summary>
        private void FinishWalkOut()
        {
            if (walkingOutId == null) return;
            string leaving = walkingOutId;
            walkingOutId = null;
            if (npcMeetings != null) { npcMeetings.EndDeparture(); npcMeetings.DepartureCandidate = null; }
            if (walkOutDoor != null) { Destroy(walkOutDoor.gameObject); walkOutDoor = null; }
            if (departingId == leaving) departingId = null;
            if (IsReady) Project();
            if (cameraRig != null && player != null) cameraRig.FocusSubject(player.transform, false);
        }

        /// <summary>Ends a walk out now, as a press does. Public for the tests and the season walks.</summary>
        public void SkipWalkOut() => FinishWalkOut();

        /// <summary>Forgets a walk out and the jury's places when the season is replaced under them.</summary>
        private void ResetWalkOut()
        {
            if (npcMeetings != null && walkingOutId != null) { npcMeetings.EndDeparture(); npcMeetings.DepartureCandidate = null; }
            walkingOutId = null;
            if (walkOutDoor != null) { Destroy(walkOutDoor.gameObject); walkOutDoor = null; }
            juryBench.Clear();
            juryBenchSeason = null;
        }

        /// <summary>
        /// What the evicted does at the door: the reference's goodbye lines, by how they leave things
        /// with the player - a deal between them, warmth, or a grudge - and where they are going.
        /// </summary>
        public static string GoodbyeLine(EpisodeState state, string id)
        {
            var who = state?.Find(id);
            if (who == null) return string.Empty;
            string name = who.name.Split(' ')[0];
            double warmth = state.Score(id, state.playerId);
            bool dealt = state.deals.Any(d => DealStatus.Binds(d.status) && d.status != DealStatus.Proposed
                && ((d.proposerId == id && d.recipientId == state.playerId) || (d.proposerId == state.playerId && d.recipientId == id)));
            string opener = dealt ? name + " pauses at the door and turns to you…"
                : warmth >= 20 ? name + " gives you one last look before walking out the door."
                : warmth <= -20 ? name + " glares at you from the doorway."
                : name + " walks to the door without looking back.";
            return opener + " They'll be waiting in the jury house.";
        }

        // ---------------------------------------------------------------- the jury on finale night

        /// <summary>Where each juror stands on finale night, chosen once for the finale and kept.</summary>
        private readonly Dictionary<string, Vector3> juryBench = new Dictionary<string, Vector3>();
        private string juryBenchSeason;

        /// <summary>Whether the season is on its finale night: from jury questioning to the end.</summary>
        public static bool FinaleNight(EpisodePhase phase) =>
            phase == EpisodePhase.JuryQuestioning || phase == EpisodePhase.FinalSpeeches
            || phase == EpisodePhase.Jury || phase == EpisodePhase.Finished;

        /// <summary>Whether a juror is in the living room tonight: finale night, and a place was found for them.</summary>
        private bool OnJuryBench(EpisodeState state, ContestantState model) =>
            state != null && FinaleNight(state.phase) && model.status == ContestantStatus.Jury && juryBench.ContainsKey(model.id);

        /// <summary>
        /// Chooses the jurors' places for the finale: the living room's floor around its middle, a
        /// metre apart, clear of furniture and of everyone - the finalists and the player included -
        /// nearest the middle first. A juror with no place is not shown, rather than shown somewhere
        /// wrong.
        /// </summary>
        private void BenchTheJury(EpisodeState state)
        {
            if (state == null || !FinaleNight(state.phase))
            {
                if (juryBench.Count > 0) juryBench.Clear();
                juryBenchSeason = null;
                return;
            }
            if (juryBenchSeason == state.sessionId && juryBench.Count > 0) return;
            juryBench.Clear();
            juryBenchSeason = state.sessionId;
            var jurors = state.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Jury).Select(c => c.id).ToList();
            if (jurors.Count == 0 || player == null || player.Agent == null) return;
            var marker = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .FirstOrDefault(m => m.RoomName == "Living");
            if (marker == null || !HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out _)) return;
            var filter = new NavMeshQueryFilter { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };
            var occupied = housemates.Where(n => n != null && n.gameObject.activeInHierarchy).Select(n => n.transform.position).ToList();
            occupied.Add(player.transform.position);
            // Clear of the episode station too: the player walks there for every decision tonight.
            occupied.Add(StationPosition);
            var spots = JuryPlaces(marker.transform.position, jurors.Count, occupied,
                (Vector3 wanted, out Vector3 sampled, out string room) => rooms.TrySampleFloor(wanted, 0.35f, filter, 0.25f, out sampled, out room),
                sampled => rooms.HasCapsuleClearance(sampled, 0.35f, 1.9f, player.transform));
            for (int i = 0; i < jurors.Count && i < spots.Count; i++) juryBench[jurors[i]] = spots[i];
        }

        /// <summary>Samples the floor under a wanted place: where a body would stand, and in which room.</summary>
        public delegate bool FloorSampler(Vector3 wanted, out Vector3 sampled, out string room);

        /// <summary>
        /// The jury's places: rings around the living room's middle, sixteen to a ring, nearest the
        /// middle first. Each is on the living room's own floor, clear, and at least a metre from
        /// everyone already standing there or placed before it. A room that cannot hold the whole
        /// jury gives the places it has.
        /// </summary>
        public static List<Vector3> JuryPlaces(Vector3 centre, int count, IEnumerable<Vector3> occupied,
            FloorSampler sample, System.Func<Vector3, bool> clear)
        {
            var taken = new List<Vector3>(occupied);
            var spots = new List<Vector3>();
            for (float ring = 1.6f; ring <= 5.2f && spots.Count < count; ring += 0.9f)
                for (int step = 0; step < 16 && spots.Count < count; step++)
                {
                    float angle = step * Mathf.PI * 2f / 16f;
                    var candidate = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                    if (!sample(candidate, out var sampled, out var room) || room != "Living") continue;
                    if (!clear(sampled)) continue;
                    var flat = new Vector3(sampled.x, 0f, sampled.z);
                    if (taken.Any(other => (new Vector3(other.x, 0f, other.z) - flat).magnitude < 1.0f)) continue;
                    taken.Add(sampled);
                    spots.Add(sampled);
                }
            return spots;
        }

        /// <summary>Stands each juror on their place, facing the middle of the room. Only ever while they have no navigation.</summary>
        private void StandTheJury(EpisodeState state)
        {
            if (housemates == null || juryBench.Count == 0) return;
            var marker = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .FirstOrDefault(m => m.RoomName == "Living");
            foreach (var npc in housemates)
            {
                if (npc == null || !juryBench.TryGetValue(npc.Id, out var place)) continue;
                // Somebody still walking out, or still in the room for the card about them, is not moved.
                if (npc.Id == departingId || npc.Id == walkingOutId) continue;
                var facing = marker != null ? marker.transform.position - place : Vector3.forward;
                facing.y = 0f;
                npc.transform.SetPositionAndRotation(place, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : npc.transform.rotation);
            }
            Physics.SyncTransforms();
        }
    }
}
