#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// R7's lab (R7 build plan C1): the A1 records - each week's prompts, answers, role-week flags and minutes, outside the
    /// rows the goldens hash - and the A2 opt-in board answering, which the lead's decision Q3 shapes: a player works out its
    /// nominees or its veto decision first and answers the board as that decision keeps, one word on the veto at most, no
    /// promise of safety broken at the nomination it was made at, and the players who leave every board byte-identical with it.
    /// Every test that plays Roles or Blame names it, by a direct assignment or a cell that holds it only while it is not
    /// Current (<see cref="At"/>), so each stays right once Roles enrols. The Unity-free run only, as every lab test.
    /// </summary>
    public sealed class BalanceLabBoardsTests
    {
        /// <summary>The story rules both arms play: Blame, today's Current, and Roles, the role moments.</summary>
        private static readonly int[] Rules = { StoryRules.Blame, StoryRules.Roles };

        /// <summary>A cell at these story rules: as it is while they are Current, opted in or held otherwise (BudgetNought's pattern).</summary>
        private static BalanceLab.Cell At(BalanceLab.Cell cell, int rules) => rules == StoryRules.Current ? cell : cell.AtStoryRules(rules);

        /// <summary>The players who answer at least one board.</summary>
        private static readonly string[] Answering = BalancePolicies.Gated.Where(p => p != BalancePolicies.Passive && p != BalancePolicies.Beast).ToArray();

        // ---------------------------------------------------------------- the flag

        /// <summary>
        /// A cell answers the boards only when it says so: its name says it ("/boards1", nothing without it, so every cell keeps
        /// its name), its seeds and fresh season are the unflagged cell's, BALANCE_BOARDS reads 0 or 1 and nothing else, only the
        /// gated players take the flag, and no row says it - nor any of R7's records.
        /// </summary>
        [Test]
        public void ACellAnswersTheBoardsOnlyWhenItSaysSo()
        {
            var shipped = new BalanceLab.Cell { policy = BalancePolicies.Social, size = 8 };
            Assert.That(shipped.House, Is.EqualTo("Regular/8/npc0"), "An unflagged cell keeps its name.");
            var answering = shipped.AnsweringBoards();
            Assert.That(answering.House, Is.EqualTo("Regular/8/npc0/boards1"));
            Assert.That(answering.HeldAtTuning0().House, Does.EndWith("/tuning0/boards1"), "Held and answering, both named.");
            Assert.That(shipped.answersBoards, Is.False, "Answering a copy leaves the cell it came from alone.");
            Assert.That(BalanceLab.Seed(answering, 7), Is.EqualTo(BalanceLab.Seed(shipped, 7)), "The arms play the same seasons.");
            Assert.That(JsonConvert.SerializeObject(BalanceLab.Fresh(answering, 3u)), Is.EqualTo(JsonConvert.SerializeObject(BalanceLab.Fresh(shipped, 3u))),
                "The flag is the player's, never a rule of the season.");

            foreach (string unset in new[] { null, "", " ", "0" }) Assert.That(BalanceLabParts.Boards(unset), Is.False, "'" + unset + "'");
            Assert.That(BalanceLabParts.Boards("1"), Is.True);
            foreach (string wrong in new[] { "2", "yes", "true" }) Assert.Throws<ArgumentException>(() => BalanceLabParts.Boards(wrong), wrong);

            Assert.That(((GatedPolicy)BalancePolicies.Create(BalancePolicies.Reader, answersBoards: true)).AnswersBoards, Is.True);
            Assert.That(((GatedPolicy)BalancePolicies.Create(BalancePolicies.Reader)).AnswersBoards, Is.False, "Off unless asked for.");
            Assert.That(BalancePolicies.Create(BalancePolicies.OracleSkilled, answersBoards: true), Is.InstanceOf<IOraclePolicy>(), "The oracles play as they did.");

            var run = BalanceLab.Play(new BalanceLab.Cell { policy = BalancePolicies.Social, size = 6 }.AnsweringBoards(), 0);
            Assert.That(run.error, Is.Null);
            string row = BalanceLab.Row(run);
            foreach (string field in new[] { "boards", "answersBoards", "promptsByWeek", "storylinesPeak", "vetoBoardCards", "minutes" })
                Assert.That(row, Does.Not.Contain(field), "The hashed row carries none of R7's records: " + field);
        }

        // ---------------------------------------------------------------- whole seasons

        /// <summary>
        /// The passive player, the studier and the beast leave every board, so with the boards answered they play the seasons
        /// they play without them, byte for byte, at Blame and at Roles - and R7's records, which only read, are the same too.
        /// The flagged seasons met boards (the path ran), and their records hold together: every week a record, the story's asks
        /// summed the pace's, a minute count for every week in the house, and the storylines' end their peak.
        /// </summary>
        [Test]
        public void ThePlayersWhoLeaveEveryBoardPlayTheSameSeasonsWithTheBoardsAnswered()
        {
            const int seeds = 4;
            int boards = 0;
            foreach (int rules in Rules)
                foreach (string policy in new[] { BalancePolicies.Passive, BalancePolicies.Studier, BalancePolicies.Beast })
                {
                    var plain = At(new BalanceLab.Cell { policy = policy, size = 8 }, rules);
                    var without = BalanceLab.Run(new[] { plain }, seeds);
                    var with = BalanceLab.Run(new[] { plain.AnsweringBoards() }, seeds);
                    for (int i = 0; i < seeds; i++)
                    {
                        string at = policy + " at story rules " + rules + ", season " + i;
                        Assert.That(with[i].error, Is.Null, at);
                        Assert.That(BalanceLab.Fresh(with[i].cell, with[i].seed).story.rulesVersion, Is.EqualTo(rules), at);
                        Assert.That(BalanceLab.Row(with[i]), Is.EqualTo(BalanceLab.Row(without[i])), at + ": the same season with the boards answered.");
                        Assert.That(JsonConvert.SerializeObject(with[i].promptsByWeek), Is.EqualTo(JsonConvert.SerializeObject(without[i].promptsByWeek)), at + ": the same records.");
                        Records(with[i], at);
                        boards += with[i].promptsByWeek.Values.Sum(w => w.pitchCards + w.vetoBoardCards);
                    }
                }
            Assert.That(boards, Is.GreaterThan(0), "The flagged seasons met boards, so the answering path ran.");
        }

        /// <summary>
        /// Every gated player, answering the boards, plays legal seasons at Blame and at Roles: no error, at most one word on the
        /// veto a week, and records that hold together. The answering players answer boards; a season's prompts and answers are
        /// printed by policy for the eye.
        /// </summary>
        [Test]
        public void EveryAnsweringPlayerPlaysALegalSeasonAndGivesOneWordOnTheVetoAWeek()
        {
            const int seeds = 2;
            var answered = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (int rules in Rules)
            {
                var cells = BalancePolicies.Gated.Select(p => At(new BalanceLab.Cell { policy = p, size = 8 }, rules).AnsweringBoards()).ToList();
                var runs = BalanceLab.Run(cells, seeds);
                foreach (var r in runs)
                {
                    string at = r.cell.Key + " season " + r.index;
                    Assert.That(r.error, Is.Null, at);
                    Records(r, at);
                    foreach (var w in r.promptsByWeek.Values)
                        Assert.That(w.words[BalanceLab.PromptWatch.VetoAsk] + w.words[BalanceLab.PromptWatch.KeepTheBlock], Is.LessThanOrEqualTo(1), at + ", week " + w.week + ": one word on the veto.");
                    int answers = r.promptsByWeek.Values.Sum(w => w.answers[BalanceLab.PromptWatch.Pitch] + w.answers[BalanceLab.PromptWatch.VetoAsk] + w.answers[BalanceLab.PromptWatch.KeepTheBlock]);
                    answered[r.cell.policy] = (answered.TryGetValue(r.cell.policy, out int n) ? n : 0) + answers;
                    TestContext.WriteLine(at + ": prompts " + r.promptsByWeek.Values.Sum(w => w.Shown) + " (veto board " + r.promptsByWeek.Values.Sum(w => w.vetoBoardCards)
                        + ", pitches " + r.promptsByWeek.Values.Sum(w => w.pitchCards) + ", story " + r.promptsByWeek.Values.Sum(w => w.storyAsks.Values.Sum())
                        + "), board answers " + answers + ", words " + r.promptsByWeek.Values.Sum(w => w.words.Values.Sum()) + ", storylines " + r.storylines);
                }
            }
            Assert.That(Answering.Sum(p => answered[p]), Is.GreaterThan(0), "The answering players answered boards.");
            Assert.That(answered[BalancePolicies.Passive] + answered[BalancePolicies.Beast], Is.Zero, "The passive player and the beast leave them.");
        }

        /// <summary>A season's R7 records hold together.</summary>
        private static void Records(BalanceLab.SeasonRun r, string at)
        {
            Assert.That(r.promptsByWeek.Keys, Is.EqualTo(Enumerable.Range(1, r.autopsy.weeks)), at + ": a record every week.");
            Assert.That(r.promptsByWeek.Values.Sum(w => w.storyAsks.Values.Sum()), Is.EqualTo(r.pace.asks), at + ": the story's asks are the pace's.");
            Assert.That(r.storylines, Is.EqualTo(r.storylinesPeak), at + ": nothing removes a storyline, so the end is the peak.");
            Assert.That(r.storylines, Is.InRange(1, EpisodeEngine.StorylineCeiling), at);
            foreach (int week in BalanceLabReports.WeeksInTheHouse(r)) Assert.That(r.promptsByWeek[week].minutes, Is.GreaterThan(0), at + ", week " + week + ": minutes in the house.");
            foreach (var w in r.autopsy.weeksPlayed)
                Assert.That((r.promptsByWeek[w.week].playerHoh, r.promptsByWeek[w.week].playerVetoHolder, r.promptsByWeek[w.week].playerNominated),
                    Is.EqualTo((w.playerHoh, w.playerVetoHolder, w.playerNominated)), at + ", week " + w.week + ": the autopsy's role-week flags.");
            foreach (var w in r.promptsByWeek.Values)
            {
                Assert.That(w.keepTheBlockCards, Is.InRange(0, Math.Min(1, w.vetoBoardCards)), at + ", week " + w.week);
                if (w.pitchCards > 0) Assert.That(w.playerHoh, Is.True, at + ", week " + w.week + ": pitches come to a Head of Household.");
                if (w.vetoBoardCards > 0) Assert.That(w.playerVetoHolder, Is.True, at + ", week " + w.week + ": the veto board comes to its holder.");
            }
        }

        // ---------------------------------------------------------------- the pitches

        /// <summary>
        /// At a player Head of Household's pitches - a three-card board at Roles, the week-one queue of seven at Blame - every
        /// gated player answering the boards answers before it nominates (the passive player and the beast leave them), promises
        /// safety only to pitchers it leaves off its block, and breaks no promise at the nomination; and as the replacement for a
        /// houseguest's save it names nobody it promised. Without the boards every one of them nominates first.
        /// </summary>
        [Test]
        public void AnsweringPlayersAnswerThePitchesBeforeTheyNominateAndBreakNoPromise()
        {
            foreach (int rules in Rules)
            {
                var crowned = RolePitchBoardTests.Crowned(1, 2, roles: rules == StoryRules.Roles).Clone();
                crowned.story.rulesVersion = rules;
                var dealt = Advanced(crowned);
                int pitches = dealt.replyCards.Count(c => c.kind == ReplyCards.Pitch);
                Assert.That(pitches, Is.EqualTo(rules == StoryRules.Roles ? HoHPitches.BoardSize : RolePitchBoardTests.Courters(crowned).Count), "story rules " + rules + ": the board or the queue.");
                foreach (string policy in BalancePolicies.Gated)
                {
                    string at = policy + " at story rules " + rules;
                    var unflagged = new EpisodeEngine(dealt.Clone());
                    Assert.That(PlayUntil(unflagged, BalancePolicies.Create(policy), EpisodeCommandKind.Nominate, at).Single().kind, Is.EqualTo(EpisodeCommandKind.Nominate),
                        at + ": without the boards the nominations come first.");

                    var engine = new EpisodeEngine(dealt.Clone());
                    var player = BalancePolicies.Create(policy, answersBoards: true);
                    var used = PlayUntil(engine, player, EpisodeCommandKind.Nominate, at);
                    var s = engine.Snapshot;
                    var replies = used.Where(c => c.kind == EpisodeCommandKind.ReplyToHouseguest).ToList();
                    if (Answering.Contains(policy)) Assert.That(used[0].kind, Is.EqualTo(EpisodeCommandKind.ReplyToHouseguest), at + ": a pitch answered before the nominations.");
                    else Assert.That(replies, Is.Empty, at + ": every pitch left.");
                    Assert.That(used.Take(used.Count - 1).All(c => c.kind == EpisodeCommandKind.ReplyToHouseguest), Is.True, at + ": only answers before the nominations.");
                    var promisedTo = s.ledger.replies.Where(r => r.week == s.week && r.kind == ReplyCards.Pitch && r.replyKey == HoHPitches.PromiseKey).Select(r => r.fromId).ToList();
                    Assert.That(promisedTo.Intersect(s.nominees), Is.Empty, at + ": safety promised only off the block.");
                    Assert.That(BrokenSafety(s), Is.Empty, at + ": no promise broken at the nominations.");
                    if (rules == StoryRules.Blame && (policy == BalancePolicies.Social || policy == BalancePolicies.Floater))
                        Assert.That(replies, Has.Count.EqualTo(pitches), at + ": the whole queue answered, one at a time.");

                    // A houseguest holding the veto on the block saves themselves: the player Head of Household names the replacement.
                    var meeting = HolderOnTheBlockSaves(engine);
                    var decided = PlayUntil(meeting, player, EpisodeCommandKind.ResolveVeto, at + " (the replacement)");
                    Assert.That(decided.Single().kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto), at + ": no board is the Head of Household's.");
                    var after = meeting.Snapshot;
                    Assert.That(promisedTo, Does.Not.Contain(decided.Single().secondTargetId), at + ": the replacement spares the promises.");
                    Assert.That(BrokenSafety(after), Is.Empty, at + ": no promise broken by the replacement.");
                    TestContext.WriteLine(at + ": " + string.Join(", ", replies.Select(c => c.text)) + " | nominated " + string.Join(" and ", s.nominees) + " | promised " + string.Join(", ", promisedTo)
                        + " | replacement " + decided.Single().secondTargetId);
                }
            }
        }

        /// <summary>The player's promises of safety broken, by whom they were made to.</summary>
        private static List<string> BrokenSafety(EpisodeState s) =>
            CommitmentReferences.Promises(s).Where(p => p.fromId == s.playerId && p.kind == PromiseKind.Safety && p.status == PromiseStatus.Broken).Select(p => p.toId).ToList();

        /// <summary>
        /// From the player's nominations on to the veto meeting, with the first nominee - a houseguest - holding the veto: they
        /// save themselves, so the player as Head of Household names the replacement.
        /// </summary>
        private static EpisodeEngine HolderOnTheBlockSaves(EpisodeEngine engine)
        {
            for (int i = 0; i < 50 && !(engine.Snapshot.phase == EpisodePhase.Veto && !engine.Snapshot.competitionResolved); i++)
                RoleVetoBoardTests.Apply(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
            var s = engine.Snapshot.Clone();
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Veto));
            s.vetoHolderId = s.nominees[0];
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.competitionResolved = true;
            s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.vetoHolderId ? 100 : i }).ToList();
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            var meeting = new EpisodeEngine(s);
            var at = RoleVetoBoardTests.Apply(meeting, RoleVetoBoardTests.Command(s, EpisodeCommandKind.Advance));
            Assert.That(at.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(EpisodeEngine.NpcVetoSave(at), Is.EqualTo(s.vetoHolderId), "The holder saves themselves.");
            return meeting;
        }

        // ---------------------------------------------------------------- the veto meeting

        /// <summary>
        /// At a holder's veto meeting - the nominees' two asks and, at Roles, the Head of Household's ask to keep the block - every
        /// gated player answering the boards answers before it decides the veto (the passive player and the beast leave them),
        /// gives at most one word, and keeps it: a nominee's ask taken is the veto used on them, the block's ask taken the veto
        /// unused. A holder on the block (asked at Blame, where nominees still ask one) gives no word and saves itself. Without
        /// the boards every one of them decides the veto first.
        /// </summary>
        [Test]
        public void AnsweringPlayersAnswerTheVetoBoardBeforeTheVetoAndKeepTheirOneWord()
        {
            foreach (int rules in Rules)
            {
                var before = RoleKeepTheBlockTests.BeforeTheMeeting(RoleTone.Calculated, out _, out _, out _, roles: rules == StoryRules.Roles).Clone();
                before.story.rulesVersion = rules;
                var meeting = Advanced(before);
                int cards = RoleOffers.BoardAsks(meeting).Count;
                Assert.That(cards, Is.EqualTo(rules == StoryRules.Roles ? 3 : 2), "story rules " + rules + ": both nominees asked, and at Roles the Head of Household.");
                foreach (string policy in BalancePolicies.Gated)
                {
                    string at = policy + " at story rules " + rules;
                    var unflagged = new EpisodeEngine(meeting.Clone());
                    Assert.That(PlayUntil(unflagged, BalancePolicies.Create(policy), EpisodeCommandKind.ResolveVeto, at).Single().kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto),
                        at + ": without the boards the veto comes first.");

                    var engine = new EpisodeEngine(meeting.Clone());
                    var used = PlayUntil(engine, BalancePolicies.Create(policy, answersBoards: true), EpisodeCommandKind.ResolveVeto, at);
                    var answers = used.Take(used.Count - 1).ToList();
                    Assert.That(answers.All(c => c.kind == EpisodeCommandKind.RespondToDeal), Is.True, at + ": only answers before the veto.");
                    // The random player's coin may leave every ask its veto does not keep, as it leaves them here at Blame.
                    if (Answering.Contains(policy) && policy != BalancePolicies.Random) Assert.That(answers, Is.Not.Empty, at + ": a board ask answered before the veto.");
                    else if (!Answering.Contains(policy)) Assert.That(answers, Is.Empty, at + ": every ask left.");
                    var words = answers.Where(c => c.text == EpisodeEngine.AcceptDeal).Select(c => meeting.deals.Single(d => d.id == c.targetId)).ToList();
                    Assert.That(words, Has.Count.LessThanOrEqualTo(1), at + ": one word on the veto.");
                    var veto = used.Last();
                    foreach (var word in words)
                    {
                        if (word.type == DealKind.VetoHold) Assert.That(veto.useVeto, Is.False, at + ": the block kept, as promised.");
                        else Assert.That((veto.useVeto, veto.targetId), Is.EqualTo((true, word.proposerId)), at + ": the veto used on the nominee it was promised to.");
                        Assert.That(engine.Snapshot.deals.Single(d => d.id == word.id).status, Is.Not.EqualTo(DealStatus.Broken), at + ": the word kept.");
                    }
                    // The exploit hunter keeps the first nominee's ask; the social player, who saves nobody else, the block's (at Roles).
                    if (policy == BalancePolicies.Exploit || (policy == BalancePolicies.Social && rules == StoryRules.Roles))
                        Assert.That(words, Has.Count.EqualTo(1), at + ": the word its veto keeps taken.");
                    TestContext.WriteLine(at + ": " + string.Join(", ", answers.Select(c => (c.targetId == null ? "" : c.targetId.StartsWith(NpcDeals.KeepTheBlockPrefix, StringComparison.Ordinal) ? "keep " : "ask ") + c.text))
                        + " | veto " + (veto.useVeto ? "used on " + veto.targetId : "unused"));
                }
            }

            // A holder on the block: the other nominee asks at Blame (not at Roles, where nobody asks a nominated holder).
            var nominated = NominatedHolderAtBlame(out string other);
            Assert.That(RoleOffers.BoardAsks(nominated).Select(d => d.proposerId), Is.EqualTo(new[] { other }));
            foreach (string policy in BalancePolicies.Gated)
            {
                var engine = new EpisodeEngine(nominated.Clone());
                var used = PlayUntil(engine, BalancePolicies.Create(policy, answersBoards: true), EpisodeCommandKind.ResolveVeto, policy + " on the block");
                Assert.That(used.Where(c => c.kind == EpisodeCommandKind.RespondToDeal && c.text == EpisodeEngine.AcceptDeal), Is.Empty, policy + ": no word it cannot keep while saving itself.");
                Assert.That((used.Last().useVeto, used.Last().targetId), Is.EqualTo((true, nominated.playerId)), policy + ": it saves itself.");
            }
        }

        /// <summary>
        /// A season held at Blame walked to week one's veto competition with the player nominated, then handed the veto, the
        /// other nominee warm enough to ask; advanced into the meeting.
        /// </summary>
        private static EpisodeState NominatedHolderAtBlame(out string other)
        {
            for (uint seed = 7001; seed < 7121; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = StoryRules.Blame;
                var walk = new EpisodeEngine(start);
                for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                    Assert.That(walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot)).accepted, Is.True, "seed " + seed);
                var s = walk.Snapshot.Clone();
                if (s.phase != EpisodePhase.Veto || s.competitionResolved || !s.nominees.Contains(s.playerId)) continue;
                string asker = other = s.nominees.Single(id => id != s.playerId);
                var edge = s.relationships.FirstOrDefault(r => r.fromId == asker && r.toId == s.playerId);
                if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = asker, toId = s.playerId });
                edge.score = 50;
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
                var meeting = Advanced(s);
                if (RoleOffers.BoardAsks(meeting).Count == 1) return meeting;
            }
            Assert.Fail("No seed nominated the player in week one with the other nominee asking.");
            other = null;
            return null;
        }

        // ---------------------------------------------------------------- the records

        /// <summary>
        /// R7's counters read the boards as they stand: a three-card pitch board at Roles and week one's queue of every courting
        /// houseguest at Blame, each counted once at the nominations' first state; the veto meeting's three cards at Roles, the
        /// Head of Household's keep-the-block among them, and its two at Blame; and each answer by its class, a promise and a yes
        /// on the veto as words.
        /// </summary>
        [Test]
        public void TheRecordsCountARolesBoardABlameQueueAndEachAnswer()
        {
            foreach (int rules in Rules)
            {
                bool roles = rules == StoryRules.Roles;
                string at = "story rules " + rules;
                var crowned = RolePitchBoardTests.Crowned(1, 2, roles: rules == StoryRules.Roles).Clone();
                crowned.story.rulesVersion = rules;
                var dealt = Advanced(crowned);
                var run = new BalanceLab.SeasonRun();
                var watch = new BalanceLab.PromptWatch();
                watch.Look(dealt, run);
                watch.Look(dealt, run);
                var week = run.promptsByWeek[dealt.week];
                Assert.That(week.pitchCards, Is.EqualTo(roles ? 3 : RolePitchBoardTests.Courters(crowned).Count), at + ": counted once, the board or the whole queue.");
                Assert.That((week.vetoBoardCards, week.keepTheBlockCards), Is.EqualTo((0, 0)), at);
                var pitches = dealt.replyCards.Where(c => c.kind == ReplyCards.Pitch).ToList();
                watch.Answer(RoleVetoBoardTests.Command(dealt, EpisodeCommandKind.ReplyToHouseguest, pitches[0].id, text: HoHPitches.PromiseKey), dealt, run);
                watch.Answer(RoleVetoBoardTests.Command(dealt, EpisodeCommandKind.ReplyToHouseguest, pitches[0].id, text: HoHPitches.FeelOutKey), dealt, run);
                watch.Answer(RoleVetoBoardTests.Command(dealt, EpisodeCommandKind.ReplyToHouseguest, pitches[1].id, text: HoHPitches.HearKey), dealt, run);
                Assert.That((week.answers[BalanceLab.PromptWatch.Pitch], week.words[BalanceLab.PromptWatch.Pitch]), Is.EqualTo((2, 1)),
                    at + ": two pitches answered, one promise; feeling one out answers nothing.");

                var before = RoleKeepTheBlockTests.BeforeTheMeeting(RoleTone.Calculated, out string nominee, out _, out _, roles: rules == StoryRules.Roles).Clone();
                before.story.rulesVersion = rules;
                var meeting = Advanced(before);
                var counted = new BalanceLab.SeasonRun();
                var meetings = new BalanceLab.PromptWatch();
                meetings.Look(meeting, counted);
                meetings.Look(meeting, counted);
                var veto = counted.promptsByWeek[meeting.week];
                Assert.That((veto.vetoBoardCards, veto.keepTheBlockCards, veto.pitchCards), Is.EqualTo(roles ? (3, 1, 0) : (2, 0, 0)), at + ": the veto board, counted once.");
                var ask = RoleVetoBoardTests.AskOf(meeting, nominee);
                meetings.Answer(RoleVetoBoardTests.Answer(meeting, ask.id, false), meeting, counted);
                Assert.That((veto.answers[BalanceLab.PromptWatch.VetoAsk], veto.words[BalanceLab.PromptWatch.VetoAsk]), Is.EqualTo((1, 0)), at + ": a no is an answer, not a word.");
                if (roles)
                {
                    var hold = RoleKeepTheBlockTests.HoldOf(meeting);
                    meetings.Answer(RoleVetoBoardTests.Answer(meeting, hold.id, true), meeting, counted);
                    Assert.That((veto.answers[BalanceLab.PromptWatch.KeepTheBlock], veto.words[BalanceLab.PromptWatch.KeepTheBlock]), Is.EqualTo((1, 1)), at + ": the block's ask taken.");
                }
                meetings.Answer(RoleVetoBoardTests.Answer(meeting, "deal-ask-elsewhere", true), meeting, counted);
                Assert.That((veto.answers[BalanceLab.PromptWatch.OtherOffer], veto.words.Values.Sum()), Is.EqualTo((1, roles ? 1 : 0)), at + ": any other offer is no word on the veto.");
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The state an Advance leaves this one in, through the public engine.</summary>
        private static EpisodeState Advanced(EpisodeState s) => RoleVetoBoardTests.Apply(new EpisodeEngine(s), RoleVetoBoardTests.Command(s, EpisodeCommandKind.Advance));

        /// <summary>
        /// The lab's own steps (<see cref="BalanceLab.Step(EpisodeEngine, object, EpisodeState, BalanceLab.SeasonRun, out EpisodeCommand)"/>:
        /// the player's command, the walker's when it has none) until one of <paramref name="until"/>'s kind is applied; the
        /// commands applied, in order.
        /// </summary>
        private static List<EpisodeCommand> PlayUntil(EpisodeEngine engine, object player, EpisodeCommandKind until, string at)
        {
            var run = new BalanceLab.SeasonRun { cell = new BalanceLab.Cell { policy = (player as IBalancePolicy)?.Name, size = 8 }, seed = 17u };
            var used = new List<EpisodeCommand>();
            for (int i = 0; i < 40; i++)
            {
                var result = BalanceLab.Step(engine, player, engine.Snapshot, run, out var command);
                Assert.That(result.accepted, Is.True, at + ": " + command.kind + " " + command.targetId + " was refused: " + result.reason);
                used.Add(command);
                if (command.kind == until) return used;
            }
            Assert.Fail(at + ": no " + until + " in forty steps: " + string.Join(", ", used.Select(c => c.kind)));
            return used;
        }
    }
}
#endif
