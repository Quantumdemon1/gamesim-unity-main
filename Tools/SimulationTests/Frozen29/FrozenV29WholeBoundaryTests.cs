// Frozen29 (SCH30): carried from Tools/SimulationTests/Frozen28/FrozenV28WholeBoundaryTests.cs by name, then to schema 29
// where a case builds a whole schema-29 state or payload.
// These detached malformed-token controls do not replace accepted historical corpus replay.
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Persistence.Frozen29ReadOnly
{
    [TestFixture]
    internal sealed class FrozenV29WholeBoundaryTests
    {
        [Test]
        public void NullObjectUsesInvalidDataBoundary() =>
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(null));

        [TestCase(25)] [TestCase(28)] [TestCase(30)]
        public void OtherIntegerHeadersAreNotProjected(int version)
        {
            var root = new JObject { ["schemaVersion"] = version };
            var header = root["schemaVersion"];
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(root));
            Assert.That(root["schemaVersion"], Is.SameAs(header));
            Assert.That(root.Count, Is.EqualTo(1));
        }

        [Test]
        public void LiteralLookingFloatDoesNotBecomeInteger29()
        {
            var root = new JObject { ["schemaVersion"] = new JValue(29d) };
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(root));
            Assert.That(root["schemaVersion"].Type, Is.EqualTo(JTokenType.Float));
        }

        [Test]
        public void MissingShapeFieldIsNotDefaulted()
        {
            var root = new JObject { ["schemaVersion"] = 29 };
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(root));
            Assert.That(root.Properties().Select(p => p.Name), Is.EqualTo(new[] { "schemaVersion" }));
        }

        [Test]
        public void MalformedTreeOrderAndTokenIdentitySurviveRepeatedRefusal()
        {
            var payload = new JObject { ["schemaVersion"] = 29, ["unexpected"] = new JArray(3, 2, 1) };
            var names = payload.Properties().Select(p => p.Name).ToArray();
            var array = payload["unexpected"];
            var bytes = payload.ToString(Newtonsoft.Json.Formatting.None);
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(payload));
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(payload));
            Assert.That(payload["unexpected"], Is.SameAs(array));
            Assert.That(payload.Properties().Select(p => p.Name), Is.EqualTo(names));
            Assert.That(payload.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo(bytes));
        }

        [Test]
        public void DeepTreeIsRefusedWithoutReplacingAnyNode()
        {
            var root = new JObject { ["schemaVersion"] = 29 };
            var branch = new JArray(); root["extra"] = branch;
            var current = branch;
            for (int n = 0; n < 66; n++) { var next = new JArray(); current.Add(next); current = next; }
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(root));
            Assert.That(root["extra"], Is.SameAs(branch));
            Assert.That(current.Count, Is.Zero);
        }

        [Test]
        public void OversizedIntegerDoesNotBecomeAHeaderDefault()
        {
            var integer = new JValue(BigInteger.Pow(new BigInteger(10), 200));
            var root = new JObject { ["schemaVersion"] = integer };
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV29.Validate(root));
            Assert.That(root["schemaVersion"], Is.SameAs(integer));
        }
    }
}
