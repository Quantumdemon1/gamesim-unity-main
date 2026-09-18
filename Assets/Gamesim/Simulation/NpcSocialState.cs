using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>Durable native clock and source-conversation inputs, never Unity world poses.</summary>
    [Serializable]
    public sealed class NpcSocialState
    {
        public int rulesStartWeek = 1;
        public long clockTick, nextScanTick = 3, nextConversationSequence = 1;
        public uint randomState;
        public List<NpcConversationState> pending = new List<NpcConversationState>();
        public List<NpcCooldownState> cooldowns = new List<NpcCooldownState>();
        public List<NpcPairMemoryState> pairMemory = new List<NpcPairMemoryState>();

        public static uint InitialRandomState(uint seed) => SeededRandom.HashSeed(
            "gamesim:npc-social:v1:" + seed.ToString("x8", CultureInfo.InvariantCulture));

        public static NpcSocialState Create(uint seed, int startWeek = 1)
        {
            if (startWeek < 1 || startWeek > 101) throw new ArgumentOutOfRangeException(nameof(startWeek));
            return new NpcSocialState { rulesStartWeek = startWeek, randomState = InitialRandomState(seed) };
        }

        /// <summary>
        /// Whether houseguests are acting on their own account yet.
        ///
        /// <para>The same boundary the conversation scheduler uses, and for the same reason. A
        /// season saved before houseguests formed their own alliances and gave their own word keeps
        /// the week it was in; autonomy starts with the next one. Recorded fixtures set it beyond
        /// their own length, which is how a replay stays the season it recorded rather than the
        /// season it would be if run today.</para>
        ///
        /// <para>Phase-independent, unlike <see cref="IsEligible"/>: alliances are settled as the
        /// social week opens and words are given at two points in the week, so the callers decide
        /// when, and this decides whether.</para>
        /// </summary>
        public static bool AutonomyHasBegun(EpisodeState state) =>
            state?.npcSocial != null && state.week >= state.npcSocial.rulesStartWeek;

        public static bool IsEligiblePhase(EpisodePhase phase) => phase == EpisodePhase.Social || phase == EpisodePhase.Campaign;
        public static bool IsEligible(EpisodeState state) => state?.npcSocial != null &&
            state.week >= state.npcSocial.rulesStartWeek && IsEligiblePhase(state.phase) && state.pendingDiary == null &&
            state.Find(state.playerId)?.status == ContestantStatus.Active && state.Active.Count(actor => !actor.isPlayer) >= 2;

        public static bool IsKnownTopic(string topic) => topic == "bonding" || topic == "strategy" || topic == "gossip" ||
            topic == "tension" || topic == "casual" || topic == "nominations" || topic == "alliance_talk" || topic == "rivalry";

        public static bool IsKnownRendezvous(string id) => id == "living-east-chat" || id == "kitchen-west-chat" ||
            id == "bedroom-south-chat" || id == "yard-south-chat";

        public NpcSocialState Clone()
        {
            var copy = (NpcSocialState)MemberwiseClone();
            copy.pending = pending.Select(row => row.Clone()).ToList();
            copy.cooldowns = cooldowns.Select(row => row.Clone()).ToList();
            copy.pairMemory = pairMemory.Select(row => row.Clone()).ToList();
            return copy;
        }
    }

    [Serializable]
    public sealed class NpcConversationState
    {
        public long sequence, startedTick;
        public string firstId, secondId, topic, rendezvousId;
        public int week;
        public EpisodePhase phase;
        public double durationMs;
        public NpcConversationState Clone() => (NpcConversationState)MemberwiseClone();
    }

    [Serializable]
    public sealed class NpcCooldownState
    {
        public string npcId;
        public long untilTick;
        public NpcCooldownState Clone() => (NpcCooldownState)MemberwiseClone();
    }

    [Serializable]
    public sealed class NpcPairMemoryState
    {
        public string fromId, toId, lastTopic;
        public int count;
        public long lastStartTick;
        public NpcPairMemoryState Clone() => (NpcPairMemoryState)MemberwiseClone();
    }
}
