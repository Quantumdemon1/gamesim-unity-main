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

            /// <summary>On a Close friend's stage talk (talk rules 2), the favour this reply grants or turns down; else None.</summary>
            public TalkFavour Favour;

            /// <summary>The button's pill: the favour's words on a favour reply, else the tone's caption.</summary>
            public string Pill => Favour == TalkFavour.Keep ? FavourKeepPill : Favour == TalkFavour.Refuse ? FavourRefusePill : ToneCaption;
        }

        /// <summary>The pills of a Close friend's two favour replies (talk rules 2): the reply's own words stay its caption.</summary>
        public const string FavourKeepPill = "Do the favor", FavourRefusePill = "Turn it down";

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
                replies.Add(new Reply
                {
                    Tone = reply.Tone, ToneCaption = TalkTone.Caption(reply.Tone), Words = words,
                    // The favour is the Close friend's stage talk's alone (the engine ignores it on any other row).
                    Favour = talk.Kind == TalkKind.Stage && talk.Stage == FriendStage.CloseFriend ? reply.Favour : TalkFavour.None,
                });
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

        /// <summary>
        /// What changed, in one line: a stage reached first (talk rules 2: the engine's event line, "You and Dana are friendly
        /// now."), then the fact learned, then the standing note, else nothing new.
        /// </summary>
        public static string ChangeLine(TalkResult result)
        {
            if (result == null) return NothingNew;
            if (!string.IsNullOrEmpty(result.StageEvent)) return result.StageEvent;
            if (!string.IsNullOrEmpty(result.Fact)) return LearnedPrefix + result.Fact;
            if (!string.IsNullOrEmpty(result.StandingLine)) return StandingNote;
            return NothingNew;
        }

        /// <summary>The lines the third beat shows under their reaction, in order: the standing line (their words), the outcome and the change.</summary>
        public static List<string> Aftermath(TalkResult result) => Aftermath(result, null);

        /// <summary>
        /// The third beat's lines for a housemate by first name: the standing line, the outcome, then at talk rules 2 the favour
        /// given or turned down and, on the talk that first shows it, the chemistry, then the change. Below talk rules 2 the
        /// result's slice-2 fields are at their defaults, so the lines are exactly S1's.
        /// </summary>
        public static List<string> Aftermath(TalkResult result, string firstName)
        {
            var lines = new List<string>();
            if (result == null) return lines;
            if (!string.IsNullOrEmpty(result.StandingLine)) lines.Add(result.StandingLine);
            if (!string.IsNullOrEmpty(result.Outcome)) lines.Add(result.Outcome);
            string favour = FavourLine(result.Favour, firstName);
            if (favour != null) lines.Add(favour);
            string chemistry = ChemistryFirstLine(result.ChemistryShown);
            if (chemistry != null) lines.Add(chemistry);
            lines.Add(ChangeLine(result));
            return lines;
        }

        // ------------------------------------------------------------------ talk rules 2 (free talk slice 2)

        /// <summary>The housemate page's block of where you stand with them, and the player's own card's block of how the house reads them.</summary>
        public const string BetweenYouHeading = "Between you", YourReadHeading = "How you come across";

        /// <summary>The favour given or turned down, said once after a Close friend's stage talk: null for any other reply.</summary>
        public static string FavourLine(TalkFavour favour, string firstName)
        {
            string who = string.IsNullOrEmpty(firstName) ? "them" : firstName;
            switch (favour)
            {
                case TalkFavour.Keep: return "You gave " + who + " your word.";
                case TalkFavour.Refuse: return "You didn't promise " + who + " anything.";
                default: return null;
            }
        }

        /// <summary>The chemistry said once, on the talk that first shows it ("You two are easy company."); null for no word.</summary>
        public static string ChemistryFirstLine(string chemistryWord)
        {
            switch (chemistryWord)
            {
                case TalkChemistry.KindredWord: return "You two are kindred.";
                case TalkChemistry.EasyWord: return "You two are easy company.";
                case TalkChemistry.NeitherWord: return "Between you two: no chemistry either way.";
                case TalkChemistry.GratingWord: return "You two grate on each other.";
                default: return null;
            }
        }

        /// <summary>
        /// The one plain line of where you stand with a housemate: the stage word (none for a Stranger) and, once shown, the
        /// chemistry ("Friend · Chemistry: easy company"); null when there is neither.
        /// </summary>
        public static string BondLine(TalkNotes.Bond bond)
        {
            if (bond == null) return null;
            var parts = new List<string>();
            if (bond.Stage != FriendStage.Stranger && !string.IsNullOrEmpty(bond.StageWord)) parts.Add(bond.StageWord);
            if (!string.IsNullOrEmpty(bond.ChemistryLine)) parts.Add(bond.ChemistryLine);
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        /// <summary>The housemate page's "Between you" block, above the free talk's other blocks: none below talk rules 2 or with nothing to say.</summary>
        public static List<Block> BondBlocks(EpisodeState s, string npcId)
        {
            var blocks = new List<Block>();
            string line = BondLine(TalkNotes.BondWith(s, npcId));
            if (line != null) blocks.Add(new Block { Heading = BetweenYouHeading, Lines = { line } });
            return blocks;
        }

        /// <summary>The player's own card's "How you come across" block, "The house reads you as a flirt.": none below talk rules 2.</summary>
        public static List<Block> YourReadBlocks(EpisodeState s)
        {
            var blocks = new List<Block>();
            string line = s == null ? null : TalkNotes.HouseReadsYou(s);
            if (!string.IsNullOrEmpty(line)) blocks.Add(new Block { Heading = YourReadHeading, Lines = { line } });
            return blocks;
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
