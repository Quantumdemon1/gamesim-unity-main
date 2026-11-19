using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 31's last field (FIELDS.md): the housemates' minds' ladder, <see cref="MindRules"/>, saved as
    /// <c>story.mindRules</c>. Its bounds and admission by schema, the fresh season's level and its place in
    /// <see cref="ShippedRules.Fields"/>, the validation that holds it, and the two test boundaries that take a live
    /// state back to schema 30 (the digest observer and the old-save trace). Nothing reads the level to change play.
    /// Simulation only, so the Unity-free subset runs it too.
    /// </summary>
    public sealed class MindRulesSchema31Tests
    {
        // ------------------------------------------------------------------ the ladder

        [Test]
        public void TheLadderIsBoundedAtBlindsidesAndShipsOff()
        {
            Assert.That(MindRules.Latest, Is.EqualTo(MindRules.Blindsides));
            Assert.That(new[] { MindRules.Off, MindRules.Decisions, MindRules.Plans, MindRules.Memory, MindRules.Storylines, MindRules.Blindsides },
                Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(MindRules.Shipped, Is.EqualTo(MindRules.Off), "No level is enrolled.");
            Assert.That(MindRules.Schema, Is.EqualTo(31));
        }

        [TestCase(-1, false)] [TestCase(0, true)] [TestCase(1, true)] [TestCase(5, true)] [TestCase(6, false)]
        public void IsDefinedIsTheClosedSetZeroToLatest(int level, bool defined) =>
            Assert.That(MindRules.IsDefined(level), Is.EqualTo(defined));

        [TestCase(0, 30, true)] [TestCase(0, 31, true)] [TestCase(1, 30, false)] [TestCase(1, 31, true)]
        [TestCase(5, 30, false)] [TestCase(5, 31, true)] [TestCase(6, 31, false)]
        public void ALevelIsAdmittedOnlyFromTheSchemaThatIntroducedIt(int level, int schema, bool admitted) =>
            Assert.That(MindRules.Admits(level, schema), Is.EqualTo(admitted));

        [Test]
        public void AtIsALowerBoundFromLevelOne()
        {
            var s = Fresh();
            Assert.That(MindRules.At(s, MindRules.Off), Is.False, "Level 0 is no rule.");
            Assert.That(MindRules.At(s, MindRules.Decisions), Is.False, "A season at 0 plays no level.");
            s.story.mindRules = MindRules.Memory;
            Assert.That(MindRules.At(s, MindRules.Decisions), Is.True);
            Assert.That(MindRules.At(s, MindRules.Memory), Is.True);
            Assert.That(MindRules.At(s, MindRules.Storylines), Is.False);
            Assert.That(MindRules.At(null, MindRules.Decisions), Is.False);
        }

        // ------------------------------------------------------------------ the fresh season

        [Test]
        public void ApplyFreshWritesTheShippedLevelAndFieldsListsItLast()
        {
            var s = Fresh();
            Assert.That(s.story.mindRules, Is.Zero, "A built season holds 0 before ApplyFresh.");
            s.story.mindRules = MindRules.Storylines; // A value ApplyFresh must overwrite, so the write itself is proved.
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.mindRules, Is.EqualTo(MindRules.Shipped));
            var fields = ShippedRules.Fields(s);
            Assert.That(fields[fields.Count - 1].Key, Is.EqualTo("story.mindRules"));
            Assert.That(fields[fields.Count - 1].Value, Is.EqualTo(MindRules.Off));
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
        }

        // ------------------------------------------------------------------ validation

        [TestCase(6)] [TestCase(-1)]
        public void AnUndefinedMindLevelIsRefused(int level)
        {
            var s = Shipped();
            s.story.mindRules = level;
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Unsupported mind rules version."));
        }

        /// <summary>A defined level is admitted at 31 on its own: no other saved field hangs on it, and free talk stays off.</summary>
        [TestCase(1)] [TestCase(5)]
        public void ADefinedMindLevelIsAcceptedAtThirtyOne(int level)
        {
            var s = Shipped();
            s.story.mindRules = level;
            Assert.That(s.story.talkRules, Is.EqualTo(TalkRules.Off));
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
        }

        // ------------------------------------------------------------------ the schema-31 digest observer

        [Test]
        public void TheObserversViewDropsTheMindRulesAndEndsTheStoryAtItsReckonings()
        {
            var json = JObject.FromObject(Fresh());
            Assert.That(((JObject)json["story"]).Properties().Last().Name, Is.EqualTo("mindRules"), "Declared last on the story.");
            string before = Text(json);
            var view = LegacyDigestSchema31Observer.View30(json);
            Assert.That(Text(json), Is.EqualTo(before), "The input is never changed.");
            var story = (JObject)view["story"];
            Assert.That(story.Property("mindRules"), Is.Null);
            Assert.That(story.Properties().Last().Name, Is.EqualTo("reckonings"));
        }

        [TestCase("mind-rules")] [TestCase("mind-order")] [TestCase("missing-mind-rules")]
        public void MindRulesAwayFromZeroOrOutOfPlaceHaveNoSchemaThirtyView(string defect)
        {
            var json = JObject.FromObject(Fresh());
            var story = (JObject)json["story"];
            switch (defect)
            {
                case "mind-rules": story["mindRules"] = MindRules.Decisions; break;
                case "mind-order": { var p = story.Property("mindRules"); p.Remove(); story.AddFirst(p); break; }
                default: story.Remove("mindRules"); break;
            }
            string before = Text(json);
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema31Observer.View30(json), defect);
            Assert.That(Text(json), Is.EqualTo(before));
        }

        // ------------------------------------------------------------------ the old-save trace

        [Test]
        public void TheTraceTakesALiveSeasonToThirtyWithoutTheMindRules()
        {
            var fields = OldSaveTrace.Fields(Fresh());
            Assert.That(fields["story"]["mindRules"]?.Type, Is.EqualTo(JTokenType.Integer));
            var projected = OldSaveTrace.Project(fields, 30);
            Assert.That((int)projected["schemaVersion"], Is.EqualTo(30));
            Assert.That(((JObject)projected["story"]).Property("mindRules"), Is.Null);
        }

        [Test]
        public void TheTraceRefusesASeasonWhoseMindRulesLeftZero()
        {
            var s = Fresh();
            s.story.mindRules = MindRules.Decisions;
            var fields = OldSaveTrace.Fields(s);
            Assert.Throws<InvalidDataException>(() => OldSaveTrace.Project(fields, 30));
        }

        // ------------------------------------------------------------------ helpers

        private static EpisodeState Fresh() => SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3103u);

        private static EpisodeState Shipped()
        {
            var s = Fresh();
            ShippedRules.ApplyFresh(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, "The control: " + reason);
            return s;
        }

        private static string Text(JToken token) => token.ToString(Formatting.None);
    }
}
