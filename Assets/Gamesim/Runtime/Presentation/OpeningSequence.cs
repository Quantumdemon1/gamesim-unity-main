using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The five beats before the first competition: intro, house entry, walk-in, tutorial, meet and
    /// greet.
    ///
    /// <para>Four of the five have never existed here. A season started by dropping the player into a
    /// furnished house with a HUD already on it, which is a simulation beginning rather than an
    /// episode beginning — the reference build spends its first minute establishing that this is a
    /// television programme, and that minute is most of the difference in how the two feel.</para>
    ///
    /// <para><b>Everything is skippable and every beat is recorded.</b> A sequence that replays on
    /// every load is the single worst thing a cinematic can do, so each beat commits
    /// <see cref="EpisodeCommandKind.MarkOpeningBeat"/> as it finishes and a season that has seen a
    /// beat never plays it again. Skipping marks the rest of them, because a player who skipped the
    /// intro has decided about the intro.</para>
    ///
    /// <para>Motion is the decoration, not the content. Under the reduced-motion preference the
    /// confetti, the flashes and the fly-ins simply do not happen while every card still holds long
    /// enough to read — the preference removes movement, not information.</para>
    ///
    /// <para><b>Confetti and flashes are UI images rather than particle systems</b>, which is a
    /// deliberate departure from the plan that called for runtime-built <c>ParticleSystem</c>s. A
    /// particle system renders in the world, so over a screen-space overlay it would need a camera
    /// to sit in front of, a URP material to be lit by and a render pipeline to be present — three
    /// dependencies for confetti. Rectangles in the same canvas as the titles need none of them and
    /// obey the same fade.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OpeningSequence : MonoBehaviour
    {
        /// <summary>Captions tests and the tour find these controls by.</summary>
        public const string SkipCaption = "Skip the opening";

        private const float TitleRise = 0.9f;
        private const float CardHold = 2.4f;
        private const float RoomHold = 1.5f;
        private const int ConfettiCount = 90;

        /// <summary>What the sequence needs from the game around it.</summary>
        public sealed class Settings
        {
            /// <summary>Commits a finished beat. The only thing here that touches the season.</summary>
            public Action<string> MarkBeat;

            /// <summary>Optional. Without it the walk-in shows its cards and moves no camera.</summary>
            public HouseCameraRig Rig;

            /// <summary>Where the walk-in stops, in the order it visits them.</summary>
            public IReadOnlyList<KeyValuePair<string, Vector3>> RoomStops =
                new List<KeyValuePair<string, Vector3>>();

            /// <summary>Runs the first-run tour and calls back when it is done or skipped.</summary>
            public Action<Action> RunTutorial;

            /// <summary>Called once, whether the sequence finished or was skipped.</summary>
            public Action Finished;

            public bool ReducedMotion;

            /// <summary>The house, for the fly-in panels and the meet and greet.</summary>
            public IReadOnlyList<ContestantState> Cast = new List<ContestantState>();

            /// <summary>The season's own opening line, which the house-entry card reads out.</summary>
            public string ArrivalLine = string.Empty;
        }

        private CanvasGroup group;
        private CanvasScaler scaler;
        private RectTransform stage;
        private Settings settings;
        private Coroutine running;
        private readonly List<string> remaining = new List<string>();

        public bool IsPlaying => running != null;

        /// <summary>The beat on screen, or null between beats and when nothing is playing.</summary>
        public string CurrentBeat { get; private set; }

        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
            }
        }

        /// <summary>
        /// A canvas above every other overlay, and it raycasts — the skip control is the one thing
        /// on screen and has to be clickable.
        ///
        /// <para>Parented to the director, the way the cast screen and the season report are, rather
        /// than sitting on its own scene root like the ceremony cards. The ceremony cards stay
        /// outside that subtree on purpose because they are watched rather than used, and a card that
        /// happened to carry a houseguest's name would show up in every enumeration of the director's
        /// controls. This one is a screen with a control on it, so it belongs where the other screens
        /// are.</para>
        /// </summary>
        public static OpeningSequence Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Opening Sequence",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the ceremony takeover's 100 and the cast screen's 125: nothing outranks the
            // opening, because nothing else should be happening during it.
            canvas.sortingOrder = 140;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var sequence = root.AddComponent<OpeningSequence>();
            sequence.scaler = scaler;
            sequence.group = root.GetComponent<CanvasGroup>();
            sequence.Hide();
            return sequence;
        }

        private void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            CurrentBeat = null;
        }

        /// <summary>
        /// Plays whichever of the five beats this season has not seen.
        ///
        /// <para>Returns without doing anything when there are none left, which is the ordinary case
        /// for every load after the first. The caller does not have to know the difference.</para>
        /// </summary>
        public void Play(IEnumerable<string> alreadySeen, Settings plan)
        {
            if (running != null) return;
            settings = plan ?? new Settings();

            var seen = new HashSet<string>(alreadySeen ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            remaining.Clear();
            remaining.AddRange(OpeningBeat.InOrder.Where(beat => !seen.Contains(beat)));
            if (remaining.Count == 0) { settings.Finished?.Invoke(); return; }

            if (settings.Rig != null) settings.Rig.ControlsEnabled = false;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            running = StartCoroutine(Run());
        }

        /// <summary>
        /// Ends the sequence now and records every beat it had left.
        ///
        /// <para>Marking the unplayed beats rather than leaving them pending is the point: somebody
        /// who skipped the opening has decided about the opening, and offering it again on the next
        /// load would be arguing with them.</para>
        /// </summary>
        public void Skip()
        {
            if (running == null) return;
            StopCoroutine(running);
            running = null;
            foreach (var beat in remaining.ToList()) Mark(beat);
            Finish();
        }

        // ---------------------------------------------------------------- the sequence

        private IEnumerator Run()
        {
            foreach (var beat in remaining.ToList())
            {
                CurrentBeat = beat;
                switch (beat)
                {
                    case OpeningBeat.Intro: yield return Intro(); break;
                    case OpeningBeat.HouseEntry: yield return HouseEntry(); break;
                    case OpeningBeat.WalkIn: yield return WalkIn(); break;
                    case OpeningBeat.Tutorial: yield return Tutorial(); break;
                    case OpeningBeat.MeetAndGreet: yield return MeetAndGreet(); break;
                }
                Mark(beat);
            }

            running = null;
            Finish();
        }

        private void Mark(string beat)
        {
            remaining.Remove(beat);
            settings.MarkBeat?.Invoke(beat);
        }

        private void Finish()
        {
            Clear();
            Hide();
            if (settings?.Rig != null) settings.Rig.ControlsEnabled = true;
            var done = settings?.Finished;
            settings = null;
            done?.Invoke();
        }

        // ---------------------------------------------------------------- beats

        /// <summary>
        /// The title sequence: confetti, flashbulbs, the cast flying in on panels, and the camera
        /// pulling all the way out before diving on the house.
        /// </summary>
        private IEnumerator Intro()
        {
            Clear();
            Scrim(new Color(0.01f, 0.02f, 0.03f, 1f));
            Skipper();

            var title = Title("BIG BROTHER", 84f, UiTheme.Gold, 120f);
            var subtitle = Title("A NEW SEASON BEGINS", 26f, UiTheme.Paper, 40f);
            var panels = Panels();

            if (settings.Rig != null)
            {
                // All the way out, then in: the source's "dramatic zoom". Reduced motion lands on
                // the second of these immediately, which is the shot rather than the sweep.
                settings.Rig.MoveTo(settings.Rig.HouseCenter, settings.Rig.FarthestDistance);
            }

            if (!Motionless)
            {
                Confetti();
                yield return Flashbulbs(2);
                yield return Rise(title, TitleRise);
                yield return Rise(subtitle, TitleRise * 0.6f);
                foreach (var panel in panels) yield return FlyIn(panel, 0.12f);
            }

            if (settings.Rig != null)
                settings.Rig.MoveTo(settings.Rig.HouseCenter, settings.Rig.FarthestDistance * 0.45f);

            yield return Hold(CardHold);
        }

        private IEnumerator HouseEntry()
        {
            Clear();
            Scrim(new Color(0.01f, 0.02f, 0.03f, 0.96f));
            Skipper();
            Title("THE HOUSEGUESTS ARRIVE", 48f, UiTheme.Gold, 70f);
            Title(string.IsNullOrEmpty(settings.ArrivalLine)
                    ? "Doors open, bags come down, and nobody knows anybody yet."
                    : settings.ArrivalLine,
                20f, UiTheme.Muted, 64f);
            yield return Hold(CardHold);
        }

        /// <summary>
        /// The tour of the house: the camera visits each room and the room says its name.
        ///
        /// <para>The scrim thins right down for this one — it is the only beat whose content is the
        /// house itself rather than a card, so a card over the top of it would be hiding the thing
        /// it exists to show.</para>
        /// </summary>
        private IEnumerator WalkIn()
        {
            Clear();
            Scrim(new Color(0.01f, 0.02f, 0.03f, 0.25f));
            Skipper();
            var caption = Title(string.Empty, 44f, UiTheme.Paper, 70f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -360f);

            foreach (var stop in settings.RoomStops)
            {
                caption.text = stop.Key.ToUpperInvariant();
                settings.Rig?.MoveTo(stop.Value, 9f);
                yield return Hold(RoomHold);
            }

            if (settings.RoomStops.Count == 0)
            {
                caption.text = "THE HOUSE";
                yield return Hold(RoomHold);
            }
        }

        /// <summary>
        /// Hands over to the first-run tour and waits for it.
        ///
        /// <para>The overlay hides rather than closing: the tour points at HUD panels, and a
        /// full-screen scrim over the thing it is pointing at would make it useless.</para>
        /// </summary>
        private IEnumerator Tutorial()
        {
            Clear();
            if (settings.RunTutorial == null) yield break;

            group.alpha = 0f;
            group.blocksRaycasts = false;

            bool done = false;
            settings.RunTutorial(() => done = true);
            // A tour that never calls back — because it was suppressed in batchmode, or because the
            // player has seen it before — must not strand the rest of the opening behind it.
            float waited = 0f;
            while (!done && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }

            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        private IEnumerator MeetAndGreet()
        {
            Clear();
            Scrim(new Color(0.01f, 0.02f, 0.03f, 0.96f));
            Skipper();
            Title("MEET THE HOUSE", 48f, UiTheme.Gold, 70f);
            Title("First impressions, before anybody has done anything worth remembering.",
                19f, UiTheme.Muted, 44f);
            Panels();
            yield return Hold(CardHold);
        }

        // ---------------------------------------------------------------- pieces

        private void Clear()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            stage = null;
        }

        private void Scrim(Color colour)
        {
            var scrim = HudPrimitives.Fill("Scrim", transform, colour, 1);
            scrim.anchorMin = Vector2.zero;
            scrim.anchorMax = Vector2.one;
            scrim.offsetMin = Vector2.zero;
            scrim.offsetMax = Vector2.zero;
            scrim.GetComponent<Image>().raycastTarget = false;

            // Titles stack downward from here, reset for every beat so the second card does not
            // start where the first one finished.
            titleCursor = 90f;

            stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(transform, false);
            stage.anchorMin = Vector2.zero;
            stage.anchorMax = Vector2.one;
            stage.offsetMin = Vector2.zero;
            stage.offsetMax = Vector2.zero;
        }

        /// <summary>The one control on screen, on every beat, in the same place every time.</summary>
        private void Skipper()
        {
            var pill = HudPrimitives.Fill(SkipCaption, stage, UiTheme.SurfaceRaised, 18);
            pill.anchorMin = new Vector2(1f, 0f);
            pill.anchorMax = new Vector2(1f, 0f);
            pill.pivot = new Vector2(1f, 0f);
            pill.sizeDelta = new Vector2(240f, 42f);
            pill.anchoredPosition = new Vector2(-40f, 40f);
            UiTheme.AddBorder(pill, 18, UiTheme.Outline);

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 14f, UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = SkipCaption;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(Skip);
        }

        private TMP_Text Title(string value, float size, Color colour, float height)
        {
            var label = HudPrimitives.Label("Title", stage, size, colour, TextAlignmentOptions.Center);
            label.text = Localisation.Text(value);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1500f, height);
            rect.anchoredPosition = new Vector2(0f, titleCursor);
            titleCursor -= height;
            return label;
        }

        private float titleCursor;

        /// <summary>
        /// The cast, on cards that fly in from the right the way a title sequence introduces people.
        /// Wardrobe colour and name, which is what the cast grid shows before a body exists.
        /// </summary>
        private List<RectTransform> Panels()
        {
            var panels = new List<RectTransform>();
            var cast = settings.Cast.Take(8).ToList();
            if (cast.Count == 0) return panels;

            const float width = 176f;
            const float gutter = 12f;
            float x = -(cast.Count - 1) * (width + gutter) / 2f;

            foreach (var person in cast)
            {
                var wardrobe = CastPalette.For(ContentCatalog.CanonicalId(person.id));
                var panel = HudPrimitives.Fill(person.name, stage, UiTheme.Surface, 10);
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(width, 116f);
                panel.anchoredPosition = new Vector2(x, -250f);
                UiTheme.AddBorder(panel, 10, wardrobe);
                panel.GetComponent<Image>().raycastTarget = false;

                var swatch = HudPrimitives.Disc("Swatch", panel, wardrobe);
                swatch.anchorMin = new Vector2(0.5f, 1f);
                swatch.anchorMax = new Vector2(0.5f, 1f);
                swatch.pivot = new Vector2(0.5f, 1f);
                swatch.sizeDelta = new Vector2(48f, 48f);
                swatch.anchoredPosition = new Vector2(0f, -12f);
                swatch.GetComponent<Image>().raycastTarget = false;

                var name = HudPrimitives.Label("Name", panel, 14f, UiTheme.Paper, TextAlignmentOptions.Center);
                name.text = person.name;
                name.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                name.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                name.rectTransform.pivot = new Vector2(0.5f, 0f);
                name.rectTransform.sizeDelta = new Vector2(width - 12f, 40f);
                name.rectTransform.anchoredPosition = new Vector2(0f, 6f);

                panels.Add(panel);
                x += width + gutter;
            }
            return panels;
        }

        /// <summary>
        /// Falling paper, as plain rectangles in the same canvas as everything else.
        ///
        /// <para>They are given no <see cref="Button"/> and no raycast, so a piece of confetti cannot
        /// land on the skip control and eat the click that would end the sequence.</para>
        /// </summary>
        private void Confetti()
        {
            var colours = new[] { UiTheme.Gold, UiTheme.Accent, UiTheme.Positive, UiTheme.Award, UiTheme.Warning };
            for (int i = 0; i < ConfettiCount; i++)
            {
                var piece = HudPrimitives.Fill("Confetti", stage, colours[i % colours.Length], 2);
                piece.anchorMin = new Vector2(0.5f, 0.5f);
                piece.anchorMax = new Vector2(0.5f, 0.5f);
                piece.pivot = new Vector2(0.5f, 0.5f);
                piece.sizeDelta = new Vector2(UnityEngine.Random.Range(6f, 14f), UnityEngine.Random.Range(10f, 20f));
                piece.GetComponent<Image>().raycastTarget = false;
                StartCoroutine(Fall(piece, UnityEngine.Random.Range(0f, 1.4f)));
            }
        }

        private IEnumerator Fall(RectTransform piece, float delay)
        {
            float x = UnityEngine.Random.Range(-940f, 940f);
            float spin = UnityEngine.Random.Range(-220f, 220f);
            float speed = UnityEngine.Random.Range(320f, 620f);
            float y = 620f + UnityEngine.Random.Range(0f, 400f);
            piece.anchoredPosition = new Vector2(x, y);

            while (delay > 0f && piece != null) { delay -= Time.unscaledDeltaTime; yield return null; }
            while (piece != null && y > -640f)
            {
                y -= speed * Time.unscaledDeltaTime;
                x += Mathf.Sin(y * 0.01f) * 0.8f;
                piece.anchoredPosition = new Vector2(x, y);
                piece.localRotation = Quaternion.Euler(0f, 0f, piece.localRotation.eulerAngles.z + spin * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        /// <summary>Press flashes: a white sheet snapped to full and faded out, twice over.</summary>
        private IEnumerator Flashbulbs(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var flash = HudPrimitives.Fill("Flash", stage, new Color(1f, 1f, 1f, 0.85f), 1);
                flash.anchorMin = Vector2.zero;
                flash.anchorMax = Vector2.one;
                flash.offsetMin = Vector2.zero;
                flash.offsetMax = Vector2.zero;
                var image = flash.GetComponent<Image>();
                image.raycastTarget = false;

                float life = 0.22f;
                while (life > 0f && image != null)
                {
                    life -= Time.unscaledDeltaTime;
                    image.color = new Color(1f, 1f, 1f, Mathf.Max(0f, life / 0.22f) * 0.85f);
                    yield return null;
                }
                if (flash != null) Destroy(flash.gameObject);
                yield return Hold(0.12f);
            }
        }

        private IEnumerator Rise(TMP_Text label, float seconds)
        {
            var rect = label.rectTransform;
            var landing = rect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < seconds && label != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                rect.anchoredPosition = landing + new Vector2(0f, (1f - t) * -70f);
                label.alpha = t;
                yield return null;
            }
            if (label != null) { rect.anchoredPosition = landing; label.alpha = 1f; }
        }

        private IEnumerator FlyIn(RectTransform panel, float seconds)
        {
            var landing = panel.anchoredPosition;
            var from = landing + new Vector2(1400f, 0f);
            float elapsed = 0f;
            while (elapsed < seconds && panel != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                panel.anchoredPosition = Vector2.Lerp(from, landing, t * t * (3f - 2f * t));
                yield return null;
            }
            if (panel != null) panel.anchoredPosition = landing;
        }

        /// <summary>
        /// A readable pause.
        ///
        /// <para>Held even under reduced motion, and shortened to nothing in batchmode. The first is
        /// because the preference is about movement rather than about being given less time to read;
        /// the second is because an automated season must not spend ten seconds on a title card it
        /// cannot see.</para>
        /// </summary>
        /// <summary>
        /// Whether to skip the movement — the player asked for that, or nobody is watching.
        ///
        /// <para>Batchmode counts because the animations are timed against real seconds rather than
        /// frames: a title that rises over nine tenths of a second takes nine tenths of a second in a
        /// headless run too, and spends it drawing to a surface that is never presented.</para>
        /// </summary>
        private bool Motionless => settings.ReducedMotion || Application.isBatchMode;

        private IEnumerator Hold(float seconds)
        {
            if (Application.isBatchMode) yield break;
            float elapsed = 0f;
            while (elapsed < seconds) { elapsed += Time.unscaledDeltaTime; yield return null; }
        }
    }
}
