using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Things that happen to the house.
    ///
    /// <para>Every week in this port so far happens <i>because</i> the player pressed something.
    /// Nothing arrives on its own, nobody walks in on anything, and no week is unlike the last
    /// except in who won what. The reference's event layer is what fixes that, and this is the part
    /// of it that asks the player a question.</para>
    ///
    /// <para>Ported from <c>house-event-system.ts</c>'s templates, which are written with role
    /// placeholders — <c>{ALLY}</c>, <c>{RIVAL}</c>, <c>{HOH}</c>, <c>{NOMINEE}</c> — and filled
    /// from the season's own state. That design is why they port at all: the situations are about
    /// positions rather than about people, so they mean something in any house.</para>
    ///
    /// <para><b>Drawn once a week, and only where the roles resolve.</b> A template that names the
    /// Head of Household is not worth showing before anybody holds it, and the reference falls back
    /// to a simpler one rather than printing a placeholder.</para>
    /// </summary>
    public static class HouseEvents
    {
        /// <summary>Who a template can be about.</summary>
        public const string Ally = "{ALLY}", Rival = "{RIVAL}", Hoh = "{HOH}", Nominee = "{NOMINEE}";

        /// <summary>
        /// The most events a season keeps. Bounded because validation bounds it, and because a
        /// hundred-week season should not end up with a list nothing will ever read.
        /// </summary>
        public const int Ceiling = 400;

        // ---------------------------------------------------------------- the templates

        /// <summary>One situation, with its roles left open.</summary>
        public sealed class Template
        {
            public string id, title, narrative;
            /// <summary>Further phrasings of the narrative, by week; the mechanics never vary.</summary>
            public string[] alternatives;
            public string[] roles;
            public Option[] options;
        }

        /// <summary>One way of answering, with its consequences still in role terms.</summary>
        public sealed class Option
        {
            public string label, description, risk;
            public double trust;
            public (string role, double amount)[] moves;
        }

        /// <summary>
        /// The six the reference ships, word for word and number for number.
        ///
        /// <para>They are the reference's <i>fallback</i> templates — its first choice is to ask a
        /// language model for a bespoke situation. That route is not ported and will not be: a save
        /// whose content depends on a network call is a save that cannot be replayed, and every
        /// season here has to reproduce exactly from its seed.</para>
        /// </summary>
        public static readonly Template[] Catalog =
        {
            new Template
            {
                id = "overheard-conversation", title = "Overheard Conversation",
                narrative = "You walk into the storage room and overhear {RIVAL} telling {ALLY} that "
                    + "you're their next target. They haven't noticed you yet.",
                alternatives = new[]
                {
                    "Passing the bathroom door you catch {RIVAL}'s voice, low and certain: you're the name they are putting up next, and {ALLY} is listening. Neither has seen you.",
                    "Late in the kitchen, {RIVAL} is laying out the week for {ALLY}, and the plan has your name at the top of it. You are standing just out of their eyeline.",
                },
                roles = new[] { Rival, Ally },
                options = new[]
                {
                    new Option { label = "Confront them", description = "Call them out directly in front of everyone",
                        risk = HouseEventRisk.Medium, trust = 3,
                        moves = new[] { (Rival, -12d), (Ally, 5d) } },
                    new Option { label = "Tell your alliance", description = "Report back to your allies about the plot",
                        risk = HouseEventRisk.Low, trust = 5,
                        moves = new[] { (Ally, 8d) } },
                    new Option { label = "Stay quiet", description = "Keep this intel to yourself for now",
                        risk = HouseEventRisk.Low, trust = 0,
                        moves = new (string, double)[0] },
                },
            },
            new Template
            {
                id = "secret-deal-offer", title = "Secret Deal Offer",
                narrative = "{RIVAL} pulls you aside after dinner with an unexpected offer. They want "
                    + "to form a secret side-deal to protect each other, but your alliance doesn't "
                    + "know about it.",
                alternatives = new[]
                {
                    "{RIVAL} finds you alone in the yard and keeps their voice down: a quiet arrangement, just the two of you, each keeping the other off the block. Your alliance would not be told.",
                    "{RIVAL} catches you on the stairs with a proposal they have clearly rehearsed - a private pact, safety for safety, kept from everyone you are already working with.",
                },
                roles = new[] { Rival },
                options = new[]
                {
                    new Option { label = "Accept secretly", description = "Take the deal but keep it hidden from your alliance",
                        risk = HouseEventRisk.High, trust = -5,
                        moves = new[] { (Rival, 15d) } },
                    new Option { label = "Decline politely", description = "Turn them down without making enemies",
                        risk = HouseEventRisk.Low, trust = 2,
                        moves = new[] { (Rival, -3d) } },
                    new Option { label = "Report to alliance", description = "Tell your allies about the approach",
                        risk = HouseEventRisk.Medium, trust = 8,
                        moves = new[] { (Rival, -15d), (Ally, 10d) } },
                },
            },
            new Template
            {
                id = "house-divide", title = "House Divide",
                narrative = "The house is split after a heated argument between {HOH} and {NOMINEE}. "
                    + "Everyone is picking sides, and both are looking to you for support.",
                alternatives = new[]
                {
                    "{HOH} and {NOMINEE} have stopped speaking after a row the whole house heard through the walls. Camps are forming, and both of them want to know which one you are in.",
                    "A dinner turned into a shouting match between {HOH} and {NOMINEE}, and nobody has sat at the same table since. Each has asked you, separately, where you stand.",
                },
                roles = new[] { Hoh, Nominee },
                options = new[]
                {
                    new Option { label = "Side with HoH", description = "Publicly support the Head of Household",
                        risk = HouseEventRisk.Medium, trust = 2,
                        moves = new[] { (Hoh, 12d), (Nominee, -10d) } },
                    new Option { label = "Side with nominee", description = "Stand up for the underdog",
                        risk = HouseEventRisk.High, trust = 3,
                        moves = new[] { (Nominee, 15d), (Hoh, -12d) } },
                    new Option { label = "Stay neutral", description = "Refuse to pick a side in the conflict",
                        risk = HouseEventRisk.Low, trust = -2,
                        moves = new[] { (Hoh, -3d), (Nominee, -3d) } },
                },
            },
            new Template
            {
                id = "late-night-alliance-talk", title = "Late Night Alliance Talk",
                narrative = "{ALLY} wakes you up in the middle of the night, worried that someone in "
                    + "your alliance is leaking information. They suspect {RIVAL} is playing both sides.",
                alternatives = new[]
                {
                    "It is past two when {ALLY} shakes you awake. Something you said in confidence has come back to them from the other side of the house, and they think {RIVAL} carried it.",
                    "{ALLY} corners you before anyone else is up. A detail only your alliance knew is out, and they are sure {RIVAL} has been talking to both camps.",
                },
                roles = new[] { Ally, Rival },
                options = new[]
                {
                    new Option { label = "Investigate together", description = "Team up to find out the truth",
                        risk = HouseEventRisk.Low, trust = 5,
                        moves = new[] { (Ally, 10d) } },
                    new Option { label = "Dismiss concerns", description = "Tell them they're being paranoid",
                        risk = HouseEventRisk.Medium, trust = -3,
                        moves = new[] { (Ally, -8d) } },
                    new Option { label = "Confront the suspect", description = "Go directly to the suspected leak",
                        risk = HouseEventRisk.High, trust = 4,
                        moves = new[] { (Rival, -10d), (Ally, 5d) } },
                },
            },
            new Template
            {
                id = "competition-sabotage", title = "Competition Sabotage",
                narrative = "You discover that {RIVAL} has been studying the house layout for an "
                    + "upcoming memory competition. {ALLY} suggests you could sabotage their preparation.",
                alternatives = new[]
                {
                    "{RIVAL} has been pacing the rooms counting steps and doors, getting ready for a memory competition. {ALLY} points out how easy it would be to feed them the wrong count.",
                    "You find {RIVAL}'s notes on the house layout tucked under a cushion - competition prep. {ALLY} wonders aloud whether the notes need to stay accurate.",
                },
                roles = new[] { Rival, Ally },
                options = new[]
                {
                    new Option { label = "Sabotage them", description = "Feed false information about the house layout",
                        risk = HouseEventRisk.High, trust = -8,
                        moves = new[] { (Rival, -8d), (Ally, 5d) } },
                    new Option { label = "Warn them", description = "Tell the rival about the sabotage plot to gain favor",
                        risk = HouseEventRisk.High, trust = -3,
                        moves = new[] { (Rival, 12d), (Ally, -15d) } },
                    new Option { label = "Stay out of it", description = "Don't get involved in the scheming",
                        risk = HouseEventRisk.Low, trust = 2,
                        moves = new (string, double)[0] },
                },
            },
            new Template
            {
                id = "emotional-breakdown", title = "Emotional Breakdown",
                narrative = "{NOMINEE} breaks down crying in the bathroom, feeling isolated and "
                    + "targeted. No one else has noticed yet, and you're the only one who can comfort them.",
                alternatives = new[]
                {
                    "{NOMINEE} is sitting on the bathroom floor with the door half open, not really crying any more, just done. Nobody else has come looking.",
                    "You find {NOMINEE} in the yard after dark, alone, convinced the whole house has already decided. Whatever gets said next, you are the one saying it.",
                },
                roles = new[] { Nominee },
                options = new[]
                {
                    new Option { label = "Comfort them", description = "Sit with them and offer genuine support",
                        risk = HouseEventRisk.Low, trust = 5,
                        moves = new[] { (Nominee, 18d) } },
                    new Option { label = "Strategic sympathy", description = "Use this moment to build a strategic bond",
                        risk = HouseEventRisk.Medium, trust = -3,
                        moves = new[] { (Nominee, 10d) } },
                    new Option { label = "Walk away", description = "Pretend you didn't see anything",
                        risk = HouseEventRisk.Low, trust = -2,
                        moves = new[] { (Nominee, -5d) } },
                },
            },
        };

        // ---------------------------------------------------------------- filling the roles

        /// <summary>
        /// Who plays each part this week.
        ///
        /// <para>The ally is whoever the player is warmest with and the rival whoever they are
        /// coldest with — the reference sorts on the relationship and takes both ends. Ties break on
        /// id so the same season always casts the same people.</para>
        /// </summary>
        public static Dictionary<string, string> Roles(EpisodeState state)
        {
            var roles = new Dictionary<string, string>(StringComparer.Ordinal);
            if (state == null) return roles;

            var npcs = state.Active.Where(c => !c.isPlayer)
                .OrderByDescending(c => state.Score(state.playerId, c.id))
                .ThenBy(c => c.id, StringComparer.Ordinal)
                .ToList();
            if (npcs.Count < 2) return roles;

            roles[Ally] = npcs[0].id;
            roles[Rival] = npcs[npcs.Count - 1].id;

            // The reference falls back to "some houseguest" for these two rather than leaving them
            // unresolved, so a template naming the block still plays before anybody is on it.
            string hoh = state.hohId != null && state.Find(state.hohId)?.isPlayer == false
                ? state.hohId : npcs[0].id;
            string nominee = state.nominees.FirstOrDefault(id => state.Find(id)?.isPlayer == false)
                             ?? npcs[1].id;
            roles[Hoh] = hoh;
            roles[Nominee] = nominee;
            return roles;
        }

        /// <summary>Whether every part a template needs has somebody to play it.</summary>
        public static bool Castable(Template template, Dictionary<string, string> roles) =>
            template != null && roles != null && template.roles.All(roles.ContainsKey);

        /// <summary>Puts names where the placeholders are.</summary>
        public static string Fill(EpisodeState state, string text, Dictionary<string, string> roles)
        {
            if (string.IsNullOrEmpty(text) || roles == null) return text;
            foreach (var pair in roles)
            {
                var actor = state.Find(pair.Value);
                if (actor != null) text = text.Replace(pair.Key, actor.name);
            }
            return text;
        }

        // ---------------------------------------------------------------- drawing one

        /// <summary>Whether the house is in a position to have anything happen to it.</summary>
        public static bool Ready(EpisodeState state) =>
            state != null
            && state.week >= state.eventRulesStartWeek
            && state.houseEvents.Count < Ceiling
            && state.Find(state.playerId)?.status == ContestantStatus.Active
            && state.Active.Count(c => !c.isPlayer) >= 2
            && !state.houseEvents.Any(e => e.week == state.week);

        /// <summary>Anything still waiting on an answer.</summary>
        public static HouseEventState Pending(EpisodeState state) =>
            state?.houseEvents.FirstOrDefault(e => !e.resolved);

        /// <summary>
        /// Draws this week's situation, or nothing.
        ///
        /// <para>Takes its roll rather than making one, so the engine can spend the season's own
        /// generator — this runs inside a committed command and the draw has to replay.</para>
        ///
        /// <para>A template whose roles do not all resolve falls back to the first simple one that
        /// does, which is the reference's own recovery rather than a placeholder in the narrative.
        /// </para>
        /// </summary>
        /// <summary>
        /// The narrative for this week: the original the first week, then the alternatives in turn.
        /// Keyed on the week and never on a roll, so the words change and the season's randomness
        /// does not.
        /// </summary>
        public static string Narrative(string first, string[] alternatives, int week)
        {
            if (alternatives == null || alternatives.Length == 0) return first;
            int index = (Math.Max(1, week) - 1) % (alternatives.Length + 1);
            return index == 0 ? first : alternatives[index - 1];
        }

        public static HouseEventState Draw(EpisodeState state, double roll, long sequence)
        {
            if (!Ready(state)) return null;
            var roles = Roles(state);
            if (roles.Count == 0) return null;

            int index = (int)(roll * Catalog.Length);
            if (index >= Catalog.Length) index = Catalog.Length - 1;
            if (index < 0) index = 0;

            var template = Catalog[index];
            if (!Castable(template, roles))
                template = Catalog.FirstOrDefault(t => t.roles.Length <= 2 && Castable(t, roles));
            if (template == null) return null;

            return new HouseEventState
            {
                id = "house-event-" + sequence,
                kind = HouseEventKind.House,
                title = template.title,
                narrative = Fill(state, Narrative(template.narrative, template.alternatives, state.week), roles),
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
                    // Resolved to people here, not left in role terms: the roles are who they are
                    // this week, and a save answered three weeks later must not silently re-aim at
                    // whoever the player has since fallen out with.
                    impacts = option.moves
                        .Where(m => roles.ContainsKey(m.role))
                        .Select(m => new HouseEventImpact { targetId = roles[m.role], amount = m.amount })
                        .ToList(),
                }).ToList(),
            };
        }

        /// <summary>What answering this way is worth, for the log and the recap.</summary>
        public static string Outcome(EpisodeState state, HouseEventState item, int index)
        {
            if (item == null || index < 0 || index >= item.choices.Count) return null;
            var choice = item.choices[index];
            var moved = choice.impacts
                .Select(i => (state.Find(i.targetId)?.name ?? i.targetId)
                             + (i.amount >= 0 ? " +" : " ") + i.amount.ToString("0"))
                .ToList();
            return choice.label + (moved.Count == 0 ? " — nobody was any the wiser." : ": " + string.Join(", ", moved));
        }
    }
}
