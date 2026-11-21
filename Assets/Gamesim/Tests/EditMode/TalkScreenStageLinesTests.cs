using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free "Just talk" screen's words at talk rules 2 (free talk slice 2), pure, so the Unity-free subset runs them: the
    /// housemate page's "Between you" line (the stage word, none for a Stranger, and the chemistry once shown), the player's
    /// own card's "The house reads you as ..." line, the Close friend's favour pills, and the third beat's lines (the favour,
    /// the chemistry said once, the stage reached). At talk rules 1 the page and the panel are exactly as slice 1 drew them.
    /// The director (EpisodeDirector.JustTalk.cs, EpisodeDirector.ProfileSources.cs) draws exactly these lines.
    /// </summary>
    public sealed class TalkScreenStageLinesTests
    {
        /// <summary>A contact with enough talk behind it for a stage and the chemistry to show at talk rules 2.</summary>
        private static string Bonded(EpisodeState s)
        {
            string npc = FreeTalkFixture.Npcs(s).First().id;
            var contact = FreeTalkFixture.ContactOf(s, npc);
            contact.rapport = 6;
            contact.talks = TalkChemistry.ShownAfterTalks;
            contact.agreement = 4;
            return npc;
        }

        private static TalkRow StageRow(FriendStage stage) => new TalkRow
        {
            Id = "steady.test.stage", Pool = TalkPools.Steady, Kind = TalkKind.Stage, Stage = stage,
            Open = "(by the pantry) \"Keep my name off your lips?\"",
            Replies = new[]
            {
                new TalkReply { Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you.\"", Lands = "\"Good.\"", Even = "\"Okay.\"", Grates = "\"Hm.\"" },
                new TalkReply { Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that.\"", Lands = "\"Fair.\"", Even = "\"Okay.\"", Grates = "\"Wow.\"" },
                new TalkReply { Tone = TalkRules.Tease, Line = "\"Depends what's for dinner.\"", Lands = "\"Ha.\"", Even = "\"Sure.\"", Grates = "\"Not funny.\"" },
            },
        };

        [Test]
        public void AtTalkRulesOneThePageAndThePanelAreAsSliceOneDrewThem()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Talk);
            string npc = Bonded(s);
            Assert.That(TalkScreenLines.BondBlocks(s, npc), Is.Empty, "No 'Between you' block at talk rules 1.");
            Assert.That(TalkScreenLines.YourReadBlocks(s), Is.Empty, "No 'How you come across' block at talk rules 1.");
            foreach (var reply in TalkScreenLines.Replies(s, npc, StageRow(FriendStage.Friend)))
                Assert.That(reply.Pill, Is.EqualTo(reply.ToneCaption), "Off the Close friend's talk a pill is the tone's caption.");
            var result = new TalkResult { NpcId = npc, Outcome = FreeTalk.Landed, Fact = "They grew up by the sea." };
            Assert.That(TalkScreenLines.Aftermath(result, "Dana"), Is.EqualTo(TalkScreenLines.Aftermath(result)),
                "A slice-1 result's third beat is unchanged by the name.");
            Assert.That(TalkScreenLines.Aftermath(result), Is.EqualTo(new List<string> { FreeTalk.Landed, TalkScreenLines.LearnedPrefix + result.Fact }));
        }

        [Test]
        public void AtTalkRulesTwoTheHousematesPageSaysTheStageAndTheChemistryInOneLine()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string npc = Bonded(s);
            uint random = s.randomState;
            int revision = s.revision;
            var bond = TalkNotes.BondWith(s, npc);
            Assert.That(bond, Is.Not.Null);
            Assert.That(bond.Stage, Is.GreaterThanOrEqualTo(FriendStage.Acquainted), "Rapport 6 holds a stage whatever the chemistry.");
            Assert.That(bond.ChemistryWord, Is.Not.Null, "Three counted talks show the chemistry.");
            var blocks = TalkScreenLines.BondBlocks(s, npc);
            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(blocks[0].Heading, Is.EqualTo(TalkScreenLines.BetweenYouHeading));
            Assert.That(blocks[0].Lines, Is.EqualTo(new List<string> { bond.StageWord + " · " + TalkNotes.ChemistryLabel + ": " + bond.ChemistryWord }));
            Assert.That(s.randomState, Is.EqualTo(random), "The page reads nothing from the season's stream.");
            Assert.That(s.revision, Is.EqualTo(revision));

            // Before the third counted talk the chemistry is absent; a Stranger with no chemistry has no block at all.
            FreeTalkFixture.ContactOf(s, npc).talks = TalkChemistry.ShownAfterTalks - 1;
            Assert.That(TalkScreenLines.BondBlocks(s, npc).Single().Lines.Single(), Is.EqualTo(TalkNotes.BondWith(s, npc).StageWord));
            string stranger = FreeTalkFixture.Npcs(s).Last().id;
            Assert.That(TalkNotes.BondWith(s, stranger).Stage, Is.EqualTo(FriendStage.Stranger));
            Assert.That(TalkScreenLines.BondBlocks(s, stranger), Is.Empty, "Nothing is said of a Stranger.");
        }

        [Test]
        public void BondLineLeavesTheStrangerOutAndNamesTheChemistryOnlyOnceShown()
        {
            Assert.That(TalkScreenLines.BondLine(null), Is.Null);
            Assert.That(TalkScreenLines.BondLine(new TalkNotes.Bond { Stage = FriendStage.Stranger, StageWord = "Stranger" }), Is.Null);
            Assert.That(TalkScreenLines.BondLine(new TalkNotes.Bond { Stage = FriendStage.Stranger, StageWord = "Stranger", ChemistryWord = TalkChemistry.GratingWord }),
                Is.EqualTo("Chemistry: grating"));
            Assert.That(TalkScreenLines.BondLine(new TalkNotes.Bond { Stage = FriendStage.CloseFriend, StageWord = "Close friend", ChemistryWord = TalkChemistry.KindredWord }),
                Is.EqualTo("Close friend · Chemistry: kindred"));
            Assert.That(TalkScreenLines.BondLine(new TalkNotes.Bond { Stage = FriendStage.Friendly, StageWord = "Friendly" }), Is.EqualTo("Friendly"));
        }

        [Test]
        public void AtTalkRulesTwoThePlayersCardSaysHowTheHouseReadsThem()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            var lines = new HashSet<string>();
            foreach (int lean in new[] { -TalkRules.MostLean, 0, TalkRules.MostLean })
            {
                s.story.talkLean = lean;
                var blocks = TalkScreenLines.YourReadBlocks(s);
                Assert.That(blocks.Count, Is.EqualTo(1));
                Assert.That(blocks[0].Heading, Is.EqualTo(TalkScreenLines.YourReadHeading));
                string line = blocks[0].Lines.Single();
                Assert.That(line, Is.EqualTo(TalkNotes.HouseReadsYou(s)));
                Assert.That(line, Does.StartWith(TalkLean.ReadsYouAs).And.EndWith("."));
                lines.Add(line);
            }
            Assert.That(lines.Count, Is.GreaterThanOrEqualTo(2), "The lean moves the read one class at the ends.");
        }

        [Test]
        public void TheCloseFriendsFavourRepliesWearTheFavoursWordsAndTheThirdBeatNamesTheMoment()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string npc = FreeTalkFixture.Npcs(s).First().id;
            var replies = TalkScreenLines.Replies(s, npc, StageRow(FriendStage.CloseFriend));
            Assert.That(replies.Select(r => r.Words), Is.EqualTo(new[] { "I've got you.", "Can't promise that.", "Depends what's for dinner." }),
                "The captions are the replies' own words.");
            Assert.That(replies.Select(r => r.Pill), Is.EqualTo(new[] { TalkScreenLines.FavourKeepPill, TalkScreenLines.FavourRefusePill, TalkTone.Caption(TalkRules.Tease) }));
            Assert.That(TalkScreenLines.Replies(s, npc, StageRow(FriendStage.Friend)).All(r => r.Favour == TalkFavour.None), Is.True,
                "A favour is the Close friend's talk's alone.");

            var result = new TalkResult
            {
                NpcId = npc, Outcome = FreeTalk.Landed, Favour = TalkFavour.Keep, ChemistryShown = TalkChemistry.EasyWord,
                StageReached = FriendStage.CloseFriend, StageEvent = TalkFriendship.EventLine(FriendStage.CloseFriend, "Dana"),
            };
            Assert.That(TalkScreenLines.Aftermath(result, "Dana"), Is.EqualTo(new List<string>
            {
                FreeTalk.Landed, "You gave Dana your word.", "You two are easy company.", result.StageEvent,
            }));
            Assert.That(TalkScreenLines.FavourLine(TalkFavour.Refuse, "Dana"), Is.EqualTo("You didn't promise Dana anything."));
            Assert.That(TalkScreenLines.FavourLine(TalkFavour.None, "Dana"), Is.Null);
            result.Fact = "They grew up by the sea.";
            Assert.That(TalkScreenLines.ChangeLine(result), Is.EqualTo(result.StageEvent), "A stage reached is the line of what changed.");
            var words = new[] { TalkChemistry.KindredWord, TalkChemistry.EasyWord, TalkChemistry.NeitherWord, TalkChemistry.GratingWord };
            Assert.That(words.Select(TalkScreenLines.ChemistryFirstLine).Distinct().Count(l => !string.IsNullOrEmpty(l)), Is.EqualTo(4),
                "Every chemistry word is said once in its own line.");
            Assert.That(TalkScreenLines.ChemistryFirstLine(null), Is.Null);
        }
    }
}
