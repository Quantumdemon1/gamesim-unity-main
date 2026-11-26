using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Housemates use first names when they talk to you (talk polish TP-1). Every token of a talk line says a first name, past a
    /// title; the two talk lines a season keeps (the standing line and a stage's event row) keep full card names below talk rules
    /// 4, so a season played today saves and logs the words it always did, and use first names from it; the talk screen and the
    /// housemate page show both in first names at any level; and the star pool's grudge talk, offered at week 4 whether or not a
    /// jury sits, no longer says one does. On the tests' own tables and renamed cards. Simulation only.
    /// </summary>
    public sealed class FreeTalkNamesTests
    {
        private const string Titled = "Dr. Pat Example", TitledFirst = "Pat";

        private static string First(ContestantState c) => CharacterNames.FirstName(c.name);

        [Test]
        public void EveryTokenSaysAFirstNamePastATitle()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Romance);
            var npcs = FreeTalkFixture.Npcs(s);
            var player = s.Find(s.playerId);
            npcs[0].name = Titled;
            player.name = "Robin Tester";
            npcs[1].name = "Lee Morgan";
            npcs[2].name = "Kai Brooks";
            npcs[3].name = "Sky Rivers";
            s.hohId = npcs[1].id;
            s.nominees = new List<string> { npcs[2].id, npcs[3].id };

            string line = TalkText.Fill(s, npcs[0].id, "(at the table) \"{name}, {player}, {hoh}, {nominees}.\"");
            Assert.That(line, Is.EqualTo("(at the table) \"Pat, Robin, Lee, Kai and Sky.\""), "A first name for every person a line names.");
            Assert.That(TalkScreenLines.Opener(s, npcs[0].id, new TalkRow { Open = "\"Hey, {player}.\"" }), Is.EqualTo("\"Hey, Robin.\""),
                "Their opening line on the talk screen.");
            Assert.That(TalkText.Fill(s, npcs[0].id, "\"{close} and {worry}.\"", new Dictionary<string, string> { ["close"] = "Sam", ["worry"] = "nobody" }),
                Is.EqualTo("\"Sam and nobody.\""), "A standing part is said as it was given.");
            s.hohId = null;
            s.nominees.Clear();
            Assert.That(TalkText.Fill(s, npcs[0].id, "\"{hoh}, {nominees}.\""), Is.EqualTo("\"nobody, nobody yet.\""), "Nobody is still nobody.");
        }

        [Test]
        public void TheSavedStandingLineKeepsFullNamesBelowTalkFourAndSaysFirstNamesFromIt()
        {
            var row = new TalkRow
            {
                Id = "steady.test.names", Pool = TalkPools.Steady, Kind = TalkKind.Standing,
                Standing = new TalkStandingLines { Honest = "\"{close} I trust. {worry} I watch, {player}. Me? {safe}.\"" },
            };
            foreach (int level in new[] { TalkRules.Talk, TalkRules.Chemistry, TalkRules.Romance, TalkRules.Couples })
            {
                var s = FreeTalkFixture.Fresh(level);
                var npcs = FreeTalkFixture.Npcs(s);
                var player = s.Find(s.playerId);
                player.name = "Robin Tester";
                npcs[1].name = Titled;
                npcs[2].name = "Kai Brooks";
                FreeTalkFixture.SetScore(s, npcs[0].id, npcs[1].id, 99);
                FreeTalkFixture.SetScore(s, npcs[0].id, npcs[2].id, -99);
                string safe = TalkStanding.Parts(s, npcs[0].id, Candour.Honest)["safe"];
                string full = "\"" + Titled + " I trust. Kai Brooks I watch, Robin Tester. Me? " + safe + ".\"";
                string first = "\"" + TitledFirst + " I trust. Kai I watch, Robin. Me? " + safe + ".\"";

                string saved = TalkStanding.Line(s, npcs[0].id, row, Candour.Honest);
                Assert.That(saved, Is.EqualTo(level >= TalkRules.Couples ? first : full),
                    "Talk rules " + level + ": below 4 the season saves the words it always saved; from 4, first names.");
                Assert.That(TalkText.FirstNames(s, saved), Is.EqualTo(first), "Talk rules " + level + ": shown in first names either way.");
            }
        }

        [Test]
        public void AStagesEventRowIsLoggedInTheFullNameBelowTalkFourAndTheScreenSaysTheFirstName()
        {
            using (TalkBank.Use(FreeTalkS2Fixture.Table()))
            {
                foreach (int level in new[] { TalkRules.Chemistry, TalkRules.Romance, TalkRules.Couples })
                {
                    var s = FreeTalkFixture.Fresh(level);
                    var npc = FreeTalkFixture.Npcs(s)[0];
                    npc.name = Titled;
                    FreeTalkS2Fixture.ClassAs(s, npc.id, TalkPools.Steady);
                    FreeTalkFixture.ContactOf(s, npc.id).rapport = 3;
                    FreeTalkS2Fixture.Views(s, npc.id, 30, 0);
                    var engine = new EpisodeEngine(s);
                    var before = engine.Snapshot;
                    var offer = FreeTalk.Offer(before, npc.id);
                    Assert.That(offer.Available, Is.True, offer.Reason);
                    Assert.That(offer.Talk.Kind, Is.EqualTo(TalkKind.Stage), "The control: a stage talk is on offer at talk rules " + level + ".");

                    var result = FreeTalk.Result(before, npc.id, offer.Talk.Id, TalkRules.Warm);
                    string logged = TalkFriendship.EventLine(offer.Talk.Stage, level >= TalkRules.Couples ? TitledFirst : Titled);
                    string said = TalkFriendship.EventLine(offer.Talk.Stage, TitledFirst);
                    Assert.That(result.StageEvent, Is.EqualTo(logged), "Talk rules " + level + ".");
                    Assert.That(FreeTalkFixture.Talk(engine, npc.id, TalkRules.Warm).accepted, Is.True);
                    var rows = engine.Snapshot.events
                        .Where(e => e.sequence >= before.nextSequence && e.kind == TalkFriendship.StageEventKind).Select(e => e.text).ToList();
                    Assert.That(rows, Is.EqualTo(new[] { logged }), "Talk rules " + level + ": the season's log.");
                    Assert.That(TalkScreenLines.ChangeLine(result, TitledFirst), Is.EqualTo(said), "Talk rules " + level + ": the screen says the first name.");
                    Assert.That(TalkScreenLines.Aftermath(result, TitledFirst).Last(), Is.EqualTo(said));
                    Assert.That(TalkScreenLines.ChangeLine(result), Is.EqualTo(logged), "With no first name to say, the line as logged.");
                }
            }
        }

        [Test]
        public void TheTalkScreenAndTheHousematePageShowTheSavedStandingLineInFirstNames()
        {
            var table = FreeTalkFixture.Table("st", "r1");
            foreach (var row in table.Where(r => r.Standing != null))
                row.Standing.Neutral = row.Standing.Honest = row.Standing.Dishonest = "\"{close} I trust. {worry} I watch. Me? {safe}.\"";
            using (TalkBank.Use(table))
            {
                var s = FreeTalkFixture.WeekTwo(TalkRules.Romance).Snapshot;
                ContestantState npc = null;
                foreach (var who in FreeTalkFixture.Npcs(s))
                {
                    var contact = FreeTalkFixture.ContactOf(s, who.id);
                    contact.talks = 1;
                    contact.rapport = 12;
                    FreeTalkFixture.SetScore(s, who.id, s.playerId, 40);
                    if (FreeTalk.Offer(s, who.id).Talk?.Kind == TalkKind.Standing) { npc = who; break; }
                }
                Assert.That(npc, Is.Not.Null, "The control: somebody's standing talk is on offer.");
                var engine = new EpisodeEngine(s);
                var before = engine.Snapshot;
                var offer = FreeTalk.Offer(before, npc.id);
                Assert.That(offer.Available, Is.True, offer.Reason);
                Assert.That(offer.Talk.Kind, Is.EqualTo(TalkKind.Standing), "The control: where they stand is on offer.");

                var result = FreeTalk.Result(before, npc.id, offer.Talk.Id, TalkRules.Warm);
                string close = TalkStanding.Parts(before, npc.id, result.Stance)["close"];
                Assert.That(close, Does.Contain(" "), "The control: talk rules 3 saves the full card name.");
                Assert.That(result.StandingLine, Does.StartWith("\"" + close + " I trust."));
                Assert.That(result.StandingSaid, Is.EqualTo(TalkText.FirstNames(before, result.StandingLine)));
                Assert.That(result.StandingSaid, Does.StartWith("\"" + CharacterNames.FirstName(close) + " I trust."), "Said in first names.");
                Assert.That(TalkScreenLines.Aftermath(result, First(npc)).First(), Is.EqualTo(result.StandingSaid), "The talk screen's third beat.");

                Assert.That(FreeTalkFixture.Talk(engine, npc.id, TalkRules.Warm).accepted, Is.True);
                var after = engine.Snapshot;
                Assert.That(FreeTalk.Contact(after, npc.id).standing, Is.EqualTo(result.StandingLine), "The season keeps the line it always kept.");
                Assert.That(TalkNotes.FromThem(after, npc.id).Line, Is.EqualTo(result.StandingSaid), "The housemate page says it as the talk did.");
            }
        }

        [Test]
        public void TheGrudgeTalkKeepsItsIdGateAndRepliesAndNoLongerSaysAJurySits()
        {
            var row = TalkBank.Find("star.r.grudge");
            Assert.That(row, Is.Not.Null, "The row keeps its id, so a season that spent it keeps it spent.");
            Assert.That(row.Pool, Is.EqualTo(TalkPools.Star));
            Assert.That(row.Kind, Is.EqualTo(TalkKind.Rhythm));
            Assert.That(row.When.Layer, Is.EqualTo(1));
            Assert.That(row.When.Week, Is.EqualTo(4));
            Assert.That(row.When.Window, Is.EqualTo(TalkWindows.AfterEviction));
            Assert.That(row.Replies.Select(r => r.Tone).ToArray(), Is.EqualTo(new[] { TalkRules.Careful, TalkRules.Warm, TalkRules.LetGo }));
            Assert.That(row.Open, Does.Not.Contain("jury").IgnoreCase, "Offered at week 4 whether or not a jury sits: the opener claims none.");
            Assert.That(row.Open, Does.Contain("Grudge"), "Its replies still answer a grudge count.");
        }
    }
}
