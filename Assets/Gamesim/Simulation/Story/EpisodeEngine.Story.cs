using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The story system's runtime: story cycles that fire beats at the show's anchors, the pacing
    /// that keeps it from becoming a questionnaire, and the one command that answers a beat.
    ///
    /// <para>The design is <c>20-story-arcs-brainstorm.md</c> with the Big Brother addendum
    /// (<c>21-bb-build-addendum.md</c>). Every draw is keyed (<see cref="StoryRandom"/>), so nothing
    /// here moves <see cref="EpisodeState.randomState"/>; every consequence goes through the effect
    /// router in <c>EpisodeEngine.StoryEffects.cs</c> to a consumer the engine already reads.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        // ---------------------------------------------------------------- switches

        /// <summary>Whether the story system runs in this season this week.</summary>
        public static bool StoryOn(EpisodeState s) =>
            s?.story != null && s.story.rulesStartWeek >= 1 && s.week >= s.story.rulesStartWeek;

        /// <summary>Whether the story system runs and this season plays under at least this milestone.</summary>
        public static bool StoryAt(EpisodeState s, int version) => StoryOn(s) && s.story.rulesVersion >= version;

        /// <summary>
        /// Switches the story system on for a season: from <paramref name="fromWeek"/>, under every
        /// milestone this build has, with each houseguest's lore snapshotted. The director calls this
        /// for every season it starts; the save migration does the same from the week after a save's.
        /// </summary>
        public static void EnableStory(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.story == null) s.story = new StoryWorldState();
            s.story.rulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
            s.story.rulesVersion = StoryRules.Current;
            Lore.Cast(s);
        }

        /// <summary>An id from the season's own sequence. Consumes it, so two in one step never collide.</summary>
        private static string Mint(EpisodeState s, string prefix) => prefix + "-" + s.nextSequence++;

        // ---------------------------------------------------------------- where the week is

        /// <summary>The last anchor the week has passed, from the phase.</summary>
        public static string CurrentAnchor(EpisodeState s)
        {
            switch (s.phase)
            {
                case EpisodePhase.Social: return StoryAnchors.EvictionNight;
                case EpisodePhase.HoH: return StoryAnchors.SocialClose;
                case EpisodePhase.Nomination: return s.nominees.Count == 2 ? StoryAnchors.NomsSet : StoryAnchors.HohCrowned;
                case EpisodePhase.VetoSelection:
                case EpisodePhase.Veto: return StoryAnchors.NomsSet;
                case EpisodePhase.VetoMeeting: return StoryAnchors.VetoWon;
                case EpisodePhase.Campaign: return StoryAnchors.BlockSet;
                case EpisodePhase.Eviction: return StoryAnchors.EvictionEve;
                default: return null;
            }
        }

        /// <summary>The anchor after this one, round the week.</summary>
        public static string NextAnchor(string anchor)
        {
            switch (anchor)
            {
                case StoryAnchors.HohCrowned: return StoryAnchors.NomsSet;
                case StoryAnchors.NomsSet: return StoryAnchors.VetoWon;
                case StoryAnchors.VetoWon: return StoryAnchors.BlockSet;
                case StoryAnchors.BlockSet: return StoryAnchors.EvictionEve;
                case StoryAnchors.EvictionEve: return StoryAnchors.EvictionNight;
                case StoryAnchors.EvictionNight: return StoryAnchors.SocialClose;
                default: return StoryAnchors.HohCrowned;
            }
        }

        /// <summary>Whether an anchor still comes round this week after the current one.</summary>
        private static bool LaterThisWeek(string current, string anchor)
        {
            int now = StoryAnchors.Order(current), then = StoryAnchors.Order(anchor);
            return now >= 0 && then > now;
        }

        // ---------------------------------------------------------------- airtime

        /// <summary>Most beats a week may ask the player, not counting a Diary Room summons.</summary>
        public const int AsksAWeek = 2;
        /// <summary>Most Diary Room summonses a week.</summary>
        public const int SummonsesAWeek = 1;

        private static bool Asks(HouseEventState e) =>
            e.IsStory && StorySurfaces.Asks(e.surface) && e.surface != StorySurfaces.Summons;

        /// <summary>A story beat still waiting on the player, not counting a summons.</summary>
        public static HouseEventState OpenStoryAsk(EpisodeState s) =>
            s?.houseEvents.FirstOrDefault(e => !e.resolved && Asks(e));

        /// <summary>A Diary Room summons still waiting on the player.</summary>
        public static HouseEventState OpenSummons(EpisodeState s) =>
            s?.houseEvents.FirstOrDefault(e => !e.resolved && e.IsStory && e.surface == StorySurfaces.Summons);

        /// <summary>Every story beat waiting on the player, oldest first.</summary>
        public static List<HouseEventState> OpenStoryBeats(EpisodeState s) =>
            s?.houseEvents.Where(e => !e.resolved && e.IsStory).ToList() ?? new List<HouseEventState>();

        /// <summary>
        /// The anchor the next Advance from here closes at, or null when it closes none - a
        /// competition not yet played, or a decision that is the player's to make first. The
        /// episode screen warns above its way on before it lets a storyline pass (plan §5.2).
        /// </summary>
        public static string AdvanceCloses(EpisodeState s)
        {
            if (s == null) return null;
            switch (s.phase)
            {
                case EpisodePhase.Social: return StoryAnchors.SocialClose;
                case EpisodePhase.HoH: return s.competitionResolved ? StoryAnchors.HohCrowned : null;
                case EpisodePhase.Veto: return s.competitionResolved ? StoryAnchors.VetoWon : null;
                case EpisodePhase.Nomination: return s.nominees.Count == 0 && s.hohId != s.playerId ? StoryAnchors.NomsSet : null;
                case EpisodePhase.VetoMeeting: return s.vetoResolved ? StoryAnchors.BlockSet : null;
                case EpisodePhase.Campaign: return StoryAnchors.EvictionEve;
                case EpisodePhase.Eviction: return s.evictionResolved ? StoryAnchors.EvictionNight : null;
                default: return null;
            }
        }

        /// <summary>The open beats the next Advance lets pass, by the rule <see cref="StoryLapse"/> applies to them.</summary>
        public static List<HouseEventState> LapsingOnAdvance(EpisodeState s)
        {
            string anchor = AdvanceCloses(s);
            if (anchor == null || !StoryOn(s)) return new List<HouseEventState>();
            return OpenStoryBeats(s).Where(e => e.closesAnchor == anchor || anchor == StoryAnchors.SocialClose || e.week < s.week - 1).ToList();
        }

        /// <summary>
        /// The asks this week. A beat that follows another at once is the same conversation, so a
        /// cycle's beats that close at the same anchor count once: the budget is interruptions, not cards.
        /// </summary>
        private static int AsksThisWeek(EpisodeState s) =>
            s.houseEvents.Where(e => e.week == s.week && Asks(e) && !IsPlayBeat(s, e))
                .Select(e => e.cycleId + "|" + e.closesAnchor).Distinct().Count();

        // ---------------------------------------------------------------- plays' own airtime (plan 30 §5)

        /// <summary>Most new play offers a week: their own budget, beside the arcs' asks rather than out of them.</summary>
        public const int PlayOffersAWeek = 2;

        /// <summary>Whether a beat belongs to a play: plays keep their own airtime.</summary>
        private static bool IsPlayBeat(EpisodeState s, HouseEventState e) =>
            e.cycleId != null && StoryCatalog.Find(s.storylines.FirstOrDefault(x => x.id == e.cycleId)?.templateId)?.play != null;

        /// <summary>An arc's ask still waiting on the player: plays are left out, as they are of the weekly count.</summary>
        private static HouseEventState OpenArcAsk(EpisodeState s) =>
            s.houseEvents.FirstOrDefault(e => !e.resolved && Asks(e) && !IsPlayBeat(s, e));

        /// <summary>A play's beat still waiting on the player: one at a time, like the arcs'.</summary>
        private static HouseEventState OpenPlayAsk(EpisodeState s) =>
            s.houseEvents.FirstOrDefault(e => !e.resolved && Asks(e) && IsPlayBeat(s, e));

        /// <summary>The plays offered this week: their offers, not their steps.</summary>
        private static int PlayOffersThisWeek(EpisodeState s) =>
            s.houseEvents.Count(e => e.week == s.week && IsPlayBeat(s, e) && e.contentId != null && e.contentId.EndsWith(":offer", StringComparison.Ordinal));
        private static int SummonsesThisWeek(EpisodeState s) =>
            s.houseEvents.Count(e => e.week == s.week && e.IsStory && e.surface == StorySurfaces.Summons);

        /// <summary>
        /// Whether another beat may be put to the player now: one open at a time, two a week, one
        /// Diary Room summons a week on top. The web's own cadence is per-system; this replaces the
        /// port's one-situation-a-week slot with a budget every source shares.
        /// </summary>
        /// <summary>The one card before the first eviction: the first night, which rides on the opening.</summary>
        internal const string FirstNightArc = "first-night";

        /// <summary>
        /// Whether the season is still before its first ask (plan §5.2): the first can open at week
        /// one's eviction night, and before that the only story is the first night's card. Nothing
        /// new starts, and a beat that would reach the player waits.
        /// </summary>
        public static bool BeforeTheFirstAsk(EpisodeState s) => s != null && s.week <= 1 && !s.evictionResolved;

        public static bool AirtimeFor(EpisodeState s, string surface, bool mustFire = false) =>
            AirtimeFor(s, surface, mustFire, false, false);

        /// <summary>
        /// The same, for a play's beat (plan 30 §5): plays have their own open slot and a budget of
        /// <see cref="PlayOffersAWeek"/> offers, and a play the player has taken on is not held back by
        /// any weekly count at all - they chose to chase it. An arc's ask and a play's can be open at
        /// once; each waits only for its own kind.
        /// </summary>
        public static bool AirtimeFor(EpisodeState s, string surface, bool mustFire, bool play, bool playStep)
        {
            if (s.houseEvents.Count >= HouseEvents.Ceiling) return false;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return false;
            if (surface == StorySurfaces.Npc) return true;
            if (play) return OpenPlayAsk(s) == null && (playStep || PlayOffersThisWeek(s) < PlayOffersAWeek);
            if (surface == StorySurfaces.Summons)
                return OpenSummons(s) == null && (mustFire || SummonsesThisWeek(s) < SummonsesAWeek);
            return OpenArcAsk(s) == null && (mustFire || AsksThisWeek(s) < AsksAWeek);
        }

        // ---------------------------------------------------------------- the anchors

        /// <summary>
        /// Lapses every beat that closes at this anchor, before the Advance does anything else: a
        /// beat's window names the Advance that closes it, and its lapse option resolves inside that
        /// same command. Nothing expires in real time and nothing piles up.
        /// </summary>
        internal static void StoryLapse(EpisodeState s, string anchor)
        {
            if (s?.story == null) return;
            foreach (var item in s.houseEvents.Where(e => e.IsStory && !e.resolved
                         && (e.closesAnchor == anchor || anchor == StoryAnchors.SocialClose || e.week < s.week - 1)).ToList())
            {
                int index = item.choices.FindIndex(c => c.optionId == item.lapseOptionId);
                ResolveStoryBeat(s, item, index, true, null);
            }
        }

        /// <summary>
        /// The story system's pulse at an anchor, after the anchor's own engine work: departures,
        /// scheduled beats, pulses, and at most one new arc from the pool.
        /// </summary>
        internal static void StoryAnchor(EpisodeState s, string anchor)
        {
            if (!StoryOn(s)) return;
            var ctx = new StoryContext(s, anchor);
            EndDepartedCycles(s);
            StorySystemsAt(s, anchor);
            // Plays first: one decided here opens nothing further below.
            DecidePlays(s, anchor);

            foreach (var cycle in RunningCycles(s).ToList())
            {
                var template = StoryCatalog.Find(cycle.templateId);
                if (template == null || s.houseEvents.Any(e => !e.resolved && e.cycleId == cycle.id)) continue;
                // A play taken on with nothing scheduled is waiting for its goal or its deadline,
                // which DecidePlays settles: it neither blows over nor goes stale meanwhile.
                if (template.play != null && TakenOn(cycle) && cycle.nextAnchor == null) continue;
                var view = new StoryCycle(cycle, template);
                if (cycle.nextAnchor == anchor && cycle.nextWeek <= s.week)
                {
                    FireScheduled(s, view, anchor);
                    continue;
                }
                // A beat waiting on a conversation the player never has goes stale like any other:
                // an arc holding its lane for weeks is a lane nothing else can use.
                if (cycle.nextAnchor != null && cycle.nextWeek + 2 < s.week)
                {
                    EndCycle(s, cycle, "stale");
                    continue;
                }
                if (cycle.nextAnchor == null)
                {
                    // A cycle waiting on its pulse: the pulse fires a beat, ends it ("end:why"), or
                    // lets it wait. One with no pulse has nothing left to happen, and one that has
                    // waited a month has blown over.
                    if (template.pulse == null) { EndCycle(s, cycle, "done"); continue; }
                    int lastActive = Math.Max(cycle.week, cycle.path.Count == 0 ? 0 : cycle.path.Max(p => p.week));
                    if (lastActive + 4 < s.week) { EndCycle(s, cycle, "stale"); continue; }
                    string beatId = template.pulse(ctx, view);
                    if (beatId != null && beatId.StartsWith("end:", StringComparison.Ordinal)) { EndCycle(s, cycle, beatId.Substring(4)); continue; }
                    var beat = beatId == null ? null : template.Beat(beatId);
                    if (beat != null) Fire(s, view, beat, anchor, false);
                }
            }

            TryStartFromPool(s, ctx);
            TryStartPlayFromPool(s, ctx);
        }

        /// <summary>The running story cycles, in id order.</summary>
        private static IEnumerable<StorylineState> RunningCycles(EpisodeState s) =>
            s.storylines.Where(x => x.beatId != null && StorylineStatus.Running(x.status))
                .OrderBy(x => x.id, StringComparer.Ordinal);

        private static void FireScheduled(EpisodeState s, StoryCycle cycle, string anchor)
        {
            var beat = cycle.template.Beat(cycle.record.beatId);
            if (beat == null) { EndCycle(s, cycle.record, "lost"); return; }
            var ctx = new StoryContext(s, anchor);
            if (beat.ready != null && !beat.ready(ctx, cycle)) { Postpone(s, cycle.record, anchor); return; }
            if (!beat.NpcHeld && !AirtimeFor(s, beat.surface, cycle.template.lane == StoryLanes.Production || cycle.template.urgent,
                    cycle.template.play != null, TakenOn(cycle.record)))
            {
                Postpone(s, cycle.record, anchor);
                return;
            }
            Fire(s, cycle, beat, anchor, true);
        }

        /// <summary>
        /// Where a beat put off at this anchor tries again: the next anchor round the week. The close
        /// of the social window lapses beats but fires none (StoryAnchor never runs there), so a beat
        /// put off at eviction night waited there until it went stale, holding its lane; under the
        /// reach rules it tries again at the next Head of Household.
        /// </summary>
        public static string RetryAnchor(EpisodeState s, string anchor)
        {
            string next = NextAnchor(anchor);
            return next == StoryAnchors.SocialClose && StoryAt(s, StoryRules.Reach) ? StoryAnchors.HohCrowned : next;
        }

        /// <summary>A scheduled beat that cannot fire yet tries again at the next anchor.</summary>
        private static void Postpone(EpisodeState s, StorylineState cycle, string anchor)
        {
            string next = RetryAnchor(s, anchor);
            cycle.nextAnchor = next;
            cycle.nextWeek = LaterThisWeek(anchor, next) ? s.week : s.week + 1;
        }

        // ---------------------------------------------------------------- starting a cycle

        private static void TryStartFromPool(EpisodeState s, StoryContext ctx)
        {
            var candidates = new List<(ArcTemplate template, ArcBinding binding, double weight)>();
            foreach (var template in StoryCatalog.All)
            {
                // Plays draw from their own pool below.
                if (template.play != null || Array.IndexOf(template.startAnchors, ctx.anchor) < 0) continue;
                var binding = Castable(s, ctx, template);
                if (binding == null) continue;
                double weight = template.weight == null ? 10 : template.weight(ctx, binding);
                if (!(weight > 0)) continue;
                candidates.Add((template, binding, weight));
            }
            if (candidates.Count == 0) return;
            // How much "nothing new" weighs. The strategy windows took six arcs out of the house's pool,
            // which left seasons at three asks against the plan's four to six (§5.2): under the reach
            // rules the pause is shorter. The airtime's ceilings are unchanged.
            bool reach = StoryAt(s, StoryRules.Reach);
            double nothing = ctx.anchor == StoryAnchors.EvictionNight ? (reach ? 25 : 40) : (reach ? 40 : 60);
            double total = candidates.Sum(c => c.weight) + nothing;
            double roll = StoryRandom.Unit(s, "w" + s.week + ":" + ctx.anchor + ":pool") * total;
            foreach (var candidate in candidates.OrderBy(c => c.template.id, StringComparer.Ordinal))
            {
                if (roll < candidate.weight)
                {
                    StartCycle(s, candidate.template, candidate.binding, ctx.anchor);
                    return;
                }
                roll -= candidate.weight;
            }
        }

        /// <summary>How much "no new play" weighs in the plays' pool: low, so a castable play usually comes.</summary>
        public const double PlayNothingWeight = 20;

        /// <summary>
        /// Plays draw from their own pool (plan 30 §5): at most one new offer an anchor, on a roll of
        /// their own and within their own budget, so plays neither crowd out the house's arcs nor
        /// wait behind them. A season before the plays rules casts none, so its pool is untouched.
        /// </summary>
        private static void TryStartPlayFromPool(EpisodeState s, StoryContext ctx)
        {
            var candidates = new List<(ArcTemplate template, ArcBinding binding, double weight)>();
            foreach (var template in StoryCatalog.All)
            {
                if (template.play == null || Array.IndexOf(template.startAnchors, ctx.anchor) < 0) continue;
                var binding = Castable(s, ctx, template);
                if (binding == null) continue;
                double weight = template.weight == null ? 10 : template.weight(ctx, binding);
                if (weight > 0) candidates.Add((template, binding, weight));
            }
            if (candidates.Count == 0) return;
            double roll = StoryRandom.Unit(s, "w" + s.week + ":" + ctx.anchor + ":plays") * (candidates.Sum(c => c.weight) + PlayNothingWeight);
            foreach (var candidate in candidates.OrderBy(c => c.template.id, StringComparer.Ordinal))
            {
                if (roll < candidate.weight)
                {
                    StartCycle(s, candidate.template, candidate.binding, ctx.anchor);
                    return;
                }
                roll -= candidate.weight;
            }
        }

        /// <summary>
        /// Whether an arc may start now, and who would play it. Checks, in order: the rules version,
        /// the week and house size, its lane, its cooldowns, the player's own card, the airtime its
        /// opener needs, its casting, and the real-alumni rule for every part.
        /// </summary>
        public static ArcBinding Castable(EpisodeState s, StoryContext ctx, ArcTemplate template)
        {
            if (!StoryOn(s) || template == null || template.rulesVersion > s.story.rulesVersion) return null;
            if (BeforeTheFirstAsk(s) && template.id != FirstNightArc) return null;
            if (s.week < template.minWeek || s.Active.Count() < template.minActive) return null;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return null;
            if (s.storylines.Any(x => x.templateId == template.id && StorylineStatus.Running(x.status))) return null;
            if (template.lane == StoryLanes.Play)
            {
                if (RunningCycles(s).Count(x => x.lane == StoryLanes.Play) >= StoryLanes.MaxPlays) return null;
            }
            else if (template.lane != StoryLanes.Production
                     && RunningCycles(s).Any(x => x.lane == template.lane)) return null;
            if (Cooling(s, "tpl:" + template.id)) return null;
            if (template.group != null && Cooling(s, "grp:" + template.group)) return null;
            if (!StoryPeople.PlayerAllowed(s, template.playerNeeds)) return null;
            if (template.playerNeeds == StoryPeople.Sensitivity.Romance && !s.story.romanceStorylines) return null;
            var opener = template.beats.FirstOrDefault();
            if (opener == null) return null;
            // The first beat the player will be asked: the opener, or - when a houseguest holds the
            // opener, deciding the tone - the first one after it. An arc the house has no room to
            // put to the player does not start.
            var firstAsk = opener.NpcHeld ? template.beats.FirstOrDefault(b => !b.NpcHeld) : opener;
            if (firstAsk != null && !AirtimeFor(s, firstAsk.surface, template.lane == StoryLanes.Production || template.urgent,
                    template.play != null, false)) return null;
            var binding = template.cast?.Invoke(ctx);
            if (binding == null) return null;
            foreach (var role in template.roles)
            {
                var id = binding.Get(role.key);
                if (id == null) { if (role.optional) continue; return null; }
                var who = s.Find(id);
                if (who == null || who.isPlayer) return null;
                if (who.status != ContestantStatus.Active && !(role.departed && who.status == ContestantStatus.Jury)) return null;
                if (!StoryPeople.Allowed(s, id, role.needs)) return null;
            }
            string headliner = binding.headliner ?? template.roles.Select(r => binding.Get(r.key)).FirstOrDefault(id => id != null);
            if (headliner != null)
            {
                if (Cooling(s, "star:" + headliner)
                    || (template.oncePerHeadliner && Cooling(s, "once:" + template.id + ":" + headliner))) return null;
                binding.headliner = headliner;
            }
            var ids = template.roles.Select(r => binding.Get(r.key)).Where(id => id != null).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    if (Cooling(s, "pair:" + ids[i] + "|" + ids[j])) return null;
            return binding;
        }

        /// <summary>
        /// Starts one arc now if it casts at this anchor - for the port's verification runs and the
        /// tests, which need a particular story in front of them without playing seasons until it
        /// comes up. Casting, lanes, cooldowns and the arc's own rules hold; the weekly airtime and
        /// the pool's draw do not. Returns whether the arc is running afterwards.
        /// </summary>
        public static bool StartStory(EpisodeState s, string arcId, string anchor)
        {
            var template = StoryCatalog.Find(arcId);
            if (template == null || !StoryAt(s, template.rulesVersion)) return false;
            var binding = Castable(s, new StoryContext(s, anchor), template);
            if (binding == null) return false;
            var cycle = StartCycle(s, template, binding, anchor);
            return cycle != null && StorylineStatus.Running(cycle.status);
        }

        /// <summary>
        /// Starts an arc with a binding and fires its opener. Returns the cycle, or null when the
        /// opener could not be put to the player.
        /// </summary>
        internal static StorylineState StartCycle(EpisodeState s, ArcTemplate template, ArcBinding binding, string anchor,
            string openerId = null)
        {
            // An arc the engine starts at a particular point - production's ladder at the rung the
            // strike landed on - names its opener; everything else opens on its first beat.
            var opener = (openerId == null ? null : template.Beat(openerId)) ?? template.beats[0];
            var cycle = new StorylineState
            {
                id = Mint(s, "story"),
                templateId = template.id,
                category = template.eyebrow ?? template.lane,
                title = template.title,
                status = StorylineStatus.Active,
                week = s.week,
                endedWeek = 0,
                beatId = opener.id,
                lane = template.lane,
                variant = 0,
                cast = template.roles.Where(r => binding.Get(r.key) != null)
                    .Select(r => new StoryRoleState { role = r.key, contestantId = binding.Get(r.key) }).ToList(),
            };
            s.storylines.Add(cycle);
            if (binding.headliner != null && template.oncePerHeadliner) Cool(s, "once:" + template.id + ":" + binding.headliner, s.week + 100);
            Fire(s, new StoryCycle(cycle, template), opener, anchor, true);
            return cycle;
        }

        // ---------------------------------------------------------------- firing a beat

        /// <summary>
        /// Puts a beat in front of the player, or plays it out when an NPC holds it. A chained beat
        /// - one that follows the last at once, in the same conversation - is not a new ask, so the
        /// weekly budget does not stop it; one open card at a time still does.
        /// </summary>
        private static void Fire(EpisodeState s, StoryCycle cycle, BeatTemplate beat, string anchor, bool scheduled, bool chained = false)
        {
            cycle.record.beatId = beat.id;
            cycle.record.nextAnchor = null;
            cycle.record.nextWeek = 0;
            if (beat.NpcHeld)
            {
                PlayNpcBeat(s, cycle, beat, anchor);
                return;
            }
            if ((BeforeTheFirstAsk(s) && cycle.template.id != FirstNightArc)
                || !AirtimeFor(s, beat.surface, chained || cycle.template.lane == StoryLanes.Production || cycle.template.urgent,
                    cycle.template.play != null, TakenOn(cycle.record)))
            {
                Postpone(s, cycle.record, anchor);
                return;
            }
            var item = DrawBeat(s, cycle, beat, anchor);
            s.houseEvents.Add(item);
            cycle.record.eventId = item.id;
            Log(s, StoryLog.Offer, item.title + ". " + item.narrative, s.playerId);
        }

        /// <summary>
        /// Draws a beat: its options resolved to people now, its gates decided now, its effects
        /// stored on the choices, so a beat answered later moves the people it was always about.
        /// </summary>
        private static HouseEventState DrawBeat(EpisodeState s, StoryCycle cycle, BeatTemplate beat, string anchor)
        {
            var ctx = new StoryContext(s, anchor);
            var player = s.Find(s.playerId);
            var choices = new List<HouseEventChoice>();
            foreach (var option in beat.options)
            {
                if (option.onlyIf != null && option.onlyIf.Length > 0 && !Personality.Has(player, option.onlyIf)) continue;
                if (option.showIf != null && option.id != beat.lapse && !option.showIf(ctx, cycle)) continue;
                var choice = new HouseEventChoice
                {
                    label = option.label,
                    description = Trim(StoryText.Neutral(option.description) ?? string.Empty, 500),
                    risk = HouseEventRisk.IsKnown(option.risk) ? option.risk : HouseEventRisk.Low,
                    optionId = option.id,
                    glyph = option.glyph,
                    approach = Personality.Approach.IsKnown(option.approach) ? option.approach : null,
                    checkBase = option.check,
                    subjectId = option.check >= 0 ? cycle.Role(option.checkRole) ?? Headliner(cycle) : null,
                    lapse = option.id == beat.lapse,
                    costsAction = option.costsAction,
                    conduct = option.conduct,
                    bonusTraits = option.bonusTraits?.ToList() ?? new List<string>(),
                    against = option.against?.ToList() ?? new List<string>(),
                    next = option.next,
                    nextOnBackfire = option.nextOnBackfire,
                };
                var effects = option.effects.AsEnumerable();
                if (option.extra != null) effects = effects.Concat(option.extra(ctx, cycle) ?? Enumerable.Empty<Fx>());
                choice.effects = effects.Select(fx => ResolveFx(s, cycle, fx)).Where(e => e != null).Take(24).ToList();
                var backfire = option.backfire.AsEnumerable();
                if (option.backfireExtra != null) backfire = backfire.Concat(option.backfireExtra(ctx, cycle) ?? Enumerable.Empty<Fx>());
                choice.backfire = backfire.Select(fx => ResolveFx(s, cycle, fx)).Where(e => e != null).Take(24).ToList();
                if (option.pick != null)
                {
                    choice.pickPerson = true;
                    choice.eligibleIds = option.pick(ctx, cycle)?.Where(id => s.Find(id) != null).Distinct().Take(32).ToList() ?? new List<string>();
                    if (choice.eligibleIds.Count == 0) { choice.locked = true; choice.lockReason = "Nobody to name right now"; }
                }
                if (option.lockUnless != null && option.lockUnless.Length > 0 && !Personality.Has(player, option.lockUnless))
                {
                    choice.locked = true;
                    choice.lockReason = "Not in your character: " + string.Join(" or ", option.lockUnless);
                }
                if (option.needs != null && !option.needs(ctx, cycle))
                {
                    choice.locked = true;
                    choice.lockReason = option.needsLabel ?? "Not open to you yet";
                }
                if (option.conduct && !ConductOpen(s, cycle))
                {
                    choice.locked = true;
                    choice.lockReason = !StoryAt(s, StoryRules.Production) ? "Not in this season's rules"
                        : !string.IsNullOrEmpty(s.story.pendingRemovalId) ? "Off the table: production has warned you"
                        : "Not with this houseguest";
                }
                choices.Add(choice);
            }
            // A beat always has something the player can take: the lapse option is never locked.
            foreach (var choice in choices.Where(c => c.lapse)) { choice.locked = false; choice.lockReason = null; }

            var involved = cycle.record.cast.Select(r => r.contestantId).Where(id => id != s.playerId)
                .Distinct(StringComparer.Ordinal).ToList();
            return new HouseEventState
            {
                id = Mint(s, "house-event"),
                kind = HouseEventKind.Story,
                title = Trim(beat.title ?? cycle.template.title ?? "Something is happening", 200),
                narrative = Trim(beat.summary ?? StoryText.Neutral(beat.text) ?? "Something is happening in the house.", 2000),
                involvedIds = involved,
                week = s.week,
                resolved = false,
                chosenIndex = -1,
                contentId = cycle.template.id + ":" + beat.id,
                cycleId = cycle.Id,
                closesAnchor = beat.closes ?? NextAnchor(anchor == StoryAnchors.Conversation ? CurrentAnchor(s) : anchor),
                surface = StorySurfaces.IsKnown(beat.surface) ? beat.surface : StorySurfaces.Scene,
                venue = beat.venue,
                lapseOptionId = beat.lapse,
                cast = cycle.record.cast.Select(r => r.Clone()).ToList(),
                choices = choices,
            };
        }

        private static string Headliner(StoryCycle cycle) =>
            cycle.record.cast.FirstOrDefault()?.contestantId;

        private static string Trim(string text, int max) =>
            text == null ? null : text.Length <= max ? text : text.Substring(0, max);

        /// <summary>One consequence, from role terms to people.</summary>
        private static StoryEffectState ResolveFx(EpisodeState s, StoryCycle cycle, Fx fx)
        {
            if (fx == null || !StoryEffects.IsKnown(fx.kind)) return null;
            string Who(string key)
            {
                if (string.IsNullOrEmpty(key)) return null;
                if (key == Fx.Player) return s.playerId;
                if (key == Fx.Pick) return StoryEffects.Picked;
                // "@id" names somebody directly: an option whose people are worked out from the
                // season as it stands (a Have-Not draw, a witness list) rather than from a role.
                if (key[0] == '@') return s.Find(key.Substring(1))?.id;
                return cycle.Role(key);
            }
            var effect = new StoryEffectState
            {
                kind = fx.kind, fromId = Who(fx.from), toId = Who(fx.to), thirdId = Who(fx.third),
                type = fx.type, text = fx.text, amount = fx.amount, weeks = fx.weeks,
            };
            // A role nobody plays drops the effect rather than aiming it at nobody.
            if ((!string.IsNullOrEmpty(fx.from) && effect.fromId == null) || (!string.IsNullOrEmpty(fx.to) && effect.toId == null))
                return null;
            // A third party is optional for a fact's extra knower and an alliance's third member.
            if (!string.IsNullOrEmpty(fx.third) && effect.thirdId == null && fx.kind != StoryEffects.Fact && fx.kind != StoryEffects.Alliance) return null;
            return effect;
        }

        /// <summary>An NPC holds this choice: they pick by their axes, and it plays out without a card.</summary>
        private static void PlayNpcBeat(EpisodeState s, StoryCycle cycle, BeatTemplate beat, string anchor)
        {
            var ctx = new StoryContext(s, anchor);
            string optionId = beat.npc?.Invoke(ctx, cycle, beat.options);
            if (optionId == null)
            {
                var holder = s.Find(Headliner(cycle));
                var weighted = beat.options.Select(o => (o, w: o.ai == null ? 1.0 : Math.Max(0.01, o.ai(holder)))).ToList();
                double total = weighted.Sum(x => x.w);
                double roll = StoryRandom.Unit(s, cycle.Id + ":" + beat.id + ":npc:w" + s.week) * total;
                foreach (var (option, weight) in weighted)
                {
                    if (roll < weight) { optionId = option.id; break; }
                    roll -= weight;
                }
                optionId ??= beat.options.LastOrDefault()?.id;
            }
            var chosen = beat.Option(optionId);
            if (chosen == null) { EndCycle(s, cycle.record, "lost"); return; }
            var effects = chosen.effects.AsEnumerable();
            if (chosen.extra != null) effects = effects.Concat(chosen.extra(ctx, cycle) ?? Enumerable.Empty<Fx>());
            foreach (var effect in effects.Select(fx => ResolveFx(s, cycle, fx)).Where(e => e != null).ToList())
                ApplyStoryEffect(s, effect, cycle.record, null, cycle.Id + ":" + beat.id + ":" + chosen.id, 0, false);
            int sequence = s.nextSequence;
            // An NPC choice is theirs: its line reaches only the houseguests in it. A choice with no
            // outcome line - a beat that only decides which way the story goes - writes nothing, since
            // a line with no audience would be one everybody sees.
            var audience = cycle.record.cast.Select(r => r.contestantId).Where(id => id != s.playerId).Distinct().ToArray();
            if (chosen.outcome != null && audience.Length > 0)
                Log(s, StoryLog.Outcome, StoryText.Neutral(chosen.outcome), audience);
            AddStep(cycle.record, beat.id, chosen.id, StoryResults.Npc, s.week, chosen.outcome != null && audience.Length > 0 ? sequence : 0);
            AdvanceCycle(s, cycle, chosen.next, anchor);
        }

        // ---------------------------------------------------------------- answering

        /// <summary>
        /// ProgressStoryline: the player answers a story beat. The command carries the beat's event
        /// id in <c>targetId</c> and the option id in <c>secondTargetId</c>; a pick-a-person option
        /// carries the person's id in <c>text</c>. The label is never the payload.
        /// </summary>
        private static void ProgressStoryline(EpisodeState s, EpisodeCommand c)
        {
            Require(s.Find(s.playerId).status == ContestantStatus.Active,
                "Evicted players can follow the season but cannot influence it.");
            var item = s.houseEvents.FirstOrDefault(e => e.id == c.targetId && e.IsStory && !e.resolved);
            Require(item != null, "That moment has already passed.");
            int index = item.choices.FindIndex(x => x.optionId == c.secondTargetId);
            Require(index >= 0, "Choose one of this moment's options.");
            var choice = item.choices[index];
            Require(!choice.locked, choice.lockReason ?? "That option is not open to you.");
            string picked = null;
            if (choice.pickPerson)
            {
                picked = (c.text ?? string.Empty).Trim();
                Require(choice.eligibleIds.Contains(picked), "Choose one of the people this option names.");
                Require(s.Find(picked)?.status == ContestantStatus.Active, "They are no longer in the house.");
            }
            if (choice.costsAction)
            {
                // Under the week's windows (STRATEGY-LOOP-PLAN.md section 4) any open window has seats;
                // before them, actions belong to free time or campaigning.
                Require(WeekRulesOn(s) ? Window(s) != Windows.None : s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign,
                    "That takes a social action, and actions belong to free time or campaigning.");
                Require(SocialActionsSpent(s) < SocialActionBudget(s), "You have no actions left this week.");
            }
            ResolveStoryBeat(s, item, index, false, picked);
            if (choice.costsAction) SpendSocialAction(s);
            // An answer can win a play on the spot: the alliance found out, the word given.
            DecidePlays(s, null);
        }

        /// <summary>
        /// Resolves a beat, answered or lapsed: the check, the out-of-character price, a hidden
        /// hot-button, every effect, the record, and what comes next.
        /// </summary>
        private static void ResolveStoryBeat(EpisodeState s, HouseEventState item, int index, bool lapsed, string picked)
        {
            var cycleRecord = s.storylines.FirstOrDefault(x => x.id == item.cycleId);
            var template = StoryCatalog.Find(cycleRecord?.templateId);
            item.resolved = true;
            item.chosenIndex = index;
            if (index < 0 || index >= item.choices.Count || cycleRecord == null || template == null)
            {
                item.outcome = "It passed.";
                if (cycleRecord != null && StorylineStatus.Running(cycleRecord.status)) EndCycle(s, cycleRecord, "lost");
                return;
            }
            var cycle = new StoryCycle(cycleRecord, template);
            var choice = item.choices[index];
            var player = s.Find(s.playerId);
            string beatId = item.contentId.Substring(item.contentId.IndexOf(':') + 1);
            var option = template.Beat(beatId)?.Option(choice.optionId);

            string result = lapsed ? StoryResults.Lapsed : StoryResults.Plain;
            if (!lapsed && choice.checkBase >= 0)
            {
                int chance = StoryOdds.Chance(s, item, choice);
                double roll = StoryRandom.Unit(s, item.id + ":" + choice.optionId + ":check") * 100;
                result = roll < chance ? StoryResults.Success : StoryResults.Backfire;
            }

            if (!lapsed && choice.against.Count > 0 && Personality.Has(player, choice.against.ToArray()))
                Personality.AdjustStress(player, 1);

            // How the gains land with the person the option is aimed at. A hidden hot-button changes
            // only the payoff: the chance shown was exactly the chance used.
            int reception = 0;
            bool taughtHotButton = false;
            // Who the option is aimed at: its checked role, else the person the player named, else the headliner.
            var subject = s.Find(choice.subjectId ?? picked ?? item.involvedIds.FirstOrDefault());
            if (!lapsed && result != StoryResults.Backfire && subject != null && choice.approach != null)
            {
                var hidden = Lore.HiddenHotButton(s, subject.id, choice.approach);
                if (hidden != null)
                {
                    reception = -1;
                    taughtHotButton = Lore.Learn(s, hidden.id);
                }
                else reception = StoryOdds.VisibleReception(s, subject, choice.approach);
            }

            var effects = result == StoryResults.Backfire ? choice.backfire : choice.effects;
            string key = item.id + ":" + choice.optionId;
            var applied = new List<StoryEffectState>();
            foreach (var effect in effects.ToList())
            {
                var resolved = Substitute(effect, picked);
                ApplyStoryEffect(s, resolved, cycleRecord, subject?.id, key, reception, result == StoryResults.Backfire);
                applied.Add(resolved);
            }

            string outcome = result == StoryResults.Backfire ? option?.backfireOutcome ?? option?.outcome : option?.outcome;
            item.outcome = Trim(StoryText.Neutral(outcome) ?? choice.label, 2000);
            int sequence = s.nextSequence;
            var audience = new[] { s.playerId }.Concat(cycleRecord.cast.Select(r => r.contestantId)).Distinct().ToArray();
            Log(s, StoryLog.Outcome, item.title + ": " + (StoryText.Neutral(outcome) ?? choice.label), audience);
            AddStep(cycleRecord, beatId, choice.optionId, result, s.week, sequence);
            // Fallout: a beat the house gives a card to, marked when the player answers it. The line is
            // name-free - the card's faces come from the beat's own people - and it goes to the same
            // audience as the outcome it follows.
            string fallout = template.Beat(beatId)?.fallout;
            if (!lapsed && StoryLog.IsFallout(fallout))
                Log(s, fallout, item.title + ".", audience.Concat(item.involvedIds).Distinct().ToArray());
            if (taughtHotButton && subject != null)
                Log(s, "story-lore", "It worked, but something you said landed wrong. You learned what " + subject.name
                    + " can't stand.", s.playerId, subject.id);
            if (!lapsed && subject != null)
                Remember(s, s.playerId, subject.id, (item.title ?? "A moment") + ": " + choice.label + ".", true);
            // A play's step says what it changed (plan 30 §4); other arcs keep to their outcome line.
            if (template.play != null) Receipts(s, applied);

            string next = result == StoryResults.Backfire ? choice.nextOnBackfire ?? choice.next : choice.next;
            AdvanceCycle(s, cycle, next, CurrentAnchor(s));
        }

        private static StoryEffectState Substitute(StoryEffectState effect, string picked)
        {
            if (picked == null) return effect;
            var copy = effect.Clone();
            if (copy.fromId == StoryEffects.Picked) copy.fromId = picked;
            if (copy.toId == StoryEffects.Picked) copy.toId = picked;
            if (copy.thirdId == StoryEffects.Picked) copy.thirdId = picked;
            return copy;
        }

        private static void AddStep(StorylineState cycle, string beatId, string optionId, string result, int week, int sequence)
        {
            if (cycle.path.Count >= 24) cycle.path.RemoveAt(0);
            cycle.path.Add(new StoryStepState { beatId = beatId, optionId = optionId, result = result, week = week, logSequence = sequence });
        }

        /// <summary>An option's next that hands the cycle to its pulse instead of a scheduled beat.</summary>
        public const string Waits = "wait";

        /// <summary>Moves a cycle on to its next beat, or ends it.</summary>
        private static void AdvanceCycle(EpisodeState s, StoryCycle cycle, string next, string anchor)
        {
            if (!StorylineStatus.Running(cycle.record.status)) return;
            if (string.IsNullOrEmpty(next) || next.StartsWith("end:", StringComparison.Ordinal))
            {
                EndCycle(s, cycle.record, string.IsNullOrEmpty(next) ? "done" : next.Substring(4));
                return;
            }
            if (next == Waits)
            {
                // Nothing scheduled: the arc's pulse decides what happens next, at a later anchor.
                cycle.record.nextAnchor = null;
                cycle.record.nextWeek = 0;
                return;
            }
            var beat = cycle.template.Beat(next);
            if (beat == null) { EndCycle(s, cycle.record, "lost"); return; }
            cycle.record.beatId = beat.id;
            if (string.IsNullOrEmpty(beat.anchor))
            {
                // No anchor: it follows at once, in the same moment - and it is the same ask only
                // once the player has answered part of it. After a houseguest's own beat (the tone
                // of a confrontation) the first card is a new ask, and the week's budget holds.
                bool sameAsk = cycle.record.path.Any(step => step.result != StoryResults.Npc);
                Fire(s, cycle, beat, anchor ?? CurrentAnchor(s), true, chained: sameAsk);
                return;
            }
            cycle.record.nextAnchor = beat.anchor;
            cycle.record.nextWeek = beat.anchor == StoryAnchors.Conversation || LaterThisWeek(anchor, beat.anchor)
                ? s.week : s.week + 1;
        }

        /// <summary>
        /// Ends a cycle and starts its cooldowns: the template's, a pair's, and a week's rest for
        /// the headliner. Any beat of it still open lapses first.
        /// </summary>
        internal static void EndCycle(EpisodeState s, StorylineState cycle, string ending)
        {
            if (!StorylineStatus.Running(cycle.status)) return;
            cycle.status = StorylineStatus.Completed;
            cycle.endedWeek = s.week;
            cycle.endingId = Trim(ending, 80);
            cycle.nextAnchor = null;
            cycle.nextWeek = 0;
            foreach (var open in s.houseEvents.Where(e => e.cycleId == cycle.id && !e.resolved).ToList())
            {
                open.resolved = true;
                open.chosenIndex = -1;
                open.outcome = "It passed.";
            }
            var template = StoryCatalog.Find(cycle.templateId);
            if (template == null) return;
            // A play turned down for the first time may come round again next week (plan 30 D2).
            Cool(s, "tpl:" + template.id, s.week + (FirstRefusal(s, cycle, template, ending) ? 1 : template.cooldownWeeks));
            if (template.group != null && template.groupCooldownWeeks > 0)
                Cool(s, "grp:" + template.group, s.week + template.groupCooldownWeeks);
            var ids = cycle.cast.Select(r => r.contestantId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    Cool(s, "pair:" + ids[i] + "|" + ids[j], s.week + template.pairCooldownWeeks);
            var headliner = cycle.cast.FirstOrDefault()?.contestantId;
            if (headliner != null) Cool(s, "star:" + headliner, s.week + 1);
        }

        /// <summary>
        /// A cycle whose required part has left the house ends with the authored "left-house".
        /// Effects already written stay; its open beat simply passes.
        /// </summary>
        private static void EndDepartedCycles(EpisodeState s)
        {
            foreach (var cycle in RunningCycles(s).ToList())
            {
                var template = StoryCatalog.Find(cycle.templateId);
                if (template == null) { EndCycle(s, cycle, "lost"); continue; }
                bool gone = cycle.cast.Any(r =>
                {
                    var role = template.roles.FirstOrDefault(x => x.key == r.role);
                    return (role == null || (!role.optional && !role.departed)) && s.Find(r.contestantId)?.status != ContestantStatus.Active;
                });
                if (gone || s.Find(s.playerId)?.status != ContestantStatus.Active) EndCycle(s, cycle, "left-house");
            }
        }

        // ---------------------------------------------------------------- cooldowns

        private static bool Cooling(EpisodeState s, string key) =>
            s.story.cooldowns.Any(c => c.key == key && c.untilWeek > s.week);

        private static void Cool(EpisodeState s, string key, int untilWeek)
        {
            key = Trim(key, 200);
            untilWeek = Math.Max(1, Math.Min(s.week + 100, untilWeek));
            var existing = s.story.cooldowns.FirstOrDefault(c => c.key == key);
            if (existing != null) { existing.untilWeek = Math.Max(existing.untilWeek, untilWeek); return; }
            if (s.story.cooldowns.Count >= 256) s.story.cooldowns.RemoveAll(c => c.untilWeek <= s.week);
            if (s.story.cooldowns.Count >= 256) return;
            s.story.cooldowns.Add(new StoryCooldownState { key = key, untilWeek = untilWeek });
        }
    }
}
