using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// ACTIONS-DEALS-ALLIANCES-PLAN C1, every deal does something and accepting commits you: an
    /// information deal passes one reading a week, on its partner's word; a partnership is judged at
    /// every vote that tests it and stands while it is kept; a safety pact is kept and rewarded when the
    /// Head of Household spares their partner; a final two deal is an obligation in the final Head of
    /// Household's choice; deals and final two promises end with an evictee (X4); a vote deal both
    /// parties broke names neither; a settlement a ballot decided never moves the player's own view;
    /// and accepting an offer is +4 and weighs one step heavier broken (decision 15). Each is shown as a
    /// season without the commitment rules plays it - as every recorded season does - and as it plays
    /// under them. X4's fixture first. Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
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
        public void X4_UnderTheRulesDealsAndFinalTwoPromisesEndWithAnEvicteeAsTheHouseTurnsToTheWeek()
        {
            string offer = NpcDeals.OfferPrefix + "90";
            var endsWithThem = new[] { "deal-final-two", "deal-pact", "deal-about", "deal-invite", offer };
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
                // The pact the player and the evictee formed by an invitation, agreed and open-ended.
                s.deals.Add(Deal("deal-invite", DealKind.AllianceInvite, s.playerId, gone, expires: 0));
                s.alliances.Add(new AllianceState { id = "alliance-pair", name = "The Pair", members = new List<string> { s.playerId, gone }, active = true });
                s.promises.Add(Promise("promise-to", s.playerId, gone, PromiseKind.FinalTwo));
                s.promises.Add(Promise("promise-from", gone, last, PromiseKind.FinalTwo));
                s.promises.Add(Promise("promise-safety", last, gone, PromiseKind.Safety, expires: 2));
                Valid(s);
                var engine = new EpisodeEngine(s);
                var revealed = Resolve(engine);
                Assert.That(revealed.Find(gone).status, Is.EqualTo(ContestantStatus.Jury), "The house voted them out.");
                // As they walk out the deal still binds: the goodbye at the door reads it
                // (EpisodeDirector.GoodbyeTone, CeremonyTruthTests).
                Assert.That(revealed.deals.Any(d => DealStatus.Binds(d.status) && d.status != DealStatus.Proposed
                    && ((d.proposerId == gone && d.recipientId == s.playerId) || (d.proposerId == s.playerId && d.recipientId == gone))), Is.True,
                    "The walk out reads a deal between them.");
                var turned = ToTheWeek(engine);
                Valid(turned);
                if (rules)
                {
                    foreach (var id in endsWithThem)
                    {
                        var deal = turned.deals.Single(d => d.id == id);
                        Assert.That(deal.status, Is.EqualTo(DealStatus.Expired), id + " ends with the one it binds or names, as the week turns.");
                        Assert.That(deal.settledWeek == 0 && deal.brokenById == null, Is.True, id + ": an ending, not a verdict.");
                    }
                    Assert.That(turned.promises.Single(p => p.id == "promise-to").status, Is.EqualTo(PromiseStatus.Expired), "A final two promise made to them ends,");
                    Assert.That(turned.promises.Single(p => p.id == "promise-from").status, Is.EqualTo(PromiseStatus.Expired), "and one they made.");
                    Assert.That(turned.promises.Single(p => p.id == "promise-safety").status, Is.EqualTo(PromiseStatus.Active), "Other promises are as they were.");
                    Assert.That(turned.relationships.SelectMany(r => r.events).Any(e => e.type == "deal_broken" || e.type == "promise-broken"), Is.False,
                        "Nothing is held against anybody for it.");
                    Assert.That(CommitmentsRead.Of(turned).Single(c => c.id == "deal-final-two").term, Is.EqualTo("until they left in week " + s.week),
                        "Your word says when it ended, as an oath's term does.");
                }
                else
                {
                    Assert.That(turned.deals.Single(d => d.id == "deal-final-two").status, Is.EqualTo(DealStatus.Active), "Without the rules a final two with a juror stood,");
                    Assert.That(turned.deals.Single(d => d.id == offer).status, Is.EqualTo(DealStatus.Proposed), "an offer from them waited,");
                    Assert.That(turned.promises.Single(p => p.id == "promise-to").status, Is.EqualTo(PromiseStatus.Active), "and so did a promise, for the final eviction to break.");
                    Assert.That(CommitmentsRead.Of(turned).Single(c => c.id == "deal-final-two").term, Is.EqualTo("never expires"));
                }
                // How the pact began is still the invitation, ended or not.
                Assert.That(AllianceRead.Yours(turned).Single(p => p.id == "alliance-pair").formed,
                    Is.EqualTo("You invited " + turned.Find(gone).name.Split(' ')[0] + "."));
                // The player took the final two: ended or not, it is on their record as taken, not as an offer they let lapse.
                Assert.That(turned.ledger.opportunities.Single(o => o.id == "deal-final-two").response, Is.EqualTo(OpportunityResponse.Taken));
                EpisodeEngine.ReconcileOpportunities(turned);
                Assert.That(turned.ledger.opportunities.Single(o => o.id == "deal-final-two").response, Is.EqualTo(OpportunityResponse.Taken), "and stays so at the next reconcile.");
                Assert.That(GameSense.Evaluate(turned).notes.Where(n => n.rowId == "deal-final-two").Select(n => n.points), Is.EqualTo(new[] { 1.0 }), "A deal made, not an offer left on the table.");
            }
        }

        [Test]
        public void X4_TheEndingWritesNothingLogsNothingAndDrawsNothing()
        {
            var s = Campaign(true);
            var npcs = NpcIds(s);
            string gone = s.nominees[0], last = npcs[4];
            s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, s.playerId, gone, expires: 0));
            s.deals.Add(Deal("deal-about", DealKind.TargetAgreement, npcs[3], last, target: gone, expires: 1));
            s.deals.Add(Deal("deal-elsewhere", DealKind.Partnership, npcs[3], last, expires: 0));
            s.promises.Add(Promise("promise-to", s.playerId, gone, PromiseKind.FinalTwo));
            s.promises.Add(Promise("promise-safety", last, gone, PromiseKind.Safety, expires: 2));
            s.Find(gone).status = ContestantStatus.Jury;
            var before = s.Clone();
            typeof(EpisodeEngine).GetMethod("EndWithTheEvictee", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { s, gone });
            Assert.That(s.deals.Single(d => d.id == "deal-final-two").status, Is.EqualTo(DealStatus.Expired));
            Assert.That(s.deals.Single(d => d.id == "deal-about").status, Is.EqualTo(DealStatus.Expired));
            Assert.That(s.deals.Single(d => d.id == "deal-elsewhere").status, Is.EqualTo(DealStatus.Active), "A deal between two others is theirs.");
            Assert.That(s.promises.Single(p => p.id == "promise-to").status, Is.EqualTo(PromiseStatus.Expired));
            Assert.That(s.promises.Single(p => p.id == "promise-safety").status, Is.EqualTo(PromiseStatus.Active));
            Assert.That(s.randomState, Is.EqualTo(before.randomState), "Nothing drawn,");
            Assert.That(s.nextSequence, Is.EqualTo(before.nextSequence), "nothing minted,");
            Assert.That(Json(s.events), Is.EqualTo(Json(before.events)), "nothing logged,");
            Assert.That(Json(s.relationships), Is.EqualTo(Json(before.relationships)), "nothing on anybody's record,");
            Assert.That(Json(s.memories), Is.EqualTo(Json(before.memories)), "nothing remembered,");
            Assert.That(Json(s.ledger), Is.EqualTo(Json(before.ledger)), "nothing on the season's ledger.");
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
                // A Loyal partner, whose word is always the truth. The partner and the player vote the
                // first nominee out; the last voter the second. The count alone cannot then place the
                // partner's ballot.
                s.Find(partner).traits = new List<string> { "Loyal" };
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
                Assert.That(claim.targetId, Is.EqualTo(gone), "and a Loyal partner says it truly.");
                var line = closed.state.events.Last();
                Assert.That(line.kind, Is.EqualTo("vote-read"));
                Assert.That(line.text, Is.EqualTo(EpisodeEngine.ReadingLine(closed.state.Find(partner).name, closed.state.Find(gone).name)));
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, partner }), "Between the two of them.");
                Assert.That(VoteRead.Read(closed.state).voters.Single(v => v.voterId == partner).saysId, Is.EqualTo(gone), "The whip count reads it.");
                Assert.That(after.ledger.claims.Where(k => k.week == s.week).Select(k => k.status), Is.EqualTo(new[] { ClaimStatus.Kept }),
                    "Judged at the reveal like every other claim, and one a week.");
                Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.True, "The player knows that ballot by it.");
                Assert.That(after.deals.Single(d => d.id == "deal-info").status, Is.EqualTo(DealStatus.Active), "A true word keeps the deal standing.");
            }
        }

        [Test]
        public void C1_UnderTheRulesAnInformationPartnersWordIsAsGoodAsItIsToAnybodyWhoAsks()
        {
            uint seed = SeasonWithAMiddlingReadingCoin(out double coin);
            foreach (var (traits, view, honest) in new[]
                     {
                         (new[] { "Loyal" }, -100.0, true),
                         (new[] { "Social" }, 100.0, true),
                         (new[] { "Social" }, -100.0, false),
                         (new[] { "Sneaky" }, 100.0, false),
                     })
            {
                string label = string.Join("/", traits) + " at " + view + " (coin " + coin.ToString("0.00") + ")";
                var s = Campaign(true, seed);
                var npcs = NpcIds(s);
                string partner = npcs[3], gone = s.nominees[0], stays = s.nominees[1];
                s.Find(partner).traits = new List<string>(traits);
                SetScore(s, partner, gone, -100); SetScore(s, partner, stays, 100);
                SetScore(s, partner, s.playerId, view);
                s.deals.Add(Deal("deal-info", DealKind.InformationSharing, s.playerId, partner, expires: 0));
                Valid(s);
                Assert.That(EpisodeEngine.VoteHonesty(s.Find(partner), view) > coin, Is.EqualTo(honest), label + ": the fixture's own sum.");
                uint stream = s.randomState;
                var engine = new EpisodeEngine(s);
                var closed = Apply(engine, EpisodeCommandKind.Advance);
                Assert.That(closed.accepted, Is.True, closed.reason);
                Assert.That(closed.state.randomState, Is.EqualTo(stream), label + ": drawn on a keyed coin, the season's stream untouched.");
                Assert.That(EpisodeEngine.ProjectBallot(closed.state, partner).selectedNomineeId, Is.EqualTo(gone), label + ": the ballot they mean to cast.");
                var claim = closed.state.ledger.claims.Single(k => k.week == s.week && k.voterId == partner);
                Assert.That(claim.targetId, Is.EqualTo(honest ? gone : stays), label + (honest ? ": the truth." : ": a lie names the nominee they are not voting out."));
                Assert.That(closed.state.events.Last().text, Does.EndWith(closed.state.Find(claim.targetId).name + "."), label + ": as the line says it.");
            }
        }

        [Test]
        public void C1_UnderTheRulesAnInformationPartnerCaughtLyingBreaksTheDeal()
        {
            uint seed = SeasonWithAMiddlingReadingCoin(out _);
            foreach (bool rules in new[] { false, true })
            {
                var s = Campaign(rules, seed);
                var npcs = NpcIds(s);
                string partner = npcs[3], gone = s.nominees[0], stays = s.nominees[1];
                // Cold on the player, so their word is worth a fifth: on this season's coin, a lie.
                s.Find(partner).traits = new List<string> { "Social" };
                SetScore(s, partner, gone, -100); SetScore(s, partner, stays, 100);
                SetScore(s, partner, s.playerId, -100);
                s.deals.Add(Deal("deal-info", DealKind.InformationSharing, s.playerId, partner, expires: 0));
                Valid(s);
                double before = s.Score(s.playerId, partner);
                var after = Evict(s);
                Valid(after);
                var deal = after.deals.Single(d => d.id == "deal-info");
                if (!rules)
                {
                    Assert.That(deal.status, Is.EqualTo(DealStatus.Active), "Without the rules an information deal was never judged.");
                    Assert.That(after.ledger.claims, Is.Empty);
                    continue;
                }
                var claim = after.ledger.claims.Single(k => k.week == s.week && k.voterId == partner);
                Assert.That(claim.targetId, Is.EqualTo(stays), "They told the player the other nominee,");
                Assert.That(claim.status, Is.EqualTo(ClaimStatus.Lied), "and the reveal judged it a lie,");
                Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "which broke the deal:");
                Assert.That(deal.brokenById, Is.EqualTo(partner), "by them,");
                Assert.That(deal.settledWeek, Is.EqualTo(s.week), "at that reveal.");
                var line = after.events.Single(e => e.kind == "deal-outcome");
                Assert.That(line.text, Is.EqualTo(after.Find(partner).name + " broke a information sharing with " + after.Find(after.playerId).name + "."));
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { partner, after.playerId }), "Told to both: the judged lie has already told the ballot.");
                Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.True);
                Assert.That(after.Score(after.playerId, partner), Is.EqualTo(System.Math.Max(-100, before + EpisodeEngine.VoteLieCost + DealResolution.BrokenBase * DealTrust.Weight(DealTrust.Low))),
                    "The lie costs them with the player, and so does the deal they broke.");
                Assert.That(CommitmentsRead.Of(after).Single(c => c.id == "deal-info").status, Is.EqualTo("broken by them"));
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
                var line = after.events.Single(e => e.kind == "deal-outcome");
                Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId }), "Told to the one whose ballot decided it.");
                Assert.That(KnownBallots.DealOutcomeKnown(after, deal), Is.True, "The player knows their own ballot.");
                if (votesThemOut)
                {
                    Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "Voting a partner out breaks it,");
                    Assert.That(deal.settledWeek, Is.EqualTo(s.week));
                    Assert.That(deal.brokenById, Is.EqualTo(s.playerId), "by the player's own ballot.");
                    Assert.That(after.Score(partner, after.playerId) - before, Is.EqualTo(DealResolution.BrokenBase * DealTrust.Weight(DealTrust.Medium)).Within(1e-9),
                        "The partner thinks the worse of them for it.");
                    Assert.That(CommitmentsRead.Of(after).Single(c => c.id == "deal-partners").status, Is.EqualTo("broken by you"));
                }
                else
                {
                    Assert.That(deal.status, Is.EqualTo(DealStatus.Active), "Keeping a partner keeps the partnership standing,");
                    Assert.That(deal.settledWeek == 0 && deal.brokenById == null, Is.True, "with nothing settled,");
                    var kept = Entries(after, after.playerId, partner, "deal_fulfilled").Single();
                    Assert.That(kept.impactScore, Is.EqualTo(EpisodeEngine.PartnershipKept), "and a small kept record,");
                    Assert.That(kept.description, Is.EqualTo(line.text));
                    Assert.That(after.Score(partner, after.playerId), Is.EqualTo(before), "which moves no view.");
                    Assert.That(CommitmentsRead.Of(after).Single(c => c.id == "deal-partners").status, Is.EqualTo("agreed"));
                    var week = YourWeek.Build(after, s.week).word.Single(l => l.kind == YourWeek.Kinds.Deal);
                    Assert.That((week.verdict, week.byId), Is.EqualTo((YourWeek.Verdicts.Kept, after.playerId)), "The recap's week says the player kept it.");
                }
            }
        }

        [Test]
        public void C1_UnderTheRulesAKeptPartnershipStandsAndEveryVoteThatTestsItJudgesItAgain()
        {
            var s = Campaign(true);
            string partner = s.nominees[0], other = s.nominees[1];
            // The rest of the house votes the other nominee out too, so the partner stays.
            foreach (var voter in EpisodeEngine.Voters(s).Where(v => !v.isPlayer)) { SetScore(s, voter.id, other, -100); SetScore(s, voter.id, partner, 100); }
            s.deals.Add(Deal("deal-partners", DealKind.Partnership, s.playerId, partner, expires: 0));
            Valid(s);
            var engine = new EpisodeEngine(s);
            EpisodeEngineTests.OpenTheVote(engine);
            Assert.That(engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.CastVote, other)).accepted, Is.True);
            var after = Resolve(engine);
            Assert.That(after.Find(partner).status, Is.EqualTo(ContestantStatus.Active), "The partner stayed.");
            var deal = after.deals.Single(d => d.id == "deal-partners");
            Assert.That(deal.status, Is.EqualTo(DealStatus.Active), "Kept, it stands.");
            double reluctance = StrategyRules.NominationReluctance(after, partner, after.playerId);
            var without = after.Clone(); without.deals.RemoveAll(d => d.id == "deal-partners");
            Assert.That(reluctance - StrategyRules.NominationReluctance(without, partner, after.playerId), Is.EqualTo(StrategyRules.DealWeight(DealKind.Partnership)),
                "A standing partnership still holds a Head of Household back from nominating their partner.");

            // The next vote that tests it judges it again: kept, it stands; broken, it ends.
            var next = after.Clone();
            string newcomer = NpcIds(next).First(id => id != partner);
            next.nominees = new List<string> { partner, newcomer };
            next.votes = new List<VoteState> { new VoteState { voterId = next.playerId, targetId = newcomer } };
            var keep = DealResolution.Verdicts(next, DealResolution.Votes, null).Single(v => v.deal.id == "deal-partners");
            Assert.That((keep.status, keep.stands, keep.actorId), Is.EqualTo((DealStatus.Fulfilled, true, next.playerId)));
            next.votes[0].targetId = partner;
            var breach = DealResolution.Verdicts(next, DealResolution.Votes, null).Single(v => v.deal.id == "deal-partners");
            Assert.That((breach.status, breach.stands, breach.actorId), Is.EqualTo((DealStatus.Broken, false, next.playerId)));
        }

        [Test]
        public void C1_UnderTheRulesAPartnersBallotKeepsItAndThePlayerIsToldOnlyOnceTheyKnowThatBallot()
        {
            foreach (bool informed in new[] { false, true })
            {
                var s = PlayerOnTheBlock(out string partner, out string other);
                s.Find(partner).traits = new List<string> { "Loyal" };
                s.deals.Add(Deal("deal-partners", DealKind.Partnership, partner, s.playerId, expires: 0));
                if (informed) s.deals.Add(Deal("deal-info", DealKind.InformationSharing, s.playerId, partner, expires: 0));
                s.alliances.Add(new AllianceState { id = "alliance-partners", name = "The Partners", members = new List<string> { s.playerId, partner }, active = true });
                Valid(s);
                double mine = s.Score(s.playerId, partner);
                var after = Evict(s);
                Valid(after);
                Assert.That(after.Find(other).status, Is.EqualTo(ContestantStatus.Jury), "The partner voted the other nominee out.");
                var deal = after.deals.Single(d => d.id == "deal-partners");
                Assert.That(deal.status, Is.EqualTo(DealStatus.Active), "Kept by them, it stands.");
                var line = after.events.Single(e => e.kind == "deal-outcome");
                Assert.That(line.text, Is.EqualTo(after.Find(partner).name + " honoured a partnership with " + after.Find(after.playerId).name + "."));
                Assert.That(line.audienceIds, Is.EqualTo(new[] { partner }), "The line goes to the one whose ballot kept it.");
                Assert.That(Entries(after, after.playerId, partner, "deal_fulfilled").Single().impactScore, Is.EqualTo(EpisodeEngine.PartnershipKept),
                    "The keep is on the player's record,");
                Assert.That(after.Score(after.playerId, partner), Is.EqualTo(mine), "and moves no view of theirs.");
                Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.EqualTo(informed), "Their ballot is the player's to know only by the information deal.");
                Assert.That(KnownBallots.TellsAnUnknownBallot(after, partner, line.text, s.week), Is.EqualTo(!informed), "The record waits for it,");
                Assert.That(KnownBallots.PlayerMemories(after).Any(m => m.text == line.text), Is.EqualTo(informed), "and so does the memory,");
                var week = YourWeek.Build(after, s.week).word.Single(l => l.kind == YourWeek.Kinds.Deal);
                Assert.That(week.verdict, Is.EqualTo(informed ? YourWeek.Verdicts.Kept : YourWeek.Verdicts.NotKnown), "and the recap's week.");
                Assert.That(HouseguestNotes.For(after, partner).Single(n => n.kind == HouseguestNotes.Kinds.Offer && n.text.Contains("partnership")).text, Does.EndWith("agreed"),
                    "It stands, and the notes say nothing a ballot did.");
                Assert.That(AllianceRead.Yours(after).Single(p => p.id == "alliance-partners").deals.Single(d => d.type == DealKind.Partnership).text, Does.EndWith("agreed"));
                Assert.That(FinalistRead.RelationshipLine(after, partner) ?? "", Does.Not.Contain("Kept a deal"));
            }
        }

        [Test]
        public void C1_UnderTheRulesAPartnersBallotBreaksItAndThePlayerIsToldOnlyOnceTheyKnowThatBallot()
        {
            foreach (bool informed in new[] { false, true })
            {
                var s = PlayerOnTheBlock(out string partner, out string other);
                var npcs = NpcIds(s);
                string against = npcs[2];
                // The partner votes the player out; the other two vote the other nominee out, so the
                // player stays, and the count alone does not place the partner's ballot.
                s.Find(partner).traits = new List<string> { "Loyal" };
                SetScore(s, partner, s.playerId, -100); SetScore(s, partner, other, 100);
                SetScore(s, against, s.playerId, 100); SetScore(s, against, other, -100);
                s.deals.Add(Deal("deal-partners", DealKind.Partnership, partner, s.playerId, expires: 0));
                if (informed) s.deals.Add(Deal("deal-info", DealKind.InformationSharing, s.playerId, partner, expires: 0));
                s.alliances.Add(new AllianceState { id = "alliance-partners", name = "The Partners", members = new List<string> { s.playerId, partner }, active = true });
                Valid(s);
                double mine = s.Score(s.playerId, partner);
                var after = Evict(s);
                Valid(after);
                Assert.That(after.Find(other).status, Is.EqualTo(ContestantStatus.Jury), "The other nominee went.");
                Assert.That(after.votes.Single(v => v.voterId == partner).targetId, Is.EqualTo(after.playerId), "The partner voted the player out.");
                var deal = after.deals.Single(d => d.id == "deal-partners");
                Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "Their ballot broke it,");
                Assert.That(deal.brokenById, Is.EqualTo(partner), "on the record,");
                Assert.That(after.events.Single(e => e.kind == "deal-outcome").audienceIds, Is.EqualTo(new[] { partner }), "in a line to them alone.");
                Assert.That(after.Score(after.playerId, partner), Is.EqualTo(mine),
                    "The player's own view does not move: a jump the size of the deal would tell them the ballot.");
                Assert.That(Entries(after, after.playerId, partner, "deal_broken").Single().impactScore, Is.EqualTo(DealResolution.BrokenBase * DealTrust.Weight(DealTrust.Medium)),
                    "The record still holds it, for its readers to tell when the player can know.");
                Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.EqualTo(informed));
                Assert.That(KnownBallots.DealOutcomeKnown(after, deal), Is.EqualTo(informed));
                string first = after.Find(partner).name.Split(' ')[0];
                Assert.That(HouseguestNotes.For(after, partner).Single(n => n.kind == HouseguestNotes.Kinds.Offer && n.text.Contains("partnership")).text,
                    informed ? Does.EndWith("broken by " + first) : Does.EndWith(KnownBallots.Unresolved), "The notes,");
                var page = CommitmentsRead.Of(after).Single(c => c.id == "deal-partners");
                Assert.That(page.outcome, Is.EqualTo(informed ? CommitmentsRead.Outcomes.Broken : CommitmentsRead.Outcomes.Unresolved), "Your word,");
                Assert.That(AllianceRead.Yours(after).Single(p => p.id == "alliance-partners").deals.Single(d => d.type == DealKind.Partnership).text,
                    informed ? Does.EndWith("broken") : Does.EndWith(KnownBallots.Unresolved), "the alliances page,");
                Assert.That(KnownBallots.PlayerMemories(after).Any(m => m.text.Contains(" broke a partnership with ")), Is.EqualTo(informed), "the memories,");
                Assert.That(YourWeek.Build(after, s.week).word.Single(l => l.kind == YourWeek.Kinds.Deal).verdict,
                    Is.EqualTo(informed ? YourWeek.Verdicts.Broken : YourWeek.Verdicts.NotKnown), "and the recap's week wait for the ballot.");
            }
        }

        [Test]
        public void C1_UnderTheRulesAHeadOfHouseholdsTieBreakJudgesAPartnershipInTheOpen()
        {
            var s = Season(31, 5);
            var npcs = NpcIds(s);
            string partner = npcs[0], other = npcs[1], against = npcs[2], with = npcs[3];
            s.phase = EpisodePhase.Campaign;
            s.hohId = partner;
            s.nominees = new List<string> { s.playerId, other };
            s.vetoHolderId = against;
            s.vetoPlayers = s.Active.Select(c => c.id).ToList();
            s.vetoResolved = true;
            SetScore(s, partner, s.playerId, 100); SetScore(s, partner, other, -100);
            SetScore(s, against, s.playerId, -100); SetScore(s, against, other, 100);
            SetScore(s, with, s.playerId, 100); SetScore(s, with, other, -100);
            s.deals.Add(Deal("deal-partners", DealKind.Partnership, partner, s.playerId, expires: 0));
            EpisodeEngine.EnableCommitments(s);
            Valid(s);
            var after = Evict(s);
            Valid(after);
            Assert.That(after.votes.Single(v => v.voterId == partner).targetId, Is.EqualTo(other), "The house tied, and the Head of Household broke it for the player.");
            Assert.That(after.Find(other).status, Is.EqualTo(ContestantStatus.Jury));
            Assert.That(after.deals.Single(d => d.id == "deal-partners").status, Is.EqualTo(DealStatus.Active), "Kept by the tie-break, it stands,");
            var line = after.events.Single(e => e.kind == "deal-outcome");
            Assert.That(line.audienceIds, Is.EqualTo(new[] { partner }));
            Assert.That(KnownBallots.Read(after, s.week).Knows(partner), Is.True, "and a tie-break is read in the open,");
            Assert.That(KnownBallots.TellsAnUnknownBallot(after, partner, line.text, s.week), Is.False, "so the record tells the player at once,");
            Assert.That(KnownBallots.PlayerMemories(after).Any(m => m.text == line.text), Is.True, "and the memory,");
            Assert.That(YourWeek.Build(after, s.week).word.Single(l => l.kind == YourWeek.Kinds.Deal).verdict, Is.EqualTo(YourWeek.Verdicts.Kept), "and the recap's week.");
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
                string against = npcs[2];
                SetScore(s, partner, s.playerId, -100); SetScore(s, partner, other, 100);
                SetScore(s, against, s.playerId, 100); SetScore(s, against, other, -100);
                s.deals.Add(Deal("deal-vote", DealKind.VoteSave, partner, s.playerId, target: s.playerId, expires: s.week));
                s.deals.Add(Deal("deal-partners", DealKind.Partnership, partner, s.playerId, expires: 0));
                Valid(s);
                double mine = s.Score(s.playerId, partner);
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
                    Assert.That(reckoning, Is.False, "No \"You Broke Your Word\" waits in their next conversation to tell the player that ballot,");
                    Assert.That(after.Score(after.playerId, partner), Is.EqualTo(mine), "and the player's own view tells nothing either.");
                }
                else
                {
                    Assert.That(reckoning, Is.True, "Without the rules the story raised it for the player, ballot and all,");
                    Assert.That(after.Score(after.playerId, partner), Is.LessThan(mine), "and the player's view of them fell by the deal's weight.");
                }
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

        [Test]
        public void C1_UnderTheRulesASafetyPactIsNotKeptForAPartnerPutUpThatWeekAndSavedByTheVeto()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(29, 8);
                var npcs = NpcIds(s);
                string hoh = npcs[0];
                // The ceremony put the player up; a pact with the Head of Household struck after it; the
                // player then holds the veto and saves themselves.
                s.phase = EpisodePhase.VetoMeeting;
                s.hohId = hoh;
                s.nominees = new List<string> { s.playerId, npcs[1] };
                foreach (var nominee in s.nominees) { s.Find(nominee).nominationWeeks.Add(s.week); s.Find(nominee).timesNominated = 1; }
                s.vetoHolderId = s.playerId;
                s.vetoPlayers = Lineup(s);
                s.deals.Add(Deal("deal-late", DealKind.SafetyAgreement, s.playerId, hoh, expires: 2));
                if (rules) EpisodeEngine.EnableCommitments(s);
                Valid(s);
                var command = Command(s, EpisodeCommandKind.ResolveVeto, s.playerId, EpisodeEngine.ReplacementCandidates(s).First().id);
                command.useVeto = true;
                var result = new EpisodeEngine(s).Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(result.state.nominees, Does.Not.Contain(s.playerId), "Saved by their own veto,");
                Assert.That(result.state.deals.Single(d => d.id == "deal-late").status, Is.EqualTo(DealStatus.Active),
                    "but the Head of Household put them up: nothing they spared, so nothing kept.");
                Assert.That(result.state.events.Any(e => e.kind == "deal-outcome"), Is.False);
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
        public void C1_UnderTheRulesADealThePlayerTookIsAChanceTakenEvenWhenItLapsesUnseen()
        {
            string id = NpcDeals.OfferPrefix + "80";
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(61, 8);
                string asker = NpcIds(s)[0];
                s.deals.Add(Deal(id, DealKind.SafetyAgreement, asker, s.playerId, status: DealStatus.Proposed, expires: 1));
                if (rules) EpisodeEngine.EnableCommitments(s);
                Valid(s);
                var result = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.RespondToDeal, id, null, EpisodeEngine.AcceptDeal));
                Assert.That(result.accepted, Is.True, result.reason);
                // The week turns and the house lets the one-week pact lapse before any reveal has seen it taken.
                var lapsed = result.state.Clone();
                lapsed.week = 2;
                NpcDeals.Settle(lapsed);
                Assert.That(lapsed.deals.Single(d => d.id == id).status, Is.EqualTo(DealStatus.Expired));
                EpisodeEngine.ReconcileOpportunities(lapsed);
                var row = lapsed.ledger.opportunities.Single(o => o.id == id);
                var note = GameSense.Evaluate(lapsed).notes.Single(n => n.rowId == id);
                if (rules)
                {
                    Assert.That(row.response, Is.EqualTo(OpportunityResponse.Taken), "Under the rules the yes was on the record at once:");
                    Assert.That(note.points, Is.EqualTo(1), "a deal made.");
                }
                else
                {
                    Assert.That(row.response, Is.EqualTo(OpportunityResponse.Expired), "Without them it read as an offer left on the table,");
                    Assert.That(note.points, Is.EqualTo(-1), "and cost a point.");
                }
            }
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

            // A nominee's veto ask the player accepted: critical already, and nothing is heavier.
            var veto = AtTheVetoMeeting(true);
            string asking = veto.nominees[0];
            string ask = NpcDeals.VetoAskPrefix + "5";
            veto.deals.Add(Deal(ask, DealKind.VetoUse, asking, veto.playerId, expires: 1));
            Valid(veto);
            var declined = Command(veto, EpisodeCommandKind.ResolveVeto);
            declined.useVeto = false;
            var settled = new EpisodeEngine(veto).Apply(declined);
            Assert.That(settled.accepted, Is.True, settled.reason);
            var broken = settled.state.deals.Single(d => d.id == ask);
            Assert.That((broken.status, broken.brokenById), Is.EqualTo((DealStatus.Broken, veto.playerId)), "Left on the block: the player broke the veto ask they accepted.");
            Assert.That(DealResolution.AcceptedOffer(settled.state, broken), Is.True);
            Assert.That(DealResolution.BreachWeight(settled.state, broken), Is.EqualTo(DealTrust.Weight(DealTrust.Critical)), "One step heavier than critical is critical.");
            Assert.That(Entries(settled.state, asking, veto.playerId, "deal_broken").Single().impactScore, Is.EqualTo(DealResolution.BrokenBase * DealTrust.Weight(DealTrust.Critical)));

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
                EpisodeEngine.EnableStory(s);
                string partner = NpcIds(s)[3], named = s.nominees[0], instead = s.nominees[1];
                // They agreed to vote the first nominee out, and each votes the second out instead.
                s.deals.Add(Deal("deal-vote", DealKind.VoteEvict, s.playerId, partner, target: named, expires: s.week));
                SetScore(s, partner, named, 100); SetScore(s, partner, instead, -100);
                Valid(s);
                var engine = new EpisodeEngine(s);
                EpisodeEngineTests.OpenTheVote(engine);
                var open = engine.Snapshot;
                double mine = open.Score(open.playerId, partner), theirs = open.Score(partner, open.playerId);
                var cast = engine.Apply(Command(open, EpisodeCommandKind.CastVote, instead));
                Assert.That(cast.accepted, Is.True, cast.reason);
                var after = Resolve(engine);
                Valid(after);
                string player = after.playerId;
                var deal = after.deals.Single(d => d.id == "deal-vote");
                Assert.That(after.votes.Single(v => v.voterId == partner).targetId, Is.EqualTo(instead), "The partner broke it,");
                Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "and so did the player.");
                var myRecord = Entries(after, player, partner, "deal_broken");
                var theirRecord = Entries(after, partner, player, "deal_broken");
                var line = after.events.Single(e => e.kind == "deal-outcome");
                bool grudge = after.story.grudges.Any(g => g.cause == GrudgeCauses.DealBroken
                    && ((g.holderId == partner && g.targetId == player) || (g.holderId == player && g.targetId == partner)));
                if (rules)
                {
                    Assert.That(deal.brokenById, Is.Null, "Both broke it: the record names neither.");
                    Assert.That(deal.settledWeek, Is.EqualTo(after.week));
                    Assert.That(Breaches.BrokenByBoth(deal), Is.True);
                    Assert.That(Breaches.Broke(after, deal, player) && Breaches.Broke(after, deal, partner), Is.True, "Each broke it,");
                    Assert.That(NpcDeals.BrokenDeals(after, player), Is.EqualTo(1));
                    Assert.That(NpcDeals.BrokenDeals(after, partner), Is.EqualTo(1), "and each is held to it.");
                    Assert.That(myRecord.Select(e => e.impactScore), Is.EqualTo(theirRecord.Select(e => e.impactScore)),
                        "Neither is held as the one wronged: each holds it against the other alike, as a bloc's partners do.");
                    Assert.That(line.text, Is.EqualTo(after.Find(player).name + " and " + after.Find(partner).name + " fell out over their vote to evict."));
                    Assert.That(line.audienceIds, Is.EqualTo(new[] { partner }), "The line tells the partner's ballot, so it is not the player's until they know it.");
                    Assert.That(after.relationships.SelectMany(r => r.events).Any(e => e.type == "heard_about_betrayal"), Is.False,
                        "Nobody in particular broke it, so there is no betrayal for the house to hear of,");
                    Assert.That(grudge, Is.False, "no word-broken grudge either way,");
                    Assert.That(after.story.reckonings.Any(r => r.npcId == partner), Is.False, "and no reckoning.");
                    Assert.That(after.Score(player, partner), Is.EqualTo(mine), "The player's own view does not move with the partner's ballot;");
                    Assert.That(after.Score(partner, player), Is.EqualTo(theirs + DealResolution.BrokenBase * DealTrust.Weight(DealTrust.Medium)).Within(1e-9),
                        "the partner's does.");
                }
                else
                {
                    Assert.That(line.text, Is.EqualTo(after.Find(player).name + " broke a vote to evict with " + after.Find(partner).name + "."),
                        "Without the rules the first of the two was named,");
                    Assert.That(after.Score(partner, player), Is.LessThan(theirs), "and the second held it against them as the one wronged,");
                    Assert.That(grudge, Is.True, "with a word-broken grudge.");
                }
            }
        }

        [Test]
        public void C1_AVoteDealBothBrokeIsHeldAgainstEachInEveryBallot()
        {
            var s = Campaign(true);
            var npcs = NpcIds(s);
            string a = s.nominees[0], b = npcs[4], voter = npcs[3];
            var both = Deal("deal-both", DealKind.VoteSave, a, b, target: s.nominees[1], status: DealStatus.Broken, expires: s.week);
            both.settledWeek = s.week;
            s.deals.Add(both);
            Valid(s);
            var seen = WebEvictionVoting.FromNative(s, voter).state.deals.Single(d => d.id == "deal-both");
            Assert.That(seen.brokenByBoth, Is.True, "Both broke it,");
            Assert.That(seen.brokenById, Is.Null, "so it names neither,");
            Assert.That(Breaches.CountsAgainst(s, both, a) && Breaches.CountsAgainst(s, both, b), Is.True, "and it counts against each.");
            Assert.That(NpcDeals.BrokenDeals(s, a) + NpcDeals.BrokenDeals(s, b), Is.EqualTo(2));
            // Through a ballot's own value: the nominee who broke it is the bigger threat in a voter's evaluation.
            var without = s.Clone();
            without.deals.RemoveAll(d => d.id == "deal-both");
            double Threat(EpisodeState state) => WebEvictionVoting.EvaluateNative(state, voter).nomineeEvaluations
                .Single(n => n.nomineeId == a).factors.Single(f => f.code == "threat").value;
            Assert.That(Threat(s), Is.LessThan(Threat(without)), "A broken deal adds to a nominee's threat in the ballot, and both of them broke this one.");
            var withoutTheRules = Campaign(false);
            withoutTheRules.deals.Add(Deal("deal-both", DealKind.VoteSave, a, b, target: withoutTheRules.nominees[1], status: DealStatus.Broken, expires: s.week));
            Assert.That(WebEvictionVoting.FromNative(withoutTheRules, voter).state.deals.Single(d => d.id == "deal-both").brokenByBoth, Is.False,
                "Without the rules the web's own rule holds a breach against both sides anyway.");
        }

        // ------------------------------------------------------------ the jury house's knowledge

        [Test]
        public void C1_UnderTheRulesTheJuryHouseSaysHowABallotEndedADealOnlyOnceThePlayerKnowsThatBallot()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(55, 8);
                var npcs = NpcIds(s);
                string juror = npcs[0], hoh = npcs[1], other = npcs[2], hoh3 = npcs[3], up = npcs[4];
                s.week = 3;
                s.phase = EpisodePhase.Social;
                // Week two: the player and another on the block, the juror voting; week three: the juror out.
                s.ledger.power.Add(new PowerRow { week = 2, hohId = hoh, vetoHolderId = hoh, nominees = new List<string> { s.playerId, other }, tally = new List<int> { 1, 2 }, evicteeId = other });
                s.ledger.power.Add(new PowerRow { week = 3, hohId = hoh3, vetoHolderId = hoh3, nominees = new List<string> { juror, up }, tally = new List<int> { 3, 1 }, evicteeId = juror });
                s.Find(other).status = ContestantStatus.Jury;
                s.Find(juror).status = ContestantStatus.Jury;
                var deal = Deal("deal-partners", DealKind.Partnership, juror, s.playerId, status: DealStatus.Broken, expires: 0);
                var promise = Promise("promise-vote", juror, s.playerId, PromiseKind.Vote, week: 2, expires: 2);
                promise.targetId = other;
                promise.status = PromiseStatus.Broken;
                if (rules)
                {
                    EpisodeEngine.EnableCommitments(s, 1);
                    deal.brokenById = juror; deal.settledWeek = 2;
                    promise.brokenById = juror; promise.settledWeek = 2;
                }
                s.deals.Add(deal);
                s.promises.Add(promise);
                var knows = JuryHouseRead.ReadJuror(s, juror).knows;
                if (rules)
                {
                    Assert.That(knows, Does.Contain("Partnership: " + KnownBallots.Unresolved + "."), "The juror's ballot broke it, and the player cannot place that ballot,");
                    Assert.That(knows, Does.Contain("They promised you a vote: " + KnownBallots.Unresolved + "."), "nor the one that broke their vote promise.");
                }
                else
                {
                    Assert.That(knows, Does.Contain("Partnership: broken."), "Without the rules the page said it as it stood.");
                    Assert.That(knows, Does.Contain("They promised you a vote: broken."));
                }
            }
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

        private static string Json(object value) => JsonConvert.SerializeObject(value);

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

        /// <summary>Plain steps from a resolved eviction until the house has turned to the social week.</summary>
        private static EpisodeState ToTheWeek(EpisodeEngine engine)
        {
            for (int i = 0; i < 8 && engine.Snapshot.phase != EpisodePhase.Social; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Social), "The house turned to the social week.");
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
        private static EpisodeState Campaign(bool rules, uint seed = 7)
        {
            var s = ContentCatalog.Create(seed);
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
        /// A campaign season whose coin for the voter who will hold an information deal (the fourth
        /// houseguest) falls between 0.3 and 0.9 this week: a partner whose word is worth a fifth or a
        /// quarter lies on it, one whose word is worth nineteen in twenty tells the truth.
        /// </summary>
        private static uint SeasonWithAMiddlingReadingCoin(out double coin)
        {
            for (uint seed = 7; seed < 200; seed++)
            {
                var s = Campaign(true, seed);
                coin = StoryRandom.Unit(s, EpisodeEngine.ReadingKey(s, NpcIds(s)[3]));
                if (coin > 0.3 && coin < 0.9) return seed;
            }
            Assert.Fail("No season of the first two hundred draws a middling coin for the reading.");
            coin = 0;
            return 0;
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

        /// <summary>The veto meeting with the player holding the veto: a houseguest at the head of the house, two houseguests on the block.</summary>
        private static EpisodeState AtTheVetoMeeting(bool rules)
        {
            var s = Season(29, 8);
            var npcs = NpcIds(s);
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            foreach (var nominee in s.nominees) { s.Find(nominee).nominationWeeks.Add(s.week); s.Find(nominee).timesNominated = 1; }
            s.vetoHolderId = s.playerId;
            s.vetoPlayers = Lineup(s);
            if (rules) EpisodeEngine.EnableCommitments(s);
            return s;
        }

        /// <summary>The veto's six: the Head of Household, the block and the holder, then the rest of the house in order.</summary>
        private static List<string> Lineup(EpisodeState s)
        {
            var seats = new List<string> { s.hohId }.Concat(s.nominees).Concat(new[] { s.vetoHolderId })
                .Concat(s.Active.Select(c => c.id)).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            return seats.Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
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
