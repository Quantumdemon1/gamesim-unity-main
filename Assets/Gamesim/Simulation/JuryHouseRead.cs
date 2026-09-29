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
    /// </summary>
    public static class JuryHouseRead
    {
        public const string Supportive = "Supportive", Wavering = "Wavering", Open = "Open",
            Skeptical = "Skeptical", Bitter = "Bitter", Unknown = "Unknown";

        /// <summary>The bands in the order the legend reads them.</summary>
        public static readonly string[] Bands = { Supportive, Wavering, Open, Skeptical, Bitter, Unknown };

        public sealed class Juror
        {
            public string id, band, reason, trait;
            /// <summary>The week of the read the band starts from, or null when there is none.</summary>
            public int? readWeek;
            /// <summary>The week they left, from the ledger's power rows, or null when it is not on the record.</summary>
            public int? leftWeek;
            /// <summary>What the player and they share: alliances, deals, promises, the player's public votes against them.</summary>
            public List<string> knows = new List<string>();
            /// <summary>What the player did after they left, which they did not see.</summary>
            public List<string> missing = new List<string>();
            /// <summary>Dated lines from the rows between them, oldest first ("Week 6 · left on your nomination").</summary>
            public List<string> highlights = new List<string>();
        }

        public sealed class House
        {
            public List<Juror> jurors = new List<Juror>();
            /// <summary>What most of the jury leads with, worded, from the traits the questioning reads.</summary>
            public List<string> matters = new List<string>();
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
            return house;
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
            bool Later(PromiseState p) =>
                p.kind == PromiseKind.FinalTwo
                // Safety and alliance loyalty break at a nomination, no later than the promise's end.
                || ((p.kind == PromiseKind.Safety || p.kind == PromiseKind.AllianceLoyalty) && p.expiresWeek > week)
                || p.week > week;
            if (s.promises.Any(p => p.fromId == player && p.toId == jurorId && p.status == PromiseStatus.Broken && Later(p)))
                return "you broke a promise to them since";
            if (s.ledger?.replies != null && s.ledger.replies.Any(r => r.kind == ReplyCards.Plea && r.fromId == jurorId && r.replyKey == "refuse" && r.week > week))
                return "you refused their plea since";
            if (s.ledger?.ballots != null && s.ledger.ballots.Any(b => b.voterId == player && b.targetId == jurorId && b.week > week))
                return "you voted to evict them since";
            if (s.deals.Any(d => d.status == DealStatus.Broken && (d.week > week || d.type == DealKind.FinalTwo)
                && ((d.proposerId == player && d.recipientId == jurorId) || (d.proposerId == jurorId && d.recipientId == player))))
                return "a deal between you broke since";
            return null;
        }

        private static string Word(double score) => score >= FinalistRead.WarmStanding ? "warm on you" : score <= FinalistRead.ColdStanding ? "cold on you" : "not sure about you";

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
            int votes = s.ledger?.ballots?.Count(b => b.voterId == player && b.targetId == id) ?? 0;
            if (votes > 0) juror.knows.Add("You voted to evict them " + (votes == 1 ? "once" : votes + " times") + ", in the open.");
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
                foreach (var b in s.ledger.ballots.Where(b => b.voterId == player && b.targetId == id))
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
            juror.highlights = lines.OrderBy(l => l.week).Select(l => "Week " + l.week + " · " + l.text).Distinct().ToList();
        }
    }
}
