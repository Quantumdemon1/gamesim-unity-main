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

        /// <summary>How many houseguests the stage has seated so far, the player included. A read for tests.</summary>
        public int CeremonyStageSeated => IsCeremonyStaged ? ceremonyStage.SeatedCount : 0;

        /// <summary>Who the stage stands at the set's head - the Head of Household - or null.</summary>
        public string CeremonyStageStanding => IsCeremonyStaged ? ceremonyStage.StandingId : null;

        /// <summary>The screen the staged ceremony plays on, or null.</summary>
        public ScreenSurface CeremonyStageScreen => IsCeremonyStaged ? ceremonyStage.Screen : null;

        /// <summary>
        /// Stages a ceremony the commit just decided, when this house can: the house is gathered,
        /// and <paramref name="playCard"/> is called with the set's screen once the seats have
        /// filled (or the summons has run its course), instead of now. Returns false when it cannot
        /// - reduced motion, a batch run not asking, the house busy, no set for it - and the caller
        /// plays the card at once, as it always has.
        /// </summary>
        private bool TryBeginCeremonyStage(string kind, EpisodeState state, Func<ScreenSurface, bool> playCard)
        {
            if (!CeremonyStages || reducedMotion || (Application.isBatchMode && !StagesInBatchRuns) || state == null) return false;
            if (npcMeetings == null || !npcMeetings.IsReady || npcWorldFailed || npcDiagnosticsSuspended || competitionArenaStaging) return false;
            if (openingStage != null && openingStage.Active) return false;
            if (walkingOutId != null || FinaleNight(state.phase)) return false;
            if (player == null || player.Agent == null || !player.Agent.isOnNavMesh || cameraRig == null) return false;
            EndCeremonyStage();
            var stage = CeremonyStage.TryCreate(this, kind, state, playCard, out var reason);
            if (stage == null) { if (reason != null) Debug.Log("Ceremony stage: " + reason); return false; }
            ceremonyStage = stage;
            stage.Begin();
            return true;
        }

        /// <summary>Each frame: the stage kept moving, and forgotten once it has let the house go.</summary>
        private void TickCeremonyStage()
        {
            if (ceremonyStage == null) return;
            ceremonyStage.Tick();
            if (!ceremonyStage.Active) ceremonyStage = null;
        }

        /// <summary>
        /// Ends a stage now, with everyone let go: a new commit, the opening, the arena, a season
        /// replaced. Its card, if it never played, stays unplayed: whatever ended the stage has the
        /// screen now.
        /// </summary>
        private void EndCeremonyStage()
        {
            if (ceremonyStage == null) return;
            ceremonyStage.End(false);
            ceremonyStage = null;
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
            /// <summary>The card is down; the house keeps its seats while the evicted walk out, then gets up.</summary>
            Release,
        }

        /// <summary>The room a ceremony is staged in: the plan's sets, not the framing's rooms - the veto meeting sits at the nomination table.</summary>
        public static string CeremonyStageRoom(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return "Nomination";
                case CeremonySting.VetoKind: return "Nomination";
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
                case CeremonySting.VetoKind: return "The house gathers at the table for the veto meeting.";
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

        /// <summary>The most a stage may run, in real seconds, before the house is let go whatever the card says.</summary>
        public const float CeremonyStageSeconds = 150f;

        private sealed class CeremonyStage
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
            private readonly List<Cue> cues = new List<Cue>();
            private readonly object playerOwner = new object();
            private readonly Vector3 roomCentre;
            private HouseInteractionAnchor playerPlace;
            private HouseSeatPresentation playerSeat;
            private Transform lookMark;
            private float summonsUntil, summonsHardBy, endBy, pressGuardUntil, playerAlignedAt, releasedAt, retryAt;
            /// <summary>How often whoever was left out at the summons is asked again, while the house is still gathering.</summary>
            private const float RetrySeconds = 1.5f;
            private bool begun, ended, cardStarted, playerSeatedRequested, playerHeld;
            private int keysShown;
            private Collider playerCollider;
            private bool playerColliderEnabled, playerUpdatePosition, playerUpdateRotation;

            public string Kind { get; }
            public string Room { get; }
            public ScreenSurface Screen { get; }
            public CeremonyStageStep Step { get; private set; }
            public string StandingId { get; private set; }
            public bool Active => begun && !ended;
            public bool Narrating => Active && Step != CeremonyStageStep.Release;
            public int SeatedCount => seated.Values.Count(seat => seat != null && seat.Active) + (playerSeat != null && playerSeat.Active ? 1 : 0);

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
            public static CeremonyStage TryCreate(EpisodeDirector owner, string kind, EpisodeState state, Func<ScreenSurface, bool> playCard, out string reason)
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
                if (!stage.Assign(out reason)) return null;
                return stage;
            }

            /// <summary>
            /// Who sits where, read from the committed state: the nomination's Head of Household at
            /// the head with everyone else round the table; the veto's holder beside them; the
            /// eviction's nominees in the hot seats, the Head of Household standing to one side,
            /// the rest on the sofa and behind it. Cast order, the player first as in the cast.
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
                    case CeremonySting.NominationKind:
                    case CeremonySting.VetoKind:
                    {
                        var heads = CeremonySeating.Anchors(scene, CeremonySeating.NominationHead);
                        var seats = CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat);
                        if (seats.Count == 0) { reason = "no chairs at the table"; return false; }
                        var standing = new List<string>();
                        if (!string.IsNullOrEmpty(state.hohId) && active.Contains(state.hohId)) standing.Add(state.hohId);
                        if (Kind == CeremonySting.VetoKind && !string.IsNullOrEmpty(state.vetoHolderId) && active.Contains(state.vetoHolderId)
                            && !standing.Contains(state.vetoHolderId)) standing.Add(state.vetoHolderId);
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
                        var sofa = CeremonySeating.Anchors(scene, CeremonySeating.SofaSeat);
                        var marks = CeremonySeating.Anchors(scene, CeremonySeating.LivingMark);
                        if (hot.Count < 2) { reason = "no hot seats"; return false; }
                        int hotSeat = 0;
                        // The committed state has already evicted one of them: the evicted, whom the
                        // walk-out takes after the card, sits in the hot seat to hear it all the same.
                        foreach (var id in nominees)
                            if ((active.Contains(id) || id == director.departingId) && hotSeat < hot.Count) Place(id, hot[hotSeat++]);
                        int mark = 0;
                        if (!string.IsNullOrEmpty(state.hohId) && active.Contains(state.hohId) && !placeOf.ContainsKey(state.hohId) && marks.Count > 0)
                        { Place(state.hohId, marks[mark++]); StandingId = state.hohId; }
                        int sofaSeat = 0;
                        foreach (var id in active)
                        {
                            if (placeOf.ContainsKey(id)) continue;
                            if (sofaSeat < sofa.Count) Place(id, sofa[sofaSeat++]);
                            else if (mark < marks.Count) Place(id, marks[mark++]);
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
                if (director.sting != null) director.sting.Play(Kind, SummonsLine(Kind), director.reducedMotion);

                var ids = new List<string>(); var anchors = new List<HouseInteractionAnchor>();
                foreach (var pair in places)
                {
                    if (pair.Key == state.playerId) continue;
                    ids.Add(pair.Key); anchors.Add(pair.Value);
                }
                int sent = director.npcMeetings.BeginCeremonyStage(ids, anchors, out var left);
                if (left != null) Debug.Log("Ceremony stage (" + Kind + "): " + sent + " sent to their places. " + left);

                if (placeOf.TryGetValue(state.playerId, out var mine) && director.player != null)
                {
                    director.player.ReleaseActivityMove(playerOwner);
                    if (director.player.TryBeginActivityMove(playerOwner, mine.Approach, out var reason)) playerPlace = mine;
                    else Debug.Log("Ceremony stage (" + Kind + "): you stay where you are: " + reason);
                }
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
            }

            public void SkipSummons()
            {
                if (Active && Step == CeremonyStageStep.Summons) summonsUntil = summonsHardBy = Time.unscaledTime;
            }

            private bool EveryoneArrived
            {
                get
                {
                    var meetings = director.npcMeetings;
                    if (meetings == null || meetings.CeremonyArrivals < meetings.CeremonyStageCount) return false;
                    return playerPlace == null || director.player.ActivityHasArrived(playerOwner);
                }
            }

            /// <summary>The people the card is about: an eviction's nominees, a nomination's or a veto's Head of Household.</summary>
            private IEnumerable<string> Principals
            {
                get
                {
                    if (Kind == CeremonySting.EvictionKind)
                        foreach (var id in state.nominees ?? new List<string>()) { if (placeOf.ContainsKey(id)) yield return id; }
                    else if (StandingId != null && placeOf.ContainsKey(StandingId)) yield return StandingId;
                }
            }

            /// <summary>Whether everyone the card is about has reached their place - the player by their own move.</summary>
            private bool PrincipalsInPlace
            {
                get
                {
                    foreach (var id in Principals)
                    {
                        if (id == state.playerId)
                        {
                            if (playerPlace != null && !director.player.ActivityHasArrived(playerOwner)) return false;
                            continue;
                        }
                        if (!arrived.Contains(id)) return false;
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
                director.cameraRig.ControlsEnabled = false;
                director.npcMeetings.ResumeCeremonyActors();
                if (Step != CeremonyStageStep.Release && now >= retryAt) { retryAt = now + RetrySeconds; SendTheLeftOut(); }
                SeatArrivals();
                SeatThePlayer();

                switch (Step)
                {
                    case CeremonyStageStep.Summons:
                        bool pressed = now >= pressGuardUntil && CeremonyTakeover.SkipPressed();
                        // Past the summons the card waits only for the people it is about, and
                        // for them only as long as the patience allows.
                        if (EveryoneArrived || pressed || (now >= summonsUntil && (PrincipalsInPlace || now >= summonsHardBy))) StartCard();
                        break;
                    case CeremonyStageStep.Playing:
                        RunCues(now);
                        if (!CardPlaying) Release();
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
                    if (id == state.playerId || arrived.Contains(id) || meetings.CeremonyActorHolds(id)) continue;
                    if (meetings.CeremonyPlace(id) != null) continue;
                    meetings.JoinCeremonyStage(id, pair.Value, out _);
                }
                if (playerPlace == null && placeOf.TryGetValue(state.playerId, out var mine) && director.player != null && !playerSeatedRequested
                    && !director.player.HasActivityOwner && director.player.TryBeginActivityMove(playerOwner, mine.Approach, out _))
                    playerPlace = mine;
            }

            private bool CardPlaying =>
                (director.keyCeremony != null && director.keyCeremony.IsPlaying)
                || (director.voteReveal != null && director.voteReveal.IsPlaying)
                || (director.takeover != null && director.takeover.IsPlaying);

            /// <summary>The card, on the screen. A card that declines its shape ends the stage: the caller's generic card plays instead.</summary>
            private void StartCard()
            {
                cardStarted = true;
                Step = CeremonyStageStep.Playing;
                bool up = playCard(Screen);
                if (!up || !CardPlaying) { End(true); return; }
                Cut(Screen.Shot(CutSeconds));
            }

            /// <summary>Everyone who has reached their place sits, or stands facing the way the place faces.</summary>
            private void SeatArrivals()
            {
                foreach (var pair in places)
                {
                    string id = pair.Key;
                    if (id == state.playerId || arrived.Contains(id)) continue;
                    if (!director.npcMeetings.CeremonyActorArrived(id)) continue;
                    arrived.Add(id);
                        // BodyFor, not Housemates(): the latter is the active contestants, and the
                    // evicted in the hot seat is no longer one.
                    var npc = director.BodyFor(id);
                    var visual = npc != null ? npc.GetComponent<CharacterPresentation>() : null;
                    if (visual == null) continue;
                    visual.SetTalking(false); visual.SetArguing(false);
                    visual.SetFacing(pair.Value.Facing);
                    if (!pair.Value.Posed) continue;
                    var seat = npc.GetComponent<HouseSeatPresentation>();
                    if (seat == null) seat = npc.gameObject.AddComponent<HouseSeatPresentation>();
                    if (!seat.isActiveAndEnabled || seat.Active) continue;
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
                        bool voterBeat = beat.Index % 3 == 2;
                        string voter = voterBeat ? VoterOf(beat.Index) : null;
                        Schedule(hold * 0.35f / speed, () =>
                        {
                            if (voter != null && placeOf.ContainsKey(voter)) { Cut(SeatShot(voter)); LookDown(voter, 1.2f); }
                            else
                            {
                                Cut(nominees.Count == 2 ? PairShot(nominees[0], nominees[1]) : SeatShot(against));
                                if (against != null) LookDown(against, 0.8f);
                            }
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
                        director.TurnHeads(state, leaving, null);
                        float resultHold = card != null ? card.ResultHoldSeconds : 3.6f;
                        Schedule(Mathf.Min(1.5f, resultHold * 0.4f) / speed, () =>
                        {
                            if (nominees.Count == 2) Cut(PairShot(nominees[0], nominees[1]));
                            else if (leaving != null) Cut(SeatShot(leaving));
                            if (staying != null) Reaction(staying, CharacterPresentation.Reaction.Won);
                            if (leaving != null) StandUp(leaving);
                        });
                        // Up and turned to the room by the time the card is down: the standing take
                        // plays once the body is on its feet.
                        Schedule(Mathf.Min(2.4f, resultHold * 0.65f) / speed, () => { if (leaving != null) Reaction(leaving, CharacterPresentation.Reaction.Evicted); });
                        break;
                    case CeremonyBeatKind.Closed:
                        ClearCues();
                        // A skip can close the card before the result's cues ran: the evicted still
                        // get up, and their lease still goes, so the walk-out can take them.
                        StandUp(beat.SubjectId ?? (card != null ? card.EvictedId : null));
                        Release();
                        break;
                }
            }

            /// <summary>Who cast the house's vote at this index: the committed ballot's voter, by name.</summary>
            private string VoterOf(int index)
            {
                if (state.votes == null) return null;
                int house = 0;
                foreach (var vote in state.votes)
                {
                    if (!string.IsNullOrEmpty(state.hohId) && vote.voterId == state.hohId) continue;
                    if (house++ == index) return vote.voterId;
                }
                return null;
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

            /// <summary>A glance at the floor in front of the chair: the flinch of a vote against you, or a voter looking at their hands.</summary>
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

            // ------------------------------------------------------------ the shots

            private void Cut(HouseCameraRig.Shot shot)
            {
                shot.Seconds = CutSeconds;
                director.cameraRig.MoveTo(shot);
            }

            /// <summary>The wide from across the set, looking at the screen over the chairs, as the house gathers.</summary>
            private HouseCameraRig.Shot Wide(float seconds) => new HouseCameraRig.Shot
            {
                Focus = new Vector3(roomCentre.x, 1.0f, roomCentre.z), Distance = 7f, Pitch = 22f, Yaw = Screen.LookYaw,
                FieldOfView = Lens, Seconds = seconds, DepthOfFieldWeight = 0f,
            };

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
                ClearCues();
                director.cameraRig.ReleaseShot(1.2f);
                director.cameraRig.ControlsEnabled = !director.IsPanelOpen;
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
                ended = true;
                ClearCues();
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
                if (director.cameraRig != null && begun)
                {
                    if (Step != CeremonyStageStep.Release) director.cameraRig.ReleaseShot(1.2f);
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
                        if (agent != null) { agent.updatePosition = playerUpdatePosition; agent.updateRotation = playerUpdateRotation; }
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
