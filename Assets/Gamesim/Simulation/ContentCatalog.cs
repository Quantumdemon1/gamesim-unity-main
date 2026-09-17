using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The native six-contestant *scenario*.
    ///
    /// <para>Six is this scenario's cast, not the format's rule — validation accepts any house
    /// between <see cref="EpisodeValidation.MinimumCast"/> and <see cref="EpisodeValidation.MaximumCast"/>.
    /// Until player setup exists there is nothing that asks for a different size, and this stays the
    /// season a fresh game starts.</para>
    ///
    /// <para>Its arrival line names six on purpose. That text is recorded in the frozen voting-bloc
    /// parity fixture, so generalising the wording breaks a replay comparison for no gain; a setup
    /// wizard that builds a different house should write its own arrival line rather than edit
    /// this one.</para>
    ///
    /// Identity/traits come from the web character-templates.ts;
    /// fixed stats use its creation.ts lower-middle base rolls plus traits.ts boosts.
    /// Motives, room homes and starting relationships are authored Unity scenario defaults.
    /// </summary>
    public static class ContentCatalog
    {
        public const string PlayerId = "player";
        public const string MayaId = "maya-hassan";

        public static EpisodeState Create(uint seed, string playerName = "You")
        {
            var state = new EpisodeState
            {
                sessionId = "gamesim-" + seed.ToString("x8"),
                seed = seed,
                npcSocial = NpcSocialState.Create(seed),
                randomState = seed == 0 ? 0x6D2B79F5u : seed,
                playerId = PlayerId,
                phase = EpisodePhase.Social,
                week = 1,
                socialActions = 0,
                contestants = new List<ContestantState>
                {
                    new ContestantState
                    {
                        id = PlayerId,
                        name = string.IsNullOrWhiteSpace(playerName) ? "You" : playerName.Trim(),
                        pronouns = "they/them", isPlayer = true, status = ContestantStatus.Active,
                        motive = "Choose who to trust, survive the vote, and build a game you can explain.",
                        homeRoom = "Living",
                        // A balanced player is an explicit scenario override, not a web NPC roll.
                        stats = new ContestantStats
                        {
                            physical = 6, mental = 6, endurance = 6, social = 6,
                            luck = 6, competition = 6, strategic = 6, loyalty = 6
                        }
                    },
                    Npc(MayaId, "Maya Hassan", "she/her", "Private",
                        "Build a dependable voting partnership without making promises she cannot keep.",
                        "Strategic", "Social"),
                    Npc("taylor-kim", "Taylor Kim", "she/her", "Yard",
                        "Prove she can win under pressure and earn safety without surrendering her independence.",
                        "Competitive", "Confrontational"),
                    Npc("jamie-roberts", "Jamie Roberts", "she/her", "Kitchen",
                        "Protect people who treat her honestly while keeping the courage to make her own move.",
                        "Emotional", "Strategic"),
                    Npc("casey-wilson", "Casey Wilson", "she/her", "Living",
                        "Keep conversations light, compare what people say, and avoid becoming the obvious target.",
                        "Social", "Strategic"),
                    Npc("riley-johnson", "Riley Johnson", "he/him", "Bedroom",
                        "Turn careful observation into one reliable partnership and a well-timed competition win.",
                        "Analytical", "Strategic")
                }
            };

            foreach (var from in state.contestants)
                foreach (var to in state.contestants)
                    if (from.id != to.id)
                        state.relationships.Add(new RelationshipState
                        {
                            fromId = from.id, toId = to.id, score = StartingScore(from.id, to.id)
                        });

            // These are private arrival observations, not omniscient facts about other people.
            Remember(state, MayaId, "jamie-roberts", "Jamie checked in on me during arrival. I appreciated it.");
            Remember(state, "jamie-roberts", MayaId, "Maya listened when I talked about the pressure of moving in.");
            Remember(state, "casey-wilson", "riley-johnson", "Riley questioned my first plan before hearing me out.");
            Remember(state, "riley-johnson", "casey-wilson", "Casey's first plan moved faster than I was comfortable with.");
            state.events.Add(new EpisodeEvent
            {
                sequence = state.nextSequence++, week = 1, phase = EpisodePhase.Social,
                kind = "arrival",
                text = "Six housemates, one new game. Meet the cast, decide who to trust, and prepare for the first competition.",
                audienceIds = new List<string>(state.contestants.ConvertAll(character => character.id))
            });
            return state;
        }

        /// <summary>U02 used the short Maya id before simulation saves existed.</summary>
        public static string CanonicalId(string id) => string.Equals(id, "maya", StringComparison.Ordinal) ? MayaId : id;

        private static ContestantState Npc(string id, string name, string pronouns, string room,
            string motive, params string[] traits)
        {
            return new ContestantState
            {
                id = id, name = name, pronouns = pronouns, homeRoom = room, motive = motive,
                status = ContestantStatus.Active, traits = new List<string>(traits),
                stats = DefaultStats(traits)
            };
        }

        private static ContestantStats DefaultStats(string[] traits)
        {
            // creation.ts ranges: primary 5..8, secondary 4..7, other 3..6.
            // Choose 6/5/4 before applying each trait's +2 primary and +1 secondary.
            var primary = new HashSet<string>();
            var secondary = new HashSet<string>();
            foreach (var trait in traits)
            {
                BoostKeys(trait, out var first, out var second);
                primary.Add(first); secondary.Add(second);
            }
            var values = new Dictionary<string, double>();
            foreach (var key in new[] { "physical", "mental", "endurance", "social", "luck", "competition", "strategic", "loyalty" })
                values[key] = primary.Contains(key) ? 6 : secondary.Contains(key) ? 5 : 4;
            foreach (var trait in traits)
            {
                BoostKeys(trait, out var first, out var second);
                values[first] = Math.Min(10, values[first] + 2);
                values[second] = Math.Min(10, values[second] + 1);
            }
            return new ContestantStats
            {
                physical = values["physical"], mental = values["mental"], endurance = values["endurance"],
                social = values["social"], luck = values["luck"], competition = values["competition"],
                strategic = values["strategic"], loyalty = values["loyalty"]
            };
        }

        private static void BoostKeys(string trait, out string primary, out string secondary)
        {
            switch (trait)
            {
                case "Competitive":
                case "Confrontational": primary = "physical"; secondary = "endurance"; break;
                case "Strategic":
                case "Analytical": primary = "mental"; secondary = "strategic"; break;
                case "Emotional": primary = "social"; secondary = "loyalty"; break;
                case "Social": primary = "social"; secondary = "luck"; break;
                default: throw new ArgumentOutOfRangeException(nameof(trait), trait, "No default stat mapping for this scenario trait.");
            }
        }

        private static double StartingScore(string first, string second)
        {
            if ((first == MayaId && second == "jamie-roberts") || (second == MayaId && first == "jamie-roberts")) return 15;
            if ((first == "casey-wilson" && second == "riley-johnson") || (second == "casey-wilson" && first == "riley-johnson")) return -10;
            return 0;
        }

        private static void Remember(EpisodeState state, string owner, string subject, string text)
        {
            state.memories.Add(new MemoryState { ownerId = owner, subjectId = subject, week = 1, isPrivate = true, text = text });
        }
    }
}
