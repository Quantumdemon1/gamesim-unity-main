using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Head of Household's pitch board reloaded mid-board through the shipping save store (ROLE-MOMENTS-PLAN R5, §4.1): a
    /// season at Roles with one pitch felt out and another answered out of order, saved and loaded by
    /// <see cref="EpisodeSaveStore"/>, shows the same board in the same order, still takes any card first, refuses a second
    /// question to the card already felt out, and settles the nominations - the promise kept, the honest no - exactly as the
    /// season that never left memory does. Nothing about the board is stored beyond the reply cards and rows it always had.
    /// <see cref="RolePitchBoardTests"/> plays the board in memory; the subset leaves the store out, so this is EditMode only.
    /// Only a unique temporary slot is written.
    /// </summary>
    public sealed class RolePitchBoardPersistenceTests
    {
        [Test]
        public void TheSaveStoreReloadsTheBoardMidBoardAndTheNominationsSettleAlike()
        {
            var engine = RolePitchBoardTests.Dealt(RolePitchBoardTests.Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Earnest, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            s = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Answer(s, CardOf(s, board[1]).id, HoHPitches.FeelOutKey));
            s = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Answer(s, CardOf(s, board[2]).id, HoHPitches.PromiseKey));
            var cards = Board(s);
            Assert.That(cards, Has.Count.EqualTo(2), "One answered, one felt out and waiting, one waiting.");

            string folder = Path.Combine(Path.GetTempPath(), "GamesimRolePitchBoard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new EpisodeSaveStore(Path.Combine(folder, "episode.json"));
                store.Save(s);
                Assert.That(new EpisodeSaveStore(store.SavePath).TryLoad(out var loaded, out string message), Is.True, message);
                Assert.That(Board(loaded), Is.EqualTo(cards), "A reload shows the same board, in its order, felt out where it was.");
                Assert.That(EpisodeEngine.RolesOn(loaded) && HoHPitches.OnTheBoard(loaded), Is.True);

                var reloaded = new EpisodeEngine(loaded);
                var again = reloaded.Apply(RolePitchBoardTests.Answer(loaded, CardOf(loaded, board[1]).id, HoHPitches.FeelOutKey));
                Assert.That((again.accepted, again.reason), Is.EqualTo((false, "You already felt out this pitch.")), "A reload does not forget the question asked.");

                var memory = new EpisodeEngine(s);
                EpisodeState Play(EpisodeEngine on, EpisodeState from)
                {
                    // The second card first, then the first: any order, after the reload as before it.
                    var next = RolePitchBoardTests.Apply(on, RolePitchBoardTests.Answer(from, CardOf(from, board[1]).id, HoHPitches.TurnDownKey));
                    next = RolePitchBoardTests.Apply(on, RolePitchBoardTests.Answer(next, CardOf(next, board[0]).id, HoHPitches.HearKey));
                    string other = next.Active.First(c => !c.isPlayer && !board.Contains(c.id)).id;
                    return RolePitchBoardTests.Apply(on, RolePitchBoardTests.Command(next, EpisodeCommandKind.Nominate, board[1], other));
                }
                var kept = Play(memory, s);
                var settled = Play(reloaded, loaded);
                Assert.That(settled.relationships.Where(r => r.fromId == settled.playerId && r.toId == board[2]).SelectMany(r => r.events)
                    .Count(e => e.week == settled.week && e.description == "Kept me off the block, as promised."), Is.EqualTo(1),
                    "The promise made before the reload pays at the nominations after it,");
                Assert.That(Grudges.Severity(settled, board[1], settled.playerId), Is.EqualTo(70 - HoHPitches.HonestNoEase),
                    "and the honest no eases the nomination,");
                Assert.That(JsonConvert.SerializeObject(settled), Is.EqualTo(JsonConvert.SerializeObject(kept)),
                    "exactly as the season that was never saved.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static ReplyCardState CardOf(EpisodeState s, string fromId) => s.replyCards.Single(c => c.kind == ReplyCards.Pitch && c.fromId == fromId);

        /// <summary>The board as text: each card's every word, in the board's order.</summary>
        private static List<string> Board(EpisodeState s) =>
            RoleOffers.PitchBoard(s).Select(card => string.Join(" | ", card.offerId, card.speakerId, card.tone, card.situation, card.read, card.quote,
                card.recommends, card.stakes, card.answers, card.history, card.assessment, RoleOffers.Description(card))).ToList();
    }
}
