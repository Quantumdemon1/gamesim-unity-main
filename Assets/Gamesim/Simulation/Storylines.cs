using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Storylines: house events that remember.
    ///
    /// <para>Ported from <c>storyline-fallbacks.ts</c>. A storyline differs from an ordinary
    /// situation in three ways, and all three are why it needed state of its own: it belongs to a
    /// category, it will not come round again for some weeks after it ends, and the choice leaves a
    /// <see cref="StoryModifierState"/> behind that outlives the conversation.</para>
    ///
    /// <para><b>The AI route is not ported, for the third time and the same reason.</b> The
    /// reference generates storylines through an edge function and falls back to these templates
    /// when it cannot. A save whose content came from a network call cannot be replayed, and every
    /// season here reproduces from its seed. The fallback is the deterministic half, so the fallback
    /// is what was ported — exactly as with the house events.</para>
    ///
    /// <para>What is <b>not</b> ported from the fallback either: multi-chapter arcs. Every template
    /// the reference ships offline has exactly one chapter, so a chapter index would be a field
    /// that is always zero. <c>branching-story-system.ts</c> is where the branching lives and it is
    /// AI-driven; porting a chapter counter to serve nothing would be inventing structure.</para>
    /// </summary>
    public static class Storylines
    {
        /// <summary>How many storylines can be running at once. The reference's number.</summary>
        public const int MostAtOnce = 2;

        /// <summary>How long a storyline nobody picked up waits before it is left behind.</summary>
        public const int StaleWeeks = 3;

        /// <summary>What a storyline is about.</summary>
        public const string PowerPlay = "power_play", SocialDrama = "social_drama", Survival = "survival";

        /// <summary>One storyline, with its roles and its consequences still in role terms.</summary>
        public sealed class Template
        {
            public string id, category, title, chapterTitle, narrative;
            /// <summary>Further phrasings of the chapter, by week; the mechanics never vary.</summary>
            public string[] alternatives;
            public string[] roles;
            public int minimumWeek, cooldownWeeks;
            public Option[] options;
        }

        /// <summary>One way of answering, and what it leaves behind.</summary>
        public sealed class Option
        {
            public string label, description, risk;
            public double trust;
            public (string role, double amount)[] moves;
            public Modifier modifier;
        }

        /// <summary>The mark a choice leaves on the weeks after it.</summary>
        public sealed class Modifier
        {
            public string id, name, description;
            public int weeks;
            public double competition, social;
        }

        /// <summary>The three the reference ships offline, word for word and number for number.</summary>
        public static readonly Template[] Catalog =
        {
            new Template
            {
                id = "fallback_power_play", category = PowerPlay,
                title = "The Power Struggle", chapterTitle = "Challenge Issued",
                narrative = "{RIVAL} has been talking behind your back, rallying votes against you. "
                    + "The house is starting to take sides. You need to decide how to respond before "
                    + "it is too late.",
                alternatives = new[]
                {
                    "{RIVAL} is running a campaign against you - quiet conversations, one housemate at a time - and it is working. If you are going to answer it, this is the week.",
                    "The word is that {RIVAL} wants you gone and has been counting the votes to do it. People are watching to see what you do about that.",
                },
                roles = new[] { HouseEvents.Rival },
                minimumWeek = 2, cooldownWeeks = 4,
                options = new[]
                {
                    new Option
                    {
                        label = "Confront them publicly",
                        description = "Call them out in front of everyone to show strength.",
                        risk = HouseEventRisk.High, trust = -3,
                        moves = new[] { (HouseEvents.Rival, -15d) },
                        modifier = new Modifier
                        {
                            id = "power_move", name = "Power Move",
                            description = "You showed the house you will not be pushed around.",
                            weeks = 2, competition = 2,
                        },
                    },
                    new Option
                    {
                        label = "Build a counter-alliance",
                        description = "Quietly gather allies to protect yourself.",
                        risk = HouseEventRisk.Medium, trust = 2,
                        moves = new[] { (HouseEvents.Rival, -5d) },
                        modifier = new Modifier
                        {
                            id = "alliance_builder", name = "Alliance Builder",
                            description = "Your social game is strengthening.",
                            weeks = 2, social = 10,
                        },
                    },
                    new Option
                    {
                        label = "Ignore it completely",
                        description = "Rise above the drama and focus on competitions.",
                        risk = HouseEventRisk.Low, trust = 3,
                        moves = new (string, double)[0],
                    },
                },
            },
            new Template
            {
                id = "fallback_social_drama", category = SocialDrama,
                title = "Broken Trust", chapterTitle = "The Revelation",
                narrative = "You overhear {ALLY} sharing details of your strategy with another group. "
                    + "They do not know you heard everything. The betrayal stings, but how you handle "
                    + "it could define your game.",
                alternatives = new[]
                {
                    "Through the wall you hear {ALLY} explaining your plan - your plan - to people who were never meant to hear it. They walk out smiling, unaware you were on the other side.",
                    "{ALLY} thought the yard was empty when they laid out your strategy to another group. It was not. What you do with what you heard is now the whole question.",
                },
                roles = new[] { HouseEvents.Ally },
                minimumWeek = 1, cooldownWeeks = 4,
                options = new[]
                {
                    new Option
                    {
                        label = "Confront them privately",
                        description = "Say it to their face, with nobody else in the room.",
                        risk = HouseEventRisk.Low, trust = 3,
                        moves = new[] { (HouseEvents.Ally, -8d) },
                    },
                    new Option
                    {
                        label = "Use it against them",
                        description = "Say nothing, and spend what you know when it is worth most.",
                        risk = HouseEventRisk.High, trust = -5,
                        moves = new[] { (HouseEvents.Ally, -18d) },
                        modifier = new Modifier
                        {
                            id = "counter_intel", name = "Counter Intelligence",
                            description = "You know something they do not know you know.",
                            weeks = 2, competition = 1, social = -5,
                        },
                    },
                },
            },
            new Template
            {
                id = "fallback_survival", category = Survival,
                title = "Back Against the Wall", chapterTitle = "The Last Stand",
                narrative = "{HOH} has made it clear — you are the target this week. The votes are "
                    + "stacking up against you. You have one last chance to flip the house before "
                    + "eviction night.",
                alternatives = new[]
                {
                    "{HOH} is not hiding it: you are the one they want out, and the count already leans their way. There is a little time left to change some minds.",
                    "Everyone knows what {HOH} wants this week, and it is you on the block and out the door. The numbers are against you, and the clock is short.",
                },
                roles = new[] { HouseEvents.Hoh },
                minimumWeek = 1, cooldownWeeks = 5,
                options = new[]
                {
                    new Option
                    {
                        label = "Make a desperate deal",
                        description = "Offer them whatever it takes to get through the week.",
                        risk = HouseEventRisk.Medium, trust = -2,
                        moves = new[] { (HouseEvents.Hoh, 8d) },
                        modifier = new Modifier
                        {
                            id = "deal_maker", name = "Deal Maker",
                            description = "You bought yourself a week, and everybody knows it.",
                            weeks = 2, social = 5,
                        },
                    },
                    new Option
                    {
                        label = "Win the veto",
                        description = "Stop negotiating and go and take it off the table yourself.",
                        risk = HouseEventRisk.High, trust = 0,
                        moves = new (string, double)[0],
                        modifier = new Modifier
                        {
                            id = "clutch_performer", name = "Clutch Performer",
                            description = "Your back is against the wall and it suits you.",
                            weeks = 1, competition = 3,
                        },
                    },
                    new Option
                    {
                        label = "Accept your fate gracefully",
                        description = "Stop fighting it, and be remembered for how you went out.",
                        risk = HouseEventRisk.Low, trust = 5,
                        moves = new[] { (HouseEvents.Hoh, 5d) },
                    },
                },
            },
        };

        // ---------------------------------------------------------------- starting one

        /// <summary>Whether the season is in a position to begin a story.</summary>
        public static bool Ready(EpisodeState state) =>
            state != null
            && state.week >= state.storyRulesStartWeek
            && state.storylines.Count(x => StorylineStatus.Running(x.status)) < MostAtOnce
            && state.Find(state.playerId)?.status == ContestantStatus.Active
            && state.Active.Count(c => !c.isPlayer) >= 2;

        /// <summary>
        /// Whether a template may come round again.
        ///
        /// <para>A storyline that is running cannot start twice, and one that has ended waits out
        /// its cooldown from the week it ended rather than the week it began — otherwise a long
        /// story would be eligible again before it had finished being told.</para>
        /// </summary>
        public static bool Available(EpisodeState state, Template template)
        {
            if (state == null || template == null) return false;
            if (state.week < template.minimumWeek) return false;
            foreach (var past in state.storylines.Where(x => x.templateId == template.id))
            {
                if (StorylineStatus.Running(past.status)) return false;
                if (state.week - past.endedWeek < template.cooldownWeeks) return false;
            }
            return true;
        }

        /// <summary>
        /// Begins a story, or nothing.
        ///
        /// <para>Returns the chapter as a house event alongside the record, because a chapter is a
        /// situation with choices and there is no reason to model that twice. The caller adds both
        /// or neither — a record whose event went missing would be a story nobody can answer.</para>
        /// </summary>
        public static bool Begin(EpisodeState state, double roll, long sequence,
            out StorylineState story, out HouseEventState chapter)
        {
            story = null;
            chapter = null;
            if (!Ready(state)) return false;

            var roles = HouseEvents.Roles(state);
            if (roles.Count == 0) return false;

            var available = Catalog
                .Where(t => Available(state, t) && t.roles.All(roles.ContainsKey))
                .ToList();
            if (available.Count == 0) return false;

            int index = (int)(roll * available.Count);
            var template = available[index >= available.Count ? available.Count - 1 : index];

            chapter = new HouseEventState
            {
                id = "house-event-" + sequence,
                kind = HouseEventKind.House,
                title = template.chapterTitle,
                narrative = HouseEvents.Fill(state, HouseEvents.Narrative(template.narrative, template.alternatives, state.week), roles),
                involvedIds = template.roles.Select(r => roles[r]).Distinct(StringComparer.Ordinal).ToList(),
                week = state.week,
                resolved = false,
                chosenIndex = -1,
                choices = template.options.Select(option => new HouseEventChoice
                {
                    label = option.label,
                    description = option.description,
                    risk = option.risk,
                    trustChange = option.trust,
                    impacts = option.moves
                        .Where(m => roles.ContainsKey(m.role))
                        .Select(m => new HouseEventImpact { targetId = roles[m.role], amount = m.amount })
                        .ToList(),
                }).ToList(),
            };

            story = new StorylineState
            {
                id = "story-" + sequence,
                templateId = template.id,
                category = template.category,
                title = template.title,
                eventId = chapter.id,
                status = StorylineStatus.Active,
                week = state.week,
                endedWeek = 0,
            };
            return true;
        }

        /// <summary>The template a record came from, or null where a save carries one this build lost.</summary>
        public static Template Find(string templateId) =>
            Catalog.FirstOrDefault(t => t.id == templateId);

        /// <summary>The story waiting on a particular house event, if any.</summary>
        public static StorylineState For(EpisodeState state, string eventId) =>
            state?.storylines.FirstOrDefault(x => x.eventId == eventId && StorylineStatus.Running(x.status));

        // ---------------------------------------------------------------- what they leave behind

        /// <summary>
        /// A modifier's competition bonus, summed across everything still running.
        ///
        /// <para>Read where the player's own competition score is worked out, beside the study
        /// bonus that already lives there. A modifier nothing consumed would be the shape this port
        /// keeps finding.</para>
        /// </summary>
        public static double CompetitionBonus(EpisodeState state) =>
            state?.activeModifiers.Where(m => m.weeksLeft > 0).Sum(m => m.competitionBonus) ?? 0;

        /// <summary>
        /// The interactions a player's modifiers are worth this week, which can be negative.
        ///
        /// <para>Converted rather than spent raw — see <see cref="StoryModifiers.PointsPerAction"/>
        /// for why five of the reference's points are not five conversations here.</para>
        /// </summary>
        public static int SocialActions(EpisodeState state) =>
            state == null ? 0
                : StoryModifiers.ActionsFrom(state.activeModifiers.Where(m => m.weeksLeft > 0).Sum(m => m.socialBonus));

        /// <summary>
        /// Ages every modifier by a week and drops the ones that have run out.
        ///
        /// <para>Called once where the week turns. A modifier with a duration that nothing counted
        /// down would last the rest of the season, which is the opposite of what a duration is.
        /// </para>
        /// </summary>
        public static void AgeModifiers(EpisodeState state)
        {
            if (state == null) return;
            foreach (var modifier in state.activeModifiers) modifier.weeksLeft--;
            state.activeModifiers.RemoveAll(m => m.weeksLeft <= 0);
        }

        /// <summary>
        /// Gives up on stories the player has left alone too long.
        ///
        /// <para>Abandoned rather than completed, because a thread nobody picked up did not finish —
        /// and the cooldown should know the difference.</para>
        /// </summary>
        public static void AbandonStale(EpisodeState state)
        {
            if (state == null) return;
            foreach (var story in state.storylines.Where(x => StorylineStatus.Running(x.status)).ToList())
            {
                if (state.week - story.week < StaleWeeks) continue;
                var chapter = state.houseEvents.FirstOrDefault(e => e.id == story.eventId);
                if (chapter != null && chapter.resolved) continue;
                story.status = StorylineStatus.Abandoned;
                story.endedWeek = state.week;
            }
        }
    }
}
