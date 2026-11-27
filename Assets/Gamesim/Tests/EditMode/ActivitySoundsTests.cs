using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The sounds of the player's activities, as numbers: six activities have a loop, each a loop
    /// rather than a cue and heard under the news; a loop comes in over 0.4 s and goes over 0.6 s;
    /// the one voice carries one loop at a time; and the dance's beat is music the season's music
    /// steps back for. Unity-free, so Tools/SimulationTests runs it; EpisodePlayModeTests'
    /// HouseActivities_CookingIsHeardUntilThePlayerGetsUp pins the house's side.
    /// </summary>
    public sealed class ActivitySoundsTests
    {
        /// <summary>The frame lengths every fade is run at: 60 and 30 fps, the longest step a frame is credited, and a batchmode frame.</summary>
        private static readonly float[] Frames = { 1f / 60f, 1f / 30f, MusicFade.LongestStep, 0.00043f };

        private const float Near = 1e-3f;

        /// <summary>The voice moved on <paramref name="total"/> seconds in steps of <paramref name="frame"/>, the last whatever is left.</summary>
        private static void Run(ActivityLoop loop, double total, float frame)
        {
            double elapsed = 0.0;
            while (elapsed < total - 1e-9)
            {
                float step = (float)Math.Min(frame, total - elapsed);
                loop.Tick(step);
                elapsed += step;
            }
        }

        [Test]
        public void EverySoundingActivityShipsALoopOfSixToThirtySecondsAtFullRate()
        {
            var names = ActivitySounds.Rows.Select(row => row.Activity).ToArray();
            Assert.That(names, Is.EqualTo(new[] { "PrepareSnack", "Sleep", "Swim", "Soak", "Cook", "Dance" }),
                "Six activities make a sound, in the activities' order;");
            Assert.That(ActivitySounds.Find("SitAtTable"), Is.Null, "sitting at the table");
            Assert.That(ActivitySounds.Find("Rest"), Is.Null, "and resting on a lounger stay quiet.");
            string folder = Path.Combine(GamesimRoot(), "Resources", "Audio", "Activities");
            foreach (var name in names)
            {
                string path = Path.Combine(folder, name + ".wav");
                Assert.That(File.Exists(path), Is.True, "Resources/" + ActivitySounds.ResourceFolder + name + " is the activity's loop.");
                ReadWav(path, out int rate, out int channels, out double seconds, out double rms);
                Assert.That(rate, Is.EqualTo(44100), name + " is at full rate.");
                Assert.That(channels, Is.EqualTo(2), name + " is stereo, as the rooms' beds are.");
                Assert.That(seconds, Is.InRange(6.0, 30.0), name + " is a loop, not a cue.");
                Assert.That(rms, Is.InRange(-34.0, -18.0), name + " is heard, and under the game's news (" + rms.ToString("0.00") + " dB).");
            }
            var shipped = Directory.GetFiles(folder, "*.wav").Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.That(shipped, Is.EqualTo(names.OrderBy(name => name, StringComparer.Ordinal).ToArray()),
                "Every loop in the folder is an activity's, named for it.");
        }

        [Test]
        public void ALoopComesInOverFourTenthsOfASecondAndGoesOverSixTenths()
        {
            Assert.That(ActivitySounds.FadeIn, Is.EqualTo(0.4f));
            Assert.That(ActivitySounds.FadeOut, Is.EqualTo(MusicFade.DuckOut), "It goes as the music's duck comes back.");
            foreach (var frame in Frames)
            {
                var loop = new ActivityLoop();
                Assert.That(loop.Sounding, Is.False, "Nothing asked for, nothing sounds.");
                loop.Ask(ActivitySounds.LoopOf("Cook"));
                Assert.That(loop.Key, Is.EqualTo("Cook"), "A silent voice takes the loop at once");
                Assert.That(loop.Level, Is.EqualTo(0f), "and starts it from silence.");
                Assert.That(loop.Sounding, Is.True);
                Run(loop, ActivitySounds.FadeIn / 2, frame);
                Assert.That(loop.Level, Is.EqualTo(0.5f).Within(Near), "Halfway up at 0.2 s, in steps of " + frame + " s.");
                Run(loop, ActivitySounds.FadeIn / 2, frame);
                Assert.That(loop.Level, Is.EqualTo(1f), "Full at 0.4 s.");

                loop.Ask(ActivitySounds.LoopOf(null));
                Assert.That(loop.Key, Is.EqualTo("Cook"), "Getting up lets the loop fade rather than cutting it.");
                Assert.That(loop.Sounding, Is.True);
                Run(loop, ActivitySounds.FadeOut / 2, frame);
                Assert.That(loop.Level, Is.EqualTo(0.5f).Within(Near), "Halfway down at 0.3 s.");
                Run(loop, ActivitySounds.FadeOut / 2, frame);
                Assert.That(loop.Level, Is.EqualTo(0f), "Silent at 0.6 s,");
                Assert.That(loop.Key, Is.Null, "and the voice is let go.");
                Assert.That(loop.Sounding, Is.False);
            }
        }

        [Test]
        public void OneLoopAtATime_TheNextWaitsForTheLastToFadeAndATurnBackCarriesOn()
        {
            foreach (var frame in Frames)
            {
                var loop = new ActivityLoop();
                loop.Ask("Cook");
                Run(loop, ActivitySounds.FadeIn, frame);
                loop.Ask("Swim");
                Assert.That(loop.Key, Is.EqualTo("Cook"), "The voice keeps the sizzle while it fades");
                Run(loop, ActivitySounds.FadeOut / 2, frame);
                Assert.That(loop.Level, Is.EqualTo(0.5f).Within(Near), "and it is halfway down at 0.3 s.");
                Assert.That(loop.Key, Is.EqualTo("Cook"), "The swim waits for it.");

                loop.Ask("Cook");
                Run(loop, ActivitySounds.FadeIn / 4, frame);
                Assert.That(loop.Level, Is.EqualTo(0.75f).Within(Near), "Turned back, it climbs from where it was: a quarter of the way in 0.1 s.");

                loop.Ask("Swim");
                Run(loop, ActivitySounds.FadeOut * 0.75, frame);
                Assert.That(loop.Key, Is.EqualTo("Swim"), "Once the sizzle is silent the voice takes the swim");
                Assert.That(loop.Level, Is.LessThan(0.01f), "from silence (a batchmode frame's sliver of a climb aside)");
                Run(loop, ActivitySounds.FadeIn, frame);
                Assert.That(loop.Level, Is.EqualTo(1f), "and brings it all the way in.");

                loop.Silence();
                Assert.That(loop.Level, Is.EqualTo(0f), "Taken away by the preferences, it is silent at once,");
                Assert.That(loop.Key, Is.EqualTo("Swim"), "still the swim,");
                Assert.That(loop.Sounding, Is.True, "and comes back in from silence when it can be heard.");
            }
        }

        [Test]
        public void TheDanceIsMusicAndTheSeasonsMusicStepsBackUnderIt()
        {
            foreach (var row in ActivitySounds.Rows)
            {
                Assert.That(ActivitySounds.DucksMusic(row.Activity), Is.EqualTo(row.Activity == "Dance"), row.Activity);
                Assert.That(ActivitySounds.GainOf(row.Activity), Is.EqualTo(1f), row.Activity + " plays as rendered.");
            }
            Assert.That(ActivitySounds.DucksMusic(null), Is.False);
            Assert.That(ActivitySounds.DucksMusic("SitAtTable"), Is.False);

            Assert.That(MusicFade.MusicDuckFor(false, false, true), Is.EqualTo(MusicFade.ActivityMusicDuck), "Dancing, the music is under the beat.");
            Assert.That(MusicFade.ActivityMusicDuck, Is.LessThan(MusicFade.PausedMusicDuck), "Deeper than the menu's duck,");
            Assert.That(MusicFade.MusicDuckFor(true, false, true), Is.EqualTo(MusicFade.ActivityMusicDuck), "so the menu opened over a dance leaves it there,");
            Assert.That(MusicFade.MusicDuckFor(false, true, true), Is.EqualTo(MusicFade.ActivityMusicDuck), "as a big moment does.");
            Assert.That(MusicFade.MusicDuckFor(false, false, false), Is.EqualTo(1f), "Without a dance nothing changes:");
            Assert.That(MusicFade.MusicDuckFor(true, false, false), Is.EqualTo(MusicFade.PausedMusicDuck));
            Assert.That(MusicFade.MusicDuckFor(false, true, false), Is.EqualTo(MusicFade.MomentMusicDuck));
            Assert.That(MusicFade.MusicDuckFor(true, true, false), Is.EqualTo(MusicFade.MusicDuckFor(true, true)));
        }

        /// <summary>A 16-bit PCM WAV's rate, channels, length and level over all its samples, in decibels of full scale.</summary>
        private static void ReadWav(string path, out int rate, out int channels, out double seconds, out double rmsDb)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"), path);
            Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WAVE"), path);
            int format = 0, bits = 0;
            rate = 0; channels = 0;
            for (int at = 12; at + 8 <= bytes.Length;)
            {
                string id = Encoding.ASCII.GetString(bytes, at, 4);
                int size = BitConverter.ToInt32(bytes, at + 4);
                int body = at + 8;
                if (id == "fmt ")
                {
                    format = BitConverter.ToInt16(bytes, body);
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    rate = BitConverter.ToInt32(bytes, body + 4);
                    bits = BitConverter.ToInt16(bytes, body + 14);
                }
                else if (id == "data")
                {
                    Assert.That(format, Is.EqualTo(1), path + " is PCM.");
                    Assert.That(bits, Is.EqualTo(16), path + " is 16-bit.");
                    Assert.That(channels, Is.GreaterThan(0), path);
                    Assert.That(rate, Is.GreaterThan(0), path);
                    int count = Math.Min(size, bytes.Length - body) / 2;
                    Assert.That(count, Is.GreaterThan(0), path);
                    double sum = 0.0;
                    for (int i = 0; i < count; i++)
                    {
                        double sample = BitConverter.ToInt16(bytes, body + 2 * i) / 32768.0;
                        sum += sample * sample;
                    }
                    seconds = count / (double)channels / rate;
                    rmsDb = 10.0 * Math.Log10(sum / count);
                    return;
                }
                at = body + size + (size & 1);
            }
            Assert.Fail(path + " has no data chunk.");
            seconds = 0.0; rmsDb = 0.0;
        }

        private static string GamesimRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                if (Directory.Exists(Path.Combine(candidate, "Resources", "Audio", "Activities"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Resources/Audio/Activities was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
