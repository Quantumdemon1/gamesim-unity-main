using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The houseguests' murmurs (AU-16): each houseguest's voice from their id and pronouns alone -
    /// the same in every session and every process, never the season's random stream - and a murmur
    /// a syllable a word, capped at 1.2 seconds, that starts and ends in silence. Unity-free;
    /// CueLoudnessTests holds its level in the mix.
    /// </summary>
    public sealed class MurmurTests
    {
        private const int Rate = 22050;

        [Test]
        public void AHouseguestsPitch_ComesFromTheirIdAndPronouns_TheSameInEveryProcess()
        {
            // FNV-1a over the id's UTF-16 code units: pinned, so a hash .NET seeds per process (string.GetHashCode) cannot pass.
            Assert.That(Murmur.Hash("player"), Is.EqualTo(0xAF75DB2Eu));
            Assert.That(Murmur.Hash("maya-hassan"), Is.EqualTo(0xD9339B8Au));
            Assert.That(Murmur.Hash(""), Is.EqualTo(2166136261u), "Nothing hashed is FNV's offset.");

            Assert.That(Murmur.RegisterOf("he/him"), Is.EqualTo(Murmur.Register.Low));
            Assert.That(Murmur.RegisterOf("She/Her"), Is.EqualTo(Murmur.Register.High));
            Assert.That(Murmur.RegisterOf("they/them"), Is.EqualTo(Murmur.Register.Middle));
            Assert.That(Murmur.RegisterOf(null), Is.EqualTo(Murmur.Register.Middle));
            Assert.That(Murmur.PitchOf("maya-hassan", "she/her"), Is.EqualTo(175.0 * Math.Pow(2.0, 1.26 / 12.0)).Within(1e-9),
                "Her register's floor, raised by her id's hash: 126 hundredths of a semitone.");

            foreach (var pronouns in new[] { "he/him", "she/her", "they/them" })
            {
                double floor = Murmur.FloorOf(Murmur.RegisterOf(pronouns)), ceiling = floor * Math.Pow(2.0, 9.0 / 12.0);
                foreach (var id in new[] { "player", "maya-hassan", "taylor-kim", "riley-johnson", "a", "houseguest-01" })
                {
                    double pitch = Murmur.PitchOf(id, pronouns);
                    Assert.That(pitch, Is.GreaterThanOrEqualTo(floor).And.LessThan(ceiling), id + " (" + pronouns + ") stays in the register.");
                    Assert.That(Murmur.PitchOf(id, pronouns), Is.EqualTo(pitch), id + ": the same voice every time.");
                    Assert.That(Murmur.For(id, pronouns, "Okay.", false).PitchHz, Is.EqualTo(pitch), id + ": whatever the line.");
                    Assert.That(Murmur.For(id, pronouns, "I'm not sure about that at all, honestly.", true).PitchHz, Is.EqualTo(pitch), id + ": witnessed or not.");
                }
            }

            // The default cast: every houseguest has a voice of their own - a quarter of a semitone apart, or plainly brighter or darker.
            var cast = ContentCatalog.Create(1).contestants;
            Assert.That(cast.Count, Is.GreaterThanOrEqualTo(6));
            foreach (var one in cast)
                foreach (var other in cast.Where(each => string.CompareOrdinal(each.id, one.id) > 0))
                {
                    double apart = Math.Abs(12.0 * Math.Log(Murmur.PitchOf(one.id, one.pronouns) / Murmur.PitchOf(other.id, other.pronouns), 2.0));
                    double brighter = Math.Abs(Murmur.BrightnessOf(one.id) - Murmur.BrightnessOf(other.id));
                    Assert.That(apart >= 0.25 || brighter >= 0.1, Is.True,
                        one.id + " and " + other.id + " sound alike: " + apart.ToString("0.00") + " semitones apart, brightness " + brighter.ToString("0.00") + " apart.");
                }
        }

        [Test]
        public void AMurmur_IsASyllableAWord_FromTwoToTen_NeverLongerThanOnePointTwoSeconds()
        {
            Assert.That(Murmur.WordCount(null), Is.EqualTo(0));
            Assert.That(Murmur.WordCount("  "), Is.EqualTo(0));
            Assert.That(Murmur.WordCount("\u2014"), Is.EqualTo(0), "A dash is not a word.");
            Assert.That(Murmur.WordCount("Okay."), Is.EqualTo(1));
            Assert.That(Murmur.WordCount("How's the day treating you?"), Is.EqualTo(5), "How's is one word.");
            Assert.That(Murmur.WordCount("\"Well \u2014 maybe.\""), Is.EqualTo(2));
            Assert.That(Murmur.WordCount("3 votes,  for   sure"), Is.EqualTo(4));

            Assert.That(Enumerable.Range(0, 13).Select(Murmur.SyllablesFor), Is.EqualTo(new[] { 0, 2, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }));
            double last = 0.0;
            for (int words = 0; words <= 40; words++)
            {
                double seconds = Murmur.SecondsFor(words);
                Assert.That(seconds, Is.GreaterThanOrEqualTo(last), words + " words: a longer line never murmurs shorter.");
                Assert.That(seconds, Is.LessThanOrEqualTo(Murmur.MaxSeconds + 1e-9), words + " words: capped at 1.2 s.");
                last = seconds;
            }
            Assert.That(Murmur.SecondsFor(0), Is.EqualTo(0.0), "A line with no words has no murmur.");
            Assert.That(Murmur.SecondsFor(5), Is.EqualTo(0.6).Within(1e-9));
            Assert.That(Murmur.SecondsFor(40), Is.EqualTo(1.2).Within(1e-9));

            var plan = Murmur.For("maya-hassan", "she/her", "I think we should keep this between us until the vote on Thursday, okay?", false);
            Assert.That(plan.Words, Is.EqualTo(14));
            Assert.That(plan.Syllables, Is.EqualTo(10));
            Assert.That(plan.Question, Is.True, "It asks.");
            Assert.That(Murmur.SampleCount(plan, Rate), Is.EqualTo(Murmur.MaxSampleCount(Rate)), "The longest murmur fills the voice's buffer exactly.");
            Assert.That(Murmur.MaxSampleCount(Rate), Is.EqualTo(26460));
            Assert.That(Murmur.SampleCount(Murmur.For("maya-hassan", "she/her", "...", false), Rate), Is.EqualTo(0), "No words, no samples.");

            Assert.That(Murmur.IsQuestion("\"Are you in?\" "), Is.True, "Past the closing quote.");
            Assert.That(Murmur.IsQuestion("Are you in? \u201D"), Is.True);
            Assert.That(Murmur.IsQuestion("I'm in."), Is.False);
            Assert.That(Murmur.IsQuestion(""), Is.False);
        }

        [Test]
        public void AMurmur_StartsAndEndsInSilence_SoundsTheSameEveryTime_AndAWitnessedOneOnlyQuieter()
        {
            const string line = "How's the day treating you?";
            var plan = Murmur.For("player", "they/them", line, false);
            var samples = Murmur.Render(plan, Rate);
            Assert.That(samples.Length, Is.EqualTo(Murmur.SampleCount(plan, Rate)));
            Assert.That(samples.Length, Is.EqualTo((int)Math.Ceiling(0.6 * Rate - 1e-9)), "Five words, five syllables of 0.12 s.");
            Assert.That(samples[0], Is.EqualTo(0f), "It starts in silence, with no click.");
            Assert.That(samples.Skip(samples.Length - 400).Max(sample => Math.Abs(sample)), Is.LessThan(1e-6f), "And ends in the breath after its last syllable.");
            float peak = samples.Max(sample => Math.Abs(sample));
            Assert.That(peak, Is.GreaterThan(0.1f).And.LessThan(0.3f), "Soft, and never near the clip.");
            Assert.That(Murmur.Render(Murmur.For("player", "they/them", line, false), Rate), Is.EqualTo(samples), "The same line murmurs the same way every time.");
            Assert.That(Murmur.Render(Murmur.For("maya-hassan", "she/her", line, false), Rate), Is.Not.EqualTo(samples), "Another houseguest says it in another voice.");

            var witnessed = Murmur.For("player", "they/them", line, true);
            Assert.That(witnessed.Witnessed, Is.True);
            Assert.That(witnessed.GainDb, Is.EqualTo(Murmur.WitnessedGainDb));
            Assert.That(plan.GainDb, Is.EqualTo(0f), "A conversation's own line plays at the Voice volume's full place.");
            Assert.That(Murmur.WitnessedGainDb, Is.LessThanOrEqualTo(-5f), "A pair across the room is plainly quieter.");
            Assert.That(Murmur.Render(witnessed, Rate), Is.EqualTo(samples), "Witnessed, it is the same murmur, only played lower.");

            // Into the voice's buffer: what is left of it after the murmur is cleared, so an earlier, longer murmur is not heard again.
            var buffer = Enumerable.Repeat(1f, Murmur.MaxSampleCount(Rate)).ToArray();
            int written = Murmur.Render(plan, Rate, buffer);
            Assert.That(written, Is.EqualTo(samples.Length));
            Assert.That(buffer.Take(written), Is.EqualTo(samples));
            Assert.That(buffer.Skip(written).All(sample => sample == 0f), Is.True, "The rest of the buffer is silence.");
            Assert.That(Murmur.Render(Murmur.For("player", "they/them", "", false), Rate, buffer), Is.EqualTo(0));
            Assert.That(buffer.All(sample => sample == 0f), Is.True, "A line with no words leaves the voice silent.");
        }
    }
}
