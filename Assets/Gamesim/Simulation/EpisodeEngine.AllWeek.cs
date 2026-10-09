using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// D2, the all-week NPC strategy (WAVE-D-NPC-PACTS-PLAN §4): the house's turns spread over the four
    /// windows of every week, fired on the season's own steps.
    ///
    /// <para><b>The cadence.</b> As a window first needs one, the engine plans it: the houseguests in the
    /// house who are not resting this window (<see cref="Rests"/>), in an order the window's own keyed
    /// stream shuffles, one beat each, with the window's seats as they stood (<see cref="NpcSocialState"/>'s
    /// beat fields). Each committed command then catches the house up (<see cref="CatchUp(EpisodeState)"/>):
    /// as the player spends the window's actions, the plan's beats fall due in step (<see cref="Due"/>), and
    /// whatever is left fires as the window closes (<see cref="Close"/>), before the step that ends it. A
    /// beat is one success on <see cref="NpcSocialActions.Settle"/>'s ladder (<see cref="NpcSocialActions.Beat"/>),
    /// three a houseguest a week, two on slop; it draws only from its own keyed stream, never the
    /// season's, and what it did is kept as this week's act (<see cref="NpcSocialState.acts"/>), in a room
    /// where it can be seen. The beats the window's close fires never involve the player: their cards
    /// would be cleared before the player could answer them, and the window is over.</para>
    ///
    /// <para><b>Standing business stays where it was:</b> pacts that sour fall apart as the social week opens
    /// (where the weekly pass ran, now <see cref="NpcAlliances.Dissolve"/> alone), the deals, the words given
    /// as the campaign opens, the veto's ask, courting the Head of Household and the campaign - the last two
    /// recorded as acts, with no draw.</para>
    ///
    /// <para>Keyed to <see cref="EpisodeState.allWeekRulesStartWeek"/> and the week rules; before it the
    /// house plays the week as it always did. Loading fires nothing, and nothing reads the clock.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Switches the all-week beats on from a week, no later than the week after the season's own
        /// (schema 28's boundary). They play in the week's windows, so the week rules must start no later:
        /// not that they are on yet, which the importer's week rules, set for next week, are not.
        /// </summary>
        public static void EnableAllWeek(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            int week = Math.Max(1, Math.Min(fromWeek, s.week + 1));
            if (s.weekRulesStartWeek < 1 || s.weekRulesStartWeek > week)
                throw new ArgumentException("The all-week beats play in the week's windows: the week rules must start no later than they do.", nameof(s));
            s.allWeekRulesStartWeek = week;
        }

        /// <summary>Whether the house plays its turns all week (X8, D2-M2): the start week reached, the week in windows, and the house acting on its own account.</summary>
        public static bool AllWeekOn(EpisodeState s) =>
            s != null && s.allWeekRulesStartWeek >= 1 && s.week >= s.allWeekRulesStartWeek
            && WeekRulesOn(s) && NpcSocialState.AutonomyHasBegun(s);

        /// <summary>A houseguest's beats a week: the weekly pass's three turns.</summary>
        public const int BeatsAWeek = NpcSocialActions.ActionsPerSocialPhase;

        /// <summary>
        /// The most acts a week can hold (D2-L3): four for every houseguest who is not the player - three
        /// beats and a court, the evicted counted too, since the acts a week's evictee fired before the vote
        /// stay into its last window - and the campaign's visits, two for each of the two nominees.
        /// </summary>
        public static int MostActs(EpisodeState s) => 4 * s.contestants.Count(c => !c.isPlayer) + 2 * CampaignVisits;

        /// <summary>A houseguest's beats this week: three, or two for a Have-Not on slop and a cot (as the weekly pass gave them).</summary>
        public static int BeatQuota(EpisodeState s, string npcId) => OnSlop(s, npcId) ? BeatsAWeek - 1 : BeatsAWeek;

        private static bool OnSlop(EpisodeState s, string npcId) => StoryAt(s, StoryRules.Production) && Production.IsHaveNot(s, npcId);

        /// <summary>
        /// Whether the houseguest at <paramref name="index"/> in the cast rests in a window this week:
        /// one window a week each, turning with the week, <c>(index + week) mod 4</c>; a Have-Not rests
        /// in the opposite one as well.
        /// </summary>
        public static bool Rests(EpisodeState s, int index, int window)
        {
            if (index < 0 || index >= s.contestants.Count) return true;
            if ((index + s.week) % Windows.Count == window) return true;
            return OnSlop(s, s.contestants[index].id) && (index + s.week + 2) % Windows.Count == window;
        }

        /// <summary>
        /// How many of a window's <paramref name="beats"/> are due at a tick (the window's actions spent):
        /// in step with the seats, <c>ceil(beats·(tick+1)/(seats+1))</c>, and all of them once the seats are spent.
        /// </summary>
        public static int Due(int beats, int seats, int tick)
        {
            if (beats <= 0) return 0;
            if (tick >= seats) return beats;
            int t = Math.Max(0, tick);
            return (beats * (t + 1) + seats) / (seats + 1);
        }

        /// <summary>A window's tick: the actions spent in it so far.</summary>
        public static int WindowTick(EpisodeState s, int window) =>
            s.windowActions != null && window >= 0 && window < s.windowActions.Count ? s.windowActions[window] : 0;

        /// <summary>Whether an act is a beat's (<c>"{week}-{window}-{k}"</c>), as against a court or a campaign visit the week recorded.</summary>
        public static bool IsBeat(NpcActState act)
        {
            var parts = act?.id?.Split('-');
            return parts != null && parts.Length == 3 && parts.All(p => p.Length > 0 && p.All(char.IsDigit));
        }

        /// <summary>This week's beats a houseguest has acted in.</summary>
        public static int BeatsThisWeek(EpisodeState s, string npcId) =>
            s.npcSocial.acts.Count(a => a != null && a.week == s.week && a.actorId == npcId && IsBeat(a));

        /// <summary>The beats due at the open window's tick, after a command's own effects (§1).</summary>
        public static void CatchUp(EpisodeState s) => CatchUp(s, false);

        /// <summary>
        /// The same, player-free where <paramref name="playerFree"/>: a commit whose own lines put a counter
        /// on the table (C7) catches the house up without a word to the player, which would lapse it (D2's decision 6).
        /// </summary>
        public static void CatchUp(EpisodeState s, bool playerFree)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            Beats(s, false, playerFree);
        }

        /// <summary>
        /// The open window's remaining beats, as it closes, before the step that ends it (§1): never involving
        /// the player (D2-H3). Idempotent: a plan that has fired every beat fires nothing more.
        /// </summary>
        public static void Close(EpisodeState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            Beats(s, true, true);
        }

        private static void Beats(EpisodeState s, bool closing, bool playerFree)
        {
            if (!AllWeekOn(s) || IsFirstNight(s)) return;
            int window = Window(s);
            if (window == Windows.None) return;
            var social = s.npcSocial;
            if ((social.beatWeek != s.week || social.beatWindow != window) && !PlanBeats(s, window)) return;
            int tick = WindowTick(s, window);
            int due = closing ? social.beatPlan.Count : Due(social.beatPlan.Count, social.beatSeats, tick);
            while (social.beatsFired < due)
            {
                int k = social.beatsFired++;
                Beat(s, window, k, tick, closing || playerFree);
            }
        }

        /// <summary>
        /// The window's plan (§4.3): the houseguests in the house not resting, in the order the window's keyed
        /// stream shuffles (Fisher-Yates), and the window's seats as they stand. None with fewer than two
        /// houseguests to play it.
        /// </summary>
        private static bool PlanBeats(EpisodeState s, int window)
        {
            if (s.Active.Count(c => !c.isPlayer) < 2) return false;
            var plan = new List<string>();
            for (int i = 0; i < s.contestants.Count; i++)
            {
                var c = s.contestants[i];
                if (!c.isPlayer && c.status == ContestantStatus.Active && !Rests(s, i, window)) plan.Add(c.id);
            }
            var draw = StoryRandom.Stream(s, "allweek:order:" + s.week + ":" + window);
            for (int i = plan.Count - 1; i > 0; i--)
            {
                int j = Math.Min(i, (int)(draw() * (i + 1)));
                string swap = plan[i]; plan[i] = plan[j]; plan[j] = swap;
            }
            var social = s.npcSocial;
            social.beatWeek = s.week;
            social.beatWindow = window;
            social.beatsFired = 0;
            social.beatSeats = WindowSeats(s, window);
            social.beatPlan = plan;
            return true;
        }

        /// <summary>
        /// Beat <paramref name="k"/> of the window's plan: its houseguest's next success, on the beat's own
        /// keyed stream, kept as an act. A slot whose houseguest has gone, has had the week's beats or does
        /// nothing still counts.
        /// </summary>
        private static void Beat(EpisodeState s, int window, int k, int tick, bool playerFree)
        {
            var social = s.npcSocial;
            string npcId = social.beatPlan[k];
            var npc = s.Find(npcId);
            if (npc == null || npc.status != ContestantStatus.Active || BeatsThisWeek(s, npcId) >= BeatQuota(s, npcId)) return;
            if (social.acts.Count >= MostActs(s)) return;
            var act = NpcSocialActions.Beat(s, npcId, StoryRandom.Stream(s, "allweek:beat:" + s.week + ":" + window + ":" + k),
                playerFree, window == Windows.AfterVeto);
            if (act == null) return;
            act.id = s.week + "-" + window + "-" + k;
            act.week = s.week;
            act.window = window;
            act.firedTick = tick;
            act.room = StageRoom(s, act);
            social.acts.Add(act);
        }

        /// <summary>
        /// One of the week's acts that is not a beat - a court as the nominations open, a campaign visit -
        /// recorded under the rules with no draw, in the open window at its tick. A record already kept, or
        /// one past the week's bound, is not kept again.
        /// </summary>
        public static void RecordAct(EpisodeState s, string kind, string id, string actorId, string partnerId, string subjectId)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (!AllWeekOn(s) || IsFirstNight(s)) return;
            int window = Window(s);
            var acts = s.npcSocial.acts;
            if (window == Windows.None || acts.Any(a => a?.id == id) || acts.Count >= MostActs(s)) return;
            var act = new NpcActState
            {
                id = id, kind = kind, actorId = actorId, partnerId = partnerId, subjectId = subjectId,
                week = s.week, window = window, firedTick = WindowTick(s, window),
            };
            act.room = StageRoom(s, act);
            acts.Add(act);
        }

        /// <summary>
        /// The room an act is staged in (§4.3), by a keyed coin on its id; none for a kind never staged, and
        /// none for an act the player is in or is about (D2's decision 9): the player was there, or it is
        /// about them, and a sighting of it would be a sighting of nothing they could not know.
        /// </summary>
        private static string StageRoom(EpisodeState s, NpcActState act)
        {
            if (act.actorId == s.playerId || act.partnerId == s.playerId || act.subjectId == s.playerId) return null;
            bool suite = !string.IsNullOrEmpty(s.hohId) && (act.actorId == s.hohId || act.partnerId == s.hohId);
            var rooms = NpcActKinds.Rooms(act.kind, suite);
            return rooms.Length == 0 ? null : rooms[StoryRandom.Index(s, "allweek:room:" + act.id, rooms.Length)];
        }

        /// <summary>Whether an act can still be seen: its week and window are the open ones, both of the two are in the house, and the window has not moved two ticks past it.</summary>
        public static bool ActOpen(EpisodeState s, NpcActState act) => act != null && ActOpen(s, act, WindowTick(s, act.window));

        /// <summary>The same at a given tick of the act's window.</summary>
        public static bool ActOpen(EpisodeState s, NpcActState act, int tick) =>
            s != null && act != null && AllWeekOn(s) && act.week == s.week && act.window == Window(s)
            && s.Find(act.actorId)?.status == ContestantStatus.Active && s.Find(act.partnerId)?.status == ContestantStatus.Active
            && tick < act.firedTick + 2;

        /// <summary>Whether a commit's own lines, from <paramref name="fromSequence"/>, put a counter on the table (C7).</summary>
        private static bool CounterOnTheTable(EpisodeState s, int fromSequence) =>
            s.events.Any(e => e != null && e.sequence >= fromSequence && e.kind == Negotiation.CounterEventKind);

        /// <summary>
        /// The engine's relationship path on a given stream: a beat that involves the player moves the pair as
        /// the player's own acts do, its draws from the beat's keyed stream rather than the season's.
        /// </summary>
        internal static void ChangeOnStream(EpisodeState s, string from, string to, double delta, Func<double> roll,
            string note = null, string eventType = null) => ChangeWithRoll(s, from, to, delta, roll, note, eventType);
    }
}
