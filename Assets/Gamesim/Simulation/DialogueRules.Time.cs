using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 4's conversation time (DIALOGUE-PLAN §4 and §5.4, slice D5c): a campaign conversation or an appeal to
    /// the veto holder can win the player one more conversation in its window, or cost them one. The receipt is the
    /// conversation's ending receipt (<see cref="DialogueTimeReceiptState"/>, at home since schema 29): no saved field is
    /// added.
    ///
    /// <list type="bullet">
    /// <item><b>+1</b> after a good landing: the houseguest warmed to a reply. One more conversation in this window.</item>
    /// <item><b>-1</b> after a reply the screen marks as a risk (medium or high) backfired. The argument cost an opportunity.</item>
    /// <item>Otherwise 0, and always 0 for a conversation that lapsed or for the veto question, whose answer is its reward.</item>
    /// </list>
    /// <para>What is proposed follows from the path alone: the reaction is the tree's own keyed coin, and the option is the
    /// player's choice. Nothing is drawn. What is applied is the proposal unless it is refused: the window the conversation
    /// belongs to must still be open, a week takes at most one applied +1 and one applied -1 (capped separately, counted on the
    /// week's retained receipts, so a refused +1 records proposed 1 and applied 0 and is never retried), and a -1 applies only
    /// while two conversations remain after the conversation's own cost. <c>WindowBudget</c> reads the applied receipts
    /// (EpisodeEngine.Week.cs); they never pass through the positive-only bank. Below rules 4 every receipt stays zero and
    /// nothing here reads differently.</para>
    /// </summary>
    public static partial class DialogueRules
    {
        /// <summary>The version that introduced conversation time.</summary>
        public const int TimeFrom = Rules4;

        public const int EarnedTime = 1, LostTime = -1;

        /// <summary>The one line the player is told when a receipt applies (DIALOGUE-PLAN §6's slice row).</summary>
        public const string EarnedTimeLine = "One more conversation in this window.", LostTimeLine = "The argument cost you an opportunity.";

        /// <summary>Whether a season's dialogue rules write and read conversation time: rules 4 and later.</summary>
        public static bool TimeOn(EpisodeState s) => On(s) && s.dialogueRulesVersion >= TimeFrom;

        /// <summary>The line for an applied receipt, or null for none.</summary>
        public static string TimeLine(int applied) => applied > 0 ? EarnedTimeLine : applied < 0 ? LostTimeLine : null;

        /// <summary>
        /// The receipt a conversation's ending proposes: +1 when the houseguest warmed to a reply, -1 when a reply marked as a
        /// risk backfired, otherwise 0. Only the campaign trees and the holder appeal carry time, and only at rules 4 and later;
        /// a lapse proposes nothing. Reads the path and the responses; draws nothing.
        /// </summary>
        internal static int ProposedTime(EpisodeState s, ArcTemplate template, StorylineState cycle, DialogueEpisodeState e, bool lapsed)
        {
            if (!TimeOn(s) || lapsed || template == null || !IsRules3Template(template.id) || cycle?.path == null || e?.responses == null)
                return 0;
            string prefix = template.id + "/";
            foreach (var response in e.responses)
            {
                if (response?.responseKey == null || !response.responseKey.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string suffix = response.responseKey.Substring(prefix.Length);
                if (suffix == DialogueCampaignTrees.Warmed) return EarnedTime;
                if (suffix != DialogueCampaignTrees.Backfired) continue;
                var step = response.pathIndex >= 0 && response.pathIndex < cycle.path.Count ? cycle.path[response.pathIndex] : null;
                string risk = step == null ? null : template.Beat(step.beatId)?.Option(step.optionId)?.risk;
                return risk == HouseEventRisk.Medium || risk == HouseEventRisk.High ? LostTime : 0;
            }
            return 0;
        }

        /// <summary>Whether another conversation of this week already holds an applied receipt of this sign.</summary>
        internal static bool TimeTaken(EpisodeState s, int week, int delta, DialogueEpisodeState except) =>
            s?.dialogue?.episodes?.Any(x => x != null && !ReferenceEquals(x, except) && (except == null || x.id != except.id) && x.week == week
                && x.time != null && Math.Sign(x.time.appliedDelta) == Math.Sign(delta)) == true;

        /// <summary>The applied receipts of the season's current week in one window: what <c>WindowBudget</c> adds. Zero below rules 4.</summary>
        public static int TimeInWindow(EpisodeState s, int window) => !TimeOn(s) || s.dialogue?.episodes == null ? 0
            : s.dialogue.episodes.Where(e => e?.time != null && e.week == s.week && e.time.window == window).Sum(e => e.time.appliedDelta);

        /// <summary>The applied +1s of the current week in one window: actions they paid for are not the week's extras.</summary>
        public static int EarnedInWindow(EpisodeState s, int window) => !TimeOn(s) || s.dialogue?.episodes == null ? 0
            : s.dialogue.episodes.Count(e => e?.time != null && e.week == s.week && e.time.window == window && e.time.appliedDelta > 0);

        // ---------------------------------------------------------------- the validator

        /// <summary>
        /// A rules-4 conversation's ending receipt: the proposal its own path makes, applied as proposed or refused to zero,
        /// never spent fields (a +1 is read where it was earned, so nothing marks its spending). Below rules 4: exactly zero.
        /// </summary>
        private static bool TimeReceiptHolds(EpisodeState s, ArcTemplate template, StorylineState cycle, DialogueEpisodeState e, bool lapsed)
        {
            var t = e.time;
            if (t == null || t.spentRevision != 0 || t.spentSequence != 0 || t.spentWindowAction != 0) return false;
            int proposed = ProposedTime(s, template, cycle, e, lapsed);
            return t.proposedDelta == proposed && (t.appliedDelta == 0 || t.appliedDelta == proposed);
        }

        /// <summary>A week holds at most one applied +1 and one applied -1, whatever conversations proposed them (§5.4).</summary>
        private static bool TimeRules4(EpisodeState s, out string error)
        {
            error = null;
            var applied = s.dialogue.episodes.Where(e => e?.time != null && e.time.appliedDelta != 0).ToArray();
            if (applied.Any(e => e.time.appliedDelta != EarnedTime && e.time.appliedDelta != LostTime)
                || applied.GroupBy(e => (e.week, e.time.appliedDelta)).Any(g => g.Count() > 1))
                return Fail(out error, "A week holds at most one conversation that won time and one that cost it.");
            return true;
        }
    }
}
