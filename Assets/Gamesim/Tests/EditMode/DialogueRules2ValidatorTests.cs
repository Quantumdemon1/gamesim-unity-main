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
    /// Hand-built storage witnesses for dialogue rules 2's own validator (DIALOGUE-PLAN D2), as DialogueCampaignRulesTests
    /// builds rules 1's: states at schema 30, the schema that introduces rules 2, read by <see cref="DialogueRules.TryValidate(EpisodeState, out string)"/>
    /// alone, which holds a season's dialogue to its version and never to the live schema. NOT played seasons, not whole-state
    /// acceptance; the played conversation is DialogueRules2EngineTests'.
    /// </summary>
    public sealed class DialogueRules2ValidatorTests
    {
        private const string Speaker = "casey-wilson", Other = "taylor-kim", Template = DialogueCampaignWord.TemplateId;
        private const int Week = 2;
        private static ArcTemplate Tree => StoryCatalog.Find(Template);

        private sealed class SavedFields : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type type) =>
                type.GetFields(BindingFlags.Public | BindingFlags.Instance).Cast<MemberInfo>().ToList();
        }
        private static string Json(object value) => JsonConvert.SerializeObject(value, new JsonSerializerSettings { ContractResolver = new SavedFields() });

        private static EpisodeState Empty()
        {
            var s = ContentCatalog.Create(6511);
            ShippedRules.ApplyFresh(s);
            s.schemaVersion = DialogueRules.Rules2Schema;
            s.dialogueRulesVersion = DialogueRules.Rules2;
            s.week = Week;
            s.phase = EpisodePhase.Campaign; s.hohId = ContentCatalog.MayaId;
            s.nominees = new List<string> { s.playerId, Other };
            s.revision = 20; s.nextSequence = 100;
            s.windowActions[Windows.AfterVeto] = 1;
            return s;
        }

        private static HouseEventState Event(string beatId, int sequence, string cycleId)
        {
            var beat = Tree.Beat(beatId);
            return new HouseEventState
            {
                id = "house-event-" + sequence, kind = HouseEventKind.Story, week = Week, cycleId = cycleId,
                contentId = Template + ":" + beatId, title = beat.title, narrative = beat.summary,
                closesAnchor = beat.closes, surface = beat.surface, venue = beat.venue, lapseOptionId = beat.lapse,
                cast = new List<StoryRoleState> { new StoryRoleState { role = DialogueCampaignV1.SpeakerRole, contestantId = Speaker } },
                involvedIds = new List<string> { Speaker },
                choices = beat.options.Select(o => new HouseEventChoice { optionId = o.id, label = o.label, description = o.description,
                    risk = o.risk, lapse = o.id == beat.lapse, next = o.next }).ToList()
            };
        }

        /// <summary>
        /// A conversation with <see cref="Speaker"/> in week 2. <paramref name="reply"/> null leaves it open at the reply,
        /// Leave ends it there; <paramref name="finish"/> null leaves it open at the finish. Sequences as the engine mints
        /// them: the cycle 10, the open 11, the word 13, its line 14; the reply 15, the citation 17, its line 18; the finish
        /// 19, its line 21. Revisions 18, 19 and 20, one a step.
        /// </summary>
        private static EpisodeState Witness(string answer, string reply = null, string reaction = DialogueCampaignWord.Unmoved,
            string finish = null, bool lie = false, bool lapsed = false)
        {
            var s = Empty();
            bool told = answer == DialogueCampaignWord.Word || answer == DialogueCampaignWord.Against;
            string replyBeat = DialogueCampaignWord.ReplyBeat(answer, false, lie);
            var beats = new List<string> { DialogueCampaignWord.Open, replyBeat };
            var options = new List<string> { DialogueCampaignWord.OpenCase };
            if (reply != null) options.Add(reply);
            if (reply != null && reply != DialogueCampaignWord.Leave)
            {
                beats.Add(DialogueCampaignWord.FinishBeat(reaction));
                if (finish != null) options.Add(finish);
            }
            bool ended = options.Count == beats.Count;
            string ending = !ended ? null : lapsed ? "lapsed" : options.Last() == DialogueCampaignWord.Plea ? "answered" : "neutral";
            var episode = new DialogueEpisodeState
            {
                id = "story-10", npcId = Speaker, occasionKind = DialogueCampaignV1.Kind, occasionId = DialogueRules.Occasion(Week, Speaker),
                contentVersion = DialogueCampaignWord.ContentVersion, week = Week, window = Windows.AfterVeto, startedPhase = EpisodePhase.Campaign,
                startedRevision = 18, startedSequence = 10, entryEventId = "house-event-11",
                entry = new DialogueEntryReceiptState { kind = DialogueRules.ActionEntry, revision = 18, windowActionBefore = 0, windowActionAfter = 1 }
            };
            var cycle = new StorylineState
            {
                id = episode.id, templateId = Template, title = Tree.title, category = Tree.eyebrow, lane = StoryLanes.Game, week = Week,
                cast = new List<StoryRoleState> { new StoryRoleState { role = DialogueCampaignV1.SpeakerRole, contestantId = Speaker } },
                status = ended ? StorylineStatus.Completed : StorylineStatus.Active, endedWeek = ended ? Week : 0, endingId = ending
            };
            s.dialogue.episodes.Add(episode); s.storylines.Add(cycle);
            for (int i = 0; i < beats.Count; i++)
            {
                var item = Event(beats[i], 11 + 4 * i, episode.id);
                s.houseEvents.Add(item); cycle.eventId = item.id; cycle.beatId = beats[i];
                if (i >= options.Count) break;
                bool last = ended && i == options.Count - 1, lapse = last && lapsed;
                string option = lapse ? DialogueCampaignWord.Leave : options[i];
                var authored = Tree.Beat(beats[i]).Option(option);
                item.resolved = true; item.chosenIndex = item.choices.FindIndex(c => c.optionId == option);
                item.outcome = lapse ? DialogueCampaignV1.LapsedOutcome : authored.outcome;
                int log = 14 + 4 * i;
                cycle.path.Add(new StoryStepState { beatId = beats[i], optionId = option,
                    result = lapse ? StoryResults.Lapsed : StoryResults.Plain, week = Week, logSequence = log });
                s.events.Add(new EpisodeEvent { sequence = log, week = Week, phase = EpisodePhase.Campaign, kind = StoryLog.Outcome,
                    text = "A handbuilt rules-2 storage witness.", audienceIds = new List<string> { s.playerId, Speaker } });
                string suffix = lapse ? "occasion-closed" : i == 0 ? answer : authored.next == DialogueCampaignWord.ByReaction ? reaction
                    : option == DialogueCampaignWord.Plea ? "vote-" + LobbyResponse.Receptive
                    : option == DialogueCampaignWord.KeepOpen ? "no-promise-made" : "respectful-exit";
                episode.responses.Add(new DialogueResponseState { eventId = item.id, speakerId = Speaker, pathIndex = i,
                    revision = 18 + i, responseKey = Template + "/" + suffix });
                if (i == 1 && DialogueCampaignWord.EvidenceOf(option) != null)
                {
                    string kind = DialogueCampaignWord.EvidenceOf(option);
                    episode.evidence.Add(new DialogueEvidenceState { id = "dialogue-evidence-17", kind = kind, eventId = item.id,
                        pathIndex = 1, capturedRevision = 19,
                        statementId = kind == DialogueCampaignWord.StatementEvidence ? "dialogue-statement-13" : null,
                        memory = kind == DialogueCampaignWord.MemoryEvidence ? Lie(s) : null });
                }
                if (last)
                {
                    episode.time = new DialogueTimeReceiptState { revision = 18 + i, sequence = log, window = Windows.AfterVeto,
                        reason = lapsed ? "occasion-closed" : ending };
                    if (option == DialogueCampaignWord.Plea && !lapse)
                        s.lobbies.Add(new LobbyState { week = Week, phase = EpisodePhase.Campaign, deciderId = Speaker, ask = LobbyAsk.Vote,
                            subjectId = s.playerId, response = LobbyResponse.Receptive, influence = 0,
                            approach = episode.evidence.Count > 0 ? LobbyApproach.Strategic : LobbyApproach.Emotional });
                }
            }
            if (told)
            {
                string target = answer == DialogueCampaignWord.Against ? s.playerId : Other;
                s.ledger.claims.Add(new ClaimRow { week = Week, voterId = Speaker, targetId = target, source = ClaimSource.Told });
                episode.statement = new DialogueStatementState { id = "dialogue-statement-13", eventId = "house-event-11", pathIndex = 0,
                    speakerId = Speaker, targetId = target, answer = DialogueRules.VoteOut, conditionKind = DialogueRules.VoteClaim,
                    week = Week, revision = 18, sequence = 13, audienceIds = new List<string> { s.playerId } };
            }
            if (lapsed) { s.phase = EpisodePhase.Eviction; s.evictionStage = EvictionStage.Speeches; }
            return s;
        }

        private static MemoryState Lie(EpisodeState s) => new MemoryState
        {
            ownerId = s.playerId, subjectId = Speaker, week = 1, isPrivate = true,
            text = s.Find(Speaker).name + " told me in week 1 they would vote out " + s.Find(Other).name + ", and voted out "
                + s.Find(ContentCatalog.MayaId).name + "."
        };

        private static void Accept(EpisodeState s)
        {
            string before = Json(s);
            Assert.That(DialogueRules.TryValidate(s, out string error), Is.True, error);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static void Reject(EpisodeState s, string reason)
        {
            string before = Json(s);
            Assert.That(DialogueRules.TryValidate(s, out string error), Is.False, "Accepted: " + reason);
            Assert.That(error, Does.Contain(reason));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static IEnumerable<TestCaseData> Shapes()
        {
            string W = DialogueCampaignWord.Word, A = DialogueCampaignWord.Against, G = DialogueCampaignWord.Guarded, C = DialogueCampaignWord.Concern;
            yield return new TestCaseData(G, null, DialogueCampaignWord.Unmoved, null, false, false).SetName("OpenAtTheReply");
            yield return new TestCaseData(W, null, DialogueCampaignWord.Unmoved, null, false, false).SetName("OpenAtTheReplyWithTheirWord");
            yield return new TestCaseData(A, DialogueCampaignWord.Appeal, DialogueCampaignWord.Backfired, null, false, false).SetName("OpenAtTheFinish");
            yield return new TestCaseData(W, DialogueCampaignWord.CiteWord, DialogueCampaignWord.Warmed, DialogueCampaignWord.Plea, false, false).SetName("PleadedAfterHoldingThemToTheirWord");
            yield return new TestCaseData(C, DialogueCampaignWord.AskConcern, DialogueCampaignWord.Warmed, DialogueCampaignWord.Plea, false, false).SetName("PleadedAfterTheConcern");
            yield return new TestCaseData(G, DialogueCampaignWord.CiteLie, DialogueCampaignWord.Backfired, DialogueCampaignWord.KeepOpen, true, false).SetName("KeptOpenAfterTheLie");
            yield return new TestCaseData(G, DialogueCampaignWord.Press, DialogueCampaignWord.Unmoved, DialogueCampaignWord.Leave, false, false).SetName("LeftAtTheFinish");
            yield return new TestCaseData(C, DialogueCampaignWord.Leave, DialogueCampaignWord.Unmoved, null, false, false).SetName("LeftAtTheReply");
            yield return new TestCaseData(W, DialogueCampaignWord.CiteWord, DialogueCampaignWord.Unmoved, DialogueCampaignWord.Leave, false, true).SetName("LapsedAtTheFinish");
        }

        [TestCaseSource(nameof(Shapes))]
        public void EveryShapeTheTreeCanReachIsAccepted(string answer, string reply, string reaction, string finish, bool lie, bool lapsed) =>
            Accept(Witness(answer, reply, reaction, finish, lie, lapsed));

        [Test]
        public void AnEmptyRules2StoreIsAcceptedFromSchema30AndRefusedBelowIt()
        {
            var s = Empty(); Accept(s);
            s.schemaVersion = 29;
            Reject(s, "needs schema 30");
        }

        [TestCase("grant-entry", "window's action only")]
        [TestCase("no-tick", "window's action only")]
        [TestCase("rules-1-content", "Unknown dialogue content")]
        [TestCase("grants", "action-funded")]
        [TestCase("veto-occasions", "action-funded")]
        [TestCase("conditional", "Conditional dialogue obligations")]
        [TestCase("unknown-answer", "not one this conversation can give")]
        [TestCase("reply-not-the-answers", "must follow the voter's answer")]
        [TestCase("finish-not-the-reaction", "canonical definition")]
        [TestCase("unknown-reaction", "not one this conversation can give")]
        [TestCase("no-word", "exact copied vote claim")]
        [TestCase("word-for-a-deflection", "Only a voter who gave their word")]
        [TestCase("word-flipped", "exact copied vote claim")]
        [TestCase("word-not-on-the-ledger", "exact copied vote claim")]
        [TestCase("word-overheard", "exact copied vote claim")]
        [TestCase("word-to-everyone", "exact copied vote claim")]
        [TestCase("evidence-uncited", "Only a citing reply")]
        [TestCase("evidence-kind", "exact owned revision")]
        [TestCase("evidence-sequence", "exact owned revision")]
        [TestCase("plea-approach", "actual vote response")]
        [TestCase("time", "zero-time")]
        [TestCase("risk", "canonical definition")]
        [TestCase("too-long", "exact campaign cycle")]
        public void TheValidatorRefusesWhatTheTreeNeverWrites(string defect, string reason)
        {
            var s = defect == "word-for-a-deflection" || defect == "unknown-answer" || defect == "reply-not-the-answers"
                ? Witness(DialogueCampaignWord.Guarded)
                : defect == "evidence-uncited" ? Witness(DialogueCampaignWord.Concern, DialogueCampaignWord.AskConcern, DialogueCampaignWord.Warmed)
                : Witness(DialogueCampaignWord.Word, DialogueCampaignWord.CiteWord, DialogueCampaignWord.Warmed, DialogueCampaignWord.Plea);
            Accept(s);
            var episode = s.dialogue.episodes.Single();
            switch (defect)
            {
                case "grant-entry": episode.entry.kind = DialogueRules.GrantEntry; episode.entry.grantId = "dialogue-grant-1"; episode.entry.windowActionAfter = 0; break;
                case "no-tick": episode.entry.kind = "wallet"; break;
                case "rules-1-content": s.storylines.Add(new StorylineState { id = "story-90", templateId = DialogueCampaignV1.TemplateId }); break;
                case "grants": s.dialogue.grants.Add(new DialogueRoleGrantState()); break;
                case "veto-occasions": s.dialogue.vetoOccasions.Add(new DialogueVetoOccasionState()); break;
                case "conditional": s.unifiedCommitments.Add(new UnifiedCommitmentState { conditionalVeto = new ConditionalVetoTermsState() }); break;
                case "unknown-answer": episode.responses[0].responseKey = Template + "/lied"; break;
                case "reply-not-the-answers": episode.responses[0].responseKey = Template + "/" + DialogueCampaignWord.Concern; break;
                case "finish-not-the-reaction": episode.responses[1].responseKey = Template + "/" + DialogueCampaignWord.Backfired; break;
                case "unknown-reaction": episode.responses[1].responseKey = Template + "/delighted"; break;
                case "no-word": episode.statement = null; break;
                case "word-for-a-deflection":
                    episode.statement = new DialogueStatementState { id = "dialogue-statement-13", eventId = "house-event-11", speakerId = Speaker,
                        targetId = Other, answer = DialogueRules.VoteOut, conditionKind = DialogueRules.VoteClaim, week = Week, revision = 18,
                        sequence = 13, audienceIds = new List<string> { s.playerId } };
                    break;
                case "word-flipped": episode.statement.targetId = s.playerId; s.ledger.claims.Single().targetId = s.playerId; break;
                case "word-not-on-the-ledger": s.ledger.claims.Clear(); break;
                case "word-overheard": s.ledger.claims.Single().source = ClaimSource.Overheard; break;
                case "word-to-everyone": episode.statement.audienceIds.Add(Other); break;
                case "evidence-uncited":
                    episode.evidence.Add(new DialogueEvidenceState { id = "dialogue-evidence-17", kind = DialogueCampaignWord.StatementEvidence,
                        eventId = "house-event-15", pathIndex = 1, capturedRevision = 19 });
                    break;
                case "evidence-kind": episode.evidence[0].kind = DialogueCampaignWord.MemoryEvidence; break;
                case "evidence-sequence": episode.evidence[0].id = "dialogue-evidence-16"; break;
                case "plea-approach": s.lobbies.Single().approach = LobbyApproach.Emotional; break;
                case "time": episode.time.appliedDelta = 1; break;
                case "risk": s.houseEvents[1].choices.First(c => c.optionId == DialogueCampaignWord.Press).risk = HouseEventRisk.Low; break;
                case "too-long":
                    var cycle = s.storylines.Single(c => c.id == episode.id);
                    for (int i = 0; i < 3; i++) cycle.path.Add(cycle.path.Last());
                    break;
            }
            Reject(s, reason);
        }

        /// <summary>A memory citation keeps its copy, and the copy is the reveal's own line about this voter, from an earlier week.</summary>
        [TestCase("other-voter")] [TestCase("this-week")] [TestCase("not-the-reveals-line")] [TestCase("shared")]
        public void ALieIsCitedOnlyAsTheRevealsOwnLineAboutThisVoter(string defect)
        {
            var s = Witness(DialogueCampaignWord.Guarded, DialogueCampaignWord.CiteLie, DialogueCampaignWord.Backfired, DialogueCampaignWord.Plea, lie: true);
            Accept(s);
            var memory = s.dialogue.episodes.Single().evidence.Single().memory;
            switch (defect)
            {
                case "other-voter": memory.subjectId = Other; break;
                case "this-week": memory.week = Week; break;
                case "not-the-reveals-line": memory.text = "I overheard " + s.Find(Speaker).name + " in week 1."; break;
                case "shared": memory.isPrivate = false; break;
            }
            Reject(s, "exact owned revision");
        }

        [Test]
        public void ConversationsOfAnotherVersionNeverValidateAsThisOnes()
        {
            var word = Witness(DialogueCampaignWord.Guarded);
            word.dialogueRulesVersion = 1; word.schemaVersion = 29;
            Assert.That(DialogueRules.TryValidate(word, out string error), Is.False, "Rules 1 never holds rules 2's tree.");
            Assert.That(error, Is.EqualTo("Unknown dialogue content is not supported by this pilot."));
            // Rules 0's inert check is the merged one (b208d46d), which the frozen schema-29 reader copies: it names rules 1's
            // pairs. A rules-0 state that holds a rules-1 cycle is refused...
            var off = Witness(DialogueCampaignWord.Guarded);
            off.dialogueRulesVersion = 0; off.dialogue.episodes.Clear();
            foreach (var cycle in off.storylines.Where(c => c.templateId == Template)) cycle.templateId = DialogueCampaignV1.TemplateId;
            Assert.That(DialogueRules.TryValidate(off, out error), Is.False, "Rules 0 holds no rules-1 conversation.");
            Assert.That(error, Is.EqualTo("Disabled dialogue storage must remain complete and inert."));
            // ...and the check is not widened to rules 2's tree: no engine path writes one into a rules-0 season, and a wider
            // check would refuse at 29 what the frozen reader accepts, so the two would disagree after the bump (D2's review).
            var inert = Witness(DialogueCampaignWord.Guarded);
            inert.dialogueRulesVersion = 0; inert.dialogue.episodes.Clear();
            Assert.That(DialogueRules.TryValidate(inert, out error), Is.True, "Rules 0's check is as merged: " + error);
            var future = Witness(DialogueCampaignWord.Guarded);
            future.dialogueRulesVersion = DialogueRules.Version + 1;
            Assert.That(DialogueRules.TryValidate(future, out error), Is.False);
            Assert.That(error, Is.EqualTo("Unsupported dialogue rules version."));
        }
    }
}
