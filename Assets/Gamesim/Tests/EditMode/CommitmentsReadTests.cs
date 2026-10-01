using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The player's word (ACTIONS-DEALS-ALLIANCES-PLAN V1): the reader of every commitment the
    /// player is a party to, and the dry run behind the decision screens' warnings.
    ///
    /// <para>The dry run's tests hold it to the engine itself: each kind of decision is run as a
    /// warning and then committed for real on another engine, and what the warning said would
    /// break is what the commit broke. A warning that disagreed with the verdict that followed it
    /// would be worse than none.</para>
    /// </summary>
    public sealed class CommitmentsReadTests
    {
        // ---------------------------------------------------------------- the reader

        [Test]
        public void EveryKindOfCommitmentThePlayerIsAPartyToIsReadAndNothingElse()
        {
            var s = Season();
            var npc = Npcs(s);
            string player = s.playerId;
            s.week = 4;
            Promise(s, "p-mine", PromiseKind.Safety, player, npc[0].id, week: 4, expires: 5);
            Promise(s, "p-theirs", PromiseKind.FinalTwo, npc[1].id, player, status: PromiseStatus.Broken, week: 2);
            Promise(s, "p-npc", PromiseKind.Safety, npc[2].id, npc[3].id, week: 4, expires: 5);
            Deal(s, "d-mine", DealKind.SafetyAgreement, player, npc[0].id, week: 4, expires: 4);
            Deal(s, "d-offer", DealKind.VoteEvict, npc[1].id, player, about: npc[2].id, status: DealStatus.Proposed, week: 4, expires: 4);
            Deal(s, "d-declined", DealKind.Partnership, npc[3].id, player, status: DealStatus.Declined, week: 2);
            Deal(s, "d-npc", DealKind.FinalTwo, npc[2].id, npc[3].id, week: 3);
            Oath(s, npc[4].id, 3);
            s.alliances.Add(new AllianceState { id = "pact", name = "The Pact", members = new List<string> { player, npc[0].id, npc[1].id } });
            s.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "pact", callerId = player, targetId = npc[2].id,
                followed = new List<string> { npc[0].id }, defected = new List<string> { npc[1].id } });

            var read = CommitmentsRead.Of(s);
            Assert.That(read.Select(c => c.kind + ":" + c.id), Is.EqualTo(new[]
            {
                "promise:p-mine", "promise:p-theirs", "deal:d-mine", "deal:d-offer", "deal:d-declined",
                "oath:" + CommitmentsRead.OathId(s.loyaltyOaths[0]),
                "call:call:pact:3:" + npc[0].id, "call:call:pact:3:" + npc[1].id,
            }), "Every commitment the player is a party to, promises first; nothing between two houseguests.");
            Assert.That(read.All(c => c.withId != player), Is.True);

            var mine = read.Single(c => c.id == "p-mine");
            Assert.That((mine.withId, mine.yours, mine.title, mine.binds, mine.term, mine.status, mine.outcome),
                Is.EqualTo((npc[0].id, true, "Your promise of safety", "not to nominate them", "until week 5", "still standing", CommitmentsRead.Outcomes.Open)));
            var theirs = read.Single(c => c.id == "p-theirs");
            Assert.That((theirs.yours, theirs.title, theirs.binds, theirs.outcome, theirs.brokenById, theirs.status),
                Is.EqualTo((false, "Their final two promise", "to take you to the final two", CommitmentsRead.Outcomes.Broken, npc[1].id, "broken by them")),
                "A promise is only ever broken by whoever gave it.");
            var deal = read.Single(c => c.id == "d-mine");
            Assert.That((deal.title, deal.binds, deal.term, deal.status, deal.outcome),
                Is.EqualTo(("Safety deal you proposed", "not to nominate each other", "this week", "agreed", CommitmentsRead.Outcomes.Open)));
            var offer = read.Single(c => c.id == "d-offer");
            Assert.That((offer.title, offer.aboutId, offer.binds, offer.status, offer.outcome),
                Is.EqualTo(("Vote-to-evict deal they offered", npc[2].id, "to vote to evict " + First(npc[2]), "waiting on you", CommitmentsRead.Outcomes.Open)));
            var declined = read.Single(c => c.id == "d-declined");
            Assert.That((declined.status, declined.outcome, declined.term), Is.EqualTo(("you declined", CommitmentsRead.Outcomes.Lapsed, "never expires")));
            var oath = read.Single(c => c.kind == CommitmentsRead.Kinds.Oath);
            Assert.That((oath.withId, oath.title, oath.term, oath.status, oath.outcome, oath.week),
                Is.EqualTo((npc[4].id, "Your loyalty oath", "never expires", "standing", CommitmentsRead.Outcomes.Open, 3)));
            var followed = read.Single(c => c.kind == CommitmentsRead.Kinds.Call && c.withId == npc[0].id);
            Assert.That((followed.title, followed.binds, followed.term, followed.status, followed.outcome),
                Is.EqualTo(("Your call in The Pact", "their vote to evict " + First(npc[2]), "week 3 only", "followed it", CommitmentsRead.Outcomes.Kept)),
                "A call's week has passed, so its vote has been revealed.");
            var ignored = read.Single(c => c.kind == CommitmentsRead.Kinds.Call && c.withId == npc[1].id);
            Assert.That((ignored.status, ignored.outcome, ignored.brokenById), Is.EqualTo(("ignored it", CommitmentsRead.Outcomes.Broken, npc[1].id)));
            Assert.That(CommitmentsRead.Line(mine), Is.EqualTo("Your promise of safety: not to nominate them · until week 5 · still standing"));
            Assert.That(CommitmentsRead.With(s, npc[0].id).Select(c => c.id), Is.EquivalentTo(new[] { "p-mine", "d-mine", "call:pact:3:" + npc[0].id }));
        }

        [Test]
        public void ATermSaysTheWeekItRunsToFromTheWeekItIsRead()
        {
            var s = Season();
            s.week = 5;
            string Term(int week, int until, string outcome = CommitmentsRead.Outcomes.Open) =>
                CommitmentsRead.Term(s, new CommitmentsRead.Commitment { week = week, untilWeek = until, outcome = outcome });
            Assert.That(Term(2, 0), Is.EqualTo("never expires"));
            Assert.That(Term(5, 5), Is.EqualTo("this week"));
            Assert.That(Term(5, 6), Is.EqualTo("until week 6"));
            Assert.That(Term(3, 3, CommitmentsRead.Outcomes.Lapsed), Is.EqualTo("week 3 only"));
            Assert.That(Term(2, 3, CommitmentsRead.Outcomes.Kept), Is.EqualTo("until week 3"));
            Assert.That(Term(4, 4), Is.EqualTo("until week 4, still binding"),
                "A deal binds until the house next settles its deals, which can be after its week: the term must not claim it has ended.");
            Assert.That(CommitmentsRead.Term(s, new CommitmentsRead.Commitment { kind = CommitmentsRead.Kinds.Deal, week = 4, untilWeek = 4,
                outcome = CommitmentsRead.Outcomes.Open, status = "waiting on you" }), Is.EqualTo("until week 4, still open"),
                "An offer nobody has answered binds nobody: it is open, not binding.");
        }

        [Test]
        public void ABrokenDealBlamesOnlyWhoTheRecordNames()
        {
            var s = Season();
            var npc = Npcs(s);
            string player = s.playerId;
            s.week = 3;
            // Week 2: the houseguest held the house and put the player up, breaking their safety deal.
            s.ledger.power.Add(new PowerRow { week = 2, hohId = npc[0].id, nominees = new List<string> { player, npc[2].id } });
            Deal(s, "d-named", DealKind.SafetyAgreement, player, npc[0].id, status: DealStatus.Broken, week: 2, expires: 2);
            // Nothing on the record says who broke this one.
            Deal(s, "d-unnamed", DealKind.SafetyAgreement, npc[1].id, player, status: DealStatus.Broken, week: 3, expires: 3);
            Deal(s, "d-block", DealKind.VoteTogether, player, npc[3].id, status: DealStatus.Broken, week: 2, expires: 2);

            var read = CommitmentsRead.Of(s);
            var named = read.Single(c => c.id == "d-named");
            Assert.That((named.brokenById, named.status), Is.EqualTo((npc[0].id, "broken by them")));
            var unnamed = read.Single(c => c.id == "d-unnamed");
            Assert.That((unnamed.brokenById, unnamed.status, unnamed.outcome), Is.EqualTo(((string)null, "broken", CommitmentsRead.Outcomes.Broken)),
                "Deals do not record who broke them: where the record cannot say, nobody is blamed.");
            Assert.That(read.Single(c => c.id == "d-block").status, Is.EqualTo("fell apart"), "A voting block is settled by both at once.");
        }

        [Test]
        public void AnOathIsKeptWhenEitherLeavesAndItsBreachIsOnThePlayersOwnRecord()
        {
            var s = Season();
            var npc = Npcs(s);
            s.week = 6;
            Oath(s, npc[0].id, 2);
            Oath(s, npc[1].id, 3);
            npc[1].status = ContestantStatus.Jury;
            s.ledger.power.Add(new PowerRow { week = 5, hohId = npc[5].id, nominees = new List<string> { npc[1].id, npc[6].id },
                evicteeId = npc[1].id, tally = new List<int> { 4, 1 } });
            // An oath breach removes the oath and writes the arc the oath's rule writes.
            s.relationshipArcs.Add(new RelationshipArcState { npcId = npc[2].id, npcName = npc[2].name, weeklyHistory = new List<ArcHistory>
                { new ArcHistory { week = 4, delta = -25, reason = "Broke loyalty oath by nominating you in week 4" } } });
            s.relationshipArcs.Add(new RelationshipArcState { npcId = npc[3].id, npcName = npc[3].name, weeklyHistory = new List<ArcHistory>
                { new ArcHistory { week = 5, delta = -20, reason = "Broke loyalty oath by voting to evict " + npc[3].name + " in week 5" } } });
            // Production removed somebody the player had declared to: the removal takes every oath
            // naming them off the list, unbroken, and leaves the declaration on the player's edge
            // (EpisodeEngine.Expel).
            Oath(s, npc[4].id, 2);
            s.loyaltyOaths.RemoveAll(o => o.targetId == npc[4].id);
            npc[4].status = ContestantStatus.Expelled;
            s.story.removals.Add(new RemovalState { contestantId = npc[4].id, week = 4, reasonId = "conduct" });

            var oaths = CommitmentsRead.Of(s).Where(c => c.kind == CommitmentsRead.Kinds.Oath).ToList();
            Assert.That(oaths.Select(c => c.withId), Is.EquivalentTo(new[] { npc[0].id, npc[1].id, npc[2].id, npc[3].id, npc[4].id }),
                "Every oath the player declared, standing, kept, broken or ended by a removal, once each.");
            Assert.That(oaths.Select(c => c.title).Distinct(), Is.EqualTo(new[] { "Your loyalty oath" }), "Every oath is the player's own declaration.");
            var standing = oaths.Single(c => c.withId == npc[0].id);
            Assert.That((standing.outcome, standing.term), Is.EqualTo((CommitmentsRead.Outcomes.Open, "never expires")));
            var left = oaths.Single(c => c.withId == npc[1].id);
            Assert.That((left.outcome, left.status, left.term, left.settledWeek),
                Is.EqualTo((CommitmentsRead.Outcomes.Kept, "never broken", "until they left in week 5", 5)));
            var theirs = oaths.Single(c => c.withId == npc[2].id);
            Assert.That((theirs.outcome, theirs.brokenById, theirs.settledWeek, theirs.status, theirs.term),
                Is.EqualTo((CommitmentsRead.Outcomes.Broken, npc[2].id, 4, "broken by them with a nomination", "held until week 4")));
            Assert.That(CommitmentsRead.Line(theirs),
                Is.EqualTo("Your loyalty oath: never to nominate or vote out each other · held until week 4 · broken by them with a nomination"));
            var yours = oaths.Single(c => c.withId == npc[3].id);
            Assert.That((yours.brokenById, yours.status, yours.term), Is.EqualTo((s.playerId, "broken by you with a vote", "held until week 5")));
            var removed = oaths.Single(c => c.withId == npc[4].id);
            Assert.That((removed.outcome, removed.status, removed.term, removed.IsOpen),
                Is.EqualTo((CommitmentsRead.Outcomes.Kept, "never broken", "until they left in week 4", false)),
                "An oath with somebody production removed still shows, settled.");
            Assert.That(oaths.Where(c => !c.IsOpen).Select(c => c.term), Has.None.EqualTo("never expires"), "An oath that has ended never reads as one that never expires.");

            // The player removed: every oath of theirs went with them, and each still shows, settled.
            var gone = Season();
            var others = Npcs(gone);
            Oath(gone, others[0].id, 1);
            gone.week = 3;
            gone.loyaltyOaths.Clear();
            gone.Find(gone.playerId).status = ContestantStatus.Expelled;
            gone.story.removals.Add(new RemovalState { contestantId = gone.playerId, week = 3, reasonId = "conduct" });
            var own = CommitmentsRead.Of(gone).Single(c => c.kind == CommitmentsRead.Kinds.Oath);
            Assert.That((own.withId, own.outcome, own.term), Is.EqualTo((others[0].id, CommitmentsRead.Outcomes.Kept, "until you left in week 3")));
        }

        [Test]
        public void ADealThatNamesThePlayerSaysYouAndAVetoDealAssumesNobodyHoldsIt()
        {
            var s = Season();
            var npc = Npcs(s);
            s.week = 3;
            Deal(s, "d-keep", DealKind.VoteSave, npc[0].id, s.playerId, about: s.playerId, week: 3, expires: 3);
            Deal(s, "d-veto-asked", DealKind.VetoUse, npc[1].id, s.playerId, week: 3, expires: 3);
            Deal(s, "d-veto-offered", DealKind.VetoUse, s.playerId, npc[2].id, week: 3, expires: 3);
            var read = CommitmentsRead.Of(s);
            var keep = read.Single(c => c.id == "d-keep");
            Assert.That((keep.aboutId, keep.binds), Is.EqualTo((s.playerId, "to vote to keep you")), "A deal about the player says you.");
            Assert.That(CommitmentsRead.Line(keep), Is.EqualTo("Vote-to-save deal they offered: to vote to keep you · this week · agreed"),
                "never the player's own name in the third person.");
            foreach (var id in new[] { "d-veto-asked", "d-veto-offered" })
                Assert.That(read.Single(c => c.id == id).binds, Is.EqualTo("whichever of you holds the veto uses it on the other"),
                    "Either party can hold the veto, and the deal binds whichever does: " + id + ".");
        }

        [Test]
        public void NothingIsRunForADecisionWhenNothingItSettlesIsStanding()
        {
            var s = AtNominations();
            var npc = EpisodeEngine.NominationCandidates(s).ToList();
            string player = s.playerId;
            foreach (var kind in new[] { CommitmentsRead.DecisionKinds.Nominate, CommitmentsRead.DecisionKinds.Veto,
                         CommitmentsRead.DecisionKinds.Vote, CommitmentsRead.DecisionKinds.FinalEviction })
                Assert.That(CommitmentsRead.AtStake(s, kind), Is.False, "A fresh season has nothing at stake: " + kind + ".");

            // Words the player is not a party to, or is owed rather than gave, or of a kind no decision settles.
            Deal(s, "d-npc", DealKind.SafetyAgreement, npc[0].id, npc[1].id);
            Promise(s, "p-owed", PromiseKind.Safety, npc[2].id, player, expires: 2);
            Deal(s, "d-info", DealKind.InformationSharing, player, npc[3].id);
            Deal(s, "d-block", DealKind.VoteTogether, player, npc[4].id);
            Deal(s, "d-asked", DealKind.SafetyAgreement, npc[5].id, player, status: DealStatus.Proposed);
            foreach (var kind in new[] { CommitmentsRead.DecisionKinds.Nominate, CommitmentsRead.DecisionKinds.Veto,
                         CommitmentsRead.DecisionKinds.Vote, CommitmentsRead.DecisionKinds.FinalEviction })
                Assert.That(CommitmentsRead.AtStake(s, kind), Is.False, "Nothing the player gave that a decision settles: " + kind + ".");
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Nominate(npc[0].id, npc[1].id)), Is.Empty);

            // Each kind of decision, and what it settles.
            Deal(s, "d-safety", DealKind.SafetyAgreement, player, npc[6].id);
            Assert.That((CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.Nominate), CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.Veto),
                CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.Vote), CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.FinalEviction)),
                Is.EqualTo((true, true, false, false)), "A safety deal is settled by a nomination, a replacement among them.");
            var v = Season();
            var w = Npcs(v);
            Deal(v, "d-veto", DealKind.VetoUse, w[0].id, v.playerId);
            Assert.That((CommitmentsRead.AtStake(v, CommitmentsRead.DecisionKinds.Veto), CommitmentsRead.AtStake(v, CommitmentsRead.DecisionKinds.Nominate)),
                Is.EqualTo((true, false)), "A veto deal, by the veto alone.");
            var b = Season();
            Promise(b, "p-vote", PromiseKind.Vote, b.playerId, Npcs(b)[0].id, about: Npcs(b)[1].id);
            Assert.That(CommitmentsRead.AtStake(b, CommitmentsRead.DecisionKinds.Vote), Is.True, "A vote promise, by the ballot.");
            var o = Season();
            Oath(o, Npcs(o)[0].id, 1);
            Assert.That((CommitmentsRead.AtStake(o, CommitmentsRead.DecisionKinds.Nominate), CommitmentsRead.AtStake(o, CommitmentsRead.DecisionKinds.Vote),
                CommitmentsRead.AtStake(o, CommitmentsRead.DecisionKinds.FinalEviction)), Is.EqualTo((true, true, false)), "An oath, by a nomination or a ballot.");
            var f = Season();
            Promise(f, "p-final", PromiseKind.FinalTwo, f.playerId, Npcs(f)[0].id);
            Assert.That(CommitmentsRead.AtStake(f, CommitmentsRead.DecisionKinds.FinalEviction), Is.True, "A Final 2 promise, by the final choice.");
        }

        [Test]
        public void ACallIsOpenUntilItsVoteIsRevealed()
        {
            var s = Season();
            var npc = Npcs(s);
            s.week = 3; s.phase = EpisodePhase.Campaign;
            s.alliances.Add(new AllianceState { id = "pact", name = "The Pact", members = new List<string> { s.playerId, npc[0].id } });
            s.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "pact", callerId = s.playerId, targetId = npc[1].id, followed = new List<string> { npc[0].id } });
            var call = CommitmentsRead.Of(s).Single(c => c.kind == CommitmentsRead.Kinds.Call);
            Assert.That((call.outcome, call.status, call.term), Is.EqualTo((CommitmentsRead.Outcomes.Open, "with you", "this week")));
            s.phase = EpisodePhase.Eviction; s.evictionResolved = true;
            Assert.That(CommitmentsRead.Of(s).Single(c => c.kind == CommitmentsRead.Kinds.Call).outcome, Is.EqualTo(CommitmentsRead.Outcomes.Kept));
        }

        [Test]
        public void NothingIsReadForNoSeasonOrNoPlayer()
        {
            Assert.That(CommitmentsRead.Of(null), Is.Empty);
            var s = Season();
            s.playerId = "nobody";
            Assert.That(CommitmentsRead.Of(s), Is.Empty);
            Assert.That(CommitmentsRead.WouldBreak(null, CommitmentsRead.Decision.Vote("x")), Is.Empty);
            Assert.That(CommitmentsRead.Warning(Season(), new List<CommitmentsRead.Breach>()), Is.Null);
        }

        // ---------------------------------------------------------------- the dry run, held to the engine

        [Test]
        public void ANominationWarnsOfExactlyWhatTheCommitBreaks()
        {
            var s = AtNominations();
            var npc = EpisodeEngine.NominationCandidates(s).ToList();
            string player = s.playerId;
            Deal(s, "d-safety", DealKind.SafetyAgreement, player, npc[0].id);
            Deal(s, "d-target", DealKind.TargetAgreement, npc[1].id, player, about: npc[5].id);
            Deal(s, "d-other", DealKind.SafetyAgreement, player, npc[6].id);
            Deal(s, "d-npc", DealKind.SafetyAgreement, npc[2].id, npc[0].id);
            Promise(s, "p-safety", PromiseKind.Safety, player, npc[1].id, expires: 2);
            Promise(s, "p-npc", PromiseKind.Safety, npc[3].id, npc[0].id, expires: 2);
            Oath(s, npc[0].id, 1);
            Valid(s);
            var before = Fingerprint(s);

            var decision = CommitmentsRead.Decision.Nominate(npc[0].id, npc[1].id);
            var warned = CommitmentsRead.WouldBreak(s, decision);
            Assert.That(Fingerprint(s), Is.EqualTo(before), "The dry run writes nothing to the season it read, and draws nothing from its stream.");
            Assert.That(Ids(warned), Is.EquivalentTo(new[] { "deal:d-safety", "deal:d-target", "promise:p-safety", OathKey(s, npc[0].id) }),
                "The safety deal and the oath with the first, the target deal and the promise with the second; nothing of anybody else's.");
            Assert.That(CommitmentsRead.CountOf(warned), Is.EqualTo(4), "Four commitments, each counted once.");
            Assert.That(Ids(CommitmentsRead.ByTheRules(s, decision)), Is.EquivalentTo(Ids(warned)), "The rules and the engine's run agree.");

            var after = Commit(s, Nominate(s, npc[0].id, npc[1].id));
            Assert.That(BrokenOfThePlayers(s, after), Is.EquivalentTo(Ids(warned)), "and so does the commit that follows.");
            Assert.That(after.deals.Single(d => d.id == "d-npc").status, Is.EqualTo(DealStatus.Active), "A deal between two houseguests is theirs.");

            Assert.That(CommitmentsRead.Warning(s, warned), Is.EqualTo(
                "Nominating " + First(npc[0]) + " breaks your safety deal with " + Them(npc[0]) + " and your loyalty oath to " + Them(npc[0]) + ". "
                + "Nominating " + First(npc[1]) + " breaks your deal with " + Them(npc[1]) + " to put " + First(npc[5]) + " up and your promise of safety to " + Them(npc[1]) + "."));
        }

        [Test]
        public void ANominationThatHitsTheAgreedTargetBreaksNothing()
        {
            var s = AtNominations();
            var npc = EpisodeEngine.NominationCandidates(s).ToList();
            Deal(s, "d-target", DealKind.TargetAgreement, s.playerId, npc[0].id, about: npc[1].id);
            Valid(s);
            var decision = CommitmentsRead.Decision.Nominate(npc[0].id, npc[1].id);
            Assert.That(CommitmentsRead.WouldBreak(s, decision), Is.Empty, "Hitting the target keeps the deal even with the partner beside them.");
            Assert.That(CommitmentsRead.ByTheRules(s, decision), Is.Empty);
            Assert.That(CommitmentsRead.Warning(s, CommitmentsRead.WouldBreak(s, decision)), Is.Null);
            var after = Commit(s, Nominate(s, npc[0].id, npc[1].id));
            Assert.That(after.deals.Single().status, Is.EqualTo(DealStatus.Fulfilled));
        }

        [Test]
        public void AnIllegalNominationIsNotWarnedAbout()
        {
            var s = AtNominations();
            var npc = EpisodeEngine.NominationCandidates(s).ToList();
            Deal(s, "d-safety", DealKind.SafetyAgreement, s.playerId, npc[0].id);
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Nominate(npc[0].id, npc[0].id)), Is.Empty);
            Assert.That(CommitmentsRead.ByTheRules(s, CommitmentsRead.Decision.Nominate(npc[0].id, npc[0].id)), Is.Empty);
        }

        [Test]
        public void TheVetoHoldersChoicesWarnOfExactlyWhatEachCommitBreaks()
        {
            var s = AtTheVetoMeeting(playerHoh: false);
            string first = s.nominees[0], second = s.nominees[1];
            Deal(s, "d-veto", DealKind.VetoUse, first, s.playerId, expires: s.week);
            Valid(s);
            var before = Fingerprint(s);

            var choices = new[]
            {
                CommitmentsRead.Decision.Veto(true, first), CommitmentsRead.Decision.Veto(true, second), CommitmentsRead.Decision.Veto(false),
            };
            var warned = choices.Select(choice => CommitmentsRead.WouldBreak(s, choice)).ToList();
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            Assert.That(warned[0], Is.Empty, "Saving the partner keeps the deal.");
            Assert.That(Ids(warned[1]), Is.EqualTo(new[] { "deal:d-veto" }), "Saving the other nominee breaks it,");
            Assert.That(Ids(warned[2]), Is.EqualTo(new[] { "deal:d-veto" }), "and so does keeping the block.");
            for (int i = 0; i < choices.Length; i++)
            {
                Assert.That(Ids(CommitmentsRead.ByTheRules(s, choices[i])), Is.EquivalentTo(Ids(warned[i])), "The rules agree on choice " + i + ".");
                var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ResolveVeto);
                command.useVeto = choices[i].useVeto; command.targetId = choices[i].firstId;
                var after = Commit(s, command);
                Assert.That(BrokenOfThePlayers(s, after), Is.EquivalentTo(Ids(warned[i])), "The commit agrees on choice " + i + ".");
            }
            Assert.That(CommitmentsRead.Warning(s, warned.SelectMany(w => w)), Is.EqualTo(
                "Saving " + First(s, second) + " or not using the veto breaks your veto deal with " + First(s, first) + "."));
        }

        [Test]
        public void AHeadOfHouseholdHoldingTheVetoIsWarnedAboutTheReplacementToo()
        {
            var s = AtTheVetoMeeting(playerHoh: true);
            string first = s.nominees[0];
            var candidates = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
            Assert.That(candidates.Count, Is.GreaterThanOrEqualTo(2));
            Deal(s, "d-safety", DealKind.SafetyAgreement, candidates[0], s.playerId);
            Promise(s, "p-safety", PromiseKind.Safety, s.playerId, candidates[1], expires: 2);
            Valid(s);

            foreach (string replacement in candidates.Take(3))
            {
                var decision = CommitmentsRead.Decision.Veto(true, first, replacement);
                var warned = CommitmentsRead.WouldBreak(s, decision);
                Assert.That(Ids(CommitmentsRead.ByTheRules(s, decision)), Is.EquivalentTo(Ids(warned)));
                var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ResolveVeto);
                command.useVeto = true; command.targetId = first; command.secondTargetId = replacement;
                Assert.That(BrokenOfThePlayers(s, Commit(s, command)), Is.EquivalentTo(Ids(warned)), "Naming " + replacement + ".");
                Assert.That(warned.All(b => b.act == CommitmentsRead.Acts.Replace && b.causeId == replacement), Is.True);
            }
            Assert.That(CommitmentsRead.Warning(s, CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Veto(true, first, candidates[0]))), Is.EqualTo(
                "Naming " + First(s, candidates[0]) + " as the replacement breaks your safety deal with " + Them(s.Find(candidates[0])) + "."));
        }

        /// <summary>
        /// The screens sweep every replacement they offer by the rules on one copy of the season,
        /// where an engine run each would be one a name: the sweep says exactly what each name
        /// alone would break - an oath among them, which a shared record of what was broken could
        /// have swallowed - and the engine, committing each name, agrees.
        /// </summary>
        [Test]
        public void ASweepOfEveryReplacementIsJudgedOnOneCopyAsEachNameIsAlone()
        {
            var s = AtTheVetoMeeting(playerHoh: true);
            string first = s.nominees[0];
            var candidates = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
            Assert.That(candidates.Count, Is.GreaterThanOrEqualTo(3));
            Deal(s, "d-safety", DealKind.SafetyAgreement, candidates[0], s.playerId);
            Promise(s, "p-safety", PromiseKind.Safety, s.playerId, candidates[1], expires: 2);
            Oath(s, candidates[2], 1);
            Valid(s);
            var before = Fingerprint(s);

            var sweep = candidates.Select(replacement => CommitmentsRead.Decision.Veto(true, first, replacement)).ToList();
            var swept = CommitmentsRead.ByTheRules(s, sweep);
            Assert.That(Fingerprint(s), Is.EqualTo(before), "The sweep writes nothing to the season it read.");
            Assert.That(Ids(swept), Is.EquivalentTo(sweep.SelectMany(decision => Ids(CommitmentsRead.ByTheRules(s, decision)))),
                "One copy for them all says what each name alone says.");
            Assert.That(Ids(swept), Is.EquivalentTo(new[] { "deal:d-safety", "promise:p-safety", OathKey(s, candidates[2]) }));
            foreach (var decision in sweep.Take(3))
            {
                var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ResolveVeto);
                command.useVeto = true; command.targetId = first; command.secondTargetId = decision.secondId;
                Assert.That(BrokenOfThePlayers(s, Commit(s, command)), Is.EquivalentTo(Ids(swept.Where(b => b.causeId == decision.secondId))),
                    "The engine agrees on naming " + decision.secondId + ".");
            }
            Assert.That(CommitmentsRead.Warning(s, swept), Is.EqualTo(
                "Naming " + First(s, candidates[0]) + " as the replacement breaks your safety deal with " + Them(s.Find(candidates[0])) + ". "
                + "Naming " + First(s, candidates[1]) + " as the replacement breaks your promise of safety to " + Them(s.Find(candidates[1])) + ". "
                + "Naming " + First(s, candidates[2]) + " as the replacement breaks your loyalty oath to " + Them(s.Find(candidates[2])) + "."));
            Assert.That(CommitmentsRead.ByTheRules(s, new CommitmentsRead.Decision[0]), Is.Empty, "No decisions, nothing to judge.");
        }

        /// <summary>
        /// The ballot is warned of only while it is the player's to cast, as the engine takes one:
        /// not before the house votes, not once theirs is in, not when the eviction is over, not
        /// from the block or the Head of Household's chair - except to break a tie.
        /// </summary>
        [Test]
        public void ABallotIsWarnedOfOnlyWhileThePlayerCanCastIt()
        {
            var s = AtTheVote();
            string keep = s.nominees[0], evict = s.nominees[1];
            var voters = EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ToList();
            Promise(s, "p-vote", PromiseKind.Vote, s.playerId, voters[0], about: evict, expires: s.week);
            Valid(s);
            Assert.That(Ids(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Vote(keep))), Is.EqualTo(new[] { "promise:p-vote" }),
                "Voting against the promise breaks it.");

            EpisodeState Shaped(System.Action<EpisodeState> shape) { var copy = s.Clone(); shape(copy); return copy; }
            var closed = new Dictionary<string, EpisodeState>
            {
                ["before the house votes"] = Shaped(x => x.evictionStage = EvictionStage.Speeches),
                ["once the player's ballot is in"] = Shaped(x => x.votes.Add(new VoteState { voterId = x.playerId, targetId = evict, reason = "test" })),
                ["once the eviction is over"] = Shaped(x => x.evictionResolved = true),
                ["outside the eviction"] = Shaped(x => x.phase = EpisodePhase.Campaign),
                ["from the block"] = Shaped(x => x.nominees = new List<string> { x.playerId, evict }),
                ["from the Head of Household's chair"] = Shaped(x => x.hohId = x.playerId),
            };
            foreach (var pair in closed)
            {
                Assert.That(CommitmentsRead.WouldBreak(pair.Value, CommitmentsRead.Decision.Vote(keep)), Is.Empty, "No ballot to warn of " + pair.Key + ".");
                Assert.That(CommitmentsRead.WouldBreak(pair.Value, CommitmentsRead.Decision.Vote(evict)), Is.Empty, "Nor the other way, " + pair.Key + ".");
            }

            // The Head of Household breaks a tie with a ballot the reveal judges like any other.
            var tie = AtTheVote(size: 9);
            tie.hohId = tie.playerId;
            var tied = EpisodeEngine.Voters(tie).Select(v => v.id).ToList();
            Assert.That(tied.Count % 2, Is.EqualTo(0), "Precondition: an even house of voters.");
            for (int i = 0; i < tied.Count; i++)
                tie.votes.Add(new VoteState { voterId = tied[i], targetId = tie.nominees[i % 2], reason = "test" });
            tie.evictionStage = EvictionStage.Tiebreaker;
            Promise(tie, "p-tie", PromiseKind.Vote, tie.playerId, tied[0], about: tie.nominees[1], expires: tie.week);
            Assert.That(EpisodeEngine.NeedsPlayerTieBreak(tie), Is.True, "Precondition: the tie is the player's to break.");
            Assert.That(Ids(CommitmentsRead.WouldBreak(tie, CommitmentsRead.Decision.Vote(tie.nominees[0]))), Is.EqualTo(new[] { "promise:p-tie" }));
        }

        /// <summary>
        /// The knowledge gate in the dry run: when the player uses the veto, a houseguest Head of
        /// Household names the replacement, and whom they name can break their own word to the player.
        /// The engine's run on the copy does break it; the warning must not say so, because it is
        /// the houseguest's breach and saying it would say whom they mean to name.
        /// </summary>
        [Test]
        public void AHouseguestsReplacementIsNeverTheWarningsToGiveAway()
        {
            var s = AtTheVetoMeeting(playerHoh: false);
            string hoh = s.hohId;
            var candidates = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
            // Allied with everybody who could go up, so whoever they name is an ally.
            s.alliances.Add(new AllianceState { id = "hoh-pact", name = "The Head's Pact", members = new List<string> { hoh }.Concat(candidates).ToList() });
            Promise(s, "p-hoh", PromiseKind.AllianceLoyalty, hoh, s.playerId, expires: 2);
            Valid(s);
            var decision = CommitmentsRead.Decision.Veto(true, s.nominees[0]);
            var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ResolveVeto);
            command.useVeto = true; command.targetId = s.nominees[0];
            var after = Commit(s, command);
            Assert.That(after.promises.Single(p => p.id == "p-hoh").status, Is.EqualTo(PromiseStatus.Broken),
                "Precondition: the Head of Household's replacement breaks their own word to the player.");
            Assert.That(CommitmentsRead.WouldBreak(s, decision), Is.Empty, "and the warning keeps it to itself.");
            Assert.That(CommitmentsRead.ByTheRules(s, decision), Is.Empty);
        }

        [Test]
        public void AHeadOfHouseholdNamingTheReplacementForAHouseguestsSaveIsWarnedOfTheirOwnWordOnly()
        {
            var s = AtTheVetoMeeting(playerHoh: true);
            // A houseguest on the block holds the veto and saves themselves.
            string holder = s.nominees[0];
            s.vetoHolderId = holder;
            s.vetoPlayers = Lineup(s);
            Assert.That(EpisodeEngine.NpcVetoSave(s), Is.EqualTo(holder), "Precondition: a holder on the block saves themselves.");
            var candidates = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
            Deal(s, "d-holder", DealKind.VetoUse, s.playerId, holder);
            Deal(s, "d-safety", DealKind.SafetyAgreement, s.playerId, candidates[0]);
            Valid(s);
            foreach (string replacement in candidates.Take(2))
            {
                var decision = CommitmentsRead.Decision.Veto(true, holder, replacement);
                var warned = CommitmentsRead.WouldBreak(s, decision);
                Assert.That(Ids(warned), Is.EquivalentTo(replacement == candidates[0] ? new[] { "deal:d-safety" } : new string[0]),
                    "Naming " + replacement + ": the player's own nomination, and never the holder's choice.");
                Assert.That(Ids(CommitmentsRead.ByTheRules(s, decision)), Is.EquivalentTo(Ids(warned)));
                var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ResolveVeto);
                command.useVeto = true; command.targetId = holder; command.secondTargetId = replacement;
                Assert.That(BrokenOfThePlayers(s, Commit(s, command)), Is.EquivalentTo(Ids(warned)));
            }
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Veto(true, s.nominees[1], candidates[0])), Is.Empty,
                "A save that is not the holder's is not a decision the Head of Household can make.");
        }

        [Test]
        public void ABallotIsJudgedOnThePlayersOwnBallotAsTheRevealJudgesIt()
        {
            var s = AtTheVote();
            string keep = s.nominees[0], evict = s.nominees[1];
            var voters = EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ToList();
            Deal(s, "d-vote", DealKind.VoteEvict, s.playerId, voters[0], about: evict, expires: s.week);
            Promise(s, "p-vote", PromiseKind.Vote, s.playerId, voters[1], about: evict, expires: s.week);
            Oath(s, keep, 1);
            Valid(s);
            var before = Fingerprint(s);

            var keeping = CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Vote(evict));
            Assert.That(keeping, Is.Empty, "Voting as promised breaks nothing.");
            var breaking = CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Vote(keep));
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            Assert.That(Ids(breaking), Is.EquivalentTo(new[] { "promise:p-vote", OathKey(s, keep), "deal:d-vote" }));
            Assert.That(CommitmentsRead.Warning(s, breaking), Is.EqualTo(
                "Voting to evict " + First(s, keep) + " breaks your promise to " + First(s, voters[1]) + " to vote out " + First(s, evict)
                + ", your loyalty oath to " + Them(s.Find(keep)) + " and your deal with " + First(s, voters[0]) + " to evict " + First(s, evict) + "."));

            // Through the reveal: everything the player's own ballot broke is broken, whatever the
            // partners voted. The partner's ballot can break the deal too, which no warning can know.
            foreach (string target in new[] { keep, evict })
            {
                var engine = new EpisodeEngine(s);
                var vote = EpisodeEngineTests.Command(s, EpisodeCommandKind.CastVote);
                vote.targetId = target;
                Assert.That(engine.Apply(vote).accepted, Is.True);
                for (int guard = 0; guard < 6 && !engine.Snapshot.evictionResolved; guard++)
                    Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
                var after = engine.Snapshot;
                Assert.That(after.evictionResolved, Is.True, "The reveal ran.");
                var warned = Ids(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Vote(target)));
                var broken = BrokenOfThePlayers(s, after);
                Assert.That(broken.Intersect(warned), Is.EquivalentTo(warned), "Everything warned of was broken at the reveal (" + target + ").");
                Assert.That(broken.Where(id => !id.StartsWith("deal:")), Is.EquivalentTo(warned.Where(id => !id.StartsWith("deal:"))),
                    "The player's promises and oaths are judged on their own ballot alone, exactly as warned (" + target + ").");
            }
        }

        [Test]
        public void AVotingBlockIsNeverWarnedAboutItIsDecidedByAPrivateBallot()
        {
            var s = AtTheVote();
            var voter = EpisodeEngine.Voters(s).First(v => !v.isPlayer).id;
            Deal(s, "d-block", DealKind.VoteTogether, s.playerId, voter, expires: s.week);
            foreach (string target in s.nominees)
                Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Vote(target)), Is.Empty,
                    "Whether a voting block holds turns on the partner's ballot, which the player cannot see before the reveal.");
        }

        [Test]
        public void TheFinalChoiceWarnsOfExactlyWhatTheCommitBreaks()
        {
            var s = FinalEvictionPending();
            var finalists = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            var juror = s.contestants.First(c => c.status == ContestantStatus.Jury).id;
            Deal(s, "d-final", DealKind.FinalTwo, finalists[0], s.playerId, week: s.week);
            Promise(s, "p-final", PromiseKind.FinalTwo, s.playerId, finalists[1], week: s.week);
            Deal(s, "d-juror", DealKind.FinalTwo, s.playerId, juror, week: s.week);
            Valid(s);
            var before = Fingerprint(s);

            var takeSecond = CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.FinalEviction(finalists[0]));
            var takeFirst = CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.FinalEviction(finalists[1]));
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            Assert.That(Ids(takeSecond), Is.EquivalentTo(new[] { "deal:d-final", "deal:d-juror" }));
            Assert.That(Ids(takeFirst), Is.EquivalentTo(new[] { "promise:p-final", "deal:d-juror" }),
                "A Final 2 deal with a juror is broken whichever finalist is taken, as the engine settles it today.");
            foreach (var (evicted, warned) in new[] { (finalists[0], takeSecond), (finalists[1], takeFirst) })
            {
                Assert.That(Ids(CommitmentsRead.ByTheRules(s, CommitmentsRead.Decision.FinalEviction(evicted))), Is.EquivalentTo(Ids(warned)));
                var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.FinalEvict);
                command.targetId = evicted;
                Assert.That(BrokenOfThePlayers(s, Commit(s, command)), Is.EquivalentTo(Ids(warned)));
            }
            Assert.That(CommitmentsRead.Warning(s, takeSecond.Where(b => b.id == "d-final")), Is.EqualTo(
                "Taking " + First(s, finalists[1]) + " to the Final 2 breaks your final two deal with " + First(s, finalists[0]) + "."));
        }

        [Test]
        public void ADecisionThatIsNotThePlayersToMakeIsNotWarnedAbout()
        {
            var s = AtNominations();
            s.hohId = EpisodeEngine.NominationCandidates(s).First().id;
            var npc = EpisodeEngine.NominationCandidates(s).ToList();
            Deal(s, "d-safety", DealKind.SafetyAgreement, s.playerId, npc[0].id);
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Nominate(npc[0].id, npc[1].id)), Is.Empty);
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Veto(false)), Is.Empty);
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.FinalEviction(npc[0].id)), Is.Empty);
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Vote(npc[0].id)), Is.Empty);
        }

        // ---------------------------------------------------------------- the notes

        [Test]
        public void ADealNoteSaysWhomItIsAboutAndHowItEnded()
        {
            var s = Season();
            var npc = Npcs(s);
            string first = First(npc[0]);
            s.week = 3;
            s.ledger.power.Add(new PowerRow { week = 2, hohId = s.playerId, nominees = new List<string> { npc[0].id, npc[2].id } });
            Deal(s, "d-target", DealKind.TargetAgreement, s.playerId, npc[0].id, about: npc[1].id, status: DealStatus.Broken, week: 2, expires: 2);
            Deal(s, "d-vote", DealKind.VoteSave, npc[0].id, s.playerId, about: npc[2].id, week: 3, expires: 3);
            var texts = HouseguestNotes.For(s, npc[0].id).Select(n => n.text).ToList();
            Assert.That(texts, Has.Some.EqualTo("You put a target agreement to " + first + " (about " + npc[1].name + ") · broken by you"));
            Assert.That(texts, Has.Some.EqualTo(first + " put a vote to save to you (about " + npc[2].name + ") · agreed"));

            // The campaign's Recent Intel row holds one short line: the offer and where it stands, as it always read.
            var intel = CampaignBrief.RecentIntel(s, 20).Select(line => line.text).ToList();
            Assert.That(intel, Has.Some.EqualTo("You put a target agreement to " + first + " · broken"));
            Assert.That(intel, Has.Some.EqualTo(first + " put a vote to save to you · agreed"));
            Assert.That(intel.Any(line => line.Contains("(about ")), Is.False, "Whom an offer was about stays on the notes page, where there is room for it.");
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed = 21, int size = 8) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToList();

        /// <summary>The fixture is a season the engine will hold, so a dry run of it is the engine's and not the rules' fallback.</summary>
        private static void Valid(EpisodeState s) =>
            Assert.That(EpisodeValidation.TryValidate(s, out var why), Is.True, "The fixture must be a valid season: " + why);

        private static string First(ContestantState who) => FinalistRead.FirstName(who.name);
        private static string First(EpisodeState s, string id) => First(s.Find(id));
        private static string Them(ContestantState who) => StoryPeople.Pronouns(who).them;

        private static DealState Deal(EpisodeState s, string id, string type, string proposer, string recipient, string about = null,
            string status = DealStatus.Active, int week = 0, int expires = 0)
        {
            var deal = new DealState
            {
                id = id, type = type, proposerId = proposer, recipientId = recipient, targetId = about, status = status,
                week = week > 0 ? week : s.week, expiresWeek = expires, trustImpact = DealKind.DefaultTrust(type),
            };
            s.deals.Add(deal);
            return deal;
        }

        private static PromiseState Promise(EpisodeState s, string id, PromiseKind kind, string from, string to, string about = null,
            PromiseStatus status = PromiseStatus.Active, int week = 0, int expires = 0)
        {
            var promise = new PromiseState
            {
                id = id, kind = kind, fromId = from, toId = to, targetId = about, status = status,
                week = week > 0 ? week : s.week, expiresWeek = expires,
            };
            s.promises.Add(promise);
            return promise;
        }

        /// <summary>
        /// The player's loyalty declaration to a houseguest as the engine records one: the oath, the
        /// milestone validation asks for, and the note on the player's own edge with them.
        /// </summary>
        private static WebOathRecord Oath(EpisodeState s, string npcId, int week)
        {
            if (!s.shownOathMilestones.Contains(npcId)) s.shownOathMilestones.Add(npcId);
            var oath = new WebOathRecord { playerId = s.playerId, targetId = npcId, week = week, timestamp = s.nextSequence++ };
            s.loyaltyOaths.Add(oath);
            var edge = s.relationships.Single(r => r.fromId == s.playerId && r.toId == npcId);
            edge.notes.Add("loyalty-oath");
            return oath;
        }

        private static string OathKey(EpisodeState s, string npcId) =>
            "oath:" + CommitmentsRead.OathId(s.loyaltyOaths.Single(o => o.targetId == npcId));

        private static List<string> Ids(IEnumerable<CommitmentsRead.Breach> breaches) =>
            breaches.Select(b => b.kind + ":" + b.id).ToList();

        /// <summary>The player's commitments standing in <paramref name="before"/> and broken in <paramref name="after"/>, as breach ids.</summary>
        private static List<string> BrokenOfThePlayers(EpisodeState before, EpisodeState after)
        {
            string player = before.playerId;
            var ids = new List<string>();
            foreach (var deal in before.deals.Where(d => d.status == DealStatus.Active && (d.proposerId == player || d.recipientId == player)))
                if (after.deals.Single(d => d.id == deal.id).status == DealStatus.Broken) ids.Add("deal:" + deal.id);
            foreach (var promise in before.promises.Where(p => p.status == PromiseStatus.Active && p.fromId == player))
                if (after.promises.Single(p => p.id == promise.id).status == PromiseStatus.Broken) ids.Add("promise:" + promise.id);
            foreach (var oath in before.loyaltyOaths)
                if (!after.loyaltyOaths.Any(o => o.targetId == oath.targetId)) ids.Add("oath:" + CommitmentsRead.OathId(oath));
            return ids;
        }

        /// <summary>What a dry run must leave as it was: the revision, the stream, the sequence and every record's standing.</summary>
        private static string Fingerprint(EpisodeState s) => string.Join("|", new[]
        {
            s.revision.ToString(), s.randomState.ToString(), s.nextSequence.ToString(), s.phase.ToString(), s.events.Count.ToString(),
            string.Join(",", s.nominees), string.Join(",", s.deals.Select(d => d.id + "=" + d.status)),
            string.Join(",", s.promises.Select(p => p.id + "=" + p.status)), string.Join(",", s.loyaltyOaths.Select(o => o.targetId)),
            string.Join(",", s.relationships.Select(r => r.score.ToString("0.###"))),
        });

        private static EpisodeState Commit(EpisodeState s, EpisodeCommand command)
        {
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static EpisodeCommand Nominate(EpisodeState s, string first, string second)
        {
            var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.Nominate);
            command.targetId = first; command.secondTargetId = second;
            return command;
        }

        /// <summary>A season parked at the nominations with the player holding the house, placed rather than played.</summary>
        private static EpisodeState AtNominations(uint seed = 5)
        {
            var s = Season(seed, 10);
            s.phase = EpisodePhase.Nomination;
            s.hohId = s.playerId;
            s.competitionResolved = true;
            return s;
        }

        /// <summary>The veto meeting with the player holding the veto: a houseguest or the player at the head of the house, two houseguests on the block.</summary>
        private static EpisodeState AtTheVetoMeeting(bool playerHoh, uint seed = 7)
        {
            var s = Season(seed, 8);
            var npc = Npcs(s);
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = playerHoh ? s.playerId : npc[0].id;
            s.nominees = new List<string> { npc[1].id, npc[2].id };
            s.vetoHolderId = s.playerId;
            s.vetoPlayers = Lineup(s);
            return s;
        }

        /// <summary>The veto's six: the Head of Household, the block and the holder, then the rest of the house in order.</summary>
        private static List<string> Lineup(EpisodeState s)
        {
            var seats = new List<string> { s.hohId }.Concat(s.nominees).Concat(new[] { s.vetoHolderId })
                .Concat(s.Active.Select(c => c.id)).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            return seats.Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
        }

        /// <summary>Eviction night at the vote, under the levers: a houseguest at the head of the house, two on the block, the player voting.</summary>
        private static EpisodeState AtTheVote(uint seed = 9, int size = 8)
        {
            var s = Season(seed, size);
            var npc = Npcs(s);
            s.phase = EpisodePhase.Eviction;
            s.evictionStage = EvictionStage.Voting;
            s.hohId = npc[0].id;
            s.nominees = new List<string> { npc[1].id, npc[2].id };
            s.vetoHolderId = npc[3].id;
            s.vetoResolved = true;
            s.vetoPlayers = Lineup(s);
            EpisodeEngine.EnableLevers(s);
            return s;
        }

        /// <summary>A real season walked to the final eviction with the player as the final Head of Household.</summary>
        private static EpisodeState FinalEvictionPending()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 400; guard++)
                {
                    var snapshot = engine.Snapshot;
                    if (snapshot.phase == EpisodePhase.FinalEviction)
                    {
                        if (snapshot.hohId == snapshot.playerId) return snapshot;
                        break;
                    }
                    if (snapshot.phase == EpisodePhase.Finished || snapshot.Find(snapshot.playerId).status != ContestantStatus.Active) break;
                    var result = engine.Apply(EpisodeEngineTests.NextCommand(snapshot));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.Fail("No season of the first forty reached a final eviction the player makes.");
            return null;
        }
    }
}
