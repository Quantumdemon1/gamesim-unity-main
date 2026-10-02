using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Every eviction ballot of a finished season, as the season report reads them out
    /// (UI-UX-PASS-PLAN J0; decision 8, "the tapes"): per eviction week, who went and by what
    /// count, then each voter beside the nominee they voted to evict, the Head of Household's
    /// tie-break marked, and a ballot marked where the voter said otherwise.
    ///
    /// <para><b>Sealed until the finale.</b> Through the season a ballot is the player's only as
    /// <see cref="KnownBallots"/> says: their own, the tie-break, the count's proof, what they were
    /// told. Once the jury has crowned a winner (<see cref="EpisodePhase.Finished"/>) every
    /// eviction ballot is public, so a season ends with who lied in the open. <see cref="Read"/> is
    /// that one exception and nothing more: before the finale it returns nothing, whoever asks, so
    /// the privacy sentinel's rule (BallotPrivacyTests) holds for every state before the finale, and
    /// at the finale for every reader but this one. The jury's ballots are not here: they are read
    /// live at the finale and stay named where they always were (decision 7).</para>
    ///
    /// <para><b>The record.</b> The engine keeps no table of ballots. Each ballot's line goes to its
    /// voter at the reveal ("Maya Hassan voted to evict Casey Wilson. …"; in the open on a save from
    /// before ballots went private), and the log keeps its last 256 lines, so a long season's early
    /// weeks may have rolled off it. Beside the lines the ledger keeps what places a ballot whatever
    /// the log has forgotten: the week's block and count (its power row), the player's own row, the
    /// claims the reveal judged - kept names the ballot, a lie the other nominee - and the Head of
    /// Household's deciding vote, which the format reads live; every one of those through
    /// <see cref="KnownBallots"/>, which reads them for the player. Then the count proves what it
    /// can over everything placed, by decision 3's rule. A ballot none of these place is counted,
    /// with its voter where the house's voters are certain, and never guessed. Nothing is rolled,
    /// written or saved here.</para>
    ///
    /// <para><b>Lies.</b> A ballot is marked where the ledger holds a claim about it that the reveal
    /// judged a lie (<see cref="ClaimStatus.Lied"/>) - told to the player's face, overheard, or an
    /// ally's account - in the claim's own words. A claim kept, or no claim, marks nothing.</para>
    /// </summary>
    public static class SeasonBallots
    {
        // ---------------------------------------------------------------- the words

        /// <summary>The section's heading on the season report.</summary>
        public const string Heading = "How the house voted";

        /// <summary>The line under the heading once the tapes are open.</summary>
        public const string Intro = "Every eviction ballot of the season, read out now that it is over. "
            + "A ballot marked LIED is a houseguest who said one name and voted the other.";

        /// <summary>Said after the intro when the record no longer holds every ballot of some week.</summary>
        public const string IntroIncomplete = "The record keeps the season's latest lines: a ballot it no longer holds is counted, never guessed.";

        /// <summary>The section before the finale: no ballot, no name.</summary>
        public const string SealedLine = "Sealed until the finale: every eviction ballot is read out once the season is over.";

        /// <summary>A finished season with no eviction on its record (a season built for a test, a save from before the ledger).</summary>
        public const string EmptyLine = "No eviction vote is on the season's record.";

        /// <summary>The marks a ballot can carry.</summary>
        public const string LieWord = "LIED", TieBreakWord = "TIE-BREAK", SoleVoteWord = "SOLE VOTE";

        /// <summary>Where a ballot on the tapes comes from. Every source is the ballot as cast; the word says only how the record still holds it.</summary>
        public static class Sources
        {
            /// <summary>The ballot's own line, logged at the reveal.</summary>
            public const string Line = "line";
            /// <summary>A line the house heard: a save from before ballots went private, or a loyalty oath's breach announced.</summary>
            public const string Revealed = "revealed";
            /// <summary>The player's own ballot row.</summary>
            public const string Own = "own";
            /// <summary>The Head of Household's deciding vote, read live by the format.</summary>
            public const string TieBreak = "tie-break";
            /// <summary>A claim the reveal judged: kept names the ballot, a lie the other nominee.</summary>
            public const string Claim = "claim";
            /// <summary>Proven by the count over everything placed.</summary>
            public const string Count = "count";
            /// <summary>The final Head of Household's choice, the season's last eviction.</summary>
            public const string FinalChoice = "final";
        }

        /// <summary>What a voter said their ballot would be, as the ledger holds it, with the reveal's verdict.</summary>
        public sealed class Said
        {
            /// <summary>The nominee they said they would evict.</summary>
            public string saidId;
            /// <summary>How the player had it: one of <see cref="ClaimSource"/> (told, overheard, an ally's account).</summary>
            public string source;
            /// <summary>The reveal judged it a lie: the ballot went the other way.</summary>
            public bool lied;
        }

        /// <summary>One ballot on the tapes.</summary>
        public sealed class Row
        {
            public string voterId, targetId;
            /// <summary>One of <see cref="Sources"/>.</summary>
            public string source;
            /// <summary>The Head of Household's deciding vote, outside the house's count.</summary>
            public bool tieBreak;
            /// <summary>The final Head of Household's choice: the season's last eviction, by one vote.</summary>
            public bool finalChoice;
            /// <summary>Every judged claim about this ballot, told first, then overheard, then an ally's account.</summary>
            public List<Said> said = new List<Said>();

            /// <summary>The claim the reveal caught out, the surest first; null where nothing they said was a lie.</summary>
            public Said Lie => said.FirstOrDefault(claim => claim.lied);
            public bool Lied => Lie != null;
        }

        /// <summary>One eviction on the tapes.</summary>
        public sealed class Week
        {
            public int week;
            /// <summary>The final eviction: the last Head of Household's choice, no house vote.</summary>
            public bool final;
            public bool tieBroken;
            public string hohId, evictedId;
            /// <summary>The block as the count paired it; the two finalists the last Head of Household chose between on the final week.</summary>
            public List<string> nominees = new List<string>();
            /// <summary>The house's votes per nominee, the tie-break excluded as the engine counts them; empty on the final week.</summary>
            public List<int> tally = new List<int>();
            /// <summary>Every ballot the record places: the house's in the cast's order, the tie-break last.</summary>
            public List<Row> rows = new List<Row>();
            /// <summary>Per nominee, the house's ballots against them that the record no longer places.</summary>
            public List<int> unplacedAgainst = new List<int>();
            /// <summary>Whose ballots those are, in the cast's order, where the house's voters that week are certain; empty otherwise.</summary>
            public List<string> unplaced = new List<string>();

            /// <summary>The house's ballots the record no longer places.</summary>
            public int Missing => unplacedAgainst.Sum();
            /// <summary>Every ballot of the week is on the tapes.</summary>
            public bool Complete => Missing == 0;

            /// <summary>The house's votes against <paramref name="nomineeId"/>.</summary>
            public int Against(string nomineeId)
            {
                int at = nominees.IndexOf(nomineeId ?? "");
                return at >= 0 && at < tally.Count ? tally[at] : 0;
            }

            /// <summary>The house's ballots against <paramref name="nomineeId"/> the record no longer places.</summary>
            public int MissingAgainst(string nomineeId)
            {
                int at = nominees.IndexOf(nomineeId ?? "");
                return at >= 0 && at < unplacedAgainst.Count ? unplacedAgainst[at] : 0;
            }

            /// <summary>The house's ballots against <paramref name="nomineeId"/>, the tie-break apart.</summary>
            public IEnumerable<Row> Side(string nomineeId) => rows.Where(row => !row.tieBreak && !row.finalChoice && row.targetId == nomineeId);

            /// <summary>The block, the one who went first: the table's two sides.</summary>
            public IEnumerable<string> Sides => final ? Enumerable.Empty<string>()
                : nominees.OrderBy(id => id == evictedId ? 0 : 1);

            public Row TieBreak => rows.FirstOrDefault(row => row.tieBreak);
            public Row FinalChoice => rows.FirstOrDefault(row => row.finalChoice);

            /// <summary>The finalist the last Head of Household took to the final two.</summary>
            public string KeptId => final ? nominees.FirstOrDefault(id => id != evictedId) : null;
        }

        // ---------------------------------------------------------------- the gate

        /// <summary>Whether the tapes are open: the season is over and its winner crowned. Before then nothing here names a ballot.</summary>
        public static bool Open(EpisodeState s) => s != null && s.phase == EpisodePhase.Finished;

        // ---------------------------------------------------------------- the tapes

        /// <summary>
        /// Every eviction on the record, oldest first, with each ballot the record places; empty
        /// before the finale (<see cref="Open"/>), and for a season with no player.
        /// </summary>
        public static List<Week> Read(EpisodeState s)
        {
            var weeks = new List<Week>();
            if (!Open(s) || string.IsNullOrEmpty(s.playerId) || s.Find(s.playerId) == null) return weeks;
            var power = s.ledger?.power ?? new List<PowerRow>();
            foreach (int number in power.Where(p => p != null && p.week >= 1).Select(p => p.week).Distinct().OrderBy(n => n))
            {
                // The week's last row, as every reader of the ledger takes it.
                var row = power.Last(p => p != null && p.week == number);
                if (string.IsNullOrEmpty(row.evicteeId) || s.Find(row.evicteeId) == null) continue;
                bool final = (row.tally == null || row.tally.Count == 0) && row.vetoHolderId == null;
                if (final) weeks.Add(FinalWeek(s, row));
                else if (row.tally != null && row.tally.Count == 2 && row.nominees != null && row.nominees.Count == 2 && row.nominees.Contains(row.evicteeId))
                    weeks.Add(HouseWeek(s, row));
            }
            return weeks;
        }

        private static Week FinalWeek(EpisodeState s, PowerRow power)
        {
            var week = new Week
            {
                week = power.week, final = true, hohId = power.hohId, evictedId = power.evicteeId,
                nominees = new List<string>(power.nominees ?? new List<string>()),
            };
            if (!string.IsNullOrEmpty(power.hohId) && s.Find(power.hohId) != null)
                week.rows.Add(new Row { voterId = power.hohId, targetId = power.evicteeId, source = Sources.FinalChoice, finalChoice = true });
            return week;
        }

        private static Week HouseWeek(EpisodeState s, PowerRow power)
        {
            var week = new Week
            {
                week = power.week, hohId = power.hohId, evictedId = power.evicteeId,
                nominees = new List<string>(power.nominees), tally = new List<int>(power.tally),
            };
            week.tieBroken = week.tally[0] == week.tally[1];
            var placed = new Dictionary<string, Row>(StringComparer.Ordinal);
            void Place(string voterId, string targetId, string source)
            {
                if (string.IsNullOrEmpty(voterId) || placed.ContainsKey(voterId) || s.Find(voterId) == null || !week.nominees.Contains(targetId ?? "")) return;
                placed[voterId] = new Row { voterId = voterId, targetId = targetId, source = source, tieBreak = voterId == week.hohId };
            }

            // The ballots' own lines, while the log still holds them: to the voter since ballots
            // went private, in the open on an older save.
            foreach (var line in (s.events ?? new List<EpisodeEvent>()).Where(e => e != null && e.week == week.week && e.kind == "vote-reveal"))
            {
                var read = KnownBallots.ReadRevealLine(s, line.text, week.hohId);
                if (read != null) Place(read.voterId, read.targetId, line.audienceIds == null || line.audienceIds.Count == 0 ? Sources.Revealed : Sources.Line);
            }

            // What the ledger still proves whatever the log has forgotten, read the one way the
            // player's own knowledge is read: their row, the tie-break, the judged claims, a line
            // read in the open, an oath's announced breach, and the count over those.
            var sheet = KnownBallots.Read(s, week.week);
            foreach (var known in sheet.ballots.Where(b => b != null && b.Known && b.Certain))
                Place(known.voterId, known.targetId, SourceOf(known.basis));

            // The count over everything placed: a side left with no unplaced vote means every
            // unplaced voter went the other way - where those voters are exactly the votes left.
            var voters = sheet.voters.Where(id => id != week.hohId).Distinct().ToList();
            bool exact = voters.Count == week.tally.Sum();
            var left = new[] { week.tally[0], week.tally[1] };
            foreach (var row in placed.Values.Where(r => r.voterId != week.hohId))
                left[week.nominees.IndexOf(row.targetId)]--;
            var unplaced = voters.Where(id => !placed.ContainsKey(id)).ToList();
            if (left[0] >= 0 && left[1] >= 0 && unplaced.Count > 0)
            {
                int to = left[0] == 0 ? 1 : left[1] == 0 ? 0 : -1;
                if (to >= 0 && unplaced.Count == left[to])
                {
                    foreach (var id in unplaced) Place(id, week.nominees[to], Sources.Count);
                    left[to] = 0;
                    unplaced.Clear();
                }
            }
            week.unplacedAgainst = left.Select(n => Math.Max(0, n)).ToList();
            // A voter is named as unplaced only where the house's voters are certain: the record's
            // list of them matches the count, and what is left of it is what is left of the count.
            if (exact && unplaced.Count == week.Missing) week.unplaced = Ordered(s, unplaced);

            // What each voter said of their ballot, as the reveal judged it.
            var claims = (s.ledger?.claims ?? new List<ClaimRow>())
                .Where(k => k != null && k.week == week.week && k.voterId != null && k.targetId != null && k.status != ClaimStatus.Open).ToList();
            foreach (var row in placed.Values)
                row.said = claims.Where(k => k.voterId == row.voterId).OrderBy(k => SourceRank(k.source))
                    .Select(k => new Said { saidId = k.targetId, source = k.source, lied = k.status == ClaimStatus.Lied })
                    .GroupBy(said => said.source + "|" + said.saidId + "|" + said.lied).Select(group => group.First()).ToList();

            week.rows = placed.Values.OrderBy(row => row.tieBreak ? 1 : 0).ThenBy(row => CastIndex(s, row.voterId)).ToList();
            return week;
        }

        private static string SourceOf(string basis)
        {
            switch (basis)
            {
                case KnownBallots.Basis.Own: return Sources.Own;
                case KnownBallots.Basis.TieBreak: return Sources.TieBreak;
                case KnownBallots.Basis.Revealed: return Sources.Revealed;
                case KnownBallots.Basis.Proven: return Sources.Count;
                default: return Sources.Claim;
            }
        }

        private static int SourceRank(string source) =>
            source == ClaimSource.Told ? 0 : source == ClaimSource.Overheard ? 1 : source == ClaimSource.Ally ? 2 : 3;

        private static int CastIndex(EpisodeState s, string id)
        {
            int at = s.contestants.FindIndex(c => c != null && c.id == id);
            return at < 0 ? int.MaxValue : at;
        }

        private static List<string> Ordered(EpisodeState s, IEnumerable<string> ids) => ids.OrderBy(id => CastIndex(s, id)).ToList();

        // ---------------------------------------------------------------- the table's words

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? id ?? "";

        /// <summary>A houseguest in the object of a sentence: "you" for the player, their name otherwise.</summary>
        private static string Whom(EpisodeState s, string id) => id != null && id == s.playerId ? "you" : Name(s, id);

        /// <summary>"WEEK 3", with what made it different: a tie broken, or the final eviction.</summary>
        public static string Eyebrow(Week week) =>
            "WEEK " + week.week + (week.final ? " · FINAL EVICTION" : week.tieBroken ? " · TIE BROKEN" : "");

        /// <summary>Who went: "Maya Hassan evicted", or "You were evicted".</summary>
        public static string Title(EpisodeState s, Week week) =>
            week.evictedId == s.playerId ? "You were evicted" : Name(s, week.evictedId) + " evicted";

        /// <summary>
        /// The count in the house's words, the one who went first: "By a vote of 3 to 1.", "By a
        /// single vote.", "Tied 2 to 2; Maya Hassan broke the tie." - and the final eviction's
        /// "The final Head of Household's sole vote."
        /// </summary>
        public static string CountWords(EpisodeState s, Week week)
        {
            if (week.final) return "The final Head of Household's sole vote.";
            int against = week.Against(week.evictedId), others = week.tally.Sum() - against;
            if (against + others == 1) return "By a single vote.";
            if (week.tieBroken)
                return "Tied " + against + " to " + others + "; " + (week.hohId == s.playerId ? "you" : Name(s, week.hohId)) + " broke the tie.";
            return "By a vote of " + against + " to " + others + ".";
        }

        /// <summary>One side of the table: "TO EVICT MAYA (3)", the house's count against that nominee; "TO EVICT YOU (3)" for the player.</summary>
        public static string SideHeading(EpisodeState s, Week week, string nomineeId) =>
            "TO EVICT " + (nomineeId == s.playerId ? "YOU" : FinalistRead.FirstName(Name(s, nomineeId)).ToUpperInvariant())
            + " (" + week.Against(nomineeId) + ")";

        /// <summary>
        /// The line under a lie: what they said, in the claim's own source - "Told you they'd evict
        /// Maya Hassan", "Overheard saying they'd evict you", "An ally heard they'd evict Taylor
        /// Kim". Null where the ballot carries no lie.
        /// </summary>
        public static string LieWords(EpisodeState s, Row row)
        {
            var lie = row?.Lie;
            if (lie == null) return null;
            string evict = "they'd evict " + Whom(s, lie.saidId);
            switch (lie.source)
            {
                case ClaimSource.Told: return "Told you " + evict;
                case ClaimSource.Overheard: return "Overheard saying " + evict;
                case ClaimSource.Ally: return "An ally heard " + evict;
                default: return "Said " + evict;
            }
        }

        /// <summary>The line under the Head of Household's deciding vote: "Head of Household: broke the tie to evict Maya Hassan."</summary>
        public static string TieBreakWords(EpisodeState s, Week week) =>
            "Head of Household: broke the tie to evict " + Whom(s, week.evictedId) + ".";

        /// <summary>The line under the final eviction's one vote: "Final Head of Household: took Riley Johnson to the final two."</summary>
        public static string FinalWords(EpisodeState s, Week week) =>
            week.KeptId == null ? "Final Head of Household." : "Final Head of Household: took " + Whom(s, week.KeptId) + " to the final two.";

        /// <summary>A side's ballots the record no longer places, counted: "1 ballot not on the record."</summary>
        public static string MissingSideWords(int missing) =>
            missing == 1 ? "1 ballot not on the record." : missing + " ballots not on the record.";

        /// <summary>Whose ballots those are, where the record is sure: "Not on the record: Casey Wilson and Jamie Roberts." Null where it names nobody.</summary>
        public static string MissingWords(EpisodeState s, Week week)
        {
            if (week == null || week.unplaced.Count == 0) return null;
            var names = week.unplaced.Select(id => id == s.playerId ? "You" : Name(s, id)).ToList();
            string list = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
            return "Not on the record: " + list + ".";
        }
    }
}
