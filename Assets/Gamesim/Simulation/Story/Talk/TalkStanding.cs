using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Where a housemate stands this week (layer 4, spec 3.7, ruling R6): who they are close to, who worries them and how safe they
    /// feel, read from the scores as they are, and said in the candour <see cref="Candour.Choose"/> gives them for the "where"
    /// question on its own keyed coin. Honest names the names; neutral keeps them back (the neutral lines carry no names); dishonest
    /// names decoys: the second closest, and the next most worrying. Pure; nothing drawn from the season's stream.
    /// </summary>
    public static class TalkStanding
    {
        /// <summary>Their view of somebody at which they count them as having their back (spec 3.7).</summary>
        public const double BackView = 25;

        /// <summary>A view this low or lower is who worries them before any comp record (spec 3.7).</summary>
        public const double WorryView = -15;

        /// <summary>What {safe} says (a clause with a capital and no stop: the lines say "Me? {safe}.").</summary>
        public const string SafeHoh = "Safe this week. I hold the key", SafeBlock = "On the block, and I know it",
            SafeVeto = "Safe. I've got the veto", SafeNobody = "Nobody really has my back", SafeOne = "One person has my back",
            SafeFew = "Two or three people have my back", SafeMany = "Plenty of people have my back";

        /// <summary>When nobody else is left to name.</summary>
        public const string Nobody = "nobody";

        /// <summary>
        /// The stance this week, from Candour's own keyed coin for the "where" question (R6). From talk rules 2 a Close friend or
        /// closer never lies about it (T7): a dishonest draw becomes neutral; the same coin is drawn either way.
        /// </summary>
        public static string Stance(EpisodeState s, string npcId)
        {
            string stance = Candour.Choose(s, npcId, Candour.Where, null);
            if (stance == Candour.Dishonest && TalkFriendship.AtLeast(s, npcId, FriendStage.CloseFriend)) return Candour.Neutral;
            return stance;
        }

        /// <summary>The {close}, {worry} and {safe} parts in a stance: the true names, or in the dishonest stance the decoys; each name as the saved line writes it (<see cref="TalkText.SavedName"/>).</summary>
        public static Dictionary<string, string> Parts(EpisodeState s, string npcId, string stance)
        {
            var others = s.Active.Where(o => o.id != npcId && !o.isPlayer).ToList();
            var closest = others.OrderByDescending(o => s.Score(npcId, o.id)).ThenBy(o => o.id, StringComparer.Ordinal).ToList();
            var worries = Worries(s, npcId, others);
            var trueClose = closest.FirstOrDefault();
            var trueWorry = worries.FirstOrDefault(o => o != trueClose) ?? worries.FirstOrDefault();
            var close = trueClose;
            var worry = trueWorry;
            if (stance == Candour.Dishonest)
            {
                close = closest.Count > 1 ? closest[1] : trueClose;
                worry = worries.FirstOrDefault(o => o != trueWorry && o != close) ?? worries.FirstOrDefault(o => o != close) ?? trueWorry;
            }
            return new Dictionary<string, string>
            {
                ["close"] = TalkText.SavedName(s, close?.name) ?? Nobody,
                ["worry"] = TalkText.SavedName(s, worry?.name) ?? Nobody,
                ["safe"] = Safe(s, npcId, others),
            };
        }

        /// <summary>Who worries them, most first: anyone at or under -15 by their view, lowest first; then the comp record (HoH and veto wins); then the lowest view.</summary>
        private static List<ContestantState> Worries(EpisodeState s, string npcId, List<ContestantState> others)
        {
            var feared = others.Where(o => s.Score(npcId, o.id) <= WorryView)
                .OrderBy(o => s.Score(npcId, o.id)).ThenBy(o => o.id, StringComparer.Ordinal).ToList();
            var rest = others.Where(o => !feared.Contains(o))
                .OrderByDescending(o => o.hohWins + o.vetoWins).ThenBy(o => s.Score(npcId, o.id)).ThenBy(o => o.id, StringComparer.Ordinal);
            return feared.Concat(rest).ToList();
        }

        /// <summary>How safe they feel: HoH, on the block, holding the veto, or how many have their back.</summary>
        public static string Safe(EpisodeState s, string npcId, List<ContestantState> others)
        {
            if (s.hohId == npcId) return SafeHoh;
            if (s.nominees.Contains(npcId)) return SafeBlock;
            if (s.vetoHolderId == npcId) return SafeVeto;
            int backs = others.Count(o => s.Score(o.id, npcId) >= BackView);
            return backs == 0 ? SafeNobody : backs == 1 ? SafeOne : backs <= 3 ? SafeFew : SafeMany;
        }

        /// <summary>
        /// The standing line a talk says in a stance, filled as it is saved (<see cref="TalkText.SavedName"/>: full names below talk
        /// rules 4, first names from it), at most the saved snapshot's 256 characters; null without lines.
        /// </summary>
        public static string Line(EpisodeState s, string npcId, TalkRow talk, string stance)
        {
            var lines = talk?.Standing;
            if (lines == null) return null;
            string template = stance == Candour.Honest ? lines.Honest : stance == Candour.Dishonest ? lines.Dishonest : lines.Neutral;
            if (string.IsNullOrEmpty(template)) template = lines.Neutral;
            if (string.IsNullOrEmpty(template)) return null;
            string line = TalkText.Fill(s, npcId, template, Parts(s, npcId, stance), name => TalkText.SavedName(s, name));
            return line.Length <= TalkRules.MostStandingLength ? line : line.Substring(0, TalkRules.MostStandingLength);
        }
    }
}
