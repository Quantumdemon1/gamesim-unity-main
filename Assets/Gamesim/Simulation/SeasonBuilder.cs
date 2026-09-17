using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Builds a season from a choice of houseguest, roster and house size.
    ///
    /// <para>Removing the six-contestant rule made other house sizes legal; this is what finally
    /// makes one. Before it, every new season came from <see cref="ContentCatalog.Create"/>, which
    /// is a single authored scenario — the rule was gone but there was still exactly one house.</para>
    ///
    /// <para><b>Nothing here is random.</b> The cast is taken from <see cref="CastTemplates"/> in
    /// table order and the stats come from the trait formula, so the same choice and seed always
    /// build the same house. Drawing the cast with the season's own generator would have been the
    /// obvious alternative and is wrong: it consumes <see cref="EpisodeState.randomState"/> before
    /// week one, which is the state every replay fixture in this project is anchored to.</para>
    ///
    /// <para>The shipped scenario is deliberately untouched. <see cref="ContentCatalog.Create"/>
    /// still builds the six-person season with its authored opening relationships and arrival
    /// memories, and is still what a player gets if they never open the cast screen.</para>
    /// </summary>
    public static class SeasonBuilder
    {
        /// <summary>The house size the reference build defaults to.</summary>
        public const int DefaultHouseSize = 8;

        public static int MinimumHouse => EpisodeValidation.MinimumCast;

        /// <summary>What the player picked on the cast screen.</summary>
        public sealed class Choice
        {
            public CastTemplates.Roster Roster = CastTemplates.Roster.Regular;

            /// <summary>
            /// The houseguest the player is. Null keeps the unaffiliated newcomer the project has
            /// always started with, so the screen can be dismissed without picking anyone.
            /// </summary>
            public string PlayerTemplateId;

            public int HouseSize = DefaultHouseSize;

            public Choice Copy() => (Choice)MemberwiseClone();
        }

        /// <summary>
        /// How many houseguests this roster can seat, which is the roster's own size rather than
        /// <see cref="EpisodeValidation.MaximumCast"/>. Validation accepts sixteen stored
        /// houseguests; a roster holds twelve people, and the builder will not pad a regular season
        /// with all-stars to reach a number.
        /// </summary>
        public static int LargestHouse(CastTemplates.Roster roster)
            => Math.Min(EpisodeValidation.MaximumCast, CastTemplates.In(roster).Count());

        public static int ClampHouseSize(CastTemplates.Roster roster, int wanted)
            => Math.Max(MinimumHouse, Math.Min(LargestHouse(roster), wanted));

        public static EpisodeState Create(Choice choice, uint seed)
        {
            if (choice == null) throw new ArgumentNullException(nameof(choice));

            var persona = string.IsNullOrEmpty(choice.PlayerTemplateId)
                ? null
                : CastTemplates.Find(choice.PlayerTemplateId);
            int size = ClampHouseSize(choice.Roster, choice.HouseSize);

            var state = new EpisodeState
            {
                sessionId = "gamesim-" + seed.ToString("x8"),
                seed = seed,
                npcSocial = NpcSocialState.Create(seed),
                randomState = seed == 0 ? 0x6D2B79F5u : seed,
                playerId = ContentCatalog.PlayerId,
                phase = EpisodePhase.Social,
                week = 1,
                socialActions = 0,
            };

            state.contestants.Add(Player(persona));

            // In table order, skipping whoever the player is playing as. Order is the roster's, not
            // a shuffle, for the determinism reason in the class note above.
            foreach (var template in CastTemplates.In(choice.Roster)
                         .Where(t => persona == null || t.Id != persona.Id)
                         .Take(size - 1))
                state.contestants.Add(CastTemplates.ToContestant(template, false));

            foreach (var from in state.contestants)
                foreach (var to in state.contestants)
                    if (from.id != to.id)
                        state.relationships.Add(new RelationshipState { fromId = from.id, toId = to.id, score = 0 });

            state.events.Add(new EpisodeEvent
            {
                sequence = state.nextSequence++,
                week = 1,
                phase = EpisodePhase.Social,
                kind = "arrival",
                text = Arrival(state.contestants.Count, choice.Roster),
                audienceIds = state.contestants.Select(c => c.id).ToList(),
            });
            return state;
        }

        /// <summary>
        /// The player's houseguest.
        ///
        /// <para>The id is always <see cref="ContentCatalog.PlayerId"/>, whichever card was picked.
        /// Playing as Dan changes who you are on screen, not which record the engine treats as
        /// yours, and keeping one id means the chosen template can still appear in a later season as
        /// an NPC without colliding with a save.</para>
        /// </summary>
        private static ContestantState Player(CastTemplates.Template persona)
        {
            if (persona == null)
            {
                return new ContestantState
                {
                    id = ContentCatalog.PlayerId,
                    name = "You",
                    pronouns = "they/them",
                    isPlayer = true,
                    status = ContestantStatus.Active,
                    motive = "Choose who to trust, survive the vote, and build a game you can explain.",
                    homeRoom = "Living",
                    archetype = "The Newcomer", age = 30, occupation = "Houseguest",
                    // The same balanced override the shipped scenario gives the player: a starting
                    // house that is not tilted before anybody has played a week.
                    stats = new ContestantStats
                    {
                        physical = 6, mental = 6, endurance = 6, social = 6,
                        luck = 6, competition = 6, strategic = 6, loyalty = 6
                    }
                };
            }

            var player = CastTemplates.ToContestant(persona, true);
            player.id = ContentCatalog.PlayerId;
            return player;
        }

        /// <summary>
        /// The opening line of the season.
        ///
        /// <para><see cref="ContentCatalog"/>'s own arrival text names six and cannot be generalised
        /// — it is recorded verbatim in a frozen replay fixture — so a built season writes its own
        /// rather than editing that one.</para>
        /// </summary>
        private static string Arrival(int count, CastTemplates.Roster roster)
            => Word(count) + " housemates, one new game"
               + (roster == CastTemplates.Roster.AllStars ? ", and every one of them has played before" : string.Empty)
               + ". Meet the cast, decide who to trust, and prepare for the first competition.";

        private static readonly string[] Words =
        {
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight",
            "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen"
        };

        private static string Word(int count)
            => count >= 0 && count < Words.Length ? Words[count] : count.ToString();
    }
}
