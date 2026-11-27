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
        [Test]
        public void Audio_FirstEnabledSourcesRespectTheSavedMasterEffectsAndReducedSettings()
        {
            var originalRoot = Gamesim.Episode.EpisodeDirector.SaveRootOverride;
            bool originalRoute = Gamesim.Episode.EpisodeDirector.PreferencesBesideSaves;
            try
            {
                string root = System.IO.Path.Combine(temporaryDirectory, "audio-start");
                System.IO.Directory.CreateDirectory(root);
                Gamesim.Episode.EpisodeDirector.SaveRootOverride = root;
                Gamesim.Episode.EpisodeDirector.PreferencesBesideSaves = true;
                var prefs = Gamesim.Episode.EpisodeDirector.LaunchPreferences();
                Assert.That(prefs, Is.TypeOf<Gamesim.Persistence.RootFileStore>());
                foreach (var values in new[] { (0, 100, 0, 0), (70, 0, 0, 0), (70, 100, 1, 0), (70, 100, 0, 1), (70, 100, 0, 0) })
                {
                    prefs.SetInt("Gamesim.Volume", values.Item1);
                    prefs.SetInt(SoundPictures.EffectsVolumeKey, values.Item2);
                    prefs.SetInt("Gamesim.ReducedAudio", values.Item3);
                    prefs.SetInt("Gamesim.Muted", values.Item4);
                    prefs.Save();
                    var host = new GameObject("First audible frame");
                    try
                    {
                        var audio = host.AddComponent<HouseAudio>();
                        bool hear = values.Item1 > 0 && values.Item2 > 0 && values.Item3 == 0 && values.Item4 == 0;
                        Assert.That(audio.Volume, Is.EqualTo(values.Item1 / 100f).Within(.001f));
                        Assert.That(audio.AmbiencePlaying, Is.EqualTo(hear));
                        audio.SetRoom("Kitchen");
                        Assert.That(audio.RoomTonePlaying, Is.EqualTo(hear));
                        audio.enabled = false;
                        Assert.That(audio.RoomTonePlaying, Is.False);
                        audio.enabled = true;
                        Assert.That(audio.RoomTonePlaying, Is.EqualTo(hear));
                    }
                    finally { Object.DestroyImmediate(host); }
                }
            }
            finally
            {
                Gamesim.Episode.EpisodeDirector.SaveRootOverride = originalRoot;
                Gamesim.Episode.EpisodeDirector.PreferencesBesideSaves = originalRoute;
            }
        }

        [UnityTest]
        public IEnumerator Audio_QuickBallotsKeepEachMarkerAndUseOneAttackPerFrame()
        {
            var audio = director.GetComponent<HouseAudio>();
            audio.SetMuted(false);
            audio.SetVolume(.35f);
            audio.SetChannelVolumes(1f, 1f, 1f);
            yield return null;
            int attacks = audio.CueVoicesStarted;
            var markers = new System.Collections.Generic.List<HouseAudio.Cue>();
            audio.CueAsked += markers.Add;
            try
            {
                for (int i = 0; i < 3; i++) audio.PlayCue(HouseAudio.Cue.Vote);
                audio.PlayCue(HouseAudio.Cue.Eviction);
                Assert.That(markers, Is.EqualTo(new[] { HouseAudio.Cue.Vote, HouseAudio.Cue.Vote, HouseAudio.Cue.Vote, HouseAudio.Cue.Eviction }));
                Assert.That(audio.CueVoicesStarted - attacks, Is.EqualTo(2));
                yield return null;
                int beforeNext = audio.CueVoicesStarted;
                audio.PlayCue(HouseAudio.Cue.Vote);
                Assert.That(audio.CueVoicesStarted - beforeNext, Is.EqualTo(1));
            }
            finally { audio.CueAsked -= markers.Add; }
        }

        /// <summary>
        /// Every cue's recording is decoded when the house's audio starts rather than by its first
        /// sounding, and a hover asked again inside its spacing is counted but not heard.
        /// </summary>
        [Test]
        public void Audio_EveryCueIsDecodedAtTheStart_AndAQuickSecondHoverIsCountedButNotHeard()
        {
            var cues = (HouseAudio.Cue[])System.Enum.GetValues(typeof(HouseAudio.Cue));
            // Each recording let go first, so a clip an earlier test sounded cannot pass for one decoded here.
            foreach (var cue in cues)
            {
                var recording = Resources.Load<AudioClip>(HouseAudio.CueResourceFolder + cue);
                Assert.That(recording, Is.Not.Null, cue + " ships a recording.");
                recording.UnloadAudioData();
                Assert.That(recording.loadState, Is.EqualTo(AudioDataLoadState.Unloaded), cue + " is let go before the house starts.");
            }
            var host = new GameObject("Cue preload");
            try
            {
                var audio = host.AddComponent<HouseAudio>();
                Assert.That(audio.CuesPreloaded, Is.EqualTo(cues.Length), "Every cue is decoded when the house's audio starts.");
                audio.SetMuted(false);
                audio.SetVolume(.35f);
                audio.SetChannelVolumes(1f, 1f, 1f);
                int asked = audio.CuesAsked, voices = audio.CueVoicesStarted;
                audio.PlayCue(HouseAudio.Cue.Hover);
                audio.PlayCue(HouseAudio.Cue.Hover);
                Assert.That(audio.CuesAsked - asked, Is.EqualTo(2), "Both hovers are asked for and counted,");
                Assert.That(audio.LastCue, Is.EqualTo(HouseAudio.Cue.Hover));
                Assert.That(audio.CueVoicesStarted - voices, Is.EqualTo(1), "but the second, in the same instant, is not heard.");
                audio.PlayCue(HouseAudio.Cue.Button);
                Assert.That(audio.CueVoicesStarted - voices, Is.EqualTo(2), "A press beside it still sounds.");
            }
            finally { Object.DestroyImmediate(host); }
        }

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
            director.OpenSettingsAt(SettingsSection.Sound);
            yield return null;
            PressSettingRow(SettingRowWords.MusicRow, "Off");
            Assert.That(audio.RoomTonePlaying, Is.True, "Turning off the score keeps the kitchen under Effects.");
            PressSettingRow(SettingRowWords.MusicRow, "On");
            director.ClosePanels();
            yield return null;
            // A free move is a watching view from RS5: the room view watches the kitchen and draws it alone.
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(RoomLayers.IndexOf("Kitchen")), "The free move to the kitchen's marker watches the kitchen,");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "and the view draws it.");
            // The beds crossfade on the real clock (RoomToneBlend.CrossfadeSeconds, 0.75 s, no more
            // than a twentieth of a second a frame): waited on the clock, with room for slow frames.
            float fade = Time.realtimeSinceStartup + 10f;
            while (audio.BedLevel("Kitchen") < 1f && Time.realtimeSinceStartup < fade) yield return null;
            Assert.That(audio.BedLevel("Kitchen"), Is.EqualTo(1f), "The kitchen's bed fades all the way in.");
            cameraRig.MoveTo(yard.transform.position, 12f);
            deadline = Time.realtimeSinceStartup + 3f;
            while (director.RoomUnderCamera == "Kitchen" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.RoomUnderCamera, Is.Not.EqualTo("Kitchen"), "The camera leaves the kitchen,");
            Assert.That(audio.BedVoicesSounding, Is.EqualTo(2), "and the next bed comes in under the kitchen's:");
            Assert.That(audio.BedLevel("Kitchen"), Is.GreaterThan(0.5f), "the kitchen fades out rather than cutting.");
            while (director.RoomUnderCamera != "Yard" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(audio.CurrentRoom, Is.EqualTo("Yard"), "and the yard's when it moves on.");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Yard")), "The free move to the yard's marker watches the yard.");
            fade = Time.realtimeSinceStartup + 10f;
            while ((audio.BedLevel("Kitchen") > 0f || audio.BedVoicesSounding != 1) && Time.realtimeSinceStartup < fade) yield return null;
            Assert.That(audio.BedLevel("Kitchen"), Is.EqualTo(0f), "The kitchen's bed reaches silence");
            Assert.That(audio.BedVoicesSounding, Is.EqualTo(1), "and lets its voice go,");
            Assert.That(audio.RoomTonePlaying, Is.True, "leaving the yard's bed alone.");
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
            var button = ButtonWithCaption(Gamesim.Presentation.WayOut.CloseCaption);
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            Assert.That(audio.LastCue, Is.EqualTo(HouseAudio.Cue.Hover), "The pointer arriving over a control ticks.");
            director.ClosePanels();
            yield return null;
            Assert.That(audio.LastCue, Is.EqualTo(HouseAudio.Cue.PanelClose), "and a panel leaving says so.");
        }

        /// <summary>
        /// Every press answers (AU-10). On the main menu the pointer over a control ticks and a press
        /// that opens no panel clicks; the cast screen's way back clicks as well; Settings, which opens a
        /// panel, sounds the panel alone; a tab in it, which opens nothing, clicks once; and a greyed-out
        /// control - on the menu or in the HUD - is silent under the pointer. Each step is pressed and
        /// heard inside one frame, so nothing the house does between frames is counted.
        /// </summary>
        [UnityTest]
        public IEnumerator Audio_EveryPressAnswersOnceAndAGreyedControlIsSilent()
        {
            HoldTheHouseForTheFixture();
            var audio = director.GetComponent<HouseAudio>();
            director.OpenMainMenu();
            yield return Frames(2);
            var asked = new System.Collections.Generic.List<HouseAudio.Cue>();
            void Note(HouseAudio.Cue cue) => asked.Add(cue);
            void Enter(GameObject control) => UnityEngine.EventSystems.ExecuteEvents.Execute(control,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            audio.CueAsked += Note;
            try
            {
                var start = CastButtons(MainMenu.NewSeasonCaption)[0];
                var settings = CastButtons(MainMenu.SettingsCaption)[0];
                settings.interactable = false;
                Enter(settings.gameObject);
                settings.interactable = true;
                Assert.That(asked, Is.Empty, "A greyed-out control on the menu is silent under the pointer.");
                Enter(start.gameObject);
                start.onClick.Invoke();
                Assert.That(asked, Is.EqualTo(new[] { HouseAudio.Cue.Hover, HouseAudio.Cue.Button }),
                    "The pointer over a menu control ticks, and a press that opens no panel clicks.");
                yield return Frames(2);
                Assert.That(CastScreen().IsShowing, Is.True, "The cast screen came up.");

                asked.Clear();
                var back = CastScreen().GetComponentsInChildren<UnityEngine.UI.Button>()
                    .Where(button => button.name == WayOut.BackObjectName && button.IsActive() && button.IsInteractable()).Last();
                back.onClick.Invoke();
                Assert.That(asked, Is.EqualTo(new[] { HouseAudio.Cue.Button }), "The cast screen's way back clicks too.");
                yield return Frames(2);
                Assert.That(Menu().IsShowing, Is.True, "and goes back to the menu.");

                asked.Clear();
                CastButtons(MainMenu.SettingsCaption)[0].onClick.Invoke();
                Assert.That(asked, Is.EqualTo(new[] { HouseAudio.Cue.PanelOpen }), "Settings opens a panel: its sound alone, no click on top.");
                yield return Frames(2);

                asked.Clear();
                ShellTabs().Single(tab => tab.name == SettingsWords.TabName(SettingsSection.Sound)).onClick.Invoke();
                Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Sound), "The tab shows its section,");
                Assert.That(asked, Is.EqualTo(new[] { HouseAudio.Cue.Button }), "and, opening nothing, clicks once.");
                yield return Frames(2);

                asked.Clear();
                var display = ShellTabs().Single(tab => tab.name == SettingsWords.TabName(SettingsSection.Display));
                Enter(display.gameObject);
                display.interactable = false;
                Enter(display.gameObject);
                display.interactable = true;
                Assert.That(asked, Is.EqualTo(new[] { HouseAudio.Cue.Hover }), "A HUD tab ticks under the pointer, and the same tab greyed out is silent.");
            }
            finally { audio.CueAsked -= Note; }
        }

        [UnityTest]
        public IEnumerator Audio_ReducedSoundStopsTheRoomToneAndSoftensTheCues()
        {
            var audio = director.GetComponent<HouseAudio>();
            Assert.That(audio, Is.Not.Null, "The director carries the house's audio.");
            director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Accessibility);
            yield return null;
            Assert.That(director.ReducedAudio, Is.False, "A fresh episode plays full sound.");
            float full = audio.CueVolume;
            Assume.That(full, Is.GreaterThan(0f));

            PressSettingRow(Gamesim.Presentation.SettingRowWords.ReducedSoundRow, "On");
            yield return null;
            Assert.That(director.ReducedAudio, Is.True);
            Assert.That(audio.ReducedAudio, Is.True, "The preference reaches the audio.");
            Assert.That(audio.CueVolume, Is.EqualTo(full * 0.5f).Within(0.001f), "The cues play at half.");
            Assert.That(audio.AmbiencePlaying, Is.False, "The room tone stops.");
            Assert.That(audio.RoomTonePlaying, Is.False, "and so does a room's own bed");
            Assert.That(SettingRowValue(Gamesim.Presentation.SettingRowWords.ReducedSoundRow), Is.EqualTo("On"), "The row says it is on, and a press turns it off.");

            PressSettingRow(Gamesim.Presentation.SettingRowWords.ReducedSoundRow, "Off");
            yield return null;
            Assert.That(director.ReducedAudio, Is.False);
            Assert.That(audio.CueVolume, Is.EqualTo(full).Within(0.001f));
            director.ClosePanels();
        }
    }
}
