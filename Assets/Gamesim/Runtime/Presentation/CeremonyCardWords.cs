namespace Gamesim.Presentation
{
    /// <summary>
    /// What a ceremony card says moves it on, in the words of the device the player is holding
    /// (UI-UX plan U5). A left click, Enter, Escape and the pad's A or B are one press on every card
    /// (the Ceremony map's Skip), so the keyboard's line names the click and Enter, and the pad's
    /// names A. Captions other code and tests identify each line by: changed here, changed there.
    ///
    /// <para>Unity-free, so the subset holds the words itself.</para>
    /// </summary>
    public static class CeremonyCardWords
    {
        /// <summary>The ceremony card's last line on a keyboard: a click or Enter takes it down.</summary>
        public const string CloseCaption = "Click or Enter  Close";

        /// <summary>The same line on a pad.</summary>
        public const string PadCloseCaption = "A  Close";

        /// <summary>The veto draw's last line on a keyboard: a click or Enter turns every chip still in the bag.</summary>
        public const string SkipToResultCaption = "Click or Enter  Skip to result";

        /// <summary>The same line on a pad.</summary>
        public const string PadSkipToResultCaption = "A  Skip to result";

        /// <summary>The card's closing line for the device last used on it.</summary>
        public static string CloseFor(bool pad) => pad ? PadCloseCaption : CloseCaption;

        /// <summary>The veto draw's line for the device last used on it.</summary>
        public static string SkipToResultFor(bool pad) => pad ? PadSkipToResultCaption : SkipToResultCaption;

        /// <summary>
        /// Whether the key ceremony shows its click line under its controls: on a keyboard and mouse,
        /// where a click is one of the presses that work; never on a pad, which has no click and whose
        /// controls line already names A.
        /// </summary>
        public static bool ShowsClickLine(bool pad) => !pad;

        /// <summary>
        /// The device a card opens in the words of: the one the house last heard a press from, when the
        /// card was handed a way to ask (the director's hints); otherwise the one it last saw itself.
        /// A card's first frames ignore input, so without this a controller player's first card of
        /// each kind would name the keyboard until the very press that closes it.
        /// </summary>
        public static bool StartsOnPad(System.Func<bool> padHints, bool lastSeen) => padHints != null ? padHints() : lastSeen;
    }
}
