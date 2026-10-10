using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Vote family V6: the unified vote rules (mode 2) at the director's durable boundary. A season the director starts is
    /// mode 2 before its first save, and a reload keeps it; a vote promise the player makes is a canonical row, saved before
    /// it is installed, reloaded exactly, and its repeat refused without a save; and the reveal - the command that settles
    /// the week's Vote rows and publishes their archive frame - is saved before it is installed: a failed save leaves the
    /// season and both slot files as they were, and the retry commits exactly the prepared reveal, once. Every fixture is the
    /// public engine's own season from <see cref="ShippedRules.ApplyFresh"/>, walked by public commands and loaded as a player
    /// loads a save; nothing is manufactured.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator UnifiedVote_FreshSeasonsStartInModeTwoBeforeTheFirstSaveAndReloadKeepsIt()
        {
            Assert.That(director.Snapshot.unifiedCommitmentRulesVersion, Is.Zero, "The scene's fallback season is not upgraded.");
            director.SuspendNpcAutonomyForDiagnostics();
            director.StartSeason(null);
            // Before any frame can run: the snapshot and the first write already carry mode 2.
            UvAssertMode(director.Snapshot);
            UvAssertDisk(director.Snapshot);
            yield return SettleCast();
            director.ClosePanels();
            var quick = director.Snapshot;
            UvAssertMode(quick);
            director.SaveNow();
            director.LoadNow();
            UvEquivalent(quick, director.Snapshot);

            director.SuspendNpcAutonomyForDiagnostics();
            director.StartSeason(new SeasonBuilder.Choice { HouseSize = 8 });
            UvAssertMode(director.Snapshot);
            UvAssertDisk(director.Snapshot);
            yield return SettleCast();
            director.ClosePanels();
            var cast = director.Snapshot;
            director.SaveNow();
            director.LoadNow();
            UvEquivalent(cast, director.Snapshot);
            UvAssertMode(director.Snapshot);
        }

        [UnityTest]
        public IEnumerator UnifiedVote_APublicVotePromiseIsACanonicalRowSavedReloadedAndItsRepeatRefusedWithoutASave()
        {
            yield return UvInstall(s => s.phase == EpisodePhase.Campaign && s.pendingDiary == null && UvPlayerVotes(s)
                && EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s), "a campaign the player votes in");
            var before = director.Snapshot;
            var command = UvPromise(before);
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            var result = director.Submit(command);
            Assert.That(result.accepted, Is.True, result.reason);
            UvEquivalent(expected.state, director.Snapshot);
            var row = director.Snapshot.unifiedCommitments.Single(r => !before.unifiedCommitments.Any(old => old.id == r.id));
            Assert.That((row.kind, row.sourcePolicy, row.origin, row.makerId, row.beneficiaryId, row.targetId),
                Is.EqualTo((UnifiedVoteTogether.Vote, UnifiedCommitments.PromisePolicy, UnifiedCommitments.PlayerPromise,
                    before.playerId, command.targetId, command.secondTargetId)), "The word is one canonical Vote row.");
            Assert.That(director.Snapshot.promises.Any(p => p.kind == PromiseKind.Vote), Is.False, "No raw mirror.");
            UvAssertDisk(expected.state);
            director.LoadNow();
            UvEquivalent(expected.state, director.Snapshot);

            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(_ => writes++));
            try
            {
                var duplicate = director.Submit(command);
                Assert.That((duplicate.accepted, duplicate.duplicate), Is.EqualTo((false, true)), "The saved receipt survives the reload.");
                var repeat = UvPromise(director.Snapshot);
                Assert.That(director.Submit(repeat).accepted, Is.False, "A new id cannot give the same active word twice.");
                Assert.That(writes, Is.Zero, "Refused commands never reach the disk.");
                UvEquivalent(expected.state, director.Snapshot);
                UvAssertDisk(expected.state);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator UnifiedVote_ARevealsFailedSavePreservesTheSeasonAndBothCopiesAndTheRetryCommitsTheSameReveal()
        {
            yield return UvInstall(s => s.phase == EpisodePhase.Eviction && !s.evictionResolved && s.pendingDiary == null
                && s.evictionStage == EvictionStage.Voting && UvPlayerVotes(s) && s.votes.Any(v => v.voterId == s.playerId)
                && s.week > 1, "an open vote past the first week, the player's ballot cast");
            var before = director.Snapshot;
            var reveal = UvCommand(before, EpisodeCommandKind.Advance);
            var expected = new EpisodeEngine(before).Apply(reveal);
            Assert.That(expected.accepted && expected.state.evictionResolved, Is.True, expected.reason);
            Assert.That(expected.state.unifiedVoteReveals.Count, Is.EqualTo(before.unifiedVoteReveals.Count + 1), "Fixture: the reveal publishes a frame.");
            director.SaveNow();
            director.SaveNow();
            var retained = UvImage();
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                throw new IOException("Injected mode-2 reveal pre-write failure.");
            }));
            try
            {
                Assert.That(director.Submit(reveal).accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                UvEquivalent(expected.state, attempted);
                UvEquivalent(before, director.Snapshot);
                UvAssertImage(retained);
                UvAssertDisk(before);
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.StatusMessage, Does.Contain("not committed"));
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow();
            UvEquivalent(before, director.Snapshot);
            var retry = director.Submit(reveal);
            Assert.That(retry.accepted, Is.True, retry.reason);
            UvEquivalent(expected.state, director.Snapshot);
            UvAssertDisk(expected.state);
            Assert.That(director.Snapshot.acceptedCommandIds.Count(id => id == reveal.id), Is.EqualTo(1), "Committed once.");
            director.LoadNow();
            UvEquivalent(expected.state, director.Snapshot);
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        /// A fresh season as the director starts one (the quick start's factory and <see cref="ShippedRules.ApplyFresh"/>: mode 2),
        /// walked by public commands to the first state <paramref name="at"/> takes, saved to this test's slot and loaded as a
        /// player loads it.
        /// </summary>
        private IEnumerator UvInstall(Func<EpisodeState, bool> at, string what)
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 40 && fixture == null; seed++)
            {
                var start = ContentCatalog.Create(seed);
                ShippedRules.ApplyFresh(start);
                UvAssertMode(start);
                var engine = new EpisodeEngine(start);
                for (int guard = 0; guard < 600; guard++)
                {
                    var current = engine.Snapshot;
                    if (at(current)) { fixture = current; break; }
                    if (current.phase == EpisodePhase.Finished || current.Find(current.playerId).status != ContestantStatus.Active) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded public fixture: " + what);
            UvAssertMode(fixture);
            director.FreezeForReloadForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels();
            UvEquivalent(fixture, director.Snapshot);
            UvAssertDisk(fixture);
        }

        private static bool UvPlayerVotes(EpisodeState s) => EpisodeEngine.Voters(s).Any(c => c.isPlayer) && s.nominees.Count == 2;

        private static EpisodeCommand UvCommand(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "unified-vote-" + kind + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        /// <summary>The player's word on the vote to the first voter who is not on the block: to evict the nominee who is not them.</summary>
        private static EpisodeCommand UvPromise(EpisodeState s)
        {
            var command = UvCommand(s, EpisodeCommandKind.PromiseVote);
            command.targetId = EpisodeEngine.Voters(s).First(c => !c.isPlayer).id;
            command.secondTargetId = s.nominees.First(id => id != command.targetId);
            return command;
        }

        private static void UvAssertMode(EpisodeState s)
        {
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version), "Mode 2, the unified vote rules (V6).");
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(1));
            Assert.That(s.dealRulesStartWeek, Is.EqualTo(1));
            Assert.That(YourWord.On(s), Is.True);
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Vote || p.kind == PromiseKind.Safety), Is.False, "No raw Vote or Safety word.");
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement || d.type == DealKind.VoteTogether
                || d.type == DealKind.VoteSave || d.type == DealKind.VoteEvict), Is.False, "No raw Vote or Safety deal.");
            EpisodeSaveValidation.Validate(s);
        }

        private static void UvEquivalent(EpisodeState expected, EpisodeState actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.randomState, Is.EqualTo(expected.randomState));
            Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)),
                "The complete durable graph - canonical rows, archive frames, receipts - remains exact.");
        }

        private void UvAssertDisk(EpisodeState expected)
        {
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string message), Is.True, message);
            UvEquivalent(expected, disk);
        }

        private SortedDictionary<string, byte[]> UvImage()
        {
            string root = Path.GetFullPath(temporaryDirectory);
            Assert.That(Path.GetFileName(root), Does.StartWith("GamesimEpisodeTests-"));
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            return new SortedDictionary<string, byte[]>(Directory.GetFiles(root)
                .ToDictionary(Path.GetFileName, File.ReadAllBytes), StringComparer.Ordinal);
        }

        private void UvAssertImage(SortedDictionary<string, byte[]> expected)
        {
            var actual = UvImage();
            Assert.That(actual.Keys, Is.EqualTo(expected.Keys));
            foreach (var file in expected) CollectionAssert.AreEqual(file.Value, actual[file.Key], file.Key);
        }
    }
}
