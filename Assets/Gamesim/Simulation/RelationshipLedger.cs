using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What the house remembers, and for how long.
    ///
    /// <para>Every relationship already carried an event log — <see cref="RelationshipEventState"/>
    /// with a type, an impact and a <c>decayable</c> flag. The structure was right and the flag was
    /// dead: it was written <c>true</c> at the single call site that produced events and read
    /// nowhere at all, so every act in the house aged at the same rate, which is to say none.</para>
    ///
    /// <para><b>The asymmetry is the point.</b> The reference build's own note on this system is
    /// that betrayals never decay, and that this is the main reason its house develops long memories
    /// and grudges. A conversation should fade; being put on the block should not. Without the
    /// exception, decay makes betrayal cheap; without decay, the exception distinguishes nothing.
    /// Both halves only mean something together.</para>
    ///
    /// <para>This decays an event's <i>contribution to what is derived from the ledger</i> — trust,
    /// recent impact, reputation — rather than the stored relationship score. The score is the
    /// running total the engine maintains through <c>Change</c>; rewriting it on a timer would make
    /// a committed number drift under a save that nobody touched.</para>
    /// </summary>
    public static class RelationshipLedger
    {
        /// <summary>
        /// How long a decaying act keeps its full weight before it starts fading, in weeks.
        /// The source's decision factors read the last two weeks as "recent".
        /// </summary>
        public const int RecentWeeks = 2;

        /// <summary>How many weeks a decaying act takes to fade to nothing once it starts.</summary>
        public const int FadeWeeks = 4;

        /// <summary>
        /// The acts that never fade, mapped from the reference build's interaction defaults.
        ///
        /// <para>Its list is longer than this one because it records interactions this port does not
        /// model separately. What matters is the rule behind the list rather than its length: an act
        /// that changed someone's standing <i>in the game</i> — put them on the block, took them off
        /// it, voted them out, broke a word — is permanent. An act that was only ever social fades.
        /// </para>
        /// </summary>
        private static readonly HashSet<string> Permanent = new HashSet<string>(StringComparer.Ordinal)
        {
            "nominated",        // -25 in the source, and the house does not forget who put them up
            "saved-with-veto",  // +40
            "voted-against",    // -20
            "promise-made",     // +15
            "promise-kept",     // +25
            "promise-broken",   // -40
            "alliance-formed",  // +30
            "alliance-betrayed" // -50, the heaviest single entry in the source's table
        };

        /// <summary>Whether an act of this type fades with time.</summary>
        public static bool Decays(string type) => type == null || !Permanent.Contains(type);

        /// <summary>
        /// What an act still counts for, given how long ago it happened.
        ///
        /// <para>Permanent acts count in full forever. Everything else holds its weight for
        /// <see cref="RecentWeeks"/> and then fades linearly to nothing over <see cref="FadeWeeks"/>.
        /// </para>
        /// </summary>
        public static double Weight(RelationshipEventState entry, int currentWeek)
        {
            if (entry == null) return 0;
            if (!entry.decayable) return 1;
            int age = Math.Max(0, currentWeek - entry.week);
            if (age <= RecentWeeks) return 1;
            double faded = 1 - (age - RecentWeeks) / (double)FadeWeeks;
            return Math.Max(0, faded);
        }

        /// <summary>Everything one houseguest still holds against — or in favour of — another.</summary>
        public static double WeightedImpact(EpisodeState state, string from, string to)
        {
            var edge = Edge(state, from, to);
            return edge == null ? 0 : edge.events.Sum(entry => entry.impactScore * Weight(entry, state.week));
        }

        /// <summary>
        /// The last two weeks only.
        ///
        /// <para>The source feeds this into its decision factors at the highest raw multiplier it
        /// uses, which is what lets a fresh betrayal reorder somebody's targets immediately while
        /// the permanent ledger governs long-term trust.</para>
        /// </summary>
        public static double RecentImpact(EpisodeState state, string from, string to)
        {
            var edge = Edge(state, from, to);
            return edge == null ? 0
                : edge.events.Where(entry => state.week - entry.week <= RecentWeeks)
                    .Sum(entry => entry.impactScore);
        }

        /// <summary>The heaviest thing still on the record between two houseguests, if any.</summary>
        public static RelationshipEventState MostSignificant(EpisodeState state, string from, string to)
        {
            var edge = Edge(state, from, to);
            return edge?.events
                .OrderByDescending(entry => Math.Abs(entry.impactScore * Weight(entry, state.week)))
                .FirstOrDefault();
        }

        /// <summary>Whether anything permanent and negative sits between them.</summary>
        public static bool HoldsAGrudge(EpisodeState state, string from, string to)
        {
            var edge = Edge(state, from, to);
            return edge != null && edge.events.Any(entry => !entry.decayable && entry.impactScore < 0);
        }

        /// <summary>
        /// Writes both directions of an act onto the record, without touching either score or the
        /// season's generator.
        ///
        /// <para>The engine's own relationship path rolls for a reciprocal delta, which is exactly
        /// what an autonomous houseguest must not do — a roll spent outside a player's command
        /// shifts every competition and vote after it. Acts recorded here are symmetric by
        /// construction, so there is nothing to roll for.</para>
        ///
        /// <para>This records what happened. It does not move anybody's standing; a caller that
        /// wants the score to change says so separately, through the engine.</para>
        /// </summary>
        public static void Record(EpisodeState state, string from, string to,
            string type, double impact, string description)
        {
            if (state == null || from == to) return;
            foreach (var pair in new[] { (from, to), (to, from) })
            {
                var edge = Edge(state, pair.Item1, pair.Item2);
                if (edge == null)
                {
                    edge = new RelationshipState { fromId = pair.Item1, toId = pair.Item2, score = 0 };
                    state.relationships.Add(edge);
                }
                edge.events.Add(new RelationshipEventState
                {
                    sequence = state.nextSequence++, week = state.week, type = type,
                    description = description, impactScore = impact, decayable = Decays(type),
                });
                if (edge.events.Count > 512) edge.events.RemoveAt(0);
            }
        }

        /// <summary>
        /// Moves two houseguests' standing with each other, symmetrically and without a roll.
        ///
        /// <para>The engine's own <c>Change</c> is the right path whenever the player is one of the
        /// two, and this is the right path whenever they are not — not to avoid the randomness, but
        /// because of what else <c>Change</c> does. It updates <c>relationshipArcs</c>, and an arc
        /// is keyed by one houseguest with no record of who it is <i>with</i>: every line it
        /// produces reads "Your rivalry with X". Feeding it from conversations the player was never
        /// part of would have the house's own traffic accumulate into the player's feuds and
        /// friendships, and <see cref="ThreatAssessment"/> and the eviction vote both read those.
        /// </para>
        ///
        /// <para>So this writes the two scores and the interaction week, and nothing else. What
        /// happened is recorded separately through <see cref="Record"/>.</para>
        /// </summary>
        public static void Move(EpisodeState state, string from, string to, double delta)
        {
            if (state == null || from == to) return;
            Write(state, from, to, delta);
            Write(state, to, from, delta);
            foreach (var edge in state.relationships.Where(r =>
                         (r.fromId == from && r.toId == to) || (r.fromId == to && r.toId == from)))
                edge.lastInteractionWeek = state.week;
        }

        private static void Write(EpisodeState state, string from, string to, double delta)
        {
            var edge = Edge(state, from, to);
            if (edge == null)
            {
                edge = new RelationshipState { fromId = from, toId = to };
                state.relationships.Add(edge);
            }
            edge.score = WebRules.ClampScore(edge.score + delta);
        }

        private static RelationshipState Edge(EpisodeState state, string from, string to) =>
            state?.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
    }
}
