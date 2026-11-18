using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 2, part d: one way out on the decisions, the finale, the jury house, the season
    /// report and a conversation's pages, drawn by the shared helper (WayOut) where every way out
    /// sits - a page's way back at the top left of the panel's band (or the conversation's column),
    /// a card's Close at its top right - and Escape doing what that control does.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The docked panel's own Close, pressable: the one way out of a card over the house. Found by
        /// what it is - a button of the panel itself, the shared helper's Close or captioned Close
        /// (<see cref="WayOut.AddClose"/>, with the key beside the caption and never in it).
        /// </summary>
        private Button PanelCloseButton(bool pressable = true)
        {
            var close = director.GetComponentsInChildren<Button>(true).LastOrDefault(button => button.IsActive()
                && button.transform.parent != null && button.transform.parent.name == "Episode panel"
                && (button.name == WayOut.CloseObjectName || button.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Any(text => text.text == WayOut.CloseCaption)));
            Assert.That(close, Is.Not.Null, "The panel has its Close.");
            if (pressable) AssertPressable(close, WayOut.CloseCaption);
            return close;
        }

        /// <summary>A page's way back as the shared helper draws it: its name, its words, its key beside them and its corner.</summary>
        private void AssertAPageWayBack(Button back, string caption, string where)
        {
            Assert.That(back, Is.Not.Null, where + ": the page has its way back.");
            Assert.That(back.name, Is.EqualTo(WayOut.BackObjectName), where + ": drawn by the shared helper.");
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo(caption), where + ": its words name the page it goes back to.");
            Assert.That(WayOut.IsWayBack(caption), Is.True, where + ": worded 'Back to <place>'.");
            Assert.That(WayOut.KeyHintOf(back), Is.EqualTo(EpisodeHud.DecisionBackKey), where + ": the key beside the words, never in them.");
            var rect = (RectTransform)back.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), where + ": at the top left.");
            Assert.That(rect.GetComponentInParent<ScrollRect>(), Is.Null, where + ": outside the scroll, so it never scrolls away.");
            Assert.That(back.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None), where + ": in the keyboard's and the pad's ring.");
            Assert.That(WayOut.CaptionSizes(back).y, Is.GreaterThanOrEqualTo(WayOut.MinFontSize), where + ": no label under 14.");
        }

        /// <summary>
        /// The jury's questions are a page over the finale: their one way back stands in the band at
        /// the top left - no longer pinned in the footer - and Escape goes back a page, as the control
        /// does, where it used to close the whole panel.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBack_TheJurysQuestionsGoBackFromTheBandAndByEscape()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return OpenFinalePanel();
            int revision = director.Snapshot.revision;
            FinaleControl(EpisodeDirector.JuryQuestionsCaption).onClick.Invoke();
            yield return Frames(2);
            var back = Hud.DecisionPageBackControl;
            AssertAPageWayBack(back, EpisodeDirector.BackToFinaleCaption, "The jury's questions");
            Assert.That(back.transform.parent.name, Is.EqualTo(EpisodeHud.DecisionBandName), "In the panel's band.");
            Assert.That(FinaleControlCount(EpisodeDirector.BackToFinaleCaption), Is.EqualTo(1), "One way back, not a second in the footer.");

            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(director.IsPhasePanelOpen, Is.True, "Escape goes back a page: the finale panel stays open,");
            Assert.That(FinalePart(EpisodeHud.FinalePageName), Is.Not.Null, "with the finale page back,");
            Assert.That(Hud.DecisionPageBackControl, Is.Null, "and no way back on it.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Nothing is committed.");
            director.ClosePanels();
        }

        /// <summary>
        /// The season report's Close is the card's way out, not one of its ways on: one Close, drawn by
        /// the shared helper at the card's top right, outside the footer, which keeps the ways on.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBack_TheReportsCloseStandsInItsTopRightCorner()
        {
            var went = new List<string>();
            Report().Show(Finished(), _ => null, () => went.Add("review"), null,
                () => went.Add("new season"), () => went.Add("menu"), () => went.Add("closed"));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var closes = ReportButtons(SeasonReport.CloseCaption);
            Assert.That(closes, Has.Length.EqualTo(1), "One Close.");
            var close = closes[0];
            Assert.That(close.name, Is.EqualTo(WayOut.CloseObjectName), "Drawn by the shared helper.");
            Assert.That(WayOut.KeyHintOf(close), Is.EqualTo(SeasonReport.CloseKeyHint), "Escape beside the caption, not in it.");
            var rect = (RectTransform)close.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one), "At the card's top right.");
            var footer = Report().GetComponentsInChildren<RectTransform>().Last(part => part.name == SeasonReport.FooterName);
            Assert.That(rect.IsChildOf(footer), Is.False, "Not among the ways on.");
            Assert.That(footer.GetComponentsInChildren<Button>(), Has.Length.EqualTo(3), "The footer keeps the three ways on,");
            foreach (string way in new[] { SeasonReport.NewSeasonCaption, SeasonReport.ReviewCaption, SeasonReport.MainMenuCaption })
                Assert.That(ReportButtons(way).Single().transform.IsChildOf(footer), Is.True, way + " among them.");
            close.onClick.Invoke();
            Assert.That(went, Is.EqualTo(new[] { "closed" }));
            Assert.That(Report().IsShowing, Is.False);
        }

        /// <summary>
        /// The jury house over the window at three has one way out, the way back to the window in the
        /// band, where it had a last row "Leave the jury house" beside the panel's Close; Escape takes it.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBack_TheJuryHouseGoesBackToTheWindowByItsOneWayBack()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(
                state => state.phase == EpisodePhase.Social && state.Active.Count() == 3
                    && state.Active.Any(actor => actor.isPlayer) && state.pendingDiary == null,
                "the player in the window at three");
            yield return PutAwayTheCards();
            yield return OpenFreeTime();
            ButtonWithCaption(EpisodeDirector.JuryHouseCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.InJuryHouse, Is.True);
            int revision = director.Snapshot.revision;
            AssertAPageWayBack(Hud.DecisionPageBackControl, EpisodeDirector.BackToPreparationCaption, "The jury house at three");
            Assert.That(director.GetComponentsInChildren<Button>().Count(button => button.IsActive()
                && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == "Leave the jury house")), Is.EqualTo(0),
                "No second way out of its own.");

            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(director.InJuryHouse, Is.False, "Escape leaves the jury house,");
            Assert.That(director.IsPhasePanelOpen, Is.True, "back into the window, which stays open.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Nothing is committed.");
            director.ClosePanels();
        }

        /// <summary>
        /// A conversation's topic page: Back to topics is the column's way back at its top left, out of
        /// the rows it used to close, and still one level up.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBack_BackToTopicsIsTheColumnsWayBack()
        {
            yield return InstallTalkingHouse(8, false);
            yield return TalkTo(Listener(director.Snapshot).id);
            yield return EnterConversationCategory(EpisodeDirector.LearnCategoryCaption);
            var back = Hud.DecisionPageBackControl;
            AssertAPageWayBack(back, EpisodeDirector.ConversationTopicsBackCaption, "A topic's page");
            Assert.That(back.transform.parent.name, Is.EqualTo(EpisodeHud.ConversationColumnName), "In the conversation's column.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(back.gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            Assert.That(director.GetComponent<HouseAudio>().LastCue, Is.EqualTo(HouseAudio.Cue.Hover),
                "The pointer arriving over a page's way back ticks, as over every button of the HUD.");
            back.onClick.Invoke();
            yield return null;
            Assert.That(director.CurrentConversationCategory, Is.Null, "One level up: the topics.");
            Assert.That(director.IsConversationOpen, Is.True, "The conversation stays open.");
            director.ClosePanels();
        }

        /// <summary>
        /// With a verb's people open on a topic's page, the page's one way back names that level - "Back to the
        /// choices", not "Back to topics" with an Escape beside it that only shut the people - and Escape and the
        /// control both go back that one level. Pressed from the keyboard, the way back lets the keyboard go to
        /// the page it lands on, not to that page's own way back, where a second Enter would go back again.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBack_APickersWayBackAndEscapeGoBackOneLevel()
        {
            yield return InstallTalkingHouse(8, false);
            yield return TalkTo(Listener(director.Snapshot).id);
            yield return PressRow(EpisodeDirector.VentPickerCaption);
            Assert.That(director.ConversationPicker, Is.EqualTo(EpisodeDirector.VentPickerCaption));
            AssertAPageWayBack(Hud.DecisionPageBackControl, EpisodeDirector.ConversationChoicesBackCaption, "A verb's people");

            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(director.ConversationPicker, Is.Null, "Escape shuts the people, as the way back says,");
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.PressureCategoryCaption), "on the same page.");
            AssertAPageWayBack(Hud.DecisionPageBackControl, EpisodeDirector.ConversationTopicsBackCaption, "The page again");

            yield return PressRow(EpisodeDirector.VentPickerCaption);
            yield return KeyboardSubmit(EpisodeDirector.ConversationChoicesBackCaption);
            yield return Frames(2);
            Assert.That(director.ConversationPicker, Is.Null, "The control does what Escape does,");
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.PressureCategoryCaption), "one level.");
            var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.That(selected == null || selected.name != WayOut.BackObjectName, Is.True,
                "The keyboard is not handed to the page's way back: a second Enter would leave the page too.");
            director.ClosePanels();
        }
    }
}
