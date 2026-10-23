using System;
using System.Collections.Generic;
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
                bool paidCampaign = DialogueRules.On(state) && state.phase == EpisodePhase.Campaign
                    && ask == LobbyAsk.Vote && forYourself;
                if (paidCampaign)
                    hud.Paragraph("A desperate plea starts a conversation for one action. Its replies and final plea cost no further actions; you can walk away and return.");
                foreach (string approach in LobbyApproach.All)
                {
                    string how = approach;
                    string tag = KnownOdds.Plea(state, decider, ask, subject, how).word
                        + (how == LobbyApproach.Deal ? " · binds you next week" : how == LobbyApproach.Pressure ? " · can backfire" : "");
                    bool entry = paidCampaign && how == LobbyApproach.Emotional;
                    bool available = !entry || DialogueRules.TryBuildEntryCommand(state, decider, out _);
                    string caption = EpisodeHud.LobbyApproachCaption(how, forYourself);
                    var button = available ? hud.Action(caption, () =>
                    {
                        ClearLobbyDraft();
                        if (entry)
                        {
                            if (DialogueRules.TryBuildEntryCommand(state, decider, out var plan))
                                Commit(state, plan.kind, plan.targetId, plan.secondTargetId, text: plan.text);
                        }
                        else Commit(state, EpisodeCommandKind.Lobby, decider, subject, text: LobbyAsk.Encode(ask, how));
                    }) : hud.LockedAction(caption);
                    // A locked entry says why, in CanStart's own words: an active dialogue, a used occasion, an
                    // unanswered house moment or a full path each read differently. The caption stays as it is.
                    string refusal = null;
                    if (!available && DialogueRules.CanStart(state, decider, out refusal)) refusal = null;
                    hud.Tag(button, available ? tag : string.IsNullOrEmpty(refusal) ? "This plea cannot start now" : refusal.TrimEnd('.'));
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
                OfferDecline(state, offer);
            }
        }

        /// <summary>The offer board's line at the veto decision, under its eyebrow (ROLE-MOMENTS-PLAN §5.1).</summary>
        public const string VetoOfferBoardLine = "Houseguests came to you before your veto decision.";

        /// <summary>
        /// The same asks as <see cref="VetoOffers"/>, on the offer board (ROLE-MOMENTS-PLAN R2): a card each, in the
        /// order the block was named rather than the order they were filed in (<see cref="RoleOffers.VetoAsks"/>).
        /// Each card's answers do what the rows' do - the free answer, committed as it is pressed - and a card whose
        /// yes the veto cannot keep says why in the rows' own words (<see cref="VetoSavesOneLine"/>) over a disabled
        /// yes. Drawn only under <see cref="EpisodeEngine.RolesOn"/>, at the meeting and in the diary's veto column.
        /// Its head says that silence costs more than a no (<see cref="RoleOffers.SilenceLine"/>), which the engine
        /// makes true for these asks under the role moments (R3): a no costs its tone's, silence more at the decision.
        /// After the nominees' cards stands the Head of Household's (R4, <see cref="RoleOffers.BoardAsks"/>), asking the
        /// player to leave the block alone, answered the same way; a word on the veto given locks the other cards' yes,
        /// each saying why (<see cref="VetoWordLockLine"/>).
        /// </summary>
        private void VetoOfferBoard(EpisodeState state)
        {
            var cards = new List<EpisodeHud.OfferBoardCard>();
            foreach (var offer in RoleOffers.BoardAsks(state))
            {
                var words = RoleOffers.CardOf(state, offer);
                if (words == null) continue;
                string id = offer.id;
                cards.Add(new EpisodeHud.OfferBoardCard
                {
                    Card = words, Offer = offer,
                    LockReason = VetoWordLockLine(state, offer),
                    Accept = () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: EpisodeEngine.AcceptDeal),
                    Decline = () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: "decline"),
                });
            }
            hud.OfferBoard(state, new EpisodeHud.OfferBoardSpec { Line = VetoOfferBoardLine, Silence = RoleOffers.SilenceLine, Cards = cards, Redraw = Render });
        }

        /// <summary>
        /// Whether the offer board is up in the player's veto decision: the role moments, the player holding the
        /// veto before the meeting, and an ask waiting on them - a nominee's, or the Head of Household's (R4). The
        /// diary says then that an answer saves as it is pressed, as the pitches' do.
        /// </summary>
        public static bool VetoOfferBoardShows(EpisodeState state) =>
            state != null && EpisodeEngine.RolesOn(state) && state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
            && state.vetoHolderId == state.playerId && RoleOffers.BoardAsks(state).Count > 0;

        /// <summary>The pitch board's line under its eyebrow (ROLE-MOMENTS-PLAN R5).</summary>
        public const string PitchBoardLine = "Houseguests came to you before your nominations.";

        /// <summary>
        /// Whether the Head of Household's pitches are a board (ROLE-MOMENTS-PLAN R5): the role moments' pitch board
        /// (<see cref="HoHPitches.OnTheBoard"/>) - the player Head of Household before naming anybody - with a pitch waiting.
        /// Without it the pitches come one at a time, as they always did. The rule is <see cref="RoleOffers.PitchBoardShows"/>'s,
        /// in the simulation, where the subset tests it.
        /// </summary>
        public static bool PitchBoardShows(EpisodeState state) => RoleOffers.PitchBoardShows(state);

        /// <summary>
        /// The Head of Household's pitches on the offer board (ROLE-MOMENTS-PLAN R5, §3.1): at most three cards, in the order
        /// the board was dealt (<see cref="RoleOffers.PitchBoardCards"/>), each the pitcher's face, tone, situation, read, line,
        /// recommendation, stakes, history and - once felt out - what they said, over the pitch's four answers under their own
        /// captions, unchanged: "Promise safety", "Hear them without committing", "Turn them down" and "Feel them out (free)".
        /// Any card can be answered first; each answer commits as it is pressed, as the pitch's always did. A promise the record
        /// cannot hold is drawn locked under a line saying why, and a pitcher already felt out has the question locked, their
        /// answer on the card. The head says no word about silence: an unanswered pitch clears at the nominations, at no cost.
        /// </summary>
        private void PitchOfferBoard(EpisodeState state)
        {
            object view = BeginReplyChoices();
            var cards = new List<EpisodeHud.OfferBoardCard>();
            foreach (var pitch in RoleOffers.PitchBoardCards(state))
            {
                var words = RoleOffers.PitchCard(state, pitch);
                if (words == null) continue;
                string id = pitch.id;
                bool promiseLocked = !HoHPitches.CanPromiseSafety(state, pitch.fromId), assessed = HoHPitches.Assessed(state, pitch);
                var answers = ReplyCards.Replies(ReplyCards.Pitch).Select(reply =>
                {
                    bool locked = reply.Key == HoHPitches.PromiseKey && promiseLocked;
                    return new EpisodeHud.OfferBoardAnswer
                    {
                        Caption = EpisodeHud.ReplyCaption(reply.Label), Key = reply.Key, Locked = locked,
                        Choose = locked ? null : ReplyChoice(state, id, reply.Key, view),
                    };
                }).ToList();
                answers.Add(new EpisodeHud.OfferBoardAnswer
                {
                    Caption = EpisodeHud.FeelOutPitchCaption, Key = HoHPitches.FeelOutKey, Locked = assessed,
                    Choose = assessed ? null : ReplyChoice(state, id, HoHPitches.FeelOutKey, view),
                });
                cards.Add(new EpisodeHud.OfferBoardCard
                {
                    Card = words, LockReason = promiseLocked ? HoHPitches.PromiseRecordFull : null, Answers = answers,
                });
            }
            hud.OfferBoard(state, new EpisodeHud.OfferBoardSpec { Eyebrow = EpisodeHud.PitchBoardEyebrow, Line = PitchBoardLine, Cards = cards, Redraw = Render });
        }

        /// <summary>
        /// An offer's accept. For a nominee's veto ask once the player has given another nominee
        /// their word on the veto this week, the same caption drawn locked, under a line saying why
        /// and what becomes of the offer: the veto saves one of them, so a second yes is a word that
        /// cannot be kept (X13). Nothing is answered for the player - declining would cost them a
        /// little with the nominee, and that is theirs to choose - so the decline beside it stays as
        /// it was, and an offer left alone lapses as any other does. Under the role moments the Head of
        /// Household's ask to keep the block is a word on the veto too (R4, <see cref="VetoWordLockLine"/>).
        /// </summary>
        private Button OfferAccept(EpisodeState state, DealState offer)
        {
            string id = offer.id;
            // An invitation that would bring the player into a fourth pact (ACTIONS-DEALS-ALLIANCES-PLAN
            // C4, decision 10): the engine refuses the yes, so it is drawn locked under a line saying why.
            if (offer.type == DealKind.AllianceInvite && EpisodeEngine.InvitationPastPactCap(state, offer.proposerId))
            {
                hud.Paragraph(PactCapOfferLine(state, offer.proposerId));
                return hud.DescribeOfferControl(hud.LockedAction(EpisodeHud.DealAcceptCaption), state, offer, true);
            }
            string locked = VetoWordLockLine(state, offer);
            if (locked == null)
                return hud.DescribeOfferControl(hud.ActionFor(id, EpisodeHud.DealAcceptCaption,
                    () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: EpisodeEngine.AcceptDeal)), state, offer, true);
            hud.Paragraph(locked);
            return hud.DescribeOfferControl(hud.LockedAction(EpisodeHud.DealAcceptCaption), state, offer, true);
        }

        /// <summary>
        /// Why an offer's yes is locked as a second word on the veto this week, in words, or null: the one test the rows,
        /// the board and the season verification draw (<see cref="Negotiation.VetoWordLockLine"/>). A nominee's veto ask
        /// once the player gave another nominee their word, as it always was; and under the role moments
        /// (ROLE-MOMENTS-PLAN R4), where the engine refuses any second word on the veto, a nominee's ask once the player
        /// gave the Head of Household their word to keep the block, and the Head of Household's ask once they gave a
        /// nominee theirs.
        /// </summary>
        public static string VetoWordLockLine(EpisodeState state, DealState offer) => Negotiation.VetoWordLockLine(state, offer);

        private UnityEngine.UI.Button OfferDecline(EpisodeState state, DealState offer)
        {
            string id = offer.id;
            return hud.DescribeOfferControl(hud.ActionFor(id, EpisodeHud.DealDeclineCaption,
                () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: "decline")), state, offer, false);
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
        /// The nominee on this week's block the player has already given their word on the veto to, other than
        /// <paramref name="askingId"/>, or null (<see cref="Negotiation.VetoPromisedTo"/>).
        /// </summary>
        public static string VetoPromisedTo(EpisodeState state, string askingId) => Negotiation.VetoPromisedTo(state, askingId);

        /// <summary>
        /// Above the other nominee's locked accept, once the player has said yes to one veto ask: why it is locked, and
        /// both ways the offer can still end (<see cref="Negotiation.VetoSavesOneLine"/>).
        /// </summary>
        public static string VetoSavesOneLine(EpisodeState state, string promisedId, string askingId) =>
            Negotiation.VetoSavesOneLine(state, promisedId, askingId);

        /// <summary>
        /// A houseguest who came to the player, waiting on an answer. Drawn before the week's
        /// situation, one card at a time; true when there was one.
        /// </summary>
        private bool PendingReplyCard(EpisodeState state, bool privateRoom = false)
        {
            var card = ReplyCards.Pending(state);
            if (card == null) return false;
            string cardId = card.id;
            if (privateRoom) { hud.Heading("Houseguest pitch"); hud.Paragraph(ReplyCards.Title(state, card)); hud.Paragraph(ReplyCards.Message(state, card)); }
            else hud.HouseEventHeader(ReplyCards.Title(state, card), ReplyCards.Message(state, card), EpisodeHud.ReplyCardEyebrow);
            object replyView = BeginReplyChoices();
            hud.EventChoices(ReplyCards.Replies(card.kind).Select(reply =>
            {
                string key = reply.Key;
                return (EpisodeHud.ReplyCaption(reply.Label), ReplyCardPayoffs.Description(state, card, reply), EpisodeHud.RiskTag(reply.Risk),
                    key == "promise-safety" && !HoHPitches.CanPromiseSafety(state, card.fromId) ? null : ReplyChoice(state, cardId, key, replyView));
            }).ToList());
            if (card.kind == ReplyCards.Pitch)
            {
                hud.Paragraph(HoHPitches.Assessment(state, card) ?? "Ask why they are recommending this. Asking makes no new promise.");
                if (HoHPitches.Assessed(state, card)) hud.LockedAction(EpisodeHud.FeelOutPitchCaption);
                else hud.Action(EpisodeHud.FeelOutPitchCaption, ReplyChoice(state, cardId, HoHPitches.FeelOutKey, replyView));
            }
            return true;
        }
    }
}
