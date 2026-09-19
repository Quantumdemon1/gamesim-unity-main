using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Things happening to the house.
    ///
    /// <para>Every week in this port happened because the player pressed something. Nothing arrived
    /// on its own and no week was unlike the last except in who won what. These tests are mostly
    /// about the two properties that make an event layer safe: it draws from the season's own
    /// generator so a replay sees the same situations, and a choice resolved to people when it was
    /// offered still means those people when it is answered.</para>
    /// </summary>
    public sealed class HouseEventTests
    {
        // ---------------------------------------------------------------- the catalog

        [Test]
        public void EveryTemplateIsWellFormedAndOffersARealDecision()
        {
            Assert.That(HouseEvents.Catalog, Is.Not.Empty);
            CollectionAssert.AllItemsAreUnique(HouseEvents.Catalog.Select(t => t.id).ToList());

            foreach (var template in HouseEvents.Catalog)
            {
                Assert.That(template.title, Is.Not.Null.And.Not.Empty);
                Assert.That(template.narrative, Is.Not.Null.And.Not.Empty);
                Assert.That(template.roles, Is.Not.Empty, template.id);
                Assert.That(template.options.Length, Is.GreaterThanOrEqualTo(2),
                    template.id + " offers no real choice.");

                foreach (var option in template.options)
                {
                    Assert.That(option.label, Is.Not.Null.And.Not.Empty, template.id);
                    Assert.That(HouseEventRisk.IsKnown(option.risk), Is.True, template.id + ": " + option.risk);
                    foreach (var move in option.moves)
                        Assert.That(template.roles.Contains(move.role)
                                    || move.role == HouseEvents.Ally || move.role == HouseEvents.Rival,
                            Is.True, template.id + " moves " + move.role + ", which it does not cast.");
                }

                // Every narrative's placeholders are parts the template actually casts.
                foreach (string role in new[] { HouseEvents.Ally, HouseEvents.Rival, HouseEvents.Hoh, HouseEvents.Nominee })
                    if (template.narrative.Contains(role))
                        Assert.That(template.roles, Does.Contain(role),
                            template.id + " names " + role + " without casting it.");
            }
        }

        /// <summary>A situation with no consequences either way would be a paragraph, not an event.</summary>
        [Test]
        public void EveryTemplateHasAChoiceThatCostsAndAChoiceThatDoesNot()
        {
            foreach (var template in HouseEvents.Catalog)
            {
                Assert.That(template.options.Any(o => o.moves.Any(m => m.amount > 0) || o.trust > 0),
                    Is.True, template.id + " has no upside anywhere.");
                Assert.That(template.options.Any(o => o.moves.Any(m => m.amount < 0) || o.trust < 0),
                    Is.True, template.id + " has no downside anywhere.");
            }
        }

        // ---------------------------------------------------------------- casting

        [Test]
        public void TheAllyIsWhoTheyAreWarmestWithAndTheRivalIsWhoTheyAreColdestWith()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            Set(state, state.playerId, cast[0].id, -50);
            Set(state, state.playerId, cast[1].id, 70);

            var roles = HouseEvents.Roles(state);
            Assert.That(roles[HouseEvents.Ally], Is.EqualTo(cast[1].id));
            Assert.That(roles[HouseEvents.Rival], Is.EqualTo(cast[0].id));
        }

        [Test]
        public void EveryPartIsCastEvenBeforeAnybodyHoldsThePower()
        {
            var state = Season(5u, 8);
            Assert.That(state.hohId, Is.Null.Or.Empty);
            Assert.That(state.nominees, Is.Empty);

            var roles = HouseEvents.Roles(state);
            foreach (string role in new[] { HouseEvents.Ally, HouseEvents.Rival, HouseEvents.Hoh, HouseEvents.Nominee })
            {
                Assert.That(roles.ContainsKey(role), Is.True, role);
                Assert.That(state.Find(roles[role]), Is.Not.Null, role);
                Assert.That(state.Find(roles[role]).isPlayer, Is.False, role + " must never be the player.");
            }
        }

        [Test]
        public void ThePowerIsCastToWhoeverActuallyHoldsIt()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            state.hohId = cast[2].id;
            state.nominees = new List<string> { cast[3].id, cast[4].id };

            var roles = HouseEvents.Roles(state);
            Assert.That(roles[HouseEvents.Hoh], Is.EqualTo(cast[2].id));
            Assert.That(roles[HouseEvents.Nominee], Is.EqualTo(cast[3].id));
        }

        [Test]
        public void AHouseTooSmallToHaveSidesCastsNobody()
        {
            var state = Season(5u, 8);
            foreach (var gone in Npcs(state).Skip(1)) gone.status = ContestantStatus.Jury;
            Assert.That(HouseEvents.Roles(state), Is.Empty);
            Assert.That(HouseEvents.Ready(state), Is.False);
            Assert.That(HouseEvents.Draw(state, 0.5, 1), Is.Null);
        }

        [Test]
        public void APlaceholderNeverReachesThePlayer()
        {
            var state = Season(5u, 8);
            var roles = HouseEvents.Roles(state);
            for (int i = 0; i < HouseEvents.Catalog.Length; i++)
            {
                var drawn = HouseEvents.Draw(state, (i + 0.5) / HouseEvents.Catalog.Length, i + 1);
                Assert.That(drawn, Is.Not.Null);
                foreach (string role in new[] { HouseEvents.Ally, HouseEvents.Rival, HouseEvents.Hoh, HouseEvents.Nominee })
                    Assert.That(drawn.narrative, Does.Not.Contain(role), drawn.title);
                Assert.That(drawn.narrative, Does.Not.Contain("{"));
            }
        }

        // ---------------------------------------------------------------- drawing

        [Test]
        public void EveryTemplateInTheCatalogCanBeDrawn()
        {
            var state = Season(5u, 8);
            var titles = new HashSet<string>();
            for (double roll = 0; roll < 1; roll += 0.01)
            {
                var drawn = HouseEvents.Draw(state, roll, 1);
                if (drawn != null) titles.Add(drawn.title);
            }
            CollectionAssert.AreEquivalent(HouseEvents.Catalog.Select(t => t.title).ToList(), titles.ToList());
        }

        [Test]
        public void ADrawnEventIsUnansweredAndValid()
        {
            var state = Season(5u, 8);
            var drawn = HouseEvents.Draw(state, 0.5, 42);
            Assert.That(drawn, Is.Not.Null);
            Assert.That(drawn.resolved, Is.False);
            Assert.That(drawn.chosenIndex, Is.EqualTo(-1));
            Assert.That(drawn.outcome, Is.Null);
            Assert.That(drawn.week, Is.EqualTo(state.week));
            Assert.That(drawn.kind, Is.EqualTo(HouseEventKind.House));
            Assert.That(drawn.involvedIds, Is.Not.Empty);
            Assert.That(drawn.involvedIds, Has.All.Matches<string>(id => state.Find(id) != null));

            state.houseEvents.Add(drawn);
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        }

        /// <summary>
        /// The reason the choices are stored: they name people, not roles, so answering later moves
        /// who it was always about.
        /// </summary>
        [Test]
        public void AChoiceNamesPeopleRatherThanRolesTheMomentItIsOffered()
        {
            var state = Season(5u, 8);
            var drawn = HouseEvents.Draw(state, 0.5, 1);
            Assert.That(drawn.choices.SelectMany(c => c.impacts), Is.Not.Empty);
            foreach (var impact in drawn.choices.SelectMany(c => c.impacts))
            {
                Assert.That(state.Find(impact.targetId), Is.Not.Null);
                Assert.That(impact.targetId, Does.Not.StartWith("{"));
            }
        }

        [Test]
        public void OnlyOneThingHappensToTheHousePerWeek()
        {
            var state = Season(5u, 8);
            state.houseEvents.Add(HouseEvents.Draw(state, 0.2, 1));
            Assert.That(HouseEvents.Ready(state), Is.False);
            Assert.That(HouseEvents.Draw(state, 0.8, 2), Is.Null);

            state.week = 2;
            Assert.That(HouseEvents.Ready(state), Is.True);
        }

        [Test]
        public void NothingHappensBeforeTheHousesEventRulesBegin()
        {
            var state = Season(5u, 8);
            state.eventRulesStartWeek = state.week + 1;
            Assert.That(HouseEvents.Ready(state), Is.False);
            Assert.That(HouseEvents.Draw(state, 0.5, 1), Is.Null);
        }

        [Test]
        public void NothingHappensToSomebodyWhoHasLeft()
        {
            var state = Season(5u, 8);
            state.Find(state.playerId).status = ContestantStatus.Jury;
            Assert.That(HouseEvents.Ready(state), Is.False);
        }

        // ---------------------------------------------------------------- answering

        [Test]
        public void AnsweringMovesExactlyWhoTheChoiceNamed()
        {
            var engine = Engine(out var state);
            var item = Seed(engine, 0.5);
            var choice = item.choices.First(c => c.impacts.Count > 0);
            var before = engine.Snapshot;

            var result = engine.Apply(Answer(before, item.id, choice.label));
            Assert.That(result.accepted, Is.True, result.reason);

            var after = engine.Snapshot;
            foreach (var impact in choice.impacts)
            {
                double moved = after.Score(after.playerId, impact.targetId)
                               - before.Score(before.playerId, impact.targetId);
                Assert.That(System.Math.Sign(moved), Is.EqualTo(System.Math.Sign(impact.amount)),
                    state.Find(impact.targetId).name);
            }

            var settled = after.houseEvents.Single(e => e.id == item.id);
            Assert.That(settled.resolved, Is.True);
            Assert.That(settled.chosenIndex, Is.EqualTo(item.choices.IndexOf(choice)));
            Assert.That(settled.outcome, Is.Not.Null.And.Not.Empty);
            Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
        }

        /// <summary>
        /// Trust has to leave a mark the ledger can see, because that is what trust is here — a
        /// counter nothing reads would be a cost that costs nothing.
        /// </summary>
        [Test]
        public void AChoiceThatCostsTrustIsVisibleToWhoeverSawIt()
        {
            var engine = Engine(out var state);
            var item = Seed(engine, 0.5);
            var costly = item.choices.FirstOrDefault(c => c.trustChange < 0);
            if (costly == null) Assert.Ignore("This situation has no choice that costs trust.");

            string witness = item.involvedIds[0];
            var before = engine.Snapshot;
            double trustBefore = ThreatAssessment.TrustScore(before, before.playerId, witness);

            Assert.That(engine.Apply(Answer(before, item.id, costly.label)).accepted, Is.True);

            var after = engine.Snapshot;
            Assert.That(ThreatAssessment.TrustScore(after, after.playerId, witness),
                Is.LessThan(trustBefore));
        }

        [Test]
        public void ASituationCanOnlyBeAnsweredOnce()
        {
            var engine = Engine(out var state);
            var item = Seed(engine, 0.5);
            string label = item.choices[0].label;

            Assert.That(engine.Apply(Answer(engine.Snapshot, item.id, label)).accepted, Is.True);
            var again = engine.Apply(Answer(engine.Snapshot, item.id, label));
            Assert.That(again.accepted, Is.False);
            Assert.That(again.reason, Does.Contain("already passed"));
        }

        [Test]
        public void AnswerWithSomethingNobodyOfferedIsRefusedAndCostsNothing()
        {
            var engine = Engine(out var state);
            var item = Seed(engine, 0.5);
            var before = engine.Snapshot;

            var result = engine.Apply(Answer(before, item.id, "Set fire to the house"));
            Assert.That(result.accepted, Is.False);
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(before.randomState));
            Assert.That(engine.Snapshot.houseEvents.Single(e => e.id == item.id).resolved, Is.False);
        }

        [Test]
        public void AnsweringIsNotASocialAction()
        {
            var engine = Engine(out var state);
            var item = Seed(engine, 0.5);
            int spent = EpisodeEngine.SocialActionsSpent(engine.Snapshot);

            Assert.That(engine.Apply(Answer(engine.Snapshot, item.id, item.choices[0].label)).accepted, Is.True);
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(spent),
                "The situation came to the player; charging them for being walked in on "
                + "would be charging for the weather.");
        }

        // ---------------------------------------------------------------- through a season

        [Test]
        public void ASeasonPlayedThroughTheEngineHasThingsHappenToIt()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(7u));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }

            var finished = engine.Snapshot;
            Assert.That(finished.houseEvents, Is.Not.Empty,
                "A whole season should have had something happen to it.");
            CollectionAssert.AllItemsAreUnique(finished.houseEvents.Select(e => e.id).ToList());
            // Nothing answers for the player, so a season played without touching them leaves them open.
            Assert.That(finished.houseEvents, Has.All.Matches<HouseEventState>(e => !e.resolved));
            Assert.That(finished.houseEvents.Select(e => e.week).Distinct().Count(),
                Is.EqualTo(finished.houseEvents.Count), "One a week, no more.");
            Assert.That(EpisodeValidation.TryValidate(finished, out string error), Is.True, error);
        }

        [Test]
        public void TheSameSeasonHasTheSameThingsHappenToIt()
        {
            CollectionAssert.AreEqual(Digest(Play(9u)), Digest(Play(9u)));
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

        private static EpisodeEngine Engine(out EpisodeState state, uint seed = 7u)
        {
            state = Season(seed, 10);
            state.phase = EpisodePhase.Social;
            return new EpisodeEngine(state);
        }

        /// <summary>
        /// Puts a situation in front of the player without playing a week to reach one.
        ///
        /// <para>The engine draws these at a phase transition; driving a whole week would make every
        /// test below about the week rather than about the answer. There is no command for "the
        /// house does something" — that is the house's move, not the player's — so the fixture
        /// writes it the way the engine does.</para>
        /// </summary>
        private static HouseEventState Seed(EpisodeEngine engine, double roll)
        {
            var live = (EpisodeState)typeof(EpisodeEngine)
                .GetField("current", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(engine);
            var drawn = HouseEvents.Draw(live, roll, live.nextSequence);
            Assert.That(drawn, Is.Not.Null, "The fixture needs a situation to answer.");
            live.houseEvents.Add(drawn);
            return drawn;
        }

        private static EpisodeCommand Answer(EpisodeState s, string id, string label) =>
            new EpisodeCommand
            {
                id = "cmd-" + s.revision, kind = EpisodeCommandKind.ResolveHouseEvent,
                actorId = s.playerId, targetId = id, text = label,
                expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static EpisodeState Play(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
                engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
            return engine.Snapshot;
        }

        private static List<string> Digest(EpisodeState state) =>
            state.houseEvents.Select(e => e.week + ":" + e.title + ":" + string.Join(",", e.involvedIds)).ToList();
    }
}
