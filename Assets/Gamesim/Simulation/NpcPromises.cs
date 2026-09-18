using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Houseguests giving each other their word.
    ///
    /// <para>The companion to <see cref="NpcAlliances"/>, and the other half of the finding that
    /// prompted both: <c>promises.Add</c> appeared exactly once in the whole simulation, on the
    /// player's path. Nobody in this house had ever promised anybody anything unless the player was
    /// one of the two people involved.</para>
    ///
    /// <para>Ported from the reference build's <c>shouldNPCMakePromise</c>, whose organising idea is
    /// worth stating plainly: <b>promise type follows game position, not affection</b>. A nominee
    /// begs for votes because they are on the block, not because they like you. Somebody courts the
    /// Head of Household because of the title, not the person. Warmth only decides the fourth case,
    /// and the endgame decides the fifth.</para>
    ///
    /// <para>Like the alliance pass, this spends no randomness: every branch is a function of state,
    /// so a houseguest's word costs the season's generator nothing.</para>
    /// </summary>
    public static class NpcPromises
    {
        /// <summary>The source's <c>NPC_PROMISE_THRESHOLD</c>: warm enough to start laying groundwork.</summary>
        public const double WarmthThreshold = 30;

        /// <summary>Above this, at six or fewer, houseguests start shopping for a final two.</summary>
        public const double FinalTwoThreshold = 60;

        /// <summary>The house size at which the endgame starts shaping who talks to whom.</summary>
        public const int EndgameSize = 6;

        /// <summary>
        /// What one houseguest would offer another, if anything.
        ///
        /// <para>Checked in the source's order, because the order <i>is</i> the rule: desperation
        /// first, then power, then warmth, then the endgame.</para>
        /// </summary>
        public static PromiseKind? Offer(EpisodeState state, string npcId, string targetId)
        {
            var npc = state.Find(npcId);
            var target = state.Find(targetId);
            if (npc == null || target == null || npcId == targetId) return null;
            if (npc.status != ContestantStatus.Active || target.status != ContestantStatus.Active) return null;

            bool allied = state.Allied(npcId, targetId);
            double relationship = state.Score(npcId, targetId);

            // On the block, and talking to somebody who is not.
            if (state.nominees.Contains(npcId) && !state.nominees.Contains(targetId))
                return PromiseKind.Vote;

            // They hold the power and we have no claim on them.
            if (state.hohId == targetId && !allied)
                return PromiseKind.Safety;

            // Warm but unallied: lay the groundwork for a pact.
            if (relationship > WarmthThreshold && !allied)
                return PromiseKind.AllianceLoyalty;

            // Late, close, and nothing agreed yet.
            if (relationship > FinalTwoThreshold && state.Active.Count() <= EndgameSize
                && !state.promises.Any(p => p.fromId == npcId && p.kind == PromiseKind.FinalTwo
                                            && p.status == PromiseStatus.Active))
                return PromiseKind.FinalTwo;

            return null;
        }

        /// <summary>
        /// The weekly pass: each houseguest may give their word to one person.
        ///
        /// <para>One apiece, in cast order, so a house full of warm feelings does not produce forty
        /// promises in an evening and exhaust the two hundred a season may hold. The recipient is
        /// whoever they are closest to among those they would say something to — ties broken on id,
        /// so the same house always makes the same offers.</para>
        /// </summary>
        public static void Settle(EpisodeState state)
        {
            if (!NpcSocialState.AutonomyHasBegun(state)) return;
            foreach (var npc in state.contestants.Where(c => c.status == ContestantStatus.Active && !c.isPlayer))
            {
                if (state.promises.Count >= PromiseCeiling) return;

                var chosen = state.contestants
                    .Where(other => other.status == ContestantStatus.Active && other.id != npc.id)
                    .Select(other => new { other.id, kind = Offer(state, npc.id, other.id) })
                    .Where(candidate => candidate.kind.HasValue && !AlreadyPromised(state, npc.id, candidate.id, candidate.kind.Value))
                    .OrderByDescending(candidate => state.Score(npc.id, candidate.id))
                    .ThenBy(candidate => candidate.id, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (chosen == null) continue;
                Give(state, npc.id, chosen.id, chosen.kind.Value);
            }
        }

        /// <summary>What validation allows a season to hold, so the pass stops short of it.</summary>
        public const int PromiseCeiling = 200;

        private static bool AlreadyPromised(EpisodeState state, string from, string to, PromiseKind kind) =>
            state.promises.Any(p => p.fromId == from && p.toId == to && p.kind == kind
                                    && p.status == PromiseStatus.Active);

        private static void Give(EpisodeState state, string from, string to, PromiseKind kind)
        {
            state.promises.Add(new PromiseState
            {
                id = "promise-npc-" + state.nextSequence,
                fromId = from, toId = to,
                targetId = kind == PromiseKind.Vote ? state.nominees.FirstOrDefault(id => id != from) : null,
                kind = kind,
                status = PromiseStatus.Active,
                week = state.week,
                // The engine's own expiry rule: a final two is open-ended, safety covers next week,
                // and everything else is good for the week it was given in.
                expiresWeek = kind == PromiseKind.FinalTwo ? 0
                    : kind == PromiseKind.Safety ? state.week + 1
                    : state.week,
            });

            RelationshipLedger.Record(state, from, to, "promise-made", 15,
                state.Find(from).name + " gave " + state.Find(to).name + " their word");
        }
    }
}
