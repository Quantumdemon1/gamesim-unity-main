using System;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The veto board reloaded mid-meeting through the shipping save store (ROLE-MOMENTS-PLAN R3, §4.1): a season at Roles
    /// with one card answered and one still waiting, saved and loaded by <see cref="EpisodeSaveStore"/>, shows the same
    /// board, holds the player to the word they gave, and settles the decision - the waiting card's silence, the riders -
    /// exactly as the season that never left memory does. Nothing about the board is stored: its tones are read again.
    /// <see cref="RoleVetoBoardTests"/> plays the board in memory; the subset leaves the store out, so this is EditMode
    /// only. Only a unique temporary slot is written.
    /// </summary>
    public sealed class RoleVetoBoardPersistenceTests
    {
        [Test]
        public void TheSaveStoreReloadsTheBoardMidMeetingAndTheDecisionSettlesAlike()
        {
            foreach (bool yes in new[] { true, false })
            {
                var engine = RoleVetoBoardTests.Board(RoleTone.Threatening, out string first, out string second);
                var answered = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(engine.Snapshot, RoleVetoBoardTests.AskOf(engine.Snapshot, second).id, yes));
                var cards = Board(answered);
                Assert.That(cards, Has.Count.EqualTo(1), "One card answered, one waiting.");

                string folder = Path.Combine(Path.GetTempPath(), "GamesimRoleVetoBoard-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                try
                {
                    var store = new EpisodeSaveStore(Path.Combine(folder, "episode.json"));
                    store.Save(answered);
                    Assert.That(new EpisodeSaveStore(store.SavePath).TryLoad(out var loaded, out string message), Is.True, message);
                    Assert.That(Board(loaded), Is.EqualTo(cards), "yes " + yes + ": a reload shows the same board.");
                    Assert.That(Negotiation.VetoWordGiven(loaded), Is.EqualTo(yes), "yes " + yes + ": and the word given, from the deals saved.");

                    var reloaded = new EpisodeEngine(loaded);
                    var waiting = RoleVetoBoardTests.AskOf(loaded, first).id;
                    var again = reloaded.Apply(RoleVetoBoardTests.Answer(loaded, waiting, true));
                    Assert.That(again.accepted, Is.EqualTo(!yes), "yes " + yes + ": " + again.reason);
                    if (yes) Assert.That(again.reason, Is.EqualTo(Negotiation.VetoSavesOneRefusal), "A reload does not forget the word given.");
                    else reloaded = new EpisodeEngine(loaded);

                    var decision = RoleVetoBoardTests.Decide(loaded, false, null);
                    var kept = RoleVetoBoardTests.Apply(new EpisodeEngine(answered), decision);
                    var settled = RoleVetoBoardTests.Apply(reloaded, decision);
                    Assert.That(RoleVetoBoardTests.Events(settled, settled.playerId, first, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                        Is.EqualTo(new[] { RoleTone.Stakes(RoleTone.Threatening).lapse }), "yes " + yes + ": the waiting card's silence is charged after a reload,");
                    Assert.That(Grudges.Severity(settled, first, settled.playerId), Is.EqualTo(RoleTone.ThreateningGrudge), "yes " + yes + ": and its rider settles,");
                    Assert.That(JsonConvert.SerializeObject(settled), Is.EqualTo(JsonConvert.SerializeObject(kept)),
                        "yes " + yes + ": exactly as the season that was never saved.");
                }
                finally
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        /// <summary>The board as text: each card's every word, in the board's order.</summary>
        private static System.Collections.Generic.List<string> Board(EpisodeState s) =>
            RoleOffers.VetoBoard(s).Select(card => string.Join(" | ", card.offerId, card.speakerId, card.tone, card.situation, card.read, card.quote,
                card.terms, card.stakes, card.answers, card.history, RoleOffers.Description(card))).ToList();
    }
}
