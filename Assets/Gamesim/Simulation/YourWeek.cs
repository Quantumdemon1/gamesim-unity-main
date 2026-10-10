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
    /// <para><b>Only what the player can know after the reveal.</b> The reveal reads the count, not
    /// the ballots (UI-UX-PASS-PLAN B0): a claim is judged against the ballot as the player knows it
    /// (<see cref="KnownBallots"/> - their own, the tie-break, what the count proves, what they were
    /// told and the reveal judged), and is "not known" where they cannot place it; the whip count
    /// against the evictee. A deal or a promise is judged by the outcome line the player was told, in
    /// the words they were told it, in the week it was told: for a deal, the player's own copy of
    /// that line on their record with the other party, which outlasts the log; for a promise, the log
    /// line, which keeps the house's last 256 lines, and the player's own memory of it, which keeps
    /// their last 30. A vote deal or a vote promise the other party settled by their ballot reads
    /// unresolved until the player knows that ballot (decision 4). A promise from a week both have
    /// rolled past is not on the list, so a review of an early week in a long season can show fewer
    /// promises than that week ended. A call's members are judged by their ballots where the player
    /// knows them, and otherwise by what each member said at the call. Game Sense is the verdict's
    /// own notes, the known ones only (<see cref="GameSense.Note.known"/>): its strategy face is made
    /// of public and player-owned rows, while its competitions face rests on odds and its social face
    /// on how the house sees the player, neither of which anybody is shown mid-season.</para>
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
            /// <summary>Under the leak rules (WAVE-D-NPC-PACTS-PLAN D4-6): somebody found out about another of the player's pacts.</summary>
            public const string FoundOut = "found out";
            /// <summary>Under the war rooms (WAVE-D-NPC-PACTS-PLAN D3-S7): a pact's plan, as the player answered it; its voters follow as members.</summary>
            public const string Plan = "plan";
        }

        /// <summary>What a call member's verdict rests on: the ballot as the player knows it, or what they said at the call.</summary>
        public static class Bases
        {
            public const string Ballot = "ballot", Call = "call";
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
            /// <summary>For a call's member, what the verdict rests on (<see cref="Bases"/>); null for the other kinds.</summary>
            public string basis;
            /// <summary>Canonical agreement provenance, not extra outcome counts; null for legacy lines.</summary>
            public IReadOnlyList<string> evidenceIds;
            /// <summary>One canonical breach identity, or null for a fulfillment or legacy line.</summary>
            public string effectKey;

            public override string ToString() => verdict == null ? text : text + " · " + verdict;
        }

        /// <summary>Game Sense so far, in the parts the player can already see.</summary>
        public sealed class Sense
        {
            /// <summary>The strategy face through the week, from the known notes alone; held at the week the player's season ended.</summary>
            public int strategy = (int)GameSense.Base;
            /// <summary>The chances the season had offered through the week, and how many were taken; held as the strategy face is.</summary>
            public int offered, taken;
            /// <summary>The week's own known notes of the strategy and competitions faces, as the verdict wrote them; none after the player's season ended.</summary>
            public List<GameSense.Note> rows = new List<GameSense.Note>();
            /// <summary>The week the player's season ended, when this week came after it; 0 otherwise.</summary>
            public int endedWeek;

            /// <summary>"Your season ended in week 4.", for a week after the player left; null otherwise.</summary>
            public string Ended => endedWeek > 0 ? "Your season ended in week " + endedWeek + "." : null;
        }

        public sealed class Week
        {
            public int week;
            /// <summary>The whip count the player voted on, then every claim they held, in the order they heard them.</summary>
            public List<Line> reads = new List<Line>();
            /// <summary>The deals, then the promises, that ended this week with the player as a party.</summary>
            public List<Line> word = new List<Line>();
            /// <summary>
            /// Each call the player made: its own line, then who followed and who would not. Under the war rooms
            /// (D3-S7) then each plan of their pacts, a call it made among them: its own line, then who of it voted.
            /// </summary>
            public List<Line> calls = new List<Line>();
            /// <summary>Who found out about another of the player's pacts, under the leak rules (D4-6): each line the player read.</summary>
            public List<Line> exposed = new List<Line>();
            public Sense sense = new Sense();

            /// <summary>A week with nothing of the player's to judge.</summary>
            public bool Empty => reads.Count == 0 && word.Count == 0 && calls.Count == 0 && exposed.Count == 0;

            /// <summary>Every line, reads first, as the recap prints them.</summary>
            public IEnumerable<Line> Lines => reads.Concat(word).Concat(calls).Concat(exposed);
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
            CanonicalWord(s, week, mine.word);
            Calls(s, week, power, revealed, mine.calls);
            Exposures(s, week, mine.exposed);
            mine.sense = SenseSoFar(s, week);
            return mine;
        }

        // ---------------------------------------------------------------- reads and claims

        /// <summary>
        /// The whip count the player cast their ballot on, judged against the evictee, and every
        /// claim of the week, judged against that voter's ballot as the player knows it: right where
        /// the known ballot is what they said, wrong where it is not, and not known where the player
        /// cannot place it. The count can prove a claim false on its own - every ballot is placed
        /// when the vote is unanimous - without the house ever reading a ballot aloud.
        /// </summary>
        private static void Reads(EpisodeState s, int week, PowerRow power, bool revealed, List<Line> lines)
        {
            var sheet = KnownBallots.Read(s, week);
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
                // An ally's claim is their own word to the pact at a meeting (ACTIONS-DEALS-ALLIANCES-PLAN C6).
                string said = claim.source == ClaimSource.Told
                        ? Who(s, claim.voterId) + (you ? " told you they would vote you out" : " told you: evict " + Whom(s, claim.targetId))
                    : claim.source == ClaimSource.Overheard ? "Overheard: " + Who(s, claim.voterId) + voting
                    : Who(s, claim.voterId) + (you ? " told the pact they would vote you out" : " told the pact: evict " + Whom(s, claim.targetId));
                var line = new Line { kind = Kinds.Claim, aboutId = claim.voterId };
                var known = sheet.Of(claim.voterId);
                if (known != null && known.Certain && known.targetId == claim.targetId) { line.verdict = Verdicts.Right; line.text = said + ", and voted that way."; }
                else if (known != null && known.Certain)
                {
                    line.verdict = Verdicts.Wrong;
                    line.text = said + ", and voted to evict " + (known.targetId == s.playerId ? "you" : Whom(s, known.targetId)) + ".";
                }
                else
                {
                    line.verdict = Verdicts.NotKnown;
                    line.text = said + (!revealed ? ". The vote is still to come."
                        : claim.status == ClaimStatus.Open ? ". Their vote never came." : ". How they voted is not known.");
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
                // A canonical Safety outcome is CanonicalWord's line wherever Safety is canonical (vote family V5e).
                if (UnifiedCommitments.SafetyAuthorityOn(s) && ReadDeal(s, record.description, partner, out _, out string canonicalType, out _)
                    && canonicalType == DealKind.SafetyAgreement && HasCanonicalOutcome(s, week, partner, kept)) continue;
                var line = new Line { kind = Kinds.Deal, verdict = kept ? Verdicts.Kept : Verdicts.Broken, aboutId = partner };
                if (ReadDeal(s, record.description, partner, out string actorId, out string type, out bool said) && said == kept)
                {
                    string what = DealWords(type);
                    // A vote deal the other party settled by their ballot - or that the two of them
                    // settled together - is told once the player knows that ballot (decision 4). So
                    // is a partnership, which under the commitment rules only the vote settles (C1).
                    if (KnownBallots.IsBallotKind(type) && !KnownBallots.Knows(s, week, partner)
                        && (actorId != s.playerId || KeepingTellsTheirBallot(s, week, partner, type, kept)))
                    {
                        line.verdict = Verdicts.NotKnown;
                        line.text = "The " + what + " with " + Whom(s, partner) + " is " + KnownBallots.Unresolved + ".";
                        lines.Add(line);
                        continue;
                    }
                    line.byId = actorId;
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
            if (UnifiedVoteStore.On(s)) UntoldVoteDeals(s, week, lines);
        }

        /// <summary>
        /// Mode 2 only (the pre-V6 review; the lead's decision that what a ballot settled is the player's to know only where that
        /// ballot is): a vote deal of the player's whose ending they cannot know (<see cref="KnownBallots.DealOutcomeKnown"/>) is a
        /// not-known line of its own, as the record's line for it says. Under Rule2 a row that owned no consequence of its reveal
        /// wrote no record, and which row owns turns on how the others voted - the partner's word broken by the same hidden ballot
        /// owns their breach of the player where the deal broke with it, and not where it held - so each such row the record did not
        /// tell is told here, after the record's lines. Mode 1 writes every row's record, and never reaches this.
        /// </summary>
        private static void UntoldVoteDeals(EpisodeState s, int week, List<Line> lines)
        {
            var untold = CommitmentReferences.Deals(s).Where(d => KnownBallots.IsVoteDeal(d.type) && d.settledWeek == week
                    && (d.status == DealStatus.Fulfilled || d.status == DealStatus.Broken) && (d.proposerId == s.playerId) != (d.recipientId == s.playerId)
                    && CommitmentReferences.FindCanonical(s, d.id)?.kind == UnifiedVoteTogether.Vote && !KnownBallots.DealOutcomeKnown(s, d))
                .GroupBy(d => (partner: d.proposerId == s.playerId ? d.recipientId : d.proposerId, d.type)).ToList();
            foreach (var group in untold)
            {
                if (s.Find(group.Key.partner) == null) continue;
                string text = "The " + DealWords(group.Key.type) + " with " + Whom(s, group.Key.partner) + " is " + KnownBallots.Unresolved + ".";
                int told = lines.Count(line => line.kind == Kinds.Deal && line.verdict == Verdicts.NotKnown && line.aboutId == group.Key.partner && line.text == text);
                for (int i = told; i < group.Count(); i++)
                    lines.Add(new Line { kind = Kinds.Deal, verdict = Verdicts.NotKnown, aboutId = group.Key.partner, text = text });
            }
        }

        /// <summary>
        /// The knowledge gate for a deal the player is said to have kept, under mode 2 only (vote family V5e): a vote deal is kept
        /// only where the other party's ballot kept it too, so "you kept it" tells that ballot - unless the player can know how the
        /// deal ended (<see cref="KnownBallots.DealOutcomeKnown"/>: the other cast none, or the player knows it). Mode 1 tells it,
        /// as its recorded seasons did; closing it there needs a rule. A deal the player broke is theirs to know.
        /// </summary>
        private static bool KeepingTellsTheirBallot(EpisodeState s, int week, string partner, string type, bool kept) =>
            kept && UnifiedVoteStore.On(s) && !CommitmentReferences.Deals(s).Any(d => d.type == type && d.status == DealStatus.Fulfilled
                && d.settledWeek == week && ((d.proposerId == s.playerId && d.recipientId == partner) || (d.proposerId == partner && d.recipientId == s.playerId))
                && KnownBallots.DealOutcomeKnown(s, d));

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
            foreach (var spelling in DealKind.Titles(kind))
            {
                string title = spelling.ToLowerInvariant();
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
        /// week's outcome lines logged to them, and the same words in their own memory. Each outcome is
        /// written once to each, so whichever kept more of the week holds all of it - but the log keeps
        /// only the house's last 256 lines and the memory only the player's last 30, so a week both
        /// have rolled past has lost its promise lines, and a review of it shows fewer than it ended.
        ///
        /// <para>A memory is not always a party's. Every witness of a broken promise remembers it in
        /// the same words, the promisee's own shape, so a remembered line counts only as many times as
        /// the player was a party to a promise that could have ended so that week: its kind, ended as
        /// the line says, made by the one it names to the other of the pair, and binding that week.</para>
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
                if (UnifiedCommitments.SafetyAuthorityOn(s) && ReadPromise(s, outcome.text, outcome.partner, out _, out PromiseKind canonicalKind, out bool canonicalKept)
                    && canonicalKind == PromiseKind.Safety && HasCanonicalOutcome(s, week, outcome.partner, canonicalKept)) continue;
                int heard = told.Count(x => x == outcome);
                int recalled = Math.Min(remembered.Count(x => x == outcome), PromisesItCouldBe(s, week, outcome.text, outcome.partner));
                for (int i = 0; i < Math.Max(heard, recalled); i++) lines.Add(PromiseLine(s, week, outcome.text, outcome.partner));
            }
        }

        /// <summary>
        /// How many promises the player was a party to that an outcome line could be: of its kind,
        /// ended as it says, made by the one it names to the other of the pair, and binding in
        /// <paramref name="week"/> - made by then, and not lapsed before it.
        /// </summary>
        private static int PromisesItCouldBe(EpisodeState s, int week, string text, string partnerId)
        {
            if (!ReadPromise(s, text, partnerId, out string fromId, out PromiseKind kind, out bool kept)) return 0;
            string toId = fromId == s.playerId ? partnerId : s.playerId;
            var ended = kept ? PromiseStatus.Fulfilled : PromiseStatus.Broken;
            return CommitmentReferences.Promises(s).Count(p => p.fromId == fromId && p.toId == toId && p.kind == kind && p.status == ended
                && p.week <= week && (p.expiresWeek == 0 || week <= p.expiresWeek));
        }

        private static bool HasCanonicalOutcome(EpisodeState s, int week, string partner, bool kept) =>
            UnifiedCommitmentHistory.Records(s).Any(row => row.settledWeek == week
                && row.status == (kept ? DealStatus.Fulfilled : DealStatus.Broken)
                && ((row.makerId == s.playerId && row.beneficiaryId == partner) || (row.makerId == partner && row.beneficiaryId == s.playerId)));

        /// <summary>
        /// The week's canonical Safety word, by incident and receipt, wherever Safety is canonical (vote family V5e). Mode 2's
        /// Vote outcomes are told as mode 1's are, by the lines their owners wrote (<see cref="Deals"/>, <see cref="Promises"/>).
        /// </summary>
        private static void CanonicalWord(EpisodeState s, int week, List<Line> lines)
        {
            if (!UnifiedCommitments.SafetyAuthorityOn(s)) return;
            var rows = UnifiedCommitmentHistory.Records(s).ToDictionary(row => row.id, StringComparer.Ordinal);
            foreach (var incident in UnifiedCommitmentHistory.Breaches(s).Where(incident =>
                rows[incident.EffectOwnerId].settledWeek == week && (incident.ActorId == s.playerId || incident.WrongedId == s.playerId)))
            {
                var owner = rows[incident.EffectOwnerId];
                bool promise = owner.sourcePolicy == UnifiedCommitments.PromisePolicy;
                string partner = incident.ActorId == s.playerId ? incident.WrongedId : incident.ActorId;
                string what = promise ? "promise of safety" : "safety deal";
                lines.Add(new Line { kind = promise ? Kinds.Promise : Kinds.Deal, verdict = Verdicts.Broken,
                    aboutId = partner, byId = incident.ActorId, effectKey = incident.EffectKey, evidenceIds = incident.EvidenceIds,
                    text = incident.ActorId == s.playerId ? "You broke the " + what + " with " + Whom(s, partner) + "."
                        : Who(s, partner) + " broke the " + what + " with you." });
            }
            foreach (var receipt in UnifiedCommitmentHistory.Fulfillments(s).Where(receipt => receipt.SettledWeek == week
                && (receipt.FirstId == s.playerId || receipt.SecondId == s.playerId)))
            {
                string partner = receipt.FirstId == s.playerId ? receipt.SecondId : receipt.FirstId;
                // Both sides kept it. The record stores no fulfillment actor; never invent one
                // from a current HoH or a historical power row that may no longer be retained.
                lines.Add(new Line { kind = Kinds.Deal, verdict = Verdicts.Kept, aboutId = partner,
                    evidenceIds = receipt.EvidenceIds, text = "You and " + Whom(s, partner) + " kept your safety deal." });
            }
        }

        private static Line PromiseLine(EpisodeState s, int week, string text, string partner)
        {
            var line = new Line { kind = Kinds.Promise, aboutId = partner };
            if (ReadPromise(s, text, partner, out string fromId, out PromiseKind kind, out bool kept))
            {
                string what = PromiseWords(kind);
                // Their vote promise ended by their ballot: told once the player knows it (decision 4).
                if (kind == PromiseKind.Vote && fromId != s.playerId && !KnownBallots.Knows(s, week, partner))
                {
                    line.verdict = Verdicts.NotKnown;
                    line.text = "Their " + what + " to you is " + KnownBallots.Unresolved + ".";
                    return line;
                }
                line.verdict = kept ? Verdicts.Kept : Verdicts.Broken;
                line.byId = fromId;
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

        /// <summary>
        /// Each call the player made this week: its own line, then each member who could vote. A
        /// member whose ballot the player knows (<see cref="KnownBallots"/>) is judged by it: followed
        /// if they voted out who was called, defected if not, whatever they said at the call. A member
        /// whose ballot the player cannot place stands as they said at the call. Each line says which
        /// it rests on. Never a ballot the reveal kept private. Under the war rooms (D3-S7) a call a
        /// pact's plan made is said as that plan, after the calls (<see cref="Plans"/>).
        /// </summary>
        private static void Calls(EpisodeState s, int week, PowerRow power, bool revealed, List<Line> lines)
        {
            var sheet = KnownBallots.Read(s, week);
            var plans = PactPlans.OfWeek(s, week);
            foreach (var call in s.ledger.calls.Where(c => c.week == week && c.callerId == s.playerId))
            {
                if (plans.Any(p => p.allianceId == call.allianceId)) continue;
                string pact = s.alliances.FirstOrDefault(a => a.id == call.allianceId)?.name ?? "your alliance";
                string target = Whom(s, call.targetId);
                string after = !revealed ? "."
                    : power.evicteeId == call.targetId ? ", and " + target + " went home."
                    : ", and " + target + " stayed.";
                lines.Add(new Line { kind = Kinds.Call, text = "You called it in " + pact + ": evict " + target + after });
                foreach (var id in call.followed.Concat(call.defected))
                {
                    bool said = call.followed.Contains(id);
                    string known = sheet.TargetOf(id);
                    var line = new Line { kind = Kinds.Member, aboutId = id };
                    if (known != null)
                    {
                        bool voted = known == call.targetId;
                        line.verdict = voted ? Verdicts.Followed : Verdicts.Defected;
                        line.basis = Bases.Ballot;
                        string ballot = voted ? "voted out " + target : "voted to evict " + Whom(s, known);
                        line.text = Who(s, id) + (said ? " was with you at the call, " + (voted ? "and " : "then ") + ballot + "."
                            : " was not with you at the call, " + (voted ? "then " + ballot + " anyway." : "and " + ballot + "."));
                    }
                    else
                    {
                        line.verdict = said ? Verdicts.Followed : Verdicts.Defected;
                        line.basis = Bases.Call;
                        line.text = Who(s, id) + (said ? " was with you at the call." : " was not with you at the call.");
                    }
                    lines.Add(line);
                }
            }
            Plans(s, plans, power, revealed, sheet, lines);
        }

        /// <summary>
        /// Under the war rooms (WAVE-D-NPC-PACTS-PLAN D3-S7): each plan of the week's, in the order the pacts
        /// met - its own line, as the player answered it (<see cref="PlanLine"/>), then each member of it who
        /// voted. A member whose ballot the player knows is judged by it: followed if they voted out the plan's
        /// target; defected if not, where they were with it; and where they were not with it, no verdict - they
        /// did as they said. A member whose ballot the player cannot place stands as the player was told
        /// (<see cref="PactPlans.ToldWith"/>): followed where they were with it; where they were not, not known -
        /// a dissenter is never flagged (§3.3), and one who went along on their own coin under a plan an NPC
        /// leads was never said. A plan still open or void has no members.
        /// </summary>
        private static void Plans(EpisodeState s, List<PactPlanRow> plans, PowerRow power, bool revealed, KnownBallots.Sheet sheet, List<Line> lines)
        {
            foreach (var plan in plans)
            {
                lines.Add(new Line { kind = Kinds.Plan, text = PlanLine(s, plan, power, revealed) });
                if (string.IsNullOrEmpty(plan.targetId)) continue;
                bool called = PactPlans.PlayerCalled(s, plan);
                var told = PactPlans.ToldWith(s, plan);
                string Out(string id) => id == s.playerId ? "voted to evict you" : "voted out " + Whom(s, id);
                foreach (string id in PactPlans.Voted(s, plan))
                {
                    bool with = told.Contains(id);
                    string stood = with ? " was with the plan" : called ? " was not with the plan" : " did not back the plan";
                    string known = sheet.TargetOf(id);
                    var line = new Line { kind = Kinds.Member, aboutId = id };
                    if (known != null)
                    {
                        bool voted = known == plan.targetId;
                        // Only one the player was told is with it can defect: one not with it who voted the
                        // other way did as they said, and carries no verdict (§3.3: a dissenter is never flagged).
                        line.verdict = voted ? Verdicts.Followed : with ? Verdicts.Defected : null;
                        line.basis = Bases.Ballot;
                        string ballot = voted ? Out(plan.targetId) : "voted to evict " + Whom(s, known);
                        line.text = Who(s, id) + stood + (with ? (voted ? ", and " : ", then ") + ballot + "." : voted ? ", then " + ballot + " anyway." : ", and " + ballot + ".");
                    }
                    else
                    {
                        line.verdict = with ? Verdicts.Followed : Verdicts.NotKnown;
                        line.basis = Bases.Call;
                        line.text = Who(s, id) + stood + ".";
                    }
                    lines.Add(line);
                }
            }
        }

        /// <summary>
        /// A plan's own line, in the words the player answered it: "You went with The War Room's plan: evict
        /// Maya Hassan, and Maya Hassan went home." / "The War Room went with your push: evict Alex Moore." /
        /// "You pushed for Alex Moore, but The War Room held to its plan: evict Maya Hassan." / "You lay low on
        /// The War Room's plan: evict Maya Hassan." / "You let The War Room's plan stand: evict Maya Hassan."
        /// (the campaign's close) / "The War Room's plan came to nothing." / "You have not answered The War
        /// Room's plan yet." The reveal's ending only once it is read.
        /// </summary>
        public static string PlanLine(EpisodeState s, PactPlanRow plan, PowerRow power, bool revealed)
        {
            if (s == null || plan == null) return string.Empty;
            string pact = s.alliances.FirstOrDefault(a => a.id == plan.allianceId)?.name;
            if (string.IsNullOrEmpty(pact)) pact = "your alliance";
            string Pact() => char.ToUpperInvariant(pact[0]) + pact.Substring(1);
            if (plan.stance == PactPlanStance.Open) return "You have not answered " + pact + "'s plan yet.";
            if (string.IsNullOrEmpty(plan.targetId)) return Pact() + "'s plan came to nothing.";
            string target = Whom(s, plan.targetId);
            string evict = "evict " + target + (!revealed || power == null ? "."
                : power.evicteeId == plan.targetId ? ", and " + target + " went home." : ", and " + target + " stayed.");
            switch (plan.stance)
            {
                case PactPlanStance.Agreed: return "You went with " + pact + "'s plan: " + evict;
                case PactPlanStance.Countered:
                    return PactPlans.PlayerCalled(s, plan) ? Pact() + " went with your push: " + evict
                        : "You pushed for " + Whom(s, plan.counterId) + ", but " + pact + " held to its plan: " + evict;
                case PactPlanStance.Low: return "You lay low on " + pact + "'s plan: " + evict;
                default: return "You let " + pact + "'s plan stand: " + evict;
            }
        }

        // ---------------------------------------------------------------- who found out (D4-6)

        /// <summary>
        /// Who found out about another of the player's pacts that week, under the leak rules
        /// (WAVE-D-NPC-PACTS-PLAN D4-6): each double-dealing line the player read, in its own words and in
        /// the order they read them, about the one who found out. No verdict: the line says what it did.
        /// The log keeps the house's last 256 lines, so a long-past week may show fewer.
        /// </summary>
        private static void Exposures(EpisodeState s, int week, List<Line> lines)
        {
            foreach (var e in (s.events ?? new List<EpisodeEvent>()).Where(e => AllianceLeaks.IsDoubleDealingLine(e) && e.week == week
                         && e.text != null && e.audienceIds != null && e.audienceIds.Contains(s.playerId)))
                lines.Add(new Line { kind = Kinds.FoundOut, text = e.text, aboutId = e.audienceIds.FirstOrDefault(id => id != s.playerId) });
        }

        // ---------------------------------------------------------------- Game Sense so far

        /// <summary>
        /// Game Sense through the week in the parts the player can see: the strategy face from the
        /// known notes, the chances taken, and the week's own known notes, a competition thrown or
        /// fought from the block among them. The number itself, the competitions and social faces, and
        /// another Head of Household's plan wait for the season's end, so the face shown can still move
        /// at the finale by that plan's row; the recap's subtitle says so.
        /// </summary>
        private static Sense SenseSoFar(EpisodeState s, int week)
        {
            var sense = new Sense();
            // The weeks after the player's season ended are the house's, not theirs: the verdict at the
            // end still writes the power of each ("you stayed off the block"), but so far holds where it
            // stood the week they left, and no week after it has rows of theirs.
            int ended = SeasonEnded(s);
            bool gone = ended > 0 && week > ended;
            int through = gone ? ended : week;
            // A play still running has been neither taken up nor let pass yet: the verdict, which reads
            // a season that is over, counts an unanswered one as let pass, which it is not until it closes.
            var running = new HashSet<string>((s.storylines ?? new List<StorylineState>())
                .Where(cycle => StorylineStatus.Running(cycle.status)).Select(cycle => cycle.id), StringComparer.Ordinal);
            var known = GameSense.Evaluate(s).notes
                .Where(n => n.known && n.week <= through && !(n.rowKind == "opportunity" && n.rowId != null && running.Contains(n.rowId))).ToList();
            sense.strategy = GameSense.Face(known, GameSense.Strategy);
            var chances = s.ledger.opportunities.Where(o => o.week <= through).ToList();
            sense.offered = chances.Count;
            sense.taken = chances.Count(o => o.response == OpportunityResponse.Taken);
            sense.endedWeek = gone ? ended : 0;
            // The social face's notes carry the week a pact or bond began, not a week's event.
            if (!gone) sense.rows = known.Where(n => n.week == week && n.face != GameSense.Social).Take(SenseRowLimit).ToList();
            return sense;
        }

        /// <summary>The week the player's season ended - the week they were evicted, or production removed them - or 0 while they are still in it.</summary>
        public static int SeasonEnded(EpisodeState s)
        {
            var you = s?.Find(s.playerId);
            if (you == null || you.status == ContestantStatus.Active || you.status == ContestantStatus.Winner || you.status == ContestantStatus.RunnerUp) return 0;
            int evicted = s.ledger?.power?.Where(p => p.evicteeId == s.playerId).Select(p => p.week).DefaultIfEmpty(0).Min() ?? 0;
            if (evicted > 0) return evicted;
            return s.story?.removals?.FirstOrDefault(r => r.contestantId == s.playerId)?.week ?? 0;
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
                case DealKind.VoteTogether: return "voting bloc";
                case DealKind.VoteSave: return "vote-to-save deal";
                case DealKind.VoteEvict: return "vote-to-evict deal";
                case DealKind.VetoUse: return "veto commitment";
                case DealKind.InformationSharing: return "information deal";
                case DealKind.FinalTwo: return "final two deal";
                case DealKind.AllianceInvite: return "alliance invitation";
                case DealKind.FinalThree: return "final three deal";
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
