using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// "Conversation lines: Move on" (UI plan D11, lead ruling L15): the clock each spoken line of a
    /// conversation exchange keeps. A line holds for its reading time at the caption time in use; a
    /// redraw keeps its clock; it moves on once and never again; a press that moved the exchange on
    /// starts the next line's own clock, so the line before can never move it on; a cover holds it.
    /// Unity-free, so the subset runs it on every push; the director's use of it is checked in
    /// PlayMode (EpisodePlayModeTests.ConversationExchangeFlow.cs).
    /// </summary>
    public sealed class ConversationLinePaceTests
    {
        private const string Session = "season-a";
        private const string Question = "How's the day treating you?";
        private const string LongReply = "Honestly it has been a long one, but I am glad you came over to ask.";

        [Test]
        public void ALine_HoldsForItsReadingTime_AndHalfAsLongAgainUnderLonger()
        {
            Assert.That(ConversationLinePace.Hold(Question, CaptionTime.Standard), Is.EqualTo(1.2 + 0.3 * 5).Within(1e-9), "1.2 seconds and 0.3 a word.");
            Assert.That(ConversationLinePace.Hold(Question, CaptionTime.Longer), Is.EqualTo((1.2 + 0.3 * 5) * 1.5).Within(1e-9));
            Assert.That(ConversationLinePace.Hold(Question, CaptionTime.Standard), Is.EqualTo(CaptionTiming.Hold(0.0, Question, CaptionTime.Standard)),
                "The same reading time as every caption the game takes down by itself.");
            Assert.That(ConversationLinePace.Name(false), Is.EqualTo("wait"));
            Assert.That(ConversationLinePace.Name(true), Is.EqualTo("move-on"));

            var pace = new ConversationLinePace();
            pace.Show(Session, 3, 0, Question, CaptionTime.Longer, 10.0);
            Assert.That(pace.Due(10.0 + 4.0), Is.False, "Under Longer, 2.7 seconds is not yet up.");
            Assert.That(pace.Due(10.0 + 4.05), Is.True);
        }

        [Test]
        public void ALine_MovesOnOnce_AndARedrawKeepsItsClock()
        {
            var pace = new ConversationLinePace();
            Assert.That(pace.Due(1000.0), Is.False, "No line, nothing to move on.");
            pace.Show(Session, 3, 0, Question, CaptionTime.Standard, 0.0);
            pace.Show(Session, 3, 0, Question, CaptionTime.Standard, 2.0);
            Assert.That(pace.DueAt, Is.EqualTo(2.7).Within(1e-9), "A redraw of the same line keeps the clock it started.");
            Assert.That(pace.Due(2.6), Is.False);
            Assert.That(pace.Due(2.7), Is.True, "Up at its reading time,");
            Assert.That(pace.Session, Is.EqualTo(Session), "for the line that was drawn:");
            Assert.That(pace.Revision, Is.EqualTo(3));
            Assert.That(pace.Step, Is.EqualTo(0));
            Assert.That(pace.Due(2.8), Is.False, "and only once.");
            pace.Show(Session, 3, 0, Question, CaptionTime.Standard, 5.0);
            Assert.That(pace.Due(1000.0), Is.False, "A line moved on stays moved on, however often it is drawn again.");
        }

        [Test]
        public void APress_StartsTheNextLinesOwnClock_SoTheLineBeforeNeverMovesItOn()
        {
            var pace = new ConversationLinePace();
            pace.Show(Session, 3, 0, Question, CaptionTime.Standard, 0.0);
            // A press at one second moves the exchange on: the reply is drawn and starts its own clock.
            double replyHold = ConversationLinePace.Hold(LongReply, CaptionTime.Standard);
            pace.Show(Session, 3, 1, LongReply, CaptionTime.Standard, 1.0);
            Assert.That(pace.Due(2.7), Is.False, "The question's time is not the reply's.");
            Assert.That(pace.Due(1.0 + replyHold - 0.01), Is.False);
            Assert.That(pace.Due(1.0 + replyHold), Is.True, "The reply moves on after its own reading time from the press,");
            Assert.That(pace.Step, Is.EqualTo(1), "as the reply.");
            Assert.That(pace.Due(1000.0), Is.False);
            // The next exchange (a new revision) is a new line from its first step.
            pace.Show(Session, 4, 0, Question, CaptionTime.Standard, 20.0);
            Assert.That(pace.Due(22.7), Is.True);
            Assert.That(pace.Revision, Is.EqualTo(4));
            Assert.That(pace.Step, Is.EqualTo(0));
        }

        [Test]
        public void ACoveredLine_WaitsAsLongAsItWasCovered_AndAnEndedExchangeKeepsNoClock()
        {
            var pace = new ConversationLinePace();
            pace.Show(Session, 3, 0, Question, CaptionTime.Standard, 0.0);
            pace.Cover(1.5);
            Assert.That(pace.Due(2.7), Is.False, "Nobody read it while it was covered.");
            Assert.That(pace.Due(4.2), Is.True);
            pace.Cover(5.0);
            Assert.That(pace.DueAt, Is.EqualTo(4.2).Within(1e-9), "A line already moved on is not put back on the clock.");
            pace.Show(Session, 5, 0, Question, CaptionTime.Standard, 10.0);
            pace.Clear();
            Assert.That(pace.Armed, Is.False);
            Assert.That(pace.Due(1000.0), Is.False, "The exchange ended: nothing moves on after it.");
            Assert.That(pace.Session, Is.Null);
        }
    }
}
