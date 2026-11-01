using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The lamps' hook (ROOM-SCOPE-PLAN §3.2 d, D8 i, RS3): while the scope is a room, the main camera
    /// renders with every lamp homed outside it off, from its begin of rendering to its end, and
    /// every light reads as authored between frames. Each test reads the lights inside the main
    /// camera's own begin callback, after the director's hook: handlers run in the order they were
    /// subscribed, and the director subscribes when it is enabled, at load, before any test does.
    /// Every source is still off (RS3 makes none live), so each test holds the scope on.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// §3.5's room views with the follow spotlight over the one followed: six cube faces for each
        /// lamp at home and one for the spot, so 19, 13, 7 or 1 shadow maps, at 256 px a map in the
        /// three-lamp rooms and 512 in the two- and one-lamp rooms; the empty rooms keep the spot's own 256.
        /// </summary>
        private static readonly string[] LampScopeRooms = { "Living", "Kitchen", "Bedroom", "Private", "Yard", "HoH", "Nomination", "Games" };
        private static readonly int[] LampScopeMaps = { 19, 1, 19, 13, 1, 19, 13, 7 };
        private static readonly int[] LampScopeMapSize = { 256, 256, 256, 512, 256, 256, 512, 512 };

        /// <summary>What one render of the main camera saw, from inside its begin callback.</summary>
        private sealed class LampScopeSight
        {
            public int Renders;
            public RoomSet Viewed;
            public int LampsOff;
            /// <summary>The house's lamps as the render saw them, in the homes' order.</summary>
            public bool[] Lamps;
            /// <summary>Every other light the render saw changed from its authored state, by name.</summary>
            public readonly List<string> OthersChanged = new List<string>();
            /// <summary>The shadowed lights the camera's frustum keeps, as the shadow-map model reads them.</summary>
            public readonly List<VerificationShadowLight> Shadowed = new List<VerificationShadowLight>();
            public readonly List<string> ShadowedNames = new List<string>();
            public int LampSlices, Spots;
            public Vector3 Eye;
        }

        /// <summary>
        /// The main camera's lamps in each room view: inside its render, only the viewed room's own
        /// lamps are on, the frustum keeps 19, 13, 7 or 1 shadow maps as §3.5 models them (the follow
        /// spotlight's one face included), and the atlas lays them at 256 or 512 px instead of the
        /// whole house's 128. Between frames, every lamp is as authored. With nothing held, the main
        /// camera renders with every lamp.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_OnlyTheViewedRoomsLampsCastShadowsForTheMainCamera()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            var others = LampScopeOtherLights(homes);
            var view = cameraRig.ViewCamera;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null, "The project renders through URP.");
            LampScopeSight sight = null;
            void Look(ScriptableRenderContext context, Camera camera)
            {
                if (camera != view || sight == null || sight.Renders > 0) return;
                LampScopeRead(sight, camera, homes, others);
            }
            var owner = new object();
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            RenderPipelineManager.beginCameraRendering += Look;
            try
            {
                // Nothing held: every source is off, so the main camera renders with every lamp.
                sight = new LampScopeSight();
                yield return LampScopeNextRender(() => sight.Renders > 0);
                Assert.That(sight.Viewed.IsWholeHouse, Is.True, "Nothing holds the scope: the whole house.");
                Assert.That(sight.LampsOff, Is.Zero, "The hook leaves every lamp on for the whole house.");
                Assert.That(sight.Lamps, Is.EqualTo(authored), "Every lamp as authored inside the main camera's render.");

                // The player is carried by its transform to each room's middle (the walker rule reads
                // only the feet) and followed there, under the room's view at D4's farthest zoom.
                agent.ResetPath();
                agent.updatePosition = false;
                director.HoldRoomScope(owner);
                var failures = new List<string>();
                for (int r = 0; r < LampScopeRooms.Length; r++)
                {
                    string room = LampScopeRooms[r];
                    int index = RoomLayers.IndexOf(room);
                    var roomView = VerificationViews.All.Single(v => v.Id.StartsWith("V4", StringComparison.Ordinal) && v.Room == room);
                    player.transform.position = ScopeCentre(scope, room);
                    Physics.SyncTransforms();
                    cameraRig.MoveTo(LampScopeShot(roomView));
                    cameraRig.FocusSubject(player.transform, false);
                    yield return LampScopeSettle(room);
                    sight = new LampScopeSight();
                    yield return LampScopeNextRender(() => sight.Renders > 0);

                    string where = room + " (" + roomView.Id + ")";
                    Assert.That(sight.Viewed, Is.EqualTo(RoomSet.Of(room)), where + ": the held scope is the followed player's room.");
                    var expectedOn = homes.Select((home, i) => authored[i] && home.Room == index).ToArray();
                    Assert.That(sight.Lamps, Is.EqualTo(expectedOn), where + ": inside the main camera's render only the room's own lamps are on.");
                    Assert.That(sight.LampsOff, Is.EqualTo(homes.Where((home, i) => authored[i] && home.Room != index).Count()),
                        where + ": the hook switched off every other lamp that was on.");
                    Assert.That(sight.OthersChanged, Is.Empty, where + ": the fills, the screens' lights and the Moon are untouched.");
                    Assert.That(sight.Spots, Is.EqualTo(1), where + ": the follow spotlight over the player is the one spot: " + string.Join(", ", sight.ShadowedNames));
                    int homeLamps = homes.Where((home, i) => authored[i] && home.Room == index).Count();
                    if (sight.LampSlices != 6 * homeLamps)
                        failures.Add(where + ": " + sight.LampSlices + " lamp faces, not 6 x " + homeLamps + " (" + string.Join(", ", sight.ShadowedNames) + ")");
                    var atlas = VerificationViews.LayoutShadowAtlas(sight.Shadowed, sight.Eye.x, sight.Eye.y, sight.Eye.z, pipeline.additionalLightsShadowmapResolution);
                    if (atlas.slicesRequested != LampScopeMaps[r] || atlas.slicesKept != LampScopeMaps[r])
                        failures.Add(where + ": " + atlas.slicesRequested + " maps asked for and " + atlas.slicesKept + " kept, not §3.5's " + LampScopeMaps[r]);
                    if (atlas.largestSlice != LampScopeMapSize[r])
                        failures.Add(where + ": " + atlas.largestSlice + " px a map, not §3.5's " + LampScopeMapSize[r]);

                    // Between frames, as authored, and the hook holds nothing.
                    Assert.That(homes.Select(home => home.Light.enabled).ToArray(), Is.EqualTo(authored), where + ": between frames every lamp is as authored.");
                    Assert.That(director.LampsOffForTheMainCamera, Is.Zero, where + ": between frames the hook holds no lamp off.");
                }
                Assert.That(failures, Is.Empty, string.Join("; ", failures));
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Look;
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                cameraRig.ClearSubject();
                agent.updatePosition = agentPlaces;
            }

            // Let go, the main camera renders with every lamp again.
            sight = new LampScopeSight();
            RenderPipelineManager.beginCameraRendering += Look;
            try { yield return LampScopeNextRender(() => sight.Renders > 0); }
            finally { RenderPipelineManager.beginCameraRendering -= Look; }
            Assert.That(sight.Viewed.IsWholeHouse, Is.True, "Released, the whole house.");
            Assert.That(sight.Lamps, Is.EqualTo(authored), "Released, every lamp as authored inside the main camera's render.");
        }

        /// <summary>
        /// The live feed is not the main camera: with the scope held on the kitchen, the living room's
        /// three lamps are off inside the main camera's render and on inside the feed's (D10: the feed
        /// films the house's other rooms with their lamps). Between the two, as authored.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheLiveFeedKeepsItsRoomsLamps()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var feed = Object.FindAnyObjectByType<LiveFeed>();
            Assert.That(feed, Is.Not.Null, "The director attaches the live feed.");
            Assert.That(feed.Camera, Is.Not.Null);
            var view = cameraRig.ViewCamera;
            Assert.That(feed.Camera, Is.Not.SameAs(view), "The feed films through a camera of its own.");
            int living = RoomLayers.IndexOf("Living");
            var livingLamps = homes.Where(home => home.Room == living).Select(home => home.Light).ToArray();
            Assert.That(livingLamps, Has.Length.EqualTo(3), "The living room's three lamps (RS1 pins them).");
            Assert.That(livingLamps.All(lamp => lamp.isActiveAndEnabled), Is.True, "They are on as authored.");
            bool[] inMain = null, inFeed = null;
            int mainOff = -1;
            void Look(ScriptableRenderContext context, Camera camera)
            {
                if (camera == view && inMain == null) { inMain = livingLamps.Select(lamp => lamp.enabled).ToArray(); mainOff = director.LampsOffForTheMainCamera; }
                else if (camera == feed.Camera && inFeed == null) inFeed = livingLamps.Select(lamp => lamp.enabled).ToArray();
            }
            var owner = new object();
            RenderPipelineManager.beginCameraRendering += Look;
            try
            {
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Kitchen"), Distance = 11.25f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Kitchen"),
                });
                director.HoldRoomScope(owner);
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "The held scope is the kitchen's.");
                yield return LampScopeNextRender(() => inMain != null);
                Assert.That(inMain, Is.All.False, "Inside the main camera's render the living room's lamps are off: the kitchen is viewed.");
                Assert.That(mainOff, Is.GreaterThanOrEqualTo(livingLamps.Length), "The hook had them, among the others, off.");
                Assert.That(livingLamps.Select(lamp => lamp.enabled), Is.All.True, "Between renders they read as authored.");

                // The feed renders by hand, from its own LateUpdate, as here.
                feed.Camera.Render();
                Assert.That(inFeed, Is.Not.Null, "The feed's render reached the begin callback.");
                Assert.That(inFeed, Is.All.True, "Inside the feed's render the living room's lamps are on.");
                Assert.That(livingLamps.Select(lamp => lamp.enabled), Is.All.True, "And after it, as authored.");
                Assert.That(director.LampsOffForTheMainCamera, Is.Zero, "The hook holds nothing outside the main camera's render.");
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Look;
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
        }

        /// <summary>
        /// Light.enabled between frames is the authored state (§3.3): with the scope held on the games
        /// room for a run of real frames, every lamp but its one is off inside each main-camera render
        /// and back by the end callback; the frame's code, before and after rendering, reads every
        /// light as authored; and nothing else the house lights by - fills, screens, the Moon - ever
        /// changes, inside a render or out. A living-room lamp and the games room's own are off before
        /// the hold, so the authored state has a lamp off on each side of the scope: the hook leaves a
        /// lamp that is already off alone, records only what it switched, and puts back only that.
        /// A main-camera render that never reaches its end (URP skips the end when a begin handler
        /// throws) is made good twice: the next frame's Update puts its lamps back, and so does the next
        /// render's begin, before it reads a view that has moved.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_LightsReadAsAuthoredBetweenFrames()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            int games = RoomLayers.IndexOf("Games");
            int living = RoomLayers.IndexOf("Living");
            var offOutside = homes.First(home => home.Room == living).Light;
            var offInside = homes.First(home => home.Room == games).Light;
            Assert.That(offOutside.enabled && offInside.enabled, Is.True, "Both lamps are on as the scene authors them.");
            int outsideAsAuthored = homes.Count(home => home.Light.enabled && home.Room != games);
            offOutside.enabled = false;
            offInside.enabled = false;
            try
            {
                var everyLight = SceneComponents<Light>();
                var authored = everyLight.Select(light => light.enabled).ToArray();
                var lampsAuthored = homes.Select(home => home.Light.enabled).ToArray();
                var others = LampScopeOtherLights(homes);
                var view = cameraRig.ViewCamera;
                int outside = homes.Count(home => home.Light.enabled && home.Room != games);
                Assert.That(outside, Is.EqualTo(outsideAsAuthored - 1), "The living-room lamp off by hand is not among the lamps to switch.");
                Assert.That(outside, Is.GreaterThan(0), "There are lamps outside the games room to switch.");
                int renders = 0, scopedRenders = 0;
                var failures = new List<string>();
                void OnBegin(ScriptableRenderContext context, Camera camera)
                {
                    if (camera != view) return;
                    renders++;
                    var sight = new LampScopeSight();
                    LampScopeRead(sight, camera, homes, others);
                    if (sight.Viewed != RoomSet.Of("Games")) return;
                    scopedRenders++;
                    if (sight.LampsOff != outside) failures.Add("a render with " + sight.LampsOff + " lamps off, not " + outside);
                    for (int i = 0; i < homes.Count; i++)
                        if (homes[i].Room != games && sight.Lamps[i]) failures.Add(homes[i].Light.name + " on inside a games-room render");
                    if (offInside.enabled) failures.Add(offInside.name + ", off before the hold, on inside a games-room render");
                    if (sight.OthersChanged.Count > 0) failures.Add("changed inside a render: " + string.Join(", ", sight.OthersChanged));
                }
                void OnEnd(ScriptableRenderContext context, Camera camera)
                {
                    if (camera != view) return;
                    // The director's end handler subscribed first, so it has put them back by now.
                    LampScopeAsAuthored(everyLight, authored, "at the main camera's end of rendering", failures);
                }
                var owner = new object();
                RenderPipelineManager.beginCameraRendering += OnBegin;
                RenderPipelineManager.endCameraRendering += OnEnd;
                try
                {
                    cameraRig.MoveTo(new HouseCameraRig.Shot
                    {
                        Focus = ScopeCentre(scope, "Games"), Distance = 10.5f, Pitch = 45f, Yaw = 90f, Rooms = RoomSet.Of("Games"),
                    });
                    director.HoldRoomScope(owner);
                    // Real frames, at least thirty and half a second of them (a batchmode frame is far
                    // under a millisecond), but no more than six hundred.
                    float until = Time.realtimeSinceStartup + .5f;
                    int frames = 0;
                    while (frames < 30 || (Time.realtimeSinceStartup < until && frames < 600))
                    {
                        yield return null;
                        frames++;
                        // The frame's code: the director's Update has run, the frame is not yet drawn.
                        LampScopeAsAuthored(everyLight, authored, "in frame " + frames + "'s code", failures);
                        if (director.LampsOffForTheMainCamera != 0) failures.Add("frame " + frames + ": the hook holds lamps off between renders");
                        if (failures.Count > 8) break;
                    }
                    if (scopedRenders == 0)
                    {
                        // A run that draws no frames to a screen: one render by hand, through the same hook.
                        LampScopeRenderByHand(view);
                    }
                    Assert.That(scopedRenders, Is.GreaterThan(0), "The main camera rendered the games room's scope (" + renders + " renders).");
                    Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(10)));

                    // A render that never reached its end: the hook's begin handler, run by hand just
                    // after the main camera's end, leaves the lamps as such a render does, with no main
                    // render before the next Update. That Update puts them back.
                    var begin = typeof(EpisodeDirector).GetMethod("BeginLampScope", BindingFlags.NonPublic | BindingFlags.Static);
                    Assert.That(begin, Is.Not.Null, "The lamps' begin handler.");
                    int leftOff = -1, leftAt = -1;
                    void LeaveOff(ScriptableRenderContext context, Camera camera)
                    {
                        if (camera != view || leftAt >= 0) return;
                        begin.Invoke(null, new object[] { context, camera });
                        leftOff = director.LampsOffForTheMainCamera;
                        leftAt = Time.frameCount;
                    }
                    RenderPipelineManager.endCameraRendering += LeaveOff;
                    try
                    {
                        float deadline = Time.realtimeSinceStartup + 2f;
                        int waited = 0;
                        while (leftAt < 0 && (waited < 3 || Time.realtimeSinceStartup < deadline))
                        {
                            yield return null;
                            waited++;
                        }
                        if (leftAt < 0) LampScopeRenderByHand(view);
                    }
                    finally { RenderPipelineManager.endCameraRendering -= LeaveOff; }
                    Assert.That(leftAt, Is.GreaterThanOrEqualTo(0), "The main camera's render reached the end callback.");
                    Assert.That(leftOff, Is.EqualTo(outside), "Run by hand after the main camera's end, the hook has the lamps outside the games room off.");
                    // Drawn by hand, the render came in this frame's code: the next Update is a frame away.
                    if (Time.frameCount == leftAt) yield return null;
                    var unended = new List<string>();
                    LampScopeAsAuthored(everyLight, authored, "in the frame's code after a render that never ended", unended);
                    Assert.That(unended, Is.Empty, "The next Update put the lamps back: " + string.Join("; ", unended));
                    Assert.That(director.LampsOffForTheMainCamera, Is.Zero, "and the hook holds none.");

                    // Left unfinished again, and the view moved to the living room before the next render:
                    // that render's begin puts back what the last one left off before it reads the new
                    // scope, so the room it now views has its lamps. All in this frame's code, so no
                    // Update comes between the two.
                    begin.Invoke(null, new object[] { default(ScriptableRenderContext), view });
                    Assert.That(director.LampsOffForTheMainCamera, Is.EqualTo(outside), "By hand, the hook has the lamps outside the games room off.");
                    cameraRig.MoveTo(new HouseCameraRig.Shot
                    {
                        Focus = ScopeCentre(scope, "Living"), Distance = 11.25f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Living"),
                    });
                    Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Living")), "The held scope is the living room's.");
                    LampScopeSight moved = null;
                    void Moved(ScriptableRenderContext context, Camera camera)
                    {
                        if (camera != view || moved != null) return;
                        moved = new LampScopeSight();
                        LampScopeRead(moved, camera, homes, others);
                    }
                    RenderPipelineManager.beginCameraRendering += Moved;
                    try { LampScopeRenderByHand(view); }
                    finally { RenderPipelineManager.beginCameraRendering -= Moved; }
                    Assert.That(moved, Is.Not.Null, "The render by hand reached the begin callback.");
                    Assert.That(moved.Viewed, Is.EqualTo(RoomSet.Of("Living")), "The next render reads the living room's scope.");
                    Assert.That(moved.Lamps, Is.EqualTo(homes.Select((home, i) => lampsAuthored[i] && home.Room == living).ToArray()),
                        "Inside the next render, the living room's lamps as authored and every other lamp off.");
                    Assert.That(moved.LampsOff, Is.EqualTo(homes.Where((home, i) => lampsAuthored[i] && home.Room != living).Count()),
                        "The hook holds off exactly the lamps outside the living room that were on.");
                    Assert.That(moved.OthersChanged, Is.Empty, "No fill, screen light or the Moon changes inside the render.");
                    var ended = new List<string>();
                    LampScopeAsAuthored(everyLight, authored, "after the next render's end", ended);
                    Assert.That(ended, Is.Empty, string.Join("; ", ended));
                    Assert.That(director.LampsOffForTheMainCamera, Is.Zero, "The hook holds none after the next render's end.");
                    Assert.That(failures, Is.Empty, "At the main camera's ends: " + string.Join("; ", failures.Take(10)));
                }
                finally
                {
                    RenderPipelineManager.beginCameraRendering -= OnBegin;
                    RenderPipelineManager.endCameraRendering -= OnEnd;
                    director.Release(owner);
                    cameraRig.ReleaseShot(0f);
                }
                yield return null;
                var after = new List<string>();
                LampScopeAsAuthored(everyLight, authored, "after the hold", after);
                Assert.That(after, Is.Empty, string.Join("; ", after));
            }
            finally
            {
                offOutside.enabled = true;
                offInside.enabled = true;
            }
        }

        /// <summary>
        /// The sleep dim still dims its fill under a held scope: the fills are not lamps, so the hook
        /// never switches one, and the dim finds the bedroom's fill and takes it down whether the
        /// bedroom is viewed or not. The bedroom's lamps are off for the main camera only while the
        /// view leaves the bedroom out; on waking the fill comes back exactly, and every lamp reads as
        /// authored between frames throughout.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheSleepDimStillDimsItsFill()
        {
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            var others = LampScopeOtherLights(homes);
            var bed = PlacesFor(HouseFurnitureActivity.Sleep).First(anchor => anchor.transform.parent.name == "bedSingle");
            Assert.That(bed.RoomId, Is.EqualTo("Bedroom"), "A single bed is the bedroom's.");
            int bedroom = RoomLayers.IndexOf("Bedroom");
            var fill = SceneComponents<Light>().Single(light => light.name == bed.RoomId + " fill");
            Assert.That(HouseRoomScope.IsLamp(fill), Is.False, "A fill is not a lamp: it casts no shadows.");
            float lit = fill.intensity;
            var view = cameraRig.ViewCamera;
            LampScopeSight sight = null;
            bool fillOn = false;
            float fillAt = -1f;
            void Look(ScriptableRenderContext context, Camera camera)
            {
                if (camera != view || sight == null || sight.Renders > 0) return;
                LampScopeRead(sight, camera, homes, others);
                fillOn = fill.isActiveAndEnabled;
                fillAt = fill.intensity;
            }
            var owner = new object();
            director.HoldRoomScope(owner);
            RenderPipelineManager.beginCameraRendering += Look;
            try
            {
                yield return BeginInHouse(bed, HouseFurnitureActivity.Sleep);
                float dimming = Time.realtimeSinceStartup + EpisodeDirector.SleepDimSeconds + 1f;
                while (Time.realtimeSinceStartup < dimming && fill.intensity > lit * .5f) yield return null;
                Assert.That(director.DimmedForSleep, Is.SameAs(fill), "The dim found the bedroom's fill under the held scope.");
                Assert.That(fill.intensity, Is.LessThan(lit * .5f), "The room goes dim while the player sleeps in it.");

                // Viewed: the bedroom's lamps on, the others off, the fill on and dimmed.
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Bedroom"), Distance = 11.25f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Bedroom"),
                });
                sight = new LampScopeSight();
                yield return LampScopeNextRender(() => sight.Renders > 0);
                Assert.That(sight.Viewed, Is.EqualTo(RoomSet.Of("Bedroom")));
                Assert.That(sight.Lamps, Is.EqualTo(homes.Select((home, i) => authored[i] && home.Room == bedroom).ToArray()),
                    "Inside the main camera's render, the bedroom's lamps alone.");
                Assert.That(fillOn, Is.True, "The fill is on inside the render.");
                Assert.That(fillAt, Is.LessThan(lit * .5f), "and dimmed.");

                // Not viewed: the bedroom's lamps off for the main camera; the fill on and dimmed still.
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Kitchen"), Distance = 11.25f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Kitchen"),
                });
                sight = new LampScopeSight();
                yield return LampScopeNextRender(() => sight.Renders > 0);
                Assert.That(sight.Viewed, Is.EqualTo(RoomSet.Of("Kitchen")));
                Assert.That(Enumerable.Range(0, homes.Count).Where(i => homes[i].Room == bedroom).Select(i => sight.Lamps[i]), Is.All.False,
                    "With the kitchen viewed, the bedroom's lamps are off for the main camera.");
                Assert.That(fillOn, Is.True, "Its fill is not a lamp: on inside the render,");
                Assert.That(fillAt, Is.LessThan(lit * .5f), "and still dimmed.");
                Assert.That(sight.OthersChanged, Is.Empty, "No fill, screen light or the Moon changes inside the render.");
                Assert.That(homes.Select(home => home.Light.enabled).ToArray(), Is.EqualTo(authored), "Between frames, every lamp as authored.");
                Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Sleep), "The shots did not wake the player.");
                cameraRig.ReleaseShot(0f);

                director.FinishPlayerHouseActivity();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.IsPlayerHouseActivityActive, Is.False, "Getting up ends it.");
                float waking = Time.realtimeSinceStartup + EpisodeDirector.SleepDimSeconds + 1f;
                while (Time.realtimeSinceStartup < waking && director.DimmedForSleep != null) yield return null;
                Assert.That(director.DimmedForSleep, Is.Null, "The light is let go of once the player is up.");
                Assert.That(fill.intensity, Is.EqualTo(lit), "And the fill comes back exactly as it was.");
                Assert.That(fill.enabled, Is.True);
                Assert.That(homes.Select(home => home.Light.enabled).ToArray(), Is.EqualTo(authored), "Every lamp as authored.");
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Look;
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
        }

        /// <summary>
        /// R8: statics outlive play sessions in the editor (domain reload is off at play start), so the
        /// hook is one subscription on the pipeline's begin and end camera events, however often the
        /// director is enabled. Disabled, it goes. A new session's SubsystemRegistration reset drops
        /// whatever the last one left - here the live director's own, as a session that never let go
        /// would leave it - and the next director to enable subscribes once, not twice, and the hook
        /// works again.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TwoPlaySessionsHoldOneSubscription()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            int lampsOn = authored.Count(on => on);
            Assert.That(LampScopeSubscriptions("beginCameraRendering"), Is.EqualTo(1), "The enabled director's hook: one begin handler.");
            Assert.That(LampScopeSubscriptions("endCameraRendering"), Is.EqualTo(1), "and one end handler.");

            director.enabled = false;
            try
            {
                Assert.That(LampScopeSubscriptions("beginCameraRendering"), Is.Zero, "Disabled, the hook goes with the director.");
                Assert.That(LampScopeSubscriptions("endCameraRendering"), Is.Zero);
            }
            finally { director.enabled = true; }
            Assert.That(LampScopeSubscriptions("beginCameraRendering"), Is.EqualTo(1), "Enabled again: one.");
            director.enabled = false;
            director.enabled = true;
            Assert.That(LampScopeSubscriptions("beginCameraRendering"), Is.EqualTo(1), "And again: still one.");
            Assert.That(LampScopeSubscriptions("endCameraRendering"), Is.EqualTo(1));

            // The next play session, as the editor starts it: the SubsystemRegistration reset runs
            // before any scene loads, while the last session's subscription is still on the events.
            var reset = typeof(EpisodeDirector).GetMethod("ForgetLampScope", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(reset, Is.Not.Null, "The director's lamp hook has its reset.");
            Assert.That(reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>()?.loadType,
                Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration), "It runs at SubsystemRegistration, before a session's first scene.");
            reset.Invoke(null, null);
            Assert.That(LampScopeSubscriptions("beginCameraRendering"), Is.Zero, "The reset lets the last session's hook go.");
            Assert.That(LampScopeSubscriptions("endCameraRendering"), Is.Zero);

            var view = cameraRig.ViewCamera;
            int off = -1;
            void Look(ScriptableRenderContext context, Camera camera)
            {
                if (camera == view && off < 0) off = director.LampsOffForTheMainCamera;
            }
            var owner = new object();
            RenderPipelineManager.beginCameraRendering += Look;
            try
            {
                // The kitchen has no lamp of its own, so a working hook has every lamp off for it.
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Kitchen"), Distance = 11.25f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Kitchen"),
                });
                director.HoldRoomScope(owner);
                yield return LampScopeNextRender(() => off >= 0);
                Assert.That(off, Is.Zero, "Reset, the old session's director is no longer served: its lamps stay on.");

                // This session's director enables, as a loaded scene's does: one subscription, and the hook works.
                director.enabled = false;
                director.enabled = true;
                Assert.That(LampScopeSubscriptions("beginCameraRendering"), Is.EqualTo(1), "The new session's director: one begin handler.");
                Assert.That(LampScopeSubscriptions("endCameraRendering"), Is.EqualTo(1), "and one end handler.");
                // The director subscribed again, after this test's handler: the test subscribes again too,
                // to read the lamps after the hook. The kitchen's shot again, whatever re-enabling let go.
                RenderPipelineManager.beginCameraRendering -= Look;
                RenderPipelineManager.beginCameraRendering += Look;
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Kitchen"), Distance = 11.25f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Kitchen"),
                });
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "The held scope is the kitchen's.");
                off = -1;
                yield return LampScopeNextRender(() => off >= 0);
                Assert.That(off, Is.EqualTo(lampsOn), "The hook switches every lamp off for the kitchen's view again.");
                Assert.That(director.LampsOffForTheMainCamera, Is.Zero, "and holds none between frames.");
                Assert.That(homes.Select(home => home.Light.enabled).ToArray(), Is.EqualTo(authored), "Every lamp as authored between frames.");
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Look;
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
        }

        // ------------------------------------------------------------------------------------
        // Helpers.
        // ------------------------------------------------------------------------------------

        /// <summary>The house's lamps and their homes, as the hook reads them: fourteen, all present.</summary>
        private static List<HouseRoomScope.LampHome> LampScopeHomes(HouseRoomScope scope)
        {
            var homes = new List<HouseRoomScope.LampHome>();
            scope.LampHomes(homes);
            Assert.That(homes, Has.Count.EqualTo(14), "The house's fourteen lamps (RS1 pins their homes).");
            Assert.That(homes.All(home => home.Light != null && home.Room >= 0), Is.True, "Each lamp stands in a room.");
            return homes;
        }

        /// <summary>Every other light in the house - the fills, the screens' lights, the Moon - with its authored state.</summary>
        private static Dictionary<Light, bool> LampScopeOtherLights(List<HouseRoomScope.LampHome> homes)
        {
            var lamps = new HashSet<Light>(homes.Select(home => home.Light));
            return SceneComponents<Light>().Where(light => !lamps.Contains(light)).ToDictionary(light => light, light => light.enabled);
        }

        /// <summary>
        /// Reads, inside a begin callback of <paramref name="camera"/>: the scope, the hook's count, each
        /// lamp's state, any other light changed, and the shadowed lights the camera's frustum keeps (as
        /// the probe counts them, §5.3: a light by its range sphere, six faces for a point light, one for a spot).
        /// </summary>
        private void LampScopeRead(LampScopeSight sight, Camera camera, List<HouseRoomScope.LampHome> homes, Dictionary<Light, bool> others)
        {
            sight.Renders++;
            sight.Viewed = director.ViewedRooms;
            sight.LampsOff = director.LampsOffForTheMainCamera;
            sight.Lamps = homes.Select(home => home.Light.enabled).ToArray();
            foreach (var pair in others)
                if (pair.Key != null && pair.Key.enabled != pair.Value) sight.OthersChanged.Add(pair.Key.name);
            sight.Eye = camera.transform.position;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera.projectionMatrix * camera.worldToCameraMatrix);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light == null || !light.isActiveAndEnabled) continue;
                if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                if (light.shadows == LightShadows.None || light.shadowStrength <= 0f) continue;
                var at = light.transform.position;
                bool inside = true;
                foreach (var plane in planes)
                    if (plane.GetDistanceToPoint(at) < -light.range) { inside = false; break; }
                if (!inside) continue;
                sight.Shadowed.Add(PortVerification.ShadowLightOf(light));
                sight.ShadowedNames.Add(light.name);
                if (light.type == LightType.Spot) sight.Spots++;
                else sight.LampSlices += 6;
            }
        }

        private static void LampScopeAsAuthored(Light[] lights, bool[] authored, string when, List<string> failures)
        {
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].enabled != authored[i])
                    failures.Add(lights[i].name + " reads " + (lights[i].enabled ? "on" : "off") + " " + when);
        }

        /// <summary>A V4 view as a shot naming its room (the probe's own shot, §5.3).</summary>
        private static HouseCameraRig.Shot LampScopeShot(VerificationView view) => new HouseCameraRig.Shot
        {
            Focus = new Vector3((float)view.FocusX, (float)view.FocusY, (float)view.FocusZ),
            Distance = (float)view.Distance, Pitch = (float)view.Pitch, Yaw = (float)view.Yaw,
            FieldOfView = (float)view.FieldOfView, Rooms = RoomSet.Of(view.Room),
        };

        /// <summary>
        /// Waits, on the real clock (the rig eases on unscaled time, and a batchmode frame is far under
        /// a millisecond, so a frame's own step says nothing), for the camera to reach the pose it eases
        /// toward: the pivot on its focus, the boom at its distance, the angles and the lens its shot's.
        /// </summary>
        private IEnumerator LampScopeSettle(string room)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            bool Landed()
            {
                var pivot = cameraRig.transform;
                return !cameraRig.IsTravelling
                    && (pivot.position - cameraRig.DesiredFocus).magnitude < .02f
                    && Mathf.Abs(cameraRig.Distance - cameraRig.DesiredDistance) < .02f
                    && Mathf.Abs(Mathf.DeltaAngle(pivot.eulerAngles.y, cameraRig.Yaw)) < .2f
                    && Mathf.Abs(Mathf.DeltaAngle(pivot.eulerAngles.x, cameraRig.Pitch)) < .2f
                    && Mathf.Abs(cameraRig.ViewCamera.fieldOfView - cameraRig.DesiredFieldOfView) < .1f;
            }
            yield return null;
            while (!Landed() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Landed(), Is.True, "The camera came to rest over the " + room + " (pivot "
                + cameraRig.transform.position.ToString("F2") + " for " + cameraRig.DesiredFocus.ToString("F2")
                + ", boom " + cameraRig.Distance.ToString("F2") + " for " + cameraRig.DesiredDistance.ToString("F2") + ").");
        }

        /// <summary>
        /// Waits for the main camera's next render to be seen: the frame's own, or, where the run draws
        /// no frames to a screen, one by hand through the same begin and end callbacks.
        /// </summary>
        private IEnumerator LampScopeNextRender(Func<bool> seen)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            int frames = 0;
            while (!seen() && (frames < 3 || Time.realtimeSinceStartup < deadline))
            {
                yield return null;
                frames++;
            }
            if (!seen()) LampScopeRenderByHand(cameraRig.ViewCamera);
            Assert.That(seen(), Is.True, "The main camera's render reached the begin callback.");
        }

        /// <summary>The main camera drawn once by hand into a target the size of the screen, then everything about it put back.</summary>
        private static void LampScopeRenderByHand(Camera camera)
        {
            var target = camera.targetTexture;
            var active = RenderTexture.active;
            var frame = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = frame;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = target;
                RenderTexture.active = active;
                frame.Release();
                Object.Destroy(frame);
            }
        }

        /// <summary>The director's handlers on one of the pipeline's camera events, read from the event's own field.</summary>
        private static int LampScopeSubscriptions(string eventName)
        {
            var field = typeof(RenderPipelineManager).GetField(eventName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "RenderPipelineManager keeps " + eventName + "'s handlers in a field of that name.");
            var handlers = field.GetValue(null) as Delegate;
            if (handlers == null) return 0;
            return handlers.GetInvocationList().Count(handler => handler.Method.DeclaringType == typeof(EpisodeDirector));
        }
    }
}
