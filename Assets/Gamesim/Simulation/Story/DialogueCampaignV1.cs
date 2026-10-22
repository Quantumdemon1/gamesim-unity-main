using System;

namespace Gamesim.Simulation
{
    /// <summary>The closed personal-appeal pilot. Fresh definitions never join the ambient pool.</summary>
    public static class DialogueCampaignV1
    {
        public const string TemplateId = "dialogue-campaign-v1", Kind = "campaign", EntryKind = "action", SpeakerRole = "speaker";
        public const int ContentVersion = 1;
        public const string Title = "Make your case", Category = "Campaign";
        public const string Open = "open", Case = "case", Finish = "finish-personal";
        public const string OpenCase = "open-case", Appeal = "appeal-personally", Plea = "make-plea", Leave = "leave", KeepOpen = "keep-vote-open";
        public const string OpenTitle = "Make your case", CaseTitle = "A personal appeal", FinishTitle = "Ask for their vote";
        public const string OpenSummary = "There is time to make a case.", CaseSummary = "The conversation becomes personal.", FinishSummary = "A vote is still a choice.";
        public const string OpenLabel = "Make your case", AppealLabel = "Appeal personally", PleaLabel = "Make the plea", LeaveLabel = "Leave it there", KeepLabel = "Keep their vote open";
        public const string OpenDescription = "Ask to be heard before the vote.", AppealDescription = "Explain what staying means to you.", PleaDescription = "Ask for a vote to keep you.";
        public const string LeaveDescription = "End the conversation respectfully.", KeepDescription = "Leave their vote unpromised.";
        public const string OpenOutcome = "You opened your case.", AppealOutcome = "You made a personal appeal.", PleaOutcome = "You asked for their vote.";
        public const string LeaveOutcome = "You left the conversation respectfully.", KeepOutcome = "You left their vote open.";
        public const string LapsedOutcome = "The conversation ended when the occasion closed.";

        // No shared mutable template is an authority. Validation constructs this definition afresh.
        internal static ArcTemplate CreateTemplate() => new ArcTemplate
        {
            id = TemplateId, lane = StoryLanes.Game, eyebrow = Category, title = Title,
            origin = "native: the bounded ordinary-funded Emotional campaign pilot",
            rulesVersion = StoryRules.Threads, minWeek = 1, minActive = 4,
            cooldownWeeks = 0, pairCooldownWeeks = 0, oncePerHeadliner = false,
            startAnchors = Array.Empty<string>(), roles = new[] { new ArcRole(SpeakerRole) },
            beats = new[]
            {
                Beat(Open, OpenTitle, OpenSummary,
                    Option(OpenCase, OpenLabel, OpenDescription, OpenOutcome, Case), Exit()),
                Beat(Case, CaseTitle, CaseSummary,
                    Option(Appeal, AppealLabel, AppealDescription, AppealOutcome, Finish), Exit()),
                Beat(Finish, FinishTitle, FinishSummary,
                    Option(Plea, PleaLabel, PleaDescription, PleaOutcome, "end:answered"),
                    Option(KeepOpen, KeepLabel, KeepDescription, KeepOutcome, "end:neutral"), Exit())
            }
        };

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary, text = summary,
            closes = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next) => new OptionTemplate
        {
            id = id, label = label, description = description, outcome = outcome, next = next
        };

        private static OptionTemplate Exit() => Option(Leave, LeaveLabel, LeaveDescription, LeaveOutcome, "end:neutral");
    }
}
