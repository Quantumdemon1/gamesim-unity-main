using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The live eviction: two nominees, the votes revealed one at a time, and the result.
    ///
    /// <para>The eviction is the only beat in the week whose outcome the player cannot already
    /// infer, and the simulation commits the whole thing in a single frame — every vote, the tally
    /// and the eviction, all at once. Reporting that as one sentence in the status line threw away
    /// the only suspense the format has. This spends it: the counts climb one vote at a time from a
    /// result that is already decided and already saved.</para>
    ///
    /// <para>Nothing here is a decision. The votes are read from committed state, so the reveal
    /// cannot disagree with the save, and skipping it — by reloading, or by the card being cancelled
    /// for a scene change — costs a presentation and never an outcome.</para>
    ///
    /// <para>Same two rules as the other overlays: no <see cref="GraphicRaycaster"/> and no
    /// raycasting graphic, and it ends on a timer rather than on acknowledgement, so an automated
    /// season is never held up behind it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoteReveal : MonoBehaviour
    {
        private const float FadeIn = 0.30f;
        private const float IntroHold = 0.85f;
        private const float PerVote = 0.45f;
        private const float ResultHold = 1.9f;
        private const float FadeOut = 0.45f;

        /// <summary>One committed ballot, as the card needs it.</summary>
        public readonly struct Ballot
        {
            public readonly string VoterName;
            public readonly string TargetId;

            public Ballot(string voterName, string targetId)
            {
                VoterName = voterName; TargetId = targetId;
            }
        }

        /// <summary>A nominee on the block.</summary>
        public readonly struct Nominee
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Texture Portrait;

            public Nominee(string id, string name, Texture portrait)
            {
                Id = id; Name = name; Portrait = portrait;
            }
        }

        private CanvasGroup group;
        private RectTransform column, scrim, dotRow, banner;
        private TMP_Text eyebrow, title, progress, bannerText;
        private readonly List<RectTransform> dots = new List<RectTransform>();
        private readonly List<TMP_Text> counts = new List<TMP_Text>();
        private readonly List<RectTransform> rims = new List<RectTransform>();
        private List<Nominee> nominees = new List<Nominee>();
        private List<Ballot> ballots = new List<Ballot>();
        private string evictedId, evictedName;
        private float elapsed;
        private int shown = -1;
        private bool playing, reduced;

        public float FontScale { get; set; } = 1f;
        public bool IsPlaying => playing;

        /// <summary>True once every ballot is on the board and the result banner is up.</summary>
        public bool ShowingResult => banner != null && banner.gameObject.activeSelf;

        /// <summary>How long a reveal of this many votes will take, start to finish.</summary>
        public float Duration => FadeIn + IntroHold + ballots.Count * PerVote + ResultHold + FadeOut;

        public static VoteReveal Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Vote Reveal",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the takeover's 100: the eviction replaces the generic ceremony card rather than
            // stacking with it.
            canvas.sortingOrder = 110;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<VoteReveal>();
        }

        /// <summary>
        /// Plays the reveal. Needs exactly two nominees and at least one ballot; anything else is
        /// a shape this card cannot narrate, and it declines rather than drawing a broken tally.
        /// </summary>
        public bool Play(int week, IList<Nominee> block, IList<Ballot> votes, string evicted, bool reducedMotion)
        {
            if (block == null || block.Count != 2 || votes == null || votes.Count == 0) return false;

            nominees = new List<Nominee>(block);
            // Reveal in a stable order, and never one that leaks the result: committed order is the
            // order the house voted, which is what a broadcast shows.
            ballots = new List<Ballot>(votes);
            evictedId = evicted;
            evictedName = nominees.Find(n => n.Id == evicted).Name;

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            shown = -1;
            playing = true;

            eyebrow.text = "WEEK " + Mathf.Max(1, week);
            banner.gameObject.SetActive(false);
            Tally(0);

            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            group.alpha = reduced ? 1f : 0f;
            return true;
        }

        public void Cancel()
        {
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;

            float revealStart = FadeIn + IntroHold;
            float revealEnd = revealStart + ballots.Count * PerVote;

            // Fade.
            if (reduced) group.alpha = 1f;
            else if (elapsed < FadeIn) group.alpha = Eased(elapsed / FadeIn);
            else if (elapsed < revealEnd + ResultHold) group.alpha = 1f;
            else
            {
                float exit = (elapsed - revealEnd - ResultHold) / FadeOut;
                if (exit >= 1f) { Cancel(); return; }
                group.alpha = 1f - Eased(exit);
            }

            if (reduced && elapsed >= Duration) { Cancel(); return; }

            // How many ballots are on the board by now.
            int target = elapsed < revealStart
                ? 0
                : Mathf.Min(ballots.Count, Mathf.FloorToInt((elapsed - revealStart) / PerVote) + 1);
            if (target != shown) Tally(target);

            if (elapsed >= revealEnd && !banner.gameObject.activeSelf) Result();
        }

        private static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Redraws the board for the first <paramref name="count"/> committed ballots.</summary>
        private void Tally(int count)
        {
            shown = count;
            for (int i = 0; i < nominees.Count; i++)
            {
                int votes = 0;
                for (int b = 0; b < count; b++) if (ballots[b].TargetId == nominees[i].Id) votes++;
                counts[i].text = votes.ToString();
            }

            for (int i = 0; i < dots.Count; i++)
            {
                dots[i].GetComponent<Image>().color = i < count ? UiTheme.Danger : UiTheme.Outline;
            }

            progress.text = count >= ballots.Count
                ? "All votes are in"
                : "Revealing vote " + Mathf.Max(1, count) + " of " + ballots.Count;
        }

        /// <summary>The result stage: the block dims apart from whoever is leaving.</summary>
        private void Result()
        {
            banner.gameObject.SetActive(true);
            bannerText.text = string.IsNullOrEmpty(evictedName)
                ? "The house has voted"
                : evictedName.ToUpperInvariant() + "  ·  EVICTED";

            for (int i = 0; i < nominees.Count; i++)
            {
                bool leaving = nominees[i].Id == evictedId;
                rims[i].GetComponent<Image>().color = leaving ? UiTheme.Danger : UiTheme.Outline;
                counts[i].color = leaving ? UiTheme.Danger : UiTheme.Muted;
            }
            progress.text = "By a vote of the house";
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            if (column != null)
            {
                foreach (Transform child in column) Destroy(child.gameObject);
                dots.Clear(); counts.Clear(); rims.Clear();
            }
            else
            {
                group = GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                scrim = HudPrimitives.Fill("Scrim", root,
                    new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.93f), 1);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;

                column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
                column.SetParent(root, false);
                column.anchorMin = new Vector2(.5f, .5f);
                column.anchorMax = new Vector2(.5f, .5f);
                column.pivot = new Vector2(.5f, .5f);
            }

            float scale = Mathf.Max(0.5f, FontScale);
            column.sizeDelta = new Vector2(880f * scale, 470f * scale);

            eyebrow = HudPrimitives.Label("Week", column, 15f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 14f;
            Place(eyebrow.rectTransform, 880f * scale, 22f * scale, 0f);

            title = HudPrimitives.Label("Title", column, 48f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            title.text = "LIVE EVICTION";
            Place(title.rectTransform, 880f * scale, 60f * scale, -26f * scale);

            // The two columns, with the tally between them.
            float slot = 300f * scale;
            float portrait = 104f * scale;
            for (int i = 0; i < nominees.Count; i++)
            {
                float x = (i == 0 ? -1f : 1f) * slot * 0.5f;

                var rim = HudPrimitives.Portrait(column, nominees[i].Portrait, UiTheme.Danger, portrait, 4f * scale, false);
                rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
                rim.anchoredPosition = new Vector2(x, -100f * scale);
                rims.Add(rim);

                var name = HudPrimitives.Label("Nominee", column, 19f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
                name.text = nominees[i].Name;
                Place(name.rectTransform, slot, 24f * scale, -(100f + portrait + 14f) * scale, x);

                var count = HudPrimitives.Label("Votes", column, 58f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
                count.text = "0";
                Place(count.rectTransform, slot, 66f * scale, -(100f + portrait + 40f) * scale, x);
                counts.Add(count);

                var caption = HudPrimitives.Label("Votes caption", column, 12f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
                caption.text = "VOTES";
                caption.characterSpacing = 8f;
                Place(caption.rectTransform, slot, 18f * scale, -(100f + portrait + 104f) * scale, x);
            }

            var versus = HudPrimitives.Label("Versus", column, 30f * scale, UiTheme.Danger, TextAlignmentOptions.Center);
            versus.text = "VS";
            Place(versus.rectTransform, 120f * scale, 40f * scale, -(100f + portrait * 0.4f) * scale);

            // One pip per committed ballot, filling as the votes come in.
            dotRow = new GameObject("Dots", typeof(RectTransform)).GetComponent<RectTransform>();
            dotRow.SetParent(column, false);
            Place(dotRow, 880f * scale, 20f * scale, -(100f + portrait + 130f) * scale);

            float pip = 11f * scale, step = 20f * scale;
            float first = -(ballots.Count - 1) * step * 0.5f;
            for (int i = 0; i < ballots.Count; i++)
            {
                var dot = HudPrimitives.Disc("Pip", dotRow, UiTheme.Outline);
                dot.anchorMin = new Vector2(.5f, .5f); dot.anchorMax = new Vector2(.5f, .5f); dot.pivot = new Vector2(.5f, .5f);
                dot.anchoredPosition = new Vector2(first + i * step, 0f);
                dot.sizeDelta = new Vector2(pip, pip);
                dots.Add(dot);
            }

            progress = HudPrimitives.Label("Progress", column, 16f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, 880f * scale, 22f * scale, -(100f + portrait + 156f) * scale);

            banner = HudPrimitives.Fill("Result banner", column, UiTheme.Danger, UiTheme.ControlRadius);
            Place(banner, 560f * scale, 44f * scale, -(100f + portrait + 186f) * scale);
            bannerText = HudPrimitives.Label("Result", banner, 22f * scale, UiTheme.Ink, TextAlignmentOptions.Center);
            bannerText.rectTransform.anchorMin = Vector2.zero;
            bannerText.rectTransform.anchorMax = Vector2.one;
            bannerText.rectTransform.offsetMin = Vector2.zero;
            bannerText.rectTransform.offsetMax = Vector2.zero;
            banner.gameObject.SetActive(false);
        }

        private static void Place(RectTransform rect, float width, float height, float y, float x = 0f)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
