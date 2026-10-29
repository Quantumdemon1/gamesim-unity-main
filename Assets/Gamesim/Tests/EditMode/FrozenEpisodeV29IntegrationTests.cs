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
    /// SCH30 step 1 (ALL-STAR-HISTORIES-PLAN 8.3), at the live schema 29: the frozen schema-29 whole reader on the real
    /// save paths, and its parity with the live validator. Reflection reaches the internal reader; direct calls prove
    /// the dispatch wiring (a malformed 29 is refused by PrepareCurrentPayload itself, so the case fails if the call is
    /// removed); real temporary slots prove load, save, rotation and reload. The parity matrix holds the reader to the
    /// live validator: every retained fixture as the chain hands it on at 29, seasons the engine plays at dialogue rules
    /// 1 and under the role moments (story rules Roles), and mutations of each - both accept or both refuse, and a
    /// semantic refusal says the same thing. Retained fixtures are read-only; every edit is a detached JSON control.
    /// </summary>
    public sealed class FrozenEpisodeV29IntegrationTests
    {
        private const string EdgeReason = "The all-week rules' records are only ever between two houseguests.";
        private static readonly string[] Current29 =
        {
            "s29-default-w2-social", "s29-mode1-allweek-w3-veto-meeting", "s29-mode2-allweek-w3-campaign"
        };
        private static string FixtureRoot => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");

        // ------------------------------------------------------------------ the dispatch

        [TestCaseSource(nameof(Current29))]
        public void DirectTwentyNineDispatchRunsTheFrozenReaderAndReturnsADetachedCopy(string id)
        {
            var payload = Fixture(id);
            var before = new TokenImage(payload);
            Frozen(29, payload);
            var result = EpisodeSaveMigrations.PrepareCurrentPayload(payload, out bool migrated);
            Assert.That(migrated, Is.False, "A direct 29 is the live schema: it is held to its frozen reader, not migrated.");
            Assert.That(result, Is.Not.SameAs(payload));
            Assert.That(result.ToString(Formatting.None), Is.EqualTo(payload.ToString(Formatting.None)));
            var originals = new HashSet<JToken>(payload.DescendantsAndSelf(), TokenIdentity.Instance);
            foreach (var copy in result.DescendantsAndSelf())
                Assert.That(originals.Contains(copy), Is.False, "No returned token aliases any retained input token.");
            ((JArray)result["dialogue"]["episodes"]).Add("caller mutation");
            before.AssertUnchanged(payload);
        }

        [TestCase("s29-default-w2-social", "missing-field")]
        [TestCase("s29-default-w2-social", "unknown-field")]
        [TestCase("s29-default-w2-social", "dialogue-rules-2")]
        [TestCase("s29-default-w2-social", "story-rules-10")]
        [TestCase("s29-mode1-allweek-w3-veto-meeting", "missing-field")]
        [TestCase("s29-mode1-allweek-w3-veto-meeting", "unknown-field")]
        [TestCase("s29-mode1-allweek-w3-veto-meeting", "player-edge")]
        [TestCase("s29-mode1-allweek-w3-veto-meeting", "conditional-at-rules-0")]
        [TestCase("s29-mode2-allweek-w3-campaign", "missing-field")]
        [TestCase("s29-mode2-allweek-w3-campaign", "unknown-field")]
        [TestCase("s29-mode2-allweek-w3-campaign", "player-edge")]
        [TestCase("s29-mode2-allweek-w3-campaign", "dialogue-rules-2")]
        [TestCase("s29-mode2-allweek-w3-campaign", "story-rules-10")]
        public void MalformedCurrent29IsRefusedByDispatchItselfBeforeAnyLiveDtoHydration(string id, string defect)
        {
            var control = Fixture(id);
            Frozen(29, control);
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.PrepareCurrentPayload(control, out _));
            var bad = (JObject)control.DeepClone();
            Defect(bad, defect);
            var before = new TokenImage(bad);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var frozen = Assert.Throws<InvalidDataException>(() => Frozen(29, bad));
                bool migrated = true;
                var dispatch = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out migrated));
                Assert.That(migrated, Is.False);
                Assert.That(dispatch.Message, Is.EqualTo(frozen.Message));
                if (defect == "player-edge") Assert.That(dispatch.Message, Is.EqualTo(EdgeReason));
                before.AssertUnchanged(bad);
            }
        }

        [TestCase("s28-mode1-allweek-w2-campaign")]
        [TestCase("s28-mode1-finished")]
        [TestCase("s28-mode2-allweek-w3-campaign")]
        [TestCase("s28-mode2-finished")]
        [TestCase("s27-mode1-w3-social")]
        [TestCase("s27-default-w2-social")]
        public void EveryOlderChainEndsAtTheFrozenTwentyNineGateAndLeavesItsInputUnmodified(string id)
        {
            var original = Fixture(id);
            var before = new TokenImage(original);
            var expected = EpisodeSaveMigrations.UpgradeV28ToV29(Chain28(original));
            Frozen(29, expected);
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(original, out bool migrated);
            Assert.That(migrated, Is.True);
            Assert.That(current, Is.Not.SameAs(original));
            Assert.That(current.ToString(Formatting.None), Is.EqualTo(expected.ToString(Formatting.None)));
            Assert.That((int)current["schemaVersion"], Is.EqualTo(29));
            Assert.That((int)current["dialogueRulesVersion"], Is.Zero);
            before.AssertUnchanged(original);
        }

        /// <summary>
        /// A schema-28 payload its own reader accepts can still be refused at the end of the chain: a storyline on the
        /// dialogue pilot's campaign template is nothing to Frozen28, but at 29 an off dialogue store must hold no
        /// campaign content. The refusal comes from the frozen 29 gate PrepareCurrentPayload runs on the chain's end.
        /// </summary>
        [Test]
        public void ATwentyEightPayloadItsOwnReaderAcceptsStillFacesTheFinalTwentyNineGate()
        {
            var control = Fixture("s28-mode1-leaks-w2-eviction");
            Frozen(28, control);
            var bad = (JObject)control.DeepClone();
            var storyline = (JObject)((JArray)bad["storylines"]).First().DeepClone();
            storyline["id"] = "story-" + ((int)bad["nextSequence"]).ToString(CultureInfo.InvariantCulture);
            storyline["templateId"] = DialogueCampaignV1.TemplateId;
            ((JArray)bad["storylines"]).Add(storyline);
            bad["nextSequence"] = (int)bad["nextSequence"] + 1;
            Assert.DoesNotThrow(() => Frozen(28, bad), "Schema 28 never knew the dialogue store: its own reader accepts this.");
            var before = new TokenImage(bad);
            var stepped = EpisodeSaveMigrations.UpgradeV28ToV29(bad);
            var frozen = Assert.Throws<InvalidDataException>(() => Frozen(29, stepped));
            Assert.That(frozen.Message, Is.EqualTo("Disabled dialogue storage must remain complete and inert."));
            var error = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out _));
            Assert.That(error.Message, Is.EqualTo(frozen.Message), "Proves the final gate, not only the direct-29 dispatch.");
            Assert.That(Live(stepped), Is.EqualTo(frozen.Message), "The live validator gives the chain's end the same verdict.");
            before.AssertUnchanged(bad);
        }

        [TestCaseSource(nameof(Current29))]
        public void RealSlotsLoadSaveRotateAndReloadTheSchemaTwentyNineWriterFixtures(string id)
        {
            byte[] source = FixtureBytes(id);
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, source);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"), "A direct 29 is not migrated.");
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(source), "Loading is read-only.");
            var loadedImage = Payload(loaded);
            Assert.That(JToken.DeepEquals(loadedImage, Fixture(id)), Is.True, "The store hands on the writer's state as it is.");
            Frozen(29, loadedImage);
            slot.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(source), "Rotation retains the exact writer envelope.");
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(again), loadedImage), Is.True);
            Assert.That(File.ReadAllBytes(Path.Combine(FixtureRoot, id + ".save.json")), Is.EqualTo(source));
        }

        [Test]
        public void AFrozenRefusalOfADirectTwentyNineBlocksTheLoadAndLeavesBothCopies()
        {
            const string id = "s29-mode2-allweek-w3-campaign";
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
        /// Every retained fixture, as the chain hands it on at 29 (a direct 29 as it is): the frozen reader and the live
        /// validator give it the same verdict. A fixture refused before 29 (by an older reader) has no 29 to compare.
        /// </summary>
        [Test]
        public void EveryRetainedFixtureGetsTheSameVerdictFromTheFrozenReaderAndTheLiveValidator()
        {
            int compared = 0, accepted = 0;
            var refusedEarlier = new List<string>();
            foreach (string id in AllFixtures())
            {
                var original = Fixture(id);
                JObject at29;
                try { at29 = (int)original["schemaVersion"] == 29 ? original : EpisodeSaveMigrations.UpgradeV28ToV29(Chain28(original)); }
                catch (InvalidDataException) { refusedEarlier.Add(id); continue; }
                string verdict = Agree(at29, id);
                compared++;
                if (verdict == null) accepted++;
            }
            TestContext.Out.WriteLine("Compared " + compared + " fixtures at 29, " + accepted + " accepted by both; refused before 29: "
                + string.Join(", ", refusedEarlier));
            Assert.That(compared, Is.GreaterThanOrEqualTo(50), "The manifest's fixtures, not a quietly empty folder.");
            Assert.That(accepted, Is.EqualTo(compared), "Every fixture that reaches 29 is a lawful schema-29 season.");
        }

        private static IEnumerable<string> BuiltSeasons() => new[]
        {
            "dialogue-case", "dialogue-finish-personal", "dialogue-answered", "dialogue-neutral", "dialogue-lapsed",
            "roles-fresh", "roles-pitch-board", "roles-keep-the-block-asked", "roles-keep-the-block-kept",
        };

        [TestCaseSource(nameof(BuiltSeasons))]
        public void SeasonsTheEnginePlaysAtDialogueRulesOneAndUnderTheRoleMomentsAreAcceptedByBoth(string season)
        {
            var payload = Payload(Built(season));
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(29));
            if (season.StartsWith("dialogue-", StringComparison.Ordinal))
            {
                Assert.That((int)payload["dialogueRulesVersion"], Is.EqualTo(1));
                Assert.That(((JArray)payload["dialogue"]["episodes"]).Count, Is.EqualTo(1), "A rules-1 store holding its conversation.");
            }
            else Assert.That((int)payload["story"]["rulesVersion"], Is.EqualTo(StoryRules.Roles));
            Assert.That(Agree(payload, season), Is.Null, season + " is a lawful schema-29 season.");
            var before = new TokenImage(payload);
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.PrepareCurrentPayload(payload, out _));
            before.AssertUnchanged(payload);
        }

        private static IEnumerable<TestCaseData> Mutations()
        {
            // (season, mutation, expected: true accepted / false refused)
            foreach (string season in new[] { "s29-default-w2-social", "s29-mode2-allweek-w3-campaign", "dialogue-finish-personal",
                "roles-keep-the-block-kept" })
            {
                yield return new TestCaseData(season, "alumni-history-npc", true);
                yield return new TestCaseData(season, "alumni-history-player", true);
                yield return new TestCaseData(season, "dialogue-rules-2", false);
                yield return new TestCaseData(season, "dialogue-rules-3", false);
                yield return new TestCaseData(season, "dialogue-rules-minus-1", false);
                yield return new TestCaseData(season, "story-rules-10", false);
                yield return new TestCaseData(season, "story-rules-12", false);
                yield return new TestCaseData(season, "conditional-veto", false);
                yield return new TestCaseData(season, "grant", false);
                yield return new TestCaseData(season, "veto-occasion", false);
                yield return new TestCaseData(season, "missing-dialogue", false);
                yield return new TestCaseData(season, "null-dialogue", false);
                yield return new TestCaseData(season, "unknown-dialogue-field", false);
                yield return new TestCaseData(season, "player-edge", false);
            }
            foreach (string season in new[] { "s29-default-w2-social", "s29-mode1-allweek-w3-veto-meeting", "s29-mode2-allweek-w3-campaign" })
                yield return new TestCaseData(season, "episode-at-rules-0", false);
            // Seasons that hold a storyline to copy, and a deal.
            foreach (string season in new[] { "s29-default-w2-social", "s29-mode2-allweek-w3-campaign" })
                yield return new TestCaseData(season, "campaign-storyline-at-rules-0", false);
            yield return new TestCaseData("s29-mode2-allweek-w3-campaign", "veto-hold-outside-roles", false);
            yield return new TestCaseData("s29-mode2-allweek-w3-campaign", "keep-id-outside-roles", false);
            foreach (string season in new[] { "dialogue-case", "dialogue-answered", "dialogue-lapsed" })
            {
                yield return new TestCaseData(season, "dialogue-rules-0-with-store", false);
                yield return new TestCaseData(season, "episode-content-version-3", false);
                yield return new TestCaseData(season, "statement", false);
                yield return new TestCaseData(season, "time-delta", false);
                yield return new TestCaseData(season, "response-key", false);
                yield return new TestCaseData(season, "entry-grant", false);
            }
            foreach (string season in new[] { "roles-pitch-board", "roles-keep-the-block-asked", "roles-keep-the-block-kept" })
            {
                yield return new TestCaseData(season, "story-rules-threads", false);
                yield return new TestCaseData(season, "story-rules-10", false);
            }
            yield return new TestCaseData("roles-pitch-board", "pitch-impact", false);
            yield return new TestCaseData("roles-pitch-board", "fourth-pitcher", false);
        }

        [TestCaseSource(nameof(Mutations))]
        public void MutationsOfSchemaTwentyNineSeasonsGetTheSameVerdictFromTheFrozenReaderAndTheLiveValidator(string season,
            string mutation, bool accepted)
        {
            var payload = season.StartsWith("s29-", StringComparison.Ordinal) ? Fixture(season) : Payload(Built(season));
            Assert.That(Agree(payload, season), Is.Null, "The control is lawful.");
            var mutated = (JObject)payload.DeepClone();
            Mutate(mutated, mutation);
            string verdict = Agree(mutated, season + "/" + mutation);
            if (accepted) Assert.That(verdict, Is.Null, season + "/" + mutation);
            else Assert.That(verdict, Is.Not.Null, season + "/" + mutation + " is not a lawful schema-29 season.");
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
            string live = Live((JObject)payload.DeepClone());
            before.AssertUnchanged(payload);
            Assert.That(frozen == null, Is.EqualTo(live == null), label + ": frozen '" + frozen + "', live '" + live + "'.");
            if (frozen != null && !live.StartsWith("shape: ", StringComparison.Ordinal) && !IsFrozenShape(frozen))
                Assert.That(frozen, Is.EqualTo(live), label + ": the same refusal.");
            return frozen;
        }

        private static bool IsFrozenShape(string message) =>
            message.Contains(" contains missing or unknown schema fields.") || message.Contains(" must be ")
            || message.Contains(" cannot be null.") || message.StartsWith("Frozen29 component", StringComparison.Ordinal)
            || message.Contains(" missing or too deeply nested.") || message.Contains(" cannot represent its numeric token.");

        private static string FrozenVerdict(JObject payload)
        {
            try { Frozen(29, payload); return null; }
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

        private static JObject Chain28(JObject original)
        {
            int version = (int)original["schemaVersion"];
            if (version == 28) return original;
            return EpisodeSaveMigrations.UpgradeV27ToV28(version == 27 ? original : EpisodeSaveMigrations.PrepareV27Payload(original, out _));
        }

        private static void Defect(JObject bad, string defect)
        {
            switch (defect)
            {
                case "missing-field": bad.Remove("dialogueRulesVersion"); break;
                case "unknown-field": ((JObject)bad["dialogue"]).Add("futureWeeklyCap", 1); break;
                case "player-edge": AddRecord(bad, PlayerEdge(bad, true), "npc-conversation", (int)bad["week"]); break;
                case "dialogue-rules-2": bad["dialogueRulesVersion"] = 2; break;
                case "story-rules-10": bad["story"]["rulesVersion"] = 10; break;
                case "conditional-at-rules-0":
                    var row = ((JArray)bad["unifiedCommitments"]).Cast<JObject>().First();
                    row["conditionalVeto"] = Terms();
                    break;
                default: throw new ArgumentException("Unknown defect " + defect);
            }
        }

        private static JObject Terms() => new JObject
        {
            ["statementId"] = "dialogue-statement-1", ["consentEventId"] = "house-event-1", ["consentPathIndex"] = 1,
            ["consentRevision"] = 1, ["resolution"] = JValue.CreateNull()
        };

        private static void Mutate(JObject s, string mutation)
        {
            string player = (string)s["playerId"];
            switch (mutation)
            {
                case "alumni-history-npc":
                case "alumni-history-player":
                {
                    bool playerEdge = mutation.EndsWith("player", StringComparison.Ordinal);
                    var edge = Edges(s).First(row => playerEdge == ((string)row["fromId"] == player || (string)row["toId"] == player));
                    int next = (int)s["nextSequence"];
                    ((JArray)edge["events"]).Add(new JObject
                    {
                        ["sequence"] = next, ["week"] = 1, ["type"] = "alumni-history",
                        ["description"] = "Played together before.", ["impactScore"] = 6.0, ["decayable"] = false
                    });
                    s["nextSequence"] = next + 1;
                    break;
                }
                case "dialogue-rules-2": s["dialogueRulesVersion"] = 2; break;
                case "dialogue-rules-3": s["dialogueRulesVersion"] = 3; break;
                case "dialogue-rules-minus-1": s["dialogueRulesVersion"] = -1; break;
                case "dialogue-rules-0-with-store": s["dialogueRulesVersion"] = 0; break;
                case "story-rules-10": s["story"]["rulesVersion"] = 10; break;
                case "story-rules-12": s["story"]["rulesVersion"] = 12; break;
                case "story-rules-threads": s["story"]["rulesVersion"] = StoryRules.Threads; break;
                case "conditional-veto":
                {
                    var rows = ((JArray)s["unifiedCommitments"]).Cast<JObject>().ToArray();
                    if (rows.Length == 0) { s["dialogue"]["grants"] = new JArray(new JObject()); break; }
                    rows[0]["conditionalVeto"] = Terms();
                    break;
                }
                case "grant":
                    ((JArray)s["dialogue"]["grants"]).Add(new JObject
                    {
                        ["id"] = "dialogue-grant-1", ["ownerId"] = player, ["role"] = "nominee", ["sourceKind"] = "nomination",
                        ["status"] = "available", ["consumedEpisodeId"] = null, ["expiryReason"] = null, ["week"] = (int)s["week"],
                        ["sourceRevision"] = 1, ["sourceSequence"] = 1, ["consumedRevision"] = 0, ["expiredRevision"] = 0,
                        ["sourcePhase"] = 2, ["sourceNominees"] = new JArray(player)
                    });
                    break;
                case "veto-occasion":
                    ((JArray)s["dialogue"]["vetoOccasions"]).Add(new JObject
                    {
                        ["week"] = (int)s["week"], ["resultRevision"] = 1, ["resultSequence"] = 1, ["hohId"] = player,
                        ["holderId"] = player, ["participants"] = new JArray(), ["nomineesAtResult"] = new JArray(),
                        ["decision"] = null
                    });
                    break;
                case "missing-dialogue": s.Remove("dialogue"); break;
                case "null-dialogue": s["dialogue"] = JValue.CreateNull(); break;
                case "unknown-dialogue-field": ((JObject)s["dialogue"]).Add("weeklyCap", 1); break;
                case "player-edge": AddRecord(s, PlayerEdge(s, true), "veto-save", (int)s["week"]); break;
                case "campaign-storyline-at-rules-0":
                {
                    var line = (JObject)((JArray)s["storylines"]).FirstOrDefault()?.DeepClone() ?? throw new InvalidOperationException("No storyline to copy.");
                    line["id"] = "story-" + ((int)s["nextSequence"]).ToString(CultureInfo.InvariantCulture);
                    line["templateId"] = DialogueCampaignV2.TemplateId;
                    ((JArray)s["storylines"]).Add(line);
                    s["nextSequence"] = (int)s["nextSequence"] + 1;
                    break;
                }
                case "episode-at-rules-0":
                    ((JArray)s["dialogue"]["episodes"]).Add(new JObject
                    {
                        ["id"] = "story-1", ["npcId"] = null, ["occasionKind"] = "campaign", ["occasionId"] = null,
                        ["entryEventId"] = null, ["contentVersion"] = 1, ["week"] = 1, ["window"] = 2, ["startedRevision"] = 1,
                        ["startedSequence"] = 1, ["startedPhase"] = 6, ["entry"] = null, ["responses"] = new JArray(),
                        ["evidence"] = new JArray(), ["statement"] = null, ["time"] = null
                    });
                    break;
                case "veto-hold-outside-roles":
                case "keep-id-outside-roles":
                {
                    var deals = (JArray)s["deals"];
                    var deal = (JObject)deals.FirstOrDefault()?.DeepClone() ?? throw new InvalidOperationException("No deal to copy.");
                    if (mutation == "veto-hold-outside-roles") deal["type"] = DealKind.VetoHold;
                    else deal["id"] = NpcDeals.KeepTheBlockPrefix + ((int)s["nextSequence"]).ToString(CultureInfo.InvariantCulture);
                    if (mutation == "veto-hold-outside-roles") deal["id"] = "deal-npc-" + ((int)s["nextSequence"]).ToString(CultureInfo.InvariantCulture);
                    deals.Add(deal);
                    s["nextSequence"] = (int)s["nextSequence"] + 1;
                    break;
                }
                case "episode-content-version-3": s["dialogue"]["episodes"][0]["contentVersion"] = 3; break;
                case "statement":
                    s["dialogue"]["episodes"][0]["statement"] = new JObject
                    {
                        ["id"] = "dialogue-statement-1", ["eventId"] = null, ["speakerId"] = null, ["targetId"] = null,
                        ["answer"] = null, ["conditionKind"] = "vote-claim", ["pathIndex"] = 0, ["week"] = 1, ["revision"] = 1,
                        ["sequence"] = 1, ["audienceIds"] = new JArray()
                    };
                    break;
                case "time-delta":
                {
                    var episode = (JObject)s["dialogue"]["episodes"][0];
                    if (episode["time"].Type == JTokenType.Null)
                        episode["time"] = new JObject
                        {
                            ["revision"] = 1, ["sequence"] = 1, ["window"] = 2, ["proposedDelta"] = 1, ["appliedDelta"] = 1,
                            ["reason"] = "answered", ["spentRevision"] = 0, ["spentSequence"] = 0, ["spentWindowAction"] = 0
                        };
                    else { episode["time"]["proposedDelta"] = 1; episode["time"]["appliedDelta"] = 1; }
                    break;
                }
                case "response-key":
                    s["dialogue"]["episodes"][0]["responses"][0]["responseKey"] = "dialogue-campaign-v1/promise-made";
                    break;
                case "entry-grant": s["dialogue"]["episodes"][0]["entry"]["grantId"] = "dialogue-grant-1"; break;
                case "pitch-impact":
                {
                    var reply = ((JArray)s["ledger"]["replies"]).Cast<JObject>().Last(row => (string)row["kind"] == ReplyCards.Pitch
                        && (string)row["replyKey"] == HoHPitches.TurnDownKey);
                    reply["toThem"] = -5.0; // The answer's own number, which the board does not move.
                    break;
                }
                case "fourth-pitcher":
                {
                    var replies = (JArray)s["ledger"]["replies"];
                    var row = (JObject)replies.Cast<JObject>().Last(r => (string)r["kind"] == ReplyCards.Pitch).DeepClone();
                    string outsider = ((JArray)s["contestants"]).Cast<JObject>().Select(c => (string)c["id"])
                        .First(id => id != player && !replies.Cast<JObject>().Any(r => (string)r["kind"] == ReplyCards.Pitch
                            && (int)r["week"] == (int)s["week"] && (string)r["fromId"] == id));
                    row["fromId"] = outsider;
                    row["cardId"] = "reply-" + ((int)s["nextSequence"]).ToString(CultureInfo.InvariantCulture);
                    replies.Add(row);
                    s["nextSequence"] = (int)s["nextSequence"] + 1;
                    break;
                }
                default: throw new ArgumentException("Unknown mutation " + mutation);
            }
        }

        private static readonly Dictionary<string, EpisodeState> BuiltCache = new Dictionary<string, EpisodeState>(StringComparer.Ordinal);

        /// <summary>Seasons the public engine plays: no state is written by hand.</summary>
        private static EpisodeState Built(string season)
        {
            if (BuiltCache.TryGetValue(season, out var held)) return held.Clone();
            if (season.StartsWith("dialogue-", StringComparison.Ordinal)) DialogueRoute();
            else if (season == "roles-fresh")
            {
                var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1111u);
                ShippedRules.ApplyFresh(s);
                s.story.rulesVersion = StoryRules.Roles; // The tests' opt-in until the enrolment (ROLE-MOMENTS-PLAN §4.2).
                BuiltCache[season] = s;
            }
            else if (season == "roles-pitch-board")
            {
                var engine = RolePitchBoardTests.Dealt(RolePitchBoardTests.Crowned(), out string[] board,
                    RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
                var s = engine.Snapshot;
                string[] keys = { HoHPitches.TurnDownKey, HoHPitches.HearKey, HoHPitches.PromiseKey };
                for (int i = 0; i < board.Length; i++)
                {
                    var card = s.replyCards.Single(c => c.kind == ReplyCards.Pitch && c.fromId == board[i]);
                    s = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Answer(s, card.id, keys[i]));
                }
                Assert.That(s.ledger.replies.Any(r => r.kind == ReplyCards.Pitch && r.replyKey == HoHPitches.TurnDownKey
                    && r.toThem != ReplyCards.Find(ReplyCards.Pitch, HoHPitches.TurnDownKey).ToThem), Is.True,
                    "A turned-down pitch on the board records its tone's cost, not the answer's own number.");
                BuiltCache[season] = s;
            }
            else
            {
                var engine = RoleKeepTheBlockTests.Board(RoleTone.Calculated, out _, out _, out _);
                var meeting = engine.Snapshot;
                var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, RoleKeepTheBlockTests.HoldOf(meeting).id, true));
                BuiltCache["roles-keep-the-block-asked"] = taken;
                var kept = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, false, null));
                Assert.That(RoleKeepTheBlockTests.HoldOf(kept).status, Is.EqualTo(DealStatus.Fulfilled));
                BuiltCache["roles-keep-the-block-kept"] = kept;
            }
            return BuiltCache[season].Clone();
        }

        /// <summary>
        /// The pilot's own observed route (DialogueCampaignV1WriterCaptureTests, seed 1, eight houseguests, the rules-1
        /// opt-in on a shipped fresh season): to week one's campaign, the old asks closed, talks until one action is left,
        /// then the conversation - the case node, the finish node, and its three endings.
        /// </summary>
        private static void DialogueRoute()
        {
            int nonce = 0;
            EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind) => new EpisodeCommand
            {
                id = "frozen29-parity-" + (++nonce).ToString(CultureInfo.InvariantCulture), actorId = state.playerId,
                expectedRevision = state.revision, expectedPhase = state.phase, kind = kind,
            };
            EpisodeState Apply(EpisodeEngine on, EpisodeCommand command)
            {
                var result = on.Apply(command);
                Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
                return result.state;
            }
            void CloseOldAsks(EpisodeEngine on)
            {
                for (int guard = 0; guard < 24; guard++)
                {
                    var s = on.Snapshot;
                    var item = s.houseEvents.FirstOrDefault(row => !row.resolved && HouseEventKind.Asks(row.kind));
                    if (item == null) return;
                    var command = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                    command.targetId = item.id;
                    if (item.IsStory) command.secondTargetId = item.choices.Single(choice => choice.optionId == item.lapseOptionId).optionId;
                    else command.text = item.choices.First().label;
                    Apply(on, command);
                }
                Assert.Fail("The old asks did not close within their budget.");
            }
            EpisodeCommand Choice(EpisodeState state, string option)
            {
                var episode = DialogueRules.Active(state);
                var command = Command(state, EpisodeCommandKind.ChooseDialogue);
                command.targetId = episode.id; command.text = DialogueRules.Cycle(state, episode).beatId;
                command.secondTargetId = option; return command;
            }
            int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);

            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1);
            ShippedRules.ApplyFresh(fresh);
            fresh.dialogueRulesVersion = 1; // The fixtures' opt-in, as D1's chip writes it.
            var engine = new EpisodeEngine(fresh);
            for (int step = 0; step < 96 && engine.Snapshot.phase != EpisodePhase.Campaign; step++)
            {
                var next = ModeTwoReaderSweep.Next(engine.Snapshot);
                next.id = "frozen29-parity-" + (++nonce).ToString(CultureInfo.InvariantCulture);
                Apply(engine, next);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(engine.Snapshot.nominees, Does.Contain(engine.Snapshot.playerId));
            CloseOldAsks(engine);
            for (int talks = 0; Remaining(engine.Snapshot) > 1 && talks < 16; talks++)
            {
                var s = engine.Snapshot;
                var talk = Command(s, EpisodeCommandKind.Talk);
                talk.targetId = s.nominees.First(id => id != s.playerId);
                Apply(engine, talk);
                CloseOldAsks(engine);
            }
            var campaign = engine.Snapshot;
            string voter = EpisodeEngine.Voters(campaign).Where(p => !p.isPlayer).Select(p => p.id)
                .FirstOrDefault(id => DialogueRules.CanStart(campaign, id, out _));
            Assert.That(voter, Is.Not.Null, "The observed route reaches a voter the pilot can open.");
            var start = Command(campaign, EpisodeCommandKind.StartDialogue);
            start.targetId = voter; start.text = DialogueCampaignV1.TemplateId; start.secondTargetId = DialogueCampaignV1.OpenCase;
            var entered = Apply(engine, start);
            BuiltCache["dialogue-case"] = entered;
            var middle = Apply(engine, Choice(entered, DialogueCampaignV1.Appeal));
            BuiltCache["dialogue-finish-personal"] = middle;
            foreach (string ending in new[] { "answered", "neutral", "lapsed" })
            {
                var branch = new EpisodeEngine(middle);
                var final = ending == "lapsed" ? Command(middle, EpisodeCommandKind.Advance)
                    : Choice(middle, ending == "answered" ? DialogueCampaignV1.Plea : DialogueCampaignV1.KeepOpen);
                BuiltCache["dialogue-" + ending] = Apply(branch, final);
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
            private const string Prefix = "GamesimFrozen29Gate-";
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
