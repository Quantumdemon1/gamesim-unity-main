using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// <see cref="NpcPairing"/> (BALANCE plan B5a): the director's route planner's loop, moved into the
    /// simulation so the balance lab's synthetic NPC world pairs the house as the house does. A pure
    /// refactor: over hundreds of generated houses the try sequence is the one the director's own loop at
    /// f8066b4b made (copied below line for line), and the director now calls it.
    /// </summary>
    public sealed class NpcPairingTests
    {
        // ---------------------------------------------------------------- the oracle

        /// <summary>
        /// EpisodeDirector.NpcSocial.cs PlanNpcApproaches at f8066b4b, line for line: the approaches under way
        /// and the talk spot's houseguest as the runtime's own busy ids, the coordinator's TryReservePair as
        /// <paramref name="tryReservePair"/> (one lease token minted a call).
        /// </summary>
        private static void PlannerAtF8066b4b(EpisodeState projected, IEnumerable<string> approachIds, string talkSpotNpcId, Func<string, string, bool> tryReservePair)
        {
            var state = projected;
            var unavailable = new HashSet<string>(state.npcSocial.pending.SelectMany(row => new[] { row.firstId, row.secondId }));
            foreach (var id in approachIds) unavailable.Add(id);
            if (talkSpotNpcId != null) unavailable.Add(talkSpotNpcId);
            foreach (var cooldown in state.npcSocial.cooldowns.Where(row => row.untilTick > state.npcSocial.clockTick)) unavailable.Add(cooldown.npcId);
            var idle = state.Active.Where(actor => !actor.isPlayer && !unavailable.Contains(actor.id)).ToArray();
            for (int first = 0; first < idle.Length; first++)
            {
                if (unavailable.Contains(idle[first].id)) continue;
                var order = new List<int>();
                string wanted = NpcAgendas.PreferredPartner(state, idle[first].id);
                int at = wanted == null ? -1 : Array.FindIndex(idle, actor => actor.id == wanted);
                if (at >= 0 && at != first) order.Add(at);
                for (int second = first + 1; second < idle.Length; second++) if (second != at) order.Add(second);
                foreach (int second in order)
                {
                    if (unavailable.Contains(idle[second].id)) continue;
                    if (!tryReservePair(idle[first].id, idle[second].id)) continue;
                    unavailable.Add(idle[first].id); unavailable.Add(idle[second].id); break;
                }
            }
        }

        /// <summary>
        /// A coordinator stand-in: at most two leases at once, as the house's coordinator holds, and a keyed coin
        /// that refuses a share of the routes - the same answers for the same try, whoever asks.
        /// </summary>
        private sealed class FakeReserve
        {
            private readonly string key;
            private readonly int refusePercent;
            private int held;
            public readonly List<string> tries = new List<string>();
            public FakeReserve(string key, int refusePercent) { this.key = key; this.refusePercent = refusePercent; }

            public bool TryReserve(string first, string second)
            {
                bool ok = held < 2 && SeededRandom.HashSeed(key + ":" + tries.Count.ToString(CultureInfo.InvariantCulture) + ":" + first + ":" + second) % 100u >= (uint)refusePercent;
                tries.Add(first + ">" + second + (ok ? " held" : " refused"));
                if (ok) held++;
                return ok;
            }
        }

        /// <summary>
        /// The refactor's proof: over 300 and more houses - the free time and campaigns of walked seasons at six,
        /// eight and twelve, each reworked with cooldowns live and lapsed, pending pairs, NPC pacts and new views
        /// (so agendas point elsewhere), approaches under way, a houseguest on their way to the player, and a
        /// coordinator refusing on a keyed coin - <see cref="NpcPairing.Plan"/> tries exactly the pairs the
        /// director's loop tried, in its order, and holds the same ones.
        /// </summary>
        [Test]
        public void PlanTriesWhatTheDirectorsLoopTriedInItsOrder()
        {
            int compared = 0, agendaFirst = 0, refusals = 0, twoHeld = 0, none = 0;
            foreach (var (base_, variant, extra, talk) in GeneratedHouses())
            {
                string key = base_ + "/" + variant;
                foreach (int refuse in new[] { 0, 35, 70 })
                {
                    var oracle = new FakeReserve(key, refuse);
                    PlannerAtF8066b4b(extra.state, extra.approaches, talk, oracle.TryReserve);
                    var pairing = new FakeReserve(key, refuse);
                    var busy = NpcPairing.Busy(extra.state);
                    foreach (string id in extra.approaches) busy.Add(id);
                    if (talk != null) busy.Add(talk);
                    var held = NpcPairing.Plan(extra.state, busy, pairing.TryReserve);
                    Assert.That(pairing.tries, Is.EqualTo(oracle.tries), key + " refusing " + refuse + "%");
                    Assert.That(held.Select(p => p.first + ">" + p.second + " held"), Is.EqualTo(oracle.tries.Where(t => t.EndsWith(" held", StringComparison.Ordinal))));
                    compared++;
                    refusals += oracle.tries.Count(t => t.EndsWith(" refused", StringComparison.Ordinal));
                    if (held.Count == 2) twoHeld++;
                    if (oracle.tries.Count == 0) none++;
                }
                agendaFirst += AgendaLeads(extra.state, extra.approaches, talk) ? 1 : 0;
            }
            TestContext.Out.WriteLine("compared " + compared + " plans; agenda first in " + agendaFirst + " houses; refusals " + refusals + "; two held " + twoHeld + "; nobody free " + none);
            Assert.That(compared, Is.GreaterThanOrEqualTo(900), "Three hundred houses and more, each at three refusal rates.");
            Assert.That(agendaFirst, Is.GreaterThan(30), "Agendas reordered the tries in many houses.");
            Assert.That(refusals, Is.GreaterThan(100), "The coordinator refused routes.");
            Assert.That(twoHeld, Is.GreaterThan(50), "Scans held both pair slots.");
        }

        /// <summary>Whether some idle houseguest's agenda points past the next idle body: a house where the agenda changes the order.</summary>
        private static bool AgendaLeads(EpisodeState s, IEnumerable<string> approaches, string talk)
        {
            var busy = NpcPairing.Busy(s);
            foreach (string id in approaches) busy.Add(id);
            if (talk != null) busy.Add(talk);
            var idle = s.Active.Where(c => !c.isPlayer && !busy.Contains(c.id)).Select(c => c.id).ToList();
            for (int i = 0; i + 1 < idle.Count; i++)
            {
                string wanted = NpcAgendas.PreferredPartner(s, idle[i]);
                if (wanted != null && wanted != idle[i + 1] && idle.Contains(wanted)) return true;
            }
            return false;
        }

        private sealed class House
        {
            public EpisodeState state;
            public List<string> approaches = new List<string>();
        }

        /// <summary>The houses the oracle test pairs: walked seasons' free time and campaigns, each reworked twenty ways.</summary>
        private static IEnumerable<(string, int, House, string)> GeneratedHouses()
        {
            foreach (var (size, seed) in new[] { (6, 11u), (8, 12u), (12, 13u), (8, 14u), (10, 15u), (12, 16u) })
            {
                int taken = 0;
                foreach (var s in FreeTimeStates(size, seed))
                {
                    string name = "n" + size + " s" + seed + " w" + s.week + " " + s.phase;
                    for (int variant = 0; variant < 20; variant++)
                    {
                        var coin = new SeededRandom(SeededRandom.HashSeed(name + "/" + variant));
                        var house = Rework(s.Clone(), coin, variant);
                        string talk = null;
                        var npcs = house.state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
                        if (coin.NextDouble() < 0.2) talk = npcs[(int)(coin.NextDouble() * npcs.Count) % npcs.Count];
                        yield return (name, variant, house, talk);
                    }
                    if (++taken >= 10) break;
                }
            }
        }

        /// <summary>The states of a season walked to its fourth week at which the NPC world could run: free time and the campaign, the player in the house.</summary>
        private static IEnumerable<EpisodeState> FreeTimeStates(int size, uint seed)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            ShippedRules.ApplyFresh(fresh);
            var engine = new EpisodeEngine(fresh);
            var seen = new HashSet<string>();
            for (int i = 0; i < 3000; i++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished || s.week > 4 || s.Active.Count() <= 4) yield break;
                if (NpcSocialState.IsEligible(s) && seen.Add(s.week + ":" + s.phase)) yield return s;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(s));
                Assert.That(result.accepted, Is.True, result.reason);
            }
        }

        /// <summary>One house reworked: the NPC clock moved, cooldowns live and lapsed, pending pairs, NPC pacts and new views, approaches under way.</summary>
        private static House Rework(EpisodeState s, SeededRandom coin, int variant)
        {
            var house = new House { state = s };
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            string Pick() => npcs[(int)(coin.NextDouble() * npcs.Count) % npcs.Count];
            if (variant == 0) return house; // The walked state as it stands.
            s.npcSocial.clockTick = (long)(coin.NextDouble() * 400);
            foreach (string id in npcs)
                if (coin.NextDouble() < 0.25)
                    s.npcSocial.cooldowns.Add(new NpcCooldownState { npcId = id, untilTick = s.npcSocial.clockTick + (long)(coin.NextDouble() * 30) - 12 });
            for (int pending = 0; pending < 2 && coin.NextDouble() < 0.4; pending++)
            {
                string a = Pick(), b = Pick();
                if (a == b) continue;
                s.npcSocial.pending.Add(new NpcConversationState { sequence = pending + 1, firstId = a, secondId = b, rendezvousId = NpcPairing.KnownRendezvous[pending], week = s.week, phase = s.phase });
            }
            // New views between houseguests: a Build agenda points elsewhere, a pact's Closest changes.
            foreach (string from in npcs)
                foreach (string to in npcs.Where(x => x != from))
                    if (coin.NextDouble() < 0.3) SetScore(s, from, to, Math.Round(coin.NextDouble() * 200 - 100));
            // Houseguest pacts: Hold and Hunt agendas name a pact-mate.
            for (int pact = 0; pact < 2 && coin.NextDouble() < 0.45; pact++)
            {
                var members = npcs.OrderBy(_ => coin.NextDouble()).Take(coin.NextDouble() < 0.5 ? 2 : 3).ToList();
                s.alliances.Add(new AllianceState { id = "alliance-pairing-test-" + pact, name = "Pairing " + pact, active = true, members = members });
            }
            if (coin.NextDouble() < 0.3) { house.approaches.Add(Pick()); house.approaches.Add(Pick()); }
            return house;
        }

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        // ---------------------------------------------------------------- the rules, case by case

        /// <summary>A house in free time with everybody idle: the first walked season's social week at eight.</summary>
        private static EpisodeState Idle()
        {
            var s = FreeTimeStates(8, 21u).First(x => x.phase == EpisodePhase.Social).Clone();
            s.npcSocial.pending.Clear();
            s.npcSocial.cooldowns.Clear();
            s.alliances.RemoveAll(a => a.members.Any(m => s.Find(m)?.isPlayer == false));
            return s;
        }

        [Test]
        public void AHouseguestTriesTheOneTheirAgendaPointsAtBeforeTheNextInCastOrder()
        {
            var s = Idle();
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            string first = npcs[0], last = npcs[npcs.Count - 1];
            // A pact of the first and the last: the first holds tight with the last (Hold).
            s.alliances.Add(new AllianceState { id = "alliance-pairing-hold", name = "Hold", active = true, members = new List<string> { first, last } });
            Assert.That(NpcAgendas.PreferredPartner(s, first), Is.EqualTo(last), "Precondition: the pact points the first at the last.");
            var tries = new List<string>();
            var held = NpcPairing.Plan(s, NpcPairing.Busy(s), (a, b) => { tries.Add(a + ">" + b); return true; });
            Assert.That(tries[0], Is.EqualTo(first + ">" + last), "The agenda's partner first.");
            Assert.That(held[0], Is.EqualTo((first, last)));
            // Refused there, the first tries the next idle body in cast order, the last skipped.
            tries.Clear();
            NpcPairing.Plan(s, NpcPairing.Busy(s), (a, b) => { tries.Add(a + ">" + b); return b != last; });
            Assert.That(tries.Take(2), Is.EqualTo(new[] { first + ">" + last, first + ">" + npcs[1] }));
            Assert.That(tries.Count(t => t == first + ">" + last), Is.EqualTo(1), "Tried once in a scan.");
        }

        [Test]
        public void NobodyOnALiveCooldownInAPendingPairOrBusyIsTried()
        {
            var s = Idle();
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.npcSocial.clockTick = 40;
            s.npcSocial.cooldowns.Add(new NpcCooldownState { npcId = npcs[0], untilTick = 41 });   // live
            s.npcSocial.cooldowns.Add(new NpcCooldownState { npcId = npcs[1], untilTick = 40 });   // lapsed this tick
            s.npcSocial.pending.Add(new NpcConversationState { sequence = 1, firstId = npcs[2], secondId = npcs[3], rendezvousId = NpcPairing.KnownRendezvous[0], week = s.week, phase = s.phase });
            var busy = NpcPairing.Busy(s);
            Assert.That(busy, Is.EquivalentTo(new[] { npcs[0], npcs[2], npcs[3] }), "Busy: the live cooldown and the pending pair.");
            busy.Add(npcs[4]); // An approach under way, the caller's own.
            var named = new HashSet<string>();
            NpcPairing.Plan(s, busy, (a, b) => { named.Add(a); named.Add(b); return false; });
            foreach (string busyId in new[] { npcs[0], npcs[2], npcs[3], npcs[4] }) Assert.That(named, Does.Not.Contain(busyId), busyId + " is busy.");
            Assert.That(named, Does.Contain(npcs[1]), "A cooldown that has run out frees its houseguest.");
            Assert.That(named, Does.Not.Contain(s.playerId), "Never the player.");
        }

        [Test]
        public void PlanReadsTheSeasonAndChangesNothing()
        {
            var s = Idle();
            string before = JsonConvert.SerializeObject(s);
            uint random = s.randomState, npcRandom = s.npcSocial.randomState;
            NpcPairing.Plan(s, NpcPairing.Busy(s), (a, b) => true);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            Assert.That(s.randomState, Is.EqualTo(random));
            Assert.That(s.npcSocial.randomState, Is.EqualTo(npcRandom));
        }

        /// <summary>The six rendezvous, in their authored order: those the saved world's own check names, read from its source.</summary>
        [Test]
        public void TheKnownRendezvousAreTheSixTheSavedWorldKnowsInTheirAuthoredOrder()
        {
            Assert.That(NpcPairing.KnownRendezvous.Distinct().Count(), Is.EqualTo(6));
            foreach (string id in NpcPairing.KnownRendezvous) Assert.That(NpcSocialState.IsKnownRendezvous(id), Is.True, id);
            Assert.That(NpcSocialState.IsKnownRendezvous("hot-tub-chat"), Is.False);
            string source = File.ReadAllText(Path.Combine(SourceRoot(), "Simulation", "NpcSocialState.cs"));
            int start = source.IndexOf("public static bool IsKnownRendezvous(string id)", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The check was found.");
            string body = source.Substring(start, source.IndexOf(';', start) - start);
            var named = Regex.Matches(body, "\"([a-z\\-]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.That(NpcPairing.KnownRendezvous, Is.EqualTo(named), "Every id the check knows, in its order.");
        }

        /// <summary>The director pairs through NpcPairing and keeps no loop of its own: one call, no agenda read.</summary>
        [Test]
        public void TheDirectorsRoutePlannerPairsThroughNpcPairing()
        {
            string source = File.ReadAllText(Path.Combine(SourceRoot(), "Runtime", "Episode", "EpisodeDirector.NpcSocial.cs"));
            int start = source.IndexOf("private void PlanNpcApproaches()", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The route planner was found.");
            int end = source.IndexOf("private void ", start + 10, StringComparison.Ordinal);
            string body = source.Substring(start, (end < 0 ? source.Length : end) - start);
            Assert.That(Regex.Matches(body, @"NpcPairing\.Plan\(").Count, Is.EqualTo(1), "One call of NpcPairing.Plan.");
            Assert.That(Regex.Matches(body, @"NpcPairing\.Busy\(").Count, Is.EqualTo(1), "The saved world's busy ids from NpcPairing.Busy.");
            Assert.That(body, Does.Not.Contain("PreferredPartner"), "Who tries whom first is NpcPairing's.");
            Assert.That(body, Does.Not.Contain("for (int"), "No loop of its own.");
            Assert.That(Regex.Matches(body, @"\+\+npcLeaseCounter").Count, Is.EqualTo(1), "One lease token a try.");
        }

        /// <summary>Assets/Gamesim, found upward from the working directory (the Unity project) or the test binary (Tools/SimulationTests).</summary>
        private static string SourceRoot()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // An assembly loaded from bytes has no location, and Path.GetDirectoryName("") throws on Mono.
            string binary = typeof(NpcPairingTests).Assembly.Location;
            var starts = new List<string> { Directory.GetCurrentDirectory() };
            if (!string.IsNullOrEmpty(binary)) starts.Add(Path.GetDirectoryName(binary));
            foreach (string start in starts)
            {
                string at = start;
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
                {
                    string candidate = Path.Combine(at, "Assets", "Gamesim");
                    if (File.Exists(Path.Combine(candidate, "Simulation", "SeasonBuilder.cs"))) { found.Add(Path.GetFullPath(candidate)); break; }
                    string next = Path.GetDirectoryName(at);
                    if (next == at) break;
                    at = next;
                }
            }
            Assert.That(found, Has.Count.EqualTo(1), "Exactly one Assets/Gamesim source tree above the test: " + string.Join(", ", found));
            return found.Single();
        }
    }
}
