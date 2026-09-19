using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The social vocabulary ported from the reference build's reducer.
    ///
    /// <para>The player used to have one conversation action where the source has fourteen. These
    /// pin the six that were missing against the source's own numbers, and — more importantly —
    /// against the two properties that are easy to break while adding them: every outcome comes off
    /// the season's own generator so a replay reproduces it, and nothing any of them reveals escapes
    /// the knowledge boundary acceptance A9 guards.</para>
    /// </summary>
    public sealed class SocialActionParityTests
    {
        // ---------------------------------------------------------------- determinism

        /// <summary>
        /// The whole set is replayable. Each of these rolls, and a roll taken off a fresh generator
        /// rather than the season's would make the same command produce a different house on reload
        /// — which is the failure every replay fixture in this project exists to catch.
        /// </summary>
        [TestCase(EpisodeCommandKind.AskForIntel)]
        [TestCase(EpisodeCommandKind.Eavesdrop)]
        [TestCase(EpisodeCommandKind.SpreadLie)]
        [TestCase(EpisodeCommandKind.VentAbout)]
        [TestCase(EpisodeCommandKind.SchemeAgainst)]
        public void TheSameSeedAndCommandProduceTheSameOutcome(EpisodeCommandKind kind)
        {
            var first = Apply(kind);
            var second = Apply(kind);

            CollectionAssert.AreEqual(Scores(first), Scores(second), kind + " is not reproducible.");
            Assert.That(first.randomState, Is.EqualTo(second.randomState), kind + " must draw from the saved stream.");
            CollectionAssert.AreEqual(
                first.memories.Select(m => m.ownerId + "|" + m.text).ToList(),
                second.memories.Select(m => m.ownerId + "|" + m.text).ToList());
        }

        [TestCase(EpisodeCommandKind.AskForIntel)]
        [TestCase(EpisodeCommandKind.Eavesdrop)]
        [TestCase(EpisodeCommandKind.SpreadLie)]
        [TestCase(EpisodeCommandKind.VentAbout)]
        [TestCase(EpisodeCommandKind.SchemeAgainst)]
        public void EveryActionCostsExactlyOneFromTheWeeksBudget(EpisodeCommandKind kind)
        {
            var engine = Engine();
            int before = EpisodeEngine.SocialActionsSpent(engine.Snapshot);
            var result = engine.Apply(Command(engine.Snapshot, kind));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(EpisodeEngine.SocialActionsSpent(result.state), Is.EqualTo(before + 1));
        }

        [TestCase(EpisodeCommandKind.AskForIntel)]
        [TestCase(EpisodeCommandKind.Eavesdrop)]
        [TestCase(EpisodeCommandKind.SpreadLie)]
        [TestCase(EpisodeCommandKind.VentAbout)]
        [TestCase(EpisodeCommandKind.SchemeAgainst)]
        public void EveryActionLeavesAValidSeason(EpisodeCommandKind kind)
        {
            Assert.That(EpisodeValidation.TryValidate(Apply(kind), out var error), Is.True, kind + ": " + error);
        }

        // ---------------------------------------------------------------- what each one does

        /// <summary>Source: two to four points, and the housemate remembers being asked.</summary>
        [Test]
        public void AskingForIntelBuildsTrustWithinTheSourcesRange()
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                var engine = Engine(seed);
                var before = engine.Snapshot;
                var partner = Partner(before);
                double was = before.Score(before.playerId, partner);

                var after = Accept(engine, Command(before, EpisodeCommandKind.AskForIntel, partner));
                double moved = after.Score(after.playerId, partner) - was;

                Assert.That(moved, Is.GreaterThan(0), "Seed " + seed + ": asking should build trust.");
                Assert.That(after.memories.Any(m => m.ownerId == partner && m.isPrivate), Is.True,
                    "The housemate should privately remember being asked.");
            }
        }

        /// <summary>
        /// Source: five to twelve points of damage between the two of them, and fifteen against the
        /// player when it comes back. The damage lands between the other two — not on the player,
        /// which is what makes it worth doing.
        /// </summary>
        [Test]
        public void ALieDamagesTheTwoPeopleItIsToldBetween()
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                var engine = Engine(seed);
                var before = engine.Snapshot;
                string recipient = Partner(before);
                string about = before.Active.First(c => !c.isPlayer && c.id != recipient).id;
                double was = before.Score(recipient, about);

                var after = Accept(engine, Command(before, EpisodeCommandKind.SpreadLie, recipient, about));
                double damage = after.Score(recipient, about) - was;

                Assert.That(damage, Is.LessThan(0), "Seed " + seed + ": a lie should sour them on its subject.");
                Assert.That(after.memories.Any(m => m.ownerId == recipient && m.subjectId == about), Is.True,
                    "The recipient remembers being told, whether or not it was true.");
            }
        }

        /// <summary>
        /// A lie does come back sometimes, and when it does the player pays for it. Across enough
        /// seeds both outcomes must appear, or the risk is not real.
        /// </summary>
        [Test]
        public void ALieIsSometimesDiscoveredAndSometimesNot()
        {
            bool sawDiscovery = false, sawSilence = false;
            for (uint seed = 1; seed <= 60 && !(sawDiscovery && sawSilence); seed++)
            {
                var engine = Engine(seed);
                var before = engine.Snapshot;
                string recipient = Partner(before);
                string about = before.Active.First(c => !c.isPlayer && c.id != recipient).id;
                double was = before.Score(before.playerId, about);

                var after = Accept(engine, Command(before, EpisodeCommandKind.SpreadLie, recipient, about));
                if (after.Score(after.playerId, about) < was) sawDiscovery = true; else sawSilence = true;
            }
            Assert.That(sawDiscovery, Is.True, "A lie that is never discovered carries no risk at all.");
            Assert.That(sawSilence, Is.True, "A lie that is always discovered is not worth telling.");
        }

        /// <summary>
        /// Source: eight to fifteen if it lands, minus ten if it does not. Venting is a real gamble,
        /// and a test that only ever saw it land would be describing a different action.
        /// </summary>
        [Test]
        public void VentingLandsSometimesAndBackfiresSometimes()
        {
            bool landed = false, backfired = false;
            for (uint seed = 1; seed <= 60 && !(landed && backfired); seed++)
            {
                var engine = Engine(seed);
                var before = engine.Snapshot;
                string listener = Partner(before);
                string about = before.Active.First(c => !c.isPlayer && c.id != listener).id;
                double was = before.Score(before.playerId, listener);

                var after = Accept(engine, Command(before, EpisodeCommandKind.VentAbout, listener, about));
                if (after.Score(after.playerId, listener) > was) landed = true; else backfired = true;
            }
            Assert.That(landed && backfired, Is.True, "Venting must be able to go either way.");
        }

        /// <summary>
        /// Scheming damages the target's bonds with other houseguests rather than the player's
        /// standing. Doing it quietly is the whole point; if it cost the player openly it would just
        /// be a worse version of confronting them.
        /// </summary>
        [Test]
        public void SchemingDamagesTheTargetsOtherBondsAndNotThePlayers()
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                var engine = Engine(seed);
                var before = engine.Snapshot;
                string target = Partner(before);
                double standing = before.Score(target, before.playerId);

                var after = Accept(engine, Command(before, EpisodeCommandKind.SchemeAgainst, target));

                double worst = after.Active.Where(c => !c.isPlayer && c.id != target)
                    .Select(c => after.Score(target, c.id) - before.Score(target, c.id))
                    .DefaultIfEmpty(0).Min();
                Assert.That(worst, Is.LessThan(0), "Seed " + seed + ": scheming should cost the target somewhere.");
                Assert.That(after.Score(target, after.playerId), Is.EqualTo(standing),
                    "The target must not learn it was you.");
            }
        }

        // ---------------------------------------------------------------- knowledge boundaries

        /// <summary>
        /// The claim that matters most. An overheard conversation becomes something the player knows
        /// privately, never a fact the house shares and never narration of a room the player's
        /// character was not standing in.
        /// </summary>
        [Test]
        public void EavesdroppingRevealsOnlyAPrivateMemoryTheListenerOwns()
        {
            int heard = 0, caught = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = Engine(seed);
                var before = engine.Snapshot;
                var after = Accept(engine, Command(before, EpisodeCommandKind.Eavesdrop));

                var added = after.memories.Skip(before.memories.Count).ToList();
                Assert.That(added, Has.Count.EqualTo(1), "Seed " + seed + ": exactly one memory should be written.");
                Assert.That(added[0].isPrivate, Is.True, "Overheard information is private by construction.");

                var events = after.events.Where(e => e.sequence >= before.nextSequence).ToList();
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].audienceIds, Does.Contain(after.playerId));
                Assert.That(events[0].audienceIds, Has.Count.LessThanOrEqualTo(2),
                    "Only the listener, and whoever caught them, may know this happened.");

                if (added[0].ownerId == after.playerId) heard++; else caught++;
            }
            Assert.That(heard, Is.GreaterThan(0), "Listening in should sometimes work.");
            Assert.That(caught, Is.GreaterThan(0), "Listening in should sometimes be noticed.");
            Assert.That(heard, Is.GreaterThan(caught),
                "The source catches you roughly three times in ten, so success should be the common case.");
        }

        /// <summary>
        /// A backdoor plan is intent, not a nomination, and it does not outlive its week.
        ///
        /// <para>It is set at nominations rather than during the social week, and costs no action.
        /// That is the only moment it can be set: during the social week the Head of Household title
        /// still belongs to last week's winner, and by campaigning the veto has already been used.
        /// </para>
        /// </summary>
        [Test]
        public void ABackdoorPlanIsIntentThatMovesNobodyAndExpiresWithTheWeek()
        {
            var engine = NominatingAsHeadOfHousehold();
            var before = engine.Snapshot;
            string target = EpisodeEngine.NominationCandidates(before).First().id;

            var after = Accept(engine, Command(before, EpisodeCommandKind.SetBackdoorPlan, target));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)),
                "A plan is not a conversation and does not spend the week's budget.");
            Assert.That(after.backdoorTargetId, Is.EqualTo(target));
            Assert.That(after.nominees, Does.Not.Contain(target), "Planning is not nominating.");
            CollectionAssert.AreEqual(Scores(before), Scores(after), "A plan told to nobody changes nobody's standing.");

            int guard = 0;
            while (engine.Snapshot.week == before.week && guard++ < 200)
            {
                var step = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(step.accepted, Is.True, step.reason);
            }
            Assert.That(engine.Snapshot.week, Is.GreaterThan(before.week));
            Assert.That(engine.Snapshot.backdoorTargetId, Is.Null, "A plan for a week that has ended is not a plan.");
        }

        [Test]
        public void ABackdoorCannotBePlannedOutsideNominations()
        {
            var engine = Engine();
            var state = engine.Snapshot;
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Social));

            var result = engine.Apply(Command(state, EpisodeCommandKind.SetBackdoorPlan, Partner(state)));
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.Contain("nominations are made"));
        }

        [Test]
        public void OnlyTheHeadOfHouseholdCanPlanABackdoor()
        {
            var engine = NominatingAsSomeoneElse();
            var state = engine.Snapshot;
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(state.hohId, Is.Not.EqualTo(state.playerId));

            var result = engine.Apply(Command(state, EpisodeCommandKind.SetBackdoorPlan,
                state.Active.First(c => !c.isPlayer && c.id != state.hohId).id));
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.Contain("Head of Household"));
        }

        /// <summary>A season driven to nominations with the player holding the title.</summary>
        private static EpisodeEngine NominatingAsHeadOfHousehold() => Nominating(true);

        /// <summary>The same, with the title in somebody else's hands.</summary>
        private static EpisodeEngine NominatingAsSomeoneElse() => Nominating(false);

        private static EpisodeEngine Nominating(bool playerHolds)
        {
            for (uint seed = 1; seed <= 80; seed++)
            {
                var engine = Engine(seed);
                for (int guard = 0; guard < 40; guard++)
                {
                    var state = engine.Snapshot;
                    if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0)
                    {
                        if ((state.hohId == state.playerId) == playerHolds) return engine;
                        break;
                    }
                    if (state.phase == EpisodePhase.Finished) break;
                    var step = engine.Apply(EpisodeEngineTests.NextCommand(state));
                    Assert.That(step.accepted, Is.True, step.reason);
                }
            }
            Assert.Fail("No bounded seed reached nominations with the title "
                + (playerHolds ? "held by the player." : "held by a housemate."));
            return null;
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeEngine Engine(uint seed = 4242) => new EpisodeEngine(ContentCatalog.Create(seed));

        private static string Partner(EpisodeState state) => state.Active.First(c => !c.isPlayer).id;

        /// <summary>The state after one action of this kind, from a fixed seed.</summary>
        private static EpisodeState Apply(EpisodeCommandKind kind)
        {
            var engine = Engine();
            return Accept(engine, Command(engine.Snapshot, kind));
        }

        private static EpisodeState Accept(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>
        /// A command of this kind with whatever second target it needs, so the parametrised tests
        /// above can treat the whole vocabulary uniformly.
        /// </summary>
        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind,
            string target = null, string second = null)
        {
            var others = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            target = target ?? others[0];
            if (second == null && (kind == EpisodeCommandKind.SpreadLie || kind == EpisodeCommandKind.VentAbout))
                second = others.First(id => id != target);

            return new EpisodeCommand
            {
                id = "social-parity-" + state.revision + "-" + kind,
                kind = kind,
                actorId = state.playerId,
                targetId = target,
                secondTargetId = second,
                expectedPhase = state.phase,
                expectedRevision = state.revision,
            };
        }

        private static List<string> Scores(EpisodeState state) => state.relationships
            .OrderBy(r => r.fromId).ThenBy(r => r.toId)
            .Select(r => r.fromId + "->" + r.toId + "=" + r.score.ToString("0.####"))
            .ToList();
    }
}
