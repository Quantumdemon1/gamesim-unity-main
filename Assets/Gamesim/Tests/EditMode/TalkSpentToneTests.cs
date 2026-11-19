using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 31's spent talks keep the tone each answered talk was given in ("&lt;talk id&gt;:&lt;tone&gt;"), because a
    /// later callback talk's gate reads it (spec 5.3, <c>callback=&lt;talk id&gt;:&lt;tone&gt;</c>) and the feature has no
    /// second schema step (ruling R2). A talk with no answer is spent as its bare id. Validation holds each entry to that
    /// form, and a talk is spent once whatever its tone. Simulation only, so the Unity-free subset runs it too.
    /// </summary>
    public sealed class TalkSpentToneTests
    {
        [Test]
        public void ASpentEntryCarriesTheToneItWasAnsweredIn()
        {
            Assert.That(TalkRules.ToneMark, Is.EqualTo(':'));
            string entry = TalkRules.Spent("casey.l1.bywater", TalkRules.Warm);
            Assert.That(entry, Is.EqualTo("casey.l1.bywater:warm"));
            Assert.That(TalkRules.SpentId(entry), Is.EqualTo("casey.l1.bywater"));
            Assert.That(TalkRules.SpentTone(entry), Is.EqualTo(TalkRules.Warm));
            Assert.That(TalkRules.Spent("casey.softno", ""), Is.EqualTo("casey.softno"));
            Assert.That(TalkRules.Spent("casey.softno", null), Is.EqualTo("casey.softno"));
            Assert.That(TalkRules.SpentTone("casey.softno"), Is.EqualTo(""));
        }

        [TestCase("t1", true)]
        [TestCase("t1:warm", true)]
        [TestCase("t1:callout", true)]
        [TestCase("t1:", false)]
        [TestCase(":warm", false)]
        [TestCase("t1:shout", false)]
        [TestCase("t1:warm:push", false)]
        [TestCase("", false)]
        public void ASpentEntryIsAnIdAndAtMostOneKnownTone(string entry, bool valid) =>
            Assert.That(TalkRules.IsSpentEntry(entry), Is.EqualTo(valid));

        [Test]
        public void ASpentIdIsBoundedWithOrWithoutItsTone()
        {
            string longest = new string('t', TalkRules.MostIdLength);
            Assert.That(TalkRules.IsSpentEntry(longest), Is.True);
            Assert.That(TalkRules.IsSpentEntry(longest + ":" + TalkRules.CallOut), Is.True);
            Assert.That(TalkRules.IsSpentEntry(longest + "t"), Is.False);
            Assert.That(TalkRules.IsSpentEntry(longest + "t:" + TalkRules.Warm), Is.False);
            Assert.That(TalkRules.IsSpentEntry(null), Is.False);
        }

        [Test]
        public void ValidationAcceptsAnsweredAndUnansweredSpentTalks()
        {
            var s = Valid();
            var c = s.story.contacts[0];
            c.spentTalks.Add("casey.l1.bywater:warm");
            c.spentTalks.Add("casey.softno");
            foreach (var tone in TalkRules.Tones) c.spentTalks.Add("t." + tone + ":" + tone);
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
        }

        [TestCase("same-id-two-tones")]
        [TestCase("same-id-bare-and-toned")]
        [TestCase("unknown-tone")]
        [TestCase("empty-tone")]
        [TestCase("no-id")]
        [TestCase("two-tones")]
        public void ValidationRefusesAMalformedOrTwiceSpentTalk(string defect)
        {
            var s = Valid();
            var spent = s.story.contacts[0].spentTalks;
            switch (defect)
            {
                case "same-id-two-tones": spent.Add("t1:warm"); spent.Add("t1:push"); break;
                case "same-id-bare-and-toned": spent.Add("t1"); spent.Add("t1:warm"); break;
                case "unknown-tone": spent.Add("t1:shout"); break;
                case "empty-tone": spent.Add("t1:"); break;
                case "no-id": spent.Add(":warm"); break;
                default: spent.Add("t1:warm:push"); break;
            }
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Invalid contact data."));
        }

        // ------------------------------------------------------------------ helpers

        private const int Week = 3;

        /// <summary>A shipped season walked to week three with one contact at its defaults, at the latest talk level.</summary>
        private static EpisodeState Valid()
        {
            if (Walked == null)
            {
                var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3101u);
                ShippedRules.ApplyFresh(s);
                var engine = new EpisodeEngine(s);
                for (int i = 0; i < 4000 && engine.Snapshot.week < Week; i++)
                    Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
                Walked = engine.Snapshot;
                Assert.That(Walked.week, Is.EqualTo(Week));
            }
            var copy = Walked.Clone();
            if (copy.story.contacts.Count == 0)
                copy.story.contacts.Add(new ContactState { npcId = copy.contestants.First(c => !c.isPlayer).id });
            copy.story.talkRules = TalkRules.Latest;
            Assert.That(EpisodeValidation.TryValidate(copy, out string reason), Is.True, "The control: " + reason);
            return copy;
        }

        private static EpisodeState Walked;
    }
}
