using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The room scope's measurement (ROOM-SCOPE-PLAN §5.3, RS0): the probe's views, the marks the cast
    /// stands on, the command line, the report's arithmetic and the shadow-map model. Every case but
    /// the last two is plain arithmetic on <see cref="VerificationViews"/>; those two read the saved
    /// house - its lamps through the model, and today's camera against the poses written down.
    /// </summary>
    public sealed class VerificationViewsTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const double Wide = 16.0 / 9.0;

        private static string[] Args(params string[] more)
            => new[] { "Gamesim.exe", "--gamesim-verify", "--gamesim-save-root", "C:/probe", VerificationViews.Argument }.Concat(more).ToArray();

        // ------------------------------------------------------------------ the views

        [Test]
        public void EveryViewsFocusIsOnItsRoom()
        {
            foreach (var view in VerificationViews.All)
            {
                if (view.Room == null)
                {
                    // The whole house: its focus is over the house's own outline.
                    Assert.That(view.FocusX, Is.InRange(-14.0, 14.15), view.Id);
                    Assert.That(view.FocusZ, Is.InRange(-20.0, 20.0), view.Id);
                    continue;
                }
                var room = VerificationViews.Room(view.Room);
                Assert.That(room, Is.Not.Null, view.Id + " is about " + view.Room + ", which the plan's house lacks.");
                Assert.That(room.Contains(view.FocusX, view.FocusZ), Is.True,
                    view.Id + "'s focus (" + view.FocusX + ", " + view.FocusZ + ") is not on the " + view.Room + "'s floor.");
                // The view looks at its focus: it is on the frustum's axis, in front of the eye.
                Assert.That(view.Frustum(Wide).SphereVisible(view.FocusX, view.FocusY, view.FocusZ, 0), Is.True, view.Id);
            }
            Assert.That(VerificationViews.All.Select(view => view.Id), Is.EqualTo(new[]
                { "V1", "V2", "V3", "V4a", "V4b", "V4c", "V4d", "V4e", "V4f", "V4g", "V4h", "V5", "V6", "V6g", "V7", "V8", "V9" }));
        }

        [Test]
        public void EachRoomIsViewedAtD4sFarthestZoomAlongItsBestHeading()
        {
            string[] letters = { "a", "b", "c", "d", "e", "f", "g", "h" };
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                string room = RoomLayers.RoomIds[i];
                var view = VerificationViews.Find("V4" + letters[i]);
                var plan = VerificationViews.Room(room);
                Assert.That(view.Room, Is.EqualTo(room));
                Assert.That(view.FocusX, Is.EqualTo(plan.CentreX).Within(1e-9), room);
                Assert.That(view.FocusZ, Is.EqualTo(plan.CentreZ).Within(1e-9), room);
                bool south = room == "HoH" || room == "Nomination" || room == "Games";
                Assert.That(view.Distance, Is.EqualTo(room == "Yard" ? 18.25 : south ? 10.5 : 11.25), room);
                Assert.That(view.Yaw, Is.EqualTo(south ? 90 : 0), room);
                Assert.That(view.Pitch, Is.EqualTo(VerificationViews.TodayPitchFor(view.Distance)).Within(1e-9), room + " keeps today's pitch for its metres.");
                Assert.That(view.CaptureAtYaw45, Is.True, room + " is captured at yaw 45 too, for D4.");
                Assert.That(view.AtYaw(45).Yaw, Is.EqualTo(45));
            }
            Assert.That(VerificationViews.All.Where(view => view.CaptureAtYaw45).Count(), Is.EqualTo(RoomLayers.Count), "Only D4's rooms are captured twice.");
        }

        [Test]
        public void TheViewsAreTodaysCameraWrittenDown()
        {
            Assert.That(VerificationViews.TodayPitchFor(10), Is.EqualTo(45).Within(1e-9));
            Assert.That(VerificationViews.TodayPitchFor(34), Is.EqualTo(62).Within(1e-9));
            var v1 = VerificationViews.Find("V1");
            Assert.That(new[] { v1.FocusX, v1.FocusY, v1.FocusZ, v1.Distance, v1.Yaw, v1.FieldOfView },
                Is.EqualTo(new[] { .075, 1.2, 0, 24, 45, 55 }));
            Assert.That(v1.Pitch, Is.EqualTo(45 + 17 * 14.0 / 24).Within(1e-9));
            var v3 = VerificationViews.Find("V3");
            Assert.That(new[] { v3.FocusX, v3.FocusZ, v3.Distance, v3.Pitch }, Is.EqualTo(new[] { -7.0, -5, 10, 45 }));
            // The game's own shots, by their constants.
            Assert.That(VerificationViews.TwoShotDistance, Is.EqualTo(HouseCameraRig.TwoShotDistance).Within(1e-6));
            Assert.That(VerificationViews.TwoShotPitch, Is.EqualTo(HouseCameraRig.TwoShotPitch).Within(1e-6));
            Assert.That(VerificationViews.TwoShotFieldOfView, Is.EqualTo(HouseCameraRig.TwoShotFieldOfView).Within(1e-6));
            Assert.That(VerificationViews.TwoShotLift, Is.EqualTo(HouseCameraRig.TwoShotLift).Within(1e-6));
            Assert.That(VerificationViews.CeremonyDistance, Is.EqualTo(EpisodeDirector.CeremonyDistance).Within(1e-6));
            Assert.That(VerificationViews.CompetitionDistance, Is.EqualTo(EpisodeDirector.CompetitionShotDistance).Within(1e-6));
            Assert.That(VerificationViews.CompetitionPitch, Is.EqualTo(EpisodeDirector.CompetitionShotPitch).Within(1e-6));
            Assert.That(VerificationViews.CompetitionFieldOfView, Is.EqualTo(EpisodeDirector.CompetitionShotFieldOfView).Within(1e-6));
            Assert.That(VerificationViews.DiaryFieldOfView, Is.EqualTo(EpisodeDirector.DiaryShotFieldOfView).Within(1e-6));
            Assert.That(VerificationViews.DiaryDepthOfField, Is.EqualTo(EpisodeDirector.DiaryShotDepthOfField).Within(1e-6));
            Assert.That(VerificationViews.OverviewPitch, Is.EqualTo(EpisodeDirector.OverviewPitch).Within(1e-6));
            Assert.That(VerificationViews.OverviewHalfHeight, Is.EqualTo(EpisodeDirector.OverviewOrthographicSize).Within(1e-6));
            Assert.That(VerificationViews.OverviewLift, Is.EqualTo(EpisodeDirector.OverviewLift).Within(1e-6));
            var v6 = VerificationViews.Find("V6");
            var v6g = VerificationViews.Find("V6g");
            Assert.That(v6g.CeremonyPlaces && !v6.CeremonyPlaces, Is.True, "V6g is V6's framing with the cast at the table.");
            Assert.That(new[] { v6g.FocusX, v6g.FocusZ, v6g.Distance, v6g.Pitch, v6g.Yaw }, Is.EqualTo(new[] { v6.FocusX, v6.FocusZ, v6.Distance, v6.Pitch, v6.Yaw }));
            // The diary interview from the chair's camera point to the face (EpisodeDirector.StartDiaryShot's arithmetic).
            var v8 = VerificationViews.Find("V8");
            Assert.That(v8.Route, Is.EqualTo(VerificationViewRoute.Diary));
            v8.EyePosition(out double x, out double y, out double z);
            Assert.That(new[] { x, y, z }, Is.EqualTo(new[] { 7, 1.45, 1.4 }).Within(1e-9));
            Assert.That(VerificationViews.Find("V2").Route, Is.EqualTo(VerificationViewRoute.Overview));
        }

        [Test]
        public void V9IsDeclaredAndSkippedWithItsReason()
        {
            var v9 = VerificationViews.Find("V9");
            Assert.That(v9.Route, Is.EqualTo(VerificationViewRoute.Skipped));
            Assert.That(v9.SkippedBecause, Does.Contain("FullFieldView").And.Contain("props"));
            Assert.That(VerificationViews.Requested(null).Select(view => view.Id), Does.Not.Contain("V9"));
            Assert.That(VerificationViews.Requested(VerificationViews.Read(Args())).Count, Is.EqualTo(VerificationViews.All.Count - 1));
            // The W3 capture's eye on this branch: 22.4 m back from the yard's centre and 9 m up, at 50 degrees.
            v9.EyePosition(out double x, out double y, out double z);
            Assert.That(new[] { x, y, z }, Is.EqualTo(new[] { 0, 9.95, -7.4 }).Within(1e-9));
            Assert.That(v9.FieldOfView, Is.EqualTo(50));
        }

        [Test]
        public void TheMarksPutTwoInARoomInThePlansOrder()
        {
            var sixteen = VerificationViews.Marks(16);
            Assert.That(sixteen.GroupBy(mark => mark.Room).Select(group => group.Count()), Is.All.EqualTo(2));
            Assert.That(sixteen.Select(mark => mark.Room).Distinct(), Is.EquivalentTo(RoomLayers.RoomIds));
            var twelve = VerificationViews.Marks(12);
            Assert.That(twelve.Select(mark => mark.Room).Distinct(), Is.EquivalentTo(new[] { "Living", "Kitchen", "Bedroom", "Private", "Yard", "Nomination" }),
                "At twelve: two in each north-block room, the Yard and Nomination; HoH and Games empty.");
            foreach (var mark in sixteen)
            {
                Assert.That(VerificationViews.Room(mark.Room).Contains(mark.X, mark.Z), Is.True, "Mark " + mark.Index + " is on the " + mark.Room + "'s floor.");
                var partner = sixteen[mark.Partner];
                Assert.That(partner.Partner, Is.EqualTo(mark.Index));
                Assert.That(partner.Room, Is.EqualTo(mark.Room));
                Assert.That(Math.Abs(partner.X - mark.X), Is.EqualTo(2 * VerificationViews.MarkHalfSpacing).Within(1e-9));
            }
            // The two-shot frames the Living room's pair, marks 0 and 1, from over their midpoint.
            var v5 = VerificationViews.Find("V5");
            Assert.That(v5.FocusX, Is.EqualTo((sixteen[0].X + sixteen[1].X) / 2).Within(1e-9));
            Assert.That(v5.FocusZ, Is.EqualTo(sixteen[0].Z).Within(1e-9));
            Assert.That(VerificationViews.Marks(0), Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => VerificationViews.Marks(17));
            Assert.Throws<ArgumentOutOfRangeException>(() => VerificationViews.Marks(-1));
        }

        // ------------------------------------------------------------------ the command line

        [Test]
        public void TheCommandLineReadsTheRequest()
        {
            Assert.That(VerificationViews.Read(new[] { "Gamesim.exe", "--gamesim-verify" }), Is.Null, "No views asked for.");
            var timing = VerificationViews.Read(Args());
            Assert.That(timing.Pass, Is.EqualTo(VerificationViews.TimingPass));
            Assert.That(new[] { timing.Settle, timing.Measure }, Is.EqualTo(new[] { 10.0, 20.0 }));
            Assert.That(timing.Repeats, Is.EqualTo(3));
            Assert.That(timing.Only, Is.Null);
            Assert.That(timing.LiveWorld || timing.Quick || timing.HoldScope || timing.WholeHouse, Is.False);
            var counters = VerificationViews.Read(Args(VerificationViews.PassArgument, "counters"));
            Assert.That(counters.Pass, Is.EqualTo(VerificationViews.CountersPass));
            Assert.That(counters.Repeats, Is.EqualTo(1), "Counter passes once.");
            var quick = VerificationViews.Read(Args(VerificationViews.QuickArgument, VerificationViews.LiveArgument));
            Assert.That(new[] { quick.Settle, quick.Measure }, Is.EqualTo(new[] { 2.0, 3.0 }));
            Assert.That(quick.Repeats, Is.EqualTo(1));
            Assert.That(quick.LiveWorld, Is.True);
            var only = VerificationViews.Read(Args(VerificationViews.OnlyArgument, "v4A, V1,V1"));
            Assert.That(only.Only, Is.EqualTo(new[] { "V4a", "V1" }), "Ids are matched without case and kept once.");
            Assert.That(VerificationViews.Requested(only).Select(view => view.Id), Is.EqualTo(new[] { "V1", "V4a" }), "In the views' own order.");
            var ablations = VerificationViews.Read(Args(VerificationViews.PassArgument, "ablations", VerificationViews.AblationsArgument, "ssao-off"));
            Assert.That(ablations.Ablations, Is.EqualTo(new[] { VerificationViews.SsaoOff }));
            Assert.That(ablations.Repeats, Is.EqualTo(3), "Ablations are timed: three times.");
            Assert.That(VerificationViews.Read(Args(VerificationViews.ScopeArgument)).HoldScope, Is.True);
            Assert.That(VerificationViews.Read(Args(VerificationViews.WholeHouseArgument)).WholeHouse, Is.True);
        }

        [TestCase("no-verify", "requires --gamesim-verify")]
        [TestCase("season", "cannot run with --gamesim-verify-season")]
        [TestCase("look-sheet", "cannot run with --gamesim-look-sheet")]
        [TestCase("creator", "cannot run with --gamesim-verify-creator")]
        [TestCase("unknown-pass", "is not a pass")]
        [TestCase("no-pass", "needs a pass")]
        [TestCase("live-counters", "is a timing pass")]
        [TestCase("scope-and-whole", "ask for opposite things")]
        [TestCase("unknown-view", "V10 is not a view")]
        [TestCase("empty-only", "needs view ids")]
        [TestCase("ablations-timing", "belongs to the ablations pass")]
        [TestCase("unknown-ablation", "fog-off is not an ablation")]
        public void TheCommandLineIsRefusedAsWritten(string wrong, string because)
        {
            string[] args;
            switch (wrong)
            {
                case "no-verify": args = new[] { "Gamesim.exe", VerificationViews.Argument }; break;
                case "season": args = Args("--gamesim-verify-season"); break;
                case "look-sheet": args = Args("--gamesim-look-sheet"); break;
                case "creator": args = Args("--gamesim-verify-creator"); break;
                case "unknown-pass": args = Args(VerificationViews.PassArgument, "gpu"); break;
                case "no-pass": args = Args(VerificationViews.PassArgument, VerificationViews.LiveArgument); break;
                case "live-counters": args = Args(VerificationViews.PassArgument, "counters", VerificationViews.LiveArgument); break;
                case "scope-and-whole": args = Args(VerificationViews.ScopeArgument, VerificationViews.WholeHouseArgument); break;
                case "unknown-view": args = Args(VerificationViews.OnlyArgument, "V1,V10"); break;
                case "empty-only": args = Args(VerificationViews.OnlyArgument, ","); break;
                case "ablations-timing": args = Args(VerificationViews.AblationsArgument, "ssao-off"); break;
                default: args = Args(VerificationViews.PassArgument, "ablations", VerificationViews.AblationsArgument, "ssao-off,fog-off"); break;
            }
            string refusal = VerificationViews.Refusal(args);
            Assert.That(refusal, Is.Not.Null, wrong + " is refused.");
            Assert.That(refusal, Does.Contain(because));
        }

        [Test]
        public void ARequestThatCanRunIsNotRefused()
        {
            Assert.That(VerificationViews.Refusal(new[] { "Gamesim.exe", "--gamesim-verify" }), Is.Null, "No views asked for, nothing to refuse.");
            Assert.That(VerificationViews.Refusal(Args()), Is.Null);
            Assert.That(VerificationViews.Refusal(Args(VerificationViews.LiveArgument, "--gamesim-house-size", "16", "--gamesim-stress-roster")), Is.Null);
            Assert.That(VerificationViews.Refusal(Args(VerificationViews.PassArgument, "counters", VerificationViews.OnlyArgument, "V1,V4a,V6g")), Is.Null);
            Assert.That(VerificationViews.Refusal(Args(VerificationViews.PassArgument, "ablations", VerificationViews.AblationsArgument,
                VerificationViews.Confinement + "," + VerificationViews.SmallMeshCulling, VerificationViews.QuickArgument)), Is.Null);
        }

        [Test]
        public void TheReportIsNamedForItsPassSizeAndCast()
        {
            Assert.That(VerificationViews.ReportName(VerificationViews.Read(Args()), 16), Is.EqualTo("views-timing-16"));
            Assert.That(VerificationViews.ReportName(VerificationViews.Read(Args(VerificationViews.LiveArgument)), 16), Is.EqualTo("views-timing-16-live"));
            Assert.That(VerificationViews.ReportName(VerificationViews.Read(Args(VerificationViews.PassArgument, "counters", VerificationViews.ScopeArgument)), 12),
                Is.EqualTo("views-counters-12-scope"));
            Assert.That(VerificationViews.ReportName(VerificationViews.Read(Args(VerificationViews.WholeHouseArgument, VerificationViews.QuickArgument)), 6),
                Is.EqualTo("views-timing-6-whole-house-quick"));
        }

        // ------------------------------------------------------------------ the report

        [TestCase("10,10.4,10.2", VerificationViews.Repeatable)]
        [TestCase("10,10.5,10.25", VerificationViews.Repeatable)]
        [TestCase("10,10.6,10.1", VerificationViews.NotRepeatable)]
        [TestCase("4,3", VerificationViews.NotRepeatable)]
        [TestCase("10", VerificationViews.SingleRun)]
        public void AViewsMediansMustLieWithinFivePercentOfEachOther(string runs, string verdict)
        {
            var medians = runs.Split(',').Select(value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.That(VerificationViews.Repeatability(medians, out double spread), Is.EqualTo(verdict));
            if (medians.Length > 1) Assert.That(spread, Is.EqualTo((medians.Max() - medians.Min()) / medians.Min()).Within(1e-12));
        }

        [Test]
        public void AViewThatDoesNotRepeatIsRunOnceMoreAndFlagged()
        {
            var row = new VerificationViewRow { id = "V1" };
            row.runs.Add(Run(1, 10, false, 4, 2, 6));
            row.runs.Add(Run(2, 11, false, 5, 3, 7));
            row.runs.Add(Run(3, 10.2, false, 4.5, -1, 6.5));
            VerificationViews.CompleteRow(row);
            Assert.That(row.repeatability, Is.EqualTo(VerificationViews.NotRepeatable));
            Assert.That(row.spread, Is.EqualTo(.1).Within(1e-12));
            Assert.That(row.flagged, Is.True);
            Assert.That(VerificationViews.NeedsRerun(row), Is.True);
            Assert.That(row.medians, Is.EqualTo(new[] { 10, 11, 10.2 }).Within(1e-5));
            Assert.That(row.medianOfMediansMs, Is.EqualTo(10.2).Within(1e-5));
            Assert.That(new[] { row.cpuMainMedianMs, row.cpuRenderMedianMs, row.gpuMedianMs }, Is.EqualTo(new[] { 4.5, 2, 6.5 }),
                "The median of what each run measured; a run with no render-thread time does not count.");
            row.runs.Add(Run(4, 10.4, true, 4, 2, 6));
            VerificationViews.CompleteRow(row);
            Assert.That(row.rerun && row.flagged, Is.True);
            Assert.That(VerificationViews.NeedsRerun(row), Is.False, "Once more, never twice.");
            Assert.That(row.medians.Length, Is.EqualTo(4));
            Assert.That(row.repeatability, Is.EqualTo(VerificationViews.NotRepeatable), "Judged on the first three; the rerun is reported beside them.");
            var steady = new VerificationViewRow { id = "V2" };
            for (int i = 1; i <= 3; i++) steady.runs.Add(Run(i, 10 + i * .1, false, -1, -1, -1));
            VerificationViews.CompleteRow(steady);
            Assert.That(steady.flagged || VerificationViews.NeedsRerun(steady), Is.False);
            Assert.That(new[] { steady.cpuMainMedianMs, steady.gpuMedianMs }, Is.EqualTo(new[] { -1.0, -1 }), "Nothing measured reads -1.");
        }

        [Test]
        public void TheReportCarriesWhatThePlanAsksFor()
        {
            AssertFields(typeof(VerificationViewsReport), "status", "pass", "scope", "buildKind", "drawerMode", "frameTimingSource", "graphical", "batchMode",
                "developmentBuild", "liveWorld", "houseSize", "houseSizeRequested", "repeats", "settleSeconds", "measureSeconds", "drawerCoveragePercent",
                "drawerRenderers", "drawerExcludedRenderers", "skippedViews", "marks", "views", "ablations", "checks", "rawFramesFile", "errors", "notes");
            AssertFields(typeof(VerificationViewRow), "id", "room", "route", "ablation", "viewedRooms", "runs", "medians", "medianOfMediansMs", "spread",
                "repeatability", "rerun", "flagged", "cpuMainMedianMs", "cpuRenderMedianMs", "gpuMedianMs", "counters", "shadows", "bodiesByRoom", "captures");
            AssertFields(typeof(VerificationViewRun), "repeat", "rerun", "frames", "cpuMainMedianMs", "cpuRenderMedianMs", "gpuMedianMs");
            AssertFields(typeof(VerificationViewCounters), "batches", "setPassCalls", "drawCalls", "triangles", "vertices", "shadowCasters",
                "visibleSkinnedMeshes", "drawerVisibleInstances", "drawerLayerCulled", "drawerFrustumCulled", "drawerSmallMeshCulled");
            AssertFields(typeof(VerificationShadowWork), "lightsInFrustum", "lampsInFrustum", "atlas", "houseCasters", "bodyCasters", "lamps", "atlasLine");
            AssertFields(typeof(VerificationShadowAtlas), "slicesRequested", "slicesKept", "scale", "atlasSize", "largestSlice");
            Assert.That(typeof(VerificationViewsReport).IsSerializable && typeof(VerificationViewRow).IsSerializable, Is.True, "JsonUtility writes the report.");
        }

        [Test]
        public void ARunPassesOnlyMeasuredWithNoErrorAndNoFailedCheck()
        {
            VerificationViewsReport Report()
            {
                var report = new VerificationViewsReport { pass = VerificationViews.TimingPass, errors = new string[0] };
                var row = new VerificationViewRow { id = "V1" };
                row.runs.Add(Run(1, 10, false, -1, -1, -1));
                report.views.Add(row);
                return report;
            }
            Assert.That(VerificationViews.Status(Report()), Is.EqualTo("Passed"));
            var flagged = Report();
            flagged.views[0].runs.Add(Run(2, 20, false, -1, -1, -1));
            VerificationViews.CompleteRow(flagged.views[0]);
            Assert.That(VerificationViews.Status(flagged), Is.EqualTo("Passed"), "A flagged view is looked at twice, not failed.");
            var erred = Report();
            erred.errors = new[] { "an error" };
            Assert.That(VerificationViews.Status(erred), Is.EqualTo("Failed"));
            var failedCheck = Report();
            failedCheck.checks.Add(VerificationProbeCheck.Of("P2", VerificationProbeCheck.Failed, "the marker stayed"));
            Assert.That(VerificationViews.Status(failedCheck), Is.EqualTo("Failed"));
            var recorded = Report();
            recorded.checks.Add(VerificationProbeCheck.Of("P3", VerificationProbeCheck.Recorded, "for the record"));
            Assert.That(VerificationViews.Status(recorded), Is.EqualTo("Passed"));
            var unmeasured = Report();
            unmeasured.views.Add(new VerificationViewRow { id = "V2" });
            Assert.That(VerificationViews.Status(unmeasured), Is.EqualTo("Failed"), "Every requested view must be measured.");
            var ablations = Report();
            ablations.pass = VerificationViews.AblationsPass;
            Assert.That(VerificationViews.Status(ablations), Is.EqualTo("Failed"), "The ablations pass is judged on its own rows.");
        }

        [Test]
        public void TheAtlasLineIsReadAsUrpWritesIt()
        {
            const string Line = "Reduced additional punctual light shadows resolution by 8 to make 84 shadow maps fit in the 2048x2048 shadow atlas. "
                + "To avoid this, increase shadow atlas size, decrease big shadow resolutions, or reduce the number of shadow maps active in the same frame";
            Assert.That(VerificationViews.TryParseAtlasMessage(Line, out int scale, out int slices, out int atlas), Is.True);
            Assert.That(new[] { scale, slices, atlas }, Is.EqualTo(new[] { 8, 84, 2048 }));
            Assert.That(VerificationViews.TryParseAtlasMessage("Too many additional punctual lights shadows.", out _, out _, out _), Is.False);
            Assert.That(VerificationViews.TryParseAtlasMessage(null, out _, out _, out _), Is.False);
        }

        // ------------------------------------------------------------------ the ablations

        [Test]
        public void EachAblationIsMeasuredBesideItsViewsBaseline()
        {
            var all = VerificationViews.AblationPlan(VerificationViews.Read(Args(VerificationViews.PassArgument, "ablations")));
            Assert.That(all.Count, Is.EqualTo(21), "V1, V4a and V6g with five ablations and a baseline each, small-mesh culling at V1 and V2, and V2's baseline.");
            Assert.That(all.Take(7).Select(step => step.ToString()), Is.EqualTo(new[]
            {
                "V1 baseline", "V1 lamp-shadows-off", "V1 bodies-off", "V1 ssao-off", "V1 moon-shadows-off", "V1 rendering-layer-confinement", "V1 small-mesh-culling",
            }));
            Assert.That(all.Where(step => step.Ablation == VerificationViews.SmallMeshCulling).Select(step => step.View), Is.EqualTo(new[] { "V1", "V2" }));
            foreach (var view in all.Select(step => step.View).Distinct())
                Assert.That(all.First(step => step.View == view).Ablation, Is.EqualTo(VerificationViews.Baseline), view + "'s baseline comes first.");
            var ssao = VerificationViews.AblationPlan(VerificationViews.Read(Args(VerificationViews.PassArgument, "ablations", VerificationViews.AblationsArgument, "ssao-off")));
            Assert.That(ssao.Select(step => step.ToString()), Is.EqualTo(new[] { "V1 baseline", "V1 ssao-off", "V4a baseline", "V4a ssao-off", "V6g baseline", "V6g ssao-off" }));
            var smallMesh = VerificationViews.AblationPlan(VerificationViews.Read(Args(VerificationViews.PassArgument, "ablations",
                VerificationViews.AblationsArgument, VerificationViews.SmallMeshCulling)));
            Assert.That(smallMesh.Select(step => step.ToString()), Is.EqualTo(new[] { "V1 baseline", "V1 small-mesh-culling", "V2 baseline", "V2 small-mesh-culling" }));
        }

        [Test]
        public void ConfinementStaysInTheEightNamedRenderingLayers()
        {
            var bits = Enumerable.Range(0, RoomLayers.Count).Select(VerificationViews.ConfinementBit).ToArray();
            Assert.That(bits.Distinct().Count(), Is.EqualTo(RoomLayers.Count), "One rendering layer a room.");
            Assert.That(bits.All(bit => bit != 0 && (bit & (bit - 1)) == 0 && (bit & ~VerificationViews.AllConfinementBits) == 0), Is.True);
            Assert.That(VerificationViews.AllConfinementBits, Is.EqualTo(0xFFu));
            Assert.That(VerificationViews.ConfinementBit(-1), Is.EqualTo(VerificationViews.AllConfinementBits), "The shell and anything unplaced: every room's.");
        }

        // ------------------------------------------------------------------ the shadow-map model

        [TestCase(14, false, 84, 8, 128)]
        [TestCase(14, true, 85, 8, 128)]
        [TestCase(7, false, 42, 4, 256)]
        [TestCase(3, true, 19, 4, 256)]
        [TestCase(2, true, 13, 2, 512)]
        [TestCase(1, true, 7, 2, 512)]
        [TestCase(0, true, 1, 1, 256)]
        public void TheAtlasHalvesEveryMapUntilAllFit(int lamps, bool followSpot, int slices, int scale, int largest)
        {
            // §3.5: soft High-tier lamps (1024 px, six faces) and the follow spotlight (hard, Low tier, one face) in a 2048 atlas.
            var lights = Enumerable.Range(0, lamps).Select(i => new VerificationShadowLight("lamp " + i, i, 1, 0, 6, false, true, 1024)).ToList();
            if (followSpot) lights.Add(new VerificationShadowLight("follow spot", 0, 4, 0, 8, true, false, 256));
            var atlas = VerificationViews.LayoutShadowAtlas(lights, 0, 20, -10, 2048);
            Assert.That(atlas.slicesRequested, Is.EqualTo(slices));
            Assert.That(atlas.slicesKept, Is.EqualTo(slices), "Nothing is dropped at these counts.");
            Assert.That(atlas.scale, Is.EqualTo(scale));
            Assert.That(atlas.largestSlice, Is.EqualTo(largest));
            Assert.That(atlas.tooMany, Is.False);
            if (followSpot && lamps > 0) Assert.That(atlas.smallestSlice, Is.EqualTo(256 / scale), "The spot is scaled with the rest.");
        }

        [Test]
        public void ASphereIsKeptUnlessItLiesWhollyBeyondOnePlane()
        {
            var frustum = VerificationViews.Find("V1").Frustum(Wide);
            Assert.That(frustum.SphereVisible(.075, 1.2, 0, 0), Is.True, "The focus.");
            Assert.That(frustum.SphereVisible(frustum.EyeX, frustum.EyeY + 5, frustum.EyeZ, 1), Is.False, "Behind and above the eye.");
            Assert.That(frustum.SphereVisible(frustum.EyeX, frustum.EyeY + 5, frustum.EyeZ, 10), Is.True, "Reaching into the frustum.");
            Assert.That(frustum.SphereVisible(200, 0, 200, 6), Is.False, "Past the far plane.");
            var lights = new[]
            {
                new VerificationShadowLight("in", .075, 1, 0, 6, false, true, 1024),
                new VerificationShadowLight("out", 300, 1, 300, 6, false, true, 1024),
            };
            Assert.That(VerificationViews.InFrustum(frustum, lights).Select(light => light.Name), Is.EqualTo(new[] { "in" }));
            var overview = VerificationViews.Find("V2").Frustum(Wide);
            Assert.That(overview.SphereVisible(-13, 0, -19, 0) && overview.SphereVisible(12, 0, 18, 0), Is.True, "The overview holds the house from the south wing to the yard.");
            // A frustum from six planes, as the probe reads a live camera's: here a box two metres across.
            var box = new VerificationFrustum(0, 0, 0,
                new[] { new double[] { 1, 0, 0 }, new double[] { -1, 0, 0 }, new double[] { 0, 1, 0 }, new double[] { 0, -1, 0 }, new double[] { 0, 0, 1 }, new double[] { 0, 0, -1 } },
                new double[] { 1, 1, 1, 1, 1, 1 });
            Assert.That(box.SphereVisible(0, 0, 0, 0), Is.True);
            Assert.That(box.SphereVisible(3, 0, 0, 1), Is.False);
            Assert.That(box.SphereVisible(3, 0, 0, 2.5), Is.True);
        }

        // ------------------------------------------------------------------ the saved house

        [Test]
        public void TheShadowModelCountsEightyFourFacesAtV1AndFortyTwoAtV9sEye()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var lights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
                    .Where(light => light.enabled && light.gameObject.activeInHierarchy && light.type != LightType.Directional
                        && light.shadows != LightShadows.None && light.shadowStrength > 0f)
                    .Select(light => PortVerification.ShadowLightOf(light)).ToList();
                Assert.That(lights.Count, Is.EqualTo(14), "The saved house's shadowed lights are its fourteen lamps.");
                var v1 = Work(VerificationViews.Find("V1"), Wide, lights);
                Assert.That(v1.lights, Is.EqualTo(14), "Today's default touches every lamp's reach.");
                Assert.That(v1.slicesKept, Is.EqualTo(84), "Six faces a lamp.");
                Assert.That(v1.scale, Is.EqualTo(8), "URP's 'reduced by 8' in the editor logs.");
                Assert.That(v1.largestSlice, Is.EqualTo(128));
                var v9 = Work(VerificationViews.Find("V9"), 1600.0 / 900.0, lights);
                Assert.That(v9.lights, Is.EqualTo(7), "The W3 full-field capture's eye: the 42-map line in the editor logs.");
                Assert.That(v9.slicesKept, Is.EqualTo(42));
                Assert.That(v9.largestSlice, Is.EqualTo(256));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheViewsStandInTheSavedHouseWithTodaysCamera()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                Assert.That(HouseRoomScope.TryCreate(scene, out var scope, out string why), Is.True, why);
                for (int i = 0; i < RoomLayers.Count; i++)
                {
                    var plan = VerificationViews.Room(RoomLayers.RoomIds[i]);
                    var box = scope.Box(i);
                    Assert.That(new double[] { box.xMin, box.xMax, box.yMin, box.yMax },
                        Is.EqualTo(new[] { plan.MinX, plan.MaxX, plan.MinZ, plan.MaxZ }).Within(VerificationViews.HouseTolerance), plan.Room + "'s floor.");
                }
                var markers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true)).ToArray();
                foreach (var plan in VerificationViews.House)
                {
                    var marker = markers.Single(candidate => candidate.RoomName == plan.Room);
                    Assert.That(new double[] { marker.transform.position.x, marker.transform.position.z },
                        Is.EqualTo(new[] { plan.MarkX, plan.MarkZ }).Within(VerificationViews.HouseTolerance), plan.Room + "'s marker, where its marks stand.");
                }
                var rig = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseCameraRig>(true)).Single();
                var v1 = VerificationViews.Find("V1");
                Assert.That(new double[] { rig.HouseCenter.x, rig.HouseCenter.y, rig.HouseCenter.z },
                    Is.EqualTo(new[] { v1.FocusX, v1.FocusY, v1.FocusZ }).Within(1e-3), "V1 is over the rig's house centre.");
                Assert.That(rig.Yaw, Is.EqualTo((float)VerificationViews.TodayYaw).Within(1e-3f));
                Assert.That(rig.NearestDistance, Is.EqualTo((float)VerificationViews.TodayNearest).Within(1e-3f), "Today's scene minimum (D11 restores 4 m in RS5).");
                Assert.That(rig.FarthestDistance, Is.EqualTo((float)VerificationViews.TodayFarthest).Within(1e-3f));
                foreach (double distance in new[] { 10, 11, 11.25, 18.25, 24, 34 })
                    Assert.That(rig.PitchFor((float)distance), Is.EqualTo((float)VerificationViews.TodayPitchFor(distance)).Within(1e-3f), "The pitch at " + distance + " m.");
                var camera = rig.ViewCamera;
                Assert.That(camera.fieldOfView, Is.EqualTo((float)VerificationViews.TodayFieldOfView).Within(1e-3f));
                Assert.That(camera.nearClipPlane, Is.EqualTo((float)VerificationViews.TodayNearClip).Within(1e-3f));
                Assert.That(camera.farClipPlane, Is.EqualTo((float)VerificationViews.TodayFarClip).Within(1e-3f));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        // ------------------------------------------------------------------ helpers

        private static VerificationShadowAtlas Work(VerificationView view, double aspect, List<VerificationShadowLight> lights)
        {
            var frustum = view.Frustum(aspect);
            var visible = VerificationViews.InFrustum(frustum, lights);
            return VerificationViews.LayoutShadowAtlas(visible, frustum.EyeX, frustum.EyeY, frustum.EyeZ, 2048);
        }

        private static VerificationViewRun Run(int repeat, double median, bool rerun, double main, double render, double gpu) => new VerificationViewRun
        {
            repeat = repeat, rerun = rerun, cpuMainMedianMs = main, cpuRenderMedianMs = render, gpuMedianMs = gpu,
            frames = VerificationFrameSummary.Of("V", new[] { (float)median }),
        };

        private static void AssertFields(Type type, params string[] names)
        {
            var declared = type.GetFields().Select(field => field.Name).ToArray();
            Assert.That(declared, Is.SupersetOf(names), type.Name + " carries the fields the report needs.");
        }
    }
}
