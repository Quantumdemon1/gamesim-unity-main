using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>
    /// A fresh season's rules with the cast screen's conversation preview turned on (dialogue D1): exactly the director's
    /// for a season started with the chip on, on whatever dialogue rules that build's chip writes (DialogueRules.Offered:
    /// rules 1 at D1's build, rules 2 from the build that moved the chip). For the recorder's converse mode, at builds
    /// that have the chip.
    /// </summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh) =>
            ShippedRules.ApplyFresh(fresh, new SeasonBuilder.Choice { CampaignConversations = true });
    }
}
