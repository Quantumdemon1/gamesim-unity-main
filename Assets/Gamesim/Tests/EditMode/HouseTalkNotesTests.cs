using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What the notebook and the profile show of the house talk (plan 32 §C and §G4, S1): at story rules 12 the notes page
    /// gives this week's answer as it was said and, beside an answer in a manner the voter lied in before, "Last time they
    /// sounded like this, they lied."; the profile's blocks hold "Their manner" and "What you've learned" only where their
    /// evidence exists, and never "How they seem to see you". Below 12 neither changes. Pure simulation (the director's
    /// one-line hook copies the blocks into the profile), so the Unity-free subset runs it.
    /// </summary>
    public sealed class HouseTalkNotesTests
    {
        private static (EpisodeState s, string npc) Asked(int rules, uint seed = 7)
        {
            var s = VoteAnswerTests.Campaign(rules, seed);
            string npc = VoteAnswerTests.Voter(s, 0);
            s.Find(npc).traits = new List<string> { "Sneaky", "Competitive" };
            return (VoteAnswerTests.Accepted(s, EpisodeCommandKind.AskVote, npc), npc);
        }

        [Test]
        public void BelowTwelveTheNotesAndTheProfileAreUnchanged()
        {
            var (s, npc) = Asked(StoryRules.Roles);
            Assert.That(HouseguestNotes.HouseTalkBlocks(s, npc), Is.Empty);
            Assert.That(HouseguestNotes.For(s, npc).Any(n => n.text.Contains("\" (")), Is.False);
        }

        [Test]
        public void AtTwelveTheNotesGiveThisWeeksAnswerAsItWasSaid()
        {
            for (uint seed = 1; seed <= 12; seed++)
            {
                var (s, npc) = Asked(StoryRules.Manners, seed);
                var said = EpisodeEngine.Said(s, npc, s.week);
                string line = s.events.Last(e => e.kind == "vote-read").text;
                string quoted = line.Substring(line.IndexOf('"') + 1).TrimEnd('"');
                var note = HouseguestNotes.For(s, npc).Single(n => n.text.Contains("\" ("));
                Assert.That(note.text, Is.EqualTo(FinalistRead.FirstName(s.Find(npc).name) + ": \"" + quoted + "\" (" + HouseAnswers.MannerWords(said.manner) + ")"),
                    "seed " + seed + ": the same words the log holds, rebuilt from the record.");
                Assert.That(note.kind, Is.EqualTo(HouseguestNotes.Kinds.Vote));
            }
        }

        [Test]
        public void AnAnswerInAMannerTheyLiedInBeforeSaysSo()
        {
            var (s, npc) = Asked(StoryRules.Manners);
            var said = EpisodeEngine.Said(s, npc, s.week);
            Assert.That(HouseguestNotes.For(s, npc).Any(n => n.text.Contains(HouseAnswers.LastTimeTheyLied)), Is.False, "Nothing learned yet.");
            s.story.cooldowns.Add(new StoryCooldownState { key = EpisodeEngine.TellPrefix + npc + ":" + (Manner.IsTell(said.manner) ? said.manner : Manner.Hedged), untilWeek = s.week + 100 });
            bool same = Manner.IsTell(said.manner);
            Assert.That(HouseguestNotes.For(s, npc).Any(n => n.text.EndsWith(" · " + HouseAnswers.LastTimeTheyLied, StringComparison.Ordinal)), Is.EqualTo(same),
                "Only beside an answer in the manner they lied in.");
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }

        [Test]
        public void TheProfileBlocksNeedTheirEvidenceAndNeverSayHowTheySeeYou()
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners);
            string npc = VoteAnswerTests.Voter(s, 0);
            Assert.That(HouseguestNotes.HouseTalkBlocks(s, npc), Is.Empty, "Never asked, never caught: nothing to show.");
            var after = VoteAnswerTests.Accepted(s, EpisodeCommandKind.AskVote, npc);
            var blocks = HouseguestNotes.HouseTalkBlocks(after, npc);
            Assert.That(blocks.Select(b => b.Heading), Is.EqualTo(new[] { HouseguestNotes.TheirMannerHeading }));
            Assert.That(blocks[0].Lines[0], Is.EqualTo(HouseAnswers.VoiceWords(Voice.Of(after.Find(npc), false))));
            after.story.cooldowns.Add(new StoryCooldownState { key = EpisodeEngine.TellPrefix + npc + ":" + Manner.TurnedBack, untilWeek = after.week + 100 });
            blocks = HouseguestNotes.HouseTalkBlocks(after, npc);
            Assert.That(blocks.Select(b => b.Heading), Is.EqualTo(new[] { HouseguestNotes.TheirMannerHeading, HouseguestNotes.WhatYouveLearnedHeading }));
            Assert.That(blocks[1].Lines.Single(), Does.Contain(HouseAnswers.MannerWords(Manner.TurnedBack)));
            Assert.That(blocks.Any(b => b.Heading == "How they seem to see you"), Is.False, "That block joins from 13.");
            foreach (var line in blocks.SelectMany(b => b.Lines))
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(line, @"\d"), Is.False, "Never a number: " + line);
        }

        [Test]
        public void TheProfileNeverShowsWhatTheyHaveNotShownThePlayer()
        {
            // A lie they told, judged nowhere yet: no tell is shown before the reveal, and the dominant tell is never named.
            var s = VoteAnswerTests.Campaign(StoryRules.Manners);
            string npc = VoteAnswerTests.Voter(s, 0);
            s.Find(npc).traits = new List<string> { "Confrontational", "Emotional" };
            var after = VoteAnswerTests.Accepted(s, EpisodeCommandKind.AskVote, npc);
            var lines = HouseguestNotes.HouseTalkBlocks(after, npc).SelectMany(b => b.Lines).ToList();
            Assert.That(lines.Any(l => l.Contains("lied")), Is.False, "Nothing learned before a reveal.");
            Assert.That(lines.Any(l => l.Contains(Candour.Dishonest) || l.Contains(Candour.Honest)), Is.False, "Never the stance.");
        }

        [Test]
        public void AnInformationPartnersReadingAfterTheAskNeverChangesTheQuotedAnswer()
        {
            // Under the commitment rules an information partner the player asked during the campaign also passes a reading as
            // campaigning closes: a second Told claim this week, which may name the other nominee. The note quotes the ask.
            int differed = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var s = VoteAnswerTests.Campaign(StoryRules.Manners, seed, true);
                string npc = VoteAnswerTests.Voter(s, 0);
                s.Find(npc).traits = new List<string> { "Sneaky", "Competitive" };
                s.deals.Add(new DealState
                {
                    id = "deal-info", type = DealKind.InformationSharing, proposerId = s.playerId, recipientId = npc,
                    status = DealStatus.Active, week = s.week, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
                });
                var asked = VoteAnswerTests.Accepted(s, EpisodeCommandKind.AskVote, npc);
                if (EpisodeEngine.Said(asked, npc, asked.week).stance == Candour.Neutral) continue;
                string line = asked.events.Last(e => e.kind == "vote-read").text;
                string quoted = line.Substring(line.IndexOf('"') + 1).TrimEnd('"');
                var closed = VoteAnswerTests.Accepted(asked, EpisodeCommandKind.Advance, null);
                var told = closed.ledger.claims.Where(c => c.week == closed.week && c.voterId == npc && c.source == ClaimSource.Told).ToList();
                Assert.That(told.Count, Is.EqualTo(2), "seed " + seed + ": the ask's claim, then the reading's.");
                if (told[0].targetId != told[1].targetId) differed++;
                var note = HouseguestNotes.For(closed, npc).Single(n => n.text.Contains("\" ("));
                Assert.That(note.text, Does.StartWith(FinalistRead.FirstName(closed.Find(npc).name) + ": \"" + quoted + "\" ("),
                    "seed " + seed + ": the answer as the ask said it, whatever the reading named.");
            }
            Assert.That(differed, Is.GreaterThan(0), "Some reading named the other nominee, so the order mattered.");
        }
    }
}
