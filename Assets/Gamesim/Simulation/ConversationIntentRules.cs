using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Wave C E2 native conversation trade-offs. The source-backed vocabulary stays unchanged for
    /// old seasons; the unreleased fresh-season economy boundary opts into these extensions.
    /// Personal chat trades warmth for lore only when the season actually has the lore system.
    /// </summary>
    public static class ConversationIntentRules
    {
        public static bool PersonalLoreOn(EpisodeState state) =>
            EpisodeEngine.EconomyRulesOn(state) && EpisodeEngine.StoryAt(state, StoryRules.Lore);

        /// <summary>Two to four base warmth, instead of the source's five to eight. One existing draw.</summary>
        public static double PersonalWarmth(EpisodeState state, double roll)
        {
            if (!PersonalLoreOn(state)) return WebSocialVocabulary.PersonalChat(roll);
            if (double.IsNaN(roll) || roll <= 0) return 2;
            if (roll >= 1) return 4;
            return 2 + Math.Floor(roll * 3);
        }

        /// <summary>At most two new, reachable facts. No extra contact, rapport or story-trigger roll.</summary>
        public static int RevealLimit(EpisodeState state, EpisodeCommandKind kind) =>
            kind == EpisodeCommandKind.PersonalChat && PersonalLoreOn(state) ? 2 : 1;
    }
}
