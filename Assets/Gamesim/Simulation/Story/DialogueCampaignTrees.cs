using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 3's campaign conversation (DIALOGUE-PLAN §5.1, slice D3): the vote-word tree D2 built, and beside
    /// it the memory, the pressure and the neutral trees, chosen by the player at the conversation's second node. A closed
    /// definition, built afresh on every read and never a member of the ambient pool, as every dialogue template is.
    ///
    /// <para>Four nodes, within the five steps rules 2 set: the open ("Make your case", the paid entry), the approach
    /// (the player's first choice: which conversation this is), the voter's answer drawn into a reply node, and the
    /// finish the voter's reaction picks. Three consequential choices at most, four options at a node, and a neutral exit
    /// on every node.</para>
    /// <list type="bullet">
    /// <item><b>Ask where they stand</b> (the vote-word tree): the voter's word if they told the player this week,
    /// otherwise a concern or a deflection on a keyed coin.</item>
    /// <item><b>Bring up your history</b> (the memory tree), offered only while the player holds a fact about this voter
    /// from an earlier week: a vote they told the player about and kept, or a lie the reveal caught them in. The voter's
    /// answer is that fact; nothing is drawn for it.</item>
    /// <item><b>Keep things easy</b> (the neutral tree), offered when there is no such history: a light talk, which
    /// rarely moves anyone and almost never goes wrong.</item>
    /// <item><b>Lean on them</b> (the pressure tree): the voter wavers or bristles on a keyed coin, and pushing harder
    /// is the likeliest reply of all to backfire.</item>
    /// </list>
    /// <para>The memory and the neutral trees share the approach's third seat, so a node never offers more than four.
    /// Content is versioned like rules: what a node does never changes under <see cref="ContentVersion"/> 1 once
    /// rules 3 freezes.</para>
    /// </summary>
    public static class DialogueCampaignTrees
    {
        public const string TemplateId = "dialogue-campaign-trees";
        public const int ContentVersion = 1;
        public const string Open = "open", Approach = "approach", ApproachHistory = "approach-history";
        public const string ReplyPrefix = "reply-", FinishPrefix = DialogueCampaignWord.FinishPrefix;

        /// <summary>The next of the open: the approach node, with or without the history seat, fixed by the facts the player holds.</summary>
        public const string ByFacts = "by-facts";
        public const string ByAnswer = DialogueCampaignWord.ByAnswer, ByReaction = DialogueCampaignWord.ByReaction;

        /// <summary>The open's own response: the voter agrees to hear the player out.</summary>
        public const string Listening = "hear-your-case";

        public const string OpenCase = DialogueCampaignV1.OpenCase, Leave = DialogueCampaignV1.Leave;
        public const string Plea = DialogueCampaignV1.Plea, KeepOpen = DialogueCampaignV1.KeepOpen;

        // ---------------------------------------------------------------- the approach: which tree this is
        public const string AskStand = "ask-where-they-stand", BringUp = "bring-up-your-history",
            KeepEasy = "keep-things-easy", Lean = "lean-on-them";
        public const string AskStandLabel = "Ask where they stand", BringUpLabel = "Bring up your history",
            KeepEasyLabel = "Keep things easy", LeanLabel = "Lean on them";

        // ---------------------------------------------------------------- the voter's answers, one closed set a tree
        public const string Word = DialogueCampaignWord.Word, Against = DialogueCampaignWord.Against,
            Concern = DialogueCampaignWord.Concern, Guarded = DialogueCampaignWord.Guarded;
        public const string Kept = "kept", Lied = "lied", Easy = "easy", Distracted = "distracted", Wavers = "wavers", Bristles = "bristles";
        public static readonly string[] WordAnswers = { Word, Against, Concern, Guarded };
        public static readonly string[] HistoryAnswers = { Kept, Lied };
        public static readonly string[] EasyAnswers = { Easy, Distracted };
        public static readonly string[] PressureAnswers = { Wavers, Bristles };

        public const string Warmed = DialogueCampaignWord.Warmed, Unmoved = DialogueCampaignWord.Unmoved, Backfired = DialogueCampaignWord.Backfired;
        public static readonly string[] Reactions = DialogueCampaignWord.Reactions;

        // ---------------------------------------------------------------- the replies
        public const string CiteWord = DialogueCampaignWord.CiteWord, Appeal = DialogueCampaignWord.Appeal,
            AskConcern = DialogueCampaignWord.AskConcern, Press = DialogueCampaignWord.Press;
        public const string CountOnWord = "count-on-their-word", CallOutLie = "call-out-the-lie", ClearUp = "clear-things-up",
            ShareLaugh = "share-a-laugh", TurnToVote = "turn-it-to-the-vote", GiveSpace = "give-them-space",
            PushHarder = "push-harder", EaseOff = "ease-off", BackDown = "back-down";
        public const string CountOnWordLabel = "Count on their word again", CallOutLieLabel = "Call out the lie",
            ClearUpLabel = "Clear things up", ShareLaughLabel = "Share a laugh", TurnToVoteLabel = "Turn it to the vote",
            GiveSpaceLabel = "Give them space", PushHarderLabel = "Push harder", EaseOffLabel = "Ease off", BackDownLabel = "Back down";

        public const string StatementEvidence = DialogueCampaignWord.StatementEvidence, MemoryEvidence = DialogueCampaignWord.MemoryEvidence;

        /// <summary>The answers an approach can draw, or null for an option that is no approach.</summary>
        public static string[] AnswersOf(string approachOption) =>
            approachOption == AskStand ? WordAnswers : approachOption == BringUp ? HistoryAnswers
            : approachOption == KeepEasy ? EasyAnswers : approachOption == Lean ? PressureAnswers : null;

        /// <summary>Every answer of every tree: each is unique across the trees, so it names its own reply node.</summary>
        public static IEnumerable<string> AllAnswers() => WordAnswers.Concat(HistoryAnswers).Concat(EasyAnswers).Concat(PressureAnswers);

        public static string ReplyBeat(string answer) => ReplyPrefix + answer;
        public static string FinishBeat(string reaction) => FinishPrefix + reaction;

        /// <summary>The answer a reply node was drawn for, or null for any other node.</summary>
        public static string AnswerOf(string beatId) =>
            beatId != null && beatId.StartsWith(ReplyPrefix, StringComparison.Ordinal)
            && AllAnswers().Contains(beatId.Substring(ReplyPrefix.Length)) ? beatId.Substring(ReplyPrefix.Length) : null;

        public static bool IsApproach(string beatId) => beatId == Approach || beatId == ApproachHistory;

        /// <summary>Whether an answer copies a vote claim as the conversation's statement: this week's word, or a kept one.</summary>
        public static bool WritesStatement(string answer) => answer == Word || answer == Against || answer == Kept;

        /// <summary>The evidence a reply option cites, or null for an option that cites nothing.</summary>
        public static string EvidenceOf(string optionId) =>
            optionId == CiteWord || optionId == CountOnWord ? StatementEvidence : optionId == CallOutLie ? MemoryEvidence : null;

        /// <summary>
        /// How a reply lands: the chance the voter warms to it, and the chance it backfires (DIALOGUE-PLAN §5.4,
        /// unmeasured starting values tuned only through D6). D2's replies keep D2's numbers. A kept word is the safest
        /// ground of all; pushing harder the likeliest to go wrong. An exit draws nothing.
        /// </summary>
        public static bool TryOdds(string optionId, out double warm, out double sour)
        {
            switch (optionId)
            {
                case CountOnWord: warm = 0.55; sour = 0.05; return true;
                case CallOutLie: warm = 0.35; sour = 0.35; return true;
                case ClearUp: warm = 0.40; sour = 0.10; return true;
                case ShareLaugh: warm = 0.35; sour = 0.05; return true;
                case TurnToVote: warm = 0.30; sour = 0.10; return true;
                case GiveSpace: warm = 0.25; sour = 0.00; return true;
                case PushHarder: warm = 0.25; sour = 0.50; return true;
                case EaseOff: warm = 0.35; sour = 0.05; return true;
                case BackDown: warm = 0.20; sour = 0.05; return true;
                default: return DialogueCampaignWord.TryOdds(optionId, out warm, out sour);
            }
        }

        /// <summary>How the voter and the player come to see each other after a reaction: +3, 0 or -3 (§5.4).</summary>
        public static int Delta(string reaction) => DialogueCampaignWord.Delta(reaction);

        // No shared mutable template is an authority. Validation constructs this definition afresh.
        internal static ArcTemplate CreateTemplate()
        {
            var beats = new List<BeatTemplate>
            {
                Beat(Open, DialogueCampaignV1.OpenTitle, "There is time to talk before the vote.",
                    Option(OpenCase, DialogueCampaignV1.OpenLabel, "Ask for a word before the vote, and choose how to start.",
                        "You asked for a word.", ByFacts), Exit()),
                ApproachBeat(Approach, false),
                ApproachBeat(ApproachHistory, true)
            };
            foreach (string answer in AllAnswers()) beats.Add(Reply(answer));
            beats.Add(Finish(Warmed, "They warmed to you", "That landed. Their vote is still theirs to give."));
            beats.Add(Finish(Unmoved, "They heard you", "Nothing moved yet. Their vote is still theirs to give."));
            beats.Add(Finish(Backfired, "That landed badly", "It went wrong. Their vote is still theirs to give."));
            return new ArcTemplate
            {
                id = TemplateId, lane = StoryLanes.Game, eyebrow = DialogueCampaignV1.Category, title = DialogueCampaignV1.Title,
                origin = "native: dialogue rules 3's action-funded campaign conversation, four trees, zero-time contract",
                rulesVersion = StoryRules.Threads, minWeek = 1, minActive = 4,
                cooldownWeeks = 0, pairCooldownWeeks = 0, oncePerHeadliner = false,
                startAnchors = Array.Empty<string>(), roles = new[] { new ArcRole(DialogueCampaignV1.SpeakerRole) },
                beats = beats.ToArray()
            };
        }

        private static BeatTemplate ApproachBeat(string id, bool history) => Beat(id, "How do you start?",
            history ? "They are listening. You have some history with them." : "They are listening.",
            Option(AskStand, AskStandLabel, "Ask where their vote is, and answer what they say.", "You asked where they stand.", ByAnswer),
            history
                ? Option(BringUp, BringUpLabel, "Talk about what happened between you in an earlier week.", "You brought up your history.", ByAnswer)
                : Option(KeepEasy, KeepEasyLabel, "Talk about anything but the game for a minute.", "You kept things easy.", ByAnswer),
            Option(Lean, LeanLabel, "Tell them plainly that you need their vote. It can backfire.", "You leaned on them.", ByAnswer, HouseEventRisk.Medium),
            Exit());

        private static BeatTemplate Reply(string answer)
        {
            var options = new List<OptionTemplate>();
            string title, summary;
            switch (answer)
            {
                case Word:
                    title = "They give you their word"; summary = "They say they are not voting you out.";
                    options.Add(Option(CiteWord, DialogueCampaignWord.CiteWordLabel, "Ask them to keep the vote they told you about.", "You held them to their word.", ByReaction));
                    options.Add(AppealOption());
                    options.Add(PressOption());
                    break;
                case Against:
                    title = "They tell you straight"; summary = "They say they are voting you out.";
                    options.Add(AppealOption()); options.Add(PressOption());
                    break;
                case Concern:
                    title = "They have a concern"; summary = "They worry what you would do if you stayed.";
                    options.Add(Option(AskConcern, DialogueCampaignWord.AskConcernLabel, "Let them say what they are afraid of, and answer it.", "You asked what worries them.", ByReaction));
                    options.Add(PressOption());
                    break;
                case Guarded:
                    title = "They keep it close"; summary = "They won't say where their vote is.";
                    options.Add(AppealOption()); options.Add(PressOption());
                    break;
                case Kept:
                    title = "They remember it too"; summary = "They told you how they would vote once, and they did.";
                    options.Add(Option(CountOnWord, CountOnWordLabel, "Remind them they kept their word to you, and ask them to again.", "You counted on their word.", ByReaction));
                    options.Add(AppealOption());
                    break;
                case Lied:
                    title = "It is awkward"; summary = "They lied to you about a vote once, and you both know it.";
                    options.Add(Option(CallOutLie, CallOutLieLabel, "Remind them they lied to you about a vote. It can backfire.", "You called out the lie.", ByReaction, HouseEventRisk.Medium));
                    options.Add(Option(ClearUp, ClearUpLabel, "Say it is behind you, and ask for a fair hearing.", "You cleared things up.", ByReaction));
                    break;
                case Easy:
                    title = "They relax"; summary = "The talk is easy for once.";
                    options.Add(Option(ShareLaugh, ShareLaughLabel, "Keep it fun. Leave the vote out of it.", "You shared a laugh.", ByReaction));
                    options.Add(TurnToVoteOption());
                    break;
                case Distracted:
                    title = "Their mind is elsewhere"; summary = "They are only half listening.";
                    options.Add(Option(GiveSpace, GiveSpaceLabel, "Let them be. Nothing ventured, nothing lost.", "You gave them space.", ByReaction));
                    options.Add(TurnToVoteOption());
                    break;
                case Wavers:
                    title = "They waver"; summary = "They don't like being put on the spot.";
                    options.Add(PushHarderOption());
                    options.Add(Option(EaseOff, EaseOffLabel, "Back off a little and let it sink in.", "You eased off.", ByReaction));
                    break;
                default: // Bristles
                    title = "They bristle"; summary = "They don't take kindly to being pushed.";
                    options.Add(PushHarderOption());
                    options.Add(Option(BackDown, BackDownLabel, "Apologize and drop it.", "You backed down.", ByReaction));
                    break;
            }
            options.Add(Exit());
            return Beat(ReplyBeat(answer), title, summary, options.ToArray());
        }

        private static OptionTemplate AppealOption() => Option(DialogueCampaignWord.Appeal, DialogueCampaignWord.AppealLabel,
            "Tell them what staying means to you.", "You made it personal.", ByReaction);

        private static OptionTemplate PressOption() => Option(DialogueCampaignWord.Press, DialogueCampaignWord.PressLabel,
            "Push them for a straight answer. It can backfire.", "You pressed them.", ByReaction, HouseEventRisk.High);

        private static OptionTemplate PushHarderOption() => Option(PushHarder, PushHarderLabel,
            "Tell them you won't let it go. It often backfires.", "You pushed harder.", ByReaction, HouseEventRisk.High);

        private static OptionTemplate TurnToVoteOption() => Option(TurnToVote, TurnToVoteLabel,
            "Bring the talk around to the vote.", "You turned it to the vote.", ByReaction);

        private static BeatTemplate Finish(string reaction, string title, string summary) => Beat(FinishBeat(reaction), title, summary,
            Option(Plea, DialogueCampaignV1.PleaLabel, DialogueCampaignV1.PleaDescription, DialogueCampaignV1.PleaOutcome, "end:answered"),
            Option(KeepOpen, DialogueCampaignV1.KeepLabel, DialogueCampaignV1.KeepDescription, DialogueCampaignV1.KeepOutcome, "end:neutral"),
            Exit());

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary, text = summary,
            closes = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next,
            string risk = HouseEventRisk.Low) => new OptionTemplate
        { id = id, label = label, description = description, outcome = outcome, next = next, risk = risk };

        private static OptionTemplate Exit() => Option(Leave, DialogueCampaignV1.LeaveLabel, DialogueCampaignV1.LeaveDescription,
            DialogueCampaignV1.LeaveOutcome, "end:neutral");
    }
}
