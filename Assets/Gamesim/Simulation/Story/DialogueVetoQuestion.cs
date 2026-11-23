using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 4's veto question (DIALOGUE-PLAN §4.1, §4.2 and §5.1, slice D4): a nominee asks a veto player, after the
    /// veto players are drawn and before the competition is played, whether they would use the veto on them if they win it.
    /// A closed definition, built afresh on every read and never a member of the ambient pool.
    ///
    /// <para>Two nodes. The player opens ("Ask if they'd use the veto on you", the paid entry: the window's one seat after the
    /// nominations) and the veto player answers on a keyed coin: yes, no, not committing, or a condition. The answer is the
    /// conversation's statement. After a yes the player can ask "Can we make that a promise?", which writes one
    /// <see cref="DealKind.VetoIf"/> deal; after any answer they can thank them or leave. The conversation lapses at the
    /// competition's result.</para>
    /// </summary>
    public static class DialogueVetoQuestion
    {
        public const string TemplateId = "dialogue-veto-question", Kind = "veto-question";
        public const int ContentVersion = 1;
        public const string Title = "A word before the veto", Category = "Veto competition";
        public const string Open = "open", ReplyPrefix = "reply-";
        public const string ByAnswer = DialogueCampaignWord.ByAnswer;

        /// <summary>The veto player's answer to the question: the first response's suffix and the statement's answer.</summary>
        public const string Yes = "yes", No = "no", NotCommitting = "not-committing", Condition = "condition";
        public static readonly string[] Answers = { Yes, No, NotCommitting, Condition };

        /// <summary>The statement's closed condition: the answer holds only if the speaker wins the veto (§6's fit check).</summary>
        public const string ConditionKind = "veto-if-win";

        public const string OpenCase = DialogueCampaignV1.OpenCase, Leave = DialogueCampaignV1.Leave;
        public const string AskPromise = "ask-for-a-promise", Thank = "thank-them";
        public const string OpenLabel = "Ask if they'd use the veto on you", AskPromiseLabel = "Can we make that a promise?",
            ThankLabel = "Thank them";

        /// <summary>The responses of the second step: the promise given or refused, thanks, and the exit.</summary>
        public const string PromiseMade = "promise-made", PromiseRefused = "promise-refused", Thanked = "thanked",
            RespectfulExit = "respectful-exit", OccasionClosed = "occasion-closed";

        /// <summary>The endings: the promise asked for, and every other exit.</summary>
        public const string Answered = "answered", Neutral = "neutral";

        public static string ReplyBeat(string answer) => ReplyPrefix + answer;

        /// <summary>The answer a reply node was drawn for, or null for any other node.</summary>
        public static string AnswerOf(string beatId) =>
            beatId != null && beatId.StartsWith(ReplyPrefix, StringComparison.Ordinal)
            && Array.IndexOf(Answers, beatId.Substring(ReplyPrefix.Length)) >= 0 ? beatId.Substring(ReplyPrefix.Length) : null;

        // No shared mutable template is an authority. Validation constructs this definition afresh.
        internal static ArcTemplate CreateTemplate() => new ArcTemplate
        {
            id = TemplateId, lane = StoryLanes.Game, eyebrow = Category, title = Title,
            origin = "native: dialogue rules 4's action-funded veto question, zero-time contract",
            rulesVersion = StoryRules.Threads, minWeek = 1, minActive = 4,
            cooldownWeeks = 0, pairCooldownWeeks = 0, oncePerHeadliner = false,
            startAnchors = Array.Empty<string>(), roles = new[] { new ArcRole(DialogueCampaignV1.SpeakerRole) },
            beats = new[]
            {
                Beat(Open, Title, "The veto competition has not been played yet.",
                    Option(OpenCase, OpenLabel, "Ask whether they would use the veto on you if they win it.",
                        "You asked if they would use the veto on you.", ByAnswer),
                    Exit()),
                Beat(ReplyBeat(Yes), "They said yes", "\"If I win it, I'll use it on you.\"",
                    Option(AskPromise, AskPromiseLabel, "Ask for their word. If they win and don't use it on you, they broke it.",
                        "You asked them to promise.", "end:" + Answered),
                    ThankOption(), Exit()),
                Beat(ReplyBeat(No), "They said no", "\"Honestly? No. If I win, I'm not using it on you.\"",
                    ThankOption(), Exit()),
                Beat(ReplyBeat(NotCommitting), "They won't say", "\"Let me win it first. Then we'll talk.\"",
                    ThankOption(), Exit()),
                Beat(ReplyBeat(Condition), "They have a condition", "\"Maybe. If I pull you off the block, I need your vote next week.\"",
                    ThankOption(), Exit())
            }
        };

        private static OptionTemplate ThankOption() => Option(Thank, ThankLabel, "Leave it there, on good terms.", "You thanked them.", "end:" + Neutral);

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary, text = summary,
            closes = StoryAnchors.VetoWon, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next) => new OptionTemplate
        { id = id, label = label, description = description, outcome = outcome, next = next, risk = HouseEventRisk.Low };

        private static OptionTemplate Exit() => Option(Leave, DialogueCampaignV1.LeaveLabel, DialogueCampaignV1.LeaveDescription,
            DialogueCampaignV1.LeaveOutcome, "end:" + Neutral);
    }
}
