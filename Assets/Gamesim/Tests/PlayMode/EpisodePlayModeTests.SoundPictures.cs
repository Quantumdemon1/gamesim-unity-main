using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Every sound has a picture (PLAN A, A11): a week of the season played with the sound muted, by
    /// the keyboard walk's own presses, and every cue that carries news (<see cref="SoundPictures"/>)
    /// comes with a change of the text on screen in the frame it sounds - so a player who cannot hear
    /// it, or who muted it, is told the same thing. And the three volumes beside the master each
    /// reach their own sounds and nothing else.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Runs at the head of every frame, before the game's own updates.</summary>
        [DefaultExecutionOrder(-30000)]
        private sealed class FrameHeadProbe : MonoBehaviour
        {
            public Action Head;
            private void Update() => Head?.Invoke();
        }

        /// <summary>Runs at the foot of every frame, after the game's own late updates.</summary>
        [DefaultExecutionOrder(30000)]
        private sealed class FrameFootProbe : MonoBehaviour
        {
            public Action Foot;
            private void LateUpdate() => Foot?.Invoke();
        }

        /// <summary>
        /// Every label up on a screen canvas with words in it, as one string: what the screen says this
        /// moment, a card fading in included (its words are set the frame it is played).
        /// </summary>
        private static string TextOnScreen()
        {
            var words = new List<string>();
            foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text)) continue;
                var canvas = label.canvas;
                if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) continue;
                words.Add(label.text);
            }
            words.Sort(StringComparer.Ordinal);
            return string.Join("\u0001", words);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Audio_EveryInformationCueComesWithTextOnScreenInTheSameFrame()
        {
            var audio = director.GetComponent<HouseAudio>();
            // Muted, as a player who cannot hear it plays: every cue is still asked for, and none sounds.
            director.OpenSettings();
            yield return null; yield return null;
            ButtonWithCaption("Mute sound").onClick.Invoke();
            yield return null;
            director.ClosePanels();
            yield return null;
            Assert.That(audio.Muted, Is.True, "The walk is played with the sound off.");

            player.Agent.speed = 25; player.Agent.acceleration = 100;
            director.SetCeremonyPace(CeremonyPace.Quick);

            var probes = new GameObject("Sound picture probes");
            var head = probes.AddComponent<FrameHeadProbe>();
            var foot = probes.AddComponent<FrameFootProbe>();
            string atHead = null;
            var cues = new List<HouseAudio.Cue>();
            var heard = new Dictionary<HouseAudio.Cue, int>();
            var silentPictures = new List<string>();
            head.Head = () => { atHead = TextOnScreen(); cues.Clear(); };
            Action<HouseAudio.Cue> listen = cue =>
            {
                if (!SoundPictures.IsInformation(cue.ToString())) return;
                cues.Add(cue);
                heard[cue] = heard.TryGetValue(cue, out int count) ? count + 1 : 1;
            };
            foot.Foot = () =>
            {
                if (cues.Count == 0 || atHead == null) return;
                if (TextOnScreen() == atHead)
                    silentPictures.Add(string.Join("+", cues) + " in " + director.Snapshot.phase + " week " + director.Snapshot.week + " (frame " + Time.frameCount + ")");
                cues.Clear();
            };
            audio.CueAsked += listen;
            try
            {
                var previous = director.Snapshot;
                bool recapped = false;
                for (int guard = 0; guard < 60 && !recapped; guard++)
                {
                    if (SceneComponents<CompetitionResult>().Any(result => result.IsPlaying))
                        yield return ContinueCompetitionResults(byKeyboard: true);
                    yield return AwaitRecap(previous, director.Snapshot);
                    if (director.IsWeeklyRecapOpen)
                    {
                        yield return KeyboardSubmit(WeeklyRecapScreen.ContinueCaption);
                        recapped = true;
                        break;
                    }
                    if (!director.IsPanelOpen)
                    {
                        WarpPlayer(director.StationPosition);
                        Assert.That(director.TryOpenPhasePanel(), Is.True, "The station must open in " + director.Snapshot.phase);
                        yield return null; yield return null;
                    }
                    var state = director.Snapshot;
                    foreach (var caption in KeyboardCaptions(state))
                        yield return KeyboardSubmit(caption);
                    yield return SettleReveal();
                    previous = state;
                }
                Assert.That(recapped, Is.True, "The walk reached the first week's recap.");
            }
            finally
            {
                audio.CueAsked -= listen;
                Object.Destroy(probes);
            }

            string said = string.Join(", ", heard.Select(pair => pair.Key + " x" + pair.Value));
            TestContext.WriteLine("Information cues in the first week, muted: " + said);
            Assert.That(heard.ContainsKey(HouseAudio.Cue.Nomination) && heard.ContainsKey(HouseAudio.Cue.Eviction), Is.True,
                "A week's nominations and its eviction both sound: " + said);
            Assert.That(silentPictures, Is.Empty,
                "These cues sounded in a frame whose text did not change - news only a hearing player was given: " + string.Join(" | ", silentPictures));
        }

        /// <summary>
        /// The music's, the effects' and the interface's volumes (A11), each a share of the master
        /// beside it: each control moves its own share and only its own, the cues and the beds follow
        /// their channel, and the master stays where it was.
        /// </summary>
        [UnityTest]
        public IEnumerator Audio_TheThreeVolumesStandBesideTheMaster()
        {
            var audio = director.GetComponent<HouseAudio>();
            director.OpenSettings();
            yield return null; yield return null;
            float master = audio.Volume;
            float nomination = audio.VolumeOf(HouseAudio.Cue.Nomination), button = audio.VolumeOf(HouseAudio.Cue.Button);
            Assert.That(nomination, Is.EqualTo(audio.CueVolume).Within(1e-5f), "At the whole of the master, a cue plays at the master, as it always did.");

            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Effects)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Effects), Is.EqualTo(90));
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Nomination), Is.EqualTo(nomination * .9f).Within(1e-5f), "The effects' control lowers the game's news,");
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Button), Is.EqualTo(button).Within(1e-5f), "and not the interface's answers.");

            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Interface)).onClick.Invoke();
            yield return null;
            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Interface)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Interface), Is.EqualTo(80));
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Button), Is.EqualTo(button * .8f).Within(1e-5f), "The interface's control lowers the interface,");
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Nomination), Is.EqualTo(nomination * .9f).Within(1e-5f), "and nothing else.");

            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Music)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Music), Is.EqualTo(90));
            Assert.That(audio.ChannelVolume(SoundChannel.Music), Is.EqualTo(.9f).Within(1e-5f), "The music's control lowers the music's share.");
            Assert.That(audio.Volume, Is.EqualTo(master).Within(1e-5f), "The master stays where it was through all three.");

            ButtonWithCaption(SoundPictures.UpCaption(SoundChannel.Effects)).onClick.Invoke();
            yield return null;
            ButtonWithCaption(SoundPictures.UpCaption(SoundChannel.Effects)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Effects), Is.EqualTo(100), "A share never passes the whole of the master.");
            director.SetChannelVolume(SoundChannel.Music, 100);
            director.SetChannelVolume(SoundChannel.Interface, 100);
            director.ClosePanels();
            yield return null;
        }
    }
}
