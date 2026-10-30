using Gamesim.Presentation;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The frame a houseguest's ask opens in (OFFERS-COME-TO-YOU-PLAN §C.3): the eyebrow, who is asking, where the
    /// player stands with them, the line they said, then the terms under "THEIR OFFER" and how long the offer stands.
    /// The owner draws the answers under it - the deal table's own Accept and Turn down, which register as the offer's
    /// controls - and the way past it. It reads nothing and commits nothing.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The walk-over dialogue's way out that answers nothing. No other control says it.</summary>
        public const string ThinkItOverCaption = "Think it over";
        /// <summary>The heading over the terms, the words the invited view has always used.</summary>
        public const string AskOfferHeading = "THEIR OFFER";
        /// <summary>The card's eyebrow, when the houseguest could not come over (slice 2).</summary>
        public const string AskDirectEyebrow = "A WORD IN PRIVATE";
        /// <summary>What the card says once the offer has gone (slice 2).</summary>
        public const string AskGoneLine = "That offer is no longer on the table.";
        /// <summary>The names a test finds the dialogue's lines by.</summary>
        public const string AskLineName = "Ask spoken line", AskStandingName = "Ask standing", AskTermsName = "Ask terms",
                            AskLapseName = "Ask lapse line", AskMoreName = "Ask more waiting",
                            AskYourWordsName = "Ask your words", AskReplyName = "Ask their reply";

        /// <summary>The walk-over's eyebrow: "MAYA PULLS YOU ASIDE".</summary>
        public static string AskWalkOverEyebrow(string first) => (first ?? "").ToUpperInvariant() + " PULLS YOU ASIDE";

        /// <summary>The raised dialogue's eyebrow, the objective line's own words: "MAYA HAS AN OFFER FOR YOU".</summary>
        public static string AskRaisedEyebrow(string first) => (first ?? "").ToUpperInvariant() + " HAS AN OFFER FOR YOU";

        /// <summary>The line under the controls while other houseguests wait: "1 more houseguest wants a word."</summary>
        public static string AskMoreWaitingLine(int n) => n == 1 ? "1 more houseguest wants a word."
                                                                 : n + " more houseguests want a word.";

        /// <summary>
        /// What a screen reader says for an ask's answer: the caption, who is asking, then the terms, each as a
        /// sentence - as the board says its cards' answers.
        /// </summary>
        public static string AskReading(string caption, string askerName, string terms) =>
            Sentence(caption) + " " + Sentence(askerName) + " " + (terms ?? "").Trim();

        private static string Sentence(string words)
        {
            string text = (words ?? "").Trim();
            return text.Length == 0 || text.EndsWith(".") || text.EndsWith("?") || text.EndsWith("!") ? text : text + ".";
        }

        /// <summary>
        /// Draws an ask's dialogue, top to bottom: the eyebrow; the speaker's face and name with their pronouns under
        /// them (in a conversation, its title, which sets the Conversation layout); their standing and mood as a line
        /// of body text; their line in a speech card - the whisper bubble for a private kind - when there is one; the
        /// heading "THEIR OFFER"; the terms; and how long the offer stands, when something takes it away. The answers
        /// are the owner's to draw after it.
        /// </summary>
        public void AskDialogue(string askerId, string eyebrow, string pronouns, string standing, string line,
                                bool whisper, string terms, string lapseLine, bool inConversation)
        {
            var state = director != null ? director.Snapshot : null;
            string name = state != null ? state.Find(askerId)?.name : null;
            Eyebrow(eyebrow, Accent);
            // The subtitle is the pronouns only: its box is fixed, and longer copy would truncate there.
            if (inConversation) SpeakerTitle(askerId, name ?? askerId, pronouns ?? "");
            else PanelTitle(name ?? askerId, pronouns ?? "");
            if (!string.IsNullOrEmpty(standing)) NamedParagraph(AskStandingName, standing);
            if (!string.IsNullOrEmpty(line)) Speech("\"" + line + "\"", AskLineName, whisper);
            Heading(AskOfferHeading);
            NamedParagraph(AskTermsName, terms ?? "");
            if (!string.IsNullOrEmpty(lapseLine)) NamedParagraph(AskLapseName, lapseLine, UiTheme.Muted);
        }

        /// <summary>The line under the answers saying how many other houseguests want a word; nothing for none.</summary>
        public void AskMoreWaiting(int count)
        {
            if (count <= 0) return;
            NamedParagraph(AskMoreName, AskMoreWaitingLine(count), UiTheme.Muted);
        }

        /// <summary>
        /// Registers what a screen reader says for one of the dialogue's answers, beside the board's
        /// (<see cref="TryReadOfferControl"/> reads both); Begin clears it with the controls.
        /// </summary>
        public void ReadAskControl(Button button, string reading)
        {
            if (button == null || string.IsNullOrEmpty(reading)) return;
            offerReadings[button] = reading;
        }
    }
}
