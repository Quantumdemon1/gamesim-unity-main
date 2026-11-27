using System.Collections.Generic;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Every press answers (AU-10): a press that asked for no sound of its own clicks once, after it
    /// ran, and one that asked for its own - a panel arriving, a commit's sound - is left to it, so no
    /// press sounds twice. Before, a tab, a switch or a way back made no sound at all. Unity-free.
    /// </summary>
    public sealed class PressAnswerTests
    {
        [Test]
        public void APressThatAskedForNoSound_ClicksOnce_AfterItRan()
        {
            var heard = new List<string>();
            int asked = 0;
            PressAnswer.Run(() => heard.Add("ran"), () => asked, () => { asked++; heard.Add("click"); });
            Assert.That(heard, Is.EqualTo(new[] { "ran", "click" }), "The press runs, then its click is asked for, once.");
            Assert.That(asked, Is.EqualTo(1));
        }

        [Test]
        public void APressThatAskedForItsOwnSound_IsLeftToIt()
        {
            var heard = new List<string>();
            int asked = 0;
            // A panel arriving asks for its own sound while the press runs.
            PressAnswer.Run(() => { asked++; heard.Add("panel"); }, () => asked, () => heard.Add("click"));
            Assert.That(heard, Is.EqualTo(new[] { "panel" }), "No click on top of the panel's sound.");
            // And a count that had moved before the press is no sound of the press's own.
            heard.Clear();
            asked = 7;
            PressAnswer.Run(() => heard.Add("ran"), () => asked, () => heard.Add("click"));
            Assert.That(heard, Is.EqualTo(new[] { "ran", "click" }), "Only what the press itself asked for counts.");
        }

        [Test]
        public void NothingToRun_IsNoPress_AndNoCountStillClicks()
        {
            var heard = new List<string>();
            int reads = 0;
            PressAnswer.Run(null, () => reads++, () => heard.Add("click"));
            Assert.That(heard, Is.Empty, "A press with nothing to run makes no sound.");
            Assert.That(reads, Is.Zero, "and reads nothing.");
            PressAnswer.Run(() => heard.Add("ran"), null, () => heard.Add("click"));
            Assert.That(heard, Is.EqualTo(new[] { "ran", "click" }), "With no count to read, a press clicks.");
        }
    }
}
