using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 31's step 1 (the free talk's S0): the frozen schema-30 whole reader on the real save paths, and its parity
    /// with the live validator, in FrozenEpisodeV29IntegrationTests' form. Written at the live 30, where
    /// PrepareCurrentPayload ran the reader on every direct 30 and on the end of every older chain; since the bump (step
    /// 2) it is UpgradeV30ToV31's first step and a direct 30 migrates, the live side reading it with schema 31's
    /// literals added by hand (PersistenceMigrationTests.Lift31). Reflection reaches
    /// the internal reader; direct calls prove the dispatch wiring (a malformed 30 is refused by PrepareCurrentPayload
    /// itself, so the case fails if the call is removed); real temporary slots prove load, save, rotation and reload.
    /// The parity matrix holds the reader to the live validator: every retained fixture as the chain hands it on at
    /// 30, fresh seasons the game starts, and mutations of the schema-30 writer fixtures. Both accept or both refuse,
    /// and a semantic refusal says the same thing. Every edit is a detached JSON control.
    /// </summary>
    public sealed class FrozenEpisodeV30IntegrationTests
    {
        private const string EdgeReason = "The all-week rules' records are only ever between two houseguests.";
        private static readonly string[] Current30 =
        {
            "s30-default-w2-social", "s30-mode2-allstars-w3-campaign", "s30-mode2-allstars-alumni1-w3-campaign",
            "s30-mode2-allstars-alumni5-w3-campaign", "s30-mode2-allweek-w3-campaign", "s30-mode2-allweek-story10-tuning1-w2-hoh",
            "s30-mode2-allweek-story11-competition5-tuning1-w1-veto-meeting", "s30-mode2-allweek-story11-tuning1-w3-veto-meeting",
            "s30-mode2-allweek-tuning1-studier-w2-hoh", "s30-mode2-allweek-tuning1-w3-campaign",
            "s30-mode2-dialogue1-tuning1-w1-campaign", "s30-mode2-dialogue2-tuning1-w1-campaign",
        };
        private static string FixtureRoot => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");

        // ------------------------------------------------------------------ the dispatch

        [TestCaseSource(nameof(Current30))]
        public void DirectThirtyDispatchRunsTheFrozenReaderAndReturnsADetachedCopy(string id)
        {
            var payload = Fixture(id);
            var before = new TokenImage(payload);
            Frozen(30, payload);
            var result = EpisodeSaveMigrations.PrepareCurrentPayload(payload, out bool migrated);
            Assert.That(migrated, Is.True, "A direct 30 is historical since schema 31: held to its frozen reader, then the talk step.");
            Assert.That(result, Is.Not.SameAs(payload));
            Assert.That(result.ToString(Formatting.None), Is.EqualTo(PersistenceMigrationTests.Lift31(payload).ToString(Formatting.None)));
            Assert.That(result.ToString(Formatting.None), Is.EqualTo(EpisodeSaveMigrations.UpgradeV30ToV31(payload).ToString(Formatting.None)));
            var originals = new HashSet<JToken>(payload.DescendantsAndSelf(), TokenIdentity.Instance);
            foreach (var copy in result.DescendantsAndSelf())
                Assert.That(originals.Contains(copy), Is.False, "No returned token aliases any retained input token.");
            ((JArray)result["dialogue"]["episodes"]).Add("caller mutation");
            before.AssertUnchanged(payload);
        }

        [TestCase("s30-default-w2-social", "missing-field")]
        [TestCase("s30-default-w2-social", "unknown-field")]
        [TestCase("s30-default-w2-social", "dialogue-rules-3")]
        [TestCase("s30-default-w2-social", "story-rules-13")]
        [TestCase("s30-mode2-allweek-w3-campaign", "missing-field")]
        [TestCase("s30-mode2-allweek-w3-campaign", "unknown-field")]
        [TestCase("s30-mode2-allweek-w3-campaign", "player-edge")]
        [TestCase("s30-mode2-allweek-w3-campaign", "story-rules-13")]
        [TestCase("s30-mode2-dialogue2-tuning1-w1-campaign", "dialogue-rules-3")]
        [TestCase("s30-mode2-allstars-alumni5-w3-campaign", "missing-field")]
        public void MalformedCurrent30IsRefusedByDispatchItselfBeforeAnyLiveDtoHydration(string id, string defect)
        {
            var control = Fixture(id);
            Frozen(30, control);
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.PrepareCurrentPayload(control, out _));
            var bad = (JObject)control.DeepClone();
            Defect(bad, defect);
            var before = new TokenImage(bad);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var frozen = Assert.Throws<InvalidDataException>(() => Frozen(30, bad));
                bool migrated = true;
                var dispatch = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out migrated));
                Assert.That(migrated, Is.False);
                Assert.That(dispatch.Message, Is.EqualTo(frozen.Message));
                if (defect == "player-edge") Assert.That(dispatch.Message, Is.EqualTo(EdgeReason));
                before.AssertUnchanged(bad);
            }
        }

        [TestCase("s29-default-w2-social")]
        [TestCase("s29-mode1-allweek-w3-veto-meeting")]
        [TestCase("s29-mode2-allweek-w3-campaign")]
        [TestCase("s29-mode2-dialogue1-w2-campaign")]
        [TestCase("s28-mode2-finished")]
        [TestCase("s27-default-w2-social")]
        public void EveryOlderChainEndsAtTheFrozenThirtyGateAndLeavesItsInputUnmodified(string id)
        {
            var original = Fixture(id);
            var before = new TokenImage(original);
            var expected = EpisodeSaveMigrations.UpgradeV29ToV30((int)original["schemaVersion"] == 29 ? original
                : EpisodeSaveMigrations.PrepareV29Payload(original, out _));
            Frozen(30, expected);
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(original, out bool migrated);
            Assert.That(migrated, Is.True);
            Assert.That(current, Is.Not.SameAs(original));
            Assert.That(current.ToString(Formatting.None), Is.EqualTo(PersistenceMigrationTests.Lift31(expected).ToString(Formatting.None)));
            Assert.That((int)current["schemaVersion"], Is.EqualTo(31));
            before.AssertUnchanged(original);
        }

        [TestCaseSource(nameof(Current30))]
        public void RealSlotsLoadSaveRotateAndReloadTheSchemaThirtyWriterFixtures(string id)
        {
            byte[] source = FixtureBytes(id);
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, source);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(source), "Loading is read-only.");
            var loadedImage = Payload(loaded);
            Assert.That(message, Does.Contain("Schema 30").And.Contain("schema 31 in memory"), "A direct 30 migrates in memory.");
            Assert.That(JToken.DeepEquals(loadedImage, PersistenceMigrationTests.Lift31(Fixture(id))), Is.True, "The writer's state, with schema 31's literals.");
            var retained30 = (JObject)loadedImage.DeepClone(); PersistenceMigrationTests.StripSchema31(retained30);
            Frozen(30, retained30);
            slot.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(source), "Rotation retains the exact writer envelope.");
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(again), loadedImage), Is.True);
            Assert.That(File.ReadAllBytes(Path.Combine(FixtureRoot, id + ".save.json")), Is.EqualTo(source));
        }

        [Test]
        public void AFrozenRefusalOfADirectThirtyBlocksTheLoadAndLeavesBothCopies()
        {
            const string id = "s30-mode2-allweek-w3-campaign";
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, FixtureBytes(id));
            Assert.That(slot.Store.TryLoad(out var good, out string message), Is.True, message);
            slot.Store.Save(good);
            var bad = Fixture(id);
            Defect(bad, "player-edge");
            slot.Write(slot.Store.SavePath, System.Text.Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(bad)));
            var retained = slot.Image();
            Assert.That(slot.Store.TryLoad(out var refused, out message), Is.False);
            Assert.That(refused, Is.Null);
            Assert.That(message, Does.Contain(EdgeReason).And.Not.Contain("checksum"));
            slot.AssertImage(retained);
            Assert.That(slot.Store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(recovered), Payload(good)), Is.True);
        }

        // ------------------------------------------------------------------ parity with the live validator

        private static IEnumerable<string> AllFixtures() =>
            Directory.GetFiles(FixtureRoot, "*.save.json").Select(path => Path.GetFileName(path))
                .Select(name => name.Substring(0, name.Length - ".save.json".Length)).OrderBy(name => name, StringComparer.Ordinal);

        /// <summary>
        /// Every retained fixture, as the chain hands it on at 30 (a direct 30 as it is): the frozen reader and the live
        /// validator give it the same verdict. A fixture refused before 30 (by an older reader) has no 30 to compare.
        /// </summary>
        [Test]
        public void EveryRetainedFixtureGetsTheSameVerdictFromTheFrozenReaderAndTheLiveValidator()
        {
            int compared = 0, accepted = 0, direct = 0;
            var refusedEarlier = new List<string>();
            foreach (string id in AllFixtures())
            {
                var original = Fixture(id);
                JObject at30;
                if ((int)original["schemaVersion"] == 30) { at30 = original; direct++; }
                else
                {
                    try { at30 = EpisodeSaveMigrations.UpgradeV29ToV30((int)original["schemaVersion"] == 29 ? original : EpisodeSaveMigrations.PrepareV29Payload(original, out _)); }
                    catch (InvalidDataException) { refusedEarlier.Add(id); continue; }
                }
                string verdict = Agree(at30, id);
                compared++;
                if (verdict == null) accepted++;
            }
            TestContext.Out.WriteLine("Compared " + compared + " fixtures at 30 (" + direct + " direct), " + accepted
                + " accepted by both; refused before 30: " + string.Join(", ", refusedEarlier));
            Assert.That(direct, Is.EqualTo(Current30.Length), "Every schema-30 writer fixture is in the matrix.");
            Assert.That(compared, Is.GreaterThanOrEqualTo(60), "The manifest's fixtures, not a quietly empty folder.");
            Assert.That(accepted, Is.EqualTo(compared), "Every fixture that reaches 30 is a lawful schema-30 season.");
        }

        /// <summary>Fresh seasons the game starts (ShippedRules.ApplyFresh), at their retained 30: lawful schema-30 seasons to both.</summary>
        [TestCase(8, 1u)]
        [TestCase(12, 4242u)]
        public void FreshSeasonsTheGameStartsAreAcceptedByBoth(int houseSize, uint seed)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize }, seed);
            ShippedRules.ApplyFresh(s);
            // Held at talk rules 0 by name: level 1 (enrolled with free talk slice 1) is newer than every older schema's reader,
            // and the exact schema-31 inverse never erases a level that is on.
            Assert.That(s.story.talkRules, Is.EqualTo(TalkRules.Chemistry), "Level 2 since free talk slice 2's enrolment.");
            s.story.talkRules = TalkRules.Off;
            var payload = Payload(s);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(31));
            PersistenceMigrationTests.StripSchema31(payload); // The retained 30: talk rules 0, every talk field at its default.
            Assert.That(Agree(payload, "fresh-" + houseSize + "-" + seed), Is.Null);
        }

        private static IEnumerable<TestCaseData> Mutations()
        {
            const string story11 = "s30-mode2-allweek-story11-tuning1-w3-veto-meeting";
            yield return new TestCaseData(story11, "story-rules-12", "accept");
            yield return new TestCaseData(story11, "story-rules-13", "refuse");
            yield return new TestCaseData(story11, "story-rules-14", "refuse");
            yield return new TestCaseData(story11, "story-rules-15", "either");
            yield return new TestCaseData(story11, "tell-key", "refuse");
            yield return new TestCaseData(story11, "tell-key-at-12", "accept");
            yield return new TestCaseData(story11, "holder-offer-id", "refuse");
            yield return new TestCaseData(story11, "tuning-2", "refuse");
            yield return new TestCaseData(story11, "study-2", "refuse");
            yield return new TestCaseData("s30-mode2-allweek-w3-campaign", "alumni-7", "refuse");
            yield return new TestCaseData("s30-mode2-allweek-w3-campaign", "edition-with-rules-off", "refuse");
            yield return new TestCaseData("s30-mode2-allweek-w3-campaign", "study-6", "refuse");
            yield return new TestCaseData("s30-mode2-allstars-alumni5-w3-campaign", "alumni-receipt-words", "refuse");
            yield return new TestCaseData("s30-mode2-allstars-alumni1-w3-campaign", "unknown-edition", "refuse");
            yield return new TestCaseData("s30-mode2-allweek-story11-competition5-tuning1-w1-veto-meeting", "competition-6", "refuse");
            yield return new TestCaseData("s30-mode2-allweek-story11-competition5-tuning1-w1-veto-meeting", "competition-3", "refuse");
            yield return new TestCaseData("s30-mode2-dialogue2-tuning1-w1-campaign", "dialogue-rules-3", "refuse");
            yield return new TestCaseData("s30-mode2-dialogue2-tuning1-w1-campaign", "response-key", "refuse");
            yield return new TestCaseData("s30-mode2-dialogue2-tuning1-w1-campaign", "entry-grant", "refuse");
            yield return new TestCaseData("s30-mode2-dialogue1-tuning1-w1-campaign", "response-key", "refuse");
        }

        [TestCaseSource(nameof(Mutations))]
        public void MutationsOfSchemaThirtySeasonsGetTheSameVerdictFromTheFrozenReaderAndTheLiveValidator(string season,
            string mutation, string expected)
        {
            var payload = Fixture(season);
            Assert.That(Agree(payload, season), Is.Null, "The control is lawful.");
            var mutated = (JObject)payload.DeepClone();
            Mutate(mutated, mutation);
            string verdict = Agree(mutated, season + "/" + mutation);
            if (expected == "accept") Assert.That(verdict, Is.Null, season + "/" + mutation);
            else if (expected == "refuse") Assert.That(verdict, Is.Not.Null, season + "/" + mutation + " is not a lawful schema-30 season.");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Both verdicts on a detached copy each: null for accepted, the refusal otherwise. Both accept or both refuse,
        /// and a refusal from the validators themselves (not a shape check, whose wording each reader owns) says the
        /// same thing. The input is never changed.
        /// </summary>
        private static string Agree(JObject payload, string label)
        {
            var before = new TokenImage(payload);
            string frozen = FrozenVerdict((JObject)payload.DeepClone());
            string live = Live(PersistenceMigrationTests.Lift31(payload));
            before.AssertUnchanged(payload);
            Assert.That(frozen == null, Is.EqualTo(live == null), label + ": frozen '" + frozen + "', live '" + live + "'.");
            if (frozen != null && !live.StartsWith("shape: ", StringComparison.Ordinal) && !IsFrozenShape(frozen))
                Assert.That(frozen, Is.EqualTo(live), label + ": the same refusal.");
            return frozen;
        }

        private static bool IsFrozenShape(string message) =>
            message.Contains(" contains missing or unknown schema fields.") || message.Contains(" must be ")
            || message.Contains(" cannot be null.") || message.StartsWith("Frozen30 component", StringComparison.Ordinal)
            || message.Contains(" missing or too deeply nested.") || message.Contains(" cannot represent its numeric token.");

        private static string FrozenVerdict(JObject payload)
        {
            try { Frozen(30, payload); return null; }
            catch (InvalidDataException error) { return error.Message; }
        }

        /// <summary>The live path TryRead takes after the dispatch: the live shape, the live DTO and the live validation.</summary>
        private static string Live(JObject payload)
        {
            var saveJson = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
            var serializer = (JsonSerializer)saveJson.GetMethod("Serializer").Invoke(null, null);
            try
            {
                saveJson.GetMethod("CheckDtoShape").Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidDataException)
            {
                return "shape: " + error.InnerException.Message;
            }
            try
            {
                EpisodeSaveValidation.Validate(payload.ToObject<EpisodeState>(serializer));
                return null;
            }
            catch (InvalidDataException error) { return error.Message; }
            catch (JsonException error) { return "shape: " + error.Message; }
        }

        private static void Defect(JObject bad, string defect)
        {
            switch (defect)
            {
                case "missing-field": bad.Remove("tuningRulesVersion"); break;
                case "unknown-field": ((JObject)bad["dialogue"]).Add("futureWeeklyCap", 1); break;
                case "player-edge": AddRecord(bad, PlayerEdge(bad, true), "npc-conversation", (int)bad["week"]); break;
                case "dialogue-rules-3": bad["dialogueRulesVersion"] = 3; break;
                case "story-rules-13": bad["story"]["rulesVersion"] = 13; break;
                default: throw new ArgumentException("Unknown defect " + defect);
            }
        }

        private static void Mutate(JObject s, string mutation)
        {
            string npc = ((JArray)s["contestants"]).Cast<JObject>().Where(c => !(bool)c["isPlayer"]).Select(c => (string)c["id"]).First();
            switch (mutation)
            {
                case "story-rules-12": s["story"]["rulesVersion"] = 12; break;
                case "story-rules-13": s["story"]["rulesVersion"] = 13; break;
                case "story-rules-14": s["story"]["rulesVersion"] = 14; break;
                case "story-rules-15": s["story"]["rulesVersion"] = 15; break;
                case "tell-key":
                    ((JArray)s["story"]["cooldowns"]).Add(new JObject { ["key"] = "tell:" + npc + ":hedged", ["untilWeek"] = 100 });
                    break;
                case "tell-key-at-12":
                    s["story"]["rulesVersion"] = 12;
                    ((JArray)s["story"]["cooldowns"]).Add(new JObject { ["key"] = "tell:" + npc + ":hedged", ["untilWeek"] = 100 });
                    break;
                case "holder-offer-id":
                {
                    var ask = ((JArray)s["deals"]).Cast<JObject>().Single(d => ((string)d["id"]).StartsWith("deal-veto-", StringComparison.Ordinal));
                    ask["id"] = "deal-save-" + ((string)ask["id"]).Substring("deal-veto-".Length);
                    break;
                }
                case "tuning-2": s["tuningRulesVersion"] = 2; break;
                case "study-2": s["playerStudyBonus"] = 2; break;
                case "study-6": s["playerStudyBonus"] = 6; break;
                case "alumni-7": s["alumniRulesVersion"] = 7; break;
                case "edition-with-rules-off": s["alumniCatalogEdition"] = "allstar-histories-1"; break;
                case "unknown-edition": s["alumniCatalogEdition"] = "allstar-histories-2"; break;
                case "alumni-receipt-words":
                {
                    var receipt = Edges(s).SelectMany(edge => edge["events"]).First(e => (string)e["type"] == "alumni-history");
                    receipt["description"] = (string)receipt["description"] + " ";
                    break;
                }
                case "competition-6": s["competitionRulesVersion"] = 6; break;
                case "competition-3": s["competitionRulesVersion"] = 3; break;
                case "dialogue-rules-3": s["dialogueRulesVersion"] = 3; break;
                case "response-key":
                {
                    var key = s["dialogue"]["episodes"][0]["responses"][0]["responseKey"];
                    s["dialogue"]["episodes"][0]["responses"][0]["responseKey"] = (string)key + "-not";
                    break;
                }
                case "entry-grant": s["dialogue"]["episodes"][0]["entry"]["grantId"] = "dialogue-grant-1"; break;
                default: throw new ArgumentException("Unknown mutation " + mutation);
            }
        }

        private static JObject Fixture(string id) => (JObject)JObject.Parse(System.Text.Encoding.UTF8.GetString(FixtureBytes(id)))["state"];
        private static byte[] FixtureBytes(string id) => File.ReadAllBytes(Path.Combine(FixtureRoot, id + ".save.json"));
        private static IEnumerable<JObject> Edges(JObject payload) => ((JArray)payload["relationships"]).Cast<JObject>();
        private static JObject PlayerEdge(JObject payload, bool fromPlayer)
        {
            string player = (string)payload["playerId"];
            return Edges(payload).First(edge => fromPlayer
                ? (string)edge["fromId"] == player && (string)edge["toId"] != player
                : (string)edge["toId"] == player && (string)edge["fromId"] != player);
        }
        private static void AddRecord(JObject payload, JObject edge, string type, int week)
        {
            int sequence = (int)payload["nextSequence"];
            ((JArray)edge["events"]).Add(new JObject
            {
                ["sequence"] = sequence, ["week"] = week, ["type"] = type,
                ["description"] = "Detached historical gate control", ["impactScore"] = 25.0, ["decayable"] = true
            });
            payload["nextSequence"] = sequence + 1;
        }
        private static void Frozen(int version, JObject original)
        {
            var type = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV" + version, true);
            var method = type.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try { method.Invoke(null, new object[] { original }); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static JObject Payload(EpisodeState state)
        {
            var serializer = (JsonSerializer)typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true)
                .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            return JObject.FromObject(state, serializer);
        }

        private sealed class TokenIdentity : IEqualityComparer<JToken>
        {
            internal static readonly TokenIdentity Instance = new TokenIdentity();
            public bool Equals(JToken first, JToken second) => ReferenceEquals(first, second);
            public int GetHashCode(JToken token) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(token);
        }

        private sealed class TokenImage
        {
            private readonly string text;
            private readonly JToken[] tokens, parents, previous, next;
            internal TokenImage(JObject root)
            {
                text = root.ToString(Formatting.None);
                tokens = root.DescendantsAndSelf().ToArray();
                parents = tokens.Select(t => (JToken)t.Parent).ToArray();
                previous = tokens.Select(t => t.Previous).ToArray();
                next = tokens.Select(t => t.Next).ToArray();
            }
            internal void AssertUnchanged(JObject root)
            {
                Assert.That(root.ToString(Formatting.None), Is.EqualTo(text));
                var now = root.DescendantsAndSelf().ToArray();
                Assert.That(now.Length, Is.EqualTo(tokens.Length));
                for (int i = 0; i < now.Length; i++)
                {
                    Assert.That(now[i], Is.SameAs(tokens[i]));
                    Assert.That(now[i].Parent, Is.SameAs(parents[i]));
                    Assert.That(now[i].Previous, Is.SameAs(previous[i]));
                    Assert.That(now[i].Next, Is.SameAs(next[i]));
                }
            }
        }

        private sealed class Slot : IDisposable
        {
            private const string Prefix = "GamesimFrozen30Gate-";
            private readonly string leaf = Prefix + Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;
            internal Slot()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Guard(); Directory.CreateDirectory(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }
            internal void Write(string path, byte[] bytes)
            {
                Guard();
                Assert.That(path == Store.SavePath || path == Store.BackupPath, Is.True);
                File.WriteAllBytes(path, bytes);
            }
            internal SortedDictionary<string, byte[]> Image()
            {
                Guard();
                Assert.That(Directory.GetDirectories(Root), Is.Empty);
                return new SortedDictionary<string, byte[]>(Directory.GetFiles(Root).ToDictionary(Path.GetFileName, File.ReadAllBytes),
                    StringComparer.Ordinal);
            }
            internal void AssertImage(SortedDictionary<string, byte[]> expected)
            {
                var actual = Image();
                Assert.That(actual.Keys, Is.EqualTo(expected.Keys), "No extra pending, retained or replaced files during refusal.");
                foreach (var pair in expected) Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
            private void Guard()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(Root), Is.EqualTo(temp));
                Assert.That(Path.GetFileName(Root), Is.EqualTo(leaf));
                Assert.That(leaf, Does.StartWith(Prefix));
                if (!Directory.Exists(Root)) return;
                Assert.That(File.GetAttributes(Root) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
                Assert.That(Directory.GetDirectories(Root), Is.Empty);
                foreach (string file in Directory.GetFiles(Root))
                    Assert.That(File.GetAttributes(file) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
            }
            public void Dispose()
            {
                Guard();
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
