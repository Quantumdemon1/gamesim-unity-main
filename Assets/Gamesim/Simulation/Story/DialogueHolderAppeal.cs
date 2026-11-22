using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 3's appeal to the veto holder (DIALOGUE-PLAN §4.1 and §5.1, slice D3): a nominee's conversation with
    /// the houseguest who holds the veto, at the veto meeting before the decision. A closed definition, built afresh on
    /// every read and never a member of the ambient pool.
    ///
    /// <para>Three nodes. The player opens ("Ask about the veto", the paid entry: the window's one seat after the
    /// nominations) and the holder answers on a keyed coin, weighed by how they see the player: listening, or closed. The
    /// player replies with the relationship, the other nominee as the bigger threat (only to a holder who is listening),
    /// pressure, or a polite exit, and the holder reacts once: +3, 0 or -3. The last node is the player's call: the
    /// ordinary save plea's roll ("Ask them to use it on you"), no ask at all, or nothing. The conversation lapses at the
    /// decision, whoever makes it.</para>
    /// </summary>
    public static class DialogueHolderAppeal
    {
        public const string TemplateId = "dialogue-holder-appeal", Kind = "holder";
        public const int ContentVersion = 1;
        public const string Title = "Appeal to the veto holder", Category = "Veto meeting";
        public const string Open = "open", ReplyPrefix = "reply-", FinishPrefix = DialogueCampaignWord.FinishPrefix;
        public const string ByAnswer = DialogueCampaignWord.ByAnswer, ByReaction = DialogueCampaignWord.ByReaction;

        /// <summary>The holder's answer to the open, the first response's suffix.</summary>
        public const string Listening = "listening", Closed = "closed";
        public static readonly string[] Answers = { Listening, Closed };

        public const string Warmed = DialogueCampaignWord.Warmed, Unmoved = DialogueCampaignWord.Unmoved, Backfired = DialogueCampaignWord.Backfired;
        public static readonly string[] Reactions = DialogueCampaignWord.Reactions;

        public const string OpenCase = DialogueCampaignV1.OpenCase, Leave = DialogueCampaignV1.Leave;
        public const string Appeal = DialogueCampaignWord.Appeal, Press = DialogueCampaignWord.Press, NameThreat = "name-the-bigger-threat";
        public const string AskToUse = "ask-them-to-use-it", LeaveWithThem = "leave-it-with-them";
        public const string OpenLabel = "Ask about the veto", NameThreatLabel = "Name the bigger threat",
            AskToUseLabel = "Ask them to use it on you", LeaveWithThemLabel = "Leave it with them";

        /// <summary>The response of the finish that asks nothing.</summary>
        public const string NoAsk = "no-ask-made";

        public static string ReplyBeat(string answer) => ReplyPrefix + answer;
        public static string FinishBeat(string reaction) => FinishPrefix + reaction;

        /// <summary>The answer a reply node was drawn for, or null for any other node.</summary>
        public static string AnswerOf(string beatId) =>
            beatId != null && beatId.StartsWith(ReplyPrefix, StringComparison.Ordinal)
            && Array.IndexOf(Answers, beatId.Substring(ReplyPrefix.Length)) >= 0 ? beatId.Substring(ReplyPrefix.Length) : null;

        /// <summary>How a reply lands (unmeasured starting values, DIALOGUE-PLAN §5.4); an exit draws nothing.</summary>
        public static bool TryOdds(string optionId, out double warm, out double sour)
        {
            if (optionId == NameThreat) { warm = 0.35; sour = 0.15; return true; }
            return DialogueCampaignWord.TryOdds(optionId == Appeal || optionId == Press ? optionId : null, out warm, out sour);
        }

        public static int Delta(string reaction) => DialogueCampaignWord.Delta(reaction);

        // No shared mutable template is an authority. Validation constructs this definition afresh.
        internal static ArcTemplate CreateTemplate() => new ArcTemplate
        {
            id = TemplateId, lane = StoryLanes.Game, eyebrow = Category, title = Title,
            origin = "native: dialogue rules 3's action-funded appeal to the veto holder, zero-time contract",
            rulesVersion = StoryRules.Threads, minWeek = 1, minActive = 4,
            cooldownWeeks = 0, pairCooldownWeeks = 0, oncePerHeadliner = false,
            startAnchors = Array.Empty<string>(), roles = new[] { new ArcRole(DialogueCampaignV1.SpeakerRole) },
            beats = new[]
            {
                Beat(Open, Title, "The veto meeting has not happened yet.",
                    Option(OpenCase, OpenLabel, "Ask whether they mean to use the veto, and make your case.", "You asked about the veto.", ByAnswer),
                    Exit()),
                Beat(ReplyBeat(Listening), "They are listening", "They have not made up their mind about the veto.",
                    AppealOption(),
                    Option(NameThreat, NameThreatLabel, "Tell them why the other nominee is the bigger danger to their game.", "You named the bigger threat.", ByReaction),
                    PressOption(), Exit()),
                Beat(ReplyBeat(Closed), "They are not keen", "They don't sound ready to use the veto on you.",
                    AppealOption(), PressOption(), Exit()),
                Finish(Warmed, "They warmed to you", "That landed. The veto is still theirs to use."),
                Finish(Unmoved, "They heard you", "Nothing moved yet. The veto is still theirs to use."),
                Finish(Backfired, "That landed badly", "It went wrong. The veto is still theirs to use.")
            }
        };

        private static OptionTemplate AppealOption() => Option(Appeal, DialogueCampaignWord.AppealLabel,
            "Tell them what staying means to you.", "You made it personal.", ByReaction);

        private static OptionTemplate PressOption() => Option(Press, DialogueCampaignWord.PressLabel,
            "Push them for a straight answer. It can backfire.", "You pressed them.", ByReaction, HouseEventRisk.High);

        private static BeatTemplate Finish(string reaction, string title, string summary) => Beat(FinishBeat(reaction), title, summary,
            Option(AskToUse, AskToUseLabel, "Ask them to use the veto to take you off the block.", "You asked them to use the veto on you.", "end:answered"),
            Option(LeaveWithThem, LeaveWithThemLabel, "Ask for nothing. Let them decide.", "You left the veto with them.", "end:neutral"),
            Exit());

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary, text = summary,
            closes = StoryAnchors.BlockSet, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next,
            string risk = HouseEventRisk.Low) => new OptionTemplate
        { id = id, label = label, description = description, outcome = outcome, next = next, risk = risk };

        private static OptionTemplate Exit() => Option(Leave, DialogueCampaignV1.LeaveLabel, DialogueCampaignV1.LeaveDescription,
            DialogueCampaignV1.LeaveOutcome, "end:neutral");
    }
}
