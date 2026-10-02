using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The ballots of an eviction as the player knows them (UI-UX-PASS-PLAN B0; decisions 1 to 4).
    ///
    /// <para>The reveal reads the count, not the ballots. What the player knows of a week's vote is
    /// what they can source: their own ballot; the Head of Household's tie-break, which the format
    /// reads live; a ballot read in the open (a save from before ballots went private kept its public
    /// lines, and the house is told when a loyalty oath breaks); what the count proves - every ballot
    /// when the vote is unanimous, the one ballot when it is the only one, and the last ballot once
    /// every other is placed; what a voter told them, judged at the reveal; and what they overheard
    /// or an ally reported, judged the same way. Everything else is an unknown slot: the sheet says
    /// how many ballots it cannot place, and never whose.</para>
    ///
    /// <para><b>Every reader of a ballot reads this.</b> The notebook's Known ballots, the recap's
    /// vote breakdown, Your week, the houseguest notes, the finalist read, the jury house, the
    /// finale's receipts, the walk out and the deal, promise and oath verdicts the player is told
    /// (<see cref="DealOutcomeKnown"/>, <see cref="PromiseOutcomeKnown"/>) all go through here, so
    /// one rule says what a ballot looks like to the player. Nothing here reads
    /// <see cref="EpisodeState.votes"/> for anybody but the player except in the week the box is
    /// still the record, and then only to count it.</para>
    ///
    /// <para><b>The basis words.</b> <see cref="Basis.Own"/>: the player's own ballot, from the
    /// ledger's row or the live box. <see cref="Basis.TieBreak"/>: the Head of Household's deciding
    /// vote, public by the format (decision 1). <see cref="Basis.Revealed"/>: a line the house heard
    /// read - a legacy public <c>vote-reveal</c> line, or an oath breach announced to the house.
    /// <see cref="Basis.Proven"/>: proven by the count (decision 3). <see cref="Basis.Told"/>: what
    /// the voter said to the player's face, with the reveal's verdict; a lie caught names the ballot
    /// the voter actually cast. <see cref="Basis.Overheard"/> and <see cref="Basis.Reported"/>: what
    /// the player overheard or an ally reported, judged the same way. <see cref="Basis.Unknown"/>: a
    /// slot. A claim's verdict is the engine's own judgement at the reveal (the read's settlement,
    /// EpisodeEngine.SettleVoteRead); wave B moves that judgement behind the knowledge store (R3).
    /// </para>
    ///
    /// <para><b>The count's proof, for anyone's eyes.</b> <see cref="ProvenFor"/> applies the same
    /// rule from another houseguest's seat - their own ballot, the public tie-break and the count -
    /// so a later slice can give NPCs the same knowledge the player has. Nothing is rolled, written
    /// or saved here.</para>
    /// </summary>
    public static class KnownBallots
    {
        /// <summary>The line a verdict reads while the ballot it rests on is not the player's to know (decision 4).</summary>
        public const string Unresolved = "unresolved: you do not know how they voted";

        /// <summary>Where a known ballot comes from.</summary>
        public static class Basis
        {
            public const string Own = "own", TieBreak = "tie-break", Revealed = "revealed", Proven = "proven",
                Told = "told", Overheard = "overheard", Reported = "reported", Unknown = "unknown";

            /// <summary>Every basis, surest first: the order one source outranks another for the same voter.</summary>
            public static readonly string[] All = { Own, TieBreak, Revealed, Told, Overheard, Reported, Proven, Unknown };

            public static bool IsKnown(string basis) => basis != null && Array.IndexOf(All, basis) >= 0;

            /// <summary>The basis in the notebook's words: the tag beside a known ballot.</summary>
            public static string Word(string basis)
            {
                switch (basis)
                {
                    case Own: return "your ballot";
                    case TieBreak: return "the Head of Household's tie-break";
                    case Revealed: return "read in the open";
                    case Proven: return "proven by the count";
                    case Told: return "told you";
                    case Overheard: return "overheard";
                    case Reported: return "an ally's account";
                    default: return "unknown";
                }
            }

            internal static int Rank(string basis)
            {
                int at = Array.IndexOf(All, basis);
                return at < 0 ? All.Length : at;
            }
        }

        /// <summary>One voter's ballot as the player knows it, or a slot for one they do not.</summary>
        public sealed class Ballot
        {
            /// <summary>Who cast it; null on an unknown slot, which names nobody.</summary>
            public string voterId;
            /// <summary>The nominee the ballot went against, as the player knows it; null where they do not know.</summary>
            public string targetId;
            /// <summary>One of <see cref="Basis"/>.</summary>
            public string basis = Basis.Unknown;
            /// <summary>What the voter said, or was heard saying, where the basis is a claim; null otherwise.</summary>
            public string saidId;
            /// <summary>The reveal's verdict on a claim (<see cref="ClaimStatus"/>); null where the basis is not a claim.</summary>
            public string verdict;
            /// <summary>The public reason the ballot gave, where a line read in the open carried one; null otherwise.</summary>
            public string reason;

            /// <summary>Whether the player can place this ballot at all.</summary>
            public bool Known => targetId != null;

            /// <summary>Whether it is placed beyond doubt: everything known here is, since an unjudged claim never makes a ballot.</summary>
            public bool Certain => Known && basis != Basis.Unknown && verdict != ClaimStatus.Open;

            /// <summary>A claim the reveal caught out: the voter said one name and cast the other.</summary>
            public bool Lied => verdict == ClaimStatus.Lied;
        }

        /// <summary>A week's vote as the player knows it.</summary>
        public sealed class Sheet
        {
            public int week;
            /// <summary>The vote is under way and not revealed: the player's own ballot is the only one here.</summary>
            public bool pending;
            /// <summary>The final eviction: the last Head of Household's choice, no ballots.</summary>
            public bool final;
            /// <summary>The block, as the count paired it.</summary>
            public List<string> nominees = new List<string>();
            public string evictedId, hohId;
            /// <summary>The house's votes per nominee, the tie-break excluded as the engine counts them; empty before the reveal.</summary>
            public List<int> tally = new List<int>();
            public bool tieBroken;
            /// <summary>The house's voters as far as the record says, the Head of Household never among them.</summary>
            public List<string> voters = new List<string>();
            /// <summary>Known ballots first - the player's own, the tie-break, then the rest in the cast's order - and then an unknown slot for each the player cannot place.</summary>
            public List<Ballot> ballots = new List<Ballot>();

            /// <summary>Whether the count has been read: a tally is on the record.</summary>
            public bool Revealed => tally.Count == 2;
            public int Known => ballots.Count(b => b.Known);
            public int Unknown => ballots.Count(b => !b.Known);

            /// <summary>The ballot of <paramref name="voterId"/> as the player knows it, or null where they know nothing of it.</summary>
            public Ballot Of(string voterId) => voterId == null ? null : ballots.FirstOrDefault(b => b.voterId == voterId && b.Known);

            public bool Knows(string voterId) => Of(voterId)?.Certain == true;

            /// <summary>Who <paramref name="voterId"/> voted to evict, where the player knows; null otherwise.</summary>
            public string TargetOf(string voterId) => Knows(voterId) ? Of(voterId).targetId : null;

            /// <summary>The house's votes against <paramref name="nomineeId"/>, or 0 off the block or before the reveal.</summary>
            public int Against(string nomineeId)
            {
                int at = nominees.IndexOf(nomineeId ?? "");
                return at >= 0 && at < tally.Count ? tally[at] : 0;
            }
        }

        // ---------------------------------------------------------------- the sheet

        /// <summary>The week's vote as the player knows it: see the class summary.</summary>
        public static Sheet Read(EpisodeState s, int week)
        {
            var sheet = new Sheet { week = week };
            if (s == null || week < 1 || string.IsNullOrEmpty(s.playerId) || s.Find(s.playerId) == null) return sheet;
            var frame = Frame.Of(s, week);
            sheet.final = frame.final;
            sheet.pending = frame.pending;
            sheet.nominees = new List<string>(frame.nominees);
            sheet.evictedId = frame.evictedId;
            sheet.hohId = frame.hohId;
            sheet.tally = new List<int>(frame.tally);
            sheet.tieBroken = frame.tieBroken;
            sheet.voters = new List<string>(frame.voters);
            if (frame.final) return sheet;

            var known = new Dictionary<string, Ballot>(StringComparer.Ordinal);
            void Place(Ballot b)
            {
                if (b == null || string.IsNullOrEmpty(b.voterId) || string.IsNullOrEmpty(b.targetId)) return;
                if (!known.TryGetValue(b.voterId, out var have) || Basis.Rank(b.basis) < Basis.Rank(have.basis)) known[b.voterId] = b;
            }

            // The player's own ballot: the ledger's row outlives the box; the box is the live week's record.
            string own = frame.live ? frame.box.FirstOrDefault(v => v.voterId == s.playerId)?.targetId
                : s.ledger?.ballots?.LastOrDefault(b => b.week == week && b.voterId == s.playerId)?.targetId;
            if (own != null)
            {
                bool deciding = s.playerId == frame.hohId;
                Place(new Ballot { voterId = s.playerId, targetId = own, basis = deciding ? Basis.TieBreak : Basis.Own });
            }
            if (frame.pending)
            {
                sheet.ballots.AddRange(known.Values);
                return sheet;
            }

            // The Head of Household's deciding vote is read live: public (decision 1).
            if (frame.tieBroken && frame.hohId != null && frame.evictedId != null)
                Place(new Ballot { voterId = frame.hohId, targetId = frame.evictedId, basis = Basis.TieBreak });

            // Lines the house heard read.
            foreach (var line in PublicLines(s, week))
            {
                var read = ReadRevealLine(s, line.text, frame.hohId) ?? ReadOathLine(s, line);
                if (read == null) continue;
                if (read.voterId == s.playerId) read.basis = s.playerId == frame.hohId ? Basis.TieBreak : Basis.Own;
                else if (read.voterId == frame.hohId) read.basis = Basis.TieBreak;
                Place(read);
            }
            // An oath's breach by a vote against the player was announced to the house and is on the
            // player's own arc with the breaker in the rule's words: known from the announcement,
            // and still known once the log has rolled past the line.
            foreach (var breach in OathBreaches(s, week))
                if (breach.voterId != frame.hohId) Place(breach);

            // What the player was told, overheard or had reported, judged at the reveal.
            foreach (var claim in (s.ledger?.claims ?? new List<ClaimRow>()).Where(k => k != null && k.week == week && k.status != ClaimStatus.Open))
            {
                if (claim.voterId == null || claim.targetId == null || claim.voterId == frame.hohId) continue;
                string target = claim.status == ClaimStatus.Kept ? claim.targetId
                    : frame.nominees.Count == 2 && frame.nominees.Contains(claim.targetId) ? frame.nominees.First(id => id != claim.targetId) : null;
                if (target == null) continue;
                Place(new Ballot { voterId = claim.voterId, targetId = target, basis = BasisOf(claim.source), saidId = claim.targetId, verdict = claim.status });
            }

            // The count's proof, from what is certain (decision 3).
            foreach (var pair in Prove(frame, known.Values.Where(b => b.Certain).Select(b => (b.voterId, b.targetId))))
                if (!known.ContainsKey(pair.Key)) known[pair.Key] = new Ballot { voterId = pair.Key, targetId = pair.Value, basis = Basis.Proven };

            // Known first: the player's own, the tie-break, then the cast's order; then the slots.
            var order = new List<string>();
            if (known.ContainsKey(s.playerId)) order.Add(s.playerId);
            if (frame.hohId != null && known.ContainsKey(frame.hohId) && !order.Contains(frame.hohId)) order.Add(frame.hohId);
            foreach (var actor in s.contestants) if (known.ContainsKey(actor.id) && !order.Contains(actor.id)) order.Add(actor.id);
            foreach (var id in known.Keys) if (!order.Contains(id)) order.Add(id);
            foreach (var id in order) sheet.ballots.Add(known[id]);

            int houseKnown = known.Values.Count(b => b.voterId != frame.hohId);
            int cast = frame.exact ? frame.voters.Count : frame.tally.Sum();
            for (int slot = houseKnown; slot < cast; slot++) sheet.ballots.Add(new Ballot());
            return sheet;
        }

        /// <summary>Whether the player can place <paramref name="voterId"/>'s ballot in <paramref name="week"/> beyond doubt.</summary>
        public static bool Knows(EpisodeState s, int week, string voterId) => Read(s, week).Knows(voterId);

        /// <summary>Who <paramref name="voterId"/> voted to evict in <paramref name="week"/>, where the player knows; null otherwise.</summary>
        public static string TargetOf(EpisodeState s, int week, string voterId) => Read(s, week).TargetOf(voterId);

        /// <summary>
        /// The eviction weeks on the record, oldest first: every week with a count in the ledger,
        /// the week whose box is still the record, and any week a claim was judged in.
        /// </summary>
        public static List<int> Weeks(EpisodeState s)
        {
            var weeks = new SortedSet<int>();
            if (s == null) return weeks.ToList();
            foreach (var p in s.ledger?.power ?? new List<PowerRow>())
                if (p != null && p.tally != null && p.tally.Count == 2 && p.evicteeId != null) weeks.Add(p.week);
            foreach (var k in s.ledger?.claims ?? new List<ClaimRow>())
                if (k != null && k.status != ClaimStatus.Open) weeks.Add(k.week);
            if (Frame.LiveBox(s)) weeks.Add(s.week);
            return weeks.ToList();
        }

        // ---------------------------------------------------------------- the count, for anyone's eyes

        /// <summary>
        /// The ballots of <paramref name="week"/> that <paramref name="viewerId"/> can place from
        /// their own seat, by voter: their own ballot, the Head of Household's tie-break, and what
        /// the count proves on top of those - the same rule the player's sheet uses (decision 3),
        /// with nothing anybody told them. Empty before the reveal. A later slice feeds this with what
        /// a houseguest was told; nothing here is their reaction, only what they could know.
        /// </summary>
        public static Dictionary<string, string> ProvenFor(EpisodeState s, int week, string viewerId)
        {
            var placed = new Dictionary<string, string>(StringComparer.Ordinal);
            if (s == null || week < 1 || string.IsNullOrEmpty(viewerId) || s.Find(viewerId) == null) return placed;
            var frame = Frame.Of(s, week);
            if (frame.final || frame.pending || !frame.revealed) return placed;

            string own = viewerId == s.playerId
                ? (frame.live ? frame.box.FirstOrDefault(v => v.voterId == viewerId)?.targetId
                    : s.ledger?.ballots?.LastOrDefault(b => b.week == week && b.voterId == viewerId)?.targetId)
                : frame.live ? frame.box.FirstOrDefault(v => v.voterId == viewerId)?.targetId : null;
            if (own != null) placed[viewerId] = own;
            if (frame.tieBroken && frame.hohId != null && frame.evictedId != null) placed[frame.hohId] = frame.evictedId;
            foreach (var pair in Prove(frame, placed.Where(p => p.Key != frame.hohId).Select(p => (p.Key, p.Value)).ToList()))
                if (!placed.ContainsKey(pair.Key)) placed[pair.Key] = pair.Value;
            return placed;
        }

        /// <summary>
        /// What the count proves beyond the ballots already placed: with the placed ballots taken
        /// out of the tally, a nominee left with no votes means every remaining voter went the other
        /// way - a unanimous vote, a sole vote, or the last ballot once every other is placed. The
        /// last of those needs the voter list to be exact; a nominee with no votes at all needs only
        /// the voters the record names.
        /// </summary>
        private static Dictionary<string, string> Prove(Frame frame, IEnumerable<(string voterId, string targetId)> placed)
        {
            var proven = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!frame.revealed || frame.nominees.Count != 2 || frame.tally.Count != 2 || frame.tally.Sum() <= 0) return proven;
            var left = frame.tally.ToArray();
            var remaining = new List<string>(frame.voters);
            foreach (var (voterId, targetId) in placed)
            {
                if (voterId == frame.hohId || !remaining.Remove(voterId)) continue;
                int at = frame.nominees.IndexOf(targetId);
                if (at < 0) return proven;
                left[at]--;
            }
            if (left.Any(n => n < 0) || remaining.Count == 0) return proven;
            int whole = frame.tally[0] == 0 ? 1 : frame.tally[1] == 0 ? 0 : -1;
            int rest = left[0] == 0 ? 1 : left[1] == 0 ? 0 : -1;
            if (whole >= 0)
            {
                // Nobody voted the other way: every voter the record names went against this one.
                foreach (var id in remaining) proven[id] = frame.nominees[whole];
            }
            else if (rest >= 0 && frame.exact && remaining.Count == left[rest])
            {
                foreach (var id in remaining) proven[id] = frame.nominees[rest];
            }
            return proven;
        }

        // ---------------------------------------------------------------- the lines the house heard

        private const string Evicts = " voted to evict ", TieBreakPrefix = "HoH tie-break: ", OathBreach = " broke their loyalty oath to ",
            OathByVote = " by voting to evict them!";

        /// <summary>The engine's reason on the player's own ballot, which says nothing to them.</summary>
        public const string PlayerReason = "Player's decision";

        /// <summary>
        /// One ballot out of "Maya Hassan voted to evict Casey Wilson. Reason", as a save from before
        /// ballots went private holds them in the open: the voter and the target by the cast's names
        /// (longest first, so "Jamie Roberts" is not "Jamie"), "you" for the player as the engine
        /// writes it. The basis is <see cref="Basis.Revealed"/>, <see cref="Basis.TieBreak"/> for the
        /// Head of Household's. Null when the line does not read.
        /// </summary>
        public static Ballot ReadRevealLine(EpisodeState s, string text, string hohId = null)
        {
            if (s == null || string.IsNullOrEmpty(text)) return null;
            var voter = Subject(s, text);
            if (voter == null || !text.Substring(voter.name.Length).StartsWith(Evicts, StringComparison.Ordinal)) return null;
            string rest = text.Substring(voter.name.Length + Evicts.Length);
            var player = s.Find(s.playerId);
            ContestantState target;
            if (rest.StartsWith("you.", StringComparison.Ordinal) || rest.StartsWith("yourself.", StringComparison.Ordinal))
            {
                target = player;
                rest = rest.Substring(rest.IndexOf('.') + 1);
            }
            else
            {
                target = s.contestants.Where(c => !string.IsNullOrEmpty(c.name) && rest.StartsWith(c.name + ".", StringComparison.Ordinal))
                    .OrderByDescending(c => c.name.Length).FirstOrDefault();
                if (target == null) return null;
                rest = rest.Substring(target.name.Length + 1);
            }
            string reason = rest.Trim();
            bool tieBreak = reason.StartsWith(TieBreakPrefix, StringComparison.Ordinal) || (hohId != null && voter.id == hohId);
            if (reason.StartsWith(TieBreakPrefix, StringComparison.Ordinal)) reason = reason.Substring(TieBreakPrefix.Length).Trim();
            if (reason == PlayerReason || reason.Length == 0) reason = null;
            return new Ballot
            {
                voterId = voter.id, targetId = target?.id, reason = reason,
                basis = voter.id == s.playerId && !tieBreak ? Basis.Own : tieBreak ? Basis.TieBreak : Basis.Revealed,
            };
        }

        /// <summary>The house is told when a loyalty oath breaks: "X broke their loyalty oath to Y by voting to evict them!" names X's ballot.</summary>
        private static Ballot ReadOathLine(EpisodeState s, EpisodeEvent line)
        {
            if (line?.text == null || line.kind != "loyalty_oath_broken" || !line.text.EndsWith(OathByVote, StringComparison.Ordinal)) return null;
            var breaker = Subject(s, line.text);
            if (breaker == null) return null;
            string rest = line.text.Substring(breaker.name.Length);
            if (!rest.StartsWith(OathBreach, StringComparison.Ordinal)) return null;
            string victimName = rest.Substring(OathBreach.Length, rest.Length - OathBreach.Length - OathByVote.Length);
            var victim = s.contestants.FirstOrDefault(c => c.name == victimName);
            if (victim == null) return null;
            return new Ballot { voterId = breaker.id, targetId = victim.id, basis = Basis.Revealed };
        }

        /// <summary>The words an oath's breach by a vote against the player is recorded under on the player's arc with the breaker (<see cref="WebLoyaltyOaths"/>; <see cref="CommitmentsRead"/> reads the same entry).</summary>
        public const string OathBreachByVoteAgainstYou = "Broke loyalty oath by voting to evict you in week ";

        /// <summary>
        /// The ballots the player's own relationship arcs record: a houseguest who broke a loyalty
        /// oath by voting to evict the player in <paramref name="week"/>, in the words the oath's rule
        /// writes on the arc. The house was told of the breach in the open, so the basis is
        /// <see cref="Basis.Revealed"/>; the arc outlives the line on the log.
        /// </summary>
        private static IEnumerable<Ballot> OathBreaches(EpisodeState s, int week)
        {
            string reason = OathBreachByVoteAgainstYou + week;
            foreach (var arc in s.relationshipArcs ?? new List<RelationshipArcState>())
            {
                if (arc?.weeklyHistory == null || string.IsNullOrEmpty(arc.npcId) || arc.npcId == s.playerId || s.Find(arc.npcId) == null) continue;
                if (arc.weeklyHistory.Any(entry => entry != null && entry.week == week && entry.reason == reason))
                    yield return new Ballot { voterId = arc.npcId, targetId = s.playerId, basis = Basis.Revealed };
            }
        }

        private static ContestantState Subject(EpisodeState s, string line) =>
            s.contestants.Where(c => !string.IsNullOrEmpty(c.name) && line.StartsWith(c.name, StringComparison.Ordinal))
                .OrderByDescending(c => c.name.Length).FirstOrDefault();

        private static IEnumerable<EpisodeEvent> PublicLines(EpisodeState s, int week) =>
            s.events.Where(e => e != null && e.week == week && (e.kind == "vote-reveal" || e.kind == "loyalty_oath_broken")
                && (e.audienceIds == null || e.audienceIds.Count == 0));

        private static string BasisOf(string source) =>
            source == ClaimSource.Told ? Basis.Told : source == ClaimSource.Overheard ? Basis.Overheard : Basis.Reported;

        // ---------------------------------------------------------------- the verdicts the player is told (decision 4)

        /// <summary>A deal the vote settles: a voting block, a vote to save, a vote to evict.</summary>
        public static bool IsVoteDeal(string type) =>
            type == DealKind.VoteTogether || type == DealKind.VoteSave || type == DealKind.VoteEvict;

        /// <summary>
        /// A kind of deal whose ending a line of record can only have learned from a ballot: the vote
        /// deals, and a partnership, which under the commitment rules only the vote settles
        /// (ACTIONS-DEALS-ALLIANCES-PLAN C1) - and before them nothing settled at all, so no line
        /// tells one's ending.
        /// </summary>
        public static bool IsBallotKind(string type) => IsVoteDeal(type) || type == DealKind.Partnership;

        /// <summary>
        /// Whether this deal was settled by the vote: a vote deal, or a partnership settled under the
        /// commitment rules (C1), which the record says by its week. A partnership a season brought in
        /// already broken (a web import) was not.
        /// </summary>
        public static bool SettledByABallot(DealState d) =>
            d != null && (IsVoteDeal(d.type) || (d.type == DealKind.Partnership && d.settledWeek > 0));

        /// <summary>
        /// Whether the player can know how a vote deal of theirs ended - and, under the commitment
        /// rules, a partnership (C1), which the vote settles too. The engine settles it by the true
        /// ballots; the player is told only once they know the other party's - their own ballot
        /// settles it alone where it was theirs that broke it, or where theirs was the only one that
        /// counted. Any other deal, an unsettled one, or one between two other houseguests: yes.
        /// </summary>
        public static bool DealOutcomeKnown(EpisodeState s, DealState d)
        {
            if (s == null || d == null || !SettledByABallot(d)) return true;
            if (d.status != DealStatus.Fulfilled && d.status != DealStatus.Broken) return true;
            string player = s.playerId;
            if (d.proposerId != player && d.recipientId != player) return true;
            string partner = d.proposerId == player ? d.recipientId : d.proposerId;
            int week = DealSettledWeek(s, d, partner);
            if (week == 0) return false;
            var sheet = Read(s, week);
            // A partner who cast no ballot that week - the Head of Household with no tie to break,
            // a nominee - had nothing the player needs to learn: the player's own ballot settled it.
            if (!sheet.voters.Contains(partner) && !(sheet.tieBroken && sheet.hohId == partner)) return true;
            if (d.type != DealKind.VoteTogether && d.targetId != null)
            {
                string own = sheet.TargetOf(player);
                bool evictedTarget = own == d.targetId;
                // The player's own ballot broke it: known without the partner's.
                if (own != null && (d.type == DealKind.VoteEvict ? !evictedTarget : evictedTarget) && d.status == DealStatus.Broken) return true;
            }
            return sheet.Knows(partner);
        }

        /// <summary>
        /// Whether the player can know how a vote promise made to them ended: once they know the
        /// promiser's ballot in the week it was judged. Their own promises, other kinds, unsettled
        /// ones and promises between two other houseguests: yes.
        /// </summary>
        public static bool PromiseOutcomeKnown(EpisodeState s, PromiseState p)
        {
            if (s == null || p == null || p.kind != PromiseKind.Vote) return true;
            if (p.status != PromiseStatus.Fulfilled && p.status != PromiseStatus.Broken) return true;
            if (p.toId != s.playerId || p.fromId == s.playerId) return true;
            int week = PromiseSettledWeek(s, p);
            return week > 0 && Knows(s, week, p.fromId);
        }

        /// <summary>The week the reveal judged a vote promise: the first on the record, from the week it was made, in which the promiser voted - with the house, or as the Head of Household breaking a tie. 0 where the record cannot say.</summary>
        public static int PromiseSettledWeek(EpisodeState s, PromiseState p)
        {
            if (s == null || p == null) return 0;
            int last = p.expiresWeek >= p.week && p.expiresWeek > 0 ? p.expiresWeek : s.week;
            foreach (int week in Weeks(s))
            {
                if (week < p.week || week > last) continue;
                var frame = Frame.Of(s, week);
                if (frame.revealed && Voted(frame, p.fromId)) return week;
            }
            return 0;
        }

        /// <summary>The week the reveal judged a vote deal: the first on the record in its term in which a party voted on it - with the house, or as the Head of Household breaking a tie. 0 where the record cannot say. A partnership's is its record's (C1).</summary>
        public static int DealSettledWeek(EpisodeState s, DealState d, string partnerId)
        {
            if (s == null || d == null) return 0;
            if (d.type == DealKind.Partnership) return Math.Max(0, d.settledWeek);
            int last = d.expiresWeek >= d.week && d.expiresWeek > 0 ? d.expiresWeek : s.week;
            foreach (int week in Weeks(s))
            {
                if (week < d.week || week > last) continue;
                var frame = Frame.Of(s, week);
                if (!frame.revealed) continue;
                if (d.type == DealKind.VoteTogether)
                {
                    if (Voted(frame, d.proposerId) && Voted(frame, d.recipientId)) return week;
                }
                else if (d.targetId != null && frame.nominees.Contains(d.targetId)
                    && (Voted(frame, d.proposerId) || Voted(frame, d.recipientId))) return week;
            }
            return 0;
        }

        /// <summary>Whether <paramref name="id"/> cast a ballot in the week of <paramref name="frame"/>: among the house's voters, or the Head of Household breaking its tie, which the engine counts as a ballot when it judges a deal or a promise.</summary>
        private static bool Voted(Frame frame, string id) =>
            id != null && (frame.voters.Contains(id) || (frame.tieBroken && frame.hohId == id));

        /// <summary>
        /// Whether a line of record written to the player - a memory, or an entry on their own record
        /// with somebody - tells them a ballot they do not know: the engine's own sentence for a vote
        /// deal or a vote promise between them and <paramref name="partnerId"/>, settled in
        /// <paramref name="week"/>, while that partner's ballot is not theirs to know. A reader that
        /// prints such lines leaves these out.
        /// </summary>
        public static bool TellsAnUnknownBallot(EpisodeState s, string partnerId, string text, int week)
        {
            if (s == null || string.IsNullOrEmpty(partnerId) || string.IsNullOrEmpty(text) || partnerId == s.playerId) return false;
            var partner = s.Find(partnerId);
            var player = s.Find(s.playerId);
            if (partner == null || player == null) return false;
            string them = partner.name, you = player.name;
            bool tells = false;
            if (text == them + " broke a Vote promise." || text == them + " fulfilled a Vote promise.") tells = true;
            // A partnership's ending is a ballot too, under the commitment rules (C1); before them no
            // line ever told one.
            foreach (var kind in new[] { DealKind.VoteTogether, DealKind.VoteSave, DealKind.VoteEvict, DealKind.Partnership })
            foreach (var spelling in DealKind.Titles(kind))
            {
                string title = spelling.ToLowerInvariant();
                if (text == them + " honoured a " + title + " with " + you + "." || text == them + " broke a " + title + " with " + you + "."
                    || text == you + " and " + them + " held to their " + title + "." || text == them + " and " + you + " held to their " + title + "."
                    || text == you + " and " + them + " fell out over their " + title + "." || text == them + " and " + you + " fell out over their " + title + ".")
                    tells = true;
            }
            // A betrayal told by a ballot (ACTIONS-DEALS-ALLIANCES-PLAN C2): the ally's vote to evict the
            // player, a call of theirs the ally refused and then voted against, or a vote deal they broke
            // with them by it, on the player's record of them.
            if (Allegiance.TellsABallot(s, partnerId, text)) tells = true;
            if (!tells) return false;
            return !Knows(s, week, partnerId);
        }

        /// <summary>
        /// The player's own memories, in the order they were written, less any that tells them a
        /// ballot they do not know (<see cref="TellsAnUnknownBallot"/>). Every reader that prints the
        /// player's memories - the notebook's story and its notes, the diary room's confessional and
        /// its reflections, a houseguest's profile and their notes, the web's secrets - reads this
        /// list, so one rule gates them all.
        /// </summary>
        public static IEnumerable<MemoryState> PlayerMemories(EpisodeState s)
        {
            if (s?.memories == null || s.playerId == null) yield break;
            foreach (var memory in s.memories)
                if (memory != null && memory.ownerId == s.playerId && !TellsAnUnknownBallot(s, memory.subjectId, memory.text, memory.week))
                    yield return memory;
        }

        // ---------------------------------------------------------------- the week's frame

        /// <summary>A week's vote as the record holds it: the block, the count, the voters, and whether the box is still the record.</summary>
        private sealed class Frame
        {
            public bool final, pending, revealed, live, exact, tieBroken;
            public string hohId, evictedId;
            public List<string> nominees = new List<string>(), voters = new List<string>();
            public List<int> tally = new List<int>();
            public List<VoteState> box = new List<VoteState>();

            /// <summary>Whether the box holds the current week's eviction ballots: the eviction night, or the social week after it before the week turns. Never the jury's.</summary>
            public static bool LiveBox(EpisodeState s) =>
                s.votes != null && s.votes.Count > 0
                && (s.phase == EpisodePhase.Eviction || (s.phase == EpisodePhase.Social && s.evictionResolved));

            public static Frame Of(EpisodeState s, int week)
            {
                var frame = new Frame();
                var power = s.ledger?.power?.LastOrDefault(p => p != null && p.week == week);
                if (power != null && (power.tally == null || power.tally.Count == 0) && power.evicteeId != null && power.vetoHolderId == null)
                {
                    frame.final = true;
                    return frame;
                }
                frame.live = week == s.week && LiveBox(s);
                if (frame.live) frame.box = s.votes.Where(v => v != null && v.voterId != null).ToList();
                frame.hohId = power?.hohId ?? (week == s.week ? s.hohId : null);
                frame.nominees = power != null && power.nominees != null && power.nominees.Count > 0 ? new List<string>(power.nominees)
                    : week == s.week ? new List<string>(s.nominees ?? new List<string>()) : new List<string>();
                frame.evictedId = power?.evicteeId;
                bool counted = power != null && power.tally != null && power.tally.Count == 2 && frame.nominees.Count == 2;
                bool boxed = frame.live && s.evictionResolved && frame.nominees.Count == 2;
                if (!counted && !boxed)
                {
                    frame.pending = week == s.week && s.phase == EpisodePhase.Eviction && !s.evictionResolved;
                    return frame;
                }
                frame.revealed = true;
                frame.tally = counted ? new List<int>(power.tally)
                    : frame.nominees.Select(id => frame.box.Count(v => v.targetId == id && v.voterId != frame.hohId)).ToList();
                frame.tieBroken = frame.tally[0] == frame.tally[1];
                if (frame.evictedId == null && frame.live)
                    frame.evictedId = frame.tieBroken ? frame.box.FirstOrDefault(v => v.voterId == frame.hohId)?.targetId
                        : frame.tally[0] > frame.tally[1] ? frame.nominees[0] : frame.nominees[1];
                frame.voters = frame.live
                    ? frame.box.Where(v => v.voterId != frame.hohId).Select(v => v.voterId).Distinct().ToList()
                    : Reconstruct(s, week, frame);
                frame.exact = frame.live || frame.voters.Count == frame.tally.Sum();
                return frame;
            }

            /// <summary>
            /// The house's voters in a week whose box is gone: everybody in the house at that vote -
            /// still in it now, or gone in a later week by the record - less the Head of Household
            /// and the block. Where the count disagrees with this list the list is not trusted for
            /// the last ballot's proof (<see cref="exact"/>).
            /// </summary>
            private static List<string> Reconstruct(EpisodeState s, int week, Frame frame)
            {
                var voters = new List<string>();
                var rows = s.ledger?.power ?? new List<PowerRow>();
                var removals = s.story?.removals ?? new List<RemovalState>();
                foreach (var c in s.contestants)
                {
                    if (c == null || c.id == frame.hohId || frame.nominees.Contains(c.id)) continue;
                    bool inHouse = c.status == ContestantStatus.Active || c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp
                        || rows.Any(p => p != null && p.evicteeId == c.id && p.week > week)
                        || removals.Any(r => r != null && r.contestantId == c.id && r.week > week);
                    if (inHouse) voters.Add(c.id);
                }
                return voters;
            }
        }
    }
}
