using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The real-alumni rule, and the other questions casting asks about a person.
    ///
    /// <para>About half the roster are real Big Brother players under their real names. The story
    /// system never invents a romance, a secret, a blow-up, a strike or a removal about an
    /// identifiable person (20 §3.10). "Real" means a card from the All-Stars roster whose name is
    /// still the card's: a custom houseguest built from an alumni card counts as real until it is
    /// renamed, and so does <b>the player</b> on an alumni card, who keeps the real name.</para>
    /// </summary>
    public static class StoryPeople
    {
        /// <summary>What a role asks of whoever plays it.</summary>
        public enum Sensitivity
        {
            /// <summary>Game positions only: swing vote, pawn, backdoor target, ally in a game arc. Real people allowed.</summary>
            Game,
            /// <summary>Personal life, invented backstory, secrets. Fictional people only.</summary>
            Personal,
            /// <summary>Romance. Fictional people only, and never a player on an alumni card.</summary>
            Romance,
            /// <summary>Conduct: blow-ups, strikes, removal. Fictional people only.</summary>
            Conduct,
        }

        /// <summary>The template a houseguest was built from, if any: <c>sourceTemplateId</c>, else an id that names one.</summary>
        public static CastTemplates.Template TemplateOf(ContestantState who)
        {
            if (who == null) return null;
            var id = who.sourceTemplateId ?? who.id;
            return string.IsNullOrEmpty(id) ? null : CastTemplates.Find(id);
        }

        /// <summary>
        /// Whether this is a real, identifiable person: an All-Stars card whose name is still the
        /// card's name. The player's own card is covered by the same rule.
        /// </summary>
        public static bool IsRealPerson(ContestantState who)
        {
            var template = TemplateOf(who);
            return template != null && template.Roster == CastTemplates.Roster.AllStars
                   && string.Equals(template.Name, who.name, StringComparison.Ordinal);
        }

        public static bool IsRealPerson(EpisodeState state, string id) => IsRealPerson(state?.Find(id));

        /// <summary>Whether somebody may play a role that asks this much of them.</summary>
        public static bool Allowed(EpisodeState state, string id, Sensitivity needs)
        {
            var who = state?.Find(id);
            if (who == null) return false;
            if (needs == Sensitivity.Game) return true;
            return !IsRealPerson(who);
        }

        /// <summary>
        /// Whether the player may be in a story that asks this much. The player on an identity-intact
        /// alumni card gets game arcs only; renaming the card lifts it.
        /// </summary>
        public static bool PlayerAllowed(EpisodeState state, Sensitivity needs) =>
            needs == Sensitivity.Game || !IsRealPerson(state?.Find(state.playerId));

        /// <summary>Active houseguests other than the player, in cast order.</summary>
        public static List<ContestantState> ActiveNpcs(EpisodeState state) =>
            state.Active.Where(c => !c.isPlayer).ToList();

        /// <summary>The player's warmest active houseguest meeting a condition, ties broken by id.</summary>
        public static ContestantState Warmest(EpisodeState state, Func<ContestantState, bool> where = null) =>
            ActiveNpcs(state).Where(c => where == null || where(c))
                .OrderByDescending(c => state.Score(state.playerId, c.id)).ThenBy(c => c.id, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>The player's coldest active houseguest meeting a condition, ties broken by id.</summary>
        public static ContestantState Coldest(EpisodeState state, Func<ContestantState, bool> where = null) =>
            ActiveNpcs(state).Where(c => where == null || where(c))
                .OrderBy(c => state.Score(state.playerId, c.id)).ThenBy(c => c.id, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>The NPC Head of Household, if the Head of Household is not the player.</summary>
        public static ContestantState NpcHoh(EpisodeState state)
        {
            var hoh = state.Find(state.hohId);
            return hoh != null && !hoh.isPlayer && hoh.status == ContestantStatus.Active ? hoh : null;
        }

        /// <summary>The mutual warmth of a pair: the lower of the two directed scores.</summary>
        public static double Mutual(EpisodeState state, string a, string b) =>
            Math.Min(state.Score(a, b), state.Score(b, a));

        /// <summary>
        /// What a houseguest wants from a conversation with the player: the web's <c>inferIntent</c>
        /// (<c>npc-social-behavior.ts:1091-1180</c>), checked in its order. A trigger input only - the
        /// text never states it, and "Read them" is how a player learns it. Trust is the web's default,
        /// 50 plus half the score.
        /// </summary>
        public static string Intent(EpisodeState state, string npcId)
        {
            var npc = state?.Find(npcId);
            if (npc == null || npc.isPlayer) return Intents.BuildRapport;
            string player = state.playerId;
            double trust = 50 + state.Score(npcId, player) * 0.5;
            bool allied = state.Allied(npcId, player);
            if (allied && trust < 45) return Intents.TestLoyalty;
            if (allied) return Intents.CoordinateStrategy;
            if (Grudges.Severity(state, npcId, player) > 50) return Intents.GatherIntel;
            if (state.hohId == npcId) return Intents.AssessThreat;
            if (state.nominees.Contains(npcId) && EpisodeEngine.Voters(state).Any(v => v.isPlayer)) return Intents.CampaignSubtly;
            if (state.story?.grudges.Any(g => g.holderId == npcId && g.targetId == player && g.cause == GrudgeCauses.LieDiscovered) == true)
                return Intents.ProbeDeception;
            string lead = NpcSocialActions.LeadTrait(npc);
            if (state.Score(npcId, player) < -20 && (lead == "Confrontational" || lead == "Competitive")) return Intents.Intimidate;
            return Intents.BuildRapport;
        }

        /// <summary>The web's eight conversational intents, and how often each gives itself away.</summary>
        public static class Intents
        {
            public const string TestLoyalty = "test_loyalty", CoordinateStrategy = "coordinate_strategy", GatherIntel = "gather_intel";
            public const string AssessThreat = "assess_threat", CampaignSubtly = "campaign_subtly", ProbeDeception = "probe_deception";
            public const string Intimidate = "intimidate", BuildRapport = "build_rapport";

            /// <summary>The web's reveal chance: how easily a player reads the intent, as a percentage.</summary>
            public static int RevealChance(string intent)
            {
                switch (intent)
                {
                    case TestLoyalty: return 15;
                    case CoordinateStrategy: return 60;
                    case GatherIntel: return 10;
                    case AssessThreat: return 20;
                    case CampaignSubtly: return 30;
                    case ProbeDeception: return 10;
                    case Intimidate: return 40;
                    default: return 80;
                }
            }
        }

        /// <summary>A pronoun set's forms: subject, object, possessive, reflexive.</summary>
        public static (string they, string them, string their, string themselves) Pronouns(ContestantState who)
        {
            var p = (who?.pronouns ?? "they/them").ToLowerInvariant();
            if (p.StartsWith("she")) return ("she", "her", "her", "herself");
            if (p.StartsWith("he")) return ("he", "him", "his", "himself");
            return ("they", "them", "their", "themselves");
        }
    }
}
