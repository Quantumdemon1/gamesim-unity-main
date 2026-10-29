using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>
    /// A fresh season's rules with the cast screen's conversation preview turned on (dialogue D1): exactly the director's
    /// for a season started with the chip on. For the recorder's converse mode, at builds that have the chip.
    /// </summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh) =>
            ShippedRules.ApplyFresh(fresh, new SeasonBuilder.Choice { CampaignConversations = true });
    }
}
