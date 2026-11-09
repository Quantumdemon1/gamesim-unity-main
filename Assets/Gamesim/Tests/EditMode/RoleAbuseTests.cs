using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The role moments' abuse checklist (ROLE-MOMENTS-PLAN R7 (5); the R7 build plan's C2), through the public engine: what a
    /// player who games the boards cannot get. (1) The holder can put no word on the veto to anybody, so the board's one word is
    /// the only one; (2) a word taken and broken at the decision books the yes's +4 and the breach's -45 with the asker and
    /// nothing else, and the breach keeps that asker's next ask away until they read the player a breach's fifteen warmer;
    /// (3) a greedy comforter answers quiet tears at most once a week, so its +5 comes at most once a week in the house, and
    /// each seat leaves one favour - favours over one houseguest stack, as the lead decided (R7, Q7); (4) the pawn talk's word
    /// taken, the player who puts that Head of Household up next week breaks their own promise of safety, in every commitment
    /// mode; (5) a board ask after the veto decision, a pitch after the nominations and the pawn card after the veto
    /// competition are refused and change nothing; (7) a promise to a pitcher and a seat with quiet tears, pressed twice,
    /// commit once; (9) an invitation never raises the veto board's own asks under the role moments (R7, Q8). Every season
    /// here opts into the role moments by assignment (story rules Roles) and every control is held at threads by name, so
    /// nothing reads the shipped story rules. Item (6) is the save store's (<c>RoleAbusePersistenceTests</c>, EditMode only)
    /// and the director's, with item (8) (<c>EpisodePlayModeTests.RoleAbuse</c>). Pure simulation: the Unity-free subset runs it.
    /// </summary>
    public sealed class RoleAbuseTests
    {
        private const string TearsCard = "quiet-tears:tears";

        // ------------------------------------------------------------------ (1) accepting both asks

        /// <summary>
        /// At the meeting the holder can put no word on the veto to anybody - a veto commitment binds only a houseguest who holds
        /// the veto, and a keep-the-block deal is never the player's to put - before a yes on the board and after one: the deal
        /// table offers neither to any houseguest, and the engine refuses the proposal and moves nothing. So the board's one word
        /// is the only word on the veto the player can give. (The board's own refusal of a second yes is
        /// RoleVetoBoardTests.UnderRolesASecondVetoYesIsRefusedAndChangesNothing; a keep-the-block yes against a nominee's,
        /// RoleKeepTheBlockTests.AKeepTheBlockYesAndAVetoYesLockEachOther.)
        /// </summary>
        [Test]
        public void TheHolderCanPutNoWordOnTheVetoToAnybodyBeforeOrAfterAYes()
        {
            var engine = RoleKeepTheBlockTests.Board(RoleTone.Calculated, out string first, out string second, out string hoh);
            var meeting = engine.Snapshot;
            Assert.That((meeting.vetoHolderId, EpisodeEngine.RolesOn(meeting)), Is.EqualTo((meeting.playerId, true)));
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, RoleVetoBoardTests.AskOf(meeting, first).id, true));
            Assert.That(Negotiation.VetoWordGiven(taken), Is.True, "(a yes on the board)");
            foreach (var (when, s) in new[] { ("before a yes", meeting), ("after a yes", taken) })
            {
                foreach (var npc in s.Active.Where(c => !c.isPlayer))
                {
                    var table = PlayerDeals.Available(s, npc.id);
                    Assert.That(table, Does.Not.Contain(DealKind.VetoUse), when + ": the deal table offers " + npc.id + " no veto commitment,");
                    Assert.That(table, Does.Not.Contain(DealKind.VetoHold), when + ": nor a keep-the-block deal.");
                    Assert.That(PlayerDeals.CanPropose(s, npc.id, DealKind.VetoUse, null, out _), Is.False, when + ": " + npc.id);
                }
                string before = Json(s);
                foreach (string to in new[] { first, second, hoh })
                    foreach (string kind in new[] { DealKind.VetoUse, DealKind.VetoHold })
                    {
                        var refused = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.ProposeDeal, to, null, kind));
                        Assert.That(refused.accepted, Is.False, when + ": " + kind + " put to " + to + " is refused.");
                    }
                Assert.That(Json(s), Is.EqualTo(before), when + ": nothing moved.");
            }
        }

        // ------------------------------------------------------------------ (2) accept-then-break loops

        /// <summary>
        /// A nominee's ask taken, then the veto used on the other nominee: the player broke it, and the books with the asker are
        /// exactly the yes's +4 and the breach's -45 - no no, no silence, no keeping, no defiance beside them. The breach is on
        /// the player's record, so next week that nominee, on the block again with the player holding the veto, asks only once
        /// they read the player a broken deal's fifteen warmer: at the dealing floor plus fifteen a broken deal, less one, they do
        /// not; at it they do; and the same week with this breach off the record asks at the lower score. Taking a word to
        /// break it buys no loop. (The keep-the-block ask's floor with a breach on record is
        /// RoleKeepTheBlockTests.EachConditionAloneKeepsTheAskAway.)
        /// </summary>
        [Test]
        public void AWordTakenAndBrokenBooksPlusFourAndMinusFortyFiveAndKeepsTheirNextAskAway()
        {
            var engine = RoleVetoBoardTests.Board(RoleTone.Pragmatic, out string first, out string second);
            var meeting = engine.Snapshot;
            string player = meeting.playerId;
            int broken = NpcDeals.BrokenDeals(meeting, player);
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, RoleVetoBoardTests.AskOf(meeting, first).id, true));
            var decided = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, true, second));
            var ask = RoleVetoBoardTests.AskOf(decided, first);
            Assert.That((ask.status, ask.brokenById), Is.EqualTo((DealStatus.Broken, player)), "Saving the other nominee breaks the word taken.");
            Assert.That(RoleVetoBoardTests.Events(decided, player, first, "deal_accepted").Select(e => e.impactScore),
                Is.EqualTo(new[] { PlayerDeals.CommittedAcceptedImpact }), "+4 for the yes,");
            Assert.That(RoleVetoBoardTests.Events(decided, first, player, "deal_broken").Select(e => e.impactScore), Is.EqualTo(new[] { -45.0 }),
                "-45 for the breach, once,");
            foreach (string type in new[] { "deal_declined", "deal_fulfilled", EpisodeEngine.LapsedOfferEvent, EpisodeEngine.DefiedAskEvent })
                Assert.That(RoleVetoBoardTests.Events(decided, first, player, type).Concat(RoleVetoBoardTests.Events(decided, player, first, type)), Is.Empty,
                    "and nothing else with them: no " + type + ".");
            Assert.That(NpcDeals.BrokenDeals(decided, player), Is.EqualTo(broken + 1), "The breach is on the player's record.");

            double floor = NpcDeals.DealingFloor + NpcDeals.BrokenDealPenalty * NpcDeals.BrokenDeals(decided, player);
            Assert.That(NextWeeksAsk(decided, first, floor - 1, forgiven: false), Is.False, "One short of the floor and a breach's fifteen, they do not ask;");
            Assert.That(NextWeeksAsk(decided, first, floor, forgiven: false), Is.True, "at it they do;");
            Assert.That(NextWeeksAsk(decided, first, floor - 1, forgiven: true), Is.True, "and with this breach off the record the lower score asks.");
        }

        /// <summary>
        /// Next week's meeting on <paramref name="decided"/>'s record: a week on, <paramref name="nominee"/> and another houseguest
        /// on the block, the player holding the veto with the decision to come, and the nominee reading the player at
        /// <paramref name="score"/> - with <paramref name="forgiven"/>, the player's breach against them this week off the record.
        /// Whether the asks' filing (<see cref="NpcDeals.AskForTheVeto"/>, which the advance into a meeting runs) files theirs.
        /// </summary>
        private static bool NextWeeksAsk(EpisodeState decided, string nominee, double score, bool forgiven)
        {
            var s = decided.Clone();
            if (forgiven)
                foreach (var deal in s.deals.Where(d => d.week == s.week && d.proposerId == nominee && d.brokenById == s.playerId))
                {
                    deal.status = DealStatus.Expired;
                    deal.brokenById = null;
                }
            s.week++;
            s.phase = EpisodePhase.VetoMeeting;
            s.vetoResolved = false;
            s.vetoHolderId = s.playerId;
            string other = s.Active.First(c => !c.isPlayer && c.id != nominee && c.id != s.hohId).id;
            s.nominees = new List<string> { nominee, other };
            Set(s, nominee, s.playerId, score);
            Assert.That(EpisodeEngine.RolesOn(s) && StrategyRules.VetoCanBeUsed(s), Is.True, "(next week's meeting)");
            NpcDeals.AskForTheVeto(s);
            return s.deals.Any(d => RoleOffers.IsVetoAsk(s, d) && d.proposerId == nominee && d.status == DealStatus.Proposed);
        }

        // ------------------------------------------------------------------ (3) comfort farming

        /// <summary>
        /// A greedy comforter: RoleStoryMomentsTests' eleven seasons under the role moments
        /// (SeasonsUnderTheRoleMomentsKeepTheirMomentsInBounds), walked by the test policy with the player answering every quiet
        /// tears card put to them - sitting with them a while where that is shown and a seat is left, else comforting them. They
        /// answer at most one card a week, so the +5 those answers move comes at most once a week in the house; each seat leaves
        /// exactly one favour more and a comfort none, inside the story's 32. (Quiet tears' once a week is
        /// RoleStoryMomentsTests.QuietTearsComesAtMostOnceAWeek; the favours' stacking, the next test.)
        /// </summary>
        [Test]
        public void AGreedyComforterGetsOneAnswerAWeekAndOneFavourASeat()
        {
            int answered = 0, seats = 0;
            foreach (uint seed in new uint[] { 7601, 7602, 7682, 7621, 7622, 7623, 7624, 7625, 7626, 7627, 7628 })
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = StoryRules.Roles;
                var engine = new EpisodeEngine(start);
                var inTheHouse = new HashSet<int>();
                var answers = new List<(int week, string subject, string option)>();
                for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    string where = "seed " + seed + " week " + s.week + " " + s.phase + ": ";
                    bool active = s.Find(s.playerId).status == ContestantStatus.Active;
                    if (active) inTheHouse.Add(s.week);
                    var card = active ? s.houseEvents.FirstOrDefault(e => !e.resolved && e.contentId == TearsCard) : null;
                    if (card != null)
                    {
                        string subject = Subject(card);
                        int held = Favours(s, subject);
                        bool room = s.story.hooks.Count(h => !h.spent) < 32;
                        string chosen = null;
                        CommandResult result = null;
                        if (card.choices.Any(c => c.optionId == "sit-a-while"))
                        {
                            result = engine.Apply(Progress(s, card.id, "sit-a-while"));
                            if (result.accepted) chosen = "sit-a-while";
                            else Assert.That(result.reason, Is.EqualTo("You have no actions left this week."), where + "a seat is refused only for want of one.");
                        }
                        if (chosen == null && card.choices.Any(c => c.optionId == "comfort"))
                        {
                            result = engine.Apply(Progress(s, card.id, "comfort"));
                            Assert.That(result.accepted, Is.True, where + result.reason);
                            chosen = "comfort";
                        }
                        if (chosen != null)
                        {
                            answers.Add((s.week, subject, chosen));
                            Assert.That(Favours(result.state, subject), Is.EqualTo(held + (chosen == "sit-a-while" && room ? 1 : 0)),
                                where + chosen + ": one favour a seat, none a comfort.");
                            continue;
                        }
                    }
                    var step = engine.Apply(RoleStoryPools.Next(s));
                    Assert.That(step.accepted, Is.True, where + step.reason);
                }
                var end = engine.Snapshot;
                Assert.That(end.phase, Is.EqualTo(EpisodePhase.Finished), "seed " + seed);
                Assert.That(answers.GroupBy(a => a.week).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, "seed " + seed + ": at most one answer a week.");
                Assert.That(answers.Count, Is.LessThanOrEqualTo(inTheHouse.Count), "seed " + seed + ": a +5 at most once a week in the house.");
                Assert.That(end.story.hooks.Count(h => !h.spent), Is.LessThanOrEqualTo(32), "seed " + seed + ": inside the story's bound.");
                answered += answers.Count;
                seats += answers.Count(a => a.option == "sit-a-while");
            }
            TestContext.WriteLine("The greedy player answered " + answered + " quiet tears cards, " + seats + " of them seats.");
            Assert.That(answered, Is.GreaterThan(0), "The greedy player answered quiet tears in some season (" + seats + " of them seats).");
        }

        /// <summary>
        /// Favours over one houseguest stack (the lead's call, R7 Q7: allowed and pinned, each earned - by a seat with quiet tears or
        /// a Desperate nominee's word kept): a seat with them leaves a favour beside one they already owe, both stand, and calling
        /// one in spends one and leaves the other. (The story's bound of 32 is RoleVetoBoardTests.ADesperateNomineesFavourKeepsInsideTheStorysBound.)
        /// </summary>
        [Test]
        public void FavoursOverOneHouseguestStack()
        {
            var found = RoleStoryMomentsTests.TearsCard(playerHoh: false, StoryAnchors.BlockSet, out var drawn);
            Assert.That(found, Is.Not.Null, "No block brought quiet tears.");
            var owed = drawn.Clone();
            var card = RoleStoryMomentsTests.OpenTears(owed);
            string subject = Subject(card);
            Assert.That(Hooks.Create(owed, owed.playerId, subject, null), Is.Not.Null, "(a favour they already owe)");
            Valid(owed);
            Assert.That(Favours(owed, subject), Is.EqualTo(1));
            var sat = Apply(new EpisodeEngine(owed), Progress(owed, card.id, "sit-a-while"));
            Assert.That(Favours(sat, subject), Is.EqualTo(2), "The seat's favour stands beside the one already owed.");
            Hooks.Spend(sat, sat.playerId, subject);
            Assert.That((Favours(sat, subject), Hooks.Has(sat, sat.playerId, subject)), Is.EqualTo((1, true)), "Calling one in spends one.");
        }

        // ------------------------------------------------------------------ (4) renomination

        /// <summary>
        /// The pawn talk's word taken (R6): the Head of Household's promise of safety to the player, and the player's own back to
        /// them through next week. The player saves themselves with the veto, wins next week's Head of Household and puts that
        /// Head of Household on the block: their own promise is broken, by them, in every commitment mode; naming two others
        /// instead breaks nothing. (The word's making is RoleStoryMomentsTests.TakingTheirWordGivesYourWordBackThroughNextWeek; a
        /// pitcher's promise broken by the nominations, RolePitchBoardTests.APromisePaysAtTheNominationsOnlyThoseLeftOffTheBlock.)
        /// </summary>
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void TakingThePawnsWordThenNominatingThatHeadOfHouseholdBreaksThePlayersOwn(int mode)
        {
            foreach (bool renominate in new[] { true, false })
            {
                string what = "mode " + mode + (renominate ? ", that Head of Household named" : ", two others named");
                var pawn = RoleStoryMomentsTests.PawnWeek(backdoor: false, coin: true, mode: mode);
                var engine = pawn.engine;
                var s = pawn.after.Clone();
                string hoh = pawn.hohId;
                int week = s.week;
                s = Apply(engine, Progress(s, RoleStoryMomentsTests.JustAPawn(s).id, "take-word-roles"));
                Assert.That((OwnWord(s, hoh, week).status, OwnWord(s, hoh, week).expiresWeek), Is.EqualTo((PromiseStatus.Active, week + 1)),
                    what + ": the player's word, through next week.");

                var next = SelfSavedToNextWeeksHoH(engine, week, what);
                Assert.That(next.Find(hoh).status, Is.EqualTo(ContestantStatus.Active), what + ": the Head of Household who called them up is still in.");
                var crowned = next.Clone();
                crowned.hohId = crowned.playerId;
                crowned.competitionResolved = true;
                crowned.competitionScores = EpisodeEngine.CompetitionPlayers(crowned)
                    .Select(c => new CompetitionScore { contestantId = c.id, score = c.id == crowned.playerId ? 10 : 1 }).ToList();
                Valid(crowned);
                var nominating = new EpisodeEngine(crowned);
                var open = Apply(nominating, Command(crowned, EpisodeCommandKind.Advance));
                Assert.That((open.phase, open.hohId, open.nominees.Count), Is.EqualTo((EpisodePhase.Nomination, open.playerId, 0)), what);
                Assert.That(OwnWord(open, hoh, week).status, Is.EqualTo(PromiseStatus.Active), what + ": the word stands at the nominations.");
                var pool = EpisodeEngine.NominationCandidates(open).Where(c => !c.isPlayer && c.id != hoh).Select(c => c.id)
                    .OrderBy(id => id, StringComparer.Ordinal).ToList();
                Assert.That(EpisodeEngine.NominationCandidates(open).Any(c => c.id == hoh), Is.True, what + ": they can be named.");
                var named = Apply(nominating, Command(open, EpisodeCommandKind.Nominate, renominate ? hoh : pool[0], renominate ? pool[0] : pool[1]));
                var word = OwnWord(named, hoh, week);
                if (renominate) Assert.That((word.status, word.brokenById), Is.EqualTo((PromiseStatus.Broken, named.playerId)), what + ": broken, by the player.");
                else Assert.That(word.status, Is.Not.EqualTo(PromiseStatus.Broken), what + ": nothing broken.");
                Valid(named);
            }
        }

        /// <summary>
        /// From the pawn talk's week, on to its veto competition with the player - on the block - handed the veto, as the veto
        /// board's fixtures hand it; the veto used on themselves, so the house cannot vote them out; then the season walked by the
        /// test policy to next week's Head of Household competition, which the player plays in.
        /// </summary>
        private static EpisodeState SelfSavedToNextWeeksHoH(EpisodeEngine engine, int week, string what)
        {
            for (int i = 0; i < 60 && !(engine.Snapshot.phase == EpisodePhase.Veto && !engine.Snapshot.competitionResolved); i++)
                Apply(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
            var s = engine.Snapshot.Clone();
            Assert.That((s.week, s.phase, s.nominees.Contains(s.playerId)), Is.EqualTo((week, EpisodePhase.Veto, true)), what + ": the veto competition, the player on the block.");
            s.vetoHolderId = s.playerId;
            if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
            s.competitionResolved = true;
            s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
            Valid(s);
            var walk = new EpisodeEngine(s);
            var meeting = Apply(walk, Command(s, EpisodeCommandKind.Advance));
            Assert.That(meeting.phase, Is.EqualTo(EpisodePhase.VetoMeeting), what);
            var saved = Apply(walk, RoleVetoBoardTests.Decide(meeting, true, meeting.playerId));
            Assert.That(saved.nominees, Does.Not.Contain(saved.playerId), what + ": saved by their own veto.");
            for (int i = 0; i < 200 && !(walk.Snapshot.week == week + 1 && walk.Snapshot.phase == EpisodePhase.HoH && !walk.Snapshot.competitionResolved); i++)
                Apply(walk, EpisodeEngineTests.NextCommand(walk.Snapshot));
            var next = walk.Snapshot;
            Assert.That((next.week, next.phase, next.competitionResolved), Is.EqualTo((week + 1, EpisodePhase.HoH, false)), what + ": next week's Head of Household.");
            Assert.That(next.Find(next.playerId).status, Is.EqualTo(ContestantStatus.Active), what);
            Assert.That(EpisodeEngine.CompetitionPlayers(next).Any(c => c.isPlayer), Is.True, what + ": the player plays for it.");
            return next;
        }

        // ------------------------------------------------------------------ (5) a stale card after a phase advance

        /// <summary>
        /// The board's asks answered after the veto decision - a yes, to take a word nothing can keep now, or a no, to pay a no's
        /// price in place of the silence the decision charged - are refused in the engine's words when sent against the decision's
        /// revision, and are stale against the meeting's. Nothing moves. (An ask answered twice is
        /// RoleVetoBoardTests.ADoubleClickOrARepeatedCommandCommitsOneYes.)
        /// </summary>
        [Test]
        public void ABoardAskAnsweredAfterTheDecisionIsRefusedAndChangesNothing()
        {
            var engine = RoleKeepTheBlockTests.Board(RoleTone.Calculated, out _, out _, out _);
            var meeting = engine.Snapshot;
            var asks = RoleOffers.BoardAsks(meeting);
            Assert.That(asks, Has.Count.EqualTo(3), "The nominees' asks and the Head of Household's.");
            var decided = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(meeting, false, null));
            Assert.That((decided.phase, decided.vetoResolved), Is.EqualTo((EpisodePhase.VetoMeeting, true)));
            Assert.That(asks.Select(ask => decided.deals.Single(d => d.id == ask.id).status), Has.None.EqualTo(DealStatus.Proposed), "(the decision settled them)");
            string json = Json(engine.Snapshot);
            foreach (var ask in asks)
                foreach (bool yes in new[] { true, false })
                {
                    string what = ask.proposerId + (yes ? "'s yes" : "'s no");
                    var late = engine.Apply(RoleVetoBoardTests.Answer(decided, ask.id, yes));
                    Assert.That((late.accepted, late.reason), Is.EqualTo((false, "That offer is no longer on the table.")), what);
                    var stale = RoleVetoBoardTests.Answer(meeting, ask.id, yes);
                    stale.id = "role-abuse-stale-" + ask.id + "-" + yes;
                    var old = engine.Apply(stale);
                    Assert.That(old.accepted, Is.False, what);
                    Assert.That(old.reason, Does.StartWith("The episode changed"), what + ": sent against the meeting's revision.");
                }
            Assert.That(Json(engine.Snapshot), Is.EqualTo(json), "Nothing moved.");
        }

        /// <summary>
        /// A pitch answered after the nominations - a promise to collect the +8 for someone already left off, a no to ease a grudge
        /// already earned, a question - is refused ("That moment has passed.") and moves nothing: the names said, every card is gone.
        /// </summary>
        [Test]
        public void APitchAnsweredAfterTheNominationsIsRefusedAndChangesNothing()
        {
            var engine = RolePitchBoardTests.Dealt(RolePitchBoardTests.Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var dealt = engine.Snapshot;
            var cards = board.Select(id => CardOf(dealt, id).id).ToList();
            var others = dealt.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            var named = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Command(dealt, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(named.replyCards, Is.Empty, "(the names said, the cards are gone)");
            string json = Json(engine.Snapshot);
            foreach (string card in cards)
                foreach (string key in new[] { HoHPitches.PromiseKey, HoHPitches.TurnDownKey, HoHPitches.HearKey, HoHPitches.FeelOutKey })
                {
                    var late = engine.Apply(RolePitchBoardTests.Answer(named, card, key));
                    Assert.That((late.accepted, late.reason), Is.EqualTo((false, "That moment has passed.")), card + " " + key);
                }
            Assert.That(Json(engine.Snapshot), Is.EqualTo(json), "Nothing moved.");
        }

        /// <summary>
        /// The pawn card left unanswered lapses at the veto competition, where it closes, on saying nothing: no promise either way,
        /// no seat and nothing moved (its lapse has no effects; taking their word's +3 and promises are that answer's alone). Every
        /// answer after it - their word, in writing, calling it out - is refused ("That moment has already passed.") and moves nothing.
        /// </summary>
        [Test]
        public void ThePawnCardAnsweredAfterTheVetoCompetitionIsRefusedAndItsLapseWritesNothing()
        {
            var pawn = RoleStoryMomentsTests.PawnWeek(backdoor: false, coin: true);
            var engine = pawn.engine;
            var drawn = pawn.after.Clone();
            string hoh = pawn.hohId, player = drawn.playerId;
            var card = RoleStoryMomentsTests.JustAPawn(drawn);
            Assert.That(card.choices.Single(c => c.optionId == card.lapseOptionId).effects, Is.Empty, "Saying nothing moves nothing.");
            for (int i = 0; i < 60 && !engine.Snapshot.houseEvents.Single(e => e.id == card.id).resolved; i++)
                Apply(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
            var lapsed = engine.Snapshot;
            var item = lapsed.houseEvents.Single(e => e.id == card.id);
            Assert.That((item.resolved, item.choices[item.chosenIndex].optionId), Is.EqualTo((true, "say-nothing")), "Unanswered, it lapsed on saying nothing,");
            Assert.That((lapsed.week, lapsed.phase), Is.EqualTo((drawn.week, EpisodePhase.VetoMeeting)), "at the veto competition, where it closes.");
            Assert.That(CommitmentReferences.Promises(lapsed).Where(p => p.kind == PromiseKind.Safety && p.week == drawn.week
                && ((p.fromId == hoh && p.toId == player) || (p.fromId == player && p.toId == hoh))), Is.Empty, "The lapse writes no promise,");
            Assert.That(lapsed.windowActions, Is.EqualTo(drawn.windowActions), "and spends no seat.");
            Assert.That((lapsed.socialActions, lapsed.outOfPhaseSocialActions), Is.EqualTo((drawn.socialActions, drawn.outOfPhaseSocialActions)), "(nor an action)");

            string json = Json(lapsed);
            foreach (string option in new[] { "take-word-roles", "in-writing-roles", "call-it", "say-nothing" })
            {
                var late = engine.Apply(Progress(lapsed, card.id, option));
                Assert.That((late.accepted, late.reason), Is.EqualTo((false, "That moment has already passed.")), option);
            }
            Assert.That(Json(engine.Snapshot), Is.EqualTo(json), "Nothing moved.");
        }

        // ------------------------------------------------------------------ (7) a double click

        /// <summary>
        /// A promise to a pitcher pressed twice commits once: the same command again is a duplicate, a second press against the
        /// old revision is stale, and one against the new finds the card gone - one promise of safety stands, and the nominations
        /// pay its +8 once. (A veto yes pressed twice is RoleVetoBoardTests.ADoubleClickOrARepeatedCommandCommitsOneYes.)
        /// </summary>
        [Test]
        public void APromiseToAPitcherPressedTwiceCommitsOnceAndPaysOnce()
        {
            var engine = RolePitchBoardTests.Dealt(RolePitchBoardTests.Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var dealt = engine.Snapshot;
            string card = CardOf(dealt, board[0]).id;
            var promise = RolePitchBoardTests.Answer(dealt, card, HoHPitches.PromiseKey);
            var promised = RolePitchBoardTests.Apply(engine, promise);
            string after = Json(promised);

            var again = engine.Apply(promise);
            Assert.That((again.accepted, again.duplicate), Is.EqualTo((false, true)), "The same command: already committed.");
            var stale = RolePitchBoardTests.Answer(dealt, card, HoHPitches.PromiseKey);
            stale.id = "role-abuse-double-promise";
            var late = engine.Apply(stale);
            Assert.That(late.accepted, Is.False);
            Assert.That(late.reason, Does.StartWith("The episode changed"), "A second press against the revision the first changed.");
            var gone = engine.Apply(RolePitchBoardTests.Answer(promised, card, HoHPitches.PromiseKey));
            Assert.That((gone.accepted, gone.reason), Is.EqualTo((false, "That moment has passed.")), "Sent again against the new revision.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(after), "One promise, and nothing else, was committed.");
            Assert.That(CommitmentReferences.Promises(promised).Count(p => p.kind == PromiseKind.Safety && p.fromId == promised.playerId && p.toId == board[0]
                && p.status == PromiseStatus.Active), Is.EqualTo(1), "One promise of safety.");

            var others = promised.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            var named = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Command(promised, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(named.relationships.Where(r => r.fromId == named.playerId && r.toId == board[0]).SelectMany(r => r.events)
                .Count(e => e.week == named.week && e.type == "reply" && e.description == "Kept me off the block, as promised."), Is.EqualTo(1),
                "The nominations pay the promise once.");
        }

        /// <summary>
        /// Sitting with quiet tears a while, pressed twice, spends one seat and leaves one favour: the same command again is a
        /// duplicate, a second press against the old revision is stale, and any answer against the new finds the moment passed.
        /// </summary>
        [Test]
        public void ASeatWithQuietTearsPressedTwiceSpendsOneSeatAndLeavesOneFavour()
        {
            var engine = RoleStoryMomentsTests.TearsCard(playerHoh: false, StoryAnchors.BlockSet, out var drawn);
            Assert.That(engine, Is.Not.Null, "No block brought quiet tears.");
            var s = drawn.Clone();
            var card = RoleStoryMomentsTests.OpenTears(s);
            string subject = Subject(card);
            int spent = EpisodeEngine.SocialActionsSpent(s), held = Favours(s, subject);
            var sit = Progress(s, card.id, "sit-a-while");
            var sat = Apply(engine, sit);
            string after = Json(sat);

            var again = engine.Apply(sit);
            Assert.That((again.accepted, again.duplicate), Is.EqualTo((false, true)), "The same command: already committed.");
            var stale = Progress(s, card.id, "sit-a-while");
            stale.id = "role-abuse-double-seat";
            var late = engine.Apply(stale);
            Assert.That(late.accepted, Is.False);
            Assert.That(late.reason, Does.StartWith("The episode changed"), "A second press against the revision the first changed.");
            foreach (string option in new[] { "sit-a-while", "comfort", "pretend" })
            {
                var gone = engine.Apply(Progress(sat, card.id, option));
                Assert.That((gone.accepted, gone.reason), Is.EqualTo((false, "That moment has already passed.")), option + " against the new revision.");
            }
            Assert.That(Json(engine.Snapshot), Is.EqualTo(after), "One seat, and nothing else, was committed.");
            Assert.That(EpisodeEngine.SocialActionsSpent(sat), Is.EqualTo(spent + 1), "One seat,");
            Assert.That(Favours(sat, subject), Is.EqualTo(held + 1), "and one favour.");
        }

        // ------------------------------------------------------------------ (9) an interrupted NPC approach

        /// <summary>
        /// The lead's Q8 (R7): an invitation (<see cref="NpcInvitation.Available"/>) - a houseguest walking over to raise an offer,
        /// the director's way whenever the asks are off (every PlayMode fixture, every verification route, every batch run) -
        /// could raise a board ask in the one window it is open in. At the meeting the asks wait and are the player's to answer,
        /// the phase is one an invitation plays in, and the conversation is open with every asker - the week's windows, the Head
        /// of Household a decider besides - as the director's own gate reads it (mirrored from
        /// EpisodeDirector.NpcInvitationConversationAvailable). So under the role moments the invitation raises none of the board's
        /// asks, whose no and silence carry a price only the board says, while an ordinary offer the same week is still raised and
        /// the asks still wait on the player; a meeting without the role moments raises the nominees' asks as it always did. (The
        /// queue the asks come to the player in keeps them on the board too: ComesToYouTests.RoleBoardAsksStayOnTheBoard.)
        /// </summary>
        [Test]
        public void AnInvitationNeverRaisesTheVetoBoardsOwnAsks()
        {
            var meeting = RoleKeepTheBlockTests.Board(RoleTone.Calculated, out string first, out string second, out string hoh).Snapshot;
            var board = RoleOffers.BoardAsks(meeting);
            Assert.That(board.Select(d => d.proposerId), Is.EqualTo(new[] { meeting.nominees[0], meeting.nominees[1], hoh }), "The board: both nominees and the Head of Household.");
            Assert.That(NpcInvitation.PhaseAllows(meeting.phase), Is.True, "An invitation plays at the meeting,");
            foreach (var ask in board)
            {
                Assert.That(ConversationOpen(meeting, ask.proposerId), Is.True, "the conversation is open with " + ask.proposerId + ",");
                foreach (bool yes in new[] { true, false })
                    Assert.That(new EpisodeEngine(meeting).Apply(RoleVetoBoardTests.Answer(meeting, ask.id, yes)).accepted, Is.True,
                        "and " + ask.id + " is the player's to answer.");
            }

            var s = meeting.Clone();
            string other = s.Active.First(c => !c.isPlayer && c.id != first && c.id != second && c.id != hoh).id;
            s.deals.Add(new DealState
            {
                id = NpcDeals.OfferPrefix + "901", type = DealKind.Partnership, proposerId = other, recipientId = s.playerId,
                status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
            });
            var raised = NpcInvitation.Available(s).Select(row => row.RecordId).ToList();
            Assert.That(raised.Intersect(board.Select(d => d.id)), Is.Empty, "No invitation raises a board ask: " + string.Join(", ", raised));
            Assert.That(raised, Does.Contain(NpcDeals.OfferPrefix + "901"), "An ordinary offer the same week is still raised,");
            Assert.That(WaitingOnYou.Read(s).items.Select(item => item.id), Is.SupersetOf(board.Select(d => d.id)), "and the asks still wait on the player.");

            var old = RoleKeepTheBlockTests.Board(RoleTone.Calculated, out _, out _, out _, roles: false).Snapshot;
            Assert.That((old.story.rulesVersion, EpisodeEngine.RolesOn(old)), Is.EqualTo((StoryRules.Threads, false)));
            Assert.That(NpcInvitation.Available(old).Select(row => row.RecordId), Is.SupersetOf(RoleOffers.VetoAsks(old).Select(d => d.id)),
                "Without the role moments the nominees' asks are raised, as they always were.");
        }

        /// <summary>The director's gate on an invitation's conversation (EpisodeDirector.NpcInvitationConversationAvailable), read on the state.</summary>
        private static bool ConversationOpen(EpisodeState s, string npcId) => s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign
            || StrategyRules.IsDecider(s, npcId) || (EpisodeEngine.WeekRulesOn(s) && EpisodeEngine.Window(s) != Windows.None);

        // ------------------------------------------------------------------ fixtures

        private static string Subject(HouseEventState card) => card.cast.Single(r => r.role == "SUBJECT").contestantId;

        /// <summary>The player's unspent favours over a houseguest.</summary>
        private static int Favours(EpisodeState s, string overId) =>
            s.story.hooks.Count(h => !h.spent && h.holderId == s.playerId && h.overId == overId);

        /// <summary>The player's promise of safety to a houseguest, made in a week, in whichever store holds it.</summary>
        private static PromiseState OwnWord(EpisodeState s, string toId, int week) =>
            CommitmentReferences.Promises(s).Single(p => p.kind == PromiseKind.Safety && p.fromId == s.playerId && p.toId == toId && p.week == week);

        private static ReplyCardState CardOf(EpisodeState s, string fromId) => s.replyCards.Single(c => c.kind == ReplyCards.Pitch && c.fromId == fromId);

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);

        internal static EpisodeState Apply(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + " " + command.targetId + " " + command.secondTargetId + ": " + result.reason);
            return result.state;
        }

        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "role-abuse-" + kind + "-" + s.revision + "-" + target + "-" + second + "-" + text, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        internal static EpisodeCommand Progress(EpisodeState s, string eventId, string optionId) =>
            Command(s, EpisodeCommandKind.ProgressStoryline, eventId, optionId);
    }
}
