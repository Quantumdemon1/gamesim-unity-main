// These detached malformed-token controls do not replace accepted historical corpus replay.
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Persistence.Frozen28ReadOnly
{
    [TestFixture]
    internal sealed class FrozenV28WholeBoundaryTests
    {
        [Test]
        public void NullObjectUsesInvalidDataBoundary() =>
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(null));

        [TestCase(25)] [TestCase(27)] [TestCase(29)]
        public void OtherIntegerHeadersAreNotProjected(int version)
        {
            var root = new JObject { ["schemaVersion"] = version };
            var header = root["schemaVersion"];
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(root));
            Assert.That(root["schemaVersion"], Is.SameAs(header));
            Assert.That(root.Count, Is.EqualTo(1));
        }

        [Test]
        public void LiteralLookingFloatDoesNotBecomeInteger28()
        {
            var root = new JObject { ["schemaVersion"] = new JValue(28d) };
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(root));
            Assert.That(root["schemaVersion"].Type, Is.EqualTo(JTokenType.Float));
        }

        [Test]
        public void MissingShapeFieldIsNotDefaulted()
        {
            var root = new JObject { ["schemaVersion"] = 28 };
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(root));
            Assert.That(root.Properties().Select(p => p.Name), Is.EqualTo(new[] { "schemaVersion" }));
        }

        [Test]
        public void MalformedTreeOrderAndTokenIdentitySurviveRepeatedRefusal()
        {
            var payload = new JObject { ["schemaVersion"] = 28, ["unexpected"] = new JArray(3, 2, 1) };
            var names = payload.Properties().Select(p => p.Name).ToArray();
            var array = payload["unexpected"];
            var bytes = payload.ToString(Newtonsoft.Json.Formatting.None);
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(payload));
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(payload));
            Assert.That(payload["unexpected"], Is.SameAs(array));
            Assert.That(payload.Properties().Select(p => p.Name), Is.EqualTo(names));
            Assert.That(payload.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo(bytes));
        }

        [Test]
        public void DeepTreeIsRefusedWithoutReplacingAnyNode()
        {
            var root = new JObject { ["schemaVersion"] = 28 };
            var branch = new JArray(); root["extra"] = branch;
            var current = branch;
            for (int n = 0; n < 66; n++) { var next = new JArray(); current.Add(next); current = next; }
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(root));
            Assert.That(root["extra"], Is.SameAs(branch));
            Assert.That(current.Count, Is.Zero);
        }

        [Test]
        public void OversizedIntegerDoesNotBecomeAHeaderDefault()
        {
            var integer = new JValue(BigInteger.Pow(new BigInteger(10), 200));
            var root = new JObject { ["schemaVersion"] = integer };
            Assert.Throws<InvalidDataException>(() => FrozenEpisodeV28.Validate(root));
            Assert.That(root["schemaVersion"], Is.SameAs(integer));
        }
    }
}
