using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house's beds, as numbers (§3.C room tone per room): a room crossed is a crossfade over
    /// three quarters of a second at a constant rate, a turn back mid-fade carries on from where it
    /// was, a third bed takes the quieter voice, the director keeps the last room for half a second
    /// when the focus is in none, and every room's bed is about as loud as the living room's.
    /// Unity-free, so Tools/SimulationTests runs it; EpisodePlayModeTests.Audio pins the house's side.
    /// </summary>
    public sealed class RoomToneBlendTests
    {
        /// <summary>The frame lengths every fade is run at: 60 and 30 fps, a coarse step, and a batchmode frame.</summary>
        private static readonly float[] Frames = { 1f / 60f, 1f / 30f, 0.1f, 0.00043f };

        private const float Near = 1e-3f;

        /// <summary>The blend moved on <paramref name="total"/> seconds in steps of <paramref name="frame"/>, the last whatever is left.</summary>
        private static void Run(RoomToneBlend blend, double total, float frame)
        {
            double elapsed = 0.0;
            while (elapsed < total - 1e-9)
            {
                float step = (float)Math.Min(frame, total - elapsed);
                blend.Tick(step);
                elapsed += step;
            }
        }

        [Test]
        public void ARoomCrossedFadesOneBedOutAndTheNextInOverThreeQuartersOfASecond()
        {
            foreach (var frame in Frames)
            {
                var blend = new RoomToneBlend();
                int kitchen = blend.Enter("Kitchen", out bool given);
                Assert.That(given, Is.True, "A first bed is loaded into a voice.");
                Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0f), "It starts from silence.");
                Run(blend, RoomToneBlend.CrossfadeSeconds / 2, frame);
                Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0.5f).Within(Near), "Halfway up at half the time, in steps of " + frame + " s.");
                Run(blend, RoomToneBlend.CrossfadeSeconds / 2, frame);
                Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(1f), "Full at 0.75 s.");

                int yard = blend.Enter("Yard", out given);
                Assert.That(given, Is.True, "The next room's bed is loaded into the other voice");
                Assert.That(yard, Is.Not.EqualTo(kitchen), "and the kitchen's keeps sounding.");
                Run(blend, RoomToneBlend.CrossfadeSeconds / 2, frame);
                Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0.5f).Within(Near), "The kitchen is halfway down");
                Assert.That(blend.LevelOf("Yard"), Is.EqualTo(0.5f).Within(Near), "as the yard is halfway up.");
                Assert.That(blend.Wanted(kitchen) && blend.Wanted(yard), Is.True, "Both voices sound mid-fade.");
                Run(blend, RoomToneBlend.CrossfadeSeconds / 2, frame);
                Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0f), "The kitchen reaches silence at 0.75 s");
                Assert.That(blend.LevelOf("Yard"), Is.EqualTo(1f), "and the yard full.");
                Assert.That(blend.Wanted(kitchen), Is.False, "A bed faded out is let go.");
                Assert.That(blend.Current, Is.EqualTo(yard));
            }
        }

        [Test]
        public void ATurnBackMidFadeCarriesOnFromWhereItHadGot()
        {
            var blend = new RoomToneBlend();
            int kitchen = blend.Enter("Kitchen", out _);
            blend.Tick(RoomToneBlend.CrossfadeSeconds);
            int yard = blend.Enter("Yard", out _);
            blend.Tick(0.3f);
            Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0.6f).Within(Near));
            Assert.That(blend.Enter("Kitchen", out bool given), Is.EqualTo(kitchen), "Turning back finds the kitchen's own voice");
            Assert.That(given, Is.False, "still carrying its bed, so nothing restarts.");
            blend.Tick(0.15f);
            Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0.8f).Within(Near), "The kitchen rises from 0.6, not from silence or full,");
            Assert.That(blend.LevelOf("Yard"), Is.EqualTo(0.2f).Within(Near), "and the yard falls from 0.4.");
            blend.Tick(0.15f);
            Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(1f));
            Assert.That(blend.Wanted(yard), Is.False, "The yard has faded out");
            Assert.That(blend.KeyOf(yard), Is.EqualTo("Yard"), "and its voice keeps the bed");
            Assert.That(blend.Enter("Yard", out given), Is.EqualTo(yard));
            Assert.That(given, Is.False, "for a return, which picks up where the yard left off.");
        }

        [Test]
        public void AThirdBedMidFadeTakesTheQuieterVoice()
        {
            var blend = new RoomToneBlend();
            int kitchen = blend.Enter("Kitchen", out _);
            blend.Tick(RoomToneBlend.CrossfadeSeconds);
            int second = blend.Enter("Yard", out _);
            blend.Tick(0.15f);
            Assert.That(blend.Enter("Living", out bool given), Is.EqualTo(second), "The yard, at 0.2, is the quieter: the living room takes its voice,");
            Assert.That(given, Is.True);
            Assert.That(blend.LevelOf("Yard"), Is.EqualTo(0f), "the yard goes,");
            blend.Tick(0.15f);
            Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0.6f).Within(Near), "and the kitchen carries on down from 0.8.");
            Assert.That(blend.LevelOf("Living"), Is.EqualTo(0.2f).Within(Near));

            blend.Tick(0.3f);
            Assert.That(blend.Enter("Games", out given), Is.EqualTo(kitchen), "The kitchen, at 0.2 under the living room's 0.6, is the quieter now.");
            Assert.That(given, Is.True);
            Assert.That(blend.TargetOf(second), Is.EqualTo(0f), "The living room falls from where it is.");
            Assert.That(blend.LevelOf("Living"), Is.EqualTo(0.6f).Within(Near));
        }

        [Test]
        public void TheGenericAirIsABedLikeAnyRoomAndSilenceTakesEveryBedAtOnce()
        {
            var blend = new RoomToneBlend();
            int air = blend.Enter(null, out _);
            Assert.That(blend.KeyOf(air), Is.EqualTo(RoomToneBlend.Air), "No room is the generic air.");
            blend.Tick(RoomToneBlend.CrossfadeSeconds);
            int kitchen = blend.Enter("Kitchen", out _);
            blend.Tick(0.3f);
            Assert.That(blend.LevelOf(null), Is.EqualTo(0.6f).Within(Near), "Entering a room fades the air out");
            Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0.4f).Within(Near), "as the room comes in.");
            Assert.That(RoomToneBlend.PeakOf(RoomToneBlend.Air), Is.EqualTo(RoomToneBlend.AirPeak));
            Assert.That(RoomToneBlend.PeakOf("Kitchen"), Is.EqualTo(RoomToneBlend.RoomPeak));

            blend.Silence();
            Assert.That(blend.LevelOf(null) + blend.LevelOf("Kitchen"), Is.EqualTo(0f), "Muted or reduced: every bed is silent at once,");
            Assert.That(blend.Wanted(air), Is.False, "the air that was going is let go,");
            Assert.That(blend.Wanted(kitchen), Is.True, "and the kitchen is still the bed asked for");
            blend.Tick(RoomToneBlend.CrossfadeSeconds / 2);
            Assert.That(blend.LevelOf("Kitchen"), Is.EqualTo(0.5f).Within(Near), "which comes back in from silence.");
        }

        [Test]
        public void TheDirectorHoldsTheLastRoomForHalfASecondBeforeTheAir()
        {
            float missed = float.NaN;
            Assert.That(RoomToneBlend.HeldRoom("Kitchen", null, 10f, ref missed), Is.EqualTo("Kitchen"));
            Assert.That(float.IsNaN(missed), Is.True, "A room found is no miss.");
            Assert.That(RoomToneBlend.HeldRoom(null, "Kitchen", 10.25f, ref missed), Is.EqualTo("Kitchen"), "A doorway keeps the kitchen,");
            Assert.That(RoomToneBlend.HeldRoom(null, "Kitchen", 10.5f, ref missed), Is.EqualTo("Kitchen"), "a quarter of a second on too,");
            Assert.That(RoomToneBlend.HeldRoom(null, "Kitchen", 10.75f, ref missed), Is.Null, "and at half a second the air takes over.");

            missed = float.NaN;
            Assert.That(RoomToneBlend.HeldRoom(null, "Kitchen", 20f, ref missed), Is.EqualTo("Kitchen"));
            Assert.That(RoomToneBlend.HeldRoom("Living", "Kitchen", 20.25f, ref missed), Is.EqualTo("Living"), "A room found in the hold is the next room");
            Assert.That(float.IsNaN(missed), Is.True, "and ends the miss,");
            Assert.That(RoomToneBlend.HeldRoom(null, "Living", 20.5f, ref missed), Is.EqualTo("Living"), "so the next doorway is held afresh.");
            Assert.That(RoomToneBlend.HeldRoom(null, null, 30f, ref missed), Is.Null, "With no room to hold, none is told.");
        }

        [Test]
        public void EveryRoomsBedIsWithinThreeDecibelsOfTheLivingRoomsAsItIsPlayed()
        {
            string folder = Path.Combine(GamesimRoot(), "Resources", "Audio", "Rooms");
            var beds = Directory.GetFiles(folder, "*.wav").OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(path => Path.GetFileNameWithoutExtension(path),
                    path => RmsDecibels(path) + 20.0 * Math.Log10(RoomToneBlend.PeakOf(Path.GetFileNameWithoutExtension(path))));
            Assert.That(beds.Count, Is.GreaterThanOrEqualTo(8), "Every room has its bed.");
            Assert.That(beds.ContainsKey("Living"), Is.True);
            double living = beds["Living"];
            foreach (var bed in beds)
                Assert.That(Math.Abs(bed.Value - living), Is.LessThanOrEqualTo(3.0),
                    bed.Key + " plays at " + bed.Value.ToString("0.00") + " dB against the living room's " + living.ToString("0.00") + ".");
        }

        /// <summary>A 16-bit PCM WAV's level over all its samples and channels, in decibels of full scale.</summary>
        private static double RmsDecibels(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"), path);
            Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WAVE"), path);
            int format = 0, bits = 0;
            for (int at = 12; at + 8 <= bytes.Length;)
            {
                string id = Encoding.ASCII.GetString(bytes, at, 4);
                int size = BitConverter.ToInt32(bytes, at + 4);
                int body = at + 8;
                if (id == "fmt ")
                {
                    format = BitConverter.ToInt16(bytes, body);
                    bits = BitConverter.ToInt16(bytes, body + 14);
                }
                else if (id == "data")
                {
                    Assert.That(format, Is.EqualTo(1), path + " is PCM.");
                    Assert.That(bits, Is.EqualTo(16), path + " is 16-bit.");
                    int count = Math.Min(size, bytes.Length - body) / 2;
                    double sum = 0.0;
                    for (int i = 0; i < count; i++)
                    {
                        double sample = BitConverter.ToInt16(bytes, body + 2 * i) / 32768.0;
                        sum += sample * sample;
                    }
                    Assert.That(count, Is.GreaterThan(0), path);
                    return 10.0 * Math.Log10(sum / count);
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
                if (Directory.Exists(Path.Combine(candidate, "Resources", "Audio", "Rooms"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Resources/Audio/Rooms was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
