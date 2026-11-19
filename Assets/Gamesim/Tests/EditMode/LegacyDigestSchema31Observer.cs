using System;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Test-only current31 to retained30 boundary (the free talk's S0). Not a save migration and not an authority to
    /// discard any rule. Every digest of a current state is taken by <see cref="LegacyDigestSchema30Observer"/>, which since
    /// schema 31 reads the current JSON and the state's own view through <see cref="View30"/>: a schema-31 JSON with every
    /// talk field at its default and in its declared place - the story's three last on the story, each contact's last on
    /// the contact - loses them on a detached copy and gains the header 30, so the schema-30 observer's own contract, and
    /// every older one behind it, sees exactly the schema-30 JSON a schema-30 build wrote. A recorded digest therefore never
    /// moves for the bump: one that moves is a regression, never a re-record. A season with any talk field away from its
    /// default has no schema-30 view, and is refused here, in this observer's words.
    /// </summary>
    internal static class LegacyDigestSchema31Observer
    {
        /// <summary>The live schema this boundary reads.</summary>
        internal const int Live = 31;

        /// <summary>Schema 31's story fields, in their declared order after the reckonings.</summary>
        internal static readonly string[] Story = { "talkRules", "talkLean", "talkPairs" };

        /// <summary>Schema 31's contact fields, in ContactState's declared order after lastWeek.</summary>
        internal static readonly string[] Contact =
        {
            "talkWindow", "talks", "talkTenths", "spentTalks", "talkPending", "agreement", "lastTone", "priorTone",
            "standingWeek", "standing", "stageReached", "cool", "spark", "romance", "romanceWeek", "romanceClosedUntil",
            "flirts", "notYets", "turnAsides", "coupleAnchors", "walkUpWindow", "notNows", "walkUpsOffUntil", "talkFlags"
        };

        /// <summary>
        /// The schema-30 view of a JSON: a schema-31 one with every talk field at its default and in its place, on a
        /// detached copy without them and with the header 30; any other JSON as it is (the schema-30 observer's own
        /// contract judges it). The input is never changed.
        /// </summary>
        internal static JObject View30(JObject source)
        {
            if (source == null || !(source["schemaVersion"]?.Type == JTokenType.Integer && (long)source["schemaVersion"] == Live)) return source;
            Require(source["story"] is JObject, "A schema-31 source holds its story.");
            var story = (JObject)source["story"];
            var names = story.Properties().Select(p => p.Name).ToList();
            Require(names.Count >= Story.Length && names.Skip(names.Count - Story.Length).SequenceEqual(Story),
                "The talk fields must stand last on the story, in their declared order.");
            Require(Integer(story["talkRules"], 0) && Integer(story["talkLean"], 0) && story["talkPairs"] is JArray pairs && pairs.Count == 0,
                "Only a season without free talk has a schema-30 view.");
            Require(story["contacts"] is JArray, "A schema-31 source holds its contacts.");
            foreach (var token in (JArray)story["contacts"])
            {
                Require(token is JObject, "A contact is an object.");
                var contact = (JObject)token;
                var fields = contact.Properties().Select(p => p.Name).ToList();
                Require(fields.Count >= Contact.Length && fields.Skip(fields.Count - Contact.Length).SequenceEqual(Contact),
                    "The talk fields must stand last on each contact, in their declared order.");
                foreach (string name in Contact)
                {
                    var value = contact[name];
                    bool atDefault = value is JArray list ? list.Count == 0
                        : value?.Type == JTokenType.String ? (string)value == ""
                        : Integer(value, 0);
                    Require(atDefault, "Only a season without free talk has a schema-30 view: " + name + ".");
                }
            }
            var view = (JObject)source.DeepClone();
            var viewStory = (JObject)view["story"];
            foreach (string name in Story) viewStory.Remove(name);
            foreach (JObject contact in (JArray)viewStory["contacts"])
                foreach (string name in Contact) contact.Remove(name);
            view["schemaVersion"] = 30;
            return view;
        }

        /// <summary>The schema-30 view of a live state's own default JSON (getters kept, as JObject.FromObject writes them).</summary>
        internal static JObject OwnView30(EpisodeState state) => state == null ? null : View30(JObject.FromObject(state));

        private static bool Integer(JToken token, long expected) => token?.Type == JTokenType.Integer && (long)token == expected;
        private static void Require(bool accepted, string reason)
        { if (!accepted) throw new InvalidDataException(reason); }
    }
}
