namespace Gamesim.Presentation
{
    /// <summary>
    /// When a competition's clock ticks in its last seconds (AU-08): each time its digits come to
    /// another whole second inside the last five. The tick is news (<c>CountTick</c> in
    /// <see cref="SoundPictures"/>), so it is read from the digits the clock shows rather than from
    /// the time under them: "5.0" shows at 5.04 seconds left, before the time itself is down to 5,
    /// and a tick read from the time would sound in a frame whose digits had not moved. Unity-free.
    /// </summary>
    public static class CountTicks
    {
        /// <summary>The last seconds that tick: the five the clock calls its final seconds.</summary>
        public const int FinalSeconds = 5;

        /// <summary>
        /// The whole second a clock's digits stand in, counting down: "5.0" is the fifth-last second,
        /// "4.9" to "4.1" still the fifth, "4.0" the fourth, "0.0" none. Read from every digit in the
        /// text, so the point between them may be either a point or a comma; -1 for text with no digit.
        /// </summary>
        public static int WholeSecondShown(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return -1;
            long tenths = 0;
            bool any = false;
            foreach (char c in digits)
            {
                if (c < '0' || c > '9') continue;
                tenths = tenths * 10 + (c - '0');
                any = true;
                if (tenths > int.MaxValue) return int.MaxValue;
            }
            return any ? (int)((tenths + 9) / 10) : -1;
        }

        /// <summary>
        /// Whether the clock ticks now: it shows a whole second inside the last five that is not the
        /// one it last ticked on (<paramref name="lastTicked"/>, -1 for none yet this attempt).
        /// </summary>
        public static bool Ticks(int shown, int lastTicked) => shown >= 1 && shown <= FinalSeconds && shown != lastTicked;
    }
}
