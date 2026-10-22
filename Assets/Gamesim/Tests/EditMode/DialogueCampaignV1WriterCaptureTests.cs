using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// Ordinary native v1-path save/replay regression with a bounded retained-output channel.
    /// Only an independently admitted PRE-v2 run may seed the immutable old-writer fixture set.
    /// Later runs remain useful regressions but must never replace/relabel that original capture.
    /// No source hash is an enduring test expectation; actual source/assembly identities are reported.
    /// </summary>
    public sealed class DialogueCampaignV1WriterCaptureTests
    {
        private const string Prefix = "GAMESIM_V1_WRITER ";
        private const string TempPrefix = "GamesimDialogueV1Writer-";
        private const int MaxFixtureBytes = 8 * 1024 * 1024, MaxBaselineBytes = 8 * 1024 * 1024;
        private const int MaxOutputCharacters = 32 * 1024 * 1024;
        private static readonly string[] FixtureIds = { "case", "finish-personal", "answered", "neutral", "lapsed" };
        private static readonly string[] ObservedSourceFiles =
        {
            "Runtime/Persistence/EpisodeSaveStore.cs",
            "Runtime/Persistence/EpisodeSaveValidation.cs",
            "Simulation/DialogueRules.cs",
            "Simulation/Story/DialogueCampaignV1.cs",
            "Simulation/Story/StoryText.cs",
            "Simulation/EpisodeEngine.Dialogue.cs",
            "Simulation/EpisodeEngine.Strategy.cs",
            "Tests/EditMode/ModeTwoReaderSweep.cs",
            "Tests/EditMode/EpisodeEngineTests.cs",
            "Tests/EditMode/DialogueCampaignV1WriterCaptureTests.cs",
        };
        private readonly List<CapturedFile> files = new List<CapturedFile>();
        private string directory;
        private EpisodeSaveStore traceStore;
        private int nonce, navigation, talks, neutralAnswers;

        private sealed class CapturedFile
        {
            internal string FixtureId, Role, Name, OriginalPath;
            internal byte[] Bytes;
        }

        [SetUp]
        public void CreateAnIsolatedSlotDirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), TempPrefix + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(directory), Is.False);
            Directory.CreateDirectory(directory);
            traceStore = new EpisodeSaveStore(Path.Combine(directory, "trace.json"));
            files.Clear();
            nonce = navigation = talks = neutralAnswers = 0;
        }

        [TearDown]
        public void RemoveOnlyKnownOwnedTemporaryFiles()
        {
            if (directory == null || !Directory.Exists(directory)) return;
            string full = Path.GetFullPath(directory);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(full.StartsWith(temp, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(full), Does.StartWith(TempPrefix));
            Assert.That((File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0, Is.True);
            Assert.That(Directory.GetDirectories(full), Is.Empty);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in FixtureIds.Concat(new[] { "trace" }))
            {
                var slot = new EpisodeSaveStore(Path.Combine(full, id + ".json"));
                allowed.Add(slot.SavePath); allowed.Add(slot.BackupPath);
            }
            string[] paths = Directory.GetFiles(full);
            Assert.That(paths.All(allowed.Contains), Is.True, "Unexpected files are preserved rather than recursively removed.");
            Assert.That(paths.All(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0), Is.True);
            foreach (string path in paths) File.Delete(path);
            Directory.Delete(full, false);
        }

        [Test]
        public void FiveActualV1WriterPairsKeepTheirSavedChoicesAndReplayBeforeAnyConsumerUpgrade()
        {
            JObject sourcesBefore = SourceIdentity();
            JObject assembliesBefore = AssemblyIdentity();
            var route = new JArray();
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "The current unpublished pilot must not change shipping defaults.");
            fresh.dialogueRulesVersion = 1; // Explicit fresh-fixture opt-in; no gameplay facts/counters are assigned.
            Assert.That(fresh.unifiedCommitmentRulesVersion, Is.EqualTo(2));
            Assert.That(fresh.allWeekRulesStartWeek, Is.EqualTo(1));
            Valid(fresh);
            string freshBytes = Json(fresh);
            JObject initial = Observe(fresh);
            var engine = new EpisodeEngine(fresh);
            for (; navigation < 96; navigation++)
            {
                var s = engine.Snapshot;
                Assert.That(s.week, Is.EqualTo(1));
                Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active));
                Assert.That(s.phase, Is.Not.EqualTo(EpisodePhase.Finished));
                if (s.phase == EpisodePhase.Campaign) break;
                // The established native first-week public walker, not its mode projection.
                var next = ModeTwoReaderSweep.Next(s);
                next.id = NewId();
                ApplyAndRecord(engine, next, route);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(engine.Snapshot.nominees, Does.Contain(engine.Snapshot.playerId));
            CloseOldAsks(engine, route);
            while (Remaining(engine.Snapshot) > 1 && talks < 16)
            {
                var s = engine.Snapshot;
                var talk = Command(s, EpisodeCommandKind.Talk);
                talk.targetId = s.nominees.First(id => id != s.playerId);
                ApplyAndRecord(engine, talk, route);
                talks++;
                CloseOldAsks(engine, route);
            }
            var campaign = engine.Snapshot;
            Assert.That(Remaining(campaign), Is.EqualTo(1));
            Assert.That(campaign.week, Is.EqualTo(1));
            Assert.That(campaign.revision, Is.EqualTo(11), "The already observed fixed seed1 route, not a new favorable-outcome search.");
            string voter = EpisodeEngine.Voters(campaign).Where(p => !p.isPlayer).Select(p => p.id)
                .FirstOrDefault(id => DialogueRules.CanStart(campaign, id, out _));
            Assert.That(voter, Is.EqualTo("alex-chen"));
            Assert.That(Json(fresh), Is.EqualTo(freshBytes));
            string campaignBytes = Json(campaign);

            var paidPrefix = new JArray();
            var start = Command(campaign, EpisodeCommandKind.StartDialogue);
            start.targetId = voter; start.text = DialogueCampaignV1.TemplateId; start.secondTargetId = "open-case";
            var entered = ApplyAndRecord(engine, start, paidPrefix);
            string episodeId = DialogueRules.Active(entered).id;
            AssertPaidNode(entered, episodeId, "case");
            Assert.That(entered.windowActions[Windows.AfterVeto], Is.EqualTo(campaign.windowActions[Windows.AfterVeto] + 1));
            Assert.That(entered.outOfPhaseSocialActions, Is.EqualTo(campaign.outOfPhaseSocialActions + 1));
            var appeal = Choice(entered, "appeal-personally");
            var middle = ApplyAndRecord(engine, appeal, paidPrefix);
            AssertPaidNode(middle, episodeId, "finish-personal");
            AssertFreeCounters(entered, middle);
            Assert.That(middle.randomState, Is.EqualTo(entered.randomState));
            Assert.That(middle.npcSocial.randomState, Is.EqualTo(entered.npcSocial.randomState));
            string enteredBytes = Json(entered), middleBytes = Json(middle);

            var fixtures = new JArray();
            fixtures.Add(CapturePair("case", campaign, entered, start, paidPrefix.Take(1), episodeId));
            fixtures.Add(CapturePair("finish-personal", entered, middle, appeal, paidPrefix, episodeId));
            foreach (string id in new[] { "answered", "neutral", "lapsed" })
            {
                var branch = new EpisodeEngine(middle);
                EpisodeCommand final = id == "lapsed" ? Command(middle, EpisodeCommandKind.Advance)
                    : Choice(middle, id == "answered" ? "make-plea" : "keep-vote-open");
                var branchScript = new JArray();
                var closed = ApplyAndRecord(branch, final, branchScript);
                AssertFreeCounters(middle, closed);
                AssertTerminal(closed, episodeId, id);
                fixtures.Add(CapturePair(id, middle, closed, final,
                    paidPrefix.Concat(branchScript), episodeId));
            }
            Assert.That(Json(fresh), Is.EqualTo(freshBytes));
            Assert.That(Json(campaign), Is.EqualTo(campaignBytes));
            Assert.That(Json(entered), Is.EqualTo(enteredBytes));
            Assert.That(Json(middle), Is.EqualTo(middleBytes));
            Assert.That(neutralAnswers, Is.LessThanOrEqualTo(24));
            Assert.That(talks, Is.LessThanOrEqualTo(16));
            Assert.That(navigation, Is.LessThan(96));
            Assert.That(route.Count, Is.EqualTo(navigation + talks + neutralAnswers));
            Assert.That(files.Count, Is.EqualTo(10));
            Assert.That(files.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(10));
            Assert.That(files.Select(row => row.Name), Is.EqualTo(FixtureIds.SelectMany(id =>
                new[] { id + ".primary.json", id + ".backup.json" })));
            foreach (var file in files) Assert.That(File.ReadAllBytes(file.OriginalPath), Is.EqualTo(file.Bytes));
            Assert.That(JToken.DeepEquals(SourceIdentity(), sourcesBefore), Is.True, "Observed source bytes changed during capture.");
            Assert.That(JToken.DeepEquals(AssemblyIdentity(), assembliesBefore), Is.True, "Observed executing assembly identity changed.");

            var baseline = new JObject
            {
                ["schema"] = 1, ["kind"] = "actual-v1-writer-baseline",
                ["classification"] = "Generated writer evidence; only independently admitted pre-v2 run is historical input.",
                ["seed"] = 1, ["houseSize"] = 8, ["dialogueFixtureOptIn"] = 1,
                ["shippingFreshWasOff"] = true, ["templateId"] = DialogueCampaignV1.TemplateId,
                ["contentVersion"] = 1, ["navigationCommands"] = navigation, ["talkCommands"] = talks,
                ["neutralAnswersTotal"] = neutralAnswers, ["maximumNeutralAnswersTotal"] = 24,
                ["sources"] = sourcesBefore, ["assemblies"] = assembliesBefore,
                ["unityVersion"] = UnityEngine.Application.unityVersion,
                ["stateDigestAlgorithm"] = "SHA256/UTF8/recursively-Ordinal-sorted-object-keys/array-order-preserved/compact-json/DateParseNone/FloatParseDouble",
                ["initialActualWriterState"] = initial, ["publicRoute"] = route,
                ["campaign"] = Observe(campaign), ["fixtures"] = fixtures,
                ["independentEffectParity"] = false, ["fullSuiteAcceptance"] = false,
            };
            // Construct and bound the complete output before emitting any success-shaped marker.
            EmitCompleteCapture(baseline);
        }

        private JObject CapturePair(string id, EpisodeState predecessor, EpisodeState state, EpisodeCommand lastAccepted,
            IEnumerable<JToken> branchScript, string episodeId)
        {
            string predecessorBytes = Json(predecessor), stateBytes = Json(state), requestBytes = Json(lastAccepted);
            var store = new EpisodeSaveStore(Path.Combine(directory, id + ".json"));
            Assert.That(File.Exists(store.SavePath) || File.Exists(store.BackupPath), Is.False);
            store.Save(predecessor);
            byte[] expectedBackup = File.ReadAllBytes(store.SavePath);
            store.Save(state);
            byte[] primary = File.ReadAllBytes(store.SavePath), backup = File.ReadAllBytes(store.BackupPath);
            Assert.That(backup, Is.EqualTo(expectedBackup));
            Assert.That(store.TryLoad(out var loaded, out string primaryError), Is.True, primaryError);
            // Read the actual backup through the same strict public reader; never recover/overwrite it.
            Assert.That(new EpisodeSaveStore(store.BackupPath).TryLoad(out var old, out string backupError), Is.True, backupError);
            Valid(loaded); Valid(old);
            Assert.That(Json(loaded), Is.EqualTo(stateBytes));
            Assert.That(Json(old), Is.EqualTo(predecessorBytes));
            Assert.That(Json(predecessor), Is.EqualTo(predecessorBytes));
            Assert.That(Json(state), Is.EqualTo(stateBytes));
            Assert.That(Json(lastAccepted), Is.EqualTo(requestBytes));
            AddFile(id, "primary", store.SavePath, primary);
            AddFile(id, "backup", store.BackupPath, backup);
            var resume = new JArray();
            var engine = new EpisodeEngine(loaded);
            EpisodeCommand terminal = lastAccepted;
            if (id == "case")
            {
                var choice = Choice(engine.Snapshot, "appeal-personally");
                var next = ApplyAndRecord(engine, choice, resume);
                AssertFreeCounters(loaded, next);
            }
            if (id == "case" || id == "finish-personal")
            {
                terminal = Choice(engine.Snapshot, "make-plea");
                var next = ApplyAndRecord(engine, terminal, resume);
                AssertFreeCounters(loaded, next);
                AssertTerminal(next, episodeId, "answered");
            }
            else AssertTerminal(loaded, episodeId, id);
            var committed = engine.Snapshot;
            string committedBytes = Json(committed), terminalBytes = Json(terminal);
            var duplicate = engine.Apply(terminal);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.state), Is.EqualTo(committedBytes));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committedBytes));
            Assert.That(Json(terminal), Is.EqualTo(terminalBytes));
            Valid(duplicate.state);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
            Assert.That(Directory.GetFiles(directory, "*.before-recovery-*"), Is.Empty);
            return new JObject
            {
                ["id"] = id, ["primaryFile"] = id + ".primary.json", ["backupFile"] = id + ".backup.json",
                ["primary"] = Describe(state, WireState(primary)),
                ["backup"] = Describe(predecessor, WireState(backup)),
                ["acceptedBranchAfterCampaign"] = new JArray(branchScript.Select(token => token.DeepClone())),
                ["resumeAfterLoad"] = resume,
                ["idempotencyAfterResumeOrTerminalLoad"] = new JObject
                {
                    ["command"] = ParseObject(terminalBytes), ["accepted"] = false, ["duplicate"] = true,
                    ["reason"] = duplicate.reason, ["after"] = Observe(duplicate.state),
                },
                ["originalPrimaryAndBackupUnchanged"] = true,
            };
        }

        private EpisodeState ApplyAndRecord(EpisodeEngine engine, EpisodeCommand command, JArray records)
        {
            var source = engine.Snapshot;
            Valid(source);
            string original = Json(source), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, source.phase + " " + command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(source.revision + 1));
            Valid(result.state);
            Assert.That(Json(source), Is.EqualTo(original));
            Assert.That(Json(command), Is.EqualTo(request));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            records.Add(new JObject
            {
                ["beforeRevision"] = source.revision, ["beforePhase"] = source.phase.ToString(),
                ["command"] = ParseObject(request), ["accepted"] = true, ["duplicate"] = false,
                ["reason"] = result.reason, ["after"] = Observe(result.state),
            });
            return result.state;
        }

        private JObject Observe(EpisodeState state)
        {
            Valid(state);
            string original = Json(state);
            traceStore.Save(state); // The real writer determines the complete persisted wire token.
            byte[] primary = File.ReadAllBytes(traceStore.SavePath);
            byte[] backup = File.Exists(traceStore.BackupPath) ? File.ReadAllBytes(traceStore.BackupPath) : null;
            Assert.That(traceStore.TryLoad(out var reloaded, out string error), Is.True, error);
            Assert.That(Json(reloaded), Is.EqualTo(original));
            Assert.That(Json(state), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(traceStore.SavePath), Is.EqualTo(primary));
            if (backup != null) Assert.That(File.ReadAllBytes(traceStore.BackupPath), Is.EqualTo(backup));
            return Describe(state, WireState(primary));
        }

        private static JObject Describe(EpisodeState state, JObject wire)
        {
            var presentations = new JArray();
            foreach (var episode in state.dialogue.episodes)
            {
                var cycle = state.storylines.Single(row => row.id == episode.id);
                var events = new JArray();
                foreach (var item in state.houseEvents.Where(row => row.cycleId == episode.id))
                    events.Add(new JObject
                    {
                        ["id"] = item.id, ["contentId"] = item.contentId, ["resolved"] = item.resolved,
                        ["title"] = StoryText.Title(item), ["narrative"] = StoryText.Narrative(state, item),
                        ["eyebrow"] = StoryText.Eyebrow(item), ["outcome"] = item.outcome,
                        ["choices"] = new JArray(item.choices.Select(choice => new JObject
                        {
                            ["optionId"] = choice.optionId, ["label"] = choice.label,
                            ["description"] = StoryText.Description(state, item, choice),
                            ["locked"] = choice.locked, ["costsAction"] = choice.costsAction,
                            ["pickPerson"] = choice.pickPerson,
                        })),
                    });
                presentations.Add(new JObject
                {
                    ["episodeId"] = episode.id, ["templateId"] = cycle.templateId,
                    ["contentVersion"] = episode.contentVersion, ["node"] = cycle.beatId,
                    ["status"] = cycle.status, ["endingId"] = cycle.endingId,
                    ["responseKeys"] = new JArray(episode.responses.Select(row => row.responseKey)),
                    ["events"] = events,
                });
            }
            return new JObject
            {
                ["revision"] = state.revision, ["phase"] = state.phase.ToString(), ["week"] = state.week,
                ["mainRng"] = state.randomState, ["npcRng"] = state.npcSocial.randomState,
                ["remainingAp"] = Remaining(state), ["windowActions"] = new JArray(state.windowActions),
                ["socialActions"] = state.socialActions, ["outOfPhaseSocialActions"] = state.outOfPhaseSocialActions,
                ["boughtActionPoints"] = state.boughtActionPoints, ["moveInExtrasSpent"] = state.moveInExtrasSpent,
                ["stateSha256"] = Sha(Encoding.UTF8.GetBytes(Canonical(wire))),
                ["state"] = wire.DeepClone(), ["presentations"] = presentations,
            };
        }

        private void CloseOldAsks(EpisodeEngine engine, JArray route)
        {
            for (;;)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(row => !row.resolved && HouseEventKind.Asks(row.kind));
                if (item == null) return;
                Assert.That(neutralAnswers, Is.LessThan(24), "One TOTAL neutral-answer budget; never reset it per Talk.");
                Assert.That(item.contentId?.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal) == true, Is.False);
                var command = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                command.targetId = item.id;
                if (item.IsStory)
                {
                    var neutral = item.choices.Single(choice => choice.optionId == item.lapseOptionId);
                    Assert.That(neutral.locked || neutral.costsAction || neutral.pickPerson, Is.False);
                    command.secondTargetId = neutral.optionId;
                }
                else command.text = item.choices.First().label;
                ApplyAndRecord(engine, command, route);
                neutralAnswers++;
            }
        }

        private void AddFile(string id, string role, string path, byte[] bytes)
        {
            Assert.That(FixtureIds.Contains(id), Is.True);
            Assert.That(role == "primary" || role == "backup", Is.True);
            Assert.That(bytes.Length, Is.InRange(1, 8 * 1024 * 1024));
            files.Add(new CapturedFile { FixtureId = id, Role = role, Name = id + "." + role + ".json",
                OriginalPath = path, Bytes = bytes });
            Assert.That(files.Sum(file => (long)file.Bytes.Length), Is.LessThanOrEqualTo(MaxFixtureBytes),
                "The fixed diagnostic export bound is not a new shipping save budget.");
        }

        private void EmitCompleteCapture(JObject baseline)
        {
            byte[] baselineBytes = Encoding.UTF8.GetBytes(baseline.ToString(Formatting.None));
            Assert.That(baselineBytes.Length, Is.InRange(1, MaxBaselineBytes));
            long total = files.Sum(file => (long)file.Bytes.Length);
            var records = new List<JObject>
            {
                new JObject
                {
                    ["schema"] = 1, ["kind"] = "begin", ["protocol"] = "gamesim-v1-writer-capture",
                    ["fixtureIds"] = new JArray(FixtureIds), ["fileCount"] = 10, ["recordCount"] = 13,
                    ["maximumFixtureBytes"] = MaxFixtureBytes, ["maximumBaselineBytes"] = MaxBaselineBytes,
                },
            };
            foreach (var file in files)
                records.Add(new JObject
                {
                    ["schema"] = 1, ["kind"] = "file", ["fixtureId"] = file.FixtureId, ["role"] = file.Role,
                    ["name"] = file.Name, ["bytes"] = file.Bytes.Length, ["sha256"] = Sha(file.Bytes),
                    ["base64"] = Convert.ToBase64String(file.Bytes),
                });
            records.Add(new JObject
            {
                ["schema"] = 1, ["kind"] = "baseline", ["name"] = "v1-baseline.json",
                ["bytes"] = baselineBytes.Length, ["sha256"] = Sha(baselineBytes),
                ["base64"] = Convert.ToBase64String(baselineBytes),
            });
            records.Add(new JObject
            {
                ["schema"] = 1, ["kind"] = "complete", ["fileCount"] = 10, ["recordCount"] = 13,
                ["totalFixtureBytes"] = total, ["baselineBytes"] = baselineBytes.Length,
                ["baselineSha256"] = Sha(baselineBytes), ["allWriterLoadsAndReplaysPassed"] = true,
                ["historicalCaptureAdmissionRequired"] = true, ["fullSuiteAcceptance"] = false,
            });
            Assert.That(records.Count, Is.EqualTo(13));
            string[] lines = records.Select(record => Prefix + record.ToString(Formatting.None)).ToArray();
            Assert.That(lines.Sum(line => (long)line.Length + 1), Is.LessThanOrEqualTo(MaxOutputCharacters));
            foreach (string line in lines) TestContext.Out.WriteLine(line);
        }

        private static JObject SourceIdentity()
        {
            var rows = new JArray();
            foreach (string relative in ObservedSourceFiles)
            {
                string path = Path.Combine(UnityEngine.Application.dataPath, "Gamesim",
                    relative.Replace('/', Path.DirectorySeparatorChar));
                var info = new FileInfo(path);
                Assert.That(info.Exists, Is.True, relative);
                Assert.That(info.Length, Is.InRange(1L, 2L * 1024 * 1024));
                Assert.That((info.Attributes & FileAttributes.ReparsePoint) == 0, Is.True, relative);
                byte[] bytes = File.ReadAllBytes(path);
                rows.Add(new JObject { ["path"] = relative, ["bytes"] = bytes.Length, ["sha256"] = Sha(bytes) });
            }
            return new JObject { ["files"] = rows };
        }

        private static JObject AssemblyIdentity()
        {
            var rows = new JArray();
            foreach (Assembly assembly in new[] { typeof(EpisodeEngine).Assembly, typeof(EpisodeSaveStore).Assembly,
                typeof(DialogueCampaignV1WriterCaptureTests).Assembly })
            {
                var info = new FileInfo(assembly.Location);
                Assert.That(info.Exists, Is.True);
                Assert.That(info.Length, Is.InRange(1L, 64L * 1024 * 1024));
                Assert.That((info.Attributes & FileAttributes.ReparsePoint) == 0, Is.True);
                rows.Add(new JObject
                {
                    ["name"] = assembly.GetName().Name, ["fullName"] = assembly.FullName,
                    ["mvid"] = assembly.ManifestModule.ModuleVersionId.ToString("D"),
                    ["fileName"] = info.Name, ["bytes"] = info.Length, ["sha256"] = Sha(File.ReadAllBytes(info.FullName)),
                });
            }
            return new JObject { ["files"] = rows };
        }

        private static JObject WireState(byte[] envelope)
        {
            var parsed = ParseObject(Encoding.UTF8.GetString(envelope));
            Assert.That((string)parsed["format"], Is.EqualTo("gamesim-unity-save"));
            Assert.That((int)parsed["version"], Is.EqualTo(1));
            Assert.That(parsed["state"], Is.TypeOf<JObject>());
            Assert.That((int)parsed["state"]["schemaVersion"], Is.EqualTo(29));
            return (JObject)parsed["state"].DeepClone();
        }

        private static JObject ParseObject(string text)
        {
            using var input = new StringReader(text);
            using var reader = new JsonTextReader(input)
            { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64 };
            var result = JObject.Load(reader);
            Assert.That(reader.Read(), Is.False);
            return result;
        }

        private static string Canonical(JToken token) => Ordered(token).ToString(Formatting.None);
        private static JToken Ordered(JToken token)
        {
            if (token is JObject obj)
                return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => new JProperty(p.Name, Ordered(p.Value))));
            if (token is JArray array) return new JArray(array.Select(Ordered));
            return token.DeepClone();
        }
        private static string Sha(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private EpisodeCommand Choice(EpisodeState state, string option)
        {
            var episode = DialogueRules.Active(state);
            Assert.That(episode, Is.Not.Null);
            var command = Command(state, EpisodeCommandKind.ChooseDialogue);
            command.targetId = episode.id; command.text = DialogueRules.Cycle(state, episode).beatId;
            command.secondTargetId = option; return command;
        }
        private EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = NewId(), actorId = state.playerId, expectedRevision = state.revision, expectedPhase = state.phase, kind = kind,
        };
        private string NewId() => "v1-writer-capture-" + (++nonce).ToString(CultureInfo.InvariantCulture);
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        private static void AssertPaidNode(EpisodeState s, string id, string node)
        {
            var active = DialogueRules.Active(s);
            Assert.That(active?.id, Is.EqualTo(id));
            Assert.That(active.contentVersion, Is.EqualTo(1));
            Assert.That(DialogueRules.Cycle(s, active).templateId, Is.EqualTo(DialogueCampaignV1.TemplateId));
            Assert.That(DialogueRules.Cycle(s, active).beatId, Is.EqualTo(node));
            Assert.That(Remaining(s), Is.Zero);
        }
        private static void AssertFreeCounters(EpisodeState before, EpisodeState after)
        {
            Assert.That(Remaining(after), Is.Zero);
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints));
            Assert.That(after.moveInExtrasSpent, Is.EqualTo(before.moveInExtrasSpent));
        }
        private static void AssertTerminal(EpisodeState state, string id, string ending)
        {
            Valid(state);
            var episode = state.dialogue.episodes.Single(row => row.id == id);
            var cycle = state.storylines.Single(row => row.id == id);
            Assert.That(DialogueRules.Active(state), Is.Null);
            Assert.That(cycle.templateId, Is.EqualTo(DialogueCampaignV1.TemplateId));
            Assert.That(episode.contentVersion, Is.EqualTo(1));
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(cycle.endingId, Is.EqualTo(ending));
            Assert.That(cycle.path.Count, Is.EqualTo(3));
            Assert.That(cycle.path.Select(row => row.optionId), Is.EqualTo(new[]
                { "open-case", "appeal-personally", ending == "answered" ? "make-plea" : ending == "neutral" ? "keep-vote-open" : "leave" }));
            Assert.That(state.houseEvents.Count(row => row.cycleId == id), Is.EqualTo(3));
            Assert.That(state.houseEvents.Where(row => row.cycleId == id).All(row => row.resolved), Is.True);
            Assert.That(episode.responses.Count, Is.EqualTo(3));
            Assert.That(episode.evidence, Is.Empty);
            Assert.That(episode.time, Is.Not.Null);
            Assert.That(episode.time.reason, Is.EqualTo(ending == "lapsed" ? "occasion-closed" : ending));
            Assert.That(new[] { episode.time.proposedDelta, episode.time.appliedDelta, episode.time.spentRevision,
                episode.time.spentSequence, episode.time.spentWindowAction }, Is.All.EqualTo(0));
            if (ending == "answered")
            {
                var lobby = state.lobbies.Single(row => row.week == state.week && row.deciderId == episode.npcId
                    && row.ask == LobbyAsk.Vote && row.approach == LobbyApproach.Emotional);
                Assert.That(episode.responses.Last().responseKey, Is.EqualTo(DialogueCampaignV1.TemplateId + "/vote-" + lobby.response));
            }
            else Assert.That(episode.responses.Last().responseKey,
                Is.EqualTo(DialogueCampaignV1.TemplateId + (ending == "neutral" ? "/no-promise-made" : "/occasion-closed")));
            if (ending == "lapsed")
            {
                Assert.That(state.phase, Is.EqualTo(EpisodePhase.Eviction));
                Assert.That(cycle.path.Last().result, Is.EqualTo(StoryResults.Lapsed));
            }
            Assert.That(Remaining(state), Is.Zero);
        }
    }
}
