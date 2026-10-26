using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R2 = Gamesim.Tests.EditMode.DialogueRules2Season;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 2's vote-word conversation played through the public commands the HUD builds (DIALOGUE-PLAN D2):
    /// the paid entry, the voter's keyed answer, the one reaction that moves how the two of them see each other, the
    /// final plea, the exits, the lapse, the free-reply guards and a greedy scripted player hunting for loops.
    ///
    /// <para>Every test begins with <see cref="DialogueRules2Season.HeldBelowItsSchema"/>: rules 2 is defined from schema
    /// 30 only, so below it the test asserts the refusal and ends; from the pooled bump on it plays the conversation.</para>
    /// </summary>
    public sealed class DialogueRules2EngineTests
    {
        private static readonly string[] Unmoved = { "lobbies", "memories", "votes", "deals", "promises", "unifiedCommitments",
            "randomState", "windowActions", "outOfPhaseSocialActions", "socialActions" };

        private R2.Campaign campaign;

        private R2.Campaign Found => campaign ?? (campaign = R2.FindCampaign());

        private EpisodeEngine Fresh(out EpisodeState s)
        {
            s = Found.State.Clone();
            return new EpisodeEngine(s);
        }

        [Test]
        public void TheEntryCostsOneActionAndTheVoterAnswersOnTheOccasionsOwnCoin()
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            var entered = R2.Accepted(engine, R2.Entry(s, voter));
            var episode = R2.Conversation(entered, voter);
            Assert.That(R2.Remaining(entered), Is.EqualTo(R2.Remaining(s) - 1), "The entry is the window's one action.");
            Assert.That(entered.randomState, Is.EqualTo(s.randomState), "The voter's answer draws nothing from the season's stream.");
            Assert.That(episode.entry.kind, Is.EqualTo(DialogueRules.ActionEntry));
            Assert.That(episode.contentVersion, Is.EqualTo(DialogueCampaignWord.ContentVersion));
            Assert.That(episode.responses, Has.Count.EqualTo(1));
            string answer = R2.Suffix(episode.responses[0]);
            Assert.That(new[] { DialogueCampaignWord.Guarded, DialogueCampaignWord.Concern }, Does.Contain(answer),
                "Never asked where their vote is this week, the voter has no word to give.");
            Assert.That(episode.statement, Is.Null);
            var cycle = R2.Cycle(entered, episode);
            Assert.That(DialogueCampaignWord.AnswerOf(cycle.beatId), Is.EqualTo(answer), "The reply node is the answer's.");
            Assert.That(entered.houseEvents.Single(e => e.id == cycle.eventId).choices.Count, Is.LessThanOrEqualTo(4));

            // The coin is the occasion's: something else first (a talk with another voter, a main-stream roll) moves the
            // season's stream, never this voter's answer.
            var other = new EpisodeEngine(s);
            var talk = R2.Command(s, EpisodeCommandKind.Talk);
            talk.targetId = Found.Voters[1];
            var talked = R2.Accepted(other, talk);
            Assert.That(talked.randomState, Is.Not.EqualTo(s.randomState), "The control really drew from the season's stream.");
            var later = R2.Accepted(other, R2.Entry(talked, voter));
            Assert.That(R2.Suffix(R2.Conversation(later, voter).responses[0]), Is.EqualTo(answer));
        }

        [Test]
        public void AVoterWhoToldYouWhereTheirVoteIsGivesTheirWordAndItIsCopied()
        {
            if (R2.HeldBelowItsSchema()) return;
            foreach (string voter in Found.Voters)
            {
                var engine = Fresh(out var s);
                var asked = R2.Accepted(engine, R2.AskVote(s, voter));
                var claim = asked.ledger.claims.LastOrDefault(k => k.week == asked.week && k.voterId == voter && k.source == ClaimSource.Told);
                if (claim == null) continue; // A strategist deflected; nothing was told, so there is nothing to copy.
                var entered = R2.Accepted(engine, R2.Entry(asked, voter));
                var episode = R2.Conversation(entered, voter);
                string answer = R2.Suffix(episode.responses[0]);
                Assert.That(answer, Is.EqualTo(claim.targetId == entered.playerId ? DialogueCampaignWord.Against : DialogueCampaignWord.Word));
                var word = episode.statement;
                Assert.That(word, Is.Not.Null, "Their word is the conversation's one statement.");
                Assert.That(word.speakerId, Is.EqualTo(voter));
                Assert.That(word.targetId, Is.EqualTo(claim.targetId));
                Assert.That(word.week, Is.EqualTo(claim.week));
                Assert.That(word.conditionKind, Is.EqualTo(DialogueRules.VoteClaim));
                Assert.That(word.answer, Is.EqualTo(DialogueRules.VoteOut));
                Assert.That(word.audienceIds, Is.EqualTo(new[] { entered.playerId }), "Said to the player alone.");
                var reply = entered.houseEvents.Single(e => e.id == R2.Cycle(entered, episode).eventId);
                Assert.That(reply.choices.Any(c => c.optionId == DialogueCampaignWord.CiteWord), Is.EqualTo(answer == DialogueCampaignWord.Word),
                    "Only a word to keep the player can be held to.");
                return;
            }
            Assert.Fail("No voter of the witness campaign told the player where their vote is.");
        }

        [Test]
        public void EachReplyMovesHowTheyAndThePlayerSeeEachOtherAtMostOnceAndDrawsNothing()
        {
            if (R2.HeldBelowItsSchema()) return;
            var seen = new HashSet<string>();
            foreach (string voter in Found.Voters)
                foreach (bool ask in new[] { false, true })
                {
                    var engine = Fresh(out var s);
                    if (ask) s = R2.Accepted(engine, R2.AskVote(s, voter));
                    var entered = R2.Accepted(engine, R2.Entry(s, voter));
                    var episode = R2.Conversation(entered, voter);
                    var reply = entered.houseEvents.Single(e => e.id == R2.Cycle(entered, episode).eventId);
                    foreach (var option in reply.choices.Where(c => c.optionId != DialogueCampaignWord.Leave))
                    {
                        var branch = new EpisodeEngine(entered);
                        var before = R2.Scores(entered);
                        var answered = R2.Accepted(branch, R2.Choice(entered, option.optionId));
                        var after = R2.Conversation(answered, voter);
                        string reaction = R2.Suffix(after.responses[1]);
                        seen.Add(reaction);
                        Assert.That(DialogueCampaignWord.Reactions, Does.Contain(reaction));
                        Assert.That(R2.Cycle(answered, after).beatId, Is.EqualTo(DialogueCampaignWord.FinishBeat(reaction)));
                        Assert.That(R2.Remaining(answered), Is.EqualTo(R2.Remaining(entered)), "A reply is free.");
                        JObject was = R2.Tree(entered), now = R2.Tree(answered);
                        foreach (string field in Unmoved)
                            Assert.That(JToken.DeepEquals(now[field], was[field]), Is.True, option.optionId + ": " + field + " moved.");
                        var scores = R2.Scores(answered);
                        var moved = scores.Keys.Union(before.Keys)
                            .Where(k => (scores.TryGetValue(k, out double a) ? a : 0) != (before.TryGetValue(k, out double b) ? b : 0)).ToArray();
                        string mine = entered.playerId + ">" + voter, theirs = voter + ">" + entered.playerId;
                        Assert.That(moved, Is.SubsetOf(new[] { mine, theirs }), "Only the player and this voter: never a pair of NPCs.");
                        if (reaction == DialogueCampaignWord.Unmoved) Assert.That(moved, Is.Empty, "An unmoved voter moves nothing.");
                        else
                        {
                            // The ±3 is the player's act toward the voter, through the engine's reducer (§5.4): the player's
                            // view of the voter moves by it, adjusted as any player act's is, and how the voter feels about
                            // the player (§5.3's view) by its reciprocal, 0.8 to 1.2 times it, drawn on the conversation's key.
                            int delta = DialogueCampaignWord.Delta(reaction);
                            double was0 = before.TryGetValue(mine, out double m) ? m : 0, their0 = before.TryGetValue(theirs, out double t) ? t : 0;
                            double roll = StoryRandom.Unit(entered, DialogueRules.Key(after.occasionId, 1) + ":change#0");
                            Assert.That(scores[mine], Is.EqualTo(WebRules.ClampScore(was0
                                + WebRules.RelationshipDelta(delta, entered.Find(entered.playerId).stats.social, true))),
                                option.optionId + " " + reaction + ": the player's view of the voter.");
                            Assert.That(scores[theirs], Is.EqualTo(WebRules.ClampScore(their0 + WebRules.ReciprocalDelta(delta, roll))),
                                option.optionId + " " + reaction + ": the voter's view of the player, on the keyed reciprocal draw.");
                            Assert.That(Math.Sign(scores[theirs] - their0), Is.EqualTo(Math.Sign(delta)), "The voter's view moves by the reaction's sign.");
                            Assert.That(Math.Sign(scores[mine] - was0), Is.EqualTo(Math.Sign(delta)));
                        }
                        string cited = DialogueCampaignWord.EvidenceOf(option.optionId);
                        Assert.That(after.evidence.Select(e => e.kind), Is.EqualTo(cited == null ? new string[0] : new[] { cited }));
                    }
                }
            TestContext.Out.WriteLine("Reactions seen: " + string.Join(", ", seen.OrderBy(x => x)) + ".");
        }

        [TestCase(false)] [TestCase(true)]
        public void TheFinalPleaIsTheOrdinaryPleaOnceAndCostsNothingMore(bool cite)
        {
            if (R2.HeldBelowItsSchema()) return;
            foreach (string voter in Found.Voters)
            {
                var engine = Fresh(out var s);
                if (cite) s = R2.Accepted(engine, R2.AskVote(s, voter));
                var entered = R2.Accepted(engine, R2.Entry(s, voter));
                var reply = entered.houseEvents.Single(e => e.id == R2.Cycle(entered, R2.Conversation(entered, voter)).eventId);
                var option = reply.choices.FirstOrDefault(c => cite ? DialogueCampaignWord.EvidenceOf(c.optionId) != null
                    : c.optionId == DialogueCampaignWord.Appeal || c.optionId == DialogueCampaignWord.AskConcern);
                if (option == null) continue;
                var replied = R2.Accepted(engine, R2.Choice(entered, option.optionId));
                var pleaded = R2.Accepted(engine, R2.Choice(replied, DialogueCampaignWord.Plea));
                var lobby = pleaded.lobbies.Single(l => l.week == pleaded.week && l.deciderId == voter);
                Assert.That(lobby.approach, Is.EqualTo(cite ? LobbyApproach.Strategic : LobbyApproach.Emotional),
                    "Strategic after a citation, Emotional otherwise: the ordinary plea's roll.");
                Assert.That(pleaded.randomState, Is.Not.EqualTo(replied.randomState), "The plea's own roll, on the season's stream.");
                Assert.That(R2.Remaining(pleaded), Is.EqualTo(R2.Remaining(s) - 1), "One action for the whole conversation.");
                var episode = R2.Conversation(pleaded, voter);
                Assert.That(R2.Suffix(episode.responses[2]), Is.EqualTo("vote-" + lobby.response));
                Assert.That(R2.Cycle(pleaded, episode).endingId, Is.EqualTo("answered"));
                Assert.That(episode.time.proposedDelta, Is.Zero); Assert.That(episode.time.appliedDelta, Is.Zero);
                Assert.That(DialogueRules.Active(pleaded), Is.Null);
                foreach (string approach in LobbyApproach.All)
                    R2.Refused(new EpisodeEngine(pleaded), R2.Plea(pleaded, voter, approach));
                return;
            }
            Assert.Fail("No reply of the witness campaign offered " + (cite ? "a citation." : "the relationship or the concern."));
        }

        [TestCase("reply")] [TestCase("finish")] [TestCase("keep")]
        public void LeavingEndsNeutrallyAndTheConversationDrawsNothingFromTheSeasonsStream(string at)
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            var state = R2.Accepted(engine, R2.Entry(s, voter));
            if (at != "reply")
            {
                var reply = state.houseEvents.Single(e => e.id == R2.Cycle(state, R2.Conversation(state, voter)).eventId);
                state = R2.Accepted(engine, R2.Choice(state, reply.choices.First(c => c.optionId != DialogueCampaignWord.Leave).optionId));
            }
            var ended = R2.Accepted(engine, R2.Choice(state, at == "keep" ? DialogueCampaignWord.KeepOpen : DialogueCampaignWord.Leave));
            var episode = R2.Conversation(ended, voter);
            Assert.That(R2.Cycle(ended, episode).endingId, Is.EqualTo("neutral"));
            Assert.That(R2.Suffix(episode.responses.Last()), Is.EqualTo(at == "keep" ? "no-promise-made" : "respectful-exit"));
            Assert.That(ended.randomState, Is.EqualTo(s.randomState), "Start to finish without the plea: the season's stream never moved.");
            Assert.That(ended.lobbies.Count, Is.EqualTo(s.lobbies.Count), "No plea was made.");
            Assert.That(R2.Remaining(ended), Is.EqualTo(R2.Remaining(s) - 1));
        }

        [Test]
        public void AnOccasionIsUsedOnceAWeekWhateverItsEndingAndStaleChoicesAreRefused()
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            var entry = R2.Entry(s, voter);
            var entered = R2.Accepted(engine, entry);
            Assert.That(DialogueRules.CanStart(entered, Found.Voters[1], out string busy), Is.False);
            Assert.That(busy, Is.EqualTo("Finish the active dialogue first."));
            var duplicate = engine.Apply(entry);
            Assert.That(duplicate.accepted, Is.False, "A resent entry is not a second conversation.");
            var reply = entered.houseEvents.Single(e => e.id == R2.Cycle(entered, R2.Conversation(entered, voter)).eventId);
            var first = R2.Choice(entered, reply.choices.First().optionId);
            var answered = R2.Accepted(engine, first);
            // The answered node again: at the old revision it is stale; at the new one the node is no longer the current one.
            R2.Refused(engine, first);
            var stale = R2.Choice(entered, reply.choices.First().optionId);
            stale.expectedRevision = answered.revision; stale.expectedPhase = answered.phase; stale.id = "stale-node";
            Assert.That(R2.Refused(engine, stale), Is.EqualTo("The conversation changed. Refresh this choice."));
            var left = R2.Accepted(engine, R2.Choice(answered, DialogueCampaignWord.Leave));
            Assert.That(DialogueRules.CanStart(left, voter, out string used), Is.False);
            Assert.That(used, Is.EqualTo("This campaign occasion has already been used."), "Counted on the started row, left or not.");
            var again = R2.Command(left, EpisodeCommandKind.StartDialogue);
            again.targetId = voter; again.secondTargetId = DialogueCampaignWord.OpenCase; again.text = DialogueCampaignWord.TemplateId;
            Assert.That(R2.Refused(engine, again), Is.EqualTo("This campaign occasion has already been used."));
            var old = R2.Command(left, EpisodeCommandKind.StartDialogue);
            old.targetId = Found.Voters[1]; old.secondTargetId = DialogueCampaignV1.OpenCase; old.text = DialogueCampaignV1.TemplateId;
            Assert.That(R2.Refused(engine, old), Is.EqualTo("Choose the offered campaign opening."), "Rules 2 never opens rules 1's content.");
        }

        [Test]
        public void TheCampaignBoundaryLapsesAnOpenConversation()
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            var entered = R2.Accepted(engine, R2.Entry(s, voter));
            var advance = R2.Command(entered, EpisodeCommandKind.Advance);
            var lapsed = R2.Accepted(engine, advance);
            var episode = R2.Conversation(lapsed, voter);
            Assert.That(R2.Cycle(lapsed, episode).endingId, Is.EqualTo("lapsed"));
            Assert.That(episode.time.reason, Is.EqualTo("occasion-closed"));
            Assert.That(R2.Suffix(episode.responses.Last()), Is.EqualTo("occasion-closed"));
            Assert.That(DialogueRules.Active(lapsed), Is.Null);
            Assert.That(lapsed.phase, Is.Not.EqualTo(EpisodePhase.Campaign));
        }

        [Test]
        public void AnIndependentPleaToTheSameVoterClosesTheOpenConversation()
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            var entered = R2.Accepted(engine, R2.Entry(s, voter));
            var pleaded = R2.Accepted(engine, R2.Plea(entered, voter, LobbyApproach.Strategic));
            var episode = R2.Conversation(pleaded, voter);
            Assert.That(R2.Cycle(pleaded, episode).endingId, Is.EqualTo("lapsed"), "The plea made the other way closes the talk.");
            Assert.That(R2.Suffix(episode.responses.Last()), Is.EqualTo("occasion-closed"));
            Assert.That(pleaded.lobbies.Single(l => l.week == pleaded.week && l.deciderId == voter).approach, Is.EqualTo(LobbyApproach.Strategic));
            Assert.That(R2.Remaining(pleaded), Is.EqualTo(R2.Remaining(s) - 2), "The talk and the plea, each paid.");
            Assert.That(DialogueRules.Active(pleaded), Is.Null);
        }

        [Test]
        public void ARefusedConversationLeavesThePleaToBeMadeTheOrdinaryWay()
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            Assert.That(DialogueRules.KeepsThePleaWhenRefused(s), Is.True, "From rules 2 a refusal never takes a plea away.");
            var rules1 = s.Clone(); rules1.dialogueRulesVersion = DialogueRules.Rules1;
            Assert.That(DialogueRules.On(rules1), Is.True);
            Assert.That(DialogueRules.KeepsThePleaWhenRefused(rules1), Is.False, "Rules 1 keeps its lock, as merged.");
            var entered = R2.Accepted(engine, R2.Entry(s, voter));
            var left = R2.Accepted(engine, R2.Choice(entered, DialogueCampaignWord.Leave));
            Assert.That(DialogueRules.CanStart(left, voter, out _), Is.False);
            var pleaded = R2.Accepted(engine, R2.Plea(left, voter, LobbyApproach.Emotional));
            Assert.That(pleaded.lobbies.Count(l => l.week == pleaded.week && l.deciderId == voter), Is.EqualTo(1));
            Assert.That(R2.Remaining(pleaded), Is.EqualTo(R2.Remaining(s) - 2), "The talk's action and the plea's own: nothing free.");
        }

        /// <summary>
        /// The owner's check (OWNER-DECISIONS-2026-10-22 §4) for rules 2: at AP 0 a reply delivers none of a paid plea's
        /// effect. Every plea is refused for want of a conversation; the reply is accepted and writes only its command, its
        /// conversation, its event and its line, and the one relationship change its reaction carries, if any. No plea
        /// record, memory, ballot, deal, promise or commitment moves, no roll is drawn and nothing is spent.
        /// </summary>
        [Test]
        public void AtApZeroAReplyDeliversNoneOfAPaidPleasEffect()
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            var state = R2.Accepted(engine, R2.Entry(s, voter));
            while (R2.Remaining(state) > 0)
            {
                var talk = R2.Command(state, EpisodeCommandKind.Talk);
                talk.targetId = state.Active.First(p => !p.isPlayer && p.id != voter).id;
                state = R2.Accepted(engine, talk);
            }
            foreach (string approach in LobbyApproach.All)
                Assert.That(new EpisodeEngine(state).Apply(R2.Plea(state, voter, approach)).reason,
                    Is.EqualTo("You have no conversations left this week."), approach + ": the plea is paid.");
            var reply = state.houseEvents.Single(e => e.id == R2.Cycle(state, R2.Conversation(state, voter)).eventId);
            foreach (var option in reply.choices)
            {
                var answered = R2.Accepted(new EpisodeEngine(state), R2.Choice(state, option.optionId));
                JObject was = R2.Tree(state), now = R2.Tree(answered);
                var changed = now.Properties().Where(p => !JToken.DeepEquals(p.Value, was[p.Name])).Select(p => p.Name).ToArray();
                Assert.That(changed, Is.SubsetOf(new[] { "revision", "nextSequence", "acceptedCommandIds", "events", "houseEvents",
                    "storylines", "dialogue", "relationships", "relationshipArcs", "shownOathMilestones", "oathOpportunities" }),
                    option.optionId + ": only its own conversation, and the reaction's one change.");
                foreach (string field in Unmoved)
                    Assert.That(JToken.DeepEquals(now[field], was[field]), Is.True, option.optionId + ": " + field + ", a paid plea's effect, delivered free.");
            }
        }

        [Test]
        public void AConversationSurvivesASaveRoundTripAndPlaysOnTheSame()
        {
            if (R2.HeldBelowItsSchema()) return;
            var engine = Fresh(out var s);
            string voter = Found.Voters[0];
            var asked = R2.Accepted(engine, R2.AskVote(s, voter));
            var entered = R2.Accepted(engine, R2.Entry(asked, voter));
            var restored = R2.RoundTrip(entered);
            Assert.That(R2.Json(restored), Is.EqualTo(R2.Json(entered)));
            Assert.That(EpisodeValidation.TryValidate(restored, out string error), Is.True, error);
            var reply = entered.houseEvents.Single(e => e.id == R2.Cycle(entered, R2.Conversation(entered, voter)).eventId);
            var choice = R2.Choice(entered, reply.choices.First().optionId);
            var live = R2.Accepted(engine, choice);
            var resumed = R2.Accepted(new EpisodeEngine(restored), choice);
            Assert.That(R2.Json(resumed), Is.EqualTo(R2.Json(live)), "A reload resumes the persisted node and its coin.");
        }

        /// <summary>
        /// A lie the reveal caught a voter telling the player can be brought up in a later week's conversation with them,
        /// and the citation keeps the reveal's own line as its memory copy: the player's, about this voter, from that week.
        /// The season is a plain walk that asks every voter where their vote is in every campaign, until a nominated week
        /// meets a voter it caught lying.
        /// </summary>
        [Test]
        public void ALieTheRevealCaughtCanBeBroughtUpLaterAndIsCitedInItsOwnWords()
        {
            if (R2.HeldBelowItsSchema()) return;
            for (uint seed = 1; seed <= 48; seed++)
            {
                var engine = new EpisodeEngine(R2.Start(seed, 12));
                var asked = new HashSet<int>();
                for (int step = 0; step < 1200; step++)
                {
                    var s = engine.Snapshot;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Campaign && VoteRead.Available(s) && asked.Add(s.week))
                    {
                        R2.CloseAsks(engine);
                        string liar = s.nominees.Contains(s.playerId)
                            ? EpisodeEngine.Voters(engine.Snapshot).Where(v => !v.isPlayer).Select(v => v.id).FirstOrDefault(id =>
                                DialogueRules.CanStart(engine.Snapshot, id, out _)
                                && engine.Snapshot.ledger.claims.Any(k => k.voterId == id && k.week < s.week && k.status == ClaimStatus.Lied
                                    && k.source == ClaimSource.Told)
                                && engine.Snapshot.memories.Any(m => m.ownerId == s.playerId && m.subjectId == id && m.text.Contains(" they would vote out ")))
                            : null;
                        if (liar != null)
                        {
                            var state = engine.Snapshot;
                            var entered = R2.Accepted(engine, R2.Entry(state, liar));
                            var episode = R2.Conversation(entered, liar);
                            Assert.That(DialogueCampaignWord.OffersLie(R2.Cycle(entered, episode).beatId), Is.True, "The reply offers the lie.");
                            var cited = R2.Accepted(engine, R2.Choice(entered, DialogueCampaignWord.CiteLie));
                            var row = R2.Conversation(cited, liar).evidence.Single();
                            Assert.That(row.kind, Is.EqualTo(DialogueCampaignWord.MemoryEvidence));
                            var claim = state.ledger.claims.Where(k => k.voterId == liar && k.week < state.week && k.status == ClaimStatus.Lied
                                && k.source == ClaimSource.Told).OrderByDescending(k => k.week).First();
                            Assert.That(row.memory.ownerId, Is.EqualTo(state.playerId));
                            Assert.That(row.memory.subjectId, Is.EqualTo(liar));
                            Assert.That(row.memory.week, Is.EqualTo(claim.week));
                            Assert.That(row.memory.text, Does.StartWith(state.Find(liar).name + " told me in week " + claim.week
                                + " they would vote out " + state.Find(claim.targetId).name + ", and voted out "));
                            Assert.That(state.memories.Any(m => m.text == row.memory.text), Is.True, "A copy of the memory the player holds.");
                            var pleaded = R2.Accepted(engine, R2.Choice(cited, DialogueCampaignWord.Plea));
                            Assert.That(pleaded.lobbies.Single(l => l.week == pleaded.week && l.deciderId == liar).approach, Is.EqualTo(LobbyApproach.Strategic));
                            TestContext.Out.WriteLine("Caught lie: seed " + seed + " (12), week " + state.week + ", voter " + liar + ".");
                            return;
                        }
                        foreach (string voter in EpisodeEngine.Voters(engine.Snapshot).Where(v => !v.isPlayer).Select(v => v.id).ToArray())
                            if (!EpisodeEngine.AskedThisWeek(engine.Snapshot, voter)) engine.Apply(R2.AskVote(engine.Snapshot, voter));
                    }
                    R2.Accepted(engine, ModeTwoReaderSweep.Next(engine.Snapshot));
                }
            }
            Assert.Fail("No season of 48 at twelve met a nominated campaign with a voter the reveal had caught lying to the player.");
        }

        /// <summary>
        /// A greedy scripted player, as once-a-week-guards-count-attempts asks of every free action: in every campaign it is
        /// nominated in, it asks every voter where their vote is, then tries every dialogue command it can build - every
        /// voter's entry, every option of the open node, a resent choice, a re-entry after leaving - on a copy, and takes the
        /// one that warms the voter most. Whatever it does, a conversation costs its action, happens once per voter and week,
        /// moves the relationship once at most, and every free command it was accepted advanced a conversation by one node.
        /// </summary>
        [Test]
        public void AGreedyScriptedPlayerFindsNoFreeLoop()
        {
            if (R2.HeldBelowItsSchema()) return;
            int conversations = 0, free = 0, changes = 0, campaigns = 0;
            var answers = new Dictionary<string, int>(StringComparer.Ordinal);
            var cited = new Dictionary<string, int>(StringComparer.Ordinal);
            for (uint seed = 1; seed <= 8; seed++)
            {
                var engine = new EpisodeEngine(R2.Start(seed));
                var played = new HashSet<string>(StringComparer.Ordinal);
                for (int step = 0; step < 900; step++)
                {
                    var s = engine.Snapshot;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Campaign && s.nominees.Contains(s.playerId) && played.Add(s.week.ToString()))
                    {
                        campaigns++;
                        R2.CloseAsks(engine);
                        Greedy(engine, ref conversations, ref free, ref changes, answers, cited);
                        s = engine.Snapshot;
                    }
                    R2.Accepted(engine, ModeTwoReaderSweep.Next(s));
                }
                var end = engine.Snapshot;
                Assert.That(end.dialogue.episodes.GroupBy(e => e.occasionId).All(g => g.Count() == 1), Is.True, "One conversation an occasion.");
                Assert.That(end.dialogue.episodes.Count, Is.LessThanOrEqualTo(DialogueRules.ConversationBudget(DialogueRules.Rules2)));
                Assert.That(EpisodeValidation.TryValidate(end, out string error), Is.True, error);
            }
            TestContext.Out.WriteLine("Greedy player: " + campaigns + " campaigns, " + conversations + " conversations, " + free
                + " free replies, " + changes + " relationship changes; answers " + string.Join(", ", answers.Select(p => p.Key + " " + p.Value))
                + "; cited " + string.Join(", ", cited.Select(p => p.Key + " " + p.Value)) + ".");
            Assert.That(conversations, Is.GreaterThan(0), "The hunt really talked.");
            Assert.That(changes, Is.LessThanOrEqualTo(conversations), "At most one relationship change a conversation.");
            Assert.That(free, Is.LessThanOrEqualTo(conversations * (DialogueRules.StepsFor(DialogueRules.Rules2) - 1)));
        }

        private static void Greedy(EpisodeEngine engine, ref int conversations, ref int free, ref int changes,
            Dictionary<string, int> answers, Dictionary<string, int> cited)
        {
            var s = engine.Snapshot;
            if (VoteRead.Available(s))
                foreach (var voter in EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ToArray())
                {
                    s = engine.Snapshot;
                    if (EpisodeEngine.AskedThisWeek(s, voter)) continue;
                    var asked = engine.Apply(R2.AskVote(s, voter));
                    if (asked.accepted) Assert.That(R2.Remaining(asked.state), Is.EqualTo(R2.Remaining(s)), "Asking is free.");
                }
            for (int guard = 0; guard < 64; guard++)
            {
                s = engine.Snapshot;
                var candidates = new List<EpisodeCommand>();
                var active = DialogueRules.Active(s);
                if (active == null)
                    foreach (var voter in EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id))
                    {
                        var start = R2.Command(s, EpisodeCommandKind.StartDialogue);
                        start.targetId = voter; start.secondTargetId = DialogueCampaignWord.OpenCase; start.text = DialogueCampaignWord.TemplateId;
                        start.id = "greedy-start-" + s.revision + "-" + voter;
                        candidates.Add(start);
                    }
                else
                {
                    var cycle = DialogueRules.Cycle(s, active);
                    var item = s.houseEvents.Single(e => e.id == cycle.eventId);
                    foreach (var option in item.choices)
                    {
                        var choice = R2.Command(s, EpisodeCommandKind.ChooseDialogue);
                        choice.targetId = active.id; choice.secondTargetId = option.optionId; choice.text = cycle.beatId;
                        choice.id = "greedy-choice-" + s.revision + "-" + option.optionId;
                        candidates.Add(choice);
                    }
                    foreach (string stale in new[] { DialogueCampaignWord.Open, DialogueCampaignWord.FinishBeat(DialogueCampaignWord.Warmed) })
                    {
                        if (stale == cycle.beatId) continue;
                        var choice = R2.Command(s, EpisodeCommandKind.ChooseDialogue);
                        choice.targetId = active.id; choice.secondTargetId = DialogueCampaignWord.Plea; choice.text = stale;
                        choice.id = "greedy-stale-" + s.revision + "-" + stale;
                        candidates.Add(choice);
                    }
                }
                EpisodeState best = null; EpisodeCommand pick = null; double bestScore = double.MinValue;
                foreach (var command in candidates)
                {
                    var result = new EpisodeEngine(s).Apply(command);
                    if (!result.accepted) continue;
                    var next = result.state;
                    Check(s, next, command);
                    var episode = next.dialogue.episodes.LastOrDefault();
                    string npc = episode?.npcId;
                    double view = npc == null ? 0 : next.Score(npc, next.playerId) - s.Score(npc, s.playerId);
                    // Greedy: a voter with something to cite first, then the warmest reply, a citation before anything
                    // else, the exit last.
                    var reply = command.kind == EpisodeCommandKind.StartDialogue && episode != null
                        ? next.houseEvents.Single(e => e.id == R2.Cycle(next, episode).eventId) : null;
                    double score = view * 10 + (DialogueCampaignWord.EvidenceOf(command.secondTargetId) != null ? 1 : 0)
                        + (reply?.choices.Count(c => DialogueCampaignWord.EvidenceOf(c.optionId) != null) ?? 0)
                        - (command.secondTargetId == DialogueCampaignWord.Leave ? 2 : 0);
                    if (score > bestScore) { bestScore = score; best = next; pick = command; }
                }
                if (pick == null) return;
                var before = engine.Snapshot;
                var taken = R2.Accepted(engine, pick);
                if (pick.kind == EpisodeCommandKind.StartDialogue)
                {
                    conversations++;
                    string answer = R2.Suffix(taken.dialogue.episodes.Last().responses[0]);
                    answers[answer] = answers.TryGetValue(answer, out int n) ? n + 1 : 1;
                }
                else
                {
                    free++;
                    var episode = taken.dialogue.episodes.Single(e => e.id == pick.targetId);
                    if (episode.responses.Count == 2 && pick.secondTargetId != DialogueCampaignWord.Leave
                        && DialogueCampaignWord.Delta(R2.Suffix(episode.responses[1])) != 0) changes++;
                    string kind = DialogueCampaignWord.EvidenceOf(pick.secondTargetId);
                    if (kind != null) cited[kind] = cited.TryGetValue(kind, out int n) ? n + 1 : 1;
                }
                Assert.That(R2.Json(taken), Is.EqualTo(R2.Json(best)), "The probe and the real command agree.");
                Assert.That(before.revision + 1, Is.EqualTo(taken.revision));
            }
            Assert.Fail("The greedy player found more than 64 accepted dialogue commands in one campaign: a loop.");
        }

        // What every accepted dialogue command must hold, whatever the player tried.
        private static void Check(EpisodeState s, EpisodeState next, EpisodeCommand command)
        {
            int spent = R2.Remaining(s) - R2.Remaining(next);
            if (command.kind == EpisodeCommandKind.StartDialogue)
            {
                Assert.That(spent, Is.EqualTo(1), "A conversation costs the window's action.");
                Assert.That(next.dialogue.episodes.Count, Is.EqualTo(s.dialogue.episodes.Count + 1));
                Assert.That(next.randomState, Is.EqualTo(s.randomState), "Opening draws nothing from the season's stream.");
                return;
            }
            Assert.That(spent, Is.Zero, "A reply is free.");
            Assert.That(command.text, Is.EqualTo(DialogueRules.Cycle(s, DialogueRules.Active(s)).beatId), "Only the current node answers.");
            var was = s.dialogue.episodes.Single(e => e.id == command.targetId);
            var now = next.dialogue.episodes.Single(e => e.id == command.targetId);
            Assert.That(R2.Cycle(next, now).path.Count, Is.EqualTo(R2.Cycle(s, was).path.Count + 1), "Every free command advances one node.");
            Assert.That(now.responses.Count, Is.EqualTo(was.responses.Count + 1));
            if (command.secondTargetId != DialogueCampaignWord.Plea)
                Assert.That(next.randomState, Is.EqualTo(s.randomState), "Only the plea rolls the season's stream.");
            Assert.That(next.lobbies.Count, Is.EqualTo(s.lobbies.Count + (command.secondTargetId == DialogueCampaignWord.Plea ? 1 : 0)));
        }
    }
}
