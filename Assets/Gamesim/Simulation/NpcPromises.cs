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

            // The first two branches are positional, and both are guarded on a vote that has not
            // happened yet. That guard is this port's, not the source's, and it exists because of
            // how a week is shaped here: the social phase belongs to the END of its week, so the
            // block is still standing in state and the title still sits with somebody while the
            // vote that settled both has already been counted.
            //
            // Without it the surviving nominee spends the following week begging for votes that
            // were counted days ago, and the whole house courts an outgoing Head of Household whose
            // power is spent — they have nominated, the veto has been used and the vote is in. Both
            // branches belong to campaigning, and that is the other moment this pass runs.
            bool blockStillStands = !state.evictionResolved;

            // On the block, and talking to somebody who is not.
            if (blockStillStands && state.nominees.Contains(npcId) && !state.nominees.Contains(targetId))
                return PromiseKind.Vote;

            // They hold the power and we have no claim on them.
            if (blockStillStands && state.hohId == targetId && !allied)
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
        ///
        /// <para>The season runs this at the start of campaigning, where the block has just been
        /// settled and a promise is about the block. The social week runs it through
        /// <see cref="NpcSocialActions.Settle"/> instead, so it competes for a turn with everything
        /// else a houseguest might do with one.</para>
        /// </summary>
        public static void Settle(EpisodeState state)
        {
            if (!NpcSocialState.AutonomyHasBegun(state)) return;
            foreach (var npc in state.contestants.Where(c => c.status == ContestantStatus.Active && !c.isPlayer))
                TryGive(state, npc.id);
        }

        /// <summary>
        /// One houseguest's word for the week: whoever they are closest to among the people they
        /// would say something to, ties broken on id.
        ///
        /// <para><b>Never the player.</b> The source leaves them out of autonomous promise
        /// generation for the same reason it leaves them out of alliance generation, and until a
        /// houseguest can walk up and offer the player something the player can answer, a promise
        /// made to them is a line in a file they never see.</para>
        /// </summary>
        public static bool TryGive(EpisodeState state, string npcId)
        {
            if (state.promises.Count >= PromiseCeiling) return false;

            var chosen = state.contestants
                .Where(other => other.status == ContestantStatus.Active && !other.isPlayer && other.id != npcId)
                .Select(other => new { other.id, kind = Offer(state, npcId, other.id) })
                .Where(candidate => candidate.kind.HasValue && !AlreadyPromised(state, npcId, candidate.id, candidate.kind.Value))
                .OrderByDescending(candidate => state.Score(npcId, candidate.id))
                .ThenBy(candidate => candidate.id, StringComparer.Ordinal)
                .FirstOrDefault();

            if (chosen == null) return false;
            Give(state, npcId, chosen.id, chosen.kind.Value);
            return true;
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
