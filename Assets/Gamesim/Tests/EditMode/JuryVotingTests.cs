using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// How a juror decides who deserves to win.
    ///
    /// <para>The test that matters is <see cref="AJurorCanCrownSomebodyTheyDoNotLike"/>. Until this
    /// existed a juror voted on relationship and a roll, so the finalist who had been nicer won and
    /// how either of them played was worth nothing — which makes competitions, surviving the block
    /// and outmanoeuvring people all pointless the moment the finale starts.</para>
    /// </summary>
    public sealed class JuryVotingTests
    {
        // ---------------------------------------------------------------- respect

        [Test]
        public void ReachingTheEndIsWorthSomethingBeforeAnythingElse()
        {
            var nobody = Guest(hoh: 0, veto: 0, nominated: 0, strategic: 0);
            Assert.That(WebJuryVoting.GameplayRespect(nobody), Is.EqualTo(WebJuryVoting.MadeItToTheEnd));
            Assert.That(WebJuryVoting.GameplayRespect(null), Is.Zero);
        }

        [Test]
        public void EveryKindOfGameIsWorthTheReferencesNumber()
        {
            double baseline = WebJuryVoting.GameplayRespect(Guest(0, 0, 0, 0));
            Assert.That(WebJuryVoting.GameplayRespect(Guest(1, 0, 0, 0)) - baseline,
                Is.EqualTo(WebJuryVoting.PerHohWin));
            Assert.That(WebJuryVoting.GameplayRespect(Guest(0, 1, 0, 0)) - baseline,
                Is.EqualTo(WebJuryVoting.PerVetoWin));
            Assert.That(WebJuryVoting.GameplayRespect(Guest(0, 0, 1, 0)) - baseline,
                Is.EqualTo(WebJuryVoting.PerNomination));
            Assert.That(WebJuryVoting.GameplayRespect(Guest(0, 0, 0, 5)) - baseline, Is.EqualTo(5));
        }

        /// <summary>Running the house is worth more than saving yourself once.</summary>
        [Test]
        public void WinningTheHouseCountsForMoreThanWinningTheVeto()
        {
            Assert.That(WebJuryVoting.PerHohWin, Is.GreaterThan(WebJuryVoting.PerVetoWin));
            Assert.That(WebJuryVoting.GameplayRespect(Guest(2, 0, 0, 0)),
                Is.GreaterThan(WebJuryVoting.GameplayRespect(Guest(0, 2, 0, 0))));
        }

        /// <summary>
        /// Surviving the block is a game in itself. A finalist nominated four times and still
        /// standing played differently from one never nominated at all.
        /// </summary>
        [Test]
        public void SurvivingTheBlockIsRespectedRatherThanPitied()
        {
            Assert.That(WebJuryVoting.GameplayRespect(Guest(0, 0, 4, 0)),
                Is.GreaterThan(WebJuryVoting.GameplayRespect(Guest(0, 0, 0, 0))));
        }

        [Test]
        public void NoAmountOfWinningRunsAwayWithTheWholeThing()
        {
            Assert.That(WebJuryVoting.GameplayRespect(Guest(50, 50, 50, 10)),
                Is.EqualTo(WebJuryVoting.RespectCeiling));
        }

        // ---------------------------------------------------------------- the weights

        [Test]
        public void TheWeightsAreTheReferencesAndTheySumToOne()
        {
            Assert.That(WebJuryVoting.RelationshipWeight, Is.EqualTo(0.3));
            Assert.That(WebJuryVoting.RespectWeight, Is.EqualTo(0.4));
            Assert.That(WebJuryVoting.LoyaltyWeight, Is.EqualTo(0.15));
            Assert.That(WebJuryVoting.ObligationWeight, Is.EqualTo(0.15));
            Assert.That(WebJuryVoting.RelationshipWeight + WebJuryVoting.RespectWeight
                        + WebJuryVoting.LoyaltyWeight + WebJuryVoting.ObligationWeight,
                Is.EqualTo(1).Within(1e-9));
            Assert.That(WebJuryVoting.RespectWeight, Is.GreaterThan(WebJuryVoting.RelationshipWeight),
                "Respect outweighing affection is the whole design.");
        }

        // ---------------------------------------------------------------- the point of it

        /// <summary>
        /// The case the old rule could never produce: a juror who likes one finalist more and votes
        /// for the other because of how they played.
        /// </summary>
        [Test]
        public void AJurorCanCrownSomebodyTheyDoNotLike()
        {
            var state = Finale(out var juror, out var played, out var liked);

            // Liked by a mile, but did nothing all season.
            Set(state, juror, liked.id, 60);
            liked.hohWins = 0; liked.vetoWins = 0; liked.timesNominated = 0; liked.stats.strategic = 2;

            // Barely tolerated, and ran the house.
            Set(state, juror, played.id, 10);
            played.hohWins = 4; played.vetoWins = 3; played.timesNominated = 3; played.stats.strategic = 9;

            Assert.That(WebJuryVoting.Score(state, juror, played.id),
                Is.GreaterThan(WebJuryVoting.Score(state, juror, liked.id)),
                "A jury that could only reward kindness makes the whole game pointless.");
        }

        /// <summary>And the reverse still works: a bitter jury is a real outcome, not an impossible one.</summary>
        [Test]
        public void ALandslideOfAffectionStillBeatsAModestGame()
        {
            var state = Finale(out var juror, out var played, out var liked);
            Set(state, juror, liked.id, 100);
            Set(state, juror, played.id, -100);
            played.hohWins = 1; played.vetoWins = 1; played.stats.strategic = 5;
            liked.stats.strategic = 3;

            Assert.That(WebJuryVoting.Score(state, juror, liked.id),
                Is.GreaterThan(WebJuryVoting.Score(state, juror, played.id)));
        }

        // ---------------------------------------------------------------- what was agreed

        [Test]
        public void KeepingYourWordToAJurorIsWorthSomethingAtTheEnd()
        {
            var state = Finale(out var juror, out var kept, out var broke);
            state.deals.Add(new DealState
            {
                id = "kept", type = DealKind.FinalTwo, proposerId = kept.id, recipientId = juror,
                status = DealStatus.Fulfilled, week = 2, trustImpact = DealTrust.Critical,
            });
            state.deals.Add(new DealState
            {
                id = "broke", type = DealKind.FinalTwo, proposerId = broke.id, recipientId = juror,
                status = DealStatus.Broken, week = 2, trustImpact = DealTrust.Critical,
            });

            Assert.That(WebJuryVoting.Obligations(state, juror, kept.id), Is.GreaterThan(0));
            Assert.That(WebJuryVoting.Obligations(state, juror, broke.id), Is.LessThan(0));
            Assert.That(WebJuryVoting.Score(state, juror, kept.id),
                Is.GreaterThan(WebJuryVoting.Score(state, juror, broke.id)));
        }

        [Test]
        public void ObligationsAreBoundedSoTheyCannotSwampEverythingElse()
        {
            var state = Finale(out var juror, out var finalist, out _);
            for (int i = 0; i < 20; i++)
                state.deals.Add(new DealState
                {
                    id = "d" + i, type = DealKind.FinalTwo, proposerId = finalist.id, recipientId = juror,
                    status = DealStatus.Fulfilled, week = 2, trustImpact = DealTrust.Critical,
                });
            Assert.That(WebJuryVoting.Obligations(state, juror, finalist.id), Is.EqualTo(50));
        }

        [Test]
        public void SomebodyElsesDealIsNoneOfThisJurorsBusiness()
        {
            var state = Finale(out var juror, out var finalist, out var other);
            state.deals.Add(new DealState
            {
                id = "theirs", type = DealKind.FinalTwo, proposerId = finalist.id, recipientId = other.id,
                status = DealStatus.Fulfilled, week = 2, trustImpact = DealTrust.Critical,
            });
            Assert.That(WebJuryVoting.Obligations(state, juror, finalist.id), Is.Zero);
        }

        // ---------------------------------------------------------------- alliances

        [Test]
        public void AnAllianceYouAreStillInBeatsOneThatEndedWhichBeatsNoneAtAll()
        {
            var state = Finale(out var juror, out var together, out var parted);
            state.alliances.Add(new AllianceState
            {
                id = "live", name = "The Pact", active = true,
                members = new List<string> { juror, together.id },
            });
            state.alliances.Add(new AllianceState
            {
                id = "dead", name = "The Old Pact", active = false,
                members = new List<string> { juror, parted.id },
            });

            double live = WebJuryVoting.AllianceLoyalty(state, juror, together.id);
            double dead = WebJuryVoting.AllianceLoyalty(state, juror, parted.id);
            Assert.That(live, Is.GreaterThan(dead));
            Assert.That(dead, Is.GreaterThan(0),
                "Somebody who was with you and left is remembered differently from a stranger.");
        }

        // ---------------------------------------------------------------- what they say

        [Test]
        public void AJurorSaysWhyAndTheReasonMatchesWhatTheyDid()
        {
            var state = Finale(out var juror, out var played, out var liked);
            Set(state, juror, liked.id, 60);
            Set(state, juror, played.id, 10);
            played.hohWins = 4; played.stats.strategic = 9;

            string said = WebJuryVoting.Reason(state, juror, played, liked);
            Assert.That(said, Does.Contain("better game"));
            Assert.That(WebJuryVoting.Reason(state, juror, liked, played), Does.Contain("gut"));
        }

        // ---------------------------------------------------------------- through a season

        [Test]
        public void EveryJurorInAPlayedSeasonVotesOnceAndSaysWhy()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(7u));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }

            var finished = engine.Snapshot;
            Assert.That(finished.winnerId, Is.Not.Null.And.Not.Empty);
            Assert.That(finished.votes, Is.Not.Empty);
            CollectionAssert.AllItemsAreUnique(finished.votes.Select(v => v.voterId).ToList());
            Assert.That(finished.votes, Has.All.Matches<VoteState>(v => !string.IsNullOrWhiteSpace(v.reason)));
            Assert.That(EpisodeValidation.TryValidate(finished, out string error), Is.True, error);
        }

        [Test]
        public void TheSameSeasonCrownsTheSameWinner()
        {
            Assert.That(Play(11u).winnerId, Is.EqualTo(Play(11u).winnerId));
        }

        // ---------------------------------------------------------------- fixtures

        private static ContestantState Guest(int hoh, int veto, int nominated, double strategic)
        {
            var guest = new ContestantState { id = "g", name = "Guest" };
            guest.hohWins = hoh; guest.vetoWins = veto; guest.timesNominated = nominated;
            guest.stats.strategic = strategic;
            return guest;
        }

        /// <summary>A season reduced to one juror and two finalists.</summary>
        private static EpisodeState Finale(out string juror, out ContestantState first, out ContestantState second)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            first = cast[0];
            second = cast[1];
            juror = cast[2].id;
            return state;
        }

        private static void Set(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r => r.fromId == from && r.toId == to))
                edge.score = score;
        }

        private static EpisodeState Play(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
                engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
            return engine.Snapshot;
        }
    }
}
