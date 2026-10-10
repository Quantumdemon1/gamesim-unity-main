using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Every sound has a picture (PLAN A, A11): the table pairing each of the house's cues with the
    /// visible change that says the same thing, and the three volumes beside the master. Unity-free;
    /// SoundPicturesCueTests holds the table to the cues the house actually has, and the
    /// episode's PlayMode suite holds each information cue to a change of the text on screen.
    /// </summary>
    public sealed class SoundPicturesTests
    {
        [Test]
        public void EveryRow_NamesOneCue_AndSaysWhatTheScreenShows()
        {
            var cues = SoundPictures.Rows.Select(row => row.Cue).ToList();
            Assert.That(cues, Is.Unique, "One row a cue.");
            foreach (var row in SoundPictures.Rows)
            {
                Assert.That(row.Picture, Is.Not.Null.And.Not.Empty, row.Cue + " says what the screen shows when it sounds.");
                Assert.That(SoundPictures.Find(row.Cue), Is.SameAs(row));
            }
        }

        [Test]
        public void TheGamesNewsPlaysAsEffects_AndTheInterfacesAnswersAsTheInterface()
        {
            foreach (var row in SoundPictures.Rows)
                Assert.That(row.Channel, Is.EqualTo(row.Kind == SoundKind.Information ? SoundChannel.Effects : SoundChannel.Interface), row.ToString());
            // The news the plan names, each information.
            foreach (var cue in new[] { "SocialUp", "SocialDown", "Nomination", "Veto", "Vote", "Eviction", "CompetitionWin", "Finale", "Save" })
                Assert.That(SoundPictures.IsInformation(cue), Is.True, cue + " carries news.");
            foreach (var cue in new[] { "Button", "PanelOpen", "PanelClose", "Hover", "TutorialStep" })
                Assert.That(SoundPictures.IsInformation(cue), Is.False, cue + " answers a hand.");
            Assert.That(SoundPictures.IsInformation("SomeCueNotYetInTheTable"), Is.True, "A cue with no row is treated as news until it has one.");
            Assert.That(SoundPictures.ChannelOf("SomeCueNotYetInTheTable"), Is.EqualTo(SoundChannel.Effects));
        }

        [Test]
        public void TheVolumes_StepByTenInsideTheWhole_AndStartAtTheWhole()
        {
            Assert.That(SoundPictures.DefaultVolume, Is.EqualTo(100), "A player who never touches them hears what they always heard.");
            Assert.That(SoundPictures.Stepped(100, true), Is.EqualTo(100));
            Assert.That(SoundPictures.Stepped(100, false), Is.EqualTo(90));
            Assert.That(SoundPictures.Stepped(5, false), Is.EqualTo(0));
            Assert.That(SoundPictures.Stepped(0, true), Is.EqualTo(10));
            Assert.That(SoundPictures.Clamp(140), Is.EqualTo(100));
            Assert.That(SoundPictures.Clamp(-3), Is.EqualTo(0));
        }

        [Test]
        public void TheVolumeControls_HaveCaptionsOfTheirOwn()
        {
            var captions = new[] { SoundChannel.Music, SoundChannel.Effects, SoundChannel.Interface }
                .SelectMany(channel => new[] { SoundPictures.DownCaption(channel), SoundPictures.UpCaption(channel) }).ToList();
            Assert.That(captions, Is.EqualTo(new[] { "Music volume down", "Music volume up", "Effects volume down", "Effects volume up", "Interface volume down", "Interface volume up" }));
            // Unique beside the master's own and every sound control already in the settings.
            foreach (var existing in new[] { "Volume down", "Volume up", "Mute sound", "Turn sound on", "Turn music off", "Turn music on", "Reduce sound", "Full sound" })
                Assert.That(captions, Has.No.Member(existing));
            Assert.That(SoundPictures.MeterCaption(SoundChannel.Music), Is.EqualTo("Music volume"));
            Assert.That(new[] { SoundPictures.MusicVolumeKey, SoundPictures.EffectsVolumeKey, SoundPictures.InterfaceVolumeKey },
                Is.EqualTo(new[] { "Gamesim.MusicVolume", "Gamesim.EffectsVolume", "Gamesim.InterfaceVolume" }));
        }
    }
}
