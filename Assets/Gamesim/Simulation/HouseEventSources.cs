using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The rest of the reference's event layer: the four systems beyond <see cref="HouseEvents"/>.
    ///
    /// <para>All four write the same <see cref="HouseEventState"/>, which is why they need no schema
    /// of their own — <see cref="HouseEventKind"/> named all six kinds when the first one landed.
    /// What differs is where each finds its situations.</para>
    ///
    /// <list type="bullet">
    /// <item><b>Ambient</b> is the house being a house. It narrates and asks nothing, which is the
    /// one kind <see cref="HouseEventKind.Asks"/> says has no choices — a house that only ever
    /// interrogated the player would read as a questionnaire.</item>
    /// <item><b>Proximity</b> is two houseguests in the same room. <b>This port owns the harder half
    /// already</b>: a real house, real rooms, and a scheduler that knows who is standing where, so
    /// this is the one system that is better served here than in the reference, which picks a
    /// location from a list of strings.</item>
    /// <item><b>Emergent</b> is the season's own state having consequences — a friendship past the
    /// point of being casual, a rivalry that has escalated, a pair nobody has formalised.</item>
    /// <item><b>Crisis</b> is the week going wrong, and it reads the systems this port has built
    /// since: a deal's two parties are who you get caught listening to.</item>
    /// </list>
    ///
    /// <para>Every generator takes its rolls rather than making them, for the same reason
    /// <see cref="HouseEvents.Draw"/> does: these run inside committed commands and the draws have
    /// to replay.</para>
    /// </summary>
    public static class HouseEventSources
    {
        /// <summary>How much of a relationship arc counts as having escalated. The source's band.</summary>
        public const int EscalatedArc = 3;

        /// <summary>Warmth past which a pair are obviously together and have not said so.</summary>
        public const double UnspokenPairWarmth = 60;

        /// <summary>The week before which the house has not been in it long enough for a crisis.</summary>
        public const int CrisisFirstWeek = 2;

        // ---------------------------------------------------------------- ambient

        /// <summary>The reference's solo lines, with its placeholders.</summary>
        public static readonly string[] SoloLines =
        {
            "{NPC} is pacing back and forth in {LOCATION}, lost in thought.",
            "You overhear {NPC} practising a speech in {LOCATION}.",
            "{NPC} is sitting alone in {LOCATION}, staring at the memory wall.",
            "The sound of {NPC} laughing echoes from {LOCATION}.",
            "{NPC} has been unusually quiet today, keeping to themselves in {LOCATION}.",
            "You notice {NPC} making notes on a piece of paper in {LOCATION}.",
            "Someone left a mess in {LOCATION}. You suspect {NPC}.",
            "{NPC} can be heard singing off-key in {LOCATION}.",
        };

        /// <summary>The reference's pair lines.</summary>
        public static readonly string[] PairLines =
        {
            "{NPC1} and {NPC2} are whispering in {LOCATION}. They stop when they see you.",
            "You catch {NPC1} and {NPC2} sharing a laugh in {LOCATION}.",
            "{NPC1} is venting to {NPC2} in {LOCATION}. It sounds heated.",
            "{NPC1} and {NPC2} have been inseparable all day, hanging around {LOCATION}.",
            "{NPC1} and {NPC2} are deep in strategy talk in {LOCATION}.",
            "There's tension between {NPC1} and {NPC2} — they haven't spoken all day.",
        };

        /// <summary>The reference's house-wide lines, which name nobody.</summary>
        public static readonly string[] HouseLines =
        {
            "The house is eerily quiet tonight.",
            "Someone keeps leaving the lights on in the storage room.",
            "The memory wall feels heavier with each passing week.",
            "A strange energy fills the house tonight. Everyone is on edge.",
            "Another restless night in the house.",
        };

        /// <summary>
        /// Something the house is doing, narrated and never asked about.
        ///
        /// <para>Takes the room from the caller so the presentation layer can supply a real one —
        /// the reference picks from a list of strings, and this port has an actual house whose rooms
        /// have names.</para>
        /// </summary>
        public static HouseEventState Ambient(EpisodeState state, double pick, double which,
            string room, long sequence)
        {
            if (state == null) return null;
            var npcs = state.Active.Where(c => !c.isPlayer)
                .OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            if (npcs.Count == 0) return null;
            room = string.IsNullOrWhiteSpace(room) ? "the house" : room;

            string line;
            var involved = new List<string>();
            if (pick < 0.45 || npcs.Count < 2)
            {
                var who = npcs[Index(which, npcs.Count)];
                line = SoloLines[Index(pick / 0.45, SoloLines.Length)]
                    .Replace("{NPC}", who.name).Replace("{LOCATION}", room);
                involved.Add(who.id);
            }
            else if (pick < 0.85)
            {
                var first = npcs[Index(which, npcs.Count)];
                var second = npcs[(Index(which, npcs.Count) + 1) % npcs.Count];
                line = PairLines[Index((pick - 0.45) / 0.4, PairLines.Length)]
                    .Replace("{NPC1}", first.name).Replace("{NPC2}", second.name)
                    .Replace("{LOCATION}", room);
                involved.Add(first.id);
                involved.Add(second.id);
            }
            else
            {
                line = HouseLines[Index((pick - 0.85) / 0.15, HouseLines.Length)];
            }

            return new HouseEventState
            {
                id = "house-event-" + sequence,
                kind = HouseEventKind.Ambient,
                title = "In the house",
                narrative = line,
                involvedIds = involved,
                week = state.week,
                // Nothing to answer, so it arrives already settled. An ambient event that sat
                // unresolved would block the one situation a week that does ask something.
                resolved = true,
                chosenIndex = -1,
                outcome = null,
                choices = new List<HouseEventChoice>(),
            };
        }

        // ---------------------------------------------------------------- proximity

        /// <summary>
        /// Two houseguests the player has walked in on.
        ///
        /// <para>The caller supplies who and where, because only the presentation layer knows — this
        /// port tracks where everybody actually is, which is the half the reference does not have.
        /// The situation is the same either way: you have seen something, and what you do about it
        /// is the question.</para>
        /// </summary>
        public static HouseEventState Proximity(EpisodeState state, string firstId, string secondId,
            string room, long sequence)
        {
            var first = state?.Find(firstId);
            var second = state?.Find(secondId);
            if (first == null || second == null || first.id == second.id) return null;
            if (first.isPlayer || second.isPlayer) return null;
            if (first.status != ContestantStatus.Active || second.status != ContestantStatus.Active) return null;
            room = string.IsNullOrWhiteSpace(room) ? "the house" : room;

            bool close = state.Score(firstId, secondId) >= UnspokenPairWarmth;
            return new HouseEventState
            {
                id = "house-event-" + sequence,
                kind = HouseEventKind.Proximity,
                title = "You walk in on something",
                narrative = first.name + " and " + second.name + " are in " + room
                    + (close ? ", and they stop talking the moment they notice you."
                             : ", mid-argument. Neither of them looks pleased to see you."),
                involvedIds = new List<string> { first.id, second.id },
                week = state.week,
                resolved = false,
                chosenIndex = -1,
                choices = new List<HouseEventChoice>
                {
                    new HouseEventChoice
                    {
                        label = "Join them", description = "Walk in as though you were always going to.",
                        risk = HouseEventRisk.Medium, trustChange = 0,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = first.id, amount = close ? -4 : 6 },
                            new HouseEventImpact { targetId = second.id, amount = close ? -4 : 6 },
                        },
                    },
                    new HouseEventChoice
                    {
                        label = "Back out quietly", description = "Pretend you saw nothing.",
                        risk = HouseEventRisk.Low, trustChange = 0,
                        impacts = new List<HouseEventImpact>(),
                    },
                    new HouseEventChoice
                    {
                        label = "Ask what that was about", description = "Put them on the spot, now, in front of each other.",
                        risk = HouseEventRisk.High, trustChange = -3,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = first.id, amount = -8 },
                            new HouseEventImpact { targetId = second.id, amount = close ? -8 : 4 },
                        },
                    },
                },
            };
        }

        // ---------------------------------------------------------------- emergent

        /// <summary>
        /// A situation the season made rather than a catalogue did.
        ///
        /// <para>Two triggers, both the reference's. A pair warm past sixty who are not in an
        /// alliance have obviously been working together and have not said so; a relationship arc
        /// that has escalated to Intense or better has become something the house can see. Both read
        /// state this port already keeps and nothing has been asking about.</para>
        ///
        /// <para>Returns nothing when the season has not produced anything worth remarking on, which
        /// is most weeks — that is what makes it emergent rather than scheduled.</para>
        /// </summary>
        public static HouseEventState Emergent(EpisodeState state, long sequence)
        {
            if (state == null) return null;

            // An arc the house has watched escalate, worth more than a warm pair because the season
            // spent several weeks building it.
            var arc = state.relationshipArcs
                .Where(a => a.escalationLevel >= EscalatedArc)
                .OrderByDescending(a => a.escalationLevel)
                .ThenByDescending(a => a.intensity)
                .ThenBy(a => a.npcId, StringComparer.Ordinal)
                .FirstOrDefault(a => state.Find(a.npcId)?.status == ContestantStatus.Active);
            if (arc != null)
            {
                var other = state.Find(arc.npcId);
                // The arc says what it is. Reading the current score instead would call a rivalry a
                // friendship the moment one good conversation pushed it above zero, which is exactly
                // the history an arc exists to remember.
                bool sour = string.Equals(arc.arcType, "rivalry", StringComparison.OrdinalIgnoreCase);
                return new HouseEventState
                {
                    id = "house-event-" + sequence,
                    kind = HouseEventKind.Emergent,
                    title = sour ? "This has gone far enough" : "Everyone has noticed",
                    narrative = sour
                        ? "Whatever is between you and " + other.name + " has stopped being private. "
                          + "The house has started arranging itself around it."
                        : "You and " + other.name + " have become the pair everybody expects to see "
                          + "together. That is a target as much as it is an ally.",
                    involvedIds = new List<string> { other.id },
                    week = state.week,
                    resolved = false,
                    chosenIndex = -1,
                    choices = new List<HouseEventChoice>
                    {
                        new HouseEventChoice
                        {
                            label = sour ? "Have it out in the open" : "Lean into it",
                            description = sour ? "Say it where everybody can hear."
                                : "Stop pretending you are not working together.",
                            risk = HouseEventRisk.High, trustChange = sour ? -4 : 2,
                            impacts = new List<HouseEventImpact>
                            {
                                new HouseEventImpact { targetId = other.id, amount = sour ? -12 : 12 },
                            },
                        },
                        new HouseEventChoice
                        {
                            label = "Cool it down",
                            description = "Be seen with other people for a while.",
                            risk = HouseEventRisk.Low, trustChange = 0,
                            impacts = new List<HouseEventImpact>
                            {
                                new HouseEventImpact { targetId = other.id, amount = sour ? 5 : -5 },
                            },
                        },
                    },
                };
            }

            // A pair the player is warm with who have never formalised it.
            var unspoken = state.Active
                .Where(c => !c.isPlayer && state.Score(state.playerId, c.id) >= UnspokenPairWarmth)
                .Where(c => !state.Allied(state.playerId, c.id))
                .OrderByDescending(c => state.Score(state.playerId, c.id))
                .ThenBy(c => c.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unspoken == null) return null;

            return new HouseEventState
            {
                id = "house-event-" + sequence,
                kind = HouseEventKind.Emergent,
                title = "Nobody has said it out loud",
                narrative = unspoken.name + " has been on your side for weeks without either of you "
                    + "calling it anything. That is either trust or a misunderstanding waiting to happen.",
                involvedIds = new List<string> { unspoken.id },
                week = state.week,
                resolved = false,
                chosenIndex = -1,
                choices = new List<HouseEventChoice>
                {
                    new HouseEventChoice
                    {
                        label = "Say it out loud", description = "Put a name to what you already have.",
                        risk = HouseEventRisk.Medium, trustChange = 4,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = unspoken.id, amount = 10 },
                        },
                    },
                    new HouseEventChoice
                    {
                        label = "Leave it unspoken", description = "What is not agreed cannot be broken.",
                        risk = HouseEventRisk.Low, trustChange = 0,
                        impacts = new List<HouseEventImpact>(),
                    },
                },
            };
        }

        // ---------------------------------------------------------------- crisis

        /// <summary>
        /// The week going wrong.
        ///
        /// <para>The reference's crises prefer the parties to an active deal, because being caught
        /// listening to two people who have actually agreed something is worse than being caught
        /// listening to nobody in particular. That preference could not be ported until deals
        /// existed; it can now.</para>
        /// </summary>
        public static HouseEventState Crisis(EpisodeState state, double roll, long sequence)
        {
            if (state == null || state.week < CrisisFirstWeek) return null;
            var npcs = state.Active.Where(c => !c.isPlayer)
                .OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            if (npcs.Count < 2) return null;

            // Whoever has actually agreed something, if anybody has.
            var deal = state.deals
                .Where(d => d.status == DealStatus.Active
                            && d.proposerId != state.playerId && d.recipientId != state.playerId)
                .OrderBy(d => d.id, StringComparer.Ordinal)
                .FirstOrDefault(d => state.Find(d.proposerId)?.status == ContestantStatus.Active
                                     && state.Find(d.recipientId)?.status == ContestantStatus.Active);

            var first = deal != null ? state.Find(deal.proposerId) : npcs[Index(roll, npcs.Count)];
            var second = deal != null ? state.Find(deal.recipientId)
                : npcs[(Index(roll, npcs.Count) + 1) % npcs.Count];
            if (first == null || second == null || first.id == second.id) return null;

            return new HouseEventState
            {
                id = "house-event-" + sequence,
                kind = HouseEventKind.Crisis,
                title = "Caught Eavesdropping",
                narrative = "You overhear " + first.name + " and " + second.name + " whispering about "
                    + "nominations. Just as you lean in closer, " + first.name + " spots you.",
                involvedIds = new List<string> { first.id, second.id },
                week = state.week,
                resolved = false,
                chosenIndex = -1,
                choices = new List<HouseEventChoice>
                {
                    new HouseEventChoice
                    {
                        label = "Deny everything", description = "\"I was just grabbing cereal.\"",
                        risk = HouseEventRisk.High, trustChange = -2,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = first.id, amount = -5 },
                        },
                    },
                    new HouseEventChoice
                    {
                        label = "Own it", description = "\"I heard everything. Want to include me?\"",
                        risk = HouseEventRisk.Medium, trustChange = 1,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = first.id, amount = 2 },
                        },
                    },
                    new HouseEventChoice
                    {
                        label = "Apologise and leave", description = "\"Sorry. I'll give you space.\"",
                        risk = HouseEventRisk.Low, trustChange = 0,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = first.id, amount = -1 },
                        },
                    },
                },
            };
        }

        // ---------------------------------------------------------------- shared

        /// <summary>A roll turned into an index, never stepping off the end.</summary>
        private static int Index(double roll, int count)
        {
            if (count <= 0) return 0;
            if (double.IsNaN(roll) || roll < 0) return 0;
            int value = (int)(roll * count);
            return value >= count ? count - 1 : value;
        }
    }
}
