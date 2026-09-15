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
            if (!npcMeetings.Reconcile(NpcWorldGeneration, housemates, cast.Select(actor => actor.id).ToArray(),
                cast.Where(actor => actor.status == ContestantStatus.Active).Select(actor => actor.id), out var reason))
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
            EnsureNpcSocialWorld();
            if (npcMeetings != null && !npcMeetings.IsReady && !npcWorldFailed && Time.unscaledTime >= npcNextBindingCheck)
            {
                npcNextBindingCheck = Time.unscaledTime + .1f;
                ReconcileNpcSocialWorld(); // Surface failed asynchronous bindings, without retry/teleport.
            }
            bool eligible = NpcCanAdvance && npcMeetings != null;
            SetNpcWorldPaused(!eligible);
            if (!eligible) { npcCaption?.Hide(); return; }
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
                for (int second = first + 1; second < idle.Length; second++)
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
                foreach (var npc in housemates) if (npc != null) npc.GetComponent<CharacterPresentation>()?.SetTalking(false);
            npcWorldPaused = paused;
        }

        private void UpdateNpcConversationPresentation()
        {
            if (!NpcCanAdvance) { npcCaption?.Hide(); return; }
            var talking = new HashSet<string>(); bool witnessed = false;
            foreach (var pending in projected.npcSocial.pending)
            {
                if (!npcPendingWorld.TryGetValue(pending.sequence, out var lease) || !npcMeetings.ValidateArrivedPair(lease, out _)) continue;
                talking.Add(pending.firstId); talking.Add(pending.secondId);
                if (!witnessed && npcMeetings.CanWitness(player, lease))
                {
                    npcCaption.Show(projected.Find(pending.firstId).name, projected.Find(pending.secondId).name, pending.topic, largeText ? 1.2f : 1);
                    npcShownLease = lease;
                    witnessed = true;
                }
            }
            if (!witnessed) { npcCaption?.Hide(); npcShownLease = null; }
            foreach (var npc in housemates)
                if (npc != null) npc.GetComponent<CharacterPresentation>()?.SetTalking(talking.Contains(npc.Id));
        }

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
            // Disable/re-enable also replaces world ownership, even without a save load.
            loadGeneration = checked(loadGeneration + 1);
            if (housemates != null)
                foreach (var npc in housemates) if (npc != null) npc.GetComponent<CharacterPresentation>()?.SetTalking(false);
            npcCaption?.Hide(); npcMeetings?.Dispose(); npcMeetings = null;
            npcShownLease = null;
            npcPendingWorld.Clear(); npcApproaches.Clear(); npcObstructionSince.Clear();
            npcFrameFraction = npcWorldFraction = npcFreeSeconds = 0;
            npcNextBindingCheck = 0;
            npcWorldPaused = true;
        }
    }
}
