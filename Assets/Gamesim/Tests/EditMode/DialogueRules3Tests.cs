using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 3 (DIALOGUE-PLAN D3): the campaign conversation's four trees (the vote word, the memory, the pressure
    /// and the neutral tree) and the appeal to the veto holder, played on genuine seasons enrolled by the literal 3
    /// (<see cref="DialogueRules3Season"/>), and the rules 2 they leave as they were. Shared cases: the dotnet subset runs them.
    /// </summary>
    public sealed class DialogueRules3Tests
    {
        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command) => DialogueRules2Season.Accepted(engine, command);
        private static EpisodeCommand Choice(EpisodeState s, string option) => DialogueRules2Season.Choice(s, option);

        // ---------------------------------------------------------------- the content

        [Test]
        public void TheTwoTemplatesAreClosedFreshAndWithinTheShapeBounds()
        {
            foreach (string id in new[] { DialogueCampaignTrees.TemplateId, DialogueHolderAppeal.TemplateId })
            {
                var first = DialogueRules.ContentFor(3, id, 1);
                Assert.That(first, Is.Not.Null, id);
                Assert.That(ReferenceEquals(first, DialogueRules.ContentFor(3, id, 1)), Is.False, "A fresh definition on every read.");
                Assert.That(DialogueRules.ContentFor(2, id, 1), Is.Null, "Rules 2 never plays rules 3's content.");
                Assert.That(DialogueRules.ContentFor(3, id, 2), Is.Null, "An unknown content version.");
                Assert.That(DialogueRules.IsDialogueTemplate(id), Is.True);
                Assert.That(StoryCatalog.Find(id)?.id, Is.EqualTo(id));
                Assert.That(StoryCatalog.All.Any(t => t.id == id), Is.False, "Never an ambient-pool arc.");
                var beats = first.beats.ToDictionary(b => b.id, StringComparer.Ordinal);
                foreach (var beat in first.beats)
                {
                    Assert.That(beat.options.Length, Is.InRange(2, 4), id + ":" + beat.id + ": at most four options a node.");
                    Assert.That(beat.lapse, Is.EqualTo(DialogueCampaignV1.Leave));
                    Assert.That(beat.options.Last().id, Is.EqualTo(DialogueCampaignV1.Leave), id + ":" + beat.id + ": every node's last option is the neutral exit.");
                    Assert.That(beat.options.Select(o => o.id).Distinct().Count(), Is.EqualTo(beat.options.Length));
                }
                // Every path: at most five steps, at most three consequential choices, and every node reached.
                var reached = new HashSet<string>(StringComparer.Ordinal);
                int longest = 0;
                void Walk(string beatId, int depth, string answer)
                {
                    reached.Add(beatId);
                    longest = Math.Max(longest, depth);
                    Assert.That(depth, Is.LessThanOrEqualTo(4), id + ": a path of at most four nodes (five steps with the lapse).");
                    foreach (var option in beats[beatId].options)
                    {
                        if (option.next.StartsWith("end:", StringComparison.Ordinal)) continue;
                        IEnumerable<string> next = option.next == DialogueCampaignTrees.ByFacts
                            ? new[] { DialogueCampaignTrees.Approach, DialogueCampaignTrees.ApproachHistory }
                            : option.next == DialogueCampaignTrees.ByAnswer
                                ? (id == DialogueHolderAppeal.TemplateId ? DialogueHolderAppeal.Answers.Select(DialogueHolderAppeal.ReplyBeat)
                                    : DialogueCampaignTrees.AnswersOf(option.id).Select(DialogueCampaignTrees.ReplyBeat))
                            : option.next == DialogueCampaignTrees.ByReaction ? DialogueCampaignTrees.Reactions.Select(DialogueCampaignTrees.FinishBeat)
                            : new[] { option.next };
                        foreach (string n in next)
                        {
                            Assert.That(beats.ContainsKey(n), Is.True, id + ": " + beatId + " -> " + n);
                            Walk(n, depth + 1, answer);
                        }
                    }
                }
                Walk("open", 1, null);
                Assert.That(reached, Is.EquivalentTo(beats.Keys), id + ": every node is reachable.");
                Assert.That(longest, Is.EqualTo(id == DialogueHolderAppeal.TemplateId ? 3 : 4));
            }
            Assert.That(DialogueRules.ContentFor(3, DialogueCampaignWord.TemplateId, 1), Is.Null, "Rules 3 opens its own trees.");
            Assert.That(DialogueRules.EntryTemplateFor(3), Is.EqualTo(DialogueCampaignTrees.TemplateId));
            Assert.That(DialogueRules.EntryTemplateFor(2), Is.EqualTo(DialogueCampaignWord.TemplateId), "Rules 2 keeps its one tree.");
            Assert.That(DialogueCampaignTrees.AllAnswers().Distinct().Count(), Is.EqualTo(DialogueCampaignTrees.AllAnswers().Count()),
                "Each answer names its own reply node.");
        }

        // ---------------------------------------------------------------- the campaign trees

        /// <summary>
        /// The campaign conversation opens on the approach node for one action, and each tree the node offers draws the
        /// voter's answer into its own reply node, a reaction into a finish, and ends in the ordinary plea. Opening, the
        /// answer and the reaction draw nothing from the season's stream; the one relationship change is between the player
        /// and that voter alone; the state is whole after every step.
        /// </summary>
        [Test]
        public void EachTreeOnTheApproachReachesItsReplyAndEndsInTheOrdinaryPlea()
        {
            var stop = DialogueRules3Season.FindCampaign();
            var before = stop.State;
            string voter = stop.People[0];
            Assert.That(DialogueRules.IsConversationPlea(before, voter, LobbyAsk.Vote, before.playerId), Is.True);
            var opened = Accepted(new EpisodeEngine(before.Clone()), DialogueRules3Season.Entry(before, voter, DialogueRules3Season.Trees));
            Assert.That(EpisodeEngine.SocialActionsSpent(opened), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1), "The entry costs one action.");
            Assert.That(opened.randomState, Is.EqualTo(before.randomState), "Opening draws nothing from the season's stream.");
            bool history = DialogueRules.HistoryFact(before, voter, before.week) != null;
            Assert.That(DialogueRules3Season.Beat(opened), Is.EqualTo(history ? DialogueCampaignTrees.ApproachHistory : DialogueCampaignTrees.Approach));
            Assert.That(DialogueRules3Season.Suffix(DialogueRules3Season.Open(opened).responses[0]), Is.EqualTo(DialogueCampaignTrees.Listening));
            var approaches = DialogueRules3Season.Options(opened);
            Assert.That(approaches, Is.EqualTo(new[] { DialogueCampaignTrees.AskStand,
                history ? DialogueCampaignTrees.BringUp : DialogueCampaignTrees.KeepEasy, DialogueCampaignTrees.Lean, DialogueCampaignTrees.Leave }));
            foreach (string approach in approaches.Where(a => a != DialogueCampaignTrees.Leave))
            {
                var engine = new EpisodeEngine(opened.Clone());
                var answered = Accepted(engine, Choice(opened, approach));
                var episode = DialogueRules3Season.Open(answered);
                string answer = DialogueRules3Season.Suffix(episode.responses[1]);
                Assert.That(DialogueCampaignTrees.AnswersOf(approach), Does.Contain(answer), approach);
                Assert.That(DialogueRules3Season.Beat(answered), Is.EqualTo(DialogueCampaignTrees.ReplyBeat(answer)), approach);
                Assert.That(answered.randomState, Is.EqualTo(before.randomState), approach + ": the answer is keyed.");
                Assert.That(EpisodeEngine.SocialActionsSpent(answered), Is.EqualTo(EpisodeEngine.SocialActionsSpent(opened)), "A reply is free.");
                Assert.That(DialogueRules3Season.Moved(opened, answered), Is.Empty, approach + ": the answer moves nobody.");
                string reply = DialogueRules3Season.Options(answered).First(o => DialogueCampaignTrees.EvidenceOf(o) == null && o != DialogueCampaignTrees.Leave);
                var reacted = Accepted(engine, Choice(answered, reply));
                string reaction = DialogueRules3Season.Suffix(DialogueRules3Season.Open(reacted).responses[2]);
                Assert.That(DialogueRules3Season.Beat(reacted), Is.EqualTo(DialogueCampaignTrees.FinishBeat(reaction)));
                Assert.That(reacted.randomState, Is.EqualTo(before.randomState), approach + ": the reaction is keyed.");
                var moved = DialogueRules3Season.Moved(answered, reacted);
                if (reaction == DialogueCampaignTrees.Unmoved) Assert.That(moved, Is.Empty, approach + ": an unmoved voter moves nothing.");
                else Assert.That(moved, Is.SubsetOf(new[] { before.playerId + ">" + voter, voter + ">" + before.playerId }).And.Not.Empty,
                    approach + ": only the player and the voter, never a pair of NPCs.");
                var pleaded = Accepted(engine, Choice(reacted, DialogueCampaignTrees.Plea));
                var row = pleaded.lobbies.Single(l => l.week == pleaded.week && l.phase == EpisodePhase.Campaign && l.deciderId == voter);
                Assert.That((row.ask, row.subjectId, row.approach), Is.EqualTo((LobbyAsk.Vote, pleaded.playerId, LobbyApproach.Emotional)),
                    approach + ": the ordinary Emotional plea, as nothing was cited.");
                var ended = pleaded.dialogue.episodes.Single(e => e.id == episode.id);
                Assert.That(ended.time?.reason, Is.EqualTo("answered"));
                Assert.That(DialogueRules.Active(pleaded), Is.Null);
                Assert.That(EpisodeEngine.SocialActionsSpent(pleaded), Is.EqualTo(EpisodeEngine.SocialActionsSpent(opened)), "The final plea costs no further action.");
            }
            // The neutral exit at the approach ends it, drawing nothing and moving nobody.
            var left = Accepted(new EpisodeEngine(opened.Clone()), Choice(opened, DialogueCampaignTrees.Leave));
            Assert.That(DialogueRules.Active(left), Is.Null);
            Assert.That(left.randomState, Is.EqualTo(before.randomState));
            Assert.That(DialogueRules3Season.Moved(opened, left), Is.Empty);
        }

        /// <summary>
        /// The vote-word tree inside rules 3: a voter who told the player where their vote is gives that word, copied as the
        /// conversation's statement at the approach; holding them to it cites it, and the plea after a citation is the
        /// Strategic plea.
        /// </summary>
        [Test]
        public void AskingWhereTheyStandCopiesTheWordTheyGaveAndCitingItMakesTheStrategicPlea()
        {
            var stop = DialogueRules3Season.FindCampaign();
            var engine = new EpisodeEngine(stop.State.Clone());
            string voter = null;
            foreach (string id in stop.People)
            {
                var asked = Accepted(engine, DialogueRules2Season.AskVote(engine.Snapshot, id));
                var claim = asked.ledger.claims.LastOrDefault(k => k != null && k.week == asked.week && k.voterId == id && k.source == ClaimSource.Told);
                if (claim != null && claim.targetId != asked.playerId) { voter = id; break; }
            }
            Assert.That(voter, Is.Not.Null, "A voter of this stop gave the player a word naming somebody else.");
            var s = engine.Snapshot;
            var opened = Accepted(engine, DialogueRules3Season.Entry(s, voter, DialogueRules3Season.Trees));
            var answered = Accepted(engine, Choice(opened, DialogueCampaignTrees.AskStand));
            var episode = DialogueRules3Season.Open(answered);
            Assert.That(DialogueRules3Season.Beat(answered), Is.EqualTo(DialogueCampaignTrees.ReplyBeat(DialogueCampaignTrees.Word)));
            Assert.That(episode.statement, Is.Not.Null);
            Assert.That((episode.statement.speakerId, episode.statement.week, episode.statement.pathIndex),
                Is.EqualTo((voter, answered.week, 1)), "The word, copied at the approach.");
            var cycle = DialogueRules.Cycle(answered, episode);
            Assert.That(DialogueRules.TryReadStatement(answered, cycle.eventId, out var view), Is.True, "The HUD reads the word on the reply.");
            Assert.That(view.SpeakerId, Is.EqualTo(voter));
            var reacted = Accepted(engine, Choice(answered, DialogueCampaignTrees.CiteWord));
            Assert.That(DialogueRules3Season.Open(reacted).evidence.Single().statementId, Is.EqualTo(episode.statement.id));
            var pleaded = Accepted(engine, Choice(reacted, DialogueCampaignTrees.Plea));
            Assert.That(pleaded.lobbies.Single(l => l.week == pleaded.week && l.phase == EpisodePhase.Campaign && l.deciderId == voter).approach,
                Is.EqualTo(LobbyApproach.Strategic), "After a citation the plea is the Strategic plea's roll.");
        }

        /// <summary>
        /// The memory tree: a vote the voter told the player about in an earlier week, which the reveal judged kept, opens
        /// the approach's history seat in place of the neutral tree. Bringing it up answers with that history - no coin -
        /// copies the old claim as the statement, and counting on their word cites it. The claim is planted on the ledger
        /// by hand (the walk asks no votes); everything after it is the engine's.
        /// </summary>
        [Test]
        public void AKeptWordFromAnEarlierWeekOpensTheMemoryTreeAndIsCited()
        {
            var stop = DialogueRules3Season.FindCampaign(fromWeek: 2);
            var s = stop.State.Clone();
            string voter = stop.People[0];
            string target = s.Active.First(c => !c.isPlayer && c.id != voter).id;
            s.ledger.claims.Add(new ClaimRow { week = s.week - 1, voterId = voter, targetId = target, source = ClaimSource.Told, status = ClaimStatus.Kept });
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            Assert.That(DialogueRules.HistoryFact(s, voter, s.week)?.status, Is.EqualTo(ClaimStatus.Kept));
            var engine = new EpisodeEngine(s);
            var opened = Accepted(engine, DialogueRules3Season.Entry(s, voter, DialogueRules3Season.Trees));
            Assert.That(DialogueRules3Season.Beat(opened), Is.EqualTo(DialogueCampaignTrees.ApproachHistory));
            Assert.That(DialogueRules3Season.Options(opened), Does.Contain(DialogueCampaignTrees.BringUp).And.Not.Contain(DialogueCampaignTrees.KeepEasy));
            var answered = Accepted(engine, Choice(opened, DialogueCampaignTrees.BringUp));
            var episode = DialogueRules3Season.Open(answered);
            Assert.That(DialogueRules3Season.Beat(answered), Is.EqualTo(DialogueCampaignTrees.ReplyBeat(DialogueCampaignTrees.Kept)));
            Assert.That(episode.statement, Is.Not.Null, "The kept claim, copied.");
            Assert.That((episode.statement.targetId, episode.statement.week), Is.EqualTo((target, s.week - 1)), "The kept claim, copied.");
            Assert.That(answered.randomState, Is.EqualTo(s.randomState));
            var reacted = Accepted(engine, Choice(answered, DialogueCampaignTrees.CountOnWord));
            Assert.That(DialogueRules3Season.Open(reacted).evidence.Single().kind, Is.EqualTo(DialogueCampaignTrees.StatementEvidence));
            var pleaded = Accepted(engine, Choice(reacted, DialogueCampaignTrees.Plea));
            Assert.That(pleaded.lobbies.Single(l => l.week == pleaded.week && l.phase == EpisodePhase.Campaign && l.deciderId == voter).approach,
                Is.EqualTo(LobbyApproach.Strategic));
        }

        // ---------------------------------------------------------------- the holder appeal

        /// <summary>
        /// The appeal to the veto holder: at the veto meeting, a nominee's Emotional plea to the holder opens a conversation
        /// for the window's one action; the holder answers on a keyed coin, reacts once, and the appeal ends in the ordinary
        /// Emotional save plea, which the holder's decision then reads as it reads any plea.
        /// </summary>
        [Test]
        public void AnAppealToTheHolderEndsInTheOrdinarySavePleaAndTheDecisionFollows()
        {
            var stop = DialogueRules3Season.FindVetoMeeting();
            var before = stop.State;
            string holder = stop.People[0];
            Assert.That(DialogueRules.IsConversationPlea(before, holder, LobbyAsk.Save, before.playerId), Is.True);
            Assert.That(DialogueRules.IsConversationPlea(before, holder, LobbyAsk.Keep, null), Is.False);
            var engine = new EpisodeEngine(before.Clone());
            var opened = Accepted(engine, DialogueRules3Season.Entry(before, holder, DialogueRules3Season.Holder));
            var episode = DialogueRules3Season.Open(opened);
            Assert.That((episode.occasionKind, episode.occasionId, episode.window, episode.startedPhase),
                Is.EqualTo((DialogueHolderAppeal.Kind, DialogueRules.HolderOccasion(before.week, holder), Windows.AfterNominations, EpisodePhase.VetoMeeting)));
            string answer = DialogueRules3Season.Suffix(episode.responses[0]);
            Assert.That(DialogueHolderAppeal.Answers, Does.Contain(answer));
            Assert.That(DialogueRules3Season.Beat(opened), Is.EqualTo(DialogueHolderAppeal.ReplyBeat(answer)));
            var reacted = Accepted(engine, Choice(opened, DialogueHolderAppeal.Appeal));
            var moved = DialogueRules3Season.Moved(opened, reacted);
            Assert.That(moved, Is.SubsetOf(new[] { before.playerId + ">" + holder, holder + ">" + before.playerId }), "Only the player and the holder.");
            var pleaded = Accepted(engine, Choice(reacted, DialogueHolderAppeal.AskToUse));
            var row = pleaded.lobbies.Single(l => l.week == pleaded.week && l.phase == EpisodePhase.VetoMeeting && l.deciderId == holder);
            Assert.That((row.ask, row.subjectId, row.approach), Is.EqualTo((LobbyAsk.Save, pleaded.playerId, LobbyApproach.Emotional)));
            Assert.That(DialogueRules3Season.Suffix(pleaded.dialogue.episodes.Single(e => e.id == episode.id).responses.Last()), Is.EqualTo("save-" + row.response));
            Assert.That(DialogueRules.Active(pleaded), Is.Null);
            var decided = Accepted(engine, EpisodeEngineTests.NextCommand(pleaded));
            Assert.That(decided.vetoResolved, Is.True, "The decision follows, as in any season.");
        }

        /// <summary>
        /// An appeal left open lapses at the decision through its own exit, drawing nothing of its own: the decision's
        /// draws are the same as the same meeting's with the appeal left before the decision.
        /// </summary>
        [Test]
        public void AnAppealLeftOpenLapsesAtTheDecisionAndTheDecisionDrawsAsIfItHadBeenLeft()
        {
            var stop = DialogueRules3Season.FindVetoMeeting();
            string holder = stop.People[0];
            var engine = new EpisodeEngine(stop.State.Clone());
            var opened = Accepted(engine, DialogueRules3Season.Entry(stop.State, holder, DialogueRules3Season.Holder));
            var twin = new EpisodeEngine(opened.Clone());
            var left = Accepted(twin, Choice(opened, DialogueHolderAppeal.Leave));
            var lapsed = Accepted(engine, EpisodeEngineTests.NextCommand(opened));
            var episode = lapsed.dialogue.episodes.Single();
            var cycle = DialogueRules.Cycle(lapsed, episode);
            Assert.That(episode.time, Is.Not.Null);
            Assert.That((cycle.status, cycle.endingId, episode.time.reason), Is.EqualTo((StorylineStatus.Completed, "lapsed", "occasion-closed")));
            Assert.That(lapsed.vetoResolved, Is.True);
            var decided = Accepted(twin, EpisodeEngineTests.NextCommand(left));
            Assert.That(lapsed.randomState, Is.EqualTo(decided.randomState), "The lapse draws nothing: the decision rolls as it would have.");
            Assert.That(lapsed.nominees, Is.EqualTo(decided.nominees), "And decides the same.");
        }

        /// <summary>
        /// Any other plea to the holder while the appeal is open is the ordinary one-shot plea, and closes the appeal unmade;
        /// once the appeal is used, the desperate save plea is the one-shot plea, with the reason (owner, 2026-10-25: "Never
        /// lose the plea").
        /// </summary>
        [Test]
        public void APleaToTheHolderOutsideTheAppealClosesItAndAUsedAppealLeavesTheOneShotPlea()
        {
            var stop = DialogueRules3Season.FindVetoMeeting();
            string holder = stop.People[0];
            var engine = new EpisodeEngine(stop.State.Clone());
            var opened = Accepted(engine, DialogueRules3Season.Entry(stop.State, holder, DialogueRules3Season.Holder));
            Assert.That(DialogueRules.PleaInOpenConversation(opened, holder), Is.True);
            var plea = DialogueRules2Season.Command(opened, EpisodeCommandKind.Lobby);
            plea.targetId = holder; plea.secondTargetId = opened.playerId; plea.text = LobbyAsk.Encode(LobbyAsk.Save, LobbyApproach.Strategic);
            if (DialogueRules2Season.Remaining(opened) > 0)
            {
                var closed = Accepted(new EpisodeEngine(opened.Clone()), plea);
                Assert.That(DialogueRules.Cycle(closed, closed.dialogue.episodes.Single()).endingId, Is.EqualTo("lapsed"));
                Assert.That(closed.lobbies.Single(l => l.deciderId == holder && l.phase == EpisodePhase.VetoMeeting).approach, Is.EqualTo(LobbyApproach.Strategic));
            }
            var left = Accepted(engine, Choice(opened, DialogueHolderAppeal.Leave));
            Assert.That(DialogueRules.CanStart(left, holder, out string why), Is.False);
            if (DialogueRules2Season.Remaining(left) > 0)
            {
                Assert.That(why, Is.EqualTo("This veto occasion has already been used."));
                Assert.That(DialogueRules.PleaWithoutConversation(left, holder, out string reason), Is.True);
                Assert.That(reason, Is.EqualTo(why));
            }
        }

        // ---------------------------------------------------------------- old seasons and the validator

        /// <summary>
        /// Rules 2 stays as built: its season opens the vote-word tree, its plea panel offers no holder appeal, and a
        /// rules-3 conversation copied into it is refused as unknown content.
        /// </summary>
        [Test]
        public void ARulesTwoSeasonKeepsItsOneTreeAndHasNoHolderAppeal()
        {
            var stop = DialogueRules3Season.FindVetoMeeting();
            var rules2 = stop.State.Clone();
            rules2.dialogueRulesVersion = DialogueRules.Rules2;
            Assert.That(EpisodeValidation.TryValidate(rules2, out string error), Is.True, error);
            Assert.That(DialogueRules.IsConversationPlea(rules2, rules2.vetoHolderId, LobbyAsk.Save, rules2.playerId), Is.False);
            Assert.That(DialogueRules.HolderAppealOn(rules2), Is.False);
            Assert.That(DialogueRules.PleaWithoutConversation(rules2, rules2.vetoHolderId, out _), Is.False, "No veto-meeting fallback below rules 3.");
            var opened = Accepted(new EpisodeEngine(stop.State.Clone()), DialogueRules3Season.Entry(stop.State, stop.People[0], DialogueRules3Season.Holder));
            opened.dialogueRulesVersion = DialogueRules.Rules2;
            Assert.That(DialogueRules.TryValidate(opened, out error), Is.False);
            Assert.That(error, Is.EqualTo("Unknown dialogue content is not supported by this version."));
        }

        [TestCase("response-key")] [TestCase("occasion-kind")] [TestCase("window")] [TestCase("statement")] [TestCase("answer-of-another-tree")]
        public void TheValidatorRefusesARules3ConversationThatStrays(string defect)
        {
            var stop = DialogueRules3Season.FindCampaign();
            string voter = stop.People[0];
            var engine = new EpisodeEngine(stop.State.Clone());
            var opened = Accepted(engine, DialogueRules3Season.Entry(stop.State, voter, DialogueRules3Season.Trees));
            var answered = Accepted(engine, Choice(opened, DialogueCampaignTrees.Lean));
            var bad = answered.Clone();
            var episode = bad.dialogue.episodes.Single();
            switch (defect)
            {
                case "response-key": episode.responses[0].responseKey += "-not"; break;
                case "occasion-kind": episode.occasionKind = DialogueHolderAppeal.Kind; break;
                case "window": episode.window = Windows.AfterNominations; break;
                case "statement":
                    episode.statement = new DialogueStatementState { id = "dialogue-statement-1", speakerId = voter, targetId = bad.playerId,
                        conditionKind = DialogueRules.VoteClaim, answer = DialogueRules.VoteOut, week = bad.week,
                        audienceIds = new List<string> { bad.playerId } };
                    break;
                default: episode.responses[1].responseKey = DialogueCampaignTrees.TemplateId + "/" + DialogueCampaignTrees.Easy; break;
            }
            Assert.That(DialogueRules.TryValidate(answered, out string error), Is.True, error);
            Assert.That(DialogueRules.TryValidate(bad, out error), Is.False, defect);
            Assert.That(EpisodeValidation.TryValidate(bad, out _), Is.False, defect);
        }
    }
}
