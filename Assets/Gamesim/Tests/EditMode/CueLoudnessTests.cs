using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The cues' mix, read from the recordings themselves: every recording peaks at the same level,
    /// so loudness ran backwards - the panels' swish stood level with the eviction and the hover's
    /// tick over the ballot's - until each row of the sound table carried its place in the mix
    /// (<see cref="SoundPictures.Row.GainDb"/>). A cue's loudness here is its recording's loudest
    /// 400 milliseconds (the whole of a shorter one) plus its row's gain. Unity-free: it reads the
    /// WAVs' bytes. The competitions' loop is measured here too, whole, against the season's bed, and
    /// the main menu's title loop against the theme.
    /// </summary>
    public sealed class CueLoudnessTests
    {
        /// <summary>The game's biggest news, the beats the interface must never sound as big as.</summary>
        private static readonly string[] Headlines = { "Nomination", "Veto", "Eviction", "CompetitionWin", "Finale" };

        [Test]
        public void EveryInterfaceAnswerSitsEightDecibelsUnderTheQuietestHeadline_AndTheHoverTwelve()
        {
            var loudness = SoundPictures.Rows.ToDictionary(row => row.Cue, row => LoudestDecibels(Recording(row.Cue)) + row.GainDb);
            double quietestHeadline = Headlines.Min(cue => loudness[cue]);
            foreach (var row in SoundPictures.Rows.Where(row => row.Kind == SoundKind.Interface))
            {
                double under = row.Cue == "Hover" ? 12.0 : 8.0;
                Assert.That(loudness[row.Cue], Is.LessThanOrEqualTo(quietestHeadline - under),
                    row.Cue + " plays at " + loudness[row.Cue].ToString("0.00") + " dB against the quietest headline's "
                    + quietestHeadline.ToString("0.00") + ": at least " + under + " dB under it.");
            }
            Assert.That(SoundPictures.Rows.Count(row => row.Kind == SoundKind.Interface), Is.GreaterThanOrEqualTo(5),
                "The press, the two panels, the hover and the tour's step are all measured.");
        }

        /// <summary>
        /// The front door (AU-06) is the house, not news: its clunk and its thud, as played, sit at
        /// least 3 dB under the quietest headline, so a door never sounds as big as the eviction it
        /// follows; and the thud is the bigger of the two, as a landing is.
        /// </summary>
        [Test]
        public void TheFrontDoorSitsUnderTheQuietestHeadline_AndItsThudOverItsClunk()
        {
            var loudness = SoundPictures.Rows.ToDictionary(row => row.Cue, row => LoudestDecibels(Recording(row.Cue)) + row.GainDb);
            double quietestHeadline = Headlines.Min(cue => loudness[cue]);
            foreach (var row in SoundPictures.Rows.Where(row => row.Kind == SoundKind.World))
                Assert.That(loudness[row.Cue], Is.LessThanOrEqualTo(quietestHeadline - 3.0),
                    row.Cue + " plays at " + loudness[row.Cue].ToString("0.00") + " dB against the quietest headline's "
                    + quietestHeadline.ToString("0.00") + ": at least 3 dB under it.");
            Assert.That(loudness["DoorShut"], Is.GreaterThan(loudness["DoorOpen"]), "The door lands harder than it gives.");
        }

        /// <summary>
        /// A competition's count (AU-08) ticks every second, so it plays at least 3 dB under a ballot's
        /// tick, and over the interface's loudest answer, as news does; an early press is a small
        /// thing, at least 2 dB under a miss.
        /// </summary>
        [Test]
        public void TheCountTicksUnderTheBallot_AndAnEarlyPressBuzzesUnderAMiss()
        {
            var loudness = SoundPictures.Rows.ToDictionary(row => row.Cue, row => LoudestDecibels(Recording(row.Cue)) + row.GainDb);
            double loudestAnswer = SoundPictures.Rows.Where(row => row.Kind == SoundKind.Interface).Max(row => loudness[row.Cue]);
            Assert.That(loudness["CountTick"], Is.LessThanOrEqualTo(loudness["Vote"] - 3.0),
                "The count's tick plays at " + loudness["CountTick"].ToString("0.00") + " dB against the ballot's " + loudness["Vote"].ToString("0.00") + ".");
            Assert.That(loudness["CountTick"], Is.GreaterThan(loudestAnswer),
                "The count's tick is news, over the interface's loudest answer at " + loudestAnswer.ToString("0.00") + " dB.");
            Assert.That(loudness["FalseStart"], Is.LessThanOrEqualTo(loudness["SocialDown"] - 2.0),
                "An early press plays at " + loudness["FalseStart"].ToString("0.00") + " dB against a miss's " + loudness["SocialDown"].ToString("0.00") + ".");
        }

        /// <summary>
        /// A competition's loop takes over from the season's bed at the bed's own gain
        /// (<see cref="MusicFade.CompetitionPeak"/>), so its recording is what sets how loud it is: about
        /// as loud as the bed, measured whole, up to 3 dB over it for its drive and never more than a
        /// decibel under it - a competition does not go quiet, and it does not shout over the game's cues.
        /// </summary>
        [Test]
        public void TheCompetitionLoopPlaysAboutAsLoudAsTheSeasonsBed()
        {
            double bed = LoudestDecibels(Music("Season"), whole: true);
            double competition = LoudestDecibels(Music("Competition"), whole: true);
            Assert.That(competition - bed, Is.InRange(-1.0, 3.0),
                "The competition loop plays at " + competition.ToString("0.00") + " dB against the season bed's " + bed.ToString("0.00") + ".");
            Assert.That(MusicFade.CompetitionPeak, Is.EqualTo(MusicFade.SeasonPeak), "Both play at the same gain, so the recordings decide.");
        }

        /// <summary>
        /// The main menu's title loop is a soft one: its recording is about as loud as the theme's,
        /// measured whole, and it plays at 60 percent of the theme's gain (<see cref="MusicFade.TitlePeak"/>),
        /// so the menu is heard 3 to 6 dB under the theme that opens the show. And it is a loop worth
        /// sitting on a menu with: 30 to 40 seconds before it comes round.
        /// </summary>
        [Test]
        public void TheTitleLoopPlaysSofterThanTheTheme()
        {
            double theme = LoudestDecibels(Music("Theme"), whole: true);
            double title = LoudestDecibels(Music("Title"), whole: true);
            Assert.That(title - theme, Is.InRange(-1.0, 1.0),
                "The title recording is " + title.ToString("0.00") + " dB against the theme's " + theme.ToString("0.00") + ".");
            double heard = title + 20.0 * Math.Log10(MusicFade.TitlePeak) - (theme + 20.0 * Math.Log10(MusicFade.ThemePeak));
            Assert.That(heard, Is.InRange(-6.0, -3.0), "As played, the title sits " + heard.ToString("0.00") + " dB against the theme.");
            Assert.That(LengthSeconds(Music("Title")), Is.InRange(30.0, 40.0), "A 30 to 40 second loop.");
        }

        /// <summary>A 16-bit PCM WAV's length in seconds, from its format and the size of its data.</summary>
        private static double LengthSeconds(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int channels = 0, rate = 0;
            for (int at = 12; at + 8 <= bytes.Length;)
            {
                string id = Encoding.ASCII.GetString(bytes, at, 4);
                int size = BitConverter.ToInt32(bytes, at + 4);
                int body = at + 8;
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    rate = BitConverter.ToInt32(bytes, body + 4);
                }
                else if (id == "data" && channels > 0 && rate > 0)
                    return Math.Min(size, bytes.Length - body) / (2.0 * channels) / rate;
                at = body + size + (size & 1);
            }
            Assert.Fail(path + " has no data chunk after its format.");
            return 0.0;
        }

        private static string Music(string name)
        {
            string path = Path.Combine(GamesimRoot(), "Resources", "Audio", name + ".wav");
            Assert.That(File.Exists(path), Is.True, name + " has its recording.");
            return path;
        }

        private static string Recording(string cue)
        {
            string path = Path.Combine(GamesimRoot(), "Resources", "Audio", "Cues", cue + ".wav");
            Assert.That(File.Exists(path), Is.True, cue + " has its recording.");
            return path;
        }

        /// <summary>
        /// A 16-bit PCM WAV's loudest 400 milliseconds, in decibels of full scale: the mean square of
        /// every channel's samples over the window, slid a frame at a time; a recording shorter than
        /// the window is measured whole, and so is any recording when <paramref name="whole"/> is asked.
        /// </summary>
        private static double LoudestDecibels(string path, bool whole = false)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"), path);
            Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WAVE"), path);
            int format = 0, channels = 0, rate = 0, bits = 0;
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
                    int frames = Math.Min(size, bytes.Length - body) / (2 * channels);
                    Assert.That(frames, Is.GreaterThan(0), path);
                    var energy = new double[frames];
                    for (int frame = 0; frame < frames; frame++)
                    {
                        double sum = 0.0;
                        for (int channel = 0; channel < channels; channel++)
                        {
                            double sample = BitConverter.ToInt16(bytes, body + 2 * (frame * channels + channel)) / 32768.0;
                            sum += sample * sample;
                        }
                        energy[frame] = sum / channels;
                    }
                    int window = whole ? frames : Math.Min(frames, (int)(0.4 * rate));
                    double running = 0.0;
                    for (int frame = 0; frame < window; frame++) running += energy[frame];
                    double loudest = running;
                    for (int frame = window; frame < frames; frame++)
                    {
                        running += energy[frame] - energy[frame - window];
                        if (running > loudest) loudest = running;
                    }
                    return 10.0 * Math.Log10(loudest / window);
                }
                at = body + size + (size & 1);
            }
            Assert.Fail(path + " has no data chunk.");
            return 0.0;
        }

        private static string GamesimRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                if (Directory.Exists(Path.Combine(candidate, "Resources", "Audio", "Cues"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Resources/Audio/Cues was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
