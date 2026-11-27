using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What a free talk teaches (talk rules 1, slice S1): the layers at their numbers, nothing from a grating landing or a reply that
    /// holds back, the weekly "where they stand" and its wording, and the All-Stars' own pool (ruling R7). On the tests' own table.
    /// Simulation only, so the Unity-free subset runs it.
    /// </summary>
    public sealed class FreeTalkLayerTests
    {
        private static bool HasFacts(EpisodeState s, ContestantState npc, int depth) =>
            Lore.FactsOf(s, npc.id).Any(f => f.depth == depth && f.facet != Lore.Facets.Secret);

        private static string ToneLandingAtLeast(EpisodeState s, ContestantState npc, int landing) =>
            TalkRules.Tones.Where(t => t != TalkRules.Flirt).FirstOrDefault(t => FreeTalk.Landing(s, npc, t) >= landing);

        // ------------------------------------------------------------------ the layers

        [Test]
        public void TheFirstTalkTeachesWhoTheyAreOnTheirPage()
        {
            using (TalkBank.Use(FreeTalkFixture.Table("first")))
            {
                var s = FreeTalkFixture.Fresh();
                var npc = FreeTalkFixture.Npcs(s).First(n => HasFacts(s, n, 1) && ToneLandingAtLeast(s, n, 0) != null);
                string tone = ToneLandingAtLeast(s, npc, 0);
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc.id);
                var result = FreeTalk.Result(engine.Snapshot, npc.id, offer.Talk.Id, tone);
                Assert.That(result.FactLayer, Is.EqualTo(1));
                Assert.That(FreeTalkFixture.Talk(engine, npc.id, tone).accepted, Is.True);
                var after = engine.Snapshot;
                Assert.That(Lore.Knows(after, result.FactId), Is.True);
                Assert.That(after.events.Any(e => e.kind == "story-lore" && e.text.EndsWith(result.Fact, StringComparison.Ordinal)), Is.True);
                var page = TalkNotes.WhatYouKnow(after, npc.id);
                Assert.That(page.Count, Is.EqualTo(1));
                Assert.That(page[0].Heading, Is.EqualTo(TalkNotes.WhoTheyAre));
                Assert.That(page[0].Facts, Is.EqualTo(new[] { result.Fact }));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void AGratingLandingOrAReplyThatHoldsBackTeachesNothing(bool holdsBack)
        {
            var table = TalkPools.All.Select(p => FreeTalkFixture.Row(p, "first", TalkKind.Layer, 1, new TalkGate { First = true }, holdsBack: holdsBack)).ToList();
            using (TalkBank.Use(table))
            {
                var s = FreeTalkFixture.Fresh();
                string npc, tone;
                if (holdsBack)
                {
                    var who = FreeTalkFixture.Npcs(s).First(n => HasFacts(s, n, 1) && ToneLandingAtLeast(s, n, 1) != null);
                    npc = who.id; tone = ToneLandingAtLeast(s, who, 1);
                }
                else Assert.That(FreeTalkFixture.TryFind(s, -1, n => HasFacts(s, n, 1), out npc, out tone), Is.True);
                var engine = new EpisodeEngine(s);
                int known = engine.Snapshot.story.knownFacts.Count;
                Assert.That(FreeTalkFixture.Talk(engine, npc, tone).accepted, Is.True);
                Assert.That(engine.Snapshot.story.knownFacts.Count, Is.EqualTo(known));
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).talks, Is.EqualTo(1), "It still counts as the window's talk.");
            }
        }

        [Test]
        public void LayersOpenAtTheirNumbers()
        {
            var s = FreeTalkFixture.Fresh();
            var npc = FreeTalkFixture.Npcs(s).First(n => !FreeTalk.IsAllStar(n));
            var contact = FreeTalkFixture.ContactOf(s, npc.id);
            FreeTalkFixture.SetScore(s, npc.id, s.playerId, 14);
            Assert.That(FreeTalk.OpenLayer(s, npc.id), Is.EqualTo(1));
            contact.rapport = 2;
            Assert.That(FreeTalk.OpenLayer(s, npc.id), Is.EqualTo(1));
            contact.rapport = 3;
            Assert.That(FreeTalk.OpenLayer(s, npc.id), Is.EqualTo(2));
            contact.rapport = 6;
            Assert.That(FreeTalk.OpenLayer(s, npc.id), Is.EqualTo(2), "What drives them needs their view of you at 15 too.");
            FreeTalkFixture.SetScore(s, npc.id, s.playerId, 15);
            Assert.That(FreeTalk.OpenLayer(s, npc.id), Is.EqualTo(3));
            Assert.That(FreeTalk.OpenLayer(FreeTalkFixture.Fresh(TalkRules.Off), npc.id), Is.Zero);
        }

        [TestCase(2)] [TestCase(3)]
        public void ALayerFrameLeadsToAFactOfItsLayerOnceItIsOpen(int layer)
        {
            using (TalkBank.Use(FreeTalkFixture.Table("l" + layer)))
            {
                var s = FreeTalkFixture.Fresh();
                var npc = FreeTalkFixture.Npcs(s).First(n => !FreeTalk.IsAllStar(n) && HasFacts(s, n, layer) && ToneLandingAtLeast(s, n, 0) != null);
                foreach (var fact in Lore.FactsOf(s, npc.id).Where(f => f.depth < layer)) Lore.Learn(s, fact.id);
                var contact = FreeTalkFixture.ContactOf(s, npc.id);
                contact.talks = 1;
                contact.rapport = layer == 2 ? 2 : 5;
                FreeTalkFixture.SetScore(s, npc.id, s.playerId, 15);
                Assert.That(FreeTalk.Offer(s, npc.id).Refusal, Is.EqualTo(TalkRefusal.NothingToSay), "Not yet open.");
                contact.rapport = layer == 2 ? 3 : 6;
                var engine = new EpisodeEngine(s);
                string tone = ToneLandingAtLeast(s, npc, 0);
                var offer = FreeTalk.Offer(engine.Snapshot, npc.id);
                Assert.That(offer.Available, Is.True, offer.Reason);
                var result = FreeTalk.Result(engine.Snapshot, npc.id, offer.Talk.Id, tone);
                Assert.That(result.FactLayer, Is.EqualTo(layer));
                Assert.That(FreeTalkFixture.Talk(engine, npc.id, tone).accepted, Is.True);
                Assert.That(Lore.Knows(engine.Snapshot, result.FactId), Is.True);
                Assert.That(TalkNotes.WhatYouKnow(engine.Snapshot, npc.id).Last().Heading, Is.EqualTo(TalkNotes.HeadingOf(layer)));
            }
        }

        // ------------------------------------------------------------------ where they stand (ruling R6)

        [Test]
        public void TheStandingPartsNameTheTruthOrItsDecoys()
        {
            var s = FreeTalkFixture.Fresh();
            var npcs = FreeTalkFixture.Npcs(s);
            var npc = npcs[0];
            foreach (var other in npcs.Skip(1)) { FreeTalkFixture.SetScore(s, npc.id, other.id, 0); FreeTalkFixture.SetScore(s, other.id, npc.id, 0); }
            ContestantState a = npcs[1], b = npcs[2], c = npcs[3];
            FreeTalkFixture.SetScore(s, npc.id, a.id, 60);
            FreeTalkFixture.SetScore(s, npc.id, b.id, 40);
            FreeTalkFixture.SetScore(s, npc.id, c.id, -30);

            var honest = TalkStanding.Parts(s, npc.id, Candour.Honest);
            Assert.That(honest["close"], Is.EqualTo(a.name));
            Assert.That(honest["worry"], Is.EqualTo(c.name));
            Assert.That(honest["safe"], Is.EqualTo(TalkStanding.SafeNobody));
            var dishonest = TalkStanding.Parts(s, npc.id, Candour.Dishonest);
            Assert.That(dishonest["close"], Is.EqualTo(b.name));
            Assert.That(dishonest["worry"], Is.Not.EqualTo(c.name));
            Assert.That(dishonest["worry"], Is.Not.EqualTo(b.name));

            var talk = FreeTalkFixture.Row(TalkPools.Steady, "st", TalkKind.Standing, 4, new TalkGate { Layer = 4 });
            string neutral = TalkStanding.Line(s, npc.id, talk, Candour.Neutral);
            Assert.That(new[] { a.name, b.name, c.name }.Any(n => neutral.Contains(n)), Is.False, neutral);
            Assert.That(TalkStanding.Line(s, npc.id, talk, Candour.Honest), Does.Contain(a.name).And.Contain(c.name));

            FreeTalkFixture.SetScore(s, a.id, npc.id, 30);
            FreeTalkFixture.SetScore(s, b.id, npc.id, 30);
            Assert.That(TalkStanding.Parts(s, npc.id, Candour.Honest)["safe"], Is.EqualTo(TalkStanding.SafeFew));
            s.hohId = npc.id;
            Assert.That(TalkStanding.Parts(s, npc.id, Candour.Honest)["safe"], Is.EqualTo(TalkStanding.SafeHoh));
        }

        [Test]
        public void TheStanceIsCandoursOwnCoinAndALoyalHousemateNeverLies()
        {
            var s = FreeTalkFixture.Fresh();
            var loyal = FreeTalkFixture.Npcs(s)[0];
            if (!loyal.traits.Contains("Loyal")) loyal.traits.Add("Loyal");
            foreach (var npc in FreeTalkFixture.Npcs(s))
            {
                string stance = TalkStanding.Stance(s, npc.id);
                Assert.That(stance, Is.EqualTo(Candour.Choose(s, npc.id, Candour.Where, null)));
                Assert.That(TalkStanding.Stance(s, npc.id), Is.EqualTo(stance), "The same coin all week.");
                if (npc.traits.Contains("Loyal")) Assert.That(stance, Is.Not.EqualTo(Candour.Dishonest), npc.id);
            }
            Assert.That(Candour.WeightsFor(s, loyal.id, Candour.Where, null).dishonest, Is.Zero);
        }

        [Test]
        public void WhereTheyStandIsSnapshotOnceAWeek()
        {
            using (TalkBank.Use(FreeTalkFixture.Table("st", "r1")))
            {
                var s = FreeTalkFixture.WeekTwo().Snapshot;
                var npc = FreeTalkFixture.Npcs(s).First(n => ToneLandingAtLeast(s, n, 0) != null);
                var contact = FreeTalkFixture.ContactOf(s, npc.id);
                contact.talks = 1;
                contact.rapport = 8;
                FreeTalkFixture.SetScore(s, npc.id, s.playerId, 40);
                var engine = new EpisodeEngine(s);
                Assert.That(FreeTalk.StandingOpen(engine.Snapshot, npc.id), Is.True);
                var offer = FreeTalk.Offer(engine.Snapshot, npc.id);
                Assert.That(offer.Talk.Kind, Is.EqualTo(TalkKind.Standing));
                string tone = ToneLandingAtLeast(s, npc, 0);
                var result = FreeTalk.Result(engine.Snapshot, npc.id, offer.Talk.Id, tone);
                Assert.That(result.Stance, Is.EqualTo(TalkStanding.Stance(engine.Snapshot, npc.id)));
                Assert.That(FreeTalkFixture.Talk(engine, npc.id, tone).accepted, Is.True);
                var after = engine.Snapshot;
                var snapshot = TalkNotes.FromThem(after, npc.id);
                Assert.That(snapshot.Week, Is.EqualTo(after.week));
                Assert.That(snapshot.Current, Is.True);
                Assert.That(snapshot.Line, Is.EqualTo(result.StandingSaid), "The page shows what the talk screen said.");
                Assert.That(FreeTalk.Contact(after, npc.id).standing, Is.EqualTo(result.StandingLine), "The season keeps the line as it saves it.");

                int week = after.week, key = FreeTalk.WindowKey(after);
                FreeTalkFixture.WalkUntil(engine, x => FreeTalk.WindowKey(x) != 0 && FreeTalk.WindowKey(x) != key || x.week != week);
                var later = engine.Snapshot;
                Assert.That(later.week, Is.EqualTo(week), "The control: the next window of the same week.");
                if (later.Find(npc.id).status == ContestantStatus.Active)
                    Assert.That(FreeTalk.Offer(later, npc.id).Talk?.Kind, Is.Not.EqualTo(TalkKind.Standing), "One snapshot a week.");
            }
        }

        // ------------------------------------------------------------------ real people (ruling R7)

        [Test]
        public void AnAllStarNeverGetsAClassPoolTalkOrLayerThree()
        {
            var classAndStarThree = FreeTalkFixture.Table().Where(t => t.Pool != TalkPools.Star || t.Layer == 3).ToList();
            using (TalkBank.Use(classAndStarThree))
            {
                var s = FreeTalkFixture.Fresh(roster: CastTemplates.Roster.AllStars);
                var stars = FreeTalkFixture.Npcs(s);
                Assert.That(stars.All(FreeTalk.IsAllStar), Is.True, "The control: an All-Stars house.");
                stars[0].name = "Renamed Card";
                foreach (var npc in stars)
                {
                    var contact = FreeTalkFixture.ContactOf(s, npc.id);
                    contact.talks = 1;
                    contact.rapport = 10;
                    FreeTalkFixture.SetScore(s, npc.id, s.playerId, 50);
                    Assert.That(FreeTalk.PoolOf(npc), Is.EqualTo(TalkPools.Star));
                    Assert.That(FreeTalk.OpenLayer(s, npc.id), Is.EqualTo(2), npc.id);
                    Assert.That(FreeTalk.Offer(s, npc.id).Refusal, Is.EqualTo(TalkRefusal.NothingToSay), npc.id);
                }
            }
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var s = FreeTalkFixture.Fresh(roster: CastTemplates.Roster.AllStars);
                var engine = new EpisodeEngine(s);
                foreach (var npc in FreeTalkFixture.Npcs(s))
                {
                    var offer = FreeTalk.Offer(engine.Snapshot, npc.id);
                    Assert.That(offer.Talk.Pool, Is.EqualTo(TalkPools.Star));
                    Assert.That(FreeTalkFixture.Talk(engine, npc.id, TalkRules.Warm).accepted, Is.True);
                }
                var after = engine.Snapshot;
                foreach (var npc in FreeTalkFixture.Npcs(after))
                    Assert.That(Lore.Learned(after, npc.id).All(f => f.depth <= 2), Is.True, npc.id);
            }
        }
    }
}
