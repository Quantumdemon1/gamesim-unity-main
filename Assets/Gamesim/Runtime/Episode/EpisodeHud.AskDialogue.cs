using Gamesim.Presentation;
using UnityEngine;
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
        /// <summary>The way out of a walk-over's dialogue, or of an ask's card, that answers nothing. No other control says it.</summary>
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
        /// <summary>The name of the line a reply card's page shows once it is answered: the answer's own log line (slice 4a).</summary>
        public const string AskOutcomeName = "Ask outcome";
        /// <summary>What a reply card's page says when the card went before any answer (slice 4a).</summary>
        public const string AskCardGoneLine = "They've moved on.";

        /// <summary>The walk-over's eyebrow: "MAYA PULLS YOU ASIDE".</summary>
        public static string AskWalkOverEyebrow(string first) => (first ?? "").ToUpperInvariant() + " PULLS YOU ASIDE";

        /// <summary>The raised dialogue's eyebrow, the objective line's own words: "MAYA HAS AN OFFER FOR YOU".</summary>
        public static string AskRaisedEyebrow(string first) => (first ?? "").ToUpperInvariant() + " HAS AN OFFER FOR YOU";

        /// <summary>
        /// A raised reply card's eyebrow (slice 4a): "MAYA WANTS A WORD". A confrontation, gossip or a plea is not an offer,
        /// so the offer's own words would be untrue.
        /// </summary>
        public static string AskCardRaisedEyebrow(string first) => (first ?? "").ToUpperInvariant() + " WANTS A WORD";

        /// <summary>
        /// A reply card's page once the player has answered it on the card (slice 4a): the eyebrow, then the answer's own
        /// log line - what the season keeps of it. The owner draws the way back to the house under it.
        /// </summary>
        public void AskOutcome(string line)
        {
            Eyebrow(AskDirectEyebrow, Accent);
            NamedParagraph(AskOutcomeName, line ?? "");
        }

        /// <summary>
        /// A reply card's answers in an ask's frame (slice 4a): the tiles every card surface draws them as
        /// (<see cref="EventChoices"/>), each registered with what a screen reader says for it.
        /// </summary>
        public void AskChoices(System.Collections.Generic.IList<(string Caption, string Description, string Risk, System.Action Choose, string Reading)> choices)
        {
            if (choices == null || choices.Count == 0 || content == null) return;
            var tiles = new System.Collections.Generic.List<(string Caption, string Description, string Risk, System.Action Choose)>();
            foreach (var choice in choices) tiles.Add((choice.Caption, choice.Description, choice.Risk, choice.Choose));
            EventChoices(tiles);
            // The grid just drawn is the content's last of that name; each tile is named for its caption.
            Transform grid = null;
            for (int i = content.childCount - 1; i >= 0 && grid == null; i--)
                if (content.GetChild(i).name == EventChoicesName) grid = content.GetChild(i);
            if (grid == null) return;
            foreach (var choice in choices)
                for (int i = 0; i < grid.childCount; i++)
                {
                    var tile = grid.GetChild(i);
                    if (tile.name == choice.Caption) ReadAskControl(tile.GetComponent<Button>(), choice.Reading);
                }
        }

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
        /// them (in a conversation, its title, which sets the Conversation layout; on the card, the same row in the
        /// card's own layout); their standing and mood as a line of body text; their line in a speech card - the
        /// whisper bubble for a private kind - when there is one; the heading "THEIR OFFER"; the terms; and how long
        /// the offer stands, when something takes it away. The answers are the owner's to draw after it.
        /// </summary>
        public void AskDialogue(string askerId, string eyebrow, string pronouns, string standing, string line,
                                bool whisper, string terms, string lapseLine, bool inConversation) =>
            AskDialogue(askerId, eyebrow, pronouns, standing, line, whisper, AskOfferHeading, terms, lapseLine, inConversation);

        /// <summary>
        /// The dialogue under a heading of the source's own (slice 4a): a reply card passes its title, the words the
        /// free-time hero and the campaign board show over it, and its message as the terms.
        /// </summary>
        public void AskDialogue(string askerId, string eyebrow, string pronouns, string standing, string line,
                                bool whisper, string heading, string terms, string lapseLine, bool inConversation)
        {
            var state = director != null ? director.Snapshot : null;
            string name = state != null ? state.Find(askerId)?.name : null;
            Eyebrow(eyebrow, Accent);
            // The subtitle is the pronouns only: its box is fixed, and longer copy would truncate there.
            if (inConversation) SpeakerTitle(askerId, name ?? askerId, pronouns ?? "");
            else SpeakerRow(askerId, name ?? askerId, pronouns ?? "");
            if (!string.IsNullOrEmpty(standing)) NamedParagraph(AskStandingName, standing);
            if (!string.IsNullOrEmpty(line)) AskSpeech(line, AskLineName, whisper);
            Heading(heading ?? AskOfferHeading);
            NamedParagraph(AskTermsName, terms ?? "");
            if (!string.IsNullOrEmpty(lapseLine)) NamedParagraph(AskLapseName, lapseLine, UiTheme.Muted);
        }

        /// <summary>
        /// The card's page once the player has answered (slice 2): what the player said, then what the houseguest said
        /// back, each under its speaker's face as a conversation's exchange says them. The owner draws the way back to
        /// the house under it.
        /// </summary>
        public void AskReply(string askerId, string playerLine, string npcLine)
        {
            var state = director != null ? director.Snapshot : null;
            var asker = state != null ? state.Find(askerId) : null;
            var self = state != null ? state.Find(state.playerId) : null;
            Eyebrow(AskDirectEyebrow, Accent);
            // The exchange's own words for the two halves (EpisodeHud.ConversationExchange.cs).
            string you = self?.name ?? "You";
            if (self != null && !string.Equals(you, "You", System.StringComparison.OrdinalIgnoreCase)) you += " (You)";
            SpeakerRow(state != null ? state.playerId : null, you, "Your words");
            AskSpeech(playerLine ?? "", AskYourWordsName, false);
            SpeakerRow(askerId, asker?.name ?? askerId, "Their reply");
            AskSpeech(npcLine ?? "", AskReplyName, false);
        }

        /// <summary>
        /// A line said in an ask, in a speech card. One line of Inter lays out at about 1.21x its size, which draws
        /// nothing in a box under that; an ask's lines get a floor of their own, so every other conversation's spoken
        /// line stays as it was.
        /// </summary>
        private void AskSpeech(string line, string name, bool whisper)
        {
            var spoken = Speech("\"" + line + "\"", name, whisper);
            spoken.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.Ceil(spoken.fontSize * 1.32f) + 1f;
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
