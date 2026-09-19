using System.Collections;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The reduced-audio preference (MASTER-PLAN §3.C): the settings control stops the room tone
    /// and halves the cues and the music, every cue still sounds, and the control flips back.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Audio_ReducedSoundStopsTheRoomToneAndSoftensTheCues()
        {
            var audio = director.GetComponent<HouseAudio>();
            Assert.That(audio, Is.Not.Null, "The director carries the house's audio.");
            director.OpenSettings();
            yield return null;
            Assert.That(director.ReducedAudio, Is.False, "A fresh episode plays full sound.");
            float full = audio.CueVolume;
            Assume.That(full, Is.GreaterThan(0f));

            ButtonWithCaption("Reduce sound").onClick.Invoke();
            yield return null;
            Assert.That(director.ReducedAudio, Is.True);
            Assert.That(audio.ReducedAudio, Is.True, "The preference reaches the audio.");
            Assert.That(audio.CueVolume, Is.EqualTo(full * 0.5f).Within(0.001f), "The cues play at half.");
            Assert.That(audio.AmbiencePlaying, Is.False, "The room tone stops.");
            Assert.That(ButtonWithCaption("Full sound"), Is.Not.Null, "The control now offers the way back.");

            ButtonWithCaption("Full sound").onClick.Invoke();
            yield return null;
            Assert.That(director.ReducedAudio, Is.False);
            Assert.That(audio.CueVolume, Is.EqualTo(full).Within(0.001f));
            director.ClosePanels();
        }
    }
}
