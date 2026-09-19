using System.Collections;
using System.Linq;
using Gamesim.House;
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
        public IEnumerator Audio_TheRoomToneFollowsTheCamerasFocus()
        {
            var audio = director.GetComponent<HouseAudio>();
            var kitchen = SceneComponents<HouseRoomMarker>().First(m => m.RoomName == "Kitchen");
            var yard = SceneComponents<HouseRoomMarker>().First(m => m.RoomName == "Yard");
            cameraRig.MoveTo(kitchen.transform.position, 12f);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (director.RoomUnderCamera != "Kitchen" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.RoomUnderCamera, Is.EqualTo("Kitchen"), "The camera looks into the kitchen.");
            Assert.That(audio.CurrentRoom, Is.EqualTo("Kitchen"), "and the audio hears it");
            Assert.That(audio.RoomTonePlaying, Is.True, "with the kitchen's bed playing");
            cameraRig.MoveTo(yard.transform.position, 12f);
            deadline = Time.realtimeSinceStartup + 3f;
            while (director.RoomUnderCamera != "Yard" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(audio.CurrentRoom, Is.EqualTo("Yard"), "and the yard's when it moves on.");
        }

        [UnityTest]
        public IEnumerator Audio_PanelsAndHoversHaveTheirFoley()
        {
            var audio = director.GetComponent<HouseAudio>();
            director.ClosePanels();
            yield return null;
            director.OpenSettings();
            yield return null;
            Assert.That(audio.LastCue, Is.EqualTo(HouseAudio.Cue.PanelOpen), "A panel arriving says so.");
            var button = ButtonWithCaption("Close  [Esc]");
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            Assert.That(audio.LastCue, Is.EqualTo(HouseAudio.Cue.Hover), "The pointer arriving over a control ticks.");
            director.ClosePanels();
            yield return null;
            Assert.That(audio.LastCue, Is.EqualTo(HouseAudio.Cue.PanelClose), "and a panel leaving says so.");
        }

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
            Assert.That(audio.RoomTonePlaying, Is.False, "and so does a room's own bed");
            Assert.That(ButtonWithCaption("Full sound"), Is.Not.Null, "The control now offers the way back.");

            ButtonWithCaption("Full sound").onClick.Invoke();
            yield return null;
            Assert.That(director.ReducedAudio, Is.False);
            Assert.That(audio.CueVolume, Is.EqualTo(full).Within(0.001f));
            director.ClosePanels();
        }
    }
}
