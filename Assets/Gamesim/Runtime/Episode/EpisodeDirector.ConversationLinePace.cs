using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// "Conversation lines" in the Accessibility settings (UI plan D11, lead ruling L15): with "Move
    /// on", each spoken line of a conversation exchange moves on by itself once it has been up for
    /// its reading time, exactly as a press on "Continue conversation" would; with "Wait for me", the
    /// default, every line waits for the press. A press, Skip or a close still act at once, and the
    /// clock goes through the exchange's own Continue, which checks that its line is still the one
    /// showing - so it never moves a line on twice, nor after the exchange has ended, and it commits
    /// nothing. The clock is <see cref="ConversationLinePace"/>, on unscaled time; a tour or a
    /// ceremony card over the house holds it. Kept in the preferences, never in the season.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>Whether conversation lines move on by themselves ("Move on") or wait for a press ("Wait for me", the default).</summary>
        private bool autoContinueLines;

        private readonly ConversationLinePace linePace = new ConversationLinePace();

        /// <summary>The "Conversation lines" setting in use: true for "Move on".</summary>
        public bool AutoContinueLines => autoContinueLines;

        public void SetAutoContinueLines(bool moveOn)
        {
            autoContinueLines = moveOn;
            ApplyPreferences();
            Render();
        }

        /// <summary>A spoken line is drawn: a line new to the clock starts its reading time, a redraw keeps it.</summary>
        private void ShowExchangeLineOnTheClock(string session, int revision, int step, string line) =>
            linePace.Show(session, revision, step, line, captionTime, Time.unscaledTimeAsDouble);

        /// <summary>Every frame: with "Move on", the line showing moves on once its reading time is up.</summary>
        private void TickExchangeLinePace()
        {
            if (conversationExchange == null) { linePace.Clear(); return; }
            // Nobody reads the line under the tour or a ceremony's card: its time waits for them.
            if (TourIsUp || CeremonyOverlays.OnScreen) { linePace.Cover(Time.unscaledDeltaTime); return; }
            if (!autoContinueLines || !linePace.Due(Time.unscaledTimeAsDouble)) return;
            AdvanceConversationExchange(linePace.Session, linePace.Revision, linePace.Step);
        }
    }
}
