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
    /// Native-only actual SaveStore coverage of the two real week-three incoming card families.
    /// The fixed seed4/8/policy0 route was observed by discovery02; this fixture does not search,
    /// manufacture a card/producer, inject gameplay state or clear the incoming queue.
    /// Direct public card-ID selection is engine coverage, not proof of visible UI queue order.
    /// </summary>
    public sealed class DialogueCampaignRealReplySaveTests
    {
        private const string Jamie = "jamie-roberts", Alex = "alex-chen", Casey = "casey-wilson";
        private const string ConfrontCard = "reply-321", GossipCard = "reply-310";
        private const string DirectoryPrefix = "GamesimDialogueRealReply-";
        private EpisodeState preEntry, paused;
        private string preEntryBytes, pausedBytes, directory;
        private int nonce;

        [OneTimeSetUp]
        public void PlayTheFixedObservedWeekThreeRouteWithoutSearchingOrForcingState()
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "This fixture must not enable shipping defaults.");
            fresh.dialogueRulesVersion = 1; // The sole unpublished fresh-fixture rule opt-in.
            Assert.That(fresh.unifiedCommitmentRulesVersion, Is.EqualTo(2));
            Assert.That(fresh.allWeekRulesStartWeek, Is.EqualTo(1));
            Valid(fresh);
            string freshBytes = Json(fresh);
            var engine = new EpisodeEngine(fresh);
            var navigation = new int[3];
            for (int step = 0; step < 3 * 128; step++)
            {
                var s = engine.Snapshot;
                Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active));
                Assert.That(s.week, Is.InRange(1, 3));
                Assert.That(s.phase, Is.Not.EqualTo(EpisodePhase.Finished));
                if (s.week == 3 && s.phase == EpisodePhase.Campaign) break;
                Assert.That(navigation[s.week - 1], Is.LessThan(128), "Fixed route exceeded its per-week public command bound.");
                // The successful route made no earlier campaign fork or neutral answer.
                if (s.week == 2 && s.phase == EpisodePhase.Campaign)
                    Assert.That(s.nominees.Contains(s.playerId), Is.False);
                navigation[s.week - 1]++;
                Accepted(engine, NextPolicyZero(s));
            }
            var campaign = engine.Snapshot;
            Assert.That(navigation, Is.EqualTo(new[] { 16, 16, 9 }));
            Assert.That(campaign.week, Is.EqualTo(3));
            Assert.That(campaign.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(campaign.nominees, Does.Contain(campaign.playerId));
            Assert.That(campaign.npcSocial.beatWeek, Is.EqualTo(3));
            Assert.That(campaign.npcSocial.beatWindow, Is.EqualTo(Windows.AfterVeto));
            Assert.That(campaign.npcSocial.beatPlan.Skip(campaign.npcSocial.beatsFired), Does.Contain(Jamie));
            Assert.That(NpcSocialActions.Repertoire(NpcSocialActions.LeadTrait(campaign.Find(Jamie))),
                Does.Contain(NpcActionKind.Confront));
            Assert.That(EpisodeEngine.BeatsThisWeek(campaign, Jamie), Is.LessThan(EpisodeEngine.BeatQuota(campaign, Jamie)));
            Assert.That(campaign.npcSocial.acts.Any(a => a.week == 3 && a.actorId == Jamie && a.kind == NpcActKinds.Confront), Is.False);
            NoUnansweredHouseAsk(campaign);
            int initialBought = campaign.boughtActionPoints, initialAp = Remaining(campaign);
            Assert.That(EpisodeEngine.WeekRulesOn(campaign), Is.True);
            Assert.That(EpisodeEngine.EconomyRulesOn(campaign), Is.True);
            Assert.That(EpisodeEngine.Window(campaign), Is.EqualTo(Windows.AfterVeto));
            int seats = EpisodeEngine.WindowSeats(campaign, Windows.AfterVeto);
            int spent = EpisodeEngine.SocialActionsSpent(campaign);
            int extrasUsedElsewhere = campaign.moveInExtrasSpent;
            for (int other = 0; other < Windows.Count; other++)
                if (other != Windows.AfterVeto)
                    extrasUsedElsewhere += Math.Max(0, campaign.windowActions[other] - EpisodeEngine.WindowSeats(campaign, other));
            int initialNetCredit = EpisodeEngine.WeeklyExtras(campaign) - extrasUsedElsewhere;
            TestContext.Out.WriteLine("Actual fixed campaign budget: bought=" + initialBought
                + "; storyActions=" + Storylines.SocialActions(campaign)
                + "; haveNotCost=" + HaveNots.ActionCost(campaign)
                + "; extrasUsedElsewhere=" + extrasUsedElsewhere + "; netCredit=" + initialNetCredit + ".");
            Assert.That(seats, Is.EqualTo(2));
            Assert.That(spent, Is.Zero);
            Assert.That(initialAp, Is.EqualTo(2));
            Assert.That(initialNetCredit, Is.EqualTo(-1),
                "The observed route has one absorbed credit before additional purchases become usable AP.");
            for (int purchase = 0; purchase < 6; purchase++)
            {
                var s = engine.Snapshot;
                Assert.That(ExtraActionPurchaseChoice.Available(s), Is.True);
                var picker = ExtraActionPurchaseChoice.Open(s);
                Assert.That(picker, Is.Not.Null);
                Assert.That(picker.CanChoose(s, Jamie), Is.True, "Never fall back to a random or ineligible cost target.");
                var buy = Command(s, EpisodeCommandKind.BuyActionPoint);
                buy.targetId = Jamie;
                buy.text = WebSocialVocabulary.BurnOne;
                var after = Accepted(engine, buy);
                Assert.That(after.boughtActionPoints, Is.EqualTo(s.boughtActionPoints + 1));
                Assert.That(after.week, Is.EqualTo(s.week));
                Assert.That(after.phase, Is.EqualTo(s.phase));
                Assert.That(after.windowActions, Is.EqualTo(s.windowActions));
                Assert.That(after.socialActions, Is.EqualTo(s.socialActions));
                Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(s.outOfPhaseSocialActions));
                Assert.That(after.moveInExtrasSpent, Is.EqualTo(s.moveInExtrasSpent));
                Assert.That(Storylines.SocialActions(after), Is.EqualTo(Storylines.SocialActions(s)));
                Assert.That(HaveNots.ActionCost(after), Is.EqualTo(HaveNots.ActionCost(s)));
                Assert.That(EpisodeEngine.WeeklyExtras(after), Is.EqualTo(EpisodeEngine.WeeklyExtras(s) + 1));
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent));
                // AfterVeto's two seats ignore a negative weekly pool; positive extras are
                // clamped only after the already-used weekly credit is deducted.
                int expectedBudget = Math.Max(1, seats + Math.Max(0, initialNetCredit + purchase + 1));
                Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(expectedBudget));
                Assert.That(Remaining(after), Is.EqualTo(expectedBudget - spent));
            }
            Assert.That(engine.Snapshot.boughtActionPoints, Is.EqualTo(initialBought + 6));
            Assert.That(Remaining(engine.Snapshot), Is.EqualTo(7),
                "Six raw purchases add five usable AP after absorbing the existing one-credit deficit.");
            for (int talkIndex = 0; talkIndex < 6; talkIndex++)
            {
                var s = engine.Snapshot;
                Assert.That(Remaining(s), Is.GreaterThan(1));
                string partner = s.Active.Where(p => !p.isPlayer && p.id != Jamie)
                    .Select(p => p.id).OrderBy(id => id, StringComparer.Ordinal).First();
                var talk = Command(s, EpisodeCommandKind.Talk);
                talk.targetId = partner;
                var after = Accepted(engine, talk);
                Assert.That(Remaining(after), Is.EqualTo(Remaining(s) - 1));
                NoUnansweredHouseAsk(after); // Discovery observed zero cleanup answers; do not repair a changed route.
            }
            preEntry = engine.Snapshot;
            Assert.That(Remaining(preEntry), Is.EqualTo(1));
            AssertActualCards(preEntry);
            string voter = EpisodeEngine.Voters(preEntry).Where(p => !p.isPlayer).Select(p => p.id)
                .FirstOrDefault(id => DialogueRules.CanStart(preEntry, id, out _));
            Assert.That(voter, Is.Not.Null);
            var start = Command(preEntry, EpisodeCommandKind.StartDialogue);
            start.targetId = voter;
            start.text = DialogueCampaignV1.TemplateId;
            start.secondTargetId = "open-case";
            paused = Accepted(engine, start);
            Assert.That(Remaining(paused), Is.Zero);
            Assert.That(DialogueRules.Cycle(paused, DialogueRules.Active(paused)).beatId, Is.EqualTo("case"));
            AssertActualCards(paused);
            Assert.That(Json(fresh), Is.EqualTo(freshBytes));
            preEntryBytes = Json(preEntry);
            pausedBytes = Json(paused);
            TestContext.Out.WriteLine("Actual fixed free-reply fixture: seed4/cast8/policy0/week3; navigation16,16,9;"
                + " six accepted named Jamie BurnOne purchases and six actual Talks; voter=" + voter
                + "; confrontation=reply-321/Jamie/act3-2-2/tick1; gossip=reply-310/Alex/aboutCasey/act3-2-1/tick0.");
        }

        [SetUp]
        public void CreateOnlyAnOwnedTemporarySaveSlot()
        {
            directory = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(directory), Is.False);
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void PreserveSharedInputsAndRemoveOnlyThisFixturesTwoKnownFiles()
        {
            try
            {
                if (preEntry != null) Assert.That(Json(preEntry), Is.EqualTo(preEntryBytes));
                if (paused != null) Assert.That(Json(paused), Is.EqualTo(pausedBytes));
            }
            finally
            {
                if (directory != null && Directory.Exists(directory))
                {
                    string full = Path.GetFullPath(directory);
                    string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    Assert.That(full.StartsWith(temp, StringComparison.OrdinalIgnoreCase), Is.True);
                    Assert.That(Path.GetFileName(full), Does.StartWith(DirectoryPrefix));
                    Assert.That((File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0, Is.True);
                    Assert.That(Directory.GetDirectories(full), Is.Empty);
                    var store = new EpisodeSaveStore(Path.Combine(full, "actual.json"));
                    string[] files = Directory.GetFiles(full);
                    Assert.That(files.All(path => path == store.SavePath || path == store.BackupPath), Is.True,
                        "Unexpected diagnostic files are preserved, not swept away.");
                    Assert.That(files.All(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0), Is.True);
                    foreach (string path in files) File.Delete(path);
                    Directory.Delete(full, false);
                }
            }
        }

        [TestCase("confrontation", "case")]
        [TestCase("confrontation", "finish-personal")]
        [TestCase("gossip", "case")]
        [TestCase("gossip", "finish-personal")]
        public void ActualIncomingReplySurvivesSaveLoadIsFreeAtThePaidPauseAndResumesOnce(string kind, string node)
        {
            var store = new EpisodeSaveStore(Path.Combine(directory, "actual.json"));
            RoundTrip(store, preEntry, "before-entry");
            var engine = new EpisodeEngine(RoundTrip(store, paused, "case"));
            if (node == "finish-personal")
                engine = new EpisodeEngine(RoundTrip(store,
                    Accepted(engine, Choice(engine.Snapshot, "appeal-personally")), node));
            var before = engine.Snapshot;
            Valid(before);
            string episodeId = DialogueRules.Active(before).id;
            var beforeCycle = before.storylines.Single(row => row.id == episodeId);
            Assert.That(beforeCycle.beatId, Is.EqualTo(node));
            Assert.That(Remaining(before), Is.Zero);
            AssertActualCards(before);
            var card = before.replyCards.Single(row => row.id == (kind == ReplyCards.Confrontation ? ConfrontCard : GossipCard));
            Assert.That(card.kind, Is.EqualTo(kind));
            string beforeBytes = Json(before), dialogueBytes = Json(before.dialogue), cycleBytes = Json(beforeCycle);
            string canonicalBytes = Json(before.unifiedCommitments), priorReceipts = Json(before.ledger.replies);
            string remainingCards = Json(before.replyCards.Where(row => row.id != card.id).ToArray());
            byte[][] savedBeforeReply = SlotBytes(store);
            var command = Command(before, EpisodeCommandKind.ReplyToHouseguest);
            command.targetId = card.id;
            command.text = kind == ReplyCards.Confrontation ? "apologize" : "slide";
            string requestBytes = Json(command);

            // This is exact deterministic replay, not an independent derivation of relationship effects.
            var replay = new EpisodeEngine(before).Apply(command);
            Assert.That(replay.accepted, Is.True, replay.reason);
            Assert.That(replay.duplicate, Is.False);
            Valid(replay.state);
            var answered = Accepted(engine, command);
            Assert.That(Json(answered), Is.EqualTo(Json(replay.state)));
            Assert.That(answered.randomState, Is.EqualTo(replay.state.randomState));
            Assert.That(answered.npcSocial.randomState, Is.EqualTo(replay.state.npcSocial.randomState));
            Assert.That(answered.randomState, Is.Not.EqualTo(before.randomState),
                "This real free answer legitimately draws the main RNG; free is not synonymous with no roll.");
            Assert.That(answered.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
            AssertFreeCounters(before, answered);
            Assert.That(Json(answered.dialogue), Is.EqualTo(dialogueBytes));
            Assert.That(Json(answered.storylines.Single(row => row.id == episodeId)), Is.EqualTo(cycleBytes));
            Assert.That(DialogueRules.Active(answered).id, Is.EqualTo(episodeId));
            Assert.That(Json(answered.unifiedCommitments), Is.EqualTo(canonicalBytes));
            Assert.That(Json(answered.replyCards), Is.EqualTo(remainingCards), "Do not clear or rewrite the other genuine incoming card.");
            Assert.That(answered.ledger.replies.Count, Is.EqualTo(before.ledger.replies.Count + 1));
            Assert.That(Json(answered.ledger.replies.Take(before.ledger.replies.Count).ToArray()), Is.EqualTo(priorReceipts));
            var receipt = answered.ledger.replies.Last();
            Assert.That((receipt.week, receipt.cardId, receipt.kind, receipt.fromId, receipt.listenerId, receipt.replyKey, receipt.promised),
                Is.EqualTo((before.week, card.id, card.kind, card.fromId, card.aboutId, command.text, false)));
            Assert.That(receipt.toThem, Is.EqualTo(kind == ReplyCards.Confrontation ? 10 : -5));
            AssertSlotBytes(store, savedBeforeReply); // Applying/replaying a command is not an implicit disk write.

            var persistedAnswer = RoundTrip(store, answered, node + "-reply");
            engine = new EpisodeEngine(persistedAnswer);
            byte[][] answeredFiles = SlotBytes(store);
            string committedBytes = Json(persistedAnswer);
            var duplicate = engine.Apply(command);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.state), Is.EqualTo(committedBytes));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committedBytes));
            var again = Command(engine.Snapshot, EpisodeCommandKind.ReplyToHouseguest);
            again.targetId = card.id;
            again.text = command.text;
            string againBytes = Json(again);
            var absent = engine.Apply(again);
            Assert.That(absent.accepted || absent.duplicate, Is.False);
            Assert.That(absent.reason, Is.EqualTo("That moment has passed."));
            Assert.That(Json(absent.state), Is.EqualTo(committedBytes));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committedBytes));
            Assert.That(Json(again), Is.EqualTo(againBytes));
            AssertSlotBytes(store, answeredFiles);

            if (node == "case")
            {
                var middle = Accepted(engine, Choice(engine.Snapshot, "appeal-personally"));
                AssertFreeCounters(before, middle);
                engine = new EpisodeEngine(RoundTrip(store, middle, "resumed-finish"));
            }
            var finished = Accepted(engine, Choice(engine.Snapshot, "make-plea"));
            AssertFreeCounters(before, finished);
            var episode = finished.dialogue.episodes.Single(row => row.id == episodeId);
            var cycle = finished.storylines.Single(row => row.id == episodeId);
            Assert.That(DialogueRules.Active(finished), Is.Null);
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(cycle.endingId, Is.EqualTo("answered"));
            Assert.That(cycle.path.Select(row => row.optionId), Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }));
            Assert.That(finished.houseEvents.Count(row => row.cycleId == episodeId), Is.EqualTo(3));
            Assert.That(finished.houseEvents.Where(row => row.cycleId == episodeId).All(row => row.resolved), Is.True);
            Assert.That(episode.responses.Count, Is.EqualTo(3));
            Assert.That(episode.time, Is.Not.Null);
            Assert.That(episode.time.reason, Is.EqualTo("answered"));
            Assert.That(episode.time.revision, Is.EqualTo(finished.revision));
            Assert.That(new[] { episode.time.proposedDelta, episode.time.appliedDelta, episode.time.spentRevision,
                episode.time.spentSequence, episode.time.spentWindowAction }, Is.All.EqualTo(0));
            Assert.That(Json(episode.entry), Is.EqualTo(Json(DialogueRules.Active(before).entry)));
            var lobby = finished.lobbies.Single(row => row.week == 3 && row.phase == EpisodePhase.Campaign
                && row.deciderId == episode.npcId && row.ask == LobbyAsk.Vote);
            Assert.That(lobby.approach, Is.EqualTo(LobbyApproach.Emotional));
            Assert.That(episode.responses.Last().responseKey, Is.EqualTo(DialogueCampaignV1.TemplateId + "/vote-" + lobby.response));
            Assert.That(finished.ledger.replies.Count(row => row.cardId == card.id), Is.EqualTo(1));
            var finalLoaded = RoundTrip(store, finished, "answered");
            Assert.That(Json(finalLoaded), Is.EqualTo(Json(finished)));
            Assert.That(Json(before), Is.EqualTo(beforeBytes));
            Assert.That(Json(command), Is.EqualTo(requestBytes));
            Assert.That(Json(paused), Is.EqualTo(pausedBytes));
            Assert.That(Json(preEntry), Is.EqualTo(preEntryBytes));
            TestContext.Out.WriteLine("Actual native reply/save path: " + kind + "/" + node
                + "; card=" + card.id + "; producer=" + (kind == ReplyCards.Confrontation ? "3-2-2" : "3-2-1")
                + "; replyRevision=" + answered.revision + "; resumedRevision=" + finished.revision + ".");
        }

        private static void AssertActualCards(EpisodeState s)
        {
            Assert.That(s.week, Is.EqualTo(3));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign));
            var confrontation = s.replyCards.Single(row => row.id == ConfrontCard);
            Assert.That((confrontation.kind, confrontation.fromId, confrontation.aboutId),
                Is.EqualTo((ReplyCards.Confrontation, Jamie, (string)null)));
            var gossip = s.replyCards.Single(row => row.id == GossipCard);
            Assert.That((gossip.kind, gossip.fromId, gossip.aboutId), Is.EqualTo((ReplyCards.Gossip, Alex, Casey)));
            Assert.That(s.replyCards.IndexOf(gossip), Is.LessThan(s.replyCards.IndexOf(confrontation)),
                "The real older gossip remains present; engine card-ID selection is not UI queue coverage.");
            var confronted = s.npcSocial.acts.Single(row => row.id == "3-2-2");
            Assert.That((confronted.week, confronted.window, confronted.kind, confronted.actorId, confronted.partnerId, confronted.subjectId, confronted.firedTick),
                Is.EqualTo((3, Windows.AfterVeto, NpcActKinds.Confront, Jamie, s.playerId, (string)null, 1)));
            var hunted = s.npcSocial.acts.Single(row => row.id == "3-2-1");
            Assert.That((hunted.week, hunted.window, hunted.kind, hunted.actorId, hunted.partnerId, hunted.subjectId, hunted.firedTick),
                Is.EqualTo((3, Windows.AfterVeto, NpcActKinds.Hunt, Alex, Casey, s.playerId, 0)));
            Assert.That(s.Find(Jamie).status, Is.EqualTo(ContestantStatus.Active));
            Assert.That(s.Find(Alex).status, Is.EqualTo(ContestantStatus.Active));
        }

        // Exactly discovery02's policy0, limited to the public phases reached before week3 Campaign.
        private EpisodeCommand NextPolicyZero(EpisodeState s)
        {
            var c = Command(s, EpisodeCommandKind.Advance);
            if (s.pendingDiary != null) { c.kind = EpisodeCommandKind.SkipDiary; c.targetId = s.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer))
            { c.kind = EpisodeCommandKind.Compete; c.performance = .5; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                var ids = EpisodeEngine.NominationCandidates(s).Take(2).ToArray();
                Assert.That(ids.Length, Is.EqualTo(2));
                c.kind = EpisodeCommandKind.Nominate; c.targetId = ids[0].id; c.secondTargetId = ids[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved
                && (s.vetoHolderId == s.playerId || s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null))
            {
                c.kind = EpisodeCommandKind.ResolveVeto;
                var replacement = EpisodeEngine.ReplacementCandidates(s).FirstOrDefault();
                c.useVeto = replacement != null && !EpisodeEngine.VetoIsLockedAtFinalFour(s);
                c.targetId = c.useVeto ? s.vetoHolderId == s.playerId ? s.nominees[0] : EpisodeEngine.NpcVetoSave(s) : null;
                c.secondTargetId = replacement == null ? null : replacement.id;
            }
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
                && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(row => row.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitEvictionSpeech; c.text = "I'd like to stay. I've been straight with all of you."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(row => row.voterId == s.playerId)
                && (EpisodeEngine.Voters(s).Any(row => row.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.nominees[0]; }
            return c;
        }

        private EpisodeCommand Choice(EpisodeState s, string option)
        {
            var episode = DialogueRules.Active(s);
            Assert.That(episode, Is.Not.Null);
            var c = Command(s, EpisodeCommandKind.ChooseDialogue);
            c.targetId = episode.id;
            c.text = DialogueRules.Cycle(s, episode).beatId;
            c.secondTargetId = option;
            return c;
        }

        private EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-real-reply-save-" + (++nonce), actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand c)
        {
            var source = engine.Snapshot;
            string sourceBytes = Json(source), requestBytes = Json(c);
            Valid(source);
            var result = engine.Apply(c);
            Assert.That(result.accepted, Is.True, source.phase + " " + c.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(source.revision + 1));
            Valid(result.state);
            Assert.That(Json(source), Is.EqualTo(sourceBytes));
            Assert.That(Json(c), Is.EqualTo(requestBytes));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            return result.state;
        }

        private static EpisodeState RoundTrip(EpisodeSaveStore store, EpisodeState state, string label)
        {
            Valid(state);
            string stateBytes = Json(state);
            byte[] formerPrimary = File.Exists(store.SavePath) ? File.ReadAllBytes(store.SavePath) : null;
            store.Save(state);
            Assert.That(Json(state), Is.EqualTo(stateBytes));
            if (formerPrimary == null) Assert.That(File.Exists(store.BackupPath), Is.False);
            else Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(formerPrimary), "Actual rotation must preserve the preceding primary.");
            byte[][] files = SlotBytes(store);
            Assert.That(store.TryLoad(out var loaded, out string error), Is.True, error);
            Valid(loaded);
            Assert.That(Json(loaded), Is.EqualTo(stateBytes));
            Assert.That(Json(state), Is.EqualTo(stateBytes));
            AssertSlotBytes(store, files);
            TestContext.Out.WriteLine("Actual real-reply SaveStore round trip: " + label + ", bytes=" + files[0].Length + ".");
            return loaded;
        }

        private static byte[][] SlotBytes(EpisodeSaveStore store) => new[]
        {
            File.ReadAllBytes(store.SavePath), File.Exists(store.BackupPath) ? File.ReadAllBytes(store.BackupPath) : null,
        };

        private static void AssertSlotBytes(EpisodeSaveStore store, byte[][] files)
        {
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(files[0]));
            if (files[1] == null) Assert.That(File.Exists(store.BackupPath), Is.False);
            else Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(files[1]));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(store.SavePath), "*.pending-*"), Is.Empty);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(store.SavePath), "*.before-recovery-*"), Is.Empty);
        }

        private static void AssertFreeCounters(EpisodeState before, EpisodeState after)
        {
            Assert.That(Remaining(after), Is.Zero);
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints));
            Assert.That(after.moveInExtrasSpent, Is.EqualTo(before.moveInExtrasSpent));
        }

        private static void NoUnansweredHouseAsk(EpisodeState s) =>
            Assert.That(s.houseEvents.Any(row => !row.resolved && HouseEventKind.Asks(row.kind)), Is.False);
        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
    }
}
