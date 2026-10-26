using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// SCH30's byte-level proof that ordinary seasons are unchanged (ALL-STAR-HISTORIES-PLAN 8.8). The state every fresh
    /// season starts in - each <c>ShippedRulesTests</c> builder after <see cref="ShippedRules.ApplyFresh"/>, and the
    /// default scene as <see cref="ContentCatalog.Create"/> builds it - over seeds 1, 77 and 4242, as the save writes it
    /// (the save's own serializer, compact JSON, its field order, SHA-256 of the UTF-8). Recorded at the live schema 29
    /// (SCH30 step 1, the build before the bump), so the bump has a schema-29 build to answer to: from step 2 the same
    /// season, taken back to the retained 29 by the test inverse, must hash to the same digest. A digest that moves is a
    /// regression, never a re-record (OLD-SAVE-ACCEPTANCE §6 step 5). BalanceLabGoldens hash the lab's rows, not the state.
    /// </summary>
    public sealed class FreshSeasonStateDigestTests
    {
        private static IEnumerable<TestCaseData> Seasons()
        {
            var builders = new (string builder, int size, CastTemplates.Roster roster)[]
            {
                ("quick start", 0, CastTemplates.Roster.Regular),
                ("regular", 6, CastTemplates.Roster.Regular),
                ("regular", 8, CastTemplates.Roster.Regular),
                ("regular", 12, CastTemplates.Roster.Regular),
                ("regular", 8, CastTemplates.Roster.AllStars),
                ("stress", 16, CastTemplates.Roster.Regular),
            };
            foreach (uint seed in new[] { 1u, 77u, 4242u })
            {
                foreach (var b in builders) yield return new TestCaseData(b.builder, b.size, b.roster, seed, true);
                yield return new TestCaseData("default scene", 0, CastTemplates.Roster.Regular, seed, false);
            }
        }

        private static string Key(string builder, int size, CastTemplates.Roster roster, uint seed) =>
            builder + "/" + size + "/" + roster + "/" + seed;

        /// <summary>Recorded at SCH30 step 1, at the live schema 29.</summary>
        private static readonly Dictionary<string, string> Recorded = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "quick start/0/Regular/1", "97fda4e547b17b9eb87eaed53a50ec7e4c3c4e7504415907e30fe24d7e27d19e" },
            { "regular/6/Regular/1", "3eed933b816e1bf171ad401a677f00f0febab2081ac980d96f7c86d1fd4fc178" },
            { "regular/8/Regular/1", "b6d9b1798a5c352e9eb7bfb4d1fa32d096617228879c2757e7d1b6ad0edb6d9b" },
            { "regular/12/Regular/1", "8898aaa78299e861b8e1309442332d16a4745f5228e030c6ac1be99347875770" },
            { "regular/8/AllStars/1", "3fa8f7eeb110c5a7a00932a7919dc9c2be6faed5637bad9f507d10d58b1011ee" },
            { "stress/16/Regular/1", "739474e6edcc5bdd1d5e1d8bdd3adac4666734dd2534e81bdbe8aa6ce9948ef1" },
            { "default scene/0/Regular/1", "b59c231753019f3b32e0fe699117ca0722d0872cd923417875ffecb25716018d" },
            { "quick start/0/Regular/77", "7730065271bcb6d87071f2b391d04e4a3d13fd37dc84360b47d3a12b0402426b" },
            { "regular/6/Regular/77", "7a8dcf0f8554ac6f3cc61795b0806023369e55bee757152f3da189aaa8144038" },
            { "regular/8/Regular/77", "d069ca5a9c91e0964b52139fb046cb2d2c6d642c5972d8b955ccc6cd00314a9c" },
            { "regular/12/Regular/77", "f84be404cc9f05eb4832a23a62ec6246fec6919ef14b0cdf20154ff7bf024b39" },
            { "regular/8/AllStars/77", "3f58443b3883441ac45d6b24a14c64af6b1d16051bac9ba629310333cc985223" },
            { "stress/16/Regular/77", "418bdd244aeecbe8075f07693f8cc4123a3a89b86c2627a75400e85baf49b519" },
            { "default scene/0/Regular/77", "eb7e35667d9661e19eeaf4e6f3190de5306f2057707aaee72bd8dfe9a7dd84fb" },
            { "quick start/0/Regular/4242", "6d238aed08d0389fb73063bf84f6fbec77eee455cf298863284df08947afbc21" },
            { "regular/6/Regular/4242", "a9538b38e2c4afaf881b1e0fd5b56829733f12943c622edb1c956563bfe3827e" },
            { "regular/8/Regular/4242", "08fede86af28d73ff345bec15a5f25a513f3a1ceb2d7362f4d97940b15e14322" },
            { "regular/12/Regular/4242", "9f4544be9c8fd6d9dfbbc89aa3eef400f8170616eb834809e009c4b074d32064" },
            { "regular/8/AllStars/4242", "4f7b43a591b97c2efbb85731842b770d66a923464b79eeb5f47149843f581341" },
            { "stress/16/Regular/4242", "16d8d72168d04988c2a9bf724a8de6d654c6ab967550e5719972b8c5edaca456" },
            { "default scene/0/Regular/4242", "3db71c7075d06c53e29a2239fde419a0038aba51a51cabd95ce5f589845ff0c7" },
        };

        [TestCaseSource(nameof(Seasons))]
        public void AFreshSeasonStartsInTheStateRecordedAtSchemaTwentyNine(string builder, int size, CastTemplates.Roster roster,
            uint seed, bool applyFresh)
        {
            var s = Build(builder, size, roster, seed);
            if (applyFresh) ShippedRules.ApplyFresh(s);
            var payload = Payload(s);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(29), "Recorded at the live schema 29.");
            string key = Key(builder, size, roster, seed);
            string digest = Digest(payload);
            Assert.That(Recorded.ContainsKey(key), Is.True, "RECORD " + key + " " + digest);
            Assert.That(digest, Is.EqualTo(Recorded[key]), key);
        }

        [Test]
        public void EveryBuilderAndSeedIsRecordedOnce() =>
            Assert.That(Recorded.Keys, Is.EquivalentTo(Seasons().Select(c => Key((string)c.Arguments[0], (int)c.Arguments[1],
                (CastTemplates.Roster)c.Arguments[2], (uint)c.Arguments[3]))));

        private static EpisodeState Build(string builder, int size, CastTemplates.Roster roster, uint seed)
        {
            switch (builder)
            {
                case "quick start":
                case "default scene": return ContentCatalog.Create(seed);
                case "stress": return SeasonBuilder.CreateVerificationStressHouse(size, seed);
                default: return SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = roster }, seed);
            }
        }

        private static JObject Payload(EpisodeState state)
        {
            var serializer = (JsonSerializer)typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true)
                .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            return JObject.FromObject(state, serializer);
        }

        private static string Digest(JObject payload)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None))))
                    .Replace("-", "").ToLowerInvariant();
        }
    }
}
