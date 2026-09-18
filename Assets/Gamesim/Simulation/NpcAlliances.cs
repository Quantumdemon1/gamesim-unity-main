using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Houseguests pairing up and falling out without the player in the room.
    ///
    /// <para>Until this existed, <c>alliances.Add</c> appeared exactly once in the whole simulation,
    /// on the player's own social path. No houseguest had ever formed an alliance with another
    /// houseguest in any season this project had run — which meant the voting-bloc system, one of
    /// the most substantial and best-tested things here, had only ever coordinated blocs the player
    /// personally built.</para>
    ///
    /// <para>Ported from the reference build's <c>evaluateAllianceDesire</c>. The standout term is
    /// <b>shared threats</b>: two people who both dislike the same third party are drawn together,
    /// fifteen points each. That is the enemy-of-my-enemy rule, and it is what makes a house form
    /// factions around a common problem rather than simply pairing off its friendliest members.</para>
    ///
    /// <para><b>Nothing here draws from the season's generator.</b> Every term is a function of
    /// state, so the whole pass is deterministic without spending a roll — which matters because a
    /// roll spent here would shift every competition and vote after it. The one thing it cannot
    /// avoid changing is the state itself: a season in which houseguests form alliances is a
    /// different season, and that is the point of it.</para>
    /// </summary>
    public static class NpcAlliances
    {
        /// <summary>The source's <c>NPC_ALLIANCE_MIN_RELATIONSHIP</c>: a floor, not a score.</summary>
        public const double MinimumRelationship = 25;

        /// <summary>The source's <c>NPC_ALLIANCE_MAX_PER_PERSON</c>.</summary>
        public const int MaximumEach = 3;

        /// <summary>Desire must clear this as well as the relationship floor.</summary>
        public const double ProposeThreshold = 25;

        /// <summary>
        /// What counts as disliking somebody, and so as a shared threat.
        ///
        /// <para>The source's own line, taken from the shared-threat term rather than invented: it
        /// counts a third party when both parties rate them below −20.</para>
        /// </summary>
        public const double DislikeLine = -20;

        /// <summary>
        /// How badly a pair has to sour before their alliance falls apart.
        ///
        /// <para><b>Authored.</b> The reference build says alliances "dissolve automatically if any
        /// pair sours badly" and shows the reducer that marks one broken, but never gives the
        /// threshold that fires it. This reuses the dislike line above rather than inventing a
        /// second number: the point at which the house would call two people enemies is a defensible
        /// point at which to say they are no longer allies.</para>
        /// </summary>
        public const double SourLine = DislikeLine;

        /// <summary>
        /// How much one houseguest wants to work with another.
        ///
        /// <para>The source's formula exactly: relationship at 0.4, shared threats at 15 apiece,
        /// strategic value at 0.15, trust either side of neutral at 0.3, less ten for every alliance
        /// already carried — diminishing returns on over-allying.</para>
        /// </summary>
        public static double Desire(EpisodeState state, string npcId, string targetId)
        {
            var target = state.Find(targetId);
            if (target == null) return 0;

            double relationship = state.Score(npcId, targetId);
            int sharedThreats = state.Active.Count(other =>
                other.id != npcId && other.id != targetId &&
                state.Score(npcId, other.id) < DislikeLine &&
                state.Score(targetId, other.id) < DislikeLine);
            double strategicValue = (target.hohWins + target.vetoWins) * 5 + target.stats.social * 3;
            double trust = ThreatAssessment.TrustScore(state, targetId, npcId);
            int carried = ActiveAlliancesFor(state, npcId).Count;

            return relationship * 0.4
                   + sharedThreats * 15
                   + strategicValue * 0.15
                   + (trust - ThreatAssessment.NeutralTrust) * 0.3
                   - carried * 10;
        }

        /// <summary>Whether this houseguest would put the idea to that one.</summary>
        public static bool WouldPropose(EpisodeState state, string npcId, string targetId)
        {
            if (npcId == targetId) return false;
            if (state.Find(npcId)?.status != ContestantStatus.Active) return false;
            if (state.Find(targetId)?.status != ContestantStatus.Active) return false;
            if (state.Allied(npcId, targetId)) return false;
            if (ActiveAlliancesFor(state, npcId).Count >= MaximumEach) return false;
            if (ActiveAlliancesFor(state, targetId).Count >= MaximumEach) return false;
            return state.Score(npcId, targetId) >= MinimumRelationship
                   && Desire(state, npcId, targetId) > ProposeThreshold;
        }

        /// <summary>
        /// The weekly pass: alliances that have soured fall apart, and houseguests who both want to
        /// work together start doing so.
        ///
        /// <para><b>Both parties must want it.</b> The source scores the proposer's desire and
        /// handles acceptance separately; this requires the feeling to be mutual, which is a
        /// simplification and is marked as one. It keeps the pass deterministic and one-directional
        /// desire would otherwise need an acceptance roll — a roll this cannot spend.</para>
        ///
        /// <para>At most one new alliance per houseguest per week, so a house that suddenly likes
        /// everybody does not pair off completely in a single evening.</para>
        ///
        /// <para><b>A season does not call this directly.</b> Alliance formation is the highest-priority
        /// thing a houseguest can do with a social turn, and turns are budgeted, so the weekly pass runs
        /// through <see cref="NpcSocialActions.Settle"/> — which is this loop with promises and the rest
        /// of the repertoire interleaved behind it. This stays because it is the alliance rule on its
        /// own, which is the only way to state what that rule is without the rest of the week in the
        /// way.</para>
        /// </summary>
        public static void Settle(EpisodeState state)
        {
            if (!NpcSocialState.AutonomyHasBegun(state)) return;
            Dissolve(state);

            // Cast order throughout, so the same house always pairs up the same way.
            var joined = new HashSet<string>(StringComparer.Ordinal);
            foreach (var npc in state.contestants.Where(c => c.status == ContestantStatus.Active && !c.isPlayer))
                TryPropose(state, npc.id, joined);
        }

        /// <summary>
        /// One houseguest's alliance attempt for the week: the highest-desire partner who wants it
        /// back, or nothing.
        ///
        /// <para><paramref name="joined"/> is the set of people who have already paired off in this
        /// pass. It is what holds the pass to one new alliance per houseguest per week; without it a
        /// house that suddenly likes everybody pairs off completely in a single evening.</para>
        ///
        /// <para><b>Never the player.</b> The source excludes them from autonomous alliance
        /// generation outright, and the reason is worth stating: acceptance is decided here by
        /// mutual desire, and the player's side of that is computed from their scores rather than
        /// asked of them. Including them would enrol a player in an alliance they never agreed to,
        /// and the player's own <c>FormAlliance</c> is how they join one.</para>
        /// </summary>
        public static bool TryPropose(EpisodeState state, string npcId, ISet<string> joined)
        {
            if (joined != null && joined.Contains(npcId)) return false;

            string partner = state.contestants
                .Where(other => other.status == ContestantStatus.Active
                                && !other.isPlayer
                                && other.id != npcId
                                && (joined == null || !joined.Contains(other.id))
                                && WouldPropose(state, npcId, other.id)
                                && WouldPropose(state, other.id, npcId))
                .OrderByDescending(other => Desire(state, npcId, other.id))
                .ThenBy(other => other.id, StringComparer.Ordinal)
                .Select(other => other.id)
                .FirstOrDefault();

            if (partner == null) return false;
            Form(state, npcId, partner);
            if (joined != null) { joined.Add(npcId); joined.Add(partner); }
            return true;
        }

        /// <summary>Every active alliance this houseguest belongs to.</summary>
        public static List<AllianceState> ActiveAlliancesFor(EpisodeState state, string id) =>
            state.alliances.Where(a => a.active && a.members.Contains(id)).ToList();

        // ---------------------------------------------------------------- internals

        private static void Form(EpisodeState state, string first, string second)
        {
            string name = "The " + state.Find(first).name.Split(' ')[0]
                          + " and " + state.Find(second).name.Split(' ')[0] + " Pact";
            state.alliances.Add(new AllianceState
            {
                id = "alliance-npc-" + state.nextSequence,
                name = name,
                members = new List<string> { first, second },
                active = true,
            });
            RelationshipLedger.Record(state, first, second, "alliance-formed", 30, name + " was formed");
        }

        /// <summary>
        /// An alliance ends when either side has come to dislike the other.
        ///
        /// <para>No ledger entry, and deliberately so: this is not something either of them did. A
        /// pact that dies because two people drifted apart is not a betrayal, and recording it as
        /// one would put a permanent grudge on the books that nobody earned.</para>
        /// </summary>
        public static void Dissolve(EpisodeState state)
        {
            foreach (var alliance in state.alliances.Where(a => a.active).ToList())
            {
                bool soured = alliance.members.Any(one =>
                    alliance.members.Any(other => one != other && state.Score(one, other) <= SourLine));
                bool intact = alliance.members.Count(id => state.Find(id)?.status == ContestantStatus.Active) >= 2;
                if (soured || !intact) alliance.active = false;
            }
        }

    }
}
