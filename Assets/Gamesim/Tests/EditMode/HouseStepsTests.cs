using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The player's footsteps, as numbers: a foot lands every stride the body's speed implies, and
    /// none while it stands or is moved faster than a walk; each room's ground has four takes that
    /// ship; the takes go in turn at a small change of pitch from a generator of their own; and a
    /// step far from the camera's focus is quiet. Unity-free, so Tools/SimulationTests runs it, all
    /// but the last case, which reads the body's own takes; EpisodePlayModeTests'
    /// Footsteps_AWalkAcrossTheKitchenLandsOnTileEveryStrideAndReduceSoundSilencesIt pins the house's side.
    /// </summary>
    public sealed class HouseStepsTests
    {
        /// <summary>The frame lengths a walk is run at: 60 and 30 fps, a long frame, and a batchmode frame.</summary>
        private static readonly float[] Frames = { 1f / 60f, 1f / 30f, .1f, .00043f };

        private const float Near = 1e-3f;

        /// <summary>The steps a clock lands while a body covers <paramref name="metres"/> at <paramref name="speed"/>, a frame of <paramref name="frame"/> at a time.</summary>
        private static int Walk(StepClock clock, float metres, float speed, bool running, float frame)
        {
            int steps = 0;
            double covered = 0.0;
            while (covered < metres - 1e-6)
            {
                float seconds = (float)Math.Min(frame, (metres - covered) / speed);
                if (clock.Advance(seconds * speed, seconds, running)) steps++;
                covered += seconds * speed;
            }
            return steps;
        }

        [Test]
        public void AFootLandsEveryStrideTheBodysSpeedImplies()
        {
            Assert.That(HouseSteps.Stride(2.2f, false), Is.EqualTo(HouseSteps.WalkTakeSpeed * HouseSteps.WalkCycleSeconds / 2f).Within(Near),
                "At the house's walk the stride is the walk take's own,");
            Assert.That(HouseSteps.Stride(2.2f, false), Is.EqualTo(.868f).Within(.002f), "0.87 m;");
            Assert.That(HouseSteps.StepSeconds(2.2f, false) * HouseSteps.Pace(2.2f, false), Is.EqualTo(HouseSteps.WalkCycleSeconds / 2f).Within(Near),
                "a foot lands twice in each cycle of the take at the animator's pace;");
            Assert.That(HouseSteps.Stride(4f, true), Is.EqualTo(HouseSteps.RunTakeSpeed * HouseSteps.RunCycleSeconds / 2f).Within(Near),
                "at the run it is the run take's own;");
            Assert.That(HouseSteps.Stride(1f, false), Is.EqualTo(1f * HouseSteps.WalkCycleSeconds / 2f / HouseSteps.SlowestPace).Within(Near),
                "slower than the take is let play, the steps are shorter,");
            Assert.That(HouseSteps.Stride(3f, false), Is.EqualTo(3f * HouseSteps.WalkCycleSeconds / 2f / HouseSteps.FastestPace).Within(Near),
                "and faster, longer.");

            foreach (float frame in Frames)
            {
                var walk = new StepClock();
                float stride = HouseSteps.Stride(2.2f, false);
                int expected = (int)Math.Floor(10f / stride + HouseSteps.FirstStep);
                Assert.That(Walk(walk, 10f, 2.2f, false, frame), Is.EqualTo(expected),
                    "Ten metres at a walk land a foot every stride, the first a quarter of a stride in, at a frame of " + frame + " s.");
                Assert.That(expected, Is.EqualTo(12));
                var run = new StepClock();
                Assert.That(Walk(run, 10f, 4f, true, frame), Is.EqualTo((int)Math.Floor(10f / HouseSteps.Stride(4f, true) + HouseSteps.FirstStep)),
                    "Ten metres at a run land fewer, longer steps, at a frame of " + frame + " s.");
            }

            var first = new StepClock();
            float quarter = (1f - HouseSteps.FirstStep) * HouseSteps.Stride(2.2f, false);
            Assert.That(Walk(first, quarter - .01f, 2.2f, false, 1f / 60f), Is.EqualTo(0), "Setting off, no foot lands before a quarter of a stride");
            Assert.That(Walk(first, .02f, 2.2f, false, 1f / 60f), Is.EqualTo(1), "and the first lands at it.");
        }

        [Test]
        public void AStandingBodyAWarpOrAStallLandsNoFoot()
        {
            var clock = new StepClock();
            for (int i = 0; i < 100; i++)
                Assert.That(clock.Advance(.001f, 1f / 60f, false), Is.False, "A body shifting its weight on the spot lands no foot.");
            Assert.That(clock.Walking, Is.False);

            Assert.That(Walk(clock, .37f, 2.2f, false, 1f / 30f), Is.EqualTo(1), "Under way, the first foot lands;");
            float phase = clock.Phase;
            Assert.That(clock.Advance(5f, 1f / 30f, false), Is.False, "a warp of five metres in a frame lands none");
            Assert.That(clock.Phase, Is.EqualTo(phase), "and the walk keeps its place in the stride;");
            Assert.That(clock.Advance(0f, 1f / 30f, false), Is.False, "nor does a frame's stall,");
            Assert.That(clock.Walking, Is.True, "which is still the same walk.");
            for (int i = 0; i < 7; i++) clock.Advance(0f, 1f / 30f, false);
            Assert.That(clock.Walking, Is.False, "Stood for a fifth of a second, the body has stopped,");
            Assert.That(clock.Phase, Is.EqualTo(0f), "and its next walk starts afresh.");

            Assert.That(clock.Advance(.1f, 0f, false), Is.False, "No time, no step;");
            Assert.That(clock.Advance(float.NaN, 1f / 30f, false), Is.False, "nor from a body nowhere,");
            Assert.That(clock.Advance(-1f, 1f / 30f, false), Is.False, "or a distance that cannot be.");
            Assert.That(clock.Walking, Is.False);
            Walk(clock, .3f, 2.2f, false, 1f / 30f);
            clock.Halt();
            Assert.That(clock.Walking || clock.Phase != 0f, Is.False, "Sitting down stops the walk where it is.");
        }

        [Test]
        public void EachRoomsGroundHasFourTakesThatShip()
        {
            // The rooms by their ids (RoomLayers.RoomIds), the floors as HouseFloorDressing lays them.
            Assert.That(new[] { "Living", "Bedroom", "Private", "HoH", "Nomination" }.Select(room => HouseSteps.SurfaceOf(room, 0f)),
                Is.All.EqualTo(StepSurface.Floor), "The wooden floors are wood;");
            Assert.That(HouseSteps.SurfaceOf("Kitchen", 0f), Is.EqualTo(StepSurface.Tile), "the kitchen is tile,");
            Assert.That(HouseSteps.SurfaceOf("Games", 0f), Is.EqualTo(StepSurface.Tile), "as the games room is;");
            Assert.That(HouseSteps.SurfaceOf("Yard", 0f), Is.EqualTo(StepSurface.Grass), "the yard is lawn,");
            Assert.That(HouseSteps.SurfaceOf("Yard", .1f), Is.EqualTo(StepSurface.Grass), "a little over it still lawn,");
            Assert.That(HouseSteps.SurfaceOf("Yard", .35f), Is.EqualTo(StepSurface.Deck), "and up on the pool's deck, deck;");
            Assert.That(HouseSteps.SurfaceOf("Kitchen", .35f), Is.EqualTo(StepSurface.Tile), "raised indoors is still the room's floor;");
            Assert.That(HouseSteps.SurfaceOf(null, 0f), Is.EqualTo(StepSurface.Floor), "and no room known is wood.");

            Assert.That(HouseSteps.ClipName(StepSurface.Floor, 0), Is.EqualTo("Floor_1"));
            Assert.That(HouseSteps.ClipName(StepSurface.Deck, 3), Is.EqualTo("Deck_4"));
            Assert.That(HouseSteps.ClipName(StepSurface.Tile, 4), Is.EqualTo("Tile_1"), "The takes come round.");

            string folder = Path.Combine(GamesimRoot(), "Resources", "Audio", "Steps");
            var names = Enum.GetValues(typeof(StepSurface)).Cast<StepSurface>()
                .SelectMany(surface => Enumerable.Range(0, HouseSteps.Variations).Select(take => HouseSteps.ClipName(surface, take))).ToArray();
            Assert.That(names, Has.Length.EqualTo(16));
            foreach (var name in names)
            {
                string path = Path.Combine(folder, name + ".wav");
                Assert.That(File.Exists(path), Is.True, "Resources/" + HouseSteps.ResourceFolder + name + " is a take of a step.");
                ReadWav(path, out int rate, out int channels, out double seconds, out double loudest);
                Assert.That(rate, Is.EqualTo(44100), name + " is at full rate.");
                Assert.That(channels, Is.EqualTo(2), name + " is stereo, as the house's other sounds are.");
                Assert.That(seconds, Is.InRange(.2, .6), name + " is a step, not a loop.");
                Assert.That(loudest, Is.InRange(-30.0, -20.0), name + " is heard over the room's bed and under the news (" + loudest.ToString("0.00") + " dB).");
            }
            var shipped = Directory.GetFiles(folder, "*.wav").Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.That(shipped, Is.EqualTo(names.OrderBy(name => name, StringComparer.Ordinal).ToArray()), "Every file in the folder is a take of a step.");
        }

        [Test]
        public void TheTakesGoInTurnAtASmallPitchAndAFarStepIsQuiet()
        {
            var steps = new StepVariations();
            Assert.That(Enumerable.Range(0, 8).Select(_ => steps.Take()), Is.EqualTo(new[] { 0, 1, 2, 3, 0, 1, 2, 3 }), "The four takes go in turn.");

            var pitches = Enumerable.Range(0, 200).Select(_ => steps.Pitch()).ToArray();
            Assert.That(pitches, Is.All.InRange(1f - HouseSteps.PitchJitter, 1f + HouseSteps.PitchJitter), "Each a little higher or lower,");
            Assert.That(pitches.Max() - pitches.Min(), Is.GreaterThan(HouseSteps.PitchJitter), "and not all alike.");
            var again = new StepVariations();
            Assert.That(Enumerable.Range(0, 200).Select(_ => again.Pitch()), Is.EqualTo(pitches),
                "The steps' own generator, seeded here: the same every session, and drawing on nothing the season draws on.");
            var other = new StepVariations(StepVariations.Seed + 1);
            Assert.That(Enumerable.Range(0, 200).Select(_ => other.Pitch()), Is.Not.EqualTo(pitches));

            Assert.That(HouseSteps.GainAt(0f), Is.EqualTo(1f), "A step where the camera looks is at full level,");
            Assert.That(HouseSteps.GainAt(HouseSteps.NearMetres), Is.EqualTo(1f), "and near it too;");
            float last = 1f;
            for (float metres = HouseSteps.NearMetres + .5f; metres < HouseSteps.FarMetres; metres += .5f)
            {
                float gain = HouseSteps.GainAt(metres);
                Assert.That(gain, Is.LessThan(last).And.GreaterThan(0f), "farther off it is quieter (" + metres + " m),");
                last = gain;
            }
            Assert.That(HouseSteps.GainAt(HouseSteps.FarMetres), Is.EqualTo(0f), "and across the house it is not heard.");
            Assert.That(HouseSteps.GainAt(40f), Is.EqualTo(0f));
            Assert.That(HouseSteps.GainAt(float.NaN), Is.EqualTo(0f));
            Assert.That(HouseSteps.Gain, Is.EqualTo(1f).Within(Near), "The steps play as rendered.");
        }

#if UNITY_5_3_OR_NEWER
        /// <summary>The stride is the body's: the takes' speeds and the animator's pace range as CharacterPresentation plays them.</summary>
        [Test]
        public void TheStridesAreTheBodysOwnTakes()
        {
            Assert.That(HouseSteps.WalkTakeSpeed, Is.EqualTo(CharacterPresentation.WalkTakeSpeed));
            Assert.That(HouseSteps.RunTakeSpeed, Is.EqualTo(CharacterPresentation.RunTakeSpeed));
            Assert.That(HouseSteps.SlowestPace, Is.EqualTo(CharacterPresentation.SlowestPace));
            Assert.That(HouseSteps.FastestPace, Is.EqualTo(CharacterPresentation.FastestPace));
        }
#endif

        /// <summary>A 16-bit PCM WAV's rate, channels, length and the RMS of its loudest tenth of a second, in dBFS, from its first channel.</summary>
        private static void ReadWav(string path, out int rate, out int channels, out double seconds, out double loudestDb)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"), path);
            Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WAVE"), path);
            rate = 0; channels = 0;
            int bits = 0, format = 0, at = 12;
            while (at + 8 <= bytes.Length)
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
                    int frames = Math.Min(size, bytes.Length - body) / 2 / channels;
                    int window = rate / 10;
                    Assert.That(frames, Is.GreaterThan(window), path);
                    var power = new double[frames + 1];
                    for (int i = 0; i < frames; i++)
                    {
                        double sample = BitConverter.ToInt16(bytes, body + 2 * i * channels) / 32768.0;
                        power[i + 1] = power[i] + sample * sample;
                    }
                    double loudest = 0.0;
                    for (int i = 0; i + window <= frames; i++) loudest = Math.Max(loudest, (power[i + window] - power[i]) / window);
                    seconds = frames / (double)rate;
                    loudestDb = 10.0 * Math.Log10(Math.Max(loudest, 1e-12));
                    return;
                }
                at = body + size + (size & 1);
            }
            Assert.Fail(path + " has no data chunk.");
            seconds = 0.0; loudestDb = 0.0;
        }

        private static string GamesimRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                if (Directory.Exists(Path.Combine(candidate, "Resources", "Audio", "Steps"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Resources/Audio/Steps was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
