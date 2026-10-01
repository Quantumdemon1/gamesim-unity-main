using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The player's week, judged (ACTIONS-DEALS-ALLIANCES-PLAN V4): every read and claim they held
    /// against what the reveal showed, the deals and promises that ended that week and how, the calls
    /// they made in their alliances and who followed, and their Game Sense so far.
    ///
    /// <para><b>Only what the player can know after the reveal.</b> The reveal reads every ballot
    /// aloud, so a claim is judged against a ballot the whole house heard, and the whip count against
    /// the evictee. A deal or a promise is judged by the outcome line the player was told, in the words
    /// they were told it: for a deal, the player's own copy of that line on their record with the other
    /// party (which outlasts the log's cap); for a promise, the log line and the player's own memory of
    /// it. A call is judged by who the player was told followed and who would not. Game Sense is the
    /// verdict's own notes, the known ones only (<see cref="GameSense.Note.known"/>): its strategy face
    /// is made of public and player-owned rows, while its competitions face rests on odds and its
    /// social face on how the house sees the player, neither of which anybody is shown mid-season.</para>
    ///
    /// <para>Plain data read from committed state: nothing here rolls, writes or changes an outcome.
    /// The plays and threads the player advanced are not repeated here: the recap's acts already list
    /// every story beat the week answered.</para>
    /// </summary>
    public static class YourWeek
    {
        /// <summary>What the week made of something the player held.</summary>
        public static class Verdicts
        {
            public const string Right = "right", Wrong = "wrong", NotKnown = "not known";
            public const string Kept = "kept", Broken = "broken";
            public const string Followed = "followed", Defected = "defected";
            public static readonly string[] All = { Right, Wrong, NotKnown, Kept, Broken, Followed, Defected };
        }

        /// <summary>What a line is about.</summary>
        public static class Kinds
        {
            public const string WhipCount = "whip count", Claim = "claim", Deal = "deal", Promise = "promise", Call = "call", Member = "member";
        }

        /// <summary>The most rows of the week's Game Sense the recap prints.</summary>
        public const int SenseRowLimit = 6;

        /// <summary>One thing the player held, and what the week made of it.</summary>
        public sealed class Line
        {
            /// <summary>One of <see cref="Kinds"/>.</summary>
            public string kind;
            /// <summary>One of <see cref="Verdicts"/>; null for a call's own line, which carries no verdict of its own.</summary>
            public string verdict;
            public string text;
            /// <summary>Whose face the line carries: the voter, the other party, the member; null for the player's own whip count and call.</summary>
            public string aboutId;
            /// <summary>Who kept or broke a deal or promise; null where a pair held or fell out together, and for the other kinds.</summary>
            public string byId;

            public override string ToString() => verdict == null ? text : text + " · " + verdict;
        }

        /// <summary>Game Sense so far, in the parts the player can already see.</summary>
        public sealed class Sense
        {
            /// <summary>The strategy face through the week, from the known notes alone.</summary>
            public int strategy = (int)GameSense.Base;
            /// <summary>The chances the season had offered through the week, and how many were taken.</summary>
            public int offered, taken;
            /// <summary>The week's own known notes of the strategy and competitions faces, as the verdict wrote them.</summary>
            public List<GameSense.Note> rows = new List<GameSense.Note>();
        }

        public sealed class Week
        {
            public int week;
            /// <summary>The whip count the player voted on, then every claim they held, in the order they heard them.</summary>
            public List<Line> reads = new List<Line>();
            /// <summary>The deals, then the promises, that ended this week with the player as a party.</summary>
            public List<Line> word = new List<Line>();
            /// <summary>Each call the player made: its own line, then who followed and who would not.</summary>
            public List<Line> calls = new List<Line>();
            public Sense sense = new Sense();

            /// <summary>A week with nothing of the player's to judge.</summary>
            public bool Empty => reads.Count == 0 && word.Count == 0 && calls.Count == 0;

            /// <summary>Every line, reads first, as the recap prints them.</summary>
            public IEnumerable<Line> Lines => reads.Concat(word).Concat(calls);
        }

        /// <summary>The player's week <paramref name="week"/>, judged as far as the house can see it now.</summary>
        public static Week Build(EpisodeState s, int week)
        {
            var mine = new Week { week = week };
            if (s?.ledger == null || week < 1 || week > s.week || s.Find(s.playerId) == null) return mine;
            var power = s.ledger.power.LastOrDefault(p => p.week == week);
            bool revealed = power != null && power.evicteeId != null;
            Reads(s, week, power, revealed, mine.reads);
            Deals(s, week, mine.word);
            Promises(s, week, mine.word);
            Calls(s, week, power, revealed, mine.calls);
            mine.sense = SenseSoFar(s, week);
            return mine;
        }

        // ---------------------------------------------------------------- reads and claims

        /// <summary>
        /// The whip count the player cast their ballot on, judged against the evictee, and every
        /// claim of the week, judged against the ballot the reveal read for that voter.
        /// </summary>
        private static void Reads(EpisodeState s, int week, PowerRow power, bool revealed, List<Line> lines)
        {
            var ballot = s.ledger.ballots.LastOrDefault(b => b.week == week && b.voterId == s.playerId);
            if (ballot != null)
            {
                if (ballot.readBefore == null)
                    lines.Add(new Line { kind = Kinds.WhipCount, verdict = Verdicts.NotKnown, text = "Your whip count called nobody when you voted." });
                else
                {
                    string said = "Your whip count said " + Whom(s, ballot.readBefore) + " would go";
                    var line = new Line { kind = Kinds.WhipCount };
                    if (!revealed) { line.verdict = Verdicts.NotKnown; line.text = said + ". The vote is still to come."; }
                    else if (ballot.correct) { line.verdict = Verdicts.Right; line.text = said + ", and they did."; }
                    else
                    {
                        line.verdict = Verdicts.Wrong;
                        line.text = said + "; " + Who(s, power.evicteeId) + " went instead.";
                    }
                    lines.Add(line);
                }
            }

            foreach (var claim in s.ledger.claims.Where(c => c.week == week))
            {
                // What they said, as the notes say it; a claim naming the player says so plainly.
                bool you = claim.targetId == s.playerId;
                string voting = you ? " is voting you out" : " is voting out " + Whom(s, claim.targetId);
                string said = claim.source == ClaimSource.Told
                        ? Who(s, claim.voterId) + (you ? " told you they would vote you out" : " told you: evict " + Whom(s, claim.targetId))
                    : claim.source == ClaimSource.Overheard ? "Overheard: " + Who(s, claim.voterId) + voting
                    : "An ally heard " + Who(s, claim.voterId) + voting;
                var line = new Line { kind = Kinds.Claim, aboutId = claim.voterId };
                if (claim.status == ClaimStatus.Kept) { line.verdict = Verdicts.Right; line.text = said + ", and voted that way."; }
                else if (claim.status == ClaimStatus.Lied)
                {
                    line.verdict = Verdicts.Wrong;
                    // The block was two; the ballot that was not the claim's was the other's.
                    string other = power != null && power.nominees.Count == 2 && power.nominees.Contains(claim.targetId)
                        ? power.nominees.First(id => id != claim.targetId) : null;
                    line.text = said + (other != null ? ", and voted to evict " + (other == s.playerId ? "you" : Whom(s, other)) + "." : ", and voted the other way.");
                }
                else
                {
                    line.verdict = Verdicts.NotKnown;
                    line.text = said + (revealed ? ". Their vote never came." : ". The vote is still to come.");
                }
                lines.Add(line);
            }
        }

        // ---------------------------------------------------------------- deals and promises

        /// <summary>
        /// The deals that ended this week with the player as a party: each is written once onto the
        /// player's own record with the other party, in the words of the line the player was told,
        /// typed kept or broken. Who kept or broke it, and what it was, are read from those words.
        /// </summary>
        private static void Deals(EpisodeState s, int week, List<Line> lines)
        {
            var settled = s.relationships
                .Where(r => r.fromId == s.playerId && r.toId != s.playerId && s.Find(r.toId) != null)
                .SelectMany(r => r.events.Where(e => e.week == week && (e.type == DealKept || e.type == DealBroken)).Select(e => (partner: r.toId, record: e)))
                .OrderBy(x => x.record.sequence);
            foreach (var (partner, record) in settled)
            {
                bool kept = record.type == DealKept;
                var line = new Line { kind = Kinds.Deal, verdict = kept ? Verdicts.Kept : Verdicts.Broken, aboutId = partner };
                if (ReadDeal(s, record.description, partner, out string actorId, out string type, out bool said) && said == kept)
                {
                    line.byId = actorId;
                    string what = DealWords(type);
                    line.text = actorId == null
                        ? "You and " + Whom(s, partner) + (kept ? " held to your " : " fell out over your ") + what + "."
                        : actorId == s.playerId
                            ? "You " + (kept ? "kept" : "broke") + " the " + what + " with " + Whom(s, partner) + "."
                            : Who(s, partner) + " " + (kept ? "kept" : "broke") + " the " + what + " with you.";
                }
                // A line in words this reader does not know still says what it says.
                else line.text = record.description ?? (kept ? "A deal with " + Whom(s, partner) + " was kept." : "A deal with " + Whom(s, partner) + " was broken.");
                lines.Add(line);
            }
        }

        /// <summary>The record types a deal's end writes on both parties' records (EpisodeEngine.SettleDeals).</summary>
        public const string DealKept = "deal_fulfilled", DealBroken = "deal_broken";

        /// <summary>
        /// Who a deal's outcome line names as keeping or breaking it, and the deal's kind, by matching
        /// the line against the engine's own sentences for this pair: "{who} honoured a {title} with
        /// {whom}.", "{who} broke a {title} with {whom}.", and for a pair that acted together "{a} and
        /// {b} held to their {title}." or "{a} and {b} fell out over their {title}.". False where the
        /// line is none of them.
        /// </summary>
        public static bool ReadDeal(EpisodeState s, string text, string partnerId, out string actorId, out string type, out bool kept)
        {
            actorId = null; type = null; kept = false;
            if (s == null || string.IsNullOrEmpty(text)) return false;
            string you = EngineName(s, s.playerId), them = EngineName(s, partnerId);
            foreach (var kind in DealKind.All)
            {
                string title = DealKind.Title(kind).ToLowerInvariant();
                foreach (bool honoured in new[] { true, false })
                {
                    string verb = honoured ? " honoured a " : " broke a ", pair = honoured ? " held to their " : " fell out over their ";
                    string mineSays = you + verb + title + " with " + them + ".", theirsSays = them + verb + title + " with " + you + ".";
                    if (text == mineSays || text == theirsSays)
                    {
                        actorId = text == mineSays ? s.playerId : partnerId; type = kind; kept = honoured;
                        return true;
                    }
                    if (text == you + " and " + them + pair + title + "." || text == them + " and " + you + pair + title + ".")
                    {
                        actorId = null; type = kind; kept = honoured;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The promises that ended this week with the player as a party, as they were told it: the
        /// week's outcome lines logged to them, and the same words in their own memory, which outlasts
        /// the log's cap. Each outcome is written once to each, so whichever kept more of the week
        /// holds all of it.
        /// </summary>
        private static void Promises(EpisodeState s, int week, List<Line> lines)
        {
            var told = s.events
                .Where(e => e.week == week && e.kind == "promise-outcome" && e.audienceIds != null && e.audienceIds.Contains(s.playerId))
                .OrderBy(e => e.sequence)
                .Select(e => (text: e.text, partner: e.audienceIds.FirstOrDefault(id => id != s.playerId)))
                .Where(x => x.partner != null && x.text != null).ToList();
            var remembered = s.memories
                .Where(m => m.ownerId == s.playerId && m.week == week && m.subjectId != null && m.subjectId != s.playerId && m.text != null)
                .Select(m => (text: m.text, partner: m.subjectId))
                .Where(x => ReadPromise(s, x.text, x.partner, out _, out _, out _)).ToList();
            foreach (var outcome in told.Concat(remembered).Distinct().ToList())
            {
                int times = Math.Max(told.Count(x => x == outcome), remembered.Count(x => x == outcome));
                for (int i = 0; i < times; i++) lines.Add(PromiseLine(s, outcome.text, outcome.partner));
            }
        }

        private static Line PromiseLine(EpisodeState s, string text, string partner)
        {
            var line = new Line { kind = Kinds.Promise, aboutId = partner };
            if (ReadPromise(s, text, partner, out string fromId, out PromiseKind kind, out bool kept))
            {
                line.verdict = kept ? Verdicts.Kept : Verdicts.Broken;
                line.byId = fromId;
                string what = PromiseWords(kind);
                line.text = fromId == s.playerId
                    ? "You " + (kept ? "kept" : "broke") + " your " + what + " to " + Whom(s, partner) + "."
                    : Who(s, partner) + " " + (kept ? "kept" : "broke") + " their " + what + " to you.";
                return line;
            }
            // A line in words this reader does not know still says what it says.
            line.text = text;
            line.verdict = text.Contains(" broke ") ? Verdicts.Broken : text.Contains(" fulfilled ") ? Verdicts.Kept : null;
            return line;
        }

        /// <summary>
        /// Who made a promise, its kind and how it ended, by matching its outcome line against the
        /// engine's own sentence for this pair: "{who} fulfilled a {Kind} promise." or "{who} broke a
        /// {Kind} promise.". False where the line is neither.
        /// </summary>
        public static bool ReadPromise(EpisodeState s, string text, string partnerId, out string fromId, out PromiseKind kind, out bool kept)
        {
            fromId = null; kind = PromiseKind.Safety; kept = false;
            if (s == null || string.IsNullOrEmpty(text)) return false;
            foreach (var from in new[] { s.playerId, partnerId })
                foreach (PromiseKind each in Enum.GetValues(typeof(PromiseKind)))
                    foreach (bool fulfilled in new[] { true, false })
                    {
                        if (text != EngineName(s, from) + (fulfilled ? " fulfilled" : " broke") + " a " + each + " promise.") continue;
                        fromId = from; kind = each; kept = fulfilled;
                        return true;
                    }
            return false;
        }

        // ---------------------------------------------------------------- calls

        /// <summary>Each call the player made this week: its own line, then who followed it and who would not, as they were told at the call.</summary>
        private static void Calls(EpisodeState s, int week, PowerRow power, bool revealed, List<Line> lines)
        {
            foreach (var call in s.ledger.calls.Where(c => c.week == week && c.callerId == s.playerId))
            {
                string pact = s.alliances.FirstOrDefault(a => a.id == call.allianceId)?.name ?? "your alliance";
                string target = Whom(s, call.targetId);
                string after = !revealed ? "."
                    : power.evicteeId == call.targetId ? ", and " + target + " went home."
                    : ", and " + target + " stayed.";
                lines.Add(new Line { kind = Kinds.Call, text = "You called it in " + pact + ": evict " + target + after });
                foreach (var id in call.followed)
                    lines.Add(new Line { kind = Kinds.Member, verdict = Verdicts.Followed, aboutId = id, text = Who(s, id) + " followed your call." });
                foreach (var id in call.defected)
                    lines.Add(new Line { kind = Kinds.Member, verdict = Verdicts.Defected, aboutId = id, text = Who(s, id) + " would not follow your call." });
            }
        }

        // ---------------------------------------------------------------- Game Sense so far

        /// <summary>
        /// Game Sense through the week in the parts the player can see: the strategy face from the
        /// known notes, the chances taken, and the week's own known notes. The number itself and the
        /// competitions and social faces wait for the season's end.
        /// </summary>
        private static Sense SenseSoFar(EpisodeState s, int week)
        {
            var sense = new Sense();
            // A play still running has been neither taken up nor let pass yet: the verdict, which reads
            // a season that is over, counts an unanswered one as let pass, which it is not until it closes.
            var running = new HashSet<string>((s.storylines ?? new List<StorylineState>())
                .Where(cycle => StorylineStatus.Running(cycle.status)).Select(cycle => cycle.id), StringComparer.Ordinal);
            var known = GameSense.Evaluate(s).notes
                .Where(n => n.known && n.week <= week && !(n.rowKind == "opportunity" && n.rowId != null && running.Contains(n.rowId))).ToList();
            sense.strategy = GameSense.Face(known, GameSense.Strategy);
            var chances = s.ledger.opportunities.Where(o => o.week <= week).ToList();
            sense.offered = chances.Count;
            sense.taken = chances.Count(o => o.response == OpportunityResponse.Taken);
            // The social face's notes carry the week a pact or bond began, not a week's event.
            sense.rows = known.Where(n => n.week == week && n.face != GameSense.Social).Take(SenseRowLimit).ToList();
            return sense;
        }

        /// <summary>A Game Sense note without the "Week 3: " it opens with, which a week's own recap does not need.</summary>
        public static string RowText(GameSense.Note note)
        {
            if (note?.text == null) return string.Empty;
            string prefix = "Week " + note.week + ": ";
            if (!note.text.StartsWith(prefix, StringComparison.Ordinal)) return note.text;
            string rest = note.text.Substring(prefix.Length);
            return rest.Length == 0 ? rest : char.ToUpperInvariant(rest[0]) + rest.Substring(1);
        }

        // ---------------------------------------------------------------- words

        /// <summary>A deal's kind in a few words.</summary>
        public static string DealWords(string type)
        {
            switch (type)
            {
                case DealKind.TargetAgreement: return "target agreement";
                case DealKind.SafetyAgreement: return "safety pact";
                case DealKind.VoteTogether: return "voting block";
                case DealKind.VoteSave: return "vote-to-save deal";
                case DealKind.VoteEvict: return "vote-to-evict deal";
                case DealKind.VetoUse: return "veto commitment";
                case DealKind.InformationSharing: return "information deal";
                case DealKind.FinalTwo: return "final two deal";
                case DealKind.AllianceInvite: return "alliance invitation";
                default: return "partnership";
            }
        }

        /// <summary>A promise's kind in a few words.</summary>
        public static string PromiseWords(PromiseKind kind)
        {
            switch (kind)
            {
                case PromiseKind.Safety: return "safety promise";
                case PromiseKind.Vote: return "vote promise";
                case PromiseKind.FinalTwo: return "final two promise";
                case PromiseKind.AllianceLoyalty: return "loyalty promise";
                default: return "promise to share";
            }
        }

        /// <summary>The name the engine writes into its sentences, the player's included.</summary>
        private static string EngineName(EpisodeState s, string id) => s.Find(id)?.name ?? "Unknown housemate";

        /// <summary>A houseguest opening a sentence: "You" for the player.</summary>
        private static string Who(EpisodeState s, string id) => id == s.playerId ? "You" : s.Find(id)?.name ?? "Somebody";

        /// <summary>A houseguest after the verb: "you" for the player.</summary>
        private static string Whom(EpisodeState s, string id) => id == s.playerId ? "you" : s.Find(id)?.name ?? "somebody";
    }
}
