using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Gamesim.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Fixed historical JSON tests over actual retained managed26 witnesses.
    /// No current DTO, factory, Apply, migration, ordinary save or Unity API defines history.
    /// Recorded command links and disk flags are not a new command replay or disk/native run.
    /// </summary>
    public sealed class FrozenEpisodeV26ContractTests
    {
        private static readonly Type Contract = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV26", true);
        private static readonly string[] SemanticLabels = { "unknown-authority", "unknown-hearing", "commitments-off", "story-off", "bad-week", "bad-stat",
            "future-id", "future-settlement", "unknown-kind", "missing-initial", "missing-evidence", "original-party-absent",
            "null-vote-archive", "nonempty-vote-archive", "nonnull-target", "nonnull-subtype" };
        private static readonly string[] ShapeLabels = { "missing-header", "null-header", "text-header", "fraction-header", "old-header", "future-header",
            "unknown-root", "missing-archive", "object-archive", "missing-target", "missing-subtype", "unknown-row" };

        [Test]
        public void PortableInventoryKeepsOriginalProvenanceAndEveryRawMetadataByte()
        {
            var manifest = FrozenV26TestCorpus.Manifest;
            Assert.That(((JArray)manifest["files"]).Count, Is.EqualTo(471));
            Assert.That(FrozenV26TestCorpus.Aliases.Count(), Is.EqualTo(264));
            Assert.That(FrozenV26TestCorpus.Aliases.Where(a => (string)a["kind"] == "semantic-refused").Select(a => (string)a["label"]), Is.EquivalentTo(SemanticLabels));
            Assert.That(FrozenV26TestCorpus.Aliases.Where(a => (string)a["kind"] == "shape-refused").Select(a => (string)a["label"]), Is.EquivalentTo(ShapeLabels));
            var binding = FrozenV26TestCorpus.Parse(FrozenV26TestCorpus.ReadRaw("binding.json"));
            Assert.That((string)binding["currentSourceHead"], Is.EqualTo("c15eb54fe0b3428633571cc034d99ec7199efffe"));
            Assert.That((string)binding["compilerObservedSourceHead"], Is.EqualTo("a5c46909cade0abd8c403cf3e72330f6957d4055"));
            Assert.That((bool)binding["accepted"] && (bool)binding["complete"], Is.True);
            Assert.That((bool)binding["nativeEditorOrPlayerLaunched"] || (bool)binding["ordinaryUserSaveWritten"], Is.False);
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        [TestCase(19)]
        [TestCase(20)]
        [TestCase(21)]
        [TestCase(22)]
        [TestCase(23)]
        [TestCase(24)]
        [TestCase(25)]
        [TestCase(26)]
        [TestCase(27)]
        [TestCase(28)]
        [TestCase(29)]
        [TestCase(30)]
        [TestCase(31)]
        [TestCase(32)]
        [TestCase(33)]
        [TestCase(34)]
        [TestCase(35)]
        [TestCase(36)]
        [TestCase(37)]
        [TestCase(38)]
        [TestCase(39)]
        [TestCase(40)]
        [TestCase(41)]
        [TestCase(42)]
        [TestCase(43)]
        [TestCase(44)]
        [TestCase(45)]
        [TestCase(46)]
        [TestCase(47)]
        [TestCase(48)]
        [TestCase(49)]
        [TestCase(50)]
        [TestCase(51)]
        [TestCase(52)]
        [TestCase(53)]
        [TestCase(54)]
        [TestCase(55)]
        [TestCase(56)]
        [TestCase(57)]
        [TestCase(58)]
        [TestCase(59)]
        [TestCase(60)]
        [TestCase(61)]
        [TestCase(62)]
        [TestCase(63)]
        [TestCase(64)]
        [TestCase(65)]
        [TestCase(66)]
        [TestCase(67)]
        [TestCase(68)]
        [TestCase(69)]
        [TestCase(70)]
        [TestCase(71)]
        [TestCase(72)]
        [TestCase(73)]
        [TestCase(74)]
        [TestCase(75)]
        [TestCase(76)]
        [TestCase(77)]
        [TestCase(78)]
        [TestCase(79)]
        [TestCase(80)]
        [TestCase(81)]
        [TestCase(82)]
        [TestCase(83)]
        [TestCase(84)]
        [TestCase(85)]
        [TestCase(86)]
        [TestCase(87)]
        [TestCase(88)]
        [TestCase(89)]
        [TestCase(90)]
        [TestCase(91)]
        [TestCase(92)]
        [TestCase(93)]
        [TestCase(94)]
        [TestCase(95)]
        [TestCase(96)]
        [TestCase(97)]
        [TestCase(98)]
        [TestCase(99)]
        [TestCase(100)]
        [TestCase(101)]
        [TestCase(102)]
        [TestCase(103)]
        [TestCase(104)]
        [TestCase(105)]
        [TestCase(106)]
        [TestCase(107)]
        [TestCase(108)]
        [TestCase(109)]
        [TestCase(110)]
        [TestCase(111)]
        [TestCase(112)]
        [TestCase(113)]
        [TestCase(114)]
        [TestCase(115)]
        [TestCase(116)]
        [TestCase(117)]
        [TestCase(118)]
        [TestCase(119)]
        [TestCase(120)]
        [TestCase(121)]
        [TestCase(122)]
        [TestCase(123)]
        [TestCase(124)]
        [TestCase(125)]
        [TestCase(126)]
        [TestCase(127)]
        [TestCase(128)]
        [TestCase(129)]
        [TestCase(130)]
        [TestCase(131)]
        [TestCase(132)]
        [TestCase(133)]
        [TestCase(134)]
        [TestCase(135)]
        [TestCase(136)]
        [TestCase(137)]
        [TestCase(138)]
        [TestCase(139)]
        [TestCase(140)]
        [TestCase(141)]
        [TestCase(142)]
        [TestCase(143)]
        [TestCase(144)]
        [TestCase(145)]
        [TestCase(146)]
        [TestCase(147)]
        [TestCase(148)]
        [TestCase(149)]
        [TestCase(150)]
        [TestCase(151)]
        [TestCase(152)]
        [TestCase(153)]
        [TestCase(154)]
        [TestCase(155)]
        [TestCase(156)]
        [TestCase(157)]
        [TestCase(158)]
        [TestCase(159)]
        [TestCase(160)]
        [TestCase(161)]
        [TestCase(162)]
        [TestCase(163)]
        [TestCase(164)]
        [TestCase(165)]
        [TestCase(166)]
        [TestCase(167)]
        [TestCase(168)]
        [TestCase(169)]
        [TestCase(170)]
        [TestCase(171)]
        [TestCase(172)]
        [TestCase(173)]
        [TestCase(174)]
        [TestCase(175)]
        [TestCase(176)]
        [TestCase(177)]
        [TestCase(178)]
        [TestCase(179)]
        [TestCase(180)]
        [TestCase(181)]
        [TestCase(182)]
        [TestCase(183)]
        [TestCase(184)]
        [TestCase(185)]
        [TestCase(186)]
        [TestCase(187)]
        [TestCase(188)]
        [TestCase(189)]
        [TestCase(190)]
        [TestCase(191)]
        [TestCase(192)]
        [TestCase(193)]
        [TestCase(194)]
        [TestCase(195)]
        [TestCase(196)]
        [TestCase(197)]
        [TestCase(198)]
        [TestCase(199)]
        [TestCase(200)]
        [TestCase(201)]
        [TestCase(202)]
        [TestCase(203)]
        [TestCase(204)]
        [TestCase(205)]
        [TestCase(206)]
        [TestCase(207)]
        [TestCase(208)]
        [TestCase(209)]
        [TestCase(210)]
        [TestCase(211)]
        [TestCase(212)]
        [TestCase(213)]
        [TestCase(214)]
        [TestCase(215)]
        [TestCase(216)]
        [TestCase(217)]
        [TestCase(218)]
        [TestCase(219)]
        [TestCase(220)]
        [TestCase(221)]
        [TestCase(222)]
        [TestCase(223)]
        [TestCase(224)]
        [TestCase(225)]
        [TestCase(226)]
        [TestCase(227)]
        [TestCase(228)]
        [TestCase(229)]
        [TestCase(230)]
        [TestCase(231)]
        [TestCase(232)]
        [TestCase(233)]
        [TestCase(234)]
        [TestCase(235)]
        [TestCase(236)]
        [TestCase(237)]
        [TestCase(238)]
        [TestCase(239)]
        [TestCase(240)]
        [TestCase(241)]
        [TestCase(242)]
        [TestCase(243)]
        [TestCase(244)]
        [TestCase(245)]
        [TestCase(246)]
        [TestCase(247)]
        [TestCase(248)]
        [TestCase(249)]
        [TestCase(250)]
        [TestCase(251)]
        [TestCase(252)]
        [TestCase(253)]
        [TestCase(254)]
        [TestCase(255)]
        [TestCase(256)]
        [TestCase(257)]
        [TestCase(258)]
        [TestCase(259)]
        [TestCase(260)]
        [TestCase(261)]
        [TestCase(262)]
        [TestCase(263)]
        public void RetainedAliasHasItsExactIndependentDecisionAndUnchangedFieldOrder(int ordinal)
        {
            var aliases = FrozenV26TestCorpus.Aliases.ToArray();
            var alias = aliases[ordinal];
            string kind = (string)alias["kind"];
            bool refuse = kind == "semantic-refused" || kind == "shape-refused";
            var input = FrozenV26TestCorpus.Payload(alias);
            if (refuse)
            {
                string baselineSha = (string)alias["acceptedBaselineSha256"];
                Assert.That(aliases.Any(a => (string)a["payloadSha256"] == baselineSha
                    && (string)a["kind"] != "semantic-refused" && (string)a["kind"] != "shape-refused"), Is.True);
                var baseline = FrozenV26TestCorpus.Parse(FrozenV26TestCorpus.ReadRaw("payloads/" + baselineSha + ".json"));
                Check(baseline, false);
                Assert.That((string)alias["reason"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)alias["classification"], Is.EqualTo("detached-corruption-of-accepted-actual26-test-witness"));
            }
            else
            {
                CheckMode(input, (int)alias["mode"]);
                Coordinates(input, (JObject)alias["coordinates"], (string)alias["payloadSha256"]);
                Assert.That((string)alias["classification"], Is.EqualTo("actual-current26-public-producer-managed-test-witness"));
            }
            string exact = kind == "shape-refused" && ((string)alias["label"]).EndsWith("-header", StringComparison.Ordinal)
                ? "The historical schema26 header must be literal integer26." : null;
            Check(input, refuse, exact);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ActualOpeningsKeepAllFourCastSizesAndSupportedModes(int mode)
        {
            var openings = FrozenV26TestCorpus.Aliases.Where(a => (string)a["kind"] == "opening" && (int)a["mode"] == mode).ToArray();
            Assert.That(openings.Length, Is.EqualTo(4));
            var casts = new List<int>();
            foreach (var alias in openings)
            {
                var payload = FrozenV26TestCorpus.Payload(alias);
                Check(payload, false); CheckMode(payload, mode); casts.Add(((JArray)payload["contestants"]).Count);
                Assert.That((long)payload["seed"], Is.EqualTo(2505));
                Assert.That((int)payload["revision"], Is.Zero);
                Assert.That((int)payload["week"], Is.EqualTo(1));
                Assert.That((int)payload["phase"], Is.Zero);
            }
            Assert.That(casts, Is.EquivalentTo(new[] { 3, 6, 8, 12 }));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void WholeRecordedChainsRetainEveryPrivateStateAndPacketLinkWithoutExecution(int mode)
        {
            var season = ((JArray)FrozenV26TestCorpus.Manifest["wholeSeasons"]).OfType<JObject>().Single(s => (int)s["mode"] == mode);
            var boundaries = (JArray)season["boundaries"]; var commands = (JArray)season["commands"];
            Assert.That(boundaries.Count, Is.EqualTo(67)); Assert.That(commands.Count, Is.EqualTo(66));
            Assert.That((int)season["commandBound"], Is.EqualTo(512)); Assert.That((int)season["cast"], Is.EqualTo(6));
            Assert.That((bool)season["finished"], Is.True);
            var phases = new HashSet<int>(); JObject rules = null; string session = null;
            for (int index = 0; index < boundaries.Count; index++)
            {
                var point = (JObject)boundaries[index]; string sha = (string)point["payloadSha256"];
                var payload = FrozenV26TestCorpus.Parse(FrozenV26TestCorpus.ReadRaw("payloads/" + sha + ".json"));
                Check(payload, false); CheckMode(payload, mode); Coordinates(payload, point, sha);
                session = session ?? (string)payload["sessionId"]; rules = rules ?? Rules(payload);
                Assert.That((long)payload["seed"], Is.EqualTo(2505));
                Assert.That((string)payload["sessionId"], Is.EqualTo(session));
                Assert.That((int)payload["revision"], Is.EqualTo(index));
                Assert.That(((JArray)payload["contestants"]).Count, Is.EqualTo(6));
                Assert.That(JToken.DeepEquals(rules, Rules(payload)), Is.True, "Rules may not change inside a captured season.");
                phases.Add((int)payload["phase"]);
                if (index == commands.Count) continue;
                var packet = FrozenV26TestCorpus.Parse(FrozenV26TestCorpus.ReadRaw("commands/" + (string)commands[index] + ".json"));
                Assert.That(packet.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "command", "before", "after" }));
                Assert.That(JToken.DeepEquals(packet["before"], point) && JToken.DeepEquals(packet["after"], boundaries[index + 1]), Is.True);
                var command = (JObject)packet["command"];
                var next = FrozenV26TestCorpus.Parse(FrozenV26TestCorpus.ReadRaw("payloads/" + (string)boundaries[index + 1]["payloadSha256"] + ".json"));
                Assert.That((int)command["expectedRevision"], Is.EqualTo(index));
                Assert.That(JToken.DeepEquals(command["expectedPhase"], point["phase"]), Is.True);
                Assert.That((string)command["actorId"], Is.EqualTo((string)payload["playerId"]));
                Assert.That((string)command["id"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)((JArray)next["acceptedCommandIds"]).Last, Is.EqualTo((string)command["id"]));
            }
            Assert.That(phases, Is.EquivalentTo(Enumerable.Range(0, 16)));
            Assert.That(((JArray)season["phases"]).Values<int>(), Is.EqualTo(Enumerable.Range(0, 16)));
            var done = FrozenV26TestCorpus.Parse(FrozenV26TestCorpus.ReadRaw("payloads/" + (string)boundaries.Last["payloadSha256"] + ".json"));
            string winner = (string)done["winnerId"], runner = (string)done["runnerUpId"];
            Assert.That((int)done["phase"], Is.EqualTo(13));
            Assert.That(winner, Is.Not.Null.And.Not.Empty.And.Not.EqualTo(runner));
            Assert.That(((JArray)done["contestants"]).Count(c => (string)c["id"] == winner && (int)c["status"] == 3), Is.EqualTo(1));
            Assert.That(((JArray)done["contestants"]).Count(c => (string)c["id"] == runner && (int)c["status"] == 4), Is.EqualTo(1));
        }

        [TestCase(0, "primarySha256")]
        [TestCase(0, "backupSha256")]
        [TestCase(0, "checksumCorruptSha256")]
        [TestCase(0, "semanticCorruptSha256")]
        [TestCase(1, "primarySha256")]
        [TestCase(1, "backupSha256")]
        [TestCase(1, "checksumCorruptSha256")]
        [TestCase(1, "semanticCorruptSha256")]
        [TestCase(2, "primarySha256")]
        [TestCase(2, "backupSha256")]
        [TestCase(2, "checksumCorruptSha256")]
        [TestCase(2, "semanticCorruptSha256")]
        public void ActualEnvelopeBytesRetainOriginalChecksumAndSeparateStateDecision(int mode, string family)
        {
            var disk = ((JArray)FrozenV26TestCorpus.Manifest["diskExercises"]).OfType<JObject>().Single(d => (int)d["mode"] == mode);
            foreach (string flag in new[] { "twoDistinctSourceSaves", "readOnlyLoadBytesUnchanged", "candidateRefusalPreservesByteInventory",
                "checksumBeforeMigrationRefused", "explicitRecoveryExactBackup", "previousCorruptPrimaryRetained", "checksumValidSemanticLoadRefused" })
                Assert.That(disk[flag].Type == JTokenType.Boolean && (bool)disk[flag], Is.True, "Recorded actual disk proof: " + flag);
            Assert.That((string)disk["classification"], Is.EqualTo("actual-SaveStore-test-owned-managed-disk-not-user-save-or-native-acceptance"));
            string sha = (string)disk[family];
            var bytes = FrozenV26TestCorpus.ReadRaw("envelopes/" + sha + ".json");
            var envelope = FrozenV26TestCorpus.Parse(bytes); string before = Text(envelope);
            Assert.That(envelope.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "format", "version", "savedAt", "checksum", "state" }));
            Assert.That((string)envelope["format"], Is.EqualTo("gamesim-unity-save"));
            Assert.That(envelope["version"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)envelope["version"], Is.EqualTo(1));
            Assert.That(envelope["savedAt"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(DateTimeOffset.TryParse((string)envelope["savedAt"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), Is.True);
            var state = (JObject)envelope["state"]; CheckMode(state, mode);
            Assert.That((string)envelope["checksum"] == FrozenV26TestCorpus.Hash(FrozenV26TestCorpus.Utf8.GetBytes(Canonical(state))),
                Is.EqualTo(family != "checksumCorruptSha256"), "Checksum classification is separate from the fixed state decision.");
            Check(state, family == "semanticCorruptSha256");
            Assert.That(Text(envelope), Is.EqualTo(before));
            Assert.That(FrozenV26TestCorpus.ReadRaw("envelopes/" + sha + ".json"), Is.EqualTo(bytes));
            Assert.That((string)disk["primarySha256"], Is.Not.EqualTo((string)disk["backupSha256"]));
            var retained = (JArray)disk["finalDiskFiles"];
            Assert.That(retained.Count, Is.EqualTo(7));
            Assert.That(retained.Select(r => (string)r["file"]).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(7));
            // Those original absolute/relative disk locations are provenance, not loader paths.
            foreach (JObject record in retained)
            {
                Assert.That(Regex.IsMatch((string)record["file"], "^disk/mode" + mode + "/(?:(?:checksum-recovery|semantic-refusal)/)?season\\.json(?:\\.backup|\\.before-recovery-[0-9a-f]{32}\\.json)?$"), Is.True);
                Assert.That(((JArray)FrozenV26TestCorpus.Manifest["files"]).Any(f => (string)f["file"] == "envelopes/" + (string)record["sha256"] + ".json"
                    && (long)f["bytes"] == (long)record["bytes"]), Is.True);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        [TestCase(19)]
        [TestCase(20)]
        [TestCase(21)]
        [TestCase(22)]
        [TestCase(23)]
        [TestCase(24)]
        [TestCase(25)]
        [TestCase(26)]
        [TestCase(27)]
        [TestCase(28)]
        [TestCase(29)]
        [TestCase(30)]
        [TestCase(31)]
        [TestCase(32)]
        public void DirectMalformedAndNearBoundControlsAreNotPlayedOrMigratedWitnesses(int index)
        {
            var baseline = FrozenV26TestCorpus.Payload(FrozenV26TestCorpus.Aliases.Single(a => (string)a["kind"] == "checkpoint"
                && (int)a["mode"] == 2 && (string)a["label"] == "broken-deal"));
            Check(baseline, false);
            var value = (JObject)baseline.DeepClone(); var row = ((JArray)value["unifiedCommitments"]).OfType<JObject>().First();
            switch (index)
            {
                case 0: value["schemaVersion"] = new JValue(new BigInteger(26)); break;
                case 1: value["seed"] = new JValue(new BigInteger(2505)); break;
                case 2: value = null; break;
                case 3: value.Remove("schemaVersion"); break;
                case 4: value["schemaVersion"] = 27; break;
                case 5: value["schemaVersion"] = 26.0; break;
                case 6: value.Remove("unifiedVoteReveals"); break;
                case 7: value["unifiedVoteReveals"] = new JObject(); break;
                case 8: ((JArray)value["unifiedVoteReveals"]).Add(new JObject { ["week"] = 1, ["ballots"] = new JArray() }); break;
                case 9: row.Remove("targetId"); break;
                case 10: row["targetId"] = ""; break;
                case 11: row.Remove("subtype"); break;
                case 12: row["subtype"] = ""; break;
                case 13: value["unknownOriginalMember"] = true; break;
                case 14: ((JObject)value["contestants"][0]["stats"]).Remove("physical"); break;
                case 15: value["contestants"][0]["stats"]["physical"] = "5"; break;
                case 16: AddDepth(value, 64); break;
                case 17: value["unknownOriginalMember"] = new JValue(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)); break;
                case 18: value["unknownOriginalMember"] = new JValue(new byte[] { 1, 2, 3 }); break;
                case 19: value["unknownOriginalMember"] = new JRaw("null"); break;
                case 20: value["unknownOriginalMember"] = JValue.CreateUndefined(); break;
                case 21: value["unknownOriginalMember"] = new JValue(BigInteger.One << 4096); break;
                case 22: value["unknownOriginalMember"] = new JValue(-(BigInteger.One << 4096)); break;
                case 23: value["unknownOriginalMember"] = new string('\u0800', 3 * 1024 * 1024); break;
                case 24: value[new string('x', 8 * 1024 * 1024 + 1)] = true; break;
                case 25: value["unknownOriginalMember"] = new string('x', 8 * 1024 * 1024 + 1); break;
                case 26: value["unifiedCommitments"] = new JArray(Enumerable.Range(0, 401).Select(_ => row.DeepClone())); break;
                case 27: value["unifiedCommitmentRulesVersion"] = 2; break;
                case 28: AddDepth(value, 63); break;
                case 29:
                    value["unknownOriginalMember"] = "";
                    int size = FrozenV26TestCorpus.Utf8.GetByteCount(Text(value));
                    value["unknownOriginalMember"] = new string('x', 8 * 1024 * 1024 - size);
                    Assert.That(FrozenV26TestCorpus.Utf8.GetByteCount(Text(value)), Is.EqualTo(8 * 1024 * 1024)); break;
                case 30: value["unifiedCommitments"] = new JArray(Enumerable.Range(0, 400).Select(_ => row.DeepClone())); break;
                case 31: value["unknownOriginalMember"] = new JValue((BigInteger.One << 1024) - BigInteger.One); break;
                case 32: value["unknownOriginalMember"] = new JValue(BigInteger.One << 1024); break;
            }
            string excluded = index == 28 ? "deeply nested" : index == 29 ? "eight MiB" : index == 30 ? "bounded canonical list"
                : index == 31 ? "integer exceeds every finite" : null;
            Check(value, index >= 2, ExpectedDirect(index), excluded, true);
        }

        [Test]
        public void SharedHelperReturnsDetachedManifestAliasesPayloadsAndByteBuffers()
        {
            var manifest = FrozenV26TestCorpus.Manifest; var original = Text(manifest); manifest["complete"] = false;
            Assert.That(Text(FrozenV26TestCorpus.Manifest), Is.EqualTo(original));
            var aliases = FrozenV26TestCorpus.Aliases.ToArray(); var first = aliases[0]; string aliasBefore = Text(first);
            first["label"] = "caller-mutated";
            Assert.That(Text(FrozenV26TestCorpus.Aliases.First()), Is.EqualTo(aliasBefore));
            Assert.Throws<InvalidDataException>(() => FrozenV26TestCorpus.Payload(first), "A caller-relabelled alias cannot become original evidence.");
            var canonical = FrozenV26TestCorpus.Aliases.First(); var payload = FrozenV26TestCorpus.Payload(canonical); var payloadBefore = Text(payload);
            payload["revision"] = -999;
            Assert.That(Text(FrozenV26TestCorpus.Payload(canonical)), Is.EqualTo(payloadBefore));
            string relative = "payloads/" + (string)canonical["payloadSha256"] + ".json";
            var raw = FrozenV26TestCorpus.ReadRaw(relative); string hash = FrozenV26TestCorpus.Hash(raw); raw[0] ^= 1;
            Assert.That(FrozenV26TestCorpus.Hash(FrozenV26TestCorpus.ReadRaw(relative)), Is.EqualTo(hash));
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(null)] [TestCase("")] [TestCase("../corpus.json")] [TestCase("payloads/../corpus.json")]
        [TestCase("C:/corpus.json")] [TestCase("D:\\corpus.json")] [TestCase("/corpus.json")] [TestCase("corpus.json\n")]
        public void SharedHelperRefusesAbsoluteEscapingAndMalformedPaths(string relative)
        {
            Assert.Throws<InvalidDataException>(() => FrozenV26TestCorpus.ReadRaw(relative));
        }

        [TestCase("portable-index.json")] [TestCase(".gitattributes")]
        [TestCase("payloads/0000000000000000000000000000000000000000000000000000000000000000.json")]
        [TestCase("disk/mode0/season.json")]
        public void SharedHelperNeverLoadsUnindexedProvenanceOrAncillaryPaths(string relative)
        {
            Assert.Throws<InvalidDataException>(() => FrozenV26TestCorpus.ReadRaw(relative));
        }

        private static void Check(JObject input, bool refuse, string exact = null, string excluded = null, bool direct = false)
        {
            var before = input?.DeepClone(); var propertyOrder = input?.DescendantsAndSelf().OfType<JProperty>().Select(p => p.Name).ToArray();
            var byteTokens = input?.DescendantsAndSelf().OfType<JValue>().Where(v => v.Value is byte[])
                .Select(v => Tuple.Create(v, FrozenV26TestCorpus.Hash((byte[])v.Value))).ToArray();
            string serialized = !direct && input != null ? Text(input) : null;
            string error = null;
            try { Invoke(input); } catch (InvalidDataException failure) { error = failure.Message; }
            Assert.That(error != null, Is.EqualTo(refuse));
            if (exact != null) Assert.That(error, Is.EqualTo(exact));
            if (excluded != null && error != null) Assert.That(error, Does.Not.Contain(excluded), "Near-bound input must reach its semantic/shape refusal, not the exceeded preflight.");
            Assert.That(JToken.DeepEquals(before, input), Is.True, "Complete caller data must be unchanged.");
            if (serialized != null) Assert.That(Text(input), Is.EqualTo(serialized), "Caller scalar/list/property order must be unchanged.");
            if (propertyOrder != null) Assert.That(input.DescendantsAndSelf().OfType<JProperty>().Select(p => p.Name), Is.EqualTo(propertyOrder));
            if (byteTokens != null) Assert.That(byteTokens.All(t => FrozenV26TestCorpus.Hash((byte[])t.Item1.Value) == t.Item2), Is.True);
        }
        private static void Invoke(JObject payload)
        {
            var method = Contract.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(JObject) }, null);
            if (method == null || method.ReturnType != typeof(void)) throw new InvalidDataException("Exact fixed26 contract entry is missing.");
            try { method.Invoke(null, new object[] { payload }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void CheckMode(JObject state, int mode)
        {
            Assert.That(state["schemaVersion"].Type, Is.EqualTo(JTokenType.Integer)); Assert.That((int)state["schemaVersion"], Is.EqualTo(26));
            Assert.That(mode, Is.InRange(0, 2));
            Assert.That((int)state["unifiedCommitmentRulesVersion"], Is.EqualTo(mode == 0 ? 0 : 1));
            Assert.That((int)state["unifiedHearingRulesVersion"], Is.EqualTo(mode == 2 ? 1 : 0), "Fixture mode2 is Safety1/hearing1, never public Vote2.");
        }
        private static void Coordinates(JObject state, JObject coordinate, string sha)
        {
            Assert.That(coordinate.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "seed", "sessionId", "revision", "week", "phase", "payloadSha256" }));
            foreach (string field in new[] { "seed", "sessionId", "revision", "week", "phase" })
                Assert.That(JToken.DeepEquals(state[field], coordinate[field]), Is.True, field);
            Assert.That((string)coordinate["payloadSha256"], Is.EqualTo(sha));
        }
        private static JObject Rules(JObject state)
        {
            var rules = new JObject();
            foreach (var property in state.Properties().Where(p => p.Name.EndsWith("RulesStartWeek", StringComparison.Ordinal) || p.Name.EndsWith("RulesVersion", StringComparison.Ordinal)))
                rules[property.Name] = property.Value.DeepClone();
            rules["story"] = new JObject { ["rulesStartWeek"] = state["story"]["rulesStartWeek"].DeepClone(), ["rulesVersion"] = state["story"]["rulesVersion"].DeepClone() };
            rules["npcSocial"] = state["npcSocial"].Type == JTokenType.Null ? JValue.CreateNull() : new JObject { ["rulesStartWeek"] = state["npcSocial"]["rulesStartWeek"].DeepClone() };
            return rules;
        }
        private static void AddDepth(JObject value, int depth)
        {
            JObject cursor = value;
            for (int i = 0; i < depth; i++) { var child = new JObject(); cursor["deep"] = child; cursor = child; }
        }
        private static string ExpectedDirect(int index)
        {
            if (index == 2) return "Historical schema26 payload is missing.";
            if (index >= 3 && index <= 5) return "The historical schema26 header must be literal integer26.";
            if (index == 16) return "Historical schema26 tree is too deeply nested.";
            if (index >= 17 && index <= 20) return "Historical schema26 contains a non-JSON token.";
            if (index == 21 || index == 22 || index == 32) return "Historical schema26 integer exceeds every finite numeric contract.";
            if (index == 23) return "Historical schema26 payload exceeds eight MiB.";
            if (index == 24) return "Historical schema26 property name is too large.";
            if (index == 25) return "Historical schema26 text is too large.";
            if (index == 26) return "Historical schema26 requires its bounded canonical list.";
            return null;
        }
        private static string Canonical(JToken token)
        {
            if (token is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new JProperty(p.Name, CanonicalToken(p.Value)))).ToString(Formatting.None);
            return CanonicalToken(token).ToString(Formatting.None);
        }
        private static JToken CanonicalToken(JToken token) => token is JObject obj
            ? new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, CanonicalToken(p.Value))))
            : token is JArray array ? new JArray(array.Select(CanonicalToken)) : token.DeepClone();
        private static string Text(JToken value) => value.ToString(Formatting.None);
    }

    /// <summary>
    /// Shared typed relative corpus access for Fixed26 and separately owned migration tests.
    /// Original absolute paths are provenance only. Every public view is detached/pinned.
    /// </summary>
    internal static class FrozenV26TestCorpus
    {
        internal const string ManifestSha = "9fe276f0a3cf761ad18b2b4c08444097589b2673283b63788eabe5724150e993";
        internal const string BindingSha = "7efd225a127350503ff3028bf878e3d38fd469907ff47504d1137f2e31e9a757";
        private const string IndexSha = "7da5cb5f43bc65b71558fdf81e1301ed5d71558521be6edca6f44b9c3af65c46";
        private const string RelativeRoot = "Assets/Gamesim/Tests/EditMode/Fixtures/FrozenV26";
        private const string Attributes = "# Preserve byte-exact historical JSON witnesses.\n*.json -text -eol\n.gitattributes -text -eol\n";
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly Regex Sha = new Regex("^[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);
        private static readonly Regex RawPath = new Regex("^(payloads|commands|envelopes)/[0-9a-f]{64}\\.json\\z", RegexOptions.CultureInvariant);
        private static readonly object Gate = new object();
        // Fresh managed-harness seam only; ordinary tests use bounded BCL ancestry.
        private static string PackageRootOverride = null;
        private static Package Cached;
        private sealed class Entry { internal string Hash; internal long Bytes; }
        private sealed class Package
        {
            internal string Root;
            internal JObject Manifest;
            internal Dictionary<string, Entry> Entries;
        }

        internal static JObject Manifest => (JObject)Get().Manifest.DeepClone();
        internal static IEnumerable<JObject> Aliases => ((JArray)Get().Manifest["aliases"]).OfType<JObject>().Select(a => (JObject)a.DeepClone()).ToArray();
        internal static JObject Payload(JObject alias)
        {
            Need(alias != null && alias["payloadSha256"]?.Type == JTokenType.String && Sha.IsMatch((string)alias["payloadSha256"]), "An actual relative payload hash is required.");
            Need(((JArray)Get().Manifest["aliases"]).OfType<JObject>().Any(a => JToken.DeepEquals(a, alias)), "The complete caller alias must be an actual pinned manifest row.");
            return Parse(ReadRaw("payloads/" + (string)alias["payloadSha256"] + ".json"));
        }
        internal static byte[] ReadRaw(string relative)
        {
            Need(relative != null && (relative == "corpus.json" || relative == "binding.json" || RawPath.IsMatch(relative)), "Only exact relative indexed data paths are allowed.");
            var package = Get();
            Need(package.Entries.TryGetValue(relative, out var entry), "Unindexed/provenance paths are never opened.");
            return ReadPinned(Child(package.Root, relative), entry.Bytes, entry.Hash);
        }
        internal static void AssertPhysicalUnchanged()
        {
            var package = Get(); VerifyPhysical(package);
            var rawManifest = Parse(ReadPinned(Child(package.Root, "corpus.json"), 241660, ManifestSha));
            Need(Text(rawManifest) == Text(package.Manifest), "Pinned cached metadata changed.");
        }

        private static Package Get()
        {
            lock (Gate)
            {
                if (Cached != null) return Cached;
                string root = FindRoot(); NoReparse(root);
                var index = Parse(ReadPinned(Path.Combine(root, "portable-index.json"), 87264, IndexSha));
                Fields(index, "schema", "version", "kind", "manifestSha256", "bindingSha256", "files");
                Need(Int(index, "schema") == 1 && Int(index, "version") == 1 && String(index, "kind") == "gamesim-frozen26-managed-test-corpus"
                    && String(index, "manifestSha256") == ManifestSha && String(index, "bindingSha256") == BindingSha, "Exact portable index identity required.");
                var entries = new Dictionary<string, Entry>(StringComparer.Ordinal); string previous = null; long total = 0;
                Need(index["files"] is JArray files && files.Count == 473, "Exactly473 portable data rows required.");
                foreach (JObject row in (JArray)index["files"])
                {
                    Fields(row, "path", "sha256", "bytes"); string relative = String(row, "path"), hash = String(row, "sha256"); long size = Integer(row, "bytes");
                    Need((relative == "corpus.json" || relative == "binding.json" || RawPath.IsMatch(relative)) && Sha.IsMatch(hash)
                        && (previous == null || StringComparer.Ordinal.Compare(previous, relative) < 0)
                        && size > 0 && size <= 8 * 1024 * 1024 && !entries.ContainsKey(relative), "Invalid/duplicate/unsorted bounded data row.");
                    if (RawPath.IsMatch(relative)) Need(Path.GetFileNameWithoutExtension(relative) == hash, "Content address must equal its raw hash.");
                    entries.Add(relative, new Entry { Hash = hash, Bytes = size }); total += size; previous = relative;
                }
                Need(total < 128L * 1024 * 1024 && entries.Count(e => e.Key.StartsWith("payloads/", StringComparison.Ordinal)) == 261
                    && entries.Count(e => e.Key.StartsWith("commands/", StringComparison.Ordinal)) == 198
                    && entries.Count(e => e.Key.StartsWith("envelopes/", StringComparison.Ordinal)) == 12, "Complete raw family inventory required.");
                var package = new Package { Root = root, Entries = entries };
                VerifyPhysical(package);
                package.Manifest = Parse(ReadPinned(Child(root, "corpus.json"), 241660, ManifestSha));
                VerifyManifest(package);
                var binding = Parse(ReadPinned(Child(root, "binding.json"), 1400448, BindingSha));
                Need(Bool(binding, "accepted") && Bool(binding, "complete") && binding["failure"].Type == JTokenType.Null
                    && binding["sourceDrift"] is JArray drift && drift.Count == 0 && Bool(binding, "headUnchanged")
                    && Bool(binding, "controlUnchanged") && Bool(binding, "inventoryUnchanged") && Bool(binding, "fixturesUnchanged")
                    && !Bool(binding, "nativeEditorOrPlayerLaunched") && !Bool(binding, "ordinaryUserSaveWritten")
                    && !Bool(binding, "schema27Activated") && !Bool(binding, "authority2Activated"), "Original accepted unchanged managed capture required.");
                Cached = package;
                return Cached;
            }
        }
        private static void VerifyManifest(Package package)
        {
            var m = package.Manifest;
            Need(Int(m, "schema") == 1 && Int(m, "literalPayloadSchema") == 26 && String(m, "kind") == "gamesim-actual-inert26-managed-test-corpus"
                && Bool(m, "complete") && Int(m, "openingCount") == 12 && Int(m, "checkpointCount") == 23 && Int(m, "acceptedPublicChainCommands") == 198
                && Int(m, "semanticRefusals") == 16 && Int(m, "shapeRefusals") == 12 && Integer(m, "dataBytesBeforeManifest") == 13973386
                && Bool(m, "capturedManagedTestWitnessesNotShippingUserSaves") && !Bool(m, "nativeEditorOrPlayerLaunched") && !Bool(m, "old25CorpusRetagged")
                && !Bool(m, "schema27Activated") && !Bool(m, "authority2Activated"), "Pinned actual26 manifest identity/counts required.");
            Need(m["aliases"] is JArray aliases && aliases.Count == 264 && m["wholeSeasons"] is JArray seasons && seasons.Count == 3
                && m["diskExercises"] is JArray disks && disks.Count == 3 && m["files"] is JArray files && files.Count == 471, "Complete retained sections required.");
            var seen = new HashSet<string>(StringComparer.Ordinal); long bytes = 0;
            foreach (JObject file in (JArray)m["files"])
            {
                string path = String(file, "file"), hash = String(file, "sha256"); long size = Integer(file, "bytes");
                Need(RawPath.IsMatch(path) && seen.Add(path) && package.Entries.TryGetValue(path, out var entry)
                    && entry.Hash == hash && entry.Bytes == size, "Every manifest raw row must match the portable index.");
                bytes += size;
            }
            Need(bytes == 13777946 && seen.Count == 471, "Exact original raw byte closure required.");
            foreach (JObject alias in (JArray)m["aliases"])
            {
                string kind = String(alias, "kind");
                Need(new[] { "opening", "checkpoint", "season-boundary", "semantic-refused", "shape-refused" }.Contains(kind)
                    && Sha.IsMatch(String(alias, "payloadSha256")) && seen.Contains("payloads/" + String(alias, "payloadSha256") + ".json"), "Every typed alias must resolve inside the pinned package.");
            }
        }
        private static void VerifyPhysical(Package package)
        {
            var expected = new HashSet<string>(StringComparer.Ordinal) { "portable-index.json", "portable-index.json.meta", ".gitattributes",
                "payloads.meta", "commands.meta", "envelopes.meta" };
            foreach (string path in package.Entries.Keys) { expected.Add(path); expected.Add(path + ".meta"); }
            var actual = new HashSet<string>(StringComparer.Ordinal); var directories = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(); pending.Push(package.Root);
            while (pending.Count != 0)
            {
                string directory = pending.Pop(); NoReparse(directory);
                foreach (string child in Directory.EnumerateFileSystemEntries(directory))
                {
                    NoReparse(child); string relative = Relative(package.Root, child);
                    if (Directory.Exists(child))
                    {
                        Need(relative == "payloads" || relative == "commands" || relative == "envelopes", "No extra/nested package directories.");
                        Need(directories.Add(relative), "Duplicate package directory."); pending.Push(child);
                    }
                    else Need(actual.Add(relative) && expected.Contains(relative) && actual.Count <= 952, "Unknown/duplicate/excess package file.");
                }
            }
            Need(actual.SetEquals(expected) && actual.Count == 952 && directories.SetEquals(new[] { "payloads", "commands", "envelopes" }), "Exact952 inner files/three folders required.");
            ReadPinned(Child(package.Root, "portable-index.json"), 87264, IndexSha);
            Need(ReadExact(Child(package.Root, ".gitattributes"), Utf8.GetByteCount(Attributes)).SequenceEqual(Utf8.GetBytes(Attributes)), "Exact byte-preservation policy required.");
            foreach (var file in package.Entries) ReadPinned(Child(package.Root, file.Key), file.Value.Bytes, file.Value.Hash);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in package.Entries.Keys.Concat(new[] { "portable-index.json" }))
            {
                Need(guids.Add(MetaGuid(path)), "Duplicate JSON GUID.");
                var expectedMeta = MetaBytes(path, false);
                Need(ReadExact(Child(package.Root, path + ".meta"), expectedMeta.Length).SequenceEqual(expectedMeta), "Noncanonical/orphan JSON meta.");
            }
            foreach (string folder in new[] { "payloads", "commands", "envelopes" })
            {
                Need(guids.Add(MetaGuid(folder + "/")), "Duplicate folder GUID.");
                var bytes = MetaBytes(folder + "/", true);
                Need(ReadExact(Child(package.Root, folder + ".meta"), bytes.Length).SequenceEqual(bytes), "Noncanonical folder meta.");
            }
            Need(guids.Add(MetaGuid("")) && guids.Count == 478, "Exactly478 unique GUIDs required.");
            var rootMeta = MetaBytes("", true); string adjacent = package.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta";
            Need(ReadExact(adjacent, rootMeta.Length).SequenceEqual(rootMeta), "Exact adjacent folder meta required.");
        }
        private static string FindRoot()
        {
            if (PackageRootOverride != null)
            {
                Need(Path.IsPathRooted(PackageRootOverride), "Fresh private harness override must be absolute.");
                string chosen = Path.GetFullPath(PackageRootOverride);
                Need(Path.GetFileName(chosen) == "FrozenV26", "Exact package basename required."); NoReparse(chosen); return chosen;
            }
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string start in new[] { Directory.GetCurrentDirectory(), Path.GetDirectoryName(typeof(FrozenV26TestCorpus).Assembly.Location) })
            {
                string at = start;
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
                {
                    string candidate = Path.Combine(at, RelativeRoot.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(Path.Combine(candidate, "portable-index.json"))) candidates.Add(Path.GetFullPath(candidate));
                    string next = Path.GetDirectoryName(at); if (next == at) break; at = next;
                }
            }
            Need(candidates.Count == 1, "Exactly one bounded package required; missing/ambiguous is failure, never a skip.");
            return candidates.Single();
        }
        private static string Child(string root, string relative)
        {
            Need(relative != null && !Path.IsPathRooted(relative) && Regex.IsMatch(relative, "^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+)*\\z")
                && !relative.Split('/').Any(p => p == "." || p == ".."), "Only a bounded nonescaping relative path is accepted.");
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            Need(path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Package child escaped root.");
            NoReparse(path); return path;
        }
        private static string Relative(string root, string path) => Path.GetFullPath(path).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/');
        private static void NoReparse(string path)
        {
            for (string cursor = Path.GetFullPath(path); !string.IsNullOrEmpty(cursor);)
            {
                if (File.Exists(cursor) || Directory.Exists(cursor)) Need((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) == 0, "Reparse ancestry/items refused.");
                string next = Path.GetDirectoryName(cursor); if (next == cursor) break; cursor = next;
            }
        }
        private static string MetaGuid(string relative) => Hash(Utf8.GetBytes("gamesim-frozen-v26-portable-fixture-v1|FrozenV26/" + relative)).Substring(0, 32);
        private static byte[] MetaBytes(string relative, bool folder) => Utf8.GetBytes("fileFormatVersion: 2\nguid: " + MetaGuid(relative) + "\n"
            + (folder ? "folderAsset: yes\nDefaultImporter:\n" : "TextScriptImporter:\n")
            + "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        private static byte[] ReadPinned(string path, long length, string hash)
        {
            var bytes = ReadExact(path, length); Need(Hash(bytes) == hash, "Original raw/index hash changed."); return bytes;
        }
        private static byte[] ReadExact(string path, long length)
        {
            NoReparse(path); Need(length > 0 && length <= 8L * 1024 * 1024, "Bounded exact read length required.");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Need(stream.Length == length, "Pinned length differs before bounded allocation.");
                var bytes = new byte[checked((int)length)]; int offset = 0;
                while (offset < bytes.Length) { int read = stream.Read(bytes, offset, bytes.Length - offset); Need(read > 0, "Truncated immutable file."); offset += read; }
                Need(stream.ReadByte() == -1 && stream.Length == length, "Immutable file grew/shrank during read."); return bytes;
            }
        }
        internal static JObject Parse(byte[] bytes)
        {
            Need(bytes != null && bytes.Length > 0 && bytes.Length <= 8L * 1024 * 1024, "Bounded JSON bytes required.");
            using (var text = new StringReader(Utf8.GetString(bytes)))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                var value = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Need(value is JObject && !reader.Read(), "Exactly one complete JSON object required."); return (JObject)value;
            }
        }
        internal static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string Text(JToken value) => value.ToString(Formatting.None);
        private static void Fields(JObject value, params string[] names) => Need(value.Properties().Select(p => p.Name).SequenceEqual(names, StringComparer.Ordinal), "Exact ordered index fields required.");
        private static int Int(JObject value, string key) => checked((int)Integer(value, key));
        private static long Integer(JObject value, string key) { Need(value[key]?.Type == JTokenType.Integer, "Literal integer required: " + key); return (long)value[key]; }
        private static string String(JObject value, string key) { Need(value[key]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value[key]), "Actual text required: " + key); return (string)value[key]; }
        private static bool Bool(JObject value, string key) { Need(value[key]?.Type == JTokenType.Boolean, "Actual boolean required: " + key); return (bool)value[key]; }
        private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
    }
}
