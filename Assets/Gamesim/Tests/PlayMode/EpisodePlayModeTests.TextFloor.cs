using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The text floor (PLAN A, A8): no text the player can see on the owner's 1920x1080 frame is drawn
    /// under 12 pixels at the standard text size - the size every larger one only grows from. Measured,
    /// not reasoned: each label's size as drawn - after its auto-sizing has shrunk it to its box - times
    /// its canvas's scale on that frame (1.2 for the house's 1600x900 canvases, 1 for the front door's
    /// 1920x1080 ones), on the house's chrome, the controls card, every panel the clip sweep opens,
    /// the front door's menu and a ceremony's strip.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The floor, in pixels on the 1920x1080 frame.</summary>
        private const float TextFloorPixels = 12f;

        [UnityTest, Timeout(600000)]
        public IEnumerator Accessibility_NoVisibleTextUnderTheFloor()
        {
            if (!Application.isBatchMode) yield break;
            HoldTheHouseForTheFixture();
            yield return ApplyTextSize(TextSize.Standard);
            Assert.That(director.TextSize, Is.EqualTo(TextSize.Standard));
            yield return SettleCast();

            var surfaces = new (string Name, Func<bool> Open, Action Close)[]
            {
                ("the house", () => { director.ClosePanels(); return !director.IsPanelOpen; }, null),
                // Pressed where it stands on the frame's canvas: under the lens the batch's own screen is not the frame.
                ("the controls card", () =>
                {
                    director.ClosePanels();
                    FindButton(ExpandControlsCaption).onClick.Invoke();
                    return ShowsControl(CollapseControlsCaption);
                }, () => { if (ShowsControl(CollapseControlsCaption)) FindButton(CollapseControlsCaption).onClick.Invoke(); }),
                ("the notebook", () => { director.OpenJournal(); return director.IsPanelOpen; }, null),
                ("who is where", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Rooms); return director.IsPanelOpen; }, null),
                ("the houseguests", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.People); return director.IsPanelOpen; }, null),
                ("a houseguest profile", () =>
                {
                    var someone = director.Snapshot.contestants.FirstOrDefault(c => !c.isPlayer);
                    if (someone == null) return false;
                    director.ShowHouseguestProfile(someone.id);
                    return director.ProfileId == someone.id;
                }, null),
                ("the vote", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes); return director.IsPanelOpen; }, null),
                ("the alliances", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances); return director.IsPanelOpen; }, null),
                ("your word", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Word); return director.IsPanelOpen; }, null),
                ("the relationships", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network); return director.IsPanelOpen; }, null),
                ("the episode screen", () => { WarpPlayer(director.StationPosition); return director.TryOpenPhasePanel(); }, null),
                ("the settings", () => { director.OpenSettings(); return director.IsPanelOpen; }, null),
                ("house activities", () => { director.OpenHouseActivities(); return director.IsHouseActivityOpen; }, null),
                ("a conversation", () =>
                {
                    var npc = SceneComponents<HouseNpc>().FirstOrDefault(actor => actor.gameObject.activeInHierarchy);
                    if (npc == null) return false;
                    WarpPlayer(npc.transform.position + (player.transform.position - npc.transform.position).normalized * 1.4f);
                    return director.TryOpenNpc(npc.Id);
                }, null),
                ("a ceremony's strip", () =>
                {
                    director.ClosePanels();
                    var sting = SceneComponents<CeremonySting>().Single();
                    sting.Play(CeremonySting.NominationKind, "Maya Hassan has nominated Riley Johnson and Jamie Roberts for eviction.", true);
                    sting.Held = true;
                    return sting.IsPlaying;
                }, () => SceneComponents<CeremonySting>().Single().Cancel()),
                ("the main menu", () => { director.OpenMainMenu(); return SceneComponents<MainMenu>().Any(menu => menu.IsShowing); },
                    () => director.CloseMainMenu()),
            };

            var under = new List<string>();
            var unreached = new List<string>();
            var lens = new CaptureLens(cameraRig.ViewCamera, 1920, 1080);
            try
            {
                yield return null; yield return null;
                foreach (var surface in surfaces)
                {
                    director.ClosePanels();
                    yield return null;
                    if (!surface.Open()) { unreached.Add(surface.Name); continue; }
                    // The HUD laid out for the frame, and a moment for anything that lands late.
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    RenderHudForTheCurrentCanvas();
                    float settle = Time.realtimeSinceStartup + 1f;
                    while (Time.realtimeSinceStartup < settle) yield return null;
                    Canvas.ForceUpdateCanvases();
                    under.AddRange(TextUnderTheFloor().Select(line => surface.Name + ": " + line));
                    surface.Close?.Invoke();
                    yield return null;
                }
            }
            finally { lens.Dispose(); }
            director.ClosePanels();
            yield return null;

            Assert.That(unreached, Is.Empty, "These surfaces never opened, so the floor says nothing about them: " + string.Join(", ", unreached));
            Assert.That(under.Distinct().ToArray(), Is.Empty,
                "Text drawn under " + TextFloorPixels + " pixels on the 1920x1080 frame at the standard size: " + string.Join(" | ", under.Distinct()));
        }

        /// <summary>
        /// The largest text (the lead's decision A-3), chosen by its own control, reaches every
        /// surface the larger text does and the ones that ignored the size: the HUD and every card at
        /// 1.4, the names over the houseguests' heads, and the witnessed conversation's line, which
        /// was clamped to the larger size. The settings then offer the other two sizes by the words
        /// they always had, and the standard size takes everything back.
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_TheLargestTextReachesEverySurface()
        {
            HoldTheHouseForTheFixture();
            director.OpenSettings();
            yield return null; yield return null;
            Assert.That(ShowsControl(TextSizes.LargerCaption) && ShowsControl(TextSizes.LargestCaption), Is.True,
                "At the standard size the settings offer the larger and the largest.");
            Assert.That(ShowsControl(TextSizes.StandardCaption), Is.False, "and not the size in use.");
            ButtonWithCaption(TextSizes.LargestCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.TextSize, Is.EqualTo(TextSize.Largest));
            Assert.That(director.LargeText, Is.True, "The largest is larger text, to anything that asks the old question.");
            Assert.That(ShowsControl(TextSizes.StandardCaption) && ShowsControl(TextSizes.LargerCaption), Is.True,
                "At the largest the settings offer the standard and the larger, by the words they always had.");
            Assert.That(ShowsControl(TextSizes.LargestCaption), Is.False);
            director.ClosePanels();
            yield return null; yield return null;

            Assert.That(director.GetComponentInChildren<EpisodeHud>().FontScale, Is.EqualTo(1.4f).Within(1e-4f), "The HUD.");
            Assert.That(SceneComponents<CeremonySting>().Single().FontScale, Is.EqualTo(1.4f).Within(1e-4f), "The ceremony's strip.");
            Assert.That(SceneComponents<CeremonyTakeover>().Single().FontScale, Is.EqualTo(1.4f).Within(1e-4f), "The ceremony's card.");
            Assert.That(HouseNpc.NameScale, Is.EqualTo(1.4f).Within(1e-4f), "The names over the houseguests' heads.");
            var plates = director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .Where(rect => rect.name == HouseNpc.PlateName).ToList();
            Assert.That(plates, Is.Not.Empty, "The houseguests wear their plates.");
            yield return null;
            foreach (var plate in plates)
                Assert.That(plate.localScale.x, Is.EqualTo(0.0055f * 1.4f).Within(1e-6f), "Every plate grows with the text: " + HierarchyPath(plate));

            // The witnessed conversation's line, clamped at the larger size until now.
            var caption = SceneComponents<HouseConversationCaption>().Single();
            caption.Show("Maya Hassan", "Riley Johnson", "strategy", director.TextScale);
            yield return null;
            var line = caption.GetComponentsInChildren<TMP_Text>(true).First(label => label.text == caption.CurrentText);
            Assert.That(line.fontSize, Is.EqualTo(Mathf.RoundToInt(19 * 1.4f)), "The witnessed line at the largest size.");
            caption.Hide();

            director.SetTextSize(TextSize.Standard);
            yield return null; yield return null;
            Assert.That(HouseNpc.NameScale, Is.EqualTo(1f));
            foreach (var plate in plates.Where(plate => plate != null))
                Assert.That(plate.localScale.x, Is.EqualTo(0.0055f).Within(1e-6f), "and back with the standard size.");
        }

        /// <summary>
        /// Every label up on a screen canvas - not a world-space one: a name over a head scales with
        /// the camera's distance - with words in it that are drawn at all, smaller than the floor, as
        /// "'words' 10.8 px in path".
        /// </summary>
        private static IEnumerable<string> TextUnderTheFloor()
        {
            foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text) || label.color.a <= .01f) continue;
                var canvas = label.canvas;
                if (canvas == null) continue;
                var root = canvas.rootCanvas;
                if (root.renderMode == RenderMode.WorldSpace || root.name == CaptureLens.GuardName) continue;
                if (!Drawn(label.transform)) continue;
                label.ForceMeshUpdate();
                if (label.textInfo.characterCount == 0 || !label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(glyph => glyph.isVisible)) continue;
                float nested = root.transform.lossyScale.y > 0f ? label.transform.lossyScale.y / root.transform.lossyScale.y : 1f;
                float pixels = label.fontSize * root.scaleFactor * nested;
                if (pixels + .01f < TextFloorPixels)
                    yield return "'" + Excerpt(label.text) + "' " + pixels.ToString("0.0") + " px (" + label.fontSize.ToString("0.#") + " units) in " + HierarchyPath(label.transform);
            }
        }

        /// <summary>Whether nothing above the label fades it out: every canvas group on the way up lets it show.</summary>
        private static bool Drawn(Transform node)
        {
            for (var at = node; at != null; at = at.parent)
            {
                var group = at.GetComponent<CanvasGroup>();
                if (group != null && group.alpha <= .01f) return false;
                if (group != null && group.ignoreParentGroups) break;
            }
            return true;
        }
    }
}
