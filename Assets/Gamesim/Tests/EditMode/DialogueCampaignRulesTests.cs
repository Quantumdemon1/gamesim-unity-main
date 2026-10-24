using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Handbuilt storage witnesses for the local semantic owner, NOT authored paid gameplay,
    /// whole-state acceptance, persistence proof, or permission to activate fresh seasons.
    /// </summary>
    public sealed class DialogueCampaignRulesTests
    {
        private const string Speaker = "casey-wilson";
        private sealed class SavedFields : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type type) =>
                type.GetFields(BindingFlags.Public | BindingFlags.Instance).Cast<MemberInfo>().ToList();
        }
        private static readonly IContractResolver SavedFieldContracts = new SavedFields();

        // Match the saved DTO surface. Convenience getters such as Active may enumerate corrupt
        // participant storage; the refusal/nonmutation assertion must reach the validator itself.
        private static string Json(object value) => JsonConvert.SerializeObject(value,
            new JsonSerializerSettings { ContractResolver = SavedFieldContracts });

        private static EpisodeState Empty()
        {
            var s = ContentCatalog.Create(6511);
            ShippedRules.ApplyFresh(s);
            s.dialogueRulesVersion = 1;
            s.phase = EpisodePhase.Campaign; s.hohId = ContentCatalog.MayaId;
            s.nominees = new List<string> { s.playerId, "taylor-kim" };
            s.revision = 20; s.nextSequence = 100;
            s.windowActions[Windows.AfterVeto] = 1;
            return s;
        }

        private static HouseEventChoice Choice(string id) => new HouseEventChoice
        {
            optionId = id, lapse = id == DialogueCampaignV1.Leave,
            label = id == DialogueCampaignV1.OpenCase ? DialogueCampaignV1.OpenLabel
                : id == DialogueCampaignV1.Appeal ? DialogueCampaignV1.AppealLabel
                : id == DialogueCampaignV1.Plea ? DialogueCampaignV1.PleaLabel
                : id == DialogueCampaignV1.KeepOpen ? DialogueCampaignV1.KeepLabel : DialogueCampaignV1.LeaveLabel,
            description = id == DialogueCampaignV1.OpenCase ? DialogueCampaignV1.OpenDescription
                : id == DialogueCampaignV1.Appeal ? DialogueCampaignV1.AppealDescription
                : id == DialogueCampaignV1.Plea ? DialogueCampaignV1.PleaDescription
                : id == DialogueCampaignV1.KeepOpen ? DialogueCampaignV1.KeepDescription : DialogueCampaignV1.LeaveDescription,
            next = id == DialogueCampaignV1.OpenCase ? DialogueCampaignV1.Case
                : id == DialogueCampaignV1.Appeal ? DialogueCampaignV1.Finish
                : id == DialogueCampaignV1.Plea ? "end:answered" : "end:neutral"
        };

        private static HouseEventState Event(string beat, int sequence, string cycleId) => new HouseEventState
        {
            id = "house-event-" + sequence, kind = HouseEventKind.Story, week = 1, cycleId = cycleId,
            contentId = DialogueCampaignV1.TemplateId + ":" + beat,
            title = beat == DialogueCampaignV1.Open ? DialogueCampaignV1.OpenTitle
                : beat == DialogueCampaignV1.Case ? DialogueCampaignV1.CaseTitle : DialogueCampaignV1.FinishTitle,
            narrative = beat == DialogueCampaignV1.Open ? DialogueCampaignV1.OpenSummary
                : beat == DialogueCampaignV1.Case ? DialogueCampaignV1.CaseSummary : DialogueCampaignV1.FinishSummary,
            closesAnchor = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapseOptionId = DialogueCampaignV1.Leave,
            cast = new List<StoryRoleState> { new StoryRoleState { role = "speaker", contestantId = Speaker } },
            involvedIds = new List<string> { Speaker },
            choices = (beat == DialogueCampaignV1.Open ? new[] { DialogueCampaignV1.OpenCase, DialogueCampaignV1.Leave }
                : beat == DialogueCampaignV1.Case ? new[] { DialogueCampaignV1.Appeal, DialogueCampaignV1.Leave }
                : new[] { DialogueCampaignV1.Plea, DialogueCampaignV1.KeepOpen, DialogueCampaignV1.Leave }).Select(Choice).ToList()
        };

        private static EpisodeState Witness(string shape, string answer = LobbyResponse.Receptive)
        {
            var s = Empty();
            bool active = shape.StartsWith("active-", StringComparison.Ordinal);
            bool finish = shape.EndsWith("finish", StringComparison.Ordinal) || shape == "answered" || shape == "keep";
            bool lapsed = shape.StartsWith("lapsed-", StringComparison.Ordinal), absent = shape == "lapsed-absent";
            var episode = new DialogueEpisodeState
            {
                id = "story-10", npcId = Speaker, occasionKind = "campaign", occasionId = "week:1:campaign:" + Speaker,
                contentVersion = 1, week = 1, window = Windows.AfterVeto, startedPhase = EpisodePhase.Campaign,
                startedRevision = 18, startedSequence = 10, entryEventId = "house-event-11",
                entry = new DialogueEntryReceiptState { kind = "action", revision = 18, windowActionBefore = 0, windowActionAfter = 1 }
            };
            var cycle = new StorylineState
            {
                id = episode.id, templateId = DialogueCampaignV1.TemplateId, title = DialogueCampaignV1.Title,
                category = DialogueCampaignV1.Category, lane = StoryLanes.Game, week = 1,
                cast = new List<StoryRoleState> { new StoryRoleState { role = "speaker", contestantId = Speaker } },
                status = active ? StorylineStatus.Active : StorylineStatus.Completed,
                endedWeek = active ? 0 : 1, endingId = active ? null : lapsed ? "lapsed" : shape == "answered" ? "answered" : "neutral"
            };
            s.dialogue.episodes.Add(episode); s.storylines.Add(cycle);
            string[] beats = finish ? new[] { DialogueCampaignV1.Open, DialogueCampaignV1.Case, DialogueCampaignV1.Finish }
                : new[] { DialogueCampaignV1.Open, DialogueCampaignV1.Case };
            for (int i = 0; i < beats.Length; i++)
            {
                var item = Event(beats[i], 11 + 4 * i, episode.id);
                s.houseEvents.Add(item); cycle.eventId = item.id; cycle.beatId = beats[i];
                if (active && i == beats.Length - 1) continue;
                bool terminal = !active && i == beats.Length - 1;
                string option = !terminal ? i == 0 ? DialogueCampaignV1.OpenCase : DialogueCampaignV1.Appeal
                    : shape == "answered" ? DialogueCampaignV1.Plea : shape == "keep" ? DialogueCampaignV1.KeepOpen : DialogueCampaignV1.Leave;
                item.resolved = true; item.chosenIndex = item.choices.FindIndex(c => c.optionId == option);
                item.outcome = terminal && lapsed ? DialogueCampaignV1.LapsedOutcome
                    : option == DialogueCampaignV1.OpenCase ? DialogueCampaignV1.OpenOutcome
                    : option == DialogueCampaignV1.Appeal ? DialogueCampaignV1.AppealOutcome
                    : option == DialogueCampaignV1.Plea ? DialogueCampaignV1.PleaOutcome
                    : option == DialogueCampaignV1.KeepOpen ? DialogueCampaignV1.KeepOutcome : DialogueCampaignV1.LeaveOutcome;
                int log = 12 + 4 * i;
                cycle.path.Add(new StoryStepState { beatId = beats[i], optionId = option,
                    result = terminal && lapsed ? StoryResults.Lapsed : StoryResults.Plain, week = 1, logSequence = log });
                s.events.Add(new EpisodeEvent { sequence = log, week = 1, phase = EpisodePhase.Campaign,
                    kind = StoryLog.Outcome, text = "A handbuilt campaign storage witness.", audienceIds = new List<string> { s.playerId, Speaker } });
                if (!terminal || !absent)
                    episode.responses.Add(new DialogueResponseState { eventId = item.id, speakerId = Speaker, pathIndex = i,
                        revision = terminal ? 20 : 18 + i,
                        responseKey = DialogueCampaignV1.TemplateId + "/" + (!terminal ? i == 0 ? "hear-your-case" : "hear-personal-case"
                            : lapsed ? "occasion-closed" : shape == "answered" ? "vote-" + answer
                            : shape == "keep" ? "no-promise-made" : "respectful-exit") });
                if (terminal) episode.time = new DialogueTimeReceiptState { revision = 20, sequence = log,
                    window = Windows.AfterVeto, reason = lapsed ? "occasion-closed" : cycle.endingId };
            }
            if (shape == "answered") s.lobbies.Add(new LobbyState { week = 1, phase = EpisodePhase.Campaign,
                deciderId = Speaker, ask = LobbyAsk.Vote, subjectId = s.playerId, approach = LobbyApproach.Emotional, response = answer, influence = 0 });
            // A current boundary lapse has actually left Campaign. These remain handbuilt local
            // storage fixtures, not evidence that an authored Advance command was executed.
            if (lapsed) { s.phase = EpisodePhase.Eviction; s.evictionStage = EvictionStage.Speeches; }
            if (absent) s.Find(Speaker).status = ContestantStatus.Evicted;
            return s;
        }

        private static void Accept(EpisodeState s)
        {
            string before = Json(s);
            Assert.That(DialogueRules.TryValidate(s, out string error), Is.True, error);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static void Reject(EpisodeState s, string reason)
        {
            string before = Json(s);
            Assert.That(DialogueRules.TryValidate(s, out string error), Is.False);
            Assert.That(error, Does.Contain(reason));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("active-case")] [TestCase("active-finish")]
        [TestCase("neutral-case")] [TestCase("neutral-finish")] [TestCase("keep")]
        [TestCase("lapsed-case")] [TestCase("lapsed-finish")] [TestCase("lapsed-absent")]
        public void HandbuiltLocalStorageWitnessesHaveExactGraphAndReceiptClosure(string shape) => Accept(Witness(shape));

        [TestCase(LobbyResponse.Receptive)] [TestCase(LobbyResponse.Open)]
        [TestCase(LobbyResponse.Skeptical)] [TestCase(LobbyResponse.Hostile)]
        public void HandbuiltAnsweredWitnessBindsTheRetainedActualResponseKey(string response) => Accept(Witness("answered", response));

        [Test]
        public void EmptyRuleShelfAndOffDefaultsAreDistinctFromFreshEnrollment()
        {
            var s = Empty(); Accept(s);
            s.dialogueRulesVersion = 0; Accept(s);
            Assert.That(DialogueRules.On(s), Is.False);
            string before = Json(s);
            Assert.That(DialogueRules.CanStart(s, Speaker, out string error), Is.False);
            Assert.That(error, Does.Contain("not active"));
            Assert.That(Json(s), Is.EqualTo(before));
            var fresh = ContentCatalog.Create(6512); ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "These local tests do not activate fresh seasons.");
        }

        [Test]
        public void CanonicalLookupIsClosedFreshDataAndNeverAnAutomaticPoolCandidate()
        {
            Assert.That(DialogueCampaignV1.TemplateId, Is.EqualTo("dialogue-campaign-v1"));
            Assert.That(DialogueCampaignV1.ContentVersion, Is.EqualTo(1));
            Assert.That(StoryCatalog.All.Any(a => a.id == DialogueCampaignV1.TemplateId), Is.False);
            var first = StoryCatalog.Find(DialogueCampaignV1.TemplateId);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.startAnchors, Is.Empty);
            Assert.That(first.conversationTopics, Is.Null);
            Assert.That(first.cast, Is.Null);
            Assert.That(first.weight, Is.Null);
            Assert.That(first.beats.Select(b => b.id), Is.EqualTo(new[] { "open", "case", "finish-personal" }));
            Assert.That(first.beats.Select(b => b.options.Length), Is.EqualTo(new[] { 2, 2, 3 }));
            foreach (var beat in first.beats)
            {
                Assert.That(beat.lapse, Is.EqualTo("leave"));
                foreach (var option in beat.options)
                {
                    Assert.That(option.check, Is.EqualTo(-1));
                    Assert.That(option.costsAction || option.conduct, Is.False);
                    Assert.That(option.effects, Is.Empty); Assert.That(option.backfire, Is.Empty);
                }
            }
            first.beats[0].options[0].label = "Caller mutation";
            var second = StoryCatalog.Find(DialogueCampaignV1.TemplateId);
            Assert.That(ReferenceEquals(first, second), Is.False);
            Assert.That(second.beats[0].options[0].label, Is.EqualTo("Make your case"));
            var s = Witness("active-case"); Accept(s);
            s.houseEvents[0].choices[0].label = "Caller mutation";
            Reject(s, "canonical definition");
        }

        [TestCase("mode")] [TestCase("hearing")] [TestCase("economy")] [TestCase("story-version")]
        [TestCase("read")] [TestCase("lever")] [TestCase("week")] [TestCase("strategy")]
        [TestCase("commitment")] [TestCase("agency")] [TestCase("all-week")]
        [TestCase("deal")] [TestCase("story")] [TestCase("story-world")] [TestCase("npc-world")]
        public void EveryPilotPrerequisiteMustBeActiveFromTheStart(string boundary)
        {
            var s = Empty(); Accept(s);
            switch (boundary)
            {
                case "mode": s.unifiedCommitmentRulesVersion = 1; break;
                case "hearing": s.unifiedHearingRulesVersion = 0; break;
                case "economy": s.economyRulesVersion = 0; break;
                case "story-version": s.story.rulesVersion--; break;
                case "read": s.readRulesStartWeek = 2; break;
                case "lever": s.leverRulesStartWeek = 2; break;
                case "week": s.weekRulesStartWeek = 2; break;
                case "strategy": s.strategyRulesStartWeek = 2; break;
                case "commitment": s.commitmentRulesStartWeek = 2; break;
                case "agency": s.agencyRulesStartWeek = 2; break;
                case "all-week": s.allWeekRulesStartWeek = 2; break;
                case "deal": s.dealRulesStartWeek = 2; break;
                case "story": s.storyRulesStartWeek = 2; break;
                case "story-world": s.story.rulesStartWeek = 2; break;
                case "npc-world": s.npcSocial.rulesStartWeek = 2; break;
            }
            Reject(s, "active-from-week-one");
        }

        /// <summary>
        /// Threads or any later story milestone (ROLE-MOMENTS-PLAN R0): enrolling Roles must not switch the pilot
        /// off for good. An earlier milestone still refuses ("story-version" above).
        /// </summary>
        [TestCase(StoryRules.Threads)] [TestCase(StoryRules.Roles)]
        public void ThePilotHoldsAtThreadsAndAtALaterStoryMilestone(int version)
        {
            foreach (var s in new[] { Empty(), Witness("active-case") })
            {
                s.story.rulesVersion = version;
                Accept(s);
            }
        }

        [TestCase("content", "identity")] [TestCase("occasion", "identity")]
        [TestCase("future-start", "identity")] [TestCase("player-speaker", "identity")]
        [TestCase("entry", "entry receipt")] [TestCase("grant", "entry receipt")]
        [TestCase("tick", "entry receipt")] [TestCase("future-response", "response")]
        [TestCase("response-key", "response")] [TestCase("response-event", "response")]
        [TestCase("statement", "evidence storage")] [TestCase("evidence", "evidence storage")]
        [TestCase("cycle", "Unknown dialogue content")] [TestCase("variant", "exact campaign cycle")]
        [TestCase("path", "resolved event")] [TestCase("option", "resolved event")]
        [TestCase("outcome", "authored edges")] [TestCase("current", "current node")]
        [TestCase("extra-response", "current node")] [TestCase("time-active", "active campaign")]
        public void SingleFieldStorageCorruptionsRefuseWithoutRepair(string corruption, string reason)
        {
            var s = Witness("active-case"); Accept(s);
            var e = s.dialogue.episodes[0]; var c = s.storylines[0];
            switch (corruption)
            {
                case "content": e.contentVersion++; break;
                case "occasion": e.occasionId += "-forged"; break;
                case "future-start": e.startedRevision = s.revision + 1; break;
                case "player-speaker": e.npcId = s.playerId; break;
                case "entry": e.entry = null; break;
                case "grant": e.entry.grantId = "grant-forged"; break;
                case "tick": e.entry.windowActionAfter++; break;
                case "future-response": e.responses[0].revision = s.revision + 1; break;
                case "response-key": e.responses[0].responseKey += "-forged"; break;
                case "response-event": e.responses[0].eventId = s.houseEvents[1].id; break;
                case "statement": e.statement = new DialogueStatementState(); break;
                case "evidence": e.evidence.Add(new DialogueEvidenceState()); break;
                case "cycle": c.templateId += "-future"; break;
                case "variant": c.variant++; break;
                case "path": c.path[0].beatId = DialogueCampaignV1.Finish; break;
                case "option": c.path[0].optionId = DialogueCampaignV1.Leave; break;
                case "outcome": s.houseEvents[0].outcome += "-forged"; break;
                case "current": c.eventId = s.houseEvents[0].id; break;
                case "extra-response": e.responses.Add(e.responses[0].Clone()); break;
                case "time-active": e.time = new DialogueTimeReceiptState(); break;
            }
            Reject(s, reason);
        }

        [TestCase("effect")] [TestCase("backfire")] [TestCase("check")] [TestCase("cost")]
        [TestCase("impact")] [TestCase("trust")] [TestCase("next")] [TestCase("label")]
        [TestCase("locked")] [TestCase("picker")] [TestCase("bonus")] [TestCase("order")]
        public void ShapeValidGenericChoicesCannotChangeTheClosedPilot(string corruption)
        {
            var s = Witness("active-case"); Accept(s);
            var eventRow = s.houseEvents[1]; var choice = eventRow.choices[0];
            switch (corruption)
            {
                case "effect": choice.effects.Add(new StoryEffectState { kind = StoryEffects.Move, amount = 3 }); break;
                case "backfire": choice.backfire.Add(new StoryEffectState { kind = StoryEffects.Move, amount = -3 }); break;
                case "check": choice.checkBase = 50; break;
                case "cost": choice.costsAction = true; break;
                case "impact": choice.impacts.Add(new HouseEventImpact { targetId = s.playerId, amount = 3 }); break;
                case "trust": choice.trustChange = 3; break;
                case "next": choice.next = DialogueCampaignV1.Open; break;
                case "label": choice.label += "-forged"; break;
                case "locked": choice.locked = true; break;
                case "picker": choice.pickPerson = true; break;
                case "bonus": choice.bonusTraits.Add("Social"); break;
                case "order": eventRow.choices.Reverse(); break;
            }
            Reject(s, "canonical definition");
        }

        [TestCase("root")] [TestCase("episodes")] [TestCase("entry")]
        [TestCase("responses")] [TestCase("evidence")] [TestCase("cycle-cast")]
        [TestCase("path")] [TestCase("vars")] [TestCase("event-cast")]
        [TestCase("choices")] [TestCase("effects")] [TestCase("participant")]
        public void MissingRequiredStorageRefusesRatherThanThrowingOrRepairing(string missing)
        {
            var s = Witness("active-case"); Accept(s);
            switch (missing)
            {
                case "root": s.dialogue = null; break;
                case "episodes": s.dialogue.episodes = null; break;
                case "entry": s.dialogue.episodes[0].entry = null; break;
                case "responses": s.dialogue.episodes[0].responses = null; break;
                case "evidence": s.dialogue.episodes[0].evidence = null; break;
                case "cycle-cast": s.storylines[0].cast = null; break;
                case "path": s.storylines[0].path = null; break;
                case "vars": s.storylines[0].vars = null; break;
                case "event-cast": s.houseEvents[0].cast = null; break;
                case "choices": s.houseEvents[0].choices = null; break;
                case "effects": s.houseEvents[0].choices[0].effects = null; break;
                case "participant": s.contestants[0] = null; break;
            }
            string before = Json(s);
            Assert.That(DialogueRules.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("future-time")] [TestCase("nonzero")] [TestCase("spent")]
        [TestCase("missing")] [TestCase("reason")] [TestCase("sequence")]
        public void EveryClosedEpisodeHasExactlyItsZeroTimeReceipt(string corruption)
        {
            var s = Witness("answered"); Accept(s);
            var e = s.dialogue.episodes[0];
            switch (corruption)
            {
                case "future-time": e.time.revision = s.revision + 1; break;
                case "nonzero": e.time.proposedDelta = 1; break;
                case "spent": e.time.spentRevision = 1; break;
                case "missing": e.time = null; break;
                case "reason": e.time.reason = "neutral"; break;
                case "sequence": e.time.sequence++; break;
            }
            Reject(s, "zero-time receipt");
        }

        [TestCase("phase")] [TestCase("nominee")] [TestCase("departure")] [TestCase("already-asked")]
        public void OnlyActiveEpisodesRequireTheActualLiveCampaign(string change)
        {
            var s = Witness("active-case"); Accept(s);
            if (change == "phase") s.phase = EpisodePhase.Eviction;
            else if (change == "nominee") s.nominees.Remove(s.playerId);
            else if (change == "departure") s.Find(Speaker).status = ContestantStatus.Evicted;
            else s.lobbies.Add(new LobbyState { week = 1, phase = EpisodePhase.Campaign, deciderId = Speaker });
            Reject(s, change == "already-asked" ? "already heard" : "no longer playable");
        }

        [Test]
        public void EndedHistorySurvivesLaterDepartureAndResetCountersButNotAContradictoryRetainedAnswer()
        {
            var s = Witness("answered"); Accept(s);
            s.lobbies[0].response = LobbyResponse.Hostile;
            Reject(s, "actual Emotional vote response");
            s.lobbies.Clear(); s.week = 2; s.revision++;
            s.phase = EpisodePhase.Social; s.nominees.Clear(); s.Find(Speaker).status = ContestantStatus.Evicted;
            s.windowActions = new List<int> { 0, 0, 0, 0 };
            Accept(s); // No attempt to reconstruct a previous week's eligibility or vanished lobby.
        }

        [Test]
        public void LapseHasOnlyTheOnePermittedMissingSpeakerResponse()
        {
            var s = Witness("lapsed-case"); Accept(s);
            s.Find(Speaker).status = ContestantStatus.Evicted;
            Reject(s, "actual speaker response"); // Actual new closure cannot fabricate present speech.
            s.revision++; Accept(s); // Retained speech can predate a later departure.
            s.dialogue.episodes[0].responses.RemoveAt(1); Accept(s);
            s.Find(Speaker).status = ContestantStatus.Active;
            Reject(s, "actual speaker response");
        }

        [TestCase("lapsed-case")] [TestCase("lapsed-finish")]
        public void CurrentEligibleCampaignCannotInventAnAutomaticLapse(string shape)
        {
            var s = Witness(shape); Accept(s);
            s.phase = EpisodePhase.Campaign;
            Assert.That(s.dialogue.episodes[0].time.revision, Is.EqualTo(s.revision));
            Assert.That(s.lobbies, Is.Empty);
            Assert.That(StrategyRules.CanLobby(s, Speaker, LobbyAsk.Vote, s.playerId, out string reason), Is.True, reason);
            Reject(s, "occasion");
            // Older receipts cannot reconstruct the earlier closure's live context. This is a
            // historical storage control, not a claim that the fabricated command was executed.
            s.revision++;
            Accept(s);
        }

        private static void FillEvents(EpisodeState s, int count)
        {
            while (s.houseEvents.Count < count) s.houseEvents.Add(new HouseEventState { id = "padding-" + s.houseEvents.Count,
                kind = HouseEventKind.Ambient, title = "Storage padding", narrative = "Not a gameplay witness.", week = s.week, resolved = true });
        }

        [TestCase(397, true)] [TestCase(398, false)]
        public void EntryReservesAllThreeEventsBeforeTheFirstDebit(int events, bool expected)
        {
            var s = Empty(); FillEvents(s, events); Accept(s);
            string before = Json(s);
            Assert.That(DialogueRules.CanStart(s, Speaker, out _), Is.EqualTo(expected));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("active-case", 399, true)] [TestCase("active-case", 400, false)]
        [TestCase("active-finish", 400, true)]
        public void CompetingEventsCannotStealTheActivePathReservation(string shape, int count, bool accepted)
        {
            var s = Witness(shape); Accept(s); FillEvents(s, count);
            if (accepted) Accept(s); else Reject(s, "remaining event capacity");
        }

        [Test]
        public void NeutralOccasionAndSharedCycleCapacityCannotBeBoughtAgain()
        {
            var s = Witness("neutral-case"); Accept(s);
            Assert.That(DialogueRules.CanStart(s, Speaker, out string reason), Is.False);
            Assert.That(reason, Does.Contain("occasion"));
            s = Empty();
            for (int i = 0; i < 99; i++) s.storylines.Add(new StorylineState { id = "padding-" + i, templateId = "legacy", week = 1 });
            Accept(s);
            Assert.That(DialogueRules.CanStart(s, Speaker, out reason), Is.True, reason);
            s.storylines.Add(new StorylineState { id = "padding-99", templateId = "legacy", week = 1 }); Accept(s);
            Assert.That(DialogueRules.CanStart(s, Speaker, out reason), Is.False);
            Assert.That(reason, Does.Contain("cannot fit"));
        }

        [Test]
        public void DifferentEndedOccasionsKeepNonoverlappingRevisionsSequencesAndEntryTicks()
        {
            var s = Witness("neutral-case"); Accept(s);
            var second = Witness("neutral-case");
            const string other = "riley-johnson";
            var e = second.dialogue.episodes[0]; var c = second.storylines[0];
            e.id = c.id = "story-40"; e.startedSequence = 40; e.startedRevision += 3;
            e.npcId = other; e.occasionId = "week:1:campaign:" + other; e.entryEventId = "house-event-41";
            e.entry.revision += 3; e.entry.windowActionBefore = 1; e.entry.windowActionAfter = 2;
            e.time.revision += 3; e.time.sequence += 30;
            c.cast[0].contestantId = other;
            for (int i = 0; i < c.path.Count; i++)
            {
                c.path[i].logSequence += 30;
                var item = second.houseEvents[i];
                item.id = "house-event-" + (41 + 4 * i); item.cycleId = e.id;
                item.cast[0].contestantId = other; item.involvedIds[0] = other;
                e.responses[i].eventId = item.id; e.responses[i].speakerId = other; e.responses[i].revision += 3;
            }
            c.eventId = second.houseEvents.Last().id;
            s.dialogue.episodes.Add(e); s.storylines.Add(c); s.houseEvents.AddRange(second.houseEvents);
            s.events.AddRange(second.events.Where(row => row.kind == StoryLog.Outcome).Select(row =>
            { var copy = row.Clone(); copy.sequence += 30; copy.audienceIds[1] = other; return copy; }));
            s.revision = 30; s.windowActions[Windows.AfterVeto] = 2;
            Accept(s);
            e.entry.windowActionBefore = 0; e.entry.windowActionAfter = 1;
            Reject(s, "entry receipt");
            e.entry.windowActionBefore = 1; e.entry.windowActionAfter = 2;
            e.startedRevision = e.entry.revision = e.responses[0].revision = 20;
            Reject(s, "nonoverlapping chronology");
        }

        [Test]
        public void RuleOffAlsoRefusesAnUnfundedPilotCycleOrEventWithoutEpisodeRows()
        {
            var s = Witness("active-case"); Accept(s);
            s.dialogue.episodes.Clear(); s.dialogueRulesVersion = 0;
            Reject(s, "inert");
            s.storylines.Clear();
            Reject(s, "inert");
        }

        [TestCase("off")] [TestCase("unknown")] [TestCase("duplicate")]
        [TestCase("orphan")] [TestCase("future-family")] [TestCase("grants")]
        public void ThePilotCannotAdmitAnotherAuthorityOrAnUnownedGraph(string corruption)
        {
            var s = Witness("active-case"); Accept(s);
            switch (corruption)
            {
                case "off": s.dialogueRulesVersion = 0; break;
                case "unknown": s.dialogueRulesVersion = 2; break;
                case "duplicate": s.dialogue.episodes.Add(s.dialogue.episodes[0].Clone()); break;
                case "orphan": s.dialogue.episodes.Clear(); break;
                case "future-family": s.unifiedCommitments.Add(new UnifiedCommitmentState { conditionalVeto = new ConditionalVetoTermsState() }); break;
                case "grants": s.dialogue.grants.Add(new DialogueRoleGrantState()); break;
            }
            Reject(s, corruption == "off" ? "inert" : corruption == "unknown" ? "version"
                : corruption == "duplicate" ? "unique" : corruption == "orphan" ? "no dialogue owner"
                : corruption == "future-family" ? "not supported" : "ordinary-funded");
        }
    }
}
