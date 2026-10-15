using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    // Trusted in-process requests, not player commands, persisted receipts or a network API.
    public enum NpcOperationKind { Tick, Start, Cancel }

    public sealed class NpcWorldReadyEvidence
    {
        public long sequence, observedClockTick;
        public string firstId, secondId, rendezvousId;
    }

    public sealed class NpcOperationRequest
    {
        public NpcOperationKind kind;
        public string sessionId;
        public int expectedRevision;
        public EpisodePhase expectedPhase;
        public long expectedClockTick, targetClockTick, sequence;
        public string firstId, secondId, rendezvousId;
        // Root runtime attests current free-roam/modal and physical readiness. This
        // DTO is not a substitute for NavMesh/room/LOS/load-generation checks.
        public bool freeRoamReady;
        public List<NpcWorldReadyEvidence> worldReady = new List<NpcWorldReadyEvidence>();
    }

    public sealed class NpcOperationResult
    {
        public bool accepted, duplicate, scanDue;
        public string reason;
        public EpisodeState candidate;
        // Coordinator diagnostics only; do not render mechanical/private evidence in HUD.
        public List<long> completedSequences = new List<long>(), cancelledSequences = new List<long>();
    }

    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Prepare a detached trusted candidate. This NEVER installs current. The root
        /// coordinator must save successfully, then construct/install a new engine; a
        /// failed save or abandoned candidate consumes no authority or random draws.
        /// </summary>
        public NpcOperationResult PrepareNpcOperation(NpcOperationRequest request)
        {
            NpcOperationResult Reject(string reason) => new NpcOperationResult { reason = reason, candidate = Snapshot };
            NpcOperationResult Duplicate() => new NpcOperationResult { duplicate = true, reason = "This NPC operation was already finalized.", candidate = Snapshot };
            if (request == null || request.sessionId != current.sessionId) return Reject("The NPC operation belongs to another or missing session.");
            if (!Enum.IsDefined(typeof(NpcOperationKind), request.kind)) return Reject("Unknown NPC operation.");
            if (request.expectedClockTick < 0 || request.targetClockTick < 0) return Reject("NPC clocks cannot be negative.");
            var social = current.npcSocial;
            if (request.kind == NpcOperationKind.Tick)
            {
                if (request.expectedClockTick == long.MaxValue || request.targetClockTick != request.expectedClockTick + 1 || request.sequence != 0)
                    return Reject("A clock operation must advance exactly one eligible second.");
                if (request.targetClockTick <= social.clockTick) return Duplicate();
            }
            else
            {
                if (request.targetClockTick != request.expectedClockTick || request.sequence < 1)
                    return Reject("Start/cancel must preserve the current clock and identify a positive conversation sequence.");
                var pending = social.pending.SingleOrDefault(row => row.sequence == request.sequence);
                // Membership is checked BEFORE watermark finalization: a slow sequence1
                // is still pending after sequence2 has completed. Never use maxCompleted.
                if (request.sequence < social.nextConversationSequence && pending == null) return Duplicate();
                if (request.kind == NpcOperationKind.Start && pending != null)
                {
                    if (pending.firstId != request.firstId || pending.secondId != request.secondId || pending.rendezvousId != request.rendezvousId)
                        return Reject("An issued conversation sequence cannot be reused for another pair or venue.");
                    return Duplicate();
                }
            }
            if (request.expectedRevision != current.revision || request.expectedPhase != current.phase || request.expectedClockTick != social.clockTick)
                return Reject("The episode or NPC clock changed. Refresh world evidence before retrying.");
            var next = current.Clone();
            var result = new NpcOperationResult();
            try
            {
                if (request.kind == NpcOperationKind.Cancel)
                {
                    var row = next.npcSocial.pending.SingleOrDefault(item => item.sequence == request.sequence);
                    Require(row != null, "Only an issued pending conversation can be cancelled.");
                    next.npcSocial.pending.Remove(row); result.cancelledSequences.Add(row.sequence);
                    // A cancelled accepted start keeps its memory and topic/duration draws.
                    // No completion effects, completion RNG, cooldown, or log is invented.
                }
                else
                {
                    Require(request.freeRoamReady && NpcSocialState.IsEligible(next), "NPC time requires eligible active-player free roam without a blocking modal.");
                    if (request.kind == NpcOperationKind.Start) StartNpcConversation(next, request);
                    else TickNpcSocial(next, request, result);
                }
                next.revision = checked(current.revision + 1);
                if (!Valid(next, out var error)) throw new RuleException("NPC candidate rejected: " + error);
                result.accepted = true; result.reason = "Prepared; save before install."; result.candidate = next;
                return result;
            }
            catch (RuleException error) { return Reject(error.Message); }
            catch (ArgumentException error) { return Reject("Invalid NPC source input: " + error.Message); }
            catch (OverflowException) { return Reject("NPC clock or sequence exceeds supported bounds."); }
        }

        private static void StartNpcConversation(EpisodeState state, NpcOperationRequest request)
        {
            var social = state.npcSocial;
            Require(request.sequence == social.nextConversationSequence, "Refresh the next issued NPC conversation sequence.");
            bool ActiveNpc(string id) => state.Active.Any(actor => actor.id == id && !actor.isPlayer);
            Require(ActiveNpc(request.firstId) && ActiveNpc(request.secondId) && request.firstId != request.secondId,
                "A conversation requires two distinct active NPCs, never the player.");
            Require(NpcSocialState.IsKnownRendezvous(request.rendezvousId), "Choose an authored NPC rendezvous.");
            Require(social.pending.Count < 2 && !social.pending.Any(row => row.firstId == request.firstId || row.secondId == request.firstId ||
                row.firstId == request.secondId || row.secondId == request.secondId), "An NPC is already in a pending conversation.");
            Require(!social.pending.Any(row => row.rendezvousId == request.rendezvousId), "The authored pair rendezvous is already occupied.");
            Require(!social.cooldowns.Any(row => (row.npcId == request.firstId || row.npcId == request.secondId) && row.untilTick > social.clockTick),
                "A participant is still on conversation cooldown.");
            Require(request.worldReady != null && request.worldReady.Count == 1 && MatchesWorld(request.worldReady[0], request.sequence,
                request.firstId, request.secondId, request.rendezvousId, social.clockTick), "Current matching physical readiness is required for the start.");
            var first = state.Find(request.firstId); var second = state.Find(request.secondId);
            var forward = FindNpcMemory(social, first.id, second.id); var reverse = FindNpcMemory(social, second.id, first.id);
            var input = new WebNpcConversationTopicInput
            {
                traits1 = new List<string>(first.traits), traits2 = new List<string>(second.traits), score = state.Score(first.id, second.id),
                areAllied = state.Allied(first.id, second.id), memory = SourceNpcMemory(forward),
                // Native Campaign is a free-roam adapter, not a web GamePhase. Neither
                // it nor source SocialInteraction gets a phase-specific nomination boost.
                gameContext = new WebNpcConversationGameContext { phase = state.phase == EpisodePhase.Social ? "SocialInteraction" : "Eviction",
                    nominees = new List<string>(state.nominees), recentEvictees = null }, seekIntent = null
            };
            // Native events carry no involved-houseguest IDs. Use real evidence presence
            // at the same source multiplication boundary instead of fabricating evictees.
            bool recentEviction = state.events.Any(item => item.kind == "eviction" && item.week >= state.week - 1);
            string topic = WebNpcConversations.PickTopic(input, recentEviction, () => NpcRoll(state));
            double duration = WebNpcConversations.DurationMilliseconds(topic, input.areAllied, input.score, () => NpcRoll(state));
            var memory = WebNpcConversations.RecordPairMemory(first.id, second.id, topic, social.clockTick * 1000d,
                SourceNpcMemory(forward), SourceNpcMemory(reverse));
            InstallNpcMemory(social, first.id, second.id, memory.forward, social.clockTick);
            InstallNpcMemory(social, second.id, first.id, memory.reverse, social.clockTick);
            social.pending.Add(new NpcConversationState { sequence = request.sequence, firstId = first.id, secondId = second.id,
                rendezvousId = request.rendezvousId, startedTick = social.clockTick, durationMs = duration, topic = topic, week = state.week, phase = state.phase });
            social.nextConversationSequence = checked(social.nextConversationSequence + 1);
        }

        private static void TickNpcSocial(EpisodeState state, NpcOperationRequest request, NpcOperationResult result)
        {
            var social = state.npcSocial;
            Require(request.worldReady != null && request.worldReady.Count == social.pending.Count &&
                request.worldReady.All(row => row != null) && request.worldReady.Select(row => row.sequence).Distinct().Count() == request.worldReady.Count,
                "A tick requires exactly one current world proof for every pending pair.");
            foreach (var row in social.pending)
                Require(request.worldReady.Any(evidence => MatchesWorld(evidence, row.sequence, row.firstId, row.secondId, row.rendezvousId, social.clockTick)),
                    "A pending pair lost physical readiness. Cancel or restore it before advancing NPC time.");
            social.clockTick = request.targetClockTick;
            if (social.clockTick >= social.nextScanTick)
            { social.nextScanTick = checked(social.nextScanTick + 3); result.scanDue = true; }
            social.cooldowns.RemoveAll(row => row.untilTick <= social.clockTick);
            // Persisted insertion order is validated sequence order. Do not sort by due
            // time, venue, actor name, or caller evidence order; each prior effect is committed
            // into this candidate before the next due completion consumes its own stream.
            foreach (var row in social.pending.ToArray())
            {
                if ((social.clockTick - row.startedTick) * 1000d < row.durationMs) continue;
                var completion = WebNpcConversations.Complete(new WebNpcConversationCompletionInput
                {
                    participants = new List<string> { row.firstId, row.secondId }, topic = row.topic, playerId = state.playerId,
                    traits1 = new List<string>(state.Find(row.firstId).traits), traits2 = new List<string>(state.Find(row.secondId).traits),
                    memory = SourceNpcMemory(FindNpcMemory(social, row.firstId, row.secondId)),
                    allNpcIds = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList()
                }, () => NpcRoll(state));
                // Under agency the pair's temperaments bias the outcome (NPC-AGENCY-PLAN.md §3.1):
                // kindred pairs never come out of a chat colder, oil and water rarely warmer.
                int delta = completion.delta + ConversationBias(state, row.firstId, row.secondId);
                if (delta != 0) CompletionChange(state, row, row.firstId, row.secondId, delta, NpcConversationEvent);
                if (completion.gossipTarget != null)
                {
                    CompletionChange(state, row, row.firstId, completion.gossipTarget.id, completion.gossipTarget.delta, NpcGossipEvent);
                    CompletionChange(state, row, row.secondId, completion.gossipTarget.id, completion.gossipTarget.delta, NpcGossipEvent);
                }
                SetNpcCooldown(social, row.firstId); SetNpcCooldown(social, row.secondId);
                social.pending.Remove(row); result.completedSequences.Add(row.sequence);
            }
        }

        /// <summary>
        /// The ledger's types for what a completed conversation did under D2's rules (<see cref="CompletionChange"/>):
        /// the pair's own talk, and what the two of them said about a third. Both fade (<see cref="RelationshipLedger.Decays"/>),
        /// and neither is a type anything else reads by name, so a conversation in the house counts for trust and nothing more.
        /// </summary>
        public const string NpcConversationEvent = "npc-conversation", NpcGossipEvent = "npc-gossip";

        /// <summary>
        /// How many of the season's sequence ids the house's talk has taken from <paramref name="fromSequence"/> on: the
        /// <see cref="NpcConversationEvent"/> and <see cref="NpcGossipEvent"/> records between two houseguests, one id each
        /// (<see cref="RelationshipLedger.Record"/> writes both directions, so two for a pair's talk that moved them and four
        /// for a gossip). None with D2's rules off. These are the only ids free roam takes, so a check that the house left
        /// the season's own counter alone allows exactly this many (PortVerification's autonomy QA).
        /// </summary>
        public static int HouseTalkRecords(EpisodeState state, int fromSequence) =>
            state == null ? 0 : state.relationships
                .Where(r => r.fromId != state.playerId && r.toId != state.playerId)
                .Sum(r => r.events.Count(e => e.sequence >= fromSequence && (e.type == NpcConversationEvent || e.type == NpcGossipEvent)));

        /// <summary>
        /// What a completed conversation does to two houseguests' standing with each other.
        ///
        /// <para><b>Before D2's rules</b> (<see cref="AllWeekOn"/> false: every season saved before them) the engine's own
        /// path, <c>ChangeWithRoll</c> on the NPC world's stream, exactly as it always ran. That path also writes
        /// <c>relationshipArcs</c> for both houseguests, and an arc is the player's own - it feeds how each houseguest votes
        /// on the player (<see cref="WebEvictionVoting"/>) and the threat ranking (<see cref="ThreatAssessment"/>) - so in
        /// those seasons the house's chatter moved how it voted on the player (the balance review's finding 3).</para>
        ///
        /// <para><b>Under them</b> a pair of houseguests moves as <see cref="NpcSocialActions"/> moves one: through
        /// <see cref="RelationshipLedger.Move"/> and <see cref="RelationshipLedger.Record"/>, no arc. The player is never one
        /// of the two today (a conversation is between two houseguests and its gossip leaves the player out); were they,
        /// the engine's own path would stay, as it does for an act of the house's on the player. The ledger's move is
        /// symmetric, so it has no reciprocal to roll; the draw <c>ChangeWithRoll</c> spent on one is drawn and set aside,
        /// so the NPC world's stream advances exactly as it did and every later conversation draws what it would have.
        /// The season's own stream is untouched either way. Its sequence counter is not: each record takes an id
        /// (<see cref="HouseTalkRecords"/>), so what is keyed to <c>nextSequence</c> - the talk story hooks, a refused
        /// deal's counter coin, the ids of house events, deals and promises - lands differently in a season the house
        /// talked through, as it may under a new rule.</para>
        /// </summary>
        private static void CompletionChange(EpisodeState state, NpcConversationState row, string from, string to, double delta, string type)
        {
            if (!AllWeekOn(state) || from == state.playerId || to == state.playerId)
            {
                ChangeWithRoll(state, from, to, delta, () => NpcRoll(state));
                return;
            }
            if (from == to) return;
            string pair = Name(state, row.firstId) + " and " + Name(state, row.secondId);
            RelationshipLedger.Move(state, from, to, delta);
            RelationshipLedger.Record(state, from, to, type, delta,
                type == NpcGossipEvent ? "What " + pair + " said about " + Name(state, to) : pair + " talked (" + row.topic + ")");
            NpcRoll(state);
        }

        private static bool MatchesWorld(NpcWorldReadyEvidence evidence, long sequence, string firstId, string secondId, string venue, long clock) =>
            evidence != null && evidence.sequence == sequence && evidence.firstId == firstId && evidence.secondId == secondId &&
            evidence.rendezvousId == venue && evidence.observedClockTick == clock;

        private static NpcPairMemoryState FindNpcMemory(NpcSocialState social, string from, string to) =>
            social.pairMemory.SingleOrDefault(row => row.fromId == from && row.toId == to);
        private static WebNpcConversationMemory SourceNpcMemory(NpcPairMemoryState memory) => memory == null ? null :
            new WebNpcConversationMemory { partnerId = memory.toId, count = memory.count, lastTopic = memory.lastTopic, lastTime = memory.lastStartTick * 1000d };
        private static void InstallNpcMemory(NpcSocialState social, string from, string to, WebNpcConversationMemory source, long tick)
        {
            var row = FindNpcMemory(social, from, to);
            if (row == null) { row = new NpcPairMemoryState { fromId = from, toId = to }; social.pairMemory.Add(row); }
            row.count = source.count; row.lastTopic = source.lastTopic; row.lastStartTick = tick;
        }
        private static void SetNpcCooldown(NpcSocialState social, string id)
        {
            var row = social.cooldowns.SingleOrDefault(item => item.npcId == id);
            if (row == null) { row = new NpcCooldownState { npcId = id }; social.cooldowns.Add(row); }
            row.untilTick = checked(social.clockTick + 15);
        }
        private static double NpcRoll(EpisodeState state)
        {
            var rng = new SeededRandom(state.npcSocial.randomState); double value = rng.NextDouble();
            state.npcSocial.randomState = rng.State; return value;
        }

        // Called once after an ordinary player command executes and before candidate
        // validation. Phase/week/status changes invalidate pending starts without replay,
        // effects, extra draws, player action charges, or rewriting their start memory.
        private static void CancelInvalidNpcConversations(EpisodeState state)
        {
            if (state.npcSocial == null) return;
            bool eligible = NpcSocialState.IsEligible(state);
            state.npcSocial.pending.RemoveAll(row => !eligible || row.week != state.week || row.phase != state.phase ||
                state.Find(row.firstId)?.status != ContestantStatus.Active || state.Find(row.secondId)?.status != ContestantStatus.Active);
        }
    }
}
