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
    public sealed class HudPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float Dip = 0.96f;
        public bool ReducedMotion;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!ReducedMotion) transform.localScale = new Vector3(Dip, Dip, 1f);
        }

        public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;
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
