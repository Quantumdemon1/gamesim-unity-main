using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The four event sources beyond the template catalogue.
    ///
    /// <para>All four write the same <see cref="HouseEventState"/>, so the shared properties — a
    /// valid season, people rather than roles, nothing aimed at the player — are checked once per
    /// source rather than argued about four times.</para>
    /// </summary>
    public sealed class HouseEventSourceTests
    {
        // ---------------------------------------------------------------- ambient

        /// <summary>
        /// Ambient narration is the one kind that asks nothing, so it arrives already settled — an
        /// unanswered one would occupy the week's single situation and block the ones that matter.
        /// </summary>
        [Test]
        public void AmbientNarrationArrivesSettledAndAsksNothing()
        {
            var state = Season(5u, 8);
            for (double pick = 0; pick < 1; pick += 0.05)
            {
                var line = HouseEventSources.Ambient(state, pick, 0.3, "the kitchen", 1);
                Assert.That(line, Is.Not.Null, pick.ToString());
                Assert.That(line.kind, Is.EqualTo(HouseEventKind.Ambient));
                Assert.That(HouseEventKind.Asks(line.kind), Is.False);
                Assert.That(line.choices, Is.Empty);
                Assert.That(line.resolved, Is.True);
                Assert.That(line.chosenIndex, Is.EqualTo(-1));
                Assert.That(line.narrative, Does.Not.Contain("{"));
            }
        }

        [Test]
        public void AmbientNarrationReachesAllThreeShapes()
        {
            var state = Season(5u, 8);
            var counts = new Dictionary<int, int> { { 0, 0 }, { 1, 0 }, { 2, 0 } };
            for (double pick = 0; pick < 1; pick += 0.01)
            {
                var line = HouseEventSources.Ambient(state, pick, 0.3, "the kitchen", 1);
                counts[line.involvedIds.Count]++;
            }
            Assert.That(counts[1], Is.GreaterThan(0), "solo");
            Assert.That(counts[2], Is.GreaterThan(0), "pair");
            Assert.That(counts[0], Is.GreaterThan(0), "the house at large");
        }

        [Test]
        public void AmbientNarrationNamesARoomAndNeverTheEmptyString()
        {
            var state = Season(5u, 8);
            var solo = HouseEventSources.Ambient(state, 0.1, 0.3, null, 1);
            Assert.That(solo.narrative, Does.Contain("the house"));
            Assert.That(HouseEventSources.Ambient(state, 0.1, 0.3, "  ", 1).narrative, Does.Contain("the house"));
        }

        [Test]
        public void AHouseWithNobodyLeftNarratesNothing()
        {
            var state = Season(5u, 8);
            foreach (var npc in state.contestants.Where(c => !c.isPlayer)) npc.status = ContestantStatus.Jury;
            Assert.That(HouseEventSources.Ambient(state, 0.2, 0.2, "the kitchen", 1), Is.Null);
        }

        // ---------------------------------------------------------------- proximity

        [Test]
        public void WalkingInOnTwoHouseguestsOffersARealDecision()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            var drawn = HouseEventSources.Proximity(state, cast[0].id, cast[1].id, "the kitchen", 1);

            Assert.That(drawn, Is.Not.Null);
            Assert.That(drawn.kind, Is.EqualTo(HouseEventKind.Proximity));
            Assert.That(drawn.narrative, Does.Contain("the kitchen"));
            Assert.That(drawn.narrative, Does.Contain(cast[0].name).And.Contain(cast[1].name));
            Assert.That(drawn.choices.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(drawn.resolved, Is.False);
            CollectionAssert.AreEquivalent(new[] { cast[0].id, cast[1].id }, drawn.involvedIds);

            state.houseEvents.Add(drawn);
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        }

        /// <summary>A warm pair stopping talking reads differently from a pair mid-argument.</summary>
        [Test]
        public void WhatYouWalkedInOnDependsOnWhetherTheyGetOn()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            Set(state, cast[0].id, cast[1].id, HouseEventSources.UnspokenPairWarmth + 5);
            var close = HouseEventSources.Proximity(state, cast[0].id, cast[1].id, "the kitchen", 1);
            Assert.That(close.narrative, Does.Contain("stop talking"));

            Set(state, cast[0].id, cast[1].id, -20);
            var sour = HouseEventSources.Proximity(state, cast[0].id, cast[1].id, "the kitchen", 2);
            Assert.That(sour.narrative, Does.Contain("argument"));
        }

        [Test]
        public void YouCannotWalkInOnYourselfOrOnNobody()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            Assert.That(HouseEventSources.Proximity(state, state.playerId, cast[0].id, "x", 1), Is.Null);
            Assert.That(HouseEventSources.Proximity(state, cast[0].id, cast[0].id, "x", 1), Is.Null);
            Assert.That(HouseEventSources.Proximity(state, cast[0].id, "nobody", "x", 1), Is.Null);

            cast[1].status = ContestantStatus.Jury;
            Assert.That(HouseEventSources.Proximity(state, cast[0].id, cast[1].id, "x", 1), Is.Null,
                "Somebody who has left is not in the kitchen.");
        }

        // ---------------------------------------------------------------- emergent

        [Test]
        public void AQuietSeasonProducesNothingEmergent()
        {
            var state = Season(5u, 8);
            Assert.That(HouseEventSources.Emergent(state, 1), Is.Null,
                "Most weeks the season has made nothing worth remarking on. That is what emergent means.");
        }

        [Test]
        public void APairWarmEnoughAndUnalliedBecomesSomethingWorthNaming()
        {
            var state = Season(5u, 8);
            var friend = Npcs(state)[0];
            Set(state, state.playerId, friend.id, HouseEventSources.UnspokenPairWarmth + 5);

            var drawn = HouseEventSources.Emergent(state, 1);
            Assert.That(drawn, Is.Not.Null);
            Assert.That(drawn.kind, Is.EqualTo(HouseEventKind.Emergent));
            Assert.That(drawn.involvedIds, Is.EqualTo(new[] { friend.id }));
            Assert.That(drawn.narrative, Does.Contain(friend.name));

            state.alliances.Add(new AllianceState
            {
                id = "a", name = "The Pact", members = new List<string> { state.playerId, friend.id },
            });
            Assert.That(HouseEventSources.Emergent(state, 2), Is.Null,
                "Once it is named there is nothing left to name.");
        }

        /// <summary>
        /// An arc says what it is. Reading the current score instead would call a rivalry a
        /// friendship the moment one good conversation pushed it above zero.
        /// </summary>
        [Test]
        public void AnEscalatedRivalryReadsAsARivalryEvenWhileTheScoreIsRecovering()
        {
            var state = Season(5u, 8);
            var rival = Npcs(state)[0];
            state.relationshipArcs.Add(new RelationshipArcState
            {
                npcId = rival.id, npcName = rival.name, arcType = "rivalry",
                intensity = 80, escalationLevel = HouseEventSources.EscalatedArc,
            });
            Set(state, state.playerId, rival.id, 40);

            var drawn = HouseEventSources.Emergent(state, 1);
            Assert.That(drawn, Is.Not.Null);
            Assert.That(drawn.title, Is.EqualTo("This has gone far enough"));
            Assert.That(drawn.choices[0].label, Does.Contain("out in the open"));
        }

        [Test]
        public void AnArcThatHasNotEscalatedIsNotAnEvent()
        {
            var state = Season(5u, 8);
            var other = Npcs(state)[0];
            state.relationshipArcs.Add(new RelationshipArcState
            {
                npcId = other.id, npcName = other.name, arcType = "rivalry",
                intensity = 80, escalationLevel = HouseEventSources.EscalatedArc - 1,
            });
            Assert.That(HouseEventSources.Emergent(state, 1), Is.Null);
        }

        [Test]
        public void AnArcAboutSomebodyWhoHasLeftIsNotAnEvent()
        {
            var state = Season(5u, 8);
            var gone = Npcs(state)[0];
            state.relationshipArcs.Add(new RelationshipArcState
            {
                npcId = gone.id, npcName = gone.name, arcType = "rivalry",
                intensity = 90, escalationLevel = 4,
            });
            gone.status = ContestantStatus.Jury;
            Assert.That(HouseEventSources.Emergent(state, 1), Is.Null);
        }

        // ---------------------------------------------------------------- crisis

        [Test]
        public void NothingGoesWrongInTheFirstWeek()
        {
            var state = Season(5u, 8);
            state.week = HouseEventSources.CrisisFirstWeek - 1;
            Assert.That(HouseEventSources.Crisis(state, 0.5, 1), Is.Null);

            state.week = HouseEventSources.CrisisFirstWeek;
            Assert.That(HouseEventSources.Crisis(state, 0.5, 1), Is.Not.Null);
        }

        /// <summary>
        /// The reference prefers the parties to a real deal, because being caught listening to two
        /// people who have actually agreed something is worse than overhearing nobody in particular.
        /// That preference could not be ported until deals existed.
        /// </summary>
        [Test]
        public void BeingCaughtListeningPrefersPeopleWhoHaveActuallyAgreedSomething()
        {
            var state = Season(5u, 8);
            state.week = 3;
            var cast = Npcs(state);
            var parties = new[] { cast[3].id, cast[4].id };
            state.deals.Add(new DealState
            {
                id = "d", type = DealKind.Partnership, proposerId = parties[0], recipientId = parties[1],
                status = DealStatus.Active, week = 1, trustImpact = DealTrust.Medium,
            });

            var drawn = HouseEventSources.Crisis(state, 0.01, 1);
            Assert.That(drawn, Is.Not.Null);
            CollectionAssert.AreEquivalent(parties, drawn.involvedIds);
            Assert.That(drawn.kind, Is.EqualTo(HouseEventKind.Crisis));
        }

        [Test]
        public void ADealThePlayerIsPartyToIsNotSomethingTheyCanOverhear()
        {
            var state = Season(5u, 8);
            state.week = 3;
            var cast = Npcs(state);
            state.deals.Add(new DealState
            {
                id = "d", type = DealKind.Partnership, proposerId = state.playerId, recipientId = cast[0].id,
                status = DealStatus.Active, week = 1, trustImpact = DealTrust.Medium,
            });

            var drawn = HouseEventSources.Crisis(state, 0.01, 1);
            Assert.That(drawn.involvedIds, Does.Not.Contain(state.playerId));
        }

        // ---------------------------------------------------------------- shared properties

        [Test]
        public void NoSourceEverAimsAnythingAtThePlayer()
        {
            var state = Season(5u, 8);
            state.week = 3;
            var cast = Npcs(state);
            Set(state, state.playerId, cast[0].id, 80);
            state.relationshipArcs.Add(new RelationshipArcState
            {
                npcId = cast[1].id, npcName = cast[1].name, arcType = "rivalry",
                intensity = 90, escalationLevel = 4,
            });

            foreach (var drawn in new[]
                     {
                         HouseEventSources.Ambient(state, 0.6, 0.2, "the kitchen", 1),
                         HouseEventSources.Proximity(state, cast[0].id, cast[1].id, "the kitchen", 2),
                         HouseEventSources.Emergent(state, 3),
                         HouseEventSources.Crisis(state, 0.4, 4),
                     })
            {
                Assert.That(drawn, Is.Not.Null);
                Assert.That(drawn.involvedIds, Does.Not.Contain(state.playerId), drawn.kind);
                foreach (var impact in drawn.choices.SelectMany(c => c.impacts))
                {
                    Assert.That(impact.targetId, Is.Not.EqualTo(state.playerId), drawn.kind);
                    Assert.That(state.Find(impact.targetId), Is.Not.Null, drawn.kind);
                }
            }
        }

        [Test]
        public void EverySourceProducesSomethingValidationAccepts()
        {
            var state = Season(5u, 8);
            state.week = 3;
            var cast = Npcs(state);
            Set(state, state.playerId, cast[0].id, 80);

            long sequence = state.nextSequence;
            foreach (var drawn in new[]
                     {
                         HouseEventSources.Ambient(state, 0.6, 0.2, "the kitchen", sequence++),
                         HouseEventSources.Proximity(state, cast[0].id, cast[1].id, "the kitchen", sequence++),
                         HouseEventSources.Emergent(state, sequence++),
                         HouseEventSources.Crisis(state, 0.4, sequence++),
                     })
            {
                Assert.That(drawn, Is.Not.Null);
                state.houseEvents.Add(drawn);
            }
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        }

        [Test]
        public void EveryRoomTheSimulationCanNameIsOneTheHouseActuallyHas()
        {
            Assert.That(HouseRooms.All, Is.Not.Empty);
            CollectionAssert.AllItemsAreUnique(HouseRooms.All);
            Assert.That(HouseRooms.All, Has.All.Matches<string>(r => r.StartsWith("the ")));
            // The reference names a hot tub and a hammock; this house has neither, and narration
            // that named a room the player cannot walk into would contradict the set.
            Assert.That(HouseRooms.All, Does.Not.Contain("the hot tub"));
            Assert.That(HouseRooms.All, Does.Not.Contain("the hammock"));

            var state = Season(5u, 8);
            Assert.That(HouseRooms.Any(state, 0), Is.EqualTo(HouseRooms.All[0]));
            Assert.That(HouseRooms.Any(state, 0.999), Is.EqualTo(HouseRooms.All[HouseRooms.All.Length - 1]));
            Assert.That(HouseRooms.Any(state, 1), Is.EqualTo(HouseRooms.All[HouseRooms.All.Length - 1]));
            Assert.That(HouseRooms.Any(state, -1), Is.EqualTo(HouseRooms.All[0]));
        }

        // ---------------------------------------------------------------- through a season

        [Test]
        public void ASeasonPlayedThroughTheEngineNarratesItselfAsWellAsQuestioningThePlayer()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(7u));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }

            var finished = engine.Snapshot;
            Assert.That(finished.houseEvents.Any(e => e.kind == HouseEventKind.Ambient), Is.True,
                "The house should have been a house.");
            Assert.That(finished.houseEvents.Where(e => e.kind == HouseEventKind.Ambient),
                Has.All.Matches<HouseEventState>(e => e.resolved && e.choices.Count == 0));
            // At most one ambient line and one situation per week.
            foreach (var group in finished.houseEvents.GroupBy(e => new { e.week, ambient = e.kind == HouseEventKind.Ambient }))
                Assert.That(group.Count(), Is.EqualTo(1), "week " + group.Key.week);
            CollectionAssert.AllItemsAreUnique(finished.houseEvents.Select(e => e.id).ToList());
            Assert.That(EpisodeValidation.TryValidate(finished, out string error), Is.True, error);
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState state) =>
            state.Active.Where(c => !c.isPlayer).ToList();

        private static void Set(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r => r.fromId == from && r.toId == to))
                edge.score = score;
        }
    }
}
