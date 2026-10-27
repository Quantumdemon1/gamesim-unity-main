using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.House;

namespace Gamesim.Episode
{
    /// <summary>
    /// The room scope's measurement (ROOM-SCOPE-PLAN §5.3, RS0): the views the probe holds, the house
    /// they are taken in, the marks the cast stands on, the passes, and the arithmetic the report is
    /// made of - medians, the 5% repeatability rule, and the shadow-map model that mirrors URP 17.6's
    /// AdditionalLightsShadowAtlasLayout. Unity-free on purpose, like
    /// <see cref="VerificationPerformance"/>: the runner (<c>PortVerification.Views.cs</c>) measures and
    /// everything here is arithmetic on what it measured, so a plain fixture checks the same rules the
    /// player reports by.
    ///
    /// <para>The views compare one pose with another, before and after the scope; they give no
    /// verdict. The verdict is the 300 s profile's alone (<see cref="VerificationPerformance.Verdict"/>).
    /// Every pose is today's numbers written down, not read from the rig, so RS7 measures the same
    /// frames after RS5 has changed the free camera.</para>
    /// </summary>
    public static class VerificationViews
    {
        // ------------------------------------------------------------------------------------
        // The command line.
        // ------------------------------------------------------------------------------------

        /// <summary>The views mode: with <c>--gamesim-verify</c> and an absolute <c>--gamesim-save-root</c>.</summary>
        public const string Argument = "--gamesim-verify-views";
        /// <summary>Which pass: <see cref="TimingPass"/> (the default), <see cref="CountersPass"/> or <see cref="AblationsPass"/>.</summary>
        public const string PassArgument = "--gamesim-views-pass";
        /// <summary>The cast stays the house's, walking where the NPC world takes it (a timing pass, the live spread).</summary>
        public const string LiveArgument = "--gamesim-views-live";
        /// <summary>A comma-separated subset of the views, by id.</summary>
        public const string OnlyArgument = "--gamesim-views-only";
        /// <summary>A comma-separated subset of the ablations (the ablations pass only); the baseline is always measured.</summary>
        public const string AblationsArgument = "--gamesim-views-ablations";
        /// <summary>A smoke run: <see cref="QuickSettleSeconds"/> and <see cref="QuickMeasureSeconds"/>, once. Never a measurement.</summary>
        public const string QuickArgument = "--gamesim-views-quick";
        /// <summary>Holds the room scope on (EpisodeDirector.HoldRoomScope) for every view that names its rooms.</summary>
        public const string ScopeArgument = "--gamesim-views-scope";
        /// <summary>Holds the whole house drawn (EpisodeDirector.HoldWholeHouse): ROOM-SCOPE-PLAN §6.2's rollback switch.</summary>
        public const string WholeHouseArgument = "--gamesim-whole-house";

        public const string TimingPass = "timing", CountersPass = "counters", AblationsPass = "ablations";
        public static readonly IReadOnlyList<string> Passes = Array.AsReadOnly(new[] { TimingPass, CountersPass, AblationsPass });

        /// <summary>Each view is held this long to settle, then this long to measure (§5.3).</summary>
        public const double SettleSeconds = 10, MeasureSeconds = 20;
        public const double QuickSettleSeconds = 2, QuickMeasureSeconds = 3;
        /// <summary>Timing passes run every view three times; the counter pass once.</summary>
        public const int TimingRepeats = 3, CounterRepeats = 1;
        /// <summary>A view's medians must lie within 5% of each other, or it is run once more and flagged.</summary>
        public const double RepeatabilityTolerance = .05;

        public const string Repeatable = "Repeatable", NotRepeatable = "Not repeatable", SingleRun = "Single run";

        /// <summary>The modes that build or walk their own season: the views hold the house still, so they run alone.</summary>
        private static readonly string[] ExclusiveModes =
        {
            "--gamesim-verify-season", "--gamesim-verify-study", "--gamesim-verify-blocs", "--gamesim-verify-autonomy",
            "--gamesim-verify-creator", "--gamesim-look-sheet",
        };

        /// <summary>
        /// Why a views request cannot run, or null when it can (or none was made). Judged as written,
        /// before anything starts, so a typo never measures the wrong thing for twenty minutes.
        /// </summary>
        public static string Refusal(IList<string> args)
        {
            if (args == null || !args.Contains(Argument)) return null;
            if (!args.Contains("--gamesim-verify"))
                return Argument + " requires --gamesim-verify and an isolated absolute --gamesim-save-root.";
            foreach (string mode in ExclusiveModes)
                if (args.Contains(mode)) return Argument + " holds the house still and runs alone; it cannot run with " + mode + ".";
            string pass = TimingPass;
            if (args.Contains(PassArgument))
            {
                if (!TryValue(args, PassArgument, out pass)) return PassArgument + " needs a pass: " + string.Join(", ", Passes) + ".";
                if (!Passes.Contains(pass)) return PassArgument + " " + pass + " is not a pass: " + string.Join(", ", Passes) + ".";
            }
            if (args.Contains(LiveArgument) && pass != TimingPass)
                return LiveArgument + " is a timing pass: the live world moves the cast, so its counters would compare nothing.";
            if (args.Contains(ScopeArgument) && args.Contains(WholeHouseArgument))
                return ScopeArgument + " and " + WholeHouseArgument + " ask for opposite things.";
            if (args.Contains(OnlyArgument))
            {
                if (!TryValue(args, OnlyArgument, out string only) || Split(only).Length == 0) return OnlyArgument + " needs view ids, such as V1,V4a.";
                foreach (string id in Split(only))
                    if (Find(id) == null) return OnlyArgument + ": " + id + " is not a view (" + string.Join(", ", All.Select(view => view.Id)) + ").";
            }
            if (args.Contains(AblationsArgument))
            {
                if (pass != AblationsPass) return AblationsArgument + " belongs to the ablations pass.";
                if (!TryValue(args, AblationsArgument, out string list) || Split(list).Length == 0) return AblationsArgument + " needs ablation names.";
                foreach (string name in Split(list))
                    if (!Ablations.Contains(name)) return AblationsArgument + ": " + name + " is not an ablation (" + string.Join(", ", Ablations) + ").";
            }
            return null;
        }

        /// <summary>The request as the command line makes it, or null when no views run was asked for. Call <see cref="Refusal"/> first.</summary>
        public static VerificationViewsRequest Read(IList<string> args)
        {
            if (args == null || !args.Contains(Argument)) return null;
            var request = new VerificationViewsRequest
            {
                Pass = TryValue(args, PassArgument, out string pass) && Passes.Contains(pass) ? pass : TimingPass,
                LiveWorld = args.Contains(LiveArgument),
                Quick = args.Contains(QuickArgument),
                HoldScope = args.Contains(ScopeArgument),
                WholeHouse = args.Contains(WholeHouseArgument),
            };
            if (TryValue(args, OnlyArgument, out string only))
                request.Only = Split(only).Select(id => Find(id)).Where(view => view != null).Select(view => view.Id).Distinct().ToArray();
            if (TryValue(args, AblationsArgument, out string ablations))
                request.Ablations = Split(ablations).Where(name => Ablations.Contains(name)).Distinct().ToArray();
            request.Settle = request.Quick ? QuickSettleSeconds : SettleSeconds;
            request.Measure = request.Quick ? QuickMeasureSeconds : MeasureSeconds;
            request.Repeats = request.Quick || request.Pass == CountersPass ? CounterRepeats : TimingRepeats;
            return request;
        }

        private static bool TryValue(IList<string> args, string name, out string value)
        {
            value = null;
            int at = args.IndexOf(name);
            if (at < 0 || at + 1 >= args.Count || args[at + 1].StartsWith("--", StringComparison.Ordinal)) return false;
            value = args[at + 1];
            return true;
        }

        private static string[] Split(string list)
            => (list ?? "").Split(',').Select(part => part.Trim()).Where(part => part.Length > 0).ToArray();

        /// <summary>The report's file name in the save root: the pass, the house size, and how the cast stood.</summary>
        public static string ReportName(VerificationViewsRequest request, int houseSize)
            => "views-" + request.Pass + "-" + houseSize.ToString(CultureInfo.InvariantCulture)
               + (request.LiveWorld ? "-live" : "") + (request.HoldScope ? "-scope" : request.WholeHouse ? "-whole-house" : "")
               + (request.Quick ? "-quick" : "");

        // ------------------------------------------------------------------------------------
        // The house the views are taken in, and the marks the cast stands on.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The eight floors in plan, in <see cref="RoomLayers"/> order, and each room's marker (where the
        /// game walks people to: a walkable spot in every room). The probe checks the saved house still
        /// has these floors before it measures, so a before and an after compare the same frames.
        /// </summary>
        public static readonly IReadOnlyList<VerificationRoomPlan> House = Array.AsReadOnly(new[]
        {
            new VerificationRoomPlan("Living", -14, 0, -10, 0, -5, -7.6),
            new VerificationRoomPlan("Kitchen", 0, 14, -10, 0, 3, -6),
            new VerificationRoomPlan("Bedroom", -14, 0, 0, 10, -5, 2),
            new VerificationRoomPlan("Private", 0, 14, 0, 10, 7, 3),
            new VerificationRoomPlan("Yard", -14, 14, 10, 20, 0, 14),
            new VerificationRoomPlan("HoH", -14, -4.6167, -20, -10, -9.3083, -15),
            new VerificationRoomPlan("Nomination", -4.6167, 4.7667, -20, -10, .075, -15),
            new VerificationRoomPlan("Games", 4.7667, 14.15, -20, -10, 9.4583, -15),
        });

        /// <summary>How far a scene's floor may stand from the plan's before the views stop comparing (metres).</summary>
        public const double HouseTolerance = .02;

        public static VerificationRoomPlan Room(string room)
        {
            foreach (var plan in House) if (plan.Room == room) return plan;
            return null;
        }

        /// <summary>
        /// The order the marks fill the rooms, two to a room: at twelve, two in each north-block room,
        /// the Yard and Nomination, with HoH and Games empty; at sixteen, two in every room (§5.3).
        /// </summary>
        public static readonly IReadOnlyList<string> MarkOrder = Array.AsReadOnly(new[]
            { "Living", "Kitchen", "Bedroom", "Private", "Yard", "Nomination", "HoH", "Games" });

        /// <summary>The two marks of a room stand this far either side of its marker, along x, facing each other.</summary>
        public const double MarkHalfSpacing = .75;
        public const int MostBodies = 16;

        /// <summary>
        /// Where <paramref name="bodies"/> bodies stand: body i at mark i, the player first, then the
        /// houseguests in the season's order. Body 2k and 2k + 1 share a room and face each other.
        /// </summary>
        public static IReadOnlyList<VerificationMark> Marks(int bodies)
        {
            if (bodies < 0 || bodies > MostBodies) throw new ArgumentOutOfRangeException(nameof(bodies), "The house holds 0 to " + MostBodies + " bodies.");
            var marks = new List<VerificationMark>(bodies);
            for (int i = 0; i < bodies; i++)
            {
                var room = Room(MarkOrder[i / 2]);
                double side = i % 2 == 0 ? -MarkHalfSpacing : MarkHalfSpacing;
                marks.Add(new VerificationMark(i, room.Room, room.MarkX + side, room.MarkZ, i % 2 == 0 ? i + 1 : i - 1));
            }
            return marks;
        }

        // ------------------------------------------------------------------------------------
        // The views (§5.3). Today's camera, written down: the scene's rig (houseCenter (0.075, 1.2, 0),
        // yaw 45, 10 to 34 m, pitch 45 to 62 with distance) and its 55-degree lens.
        // ------------------------------------------------------------------------------------

        public const double TodayCentreX = .075, TodayCentreY = 1.2, TodayCentreZ = 0;
        public const double TodayYaw = 45, TodayFieldOfView = 55;
        public const double TodayNearest = 10, TodayFarthest = 34, TodayClosePitch = 45, TodayFarPitch = 62;
        /// <summary>The camera's clip planes in the scene, for the shadow model's frustum.</summary>
        public const double TodayNearClip = .15, TodayFarClip = 150;

        /// <summary>Today's pitch for a distance: HouseCameraRig.PitchFor with the scene's 10 to 34 m (the curve RS5 keeps in metres).</summary>
        public static double TodayPitchFor(double distance)
            => TodayClosePitch + (TodayFarPitch - TodayClosePitch) * Math.Max(0, Math.Min(1, (distance - TodayNearest) / (TodayFarthest - TodayNearest)));

        /// <summary>D4's farthest zoom along each room's best heading: 11.25 m at yaw 0 in the north block, 10.5 m at yaw 90 in the south wing, 18.25 m at yaw 0 for the Yard.</summary>
        public const double NorthRoomFarthest = 11.25, SouthRoomFarthest = 10.5, YardFarthest = 18.25;
        public const double NorthRoomHeading = 0, SouthRoomHeading = 90, YardHeading = 0;
        /// <summary>D4's rooms are also captured at the scene's yaw, for the owner's comparison.</summary>
        public const double SecondCaptureYaw = 45;

        /// <summary>The conversation two-shot (HouseCameraRig.TwoShotDistance, TwoShotPitch, TwoShotFieldOfView, TwoShotLift).</summary>
        public const double TwoShotDistance = 4.6, TwoShotPitch = 25, TwoShotFieldOfView = 40, TwoShotLift = -.3;
        /// <summary>The ceremony framing (EpisodeDirector.CeremonyDistance, at the room's marker, the rig's yaw).</summary>
        public const double CeremonyDistance = 11;
        /// <summary>The competition shot (EpisodeDirector.CompetitionShotDistance, Pitch, FieldOfView) over the station.</summary>
        public const double CompetitionDistance = 12, CompetitionPitch = 18, CompetitionFieldOfView = 50;
        /// <summary>The diary interview (EpisodeDirector.DiaryShotFieldOfView, DiaryShotDepthOfField).</summary>
        public const double DiaryFieldOfView = 40, DiaryDepthOfField = .7;
        /// <summary>The overview (EpisodeDirector.OverviewPitch, OverviewOrthographicSize, OverviewLift), at the farthest distance.</summary>
        public const double OverviewPitch = 62, OverviewHalfHeight = 22, OverviewLift = 3.3;
        /// <summary>The W3 full-field capture's lens (EpisodePlayModeTests.CompetitionApparatus: 1600 x 900, 50 degrees).</summary>
        public const double FullFieldFieldOfView = 50;

        /// <summary>Why V9 is declared and never measured on this branch.</summary>
        public const string FullFieldSkipped =
            "V9 is the W3 full-field capture's eye re-derived from the props branch's FullFieldView (claude/props-competitions), "
            + "which is not on this branch; until it lands V9 is the model's calibration eye only (42 maps), not a measured view.";

        private static readonly VerificationView[] views = BuildViews();

        /// <summary>Every view, V1 to V9, in the order the passes take them.</summary>
        public static readonly IReadOnlyList<VerificationView> All = Array.AsReadOnly(views);

        public static VerificationView Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var view in views) if (string.Equals(view.Id, id, StringComparison.OrdinalIgnoreCase)) return view;
            return null;
        }

        /// <summary>The views a request runs, in order: every view, or its subset, never a skipped one.</summary>
        public static IReadOnlyList<VerificationView> Requested(VerificationViewsRequest request)
            => views.Where(view => view.SkippedBecause == null
                && (request == null || request.Only == null || request.Only.Contains(view.Id))).ToArray();

        private static VerificationView[] BuildViews()
        {
            var list = new List<VerificationView>
            {
                VerificationView.Pose("V1", "Today's default: 24 m over the house centre, yaw 45", null,
                    TodayCentreX, TodayCentreY, TodayCentreZ, 24, TodayPitchFor(24), TodayYaw, TodayFieldOfView),
                VerificationView.Overview("V2", "The overview",
                    TodayCentreX - Math.Sin(TodayYaw * Deg) * OverviewLift, TodayCentreY, TodayCentreZ - Math.Cos(TodayYaw * Deg) * OverviewLift,
                    TodayFarthest, OverviewPitch, TodayYaw, OverviewHalfHeight),
                VerificationView.Pose("V3", "The closest free zoom (10 m) on the Living room's centre", "Living",
                    Room("Living").CentreX, TodayCentreY, Room("Living").CentreZ, TodayNearest, TodayPitchFor(TodayNearest), TodayYaw, TodayFieldOfView),
            };
            string[] letters = { "a", "b", "c", "d", "e", "f", "g", "h" };
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                var room = Room(RoomLayers.RoomIds[i]);
                // The south wing's rooms lie wholly south of the north block's wall line, z = -10.
                bool yard = room.Room == "Yard", south = room.MaxZ < -9.5;
                double distance = yard ? YardFarthest : south ? SouthRoomFarthest : NorthRoomFarthest;
                double heading = yard ? YardHeading : south ? SouthRoomHeading : NorthRoomHeading;
                list.Add(VerificationView.Pose("V4" + letters[i], room.Room + " at D4's farthest zoom and best heading", room.Room,
                    room.CentreX, TodayCentreY, room.CentreZ, distance, TodayPitchFor(distance), heading, TodayFieldOfView, captureAtYaw45: true));
            }
            // The two-shot over the Living room's pair (marks 0 and 1, along x): from the north,
            // inside the room, the pair's yaw turned a quarter (HouseCameraRig.TwoShot's side).
            var living = Room("Living");
            list.Add(VerificationView.Pose("V5", "A Living-room two-shot over the pair on its marks", "Living",
                living.MarkX, 1 + TwoShotLift, living.MarkZ, TwoShotDistance, TwoShotPitch, 180, TwoShotFieldOfView, depthOfField: 1));
            var nomination = Room("Nomination");
            list.Add(VerificationView.Pose("V6", "Ceremony framing at the nomination marker, the cast two per room", "Nomination",
                nomination.MarkX, TodayCentreY, nomination.MarkZ, CeremonyDistance, TodayPitchFor(CeremonyDistance), TodayYaw, TodayFieldOfView));
            list.Add(VerificationView.Pose("V6g", "Ceremony framing with the cast at the nomination table's places: the busiest real frame", "Nomination",
                nomination.MarkX, TodayCentreY, nomination.MarkZ, CeremonyDistance, TodayPitchFor(CeremonyDistance), TodayYaw, TodayFieldOfView,
                ceremonyPlaces: true));
            // The station stands on the yard's centre line (EpisodeDirector.StationPosition); the shot
            // frames a metre over it.
            list.Add(VerificationView.Pose("V7", "The competition shot over the station", "Yard",
                0, 1, 15, CompetitionDistance, CompetitionPitch, 0, CompetitionFieldOfView));
            // The diary chair (bb_set_diarychair at (7, 0, 4), yaw 180): its anchor's camera offset puts
            // the eye at (7, 1.45, 1.4), looking at the face. The probe opens the real diary; this pose
            // is its fallback and the model's.
            list.Add(VerificationView.Eye("V8", "The diary interview", "Private", VerificationViewRoute.Diary,
                7, 1.45, 1.4, 7, 1.15, 4.0, DiaryFieldOfView, DiaryDepthOfField, null));
            // The W3 capture's eye on this branch: over the yard floor's centre, 22.4 m back and 9 m up.
            list.Add(VerificationView.Eye("V9", "The W3 full-field capture's eye", "Yard", VerificationViewRoute.Skipped,
                0, .95 + 9, 15 - 22.4, 0, .95, 15, FullFieldFieldOfView, 0, FullFieldSkipped));
            return list.ToArray();
        }

        internal const double Deg = Math.PI / 180;

        // ------------------------------------------------------------------------------------
        // The ablations (§5.3): development build, graphical, at 16; at V1, V4a and V6g, and the
        // drawer's small-mesh culling at V1 and V2.
        // ------------------------------------------------------------------------------------

        public const string Baseline = "baseline", LampShadowsOff = "lamp-shadows-off", BodiesOff = "bodies-off",
            SsaoOff = "ssao-off", MoonShadowsOff = "moon-shadows-off", Confinement = "rendering-layer-confinement",
            SmallMeshCulling = "small-mesh-culling";

        /// <summary>Every ablation the pass knows; the baseline is not one, it is always measured.</summary>
        public static readonly IReadOnlyList<string> Ablations = Array.AsReadOnly(new[]
            { LampShadowsOff, BodiesOff, SsaoOff, MoonShadowsOff, Confinement, SmallMeshCulling });

        public static readonly IReadOnlyList<string> AblationViews = Array.AsReadOnly(new[] { "V1", "V4a", "V6g" });
        public static readonly IReadOnlyList<string> SmallMeshViews = Array.AsReadOnly(new[] { "V1", "V2" });

        /// <summary>The drawer's small-mesh threshold the ablation sets, in percent of the screen (PC_RPAsset ships 0).</summary>
        public const float SmallMeshAblationPercent = 2f;

        /// <summary>The (view, ablation) pairs the ablations pass measures, in order, each with its view's baseline first.</summary>
        public static IReadOnlyList<VerificationAblationStep> AblationPlan(VerificationViewsRequest request)
        {
            var wanted = request != null && request.Ablations != null ? request.Ablations : Ablations.ToArray();
            var steps = new List<VerificationAblationStep>();
            foreach (string view in AblationViews.Concat(SmallMeshViews).Distinct())
            {
                if (request != null && request.Only != null && !request.Only.Contains(view)) continue;
                var mine = new List<string>();
                if (AblationViews.Contains(view))
                    mine.AddRange(Ablations.Where(name => name != SmallMeshCulling && wanted.Contains(name)));
                if (SmallMeshViews.Contains(view) && wanted.Contains(SmallMeshCulling)) mine.Add(SmallMeshCulling);
                if (mine.Count == 0) continue;
                steps.Add(new VerificationAblationStep(view, Baseline));
                foreach (string name in mine) steps.Add(new VerificationAblationStep(view, name));
            }
            return steps;
        }

        /// <summary>
        /// Rendering-layer confinement (D8 ii) in the eight rendering layers the project names (bits 0
        /// to 7, TagManager's "Default" and "Light Layer 1" to 7): room i's renderers and lamps share
        /// bit i; the shell and every light that is not a lamp take all eight, so the fills, the screens
        /// and the Moon light everything they lit before.
        /// </summary>
        public static uint ConfinementBit(int room) => room >= 0 && room < RoomLayers.Count ? 1u << room : AllConfinementBits;
        public const uint AllConfinementBits = (1u << RoomLayers.Count) - 1;

        // ------------------------------------------------------------------------------------
        // The report arithmetic.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Whether a view's medians lie within <see cref="RepeatabilityTolerance"/> of each other: the
        /// largest no more than 5% over the smallest. One median (a counter pass) is a single run.
        /// </summary>
        public static string Repeatability(IReadOnlyList<double> medians, out double spread)
        {
            spread = 0;
            if (medians == null || medians.Count < 2) return SingleRun;
            double low = medians.Min(), high = medians.Max();
            spread = low > 0 ? (high - low) / low : high > 0 ? double.PositiveInfinity : 0;
            return spread <= RepeatabilityTolerance + 1e-9 ? Repeatable : NotRepeatable;
        }

        /// <summary>
        /// Completes a view's row from its runs: the medians in run order, the median of them, the
        /// repeatability of the first runs (a rerun's is judged with them), and the flag a rerun leaves.
        /// </summary>
        public static void CompleteRow(VerificationViewRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            var runs = row.runs ?? new List<VerificationViewRun>();
            row.medians = runs.Select(run => run.frames != null ? run.frames.medianMs : 0).ToArray();
            var first = runs.Where(run => !run.rerun).Select(run => run.frames != null ? run.frames.medianMs : 0).ToList();
            row.repeatability = Repeatability(first, out double spread);
            row.spread = spread;
            row.rerun = runs.Any(run => run.rerun);
            row.flagged = row.rerun || row.repeatability == NotRepeatable;
            var sorted = row.medians.OrderBy(value => value).ToArray();
            row.medianOfMediansMs = sorted.Length == 0 ? 0 : sorted[(sorted.Length - 1) / 2];
            row.cpuMainMedianMs = MedianOfKnown(runs.Select(run => run.cpuMainMedianMs));
            row.cpuRenderMedianMs = MedianOfKnown(runs.Select(run => run.cpuRenderMedianMs));
            row.gpuMedianMs = MedianOfKnown(runs.Select(run => run.gpuMedianMs));
        }

        /// <summary>Whether a row needs its one more run: timed, judged and not repeatable, and not yet rerun.</summary>
        public static bool NeedsRerun(VerificationViewRow row)
            => row != null && row.repeatability == NotRepeatable && !row.rerun;

        /// <summary>The median of the values that were measured (non-negative), or -1 when none were.</summary>
        public static double MedianOfKnown(IEnumerable<double> values)
        {
            var known = (values ?? Enumerable.Empty<double>()).Where(value => value >= 0 && !double.IsNaN(value)).OrderBy(value => value).ToArray();
            return known.Length == 0 ? -1 : known[(known.Length - 1) / 2];
        }

        /// <summary>
        /// The run's outcome: Passed when nothing went wrong - no error, no failed check, every
        /// requested view measured - otherwise Failed. A flagged view is reported, not failed: the
        /// 5% rule says when to look twice, not that the numbers are wrong.
        /// </summary>
        public static string Status(VerificationViewsReport report)
        {
            if (report == null) return "Failed";
            bool errors = report.errors != null && report.errors.Length > 0;
            bool checks = report.checks != null && report.checks.Any(check => check.outcome == VerificationProbeCheck.Failed);
            var rows = report.pass == AblationsPass ? report.ablations : report.views;
            bool measured = rows != null && rows.Count > 0 && rows.All(row => row.runs != null && row.runs.Count > 0 && row.runs.All(run => run.frames != null && run.frames.frames > 0));
            return !errors && !checks && measured ? "Passed" : "Failed";
        }

        /// <summary>
        /// Reads URP's atlas line ("Reduced additional punctual light shadows resolution by 8 to make
        /// 84 shadow maps fit in the 2048x2048 shadow atlas"), which the development build logs only
        /// under UNITY_ENABLE_CHECKS for every new configuration, and a release player once in its life.
        /// </summary>
        public static bool TryParseAtlasMessage(string text, out int scale, out int slices, out int atlas)
        {
            scale = slices = atlas = 0;
            const string Head = "Reduced additional punctual light shadows resolution by ";
            if (string.IsNullOrEmpty(text)) return false;
            int at = text.IndexOf(Head, StringComparison.Ordinal);
            if (at < 0) return false;
            var words = text.Substring(at + Head.Length).Split(' ');
            // "<scale> to make <slices> shadow maps fit in the <atlas>x<atlas> shadow atlas..."
            if (words.Length < 10 || words[1] != "to" || words[2] != "make" || words[4] != "shadow" || words[5] != "maps") return false;
            if (!int.TryParse(words[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out scale)
                || !int.TryParse(words[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out slices)) return false;
            var size = words[9].Split('x');
            return size.Length == 2 && int.TryParse(size[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out atlas);
        }

        // ------------------------------------------------------------------------------------
        // The shadow-map model (§5.3): the shadowed lights the main camera's culling keeps, six faces
        // for a point light and one for a spot, at their tier, halved until all fit - URP 17.6's
        // AdditionalLightsShadowAtlasLayout, step for step.
        // ------------------------------------------------------------------------------------

        /// <summary>URP's floor on a punctual shadow map (ShadowUtils.kMinimumPunctualLight*ShadowResolution).</summary>
        public const int MinimumSoftShadowResolution = 16, MinimumHardShadowResolution = 8;
        /// <summary>URP's cap on shadow slices without structured buffers (ShaderOptions.k_MaxVisibleLightCountDesktop).</summary>
        public const int DesktopMaxSlices = 256;

        /// <summary>The lights a frustum keeps: those whose range sphere it touches, as Unity's culling does for a point light.</summary>
        public static List<VerificationShadowLight> InFrustum(VerificationFrustum frustum, IEnumerable<VerificationShadowLight> lights)
        {
            var kept = new List<VerificationShadowLight>();
            if (frustum == null || lights == null) return kept;
            foreach (var light in lights)
                if (frustum.SphereVisible(light.X, light.Y, light.Z, light.Range)) kept.Add(light);
            return kept;
        }

        /// <summary>
        /// Lays the shadow requests of <paramref name="visible"/> (in visible-light order) into a square
        /// atlas of <paramref name="atlasSize"/> as URP 17.6 does: sorted by resolution, hard before soft,
        /// then by distance to the eye; lights dropped from the end while even the estimated scale leaves
        /// them under URP's minimum; then packed into squares, the scale doubling until everything fits.
        /// </summary>
        public static VerificationShadowAtlas LayoutShadowAtlas(IList<VerificationShadowLight> visible,
            double eyeX, double eyeY, double eyeZ, int atlasSize, int maxSlices = DesktopMaxSlices)
        {
            var requests = new List<Request>();
            if (visible != null)
                for (int light = 0; light < visible.Count; light++)
                {
                    var l = visible[light];
                    double dx = eyeX - l.X, dy = eyeY - l.Y, dz = eyeZ - l.Z;
                    int slices = l.Spot ? 1 : 6;
                    for (int slice = 0; slice < slices; slice++)
                        requests.Add(new Request { Light = light, Slice = slice, Resolution = l.Resolution, Soft = l.Soft, Point = !l.Spot, Distance = dx * dx + dy * dy + dz * dz });
                }
            var sorted = requests.OrderByDescending(r => r.Resolution).ThenBy(r => r.Soft ? 1 : 0).ThenBy(r => r.Distance)
                .ThenBy(r => r.Light).ThenBy(r => r.Slice).ToArray();
            int total = Math.Min(sorted.Length, maxSlices);
            bool enough = false;
            int estimated = 1;
            while (!enough && total > 0)
            {
                var last = sorted[total - 1];
                estimated = EstimateScale(sorted, total, atlasSize);
                if (last.Resolution >= estimated * Minimum(last.Soft)) enough = true;
                else total -= last.Point ? 6 : 1;
            }
            bool fits = false, tooMany = false;
            int scale = estimated;
            var allocated = new int[Math.Max(0, total)];
            var free = new List<Square>();
            while (!fits && !tooMany)
            {
                free.Clear();
                free.Add(new Square(0, 0, atlasSize, atlasSize));
                fits = true;
                for (int i = 0; i < total; i++)
                {
                    int resolution = sorted[i].Resolution / scale;
                    if (resolution < Minimum(sorted[i].Soft)) { tooMany = true; break; }
                    bool found = false;
                    for (int area = 0; area < free.Count; area++)
                    {
                        var square = free[area];
                        if (square.Width < resolution) continue;
                        allocated[i] = resolution;
                        free.RemoveAt(area);
                        // The rest of the square, cut into squares of this size, where it was.
                        int remaining = total - i - 1, added = 0, x = square.X, y = square.Y;
                        while (added < remaining)
                        {
                            x += resolution;
                            if (x + resolution > square.X + square.Width)
                            {
                                x = square.X;
                                y += resolution;
                                if (y + resolution > square.Y + square.Height) break;
                            }
                            free.Insert(area + added, new Square(x, y, resolution, resolution));
                            added++;
                        }
                        found = true;
                        break;
                    }
                    if (!found) { fits = false; break; }
                }
                if (!fits && !tooMany) scale *= 2;
            }
            var kept = total > 0 && !tooMany ? allocated.Take(total).ToArray() : new int[0];
            return new VerificationShadowAtlas
            {
                lights = visible != null ? visible.Count : 0,
                slicesRequested = sorted.Length,
                slicesKept = Math.Max(0, total),
                scale = scale,
                atlasSize = atlasSize,
                tooMany = tooMany,
                largestSlice = kept.Length > 0 ? kept.Max() : 0,
                smallestSlice = kept.Length > 0 ? kept.Min() : 0,
            };
        }

        private static int Minimum(bool soft) => soft ? MinimumSoftShadowResolution : MinimumHardShadowResolution;

        private static int EstimateScale(Request[] requests, int end, int atlasSize)
        {
            long atlasTexels = (long)atlasSize * atlasSize, wanted = 0;
            for (int i = 0; i < end; i++) wanted += (long)requests[i].Resolution * requests[i].Resolution;
            int scale = 1;
            while (wanted > atlasTexels * scale * scale) scale *= 2;
            return scale;
        }

        private struct Request
        {
            public int Light, Slice, Resolution;
            public bool Soft, Point;
            public double Distance;
        }

        private readonly struct Square
        {
            public readonly int X, Y, Width, Height;
            public Square(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
        }
    }

    /// <summary>What the command line asked the views probe for (<see cref="VerificationViews.Read"/>).</summary>
    public sealed class VerificationViewsRequest
    {
        public string Pass = VerificationViews.TimingPass;
        public bool LiveWorld, Quick, HoldScope, WholeHouse;
        /// <summary>The views to run by id, or null for every view.</summary>
        public string[] Only;
        /// <summary>The ablations to run, or null for every ablation.</summary>
        public string[] Ablations;
        public double Settle = VerificationViews.SettleSeconds, Measure = VerificationViews.MeasureSeconds;
        public int Repeats = VerificationViews.TimingRepeats;
    }

    /// <summary>How a view reaches its frame: a shot with its numbers, the overview, the diary, or not at all.</summary>
    public enum VerificationViewRoute { Shot, Overview, Diary, Skipped }

    /// <summary>One of the probe's views: its pose in today's numbers and the room it is about (null: the whole house).</summary>
    public sealed class VerificationView
    {
        public readonly string Id, Label, Room;
        public readonly VerificationViewRoute Route;
        public readonly double FocusX, FocusY, FocusZ, Distance, Pitch, Yaw, FieldOfView, DepthOfField;
        /// <summary>The overview's lens: orthographic, this half-height. Zero for a perspective view.</summary>
        public readonly double OrthographicHalfHeight;
        /// <summary>Captured again at the scene's yaw (D4's rooms).</summary>
        public readonly bool CaptureAtYaw45;
        /// <summary>The cast stands at the nomination table's places, not on the marks.</summary>
        public readonly bool CeremonyPlaces;
        /// <summary>Why the view is declared but never measured, or null.</summary>
        public readonly string SkippedBecause;

        private VerificationView(string id, string label, string room, VerificationViewRoute route, double fx, double fy, double fz,
            double distance, double pitch, double yaw, double fieldOfView, double depthOfField, double orthographic,
            bool captureAtYaw45, bool ceremonyPlaces, string skipped)
        {
            Id = id; Label = label; Room = room; Route = route;
            FocusX = fx; FocusY = fy; FocusZ = fz; Distance = distance; Pitch = pitch; Yaw = yaw;
            FieldOfView = fieldOfView; DepthOfField = depthOfField; OrthographicHalfHeight = orthographic;
            CaptureAtYaw45 = captureAtYaw45; CeremonyPlaces = ceremonyPlaces; SkippedBecause = skipped;
        }

        internal static VerificationView Pose(string id, string label, string room, double fx, double fy, double fz,
            double distance, double pitch, double yaw, double fieldOfView, double depthOfField = 0,
            bool captureAtYaw45 = false, bool ceremonyPlaces = false)
            => new VerificationView(id, label, room, VerificationViewRoute.Shot, fx, fy, fz, distance, pitch, yaw, fieldOfView,
                depthOfField, 0, captureAtYaw45, ceremonyPlaces, null);

        internal static VerificationView Overview(string id, string label, double fx, double fy, double fz,
            double distance, double pitch, double yaw, double halfHeight)
            => new VerificationView(id, label, null, VerificationViewRoute.Overview, fx, fy, fz, distance, pitch, yaw,
                VerificationViews.TodayFieldOfView, 0, halfHeight, false, false, null);

        /// <summary>A view from an eye looking at a point, turned into a pose the way the diary's shot is (EpisodeDirector.StartDiaryShot).</summary>
        internal static VerificationView Eye(string id, string label, string room, VerificationViewRoute route,
            double ex, double ey, double ez, double lx, double ly, double lz, double fieldOfView, double depthOfField, string skipped)
        {
            double dx = lx - ex, dy = ly - ey, dz = lz - ez;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double pitch = Math.Asin(Math.Max(-1, Math.Min(1, -dy / distance))) / VerificationViews.Deg;
            double yaw = Math.Atan2(dx, dz) / VerificationViews.Deg;
            return new VerificationView(id, label, room, route, lx, ly, lz, distance, pitch, yaw, fieldOfView, depthOfField, 0,
                false, false, skipped);
        }

        /// <summary>The same view at another yaw (D4's second capture).</summary>
        public VerificationView AtYaw(double yaw)
            => new VerificationView(Id, Label, Room, Route, FocusX, FocusY, FocusZ, Distance, Pitch, yaw, FieldOfView, DepthOfField,
                OrthographicHalfHeight, false, CeremonyPlaces, SkippedBecause);

        /// <summary>Where the eye stands: the focus, the distance back along the view.</summary>
        public void EyePosition(out double x, out double y, out double z)
        {
            VerificationFrustum.Basis(Pitch, Yaw, out var forward, out _, out _);
            x = FocusX - forward[0] * Distance;
            y = FocusY - forward[1] * Distance;
            z = FocusZ - forward[2] * Distance;
        }

        /// <summary>The view's frustum at an aspect, with today's clip planes.</summary>
        public VerificationFrustum Frustum(double aspect, double near = VerificationViews.TodayNearClip, double far = VerificationViews.TodayFarClip)
        {
            EyePosition(out double x, out double y, out double z);
            VerificationFrustum.Basis(Pitch, Yaw, out var forward, out var right, out var up);
            return new VerificationFrustum(x, y, z, forward, right, up,
                Math.Tan(FieldOfView * .5 * VerificationViews.Deg), aspect, near, far, OrthographicHalfHeight);
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// A camera's view volume as six inward planes, for the range-sphere test Unity's culling makes of
    /// a punctual light: a sphere is kept unless it lies wholly outside one plane.
    /// </summary>
    public sealed class VerificationFrustum
    {
        public readonly double EyeX, EyeY, EyeZ;
        private readonly double[][] normals = new double[6][];
        private readonly double[] offsets = new double[6];

        /// <summary>
        /// A frustum from an eye and its axes (unit vectors), the tangent of half the vertical field of
        /// view, the aspect (width over height) and the clip planes; orthographic with a half-height
        /// above zero.
        /// </summary>
        public VerificationFrustum(double eyeX, double eyeY, double eyeZ, double[] forward, double[] right, double[] up,
            double tanHalfVertical, double aspect, double near, double far, double orthographicHalfHeight = 0)
        {
            EyeX = eyeX; EyeY = eyeY; EyeZ = eyeZ;
            var eye = new[] { eyeX, eyeY, eyeZ };
            Set(0, forward, -Dot(forward, eye) - near);
            Set(1, Negate(forward), Dot(forward, eye) + far);
            if (orthographicHalfHeight > 0)
            {
                double halfWidth = orthographicHalfHeight * aspect;
                Set(2, right, -Dot(right, eye) + halfWidth);
                Set(3, Negate(right), Dot(right, eye) + halfWidth);
                Set(4, up, -Dot(up, eye) + orthographicHalfHeight);
                Set(5, Negate(up), Dot(up, eye) + orthographicHalfHeight);
                return;
            }
            int plane = 2;
            foreach (var (axis, tangent) in new[] { (right, tanHalfVertical * aspect), (up, tanHalfVertical) })
                foreach (int side in new[] { 1, -1 })
                {
                    // Through the eye, containing the edge direction forward + axis * tangent * side;
                    // its normal leans toward the forward axis, which is inside.
                    var normal = new double[3];
                    for (int i = 0; i < 3; i++) normal[i] = forward[i] * tangent - axis[i] * side;
                    double length = Math.Sqrt(Dot(normal, normal));
                    for (int i = 0; i < 3; i++) normal[i] /= length;
                    Set(plane++, normal, -Dot(normal, eye));
                }
        }

        /// <summary>
        /// A frustum from six inward planes (normal and offset: n · p + offset is positive inside), as a
        /// live camera's are read from its projection - the overview's blended lens included.
        /// </summary>
        public VerificationFrustum(double eyeX, double eyeY, double eyeZ, double[][] planeNormals, double[] planeOffsets)
        {
            if (planeNormals == null || planeOffsets == null || planeNormals.Length != 6 || planeOffsets.Length != 6)
                throw new ArgumentException("A frustum has six planes.");
            EyeX = eyeX; EyeY = eyeY; EyeZ = eyeZ;
            for (int i = 0; i < 6; i++) Set(i, planeNormals[i], planeOffsets[i]);
        }

        /// <summary>The rig's axes for a pitch down and a yaw (HouseCameraRig: rotation = Euler(pitch, yaw, 0)).</summary>
        public static void Basis(double pitch, double yaw, out double[] forward, out double[] right, out double[] up)
        {
            double p = pitch * VerificationViews.Deg, y = yaw * VerificationViews.Deg;
            forward = new[] { Math.Sin(y) * Math.Cos(p), -Math.Sin(p), Math.Cos(y) * Math.Cos(p) };
            right = new[] { Math.Cos(y), 0, -Math.Sin(y) };
            up = new[] { Math.Sin(y) * Math.Sin(p), Math.Cos(p), Math.Cos(y) * Math.Sin(p) };
        }

        /// <summary>Whether any of a sphere is inside: it is outside only when it lies wholly beyond one plane.</summary>
        public bool SphereVisible(double x, double y, double z, double radius)
        {
            for (int i = 0; i < 6; i++)
            {
                var n = normals[i];
                if (n[0] * x + n[1] * y + n[2] * z + offsets[i] < -radius) return false;
            }
            return true;
        }

        private void Set(int plane, double[] normal, double offset)
        {
            normals[plane] = (double[])normal.Clone();
            offsets[plane] = offset;
        }

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double[] Negate(double[] a) => new[] { -a[0], -a[1], -a[2] };
    }

    /// <summary>A room's floor in plan (x and z, metres) and its marker, where the game walks people to.</summary>
    public sealed class VerificationRoomPlan
    {
        public readonly string Room;
        public readonly double MinX, MaxX, MinZ, MaxZ, MarkX, MarkZ;

        public VerificationRoomPlan(string room, double minX, double maxX, double minZ, double maxZ, double markX, double markZ)
        {
            Room = room; MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ; MarkX = markX; MarkZ = markZ;
        }

        public double CentreX => (MinX + MaxX) * .5;
        public double CentreZ => (MinZ + MaxZ) * .5;
        public bool Contains(double x, double z) => x > MinX && x < MaxX && z > MinZ && z < MaxZ;
    }

    /// <summary>Body <see cref="Index"/>'s mark: a point in a room, and the body it stands with and faces.</summary>
    public readonly struct VerificationMark
    {
        public readonly int Index, Partner;
        public readonly string Room;
        public readonly double X, Z;

        public VerificationMark(int index, string room, double x, double z, int partner)
        {
            Index = index; Room = room; X = x; Z = z; Partner = partner;
        }
    }

    /// <summary>One step of the ablations pass: a view, and the one thing switched off for it (or the baseline).</summary>
    public readonly struct VerificationAblationStep
    {
        public readonly string View, Ablation;
        public VerificationAblationStep(string view, string ablation) { View = view; Ablation = ablation; }
        public override string ToString() => View + " " + Ablation;
    }

    /// <summary>A light as the shadow model sees it: where, how far it reaches, its kind, filtering and tier resolution.</summary>
    public readonly struct VerificationShadowLight
    {
        public readonly string Name;
        public readonly double X, Y, Z, Range;
        public readonly bool Spot, Soft;
        public readonly int Resolution;

        public VerificationShadowLight(string name, double x, double y, double z, double range, bool spot, bool soft, int resolution)
        {
            Name = name; X = x; Y = y; Z = z; Range = range; Spot = spot; Soft = soft; Resolution = resolution;
        }
    }

    /// <summary>The shadow atlas a view needs: the slices asked for and kept, the scale URP divides them by, and the sizes they get.</summary>
    [Serializable]
    public sealed class VerificationShadowAtlas
    {
        public int lights, slicesRequested, slicesKept, scale, atlasSize, largestSlice, smallestSlice;
        public bool tooMany;
    }

    // ----------------------------------------------------------------------------------------
    // The report: views-<pass>-<size>[...].json in the save root.
    // ----------------------------------------------------------------------------------------

    [Serializable]
    public sealed class VerificationViewsReport
    {
        public string status = "Running", pass, startedUtc, finishedUtc;
        /// <summary>"as shipped" (every source as the build has it), "held on" (HoldRoomScope) or "whole house" (HoldWholeHouse).</summary>
        public string scope;
        public string buildKind, unityVersion, gpu, processor, operatingSystem, graphicsDeviceType, resolution, renderPipeline;
        /// <summary>The pipeline asset's GPU Resident Drawer mode as the player runs it, and its small-mesh threshold.</summary>
        public string drawerMode;
        public float drawerSmallMeshPercent;
        /// <summary>Where the CPU and GPU times came from: FrameTimingManager, the profiler's counters, or none.</summary>
        public string frameTimingSource;
        public string seasonSource, sessionId;
        public long seasonSeed;
        public bool graphical, batchMode, developmentBuild, liveWorld, quick, houseHeld, ceremonySetsDressed;
        public int houseSize, houseSizeRequested, repeats, ceremonyPlaces;
        public double settleSeconds, measureSeconds;
        public float smallMeshAblationPercent = VerificationViews.SmallMeshAblationPercent;
        /// <summary>The drawer's own coverage counters, read once (P1); -1 when the build has none.</summary>
        public double drawerCoveragePercent = -1;
        public long drawerRenderers = -1, drawerExcludedRenderers = -1;
        public string[] viewsRequested, skippedViews, notes;
        public List<VerificationBodyMark> marks = new List<VerificationBodyMark>();
        public List<VerificationViewRow> views = new List<VerificationViewRow>();
        public List<VerificationViewRow> ablations = new List<VerificationViewRow>();
        public List<VerificationProbeCheck> checks = new List<VerificationProbeCheck>();
        public string rawFramesFile;
        public string[] errors;
    }

    /// <summary>One view (or, in the ablations pass, one view with one ablation): its runs and what was counted.</summary>
    [Serializable]
    public sealed class VerificationViewRow
    {
        public string id, label, room, route, ablation;
        /// <summary>Anything about how the view was reached that a reader must know (a fallback, an ablation that did not take).</summary>
        public string note;
        /// <summary>The rooms the director's scope said were drawn while the view was measured.</summary>
        public string viewedRooms;
        public List<VerificationViewRun> runs = new List<VerificationViewRun>();
        public double[] medians;
        public double medianOfMediansMs, spread;
        public string repeatability;
        public bool rerun, flagged;
        public double cpuMainMedianMs = -1, cpuRenderMedianMs = -1, gpuMedianMs = -1;
        public VerificationViewCounters counters;
        public VerificationShadowWork shadows;
        public List<VerificationRoomCount> bodiesByRoom = new List<VerificationRoomCount>();
        public List<VerificationViewCapture> captures = new List<VerificationViewCapture>();
    }

    [Serializable]
    public sealed class VerificationViewRun
    {
        public int repeat;
        public bool rerun;
        public double startedSeconds;
        public VerificationFrameSummary frames;
        public double cpuMainMedianMs = -1, cpuRenderMedianMs = -1, gpuMedianMs = -1;
    }

    /// <summary>
    /// A view's counters, each the median over its measured frames; -1 when the build does not
    /// report it. The engine's render counters are only comparable once P6 shows they include the
    /// drawer's draws; the drawer's count camera views only, never shadow views, summed over every
    /// camera in the frame (the live feed is paused for them).
    /// </summary>
    [Serializable]
    public sealed class VerificationViewCounters
    {
        public long batches = -1, setPassCalls = -1, drawCalls = -1, triangles = -1, vertices = -1, shadowCasters = -1, visibleSkinnedMeshes = -1;
        public long drawerTotalInstances = -1, drawerVisibleInstances = -1, drawerLayerCulled = -1, drawerFrustumCulled = -1, drawerSmallMeshCulled = -1;
    }

    /// <summary>
    /// A view's shadow work, counted by the probe inside the main camera's begin callback (after any
    /// scope hook): the shadowed lights its frustum keeps, the atlas they need, and the classifier's
    /// casters in each lamp's reach. The atlas line, when URP logged one, is the cross-check (P7).
    /// </summary>
    [Serializable]
    public sealed class VerificationShadowWork
    {
        public int lightsInFrustum, lampsInFrustum, spotsInFrustum;
        public VerificationShadowAtlas atlas;
        public int houseCasters, bodyCasters;
        public List<VerificationLampWork> lamps = new List<VerificationLampWork>();
        /// <summary>"agrees", "disagrees", "not logged" or "not checked" against URP's atlas line.</summary>
        public string atlasLine = "not checked";
        public int loggedSlices = -1, loggedScale = -1;
    }

    [Serializable]
    public sealed class VerificationLampWork
    {
        public string name, room;
        public float x, y, z, range;
        public bool enabled, inFrustum;
        public int houseCasters, bodyCasters;
    }

    [Serializable]
    public sealed class VerificationBodyMark
    {
        public string id, room, walkerRoom;
        public double markX, markZ;
        public float placedX, placedY, placedZ;
        public bool placed;
    }

    [Serializable]
    public sealed class VerificationRoomCount
    {
        public string room;
        public int bodies;
    }

    [Serializable]
    public sealed class VerificationViewCapture
    {
        public string path, failure;
        public double yaw;
        public bool rendered;
    }

    /// <summary>One of the drawer-path checks P1 to P7: Passed, Failed (the run fails), Recorded (for the record), or Not run.</summary>
    [Serializable]
    public sealed class VerificationProbeCheck
    {
        public const string Passed = "Passed", Failed = "Failed", Recorded = "Recorded", NotRun = "Not run";
        public string id, outcome, detail;

        public static VerificationProbeCheck Of(string id, string outcome, string detail)
            => new VerificationProbeCheck { id = id, outcome = outcome, detail = detail };
    }
}
