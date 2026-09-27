using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Every arc the story system can tell, gathered from the content files one milestone at a time.
    ///
    /// <para>Append-only. An open beat in a save finds its template here by id, so an arc that is
    /// retired stays in the list with no start anchors rather than being deleted.</para>
    /// </summary>
    public static partial class StoryCatalog
    {
        private static List<ArcTemplate> all;

        /// <summary>The whole catalogue, built once.</summary>
        public static IReadOnlyList<ArcTemplate> All => all ?? (all = Build());

        private static List<ArcTemplate> Build()
        {
            var list = new List<ArcTemplate>();
            list.AddRange(Spine());
            list.AddRange(PhaseBeats());
            list.AddRange(Ported());
            list.AddRange(HouseRemembers());
            list.AddRange(Staging());
            list.AddRange(KnowThem());
            list.AddRange(BondsAndSecrets());
            list.AddRange(ProductionArcs());
            return list;
        }

        /// <summary>An arc by id, or null.</summary>
        public static ArcTemplate Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var list = All;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) return list[i];
            return null;
        }

        // ---------------------------------------------------------------- building helpers
        //
        // Small constructors so a content file reads as content. Every helper returns a fresh
        // object; the catalogue is data and nothing mutates it after Build.

        internal static ArcBinding Bind() => new ArcBinding();

        internal static OptionTemplate Opt(string id, string label, string description, string outcome,
            params Fx[] effects) =>
            new OptionTemplate { id = id, label = label, description = description, outcome = outcome, effects = effects ?? Array.Empty<Fx>() };

        internal static OptionTemplate Lapse(string id, string label, string description, string outcome, params Fx[] effects)
        {
            var option = Opt(id, label, description, outcome, effects);
            option.risk = HouseEventRisk.Low;
            return option;
        }

        /// <summary>An option with a risk tag and an approach, the two things almost every option sets.</summary>
        internal static OptionTemplate Opt(string id, string label, string risk, string approach, string description,
            string outcome, params Fx[] effects)
        {
            var option = Opt(id, label, description, outcome, effects);
            option.risk = risk;
            option.approach = approach;
            return option;
        }

        /// <summary>An option's next that hands the cycle to its pulse: nothing is scheduled, the pulse decides.</summary>
        internal const string Waits = EpisodeEngine.Waits;

        /// <summary>Sets where an option goes next: a beat id, or "end:reason".</summary>
        internal static OptionTemplate Then(this OptionTemplate option, string next, string nextOnBackfire = null)
        {
            option.next = next;
            option.nextOnBackfire = nextOnBackfire;
            return option;
        }

        /// <summary>Makes an option a check against a role, with what it costs when it backfires.</summary>
        internal static OptionTemplate Checked(this OptionTemplate option, double chance, string role, string backfireOutcome,
            params Fx[] backfire)
        {
            option.check = chance;
            option.checkRole = role;
            option.backfireOutcome = backfireOutcome;
            option.backfire = backfire ?? Array.Empty<Fx>();
            return option;
        }

        internal static OptionTemplate Bonus(this OptionTemplate option, params string[] traits) { option.bonusTraits = traits; return option; }
        internal static OptionTemplate Against(this OptionTemplate option, params string[] traits) { option.against = traits; return option; }
        internal static OptionTemplate LockUnless(this OptionTemplate option, params string[] traits) { option.lockUnless = traits; return option; }
        internal static OptionTemplate OnlyIf(this OptionTemplate option, params string[] traits) { option.onlyIf = traits; return option; }
        internal static OptionTemplate Glyph(this OptionTemplate option, string glyph) { option.glyph = glyph; return option; }
        internal static OptionTemplate CostsAction(this OptionTemplate option) { option.costsAction = true; return option; }
        internal static OptionTemplate Conduct(this OptionTemplate option) { option.conduct = true; return option; }
        internal static OptionTemplate Ai(this OptionTemplate option, Func<ContestantState, double> weight) { option.ai = weight; return option; }

        internal static OptionTemplate Needs(this OptionTemplate option, Func<StoryContext, StoryCycle, bool> needs, string label)
        {
            option.needs = needs;
            option.needsLabel = label;
            return option;
        }

        internal static OptionTemplate Extra(this OptionTemplate option, Func<StoryContext, StoryCycle, IEnumerable<Fx>> extra)
        {
            option.extra = extra;
            return option;
        }

        internal static OptionTemplate BackfireExtra(this OptionTemplate option, Func<StoryContext, StoryCycle, IEnumerable<Fx>> extra)
        {
            option.backfireExtra = extra;
            return option;
        }

        internal static OptionTemplate Picks(this OptionTemplate option, Func<StoryContext, StoryCycle, IEnumerable<string>> pick)
        {
            option.pick = pick;
            return option;
        }

        /// <summary>Only from a rules version on: the option is locked, with a plain reason, in a season before it.</summary>
        internal static OptionTemplate From(this OptionTemplate option, int version) =>
            option.Needs((c, _) => c.AtLeast(version), "Not in this season's rules");

        // ---------------------------------------------------------------- venues
        //
        // Where a beat is staged. A presentation hint the rules never read; the names match the
        // rooms the house builds.

        internal const string Kitchen = StoryVenues.Kitchen, Living = StoryVenues.Living, Yard = StoryVenues.Yard, Bedroom = StoryVenues.Bedroom;
        internal const string Storage = StoryVenues.Storage, HohRoom = StoryVenues.HohRoom, DiaryRoom = StoryVenues.DiaryRoom, Hallway = StoryVenues.Hallway;
        internal const string Bathroom = StoryVenues.Bathroom, Table = StoryVenues.Table;

        // ---------------------------------------------------------------- who
        //
        // Casting reads hidden state freely; the text never states it (20 §2.10). Every choice
        // between equals breaks on id, and every choice by chance is keyed on the week and anchor.

        internal static ArcRole Role(string key, StoryPeople.Sensitivity needs = StoryPeople.Sensitivity.Game) => new ArcRole(key, needs);
        internal static ArcRole Optional(string key, StoryPeople.Sensitivity needs = StoryPeople.Sensitivity.Game) => new ArcRole(key, needs, true);
        /// <summary>A part somebody who has just left the house may play: the evictee you say goodbye to.</summary>
        internal static ArcRole Departed(string key) => new ArcRole(key) { departed = true };

        internal static OptionTemplate ShowIf(this OptionTemplate option, Func<StoryContext, StoryCycle, bool> show) { option.showIf = show; return option; }

        internal static string P(StoryContext c) => c.state.playerId;
        internal static ContestantState PlayerOf(StoryContext c) => c.state.Find(c.state.playerId);
        internal static List<ContestantState> Npcs(StoryContext c) => StoryPeople.ActiveNpcs(c.state);
        internal static string NpcHoh(StoryContext c) => StoryPeople.NpcHoh(c.state)?.id;
        internal static bool PlayerIsHoh(StoryContext c) => c.state.hohId == c.state.playerId;
        internal static bool PlayerNominated(StoryContext c) => c.state.nominees.Contains(c.state.playerId);
        internal static bool PlayerVotes(StoryContext c) => EpisodeEngine.Voters(c.state).Any(v => v.isPlayer);
        internal static bool Nominated(StoryContext c, string id) => id != null && c.state.nominees.Contains(id);

        internal static List<ContestantState> NpcNominees(StoryContext c) =>
            c.state.nominees.Select(c.state.Find).Where(x => x != null && !x.isPlayer && x.status == ContestantStatus.Active).ToList();

        internal static string NpcVetoHolder(StoryContext c)
        {
            var holder = c.state.Find(c.state.vetoHolderId);
            return holder != null && !holder.isPlayer && holder.status == ContestantStatus.Active ? holder.id : null;
        }

        internal static string Warmest(StoryContext c, Func<ContestantState, bool> where = null) => StoryPeople.Warmest(c.state, where)?.id;
        internal static string Coldest(StoryContext c, Func<ContestantState, bool> where = null) => StoryPeople.Coldest(c.state, where)?.id;
        internal static double Mutual(StoryContext c, string a, string b) => StoryPeople.Mutual(c.state, a, b);

        /// <summary>One of a list by a keyed draw: the same week, anchor and purpose always pick the same one.</summary>
        internal static T Keyed<T>(StoryContext c, string purpose, IList<T> list) where T : class =>
            list == null || list.Count == 0 ? null
                : list[StoryRandom.Index(c.state, "w" + c.state.week + ":" + c.anchor + ":cast:" + purpose, list.Count)];

        /// <summary>Whether any of these people are the same.</summary>
        internal static bool Distinct(params string[] ids) =>
            ids.Where(id => id != null).Distinct(StringComparer.Ordinal).Count() == ids.Count(id => id != null);

        /// <summary>The nominee this week's eviction sent home, once it has.</summary>
        internal static string Evicted(StoryContext c) =>
            c.state.evictionResolved ? c.state.nominees.FirstOrDefault(id => c.state.Find(id)?.status != ContestantStatus.Active) : null;

        /// <summary>A houseguest's relationship arc with the player, if the player has one with them.</summary>
        internal static RelationshipArcState Arc(StoryContext c, string npcId) =>
            c.state.relationshipArcs.FirstOrDefault(a => a.npcId == npcId);

        /// <summary>
        /// The web's trigger chance as a pool weight. Every candidate at an anchor shares one draw
        /// with "nothing happens", so a weight is only ever relative: twenty times the web's chance
        /// keeps its phase beats in proportion to each other without crowding out the arcs.
        /// </summary>
        internal static double Web(double chance) => 20 * chance;

        internal static bool ActiveAtLeast(EpisodeState s, int count) => s.Active.Count() >= count;

        // ---------------------------------------------------------------- the web's branching stories
        //
        // branching-story-system.ts's five three-act stories share one shape: a discovery with three
        // choices, a deliberation for each (two choices), and an action for each of those six (two
        // choices). This builds that shape from the web's own tables, chained so the three acts play
        // as one conversation (a chained beat is not a new ask).

        internal sealed class Act
        {
            public string title, text;
            public OptionTemplate[] options;
        }

        /// <summary>
        /// A three-act story: the discovery, then deliberation n for discovery choice n, then action
        /// 2n+m for deliberation n's choice m - the web's <c>branchMap</c> and its flat index.
        /// </summary>
        internal static BeatTemplate[] ThreeActs(BeatTemplate discovery, Act[] deliberations, Act[] actions)
        {
            var beats = new List<BeatTemplate> { discovery };
            for (int i = 0; i < discovery.options.Length && i < deliberations.Length; i++)
                if (discovery.options[i].id != discovery.lapse) discovery.options[i].next = "deliberation-" + (i + 1);
            for (int d = 0; d < deliberations.Length; d++)
            {
                var act = deliberations[d];
                for (int m = 0; m < act.options.Length; m++) act.options[m].next = "action-" + (d * 2 + m + 1);
                beats.Add(ActBeat("deliberation-" + (d + 1), act, discovery));
            }
            for (int a = 0; a < actions.Length; a++)
            {
                foreach (var option in actions[a].options) option.next = option.next ?? "end:done";
                beats.Add(ActBeat("action-" + (a + 1), actions[a], discovery));
            }
            return beats.ToArray();
        }

        private static BeatTemplate ActBeat(string id, Act act, BeatTemplate discovery)
        {
            var options = act.options.Concat(new[]
            {
                Lapse("leave-it", "Leave it there", "Walk away before it goes any further.", "You left it there.").Then("end:left"),
            }).ToArray();
            return new BeatTemplate
            {
                id = id, anchor = null, surface = discovery.surface, venue = discovery.venue,
                title = act.title, summary = "The conversation goes on.", text = act.text,
                options = options, lapse = "leave-it",
            };
        }
    }
}
