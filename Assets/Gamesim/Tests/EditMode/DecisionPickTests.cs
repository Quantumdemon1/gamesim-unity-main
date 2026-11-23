using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Pick, then commit (UI-UX-PLAN O1a, O1b): the commit under the veto meeting, the ballot and the
    /// final choice names the result of the pick, sends the command the one press used to send, and
    /// holds only while the decision it was picked for is open.
    /// </summary>
    public sealed class DecisionPickTests
    {
        private static EpisodeState House(int size = 8, uint seed = 41)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            s.week = 3;
            return s;
        }

        private static List<string> Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static EpisodeState Meeting(bool playerHolds, bool playerHeads, bool holderOnTheBlock = false)
        {
            var s = House();
            var npcs = Npcs(s);
            s.phase = EpisodePhase.VetoMeeting;
            s.vetoResolved = false;
            s.hohId = playerHeads ? s.playerId : npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = playerHolds ? s.playerId : holderOnTheBlock ? npcs[1] : npcs[3];
            return s;
        }

        private static EpisodeState Ballot()
        {
            var s = House();
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Eviction;
            s.evictionResolved = false;
            s.evictionStage = EvictionStage.Voting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            return s;
        }

        private static EpisodeState FinalThree()
        {
            var s = House(6, 31);
            s.week = 9;
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.FinalEviction;
            s.hohId = s.playerId;
            return s;
        }

        private static string Name(EpisodeState s, string id) => s.Find(id).name;

        [Test]
        public void TheVetoMeetingsCommitNamesTheResultPicked()
        {
            var holder = Meeting(playerHolds: true, playerHeads: false);
            string first = holder.nominees[0];
            Assert.That(DecisionPick.KeepTheBlock(holder).CommitCaption(holder), Is.EqualTo("Commit · do not use the veto"));
            Assert.That(DecisionPick.Save(holder, first).CommitCaption(holder), Is.EqualTo("Commit · save " + Name(holder, first)));

            var both = Meeting(playerHolds: true, playerHeads: true);
            string replacement = EpisodeEngine.ReplacementCandidates(both).First().id;
            Assert.That(DecisionPick.Save(both, both.nominees[1], replacement).CommitCaption(both),
                Is.EqualTo("Commit · save " + Name(both, both.nominees[1]) + ", nominate " + Name(both, replacement)));

            var self = Meeting(playerHolds: true, playerHeads: false);
            self.nominees[0] = self.playerId;
            Assert.That(DecisionPick.Save(self, self.playerId).CommitCaption(self), Is.EqualTo("Commit · save yourself"),
                "The player's houseguest is named You: the commit says yourself, as the save does.");

            var heads = Meeting(playerHolds: false, playerHeads: true, holderOnTheBlock: true);
            string saved = EpisodeEngine.NpcVetoSave(heads);
            Assert.That(saved, Is.EqualTo(heads.vetoHolderId), "The fixture's holder is on the block and saves themself.");
            string named = EpisodeEngine.ReplacementCandidates(heads).First().id;
            Assert.That(DecisionPick.Save(heads, saved, named).CommitCaption(heads),
                Is.EqualTo("Commit · nominate " + Name(heads, named) + " as the replacement"));
        }

        [Test]
        public void TheBallotsAndTheFinalChoicesCommitNameTheResultPicked()
        {
            var s = Ballot();
            string target = s.nominees[1];
            Assert.That(DecisionPick.Ballot(s, target).CommitCaption(s), Is.EqualTo("Commit · vote to evict " + Name(s, target)));
            Assert.That(DecisionPick.Ballot(s, target).CommitCaption(s), Is.Not.EqualTo("Vote to evict " + Name(s, target)),
                "The commit never reads as the card that picks it.");

            var f = FinalThree();
            var others = FinalistRead.Others(f);
            Assert.That(others, Has.Count.EqualTo(2));
            var (take, cut) = (others[0], others[1]);
            var (taken, gone) = FinalChoiceWords.Names(take.name, cut.name);
            string caption = DecisionPick.FinalChoice(f, cut.id).CommitCaption(f);
            Assert.That(caption, Is.EqualTo("Commit · take " + taken + ", evict " + gone));
            Assert.That(caption, Is.Not.EqualTo(FinalChoiceWords.CaptionToEvict(f, cut.id)), "The commit never reads as the column's control.");
            Assert.That(DecisionPick.FinalChoice(f, take.id).CommitCaption(f), Is.Not.EqualTo(caption), "The two commits never read alike.");
        }

        [Test]
        public void APickCommitsTheCommandTheOnePressSent()
        {
            var holder = Meeting(playerHolds: true, playerHeads: false);
            var keep = DecisionPick.KeepTheBlock(holder);
            Assert.That((keep.Kind, keep.Target, keep.Second, keep.UseVeto), Is.EqualTo((EpisodeCommandKind.ResolveVeto, (string)null, (string)null, false)));
            var save = DecisionPick.Save(holder, holder.nominees[0]);
            Assert.That((save.Kind, save.Target, save.Second, save.UseVeto), Is.EqualTo((EpisodeCommandKind.ResolveVeto, holder.nominees[0], (string)null, true)));

            var both = Meeting(playerHolds: true, playerHeads: true);
            string replacement = EpisodeEngine.ReplacementCandidates(both).First().id;
            var named = DecisionPick.Save(both, both.nominees[0], replacement);
            Assert.That((named.Kind, named.Target, named.Second, named.UseVeto), Is.EqualTo((EpisodeCommandKind.ResolveVeto, both.nominees[0], replacement, true)));

            var s = Ballot();
            var vote = DecisionPick.Ballot(s, s.nominees[0]);
            Assert.That((vote.Kind, vote.Target, vote.Second, vote.UseVeto), Is.EqualTo((EpisodeCommandKind.CastVote, s.nominees[0], (string)null, false)));

            var f = FinalThree();
            string cut = FinalistRead.Others(f)[1].id;
            var choice = DecisionPick.FinalChoice(f, cut);
            Assert.That((choice.Kind, choice.Target, choice.Second, choice.UseVeto), Is.EqualTo((EpisodeCommandKind.FinalEvict, cut, (string)null, false)));
        }

        [Test]
        public void APickHoldsOnlyWhileItsDecisionIsOpen()
        {
            var holder = Meeting(playerHolds: true, playerHeads: false);
            var save = DecisionPick.Save(holder, holder.nominees[0]);
            Assert.That(save.Holds(holder), Is.True, "A pick holds on the screen it was made on.");
            Assert.That(DecisionPick.KeepTheBlock(holder).Holds(holder), Is.True);
            Assert.That(DecisionPick.Save(holder, holder.nominees[0], EpisodeEngine.ReplacementCandidates(holder).First().id).Holds(holder), Is.False,
                "A holder who is not Head of Household names no replacement.");

            holder.vetoResolved = true;
            Assert.That(save.Holds(holder), Is.False, "Not once the meeting is decided,");
            holder.vetoResolved = false;
            holder.week++;
            Assert.That(save.Holds(holder), Is.False, "nor in another week,");
            holder.week--;
            holder.sessionId = holder.sessionId + "-other";
            Assert.That(save.Holds(holder), Is.False, "nor in another season.");

            var notHolder = Meeting(playerHolds: false, playerHeads: false);
            Assert.That(DecisionPick.KeepTheBlock(notHolder).Holds(notHolder), Is.False, "The block is the holder's to keep.");

            var heads = Meeting(playerHolds: false, playerHeads: true, holderOnTheBlock: true);
            string saved = EpisodeEngine.NpcVetoSave(heads);
            string named = EpisodeEngine.ReplacementCandidates(heads).First().id;
            Assert.That(DecisionPick.Save(heads, saved, named).Holds(heads), Is.True, "The Head of Household names the replacement for the houseguest's save,");
            string other = heads.nominees.First(id => id != saved);
            Assert.That(DecisionPick.Save(heads, other, named).Holds(heads), Is.False, "and never overrides whom the houseguest saved.");

            var s = Ballot();
            var vote = DecisionPick.Ballot(s, s.nominees[0]);
            Assert.That(vote.Holds(s), Is.True);
            Assert.That(DecisionPick.Ballot(s, Npcs(s)[3]).Holds(s), Is.False, "A ballot names a nominee.");
            s.votes.Add(new VoteState { voterId = s.playerId, targetId = s.nominees[1] });
            Assert.That(vote.Holds(s), Is.False, "A ballot already cast leaves no pick to commit.");

            var f = FinalThree();
            var choice = DecisionPick.FinalChoice(f, FinalistRead.Others(f)[0].id);
            Assert.That(choice.Holds(f), Is.True);
            Assert.That(DecisionPick.FinalChoice(f, f.playerId).Holds(f), Is.False, "The player never evicts themself.");
            f.phase = EpisodePhase.JuryQuestioning;
            Assert.That(choice.Holds(f), Is.False, "Not once the choice is made.");
        }
    }
}
