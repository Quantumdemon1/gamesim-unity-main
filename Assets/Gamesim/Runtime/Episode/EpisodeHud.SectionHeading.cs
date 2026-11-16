using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>A section heading's box and its rule, so a test finds them the way it finds a named panel.</summary>
        public const string SectionHeadingName = "Section heading", SectionRuleName = "Section rule";

        /// <summary>The section heading's type size: the smallest a label may be.</summary>
        public const int SectionHeadingSize = 14;

        /// <summary>
        /// The one heading style for a section of a screen (the menu review's section 3): small
        /// letterspaced capitals in the theme's heading blue with a soft rule under them. Settings
        /// used three - the quiet 13-point section label, a 20-point heading and a capitalised
        /// 20-point heading - for sections of the same rank. The words are kept as given: the
        /// capitals are the style's, so the caption a test reads is the one passed in.
        /// </summary>
        public void SectionHeading(string value)
        {
            var box = new GameObject(SectionHeadingName, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            box.SetParent(content, false);
            var stack = box.GetComponent<VerticalLayoutGroup>();
            stack.spacing = 4; stack.padding = new RectOffset(0, 0, 6, 0);
            stack.childControlWidth = true; stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;

            var text = NewText(box, value, SectionHeadingSize, UiTheme.Heading);
            text.fontStyle |= FontStyles.UpperCase;
            text.characterSpacing = 4f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) text.font = semibold;
            // At least 1.3 times the type: Inter draws nothing in a box much under 1.21.
            text.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.Ceil(text.fontSize * 1.3f) + 2f;

            var rule = new GameObject(SectionRuleName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            rule.rectTransform.SetParent(box, false);
            rule.raycastTarget = false;
            rule.color = new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, .35f);
            var line = rule.gameObject.AddComponent<LayoutElement>();
            line.minHeight = 1f; line.preferredHeight = 1f;
        }
    }
}
