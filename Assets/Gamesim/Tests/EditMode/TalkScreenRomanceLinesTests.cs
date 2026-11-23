using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free "Just talk" screen's words at talk rules 3 (free talk slice 3), pure, so the Unity-free subset runs them: the
    /// romance choices' captions and answers, the Flirt reply's place, the command's witness, the scene's lines, the third
    /// beat's romance lines, the "Between you" block's romance line and note, and "Took the no well" on the player's own card.
    /// At talk rules 2 not one of these is read or drawn: the page, the panel and the command are exactly slice 2's.
    /// The director (EpisodeDirector.JustTalk.cs) draws exactly these lines.
    /// </summary>
    public sealed class TalkScreenRomanceLinesTests
    {
        private static TalkRow Row(TalkKind kind, params TalkReply[] replies) => new TalkRow
        {
            Id = "spark.test.romance", Pool = TalkPools.Spark, Kind = kind,
            Open = "(by the pool) \"So. Are we doing this?\"",
            Replies = replies,
        };

        private static TalkReply Tone(string tone) =>
            new TalkReply { Tone = tone, Line = "\"Line " + tone + ".\"", Lands = "\"Good.\"", Even = "\"Okay.\"", Grates = "\"Hm.\"" };

        [Test]
        public void AtTalkRulesTwoNothingOfTheRomanceIsReadOrDrawn()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string npc = FreeTalkFixture.Npcs(s).First().id;
            var contact = FreeTalkFixture.ContactOf(s, npc);
            contact.rapport = 6;
            contact.talks = 3;
            contact.romance = TalkRules.Couple;
            contact.talkPending = "spark.test.romance";
            contact.talkFlags.Add(TalkRules.TookTheNoWell);
            uint random = s.randomState;

            Assert.That(TalkScreenLines.RomanceShown(s), Is.False);
            Assert.That(TalkScreenLines.PendingScene(s, npc), Is.Null, "No scene is resumed below talk rules 3.");
            var declaration = Row(TalkKind.Declaration, new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes.\"", Lands = "\"Good.\"" });
            Assert.That(TalkScreenLines.OfferChoices(s, npc, declaration), Is.Empty, "No choice buttons below talk rules 3.");
            var ordinary = Row(TalkKind.Layer, Tone(TalkRules.Warm), Tone(TalkRules.Straight), Tone(TalkRules.Tease));
            Assert.That(TalkScreenLines.FlirtShown(s, npc, ordinary, TalkScreenLines.Replies(s, npc, ordinary)), Is.False, "No Flirt below talk rules 3.");
            Assert.That(TalkScreenLines.AnswerText(s, "t1", TalkRules.Warm, "someone"), Is.EqualTo(FreeTalk.CommandText("t1", TalkRules.Warm)),
                "The command is slice 1's two-part text; the witness is never sent.");
            var command = TalkScreenLines.AnswerCommand(s, npc, "t1", TalkRules.Warm, "someone", "c1");
            var old = TalkScreenLines.Command(s, npc, "t1", TalkRules.Warm, "c1");
            Assert.That(new[] { command.text, command.targetId, command.actorId, command.id }, Is.EqualTo(new[] { old.text, old.targetId, old.actorId, old.id }));
            Assert.That(command.kind, Is.EqualTo(old.kind));
            Assert.That(command.expectedRevision, Is.EqualTo(old.expectedRevision));

            string bond = TalkScreenLines.BondLine(TalkNotes.BondWith(s, npc));
            var blocks = TalkScreenLines.BondBlocks(s, npc);
            Assert.That(blocks.SelectMany(b => b.Lines), Is.EqualTo(bond == null ? new List<string>() : new List<string> { bond }),
                "'Between you' holds only slice 2's line.");
            Assert.That(TalkScreenLines.YourReadBlocks(s).SelectMany(b => b.Lines), Has.None.EqualTo(TalkRomanceNotes.TookTheNoWell),
                "No romance note on the player's card below talk rules 3.");
            Assert.That(s.randomState, Is.EqualTo(random), "Reading the screen draws nothing.");
        }

        [Test]
        public void TheChoiceButtonsCarryTheEnginesWordsAndAnswersInItsOrder()
        {
            foreach (TalkChoice choice in System.Enum.GetValues(typeof(TalkChoice)))
            {
                if (choice == TalkChoice.None) { Assert.That(TalkScreenLines.ChoiceCaption(choice), Is.Empty); continue; }
                Assert.That(TalkScreenLines.ChoiceCaption(choice), Is.EqualTo(TalkChoices.Caption(choice)), choice.ToString());
            }
            Assert.That(new[] { TalkScreenLines.MakeItOfficialCaption, TalkScreenLines.AcceptCaption, TalkScreenLines.NotYetCaption,
                    TalkScreenLines.NotLikeThatCaption, TalkScreenLines.JustFriendsCaption, TalkScreenLines.EndItKindlyCaption,
                    TalkScreenLines.LaughItOffCaption, TalkScreenLines.PushAgainCaption, TalkScreenLines.ApologizeCaption },
                Is.EqualTo(new[] { "Make it official", "Accept", "Not yet", "Not like that", "Just friends", "End it kindly",
                    "Laugh it off", "Push again", "Apologize" }), "The captions are contracts, in American spelling.");

            var buttons = TalkScreenLines.ChoiceButtons(new[]
                { TalkChoice.Accept, TalkChoice.None, TalkChoice.NotYet, TalkChoice.Accept, TalkChoice.JustFriends });
            Assert.That(buttons.Select(b => b.Caption), Is.EqualTo(new[] { "Accept", "Not yet", "Just friends" }),
                "One button per choice, in the engine's order; none for None, none twice.");
            Assert.That(buttons.Select(b => b.Answer), Is.EqualTo(new[] { "choice:accept", "choice:not-yet", "choice:just-friends" }));
            Assert.That(TalkScreenLines.ChoiceButtons(null), Is.Empty);
        }

        [Test]
        public void AtTalkRulesThreeTheCommandCarriesTheWitnessAndTheFlirtKeepsToOrdinaryTalks()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Romance);
            string npc = FreeTalkFixture.Npcs(s).First().id;
            uint random = s.randomState;
            Assert.That(TalkScreenLines.RomanceShown(s), Is.True);
            Assert.That(TalkScreenLines.AnswerText(s, "t1", TalkRules.Flirt, "w1"), Is.EqualTo("t1|flirt|w1"));
            Assert.That(TalkScreenLines.AnswerText(s, "t1", "choice:accept", null), Is.EqualTo("t1|choice:accept"));
            var command = TalkScreenLines.AnswerCommand(s, npc, "t1", TalkRules.Flirt, "w1", "c1");
            Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.JustTalk));
            Assert.That(command.targetId, Is.EqualTo(npc));
            Assert.That(command.text, Is.EqualTo("t1|flirt|w1"));

            // Neither of these asks the engine: a romance row carries its own replies, and a talk with a flirt reply needs no second.
            var declaration = Row(TalkKind.Declaration, new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes.\"", Lands = "\"Good.\"" });
            Assert.That(TalkScreenLines.FlirtShown(s, npc, declaration, TalkScreenLines.Replies(s, npc, declaration)), Is.False);
            var withFlirt = Row(TalkKind.Layer, Tone(TalkRules.Warm), Tone(TalkRules.Flirt), Tone(TalkRules.Tease));
            Assert.That(TalkScreenLines.FlirtShown(s, npc, withFlirt, TalkScreenLines.Replies(s, npc, withFlirt)), Is.False);
            Assert.That(TalkScreenLines.OfferChoices(s, npc, withFlirt), Is.Empty, "An ordinary talk offers no romance choices.");
            Assert.That(TalkScreenLines.PendingScene(s, npc), Is.Null, "No scene is left open with them.");
            Assert.That(s.randomState, Is.EqualTo(random));
        }

        [Test]
        public void TheSceneSaysTheirLinesInOrderAndTheThirdBeatPutsTheRomanceBeforeTheChange()
        {
            var scene = new TalkResult
            {
                NpcId = "x", Scene = RomanceScene.TurnAside, Attempt = 1, SceneTalkId = "pat.turn.1",
                SceneLine = "(laughing) \"Was that a line?\"", SceneSecond = "\"I'm here to play.\"",
                Choices = new List<TalkChoice> { TalkChoice.LaughItOff, TalkChoice.PushAgain, TalkChoice.Apologize },
            };
            Assert.That(TalkScreenLines.SceneLines(scene, "Pat"),
                Is.EqualTo(new[] { "Pat: (laughing) \"Was that a line?\"", "Pat: \"I'm here to play.\"" }));
            Assert.That(TalkScreenLines.SceneLines(new TalkResult(), "Pat"), Is.Empty);
            Assert.That(TalkScreenLines.HasRomance(scene), Is.True);

            // A slice 2 result: no romance, so exactly slice 2's lines whatever list is passed.
            var plain = new TalkResult { NpcId = "x", Outcome = FreeTalk.Landed, Fact = "They grew up by the sea." };
            Assert.That(TalkScreenLines.HasRomance(plain), Is.False);
            Assert.That(TalkScreenLines.RomanceLines(plain, "Dana"), Is.Empty, "The engine is not asked about a result without romance.");
            Assert.That(TalkScreenLines.Aftermath(plain, "Dana", null), Is.EqualTo(TalkScreenLines.Aftermath(plain, "Dana")));

            const string spark = "There's a spark between you and Dana.";
            var flirt = new TalkResult { NpcId = "x", Tone = TalkRules.Flirt, Outcome = FreeTalk.Landed, Scene = RomanceScene.InKind, Change = RomanceChange.Spark };
            Assert.That(TalkScreenLines.Aftermath(flirt, "Dana", new[] { spark }), Is.EqualTo(new[] { FreeTalk.Landed, spark }),
                "The romance says what changed, so 'nothing new' is left out.");
            flirt.Fact = "They grew up by the sea.";
            Assert.That(TalkScreenLines.Aftermath(flirt, "Dana", new[] { spark }),
                Is.EqualTo(new[] { FreeTalk.Landed, spark, TalkScreenLines.LearnedPrefix + flirt.Fact }), "Before the change line.");

            var continuation = new TalkResult { NpcId = "x", Continuation = true, Choice = TalkChoice.Apologize, Change = RomanceChange.TurnAsideAnswered };
            Assert.That(TalkScreenLines.Aftermath(continuation, "Pat", new string[0]), Has.None.EqualTo(TalkScreenLines.NothingNew),
                "A continuation is not a counted talk: no 'nothing new'.");
        }

        [Test]
        public void TheBetweenYouBlockAndTheYourCardBlockAddTheRomanceParts()
        {
            Assert.That(TalkScreenLines.BondBlocks(null, null, null), Is.Empty);
            Assert.That(TalkScreenLines.BondBlocks("Friend", null, null).Single().Lines, Is.EqualTo(new[] { "Friend" }), "Slice 2's block as it was.");
            var block = TalkScreenLines.BondBlocks("Friend", "Romance: a spark between you.",
                new[] { TalkRomanceNotes.TookTheNoWell, "", TalkRomanceNotes.TookTheNoWell }).Single();
            Assert.That(block.Heading, Is.EqualTo(TalkScreenLines.BetweenYouHeading));
            Assert.That(block.Lines, Is.EqualTo(new[] { "Friend", "Romance: a spark between you.", TalkRomanceNotes.TookTheNoWell }),
                "Under the stage line: the romance's line, then its note once.");
            Assert.That(TalkScreenLines.BondBlocks(null, "Romance: a spark between you.", null).Single().Lines,
                Is.EqualTo(new[] { "Romance: a spark between you." }), "A Stranger with a spark still gets the block.");

            Assert.That(TalkScreenLines.YourReadBlocks(null, false), Is.Empty);
            Assert.That(TalkScreenLines.YourReadBlocks("The house reads you as a flirt.", false).Single().Lines,
                Is.EqualTo(new[] { "The house reads you as a flirt." }));
            var yours = TalkScreenLines.YourReadBlocks("The house reads you as a flirt.", true).Single();
            Assert.That(yours.Heading, Is.EqualTo(TalkScreenLines.YourReadHeading));
            Assert.That(yours.Lines, Is.EqualTo(new[] { "The house reads you as a flirt.", TalkRomanceNotes.TookTheNoWell }));
            Assert.That(TalkScreenLines.YourReadBlocks(null, true).Single().Lines, Is.EqualTo(new[] { TalkRomanceNotes.TookTheNoWell }));
        }
    }
}
