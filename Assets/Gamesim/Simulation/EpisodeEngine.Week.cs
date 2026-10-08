using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>The four windows of a week (STRATEGY-LOOP-PLAN.md §4), and none.</summary>
    public static class Windows
    {
        public const int AfterHoH = 0, AfterNominations = 1, AfterVeto = 2, AfterEviction = 3, None = -1;
        public const int Count = 4;
        public static readonly string[] Names = { "After the HoH", "After the nominations", "After the veto", "After the eviction" };
    }

    /// <summary>
    /// The week (STRATEGY-LOOP-PLAN.md §4): four windows, each with a budget of its own, sitting
    /// between the story's anchors, which keep their meaning and order. The one weekly pool made the
    /// campaign something spent in the free time before it; a window's seats are the window's, and
    /// what is not spent does not carry. Keyed to <see cref="EpisodeState.weekRulesStartWeek"/>:
    /// before it, the week is the pool it always was.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The fixed seats of the first three windows; the fourth scales with the house.</summary>
        public const int AfterHoHSeats = 2, AfterNominationsSeats = 1, AfterVetoSeats = 2;

        public static void EnableWeek(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.weekRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>Whether the week runs in windows this week.</summary>
        public static bool WeekRulesOn(EpisodeState s) => s != null && s.weekRulesStartWeek >= 1 && s.week >= s.weekRulesStartWeek;

        /// <summary>E1 is selected only at fresh-season creation, never inferred from an old save's week or phase.</summary>
        public static void EnableEconomy(EpisodeState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (!IsFirstNight(s) || !WeekRulesOn(s) || s.revision != 0 || s.socialActions != 0 || s.outOfPhaseSocialActions != 0
                || s.boughtActionPoints != 0 || s.moveInExtrasSpent != 0 || s.acceptedCommandIds == null || s.acceptedCommandIds.Count != 0
                || s.windowActions == null || s.windowActions.Count != Windows.Count || s.windowActions.Any(n => n != 0))
                throw new ArgumentException("The new economy can only be selected for a fresh, unplayed season with window rules.", nameof(s));
            s.economyRulesVersion = 1;
        }

        public static bool EconomyRulesOn(EpisodeState s) => WeekRulesOn(s) && s.economyRulesVersion == 1;

        /// <summary>The window a phase sits in, or none: competitions, ceremonies and eviction night have no seats.</summary>
        public static int Window(EpisodeState s)
        {
            switch (s.phase)
            {
                case EpisodePhase.Nomination: return Windows.AfterHoH;
                case EpisodePhase.VetoSelection:
                case EpisodePhase.Veto:
                case EpisodePhase.VetoMeeting: return Windows.AfterNominations;
                case EpisodePhase.Campaign: return Windows.AfterVeto;
                case EpisodePhase.Social: return Windows.AfterEviction;
                default: return Windows.None;
            }
        }

        /// <summary>
        /// A window's seats: two after the HoH, one after the nominations, two after the veto, and
        /// after the eviction ceil(half the house) minus three. Legacy seasons floor that at one;
        /// E1 floors it at two and gives move-in night two independent seats regardless of cast size.
        /// </summary>
        public static int WindowSeats(EpisodeState s, int window)
        {
            switch (window)
            {
                case Windows.AfterHoH: return AfterHoHSeats;
                case Windows.AfterNominations: return AfterNominationsSeats;
                case Windows.AfterVeto: return AfterVetoSeats;
                case Windows.AfterEviction:
                    if (EconomyRulesOn(s) && IsFirstNight(s)) return 2;
                    return Math.Max(EconomyRulesOn(s) ? 2 : 1,
                        (int)Math.Ceiling(Math.Max(0, s.Active.Count()) / 2.0) + 2 - (AfterHoHSeats + AfterNominationsSeats + AfterVetoSeats));
                default: return 0;
            }
        }

        /// <summary>The week's extras, once a week: bought time and what a storyline left, less a Have-Not's conversation. Can be negative.</summary>
        public static int WeeklyExtras(EpisodeState s) => Math.Max(0, s.boughtActionPoints) + Storylines.SocialActions(s) - HaveNots.ActionCost(s);

        /// <summary>
        /// The open window's budget: its seats, plus whatever of the week's extras the other windows
        /// have not spent past their own seats, so a storyline's +1 is +1 a week, spendable in any
        /// window; a negative pool (a Have-Not with no bonus) comes off the first window after the
        /// Have-Nots are named. Floored at one while a window is open: a window with no conversation
        /// would be a window the player cannot play.
        /// </summary>
        private static int WindowBudget(EpisodeState s)
        {
            int window = Window(s);
            if (window == Windows.None) return 0;
            int seats = WindowSeats(s, window);
            int extras = WeeklyExtras(s);
            if (extras < 0) return Math.Max(1, seats + (window == Windows.AfterHoH ? extras : 0));
            // Opening counters reset when the first competition starts, but the extras spent there
            // are not refunded. Keep this debit inside the positive-credit clamp: an expired bonus
            // is not a new penalty, and Have-Not penalties keep their existing separate branch.
            int usedElsewhere = EconomyRulesOn(s) ? s.moveInExtrasSpent : 0;
            for (int other = 0; other < Windows.Count; other++)
            {
                if (other == window) continue;
                int spent = s.windowActions != null && other < s.windowActions.Count ? s.windowActions[other] : 0;
                usedElsewhere += Math.Max(0, spent - WindowSeats(s, other));
            }
            return Math.Max(1, seats + Math.Max(0, extras - usedElsewhere));
        }

        /// <summary>What the open window has spent; nothing outside a window.</summary>
        private static int WindowSpent(EpisodeState s)
        {
            int window = Window(s);
            return window == Windows.None || s.windowActions == null || window >= s.windowActions.Count ? 0 : s.windowActions[window];
        }

        private static void SpendInWindow(EpisodeState s)
        {
            int window = Window(s);
            if (window == Windows.None || s.windowActions == null) return;
            while (s.windowActions.Count < Windows.Count) s.windowActions.Add(0);
            s.windowActions[window]++;
        }

        private static void ResetWindows(EpisodeState s)
        {
            if (s.windowActions == null) return;
            for (int i = 0; i < s.windowActions.Count; i++) s.windowActions[i] = 0;
        }
    }
}
