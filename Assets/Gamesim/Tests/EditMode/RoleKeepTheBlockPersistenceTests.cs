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
    /// The Head of Household's keep-the-block card reloaded mid-meeting through the shipping save store (ROLE-MOMENTS-PLAN
    /// R4, §4.1): a season at Roles with the Head of Household's ask on the board - taken, or still waiting beside the
    /// nominees' - saved and loaded by <see cref="EpisodeSaveStore"/>, shows the same board, holds the player to the word they
    /// gave, and settles the decision - the kept or broken word, the waiting card's silence, a defied Head of Household -
    /// exactly as the season that never left memory does, in every commitment mode. Nothing new is stored: the ask is a deal
    /// row, its kind a value of a closed vocabulary. <see cref="RoleKeepTheBlockTests"/> plays it in memory; the subset leaves
    /// the store out, so this is EditMode only. Only a unique temporary slot is written.
    /// </summary>
    public sealed class RoleKeepTheBlockPersistenceTests
    {
        [Test]
        public void TheSaveStoreReloadsTheHeadOfHouseholdsCardAndTheDecisionSettlesAlike()
        {
            foreach (int mode in new[] { 0, 1, 2 })
            foreach (var (taken, use) in new[] { (true, false), (true, true), (false, true) })
            {
                string what = "mode " + mode + ", " + (taken ? "taken" : "waiting") + (use ? ", the veto used" : ", the veto unused");
                var engine = RoleKeepTheBlockTests.Board(RoleTone.Threatening, out string first, out _, out string hoh, mode);
                var at = engine.Snapshot;
                if (taken) at = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(at, RoleKeepTheBlockTests.HoldOf(at).id, true));
                var cards = Board(at);
                Assert.That(cards, Has.Count.EqualTo(taken ? 2 : 3), what + ": the nominees' cards, and the Head of Household's while it waits.");

                string folder = Path.Combine(Path.GetTempPath(), "GamesimRoleKeepTheBlock-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                try
                {
                    var store = new EpisodeSaveStore(Path.Combine(folder, "episode.json"));
                    store.Save(at);
                    Assert.That(new EpisodeSaveStore(store.SavePath).TryLoad(out var loaded, out string message), Is.True, message);
                    Assert.That(JsonConvert.SerializeObject(RoleKeepTheBlockTests.HoldOf(loaded)), Is.EqualTo(JsonConvert.SerializeObject(RoleKeepTheBlockTests.HoldOf(at))),
                        what + ": the row comes back as it was saved.");
                    Assert.That(Board(loaded), Is.EqualTo(cards), what + ": a reload shows the same board.");
                    Assert.That(Negotiation.VetoWordGiven(loaded), Is.EqualTo(taken), what + ": and the word given, from the deals saved.");

                    var reloaded = new EpisodeEngine(loaded);
                    var nominee = reloaded.Apply(RoleVetoBoardTests.Answer(loaded, RoleVetoBoardTests.AskOf(loaded, first).id, true));
                    Assert.That(nominee.accepted, Is.EqualTo(!taken), what + ": " + nominee.reason);
                    if (taken) Assert.That(nominee.reason, Is.EqualTo(Negotiation.VetoSavesOneRefusal), "A reload does not forget the word given.");
                    reloaded = new EpisodeEngine(loaded);

                    var decision = RoleVetoBoardTests.Decide(loaded, use, use ? first : null);
                    var kept = RoleVetoBoardTests.Apply(new EpisodeEngine(at), decision);
                    var settled = RoleVetoBoardTests.Apply(reloaded, decision);
                    var hold = RoleKeepTheBlockTests.HoldOf(settled);
                    Assert.That(hold.status, Is.EqualTo(taken ? (use ? DealStatus.Broken : DealStatus.Fulfilled) : DealStatus.Expired), what);
                    if (!taken)
                    {
                        Assert.That(RoleVetoBoardTests.Events(settled, settled.playerId, hoh, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                            Is.EqualTo(new[] { RoleTone.Stakes(RoleTone.Threatening).lapse }), what + ": the waiting card's silence is charged after a reload,");
                        Assert.That(Grudges.Severity(settled, hoh, settled.playerId), Is.EqualTo(RoleTone.ThreateningGrudge), what + ": and the defied Head of Household's grudge.");
                    }
                    Assert.That(JsonConvert.SerializeObject(settled), Is.EqualTo(JsonConvert.SerializeObject(kept)), what + ": exactly as the season that was never saved.");
                    Assert.That(EpisodeValidation.TryValidate(settled, out string error), Is.True, error);
                }
                finally
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        /// <summary>The board as text: each card's every word, in the board's order.</summary>
        private static List<string> Board(EpisodeState s) =>
            RoleOffers.VetoBoard(s).Select(card => string.Join(" | ", card.offerId, card.speakerId, card.role, card.chip, card.chipLine, card.tone, card.situation,
                card.read, card.quote, card.terms, card.stakes, card.answers, card.history, RoleOffers.Description(card))).ToList();
    }
}
