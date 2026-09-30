using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void TheJuryIsReadInAShuffledOrderThatAReloadRepeats()
        {
            var ids = new[] { "player", "a", "b", "c", "d", "e" };
            var once = EpisodeDirector.JuryOrder(ids, 1234u);
            Assert.That(once, Is.EquivalentTo(ids), "Every juror is read, once.");
            Assert.That(EpisodeDirector.JuryOrder(ids, 1234u), Is.EqualTo(once), "The same season reads the same way.");
            var firsts = Enumerable.Range(1, 16).Select(seed => EpisodeDirector.JuryOrder(ids, (uint)seed)[0]).ToList();
            Assert.That(firsts.Count(id => id == "player"), Is.LessThan(16),
                "The player, first in the cast, is not read first in every season: the engine records the jury in cast order.");
            Assert.That(firsts.Distinct().Count(), Is.GreaterThanOrEqualTo(3), "Who is read first goes round the jury.");
            Assert.That(EpisodeDirector.JuryOrder(ids, 7u), Is.Not.EqualTo(EpisodeDirector.KeyOrder(ids, 7u, 0)),
                "Its own deal, not the keys'.");
        }

        [Test]
        public void TheJurysVoteIsPacedForSuspenseOrQuickly()
        {
            Assert.That(CeremonyPacing.JuryIntro(CeremonyPace.Suspenseful), Is.EqualTo(2.6f));
            Assert.That(CeremonyPacing.JuryIntro(CeremonyPace.Quick), Is.EqualTo(1.0f));
            Assert.That(CeremonyPacing.PerJuror(CeremonyPace.Suspenseful, 7), Is.EqualTo(2.2f), "Near the reference's 2.5 s a juror,");
            Assert.That(CeremonyPacing.PerJuror(CeremonyPace.Suspenseful, 8), Is.EqualTo(1.6f), "and quicker for a big jury.");
            Assert.That(CeremonyPacing.PerJuror(CeremonyPace.Quick, 14), Is.EqualTo(0.55f));
            Assert.That(CeremonyPacing.DecidingBeat(CeremonyPace.Suspenseful), Is.EqualTo(2.0f));
            Assert.That(CeremonyPacing.DecidingBeat(CeremonyPace.Quick), Is.EqualTo(0.3f), "A pause even at the quick pace.");
            Assert.That(CeremonyPacing.WinnerHold(CeremonyPace.Suspenseful), Is.EqualTo(5.2f));
            Assert.That(CeremonyPacing.WinnerHold(CeremonyPace.Quick), Is.EqualTo(3.2f));
            Assert.That(CeremonyPacing.WinnerHold(CeremonyPace.Quick), Is.GreaterThanOrEqualTo(ConfettiBurst.Seconds),
                "The winner holds the stage for as long as the confetti is in the air.");
        }

        [Test]
        public void TheJuryStandsInTheLivingRoomClearOfFurnitureAndEachOther()
        {
            // A living room 5.2 m square with the kitchen beyond it, a sofa east of its middle, and
            // the player standing north of it.
            var centre = new Vector3(2f, 0f, -3f);
            bool InLiving(Vector3 spot) => Mathf.Abs(spot.x - centre.x) <= 2.6f && Mathf.Abs(spot.z - centre.z) <= 2.6f;
            EpisodeDirector.FloorSampler floor = (Vector3 wanted, out Vector3 sampled, out string room) =>
            {
                sampled = wanted;
                room = InLiving(wanted) ? "Living" : "Kitchen";
                return true;
            };
            var sofa = centre + new Vector3(1.6f, 0f, 0f);
            var player = centre + new Vector3(0f, 0f, 1.6f);
            var places = EpisodeDirector.JuryPlaces(centre, 30, new[] { player }, floor, spot => (spot - sofa).magnitude > 0.5f);

            Assert.That(places.Count, Is.InRange(8, 29), "A room that cannot hold thirty jurors holds what fits,");
            Assert.That(places.All(InLiving), Is.True, "every place in the living room, even where its rings reach the kitchen,");
            Assert.That(places.All(spot => (spot - sofa).magnitude > 0.5f), Is.True, "none on the sofa,");
            Assert.That(places.All(spot => (spot - player).magnitude >= 1f), Is.True, "none on the player,");
            for (int i = 0; i < places.Count; i++)
                for (int j = i + 1; j < places.Count; j++)
                    Assert.That((places[i] - places[j]).magnitude, Is.GreaterThanOrEqualTo(1f), "and a metre between jurors.");
            Assert.That((places[0] - centre).magnitude, Is.EqualTo(1.6f).Within(1e-4f), "Nearest the middle first.");
            Assert.That(EpisodeDirector.JuryPlaces(centre, 5, new[] { player }, floor, spot => true).Count, Is.EqualTo(5),
                "A jury the room can hold is placed whole.");
        }

        [Test]
        public void TheGoodbyeAtTheDoorIsHowTheyLeaveThingsWithYou()
        {
            var state = ContentCatalog.Create(5);
            var leaving = state.contestants.First(c => !c.isPlayer && c.name.Contains(" "));
            string first = leaving.name.Split(' ')[0];
            const string after = " They'll be waiting in the jury house.";
            void Feels(double score)
            {
                var edge = state.relationships.FirstOrDefault(r => r.fromId == leaving.id && r.toId == state.playerId);
                if (edge == null) state.relationships.Add(edge = new RelationshipState { fromId = leaving.id, toId = state.playerId });
                edge.score = score;
            }
            // The player's own view of them does not decide it: how the evicted feels leaving does.
            var mine = state.relationships.FirstOrDefault(r => r.fromId == state.playerId && r.toId == leaving.id);
            if (mine == null) state.relationships.Add(mine = new RelationshipState { fromId = state.playerId, toId = leaving.id });
            mine.score = 80;

            Feels(19);
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " walks to the door without looking back." + after));
            Feels(20);
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " gives you one last look before walking out the door." + after),
                "Warmth toward you is a last look,");
            Feels(-20);
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " glares at you from the doorway." + after), "and a grudge a glare.");
            Feels(-19);
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " walks to the door without looking back." + after));

            Feels(-60);
            var deal = new DealState { id = "d", type = DealKind.FinalTwo, proposerId = leaving.id, recipientId = state.playerId,
                status = DealStatus.Proposed, week = 1 };
            state.deals.Add(deal);
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " glares at you from the doorway." + after),
                "An offer never taken up is not a deal between you,");
            deal.status = DealStatus.Active;
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " pauses at the door and turns to you…" + after),
                "but a deal is, whatever they feel, whoever proposed it.");
            deal.proposerId = state.playerId; deal.recipientId = leaving.id;
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " pauses at the door and turns to you…" + after));
            deal.status = DealStatus.Broken;
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " glares at you from the doorway." + after), "A broken one is not.");
            deal.status = DealStatus.Active; deal.recipientId = state.contestants.First(c => !c.isPlayer && c.id != leaving.id).id;
            Assert.That(EpisodeDirector.GoodbyeLine(state, leaving.id), Is.EqualTo(first + " glares at you from the doorway." + after),
                "Nor is a deal you made with somebody else.");

            Assert.That(EpisodeDirector.GoodbyeLine(state, "nobody"), Is.Empty);
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

        /// <summary>
        /// The screen's roster (MOCKUP-PASS-PLAN M18) needs to know who cast each ballot and how they
        /// look: every ballot carries its voter's id, name and a copy of their look, in the order cast.
        /// </summary>
        [Test]
        public void EachBallotCarriesItsVoterForTheScreensRoster()
        {
            var state = new EpisodeState { playerId = "p", hohId = "h" };
            foreach (var id in new[] { "p", "h", "a", "b", "c" })
                state.contestants.Add(new ContestantState { id = id, name = id.ToUpperInvariant(), status = ContestantStatus.Active });
            state.votes.Add(new VoteState { voterId = "c", targetId = "a" });
            state.votes.Add(new VoteState { voterId = "p", targetId = "b" });
            var ballots = EpisodeDirector.EvictionBallots(state);
            Assert.That(ballots.Select(ballot => ballot.VoterId), Is.EqualTo(new[] { "c", "p" }), "Who cast each, in the order cast,");
            Assert.That(ballots.Select(ballot => ballot.VoterName), Is.EqualTo(new[] { "C", "P" }), "by name,");
            Assert.That(ballots.Select(ballot => ballot.Character.id), Is.EqualTo(new[] { "c", "p" }), "with their look for the face,");
            Assert.That(ballots[0].Character, Is.Not.SameAs(state.Find("c")), "copied, as the nominees' are.");
            Assert.That(ballots.Select(ballot => ballot.TargetId), Is.EqualTo(new[] { "a", "b" }));
        }
    }
}
