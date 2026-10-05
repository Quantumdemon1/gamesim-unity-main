using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The final case's résumé (MOCKUP-PASS M15, mockup 57): the player's season in four numbers
    /// and a few dated lines, read from the record and from the player's own rows only - the wins,
    /// the weeks on the block and in power, the moves that changed the house, the alliances they
    /// were in, the promises and deals broken either way, and the weeks that got them here. Pure
    /// and roll-free, like the argument it sits beside.
    ///
    /// <para>Never why an alliance ended (<see cref="AllianceRow.why"/>), and never the week a
    /// promise or a deal broke in the caption: legacy rows retain creation chronology, while
    /// canonical own-party incidents are ordered by their recorded settlement week. No date is
    /// invented from the power ledger or from somebody else's private agreements.</para>
    /// </summary>
    public sealed class FinalCaseResume
    {
        /// <summary>A line with its week.</summary>
        public sealed class Line
        {
            public int week;
            public string text;
        }

        /// <summary>How many lines of each list the résumé keeps; a longer season still fits its column.</summary>
        public const int MostMoves = 3, MostAlliances = 3, MostBetrayals = 3, MostKeyWeeks = 4;

        public int wins, nominationsSurvived, weeks, weeksInPower;
        /// <summary>Up to three power moves, oldest first: held the house, called the vote, used the veto, won, read the vote.</summary>
        public List<Line> majorMoves = new List<Line>();
        /// <summary>"The Core with Alex, Sam · weeks 2–5", longest first, and how many more there were.</summary>
        public List<string> alliances = new List<string>();
        public int moreAlliances;
        public int brokenByYou, brokenAgainstYou;
        /// <summary>Up to three betrayals, either way: legacy creation chronology or canonical actual settlement. Undated captions.</summary>
        public List<string> betrayals = new List<string>();
        /// <summary>Up to four weeks, oldest first, the last always the one the player reached this far.</summary>
        public List<Line> keyWeeks = new List<Line>();

        public static FinalCaseResume Read(EpisodeState s)
        {
            var resume = new FinalCaseResume();
            if (s == null) return resume;
            string player = s.playerId;
            var you = s.Find(player);
            var ledger = s.ledger ?? new SeasonLedger();
            resume.wins = you == null ? 0 : FinalistRead.Wins(s, you);
            resume.nominationsSurvived = you?.timesNominated ?? 0;
            resume.weeks = s.week;
            resume.weeksInPower = ledger.power.Count(p => p.hohId == player);

            // The moves: power first, then the latest of each, shown in the order they happened.
            var moments = FinalArgument.Moments(s);
            var moves = moments.Where(m => MoveRank(m.reference) >= 0)
                .OrderBy(m => MoveRank(m.reference)).ThenByDescending(m => m.week).Take(MostMoves)
                .OrderBy(m => m.week).ToList();
            foreach (var move in moves) resume.majorMoves.Add(new Line { week = move.week, text = FinalArgument.Title(move.reference) });

            // The alliances the player was in, with the weeks the record gives them.
            var pacts = new List<(int length, int started, string text)>();
            foreach (var alliance in s.alliances.Where(a => a.members.Contains(player)))
            {
                var others = alliance.members.Where(id => id != player).Select(id => FinalistRead.FirstName(s.Find(id)?.name)).Where(name => name.Length > 0).ToList();
                if (others.Count == 0) continue;
                string text = (string.IsNullOrEmpty(alliance.name) ? "Your alliance" : alliance.name) + " with " + string.Join(", ", others);
                var row = ledger.alliances.FirstOrDefault(r => r.id == alliance.id);
                int length = 0, started = int.MaxValue;
                if (row != null && row.startedWeek > 0)
                {
                    int ended = row.endedWeek > 0 ? row.endedWeek : s.week;
                    started = row.startedWeek;
                    length = ended - started;
                    text += ended > started ? " · weeks " + started + "–" + ended : " · week " + started;
                }
                pacts.Add((length, started, text));
            }
            resume.alliances = pacts.OrderByDescending(p => p.length).ThenBy(p => p.started).Take(MostAlliances).Select(p => p.text).ToList();
            resume.moreAlliances = pacts.Count - resume.alliances.Count;

            // Broken word both ways, counted as the argument's old résumé counted it.
            var broken = new List<(int week, string text)>();
            foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Broken))
            {
                if (promise.fromId == player) { resume.brokenByYou++; broken.Add((promise.week, "You broke your word to " + Name(s, promise.toId) + ".")); }
                else if (promise.toId == player) { resume.brokenAgainstYou++; broken.Add((promise.week, Name(s, promise.fromId) + " broke their word to you.")); }
            }
            foreach (var deal in s.deals)
            {
                string by = FinalistRead.DealBreaker(s, deal);
                if (by == null) continue;
                string other = deal.proposerId == player ? deal.recipientId : deal.proposerId;
                string title = DealKind.Title(deal.type).ToLowerInvariant();
                if (by == player) { resume.brokenByYou++; broken.Add((deal.week, "You broke your " + title + " with " + Name(s, other) + ".")); }
                else { resume.brokenAgainstYou++; broken.Add((deal.week, Name(s, other) + " broke your " + title + ".")); }
            }
            if (UnifiedCommitments.RulesOn(s))
            {
                var rows = UnifiedCommitmentHistory.Records(s).ToDictionary(row => row.id, StringComparer.Ordinal);
                foreach (var incident in UnifiedCommitmentHistory.Breaches(s)
                    .Where(incident => incident.ActorId == player || incident.WrongedId == player))
                {
                    // All agreement IDs remain evidence; only the actual actor/pair incident is
                    // another betrayal. Reuse its selected source procedure's existing caption.
                    var owner = rows[incident.EffectOwnerId];
                    bool promise = owner.sourcePolicy == UnifiedCommitments.PromisePolicy;
                    string title = DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant();
                    if (incident.ActorId == player)
                    {
                        resume.brokenByYou++;
                        broken.Add((owner.settledWeek, promise ? "You broke your word to " + Name(s, incident.WrongedId) + "."
                            : "You broke your " + title + " with " + Name(s, incident.WrongedId) + "."));
                    }
                    else
                    {
                        resume.brokenAgainstYou++;
                        broken.Add((owner.settledWeek, promise ? Name(s, incident.ActorId) + " broke their word to you."
                            : Name(s, incident.ActorId) + " broke your " + title + "."));
                    }
                }
            }
            resume.betrayals = broken.OrderByDescending(b => b.week).Take(MostBetrayals).Select(b => b.text).ToList();

            // The weeks between: the latest of the moments the moves did not already name, one a
            // week, and last the week the player reached this far.
            var named = new HashSet<string>(moves.Select(m => m.reference));
            var between = moments.Where(m => m.week > 0 && !named.Contains(m.reference)).GroupBy(m => m.week).Select(g => g.First())
                .OrderByDescending(m => m.week).Take(MostKeyWeeks - 1).OrderBy(m => m.week);
            foreach (var moment in between) resume.keyWeeks.Add(new Line { week = moment.week, text = FinalArgument.Title(moment.reference) });
            resume.keyWeeks.Add(new Line { week = s.week, text = "Reached the Final " + s.Active.Count() });
            return resume;
        }

        /// <summary>Which moments are moves, most telling first: holding the house, the call, the veto, the wins, the whip count.</summary>
        private static int MoveRank(string reference)
        {
            int colon = reference == null ? -1 : reference.IndexOf(':');
            switch (colon < 0 ? reference : reference.Substring(0, colon))
            {
                case "hoh": return 0;
                case "call": return 1;
                case "veto": return 2;
                case "win": return 3;
                case "whip": return 4;
                default: return -1;
            }
        }

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? "somebody";
    }
}
