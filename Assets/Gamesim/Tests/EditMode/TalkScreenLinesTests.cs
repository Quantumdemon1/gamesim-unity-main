using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free "Just talk" screen's words (free talk slice 1), pure, so the Unity-free subset runs them: nothing is shown and
    /// the command is refused, the season's stream untouched, at talk rules 0; a reply's button is its spoken words with the
    /// tokens filled, one per tone; the command a reply sends is the engine's own text form on the state it was drawn from;
    /// the line of what changed puts a learned fact first; and the notebook's two blocks are grouped, dated and left out when
    /// empty. The director (EpisodeDirector.JustTalk.cs) draws exactly these lines.
    /// </summary>
    public sealed class TalkScreenLinesTests
    {
        private static EpisodeState Season()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3101u);
            if (!EpisodeEngine.StoryOn(s)) EpisodeEngine.EnableStory(s);
            return s;
        }

        private static string Housemate(EpisodeState s) => s.contestants.First(c => !c.isPlayer).id;

        [Test]
        public void AtTalkRulesZeroNothingIsShownAndTheCommandIsRefusedWithTheStreamUntouched()
        {
            var s = Season();
            s.story.talkRules = 0;
            string npc = Housemate(s);
            Assert.That(TalkScreenLines.Shown(s, npc), Is.False, "No tile and no row at talk rules 0.");
            Assert.That(TalkScreenLines.NotebookBlocks(s, npc), Is.Empty, "No notebook block at talk rules 0.");
            uint random = s.randomState;
            int revision = s.revision;
            var result = new EpisodeEngine(s).Apply(TalkScreenLines.Command(s, npc, "steady.l1.bunk", TalkRules.Warm, "talk-screen-0"));
            Assert.That(result.accepted, Is.False, "A free talk is refused at talk rules 0.");
            Assert.That(result.reason, Is.EqualTo(FreeTalk.OffReason));
            Assert.That(s.randomState, Is.EqualTo(random), "The season's stream is untouched.");
            Assert.That(s.revision, Is.EqualTo(revision));
        }

        [Test]
        public void AReplySendsTheEngineTextFormOnTheStateItWasDrawnFrom()
        {
            var s = Season();
            string npc = Housemate(s);
            var command = TalkScreenLines.Command(s, npc, "spark.l1.fridge", TalkRules.Tease, "talk-screen-1");
            Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.JustTalk));
            Assert.That(command.actorId, Is.EqualTo(s.playerId));
            Assert.That(command.targetId, Is.EqualTo(npc));
            Assert.That(command.expectedRevision, Is.EqualTo(s.revision));
            Assert.That(command.expectedPhase, Is.EqualTo(s.phase));
            Assert.That(command.id, Is.EqualTo("talk-screen-1"));
            Assert.That(FreeTalk.TryParse(command.text, out string talkId, out string tone), Is.True, command.text);
            Assert.That(talkId, Is.EqualTo("spark.l1.fridge"));
            Assert.That(tone, Is.EqualTo(TalkRules.Tease));
        }

        [Test]
        public void EachReplyIsItsSpokenWordsWithTheTokensFilledOnePerToneAndNoCaptionTwice()
        {
            var s = Season();
            string npc = Housemate(s);
            string name = CharacterNames.FirstName(s.Find(npc).name);
            var talk = new TalkRow
            {
                Id = "screen.test", Pool = TalkPools.Steady, Kind = TalkKind.Rhythm,
                Open = "(stirring a pot) \"Hungry, {player}?\"",
                Replies = new[]
                {
                    new TalkReply { Tone = TalkRules.Warm, Line = "(take a spoon) \"Always, {name}.\"" },
                    new TalkReply { Tone = TalkRules.Straight, Line = "\"Not really.\"" },
                    new TalkReply { Tone = TalkRules.Tease, Line = "\"Not really.\"" },
                },
            };
            var replies = TalkScreenLines.Replies(s, npc, talk);
            Assert.That(replies.Select(r => r.Words).ToArray(), Is.EqualTo(new[] { "Always, " + name + ".", "Not really." }));
            Assert.That(replies.Select(r => r.Tone).ToArray(), Is.EqualTo(new[] { TalkRules.Warm, TalkRules.Straight }));
            Assert.That(replies[0].ToneCaption, Is.EqualTo(TalkTone.Caption(TalkRules.Warm)));
            Assert.That(TalkScreenLines.Opener(s, npc, talk), Is.EqualTo("(stirring a pot) \"Hungry, " + CharacterNames.FirstName(s.Find(s.playerId).name) + "?\""));
        }

        [Test]
        public void WhatChangedPutsALearnedFactFirstThenTheStandingNoteElseNothingNew()
        {
            var learned = new TalkResult { Fact = "They grew up by the sea.", StandingLine = "\"I'm fine.\"", Outcome = FreeTalk.Landed };
            Assert.That(TalkScreenLines.ChangeLine(learned), Is.EqualTo(TalkScreenLines.LearnedPrefix + "They grew up by the sea."));
            Assert.That(TalkScreenLines.Aftermath(learned).ToArray(),
                Is.EqualTo(new[] { "\"I'm fine.\"", FreeTalk.Landed, TalkScreenLines.LearnedPrefix + "They grew up by the sea." }));
            var standing = new TalkResult { StandingLine = "\"I'm fine.\"", Outcome = FreeTalk.NothingMuchMoved };
            Assert.That(TalkScreenLines.ChangeLine(standing), Is.EqualTo(TalkScreenLines.StandingNote));
            var nothing = new TalkResult { Outcome = FreeTalk.WrongNote };
            Assert.That(TalkScreenLines.ChangeLine(nothing), Is.EqualTo(TalkScreenLines.NothingNew));
            Assert.That(TalkScreenLines.Aftermath(nothing).ToArray(), Is.EqualTo(new[] { FreeTalk.WrongNote, TalkScreenLines.NothingNew }));
        }

        [Test]
        public void TheTileSaysItIsFreeWhenOnOfferAndTheEnginesReasonWhenNot()
        {
            var open = new TalkOffer { Refusal = TalkRefusal.None, Talk = new TalkRow { Id = "screen.tile" } };
            Assert.That(TalkScreenLines.TileLine(open, "Sam"), Does.Contain("Sam").And.Contain("No action spent"));
            var had = new TalkOffer { Refusal = TalkRefusal.TalkedThisWindow, Reason = FreeTalk.TalkedReason };
            Assert.That(TalkScreenLines.TileLine(had, "Sam"), Is.EqualTo(FreeTalk.TalkedReason));
            Assert.That(TalkScreenLines.TileLine(new TalkOffer { Refusal = TalkRefusal.NothingToSay, Reason = "" }, "Sam"), Is.EqualTo(FreeTalk.NothingReason));
        }

        [Test]
        public void TheNotebookGroupsFactsByLayerDatesTheStandingAndLeavesEmptyBlocksOut()
        {
            Assert.That(TalkScreenLines.NotebookBlocks(new List<TalkNotes.Layer>(), null), Is.Empty, "Nothing known: no block.");
            var layers = new List<TalkNotes.Layer>
            {
                new TalkNotes.Layer { Number = 2, Heading = TalkNotes.Background, Facts = new List<string> { "B1" } },
                new TalkNotes.Layer { Number = 3, Heading = TalkNotes.Drives, Facts = new List<string>() },
                new TalkNotes.Layer { Number = 1, Heading = TalkNotes.WhoTheyAre, Facts = new List<string> { "A1", "A2" } },
            };
            var blocks = TalkScreenLines.NotebookBlocks(layers, new TalkNotes.Standing { Week = 2, Line = "\"Close to Sam.\"", Current = false });
            Assert.That(blocks.Select(b => b.Heading).ToArray(), Is.EqualTo(new[] { TalkNotes.WhatYouKnowHeading, TalkNotes.FromThemHeading }));
            Assert.That(blocks[0].Lines.ToArray(), Is.EqualTo(new[] { TalkNotes.WhoTheyAre, "· A1", "· A2", TalkNotes.Background, "· B1" }));
            Assert.That(blocks[1].Lines.ToArray(), Is.EqualTo(new[] { "Week 2: \"Close to Sam.\"", TalkScreenLines.EarlierWeekNote }));
            var current = TalkScreenLines.NotebookBlocks(null, new TalkNotes.Standing { Week = 3, Line = "\"Fine.\"", Current = true });
            Assert.That(current.Count, Is.EqualTo(1));
            Assert.That(current[0].Lines.ToArray(), Is.EqualTo(new[] { "Week 3: \"Fine.\"" }));
        }
    }
}
