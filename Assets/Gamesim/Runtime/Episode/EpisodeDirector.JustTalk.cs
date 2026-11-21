using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The free "Just talk" screen (free talk slice 1, talk rules 1): the row first on the Connect page, the tile on a
    /// houseguest's free-time screen, the talk's panel in three beats - their opening line, the player's replies one per tone,
    /// their reaction with one plain line of what changed - and the notebook page's two blocks. Everything it says comes from
    /// the engine's pure reads through <see cref="TalkScreenLines"/>; the one command is <see cref="EpisodeCommandKind.JustTalk"/>,
    /// free and once per housemate per window. At talk rules 0 nothing here is drawn and no command is sent. Nothing animates.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>What a houseguest's screen sends the player into a conversation for: the free talk, opened at once.</summary>
        public const string IntentJustTalk = "just-talk";

        /// <summary>The talk panel's way back: to the conversation's Connect page, where the row then says the window's talk is had.</summary>
        public const string JustTalkBackCaption = WayOut.BackPrefix + "the conversation";

        /// <summary>The houseguest whose talk panel is open (in the open conversation), and the season it was opened in; view state.</summary>
        private string justTalkWith, justTalkSession;

        /// <summary>The third beat on show: what the accepted command did, as the engine said before it applied it.</summary>
        private TalkResult justTalkResult;

        /// <summary>The preview of the reply being sent, and its command's id, while the command is in flight.</summary>
        private TalkResult justTalkPending;
        private string justTalkPendingCommand;

        /// <summary>The engine's reason when a reply was refused, shown under the replies.</summary>
        private string justTalkRefused;

        /// <summary>Whether the talk panel is open in the conversation. A read for tests.</summary>
        public bool IsJustTalkOpen => justTalkWith != null && focusedNpc != null && focusedNpc.Id == justTalkWith;

        /// <summary>The talk's third beat on show, or null before a reply. A read for tests.</summary>
        public TalkResult JustTalkShown => IsJustTalkOpen ? justTalkResult : null;

        private void ClearJustTalk()
        {
            justTalkWith = justTalkSession = null;
            justTalkResult = justTalkPending = null;
            justTalkPendingCommand = justTalkRefused = null;
        }

        /// <summary>A conversation opened for a free talk (a houseguest's tile) opens on its panel.</summary>
        private void PrepareJustTalk(EpisodeState state, ContestantState npc)
        {
            ClearJustTalk();
            if (conversationIntent != IntentJustTalk || !TalkScreenLines.Shown(state, npc.id)) return;
            justTalkWith = npc.id;
            justTalkSession = state.sessionId;
        }

        /// <summary>The row first on the Connect page: open while a talk is on offer, locked with the reason when not, absent when off.</summary>
        private void JustTalkRow(EpisodeState state, ContestantState npc, object token)
        {
            if (!TalkScreenLines.Shown(state, npc.id)) return;
            var offer = FreeTalk.Offer(state, npc.id);
            string id = npc.id;
            if (offer.Available)
            {
                hud.Tag(hud.Action(EpisodeHud.JustTalkCaption, () => OpenJustTalk(token, state, id)), TalkScreenLines.CostTag);
                return;
            }
            hud.Paragraph(TalkScreenLines.TileLine(offer, CharacterNames.FirstName(npc.name ?? "")));
            hud.LockedAction(EpisodeHud.JustTalkCaption);
        }

        private void OpenJustTalk(object token, EpisodeState state, string npcId)
        {
            if (!ConversationNavigationCurrent(token, state, npcId)) return;
            ClearJustTalk();
            justTalkWith = npcId;
            justTalkSession = state.sessionId;
            conversationCategory = ConnectCategoryCaption;
            conversationPick = null; lastSocialAction = null; conversationNarration = null;
            conversationMenuToken = new object();
            Render();
        }

        /// <summary>Back from the panel to the Connect page: the talk's view goes, nothing is sent.</summary>
        private void LeaveJustTalk(object token, EpisodeState state, string npcId)
        {
            if (!ConversationNavigationCurrent(token, state, npcId)) return;
            ClearJustTalk();
            conversationCategory = ConnectCategoryCaption;
            conversationMenuToken = new object();
            Render();
        }

        /// <summary>
        /// The talk's panel, in place of the Connect page while it is open: their line and the replies, or, once a reply is
        /// accepted, their reaction and what changed. False, with the panel let go, when it is not this houseguest's or the season
        /// no longer plays free talk.
        /// </summary>
        private bool RenderJustTalk(EpisodeState state, ContestantState npc, object token)
        {
            if (justTalkWith == null) return false;
            if (justTalkWith != npc.id || justTalkSession != state.sessionId || !FreeTalk.On(state)) { ClearJustTalk(); return false; }
            string first = CharacterNames.FirstName(npc.name ?? "");
            hud.SpeakerTitle(npc.id, npc.name, npc.pronouns);
            hud.Heading(TalkScreenLines.PanelHeading);
            hud.DecisionPageBack(JustTalkBackCaption, () => LeaveJustTalk(token, state, npc.id));
            if (cameraRig != null) cameraRig.ConversationWindowOffset = hud.ConversationWindowOffset;

            var shown = justTalkResult;
            if (shown != null && shown.NpcId == npc.id)
            {
                if (!string.IsNullOrEmpty(shown.Reply)) hud.Paragraph("You: “" + TalkText.Spoken(shown.Reply) + "”");
                if (!string.IsNullOrEmpty(shown.Reaction)) hud.Paragraph(first + ": " + shown.Reaction);
                hud.Heading(TalkScreenLines.ChangedHeading);
                // At talk rules 2 also the favour given or turned down, the chemistry once it first shows, and a stage reached.
                foreach (string line in TalkScreenLines.Aftermath(shown, first)) hud.Paragraph(line);
                return true;
            }

            var offer = FreeTalk.Offer(state, npc.id);
            if (!offer.Available)
            {
                hud.Paragraph(TalkScreenLines.TileLine(offer, first));
                return true;
            }
            hud.Paragraph(first + ": " + TalkScreenLines.Opener(state, npc.id, offer.Talk));
            hud.Heading(TalkScreenLines.ReplyHeading);
            string talkId = offer.Talk.Id, id = npc.id;
            foreach (var reply in TalkScreenLines.Replies(state, npc.id, offer.Talk))
            {
                string tone = reply.Tone;
                hud.Tag(hud.Action(reply.Words, () => AnswerJustTalk(token, state, id, talkId, tone)),
                    // The pill: the tone's caption, or on a Close friend's favour the favour's words (talk rules 2).
                    reply.Pill + " · " + FreeTag);
            }
            if (!string.IsNullOrEmpty(justTalkRefused)) hud.Paragraph(justTalkRefused);
            return true;
        }

        /// <summary>
        /// A reply: the engine's own preview of what it does, taken on the state the panel was drawn from, then the command on
        /// that state. The engine applies exactly the preview (FreeTalk.Result), so the third beat shows what was committed.
        /// </summary>
        private void AnswerJustTalk(object token, EpisodeState state, string npcId, string talkId, string tone)
        {
            if (!ConversationNavigationCurrent(token, state, npcId) || !IsCurrentDiaryRevision(state)) return;
            var command = TalkScreenLines.Command(state, npcId, talkId, tone, Guid.NewGuid().ToString("N"));
            justTalkPending = FreeTalk.Result(state, npcId, talkId, tone);
            justTalkPendingCommand = command.id;
            justTalkRefused = null;
            var result = Submit(command);
            justTalkPending = null; justTalkPendingCommand = null;
            if (result != null && !result.accepted && justTalkWith == npcId)
            {
                justTalkRefused = result.reason;
                conversationMenuToken = new object();
                Render();
            }
        }

        /// <summary>
        /// Called as a commit's conversation exchange would begin: a free talk plays no spoken exchange and narrates nothing
        /// into the category page (its panel shows the three beats); the accepted one's preview becomes the third beat. True
        /// for a free talk's command, whatever came of it.
        /// </summary>
        private bool TakeJustTalkCommand(CommandResult result, EpisodeCommand command)
        {
            if (command == null || command.kind != EpisodeCommandKind.JustTalk) return false;
            lastSocialAction = null; lastSocialDelta = 0d; standingLineBefore = null;
            if (result == null || !result.accepted || justTalkWith == null || command.targetId != justTalkWith) return true;
            justTalkResult = command.id == justTalkPendingCommand && justTalkPending != null
                ? justTalkPending
                // Accepted with no preview to show: say only that it was had, never a change the engine did not report.
                : new TalkResult { NpcId = command.targetId, TalkId = null, Outcome = null };
            return true;
        }

        /// <summary>The tile on a houseguest's free-time screen, beside the paid ways: added to the list when the season plays free talk.</summary>
        private List<EpisodeHud.MoveTile> WithJustTalkTile(EpisodeState state, ContestantState who, List<EpisodeHud.MoveTile> tiles)
        {
            if (!TalkScreenLines.Shown(state, who.id)) return tiles;
            var offer = FreeTalk.Offer(state, who.id);
            string id = who.id;
            tiles.Add(new EpisodeHud.MoveTile
            {
                Caption = EpisodeHud.JustTalkCaption,
                Description = TalkScreenLines.TileLine(offer, CharacterNames.FirstName(who.name ?? "")),
                Corner = TalkScreenLines.TileCorner, CornerTint = UiTheme.Allied, Glyph = "chat", Foot = TalkScreenLines.TileFoot,
                Choose = () => { if (FreeTalk.Offer(projected, id).Available) TalkWithIntent(id, IntentJustTalk); },
            });
            return tiles;
        }

        /// <summary>After the tiles are drawn: the free tile locked when no talk is on offer (the reason is its line).</summary>
        private void LockJustTalkTile(EpisodeState state, ContestantState who)
        {
            if (!TalkScreenLines.Shown(state, who.id) || FreeTalk.Offer(state, who.id).Available) return;
            hud.LockTile(InteractionTilesName, EpisodeHud.JustTalkCaption);
        }

        /// <summary>The houseguest screen's grid of ways to spend time on them.</summary>
        private const string InteractionTilesName = "Interaction tiles";

        /// <summary>
        /// The notebook page's free-talk blocks, "What you know about them" and "This week, from them": none at talk rules 0. At
        /// talk rules 2, above them, "Between you": the friendship stage and, once shown, the chemistry, in one line.
        /// </summary>
        private static void TalkBlocks(EpisodeState state, ContestantState actor, EpisodeHud.ProfileView view)
        {
            if (state == null || actor == null || view == null) return;
            foreach (var block in TalkScreenLines.BondBlocks(state, actor.id))
                view.Blocks.Add((block.Heading, (IList<string>)block.Lines));
            foreach (var block in TalkScreenLines.NotebookBlocks(state, actor.id))
                view.Blocks.Add((block.Heading, (IList<string>)block.Lines));
        }

        /// <summary>The player's own card's free-talk block at talk rules 2, "How you come across": "The house reads you as a flirt.". None below.</summary>
        private static void YourTalkReadBlocks(EpisodeState state, EpisodeHud.ProfileView view)
        {
            if (state == null || view == null) return;
            foreach (var block in TalkScreenLines.YourReadBlocks(state))
                view.Blocks.Add((block.Heading, (IList<string>)block.Lines));
        }
    }
}
