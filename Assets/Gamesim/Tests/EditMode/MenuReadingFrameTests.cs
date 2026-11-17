using System;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The tall reading frame's kinds and measures (menu slice 3, the review's change 5): one table
    /// gives every docked layout a kind, the notebook's layout the one READING kind, and the frame is
    /// about seven tenths of the canvas tall, beside the rail, never into the top bar or the status
    /// line, at every text size and every aspect the game lays out. Unity-free, so the subset runs it.
    /// </summary>
    public sealed class MenuReadingFrameTests
    {
        /// <summary>The docked layouts, as EpisodeHud.ActivityLayout names them (the EditMode kinds test checks the enum itself).</summary>
        private static readonly string[] Layouts =
        {
            "Standard", "Reading", "Conversation", "ConversationNotice", "Competition", "Creation", "Diary",
            "Nominations", "HouseEvent", "Ballot", "Settings", "Stage", "Strategy",
        };

        private static readonly float[] TextScales = { 1f, 1.2f, 1.4f };

        /// <summary>Canvas sizes the frame scaler gives: 16:9, 16:10, 4:3 (matched half and half), and a short wide 21:9 window (matched by height).</summary>
        private static readonly (float Width, float Height)[] Canvases =
        {
            (1600f, 900f), (1517.9f, 948.7f), (1385.6f, 1039.2f), (2133.3f, 900f), (3200f, 900f),
        };

        /// <summary>The HUD's numbers the frame is placed between: the rail's right edge, the top bar's headroom, the right edge's margin.</summary>
        private const float Left = 226f, Headroom = 80f, Edge = 24f;

        /// <summary>The status line's band's top at a text scale: the cast strip's 14 + 96, a 14 gap, the 50-unit line, an 8 gap.</summary>
        private static float Floor(float scale) => 14f + 96f * scale + 14f + 50f * scale + 8f;

        [Test]
        public void EveryDockedLayoutHasOneKind_AndOnlyTheNotebooksIsReading()
        {
            foreach (var layout in Layouts)
                Assert.That(ReadingFrame.TryKind(layout, out _), Is.True, "'" + layout + "' has a kind.");
            Assert.That(ReadingFrame.Kinds.Keys, Is.EquivalentTo(Layouts), "The table lists every layout once and nothing else.");
            Assert.That(ReadingFrame.Kinds.Where(pair => pair.Value == PanelKind.Reading).Select(pair => pair.Key), Is.EqualTo(new[] { "Reading" }),
                "The notebook's layout is the one READING layout; a conversation, a prompt, a card, a decision and the settings are QUICK.");
            Assert.That(ReadingFrame.KindOf("Settings"), Is.EqualTo(PanelKind.Quick), "Settings belongs to its own slice and keeps its shape.");
        }

        [Test]
        public void ALayoutWithoutAKind_IsAnError_NeverAGuess()
        {
            Assert.That(ReadingFrame.TryKind("Scrapbook", out _), Is.False);
            Assert.That(ReadingFrame.TryKind(null, out _), Is.False);
            var error = Assert.Throws<ArgumentException>(() => ReadingFrame.KindOf("Scrapbook"));
            Assert.That(error.Message, Does.Contain("Scrapbook"), "The error names the layout.");
        }

        [Test]
        public void TheFrame_IsAboutSevenTenthsTall_WhereTheRoomAllows()
        {
            var box = ReadingFrame.Place(1600f, 900f, Left, Floor(1f), Headroom, Edge);
            Assert.That(box.Height, Is.EqualTo(630f).Within(.01f), "Seven tenths of a 900-unit canvas at the resting text size.");
            Assert.That(box.Y, Is.EqualTo(182f).Within(.01f), "It stands on the status line's band.");
            Assert.That(box.Width, Is.EqualTo(ReadingFrame.MaxWidth).Within(.01f), "A full line on a 16:9 canvas.");
            Assert.That(box.X - Left, Is.EqualTo(1600f - Edge - box.Right).Within(.01f), "Centred in the room right of the rail.");
        }

        [Test]
        public void TheFrame_FitsBetweenTheStatusLineAndTheTopBar_AtEveryTextSizeAndAspect()
        {
            foreach (var canvas in Canvases)
            foreach (var scale in TextScales)
            {
                var box = ReadingFrame.Place(canvas.Width, canvas.Height, Left, Floor(scale), Headroom, Edge);
                string at = canvas.Width + "x" + canvas.Height + " at text x" + scale + ": " + box;
                Assert.That(box.Y, Is.EqualTo(Floor(scale)).Within(.01f), at + " stands on the status line's band.");
                Assert.That(box.Top, Is.LessThanOrEqualTo(canvas.Height - Headroom + .01f), at + " stays under the top bar.");
                Assert.That(box.Height, Is.LessThanOrEqualTo(canvas.Height * ReadingFrame.HeightShare + .01f), at + " is never more than seven tenths.");
                Assert.That(box.Height, Is.GreaterThanOrEqualTo(canvas.Height * .6f), at + " is still a tall page, not a strip.");
                Assert.That(box.X, Is.GreaterThanOrEqualTo(Left - .01f), at + " stands beside the rail, never on it.");
                Assert.That(box.Right, Is.LessThanOrEqualTo(canvas.Width - Edge + .01f), at + " keeps the right edge's margin.");
                Assert.That(box.Width, Is.GreaterThanOrEqualTo(900f), at + " is wide enough for a comfortable line.");
                Assert.That(box.Width, Is.LessThanOrEqualTo(ReadingFrame.MaxWidth + .01f), at + " is no wider than a comfortable line.");
            }
        }

        [Test]
        public void TheVeil_IsNamedAndDims_WithoutHidingTheHouse()
        {
            Assert.That(ReadingFrame.VeilName, Is.Not.Empty);
            Assert.That(ReadingFrame.VeilOpacity, Is.GreaterThan(.4f).And.LessThan(.9f), "The house is dimmed, not gone.");
        }
    }
}
