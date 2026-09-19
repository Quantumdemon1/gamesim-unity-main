using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Frozen data-only migration and atomic storage integration regressions.</summary>
    public sealed class PersistenceMigrationTests
    {
        /// <summary>
        /// Removes the card copy — occupation, archetype and age from schema 7, hometown and bio
        /// from schema 8 — from a capture of the current runtime state, so it can stand in for a
        /// save written before those fields existed.
        ///
        /// <para>Every downgrade helper in these fixtures needs this. The stored shape is checked
        /// field for field against the type it claims to be, so a "v5 payload" that still carries
        /// schema 7's contestant fields is not a v5 payload and the frozen validator says so.</para>
        /// </summary>
        /// <summary>
        /// Removes everything schema 8 added — eviction night's stages and speeches, the backdoor
        /// plan, the out-of-phase action count, the opening beats, and the last two card fields —
        /// turning a capture of the current runtime state into a schema 7 payload.
        ///
        /// <para>Separate from <see cref="StripCardCopy"/> because the two granularities are both
        /// needed: a v7 fixture keeps occupation, archetype and age, and a v6 one does not.</para>
        /// </summary>
        /// <summary>
        /// Removes schema 12's storylines and their modifiers, turning a capture of the current
        /// runtime state into a schema 11 payload.
        /// </summary>
        public static JObject StripSchema12(JObject payload)
        {
            if (payload == null) return null;
            foreach (var field in new[] { "storylines", "activeModifiers", "storyRulesStartWeek" })
                payload.Remove(field);
            return payload;
        }

        /// <summary>
        /// Removes schema 11's bought actions and event layer, turning a schema 11 payload into a
        /// schema 10 one. Compose with <see cref="StripSchema12"/> to go down from a live capture.
        /// </summary>
        public static JObject StripSchema11(JObject payload)
        {
            if (payload == null) return null;
            foreach (var field in new[] { "boughtActionPoints", "houseEvents", "eventRulesStartWeek" })
                payload.Remove(field);
            return payload;
        }

        /// <summary>
        /// Removes schema 10's deals and their rule boundary, turning a schema 10 payload into a
        /// schema 9 one. Compose with <see cref="StripSchema11"/> to go down from a live capture.
        /// </summary>
        public static JObject StripSchema10(JObject payload)
        {
            payload?.Remove("deals");
            payload?.Remove("dealRulesStartWeek");
            return payload;
        }

        /// <summary>
        /// Removes schema 9's social-budget rule boundary, turning a schema 9 payload into a
        /// schema 8 one. Compose with <see cref="StripSchema10"/> to go down from a live capture.
        /// </summary>
        public static JObject StripSchema9(JObject payload)
        {
            payload?.Remove("socialBudgetRulesStartWeek");
            return payload;
        }

        public static JObject StripSchema8(JObject payload)
        {
            if (payload == null) return null;
            foreach (var field in new[] { "evictionStage", "evictionSpeeches", "backdoorTargetId",
                         "outOfPhaseSocialActions", "openingBeatsSeen" })
                payload.Remove(field);
            foreach (var row in payload.DescendantsAndSelf().OfType<JObject>().ToArray())
            {
                if (row.Property("stats") == null) continue;
                foreach (var field in new[] { "hometown", "bio" }) row.Remove(field);
            }
            return payload;
        }

        public static JObject StripCardCopy(JObject payload)
        {
            if (payload == null) return null;
            // Every contestant-shaped row, not just the "contestants" array. A capture taken with
            // the default serializer also carries EpisodeState's computed "Active" list, which holds
            // the same records — stripping one and not the other leaves a mismatch in a comparison
            // that reads as a real difference.
            foreach (var row in payload.DescendantsAndSelf().OfType<JObject>().ToArray())
            {
                if (row.Property("stats") == null) continue;
                foreach (var field in new[] { "occupation", "archetype", "age", "hometown", "bio" }) row.Remove(field);
            }
            return payload;
        }

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(12)]
        [TestCase(13)]
        public void Migration_IsDetachedAndPreservesEveryOriginalFieldExceptVersion(int phase)
        {
            var original = V1Fixture();
            original["phase"] = phase;
            var before = original.ToString(Formatting.None);
            var defaults = new JObject
            {
                ["juryExchanges"] = new JArray(), ["juryQuestionIndex"] = 0,
                ["finalSpeeches"] = new JArray(), ["relationshipArcs"] = new JArray()
            };
            var migrated = EpisodeSaveMigrations.UpgradeV1ToV2(original, defaults);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(2));
            Assert.That((int)migrated["phase"], Is.EqualTo(phase), "Never replay questioning for a legacy Jury save.");
            Assert.That((uint)migrated["randomState"], Is.Zero);
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
            foreach (var property in original.Properties())
                if (property.Name != "schemaVersion")
                    Assert.That(JToken.DeepEquals(property.Value, migrated[property.Name]), Is.True, property.Name);
            migrated["relationships"][0]["score"] = -100;
            ((JArray)migrated["juryExchanges"]).Add("detached default");
            Assert.That((double)original["relationships"][0]["score"], Is.EqualTo(12.5));
            Assert.That((JArray)defaults["juryExchanges"], Is.Empty);
        }

        [TestCase("root-missing")]
        [TestCase("root-unknown")]
        [TestCase("nested-missing")]
        [TestCase("nested-unknown")]
        [TestCase("future-actor-field")]
        [TestCase("nested-type")]
        [TestCase("future-phase")]
        [TestCase("future-event-phase")]
        [TestCase("future-cast-status")]
        [TestCase("future-promise-kind")]
        [TestCase("null-cast")]
        public void MalformedV1_RejectsWithoutMutatingPayload(string problem)
        {
            var original = V1Fixture();
            switch (problem)
            {
                case "root-missing": original.Property("votes").Remove(); break;
                case "root-unknown": original["unexpected"] = false; break;
                case "nested-missing": ((JObject)original["contestants"][0]["stats"]).Property("luck").Remove(); break;
                case "nested-unknown": original["relationships"][0]["events"] = new JArray(); break;
                case "future-actor-field": original["contestants"][0]["mood"] = "Angry"; break;
                case "nested-type": original["contestants"][0]["isPlayer"] = "true"; break;
                case "future-phase": original["phase"] = 14; break;
                case "future-event-phase": original["events"][0]["phase"] = 14; break;
                case "future-cast-status": original["contestants"][0]["status"] = 5; break;
                case "future-promise-kind": original["promises"][0]["kind"] = 5; break;
                case "null-cast": original["contestants"] = null; break;
            }
            var before = original.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV1ToV2(original, new JObject()));
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(99)]
        public void WrongVersion_IsNeverRelabeled(int version)
        {
            var original = V1Fixture(); original["schemaVersion"] = version;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV1ToV2(original, new JObject()));
            Assert.That((int)original["schemaVersion"], Is.EqualTo(version));
        }

        [TestCase("schemaVersion")]
        [TestCase("phase")]
        [TestCase("votes")]
        [TestCase("randomState")]
        public void Defaults_CannotOverwriteExistingHistory(string field)
        {
            var original = V1Fixture();
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV1ToV2(original, new JObject { [field] = 0 }));
        }

        [Test]
        public void Dispatch_AddsOnlyTheReviewedV2DefaultsAndDoesNotRemigrateV2()
        {
            var original = V1Fixture();
            var current = EpisodeSaveMigrations.PrepareV2Payload(original, out var migrated);
            Assert.That(migrated, Is.True);
            Assert.That(current.Properties().Select(p => p.Name).Except(original.Properties().Select(p => p.Name)),
                Is.EquivalentTo(new[] { "juryExchanges", "juryQuestionIndex", "finalSpeeches", "relationshipArcs" }));
            for (var index = 0; index < 6; index++)
            {
                Assert.That((string)current["contestants"][index]["mood"], Is.EqualTo("Neutral"));
                Assert.That((string)current["contestants"][index]["stressLevel"], Is.EqualTo("Normal"));
                foreach (var property in ((JObject)original["contestants"][index]).Properties())
                    Assert.That(JToken.DeepEquals(property.Value, current["contestants"][index][property.Name]), Is.True);
                Assert.That(original["contestants"][index]["mood"], Is.Null);
            }
            foreach (JObject edge in (JArray)current["relationships"])
            {
                Assert.That((JArray)edge["notes"], Is.Empty);
                Assert.That((JArray)edge["events"], Is.Empty);
                Assert.That((int)edge["lastInteractionWeek"], Is.Zero);
            }
            Assert.That(original["relationships"][0]["events"], Is.Null);
            var reread = EpisodeSaveMigrations.PrepareV2Payload(current, out migrated);
            Assert.That(migrated, Is.False);
            Assert.That(JToken.DeepEquals(reread, current), Is.True);
            Assert.That(ReferenceEquals(reread, current), Is.False);
        }

        [Test]
        public void Store_LoadMigratesInMemoryAndNextSaveRotatesOriginalV1Bytes()
        {
            using var fixture = new IsolatedStore();
            var original = Envelope(V1Fixture());
            File.WriteAllText(fixture.Store.SavePath, original, new UTF8Encoding(false));
            var before = File.ReadAllBytes(fixture.Store.SavePath);
            Assert.That(fixture.Store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.schemaVersion, Is.EqualTo(12));
            Assert.That(loaded.randomState, Is.Zero);
            Assert.That(File.ReadAllBytes(fixture.Store.SavePath), Is.EqualTo(before));
            Assert.That(File.Exists(fixture.Store.BackupPath), Is.False);
            fixture.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(fixture.Store.BackupPath), Is.EqualTo(before));
            Assert.That((int)JObject.Parse(File.ReadAllText(fixture.Store.SavePath))["state"]["schemaVersion"], Is.EqualTo(12));
        }

        [Test]
        public void Store_ChecksOriginalChecksumBeforeAnyMigration()
        {
            using var fixture = new IsolatedStore();
            var envelope = JObject.Parse(Envelope(V1Fixture()));
            envelope["state"]["relationships"][0]["score"] = 99;
            var damaged = envelope.ToString();
            File.WriteAllText(fixture.Store.SavePath, damaged);
            Assert.That(fixture.Store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("checksum"));
            Assert.That(File.ReadAllText(fixture.Store.SavePath), Is.EqualTo(damaged));
        }

        [Test]
        public void Store_V1BackupRecoveryPreservesRawCopiesWhileReturningCurrentState()
        {
            using var fixture = new IsolatedStore();
            var original = Envelope(V1Fixture());
            File.WriteAllText(fixture.Store.BackupPath, original, new UTF8Encoding(false));
            File.WriteAllText(fixture.Store.SavePath, "damaged primary");
            var before = File.ReadAllBytes(fixture.Store.BackupPath);
            Assert.That(fixture.Store.TryRecoverBackup(out var recovered, out var message), Is.True, message);
            Assert.That(recovered.schemaVersion, Is.EqualTo(12));
            Assert.That(File.ReadAllBytes(fixture.Store.SavePath), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(fixture.Store.BackupPath), Is.EqualTo(before));
            Assert.That(File.ReadAllText(Directory.GetFiles(fixture.DirectoryPath, "*.before-recovery-*.json").Single()),
                Is.EqualTo("damaged primary"));
        }

        [TestCase("nested-v1-extra")]
        [TestCase("incomplete-v2")]
        [TestCase("future-version")]
        public void Store_ValidChecksumDoesNotBypassVersionedShapeValidation(string problem)
        {
            using var fixture = new IsolatedStore();
            var payload = V1Fixture();
            if (problem == "nested-v1-extra") payload["relationships"][0]["events"] = new JArray();
            else payload["schemaVersion"] = problem == "incomplete-v2" ? 2 : 99;
            var original = Envelope(payload);
            File.WriteAllText(fixture.Store.SavePath, original);
            Assert.That(fixture.Store.TryLoad(out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllText(fixture.Store.SavePath), Is.EqualTo(original));
        }

        internal static string Envelope(JObject payload)
        {
            // Independent canonicalization makes the golden v1 checksum stable
            // even after the production serializer or current DTO evolves.
            JToken Order(JToken token)
            {
                if (token is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => new JProperty(p.Name, Order(p.Value))));
                if (token is JArray array) return new JArray(array.Select(Order));
                return token.DeepClone();
            }
            using var hash = SHA256.Create();
            var checksum = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Order(payload).ToString(Formatting.None))))
                .Replace("-", "").ToLowerInvariant();
            return new JObject
            {
                ["format"] = "gamesim-unity-save", ["version"] = 1,
                ["savedAt"] = "2026-09-10T00:00:00.0000000Z", ["checksum"] = checksum,
                ["state"] = payload.DeepClone()
            }.ToString(Formatting.Indented);
        }

        [TestCase(12)]
        [TestCase(13)]
        public void LegacyJuryAndFinished_LoadWithoutReopeningQuestioningOrRewritingAwards(int phase)
        {
            using var fixture = new IsolatedStore();
            var payload = V1Fixture();
            payload["phase"] = phase;
            payload["week"] = 4;
            for (var index = 0; index < 6; index++)
                payload["contestants"][index]["status"] = index >= 2 ? 2 : phase == 13 ? (index == 0 ? 3 : 4) : 0;
            if (phase == 13)
            {
                payload["winnerId"] = "fixture-0";
                payload["runnerUpId"] = "fixture-1";
            }
            var original = Envelope(payload);
            File.WriteAllText(fixture.Store.SavePath, original);
            Assert.That(fixture.Store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.schemaVersion, Is.EqualTo(12));
            Assert.That((int)loaded.phase, Is.EqualTo(phase));
            Assert.That(loaded.juryExchanges, Is.Empty);
            Assert.That(loaded.finalSpeeches, Is.Empty);
            Assert.That(loaded.juryQuestionIndex, Is.Zero);
            Assert.That(loaded.randomState, Is.Zero);
            Assert.That(loaded.revision, Is.Zero);
            Assert.That(loaded.nextSequence, Is.EqualTo(2));
            Assert.That(loaded.winnerId, Is.EqualTo((string)payload["winnerId"]));
            Assert.That(loaded.runnerUpId, Is.EqualTo((string)payload["runnerUpId"]));
            Assert.That(message, Does.Contain("migrated").And.Contain("in memory"));
            Assert.That(File.ReadAllText(fixture.Store.SavePath), Is.EqualTo(original));
        }

        [TestCase("mood")]
        [TestCase("stress")]
        [TestCase("null-history")]
        [TestCase("future-interaction-week")]
        [TestCase("future-event-sequence")]
        [TestCase("forged-arc-summary")]
        [TestCase("premature-speech")]
        [TestCase("question-index")]
        public void V2_SemanticallyInvalidHistoryIsRejectedDespiteValidChecksum(string problem)
        {
            using var fixture = new IsolatedStore();
            var payload = EpisodeSaveMigrations.PrepareV2Payload(V1Fixture(), out _);
            switch (problem)
            {
                case "mood": payload["contestants"][0]["mood"] = "Unsupported"; break;
                case "stress": payload["contestants"][0]["stressLevel"] = "Unsupported"; break;
                case "null-history": payload["relationships"][0]["events"] = null; break;
                case "future-interaction-week": payload["relationships"][0]["lastInteractionWeek"] = 2; break;
                case "future-event-sequence":
                    ((JArray)payload["relationships"][0]["events"]).Add(new JObject
                    {
                        ["sequence"] = 2, ["week"] = 1, ["type"] = "general", ["description"] = "Uncommitted future event",
                        ["impactScore"] = 5, ["decayable"] = true
                    });
                    break;
                case "forged-arc-summary":
                    ((JArray)payload["relationshipArcs"]).Add(new JObject
                    {
                        ["npcId"] = "fixture-1", ["npcName"] = "Fixture 1", ["arcType"] = "friendship",
                        ["intensity"] = 90, ["escalationLevel"] = 4, ["weeklyHistory"] = new JArray()
                    });
                    break;
                case "premature-speech":
                    ((JArray)payload["finalSpeeches"]).Add(new JObject
                    {
                        ["speakerId"] = "fixture-0", ["text"] = "An early speech", ["isPlayerAuthored"] = true
                    });
                    break;
                case "question-index": payload["juryQuestionIndex"] = 1; break;
            }
            var original = Envelope(payload);
            File.WriteAllText(fixture.Store.SavePath, original);
            Assert.That(fixture.Store.TryLoad(out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllText(fixture.Store.SavePath), Is.EqualTo(original));
        }

        private sealed class IsolatedStore : IDisposable
        {
            public readonly string DirectoryPath;
            public readonly EpisodeSaveStore Store;
            public IsolatedStore()
            {
                DirectoryPath = Path.Combine(Path.GetTempPath(), "GamesimMigrationTests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DirectoryPath);
                Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json"));
            }
            public void Dispose()
            {
                var resolved = Path.GetFullPath(DirectoryPath);
                var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimMigrationTests-"));
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }

        // This fixture is deliberately independent of the evolving ContentCatalog,
        // EpisodeState constructor, current JSON serializer and current phase enum.
        // It represents the pinned native v1 shape, not a web save or user data.
        internal static JObject V1Fixture()
        {
            var state = JObject.Parse(@"{
                'schemaVersion':1,'sessionId':'native-v1-fixture','seed':0,'randomState':0,
                'revision':0,'week':1,'nextSequence':2,'socialActions':0,'phase':0,
                'playerId':'fixture-0','hohId':null,'previousHohId':null,'vetoHolderId':null,
                'winnerId':null,'runnerUpId':null,'finalPart1WinnerId':null,'finalPart2WinnerId':null,
                'competitionResolved':false,'vetoResolved':false,'evictionResolved':false,
                'contestants':[],'relationships':[],
                'promises':[{'id':'promise-fixture','fromId':'fixture-0','toId':'fixture-1','targetId':null,
                    'kind':0,'status':0,'week':1,'expiresWeek':2,'impact':'medium'}],
                'alliances':[{'id':'alliance-fixture','name':'Fixture Pact','members':['fixture-0','fixture-1'],'active':true}],
                'memories':[{'ownerId':'fixture-0','subjectId':'fixture-1','text':'Fixture memory','week':1,'isPrivate':true}],
                'nominees':[],'vetoPlayers':[],'votes':[],'competitionScores':[],
                'events':[{'sequence':1,'week':1,'phase':0,'kind':'fixture','text':'Fixture start','audienceIds':[]}],
                'acceptedCommandIds':[]
            }");
            for (var index = 0; index < 6; index++)
            {
                ((JArray)state["contestants"]).Add(new JObject
                {
                    ["id"] = "fixture-" + index, ["name"] = "Fixture " + index,
                    ["pronouns"] = "they/them", ["motive"] = "Play the game", ["homeRoom"] = "Living",
                    ["isPlayer"] = index == 0, ["status"] = 0,
                    ["stats"] = JObject.Parse("{'physical':5,'mental':5,'endurance':5,'social':5,'luck':5,'competition':5,'strategic':5,'loyalty':5}"),
                    ["traits"] = new JArray("Loyal"), ["hohWins"] = 0, ["vetoWins"] = 0,
                    ["timesNominated"] = 0, ["nominationWeeks"] = new JArray()
                });
                for (var target = 0; target < 6; target++)
                    if (target != index) ((JArray)state["relationships"]).Add(new JObject
                    {
                        ["fromId"] = "fixture-" + index, ["toId"] = "fixture-" + target,
                        ["score"] = index == 0 && target == 1 ? 12.5 : 0
                    });
            }
            return state;
        }
    }
}
