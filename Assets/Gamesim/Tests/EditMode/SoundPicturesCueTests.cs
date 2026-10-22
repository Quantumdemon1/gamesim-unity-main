using System;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The sound-and-picture table (PLAN A, A11) against the cues the house actually plays: every
    /// <see cref="HouseAudio.Cue"/> has exactly one row, and every row names a cue there is. A cue
    /// added without its picture fails here rather than reaching a player who cannot hear it.
    /// </summary>
    public sealed class SoundPicturesCueTests
    {
        [Test]
        public void EveryCue_HasARow_AndEveryRowACue()
        {
            var cues = Enum.GetNames(typeof(HouseAudio.Cue));
            var rows = SoundPictures.Rows.Select(row => row.Cue).ToArray();
            Assert.That(rows, Is.EquivalentTo(cues), "One row for each cue the house has, and no other.");
            Assert.That(rows, Is.EqualTo(cues), "In the enum's order, so the table reads as the cues do.");
        }
    }
}
