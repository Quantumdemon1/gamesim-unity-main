using System.Collections.Generic;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Negotiation on screen (ACTIONS-DEALS-ALLIANCES-PLAN C7): the counter a refused proposal came back
    /// with, at the head of the conversation it was said in; the price a nominee's veto ask carries, said
    /// under the ask (<see cref="DealSentence"/>); and the web's three situation moves among the
    /// conversation's bargains - calling in a promise, mending fences, and a veto for a price.
    ///
    /// <para>Every chance shown is the player's read (<see cref="Negotiation.Chance"/> as known, the
    /// <see cref="KnownOdds"/> terms), under the note that says whose read the chances are; nothing hidden
    /// is shown. Every caption is new (<see cref="EpisodeHud.CounterAcceptCaption"/> and its kin), and no
    /// existing one moves. A season without the commitment rules draws none of it.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The pills beside the counter's answers: taking it binds the player to both deals, with no roll; turning it down costs nothing.</summary>
        public const string CounterAcceptTag = BindsYouTag + " · no roll", CounterDeclineTag = FreeTag;

        /// <summary>The pill beside mending fences, before the player's read of its chance.</summary>
        public const string MendTag = "repairs trust";

        /// <summary>The heading over a counter that stands: "A COUNTER FROM MAYA HASSAN".</summary>
        public static string CounterHeading(string name) => "A COUNTER FROM " + (name ?? "").ToUpperInvariant();

        /// <summary>
        /// The player's read of the deal they asked for, under a counter: their own read of the houseguest
        /// on it (<see cref="KnownOdds.Deal"/>), as the deal table showed it - never the number the
        /// houseguest decided on.
        /// </summary>
        public static string CounterReadLine(EpisodeState state, Negotiation.Counter counter) =>
            "Your read of " + FinalistRead.FirstName(state.Find(counter.npcId)?.name ?? "them") + " on the "
            + Negotiation.DealWords(state, counter.kind, counter.aboutId) + " alone: "
            + KnownOdds.Deal(state, counter.npcId, counter.kind, counter.aboutId).word + ".";

        /// <summary>What calling in a promise this way costs, as its pill says it: nothing, warmth, more warmth.</summary>
        public static string CallInTag(string approach) =>
            approach == Negotiation.Remind ? "no ill will" : approach == Negotiation.Demand ? "costs warmth" : "burns warmth";

        /// <summary>
        /// A counter that stands with this houseguest (<see cref="Negotiation.OpenCounter"/>), drawn first
        /// in the conversation it was said in, since it is answered there or not at all: who it is from,
        /// what they said, both deals and what each stakes, that taking it is no roll and that anything else
        /// lets it lapse, the player's own read of the deal they asked for, and the two answers - free, as
        /// answering any offer is. True when there was one.
        /// </summary>
        private bool CounterCard(EpisodeState state, ContestantState npc)
        {
            var counter = Negotiation.OpenCounter(state, npc.id);
            if (counter == null) return false;
            string first = FinalistRead.FirstName(npc.name), id = npc.id;
            hud.Heading(CounterHeading(npc.name));
            hud.Paragraph(Negotiation.CounterLine(state, counter));
            hud.Paragraph(Negotiation.CounterTermsLine(state, counter));
            hud.Paragraph(CounterReadLine(state, counter));
            hud.Tag(hud.Action(EpisodeHud.CounterAcceptCaption(first),
                    () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: EpisodeEngine.AcceptDeal)),
                CounterAcceptTag);
            hud.Tag(hud.Action(EpisodeHud.CounterDeclineCaption(first),
                    () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: "decline")),
                CounterDeclineTag);
            return true;
        }

        /// <summary>The promises this houseguest owes that can be called in, whether fences can be mended with them, and the prices the veto can be named for: what <see cref="NegotiationRows"/> draws.</summary>
        private static bool HasNegotiationRows(EpisodeState state, ContestantState npc, out List<PromiseState> owed, out bool mend, out List<string> prices)
        {
            owed = Negotiation.Owed(state, npc.id);
            mend = EpisodeEngine.CommitmentRulesOn(state) && Negotiation.MendRefusal(state, npc.id) == null;
            prices = Negotiation.VetoPrices(state, npc.id);
            return (owed.Count > 0 || mend || prices.Count > 0)
                   && EpisodeEngine.ConversationWindowRefusal(state, npc.id, EpisodeCommandKind.Negotiate) == null;
        }

        /// <summary>
        /// The web's situation moves with this houseguest, under the commitment rules and each only where it
        /// can happen: a promise they owe the player called in, three ways, each with its cost and the
        /// player's read of its chance; fences mended after a breach of the player's; and, holding the veto
        /// before the meeting, a price named to a nominee for using it on them. Each spends the window's
        /// action, as any word does; the note on the odds comes above the first.
        /// </summary>
        private void NegotiationRows(EpisodeState state, ContestantState npc)
        {
            if (!HasNegotiationRows(state, npc, out var owed, out bool mend, out var prices)) return;
            string first = FinalistRead.FirstName(npc.name), id = npc.id;
            OddsAreYourRead(state, npc);
            foreach (var promise in owed)
                foreach (string approach in Negotiation.Approaches)
                {
                    string promiseId = promise.id, how = approach;
                    hud.Tag(hud.Action(EpisodeHud.CallInCaption(how, first, Negotiation.PromiseWords(promise.kind)),
                            () => Commit(state, EpisodeCommandKind.Negotiate, id, promiseId, text: Negotiation.CallIn(how))),
                        CallInTag(how) + " · " + Negotiation.ShownWord(state, id, how));
                }
            if (mend)
                hud.Tag(hud.Action(EpisodeHud.MendFencesCaption(first),
                        () => Commit(state, EpisodeCommandKind.Negotiate, id, text: Negotiation.MendFences)),
                    MendTag + " · " + Negotiation.ShownWord(state, id, Negotiation.MendFences));
            foreach (string kind in prices)
            {
                string price = kind;
                hud.Tag(hud.Action(EpisodeHud.VetoPriceCaption(first, price),
                        () => Commit(state, EpisodeCommandKind.Negotiate, id, text: Negotiation.VetoPriceMove(price))),
                    BindsYouTag + " · " + Negotiation.ShownWord(state, id, Negotiation.VetoForAPrice));
            }
        }
    }
}
