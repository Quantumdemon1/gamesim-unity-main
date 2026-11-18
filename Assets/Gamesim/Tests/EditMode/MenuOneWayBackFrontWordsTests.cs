using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 2, part b - the front door's words: the cast screen's one way back names where it
    /// lands and what it keeps, its one start names who the season starts as, changing a houseguest is
    /// "Edit &lt;name&gt;", and the tour's step back is no way out. Plain C#: the Unity-free suite runs these.
    /// </summary>
    public sealed class MenuOneWayBackFrontWordsTests
    {
        [Test]
        public void TheCastScreensWayBack_NamesWhereItLandsAndWhatItKeeps()
        {
            Assert.That(FrontDoorWords.CastBack(true, WayOut.MainMenu), Is.EqualTo("Back to the main menu"),
                "A first launch has no season to keep.");
            Assert.That(FrontDoorWords.CastBack(false, WayOut.MainMenu), Is.EqualTo("Back to the main menu (season kept)"));
            Assert.That(FrontDoorWords.CastBack(false, FrontDoorWords.SettingsPlace), Is.EqualTo("Back to Settings (season kept)"));
            Assert.That(FrontDoorWords.CastBack(false, FrontDoorWords.FinalePlace), Is.EqualTo("Back to the finale (season kept)"));
            Assert.That(FrontDoorWords.CastBack(false, WayOut.House), Is.EqualTo("Back to the house (season kept)"));
            Assert.That(FrontDoorWords.CastBack(false, null), Is.EqualTo("Back to the main menu (season kept)"), "No place named is the main menu.");
            foreach (string place in new[] { WayOut.MainMenu, FrontDoorWords.SettingsPlace, FrontDoorWords.FinalePlace, WayOut.House })
            {
                Assert.That(WayOut.IsWayBack(FrontDoorWords.CastBack(false, place)), Is.True, place);
                Assert.That(FrontDoorWords.CastBack(false, place), Is.EqualTo(WayOut.BackToKeeping(place, "season")),
                    "Slice 1's truthful parenthesis, through the one helper.");
            }
        }

        [Test]
        public void TheOneStart_NamesWhoTheSeasonStartsAs()
        {
            Assert.That(FrontDoorWords.StartAs("Emma"), Is.EqualTo("Start as Emma"));
            Assert.That(FrontDoorWords.StartAs("  Will "), Is.EqualTo("Start as Will"));
            Assert.That(FrontDoorWords.StartAsNewcomer, Is.EqualTo("Start as a newcomer"), "Nobody picked and nothing built.");
            Assert.That(FrontDoorWords.StartAs(""), Is.EqualTo("Start as your houseguest"), "Built, with no name yet: never the newcomer.");
            Assert.That(FrontDoorWords.StartAs(null), Is.Not.EqualTo(FrontDoorWords.StartAsNewcomer));
            foreach (string caption in new[] { FrontDoorWords.StartAs("Emma"), FrontDoorWords.StartAsNewcomer, FrontDoorWords.StartAs(null) })
            {
                Assert.That(caption, Does.StartWith(FrontDoorWords.StartAsPrefix));
                Assert.That(WayOut.IsWayBack(caption), Is.False, "The start leads on: it is not a way back.");
            }
        }

        [Test]
        public void ChangingAHouseguest_IsEditAndAName()
        {
            Assert.That(FrontDoorWords.Edit("Emma"), Is.EqualTo("Edit Emma"));
            Assert.That(FrontDoorWords.Edit(null), Is.EqualTo("Edit houseguest"), "Where no name is at hand.");
            Assert.That(FrontDoorWords.Edit(" "), Is.EqualTo("Edit houseguest"));
            Assert.That(FrontDoorWords.Edit("Emma"), Does.Not.Contain("Customize").And.Not.Contain("profile"));
        }

        [Test]
        public void TheToursStepBack_IsNoWayOutAndNoBareBack()
        {
            Assert.That(FrontDoorWords.TourPrevious, Is.EqualTo("Previous"));
            Assert.That(WayOut.IsWayBack(FrontDoorWords.TourPrevious), Is.False);
            Assert.That(FrontDoorWords.TourPrevious, Does.Not.Contain("Back"), "A bare Back is an exit word the slice folds away.");
        }

        [Test]
        public void TheWords_NameEveryPlaceInTheSharedWay()
        {
            Assert.That(FrontDoorWords.SettingsPlace, Is.EqualTo("Settings"));
            Assert.That(FrontDoorWords.FinalePlace, Is.EqualTo("the finale"));
            Assert.That(FrontDoorWords.SeasonKept, Is.EqualTo("season"));
            Assert.That(FrontDoorWords.CastBack(false, FrontDoorWords.SettingsPlace), Does.Not.Contain("slot"), "A slot is a save, never the cast screen's word.");
        }
    }
}
