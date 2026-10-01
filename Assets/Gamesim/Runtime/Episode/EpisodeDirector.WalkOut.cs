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
    /// <para><b>The staged exit</b> (PACK8-PASS-PLAN C2, MOCKUP-PASS-PLAN M19). After a staged
    /// eviction the walk out is the goodbye's second half, and the camera never follows the body
    /// across the house: the stage's goodbye, then the house watching them go - three seconds of
    /// the wide with every head on them, two on the survivor - at a slow walk with the head down,
    /// then a dip to the door shot as they come within six metres of the door. A warm or dealt
    /// goodbye stops at the door and turns back for the evicted take; then the door opens with no
    /// flare, they go through, it closes behind them, and the shot holds on the shut door before
    /// the body is switched off behind it, under a dip that hands the camera back. The chrome is
    /// aside the whole way. A press goes straight to the shut door; nothing the commit decided
    /// changes.</para>
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

        /// <summary>The staged walk's pace while it is in frame, metres a second: there is no sad walk in the library, so a slow one with the head down.</summary>
        public const float StagedWalkSpeed = 1.5f;

        /// <summary>How long the shot holds on the door shut behind them before their body goes.</summary>
        public const float DoorHoldSeconds = 1.2f;

        /// <summary>How long a warm or dealt goodbye stops at the door, turned back to the house: the opening's own pose length.</summary>
        public const float LastLookSeconds = 1.6f;

        /// <summary>How long the house watches them go before the cut to the survivor.</summary>
        private const float WatchSeconds = 3f;

        /// <summary>How near the door they come before the dip to the door shot, and how long a walk may take before it cuts there regardless.</summary>
        private const float DoorCutReach = 6f, DoorCutAfter = 20f;

        /// <summary>How far ahead of the body, on the floor, the walker's eyes go.</summary>
        private const float LookAheadMetres = 1.5f;

        /// <summary>A move this short reads as a cut, as the stage's own are.</summary>
        private const float ExitCutSeconds = 0.01f;

        private string walkingOutId;
        private int walkOutLeg;
        private float walkOutUntil, walkOutPressGuard;
        private OpeningDoorSet walkOutDoor;

        /// <summary>Whether the walk out under way is a staged eviction's: watched, slow, through a door that shuts behind it.</summary>
        private bool walkOutStaged;
        private StagedLeg stagedLeg;
        private float stagedWalkFrom, stagedLookUntil, stagedShutUntil;
        private bool stagedBrisk, stagedDoorCut;
        /// <summary>How the evicted leave things with the player, read once as the walk begins.</summary>
        private GoodbyeKind walkOutTone;
        /// <summary>The point on the floor ahead of the walker that their eyes are kept on: a scene transform of its own.</summary>
        private Transform walkOutLookMark;

        /// <summary>Where a staged walk out is.</summary>
        private enum StagedLeg { Binding, Walking, LastLook, Opening, Through, Closing, Holding }

        /// <summary>The opening's door shot, which the staged walk out cuts to as well: the doorway from the yard, square on.</summary>
        private static HouseCameraRig.Shot YardDoorShot => new HouseCameraRig.Shot
        {
            Focus = new Vector3(-2.0f, 1.85f, 13.8f), Distance = 4.1f, Pitch = 0f, Yaw = 270f,
            FieldOfView = 40f, Seconds = 0.6f, DepthOfFieldWeight = 0.3f,
        };

        /// <summary>The reference build's push-in as the door opens: a metre closer in half a second.</summary>
        private static HouseCameraRig.Shot YardPushInShot => new HouseCameraRig.Shot
        {
            Focus = new Vector3(-2.0f, 1.85f, 13.8f), Distance = 3.1f, Pitch = 0f, Yaw = 270f,
            FieldOfView = 40f, Seconds = 0.5f, DepthOfFieldWeight = 0.3f,
        };

        /// <summary>For tests: walk the evicted out even in a batch run, where it is otherwise skipped as the opening's stage is.</summary>
        public bool WalkOutsInBatchRuns { get; set; }

        /// <summary>Who is walking out, or null.</summary>
        public string WalkingOutId => walkingOutId;

        /// <summary>Whether the walk out is at the door or past it: the door is open for them.</summary>
        public bool WalkOutAtTheDoor => walkingOutId != null && (walkOutStaged ? stagedLeg >= StagedLeg.Opening : walkOutLeg >= 2);

        /// <summary>Whether the walk out under way is a staged eviction's. A read for tests.</summary>
        public bool WalkOutIsStaged => walkingOutId != null && walkOutStaged;

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
            // Under a staged eviction it is the goodbye's second half: the stage keeps the house in
            // its seats to watch, and the camera, until the door is shut behind them.
            walkOutStaged = IsCeremonyStaged && CeremonyStageKind == CeremonySting.EvictionKind;
            stagedLeg = StagedLeg.Binding;
            stagedBrisk = stagedDoorCut = false;
            walkOutTone = GoodbyeTone(projected, id);
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
            if (Time.unscaledTime >= walkOutPressGuard && CeremonyTakeover.SkipPressed()) { SkipWalkOut(); return; }
            if (walkOutStaged) { TickStagedWalkOut(); return; }
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

        private bool TryWalkOutTo(Vector3 mark, float speed = 0f)
        {
            if (player == null || player.Agent == null) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };
            if (!NavMesh.SamplePosition(mark, out var hit, 0.75f, filter)) return false;
            return npcMeetings.RouteDeparture(hit.position, out _, speed);
        }

        // ---------------------------------------------------------------- the staged exit

        /// <summary>Where a staged exit's walk stops for the last look: the inside mark, where the opening's guests posed.</summary>
        private Vector3 ExitLastLook => WalkOutInside;

        /// <summary>Where a staged exit's walk goes through the door to: the deck behind the facade.</summary>
        private Vector3 ExitThrough => WalkOutDeck;

        /// <summary>
        /// The line the walker's root passes behind the door before it closes: the vestibule's
        /// light plane, which hides whatever is past it from the yard.
        /// </summary>
        private float ExitCloseLineX => OpeningDoorSet.VestibuleFarX;

        /// <summary>
        /// Each frame of a staged walk out: watched by the house in its seats, cut to the door as
        /// they come near it, the last look, the door opened without its flare, walked through and
        /// shut, and the shot held on the shut door before the body goes behind it.
        /// </summary>
        private void TickStagedWalkOut()
        {
            float now = Time.unscaledTime;
            var body = BodyFor(walkingOutId);
            switch (stagedLeg)
            {
                case StagedLeg.Binding:
                    // Waiting for the body to take its navigation back - or for the house to have let
                    // them go because it could not.
                    if (npcMeetings.CandidateDropped(walkingOutId)) { FinishWalkOut(); return; }
                    if (!npcMeetings.CanWalk(walkingOutId)) return;
                    if (body == null || !npcMeetings.BeginDeparture(walkingOutId, out _) || !TryWalkOutTo(ExitLastLook, StagedWalkSpeed))
                    { FinishWalkOut(); return; }
                    stagedLeg = StagedLeg.Walking;
                    stagedWalkFrom = now;
                    if (ceremonyStage != null) ceremonyStage.WatchTheWalk(body, null, WatchSeconds);
                    return;
                case StagedLeg.Walking:
                    LookAhead(body);
                    // Slow while the house watches them in frame, the house's own pace once they are
                    // out of it, and slow again from the cut to the door.
                    if (!stagedDoorCut && !stagedBrisk && now >= stagedWalkFrom + WatchSeconds)
                    {
                        npcMeetings.SetDepartureSpeed(0f);
                        stagedBrisk = true;
                    }
                    if (!stagedDoorCut && body != null
                        && (FloorDistance(body.position, ExitLastLook) <= DoorCutReach || now >= stagedWalkFrom + DoorCutAfter)) CutToTheDoor();
                    if (!npcMeetings.DepartureArrived()) return;
                    if (!stagedDoorCut) CutToTheDoor();
                    TakeTheLastLook(body);
                    stagedLeg = StagedLeg.LastLook;
                    return;
                case StagedLeg.LastLook:
                    if (now < stagedLookUntil) return;
                    OpenTheExitDoor(body);
                    stagedLeg = StagedLeg.Opening;
                    return;
                case StagedLeg.Opening:
                    if (walkOutDoor != null && walkOutDoor.Openness < WalkOutDoorClear) return;
                    if (!TryWalkOutTo(ExitThrough, StagedWalkSpeed)) { FinishWalkOut(); return; }
                    stagedLeg = StagedLeg.Through;
                    return;
                case StagedLeg.Through:
                    LookAhead(body);
                    // The door closes once they are past the line that hides them - or wherever they
                    // stopped, if the route ends short of it.
                    if (body != null && body.position.x > ExitCloseLineX && !npcMeetings.DepartureArrived()) return;
                    if (walkOutDoor != null) walkOutDoor.Close(reducedMotion);
                    stagedLeg = StagedLeg.Closing;
                    return;
                case StagedLeg.Closing:
                    if (walkOutDoor != null && walkOutDoor.Openness > 0.01f) return;
                    stagedShutUntil = now + DoorHoldSeconds;
                    stagedLeg = StagedLeg.Holding;
                    return;
                default:
                    // The shot holds on the shut door; then the body goes behind it, under the dip.
                    if (now >= stagedShutUntil) FinishWalkOut();
                    return;
            }
        }

        /// <summary>
        /// The dip to the door: black for a blink, the door built closed in it, so it is never seen
        /// standing in the yard mid-walk, the stage's watch stopped, the camera on the door shot,
        /// and the walk slow again for the frame.
        /// </summary>
        private void CutToTheDoor()
        {
            stagedDoorCut = true;
            BeginTravelDip();
            if (walkOutDoor == null) walkOutDoor = OpeningDoorSet.Build(gameObject.scene);
            if (ceremonyStage != null) ceremonyStage.StopTheWatch();
            if (cameraRig != null)
            {
                var door = YardDoorShot;
                door.Seconds = ExitCutSeconds;
                cameraRig.MoveTo(door);
            }
            npcMeetings.SetDepartureSpeed(StagedWalkSpeed);
        }

        /// <summary>
        /// At the door, by how they leave things with the player (CEREMONY-CUTSCENES-PLAN §7.6, 5A):
        /// a warm or dealt goodbye stops and turns back - to the lens, where the player's own eye is
        /// - for the evicted take; a cold or an unmoved one walks straight on.
        /// </summary>
        private void TakeTheLastLook(Transform body)
        {
            stagedLookUntil = Time.unscaledTime;
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual == null) return;
            visual.LookAt(null, 0f);
            if (walkOutTone != GoodbyeKind.Dealt && walkOutTone != GoodbyeKind.Warm) return;
            if (cameraRig != null) visual.SetFacing(cameraRig.Yaw + 180f);
            visual.React(CharacterPresentation.Reaction.Evicted);
            stagedLookUntil = Time.unscaledTime + LastLookSeconds;
        }

        /// <summary>The door opens for them - the swing, and the light beyond at its settled spill with no flare - and the camera pushes in.</summary>
        private void OpenTheExitDoor(Transform body)
        {
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual != null) visual.SetFacing(float.NaN);
            if (walkOutDoor == null) walkOutDoor = OpeningDoorSet.Build(gameObject.scene);
            walkOutDoor.Open(reducedMotion, false);
            if (cameraRig != null) cameraRig.MoveTo(YardPushInShot);
        }

        /// <summary>
        /// The walker's head down, on a point kept on the floor ahead of them: a slow walk with the
        /// head down reads as heavy. A scene transform of its own: nothing may be added to the
        /// body's root, which its motion owner refuses to share.
        /// </summary>
        private void LookAhead(Transform body)
        {
            if (body == null) return;
            var visual = body.GetComponent<CharacterPresentation>();
            if (visual == null) return;
            if (walkOutLookMark == null) walkOutLookMark = new GameObject("Walk out look mark").transform;
            var ahead = body.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;
            walkOutLookMark.position = body.position + ahead.normalized * LookAheadMetres;
            visual.LookAtPoint(walkOutLookMark, 0.5f);
        }

        /// <summary>
        /// The walk out over: the house lets them go, the door comes down, and the body goes with
        /// them. A staged exit does it behind a dip - the door is already shut on them, and the
        /// camera goes back to the viewer under the black - unless <paramref name="immediate"/>:
        /// a competition taking the yard, or the stage ended under it, has the frame now.
        /// </summary>
        private void FinishWalkOut(bool immediate = false)
        {
            if (walkingOutId == null) return;
            string leaving = walkingOutId;
            bool staged = walkOutStaged;
            if (staged)
            {
                var body = BodyFor(leaving);
                var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
                if (visual != null) { visual.SetFacing(float.NaN); visual.LookAt(null, 0f); }
            }
            walkingOutId = null;
            walkOutStaged = false;
            if (npcMeetings != null) { npcMeetings.EndDeparture(); npcMeetings.DepartureCandidate = null; }
            if (staged && !immediate) BeginTravelDip();
            StrikeWalkOutDoor();
            if (departingId == leaving) departingId = null;
            if (IsReady) Project();
            if (staged)
            {
                if (cameraRig != null) cameraRig.ReleaseShot(ExitCutSeconds);
                if (ceremonyStage != null) ceremonyStage.HandBackCamera();
            }
            else if (cameraRig != null && player != null) cameraRig.FocusSubject(player.transform, false);
        }

        /// <summary>
        /// Ends a walk out now, as a press does. Public for the tests and the season walks. An
        /// unstaged one lets them go at once, as it always did. A staged exit - its goodbye or its
        /// walk - goes straight to the door shut behind them: the leaves closed, the door shot,
        /// and the body switched off under the dip. One press is one step, and nothing the commit
        /// decided changes.
        /// </summary>
        public void SkipWalkOut()
        {
            if (walkingOutId != null)
            {
                if (!walkOutStaged) { FinishWalkOut(); return; }
                if (walkOutDoor != null)
                {
                    walkOutDoor.Close(true);
                    if (cameraRig != null)
                    {
                        var shut = YardDoorShot;
                        shut.Seconds = ExitCutSeconds;
                        cameraRig.MoveTo(shut);
                    }
                }
                FinishWalkOut();
                return;
            }
            if (IsCeremonyStaged && ceremonyStage.Step == CeremonyStageStep.Goodbye) SkipTheGoodbye();
        }

        /// <summary>
        /// A press on the goodbye: as a press on the walk, straight to the door shut behind them.
        /// They never walk; their body goes under the dip, the camera with it, and the house gets
        /// up as it would after the walk.
        /// </summary>
        private void SkipTheGoodbye()
        {
            string leaving = departingId;
            if (leaving == null) return;
            if (walkOutDoor != null) walkOutDoor.Close(true);
            BeginTravelDip();
            var body = BodyFor(leaving);
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual != null) { visual.SetFacing(float.NaN); visual.LookAt(null, 0f); }
            if (cameraRig != null) cameraRig.ReleaseShot(ExitCutSeconds);
            if (ceremonyStage != null) ceremonyStage.HandBackCamera();
            if (npcMeetings != null && npcMeetings.DepartureCandidate == leaving) npcMeetings.DepartureCandidate = null;
            departingId = null;
            StrikeWalkOutDoor();
            if (ceremonyStage != null) ceremonyStage.EndTheGoodbye();
            if (IsReady) Project();
        }

        /// <summary>How far apart two points stand on the floor, heights aside.</summary>
        private static float FloorDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>The door and the walker's look mark come down: the walk out is over, or never started.</summary>
        private void StrikeWalkOutDoor()
        {
            if (walkOutDoor != null) { Destroy(walkOutDoor.gameObject); walkOutDoor = null; }
            if (walkOutLookMark != null) { Destroy(walkOutLookMark.gameObject); walkOutLookMark = null; }
        }

        /// <summary>A staged eviction's end strikes whatever its exit put up for a walk that is not under way.</summary>
        private void StrikeWalkOutDoorIfNobodyWalks()
        {
            if (walkingOutId == null) StrikeWalkOutDoor();
        }

        /// <summary>Forgets a walk out and the jury's places when the season is replaced under them.</summary>
        private void ResetWalkOut()
        {
            if (npcMeetings != null && walkingOutId != null) { npcMeetings.EndDeparture(); npcMeetings.DepartureCandidate = null; }
            walkingOutId = null;
            walkOutStaged = false;
            StrikeWalkOutDoor();
            juryBench.Clear();
            juryBenchSeason = null;
        }

        /// <summary>
        /// How the evicted leave things with the player, as far as the player knows (MOCKUP-PASS-PLAN
        /// decision 6A): a deal between them, the player's own vote or nomination against them, the
        /// player's vote to keep them, or none of these. Never their hidden feeling toward the
        /// player, which the line used to give away as they walked out to the jury.
        /// </summary>
        public enum GoodbyeKind { Neutral, Warm, Cold, Dealt }

        /// <summary>
        /// The goodbye's tone, from what the player knows: a deal between them that still binds;
        /// otherwise cold for the player's ballot against them, or for a player Head of Household
        /// with them on the block; warm for the player's ballot for the other nominee; neutral
        /// otherwise. Pure: it reads the committed state and draws nothing.
        /// </summary>
        public static GoodbyeKind GoodbyeTone(EpisodeState state, string id)
        {
            if (state == null || id == null || state.Find(id) == null) return GoodbyeKind.Neutral;
            string you = state.playerId;
            bool dealt = state.deals != null && state.deals.Any(d => DealStatus.Binds(d.status) && d.status != DealStatus.Proposed
                && ((d.proposerId == id && d.recipientId == you) || (d.proposerId == you && d.recipientId == id)));
            if (dealt) return GoodbyeKind.Dealt;
            var ballot = state.votes != null ? state.votes.FirstOrDefault(vote => vote.voterId == you) : null;
            bool nominated = state.hohId == you && state.nominees != null && state.nominees.Contains(id);
            if (nominated || (ballot != null && ballot.targetId == id)) return GoodbyeKind.Cold;
            if (ballot != null && !string.IsNullOrEmpty(ballot.targetId)) return GoodbyeKind.Warm;
            return GoodbyeKind.Neutral;
        }

        /// <summary>
        /// What the evicted does at the door: the reference's goodbye lines, by how they leave things
        /// with the player as far as the player knows (<see cref="GoodbyeTone"/>), and where they are
        /// going - the jury house for a juror, and nowhere named for somebody out before the jury.
        /// </summary>
        public static string GoodbyeLine(EpisodeState state, string id)
        {
            var who = state?.Find(id);
            if (who == null) return string.Empty;
            string name = who.name.Split(' ')[0];
            string opener;
            switch (GoodbyeTone(state, id))
            {
                case GoodbyeKind.Dealt: opener = name + " pauses at the door and turns to you…"; break;
                case GoodbyeKind.Warm: opener = name + " gives you one last look before walking out the door."; break;
                case GoodbyeKind.Cold: opener = name + " glares at you from the doorway."; break;
                default: opener = name + " walks to the door without looking back."; break;
            }
            return who.status == ContestantStatus.Jury ? opener + " They'll be waiting in the jury house." : opener;
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
                sampled => rooms.HasCapsuleClearance(sampled, 0.35f, 1.9f, player.transform) && !InFurniture(sampled));
            for (int i = 0; i < jurors.Count && i < spots.Count; i++) juryBench[jurors[i]] = spots[i];
        }

        private static readonly Collider[] furnitureHits = new Collider[4];

        /// <summary>
        /// Whether a body standing here would stand in a couch or a chair. The clearance query looks
        /// through the furniture layer on purpose - it is the camera's too - so a juror's place asks
        /// the layer itself, as the start-spot audit does.
        /// </summary>
        private bool InFurniture(Vector3 feet)
        {
            Physics.SyncTransforms();
            return gameObject.scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * (1.9f - 0.35f), 0.35f,
                furnitureHits, 1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore) > 0;
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
