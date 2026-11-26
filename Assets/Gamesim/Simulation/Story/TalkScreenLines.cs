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

        /// <summary>
        /// The paragraph a scene left open is redrawn with ("Name: opener"), or null when the row opens with nothing (a Flirt
        /// answered in kind): the panel then draws no empty "Name: " line above the choices.
        /// </summary>
        public static string SceneOpener(EpisodeState s, string npcId, TalkRow row, string first)
        {
            string text = Opener(s, npcId, row);
            return string.IsNullOrEmpty(text) ? null : first + ": " + text;
        }

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
        public static string ChangeLine(TalkResult result) => ChangeLine(result, null);

        /// <summary>
        /// What changed, for a housemate by first name: a stage reached is said with <paramref name="firstName"/> ("You and Dana are
        /// friendly now."), whatever name the season's event row holds; with no first name, exactly <see cref="ChangeLine(TalkResult)"/>.
        /// </summary>
        public static string ChangeLine(TalkResult result, string firstName)
        {
            if (result == null) return NothingNew;
            if (!string.IsNullOrEmpty(result.StageEvent))
            {
                string said = string.IsNullOrEmpty(firstName) ? "" : TalkFriendship.EventLine(result.StageReached, firstName);
                return string.IsNullOrEmpty(said) ? result.StageEvent : said;
            }
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
            // Their words in first names (the said form); the saved line when a result carries no said form.
            string standing = string.IsNullOrEmpty(result.StandingSaid) ? result.StandingLine : result.StandingSaid;
            if (!string.IsNullOrEmpty(standing)) lines.Add(standing);
            if (!string.IsNullOrEmpty(result.Outcome)) lines.Add(result.Outcome);
            string favour = FavourLine(result.Favour, firstName);
            if (favour != null) lines.Add(favour);
            string chemistry = ChemistryFirstLine(result.ChemistryShown);
            if (chemistry != null) lines.Add(chemistry);
            lines.Add(ChangeLine(result, firstName));
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
            string line = BondLine(TalkNotes.BondWith(s, npcId));
            // Talk rules 3: the romance's line and the romance's notes under it; never read below the level.
            if (!RomanceShown(s)) return BondBlocks(line, null, null);
            return BondBlocks(line, TalkRomanceNotes.PageLine(s, npcId), TalkRomanceNotes.ProfileNotes(s, npcId));
        }

        /// <summary>
        /// The "Between you" block from its parts: the stage and chemistry line, then (talk rules 3) the romance's page line and
        /// its notes ("Took the no well"); none when every part is empty. With no romance parts it is exactly slice 2's block.
        /// </summary>
        public static List<Block> BondBlocks(string bondLine, string romanceLine, IEnumerable<string> romanceNotes)
        {
            var blocks = new List<Block>();
            var block = new Block { Heading = BetweenYouHeading };
            if (bondLine != null) block.Lines.Add(bondLine);
            if (!string.IsNullOrEmpty(romanceLine)) block.Lines.Add(romanceLine);
            foreach (string note in romanceNotes ?? Enumerable.Empty<string>())
                if (!string.IsNullOrEmpty(note) && !block.Lines.Contains(note)) block.Lines.Add(note);
            if (block.Lines.Count > 0) blocks.Add(block);
            return blocks;
        }

        /// <summary>The player's own card's "How you come across" block, "The house reads you as a flirt.": none below talk rules 2.</summary>
        public static List<Block> YourReadBlocks(EpisodeState s)
        {
            string line = s == null ? null : TalkNotes.HouseReadsYou(s);
            // Talk rules 3: "Took the no well" once, when any housemate's page holds it (the engine's note, never a name).
            bool tookTheNoWell = RomanceShown(s) && s.contestants != null && s.contestants.Any(c =>
                c != null && !c.isPlayer && TalkRomanceNotes.ProfileNotes(s, c.id)?.Contains(TalkRomanceNotes.TookTheNoWell) == true);
            return YourReadBlocks(line, tookTheNoWell);
        }

        /// <summary>The "How you come across" block from its parts: the house's read, then (talk rules 3) "Took the no well"; none when both are empty.</summary>
        public static List<Block> YourReadBlocks(string readLine, bool tookTheNoWell)
        {
            var blocks = new List<Block>();
            var block = new Block { Heading = YourReadHeading };
            if (!string.IsNullOrEmpty(readLine)) block.Lines.Add(readLine);
            if (tookTheNoWell) block.Lines.Add(TalkRomanceNotes.TookTheNoWell);
            if (block.Lines.Count > 0) blocks.Add(block);
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

        // ------------------------------------------------------------------ talk rules 3 (free talk slice 3): romance

        /// <summary>
        /// Whether the panel and the pages draw anything of slice 3: talk rules 3 or later with free talk on
        /// (<see cref="TalkRomance.LevelOn"/>). Below it not one romance query is made, so the screen is exactly slice 2's.
        /// </summary>
        public static bool RomanceShown(EpisodeState s) => s?.story != null && TalkRomance.LevelOn(s);

        /// <summary>The Flirt reply's caption (a contract: tests and screen readers find the button by it). Placed last among the replies.</summary>
        public const string FlirtCaption = "Flirt";

        /// <summary>The romance choices' captions (contracts), the words of <see cref="TalkChoices.Caption"/>.</summary>
        public const string MakeItOfficialCaption = "Make it official", AcceptCaption = "Accept", NotYetCaption = "Not yet",
            NotLikeThatCaption = "Not like that", JustFriendsCaption = "Just friends", EndItKindlyCaption = "End it kindly",
            LaughItOffCaption = "Laugh it off", PushAgainCaption = "Push again", ApologizeCaption = "Apologize";

        /// <summary>A choice's button caption; empty for None.</summary>
        public static string ChoiceCaption(TalkChoice choice)
        {
            switch (choice)
            {
                case TalkChoice.MakeItOfficial: return MakeItOfficialCaption;
                case TalkChoice.Accept: return AcceptCaption;
                case TalkChoice.NotYet: return NotYetCaption;
                case TalkChoice.NotLikeThat: return NotLikeThatCaption;
                case TalkChoice.JustFriends: return JustFriendsCaption;
                case TalkChoice.EndItKindly: return EndItKindlyCaption;
                case TalkChoice.LaughItOff: return LaughItOffCaption;
                case TalkChoice.PushAgain: return PushAgainCaption;
                case TalkChoice.Apologize: return ApologizeCaption;
                default: return "";
            }
        }

        /// <summary>One choice button: the choice, its caption, and the answer its command carries (<c>choice:&lt;id&gt;</c>).</summary>
        public sealed class ChoiceButton
        {
            public TalkChoice Choice;
            public string Caption, Answer;
        }

        /// <summary>The buttons for a list of open choices, in the engine's order: one per choice, None and repeats left out.</summary>
        public static List<ChoiceButton> ChoiceButtons(IEnumerable<TalkChoice> choices)
        {
            var buttons = new List<ChoiceButton>();
            foreach (var choice in choices ?? Enumerable.Empty<TalkChoice>())
            {
                if (choice == TalkChoice.None || buttons.Any(b => b.Choice == choice)) continue;
                buttons.Add(new ChoiceButton { Choice = choice, Caption = ChoiceCaption(choice), Answer = TalkChoices.Answer(choice) });
            }
            return buttons;
        }

        /// <summary>The choices on offer with a talk on offer (a romance talk's, by the engine): none below talk rules 3.</summary>
        public static List<ChoiceButton> OfferChoices(EpisodeState s, string npcId, TalkRow talk) =>
            !RomanceShown(s) || talk == null || talk.Kind <= TalkKind.Stage ? new List<ChoiceButton>()
                : ChoiceButtons(TalkRomance.ChoicesNow(s, npcId, talk));

        /// <summary>
        /// Whether the Flirt button joins a talk's replies: talk rules 3, no reply of the talk already in the flirt tone, and the
        /// engine's word that this talk carries the Flirt answer (<see cref="TalkRomance.FlirtOffered"/>: a layer, rhythm, standing
        /// or stage talk while Flirt is open; never a romance talk, whose own replies hold the flirt where it has one).
        /// </summary>
        public static bool FlirtShown(EpisodeState s, string npcId, TalkRow talk, IEnumerable<Reply> replies) =>
            RomanceShown(s) && talk != null
            && !(replies ?? Enumerable.Empty<Reply>()).Any(r => r.Tone == TalkRules.Flirt)
            && TalkRomance.FlirtOffered(s, npcId, talk);

        /// <summary>
        /// The command's text for an answer (a tone or <c>choice:&lt;id&gt;</c>): at talk rules 3 the engine's
        /// <see cref="TalkRomance.CommandText"/> with the housemate the house sees in the room (none when null); below it
        /// slice 1's two-part text, the witness never sent.
        /// </summary>
        public static string AnswerText(EpisodeState s, string talkId, string answer, string seenById) =>
            RomanceShown(s) ? TalkRomance.CommandText(talkId, answer, seenById) : FreeTalk.CommandText(talkId, answer);

        /// <summary>The command an answer sends, on the state the screen was drawn from; below talk rules 3 exactly <see cref="Command"/>'s.</summary>
        public static EpisodeCommand AnswerCommand(EpisodeState s, string npcId, string talkId, string answer, string seenById, string commandId)
        {
            var command = Command(s, npcId, talkId, answer, commandId);
            command.text = AnswerText(s, talkId, answer, seenById);
            return command;
        }

        /// <summary>What the command would do, as the engine says before it applies it: <see cref="TalkRomance.Result"/> at talk rules 3, else <see cref="FreeTalk.Result"/>.</summary>
        public static TalkResult Preview(EpisodeState s, string npcId, string talkId, string answer, string seenById)
        {
            if (RomanceShown(s)) return TalkRomance.Result(s, npcId, AnswerText(s, talkId, answer, seenById));
            return FreeTalk.Result(s, npcId, talkId, answer);
        }

        /// <summary>Whether a result carries anything of the romance (a choice, a continuation, a scene or a change); false for every slice 1 and 2 result.</summary>
        public static bool HasRomance(TalkResult result) =>
            result != null && (result.Choice != TalkChoice.None || result.Continuation || result.Scene != RomanceScene.None
                || result.Change != RomanceChange.None || (result.Choices != null && result.Choices.Count > 0));

        /// <summary>Their words in a scene, in order (beat 1, then beat 2), each by first name: "Dana: “...”". Empty when none.</summary>
        public static List<string> SceneLines(TalkResult result, string firstName)
        {
            var lines = new List<string>();
            if (result == null) return lines;
            string who = string.IsNullOrEmpty(firstName) ? "They" : firstName;
            if (!string.IsNullOrEmpty(result.SceneLine)) lines.Add(who + ": " + result.SceneLine);
            if (!string.IsNullOrEmpty(result.SceneSecond)) lines.Add(who + ": " + result.SceneSecond);
            return lines;
        }

        /// <summary>The romance's after-lines for a result (the engine's <see cref="TalkRomanceNotes.AfterLines"/>); none for a result without romance.</summary>
        public static List<string> RomanceLines(TalkResult result, string firstName) =>
            HasRomance(result) ? (TalkRomanceNotes.AfterLines(result, firstName) ?? new List<string>()) : new List<string>();

        /// <summary>
        /// The third beat's lines with the romance's: slice 2's lines, the romance's after-lines before the change line, and
        /// "nothing new" left out when the romance said what changed (always on a continuation, which is not a counted talk).
        /// With no romance lines and no continuation it is exactly <see cref="Aftermath(TalkResult, string)"/>.
        /// </summary>
        public static List<string> Aftermath(TalkResult result, string firstName, IList<string> romanceLines)
        {
            var lines = Aftermath(result, firstName);
            var romance = (romanceLines ?? new List<string>()).Where(l => !string.IsNullOrEmpty(l)).ToList();
            if (result == null || (romance.Count == 0 && !result.Continuation)) return lines;
            int at = lines.Count - 1;
            if (lines[at] == NothingNew) lines.RemoveAt(at);
            else if (result.Continuation) at = lines.Count;
            lines.InsertRange(Math.Min(at, lines.Count), romance);
            return lines;
        }

        /// <summary>A scene left open with choices (talk rules 3, saved in contact.talkPending): its row and its choices, so the panel can finish it after a way back or a load.</summary>
        public sealed class OpenScene
        {
            public TalkRow Row;
            public List<ChoiceButton> Choices = new List<ChoiceButton>();
        }

        /// <summary>The scene left open with a housemate this window, or null (always null below talk rules 3).</summary>
        public static OpenScene PendingScene(EpisodeState s, string npcId)
        {
            if (!RomanceShown(s) || string.IsNullOrEmpty(npcId)) return null;
            var contact = s.story.contacts?.FirstOrDefault(c => c != null && c.npcId == npcId);
            if (contact == null || string.IsNullOrEmpty(contact.talkPending)) return null;
            // The engine continues a scene only in the window it was left open in: a stale one is not drawn.
            int key = FreeTalk.WindowKey(s);
            if (key == 0 || contact.talkWindow != key) return null;
            var row = TalkBank.Find(contact.talkPending);
            if (row == null) return null;
            var choices = ChoiceButtons(TalkRomance.ChoicesNow(s, npcId, row));
            return choices.Count == 0 ? null : new OpenScene { Row = row, Choices = choices };
        }
    }
}
