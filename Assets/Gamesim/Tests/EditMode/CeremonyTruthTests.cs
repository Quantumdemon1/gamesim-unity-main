using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The pure parts of honest ceremonies: the sound a commit makes, the order the keys are dealt
    /// in, the jury's line on the finale panel, and the tie-break the vote reveal must mark.
    /// </summary>
    public sealed class CeremonyTruthTests
    {
        [Test]
        public void TheCommitSoundIsTheBiggestBeatItAppended()
        {
            Assert.That(EpisodeDirector.CommitCue(new[] { "eviction", "vote-reveal", "vote-reveal", "diary" }), Is.EqualTo(HouseAudio.Cue.Eviction),
                "An eviction is followed by its votes read out and the next week's diary; its sound is still the eviction's.");
            Assert.That(EpisodeDirector.CommitCue(new[] { "veto", "deal-outcome" }), Is.EqualTo(HouseAudio.Cue.Veto),
                "A deal settling at the veto meeting does not take the veto's sound.");
            Assert.That(EpisodeDirector.CommitCue(new[] { "eviction", "winner" }), Is.EqualTo(HouseAudio.Cue.Finale));
            Assert.That(EpisodeDirector.CommitCue(new[] { "nomination", "memory" }), Is.EqualTo(HouseAudio.Cue.Nomination));
            Assert.That(EpisodeDirector.CommitCue(new[] { "social", "competition" }), Is.EqualTo(HouseAudio.Cue.CompetitionWin));
            Assert.That(EpisodeDirector.CommitCue(new[] { "social" }), Is.EqualTo(HouseAudio.Cue.Button));
            Assert.That(EpisodeDirector.CommitCue(null), Is.EqualTo(HouseAudio.Cue.Button));
        }

        [Test]
        public void TheKeysAreDealtInAShuffledOrderThatARestartRepeats()
        {
            var ids = new[] { "player", "a", "b", "c", "d" };
            var once = EpisodeDirector.KeyOrder(ids, 1234u, 3);
            Assert.That(once, Is.EquivalentTo(ids), "Everybody who draws gets a key, once.");
            Assert.That(EpisodeDirector.KeyOrder(ids, 1234u, 3), Is.EqualTo(once), "The same week of the same season deals the same way.");

            var firsts = Enumerable.Range(1, 12).Select(week => EpisodeDirector.KeyOrder(ids, 1234u, week)[0]).ToList();
            Assert.That(firsts.Count(id => id == "player"), Is.LessThan(12),
                "The player, first in the cast, is not dealt the first key every week: that told a safe player their fate.");
            Assert.That(firsts.Distinct().Count(), Is.GreaterThanOrEqualTo(3), "The first key goes round the house.");
        }

        private static EpisodeState Finale(int jurors, bool playerSecond)
        {
            var state = new EpisodeState { playerId = "p" };
            var first = new ContestantState { id = playerSecond ? "a" : "p", name = playerSecond ? "Alex" : "Pat", status = ContestantStatus.Active };
            var second = new ContestantState { id = playerSecond ? "p" : "b", name = playerSecond ? "Pat" : "Blair", status = ContestantStatus.Active };
            state.contestants.Add(first);
            state.contestants.Add(second);
            for (int i = 0; i < jurors; i++)
                state.contestants.Add(new ContestantState { id = "j" + i, name = "Juror " + i, status = ContestantStatus.Jury });
            return state;
        }

        [Test]
        public void TheJuryLineCountsTheRealJuryAndNamesWhoATieGoesTo()
        {
            Assert.That(EpisodeDirector.JuryLine(Finale(6, false)),
                Is.EqualTo("6 jurors choose the winner. If they split evenly, the win goes to Blair, as the reference game's tie rule has it."),
                "The default house seats six jurors; the panel said four.");
            Assert.That(EpisodeDirector.JuryLine(Finale(5, false)), Is.EqualTo("5 jurors choose the winner."), "An odd jury cannot split.");
            Assert.That(EpisodeDirector.JuryLine(Finale(4, true)), Does.EndWith("the win goes to you, as the reference game's tie rule has it."));
            Assert.That(EpisodeDirector.JuryLine(Finale(1, false)), Is.EqualTo("One juror chooses the winner."));
        }

        [Test]
        public void TheHeadOfHouseholdsVoteIsMarkedAsTheTieBreak()
        {
            var state = new EpisodeState { playerId = "p", hohId = "h" };
            foreach (var id in new[] { "p", "h", "a", "b", "c", "d" })
                state.contestants.Add(new ContestantState { id = id, name = id.ToUpperInvariant(), status = ContestantStatus.Active });
            state.votes.Add(new VoteState { voterId = "p", targetId = "a" });
            state.votes.Add(new VoteState { voterId = "c", targetId = "b" });
            state.votes.Add(new VoteState { voterId = "h", targetId = "a", reason = "HoH tie-break" });
            var ballots = EpisodeDirector.EvictionBallots(state);
            Assert.That(ballots.Select(ballot => ballot.TieBreak), Is.EqualTo(new[] { false, false, true }),
                "Only the Head of Household's vote breaks the tie; counted with the house's, a 1-1 tie read 2-1.");
        }
    }
}
