using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 2's one tree (DIALOGUE-PLAN §5.1, slice D2): the campaign appeal on the voter's word. A closed
    /// definition, built afresh on every read and never a member of the ambient pool, as the pilots' are.
    ///
    /// <para>Three nodes. The player opens ("Make your case", the paid entry) and the voter answers with what they
    /// really have to say: their word on the vote if they told the player this week (<see cref="Word"/> or
    /// <see cref="Against"/>), or, drawn on a keyed coin, a <see cref="Concern"/> or a <see cref="Guarded"/>
    /// deflection. The player replies with a fact they hold (the voter's own word, a public rescue, or the lie the
    /// reveal caught), the relationship, pressure or a polite exit. The voter reacts once, on a second keyed coin:
    /// +3, 0 or -3 for how they and the player see each other. The last node is the player's call, as in rules 1: the
    /// ordinary plea, their vote left open, or nothing.</para>
    ///
    /// <para>Which reply node is drawn is fixed by the voter's answer and by the facts the player holds when the
    /// conversation opens, so every node has a fixed list of at most four options: one fact, the relationship or
    /// the concern, one kind of pressure, and the exit. Which finish node follows is the voter's reaction. Content is
    /// versioned like rules: what a node does never changes under <see cref="ContentVersion"/> 1 once rules 2
    /// freezes.</para>
    /// </summary>
    public static class DialogueCampaignWord
    {
        public const string TemplateId = "dialogue-campaign-word";
        public const int ContentVersion = 1;
        public const string Open = "open", ReplyPrefix = "reply-", FinishPrefix = "finish-";

        /// <summary>The voter's answer to the open, the first response's suffix (DIALOGUE-PLAN §5.1, step 2).</summary>
        public const string Word = "word", Against = "against", Guarded = "guarded", Concern = "concern";
        public static readonly string[] Answers = { Word, Against, Guarded, Concern };

        /// <summary>The voter's reaction to the reply, the second response's suffix: +3, 0 or -3 (§5.4).</summary>
        public const string Warmed = "warmed", Unmoved = "unmoved", Backfired = "backfired";
        public static readonly string[] Reactions = { Warmed, Unmoved, Backfired };

        /// <summary>The next of an option whose node depends on what the voter says: the answer, or the reaction.</summary>
        public const string ByAnswer = "by-answer", ByReaction = "by-reaction";

        public const string OpenCase = DialogueCampaignV1.OpenCase, Leave = DialogueCampaignV1.Leave;
        public const string Plea = DialogueCampaignV1.Plea, KeepOpen = DialogueCampaignV1.KeepOpen;
        public const string CiteWord = "hold-to-their-word", CiteRescue = "remind-of-the-rescue", CiteLie = "bring-up-the-lie";
        public const string Appeal = "make-it-personal", AskConcern = "ask-what-worries-them", Press = "press-them";

        public const string CiteWordLabel = "Hold them to their word", CiteRescueLabel = "Remind them of the rescue",
            CiteLieLabel = "Bring up the last lie", AppealLabel = "Make it personal", AskConcernLabel = "Ask what worries them",
            PressLabel = "Press them";

        /// <summary>The evidence kind each citing option writes (DIALOGUE-PLAN §5.1): statement, power or memory.</summary>
        public const string StatementEvidence = "statement", PowerEvidence = "power", MemoryEvidence = "memory";

        /// <summary>The reply node for an answer and the facts held at the open. A word is cited as itself, so a rescue is never offered beside it.</summary>
        public static string ReplyBeat(string answer, bool rescue, bool lie) =>
            ReplyPrefix + answer + (rescue && answer != Word ? "-rescue" : "") + (lie ? "-lie" : "");

        public static string FinishBeat(string reaction) => FinishPrefix + reaction;

        /// <summary>Every reply node: the answer, then whether it offers the rescue, then whether it offers the lie.</summary>
        public static IEnumerable<string> ReplyBeats()
        {
            foreach (string answer in Answers)
                foreach (bool rescue in answer == Word ? new[] { false } : new[] { false, true })
                    foreach (bool lie in new[] { false, true })
                        yield return ReplyBeat(answer, rescue, lie);
        }

        /// <summary>The answer a reply node was drawn for, or null for any other node.</summary>
        public static string AnswerOf(string beatId)
        {
            if (beatId == null || !beatId.StartsWith(ReplyPrefix, StringComparison.Ordinal)) return null;
            string rest = beatId.Substring(ReplyPrefix.Length);
            int dash = rest.IndexOf('-');
            string answer = dash < 0 ? rest : rest.Substring(0, dash);
            return Array.IndexOf(Answers, answer) >= 0 && ReplyBeats().Contains(beatId) ? answer : null;
        }

        public static bool OffersRescue(string beatId) => AnswerOf(beatId) != null && beatId.Contains("-rescue");
        public static bool OffersLie(string beatId) => AnswerOf(beatId) != null && beatId.EndsWith("-lie", StringComparison.Ordinal);

        /// <summary>The evidence a reply option cites, or null for an option that cites nothing.</summary>
        public static string EvidenceOf(string optionId) =>
            optionId == CiteWord ? StatementEvidence : optionId == CiteRescue ? PowerEvidence : optionId == CiteLie ? MemoryEvidence : null;

        /// <summary>
        /// How a reply lands: the chance the voter warms to it, and the chance it backfires (DIALOGUE-PLAN §5.4,
        /// unmeasured starting values tuned only through D6). A fact is the safest ground; pressure, with or without
        /// a caught lie behind it, is the likeliest to go wrong. The exit draws nothing.
        /// </summary>
        public static bool TryOdds(string optionId, out double warm, out double sour)
        {
            switch (optionId)
            {
                case CiteWord: warm = 0.55; sour = 0.10; return true;
                case CiteRescue: warm = 0.55; sour = 0.10; return true;
                case Appeal: warm = 0.40; sour = 0.10; return true;
                case AskConcern: warm = 0.45; sour = 0.05; return true;
                case Press: warm = 0.20; sour = 0.45; return true;
                case CiteLie: warm = 0.35; sour = 0.35; return true;
                default: warm = 0; sour = 0; return false;
            }
        }

        /// <summary>How the voter and the player come to see each other after a reaction: +3, 0 or -3 (§5.4).</summary>
        public static int Delta(string reaction) => reaction == Warmed ? 3 : reaction == Backfired ? -3 : 0;

        // No shared mutable template is an authority. Validation constructs this definition afresh.
        internal static ArcTemplate CreateTemplate()
        {
            var beats = new List<BeatTemplate>
            {
                Beat(Open, DialogueCampaignV1.OpenTitle, "There is time to ask where they stand.",
                    Option(OpenCase, DialogueCampaignV1.OpenLabel, "Ask where their vote is, and make your case.",
                        "You asked where their vote is.", ByAnswer), Exit())
            };
            foreach (string beat in ReplyBeats()) beats.Add(Reply(beat));
            beats.Add(Finish(Warmed, "They warmed to you", "That landed. Their vote is still theirs to give."));
            beats.Add(Finish(Unmoved, "They heard you", "Nothing moved yet. Their vote is still theirs to give."));
            beats.Add(Finish(Backfired, "That landed badly", "It went wrong. Their vote is still theirs to give."));
            return new ArcTemplate
            {
                id = TemplateId, lane = StoryLanes.Game, eyebrow = DialogueCampaignV1.Category, title = DialogueCampaignV1.Title,
                origin = "native: dialogue rules 2's action-funded campaign appeal on the voter's word, zero-time contract",
                rulesVersion = StoryRules.Threads, minWeek = 1, minActive = 4,
                cooldownWeeks = 0, pairCooldownWeeks = 0, oncePerHeadliner = false,
                startAnchors = Array.Empty<string>(), roles = new[] { new ArcRole(DialogueCampaignV1.SpeakerRole) },
                beats = beats.ToArray()
            };
        }

        private static BeatTemplate Reply(string beatId)
        {
            string answer = AnswerOf(beatId);
            var options = new List<OptionTemplate>();
            if (answer == Word)
                options.Add(Option(CiteWord, CiteWordLabel, "Ask them to keep the vote they told you about.",
                    "You held them to their word.", ByReaction));
            else if (OffersRescue(beatId))
                options.Add(Option(CiteRescue, CiteRescueLabel, "Point to the week they used the veto to save you.",
                    "You reminded them of the rescue.", ByReaction));
            options.Add(answer == Concern
                ? Option(AskConcern, AskConcernLabel, "Let them say what they are afraid of, and answer it.",
                    "You asked what worries them.", ByReaction)
                : Option(Appeal, AppealLabel, "Tell them what staying means to you.", "You made it personal.", ByReaction));
            options.Add(OffersLie(beatId)
                ? Option(CiteLie, CiteLieLabel, "Remind them they lied to you about a vote before. It can backfire.",
                    "You brought up the last lie.", ByReaction, HouseEventRisk.Medium)
                : Option(Press, PressLabel, "Push them for a straight answer. It can backfire.", "You pressed them.",
                    ByReaction, HouseEventRisk.High));
            options.Add(Exit());
            string title = answer == Word ? "They give you their word" : answer == Against ? "They tell you straight"
                : answer == Concern ? "They have a concern" : "They keep it close";
            string summary = answer == Word ? "They say they are not voting you out."
                : answer == Against ? "They say they are voting you out."
                : answer == Concern ? "They worry what you would do if you stayed." : "They won't say where their vote is.";
            return Beat(beatId, title, summary, options.ToArray());
        }

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
