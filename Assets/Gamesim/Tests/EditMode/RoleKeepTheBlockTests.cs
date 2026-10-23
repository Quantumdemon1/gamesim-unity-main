using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Head of Household's "Don't Use the Veto" (ROLE-MOMENTS-PLAN R4, §3.3), through the public engine. Under the role
    /// moments the Head of Household asks the player who holds the veto to leave the block alone - a keep-the-block deal
    /// filed right after the nominees' asks, roll-free, once a week, and only as a counter-offer to theirs, with every one
    /// of the seven conditions the plan names; its card stands after the nominees' on the board, priced at a final two at
    /// the endgame and at nothing before it. A yes is the player's one word on the veto: it locks a nominee's yes and a
    /// nominee's locks it. The decision judges it: unused, kept for +8; used, broken by the player at -45, with the
    /// breach's witnesses, record and price voided. A no and silence cost the tone's, and the veto used anyway after
    /// either costs -5 and a grudge of fifteen, a Threatening Head of Household's thirty; a Bitter one kept eases. Every
    /// commitment mode plays it alike, validation refuses it without the role moments, the player can never put one, and
    /// every reader reads it as what it is. A season without the role moments - every committed fixture - never sees one.
    /// Pure simulation: the Unity-free subset runs it. <c>RoleKeepTheBlockPersistenceTests</c> reloads the board through
    /// the shipping save store.
    /// </summary>
    public sealed class RoleKeepTheBlockTests
    {
        private const string HeadOfHousehold = RoleTone.Role.HeadOfHousehold;

        // ------------------------------------------------------------------ filing

        /// <summary>
        /// The Head of Household asks right after the nominees, once, as the plan's row: proposed by them to the player,
        /// binding this week, High at stake, the kind and the id the role moments' own; told to the player, and drawn
        /// with no roll. The board shows the nominees in the order they were named, then the Head of Household.
        /// </summary>
        [Test]
        public void TheHeadOfHouseholdAsksRightAfterTheNomineesAsThePlansRow()
        {
            var engine = Board(RoleTone.Calculated, out string first, out string second, out string hoh);
            var meeting = engine.Snapshot;
            var hold = HoldOf(meeting);
            Assert.That((hold.type, hold.status, hold.week, hold.expiresWeek, hold.trustImpact, hold.proposerId, hold.recipientId, hold.targetId, hold.linkedDealId),
                Is.EqualTo((DealKind.VetoHold, DealStatus.Proposed, meeting.week, meeting.week, DealTrust.High, hoh, meeting.playerId, (string)null, (string)null)));
            Assert.That(hold.id, Does.StartWith(NpcDeals.KeepTheBlockPrefix));
            Assert.That(meeting.deals.Count(d => NpcDeals.IsKeepTheBlockId(d.id)), Is.EqualTo(1), "One a week.");
            long filed = long.Parse(hold.id.Substring(NpcDeals.KeepTheBlockPrefix.Length));
            Assert.That(meeting.deals.Where(d => RoleOffers.IsVetoAsk(meeting, d)).Select(d => long.Parse(d.id.Substring(NpcDeals.VetoAskPrefix.Length))),
                Has.All.LessThan(filed), "Filed right after the nominees' asks.");
            Assert.That(meeting.events.Count(e => e.kind == "deal" && e.text == meeting.Find(hoh).name
                + " is Head of Household and wants your word that the nominations stay as they are."), Is.EqualTo(1), "The player is told.");
            Assert.That(RoleOffers.BoardAsks(meeting).Select(d => d.proposerId), Is.EqualTo(new[] { meeting.nominees[0], meeting.nominees[1], hoh }),
                "The board: the nominees in the order they were named, then the Head of Household.");
            Assert.That(RoleOffers.VetoAsks(meeting).Select(d => d.proposerId), Is.EquivalentTo(new[] { first, second }), "The nominees' asks are still theirs alone.");
            Assert.That(EpisodeValidation.TryValidate(meeting, out string error), Is.True, error);

            var s = Basis(meeting);
            uint before = s.randomState;
            NpcDeals.AskToKeepTheBlock(s);
            Assert.That(s.randomState, Is.EqualTo(before), "Filing draws nothing from the season's stream.");
            Assert.That(s.deals.Count(d => NpcDeals.IsKeepTheBlockId(d.id)), Is.EqualTo(1));
        }

        /// <summary>
        /// Every one of the seven conditions is needed (§3.3), and the once-a-week guard counts any ask of the week, answered
        /// or not, and none of an earlier week's: each case below, alone, keeps the Head of Household from asking, while the
        /// same state without it asks.
        /// The backdoor is read from the story's plan, never from the player's own backdoor target.
        /// </summary>
        [Test]
        public void EachConditionAloneKeepsTheAskAway()
        {
            var meeting = Board(RoleTone.Pragmatic, out string first, out string second, out string hoh).Snapshot;
            Assert.That(Filed(Basis(meeting)), Is.True, "The control: the same state asks.");
            var cases = new (string name, Action<EpisodeState> change)[]
            {
                ("without the role moments", s => s.story.rulesVersion = StoryRules.Threads),
                // The player as Head of Household, reading themselves warmly enough to bargain, so only this condition stands in the way.
                ("the player is Head of Household", s => { s.hohId = s.playerId; Set(s, s.playerId, s.playerId, 50); }),
                ("the Head of Household has left the house", s => s.Find(hoh).status = ContestantStatus.Expelled),
                ("the player does not hold the veto", s => s.vetoHolderId = first),
                ("the veto is decided", s => s.vetoResolved = true),
                ("the player is on the block", s => s.nominees[1] = s.playerId),
                ("the player is out of the game", s => s.Find(s.playerId).status = ContestantStatus.Expelled),
                ("the veto cannot be used: the final four", s =>
                {
                    foreach (var other in s.Active.Where(c => !c.isPlayer && c.id != hoh && !s.nominees.Contains(c.id)).ToList()) other.status = ContestantStatus.Jury;
                    Assert.That(StrategyRules.VetoCanBeUsed(s), Is.False);
                }),
                ("too cold to bargain", s => Set(s, hoh, s.playerId, NpcDeals.DealingFloor - 1)),
                ("too cold once the player's broken deal is counted", s =>
                {
                    Set(s, hoh, s.playerId, NpcDeals.DealingFloor + NpcDeals.BrokenDealPenalty - 1);
                    BreakAWord(s, second);
                }),
                ("no nominee asked", s => s.deals.RemoveAll(d => RoleOffers.IsVetoAsk(s, d))),
                ("the nominees' asks are answered", s => { foreach (var ask in s.deals.Where(d => RoleOffers.IsVetoAsk(s, d))) ask.status = DealStatus.Declined; }),
                ("a backdoor plan", s => PlanABackdoor(s, hoh, first)),
                ("an ask of theirs already this week", s => s.deals.Add(new DealState
                {
                    id = NpcDeals.KeepTheBlockPrefix + "0", type = DealKind.VetoHold, proposerId = hoh, recipientId = s.playerId,
                    status = DealStatus.Declined, week = s.week, expiresWeek = s.week, trustImpact = DealTrust.High,
                })),
                ("the season's deal ceiling", s => Fill(s, NpcDeals.DealCeiling)),
            };
            foreach (var (name, change) in cases)
            {
                var s = Basis(meeting);
                change(s);
                Assert.That(Filed(s), Is.False, name + ": no ask.");
            }

            // The boundaries: at the floor exactly they ask, and one deal short of the ceiling.
            var warm = Basis(meeting);
            Set(warm, hoh, warm.playerId, NpcDeals.DealingFloor);
            Assert.That(Filed(warm), Is.True, "At the dealing floor exactly.");
            var room = Basis(meeting);
            Fill(room, NpcDeals.DealCeiling - 1);
            Assert.That(Filed(room), Is.True, "One deal under the ceiling.");
            // Once a week, not once a season: last week's ask of theirs, turned down, keeps nothing away this week.
            var lastWeek = Basis(meeting);
            Assert.That(lastWeek.week, Is.GreaterThan(1), "(a week before this one)");
            lastWeek.deals.Add(new DealState
            {
                id = NpcDeals.KeepTheBlockPrefix + "0", type = DealKind.VetoHold, proposerId = hoh, recipientId = lastWeek.playerId,
                status = DealStatus.Declined, week = lastWeek.week - 1, expiresWeek = lastWeek.week - 1, trustImpact = DealTrust.High,
            });
            Assert.That(Filed(lastWeek), Is.True, "Last week's ask leaves this week's to be asked.");
            // One nominee's ask is enough, and the player's own backdoor target is no plan of the Head of Household's.
            var one = Basis(meeting);
            one.deals.RemoveAll(d => RoleOffers.IsVetoAsk(one, d) && d.proposerId == second);
            Assert.That(Filed(one), Is.True, "One nominee's ask is a choice to make.");
            var target = Basis(meeting);
            target.backdoorTargetId = first;
            Assert.That(EpisodeEngine.BackdoorPlanned(target), Is.Null);
            Assert.That(Filed(target), Is.True, "The backdoor is read from the story's plan, never from the player's own target.");
            var planned = Basis(meeting);
            PlanABackdoor(planned, hoh, first);
            Assert.That(EpisodeEngine.BackdoorPlanned(planned), Is.EqualTo(first), "(the planted plan reads as one)");
        }

        /// <summary>
        /// Through the engine: with no nominee warm enough to ask, the Head of Household asks nothing either - the ask is a
        /// counter-offer, never a bonus; a nominated holder is asked nothing by anybody; and before the role moments the
        /// Head of Household never asks.
        /// </summary>
        [Test]
        public void ThroughTheEngineTheAskIsOnlyACounterOffer()
        {
            var s = BeforeTheMeeting(RoleTone.Pragmatic, out string first, out string second, out string hoh);
            Set(s, first, s.playerId, 0);
            Set(s, second, s.playerId, 0);
            var cold = RoleVetoBoardTests.Apply(new EpisodeEngine(s), RoleVetoBoardTests.Command(s, EpisodeCommandKind.Advance));
            Assert.That(cold.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(cold.deals.Where(d => RoleOffers.IsBoardAsk(cold, d)), Is.Empty, "No nominee asked, so the Head of Household does not.");

            var without = BeforeTheMeeting(RoleTone.Pragmatic, out _, out _, out _, roles: false);
            var old = RoleVetoBoardTests.Apply(new EpisodeEngine(without), RoleVetoBoardTests.Command(without, EpisodeCommandKind.Advance));
            Assert.That(old.deals.Count(d => RoleOffers.IsVetoAsk(old, d)), Is.EqualTo(2), "Before the role moments the nominees ask,");
            Assert.That(old.deals.Where(d => d.type == DealKind.VetoHold || NpcDeals.IsKeepTheBlockId(d.id)), Is.Empty, "and the Head of Household never does.");

            var nominated = NominatedHolder();
            var asked = RoleVetoBoardTests.Apply(new EpisodeEngine(nominated), RoleVetoBoardTests.Command(nominated, EpisodeCommandKind.Advance));
            Assert.That(asked.deals.Where(d => RoleOffers.IsBoardAsk(asked, d)), Is.Empty, "A holder on the block is asked nothing.");
        }

        // ------------------------------------------------------------------ the verdict

        /// <summary>
        /// Taken: +4, as every offer's yes, the row standing for the week. Kept - the veto unused - the decision settles it
        /// Fulfilled, kept by the player, at the Low weight: +8 on how the Head of Household reads the player, and nothing
        /// against the player's word.
        /// </summary>
        [Test]
        public void TakenAndKeptTheHeadOfHouseholdThinksEightBetterOfThePlayer()
        {
            var engine = Board(RoleTone.Calculated, out _, out _, out string hoh);
            var meeting = engine.Snapshot;
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, HoldOf(meeting).id, true));
            var hold = HoldOf(taken);
            Assert.That((hold.status, hold.expiresWeek), Is.EqualTo((DealStatus.Active, taken.week)));
            Assert.That(RoleVetoBoardTests.Events(taken, taken.playerId, hoh, "deal_accepted").Select(e => e.impactScore),
                Is.EqualTo(new[] { PlayerDeals.CommittedAcceptedImpact }), "A yes is +4.");
            Assert.That(taken.events.Count(e => e.kind == "deal" && e.text == "You accepted a keep-the-block deal from " + taken.Find(hoh).name + "."), Is.EqualTo(1));
            int broken = NpcDeals.BrokenDeals(taken, taken.playerId);

            var kept = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, false, null));
            hold = HoldOf(kept);
            Assert.That((hold.status, hold.brokenById, hold.settledWeek), Is.EqualTo((DealStatus.Fulfilled, (string)null, kept.week)));
            Assert.That(DealResolution.Impact(kept, hold, DealStatus.Fulfilled), Is.EqualTo(8), "Kept at the Low weight, not its High.");
            Assert.That(RoleVetoBoardTests.Events(kept, hoh, kept.playerId, "deal_fulfilled").Select(e => e.impactScore), Is.EqualTo(new[] { 8.0 }),
                "+8 with the Head of Household, kept by the player.");
            Assert.That(kept.events.Count(e => e.kind == "deal-outcome" && e.text == "You honoured a keep-the-block deal with " + kept.Find(hoh).name + "."), Is.EqualTo(1));
            Assert.That(NpcDeals.BrokenDeals(kept, kept.playerId), Is.EqualTo(broken));
            Assert.That(CommitmentsRead.With(kept, hoh).Single(c => c.id == hold.id).outcome, Is.EqualTo(CommitmentsRead.Outcomes.Kept));
            Assert.That(AllEvents(kept, EpisodeEngine.DefiedAskEvent), Is.Empty);
            Assert.That(EpisodeValidation.TryValidate(kept, out string error), Is.True, error);
        }

        /// <summary>
        /// Taken and broken - the veto used, on either nominee - the player broke it: -45, High raised to Critical for an
        /// accepted offer, on how the Head of Household reads the player, written as a breach against the player's word,
        /// with the usual witnesses (the house's knowledge of it), the breach's grudge, -15 on how the house sizes the player
        /// up, the Your word page's "broken by you", and the dry run's warning before the decision.
        /// </summary>
        [Test]
        public void TakenAndBrokenThePlayerBreaksItAtFortyFive()
        {
            foreach (bool saveFirst in new[] { true, false })
            {
                var engine = Board(RoleTone.Calculated, out string first, out string second, out string hoh);
                var meeting = engine.Snapshot;
                var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, HoldOf(meeting).id, true));
                string saved = saveFirst ? first : second;
                var warning = CommitmentsRead.Warning(taken, CommitmentsRead.WouldBreak(taken, CommitmentsRead.Decision.Veto(true, saved)));
                Assert.That(warning, Is.EqualTo("Saving " + FinalistRead.FirstName(taken.Find(saved).name) + " breaks your keep-the-block deal with "
                    + FinalistRead.FirstName(taken.Find(hoh).name) + "."), "The decision screen warns first.");
                Assert.That(CommitmentsRead.WouldBreak(taken, CommitmentsRead.Decision.Veto(false)), Is.Empty, "Not using it breaks nothing.");
                int broken = NpcDeals.BrokenDeals(taken, taken.playerId);

                var used = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, true, saved));
                var hold = HoldOf(used);
                Assert.That((hold.status, hold.brokenById, hold.settledWeek), Is.EqualTo((DealStatus.Broken, used.playerId, used.week)), "save " + saved);
                Assert.That(DealResolution.AcceptedOffer(used, hold), Is.True);
                Assert.That(DealResolution.Impact(used, hold, DealStatus.Broken), Is.EqualTo(-45), "High, raised to Critical for an accepted offer.");
                Assert.That(RoleVetoBoardTests.Events(used, hoh, used.playerId, "deal_broken").Select(e => e.impactScore), Is.EqualTo(new[] { -45.0 }),
                    "-45 with the Head of Household, broken by the player.");
                Assert.That(used.events.Count(e => e.kind == "deal-outcome" && e.text == "You broke a keep-the-block deal with " + used.Find(hoh).name + "."), Is.EqualTo(1));
                Assert.That(NpcDeals.BrokenDeals(used, used.playerId), Is.EqualTo(broken + 1),
                    "Against the player's word: every houseguest sizes them up fifteen colder.");
                Assert.That(YourWord.Breaches(used).Any(f => f.refId == hold.id && f.actorId == used.playerId && f.subjectId == hoh), Is.True,
                    "The house knows of it: the usual witnesses.");
                Assert.That(used.story.grudges.Single(g => g.holderId == hoh && g.targetId == used.playerId).cause, Is.EqualTo(GrudgeCauses.DealBroken));
                Assert.That(FinalistRead.DealBreaker(used, hold), Is.EqualTo(used.playerId));
                var page = CommitmentsRead.With(used, hoh).Single(c => c.id == hold.id);
                Assert.That((page.outcome, page.brokenById, page.status, page.title, page.binds),
                    Is.EqualTo((CommitmentsRead.Outcomes.Broken, used.playerId, "broken by you", "Keep-the-block deal they offered", CommitmentsRead.KeepTheBlockBinds)));
                Assert.That(AllEvents(used, EpisodeEngine.DefiedAskEvent), Is.Empty, "A breach, not a defiance: no -5 on top.");
                Assert.That(EpisodeValidation.TryValidate(used, out string error), Is.True, error);
            }
        }

        /// <summary>
        /// The kind's own rules, unit by unit: the Hold verdict keeps an unused veto and breaks a used one, judges only a deal
        /// the holder is party to, and nothing else; kept it weighs Low wherever a kept deal is weighed - the ledger and the
        /// jury - and broken its own, one step heavier for the player's yes; the other kinds weigh what they always did.
        /// </summary>
        [Test]
        public void TheHoldVerdictAndItsWeights()
        {
            var hold = new DealState { id = NpcDeals.KeepTheBlockPrefix + "1", type = DealKind.VetoHold, proposerId = "hoh", recipientId = "player", trustImpact = DealTrust.High };
            Assert.That(DealResolution.Hold(hold, "player", false), Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(DealResolution.Hold(hold, "player", true), Is.EqualTo(DealStatus.Broken));
            Assert.That(DealResolution.Hold(hold, "someone", true), Is.Null, "Only the holder's own deal answers to their decision.");
            Assert.That(DealResolution.Hold(new DealState { type = DealKind.VetoUse, proposerId = "hoh", recipientId = "player" }, "player", true), Is.Null);
            Assert.That(DealResolution.Hold(null, "player", true), Is.Null);
            Assert.That(DealResolution.Veto(hold, "player", "x", true, true), Is.Null, "The veto deal's rule never judges it.");
            Assert.That(DealResolution.KeptWeight(hold), Is.EqualTo(DealTrust.Weight(DealTrust.Low)));
            Assert.That(DealResolution.Impact(hold, DealStatus.Fulfilled), Is.EqualTo(8));
            Assert.That(DealResolution.Impact(hold, DealStatus.Broken), Is.EqualTo(-30), "Broken, its own High.");
            foreach (string kind in DealKind.All.Where(kind => kind != DealKind.VetoHold))
            foreach (string trust in DealTrust.All)
            {
                var other = new DealState { type = kind, trustImpact = trust };
                Assert.That(DealResolution.KeptWeight(other), Is.EqualTo(DealTrust.Weight(trust)), kind + " " + trust + " weighs its own kept.");
            }
            Assert.That(DealKind.DefaultTrust(DealKind.VetoHold), Is.EqualTo(DealTrust.High));

            // The jury weighs a kept one Low, and an accepted one broken at Critical - past the jury's floor of -50 on its own.
            // Mode 0, where the raw lists are all there is between them.
            var engine = Board(RoleTone.Calculated, out string first, out _, out string hoh, mode: 0);
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(engine.Snapshot, HoldOf(engine.Snapshot).id, true));
            var used = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, true, first));
            Assert.That(DealResolution.BreachWeight(used, HoldOf(used)), Is.EqualTo(DealTrust.Weight(DealTrust.Critical)), "Broken after the player's yes: Critical.");
            var alone = Alone(used, hoh);
            Assert.That(WebJuryVoting.Obligations(alone, hoh, alone.playerId), Is.EqualTo(-50), "The breach alone takes the jury's floor.");
            HoldOf(alone).status = DealStatus.Fulfilled;
            HoldOf(alone).brokenById = null;
            Assert.That(WebJuryVoting.Obligations(alone, hoh, alone.playerId), Is.EqualTo(20 * DealTrust.Weight(DealTrust.Low)), "Kept: +20, at the Low weight, not its High's +40.");
        }

        // ------------------------------------------------------------------ the price

        /// <summary>
        /// The price is a final two at the endgame, struck with the yes as a plain deal - never a vote, which the nominees'
        /// asks already hand out - and nothing where one already binds them or before the endgame. The breach voids it;
        /// kept, it stands. The card's terms say which.
        /// </summary>
        [Test]
        public void ThePriceIsAFinalTwoAtTheEndgameStruckAndVoidedByTheBreach()
        {
            var early = Board(RoleTone.Calculated, out _, out _, out _).Snapshot;
            Assert.That(EpisodeEngine.LeverRulesOn(early), Is.True, "The levers run, so a vote price would fit,");
            Assert.That(Negotiation.AskPrice(early, HoldOf(early)), Is.Null, "but before the endgame the Head of Household offers nothing:");
            Assert.That(RoleOffers.KeepTheBlockCard(early, HoldOf(early)).terms, Is.EqualTo("In return: nothing promised"));
            Assert.That(Negotiation.AskPrice(early, RoleVetoBoardTests.AskOf(early, early.nominees[0])), Is.Not.Null, "(a nominee's ask carries their vote)");

            foreach (bool keep in new[] { true, false })
            {
                var engine = new EpisodeEngine(Endgame(RoleTone.Calculated, out string first, out _, out string hoh));
                var meeting = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Command(engine.Snapshot, EpisodeCommandKind.Advance));
                var ask = HoldOf(meeting);
                var price = Negotiation.AskPrice(meeting, ask);
                Assert.That((price.kind, price.payerId, price.payeeId, price.aboutId, price.expiresWeek), Is.EqualTo((DealKind.FinalTwo, hoh, meeting.playerId, (string)null, 0)));
                Assert.That(RoleOffers.KeepTheBlockCard(meeting, ask).terms, Is.EqualTo("In return: a final two"));
                var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, ask.id, true));
                var struck = Negotiation.PriceOf(taken, HoldOf(taken));
                Assert.That((struck.type, struck.status, struck.proposerId, struck.recipientId, struck.linkedDealId, struck.expiresWeek),
                    Is.EqualTo((DealKind.FinalTwo, DealStatus.Active, hoh, taken.playerId, ask.id, 0)), "Struck with the yes, a plain final two.");
                Assert.That(taken.events.Count(e => e.text == "For your word on the veto, " + taken.Find(hoh).name + " owes you a final two."), Is.EqualTo(1));
                Assert.That(DealResolution.AcceptedOffer(taken, struck), Is.True, "Part of the bargain the yes accepted.");
                var decided = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, !keep, keep ? null : first));
                var after = decided.deals.Single(d => d.id == struck.id);
                if (keep) Assert.That(after.status, Is.EqualTo(DealStatus.Active), "Kept, the price stands.");
                else
                {
                    Assert.That(after.status, Is.EqualTo(DealStatus.Expired), "Broken by the player, the price is void,");
                    Assert.That(Negotiation.Voided(decided, after), Is.True);
                    Assert.That(decided.events.Count(e => e.text == Negotiation.VoidedLine(decided, after, HoldOf(decided))), Is.EqualTo(1), "and the two of them are told.");
                    Assert.That(CommitmentsRead.With(decided, hoh).Single(c => c.id == after.id).status, Is.EqualTo(CommitmentsRead.VoidedWord));
                }
                Assert.That(EpisodeValidation.TryValidate(decided, out string error), Is.True, error);
            }

            var bound = Endgame(RoleTone.Calculated, out _, out _, out string boundHoh);
            bound.deals.Add(new DealState
            {
                id = "deal-npc-final-two", type = DealKind.FinalTwo, proposerId = boundHoh, recipientId = bound.playerId, status = DealStatus.Active,
                week = bound.week, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.FinalTwo),
            });
            var boundMeeting = RoleVetoBoardTests.Apply(new EpisodeEngine(bound), RoleVetoBoardTests.Command(bound, EpisodeCommandKind.Advance));
            Assert.That(Negotiation.AskPrice(boundMeeting, HoldOf(boundMeeting)), Is.Null, "A final two already binds them: nothing more to offer.");
            Assert.That(RoleOffers.Terms(boundMeeting, HoldOf(boundMeeting)), Is.EqualTo("In return: nothing promised"));

            // Only the Head of Household's own ask carries one, and only under the role moments.
            var start = Endgame(RoleTone.Calculated, out _, out _, out _);
            var endgame = RoleVetoBoardTests.Apply(new EpisodeEngine(start), RoleVetoBoardTests.Command(start, EpisodeCommandKind.Advance));
            var real = HoldOf(endgame);
            Assert.That(Negotiation.AskPrice(endgame, real), Is.Not.Null);
            var story = real.Clone();
            story.id = "deal-story-1";
            Assert.That(Negotiation.AskPrice(endgame, story), Is.Null, "A keep-the-block deal by any other id carries no price.");
            var off = endgame.Clone();
            off.story.rulesVersion = StoryRules.Threads;
            Assert.That(Negotiation.AskPrice(off, real), Is.Null, "Nor one without the role moments.");
        }

        // ------------------------------------------------------------------ one word on the veto

        /// <summary>
        /// A yes to the Head of Household is the player's word on the veto: a nominee's yes is then refused in the engine's
        /// words, moving nothing, as is naming a price; and a nominee's yes refuses the Head of Household's. Both lapse at the
        /// decision unanswered. The board and the rows draw the refused yes locked, saying why.
        /// </summary>
        [Test]
        public void AKeepTheBlockYesAndAVetoYesLockEachOther()
        {
            var engine = Board(RoleTone.Calculated, out string first, out string second, out string hoh);
            var meeting = engine.Snapshot;
            var held = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, HoldOf(meeting).id, true));
            Assert.That(Negotiation.VetoWordGiven(held), Is.True, "The Head of Household's yes is a word on the veto.");
            string before = JsonConvert.SerializeObject(held);
            foreach (string nominee in new[] { first, second })
            {
                var refused = engine.Apply(RoleVetoBoardTests.Answer(held, RoleVetoBoardTests.AskOf(held, nominee).id, true));
                Assert.That((refused.accepted, refused.reason), Is.EqualTo((false, Negotiation.VetoSavesOneRefusal)), nominee);
            }
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before), "Refused, nothing moves.");
            Assert.That(Negotiation.VetoPriceRefusal(held, first, DealKind.VoteSave), Is.EqualTo(Negotiation.VetoSavesOneRefusal), "Nor may a price be named.");
            var declined = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(held, RoleVetoBoardTests.AskOf(held, first).id, false));
            Assert.That(RoleVetoBoardTests.AskOf(declined, first).status, Is.EqualTo(DealStatus.Declined), "A no stays the player's.");

            var other = Board(RoleTone.Calculated, out string one, out _, out _);
            var asked = RoleVetoBoardTests.Apply(other, RoleVetoBoardTests.Answer(other.Snapshot, RoleVetoBoardTests.AskOf(other.Snapshot, one).id, true));
            var hold = other.Apply(RoleVetoBoardTests.Answer(asked, HoldOf(asked).id, true));
            Assert.That((hold.accepted, hold.reason), Is.EqualTo((false, Negotiation.VetoSavesOneRefusal)), "A nominee's yes locks the Head of Household's.");
            var decided = RoleVetoBoardTests.Apply(other, RoleVetoBoardTests.Decide(asked, true, one));
            Assert.That(HoldOf(decided).status, Is.EqualTo(DealStatus.Expired), "Unanswered, it lapses at the decision.");
            Assert.That(decided.deals.Where(d => d.status == DealStatus.Proposed && Negotiation.IsVetoWord(d.type)), Is.Empty);
        }

        // ------------------------------------------------------------------ a no, silence, defiance and the riders

        /// <summary>An honest no to the Head of Household costs its card's tone's no, every tone the Head of Household can wear.</summary>
        [TestCase(RoleTone.Bitter)]
        [TestCase(RoleTone.Threatening)]
        [TestCase(RoleTone.Friendly)]
        [TestCase(RoleTone.Earnest)]
        [TestCase(RoleTone.Calculated)]
        [TestCase(RoleTone.Pragmatic)]
        public void AnHonestNoCostsTheHeadOfHouseholdsTone(string tone)
        {
            var engine = Board(tone, out _, out _, out string hoh);
            var meeting = engine.Snapshot;
            var card = RoleOffers.KeepTheBlockCard(meeting, HoldOf(meeting));
            Assert.That(card.tone, Is.EqualTo(tone));
            Assert.That(card.answers, Is.EqualTo(RoleOffers.Answers(RoleQuotes.KeepTheBlock, tone)));
            var no = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, HoldOf(meeting).id, false));
            Assert.That(HoldOf(no).status, Is.EqualTo(DealStatus.Declined));
            Assert.That(RoleVetoBoardTests.Events(no, no.playerId, hoh, "deal_declined").Select(e => e.impactScore), Is.EqualTo(new[] { RoleTone.Stakes(tone).decline }), tone);
            Assert.That(no.events.Count(e => e.kind == "deal" && e.text == "You declined a keep-the-block deal from " + no.Find(hoh).name + "."), Is.EqualTo(1));
        }

        /// <summary>
        /// Left hanging, the ask lapses at the decision and costs its tone's lapse, more than its no, and the player is told;
        /// never a breach. Kept unused, nothing more is charged; used anyway, the Head of Household was defied as well.
        /// </summary>
        [TestCase(RoleTone.Bitter)]
        [TestCase(RoleTone.Threatening)]
        [TestCase(RoleTone.Friendly)]
        [TestCase(RoleTone.Earnest)]
        [TestCase(RoleTone.Calculated)]
        [TestCase(RoleTone.Pragmatic)]
        public void SilenceLapsesAtTheDecisionAndIsNoBreach(string tone)
        {
            var engine = Board(tone, out _, out _, out string hoh);
            var meeting = engine.Snapshot;
            int broken = NpcDeals.BrokenDeals(meeting, meeting.playerId);
            var decided = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(meeting, false, null));
            var hold = HoldOf(decided);
            Assert.That((hold.status, hold.brokenById, hold.settledWeek), Is.EqualTo((DealStatus.Expired, (string)null, 0)), tone);
            Assert.That(RoleVetoBoardTests.Events(decided, decided.playerId, hoh, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(tone).lapse }), tone);
            Assert.That(decided.events.Count(e => e.kind == "deal" && e.text == "You left a keep-the-block deal from " + decided.Find(hoh).name + " unanswered."), Is.EqualTo(1));
            Assert.That(NpcDeals.BrokenDeals(decided, decided.playerId), Is.EqualTo(broken), tone + ": no breach.");
            Assert.That(AllEvents(decided, EpisodeEngine.DefiedAskEvent), Is.Empty, tone + ": unused, nobody was defied.");
            Assert.That(CommitmentsRead.With(decided, hoh).Single(c => c.id == hold.id).outcome, Is.EqualTo(CommitmentsRead.Outcomes.Lapsed));
        }

        /// <summary>
        /// Turned down or left hanging, and the veto used anyway: -5 with the Head of Household and a grudge of fifteen - thirty
        /// for a Threatening one, their rider - written as a change in how they feel and a story grudge, never a breach, and
        /// the player is told. Unused after a no, or used after a yes (a breach of its own), nobody was defied.
        /// </summary>
        [Test]
        public void ADefiedHeadOfHouseholdCostsFiveAndHoldsAGrudge()
        {
            foreach (var (tone, answer, grudge) in new[]
                     {
                         (RoleTone.Pragmatic, "decline", RoleTone.DefiedGrudge), (RoleTone.Pragmatic, "leave", RoleTone.DefiedGrudge),
                         (RoleTone.Threatening, "decline", RoleTone.ThreateningGrudge), (RoleTone.Threatening, "leave", RoleTone.ThreateningGrudge),
                         (RoleTone.Calculated, "decline", RoleTone.DefiedGrudge),
                     })
            {
                string what = tone + " " + answer;
                var engine = Board(tone, out string first, out _, out string hoh);
                var at = engine.Snapshot;
                if (answer == "decline") at = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(at, HoldOf(at).id, false));
                int broken = NpcDeals.BrokenDeals(at, at.playerId);
                var used = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(at, true, first));
                Assert.That(HoldOf(used).status, Is.EqualTo(answer == "decline" ? DealStatus.Declined : DealStatus.Expired), what);
                Assert.That(RoleVetoBoardTests.Events(used, used.playerId, hoh, EpisodeEngine.DefiedAskEvent).Select(e => e.impactScore),
                    Is.EqualTo(new[] { RoleTone.DefiedCost }), what + ": -5 with them.");
                var held = used.story.grudges.Single(g => g.holderId == hoh && g.targetId == used.playerId);
                Assert.That((held.severity, held.cause), Is.EqualTo((grudge, GrudgeCauses.Story)), what + ": the grudge.");
                Assert.That(used.events.Count(e => e.kind == "deal" && e.text == "You used the veto after " + used.Find(hoh).name + " asked you not to."), Is.EqualTo(1), what);
                Assert.That(NpcDeals.BrokenDeals(used, used.playerId), Is.EqualTo(broken), what + ": never a breach.");
                Assert.That(used.events.Any(e => e.kind == "deal" && e.text.Contains(used.Find(hoh).name) && (e.text.Contains("broke") || e.text.Contains("fell out"))), Is.False,
                    what + ": the recap reads no broken word into it.");
                Assert.That(EpisodeValidation.TryValidate(used, out string error), Is.True, error);
            }
            Assert.That(RoleTone.DefiedCost, Is.EqualTo(-5));
            Assert.That(RoleTone.DefiedGrudge, Is.EqualTo(15));

            // Unused after a no: nothing more than the no.
            var kept = Board(RoleTone.Threatening, out _, out _, out string keeper);
            var no = RoleVetoBoardTests.Apply(kept, RoleVetoBoardTests.Answer(kept.Snapshot, HoldOf(kept.Snapshot).id, false));
            var unused = RoleVetoBoardTests.Apply(kept, RoleVetoBoardTests.Decide(no, false, null));
            Assert.That(AllEvents(unused, EpisodeEngine.DefiedAskEvent), Is.Empty);
            Assert.That(Grudges.Severity(unused, keeper, unused.playerId), Is.Zero, "Their ask was refused, but the block stood: no grudge.");
        }

        /// <summary>A Bitter Head of Household whose ask was taken and kept eases their grudge against the player by thirty; broken, it does not ease.</summary>
        [Test]
        public void ABitterHeadOfHouseholdsGrudgeEasesWhenKept()
        {
            foreach (bool keep in new[] { true, false })
            {
                var basis = Board(RoleTone.Bitter, out string first, out _, out string hoh).Snapshot;
                basis.story.grudges.Add(new GrudgeState { holderId = hoh, targetId = basis.playerId, cause = GrudgeCauses.Nominated, severity = Grudges.Ceiling, originWeek = 1, count = 1 });
                var engine = new EpisodeEngine(basis);
                var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(basis, HoldOf(basis).id, true));
                var decided = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, !keep, keep ? null : first));
                Assert.That(HoldOf(decided).status, Is.EqualTo(keep ? DealStatus.Fulfilled : DealStatus.Broken));
                double grudge = Grudges.Severity(decided, hoh, decided.playerId);
                if (keep) Assert.That(grudge, Is.EqualTo(Grudges.Ceiling - RoleTone.BitterEase), "Kept: their grudge eases by thirty.");
                else Assert.That(grudge, Is.EqualTo(Grudges.Ceiling), "Broken: it does not ease.");
            }
            // Only a kept word eases it: refused and unused, the grudge stands.
            var refused = Board(RoleTone.Bitter, out _, out _, out string bitter).Snapshot;
            refused.story.grudges.Add(new GrudgeState { holderId = bitter, targetId = refused.playerId, cause = GrudgeCauses.Nominated, severity = 50, originWeek = 1, count = 1 });
            var refusedEngine = new EpisodeEngine(refused);
            var no = RoleVetoBoardTests.Apply(refusedEngine, RoleVetoBoardTests.Answer(refused, HoldOf(refused).id, false));
            var stood = RoleVetoBoardTests.Apply(refusedEngine, RoleVetoBoardTests.Decide(no, false, null));
            Assert.That(Grudges.Severity(stood, bitter, stood.playerId), Is.EqualTo(50));
        }

        /// <summary>
        /// The board's silence and riders are settled from the board as the player saw it, the Head of Household's card
        /// included: a keep-the-block deal anything else wrote - a story's, under another id - is no card, costs no silence
        /// and pays no rider.
        /// </summary>
        [Test]
        public void OnlyTheHeadOfHouseholdsOwnAskIsACard()
        {
            var basis = Board(RoleTone.Threatening, out string first, out _, out string hoh).Snapshot;
            var hold = HoldOf(basis);
            hold.id = "deal-story-keep";
            Assert.That(RoleOffers.IsKeepTheBlockAsk(basis, hold), Is.False, "Keyed on its id, never the kind alone.");
            Assert.That(RoleOffers.BoardAsks(basis).Select(d => d.proposerId), Does.Not.Contain(hoh));
            var engine = new EpisodeEngine(basis);
            var no = engine.Apply(RoleVetoBoardTests.Answer(basis, hold.id, false));
            Assert.That(no.accepted, Is.True, no.reason);
            Assert.That(RoleVetoBoardTests.Events(no.state, no.state.playerId, hoh, "deal_declined").Select(e => e.impactScore), Is.EqualTo(new[] { PlayerDeals.DeclineImpact }),
                "Not a card, so its no costs what every no always did.");
            var used = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(no.state, true, first));
            Assert.That(AllEvents(used, EpisodeEngine.DefiedAskEvent), Is.Empty, "No defiance,");
            Assert.That(Grudges.Severity(used, hoh, used.playerId), Is.Zero, "and no grudge.");
        }

        /// <summary>
        /// Only this week's ask is a card: one from an earlier week, on the record as it ended, is no card, is not on the board,
        /// and the decision settles nothing more on it - a Head of Household turned down last week is not defied again.
        /// </summary>
        [Test]
        public void OnlyThisWeeksAskIsACard()
        {
            var basis = Board(RoleTone.Calculated, out string first, out _, out string hoh).Snapshot;
            var old = HoldOf(basis).Clone();
            old.id = NpcDeals.KeepTheBlockPrefix + "0";
            old.week = old.expiresWeek = basis.week - 1;
            old.status = DealStatus.Declined;
            basis.deals.Add(old);
            Assert.That(EpisodeValidation.TryValidate(basis, out string error), Is.True, error);
            Assert.That(RoleOffers.IsKeepTheBlockAsk(basis, old), Is.False, "Last week's is no card,");
            Assert.That(RoleOffers.BoardAsks(basis).Select(d => d.id), Does.Not.Contain(old.id), "and not on the board.");
            var used = RoleVetoBoardTests.Apply(new EpisodeEngine(basis), RoleVetoBoardTests.Decide(basis, true, first));
            Assert.That(RoleVetoBoardTests.Events(used, used.playerId, hoh, EpisodeEngine.DefiedAskEvent), Has.Count.EqualTo(1),
                "This week's ask, left hanging, is defied once; last week's no is not settled again.");
        }

        /// <summary>
        /// A story's keep-the-block deal is the role moments' own too: a beat whose word is one writes it under them - a
        /// deal of the story's, which is no card on the board - and writes nothing without them, as a story's final three
        /// deal is written only under the commitment rules.
        /// </summary>
        [Test]
        public void AStorysKeepTheBlockDealIsTheRoleMomentsOwnAndNoCard()
        {
            foreach (bool roles in new[] { true, false })
            {
                var basis = Board(RoleTone.Calculated, out string first, out _, out _, roles: roles).Snapshot;
                string beat = RoleVetoBoardTests.StoryBeat(basis, "late-nights", "on-the-block", "PARTNER", first, false, "keep-story", "A word.", "promise-veto");
                var effects = basis.houseEvents.Single(e => e.id == beat).choices.Single().effects.Where(fx => fx.kind == StoryEffects.Deal).ToList();
                Assert.That(effects, Has.Count.EqualTo(1));
                effects[0].type = DealKind.VetoHold;
                var result = new EpisodeEngine(basis).Apply(RoleVetoBoardTests.Progress(basis, beat, "promise-veto"));
                Assert.That(result.accepted, Is.True, "roles " + roles + ": " + result.reason);
                var written = result.state.deals.Where(d => d.type == DealKind.VetoHold && !NpcDeals.IsKeepTheBlockId(d.id)).ToList();
                Assert.That(written, Has.Count.EqualTo(roles ? 1 : 0), "roles " + roles);
                if (!roles) continue;
                Assert.That(RoleOffers.IsKeepTheBlockAsk(result.state, written[0]), Is.False, "A story's is no card,");
                Assert.That(RoleOffers.BoardAsks(result.state).Select(d => d.id), Does.Not.Contain(written[0].id), "and not on the board.");
            }
        }

        // ------------------------------------------------------------------ modes, validation and the player's own table

        /// <summary>
        /// Every commitment mode plays it alike: filed, priced at the endgame, taken - the nominee's yes then refused - and
        /// broken by the veto, at -45, its price voided, validation holding at every step.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryCommitmentModePlaysItAlike(int mode)
        {
            var engine = new EpisodeEngine(Endgame(RoleTone.Calculated, out string first, out _, out string hoh, mode));
            var meeting = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(meeting.unifiedCommitmentRulesVersion, Is.EqualTo(mode == 2 ? UnifiedVoteFamilyValidation.Version : mode));
            Assert.That(HoldOf(meeting).status, Is.EqualTo(DealStatus.Proposed), "mode " + mode);
            Assert.That(NpcDeals.Pending(meeting).Select(d => d.id), Does.Contain(HoldOf(meeting).id), "mode " + mode + ": waiting on the player.");
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, HoldOf(meeting).id, true));
            Assert.That(Negotiation.PriceOf(taken, HoldOf(taken))?.type, Is.EqualTo(DealKind.FinalTwo), "mode " + mode);
            var refused = engine.Apply(RoleVetoBoardTests.Answer(taken, RoleVetoBoardTests.AskOf(taken, first).id, true));
            Assert.That((refused.accepted, refused.reason), Is.EqualTo((false, Negotiation.VetoSavesOneRefusal)), "mode " + mode);
            var used = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Decide(taken, true, first));
            Assert.That((HoldOf(used).status, HoldOf(used).brokenById), Is.EqualTo((DealStatus.Broken, used.playerId)), "mode " + mode);
            Assert.That(RoleVetoBoardTests.Events(used, hoh, used.playerId, "deal_broken").Select(e => e.impactScore), Is.EqualTo(new[] { -45.0 }), "mode " + mode);
            Assert.That(Negotiation.Voided(used, used.deals.Single(d => d.id == HoldOf(taken).linkedDealId)), Is.True, "mode " + mode);
            Assert.That(EpisodeValidation.TryValidate(used, out string error), Is.True, "mode " + mode + ": " + error);
        }

        /// <summary>
        /// Validation refuses a keep-the-block deal - by its kind or by its id - in a season without the role moments, as it
        /// refuses a final three deal without the commitment rules, and one dated a week before they began, read at the
        /// hold's own week rather than the season's; the same row validates under them. Which weeks the role
        /// moments played in is <see cref="EpisodeEngine.RolesOnIn"/>: <see cref="EpisodeEngine.RolesOn"/>'s own test at any
        /// week, each of its rules begun by then.
        /// </summary>
        [Test]
        public void ValidationRefusesItWithoutTheRoleMoments()
        {
            var meeting = Board(RoleTone.Calculated, out _, out _, out _).Snapshot;
            Assert.That(EpisodeValidation.TryValidate(meeting, out string error), Is.True, error);
            const string Refusal = "A season without the role moments has none of their records.";
            foreach (var (name, change) in new (string, Action<EpisodeState>)[]
                     {
                         ("the kind and the id", s => { }),
                         ("the kind under another id", s => HoldOf(s).id = "deal-npc-keep"),
                         ("the id with another kind", s => HoldOf(s).type = DealKind.Partnership),
                     })
            {
                var s = meeting.Clone();
                change(s);
                Assert.That(EpisodeValidation.TryValidate(s, out error), Is.True, name + " under the role moments: " + error);
                s.story.rulesVersion = StoryRules.Threads;
                Assert.That(EpisodeValidation.TryValidate(s, out error), Is.False, name + " at Threads");
                Assert.That(error, Is.EqualTo(Refusal), name);
            }
            var none = Shift(meeting, x => x.deals.RemoveAll(d => NpcDeals.IsKeepTheBlockId(d.id)));
            none.story.rulesVersion = StoryRules.Threads;
            Assert.That(EpisodeValidation.TryValidate(none, out error), Is.True, "Without one, a Threads season validates: " + error);

            // The hold's own week, not the season's: with the story's rules, or the commitment rules, begun only this week, the
            // role moments did not play last week - a hold dated then is refused, though they play now - and this week's validates.
            foreach (var (name, start) in new (string, Action<EpisodeState>)[]
                     {
                         ("the story's rules", x => x.story.rulesStartWeek = x.week), ("the commitment rules", x => x.commitmentRulesStartWeek = x.week),
                     })
            {
                var begun = Shift(meeting, start);
                Assert.That(begun.week, Is.GreaterThan(1), "(a week before this one)");
                Assert.That(EpisodeEngine.RolesOn(begun), Is.True, name + " begun this week: the role moments play now.");
                Assert.That(EpisodeValidation.TryValidate(begun, out error), Is.True, name + " begun this week, this week's hold: " + error);
                var hold = HoldOf(begun);
                hold.week = hold.expiresWeek = begun.week - 1;
                Assert.That(EpisodeValidation.TryValidate(begun, out error), Is.False, name + " begun this week, a hold dated last week");
                Assert.That(error, Is.EqualTo(Refusal), name);
            }

            // At the season's own week it is RolesOn, whichever rule is missing; at any week, each rule begun by then.
            foreach (var s in new[] { meeting, none, Shift(meeting, x => x.leverRulesStartWeek = 0), Shift(meeting, x => x.strategyRulesStartWeek = 0),
                         Shift(meeting, x => x.commitmentRulesStartWeek = x.week + 1), Shift(meeting, x => x.story.rulesStartWeek = 0), Shift(meeting, x => x.story = null) })
                Assert.That(EpisodeEngine.RolesOnIn(s, s.week), Is.EqualTo(EpisodeEngine.RolesOn(s)));
            Assert.That(EpisodeEngine.RolesOnIn(null, 1), Is.False);
            foreach (var start in new Action<EpisodeState, int>[]
                     {
                         (x, w) => x.story.rulesStartWeek = w, (x, w) => x.strategyRulesStartWeek = w,
                         (x, w) => x.commitmentRulesStartWeek = w, (x, w) => x.leverRulesStartWeek = w,
                     })
            {
                var late = Shift(meeting, x => start(x, 3));
                Assert.That(new[] { 1, 2, 3, 4 }.Select(week => EpisodeEngine.RolesOnIn(late, week)), Is.EqualTo(new[] { false, false, true, true }));
            }
        }

        /// <summary>
        /// The player can never put one, in any season: it is refused in the words an unknown kind always was, the deal
        /// table never offers it, and the engine refuses the command.
        /// </summary>
        [Test]
        public void ThePlayerCanNeverProposeOne()
        {
            foreach (bool roles in new[] { true, false })
            {
                var s = Board(RoleTone.Calculated, out _, out _, out _, roles: roles).Snapshot;
                foreach (var npc in s.Active.Where(c => !c.isPlayer))
                {
                    Assert.That(PlayerDeals.CanPropose(s, npc.id, DealKind.VetoHold, null, out string reason), Is.False, "roles " + roles + ": " + npc.id);
                    Assert.That(reason, Is.EqualTo("That is not a deal anybody in this house would recognise."));
                    Assert.That(PlayerDeals.Available(s, npc.id), Does.Not.Contain(DealKind.VetoHold));
                }
                var refused = new EpisodeEngine(s).Apply(RoleVetoBoardTests.Command(s, EpisodeCommandKind.ProposeDeal, s.hohId, null, DealKind.VetoHold));
                Assert.That(refused.accepted, Is.False, "roles " + roles);
                Assert.That(Negotiation.CounterTo(s, s.hohId, DealKind.VetoHold, null), Is.Null, "roles " + roles + ": nor is one a counter's.");
            }
        }

        // ------------------------------------------------------------------ the readers

        /// <summary>
        /// The kind's words, appended to the vocabulary's end so every reader that walks it in order keeps its matches: its
        /// own title, which the weekly lines are rebuilt from - "You honoured a keep-the-block deal with ..." reads as one,
        /// kept by the player, and a partnership's line still reads as a partnership - and its words on every page.
        /// </summary>
        [Test]
        public void EveryReaderSaysWhatItIs()
        {
            Assert.That(DealKind.All.Last(), Is.EqualTo(DealKind.VetoHold), "Appended at the end.");
            Assert.That(DealKind.All.Take(DealKind.All.Length - 1), Is.EqualTo(new[]
            {
                DealKind.TargetAgreement, DealKind.SafetyAgreement, DealKind.VoteTogether, DealKind.VoteSave, DealKind.VoteEvict,
                DealKind.VetoUse, DealKind.InformationSharing, DealKind.FinalTwo, DealKind.Partnership, DealKind.AllianceInvite, DealKind.FinalThree,
            }), "Every kind before it where it was.");
            Assert.That(DealKind.IsKnown("veto_hold"), Is.True);
            Assert.That(DealKind.Title(DealKind.VetoHold), Is.EqualTo("Keep-the-Block Deal"));
            Assert.That(DealKind.Titles(DealKind.VetoHold), Is.EqualTo(new[] { "Keep-the-Block Deal" }));
            Assert.That(DealKind.RolesOnly(DealKind.VetoHold), Is.True);
            Assert.That(DealKind.All.Where(DealKind.RolesOnly), Is.EqualTo(new[] { DealKind.VetoHold }));
            Assert.That(DealKind.CommitmentRulesOnly(DealKind.VetoHold), Is.False);
            Assert.That(DealKind.NamesATarget(DealKind.VetoHold), Is.False);
            Assert.That(Negotiation.IsVetoWord(DealKind.VetoHold), Is.True);
            Assert.That(DealKind.All.Where(Negotiation.IsVetoWord), Is.EqualTo(new[] { DealKind.VetoUse, DealKind.VetoHold }));
            Assert.That(YourWeek.DealWords(DealKind.VetoHold), Is.EqualTo("keep-the-block deal"));
            Assert.That(CommitmentsRead.DealNoun(DealKind.VetoHold), Is.EqualTo("keep-the-block deal"));
            Assert.That(NpcDeals.Urgency(DealKind.VetoHold), Is.LessThan(NpcDeals.Urgency(DealKind.VetoUse)).And.GreaterThan(NpcDeals.Urgency(DealKind.VoteSave)));
            Assert.That(StrategyRules.DealWeight(DealKind.VetoHold), Is.Zero, "It binds the holder, never the Head of Household's nominating.");
            var pair = typeof(WebEvictionVoting).GetMethod("PairDealValue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(pair.Invoke(null, new object[] { DealKind.VetoHold }), Is.EqualTo(0.0), "It binds nothing at a vote.");
            Assert.That(pair.Invoke(null, new object[] { "something_else" }), Is.EqualTo(10.0), "(the table's default)");

            var s = ContentCatalog.Create(6);
            var npc = s.contestants.First(c => !c.isPlayer);
            string you = s.Find(s.playerId).name;
            foreach (bool kept in new[] { true, false })
            {
                string line = you + (kept ? " honoured a " : " broke a ") + "keep-the-block deal with " + npc.name + ".";
                Assert.That(YourWeek.ReadDeal(s, line, npc.id, out string actor, out string type, out bool read), Is.True, line);
                Assert.That((actor, type, read), Is.EqualTo((s.playerId, DealKind.VetoHold, kept)), line);
            }
            Assert.That(YourWeek.ReadDeal(s, npc.name + " honoured a partnership with " + you + ".", npc.id, out _, out string partnership, out _), Is.True);
            Assert.That(partnership, Is.EqualTo(DealKind.Partnership), "A partnership's line still reads as a partnership.");
        }

        /// <summary>
        /// The waiting list, the invitation, the conversation and the page: the ask is a question about the veto that goes
        /// with the decision; the Head of Household opens with the veto; a yes and a no are said as what they are; and the
        /// page names it, what it binds and that it waits on the player.
        /// </summary>
        [Test]
        public void TheWaitingListTheInvitationTheExchangeAndThePageReadIt()
        {
            var engine = Board(RoleTone.Calculated, out _, out _, out string hoh);
            var meeting = engine.Snapshot;
            var hold = HoldOf(meeting);
            var item = WaitingOnYou.Read(meeting).items.Single(i => i.id == hold.id);
            Assert.That((item.kind, item.lapses, item.lapsesOnAdvance, item.fromId), Is.EqualTo((WaitingOnYou.Kind.VetoAsk, WaitingOnYou.Lapse.VetoDecision, false, hoh)),
                "A question about the veto, going with the decision the player makes.");
            var invitation = NpcInvitation.Available(meeting).Single(i => i.RecordId == hold.id);
            Assert.That(invitation.OpeningLine, Is.EqualTo("Can we talk about the veto? I'd like my nominations to stand."));
            Assert.That(invitation.IsCurrent(meeting), Is.True);

            var page = CommitmentsRead.With(meeting, hoh).Single(c => c.id == hold.id);
            Assert.That((page.kind, page.title, page.binds, page.outcome, page.term),
                Is.EqualTo((CommitmentsRead.Kinds.Deal, "Keep-the-block deal they offered", CommitmentsRead.KeepTheBlockBinds, CommitmentsRead.Outcomes.Open, "this week")));
            Assert.That(CommitmentsRead.AtStake(meeting, CommitmentsRead.DecisionKinds.Veto), Is.False, "Unanswered, nothing stands.");

            foreach (bool yes in new[] { true, false })
            {
                var answer = RoleVetoBoardTests.Answer(meeting, hold.id, yes);
                var result = new EpisodeEngine(meeting).Apply(answer);
                Assert.That(result.accepted, Is.True, result.reason);
                var exchange = ConversationExchange.Create(meeting, result.state, answer, result.state.events.Where(e => e.sequence >= meeting.nextSequence).ToList());
                Assert.That(exchange, Is.Not.Null, "yes " + yes);
                Assert.That((exchange.PlayerLine, exchange.NpcLine), Is.EqualTo(yes
                    ? ("Yes. I'll leave your nominations as they are.", "Good. Then we understand each other.")
                    : ("No. I can't promise you that about the veto.", "All right. I hear your answer.")), "yes " + yes);
                if (yes) Assert.That(CommitmentsRead.AtStake(result.state, CommitmentsRead.DecisionKinds.Veto), Is.True, "Taken, the veto decision settles it.");
            }
        }

        /// <summary>
        /// The card says what the ask is, in the plan's words: the Head of Household under the crown's role, "Don't Use the
        /// Veto" and "Keep the nominations the same", the tone as Head of Household with its situation and the player's
        /// read, one of its tone's keep-the-block lines keyed on the week and the asker, the terms, the stakes - kept, broken,
        /// the veto used anyway - within the text cap and without a number, the answers, the history; and the screen
        /// reader's heading.
        /// </summary>
        [Test]
        public void TheCardSaysWhatTheAskIs()
        {
            foreach (string tone in RoleTone.TonesFor(HeadOfHousehold))
            {
                var meeting = Board(tone, out _, out _, out string hoh).Snapshot;
                var hold = HoldOf(meeting);
                var card = RoleOffers.CardOf(meeting, hold);
                Assert.That(RoleOffers.VetoBoard(meeting).Last().offerId, Is.EqualTo(hold.id), tone + ": last on the board.");
                Assert.That((card.offerId, card.speakerId, card.name, card.role, card.moment, card.kind, card.chip, card.chipLine),
                    Is.EqualTo((hold.id, hoh, meeting.Find(hoh).name, HeadOfHousehold, RoleQuotes.KeepTheBlock, DealKind.VetoHold, "Don't Use the Veto", "Keep the nominations the same")), tone);
                Assert.That((card.tone, card.situation, card.read), Is.EqualTo((tone, "Head of Household", RoleTone.ReadLine(meeting, hoh))), tone);
                Assert.That(RoleQuotes.Lines(RoleQuotes.KeepTheBlock, tone), Does.Contain(card.quote), tone);
                Assert.That(RoleOffers.QuoteOccasion(hold), Is.EqualTo("keep-the-block:w" + meeting.week + ":" + hoh));
                Assert.That(card.terms, Is.EqualTo("In return: nothing promised"), tone);
                Assert.That(card.stakes, Is.EqualTo(RoleOffers.Stakes(RoleQuotes.KeepTheBlock, tone, true)), tone);
                Assert.That(card.answers, Is.EqualTo(RoleOffers.Answers(RoleQuotes.KeepTheBlock, tone)), tone);
                Assert.That(card.history, Is.EqualTo(RoleOffers.History(meeting, hoh)), tone);
                Assert.That(RoleOffers.Heading(card), Is.EqualTo(meeting.Find(hoh).name + ", Head of Household, don't use the veto"));
                Assert.That(RoleOffers.Description(card), Does.StartWith("Tone: " + tone + ". Head of Household. "));
            }
            // A broken yes is a breach the house may hear, as a nominee's is; the veto used after a no or silence is personal.
            Assert.That(RoleOffers.Stakes(RoleQuotes.KeepTheBlock, RoleTone.Calculated, true), Is.EqualTo("Keep it: they remember it. Break it: the house may hear. Veto anyway: personal."));
            Assert.That(RoleOffers.Stakes(RoleQuotes.KeepTheBlock, RoleTone.Bitter, true), Is.EqualTo("Keep it: their grudge eases. Break it: the house may hear. Veto anyway: personal."));
            Assert.That(RoleOffers.Stakes(RoleQuotes.KeepTheBlock, RoleTone.Threatening, true), Is.EqualTo("Keep it: they remember. Break it: the house may hear. Veto anyway: very personal."));
            Assert.That(RoleOffers.Stakes(RoleQuotes.KeepTheBlock, RoleTone.Bitter, false), Is.EqualTo(RoleOffers.Stakes(RoleQuotes.KeepTheBlock, RoleTone.Pragmatic, true)),
                "Unsettled, every tone says only keeping, breaking and the veto used anyway.");
            foreach (string tone in RoleTone.TonesFor(HeadOfHousehold))
                Assert.That(RoleOffers.Stakes(RoleQuotes.KeepTheBlock, tone, true), Does.Contain("Break it: the house may hear."),
                    tone + ": the card never hides that a broken yes is public.");
            foreach (string tone in RoleTone.Ladder)
            foreach (bool settle in new[] { true, false })
            {
                string line = RoleOffers.Stakes(RoleQuotes.KeepTheBlock, tone, settle);
                Assert.That(RoleOffers.WordCount(line), Is.LessThanOrEqualTo(RoleOffers.MostStakesWords), line);
                Assert.That(line.Any(char.IsDigit), Is.False, line);
                string answers = RoleOffers.Answers(RoleQuotes.KeepTheBlock, tone);
                Assert.That(answers, Is.EqualTo(RoleOffers.Answers(RoleQuotes.VetoAsk, tone)), tone + ": a no and silence cost the tone's, whoever asks.");
            }
        }

        // ------------------------------------------------------------------ a locked yes, in words

        /// <summary>
        /// What the rows, the board and the season verification draw over a yes the engine refuses (one test,
        /// <see cref="Negotiation.VetoWordLockLine"/>): after the Head of Household's yes, each nominee's card says the player
        /// gave their word to leave the nominations as they are, exactly where the engine refuses that yes; the board stays up
        /// while the nominees' asks wait.
        /// </summary>
        [Test]
        public void AKeepTheBlockYesLocksTheNomineesYesInItsOwnWords()
        {
            var engine = Board(RoleTone.Calculated, out string first, out string second, out string hoh);
            var meeting = engine.Snapshot;
            Assert.That(RoleOffers.BoardAsks(meeting).Select(ask => Negotiation.VetoWordLockLine(meeting, ask)), Is.All.Null, "Nothing is locked before a word.");
            Assert.That(Negotiation.BlockKeptFor(meeting), Is.Null);

            var held = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, HoldOf(meeting).id, true));
            Assert.That(Negotiation.BlockKeptFor(held), Is.EqualTo(hoh));
            foreach (string nominee in new[] { first, second })
            {
                var ask = RoleVetoBoardTests.AskOf(held, nominee);
                string line = Negotiation.VetoWordLockLine(held, ask);
                Assert.That(line, Is.EqualTo("You have already given " + held.Find(hoh).name + " your word to leave the nominations as they are. Turn "
                    + FinalistRead.FirstName(held.Find(nominee).name) + " down, or the offer lapses at the meeting."), nominee);
                Assert.That(line, Is.EqualTo(Negotiation.BlockKeptLine(held, hoh, nominee)));
                var refused = new EpisodeEngine(held).Apply(RoleVetoBoardTests.Answer(held, ask.id, true));
                Assert.That((refused.accepted, refused.reason), Is.EqualTo((false, Negotiation.VetoSavesOneRefusal)), "The engine refuses exactly the yes drawn locked.");
            }
            Assert.That(RoleOffers.BoardAsks(held).Select(d => d.proposerId), Is.EqualTo(new[] { held.nominees[0], held.nominees[1] }), "The nominees' asks still wait.");

            // Turned down, the Head of Household's ask is no word given: the nominees' yes stays open.
            var other = Board(RoleTone.Calculated, out string one, out _, out _);
            var no = RoleVetoBoardTests.Apply(other, RoleVetoBoardTests.Answer(other.Snapshot, HoldOf(other.Snapshot).id, false));
            Assert.That(Negotiation.BlockKeptFor(no), Is.Null);
            Assert.That(Negotiation.VetoWordLockLine(no, RoleVetoBoardTests.AskOf(no, one)), Is.Null);
        }

        /// <summary>
        /// A nominee's yes locks the Head of Household's the other way round, in its own words, and the other nominee's says
        /// what it always said; with both nominees answered the Head of Household's card still stands on the board.
        /// </summary>
        [Test]
        public void ANomineesYesLocksTheHeadOfHouseholdsInItsOwnWords()
        {
            var engine = Board(RoleTone.Calculated, out string first, out string second, out string hoh);
            var meeting = engine.Snapshot;
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, RoleVetoBoardTests.AskOf(meeting, first).id, true));
            var hold = HoldOf(taken);
            string line = Negotiation.VetoWordLockLine(taken, hold);
            Assert.That(line, Is.EqualTo("You have already given " + taken.Find(first).name + " your word on the veto. Turn "
                + FinalistRead.FirstName(taken.Find(hoh).name) + " down, or the offer lapses at the meeting."));
            Assert.That(line, Is.EqualTo(Negotiation.VetoGivenLine(taken, first, hoh)));
            Assert.That(Negotiation.VetoWordLockLine(taken, RoleVetoBoardTests.AskOf(taken, second)), Is.EqualTo(Negotiation.VetoSavesOneLine(taken, first, second)),
                "The other nominee's says what it always said.");
            Assert.That(Negotiation.VetoSavesOneLine(taken, first, second), Is.EqualTo("The veto can only save one of them, and you have already given "
                + taken.Find(first).name + " your word. Turn " + FinalistRead.FirstName(taken.Find(second).name) + " down, or the offer lapses at the meeting."));

            var both = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(taken, RoleVetoBoardTests.AskOf(taken, second).id, false));
            Assert.That(RoleOffers.VetoAsks(both), Is.Empty);
            Assert.That(RoleOffers.BoardAsks(both).Select(d => d.id), Is.EqualTo(new[] { hold.id }), "The Head of Household's ask is still a card.");
        }

        /// <summary>
        /// A season without the role moments locks what it always locked, and a keep-the-block row it could never hold locks
        /// nothing there and is locked by nothing.
        /// </summary>
        [Test]
        public void WithoutTheRoleMomentsTheRowsLockWhatTheyAlwaysLocked()
        {
            var engine = Board(RoleTone.Calculated, out string first, out string second, out _, roles: false);
            var meeting = engine.Snapshot;
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, RoleVetoBoardTests.AskOf(meeting, first).id, true));
            Assert.That(Negotiation.VetoWordLockLine(taken, RoleVetoBoardTests.AskOf(taken, second)), Is.EqualTo(Negotiation.VetoSavesOneLine(taken, first, second)));
            Assert.That(Negotiation.VetoWordLockLine(taken, RoleVetoBoardTests.AskOf(taken, first)), Is.Null);
            foreach (var at in new[] { meeting, taken })
            {
                var odd = at.Clone();
                var row = new DealState
                {
                    id = NpcDeals.KeepTheBlockPrefix + "1", type = DealKind.VetoHold, proposerId = odd.hohId, recipientId = odd.playerId,
                    status = DealStatus.Active, week = odd.week, expiresWeek = odd.week, trustImpact = DealTrust.High,
                };
                odd.deals.Add(row);
                Assert.That(Negotiation.VetoWordLockLine(odd, row), Is.Null);
                Assert.That(Negotiation.VetoWordLockLine(odd, RoleVetoBoardTests.AskOf(odd, second)),
                    Is.EqualTo(at == taken ? Negotiation.VetoSavesOneLine(odd, first, second) : null));
            }
            Assert.That(Negotiation.VetoWordLockLine(meeting, null), Is.Null);
            Assert.That(Negotiation.VetoWordLockLine(null, RoleVetoBoardTests.AskOf(meeting, first)), Is.Null);
        }

        /// <summary>
        /// The rows' sentence for the Head of Household's ask (the conversation's deal panel and the dashboard draw it): what it
        /// asks, and the final two it carries at the endgame, in the words every ask's price is said in.
        /// </summary>
        [Test]
        public void TheRowsSayWhatTheHeadOfHouseholdAsks()
        {
            var meeting = Board(RoleTone.Calculated, out _, out _, out string hoh).Snapshot;
            Assert.That(Negotiation.KeepTheBlockSentence(meeting, HoldOf(meeting)),
                Is.EqualTo(meeting.Find(hoh).name + " is Head of Household and wants your word that you will not use the veto."));
            var start = Endgame(RoleTone.Calculated, out _, out _, out string endHoh);
            var endgame = RoleVetoBoardTests.Apply(new EpisodeEngine(start), RoleVetoBoardTests.Command(start, EpisodeCommandKind.Advance));
            string name = endgame.Find(endHoh).name;
            Assert.That(Negotiation.KeepTheBlockSentence(endgame, HoldOf(endgame)),
                Is.EqualTo(name + " is Head of Household and wants your word that you will not use the veto. In return, " + name + " offers you a final two."));
        }

        // ------------------------------------------------------------------ fixtures

        /// <summary>
        /// R3's meeting (<see cref="RoleVetoBoardTests.BeforeTheMeeting"/>: week two, the player handed the veto, both nominees
        /// Pragmatic and warm enough to ask) with the Head of Household - a houseguest - shaped to wear <paramref name="tone"/>
        /// as Head of Household, by what the ladder reads, warm enough toward the player to bargain, holding no grudge, and
        /// planning no backdoor. Not yet at the meeting.
        /// </summary>
        internal static EpisodeState BeforeTheMeeting(string tone, out string first, out string second, out string hoh, int mode = 2, bool roles = true)
        {
            var s = RoleVetoBoardTests.BeforeTheMeeting(RoleTone.Pragmatic, out first, out second, mode, roles);
            hoh = s.hohId;
            Assert.That(s.Find(hoh).isPlayer, Is.False);
            ShapeTheHeadOfHousehold(s, hoh, tone);
            Assert.That(RoleTone.Of(s, hoh, HeadOfHousehold), Is.EqualTo(tone), "The Head of Household wears " + tone + ".");
            Assert.That(EpisodeEngine.BackdoorPlanned(s), Is.Null, "(no backdoor plan)");
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        /// <summary><see cref="BeforeTheMeeting"/>, advanced into the meeting by the engine: both nominees asked, and the Head of Household after them (under the role moments).</summary>
        internal static EpisodeEngine Board(string tone, out string first, out string second, out string hoh, int mode = 2, bool roles = true)
        {
            var s = BeforeTheMeeting(tone, out first, out second, out hoh, mode, roles);
            var engine = new EpisodeEngine(s);
            var meeting = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Command(s, EpisodeCommandKind.Advance));
            Assert.That(meeting.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(meeting.deals.Where(d => RoleOffers.IsVetoAsk(meeting, d)).Select(d => d.proposerId), Is.EquivalentTo(new[] { first, second }), "Both nominees asked.");
            Assert.That(meeting.deals.Count(d => RoleOffers.IsKeepTheBlockAsk(meeting, d)), Is.EqualTo(roles ? 1 : 0), "The Head of Household asked under the role moments only.");
            if (roles) Assert.That(RoleOffers.ToneOf(meeting, HoldOf(meeting)), Is.EqualTo(tone), "The card wears the tone at the meeting.");
            return engine;
        }

        private static readonly Dictionary<int, List<EpisodeState>> EndgameWalks = new Dictionary<int, List<EpisodeState>>();

        /// <summary>
        /// The endgame's meeting, not yet reached: a six-house season at Roles in commitment mode <paramref name="mode"/> -
        /// the endgame from its first week, so a final two is the Head of Household's price - walked through the public
        /// engine to week one's veto competition with the player neither Head of Household nor on the block; then handed the
        /// veto, both nominees warm enough to ask, and the Head of Household shaped as <see cref="BeforeTheMeeting"/> shapes
        /// them, planning no backdoor, with no final two binding them to the player.
        /// </summary>
        internal static EpisodeState Endgame(string tone, out string first, out string second, out string hoh, int mode = 2)
        {
            if (!EndgameWalks.TryGetValue(mode, out var walks))
            {
                walks = new List<EpisodeState>();
                for (uint seed = 7301; seed < 7501 && walks.Count < 3; seed++)
                {
                    var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = NpcDeals.EndgameSize }, seed);
                    ShippedRules.ApplyFresh(start);
                    start.story.rulesVersion = StoryRules.Roles;
                    start.unifiedCommitmentRulesVersion = mode == 2 ? UnifiedVoteFamilyValidation.Version : mode;
                    start.unifiedHearingRulesVersion = mode == 0 ? 0 : UnifiedCommitmentHearings.ProspectiveVersion;
                    var walk = new EpisodeEngine(start);
                    for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                    {
                        var step = walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot));
                        Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                    }
                    var s = walk.Snapshot;
                    if (s.week != 1 || s.phase != EpisodePhase.Veto || s.competitionResolved || s.Find(s.playerId).status != ContestantStatus.Active
                        || s.hohId == s.playerId || s.nominees.Contains(s.playerId) || s.Active.Count() != NpcDeals.EndgameSize
                        || EpisodeEngine.BackdoorPlanned(s) != null) continue;
                    walks.Add(s);
                }
                Assert.That(walks, Is.Not.Empty, "No seed reached week one's veto with the player free.");
                EndgameWalks[mode] = walks;
            }
            var passed = new List<string>();
            foreach (var walked in walks)
            {
                var s = walked.Clone();
                string h = s.hohId;
                ShapeTheHeadOfHousehold(s, h, tone);
                if (RoleTone.Of(s, h, HeadOfHousehold) != tone || EpisodeEngine.BackdoorPlanned(s) != null
                    || NpcDeals.Between(s, h, s.playerId).Any(d => d.type == DealKind.FinalTwo))
                {
                    passed.Add(RoleTone.Of(s, h, HeadOfHousehold) + "/" + EpisodeEngine.BackdoorPlanned(s));
                    continue;
                }
                foreach (string nominee in s.nominees)
                {
                    Set(s, nominee, s.playerId, 50);
                    s.story.grudges.RemoveAll(g => g.holderId == nominee && g.targetId == s.playerId);
                }
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
                first = s.nominees[0];
                second = s.nominees[1];
                hoh = h;
                return s;
            }
            Assert.Fail("No walked endgame could be shaped to " + tone + ": " + string.Join(", ", passed));
            first = second = hoh = null;
            return null;
        }

        /// <summary>
        /// Makes the Head of Household wear a tone by what the ladder reads (§3.0): a word the player broke last week (Bitter);
        /// Bold and Vengeful traits (Threatening); the player's own reading of them as close (Friendly); Warm (Earnest); Steady
        /// (Calculated); nothing at all (Pragmatic). Warm enough toward the player to bargain, whatever the record costs them.
        /// </summary>
        private static void ShapeTheHeadOfHousehold(EpisodeState s, string hoh, string tone)
        {
            var who = s.Find(hoh);
            who.traits = new List<string>();
            Set(s, s.playerId, hoh, 0);
            Set(s, hoh, s.playerId, 50);
            s.story.grudges.RemoveAll(g => g.holderId == hoh && g.targetId == s.playerId);
            switch (tone)
            {
                case RoleTone.Bitter: BreakAWord(s, hoh); break;
                case RoleTone.Threatening: who.traits = new List<string> { "Competitive", "Stubborn" }; break;
                case RoleTone.Friendly: Set(s, s.playerId, hoh, 30); break;
                case RoleTone.Earnest: who.traits = new List<string> { "Emotional" }; break;
                case RoleTone.Calculated: who.traits = new List<string> { "Analytical" }; break;
            }
        }

        /// <summary>A word the player broke to them in week one: an information deal, broken by the player, on the record.</summary>
        private static void BreakAWord(EpisodeState s, string id) => s.deals.Add(new DealState
        {
            id = "deal-word-" + id, type = DealKind.InformationSharing, proposerId = s.playerId, recipientId = id,
            status = DealStatus.Broken, week = 1, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
            brokenById = s.playerId, settledWeek = 1,
        });

        /// <summary>A season at Roles walked to week one's veto with the player on the block, then handed the veto; the other nominee warm enough to ask.</summary>
        private static EpisodeState NominatedHolder()
        {
            for (uint seed = 7001; seed < 7121; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = StoryRules.Roles;
                var walk = new EpisodeEngine(start);
                for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                    Assert.That(walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot)).accepted, Is.True, "seed " + seed);
                var s = walk.Snapshot;
                if (s.phase != EpisodePhase.Veto || s.competitionResolved || !s.nominees.Contains(s.playerId) || s.hohId == s.playerId) continue;
                Set(s, s.nominees.Single(id => id != s.playerId), s.playerId, 50);
                Set(s, s.hohId, s.playerId, 50);
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                return s;
            }
            Assert.Fail("No seed nominated the player in week one.");
            return null;
        }

        /// <summary>The meeting with the Head of Household's ask taken back out: the state <see cref="NpcDeals.AskToKeepTheBlock"/> meets, right after the nominees' asks.</summary>
        private static EpisodeState Basis(EpisodeState meeting)
        {
            var s = meeting.Clone();
            s.deals.RemoveAll(d => NpcDeals.IsKeepTheBlockId(d.id));
            return s;
        }

        /// <summary>Whether the Head of Household asks from this state: one more keep-the-block ask filed.</summary>
        private static bool Filed(EpisodeState s)
        {
            int before = s.deals.Count(d => NpcDeals.IsKeepTheBlockId(d.id));
            NpcDeals.AskToKeepTheBlock(s);
            return s.deals.Count(d => NpcDeals.IsKeepTheBlockId(d.id)) > before;
        }

        /// <summary>The Head of Household's plan to backdoor <paramref name="target"/> this week, as the story's backdoor arc holds one.</summary>
        private static void PlanABackdoor(EpisodeState s, string hoh, string target) => s.storylines.Add(new StorylineState
        {
            id = "story-role-backdoor", templateId = "the-backdoor", status = StorylineStatus.Active, week = s.week, beatId = "planned",
            cast = new List<StoryRoleState> { new StoryRoleState { role = "HOH", contestantId = hoh }, new StoryRoleState { role = "TARGET", contestantId = target } },
            vars = new List<StoryVarState> { new StoryVarState { key = "planned", value = 1 } },
        });

        /// <summary>Pads the season's deals to <paramref name="count"/>, as the ceiling counts them, with settled deals between two houseguests.</summary>
        private static void Fill(EpisodeState s, int count)
        {
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            for (int i = 0; CommitmentReferences.DealCount(s) < count; i++)
                s.deals.Add(new DealState
                {
                    id = "deal-fill-" + i, type = DealKind.InformationSharing, proposerId = npcs[i % npcs.Count], recipientId = npcs[(i + 1) % npcs.Count],
                    status = DealStatus.Expired, week = 1, trustImpact = DealTrust.Low,
                });
        }

        /// <summary>A copy with one change.</summary>
        private static EpisodeState Shift(EpisodeState s, Action<EpisodeState> change)
        {
            var copy = s.Clone();
            change(copy);
            return copy;
        }

        /// <summary>A copy with nothing between the player and <paramref name="id"/> but this week's keep-the-block deal: no other deal or promise between them.</summary>
        private static EpisodeState Alone(EpisodeState s, string id)
        {
            var copy = s.Clone();
            bool Pair(string a, string b) => (a == copy.playerId && b == id) || (a == id && b == copy.playerId);
            copy.deals.RemoveAll(d => Pair(d.proposerId, d.recipientId) && !(d.type == DealKind.VetoHold && d.week == copy.week));
            copy.promises.RemoveAll(p => Pair(p.fromId, p.toId));
            return copy;
        }

        /// <summary>This week's keep-the-block ask.</summary>
        internal static DealState HoldOf(EpisodeState s) => s.deals.Single(d => d.type == DealKind.VetoHold && d.week == s.week);

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>Every ledger record of a type, with the pair it is between.</summary>
        private static List<(RelationshipEventState record, string[] edge)> AllEvents(EpisodeState s, string type) =>
            s.relationships.SelectMany(r => r.events.Where(e => e.type == type).Select(e => (e, new[] { r.fromId, r.toId }))).ToList();
    }
}
