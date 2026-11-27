using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 5's HoH consultation (DIALOGUE-PLAN §4, §4.1 and §5.1, slice D5b-1): the player as Head of Household
    /// calls a houseguest in before naming the week's nominees. A closed definition, built afresh on every read and never a
    /// member of the ambient pool.
    ///
    /// <para>Three nodes. The player opens ("Ask for their case", the entry: the HoH grant, or else one of the window's seats
    /// after the HoH) and the houseguest answers on a keyed coin, weighed by how they see the player: they plead their case,
    /// or they keep their cards close. The player reassures them, asks who they would put up (only of one who pleaded),
    /// presses them, or leaves, and the houseguest reacts once: +3, 0 or -3. The last node is the player's call: a promise
    /// of safety - the existing promise, never a second one to them that week, and under the role moments the answer to their
    /// own pitch when it is on the board - no promise at all, or nothing. The conversation lapses when the nominations
    /// commit, and is not offered again for a replacement.</para>
    /// </summary>
    public static class DialogueHoHConsult
    {
        public const string TemplateId = "dialogue-hoh-consult", Kind = "hoh-consult";
        public const int ContentVersion = 1;
        public const string Title = "Before your nominations", Category = "Head of Household";
        public const string Open = "open", ReplyPrefix = "reply-", FinishPrefix = DialogueCampaignWord.FinishPrefix;
        public const string ByAnswer = DialogueCampaignWord.ByAnswer, ByReaction = DialogueCampaignWord.ByReaction;

        /// <summary>The houseguest's answer to the open, the first response's suffix.</summary>
        public const string Pleading = "pleading", Guarded = "guarded";
        public static readonly string[] Answers = { Pleading, Guarded };

        public const string Warmed = DialogueCampaignWord.Warmed, Unmoved = DialogueCampaignWord.Unmoved, Backfired = DialogueCampaignWord.Backfired;
        public static readonly string[] Reactions = DialogueCampaignWord.Reactions;

        public const string OpenCase = DialogueCampaignV1.OpenCase, Leave = DialogueCampaignV1.Leave;
        public const string Reassure = "reassure-them", AskTarget = "ask-who-they-would-name", Press = DialogueCampaignWord.Press;
        public const string PromiseSafety = "promise-them-safety", NoPromise = "make-no-promises";
        public const string OpenLabel = "Ask for their case", ReassureLabel = "Reassure them", AskTargetLabel = "Ask who they'd put up",
            PressLabel = "Press them", PromiseSafetyLabel = "Promise them safety", NoPromiseLabel = "Make no promises";

        /// <summary>The responses of the last step: the promise made, an existing one reaffirmed, no promise, and the exits.</summary>
        public const string Promised = "safety-promised", Reaffirmed = "safety-reaffirmed", NoPromiseMade = "no-promise-made",
            RespectfulExit = "respectful-exit", OccasionClosed = "occasion-closed";

        /// <summary>The endings: a promise of safety, and every other exit.</summary>
        public const string PromisedEnding = "promised", Neutral = "neutral";

        public static string ReplyBeat(string answer) => ReplyPrefix + answer;
        public static string FinishBeat(string reaction) => FinishPrefix + reaction;

        /// <summary>The answer a reply node was drawn for, or null for any other node.</summary>
        public static string AnswerOf(string beatId) =>
            beatId != null && beatId.StartsWith(ReplyPrefix, StringComparison.Ordinal)
            && Array.IndexOf(Answers, beatId.Substring(ReplyPrefix.Length)) >= 0 ? beatId.Substring(ReplyPrefix.Length) : null;

        /// <summary>The reaction a finish node was drawn for, or null for any other node.</summary>
        public static string ReactionOf(string beatId) =>
            beatId != null && beatId.StartsWith(FinishPrefix, StringComparison.Ordinal)
            && Array.IndexOf(Reactions, beatId.Substring(FinishPrefix.Length)) >= 0 ? beatId.Substring(FinishPrefix.Length) : null;

        /// <summary>How a reply lands (unmeasured starting values, DIALOGUE-PLAN §5.4); an exit draws nothing.</summary>
        public static bool TryOdds(string optionId, out double warm, out double sour)
        {
            switch (optionId)
            {
                case Reassure: warm = 0.45; sour = 0.05; return true;
                case AskTarget: warm = 0.35; sour = 0.15; return true;
                case Press: warm = 0.20; sour = 0.45; return true;
                default: warm = 0; sour = 0; return false;
            }
        }

        public static int Delta(string reaction) => DialogueCampaignWord.Delta(reaction);

        // No shared mutable template is an authority. Validation constructs this definition afresh.
        internal static ArcTemplate CreateTemplate() => new ArcTemplate
        {
            id = TemplateId, lane = StoryLanes.Game, eyebrow = Category, title = Title,
            origin = "native: dialogue rules 5's HoH consultation, paid by the HoH grant or the window's action, zero-time contract",
            rulesVersion = StoryRules.Threads, minWeek = 1, minActive = 4,
            cooldownWeeks = 0, pairCooldownWeeks = 0, oncePerHeadliner = false,
            startAnchors = Array.Empty<string>(), roles = new[] { new ArcRole(DialogueCampaignV1.SpeakerRole) },
            beats = new[]
            {
                Beat(Open, Title, "You have not named your nominees yet.",
                    Option(OpenCase, OpenLabel, "Ask them why they should stay off the block.", "You asked for their case.", ByAnswer),
                    Exit()),
                Beat(ReplyBeat(Pleading), "They make their case", "\"I'm not coming after you. Keep me off the block and I won't forget it.\"",
                    ReassureOption(),
                    Option(AskTarget, AskTargetLabel, "Ask who they think should go up instead.", "You asked who they would put up.",
                        ByReaction, HouseEventRisk.Medium),
                    PressOption(), Exit()),
                Beat(ReplyBeat(Guarded), "They keep their cards close", "\"I'm not going to beg. You'll do what you think is best.\"",
                    ReassureOption(), PressOption(), Exit()),
                Finish(Warmed, "They warmed to you", "That landed. The nominations are still yours to make."),
                Finish(Unmoved, "They heard you", "Nothing moved yet. The nominations are still yours to make."),
                Finish(Backfired, "That landed badly", "It went wrong. The nominations are still yours to make.")
            }
        };

        private static OptionTemplate ReassureOption() => Option(Reassure, ReassureLabel,
            "Tell them you hear them, without promising anything.", "You reassured them.", ByReaction);

        private static OptionTemplate PressOption() => Option(Press, PressLabel,
            "Make them earn your trust. It can backfire.", "You pressed them.", ByReaction, HouseEventRisk.High);

        private static BeatTemplate Finish(string reaction, string title, string summary) => Beat(FinishBeat(reaction), title, summary,
            Option(PromiseSafety, PromiseSafetyLabel, "Give them your word they won't go up. Naming them now or as a replacement breaks it.",
                "You promised them safety.", "end:" + PromisedEnding),
            Option(NoPromise, NoPromiseLabel, "Thank them and keep your options open.", "You made them no promises.", "end:" + Neutral),
            Exit());

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary, text = summary,
            closes = StoryAnchors.NomsSet, surface = StorySurfaces.Conversation, venue = StoryVenues.HohRoom,
            lapse = Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next,
            string risk = HouseEventRisk.Low) => new OptionTemplate
        { id = id, label = label, description = description, outcome = outcome, next = next, risk = risk };

        private static OptionTemplate Exit() => Option(Leave, DialogueCampaignV1.LeaveLabel, DialogueCampaignV1.LeaveDescription,
            DialogueCampaignV1.LeaveOutcome, "end:" + Neutral);
    }
}
