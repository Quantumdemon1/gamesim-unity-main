using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Gamesim.House;
using Gamesim.Presentation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamesim.Episode
{
    /// <summary>
    /// The room scope's probe (ROOM-SCOPE-PLAN §5.3, RS0): <c>--gamesim-verify-views</c>, beside the
    /// 300 s profile. It holds the house still - the cast on fixed marks, the NPC world stood down -
    /// and holds each view of <see cref="VerificationViews.All"/> to settle and to measure: frame
    /// times in the timing pass, the engine's and the drawer's counters and the drawer-path checks
    /// P1 to P7 in the counter pass, and the six ablations in the ablations pass. Its own shadow-map
    /// count is taken inside the main camera's begin callback. Nothing it switches outlives the run:
    /// every hold, layer, mask, light and asset setting it touches is put back, and the player quits.
    ///
    /// <para>The views compare; they give no verdict. Report: <c>views-&lt;pass&gt;-&lt;size&gt;[...].json</c>
    /// in the save root, with every measured frame beside it in a CSV.</para>
    /// </summary>
    public sealed partial class PortVerification
    {
        public const string DrawerCategoryName = "GPU Resident Drawer";
        /// <summary>The engine's render counters (ProfilerCategory.Render), read only once P6 shows they include the drawer's draws.</summary>
        public static readonly string[] RenderCounterNames =
            { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count", "Visible Skinned Meshes Count" };
        /// <summary>The drawer's per-frame counters (core 17.6 GRDProfilerCounters), development builds with the profiler on.</summary>
        public static readonly string[] DrawerCounterNames = { "Total Instances", "Visible Instances", "Layer Culled", "Frustum Culled", "Small Mesh Culled" };
        public const string DrawerRenderersCounter = "GRD Renderers", DrawerExcludedCounter = "Excluded Renderers", DrawerCoverageCounter = "Coverage %";

        /// <summary>How much a marker's patch must differ from what lies behind it to be drawn, and how little to be absent (0..1, per channel).</summary>
        private const float PresentDifference = VerificationViews.PresentDifference, AbsentDifference = VerificationViews.AbsentDifference;

        private VerificationViewsRequest viewsRequest;
        private VerificationViewsReport viewsReport;
        private bool viewsReady;
        private EpisodeDirector viewsDirector;
        private HouseCameraRig viewsRig;
        private Camera viewsCamera;
        private HousePlayerController viewsPlayer;
        private HouseRoomScope viewsScope;
        private LiveFeed viewsFeed;
        private bool viewsFeedPaused, viewsHouseHeld, viewsCastAtPlaces, viewsHoldsScope;
        private VerificationViewRoute viewsOpenRoute = VerificationViewRoute.Shot;
        private readonly List<Transform> viewsBodies = new List<Transform>();
        private readonly List<string> viewsBodyIds = new List<string>();
        private readonly List<Vector3> viewsMarks = new List<Vector3>();
        private readonly List<HouseRoomScope.Classified> viewsTable = new List<HouseRoomScope.Classified>(1024);
        private readonly List<HouseRoomScope.LampHome> viewsLamps = new List<HouseRoomScope.LampHome>(32);
        private readonly StringBuilder viewsFrames = new StringBuilder("view,ablation,repeat,rerun,ms\n");
        private readonly List<AtlasLine> atlasLines = new List<AtlasLine>();
        private readonly Plane[] viewsPlanes = new Plane[6];
        private VerificationShadowWork shadowCountInto;
        private bool shadowCountSubscribed;
        private ProfilerRecorder[] viewsRenderRecorders = new ProfilerRecorder[0], viewsDrawerRecorders = new ProfilerRecorder[0];
        private ProfilerRecorder drawerRenderers, drawerExcluded, drawerCoverage, cpuMainRecorder, cpuRenderRecorder, gpuRecorder;
        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private string frameTimingSource = "none";
        private bool viewsProfilerOn;
        private Action viewsLateUpdate;
        private RenderTexture checkTarget, manualTarget;
        private RenderTexture keptTarget;
        private int keptCullingMask;
        private bool checkFramesOn;
        /// <summary>
        /// The whole house, held for the check frames: the checks write the main camera's mask
        /// themselves, and the room scope's own mask write (RS4) stands aside meanwhile, so neither
        /// puts back a bit the other took out.
        /// </summary>
        private readonly object checkFramesHold = new object();
        private readonly List<GameObject> probeMarkers = new List<GameObject>();
        private readonly List<Material> probeMaterials = new List<Material>();

        private struct AtlasLine
        {
            public double At;
            public int Scale, Slices;
        }

        /// <summary>Whether this runner is the views probe: the director's probe-only hold asks (EpisodeDirector.RoomScopeProbe.cs).</summary>
        internal bool IsViewsProbe => viewsRequest != null;

        /// <summary>Reads the views request at install; null when none was made.</summary>
        private void ReadViewsRequest(string[] args) => viewsRequest = VerificationViews.Read(args);

        // ------------------------------------------------------------------------------------
        // The run.
        // ------------------------------------------------------------------------------------

        private IEnumerator RunViews(EpisodeDirector director)
        {
            startupStage = -1;
            viewsDirector = director;
            seasonDirector = director;
            bool graphical = !Application.isBatchMode && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            var state = director.Snapshot;
            viewsReport = new VerificationViewsReport
            {
                pass = viewsRequest.Pass, startedUtc = DateTime.UtcNow.ToString("O"),
                scope = viewsRequest.HoldScope ? "held on" : viewsRequest.WholeHouse ? "whole house" : "as shipped",
                buildKind = Debug.isDebugBuild ? "development" : "release",
                unityVersion = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
                operatingSystem = SystemInfo.operatingSystem, graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphical = graphical, batchMode = Application.isBatchMode, developmentBuild = Debug.isDebugBuild,
                liveWorld = viewsRequest.LiveWorld, quick = viewsRequest.Quick,
                houseSize = state != null ? state.contestants.Count : 0, houseSizeRequested = houseSize,
                seasonSource = profileSeason.ToString().ToLowerInvariant(), seasonSeed = seasonSeed, sessionId = seasonSessionId,
                settleSeconds = viewsRequest.Settle, measureSeconds = viewsRequest.Measure, repeats = viewsRequest.Repeats,
                viewsRequested = VerificationViews.Requested(viewsRequest).Select(view => view.Id).ToArray(),
                skippedViews = VerificationViews.All.Where(view => view.SkippedBecause != null).Select(view => view.Id + ": " + view.SkippedBecause).ToArray(),
            };
            var notes = new List<string>();
            if (viewsRequest.Quick) notes.Add("A quick run: " + viewsRequest.Settle + " s to settle and " + viewsRequest.Measure + " s to measure, once. A smoke test, not a measurement.");
            if (!string.IsNullOrEmpty(houseSizeNote)) notes.Add(houseSizeNote);
            Application.logMessageReceived += CollectAtlasLine;
            try
            {
                yield return ViewsGuard(PrepareViews(graphical, notes));
                if (viewsReady)
                {
                    if (viewsRequest.Pass == VerificationViews.AblationsPass) yield return ViewsGuard(MeasureAblations(notes));
                    else
                    {
                        // P1 and P6 before any view: whether the drawer is live, and whether the rows may carry the engine's counters.
                        if (viewsRequest.Pass == VerificationViews.CountersPass) yield return ViewsGuard(RunCounterChecks(notes));
                        yield return ViewsGuard(MeasureViews(graphical && viewsRequest.Pass == VerificationViews.TimingPass && !viewsRequest.LiveWorld));
                    }
                    if (viewsRequest.Pass == VerificationViews.CountersPass) yield return ViewsGuard(RunProbeChecks(notes));
                }
            }
            finally
            {
                RestoreViews();
                Application.logMessageReceived -= CollectAtlasLine;
            }
            FinishViews(notes);
        }

        /// <summary>
        /// Drives a routine and its nested routines itself, so whatever one throws becomes a report
        /// error rather than a dead coroutine and a player that never quits (the look sheet's guard).
        /// </summary>
        private IEnumerator ViewsGuard(IEnumerator inner)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(inner);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                object current = null;
                bool moved;
                try { moved = top.MoveNext(); if (moved) current = top.Current; }
                catch (Exception error)
                {
                    errors.Add("The views probe stopped: " + error);
                    while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                    yield break;
                }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
        }

        private IEnumerator PrepareViews(bool graphical, List<string> notes)
        {
            viewsRig = FindAnyObjectByType<HouseCameraRig>();
            viewsCamera = viewsRig != null ? viewsRig.ViewCamera : null;
            viewsPlayer = FindAnyObjectByType<HousePlayerController>();
            if (viewsRig == null || viewsCamera == null || viewsPlayer == null)
            { errors.Add("The views probe needs the house's camera rig, its camera and the player."); yield break; }
            string pass = viewsRequest.Pass;
            if (pass != VerificationViews.CountersPass && !graphical)
                errors.Add("The " + pass + " pass times frames: it needs a graphical, windowed player, never -batchmode or -nographics.");
            if (pass != VerificationViews.TimingPass && !Debug.isDebugBuild)
                errors.Add("The " + pass + " pass reads the drawer's counters and the GPU's time: it needs the development build (Editor/RoomScopeProbeBuild.cs).");
            yield return SkipOpening();
            yield return CloseEverything();
            // Uncapped, as the profile is: a capped frame measures the display, not the view.
            viewsDirector.SetFrameCap(ProfileFrameCap);
            if (graphical)
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                for (int i = 0; i < 15; i++) yield return null;
                viewsDirector.SetDisplay(DisplayMode.Windowed, ProfileWidth, ProfileHeight);
                double splash = Time.realtimeSinceStartupAsDouble + 15;
                while (!SplashScreen.isFinished && Time.realtimeSinceStartupAsDouble < splash) yield return null;
                for (int i = 0; i < 30; i++) yield return null;
            }
            viewsReport.resolution = Screen.width + "x" + Screen.height;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            viewsReport.renderPipeline = pipeline != null ? pipeline.name : "none";
            // By name: GPUResidentDrawerMode lives in an assembly Gamesim.Runtime does not reference.
            viewsReport.drawerMode = pipeline != null
                ? typeof(UniversalRenderPipelineAsset).GetProperty("gpuResidentDrawerMode")?.GetValue(pipeline)?.ToString() ?? "unknown" : "none";
            viewsReport.drawerSmallMeshPercent = pipeline != null ? pipeline.smallMeshScreenPercentage : -1f;
            viewsReport.drawerInitialized = DrawerInitialized();
            if (viewsReport.drawerMode != "none" && viewsReport.drawerMode != "Disabled" && viewsReport.drawerInitialized == "no")
                notes.Add("The asset asks for the GPU Resident Drawer but it did not start in this player: every number here is the plain SRP path's.");

            // The views are today's poses in the plan's house: a house whose floors moved compares nothing.
            viewsScope = viewsDirector.RoomScope;
            if (viewsScope == null) { errors.Add("The house's room scope did not build, so its floors are not the eight the views are taken in."); yield break; }
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                var plan = VerificationViews.Room(RoomLayers.RoomIds[i]);
                if (!viewsScope.Has(i)) { errors.Add("The house has no " + plan.Room + " floor."); continue; }
                var box = viewsScope.Box(i);
                double off = Math.Max(Math.Max(Math.Abs(box.xMin - plan.MinX), Math.Abs(box.xMax - plan.MaxX)),
                    Math.Max(Math.Abs(box.yMin - plan.MinZ), Math.Abs(box.yMax - plan.MaxZ)));
                if (off > VerificationViews.HouseTolerance)
                    errors.Add("The " + plan.Room + " floor stands " + off.ToString("0.000", CultureInfo.InvariantCulture) + " m from the plan's: the views would not compare.");
            }

            // Nothing but the probe moves the camera or the player from here.
            viewsRig.ControlsEnabled = false;
            viewsPlayer.SetInputEnabled(false);
            // The ceremony's sets, dressed once for every view and every pass alike: V6g's places,
            // and the house as it stands after the week's first ceremony.
            var scene = viewsDirector.gameObject.scene;
            CeremonySeating.Ensure(scene, viewsReport.houseSize);
            viewsReport.ceremonySetsDressed = true;
            viewsReport.ceremonyPlaces = CeremonySeating.Anchors(scene, CeremonySeating.NominationHead).Count
                + CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat).Count;
            yield return null;
            if (viewsRequest.HoldScope) { viewsDirector.HoldRoomScope(this); viewsHoldsScope = true; }
            else if (viewsRequest.WholeHouse) { viewsDirector.HoldWholeHouse(this); viewsHoldsScope = true; }
            // The drawer's counters sum every camera in the frame: the live feed is paused for them.
            viewsFeed = FindAnyObjectByType<LiveFeed>();
            if (pass == VerificationViews.CountersPass && viewsFeed != null && viewsFeed.enabled) { viewsFeed.enabled = false; viewsFeedPaused = true; }

            GatherBodies();
            CountPrimitiveBodies();
            if (!viewsRequest.LiveWorld)
            {
                viewsDirector.HoldHouseForViewsProbe(this);
                viewsHouseHeld = true;
                viewsReport.houseHeld = true;
                yield return null;
                yield return null;
                PlaceOnMarks(true);
            }
            else notes.Add("The live world: the cast walks where the NPC world takes it; each view reports the rooms its bodies stood in at each run's start.");
            // The classifier's table after the dressing, so the ceremony sets are counted with the house.
            viewsScope.Classify(viewsTable);
            viewsScope.LampHomes(viewsLamps);
            StartFrameTiming();
            if (pass == VerificationViews.CountersPass) StartCounterRecorders();
            RenderPipelineManager.beginCameraRendering += CountShadowWork;
            shadowCountSubscribed = true;
            for (int i = 0; i < 30; i++) yield return null;
            // The counter pass reads P1 from its running recorders; the others take it here, before any view.
            if (pass != VerificationViews.CountersPass) yield return CheckDrawerActiveBriefly();
            viewsReady = true;
        }

        /// <summary>
        /// Every probe measures the UMA cast (§5.1): a copy without UMA, or a cast fallen back to the
        /// primitive rig, measures another house, so any primitive body is an error, and the report
        /// names the provider that built the cast.
        /// </summary>
        private void CountPrimitiveBodies()
        {
            var provider = CharacterBodySource.Provider;
            viewsReport.bodyProvider = provider != null ? provider.GetType().Name : "none";
            int primitive = 0;
            foreach (var body in viewsBodies)
            {
                var presentation = body != null ? body.GetComponent<CharacterPresentation>() : null;
                if (presentation != null && presentation.IsPrimitiveBody) primitive++;
            }
            viewsReport.primitiveBodies = primitive;
            if (primitive > 0)
                errors.Add(primitive + " of " + viewsBodies.Count + " bodies stand on the primitive rig (body provider " + viewsReport.bodyProvider
                    + "): the probe measures the UMA cast, so run it on a copy with UMA installed and GAMESIM_UMA defined.");
        }

        /// <summary>Whether the drawer started in this player: core's internal GPUResidentDrawer.IsInitialized, by name (Gamesim.Runtime does not reference its assembly).</summary>
        private static string DrawerInitialized()
        {
            try
            {
                var drawer = Type.GetType("UnityEngine.Rendering.GPUResidentDrawer, Unity.RenderPipelines.GPUDriven.Runtime");
                var initialized = drawer?.GetMethod("IsInitialized", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (initialized == null || initialized.ReturnType != typeof(bool)) return "unknown";
                return (bool)initialized.Invoke(null, null) ? "yes" : "no";
            }
            catch (Exception error) when (error is TargetInvocationException || error is MemberAccessException || error is TypeLoadException)
            { return "unknown"; }
        }

        /// <summary>The bodies in mark order: the player, then the houseguests in the season's order.</summary>
        private void GatherBodies()
        {
            viewsBodies.Clear();
            viewsBodyIds.Clear();
            var state = viewsDirector.Snapshot;
            viewsBodies.Add(viewsPlayer.transform);
            viewsBodyIds.Add(state != null && state.playerId != null ? state.playerId : "player");
            var npcs = FindObjectsByType<HouseNpc>().Where(npc => npc.isActiveAndEnabled && npc.Id != null).ToList();
            if (state != null)
                foreach (var contestant in state.contestants)
                {
                    if (contestant == null || contestant.isPlayer) continue;
                    var npc = npcs.FirstOrDefault(candidate => candidate.Id == contestant.id);
                    if (npc == null) continue;
                    viewsBodies.Add(npc.transform);
                    viewsBodyIds.Add(npc.Id);
                    npcs.Remove(npc);
                }
            foreach (var npc in npcs.OrderBy(npc => npc.Id, StringComparer.Ordinal)) { viewsBodies.Add(npc.transform); viewsBodyIds.Add(npc.Id); }
            if (viewsBodies.Count > VerificationViews.MostBodies)
            {
                errors.Add("The house has " + viewsBodies.Count + " bodies; the marks hold " + VerificationViews.MostBodies + ".");
                viewsBodies.RemoveRange(VerificationViews.MostBodies, viewsBodies.Count - VerificationViews.MostBodies);
                viewsBodyIds.RemoveRange(VerificationViews.MostBodies, viewsBodyIds.Count - VerificationViews.MostBodies);
            }
        }

        /// <summary>Every body onto its mark (the nearest walkable point to it), the pair in each room facing each other.</summary>
        private void PlaceOnMarks(bool record)
        {
            var marks = VerificationViews.Marks(viewsBodies.Count);
            viewsMarks.Clear();
            for (int i = 0; i < marks.Count; i++)
            {
                var mark = marks[i];
                var body = viewsBodies[i];
                bool placed = PlaceBody(body, new Vector3((float)mark.X, 0f, (float)mark.Z), 1.5f);
                if (!placed) errors.Add("Body " + viewsBodyIds[i] + " could not be put on its " + mark.Room + " mark.");
                viewsMarks.Add(body != null ? body.position : Vector3.zero);
                if (!record || body == null) continue;
                var at = body.position;
                viewsReport.marks.Add(new VerificationBodyMark
                {
                    id = viewsBodyIds[i], room = mark.Room, markX = mark.X, markZ = mark.Z,
                    placedX = at.x, placedY = at.y, placedZ = at.z, placed = placed,
                    walkerRoom = viewsDirector.WalkerRoomOf(body),
                });
            }
            for (int i = 0; i < marks.Count; i++)
            {
                int partner = marks[i].Partner;
                if (partner < 0 || partner >= viewsBodies.Count || viewsBodies[i] == null || viewsBodies[partner] == null) continue;
                var toward = viewsBodies[partner].position - viewsBodies[i].position;
                toward.y = 0f;
                if (toward.sqrMagnitude > .01f) viewsBodies[i].rotation = Quaternion.LookRotation(toward);
            }
            Physics.SyncTransforms();
        }

        /// <summary>The cast at the nomination table: the two heads standing, the rest at the chairs' approaches (V6g).</summary>
        private int PlaceAtCeremonyPlaces()
        {
            var scene = viewsDirector.gameObject.scene;
            var places = CeremonySeating.Anchors(scene, CeremonySeating.NominationHead)
                .Concat(CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat)).ToList();
            int placed = 0;
            for (int i = 0; i < viewsBodies.Count && i < places.Count; i++)
            {
                if (!PlaceBody(viewsBodies[i], places[i].Approach, 1f)) continue;
                viewsBodies[i].rotation = Quaternion.Euler(0f, places[i].Facing, 0f);
                placed++;
            }
            Physics.SyncTransforms();
            viewsCastAtPlaces = true;
            return placed;
        }

        /// <summary>Puts a body at the walkable point nearest <paramref name="at"/>: by its agent's warp, or by its transform.</summary>
        private static bool PlaceBody(Transform body, Vector3 at, float reach)
        {
            if (body == null) return false;
            var agent = body.GetComponent<NavMeshAgent>();
            int areas = agent != null ? agent.areaMask : NavMesh.AllAreas;
            if (!NavMesh.SamplePosition(at, out var hit, reach, areas)) return false;
            if (agent != null && agent.enabled && agent.gameObject.activeInHierarchy)
            {
                if (!agent.Warp(hit.position)) return false;
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            else body.position = hit.position;
            return true;
        }

        // ------------------------------------------------------------------------------------
        // The views.
        // ------------------------------------------------------------------------------------

        private static HouseCameraRig.Shot ShotOf(VerificationView view) => new HouseCameraRig.Shot
        {
            Focus = new Vector3((float)view.FocusX, (float)view.FocusY, (float)view.FocusZ),
            Distance = (float)view.Distance, Pitch = (float)view.Pitch, Yaw = (float)view.Yaw,
            FieldOfView = (float)view.FieldOfView, DepthOfFieldWeight = (float)view.DepthOfField,
            Orthographic = view.OrthographicHalfHeight > 0, OrthographicSize = (float)view.OrthographicHalfHeight,
            // The rooms the view is about, so a held scope (or a later slice's live source) draws them.
            Rooms = view.Room != null ? RoomSet.Of(view.Room) : RoomSet.None,
        };

        /// <summary>Takes a view: its shot, the overview, or the diary; and the cast where the view wants it.</summary>
        private IEnumerator ApplyView(VerificationView view, VerificationViewRow row)
        {
            yield return LeaveRoute();
            if (!viewsRequest.LiveWorld && viewsHouseHeld)
            {
                if (view.CeremonyPlaces && !viewsCastAtPlaces)
                {
                    int placed = PlaceAtCeremonyPlaces();
                    if (placed < viewsBodies.Count)
                        row.note = Append(row.note, placed + " of " + viewsBodies.Count + " bodies reached the table's places; the rest stayed on their marks.");
                }
                else if (!view.CeremonyPlaces && viewsCastAtPlaces) { PlaceOnMarks(false); viewsCastAtPlaces = false; }
            }
            switch (view.Route)
            {
                case VerificationViewRoute.Overview:
                    // The overview keeps the rig's yaw: today's, first.
                    viewsRig.MoveTo(ShotOf(VerificationViews.All[0]));
                    yield return null;
                    if (viewsDirector.ShowOverview()) { viewsOpenRoute = VerificationViewRoute.Overview; row.route = "overview"; }
                    else
                    {
                        row.route = "shot";
                        row.note = Append(row.note, "The overview did not open; its pose was taken as a shot.");
                        viewsRig.MoveTo(ShotOf(view));
                    }
                    break;
                case VerificationViewRoute.Diary:
                    yield return OpenDiaryView(view, row);
                    break;
                default:
                    viewsRig.MoveTo(ShotOf(view));
                    row.route = "shot";
                    break;
            }
        }

        /// <summary>The diary interview as the game frames it: the player in the chair, the studio built, the shot settled.</summary>
        private IEnumerator OpenDiaryView(VerificationView view, VerificationViewRow row)
        {
            var approach = viewsDirector.DiaryPosition;
            bool opened = false, settled = false;
            if (!float.IsInfinity(approach.x) && PlaceBody(viewsPlayer.transform, approach, 1f))
            {
                for (int i = 0; i < 3; i++) yield return null;
                opened = viewsDirector.TryOpenDiary();
                if (opened)
                {
                    viewsOpenRoute = VerificationViewRoute.Diary;
                    double deadline = Time.realtimeSinceStartupAsDouble + 10;
                    while (Time.realtimeSinceStartupAsDouble < deadline && !(viewsDirector.IsDiarySettled && viewsRig.HasShot)) yield return null;
                    settled = viewsDirector.IsDiarySettled && viewsRig.HasShot;
                }
            }
            if (settled) { row.route = "diary"; yield break; }
            yield return LeaveRoute();
            row.route = "shot";
            row.note = Append(row.note, opened ? "The diary opened but never settled in its chair; its pose was taken as a shot, without the studio."
                : "The diary did not open; its pose was taken as a shot, without the studio.");
            viewsRig.MoveTo(ShotOf(view));
        }

        /// <summary>Leaves whatever the last view opened, and gives the probe back the camera and the player.</summary>
        private IEnumerator LeaveRoute()
        {
            var open = viewsOpenRoute;
            viewsOpenRoute = VerificationViewRoute.Shot;
            if (open == VerificationViewRoute.Overview && viewsDirector.IsOverview) viewsDirector.EndOverview();
            if (open != VerificationViewRoute.Diary) yield break;
            viewsDirector.ClosePanels();
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (viewsDirector.IsDiaryOpen && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            viewsRig.ControlsEnabled = false;
            viewsPlayer.SetInputEnabled(false);
            if (viewsHouseHeld && viewsMarks.Count > 0 && !viewsCastAtPlaces) PlaceBody(viewsPlayer.transform, viewsMarks[0], .5f);
        }

        /// <summary>Waits the settle time on the real clock, and for the rig to land if it has not.</summary>
        private IEnumerator Settle(double seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
            double late = Time.realtimeSinceStartupAsDouble + 5;
            while ((viewsRig.IsTravelling || !viewsRig.HasArrived(.1f)) && Time.realtimeSinceStartupAsDouble < late) yield return null;
        }

        private VerificationViewRow RowFor(VerificationView view, string ablation)
            => new VerificationViewRow { id = view.Id, label = view.Label, room = view.Room ?? "the whole house", ablation = ablation };

        /// <summary>The timing and counter passes: every requested view, round after round, then one more run of any view that did not repeat.</summary>
        private IEnumerator MeasureViews(bool capture)
        {
            var requested = VerificationViews.Requested(viewsRequest);
            var rows = requested.Select(view => RowFor(view, null)).ToList();
            viewsReport.views.AddRange(rows);
            for (int repeat = 1; repeat <= viewsRequest.Repeats; repeat++)
                for (int i = 0; i < requested.Count; i++)
                {
                    yield return MeasureOnce(requested[i], rows[i], null, repeat, false);
                    if (capture && repeat == 1) yield return CaptureView(requested[i], rows[i]);
                }
            for (int i = 0; i < requested.Count; i++)
            {
                VerificationViews.CompleteRow(rows[i]);
                if (!VerificationViews.NeedsRerun(rows[i])) continue;
                yield return MeasureOnce(requested[i], rows[i], null, viewsRequest.Repeats + 1, true);
                VerificationViews.CompleteRow(rows[i]);
            }
            yield return LeaveRoute();
        }

        /// <summary>One run of one view: take it, settle, measure; with an ablation switched on between the settle and the restore.</summary>
        private IEnumerator MeasureOnce(VerificationView view, VerificationViewRow row, string ablation, int repeat, bool rerun)
        {
            double windowStart = Time.realtimeSinceStartupAsDouble;
            yield return ApplyView(view, row);
            Action restore = null;
            try
            {
                if (ablation != null && ablation != VerificationViews.Baseline)
                {
                    restore = ApplyAblation(ablation, view, out string note);
                    if (repeat == 1) row.note = Append(row.note, note);
                    if (ablation == VerificationViews.SmallMeshCulling && repeat == 1) yield return ProveSmallMeshCulling(row);
                    if (ablation == VerificationViews.Confinement && repeat == 1) yield return ProveConfinement(row, view);
                }
                yield return Settle(viewsRequest.Settle);
                yield return Measure(view, row, ablation, repeat, rerun);
            }
            finally { restore?.Invoke(); }
            if (row.shadows != null && row.shadows.atlasLine == "not checked") JudgeAtlasLines(row.shadows, windowStart);
        }

        private IEnumerator Measure(VerificationView view, VerificationViewRow row, string ablation, int repeat, bool rerun)
        {
            var run = new VerificationViewRun { repeat = repeat, rerun = rerun, startedSeconds = Time.realtimeSinceStartupAsDouble };
            if (viewsRequest.LiveWorld)
            {
                CountBodiesByRoom(row);
                if (!viewsDirector.ViewsProbeNpcWorldAdvancing)
                    row.note = Append(row.note, "Run " + repeat + ": the NPC world was not advancing as it began (phase "
                        + (viewsDirector.Snapshot != null ? viewsDirector.Snapshot.phase.ToString() : "none") + ").");
            }
            else if (row.bodiesByRoom.Count == 0) CountBodiesByRoom(row);
            // The shadow work once a row, counted in the main camera's next begin callback.
            if (row.shadows == null) { row.shadows = new VerificationShadowWork(); shadowCountInto = row.shadows; }
            bool counting = viewsRenderRecorders.Length > 0;
            var frames = new List<float>(4096);
            var main = new List<double>(4096);
            var render = new List<double>(4096);
            var gpu = new List<double>(4096);
            var counters = counting ? Enumerable.Range(0, RenderCounterNames.Length + DrawerCounterNames.Length).Select(_ => new List<long>(4096)).ToArray() : null;
            // Two frames before the window: the first carries the room count above and the shadow
            // count in its render, so the first measured delta is a frame with neither.
            yield return null;
            yield return null;
            double started = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - started < viewsRequest.Measure)
            {
                // Only the floats inside the window: the CSV is written after it, so no string is made while frames are timed.
                frames.Add(Time.unscaledDeltaTime * 1000f);
                SampleFrameTiming(main, render, gpu);
                if (counting)
                {
                    for (int i = 0; i < viewsRenderRecorders.Length; i++) AddCounter(counters[i], viewsRenderRecorders[i]);
                    for (int i = 0; i < viewsDrawerRecorders.Length; i++) AddCounter(counters[RenderCounterNames.Length + i], viewsDrawerRecorders[i]);
                }
                yield return null;
            }
            string prefix = view.Id + "," + (ablation ?? "") + "," + repeat.ToString(CultureInfo.InvariantCulture) + "," + (rerun ? "1" : "0") + ",";
            foreach (float ms in frames) viewsFrames.Append(prefix).Append(ms.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            run.frames = VerificationFrameSummary.Of(view.Id, frames);
            run.cpuMainMedianMs = VerificationViews.MedianOfKnown(main);
            run.cpuRenderMedianMs = VerificationViews.MedianOfKnown(render);
            run.gpuMedianMs = VerificationViews.MedianOfKnown(gpu);
            row.runs.Add(run);
            row.viewedRooms = viewsDirector.ViewedRooms.ToString();
            if (counting && row.counters == null)
            {
                long Median(int i) => (long)VerificationViews.MedianOfKnown(counters[i].Select(value => (double)value));
                // The engine's counters only where P6 found they include the drawer's draws (§5.3): otherwise they would compare nothing.
                bool engine = viewsReport.renderCountersIncludeDrawer == VerificationViews.CountersInclude;
                long Engine(int i) => engine ? Median(i) : -1;
                row.counters = new VerificationViewCounters
                {
                    batches = Engine(0), setPassCalls = Engine(1), drawCalls = Engine(2), triangles = Engine(3), vertices = Engine(4),
                    shadowCasters = Engine(5), visibleSkinnedMeshes = Engine(6),
                    drawerTotalInstances = Median(7), drawerVisibleInstances = Median(8), drawerLayerCulled = Median(9),
                    drawerFrustumCulled = Median(10), drawerSmallMeshCulled = Median(11),
                };
            }
            VerificationViews.CompleteRow(row);
        }

        private static void AddCounter(List<long> into, ProfilerRecorder recorder)
        {
            if (recorder.Valid && recorder.Count > 0) into.Add(recorder.LastValue);
        }

        /// <summary>Where the bodies stand, by the floors: the marks' rooms once, or the live world's at every run's start (summed).</summary>
        private void CountBodiesByRoom(VerificationViewRow row)
        {
            foreach (var body in viewsBodies)
            {
                if (body == null) continue;
                string room = viewsDirector.WalkerRoomOf(body) ?? "no room";
                var count = row.bodiesByRoom.FirstOrDefault(entry => entry.room == room);
                if (count == null) row.bodiesByRoom.Add(count = new VerificationRoomCount { room = room });
                count.bodies++;
            }
        }

        /// <summary>The owner's frame of each view (graphical runs, first round): and D4's rooms again at the scene's yaw.</summary>
        private IEnumerator CaptureView(VerificationView view, VerificationViewRow row)
        {
            string stem = Path.Combine(outputDirectory, VerificationViews.ReportName(viewsRequest, viewsReport.houseSize) + "-" + view.Id);
            yield return CaptureInto(stem + ".png", view.Yaw, row);
            if (!view.CaptureAtYaw45) yield break;
            viewsRig.MoveTo(ShotOf(view.AtYaw(VerificationViews.SecondCaptureYaw)));
            yield return Settle(Math.Min(viewsRequest.Settle, 3));
            yield return CaptureInto(stem + "-yaw45.png", VerificationViews.SecondCaptureYaw, row);
        }

        private IEnumerator CaptureInto(string path, double yaw, VerificationViewRow row)
        {
            VerificationFrameEvidence evidence = null;
            yield return CaptureVerifiedFrame(path, frame => evidence = frame, errors.Add);
            row.captures.Add(new VerificationViewCapture
            {
                path = path, yaw = yaw, rendered = evidence != null && evidence.rendered, failure = evidence != null ? evidence.failure : "not captured",
            });
        }

        // ------------------------------------------------------------------------------------
        // Frame times and counters.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// CPU main and render thread and GPU time: from FrameTimingManager where the build has it on
        /// (a development build always; a release build only with the Frame Timing Stats player
        /// setting), otherwise from the profiler's frame-time counters, otherwise none - and the report says which.
        /// </summary>
        private void StartFrameTiming()
        {
            if (FrameTimingManager.IsFeatureEnabled()) frameTimingSource = "FrameTimingManager";
            else
            {
                cpuMainRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", 1);
                cpuRenderRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Render Thread Frame Time", 1);
                gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time", 1);
                frameTimingSource = cpuMainRecorder.Valid ? "ProfilerRecorder" : "none";
            }
            viewsReport.frameTimingSource = frameTimingSource;
        }

        private void SampleFrameTiming(List<double> main, List<double> render, List<double> gpu)
        {
            if (frameTimingSource == "FrameTimingManager")
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, frameTimings) == 0) return;
                var timing = frameTimings[0];
                if (timing.cpuMainThreadFrameTime > 0) main.Add(timing.cpuMainThreadFrameTime);
                if (timing.cpuRenderThreadFrameTime > 0) render.Add(timing.cpuRenderThreadFrameTime);
                if (timing.gpuFrameTime > 0) gpu.Add(timing.gpuFrameTime);
                return;
            }
            if (frameTimingSource != "ProfilerRecorder") return;
            AddMilliseconds(main, cpuMainRecorder);
            AddMilliseconds(render, cpuRenderRecorder);
            AddMilliseconds(gpu, gpuRecorder);
        }

        private static void AddMilliseconds(List<double> into, ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0 || recorder.LastValue <= 0) return;
            into.Add(recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds ? recorder.LastValue * 1e-6 : recorder.LastValue);
        }

        /// <summary>The counter pass's recorders: the engine's render counters, and the drawer's with the profiler and its category on.</summary>
        private void StartCounterRecorders()
        {
            viewsRenderRecorders = RenderCounterNames.Select(name => ProfilerRecorder.StartNew(ProfilerCategory.Render, name, 1)).ToArray();
            if (Profiler.supported)
            {
                Profiler.enabled = true;
                viewsProfilerOn = true;
            }
            var drawer = new ProfilerCategory(DrawerCategoryName);
            Profiler.SetCategoryEnabled(drawer, true);
            viewsDrawerRecorders = DrawerCounterNames.Select(name => ProfilerRecorder.StartNew(drawer, name, 1)).ToArray();
            drawerRenderers = ProfilerRecorder.StartNew(drawer, DrawerRenderersCounter, 1);
            drawerExcluded = ProfilerRecorder.StartNew(drawer, DrawerExcludedCounter, 1);
            drawerCoverage = ProfilerRecorder.StartNew(drawer, DrawerCoverageCounter, 1);
        }

        private void StopCounterRecorders()
        {
            foreach (var recorder in viewsRenderRecorders) recorder.Dispose();
            foreach (var recorder in viewsDrawerRecorders) recorder.Dispose();
            viewsRenderRecorders = new ProfilerRecorder[0];
            viewsDrawerRecorders = new ProfilerRecorder[0];
            drawerRenderers.Dispose(); drawerExcluded.Dispose(); drawerCoverage.Dispose();
            cpuMainRecorder.Dispose(); cpuRenderRecorder.Dispose(); gpuRecorder.Dispose();
            if (viewsProfilerOn) { Profiler.enabled = false; viewsProfilerOn = false; }
        }

        // ------------------------------------------------------------------------------------
        // The shadow work: inside the main camera's begin callback, after any scope hook (handlers
        // run in the order they subscribed, and the probe subscribes after the director has).
        // ------------------------------------------------------------------------------------

        private void CountShadowWork(ScriptableRenderContext context, Camera camera)
        {
            if (shadowCountInto == null || camera != viewsCamera) return;
            var work = shadowCountInto;
            shadowCountInto = null;
            try { FillShadowWork(work, camera); }
            catch (Exception error) { errors.Add("The shadow count failed: " + error.Message); }
        }

        private void FillShadowWork(VerificationShadowWork work, Camera camera)
        {
            var frustum = FrustumOf(camera);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var visible = new List<VerificationShadowLight>();
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light == null || !light.enabled || light.type == LightType.Directional) continue;
                if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                if (light.shadows == LightShadows.None || light.shadowStrength <= 0f) continue;
                var shadowed = ShadowLightOf(light, pipeline);
                if (!frustum.SphereVisible(shadowed.X, shadowed.Y, shadowed.Z, shadowed.Range)) continue;
                visible.Add(shadowed);
                work.lightsInFrustum++;
                if (light.type == LightType.Spot) work.spotsInFrustum++;
                if (HouseRoomScope.IsLamp(light)) work.lampsInFrustum++;
            }
            var eye = camera.transform.position;
            work.atlas = VerificationViews.LayoutShadowAtlas(visible, eye.x, eye.y, eye.z,
                pipeline != null ? pipeline.additionalLightsShadowmapResolution : 2048);
            // The classifier's casters in each lamp's reach that this camera would draw.
            foreach (var lamp in viewsLamps)
            {
                var light = lamp.Light;
                if (light == null) continue;
                var at = light.transform.position;
                bool enabled = light.isActiveAndEnabled && light.shadows != LightShadows.None;
                var row = new VerificationLampWork
                {
                    name = light.name, room = HouseRoomScope.RoomId(lamp.Room) ?? "none", x = at.x, y = at.y, z = at.z, range = light.range,
                    enabled = enabled, inFrustum = enabled && frustum.SphereVisible(at.x, at.y, at.z, light.range),
                };
                if (row.inFrustum) CountCasters(camera, light, out row.houseCasters, out row.bodyCasters);
                work.houseCasters += row.houseCasters;
                work.bodyCasters += row.bodyCasters;
                work.lamps.Add(row);
            }
        }

        /// <summary>
        /// The classifier's renderers a lamp would draw into its shadow maps: in its reach, casting, on
        /// a layer the camera draws, and sharing a rendering layer with the lamp (Unity culls a light's
        /// casters by the light's renderingLayerMask, which confinement narrows to its room's bit).
        /// </summary>
        private void CountCasters(Camera camera, Light light, out int house, out int bodies)
        {
            house = bodies = 0;
            var at = light.transform.position;
            float range = light.range;
            uint lightLayers = unchecked((uint)light.renderingLayerMask);
            var reach = new Bounds(at, Vector3.one * (range * 2f));
            foreach (var entry in viewsTable)
            {
                var renderer = entry.Renderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
                if (renderer.shadowCastingMode == ShadowCastingMode.Off || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                if ((renderer.renderingLayerMask & lightLayers) == 0) continue;
                var bounds = renderer.bounds;
                if (!bounds.Intersects(reach) || bounds.SqrDistance(at) > range * range) continue;
                if (entry.Placement == HouseRoomScope.Placement.Body) bodies++;
                else house++;
            }
        }

        /// <summary>A camera's frustum as its projection has it (the overview's blended lens included).</summary>
        private VerificationFrustum FrustumOf(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera.projectionMatrix * camera.worldToCameraMatrix, viewsPlanes);
            var normals = new double[6][];
            var offsets = new double[6];
            for (int i = 0; i < 6; i++)
            {
                var normal = viewsPlanes[i].normal;
                normals[i] = new double[] { normal.x, normal.y, normal.z };
                offsets[i] = viewsPlanes[i].distance;
            }
            var eye = camera.transform.position;
            return new VerificationFrustum(eye.x, eye.y, eye.z, normals, offsets);
        }

        /// <summary>
        /// A light as the shadow model reads it: its tier's resolution as URP 17.6 picks it
        /// (UniversalRenderPipeline's shadow data: a custom tier is the light's own resolution, a tier
        /// the asset's, no additional data the default High tier). Reads; adds no component.
        /// </summary>
        public static VerificationShadowLight ShadowLightOf(Light light)
            => ShadowLightOf(light, GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);

        /// <summary>A light as the shadow model reads it, against <paramref name="pipeline"/>'s tiers.</summary>
        private static VerificationShadowLight ShadowLightOf(Light light, UniversalRenderPipelineAsset pipeline)
        {
            if (light == null) throw new ArgumentNullException(nameof(light));
            int resolution;
            bool hasData = light.TryGetComponent<UniversalAdditionalLightData>(out var data);
            int tier = hasData ? data.additionalLightsShadowResolutionTier : UniversalAdditionalLightData.AdditionalLightsShadowDefaultResolutionTier;
            if (hasData && tier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom) resolution = (int)light.shadowResolution;
            else
            {
                tier = Mathf.Clamp(tier, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh);
                resolution = pipeline == null ? 1024
                    : tier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow ? pipeline.additionalLightsShadowResolutionTierLow
                    : tier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium ? pipeline.additionalLightsShadowResolutionTierMedium
                    : pipeline.additionalLightsShadowResolutionTierHigh;
            }
            var at = light.transform.position;
            return new VerificationShadowLight(light.name, at.x, at.y, at.z, light.range, light.type == LightType.Spot,
                light.shadows == LightShadows.Soft, resolution);
        }

        /// <summary>URP's atlas line, whenever it comes: the cross-check of the probe's count (P7).</summary>
        private void CollectAtlasLine(string text, string stack, LogType type)
        {
            if (!VerificationViews.TryParseAtlasMessage(text, out int scale, out int slices, out _)) return;
            atlasLines.Add(new AtlasLine { At = Time.realtimeSinceStartupAsDouble, Scale = scale, Slices = slices });
        }

        /// <summary>
        /// What URP logged while a view was taken and measured, against the probe's count for it. URP
        /// logs a line for each new configuration as the camera travels in; the last one is the view's
        /// own, since nothing changes the shadow requests while a view is held.
        /// </summary>
        private void JudgeAtlasLines(VerificationShadowWork work, double since)
        {
            var lines = atlasLines.Where(line => line.At >= since).ToList();
            if (lines.Count == 0 || work.atlas == null) { work.atlasLine = "not logged"; return; }
            var last = lines[lines.Count - 1];
            work.loggedSlices = last.Slices;
            work.loggedScale = last.Scale;
            work.atlasLine = last.Slices == work.atlas.slicesKept && last.Scale == work.atlas.scale ? "agrees" : "disagrees";
        }

        // ------------------------------------------------------------------------------------
        // The ablations (development build, graphical, at 16): one thing off at a time, put back.
        // ------------------------------------------------------------------------------------

        private IEnumerator MeasureAblations(List<string> notes)
        {
            var plan = VerificationViews.AblationPlan(viewsRequest);
            var rows = plan.Select(step => RowFor(VerificationViews.Find(step.View), step.Ablation)).ToList();
            viewsReport.ablations.AddRange(rows);
            for (int repeat = 1; repeat <= viewsRequest.Repeats; repeat++)
                for (int i = 0; i < plan.Count; i++)
                    yield return MeasureOnce(VerificationViews.Find(plan[i].View), rows[i], plan[i].Ablation, repeat, false);
            for (int i = 0; i < plan.Count; i++)
            {
                VerificationViews.CompleteRow(rows[i]);
                if (!VerificationViews.NeedsRerun(rows[i])) continue;
                yield return MeasureOnce(VerificationViews.Find(plan[i].View), rows[i], plan[i].Ablation, viewsRequest.Repeats + 1, true);
                VerificationViews.CompleteRow(rows[i]);
            }
            yield return LeaveRoute();
            notes.Add("Ablations: each row is its view with one thing switched off for the measurement and put back after; compare a row with its view's baseline.");
        }

        /// <summary>Switches one thing off for the probe and returns what puts it back exactly.</summary>
        private Action ApplyAblation(string ablation, VerificationView view, out string note)
        {
            switch (ablation)
            {
                case VerificationViews.LampShadowsOff:
                {
                    var lamps = FindObjectsByType<Light>(FindObjectsInactive.Include).Where(HouseRoomScope.IsLamp)
                        .Select(light => (light, light.shadows)).ToList();
                    foreach (var (light, _) in lamps) light.shadows = LightShadows.None;
                    note = lamps.Count + " lamps unshadowed.";
                    return () => { foreach (var (light, shadows) in lamps) if (light != null) light.shadows = shadows; };
                }
                case VerificationViews.BodiesOff:
                {
                    var drawn = viewsBodies.Where(body => body != null).SelectMany(body => body.GetComponentsInChildren<Renderer>(true))
                        .Select(renderer => (renderer, renderer.forceRenderingOff)).ToList();
                    foreach (var (renderer, _) in drawn) renderer.forceRenderingOff = true;
                    note = drawn.Count + " body renderers forced off (they still animate).";
                    return () => { foreach (var (renderer, off) in drawn) if (renderer != null) renderer.forceRenderingOff = off; };
                }
                case VerificationViews.SsaoOff:
                {
                    var switched = ActiveFeatures<ScreenSpaceAmbientOcclusion>();
                    foreach (var feature in switched) feature.SetActive(false);
                    note = switched.Count + " SSAO renderer feature(s) off.";
                    return () => { foreach (var feature in switched) if (feature != null) feature.SetActive(true); };
                }
                case VerificationViews.MoonShadowsOff:
                {
                    var moons = FindObjectsByType<Light>(FindObjectsInactive.Exclude)
                        .Where(light => light.type == LightType.Directional && light.shadows != LightShadows.None)
                        .Select(light => (light, light.shadows)).ToList();
                    foreach (var (light, _) in moons) light.shadows = LightShadows.None;
                    note = moons.Count + " directional light(s) unshadowed.";
                    return () => { foreach (var (light, shadows) in moons) if (light != null) light.shadows = shadows; };
                }
                case VerificationViews.Confinement:
                    return Confine(view, out note);
                case VerificationViews.SmallMeshCulling:
                {
                    var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    if (pipeline == null) { note = "No URP asset: small-mesh culling not applied."; return null; }
                    float kept = pipeline.smallMeshScreenPercentage;
                    pipeline.smallMeshScreenPercentage = VerificationViews.SmallMeshAblationPercent;
                    note = "The drawer's small-mesh threshold set to " + VerificationViews.SmallMeshAblationPercent.ToString(CultureInfo.InvariantCulture)
                        + "% (shipped " + kept.ToString(CultureInfo.InvariantCulture) + "%).";
                    return () => { if (pipeline != null) pipeline.smallMeshScreenPercentage = kept; };
                }
                default:
                    note = "Unknown ablation " + ablation + ": nothing switched.";
                    return null;
            }
        }

        /// <summary>The pipeline's renderer features of a kind that are on, from every renderer the asset lists.</summary>
        private static List<ScriptableRendererFeature> ActiveFeatures<T>() where T : ScriptableRendererFeature
        {
            var found = new List<ScriptableRendererFeature>();
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)) return found;
            foreach (var data in pipeline.rendererDataList)
            {
                if (data == null) continue;
                foreach (var feature in data.rendererFeatures)
                    if (feature is T && feature.isActive) found.Add(feature);
            }
            return found;
        }

        /// <summary>
        /// Rendering-layer confinement (D8 ii), applied only inside the probe through RS1's classifier
        /// table: each room's renderers and lamps on that room's rendering layer, the shell and every
        /// other light on all eight, each body on its walker room's; the lamps homed outside the view's
        /// room lit but unshadowed. A renderer the table does not hold (one made since it was read, or
        /// outside the house's scene) joins all eight, since the Living's bit is every renderer's
        /// Default. Everything it writes is put back, the lights' own masks included.
        /// </summary>
        private Action Confine(VerificationView view, out string note)
        {
            int scopeRoom = view.Room != null ? RoomLayers.IndexOf(view.Room) : -1;
            var renderers = new List<(Renderer renderer, uint mask)>();
            var tabled = new HashSet<Renderer>();
            foreach (var entry in viewsTable)
            {
                var renderer = entry.Renderer;
                if (renderer == null) continue;
                tabled.Add(renderer);
                uint bits = VerificationViews.AllConfinementBits;
                if (entry.Placement == HouseRoomScope.Placement.Room) bits = VerificationViews.ConfinementBit(entry.Room);
                else if (entry.Placement == HouseRoomScope.Placement.Body)
                {
                    var npc = renderer.GetComponentInParent<HouseNpc>(true);
                    var root = npc != null ? npc.transform : viewsPlayer.transform;
                    bits = VerificationViews.ConfinementBit(RoomLayers.IndexOf(viewsDirector.WalkerRoomOf(root)));
                }
                renderers.Add((renderer, renderer.renderingLayerMask));
                renderer.renderingLayerMask = bits;
            }
            int untabled = 0;
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (renderer == null || tabled.Contains(renderer)) continue;
                renderers.Add((renderer, renderer.renderingLayerMask));
                renderer.renderingLayerMask |= VerificationViews.AllConfinementBits;
                untabled++;
            }
            var homes = viewsLamps.Where(lamp => lamp.Light != null).ToDictionary(lamp => lamp.Light, lamp => lamp.Room);
            var lights = new List<(UniversalAdditionalLightData data, uint layers, int mask)>();
            var shadows = new List<(Light light, LightShadows shadows)>();
            int confined = 0, unshadowed = 0;
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                // Read before the data is fetched: fetching adds the component where a light has none.
                int mask = light.renderingLayerMask;
                var data = light.GetUniversalAdditionalLightData();
                lights.Add((data, data.renderingLayers.value, mask));
                if (!homes.TryGetValue(light, out int home)) { data.renderingLayers = data.renderingLayers.value | VerificationViews.AllConfinementBits; continue; }
                data.renderingLayers = VerificationViews.ConfinementBit(home);
                confined++;
                if (scopeRoom < 0 || home == scopeRoom || light.shadows == LightShadows.None) continue;
                shadows.Add((light, light.shadows));
                light.shadows = LightShadows.None;
                unshadowed++;
            }
            note = "Confined " + (renderers.Count - untabled) + " renderers and " + confined + " lamps to their rooms' rendering layers ("
                + untabled + " renderers outside the classifier's table on all eight); "
                + unshadowed + " lamps outside " + (view.Room ?? "the whole house") + " lit but unshadowed.";
            return () =>
            {
                foreach (var (renderer, mask) in renderers) if (renderer != null) renderer.renderingLayerMask = mask;
                foreach (var (data, layers, mask) in lights)
                {
                    if (data == null) continue;
                    data.renderingLayers = layers;
                    // The data's setter writes the light's mask from its own; the light's is put back as it was read.
                    var light = data.GetComponent<Light>();
                    if (light != null) light.renderingLayerMask = mask;
                }
                foreach (var (light, kept) in shadows) if (light != null) light.shadows = kept;
            };
        }

        /// <summary>
        /// Whether the drawer applies a rendering-layer write made at run time, which the confinement
        /// row rests on: a lit marker beside one of the view's lamps (receiving no shadow), made once on
        /// a room's layer no lamp in reach carries and once on the lamp's own, so the lamp's light on it
        /// is known; then moved from the lamp's layer to the other at run time, it should lose that light
        /// on its next render. Read through the check frames for a moment before the view settles, with
        /// the drawer's own count saying the marker is on its path. The verdict goes in the row's note.
        /// </summary>
        private IEnumerator ProveConfinement(VerificationViewRow row, VerificationView view)
        {
            yield return Settle(Math.Min(viewsRequest.Settle, 3));
            var frustum = FrustumOf(viewsCamera);
            int scopeRoom = view.Room != null ? RoomLayers.IndexOf(view.Room) : -1;
            var focus = new Vector3((float)view.FocusX, (float)view.FocusY, (float)view.FocusZ);
            // A lamp of the view's own room where it has one, else the lamp in view nearest its focus.
            var lamp = viewsLamps.Where(home => home.Light != null && home.Light.isActiveAndEnabled && home.Room >= 0 && home.Room < RoomLayers.Count)
                .Where(home => { var p = home.Light.transform.position; return frustum.SphereVisible(p.x, p.y, p.z, 0); })
                .OrderBy(home => home.Room == scopeRoom ? 0 : 1).ThenBy(home => (home.Light.transform.position - focus).sqrMagnitude)
                .FirstOrDefault();
            if (lamp.Light == null) { row.note = Append(row.note, "Confinement not proven: no room's lamp stands in the view."); yield break; }
            var lampAt = lamp.Light.transform.position;
            var toEye = viewsCamera.transform.position - lampAt;
            toEye.y = 0f;
            var at = lampAt + (toEye.sqrMagnitude > .01f ? toEye.normalized * .5f : Vector3.zero);
            at.y = Mathf.Max(.5f, lampAt.y - .9f);
            // The other layer: a room none of whose lamps reaches the marker (the south wing's Nomination and Games have none).
            int other = -1;
            for (int room = 0; room < RoomLayers.Count && other < 0; room++)
            {
                if (room == lamp.Room) continue;
                bool reached = viewsLamps.Any(home => home.Room == room && home.Light != null && (home.Light.transform.position - at).magnitude < home.Light.range + .5f);
                if (!reached) other = room;
            }
            var material = LitMarkerMaterial();
            if (other < 0 || material == null)
            { row.note = Append(row.note, "Confinement not proven: " + (material == null ? "no lit material for its marker." : "every room's lamps reach its marker.")); yield break; }
            uint lampBit = VerificationViews.ConfinementBit(lamp.Room), otherBit = VerificationViews.ConfinementBit(other);

            bool wasOn = Profiler.enabled;
            bool reading = Debug.isDebugBuild && Profiler.supported;
            var category = new ProfilerCategory(DrawerCategoryName);
            if (reading) { Profiler.enabled = true; Profiler.SetCategoryEnabled(category, true); }
            var registeredCounter = reading ? ProfilerRecorder.StartNew(category, DrawerRenderersCounter, 1) : default;
            BeginCheckFrames();
            try
            {
                for (int f = 0; f < 3; f++) yield return null;
                if (!PatchOf(new Bounds(at, Vector3.one * .45f), out var patch, true)) { row.note = Append(row.note, "Confinement not proven: its marker's place is not in the frame."); yield break; }
                var behind = ReadPatch(checkTarget, patch);
                // Made on the other layer: the marker without the lamp's light.
                var unlitMarker = MakeMarker("confinement, other room", Color.white, at, .45f, 0, material);
                unlitMarker.GetComponent<Renderer>().renderingLayerMask = otherBit;
                for (int f = 0; f < 3; f++) yield return null;
                var unlit = ReadPatch(checkTarget, patch);
                DropMarker(unlitMarker);
                for (int f = 0; f < 3; f++) yield return null;
                // Made on the lamp's layer, then moved off it at run time.
                long registered = DrawerCount(registeredCounter);
                var marker = MakeMarker("confinement, lamp's room", Color.white, at, .45f, 0, material);
                var renderer = marker.GetComponent<Renderer>();
                renderer.renderingLayerMask = lampBit;
                for (int f = 0; f < 3; f++) yield return null;
                var lit = ReadPatch(checkTarget, patch);
                bool onPath = registered >= 0 && DrawerCount(registeredCounter) - registered == 1;
                renderer.renderingLayerMask = otherBit;
                yield return null;
                var moved = ReadPatch(checkTarget, patch);
                yield return null;
                var movedLater = ReadPatch(checkTarget, patch);
                DropMarker(marker);
                float drawn = Mathf.Max(Difference(unlit, behind), Difference(lit, behind)), light = Difference(lit, unlit);
                float first = Difference(moved, unlit), second = Difference(movedLater, unlit);
                string when = VerificationViews.WhenItLeft(first, second);
                string numbers = " (the lamp " + lamp.Light.name + " in the " + HouseRoomScope.RoomId(lamp.Room) + ", the other layer the "
                    + HouseRoomScope.RoomId(other) + "'s; drawn " + drawn.ToString("0.000", CultureInfo.InvariantCulture)
                    + ", the lamp's light " + light.ToString("0.000", CultureInfo.InvariantCulture) + ", moved " + first.ToString("0.000", CultureInfo.InvariantCulture)
                    + " then " + second.ToString("0.000", CultureInfo.InvariantCulture) + " from the made-unlit marker).";
                string verdict = drawn <= PresentDifference ? "Confinement not proven: its marker did not draw"
                    : light < PresentDifference ? "Confinement not proven: the marker made on the lamp's bit and the one made on another room's differ too little to tell (the lamp is faint there, or the drawer ignores a renderer's rendering layers)"
                    : !onPath ? "Confinement not proven: its marker is not on the drawer's path (GRD Renderers did not rise by one)"
                    : when == VerificationViews.OnTheFirstRender ? "The drawer applies a rendering-layer write at run time: the marker moved off its lamp's layer lost the lamp's light on its next render"
                    : when == VerificationViews.ARenderLate ? "The drawer applies a rendering-layer write a render late: the marker lost the lamp's light one render after the move"
                    : "The drawer does not apply a rendering-layer write at run time: the marker kept its lamp's light, so this row measures only the out-of-room lamps' shadows";
                row.note = Append(row.note, verdict + numbers);
            }
            finally
            {
                EndCheckFrames();
                registeredCounter.Dispose();
                if (reading && !wasOn) Profiler.enabled = false;
            }
        }

        /// <summary>A lit marker material: a copy of a plain opaque Lit material the house already draws (so its variant is in the build), white and untextured; else URP's Lit.</summary>
        private Material LitMarkerMaterial()
        {
            Material source = null;
            foreach (var entry in viewsTable)
            {
                var candidate = entry.Renderer != null ? entry.Renderer.sharedMaterial : null;
                if (candidate == null || candidate.shader == null || candidate.shader.name != "Universal Render Pipeline/Lit") continue;
                if (candidate.renderQueue >= (int)RenderQueue.AlphaTest || candidate.IsKeywordEnabled("_EMISSION")) continue;
                source = candidate;
                break;
            }
            Material material;
            if (source != null) material = new Material(source);
            else
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) return null;
                material = new Material(lit);
            }
            material.name = "Room scope probe lit marker";
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            probeMaterials.Add(material);
            return material;
        }

        /// <summary>Whether the drawer took the small-mesh threshold at run time: its own counter, read for a moment with the profiler on.</summary>
        private IEnumerator ProveSmallMeshCulling(VerificationViewRow row)
        {
            if (!Debug.isDebugBuild || !Profiler.supported) { row.note = Append(row.note, "Not proven: a release build has no drawer counters."); yield break; }
            bool wasOn = Profiler.enabled;
            var drawer = new ProfilerCategory(DrawerCategoryName);
            Profiler.enabled = true;
            Profiler.SetCategoryEnabled(drawer, true);
            var culled = ProfilerRecorder.StartNew(drawer, "Small Mesh Culled", 1);
            try
            {
                for (int i = 0; i < 10; i++) yield return null;
                long count = culled.Valid && culled.Count > 0 ? culled.LastValue : -1;
                row.note = Append(row.note, count > 0 ? "The drawer culled " + count + " small instances: the threshold took."
                    : count == 0 ? "The drawer culled no small instance: it keeps the threshold it started with, so this row repeats the baseline."
                    : "Not proven: the drawer's counter did not read.");
            }
            finally
            {
                culled.Dispose();
                if (!wasOn) Profiler.enabled = false;
            }
        }

        // ------------------------------------------------------------------------------------
        // The drawer-path checks (§5.3 P1 to P7): in the counter pass, the drawer on as shipped.
        // ------------------------------------------------------------------------------------

        /// <summary>The counter pass's checks before any view is measured: P1, and P6, whose verdict says whether the rows carry the engine's counters.</summary>
        private IEnumerator RunCounterChecks(List<string> notes)
        {
            CheckDrawerActive();
            yield return CheckCountersIncludeTheDrawer(notes);
        }

        /// <summary>The counter pass's checks after its views: P2 to P5 through the check frames, then P7 on what the views logged.</summary>
        private IEnumerator RunProbeChecks(List<string> notes)
        {
            BeginCheckFrames();
            try
            {
                yield return CheckRoomMarkers();
                yield return CheckSameFrameManualRender();
                yield return CheckFirstFrameLayer();
                yield return CheckForceRenderingOff();
            }
            finally { EndCheckFrames(); }
            CheckAtlasLines();
        }

        private void AddCheck(string id, string outcome, string detail)
            => viewsReport.checks.Add(VerificationProbeCheck.Of(id, outcome, detail));

        /// <summary>P1: the drawer is on this player's path - its own coverage counters, read once.</summary>
        private void CheckDrawerActive()
        {
            long renderers = drawerRenderers.Valid && drawerRenderers.Count > 0 ? drawerRenderers.LastValue : -1;
            long excluded = drawerExcluded.Valid && drawerExcluded.Count > 0 ? drawerExcluded.LastValue : -1;
            double coverage = drawerCoverage.Valid && drawerCoverage.Count > 0 ? drawerCoverage.LastValueAsDouble : -1;
            viewsReport.drawerRenderers = renderers;
            viewsReport.drawerExcludedRenderers = excluded;
            viewsReport.drawerCoveragePercent = coverage;
            string detail = "GRD Renderers " + renderers + ", Excluded Renderers " + excluded + ", Coverage % "
                + coverage.ToString("0.00", CultureInfo.InvariantCulture) + "; the asset's mode " + viewsReport.drawerMode + ".";
            AddCheck("P1", renderers > 0 ? VerificationProbeCheck.Passed : VerificationProbeCheck.Failed,
                renderers > 0 ? "The drawer is active. " + detail
                : "The drawer's counters read nothing: it is not on this player's path, or the build has no profiler. " + detail);
        }

        /// <summary>
        /// P1 in the timing and ablations passes, before any view: the drawer's coverage counters read
        /// for a moment with the profiler on, then put back. A release build has no drawer counters, so
        /// there the check is not run and the report's drawerInitialized is the drawer's only witness.
        /// </summary>
        private IEnumerator CheckDrawerActiveBriefly()
        {
            if (!Debug.isDebugBuild || !Profiler.supported)
            {
                AddCheck("P1", VerificationProbeCheck.NotRun, "A release build has no drawer counters; the drawer "
                    + (viewsReport.drawerInitialized == "yes" ? "started" : viewsReport.drawerInitialized == "no" ? "did not start" : "may or may not have started")
                    + " in this player (drawerInitialized " + viewsReport.drawerInitialized + "), the asset's mode " + viewsReport.drawerMode + ".");
                yield break;
            }
            bool wasOn = Profiler.enabled;
            var drawer = new ProfilerCategory(DrawerCategoryName);
            Profiler.enabled = true;
            Profiler.SetCategoryEnabled(drawer, true);
            drawerRenderers = ProfilerRecorder.StartNew(drawer, DrawerRenderersCounter, 1);
            drawerExcluded = ProfilerRecorder.StartNew(drawer, DrawerExcludedCounter, 1);
            drawerCoverage = ProfilerRecorder.StartNew(drawer, DrawerCoverageCounter, 1);
            try
            {
                for (int i = 0; i < 10; i++) yield return null;
                CheckDrawerActive();
            }
            finally
            {
                drawerRenderers.Dispose(); drawerExcluded.Dispose(); drawerCoverage.Dispose();
                if (!wasOn) Profiler.enabled = false;
            }
        }

        /// <summary>From here the main camera draws into a target of the screen's size, which the checks read a frame later.</summary>
        private void BeginCheckFrames()
        {
            int width = Mathf.Max(64, Screen.width), height = Mathf.Max(64, Screen.height);
            checkTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Room scope probe frame" };
            manualTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Room scope probe manual frame" };
            keptTarget = viewsCamera.targetTexture;
            // Before the mask is kept: the hold gives back every room bit the scope had out, at once.
            if (viewsDirector != null) viewsDirector.HoldWholeHouse(checkFramesHold);
            keptCullingMask = viewsCamera.cullingMask;
            viewsCamera.targetTexture = checkTarget;
            checkFramesOn = true;
        }

        private void EndCheckFrames()
        {
            if (checkFramesOn && viewsCamera != null)
            {
                viewsCamera.targetTexture = keptTarget;
                viewsCamera.cullingMask = keptCullingMask;
            }
            if (viewsDirector != null) viewsDirector.Release(checkFramesHold);
            checkFramesOn = false;
            viewsLateUpdate = null;
            foreach (var marker in probeMarkers) if (marker != null) Destroy(marker);
            probeMarkers.Clear();
            if (checkTarget != null) { checkTarget.Release(); Destroy(checkTarget); checkTarget = null; }
            if (manualTarget != null) { manualTarget.Release(); Destroy(manualTarget); manualTarget = null; }
        }

        /// <summary>A same-frame manual render (P3) runs here, after every Update and before the drawer's once-a-frame read.</summary>
        private void LateUpdate()
        {
            var action = viewsLateUpdate;
            viewsLateUpdate = null;
            if (action == null) return;
            try { action(); }
            catch (Exception error) { errors.Add("A same-frame check failed: " + error.Message); }
        }

        /// <summary>
        /// P2: each room's marker, moved to a layer the main camera leaves out, is gone from the first
        /// render after the move and back on the first after it is restored. The first render after a
        /// change made here is this frame's own (the drawer updates in PostLateUpdate, ahead of
        /// FinishFrameRendering), so it is read one frame on; a second read a frame later tells a drawer
        /// a render late - which would flash the old room at every room change - from one that never
        /// follows. A marker the drawer's GRD Renderers did not count says nothing the lane's plain-path
        /// pin does not, so the check is then not run.
        /// </summary>
        private IEnumerator CheckRoomMarkers()
        {
            var failures = new List<string>();
            var offPath = new List<string>();
            var offPathFailures = new List<string>();
            var detail = new StringBuilder();
            string[] letters = { "a", "b", "c", "d", "e", "f", "g", "h" };
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                string room = RoomLayers.RoomIds[i];
                var view = VerificationViews.Find("V4" + letters[i]);
                var scratch = RowFor(view, null);
                yield return ApplyView(view, scratch);
                yield return Settle(Math.Min(viewsRequest.Settle, 3));
                var plan = VerificationViews.Room(room);
                var at = new Vector3((float)plan.CentreX, 2.6f, (float)plan.CentreZ);
                long registered = DrawerCount(drawerRenderers);
                var marker = MakeMarker("P2 " + room, Color.HSVToRGB(i / (float)RoomLayers.Count, 1f, 1f), at, 1.2f, 0);
                marker.SetActive(false);
                for (int f = 0; f < 3; f++) yield return null;
                // From the marker's own box: an inactive renderer reports empty bounds.
                if (!PatchOf(new Bounds(at, Vector3.one * 1.2f), out var patch, true)) { failures.Add(room + ": its marker is not in the frame"); DropMarker(marker); continue; }
                var behind = ReadPatch(checkTarget, patch);
                marker.SetActive(true);
                for (int f = 0; f < 3; f++) yield return null;
                var drawn = ReadPatch(checkTarget, patch);
                bool onPath = OnDrawerPath(registered, 1);
                if (!onPath) offPath.Add(room);
                int layer = RoomLayers.LayerOf(room);
                viewsCamera.cullingMask = keptCullingMask & ~(1 << layer);
                marker.layer = layer;
                yield return null;
                var gone = ReadPatch(checkTarget, patch);
                yield return null;
                var goneLater = ReadPatch(checkTarget, patch);
                marker.layer = 0;
                yield return null;
                var back = ReadPatch(checkTarget, patch);
                yield return null;
                var backLater = ReadPatch(checkTarget, patch);
                viewsCamera.cullingMask = keptCullingMask;
                DropMarker(marker);
                float present = Difference(drawn, behind);
                string left = VerificationViews.WhenItLeft(Difference(gone, behind), Difference(goneLater, behind));
                string returned = VerificationViews.WhenItCameBack(Difference(back, behind), Difference(backLater, behind));
                detail.Append(room).Append(": drawn ").Append(present.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(", left out ").Append(Difference(gone, behind).ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(" then ").Append(Difference(goneLater, behind).ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(", restored ").Append(Difference(back, behind).ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(" then ").Append(Difference(backLater, behind).ToString("0.000", CultureInfo.InvariantCulture)).Append("; ");
                string failure = present <= PresentDifference ? room + ": its marker did not draw"
                    : left != VerificationViews.OnTheFirstRender || returned != VerificationViews.OnTheFirstRender ? room + ": left " + left + ", came back " + returned
                    : null;
                if (failure != null) (onPath ? failures : offPathFailures).Add(failure);
            }
            string reads = "Differences from what lies behind (0..1), on the first render after each change, then the next: " + detail
                + "Marker shader " + markerShader + ".";
            string offPathNote = offPath.Count == 0 ? ""
                : "The markers of " + string.Join(", ", offPath) + " are not on the drawer's path (its GRD Renderers did not rise by one for each), so they say nothing of the drawer"
                  + (offPathFailures.Count > 0 ? "; on the path they took: " + string.Join("; ", offPathFailures) : "") + ". ";
            // A failure on the drawer's path fails the run; markers off it leave the question open.
            string outcome = failures.Count > 0 ? VerificationProbeCheck.Failed : offPath.Count > 0 ? VerificationProbeCheck.NotRun : VerificationProbeCheck.Passed;
            AddCheck("P2", outcome, (failures.Count > 0 ? "Failed for " + string.Join("; ", failures) + ". "
                    : offPath.Count > 0 ? "" : "Every room's marker left the frame on the first render after its layer's bit went, and came back on the first after it was restored. ")
                + offPathNote + reads);
        }

        /// <summary>P3, recorded: does a manual render later in the frame of a layer change see the new layer?</summary>
        private IEnumerator CheckSameFrameManualRender()
        {
            var view = VerificationViews.All[0];
            yield return ApplyView(view, RowFor(view, null));
            yield return Settle(Math.Min(viewsRequest.Settle, 3));
            var at = new Vector3((float)view.FocusX, 2.7f, (float)view.FocusZ);
            long registered = DrawerCount(drawerRenderers);
            var marker = MakeMarker("P3", Color.magenta, at, 1.2f, 0);
            marker.SetActive(false);
            yield return null;
            if (!PatchOf(new Bounds(at, Vector3.one * 1.2f), out var patch, true)) { AddCheck("P3", VerificationProbeCheck.NotRun, "The marker was not in V1's frame."); DropMarker(marker); yield break; }
            Color behind = default, drawn = default, sameFrame = default;
            viewsLateUpdate = () => behind = ManualRenderPatch(patch);
            yield return null;
            yield return null;
            marker.SetActive(true);
            yield return null;
            yield return null;
            bool onPath = OnDrawerPath(registered, 1);
            viewsLateUpdate = () => drawn = ManualRenderPatch(patch);
            yield return null;
            yield return null;
            int layer = RoomLayers.Living;
            viewsCamera.cullingMask = keptCullingMask & ~(1 << layer);
            marker.layer = layer;
            viewsLateUpdate = () => sameFrame = ManualRenderPatch(patch);
            yield return null;
            yield return null;
            viewsCamera.cullingMask = keptCullingMask;
            DropMarker(marker);
            float seen = Difference(sameFrame, behind), present = Difference(drawn, behind);
            string outcome = present <= PresentDifference ? "inconclusive: the marker did not draw in a manual render"
                : !onPath ? "inconclusive: the marker is not on the drawer's path (GRD Renderers did not rise by one)"
                : seen > PresentDifference ? "the manual render drew the marker on its old layer: it did not see the change"
                : seen < AbsentDifference ? "the manual render left the marker out: it saw the change" : "in between";
            AddCheck("P3", VerificationProbeCheck.Recorded, "A layer changed in Update, a manual Camera.Render in LateUpdate of the same frame: "
                + outcome + " (drawn " + present.ToString("0.000", CultureInfo.InvariantCulture) + ", same frame "
                + seen.ToString("0.000", CultureInfo.InvariantCulture) + "). The design never relies on it.");
        }

        /// <summary>P4: a renderer made and given its layer in one frame is drawn (or left out) right from its first render.</summary>
        private IEnumerator CheckFirstFrameLayer()
        {
            var view = VerificationViews.All[0];
            yield return ApplyView(view, RowFor(view, null));
            yield return Settle(Math.Min(viewsRequest.Settle, 3));
            var left = new Vector3((float)view.FocusX - 1.5f, 2.7f, (float)view.FocusZ + 1.5f);
            var right = new Vector3((float)view.FocusX + 1.5f, 2.7f, (float)view.FocusZ - 1.5f);
            var size = Vector3.one * 1.1f;
            if (!PatchOf(new Bounds(left, size), out var leftPatch, true) || !PatchOf(new Bounds(right, size), out var rightPatch, true))
            { AddCheck("P4", VerificationProbeCheck.Failed, "The markers' places were not in V1's frame."); yield break; }
            yield return null;
            var leftBehind = ReadPatch(checkTarget, leftPatch);
            var rightBehind = ReadPatch(checkTarget, rightPatch);
            int layer = RoomLayers.Kitchen;
            viewsCamera.cullingMask = keptCullingMask & ~(1 << layer);
            long registered = DrawerCount(drawerRenderers);
            var outside = MakeMarker("P4 left out", Color.cyan, left, 1.1f, layer);
            var inside = MakeMarker("P4 drawn", Color.yellow, right, 1.1f, 0);
            yield return null;
            float outsideSeen = Difference(ReadPatch(checkTarget, leftPatch), leftBehind);
            float insideSeen = Difference(ReadPatch(checkTarget, rightPatch), rightBehind);
            // The drawer's count of the two, a couple of frames on, before they go.
            yield return null;
            yield return null;
            bool onPath = OnDrawerPath(registered, 2);
            viewsCamera.cullingMask = keptCullingMask;
            DropMarker(outside);
            DropMarker(inside);
            bool ok = outsideSeen < AbsentDifference && insideSeen > PresentDifference;
            string reads = "Made in one frame, read from that frame's render: the one on a left-out layer differs "
                + outsideSeen.ToString("0.000", CultureInfo.InvariantCulture) + " from what lies behind it, the one on layer 0 "
                + insideSeen.ToString("0.000", CultureInfo.InvariantCulture) + ".";
            if (!onPath)
                AddCheck("P4", VerificationProbeCheck.NotRun, "The markers are not on the drawer's path (its GRD Renderers did not rise by two), so P4 says nothing of the drawer. " + reads);
            else AddCheck("P4", ok ? VerificationProbeCheck.Passed : VerificationProbeCheck.Failed, reads);
        }

        /// <summary>
        /// P5, recorded: forceRenderingOff takes a marker out of the main camera's first render after
        /// it is set, and back on the first after it is cleared (read as P2 reads, with the render after
        /// as a witness of a drawer a render late). It says whether the bodies-off ablation measures
        /// anything on the drawer's path, so a marker that never drew, or that the drawer did not count,
        /// leaves it inconclusive.
        /// </summary>
        private IEnumerator CheckForceRenderingOff()
        {
            var view = VerificationViews.All[0];
            var at = new Vector3((float)view.FocusX, 2.7f, (float)view.FocusZ);
            long registered = DrawerCount(drawerRenderers);
            var marker = MakeMarker("P5", Color.green, at, 1.2f, 0);
            marker.SetActive(false);
            yield return null;
            var renderer = marker.GetComponent<Renderer>();
            if (!PatchOf(new Bounds(at, Vector3.one * 1.2f), out var patch, true)) { AddCheck("P5", VerificationProbeCheck.NotRun, "The marker was not in V1's frame."); DropMarker(marker); yield break; }
            yield return null;
            var behind = ReadPatch(checkTarget, patch);
            marker.SetActive(true);
            yield return null; yield return null;
            float drawn = Difference(ReadPatch(checkTarget, patch), behind);
            bool onPath = OnDrawerPath(registered, 1);
            renderer.forceRenderingOff = true;
            yield return null;
            float off = Difference(ReadPatch(checkTarget, patch), behind);
            yield return null;
            float offLater = Difference(ReadPatch(checkTarget, patch), behind);
            renderer.forceRenderingOff = false;
            yield return null;
            float back = Difference(ReadPatch(checkTarget, patch), behind);
            yield return null;
            float backLater = Difference(ReadPatch(checkTarget, patch), behind);
            DropMarker(marker);
            string left = VerificationViews.WhenItLeft(off, offLater), returned = VerificationViews.WhenItCameBack(back, backLater);
            string verdict = drawn <= PresentDifference ? "inconclusive: the marker did not draw."
                : !onPath ? "inconclusive: the marker is not on the drawer's path (GRD Renderers did not rise by one)."
                : left == VerificationViews.OnTheFirstRender && returned == VerificationViews.OnTheFirstRender ? "the drawer honours it on the first render each way."
                : left == VerificationViews.NotAtAll ? "the drawer does not follow it at run time (it came back " + returned + ")."
                : "the drawer follows it late: it left " + left + " and came back " + returned + ".";
            AddCheck("P5", VerificationProbeCheck.Recorded, "forceRenderingOff on a drawer renderer, from what lies behind, on the first render after each change, then the next: drawn "
                + drawn.ToString("0.000", CultureInfo.InvariantCulture) + ", forced off " + off.ToString("0.000", CultureInfo.InvariantCulture)
                + " then " + offLater.ToString("0.000", CultureInfo.InvariantCulture) + ", restored " + back.ToString("0.000", CultureInfo.InvariantCulture)
                + " then " + backLater.ToString("0.000", CultureInfo.InvariantCulture) + "; " + verdict);
        }

        /// <summary>
        /// P6: whether the engine's triangle and draw counters include the drawer's draws, by a grid of
        /// known size on and off at V1, before any view is measured: its verdict
        /// (<see cref="VerificationViews.CountersVerdict"/>) decides whether the rows carry those
        /// counters. It concludes only when the drawer's own Visible Instances rose by the grid.
        /// </summary>
        private IEnumerator CheckCountersIncludeTheDrawer(List<string> notes)
        {
            const int Side = 10;
            const int TrianglesPerCube = 12;
            var view = VerificationViews.All[0];
            yield return ApplyView(view, RowFor(view, null));
            yield return Settle(Math.Min(viewsRequest.Settle, 3));
            var grid = new GameObject("Room scope probe: P6 grid");
            probeMarkers.Add(grid);
            var material = MarkerMaterial(Color.white);
            for (int x = 0; x < Side; x++)
                for (int z = 0; z < Side; z++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var collider = cube.GetComponent<Collider>();
                    if (collider != null) DestroyImmediate(collider);
                    cube.transform.SetParent(grid.transform, false);
                    cube.transform.localPosition = new Vector3((x - Side * .5f) * .3f, 0f, (z - Side * .5f) * .3f);
                    cube.transform.localScale = Vector3.one * .15f;
                    var cubeRenderer = cube.GetComponent<MeshRenderer>();
                    cubeRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    cubeRenderer.sharedMaterial = material;
                }
            grid.transform.position = new Vector3((float)view.FocusX, 2.7f, (float)view.FocusZ);
            grid.SetActive(false);
            for (int i = 0; i < 5; i++) yield return null;
            long[] without = null, with = null;
            yield return SampleCounters(20, values => without = values);
            grid.SetActive(true);
            for (int i = 0; i < 5; i++) yield return null;
            yield return SampleCounters(20, values => with = values);
            DropMarker(grid);
            int visibleAt = RenderCounterNames.Length + 1;
            string verdict = without == null || with == null ? VerificationViews.CountersNotRun
                : VerificationViews.CountersVerdict(without[3], with[3], without[visibleAt], with[visibleAt], Side * Side, TrianglesPerCube);
            viewsReport.renderCountersIncludeDrawer = verdict;
            if (verdict == VerificationViews.CountersNotRun)
            {
                notes.Add("P6 did not run: the rows carry no engine render counters; read the drawer's own.");
                AddCheck("P6", VerificationProbeCheck.NotRun, "The engine's Triangles Count did not read in this build.");
                yield break;
            }
            long Delta(int i) => with[i] >= 0 && without[i] >= 0 ? with[i] - without[i] : -1;
            int known = Side * Side * TrianglesPerCube;
            notes.Add(verdict == VerificationViews.CountersInclude ? "P6: the engine's render counters include the drawer's draws; the rows carry them, comparable between views."
                : verdict == VerificationViews.CountersExclude ? "P6: the engine's render counters do not include the drawer's draws; the rows carry none of them: read the drawer's own."
                : "P6 was inconclusive (the drawer did not draw its grid); the rows carry no engine render counters: read the drawer's own.");
            AddCheck("P6", VerificationProbeCheck.Recorded, "A grid of " + Side * Side + " cubes (" + known + " triangles a pass) on and off at V1: Triangles "
                + Delta(3) + ", Draw Calls " + Delta(2) + ", Batches " + Delta(0) + ", the drawer's Visible Instances " + Delta(visibleAt) + ". The engine's counters: "
                + verdict + (verdict == VerificationViews.CountersInconclusive ? " (the drawer's Visible Instances did not rise by the grid's " + Side * Side + " cubes)." : "."));
        }

        private IEnumerator SampleCounters(int frames, Action<long[]> report)
        {
            var samples = Enumerable.Range(0, RenderCounterNames.Length + DrawerCounterNames.Length).Select(_ => new List<long>()).ToArray();
            for (int f = 0; f < frames; f++)
            {
                yield return null;
                for (int i = 0; i < viewsRenderRecorders.Length; i++) AddCounter(samples[i], viewsRenderRecorders[i]);
                for (int i = 0; i < viewsDrawerRecorders.Length; i++) AddCounter(samples[RenderCounterNames.Length + i], viewsDrawerRecorders[i]);
            }
            report(samples.Select(list => (long)VerificationViews.MedianOfKnown(list.Select(value => (double)value))).ToArray());
        }

        /// <summary>P7: the probe's shadow-map count against URP's own atlas line, view by view.</summary>
        private void CheckAtlasLines()
        {
            var judged = viewsReport.views.Where(row => row.shadows != null).ToList();
            var disagree = judged.Where(row => row.shadows.atlasLine == "disagrees").ToList();
            var agree = judged.Where(row => row.shadows.atlasLine == "agrees").ToList();
            if (disagree.Count > 0)
                AddCheck("P7", VerificationProbeCheck.Failed, "URP's atlas line disagrees with the probe's count at " + string.Join(", ", disagree.Select(row =>
                    row.id + " (logged " + row.shadows.loggedSlices + " maps by " + row.shadows.loggedScale + ", counted "
                    + row.shadows.atlas.slicesKept + " by " + row.shadows.atlas.scale + ")")) + ".");
            else if (agree.Count > 0)
                AddCheck("P7", VerificationProbeCheck.Passed, "URP's atlas line agrees with the probe's count at " + string.Join(", ", agree.Select(row => row.id))
                    + "; no line disagrees.");
            else
                AddCheck("P7", VerificationProbeCheck.Recorded, "URP logged no atlas line during a view: without UNITY_ENABLE_CHECKS (see the build's "
                    + "room-scope-probe-build.json) it logs one line in the player's life. Take P7 in an editor diagnostic run on the probe copy.");
        }

        // ------------------------------------------------------------------------------------
        // Markers and the frames the checks read.
        // ------------------------------------------------------------------------------------

        private string markerShader = "none";

        /// <summary>A flat-coloured marker: URP's Unlit when the build carries it, else Lit (its colour then depends on the light, which the checks allow for).</summary>
        private Material MarkerMaterial(Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                var any = FindAnyObjectByType<MeshRenderer>();
                shader = any != null && any.sharedMaterial != null ? any.sharedMaterial.shader : null;
            }
            if (shader == null) throw new InvalidOperationException("No shader for the probe's markers.");
            markerShader = shader.name;
            var material = new Material(shader) { name = "Room scope probe marker" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            material.color = colour;
            probeMaterials.Add(material);
            return material;
        }

        /// <summary>A flat-coloured cube with no collider and no shadow, on <paramref name="layer"/>; <paramref name="material"/> instead of the flat one when given.</summary>
        private GameObject MakeMarker(string name, Color colour, Vector3 at, float size, int layer, Material material = null)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // No collider, not for a frame: a room layer carries none (RoomLayers).
            var collider = marker.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            marker.name = "Room scope probe: " + name;
            marker.layer = layer;
            marker.transform.SetPositionAndRotation(at, Quaternion.identity);
            marker.transform.localScale = Vector3.one * size;
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material != null ? material : MarkerMaterial(colour);
            probeMarkers.Add(marker);
            return marker;
        }

        /// <summary>Destroys a marker and forgets it, so the check frames' end does not destroy it twice.</summary>
        private void DropMarker(GameObject marker)
        {
            probeMarkers.Remove(marker);
            if (marker != null) Destroy(marker);
        }

        /// <summary>A drawer counter's last value (GRD Renderers counts the renderers on the drawer's path), or -1 when it does not read.</summary>
        private static long DrawerCount(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : -1;

        /// <summary>Whether the markers made since <paramref name="registered"/> was read are on the drawer's path: its GRD Renderers rose by exactly their number.</summary>
        private bool OnDrawerPath(long registered, int markers)
        {
            long now = DrawerCount(drawerRenderers);
            return registered >= 0 && now >= 0 && now - registered == markers;
        }

        /// <summary>The middle of where a box lands in the check frame, in its pixels (bottom-left origin, as ReadPixels reads).</summary>
        private bool PatchOf(Bounds bounds, out RectInt patch, bool middle)
        {
            patch = default;
            float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var screen = viewsCamera.WorldToScreenPoint(point);
                if (screen.z <= 0f) return false;
                x0 = Mathf.Min(x0, screen.x); x1 = Mathf.Max(x1, screen.x);
                y0 = Mathf.Min(y0, screen.y); y1 = Mathf.Max(y1, screen.y);
            }
            int width = checkTarget != null ? checkTarget.width : Screen.width, height = checkTarget != null ? checkTarget.height : Screen.height;
            float cx = (x0 + x1) * .5f, cy = (y0 + y1) * .5f;
            float w = Mathf.Max(4f, (x1 - x0) * (middle ? .4f : 1f)), h = Mathf.Max(4f, (y1 - y0) * (middle ? .4f : 1f));
            int px = Mathf.FloorToInt(cx - w * .5f), py = Mathf.FloorToInt(cy - h * .5f);
            int pw = Mathf.CeilToInt(w), ph = Mathf.CeilToInt(h);
            if (px < 0 || py < 0 || px + pw > width || py + ph > height) return false;
            patch = new RectInt(px, py, pw, ph);
            return true;
        }

        /// <summary>The mean colour of a patch of a target. Puts the active render target back (a Blit-style read leaves it on its source).</summary>
        private static Color ReadPatch(RenderTexture target, RectInt patch)
        {
            var previous = RenderTexture.active;
            var read = new Texture2D(patch.width, patch.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                read.ReadPixels(new Rect(patch.x, patch.y, patch.width, patch.height), 0, 0, false);
                read.Apply(false);
                var pixels = read.GetPixels32();
                double r = 0, g = 0, b = 0;
                foreach (var pixel in pixels) { r += pixel.r; g += pixel.g; b += pixel.b; }
                double n = Math.Max(1, pixels.Length) * 255.0;
                return new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
            }
            finally
            {
                RenderTexture.active = previous;
                Destroy(read);
            }
        }

        /// <summary>The main camera drawn by hand into the manual target, read at once (P3, from LateUpdate); the active render target is put back as it was before the render.</summary>
        private Color ManualRenderPatch(RectInt patch)
        {
            var kept = viewsCamera.targetTexture;
            var active = RenderTexture.active;
            viewsCamera.targetTexture = manualTarget;
            try
            {
                viewsCamera.Render();
                return ReadPatch(manualTarget, patch);
            }
            finally
            {
                viewsCamera.targetTexture = kept;
                RenderTexture.active = active;
            }
        }

        private static float Difference(Color a, Color b)
            => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        // ------------------------------------------------------------------------------------
        // Putting the house back, and the report.
        // ------------------------------------------------------------------------------------

        private void RestoreViews()
        {
            if (shadowCountSubscribed) { RenderPipelineManager.beginCameraRendering -= CountShadowWork; shadowCountSubscribed = false; }
            shadowCountInto = null;
            EndCheckFrames();
            StopCounterRecorders();
            if (viewsDirector != null)
            {
                if (viewsDirector.IsOverview) viewsDirector.EndOverview();
                if (viewsDirector.IsDiaryOpen) viewsDirector.ClosePanels();
                if (viewsHoldsScope) { viewsDirector.Release(this); viewsHoldsScope = false; }
                if (viewsHouseHeld) { viewsDirector.ReleaseHouseFromViewsProbe(this); viewsHouseHeld = false; }
            }
            if (viewsRig != null) { viewsRig.ReleaseShot(0f); viewsRig.ControlsEnabled = true; }
            if (viewsPlayer != null) viewsPlayer.SetInputEnabled(true);
            if (viewsFeedPaused && viewsFeed != null) viewsFeed.enabled = true;
            viewsFeedPaused = false;
            foreach (var material in probeMaterials) if (material != null) Destroy(material);
            probeMaterials.Clear();
        }

        /// <summary>OnDestroy's share: nothing the probe subscribed or started outlives the runner.</summary>
        private void DisposeViews()
        {
            if (viewsRequest == null) return;
            if (shadowCountSubscribed) { RenderPipelineManager.beginCameraRendering -= CountShadowWork; shadowCountSubscribed = false; }
            Application.logMessageReceived -= CollectAtlasLine;
            StopCounterRecorders();
        }

        private void FinishViews(List<string> notes)
        {
            foreach (var row in viewsReport.views.Concat(viewsReport.ablations)) VerificationViews.CompleteRow(row);
            if (viewsReport.views.Concat(viewsReport.ablations).Any(row => row.flagged))
                notes.Add("Flagged rows: a view whose medians were not within 5% of each other was run once more; read its runs before its median.");
            if (viewsReport.frameTimingSource == "none" || viewsReport.views.Concat(viewsReport.ablations).All(row => row.cpuMainMedianMs < 0))
                notes.Add("No CPU thread or GPU times: a release build has no FrameTimingManager (Frame Timing Stats is off in ProjectSettings, and the review build's "
                    + "input gates keep it so), so its rows carry frame times only; the CPU main-thread, render-thread and GPU medians come from the development "
                    + "build's graphical timing run at 16.");
            string name = VerificationViews.ReportName(viewsRequest, viewsReport.houseSize);
            try
            {
                string frames = Path.Combine(outputDirectory, name + "-frames.csv");
                File.WriteAllText(frames, viewsFrames.ToString());
                viewsReport.rawFramesFile = frames;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { errors.Add("The raw frame times could not be written: " + error.Message); }
            viewsReport.notes = notes.ToArray();
            viewsReport.errors = errors.ToArray();
            viewsReport.status = VerificationViews.Status(viewsReport);
            viewsReport.finishedUtc = DateTime.UtcNow.ToString("O");
            string path = Path.Combine(outputDirectory, name + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(viewsReport, true));
            Debug.Log("Gamesim room-scope views probe " + viewsReport.status + " (" + viewsReport.pass + ", " + viewsReport.houseSize + " houseguests): " + path);
            Application.Quit(viewsReport.status == "Passed" ? 0 : 1);
        }
    }
}
