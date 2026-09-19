using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Ways of talking to somebody, and what each costs.
    ///
    /// <para>Every conversation in this port moved a flat four points, so choosing how to approach
    /// somebody was a choice with no consequences. The ranges below are the reference's, pinned
    /// here rather than left to the engine, because a range only tested through a seeded season is
    /// a range nobody can read.</para>
    /// </summary>
    public sealed class SocialVocabularyTests
    {
        // ---------------------------------------------------------------- the ranges

        [Test]
        public void EverySafeConversationStaysInsideItsRange()
        {
            Span(WebSocialVocabulary.SmallTalk, 3, 5);
            Span(WebSocialVocabulary.PersonalChat, 5, 8);
            Span(WebSocialVocabulary.RelationshipBuilding, 5, 12);
            Span(WebSocialVocabulary.StrategicDiscussion, 2, 5);
        }

        /// <summary>
        /// The source's own ordering: time spent beats anything said.
        ///
        /// <para>Tactics and small talk are the interesting pair. Their ranges are 2–5 and 3–5, so
        /// they <b>tie at their best</b> and differ only at the floor — a tactical conversation can
        /// land as poorly as two where small talk never does. Comparing their ceilings, which is
        /// what this test did first, says they are equivalent; comparing their floors says what is
        /// actually true, which is that tactics are the colder option on average because they are
        /// not really about liking each other.</para>
        /// </summary>
        [Test]
        public void TimeSpentBeatsWordsAndTacticsRiskTheColdestConversation()
        {
            Assert.That(WebSocialVocabulary.RelationshipBuilding(0.99),
                Is.GreaterThan(WebSocialVocabulary.PersonalChat(0.99)));
            Assert.That(WebSocialVocabulary.PersonalChat(0.99),
                Is.GreaterThan(WebSocialVocabulary.SmallTalk(0.99)));

            Assert.That(WebSocialVocabulary.StrategicDiscussion(0),
                Is.LessThan(WebSocialVocabulary.SmallTalk(0)), "Two against three at the floor.");
            Assert.That(WebSocialVocabulary.StrategicDiscussion(0.99),
                Is.EqualTo(WebSocialVocabulary.SmallTalk(0.99)), "Five against five at the ceiling.");
            Assert.That(Average(WebSocialVocabulary.StrategicDiscussion),
                Is.LessThan(Average(WebSocialVocabulary.SmallTalk)));
        }

        [Test]
        public void ARiskyConversationPaysMoreAndCostsMoreTheRiskierItIs()
        {
            Assert.That(WebSocialVocabulary.DiscussGame(0.5, 0.99), Is.EqualTo(10));
            Assert.That(WebSocialVocabulary.DiscussGame(0.5, 0), Is.EqualTo(4));
            Assert.That(WebSocialVocabulary.DiscussGame(0.2, 0.99), Is.EqualTo(-5));

            Assert.That(WebSocialVocabulary.ShareSecret(0.5, 0.99), Is.EqualTo(18));
            Assert.That(WebSocialVocabulary.ShareSecret(0.5, 0), Is.EqualTo(10));
            Assert.That(WebSocialVocabulary.ShareSecret(0.2, 0.99), Is.EqualTo(-15));

            Assert.That(WebSocialVocabulary.ShareSecret(0.5, 0.99),
                Is.GreaterThan(WebSocialVocabulary.DiscussGame(0.5, 0.99)));
            Assert.That(WebSocialVocabulary.ShareSecret(0, 0),
                Is.LessThan(WebSocialVocabulary.DiscussGame(0, 0)));
        }

        /// <summary>The gate is exclusive, exactly as the source's <c>&gt;</c> is.</summary>
        [Test]
        public void TheGateIsExclusiveAtItsOwnThreshold()
        {
            Assert.That(WebSocialVocabulary.DiscussGame(WebSocialVocabulary.DiscussGameFloor, 0.9),
                Is.EqualTo(WebSocialVocabulary.DiscussGameBackfire));
            Assert.That(WebSocialVocabulary.ShareSecret(WebSocialVocabulary.ShareSecretFloor, 0.9),
                Is.EqualTo(WebSocialVocabulary.ShareSecretBackfire));
        }

        [Test]
        public void ARollOfOneNeverStepsOutsideTheRange()
        {
            // Roll() never returns one, but a range that is right by luck stops being right when
            // the generator changes.
            Assert.That(WebSocialVocabulary.SmallTalk(1), Is.EqualTo(5));
            Assert.That(WebSocialVocabulary.RelationshipBuilding(1), Is.EqualTo(12));
            Assert.That(WebSocialVocabulary.SmallTalk(-1), Is.EqualTo(3));
            Assert.That(WebSocialVocabulary.SmallTalk(double.NaN), Is.EqualTo(3));
        }

        [Test]
        public void TheHouseWideNumbersAreTheReferences()
        {
            Span(WebSocialVocabulary.WhisperDamage, -9, -4);
            Span(WebSocialVocabulary.CalloutDamage, -12, -5);
            Span(WebSocialVocabulary.MeetingFailure, -5, -2);
            Span(WebSocialVocabulary.MeetingRally, 3, 8);

            Assert.That(WebSocialVocabulary.CalloutAudience(0), Is.EqualTo(2));
            Assert.That(WebSocialVocabulary.CalloutAudience(0.99), Is.EqualTo(3));
            Assert.That(WebSocialVocabulary.MeetingAiring(0.99), Is.EqualTo(8));
            Assert.That(WebSocialVocabulary.MeetingAiring(0.1), Is.EqualTo(-10));
            Assert.That(WebSocialVocabulary.CalloutBackfire,
                Is.EqualTo(WebSocialVocabulary.WhisperBackfire * 2),
                "Reach is paid for with exposure: saying it out loud costs twice what whispering does.");
        }

        // ---------------------------------------------------------------- through the engine

        [Test]
        public void EveryNewConversationMovesSomethingAndSpendsAnAction()
        {
            foreach (var kind in Conversations)
            {
                var engine = Engine(out var state);
                var target = Npcs(state)[0];
                var before = engine.Snapshot;

                var result = engine.Apply(Command(before, kind, target.id));
                Assert.That(result.accepted, Is.True, kind + ": " + result.reason);

                var after = engine.Snapshot;
                Assert.That(after.Score(after.playerId, target.id),
                    Is.Not.EqualTo(before.Score(before.playerId, target.id)), kind.ToString());
                Assert.That(EpisodeEngine.SocialActionsSpent(after),
                    Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1), kind.ToString());
                Assert.That(after.randomState, Is.Not.EqualTo(before.randomState),
                    kind + " should have drawn from the season's generator.");
                Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
            }
        }

        [Test]
        public void TheSameSeasonSaysTheSameThingTwice()
        {
            foreach (var kind in Conversations)
            {
                var first = Engine(out var a);
                var second = Engine(out _);
                string target = Npcs(a)[0].id;

                first.Apply(Command(first.Snapshot, kind, target));
                second.Apply(Command(second.Snapshot, kind, target));

                Assert.That(first.Snapshot.randomState, Is.EqualTo(second.Snapshot.randomState), kind.ToString());
                Assert.That(first.Snapshot.Score(first.Snapshot.playerId, target),
                    Is.EqualTo(second.Snapshot.Score(second.Snapshot.playerId, target)), kind.ToString());
            }
        }

        /// <summary>
        /// A risky conversation draws twice — the gate, then the amount. Once would tie whether it
        /// worked to how well, which is a different game.
        /// </summary>
        [Test]
        public void ARiskyConversationDrawsTwiceAndASafeOneOnce()
        {
            Assert.That(Draws(EpisodeCommandKind.SmallTalk), Is.EqualTo(Draws(EpisodeCommandKind.PersonalChat)));
            Assert.That(Draws(EpisodeCommandKind.DiscussGame),
                Is.EqualTo(Draws(EpisodeCommandKind.SmallTalk) + 1));
            Assert.That(Draws(EpisodeCommandKind.ShareSecret),
                Is.EqualTo(Draws(EpisodeCommandKind.SmallTalk) + 1));
        }

        /// <summary>Over enough seeds, both outcomes of a risky conversation actually happen.</summary>
        [Test]
        public void ARiskyConversationSometimesBackfiresAndUsuallyDoesNot()
        {
            int landed = 0, backfired = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = Engine(out var state, seed);
                var target = Npcs(state)[0];
                double before = engine.Snapshot.Score(state.playerId, target.id);
                engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.ShareSecret, target.id));
                if (engine.Snapshot.Score(state.playerId, target.id) > before) landed++; else backfired++;
            }
            Assert.That(landed, Is.GreaterThan(0));
            Assert.That(backfired, Is.GreaterThan(0));
            Assert.That(landed, Is.GreaterThan(backfired), "Sharing a secret works about two times in three.");
        }

        // ---------------------------------------------------------------- rumours and meetings

        [Test]
        public void AWhisperPoisonsARelationshipThePlayerIsNotPartOf()
        {
            var engine = Engine(out var state, 4u);
            var cast = Npcs(state);
            var before = engine.Snapshot;

            var command = Command(before, EpisodeCommandKind.SpreadRumor, cast[0].id);
            command.text = EpisodeEngine.WhisperCampaign;
            Assert.That(engine.Apply(command).accepted, Is.True);

            var after = engine.Snapshot;
            // Either it got back to them, or somebody else thinks less of them now.
            bool backfired = after.Score(after.playerId, cast[0].id) < before.Score(before.playerId, cast[0].id);
            bool poisoned = cast.Skip(1).Any(other =>
                after.Score(other.id, cast[0].id) < before.Score(other.id, cast[0].id));
            Assert.That(backfired || poisoned, Is.True, "A rumour has to do something.");
            Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
        }

        [Test]
        public void ACalloutReachesMorePeopleThanAWhisperAcrossASpreadOfSeeds()
        {
            int whispered = 0, shouted = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                whispered += Reached(seed, EpisodeEngine.WhisperCampaign);
                shouted += Reached(seed, EpisodeEngine.PublicCallout);
            }
            Assert.That(shouted, Is.GreaterThan(whispered),
                "Reach is what a public call-out buys, and what it is paid for with.");
        }

        [Test]
        public void AHouseMeetingTouchesEveryRelationshipAtOnce()
        {
            var engine = Engine(out var state, 6u);
            var before = engine.Snapshot;
            var house = before.Active.Where(c => !c.isPlayer).ToList();

            var command = Command(before, EpisodeCommandKind.HouseMeeting, null);
            command.text = EpisodeEngine.RallyTroops;
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);

            var after = engine.Snapshot;
            foreach (var guest in house)
                Assert.That(after.Score(after.playerId, guest.id),
                    Is.Not.EqualTo(before.Score(before.playerId, guest.id)), guest.name);
            Assert.That(EpisodeEngine.SocialActionsSpent(after),
                Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
            Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
        }

        /// <summary>Airing everything has no middle: agreement or the opposite, nothing between.</summary>
        [Test]
        public void AiringEverythingHasNoMiddleGround()
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                var engine = Engine(out var state, seed);
                var before = engine.Snapshot;
                var command = Command(before, EpisodeCommandKind.HouseMeeting, null);
                command.text = EpisodeEngine.AirDirtyLaundry;
                if (!engine.Apply(command).accepted) continue;

                var after = engine.Snapshot;
                foreach (var guest in before.Active.Where(c => !c.isPlayer))
                {
                    double moved = after.Score(after.playerId, guest.id) - before.Score(before.playerId, guest.id);
                    // Change() adds its own reciprocal movement, so the test is the sign rather than
                    // the exact figure: nobody comes out of it indifferent.
                    Assert.That(moved, Is.Not.Zero, "seed " + seed + ", " + guest.name);
                }
            }
        }

        // ---------------------------------------------------------------- buying a turn

        [Test]
        public void BuyingATurnRaisesTheBudgetAndCostsSomebody()
        {
            var engine = Engine(out var state);
            var before = engine.Snapshot;
            int budget = EpisodeEngine.SocialActionBudget(before);
            var house = before.Active.Where(c => !c.isPlayer).ToList();

            var command = Command(before, EpisodeCommandKind.BuyActionPoint, null);
            command.text = WebSocialVocabulary.SpreadAll;
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);

            var after = engine.Snapshot;
            Assert.That(after.boughtActionPoints, Is.EqualTo(1));
            Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(budget + 1));
            Assert.That(EpisodeEngine.EarnedSocialActionBudget(after), Is.EqualTo(budget),
                "What the week gives you for free has not changed.");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)),
                "Charging an action to earn an action would be a control that does nothing.");
            foreach (var guest in house)
                Assert.That(after.Score(after.playerId, guest.id),
                    Is.LessThan(before.Score(before.playerId, guest.id)), guest.name);
        }

        [Test]
        public void BurningOneBridgeCostsThatPersonAndNobodyElse()
        {
            var engine = Engine(out var state);
            var before = engine.Snapshot;
            var burned = Npcs(before)[0];

            var command = Command(before, EpisodeCommandKind.BuyActionPoint, burned.id);
            command.text = WebSocialVocabulary.BurnOne;
            Assert.That(engine.Apply(command).accepted, Is.True);

            var after = engine.Snapshot;
            Assert.That(after.Score(after.playerId, burned.id),
                Is.LessThan(before.Score(before.playerId, burned.id)));
            foreach (var other in Npcs(before).Skip(1))
                Assert.That(after.Score(after.playerId, other.id),
                    Is.EqualTo(before.Score(before.playerId, other.id)), other.name);
        }

        [Test]
        public void ThereIsALimitToHowMuchTimeTheHouseWillSell()
        {
            var engine = Engine(out var state);
            for (int i = 0; i < WebSocialVocabulary.PurchaseCeiling; i++)
            {
                var command = Command(engine.Snapshot, EpisodeCommandKind.BuyActionPoint, null);
                command.text = WebSocialVocabulary.SpreadAll;
                Assert.That(engine.Apply(command).accepted, Is.True, "purchase " + i);
            }
            var last = Command(engine.Snapshot, EpisodeCommandKind.BuyActionPoint, null);
            last.text = WebSocialVocabulary.SpreadAll;
            var refused = engine.Apply(last);
            Assert.That(refused.accepted, Is.False);
            Assert.That(engine.Snapshot.boughtActionPoints, Is.EqualTo(WebSocialVocabulary.PurchaseCeiling));
            Assert.That(EpisodeValidation.TryValidate(engine.Snapshot, out string error), Is.True, error);
        }

        [Test]
        public void PayingWithNothingIsRefusedAndCostsNothing()
        {
            var engine = Engine(out var state);
            var before = engine.Snapshot;
            var command = Command(before, EpisodeCommandKind.BuyActionPoint, null);
            command.text = "on credit";

            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False);
            Assert.That(engine.Snapshot.boughtActionPoints, Is.Zero);
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(before.randomState));
        }

        /// <summary>
        /// A bought turn is a turn: the budget the purchase opened can actually be spent.
        /// </summary>
        [Test]
        public void ATurnBoughtIsATurnThatCanBeSpent()
        {
            var engine = Engine(out var state);
            var target = Npcs(state)[0].id;

            // Spend the week's whole allowance.
            while (EpisodeEngine.SocialActionsSpent(engine.Snapshot) < EpisodeEngine.SocialActionBudget(engine.Snapshot))
                Assert.That(engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.SmallTalk, target)).accepted, Is.True);

            var spent = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.SmallTalk, target));
            Assert.That(spent.accepted, Is.False, "The window should be closed.");

            var buy = Command(engine.Snapshot, EpisodeCommandKind.BuyActionPoint, null);
            buy.text = WebSocialVocabulary.SpreadAll;
            Assert.That(engine.Apply(buy).accepted, Is.True);

            Assert.That(engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.SmallTalk, target)).accepted,
                Is.True, "What was bought has to be usable, or it was not sold.");
        }

        // ---------------------------------------------------------------- fixtures

        private static readonly EpisodeCommandKind[] Conversations =
        {
            EpisodeCommandKind.SmallTalk, EpisodeCommandKind.PersonalChat,
            EpisodeCommandKind.DiscussGame, EpisodeCommandKind.StrategicDiscussion,
            EpisodeCommandKind.RelationshipBuilding, EpisodeCommandKind.ShareSecret,
        };

        private static List<ContestantState> Npcs(EpisodeState state) =>
            state.Active.Where(c => !c.isPlayer).ToList();

        private static EpisodeEngine Engine(out EpisodeState state, uint seed = 7u)
        {
            state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, seed);
            state.phase = EpisodePhase.Social;
            return new EpisodeEngine(state);
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target) =>
            new EpisodeCommand
            {
                id = "cmd-" + s.revision, kind = kind, actorId = s.playerId, targetId = target,
                expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>How many times one command turns the season's generator.</summary>
        private static int Draws(EpisodeCommandKind kind)
        {
            var engine = Engine(out var state);
            uint before = engine.Snapshot.randomState;
            Assert.That(engine.Apply(Command(engine.Snapshot, kind, Npcs(state)[0].id)).accepted, Is.True);
            uint after = engine.Snapshot.randomState;

            var probe = new SeededRandom(before);
            for (int draws = 1; draws <= 12; draws++)
            {
                probe.NextDouble();
                if (probe.State == after) return draws;
            }
            Assert.Fail(kind + " drew more than twelve times, or not through Roll.");
            return -1;
        }

        /// <summary>How many houseguests' opinions of the subject moved.</summary>
        private static int Reached(uint seed, string approach)
        {
            var engine = Engine(out var state, seed);
            var cast = Npcs(state);
            var before = engine.Snapshot;
            var command = Command(before, EpisodeCommandKind.SpreadRumor, cast[0].id);
            command.text = approach;
            if (!engine.Apply(command).accepted) return 0;

            var after = engine.Snapshot;
            return cast.Skip(1).Count(other =>
                after.Score(other.id, cast[0].id) != before.Score(other.id, cast[0].id));
        }

        /// <summary>What a rule is worth on average across the whole range of a draw.</summary>
        private static double Average(System.Func<double, double> rule)
        {
            double total = 0;
            int samples = 0;
            for (double roll = 0; roll < 1; roll += 0.001) { total += rule(roll); samples++; }
            return total / samples;
        }

        private static void Span(System.Func<double, double> rule, double low, double high)
        {
            double seen = double.MaxValue, top = double.MinValue;
            for (double roll = 0; roll < 1; roll += 0.001)
            {
                double value = rule(roll);
                seen = System.Math.Min(seen, value);
                top = System.Math.Max(top, value);
            }
            Assert.That(seen, Is.EqualTo(low), "lowest");
            Assert.That(top, Is.EqualTo(high), "highest");
        }
    }
}
