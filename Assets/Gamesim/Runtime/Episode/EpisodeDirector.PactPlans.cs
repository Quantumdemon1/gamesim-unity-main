using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The war room on screen (WAVE-D-NPC-PACTS-PLAN §3, D3-S4): an open plan of a pact the player is in,
    /// drawn in any conversation with somebody who was at its meeting and is still in the pact and the
    /// house, after C7's counter card (D3-L3) - the counter lapses at the next line the player hears,
    /// this answer's included, while the plan stands until the campaign closes. The heading names the
    /// pact's plan, each member's say is a line with the player's own read of their vote in a word, and
    /// the answers are rows: go with the plan, or settle a split; push for the other nominee once; lie low.
    ///
    /// <para>Every caption is new and unique among the conversation's live controls: one plan is drawn
    /// a conversation, and none of these words begins any other control's. No number is shown - the
    /// odds of anybody coming round are theirs - and closing the conversation leaves the plan open.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The answers' captions: "Go with the pact: evict Maya Hassan", "Settle it: evict Maya Hassan", "Push for Alex Moore instead", "Lie low on the plan".</summary>
        public static string PlanAgreeCaption(string name) => "Go with the pact: evict " + name;
        public static string PlanSettleCaption(string name) => "Settle it: evict " + name;
        public static string PlanCounterCaption(string name) => "Push for " + name + " instead";
        public const string PlanLieLowCaption = "Lie low on the plan";

        /// <summary>The pill on the meeting's row when the meeting would be a war room, by what the player can see.</summary>
        public const string PlanTag = "plan";

        /// <summary>Each answer's pill: answering costs nothing, as answering any offer does.</summary>
        public const string PlanAnswerTag = FreeTag;

        /// <summary>
        /// The pact whose open plan a conversation with this houseguest draws: the first of the player's
        /// standing pacts, in the house's order, whose plan is open this week and could be answered through
        /// them (they were at the meeting and are still in the pact and the house). Null for none, and
        /// always without the war rooms.
        /// </summary>
        public static AllianceState PlanCardPact(EpisodeState state, string npcId)
        {
            if (!EpisodeEngine.PactPlanRulesOn(state) || string.IsNullOrEmpty(npcId)) return null;
            return state.alliances.FirstOrDefault(pact => pact.active && pact.members.Contains(state.playerId) && pact.members.Contains(npcId)
                && PactPlans.OpenPlan(state, pact.id) != null && PactPlans.AnswerRefusal(state, pact, npcId, null) == null);
        }

        /// <summary>
        /// The answers an open plan offers, by caption and the nominee each names (null to lie low): going
        /// with the members' plan where there is one and it is not the player, or settling a split on either
        /// nominee but the player; pushing for the other nominee when the plan stands and the other is not
        /// the player; and lying low, always. With no say left standing, lying low alone. A read for tests.
        /// </summary>
        public static System.Collections.Generic.List<(string caption, string nomineeId)> PlanAnswers(EpisodeState state, AllianceState pact)
        {
            var answers = new System.Collections.Generic.List<(string caption, string nomineeId)>();
            var row = pact == null ? null : PactPlans.OpenPlan(state, pact.id);
            if (row == null) return answers;
            var standing = PactPlans.StandingSays(state, pact, row);
            string plan = PactPlans.MembersPlan(standing);
            string Name(string id) => state.Find(id)?.name ?? id;
            if (standing.Count > 0 && plan != null)
            {
                if (plan != state.playerId) answers.Add((PlanAgreeCaption(Name(plan)), plan));
                string other = state.nominees.FirstOrDefault(id => id != plan);
                if (other != null && other != state.playerId) answers.Add((PlanCounterCaption(Name(other)), other));
            }
            else if (standing.Count > 0)
                foreach (string nominee in state.nominees.Where(id => id != state.playerId))
                    answers.Add((PlanSettleCaption(Name(nominee)), nominee));
            answers.Add((PlanLieLowCaption, null));
            return answers;
        }

        /// <summary>
        /// An open plan this houseguest can be answered through (<see cref="PlanCardPact"/>), drawn after the
        /// counter card: its heading, each say with the player's read of the member's vote, and the answers.
        /// True when there was one.
        /// </summary>
        private bool PactPlanCard(EpisodeState state, ContestantState npc)
        {
            var pact = PlanCardPact(state, npc.id);
            if (pact == null) return false;
            var row = PactPlans.OpenPlan(state, pact.id);
            hud.Heading(PactPlans.Heading(pact.name));
            foreach (var line in PactPlans.CardFacts(state, pact, row)) hud.Paragraph(PactPlans.CardText(line));
            string through = npc.id, pactId = pact.id;
            foreach (var (caption, nominee) in PlanAnswers(state, pact))
            {
                string named = nominee;
                hud.Tag(hud.Action(caption, () => Commit(state, EpisodeCommandKind.AnswerPactPlan, through, named, text: pactId)), PlanAnswerTag);
            }
            return true;
        }
    }
}
