using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The player's final argument (ENDGAME-PLAN F4b, schema 21): a theme of five, the three
    /// signature moments that back it, the speech they template, and the one small term they add
    /// to the jury's score. Pure and roll-free: reading it changes nothing, and the vote's two
    /// rolls per juror stay as pinned.
    ///
    /// <para><b>The themes</b> are the web's five speech flavours (WebFinalSpeechCatalog), so a
    /// juror's theme is their lead trait as the questioning reads it
    /// (<see cref="WebJuryQuestioning.GetPrimaryTrait"/>) through the web's own trait table
    /// (<see cref="WebFinalSpeeches.TraitFlavor"/>): both are public, and both already decide what
    /// the finalists' generated speeches sound like. A juror with no traits reads as Strategic, so
    /// cerebral.</para>
    ///
    /// <para><b>The moments</b> are the player's own record, evidence only (§1): the competitions
    /// they won, the weeks they held the house, the calls they made in an alliance and the ones they
    /// broke from, the whip counts they read right, the vetoes they used on somebody else, the weeks
    /// they survived the block, the promises and deals they kept, the alliances that held. Never a
    /// hidden score, a grudge row, why an alliance ended, or the story's private plans. Each has a
    /// reference that names its row and still resolves after the season ends.</para>
    ///
    /// <para><b>The effect</b> (decision 4, A): a juror whose theme is the argument's gains
    /// <see cref="PerMoment"/> for each locked moment that backs the theme, up to
    /// <see cref="Cap"/>, under the ±10 final impression and the story term's +12. A theme with
    /// nothing behind it moves nobody. Only the player argues, so only the player's score moves; the
    /// other finalist's speech is the one the web generates, as before.</para>
    /// </summary>
    public static class FinalArgument
    {
        public const string Cerebral = "cerebral", Social = "social", Aggressive = "aggressive", Sneaky = "sneaky", Emotional = "emotional";

        /// <summary>The five themes, in the order the screen offers them.</summary>
        public static readonly string[] Themes = { Cerebral, Social, Aggressive, Sneaky, Emotional };

        /// <summary>What a juror whose theme matches gains per backing moment, and the most they gain.</summary>
        public const double PerMoment = 2, Cap = 6;

        /// <summary>How many moments an argument locks.</summary>
        public const int MomentCount = 3;

        /// <summary>The theme's caption: the control that chooses it and the word that names it.</summary>
        public static string Label(string theme)
        {
            switch (theme)
            {
                case Cerebral: return "Strategic mastermind";
                case Social: return "Social connector";
                case Aggressive: return "Competition threat";
                case Sneaky: return "Under the radar";
                case Emotional: return "Loyal to the end";
                default: return null;
            }
        }

        /// <summary>One line under the theme's caption: what arguing it claims.</summary>
        public static string Claim(string theme)
        {
            switch (theme)
            {
                case Cerebral: return "Every move was planned, and the house followed your read.";
                case Social: return "You held the house together, and the house kept you.";
                case Aggressive: return "You won when it mattered and never hid from a fight.";
                case Sneaky: return "You stayed off the radar while the house tore itself apart.";
                case Emotional: return "You kept your word and stood by your people.";
                default: return null;
            }
        }

        /// <summary>The theme a juror values: their lead trait, read as the questioning reads it, through the web's table.</summary>
        public static string ThemeOf(ContestantState juror) =>
            juror == null ? null : WebFinalSpeeches.TraitFlavor(new[] { WebJuryQuestioning.GetPrimaryTrait(juror.traits) });

        // ------------------------------------------------------------ the moments

        /// <summary>One signature moment: its reference, the theme it backs, and how it reads.</summary>
        public sealed class Moment
        {
            public string reference, theme;
            public int week;
            /// <summary>How the screen lists it, to the player: "Week 4: you won Head of Household."</summary>
            public string text;
            /// <summary>How the speech says it, in the player's voice: "In week 4, I won Head of Household."</summary>
            public string said;
        }

        /// <summary>
        /// Every moment the player could lock, oldest first. The reference grammar: <c>win:{week}:{kind}</c>,
        /// <c>hoh:{week}</c>, <c>call:{alliance}:{week}</c>, <c>defect:{alliance}:{week}</c>,
        /// <c>whip:{week}</c>, <c>veto:{week}</c>, <c>block:{week}</c>, <c>promise:{id}</c>,
        /// <c>deal:{id}</c>, <c>alliance:{id}</c>, and the two season records
        /// <c>record:unnominated</c> and <c>record:off-the-block</c>.
        /// </summary>
        public static List<Moment> Moments(EpisodeState s)
        {
            var moments = new List<Moment>();
            if (s == null) return moments;
            string player = s.playerId;
            var you = s.Find(player);
            var ledger = s.ledger ?? new SeasonLedger();
            string W(int week) => week.ToString(CultureInfo.InvariantCulture);
            void Add(string reference, string theme, int week, string text, string said) =>
                moments.Add(new Moment { reference = reference, theme = theme, week = week, text = text, said = said });

            foreach (var row in ledger.competitions.Where(r => r.placement == 1))
            {
                string what = What(row.kind);
                if (what == null) continue;
                Add("win:" + W(row.week) + ":" + row.kind, Aggressive, row.week,
                    "Week " + row.week + ": you won " + what + ".", "In week " + row.week + ", I won " + what + ".");
            }
            foreach (var p in ledger.power.OrderBy(p => p.week))
            {
                bool final = p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null;
                if (p.hohId == player && p.evicteeId != null)
                    Add("hoh:" + W(p.week), Cerebral, p.week,
                        final ? "Week " + p.week + ": as the last Head of Household, you sent " + Name(s, p.evicteeId) + " to the jury."
                              : "Week " + p.week + ": you held the house, and " + Name(s, p.evicteeId) + " went home.",
                        final ? "In week " + p.week + ", I won the final Head of Household and chose who sat beside me."
                              : "In week " + p.week + ", I held the house, and " + Name(s, p.evicteeId) + " went home.");
                if (final) continue;
                if (p.vetoHolderId == player && p.vetoUsed && p.savedId != null && p.savedId != player)
                    Add("veto:" + W(p.week), Social, p.week, "Week " + p.week + ": you used the veto on " + Name(s, p.savedId) + ".",
                        "In week " + p.week + ", I used the veto to save " + Name(s, p.savedId) + ".");
                bool upThisWeek = p.nominees.Contains(player) || p.savedId == player;
                if (upThisWeek && p.evicteeId != null && p.evicteeId != player)
                    Add("block:" + W(p.week), Social, p.week,
                        p.savedId == player ? "Week " + p.week + ": the veto took you off the block." : "Week " + p.week + ": you survived the block.",
                        p.savedId == player ? "In week " + p.week + ", I came off the block." : "In week " + p.week + ", I sat on the block and stayed.");
            }
            foreach (var call in ledger.calls)
            {
                var week = ledger.power.FirstOrDefault(p => p.week == call.week);
                // Named, so two alliances' calls in one week are two moments a player can tell apart.
                string pact = s.alliances.FirstOrDefault(a => a.id == call.allianceId)?.name;
                string where = string.IsNullOrEmpty(pact) ? "your alliance" : pact;
                if (call.callerId == player && week != null && week.evicteeId == call.targetId)
                    Add("call:" + call.allianceId + ":" + W(call.week), Cerebral, call.week,
                        "Week " + call.week + ": you called the vote in " + where + ", and " + Name(s, call.targetId) + " went.",
                        "In week " + call.week + ", I called the vote, and " + Name(s, call.targetId) + " went.");
            }
            foreach (var ballot in ledger.ballots.Where(b => b.voterId == player && b.correct && b.readBefore != null))
                Add("whip:" + W(ballot.week), Cerebral, ballot.week,
                    "Week " + ballot.week + ": your whip count said " + Name(s, ballot.readBefore) + " would go, and they did.",
                    "In week " + ballot.week + ", I counted the votes before anyone else, and I was right.");
            foreach (var promise in s.promises.Where(x => x.fromId == player && x.status == PromiseStatus.Fulfilled))
                Add("promise:" + promise.id, Emotional, promise.week,
                    "Week " + promise.week + ": you promised " + Name(s, promise.toId) + " " + HouseguestNotes.PromiseWord(promise.kind) + ", and kept it.",
                    "In week " + promise.week + ", I gave " + Name(s, promise.toId) + " my word, and I kept it.");
            foreach (var deal in s.deals.Where(x => x.status == DealStatus.Fulfilled && (x.proposerId == player || x.recipientId == player)))
            {
                string other = deal.proposerId == player ? deal.recipientId : deal.proposerId;
                string title = DealKind.Title(deal.type).ToLowerInvariant();
                Add("deal:" + deal.id, Emotional, deal.week, "Week " + deal.week + ": you and " + Name(s, other) + " kept your " + title + ".",
                    "In week " + deal.week + ", " + Name(s, other) + " and I made a " + title + ", and I kept it.");
            }
            foreach (var row in ledger.alliances)
            {
                var alliance = s.alliances.FirstOrDefault(a => a.id == row.id);
                if (alliance == null || !alliance.members.Contains(player)) continue;
                string others = string.Join(", ", alliance.members.Where(id => id != player).Select(id => Name(s, id)));
                // Named, so two alliances of the same people are two moments a player can tell apart.
                string called = string.IsNullOrEmpty(alliance.name) ? "your alliance with " + others
                    : alliance.name + ", your alliance with " + others + ",";
                if (row.endedWeek == 0)
                    Add("alliance:" + row.id, Social, row.startedWeek, "Week " + row.startedWeek + ": " + called + " held to the end.",
                        "My alliance with " + others + " held from week " + row.startedWeek + " to the end.");
                else if (row.endedWeek - row.startedWeek >= 3)
                    Add("alliance:" + row.id, Social, row.startedWeek, "Week " + row.startedWeek + ": " + called + " lasted " + (row.endedWeek - row.startedWeek) + " weeks.",
                        "My alliance with " + others + " lasted " + (row.endedWeek - row.startedWeek) + " weeks.");
            }
            if (you != null && you.timesNominated == 0 && s.week > 1)
                Add("record:unnominated", Sneaky, 0, "You were never nominated.", "I was never once nominated.");
            if (you != null)
            {
                int clear = OffTheBlock(s);
                if (clear >= 3)
                    Add("record:off-the-block", Sneaky, 0, "You stayed off the block for " + clear + " weeks.", "I stayed off the block for " + clear + " weeks.");
            }
            // One moment per reference: a row the ledger holds twice is still one moment. And one
            // caption per moment: the screen finds a tile by its words, so any two that still read
            // the same are told apart.
            var list = moments.GroupBy(m => m.reference).Select(g => g.First())
                .OrderBy(m => m.week).ThenBy(m => m.reference, StringComparer.Ordinal).ToList();
            foreach (var same in list.GroupBy(m => m.text).Where(g => g.Count() > 1))
            {
                int n = 1;
                foreach (var moment in same.Skip(1)) moment.text = moment.text.TrimEnd('.') + " (" + (++n) + ").";
            }
            return list;
        }

        /// <summary>
        /// Whose face a moment's card carries (MOCKUP-PASS M15): the houseguest the moment is about,
        /// read from the row its reference names - who went home the week the player held the house,
        /// who the veto saved, who the call or the whip count named, who the word was given to, the
        /// other side of a deal, an alliance's first ally - and the player's own for a win, a week on
        /// the block and the season's two records. Null when the row is gone. Only the player's own
        /// rows, so a card never shows a face the player had no part in the moment with.
        /// </summary>
        public static string SubjectOf(EpisodeState s, string reference)
        {
            if (s == null || string.IsNullOrEmpty(reference)) return null;
            var ledger = s.ledger ?? new SeasonLedger();
            string player = s.playerId;
            var parts = reference.Split(':');
            bool Week(string text, out int week) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out week);
            switch (parts[0])
            {
                case "win": case "block": case "record": return Resolves(s, reference) ? player : null;
                case "hoh":
                    return parts.Length == 2 && Week(parts[1], out int w1) ? ledger.power.FirstOrDefault(p => p.week == w1 && p.hohId == player)?.evicteeId : null;
                case "veto":
                    return parts.Length == 2 && Week(parts[1], out int w2) ? ledger.power.FirstOrDefault(p => p.week == w2 && p.vetoHolderId == player)?.savedId : null;
                case "whip":
                    return parts.Length == 2 && Week(parts[1], out int w3)
                        ? ledger.ballots.FirstOrDefault(b => b.week == w3 && b.voterId == player && b.readBefore != null)?.readBefore : null;
                case "call":
                    int at = reference.LastIndexOf(':');
                    if (at <= parts[0].Length || !Week(reference.Substring(at + 1), out int w4)) return null;
                    string allianceId = reference.Substring(parts[0].Length + 1, at - parts[0].Length - 1);
                    return ledger.calls.FirstOrDefault(c => c.week == w4 && c.allianceId == allianceId && c.callerId == player)?.targetId;
                case "promise": return s.promises.FirstOrDefault(x => "promise:" + x.id == reference && x.fromId == player)?.toId;
                case "deal":
                    var deal = s.deals.FirstOrDefault(x => "deal:" + x.id == reference && (x.proposerId == player || x.recipientId == player));
                    return deal == null ? null : deal.proposerId == player ? deal.recipientId : deal.proposerId;
                case "alliance":
                    return s.alliances.FirstOrDefault(a => "alliance:" + a.id == reference && a.members.Contains(player))?.members.FirstOrDefault(id => id != player);
                default: return null;
            }
        }

        /// <summary>
        /// A moment's title in a few words, from its reference alone (MOCKUP-PASS M15): "Won Head of
        /// Household", "Held the house", "Kept your word". The card's bold line and the tray's slot;
        /// the moment's own text stays the control's caption, so the two never read the same. Null
        /// for a reference of no kind the screen offers.
        /// </summary>
        public static string Title(string reference)
        {
            int colon = reference == null ? -1 : reference.IndexOf(':');
            switch (colon < 0 ? reference : reference.Substring(0, colon))
            {
                case "win":
                    switch (reference.Substring(reference.LastIndexOf(':') + 1))
                    {
                        case "HoH": return "Won Head of Household";
                        case "Veto": return "Won the Power of Veto";
                        case "FinalHoHPart1": return "Won Final HoH Part 1";
                        case "FinalHoHPart2": return "Won Final HoH Part 2";
                        case "FinalHoHPart3": return "Won Final HoH Part 3";
                        default: return "Won a competition";
                    }
                case "hoh": return "Held the house";
                case "veto": return "Used the veto";
                case "block": return "Survived the block";
                case "call": return "Called the vote";
                case "whip": return "Read the vote right";
                case "promise": return "Kept your word";
                case "deal": return "Kept a deal";
                case "alliance": return "Built an alliance";
                case "record": return reference == "record:unnominated" ? "Never nominated" : "Stayed off the block";
                default: return null;
            }
        }

        /// <summary>The weeks on the record the player was nobody's nominee: not up, not saved, not the replacement.</summary>
        private static int OffTheBlock(EpisodeState s) =>
            (s.ledger?.power ?? new List<PowerRow>()).Count(p => !(p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null)
                && p.hohId != s.playerId && !FinalistRead.PutUp(p, s.playerId));

        private static string What(string kind)
        {
            switch (kind)
            {
                case "HoH": return "Head of Household";
                case "Veto": return "the Power of Veto";
                case "FinalHoHPart1": return "Part 1 of the final Head of Household";
                case "FinalHoHPart2": return "Part 2 of the final Head of Household";
                case "FinalHoHPart3": return "Part 3 of the final Head of Household";
                default: return null;
            }
        }

        /// <summary>The theme a moment backs, from its reference alone, so a locked argument reads the same after the season.</summary>
        public static string ThemeOfReference(string reference)
        {
            int colon = reference == null ? -1 : reference.IndexOf(':');
            switch (colon < 0 ? reference : reference.Substring(0, colon))
            {
                case "win": return Aggressive;
                case "hoh": case "call": case "whip": return Cerebral;
                case "veto": case "block": case "alliance": return Social;
                case "record": return Sneaky;
                case "promise": case "deal": return Emotional;
                default: return null;
            }
        }

        /// <summary>
        /// Whether a reference still names a row of the player's: the validator's check. It asks
        /// that the row exist, not that it still qualify, so nothing a later commit writes can
        /// orphan a locked argument.
        /// </summary>
        public static bool Resolves(EpisodeState s, string reference)
        {
            if (s == null || string.IsNullOrEmpty(reference) || reference.Length > 200) return false;
            var ledger = s.ledger ?? new SeasonLedger();
            string player = s.playerId;
            var parts = reference.Split(':');
            bool Week(string text, out int week) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out week);
            switch (parts[0])
            {
                case "win": return parts.Length == 3 && Week(parts[1], out int w1) && ledger.competitions.Any(r => r.week == w1 && r.kind == parts[2] && r.placement == 1);
                case "hoh": return parts.Length == 2 && Week(parts[1], out int w2) && ledger.power.Any(p => p.week == w2 && p.hohId == player);
                case "veto": return parts.Length == 2 && Week(parts[1], out int w3) && ledger.power.Any(p => p.week == w3 && p.vetoHolderId == player);
                case "block": return parts.Length == 2 && Week(parts[1], out int w4) && ledger.power.Any(p => p.week == w4);
                case "whip": return parts.Length == 2 && Week(parts[1], out int w5) && ledger.ballots.Any(b => b.week == w5 && b.voterId == player);
                case "call":
                    int at = reference.LastIndexOf(':');
                    string allianceId = reference.Substring(parts[0].Length + 1, Math.Max(0, at - parts[0].Length - 1));
                    return at > parts[0].Length && Week(reference.Substring(at + 1), out int w6) && ledger.calls.Any(c => c.week == w6 && c.allianceId == allianceId);
                case "promise": return s.promises.Any(x => "promise:" + x.id == reference && x.fromId == player);
                case "deal": return s.deals.Any(x => "deal:" + x.id == reference && (x.proposerId == player || x.recipientId == player));
                case "alliance": return s.alliances.Any(a => "alliance:" + a.id == reference && a.members.Contains(player));
                case "record": return reference == "record:unnominated" || reference == "record:off-the-block";
                default: return false;
            }
        }

        // ------------------------------------------------------------ the lock's keys

        /// <summary>The lock carries its three references in the command's text, one to a line.</summary>
        public static string JoinReferences(IEnumerable<string> references) => string.Join("\n", references ?? Enumerable.Empty<string>());

        public static List<string> ParseReferences(string text) =>
            string.IsNullOrEmpty(text) ? new List<string>() : text.Split('\n').Select(line => line.Trim('\r')).ToList();

        /// <summary>How many moments the lock asks for: three, or every one a quiet season has.</summary>
        public static int Required(EpisodeState s) => Math.Min(MomentCount, Moments(s).Count);

        // ------------------------------------------------------------ the speech

        /// <summary>
        /// The speech the argument templates, which fills the editor when the speeches open. It goes
        /// in through the speech's own command like any other, so the player may change it, clear it
        /// or skip it. Held to the editor's 2,000 characters and to the command's plain text.
        /// </summary>
        public static string Speech(EpisodeState s)
        {
            var argument = s?.finalArgument;
            if (argument == null) return "";
            var moments = Moments(s);
            var lines = new List<string> { Opening(argument.theme) };
            foreach (var reference in argument.momentRefs ?? new List<string>())
            {
                var moment = moments.FirstOrDefault(m => m.reference == reference);
                if (moment != null) lines.Add(moment.said);
            }
            lines.Add(Closing(argument.theme));
            string speech = string.Join("\n\n", lines.Where(line => !string.IsNullOrEmpty(line)));
            return speech.Length <= 2000 ? speech : speech.Substring(0, 2000);
        }

        /// <summary>
        /// The speech's first line for a theme: the templated speech opens with it, and after the
        /// lock the final case's résumé reads it back in its quote slot (MOCKUP-PASS decision 44) -
        /// the player's own words to come, never anybody else's.
        /// </summary>
        public static string Opening(string theme)
        {
            switch (theme)
            {
                case Cerebral: return "I came into this house with a plan, and I played it with my head.";
                case Social: return "This game is played between people, and I played it with every person in that house.";
                case Aggressive: return "When this game asked for a win, I went out and got it.";
                case Sneaky: return "I played a quiet game, and a quiet game is still a game.";
                case Emotional: return "I played this game with my heart, and I kept my word.";
                default: return "";
            }
        }

        private static string Closing(string theme)
        {
            switch (theme)
            {
                case Cerebral: return "Every move I made, I made on purpose. I am asking for your vote.";
                case Social: return "The relationships I built got me here. I am asking you to reward them.";
                case Aggressive: return "I earned this seat. I am asking you to let me finish the job.";
                case Sneaky: return "You never saw me coming, and that was the point. I am asking for your vote.";
                case Emotional: return "I never forgot who stood with me. I am asking you to stand with me one more time.";
                default: return "";
            }
        }

        // ------------------------------------------------------------ the jury effect

        /// <summary>
        /// The argument's term in <see cref="WebJuryVoting.Score"/>: for the player only, under the
        /// finale rules, for a juror whose theme is the argument's, <see cref="PerMoment"/> for each
        /// locked moment that backs the theme, up to <see cref="Cap"/>. Zero otherwise, and zero on
        /// every old save, which has no argument.
        /// </summary>
        public static double Term(EpisodeState s, string jurorId, string finalistId)
        {
            var argument = s?.finalArgument;
            if (argument == null || finalistId != s.playerId || !EpisodeEngine.FinaleOn(s)) return 0;
            if (ThemeOf(s.Find(jurorId)) != argument.theme) return 0;
            int backing = (argument.momentRefs ?? new List<string>()).Count(reference => ThemeOfReference(reference) == argument.theme);
            return Math.Min(Cap, PerMoment * backing);
        }

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? "somebody";
    }
}
