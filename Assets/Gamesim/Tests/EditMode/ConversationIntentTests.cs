using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>E2's personal-lore trade-off. The other E2 verbs have their own later acceptance.</summary>
    public sealed class ConversationIntentTests
    {
        private static EpisodeState House(int size = 8, bool fresh = true)
        {
            var s = EconomyRulesTests.Fresh(size, fresh);
            EpisodeEngine.EnableStory(s);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target) =>
            new EpisodeCommand { id = "intent-" + s.revision + "-" + kind, actorId = s.playerId,
                expectedRevision = s.revision, expectedPhase = s.phase, kind = kind, targetId = target };

        private static EpisodeState Talk(EpisodeState s, EpisodeCommandKind kind, string target)
        {
            var result = new EpisodeEngine(s).Apply(Command(s, kind, target));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            return result.state;
        }

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s);
        private static IEnumerable<int> Sizes => Enumerable.Range(3, 14);

        [TestCase(0, 2)] [TestCase(0.3333333332, 2)] [TestCase(0.3333333334, 3)]
        [TestCase(0.6666666666, 3)] [TestCase(0.6666666667, 4)] [TestCase(0.99999, 4)]
        [TestCase(1, 4)] [TestCase(-1, 2)]
        public void PersonalWarmthHasThreeEqualBucketsAndFiniteBounds(double roll, double expected)
        {
            Assert.That(ConversationIntentRules.PersonalWarmth(House(), roll), Is.EqualTo(expected));
        }

        [Test]
        public void NonfiniteProbeInputsCannotCreateNonfiniteWarmth()
        {
            var s = House();
            Assert.That(ConversationIntentRules.PersonalWarmth(s, double.NaN), Is.EqualTo(2));
            Assert.That(ConversationIntentRules.PersonalWarmth(s, double.NegativeInfinity), Is.EqualTo(2));
            Assert.That(ConversationIntentRules.PersonalWarmth(s, double.PositiveInfinity), Is.EqualTo(4));
        }

        [TestCaseSource(nameof(Sizes))]
        public void EveryStoredCastUsesOneActionOneContactAndOnlyThatPersonsReachableLore(int size)
        {
            var s = House(size);
            foreach (var npc in s.Active.Where(c => !c.isPlayer))
            {
                string before = Json(s);
                var expected = s.Clone();
                expected.story.contacts.Add(new ContactState { npcId = npc.id,
                    rapport = Lore.RapportFor(EpisodeCommandKind.PersonalChat, npc) });
                var ids = new List<string>();
                for (int i = 0; i < 2; i++)
                {
                    var fact = Lore.NextReveal(expected, npc.id, EpisodeCommandKind.PersonalChat, false);
                    if (fact == null) break;
                    ids.Add(fact.id); Lore.Learn(expected, fact.id);
                }
                var after = Talk(s, EpisodeCommandKind.PersonalChat, npc.id);
                var learned = after.events.Skip(s.events.Count).Where(e => e.kind == "story-lore").ToList();
                Assert.That(learned.Count, Is.EqualTo(ids.Count));
                foreach (string id in ids) Assert.That(Lore.Knows(after, id), Is.True);
                Assert.That(learned.All(e => e.audienceIds.SequenceEqual(new[] { s.playerId, npc.id })), Is.True);
                Assert.That(after.story.contacts.Single(c => c.npcId == npc.id).weekCount, Is.EqualTo(1));
                Assert.That(after.story.contacts.Single(c => c.npcId == npc.id).rapport,
                    Is.EqualTo(Lore.RapportFor(EpisodeCommandKind.PersonalChat, npc)));
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1));
                Assert.That(after.windowActions[Windows.AfterEviction], Is.EqualTo(1));
                Assert.That(Json(s), Is.EqualTo(before), "The input snapshot is not mutated.");
            }
        }

        [TestCase(CastTemplates.Roster.Regular)] [TestCase(CastTemplates.Roster.AllStars)]
        public void BroaderPersonalFacetsRespectEachSavedRosterSheet(CastTemplates.Roster roster)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = roster }, 23);
            EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableStory(s);
            foreach (var npc in s.Active.Where(c => !c.isPlayer))
            {
                var after = Talk(s, EpisodeCommandKind.PersonalChat, npc.id);
                var facts = Lore.Learned(after, npc.id);
                Assert.That(facts.Count, Is.InRange(1, 2), npc.name);
                Assert.That(facts.All(f => Lore.FactsOf(s, npc.id).Any(original => original.id == f.id)), Is.True);
                Assert.That(facts.All(f => f.depth < 3 && f.facet != Lore.Facets.Secret && f.facet != Lore.Facets.Goal), Is.True);
                if (roster == CastTemplates.Roster.AllStars)
                    Assert.That(facts.All(f => f.sensitivity == StoryPeople.Sensitivity.Game), Is.True,
                        "Real-person sheets never gain invented private biography.");
            }
        }

        [Test]
        public void MoreFacetsAreReachableButSecretsGoalsAndDepthGatesDoNotDisappear()
        {
            var s = House(); string id = ContentCatalog.MayaId;
            s.story.contacts.Add(new ContactState { npcId = id, rapport = 100 });
            var oldFacets = Lore.FacetsFor(EpisodeCommandKind.PersonalChat);
            var expanded = Lore.FacetsFor(s, EpisodeCommandKind.PersonalChat);
            Assert.That(expanded.Length, Is.GreaterThan(oldFacets.Length));
            Assert.That(oldFacets.All(expanded.Contains), Is.True);
            Assert.That(expanded, Does.Not.Contain(Lore.Facets.Goal).And.Not.Contain(Lore.Facets.Secret));
            foreach (var fact in Lore.FactsOf(s, id).Where(f => oldFacets.Contains(f.facet))) Lore.Learn(s, fact.id);
            var next = Lore.NextReveal(s, id, EpisodeCommandKind.PersonalChat, false);
            Assert.That(next, Is.Not.Null, "Personal chat reaches beyond its old facets.");
            Assert.That(oldFacets, Does.Not.Contain(next.facet));
            Assert.That(next.depth, Is.LessThanOrEqualTo(2));
            while ((next = Lore.NextReveal(s, id, EpisodeCommandKind.PersonalChat, false)) != null)
            { Assert.That(next.depth, Is.LessThanOrEqualTo(2)); Lore.Learn(s, next.id); }
            Assert.That(Lore.Knows(s, Lore.Facet(s, id, Lore.Facets.Goal).id), Is.False);
            foreach (var fact in Lore.FactsOf(s, id).Where(f => f.facet == Lore.Facets.Secret))
                Assert.That(Lore.Knows(s, fact.id), Is.False);
        }

        [Test]
        public void RevealsStillNeedThePersonalBeatAtDepthThreeAndNeverReachDepthFour()
        {
            var s = House(); string id = ContentCatalog.MayaId;
            s.story.contacts.Add(new ContactState { npcId = id, rapport = 100 });
            foreach (var fact in Lore.FactsOf(s, id).Where(f => f.depth <= 2)) Lore.Learn(s, fact.id);
            Assert.That(Lore.NextReveal(s, id, EpisodeCommandKind.PersonalChat, false), Is.Null);
            var deep = Lore.NextReveal(s, id, EpisodeCommandKind.PersonalChat, true);
            Assert.That(deep, Is.Not.Null); Assert.That(deep.depth, Is.EqualTo(3));
            while ((deep = Lore.NextReveal(s, id, EpisodeCommandKind.PersonalChat, true)) != null)
            { Assert.That(deep.depth, Is.EqualTo(3)); Lore.Learn(s, deep.id); }
            Assert.That(Lore.FactsOf(s, id).Where(f => f.depth == 4).Any(f => Lore.Knows(s, f.id)), Is.False);
        }

        [Test]
        public void ReaderCallsAreDetachedDoNotDrawAndDoNotLearnAnything()
        {
            var s = House(); string before = Json(s);
            var facets = Lore.FacetsFor(s, EpisodeCommandKind.PersonalChat);
            facets[0] = Lore.Facets.Secret;
            Assert.That(Lore.FacetsFor(s, EpisodeCommandKind.PersonalChat)[0], Is.EqualTo(Lore.Facets.Origin));
            ConversationIntentRules.PersonalWarmth(s, .5);
            ConversationIntentRules.RevealLimit(s, EpisodeCommandKind.PersonalChat);
            Lore.NextReveal(s, ContentCatalog.MayaId, EpisodeCommandKind.PersonalChat, true);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("legacy")] [TestCase("week-delayed")] [TestCase("story-delayed")]
        [TestCase("no-lore")] [TestCase("no-story")]
        public void CompatibilityBoundariesPreserveTheSourceWarmthFacetsAndSingleReveal(string boundary)
        {
            var s = House();
            if (boundary == "legacy") s.economyRulesVersion = 0;
            if (boundary == "week-delayed") s.weekRulesStartWeek = 2;
            if (boundary == "story-delayed") s.story.rulesStartWeek = 2;
            if (boundary == "no-lore") s.story.rulesVersion = StoryRules.Lore - 1;
            if (boundary == "no-story") s.story.rulesStartWeek = 0;
            Assert.That(ConversationIntentRules.PersonalLoreOn(s), Is.False);
            for (int i = 0; i <= 100; i++)
                Assert.That(ConversationIntentRules.PersonalWarmth(s, i / 100d),
                    Is.EqualTo(WebSocialVocabulary.PersonalChat(i / 100d)));
            Assert.That(Lore.FacetsFor(s, EpisodeCommandKind.PersonalChat), Is.EqualTo(Lore.FacetsFor(EpisodeCommandKind.PersonalChat)));
            Assert.That(ConversationIntentRules.RevealLimit(s, EpisodeCommandKind.PersonalChat), Is.EqualTo(1));
        }

        [Test]
        public void OtherTopicsKeepTheirOriginalFacetsRapportAndSingleReveal()
        {
            var s = House();
            foreach (EpisodeCommandKind kind in Enum.GetValues(typeof(EpisodeCommandKind)))
            {
                if (kind == EpisodeCommandKind.PersonalChat) continue;
                Assert.That(Lore.FacetsFor(s, kind), Is.EqualTo(Lore.FacetsFor(kind)), kind.ToString());
                Assert.That(ConversationIntentRules.RevealLimit(s, kind), Is.EqualTo(1), kind.ToString());
            }
        }

        [Test]
        public void ACommittedPersonalChatReplaysAndDoesNotConsumeExtraSeasonRandomness()
        {
            var s = House(); var command = Command(s, EpisodeCommandKind.PersonalChat, ContentCatalog.MayaId);
            string serialized = Json(s);
            var replayInput = JsonConvert.DeserializeObject<EpisodeState>(serialized,
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            var engine = new EpisodeEngine(s); var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            var replay = new EpisodeEngine(replayInput).Apply(command);
            Assert.That(replay.accepted, Is.True, replay.reason);
            Assert.That(Json(replay.state), Is.EqualTo(Json(result.state)));
            string committed = Json(engine.Snapshot);
            Assert.That(engine.Apply(command).accepted, Is.False);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            var legacy = s.Clone(); legacy.economyRulesVersion = 0;
            var historical = Talk(legacy, EpisodeCommandKind.PersonalChat, ContentCatalog.MayaId);
            Assert.That(result.state.randomState, Is.EqualTo(historical.randomState), "The lore reveal never spends a season draw.");
        }

        [Test]
        public void ExhaustionAndInvalidTargetsLearnNothingAndSpendNothing()
        {
            foreach (string target in new[] { (string)null, "missing", "player" })
            {
                var s = House(); var engine = new EpisodeEngine(s); string before = Json(engine.Snapshot);
                var result = engine.Apply(Command(s, EpisodeCommandKind.PersonalChat, target));
                Assert.That(result.accepted, Is.False); Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            }
            var exhausted = House(); exhausted.windowActions[Windows.AfterEviction] = 2; exhausted.socialActions = 2;
            var full = new EpisodeEngine(exhausted); string original = Json(full.Snapshot);
            Assert.That(full.Apply(Command(exhausted, EpisodeCommandKind.PersonalChat, ContentCatalog.MayaId)).accepted, Is.False);
            Assert.That(Json(full.Snapshot), Is.EqualTo(original));
        }

        [Test]
        public void TheKnowledgeCapStillStopsLearningWithoutInventingAReplacementFact()
        {
            var s = House(16);
            // Reader-level capacity fixture, not a fabricated save: production Learn owns the cap.
            for (int i = 0; i < 200; i++) s.story.knownFacts.Add("capacity-probe-" + i);
            var fact = Lore.FactsOf(s, ContentCatalog.MayaId).First();
            Assert.That(Lore.Learn(s, fact.id), Is.False);
            Assert.That(s.story.knownFacts.Count, Is.EqualTo(200));
            Assert.That(Lore.Knows(s, fact.id), Is.False);
        }

        [Test]
        public void ExpectedWarmthAndActualLoreEstablishThePersonalVersusBondTradeoff()
        {
            var s = House(); double personal = 0, small = 0, bond = 0;
            const int samples = 2400;
            for (int i = 0; i < samples; i++)
            {
                double draw = (i + .5) / samples;
                personal += ConversationIntentRules.PersonalWarmth(s, draw);
                small += WebSocialVocabulary.SmallTalk(draw);
                bond += WebSocialVocabulary.RelationshipBuilding(draw);
            }
            Assert.That(personal / samples, Is.EqualTo(3).Within(.0001));
            Assert.That(small / samples, Is.EqualTo(4).Within(.0001));
            Assert.That(bond / samples, Is.EqualTo(8.5).Within(.0001));
            var personalState = Talk(s, EpisodeCommandKind.PersonalChat, ContentCatalog.MayaId);
            var bondState = Talk(s, EpisodeCommandKind.RelationshipBuilding, ContentCatalog.MayaId);
            Assert.That(Lore.Learned(personalState, ContentCatalog.MayaId).Count, Is.EqualTo(2));
            Assert.That(Lore.Learned(bondState, ContentCatalog.MayaId).Count, Is.EqualTo(1));
            Assert.That(bondState.story.contacts.Single().rapport, Is.GreaterThan(personalState.story.contacts.Single().rapport));
            TestContext.WriteLine("Base expected warmth: personal3, small-talk4, bond8.5. First personal lore2 versus bond1; bond builds rapport faster. This does not accept all E2 verbs or final balance.");
        }
    }
}
