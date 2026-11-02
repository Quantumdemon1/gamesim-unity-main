using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The verification's commit timing log (LIVE-SAVE-PLAN.md section 6): its file format, and that it can only
    /// be switched on by the verification. Pure: the director passes the frame, the time and the revision in.
    /// </summary>
    public sealed class NpcCommitTimingsTests
    {
        [Test]
        public void NothingIsRecordedUntilAskedAndTheFileIsItsHeader()
        {
            var timings = new NpcCommitTimings();
            Assert.That(timings.Count, Is.Zero);
            Assert.That(timings.ToCsv(), Is.EqualTo(NpcCommitTimings.Header + "\n"));
            Assert.That(NpcCommitTimings.Header,
                Is.EqualTo("frame,seconds,kind,cause,revision,prepareMs,validateMs,engineMs,projectMs,planMs,persistMs,saved"));
        }

        [Test]
        public void EachCallIsOneRowInOrderWithEmptyFieldsForThePartsThatDidNotRun()
        {
            var timings = new NpcCommitTimings(1);
            timings.Operation(12, 3.5, NpcOperationKind.Tick, 41, .25, 1.5, 2, 30.125, double.NaN);
            timings.Write(12, 3.5, "periodic", 41, 120.5, true);
            timings.Operation(13, 3.75, NpcOperationKind.Start, 42, .5, double.NaN, 1, 2, 4.0625, "start", 180.25, false);
            timings.Write(14, 4, "quit", 42, 7, false);
            timings.Operation(15, 4.25, NpcOperationKind.Cancel, 43, 1, 1, 1, 1, double.NaN);
            Assert.That(timings.Count, Is.EqualTo(5));
            var lines = timings.ToCsv().Split('\n');
            Assert.That(lines, Is.EqualTo(new[]
            {
                NpcCommitTimings.Header,
                "12,3.5,tick,,41,0.25,1.5,2,30.125,,,",
                "12,3.5,write,periodic,41,,,,,,120.5,true",
                "13,3.75,start,start,42,0.5,,1,2,4.0625,180.25,false",
                "14,4,write,quit,42,,,,,,7,false",
                "15,4.25,cancel,,43,1,1,1,1,,,",
                "",
            }));
            foreach (var line in lines.Skip(1).Where(line => line.Length > 0))
                Assert.That(line.Split(',').Length, Is.EqualTo(NpcCommitTimings.Header.Split(',').Length), line);
        }

        [Test]
        public void NumbersAreInvariantAndRoundTripWhateverTheCulture()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                var timings = new NpcCommitTimings();
                double prepare = 1d / 3, seconds = 1234.5;
                timings.Operation(1, seconds, NpcOperationKind.Tick, 9, prepare, double.NaN, double.NaN, double.NaN, double.NaN);
                var fields = timings.ToCsv().Split('\n')[1].Split(',');
                Assert.That(fields.Length, Is.EqualTo(12), "A decimal comma would split a number into two fields.");
                Assert.That(double.Parse(fields[1], CultureInfo.InvariantCulture), Is.EqualTo(seconds));
                Assert.That(double.Parse(fields[5], CultureInfo.InvariantCulture), Is.EqualTo(prepare));
            }
            finally { Thread.CurrentThread.CurrentCulture = culture; }
        }

        [Test]
        public void ALapIsTheMillisecondsSinceItsMarkAndMovesTheMark()
        {
            long mark = NpcCommitTimings.Now();
            Thread.Sleep(20);
            long before = mark;
            double first = NpcCommitTimings.Lap(ref mark);
            Assert.That(first, Is.GreaterThanOrEqualTo(10).And.LessThan(60000));
            Assert.That(mark, Is.GreaterThan(before));
            long moved = mark;
            Assert.That(NpcCommitTimings.Lap(ref mark), Is.GreaterThanOrEqualTo(0));
            Assert.That(mark, Is.GreaterThanOrEqualTo(moved));
        }

        [Test]
        public void OnlyTheVerificationCanSwitchItOnAndNoSceneEverSerializesOne()
        {
            Assert.That(typeof(NpcCommitTimings).IsSerializable, Is.False,
                "A serializable class in the director's field would be deserialized into a live instance in every scene.");
            Assert.That(typeof(NpcCommitTimings).GetCustomAttributes(typeof(SerializableAttribute), false), Is.Empty);
            var field = typeof(EpisodeDirector).GetField("commitTimingForVerification", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "The director's switch is an internal field.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(NpcCommitTimings)));
            Assert.That(field.IsAssembly, Is.True);
            Assert.That(field.IsNotSerialized, Is.True);
        }
    }
}
