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
                        archetype = "The Newcomer", age = 30, occupation = "Houseguest",
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
            var npc = new ContestantState
            {
                id = id, name = name, pronouns = pronouns, homeRoom = room, motive = motive,
                status = ContestantStatus.Active, traits = new List<string>(traits),
                stats = DefaultStats(traits)
            };
            Card(npc);
            return npc;
        }

        /// <summary>
        /// The card copy a houseguest's tile carries: an archetype, an age and a job.
        ///
        /// <para>The <b>archetypes are the web game's own</b> — this scenario's five are drawn from
        /// its regular cast, so The Diplomat, The Firebrand, The Caregiver, The Party Animal and The
        /// Brainiac are copied rather than invented. Ages and occupations are authored Unity
        /// scenario defaults, in the same class as the motives and room homes above: the reference
        /// screenshots show the shape of the field, not its value for these five.</para>
        /// </summary>
        private static void Card(ContestantState npc)
        {
            switch (npc.id)
            {
                case MayaId:
                    npc.archetype = "The Diplomat"; npc.age = 31; npc.occupation = "Mediator"; break;
                case "taylor-kim":
                    npc.archetype = "The Firebrand"; npc.age = 26; npc.occupation = "Personal Trainer"; break;
                case "jamie-roberts":
                    npc.archetype = "The Caregiver"; npc.age = 38; npc.occupation = "Paediatric Nurse"; break;
                case "casey-wilson":
                    npc.archetype = "The Party Animal"; npc.age = 24; npc.occupation = "Bartender"; break;
                case "riley-johnson":
                    npc.archetype = "The Brainiac"; npc.age = 29; npc.occupation = "Data Analyst"; break;
            }
        }

        /// <summary>
        /// This scenario's stats: the lower-middle base roll plus trait boosts.
        ///
        /// <para>The arithmetic itself lives in <see cref="WebTraits.CreateStats"/>, which is where
        /// <see cref="CastTemplates"/> reads it from too. It used to be a private copy here with its
        /// own six-trait lookup table, and five of these houseguests also appear in the template
        /// pool — two copies of the same formula is exactly the pair that drifts.</para>
        /// </summary>
        private static ContestantStats DefaultStats(string[] traits)
        {
            foreach (var trait in traits)
                if (!WebTraits.Known(trait))
                    throw new ArgumentOutOfRangeException(nameof(traits), trait, "This scenario trait is not in the trait table.");
            return WebTraits.CreateStats(traits);
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
