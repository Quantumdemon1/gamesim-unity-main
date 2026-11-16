using System;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The menu review's quick wins where the three parts meet: the words left over once the parts
    /// were joined. Each case fails on the words the screens carried before the slice.
    /// </summary>
    public sealed class MenuQuickWinsIntegrationTests
    {
        [Test]
        public void EditingAHouseguestInTheCastSaysHouseguestNotSlot()
        {
            foreach (string words in new[] { CharacterCreator.ApplySlotCaption, CharacterCreator.CancelSlotCaption, CharacterCreator.EditingCastLine })
                Assert.That(words.IndexOf("slot", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1), words);
            Assert.That(CharacterCreator.ApplySlotCaption, Does.Contain("houseguest"));
        }

        [Test]
        public void TheAccessibilityLinePromisesNoModeTheGameLacks()
        {
            // No competition has an untimed or assisted way to play: Settings must not say one does.
            Assert.That(EpisodeDirector.AccessibilityLine, Does.Not.Contain("precision").And.Not.Contain("assisted").And.Not.Contain("untimed"));
            Assert.That(EpisodeDirector.AccessibilityLine, Does.Contain("captioned"));
        }

        [Test]
        public void TheTourNoteSaysWhereItsReplayIs()
        {
            // The replay button is only in the house's own Settings, so the note says so.
            Assert.That(HouseTutorial.ReplayNote, Does.Contain("Settings in the house"));
            Assert.That(HouseTutorial.ReplayNote, Does.Not.Contain("any time"));
        }

        [Test]
        public void TheWeeklyRecapNamesThePlayersDealsAsTheNotesPageDoes()
            => Assert.That(WeeklyRecapScreen.WordTitle, Is.EqualTo(EpisodeDirector.DealsAndPromisesName));

        [Test]
        public void NoAppearanceCategoryButOneWearsAMoodFace()
        {
            // Three smiling faces once stood for Face, Hair and Accessories; unique names alone never caught it.
            var icons = CharacterCreator.AppearanceCategories.ToDictionary(category => category, CharacterCreator.CategoryIcon);
            Assert.That(icons.Values.Count(icon => icon.StartsWith("mood-", StringComparison.Ordinal)), Is.LessThanOrEqualTo(1));
            Assert.That(icons["Face"], Does.Not.StartWith("mood-"));
            Assert.That(icons["Accessories"], Does.Not.StartWith("mood-"));
        }
    }
}
