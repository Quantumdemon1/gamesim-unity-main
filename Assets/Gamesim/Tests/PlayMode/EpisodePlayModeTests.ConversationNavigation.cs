using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        // Explicit navigation keeps lookup and absence assertions honest: looking for a hidden
        // command never opens another page as a side effect.
        private IEnumerator EnterConversationCategory(string caption)
        {
            Assert.That(director.IsConversationOpen, Is.True, "A category belongs to an open conversation.");
            if (director.IsConversationExchangePlaying)
            {
                ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick.Invoke();
                yield return null;
            }
            var back = ButtonWithCaptionOrNull(EpisodeDirector.ConversationTopicsBackCaption);
            if (back != null) { back.onClick.Invoke(); yield return null; }
            ButtonWithCaption(caption).onClick.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Null, "A category displays its choices without the root wheel.");
            Assert.That(ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption), Is.Not.Null);
        }

        private IEnumerator EnterCategoryForLeaf(string caption)
        {
            if (ButtonWithCaptionOrNull(caption) != null) yield break;
            string category = caption == EpisodeHud.SmallTalkCaption || caption == EpisodeHud.PersonalChatCaption
                || caption == EpisodeHud.RelationshipBuildingCaption || caption == EpisodeHud.ShareSecretCaption
                || caption == "Spend time together" ? EpisodeDirector.ConnectCategoryCaption
                : caption == EpisodeHud.DiscussGameCaption || caption == EpisodeHud.StrategicDiscussionCaption
                    ? EpisodeDirector.GameCategoryCaption
                : caption == "Ask what they have heard" || caption == EpisodeHud.AskVoteCaption
                    || caption == EpisodeHud.ReadPersonCaption || caption == "Share something I know"
                    || caption == EpisodeDirector.NomineeIntelPickerCaption ? EpisodeDirector.LearnCategoryCaption
                : caption == EpisodeDirector.VentPickerCaption || caption == EpisodeDirector.LiePickerCaption
                    || caption == EpisodeDirector.WhisperPickerCaption || caption == EpisodeDirector.CalloutPickerCaption
                    || caption == "Work against them quietly" ? EpisodeDirector.PressureCategoryCaption
                : ConversationRoomActs.Contains(caption) ? EpisodeDirector.TogetherCategoryCaption
                : EpisodeDirector.DealsCategoryCaption;
            yield return EnterConversationCategory(category);
        }

        [UnityTest]
        public IEnumerator ConversationNavigation_CategoriesTargetsAndBackKeepTheSavedSeasonUntouched()
        {
            yield return InstallTalkingHouse(8, false);
            yield return TalkTo(Listener(director.Snapshot).id);
            var before = JsonUtility.ToJson(director.Snapshot);
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            foreach (string category in PetalCaptions)
            {
                yield return EnterConversationCategory(category);
                Assert.That(ActiveRect("NPC spoken dialogue"), Is.Null,
                    "Browsing a category does not invent or replay an NPC greeting.");
                int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(director.Snapshot)
                    - EpisodeEngine.SocialActionsSpent(director.Snapshot));
                string budget = left + (left == 1 ? " action" : " actions") + " left";
                Assert.That(ShownText(), Does.Contain(budget)
                    .And.Not.Contain("Opening topics and choosing an approach are free."),
                    "The page shows the current budget; each actual leaf carries its own paid/free tag.");
                if (category == EpisodeDirector.PressureCategoryCaption)
                {
                    yield return PressRow(EpisodeDirector.VentPickerCaption);
                    Assert.That(director.ConversationPicker, Is.EqualTo(EpisodeDirector.VentPickerCaption));
                }
                yield return KeyboardSubmit(EpisodeDirector.ConversationTopicsBackCaption);
                Assert.That(director.CurrentConversationCategory, Is.Null);
                Assert.That(director.ConversationPicker, Is.Null, "Back does not leave another category's target picker open.");
                Assert.That(ActiveRect(EpisodeHud.ConversationColumnName), Is.Null);
                Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
                yield return AssertKeyboardRing(category + " returned to topics", ModalRoot);
            }
        }

        [UnityTest]
        public IEnumerator ConversationNavigation_OldCategoryCallbacksCannotSwitchANewerPageOrReloadedConversation()
        {
            yield return InstallTalkingHouse(8, false);
            string listener = Listener(director.Snapshot).id;
            yield return TalkTo(listener);
            var oldConnect = ButtonWithCaption(EpisodeDirector.ConnectCategoryCaption).onClick;
            yield return EnterConversationCategory(EpisodeDirector.LearnCategoryCaption);
            string page = director.CurrentConversationCategory;
            var oldBack = ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick;
            oldConnect.Invoke(); yield return null;
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(page), "A retained root callback cannot overwrite the current page.");
            yield return EnterConversationCategory(EpisodeDirector.DealsCategoryCaption);
            page = director.CurrentConversationCategory;
            oldBack.Invoke(); yield return null;
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(page), "The old page cannot take Back for the new page.");
            director.LoadNow(); HoldTheHouseForTheFixture(); yield return null;
            yield return TalkTo(listener);
            var before = JsonUtility.ToJson(director.Snapshot);
            oldConnect.Invoke(); oldBack.Invoke(); yield return null;
            Assert.That(director.CurrentConversationCategory, Is.Null);
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ConversationNavigation_PaidLeafCallbacksCannotCommitFromAnotherCategoryRootOrReload()
        {
            yield return InstallTalkingHouse(8, false);
            string listener = Listener(director.Snapshot).id;
            yield return TalkTo(listener);
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            var paidClick = ButtonWithCaption(EpisodeHud.SmallTalkCaption).onClick;
            var before = JsonUtility.ToJson(director.Snapshot);
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            paidClick.Invoke(); yield return null;
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before), "Another category retires a paid choice at the same revision.");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
            yield return KeyboardSubmit(EpisodeDirector.ConversationTopicsBackCaption);
            paidClick.Invoke(); yield return null;
            Assert.That(director.CurrentConversationCategory, Is.Null);
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before), "The old paid choice cannot commit from the root wheel.");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
            director.LoadNow(); HoldTheHouseForTheFixture(); yield return null;
            yield return TalkTo(listener);
            before = JsonUtility.ToJson(director.Snapshot);
            bytes = File.ReadAllBytes(director.SavePath);
            paidClick.Invoke(); yield return null;
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before), "The loaded conversation cannot inherit the old leaf's authority.");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));

            // Sharing is an earlier render return, with its own controls and the same revision.
            // It must retire the previous Learn page even though category preparation does not run.
            yield return InstallSharingHouse(1);
            var oldRead = ButtonWithCaption(EpisodeHud.ReadPersonCaption).onClick;
            before = JsonUtility.ToJson(director.Snapshot);
            bytes = File.ReadAllBytes(director.SavePath);
            yield return OpenInformationShare();
            Assert.That(director.IsChoosingSharedInformation, Is.True);
            oldRead.Invoke(); yield return null;
            Assert.That(director.IsChoosingSharedInformation, Is.True, "A retired leaf cannot interrupt memory selection.");
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before), "The sharing screen retires all prior category commands.");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
        }

        [UnityTest]
        public IEnumerator ConversationNavigation_ARevisionInvalidatesOldCategoryCallbacks()
        {
            yield return InstallTalkingHouse(8, false);
            string listener = Listener(director.Snapshot).id;
            yield return TalkTo(listener);
            var oldCategory = ButtonWithCaption(EpisodeDirector.DealsCategoryCaption).onClick;
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.SmallTalkCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            string page = director.CurrentConversationCategory;
            oldCategory.Invoke(); yield return null;
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(page));
            Assert.That(director.IsConversationExchangePlaying, Is.True, "Old navigation cannot interrupt the accepted exchange.");
        }

        [UnityTest]
        public IEnumerator ConversationNavigation_FreeReadRemainsReachableWhenTheActionBudgetIsSpent()
        {
            yield return InstallTalkingHouse(8, false, s => s.socialActions = EpisodeEngine.SocialActionBudget(s));
            string listener = Listener(director.Snapshot).id;
            yield return TalkTo(listener);
            var before = director.Snapshot;
            yield return EnterConversationCategory(EpisodeDirector.LearnCategoryCaption);
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.ReadPersonCaption)), Does.Contain(EpisodeDirector.FreeTag));
            ButtonWithCaption(EpisodeHud.ReadPersonCaption).onClick.Invoke(); yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
            AssertIntentDurable(director.Snapshot);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ConversationNavigation_LargestTextAndCompactFramesShowOneMenuInsideTheFrame()
        {
            yield return InstallTalkingHouse(8, false);
            string listener = Listener(director.Snapshot).id;
            yield return ApplyTextSize(TextSize.Largest);
            foreach (bool compact in new[] { false, true })
            {
                director.SetCompactHud(compact);
                yield return TalkTo(listener);
                Assert.That(ActiveRect(EpisodeHud.ConversationColumnName), Is.Null, "The topic wheel leaves the actor view clear.");
                var wheel = ActiveRect(EpisodeHud.DialName);
                Assert.That(wheel, Is.Not.Null);
                var rootCanvas = (RectTransform)HudCanvas().transform;
                Assert.That(TourEncloses(TourWorldRect(rootCanvas), TourWorldRect(wheel)), Is.True, "The largest-text topic wheel stays in the frame.");
                var context = ActiveRect(EpisodeHud.ConversationTopicsContextName);
                Assert.That(context, Is.Not.Null);
                Assert.That(TourEncloses(TourWorldRect(rootCanvas), TourWorldRect(context)), Is.True,
                    "The conversation identity, standing and time remain inside the frame.");
                Assert.That(ScreenBox(context).Overlaps(ScreenBox(wheel)), Is.False,
                    "The context does not cover any topic controls.");
                foreach (var label in context.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextTruncated, Is.False, "The largest context text wraps without truncation: " + label.text);
                }
                if (Application.isBatchMode)
                {
                    yield return CaptureFraming("dialogue-topics-largest" + (compact ? "-compact" : ""),
                        inspect: frame => AssertConversationFacesInsideCapture(frame, listener), prepare: SettleConversationCaptureCamera);
                    yield return CaptureFraming("dialogue-topics-largest" + (compact ? "-compact" : "") + "-4x3", width: 1200, height: 900,
                        inspect: frame => AssertConversationFacesInsideCapture(frame, listener), prepare: SettleConversationCaptureCamera);
                }
                foreach (string category in PetalCaptions)
                {
                    yield return EnterConversationCategory(category);
                    Canvas.ForceUpdateCanvases();
                    var column = ActiveRect(EpisodeHud.ConversationColumnName);
                    var canvas = (RectTransform)HudCanvas().transform;
                    Assert.That(TourEncloses(TourWorldRect(canvas), TourWorldRect(column)), Is.True,
                        category + ": the page stays inside the frame at largest text, compact=" + compact);
                    Assert.That(ActiveRect(EpisodeHud.DialName), Is.Null, "No second menu covers the actors.");
                    var back = (RectTransform)ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).transform;
                    Assert.That(TourEncloses(TourWorldRect(canvas), TourWorldRect(back)), Is.True, "Back remains visible.");
                    if (Application.isBatchMode && category == EpisodeDirector.LearnCategoryCaption)
                    {
                        yield return CaptureFraming("dialogue-navigation-largest" + (compact ? "-compact" : ""),
                            inspect: frame => AssertConversationFacesInsideCapture(frame, listener), prepare: SettleConversationCaptureCamera);
                        yield return CaptureFraming("dialogue-navigation-largest" + (compact ? "-compact" : "") + "-4x3", width: 1200, height: 900,
                            inspect: frame => AssertConversationFacesInsideCapture(frame, listener), prepare: SettleConversationCaptureCamera);
                    }
                    yield return AssertKeyboardRing(category + " largest text, compact=" + compact, ModalRoot);
                }
            }
            director.SetCompactHud(false);
            yield return ApplyTextSize(TextSize.Standard);
        }

        // The capture adapter changes aspect and re-renders the HUD before this preparation.
        // Wait for the real shot and lens; do not move either actor to make the framing pass.
        private IEnumerator SettleConversationCaptureCamera()
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && (cameraRig.IsTravelling
                || !cameraRig.HasArrived(.005f)
                || Mathf.Abs(cameraRig.ViewCamera.fieldOfView - cameraRig.DesiredFieldOfView) > .01f))
                yield return null;
            Assert.That(cameraRig.IsTravelling, Is.False, "The conversation shot settled before inspecting the capture.");
            Assert.That(cameraRig.HasArrived(.005f), Is.True, "The actual two-shot reached its requested frame.");
            Assert.That(cameraRig.ViewCamera.fieldOfView,
                Is.EqualTo(cameraRig.DesiredFieldOfView).Within(.01f));
            float expectedFieldOfView = cameraRig.ConversationWindowOffset > 0f && cameraRig.ViewCamera.aspect < 1.5f
                ? Gamesim.House.HouseCameraRig.TwoShotNarrowCategoryFieldOfView
                : Gamesim.House.HouseCameraRig.TwoShotFieldOfView;
            Assert.That(cameraRig.DesiredFieldOfView, Is.EqualTo(expectedFieldOfView),
                "Root and wide frames retain the authored lens; only a narrow category widens it.");
        }

        private void AssertConversationFacesInsideCapture(Texture2D frame, string listenerId)
        {
            var npc = SceneComponents<Gamesim.House.HouseNpc>().Single(actor => actor.Id == listenerId && actor.gameObject.activeInHierarchy);
            var column = ActiveRect(EpisodeHud.ConversationColumnName);
            var stage = ActiveRect("Episode panel");
            Assert.That(stage, Is.Not.Null);
            const float margin = 12f;
            float left = (column != null ? ScreenBox(column).xMax : ScreenBox(stage).xMin) + margin;
            var brand = ActiveRect("Brand");
            float top = brand != null ? ScreenBox(brand).yMin - margin : frame.height - margin;
            var heads = new System.Collections.Generic.List<(Transform Actor, Rect Face, float MinimumDepth, Vector3 AnchorViewport)>();
            foreach (var actor in new[] { player.transform, npc.transform })
            {
                Bounds bounds = ConversationRenderedHeadBounds(actor);
                var points = new System.Collections.Generic.List<Vector3>();
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var projected = cameraRig.ViewCamera.WorldToViewportPoint(world);
                    points.Add(new Vector3(projected.x * frame.width, projected.y * frame.height, projected.z));
                }
                var face = Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
                var presentation = actor.GetComponent<CharacterPresentation>();
                var anchor = presentation.HeadBone ?? presentation.VisualRoot.Find("Chest/Head pivot");
                Assert.That(anchor, Is.Not.Null, actor.name + "'s ready body has its actual head anchor.");
                heads.Add((actor, face, points.Min(p => p.z), cameraRig.ViewCamera.WorldToViewportPoint(anchor.position)));
            }
            Debug.Log("[Gamesim] conversation full-head framing: category=" + (director.CurrentConversationCategory ?? "Topics")
                + ", frame=" + frame.width + "x" + frame.height + ", fov=" + cameraRig.ViewCamera.fieldOfView.ToString("R")
                + ", targetFov=" + cameraRig.DesiredFieldOfView.ToString("R") + ", offset=" + cameraRig.ConversationWindowOffset.ToString("R")
                + ", requiredLeft=" + left.ToString("R") + ", requiredRight=" + (frame.width - margin).ToString("R")
                + ", actualEye=" + cameraRig.ViewCamera.transform.position + ", desiredFocus=" + cameraRig.DesiredFocus
                + ", boom=" + cameraRig.AppliedDistance.ToString("R") + ", heads="
                + string.Join("; ", heads.Select(h => h.Actor.name + " (xMin=" + h.Face.xMin.ToString("R")
                    + ",xMax=" + h.Face.xMax.ToString("R") + ",yMin=" + h.Face.yMin.ToString("R")
                    + ",yMax=" + h.Face.yMax.ToString("R") + ",minDepth=" + h.MinimumDepth.ToString("R")
                    + ",anchorX=" + h.AnchorViewport.x.ToString("R") + ",anchorDepth=" + h.AnchorViewport.z.ToString("R") + ")")));
            if (cameraRig.ConversationWindowOffset > 0f && cameraRig.ViewCamera.aspect < 1.5f)
            {
                float actualMean = heads.Average(h => h.AnchorViewport.x);
                float publishedMean = .5f + cameraRig.ConversationWindowOffset / (2f * cameraRig.ViewCamera.aspect);
                float centeringErrorPixels = Mathf.Abs(actualMean - publishedMean) * frame.width;
                Debug.Log("[Gamesim] conversation head-plane centering: actual=" + actualMean.ToString("R")
                    + ", published=" + publishedMean.ToString("R") + ", pixelError=" + centeringErrorPixels.ToString("R"));
                Assert.That(centeringErrorPixels, Is.LessThanOrEqualTo(2f),
                    "Both real head anchors project around the published actor-window center after the camera settles.");
            }
            foreach (var head in heads)
            {
                var actor = head.Actor;
                var face = head.Face;
                string where = actor.name + " head " + face + " in " + frame.width + "x" + frame.height;
                Assert.That(head.MinimumDepth, Is.GreaterThan(0f), actor.name + "'s rendered head is in front of the capture camera.");
                Assert.That(face.xMin, Is.GreaterThanOrEqualTo(left), where + ": the full face clears the menu and its margin.");
                Assert.That(face.xMax, Is.LessThanOrEqualTo(frame.width - margin), where + ": the full face clears the right edge.");
                Assert.That(face.yMin, Is.GreaterThanOrEqualTo(margin), where + ": the face clears the capture floor.");
                Assert.That(face.yMax, Is.LessThanOrEqualTo(top), where + ": the face clears the top HUD.");
                var context = ActiveRect(EpisodeHud.ConversationTopicsContextName);
                if (context != null)
                {
                    var box = ScreenBox(context);
                    box = Rect.MinMaxRect(box.xMin - margin, box.yMin - margin, box.xMax + margin, box.yMax + margin);
                    Assert.That(face.Overlaps(box), Is.False, where + ": the face clears the context card and its margin.");
                }
            }
        }

        private static Bounds ConversationRenderedHeadBounds(Transform actor)
        {
            var presentation = actor.GetComponent<CharacterPresentation>();
            Assert.That(presentation, Is.Not.Null, actor.name + " has a real character presentation.");
            var root = presentation.VisualRoot;
            Assert.That(root, Is.Not.Null, actor.name + "'s current body is ready.");
            // The UMA-free body has an actual Face sphere. Its live renderer follows the visual
            // seat/pose hierarchy; a fabricated root + height would miss that geometry.
            var primitiveFace = root.GetComponentsInChildren<MeshRenderer>()
                .FirstOrDefault(r => r.enabled && r.gameObject.activeInHierarchy && r.name == "Face"
                    && r.transform.parent != null && r.transform.parent.name == "Head pivot");
            if (primitiveFace != null) return primitiveFace.bounds;

            var head = presentation.HeadBone;
            Assert.That(head, Is.Not.Null, actor.name + "'s provided body has resolved its actual head bone.");
            Bounds result = default;
            bool found = false;
            void Add(Vector3 point)
            {
                if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                else result.Encapsulate(point);
            }
            foreach (var renderer in head.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Add(renderer.bounds.min); Add(renderer.bounds.max);
            }
            // Provided bodies commonly use one skinned mesh for the whole person. Select its
            // actual posed head-weighted vertices so model width and customization are measured.
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sharedMesh == null) continue;
                var bones = renderer.bones;
                var selected = bones.Select(bone => bone != null && (bone == head || bone.IsChildOf(head))).ToArray();
                var weights = renderer.sharedMesh.boneWeights;
                if (weights.Length != renderer.sharedMesh.vertexCount) continue;
                bool HeadWeight(int index, float weight) => index >= 0 && index < selected.Length && selected[index] && weight >= .2f;
                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    var vertices = baked.vertices;
                    for (int i = 0; i < Mathf.Min(vertices.Length, weights.Length); i++)
                    {
                        var weight = weights[i];
                        if (HeadWeight(weight.boneIndex0, weight.weight0) || HeadWeight(weight.boneIndex1, weight.weight1)
                            || HeadWeight(weight.boneIndex2, weight.weight2) || HeadWeight(weight.boneIndex3, weight.weight3))
                            Add(renderer.transform.TransformPoint(vertices[i]));
                    }
                }
                finally { UnityEngine.Object.Destroy(baked); }
            }
            Assert.That(found, Is.True, actor.name + "'s current head has visible geometry; a center point alone is insufficient.");
            Assert.That(result.size.x, Is.GreaterThan(.01f), actor.name + "'s real head has nonzero width.");
            return result;
        }
    }
}
