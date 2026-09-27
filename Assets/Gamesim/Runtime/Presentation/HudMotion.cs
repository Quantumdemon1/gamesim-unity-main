using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A button that dips while the pointer is down on it. Zero cost between presses; nothing under
    /// reduced motion. Scale only, so the caption, the focus ring and the click are untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        private const float Dip = 0.96f;
        public bool ReducedMotion;
        /// <summary>Called when the pointer arrives over the control: the HUD plays the hover foley.</summary>
        public System.Action Hovered;

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!ReducedMotion) transform.localScale = new Vector3(Dip, Dip, 1f);
        }

        public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Lifts a panel's edge one level while the pointer is over it or the keyboard is on it, and
    /// puts it back afterwards. Resting becomes interactive; interactive becomes the accent.
    ///
    /// <para>An edge rather than a colour multiplier. The dial's petals carry their action's own
    /// colour on a glyph, and a multiplier tints whatever it is given - the old highlight was
    /// weighted green, so every hovered petal drifted green and the colour coding stopped meaning
    /// anything exactly when the player was looking at it.</para>
    ///
    /// <para>Visual only: no layout, no focus movement, nothing a rebuild can race. It reads the
    /// Border child that <see cref="UiTheme.AddBorder"/> makes, and does nothing without one.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudEmphasis : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private Image edge;
        private Color resting, promoted;
        private bool hovered, focused;

        /// <summary>Gives <paramref name="panel"/> a hover and focus step up from its resting level.</summary>
        public static void Promote(RectTransform panel, UiTheme.Emphasis level)
        {
            if (panel == null) return;
            var child = panel.Find("Border");
            var image = child != null ? child.GetComponent<Image>() : null;
            if (image == null) return;
            var component = panel.gameObject.GetComponent<HudEmphasis>();
            if (component == null) component = panel.gameObject.AddComponent<HudEmphasis>();
            component.edge = image;
            component.resting = UiTheme.Edge(level);
            component.promoted = UiTheme.Edge(level == UiTheme.Emphasis.Resting
                ? UiTheme.Emphasis.Interactive : UiTheme.Emphasis.Active);
            component.Apply();
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; Apply(); }
        public void OnSelect(BaseEventData eventData) { focused = true; Apply(); }
        public void OnDeselect(BaseEventData eventData) { focused = false; Apply(); }

        private void Apply()
        {
            if (edge != null) edge.color = hovered || focused ? promoted : resting;
        }
    }

    /// <summary>
    /// A meter's fill travelling from where it was to where it is now, so a budget that just spent
    /// an action is seen draining rather than found smaller. Ease-out, a quarter of a second,
    /// unscaled time; removes itself when it lands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudFill : MonoBehaviour
    {
        private const float Duration = 0.25f;
        private RectTransform rect;
        private float from, to, elapsed;

        public void Play(float fromFraction, float toFraction)
        {
            rect = (RectTransform)transform;
            from = fromFraction; to = toFraction; elapsed = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (rect == null) { Destroy(this); return; }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            Apply(1f - (1f - t) * (1f - t));
            if (t >= 1f) Destroy(this);
        }

        private void Apply(float eased)
        {
            var max = rect.anchorMax;
            max.x = Mathf.Lerp(from, to, eased);
            rect.anchorMax = max;
        }
    }

    /// <summary>
    /// The fade a closing panel gets. The HUD rebuilds its canvas on every render, so the panel
    /// that is closing is handed to a ghost canvas of its own - same scaler, same sorting - and
    /// fades out there while the real canvas is already showing what comes next.
    ///
    /// <para>The ghost owns no controls: every selectable is removed on the spot and the group
    /// blocks no raycasts, so nothing behind it, no keyboard ring and no test counting buttons can
    /// take it for a live panel. Under reduced motion no ghost is made at all.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudFade : MonoBehaviour
    {
        public const string GhostName = "Gamesim HUD ghost";
        private const float Duration = 0.14f;
        private const float Drop = 8f;
        private CanvasGroup group;
        private RectTransform panel;
        private Vector2 rest;
        private float elapsed;

        public static void Ghost(RectTransform panel, Canvas source, bool reducedMotion)
        {
            if (panel == null || source == null || reducedMotion) return;
            var host = new GameObject(GhostName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = source.renderMode;
            canvas.sortingOrder = source.sortingOrder;
            var scaler = host.GetComponent<CanvasScaler>();
            var from = source.GetComponent<CanvasScaler>();
            if (from != null)
            {
                scaler.uiScaleMode = from.uiScaleMode;
                scaler.referenceResolution = from.referenceResolution;
                scaler.screenMatchMode = from.screenMatchMode;
                scaler.matchWidthOrHeight = from.matchWidthOrHeight;
            }
            var group = host.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            panel.SetParent(host.transform, false);
            // At the end of the frame, not now: the control that closed the panel is often still
            // inside its own press - Button.OnSubmit checks IsActive after invoking onClick - and
            // an object destroyed under it throws. Until then the group above makes it inert.
            foreach (var selectable in host.GetComponentsInChildren<Selectable>(true)) { selectable.interactable = false; Destroy(selectable); }
            foreach (var reveal in host.GetComponentsInChildren<HudReveal>(true)) Destroy(reveal);
            var fade = host.AddComponent<HudFade>();
            fade.panel = panel;
        }

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (panel != null) rest = panel.anchoredPosition;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float eased = t * t;
            if (group != null) group.alpha = 1f - eased;
            if (panel != null) panel.anchoredPosition = rest + new Vector2(0f, -Drop * eased);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
