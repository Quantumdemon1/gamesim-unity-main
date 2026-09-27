using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A short fade-and-rise for a HUD element that has just appeared.
    ///
    /// The episode HUD tears its canvas down and rebuilds it on every render, so this is attached
    /// only on a genuine transition — a panel that was closed and is now open, or a status line
    /// whose text actually changed. Replaying it on every rebuild would be worse than no motion.
    ///
    /// It animates alpha and position only, never <c>interactable</c>, so the keyboard focus and
    /// navigation wiring keeps working while the element is still fading in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudReveal : MonoBehaviour
    {
        private const float Duration = 0.16f;

        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 restPosition;
        private bool restKnown;
        private float rise;
        private float elapsed;

        /// <summary>Reveals a freshly built element. Does nothing when motion is reduced.</summary>
        public static void Play(RectTransform target, bool reducedMotion, float rise = 14f)
        {
            if (target == null || reducedMotion) return;
            var reveal = target.gameObject.AddComponent<HudReveal>();
            reveal.rise = rise;
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            // Hidden now; moved only once its place is known. The panel is revealed the moment it
            // is built, and an activity's layout moves it after that, in the same render - so a
            // rest position read here was the docked panel's, and the reveal carried every
            // activity panel (the settings, the diary, the bands) back to it: the settings column
            // landed at the canvas's left edge, the conversation's frame on top of the rail.
            group.alpha = 0f;
        }

        /// <summary>Where the element settles: read on its first frame, after its layout has run.</summary>
        private void Settle()
        {
            if (restKnown) return;
            restPosition = rect.anchoredPosition;
            restKnown = true;
        }

        private void Update()
        {
            Settle();
            elapsed += Time.unscaledDeltaTime;
            float t = Duration <= 0f ? 1f : Mathf.Clamp01(elapsed / Duration);
            Apply(t);
            if (t < 1f) return;
            rect.anchoredPosition = restPosition;
            group.alpha = 1f;
            Destroy(this);
        }

        /// <summary>
        /// Lands the reveal now, as though its 160 ms had already passed.
        ///
        /// <para>For a caller that needs the element settled rather than arriving - the same state
        /// the reduced-motion path produces by never animating at all. A reveal begins at alpha 0,
        /// and a canvas group at alpha 0 is culled, so anything inside it takes no raycasts and is
        /// not merely invisible but genuinely unpressable until it lands.</para>
        /// </summary>
        public void Finish()
        {
            if (rect == null || group == null) return;
            Settle();
            elapsed = Duration;
            rect.anchoredPosition = restPosition;
            group.alpha = 1f;
            Destroy(this);
        }

        private void Apply(float t)
        {
            // Ease out: fast to settle, so the panel feels responsive rather than animated at.
            float eased = 1f - (1f - t) * (1f - t);
            group.alpha = eased;
            rect.anchoredPosition = restPosition + new Vector2(0f, (1f - eased) * -rise);
        }
    }
}
