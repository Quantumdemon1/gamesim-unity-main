using System;
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
            // The house's own sounds - the news and a motion the player sees - play as effects.
            foreach (var row in SoundPictures.Rows)
                Assert.That(row.Channel, Is.EqualTo(row.Kind == SoundKind.Interface ? SoundChannel.Interface : SoundChannel.Effects), row.ToString());
            // The news the plan names, each information.
            foreach (var cue in new[] { "SocialUp", "SocialDown", "Nomination", "Veto", "Vote", "Eviction", "CompetitionWin", "Finale", "Save", "Summons" })
                Assert.That(SoundPictures.IsInformation(cue), Is.True, cue + " carries news.");
            foreach (var cue in new[] { "Button", "PanelOpen", "PanelClose", "Hover", "TutorialStep" })
                Assert.That(SoundPictures.IsInformation(cue), Is.False, cue + " answers a hand.");
            Assert.That(SoundPictures.IsInformation("SomeCueNotYetInTheTable"), Is.True, "A cue with no row is treated as news until it has one.");
            Assert.That(SoundPictures.ChannelOf("SomeCueNotYetInTheTable"), Is.EqualTo(SoundChannel.Effects));
        }

        [Test]
        public void TheNewsPlaysAsRecorded_AndEveryInterfaceAnswerUnderIt()
        {
            foreach (var row in SoundPictures.Rows)
            {
                if (row.Kind == SoundKind.Information)
                {
                    Assert.That(row.GainDb, Is.EqualTo(0f), row.Cue + " is news and stays where it was.");
                    Assert.That(SoundPictures.GainOf(row.Cue), Is.EqualTo(1f), row.Cue);
                }
                else
                {
                    Assert.That(row.GainDb, Is.LessThan(0f), row.Cue + " answers a hand, or is a motion on screen, and sits under the news.");
                    Assert.That(SoundPictures.GainOf(row.Cue), Is.EqualTo((float)Math.Pow(10.0, row.GainDb / 20.0)).Within(1e-6f), row.Cue);
                }
            }
            Assert.That(SoundPictures.Find("Hover").GainDb, Is.LessThan(SoundPictures.Find("Button").GainDb), "The hover's tick sits under the press's click.");
            Assert.That(SoundPictures.GainOf("SomeCueNotYetInTheTable"), Is.EqualTo(1f), "A cue with no row plays as recorded.");
        }

        /// <summary>
        /// The front door (AU-06): its clunk and its thud are the sound of a motion the player sees -
        /// played under the effects' volume, as the house's air is, never counted as news (so no
        /// picture of text is owed for them), and under the news in the mix.
        /// </summary>
        [Test]
        public void TheFrontDoorIsAMotionOnScreen_PlayedAsEffects_UnderTheNews()
        {
            foreach (var cue in new[] { "DoorOpen", "DoorShut" })
            {
                var row = SoundPictures.Find(cue);
                Assert.That(row, Is.Not.Null, cue + " has its row.");
                Assert.That(row.Kind, Is.EqualTo(SoundKind.World), cue + " is a motion on screen.");
                Assert.That(SoundPictures.ChannelOf(cue), Is.EqualTo(SoundChannel.Effects), cue + " follows the Effects volume.");
                Assert.That(SoundPictures.IsInformation(cue), Is.False, cue + " carries no news of its own.");
                Assert.That(row.GainDb, Is.EqualTo(SoundPictures.DoorGainDb), cue);
            }
            Assert.That(SoundPictures.DoorGainDb, Is.InRange(-8f, -2f), "Under the news, and over the interface's quietest answers.");
        }

        /// <summary>
        /// A competition's count - the 3-2-1 before GO and its last five seconds - and an early press
        /// on the reaction board (AU-08) are the game's news, played as effects: they borrowed the
        /// pointer's tick, whose row is the interface's, so turning the interface down silenced a
        /// competition's count. Every beat of a competition the screen sounds now plays as effects.
        /// </summary>
        [Test]
        public void ACompetitionsCountAndAnEarlyPressAreNews_SoEveryBeatOfACompetitionPlaysAsEffects()
        {
            foreach (var cue in new[] { "CountTick", "FalseStart" })
            {
                var row = SoundPictures.Find(cue);
                Assert.That(row, Is.Not.Null, cue + " has its row.");
                Assert.That(row.Kind, Is.EqualTo(SoundKind.Information), cue + " carries news, so its picture is owed.");
                Assert.That(row.Channel, Is.EqualTo(SoundChannel.Effects), cue + " follows the Effects volume.");
                Assert.That(row.GainDb, Is.EqualTo(0f), cue + " is news and plays as recorded.");
            }
            foreach (var cue in new[] { "CompetitionStart", "CountTick", "FalseStart", "SocialUp", "SocialDown", "CompetitionWin" })
                Assert.That(SoundPictures.Find(cue).Channel, Is.Not.EqualTo(SoundChannel.Interface), cue + " is still heard with the interface at nothing.");
        }

        [Test]
        public void ASweepDownAListTicksOnceIn60Milliseconds_AndEveryOtherCueEveryTime()
        {
            Assert.That(SoundPictures.SpacingOf("Hover"), Is.EqualTo(0.06f));
            foreach (var row in SoundPictures.Rows.Where(row => row.Cue != "Hover"))
                Assert.That(SoundPictures.SpacingOf(row.Cue), Is.EqualTo(0f), row.Cue + " sounds every time it is asked.");
            Assert.That(SoundPictures.SpacingOf("SomeCueNotYetInTheTable"), Is.EqualTo(0f));

            // Twelve rows entered one a frame at 60 frames a second: ticks at the first, the fifth and the ninth.
            float spacing = SoundPictures.SpacingOf("Hover"), last = float.NaN;
            var heard = new System.Collections.Generic.List<int>();
            for (int frame = 0; frame < 12; frame++)
            {
                float now = 10f + frame / 60f;
                if (!SoundPictures.Sounds(now, last, spacing)) continue;
                heard.Add(frame);
                last = now;
            }
            Assert.That(heard, Is.EqualTo(new[] { 0, 4, 8 }), "Three ticks for twelve rows, not twelve.");
            Assert.That(SoundPictures.Sounds(10.2f, 10.1f, spacing), Is.True, "A hover a tenth of a second after the last is heard.");
            Assert.That(SoundPictures.Sounds(10.13f, 10.1f, spacing), Is.False, "One inside the spacing is not.");
            Assert.That(SoundPictures.Sounds(2f, 10.1f, spacing), Is.True, "A clock that went back (a new session) is heard.");
            Assert.That(SoundPictures.Sounds(10.1f, 10.1f, 0f), Is.True, "A cue with no spacing is heard every time.");
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
        public void TheVolumeSliders_AreNamedForTheirChannel_BesideTheSoundRows()
        {
            var names = new[] { SoundChannel.Music, SoundChannel.Effects, SoundChannel.Interface }.Select(SoundPictures.MeterCaption).ToList();
            Assert.That(names, Is.EqualTo(new[] { "Music volume", "Effects volume", "Interface volume" }));
            // Menu slice 4: each a slider row of its own, its value its percentage; no step buttons beside it,
            // and no name shared with the master's slider or the sound switches.
            foreach (var other in new[] { SettingRowWords.MasterVolumeRow, SettingRowWords.AllSoundRow, SettingRowWords.MusicRow, SettingRowWords.ReducedSoundRow })
                Assert.That(names, Has.No.Member(other));
            Assert.That(new[] { SoundPictures.MusicVolumeKey, SoundPictures.EffectsVolumeKey, SoundPictures.InterfaceVolumeKey },
                Is.EqualTo(new[] { "Gamesim.MusicVolume", "Gamesim.EffectsVolume", "Gamesim.InterfaceVolume" }));
        }
    }
}
