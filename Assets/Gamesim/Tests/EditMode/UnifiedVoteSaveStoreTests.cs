using System;
using System.Collections.Generic;
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
    /// Vote family V6: mode 2 through the durable store, on a private temporary slot. A fresh season as the director starts one
    /// (<see cref="UnifiedVotePublicCommandTests.Fresh"/>, mode 2) played through the public engine is saved and loaded at every phase
    /// it reaches and past every reveal, its archive frames and canonical rows exact; the backup rotates and its recovery returns
    /// mode 2 unchanged; a corrupt mode-2 primary is refused, offered nothing but the backup, and both copies are kept; a failed
    /// save leaves the slot as it was and the retry saves exactly the prepared reveal. A mode-1 save loads as mode 1, a historical
    /// schema-27 payload claiming mode 2 never loads, the inert 27-to-28 step keeps whatever mode it was given, and the web
    /// importer's season is mode 0: nothing but a fresh season selects the unified vote rules.
    /// <para>EditMode only: the store and the migrations are Gamesim.Persistence's.</para>
    /// </summary>
    public sealed class UnifiedVoteSaveStoreTests
    {
        private const uint Seed = 1;

        /// <summary>One public mode-2 season to its end, with every state the store is asked to keep: each phase reached, and Results after each reveal.</summary>
        private static List<EpisodeState> season;

        private static List<EpisodeState> Season()
        {
            if (season != null) return season;
            var kept = new List<EpisodeState>();
            var engine = new EpisodeEngine(UnifiedVotePublicCommandTests.Fresh(Seed));
            kept.Add(engine.Snapshot);
            for (int step = 0; step < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = engine.Snapshot;
                var after = UnifiedVotePublicCommandTests.Step(engine, Seed, out _).state;
                if (after.phase != s.phase || (!s.evictionResolved && after.evictionResolved)) kept.Add(after);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "Fixture: the season reaches the jury's verdict.");
            Assert.That(kept.All(k => k.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version), Is.True, "Fixture: mode 2 throughout.");
            Assert.That(kept.Any(k => k.unifiedVoteReveals.Count > 0 && k.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote
                && (r.status == DealStatus.Fulfilled || r.status == DealStatus.Broken))), Is.True, "Fixture: decided Vote rows and archive frames are kept.");
            return season = kept;
        }

        /// <summary>A state past a reveal that holds decided Vote rows: the save the corruption cases start from.</summary>
        private static EpisodeState Decided() => Season().First(k => k.unifiedVoteReveals.Count > 0 && k.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote
            && (r.status == DealStatus.Fulfilled || r.status == DealStatus.Broken))).Clone();

        [Test]
        public void AModeTwoSeasonRoundTripsAtEveryPhaseItReachesAndPastEveryReveal()
        {
            using var files = new Files();
            byte[] previous = null;
            int frames = 0;
            foreach (var state in Season())
            {
                files.Store.Save(state);
                if (previous != null) Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(previous), "The previous save rotated to the backup.");
                previous = File.ReadAllBytes(files.Store.SavePath);
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, state.week + " " + state.phase + ": " + message);
                Equivalent(state, loaded);
                frames = Math.Max(frames, loaded.unifiedVoteReveals.Count);
                Assert.That(Directory.GetFiles(files.Root, "*.pending-*"), Is.Empty);
            }
            Assert.That(frames, Is.GreaterThan(1), "Saves past more than one reveal carried their archive.");
        }

        /// <summary>A loaded mode-2 season plays on exactly as the one that was saved: the same next command gives the same state.</summary>
        [Test]
        public void ALoadedModeTwoSeasonPlaysOnAsTheSavedOne()
        {
            using var files = new Files();
            foreach (var state in Season().Where((k, i) => i % 7 == 3 && k.phase != EpisodePhase.Finished))
            {
                files.Store.Save(state);
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
                var next = ModeTwoReaderSweep.Next(state);
                var original = new EpisodeEngine(state).Apply(next);
                var reloaded = new EpisodeEngine(loaded).Apply(next);
                Assert.That(reloaded.accepted, Is.EqualTo(original.accepted), state.week + " " + state.phase);
                Assert.That(PinnedVoteSeason.Json(reloaded.state), Is.EqualTo(PinnedVoteSeason.Json(original.state)), state.week + " " + state.phase);
            }
        }

        [Test]
        public void TheBackupRotatesAndItsRecoveryReturnsModeTwoUnchanged()
        {
            using var files = new Files();
            var first = Decided();
            var second = Season().Last();
            files.Store.Save(first);
            byte[] firstBytes = File.ReadAllBytes(files.Store.SavePath);
            files.Store.Save(second);
            byte[] secondBytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(firstBytes));
            Assert.That(files.Store.TryRecoverBackup(out var recovered, out string message), Is.True, message);
            Equivalent(first, recovered);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(firstBytes), "The backup is installed as the primary,");
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(firstBytes), "and stays the backup.");
            Assert.That(File.ReadAllBytes(Directory.GetFiles(files.Root, "*.before-recovery-*.json").Single()), Is.EqualTo(secondBytes),
                "The primary it replaced is kept beside it.");
            Assert.That(files.Store.TryLoad(out var loaded, out message), Is.True, message);
            Equivalent(first, loaded);
        }

        /// <summary>
        /// A checksummed primary whose mode-2 season the complete core refuses: never returned, never repaired, never overwritten by a
        /// save; the good backup is offered and recovered on request, and both files stay as they were until then.
        /// </summary>
        [TestCase("frame-removed")] [TestCase("ballot-changed")] [TestCase("verdict-rewritten")] [TestCase("raw-vote-mirror")]
        [TestCase("deal-pass-later")] [TestCase("trust-weight")] [TestCase("mode-three")]
        public void ACorruptModeTwoPrimaryIsRefusedAndBothCopiesAreKept(string defect)
        {
            using var files = new Files();
            var good = Decided();
            files.Store.Save(good);
            byte[] backup = File.ReadAllBytes(files.Store.SavePath);
            var corrupt = Season().Last().Clone();
            var row = corrupt.unifiedCommitments.First(r => r.kind == UnifiedVoteTogether.Vote && (r.status == DealStatus.Fulfilled || r.status == DealStatus.Broken));
            switch (defect)
            {
                case "frame-removed": corrupt.unifiedVoteReveals.RemoveAt(0); break;
                case "ballot-changed":
                    var frame = corrupt.unifiedVoteReveals.Single(f => f.week == row.settledWeek);
                    var nominees = corrupt.ledger.power.Single(p => p.week == row.settledWeek).nominees;
                    var ballot = frame.ballots.First(b => b.voterId == row.makerId || b.voterId == row.beneficiaryId);
                    ballot.targetId = nominees.First(id => id != ballot.targetId);
                    break;
                case "verdict-rewritten":
                    row.status = row.status == DealStatus.Fulfilled ? DealStatus.Broken : DealStatus.Fulfilled;
                    row.brokenById = row.status == DealStatus.Broken ? row.makerId : null;
                    row.settlementEffectKey = row.status == DealStatus.Broken ? UnifiedVoteHistory.Key(row, row.settledWeek) : null;
                    break;
                case "raw-vote-mirror":
                    corrupt.deals.Add(new DealState { id = "deal-player-" + (corrupt.nextSequence - 1), proposerId = corrupt.playerId,
                        recipientId = row.makerId == corrupt.playerId ? row.beneficiaryId : row.makerId, type = DealKind.VoteTogether,
                        status = DealStatus.Expired, week = 1, expiresWeek = 1, trustImpact = DealKind.DefaultTrust(DealKind.VoteTogether) });
                    break;
                case "deal-pass-later": corrupt.dealRulesStartWeek = 2; break;
                case "trust-weight": row.trustImpact = DealTrust.Critical; break;
                default: corrupt.unifiedCommitmentRulesVersion = 3; break;
            }
            Assert.That(EpisodeValidation.TryValidate(corrupt, out _), Is.False, "Fixture: the defect is one the core refuses.");
            files.Write(files.Store.SavePath, corrupt);
            files.Write(files.Store.BackupPath, null, backup);
            var retained = files.Image();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False, defect);
                Assert.That(loaded, Is.Null, "A refused season is never returned.");
                Assert.That(message, Does.Not.Contain("checksum"), "The checksum holds: the season itself is refused.");
                Assert.That(message, Does.Contain("Recover backup"), "The good backup is offered, never installed by itself.");
                Assert.Throws<InvalidDataException>(() => files.Store.Save(good), "No save overwrites a primary it cannot read.");
                files.AssertImage(retained);
            }
            Assert.That(files.Store.TryRecoverBackup(out var recovered, out string recovery), Is.True, recovery);
            Equivalent(good, recovered);
        }

        /// <summary>
        /// The save-before-install boundary at a reveal (EpisodeDirector.Durable): the reveal's candidate is saved before it is
        /// installed. When the save fails the slot is as it was and the season is not advanced; the retry saves exactly the prepared
        /// reveal - its frame and its stamped rows - and nothing is played twice.
        /// </summary>
        [Test]
        public void AFailedSaveOfARevealLeavesTheSlotAndTheRetrySavesExactlyThePreparedReveal()
        {
            using var files = new Files();
            int at = Season().FindIndex(k => k.unifiedVoteReveals.Count > 0 && k.phase == EpisodePhase.Eviction && k.evictionResolved);
            Assert.That(at, Is.GreaterThan(0), "Fixture: a reveal the season reached.");
            var revealed = Season()[at];
            var before = Season().Take(at).Last(k => k.phase == EpisodePhase.Eviction && !k.evictionResolved);
            files.Store.Save(before);
            files.Store.Save(before);
            var retained = files.Image();
            using (new FileStream(files.Store.SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // The swap needs the primary; held open, the save fails as a held file does.
                Assert.That(() => files.Store.Save(revealed), Throws.InstanceOf<IOException>());
            }
            files.AssertImage(retained, "*.pending-*");
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Equivalent(before, loaded);
            files.Store.Save(revealed);
            Assert.That(files.Store.TryLoad(out loaded, out message), Is.True, message);
            Equivalent(revealed, loaded);
            Assert.That(loaded.unifiedVoteReveals.Count, Is.EqualTo(before.unifiedVoteReveals.Count + 1), "The reveal's frame, once.");
        }

        /// <summary>D6: a season recorded in mode 1 loads as mode 1, its Vote rows raw and its archive empty; mode 0 as mode 0.</summary>
        [TestCase(0)] [TestCase(1)]
        public void AnOlderModesSaveLoadsInItsOwnMode(int mode)
        {
            using var files = new Files();
            var engine = new EpisodeEngine(PinnedVoteSeason.Fresh(Seed, mode));
            // Past the first reveal, into the second week: the season's raw Vote rows decided, as its mode keeps them.
            for (int step = 0; step < 4000 && engine.Snapshot.week < 2; step++)
            {
                var s = engine.Snapshot;
                var own = ModeTwoReaderSweep.Busy(s, Seed, 0);
                var result = own == null ? null : engine.Apply(own);
                if (result == null || !result.accepted) Assert.That(engine.Apply(ModeTwoReaderSweep.Next(s)).accepted, Is.True);
            }
            var state = engine.Snapshot;
            files.Store.Save(state);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That((loaded.unifiedCommitmentRulesVersion, loaded.unifiedVoteReveals.Count), Is.EqualTo((mode, 0)));
            Assert.That(loaded.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote), Is.False);
            Assert.That(JToken.DeepEquals(Payload(loaded), Payload(state)), Is.True, "Loaded exactly, never converted.");
        }

        /// <summary>
        /// No historical save carries mode 2: a schema-27 payload claiming it is refused by the fixed 27 contract, by the step to 28 and by
        /// the store; and the step keeps whatever mode a genuine 27 payload holds (FrozenEpisodeV27ContractTests' "authority-two" pins the
        /// contract alone).
        /// </summary>
        [TestCase(0)] [TestCase(1)]
        public void AHistoricalPayloadNeverReachesModeTwo(int mode)
        {
            var state = PinnedVoteSeason.Fresh(Seed, mode);
            var former = Payload(state);
            PersistenceMigrationTests.StripSchema28(former);
            Fixed27(former);
            var upgraded = EpisodeSaveMigrations.UpgradeV27ToV28(former);
            Assert.That((int)upgraded["unifiedCommitmentRulesVersion"], Is.EqualTo(mode), "The step copies the mode it is given.");
            Assert.That((int)EpisodeSaveMigrations.PrepareCurrentPayload(former, out bool migrated)["unifiedCommitmentRulesVersion"], Is.EqualTo(mode));
            Assert.That(migrated, Is.True);

            var claimed = (JObject)former.DeepClone();
            claimed["unifiedCommitmentRulesVersion"] = UnifiedVoteFamilyValidation.Version;
            Assert.Throws<InvalidDataException>(() => Fixed27(claimed));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV27ToV28(claimed));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(claimed, out _));
            using var files = new Files();
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(claimed));
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False, message);
            Assert.That(loaded, Is.Null);
        }

        /// <summary>
        /// A mode-1 season saved after V6 is still mode 1 when loaded, saved again and recovered from its backup: neither the
        /// store nor its recovery converts a season.
        /// </summary>
        [Test]
        public void SavingAndRecoveringAModeOneSeasonKeepsItModeOne()
        {
            using var files = new Files();
            var state = PinnedVoteSeason.Fresh(Seed, 1);
            files.Store.Save(state);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            files.Store.Save(loaded);
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(again.unifiedCommitmentRulesVersion, Is.EqualTo(1));
            Assert.That(JToken.DeepEquals(Payload(again), Payload(state)), Is.True);
            Assert.That(files.Store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Assert.That(recovered.unifiedCommitmentRulesVersion, Is.EqualTo(1));
            Assert.That(JToken.DeepEquals(Payload(recovered), Payload(state)), Is.True);
        }

        // ------------------------------------------------------------ helpers

        private static void Equivalent(EpisodeState expected, EpisodeState actual)
        {
            Assert.That(actual, Is.Not.Null);
            EpisodeSaveValidation.Validate(actual);
            Assert.That(actual.unifiedCommitmentRulesVersion, Is.EqualTo(expected.unifiedCommitmentRulesVersion));
            Assert.That(JToken.DeepEquals(Payload(expected), Payload(actual)), Is.True,
                "Rules, canonical rows, archive frames, RNG, history and receipts load exactly, never converted.");
        }

        private static void Fixed27(JObject payload)
        {
            var type = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV27", true);
            try { type.GetMethod("Validate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { payload }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());

        private sealed class Files : IDisposable
        {
            private const string Prefix = "GamesimUnifiedVoteSaveStore-";
            private readonly string leaf = Prefix + Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;

            internal Files()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Guard();
                Directory.CreateDirectory(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }

            /// <summary>A checksummed envelope of a state, or exact bytes, at the primary or the backup.</summary>
            internal void Write(string target, EpisodeState s, byte[] bytes = null)
            {
                Guard();
                Assert.That(target == Store.SavePath || target == Store.BackupPath, Is.True);
                if (bytes != null) File.WriteAllBytes(target, bytes);
                else File.WriteAllText(target, PersistenceMigrationTests.Envelope(Payload(s)));
            }

            internal SortedDictionary<string, byte[]> Image(string except = null)
            {
                Guard();
                Assert.That(Directory.GetDirectories(Root), Is.Empty);
                var skipped = except == null ? new HashSet<string>() : new HashSet<string>(Directory.GetFiles(Root, except).Select(Path.GetFileName));
                return new SortedDictionary<string, byte[]>(Directory.GetFiles(Root).Where(f => !skipped.Contains(Path.GetFileName(f)))
                    .ToDictionary(Path.GetFileName, File.ReadAllBytes), StringComparer.Ordinal);
            }

            internal void AssertImage(SortedDictionary<string, byte[]> expected, string except = null)
            {
                var actual = Image(except);
                Assert.That(actual.Keys, Is.EqualTo(expected.Keys), "Refusal creates, deletes or renames no slot file.");
                foreach (var file in expected)
                    Assert.That(actual[file.Key], Is.EqualTo(file.Value), "Every retained slot byte remains unchanged: " + file.Key);
            }

            private void Guard()
            {
                string resolved = Path.GetFullPath(Root);
                string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(resolved), Is.EqualTo(parent));
                Assert.That(Path.GetFileName(resolved), Is.EqualTo(leaf));
                Assert.That(leaf, Does.StartWith(Prefix));
                if (Directory.Exists(resolved))
                    Assert.That(File.GetAttributes(resolved) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0),
                        "The private slot root must never be redirected before reading, writing or cleanup.");
            }

            public void Dispose()
            {
                Guard();
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
