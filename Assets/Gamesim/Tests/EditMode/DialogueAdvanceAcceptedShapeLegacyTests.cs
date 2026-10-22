using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Adversarial accepted-shape storage, NOT a claim that today's catalog produces a Vote
    /// deal on its neutral lapse. A real first-week public-command walk supplies the paid
    /// conversation. Only then does this fixture append one explicitly synthetic old Story
    /// cycle/event/effect. Public Advance must still preserve its original source order and
    /// admit the later canonical Story write after the paid lapse is sealed. No catalog,
    /// old history, nominee, relationship, action counter, sequence or RNG is rewritten.
    /// </summary>
    public sealed class DialogueAdvanceAcceptedShapeLegacyTests
    {
        private const int Seeds = 24, WalkCommands = 96, NeutralAnswers = 24, TalkCommands = 16;
        private const string LegacyTemplate = "final-plea", LegacyBeat = "plea", LegacyLapse = "say-nothing";
        private EpisodeState campaign;
        private string campaignBytes, voter;

        [OneTimeSetUp]
        public void FindAnActualFirstWeekCampaignWithOneAction()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero, "This fixture does not publish the pilot.");
                fresh.dialogueRulesVersion = 1;
                Valid(fresh);
                string freshBytes = Json(fresh);
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < WalkCommands; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase != EpisodePhase.Campaign)
                    {
                        Accepted(engine, ModeTwoReaderSweep.Next(s));
                        continue;
                    }
                    if (!s.nominees.Contains(s.playerId)) break;
                    CloseOldAsks(engine);
                    for (int talk = 0; talk < TalkCommands && Remaining(engine.Snapshot) > 1; talk++)
                    {
                        s = engine.Snapshot;
                        var command = Command(s, EpisodeCommandKind.Talk);
                        command.targetId = s.nominees.First(id => id != s.playerId);
                        Accepted(engine, command);
                        CloseOldAsks(engine);
                    }
                    s = engine.Snapshot;
                    Assert.That(Remaining(s), Is.LessThanOrEqualTo(1), "The actual Talk debit bound must not be silently exhausted.");
                    string eligible = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                        .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _) && !AlreadyHasThisDeal(s, id));
                    if (Remaining(s) != 1 || eligible == null) break;
                    campaign = s.Clone(); campaignBytes = Json(campaign); voter = eligible;
                    Assert.That(Json(fresh), Is.EqualTo(freshBytes), "The real walk preserves its constructor source.");
                    Assert.That(campaign.unifiedCommitmentRulesVersion, Is.EqualTo(2));
                    Assert.That(EpisodeEngine.BeforeTheFirstAsk(campaign), Is.True);
                    TestContext.Out.WriteLine("Accepted-shape legacy test's REAL paid base: seed " + seed
                        + ", revision " + s.revision + ", voter " + voter + ". The later legacy addition is synthetic.");
                    return;
                }
                Assert.That(Json(fresh), Is.EqualTo(freshBytes));
            }
            Assert.Fail("No real first-week nominated campaign with one AP and an unowed voter in "
                + Seeds + " seeds x " + WalkCommands + " public commands; no fixture state was forced.");
        }

        [TestCase("case", 2)]
        [TestCase("finish-personal", 3)]
        public void AcceptedShapeLegacyVoteEffectAfterPaidLapseCommitsInPublicAdvance(string node, int paidEvents)
        {
            Assert.That(Json(campaign), Is.EqualTo(campaignBytes));
            var played = new EpisodeEngine(campaign);
            var start = Command(played.Snapshot, EpisodeCommandKind.StartDialogue);
            start.targetId = voter; start.secondTargetId = "open-case"; start.text = DialogueCampaignV1.TemplateId;
            var entered = Accepted(played, start);
            string paidId = entered.dialogue.episodes.Single().id;
            if (node == "finish-personal")
            {
                var middle = Command(entered, EpisodeCommandKind.ChooseDialogue);
                middle.targetId = paidId; middle.secondTargetId = "appeal-personally"; middle.text = "case";
                Accepted(played, middle);
            }
            var actualPaidSource = played.Snapshot;
            Assert.That(Remaining(actualPaidSource), Is.Zero);
            Assert.That(actualPaidSource.storylines.Single(c => c.id == paidId).beatId, Is.EqualTo(node));
            Assert.That(AlreadyHasThisDeal(actualPaidSource, voter), Is.False,
                "An existing deal must not turn the tested writer into a business-refused no-op.");

            // This addition is intentionally NOT StartStory or an actual catalog-generated ask.
            // The current catalog's neutral option has no effects. Persisted old effects are
            // independently shape-validated, so an accepted shape must not break the owner seam.
            var authored = StoryCatalog.Find(LegacyTemplate).Beat(LegacyBeat).Option(LegacyLapse);
            Assert.That(authored, Is.Not.Null);
            Assert.That(authored.effects, Is.Empty, "Do not silently relabel this fixture as catalog-produced gameplay.");
            var source = AppendAcceptedShapeLegacy(actualPaidSource, paidId, voter, out string legacyId, out string eventId);
            string sourceBytes = Json(source);
            int oldCount = source.unifiedCommitments.Count;
            var controlSource = source.Clone();
            controlSource.houseEvents.Single(e => e.id == eventId).choices.Single().effects.Clear();
            Valid(controlSource);
            string controlBytes = Json(controlSource);
            var control = new EpisodeEngine(controlSource);
            var controlAfter = Accepted(control, Command(controlSource, EpisodeCommandKind.Advance));
            Assert.That(Json(controlSource), Is.EqualTo(controlBytes));

            var engine = new EpisodeEngine(source);
            var advance = Command(source, EpisodeCommandKind.Advance);
            var after = Accepted(engine, advance);
            Assert.That(Json(source), Is.EqualTo(sourceBytes));
            Assert.That(Json(campaign), Is.EqualTo(campaignBytes));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(after.evictionStage, Is.EqualTo(EvictionStage.Speeches));
            Assert.That(after.windowActions, Is.EqualTo(source.windowActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(source.outOfPhaseSocialActions));
            Assert.That(after.randomState, Is.EqualTo(controlAfter.randomState),
                "A deterministic Story deal adds no main-stream random draw relative to the identical no-effect lapse.");
            Assert.That(after.npcSocial.randomState, Is.EqualTo(controlAfter.npcSocial.randomState),
                "Close's actual NPC work runs in both branches; the later Story write cannot invent an NPC roll.");
            Assert.That(Json(after.npcSocial), Is.EqualTo(Json(controlAfter.npcSocial)),
                "The same real closing NPC pass happens before either lapse effect; it is not skipped to make the test pass.");

            var paid = after.dialogue.episodes.Single(e => e.id == paidId);
            var paidCycle = after.storylines.Single(c => c.id == paidId);
            Assert.That(paidCycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(paidCycle.endingId, Is.EqualTo("lapsed"));
            Assert.That(paidCycle.path.Last().result, Is.EqualTo(StoryResults.Lapsed));
            Assert.That(paidCycle.path.Last().optionId, Is.EqualTo("leave"));
            Assert.That(paidCycle.path.Count, Is.EqualTo(paidEvents));
            Assert.That(after.houseEvents.Count(e => e.cycleId == paidId), Is.EqualTo(paidEvents), "No fourth funded event.");
            Assert.That(paid.responses.Last().responseKey, Is.EqualTo(DialogueCampaignV1.TemplateId + "/occasion-closed"));
            Assert.That(paid.time.reason, Is.EqualTo("occasion-closed"));
            Assert.That(paid.time.revision, Is.EqualTo(after.revision));
            Assert.That(paid.time.sequence, Is.EqualTo(paidCycle.path.Last().logSequence));
            Assert.That(paid.time.window, Is.EqualTo(Windows.AfterVeto));
            Assert.That(new[] { paid.time.proposedDelta, paid.time.appliedDelta, paid.time.spentRevision,
                paid.time.spentSequence, paid.time.spentWindowAction }, Is.All.EqualTo(0));
            Assert.That(after.dialogue.grants, Is.Empty);
            Assert.That(DialogueRules.Active(after), Is.Null);
            Assert.That(Json(after.dialogue), Is.EqualTo(Json(controlAfter.dialogue)),
                "The later accepted-shape Story effect cannot rewrite the paid receipt subtree.");

            var priorIds = new HashSet<string>(source.unifiedCommitments.Select(c => c.id), StringComparer.Ordinal);
            var written = after.unifiedCommitments.Where(c => !priorIds.Contains(c.id)
                && c.kind == UnifiedVoteTogether.Vote && c.origin == UnifiedCommitments.StoryDeal).ToArray();
            Assert.That(written, Has.Length.EqualTo(1), "The actual later Story Vote writer must land, not silently refuse.");
            var row = written.Single();
            Assert.That(row.id, Does.StartWith("deal-story-"));
            Assert.That(int.TryParse(row.id.Substring("deal-story-".Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out int writerSequence), Is.True);
            Assert.That(writerSequence, Is.EqualTo(paid.time.sequence + 1),
                "The appended old event resolves immediately after the paid lapse's actual outcome log.");
            Assert.That((row.kind, row.sourcePolicy, row.origin, row.status, row.reciprocal),
                Is.EqualTo((UnifiedVoteTogether.Vote, UnifiedCommitments.DealPolicy,
                    UnifiedCommitments.StoryDeal, DealStatus.Active, true)));
            Assert.That((row.makerId, row.beneficiaryId, row.subtype, row.targetId),
                Is.EqualTo((source.playerId, voter, DealKind.VoteSave, source.playerId)));
            Assert.That((row.createdWeek, row.expiresWeek, row.voteBindingWeek, row.voteFirstRevealWeek),
                Is.EqualTo((source.week, source.week, source.week, source.week)));
            Assert.That(row.conditionalVeto, Is.Null);
            Assert.That(Json(after.unifiedCommitments.Take(oldCount)), Is.EqualTo(Json(source.unifiedCommitments)),
                "Every old canonical row remains byte-for-byte in its original prefix.");
            Assert.That(Json(after.unifiedCommitments.Where(c => c.id != row.id)), Is.EqualTo(Json(controlAfter.unifiedCommitments)),
                "The no-effect public-Advance control has exactly the same other canonical writes.");
            Assert.That(after.promises.Any(p => p.kind == PromiseKind.Vote), Is.False);
            Assert.That(after.deals.Any(d => KnownBallots.IsVoteDeal(d.type)), Is.False, "Mode2 never writes a raw Vote mirror.");

            var legacy = after.storylines.Single(c => c.id == legacyId);
            var legacyEvent = after.houseEvents.Single(e => e.id == eventId);
            Assert.That(legacyEvent.resolved, Is.True);
            Assert.That(legacyEvent.chosenIndex, Is.Zero);
            Assert.That(legacy.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(legacy.path.Single().result, Is.EqualTo(StoryResults.Lapsed));
            Assert.That(legacy.path.Single().optionId, Is.EqualTo(LegacyLapse));
            Assert.That(legacy.path.Single().logSequence, Is.EqualTo(writerSequence + 2),
                "The Story deal consumes its ID and deal log before the old Story outcome is recorded.");
            var close = after.events.Single(e => e.sequence >= source.nextSequence && e.kind == "campaign-close");
            Assert.That(close.sequence, Is.GreaterThan(legacy.path.Single().logSequence));
            Assert.That(after.nextSequence, Is.EqualTo(controlAfter.nextSequence + 2),
                "Only the actual deal ID and its actual player-facing deal log are added to this deterministic control.");
            Valid(after);

            // The committed command is idempotent even after both closures; neither is recreated.
            string committedBytes = Json(after), requestBytes = Json(advance);
            var duplicate = engine.Apply(advance);
            Assert.That(duplicate.accepted, Is.False); Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.state), Is.EqualTo(committedBytes));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committedBytes));
            Assert.That(Json(advance), Is.EqualTo(requestBytes));
            Assert.That(Json(source), Is.EqualTo(sourceBytes));
        }

        private static EpisodeState AppendAcceptedShapeLegacy(EpisodeState played, string paidId, string voterId,
            out string legacyId, out string eventId)
        {
            string before = Json(played);
            var source = played.Clone();
            string cycleKey = "accepted-shape-legacy-" + paidId;
            string eventKey = "accepted-shape-event-" + paidId;
            legacyId = cycleKey; eventId = eventKey;
            Assert.That(source.storylines.Any(c => c.id == cycleKey), Is.False);
            Assert.That(source.houseEvents.Any(e => e.id == eventKey), Is.False);
            source.storylines.Add(new StorylineState
            {
                id = legacyId, templateId = LegacyTemplate, title = "Accepted-shape legacy final plea",
                category = "The Block", lane = StoryLanes.Game, week = source.week,
                status = StorylineStatus.Active, beatId = LegacyBeat, eventId = eventId,
                cast = new List<StoryRoleState> { new StoryRoleState { role = "VOTER", contestantId = voterId } },
            });
            source.houseEvents.Add(new HouseEventState
            {
                id = eventId, kind = HouseEventKind.Story, title = "Accepted-shape legacy final plea",
                narrative = "A persisted legacy option carries an adversarial accepted-shape Vote effect.",
                week = source.week, contentId = LegacyTemplate + ":" + LegacyBeat, cycleId = legacyId,
                closesAnchor = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Bedroom,
                lapseOptionId = LegacyLapse, involvedIds = new List<string> { voterId },
                cast = new List<StoryRoleState> { new StoryRoleState { role = "VOTER", contestantId = voterId } },
                choices = new List<HouseEventChoice>
                {
                    new HouseEventChoice
                    {
                        optionId = LegacyLapse, label = "Say nothing", description = "Adversarial stored option, not an authored deal.",
                        lapse = true,
                        effects = new List<StoryEffectState>
                        {
                            new StoryEffectState { kind = StoryEffects.Deal, fromId = source.playerId,
                                toId = voterId, thirdId = source.playerId, type = DealKind.VoteSave },
                        },
                    },
                },
            });
            Assert.That(source.houseEvents.FindIndex(e => e.id == eventKey),
                Is.GreaterThan(source.houseEvents.FindIndex(e => e.cycleId == paidId && !e.resolved)));
            var restored = source.Clone();
            restored.houseEvents.RemoveAt(restored.houseEvents.Count - 1);
            restored.storylines.RemoveAt(restored.storylines.Count - 1);
            Assert.That(Json(restored), Is.EqualTo(before),
                "The only fixture changes are the additive synthetic cycle/event/effect; even sequence and both RNGs are unchanged.");
            Assert.That(Json(played), Is.EqualTo(before));
            Valid(source); // Entire EpisodeValidation, not only the local dialogue or Story shape leaf.
            return source;
        }

        private static bool AlreadyHasThisDeal(EpisodeState s, string npcId) => CommitmentReferences.Deals(s).Any(d =>
            DealStatus.Binds(d.status) && d.type == DealKind.VoteSave && d.targetId == s.playerId
            && ((d.proposerId == s.playerId && d.recipientId == npcId) || (d.proposerId == npcId && d.recipientId == s.playerId)));

        private static void CloseOldAsks(EpisodeEngine engine)
        {
            for (int i = 0; i < NeutralAnswers; i++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || !item.contentId.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal), Is.True);
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(o => o.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False);
                    c.secondTargetId = lapse.optionId;
                }
                else c.text = item.choices.First().label;
                Accepted(engine, c);
            }
            Assert.Fail("Existing asks exceeded the bound of " + NeutralAnswers + " actual neutral answers.");
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "accepted-shape-advance-" + s.revision + "-" + kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var before = engine.Snapshot;
            string beforeBytes = Json(before), requestBytes = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(Json(before), Is.EqualTo(beforeBytes));
            Assert.That(Json(command), Is.EqualTo(requestBytes));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            Valid(result.state);
            return result.state;
        }

        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
    }
}
