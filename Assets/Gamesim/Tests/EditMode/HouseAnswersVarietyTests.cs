using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The vote answer's line at story rules 12 (<see cref="HouseAnswers.VoteLineIndex"/>): housemates who answer in one voice
    /// and manner in a week never give the same line while the slot has one left, nobody gives last week's line again in the
    /// same manner, the notebook quotes the line the log gave, and nothing new is drawn from the season's stream. Pure
    /// simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class HouseAnswersVarietyTests
    {
        [Test]
        public void VotersInOneVoiceAndMannerGiveDifferentLinesAndTheNotebookQuotesEach()
        {
            int full = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var s12 = Campaign(StoryRules.Manners, seed);
                var s11 = Campaign(StoryRules.Roles, seed);
                var voters = EpisodeEngine.Voters(s12).Where(v => !v.isPlayer).Select(v => v.id).ToList();
                Assert.That(voters.Count, Is.GreaterThanOrEqualTo(6), "A twelve-seat house has voters enough to share a slot.");
                var engine12 = new EpisodeEngine(s12);
                var engine11 = new EpisodeEngine(s11);
                var quoted = new Dictionary<string, string>();
                foreach (string id in voters)
                {
                    var at = engine12.Snapshot;
                    var asked = engine12.Apply(VoteAnswerTests.Command(at, EpisodeCommandKind.AskVote, id));
                    Assert.That(asked.accepted, Is.True, asked.reason);
                    string text = asked.state.events.Last(e => e.kind == "vote-read").text;
                    string prefix = "You asked " + asked.state.Find(id).name + " where their head is at. \"";
                    Assert.That(text, Does.StartWith(prefix).And.EndWith("\""));
                    quoted[id] = text.Substring(prefix.Length, text.Length - prefix.Length - 1);
                    var at11 = engine11.Snapshot;
                    var asked11 = engine11.Apply(VoteAnswerTests.Command(at11, EpisodeCommandKind.AskVote, id));
                    Assert.That(asked11.accepted, Is.True, asked11.reason);
                }
                var after = engine12.Snapshot;
                Assert.That(after.randomState, Is.EqualTo(engine11.Snapshot.randomState), "seed " + seed + ": the lines draw nothing from the season's stream.");
                Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, error);

                var lineOf = new Dictionary<string, int>();
                foreach (string id in voters)
                {
                    var said = EpisodeEngine.Said(after, id, after.week);
                    Assert.That(said, Is.Not.Null);
                    string stated = said.stance == Candour.Neutral ? null
                        : after.ledger.claims.First(c => c.week == after.week && c.voterId == id && c.source == ClaimSource.Told).targetId;
                    lineOf[id] = Enumerable.Range(0, HouseAnswers.LinesASlot)
                        .Single(i => HouseAnswers.Fill(after, HouseAnswers.VoteLine(Voice.Plain, said.manner, i), stated) == quoted[id]);
                    // Read after every later voter has answered: an earlier answer's words never change.
                    var note = HouseguestNotes.For(after, id).Single(n => n.kind == HouseguestNotes.Kinds.Vote && n.text.Contains(": \""));
                    Assert.That(note.text, Does.Contain(": \"" + quoted[id] + "\" ("), "seed " + seed + ": the notebook quotes the log.");
                }
                foreach (var slot in voters.GroupBy(id => EpisodeEngine.Said(after, id, after.week).manner))
                {
                    var lines = slot.Select(id => lineOf[id]).ToList();
                    Assert.That(lines.Distinct().Count(), Is.EqualTo(System.Math.Min(lines.Count, HouseAnswers.LinesASlot)),
                        "seed " + seed + ", " + slot.Key + ": no line twice while the slot has one left (" + string.Join(", ", lines) + ").");
                    if (lines.Count >= HouseAnswers.LinesASlot) full++;
                }
            }
            Assert.That(full, Is.GreaterThan(0), "Some week had three voters in one voice and manner.");
        }

        [Test]
        public void NextWeekAVoterLeavesOutTheLineTheyGaveInThatManner()
        {
            int leftOut = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var s = Campaign(StoryRules.Manners, seed);
                string npc = EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).First();
                var after = VoteAnswerTests.Accepted(s, EpisodeCommandKind.AskVote, npc);
                var said = EpisodeEngine.Said(after, npc, after.week);
                var record = HouseAnswers.RecordedLine(after, npc, after.week);
                Assert.That(record, Is.Not.Null, "The answer's line is on the record.");
                Assert.That(record.manner, Is.EqualTo(said.manner));
                var row = after.story.cooldowns.Single(c => HouseAnswers.ParseLine(c.key) != null);
                Assert.That(row.untilWeek, Is.EqualTo(after.week + 2), "Kept through next week, past the week's turn.");

                var voter = after.Find(npc);
                int next = after.week + 1;
                int coin = StoryRandom.Index(after, HouseAnswers.VoteLineKey(next, npc), HouseAnswers.LinesASlot);
                int line = HouseAnswers.VoteLineIndex(after, next, voter, said.manner);
                Assert.That(line, Is.Not.EqualTo(record.index), "seed " + seed + ": not last week's line in the same manner.");
                if (coin == record.index) leftOut++;
                else Assert.That(line, Is.EqualTo(coin), "seed " + seed + ": otherwise the coin's line.");
                string another = said.manner == Manner.Definite ? Manner.Hedged : Manner.Definite;
                Assert.That(HouseAnswers.VoteLineIndex(after, next, voter, another), Is.EqualTo(coin), "Another manner's slot is free.");
            }
            Assert.That(leftOut, Is.GreaterThan(0), "Some seed's coin fell on last week's line.");
        }

        /// <summary>
        /// <see cref="VoteAnswerTests.Campaign"/> in a twelve-seat house: eight voters besides the player, every one with no
        /// traits, so all of them speak Plain and share a slot whenever they share a manner.
        /// </summary>
        private static EpisodeState Campaign(int rules, uint seed)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, seed);
            s.strategyRulesStartWeek = 1;
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            s.randomState = unchecked(seed * 2654435761u + 17u);
            foreach (var npc in s.Active.Where(c => !c.isPlayer)) npc.traits = new List<string>();
            EpisodeEngine.EnableStory(s);
            s.story.rulesVersion = rules;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }
    }
}
