using System;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 31's current31 to retained30 boundary (<see cref="LegacyDigestSchema31Observer"/>, the free talk's S0): a live
    /// state's JSON with every talk field at its default loses exactly them and gains the header 30, so every recorded
    /// digest is taken from the JSON a schema-30 build wrote; any talk value away from its default, a field out of its
    /// place or missing, is refused in this boundary's words, and the input is never changed. Unity-free subset too.
    /// </summary>
    public sealed class LegacyDigestSchema31ObserverTests
    {
        [Test]
        public void AnOffSeasonsJsonLosesExactlyTheTalkFieldsAndTakesTheHeader30()
        {
            var state = WithContact();
            var json = JObject.FromObject(state);
            string before = Text(json);
            Assert.That((int)json["schemaVersion"], Is.EqualTo(31));
            var view = LegacyDigestSchema31Observer.View30(json);
            Assert.That(view, Is.Not.SameAs(json));
            Assert.That(Text(json), Is.EqualTo(before), "The input is never changed.");
            Assert.That((int)view["schemaVersion"], Is.EqualTo(30));
            var story = (JObject)view["story"];
            Assert.That(story.Properties().Select(p => p.Name).Intersect(LegacyDigestSchema31Observer.Story), Is.Empty);
            Assert.That(story.Properties().Last().Name, Is.EqualTo("reckonings"), "The story ends where a schema-30 build ended it.");
            var contact = (JObject)story["contacts"][0];
            Assert.That(contact.Properties().Select(p => p.Name), Is.EqualTo(new[] { "npcId", "rapport", "weekCount", "lastWeek" }));
            // Exactly the fields removed: put back in place, the JSON is the live one again.
            var again = (JObject)view.DeepClone();
            again["schemaVersion"] = 31;
            foreach (string name in LegacyDigestSchema31Observer.Story) ((JObject)again["story"]).Add(name, json["story"][name].DeepClone());
            foreach (string name in LegacyDigestSchema31Observer.Contact) ((JObject)again["story"]["contacts"][0]).Add(name, json["story"]["contacts"][0][name].DeepClone());
            Assert.That(Text(again), Is.EqualTo(before));
        }

        [Test]
        public void AnyOtherHeaderPassesThroughUnchanged()
        {
            var json = JObject.FromObject(WithContact());
            json["schemaVersion"] = 30;
            Assert.That(LegacyDigestSchema31Observer.View30(json), Is.SameAs(json));
            Assert.That(LegacyDigestSchema31Observer.View30(null), Is.Null);
        }

        [TestCase("talk-rules")] [TestCase("lean")] [TestCase("pair")] [TestCase("talks")] [TestCase("tone")] [TestCase("flag")]
        [TestCase("story-order")] [TestCase("contact-order")] [TestCase("missing-story-field")] [TestCase("missing-contact-field")]
        public void ATalkValueOrAFieldOutOfPlaceHasNoSchemaThirtyView(string defect)
        {
            var json = JObject.FromObject(WithContact());
            var story = (JObject)json["story"];
            var contact = (JObject)story["contacts"][0];
            switch (defect)
            {
                // A level past the enrolled one: level 1 with nothing else written is admitted since its enrolment (the observer's summary).
                case "talk-rules": story["talkRules"] = TalkRules.Talk + 1; break;
                case "lean": story["talkLean"] = -1; break;
                case "pair": ((JArray)story["talkPairs"]).Add(new JObject()); break;
                case "talks": contact["talks"] = 1; break;
                case "tone": contact["lastTone"] = TalkRules.Warm; break;
                case "flag": ((JArray)contact["talkFlags"]).Add(TalkRules.Ex); break;
                case "story-order": { var p = story.Property("talkRules"); p.Remove(); story.AddFirst(p); break; }
                case "contact-order": { var p = contact.Property("talkFlags"); p.Remove(); contact.AddFirst(p); break; }
                case "missing-story-field": story.Remove("talkPairs"); break;
                default: contact.Remove("walkUpWindow"); break;
            }
            string before = Text(json);
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema31Observer.View30(json), defect);
            Assert.That(Text(json), Is.EqualTo(before));
        }

        [Test]
        public void TheSchemaThirtyObserverReadsALiveSeasonThroughTheView()
        {
            var state = WithContact();
            var fields = OldSaveTrace.Fields(state);
            var retained29 = LegacyDigestSchema30Observer.ProjectFields29(state, fields);
            Assert.That((int)retained29["schemaVersion"], Is.EqualTo(29));
            Assert.That(Text(retained29), Is.EqualTo(Text(OldSaveTrace.Project(fields, 29))),
                "The recorder's literals take the same live state to the same 29.");
            Assert.That(((JObject)retained29["story"]).Property("talkRules"), Is.Null);
        }

        private static EpisodeState WithContact()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3102u);
            s.story.contacts.Add(new ContactState { npcId = s.contestants.First(c => !c.isPlayer).id });
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
            return s;
        }

        private static string Text(JToken token) => token.ToString(Formatting.None);
    }
}
