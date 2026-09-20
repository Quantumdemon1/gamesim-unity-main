using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
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
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            Assert.That(player.transform.position,Is.EqualTo(start),"Starting an attempt must not teleport the player.");
            var screen=RequireCompetitionAssemblyScreen();Assert.That(screen.IsAssembling,Is.True);
            var station=SceneComponents<HouseInteractionAnchor>().Single(a=>a.VenueId=="competition-player");
            Assert.That(Vector3.Distance(player.Agent.destination,station.Approach),Is.LessThan(.3f));
            Assert.That(player.InputEnabled,Is.False);Assert.That(cameraRig.ControlsEnabled,Is.False);
            Assert.That(player.TryMoveTo(start+Vector3.right),Is.False,"Ordinary commands cannot replace a reserved route.");
            Assert.That(station.transform.parent.GetComponentsInChildren<Collider>(true),Is.Empty,"Station dressing must not add colliders.");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That((player.transform.position-start).sqrMagnitude,Is.GreaterThan(.0001f),"The claimed station must actually be approached on the native agent.");
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
            ButtonWithCaption("Practice this competition").onClick.Invoke();yield return null;
            var screen=RequireCompetitionAssemblyScreen();
            var station=SceneComponents<HouseInteractionAnchor>().Single(a=>a.VenueId=="competition-player");
            yield return PressKey(Key.Enter);
            Assert.That(screen.IsAssembling,Is.False);Assert.That(screen.IsPlaying,Is.False);
            float deadline=Time.realtimeSinceStartup+36f;
            while(screen.IsShowing && !screen.IsPlaying && Time.realtimeSinceStartup<deadline)
            {
                if(screen.Paused)yield return PressKey(Key.P);
                else yield return null;
            }
            Assert.That(screen.IsPlaying,Is.True,"A real reachable player station must eventually permit the ready countdown and practice.");
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
            yield return PressKey(Key.P);yield return new WaitForSecondsRealtime(.25f);
            Assert.That(screen.IsPlaying,Is.False);Assert.That(player.Agent.isStopped,Is.True);
            yield return PressKey(Key.Escape);Assert.That(director.IsPanelOpen,Is.True);
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
