using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house screen's This Week card (the menu review's change 7, backlog item 32): the week's
    /// latest events and the house vibe in one card under the live feed. The right column folds
    /// around the room it always reckoned under the events (a gap and the week card's room), so the
    /// card, and the Nearby card or the Pull standing in the vibe's place, must never reach lower
    /// than that room. Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class ThisWeekShapeTests
    {
        /// <summary>The most event rows the column shows (EpisodeHud.RecentEventRows).</summary>
        private const int MostRows = 3;

        [Test]
        public void TheCardAndWhatBorrowsTheVibeStayInsideTheRoomTheColumnFoldsAround()
        {
            for (int rows = 0; rows <= MostRows; rows++)
            {
                float room = ThisWeekShape.EventsHeight(rows) + ThisWeekShape.ColumnGap + TextSizeFit.WeekCardRoom;
                Assert.That(ThisWeekShape.Height(true, rows), Is.LessThanOrEqualTo(room),
                    "With " + rows + " event rows the card ends inside the room the column reckons.");
                foreach (var size in TextSizes.All)
                    Assert.That(ThisWeekShape.BorrowerTop(rows) + TextSizeFit.SideCard(TextSizes.Scale(size)), Is.LessThanOrEqualTo(room),
                        "With " + rows + " event rows the Nearby card and the Pull end inside it too, at the " + TextSizes.Name(size) + " text.");
            }
            Assert.That(ThisWeekShape.Height(false, 0), Is.LessThanOrEqualTo(TextSizeFit.WeekCardRoom),
                "Holding the vibe alone (somebody in focus) the card is no taller than the week card's room.");
            Assert.That(ThisWeekShape.Height(true, MostRows), Is.LessThan(ThisWeekShape.EventsHeight(MostRows) + ThisWeekShape.ColumnGap + TextSizeFit.WeekCardRoom),
                "and one card is shorter than the two cards it replaces.");
        }

        [Test]
        public void TheEventsAndTheVibeAreTwoSectionsOneUnderTheOther_AndLendingTheVibeEndsTheCardUnderTheEvents()
        {
            for (int rows = 0; rows <= MostRows; rows++)
            {
                Assert.That(ThisWeekShape.VibeTop(true, rows), Is.GreaterThan(ThisWeekShape.EventsHeight(rows)),
                    "The vibe starts under the events with a gap, so the two named sections never meet.");
                Assert.That(ThisWeekShape.Height(true, rows), Is.EqualTo(ThisWeekShape.VibeTop(true, rows) + ThisWeekShape.VibeHeight),
                    "The card ends where the vibe ends, so a check on the vibe is a check on the card's foot.");
                Assert.That(ThisWeekShape.LentHeight(rows), Is.EqualTo(ThisWeekShape.EventsHeight(rows)),
                    "Lent, the card ends under its events.");
                Assert.That(ThisWeekShape.BorrowerTop(rows), Is.EqualTo(ThisWeekShape.LentHeight(rows) + ThisWeekShape.ColumnGap),
                    "and what borrows the vibe's place stands a column gap under them.");
                Assert.That(ThisWeekShape.EventsHeight(rows), Is.EqualTo(42f + rows * 58f + 2f),
                    "The events keep Recent Events' rows, so the fold folds them as before.");
            }
            Assert.That(ThisWeekShape.VibeTop(false, 0), Is.GreaterThanOrEqualTo(34f), "Alone, the vibe starts under the card's heading.");
        }

        [Test]
        public void TheVibeRowsAndTheirLineHoldTheirWords()
        {
            Assert.That(ThisWeekShape.VibeWordBox, Is.GreaterThanOrEqualTo(ThisWeekShape.VibeWordFont * TextSizeFit.LabelBoxRatio),
                "A row's words and count have a box 1.3 times their size.");
            Assert.That(ThisWeekShape.TensionBox, Is.GreaterThanOrEqualTo(ThisWeekShape.TensionFont * TextSizeFit.LabelBoxRatio),
                "and so does the line under the rows.");
            Assert.That(ThisWeekShape.VibeRowHeight, Is.GreaterThanOrEqualTo(ThisWeekShape.VibeWordBox), "The rows do not overlap.");
            Assert.That(ThisWeekShape.TensionTop, Is.GreaterThanOrEqualTo(ThisWeekShape.VibeRowTop + ThisWeekShape.VibeRows * ThisWeekShape.VibeRowHeight),
                "The line starts under the last row.");
            Assert.That(System.Math.Min(ThisWeekShape.VibeWordFont, ThisWeekShape.TensionFont), Is.GreaterThanOrEqualTo(10),
                "No word under the HUD's 10-unit floor (12 pixels on the 1080 frame).");
        }
    }
}
