using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The strategy windows on screen: a plea in the conversation with whoever is deciding, the
    /// house's pointer to them, nominees asking the player for the veto, and reply cards.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The plea chosen and waiting on how to put it, and who it is for. Nothing is committed until an approach is pressed.</summary>
        private string lobbyDeciderId, lobbyAsk, lobbySubjectId;

        private void ClearLobbyDraft() { lobbyDeciderId = null; lobbyAsk = null; lobbySubjectId = null; }

        /// <summary>The heading over a plea: which decision it comes before.</summary>
        public static string LobbyHeading(EpisodeState state) =>
            state.phase == EpisodePhase.Nomination ? "A WORD BEFORE THE NOMINATIONS"
            : state.phase == EpisodePhase.Campaign ? "A WORD BEFORE THE VOTE" : "A WORD BEFORE THE VETO MEETING";

        /// <summary>What this houseguest is deciding, and what a plea costs.</summary>
        public static string LobbyLine(EpisodeState state, ContestantState npc)
        {
            if (state.phase == EpisodePhase.Campaign)
                return npc.name + " votes on eviction night. You get one plea before the vote, and it costs one of your conversations.";
            string decides = state.phase == EpisodePhase.Nomination ? npc.name + " decides who goes up this week."
                : npc.id == state.vetoHolderId && npc.id == state.hohId ? npc.name + " holds the veto, and names the replacement if it is used."
                : npc.id == state.vetoHolderId ? npc.name + " decides whether to use the veto."
                : "If the veto is used, " + npc.name + " names the replacement.";
            return decides + " You get one plea before the ceremony, and it costs one of your conversations.";
        }

        /// <summary>
        /// A plea, in the conversation with whoever is deciding: first what to ask, then how to put
        /// it, each approach with its chance beside it. Drawn above the dial, because in a window the
        /// plea is what the player came for. The chance is the player's read of the houseguest
        /// (<see cref="KnownOdds.Plea"/>), never the roll's own number, and the panel says so.
        /// </summary>
        private void LobbyPanel(EpisodeState state, ContestantState npc)
        {
            hud.Heading(LobbyHeading(state));
            var had = state.lobbies.LastOrDefault(l => l.week == state.week && l.phase == state.phase && l.deciderId == npc.id);
            if (had != null)
            {
                hud.Paragraph("You have had your plea with " + npc.name + ". They were " + had.response + ".");
                return;
            }
            if (EpisodeEngine.SocialActionsSpent(state) >= EpisodeEngine.SocialActionBudget(state))
            {
                hud.Paragraph("You have no conversations left this week to spend on a plea.");
                return;
            }
            if (lobbyDeciderId == npc.id && lobbyAsk != null && StrategyRules.CanLobby(state, npc.id, lobbyAsk, lobbySubjectId, out _))
            {
                string ask = lobbyAsk, subject = lobbySubjectId, decider = npc.id;
                hud.Paragraph(EpisodeHud.LobbyAskCaption(state, npc.name, ask, subject) + ". How do you put it?");
                OddsAreYourRead(state, npc);
                bool forYourself = subject == state.playerId;
                foreach (string approach in LobbyApproach.All)
                {
                    string how = approach;
                    string tag = KnownOdds.Plea(state, decider, ask, subject, how).word
                        + (how == LobbyApproach.Deal ? " · binds you next week" : how == LobbyApproach.Pressure ? " · can backfire" : "");
                    hud.Tag(hud.Action(EpisodeHud.LobbyApproachCaption(how, forYourself), () =>
                    {
                        ClearLobbyDraft();
                        Commit(state, EpisodeCommandKind.Lobby, decider, subject, text: LobbyAsk.Encode(ask, how));
                    }), tag);
                }
                hud.Action(EpisodeHud.LobbyBackCaption, () => { ClearLobbyDraft(); Render(); });
                return;
            }
            hud.Paragraph(LobbyLine(state, npc));
            foreach (var plea in StrategyRules.Pleas(state, npc.id))
            {
                string ask = plea.ask, subject = plea.subjectId, decider = npc.id;
                Action choose = () => { lobbyDeciderId = decider; lobbyAsk = ask; lobbySubjectId = subject; Render(); };
                string caption = EpisodeHud.LobbyAskCaption(state, npc.name, ask, subject);
                // A plea about somebody is fronted by their portrait, as a deal about somebody is, and
                // carries the player's reading of them; its tag sits left of that reading.
                if (subject == null) hud.Tag(hud.Action(caption, choose), Category(EpisodeCommandKind.Lobby));
                else hud.Tag(hud.ActionFor(subject, caption, choose), Category(EpisodeCommandKind.Lobby),
                    subject == state.playerId ? EpisodeHud.TagSeat.RowEnd : EpisodeHud.TagSeat.PastReading);
            }
        }

        /// <summary>
        /// The house's pointer, on the phase panel: who is deciding, and that there is time for a
        /// word with them. Null when no window is open.
        /// </summary>
        public static string WindowLine(EpisodeState state)
        {
            var deciders = StrategyRules.Deciders(state);
            if (deciders.Count == 0) return null;
            int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));
            string names = string.Join(" or ", deciders.Select(id => state.Find(id).name));
            string what = state.phase == EpisodePhase.Nomination ? "Nominations are " + names + "'s call."
                : "The veto meeting is next.";
            // Under the week's windows the count is the window's, and what is not spent here does not
            // carry; the old weekly pool is the week's.
            return what + " Find " + names + " in the house for a word before it happens: you have "
                + left + (left == 1 ? " conversation" : " conversations")
                + (EpisodeEngine.WeekRulesOn(state) ? " left in this window." : " left this week.");
        }

        /// <summary>
        /// Nominees asking the player for the veto, on the veto decision itself: they cannot be
        /// reached in the house before the meeting, so their question comes to the screen where it
        /// is answered.
        /// </summary>
        private void VetoOffers(EpisodeState state)
        {
            foreach (var offer in NpcDeals.Pending(state).Where(d => d.type == DealKind.VetoUse && d.week == state.week))
            {
                string id = offer.id;
                var from = state.Find(offer.proposerId);
                if (from == null || from.status != ContestantStatus.Active) continue;
                hud.Heading("AN OFFER FROM " + from.name.ToUpperInvariant());
                hud.Paragraph(DealSentence(state, offer));
                OfferAccept(state, offer);
                hud.ActionFor(id, EpisodeHud.DealDeclineCaption,
                    () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: "decline"));
            }
        }

        /// <summary>
        /// An offer's accept. For a nominee's veto ask once the player has given another nominee
        /// their word on the veto this week, the same caption drawn locked, under a line saying why
        /// and what becomes of the offer: the veto saves one of them, so a second yes is a word that
        /// cannot be kept (X13). Nothing is answered for the player - declining would cost them a
        /// little with the nominee, and that is theirs to choose - so the decline beside it stays as
        /// it was, and an offer left alone lapses as any other does.
        /// </summary>
        private Button OfferAccept(EpisodeState state, DealState offer)
        {
            string id = offer.id;
            // An invitation that would bring the player into a fourth pact (ACTIONS-DEALS-ALLIANCES-PLAN
            // C4, decision 10): the engine refuses the yes, so it is drawn locked under a line saying why.
            if (offer.type == DealKind.AllianceInvite && EpisodeEngine.InvitationPastPactCap(state, offer.proposerId))
            {
                hud.Paragraph(PactCapOfferLine(state, offer.proposerId));
                return hud.LockedAction(EpisodeHud.DealAcceptCaption);
            }
            string promised = offer.type == DealKind.VetoUse ? VetoPromisedTo(state, offer.proposerId) : null;
            if (promised == null)
                return hud.ActionFor(id, EpisodeHud.DealAcceptCaption,
                    () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: EpisodeEngine.AcceptDeal));
            hud.Paragraph(VetoSavesOneLine(state, promised, offer.proposerId));
            return hud.LockedAction(EpisodeHud.DealAcceptCaption);
        }

        /// <summary>
        /// Above an alliance invitation's locked yes, once the player holds three pacts (C4): why it is
        /// locked, and what is left to do with it. Turned down, it is the player's no; left alone, it
        /// waits until it lapses, which is the engine's to say when.
        /// </summary>
        public static string PactCapOfferLine(EpisodeState state, string askingId) =>
            EpisodeEngine.PactCapRefusal + " Turn " + FinalistRead.FirstName(state?.Find(askingId)?.name ?? "them")
            + " down, or leave the offer unanswered.";

        /// <summary>
        /// The nominee on this week's block the player has already given their word on the veto
        /// to, other than <paramref name="askingId"/>, or null. Read from the player's own deals.
        /// </summary>
        public static string VetoPromisedTo(EpisodeState state, string askingId) =>
            state == null ? null
                : state.deals.Where(d => d.type == DealKind.VetoUse && d.recipientId == state.playerId && d.proposerId != askingId
                        && d.week == state.week && (d.status == DealStatus.Active || d.status == DealStatus.Accepted)
                        && state.nominees.Contains(d.proposerId))
                    .Select(d => d.proposerId).FirstOrDefault();

        /// <summary>
        /// Above the other nominee's locked accept, once the player has said yes to one veto ask: why
        /// it is locked, and both ways the offer can still end. Turned down, it is the player's no;
        /// left alone, it lapses - at the veto decision from the strategy windows, which expires every
        /// unanswered veto ask, and at the end of the week before them, as every offer does.
        /// </summary>
        public static string VetoSavesOneLine(EpisodeState state, string promisedId, string askingId)
        {
            string promised = state?.Find(promisedId)?.name ?? "somebody";
            string asking = FinalistRead.FirstName(state?.Find(askingId)?.name ?? "them");
            string lapses = StrategyRules.Apply(state) && !state.vetoResolved ? "at the meeting" : "at the end of the week";
            return "The veto can only save one of them, and you have already given " + promised + " your word. Turn "
                + asking + " down, or the offer lapses " + lapses + ".";
        }

        /// <summary>
        /// A houseguest who came to the player, waiting on an answer. Drawn before the week's
        /// situation, one card at a time; true when there was one.
        /// </summary>
        private bool PendingReplyCard(EpisodeState state)
        {
            var card = ReplyCards.Pending(state);
            if (card == null) return false;
            string cardId = card.id;
            hud.HouseEventHeader(ReplyCards.Title(state, card), ReplyCards.Message(state, card), EpisodeHud.ReplyCardEyebrow);
            hud.EventChoices(ReplyCards.Replies(card.kind).Select(reply =>
            {
                string key = reply.Key;
                return (EpisodeHud.ReplyCaption(reply.Label), reply.Description, EpisodeHud.RiskTag(reply.Risk),
                    (Action)(() => Commit(state, EpisodeCommandKind.ReplyToHouseguest, cardId, text: key)));
            }).ToList());
            return true;
        }
    }
}
