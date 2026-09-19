using System;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cue = Gamesim.Presentation.HouseAudio.Cue;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The audio content (MASTER-PLAN §3.C): the theme and the season bed the swap loads, and one
    /// recorded cue per <see cref="HouseAudio.Cue"/>, each the shape the beat it marks needs.
    /// Rendered by ArtSource/audio under Blender's Python; this pins what the render must give.
    /// </summary>
    public sealed class AuthoredAudioTests
    {
        private const string Root = "Assets/Gamesim/Resources/Audio/";

        [Test]
        public void TheThemeAndTheSeasonBedShipAsLoopsAtFullRate()
        {
            var theme = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Theme.wav");
            var season = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Season.wav");
            Assert.That(theme, Is.Not.Null, "Resources/Audio/Theme is what makes HasRecordedMusic true.");
            Assert.That(season, Is.Not.Null, "Resources/Audio/Season plays for hours; it must exist.");
            Assert.That(theme.frequency, Is.EqualTo(44100));
            Assert.That(theme.channels, Is.EqualTo(2), "The beds are stereo.");
            Assert.That(theme.length, Is.InRange(12f, 40f), "The theme is a fanfare over the opening, not a bed.");
            Assert.That(season.length, Is.InRange(40f, 120f), "The season bed is long enough not to wear.");
        }

        [Test]
        public void EveryCueHasARecordingNamedForIt()
        {
            foreach (Cue cue in Enum.GetValues(typeof(Cue)))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Cues/" + cue + ".wav");
                Assert.That(clip, Is.Not.Null, "Resources/" + HouseAudio.CueResourceFolder + cue + " is the recorded cue.");
                Assert.That(clip.length, Is.InRange(0.04f, 2.5f), cue + " is a cue, not a bed.");
                Assert.That(clip.frequency, Is.EqualTo(44100), cue + " is at full rate.");
            }
        }

        [Test]
        public void TheButtonIsTheShortestCueAndTheFinaleTheLongest()
        {
            var lengths = Enum.GetValues(typeof(Cue)).Cast<Cue>()
                .ToDictionary(cue => cue, cue => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Cues/" + cue + ".wav")?.length ?? 0f);
            Assert.That(lengths[Cue.Button], Is.EqualTo(lengths.Values.Min()), "A button click gets out of the way.");
            Assert.That(lengths[Cue.Finale], Is.EqualTo(lengths.Values.Max()), "The finale is the one cue allowed to linger.");
            Assert.That(lengths[Cue.Eviction], Is.GreaterThan(lengths[Cue.Vote]), "An eviction lands harder than a ballot.");
        }
    }
}
