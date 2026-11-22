using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Overheard, on screen (plan 32 §I4 and §I5, slice S2b; story rules 12): the pure reads the house's watch, the listen
    /// tiles, the Nearby card and the relationship web share. Nothing here writes a season: the engine's
    /// <see cref="EpisodeEngine.OverhearRefusal"/> decides every Notice and Listen, and the hunches are read from the
    /// player's own <see cref="ClaimSource.Seen"/> rows alone, never from the real pacts. Below 12 every read here is off
    /// or empty, so a season that exists today shows nothing new.
    /// </summary>
    public static class Overhearing
    {
        /// <summary>The relationship web's one filter chip for the hunch lines (§I4): its caption, word for word.</summary>
        public const string HunchesCaption = "Hunches";

        /// <summary>The listen tiles' and the Nearby card's foot at 12: listening in is free there (§I5).</summary>
        public const string FreeFoot = "Free · stay close · they may notice";

        /// <summary>A walk-over's refusal when no conversation can be listened in on.</summary>
        public const string NobodyToListenTo = "Nobody is talking where you can listen in right now.";

        /// <summary>Whether overhearing plays here: story rules 12 or above.</summary>
        public static bool On(EpisodeState s) => s != null && EpisodeEngine.StoryAt(s, StoryRules.Manners);

        /// <summary>
        /// The topics a lingering player listens in on (§I2): strategy, alliance talk, nominations, gossip and tension. The
        /// others are only noticed, unless the player presses "Listen in".
        /// </summary>
        public static bool ListenTopic(string topic)
        {
            switch (topic)
            {
                case "strategy": case "alliance_talk": case "nominations": case "gossip": case "tension": return true;
                default: return false;
            }
        }

        /// <summary>The pending conversation with this sequence this week, or null.</summary>
        public static NpcConversationState Pending(EpisodeState s, long sequence) =>
            s?.npcSocial?.pending?.FirstOrDefault(p => p != null && p.sequence == sequence && p.week == s.week);

        /// <summary>Why the player cannot notice (<paramref name="listen"/> false) or listen in on this conversation now, or null.</summary>
        public static string Refusal(EpisodeState s, long sequence, bool listen)
        {
            if (!On(s)) return EpisodeEngine.WaveDKindRefusal;
            var conversation = Pending(s, sequence);
            if (conversation == null) return "That moment has passed.";
            return EpisodeEngine.OverhearRefusal(s, EpisodeEngine.ConversationText(sequence, listen), conversation.firstId, conversation.secondId);
        }

        /// <summary>Whether a Listen is left this week (§I1, counted from the ledger).</summary>
        public static bool ListenLeft(EpisodeState s) => On(s) && EpisodeEngine.ListenedThisWeek(s) < EpisodeEngine.ListensAWeek;

        /// <summary>
        /// The pending conversations the player could listen in on now, nearest first by <paramref name="distance"/> (the
        /// director's metres to the pair; a pair it cannot place reads as infinity and is left out).
        /// </summary>
        public static List<NpcConversationState> Listenable(EpisodeState s, Func<NpcConversationState, float> distance)
        {
            if (!On(s) || s.npcSocial?.pending == null) return new List<NpcConversationState>();
            return s.npcSocial.pending
                .Where(p => p != null && Refusal(s, p.sequence, true) == null)
                .Select(p => (p, at: distance == null ? 0f : distance(p)))
                .Where(item => !float.IsInfinity(item.at) && !float.IsNaN(item.at))
                .OrderBy(item => item.at).ThenBy(item => item.p.sequence)
                .Select(item => item.p).ToList();
        }

        /// <summary>The chance, in whole percent, that these two catch the player listening (§I3): 10 to 25.</summary>
        public static int CatchPercent(EpisodeState s, string firstId, string secondId) =>
            (int)Math.Round(100 * EpisodeEngine.CaughtChance(s, s.Find(firstId), s.Find(secondId)), MidpointRounding.AwayFromZero);

        /// <summary>
        /// The Nearby card's foot at 12 (§I5), with the catch risk for the pair being witnessed, shown before the press:
        /// "Free · stay close · 15% they notice". Without a pair, the plain <see cref="FreeFoot"/>.
        /// </summary>
        public static string NearbyFoot(EpisodeState s, string firstId, string secondId) =>
            s == null || firstId == null || secondId == null || s.Find(firstId) == null || s.Find(secondId) == null ? FreeFoot
                : "Free · stay close · " + CatchPercent(s, firstId, secondId).ToString(CultureInfo.InvariantCulture) + "% they notice";

        // ------------------------------------------------------------------ hunches (§I4)

        /// <summary>One hunch: two houseguests the player noticed together, in one tone.</summary>
        public sealed class Hunch
        {
            public string firstId, secondId;
            public int tone;
            /// <summary>The rows behind it: the current ones while any is current, otherwise all of them.</summary>
            public int rows;
            /// <summary>The weeks of those rows, earliest first, each once.</summary>
            public List<int> weeks = new List<int>();
            /// <summary>Whether a row is within <see cref="VoteRead.StandingShelfLife"/>: a hunch with none fades.</summary>
            public bool current;
        }

        /// <summary>
        /// Every hunch the player holds (§I4): the <see cref="ClaimSource.Seen"/> rows between two houseguests still in the
        /// house, grouped by the unordered pair and the tone. Never filtered by the real pacts, and never more than what the
        /// player saw. The current first (latest week, then the ids), then the faded. Empty below 12.
        /// </summary>
        public static List<Hunch> Hunches(EpisodeState s)
        {
            var hunches = new List<Hunch>();
            if (!On(s) || s.ledger?.standings == null) return hunches;
            bool InHouse(string id) => id != null && id != s.playerId && s.Find(id)?.status == ContestantStatus.Active;
            var rows = s.ledger.standings.Where(r => r != null && r.source == ClaimSource.Seen && HunchTone.IsKnown(r.score)
                && r.fromId != r.toId && InHouse(r.fromId) && InHouse(r.toId));
            foreach (var group in rows.GroupBy(r => (a: string.CompareOrdinal(r.fromId, r.toId) <= 0 ? r.fromId : r.toId,
                         b: string.CompareOrdinal(r.fromId, r.toId) <= 0 ? r.toId : r.fromId, tone: (int)r.score)))
            {
                var all = group.ToList();
                var live = all.Where(r => r.week >= s.week - VoteRead.StandingShelfLife).ToList();
                var counted = live.Count > 0 ? live : all;
                hunches.Add(new Hunch
                {
                    firstId = group.Key.a, secondId = group.Key.b, tone = group.Key.tone, rows = counted.Count,
                    weeks = counted.Select(r => r.week).Distinct().OrderBy(w => w).ToList(), current = live.Count > 0,
                });
            }
            return hunches.OrderByDescending(h => h.current).ThenByDescending(h => h.weeks.Last())
                .ThenBy(h => h.firstId, StringComparer.Ordinal).ThenBy(h => h.secondId, StringComparer.Ordinal)
                .ThenBy(h => h.tone).ToList();
        }

        /// <summary>A hunch's confidence (§I4): one row "maybe", two "probably", three or more "often".</summary>
        public static string Confidence(int rows) => rows >= 3 ? "often" : rows == 2 ? "probably" : "maybe";

        /// <summary>How the side column says a tone: "seem close", "seem to be scheming together", "seem to be at odds".</summary>
        public static string ToneWords(int tone) =>
            tone == HunchTone.Scheming ? "seem to be scheming together" : tone == HunchTone.Tense ? "seem to be at odds" : "seem close";

        /// <summary>A tone in one word, for the line's label: "warm", "scheming", "tense".</summary>
        public static string ToneWord(int tone) => tone == HunchTone.Scheming ? "scheming" : tone == HunchTone.Tense ? "tense" : "warm";

        /// <summary>"week 4", "weeks 4 and 5", "weeks 3, 4 and 5".</summary>
        public static string Weeks(IList<int> weeks)
        {
            if (weeks == null || weeks.Count == 0) return "";
            var words = weeks.Select(w => w.ToString(CultureInfo.InvariantCulture)).ToList();
            if (words.Count == 1) return "week " + words[0];
            return "weeks " + string.Join(", ", words.Take(words.Count - 1)) + " and " + words[words.Count - 1];
        }

        /// <summary>The side column's line (§I4): "Your hunch: Maya and Taylor seem close (noticed in weeks 4 and 5)."</summary>
        public static string Line(EpisodeState s, Hunch hunch) =>
            "Your hunch: " + FirstName(s, hunch.firstId) + " and " + FirstName(s, hunch.secondId) + " " + ToneWords(hunch.tone)
            + " (noticed in " + Weeks(hunch.weeks) + ").";

        /// <summary>The label on a hunch line: its confidence, and a "?" on a scheming one (§I4).</summary>
        public static string Label(Hunch hunch) => (hunch.tone == HunchTone.Scheming ? "? " : "") + Confidence(hunch.rows);

        private static string FirstName(EpisodeState s, string id) => CharacterNames.FirstName(s.Find(id)?.name ?? id);
    }
}
