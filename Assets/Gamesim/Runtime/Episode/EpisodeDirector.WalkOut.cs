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

        /// <summary>How long, on the unscaled clock, a press after the walk begins is still the one that closed the last card.</summary>
        public const float WalkOutPressGuardSeconds = 0.35f;

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

        /// <summary>The frame a press skipped the walk out: the walk is over by the time Escape's chain runs in it.</summary>
        private int walkOutSkippedFrame = -1;

        /// <summary>
        /// Whether the walk out has this frame's Escape, Start or B (PLAN A, A2): it reads the
        /// Ceremony map's Skip as a card does, without being a card, so the press that skips it -
        /// or any press while it walks - stops there, rather than going on to open the pause menu
        /// over the goodbye (EpisodeDirector.Controls.cs).
        /// </summary>
        private bool WalkOutHasThePress => walkingOutId != null || walkOutSkippedFrame == Time.frameCount;

        /// <summary>Whether the walk out under way is a staged eviction's: watched, slow, through a door that shuts behind it.</summary>
        private bool walkOutStaged;
        private StagedLeg stagedLeg;
        private float stagedWalkFrom, stagedLookUntil, stagedShutUntil;
        private bool stagedBrisk, stagedDoorCut, stagedPushPending;
        /// <summary>How the evicted leave things with the player, read once as the walk begins.</summary>
        private GoodbyeKind walkOutTone;
        /// <summary>The point on the floor ahead of the walker that their eyes are kept on: a scene transform of its own.</summary>
        private Transform walkOutLookMark;
        /// <summary>Who the stage's goodbye last played the goodbye line for, until a walk out reads it.</summary>
        private string goodbyeSaidBy;
        /// <summary>Whether the staged walk under way had its line played by the goodbye before it.</summary>
        private bool walkOutLineSaid;

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

        // ---------------------------------------------------------------- the living room's door (M23)

        /// <summary>Which door a staged eviction's evicted leave by (MOCKUP-PASS-PLAN M23).</summary>
        public enum WalkOutDoor
        {
            /// <summary>The opening's set on the living room's west wall, at its south end, in view of the couches: the owner's decision 2A.</summary>
            Living,
            /// <summary>The opening's front door in the west yard: the fallback, and every unstaged walk out's.</summary>
            Yard,
        }

        /// <summary>
        /// The door staged exits ask for: the living room's (the owner's decision 2A). Its doorway
        /// was cleared of the prototype planter and the NavMesh rebaked on 2026-10-01
        /// (HouseLivingGallery.BuildFromCommandLine), and the probe
        /// (StagedExit_TheLivingRoomsDoorwayIsClearAndFlat) measured flat floor, clearance at every
        /// mark and nothing in the leaves' swing. It still falls back to the yard's whenever its
        /// probe refuses at the goodbye, and unstaged walk outs always use the yard's.
        /// </summary>
        public const WalkOutDoor DefaultWalkOutDoor = WalkOutDoor.Living;

        /// <summary>The door staged exits ask for: <see cref="DefaultWalkOutDoor"/>, or what a test sets.</summary>
        public WalkOutDoor WalkOutThrough { get; set; } = DefaultWalkOutDoor;

        /// <summary>The door the exit under way uses: the living room's only when it was asked for and passed its probe at the goodbye.</summary>
        private WalkOutDoor walkOutDoorUsed = WalkOutDoor.Yard;

        /// <summary>The door the exit under way uses. A read for tests.</summary>
        public WalkOutDoor WalkOutDoorInUse => walkOutDoorUsed;

        // The living room's exit marks: the last look 1.4 m short of the doorway, south of the
        // left leaf's swing, and the vestibule's floor behind the leaves.
        private static readonly Vector3 LivingLastLook = new Vector3(-11.2f, 0f, -9.0f);
        private static readonly Vector3 LivingVestibule = new Vector3(-13.15f, 0f, -8.5f);

        /// <summary>A body's capsule, as the house's bodies are built: the probe's and the close line's measure.</summary>
        private const float BodyRadius = 0.35f, BodyHeight = 1.9f;

        /// <summary>How far off the living room's flat floor a mark may stand: the bake once raised the doorway onto the planter's top.</summary>
        private const float LivingFloorTolerance = 0.06f;

        /// <summary>
        /// The line the walker's root passes before the living room's door closes: a body's radius
        /// behind the closed leaves. Its light plane is the room's wall, which nobody passes.
        /// </summary>
        private static float LivingCloseLineX => DoorLayout.Living.FacadeFrontX - 0.08f - BodyRadius;

        /// <summary>The exit wide: over the seated heads, looking west down the room with the door at its centre. To be set by capture.</summary>
        private static HouseCameraRig.Shot LivingExitWide => new HouseCameraRig.Shot
        {
            Focus = new Vector3(-11.8f, 1.2f, -8.3f), Distance = 6.5f, Pitch = 12f, Yaw = 250f,
            FieldOfView = 40f, Seconds = ExitCutSeconds, DepthOfFieldWeight = 0.3f,
        };

        /// <summary>The living room's door shot, as the yard's is: the doorway square on. To be set by capture.</summary>
        private static HouseCameraRig.Shot LivingDoorShot => new HouseCameraRig.Shot
        {
            Focus = new Vector3(-12.2f, 1.5f, -8.5f), Distance = 3.6f, Pitch = 4f, Yaw = 262f,
            FieldOfView = 40f, Seconds = ExitCutSeconds, DepthOfFieldWeight = 0.3f,
        };

        /// <summary>Its push-in as the door opens: a metre closer over a second.</summary>
        private static HouseCameraRig.Shot LivingPushInShot => new HouseCameraRig.Shot
        {
            Focus = new Vector3(-12.2f, 1.5f, -8.5f), Distance = 2.6f, Pitch = 4f, Yaw = 262f,
            FieldOfView = 40f, Seconds = 1f, DepthOfFieldWeight = 0.3f,
        };

        /// <summary>The scene's set dressing, under which a prop standing in the living room's doorway is hidden while its door stands (HouseSetPieces' root).</summary>
        private const string SetPiecesRoot = "Set Pieces";

        /// <summary>The props the living room's door hid while it stood, to be shown again when it is struck.</summary>
        private readonly List<Renderer> walkOutHidden = new List<Renderer>();

        /// <summary>The props the living room's door has hidden while it stands. A read for tests.</summary>
        public IReadOnlyList<Renderer> WalkOutDoorHides => walkOutHidden;

        /// <summary>For tests: walk the evicted out even in a batch run, where it is otherwise skipped as the opening's stage is.</summary>
        public bool WalkOutsInBatchRuns { get; set; }

        /// <summary>Who is walking out, or null.</summary>
        public string WalkingOutId => walkingOutId;

        /// <summary>Why the last walk out was refused or ended early; transient diagnostic only.</summary>
        public string LastWalkOutFailure { get; private set; }

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
            LastWalkOutFailure = null;
            if (walkingOutId != null) return RefuseWalkOut("Another houseguest is still walking out.");
            if (reducedMotion || (Application.isBatchMode && !WalkOutsInBatchRuns)) return RefuseWalkOut("Walk outs are disabled for this presentation.");
            if (npcMeetings == null || npcWorldFailed || npcDiagnosticsSuspended || competitionArenaStaging)
                return RefuseWalkOut("The house cannot route the departure: " + (npcWorldFailure ?? "world unavailable or held by diagnostics or competition."));
            if (openingStage != null && openingStage.Active) return RefuseWalkOut("The opening still holds the house.");
            // The final Head of Household's choice opens finale night: the new juror joins the jury in
            // the living room rather than walking out of the door to come straight back in.
            if (projected == null || FinaleNight(projected.phase)) return RefuseWalkOut("There is no weekly departure on finale night.");
            if (housemates == null || !housemates.Any(npc => npc != null && npc.Id == id && npc.gameObject.activeInHierarchy))
                return RefuseWalkOut("The departing houseguest has no active body.");
            walkingOutId = id;
            walkOutLeg = 0;
            walkOutUntil = Time.unscaledTime + WalkOutSeconds;
            // A press already in flight - the one that closed the last card - does not skip the walk.
            walkOutPressGuard = Time.unscaledTime + WalkOutPressGuardSeconds;
            // Under a staged eviction it is the goodbye's second half: the stage keeps the house in
            // its seats to watch, and the camera, until the door is shut behind them.
            walkOutStaged = IsCeremonyStaged && CeremonyStageKind == CeremonySting.EvictionKind;
            // The goodbye played their line. A staged walk with no goodbye before it - the house
            // let their body go at the card, and it came back for the walk - plays it as the walk
            // starts, as every walk out did before the goodbye took the line.
            walkOutLineSaid = walkOutStaged && goodbyeSaidBy == id;
            goodbyeSaidBy = null;
            stagedLeg = StagedLeg.Binding;
            stagedBrisk = stagedDoorCut = stagedPushPending = false;
            // The living room's door is the goodbye's to put up; without it standing, this walk
            // goes by the yard's, as every unstaged one does.
            if (!walkOutStaged || walkOutDoor == null) walkOutDoorUsed = WalkOutDoor.Yard;
            walkOutTone = GoodbyeTone(projected, id);
            // Back into the house's world for the walk: it moves only the people it routes. If their
            // body cannot take its navigation back, the house lets them go instead of stopping.
            npcMeetings.DepartureCandidate = id;
            ReconcileNpcSocialWorld();
            return true;
        }

        private bool RefuseWalkOut(string reason) { LastWalkOutFailure = reason; return false; }

        private void AbortWalkOut(string reason)
        {
            LastWalkOutFailure = reason;
            FinishWalkOut();
        }

        /// <summary>Each frame: the walk out kept moving, leg by leg, and let go when it is done, skipped or stuck.</summary>
        private void TickWalkOut()
        {
            if (walkingOutId == null) return;
            if (Time.unscaledTime > walkOutUntil || npcMeetings == null || npcWorldFailed)
            { AbortWalkOut(npcWorldFailure ?? "The walk out exceeded its budget or lost its coordinator."); return; }
            if (Time.unscaledTime >= walkOutPressGuard && CeremonyTakeover.SkipPressed())
            {
                walkOutSkippedFrame = Time.frameCount;
                SkipWalkOut();
                return;
            }
            if (walkOutStaged) { TickStagedWalkOut(); return; }
            switch (walkOutLeg)
            {
                case 0:
                    // Waiting for the body to take its navigation back - or for the house to have let
                    // them go because it could not.
                    if (npcMeetings.CandidateDropped(walkingOutId))
                    { AbortWalkOut(BodyFor(walkingOutId)?.GetComponent<HouseNpcMotion>()?.FailureReason ?? "The departing body could not bind."); return; }
                    if (!npcMeetings.CanWalk(walkingOutId)) return;
                    if (!npcMeetings.BeginDeparture(walkingOutId, out var departureReason)) { AbortWalkOut(departureReason); return; }
                    if (!TryWalkOutTo(WalkOutInside)) { FinishWalkOut(); return; }
                    walkOutDoor = OpeningDoorSet.Build(gameObject.scene);
                    var body = BodyFor(walkingOutId);
                    if (body != null && cameraRig != null) cameraRig.FocusSubject(body, false);
                    walkOutLeg = 1;
                    return;
                case 1:
                    if (!npcMeetings.DepartureArrived()) return;
                    if (walkOutDoor != null) walkOutDoor.Open(reducedMotion);
                    // The goodbye on the strip where the body is - at the door, as its words say -
                    // rather than as the walk across the yard begins (UI-UX-PASS-PLAN W0, sweep-show 10).
                    if (sting != null && projected != null)
                    {
                        sting.Play(CeremonySting.WalkOutKind, GoodbyeLine(projected, walkingOutId), reducedMotion);
                        walkOutLineSaid = true;
                    }
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
            if (player == null || player.Agent == null) return RefuseWalkOut("The departure has no navigation query configuration.");
            var filter = new NavMeshQueryFilter { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };
            if (!NavMesh.SamplePosition(mark, out var hit, 0.75f, filter)) return RefuseWalkOut("There is no navigation floor near " + mark.ToString("F2") + ".");
            if (npcMeetings.RouteDeparture(hit.position, out var reason, speed)) return true;
            return RefuseWalkOut(reason + " " + BodyFor(walkingOutId)?.GetComponent<HouseNpcMotion>()?.LastRouteFailure);
        }

        // ---------------------------------------------------------------- the staged exit

        /// <summary>Whether the exit under way goes by the living room's door.</summary>
        private bool ExitIsTheLivingRoom => walkOutDoorUsed == WalkOutDoor.Living;

        /// <summary>Where the exit's door stands.</summary>
        private DoorLayout ExitLayout => ExitIsTheLivingRoom ? DoorLayout.Living : DoorLayout.Yard;

        /// <summary>
        /// Where a staged exit's walk stops for the last look: the yard's inside mark, where the
        /// opening's guests posed, or 1.4 m short of the living room's doorway.
        /// </summary>
        private Vector3 ExitLastLook => ExitIsTheLivingRoom ? LivingLastLook : WalkOutInside;

        /// <summary>Where a staged exit's walk goes through the door to: the deck behind the facade, or the vestibule.</summary>
        private Vector3 ExitThrough => ExitIsTheLivingRoom ? LivingVestibule : WalkOutDeck;

        /// <summary>
        /// The line the walker's root passes behind the door before it closes: the yard
        /// vestibule's light plane, which hides whatever is past it, or a body's radius behind the
        /// living room's closed leaves.
        /// </summary>
        private float ExitCloseLineX => ExitIsTheLivingRoom ? LivingCloseLineX : OpeningDoorSet.VestibuleFarX;

        /// <summary>The exit's door shot, cut to as the leaves are shut on a press.</summary>
        private HouseCameraRig.Shot ExitDoorShot
        {
            get
            {
                var door = ExitIsTheLivingRoom ? LivingDoorShot : YardDoorShot;
                door.Seconds = ExitCutSeconds;
                return door;
            }
        }

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
                    if (npcMeetings.CandidateDropped(walkingOutId))
                    { AbortWalkOut(body?.GetComponent<HouseNpcMotion>()?.FailureReason ?? "The departing body could not bind."); return; }
                    if (!npcMeetings.CanWalk(walkingOutId)) return;
                    if (body == null) { AbortWalkOut("The staged departure lost its body."); return; }
                    if (!npcMeetings.BeginDeparture(walkingOutId, out var departureReason)) { AbortWalkOut(departureReason); return; }
                    if (!TryWalkOutTo(ExitLastLook, StagedWalkSpeed)) { FinishWalkOut(); return; }
                    stagedLeg = StagedLeg.Walking;
                    stagedWalkFrom = now;
                    // The yard's walk leaves the room, and the house watches it go; the living
                    // room's stays in it, and the exit wide holds the door in frame the whole way.
                    if (ceremonyStage != null) ceremonyStage.WatchTheWalk(body, ExitIsTheLivingRoom ? LivingExitWide : (HouseCameraRig.Shot?)null, WatchSeconds);
                    return;
                case StagedLeg.Walking:
                    LookAhead(body);
                    if (!ExitIsTheLivingRoom)
                    {
                        // Slow while the house watches them in frame, the house's own pace once
                        // they are out of it, and slow again from the cut to the door.
                        if (!stagedDoorCut && !stagedBrisk && now >= stagedWalkFrom + WatchSeconds)
                        {
                            npcMeetings.SetDepartureSpeed(0f);
                            stagedBrisk = true;
                        }
                        if (!stagedDoorCut && body != null
                            && (FloorDistance(body.position, ExitLastLook) <= DoorCutReach || now >= stagedWalkFrom + DoorCutAfter)) CutToTheDoor();
                    }
                    if (!npcMeetings.DepartureArrived()) return;
                    if (!stagedDoorCut && !ExitIsTheLivingRoom) CutToTheDoor();
                    TakeTheLastLook(body);
                    // A staged walk with no goodbye before it says its line here, at the door, where
                    // the body is; the goodbye's own line is said standing (UI-UX-PASS-PLAN W0).
                    if (!walkOutLineSaid && sting != null && projected != null)
                    {
                        sting.Play(CeremonySting.WalkOutKind, GoodbyeLine(projected, walkingOutId), reducedMotion);
                        walkOutLineSaid = true;
                    }
                    stagedLeg = StagedLeg.LastLook;
                    return;
                case StagedLeg.LastLook:
                    if (now < stagedLookUntil) return;
                    OpenTheExitDoor(body);
                    stagedLeg = StagedLeg.Opening;
                    return;
                case StagedLeg.Opening:
                    // The living room's push-in starts the frame after its cut, so the cut lands first.
                    if (stagedPushPending)
                    {
                        stagedPushPending = false;
                        if (cameraRig != null) cameraRig.MoveTo(LivingPushInShot);
                    }
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
            if (walkOutDoor == null) walkOutDoor = OpeningDoorSet.Build(gameObject.scene, ExitLayout);
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
        /// a dealt goodbye stops and turns back - to the lens, where the player's own eye is - for
        /// the evicted take; a cold or an unmoved one walks straight on.
        /// </summary>
        private void TakeTheLastLook(Transform body)
        {
            stagedLookUntil = Time.unscaledTime;
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual == null) return;
            visual.LookAt(null, 0f);
            if (walkOutTone != GoodbyeKind.Dealt) return;
            if (cameraRig != null) visual.SetFacing(cameraRig.Yaw + 180f);
            visual.React(CharacterPresentation.Reaction.Evicted);
            stagedLookUntil = Time.unscaledTime + LastLookSeconds;
        }

        /// <summary>
        /// The door opens for them - the swing, and the light beyond at its settled spill with no
        /// flare - and the camera pushes in: from the yard's door shot, where the dip already put
        /// it, or cut from the living room's exit wide to its door shot first.
        /// </summary>
        private void OpenTheExitDoor(Transform body)
        {
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual != null) visual.SetFacing(float.NaN);
            if (walkOutDoor == null) walkOutDoor = OpeningDoorSet.Build(gameObject.scene, ExitLayout);
            walkOutDoor.Open(reducedMotion, false);
            if (cameraRig == null) return;
            if (!ExitIsTheLivingRoom) { cameraRig.MoveTo(YardPushInShot); return; }
            cameraRig.MoveTo(LivingDoorShot);
            stagedPushPending = true;
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
            // A walk that began says its goodbye even when it ends short of the door - a press, the
            // house giving up on it - as it did when the line played at the walk's first leg: W0
            // moved where the line is said, not whether. Standing, since the body never reached the
            // door. Not when a competition or the stage's end takes the frame now.
            bool begun = staged ? stagedLeg >= StagedLeg.Walking : walkOutLeg >= 1;
            if (begun && !walkOutLineSaid && !immediate && sting != null && projected != null)
                sting.Play(CeremonySting.WalkOutKind, GoodbyeLine(projected, leaving, GoodbyeMoment.Standing), reducedMotion);
            walkOutLineSaid = true;
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
                    if (cameraRig != null) cameraRig.MoveTo(ExitDoorShot);
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

        /// <summary>
        /// The door and the walker's look mark come down, and whatever the door hid is back: the
        /// walk out is over, or never started. The next exit chooses its door again.
        /// </summary>
        private void StrikeWalkOutDoor()
        {
            if (walkOutDoor != null) { Destroy(walkOutDoor.gameObject); walkOutDoor = null; }
            if (walkOutLookMark != null) { Destroy(walkOutLookMark.gameObject); walkOutLookMark = null; }
            foreach (var prop in walkOutHidden) if (prop != null) prop.enabled = true;
            walkOutHidden.Clear();
            walkOutDoorUsed = WalkOutDoor.Yard;
        }

        /// <summary>
        /// The goodbye has begun (the stage calls it): the exit's door is chosen. The living room's
        /// when it is asked for and its probe passes - built now, closed and dark, at the end of the
        /// room the house is looking down, with any small prop standing in its doorway hidden while
        /// it stands - and the yard's otherwise, which goes up at the dip on the way there.
        /// </summary>
        private void OnStagedGoodbye(string id)
        {
            // The goodbye has just played their line: the walk out that follows does not again.
            goodbyeSaidBy = id;
            StrikeWalkOutDoor();
            if (WalkOutThrough != WalkOutDoor.Living) return;
            if (!LivingExitClear(BodyFor(id), out var why))
            {
                Debug.Log("Walk out (" + id + "): the living room's door is refused - " + why + " - and the yard's front door is used instead.");
                return;
            }
            walkOutDoorUsed = WalkOutDoor.Living;
            walkOutDoor = OpeningDoorSet.Build(gameObject.scene, DoorLayout.Living);
            HidePropsInTheDoorway(DoorLayout.Living);
        }

        /// <summary>
        /// The living room's doorway, measured before the door goes up in it: both marks on the
        /// living room's own flat floor - the bake once raised the doorway onto a planter's top -
        /// with room to stand, the furniture asked as well as the walls, and a complete route from
        /// where the evicted stand to the vestibule. Any refusal, and the walk goes by the yard.
        /// </summary>
        private bool LivingExitClear(Transform body, out string why)
        {
            why = null;
            if (body == null) { why = "their body is not in the house"; return false; }
            if (player == null || player.Agent == null) { why = "there is no agent to measure the floor with"; return false; }
            if (!HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out why)) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };
            var vestibule = Vector3.zero;
            foreach (var mark in new[] { LivingLastLook, LivingVestibule })
            {
                if (!rooms.TrySampleFloor(mark, BodyRadius, filter, 0.25f, out var sampled, out var room) || room != "Living")
                { why = "no living-room floor at " + mark.ToString("F2") + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : ""); return false; }
                if (Mathf.Abs(sampled.y - mark.y) > LivingFloorTolerance)
                { why = "the floor at " + mark.ToString("F2") + " stands " + sampled.y.ToString("F2") + " m up"; return false; }
                if (!rooms.HasCapsuleClearance(sampled, BodyRadius, BodyHeight, body) || InFurniture(sampled))
                { why = "no room to stand at " + sampled.ToString("F2") + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : ""); return false; }
                vestibule = sampled;
            }
            if (!NavMesh.SamplePosition(body.position, out var from, 0.5f, filter))
            { why = "no floor under them at " + body.position.ToString("F2"); return false; }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, vestibule, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            { why = "no complete route from " + from.position.ToString("F2") + " to the vestibule (" + path.status + ")"; return false; }
            return true;
        }

        /// <summary>
        /// Hides the small props whose middle stands in the door's footprint - from the vestibule's
        /// back to the leaves' swing, along the facade - while the door stands, as the screen hides
        /// the television: a speaker and a plant stood there before the doorway was cleared. Only
        /// the set dressing's, and only small ones: never a wall, a floor or a couch.
        /// </summary>
        private void HidePropsInTheDoorway(DoorLayout where)
        {
            var footprint = Rect.MinMaxRect(where.VestibuleFarX - 0.1f, where.FacadeMinZ, where.SweepFrontX + 0.1f, where.FacadeMaxZ);
            var pieces = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(node => node.name == SetPiecesRoot);
            if (pieces == null) return;
            foreach (var prop in pieces.GetComponentsInChildren<Renderer>())
            {
                if (!prop.enabled) continue;
                var bounds = prop.bounds;
                if (bounds.size.x > 1.5f || bounds.size.z > 1.5f) continue;
                if (!footprint.Contains(new Vector2(bounds.center.x, bounds.center.z))) continue;
                prop.enabled = false;
                walkOutHidden.Add(prop);
            }
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
            LastWalkOutFailure = null;
            walkOutStaged = false;
            goodbyeSaidBy = null;
            StrikeWalkOutDoor();
            juryBench.Clear();
            juryBenchSeason = null;
        }

        /// <summary>
        /// How the evicted leave things with the player, as far as the evicted could know
        /// (MOCKUP-PASS-PLAN decision 6A; UI-UX-PASS-PLAN B0): a deal between them, the player's
        /// nomination of them, the player's ballot against them where the count proved it to the one
        /// going, or none of these. Never their hidden feeling toward the player, which the line used
        /// to give away as they walked out to the jury, and never a ballot the reveal kept private. A
        /// warm goodbye - the player's ballot to keep them - is one a count that evicts them never
        /// proves from their seat, so there is none: R1 may restore it on a told source.
        /// </summary>
        public enum GoodbyeKind { Neutral, Cold, Dealt }

        /// <summary>
        /// The goodbye's tone, from what the evicted could know: a deal between them that still
        /// binds; otherwise cold for a player Head of Household with them on the block, or for the
        /// player's ballot against them where the count proved it from the evicted's own seat
        /// (<see cref="KnownBallots.ProvenFor"/>: a unanimous vote, or the player's tie-break);
        /// neutral otherwise. Pure: it reads the committed state and draws nothing.
        /// </summary>
        public static GoodbyeKind GoodbyeTone(EpisodeState state, string id)
        {
            if (state == null || id == null || state.Find(id) == null) return GoodbyeKind.Neutral;
            string you = state.playerId;
            // Mode 1's raw deals - in mode 2 the view of them, its Vote rows included (vote family V5f).
            bool dealt = state.deals != null && CommitmentReferences.RawDeals(state).Any(d => DealStatus.Binds(d.status) && d.status != DealStatus.Proposed
                && ((d.proposerId == id && d.recipientId == you) || (d.proposerId == you && d.recipientId == id)));
            if (dealt) return GoodbyeKind.Dealt;
            bool nominated = state.hohId == you && state.nominees != null && state.nominees.Contains(id);
            if (nominated) return GoodbyeKind.Cold;
            KnownBallots.ProvenFor(state, state.week, id).TryGetValue(you, out var yours);
            return yours == id ? GoodbyeKind.Cold : GoodbyeKind.Neutral;
        }

        /// <summary>
        /// Where the goodbye's line is said, so its words match the picture (UI-UX-PASS-PLAN W0):
        /// standing before the house as the card comes down - the staged goodbye, before the walk -
        /// or at the door, where an unstaged walk and a staged walk with no goodbye before it say it.
        /// </summary>
        public enum GoodbyeMoment { Standing, Doorway }

        /// <summary>The goodbye's line at the door: <see cref="GoodbyeLine(EpisodeState, string, GoodbyeMoment)"/> for <see cref="GoodbyeMoment.Doorway"/>.</summary>
        public static string GoodbyeLine(EpisodeState state, string id) => GoodbyeLine(state, id, GoodbyeMoment.Doorway);

        /// <summary>
        /// What the evicted does as they go: the reference's goodbye lines, by how they leave things
        /// with the player as far as the player knows (<see cref="GoodbyeTone"/>), for the moment the
        /// line is said - the doorway's words only at the doorway, where the strip once said "glares
        /// at you from the doorway" over a body still at its seat - and where they are going: the
        /// jury house for a juror, and nowhere named for somebody out before the jury.
        /// </summary>
        public static string GoodbyeLine(EpisodeState state, string id, GoodbyeMoment moment)
        {
            var who = state?.Find(id);
            if (who == null) return string.Empty;
            string name = who.name.Split(' ')[0];
            bool atTheDoor = moment == GoodbyeMoment.Doorway;
            string opener;
            switch (GoodbyeTone(state, id))
            {
                case GoodbyeKind.Dealt: opener = name + (atTheDoor ? " pauses at the door and turns to you…" : " finds your eye before they go…"); break;
                case GoodbyeKind.Cold: opener = name + (atTheDoor ? " glares at you from the doorway." : " stands and glares at you."); break;
                default: opener = name + (atTheDoor ? " walks to the door without looking back." : " stands without a word."); break;
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
