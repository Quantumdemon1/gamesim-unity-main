using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A seven-step first-run tour, each step parked beside the thing it explains.
    ///
    /// <para>This is the one overlay in the project that deliberately takes input. Every other card
    /// refuses a raycaster and dismisses on a timer, because a broadcast graphic that swallowed a
    /// click would be worse than no graphic. A tutorial is the opposite: it is a conversation, it
    /// has to wait, and a player must be able to leave it at any point.</para>
    ///
    /// <para>That makes it the one overlay that could strand an automated season, so it is off
    /// unless asked for. The director shows it on a genuine first run only, and never in batchmode.
    /// <see cref="Show"/> is public so a test can drive and photograph it without pretending to be a
    /// first run.</para>
    ///
    /// <para><b>It is also the only change in this pass that could alter what a playtest measures.</b>
    /// Section E2 of the acceptance matrix asks whether three first-time players reach the eviction
    /// unaided, and a guided tour is precisely the intervention that criterion exists to test the
    /// absence of. Run E2 with the tour off, or record that it was on. Do not run it both ways and
    /// report the better number.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseTutorial : MonoBehaviour
    {
        public const string SeenKey = "Gamesim.TutorialSeen";
        private const float CardWidth = 330f;
        private const float CardHeight = 178f;

        /// <summary>One step: what it says, and which piece of chrome it stands beside.</summary>
        private readonly struct Step
        {
            public readonly string Anchor, Title, Body;

            public Step(string anchor, string title, string body)
            {
                Anchor = anchor; Title = title; Body = body;
            }
        }

        private static readonly Step[] Steps =
        {
            new Step(null, "Welcome to the house",
                "You are living here with the rest of the house. One leaves every week. Here is how to play."),
            new Step("Cast rail", "The cast",
                "Everyone still in the game, top to bottom. The badge under a face is what they hold this week."),
            new Step("House pill", "The week",
                "How many are left, and who is Head of Household. The Head decides the nominations."),
            new Step("Objective", "Your next step",
                "This panel always names the one thing the house is waiting on. Follow it when you are unsure."),
            new Step("Exploration controls", "Moving around",
                "Click the floor to walk. Stand near a houseguest and press E to talk to them."),
            new Step("Navigation", "Your notebook",
                "Everything your character knows: who trusts you, what you promised, and how the house voted."),
            new Step("Status", "The episode",
                "When the house is ready, the living-room screen runs the ceremony. Watch this line for what just happened."),
        };

        private Canvas canvas;
        private RectTransform card;
        private TMP_Text counter, title, body;
        private Button next, skip;
        private int step = -1;
        private Func<string, RectTransform> anchorLookup;

        /// <summary>Matches the HUD's accessibility preference.</summary>
        public float FontScale { get; set; } = 1f;

        public bool IsShowing => step >= 0;
        public int StepIndex => step;
        public int Count => Steps.Length;

        /// <summary>True when this player has already been through the tour, or skipped it.</summary>
        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

        public static HouseTutorial Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Tutorial",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        /// Starts the tour. <paramref name="anchors"/> resolves a chrome panel by name so the card
        /// can stand beside it; the HUD owns those rects and rebuilds them constantly, so they are
        /// looked up per step rather than captured once.
        /// </summary>
        public void Show(Func<string, RectTransform> anchors)
        {
            anchorLookup = anchors;
            Build();
            step = 0;
            canvas.gameObject.SetActive(true);
            Render();
        }

        public void Next()
        {
            if (step < 0) return;
            step++;
            if (step >= Steps.Length) { Finish(); return; }
            Render();
        }

        /// <summary>Leaves the tour and remembers that, so it does not reappear next launch.</summary>
        public void Skip() => Finish();

        private void Finish()
        {
            step = -1;
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void Render()
        {
            var current = Steps[step];
            counter.text = (step + 1) + " of " + Steps.Length;
            title.text = current.Title;
            body.text = current.Body;

            var label = next.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = step == Steps.Length - 1 ? "Got it" : "Next";

            Place(current.Anchor);
        }

        /// <summary>
        /// Parks the card beside its subject, clamped inside the screen.
        ///
        /// <para>Anchored chrome moves with the aspect ratio, so the card is positioned from the
        /// panel's live rect rather than from a remembered coordinate. A step whose panel is absent
        /// — the HUD hides some of it during a modal — centres instead of pointing at nothing.</para>
        /// </summary>
        private void Place(string anchorName)
        {
            float scale = Mathf.Max(0.5f, FontScale);
            var size = new Vector2(CardWidth * scale, CardHeight * scale);
            card.sizeDelta = size;

            var parent = (RectTransform)canvas.transform;
            var extent = parent.rect.size;

            Vector2 position;
            var target = string.IsNullOrEmpty(anchorName) || anchorLookup == null
                ? null : anchorLookup(anchorName);

            if (target == null)
            {
                position = Vector2.zero;
            }
            else
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                var centre = (Vector2)((corners[0] + corners[2]) * 0.5f) - extent * 0.5f;
                bool onLeft = centre.x < 0f;
                // Beside the panel on the roomier side, and below it, so the card never covers the
                // thing it is describing.
                float half = (corners[2].x - corners[0].x) * 0.5f;
                position = new Vector2(
                    centre.x + (onLeft ? half + size.x * 0.5f + 16f : -(half + size.x * 0.5f + 16f)),
                    centre.y - size.y * 0.35f);
            }

            float limitX = (extent.x - size.x) * 0.5f - 12f;
            float limitY = (extent.y - size.y) * 0.5f - 12f;
            card.anchoredPosition = new Vector2(
                Mathf.Clamp(position.x, -limitX, limitX),
                Mathf.Clamp(position.y, -limitY, limitY));
        }

        private void Build()
        {
            if (card != null) return;
            var root = (RectTransform)canvas.transform;

            card = HudPrimitives.Fill("Tutorial card", root, UiTheme.SurfaceRaised, UiTheme.PanelRadius);
            card.anchorMin = new Vector2(.5f, .5f);
            card.anchorMax = new Vector2(.5f, .5f);
            card.pivot = new Vector2(.5f, .5f);
            card.GetComponent<Image>().raycastTarget = true;
            UiTheme.AddBorder(card, UiTheme.PanelRadius, UiTheme.Accent);

            float scale = Mathf.Max(0.5f, FontScale);

            counter = HudPrimitives.Label("Counter", card, 13f * scale, UiTheme.Muted);
            Corner(counter.rectTransform, new Vector2(16f, -12f), new Vector2(120f, 18f), scale);

            title = HudPrimitives.Label("Title", card, 21f * scale, UiTheme.Paper);
            Corner(title.rectTransform, new Vector2(16f, -32f), new Vector2(CardWidth - 32f, 28f), scale);

            body = HudPrimitives.Label("Body", card, 15f * scale, UiTheme.Muted);
            Corner(body.rectTransform, new Vector2(16f, -62f), new Vector2(CardWidth - 32f, 66f), scale);

            skip = TextButton("Skip tutorial", card, UiTheme.Muted, 14f * scale);
            Corner(skip.GetComponent<RectTransform>(), new Vector2(12f, -(CardHeight - 40f)), new Vector2(120f, 30f), scale);
            skip.onClick.AddListener(Skip);

            next = TextButton("Next", card, UiTheme.Ink, 15f * scale, UiTheme.Gold);
            Corner(next.GetComponent<RectTransform>(), new Vector2(CardWidth - 110f, -(CardHeight - 40f)), new Vector2(98f, 32f), scale);
            next.onClick.AddListener(Next);
        }

        private static void Corner(RectTransform rect, Vector2 offset, Vector2 size, float scale)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset * scale;
            rect.sizeDelta = size * scale;
        }

        private static Button TextButton(string caption, Transform parent, Color ink, float size, Color? fill = null)
        {
            var rect = fill.HasValue
                ? HudPrimitives.Fill(caption, parent, fill.Value, UiTheme.ControlRadius)
                : HudPrimitives.Fill(caption, parent, new Color(0f, 0f, 0f, 0f), UiTheme.ControlRadius);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Text", rect, size, ink, TextAlignmentOptions.Center);
            label.text = caption;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }
    }
}
