using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The veto's draw as the selection screen and its reveal read it (PACK8-PASS-PLAN B2): the board
    /// is the draw the engine makes - the same seats, the same pool, as many chips as it draws, and
    /// none at all when everyone plays - and the order the drawn are revealed in is seeded and is not
    /// the house's. The screen showed three chips and "3 drawn from the house" in a house of six,
    /// where the engine seats everyone and never touches the generator.
    /// </summary>
    public sealed class VetoDrawTests
    {
        /// <summary>A house of <paramref name="house"/> at the draw: the first houseguest at the head, the next two on the block.</summary>
        private static EpisodeState AtSelection(int house, uint seed)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = house }, seed);
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.VetoSelection;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            return s;
        }

        /// <summary>The selection's way on: the Advance that makes the draw.</summary>
        private static EpisodeState Draw(EpisodeState s)
        {
            var result = new EpisodeEngine(s).Apply(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = s.playerId, expectedPhase = s.phase, expectedRevision = s.revision,
                kind = EpisodeCommandKind.Advance,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.Veto));
            return result.state;
        }

        [Test]
        public void AHouseOfEightShowsThreeChipsOverAPoolOfFive()
        {
            var s = AtSelection(8, 71u);
            var board = VetoDraw.Before(s);
            Assert.That(board.EveryonePlays, Is.False);
            Assert.That(board.Seats, Is.EqualTo(6));
            Assert.That(board.ByRight, Is.EqualTo(new[] { s.hohId, s.nominees[0], s.nominees[1] }), "The Head of Household first, then the block.");
            Assert.That(board.Eligible, Is.EqualTo(s.Active.Select(c => c.id).Where(id => !board.ByRight.Contains(id)).ToList()),
                "The rest of the house is the pool, in house order.");
            Assert.That(board.ToDraw, Is.EqualTo(3));

            Assert.That(VetoDraw.Line(board), Is.EqualTo("6 players compete: HoH, both nominees, and 3 players drawn at random."));
            Assert.That(VetoDraw.PlayingHeading(board), Is.EqualTo("AUTOMATICALLY PLAYING"));
            Assert.That(VetoDraw.DrawHeading(board), Is.EqualTo("DRAW 3 PLAYERS"));
            Assert.That(VetoDraw.ChipsLine(board), Is.EqualTo("3 chips to draw"));
            Assert.That(VetoDraw.EligibleHeading, Is.EqualTo("ELIGIBLE FOR DRAW"));
            Assert.That(VetoDraw.Headline(board), Is.EqualTo("REVEAL THE DRAW"));
            Assert.That(VetoDraw.Footnote(board), Is.EqualTo("Draw 3 players from the eligible pool to complete the Veto competition."));

            Assert.That(VetoDraw.Drawn(s), Is.Empty, "Nothing is drawn before the draw.");
            var after = Draw(s);
            var drawn = VetoDraw.Drawn(after);
            Assert.That(drawn, Has.Count.EqualTo(3), "The three chips are the three the engine draws,");
            Assert.That(drawn.All(board.Eligible.Contains), Is.True, "every one of them from the pool,");
            Assert.That(board.ByRight.Concat(drawn).OrderBy(id => id), Is.EqualTo(after.vetoPlayers.OrderBy(id => id)),
                "and with the three by right they are the committed field.");
        }

        [Test]
        public void SixOrFewerIsEveryonePlaysWithNothingDrawn()
        {
            foreach (int house in new[] { 4, 5, 6 })
            {
                var s = AtSelection(house, 72u);
                var board = VetoDraw.Before(s);
                Assert.That(board.EveryonePlays, Is.True, "A house of " + house + " seats everyone.");
                Assert.That(board.ToDraw, Is.Zero, "No chip is shown for a draw the engine never makes.");
                Assert.That(board.Eligible, Is.Empty, "and there is no pool to draw from.");
                Assert.That(board.ByRight.Take(3), Is.EqualTo(new[] { s.hohId, s.nominees[0], s.nominees[1] }),
                    "The Head of Household and the block still lead the row,");
                Assert.That(board.ByRight.OrderBy(id => id), Is.EqualTo(s.Active.Select(c => c.id).OrderBy(id => id)), "with the rest of the house after them.");
                Assert.That(VetoDraw.Line(board), Is.EqualTo(VetoDraw.EveryonePlaysLine));
                Assert.That(VetoDraw.EveryonePlaysLine, Is.EqualTo("Everyone still in the house plays for the veto."));
                Assert.That(VetoDraw.DrawHeading(board), Is.EqualTo("NO DRAW"));
                Assert.That(VetoDraw.Headline(board), Is.EqualTo("EVERYONE PLAYS"));
                Assert.That(VetoDraw.Footnote(board), Is.EqualTo("Up next: the Power of Veto competition."));

                var after = Draw(s);
                Assert.That(after.randomState, Is.EqualTo(s.randomState), "The engine draws nothing from the generator,");
                Assert.That(VetoDraw.Drawn(after), Is.Empty, "so the reveal has nobody to turn over.");
            }
        }

        [Test]
        public void TheBoardIsTheDrawTheEngineMakesInEveryHouse()
        {
            for (int house = 4; house <= 12; house++)
                for (uint seed = 1; seed <= 5; seed++)
                {
                    var s = AtSelection(house, 700u + seed);
                    var board = VetoDraw.Before(s);
                    var after = Draw(s);
                    var drawn = VetoDraw.Drawn(after);
                    string where = "A house of " + house + " (seed " + seed + ")";
                    Assert.That(board.EveryonePlays, Is.EqualTo(house <= EpisodeEngine.VetoLineupSize), where);
                    Assert.That(board.Seats, Is.EqualTo(after.vetoPlayers.Count), where + ": the seats are the field's size.");
                    Assert.That(drawn, Has.Count.EqualTo(board.ToDraw), where + ": a chip for each houseguest drawn.");
                    Assert.That(drawn.All(board.Eligible.Contains), Is.True, where + ": drawn from the pool.");
                    Assert.That(board.ByRight.Concat(drawn).OrderBy(id => id), Is.EqualTo(after.vetoPlayers.OrderBy(id => id)), where);
                    Assert.That(board.ToDraw > 0 || after.randomState == s.randomState, Is.True, where + ": no chips, no draw.");
                }
        }

        [Test]
        public void TheRevealOrderIsSeededAndIsNotTheHouses()
        {
            var ids = new[] { "alpha", "bravo", "charlie", "delta" };
            var order = VetoDraw.RevealOrder(ids, 9001u, 3);
            Assert.That(order.OrderBy(id => id), Is.EqualTo(ids.OrderBy(id => id)), "Every drawn houseguest is revealed once.");
            Assert.That(VetoDraw.RevealOrder(Enumerable.Reverse(ids), 9001u, 3), Is.EqualTo(order), "The order is the draw's, whatever order the lineup is held in,");
            Assert.That(VetoDraw.RevealOrder(ids, 9001u, 3), Is.EqualTo(order), "and the same on every reload.");
            bool reordered = false;
            for (uint seed = 1; seed <= 20 && !reordered; seed++)
                reordered = !VetoDraw.RevealOrder(ids, seed, 1).SequenceEqual(ids);
            Assert.That(reordered, Is.True, "House order would put whoever is first in the cast first every week they are drawn.");
            bool weekly = false;
            for (int week = 2; week <= 10 && !weekly; week++)
                weekly = !VetoDraw.RevealOrder(ids, 9001u, week).SequenceEqual(VetoDraw.RevealOrder(ids, 9001u, 1));
            Assert.That(weekly, Is.True, "A new week is a new shuffle.");
        }
    }
}
