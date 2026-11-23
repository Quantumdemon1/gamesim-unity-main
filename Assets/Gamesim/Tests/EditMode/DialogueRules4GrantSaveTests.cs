using System;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4's nominee grant through the real save store (DIALOGUE-PLAN D5a): a season saved mid-week with the
    /// grant unused loads whole through EpisodeSaveStore, still holds the grant, and the grant pays for the conversation as
    /// in the season never saved. EditMode only (the store is the runtime's); the Mono harness runs it.
    /// </summary>
    public sealed class DialogueRules4GrantSaveTests
    {
        private string directory;

        [SetUp]
        public void MakeAPrivateFolder()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimDialogueRules4Grant-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveThePrivateFolder()
        {
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ASeasonSavedWithAnUnusedGrantLoadsWithItAndTheGrantPays()
        {
            EpisodeState meeting = null;
            for (uint seed = 1; seed <= 60 && meeting == null; seed++)
            {
                var engine = new EpisodeEngine(DialogueRules4Season.Start(seed));
                for (int step = 0; step < 600 && meeting == null; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 5 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.nominees.Contains(s.playerId)
                        && s.vetoHolderId != null && s.vetoHolderId != s.playerId && !s.nominees.Contains(s.vetoHolderId))
                    {
                        DialogueRules2Season.CloseAsks(engine);
                        if (DialogueRules.OnTheHouseWith(engine.Snapshot, engine.Snapshot.vetoHolderId)) { meeting = engine.Snapshot; break; }
                    }
                    var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                    if (next.kind == EpisodeCommandKind.Compete) next.performance = 0;
                    DialogueRules2Season.Accepted(engine, next);
                }
            }
            Assert.That(meeting, Is.Not.Null, "A rules-4 veto meeting with the player's conversation on the house.");
            string grantId = DialogueRules.GrantId(meeting.week, DialogueRules.NomineeRole);

            var store = new EpisodeSaveStore(Path.Combine(directory, "season.json"));
            store.Save(meeting);
            Assert.That(store.TryLoad(out var loaded, out string error), Is.True, error);
            Assert.That(EpisodeValidation.TryValidate(loaded, out error), Is.True, error);
            Assert.That(DialogueRules2Season.Json(loaded), Is.EqualTo(DialogueRules2Season.Json(meeting)), "The load is the season saved.");
            Assert.That(loaded.dialogue.grants.Single(g => g.id == grantId).status, Is.EqualTo(DialogueRules.GrantOpen), "The grant survives the reload.");

            string holder = loaded.vetoHolderId;
            var afterLoad = DialogueRules2Season.Accepted(new EpisodeEngine(loaded), DialogueRules3Season.Entry(loaded, holder, DialogueRules3Season.Holder));
            var live = DialogueRules2Season.Accepted(new EpisodeEngine(meeting.Clone()), DialogueRules3Season.Entry(meeting, holder, DialogueRules3Season.Holder));
            Assert.That(DialogueRules.Active(afterLoad).entry.grantId, Is.EqualTo(grantId), "The grant paid after the reload.");
            Assert.That(DialogueRules2Season.Json(afterLoad), Is.EqualTo(DialogueRules2Season.Json(live)));
        }
    }
}
