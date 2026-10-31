using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Houseguests bring their asks to the player (OFFERS-COME-TO-YOU-PLAN, the owner, 2026-10-30: "when someone comes
    /// to you with a deal, you have to look through the menu to find it, but it should just pop up as dialogue like an
    /// RPG as an offer or have them approach you").
    ///
    /// <para><b>Three ways in, one frame.</b> A houseguest with an ask for the player walks over, says their line over
    /// their head and the dialogue opens by itself (the walk-over); a conversation the player opens with somebody who
    /// has an ask they have not seen this phase opens on it (raised); and a card where the player stands when the
    /// houseguest cannot come - busy or out of reach for a while, missed twice, the player's own spot fitting no
    /// visitor, or <see cref="AskWalkOvers"/> off (the card). The queue is <see cref="ComesToYou"/>, in its public
    /// order, one at a time.</para>
    ///
    /// <para><b>Presentation only.</b> Visits, hearing, the dialogue's opening, Think it over, Not right now, misses,
    /// retries, gaps and caps commit nothing, roll nothing and save nothing; the answers are the deal table's own
    /// controls. Like every panel the player opens, an opening flushes the NPC ticks that are due.</para>
    ///
    /// <para><b>Off where the audited behaviour must hold.</b> <see cref="AsksEnabled"/> false - every PlayMode fixture
    /// that binds a director, every PortVerification route, and by default every batch run - and the director runs
    /// the invitation as it always did (EpisodeDirector.NpcInvitations.cs), its invited view included.</para>
    ///
    /// <para>The owner's open questions are each one named choice here: <see cref="AskOpensByItself"/> (question 1),
    /// <see cref="AsksWaitWhileSeated"/> (question 4), <see cref="AskPullYieldSeconds"/> (question 5) and
    /// <see cref="AskClickHears"/> (the lead's call on a click on the asker).</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>How the ask the player is facing came to them.</summary>
        public enum AskWay { None, WalkOver, Raised, Card }

        // ---------------------------------------------------------------- the switch

        private bool AsksOn => AsksEnabled ?? !Application.isBatchMode;

        /// <summary>Whether houseguests bring their asks to the player now: <see cref="AsksEnabled"/>, or its default. A read for tests.</summary>
        public bool AsksAreOn => AsksOn;

        /// <summary>
        /// Whether a houseguest with an ask walks over to the player (the default). False: every ask comes as a card where
        /// the player stands, as one does whose asker cannot come (OFFERS-COME-TO-YOU-PLAN §B). Nothing saves it.
        /// </summary>
        public bool AskWalkOvers { get; set; } = true;

        // ---------------------------------------------------------------- the owner's choices

        /// <summary>
        /// Owner question 1: the dialogue opens by itself once the asker's line has been on screen for
        /// <see cref="AskSpeakBeatSeconds"/> and the player has stood still (the default). False: it waits for Hear, E or
        /// a click on the asker.
        /// </summary>
        private const bool AskOpensByItself = true;

        /// <summary>
        /// Owner question 4: while the player sits, lies, soaks or cooks, asks wait until they get up (the default). False
        /// lifts this gate only; the coordinator still refuses a visit to a player with an activity owner, and Hear would
        /// have to get the player up first, so coming over needs both of those too.
        /// </summary>
        private const bool AsksWaitWhileSeated = true;

        /// <summary>
        /// The lead's call (critique disposition 23): a click on the asker while their line is up hears the ask, the way E
        /// does. False: the click takes its usual route, which opens the raised dialogue without Think it over.
        /// </summary>
        private const bool AskClickHears = true;

        // ---------------------------------------------------------------- the clocks (free-house seconds unless stated)

        /// <summary>Continuous free-house time before a new visit starts.</summary>
        public const float AskSettleSeconds = 2f;
        /// <summary>How long the player has stood still before a visit starts or the dialogue opens.</summary>
        public const float AskStillSeconds = 1f;
        /// <summary>Stillness is time spent within this radius of one spot, so it does not depend on the frame rate.</summary>
        public const float AskStillRadiusMetres = .1f;
        /// <summary>How long the asker's line is on screen before the dialogue opens by itself, in unscaled seconds.</summary>
        public const float AskSpeakBeatSeconds = 1.2f;
        /// <summary>The gap after a presentation, a Think it over or a closed dialogue. The web build waits 30 s; ours also walks.</summary>
        public const float AskGapSeconds = 20f;
        /// <summary>After a miss: the ask's own retry, and the next scan.</summary>
        public const float AskRetrySeconds = 8f;
        /// <summary>The gap after a houseguest brought an ask over, before they bring another.</summary>
        public const float AskerGapSeconds = 120f;
        /// <summary>How often the queue is scanned for a visit to start.</summary>
        public const float AskScanSeconds = 1f;
        /// <summary>Misses before an ask stops being walked over this phase and comes as a card instead.</summary>
        public const int AskMissesBeforeCard = 2;
        /// <summary>
        /// Seconds of its asker's own refusals (busy, no route, not bound) before the same; and seconds the player's own
        /// spot has fitted no visitor before the first ask listed comes as a card.
        /// </summary>
        public const float AskUnreachableSecondsBeforeCard = 10f;
        /// <summary>Unasked presentations of one record in a session, of all records in a phase, and in a week.</summary>
        public const int AskPresentationsPerRecord = 2, AskPresentationsPerPhase = 3, AskPresentationsPerWeek = 6;
        /// <summary>The most one frame adds to any ask clock, unscaled: the cap a body's assembly bound uses.</summary>
        public const float AskFrameAllowanceSeconds = .25f;
        /// <summary>A frame longer than this, in unscaled seconds, is a stalled player loop: a lease it outlived is no miss.</summary>
        public const float AskStalledFrameSeconds = 1f;
        /// <summary>
        /// Owner question 5 (pending; D18 is the story session's): a Story Pull on screen holds new asks back for this many
        /// free, still seconds, then yields; the clock restarts when the Pull changes. If D18 stands as it is, make this
        /// <c>float.PositiveInfinity</c>: an ask's house gate is then the invitation's own, the Pull keeping the floor.
        /// </summary>
        public const float AskPullYieldSeconds = 20f;

        // The caps, copied so a test can lower them.
        private int askRecordCap = AskPresentationsPerRecord, askPhaseCap = AskPresentationsPerPhase, askWeekCap = AskPresentationsPerWeek;

        // ---------------------------------------------------------------- state (instance only: statics outlive play sessions)

        private bool asksWereOn;
        // The visit in flight.
        private ComesToYou.Ask askNow;
        private HouseNpcInvitationLease askVisit;
        private HouseMeetingCoordinator askCoordinator;
        private object askView;
        private long askLoad;
        private float askNextPresentation;
        private float askSpokeFor;
        private EpisodeState askCheckedSource;
        private bool askStillCurrent;
        // The ask a TryOpenNpc opens on, and the asker held out of pairing while it opens.
        private ComesToYou.Ask askOpenWith;
        private string askOpeningFor;
        // The free-house clock, its continuous run, stillness, and when the next scan may start a visit.
        private float askClock, askFreeRun, askStillFor, askNextAt, askScanAt;
        private Vector3 askStillAnchor = new Vector3(float.NaN, float.NaN, float.NaN);
        private bool askPlayerInFrame;
        // What the player has seen or put off this phase; per record this phase; per record and asker this session.
        private readonly HashSet<string> askSetAside = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> askMisses = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> askRetryAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> askUnreachable = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> askPresentations = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> askerNextAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private string askSession;
        private int askWeek, askPhaseShown, askWeekShown;
        private EpisodePhase askPhase;
        // The in-person dialogue on screen, and how it opened.
        private string askDialogueWith, askDialogueRecordId;
        private AskWay askDialogueWay;
        private bool askDialogueFirstRender;
        // The queue, read again whenever the installed state is another.
        private IReadOnlyList<ComesToYou.Ask> asksCache = Array.Empty<ComesToYou.Ask>();
        private EpisodeState asksCacheSource;
        // How long the Story Pull now up has held asks back (owner question 5).
        private float askPullHeld;
        private string askPullKey;
        // The card on screen: its ask, the reply once answered, and whether it has drawn yet. A panel of its own.
        private bool askCardOpen;
        private ComesToYou.Ask askCard;
        private ConversationExchange askCardReply;
        private bool askCardFirstRender;
        // Free, still seconds the player's own spot has fitted no visitor: one count for the spot, never per ask.
        private float askSpotBlockedFor;
        // The card's own settle: free-house seconds with no closable chrome up. The overview lets the free-house clock
        // run beneath it, so new visits may start there, but the card waits the settle time once it has closed.
        private float askCardSettle;

        // ---------------------------------------------------------------- public reads, for tests

        /// <summary>Whether an ask's card is up where the player stands.</summary>
        public bool IsAskCardOpen => askCardOpen;
        /// <summary>How many times this ask's walk-over has missed this phase.</summary>
        public int AskMissCount(string recordId) => recordId != null && askMisses.TryGetValue(recordId, out int count) ? count : 0;
        /// <summary>Whether the player has seen or put off this ask this phase.</summary>
        public bool IsAskSetAside(string recordId) => recordId != null && askSetAside.Contains(recordId);
        /// <summary>
        /// The ask the player is facing: the card's, else the dialogue's, else the visit's in flight. Hearing the ask lets
        /// the visit go before the conversation opens on it, so the dialogue's is read before the visit's.
        /// </summary>
        public string CurrentAskRecordId => askCardOpen ? askCard?.recordId : askDialogueWith != null ? askDialogueRecordId : askNow?.recordId;
        /// <summary>Who is asking: the card's houseguest, else the dialogue's, else the visitor's.</summary>
        public string CurrentAskerId => askCardOpen ? askCard?.askerId : askDialogueWith ?? askNow?.askerId;
        /// <summary>How the ask on screen came: the card, else how the dialogue opened; None for a visit still on its way, or nothing.</summary>
        public AskWay CurrentAskWay => askCardOpen ? AskWay.Card : askDialogueWith != null ? askDialogueWay : AskWay.None;

        // ---------------------------------------------------------------- gates

        /// <summary>
        /// The house free for an ask: the invitation's own gate with the Story Pull's term replaced by a bounded yield
        /// (<see cref="AskPullYieldSeconds"/>), and the player's furniture by <see cref="AsksWaitWhileSeated"/>.
        /// </summary>
        private bool AskHouseAvailable => VisitHouseFree(false, AsksWaitWhileSeated)
            && !(hud != null && hud.PullShowing && askPullHeld < AskPullYieldSeconds);

        /// <summary>Nothing of the player's own up: the emote menu, a chip's card, a storm-off, an embrace or listening in on offer.</summary>
        private bool AskMayHold => AskHouseAvailable && !emoteMenuOpen && castMenuFor == null
            && StormingId == null && CurrentEmbrace == null && !NearbyOfferUp;

        /// <summary>The clocks and the scan run, so a new visit may start: no scene waiting to stage, the player in frame.</summary>
        private bool AskMayStart => AskMayHold && !SceneStageWaiting && askPlayerInFrame;

        /// <summary>The visit in flight may open its dialogue: no closable chrome up, the player still. A waiting scene does not hold it.</summary>
        private bool AskMayOpen => AskMayHold && !ClosableChromeUp && askStillFor >= AskStillSeconds;

        /// <summary>
        /// The card may open: a new visit could start and an in-person dialogue could open, and no closable chrome has
        /// been up for the settle time, so the card never pops up the moment the overview closes.
        /// </summary>
        private bool AskCardMayOpen => AskMayStart && AskMayOpen && askCardSettle >= AskSettleSeconds;

        /// <summary>A story scene that could stage now: the beat TickSceneStage wants, not yet tried, and a house that could take it.</summary>
        private bool SceneStageWaiting
        {
            get
            {
                if (npcMeetings == null || projected == null) return false;
                StagedRoom(projected, out _);
                var beat = stagedBeat;
                bool freeTime = projected.phase == EpisodePhase.Social || projected.phase == EpisodePhase.Campaign;
                bool wanted = beat != null && freeTime && !declinedPulls.Contains(beat.id);
                return wanted && sceneStagedBeat != beat.id && !npcMeetings.HasSceneStage && npcMeetings.CanBeginSceneStage;
            }
        }

        /// <summary>A houseguest on their way to the player or up in front of them: the invitation's or an ask's.</summary>
        private bool NpcVisitInFlight => npcInvitationNow != null || askNow != null;

        /// <summary>The player's head on the view camera's screen. Worked out once a frame.</summary>
        private bool AskPlayerInFrameNow()
        {
            var view = cameraRig != null ? cameraRig.ViewCamera : null;
            if (view == null || player == null) return false;
            var point = view.WorldToViewportPoint(player.transform.position + Vector3.up * 1.7f);
            return point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
        }

        private float AskFrameTime => ComesToYouStillness.FrameTime(Time.unscaledDeltaTime);

        // ---------------------------------------------------------------- the tick

        /// <summary>
        /// Every frame the director is ready: the asks with <see cref="AsksEnabled"/> on, the invitation as it always ran
        /// with it off. A change of the switch lets any visit go and starts both afresh.
        /// </summary>
        private void TickAsks()
        {
            bool on = AsksOn;
            if (on != asksWereOn)
            {
                // A card up goes the way every panel goes, so the player's input and the camera come back with it.
                if (askCardOpen) ClosePanels();
                CancelNpcInvitation();
                ResetNpcInvitationsForLoad();
                ResetAsksForLoad();
                asksWereOn = on;
            }
            if (!on) { TickNpcInvitations(); return; }

            // A new session, week or phase: nothing is set aside, missed or held off any more.
            if (projected != null && (askSession != projected.sessionId || askWeek != projected.week || askPhase != projected.phase))
            {
                bool newSession = askSession != projected.sessionId, newWeek = newSession || askWeek != projected.week;
                CancelAskVisit();
                askSetAside.Clear(); askMisses.Clear(); askRetryAt.Clear(); askUnreachable.Clear();
                if (newSession) { askPresentations.Clear(); askerNextAt.Clear(); }
                askSession = projected.sessionId; askWeek = projected.week; askPhase = projected.phase;
                askPhaseShown = 0;
                if (newWeek) askWeekShown = 0;
            }

            float frame = AskFrameTime;
            // The player's spot is counted from where they stand still: it starts again with the stillness anchor.
            if (player == null || !ComesToYouStillness.Holds(askStillAnchor, player.transform.position, player.HasArrived))
                askSpotBlockedFor = 0f;
            askStillFor = player == null ? 0f
                : ComesToYouStillness.Step(ref askStillAnchor, askStillFor, player.transform.position, player.HasArrived, Time.unscaledDeltaTime);
            askPlayerInFrame = AskPlayerInFrameNow();

            // The Pull's yield clock (owner question 5), then the free-house clock. Only another Pull starts it again:
            // the same Pull hides under every panel and behind the ask's own line, and comes back as it was.
            string pullKey = pullNow?.Key;
            if (pullKey != null && pullKey != askPullKey) { askPullKey = pullKey; askPullHeld = 0f; }
            if (hud != null && hud.PullShowing && askStillFor >= AskStillSeconds && VisitHouseFree(false, AsksWaitWhileSeated))
                askPullHeld += frame;
            bool mayStart = AskMayStart;
            if (mayStart) { askClock += frame; askFreeRun += frame; }
            else askFreeRun = 0f;
            if (mayStart && !ClosableChromeUp) askCardSettle += frame;
            else askCardSettle = 0f;

            // The dialogue closed, or the conversation is with somebody else: the next ask waits its gap.
            if (askDialogueWith != null && (focusedNpc == null || focusedNpc.Id != askDialogueWith))
            { askNextAt = askClock + AskGapSeconds; ClearAskDialogue(); }

            // A panel, a stage, a walk, the player out of frame: the visit goes, as no miss. The camera is never moved
            // for an ask; the scan's own frame gate waits the settle time again once the player is back.
            if (!AskHouseAvailable || (askNow != null && !askPlayerInFrame)) { CancelAskVisit(); return; }

            if (askNow != null) { TickAskVisit(frame); return; }
            ScanAsks();
        }

        /// <summary>The visit in flight: immediate cancellations, the line's clock every frame, then a 10 Hz step.</summary>
        private void TickAskVisit(float frame)
        {
            if (askLoad != loadGeneration || !AskCurrentInInstalledState() || !ReferenceEquals(askCoordinator, npcMeetings))
            { CancelAskVisit(); return; }
            if (askVisit == null || askVisit.Released) { AskVisitLost(); return; }
            // Every frame, never per step: one frame's delta per 10 Hz step would stretch the beat many times over.
            if (NpcInvitationIsVisible) askSpokeFor += frame;
            if (Time.unscaledTime < askNextPresentation) return;
            askNextPresentation = Time.unscaledTime + .1f;
            if (!npcMeetings.NpcInvitationValid(askVisit)) { AskVisitLost(); return; }
            if (npcMeetings.NpcInvitationCanSpeak(askVisit, out var anchor))
            {
                if (npcInvitationBubble == null) npcInvitationBubble = gameObject.AddComponent<HouseNpcInvitationBubble>();
                object view = askView; long generation = askLoad;
                var ask = askNow;
                npcInvitationBubble.Show(projected.Find(ask.askerId).name, ask.line, TextScale, anchor,
                    () => HearAsk(view, generation, ask), () => DeferAsk(view, generation, ask), view);
            }
            else
            {
                npcInvitationBubble?.Hide();
                askSpokeFor = 0f;
            }
            if (AskOpensByItself && askSpokeFor >= AskSpeakBeatSeconds && AskMayOpen) HearAsk(askView, askLoad, askNow);
        }

        /// <summary>
        /// The lease went: a miss, retried after <see cref="AskRetrySeconds"/>. Not a miss when the dialogue would have
        /// opened by itself, the player still and no closable chrome of theirs up - a gate the player did not raise
        /// (Nearby, a storm-off, an embrace) held it until the lease ran out - or when the frame was a stalled player
        /// loop. A line left unanswered under the overview, or with <see cref="AskOpensByItself"/> false, is a miss, so
        /// after <see cref="AskMissesBeforeCard"/> of them the ask comes as a card instead.
        /// </summary>
        private void AskVisitLost()
        {
            var ask = askNow;
            if (ask == null) { CancelAskVisit(); return; }
            bool openWasDue = AskOpensByItself && !ClosableChromeUp
                && askSpokeFor >= AskSpeakBeatSeconds && askStillFor >= AskStillSeconds;
            if (openWasDue || Time.unscaledDeltaTime > AskStalledFrameSeconds)
            {
                askRetryAt[ask.recordId] = askNextAt = askClock + AskRetrySeconds;
                CancelAskVisit();
                return;
            }
            RecordAskMiss(ask.recordId);
        }

        /// <summary>
        /// Starts the first ask in public order that can come now: one visit at a time, each asker charged only for
        /// refusals of their own. A refusal every asker would meet - the house, the player, the player's spot - ends
        /// the scan and charges nobody. An ask whose asker cannot come opens as a card where the player stands.
        /// </summary>
        private void ScanAsks()
        {
            if (!AskMayStart || askFreeRun < AskSettleSeconds || askStillFor < AskStillSeconds
                || askClock < askNextAt || askClock < askScanAt || AskCapReached || player == null) return;
            askScanAt = askClock + AskScanSeconds;
            // The first ask this scan tried to bring: the one listed first that can still come, for the spot's card.
            ComesToYou.Ask firstTried = null;
            foreach (var ask in CurrentAsks())
            {
                if (!AskMayCome(ask)) continue;
                // Missed twice, out of its asker's own reach for long enough, or walk-overs off: it comes as a card, at
                // the first free, still moment with no closable chrome up for the settle time. It keeps its place in the
                // order meanwhile: nothing after it is walked over first.
                if (AskCardDue(ask.recordId))
                {
                    if (AskCardMayOpen) OpenAskCard(ask);
                    return;
                }
                if (firstTried == null) firstTried = ask;
                if (npcMeetings.TryReserveNpcInvitation(ask.askerId, player, out var visit, out _, out var refusal))
                {
                    askNow = ask; askVisit = visit; askCoordinator = npcMeetings;
                    askLoad = loadGeneration; askView = new object();
                    askNextPresentation = 0f; askSpokeFor = 0f; askCheckedSource = null;
                    return;
                }
                if (refusal == HouseNpcInvitationRefusal.Busy || refusal == HouseNpcInvitationRefusal.NoRoute
                    || refusal == HouseNpcInvitationRefusal.Asker)
                {
                    askUnreachable[ask.recordId] = AskUnreachableFor(ask.recordId) + AskScanSeconds;
                    // Due on this very scan: it comes as a card now, in its place, before anything listed after it.
                    if (AskCardDue(ask.recordId))
                    {
                        if (AskCardMayOpen) OpenAskCard(ask);
                        return;
                    }
                    continue;
                }
                // The player's own spot fits no visitor: one count for the spot, charged to no ask. Once it has held for
                // the card's time, the ask listed first that can still come - the first this scan tried, even one whose
                // own asker is busy - comes as a card; no ask is made due by it.
                if (refusal == HouseNpcInvitationRefusal.PlayerSpot)
                {
                    askSpotBlockedFor += AskScanSeconds;
                    if (askSpotBlockedFor >= AskUnreachableSecondsBeforeCard && AskCardMayOpen) OpenAskCard(firstTried ?? ask);
                }
                return;
            }
        }

        /// <summary>The queue in public order, read again whenever the installed state is another.</summary>
        private IReadOnlyList<ComesToYou.Ask> CurrentAsks()
        {
            if (!ReferenceEquals(asksCacheSource, projected))
            {
                asksCacheSource = projected;
                asksCache = projected != null ? ComesToYou.Read(projected) : Array.Empty<ComesToYou.Ask>();
            }
            return asksCache;
        }

        private bool AskCurrentInInstalledState()
        {
            if (!ReferenceEquals(askCheckedSource, projected))
            {
                askCheckedSource = projected;
                askStillCurrent = askNow != null && askNow.IsCurrent(projected)
                    && NpcInvitationConversationAvailable(projected, askNow.askerId);
            }
            return askStillCurrent;
        }

        /// <summary>Whether an ask can be walked over now: current, not set aside, under its cap, past its retry and its asker's gap, and the conversation can show it.</summary>
        private bool AskMayCome(ComesToYou.Ask ask) => ask != null && ask.IsCurrent(projected) && !askSetAside.Contains(ask.recordId)
            && AskPresentationsOf(ask.recordId) < askRecordCap
            && askClock >= (askRetryAt.TryGetValue(ask.recordId, out float retry) ? retry : 0f)
            && askClock >= (askerNextAt.TryGetValue(ask.askerId, out float asker) ? asker : 0f)
            && NpcInvitationConversationAvailable(projected, ask.askerId) && AskOpensInPerson(ask);

        /// <summary>
        /// Whether an ask could still come to the player this phase, for the "N more" line: walked over, or as a card
        /// when its asker cannot come.
        /// </summary>
        private bool AskCanStillCome(ComesToYou.Ask ask) => ask != null && ask.IsCurrent(projected)
            && !askSetAside.Contains(ask.recordId) && AskPresentationsOf(ask.recordId) < askRecordCap
            && NpcInvitationConversationAvailable(projected, ask.askerId) && AskOpensInPerson(ask);

        /// <summary>Whether an ask comes as a card rather than walked over: missed twice, its asker out of reach long enough, or walk-overs off.</summary>
        private bool AskCardDue(string recordId) => !AskWalkOvers || AskMissCount(recordId) >= AskMissesBeforeCard
            || AskUnreachableFor(recordId) >= AskUnreachableSecondsBeforeCard;

        private float AskUnreachableFor(string recordId) => askUnreachable.TryGetValue(recordId, out float seconds) ? seconds : 0f;

        private int AskPresentationsOf(string recordId) => askPresentations.TryGetValue(recordId, out int count) ? count : 0;

        private bool AskCapReached => askPhaseShown >= askPhaseCap || askWeekShown >= askWeekCap;

        /// <summary>One more unasked presentation: of the record this session, and toward the phase's and the week's caps.</summary>
        private void CountAskPresentation(ComesToYou.Ask ask)
        {
            askPresentations[ask.recordId] = AskPresentationsOf(ask.recordId) + 1;
            askPhaseShown++; askWeekShown++;
        }

        /// <summary>A walk-over that missed: counted, retried after <see cref="AskRetrySeconds"/>, never set aside.</summary>
        private void RecordAskMiss(string recordId)
        {
            askMisses[recordId] = AskMissCount(recordId) + 1;
            askRetryAt[recordId] = askNextAt = askClock + AskRetrySeconds;
            CancelAskVisit();
        }

        // ---------------------------------------------------------------- the bubble's answers, E and the click

        private bool AskCallbackCurrent(object view, long generation, ComesToYou.Ask ask) =>
            AsksOn && view != null && ReferenceEquals(view, askView) && ask != null && ReferenceEquals(ask, askNow)
            && generation == loadGeneration && AskHouseAvailable && ask.IsCurrent(projected)
            && NpcInvitationConversationAvailable(projected, ask.askerId)
            && askCoordinator != null && askCoordinator.NpcInvitationCanSpeak(askVisit, out _);

        /// <summary>
        /// Hear their offer, E, a click on the asker, or the dialogue opening by itself: the visit goes and the
        /// conversation opens on the ask (<see cref="OpenOnAsk"/>), the asker held out of pairing across the open. A
        /// conversation that cannot open is a miss.
        /// </summary>
        private void HearAsk(object view, long generation, ComesToYou.Ask ask)
        {
            if (!AskCallbackCurrent(view, generation, ask)) return;
            bool opened;
            askOpenWith = ask; askOpeningFor = ask.askerId;
            try
            {
                CancelAskVisit();
                opened = TryOpenNpc(ask.askerId);
            }
            finally { askOpenWith = null; askOpeningFor = null; }
            if (!opened) RecordAskMiss(ask.recordId);
        }

        /// <summary>Not right now, or Escape on the line: put off for the phase, counted, and the next ask waits its gap.</summary>
        private void DeferAsk(object view, long generation, ComesToYou.Ask ask)
        {
            if (!AskCallbackCurrent(view, generation, ask)) return;
            askSetAside.Add(ask.recordId);
            CountAskPresentation(ask);
            askNextAt = askClock + AskGapSeconds;
            CancelAskVisit();
        }

        private bool TryHearVisibleAsk()
        {
            if (!NpcInvitationIsVisible || !AskCallbackCurrent(askView, askLoad, askNow)) return false;
            HearAsk(askView, askLoad, askNow);
            return true;
        }

        private bool TryDeferVisibleAsk()
        {
            if (!NpcInvitationIsVisible || !AskCallbackCurrent(askView, askLoad, askNow)) return false;
            DeferAsk(askView, askLoad, askNow);
            return true;
        }

        private HouseNpc VisibleAskSpeaker()
        {
            if (!NpcInvitationIsVisible || askNow == null || askLoad != loadGeneration
                || !AskHouseAvailable || !AskCurrentInInstalledState()) return null;
            string id = askNow.askerId;
            return housemates.FirstOrDefault(npc => npc != null && npc.Id == id && npc.gameObject.activeInHierarchy);
        }

        // ---------------------------------------------------------------- opening on the ask

        /// <summary>
        /// Called by TryOpenNpc once the conversation is set up, before its render. The ask the houseguest walked over
        /// with (a walk-over); otherwise the first of theirs the player has not seen this phase, where the conversation
        /// can show it (raised) - not when the player came from the houseguest's screen to ask or pitch, whose page
        /// already shows the offer first, and not over a beat this conversation is. Either is then set aside for the
        /// phase. Nothing with asks off.
        /// </summary>
        private void OpenOnAsk(string npcId)
        {
            if (!AsksOn || projected == null || npcId == null) return;
            // A dialogue left over from the last conversation goes, its gap with it.
            if (askDialogueWith != null) { askNextAt = askClock + AskGapSeconds; ClearAskDialogue(); }
            ComesToYou.Ask ask = null;
            bool walkOver = false;
            if (askOpenWith != null && askOpenWith.askerId == npcId && askOpenWith.IsCurrent(projected) && AskOpensInPerson(askOpenWith))
            { ask = askOpenWith; walkOver = true; }
            else if (conversationIntent == null && NpcInvitationConversationAvailable(projected, npcId) && !HasConversationBeat(projected, npcId))
                ask = CurrentAsks().FirstOrDefault(row => row.askerId == npcId && row.IsCurrent(projected)
                    && !askSetAside.Contains(row.recordId) && AskOpensInPerson(row));
            if (ask == null) return;
            var person = projected.Find(npcId);
            if (person == null) return;
            PrepareConversationNavigation(projected, person);
            conversationCategory = DealsCategoryCaption; invitedDealId = ask.recordId;
            askDialogueWith = npcId; askDialogueRecordId = ask.recordId;
            askDialogueWay = walkOver ? AskWay.WalkOver : AskWay.Raised;
            askDialogueFirstRender = true;
            askSetAside.Add(ask.recordId);
            if (walkOver)
            {
                CountAskPresentation(ask);
                askerNextAt[npcId] = askClock + AskerGapSeconds;
            }
        }

        /// <summary>Think it over: nothing answered, the conversation closes, and the next ask waits its gap.</summary>
        private void ThinkItOver(string recordId)
        {
            if (recordId == null || recordId != askDialogueRecordId) return;
            askNextAt = askClock + AskGapSeconds;
            hud.DropFocusAsked();
            ClearAskDialogue();
            ClosePanels();
        }

        /// <summary>Back to topics, from the control or Escape, Start or B: out of the dialogue, into the conversation.</summary>
        private void LeaveAskDialogue()
        {
            askNextAt = askClock + AskGapSeconds;
            ClearAskDialogue();
            if (hud != null) { hud.DropFocusAsked(); hud.EndPointerHold(); }
        }

        private void ClearAskDialogue()
        {
            askDialogueWith = null; askDialogueRecordId = null; askDialogueWay = AskWay.None; askDialogueFirstRender = false;
        }

        /// <summary>
        /// The dialogue in the conversation: the frame, the deal table's own answers with their readings, Think it over
        /// for a walk-over, Back to topics, and who else wants a word. The keyboard opens on the way out that answers
        /// nothing, and pointer presses are held off the panel a moment, so a press already on its way commits nothing.
        /// False, with the dialogue let go, once the offer is no longer waiting: the Deals page draws as it always has.
        /// </summary>
        private bool RenderAskDialogue(EpisodeState state, ContestantState npc, object token)
        {
            var offer = NpcDeals.Pending(state).FirstOrDefault(d => d.proposerId == npc.id && d.id == invitedDealId);
            if (offer == null)
            {
                invitedDealId = null; askDialogueRecordId = null; askDialogueWay = AskWay.None;
                hud.DropFocusAsked(); hud.EndPointerHold();
                return false;
            }
            bool walkOver = askDialogueWay == AskWay.WalkOver;
            string first = FinalistRead.FirstName(npc.name);
            var ask = AskFrame(state, npc, offer, walkOver ? EpisodeHud.AskWalkOverEyebrow(first) : EpisodeHud.AskRaisedEyebrow(first), true);
            AskBody(state, ask, offer);
            string id = offer.id;
            if (walkOver)
                hud.Action(EpisodeHud.ThinkItOverCaption, () =>
                {
                    if (ConversationNavigationCurrent(token, state, npc.id)) ThinkItOver(id);
                });
            hud.Action(ConversationTopicsBackCaption, () =>
            {
                if (ConversationNavigationCurrent(token, state, npc.id)) BackConversationNavigation(true);
            });
            hud.AskMoreWaiting(AskCapReached ? 0 : ComesToYou.OtherAskers(CurrentAsks(), npc.id, AskCanStillCome));
            if (askDialogueFirstRender)
            {
                askDialogueFirstRender = false;
                hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
                hud.FocusWhenWired(walkOver ? EpisodeHud.ThinkItOverCaption : ConversationTopicsBackCaption);
            }
            else
            {
                hud.KeepPointerHold();
                hud.KeepFocusAsked();
            }
            if (cameraRig != null) cameraRig.ConversationWindowOffset = hud.ConversationWindowOffset;
            return true;
        }

        /// <summary>
        /// An ask's frame, in the conversation or on the card: the eyebrow, the asker, their standing and mood, their
        /// line, the terms and how long the offer stands. The line and the lapse line are the queue's; an offer the
        /// queue no longer lists keeps the invitation's line and says nothing of its lapse. The queue's row, or null.
        /// </summary>
        private ComesToYou.Ask AskFrame(EpisodeState state, ContestantState npc, DealState offer, string eyebrow, bool inConversation)
        {
            var ask = ComesToYou.Find(state, offer.id);
            string line = ask != null ? ask.line
                : NpcInvitation.Available(state).FirstOrDefault(row => row.RecordId == offer.id && row.NpcId == npc.id)?.OpeningLine;
            string mood = npc.mood != "Neutral" ? MoodLine(state, npc) : null;
            string standing = Standing(state, npc.id) + (mood != null ? " · " + mood.Substring("Mood: ".Length) : "");
            hud.AskDialogue(npc.id, eyebrow, npc.pronouns, standing, line, ask != null ? ask.whisper : ComesToYou.Whispered(offer.type),
                DealSentence(state, offer), ask?.lapseLine, inConversation);
            return ask;
        }

        // ---------------------------------------------------------------- the card (when they cannot come)

        /// <summary>
        /// The ask as a card where the player stands, when its asker cannot come over. Like the scene card, the house's
        /// due time is flushed and the world paused under it, and the player's input and the camera's controls are off.
        /// It is a presentation, and the ask is set aside for the phase as it opens. False when the ask has gone by the
        /// time the house's time is flushed.
        /// </summary>
        private bool OpenAskCard(ComesToYou.Ask ask)
        {
            if (ask == null) return false;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            // The flush may have committed the house's time: the ask must still be the one waiting.
            if (projected == null || !ask.IsCurrent(projected)) return false;
            ClosePanels();
            askCardOpen = true; askCard = ask; askCardReply = null; askCardFirstRender = true;
            askSetAside.Add(ask.recordId);
            CountAskPresentation(ask);
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            Render();
            return true;
        }

        /// <summary>
        /// The card's page, on the Stage layout, which frames no two-shot. Under "A WORD IN PRIVATE": the dialogue, the
        /// deal table's own answers with their readings, Think it over and who else wants a word. Once answered, the
        /// player's words and the reply; once the offer has gone, a line saying so; either way, Back to the house.
        /// The keyboard opens on Think it over and pointer presses are held off a moment, so a press already on its way
        /// answers nothing.
        /// </summary>
        private void AskCard(EpisodeState state)
        {
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Stage);
            var ask = askCard;
            var offer = ask == null ? null
                : NpcDeals.Pending(state).FirstOrDefault(d => d.id == ask.recordId && d.proposerId == ask.askerId);
            var npc = ask != null ? state.Find(ask.askerId) : null;
            if (askCardReply != null || offer == null || npc == null)
            {
                // Nothing left to answer, so nothing to hold the keyboard or the pointer off: the way back is the page's control.
                hud.DropFocusAsked(); hud.EndPointerHold();
                if (askCardReply != null) hud.AskReply(askCardReply.NpcId, askCardReply.PlayerLine, askCardReply.NpcLine);
                else hud.Paragraph(EpisodeHud.AskGoneLine);
                hud.Action(SceneCardDoneCaption, ClosePanels);
                return;
            }
            var shown = AskFrame(state, npc, offer, EpisodeHud.AskDirectEyebrow, false);
            AskBody(state, shown ?? ask, offer);
            hud.Action(EpisodeHud.ThinkItOverCaption, ClosePanels);
            hud.AskMoreWaiting(AskCapReached ? 0 : ComesToYou.OtherAskers(CurrentAsks(), npc.id, AskCanStillCome));
            if (askCardFirstRender)
            {
                askCardFirstRender = false;
                hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
                hud.FocusWhenWired(EpisodeHud.ThinkItOverCaption);
            }
            else
            {
                hud.KeepPointerHold();
                hud.KeepFocusAsked();
            }
        }

        /// <summary>
        /// The card goes, as every panel does (ClosePanelsInternal): Think it over, Escape, Back to the house. An ask still
        /// waiting unanswered is set aside for the phase, and after any card the next ask waits its gap.
        /// </summary>
        private void EndAskCard()
        {
            if (!askCardOpen) return;
            var ask = askCard;
            if (ask != null)
            {
                if (askCardReply == null && projected != null && ask.IsCurrent(projected)) askSetAside.Add(ask.recordId);
                askNextAt = askClock + AskGapSeconds;
            }
            askCardOpen = false; askCard = null; askCardReply = null; askCardFirstRender = false;
        }

        /// <summary>
        /// An answer on the card, once it is saved: what the conversation would have played - the player's words and the
        /// asker's reply - kept for the card's reply page when the asker is the one answered. Nothing is narrated, so
        /// nothing reaches the next conversation.
        /// </summary>
        private void CaptureAskCardReply(EpisodeState before, CommandResult result, EpisodeCommand command)
        {
            var ask = askCard;
            if (ask == null || before == null || result?.state == null) return;
            var state = result.state;
            var visible = state.events.Where(e => e.sequence >= before.nextSequence
                && (result.beatsFromSequence <= 0 || e.sequence < result.beatsFromSequence)
                && (e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                && !KnownBallots.TellsAnUnknownBallot(state, e)).ToList();
            var exchange = ConversationExchange.Create(before, state, command, visible);
            if (exchange != null && exchange.NpcId == ask.askerId) askCardReply = exchange;
        }

        // ---------------------------------------------------------------- the sources (plan §C.4)

        /// <summary>Whether an ask opens on the in-person dialogue through <see cref="OpenOnAsk"/>: an offer and a veto ask do.</summary>
        private static bool AskOpensInPerson(ComesToYou.Ask ask)
        {
            if (ask == null) return false;
            switch (ask.source)
            {
                case ComesToYou.Source.Offer:
                case ComesToYou.Source.VetoAsk:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The bubble's two captions for an ask's source, each pair its own and none of the Pull's. The deal sources keep
        /// the invitation's; the bubble takes others from slice 4, which adds the overload that relabels it.
        /// </summary>
        private static (string hear, string notNow) AskBubbleCaptions(ComesToYou.Source source)
        {
            switch (source)
            {
                default:
                    return (HouseNpcInvitationBubble.HearCaption, HouseNpcInvitationBubble.NotNowCaption);
            }
        }

        /// <summary>
        /// The answers an ask's source draws under the frame: for an offer or a veto ask, the deal table's own Accept and
        /// Turn down (free), which register as the offer's controls, each with what a screen reader says for it.
        /// </summary>
        private void AskBody(EpisodeState state, ComesToYou.Ask ask, DealState offer)
        {
            switch (ask != null ? ask.source : ComesToYou.Source.Offer)
            {
                default:
                    string name = state.Find(offer.proposerId)?.name ?? "";
                    string terms = DealSentence(state, offer);
                    var accept = OfferAccept(state, offer);
                    if (accept.interactable) hud.Tag(accept, FreeTag);
                    hud.ReadAskControl(accept, EpisodeHud.AskReading(EpisodeHud.DealAcceptCaption, name, terms));
                    var decline = OfferDecline(state, offer);
                    hud.Tag(decline, FreeTag);
                    hud.ReadAskControl(decline, EpisodeHud.AskReading(EpisodeHud.DealDeclineCaption, name, terms));
                    break;
            }
        }

        // ---------------------------------------------------------------- letting go

        /// <summary>Lets the visit in flight go: its line hidden, its lease released. Never a miss on its own.</summary>
        private void CancelAskVisit()
        {
            askSpokeFor = 0f;
            if (askNow == null && askVisit == null && askCoordinator == null) return;
            askView = null;
            askCheckedSource = null;
            npcInvitationBubble?.Hide();
            askCoordinator?.ReleaseNpcInvitation(askVisit);
            askNow = null; askVisit = null; askCoordinator = null;
        }

        /// <summary>A load, or the switch turned: every ask clock, count and set starts again. Nothing is saved, so a reload presents waiting asks again.</summary>
        private void ResetAsksForLoad()
        {
            CancelAskVisit();
            askSetAside.Clear(); askMisses.Clear(); askRetryAt.Clear(); askUnreachable.Clear();
            askPresentations.Clear(); askerNextAt.Clear();
            askClock = 0f; askFreeRun = 0f; askStillFor = 0f; askNextAt = 0f; askScanAt = 0f;
            askStillAnchor = new Vector3(float.NaN, float.NaN, float.NaN);
            askSession = null; askPhaseShown = 0; askWeekShown = 0;
            ClearAskDialogue();
            asksCache = Array.Empty<ComesToYou.Ask>(); asksCacheSource = null;
            askPullHeld = 0f; askPullKey = null;
            askSpotBlockedFor = 0f; askCardSettle = 0f;
            // A card up is the old season's: what it asked goes now, so closing it sets nothing aside and starts no gap.
            // The panel itself closes with the panels (the load's, or the switch's).
            askCard = null; askCardReply = null;
        }
    }

    /// <summary>
    /// Whether the player is standing still, the same at any frame rate: the time spent within
    /// <see cref="EpisodeDirector.AskStillRadiusMetres"/> of one spot with their walk arrived, each frame adding its own
    /// unscaled time up to <see cref="EpisodeDirector.AskFrameAllowanceSeconds"/>, so a window out of focus credits no
    /// stall in one frame. Pure, so EditMode can drive it.
    /// </summary>
    public static class ComesToYouStillness
    {
        /// <summary>The time a frame adds to an ask's clock: its own, at most <see cref="EpisodeDirector.AskFrameAllowanceSeconds"/>.</summary>
        public static float FrameTime(float unscaledDeltaTime) =>
            Mathf.Clamp(unscaledDeltaTime, 0f, EpisodeDirector.AskFrameAllowanceSeconds);

        /// <summary>
        /// One frame of stillness: the seconds still so far, given where the player stands, whether their walk has
        /// arrived and the frame's unscaled time. Walking, or leaving the radius of <paramref name="anchor"/>, starts
        /// again from here (the anchor moves to the player and the time is zero).
        /// </summary>
        public static float Step(ref Vector3 anchor, float stillFor, Vector3 position, bool arrived, float unscaledDeltaTime)
        {
            if (!Holds(anchor, position, arrived))
            {
                anchor = position;
                return 0f;
            }
            return stillFor + FrameTime(unscaledDeltaTime);
        }

        /// <summary>
        /// Whether the player is still where they stood: their walk arrived and within
        /// <see cref="EpisodeDirector.AskStillRadiusMetres"/> of <paramref name="anchor"/>. False starts the stillness
        /// again, and with it the count of the player's spot (EpisodeDirector.ComesToYou.cs).
        /// </summary>
        public static bool Holds(Vector3 anchor, Vector3 position, bool arrived)
        {
            const float radius = EpisodeDirector.AskStillRadiusMetres;
            return arrived && (position - anchor).sqrMagnitude <= radius * radius;
        }
    }
}
