using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The legacy producers, ported as one-beat arcs. Past the story boundary the weekly
    /// <c>BeginStoryline</c>/<c>OfferHouseEvent</c> slot does not run; its catalogue plays here
    /// instead, word for word and number for number, sharing the story system's airtime rather than
    /// holding a slot of its own. The six house events and the three storylines are generated from
    /// the legacy catalogues themselves, so there is one copy of their text and numbers.
    /// </summary>
    public static partial class StoryCatalog
    {
        /// <summary>The arcs a walk-in can become, in the order the engine tries them.</summary>
        public static readonly string[] ProximityArcs = { "kitchen-blowup", "walked-in-arguing", "walked-in-whispering" };

        private static IEnumerable<ArcTemplate> Ported()
        {
            foreach (var template in HouseEvents.Catalog) yield return PortedHouseEvent(template);
            foreach (var template in Storylines.Catalog) yield return PortedStoryline(template);
            yield return Emergent("emergent-rivalry", "rivalry", "This has gone far enough",
                "Whatever is between you and {THEM} has stopped being private. The house has started arranging itself around it.",
                "Have it out in the open", "Say it where everybody can hear.", -12, -4, 5);
            yield return Emergent("emergent-friendship", "friendship", "Everyone has noticed",
                "You and {THEM} have become the pair everybody expects to see together. That is a target as much as it is an ally.",
                "Lean into it", "Stop pretending you are not working together.", 12, 2, -5);
            yield return UnspokenPair();
            yield return CaughtEavesdropping();
            yield return WalkedIn("walked-in-arguing", false);
            yield return WalkedIn("walked-in-whispering", true);
        }

        /// <summary>A legacy role placeholder, "{ALLY}", as a story role key, "ALLY".</summary>
        private static string RoleKey(string placeholder) => placeholder.Trim('{', '}');

        /// <summary>
        /// The legacy roles for this week: the player's warmest and coldest, the Head of Household, a
        /// nominee. The situation needs its own; a move may also reach a role the situation never
        /// names - the legacy events moved whoever the week cast - so those are cast when they resolve.
        /// </summary>
        private static ArcBinding LegacyRoles(StoryContext c, string[] placeholders, IEnumerable<string> moved)
        {
            var roles = HouseEvents.Roles(c.state);
            if (roles.Count == 0 || !placeholders.All(roles.ContainsKey)) return null;
            // At eviction night the block still names the evictee, who has gone: under the reach rules
            // the situation is about the nominee still in the house, or does not come round.
            if (c.AtLeast(StoryRules.Reach) && roles.TryGetValue(HouseEvents.Nominee, out var named)
                && c.Find(named)?.status != ContestantStatus.Active && c.state.nominees.Contains(named))
            {
                string stayed = c.state.nominees.FirstOrDefault(id => c.Find(id) is ContestantState x && !x.isPlayer && x.status == ContestantStatus.Active);
                if (stayed == null) return null;
                roles[HouseEvents.Nominee] = stayed;
            }
            var binding = Bind();
            foreach (var placeholder in placeholders.Concat(moved).Distinct())
                if (roles.TryGetValue(placeholder, out var id)) binding.With(RoleKey(placeholder), id);
            return binding.Headlining(roles[placeholders[0]]);
        }

        /// <summary>A legacy template's parts: the ones it names, then any its options move, optionally.</summary>
        private static ArcRole[] LegacyParts(string[] placeholders, IEnumerable<string> moved) =>
            placeholders.Select(r => Role(RoleKey(r)))
                .Concat(moved.Except(placeholders).Distinct().Select(r => Optional(RoleKey(r)))).ToArray();

        private static OptionTemplate PortedOption(string id, string label, string description, string risk, double trust,
            IEnumerable<(string role, double amount)> moves, string[] placeholders, Fx extra = null)
        {
            var effects = moves.Select(m => Move(Player, RoleKey(m.role), m.amount)).ToList();
            // The legacy trust mark: every houseguest in the situation, both ways on the ledger.
            if (Math.Abs(trust) > 0.001) effects.AddRange(placeholders.Select(p => Trust(RoleKey(p), Player, trust, label)));
            if (extra != null) effects.Add(extra);
            var option = Opt(id, label, description, label + ".", effects.ToArray());
            option.risk = HouseEventRisk.IsKnown(risk) ? risk : Low;
            return option;
        }

        /// <summary>
        /// The legacy situations waited for ever; a story beat lapses when its window closes. Not
        /// answering is its own answer, and it moves nobody.
        /// </summary>
        private static OptionTemplate LetItPass() =>
            Lapse("let-it-pass", "Let it pass", "Do nothing about it.", "You let it pass.");

        private static ArcTemplate PortedHouseEvent(HouseEvents.Template t)
        {
            var options = t.options.Select((o, i) => PortedOption("option-" + (i + 1), o.label, o.description, o.risk, o.trust, o.moves, t.roles))
                .Concat(new[] { LetItPass() }).ToArray();
            const string lapse = "let-it-pass";
            var moved = t.options.SelectMany(o => o.moves.Select(m => m.role)).ToList();
            return new ArcTemplate
            {
                id = "legacy-" + t.id, lane = StoryLanes.Moment, eyebrow = "In the House", title = t.title,
                origin = "web:src/systems/house-event-system.ts (fallback templates), via HouseEvents.Catalog",
                rulesVersion = StoryRules.Spine, startAnchors = new[] { StoryAnchors.EvictionNight },
                oncePerHeadliner = false, cooldownWeeks = 3,
                roles = LegacyParts(t.roles, moved),
                cast = c => LegacyRoles(c, t.roles, moved),
                weight = (c, b) => 2.5,
                beats = new[]
                {
                    new BeatTemplate
                    {
                        id = "situation", surface = StorySurfaces.Scene, title = t.title, text = t.narrative,
                        alternates = t.alternatives ?? Array.Empty<string>(),
                        summary = StoryText.Neutral(t.narrative), options = options, lapse = lapse,
                    },
                },
            };
        }

        private static ArcTemplate PortedStoryline(Storylines.Template t)
        {
            var options = t.options.Select((o, i) => PortedOption("option-" + (i + 1), o.label, o.description, o.risk, o.trust, o.moves, t.roles,
                o.modifier == null ? null : Modifier(Player, o.modifier.id, o.modifier.weeks)))
                .Concat(new[] { LetItPass() }).ToArray();
            const string lapse = "let-it-pass";
            var moved = t.options.SelectMany(o => o.moves.Select(m => m.role)).ToList();
            return new ArcTemplate
            {
                id = "legacy-" + t.id, lane = StoryLanes.Moment, eyebrow = t.title, title = t.title,
                origin = "web:src/systems/storyline-fallbacks.ts, via Storylines.Catalog",
                rulesVersion = StoryRules.Spine, minWeek = Math.Max(1, t.minimumWeek), cooldownWeeks = Math.Max(1, t.cooldownWeeks),
                startAnchors = new[] { StoryAnchors.EvictionNight }, oncePerHeadliner = false,
                roles = LegacyParts(t.roles, moved),
                cast = c => LegacyRoles(c, t.roles, moved),
                weight = (c, b) => 4,
                beats = new[]
                {
                    new BeatTemplate
                    {
                        id = "chapter", surface = StorySurfaces.Approach, title = t.chapterTitle, text = t.narrative,
                        alternates = t.alternatives ?? Array.Empty<string>(),
                        summary = StoryText.Neutral(t.narrative), options = options, lapse = lapse,
                    },
                },
            };
        }

        // ---------------------------------------------------------------- emergent

        /// <summary>
        /// A relationship arc the house has watched escalate (escalation 3 or more), as the legacy
        /// emergent source reads it: the arc says what it is, whatever the current score.
        /// </summary>
        private static ArcTemplate Emergent(string id, string arcType, string title, string text, string openLabel,
            string openDescription, double open, double openTrust, double cool) => new ArcTemplate
        {
            id = id, lane = StoryLanes.Moment, eyebrow = "In the House", title = title,
            origin = "native: HouseEventSources.Emergent (the reference's emergent arc trigger)",
            // From the reach rules the campaign's free time is a second chance a week (weight below).
            rulesVersion = StoryRules.Spine, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet }, cooldownWeeks = 3,
            oncePerHeadliner = false,
            roles = new[] { Role("THEM") },
            cast = c =>
            {
                var arc = c.state.relationshipArcs
                    .Where(a => a.escalationLevel >= HouseEventSources.EscalatedArc && a.arcType == arcType
                                && c.Find(a.npcId)?.status == ContestantStatus.Active)
                    .OrderByDescending(a => a.escalationLevel).ThenByDescending(a => a.intensity)
                    .ThenBy(a => a.npcId, StringComparer.Ordinal).FirstOrDefault();
                return arc == null ? null : Bind().With("THEM", arc.npcId).Headlining(arc.npcId);
            },
            weight = (c, b) => c.anchor == StoryAnchors.BlockSet && !c.AtLeast(StoryRules.Reach) ? 0 : 20,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "noticed", surface = StorySurfaces.Scene, title = title, text = text, summary = StoryText.Neutral(text),
                    lapse = "cool-it",
                    options = new[]
                    {
                        Opt("open", openLabel, High, arcType == "rivalry" ? Bold : Warm, openDescription, openLabel + ".",
                            Move(Player, "THEM", open), Trust("THEM", Player, openTrust, openLabel)),
                        Opt("cool-it", "Cool it down", Low, Calculated, "Be seen with other people for a while.", "You cooled it down.",
                            Move(Player, "THEM", cool)),
                    },
                },
            },
        };

        private static ArcTemplate UnspokenPair() => new ArcTemplate
        {
            id = "unspoken-pair", lane = StoryLanes.Moment, eyebrow = "In the House", title = "Nobody has said it out loud",
            origin = "native: HouseEventSources.Emergent (the reference's unspoken-pair trigger)",
            rulesVersion = StoryRules.Spine, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet }, cooldownWeeks = 3,
            roles = new[] { Role("THEM") },
            cast = c =>
            {
                // Sixty was a skilled player's closest friend in one season in ten; under the reach
                // rules forty-five, still somebody the player has spent the season with.
                double warmth = c.AtLeast(StoryRules.Reach) ? 45 : HouseEventSources.UnspokenPairWarmth;
                string them = Warmest(c, x => c.Score(P(c), x.id) >= warmth && !c.state.Allied(P(c), x.id));
                return them == null ? null : Bind().With("THEM", them).Headlining(them);
            },
            weight = (c, b) => c.anchor == StoryAnchors.BlockSet && !c.AtLeast(StoryRules.Reach) ? 0 : 15,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "unspoken", surface = StorySurfaces.Approach, title = "Nobody has said it out loud",
                    text = "{THEM} has been on your side for weeks without either of you calling it anything. That is either trust or a misunderstanding waiting to happen.",
                    summary = "A houseguest has been on your side for weeks without either of you calling it anything.",
                    lapse = "leave-it",
                    options = new[]
                    {
                        Opt("say-it", "Say it out loud", Medium, Candid, "Put a name to what you already have.", "You put a name to it.",
                            Move(Player, "THEM", 10), Trust("THEM", Player, 4, "Say it out loud")),
                        Opt("leave-it", "Leave it unspoken", Low, Calculated, "What is not agreed cannot be broken.", "You left it unspoken."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- crisis

        /// <summary>
        /// Caught listening in: the legacy crisis, which prefers the two parties to a live deal,
        /// because being caught listening to people who have actually agreed something is worse.
        /// </summary>
        private static ArcTemplate CaughtEavesdropping() => new ArcTemplate
        {
            id = "caught-eavesdropping", lane = StoryLanes.Moment, eyebrow = "In the House", title = "Caught Eavesdropping",
            origin = "native: HouseEventSources.Crisis (EpisodeEngine.CrisisChance 0.25)",
            rulesVersion = StoryRules.Spine, minWeek = HouseEventSources.CrisisFirstWeek, startAnchors = new[] { StoryAnchors.EvictionNight },
            cooldownWeeks = 2, oncePerHeadliner = false,
            roles = new[] { Role("FIRST"), Role("SECOND") },
            cast = c =>
            {
                var deal = c.state.deals.Where(d => d.status == DealStatus.Active && d.proposerId != P(c) && d.recipientId != P(c))
                    .OrderBy(d => d.id, StringComparer.Ordinal)
                    .FirstOrDefault(d => c.Find(d.proposerId)?.status == ContestantStatus.Active && c.Find(d.recipientId)?.status == ContestantStatus.Active);
                if (deal != null) return Bind().With("FIRST", deal.proposerId).With("SECOND", deal.recipientId).Headlining(deal.proposerId);
                var npcs = Npcs(c).OrderBy(x => x.id, StringComparer.Ordinal).ToList();
                if (npcs.Count < 2) return null;
                var first = Keyed(c, "crisis", npcs);
                var second = npcs[(npcs.IndexOf(first) + 1) % npcs.Count];
                return Bind().With("FIRST", first.id).With("SECOND", second.id).Headlining(first.id);
            },
            weight = (c, b) => Web(0.25),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "caught", surface = StorySurfaces.Scene, venue = Kitchen, title = "Caught Eavesdropping",
                    text = "You overhear {FIRST} and {SECOND} whispering about nominations. Just as you lean in closer, {FIRST} spots you.",
                    summary = "You were caught listening to two houseguests whispering about nominations.",
                    lapse = "apologise",
                    options = new[]
                    {
                        Opt("deny", "Deny everything", High, Calculated, "\"I was just grabbing cereal.\"", "You denied everything.",
                            Move(Player, "FIRST", -5), Trust("FIRST", Player, -2, "Deny everything"), Trust("SECOND", Player, -2, "Deny everything")),
                        Opt("own-it", "Own it", Medium, Candid, "\"I heard everything. Want to include me?\"", "You owned it.",
                            Move(Player, "FIRST", 2), Trust("FIRST", Player, 1, "Own it"), Trust("SECOND", Player, 1, "Own it")),
                        Opt("apologise", "Apologise and leave", Low, Yield, "\"Sorry. I'll give you space.\"", "You apologised and left.",
                            Move(Player, "FIRST", -1)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- proximity

        /// <summary>
        /// Walking in on two houseguests: the legacy proximity situation, cast by the engine with
        /// the two people the player actually found. Close pairs stop talking; others are mid-row.
        /// </summary>
        private static ArcTemplate WalkedIn(string id, bool close) => new ArcTemplate
        {
            id = id, lane = StoryLanes.Moment, eyebrow = "In the House", title = "You walk in on something",
            origin = "native: HouseEventSources.Proximity (the director offers who is standing where)",
            rulesVersion = StoryRules.Spine, oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1,
            roles = new[] { Role("FIRST"), Role("SECOND") },
            cast = c => null,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "walk-in", surface = StorySurfaces.Scene, title = "You walk in on something",
                    text = close
                        ? "{FIRST} and {SECOND} are deep in conversation, and they stop talking the moment they notice you."
                        : "{FIRST} and {SECOND} are mid-argument. Neither of them looks pleased to see you.",
                    summary = close ? "Two houseguests stop talking the moment they notice you." : "Two houseguests are mid-argument.",
                    lapse = "back-out",
                    options = new[]
                    {
                        Opt("join", "Join them", Medium, close ? Bold : Warm, "Walk in as though you were always going to.", "You walked in as though you were always going to.",
                            Move(Player, "FIRST", close ? -4 : 6), Move(Player, "SECOND", close ? -4 : 6)),
                        Opt("back-out", "Back out quietly", Low, Calculated, "Pretend you saw nothing.", "You backed out quietly."),
                        Opt("ask", "Ask what that was about", High, Candid, "Put them on the spot, now, in front of each other.", "You put them on the spot.",
                            Move(Player, "FIRST", -8), Move(Player, "SECOND", close ? -8 : 4),
                            Trust("FIRST", Player, -3, "Ask what that was about"), Trust("SECOND", Player, -3, "Ask what that was about")),
                    },
                },
            },
        };

        /// <summary>
        /// How a walk-in casts each arc it can become: a real grudge or bad blood between the two is
        /// a kitchen blow-up, warmth of sixty or more is whispering, anything else an argument.
        /// Houseguests' warmth rarely passes thirty, so from the reach rules two allies, or two who
        /// like each other (twenty both ways), whisper too.
        /// </summary>
        public static ArcBinding ProximityCast(EpisodeState s, string arcId, string a, string b)
        {
            if (s == null || a == null || b == null || a == b) return null;
            bool close = s.Score(a, b) >= HouseEventSources.UnspokenPairWarmth
                         || (EpisodeEngine.StoryAt(s, StoryRules.Reach) && (s.Allied(a, b) || StoryPeople.Mutual(s, a, b) >= 20));
            switch (arcId)
            {
                case "kitchen-blowup":
                {
                    bool reason = Grudges.Severity(s, a, b) >= 40 || Grudges.Severity(s, b, a) >= 40 || StoryPeople.Mutual(s, a, b) <= -10;
                    if (!reason) return null;
                    bool aHot = Personality.Volatility(s, a, b) >= Personality.Volatility(s, b, a);
                    string hothead = aHot ? a : b, target = aHot ? b : a;
                    return Bind().With("HOTHEAD", hothead).With("TARGET", target).Headlining(hothead);
                }
                case "walked-in-arguing":
                    return close ? null : Bind().With("FIRST", a).With("SECOND", b).Headlining(a);
                case "walked-in-whispering":
                    return close ? Bind().With("FIRST", a).With("SECOND", b).Headlining(a) : null;
                default:
                    return null;
            }
        }
    }
}
