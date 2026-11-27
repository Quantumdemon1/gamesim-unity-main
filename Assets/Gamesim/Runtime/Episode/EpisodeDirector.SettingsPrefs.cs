using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The Settings frame's Sound, Display, Accessibility and Controls sections (menu slice 4). Every
    /// setting is a name-and-value row from the row kit (EpisodeHud.SettingRows.cs): its name on the
    /// left, how it stands on the right, one control that left, right and Enter change (part b). Each
    /// row changes the same field, keeps the same preference key and value, and applies it the same
    /// way the control it replaced did; the director draws the panel again and the new row reads the
    /// new value.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string AccessibilityLine = "Everything said in the house and at the ceremonies is captioned. Every screen can be used with the keyboard or a controller.";

        /// <summary>The control that plays the first-run tour again, and the words its card points to.</summary>
        public const string ReplayTourCaption = "Replay the tour";

        /// <summary>Sound: all sound on or off, the master volume, the music on or off, and each channel's share.</summary>
        private void SoundSection()
        {
            hud.SettingSwitch(SettingRowWords.AllSoundRow, !muted, on => { muted = !on; ApplyPreferences(); Render(); });
            hud.SettingSlider(SettingRowWords.MasterVolumeRow, volumePercent, SetMasterVolumeLive, SetMasterVolumeFromSlider);
            hud.SettingSwitch(SettingRowWords.MusicRow, musicOn, on => { musicOn = on; ApplyPreferences(); Render(); });
            // The music, the effects and the interface, each a share of the master (A11).
            foreach (var channel in new[] { SoundChannel.Music, SoundChannel.Effects, SoundChannel.Interface })
            {
                var each = channel;
                hud.SettingSlider(SoundPictures.MeterCaption(each), ChannelVolumePercent(each),
                    percent => ApplyChannelVolumeLive(each, percent), percent => ApplyChannelVolume(each, percent));
            }
        }

        /// <summary>Display: the window, the frame rate, the HUD's size, the house view and the language (EpisodeDirector.Display.cs).</summary>
        private void DisplaySection() => DisplaySettings();

        /// <summary>Accessibility: motion, sound, the text's size, how long captions stay, whether conversation lines move on by themselves, and the ceremonies' pace.</summary>
        private void AccessibilitySection()
        {
            hud.Aside(AccessibilityLine);
            hud.SettingSwitch(SettingRowWords.ReducedMotionRow, reducedMotion, on => { reducedMotion = on; ApplyPreferences(); Render(); });
            hud.SettingSwitch(SettingRowWords.ReducedSoundRow, reducedAudio, on => { reducedAudio = on; ApplyPreferences(); Render(); });
            // The three sizes in one row (A8): the size in use is its value, and left and right step through them.
            hud.SettingChoice(SettingRowWords.TextSizeRow, SettingRowWords.TextSizeValues, SettingRowWords.IndexOf(textSize),
                index => SetTextSize(TextSizes.All[index]));
            // How long the ceremony's strip and a witnessed conversation's line stay up (A10).
            hud.SettingChoice(SettingRowWords.CaptionTimeRow, SettingRowWords.CaptionTimeValues, captionTime == CaptionTime.Longer ? 1 : 0,
                index => SetCaptionTime(SettingRowWords.CaptionTimeAt(index)));
            // Whether a conversation's spoken lines wait for a press or move on after their reading time (D11).
            hud.SettingChoice(SettingRowWords.ConversationLinesRow, SettingRowWords.ConversationLinesValues, autoContinueLines ? 1 : 0,
                index => SetAutoContinueLines(index == 1));
            hud.SectionHeading("CEREMONIES");
            hud.SettingChoice(SettingRowWords.CeremonyPaceRow, SettingRowWords.CeremonyPaceValues, ceremonyPace == CeremonyPace.Quick ? 1 : 0,
                index => { SetCeremonyPace(SettingRowWords.CeremonyPaceAt(index)); Render(); });
            // The key ceremony and the live eviction reveal one name at a time; suspenseful holds a
            // beat on each, quick keeps them brisk. Either can be sped up or skipped while it plays,
            // by the keys or the pad's buttons as the device last pressed names them (A4f).
            hud.Paragraph(InputGlossary.CeremonyPaceLine(ceremonyPace == CeremonyPace.Suspenseful, PressWasPad));
        }

        /// <summary>
        /// Controls: the first-run tour again, every control and changing it (A4, A5), then the
        /// camera's speed and tilt (A7) and the screen edges' pan.
        /// </summary>
        private void ControlsSection()
        {
            // The first-run tour again, from the house: the tour's own card says it is here. Where it
            // cannot play, the row stays and says why.
            if (tutorial != null)
                hud.SettingAction(ReplayTourCaption, ReplayTour,
                    CanReplayTour ? null : SettingRowWords.TourWhyNot(settingsFromFrontDoor, blockedRecovery, OpeningOwnsHouse));
            ControlsSettings();
            CameraSettings();
            EdgePanRows();
        }

        /// <summary>
        /// Draws the Settings again when a setting changed from outside its row - a key, the verifier's
        /// profile, another screen - while its section is on screen, so the row reads the value in use.
        /// </summary>
        private void RedrawSettingsRows()
        {
            if (hud != null && IsReady && AreSettingsSectionsOpen) Render();
        }
    }
}
