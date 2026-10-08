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
    /// shown in batchmode. The director shows it at every season's opening, as the reference build
    /// shows it in every new game's first week, and once to a player who arrives in a season that
    /// has no opening to play (an import, a migrated save). <see cref="Show"/> is public so a test
    /// can drive and photograph it without pretending to be either.</para>
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
        public const string SkipCaption = "Skip Tutorial";

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

        /// <summary>A step's words, with the controls line in the device's words.</summary>
        private string BodyFor(Step current) =>
            PadLine ? current.Body.Replace(ControlsLine, InputGlossary.TourLine(true)) : current.Body;

        /// <summary>A press on the other kind of device rewords the step on screen, in place.</summary>
        private void FollowDevice()
        {
            var pad = Gamesim.House.HouseInput.PadUsed();
            if (!pad.HasValue || pad.Value == PadLine) return;
            PadLine = pad.Value;
            if (step < 0 || step >= visible.Count || body == null) return;
            body.text = Localisation.Text(BodyFor(visible[step]));
            Layout();
            Place();
        }

        /// <summary>The reference card's width: its max-w-sm, in the canvas's 1600x900 units.</summary>
        private const float CardWidth = 384f;
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
        /// The phase overlays are the docked panel, pointed at through the status line under it,
        /// because the panel is closed while the tour is up.</para>
        /// </summary>
        private static readonly Step[] Steps =
        {
            new Step("welcome", "Welcome to Gamesim: The House!",
                "Let's take a quick tour so you know how everything works. " + ControlsLine),
            new Step("topbar", "Top Bar",
                "The week and where the house is in it, how many houseguests are left, the social actions you have left this week, and who is Head of Household.",
                "Week chip", "House pill"),
            new Step("sidemenu", "Side Rail",
                "The Overview of the whole house and your notebook's pages: Houseguests, Relationships, Who is where, The vote and The story so far. Notebook, Save and Settings are at its foot.",
                "Rail ground"),
            new Step("gamephase", "The Episode Screen",
                "Nominations, votes and the week's other decisions happen at the living-room screen. This button walks you there; press E to open it and Esc to close it.",
                "Go to episode screen"),
            new Step("carousel", "Cast Strip",
                "Everyone in the house, along the bottom. Click a face to follow them with the camera. Under each name is their mood; once you know them, a tag says where you stand.",
                CastRail.StripName),
            new Step("overlay", "Phase Panels",
                "When you open the episode screen, the decision in front of you comes up in a panel low on the screen, with the house still in view. Watch this line for what just happened.",
                "Status"),
            new Step("ready", "You're Ready!",
                "Whenever you are unsure what to do next, the Current Objective names what the house is waiting on. Good luck!"),
        };

        private Canvas canvas;
        private RectTransform layer, ring, card;
        private RectTransform[] dims;
        private CanvasGroup layerGroup, cardGroup;
        private Image mark;
        private TMP_Text counter, title, body, nextLabel, skipLabel;
        private Button next, skip;
        private int step = -1;
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

        public bool IsShowing => step >= 0;
        public int StepIndex => step;

        /// <summary>How many steps this showing has: seven, less any whose chrome is not on screen.</summary>
        public int Count => IsShowing ? visible.Count : Steps.Length;

        /// <summary>This showing's step titles, in order, in English. A read, for tests and tools.</summary>
        public IReadOnlyList<string> StepTitles => visible.Select(item => item.Title).ToArray();

        /// <summary>The chrome the step on screen lights, by name; empty for a centred step.</summary>
        public IReadOnlyList<string> CurrentAnchors => IsShowing ? visible[step].Anchors : Array.Empty<string>();

        /// <summary>True when this player has already been through the tour, or skipped it.</summary>
        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

        /// <summary>Isolated sessions keep tour completion in memory instead of the player's preferences.</summary>
        public bool RememberCompletion { get; set; } = true;
        private bool completedThisSession;
        public bool HasSeen => completedThisSession || (RememberCompletion && Seen);

        public static HouseTutorial Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Tutorial",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
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

        /// <summary>Leaves the tour and remembers that, so the offer to an imported season does not come back.</summary>
        public void Skip() => Finish();

        private void Finish()
        {
            step = -1;
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

            Layout();
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
            if (step < 0 || card == null) return;
            FollowDevice();
            float delta = Time.unscaledDeltaTime;
            stepAge += delta;
            Track(delta);
            Place();
            ApplyMotion();
        }

        // ---------------------------------------------------------------- the spotlight

        /// <summary>Takes up a step's chrome. False when none of it is on screen, which drops the step.</summary>
        private bool Enter(Step candidate)
        {
            anchorNames = candidate.Anchors;
            targets = new RectTransform[anchorNames.Length];
            for (int i = 0; i < anchorNames.Length; i++) targets[i] = Lookup(anchorNames[i]);
            hole = null;
            relookIn = 0f;
            return anchorNames.Length == 0 || targets.Any(Visible);
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
            if (anchorNames.Length == 0) { hole = null; return; }
            relookIn -= delta;
            if (relookIn <= 0f && !targets.All(Visible))
            {
                for (int i = 0; i < targets.Length; i++)
                    if (!Visible(targets[i])) targets[i] = Lookup(anchorNames[i]);
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
            cardRest = CardCorner(bounds, card.sizeDelta);
        }

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
        /// Where the card rests: the reference build's placement rules, read off the hole's real
        /// position instead of its hard-coded one.
        ///
        /// <para>Against the right edge, to its left with the tops level (its <c>right: 80px</c>,
        /// twenty clear of a sixty-wide rail). Against the left edge, to its right with the tops
        /// level (its <c>left: 110px</c>, fourteen clear of a ninety-six-wide carousel). Across the
        /// top, under it and in from its left (its <c>top: 70px; left: 16px</c>, ten under a
        /// sixty-high bar). The reference has nothing across the bottom; this HUD has the cast strip
        /// and the status line there, and the card stands over them the way it stands under the top
        /// bar. No hole, the middle of the frame. Always inside the frame.</para>
        /// </summary>
        private Vector2 CardCorner(Rect bounds, Vector2 size)
        {
            Vector2 corner;
            if (!hole.HasValue) corner = bounds.center - size * .5f;
            else
            {
                var lit = hole.Value;
                float edge = bounds.width * .2f;
                bool rightEdge = bounds.xMax - lit.xMax < edge && lit.xMin > bounds.center.x;
                bool leftEdge = lit.xMin - bounds.xMin < edge && lit.xMax < bounds.center.x;
                if (rightEdge) corner = new Vector2(lit.xMin - 20f - size.x, lit.yMax - size.y);
                else if (leftEdge) corner = new Vector2(lit.xMax + 14f, lit.yMax - size.y);
                else if (lit.center.y >= bounds.center.y) corner = new Vector2(lit.xMin + 16f, lit.yMin - 10f - size.y);
                else corner = new Vector2(lit.center.x - size.x * .5f, lit.yMax + 10f);
            }
            float left = bounds.xMin + 12f, right = Mathf.Max(left, bounds.xMax - 12f - size.x);
            float bottom = bounds.yMin + 12f, top = Mathf.Max(bottom, bounds.yMax - 12f - size.y);
            return new Vector2(Mathf.Clamp(corner.x, left, right), Mathf.Clamp(corner.y, bottom, top));
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

        /// <summary>
        /// The card sized to what this step says, at the player's text size. Every box is at least
        /// 1.3 times its type: Inter draws nothing at all in a box under about 1.21.
        /// </summary>
        private void Layout()
        {
            float s = Mathf.Max(0.5f, FontScale);
            var bounds = layer.rect;
            float width = CardWidth * s;
            if (bounds.width > 48f) width = Mathf.Min(width, bounds.width - 24f);
            float pad = 20f * s, inner = width - 2f * pad;

            counter.fontSize = 13f * s;
            title.fontSize = 21f * s;
            body.fontSize = 15f * s;
            nextLabel.fontSize = 15f * s;
            skipLabel.fontSize = 14f * s;

            float y = pad, counterX = pad, counterHeight = 18f * s;
            if (mark != null)
            {
                Row(mark.rectTransform, pad - 2f * s, y, counterHeight, counterHeight);
                counterX = pad + 24f * s;
            }
            Row(counter.rectTransform, counterX, y, width - pad - counterX, counterHeight);
            y += counterHeight + 6f * s;

            float titleHeight = Mathf.Ceil(Mathf.Max(title.GetPreferredValues(title.text, inner, 0f).y, title.fontSize * 1.3f)) + 2f;
            Row(title.rectTransform, pad, y, inner, titleHeight);
            y += titleHeight + 8f * s;

            float bodyHeight = Mathf.Ceil(Mathf.Max(body.GetPreferredValues(body.text, inner, 0f).y, body.fontSize * 1.3f)) + 4f;
            Row(body.rectTransform, pad, y, inner, bodyHeight);
            y += bodyHeight + 18f * s;

            float buttonHeight = 36f * s, nextWidth = 124f * s, skipWidth = 132f * s;
            Row((RectTransform)skip.transform, pad - 10f * s, y, skipWidth, buttonHeight);
            Row((RectTransform)next.transform, width - pad - nextWidth, y, nextWidth, buttonHeight);
            // The words clear of the chevron, which is a mark of its own beside them.
            nextLabel.rectTransform.offsetMin = new Vector2(10f * s, 0f);
            nextLabel.rectTransform.offsetMax = new Vector2(-24f * s, 0f);
            y += buttonHeight + pad;

            card.sizeDelta = new Vector2(width, y);
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
                // the transparent fill, and a tint of nothing is nothing - "Skip Tutorial" looked
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
