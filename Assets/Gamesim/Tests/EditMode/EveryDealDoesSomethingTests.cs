using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// ACTIONS-DEALS-ALLIANCES-PLAN C1, every deal does something and accepting commits you: an
    /// information deal passes one reading a week, a partnership is judged at the vote, a safety pact
    /// is kept and rewarded when the Head of Household spares their partner, a final two deal is an
    /// obligation in the final Head of Household's choice, deals and final two promises end with an
    /// evictee (X4), a vote deal both parties broke names neither, and accepting an offer is +4 and
    /// weighs one step heavier broken (decision 15). Each is shown as a season without the commitment
    /// rules plays it - as every recorded season does - and as it plays under them. X4's fixture first.
    /// Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class EveryDealDoesSomethingTests
    {
        // ------------------------------------------------------------ X4: a final two with somebody already gone

        [Test]
        public void X4_UnderTheRulesTheFinalChoiceBreaksNoFinalTwoWithSomebodyAlreadyGone()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = FinalEviction(rules, out string hoh, out string juror);
                string player = s.playerId, other = FinalistOtherThanThePlayer(s);
                s.deals.Add(Deal("deal-final-juror", DealKind.FinalTwo, hoh, juror, week: 3, expires: 0));
                s.promises.Add(Promise("promise-final-juror", hoh, juror, PromiseKind.FinalTwo, week: 3));
                // The two finalists the final Head of Household chooses between: one of them leaves.
                s.deals.Add(Deal("deal-finalists", DealKind.Partnership, player, other, week: 3, expires: 0));
                Valid(s);
                double before = s.Score(juror, hoh);
                var after = Advance(s);
                Assert.That(after.phase, Is.Not.EqualTo(EpisodePhase.FinalEviction), "The final Head of Household chose.");
                var deal = after.deals.Single(d => d.id == "deal-final-juror");
                var promise = after.promises.Single(p => p.id == "promise-final-juror");
                var finalists = after.deals.Single(d => d.id == "deal-finalists");
                Valid(after);
                if (rules)
                {
                    Assert.That(deal.status, Is.Not.EqualTo(DealStatus.Broken), "A final two with a juror is no choice the final Head of Household made.");
                    Assert.That(promise.status, Is.Not.EqualTo(PromiseStatus.Broken), "Nor is a final two promise to one.");
                    Assert.That(after.Score(juror, hoh), Is.EqualTo(before), "The juror holds nothing against them for it,");
                    Assert.That(Entries(after, juror, hoh, "deal_broken").Concat(Entries(after, juror, hoh, "promise-broken")), Is.Empty, "on their record");
                    Assert.That(WebJuryVoting.Obligations(after, juror, hoh), Is.GreaterThanOrEqualTo(0), "or with their jury vote.");
                    Assert.That(finalists.status, Is.EqualTo(DealStatus.Expired), "A deal with the finalist evicted ends as they leave.");
                    Assert.That(finalists.settledWeek, Is.Zero, "An ending is not a settlement.");
                }
                else
                {
                    Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "Without the rules the final eviction broke it,");
                    Assert.That(promise.status, Is.EqualTo(PromiseStatus.Broken), "and the promise,");
                    Assert.That(after.Score(juror, hoh), Is.LessThan(before - 40), "at -45 for the deal and the promise's cost besides,");
                    Assert.That(WebJuryVoting.Obligations(after, juror, hoh), Is.EqualTo(-50), "and -50 with the jury.");
                    Assert.That(finalists.status, Is.EqualTo(DealStatus.Active), "and a deal with the evicted finalist stood.");
                }
            }
        }

        [Test]
        public void X4_UnderTheRulesDealsAndFinalTwoPromisesEndWithAnEvictee()
        {
            string offer = NpcDeals.OfferPrefix + "90";
            var endsWithThem = new[] { "deal-final-two", "deal-pact", "deal-about", offer };
            foreach (bool rules in new[] { false, true })
            {
                var s = Campaign(rules);
                var npcs = NpcIds(s);
                string gone = s.nominees[0], stays = s.nominees[1], holder = npcs[3], last = npcs[4];
                foreach (var voter in EpisodeEngine.Voters(s)) { SetScore(s, voter.id, gone, -100); SetScore(s, voter.id, stays, 100); }
                s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, s.playerId, gone, expires: 0));
                s.deals.Add(Deal("deal-pact", DealKind.SafetyAgreement, holder, gone, expires: 2));
                s.deals.Add(Deal("deal-about", DealKind.TargetAgreement, holder, last, target: gone, expires: 1));
                s.deals.Add(Deal(offer, DealKind.Partnership, gone, s.playerId, status: DealStatus.Proposed, expires: 1));
                s.promises.Add(Promise("promise-to", s.playerId, gone, PromiseKind.FinalTwo));
                s.promises.Add(Promise("promise-from", gone, last, PromiseKind.FinalTwo));
                s.promises.Add(Promise("promise-safety", last, gone, PromiseKind.Safety, expires: 2));
                Valid(s);
                var after = Evict(s);
                Assert.That(after.Find(gone).status, Is.EqualTo(ContestantStatus.Jury), "The house voted them out.");
                Valid(after);
                if (rules)
                {
                    foreach (var id in endsWithThem)
                    {
                        var deal = after.deals.Single(d => d.id == id);
                        Assert.That(deal.status, Is.EqualTo(DealStatus.Expired), id + " ends with the one it binds or names.");
                        Assert.That(deal.settledWeek == 0 && deal.brokenById == null, Is.True, id + ": an ending, not a verdict.");
                    }
                    Assert.That(after.promises.Single(p => p.id == "promise-to").status, Is.EqualTo(PromiseStatus.Expired), "A final two promise made to them ends,");
                    Assert.That(after.promises.Single(p => p.id == "promise-from").status, Is.EqualTo(PromiseStatus.Expired), "and one they made.");
                    Assert.That(after.promises.Single(p => p.id == "promise-safety").status, Is.EqualTo(PromiseStatus.Active), "Other promises are as they were.");
                    Assert.That(after.relationships.SelectMany(r => r.events).Any(e => e.type == "deal_broken" || e.type == "promise-broken"), Is.False,
                        "Nothing is held against anybody for it.");
                }
                else
                {
                    Assert.That(after.deals.Single(d => d.id == "deal-final-two").status, Is.EqualTo(DealStatus.Active), "Without the rules a final two with a juror stood,");
                    Assert.That(after.deals.Single(d => d.id == offer).status, Is.EqualTo(DealStatus.Proposed), "an offer from them waited,");
                    Assert.That(after.promises.Single(p => p.id == "promise-to").status, Is.EqualTo(PromiseStatus.Active), "and so did a promise, for the final eviction to break.");
                }
                // The player took the final two: ended or not, it is on their record as taken, not as an offer they let lapse.
                var taken = after.ledger.opportunities.Single(o => o.id == "deal-final-two");
                Assert.That(taken.response, Is.EqualTo(OpportunityResponse.Taken));
                EpisodeEngine.ReconcileOpportunities(after);
                Assert.That(after.ledger.opportunities.Single(o => o.id == "deal-final-two").response, Is.EqualTo(OpportunityResponse.Taken), "and stays so at the next reconcile.");
                Assert.That(GameSense.Evaluate(after).notes.Where(n => n.rowId == "deal-final-two").Select(n => n.points), Is.EqualTo(new[] { 1.0 }), "A deal made, not an offer left on the table.");
            }
        }

        [Test]
        public void X4_UnderTheRulesThePlayersFinalChoiceIsWarnedOfNothingWithSomebodyAlreadyGone()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = FinalEviction(rules, out _, out string juror, playerChooses: true);
                s.promises.Add(Promise("promise-final-juror", s.playerId, juror, PromiseKind.FinalTwo, week: 3));
                s.deals.Add(Deal("deal-final-juror", DealKind.FinalTwo, s.playerId, juror, week: 3, expires: 0));
                Valid(s);
                string evict = FinalistsBesidesTheHead(s)[0];
                var decision = CommitmentsRead.Decision.FinalEviction(evict);
                var dryRun = CommitmentsRead.WouldBreak(s, decision).Select(b => b.id).ToList();
                var byTheRules = CommitmentsRead.ByTheRules(s, decision).Select(b => b.id).ToList();
                if (rules)
                {
                    Assert.That(dryRun, Is.Empty, "The engine's run breaks nothing with a juror,");
                    Assert.That(byTheRules, Is.Empty, "and the rules' sweep agrees.");
                }
                else
                {
                    Assert.That(dryRun, Is.EquivalentTo(new[] { "promise-final-juror", "deal-final-juror" }), "Without the rules both broke whichever finalist was taken,");
                    Assert.That(byTheRules, Is.EquivalentTo(dryRun), "and the sweep said so too.");
                }
                // The final choice's page says what breaks whichever finalist is taken.
                if (rules) Assert.That(FinalistRead.BrokenEitherWay(s), Is.Empty);
                else Assert.That(FinalistRead.BrokenEitherWay(s), Is.Not.Empty);
            }
        }

        // ------------------------------------------------------------ an information deal passes one reading a week

        [Test]
        public void C1_UnderTheRulesAnInformationDealTellsThePlayerTheirVoteAsTheHouseGoesToVote()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Campaign(rules);
                var npcs = NpcIds(s);
                string hoh = npcs[0], partner = npcs[3], other = npcs[4], gone = s.nominees[0], stays = s.nominees[1];
                // The partner and the player vote the first nominee out; the last voter the second. The
                // count alone cannot then place the partner's ballot.
                SetScore(s, partner, gone, -100); SetScore(s, partner, stays, 100);
                SetScore(s, other, gone, 100); SetScore(s, other, stays, -100);
                s.deals.Add(Deal("deal-info", DealKind.InformationSharing, s.playerId, partner, expires: 0));
                s.deals.Add(Deal("deal-info-hoh", DealKind.InformationSharing, hoh, s.playerId, expires: 0));
                Valid(s);
                var engine = new EpisodeEngine(s);
                var closed = Apply(engine, EpisodeCommandKind.Advance);
                Assert.That(closed.accepted, Is.True, closed.reason);
                Assert.That(closed.state.phase, Is.EqualTo(EpisodePhase.Eviction), "Campaigning has closed.");
                var told = closed.state.ledger.claims.Where(k => k.week == s.week).ToList();
                var after = Resolve(engine);
                if (!rules)
                {
                    Assert.That(told, Is.Empty, "Without the rules an information deal never did anything.");
                    Assert.That(after.events.Any(e => e.text.Contains("kept you in the loop")), Is.False);
                    Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.False, "and the partner's ballot stayed theirs.");
                    continue;
                }
                Assert.That(told, Has.Count.EqualTo(1), "One reading, from the partner who votes: the Head of Household has no ballot to share.");
                var claim = told[0];
                Assert.That(claim.voterId, Is.EqualTo(partner));
                Assert.That(claim.source, Is.EqualTo(ClaimSource.Told), "Told to the player's face, as an answered question is.");
                Assert.That(claim.targetId, Is.EqualTo(EpisodeEngine.ProjectBallot(closed.state, partner).selectedNomineeId), "Their ballot as it stands,");
                Assert.That(claim.targetId, Is.EqualTo(gone), "honestly.");
                var line = closed.state.events.Last();
                Assert.That(line.kind, Is.EqualTo("vote-read"));
                Assert.That(line.text, Is.EqualTo(EpisodeEngine.ReadingLine(closed.state.Find(partner).name, closed.state.Find(gone).name)));
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, partner }), "Between the two of them.");
                Assert.That(VoteRead.Read(closed.state).voters.Single(v => v.voterId == partner).saysId, Is.EqualTo(gone), "The whip count reads it.");
                Assert.That(after.ledger.claims.Where(k => k.week == s.week).Select(k => k.status), Is.EqualTo(new[] { ClaimStatus.Kept }),
                    "Judged at the reveal like every other claim, and one a week.");
                Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.True, "The player knows that ballot by it.");
            }
        }

        // ------------------------------------------------------------ a partnership is judged at the vote

        [TestCase(true)]
        [TestCase(false)]
        public void C1_UnderTheRulesAPartnershipIsJudgedAtTheVoteByThePlayersOwnBallot(bool votesThemOut)
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Campaign(rules);
                string partner = s.nominees[0], other = s.nominees[1];
                s.deals.Add(Deal("deal-partners", DealKind.Partnership, s.playerId, partner, expires: 0));
                Valid(s);
                var engine = new EpisodeEngine(s);
                EpisodeEngineTests.OpenTheVote(engine);
                var open = engine.Snapshot;
                // The ballot's warning (V1) is the rules' own judgement of the player's ballot.
                Assert.That(CommitmentsRead.WouldBreak(open, CommitmentsRead.Decision.Vote(partner)).Any(b => b.id == "deal-partners"), Is.EqualTo(rules),
                    "Voting a partner out is warned of exactly when it breaks the partnership.");
                Assert.That(CommitmentsRead.WouldBreak(open, CommitmentsRead.Decision.Vote(other)).Any(b => b.id == "deal-partners"), Is.False);
                double before = open.Score(partner, open.playerId);
                var cast = engine.Apply(Command(open, EpisodeCommandKind.CastVote, votesThemOut ? partner : other));
                Assert.That(cast.accepted, Is.True, cast.reason);
                var after = Resolve(engine);
                var deal = after.deals.Single(d => d.id == "deal-partners");
                Valid(after);
                if (!rules)
                {
                    Assert.That(deal.status, Is.EqualTo(DealStatus.Active), "Without the rules a partnership never resolved.");
                    Assert.That(after.Score(partner, after.playerId), Is.EqualTo(before));
                    continue;
                }
                Assert.That(deal.status, Is.EqualTo(votesThemOut ? DealStatus.Broken : DealStatus.Fulfilled));
                Assert.That(deal.settledWeek, Is.EqualTo(s.week));
                Assert.That(deal.brokenById, Is.EqualTo(votesThemOut ? s.playerId : null), "The player's own ballot settled it.");
                double impact = (votesThemOut ? DealResolution.BrokenBase : DealResolution.FulfilledBase) * DealTrust.Weight(DealTrust.Medium);
                Assert.That(after.Score(partner, after.playerId) - before, Is.EqualTo(impact).Within(1e-9), "The partner thinks better or worse of them for it.");
                Assert.That(KnownBallots.DealOutcomeKnown(after, deal), Is.True, "The player knows their own ballot.");
                var line = after.events.Single(e => e.kind == "deal-outcome");
                Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId }), "Told to the one whose ballot settled it.");
                Assert.That(CommitmentsRead.Of(after).Single(c => c.id == "deal-partners").status, Is.EqualTo(votesThemOut ? "broken by you" : "honoured"));
            }
        }

        [Test]
        public void C1_UnderTheRulesAPartnersBallotSettlesItAndThePlayerIsToldOnlyOnceTheyKnowThatBallot()
        {
            foreach (bool informed in new[] { false, true })
            {
                var s = PlayerOnTheBlock(out string partner, out string other);
                s.deals.Add(Deal("deal-partners", DealKind.Partnership, partner, s.playerId, expires: 0));
                if (informed) s.deals.Add(Deal("deal-info", DealKind.InformationSharing, s.playerId, partner, expires: 0));
                s.alliances.Add(new AllianceState { id = "alliance-partners", name = "The Partners", members = new List<string> { s.playerId, partner }, active = true });
                Valid(s);
                var after = Evict(s);
                Valid(after);
                Assert.That(after.Find(other).status, Is.EqualTo(ContestantStatus.Jury), "The other nominee went.");
                var deal = after.deals.Single(d => d.id == "deal-partners");
                Assert.That(deal.status, Is.EqualTo(DealStatus.Fulfilled), "The partner voted the other nominee out: kept, by them.");
                Assert.That(deal.settledWeek, Is.EqualTo(s.week));
                Assert.That(after.events.Single(e => e.kind == "deal-outcome").audienceIds, Is.EqualTo(new[] { partner }),
                    "The line goes to the one whose ballot settled it, never to the player as the other party.");
                Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.EqualTo(informed), "Their ballot is the player's to know only by the information deal.");
                Assert.That(KnownBallots.DealOutcomeKnown(after, deal), Is.EqualTo(informed), "So is how the partnership ended.");
                string note = HouseguestNotes.For(after, partner).Single(n => n.kind == HouseguestNotes.Kinds.Offer && n.text.Contains("partnership")).text;
                Assert.That(note, informed ? Does.EndWith("honoured") : Does.EndWith(KnownBallots.Unresolved));
                Assert.That(KnownBallots.PlayerMemories(after).Any(m => m.text.Contains(" honoured a partnership with ")), Is.EqualTo(informed),
                    "The player's memory of it waits for the ballot too.");
                Assert.That(CommitmentsRead.Of(after).Single(c => c.id == "deal-partners").outcome,
                    Is.EqualTo(informed ? CommitmentsRead.Outcomes.Kept : CommitmentsRead.Outcomes.Unresolved));
                var week = YourWeek.Build(after, s.week).word.Single(l => l.kind == YourWeek.Kinds.Deal);
                Assert.That(week.verdict, Is.EqualTo(informed ? YourWeek.Verdicts.Kept : YourWeek.Verdicts.NotKnown), "The recap's week says so as far as the player can tell,");
                string pactLine = AllianceRead.Yours(after).Single(p => p.id == "alliance-partners").deals.Single(d => d.type == DealKind.Partnership).text;
                Assert.That(pactLine, informed ? Does.EndWith("honoured") : Does.EndWith(KnownBallots.Unresolved), "and the alliances page,");
                Assert.That(FinalistRead.RelationshipLine(after, partner) ?? "", informed ? Does.Contain("Kept a deal with you") : Does.Not.Contain("Kept a deal"), "the finalist's card,");
                Assert.That(FinalArgument.Moments(after).Any(m => m.reference == "deal:deal-partners"), Is.EqualTo(informed), "and the final argument's moments.");
            }
        }

        [Test]
        public void C1_UnderTheRulesABreachByAPartnersPrivateBallotRaisesNoReckoningThatWouldTellIt()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = PlayerOnTheBlock(out string partner, out string other, rules);
                EpisodeEngine.EnableStory(s);
                EpisodeEngine.EnableLevers(s);
                // The partner votes the player out; the other two vote the other nominee out, so the
                // player stays, and the count alone does not place the partner's ballot.
                var npcs = NpcIds(s);
                string against = npcs[2], with = npcs[4];
                SetScore(s, partner, s.playerId, -100); SetScore(s, partner, other, 100);
                SetScore(s, against, s.playerId, 100); SetScore(s, against, other, -100);
                s.deals.Add(Deal("deal-vote", DealKind.VoteSave, partner, s.playerId, target: s.playerId, expires: s.week));
                s.deals.Add(Deal("deal-partners", DealKind.Partnership, partner, s.playerId, expires: 0));
                Valid(s);
                var after = Evict(s);
                Valid(after);
                Assert.That(after.Find(other).status, Is.EqualTo(ContestantStatus.Jury), "The other nominee went.");
                Assert.That(after.votes.Single(v => v.voterId == partner).targetId, Is.EqualTo(s.playerId), "The partner voted the player out.");
                Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.False, "a ballot the player cannot place.");
                Assert.That(after.deals.Single(d => d.id == "deal-vote").status, Is.EqualTo(DealStatus.Broken), "The vote deal broke by it,");
                bool reckoning = after.story.reckonings.Any(r => r.npcId == partner);
                if (rules)
                {
                    Assert.That(after.deals.Single(d => d.id == "deal-partners").status, Is.EqualTo(DealStatus.Broken), "and so did the partnership.");
                    Assert.That(reckoning, Is.False, "No \"You Broke Your Word\" waits in their next conversation to tell the player that ballot.");
                }
                else Assert.That(reckoning, Is.True, "Without the rules the story raised it for the player, ballot and all.");
            }
        }

        // ------------------------------------------------------------ a safety pact is kept, and rewarded

        [Test]
        public void C1_UnderTheRulesASafetyPactIsKeptAndRewardedWhenTheHeadOfHouseholdSparesTheirPartner()
        {
            EpisodeState off = null;
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(29, 8);
                var npcs = NpcIds(s);
                string hoh = npcs[0];
                s.phase = EpisodePhase.Nomination;
                s.hohId = hoh;
                // The Head of Household is warm on the player and two houseguests least: those two go up.
                foreach (var other in s.contestants.Where(c => c.id != hoh)) SetScore(s, hoh, other.id, other.isPlayer ? 90 : 40);
                SetScore(s, hoh, npcs[1], -90); SetScore(s, hoh, npcs[2], -90);
                s.deals.Add(Deal("deal-pact", DealKind.SafetyAgreement, s.playerId, hoh, expires: 2));
                s.deals.Add(Deal("deal-bystanders", DealKind.SafetyAgreement, npcs[4], npcs[5], expires: 2));
                if (rules) EpisodeEngine.EnableCommitments(s);
                Valid(s);
                var after = ThroughTheVeto(s);
                Valid(after);
                Assert.That(after.vetoResolved, Is.True);
                Assert.That(after.nominees, Does.Not.Contain(after.playerId), "The Head of Household spared the player.");
                var pact = after.deals.Single(d => d.id == "deal-pact");
                Assert.That(after.deals.Single(d => d.id == "deal-bystanders").status, Is.EqualTo(DealStatus.Active), "Neither of them held the power: nothing to judge.");
                if (!rules)
                {
                    Assert.That(pact.status, Is.EqualTo(DealStatus.Active), "Without the rules a safety pact could only ever break.");
                    off = after;
                    continue;
                }
                Assert.That(pact.status, Is.EqualTo(DealStatus.Fulfilled), "Kept: the player was spared.");
                Assert.That(pact.settledWeek, Is.EqualTo(after.week));
                Assert.That(pact.brokenById, Is.Null);
                double reward = DealResolution.FulfilledBase * DealTrust.Weight(DealTrust.High);
                Assert.That(after.Score(after.playerId, hoh) - off.Score(off.playerId, hoh), Is.EqualTo(reward).Within(1e-9),
                    "And rewarded: the one spared thinks the better of them, at the pact's weight.");
                Assert.That(Entries(after, after.playerId, hoh, "deal_fulfilled").Select(e => e.impactScore), Is.EqualTo(new[] { reward }));
                var line = after.events.Single(e => e.kind == "deal-outcome");
                Assert.That(line.text, Is.EqualTo(after.Find(hoh).name + " honoured a safety pact with " + after.Find(after.playerId).name + "."));
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { hoh, after.playerId }), "Told to both: the nominations were public.");
                Assert.That(after.randomState, Is.EqualTo(off.randomState), "Drawing nothing.");
            }
        }

        // ------------------------------------------------------------ a final two deal in the final choice

        [Test]
        public void C1_UnderTheRulesAFinalTwoDealIsAnObligationTermWeighedByTheirViewAndTheirWord()
        {
            var s = FinalEviction(true, out string hoh, out string juror);
            string player = s.playerId, other = FinalistOtherThanThePlayer(s);
            var finalists = new[] { player, other };
            s.Find(hoh).traits = new List<string> { "Strategic", "Social" };
            s.deals.Add(Deal("deal-final", DealKind.FinalTwo, hoh, player, week: 3, expires: 0));
            s.deals.Add(Deal("deal-final-juror", DealKind.FinalTwo, hoh, juror, week: 3, expires: 0));
            SetScore(s, hoh, player, 50);
            var terms = EpisodeEngine.FinalTwoTerms(s, hoh, finalists);
            Assert.That(terms, Has.Count.EqualTo(1), "Only a deal with one of the two it chooses between: a juror is no longer a choice.");
            Assert.That(terms[0].nomineeId, Is.EqualTo(player));
            Assert.That(terms[0].code, Is.EqualTo("obligation"));
            Assert.That(terms[0].value, Is.EqualTo(EpisodeEngine.FinalTwoObligation), "Whole at a view of fifty.");
            Assert.That(terms[0].evidenceIds, Is.EqualTo(new[] { "deal-final" }));
            SetScore(s, hoh, player, 25);
            Assert.That(EpisodeEngine.FinalTwoTerms(s, hoh, finalists).Single().value, Is.EqualTo(EpisodeEngine.FinalTwoObligation / 2), "Half at twenty-five.");
            s.Find(hoh).traits = new List<string> { "Loyal" };
            Assert.That(EpisodeEngine.FinalTwoTerms(s, hoh, finalists).Single().value, Is.EqualTo(EpisodeEngine.FinalTwoObligation / 2 * EpisodeEngine.LoyalObligation), "A Loyal word is worth half as much again.");
            s.Find(hoh).traits = new List<string> { "Sneaky" };
            Assert.That(EpisodeEngine.FinalTwoTerms(s, hoh, finalists), Is.Empty, "A Sneaky one's nothing.");
            s.Find(hoh).traits = new List<string> { "Strategic" };
            SetScore(s, hoh, player, -5);
            Assert.That(EpisodeEngine.FinalTwoTerms(s, hoh, finalists), Is.Empty, "Nothing from a Head of Household who has turned on them.");
            var mine = FinalEviction(true, out _, out _, playerChooses: true);
            mine.deals.Add(Deal("deal-final", DealKind.FinalTwo, mine.playerId, FinalistsBesidesTheHead(mine)[0], week: 3, expires: 0));
            Assert.That(EpisodeEngine.FinalTwoTerms(mine, mine.playerId, FinalistsBesidesTheHead(mine)), Is.Empty, "The player's own choice is theirs.");
        }

        [Test]
        public void C1_UnderTheRulesAFinalTwoDealTurnsACloseFinalChoice()
        {
            // The final Head of Household's view of the other finalist, rising until, without the rules,
            // they would evict the player - their final two partner - by less than the deal weighs.
            int found = int.MinValue;
            for (int view = -100; view <= 100 && found == int.MinValue; view += 2)
            {
                var probe = Turning(false, view, out string hoh);
                var context = probe.Clone();
                context.nominees = FinalistsBesidesTheHead(probe);
                var options = WebEvictionVoting.FromNative(context, hoh);
                options.memories.Clear(); options.playerPersonaLabel = null;
                var choice = WebEvictionVoting.Evaluate(options);
                if (choice.selectedNomineeId == probe.playerId && choice.margin < EpisodeEngine.FinalTwoObligation) found = view;
            }
            Assert.That(found, Is.Not.EqualTo(int.MinValue), "Some view of the other finalist makes the choice a close one against the player.");
            foreach (bool rules in new[] { false, true })
            {
                var after = Advance(Turning(rules, found, out string hoh));
                var deal = after.deals.Single(d => d.id == "deal-final");
                if (rules)
                {
                    Assert.That(after.Find(after.playerId).status, Is.EqualTo(ContestantStatus.Active), "Under the rules the final two holds: the player is taken,");
                    Assert.That(deal.status, Is.EqualTo(DealStatus.Fulfilled), "and the deal is kept.");
                }
                else
                {
                    Assert.That(after.Find(after.playerId).status, Is.EqualTo(ContestantStatus.Jury), "Without them the deal was worth about 4.5 points: the player went,");
                    Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "and the deal broke.");
                }
            }
        }

        // ------------------------------------------------------------ accepting commits you (decision 15)

        [Test]
        public void C1_UnderTheRulesAcceptingAnOfferIsWorthFourNotTwelveOnTheSameDraws()
        {
            string id = NpcDeals.OfferPrefix + "70";
            var answered = new Dictionary<bool, EpisodeState>();
            string asker = null;
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(61, 8);
                asker = NpcIds(s)[0];
                SetScore(s, asker, s.playerId, 10); SetScore(s, s.playerId, asker, 10);
                s.deals.Add(Deal(id, DealKind.Partnership, asker, s.playerId, status: DealStatus.Proposed, expires: 1));
                if (rules) EpisodeEngine.EnableCommitments(s);
                Valid(s);
                var result = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.RespondToDeal, id, null, EpisodeEngine.AcceptDeal));
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(result.state.deals.Single(d => d.id == id).status, Is.EqualTo(DealStatus.Active));
                answered[rules] = result.state;
            }
            var off = answered[false]; var on = answered[true];
            string player = on.playerId;
            Assert.That(PlayerDeals.CommittedAcceptedImpact, Is.EqualTo(4));
            Assert.That(on.randomState, Is.EqualTo(off.randomState), "The same two draws either way.");
            Assert.That((on.Score(asker, player) - 10) * 3, Is.EqualTo(off.Score(asker, player) - 10).Within(1e-9), "Their view of the player moves a third as far,");
            double social = on.Find(player).stats.social;
            Assert.That(on.Score(player, asker) - 10, Is.EqualTo(WebRules.RelationshipDelta(PlayerDeals.CommittedAcceptedImpact, social, true)), "and the player's own: +4,");
            Assert.That(off.Score(player, asker) - 10, Is.EqualTo(WebRules.RelationshipDelta(PlayerDeals.AcceptedImpact, social, true)), "where it was +12.");
        }

        [Test]
        public void C1_UnderTheRulesAnOfferAcceptedAndThenBrokenWeighsOneStepHeavier()
        {
            string offer = NpcDeals.OfferPrefix + "5";
            foreach (var (id, rules, weight) in new[]
                     {
                         (offer, true, DealTrust.Critical),
                         ("deal-player-5", true, DealTrust.High),
                         (offer, false, DealTrust.High),
                     })
            {
                var after = NominatedByTheirSafetyPartner(rules, id, out string hoh);
                var deal = after.deals.Single(d => d.id == id);
                Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), id + ": the Head of Household put the player up.");
                Assert.That(DealResolution.BreachWeight(after, deal), Is.EqualTo(DealTrust.Weight(weight)), id + (rules ? " under" : " without") + " the rules");
                Assert.That(Entries(after, after.playerId, hoh, "deal_broken").First().impactScore, Is.EqualTo(DealResolution.BrokenBase * DealTrust.Weight(weight)),
                    id + ": the player holds it at that weight.");
            }

            // And the jury weighs it so: a partnership the player accepted and broke against one they put to them.
            foreach (bool accepted in new[] { true, false })
            {
                var s = Season(43, 8);
                EpisodeEngine.EnableCommitments(s);
                string juror = NpcIds(s)[0];
                var deal = Deal(accepted ? offer : "deal-player-5", DealKind.Partnership, accepted ? juror : s.playerId, accepted ? s.playerId : juror,
                    status: DealStatus.Broken, expires: 0);
                deal.brokenById = s.playerId; deal.settledWeek = 1;
                s.deals.Add(deal);
                Valid(s);
                Assert.That(WebJuryVoting.Obligations(s, juror, s.playerId), Is.EqualTo(-25 * DealTrust.Weight(accepted ? DealTrust.High : DealTrust.Medium)));
            }
        }

        // ------------------------------------------------------------ a vote deal both parties broke

        [Test]
        public void C1_UnderTheRulesAVoteDealBothPartiesBrokeNamesNeitherAndWrongsNeither()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Campaign(rules);
                EpisodeEngine.EnableLevers(s);
                string partner = NpcIds(s)[3], named = s.nominees[0], instead = s.nominees[1];
                // They agreed to vote the first nominee out, and each votes the second out instead.
                s.deals.Add(Deal("deal-vote", DealKind.VoteEvict, s.playerId, partner, target: named, expires: s.week));
                SetScore(s, partner, named, 100); SetScore(s, partner, instead, -100);
                Valid(s);
                var engine = new EpisodeEngine(s);
                EpisodeEngineTests.OpenTheVote(engine);
                var cast = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.CastVote, instead));
                Assert.That(cast.accepted, Is.True, cast.reason);
                var after = Resolve(engine);
                Valid(after);
                string player = after.playerId;
                var deal = after.deals.Single(d => d.id == "deal-vote");
                Assert.That(after.votes.Single(v => v.voterId == partner).targetId, Is.EqualTo(instead), "The partner broke it,");
                Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "and so did the player.");
                var mine = Entries(after, player, partner, "deal_broken");
                var theirs = Entries(after, partner, player, "deal_broken");
                var line = after.events.Single(e => e.kind == "deal-outcome");
                if (rules)
                {
                    Assert.That(deal.brokenById, Is.Null, "Both broke it: the record names neither.");
                    Assert.That(deal.settledWeek, Is.EqualTo(after.week));
                    Assert.That(Breaches.BrokenByBoth(deal), Is.True);
                    Assert.That(Breaches.Broke(after, deal, player) && Breaches.Broke(after, deal, partner), Is.True, "Each broke it,");
                    Assert.That(NpcDeals.BrokenDeals(after, player), Is.EqualTo(1));
                    Assert.That(NpcDeals.BrokenDeals(after, partner), Is.EqualTo(1), "and each is held to it.");
                    Assert.That(mine.Select(e => e.impactScore), Is.EqualTo(theirs.Select(e => e.impactScore)),
                        "Neither is held as the one wronged: each holds it against the other alike, as a bloc's partners do.");
                    Assert.That(line.text, Is.EqualTo(after.Find(player).name + " and " + after.Find(partner).name + " fell out over their vote to evict."));
                    Assert.That(line.audienceIds, Is.EqualTo(new[] { partner }), "The line tells the partner's ballot, so it is not the player's until they know it.");
                    Assert.That(after.relationships.SelectMany(r => r.events).Any(e => e.type == "heard_about_betrayal"), Is.False,
                        "Nobody in particular broke it, so there is no betrayal for the house to hear of.");
                }
                else
                {
                    Assert.That(line.text, Is.EqualTo(after.Find(player).name + " broke a vote to evict with " + after.Find(partner).name + "."),
                        "Without the rules the first of the two was named,");
                    Assert.That(after.Score(partner, player), Is.LessThan(s.Score(partner, player)), "and the second held it against them as the one wronged.");
                }
            }
        }

        [Test]
        public void C1_AVoteDealBothBrokeIsHeldAgainstEachInEveryBallot()
        {
            var s = Campaign(true);
            var npcs = NpcIds(s);
            string a = npcs[3], b = npcs[4];
            var both = Deal("deal-both", DealKind.VoteSave, a, b, target: s.nominees[0], status: DealStatus.Broken, expires: s.week);
            both.settledWeek = s.week;
            s.deals.Add(both);
            Valid(s);
            var seen = WebEvictionVoting.FromNative(s, s.playerId).state.deals.Single(d => d.id == "deal-both");
            Assert.That(seen.brokenByBoth, Is.True, "Both broke it,");
            Assert.That(seen.brokenById, Is.Null, "so it names neither,");
            Assert.That(Breaches.CountsAgainst(s, both, a) && Breaches.CountsAgainst(s, both, b), Is.True, "and it counts against each.");
            Assert.That(NpcDeals.BrokenDeals(s, a) + NpcDeals.BrokenDeals(s, b), Is.EqualTo(2));
            var withoutTheRules = Campaign(false);
            withoutTheRules.deals.Add(Deal("deal-both", DealKind.VoteSave, a, b, target: withoutTheRules.nominees[0], status: DealStatus.Broken, expires: s.week));
            Assert.That(WebEvictionVoting.FromNative(withoutTheRules, withoutTheRules.playerId).state.deals.Single(d => d.id == "deal-both").brokenByBoth, Is.False,
                "Without the rules the web's own rule holds a breach against both sides anyway.");
        }

        // ------------------------------------------------------------ fixtures

        private static EpisodeState Season(uint seed, int size = 8) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<string> NpcIds(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static DealState Deal(string id, string type, string from, string to, string target = null, string status = DealStatus.Active,
            int week = 1, int expires = 1) =>
            new DealState
            {
                id = id, type = type, proposerId = from, recipientId = to, targetId = target, status = status,
                week = week, expiresWeek = expires, trustImpact = DealKind.DefaultTrust(type),
            };

        private static PromiseState Promise(string id, string from, string to, PromiseKind kind, int week = 1, int expires = 0) =>
            new PromiseState { id = id, fromId = from, toId = to, kind = kind, status = PromiseStatus.Active, week = week, expiresWeek = expires };

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "c1-" + kind + "-" + s.revision, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target));

        private static List<RelationshipEventState> Entries(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type).ToList();

        /// <summary>One plain step from here.</summary>
        private static EpisodeState Advance(EpisodeState s)
        {
            var engine = new EpisodeEngine(s);
            var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        /// <summary>Plain steps until the eviction is resolved.</summary>
        private static EpisodeState Resolve(EpisodeEngine engine)
        {
            for (int i = 0; i < 40 && !engine.Snapshot.evictionResolved; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.evictionResolved, Is.True, "The eviction resolved.");
            return engine.Snapshot;
        }

        private static EpisodeState Evict(EpisodeState s) => Resolve(new EpisodeEngine(s));

        /// <summary>Plain steps from the nominations until the veto meeting is over.</summary>
        private static EpisodeState ThroughTheVeto(EpisodeState s)
        {
            var engine = new EpisodeEngine(s);
            for (int i = 0; i < 40 && !engine.Snapshot.vetoResolved; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            return engine.Snapshot;
        }

        /// <summary>A campaign with the player voting, a houseguest at the head of the house and two on the block.</summary>
        private static EpisodeState Campaign(bool rules)
        {
            var s = ContentCatalog.Create(7);
            s.strategyRulesStartWeek = 1;
            var npcs = NpcIds(s);
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// A campaign with the player on the block: their partner votes the other nominee out, one
        /// houseguest votes the player out and the last the other nominee, so the count alone places
        /// no ballot for the player, who casts none.
        /// </summary>
        private static EpisodeState PlayerOnTheBlock(out string partner, out string other, bool rules = true)
        {
            var s = ContentCatalog.Create(7);
            s.strategyRulesStartWeek = 1;
            var npcs = NpcIds(s);
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            other = npcs[1];
            s.nominees = new List<string> { s.playerId, other };
            s.vetoHolderId = npcs[2];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            partner = npcs[3];
            string against = npcs[2], with = npcs[4];
            SetScore(s, partner, s.playerId, 100); SetScore(s, partner, other, -100);
            SetScore(s, against, s.playerId, -100); SetScore(s, against, other, 100);
            SetScore(s, with, s.playerId, 100); SetScore(s, with, other, -100);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The final eviction: the player and two houseguests in the house, three on the jury. A
        /// houseguest is the final Head of Household, or the player when <paramref name="playerChooses"/>.
        /// </summary>
        private static EpisodeState FinalEviction(bool rules, out string hoh, out string juror, bool playerChooses = false)
        {
            var s = ContentCatalog.Create(337);
            s.week = 4;
            s.phase = EpisodePhase.FinalEviction;
            foreach (var actor in s.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            string head = playerChooses ? s.playerId : s.contestants[1].id;
            hoh = head;
            s.hohId = head;
            s.finalPart1WinnerId = head;
            s.finalPart2WinnerId = s.contestants.Take(3).First(c => c.id != head).id;
            juror = s.contestants[3].id;
            if (rules) EpisodeEngine.EnableCommitments(s);
            return s;
        }

        private static string FinalistOtherThanThePlayer(EpisodeState s) => s.Active.Single(c => !c.isPlayer && c.id != s.hohId).id;

        private static List<string> FinalistsBesidesTheHead(EpisodeState s) => s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();

        /// <summary>
        /// A houseguest final Head of Household with a final two deal with the player, warm on them, in
        /// an alliance with the other finalist, and on that finalist at <paramref name="viewOfOther"/>.
        /// </summary>
        private static EpisodeState Turning(bool rules, int viewOfOther, out string hoh)
        {
            var s = FinalEviction(rules, out hoh, out _);
            string other = FinalistOtherThanThePlayer(s);
            s.Find(hoh).traits = new List<string> { "Strategic", "Social" };
            s.deals.Add(Deal("deal-final", DealKind.FinalTwo, hoh, s.playerId, week: 3, expires: 0));
            s.alliances.Add(new AllianceState { id = "alliance-final", name = "The Final", members = new List<string> { hoh, other }, active = true });
            SetScore(s, hoh, s.playerId, 50);
            SetScore(s, hoh, other, viewOfOther);
            Valid(s);
            return s;
        }

        /// <summary>
        /// A Head of Household with a safety pact with the player - <paramref name="id"/> an offer they put
        /// to the player and the player accepted, or one the player put to them - who likes everybody else
        /// and puts the player up: the pact is broken by them.
        /// </summary>
        private static EpisodeState NominatedByTheirSafetyPartner(bool rules, string id, out string hoh)
        {
            var s = Season(29, 8);
            var npcs = NpcIds(s);
            string head = npcs[0];
            hoh = head;
            s.phase = EpisodePhase.Nomination;
            s.hohId = head;
            foreach (var other in s.contestants.Where(c => c.id != head)) SetScore(s, head, other.id, other.isPlayer ? -90 : 60);
            bool offer = id.StartsWith(NpcDeals.OfferPrefix, System.StringComparison.Ordinal);
            s.deals.Add(Deal(id, DealKind.SafetyAgreement, offer ? head : s.playerId, offer ? s.playerId : head, expires: 2));
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            var engine = new EpisodeEngine(s);
            var advanced = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
            Assert.That(advanced.accepted, Is.True, advanced.reason);
            Assert.That(advanced.state.nominees, Does.Contain(advanced.state.playerId));
            return advanced.state;
        }
    }
}
