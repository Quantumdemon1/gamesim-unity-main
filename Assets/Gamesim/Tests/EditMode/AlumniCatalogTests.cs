using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class AlumniCatalogTests
    {
        [Test]
        public void CatalogExactlyCoversTheTwelveAllStarTemplateIdentities()
        {
            Assert.That(AlumniCatalog.All.Count, Is.EqualTo(12));
            var ids = AlumniCatalog.All.Select(p => p.TemplateId).ToArray();
            Assert.That(ids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(ids.Length));
            CollectionAssert.AreEquivalent(CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id), ids);
            foreach (var profile in AlumniCatalog.All)
            {
                Assert.That(AlumniCatalog.TryGet(profile.TemplateId, out var found), Is.True);
                Assert.That(found, Is.SameAs(profile));
                Assert.That(profile.DisplayName, Is.EqualTo(CastTemplates.Find(profile.TemplateId).Name));
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("dan-gheesling ")]
        [TestCase("DAN-GHEESLING")]
        [TestCase("Dan Gheesling")]
        [TestCase("will-kirby")]
        [TestCase("alex-chen")]
        public void LookupCannotAssignAlumniToAnApproximateOrRegularIdentity(string requested)
        {
            Assert.That(AlumniCatalog.TryGet(requested, out var found), Is.False);
            Assert.That(found, Is.Null);
        }

        [Test]
        public void WillHasATitleAndARealGivenName()
        {
            Assert.That(AlumniCatalog.TryGet("dr-will-kirby", out var will), Is.True);
            Assert.That(will.NameTitle, Is.EqualTo("Dr."));
            Assert.That(will.GivenName, Is.EqualTo("Will"));
            Assert.That(will.FamilyName, Is.EqualTo("Kirby"));
            Assert.That(will.ShortName, Is.EqualTo("Will"));
            Assert.That(will.DisplayName, Is.EqualTo("Dr. Will Kirby"));
            Assert.That(AlumniCatalog.All.Where(p => p != will).All(p => p.NameTitle == string.Empty), Is.True);
            Assert.That(AlumniCatalog.All.Any(p => p.GivenName == "Dr."), Is.False);
        }

        [Test]
        public void EveryCareerAndPairFactHasStableTraceableEvidenceInsideTheCanon()
        {
            Assert.That(AlumniCatalog.Version, Is.EqualTo("bb26-2024-v1"));
            Assert.That(AlumniCatalog.CanonYear, Is.EqualTo(2024));
            Assert.That(AlumniCatalog.CanonSeason, Is.EqualTo(26));
            var sourceIds = AlumniCatalog.Sources.Select(s => s.Id).ToArray();
            Assert.That(sourceIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(sourceIds.Length));
            foreach (var source in AlumniCatalog.Sources)
            {
                Assert.That(source.Id, Is.Not.Empty);
                Assert.That(source.Title, Is.Not.Empty);
                Assert.That(source.PublishedYear, Is.InRange(2001, AlumniCatalog.CanonYear), source.Id);
                Assert.That(Uri.TryCreate(source.Url, UriKind.Absolute, out var uri), Is.True, source.Id);
                Assert.That(uri.Scheme, Is.EqualTo(Uri.UriSchemeHttps), source.Id);
            }
            var facts = AllFacts().ToArray();
            Assert.That(facts.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(facts.Length));
            foreach (var fact in facts)
            {
                Assert.That(fact.Id, Does.Match("^[a-z0-9]+(?:-[a-z0-9]+)*$"));
                Assert.That(fact.Show, Is.Not.Empty);
                Assert.That(fact.Summary, Is.Not.Empty);
                Assert.That(fact.Season, Is.GreaterThan(0));
                Assert.That(fact.Year, Is.InRange(2001, AlumniCatalog.CanonYear), fact.Id);
                if (fact.Show == "Big Brother") Assert.That(fact.Season, Is.LessThanOrEqualTo(AlumniCatalog.CanonSeason), fact.Id);
                Assert.That(fact.SourceIds, Is.Not.Empty, fact.Id);
                Assert.That(fact.SourceIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(fact.SourceIds.Count), fact.Id);
                foreach (var id in fact.SourceIds) Assert.That(sourceIds, Contains.Item(id), fact.Id);
            }
        }

        [Test]
        public void PairHistoriesAreUniqueAndDirectionsNameBothParticipants()
        {
            Assert.That(AlumniCatalog.PairHistories.Count, Is.EqualTo(13));
            var pairs = AlumniCatalog.PairHistories.Select(h =>
                string.Join("|", new[] { h.LeftTemplateId, h.RightTemplateId }.OrderBy(id => id, StringComparer.Ordinal))).ToArray();
            Assert.That(pairs.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(pairs.Length));
            foreach (var history in AlumniCatalog.PairHistories)
            {
                Assert.That(history.FactId, Is.EqualTo(history.Fact.Id));
                Assert.That(history.LeftTemplateId, Is.Not.EqualTo(history.RightTemplateId));
                Assert.That(AlumniCatalog.TryGet(history.LeftTemplateId, out _), Is.True);
                Assert.That(AlumniCatalog.TryGet(history.RightTemplateId, out _), Is.True);
                Assert.That(history.RightToLeft.FromTemplateId, Is.EqualTo(history.RightTemplateId));
                Assert.That(history.RightToLeft.ToTemplateId, Is.EqualTo(history.LeftTemplateId));
                foreach (var prior in new[] { history.LeftToRight, history.RightToLeft })
                {
                    Assert.That(prior.Relationship, Is.InRange(-100, 100), history.FactId);
                    Assert.That(prior.PriorTrust, Is.InRange(0, 100), history.FactId);
                    Assert.That(prior.PriorTrust, Is.EqualTo(50 + prior.PriorTrustOffset), history.FactId);
                }
            }
        }

        [TestCase("hitmen-bb16", 60, 25, 60, 25)]
        [TestCase("janelle-rachel-snake", 45, 15, 45, 15)]
        [TestCase("cody-danielle-reindeer", 25, 10, 25, 10)]
        [TestCase("danielle-xavier-reindeer", 25, 10, 25, 10)]
        [TestCase("cody-tyler-bb22", 25, 0, 25, 0)]
        [TestCase("will-janelle-bb7", 20, -10, 20, -10)]
        [TestCase("will-danielle-bb7", 20, -10, 20, -10)]
        [TestCase("danielle-janelle-bb7", 10, 0, 10, 0)]
        [TestCase("janelle-dan-traitors", 25, -10, 25, 0)]
        [TestCase("janelle-cody-bb22", -15, -10, 0, 0)]
        [TestCase("janelle-tyler-bb22", -15, -10, 0, 0)]
        [TestCase("rachel-cody-traitors", 10, -10, 10, 0)]
        [TestCase("cody-xavier-reindeer", 0, -10, 0, 0)]
        public void AuthoredDirectedPriorsKeepRespectSeparateFromTrust(string factId,
            int leftRelationship, int leftTrust, int rightRelationship, int rightTrust)
        {
            var history = AlumniCatalog.PairHistories.Single(h => h.FactId == factId);
            Assert.That(history.LeftToRight.Relationship, Is.EqualTo(leftRelationship));
            Assert.That(history.LeftToRight.PriorTrustOffset, Is.EqualTo(leftTrust));
            Assert.That(history.RightToLeft.Relationship, Is.EqualTo(rightRelationship));
            Assert.That(history.RightToLeft.PriorTrustOffset, Is.EqualTo(rightTrust));
        }

        [Test]
        public void DanAndWillAreNotTurnedIntoAnInventedCoPlayerHistory()
        {
            Assert.That(AlumniCatalog.TryGet("dan-gheesling", out var dan), Is.True);
            Assert.That(AlumniCatalog.TryGet("dr-will-kirby", out var will), Is.True);
            Assert.That(dan.PublicReputation, Is.Not.Empty);
            Assert.That(will.PublicReputation, Is.Not.Empty);
            Assert.That(AlumniCatalog.PairHistories.Any(h =>
                (h.LeftTemplateId == dan.TemplateId && h.RightTemplateId == will.TemplateId)
                || (h.LeftTemplateId == will.TemplateId && h.RightTemplateId == dan.TemplateId)), Is.False);
        }

        [Test]
        public void StyleAndVoiceContentAreCompleteAndDialogueIsLabeledAsFiction()
        {
            Assert.That(AlumniCatalog.DialogueProvenance, Does.Contain("Original fictional"));
            Assert.That(AlumniCatalog.DialogueProvenance, Does.Contain("not a historical quotation"));
            foreach (var profile in AlumniCatalog.All)
            {
                Assert.That(profile.CareerFacts, Is.Not.Empty, profile.TemplateId);
                Assert.That(profile.PublicReputation, Is.Not.Empty, profile.TemplateId);
                Assert.That(profile.VoiceGuide, Is.Not.Empty, profile.TemplateId);
                Assert.That(profile.OriginalDialogueExample, Is.Not.Empty, profile.TemplateId);
                Assert.That(profile.StylePriorities.Count, Is.EqualTo(4), profile.TemplateId);
                Assert.That(profile.StylePriorities.All(s => !string.IsNullOrWhiteSpace(s)), Is.True, profile.TemplateId);
                Assert.That(profile.StylePriorities.Distinct().Count(), Is.EqualTo(profile.StylePriorities.Count), profile.TemplateId);
            }
            Assert.That(AlumniCatalog.All.Select(p => p.OriginalDialogueExample).Distinct().Count(), Is.EqualTo(12));
        }

        [Test]
        public void CollectionsCannotBeMutatedThroughTheirRuntimeListInterface()
        {
            RejectListMutation(AlumniCatalog.All);
            RejectListMutation(AlumniCatalog.PairHistories);
            RejectListMutation(AlumniCatalog.Sources);
            foreach (var profile in AlumniCatalog.All)
            {
                RejectListMutation(profile.CareerFacts);
                RejectListMutation(profile.StylePriorities);
            }
            foreach (var fact in AllFacts()) RejectListMutation(fact.SourceIds);
            Assert.That(AlumniCatalog.All.Count, Is.EqualTo(12));
            Assert.That(AlumniCatalog.PairHistories.Count, Is.EqualTo(13));
        }

        [Test]
        public void PublishedRecordsHaveNoPublicMutableMembers()
        {
            foreach (var type in new[] { typeof(AlumniCatalog.Profile), typeof(AlumniCatalog.Source),
                typeof(AlumniCatalog.Fact), typeof(AlumniCatalog.PairHistory), typeof(AlumniCatalog.DirectedPrior) })
            {
                Assert.That(type.IsSealed, Is.True, type.Name);
                Assert.That(type.GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty, type.Name);
                Assert.That(type.GetProperties().Any(p => p.GetSetMethod() != null), Is.False, type.Name);
            }
        }

        private static IEnumerable<AlumniCatalog.Fact> AllFacts()
            => AlumniCatalog.All.SelectMany(p => p.CareerFacts).Concat(AlumniCatalog.PairHistories.Select(h => h.Fact));

        private static void RejectListMutation<T>(IReadOnlyList<T> readOnly)
        {
            var list = readOnly as IList<T>;
            Assert.That(list, Is.Not.Null);
            Assert.That(list.IsReadOnly, Is.True);
            Assert.That(list.Count, Is.GreaterThan(0));
            var first = list[0];
            Assert.Throws<NotSupportedException>(() => list[0] = first);
            Assert.Throws<NotSupportedException>(() => list.Add(first));
            Assert.Throws<NotSupportedException>(() => list.RemoveAt(0));
            Assert.That(list[0], Is.EqualTo(first));
        }
    }
}
