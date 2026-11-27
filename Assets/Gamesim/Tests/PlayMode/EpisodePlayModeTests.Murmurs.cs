using System.Collections;
using System.Collections.Generic;
using System.IO;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The houseguests' murmurs (AU-16), on the built screen: a conversation's spoken lines each
        /// murmur once, as they come up, in the speaker's own voice - the player's line in the player's
        /// pitch, the answer in the houseguest's - and at a syllable a word. A redraw of the line is
        /// silent; with the Voice volume at nothing a murmur is asked for and not heard; and playing
        /// them commits nothing and draws nothing from the season.
        /// </summary>
        [UnityTest]
        public IEnumerator Audio_AConversationsLinesMurmurOnceEachInTheirSpeakersVoices_OnTheVoiceVolume()
        {
            yield return InstallExchangeFlowHouse();
            var audio = director.GetComponent<HouseAudio>();
            var heard = new List<Murmur.Plan>();
            void Note(Murmur.Plan plan) => heard.Add(plan);
            audio.MurmurAsked += Note;
            try
            {
                Assert.That(director.ChannelVolumePercent(SoundChannel.Voice), Is.EqualTo(SoundPictures.DefaultVoiceVolume), "The voices start at 70%.");
                Assert.That(audio.ChannelVolume(SoundChannel.Voice), Is.EqualTo(.7f).Within(1e-5f));
                Assert.That(audio.Muted, Is.False, "The fixture's sound is on.");
                var before = director.Snapshot;
                // Counted before the press: Submit draws the player's line, and its murmur, in the same call.
                int started = audio.MurmursStarted;
                var result = director.Submit(ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk));
                Assert.That(result.accepted, Is.True, result.reason);
                var committed = director.Snapshot;
                byte[] savedBytes = File.ReadAllBytes(director.SavePath);
                yield return null;

                AssertExchangeFlowStep(before.playerId, "How's the day treating you?");
                Assert.That(heard, Has.Count.EqualTo(1), "The player's line murmurs as it comes up: " + string.Join(" | ", heard));
                var mine = heard[0];
                Assert.That(mine.SpeakerId, Is.EqualTo(before.playerId));
                Assert.That(mine.Witnessed, Is.False, "A conversation's own line, at the Voice volume's full place.");
                Assert.That(mine.Words, Is.EqualTo(5), "A syllable a word.");
                Assert.That(mine.PitchHz, Is.EqualTo(Murmur.PitchOf(before.playerId, before.Find(before.playerId).pronouns)).Within(1e-9),
                    "In the player's own voice.");
                Assert.That(audio.MurmursStarted - started, Is.EqualTo(1), "Heard on the Voice volume.");
                Assert.That(audio.MurmurVolume, Is.EqualTo(audio.CueVolume * .7f).Within(1e-5f), "At the master's level for cues, times the voices' 70%.");

                // The line drawn again (a setting changed under it) murmurs nothing more.
                director.SetAutoContinueLines(director.AutoContinueLines);
                yield return null;
                Assert.That(heard, Has.Count.EqualTo(1), "Once a line, not once a redraw.");

                // With the voices at nothing, the answer is asked for and not heard.
                director.SetChannelVolume(SoundChannel.Voice, 0);
                Assert.That(heard, Has.Count.EqualTo(1), "Changing the volume redraws the line, silently.");
                Assert.That(audio.MurmurVolume, Is.EqualTo(0f), "The murmur sounding follows the Voice volume at once.");
                ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
                yield return null;
                Assert.That(director.ConversationExchangeSpeakerId, Is.EqualTo(exchangeFlowNpcId), "The houseguest's answer is up.");
                Assert.That(heard, Has.Count.EqualTo(2), "The answer murmurs as it comes up.");
                var theirs = heard[1];
                Assert.That(theirs.SpeakerId, Is.EqualTo(exchangeFlowNpcId));
                Assert.That(theirs.PitchHz, Is.EqualTo(Murmur.PitchOf(exchangeFlowNpcId, committed.Find(exchangeFlowNpcId).pronouns)).Within(1e-9),
                    "In the houseguest's own voice.");
                string answer = ActiveRect(EpisodeHud.ConversationExchangeLineName).GetComponent<TMP_Text>().text;
                Assert.That(theirs.Words, Is.EqualTo(Murmur.WordCount(answer)), "As long as the answer on screen: " + answer);
                Assert.That(audio.MurmursStarted - started, Is.EqualTo(1), "Not heard with the Voice volume at nothing.");

                director.SetChannelVolume(SoundChannel.Voice, SoundPictures.DefaultVoiceVolume);
                ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
                yield return null;
                Assert.That(director.IsConversationExchangePlaying, Is.False, "The lines give the page back.");
                Assert.That(heard, Has.Count.EqualTo(2), "The page coming back murmurs nothing.");
                AssertExchangeFlowUnchanged(committed, savedBytes);
            }
            finally { audio.MurmurAsked -= Note; }
        }
    }
}
