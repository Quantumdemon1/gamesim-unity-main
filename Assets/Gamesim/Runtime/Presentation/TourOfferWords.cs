namespace Gamesim.Presentation
{
    /// <summary>
    /// The tour's new words: the offer a returning player gets instead of the tour itself, and the
    /// line on every card that says how to take it again. Plain text, no engine, so the Unity-free
    /// suite reads them. The word is "tour" wherever the player reads it, never "tutorial".
    /// </summary>
    public static class TourOfferWords
    {
        /// <summary>The offer card's heading.</summary>
        public const string OfferTitle = "Take the tour?";

        /// <summary>The offer card's one line under its heading.</summary>
        public const string OfferBody = "A quick walk through the house's screens and controls, one step at a time.";

        /// <summary>The offer's yes: the tour from its first step. A caption tests and screen readers find it by.</summary>
        public const string TakeCaption = "Take the tour";

        /// <summary>The offer's no: the season goes on without the tour.</summary>
        public const string NotNowCaption = "Not now";

        /// <summary>
        /// How to take the tour again, on every card of the tour. Both ways are true: once the tour
        /// has been finished or skipped, each new season opens with the offer, and the house's
        /// Settings carry a "Replay the tour" control.
        /// </summary>
        public const string ReplayNote = "A new season offers the tour again, or replay it from Settings in the house.";

        /// <summary>
        /// Whether a season's opening asks first rather than starting the tour: once the player has
        /// been through it (or skipped it), it is offered, not replayed. A first-time player gets the
        /// tour itself.
        /// </summary>
        public static bool OpensWithOffer(bool seenBefore) => seenBefore;
    }
}
