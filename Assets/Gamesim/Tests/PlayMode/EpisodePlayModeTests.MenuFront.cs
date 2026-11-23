using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The menu review's quick wins at the front door and in Settings (part b, items 13 to 17), on
    /// the built screens: Settings in plain words with "offline" said once, one section heading
    /// style there, the cast screen's own page of houseguests with no "slot" on it, the tour's Back,
    /// "Skip the tour" and replay note - and the Settings control the note points to - and one
    /// brand mark on every screen that shows one.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The words Settings used to say that a player cannot be expected to know.</summary>
        private static readonly string[] DeveloperWords = { "validated", "receipt", "cloud", "AI dialogue", "credentials", "snapshot" };

        /// <summary>
        /// The section headings each section of the Settings frame shows (menu slice 4), in order,
        /// each in the one style: the section's own, then the blocks under it.
        /// </summary>
        private static readonly (SettingsSection section, string[] headings)[] SettingsSections =
        {
            (SettingsSection.Saves, new[] { "SAVES", "IMPORT A WEB SAVE" }),
            (SettingsSection.Sound, new[] { "SOUND" }),
            (SettingsSection.Display, new[] { "DISPLAY" }),
            (SettingsSection.Accessibility, new[] { "ACCESSIBILITY", "CEREMONIES" }),
            (SettingsSection.Controls, new[] { "CONTROLS", "CAMERA" }),
            (SettingsSection.Advanced, new[] { "ADVANCED", "YOUR CAREER" }),
        };

        /// <summary>The Settings frame on <paramref name="section"/>, as the game menu's Settings row and the list open it.</summary>
        private IEnumerator OpenSettingsForMenuFront(SettingsSection section = SettingsSection.Saves)
        {
            director.OpenSettingsAt(section);
            yield return null; yield return null;
        }

        /// <summary>What is on screen under the director now, the status line aside: it carries the save store's own words.</summary>
        private string[] MenuFrontTexts() =>
            director.GetComponentsInChildren<TMP_Text>().Where(label => label.gameObject.activeInHierarchy
                && label.text != director.StatusMessage).Select(label => label.text).ToArray();

        [UnityTest]
        public IEnumerator MenuFront_SettingsSayItPlainly_AndSayOfflineOnce()
        {
            // Every section of the frame (menu slice 4), each in plain words and saying "offline" once.
            foreach (var section in SettingsWords.Sections)
            {
                yield return OpenSettingsForMenuFront(section);
                var texts = MenuFrontTexts();
                foreach (var word in DeveloperWords)
                    Assert.That(texts.Where(text => text.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty,
                        section + ": Settings no longer say \"" + word + "\".");
                var offline = texts.Where(text => text.IndexOf("offline", System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                Assert.That(offline, Has.Length.EqualTo(1), section + ": that the game is offline is said once, in one place.");
                Assert.That(offline[0], Is.EqualTo(EpisodeDirector.SettingsEyebrowCopy).Or.EqualTo(EpisodeDirector.PausedEyebrow),
                    "The one place is the eyebrow over the title.");
                // The plain finder: with no backup yet the row is on screen but greyed with its reason (the settings and
                // saves leftovers), and ButtonWithCaption refuses a control that cannot be pressed.
                if (section == SettingsSection.Advanced) Assert.That(FindButton(EpisodeDirector.RecoverBackupCaption), Is.Not.Null);
                if (section == SettingsSection.Saves) Assert.That(ButtonWithCaption(EpisodeDirector.NewSeasonInNewSlotCaption), Is.Not.Null);
            }
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuFront_EverySettingsSectionHasTheOneHeadingStyle()
        {
            foreach (var (section, headings) in SettingsSections)
            {
                yield return OpenSettingsForMenuFront(section);
                Canvas.ForceUpdateCanvases();
                var boxes = director.GetComponentsInChildren<RectTransform>()
                    .Where(rect => rect.name == EpisodeHud.SectionHeadingName && rect.gameObject.activeInHierarchy).ToArray();
                var names = boxes.Select(box => box.GetComponentInChildren<TMP_Text>().text).ToArray();
                Assert.That(names, Is.EqualTo(headings), section + ": every block of the section under the one heading, in order.");
                foreach (var box in boxes)
                {
                    var label = box.GetComponentInChildren<TMP_Text>();
                    Assert.That(label.color, Is.EqualTo(UiTheme.Heading), label.text + " is in the theme's heading blue.");
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(14f), label.text + ": no label under 14 px.");
                    Assert.That((label.fontStyle & FontStyles.UpperCase) != 0, Is.True, label.text + " is set in capitals.");
                    Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f),
                        label.text + ": a box at least 1.3 times its type.");
                    Assert.That(box.Find(EpisodeHud.SectionRuleName), Is.Not.Null, label.text + " has its rule under it.");
                }
                // "Display" is a section's name in the list now; as a heading in the rows it was another style.
                var rows = director.GetComponentsInChildren<TMP_Text>().Where(label => label.gameObject.activeInHierarchy
                    && label.GetComponentInParent<RectTransform>() != null
                    && !label.transform.GetComponentsInParent<RectTransform>().Any(parent => parent.name == EpisodeHud.SettingsSectionsName))
                    .Select(label => label.text).ToArray();
                foreach (var old in new[] { "Display", "Import a supported web save" })
                    Assert.That(rows, Does.Not.Contain(old), "\"" + old + "\" was a heading in another style.");
            }
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuFront_ReplayTheTourFromSettings()
        {
            var tour = DirectorTour();
            if (tour.IsShowing) tour.Skip();
            // The tour is replayed from the Controls section (menu slice 4).
            yield return OpenSettingsForMenuFront(SettingsSection.Controls);
            ButtonWithCaption(EpisodeDirector.ReplayTourCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.IsPanelOpen, Is.False, "Settings close for the tour.");
            Assert.That(tour.IsShowing, Is.True, "The tour plays again.");
            Assert.That(tour.StepIndex, Is.EqualTo(0), "From its first step.");
            Assert.That(TourText(tour, HouseTutorial.ReplayNoteName), Is.EqualTo(HouseTutorial.ReplayNote),
                "The card says where the tour can be replayed, and that is true.");
            Assert.That(HouseTutorial.ReplayNote, Does.Contain("Settings"));
            tour.Skip();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuFront_TourHasBackAndSkipTheTour()
        {
            var tour = DirectorTour();
            tour.Show(TourChrome);
            yield return TourFrames(2);
            try
            {
                Assert.That(TourButton(tour, HouseTutorial.PreviousCaption), Is.Null, "No step before the first.");
                Assert.That(TourButton(tour, "Skip the tour"), Is.Not.Null);
                var note = tour.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == HouseTutorial.ReplayNoteName);
                Assert.That(note.fontSize, Is.GreaterThanOrEqualTo(14f), "No label under 14 px.");
                Assert.That(note.rectTransform.rect.height, Is.GreaterThanOrEqualTo(note.fontSize * 1.3f - .5f), "The note's box is at least 1.3 times its type.");

                int sounds = 0;
                var before = tour.StepSound;
                tour.StepSound = () => sounds++;
                TourButton(tour, "Next").onClick.Invoke();
                yield return null;
                Assert.That(tour.StepIndex, Is.EqualTo(1));
                string second = TourText(tour, "Title");
                TourButton(tour, "Next").onClick.Invoke();
                yield return null;
                Assert.That(tour.StepIndex, Is.EqualTo(2));

                TourButton(tour, HouseTutorial.PreviousCaption).onClick.Invoke();
                yield return null;
                Assert.That(tour.StepIndex, Is.EqualTo(1), "Back goes to the step before.");
                Assert.That(TourText(tour, "Title"), Is.EqualTo(second));
                Assert.That(sounds, Is.EqualTo(2), "Only Next plays the step sound.");
                TourButton(tour, HouseTutorial.PreviousCaption).onClick.Invoke();
                yield return null;
                Assert.That(tour.StepIndex, Is.EqualTo(0));
                Assert.That(TourButton(tour, HouseTutorial.PreviousCaption), Is.Null, "Previous goes once the first step is back.");
                tour.StepSound = before;

                TourButton(tour, "Skip the tour").onClick.Invoke();
                Assert.That(tour.IsShowing, Is.False, "Skip the tour leaves it.");
            }
            finally
            {
                if (tour.IsShowing) tour.Skip();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuFront_TheSeasonsHouseguestsPageNeverSaysSlot()
        {
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = "Menu guest";
            var profile = CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), draft);
            Assert.That(CastScreen().ProfileStore.Save(profile, out var profileError), Is.True, profileError);

            yield return OpenCastScreen();
            var tab = CastButtons(CastSelect.SeasonHouseguestsCaption).Single();
            tab.onClick.Invoke();
            yield return null;
            CastButtons("Add Menu guest to the cast")[0].onClick.Invoke();
            yield return null;

            Assert.That(CastButtons(CastSelect.EditSeasonHouseguestCaption(1)), Has.Length.EqualTo(1));
            Assert.That(CastButtons(CastSelect.RemoveSeasonHouseguestCaption(1)), Has.Length.EqualTo(1));
            var texts = CastScreen().GetComponentsInChildren<TMP_Text>()
                .Where(label => label.gameObject.activeInHierarchy).Select(label => label.text).ToArray();
            Assert.That(texts, Does.Contain("1. Menu guest"));
            Assert.That(texts.Any(text => text.StartsWith(CastSelect.SeasonHouseguestsCaption.ToUpperInvariant())), Is.True,
                "The page's title is its tab's name.");
            var slot = new Regex(@"\bslots?\b", RegexOptions.IgnoreCase);
            Assert.That(texts.Where(text => slot.IsMatch(text)), Is.Empty, "\"Slot\" means a save, never a place in the cast.");

            // The tab's whole name at a readable size: it shrinks to fit, never under 14 px.
            Canvas.ForceUpdateCanvases();
            var tabLabel = CastButtons(CastSelect.SeasonHouseguestsCaption).Single().GetComponentInChildren<TMP_Text>();
            tabLabel.ForceMeshUpdate();
            Assert.That(tabLabel.fontSize, Is.GreaterThanOrEqualTo(14f), "The tab's name is not shrunk under 14 px.");
            CastScreen().Dismiss();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuFront_TheTitleCardShowsTheOneBrandMark()
        {
            Assert.That(BrandMark.Current, Is.EqualTo(BrandMark.Kind.House), "The house mark, the one most screens drew, until the owner says otherwise.");
            Assert.That(BrandMark.Sprite(), Is.Not.Null, "The mark's picture ships with the game.");
            var season = SequenceSeason();
            var sequence = Opening();
            sequence.Play(new string[0], SequencePlan(new System.Collections.Generic.List<string>(), season));
            yield return null;
            var emblem = SequenceNode(sequence, OpeningSequence.BrandEmblemName);
            Assert.That(emblem, Is.Not.Null, "The title card stands the brand mark over its title.");
            Assert.That(SequenceNode(sequence, "Eye emblem"), Is.Null, "The eye was this card's alone.");
            var mark = emblem.Find(OpeningSequence.BrandEmblemMarkName);
            Assert.That(mark, Is.Not.Null);
            Assert.That(mark.GetComponent<Image>().sprite, Is.SameAs(BrandMark.Sprite()), "The same mark as every other screen.");
            sequence.Skip();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuFront_TheHudAndTheCastScreenShowTheOneBrandMark()
        {
            yield return null;
            var hudMarks = director.GetComponentsInChildren<Image>()
                .Where(image => image.name == "Brand mark" && image.gameObject.activeInHierarchy).ToArray();
            Assert.That(hudMarks, Is.Not.Empty, "The HUD's top bar carries the brand.");
            Assert.That(hudMarks.Select(image => image.sprite), Has.All.SameAs(BrandMark.Sprite()));

            yield return OpenCastScreen();
            var castMarks = CastScreen().GetComponentsInChildren<Image>()
                .Where(image => image.name == "Brand mark" && image.gameObject.activeInHierarchy).ToArray();
            Assert.That(castMarks, Is.Not.Empty, "The cast screen carries the brand in its corner.");
            Assert.That(castMarks.Select(image => image.sprite), Has.All.SameAs(BrandMark.Sprite()));
            CastScreen().Dismiss();
            yield return null;
        }
    }
}
