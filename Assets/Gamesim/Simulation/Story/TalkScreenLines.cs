using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The words the free "Just talk" screen shows (free talk slice 1, talk rules 1), built from the engine's pure reads
    /// (<see cref="FreeTalk"/>, <see cref="TalkText"/>, <see cref="TalkNotes"/>) so the panel, the free-time tile and the
    /// notebook's two blocks can be checked without Unity. Nothing here draws from the season's stream or writes the state.
    /// The director (Runtime/Episode/EpisodeDirector.JustTalk.cs) draws exactly these lines.
    /// </summary>
    public static class TalkScreenLines
    {
        /// <summary>The cost the conversation's row says beside its caption: free, and no action spent.</summary>
        public const string CostTag = "free · no action";

        /// <summary>The tile's corner word and its foot: what the talk costs, which is nothing.</summary>
        public const string TileCorner = "Free", TileFoot = "Cost: nothing, no action";

        /// <summary>The panel's heads: the talk, the player's reply, and what came of it.</summary>
        public const string PanelHeading = "JUST TALK", ReplyHeading = "YOUR REPLY", ChangedHeading = "WHAT CHANGED";

        /// <summary>What changed, in one plain line: the fact learned, the standing note, or nothing new.</summary>
        public const string LearnedPrefix = "You learned: ";
        public const string StandingNote = "They told you where they stand this week. It is on their page in your notebook.";
        public const string NothingNew = "Nothing new about them this time.";

        /// <summary>The notebook's older standing snapshot: said, not only greyed.</summary>
        public const string EarlierWeekNote = "From an earlier week.";

        /// <summary>Whether the "Just talk" tile and row are drawn for a housemate at all: never at talk rules 0 (or with the lore off).</summary>
        public static bool Shown(EpisodeState s, string npcId) =>
            s != null && !string.IsNullOrEmpty(npcId) && FreeTalk.On(s) && FreeTalk.Offer(s, npcId).Refusal != TalkRefusal.Off;

        /// <summary>The tile's line under its caption: an invitation when a talk is on offer, else the engine's plain reason.</summary>
        public static string TileLine(TalkOffer offer, string firstName)
        {
            if (offer == null) return FreeTalk.NothingReason;
            if (offer.Available)
                return "Chat with " + (string.IsNullOrEmpty(firstName) ? "them" : firstName) + ". No action spent; rapport builds slowly.";
            return string.IsNullOrEmpty(offer.Reason) ? FreeTalk.NothingReason : offer.Reason;
        }

        /// <summary>Their opening line, tokens filled, as written: the stage direction in parentheses and the spoken words in quotes.</summary>
        public static string Opener(EpisodeState s, string npcId, TalkRow talk) =>
            talk == null ? "" : TalkText.Fill(s, npcId, talk.Open ?? "");

        /// <summary>One reply button: the tone it is said in, the tone's caption (the button's pill), and the player's words (its caption).</summary>
        public sealed class Reply
        {
            public string Tone, ToneCaption, Words;
        }

        /// <summary>
        /// The player's replies, one per tone, in the talk's order: the caption is the spoken words with tokens filled (what a
        /// test and a screen reader find the button by); two replies with the same words keep only the first, so a caption is
        /// never two buttons.
        /// </summary>
        public static List<Reply> Replies(EpisodeState s, string npcId, TalkRow talk)
        {
            var replies = new List<Reply>();
            if (talk?.Replies == null) return replies;
            foreach (var reply in talk.Replies)
            {
                if (reply == null || string.IsNullOrEmpty(reply.Tone)) continue;
                string words = TalkText.Spoken(TalkText.Fill(s, npcId, reply.Line ?? ""));
                if (string.IsNullOrEmpty(words) || replies.Any(r => r.Words == words)) continue;
                replies.Add(new Reply { Tone = reply.Tone, ToneCaption = TalkTone.Caption(reply.Tone), Words = words });
            }
            return replies;
        }

        /// <summary>
        /// The command a reply sends: the free talk on the state the screen was drawn from, its talk and tone in the engine's text
        /// form (<see cref="FreeTalk.CommandText"/>). Its id is the caller's (a fresh one per press).
        /// </summary>
        public static EpisodeCommand Command(EpisodeState s, string npcId, string talkId, string tone, string commandId) =>
            new EpisodeCommand
            {
                id = commandId, actorId = s.playerId, expectedPhase = s.phase, expectedRevision = s.revision,
                kind = EpisodeCommandKind.JustTalk, targetId = npcId, text = FreeTalk.CommandText(talkId, tone),
            };

        /// <summary>What changed, in one line: the fact learned first, then the standing note, else nothing new.</summary>
        public static string ChangeLine(TalkResult result)
        {
            if (result == null) return NothingNew;
            if (!string.IsNullOrEmpty(result.Fact)) return LearnedPrefix + result.Fact;
            if (!string.IsNullOrEmpty(result.StandingLine)) return StandingNote;
            return NothingNew;
        }

        /// <summary>The lines the third beat shows under their reaction, in order: the standing line (their words), the outcome and the change.</summary>
        public static List<string> Aftermath(TalkResult result)
        {
            var lines = new List<string>();
            if (result == null) return lines;
            if (!string.IsNullOrEmpty(result.StandingLine)) lines.Add(result.StandingLine);
            if (!string.IsNullOrEmpty(result.Outcome)) lines.Add(result.Outcome);
            lines.Add(ChangeLine(result));
            return lines;
        }

        /// <summary>One block of a housemate's notebook page: its heading and its lines.</summary>
        public sealed class Block
        {
            public string Heading;
            public List<string> Lines = new List<string>();
        }

        /// <summary>
        /// The notebook page's two blocks from the engine's reads: "What you know about them", each layer's plain heading as a line
        /// with its facts under it; and "This week, from them", the dated snapshot (an older one says so). A block with nothing in
        /// it is left out, so a page with neither draws as it always did.
        /// </summary>
        public static List<Block> NotebookBlocks(IList<TalkNotes.Layer> layers, TalkNotes.Standing standing)
        {
            var blocks = new List<Block>();
            var known = new Block { Heading = TalkNotes.WhatYouKnowHeading };
            foreach (var layer in (layers ?? new List<TalkNotes.Layer>()).Where(l => l != null).OrderBy(l => l.Number))
            {
                var facts = (layer.Facts ?? new List<string>()).Where(f => !string.IsNullOrEmpty(f)).ToList();
                if (facts.Count == 0) continue;
                known.Lines.Add(string.IsNullOrEmpty(layer.Heading) ? TalkNotes.HeadingOf(layer.Number) : layer.Heading);
                foreach (var fact in facts) known.Lines.Add("· " + fact);
            }
            if (known.Lines.Count > 0) blocks.Add(known);
            if (standing != null && !string.IsNullOrEmpty(standing.Line))
            {
                var from = new Block { Heading = TalkNotes.FromThemHeading };
                from.Lines.Add(standing.WeekLabel + ": " + standing.Line);
                if (!standing.Current) from.Lines.Add(EarlierWeekNote);
                blocks.Add(from);
            }
            return blocks;
        }

        /// <summary>The page's blocks for a housemate: none at talk rules 0.</summary>
        public static List<Block> NotebookBlocks(EpisodeState s, string npcId) =>
            s == null || string.IsNullOrEmpty(npcId) || !FreeTalk.On(s) ? new List<Block>()
                : NotebookBlocks(TalkNotes.WhatYouKnow(s, npcId), TalkNotes.FromThem(s, npcId));
    }
}
