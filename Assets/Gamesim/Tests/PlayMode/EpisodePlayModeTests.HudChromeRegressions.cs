using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The HUD's chrome after the phase-and-dialogue port (lane S2a, PR-9 to PR-11): the compact HUD's save
    /// confirmation, one write a volume drag, and a veto draw that lays out rather than throws.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// PR-9: the compact HUD says a save was made, as the tour promises, at every text size - in
        /// the top bar's slot the full HUD gives its objective chip, clear of every other piece of
        /// chrome.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator HudStatus_TheCompactHudConfirmsASaveAtEveryTextSize()
        {
            HoldTheHouseForTheFixture();
            try
            {
                foreach (var size in TextSizes.All)
                {
                    yield return ApplyTextSize(size);
                    director.SetCompactHud(true);
                    director.ClosePanels();
                    yield return null;
                    string where = "Compact HUD at " + TextSizes.Name(size) + " text";
                    director.SaveNow();
                    Assert.That(director.StatusMessage, Does.Contain("Saved locally."), where + ": the save succeeded.");
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var chip = ActiveRect(EpisodeHud.SaveFeedbackName);
                    Assert.That(chip, Is.Not.Null, where + ": the save is confirmed on screen.");
                    var label = chip.GetComponentsInChildren<TMP_Text>().Single();
                    Assert.That(label.text, Is.EqualTo("Saved"), where);
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextTruncated || label.isTextOverflowing, Is.False, where + ": the word is drawn whole.");
                    AssertEveryLabelDraws(chip, where);
                    var canvas = (RectTransform)HudCanvas().transform;
                    Assert.That(TourEncloses(TourWorldRect(canvas), TourWorldRect(chip)), Is.True, where + ": inside the frame.");
                    foreach (var name in new[] { "Brand", "Week chip", "House pill", "Objective", "Rail ground", "Navigation", "Status",
                        CastRail.RootName, "Exploration controls", IconRail.RootName })
                    {
                        var other = ActiveRect(name);
                        if (other == null) continue;
                        Assert.That(ScreenRect(chip).Overlaps(ScreenRect(other)), Is.False,
                            where + ": the confirmation " + ScreenRect(chip) + " overlaps '" + name + "' " + ScreenRect(other) + ".");
                    }
                }
            }
            finally
            {
                if (director != null) director.SetCompactHud(false);
            }
            yield return ApplyTextSize(TextSize.Standard);
        }

        /// <summary>
        /// PR-10: a volume drag is heard as it moves and kept once it is let go - one write, not one a
        /// percent. The preference file holds the old share until the pointer comes up.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_AVolumeDragIsHeardLiveAndKeptOnceItIsLetGo()
        {
            yield return OpenIsolatedVolumeSettings();
            const string caption = "Master volume";
            string file = Path.Combine(temporaryDirectory, PreferenceFile.FileName);
            int Stored() => PreferenceFile.Read(file, out _).GetInt("Gamesim.Volume", -1);
            yield return ReachVolumeControl(caption, false);
            var slider = VolumeControl(caption);
            Canvas.ForceUpdateCanvases();
            int storedBefore = Stored();
            float press = storedBefore == 20 ? .3f : .2f;
            int desired = storedBefore == 63 ? 58 : 63;
            var area = (RectTransform)slider.handleRect.parent;
            var canvas = slider.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 Point(float fraction) => RectTransformUtility.WorldToScreenPoint(camera,
                area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, fraction), area.rect.center.y)));
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left, position = Point(press),
                pointerPressRaycast = new RaycastResult { module = slider.GetComponentInParent<GraphicRaycaster>(), gameObject = slider.gameObject },
            };
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            pointer.position = Point(desired / 100f);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            yield return null;
            Assert.That(Mathf.RoundToInt(slider.value), Is.EqualTo(desired), "The drag moved the slider.");
            AssertVolumeReadback(caption, desired);
            Assert.That(Stored(), Is.EqualTo(storedBefore), "Mid-drag the share is heard but not yet written.");
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(VolumeControl(caption), Is.SameAs(slider), "The same control survives the drag.");
            Assert.That(Stored(), Is.EqualTo(desired), "Let go, the share is kept.");
            AssertVolumeReadback(caption, desired);
        }

        /// <summary>
        /// PR-11: the veto draw lays out without a heading it was not given rather than throwing from
        /// inside the render and leaving the HUD half-built.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_TheVetoDrawLaysOutWithoutAMissingHeadingInsteadOfThrowing()
        {
            yield return InstallVetoDraw(48, 8);
            yield return OpenStation();
            yield return null;
            var hud = director.GetComponent<EpisodeHud>();
            var board = VetoDraw.Before(director.Snapshot);
            Assert.That(board.Eligible.Count, Is.GreaterThan(0), "The fixture has a pool.");
            List<EpisodeHud.CeremonyFace> Faces(IEnumerable<string> ids) =>
                ids.Select(id => new EpisodeHud.CeremonyFace(id, null, UiTheme.Muted)).ToList();
            RectTransform row = null;
            Assert.DoesNotThrow(() => row = hud.VetoDrawBoard(Faces(board.ByRight), Faces(board.Eligible), board.ToDraw,
                VetoDraw.PlayingHeading(board), VetoDraw.DrawHeading(board), "", ""),
                "A missing pool heading and chips line are left out, not thrown over.");
            Assert.That(row, Is.Not.Null, "The row is still built.");
            Canvas.ForceUpdateCanvases();
            var words = row.GetComponentsInChildren<TMP_Text>().Select(label => label.text).ToArray();
            Assert.That(words, Does.Contain(VetoDraw.PlayingHeading(board)).And.Contain(VetoDraw.DrawHeading(board)),
                "The headings it was given are drawn.");
            Assert.That(FacesUnder(row), Is.EqualTo(board.ByRight.Count + board.Eligible.Count), "Every face is on the row.");
            AssertEveryLabelDraws(row, "The draw without a pool heading");
        }
    }
}
