using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The one way out of a screen, in words and measures (menu slice 2, the review's change 1).
    ///
    /// <para>A screen the player went to - it replaced what they were on - is left by "Back to
    /// &lt;place&gt;" at its top left, the place being the plain name of where leaving really returns.
    /// A panel, page or card over the place the player is on is left by "Close" at its top right. A
    /// page reached from another page inside a panel has both. Escape and the pad's B do what that
    /// control does; the screen wires them, as it wires the control's action.</para>
    ///
    /// <para>This half is plain C#, so the Unity-free suite reads the same words the screens draw.
    /// The builders that put the control on a frame are in <c>WayOut.Build.cs</c>. Nothing here
    /// navigates: the screen passes the action.</para>
    ///
    /// <para>A caption is a contract: tests, screen readers and the standalone verify find a control
    /// by it. Decorate around it (the key hint is its own label), never append to it.</para>
    /// </summary>
    public static partial class WayOut
    {
        /// <summary>The caption of the one control that closes a panel, page or card over the place the player is on.</summary>
        public const string CloseCaption = "Close";

        /// <summary>How every way back begins. Followed by a place, never by nothing.</summary>
        public const string BackPrefix = "Back to ";

        /// <summary>The object name of the way back the helper draws, whatever place its caption names.</summary>
        public const string BackObjectName = "Way back";

        /// <summary>The object name of the Close the helper draws.</summary>
        public const string CloseObjectName = "Way out: Close";

        /// <summary>The object name of the key hint drawn inside a way out ("Esc"), apart from its caption.</summary>
        public const string KeyHintObjectName = "Key hint";

        // The places, each spelled once. A page's own place ("the briefing", "free time") is a
        // constant in the file that owns the page; these are the places more than one screen returns to.

        /// <summary>The front door's first screen.</summary>
        public const string MainMenu = "the main menu";
        /// <summary>The cast screen, where a season's houseguests are chosen.</summary>
        public const string Cast = "the cast";
        /// <summary>The house the season is played in, with no panel open.</summary>
        public const string House = "the house";

        /// <summary>"Back to &lt;place&gt;". The place is the plain name of where leaving returns, never empty.</summary>
        public static string BackTo(string place)
        {
            if (string.IsNullOrWhiteSpace(place)) throw new ArgumentException("A way back names its place.", nameof(place));
            return BackPrefix + place.Trim();
        }

        /// <summary>
        /// "Back to &lt;place&gt; (&lt;kept&gt; kept)": the way back where leaving keeps something, said
        /// truthfully - "Back to the cast (draft kept)". <paramref name="kept"/> is the thing kept ("draft").
        /// </summary>
        public static string BackToKeeping(string place, string kept)
        {
            if (string.IsNullOrWhiteSpace(kept)) throw new ArgumentException("Say what is kept.", nameof(kept));
            return BackTo(place) + " (" + kept.Trim() + " kept)";
        }

        /// <summary>Whether <paramref name="caption"/> is worded as a way back: "Back to " and a place.</summary>
        public static bool IsWayBack(string caption) =>
            caption != null && caption.StartsWith(BackPrefix, StringComparison.Ordinal) && caption.Length > BackPrefix.Length
            && !string.IsNullOrWhiteSpace(caption.Substring(BackPrefix.Length));

        // ------------------------------------------------------------------ measures

        /// <summary>The caption's size at standard text, in the screen's units.</summary>
        public const float BaseFontSize = 18f;
        /// <summary>No way out's caption is drawn smaller than this, at any text size.</summary>
        public const float MinFontSize = 14f;
        /// <summary>The control's height at standard text: a comfortable pointer target.</summary>
        public const float BaseHeight = 44f;
        /// <summary>The gap between the frame's corner and the control, at standard text.</summary>
        public const float BaseInset = 16f;
        /// <summary>The back arrow's side, and its gap to the caption, at standard text.</summary>
        public const float BaseIcon = 20f;
        /// <summary>A label box is at least this many times its font tall (Inter draws nothing in a shorter box).</summary>
        public const float LabelBoxRatio = 1.3f;

        /// <summary>The caption's font size at a text scale (1, 1.2 or 1.4).</summary>
        public static float FontSize(float textScale) => Math.Max(MinFontSize, BaseFontSize * Scale(textScale));

        /// <summary>The control's height at a text scale: never shorter than its label box needs.</summary>
        public static float Height(float textScale) => Math.Max(BaseHeight * Scale(textScale), FontSize(textScale) * LabelBoxRatio + 8f);

        /// <summary>The gap to the frame's corner at a text scale.</summary>
        public static float Inset(float textScale) => BaseInset * Scale(textScale);

        /// <summary>
        /// The control's width: wide enough for the whole caption at its size (a generous estimate
        /// of Inter's widest run, so it is never cut), its padding, the arrow when it has one, and
        /// the key hint when it shows one.
        /// </summary>
        public static float Width(string caption, float textScale, bool withIcon, string keyHint = null)
        {
            float s = Scale(textScale), font = FontSize(textScale);
            float words = (caption ?? string.Empty).Length * font * 0.6f;
            float pad = 2f * 14f * s;
            float icon = withIcon ? (BaseIcon + 10f) * s : 0f;
            float hint = string.IsNullOrEmpty(keyHint) ? 0f : keyHint.Length * MinFontSize * Math.Max(1f, s) * 0.62f + 14f * s;
            return (float)Math.Ceiling(words + pad + icon + hint);
        }

        private static float Scale(float textScale) => textScale > 0f ? textScale : 1f;
    }
}
