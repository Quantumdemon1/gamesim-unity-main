using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator CompetitionAssemblyOwnsARealPlayerRouteAndRestoresItOnCancel()
        {
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(player.TryMoveTo(director.StationPosition+Vector3.forward),Is.True);
            var priorDestination=player.Agent.destination;
            Assert.That(director.TryOpenPhasePanel(),Is.True);
            typeof(Gamesim.Episode.EpisodeDirector).GetField("reducedMotion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,false);
            cameraRig.SetReducedMotion(false);
            var before=director.Snapshot;var start=player.transform.position;
            float radius=player.Agent.radius,height=player.Agent.height;bool enabled=player.Agent.enabled;
            var view = cameraRig.ViewCamera;
            float aspect = view.aspect;
            float began = Time.unscaledTime;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            var entrance = player.transform.position;
            Assert.That(view.aspect, Is.EqualTo(aspect).Within(.001f), "The stock viewport is unchanged.");
            Assert.That(Vector3.Distance(start, entrance), Is.GreaterThan(.5f), "The outgoing trip is cut to a nearby entrance.");
            AssertShortStageWalk(player.Agent, "player's practice approach", allowCoveredStart: true);
            Assert.That(StageBodyOutsideViewport(view, player.Agent), Is.True,
                "The entire body begins outside the unchanged stock camera.");
            Assert.That(player.IsRunning, Is.False);
            Assert.That(cameraRig.IsTravelling, Is.False, "The relocation is never shown during a camera sweep.");
            var screen=RequireCompetitionAssemblyScreen();Assert.That(screen.IsAssembling,Is.True);
            var station=SceneComponents<HouseInteractionAnchor>().Single(a=>a.VenueId=="competition-player");
            Assert.That(Vector3.Distance(player.Agent.destination,station.Approach),Is.LessThan(.3f));
            Assert.That(player.InputEnabled,Is.False);Assert.That(cameraRig.ControlsEnabled,Is.False);
            Assert.That(player.TryMoveTo(start+Vector3.right),Is.False,"Ordinary commands cannot replace a reserved route.");
            Assert.That(station.transform.parent.GetComponentsInChildren<Collider>(true),Is.Empty,"Station dressing must not add colliders.");
            yield return WaitFor(() =>
            {
                var torso = view.WorldToViewportPoint(player.transform.position + Vector3.up * 1.1f);
                return !director.IsTravelDipShowing && torso.z > view.nearClipPlane && torso.x > .03f && torso.x < .97f
                    && torso.y > .03f && torso.y < .97f && player.Agent.velocity.sqrMagnitude > .04f
                    && Vector3.Distance(player.transform.position, station.Approach) > .35f;
            }, 4f, "the player visibly walks into the unchanged stock view before reaching the station");
            Assert.That((player.transform.position-entrance).sqrMagnitude,Is.GreaterThan(.0001f),"The claimed station must actually be approached on the native agent.");
            Debug.LogFormat("[Gamesim short arrivals] visible moving entrance: aspect {0:F2}, at {1:F2} s",
                aspect, Time.unscaledTime - began);
            yield return PressKey(Key.P);Assert.That(screen.Paused,Is.True);
            yield return null;var pausedAt=player.transform.position;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Vector3.Distance(player.transform.position,pausedAt),Is.LessThan(.025f));
            Assert.That(director.Snapshot.randomState,Is.EqualTo(before.randomState));
            yield return PressKey(Key.Escape);
            Assert.That(screen.IsShowing,Is.False);Assert.That(director.IsPanelOpen,Is.True);
            Assert.That(player.InputEnabled,Is.False,"Returning to the briefing preserves its input lock.");
            Assert.That(Vector3.Distance(player.Agent.destination,priorDestination),Is.LessThan(.3f));
            Assert.That(player.Agent.radius,Is.EqualTo(radius));Assert.That(player.Agent.height,Is.EqualTo(height));Assert.That(player.Agent.enabled,Is.EqualTo(enabled));
            Assert.That(SceneComponents<HouseInteractionAnchor>().Any(a=>a.VenueId=="competition-player"),Is.False);
            Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision));
            director.ClosePanels();yield return null;
            Assert.That(player.InputEnabled,Is.True);Assert.That(cameraRig.ControlsEnabled,Is.True);
        }

        [UnityTest]
        public IEnumerator SkippingTheAssemblyViewStillWaitsForThePlayersNativeArrival()
        {
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();yield return null;
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            typeof(Gamesim.Episode.EpisodeDirector).GetField("reducedMotion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,false);
            cameraRig.SetReducedMotion(false);
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            AssertShortStageWalk(player.Agent, "standard camera practice approach", allowCoveredStart: true);
            Assert.That(StageBodyOutsideViewport(cameraRig.ViewCamera, player.Agent), Is.True);
            float entranceDistance = cameraRig.DesiredDistance;
            yield return null;
            var screen=RequireCompetitionAssemblyScreen();
            var station=SceneComponents<HouseInteractionAnchor>().Single(a=>a.VenueId=="competition-player");
            float waitingAt = Time.unscaledTime;
            yield return PressKey(Key.Enter);
            Assert.That(screen.IsAssembling,Is.False);Assert.That(screen.IsPlaying,Is.False);
            float deadline=Time.realtimeSinceStartup+36f;
            while(screen.IsShowing && !screen.IsPlaying && Time.realtimeSinceStartup<deadline)
            {
                if(screen.Paused)yield return PressKey(Key.P);
                else yield return null;
            }
            Assert.That(screen.IsPlaying,Is.True,"A real reachable player station must eventually permit the ready countdown and practice.");
            Debug.LogFormat("[Gamesim short arrivals] practice ready and playing after {0:F2} s", Time.unscaledTime - waitingAt);
            Assert.That(cameraRig.DesiredDistance, Is.GreaterThan(entranceDistance + 1f),
                "Ready play restores the station framing beyond the entrance shot.");
            Assert.That(Vector3.Distance(player.transform.position,station.Approach),Is.LessThan(.35f));
            Assert.That(player.Agent.velocity.sqrMagnitude,Is.LessThan(.04f));
            Assert.That(player.InputEnabled,Is.False);
            yield return PressKey(Key.Escape);Assert.That(director.IsPanelOpen,Is.True);
        }

        [UnityTest]
        public IEnumerator MovingTheReservedStationCancelsWithoutCommittingOrLeavingAnInputOwner()
        {
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();yield return null;
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            var before=director.Snapshot;ButtonWithCaption("Practice this competition").onClick.Invoke();yield return null;
            var screen=RequireCompetitionAssemblyScreen();
            var station=SceneComponents<HouseInteractionAnchor>().Single(a=>a.VenueId=="competition-player");
            station.transform.position+=Vector3.right;
            yield return null;yield return null;
            Assert.That(screen.IsShowing,Is.False);Assert.That(director.IsChallengeActive,Is.False);
            Assert.That(director.IsPanelOpen,Is.True);Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision));
            Assert.That(director.Snapshot.randomState,Is.EqualTo(before.randomState));
            director.ClosePanels();yield return null;
            Assert.That(player.InputEnabled,Is.True);Assert.That(cameraRig.ControlsEnabled,Is.True);
            Assert.That(player.TryMoveTo(director.StationPosition),Is.True,"Invalidated staging must release its motion claim.");
        }

        [UnityTest]
        public IEnumerator ReducedMotionSkipsAssemblyViewButStillUsesTheRealStationGate()
        {
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();yield return null;
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            typeof(Gamesim.Episode.EpisodeDirector).GetField("reducedMotion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,true);
            cameraRig.SetReducedMotion(true);
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            var screen=RequireCompetitionAssemblyScreen();
            Assert.That(screen.IsShowing,Is.True);Assert.That(screen.IsAssembling,Is.False);Assert.That(screen.IsPlaying,Is.False);
            Assert.That(SceneComponents<HouseInteractionAnchor>().Any(a=>a.VenueId=="competition-player"),Is.True);
            AssertShortStageWalk(player.Agent, "reduced-motion practice approach", allowCoveredStart: true);
            Assert.That(director.IsFramingCompetition, Is.False, "Reduced motion keeps the current camera.");
            yield return PressKey(Key.P);yield return new WaitForSecondsRealtime(.25f);
            Assert.That(screen.IsPlaying,Is.False);Assert.That(player.Agent.isStopped,Is.True);
            yield return PressKey(Key.Escape);Assert.That(director.IsPanelOpen,Is.True);
        }

        private void AssertShortStageWalk(NavMeshAgent agent, string label, bool allowCoveredStart = false)
        {
            Assert.That(agent, Is.Not.Null, label);
            Assert.That(agent.isOnNavMesh && agent.hasPath, Is.True, label + " owns a native path");
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete), label);
            var corners = agent.path.corners;
            Assert.That(corners.Length, Is.GreaterThanOrEqualTo(2), label + " has a moving approach");
            float length = Vector3.Distance(agent.transform.position, corners[0]);
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            Assert.That(length, Is.InRange(.5f, 4.55f), label + " has a real walk bounded to the stage");
            Assert.That(agent.speed, Is.EqualTo(HouseNpcMotion.DefaultSpeed).Within(.01f), label + " walks");
            if (!allowCoveredStart) return;
            var camera = cameraRig.ViewCamera;
            Assert.That(camera, Is.Not.Null, "The stock camera must render the approach.");
            bool outside = StageBodyOutsideViewport(camera, agent);
            Debug.LogFormat("[Gamesim short arrivals] {0}: {1:F2} m, outside stock view={2}, covered={3}",
                label, length, outside, director.IsTravelDipShowing);
            Assert.That(outside || director.IsTravelDipShowing, Is.True, label + " starts beyond the stock viewport or behind the cut");
        }

        private static bool StageBodyOutsideViewport(Camera camera, NavMeshAgent agent)
        {
            bool left = true, right = true, below = true, above = true, behind = true, beyond = true;
            float radius = agent.radius + .25f;
            var bounds = new Bounds(agent.transform.position + Vector3.up * (agent.height * .5f),
                new Vector3(radius * 2f, agent.height + .3f, radius * 2f));
            var visual = agent.GetComponent<CharacterPresentation>()?.VisualRoot;
            if (visual != null)
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
            var min = bounds.min;
            var max = bounds.max;
            for (int i = 0; i < 8; i++)
            {
                var at = new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var point = camera.WorldToViewportPoint(at);
                behind &= point.z < camera.nearClipPlane;
                beyond &= point.z > camera.farClipPlane;
                left &= point.z > 0f && point.x < 0f;
                right &= point.z > 0f && point.x > 1f;
                below &= point.z > 0f && point.y < 0f;
                above &= point.z > 0f && point.y > 1f;
            }
            return left || right || below || above || behind || beyond;
        }

        private CompetitionGameScreen RequireCompetitionAssemblyScreen()
        {
            var screens=SceneComponents<CompetitionGameScreen>().ToArray();
            Assert.That(screens,Has.Length.EqualTo(1),
                "Practice must reserve a real player station and open one competition screen. Visible interface: "
                +string.Join(" | ",SceneComponents<TMPro.TMP_Text>().Where(text=>text.isActiveAndEnabled).Select(text=>text.text)));
            Assert.That(screens[0].IsShowing,Is.True,"Practice must show the competition screen after reserving the station.");
            return screens[0];
        }
    }
}
