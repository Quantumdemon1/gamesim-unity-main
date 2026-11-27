using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The "Conversation lines" setting (UI plan D11, lead ruling L15) and the clock it keeps. With
    /// "Wait for me", the default, each spoken line of a conversation exchange waits for a press on
    /// "Continue conversation", as it always has; with "Move on", a line moves on by itself once it
    /// has been up for its reading time - <see cref="CaptionTiming.Hold"/> at the caption time in use,
    /// so the "Longer" setting gives it half as long again. A press, Skip or a close still act at
    /// once. The setting is kept in the preferences under its own key, never in a season, and the
    /// clock only ever presses the exchange's own Continue, which commits nothing.
    ///
    /// <para>The clock belongs to one line: the session, revision and step that drew it. A redraw of
    /// the same line keeps its clock; a new line (a press, or the clock itself, moved the exchange on)
    /// starts its own; a line the clock has moved on never comes due again. Reduced motion leaves it
    /// alone: it is reading time, not motion.</para>
    ///
    /// <para>Unity-free, so the subset holds these rules.</para>
    /// </summary>
    public sealed class ConversationLinePace
    {
        /// <summary>The preference the setting is kept under: 1 moves the lines on, anything else waits for a press.</summary>
        public const string Key = "Gamesim.AutoContinueLines";

        /// <summary>The setting a store's record names: 1 is "Move on", anything else (and no record) "Wait for me".</summary>
        public static bool FromRecord(int record) => record == 1;

        /// <summary>What the store keeps for the setting: 1 for "Move on", 0 for "Wait for me".</summary>
        public static int Record(bool moveOn) => moveOn ? 1 : 0;

        /// <summary>The setting's word in the player log's settings lines.</summary>
        public static string Name(bool moveOn) => moveOn ? "move-on" : "wait";

        /// <summary>
        /// How long a spoken line stays before it moves on by itself: its reading time at the caption
        /// time in use. No exchange line is authored a hold of its own.
        /// </summary>
        public static double Hold(string line, CaptionTime time) => CaptionTiming.Hold(0.0, line, time);

        private bool hasLine;

        /// <summary>The line on the clock: the session, revision and step of the exchange that drew it.</summary>
        public string Session { get; private set; }
        public int Revision { get; private set; }
        public int Step { get; private set; }

        /// <summary>When the line on the clock moves on, in the clock's seconds.</summary>
        public double DueAt { get; private set; }

        /// <summary>Whether a line is on the clock and has not yet moved on.</summary>
        public bool Armed { get; private set; }

        /// <summary>
        /// A line is drawn. A line new to the clock starts its reading time at <paramref name="now"/>;
        /// the line already on it keeps its clock, and once moved on stays moved on.
        /// </summary>
        public void Show(string session, int revision, int step, string line, CaptionTime time, double now)
        {
            if (hasLine && string.Equals(session, Session, StringComparison.Ordinal) && revision == Revision && step == Step) return;
            hasLine = true;
            Session = session;
            Revision = revision;
            Step = step;
            DueAt = now + Hold(line, time);
            Armed = true;
        }

        /// <summary>Something covered the line for <paramref name="seconds"/>: nobody read it then, so it waits that much longer.</summary>
        public void Cover(double seconds)
        {
            if (Armed && seconds > 0.0) DueAt += seconds;
        }

        /// <summary>True once, on the first call at or past the line's time; never again for that line.</summary>
        public bool Due(double now)
        {
            if (!Armed || now < DueAt) return false;
            Armed = false;
            return true;
        }

        /// <summary>The exchange is over: no line is on the clock.</summary>
        public void Clear()
        {
            hasLine = false;
            Armed = false;
            Session = null;
            Revision = 0;
            Step = 0;
            DueAt = 0.0;
        }
    }
}
