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

            /// <summary>
            /// A houseguest the player built themselves, which wins over <see cref="PlayerTemplateId"/>.
            ///
            /// <para>The two are not alternatives so much as stages: the creator is opened <i>from</i>
            /// a card, so a customised houseguest usually has both — the id says which card is spoken
            /// for and must not also be cast as an NPC, and the draft says who the player actually
            /// is. Creating from a blank slate leaves the id empty and every card in the house.</para>
            /// </summary>
            public CharacterDraft Authored;
            /// <summary>Explicit NPC slots. Repeated profiles are allowed and receive separate season identities.</summary>
            public List<CharacterProfile> CustomHouseguests = new List<CharacterProfile>();

            public Choice Copy()
            {
                var copy = (Choice)MemberwiseClone();
                copy.Authored = Authored?.Copy();
                copy.CustomHouseguests = CustomHouseguests?.Select(profile => profile?.Clone()).ToList();
                return copy;
            }
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

        /// <summary>The largest house any one roster seats: twelve. Every playable season is at most this.</summary>
        public static int LargestRosterHouse
            => Enum.GetValues(typeof(CastTemplates.Roster)).Cast<CastTemplates.Roster>().Max(LargestHouse);

        /// <summary>The largest stress house: what validation stores, from both rosters together (sixteen).</summary>
        public static int LargestStressHouse => Math.Min(EpisodeValidation.MaximumCast, CastTemplates.Everyone.Count + 1);

        /// <summary>
        /// <b>Verification only.</b> A house larger than any roster seats - thirteen to sixteen - for the
        /// standalone profile's stress run (<c>--gamesim-verify --gamesim-house-size 16
        /// --gamesim-stress-roster</c>), and for nothing else: the cast screen, the creator, the importer
        /// and every playable season go through <see cref="Create"/>, which never pads one roster with
        /// the other. Validation stores sixteen; no roster seats more than twelve, so without this a
        /// sixteen-person request was profiled at twelve, which is not a sixteen-person measurement.
        ///
        /// <para>The player is the unaffiliated newcomer, as on a default choice. The houseguests are
        /// both rosters' templates taken in turn - the regular season's first, the all-stars' first,
        /// the regular season's second - each in its roster's table order, so the house holds both
        /// rosters and the same size and seed always build the same house. Every template id and name
        /// is distinct across the two rosters, so nobody is cast twice. Like <see cref="Create"/>, it
        /// draws nothing from the season's generator.</para>
        /// </summary>
        public static EpisodeState CreateVerificationStressHouse(int houseSize, uint seed)
        {
            if (houseSize <= LargestRosterHouse || houseSize > LargestStressHouse)
                throw new ArgumentOutOfRangeException(nameof(houseSize), houseSize,
                    "The stress house seats " + (LargestRosterHouse + 1) + " to " + LargestStressHouse
                    + " houseguests; a house a roster can seat is built by SeasonBuilder.Create.");
            var state = NewState(seed);
            state.contestants.Add(Player(null, null));
            foreach (var template in StressCast().Take(houseSize - 1))
                state.contestants.Add(CastTemplates.ToContestant(template, false));
            Seat(state, CastTemplates.Roster.Regular);
            return state;
        }

        /// <summary>The stress house's casting order: the two rosters taken in turn, each in table order.</summary>
        private static List<CastTemplates.Template> StressCast()
        {
            var regular = CastTemplates.In(CastTemplates.Roster.Regular).ToList();
            var allStars = CastTemplates.In(CastTemplates.Roster.AllStars).ToList();
            var order = new List<CastTemplates.Template>(regular.Count + allStars.Count);
            for (int i = 0; i < Math.Max(regular.Count, allStars.Count); i++)
            {
                if (i < regular.Count) order.Add(regular[i]);
                if (i < allStars.Count) order.Add(allStars[i]);
            }
            return order;
        }

        public static EpisodeState Create(Choice choice, uint seed)
        {
            if (choice == null) throw new ArgumentNullException(nameof(choice));
            if (!Enum.IsDefined(typeof(CastTemplates.Roster), choice.Roster)) throw new ArgumentException("Choose a supported roster.");

            var templateId = choice.Authored != null ? choice.Authored.SourceTemplateId : choice.PlayerTemplateId;
            var persona = string.IsNullOrEmpty(templateId)
                ? null
                : CastTemplates.Find(templateId);
            int size = ClampHouseSize(choice.Roster, choice.HouseSize);
            if (choice.CustomHouseguests == null || choice.CustomHouseguests.Count > size - 1)
                throw new ArgumentException("Custom houseguests must fit the selected house size.");
            foreach (var profile in choice.CustomHouseguests)
                if (profile == null || !profile.TryValidate(out _)) throw new ArgumentException("A selected custom houseguest is invalid.");

            var state = NewState(seed);

            state.contestants.Add(Player(persona, choice.Authored));
            var reservedTemplates = new HashSet<string>(StringComparer.Ordinal);
            if (persona != null) reservedTemplates.Add(persona.Id);
            for (int i = 0; i < choice.CustomHouseguests.Count; i++)
            {
                var person = choice.CustomHouseguests[i].ToDraft().ToContestant();
                person.id = "custom-" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                person.isPlayer = false;
                state.contestants.Add(person);
                if (!string.IsNullOrEmpty(person.sourceTemplateId)) reservedTemplates.Add(person.sourceTemplateId);
            }

            // In table order, skipping whoever the player is playing as. Order is the roster's, not
            // a shuffle, for the determinism reason in the class note above.
            foreach (var template in CastTemplates.In(choice.Roster)
                         .Where(t => !reservedTemplates.Contains(t.Id))
                         .Take(size - state.contestants.Count))
                state.contestants.Add(CastTemplates.ToContestant(template, false));

            Seat(state, choice.Roster);
            return state;
        }

        /// <summary>A season with nobody in it yet: week one's free time, its generator at the seed.</summary>
        private static EpisodeState NewState(uint seed) => new EpisodeState
        {
            sessionId = "gamesim-" + seed.ToString("x8"),
            seed = seed,
            competitionRulesVersion = CompetitionRules.Current,
            haveNotRulesStartWeek = 1,
            strategyRulesStartWeek = 1,
            npcSocial = NpcSocialState.Create(seed),
            randomState = seed == 0 ? 0x6D2B79F5u : seed,
            playerId = ContentCatalog.PlayerId,
            phase = EpisodePhase.Social,
            week = 1,
            socialActions = 0,
        };

        /// <summary>Every ordered pair's relationship at zero, then the arrival line: the house as it walks in.</summary>
        private static void Seat(EpisodeState state, CastTemplates.Roster roster)
        {
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
                text = Arrival(state.contestants.Count, roster),
                audienceIds = state.contestants.Select(c => c.id).ToList(),
            });
        }

        /// <summary>
        /// The player's houseguest.
        ///
        /// <para>The id is always <see cref="ContentCatalog.PlayerId"/>, whichever card was picked.
        /// Playing as Dan changes who you are on screen, not which record the engine treats as
        /// yours, and keeping one id means the chosen template can still appear in a later season as
        /// an NPC without colliding with a save.</para>
        /// </summary>
        /// <param name="authored">
        /// A houseguest built in the creator, which takes precedence over <paramref name="persona"/>.
        /// A customised card arrives as both: the persona still reserves the card so the player is
        /// not also cast as an NPC, and this is who they turned that card into.
        /// </param>
        private static ContestantState Player(CastTemplates.Template persona, CharacterDraft authored = null)
        {
            if (authored != null) return authored.ToContestant();
            if (persona == null)
            {
                return new ContestantState
                {
                    id = ContentCatalog.PlayerId,
                    appearance = CharacterAppearance.Preset(ContentCatalog.PlayerId),
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
        /// The opening line of the season, in the house's own word: houseguests, as every card of
        /// the opening calls them (UI-UX-PASS-PLAN S0).
        ///
        /// <para><see cref="ContentCatalog"/>'s own arrival text names six housemates and cannot be
        /// generalised — it is recorded verbatim in a frozen replay fixture — so a built season
        /// writes its own rather than editing that one.</para>
        /// </summary>
        private static string Arrival(int count, CastTemplates.Roster roster)
            => Word(count) + " houseguests, one new game"
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
