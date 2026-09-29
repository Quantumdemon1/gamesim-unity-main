using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private sealed class NpcApproach
        {
            public HouseMeetingLease lease;
            public double expiresAt;
        }
        private sealed class NpcRuntimeRequest
        {
            public EpisodeDirector Owner;
            public long Generation;
            public NpcOperationRequest Operation;
        }
        private HouseMeetingCoordinator npcMeetings;
        private HouseConversationCaption npcCaption;
        /// <summary>How far above the pair's feet the witnessed caption's bubble is anchored.</summary>
        private const float CaptionHeadHeight = 1.95f;
        private HouseMeetingLease npcShownLease;
        private readonly Dictionary<long, HouseMeetingLease> npcPendingWorld = new Dictionary<long, HouseMeetingLease>();
        private readonly List<NpcApproach> npcApproaches = new List<NpcApproach>();
        private readonly Dictionary<long, double> npcObstructionSince = new Dictionary<long, double>();
        private double npcFrameFraction, npcWorldFraction, npcFreeSeconds;
        private bool npcApproachDiagnosticsSuppressed = false;
        private long npcLeaseCounter;
        private float npcNextBindingCheck;
        private bool npcDiagnosticsSuspended, npcWorldFailed, npcWorldPaused = true;
        private string npcWorldFailure;
        private readonly string npcWorldIdentity = Guid.NewGuid().ToString("N");

        public bool NpcAutonomyReady => npcMeetings != null && npcMeetings.IsReady && !npcWorldFailed && !npcDiagnosticsSuspended;
        public string ObservedNpcConversation => npcCaption != null ? npcCaption.CurrentText : "";

        /// <summary>
        /// Whether listening in is on offer right now: the house's Eavesdrop, on the same terms the
        /// episode screen offers it - free time or the campaign, an action left, and at least two
        /// other houseguests to overhear.
        /// </summary>
        public bool CanListenIn
        {
            get
            {
                var state = projected;
                if (!IsReady || blockedRecovery || challengeActive || state == null) return false;
                if (state.phase != EpisodePhase.Social && state.phase != EpisodePhase.Campaign) return false;
                if (state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
                if (state.Active.Count(c => !c.isPlayer) < 2) return false;
                return EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state);
            }
        }

        /// <summary>
        /// The Nearby card's control: the house's Eavesdrop, committed as the episode screen commits
        /// it, on the pair being witnessed (STRATEGY-LOOP-PLAN.md section 2): what you overhear is
        /// theirs. The engine draws its own pair when there is none to name.
        /// </summary>
        public void ListenInNearby()
        {
            if (!CanListenIn || IsPanelOpen) return;
            var witnessed = npcShownLease;
            Commit(projected, EpisodeCommandKind.Eavesdrop, witnessed?.FirstId, witnessed?.SecondId);
        }

        /// <summary>
        /// The Nearby card is up exactly while a conversation is being witnessed, listening in is on
        /// offer and no panel is open. Update runs it every frame and Render right after rebuilding
        /// the chrome, which builds the card hidden; the HUD keeps a story's Pull ahead of it.
        /// </summary>
        private void TickNearby()
        {
            if (hud != null) hud.SetNearby(!IsPanelOpen && !string.IsNullOrEmpty(ObservedNpcConversation) && CanListenIn);
        }
        public string NpcAutonomyDiagnostic => npcWorldFailure;
        /// <summary>Read-only proof for explicit QA; never a command or UI knowledge source.</summary>
        public bool IsNpcConversationPhysicallyReady(long sequence) => npcMeetings != null
            && npcPendingWorld.TryGetValue(sequence, out var lease) && npcMeetings.ValidateArrivedPair(lease, out _);
        // World lease tokens are bounded and opaque even for a legal 160-character save ID.
        private string NpcWorldGeneration => "house:" + npcWorldIdentity
            + ":load:" + loadGeneration.ToString(CultureInfo.InvariantCulture);
        private bool NpcCanAdvance => IsReady && isActiveAndEnabled && !npcDiagnosticsSuspended && !npcWorldFailed
            && !npcSaveSuspended && !durableCommitInProgress && !IsPanelOpen && !challengeActive
            && projected != null && NpcSocialState.IsEligible(projected) && npcMeetings != null && npcMeetings.IsReady;

        /// <summary>Explicit primitive-test isolation only. Never inferred from a QA save path.</summary>
        public void SuspendNpcAutonomyForDiagnostics()
        {
            if (!Application.isEditor) throw new InvalidOperationException("Autonomy suspension diagnostics are Editor-only.");
            npcDiagnosticsSuspended = true; DisposeNpcSocialWorld();
        }

        private void EnsureNpcSocialWorld()
        {
            if (npcMeetings != null || npcWorldFailed || npcDiagnosticsSuspended || projected == null
                || !NpcSocialState.IsEligible(projected) || blockedRecovery) return;
            CreateNpcSocialWorld();
        }

        /// <summary>
        /// Editor-only: builds the house's world now, as a season's first social phase does and a
        /// played season then keeps. A fixture installed past that phase has none, and the walk out
        /// borrows its people from it.
        /// </summary>
        public void BuildNpcWorldForDiagnostics()
        {
            if (!Application.isEditor) throw new InvalidOperationException("World diagnostics are Editor-only.");
            if (npcMeetings == null && !npcWorldFailed && !npcDiagnosticsSuspended && projected != null && !blockedRecovery)
                CreateNpcSocialWorld();
        }

        private void CreateNpcSocialWorld()
        {
            if (!HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out var reason)
                || !HouseMeetingCoordinator.TryCreate(rooms, new NavMeshQueryFilter
                    { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask }, out npcMeetings, out reason))
            { StopNpcWorld(reason); return; }
            if (npcCaption == null) npcCaption = gameObject.AddComponent<HouseConversationCaption>();
            ReconcileNpcSocialWorld();
        }

        private void ReconcileNpcSocialWorld()
        {
            if (npcMeetings == null || projected == null) return;
            var cast = projected.contestants.Where(actor => !actor.isPlayer).ToArray();
            // The evicted stay in the house's world while a staged ceremony holds a place for them
            // (the commit makes them a non-contestant at once, and a body unbound at the commit could
            // not take its hot seat) and while they walk out; otherwise they go as they always did -
            // unbound at the commit, and let go by the walk-out if their body cannot take its
            // navigation back.
            if (!npcMeetings.Reconcile(NpcWorldGeneration, housemates, cast.Select(actor => actor.id).ToArray(),
                cast.Where(actor => actor.status == ContestantStatus.Active || actor.id == walkingOutId || CeremonyStageHolds(actor.id))
                    .Select(actor => actor.id), out var reason))
            { StopNpcWorld(reason); return; }
            foreach (long sequence in npcPendingWorld.Keys.ToArray())
                if (!projected.npcSocial.pending.Any(row => row.sequence == sequence))
                { npcMeetings.Release(npcPendingWorld[sequence]); npcPendingWorld.Remove(sequence); npcObstructionSince.Remove(sequence); }
            if (!NpcSocialState.IsEligible(projected))
            {
                foreach (var approach in npcApproaches) npcMeetings.Release(approach.lease);
                npcApproaches.Clear();
            }
        }

        private void TickNpcSocialRuntime(float delta)
        {
            // Hide an existing caption every frame when its physical witness proof
            // expires. Topic selection/showing remains on the 10 Hz world cadence.
            if (npcShownLease != null && (!NpcCanAdvance || !npcMeetings.CanWitness(player, npcShownLease)))
            { npcCaption?.Hide(); npcShownLease = null; }
            if (!IsReady || npcDiagnosticsSuspended) return;
            if (competitionArenaStaging) { TickCompetitionArena(); return; }
            EnsureNpcSocialWorld();
            if (npcMeetings != null && !npcMeetings.IsReady && !npcWorldFailed && Time.unscaledTime >= npcNextBindingCheck)
            {
                npcNextBindingCheck = Time.unscaledTime + .1f;
                ReconcileNpcSocialWorld(); // Surface failed asynchronous bindings, without retry/teleport.
            }
            // The opening's front door has the house: its people walk where the show sends them, and
            // nothing else ticks, as the arena's stage does below a competition.
            if (openingStage != null && openingStage.Active) { openingStage.Tick(); npcCaption?.Hide(); return; }
            bool eligible = NpcCanAdvance && npcMeetings != null;
            SetNpcWorldPaused(!eligible);
            // Paused for the week's business, the house mills about (EpisodeDirector.Wander); free
            // time hands everybody back to its own conversations.
            if (!eligible) { npcCaption?.Hide(); TickWandering(); return; }
            StopWandering();
            // A suspension/debugger/long blocked frame is not elapsed social play.
            // Whole committed seconds persist; the remaining fraction is transient.
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0 || delta > 1) return;
            npcWorldFraction += delta;
            if (npcWorldFraction < .1) return;
            double elapsed = npcWorldFraction; npcWorldFraction = 0; npcFreeSeconds += elapsed;
            npcMeetings.Tick();
            ReconcileNpcSocialWorld();
            if (!NpcCanAdvance) return;
            RestorePendingNpcMeetings();
            ExpireNpcApproaches();
            bool ready = PendingNpcWorldReady();
            if (ready)
            {
                npcFrameFraction += elapsed;
                FlushNpcWholeTicks();
                if (NpcCanAdvance) StartArrivedNpcApproaches();
            }
            UpdateNpcConversationPresentation();
        }

        private void RestorePendingNpcMeetings()
        {
            foreach (var pending in projected.npcSocial.pending)
            {
                if (npcPendingWorld.ContainsKey(pending.sequence)) continue;
                string token = NpcWorldGeneration + ":saved:" + pending.sequence.ToString(CultureInfo.InvariantCulture);
                if (npcMeetings.TryReserveAtVenue(token, pending.firstId, pending.secondId, pending.rendezvousId, out var lease, out _))
                    npcPendingWorld.Add(pending.sequence, lease);
                // Binding/carving removal or a temporary occupant can delay reunion.
                // Saved topic, memory, duration and RNG are never recreated here.
            }
        }

        private bool PendingNpcWorldReady()
        {
            bool allReady = true;
            foreach (var pending in projected.npcSocial.pending.ToArray())
            {
                bool found = npcPendingWorld.TryGetValue(pending.sequence, out var lease);
                if (found && npcMeetings.ValidateArrivedPair(lease, out _))
                { npcObstructionSince.Remove(pending.sequence); continue; }
                allReady = false;
                // A loaded pending pair may need an ordinary route from its current
                // placement. Missing/changed geometry stays suspended, never discarded.
                if (!found) continue;
                if (!npcObstructionSince.TryGetValue(pending.sequence, out double since))
                    npcObstructionSince[pending.sequence] = npcFreeSeconds;
                else if (npcFreeSeconds - since >= 20)
                {
                    var request = NpcRequest(NpcOperationKind.Cancel);
                    request.Operation.sequence = pending.sequence;
                    CommitNpcOperation(request); // Unwitnessed cancellation is not a player event.
                    break;
                }
            }
            return allReady;
        }

        private void PlanNpcApproaches()
        {
            var state = projected;
            var unavailable = new HashSet<string>(state.npcSocial.pending.SelectMany(row => new[] { row.firstId, row.secondId }));
            foreach (var approach in npcApproaches) { unavailable.Add(approach.lease.FirstId); unavailable.Add(approach.lease.SecondId); }
            foreach (var cooldown in state.npcSocial.cooldowns.Where(row => row.untilTick > state.npcSocial.clockTick)) unavailable.Add(cooldown.npcId);
            var idle = state.Active.Where(actor => !actor.isPlayer && !unavailable.Contains(actor.id)).ToArray();
            for (int first = 0; first < idle.Length; first++)
            {
                if (unavailable.Contains(idle[first].id)) continue;
                // Under agency each houseguest tries the one their agenda points at before the next
                // idle body in cast order (NPC-AGENCY-PLAN.md §4); without it, cast order as always.
                var order = new List<int>();
                string wanted = NpcAgendas.PreferredPartner(state, idle[first].id);
                int at = wanted == null ? -1 : Array.FindIndex(idle, actor => actor.id == wanted);
                if (at >= 0 && at != first) order.Add(at);
                for (int second = first + 1; second < idle.Length; second++) if (second != at) order.Add(second);
                foreach (int second in order)
                {
                    if (unavailable.Contains(idle[second].id)) continue;
                    string token = NpcWorldGeneration + ":approach:" + (++npcLeaseCounter).ToString(CultureInfo.InvariantCulture);
                    if (!npcMeetings.TryReservePair(token, idle[first].id, idle[second].id, out var lease, out _)) continue;
                    npcApproaches.Add(new NpcApproach { lease = lease, expiresAt = npcFreeSeconds + 20 });
                    unavailable.Add(idle[first].id); unavailable.Add(idle[second].id); break;
                }
            }
        }

        private void ExpireNpcApproaches()
        {
            foreach (var approach in npcApproaches.ToArray())
                if (npcFreeSeconds >= approach.expiresAt)
                { npcMeetings.Release(approach.lease); npcApproaches.Remove(approach); }
        }

        private void StartArrivedNpcApproaches()
        {
            foreach (var approach in npcApproaches.ToArray())
            {
                if (!NpcCanAdvance || !npcMeetings.ValidateArrivedPair(approach.lease, out _)) continue;
                var request = NpcRequest(NpcOperationKind.Start);
                request.Operation.sequence = projected.npcSocial.nextConversationSequence;
                request.Operation.firstId = approach.lease.FirstId; request.Operation.secondId = approach.lease.SecondId; request.Operation.rendezvousId = approach.lease.VenueId;
                request.Operation.worldReady.Add(Evidence(request.Operation.sequence, approach.lease));
                if (CommitNpcOperation(request, approach.lease)) npcApproaches.Remove(approach);
            }
        }

        private NpcRuntimeRequest NpcRequest(NpcOperationKind kind) => new NpcRuntimeRequest
        {
            Owner = this, Generation = loadGeneration,
            Operation = new NpcOperationRequest
            {
                kind = kind, sessionId = projected.sessionId, expectedRevision = projected.revision, expectedPhase = projected.phase,
                expectedClockTick = projected.npcSocial.clockTick,
                targetClockTick = projected.npcSocial.clockTick + (kind == NpcOperationKind.Tick ? 1 : 0), freeRoamReady = NpcCanAdvance
            }
        };
        private NpcWorldReadyEvidence Evidence(long sequence, HouseMeetingLease lease) => new NpcWorldReadyEvidence
        {
            sequence = sequence, observedClockTick = projected.npcSocial.clockTick,
            firstId = lease.FirstId, secondId = lease.SecondId, rendezvousId = lease.VenueId
        };

        private bool FlushNpcWholeTicks()
        {
            if (!NpcCanAdvance) return !blockedRecovery;
            // Explicit save/panel calls can occur between the 100 ms world samples.
            // Count that remainder only with fresh proof for every durable pending pair.
            if (npcWorldFraction > 0)
            {
                if (!AllPendingNpcLeasesReady()) return false;
                npcFrameFraction += npcWorldFraction; npcFreeSeconds += npcWorldFraction; npcWorldFraction = 0;
            }
            while (npcFrameFraction >= 1 && NpcCanAdvance && npcMeetings != null)
            {
                var request = NpcRequest(NpcOperationKind.Tick);
                foreach (var pending in projected.npcSocial.pending)
                {
                    if (!npcPendingWorld.TryGetValue(pending.sequence, out var lease) || !npcMeetings.ValidateArrivedPair(lease, out _)) return false;
                    request.Operation.worldReady.Add(Evidence(pending.sequence, lease));
                }
                if (!CommitNpcOperation(request)) return false;
                npcFrameFraction -= 1;
            }
            return !blockedRecovery && !npcSaveSuspended;
        }

        private bool AllPendingNpcLeasesReady()
        {
            return npcMeetings != null && projected.npcSocial.pending.All(pending =>
                npcPendingWorld.TryGetValue(pending.sequence, out var lease) && npcMeetings.ValidateArrivedPair(lease, out _));
        }

        private bool CommitNpcOperation(NpcRuntimeRequest queued, HouseMeetingLease startLease = null)
        {
            if (queued == null || !ReferenceEquals(queued.Owner, this) || queued.Generation != loadGeneration || !NpcCanAdvance) return false;
            var request = queued.Operation;
            if (request == null) return false;
            if (request.kind == NpcOperationKind.Start)
            {
                if (startLease == null || !npcMeetings.TryGetLease(startLease.Token, out var owned)
                    || !ReferenceEquals(owned, startLease) || startLease.Generation != NpcWorldGeneration
                    || startLease.FirstId != request.firstId || startLease.SecondId != request.secondId
                    || startLease.VenueId != request.rendezvousId || !npcMeetings.ValidateArrivedPair(startLease, out _)) return false;
            }
            else if (startLease != null || (request.kind == NpcOperationKind.Tick && !AllPendingNpcLeasesReady())) return false;
            var result = engine.PrepareNpcOperation(request);
            if (!result.accepted) return false;
            if (!TryPersistCandidate(result.candidate, out var installed, out var failure))
            { message = failure; SetNpcWorldPaused(true); npcCaption?.Hide(); Render(); return false; }
            if (queued.Generation != loadGeneration) { StopNpcWorld("A stale world operation cannot publish into a loaded session."); return false; }
            if (startLease != null) npcPendingWorld[request.sequence] = startLease;
            engine = new EpisodeEngine(installed);
            Project(); // Reconciles removed pending leases only after successful persistence.
            // Only a durably committed scan deadline can schedule NEW routes.
            // Arrival-triggered starts and transient obstruction timers are separate.
            if (request.kind == NpcOperationKind.Tick && result.scanDue && NpcCanAdvance
                && AllPendingNpcLeasesReady() && !(Application.isEditor && npcApproachDiagnosticsSuppressed))
                PlanNpcApproaches();
            return true;
        }

        private void PauseNpcSocialForPanel()
        {
            if (NpcCanAdvance) FlushNpcWholeTicks();
            SuspendNpcWorldWithoutSaving();
        }
        private void SuspendNpcWorldWithoutSaving()
        {
            SetNpcWorldPaused(true); npcCaption?.Hide();
        }
        private void SetNpcWorldPaused(bool paused)
        {
            if (npcMeetings == null) return;
            npcMeetings.SetPaused(paused);
            if (paused && !npcWorldPaused)
                foreach (var npc in housemates)
                {
                    var visual = npc != null ? npc.GetComponent<CharacterPresentation>() : null;
                    if (visual == null) continue;
                    visual.SetTalking(false); visual.SetArguing(false);
                }
            npcWorldPaused = paused;
        }

        private void UpdateNpcConversationPresentation()
        {
            if (!NpcCanAdvance) { npcCaption?.Hide(); return; }
            var talking = new HashSet<string>(); var seated = new HashSet<string>(); var speaking = new HashSet<string>();
            var arguing = new HashSet<string>();
            var facing = new Dictionary<string, float>(); bool witnessed = false;
            foreach (var pending in projected.npcSocial.pending)
            {
                if (!npcPendingWorld.TryGetValue(pending.sequence, out var lease) || !npcMeetings.ValidateArrivedPair(lease, out _)) continue;
                talking.Add(pending.firstId); talking.Add(pending.secondId);
                // They take turns: the floor changes hands every four seconds of world time, offset
                // by the conversation's sequence so two pairs in the house are not in step.
                bool firstSpeaks = (((long)(npcFreeSeconds / 4.0) + pending.sequence) & 1) == 0;
                speaking.Add(firstSpeaks ? pending.firstId : pending.secondId);
                // The two topics the witnessed caption calls a tense conversation are the two the
                // bodies argue through. This reads the topic the same way the caption does and
                // changes nothing about what it says or when it is shown: a body waving its arms
                // across the house tells a passer-by that something is going on, which is exactly
                // what the caption already tells whoever is close enough to witness it.
                if (IsTenseTopic(pending.topic)) { arguing.Add(pending.firstId); arguing.Add(pending.secondId); }
                if (lease.Seated) { seated.Add(pending.firstId); seated.Add(pending.secondId); }
                facing[pending.firstId] = lease.FirstFacing; facing[pending.secondId] = lease.SecondFacing;
                if (!witnessed && npcMeetings.CanWitness(player, lease,out var visibleMidpoint))
                {
                    // Use the same current body endpoints that passed visibility. Navigation
                    // approaches stay reserved; they are not where seated faces are rendered.
                    var between = visibleMidpoint+Vector3.up*(lease.Seated ? .35f : CaptionHeadHeight-1.15f);
                    npcCaption.Show(projected.Find(pending.firstId).name, projected.Find(pending.secondId).name,
                        pending.topic, largeText ? 1.2f : 1, between);
                    npcShownLease = lease;
                    witnessed = true;
                }
            }
            if (!witnessed) { npcCaption?.Hide(); npcShownLease = null; }
            // Arrived pairs talk; at a seated venue they sit; either way each settles on the lease's
            // heading. A pair still travelling, or released, has none of the three.
            foreach (var npc in housemates)
            {
                var visual = npc != null ? npc.GetComponent<CharacterPresentation>() : null;
                if (visual == null) continue;
                if(npcMeetings.TryGetActivity(npc.Id,out var activity) && npcMeetings.ActivityValid(activity))continue;
                visual.SetTalking(talking.Contains(npc.Id));
                visual.SetSpeaking(speaking.Contains(npc.Id));
                var seatPose=npc.GetComponent<HouseSeatPresentation>();
                if(seated.Contains(npc.Id) && npcMeetings.TryGetSeat(npc.Id,out var seat))
                {
                    if(seatPose==null)seatPose=npc.gameObject.AddComponent<HouseSeatPresentation>();
                    string id=npc.Id;
                    seatPose.Begin(seat,()=>npcMeetings!=null && npcMeetings.TryGetSeat(id,out var current) && current==seat);
                }
                else seatPose?.End();
                // Only a pose holding the body in its chair seats it (its Cue says so every frame).
                // Saying "seated" for an arrived pair whose pose had not begun sat that one down in
                // the air at the table's approach, and a pose begun over that flag later handed it back.
                if (seatPose == null || !seatPose.Active) visual.SetSeated(false);
                visual.SetArguing(arguing.Contains(npc.Id));
                visual.SetFacing(facing.TryGetValue(npc.Id, out float yaw) ? yaw : float.NaN);
            }
        }

        /// <summary>
        /// The topics a body argues through: the same two <see cref="HouseConversationCaption.Describe"/>
        /// calls a tense conversation. Kept beside the presentation it drives rather than inside the
        /// caption, which receives an allowlisted topic and never answers questions about one.
        /// </summary>
        private static bool IsTenseTopic(string topic) => topic == "tension" || topic == "rivalry";

        private void StopNpcWorld(string reason)
        {
            npcWorldFailed = true; npcWorldFailure = reason ?? "Housemate navigation is unavailable.";
            SetNpcWorldPaused(true); npcCaption?.Hide();
            message = "Housemate activity is paused: " + npcWorldFailure + " Your episode and saved history remain available.";
        }
        private void ResetNpcSocialForLoad()
        {
            DisposeNpcSocialWorld();
            npcSaveSuspended = false; npcWorldFailed = false; npcWorldFailure = null;
        }
        private void DisposeNpcSocialWorld()
        {
            EndDiaryVisit(true);
            DisposeHouseActivities();
            EndCompetitionArena();
            // Disable/re-enable also replaces world ownership, even without a save load.
            loadGeneration = checked(loadGeneration + 1);
            if (housemates != null)
                foreach (var npc in housemates)
                {
                    var visual = npc != null ? npc.GetComponent<CharacterPresentation>() : null;
                    if (visual == null) continue;
                    visual.SetTalking(false); visual.SetSeated(false); visual.SetArguing(false); visual.SetFacing(float.NaN);
                }
            npcCaption?.Hide(); npcMeetings?.Dispose(); npcMeetings = null;
            npcShownLease = null;
            npcPendingWorld.Clear(); npcApproaches.Clear(); npcObstructionSince.Clear();
            npcFrameFraction = npcWorldFraction = npcFreeSeconds = 0;
            npcNextBindingCheck = 0;
            npcWorldPaused = true;
        }
    }
}
