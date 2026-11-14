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
    /// The role moments' abuse checklist, item (6), a load mid-moment (ROLE-MOMENTS-PLAN R7 (5); the R7 build plan's C2): the pawn
    /// talk's card - in every commitment mode - and quiet tears' card at the block, each waiting on the player under the role
    /// moments, saved and loaded through the shipping save store (<see cref="EpisodeSaveStore"/>), come back as they were saved,
    /// and the same answer on the season loaded and on the season that never left memory settles them exactly alike: taking the
    /// pawn's word (the two promises and the +3, through whichever store holds a promise), and sitting with them a while (the
    /// seat and the favour). Nothing about either card is stored beyond the house event and storyline it always was.
    /// <see cref="RoleAbuseTests"/> plays the rest of the checklist in memory; the subset leaves the store out, so this is
    /// EditMode only. Only a unique temporary slot is written.
    /// </summary>
    public sealed class RoleAbusePersistenceTests
    {
        [Test]
        public void TheSaveStoreReloadsAPawnCardAndATearsCardAndTheSameAnswerSettlesAlike()
        {
            foreach (int mode in new[] { 0, 1, 2 })
            {
                var pawn = RoleStoryMomentsTests.PawnWeek(backdoor: false, coin: true, mode: mode);
                var at = pawn.after.Clone();
                Reload(at, RoleStoryMomentsTests.JustAPawn(at).id, "take-word-roles", "mode " + mode + ", the pawn's card");
            }
            var tears = RoleStoryMomentsTests.TearsCard(playerHoh: false, StoryAnchors.BlockSet, out var drawn);
            Assert.That(tears, Is.Not.Null, "No block brought quiet tears.");
            var waiting = drawn.Clone();
            Reload(waiting, RoleStoryMomentsTests.OpenTears(waiting).id, "sit-a-while", "quiet tears at the block");
        }

        private static void Reload(EpisodeState at, string eventId, string optionId, string what)
        {
            Assert.That(EpisodeEngine.RolesOn(at), Is.True, what + ": under the role moments.");
            Assert.That(at.houseEvents.Single(e => e.id == eventId).resolved, Is.False, what + ": waiting on the player.");
            string folder = Path.Combine(Path.GetTempPath(), "GamesimRoleAbuse-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new EpisodeSaveStore(Path.Combine(folder, "episode.json"));
                store.Save(at);
                Assert.That(new EpisodeSaveStore(store.SavePath).TryLoad(out var loaded, out string message), Is.True, what + ": " + message);
                Assert.That(JsonConvert.SerializeObject(loaded.houseEvents.Single(e => e.id == eventId)),
                    Is.EqualTo(JsonConvert.SerializeObject(at.houseEvents.Single(e => e.id == eventId))), what + ": the card comes back as it was saved.");
                Assert.That(EpisodeEngine.RolesOn(loaded), Is.True, what);

                var command = RoleAbuseTests.Progress(at, eventId, optionId);
                var kept = RoleAbuseTests.Apply(new EpisodeEngine(at), command);
                var settled = RoleAbuseTests.Apply(new EpisodeEngine(loaded), command);
                Assert.That(settled.houseEvents.Single(e => e.id == eventId).resolved, Is.True, what + ": answered after the reload.");
                Assert.That(JsonConvert.SerializeObject(settled), Is.EqualTo(JsonConvert.SerializeObject(kept)),
                    what + ": the same answer settles it exactly as on the season that was never saved.");
                Assert.That(EpisodeValidation.TryValidate(settled, out string error), Is.True, what + ": " + error);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
