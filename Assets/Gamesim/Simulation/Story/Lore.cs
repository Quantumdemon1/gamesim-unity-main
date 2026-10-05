using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Getting to know the houseguests: lore sheets, the reveal ladder, and what a known fact does.
    ///
    /// <para><b>Three sources</b> (20 §3.7). <i>Authored</i> sheets for the fictional regular
    /// roster, which agree with their public cards and build on them. <i>Legacy</i> sheets for
    /// identity-intact alumni, which restate their documented game only - no family, no romance,
    /// no invented secret. <i>Derived</i> sheets for everyone else - custom and imported
    /// houseguests and renamed cards - built from their own card and trait pools, phrased as
    /// behaviour, with nothing that could contradict what the player wrote about them.</para>
    ///
    /// <para><b>Snapshotted.</b> <see cref="Cast"/> runs once, when a season switches the story
    /// system on, and saves each houseguest's fact ids. Appending to the catalogue later never
    /// changes a running season.</para>
    ///
    /// <para><b>The card stays public.</b> Name, age, hometown, occupation, both traits and the
    /// one-line bio are the move-in package the cast screen already showed. Only lore facets are
    /// hidden. The hidden-job trope lives on as a cover story: a fictional sheet may say the card is
    /// what they told the house, and the secret is the rest.</para>
    /// </summary>
    public static partial class Lore
    {
        /// <summary>One fact about a houseguest.</summary>
        public sealed class Fact
        {
            public string id, facet, text, approach, topic;

            /// <summary>How deep it sits: 1 first talk, 2 rapport 3, 3 rapport 6 and a finished personal beat, 4 only through an arc.</summary>
            public int depth;

            /// <summary>+1 when knowing it makes its approach resonate, −1 when it makes it grate, 0 otherwise.</summary>
            public int sign;

            /// <summary>What telling it asks of the person: personal and romance facts are never on a legacy sheet.</summary>
            public StoryPeople.Sensitivity sensitivity = StoryPeople.Sensitivity.Game;
        }

        /// <summary>A houseguest's lore.</summary>
        public sealed class Sheet
        {
            public string key, source, primaryTrait, romance, conflictStyle, goal;
            public Fact[] facts = Array.Empty<Fact>();

            /// <summary>A legacy sheet's audited record includes winning a season: the house's target in week one.</summary>
            public bool wonSeason;
        }

        /// <summary>The facets, in the order a profile lists them.</summary>
        public static class Facets
        {
            public const string Origin = "origin", Home = "home", Work = "work", Respects = "respects";
            public const string Unforgivable = "unforgivable", HotButton = "hot-button", Comfort = "comfort";
            public const string Goal = "goal", ConflictStyle = "conflict-style", Romance = "romance", Secret = "secret";
            public const string Legacy = "legacy", Tendency = "tendency";
            public static readonly string[] All =
            { Origin, Home, Work, Respects, Unforgivable, HotButton, Comfort, Goal, ConflictStyle, Romance, Secret, Legacy, Tendency };
        }

        // ---------------------------------------------------------------- casting

        /// <summary>
        /// Gives every non-player houseguest a sheet and snapshots its fact ids. The player's own
        /// card never gets a sheet: the player knows who they are.
        /// </summary>
        public static void Cast(EpisodeState state)
        {
            if (state?.story == null) return;
            state.story.lore.Clear();
            foreach (var who in state.contestants.Where(c => !c.isPlayer))
            {
                var sheet = SheetFor(who);
                if (sheet == null) continue;
                state.story.lore.Add(new LoreCastState
                {
                    contestantId = who.id, sheetKey = sheet.key, source = sheet.source,
                    factIds = sheet.facts.Select(f => f.id).Take(16).ToList(),
                });
            }
        }

        /// <summary>Which sheet somebody gets, by the three-source rule.</summary>
        public static Sheet SheetFor(ContestantState who)
        {
            if (who == null) return null;
            var template = StoryPeople.TemplateOf(who);
            if (template != null && string.Equals(template.Name, who.name, StringComparison.Ordinal))
            {
                if (template.Roster == CastTemplates.Roster.AllStars)
                    return Legacy.TryGetValue(template.Id, out var legacy) ? legacy : null;
                // The card must still be the card: an edited bio or a different motive is somebody
                // the player rewrote, and an authored sheet could contradict what they wrote.
                bool intact = string.IsNullOrEmpty(who.bio) ? who.motive == template.Motive : who.bio == template.Bio;
                if (intact && Authored.TryGetValue(template.Id, out var authored)) return authored;
            }
            return Derived(who);
        }

        /// <summary>The sheet a houseguest was cast with in this season, or null.</summary>
        public static Sheet SheetIn(EpisodeState state, string id)
        {
            var cast = state?.story?.lore.FirstOrDefault(l => l.contestantId == id);
            if (cast == null) return null;
            if (cast.source == LoreSources.Derived) return Derived(state.Find(id));
            var catalogue = cast.source == LoreSources.Legacy ? Legacy : Authored;
            return catalogue.TryGetValue(cast.sheetKey, out var sheet) ? sheet : null;
        }

        /// <summary>The facts a houseguest was cast with, as saved.</summary>
        public static IEnumerable<Fact> FactsOf(EpisodeState state, string id)
        {
            var cast = state?.story?.lore.FirstOrDefault(l => l.contestantId == id);
            var sheet = SheetIn(state, id);
            if (cast == null || sheet == null) return Enumerable.Empty<Fact>();
            return sheet.facts.Where(f => cast.factIds.Contains(f.id));
        }

        public static bool Knows(EpisodeState state, string factId) =>
            state?.story?.knownFacts != null && state.story.knownFacts.Contains(factId);

        /// <summary>What the player has learned about someone, shallowest first.</summary>
        public static List<Fact> Learned(EpisodeState state, string id) =>
            FactsOf(state, id).Where(f => Knows(state, f.id)).OrderBy(f => f.depth).ThenBy(f => f.id, StringComparer.Ordinal).ToList();

        // ---------------------------------------------------------------- what a known fact does

        /// <summary>
        /// Reception from what the player knows: a known "respects" fact for this approach
        /// resonates, a known hot-button grates. Lore overrides the axes.
        /// </summary>
        public static int KnownReception(EpisodeState state, string id, string approach)
        {
            if (approach == null || !Rules(state)) return 0;
            int sign = 0;
            foreach (var fact in FactsOf(state, id))
            {
                if (fact.approach != approach || fact.sign == 0 || !Knows(state, fact.id)) continue;
                if (fact.sign < 0) return -1;
                sign = 1;
            }
            return sign;
        }

        /// <summary>How many known facts about someone bear on this approach (+10 each on the odds, at most two).</summary>
        public static int KnownRelevant(EpisodeState state, string id, string approach)
        {
            if (approach == null || !Rules(state)) return 0;
            return FactsOf(state, id).Count(f => f.approach == approach && f.sign > 0 && Knows(state, f.id));
        }

        /// <summary>
        /// A hidden hot-button the approach touches: the payoff grates and the player learns the
        /// fact. The chance shown was exactly the one used; this changes only how it lands.
        /// </summary>
        public static Fact HiddenHotButton(EpisodeState state, string id, string approach)
        {
            if (approach == null || !Rules(state)) return null;
            return FactsOf(state, id).FirstOrDefault(f => f.approach == approach && f.sign < 0 && !Knows(state, f.id));
        }

        /// <summary>Teaches the player a fact. Returns false when they already knew it.</summary>
        public static bool Learn(EpisodeState state, string factId)
        {
            if (state?.story == null || string.IsNullOrEmpty(factId) || Knows(state, factId)) return false;
            if (state.story.knownFacts.Count >= 200) return false;
            state.story.knownFacts.Add(factId);
            return true;
        }

        private static bool Rules(EpisodeState state) =>
            state?.story != null && state.story.rulesVersion >= StoryRules.Lore;

        // ---------------------------------------------------------------- the reveal ladder

        /// <summary>
        /// Rapport a conversation earns, no roll: a personal chat 2, spending real time 3, small
        /// talk 1, game talk 1, a kept secret 3. Half again with somebody sociable.
        /// </summary>
        public static int RapportFor(EpisodeCommandKind kind, ContestantState npc)
        {
            int rapport;
            switch (kind)
            {
                case EpisodeCommandKind.PersonalChat: rapport = 2; break;
                case EpisodeCommandKind.RelationshipBuilding: rapport = 3; break;
                case EpisodeCommandKind.SmallTalk: rapport = 1; break;
                case EpisodeCommandKind.Talk: rapport = 1; break;
                case EpisodeCommandKind.DiscussGame: rapport = 1; break;
                case EpisodeCommandKind.StrategicDiscussion: rapport = 1; break;
                case EpisodeCommandKind.ShareSecret: rapport = 3; break;
                case EpisodeCommandKind.VentAbout: rapport = 1; break;
                // The room acts: the bedroom is the most personal room in the house.
                case EpisodeCommandKind.PillowTalk: rapport = 3; break;
                case EpisodeCommandKind.InviteUp:
                case EpisodeCommandKind.PublicDefense: rapport = 2; break;
                case EpisodeCommandKind.AllianceMeet:
                case EpisodeCommandKind.PlayAGame:
                case EpisodeCommandKind.Cook: rapport = 1; break;
                default: return 0;
            }
            return Personality.Of(npc).Sociable >= 2 ? (int)Math.Round(rapport * 1.5) : rapport;
        }

        /// <summary>The deepest a player may reach with someone right now.</summary>
        public static int Depth(EpisodeState state, string id, bool finishedPersonalBeat)
        {
            var contact = state?.story?.contacts.FirstOrDefault(c => c.npcId == id);
            if (contact == null) return 0;
            if (contact.rapport >= 6 && finishedPersonalBeat) return 3;
            if (contact.rapport >= 3) return 2;
            return 1;
        }

        /// <summary>Which facets a kind of conversation opens onto.</summary>
        public static string[] FacetsFor(EpisodeCommandKind kind)
        {
            switch (kind)
            {
                case EpisodeCommandKind.PersonalChat:
                case EpisodeCommandKind.RelationshipBuilding:
                case EpisodeCommandKind.PillowTalk:
                    return new[] { Facets.Origin, Facets.Home, Facets.Comfort, Facets.Respects, Facets.Unforgivable, Facets.Romance };
                case EpisodeCommandKind.Cook:
                    return new[] { Facets.Home, Facets.Comfort, Facets.Origin };
                case EpisodeCommandKind.PublicDefense:
                    return new[] { Facets.Respects, Facets.Unforgivable };
                case EpisodeCommandKind.InviteUp:
                    return new[] { Facets.Goal, Facets.Respects, Facets.Comfort };
                case EpisodeCommandKind.AllianceMeet:
                    return new[] { Facets.Goal, Facets.ConflictStyle, Facets.Tendency };
                case EpisodeCommandKind.SmallTalk:
                case EpisodeCommandKind.Talk:
                case EpisodeCommandKind.PlayAGame:
                    return new[] { Facets.Work, Facets.Comfort, Facets.Origin, Facets.Tendency };
                case EpisodeCommandKind.DiscussGame:
                case EpisodeCommandKind.StrategicDiscussion:
                    return new[] { Facets.Goal, Facets.ConflictStyle, Facets.Respects, Facets.Legacy, Facets.Tendency };
                case EpisodeCommandKind.VentAbout:
                    return new[] { Facets.HotButton, Facets.Unforgivable, Facets.ConflictStyle };
                case EpisodeCommandKind.ShareSecret:
                    return new[] { Facets.Unforgivable, Facets.Home, Facets.Respects };
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>
        /// E2 personal chat opens more of a person's non-secret life and temperament. The goal
        /// remains a game conversation's job. Old seasons retain the exact original facet order.
        /// A new array is returned: a reader cannot alter the catalogue's next conversation.
        /// </summary>
        public static string[] FacetsFor(EpisodeState state, EpisodeCommandKind kind) =>
            kind == EpisodeCommandKind.PersonalChat && ConversationIntentRules.PersonalLoreOn(state)
                ? new[] { Facets.Origin, Facets.Home, Facets.Comfort, Facets.Respects, Facets.Unforgivable,
                    Facets.Romance, Facets.Work, Facets.HotButton, Facets.ConflictStyle, Facets.Legacy, Facets.Tendency }
                : FacetsFor(kind);

        /// <summary>
        /// The next fact a conversation reveals: the shallowest hidden one, within reach, whose
        /// facet the conversation opens onto. Secrets never come this way.
        /// </summary>
        public static Fact NextReveal(EpisodeState state, string id, EpisodeCommandKind kind, bool finishedPersonalBeat)
        {
            if (!Rules(state)) return null;
            int reach = Depth(state, id, finishedPersonalBeat);
            var facets = FacetsFor(state, kind);
            return FactsOf(state, id)
                .Where(f => f.depth <= reach && f.depth < 4 && f.facet != Facets.Secret && Array.IndexOf(facets, f.facet) >= 0 && !Knows(state, f.id))
                .OrderBy(f => f.depth).ThenBy(f => Array.IndexOf(facets, f.facet)).ThenBy(f => f.id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>The fact behind a facet on someone's sheet, if they were cast with one.</summary>
        public static Fact Facet(EpisodeState state, string id, string facet) =>
            FactsOf(state, id).FirstOrDefault(f => f.facet == facet);

        /// <summary>Whether somebody is open to a romance storyline, by their sheet or, failing one, their axes.</summary>
        public static bool RomanceOpen(EpisodeState state, string id)
        {
            var who = state?.Find(id);
            if (who == null || StoryPeople.IsRealPerson(who)) return false;
            var sheet = SheetIn(state, id);
            if (sheet?.romance == "closed") return false;
            if (sheet?.romance == "open" || sheet?.romance == "strategic") return true;
            return Personality.Of(who).Sociable >= 1;
        }
    }
}
