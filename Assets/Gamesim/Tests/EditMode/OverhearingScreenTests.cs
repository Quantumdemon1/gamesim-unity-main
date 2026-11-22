using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Overheard, on screen (plan 32 §I4 and §I5, slice S2b; story rules 12): the pure reads the house's watch, the listen
    /// tiles, the Nearby card and the relationship web share (<see cref="Overhearing"/>). A Notice the engine accepts is one
    /// hunch in its tone with its side-column line; confidence grows with the sightings inside the shelf life and fades
    /// after it; hunches come only from the player's own sightings, never from the pacts, and never name the evicted; the
    /// catch risk is shown before the press; the walk-over finds the nearest conversation still open; and below 12 every
    /// read is off. Pure simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class OverhearingScreenTests
    {
        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static string First(EpisodeState s, string id) => CharacterNames.FirstName(s.Find(id).name);

        /// <summary>A season at 12 with one conversation on <paramref name="topic"/>, noticed through the engine's own door.</summary>
        private static EpisodeState Noticed(string topic)
        {
            var s = OverhearTests.Talking(StoryRules.Manners, topic);
            var result = new EpisodeEngine(s).Apply(OverhearTests.Overhear(s, s.npcSocial.pending[0], false));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static void Seen(EpisodeState s, string a, string b, int tone, int week) =>
            s.ledger.standings.Add(new StandingRow { week = week, fromId = a, toId = b, source = ClaimSource.Seen, score = tone });

        private static List<string> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        [Test]
        public void BelowTwelveEveryReadIsOffAndNoHunchIsRead()
        {
            var s = OverhearTests.Talking(StoryRules.Roles, "strategy");
            var npcs = Npcs(s);
            // A row no season below 12 may hold, written anyway: the web still reads nothing.
            Seen(s, npcs[0], npcs[1], HunchTone.Warm, s.week);
            Assert.That(Overhearing.On(s), Is.False);
            Assert.That(Overhearing.Hunches(s), Is.Empty);
            Assert.That(Overhearing.ListenLeft(s), Is.False);
            Assert.That(Overhearing.Refusal(s, s.npcSocial.pending[0].sequence, true), Is.EqualTo(EpisodeEngine.WaveDKindRefusal));
            Assert.That(Overhearing.Listenable(s, c => 1f), Is.Empty);
        }

        [TestCase("casual", HunchTone.Warm, "seem close", "maybe")]
        [TestCase("strategy", HunchTone.Scheming, "seem to be scheming together", "? maybe")]
        [TestCase("tension", HunchTone.Tense, "seem to be at odds", "maybe")]
        public void ANoticeIsOneHunchInItsToneWithItsLine(string topic, int tone, string words, string label)
        {
            var s = Noticed(topic);
            var talk = s.npcSocial.pending[0];
            var hunches = Overhearing.Hunches(s);
            Assert.That(hunches.Count, Is.EqualTo(1));
            var hunch = hunches[0];
            Assert.That(new[] { hunch.firstId, hunch.secondId }, Is.EquivalentTo(new[] { talk.firstId, talk.secondId }));
            Assert.That(hunch.tone, Is.EqualTo(tone));
            Assert.That(hunch.rows, Is.EqualTo(1));
            Assert.That(hunch.current, Is.True);
            Assert.That(hunch.weeks, Is.EqualTo(new[] { s.week }));
            Assert.That(Overhearing.Line(s, hunch), Is.EqualTo("Your hunch: " + First(s, hunch.firstId) + " and " + First(s, hunch.secondId)
                + " " + words + " (noticed in week " + s.week + ")."));
            Assert.That(Overhearing.Label(hunch), Is.EqualTo(label));
        }

        [Test]
        public void ConfidenceGrowsWithSightingsInTheShelfLifeAndFadesAfterIt()
        {
            var s = OverhearTests.Talking(StoryRules.Manners, "casual");
            var npcs = Npcs(s);
            s.week = 6;
            Seen(s, npcs[0], npcs[1], HunchTone.Warm, 4);
            Seen(s, npcs[1], npcs[0], HunchTone.Warm, 5);
            Seen(s, npcs[0], npcs[1], HunchTone.Warm, 6);
            // Older than the shelf life: not counted while a current one stands.
            Seen(s, npcs[0], npcs[1], HunchTone.Warm, 2);
            var hunch = Overhearing.Hunches(s).Single();
            Assert.That(hunch.rows, Is.EqualTo(3));
            Assert.That(hunch.current, Is.True);
            Assert.That(Overhearing.Label(hunch), Is.EqualTo("often"));
            Assert.That(Overhearing.Line(s, hunch), Does.EndWith("(noticed in weeks 4, 5 and 6)."));

            s.week = 9;
            var faded = Overhearing.Hunches(s).Single();
            Assert.That(faded.current, Is.False, "No sighting is current: the line fades.");
            Assert.That(faded.rows, Is.EqualTo(4));
        }

        [Test]
        public void TwoTonesOnOnePairAreTwoHunchesWhicheverWayRoundTheyWereSeen()
        {
            var s = OverhearTests.Talking(StoryRules.Manners, "casual");
            var npcs = Npcs(s);
            Seen(s, npcs[0], npcs[1], HunchTone.Warm, s.week);
            Seen(s, npcs[1], npcs[0], HunchTone.Warm, s.week);
            Seen(s, npcs[0], npcs[1], HunchTone.Tense, s.week);
            var hunches = Overhearing.Hunches(s);
            Assert.That(hunches.Select(h => h.tone).OrderBy(t => t), Is.EqualTo(new[] { HunchTone.Warm, HunchTone.Tense }));
            Assert.That(hunches.Single(h => h.tone == HunchTone.Warm).rows, Is.EqualTo(2));
            Assert.That(Overhearing.Label(hunches.Single(h => h.tone == HunchTone.Warm)), Is.EqualTo("probably"));
        }

        [Test]
        public void HunchesAreOnlyWhatYouSawNeverThePactsNorTheEvicted()
        {
            var s = OverhearTests.Talking(StoryRules.Manners, "casual");
            var npcs = Npcs(s);
            Seen(s, npcs[0], npcs[1], HunchTone.Scheming, s.week);
            Seen(s, npcs[2], npcs[3], HunchTone.Warm, s.week);
            // Other sources between the same two are not sightings.
            s.ledger.standings.Add(new StandingRow { week = s.week, fromId = npcs[0], toId = npcs[2], source = ClaimSource.Overheard, score = 40 });
            Assert.That(AllianceRead.SuspectedPairs(s), Is.Empty, "No pact the player knows of, and still a line.");
            Assert.That(Overhearing.Hunches(s).Count, Is.EqualTo(2));
            s.Find(npcs[3]).status = ContestantStatus.Evicted;
            var left = Overhearing.Hunches(s);
            Assert.That(left.Count, Is.EqualTo(1), "A hunch on somebody gone is not drawn.");
            Assert.That(new[] { left[0].firstId, left[0].secondId }, Is.EquivalentTo(new[] { npcs[0], npcs[1] }));
        }

        [Test]
        public void TheWordsCountWeeksAndConfidenceAsTheWebSaysThem()
        {
            Assert.That(Overhearing.Weeks(new[] { 4 }), Is.EqualTo("week 4"));
            Assert.That(Overhearing.Weeks(new[] { 4, 5 }), Is.EqualTo("weeks 4 and 5"));
            Assert.That(Overhearing.Weeks(new[] { 3, 4, 5 }), Is.EqualTo("weeks 3, 4 and 5"));
            Assert.That(new[] { 1, 2, 3, 4 }.Select(Overhearing.Confidence), Is.EqualTo(new[] { "maybe", "probably", "often", "often" }));
            Assert.That(Overhearing.HunchesCaption, Is.EqualTo("Hunches"));
        }

        [TestCase("strategy", true)]
        [TestCase("alliance_talk", true)]
        [TestCase("nominations", true)]
        [TestCase("gossip", true)]
        [TestCase("tension", true)]
        [TestCase("casual", false)]
        [TestCase("bonding", false)]
        [TestCase("rivalry", false)]
        public void ALingeringPlayerListensInOnlyOnTheListeningTopics(string topic, bool listens)
        {
            Assert.That(Overhearing.ListenTopic(topic), Is.EqualTo(listens));
        }

        [Test]
        public void TheCatchRiskIsShownBeforeThePress()
        {
            var s = OverhearTests.Talking(StoryRules.Manners, "strategy");
            var talk = s.npcSocial.pending[0];
            foreach (var c in s.contestants) c.traits = new List<string>();
            Assert.That(Overhearing.NearbyFoot(s, talk.firstId, talk.secondId), Is.EqualTo("Free · stay close · 15% they notice"));
            s.Find(talk.secondId).traits.Add("Analytical");
            Assert.That(Overhearing.CatchPercent(s, talk.firstId, talk.secondId), Is.EqualTo(25));
            s.Find(s.playerId).traits.Add("Sneaky");
            Assert.That(Overhearing.NearbyFoot(s, talk.firstId, talk.secondId), Is.EqualTo("Free · stay close · 20% they notice"));
            Assert.That(Overhearing.NearbyFoot(s, null, null), Is.EqualTo(Overhearing.FreeFoot));
        }

        [Test]
        public void TheWalkOverFindsTheNearestConversationStillOpenAndStopsAtTheWeeksTwo()
        {
            var s = OverhearTests.Talking(StoryRules.Manners, "strategy", pairs: 2);
            var near = s.npcSocial.pending[1];
            var far = s.npcSocial.pending[0];
            float Distance(NpcConversationState c) => c.sequence == near.sequence ? 3f : 10f;
            Assert.That(Overhearing.ListenLeft(s), Is.True);
            Assert.That(Overhearing.Listenable(s, Distance).Select(c => c.sequence), Is.EqualTo(new[] { near.sequence, far.sequence }));
            Assert.That(Overhearing.Listenable(s, c => c.sequence == near.sequence ? float.PositiveInfinity : 1f).Select(c => c.sequence),
                Is.EqualTo(new[] { far.sequence }), "A pair the house cannot place is not walked to.");

            var heard = new EpisodeEngine(s).Apply(OverhearTests.Overhear(s, near, true));
            Assert.That(heard.accepted, Is.True, heard.reason);
            s = heard.state;
            Assert.That(Overhearing.Refusal(s, near.sequence, true), Is.Not.Null, "A conversation is listened in on once.");
            Assert.That(Overhearing.Listenable(s, Distance).Select(c => c.sequence), Is.EqualTo(new[] { far.sequence }));

            var second = new EpisodeEngine(s).Apply(OverhearTests.Overhear(s, s.npcSocial.pending.Single(c => c.sequence == far.sequence), true));
            Assert.That(second.accepted, Is.True, second.reason);
            s = second.state;
            Assert.That(Overhearing.ListenLeft(s), Is.False, "Two a week, heard or caught.");
            Assert.That(Overhearing.Listenable(s, Distance), Is.Empty);
        }

        [Test]
        public void ReadingTheScreenChangesNothingInTheSeason()
        {
            var s = Noticed("strategy");
            string before = Json(s);
            var talk = s.npcSocial.pending[0];
            Overhearing.Hunches(s).ForEach(h => Overhearing.Line(s, h));
            Overhearing.NearbyFoot(s, talk.firstId, talk.secondId);
            Overhearing.Listenable(s, c => 1f);
            Overhearing.Refusal(s, talk.sequence, true);
            Overhearing.ListenLeft(s);
            Assert.That(Json(s), Is.EqualTo(before), "No draw, no write: randomState and every record as they were.");
        }
    }
}
