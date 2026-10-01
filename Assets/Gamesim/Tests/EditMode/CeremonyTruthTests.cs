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

        /// <summary>
        /// The goodbye's tone is what the player knows of how the evicted leave things with them
        /// (MOCKUP-PASS-PLAN decision 6A, PACK8-PASS-PLAN C2): a deal between them, the player's own
        /// vote or nomination against them, the player's vote to keep them. It was their hidden
        /// feeling toward the player, which the line gave away as they left for the jury.
        /// </summary>
        [Test]
        public void TheGoodbyeAtTheDoorIsHowTheyLeaveThingsWithYou()
        {
            var state = ContentCatalog.Create(5);
            var named = state.contestants.Where(c => !c.isPlayer && c.name.Contains(" ")).ToList();
            Assert.That(named.Count, Is.GreaterThanOrEqualTo(3), "Three houseguests with a first name to say goodbye by.");
            var leaving = named[0];
            var other = named[1];
            var hoh = named[2];
            string first = leaving.name.Split(' ')[0];
            const string after = " They'll be waiting in the jury house.";
            const string neutral = " walks to the door without looking back.";
            const string warm = " gives you one last look before walking out the door.";
            const string cold = " glares at you from the doorway.";
            const string dealt = " pauses at the door and turns to you…";
            leaving.status = ContestantStatus.Jury;
            state.hohId = hoh.id;
            state.nominees = new List<string> { leaving.id, other.id };
            state.votes.Clear();
            state.deals.Clear();
            string Line() => EpisodeDirector.GoodbyeLine(state, leaving.id);
            EpisodeDirector.GoodbyeKind Tone() => EpisodeDirector.GoodbyeTone(state, leaving.id);
            void Feels(double score)
            {
                var edge = state.relationships.FirstOrDefault(r => r.fromId == leaving.id && r.toId == state.playerId);
                if (edge == null) state.relationships.Add(edge = new RelationshipState { fromId = leaving.id, toId = state.playerId });
                edge.score = score;
            }

            // How they feel about the player decides nothing: the player cannot know it.
            foreach (double score in new[] { 80d, 20d, 0d, -20d, -80d })
            {
                Feels(score);
                Assert.That(Line(), Is.EqualTo(first + neutral + after), "Their hidden feeling of " + score + " is not the player's to read.");
                Assert.That(Tone(), Is.EqualTo(EpisodeDirector.GoodbyeKind.Neutral));
            }

            // The player's own ballot is - and the evicted can know it only where the count proves
            // it from their seat (UI-UX-PASS-PLAN B0): the reveal reads the count, not the ballots.
            Assert.That(named.Count, Is.GreaterThanOrEqualTo(4), "A fourth houseguest to vote beside the player.");
            state.evictionResolved = true;
            var ballot = new VoteState { voterId = state.playerId, targetId = leaving.id };
            var theirs = new VoteState { voterId = named[3].id, targetId = other.id };
            state.votes.Add(ballot);
            state.votes.Add(theirs);
            Assert.That(Line(), Is.EqualTo(first + neutral + after), "A split count says nothing of your ballot to the one going.");
            Assert.That(Tone(), Is.EqualTo(EpisodeDirector.GoodbyeKind.Neutral));
            theirs.targetId = leaving.id;
            Assert.That(Line(), Is.EqualTo(first + cold + after), "A unanimous vote against them proves yours, and it is a glare,");
            Assert.That(Tone(), Is.EqualTo(EpisodeDirector.GoodbyeKind.Cold));
            ballot.targetId = other.id; theirs.targetId = other.id;
            Assert.That(Line(), Is.EqualTo(first + warm + after), "and a unanimous vote the other way proves your vote to keep them: a last look.");
            Assert.That(Tone(), Is.EqualTo(EpisodeDirector.GoodbyeKind.Warm));
            state.votes.Clear();
            state.votes.Add(new VoteState { voterId = other.id, targetId = leaving.id });
            Assert.That(Line(), Is.EqualTo(first + neutral + after), "Somebody else's vote is not yours.");

            // So is putting them on the block.
            state.hohId = state.playerId;
            Assert.That(Line(), Is.EqualTo(first + cold + after), "Your nomination of them is a glare.");
            state.hohId = hoh.id;

            var deal = new DealState { id = "d", type = DealKind.FinalTwo, proposerId = leaving.id, recipientId = state.playerId,
                status = DealStatus.Proposed, week = 1 };
            state.deals.Add(deal);
            Assert.That(Line(), Is.EqualTo(first + neutral + after), "An offer never taken up is not a deal between you,");
            deal.status = DealStatus.Active;
            Assert.That(Line(), Is.EqualTo(first + dealt + after), "but a deal is, whoever proposed it,");
            Assert.That(Tone(), Is.EqualTo(EpisodeDirector.GoodbyeKind.Dealt));
            state.votes.Add(new VoteState { voterId = state.playerId, targetId = leaving.id });
            Assert.That(Line(), Is.EqualTo(first + dealt + after), "whatever your ballot said.");
            state.votes.Clear();
            deal.proposerId = state.playerId; deal.recipientId = leaving.id;
            Assert.That(Line(), Is.EqualTo(first + dealt + after));
            deal.status = DealStatus.Broken;
            Assert.That(Line(), Is.EqualTo(first + neutral + after), "A broken one is not.");
            deal.status = DealStatus.Active; deal.recipientId = other.id;
            Assert.That(Line(), Is.EqualTo(first + neutral + after), "Nor is a deal you made with somebody else.");
            state.deals.Clear();

            // Only a juror is going to the jury house.
            leaving.status = ContestantStatus.Evicted;
            Assert.That(Line(), Is.EqualTo(first + neutral), "Somebody out before the jury is not waiting in the jury house.");

            Assert.That(EpisodeDirector.GoodbyeLine(state, "nobody"), Is.Empty);
            Assert.That(EpisodeDirector.GoodbyeTone(state, "nobody"), Is.EqualTo(EpisodeDirector.GoodbyeKind.Neutral));
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
        /// No ballot carries its voter (UI-UX-PASS-PLAN B0): the card is handed whom each went
        /// against and nothing else - no id, no name, no look - in an order that is not the cast's,
        /// the same on every reload of the same week, with the deciding vote last.
        /// </summary>
        [Test]
        public void NoBallotCarriesItsVoterAndTheOrderIsNotTheCasts()
        {
            var state = new EpisodeState { playerId = "p", hohId = "h", seed = 1234u, week = 2 };
            foreach (var id in new[] { "p", "h", "a", "b", "c", "d", "e" })
                state.contestants.Add(new ContestantState { id = id, name = id.ToUpperInvariant(), status = ContestantStatus.Active });
            state.votes.Add(new VoteState { voterId = "p", targetId = "a" });
            state.votes.Add(new VoteState { voterId = "c", targetId = "b" });
            state.votes.Add(new VoteState { voterId = "d", targetId = "a" });
            state.votes.Add(new VoteState { voterId = "e", targetId = "b" });
            state.votes.Add(new VoteState { voterId = "h", targetId = "a", reason = "HoH tie-break" });
            var ballots = EpisodeDirector.EvictionBallots(state);
            Assert.That(typeof(VoteReveal.Ballot).GetFields().Select(field => field.Name), Is.EquivalentTo(new[] { "TargetId", "TieBreak" }),
                "A ballot has no voter, no name and no look to hand to a card.");
            Assert.That(ballots, Has.Count.EqualTo(5));
            Assert.That(ballots.Take(4).Select(ballot => ballot.TargetId).OrderBy(id => id), Is.EqualTo(new[] { "a", "a", "b", "b" }), "Every house ballot's target,");
            Assert.That(ballots.Take(4).Select(ballot => ballot.TieBreak), Is.All.False);
            Assert.That((ballots[4].TargetId, ballots[4].TieBreak), Is.EqualTo(("a", true)), "and the deciding vote last.");
            Assert.That(EpisodeDirector.EvictionBallots(state).Select(ballot => ballot.TargetId), Is.EqualTo(ballots.Select(ballot => ballot.TargetId)),
                "The same order on a reload of the same week.");

            // The engine casts in the cast's order; the card is handed another, or the climbing count would say whose vote each was.
            var castOrder = state.votes.Where(vote => vote.voterId != "h").Select(vote => vote.targetId).ToList();
            bool shuffled = false;
            for (int week = 1; week <= 12 && !shuffled; week++)
            {
                state.week = week;
                shuffled = !EpisodeDirector.EvictionBallots(state).Take(4).Select(ballot => ballot.TargetId).SequenceEqual(castOrder);
            }
            Assert.That(shuffled, Is.True, "Across twelve weeks the order leaves the cast's at least once.");
            var ids = new[] { "a", "b", "c", "d", "e" };
            Assert.That(Enumerable.Range(1, 20).Any(seed => !EpisodeDirector.VoteOrder(ids, (uint)seed, 3).SequenceEqual(EpisodeDirector.KeyOrder(ids, (uint)seed, 3))),
                Is.True, "and it is not the keys' order either.");
            Assert.That(EpisodeDirector.VoteOrder(ids, 7u, 3), Is.EquivalentTo(ids), "Everybody's ballot is on the board, once.");
            Assert.That(EpisodeDirector.EvictionBallots(null), Is.Empty);
        }

        /// <summary>
        /// The house's world routes only bodies the house keeps (PACK8-PASS-PLAN A1). A staged
        /// eviction's stage still held the evicted after their walk out had switched the body off,
        /// the coordinator failed the whole house on "An eligible NPC root is inactive.", and every
        /// ceremony after the first staged eviction played on the HUD. Both answers read one set of
        /// terms, so no combination of them can route a body that is off.
        /// </summary>
        [Test]
        public void TheHouseRoutesOnlyBodiesItKeeps()
        {
            var statuses = (ContestantStatus[])System.Enum.GetValues(typeof(ContestantStatus));
            var flags = new[] { false, true };
            foreach (var status in statuses)
                foreach (bool departing in flags)
                    foreach (bool walkingOut in flags)
                        foreach (bool bench in flags)
                            foreach (bool held in flags)
                                if (EpisodeDirector.RoutedByTheHouse(status, departing, walkingOut, bench, held))
                                    Assert.That(EpisodeDirector.KeepsBody(status, departing, walkingOut, bench), Is.True,
                                        status + (departing ? ", departing" : "") + (walkingOut ? ", walking out" : "")
                                        + (bench ? ", on the jury bench" : "") + (held ? ", held by a stage" : "")
                                        + ": routed by the house with its body switched off.");

            Assert.That(EpisodeDirector.RoutedByTheHouse(ContestantStatus.Jury, false, false, false, true), Is.False,
                "The walk out over, the stage still in its release lets the evicted go with their body.");
            Assert.That(EpisodeDirector.KeepsBody(ContestantStatus.Jury, true, false, false), Is.True,
                "The evicted are in the room while the card narrates them,");
            Assert.That(EpisodeDirector.RoutedByTheHouse(ContestantStatus.Jury, true, false, false, false), Is.False,
                "but an unstaged eviction still lets them go from the house's world at the commit,");
            Assert.That(EpisodeDirector.RoutedByTheHouse(ContestantStatus.Jury, true, false, false, true), Is.True,
                "a staged one takes them back for the hot seat,");
            Assert.That(EpisodeDirector.RoutedByTheHouse(ContestantStatus.Evicted, false, true, false, false), Is.True,
                "and the walk out routes them to the door.");
            Assert.That(EpisodeDirector.RoutedByTheHouse(ContestantStatus.Active, false, false, false, false), Is.True,
                "Everyone still playing is the house's.");
            Assert.That(EpisodeDirector.RoutedByTheHouse(ContestantStatus.Jury, false, false, true, false), Is.False,
                "The jury on finale night is placed, never routed.");
        }
    }
}
