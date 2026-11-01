using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The live house's saves (LIVE-SAVE-PLAN.md): the house's own operations - its clock seconds and the
    /// conversations its people start and cancel - are installed in memory and reach disk together, every thirty
    /// seconds of free time and at every way the player leaves it, and a failed write is judged against the last
    /// save. Unscaled time is not pinned by Time.captureDeltaTime, so the waits below are real seconds, bounded.
    /// Also the verification's commit timing log, which records how long each part of a house commit takes.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        // The most free time an installed operation waits for its write: the owner's thirty seconds, the 0.1 s
        // sample that notices it, and the frame this test reads it in.
        private const double LiveSaveBound = NpcSaveCadence.Seconds + 1.2;

        [UnityTest]
        public IEnumerator LiveSave_EveryInstalledRevisionIsOnDiskWithinThirtySecondsOfFreeTime()
        {
            yield return LiveSaveStart();
            // The real house from here: its own routes, starts, cancellations and clock, on real time.
            NpcWrite("npcApproachDiagnosticsSuppressed", false);
            var watch = new LiveSaveWatch();
            LiveSaveWatchWrites(watch);
            try { yield return LiveSaveWatchFor(watch, 60f); }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            Assert.That(watch.InstalledAt, Is.Not.Empty, "The house lived in the minute.");
            Assert.That(watch.Writes.Count, Is.InRange(1, 3),
                "One write a thirty-second window of the house's activity, not one a second: " + watch.Describe());
            foreach (var installed in watch.InstalledAt)
            {
                if (watch.Free - installed.Value < LiveSaveBound) continue; // Not yet due when the minute ended.
                var first = watch.Writes.FirstOrDefault(write => write.revision >= installed.Key);
                Assert.That(first.revision, Is.GreaterThanOrEqualTo(installed.Key),
                    "Revision " + installed.Key + " was never written: " + watch.Describe());
                Assert.That(first.free - installed.Value, Is.LessThanOrEqualTo(LiveSaveBound),
                    "Revision " + installed.Key + " waited longer than thirty seconds of free time: " + watch.Describe());
            }
        }

        /// <summary>
        /// The thirty seconds are the house's free time, not its clock: the clock stops while a pending pair walks
        /// back to its venue, and a cadence counted on it would hold the house's activity unsaved for minutes.
        /// </summary>
        [UnityTest]
        public IEnumerator LiveSave_TheThirtySecondsAreFreeTimeWhileTheHousesClockIsStopped()
        {
            var fixture = NpcPendingRuntimeFixture();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            yield return WaitForNpcRuntimeBinding();
            NpcInvoke("SetNpcWorldPaused", false);
            NpcInvoke("RestorePendingNpcMeetings");
            Assert.That(NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld").ContainsKey(1), Is.True);
            Assert.That(director.IsNpcConversationPhysicallyReady(1), Is.False, "The clock is stopped: the saved pair has still to reunite.");
            // Nothing can be installed here - no tick while the pair walks, no start without an arrival, no
            // cancellation for twenty seconds - so the cadence is primed through its own methods: 29.5 s owed.
            var cadence = LiveSaveCadence;
            cadence.Begin(director.Snapshot); cadence.Installed(); cadence.Elapsed(29.5);
            var written = LiveSavePassThrough();
            try
            {
                float deadline = Time.realtimeSinceStartup + 3f;
                while (written.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
            Assert.That(written.Count, Is.EqualTo(1), "Half a second of free time later the house writes.");
            Assert.That(written[0].npcSocial.clockTick, Is.EqualTo(fixture.npcSocial.clockTick), "...with its clock still stopped.");
            Assert.That(cadence.Clean, Is.True);
        }

        [UnityTest]
        public IEnumerator LiveSave_ADecisionAfterTenUnsavedSecondsWritesThemWithIt()
        {
            yield return LiveSaveStart();
            LiveSaveOwe(10);
            var owed = director.Snapshot;
            var result = director.Submit(LiveSaveTalk(owed, "live-save-decision"));
            Assert.That(result.accepted, Is.True, result.reason);
            var disk = LiveSaveDisk();
            Assert.That(disk.npcSocial.clockTick, Is.EqualTo(owed.npcSocial.clockTick), "The decision's write carries the house's ten seconds.");
            AssertEquivalent(director.Snapshot, disk);
            Assert.That(LiveSaveCadence.Clean, Is.True);
        }

        [UnityTest]
        public IEnumerator LiveSave_EveryWayTheHouseStopsWritesItsSecondsFirst()
        {
            yield return LiveSaveStart();

            LiveSaveOwe(2); var owed = director.Snapshot;
            director.OpenJournal();
            LiveSaveAssertWritten("the notebook", owed);
            LiveSaveResume();

            LiveSaveOwe(2); owed = director.Snapshot;
            director.OpenSettings();
            LiveSaveAssertWritten("the settings", owed);
            LiveSaveResume();

            LiveSaveOwe(2); owed = director.Snapshot;
            yield return OpenNearbyNpc(SceneComponents<HouseNpc>().First(npc => npc.Id == ContentCatalog.MayaId && npc.gameObject.activeInHierarchy));
            LiveSaveAssertWritten("a conversation", owed);
            LiveSaveResume();

            LiveSaveOwe(2); owed = director.Snapshot;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            LiveSaveAssertWritten("the phase panel", owed);
            LiveSaveResume();

            // The Unity messages are off in Editor test runs (LiveSave_FocusAndPauseDoNothingInEditorTestRuns):
            // the writes they make are reached directly.
            LiveSaveOwe(2); owed = director.Snapshot;
            Assert.That((bool)NpcInvoke("FlushForLeaving", "focus", false), Is.True);
            LiveSaveAssertWritten("switching away", owed);

            LiveSaveOwe(2); owed = director.Snapshot;
            Assert.That((bool)NpcInvoke("FlushForLeaving", "pause", false), Is.True);
            LiveSaveAssertWritten("a pause", owed);

            LiveSaveOwe(2); owed = director.Snapshot;
            NpcInvoke("OnApplicationQuit");
            LiveSaveAssertWritten("quitting", owed);
            Assert.That(NpcRead<string>("persistenceNotice"), Is.Null, "A quit that saves shows nothing.");

            // A stop no opener saved: a failed world, the season report, any future panel. Zero delta reaches the
            // paused branch before the delta check.
            LiveSaveOwe(2); owed = director.Snapshot;
            NpcWrite("npcWorldFailed", true);
            try { NpcInvoke("TickNpcSocialRuntime", 0f); }
            finally { NpcWrite("npcWorldFailed", false); }
            LiveSaveAssertWritten("a stopped world", owed);
            LiveSaveResume();

            LiveSaveOwe(2); owed = director.Snapshot;
            director.SuspendNpcAutonomyForDiagnostics();
            LiveSaveAssertWritten("a test's hold", owed);
            NpcWrite("npcDiagnosticsSuspended", false);
            yield return WaitForNpcRuntimeBinding();

            // A phase change is a decision: its write carries the house's seconds.
            LiveSaveOwe(2); owed = director.Snapshot;
            var advance = director.Submit(new EpisodeCommand
            {
                id = "live-save-advance", actorId = owed.playerId, expectedRevision = owed.revision,
                expectedPhase = owed.phase, kind = EpisodeCommandKind.Advance
            });
            Assert.That(advance.accepted, Is.True, advance.reason);
            Assert.That(director.Snapshot.phase, Is.Not.EqualTo(owed.phase));
            Assert.That(LiveSaveDisk().npcSocial.clockTick, Is.EqualTo(owed.npcSocial.clockTick));
            AssertEquivalent(director.Snapshot, LiveSaveDisk());
            Assert.That(LiveSaveCadence.Clean, Is.True);
        }

        [UnityTest]
        public IEnumerator LiveSave_ACrashLosesAtMostThirtySecondsOfTheHouseAndNoDecision()
        {
            yield return LiveSaveStart();
            var decided = director.Submit(LiveSaveTalk(director.Snapshot, "live-save-before-the-crash"));
            Assert.That(decided.accepted, Is.True, decided.reason);
            long decision = director.Snapshot.revision;
            NpcWrite("npcApproachDiagnosticsSuppressed", false);
            var watch = new LiveSaveWatch();
            LiveSaveWatchWrites(watch);
            try { yield return LiveSaveWatchFor(watch, 40f); }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
            Assert.That(watch.Writes.Count, Is.GreaterThanOrEqualTo(1),
                "Forty seconds cross the house's thirty-second write, or a lost window of under thirty proves nothing: " + watch.Describe());

            // As a kill does: the outgoing director writes nothing more, and the next one reads what is on disk.
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            var loaded = director.Snapshot;
            Assert.That(loaded.revision, Is.GreaterThanOrEqualTo(decision), "No decision is lost.");
            foreach (var installed in watch.InstalledAt)
                if (watch.Free - installed.Value > LiveSaveBound)
                    Assert.That(loaded.revision, Is.GreaterThanOrEqualTo(installed.Key),
                        "Revision " + installed.Key + " was more than thirty seconds old and lost: " + watch.Describe());
        }

        [UnityTest]
        public IEnumerator LiveSave_AFailedPeriodicWriteStopsTheHouseAsAPlainFailureAndTheRetryWritesTheSameState()
        {
            yield return LiveSaveStart();
            LiveSaveOwe(2);
            var files = LiveSaveFiles(director.SavePath);
            var attempts = new List<EpisodeState>();
            try
            {
                LiveSaveFailAPeriodicWrite(attempts);
                var held = director.Snapshot;
                Assert.That(attempts.Count, Is.EqualTo(1));
                AssertEquivalent(held, attempts[0]);
                Assert.That(director.SeasonInProgress, Is.True, "A plain failure, not the recovery lock: the disk is the last save.");
                Assert.That(director.StatusMessage, Does.StartWith("SAVE NEEDS ATTENTION"));
                Assert.That(director.StatusMessage, Does.Contain("retry Save or reload"));
                LiveSaveAssertFiles(director.SavePath, files);
                for (int sample = 0; sample < 4; sample++) NpcInvoke("TickNpcSocialRuntime", .5f);
                Assert.That(attempts.Count, Is.EqualTo(1), "Nothing on the house's own clock retries a stopped house: not its period, not its stop.");
                AssertEquivalent(held, director.Snapshot);
                LiveSaveAssertFiles(director.SavePath, files);

                var written = LiveSavePassThrough();
                director.SaveNow();
                Assert.That(written.Count, Is.EqualTo(1));
                AssertEquivalent(held, written[0]);
                AssertEquivalent(held, director.Snapshot);
                AssertEquivalent(held, LiveSaveDisk());
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.False);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator LiveSave_SaveNowWritesWhileAPairIsStillWalking()
        {
            var fixture = NpcPendingRuntimeFixture();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            yield return WaitForNpcRuntimeBinding();
            NpcInvoke("SetNpcWorldPaused", false);
            NpcInvoke("RestorePendingNpcMeetings");
            Assert.That(director.IsNpcConversationPhysicallyReady(1), Is.False, "The saved pair has still to reunite.");
            Assert.That(LiveSaveCadence.Clean, Is.True, "Nothing can be installed while the pair walks.");
            var before = director.Snapshot;
            NpcWrite("npcFrameFraction", .95d);
            NpcWrite("npcWorldFraction", .06d);
            var written = LiveSavePassThrough();
            try
            {
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.False, "The walking pair holds the clock.");
                director.SaveNow();
                Assert.That(written.Count, Is.EqualTo(1), "Save writes although the walking pair holds the clock.");
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
            Assert.That(NpcRead<double>("npcFrameFraction"), Is.EqualTo(.95d));
            Assert.That(NpcRead<double>("npcWorldFraction"), Is.EqualTo(.06d));
            AssertEquivalent(before, director.Snapshot);
            AssertEquivalent(before, LiveSaveDisk());
        }

        [UnityTest]
        public IEnumerator LiveSave_ANewSeasonWhoseOldSlotCannotBeWrittenRefusesOncePerFailure()
        {
            yield return LiveSaveStart();
            var lastSave = LiveSaveDisk();
            LiveSaveOwe(2);
            string oldPath = director.SavePath;
            var files = LiveSaveFiles(oldPath);
            var attempts = new List<EpisodeState>();
            string refusal = null;
            NpcWrite("saveCandidateForDiagnostics", new Action<EpisodeState>(candidate =>
            { attempts.Add(candidate); throw new IOException("Injected outgoing slot write failure."); }));
            try
            {
                director.StartSeason(null, reason => refusal = reason);
                Assert.That(attempts.Count, Is.EqualTo(1));
                Assert.That(director.SavePath, Is.EqualTo(oldPath), "Refused: the season stays in its slot.");
                Assert.That(refusal, Does.StartWith("SAVE NEEDS ATTENTION"), "The screen that started it is told why.");
                LiveSaveAssertFiles(oldPath, files);

                refusal = null;
                director.StartSeason(null, reason => refusal = reason);
                Assert.That(attempts.Count, Is.EqualTo(2), "The next start tries the write once more...");
                Assert.That(director.SavePath, Is.Not.EqualTo(oldPath), "...and goes ahead: a failure that persists never traps the player.");
                Assert.That(refusal, Is.Null);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
            LiveSaveAssertFiles(oldPath, files);
            Assert.That(new EpisodeSaveStore(oldPath).TryLoad(out var old, out var reason), Is.True, reason);
            AssertEquivalent(lastSave, old);

            // A house a failed periodic write stopped, then a writer that works: the start retries and writes it.
            yield return WaitForNpcRuntimeBinding();
            NpcResetTransientFractions();
            director.SaveNow();
            LiveSaveOwe(2);
            attempts.Clear();
            try
            {
                LiveSaveFailAPeriodicWrite(attempts);
                var owed = director.Snapshot;
                string secondPath = director.SavePath;
                var written = LiveSavePassThrough();
                director.StartSeason(null, reason => refusal = reason);
                Assert.That(written.Count, Is.EqualTo(1), "The start retries the stopped house's write...");
                Assert.That(director.SavePath, Is.Not.EqualTo(secondPath), "...and, written, starts.");
                Assert.That(refusal, Is.Null);
                Assert.That(new EpisodeSaveStore(secondPath).TryLoad(out var kept, out reason), Is.True, reason);
                AssertEquivalent(owed, kept);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator LiveSave_TheCommitTimingsRecordEachPartOnlyWhenAsked()
        {
            Assert.That(NpcRead<NpcCommitTimings>("commitTimingForVerification"), Is.Null,
                "Ordinary play and every Editor session leave the timing log off.");
            yield return LiveSaveStart();
            LiveSaveOwe(1);
            Assert.That(NpcRead<NpcCommitTimings>("commitTimingForVerification"), Is.Null, "A commit does not switch it on.");

            var timings = new NpcCommitTimings();
            NpcWrite("commitTimingForVerification", timings);
            try
            {
                NpcWrite("npcFrameFraction", 1d);
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True, director.StatusMessage);
                long tickRevision = director.Snapshot.revision;
                int frame = Time.frameCount;
                director.OpenJournal();
                director.SaveNow();
                var rows = LiveSaveTimingRows(timings);
                Assert.That(rows.Count, Is.EqualTo(3), timings.ToCsv());

                var tick = rows[0];
                Assert.That(tick["kind"], Is.EqualTo("tick"));
                Assert.That(tick["frame"], Is.EqualTo(frame.ToString(CultureInfo.InvariantCulture)));
                Assert.That(tick["revision"], Is.EqualTo(tickRevision.ToString(CultureInfo.InvariantCulture)));
                foreach (var part in new[] { "prepareMs", "validateMs", "engineMs", "projectMs" })
                    Assert.That(double.Parse(tick[part], CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(0), part);
                Assert.That(tick["persistMs"], Is.Empty, "A house tick is installed, not written.");
                Assert.That(tick["saved"], Is.Empty);

                var panel = rows[1];
                Assert.That(panel["kind"], Is.EqualTo(NpcCommitTimings.WriteKind));
                Assert.That(panel["cause"], Is.EqualTo("panel"), "The notebook's opening writes the installed seconds.");
                Assert.That(panel["revision"], Is.EqualTo(tickRevision.ToString(CultureInfo.InvariantCulture)));
                Assert.That(double.Parse(panel["persistMs"], CultureInfo.InvariantCulture), Is.GreaterThan(0));
                Assert.That(panel["saved"], Is.EqualTo("true"));
                Assert.That(panel["prepareMs"], Is.Empty);

                Assert.That(rows[2]["cause"], Is.EqualTo("save"));
                Assert.That(rows[2]["saved"], Is.EqualTo("true"));
            }
            finally { NpcWrite("commitTimingForVerification", null); }
        }

        [UnityTest]
        public IEnumerator LiveSave_AfterAFailedWriteTheNextWayOutWritesWhatTheMenusShow()
        {
            yield return LiveSaveStart();
            var attempts = new List<EpisodeState>();
            try
            {
                // The writer still failing: a menu opening tries once, opens all the same, and the notice stands.
                LiveSaveOwe(2);
                LiveSaveFailAPeriodicWrite(attempts);
                director.OpenJournal();
                Assert.That(attempts.Count, Is.EqualTo(2), "One retry as the notebook opens.");
                Assert.That(director.IsPanelOpen, Is.True, "The notebook opens all the same: Settings, the way to Save or reload, must.");
                Assert.That(NpcRead<string>("persistenceNotice"), Does.StartWith("SAVE NEEDS ATTENTION"));
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                director.ClosePanels();

                // The writer working again: the next opening writes what the menus show and clears the notice.
                var owed = director.Snapshot;
                var written = LiveSavePassThrough();
                director.OpenJournal();
                Assert.That(written.Count, Is.EqualTo(1));
                AssertEquivalent(owed, written[0]);
                AssertEquivalent(owed, LiveSaveDisk());
                Assert.That(NpcRead<string>("persistenceNotice"), Is.Null, "The retry that landed clears the failure's notice.");
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.False);
                director.ClosePanels();
                Assert.That(LiveSaveHouseCanAdvance, Is.True, "The house resumes once the notebook closes.");

                // An orderly quit after a failed write writes what the menus showed.
                LiveSaveOwe(2);
                attempts.Clear();
                LiveSaveFailAPeriodicWrite(attempts);
                owed = director.Snapshot;
                written = LiveSavePassThrough();
                NpcInvoke("OnApplicationQuit");
                Assert.That(written.Count, Is.EqualTo(1));
                AssertEquivalent(owed, LiveSaveDisk());

                // So does a new season after a failed write, into the slot it leaves.
                LiveSaveOwe(2);
                attempts.Clear();
                LiveSaveFailAPeriodicWrite(attempts);
                owed = director.Snapshot;
                string oldPath = director.SavePath;
                written = LiveSavePassThrough();
                director.StartSeason(null);
                Assert.That(written.Count, Is.EqualTo(1));
                Assert.That(director.SavePath, Is.Not.EqualTo(oldPath));
                Assert.That(new EpisodeSaveStore(oldPath).TryLoad(out var old, out var reason), Is.True, reason);
                AssertEquivalent(owed, old);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator LiveSave_AHeldSaveWithSecondsOwedLocksAndTheReloadReturnsToTheLastSave()
        {
            yield return LiveSaveStart();
            var known = LiveSaveDisk();
            LiveSaveOwe(2);
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            bool written;
            // Under Mono every share mode gives the same plain IOException, so an exclusive hold is the simple one.
            using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
                written = (bool)NpcInvoke("FlushUnsavedNpcTime", "periodic", false, false);
            Assert.That(written, Is.False);
            Assert.That(director.SeasonInProgress, Is.False,
                "A primary another program holds cannot be read, so the write locks recovery, as a decision's does.");
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));

            director.LoadNow();
            AssertEquivalent(known, director.Snapshot);
            Assert.That(director.SeasonInProgress, Is.True);
            Assert.That(LiveSaveCadence.Clean, Is.True,
                "The way out goes back to the last save: the owed seconds are gone, within the owner's thirty.");
            yield return WaitForNpcRuntimeBinding();
            Assert.That(LiveSaveHouseCanAdvance, Is.True, "The house resumes.");
        }

        [UnityTest]
        public IEnumerator LiveSave_ASaveRefusedOnWhatTheHouseHoldsAsksForAReload()
        {
            yield return LiveSaveStart();
            var known = LiveSaveDisk();
            long refusedFrom = known.npcSocial.clockTick + 1;
            var attempts = new List<EpisodeState>();
            NpcWrite("saveCandidateForDiagnostics", new Action<EpisodeState>(candidate =>
            {
                attempts.Add(candidate);
                if (candidate.npcSocial.clockTick >= refusedFrom) throw new InvalidDataException("Injected refusal of what the house holds.");
                new EpisodeSaveStore(director.SavePath).Save(candidate);
            }));
            try
            {
                LiveSaveOwe(2);
                LiveSaveCadence.Elapsed(NpcSaveCadence.Seconds);
                NpcResetTransientFractions();
                NpcInvoke("TickNpcSocialRuntime", .1f);
                Assert.That(attempts.Count, Is.EqualTo(1));
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.SeasonInProgress, Is.True, "Refused, not locked: the disk is the last save.");
                Assert.That(director.StatusMessage, Does.Contain("Reload to go back to your last save"));
                var held = director.Snapshot;

                var talk = director.Submit(LiveSaveTalk(held, "live-save-refused-talk"));
                Assert.That(talk.accepted, Is.False, "A decision is built on what memory holds, so it is refused too.");
                Assert.That(talk.reason, Does.Contain("The decision was not committed"));
                Assert.That(attempts.Count, Is.EqualTo(2));
                AssertEquivalent(held, director.Snapshot);

                director.SaveNow();
                Assert.That(attempts.Count, Is.EqualTo(3));
                Assert.That(director.StatusMessage, Does.Contain("Reload to go back to your last save"), "Retrying cannot save it.");
                AssertEquivalent(known, LiveSaveDisk());
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.LoadNow();
            AssertEquivalent(known, director.Snapshot);
            yield return WaitForNpcRuntimeBinding();
            Assert.That(LiveSaveHouseCanAdvance, Is.True, "The house resumes from the last save.");
            var decided = director.Submit(LiveSaveTalk(director.Snapshot, "live-save-talk-after-reload"));
            Assert.That(decided.accepted, Is.True, decided.reason);
            AssertEquivalent(director.Snapshot, LiveSaveDisk());
        }

        [UnityTest]
        public IEnumerator LiveSave_ARefusalInsideSaveNowWritesWhatIsInstalledAndKeepsItsNotice()
        {
            yield return LiveSaveStart();
            LiveSaveOwe(2);
            var installed = director.Snapshot;
            var written = LiveSavePassThrough();
            NpcWrite("npcOperationCheckForDiagnostics", new Action<EpisodeState>(candidate =>
            {
                if (candidate.npcSocial.clockTick > installed.npcSocial.clockTick)
                    throw new InvalidDataException("Injected refusal of the house's next second.");
            }));
            try
            {
                NpcWrite("npcFrameFraction", 1d); NpcWrite("npcWorldFraction", 0d);
                director.SaveNow();
                Assert.That(written.Count, Is.EqualTo(1), "One write, of what was installed, and none after the refusal.");
                AssertEquivalent(installed, written[0]);
                AssertEquivalent(installed, director.Snapshot);
                AssertEquivalent(installed, LiveSaveDisk());
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.StatusMessage, Does.StartWith("SAVE NEEDS ATTENTION: Injected refusal"));
                Assert.That(director.StatusMessage, Does.Not.Contain("Saved locally"));

                director.SaveNow(); // The explicit retry, through the suspended branch as before.
                Assert.That(written.Count, Is.EqualTo(2));
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.False);

                Assert.That(LiveSaveCadence.Clean, Is.True);
                director.SaveNow();
                Assert.That(written.Count, Is.EqualTo(2), "With nothing installed since the last save, a refusal writes nothing.");
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                AssertEquivalent(installed, director.Snapshot);
            }
            finally
            {
                NpcWrite("npcOperationCheckForDiagnostics", null);
                NpcWrite("saveCandidateForDiagnostics", null);
            }
        }

        [UnityTest]
        public IEnumerator LiveSave_FocusAndPauseDoNothingInEditorTestRuns()
        {
            yield return LiveSaveStart();
            LiveSaveOwe(2);
            var written = LiveSavePassThrough();
            try
            {
                NpcInvoke("OnApplicationFocus", false);
                NpcInvoke("OnApplicationPause", true);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
            Assert.That(written, Is.Empty, "Focus moves with the Game view and other programs in the Editor, at moments no test controls.");
            Assert.That(LiveSaveCadence.Clean, Is.False);
        }

        // The default house, bound, its routes held back so no pair walks, with a primary and a backup on disk and
        // nothing owed.
        private IEnumerator LiveSaveStart()
        {
            yield return WaitForNpcRuntimeBinding();
            NpcResetTransientFractions();
            director.SaveNow(); director.SaveNow();
            Assert.That(File.Exists(director.SavePath + ".backup"), Is.True);
            Assert.That(LiveSaveCadence.Clean, Is.True);
        }

        // Whole seconds installed the way the house installs them, through its own flush, none of them on disk.
        private void LiveSaveOwe(int seconds)
        {
            long saved = LiveSaveDisk().revision;
            var before = director.Snapshot;
            NpcResetTransientFractions();
            NpcWrite("npcFrameFraction", (double)seconds);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True, director.StatusMessage);
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(before.npcSocial.clockTick + seconds));
            Assert.That(LiveSaveDisk().revision, Is.EqualTo(saved), "Installed, not written.");
            Assert.That(LiveSaveCadence.Clean, Is.False);
        }

        // The house's thirty-second write, reached through its own sample, failing on the disk.
        private void LiveSaveFailAPeriodicWrite(List<EpisodeState> attempts)
        {
            NpcWrite("saveCandidateForDiagnostics", new Action<EpisodeState>(candidate =>
            { attempts.Add(candidate); throw new IOException("Injected periodic house write failure."); }));
            int before = attempts.Count;
            LiveSaveCadence.Elapsed(NpcSaveCadence.Seconds);
            NpcResetTransientFractions();
            NpcInvoke("TickNpcSocialRuntime", .1f);
            Assert.That(attempts.Count, Is.EqualTo(before + 1), director.StatusMessage);
            Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True, director.StatusMessage);
            Assert.That(director.SeasonInProgress, Is.True, "A plain failure, not the recovery lock.");
        }

        // A writer that passes every candidate to a store of the slot, and keeps them.
        private List<EpisodeState> LiveSavePassThrough()
        {
            var written = new List<EpisodeState>();
            NpcWrite("saveCandidateForDiagnostics", new Action<EpisodeState>(candidate =>
            { written.Add(candidate); new EpisodeSaveStore(director.SavePath).Save(candidate); }));
            return written;
        }

        private void LiveSaveAssertWritten(string way, EpisodeState owed)
        {
            AssertEquivalent(owed, director.Snapshot);
            var disk = LiveSaveDisk();
            Assert.That(disk.npcSocial.clockTick, Is.EqualTo(owed.npcSocial.clockTick), way + " writes the house's seconds first.");
            AssertEquivalent(owed, disk);
            Assert.That(LiveSaveCadence.Clean, Is.True, way);
        }

        private void LiveSaveResume()
        {
            director.ClosePanels();
            NpcInvoke("SetNpcWorldPaused", false);
            Assert.That(LiveSaveHouseCanAdvance, Is.True, director.StatusMessage);
        }

        private EpisodeState LiveSaveDisk()
        {
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out var reason), Is.True, reason);
            return disk;
        }

        private static byte[][] LiveSaveFiles(string path) => new[] { File.ReadAllBytes(path), File.ReadAllBytes(path + ".backup") };

        private static void LiveSaveAssertFiles(string path, byte[][] files)
        {
            CollectionAssert.AreEqual(files[0], File.ReadAllBytes(path), "The primary is untouched.");
            CollectionAssert.AreEqual(files[1], File.ReadAllBytes(path + ".backup"), "The backup is untouched.");
        }

        private static EpisodeCommand LiveSaveTalk(EpisodeState state, string id) => new EpisodeCommand
        {
            id = id, actorId = state.playerId, expectedRevision = state.revision, expectedPhase = state.phase,
            kind = EpisodeCommandKind.Talk, targetId = ContentCatalog.MayaId
        };

        private NpcSaveCadence LiveSaveCadence => NpcRead<NpcSaveCadence>("npcSaveCadence");

        private bool LiveSaveHouseCanAdvance
        {
            get
            {
                var property = typeof(EpisodeDirector).GetProperty("NpcCanAdvance", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(property, Is.Not.Null, "Missing runtime regression seam: NpcCanAdvance");
                return (bool)property.GetValue(director);
            }
        }

        // What a real-time watch saw: when each revision was installed and each write made, in the house's free
        // time as this test counts it (unscaled frames of a second or less, the house's own rule), and no disk read
        // a frame - a read every frame would stretch every frame it measures.
        private sealed class LiveSaveWatch
        {
            public double Free;
            public readonly SortedDictionary<long, double> InstalledAt = new SortedDictionary<long, double>();
            public readonly List<(long revision, double free)> Writes = new List<(long revision, double free)>();

            public string Describe() => "installed " + InstalledAt.Count + " revisions over " + Free.ToString("0.0", CultureInfo.InvariantCulture)
                + " s; writes at " + string.Join(", ", Writes.Select(write => "r" + write.revision + "@" + write.free.ToString("0.0", CultureInfo.InvariantCulture)));
        }

        private void LiveSaveWatchWrites(LiveSaveWatch watch)
        {
            NpcWrite("saveCandidateForDiagnostics", new Action<EpisodeState>(candidate =>
            {
                watch.Writes.Add((candidate.revision, watch.Free));
                new EpisodeSaveStore(director.SavePath).Save(candidate);
            }));
        }

        private IEnumerator LiveSaveWatchFor(LiveSaveWatch watch, float seconds)
        {
            long seen = NpcRead<EpisodeState>("projected").revision;
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                if (Time.unscaledDeltaTime <= 1f) watch.Free += Time.unscaledDeltaTime;
                long now = NpcRead<EpisodeState>("projected").revision;
                for (long revision = seen + 1; revision <= now; revision++) watch.InstalledAt[revision] = watch.Free;
                seen = Math.Max(seen, now);
            }
        }

        private static List<Dictionary<string, string>> LiveSaveTimingRows(NpcCommitTimings timings)
        {
            var lines = timings.ToCsv().Split('\n');
            var header = lines[0].Split(',');
            return lines.Skip(1).Where(line => line.Length > 0).Select(line =>
            {
                var fields = line.Split(',');
                Assert.That(fields.Length, Is.EqualTo(header.Length), line);
                return header.Select((name, index) => (name, value: fields[index])).ToDictionary(pair => pair.name, pair => pair.value);
            }).ToList();
        }
    }
}
