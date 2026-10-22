#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The balance lab's synthetic NPC world (BALANCE plan B5b): the house's 1 Hz NPC clock, driven the way the
    /// director drives it but without bodies, so a lab season has the NPC-to-NPC conversations a played season has.
    ///
    /// <para><b>Only through the engine's own door.</b> Every operation is <see cref="EpisodeEngine.PrepareNpcOperation"/>
    /// - a Tick with world evidence for every pending pair, or a Start - and its candidate is installed as a new
    /// engine, as the director installs one after saving. Nothing else is written: never an NPC-to-NPC Change, never
    /// an Apply (a source scan in BalanceLabTests holds the file to that). The engine itself checks every candidate
    /// in full.</para>
    ///
    /// <para><b>The world.</b> On a Tick that reports a scan due, <see cref="NpcPairing.Plan"/> pairs the idle house
    /// with a synthetic reserve: an approach is held while the leases - approaches under way and pending pairs - are
    /// fewer than two, as the house's meeting coordinator holds them. An approach arrives <see cref="ArrivalTicks"/>
    /// ticks later and starts its conversation at the first free rendezvous in <see cref="NpcPairing.KnownRendezvous"/>;
    /// one that cannot start is held until 20 seconds after it was planned, as the director's expires. Approaches are
    /// dropped as a phase ends or the house stops being eligible (<see cref="NpcSocialState.IsEligible"/>).</para>
    ///
    /// <para><b>The budget</b> (the lead's decisions 1 and 3): the cell's ticks a week, half in the social week and
    /// half in the campaign, spent in <see cref="Slice"/>-tick slices before each of the player's decisions and the
    /// rest before the Advance that closes the phase. Each free-time phase as it is played takes its half, so the
    /// move-in night - a social phase before the first Head of Household, in week one with the social week after
    /// the first eviction (the week turns at the Head of Household) - takes a social half of its own. Ticks run only
    /// while the house is eligible - the player in the house, in free time, no diary waiting - so an evicted
    /// player's seasons tick no more.</para>
    /// </summary>
    internal sealed class BalanceLabNpcWorld
    {
        /// <summary>The ticks an approach takes to arrive (decision 3).</summary>
        public const int ArrivalTicks = 5;
        /// <summary>The ticks spent before each decision (decision 3).</summary>
        public const int Slice = 60;
        /// <summary>The seconds an approach is held before it expires (the director's 20).</summary>
        public const int ApproachSeconds = 20;

        private sealed class Approach
        {
            public string first, second;
            public long arriveAt, expiresAt;
        }

        private readonly int budget;
        private readonly BalanceLab.SeasonRun run;
        private readonly List<Approach> approaches = new List<Approach>();
        private int phaseWeek = -1;
        private EpisodePhase phase;
        private int spent;
        /// <summary>
        /// The engine's state as the world last read it: a snapshot at the start of each spend, then each installed
        /// candidate (the engine keeps a copy of its own, so the world only reads this one, never writes it).
        /// </summary>
        private EpisodeState state;

        /// <summary>The engine as the world last installed it: every command and operation goes to this one.</summary>
        public EpisodeEngine Engine { get; private set; }

        /// <summary>Called after every installed operation with the state before it and after it (tests watch here).</summary>
        public Action<EpisodeState, EpisodeState, NpcOperationKind> Watch;

        public BalanceLabNpcWorld(EpisodeEngine engine, int ticksPerWeek, BalanceLab.SeasonRun run)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            budget = Math.Max(0, ticksPerWeek);
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>The engine a command now goes to (Step's door).</summary>
        public EpisodeEngine Current() => Engine;

        /// <summary>This phase's share of the week's ticks: half in the social week, the rest in the campaign.</summary>
        private int PhaseBudget(EpisodeState s) => s.phase == EpisodePhase.Social ? budget / 2 : s.phase == EpisodePhase.Campaign ? budget - budget / 2 : 0;

        /// <summary>The ticks this phase has left to spend, nought while the house is not eligible.</summary>
        private int Left()
        {
            Track(state);
            return NpcSocialState.IsEligible(state) ? Math.Max(0, PhaseBudget(state) - spent) : 0;
        }

        private void Track(EpisodeState s)
        {
            if (s.week == phaseWeek && s.phase == phase) return;
            phaseWeek = s.week; phase = s.phase; spent = 0;
            approaches.Clear();
        }

        /// <summary>A slice before a decision: up to <see cref="Slice"/> of the phase's ticks. True when any was spent.</summary>
        public bool SpendSlice()
        {
            state = Engine.Snapshot;
            return Spend(Math.Min(Slice, Left())) > 0;
        }

        /// <summary>The rest of the phase's ticks, before the Advance that closes it. True when any was spent.</summary>
        public bool SpendRest()
        {
            state = Engine.Snapshot;
            return Spend(Left()) > 0;
        }

        private int Spend(int ticks)
        {
            int done = 0;
            for (; done < ticks; done++)
            {
                if (!NpcSocialState.IsEligible(state)) { approaches.Clear(); break; }
                if (!Tick()) break;
                spent++;
            }
            return done;
        }

        private static NpcWorldReadyEvidence Evidence(long sequence, long clock, string first, string second, string venue) => new NpcWorldReadyEvidence
            { sequence = sequence, observedClockTick = clock, firstId = first, secondId = second, rendezvousId = venue };

        private NpcOperationRequest Request(EpisodeState s, NpcOperationKind kind) => new NpcOperationRequest
        {
            kind = kind, sessionId = s.sessionId, expectedRevision = s.revision, expectedPhase = s.phase, expectedClockTick = s.npcSocial.clockTick,
            targetClockTick = s.npcSocial.clockTick + (kind == NpcOperationKind.Tick ? 1 : 0), freeRoamReady = true,
        };

        /// <summary>
        /// One operation through the engine's door, installed as a new engine when accepted (as the director installs
        /// one: the engine validates it again and keeps its own copy). The world then reads the candidate.
        /// </summary>
        private NpcOperationResult Install(NpcOperationRequest request)
        {
            var before = state;
            var clock = Stopwatch.StartNew();
            var result = Engine.PrepareNpcOperation(request);
            if (result.accepted) Engine = new EpisodeEngine(result.candidate);
            run.npcMilliseconds += clock.Elapsed.TotalMilliseconds;
            if (!result.accepted) { run.npcRejected++; return result; }
            state = result.candidate;
            run.npcOps++;
            Watch?.Invoke(before, state, request.kind);
            return result;
        }

        /// <summary>One whole second: the clock, a scan when one is due, and the approaches that have arrived.</summary>
        private bool Tick()
        {
            var s = state;
            var request = Request(s, NpcOperationKind.Tick);
            foreach (var pending in s.npcSocial.pending)
                request.worldReady.Add(Evidence(pending.sequence, s.npcSocial.clockTick, pending.firstId, pending.secondId, pending.rendezvousId));
            var result = Install(request);
            if (!result.accepted) return false;
            run.npcTicks++;
            approaches.RemoveAll(a => state.npcSocial.clockTick >= a.expiresAt);
            if (result.scanDue) Plan(state);
            StartArrived();
            return true;
        }

        /// <summary>A scan: NpcPairing pairs the idle house, every try a reservation the lease cap allows or refuses.</summary>
        private void Plan(EpisodeState s)
        {
            run.npcScans++;
            var busy = NpcPairing.Busy(s);
            foreach (var a in approaches) { busy.Add(a.first); busy.Add(a.second); }
            long clock = s.npcSocial.clockTick;
            NpcPairing.Plan(s, busy, (first, second) =>
            {
                run.npcTries++;
                if (approaches.Count + s.npcSocial.pending.Count >= 2) return false;
                approaches.Add(new Approach { first = first, second = second, arriveAt = clock + ArrivalTicks, expiresAt = clock + ApproachSeconds });
                run.npcHeld++;
                return true;
            });
        }

        /// <summary>Every approach that has arrived starts its conversation at the first free rendezvous; one that cannot waits until it expires.</summary>
        private void StartArrived()
        {
            foreach (var a in approaches.ToList())
            {
                var s = state;
                if (s.npcSocial.clockTick < a.arriveAt) continue;
                string venue = NpcPairing.KnownRendezvous.FirstOrDefault(v => s.npcSocial.pending.All(p => p.rendezvousId != v));
                if (venue == null) continue;
                var request = Request(s, NpcOperationKind.Start);
                request.sequence = s.npcSocial.nextConversationSequence;
                request.firstId = a.first; request.secondId = a.second; request.rendezvousId = venue;
                request.worldReady.Add(Evidence(request.sequence, s.npcSocial.clockTick, a.first, a.second, venue));
                if (!Install(request).accepted) continue;
                run.npcStarts++;
                approaches.Remove(a);
            }
        }
    }
}
#endif
