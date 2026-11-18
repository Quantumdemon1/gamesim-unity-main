using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The menu review's quick wins on the character creator (part a): the words its controls carry,
    /// and the rules that decide what shows. The built screen is pinned in
    /// MenuQuickWinsCreatorPlayModeTests.
    /// </summary>
    public sealed class MenuQuickWinsCreatorTests
    {
        [Test]
        public void TheWayBackSaysTheDraftIsKept()
        {
            Assert.That(CharacterCreator.BackCaption, Is.EqualTo("Back to the cast (draft kept)"));
        }

        [Test]
        public void TheWayOnNamesTheNextStep()
        {
            Assert.That(CharacterCreator.NextCaption("Identity"), Is.EqualTo("Next: Identity"));
            Assert.That(CharacterCreator.NextCaption("Review"), Is.EqualTo("Next: Review"));
        }

        [Test]
        public void TheNameIsMarkedRequired()
        {
            Assert.That(CharacterCreator.RequiredLabel("Name", true), Is.EqualTo("Name (required)"));
            Assert.That(CharacterCreator.RequiredLabel("Occupation", false), Is.EqualTo("Occupation"), "Only a field the houseguest needs is marked.");
        }

        [Test]
        public void ThePreviewSpeaksOnlyWhenItFailed()
        {
            Assert.That(CharacterCreator.PreviewStatusShows(false), Is.False, "A working or building preview says nothing.");
            Assert.That(CharacterCreator.PreviewStatusShows(true), Is.True, "A failed one says so and offers Retry preview.");
        }

        [Test]
        public void EveryAppearanceCategoryHasItsOwnIcon()
        {
            var icons = CharacterCreator.AppearanceCategories.Select(CharacterCreator.CategoryIcon).ToList();
            Assert.That(icons, Is.Unique, "No icon repeats in the category list.");
            foreach (var category in CharacterCreator.AppearanceCategories)
            {
                string icon = CharacterCreator.CategoryIcon(category);
                if (icon == CharacterCreator.DrawnSwatches) continue;
                var sprite = icon.Contains("/") ? UiTheme.Pack(icon) : UiTheme.Icon(icon);
                Assert.That(sprite, Is.Not.Null, category + "'s icon '" + icon + "' is one the screens already have.");
            }
            Assert.That(CharacterCreator.CategoryIcon("Colors"), Is.EqualTo(CharacterCreator.DrawnSwatches), "Colors draws its swatches in code.");
        }

        [Test]
        public void TheModeSwitchBelongsToTheAppearanceStep()
        {
            Assert.That(CharacterCreator.ModesShowOn("Appearance"), Is.True);
            Assert.That(CharacterCreator.ModesShowOn(CharacterCreator.QuickPageCaption), Is.True);
            foreach (var page in new[] { "Identity", "Personality", "My Houseguests", "Review" })
                Assert.That(CharacterCreator.ModesShowOn(page), Is.False, page);
            Assert.That(CharacterCreator.ModesLabel, Is.Not.Empty, "The switch says what it switches.");
        }

        [Test]
        public void TheLibraryRowSaysHouseguests()
        {
            foreach (var caption in new[] { CharacterProfileBrowser.OneBoxSearchCaption, CharacterProfileBrowser.OneBoxPreviousCaption,
                         CharacterProfileBrowser.OneBoxNextCaption })
            {
                Assert.That(caption, Does.Contain("houseguests"), caption);
                Assert.That(caption.ToLowerInvariant(), Does.Not.Contain("profile"), caption);
            }
        }
    }
}
