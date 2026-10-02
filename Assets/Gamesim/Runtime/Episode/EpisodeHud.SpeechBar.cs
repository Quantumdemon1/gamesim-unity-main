using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>The bar's name, so a test can find it the way it finds a named panel.</summary>
        public const string SpeechBarName = "Speech bar";

        /// <summary>The player as the bar names them: their name and "(You)", once.</summary>
        public static string SelfTitle(ContestantState self) => HudPrimitives.WithYou(self?.name, true);

        /// <summary>
        /// The mockups' bar at the foot of the frame (06, 07, 08): the chat mark, a face in a ring,
        /// a bold first line and a second under it. Here it is always the player's own face and
        /// something true of them on this screen. The game writes the player no inner lines, and a
        /// status message is not always speech ("You and Maya Hassan talked about the game." is
        /// the line's account of a conversation, not anybody's words), so nothing is dressed up as
        /// a quote.
        ///
        /// <para>In the strip's band when <paramref name="inStripBand"/> - mockups 07 and 08 draw
        /// no strip there - up to the quote card when it is showing; otherwise in the status line's
        /// band, over it, as mockup-06 puts its bar above the room.</para>
        /// </summary>
        public RectTransform SpeechBar(string contestantId, string title, string line, bool inStripBand)
        {
            if (canvas == null || string.IsNullOrEmpty(line)) return null;
            float s = FontScale;
            var root = (RectTransform)canvas.transform;
            float canvasWidth = root.rect.width > 0 ? root.rect.width : 1600f;
            var bar = Chrome(SpeechBarName, canvas.transform);
            bar.GetComponent<Image>().raycastTarget = false;

            float height, width;
            if (inStripBand)
            {
                SetChromeVisible(CastRail.RootName, false);
                SetChromeVisible(CastRail.StripName, false);
                // Up to the quote card where it is showing, as mockup-08 stops its bar short of it.
                // The card sits under the strip's ground, not on the canvas itself.
                float right = canvasWidth - RightColumnInset;
                foreach (var item in canvas.GetComponentsInChildren<RectTransform>())
                {
                    if (item.name != CastRail.QuoteName) continue;
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, item);
                    right = Mathf.Min(right, bounds.min.x + canvasWidth * root.pivot.x - 12f);
                }
                height = (CastRail.Height - 14f) * s;
                width = right - 16f;
                Anchor(bar, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, CastRail.Bottom + 6f * s), new Vector2(width, height));
            }
            else
            {
                height = StatusHeight * s + 18f * s;
                width = Mathf.Min(900f * s, canvasWidth - 2f * HelpGutter);
                Anchor(bar, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, StatusBottom - 9f * s), new Vector2(width, height));
            }

            float x = 18f;
            var chat = UiTheme.Pack(PackArt.IconChat);
            if (chat != null)
            {
                var mark = new GameObject("Speech mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(bar, false);
                Anchor(mark.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(x, 0f), new Vector2(30f * s, 30f * s));
                mark.sprite = chat; mark.color = UiTheme.Heading; mark.preserveAspect = true; mark.raycastTarget = false;
                x += 44f * s;
            }
            var state = director != null ? director.Snapshot : null;
            var who = state != null ? state.Find(contestantId) : null;
            if (who != null)
            {
                float side = Mathf.Min(64f * s, height - 16f * s);
                var rim = HudPrimitives.Portrait(bar, Portrait(who.id), UiTheme.Accent, side, 3f * s, false, who);
                rim.gameObject.name = "Speech face";
                rim.anchorMin = rim.anchorMax = new Vector2(0f, .5f);
                rim.pivot = new Vector2(0f, .5f);
                rim.anchoredPosition = new Vector2(x, 0f);
                x += rim.sizeDelta.x + 16f * s;
            }
            float textWidth = Mathf.Max(120f, width - x - 20f);
            var heading = FixedText(bar, title ?? string.Empty, 18, Paper, Vector2.zero, new Vector2(textWidth, 26f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
            Anchor(heading.rectTransform, new Vector2(0f, .5f), new Vector2(0f, 0f), new Vector2(x, 1f * s), new Vector2(textWidth, 26f * s));
            AutoSize(heading, 13);
            var words = FixedText(bar, line, 15, new Color(Paper.r, Paper.g, Paper.b, .86f), Vector2.zero, new Vector2(textWidth, 24f * s));
            Anchor(words.rectTransform, new Vector2(0f, .5f), new Vector2(0f, 1f), new Vector2(x, -1f * s), new Vector2(textWidth, 24f * s));
            AutoSize(words, 11);
            return bar;
        }
    }
}
