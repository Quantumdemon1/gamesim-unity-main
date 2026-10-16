using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>
    /// A fresh season's rules at the schema-28 builds with Wave D4's leaks and not yet D3's war rooms (92993662 to
    /// a6390a98^, 2026-10-08), transcribed line for line from their EpisodeDirector.StartSeason: schema 26's, with the
    /// alliance leaks after the commitment rules.
    /// </summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh)
        {
            fresh.competitionRulesVersion = CompetitionRules.Current;
            fresh.haveNotRulesStartWeek = 1;
            fresh.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(fresh);
            EpisodeEngine.EnableRead(fresh);
            EpisodeEngine.EnableLevers(fresh);
            EpisodeEngine.EnableWeek(fresh);
            EpisodeEngine.EnableEconomy(fresh);
            EpisodeEngine.EnableAgency(fresh);
            EpisodeEngine.EnableFinale(fresh);
            EpisodeEngine.EnableCommitments(fresh);
            EpisodeEngine.EnableAllianceLeaks(fresh);
            fresh.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            fresh.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
        }
    }
}
