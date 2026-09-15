using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public static partial class EpisodeValidation
    {
        private static bool TryValidateNpcSocial(EpisodeState state, out string error)
        {
            error = null;
            var social = state.npcSocial;
            if (social == null || social.rulesStartWeek < 1 || social.rulesStartWeek > 101 || social.rulesStartWeek > state.week + 1)
                return Fail(out error, "Invalid NPC social activation week.");
            // Each clock commit advances one whole second; a tick can issue two disjoint starts.
            // Bulk multi-tick commits are intentionally not part of this persisted contract.
            if (social.clockTick < 0 || social.clockTick > state.revision || social.nextScanTick <= social.clockTick ||
                social.nextScanTick > social.clockTick + 3 || social.nextScanTick % 3 != 0 ||
                social.nextConversationSequence < 1 || social.nextConversationSequence - 1 > 2L * state.revision)
                return Fail(out error, "Invalid NPC social clock or issued sequence.");
            if (social.pending == null || social.pending.Count > 2 || social.pending.Any(row => row == null) ||
                social.cooldowns == null || social.cooldowns.Count > 5 || social.cooldowns.Any(row => row == null) ||
                social.pairMemory == null || social.pairMemory.Count > 20 || social.pairMemory.Any(row => row == null))
                return Fail(out error, "Invalid NPC social collections.");
            bool Npc(string id) => state.contestants.Any(actor => actor.id == id && !actor.isPlayer);
            bool ActiveNpc(string id) => state.contestants.Any(actor => actor.id == id && !actor.isPlayer && actor.status == ContestantStatus.Active);
            if (social.cooldowns.Any(row => !Npc(row.npcId) || row.untilTick < 0 || row.untilTick > social.clockTick + 15) ||
                social.cooldowns.Select(row => row.npcId).Distinct(StringComparer.Ordinal).Count() != social.cooldowns.Count)
                return Fail(out error, "Invalid NPC conversation cooldowns.");
            if (social.pairMemory.Any(row => !Npc(row.fromId) || !Npc(row.toId) || row.fromId == row.toId ||
                !NpcSocialState.IsKnownTopic(row.lastTopic) || row.count < 1 || row.count >= social.nextConversationSequence ||
                row.lastStartTick < 0 || row.lastStartTick > social.clockTick) ||
                social.pairMemory.GroupBy(row => new { row.fromId, row.toId }).Any(group => group.Count() != 1))
                return Fail(out error, "Invalid directed NPC conversation memory.");
            foreach (var row in social.pairMemory)
            {
                var reverse = social.pairMemory.SingleOrDefault(other => other.fromId == row.toId && other.toId == row.fromId);
                if (reverse == null || reverse.count != row.count || reverse.lastTopic != row.lastTopic || reverse.lastStartTick != row.lastStartTick)
                    return Fail(out error, "NPC conversation memory must preserve both recorded directions.");
            }
            if (social.pending.Count > 0 && !NpcSocialState.IsEligible(state))
                return Fail(out error, "Pending NPC conversations require eligible free time and an active player.");
            long previousSequence = 0;
            foreach (var row in social.pending)
            {
                if (row.sequence <= previousSequence || row.sequence >= social.nextConversationSequence ||
                    !ActiveNpc(row.firstId) || !ActiveNpc(row.secondId) || row.firstId == row.secondId ||
                    !NpcSocialState.IsKnownTopic(row.topic) || !NpcSocialState.IsKnownRendezvous(row.rendezvousId) ||
                    row.week != state.week || row.phase != state.phase || row.startedTick < 0 || row.startedTick > social.clockTick ||
                    !Finite(row.durationMs) || row.durationMs < 8000 || row.durationMs > 40000 ||
                    (social.clockTick - row.startedTick) * 1000L >= row.durationMs)
                    return Fail(out error, "Invalid, stale, or expired pending NPC conversation.");
                if (((row.topic == "alliance_talk" || row.topic == "rivalry") && row.durationMs < 20000) ||
                    (row.topic == "nominations" && (row.durationMs < 15000 || row.durationMs > 30000)))
                    return Fail(out error, "NPC conversation duration does not match its source topic.");
                var memory = social.pairMemory.SingleOrDefault(item => item.fromId == row.firstId && item.toId == row.secondId);
                if (memory == null || memory.lastStartTick != row.startedTick || memory.lastTopic != row.topic ||
                    social.cooldowns.Any(item => (item.npcId == row.firstId || item.npcId == row.secondId) && item.untilTick > row.startedTick))
                    return Fail(out error, "Pending NPC conversation lacks its accepted start memory or violates cooldown.");
                previousSequence = row.sequence;
            }
            var participants = social.pending.SelectMany(row => new[] { row.firstId, row.secondId }).ToArray();
            if (participants.Distinct(StringComparer.Ordinal).Count() != participants.Length)
                return Fail(out error, "An NPC cannot participate in two pending conversations.");
            if (social.pending.Select(row => row.rendezvousId).Distinct(StringComparer.Ordinal).Count() != social.pending.Count)
                return Fail(out error, "A two-seat rendezvous cannot host two pending conversations.");
            if (state.week < social.rulesStartWeek && (social.clockTick != 0 || social.nextScanTick != 3 ||
                social.nextConversationSequence != 1 || social.randomState != NpcSocialState.InitialRandomState(state.seed) ||
                social.pending.Count != 0 || social.cooldowns.Count != 0 || social.pairMemory.Count != 0))
                return Fail(out error, "Deferred NPC social rules cannot contain retroactive activity.");
            // Every uint state, including zero after generator wraparound, is legal once activated.
            return true;
        }
    }
}
