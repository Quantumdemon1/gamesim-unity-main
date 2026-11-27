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
                // A level past the enrolled ones: levels 1 and 2 with nothing else written are admitted since their enrolment (the observer's summary).
                case "talk-rules": story["talkRules"] = TalkRules.Romance + 1; break;
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

        /// <summary>
        /// The first schema-31 reference (OLD-SAVE-ACCEPTANCE 2.15): CurrentOldSaveTrace checkpoints a live state at 31 whole, its
        /// talk fields too, exactly as the recorder's OldSaveTrace hashes a state at its own schema; a season with a free talk had
        /// is still refused at 30, an invalid one is refused at 31 too, and the state is never changed. So 31 is admitted without
        /// loosening 30.
        /// </summary>
        [Test]
        public void TheCurrentTraceCheckpointsALiveSeasonWholeAtThirtyOneAndAFreeTalkHadStillHasNoThirty()
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3303u);
            ShippedRules.ApplyFresh(fresh);
            var engine = new EpisodeEngine(fresh);
            var s = engine.Snapshot;
            string npc = s.Active.First(c => !c.isPlayer).id;
            var offer = FreeTalk.Offer(s, npc);
            Assert.That(offer.Available, Is.True, offer.Reason);
            var talked = engine.Apply(new EpisodeCommand
            {
                id = "talk-31", actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
                kind = EpisodeCommandKind.JustTalk, targetId = npc, text = FreeTalk.CommandText(offer.Talk.Id, offer.Talk.Replies[0].Tone),
            });
            Assert.That(talked.accepted, Is.True, talked.reason);
            var state = talked.state;
            string before = Text(JObject.FromObject(state));

            var own = OldSaveTrace.Fields(state);
            Assert.That((int)own["story"]["contacts"].First(c => (string)c["npcId"] == npc)["talks"], Is.EqualTo(1), "A free talk was had.");
            var season = (JObject)own.DeepClone();
            season.Remove("events");
            var at31 = CurrentOldSaveTrace.Checkpoint(state, 31);
            Assert.That(at31.season, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(season, true))));
            Assert.That(at31.blind, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(own, true))));
            Assert.That(at31.text, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(own, false))));
            Assert.That(at31, Is.EqualTo(OldSaveTrace.Checkpoint(state, 31)), "The recorder hashes the same state the same way.");
            Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(state, 30), "A season with a free talk had has no schema-30 view.");
            Assert.That(Text(JObject.FromObject(state)), Is.EqualTo(before), "The state is never changed.");

            // Before the talk the same season is checkpointed at 30 by its own arm, as it always was, and at 31 whole: two views.
            Assert.That(CurrentOldSaveTrace.Checkpoint(s, 30), Is.EqualTo(OldSaveTrace.Checkpoint(s, 30)));
            Assert.That(CurrentOldSaveTrace.Checkpoint(s, 31), Is.Not.EqualTo(CurrentOldSaveTrace.Checkpoint(s, 30)));

            var invalid = state.Clone();
            invalid.contestants[1].id = invalid.contestants[0].id;
            Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(invalid, 31), "An invalid source is refused at 31 too.");
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
