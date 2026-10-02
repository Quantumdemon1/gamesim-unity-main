using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The jury house (ENDGAME-PLAN F4): what a finalist player can know of each juror's mind,
    /// in a word, and why.
    ///
    /// <para>Evidence, never the model's leaning, as <see cref="FinalistRead"/>. A juror's band
    /// starts from the player's last read of them (the ledger's <c>read</c> rows: their view of
    /// the player, banded as the read's own line bands it, 25 and -25), and the public record
    /// moves it: what the player did to them where the house could see it - putting them on the
    /// block as Head of Household, naming them the replacement, using the veto to put them up,
    /// voting them out, evicting them - and the promises the player broke them, the pleas the
    /// player refused them, the deals between them that broke.</para>
    ///
    /// <para>What is never read: the <c>juror</c> standing row (every juror's real score for
    /// the player as they left: the vote's own term, kept for Game Sense), grudge rows (their
    /// cause is last-writer-wins and their removal turns on a score the player cannot see), the
    /// jury sentiment ledger (every juror seeded from the player's own view), and why an
    /// alliance ended. The freshest evidence speaks: a warm read taken after a nomination is the
    /// juror's view after it.</para>
    ///
    /// <para>Pure and read-only, in the simulation so the Unity-free subset tests it.</para>
    ///
    /// <para>The mockup pass (MOCKUP-PASS-PLAN M12) adds three reads, each from a recorded source:
    /// what a juror saw of the player's game before they left, which is the exact complement of
    /// what they missed; the goodbye message the player recorded for them, by the choice taken and
    /// never by what it did to them; and a callout line in the juror's own recorded words where
    /// the record holds any (their question tonight, their plea from the block), dated, falling
    /// back to the band's reason. None of it says how a juror feels about the player.</para>
    /// </summary>
    public static class JuryHouseRead
    {
        public const string Supportive = "Supportive", Wavering = "Wavering", Open = "Open",
            Skeptical = "Skeptical", Bitter = "Bitter", Unknown = "Unknown";

        /// <summary>The bands in the order the legend reads them.</summary>
        public static readonly string[] Bands = { Supportive, Wavering, Open, Skeptical, Bitter, Unknown };

        /// <summary>The storyline the player records a goodbye message in, and the choices that record one.</summary>
        public const string GoodbyeTemplate = "goodbye-message";

        public sealed class Juror
        {
            public string id, band, reason, trait;
            /// <summary>The week of the read the band starts from, or null when there is none.</summary>
            public int? readWeek;
            /// <summary>The week they left, from the ledger's power rows, or null when it is not on the record.</summary>
            public int? leftWeek;
            /// <summary>What the player and they share: alliances, deals, promises, the player's public votes against them, the game they saw, the goodbye they watched.</summary>
            public List<string> knows = new List<string>();
            /// <summary>What the player did after they left, which they did not see.</summary>
            public List<string> missing = new List<string>();
            /// <summary>Dated lines from the rows between them, oldest first ("Week 6 · left on your nomination").</summary>
            public List<string> highlights = new List<string>();
            /// <summary>The same lines with their weeks apart, oldest first, so the house can merge every juror's.</summary>
            public List<(int week, string text)> dated = new List<(int, string)>();
            /// <summary>The juror's callout: their own recorded words where there are any, else the band's reason.</summary>
            public Callout line;
        }

        /// <summary>
        /// One line for a juror's callout (decision 41, A): their question to the player tonight
        /// with the engine's note on the answer once there is one, else their plea from the block
        /// as the log holds it, dated, else the band's reason. The first two are the juror's own
        /// words and are quoted; the reason is the player's evidence and never is.
        /// </summary>
        public sealed class Callout
        {
            /// <summary>When the words were said: "Tonight", "Week 5, from the block", or null for the reason.</summary>
            public string when;
            /// <summary>The words: a recorded question or plea, whole; or the band's reason.</summary>
            public string words;
            /// <summary>Whether the words are the juror's own, and so shown in quotation marks.</summary>
            public bool quoted;
            /// <summary>The engine's recorded note on the player's answer to tonight's question, once answered.</summary>
            public string note;
            /// <summary>The note without the juror's name or its full stop ("took your answer well"), for a line that already names them.</summary>
            public string noteTail;

            /// <summary>The whole line, with the words as given: "Tonight: “…” Casey Lee took your answer well."</summary>
            public string Text => Compose(words, note);

            /// <summary>
            /// The line as a callout draws it, under the juror's own name: <paramref name="shown"/> in
            /// place of the words, since a narrow callout passes an excerpt of them, and the note's
            /// tail after a dot rather than the note naming them again ("Tonight: “…” · took your
            /// answer well").
            /// </summary>
            public string Brief(string shown) => Compose(shown, string.IsNullOrEmpty(noteTail) ? null : "· " + noteTail);

            private string Compose(string shown, string after)
            {
                string said = quoted ? "“" + shown + "”" : shown;
                string line = string.IsNullOrEmpty(when) ? said : when + ": " + said;
                return string.IsNullOrEmpty(after) ? line : line + " " + after;
            }
        }

        public sealed class House
        {
            public List<Juror> jurors = new List<Juror>();
            /// <summary>What most of the jury leads with, worded, from the traits the questioning reads.</summary>
            public List<string> matters = new List<string>();
            /// <summary>
            /// Every juror's dated lines merged, newest first, each naming its juror: tonight's
            /// questions on top at the Final 2, the latest first ("Tonight · Casey asked about week
            /// 4 · took your answer well"), then "Week 6 · Casey · left on your nomination". The
            /// player's record with each of them, never what the jurors say to one another, which
            /// nothing records.
            /// </summary>
            public List<string> highlights = new List<string>();
        }

        public static House Read(EpisodeState s)
        {
            var house = new House();
            foreach (var juror in FinalistRead.Jurors(s)) house.jurors.Add(ReadJuror(s, juror.id));
            var leads = house.jurors.GroupBy(j => j.trait).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToList();
            foreach (var group in leads.Take(2))
                if (group.Count() > 1 || leads.Count == 1)
                    house.matters.Add(group.Count() + " of " + house.jurors.Count + " lead with " + group.Key + ".");
            if (house.matters.Count == 0 && house.jurors.Count > 0) house.matters.Add("No one trait leads this jury.");

            // Tonight first, newest first like the rest: the question being asked now on top and the
            // ones before it under it. The questions go round the jury in cast order, so the house's
            // own order would bury the current one under "and n more" in a large jury. Then the
            // season, newest first, jurors in their cast order within a week and each juror's own
            // lines in theirs.
            var tonight = house.jurors.Select(juror => (juror, asked: Question(s, juror.id))).Where(pair => pair.asked != null)
                .OrderByDescending(pair => s.juryExchanges.IndexOf(pair.asked)).ToList();
            foreach (var (juror, asked) in tonight)
            {
                string tail = NoteTail(s, juror.id, Note(s, asked));
                house.highlights.Add("Tonight · " + ShortName(s, juror.id) + " " + Asked(s, asked) + (tail == null ? "" : " · " + tail));
            }
            var season = house.jurors.SelectMany((juror, seat) => juror.dated.Select((line, order) => (line.week, seat, order, text: "Week " + line.week + " · " + ShortName(s, juror.id) + " · " + line.text)));
            house.highlights.AddRange(season.OrderByDescending(l => l.week).ThenBy(l => l.seat).ThenBy(l => l.order).Select(l => l.text).Distinct());
            return house;
        }

        /// <summary>
        /// A juror as the jury house names them where space is short: their first name, or their
        /// full name when another juror shares it.
        /// </summary>
        public static string ShortName(EpisodeState s, string jurorId)
        {
            string full = s.Find(jurorId)?.name ?? "";
            string first = FinalistRead.FirstName(full);
            bool shared = FinalistRead.Jurors(s).Any(other => other.id != jurorId && FinalistRead.FirstName(other.name) == first);
            return shared || first.Length == 0 ? full : first;
        }

        /// <summary>The juror's question to the player finalist, once the Final 2's questioning has drawn it; null before, and for a player juror.</summary>
        public static JuryExchangeState Question(EpisodeState s, string jurorId) =>
            s?.juryExchanges?.LastOrDefault(x => x != null && x.questionerId == jurorId && x.finalistId == s.playerId && !string.IsNullOrEmpty(x.question));

        /// <summary>
        /// The engine's own note on the player's answer, rebuilt from the saved exchange: a history
        /// question's softer note, or the catalogue's. Null until the answer is committed. The
        /// questioning screen's reaction line is this note too.
        /// </summary>
        public static string Note(EpisodeState s, JuryExchangeState exchange)
        {
            if (exchange == null || !exchange.completed || exchange.finalistId != s.playerId) return null;
            if (string.IsNullOrEmpty(exchange.questionerId) || exchange.questionerId == exchange.finalistId) return null;
            string name = s.Find(exchange.questionerId)?.name ?? "Unknown housemate";
            if (exchange.category != null) return FinaleQuestions.Note(name, FinaleQuestions.Landed(s, exchange));
            bool Choice(string key) => key == "A" || key == "B";
            if (!Choice(exchange.answerChoice) || !Choice(exchange.correctChoice)) return null;
            return WebJuryQuestioning.EvaluateChoice(new WebJuryQuestion { correctIs = exchange.correctChoice }, exchange.answerChoice,
                exchange.questionerId, name, exchange.finalistId).note;
        }

        /// <summary>
        /// The note without the juror's name or its full stop ("took your answer well"), for a line
        /// that names them already. Null when there is no note.
        /// </summary>
        private static string NoteTail(EpisodeState s, string jurorId, string note)
        {
            if (note == null) return null;
            string name = s.Find(jurorId)?.name ?? "";
            return (name.Length > 0 && note.StartsWith(name + " ", System.StringComparison.Ordinal) ? note.Substring(name.Length + 1) : note).TrimEnd('.');
        }

        /// <summary>
        /// What tonight's question was about, from the receipt it was built from: "asked about week
        /// 4", "asked about your alliance", or "asked you a question" for a comparison and for the
        /// catalogue's questions, which have no receipt.
        /// </summary>
        public static string Asked(EpisodeState s, JuryExchangeState exchange)
        {
            int? week = ReceiptWeek(s, exchange);
            if (week != null && week.Value > 0) return "asked about week " + week.Value;
            if (exchange?.receiptKind == FinaleQuestions.AllianceReceipt) return "asked about your alliance";
            return "asked you a question";
        }

        /// <summary>The week a history question's receipt names, found by its kind and id; null when there is no receipt or its row is gone.</summary>
        public static int? ReceiptWeek(EpisodeState s, JuryExchangeState exchange)
        {
            if (exchange?.receiptKind == null || exchange.receiptId == null) return null;
            string id = exchange.receiptId;
            var ledger = s.ledger ?? new SeasonLedger();
            int Parse(string text) => int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : 0;
            switch (exchange.receiptKind)
            {
                case FinaleQuestions.PowerReceipt:
                case FinaleQuestions.BallotReceipt: return Parse(id);
                case FinaleQuestions.PromiseReceipt: return s.promises.FirstOrDefault(p => p.id == id)?.week;
                case FinaleQuestions.DealReceipt: return s.deals.FirstOrDefault(d => d.id == id)?.week;
                case FinaleQuestions.ReplyReceipt: return ledger.replies.FirstOrDefault(r => r.cardId == id)?.week;
                case FinaleQuestions.CallReceipt: int at = id.LastIndexOf(':'); return at < 0 ? (int?)null : Parse(id.Substring(at + 1));
                default: return null;
            }
        }

        /// <summary>
        /// The juror's last plea from the block, as the event log holds it ("{name}: {words}"), with
        /// its week; else the week's own speech while the week still holds it. Only a speech the
        /// house heard: the log's public lines, never one addressed to somebody else.
        /// </summary>
        public static (int week, string words)? Plea(EpisodeState s, string jurorId)
        {
            var juror = s.Find(jurorId);
            if (juror == null || string.IsNullOrEmpty(juror.name)) return null;
            string prefix = juror.name + ": ";
            var logged = s.events?.Where(e => e != null && e.kind == "eviction-speech" && e.text != null && e.text.StartsWith(prefix, System.StringComparison.Ordinal)
                    && (e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(s.playerId)))
                .OrderBy(e => e.sequence).LastOrDefault();
            if (logged != null && !string.IsNullOrWhiteSpace(logged.text.Substring(prefix.Length)))
                return (logged.week, logged.text.Substring(prefix.Length).Trim());
            var spoken = s.evictionSpeeches?.LastOrDefault(x => x != null && x.speakerId == jurorId && !x.isPlayerAuthored && !string.IsNullOrWhiteSpace(x.text));
            return spoken == null ? ((int, string)?)null : (spoken.week, spoken.text.Trim());
        }

        /// <summary>The juror's callout line (decision 41, A); see <see cref="Callout"/>.</summary>
        public static Callout Line(EpisodeState s, string jurorId) => ReadJuror(s, jurorId).line;

        private static Callout LineFor(EpisodeState s, Juror juror)
        {
            var asked = Question(s, juror.id);
            if (asked != null)
            {
                string note = Note(s, asked);
                return new Callout { when = "Tonight", words = asked.question.Trim(), quoted = true, note = note, noteTail = NoteTail(s, juror.id, note) };
            }
            var plea = Plea(s, juror.id);
            if (plea != null) return new Callout { when = "Week " + plea.Value.week + ", from the block", words = plea.Value.words, quoted = true };
            return new Callout { words = juror.reason };
        }

        /// <summary>The week a juror left: the power row that names them the evictee. Null when the row is not on the record (an older save, or the ledger's cap).</summary>
        public static int? LeftWeek(EpisodeState s, string jurorId) =>
            s.ledger?.power?.LastOrDefault(p => p.evicteeId == jurorId)?.week;

        /// <summary>The player's last read of them: their view of the player, learned. Never the <c>juror</c> row, never a miss or a deflection.</summary>
        public static StandingRow LastRead(EpisodeState s, string jurorId) =>
            s.ledger?.standings?.LastOrDefault(r => r.source == ClaimSource.Read && r.fromId == jurorId && r.toId == s.playerId);

        /// <summary>A power row that records the final eviction: no tally, an evictee, and no veto.</summary>
        private static bool FinalEvictionRow(PowerRow p) => p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null;

        /// <summary>What the player did to them where the house could see it, with its week: the public record's case against the player.</summary>
        public static List<(int week, string text, bool evicted)> PublicActs(EpisodeState s, string jurorId)
        {
            var acts = new List<(int, string, bool)>();
            string player = s.playerId;
            if (s.ledger?.power == null) return acts;
            foreach (var p in s.ledger.power)
            {
                if (p.evicteeId == jurorId && p.hohId == player && FinalEvictionRow(p))
                { acts.Add((p.week, "you evicted them at the final eviction", true)); continue; }
                if (p.evicteeId == jurorId && p.hohId == player && p.tally.Count == 2 && p.tally[0] == p.tally[1]
                    && s.ledger.ballots.Any(b => b.week == p.week && b.voterId == player && b.targetId == jurorId))
                { acts.Add((p.week, "you broke the tie to evict them", true)); continue; }
                if (FinalEvictionRow(p)) continue;
                if (p.hohId == player && p.replacementId == jurorId) acts.Add((p.week, "you named them the replacement", false));
                else if (p.hohId == player && (p.nominees.Contains(jurorId) || p.savedId == jurorId)) acts.Add((p.week, "you nominated them", false));
                else if (p.vetoHolderId == player && p.vetoUsed && p.replacementId == jurorId) acts.Add((p.week, "your veto put them on the block", false));
            }
            return acts;
        }

        /// <summary>
        /// What cooled them after a warm read: a promise the player broke them, a plea the player
        /// refused, a vote to evict them, a deal between them that broke - counted only when it
        /// provably came after the read. The record holds no break week and no phase: a read in a
        /// week comes after that week's ceremonies, but a same-week campaign or reveal cannot be
        /// placed against it, so only a later week counts. A Final 2 agreement is the exception:
        /// only the final eviction breaks it, and that is after every read.
        /// </summary>
        private static string CooledSince(EpisodeState s, string jurorId, int week)
        {
            string player = s.playerId;
            // Under the commitment rules (C0) the record holds the week a promise or a deal broke, and a
            // deal cools them only when the player broke it: one they broke is not the player's to
            // answer for (X3). Before them, the guesses below and any deal between the two of them.
            bool rules = EpisodeEngine.CommitmentRulesOn(s);
            bool Later(PromiseState p) =>
                rules && p.settledWeek > 0 ? p.settledWeek > week
                : (p.kind == PromiseKind.FinalTwo
                   // Safety and alliance loyalty break at a nomination, no later than the promise's end.
                   || ((p.kind == PromiseKind.Safety || p.kind == PromiseKind.AllianceLoyalty) && p.expiresWeek > week)
                   || p.week > week);
            bool Between(DealState d) => (d.proposerId == player && d.recipientId == jurorId) || (d.proposerId == jurorId && d.recipientId == player);
            if (s.promises.Any(p => p.fromId == player && p.toId == jurorId && p.status == PromiseStatus.Broken && Later(p)))
                return "you broke a promise to them since";
            if (s.ledger?.replies != null && s.ledger.replies.Any(r => r.kind == ReplyCards.Plea && r.fromId == jurorId && r.replyKey == "refuse" && r.week > week))
                return "you refused their plea since";
            if (s.ledger?.ballots != null && s.ledger.ballots.Any(b => b.voterId == player && b.targetId == jurorId && b.week > week && CouldKnowYourBallot(s, b.week, jurorId)))
                return "you voted to evict them since";
            if (rules)
            {
                if (s.deals.Any(d => Between(d) && Breaches.Broke(s, d, player) && Breaches.BrokeAfter(d, week)))
                    return "you broke a deal with them since";
            }
            else if (s.deals.Any(d => d.status == DealStatus.Broken && (d.week > week || d.type == DealKind.FinalTwo) && Between(d)))
                return "a deal between you broke since";
            return null;
        }

        private static string Word(double score) => score >= FinalistRead.WarmStanding ? "warm on you" : score <= FinalistRead.ColdStanding ? "cold on you" : "not sure about you";

        /// <summary>
        /// Whether a juror could know the player's ballot in <paramref name="week"/> from their own
        /// seat: the count's proof, or the player's tie-break (<see cref="KnownBallots.ProvenFor"/>).
        /// The reveal reads the count, never the ballots, so a ballot the count did not prove is
        /// nothing a juror can hold against the player. Telling them is wave B's.
        /// </summary>
        public static bool CouldKnowYourBallot(EpisodeState s, int week, string jurorId) =>
            KnownBallots.ProvenFor(s, week, jurorId).ContainsKey(s.playerId);

        public static Juror ReadJuror(EpisodeState s, string jurorId)
        {
            var actor = s.Find(jurorId);
            var juror = new Juror { id = jurorId, trait = WebJuryQuestioning.GetPrimaryTrait(actor?.traits), leftWeek = LeftWeek(s, jurorId) };
            var read = LastRead(s, jurorId);
            juror.readWeek = read?.week;
            var acts = PublicActs(s, jurorId);

            var eviction = acts.Where(a => a.evicted).OrderBy(a => a.week).LastOrDefault();
            var latest = acts.OrderBy(a => a.week).LastOrDefault();
            if (eviction.text != null)
            {
                juror.band = Bitter; juror.reason = "Week " + eviction.week + ": " + eviction.text + ".";
            }
            else if (latest.text != null && (read == null || latest.week > read.week || (latest.week == read.week && read.week == 1)))
            {
                // The freshest evidence speaks: nothing learned of them since the player put them up.
                // A read in the same week came after it: reads are taken in the campaign or free
                // time, and the week's ceremonies come first - except the opening week, whose free
                // time comes before its first nominations.
                juror.band = Bitter; juror.reason = "Week " + latest.week + ": " + latest.text + ".";
            }
            else if (read == null)
            {
                juror.band = Unknown; juror.reason = "You never read them.";
            }
            else
            {
                string heard = "Your read in week " + read.week + ": " + Word(read.score) + ".";
                if (read.score <= FinalistRead.ColdStanding) { juror.band = Skeptical; juror.reason = heard; }
                else if (read.score >= FinalistRead.WarmStanding)
                {
                    string cooled = CooledSince(s, jurorId, read.week);
                    juror.band = cooled == null ? Supportive : Wavering;
                    juror.reason = cooled == null ? heard : heard + " Then " + cooled + ".";
                }
                else { juror.band = Open; juror.reason = heard; }
            }

            Knows(s, juror);
            Missing(s, juror);
            Highlights(s, juror, acts, read);
            juror.line = LineFor(s, juror);
            return juror;
        }

        private static void Knows(EpisodeState s, Juror juror)
        {
            string player = s.playerId, id = juror.id;
            foreach (var alliance in s.alliances.Where(a => a.members.Contains(player) && a.members.Contains(id)))
                juror.knows.Add("You shared " + alliance.name + (alliance.active ? "." : ", now ended."));
            foreach (var deal in s.deals.Where(d => (d.proposerId == player && d.recipientId == id) || (d.proposerId == id && d.recipientId == player)))
                juror.knows.Add(DealKind.Title(deal.type) + ": " + HouseguestNotes.DealStanding(deal.status, deal.proposerId == id) + ".");
            foreach (var promise in s.promises.Where(p => (p.fromId == player && p.toId == id) || (p.fromId == id && p.toId == player)))
                juror.knows.Add((promise.fromId == player ? "You promised them " : "They promised you ") + HouseguestNotes.PromiseWord(promise.kind)
                    + ": " + HouseguestNotes.PromiseStanding(promise.status) + ".");
            // The player's ballot against them is theirs to know only where the count showed it
            // (UI-UX-PASS-PLAN B0: the reveal reads the count, not the ballots).
            int votes = s.ledger?.ballots?.Count(b => b.voterId == player && b.targetId == id && CouldKnowYourBallot(s, b.week, id)) ?? 0;
            if (votes > 0) juror.knows.Add("You voted to evict them " + (votes == 1 ? "once" : votes + " times") + ", and the count showed it.");
            string game = SawYourGame(s, juror);
            if (game != null) juror.knows.Add(game);
            var goodbye = Goodbye(s, id);
            if (goodbye != null) juror.knows.Add("They watched your goodbye: " + goodbye.Value.said + ".");
        }

        /// <summary>
        /// What they saw of the player's game before they left: the exact complement of
        /// <see cref="Missing"/> over the same rows - the player's wins, their weeks holding the
        /// house and the vetoes they used, up to and including the week the juror left, the final
        /// eviction's row aside. Null when there is none, or when the record does not say when they
        /// left, which is when nothing is missing either.
        /// </summary>
        private static string SawYourGame(EpisodeState s, Juror juror)
        {
            if (juror.leftWeek == null) return null;
            int left = juror.leftWeek.Value;
            string player = s.playerId;
            var ledger = s.ledger ?? new SeasonLedger();
            int wins = ledger.competitions.Count(c => c.week <= left && c.placement == 1);
            var seen = ledger.power.Where(p => p.week <= left && !FinalEvictionRow(p)).ToList();
            int reign = seen.Count(p => p.hohId == player);
            int vetoes = seen.Count(p => p.vetoHolderId == player && p.vetoUsed);
            var parts = new List<string>();
            if (wins > 0) parts.Add(wins + (wins == 1 ? " win" : " wins"));
            if (reign > 0) parts.Add(reign + (reign == 1 ? " week" : " weeks") + " as Head of Household");
            if (vetoes > 0) parts.Add(vetoes + (vetoes == 1 ? " veto" : " vetoes") + " used");
            if (parts.Count == 0) return null;
            string joined = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
            return "Your " + joined + " before they left.";
        }

        /// <summary>
        /// The goodbye message the player recorded for this juror in the Diary Room on the night they
        /// left, which production played them: the week, and the choice taken as what the player did
        /// ("you kept it classy"). Null when there was none or the player skipped it. Never the
        /// effect the choice had on them, which is the vote model's.
        /// </summary>
        public static (int week, string said)? Goodbye(EpisodeState s, string jurorId)
        {
            var story = s.storylines?.LastOrDefault(x => x != null && x.templateId == GoodbyeTemplate
                && x.cast != null && x.cast.Any(role => role != null && role.role == "EVICTEE" && role.contestantId == jurorId));
            var step = story?.path?.LastOrDefault(p => p != null && GoodbyeWords(p.optionId) != null);
            if (step == null) return null;
            return (step.week > 0 ? step.week : story.week, GoodbyeWords(step.optionId));
        }

        private static string GoodbyeWords(string optionId)
        {
            switch (optionId)
            {
                case "classy": return "you kept it classy";
                case "tell-why": return "you told them why";
                case "rub-it-in": return "you rubbed it in";
                default: return null;
            }
        }

        private static void Missing(EpisodeState s, Juror juror)
        {
            if (juror.leftWeek == null) return;
            int left = juror.leftWeek.Value;
            string player = s.playerId;
            if (s.ledger?.power != null)
                foreach (var p in s.ledger.power.Where(p => p.week > left && !FinalEvictionRow(p)))
                {
                    if (p.hohId == player) juror.missing.Add("Week " + p.week + ": you held the house.");
                    if (p.vetoHolderId == player && p.vetoUsed) juror.missing.Add("Week " + p.week + ": you used the veto.");
                }
            if (s.ledger?.competitions != null)
                foreach (var c in s.ledger.competitions.Where(c => c.week > left && c.placement == 1))
                    juror.missing.Add("Week " + c.week + ": you won " + CompetitionWord(c.kind) + ".");
            // Deals the player actually struck: not offers declined, lapsed or still waiting.
            int deals = s.deals.Count(d => d.week > left && (d.proposerId == player || d.recipientId == player) && d.proposerId != juror.id && d.recipientId != juror.id
                && (d.status == DealStatus.Accepted || d.status == DealStatus.Active || d.status == DealStatus.Fulfilled || d.status == DealStatus.Broken));
            if (deals > 0) juror.missing.Add(deals + (deals == 1 ? " deal" : " deals") + " you struck in the weeks after they left.");
        }

        private static string CompetitionWord(string kind)
        {
            switch (kind)
            {
                case "HoH": return "Head of Household";
                case "Veto": return "the veto";
                case "FinalHoHPart1": return "Final HoH Part 1";
                case "FinalHoHPart2": return "Final HoH Part 2";
                case "FinalHoHPart3": return "the final Head of Household";
                default: return "a competition";
            }
        }

        private static void Highlights(EpisodeState s, Juror juror, List<(int week, string text, bool evicted)> acts, StandingRow read)
        {
            var lines = new List<(int week, string text)>();
            string player = s.playerId, id = juror.id;
            if (juror.leftWeek != null)
            {
                var row = s.ledger.power.LastOrDefault(p => p.evicteeId == id);
                string how = row == null ? "left the house"
                    : row.hohId == player && FinalEvictionRow(row) ? "left at your final eviction"
                    : row.hohId == player ? "left on your nomination"
                    : row.hohId != null ? "left in " + FinalistRead.FirstName(s.Find(row.hohId)?.name) + "'s week"
                    : "left the house";
                lines.Add((juror.leftWeek.Value, how));
            }
            foreach (var act in acts.Where(a => !a.evicted && (juror.leftWeek == null || a.week != juror.leftWeek.Value || !a.text.Contains("nominated"))))
                lines.Add((act.week, act.text));
            if (read != null) lines.Add((read.week, "your read: " + Word(read.score)));
            if (s.ledger?.ballots != null)
                foreach (var b in s.ledger.ballots.Where(b => b.voterId == player && b.targetId == id && CouldKnowYourBallot(s, b.week, id)))
                    lines.Add((b.week, "you voted to evict them"));
            if (s.ledger?.replies != null)
                foreach (var r in s.ledger.replies.Where(r => r.fromId == id))
                {
                    string answer = (ReplyCards.Find(r.kind, r.replyKey)?.Label ?? r.replyKey ?? "").ToLowerInvariant();
                    string what = r.kind == ReplyCards.Plea ? "they pleaded with you"
                        : r.kind == ReplyCards.Confrontation ? "they confronted you"
                        : r.kind == ReplyCards.Gossip ? "you caught them talking about you"
                        : "they came to you";
                    lines.Add((r.week, what + "; you answered " + answer));
                }
            var goodbye = Goodbye(s, id);
            if (goodbye != null) lines.Add((goodbye.Value.week, "they watched your goodbye: " + goodbye.Value.said));
            juror.dated = lines.OrderBy(l => l.week).Distinct().ToList();
            juror.highlights = juror.dated.Select(l => "Week " + l.week + " · " + l.text).Distinct().ToList();
        }
    }
}
