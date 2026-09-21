using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The director's shots (VISUAL-TARGET.md, phase V5): the overview, the conversation's
    /// two-shot, the diary's chair. Each leaves the dollhouse's envelope on purpose - a pitch below
    /// its floor, a boom inside its zoom, a lens of its own - and each hands the camera back.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Shots_TheOverviewFramesEveryRoomAndKeepsTheCastMoving()
        {
            float fieldOfView = cameraRig.ViewCamera.fieldOfView;
            Assert.That(director.ShowOverview(), Is.True);
            Assert.That(director.IsOverview, Is.True);
            Assert.That(director.IsPanelOpen, Is.False, "The overview is a camera mode, not a panel: the cast keeps walking.");
            Assert.That(player.InputEnabled, Is.True, "and the player can still be walked.");
            Assert.That(cameraRig.HasShot, Is.True);
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f) && cameraRig.LensOrthographic >= 0.999f, 5f);

            var camera = cameraRig.ViewCamera;
            Assert.That(cameraRig.LensOrthographic, Is.EqualTo(1f).Within(0.001f), "The overview looks through an orthographic lens.");
            Assert.That(camera.projectionMatrix.m33, Is.EqualTo(1f).Within(0.01f), "which the projection carries.");
            Assert.That(cameraRig.Pitch, Is.EqualTo(EpisodeDirector.OverviewPitch).Within(0.01f));
            var markers = SceneComponents<HouseRoomMarker>();
            Assert.That(markers, Has.Length.EqualTo(8));
            foreach (var marker in markers)
            {
                var seen = camera.WorldToViewportPoint(marker.transform.position);
                Assert.That(seen.z, Is.GreaterThan(0f), marker.RoomName + " is in front of the camera.");
                Assert.That(seen.x, Is.InRange(0.02f, 0.98f), marker.RoomName + " is on screen: " + seen);
                Assert.That(seen.y, Is.InRange(0.02f, 0.98f), marker.RoomName + " is on screen: " + seen);
            }

            var chips = director.GetComponentsInChildren<Canvas>(true)
                .Where(c => c.renderMode == RenderMode.WorldSpace && c.name.EndsWith(" label")).ToArray();
            Assert.That(chips, Has.Length.EqualTo(8), "A chip a room.");
            var words = chips.Select(c => c.GetComponentInChildren<TMP_Text>(true).text).ToArray();
            Assert.That(words, Does.Contain("KITCHEN").And.Contain("HOH SUITE").And.Contain("COMPETITION YARD"));
            Assert.That(director.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == EpisodeHud.OverviewColumnName),
                Is.True, "The column says who is where.");

            director.EndOverview();
            Assert.That(director.IsOverview, Is.False);
            Assert.That(cameraRig.HasShot, Is.False);
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f) && cameraRig.LensOrthographic <= 0.001f, 5f);
            Assert.That(camera.projectionMatrix.m33, Is.EqualTo(0f).Within(0.01f), "A perspective lens again.");
            Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView).Within(0.05f));
            Assert.That(cameraRig.Pitch, Is.InRange(45f, 70f));
            Assert.That(director.GetComponentsInChildren<Canvas>(true).Any(c => c.name.EndsWith(" label")), Is.False,
                "The chips go with the overview.");
            Assert.That(director.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == EpisodeHud.OverviewColumnName),
                Is.False, "and so does the column.");
        }

        [UnityTest]
        public IEnumerator Shots_AConversationTakesATwoShotAndBlendsDepthOfFieldInAndOut()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            var closeUp = SceneComponents<Volume>().Single(v => v.name == HouseCameraRig.CloseUpVolumeName);
            float fieldOfView = cameraRig.ViewCamera.fieldOfView;
            yield return null;
            Assert.That(closeUp.weight, Is.EqualTo(0f).Within(0.001f), "The house is sharp when nobody is talking.");

            yield return OpenNearbyNpc(maya);
            Assert.That(cameraRig.IsConversationFocused, Is.True);
            Assert.That(cameraRig.HasShot, Is.True, "A conversation is a two-shot.");
            yield return Settle(() => Mathf.Abs(cameraRig.Distance - HouseCameraRig.TwoShotDistance) < 0.05f && cameraRig.DepthOfFieldWeight > 0.95f
                && Mathf.Abs(cameraRig.ViewCamera.fieldOfView - HouseCameraRig.TwoShotFieldOfView) < 0.3f, 6f);
            Assert.That(cameraRig.Pitch, Is.EqualTo(HouseCameraRig.TwoShotPitch).Within(0.01f), "Below the dollhouse's floor: a shot may.");
            Assert.That(cameraRig.Distance, Is.EqualTo(HouseCameraRig.TwoShotDistance).Within(0.1f), "Inside the zoom's limit: a shot may.");
            Assert.That(cameraRig.ViewCamera.fieldOfView, Is.EqualTo(HouseCameraRig.TwoShotFieldOfView).Within(0.5f));
            Assert.That(closeUp.weight, Is.GreaterThan(0.95f), "The pair is in focus and the house behind them is not.");
            Assert.That(closeUp.profile.TryGet<DepthOfField>(out var depthOfField), Is.True);
            Assert.That(depthOfField.focusDistance.value, Is.EqualTo(cameraRig.AppliedDistance).Within(0.01f), "Focus sits on the pivot between them.");
            var pair = (player.transform.position + maya.transform.position) * 0.5f + Vector3.up;
            var seen = cameraRig.ViewCamera.WorldToViewportPoint(pair);
            Assert.That(seen.z > 0f && seen.x > 0.2f && seen.x < 0.8f && seen.y > 0.2f && seen.y < 0.8f, Is.True, "The pair is centred: " + seen);

            director.ClosePanels();
            Assert.That(cameraRig.IsConversationFocused, Is.False);
            Assert.That(cameraRig.HasShot, Is.False, "The conversation's end lets the shot go.");
            yield return Settle(() => closeUp.weight < 0.02f && cameraRig.Pitch >= 45f
                && Mathf.Abs(cameraRig.ViewCamera.fieldOfView - fieldOfView) < 0.3f, 6f);
            Assert.That(closeUp.weight, Is.LessThan(0.02f));
            Assert.That(cameraRig.Pitch, Is.InRange(45f, 70f), "Back inside the dollhouse's range.");
            Assert.That(cameraRig.ViewCamera.fieldOfView, Is.EqualTo(fieldOfView).Within(0.5f));
            Assert.That(cameraRig.ControlsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator Shots_InterruptedDiaryRestoresInputAndRefusesAnUnavailableChair()
        {
            WarpPlayer(director.DiaryPosition);
            yield return null;
            var position=player.transform.position;
            Assert.That(director.TryOpenDiary(),Is.True);
            yield return null;
            var anchor=SceneComponents<HouseInteractionAnchor>().Single(a=>a.VenueId==HouseInteractionAnchors.DiaryVenue);
            anchor.gameObject.SetActive(false);
            yield return null;yield return null;
            Assert.That(director.IsDiaryOpen,Is.False);
            Assert.That(director.HasDiaryRoom,Is.False);
            Assert.That(director.CanUseDiary,Is.False);
            Assert.That(director.TryOpenDiary(),Is.False);
            Assert.That(player.InputEnabled && cameraRig.ControlsEnabled,Is.True);
            Assert.That(player.Agent.enabled && player.Agent.isOnNavMesh && player.Agent.updatePosition,Is.True);
            Assert.That(player.GetComponent<Collider>().enabled,Is.True);
            Assert.That(player.GetComponent<HouseSeatPresentation>().Active,Is.False);
            Assert.That(Vector3.Distance(player.transform.position,position),Is.LessThan(.05f));
        }

        [UnityTest]
        public IEnumerator Shots_TheDiaryRoomSeatsAndFramesThePlayerThenRestoresNavigation()
        {
            WarpPlayer(director.DiaryPosition + Vector3.right * 1.5f);
            yield return null;
            var returnPosition=player.transform.position;
            var beforeVisual=player.GetComponent<CharacterPresentation>().VisualRoot.localPosition;
            bool updatePosition=player.Agent.updatePosition;
            var chair = SceneComponents<Transform>().FirstOrDefault(t => t.name == EpisodeDirector.DiaryChairName);
            Assert.That(chair, Is.Not.Null, "The set has the confessional chair the shot looks at.");
            Assert.That(director.TryOpenDiary(), Is.True);
            Assert.That(Vector3.Distance(player.transform.position,returnPosition),Is.LessThan(.01f),"Opening schedules a real route without teleporting.");
            Assert.That(player.GetComponent<DiarySeatPose>().Phase,Is.EqualTo(DiarySeatPose.VisitPhase.Approaching));
            var beforeSeating=director.Snapshot;
            director.ReviewStudyHouse("memorize-layout");director.ConfirmDiaryDecision();
            Assert.That(director.HasDiaryDecisionDraft,Is.False,"Direct callbacks must not bypass physical seating.");
            AssertEquivalent(beforeSeating,director.Snapshot);
            yield return WaitForDiarySeating();
            var reachedApproach=player.transform.position;
            Assert.That(Vector3.Distance(reachedApproach,director.DiaryPosition),Is.LessThan(.55f));
            Assert.That(Vector3.Distance(reachedApproach,returnPosition),Is.GreaterThan(.8f),"The actor must actually walk from the admitted proximity to the approach.");
            Assert.That(cameraRig.HasShot, Is.True, "The diary is a chair shot,");
            Assert.That(cameraRig.IsConversationFocused, Is.False, "not a conversation,");
            Assert.That(cameraRig.ControlsEnabled, Is.False, "and the camera is the director's while it is open.");
            var seating=player.GetComponent<DiarySeatPose>();
            Assert.That(seating,Is.Not.Null);
            Assert.That(seating.IsOccupying,Is.True);
            Assert.That(player.GetComponent<CharacterPresentation>().IsSeated,Is.True);
            Assert.That(player.Agent.enabled && player.Agent.isOnNavMesh,Is.True,"The approach's navigation ownership is retained.");
            Assert.That(player.Agent.updatePosition,Is.False,"Navigation cannot pull the staged actor out of the chair.");
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f)
                && player.GetComponent<HouseSeatPresentation>().Settled
                && Mathf.Abs(cameraRig.DepthOfFieldWeight - EpisodeDirector.DiaryShotDepthOfField) < 0.02f, 5f);
            Assert.That(cameraRig.Pitch, Is.LessThan(45f), "Below the dollhouse's floor: an eye at standing height.");
            Assert.That(cameraRig.DepthOfFieldWeight, Is.EqualTo(EpisodeDirector.DiaryShotDepthOfField).Within(0.02f));
            Assert.That(Vector3.Distance(player.transform.position,reachedApproach),Is.LessThan(.05f),
                "After arrival only the seated visual moves; the navigation root keeps its reached approach.");
            Assert.That(player.GetComponent<HouseSeatPresentation>().Settled,Is.True);
            var seen = cameraRig.ViewCamera.WorldToViewportPoint(seating.FacePosition);
            Assert.That(seen.z > 0f && seen.x > 0.3f && seen.x < 0.7f && seen.y > 0.3f && seen.y < 0.7f, Is.True, "The actual seated face holds the frame: " + seen);
            Assert.That(Vector3.Dot(player.transform.forward,(cameraRig.ViewCamera.transform.position-player.transform.position).normalized),Is.GreaterThan(.5f),
                "The interview shows the player's face, not their back.");
            var screenFace=cameraRig.ViewCamera.WorldToScreenPoint(seating.FacePosition);
            Assert.That(ScreenRect(ActiveRect("Episode panel")).Contains(new Vector2(screenFace.x,screenFace.y)),Is.False,
                "The diary choices leave the speaker's face visible.");
            Assert.That(SceneComponents<Transform>().Any(t=>t.name=="Diary interview backdrop" && t.gameObject.activeInHierarchy),Is.True);

            director.ClosePanels();
            Assert.That(director.IsDiaryOpen, Is.False);
            Assert.That(cameraRig.HasShot, Is.False, "Leaving the room releases the chair.");
            yield return WaitForDiaryExit();
            Assert.That(seating.Active,Is.False);
            Assert.That(player.GetComponent<CharacterPresentation>().IsSeated,Is.False);
            Assert.That(player.Agent.updatePosition,Is.EqualTo(updatePosition));
            Assert.That(Vector3.Distance(player.transform.position,reachedApproach),Is.LessThan(.1f),"Exit returns to the reached approach, without a navigation shortcut.");
            yield return Settle(() => cameraRig.Pitch >= 45f && cameraRig.DepthOfFieldWeight < 0.02f, 5f);
            Assert.That(cameraRig.Pitch, Is.InRange(45f, 70f));
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            Assert.That(Vector3.Distance(player.GetComponent<CharacterPresentation>().VisualRoot.localPosition,beforeVisual),Is.LessThan(.025f),
                "Leaving the chair restores the visual root as well as navigation.");
        }

        private static IEnumerator Settle(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
