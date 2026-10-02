using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The ceremonies as cut scenes (CEREMONY-CUTSCENES-PLAN §1-3): the house gathers to its seats,
    /// the card plays on the set's own screen, the camera cuts between the screen and the faces in
    /// time with the card's beats, the bodies act each beat out, and everybody is let go.
    ///
    /// <para>A stage is presentation and nothing else. It commits nothing, draws from no generator
    /// and adds no saved field: every name it acts on is read from the committed state the card was
    /// given, and the card keeps the order and the timing - the stage answers the beats the card
    /// reports (<see cref="CeremonyBeat"/>), never the other way round. A skip on the card still
    /// gives up the order and never the outcome; a save mid-ceremony reloads to the state after
    /// the commit with the card already resolved, as today.</para>
    ///
    /// <para>It borrows the house the way the opening's stage does: the houseguests through the
    /// meeting coordinator's ceremony leases, the player through an activity move, the camera
    /// through shots. Reduced motion never plays it, and a batch run does not unless asked
    /// (<see cref="StagesInBatchRuns"/>, as <see cref="WalkOutsInBatchRuns"/>): the audited season
    /// walk stays on the cards. It never runs over the opening, the walk-out or the arena, and any
    /// of those, a new commit, or a season replaced under it ends it at once, with everyone let go
    /// and the camera handed back.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private CeremonyStage ceremonyStage;

        /// <summary>For tests: stage the ceremonies even in a batch run, where they are otherwise skipped as the walk-out is.</summary>
        public bool StagesInBatchRuns { get; set; }

        /// <summary>
        /// Whether ceremonies play as stages in the house at all. On for play; the look sheet turns
        /// it off, because its budgets are the cards' own and a summons would outlast them.
        /// </summary>
        public bool CeremonyStages { get; set; } = true;

        /// <summary>Whether a ceremony is staged in the house right now: gathering, playing on its screen, or being let go.</summary>
        public bool IsCeremonyStaged => ceremonyStage != null && ceremonyStage.Active;

        /// <summary>The staged ceremony's kind, or null.</summary>
        public string CeremonyStageKind => IsCeremonyStaged ? ceremonyStage.Kind : null;

        /// <summary>Where the staged ceremony is: gathering the house, playing its card, or letting go. Null when none.</summary>
        public CeremonyStageStep? CeremonyStagePhase => IsCeremonyStaged ? ceremonyStage.Step : (CeremonyStageStep?)null;

        /// <summary>Whether the staged ceremony is still telling its story: from the summons until its card is down.</summary>
        private bool CeremonyStageNarrating => ceremonyStage != null && ceremonyStage.Narrating;

        /// <summary>
        /// Whether a staged ceremony has a place for this houseguest: the house's world keeps them
        /// while it does, which is how the evicted - a non-contestant from the commit - still walk
        /// to the hot seat and hear the vote read.
        /// </summary>
        private bool CeremonyStageHolds(string id) => ceremonyStage != null && ceremonyStage.Holds(id);

        /// <summary>How many houseguests the stage has seated so far, the player included. A read for tests.</summary>
        public int CeremonyStageSeated => IsCeremonyStaged ? ceremonyStage.SeatedCount : 0;

        /// <summary>Who the stage stands at the set's head - the Head of Household - or null.</summary>
        public string CeremonyStageStanding => IsCeremonyStaged ? ceremonyStage.StandingId : null;

        /// <summary>How many of the stage's places have been reached - seated or standing in place, the player included. A read for tests.</summary>
        public int CeremonyStageInPlace => IsCeremonyStaged ? ceremonyStage.InPlaceCount : 0;

        /// <summary>How many places the stage gave out. A read for tests.</summary>
        public int CeremonyStagePlaces => IsCeremonyStaged ? ceremonyStage.PlaceCount : 0;

        /// <summary>The screen the staged ceremony plays on, or null.</summary>
        public ScreenSurface CeremonyStageScreen => IsCeremonyStaged ? ceremonyStage.Screen : null;

        /// <summary>
        /// The staged ceremony's report of where everybody it placed has got to, one line each, or
        /// null when nothing is staged. The stage logs the same report at its card's start and at
        /// its release; this is for a test to read it when it likes.
        /// </summary>
        public string CeremonyStageReport(string moment = "now") => IsCeremonyStaged ? ceremonyStage.Report(moment) : null;

        /// <summary>
        /// Stages a ceremony the commit just decided, when this house can: the house is gathered,
        /// and <paramref name="playCard"/> is called with the set's screen once the seats have
        /// filled (or the summons has run its course), instead of now. Returns false when it cannot
        /// - reduced motion, a batch run not asking, the house busy, no set for it - and the caller
        /// plays the card at once, as it always has.
        /// </summary>
        private bool TryBeginCeremonyStage(string kind, EpisodeState state, Func<ScreenSurface, bool> playCard,
            IReadOnlyList<string> blockBefore = null)
        {
            // The policy first, and silently: the look sheet, reduced motion and a batch run not
            // asking never stage, by design, and a line for each of their ceremonies would be noise
            // in every audited walk (and a failure wherever a test asks for a log with nothing
            // unexpected in it).
            if (!CeremonyStages || reducedMotion || (Application.isBatchMode && !StagesInBatchRuns) || state == null) return false;
            // Past it, a house that cannot stage says why. Every guard below used to decline in
            // silence, and a world stopped after the first staged eviction kept every later
            // ceremony on the HUD with nothing in the log to say so.
            string declined = StageDeclined(state);
            if (declined != null) { Debug.Log("Ceremony stage declined (" + kind + "): " + declined); return false; }
            EndCeremonyStage();
            var stage = CeremonyStage.TryCreate(this, kind, state, playCard, blockBefore, out var reason);
            if (stage == null) { if (reason != null) Debug.Log("Ceremony stage: " + reason); return false; }
            ceremonyStage = stage;
            // The commit's projection unbound the evicted; now that the stage holds a place for
            // them the world takes them back, and the summons' retries send them once they are bound.
            // A body that cannot take its navigation back ("The feet are not inside one bound
            // floor's safe interior.") is let go on its own, as the walk out lets its candidate
            // go, and the card stops waiting for them: it used to fail the whole house instead.
            if (kind == CeremonySting.EvictionKind && departingId != null) npcMeetings.DepartureCandidate = departingId;
            ReconcileNpcSocialWorld();
            stage.Begin();
            return true;
        }

        /// <summary>
        /// Why this house cannot stage a ceremony now, in words for the log, or null when it can.
        /// Asked only past the policy checks, which decline in silence.
        /// </summary>
        private string StageDeclined(EpisodeState state)
        {
            if (npcMeetings == null) return "the house's world is not built";
            if (npcWorldFailed) return "the house's world stopped: " + npcWorldFailure;
            if (npcDiagnosticsSuspended) return "the house's world is suspended for diagnostics";
            if (!npcMeetings.IsReady) return "the house's world is still binding its people";
            if (competitionArenaStaging) return "the competition arena has the yard";
            if (openingStage != null && openingStage.Active) return "the opening has the house";
            if (walkingOutId != null) return walkingOutId + " is still walking out";
            if (FinaleNight(state.phase)) return "it is finale night";
            if (player == null || player.Agent == null) return "the player has no body";
            if (!player.Agent.isOnNavMesh) return "the player is off the NavMesh";
            if (cameraRig == null) return "there is no camera rig";
            return null;
        }

        /// <summary>Each frame: the stage kept moving, and forgotten once it has let the house go.</summary>
        private void TickCeremonyStage()
        {
            if (ceremonyStage == null) return;
            ceremonyStage.Tick();
            if (!ceremonyStage.Active) ceremonyStage = null;
        }

        private CeremonySkipChip skipChip;

        /// <summary>Whether the skip chip is on screen. A read for tests.</summary>
        public bool CeremonySkipShowing => skipChip != null && skipChip.IsShowing;

        /// <summary>
        /// Whether a staged ceremony is running for the chip to name its skip: gathering the house,
        /// playing its card on the set's screen, or keeping its seats while the evicted walk out.
        /// Never otherwise - a card on the HUD frame says what moves it on in its own lines - and so
        /// never in a batch run not asking for stages, or under reduced motion, which stage nothing.
        /// Exactly while the stage holds the house's input (<see cref="CeremonyStage.OwnsThePress"/>),
        /// so the press the chip names is never also a press on the HUD.
        /// </summary>
        private bool SkipChipWanted => IsCeremonyStaged && ceremonyStage.OwnsThePress;

        /// <summary>The HUD's chrome on screen this frame, for the chip to stand clear of. Reused, never shared.</summary>
        private readonly List<Rect> chromeUnderSkipChip = new List<Rect>();

        private void TickSkipChip()
        {
            if (skipChip == null) return;
            bool wanted = SkipChipWanted;
            skipChip.Show(wanted);
            if (!wanted) return;
            // The chrome is back while the evicted walk out, and the chip's corner is the cast
            // strip's right-hand end: it stands clear of whatever the HUD has on screen there.
            if (hud != null) hud.ChromeOnScreen(chromeUnderSkipChip);
            else chromeUnderSkipChip.Clear();
            skipChip.StandClearOf(chromeUnderSkipChip);
        }

        /// <summary>
        /// Ends a stage now, with everyone let go: a new commit, the opening, the arena, a season
        /// replaced. Its card, if it never played, stays unplayed: whatever ended the stage has the
        /// screen now.
        /// </summary>
        private void EndCeremonyStage()
        {
            if (ceremonyStage == null) return;
            // The exit it was keeping its seats for goes with it, at once - no hold on the door and
            // no dip: whatever ended the stage has the frame now. Not from a director being
            // disabled, which an unloading scene does: nothing is projected there.
            if (walkingOutId != null && walkOutStaged && isActiveAndEnabled) FinishWalkOut(immediate: true);
            ceremonyStage.End(false);
            ceremonyStage = null;
            // At once, not on the next frame: a director disabled here has no next frame.
            if (skipChip != null) skipChip.Show(false);
        }

        /// <summary>Skips the summons: the card plays now, whoever is still walking sits down as they arrive. Public for tests.</summary>
        public void SkipCeremonySummons() => ceremonyStage?.SkipSummons();

        /// <summary>Where a staged ceremony is.</summary>
        public enum CeremonyStageStep
        {
            /// <summary>The house is walking to its seats; the card waits.</summary>
            Summons,
            /// <summary>The card is playing on the set's screen, the camera cutting on its beats.</summary>
            Playing,
            /// <summary>
            /// An eviction's card is down and the evicted stand to say goodbye: they face the house,
            /// the house looks at them and whoever sits claps, for about four seconds before the walk
            /// out (MOCKUP-PASS-PLAN M19). Still the stage's story, so the chrome stays aside.
            /// </summary>
            Goodbye,
            /// <summary>The card is down; the house keeps its seats while the evicted walk out, then gets up.</summary>
            Release,
        }

        /// <summary>How long the evicted's goodbye holds before they walk, unless a press moves it on.</summary>
        public const float GoodbyeSeconds = 4f;

        /// <summary>The warm spot over the hot seats from the vote's card to the end of the goodbye: a child of the director, found by this name.</summary>
        public const string EvictionKeyLightName = "Eviction key light";

        /// <summary>Whether the staged eviction's key light is on over the hot seats. A read for tests.</summary>
        public bool EvictionKeyLightShowing => IsCeremonyStaged && ceremonyStage.KeyLightShowing;

        /// <summary>
        /// Whether a staged eviction is still sending somebody out: from its commit until the
        /// door is shut behind them and their body is gone - the card, the goodbye and the walk out
        /// - or, once a stage has handed them to the walk out, for as long as that walk runs. The
        /// chrome stays aside for all of it (MOCKUP-PASS-PLAN M19's full-bleed exit), and it is
        /// what the hold waits for, not the cards alone: the frame between the goodbye and the walk
        /// out's first leg would otherwise hand the chrome back, and nothing takes it again.
        /// </summary>
        public bool StagedExitRunning =>
            (IsCeremonyStaged && ceremonyStage.Kind == CeremonySting.EvictionKind && (departingId != null || walkingOutId != null))
            || (walkingOutId != null && walkOutStaged);

        /// <summary>
        /// The room a ceremony is staged in: the nomination at its table, the veto meeting and the
        /// eviction in the living room (PACK8-PASS-PLAN decision 5), where the red chairs face the U.
        /// The framing's room for each (<see cref="CeremonyRoom"/>) is the same.
        /// </summary>
        public static string CeremonyStageRoom(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return "Nomination";
                case CeremonySting.VetoKind: return "Living";
                case CeremonySting.EvictionKind: return "Living";
                default: return null;
            }
        }

        /// <summary>The summons line on the strip while the house gathers.</summary>
        public static string SummonsLine(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return "The house gathers at the table for the nomination ceremony.";
                case CeremonySting.VetoKind: return "The house takes its seats for the veto meeting.";
                case CeremonySting.EvictionKind: return "The house takes its seats for the live eviction.";
                default: return null;
            }
        }

        /// <summary>How long the house is given to gather before the card plays anyway, at each pace.</summary>
        public static float SummonsSeconds(CeremonyPace pace) => pace == CeremonyPace.Quick ? 3.5f : 7f;

        /// <summary>
        /// How much longer than the summons the card waits for the ceremony's own people - the
        /// nominees in the hot seats, the Head of Household at the head - when they are still on
        /// their way: a nominee starting at the far end of the yard walks thirty metres, and no
        /// eviction plays to an empty hot seat. A press still starts the card at once.
        /// </summary>
        public const float SummonsPatience = 3.5f;
        public static float SummonsHardSeconds(CeremonyPace pace) => SummonsSeconds(pace) * SummonsPatience;

        /// <summary>The agents' walk, metres a second (HouseNpcMotion), which the summons' wait is reckoned in.</summary>
        public const float WalkSpeed = 2.2f;

        /// <summary>
        /// How long the card waits for the house, keyed to the longest route the summons sent: the
        /// walk at the agents' pace and three seconds to sit, never less than the summons and never
        /// more than the patience allows. Measured 2026-09-28: the keys played to six seated and
        /// ten still walking at a full house, because the card started the moment its one
        /// principal was in place.
        /// </summary>
        public static float SummonsHardSecondsFor(CeremonyPace pace, float longestRoute) =>
            Mathf.Clamp(Mathf.Max(0f, longestRoute) / WalkSpeed + 3f, SummonsSeconds(pace), SummonsHardSeconds(pace));

        /// <summary>The most a stage may run, in real seconds, before the house is let go whatever the card says.</summary>
        public const float CeremonyStageSeconds = 150f;

        private sealed partial class CeremonyStage
        {
            /// <summary>The shots' shared lens, so a cut between them never eases the lens.</summary>
            private const float Lens = ScreenSurface.FieldOfView;
            /// <summary>A move this short reads as a cut: the reduced-motion framing's own rule.</summary>
            private const float CutSeconds = 0.01f;
            /// <summary>A press already in flight - the one that committed the beat - does not skip the summons.</summary>
            private const float PressGuard = 0.35f;

            private readonly EpisodeDirector director;
            private readonly EpisodeState state;
            private readonly Func<ScreenSurface, bool> playCard;
            private readonly List<KeyValuePair<string, HouseInteractionAnchor>> places = new List<KeyValuePair<string, HouseInteractionAnchor>>();
            private readonly Dictionary<string, HouseInteractionAnchor> placeOf = new Dictionary<string, HouseInteractionAnchor>();
            private readonly Dictionary<string, HouseSeatPresentation> seated = new Dictionary<string, HouseSeatPresentation>();
            private readonly HashSet<string> arrived = new HashSet<string>();
            /// <summary>Why the coordinator last refused to send someone, by id: the report's "never sent".</summary>
            private readonly Dictionary<string, string> refusals = new Dictionary<string, string>();
            /// <summary>How many retries in a row have refused someone: twice over and the card stops waiting for them.</summary>
            private readonly Dictionary<string, int> refusalCounts = new Dictionary<string, int>();
            /// <summary>Whoever the summons itself could not send, and why, as the coordinator said it.</summary>
            private string summonsRefusals;
            private readonly List<Cue> cues = new List<Cue>();
            private readonly object playerOwner = new object();
            private readonly Vector3 roomCentre;
            private HouseInteractionAnchor playerPlace;
            private HouseSeatPresentation playerSeat;
            private Transform lookMark;
            /// <summary>Where each houseguest looks down at once - the evicted and the house that voted to keep them - a mark each.</summary>
            private readonly Dictionary<string, Transform> downMarks = new Dictionary<string, Transform>();
            /// <summary>The eviction's evicted and its survivor, once the result is read.</summary>
            private string leavingId, survivorId;
            private float goodbyeUntil;
            /// <summary>The warm spot over the hot seats, from the vote's card to the end of the goodbye.</summary>
            private Light keyLight;
            /// <summary>
            /// Whether the camera is still the stage's to hand back: from the summons until its
            /// release, or, for a staged exit, until the walk out hands it back itself behind the
            /// dip. End releases it only then.
            /// </summary>
            private bool holdsCamera;
            private float summonsUntil, summonsHardBy, summonsPatienceBy, endBy, pressGuardUntil, playerAlignedAt, releasedAt, retryAt;
            /// <summary>How often whoever was left out at the summons is asked again, while the house is still gathering.</summary>
            private const float RetrySeconds = 1.5f;
            private bool begun, ended, cardStarted, playerSeatedRequested, playerHeld;
            private int keysShown;
            private Collider playerCollider;
            private bool playerColliderEnabled, playerUpdatePosition, playerUpdateRotation, playerAgentParked;
            private float playerAgentRadius;
            private UnityEngine.AI.ObstacleAvoidanceType playerAgentAvoidance;

            public string Kind { get; }
            public string Room { get; }
            public ScreenSurface Screen { get; }
            public CeremonyStageStep Step { get; private set; }
            public string StandingId { get; private set; }
            public bool Active => begun && !ended;
            public bool Narrating => Active && Step != CeremonyStageStep.Release;
            /// <summary>
            /// Whether a press on the devices is the stage's: while it narrates, and while the
            /// evicted walk out under it. The skip chip is up exactly then, naming that press, and
            /// the house takes none of its own until it is down.
            /// </summary>
            public bool OwnsThePress => Narrating || (Active && Step == CeremonyStageStep.Release && director.walkingOutId != null);
            /// <summary>Whether the stage has a place for this houseguest and has not let the house go.</summary>
            public bool Holds(string id) => !ended && id != null && placeOf.ContainsKey(id);
            /// <summary>Whether the key light is on over the hot seats.</summary>
            public bool KeyLightShowing => keyLight != null && keyLight.enabled;
            /// <summary>Who the eviction's result sent out, once it is read; null before.</summary>
            public string LeavingId => leavingId;
            public int SeatedCount => seated.Values.Count(seat => seat != null && seat.Active) + (playerSeat != null && playerSeat.Active ? 1 : 0);
            public int PlaceCount => places.Count;
            /// <summary>How many places have been reached: the houseguests the coordinator says arrived, and the player by their own move.</summary>
            public int InPlaceCount
            {
                get
                {
                    int count = 0;
                    foreach (var pair in places)
                    {
                        if (pair.Key == state.playerId) { if (playerPlace != null && director.player != null && director.player.ActivityHasArrived(playerOwner)) count++; }
                        else if (arrived.Contains(pair.Key)) count++;
                    }
                    return count;
                }
            }

            private struct Cue { public float At; public Action Do; }

            private CeremonyStage(EpisodeDirector owner, string kind, string room, EpisodeState snapshot, ScreenSurface screen,
                Func<ScreenSurface, bool> card, Vector3 centre)
            {
                director = owner; Kind = kind; Room = room; state = snapshot; Screen = screen; playCard = card; roomCentre = centre;
            }

            /// <summary>
            /// The stage for this ceremony, or null with the reason: no set dressed for it, no screen
            /// in its room, or nobody in the house to gather.
            /// </summary>
            public static CeremonyStage TryCreate(EpisodeDirector owner, string kind, EpisodeState state, Func<ScreenSurface, bool> playCard,
                IReadOnlyList<string> blockBefore, out string reason)
            {
                reason = null;
                string room = CeremonyStageRoom(kind);
                if (room == null) { reason = "no set for a " + kind; return null; }
                var scene = owner.gameObject.scene;
                var marker = owner.RoomMarker(room);
                if (marker == null) { reason = "no " + room + " room"; return null; }
                CeremonySeating.Ensure(scene, state.Active.Count());
                if (!ScreenSurface.TryFind(scene, room, out var screen)) { reason = "no screen in the " + room + " room"; return null; }
                var stage = new CeremonyStage(owner, kind, room, state, screen, playCard, marker.transform.position);
                // The veto meeting seats the block as it stood before its commit (VetoBlock).
                if (blockBefore != null) stage.blockBefore.AddRange(blockBefore);
                if (!stage.Assign(out reason)) return null;
                return stage;
            }

            /// <summary>
            /// Who sits where, read from the committed state: the nomination's Head of Household at
            /// the head with everyone else round the table; the veto meeting's block in the red
            /// chairs with the house on the U (AssignVeto); the eviction's nominees in the hot seats
            /// and the rest on the gallery, the sofa and behind it. Cast order, the player first as
            /// in the cast.
            /// </summary>
            private bool Assign(out string reason)
            {
                reason = null;
                var scene = director.gameObject.scene;
                var active = state.contestants.Where(c => c.status == ContestantStatus.Active).Select(c => c.id).ToList();
                if (active.Count == 0) { reason = "nobody in the house"; return false; }
                var nominees = state.nominees ?? new List<string>();
                switch (Kind)
                {
                    case CeremonySting.VetoKind:
                        if (!AssignVeto(active, out reason)) return false;
                        break;
                    case CeremonySting.NominationKind:
                    {
                        var heads = CeremonySeating.Anchors(scene, CeremonySeating.NominationHead);
                        var seats = CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat);
                        if (seats.Count == 0) { reason = "no chairs at the table"; return false; }
                        var standing = new List<string>();
                        if (!string.IsNullOrEmpty(state.hohId) && active.Contains(state.hohId)) standing.Add(state.hohId);
                        for (int i = 0; i < standing.Count && i < heads.Count; i++) Place(standing[i], heads[i]);
                        StandingId = standing.Count > 0 ? standing[0] : null;
                        int seat = 0;
                        foreach (var id in active)
                        {
                            if (placeOf.ContainsKey(id)) continue;
                            if (seat < seats.Count) Place(id, seats[seat++]);
                        }
                        break;
                    }
                    case CeremonySting.EvictionKind:
                    {
                        var hot = CeremonySeating.Anchors(scene, CeremonySeating.HotSeat);
                        var gallery = CeremonySeating.Anchors(scene, CeremonySeating.GallerySeat);
                        var sofa = CeremonySeating.Anchors(scene, CeremonySeating.SofaSeat);
                        var marks = CeremonySeating.Anchors(scene, CeremonySeating.LivingMark);
                        if (hot.Count < 2) { reason = "no hot seats"; return false; }
                        int hotSeat = 0;
                        // The committed state has already evicted one of them: the evicted, whom the
                        // walk-out takes after the card, sits in the hot seat to hear it all the same.
                        // The player too, when the vote has already sent them to the jury: their body is
                        // in the house until the card is over.
                        foreach (var id in nominees)
                            if ((active.Contains(id) || id == director.departingId || id == state.playerId) && hotSeat < hot.Count) Place(id, hot[hotSeat++]);
                        int mark = 0;
                        // A standing mark nobody could be sent to - laid on a NavMesh edge, or where a
                        // body already stands - is passed over, not handed out.
                        HouseInteractionAnchor NextMark()
                        {
                            while (mark < marks.Count)
                            {
                                var candidate = marks[mark++];
                                if (director.npcMeetings == null || director.player == null
                                    || director.npcMeetings.CanStandAt(candidate.Position, director.player.transform, out var why)) return candidate;
                                Debug.Log("Ceremony stage (" + Kind + "): " + candidate.VenueId + " " + candidate.Slot + " is passed over: " + why);
                            }
                            return null;
                        }
                        // With a gallery the Head of Household sits with the house (the owner's call,
                        // MOCKUP-PASS-PLAN decision 3); without one they stand at the head as before.
                        if (gallery.Count == 0 && !string.IsNullOrEmpty(state.hohId) && active.Contains(state.hohId) && !placeOf.ContainsKey(state.hohId))
                        {
                            var head = NextMark();
                            if (head != null) { Place(state.hohId, head); StandingId = state.hohId; }
                        }
                        if (gallery.Count > 0 && hot.Count >= 2) galleryFocus = (hot[0].Position + hot[1].Position) * 0.5f;
                        int gallerySeat = 0, sofaSeat = 0;
                        foreach (var id in active)
                        {
                            if (placeOf.ContainsKey(id)) continue;
                            if (gallerySeat < gallery.Count) { Place(id, gallery[gallerySeat++]); continue; }
                            if (sofaSeat < sofa.Count) { Place(id, sofa[sofaSeat++]); continue; }
                            var next = NextMark();
                            if (next != null) Place(id, next);
                        }
                        break;
                    }
                    default:
                        reason = "no staging for a " + Kind;
                        return false;
                }
                if (places.Count == 0) { reason = "no places to gather to"; return false; }
                return true;
            }

            private void Place(string id, HouseInteractionAnchor anchor)
            {
                if (id == null || anchor == null || placeOf.ContainsKey(id)) return;
                placeOf[id] = anchor;
                places.Add(new KeyValuePair<string, HouseInteractionAnchor>(id, anchor));
            }

            // ------------------------------------------------------------ the summons

            /// <summary>The house sent to its seats, the strip saying why, the camera on the wide.</summary>
            public void Begin()
            {
                if (begun) return;
                begun = true;
                Step = CeremonyStageStep.Summons;
                float now = Time.unscaledTime;
                summonsUntil = now + SummonsSeconds(director.ceremonyPace);
                summonsHardBy = now + SummonsHardSeconds(director.ceremonyPace);
                endBy = now + CeremonyStageSeconds;
                pressGuardUntil = now + PressGuard;
                director.cameraRig.ControlsEnabled = false;
                director.cameraRig.MoveTo(Wide(1.2f));
                holdsCamera = true;
                if (director.sting != null) director.sting.Play(Kind, SummonsLine(Kind), director.reducedMotion);

                var ids = new List<string>(); var anchors = new List<HouseInteractionAnchor>();
                foreach (var pair in places)
                {
                    if (pair.Key == state.playerId) continue;
                    ids.Add(pair.Key); anchors.Add(pair.Value);
                }
                int sent = director.npcMeetings.BeginCeremonyStage(ids, anchors, out var left);
                summonsRefusals = left;
                if (left != null) Debug.Log("Ceremony stage (" + Kind + "): " + sent + " sent to their places. " + left);

                // The card waits for the house: as long as the longest route the summons sent
                // takes to walk, within the pace's patience.
                float longest = 0f;
                foreach (var id in ids) longest = Mathf.Max(longest, director.npcMeetings.CeremonyRouteLength(id));

                if (placeOf.TryGetValue(state.playerId, out var mine) && director.player != null)
                {
                    director.player.ReleaseActivityMove(playerOwner);
                    if (director.player.TryBeginActivityMove(playerOwner, mine.Approach, out var reason))
                    {
                        playerPlace = mine;
                        longest = Mathf.Max(longest, Flat(director.player.transform.position, mine.Approach) * 1.3f);
                    }
                    else Debug.Log("Ceremony stage (" + Kind + "): you stay where you are: " + reason);
                }
                summonsHardBy = now + SummonsHardSecondsFor(director.ceremonyPace, longest);
                // The people the card is about get the pace's whole patience, whatever the routes
                // said: the evicted, re-bound the frame the stage was made, is sent by a retry and
                // walks thirty metres from the yard, and no eviction plays to an empty hot seat.
                summonsPatienceBy = now + SummonsHardSeconds(director.ceremonyPace);
                Subscribe(true);
            }

            private void Subscribe(bool on)
            {
                if (director.keyCeremony != null)
                {
                    director.keyCeremony.BeatReached -= OnBeat;
                    if (on) director.keyCeremony.BeatReached += OnBeat;
                }
                if (director.voteReveal != null)
                {
                    director.voteReveal.BeatReached -= OnBeat;
                    if (on) director.voteReveal.BeatReached += OnBeat;
                }
                // The veto meeting's card is the takeover on the living room's screen.
                if (director.takeover != null && Kind == CeremonySting.VetoKind)
                {
                    director.takeover.BeatReached -= OnTakeoverBeat;
                    if (on) director.takeover.BeatReached += OnTakeoverBeat;
                }
            }

            public void SkipSummons()
            {
                // The ceremony's own people are not waited for either: the card plays now.
                if (Active && Step == CeremonyStageStep.Summons) summonsUntil = summonsHardBy = summonsPatienceBy = Time.unscaledTime;
            }

            /// <summary>
            /// Whether everyone the stage placed has reached their place - the player by their own
            /// move - leaving out only whoever the coordinator has refused twice over, or let go
            /// because their body could not take its navigation back, who are not coming. Not the
            /// leases' count against their arrivals: a body the summons could not send (the
            /// evicted, still binding on that frame) holds no lease yet, and the card would have
            /// started without them.
            /// </summary>
            private bool EveryoneArrived
            {
                get
                {
                    var meetings = director.npcMeetings;
                    if (meetings == null) return false;
                    foreach (var pair in places)
                    {
                        string id = pair.Key;
                        if (id == state.playerId)
                        {
                            if (playerPlace != null && !director.player.ActivityHasArrived(playerOwner)) return false;
                            continue;
                        }
                        if (arrived.Contains(id) || meetings.CeremonyActorArrived(id)) continue;
                        if (refusalCounts.TryGetValue(id, out int refused) && refused >= 2) continue;
                        if (LetGo(id)) continue;
                        return false;
                    }
                    return true;
                }
            }

            /// <summary>
            /// Whether the house let this houseguest go because their body could not take its
            /// navigation back for their place - the evicted, re-bound for the hot seat. They are
            /// not coming, and no card waits out its patience in front of their empty chair.
            /// </summary>
            private bool LetGo(string id) => director.npcMeetings != null && director.npcMeetings.CandidateDropped(id);

            /// <summary>The people the card is about: an eviction's nominees, a veto meeting's block and holder, a nomination's Head of Household - whoever of them is coming.</summary>
            private IEnumerable<string> Principals
            {
                get
                {
                    if (Kind == CeremonySting.EvictionKind)
                        foreach (var id in state.nominees ?? new List<string>()) { if (placeOf.ContainsKey(id) && !LetGo(id)) yield return id; }
                    else if (Kind == CeremonySting.VetoKind)
                        foreach (var id in VetoPrincipals) { if (placeOf.ContainsKey(id) && !LetGo(id)) yield return id; }
                    else if (StandingId != null && placeOf.ContainsKey(StandingId) && !LetGo(StandingId)) yield return StandingId;
                }
            }

            /// <summary>Whether everyone the card is about has reached their place - the player by their own move.</summary>
            private bool PrincipalsInPlace
            {
                get
                {
                    var meetings = director.npcMeetings;
                    foreach (var id in Principals)
                    {
                        if (id == state.playerId)
                        {
                            if (playerPlace != null && !director.player.ActivityHasArrived(playerOwner)) return false;
                            continue;
                        }
                        if (!arrived.Contains(id) && !(meetings != null && meetings.CeremonyActorArrived(id))) return false;
                    }
                    return true;
                }
            }

            // ------------------------------------------------------------ each frame

            public void Tick()
            {
                if (!Active) return;
                float now = Time.unscaledTime;
                if (now > endBy || director.npcMeetings == null || director.npcWorldFailed) { End(true); return; }
                // While it narrates the stage is a card, from its summons on. The HUD is held from
                // the summons and the phase panel is still open under it, so the press that starts
                // the card reached the house as well: Submit on whatever control had the keyboard,
                // Escape closing the panels, the house's shortcuts. The gate the cards stamp says
                // the press is spoken for (the card, once up, stamps it itself). So is the walk
                // out the stage keeps its seats for, while the skip chip names the press that ends
                // it: the chrome is back by then, and the episode screen the eviction was committed
                // from is still open with its way on focused, so the Enter that ended the walk
                // pressed Continue under it too, and Escape closed the panel.
                if (OwnsThePress) CeremonyOverlays.Showing();
                director.cameraRig.ControlsEnabled = false;
                director.npcMeetings.ResumeCeremonyActors();
                // Only while the house is gathering and the card plays: from the goodbye on, the
                // evicted's place is theirs no longer, and a retry would send them back to it.
                if ((Step == CeremonyStageStep.Summons || Step == CeremonyStageStep.Playing) && now >= retryAt) { retryAt = now + RetrySeconds; SendTheLeftOut(); }
                SeatArrivals();
                SeatThePlayer();

                switch (Step)
                {
                    case CeremonyStageStep.Summons:
                        bool pressed = now >= pressGuardUntil && CeremonyTakeover.SkipPressed();
                        // A press is one step (PACK8-PASS-PLAN A1): the card starts at its block or
                        // its result, as a press on the card would take it, and the next press
                        // closes it. The card cannot read the same press again: it takes none
                        // until it has been up long enough to be read.
                        if (pressed) { StartCard(); SkipTheCardToItsResult(); break; }
                        // The card waits for the whole house as long as the longest route takes,
                        // and for the people it is about as long as the pace's patience allows;
                        // past that, whoever is still walking sits as they arrive.
                        if (EveryoneArrived || (now >= summonsHardBy && (PrincipalsInPlace || now >= summonsPatienceBy))) StartCard();
                        break;
                    case CeremonyStageStep.Playing:
                        RunCues(now);
                        if (!CardPlaying) Release();
                        break;
                    case CeremonyStageStep.Goodbye:
                        RunCues(now);
                        // A commit that took the evicted from the house under the goodbye ends it:
                        // there is nobody left to walk out.
                        if (director.departingId != leavingId) { Release(); break; }
                        // A press is one step: from the goodbye straight to the door shut behind
                        // them, as a press on the walk is. The press that closed the card is not one.
                        if (now >= pressGuardUntil && CeremonyTakeover.SkipPressed()) { director.SkipWalkOut(); break; }
                        if (now >= goodbyeUntil) Release();
                        break;
                    case CeremonyStageStep.Release:
                        RunCues(now);
                        // The house keeps its seats while the evicted walk out, and gets up once they
                        // are through the door - or at once when nobody is leaving.
                        if (director.walkingOutId == null && now >= releasedAt + 0.5f) End(true);
                        break;
                }
            }

            /// <summary>
            /// Whoever the summons could not send - a route blocked by somebody still standing on
            /// it, most often the player at the station beside the head marks - is asked again
            /// while the house is still gathering and the card plays, so they arrive late rather
            /// than never.
            /// </summary>
            private void SendTheLeftOut()
            {
                var meetings = director.npcMeetings;
                if (meetings == null) return;
                foreach (var pair in places)
                {
                    string id = pair.Key;
                    if (id == state.playerId || arrived.Contains(id) || meetings.CeremonyActorHolds(id) || LetGo(id)) continue;
                    if (meetings.CeremonyPlace(id) != null) continue;
                    if (meetings.JoinCeremonyStage(id, pair.Value, out var why))
                    {
                        refusals.Remove(id); refusalCounts.Remove(id);
                        // Sent late, they get their walk: the cap stretches to their route, within the patience.
                        float route = meetings.CeremonyRouteLength(id);
                        if (route > 0f && Step == CeremonyStageStep.Summons)
                            summonsHardBy = Mathf.Max(summonsHardBy, Mathf.Min(summonsPatienceBy, Time.unscaledTime + route / WalkSpeed + 3f));
                    }
                    else if (why != null)
                    {
                        refusals[id] = why;
                        refusalCounts[id] = refusalCounts.TryGetValue(id, out int count) ? count + 1 : 1;
                    }
                }
                if (playerPlace == null && placeOf.TryGetValue(state.playerId, out var mine) && director.player != null && !playerSeatedRequested
                    && !director.player.HasActivityOwner && director.player.TryBeginActivityMove(playerOwner, mine.Approach, out _))
                    playerPlace = mine;
            }

            private bool CardPlaying =>
                (director.keyCeremony != null && director.keyCeremony.IsPlaying)
                || (director.voteReveal != null && director.voteReveal.IsPlaying)
                || (director.takeover != null && director.takeover.IsPlaying);

            /// <summary>The card just started, taken to its block or its result: the first press's step, which the cards share.</summary>
            private void SkipTheCardToItsResult()
            {
                if (!Active || Step != CeremonyStageStep.Playing) return;
                if (director.keyCeremony != null && director.keyCeremony.IsPlaying) director.keyCeremony.SkipToResult();
                else if (director.voteReveal != null && director.voteReveal.IsPlaying) director.voteReveal.SkipToResult();
                else if (director.takeover != null && director.takeover.PlayingMeeting) director.takeover.SkipToResult();
            }

            /// <summary>The card, on the screen. A card that declines its shape ends the stage: the caller's generic card plays instead.</summary>
            private void StartCard()
            {
                cardStarted = true;
                Step = CeremonyStageStep.Playing;
                bool up = playCard(Screen);
                // A card that declined the screen played nothing: the stage ends as one that never
                // played its card does, and the card plays on the HUD once nothing is staged - the
                // generic card's framing and reactions are the house's again by then. The veto
                // meeting's does this; the reveals' fall back to the generic card themselves.
                if (!up) { cardStarted = false; End(true); return; }
                if (!CardPlaying) { End(true); return; }
                Debug.Log(Report("card start"));
                Cut(Screen.Shot(CutSeconds));
                if (Kind == CeremonySting.EvictionKind) LightTheHotSeats();
            }

            /// <summary>
            /// A warm key light over the red chairs for the vote and the goodbye (MOCKUP-PASS-PLAN
            /// M19): a soft spot from above the screen's side onto the hot seats' midpoint, on the
            /// introduction's settings and warmer, from the card's start until the goodbye is over.
            /// Only on the gallery, where there is a midpoint to light.
            /// </summary>
            private void LightTheHotSeats()
            {
                if (keyLight != null || !galleryFocus.HasValue) return;
                var lamp = new GameObject(EvictionKeyLightName);
                lamp.transform.SetParent(director.transform, false);
                keyLight = lamp.AddComponent<Light>();
                keyLight.type = LightType.Spot;
                keyLight.color = new Color(1f, 0.86f, 0.66f);
                keyLight.intensity = 6f;
                keyLight.range = 9f;
                keyLight.spotAngle = 34f;
                keyLight.innerSpotAngle = 18f;
                keyLight.shadows = LightShadows.None;
                // 1.3 m toward the screen from the chairs' midpoint and 3.2 m up: above the screen's
                // side, looking down onto the faces in red.
                var target = new Vector3(galleryFocus.Value.x, 1.0f, galleryFocus.Value.z);
                var toScreen = Screen.Centre - target; toScreen.y = 0f;
                toScreen = toScreen.sqrMagnitude > 0.0001f ? toScreen.normalized : Vector3.back;
                var from = new Vector3(target.x, 3.2f, target.z) + toScreen * 1.3f;
                lamp.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));
            }

            private void PutOutTheKeyLight()
            {
                if (keyLight == null) return;
                UnityEngine.Object.Destroy(keyLight.gameObject);
                keyLight = null;
            }

            /// <summary>Everyone who has reached their place sits, or stands facing the way the place faces.</summary>
            /// <summary>
            /// Sits everyone who is at their place, and sits them again if they lost the seat: a
            /// seat ends when a neighbour squeezing past pushes the root 0.4 m, and the body walks
            /// back to its approach and arrives again (measured 2026-09-28: two at a full table,
            /// seated once and standing beside their chairs by the release). Arrival is the
            /// coordinator's word each frame, not a thing remembered once.
            /// </summary>
            private void SeatArrivals()
            {
                var meetings = director.npcMeetings;
                if (meetings == null) return;
                foreach (var pair in places)
                {
                    string id = pair.Key;
                    if (id == state.playerId) continue;
                    // Up out of the seat for good: the evicted, turned to the house for the goodbye.
                    if (stoodUp.Contains(id)) continue;
                    if (!meetings.CeremonyActorArrived(id)) continue;
                    // BodyFor, not Housemates(): the latter is the active contestants, and the
                    // evicted in the hot seat is no longer one.
                    var npc = director.BodyFor(id);
                    var visual = npc != null ? npc.GetComponent<CharacterPresentation>() : null;
                    if (visual == null) continue;
                    if (arrived.Add(id)) { visual.SetTalking(false); visual.SetArguing(false); }
                    visual.SetFacing(pair.Value.Facing);
                    if (!pair.Value.Posed) continue;
                    var seat = npc.GetComponent<HouseSeatPresentation>();
                    if (seat == null) seat = npc.gameObject.AddComponent<HouseSeatPresentation>();
                    if (!seat.isActiveAndEnabled || seat.Active || stoodUp.Contains(id)) continue;
                    string who = id;
                    seat.Begin(pair.Value, () => Active && director.npcMeetings != null && director.npcMeetings.CeremonyActorHolds(who));
                    if (seat.Active) seated[id] = seat;
                }
            }

            /// <summary>
            /// The player's seat, by the diary's sequence: arrive, turn into the chair, then the visual
            /// body sits while the navigation root waits at the approach with its collider off.
            /// </summary>
            private void SeatThePlayer()
            {
                var player = director.player;
                if (playerPlace == null || player == null || playerSeatedRequested) return;
                if (!player.IsActivityMoveValid(playerOwner)) { playerPlace = null; return; }
                if (!player.ActivityHasArrived(playerOwner)) return;
                var visual = player.GetComponent<CharacterPresentation>();
                if (!playerHeld)
                {
                    playerHeld = true;
                    player.PauseActivityMove(playerOwner, true);
                    playerAlignedAt = Time.unscaledTime;
                    if (visual != null) visual.SetFacing(playerPlace.Facing);
                    return;
                }
                if (Time.unscaledTime - playerAlignedAt < 0.25f) return;
                playerSeatedRequested = true;
                if (!playerPlace.Posed || visual == null) return;
                var agent = player.Agent;
                playerUpdatePosition = agent.updatePosition; playerUpdateRotation = agent.updateRotation;
                agent.updatePosition = false; agent.updateRotation = false;
                // Parked like a houseguest's: the player's root stays on its approach too, and at
                // its full radius it blocked the seat beside it.
                playerAgentRadius = agent.radius; playerAgentAvoidance = agent.obstacleAvoidanceType;
                agent.radius = HouseNpcMotion.ParkedRadius; agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
                playerAgentParked = true;
                playerCollider = player.GetComponent<Collider>();
                playerColliderEnabled = playerCollider != null && playerCollider.enabled;
                if (playerCollider != null) playerCollider.enabled = false;
                playerSeat = player.GetComponent<HouseSeatPresentation>();
                if (playerSeat == null) playerSeat = player.gameObject.AddComponent<HouseSeatPresentation>();
                playerSeat.Begin(playerPlace, () => Active);
            }

            // ------------------------------------------------------------ the beats

            private void OnBeat(CeremonyBeat beat)
            {
                if (!Active || Step == CeremonyStageStep.Summons) return;
                switch (Kind)
                {
                    case CeremonySting.NominationKind: OnKeyBeat(beat); break;
                    case CeremonySting.EvictionKind: OnVoteBeat(beat); break;
                }
            }

            /// <summary>The keys: cut to the screen for each, then to the face it named; the push-in before the last; the block.</summary>
            private void OnKeyBeat(CeremonyBeat beat)
            {
                var card = director.keyCeremony;
                float speed = card != null ? Mathf.Max(1f, card.SpeedMultiplier) : 1f;
                switch (beat.Kind)
                {
                    case CeremonyBeatKind.Opened:
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        break;
                    case CeremonyBeatKind.KeyShown:
                        keysShown = beat.Index + 1;
                        if (beat.Skipped) break;
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        // The screen for half the key's hold, then the face: whoever is safe, relieved,
                        // with the neighbours glancing at them and the Head of Household looking on.
                        float hold = card != null ? card.KeyHoldSeconds : 2f;
                        string safe = beat.SubjectId;
                        bool last = beat.Index == KeysInPlay - 1;
                        Schedule(hold * 0.5f / speed, () =>
                        {
                            Cut(SeatShot(safe));
                            Relief(safe, last);
                        });
                        break;
                    case CeremonyBeatKind.LastKeyPending:
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        // The screen holds the gold key; the camera pushes in on those still waiting.
                        float beatSeconds = card != null ? card.LastKeyBeatSeconds : 1.8f;
                        Schedule(0.6f / speed, () =>
                        {
                            var waiting = WaitingOnTheLastKey();
                            if (waiting.Count == 0) return;
                            var wide = GroupShot(waiting, 4.6f);
                            Cut(wide);
                            var near = GroupShot(waiting, 3.1f);
                            near.Seconds = Mathf.Max(0.4f, beatSeconds / speed - 0.6f);
                            Schedule(0.05f, () => director.cameraRig.MoveTo(near));
                        });
                        break;
                    case CeremonyBeatKind.BlockShown:
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        var block = (state.nominees ?? new List<string>()).Where(id => placeOf.ContainsKey(id)).ToList();
                        director.TurnHeads(state, block.FirstOrDefault(), state.nominees);
                        float blockHold = card != null ? card.BlockHoldSeconds : 3.6f;
                        float step = blockHold / (block.Count + 1) / speed;
                        for (int i = 0; i < block.Count; i++)
                        {
                            string nominee = block[i];
                            Schedule(step * (i + 1), () => { Cut(SeatShot(nominee)); Nominated(nominee); });
                        }
                        break;
                    case CeremonyBeatKind.Closed:
                        ClearCues();
                        Release();
                        break;
                }
            }

            /// <summary>How many keys are in play: everyone active but the Head of Household and the block.</summary>
            private int KeysInPlay => state.contestants.Count(c => c.status == ContestantStatus.Active && c.id != state.hohId
                && (state.nominees == null || !state.nominees.Contains(c.id)));

            /// <summary>Whoever has not heard their name yet: the last safe houseguest and the block, in their chairs.</summary>
            private List<string> WaitingOnTheLastKey()
            {
                var named = new HashSet<string>();
                var safeOrder = director.SafeHouseguests(state).Select(p => p.Id).ToList();
                for (int i = 0; i < keysShown && i < safeOrder.Count; i++) named.Add(safeOrder[i]);
                return places.Select(p => p.Key).Where(id => id != state.hohId && !named.Contains(id)).ToList();
            }

            /// <summary>The votes: the count on the screen, a cut to the hot seats on each, the push-in before the last, the result.</summary>
            private void OnVoteBeat(CeremonyBeat beat)
            {
                var card = director.voteReveal;
                float speed = card != null ? Mathf.Max(1f, card.SpeedMultiplier) : 1f;
                var nominees = (state.nominees ?? new List<string>()).Where(id => placeOf.ContainsKey(id)).ToList();
                switch (beat.Kind)
                {
                    case CeremonyBeatKind.Opened:
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        break;
                    case CeremonyBeatKind.VoteShown:
                        if (beat.Skipped) break;
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        float hold = card != null ? card.VoteHoldSeconds : 1.8f;
                        string against = beat.SubjectId;
                        // The cuts between votes land on the nominees and, every third vote, on the
                        // Head of Household watching the count: never on a voter's seat, which would
                        // say whose vote was just read (UI-UX-PASS-PLAN B0). The nominee the vote
                        // went against flinches, since the count against them is public.
                        bool hohBeat = beat.Index % 3 == 2 && state.hohId != null && placeOf.ContainsKey(state.hohId);
                        string hoh = hohBeat ? state.hohId : null;
                        Schedule(hold * 0.35f / speed, () =>
                        {
                            if (hoh != null) Cut(SeatShot(hoh));
                            else Cut(nominees.Count == 2 ? PairShot(nominees[0], nominees[1]) : SeatShot(against));
                            if (against != null) LookDown(against, 0.8f);
                        });
                        Schedule(hold * 0.9f / speed, () => Cut(Screen.Shot(CutSeconds)));
                        break;
                    case CeremonyBeatKind.LastVotePending:
                        ClearCues();
                        float beatSeconds = card != null ? card.LastVoteBeatSeconds : 1.6f;
                        if (nominees.Count == 2)
                        {
                            var wide = PairShot(nominees[0], nominees[1]);
                            Cut(wide);
                            var near = PairShot(nominees[0], nominees[1]);
                            near.Distance = Mathf.Max(0.9f, wide.Distance - 0.7f); near.Seconds = Mathf.Max(0.4f, beatSeconds / speed - 0.1f);
                            Schedule(0.05f, () => director.cameraRig.MoveTo(near));
                        }
                        break;
                    case CeremonyBeatKind.TieCalled:
                    case CeremonyBeatKind.TieBroken:
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        break;
                    case CeremonyBeatKind.ResultShown:
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        string leaving = beat.SubjectId;
                        string staying = nominees.FirstOrDefault(id => id != leaving);
                        leavingId = leaving; survivorId = staying;
                        // The room takes the result as it is (MOCKUP-PASS-PLAN M19): everyone turns
                        // to the one going. Nobody looks down for a ballot they cast - the result
                        // never says who voted to keep the evicted (UI-UX-PASS-PLAN B0). The survivor
                        // stays seated with no fist pump in front of the one going, and the evicted
                        // takes it in the chair, head down: they stand for the goodbye. The cuts go
                        // to the pair and then to the Head of Household, whose seat is nobody's ballot.
                        director.TurnHeads(state, leaving, null);
                        float resultHold = card != null ? card.ResultHoldSeconds : 3.6f;
                        string watching = state.hohId != null && placeOf.ContainsKey(state.hohId) ? state.hohId : null;
                        Schedule(Mathf.Min(1.5f, resultHold * 0.4f) / speed, () =>
                        {
                            if (nominees.Count == 2) Cut(PairShot(nominees[0], nominees[1]));
                            else if (leaving != null) Cut(SeatShot(leaving));
                            if (leaving != null) LookDownAt(leaving, 2f);
                        });
                        if (watching != null) Schedule(Mathf.Min(2.6f, resultHold * 0.7f) / speed, () => Cut(SeatShot(watching)));
                        break;
                    case CeremonyBeatKind.Closed:
                        ClearCues();
                        string evicted = beat.SubjectId ?? (card != null ? card.EvictedId : null);
                        // A skip can close the card before its result was read: the evicted are
                        // the card's own all the same.
                        if (evicted != null) leavingId = evicted;
                        // The evicted houseguest stands to say goodbye before the walk out takes
                        // them. Not the player - their eviction is the season's end, as it was -
                        // and not somebody whose body the house had to let go. Not from a director
                        // being disabled either: its OnDisable cancels the card, which closes it
                        // here, and an unloading scene may already have destroyed the rig the
                        // goodbye cuts with. That close takes the plain release, which checks the
                        // rig, and OnDisable ends the stage straight after.
                        if (evicted != null && evicted == director.departingId && evicted != state.playerId
                            && placeOf.ContainsKey(evicted) && !LetGo(evicted) && director.BodyFor(evicted) != null
                            && director.isActiveAndEnabled && director.cameraRig != null)
                        {
                            BeginGoodbye(evicted);
                            break;
                        }
                        // A skip can close the card before the result's cues ran: the evicted still
                        // get up, and their lease still goes, so the walk-out can take them.
                        StandUp(evicted);
                        Release();
                        break;
                }
            }

            // ------------------------------------------------------------ the bodies

            private CharacterPresentation Visual(string id)
            {
                if (id == null) return null;
                if (id == state.playerId) return director.player != null ? director.player.GetComponent<CharacterPresentation>() : null;
                // BodyFor, not Housemates(): the latter is the active contestants, and the evicted
                // in the hot seat is no longer one.
                var npc = director.BodyFor(id);
                return npc != null ? npc.GetComponent<CharacterPresentation>() : null;
            }

            private HouseSeatPresentation SeatOf(string id)
            {
                if (id == state.playerId) return playerSeat;
                return seated.TryGetValue(id, out var seat) ? seat : null;
            }

            // ------------------------------------------------------------ the report

            /// <summary>
            /// Where everybody the stage placed has got to, one line each: their place, whether the
            /// coordinator holds their route, arrived, seated, how far from their approach, what
            /// their agent says - and for anyone not in their place, which it is: never sent (with
            /// the coordinator's reason), stuck short of the approach, still walking, or arrived
            /// and not sat. Logged at the card's start and at the release, so a play session's log
            /// names why each standing houseguest stands (CEREMONY-CUTSCENES-PLAN §7.1): every
            /// seating fault C3 had was found by a report like this, not by reading.
            /// </summary>
            public string Report(string moment)
            {
                var meetings = director.npcMeetings;
                var lines = new List<string>
                {
                    "Ceremony stage report (" + Kind + ", " + Step + ", " + moment + "): " + SeatedCount + " of " + places.Count
                    + " in their seats" + (StandingId != null ? ", " + StandingId + " standing at the head" : "")
                    + (summonsRefusals != null ? "; at the summons: " + summonsRefusals : ""),
                };
                foreach (var pair in places)
                {
                    string id = pair.Key;
                    var place = pair.Value;
                    string where = " -> " + place.VenueId + " " + place.Slot + (place.Posed ? " (seated)" : " (standing)");
                    // The scene unloading under a card that is still playing destroys the anchors
                    // before the director's OnDisable cancels the card, and the release reports.
                    // The anchor's own fields still read; its transform does not. An explicit
                    // == null: a destroyed anchor is Unity's fake null, which ?. would pass.
                    if (place == null) { lines.Add("  " + id + where + " :: the place is gone (its anchor was destroyed)"); continue; }
                    if (id == state.playerId)
                    {
                        var you = director.player;
                        bool here = playerPlace != null && you != null && you.ActivityHasArrived(playerOwner);
                        lines.Add("  " + id + where + (you != null ? " at " + you.transform.position.ToString("F2")
                            + " (" + Flat(you.transform.position, place.Approach).ToString("F2") + " m from the approach)" : "")
                            + " sent=" + (playerPlace != null) + " arrived=" + here
                            + " seat=" + (playerSeat != null && playerSeat.Active ? "active" : "none")
                            + " :: " + (playerSeat != null && playerSeat.Active ? "seated" : !place.Posed && here ? "standing in place"
                                : playerPlace == null ? "never sent (the player's move was refused)" : here ? "arrived, not seated" : "walking"));
                        continue;
                    }
                    var npc = director.BodyFor(id);
                    if (npc == null) { lines.Add("  " + id + where + " :: no active body"); continue; }
                    var motion = npc.GetComponent<HouseNpcMotion>();
                    var agent = motion != null ? motion.Agent : null;
                    var seat = SeatOf(id);
                    if (seat == null) seat = npc.GetComponent<HouseSeatPresentation>();
                    bool holds = meetings != null && meetings.CeremonyActorHolds(id);
                    bool there = arrived.Contains(id) || (meetings != null && meetings.CeremonyActorArrived(id));
                    bool sitting = seat != null && seat.Active;
                    float away = Flat(npc.transform.position, place.Approach);
                    string agentSays = agent == null ? "no agent"
                        : !agent.isActiveAndEnabled ? "agent disabled"
                        : !agent.isOnNavMesh ? "agent off the NavMesh"
                        : "hasPath=" + agent.hasPath + " status=" + agent.pathStatus + " remaining=" + agent.remainingDistance.ToString("F2")
                            + " speed=" + agent.velocity.magnitude.ToString("F2") + (agent.isStopped ? " stopped" : "");
                    bool stalled = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh
                        && (agent.isStopped || agent.velocity.sqrMagnitude < 0.05f * 0.05f || !agent.hasPath
                            || agent.pathStatus != UnityEngine.AI.NavMeshPathStatus.PathComplete);
                    string arrival = holds && !there && motion != null && motion.ArrivalFailure != null ? " [" + motion.ArrivalFailure + "]" : "";
                    string verdict = sitting ? "seated"
                        : !place.Posed && there ? "standing in place"
                        : there ? "arrived, not seated"
                        : LetGo(id) ? "let go (their body could not take its navigation back)"
                        : !holds ? "never sent" + (refusals.TryGetValue(id, out var why) ? " (" + why + ")" : summonsRefusals != null ? " (at the summons)" : " (no reason recorded)")
                        : stalled ? "stuck " + away.ToString("F2") + " m short of the approach" + arrival + Nearest(id, npc.transform.position)
                        : "walking" + arrival + Nearest(id, npc.transform.position);
                    lines.Add("  " + id + where + " at " + npc.transform.position.ToString("F2") + " (" + away.ToString("F2") + " m from the approach)"
                        + " holds=" + holds + " arrived=" + there + " seat=" + (sitting ? "active" : seat != null ? "idle" : "none")
                        + (motion != null ? " lease=" + (motion.LeaseId ?? "none") + " bound=" + motion.IsBound + " state=" + motion.State
                            + (motion.FailureReason != null ? " failure='" + motion.FailureReason + "'" : "") : " no motion")
                        + " agent: " + agentSays + " :: " + verdict);
                }
                return string.Join("\n", lines);
            }

            private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

            /// <summary>
            /// Whoever of the stage's people stands within a metre of a body that is not in its
            /// place, and how far - two roots under 0.7 m apart touch - so a report of "no
            /// clearance" or of a walker barely moving names who it is up against. Empty when
            /// nobody is that close.
            /// </summary>
            private string Nearest(string id, Vector3 at)
            {
                string who = null;
                float best = 1f;
                foreach (var pair in places)
                {
                    if (pair.Key == id) continue;
                    Component body = pair.Key == state.playerId ? (Component)director.player : director.BodyFor(pair.Key);
                    if (body == null) continue;
                    float apart = Flat(at, body.transform.position);
                    if (apart < best) { best = apart; who = pair.Key; }
                }
                return who == null ? "" : " (nearest: " + who + " " + best.ToString("F2") + " m away)";
            }

            /// <summary>Relief from the chair: a look to the Head of Household, the neighbours glancing over, and on the last key the seated fist pump.</summary>
            private void Relief(string id, bool last)
            {
                var visual = Visual(id);
                if (visual == null) return;
                var head = director.BodyFor(StandingId);
                if (head != null) visual.LookAt(head, 2.5f);
                foreach (var pair in places)
                {
                    if (pair.Key == id) continue;
                    var other = Visual(pair.Key);
                    var mine = director.BodyFor(id);
                    if (other != null && mine != null && (pair.Value.Position - placeOf[id].Position).sqrMagnitude < 2.6f * 2.6f) other.LookAt(mine, 1.8f);
                }
                if (last && visual.SupportsSeated(CharacterPresentation.Reaction.Saved)) visual.React(CharacterPresentation.Reaction.Saved);
            }

            /// <summary>On the block, from the chair: the head drops. The standing take has nothing to cut to from a chair.</summary>
            private void Nominated(string id) => LookDown(id, 4f);

            private void Reaction(string id, CharacterPresentation.Reaction kind)
            {
                var visual = Visual(id);
                if (visual != null) visual.React(kind);
            }

            /// <summary>A glance at the floor in front of the chair: the flinch of a vote against you, and the one going as the result is read.</summary>
            private void LookDown(string id, float seconds)
            {
                var visual = Visual(id);
                if (visual == null || !placeOf.TryGetValue(id, out var place)) return;
                if (lookMark == null) lookMark = new GameObject("Ceremony look mark").transform;
                lookMark.position = place.Position + Quaternion.Euler(0f, place.Facing, 0f) * Vector3.forward * 0.6f + Vector3.up * 0.4f;
                visual.LookAtPoint(lookMark, seconds);
            }

            /// <summary>The evicted get up out of the hot seat; their lease goes with them, so the walk-out can take them.</summary>
            private void StandUp(string id)
            {
                if (id == null || !stoodUp.Add(id)) return;
                var seat = SeatOf(id);
                if (seat != null && seat.Active) seat.RequestExit();
                if (id == state.playerId) ReleaseThePlayer();
                else if (director.npcMeetings != null) director.npcMeetings.ReleaseCeremonyActor(id);
                var visual = Visual(id);
                if (visual != null) visual.SetFacing(float.NaN);
            }

            private readonly HashSet<string> stoodUp = new HashSet<string>();

            /// <summary>
            /// <see cref="LookDown"/> with a mark of its own for each houseguest, so several can look
            /// down at once: the one shared mark moves to the last of them, and the rest would look at
            /// somebody else's floor.
            /// </summary>
            private void LookDownAt(string id, float seconds)
            {
                var visual = Visual(id);
                if (visual == null || !placeOf.TryGetValue(id, out var place)) return;
                if (!downMarks.TryGetValue(id, out var mark) || mark == null)
                {
                    mark = new GameObject("Ceremony look mark · " + id).transform;
                    downMarks[id] = mark;
                }
                mark.position = place.Position + Quaternion.Euler(0f, place.Facing, 0f) * Vector3.forward * 0.6f + Vector3.up * 0.4f;
                visual.LookAtPoint(mark, seconds);
            }

            // ------------------------------------------------------------ the goodbye

            /// <summary>How far above the feet a standing face is: the face shot's focus on somebody on their feet.</summary>
            private const float StandingFace = 1.55f;

            /// <summary>
            /// The evicted's goodbye (MOCKUP-PASS-PLAN M19), about four seconds between the card and
            /// the walk out. They get up out of the red chair - which lets their ceremony lease go,
            /// for the walk out to take them - and turn to the house; every head turns to them, and
            /// whoever is seated claps them out. Somebody standing only looks: the standing cheer is
            /// a celebration, and the player is the player's to move. The camera cuts in on their
            /// face and pushes in, and their goodbye line plays on the strip, moved here from the
            /// door. A press goes straight to the door shut behind them; the press that closed the
            /// card does not.
            /// </summary>
            private void BeginGoodbye(string id)
            {
                leavingId = id;
                Step = CeremonyStageStep.Goodbye;
                float now = Time.unscaledTime;
                goodbyeUntil = now + GoodbyeSeconds;
                pressGuardUntil = now + PressGuard;
                StandUp(id);
                var body = director.BodyFor(id);
                var visual = Visual(id);
                // Turned to the middle of the house: everybody still in their places.
                var middle = Vector3.zero;
                int count = 0;
                foreach (var pair in places)
                {
                    if (pair.Key == id || pair.Value == null || LetGo(pair.Key)) continue;
                    middle += pair.Value.Position;
                    count++;
                }
                float facing = body != null ? body.eulerAngles.y : 0f;
                if (count > 0 && body != null)
                {
                    var toward = middle / count - body.position;
                    toward.y = 0f;
                    if (toward.sqrMagnitude > 0.01f) facing = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
                }
                if (visual != null) visual.SetFacing(facing);
                if (body != null)
                    foreach (var pair in places)
                    {
                        if (pair.Key == id) continue;
                        var other = Visual(pair.Key);
                        if (other == null) continue;
                        other.LookAt(body, 2f);
                        if (pair.Key != state.playerId && other.IsSeated && other.SupportsSeated(CharacterPresentation.Reaction.Cheered))
                            other.React(CharacterPresentation.Reaction.Cheered);
                    }
                if (body != null)
                {
                    var face = body.position + Vector3.up * StandingFace;
                    var close = new HouseCameraRig.Shot
                    {
                        Focus = face, Distance = 3.1f, Pitch = 10f, Yaw = facing + 180f,
                        FieldOfView = Lens, Seconds = CutSeconds, DepthOfFieldWeight = 0.5f,
                    };
                    Cut(close);
                    var closer = close;
                    closer.Distance = 2.4f;
                    closer.Seconds = 2f;
                    Schedule(0.05f, () => director.cameraRig.MoveTo(closer));
                }
                // Their line, worded for where they are: standing before the house, not at the door
                // (UI-UX-PASS-PLAN W0). The walk out does not say it again.
                if (director.sting != null) director.sting.Play(CeremonySting.WalkOutKind, GoodbyeLine(state, id, GoodbyeMoment.Standing), director.reducedMotion);
                // The exit's door, chosen now: the living room's goes up closed at the end of the
                // room for the walk to come (MOCKUP-PASS-PLAN M23), the yard's at the dip later.
                director.OnStagedGoodbye(id);
            }

            /// <summary>The goodbye over, from the director's side: a press that went straight to the shut door.</summary>
            public void EndTheGoodbye()
            {
                if (Active && Step == CeremonyStageStep.Goodbye) Release();
            }

            // ------------------------------------------------------------ the walk out, watched

            /// <summary>
            /// The house watching the evicted go (MOCKUP-PASS-PLAN M19): the camera on the house,
            /// every head following the body. On the yard's walk - the long one, which leaves the
            /// room - the wide for three seconds, the survivor for two, and the house again until the
            /// walk out cuts to the door; on the living room's, <paramref name="exitWide"/> over the
            /// seated heads, looking down the room at the door, until the door opens.
            /// </summary>
            public void WatchTheWalk(Transform body, HouseCameraRig.Shot? exitWide, float watchSeconds)
            {
                if (!Active || body == null) return;
                ClearCues();
                foreach (var pair in places)
                {
                    if (pair.Key == leavingId) continue;
                    var other = Visual(pair.Key);
                    if (other != null) other.LookAt(body, exitWide.HasValue ? watchSeconds * 3f : watchSeconds);
                }
                if (exitWide.HasValue) { Cut(exitWide.Value); return; }
                Cut(Wide(CutSeconds));
                string staying = survivorId;
                if (staying == null || !placeOf.ContainsKey(staying)) return;
                Schedule(watchSeconds, () => Cut(SeatShot(staying)));
                Schedule(watchSeconds + 2f, () => Cut(Wide(CutSeconds)));
            }

            /// <summary>The walk out has the camera now: no cue of the watch cuts away from the door.</summary>
            public void StopTheWatch() => ClearCues();

            /// <summary>The walk out has handed the camera back itself, behind the dip: the stage's end leaves it alone.</summary>
            public void HandBackCamera() => holdsCamera = false;

            // ------------------------------------------------------------ the shots

            private void Cut(HouseCameraRig.Shot shot)
            {
                shot.Seconds = CutSeconds;
                director.cameraRig.MoveTo(shot);
            }

            /// <summary>The wide from across the set, looking at the screen over the chairs, as the house gathers.</summary>
            /// <summary>
            /// The establishing wide. On the gallery it looks over the U's base at the red chairs and
            /// the screen behind them, as the storyboard's first frame does, low enough to keep the
            /// seated heads in the foreground; elsewhere it frames the room from its marker.
            /// </summary>
            private HouseCameraRig.Shot Wide(float seconds) => galleryFocus.HasValue
                ? new HouseCameraRig.Shot
                {
                    Focus = new Vector3(galleryFocus.Value.x, 1.0f, galleryFocus.Value.z), Distance = 6f, Pitch = 13f, Yaw = Screen.LookYaw,
                    FieldOfView = Lens, Seconds = seconds, DepthOfFieldWeight = 0.3f,
                }
                : new HouseCameraRig.Shot
                {
                    Focus = new Vector3(roomCentre.x, 1.0f, roomCentre.z), Distance = 7f, Pitch = 22f, Yaw = Screen.LookYaw,
                    FieldOfView = Lens, Seconds = seconds, DepthOfFieldWeight = 0f,
                };

            /// <summary>The red chairs' midpoint, when the eviction is staged on the gallery; the wide looks at it.</summary>
            private Vector3? galleryFocus;

            /// <summary>A medium shot of somebody in their place, from in front of them.</summary>
            private HouseCameraRig.Shot SeatShot(string id)
            {
                if (id == null || !placeOf.TryGetValue(id, out var place)) return Screen.Shot(CutSeconds);
                var focus = FaceOf(id, place);
                return new HouseCameraRig.Shot
                {
                    Focus = focus, Distance = Reach(focus, place.Facing, 1.9f), Pitch = 12f, Yaw = place.Facing + 180f,
                    FieldOfView = Lens, Seconds = CutSeconds, DepthOfFieldWeight = 0.6f,
                };
            }

            /// <summary>The two-shot over two places side by side - the hot seats - from in front.</summary>
            private HouseCameraRig.Shot PairShot(string a, string b)
            {
                if (!placeOf.TryGetValue(a, out var first) || !placeOf.TryGetValue(b, out var second)) return SeatShot(a);
                var focus = (FaceOf(a, first) + FaceOf(b, second)) * 0.5f - Vector3.up * 0.1f;
                float facing = Mathf.LerpAngle(first.Facing, second.Facing, 0.5f);
                return new HouseCameraRig.Shot
                {
                    Focus = focus, Distance = Reach(focus, facing, 2.4f), Pitch = 12f, Yaw = facing + 180f,
                    FieldOfView = Lens, Seconds = CutSeconds, DepthOfFieldWeight = 0.4f,
                };
            }

            /// <summary>
            /// How far in front of a face the camera may sit: the distance wanted, or short of the
            /// screen when the face looks at it - the hot seats face a screen two metres away, and a
            /// camera past its face would be behind it. The rig's occlusion looks through furniture
            /// on purpose, so the screen has to be kept out of the way here.
            /// </summary>
            private float Reach(Vector3 face, float facing, float wanted)
            {
                var forward = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
                float toward = Vector3.Dot(forward, -Screen.Normal);
                if (toward <= 0.05f) return wanted;
                float along = Vector3.Dot(Screen.Centre - face, -Screen.Normal) / toward;
                return Mathf.Clamp(Mathf.Min(wanted, along - 0.4f), 0.9f, wanted);
            }

            /// <summary>A shot of several places together, from the side of the table that faces most of them.</summary>
            private HouseCameraRig.Shot GroupShot(List<string> ids, float distance)
            {
                var centre = Vector3.zero; float yaw = 0f; int count = 0;
                Vector2 facing = Vector2.zero;
                foreach (var id in ids)
                {
                    if (!placeOf.TryGetValue(id, out var place)) continue;
                    centre += FaceOf(id, place); count++;
                    facing += new Vector2(Mathf.Sin(place.Facing * Mathf.Deg2Rad), Mathf.Cos(place.Facing * Mathf.Deg2Rad));
                }
                if (count == 0) return Screen.Shot(CutSeconds);
                centre /= count;
                yaw = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg + 180f;
                return new HouseCameraRig.Shot
                {
                    Focus = centre - Vector3.up * 0.15f, Distance = distance, Pitch = 12f, Yaw = yaw,
                    FieldOfView = Lens, Seconds = CutSeconds, DepthOfFieldWeight = 0.3f,
                };
            }

            /// <summary>Where somebody's face is: the seated body's head, or head height over their place.</summary>
            private Vector3 FaceOf(string id, HouseInteractionAnchor place)
            {
                var seat = SeatOf(id);
                if (seat != null && seat.Active && seat.Settled) return seat.VisualFocus;
                return place.Position + Vector3.up * (place.Posed ? 1.05f : 1.55f);
            }

            // ------------------------------------------------------------ the cues

            private void Schedule(float delay, Action action) => cues.Add(new Cue { At = Time.unscaledTime + Mathf.Max(0f, delay), Do = action });

            private void ClearCues() => cues.Clear();

            private void RunCues(float now)
            {
                if (cues.Count == 0) return;
                var due = cues.Where(cue => cue.At <= now).OrderBy(cue => cue.At).ToList();
                if (due.Count == 0) return;
                cues.RemoveAll(cue => cue.At <= now);
                foreach (var cue in due) { if (!Active) break; cue.Do(); }
            }

            // ------------------------------------------------------------ the release

            /// <summary>
            /// The card is down. The camera goes back to the viewer; the house keeps its seats only
            /// while the evicted walk out, and the stage ends once they are through the door.
            /// </summary>
            private void Release()
            {
                if (!Active || Step == CeremonyStageStep.Release) return;
                Step = CeremonyStageStep.Release;
                releasedAt = Time.unscaledTime;
                Debug.Log(Report("release"));
                ClearCues();
                // The goodbye's light goes with the goodbye; the house watches the walk in the room's own.
                PutOutTheKeyLight();
                // A staged exit keeps the camera: the walk out frames the house watching them go,
                // the door and its close, and hands the camera back itself behind the dip.
                bool exit = Kind == CeremonySting.EvictionKind && leavingId != null && director.departingId == leavingId;
                // The rig may be gone too when the release comes from an unloading scene: End checks the same.
                if (director.cameraRig != null)
                {
                    if (!exit)
                    {
                        director.cameraRig.ReleaseShot(1.2f);
                        holdsCamera = false;
                    }
                    director.cameraRig.ControlsEnabled = !director.IsPanelOpen;
                }
                if (Kind != CeremonySting.EvictionKind) End(true);
            }

            /// <summary>
            /// Lets the house have its people back: seats emptied, leases released, the camera the
            /// viewer's, the player theirs. A stage that ends before its card played plays it on
            /// the HUD when <paramref name="playCardIfUnplayed"/> - the house could not be gathered,
            /// so the card goes as it always did and the beat is never silent - and not when
            /// something else took the screen.
            /// </summary>
            public void End(bool playCardIfUnplayed)
            {
                if (ended) return;
                // A stage ending before its card played is the report's own subject: who never got there.
                if (begun && !cardStarted) Debug.Log(Report("ended before the card"));
                ended = true;
                ClearCues();
                // Nobody the veto meeting set talking is left mid-word by a stage ended under it.
                Hush();
                Subscribe(false);
                foreach (var seat in seated.Values) if (seat != null && seat.Active) seat.RequestExit();
                seated.Clear();
                foreach (var pair in places)
                {
                    var visual = Visual(pair.Key);
                    if (visual != null) { visual.SetFacing(float.NaN); visual.LookAt(null, 0f); }
                }
                if (director.npcMeetings != null) director.npcMeetings.EndCeremonyStage();
                ReleaseThePlayer();
                if (lookMark != null) { UnityEngine.Object.Destroy(lookMark.gameObject); lookMark = null; }
                foreach (var mark in downMarks.Values) if (mark != null) UnityEngine.Object.Destroy(mark.gameObject);
                downMarks.Clear();
                PutOutTheKeyLight();
                // A door the exit built and never walked through goes with the house getting up.
                director.StrikeWalkOutDoorIfNobodyWalks();
                // Whenever the stage still holds the camera: a release that kept it for the exit,
                // and an exit that never took it, hand it back here. Not one the walk out already
                // handed back - the next owner (the arena, say) may have it by now.
                if (director.cameraRig != null && begun)
                {
                    if (holdsCamera) director.cameraRig.ReleaseShot(1.2f);
                    holdsCamera = false;
                    director.cameraRig.ControlsEnabled = !director.IsPanelOpen;
                }
                if (!cardStarted && playCardIfUnplayed && playCard != null)
                {
                    // Ended before the card played - the house could not be gathered after all - so
                    // the card plays on the HUD, as it always did, and the beat is never silent.
                    cardStarted = true;
                    playCard(null);
                }
            }

            private void ReleaseThePlayer()
            {
                var player = director.player;
                if (playerSeat != null) { playerSeat.RequestExit(); }
                if (player != null)
                {
                    var visual = player.GetComponent<CharacterPresentation>();
                    if (playerSeatedRequested && playerPlace != null && playerPlace.Posed)
                    {
                        var agent = player.Agent;
                        if (agent != null)
                        {
                            agent.updatePosition = playerUpdatePosition; agent.updateRotation = playerUpdateRotation;
                            if (playerAgentParked) { agent.radius = playerAgentRadius; agent.obstacleAvoidanceType = playerAgentAvoidance; }
                        }
                        playerAgentParked = false;
                        if (playerCollider != null) playerCollider.enabled = playerColliderEnabled;
                    }
                    if (visual != null) { visual.SetFacing(float.NaN); visual.LookAt(null, 0f); }
                    player.ReleaseActivityMove(playerOwner);
                }
                playerSeat = null;
                playerPlace = null;
                playerSeatedRequested = false;
                playerHeld = false;
            }
        }
    }
}
