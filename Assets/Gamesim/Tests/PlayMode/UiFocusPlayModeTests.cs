using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The control Enter or A presses is told apart from the one under the pointer (HP9). The
    /// keyboard's control wears the accent edge and a 2 px ring outside it; a hovered control only
    /// steps its edge up, short of the accent, and wears no ring. Before, hover and focus lit one
    /// colour, so a pad player whose mouse rested on another control saw two lit controls.
    ///
    /// <para>Every move and every check of a step happen inside one frame (the selection and the
    /// pointer are sent as the events the input module gives a control), so no render can rebuild
    /// the panel between them. The rows are the live ones, found under the open card.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator UiFocus_TheControlEnterPressesWearsTheAccentEdgeAndRing_AndTheHoveredOneDoesNot()
        {
            HoldTheHouseForTheFixture();
            yield return null;

            // A fixed button (the cast card's rows): no hover step of its own, but the keyboard on it is seen.
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            string first = maya.DisplayName.Split(' ')[0];
            Button Chip()
            {
                var rail = director.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == CastRail.RootName);
                return rail.Cast<Transform>().Select(child => child.GetComponent<Button>()).Last(button => button != null && button.name == maya.DisplayName);
            }
            Chip().onClick.Invoke();
            yield return null;
            var menu = ActiveRect(EpisodeHud.CastMenuName);
            Assert.That(menu, Is.Not.Null, "A chip opens its houseguest's card.");
            Button Row(string caption) => menu.GetComponentsInChildren<Button>().Last(button => button.name == caption);
            var talk = Row(EpisodeHud.CastTalkCaption(first));
            var profile = Row(EpisodeHud.CastProfileCaption(first));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(talk.gameObject), "The card takes the keyboard on its first row.");
            AssertFocusLook(talk, "The cast card's first row, with the keyboard on it");
            Assert.That(FocusEdgeOf(profile), Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Resting)), "The next row keeps its resting edge,");
            AssertNoFocusRing(profile, "the next row");
            EventSystem.current.SetSelectedGameObject(profile.gameObject);
            AssertFocusLook(profile, "The next row, once the keyboard moves to it");
            Assert.That(FocusEdgeOf(talk), Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Resting)), "and the first row's edge goes back to rest,");
            AssertNoFocusRing(talk, "the first row once the keyboard leaves it");
            Chip().onClick.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.CastMenuName), Is.Null, "The same chip again closes the card.");

            // A panel's rows (the game menu card): the keyboard on one row, the pointer over another.
            director.OpenSettings();
            yield return null;
            Assert.That(director.IsGameMenuOpen, Is.True, "The house's door opens the game menu card.");
            Button MenuRow(string caption) => director.GetComponentsInChildren<Button>()
                .Last(button => button.name == EpisodeHud.GameMenuRowPrefix + caption);
            var settings = MenuRow(SettingsWords.SettingsCaption);
            var leave = MenuRow(SettingsWords.MainMenuCaption);
            Canvas.ForceUpdateCanvases();
            var size = ((RectTransform)settings.transform).rect.size;
            EventSystem.current.SetSelectedGameObject(settings.gameObject);
            ExecuteEvents.Execute(leave.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Canvas.ForceUpdateCanvases();
            AssertFocusLook(settings, "The game menu's Settings row, with the keyboard on it");
            Assert.That(FocusEdgeOf(leave), Is.EqualTo(UiTheme.HoverEdge),
                "The row under the pointer lifts its edge a step, to the hover edge,");
            Assert.That(FocusEdgeOf(leave), Is.Not.EqualTo(FocusEdgeOf(settings)), "never to the accent the keyboard's row wears,");
            AssertNoFocusRing(leave, "the row under the pointer");
            Assert.That(((RectTransform)settings.transform).rect.size, Is.EqualTo(size), "The ring takes no room: the row keeps its size.");
            Assert.That(settings.name, Is.EqualTo(EpisodeHud.GameMenuRowPrefix + SettingsWords.SettingsCaption), "The name is unchanged,");
            Assert.That(settings.GetComponentsInChildren<TMP_Text>().First().text, Is.EqualTo(SettingsWords.SettingsCaption), "and so is the caption.");

            // The keyboard moves under the pointer: that row is the keyboard's now, and the other rests.
            EventSystem.current.SetSelectedGameObject(leave.gameObject);
            AssertFocusLook(leave, "The Main menu row, once the keyboard moves to it under the pointer");
            Assert.That(FocusEdgeOf(settings), Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Interactive)), "The Settings row is back at its own edge,");
            AssertNoFocusRing(settings, "the Settings row once the keyboard leaves it");
            ExecuteEvents.Execute(leave.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            AssertFocusLook(leave, "The Main menu row after the pointer leaves it");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>The colour of a control's edge: its "Border" child.</summary>
        private static Color FocusEdgeOf(Button control) => control.transform.Find("Border").GetComponent<Image>().color;

        /// <summary>
        /// What the keyboard's control looks like: the accent edge, one ring 4 px outside the rect in
        /// the theme's focus blue, round the edge's corners, taking no click and no room; and the
        /// ground it has at rest, not the pointer's tint.
        /// </summary>
        private static void AssertFocusLook(Button control, string where)
        {
            Assert.That(FocusEdgeOf(control), Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Active)), where + ": the accent edge.");
            var rings = control.GetComponentsInChildren<Image>().Where(image => image.name == UiTheme.FocusRingName).ToArray();
            Assert.That(rings.Length, Is.EqualTo(1), where + ": one ring.");
            var ring = rings[0];
            Assert.That(ring.transform.parent, Is.SameAs(control.transform), where + ": the ring is the control's own.");
            Assert.That(ring.color, Is.EqualTo(UiTheme.FocusRing), where + ": the ring is the focus blue.");
            Assert.That(ring.raycastTarget, Is.False, where + ": the ring takes no click.");
            Assert.That(ring.sprite.name, Is.EqualTo("UiTheme Outline " + (UiTheme.GlassRadius + UiTheme.FocusRingOutset)),
                where + ": a 2 px line round the edge's corners.");
            Assert.That(ring.rectTransform.offsetMin, Is.EqualTo(new Vector2(-UiTheme.FocusRingOutset, -UiTheme.FocusRingOutset)), where + ": outside the rect.");
            Assert.That(ring.rectTransform.offsetMax, Is.EqualTo(new Vector2(UiTheme.FocusRingOutset, UiTheme.FocusRingOutset)), where + ": outside the rect.");
            Assert.That(ring.GetComponent<LayoutElement>().ignoreLayout, Is.True, where + ": the ring takes no room.");
            Assert.That(control.colors.selectedColor, Is.EqualTo(control.colors.normalColor), where + ": no hover tint under focus.");
        }

        private static void AssertNoFocusRing(Button control, string where) =>
            Assert.That(control.GetComponentsInChildren<Image>().Any(image => image.name == UiTheme.FocusRingName), Is.False,
                "No ring on " + where + ".");
    }
}
