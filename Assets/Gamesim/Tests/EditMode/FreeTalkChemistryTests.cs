using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 2 (talk rules 2): chemistry (rulings R10, T1), its tenths (T2) and its gate shifts (T2), and the landing a
    /// tone used in either of the last two talks loses (R10). On the tests' own table. Simulation only.
    /// </summary>
    public sealed class FreeTalkChemistryTests
    {
        [TestCase(10, 1, 6, ChemistryBand.Kindred)]
        [TestCase(10, 0, 5, ChemistryBand.Easy)]
        [TestCase(9, 0, 5, ChemistryBand.Easy)]
        [TestCase(3, 0, 2, ChemistryBand.Easy)]
        [TestCase(2, 0, 1, ChemistryBand.Neither)]
        [TestCase(-2, 0, -1, ChemistryBand.Neither)]
        [TestCase(-3, 0, -2, ChemistryBand.Grating)]
        [TestCase(0, 9, 3, ChemistryBand.Easy)]
        [TestCase(0, -9, -3, ChemistryBand.Grating)]
        [TestCase(14, 3, 8, ChemistryBand.Kindred)]
        [TestCase(-10, -3, -8, ChemistryBand.Grating)]
        public void ChemistryIsHalfTheAgreementPlusTheClampedAffinityInItsBands(int agreement, int affinity, int value, ChemistryBand band)
        {
            Assert.That(TalkChemistry.ValueOf(agreement, affinity), Is.EqualTo(value));
            Assert.That(TalkChemistry.BandOf(value), Is.EqualTo(band));
        }

        [Test]
        public void TheWordsAreTheSpecsAndNeverANumber()
        {
            Assert.That(TalkChemistry.Word(ChemistryBand.Kindred), Is.EqualTo("kindred"));
            Assert.That(TalkChemistry.Word(ChemistryBand.Easy), Is.EqualTo("easy company"));
            Assert.That(TalkChemistry.Word(ChemistryBand.Neither), Is.EqualTo("neither"));
            Assert.That(TalkChemistry.Word(ChemistryBand.Grating), Is.EqualTo("grating"));
        }

        [Test]
        public void ChemistryShowsAndActsOnlyFromTheThirdCountedTalkAtTalkRulesTwo()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string npc = FreeTalkFixture.Npcs(s)[0].id;
            var contact = FreeTalkS2Fixture.Kindred(s, npc);
            contact.talks = 2;
            Assert.That(TalkChemistry.Value(s, npc), Is.EqualTo(8), "The control: kindred by value.");
            Assert.That(TalkChemistry.Shown(s, npc), Is.False);
            Assert.That(TalkChemistry.Band(s, npc), Is.EqualTo(ChemistryBand.Neither));
            Assert.That(TalkChemistry.WordFor(s, npc), Is.Null);
            Assert.That(TalkChemistry.RapportGate(s, npc, 6), Is.EqualTo(6));
            Assert.That(TalkNotes.BondWith(s, npc).ChemistryLine, Is.Null);

            contact.talks = 3;
            Assert.That(TalkChemistry.Band(s, npc), Is.EqualTo(ChemistryBand.Kindred));
            Assert.That(TalkChemistry.WordFor(s, npc), Is.EqualTo("kindred"));
            Assert.That(TalkNotes.BondWith(s, npc).ChemistryLine, Is.EqualTo("Chemistry: kindred"));

            var one = s.Clone();
            one.story.talkRules = TalkRules.Talk;
            Assert.That(TalkChemistry.Shown(one, npc), Is.False, "Talk rules 1 never shows it.");
            Assert.That(TalkChemistry.Band(one, npc), Is.EqualTo(ChemistryBand.Neither));
            Assert.That(TalkNotes.BondWith(one, npc), Is.Null);
        }

        [TestCase(1, TalkPools.Spark, ChemistryBand.Kindred, 12)]
        [TestCase(0, TalkPools.Steady, ChemistryBand.Easy, 6)]
        [TestCase(-1, TalkPools.Guarded, ChemistryBand.Grating, 1)]
        [TestCase(-1, TalkPools.Spark, ChemistryBand.Easy, 3)]   // 2 x 1.2 x 1.1 = 2.64: rounded twice it would be 2
        [TestCase(1, TalkPools.Guarded, ChemistryBand.Easy, 7)]  // 8 x 0.8 x 1.1 = 7.04
        public void TenthsAreTheClassAndChemistryProductRoundedOnce(int landing, string listener, ChemistryBand band, int tenths)
        {
            Assert.That(FreeTalk.Tenths(landing, false, listener, band), Is.EqualTo(tenths));
        }

        [Test]
        public void AtNeitherTheTenthsAreSliceOnesExactly()
        {
            foreach (int landing in new[] { 1, 0, -1 })
                foreach (bool repeat in new[] { false, true })
                    foreach (string listener in new[] { TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded })
                        Assert.That(FreeTalk.Tenths(landing, repeat, listener, ChemistryBand.Neither), Is.EqualTo(FreeTalk.Tenths(landing, repeat, listener)));
        }

        [Test]
        public void KindredAndGratingShiftEveryGateThroughTheOnePath()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            var npcs = FreeTalkFixture.Npcs(s);
            string kindred = npcs[0].id;
            FreeTalkS2Fixture.Kindred(s, kindred);
            Assert.That(TalkChemistry.RapportGate(s, kindred, 3), Is.EqualTo(1));
            Assert.That(TalkChemistry.RapportGate(s, kindred, 1), Is.EqualTo(0), "Not below 0.");
            Assert.That(TalkChemistry.WeekGate(s, kindred, 3), Is.EqualTo(2));
            Assert.That(TalkChemistry.WeekGate(s, kindred, 1), Is.EqualTo(1), "Not below week 1.");
            Assert.That(TalkChemistry.WeekGate(s, kindred, 0), Is.EqualTo(0), "No gate stays no gate.");
            // The layers read through it: background opens at rapport 1 with a kindred housemate.
            FreeTalkFixture.ContactOf(s, kindred).rapport = 1;
            Assert.That(FreeTalk.OpenLayer(s, kindred), Is.EqualTo(2));
            var plain = s.Clone();
            plain.story.talkRules = TalkRules.Talk;
            Assert.That(FreeTalk.OpenLayer(plain, kindred), Is.EqualTo(1), "The control: talk rules 1 keeps the gate at 3.");

            // Grating, in a season of its own: every rapport gate 3 higher, week gates unchanged.
            var g = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string gid = FreeTalkFixture.Npcs(g)[1].id;
            FreeTalkS2Fixture.Grating(g, gid);
            Assert.That(TalkChemistry.Band(g, gid), Is.EqualTo(ChemistryBand.Grating));
            Assert.That(TalkChemistry.RapportGate(g, gid, 3), Is.EqualTo(6));
            Assert.That(TalkChemistry.WeekGate(g, gid, 2), Is.EqualTo(2));
            FreeTalkFixture.ContactOf(g, gid).rapport = 5;
            Assert.That(FreeTalk.OpenLayer(g, gid), Is.EqualTo(1), "Background waits for rapport 6.");
            FreeTalkFixture.ContactOf(g, gid).rapport = 6;
            Assert.That(FreeTalk.OpenLayer(g, gid), Is.EqualTo(2));
        }

        [Test]
        public void TheThirdCountedTalkShowsTheWordInItsResult()
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
                string npc = FreeTalkFixture.Npcs(s)[0].id;
                var contact = FreeTalkS2Fixture.Kindred(s, npc);
                contact.talks = 2;
                var offer = FreeTalk.Offer(s, npc);
                Assert.That(offer.Available, Is.True, offer.Reason);
                var third = FreeTalk.Result(s, npc, offer.Talk.Id, offer.Talk.Replies[0].Tone);
                Assert.That(third.ChemistryShown, Is.EqualTo(TalkChemistry.Word(TalkChemistry.BandOf(TalkChemistry.ValueOf(third.AgreementAfter, 3)))));

                contact.talks = 1;
                Assert.That(FreeTalk.Result(s, npc, offer.Talk.Id, offer.Talk.Replies[0].Tone).ChemistryShown, Is.Null, "The second talk shows nothing.");
                contact.talks = 2;
                s.story.talkRules = TalkRules.Talk;
                Assert.That(FreeTalk.Result(s, npc, offer.Talk.Id, offer.Talk.Replies[0].Tone).ChemistryShown, Is.Null, "Talk rules 1 shows nothing.");
            }
        }

        [Test]
        public void AToneUsedInEitherOfTheLastTwoTalksLandsOneStepLowerAtTalkRulesTwo()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            Assert.That(FreeTalkFixture.TryFind(s, 1, null, out string npc, out string tone), Is.True, "A tone that lands.");
            var contact = FreeTalkFixture.ContactOf(s, npc);
            Assert.That(FreeTalk.LandingNow(s, npc, tone), Is.EqualTo(1));
            contact.lastTone = tone;
            Assert.That(FreeTalk.LandingNow(s, npc, tone), Is.EqualTo(0));
            contact.lastTone = TalkRules.Tones.First(t => t != tone && t != TalkRules.Flirt);
            contact.priorTone = tone;
            Assert.That(FreeTalk.LandingNow(s, npc, tone), Is.EqualTo(0));
            s.story.talkRules = TalkRules.Talk;
            Assert.That(FreeTalk.LandingNow(s, npc, tone), Is.EqualTo(1), "Talk rules 1 keeps slice 1's landing.");
        }
    }
}
