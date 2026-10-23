using System;

namespace Gamesim.Simulation
{
    /// <summary>Closed public-record campaign content. V1 remains a separate immutable pair.</summary>
    public static class DialogueCampaignV2
    {
        public const string TemplateId = "dialogue-campaign-v2";
        public const int ContentVersion = 2;
        public const string Open = "open", Case = "case", FinishRecord = "finish-record", FinishPersonal = "finish-personal";
        public const string Cite = "cite-public-rescue";
        public const string CaseTitle = "On the public record", CaseSummary = "You can point to what actually happened.";
        public const string RecordTitle = "Ask from the record", RecordSummary = "A public rescue is a fact, not a promise.";
        public const string CiteLabel = "Cite their public rescue", CiteDescription = "Point to the most recent completed week when they used the veto to save you.";
        public const string CiteOutcome = "You cited the public rescue.";

        // No ambient membership or mutable shared template authority. Zero time at every ending:
        // earned-time behavior needs a later explicit rule/content contract, never reinterpretation.
        internal static ArcTemplate CreateTemplate() => new ArcTemplate
        {
            id = TemplateId, lane = StoryLanes.Game, eyebrow = DialogueCampaignV1.Category, title = DialogueCampaignV1.Title,
            origin = "native: bounded public-rescue Strategic campaign argument, zero-time contract",
            rulesVersion = StoryRules.Threads, minWeek = 1, minActive = 4,
            cooldownWeeks = 0, pairCooldownWeeks = 0, oncePerHeadliner = false,
            startAnchors = Array.Empty<string>(), roles = new[] { new ArcRole(DialogueCampaignV1.SpeakerRole) },
            beats = new[]
            {
                Beat(Open, DialogueCampaignV1.OpenTitle, DialogueCampaignV1.OpenSummary,
                    Option(DialogueCampaignV1.OpenCase, DialogueCampaignV1.OpenLabel, DialogueCampaignV1.OpenDescription,
                        DialogueCampaignV1.OpenOutcome, Case), Exit()),
                Beat(Case, CaseTitle, CaseSummary,
                    Option(Cite, CiteLabel, CiteDescription, CiteOutcome, FinishRecord),
                    Option(DialogueCampaignV1.Appeal, DialogueCampaignV1.AppealLabel, DialogueCampaignV1.AppealDescription,
                        DialogueCampaignV1.AppealOutcome, FinishPersonal), Exit()),
                Beat(FinishRecord, RecordTitle, RecordSummary,
                    Option(DialogueCampaignV1.Plea, DialogueCampaignV1.PleaLabel, DialogueCampaignV1.PleaDescription,
                        DialogueCampaignV1.PleaOutcome, "end:answered"),
                    Option(DialogueCampaignV1.KeepOpen, DialogueCampaignV1.KeepLabel, DialogueCampaignV1.KeepDescription,
                        DialogueCampaignV1.KeepOutcome, "end:neutral"), Exit()),
                Beat(FinishPersonal, DialogueCampaignV1.FinishTitle, DialogueCampaignV1.FinishSummary,
                    Option(DialogueCampaignV1.Plea, DialogueCampaignV1.PleaLabel, DialogueCampaignV1.PleaDescription,
                        DialogueCampaignV1.PleaOutcome, "end:answered"),
                    Option(DialogueCampaignV1.KeepOpen, DialogueCampaignV1.KeepLabel, DialogueCampaignV1.KeepDescription,
                        DialogueCampaignV1.KeepOutcome, "end:neutral"), Exit())
            }
        };

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary, text = summary,
            closes = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = DialogueCampaignV1.Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next) => new OptionTemplate
        { id = id, label = label, description = description, outcome = outcome, next = next };

        private static OptionTemplate Exit() => Option(DialogueCampaignV1.Leave, DialogueCampaignV1.LeaveLabel,
            DialogueCampaignV1.LeaveDescription, DialogueCampaignV1.LeaveOutcome, "end:neutral");
    }
}
