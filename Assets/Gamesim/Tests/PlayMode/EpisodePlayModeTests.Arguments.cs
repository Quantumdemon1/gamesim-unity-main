using System.Collections;
using System.Collections.Generic;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Two conversations reunite in the house at the same time: one the caption would call a tense
    /// conversation and one that is only two people bonding. The tense pair argues - both bodies,
    /// not just the one with the floor - and the bonding pair does not. Reduced motion switches an
    /// argument off exactly as it switches the talk loops off, because arm-waving is the same kind
    /// of motion at a larger size.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcRuntime_ATenseConversationArguesAndABondingOneDoesNot()
        {
            var fixture = ArguingRuntimeFixture();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            yield return WaitForNpcRuntimeBinding();

            NpcInvoke("SetNpcWorldPaused", false);
            NpcInvoke("RestorePendingNpcMeetings");
            var leases = NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld");
            Assert.That(leases.TryGetValue(1, out var tense), Is.True, "The tense conversation must reserve its venue.");
            Assert.That(leases.TryGetValue(2, out var bonding), Is.True, "The bonding conversation must reserve its own.");

            // The director ticks its own world every frame; ticking it again here would run the
            // twenty-second reunion clock at double speed and cancel both meetings mid-walk.
            float deadline = Time.realtimeSinceStartup + 40;
            while ((!director.IsNpcConversationPhysicallyReady(1) || !director.IsNpcConversationPhysicallyReady(2))
                && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(NpcRead<bool>("npcWorldPaused"), Is.False, director.NpcAutonomyDiagnostic ?? director.StatusMessage);
                Assert.That(leases.ContainsKey(1) && leases.ContainsKey(2), Is.True,
                    "Both pairs must stay pending until they have arrived.");
                yield return null;
            }
            Assert.That(director.IsNpcConversationPhysicallyReady(1) && director.IsNpcConversationPhysicallyReady(2), Is.True,
                "Both pairs must reach their venues. " + director.NpcAutonomyDiagnostic);

            var argueFirst = Body(tense.FirstId);
            var argueSecond = Body(tense.SecondId);
            var calmFirst = Body(bonding.FirstId);
            var calmSecond = Body(bonding.SecondId);
            Assert.That(new[] { argueFirst, argueSecond, calmFirst, calmSecond }, Is.All.Not.Null,
                "All four houseguests have bodies in the house.");

            // One world tick (a tenth of a second) carries the arrivals into the presentation.
            float settled = Time.realtimeSinceStartup + 1f;
            while (!(argueFirst.IsTalking && argueSecond.IsTalking && calmFirst.IsTalking && calmSecond.IsTalking)
                && Time.realtimeSinceStartup < settled) yield return null;
            Assert.That(argueFirst.IsTalking && argueSecond.IsTalking, Is.True, "The tense pair is in a conversation.");
            Assert.That(calmFirst.IsTalking && calmSecond.IsTalking, Is.True, "So is the bonding pair.");
            Assert.That(argueFirst.IsArguing && argueSecond.IsArguing, Is.True,
                "Both halves of a tense conversation argue through it, not only whoever has the floor.");
            Assert.That(calmFirst.IsArguing || calmSecond.IsArguing, Is.False,
                "Two people bonding are having an ordinary conversation.");

            // The bodies read the topic the way the caption reads it, and change nothing about what
            // it says: the sentence a witness sees is the same sentence it has always been.
            Assert.That(HouseConversationCaption.Describe("Alex", "Sam", "tension"),
                Is.EqualTo("Alex and Sam are having a tense conversation."));

            argueFirst.SetReducedMotion(true);
            Assert.That(argueFirst.IsArguing, Is.False, "Reduced motion switches the argument off.");
            yield return null;
            Assert.That(argueFirst.IsArguing, Is.False, "and the next tick of the conversation does not turn it back on");
            Assert.That(argueSecond.IsArguing, Is.True, "while the body that did not ask for it still argues");
            argueFirst.SetReducedMotion(false);
        }

        /// <summary>
        /// Two saved conversations at two venues: a tense one and a bonding one, each with the
        /// accepted start memory the validator requires in both directions.
        /// </summary>
        private static EpisodeState ArguingRuntimeFixture()
        {
            var state = ContentCatalog.Create(6103);
            state.revision = 12;
            state.npcSocial.clockTick = 10;
            state.npcSocial.nextScanTick = 12;
            state.npcSocial.nextConversationSequence = 3;
            state.npcSocial.randomState = 0; // Zero is a legal restored independent RNG state.
            Pair(state, 1, ContentCatalog.MayaId, "taylor-kim", "tension", "living-east-chat");
            Pair(state, 2, "jamie-roberts", "casey-wilson", "bonding", "kitchen-west-chat");
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            return state;
        }

        private static void Pair(EpisodeState state, long sequence, string first, string second, string topic, string venue)
        {
            state.npcSocial.pending.Add(new NpcConversationState
            {
                sequence = sequence, firstId = first, secondId = second, topic = topic,
                rendezvousId = venue, week = state.week, phase = state.phase,
                startedTick = 10, durationMs = 15000.125
            });
            state.npcSocial.pairMemory.Add(new NpcPairMemoryState
                { fromId = first, toId = second, count = 1, lastTopic = topic, lastStartTick = 10 });
            state.npcSocial.pairMemory.Add(new NpcPairMemoryState
                { fromId = second, toId = first, count = 1, lastTopic = topic, lastStartTick = 10 });
        }
    }
}
