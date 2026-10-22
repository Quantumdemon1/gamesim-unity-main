using System;
using System.Collections.Generic;
using Gamesim.Episode;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class DisplayEvidenceSettlementTests
    {
        private const string Old = "Windowed,1600x900";
        private const string Requested = "Borderless,1920x1080";

        [Test]
        public void ARequestDoesNotPublishTheOldScreenAtTheEndOfTheCallingFrame()
        {
            var gate = new DisplayEvidenceSettlement();
            gate.Begin(10, Requested, Old);
            Assert.That(gate.Observe(10, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Observe(11, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Pending, Is.True, "A quiet old screen does not establish the request.");
        }

        [Test]
        public void TheRequestedScreenMustBeObservedQuietAfterTheMinimumDelay()
        {
            var gate = new DisplayEvidenceSettlement();
            gate.Begin(0, Requested, Old);
            Assert.That(gate.Observe(.5, Requested), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Observe(.9, Requested), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Observe(1, Requested), Is.EqualTo(DisplayEvidenceSettlement.Observation.Settled));
            Assert.That(gate.Pending, Is.False);
        }

        [Test]
        public void ASizeThatIsStillChangingRestartsTheQuietPeriod()
        {
            var gate = new DisplayEvidenceSettlement();
            gate.Begin(0, Requested, Old);
            gate.Observe(1, Requested);
            gate.Observe(1.1, "Borderless,1600x900");
            Assert.That(gate.Observe(1.2, Requested), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Observe(1.4, Requested), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Observe(1.5, Requested), Is.EqualTo(DisplayEvidenceSettlement.Observation.Settled));
        }

        [Test]
        public void ARefusedOrClampedRequestTimesOutInsteadOfInventingTheRequestedScreen()
        {
            var gate = new DisplayEvidenceSettlement();
            gate.Begin(0, Requested, Old);
            Assert.That(gate.Observe(9.9, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Observe(10, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.TimedOut));
            Assert.That(gate.Pending, Is.False);
        }

        [Test]
        public void TheDeadlineWinsEvenWhenTheScreenMatchesButTheMonitorOperationHasNotCompleted()
        {
            foreach (double boundary in new[] { 10d, 11d })
            {
                var gate = new DisplayEvidenceSettlement();
                gate.Begin(0, Old, Old);
                Assert.That(gate.Observe(boundary, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.TimedOut),
                    "A still-unfinished monitor operation first sampled at its deadline must not be called settled.");
            }
        }

        [Test]
        public void ASecondRequestSupersedesTheFirstWithoutPublishingItsIntermediateScreen()
        {
            var gate = new DisplayEvidenceSettlement();
            gate.Begin(0, Requested, Old);
            gate.Begin(.5, "Windowed,1280x720", Old);
            Assert.That(gate.Observe(1, Requested), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Expected, Is.EqualTo("Windowed,1280x720"));
            gate.Observe(1.5, "Windowed,1280x720");
            Assert.That(gate.Observe(1.75, "Windowed,1280x720"), Is.EqualTo(DisplayEvidenceSettlement.Observation.Settled));
        }

        [Test]
        public void AnObservationCanCompleteOnlyOnce()
        {
            var gate = new DisplayEvidenceSettlement();
            gate.Begin(0, Old, Old);
            Assert.That(gate.Observe(1, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.Settled));
            Assert.That(gate.Observe(2, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
            Assert.That(gate.Observe(11, Old), Is.EqualTo(DisplayEvidenceSettlement.Observation.Waiting));
        }

        [Test]
        public void InvalidClocksCannotMakeARequestAppearSettled()
        {
            var gate = new DisplayEvidenceSettlement();
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.Begin(double.NaN, Requested, Old));
            gate.Begin(10, Requested, Old);
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.Observe(9, Requested));
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.Observe(double.PositiveInfinity, Requested));
            Assert.That(gate.Pending, Is.True);
        }

        private static List<KeyValuePair<string, string>> Settings(string display, string resolution, string volume) =>
            new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("display", display),
                new KeyValuePair<string, string>("resolution", resolution),
                new KeyValuePair<string, string>("volume", volume)
            };

        [Test]
        public void PendingDisplayEvidenceDoesNotDelayAnUnrelatedSoundChange()
        {
            var before = Settings(Old, "1600x900", "35");
            var current = Settings(Old, "1920x1080", "45");
            var held = SettingsEvidence.AfterWindowObservation(before, current, true);
            Assert.That(SettingsEvidence.ChangeLines(before, held), Is.EqualTo(new[] { SettingsEvidence.ChangePrefix + "volume=45" }));
            Assert.That(current[1].Value, Is.EqualTo("1920x1080"), "The requested preferences are retained, not rewritten by evidence.");
            Assert.That(before[2].Value, Is.EqualTo("35"));
        }

        [Test]
        public void SettlementEmitsTheActualWindowOnceWithoutNeedingAnotherPreferenceClick()
        {
            var before = Settings(Old, "1600x900", "35");
            var actual = Settings(Requested, "1920x1080", "35");
            var pending = SettingsEvidence.AfterWindowObservation(before, actual, true);
            Assert.That(SettingsEvidence.ChangeLines(before, pending), Is.Empty);
            var settled = SettingsEvidence.AfterWindowObservation(pending, actual, false);
            Assert.That(SettingsEvidence.ChangeLines(pending, settled), Is.EqualTo(new[]
            {
                SettingsEvidence.ChangePrefix + "display=" + Requested,
                SettingsEvidence.ChangePrefix + "resolution=1920x1080"
            }));
            Assert.That(SettingsEvidence.ChangeLines(settled, actual), Is.Empty);
        }

        [Test]
        public void ATimeoutRecordsTheObservedFallbackNotTheRequestedResolution()
        {
            var before = Settings(Old, "1600x900", "35");
            var actualFallback = Settings("Windowed,1280x720", "1280x720", "35");
            var released = SettingsEvidence.AfterWindowObservation(before, actualFallback, false);
            Assert.That(SettingsEvidence.ChangeLines(before, released), Is.EqualTo(new[]
            {
                SettingsEvidence.ChangePrefix + "display=Windowed,1280x720",
                SettingsEvidence.ChangePrefix + "resolution=1280x720"
            }));
        }

        [Test]
        public void ACompleteRefusalReconcilesTheRequestEvenWhenTheOldActualWindowAndLogAreUnchanged()
        {
            var before = Settings(Old, "1600x900", "35");
            Assert.That(SettingsEvidence.WindowObservationNeedsRecording(before, Old, "1600x900", true), Is.True,
                "The pending request still says Borderless1920x1080, so silence in the actual window is not a no-op.");
            var reconciled = Settings(Old, "1600x900", "35");
            Assert.That(SettingsEvidence.ChangeLines(before, reconciled), Is.Empty);
            var volumeAfter = Settings(Old, "1600x900", "45");
            Assert.That(SettingsEvidence.ChangeLines(reconciled, volumeAfter),
                Is.EqualTo(new[] { SettingsEvidence.ChangePrefix + "volume=45" }), "The next sound click must not publish a refused resolution.");
        }

        [Test]
        public void AnUnchangedObservedWindowDoesNotWritePreferencesEveryFrame()
        {
            var before = Settings(Old, "1600x900", "35");
            Assert.That(SettingsEvidence.WindowObservationNeedsRecording(before, Old, "1600x900", false), Is.False);
            Assert.That(SettingsEvidence.WindowObservationNeedsRecording(before, Requested, "1920x1080", false), Is.True);
            Assert.That(SettingsEvidence.WindowObservationNeedsRecording(null, Old, "1600x900", false), Is.True);
        }
    }
}
