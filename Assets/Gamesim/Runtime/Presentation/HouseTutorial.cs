using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The first-run tour: the reference build's seven steps, each one dimming the house around the
    /// piece of this HUD it explains and lighting a ring round it.
    ///
    /// <para>This is the one overlay in the project that deliberately takes input. Every other card
    /// refuses a raycaster and dismisses on a timer, because a broadcast graphic that swallowed a
    /// click would be worse than no graphic. A tutorial is the opposite: it is a conversation, it
    /// has to wait, and a player must be able to leave it at any point.</para>
    ///
    /// <para>That makes it the one overlay that could strand an automated season, so it is never
    /// shown in batchmode. The director offers it at every season's opening (<see cref="Offer"/>):
    /// a first-time player gets the tour itself, as the reference build shows it in every new
    /// game's first week, and a player who has finished or skipped it before gets a card asking
    /// "Take the tour?" instead of the tour again. A player who arrives in a season that has no
    /// opening to play (an import, a migrated save) gets it once. <see cref="Show"/> is public so a
    /// test can drive and photograph it without pretending to be either.</para>
    ///
    /// <para>Its card docks beside the chrome each step lights, never over it (<see cref="TourDock"/>),
    /// and is as wide as its title needs to sit on one line.</para>
    ///
    /// <para><b>What is the reference build's, and what is not.</b> The order and the job of each
    /// step, the dim with its cut-out and lit ring, where the card stands beside what it points at,
    /// the timings, the captions and the sound on Next are its (<c>TutorialWalkthrough.tsx</c>).
    /// The words are not. Its side menu of Cameras, Deals and a Memory Wall down the right edge and
    /// its carousel of faces down the left do not exist here, and a tour that described them would
    /// be teaching a game this is not; each step names the piece of this HUD that does the same
    /// job, in words that are true of it. Its last card promised that the first Head of Household
    /// competition was about to begin, which is true of neither game - the introductions come next
    /// in the opening, and an imported season can be weeks in - so this one ends on the objective
    /// instead. The ring is the HUD's accent rather than the reference's yellow: gold is power in
    /// this house, not "look here".</para>
    ///
    /// <para><b>It is also the only change in this pass that could alter what a playtest measures.</b>
    /// Section E2 of the acceptance matrix asks whether three first-time players reach the eviction
    /// unaided, and a guided tour is precisely the intervention that criterion exists to test the
    /// absence of. Run E2 with the tour off, or record that it was on - and since it plays at every
    /// season's opening, as the reference's does, "off" means skipping it on sight (PLAYTEST_PROTOCOL.md).
    /// Do not run it both ways and
    /// report the better number. Record too whether "Skip Introductions" was used: the introductions
    /// are gameplay rather than an aid, and they move how the house feels about the player.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseTutorial : MonoBehaviour
    {
        public const string SeenKey = "Gamesim.TutorialSeen";

        /// <summary>The captions tests and screen readers find the tour's controls by. Never decorated: the chevron on Next is its own object.</summary>
        public const string NextCaption = "Next";
        public const string FinishCaption = "Let's Go!";
        public const string SkipCaption = "Skip the tour";
        /// <summary>
        /// The step before: on every step but the first, where there is none. "Previous", not a bare
        /// "Back" (menu slice 2): it is not a way out of the tour - "Skip the tour" is, and Escape with it.
        /// </summary>
        public const string PreviousCaption = FrontDoorWords.TourPrevious;

        /// <summary>
        /// How to see the tour again, on every card, so it is there at the moment of skipping. True
        /// because a new season offers it (<see cref="Offer"/>) and the house's Settings carry a
        /// "Replay the tour" control (EpisodeDirector.Season.cs).
        /// </summary>
        public const string ReplayNote = TourOfferWords.ReplayNote;

        /// <summary>The offer's answers, by caption: the contract tests and screen readers find them by.</summary>
        public const string TakeCaption = TourOfferWords.TakeCaption;
        public const string NotNowCaption = TourOfferWords.NotNowCaption;
        public const string ReplayNoteName = "Replay note";

        /// <summary>The names of the tour's parts, so a test can measure them without guessing.</summary>
        public const string StepLayerName = "Tour step";
        public const string CardName = "Tutorial card";
        public const string RingName = "Spotlight ring";
        public static readonly string[] DimNames = { "Dim top", "Dim bottom", "Dim left", "Dim right" };

        /// <summary>
        /// The movement keys and the way out of the tour, said once, on the card that is never
        /// dropped. Only what holds for the rest of the tour and after it: the opening's own keys
        /// used to be here, but the only beat left after the tour is the introductions, which
        /// ignore Space and have a "Skip Introductions" of their own, and a season offered the tour
        /// without an opening has none to press them in.
        /// </summary>
        public const string ControlsLine = InputGlossary.KeyboardTourLine;

        /// <summary>
        /// Whether the controls line speaks of the pad's buttons: the device last pressed on, as
        /// the ceremony cards follow it (PLAN A, A4). Each showing starts on the device the house
        /// was last pressed on (<see cref="LastDeviceWasPad"/>), and a press on the other kind
        /// rewords the step in place.
        /// </summary>
        public bool PadLine { get; private set; }

        /// <summary>
        /// Asked as each showing starts: whether the device the house was last pressed on is a pad.
        /// The director answers with its hints' device. The line lives on the tour's first card,
        /// which a pad's A leaves at once, so a tour that waited for a press while it was up kept
        /// the keyboard's line for a player who came through the menu and the opening on a pad.
        /// Unset, a showing starts on the keyboard's words.
        /// </summary>
        public Func<bool> LastDeviceWasPad { get; set; }

        /// <summary>
        /// The episode screen's step names the keys that open and close the screen (A4f): on a pad
        /// they are replaced as the controls line is.
        /// </summary>
        public const string StationStepKeys = InputGlossary.KeyboardStationStepKeys;

        /// <summary>A step's words, with the controls line and the episode screen's keys in the device's words.</summary>
        private string BodyFor(Step current) =>
            PadLine || PhoneDisplay.Active ? current.Body.Replace(ControlsLine, InputGlossary.TourLine(PadLine, PhoneDisplay.Active))
                .Replace(StationStepKeys, InputGlossary.StationStepKeys(PadLine, PhoneDisplay.Active))
                : current.Body;

        /// <summary>A press on the other kind of device rewords the step on screen, in place.</summary>
        private void FollowDevice()
        {
            var pad = Gamesim.House.HouseInput.PadUsed();
            if (!pad.HasValue || pad.Value == PadLine) return;
            PadLine = pad.Value;
            if (step < 0 || step >= visible.Count || body == null) return;
            body.text = Localisation.Text(BodyFor(visible[step]));
            dockDirty = true;
            Place();
        }

        /// <summary>How far the ring stands off the thing it lights, so its edge never sits on the panel's own.</summary>
        private const float HolePadding = 6f;
        /// <summary>The reference build's black/60.</summary>
        private const float DimAlpha = .6f;
        /// <summary>The reference build's step: a 0.3 s fade, and the card rising 20 px over 0.35 s after 0.15 s.</summary>
        private const float StepFadeSeconds = .3f;
        private const float CardDelaySeconds = .15f;
        private const float CardRiseSeconds = .35f;
        private const float RiseDistance = 20f;
        /// <summary>How often a step whose chrome went away mid-step looks for it again: the lookup walks the whole scene.</summary>
        private const float RelookSeconds = .25f;

        /// <summary>
        /// One step: its job in the reference build's order, what it says, and the pieces of chrome
        /// it lights. No anchors is a centred card over a plain dim, as the reference draws its first
        /// and last steps; more than one lights the rectangle that holds them all.
        /// </summary>
        private readonly struct Step
        {
            public readonly string Id, Title, Body;
            public readonly string[] Anchors;

            public Step(string id, string title, string body, params string[] anchors)
            {
                Id = id; Title = title; Body = body;
                Anchors = anchors ?? Array.Empty<string>();
            }
        }

        /// <summary>
        /// The reference build's seven steps in its order - welcome, the status bar, the side menu,
        /// the game-phase button, the character carousel, the phase overlays, you're ready - each
        /// pointed at the piece of this HUD that does that job.
        ///
        /// <para>The status bar is the top bar's week chip and house pill: the objective chip
        /// between them moves to the left column in the compact HUD, so it is not promised here but
        /// on the last card, which is true in both. The side menu is the left rail. The game-phase
        /// button is "Go to episode screen", the rail's (or the compact objective card's) way to the
        /// screen the week's decisions are made at. The carousel is the cast strip along the bottom.
        /// The phase step points at the open decision panel when one exists, or at the Current
        /// Objective when it is closed. Status is a report of what happened, not the next decision.</para>
        /// </summary>
        private static readonly Step[] Steps =
        {
            new Step("welcome", "Welcome to Gamesim: The House!",
                "Let's take a quick tour so you know how everything works. " + ControlsLine),
            new Step("topbar", "Top Bar",
                "The week and where the house is in it, how many houseguests are left, the actions you have left this week, and who is Head of Household.",
                "Week chip", "House pill"),
            new Step("sidemenu", "Side Rail",
                "The Overview of the whole house, then your Notebook: your notes, Deals & promises, Houseguests, Relationships, Who is where, The vote and The story so far. Save and Settings are at its foot. After a change, the save feedback tells you when it was saved or if saving failed.",
                "Rail ground"),
            new Step("gamephase", "The Episode Screen",
                "Nominations, votes and the week's other decisions happen at the living-room screen. This button walks you there; " + StationStepKeys,
                "Go to episode screen"),
            new Step("carousel", "Cast Strip",
                "Everyone in the house, along the bottom. Click someone's face to open their menu: talk, profile, deals or follow. Under each name is their mood; once you know them, a tag says where you stand.",
                CastRail.StripName),
            new Step("overlay", "Phase Panels",
                "A phase is the part of the week the house is in, such as free time, nominations or the vote. Open the episode screen for its decisions; the objective at the top left explains what comes next. Conversations show your words, then the houseguest's reply. Continue conversation reads the next line; Skip conversation moves on after the action has already happened.",
                "Episode panel", "Objective"),
            new Step("ready", "You're Ready!",
                "Whenever you are unsure what to do next, the objective at the top left names what the house is waiting on. Good luck!"),
        };

        private Canvas canvas;
        private RectTransform layer, ring, card;
        private RectTransform[] dims;
        private CanvasGroup layerGroup, cardGroup;
        private Image mark;
        private TMP_Text counter, title, body, nextLabel, skipLabel, backLabel, replayNote, takeLabel, notNowLabel;
        private Button next, skip, back, take, notNow;
        private int step = -1;
        /// <summary>Whether the offer card is up: "Take the tour?", asked of a player who has been through it before.</summary>
        private bool offering;
        /// <summary>Where the card was docked last, so a frame whose hole and frame have not moved does not lay it out again.</summary>
        private bool dockDirty = true;
        private TourDock.Box dockedFrame;
        private TourDock.Box? dockedHole;
        private float dockedScale;
        /// <summary>The phase step's open panel had no side with room for the card, so the step lights the objective instead.</summary>
        private bool panelRefused;
        private readonly List<Step> visible = new List<Step>();
        private Func<string, RectTransform> anchorLookup;

        // The step on screen: which chrome it lights, where that is, and how long it has been up.
        private string[] anchorNames = Array.Empty<string>();
        private RectTransform[] targets = Array.Empty<RectTransform>();
        private Rect? hole;
        private Vector2 cardRest;
        private float stepAge, relookIn;
        /// <summary>Where this step's fade starts: nothing for the tour's first step, and the dim as it stood for every step after (see <see cref="ApplyMotion"/>).</summary>
        private float fadeFrom;

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>Matches the HUD's accessibility preference.</summary>
        public float FontScale { get; set; } = 1f;

        /// <summary>
        /// The player's reduced-motion preference: every step arrives at once instead of fading in
        /// with its card rising. The director sets it beside <see cref="FontScale"/>.
        /// </summary>
        public bool ReducedMotion { get; set; }

        /// <summary>
        /// Played on every Next, the last one included, and never on a skip: the reference build's
        /// tutorialStep. Presentation only; the director connects it to the house's cue.
        /// </summary>
        public Action StepSound;

        /// <summary>Whether the tour, or the offer of it, is on screen: either dims the house and waits for an answer.</summary>
        public bool IsShowing => step >= 0 || offering;
        /// <summary>Whether the offer card ("Take the tour?") is on screen. <see cref="StepIndex"/> is -1 while it is.</summary>
        public bool IsOffering => offering;
        /// <summary>Whether the card on screen stands clear of what its step lights (always, but for a frame with no side wide enough).</summary>
        public bool CardDocked { get; private set; } = true;
        public int StepIndex => step;

        /// <summary>How many steps this showing has: seven, less any whose chrome is not on screen.</summary>
        public int Count => step >= 0 ? visible.Count : Steps.Length;

        /// <summary>This showing's step titles, in order, in English. A read, for tests and tools.</summary>
        public IReadOnlyList<string> StepTitles => visible.Select(item => item.Title).ToArray();

        /// <summary>The chrome the step on screen lights, by name; empty for a centred step.</summary>
        public IReadOnlyList<string> CurrentAnchors => step >= 0 ? anchorNames : Array.Empty<string>();

        /// <summary>True when this player has already been through the tour, or skipped it.</summary>
        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

        /// <summary>Isolated sessions keep tour completion in memory instead of the player's preferences.</summary>
        public bool RememberCompletion { get; set; } = true;
        private bool completedThisSession;
        public bool HasSeen => completedThisSession || (RememberCompletion && Seen);

        public static HouseTutorial Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Tutorial",
                typeof(RectTransform), typeof(Canvas), typeof(FrameScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above every broadcast card: a tour that a ceremony could cover would strand the player
            // on a step they cannot read or dismiss.
            canvas.sortingOrder = 140;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            var tutorial = root.AddComponent<HouseTutorial>();
            tutorial.canvas = canvas;
            root.SetActive(false);
            return tutorial;
        }

        /// <summary>
        /// Starts the tour. <paramref name="anchors"/> resolves a chrome panel by name so each step
        /// can light it; the HUD owns those rects and rebuilds them constantly, so they are looked
        /// up per step, and again whenever the one being lit is thrown away.
        ///
        /// <para>A step whose chrome is not on screen is left out, as the reference build leaves its
        /// desktop-only step out on a phone, rather than dimming the house around a hole with
        /// nothing in it.</para>
        /// </summary>
        public void Show(Func<string, RectTransform> anchors)
        {
            anchorLookup = anchors;
            Build();
            offering = false;
            SetOfferMode(false);
            visible.Clear();
            foreach (var candidate in Steps)
                if (candidate.Anchors.Length == 0 || candidate.Anchors.Any(name => Lookup(name) != null))
                    visible.Add(candidate);
            step = 0;
            fadeFrom = 0f;
            PadLine = LastDeviceWasPad != null && LastDeviceWasPad();
            canvas.gameObject.SetActive(true);
            // A canvas switched on this frame has not been sized yet, and the first step is placed
            // against it now rather than a frame late.
            Canvas.ForceUpdateCanvases();
            Render();
            // Next before Skip: the tour takes the keyboard while it is up, and the ring lands on the
            // first control it finds - which put Enter on the skip.
            if (EventSystem.current != null && IsShowing) EventSystem.current.SetSelectedGameObject(next.gameObject);
        }

        /// <summary>
        /// A season's opening: the tour itself for a player who has never been through it, and for
        /// one who has, a card asking "Take the tour?" - "Take the tour" starts it from its first
        /// step, "Not now" (or Escape, or the pad's B) lets the season go on. Nothing is saved in
        /// the season: the only record is the existing preference that the tour has been seen.
        /// </summary>
        public void Offer(Func<string, RectTransform> anchors)
        {
            if (!TourOfferWords.OpensWithOffer(HasSeen)) { Show(anchors); return; }
            anchorLookup = anchors;
            Build();
            step = -1;
            offering = true;
            SetOfferMode(true);
            visible.Clear();
            fadeFrom = 0f;
            hole = null;
            canvas.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            title.text = Localisation.Text(TourOfferWords.OfferTitle);
            body.text = Localisation.Text(TourOfferWords.OfferBody);
            dockDirty = true;
            stepAge = 0f;
            Place();
            ApplyMotion();
            // The keyboard and the pad start on the yes; Escape and B are the no.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(take.gameObject);
        }

        /// <summary>The offer's yes: the tour from its first step, on the lookup the offer was made with.</summary>
        public void TakeTour()
        {
            if (!offering) return;
            offering = false;
            Show(anchorLookup);
        }

        /// <summary>The offer's parts in, the tour's out, or the other way round.</summary>
        private void SetOfferMode(bool offer)
        {
            if (card == null) return;
            counter.gameObject.SetActive(!offer);
            if (mark != null) mark.gameObject.SetActive(!offer);
            next.gameObject.SetActive(!offer);
            skip.gameObject.SetActive(!offer);
            if (offer) back.gameObject.SetActive(false);
            replayNote.gameObject.SetActive(!offer);
            take.gameObject.SetActive(offer);
            notNow.gameObject.SetActive(offer);
        }

        /// <summary>The next step, or the end of the tour from the last one - with the step sound either way.</summary>
        public void Next()
        {
            if (step < 0) return;
            StepSound?.Invoke();
            step++;
            if (step >= visible.Count) { Finish(); return; }
            // The house stays dimmed between steps: the next one fades on from here, not from nothing.
            fadeFrom = layerGroup.alpha;
            Render();
        }

        /// <summary>
        /// The step before, with no step sound: the sound is the reference build's for going on. A
        /// step whose chrome has gone since is dropped as it would be going forward, which leaves
        /// the player on the step they were on.
        /// </summary>
        public void Back()
        {
            if (step <= 0) return;
            step--;
            fadeFrom = layerGroup.alpha;
            Render();
            // The keyboard stays on Previous while there is a step before; on the first step, Next.
            if (EventSystem.current != null && IsShowing)
                EventSystem.current.SetSelectedGameObject(step > 0 ? back.gameObject : next.gameObject);
        }

        /// <summary>
        /// Leaves the tour, or answers the offer "Not now", and remembers that the tour has been
        /// seen, so the offer to an imported season does not come back and the next season's
        /// opening asks rather than replays.
        /// </summary>
        public void Skip() => Finish();

        private void Finish()
        {
            step = -1;
            offering = false;
            completedThisSession = true;
            if (RememberCompletion)
            {
                PlayerPrefs.SetInt(SeenKey, 1);
                PlayerPrefs.Save();
            }
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// The step on screen, drawn. A step whose chrome has gone since the tour began - the HUD
        /// hides some of it while a panel or a conversation holds - is dropped at the moment it
        /// would have shown, the same as one that was never there.
        /// </summary>
        private void Render()
        {
            while (step < visible.Count && !Enter(visible[step])) visible.RemoveAt(step);
            if (step >= visible.Count) { Finish(); return; }

            var current = visible[step];
            counter.text = Localisation.Format("{0} of {1}", step + 1, visible.Count);
            title.text = Localisation.Text(current.Title);
            body.text = Localisation.Text(BodyFor(current));
            nextLabel.text = Localisation.Text(step == visible.Count - 1 ? FinishCaption : NextCaption);
            if (back.gameObject.activeSelf != step > 0) back.gameObject.SetActive(step > 0);

            dockDirty = true;
            stepAge = 0f;
            Track(0f);
            Place();
            ApplyMotion();
        }

        /// <summary>
        /// Every frame the tour is up: the hole follows its chrome, which the HUD moves and rebuilds
        /// under it, and the step's entrance plays out on the real clock, so a stalled frame does not
        /// stretch it.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsShowing || card == null) return;
            float delta = Time.unscaledDeltaTime;
            stepAge += delta;
            if (offering) { Place(); ApplyMotion(); return; }
            FollowDevice();
            Track(delta);
            Place();
            ApplyMotion();
        }

        // ---------------------------------------------------------------- the spotlight

        /// <summary>Takes up a step's chrome. False when none of it is on screen, which drops the step.</summary>
        private bool Enter(Step candidate)
        {
            panelRefused = false;
            LookupTargets(candidate);
            hole = null;
            relookIn = 0f;
            return candidate.Anchors.Length == 0 || targets.Any(Visible);
        }

        /// <summary>
        /// The phase step lights one useful target: the decision itself, then its objective - and
        /// the objective when the open panel left no side with room for the card beside it.
        /// </summary>
        private void LookupTargets(Step candidate)
        {
            anchorNames = candidate.Id == "overlay"
                ? candidate.Anchors.Where(name => Lookup(name) != null && !(panelRefused && name == PanelAnchor)).Take(1).ToArray()
                : candidate.Anchors;
            targets = new RectTransform[anchorNames.Length];
            for (int i = 0; i < anchorNames.Length; i++) targets[i] = Lookup(anchorNames[i]);
        }

        private RectTransform Lookup(string name)
        {
            if (anchorLookup == null || string.IsNullOrEmpty(name)) return null;
            var found = anchorLookup(name);
            return Visible(found) ? found : null;
        }

        /// <summary>On screen: there, switched on, and with an area. Alpha is not asked - the HUD comes back from a cinematic by alpha, the frame the tour starts.</summary>
        private static bool Visible(RectTransform rect) =>
            rect != null && rect.gameObject.activeInHierarchy && rect.rect.width > .5f && rect.rect.height > .5f;

        /// <summary>
        /// Where the step's chrome is now, as the hole round it. A HUD rebuild throws the lit panel
        /// away and draws a new one in its place, so a target that has gone is looked up again (not
        /// every frame: the lookup walks the scene). While nothing is found the hole stays where it
        /// was, rather than jumping to the middle for the frame of a rebuild.
        /// </summary>
        private void Track(float delta)
        {
            if (visible[step].Anchors.Length == 0) { hole = null; return; }
            relookIn -= delta;
            if (relookIn <= 0f && (!targets.All(Visible) || visible[step].Id == "overlay"))
            {
                LookupTargets(visible[step]);
                relookIn = RelookSeconds;
            }

            Rect? union = null;
            foreach (var target in targets)
            {
                if (!Visible(target)) continue;
                var local = LocalRect(target);
                if (!local.HasValue) continue;
                union = union.HasValue
                    ? Rect.MinMaxRect(Mathf.Min(union.Value.xMin, local.Value.xMin), Mathf.Min(union.Value.yMin, local.Value.yMin),
                        Mathf.Max(union.Value.xMax, local.Value.xMax), Mathf.Max(union.Value.yMax, local.Value.yMax))
                    : local;
            }
            if (union.HasValue)
                hole = Rect.MinMaxRect(union.Value.xMin - HolePadding, union.Value.yMin - HolePadding,
                    union.Value.xMax + HolePadding, union.Value.yMax + HolePadding);
        }

        /// <summary>
        /// A piece of chrome's rectangle in the tour's own canvas units.
        ///
        /// <para>Through the screen, both ways. The tour placed its card from the chrome's world
        /// corners less half the canvas, which is screen pixels less canvas units: right only when
        /// the two are the same, at a 1600x900 window, and 160 units out at 1920x1080. A corner goes
        /// to the screen through the camera of the canvas it is drawn on (none for an overlay) and
        /// comes back into this canvas through this canvas's, so the HUD's scale, this canvas's
        /// scale and the window's size each count once.</para>
        /// </summary>
        private Rect? LocalRect(RectTransform target)
        {
            target.GetWorldCorners(Corners);
            var drawnOn = target.GetComponentInParent<Canvas>();
            var from = drawnOn != null ? drawnOn.rootCanvas : null;
            Camera source = from == null || from.renderMode == RenderMode.ScreenSpaceOverlay ? null : from.worldCamera;
            Camera own = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(source, Corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, own, out var local)) return null;
                xMin = Mathf.Min(xMin, local.x); yMin = Mathf.Min(yMin, local.y);
                xMax = Mathf.Max(xMax, local.x); yMax = Mathf.Max(yMax, local.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>The dim, the ring and where the card rests, for the hole as it stands.</summary>
        private void Place()
        {
            var bounds = layer.rect;
            Shade(bounds);
            bool lit = hole.HasValue;
            if (ring.gameObject.activeSelf != lit) ring.gameObject.SetActive(lit);
            if (lit) Put(ring, hole.Value);
            if (Dock(bounds)) return;

            // The open panel left no side with room for the card: the phase step lights the
            // objective instead, which the step names as well, and the card docks beside that.
            panelRefused = true;
            LookupTargets(visible[step]);
            relookIn = RelookSeconds;
            hole = null;
            Track(0f);
            Shade(bounds);
            lit = hole.HasValue;
            if (ring.gameObject.activeSelf != lit) ring.gameObject.SetActive(lit);
            if (lit) Put(ring, hole.Value);
            dockDirty = true;
            Dock(bounds);
        }

        /// <summary>The name of the open decision panel, the phase step's first target.</summary>
        private const string PanelAnchor = "Episode panel";

        /// <summary>
        /// Lays the card out and docks it beside the hole (<see cref="TourDock"/>), when the frame,
        /// the hole, the text size or the words have changed since it was last docked. False only
        /// when the phase step's open panel had no side with room for the card and the objective
        /// can stand in for it.
        /// </summary>
        private bool Dock(Rect bounds)
        {
            var frame = new TourDock.Box(bounds.xMin, bounds.yMin, bounds.width, bounds.height);
            TourDock.Box? target = hole.HasValue
                ? new TourDock.Box(hole.Value.xMin, hole.Value.yMin, hole.Value.width, hole.Value.height)
                : (TourDock.Box?)null;
            float s = TourDock.Scale(FontScale);
            if (!dockDirty && s == dockedScale && Near(frame, dockedFrame) && target.HasValue == dockedHole.HasValue
                && (!target.HasValue || Near(target.Value, dockedHole.Value)))
                return true;
            dockDirty = false;
            dockedFrame = frame; dockedHole = target; dockedScale = s;

            SetFontSizes(s);
            float titleWidth = title.GetPreferredValues(title.text).x;
            float baseWidth = TourDock.BaseWidth(s, titleWidth, frame.W);
            var placement = TourDock.Place(frame, target, baseWidth, TourDock.MinWidth(s, titleWidth), Layout);
            Layout(placement.Card.W);
            cardRest = new Vector2(placement.Card.X, placement.Card.Y);
            CardDocked = placement.Docked;

            bool refuse = !placement.Docked && !offering && step >= 0 && step < visible.Count && visible[step].Id == "overlay"
                && !panelRefused && anchorNames.Length > 0 && anchorNames[0] == PanelAnchor && Lookup("Objective") != null;
            return !refuse;
        }

        private static bool Near(TourDock.Box a, TourDock.Box b) =>
            Mathf.Abs(a.X - b.X) < .5f && Mathf.Abs(a.Y - b.Y) < .5f && Mathf.Abs(a.W - b.W) < .5f && Mathf.Abs(a.H - b.H) < .5f;

        /// <summary>
        /// The reference build's desktop dim: four black panels round the hole, not one panel with a
        /// mask. A centred step dims the whole frame with the first panel.
        ///
        /// <para>The hole is lit, not open. What it shows is there to be looked at: the reference
        /// wraps its whole tour in one full-screen layer over the HUD, so a click in the hole reaches
        /// nothing, and here the ring, which covers the hole exactly, takes that click (see
        /// <see cref="Build"/>). Left open, a click on the lit rail opened a notebook page under the
        /// tour, and one on the lit top bar - chrome that lets clicks through to the house - walked
        /// the player to the floor behind it.</para>
        /// </summary>
        private void Shade(Rect bounds)
        {
            if (!hole.HasValue)
            {
                Put(dims[0], bounds);
                for (int i = 1; i < dims.Length; i++) Put(dims[i], new Rect(bounds.center, Vector2.zero));
                return;
            }
            var open = Rect.MinMaxRect(
                Mathf.Clamp(hole.Value.xMin, bounds.xMin, bounds.xMax), Mathf.Clamp(hole.Value.yMin, bounds.yMin, bounds.yMax),
                Mathf.Clamp(hole.Value.xMax, bounds.xMin, bounds.xMax), Mathf.Clamp(hole.Value.yMax, bounds.yMin, bounds.yMax));
            Put(dims[0], Rect.MinMaxRect(bounds.xMin, open.yMax, bounds.xMax, bounds.yMax));
            Put(dims[1], Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, open.yMin));
            Put(dims[2], Rect.MinMaxRect(bounds.xMin, open.yMin, open.xMin, open.yMax));
            Put(dims[3], Rect.MinMaxRect(open.xMax, open.yMin, bounds.xMax, open.yMax));
        }

        /// <summary>
        /// The reference build's step entrance: the whole step, dim and all, fades in over 0.3 s,
        /// and the card rises 20 units into place over 0.35 s after 0.15 s. All at once under
        /// reduced motion. Only alpha moves: a group made non-interactable greys out every control
        /// under it, so the card's buttons answer throughout.
        ///
        /// <para>Only the tour's first step fades in from nothing. The reference also fades each
        /// step out over 0.3 s before the next comes in, and this has no exit, so starting every
        /// step from nothing dropped the dim - 60 % black over the whole house - to nothing in one
        /// frame on every Next and brought it back over the next 0.3 s: a full-screen flash per
        /// step. A later step's fade picks up from the dim as it stands instead, which is all the
        /// way up once a step has settled, and only its card replays the rise.</para>
        /// </summary>
        private void ApplyMotion()
        {
            float fade = ReducedMotion ? 1f : Mathf.Lerp(fadeFrom, 1f, Mathf.Clamp01(stepAge / StepFadeSeconds));
            float rise = ReducedMotion ? 1f : EaseOut(Mathf.Clamp01((stepAge - CardDelaySeconds) / CardRiseSeconds));
            layerGroup.alpha = fade;
            cardGroup.alpha = rise;
            Put(card, new Rect(cardRest + new Vector2(0f, -RiseDistance * (1f - rise)), card.sizeDelta));
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>A child of the step layer over <paramref name="area"/>, given in the layer's own units.</summary>
        private void Put(RectTransform rect, Rect area)
        {
            var centre = layer.rect.center;
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = area.min - centre;
            rect.sizeDelta = new Vector2(Mathf.Max(0f, area.width), Mathf.Max(0f, area.height));
        }

        // ---------------------------------------------------------------- the card

        /// <summary>The card's type at a text scale.</summary>
        private void SetFontSizes(float s)
        {
            counter.fontSize = 13f * s;
            title.fontSize = 21f * s;
            body.fontSize = 15f * s;
            nextLabel.fontSize = 15f * s;
            skipLabel.fontSize = 14f * s;
            backLabel.fontSize = 14f * s;
            replayNote.fontSize = 14f * s;
            takeLabel.fontSize = 15f * s;
            notNowLabel.fontSize = 14f * s;
        }

        /// <summary>
        /// The card laid out at <paramref name="width"/>, at the player's text size, by
        /// <see cref="TourDock.Measure"/>; returns its height, which is what docking asks of it. Every
        /// box is at least 1.3 times its type: Inter draws nothing at all in a box under about 1.21.
        /// </summary>
        private float Layout(float width)
        {
            float s = TourDock.Scale(FontScale);
            SetFontSizes(s);
            var rows = TourDock.Measure(width, s,
                inner => title.GetPreferredValues(title.text, inner, 0f).y,
                inner => body.GetPreferredValues(body.text, inner, 0f).y,
                inner => replayNote.GetPreferredValues(replayNote.text, inner, 0f).y,
                offering);
            float pad = rows.Pad, inner = rows.Inner, counterX = pad, counterHeight = TourDock.CounterHeight(s);
            if (mark != null)
            {
                Row(mark.rectTransform, pad - 2f * s, rows.CounterY, counterHeight, counterHeight);
                counterX = pad + 24f * s;
            }
            Row(counter.rectTransform, counterX, rows.CounterY, width - pad - counterX, counterHeight);
            Row(title.rectTransform, pad, rows.TitleY, inner, rows.TitleHeight);
            Row(body.rectTransform, pad, rows.BodyY, inner, rows.BodyHeight);

            float buttonHeight = TourDock.ButtonHeight(s), nextWidth = TourDock.NextWidth(s), backWidth = TourDock.BackWidth(s);
            // The offer: "Not now" at the left, "Take the tour" at the right, where Skip and Next stand.
            Row((RectTransform)notNow.transform, pad - 10f * s, rows.ButtonsY, TourDock.SkipWidth(s), buttonHeight);
            Row((RectTransform)take.transform, width - pad - TourDock.TakeWidth(s), rows.ButtonsY, TourDock.TakeWidth(s), buttonHeight);
            // The tour: Skip at the left, then Previous beside Next at the right - the step on and the
            // step back together. On a narrow card, Skip takes the row under them.
            Row((RectTransform)skip.transform, pad - 10f * s, rows.SkipY, TourDock.SkipWidth(s), buttonHeight);
            Row((RectTransform)next.transform, width - pad - nextWidth, rows.ButtonsY, nextWidth, buttonHeight);
            Row((RectTransform)back.transform, width - pad - nextWidth - 8f * s - backWidth, rows.ButtonsY, backWidth, buttonHeight);
            // The words clear of the chevron, which is a mark of its own beside them.
            nextLabel.rectTransform.offsetMin = new Vector2(10f * s, 0f);
            nextLabel.rectTransform.offsetMax = new Vector2(-24f * s, 0f);

            // How to see it again, under the buttons, in the card's quiet type.
            Row(replayNote.rectTransform, pad, rows.NoteY, inner, Mathf.Max(rows.NoteHeight, replayNote.fontSize * 1.3f));

            card.sizeDelta = new Vector2(width, rows.Height);
            return rows.Height;
        }

        private void Build()
        {
            if (card != null) return;
            var root = (RectTransform)canvas.transform;

            // One layer for the whole step, so it fades as one, as the reference build's does.
            layer = new GameObject(StepLayerName, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            layer.SetParent(root, false);
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero; layer.offsetMax = Vector2.zero;
            layer.pivot = new Vector2(.5f, .5f);
            layerGroup = layer.GetComponent<CanvasGroup>();

            // The dim takes the clicks it covers: the house under a tour is not for walking in.
            dims = new RectTransform[DimNames.Length];
            for (int i = 0; i < DimNames.Length; i++)
            {
                var shade = new GameObject(DimNames[i], typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                shade.rectTransform.SetParent(layer, false);
                shade.color = new Color(0f, 0f, 0f, DimAlpha);
                shade.raycastTarget = true;
                dims[i] = shade.rectTransform;
            }

            // The reference's ring: a 2-unit edge at 70 % with a soft glow at 25-30 % outside it, in
            // the HUD's accent. It is sized to the hole, so it is also what takes a click there,
            // on a fill of its own that is never seen: the dim blocks the rest of the frame, and the
            // reference's full-screen tour layer blocks the hole as well (see Shade).
            ring = new GameObject(RingName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            ring.SetParent(layer, false);
            var catcher = ring.GetComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
            // Kept in the canvas's batch at alpha 0 rather than culled as transparent: it is there
            // to be hit, not seen, and the raycaster only considers what the canvas has drawn.
            catcher.canvasRenderer.cullTransparentMesh = false;
            UiTheme.AddGlow(ring, UiTheme.ControlRadius);
            UiTheme.AddBorder(ring, UiTheme.ControlRadius, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .7f));
            var halo = ring.Find("Glow");
            var haloImage = halo != null ? halo.GetComponent<Image>() : null;
            if (haloImage != null) haloImage.color = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .3f);
            ring.gameObject.SetActive(false);

            // The HUD's glass with a lit edge, as the mockups draw the one card that is talking to
            // the player; it was a flat raised grey with a gold button, and gold is power in this
            // house, not "next".
            var glass = UiTheme.GlassFill; glass.a = .95f;
            card = HudPrimitives.Fill(CardName, layer, glass, UiTheme.PanelRadius);
            card.GetComponent<Image>().raycastTarget = true;
            UiTheme.AddBorder(card, UiTheme.PanelRadius, UiTheme.Accent);
            UiTheme.AddGlow(card, UiTheme.PanelRadius);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();

            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var bulb = UiTheme.Icon("bulb");
            if (bulb != null)
            {
                mark = new GameObject("Tip mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(card, false);
                mark.sprite = bulb; mark.color = UiTheme.Joke; mark.preserveAspect = true; mark.raycastTarget = false;
            }
            counter = HudPrimitives.Label("Counter", card, 13f, UiTheme.Heading);
            if (semibold != null) counter.font = semibold;
            counter.characterSpacing = 3f;

            title = HudPrimitives.Label("Title", card, 21f, UiTheme.Paper, TextAlignmentOptions.TopLeft);
            if (semibold != null) title.font = semibold;

            body = HudPrimitives.Label("Body", card, 15f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .82f),
                TextAlignmentOptions.TopLeft);

            // Next before Skip: the tour takes the keyboard while it is up, and the ring lands on the
            // first control it finds - which put Enter on the skip. Named "Next" whatever it says:
            // the focus test and the HUD's ring find it by name, and it says "Let's Go!" last.
            next = TextButton(NextCaption, card, Color.white, 15f, UiTheme.ActionBlue);
            nextLabel = next.GetComponentInChildren<TMP_Text>();
            if (semibold != null) nextLabel.font = semibold;
            var chevron = HudPrimitives.Chevron(next.transform, Color.white, 10f * Mathf.Max(0.5f, FontScale));
            chevron.anchoredPosition = new Vector2(-12f * Mathf.Max(0.5f, FontScale), 0f);
            next.onClick.AddListener(Next);

            skip = TextButton(SkipCaption, card, UiTheme.Muted, 14f);
            skipLabel = skip.GetComponentInChildren<TMP_Text>();
            skip.onClick.AddListener(Skip);

            // Previous is quieter than Next: words in the accent with no fill, brightening to the paper
            // with the keyboard on them as the skip's do.
            back = TextButton(PreviousCaption, card, UiTheme.Accent, 14f);
            backLabel = back.GetComponentInChildren<TMP_Text>();
            back.onClick.AddListener(Back);
            back.gameObject.SetActive(false);

            // The offer's two answers, up only while it asks: the yes in the action blue Next wears,
            // the no in the skip's quiet words. Escape and the pad's B are the no as well.
            take = TextButton(TakeCaption, card, Color.white, 15f, UiTheme.ActionBlue);
            takeLabel = take.GetComponentInChildren<TMP_Text>();
            if (semibold != null) takeLabel.font = semibold;
            take.onClick.AddListener(TakeTour);
            take.gameObject.SetActive(false);
            notNow = TextButton(NotNowCaption, card, UiTheme.Muted, 14f);
            notNowLabel = notNow.GetComponentInChildren<TMP_Text>();
            notNow.onClick.AddListener(Skip);
            notNow.gameObject.SetActive(false);

            replayNote = HudPrimitives.Label(ReplayNoteName, card, 14f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            replayNote.text = Localisation.Text(ReplayNote);
        }

        /// <summary>A child of the card, placed from its top-left corner in already-scaled units.</summary>
        private static void Row(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Button TextButton(string caption, Transform parent, Color ink, float size, Color? fill = null)
        {
            var rect = fill.HasValue
                ? HudPrimitives.Fill(caption, parent, fill.Value, UiTheme.ControlRadius)
                : HudPrimitives.Fill(caption, parent, new Color(0f, 0f, 0f, 0f), UiTheme.ControlRadius);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Text", rect, size, ink, TextAlignmentOptions.Center);
            label.text = Localisation.Text(caption);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            // Seen while it has the keyboard: brighter when hovered or focused, as the HUD's own
            // controls are. Unity's default darkened a fill by four per cent, which nobody can see.
            var button = rect.gameObject.AddComponent<Button>();
            var colours = button.colors;
            if (fill.HasValue)
            {
                button.targetGraphic = image;
                colours.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            }
            else
            {
                // A control with no fill has only its words to be seen by, so they are what
                // brightens: from the ink they rest in to the HUD's paper. The tint used to go on
                // the transparent fill, and a tint of nothing is nothing - "Skip the tour" looked
                // the same with the keyboard on it as off. The fill stays its hit area, kept in the
                // canvas's batch at alpha 0 as the spotlight ring's is.
                button.targetGraphic = label;
                colours.highlightedColor = Towards(ink, UiTheme.Paper);
                image.canvasRenderer.cullTransparentMesh = false;
            }
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(.85f, .85f, .85f);
            button.colors = colours;
            return button;
        }

        /// <summary>The tint that turns <paramref name="from"/> into <paramref name="to"/>: a button's tint multiplies its graphic's own colour.</summary>
        private static Color Towards(Color from, Color to) => new Color(
            from.r > .001f ? to.r / from.r : 1f,
            from.g > .001f ? to.g / from.g : 1f,
            from.b > .001f ? to.b / from.b : 1f,
            1f);
    }
}
