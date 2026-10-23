using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        public const string ConnectCategoryCaption = "Get to know them";
        public const string GameCategoryCaption = "This week's game";
        public const string LearnCategoryCaption = "Learn and share";
        public const string DealsCategoryCaption = "Deals and promises";
        public const string PressureCategoryCaption = "Pressure and rumours";
        public const string TogetherCategoryCaption = "Do something together";
        public const string ConversationTopicsBackCaption = "Back to topics";

        private string conversationCategory, conversationMenuFor, conversationMenuSession;
        private int conversationMenuRevision;
        private object conversationMenuToken;
        private bool conversationNavigationShowing;
        private string invitedDealId;

        public string CurrentConversationCategory => focusedNpc != null ? conversationCategory : null;

        private void ResetConversationNavigation()
        {
            conversationCategory = conversationMenuFor = conversationMenuSession = invitedDealId = null;
            conversationMenuToken = null;
            conversationNavigationShowing = false;
        }

        private void PrepareConversationNavigation(EpisodeState state, ContestantState npc)
        {
            if (conversationMenuFor != npc.id || conversationMenuSession != state.sessionId)
            {
                conversationMenuFor = npc.id;
                conversationMenuSession = state.sessionId;
                conversationCategory = conversationIntent == IntentAsk ? LearnCategoryCaption
                    : conversationIntent == IntentDeal ? DealsCategoryCaption : null;
                conversationMenuToken = new object();
            }
            if (conversationMenuRevision != state.revision)
            {
                conversationMenuRevision = state.revision;
                conversationMenuToken = new object();
                ClearLobbyDraft();
                conversationPick = null;
            }
            // Every rebuilt view owns its controls; old controls cannot replace the new view.
            conversationMenuToken = new object();
        }

        private bool ConversationNavigationCurrent(object token, EpisodeState captured, string npcId) =>
            token != null && ReferenceEquals(token, conversationMenuToken) && conversationNavigationShowing
            && focusedNpc != null && focusedNpc.Id == npcId && projected != null
            && !blockedRecovery && !IsConversationExchangePlaying
            && projected.sessionId == captured.sessionId && projected.revision == captured.revision
            && projected.phase == captured.phase;

        private void ChooseConversationCategory(object token, EpisodeState captured, string npcId, string category)
        {
            if (!ConversationNavigationCurrent(token, captured, npcId)) return;
            conversationCategory = category;
            invitedDealId = null;
            conversationMenuToken = new object();
            ClearLobbyDraft(); conversationPick = null;
            ForgetInformationShare(); nomineeIntelView = null;
            lastSocialAction = null; conversationNarration = null;
            Render();
        }

        private bool BackConversationNavigation(bool toTopics = false)
        {
            // Back while a line plays is one level, as everywhere in the conversation (PROGRAM.md:14):
            // it skips the line, as the exchange's own Skip does, and leaves the player on the page it
            // was said from. It used to fall through and close the whole conversation (PR-5).
            if (!toTopics && focusedNpc != null && IsConversationExchangePlaying)
            {
                if (!ExchangeOwns(projected)) return false;
                SkipConversationExchange(exchangeSession, exchangeRevision, exchangeStep);
                return true;
            }
            if (!conversationNavigationShowing || focusedNpc == null
                || IsConversationExchangePlaying) return false;
            if (conversationCategory == null)
            {
                if (acceptedExchangeWasPresented || string.IsNullOrEmpty(conversationNarration)) return false;
                conversationNarration = null; lastSocialAction = null;
                conversationMenuToken = new object();
                Render();
                return true;
            }
            if (!toTopics && ConversationPicker != null) conversationPick = null;
            else if (!toTopics && lobbyAsk != null) ClearLobbyDraft();
            else
            {
                conversationCategory = null; invitedDealId = null; conversationIntent = null;
                // Returning from a category has already shown its narrated result. The
                // root topics must not reopen that result as another intermediate view.
                conversationNarration = null; lastSocialAction = null;
            }
            if (toTopics) { conversationPick = null; ClearLobbyDraft(); }
            conversationMenuToken = new object();
            Render();
            return true;
        }

        private void RenderConversationNavigation(EpisodeState state, ContestantState npc, bool window)
        {
            PrepareConversationNavigation(state, npc);
            conversationNavigationShowing = true;
            object token = conversationMenuToken;
            if (conversationCategory == null && !acceptedExchangeWasPresented
                && !string.IsNullOrEmpty(conversationNarration))
            {
                hud.SpeakerTitle(npc.id, npc.name, npc.pronouns);
                hud.Heading("WHAT HAPPENED");
                hud.Paragraph(conversationNarration);
                hud.Action(ConversationTopicsBackCaption, () =>
                {
                    if (ConversationNavigationCurrent(token, state, npc.id)) BackConversationNavigation(true);
                });
                if (cameraRig != null) cameraRig.ConversationWindowOffset = hud.ConversationWindowOffset;
                hud.GuardConversationChoices(() => ConversationNavigationCurrent(token, state, npc.id));
                return;
            }
            if (conversationCategory == null)
            {
                hud.ConversationTopicsWheel(npc.id, 6, npc.name, npc.pronouns,
                    Standing(state, npc.id), ActionsLeft(state) + " left");
                if (cameraRig != null) cameraRig.ConversationWindowOffset = 0f;
                UnityEngine.UI.Button Topic(string caption, string icon, UnityEngine.Color tint) =>
                    hud.Petal(caption, icon, tint, () => ChooseConversationCategory(token, state, npc.id, caption));
                Topic(ConnectCategoryCaption, "chat", UiTheme.Accent);
                Topic(GameCategoryCaption, "bulb", UiTheme.Strategic);
                Topic(LearnCategoryCaption, "eye", UiTheme.Muted);
                var deals = Topic(DealsCategoryCaption, "handshake", UiTheme.Allied);
                // What waits under Deals is said at the topics, beside the petal and never in its
                // caption: the oath, the counter, the plan and the offers used to be drawn first in
                // the conversation, and behind a category a player could open and close it without
                // learning any of them was there (PR-6).
                int waiting = DealsWaiting(state, npc.id);
                if (waiting > 0) hud.Tag(deals, WaitingTag(waiting), EpisodeHud.TagSeat.CardFoot);
                Topic(PressureCategoryCaption, "gossip", UiTheme.Warning);
                Topic(TogetherCategoryCaption, "star", UiTheme.Joke);
                hud.GuardConversationChoices(() => ConversationNavigationCurrent(token, state, npc.id));
                return;
            }

            hud.SpeakerTitle(npc.id, npc.name, npc.pronouns + " · " + string.Join(" / ", npc.traits));
            hud.Heading(conversationCategory.ToUpperInvariant());
            string mood = npc.mood != "Neutral" ? MoodLine(state, npc) : null;
            hud.Paragraph(Standing(state, npc.id) + (mood != null ? " · " + mood.Substring("Mood: ".Length) : ""));
            hud.Paragraph(ActionsLeft(state) + " left");
            hud.Action(ConversationTopicsBackCaption, () =>
            {
                if (ConversationNavigationCurrent(token, state, npc.id)) BackConversationNavigation(true);
            });
            if (cameraRig != null) cameraRig.ConversationWindowOffset = hud.ConversationWindowOffset;
            if (!acceptedExchangeWasPresented && !string.IsNullOrEmpty(conversationNarration))
            { hud.Heading("WHAT HAPPENED"); hud.Paragraph(conversationNarration); }
            if (lastSocialAction.HasValue) hud.OutcomeChips(lastSocialDelta);

            bool allied = state.Allied(state.playerId, npc.id);
            switch (conversationCategory)
            {
                case ConnectCategoryCaption:
                    ConversationAction(state, npc, EpisodeHud.SmallTalkCaption, EpisodeCommandKind.SmallTalk);
                    ConversationAction(state, npc, EpisodeHud.PersonalChatCaption, EpisodeCommandKind.PersonalChat);
                    ConversationAction(state, npc, EpisodeHud.RelationshipBuildingCaption, EpisodeCommandKind.RelationshipBuilding);
                    ConversationAction(state, npc, EpisodeHud.ShareSecretCaption, EpisodeCommandKind.ShareSecret);
                    ConversationAction(state, npc, "Spend time together", EpisodeCommandKind.Talk);
                    break;
                case GameCategoryCaption:
                    if (StrategyRules.Pleas(state, npc.id).Count > 0
                        || state.lobbies.Any(l => l.week == state.week && l.phase == state.phase && l.deciderId == npc.id))
                        LobbyPanel(state, npc);
                    else hud.Paragraph("Talk through the game as it stands. A nomination or veto plea is offered here when this houseguest is deciding.");
                    if (lobbyDeciderId == npc.id && lobbyAsk != null
                        && StrategyRules.CanLobby(state, npc.id, lobbyAsk, lobbySubjectId, out _)) break;
                    ConversationAction(state, npc, EpisodeHud.StrategicDiscussionCaption, EpisodeCommandKind.StrategicDiscussion);
                    ConversationAction(state, npc, EpisodeHud.DiscussGameCaption, EpisodeCommandKind.DiscussGame);
                    break;
                case LearnCategoryCaption:
                    AskRows(state, npc, window);
                    InformationShareRow(state, npc);
                    break;
                case PressureCategoryCaption:
                    ConversationGroups(state, npc, window, allied, SchemeGroupTitle);
                    break;
                case DealsCategoryCaption:
                    if (RenderInvitedDeal(state, npc)) break;
                    CounterCard(state, npc);
                    PactPlanCard(state, npc);
                    if (state.oathOpportunities.Contains(npc.id))
                    {
                        hud.Heading("A PERSONAL LOYALTY DECLARATION"); hud.Paragraph(OathOfferLine(npc.name));
                        hud.Tag(hud.Action(EpisodeHud.OathDeclareCaption, () => Commit(state, EpisodeCommandKind.SwearLoyalty, npc.id)), BindsYouTag + " · " + FreeTag);
                        hud.Tag(hud.Action(EpisodeHud.OathDeclineCaption, () => Commit(state, EpisodeCommandKind.DeclineLoyalty, npc.id)), FreeTag);
                    }
                    else if (state.loyaltyOaths.Any(o => o.playerId == state.playerId && o.targetId == npc.id))
                        hud.Paragraph(OathRecordedLine(npc.name));
                    ConversationGroups(state, npc, window, allied, BargainGroupTitle);
                    break;
                case TogetherCategoryCaption:
                    hud.Paragraph("Activities cost one action. What you can do together depends on the room and this part of the week.");
                    RoomActs(state, npc);
                    break;
            }
            hud.GuardConversationChoices(() => ConversationNavigationCurrent(token, state, npc.id));
        }

        private void ConversationAction(EpisodeState state, ContestantState npc, string caption, EpisodeCommandKind kind)
        {
            bool available = EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state);
            var button = available ? hud.Action(caption, () => Commit(state, kind, npc.id)) : hud.LockedAction(caption);
            hud.Tag(button, VerbTag(state, kind) + " · " + (available ? "1 action" : "No actions left"));
        }

        private bool RenderInvitedDeal(EpisodeState state, ContestantState npc)
        {
            if (invitedDealId == null) return false;
            var offer = NpcDeals.Pending(state).FirstOrDefault(d => d.proposerId == npc.id && d.id == invitedDealId);
            if (offer == null) { invitedDealId = null; return false; }
            hud.Heading("THEIR OFFER");
            hud.Paragraph(DealSentence(state, offer));
            // A yes the engine would refuse is drawn locked, and a locked control is not a move the
            // player can make: it carries no cost word, as DealPanel leaves its locked yes (PR-7).
            var accept = OfferAccept(state, offer);
            if (accept.interactable) hud.Tag(accept, FreeTag);
            string id = offer.id;
            hud.Tag(OfferDecline(state, offer), FreeTag);
            return true;
        }

        /// <summary>
        /// How many things this houseguest has put to the player that wait under Deals and promises:
        /// each offer (the rows DealPanel and the invitation draw), a counter, a war room's open plan
        /// and a loyalty declaration - the same readers the category draws them from, so the count is
        /// only ever what the player could already see there.
        /// </summary>
        private static int DealsWaiting(EpisodeState state, string npcId)
        {
            if (state == null || string.IsNullOrEmpty(npcId)) return 0;
            int waiting = NpcDeals.Pending(state).Count(offer => offer.proposerId == npcId);
            if (Negotiation.OpenCounter(state, npcId) != null) waiting++;
            if (PlanCardPact(state, npcId) != null) waiting++;
            if (state.oathOpportunities != null && state.oathOpportunities.Contains(npcId)) waiting++;
            return waiting;
        }

        /// <summary>The Deals petal's tag while something waits there: "1 waiting", "2 waiting".</summary>
        public static string WaitingTag(int waiting) => waiting + " waiting";
    }
}
