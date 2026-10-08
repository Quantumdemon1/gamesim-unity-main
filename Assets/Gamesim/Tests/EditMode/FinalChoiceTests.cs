using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// ACTIONS-DEALS-ALLIANCES-PLAN C9, the endgame. Under the commitment rules a houseguest's final
    /// choice is the final three's own: it weighs the jury the final two will face (the Head of
    /// Household would rather sit beside somebody they can beat), a pact that holds from the Head of
    /// Household's side, and the endgame's deals - C1's final two and C7's held promise as they were, and
    /// a final three deal kept to the final three - and leaves out the evaluator's terms for a game no
    /// longer left to play. And the final three deal: the commitment rules' own kind, put from the final
    /// six to the final four, broken by a nomination of the partner, kept by both when the two reach the
    /// final three, ended by an evictee. Each is shown as a season without the commitment rules plays it -
    /// as every recorded season does - and as it plays under them. Unity-free, so the dotnet subset runs
    /// it (Tools/SimulationTests); FinalChoiceSeasonHarness measures the choice over sampled seasons.
    /// </summary>
    public sealed class FinalChoiceTests
    {
        // ------------------------------------------------------------ the final choice's terms

        [Test]
        public void UnderTheRulesAPactThatHoldsFromTheHeadOfHouseholdsSideKeepsTheFinalist()
        {
            var bare = Final(true, out string hoh, out string other);
            Assert.That(EpisodeEngine.FinalChoice(bare).selectedNomineeId, Is.EqualTo(bare.playerId),
                "Control: a little warmer on the other finalist, the Head of Household evicts the player.");

            var s = Final(true, out hoh, out other);
            Pact(s, "pact-final", "The Final Pact", hoh, s.playerId);
            Valid(s);
            var pact = EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Where(t => t.code == EpisodeEngine.PactFactor).ToList();
            Assert.That(pact, Has.Count.EqualTo(1), "One pact term, however many pacts.");
            Assert.That(pact[0].nomineeId, Is.EqualTo(s.playerId));
            Assert.That(pact[0].value, Is.EqualTo(EpisodeEngine.FinalPactTerm), "The pact's weight, by a plain word.");
            Assert.That(pact[0].evidenceIds, Does.Contain("pact-final"));
            Assert.That(EpisodeEngine.FinalChoice(s).selectedNomineeId, Is.EqualTo(other), "The pact keeps the player.");

            s.Find(hoh).traits = new List<string> { "Loyal" };
            Assert.That(EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Single(t => t.code == EpisodeEngine.PactFactor).value,
                Is.EqualTo(EpisodeEngine.FinalPactTerm * EpisodeEngine.LoyalObligation), "A Loyal word is worth half as much again.");
            s.Find(hoh).traits = new List<string> { "Sneaky" };
            Assert.That(EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Any(t => t.code == EpisodeEngine.PactFactor), Is.False,
                "A Sneaky word is worth nothing.");

            // The engine's own final eviction makes the same choice.
            s.Find(hoh).traits = new List<string>();
            var after = Step(s);
            Assert.That(after.Find(other).status, Is.EqualTo(ContestantStatus.Jury), "The engine evicted the other finalist.");
            Assert.That(after.Find(after.playerId).status, Is.EqualTo(ContestantStatus.Active));
        }

        [Test]
        public void APactTheHeadOfHouseholdTurnedOnOrWentColdOnOrThatEndedHoldsNothing()
        {
            // Turned on: a betrayal of theirs stands against it (C2).
            var betrayed = Final(true, out string hoh, out string other);
            int now = betrayed.week;
            betrayed.week = 2;
            Pact(betrayed, "pact-final", "The Final Pact", hoh, betrayed.playerId);
            betrayed.week = 3;
            RelationshipLedger.RecordOneWay(betrayed, betrayed.playerId, hoh, Allegiance.BetrayedType, Allegiance.BetrayalImpact,
                Allegiance.Record(betrayed.Find(hoh).name, Allegiance.Nominated, new[] { "The Final Pact" }));
            betrayed.week = now;
            Valid(betrayed);
            Assert.That(Allegiance.Holds(betrayed, hoh, betrayed.playerId), Is.False, "The betrayal stands against the pact.");
            Assert.That(EpisodeEngine.FinalChoiceTerms(betrayed, hoh, Finalists(betrayed)).Any(t => t.code == EpisodeEngine.PactFactor), Is.False, "No pact term.");
            Assert.That(EpisodeEngine.FinalChoice(betrayed).selectedNomineeId, Is.EqualTo(betrayed.playerId), "Nothing keeps the player.");

            // Gone cold: their own view of the player under the quiet line (C3).
            var cold = Final(true, out hoh, out other, viewOfPlayer: Allegiance.QuietLine - 5, viewOfOther: Allegiance.QuietLine + 5);
            Pact(cold, "pact-final", "The Final Pact", hoh, cold.playerId);
            Valid(cold);
            Assert.That(EpisodeEngine.FinalChoiceTerms(cold, hoh, Finalists(cold)).Any(t => t.code == EpisodeEngine.PactFactor), Is.False, "No pact term.");
            Assert.That(EpisodeEngine.FinalChoice(cold).selectedNomineeId, Is.EqualTo(cold.playerId));

            // Over: a pact that ended.
            var ended = Final(true, out hoh, out other);
            Pact(ended, "pact-final", "The Final Pact", hoh, ended.playerId).active = false;
            Valid(ended);
            Assert.That(EpisodeEngine.FinalChoiceTerms(ended, hoh, Finalists(ended)).Any(t => t.code == EpisodeEngine.PactFactor), Is.False, "No pact term.");
            Assert.That(EpisodeEngine.FinalChoice(ended).selectedNomineeId, Is.EqualTo(ended.playerId));

            // A pact between the Head of Household and the other finalist holds as a pact holds.
            var theirs = Final(true, out hoh, out other, viewOfPlayer: 20, viewOfOther: 10);
            Pact(theirs, "pact-theirs", "The Other Pact", hoh, other);
            Valid(theirs);
            Assert.That(EpisodeEngine.FinalChoiceTerms(theirs, hoh, Finalists(theirs)).Single(t => t.code == EpisodeEngine.PactFactor).nomineeId, Is.EqualTo(other));
            Assert.That(EpisodeEngine.FinalChoice(theirs).selectedNomineeId, Is.EqualTo(theirs.playerId), "Their pact keeps the other finalist.");
        }

        [Test]
        public void TheEndgamesDealsAndAHeldPromiseWeighAsC1AndC7MadeThem()
        {
            // A final two deal has fixed strength while its formal status stands, separate from current liking.
            var s = Final(true, out string hoh, out string other);
            s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, hoh, s.playerId, expires: 0));
            Valid(s);
            var obligation = Factor(EpisodeEngine.FinalChoice(s), s.playerId, "obligation");
            Assert.That(obligation, Is.EqualTo(EpisodeEngine.FinalTwoObligation).Within(1e-9), "The active final two's fixed strength.");
            Assert.That(EpisodeEngine.FinalChoice(s).selectedNomineeId, Is.EqualTo(other));

            // A final two promise the player called in (C7): the same obligation, times the hold.
            var held = Final(true, out hoh, out other);
            held.promises.Add(new PromiseState { id = "promise-final-two", fromId = hoh, toId = held.playerId, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = 3 });
            RelationshipLedger.RecordOneWay(held, hoh, held.playerId, Negotiation.HeldType(PromiseKind.FinalTwo, Negotiation.Demand), 0, "Held to it.");
            Valid(held);
            Assert.That(Factor(EpisodeEngine.FinalChoice(held), held.playerId, "obligation"),
                Is.EqualTo(EpisodeEngine.FinalTwoObligation * Negotiation.Hold(Negotiation.Demand)).Within(1e-9), "The existing hold multiplier still applies.");
            Assert.That(EpisodeEngine.FinalChoice(held).selectedNomineeId, Is.EqualTo(other));
        }

        [Test]
        public void UnderTheRulesTheHeadOfHouseholdWouldRatherSitBesideSomebodyTheyCanBeat()
        {
            // The jury would crown the player beside the Head of Household and the Head of Household beside the other.
            var beaten = Final(true, out string hoh, out string other, viewOfPlayer: 20, viewOfOther: 20);
            JuryFor(beaten, beaten.playerId, over: hoh, andHohOver: other);
            Valid(beaten);
            Assert.That(EpisodeEngine.FinalJuryChance(beaten, hoh, beaten.playerId), Is.EqualTo(0).Within(1e-9), "Beside the player the jury crowns the player.");
            Assert.That(EpisodeEngine.FinalJuryChance(beaten, hoh, other), Is.EqualTo(1).Within(1e-9), "Beside the other, the Head of Household.");
            var terms = EpisodeEngine.FinalChoiceTerms(beaten, hoh, Finalists(beaten)).Where(t => t.code == EpisodeEngine.JuryFactor).ToList();
            Assert.That(terms.Single(t => t.nomineeId == beaten.playerId).value, Is.EqualTo(-EpisodeEngine.FinalJuryWeight / 2).Within(1e-9));
            Assert.That(terms.Single(t => t.nomineeId == other).value, Is.EqualTo(EpisodeEngine.FinalJuryWeight / 2).Within(1e-9));
            Assert.That(EpisodeEngine.FinalChoice(beaten).selectedNomineeId, Is.EqualTo(beaten.playerId), "They cut the one who would beat them.");

            // The other way round: the player is the one they can beat.
            var goat = Final(true, out hoh, out other, viewOfPlayer: 20, viewOfOther: 20);
            JuryFor(goat, other, over: hoh, andHohOver: goat.playerId);
            Valid(goat);
            Assert.That(EpisodeEngine.FinalJuryChance(goat, hoh, goat.playerId), Is.EqualTo(1).Within(1e-9));
            Assert.That(EpisodeEngine.FinalJuryChance(goat, hoh, other), Is.EqualTo(0).Within(1e-9));
            Assert.That(EpisodeEngine.FinalChoice(goat).selectedNomineeId, Is.EqualTo(other), "They take the player, whom they can beat.");
            Assert.That(Step(goat).Find(goat.playerId).status, Is.EqualTo(ContestantStatus.Active), "The engine's own choice.");

            // And a pact and a final two deal, kept together, outweigh the surest jury.
            var loyal = Final(true, out hoh, out other, viewOfPlayer: 50, viewOfOther: 50);
            JuryFor(loyal, loyal.playerId, over: hoh, andHohOver: other);
            Pact(loyal, "pact-final", "The Final Pact", hoh, loyal.playerId);
            loyal.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, hoh, loyal.playerId, expires: 0));
            Valid(loyal);
            Assert.That(EpisodeEngine.FinalChoice(loyal).selectedNomineeId, Is.EqualTo(other), "Their word holds against the jury.");
        }

        [Test]
        public void TheJurysChanceReadsTheJuryAsItWillSitTheOneCutAmongThem()
        {
            var s = Final(true, out string hoh, out string other, viewOfPlayer: 20, viewOfOther: 20);
            Valid(s);
            Assert.That(EpisodeEngine.FinalJuryChance(s, hoh, s.playerId), Is.EqualTo(1).Within(1e-9), "Every juror sure of the Head of Household.");

            // Two jurors each way, the cut finalist one of them: a tie goes to the second of the two in cast order.
            var tie = Final(true, out hoh, out other, viewOfPlayer: 20, viewOfOther: 20);
            var jurors = tie.contestants.Where(c => c.status == ContestantStatus.Jury).Select(c => c.id).ToList();
            foreach (var finalist in new[] { tie.playerId, other })
            {
                SetScore(tie, jurors[2], hoh, -100); SetScore(tie, jurors[2], finalist, 100);
            }
            SetScore(tie, other, tie.playerId, 100); SetScore(tie, other, hoh, -100);
            SetScore(tie, tie.playerId, other, 100); SetScore(tie, tie.playerId, hoh, -100);
            Valid(tie);
            Assert.That(tie.contestants.FindIndex(c => c.id == hoh), Is.GreaterThan(tie.contestants.FindIndex(c => c.id == tie.playerId)));
            Assert.That(EpisodeEngine.FinalJuryChance(tie, hoh, tie.playerId), Is.EqualTo(1).Within(1e-9),
                "Beside the player, two each: the tie is the Head of Household's, after the player in cast order.");
            Assert.That(EpisodeEngine.FinalJuryChance(tie, hoh, other), Is.EqualTo(0).Within(1e-9),
                "Beside the other, two each: the tie is the other's, after the Head of Household.");

            // Every juror on the fence: half and half each, a majority of four or the tie.
            var fence = Final(true, out hoh, out other, viewOfPlayer: 20, viewOfOther: 20);
            foreach (var juror in fence.contestants.Where(c => c.id != hoh))
                foreach (var finalist in fence.contestants.Take(3))
                    if (juror.id != finalist.id) SetScore(fence, juror.id, finalist.id, 0);
            Valid(fence);
            Assert.That(EpisodeEngine.FinalJuryChance(fence, hoh, fence.playerId), Is.EqualTo(11.0 / 16).Within(1e-9), "Three or four of four, or two and the tie.");
            Assert.That(EpisodeEngine.FinalJuryChance(fence, hoh, other), Is.EqualTo(5.0 / 16).Within(1e-9), "Three or four of four.");

            Assert.That(EpisodeEngine.FinalJuryChance(s, hoh, hoh), Is.EqualTo(0), "Nobody sits beside themselves.");
            Assert.That(EpisodeEngine.FinalJuryChance(s, hoh, jurors[0]), Is.EqualTo(0), "Nor beside a juror.");
        }

        [Test]
        public void UnderTheRulesTheFinalChoiceLeavesOutTheTermsForAGameStillToPlay()
        {
            var under = EpisodeEngine.FinalChoice(Final(true, out _, out _));
            var codes = under.nomineeEvaluations.SelectMany(n => n.factors).Select(f => f.code).Distinct().ToList();
            foreach (string code in EpisodeEngine.FinalChoiceLeavesOut)
                Assert.That(codes, Does.Not.Contain(code), code + " is left out.");
            Assert.That(codes, Does.Contain(EpisodeEngine.JuryFactor));
            Assert.That(codes, Does.Contain("relationship"), "The rest of the evaluator stands.");

            var without = EpisodeEngine.FinalChoice(Final(false, out _, out _));
            var those = without.nomineeEvaluations.SelectMany(n => n.factors).Select(f => f.code).Distinct().ToList();
            foreach (string code in EpisodeEngine.FinalChoiceLeavesOut)
                Assert.That(those, Does.Contain(code), code + " stands without the rules.");
            Assert.That(those, Does.Not.Contain(EpisodeEngine.JuryFactor));
            Assert.That(those, Does.Not.Contain(EpisodeEngine.PactFactor));
        }

        [Test]
        public void WithoutTheRulesTheFinalChoiceIsTheEvaluatorsAsItWas()
        {
            foreach (var setup in new System.Action<EpisodeState, string, string>[]
                     {
                         (s, hoh, other) => { },
                         (s, hoh, other) => Pact(s, "pact-final", "The Final Pact", hoh, s.playerId),
                         (s, hoh, other) => JuryFor(s, s.playerId, over: hoh, andHohOver: other),
                         (s, hoh, other) => s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, hoh, s.playerId, expires: 0)),
                     })
            {
                var s = Final(false, out string hoh, out string other);
                setup(s, hoh, other);
                Valid(s);
                Assert.That(EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)), Is.Empty, "None of C9's terms without the rules.");
                Assert.That(Json(EpisodeEngine.FinalChoice(s)), Is.EqualTo(Json(TheEvaluator(s))), "The evaluator's own choice, term for term.");
            }
        }

        // ------------------------------------------------------------ the final three deal

        [Test]
        public void AFinalThreeDealIsTheCommitmentRulesOwnAndPutOnlyFromTheFinalSixToTheFinalFour()
        {
            Assert.That(DealKind.IsKnown(DealKind.FinalThree), Is.True);
            Assert.That(DealKind.All, Does.Contain(DealKind.FinalThree));
            Assert.That(DealKind.Title(DealKind.FinalThree), Is.EqualTo("Final Three Deal"));
            Assert.That(DealKind.DefaultTrust(DealKind.FinalThree), Is.EqualTo(DealKind.DefaultTrust(DealKind.SafetyAgreement)), "A safety pact's weight.");
            Assert.That(DealKind.NamesATarget(DealKind.FinalThree), Is.False);

            var off = House(false, 5);
            string npc = Npcs(off)[0];
            Assert.That(PlayerDeals.CanPropose(off, npc, DealKind.FinalThree, null, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("That is not a deal anybody in this house would recognise."), "Refused as an unknown kind always was.");
            Assert.That(PlayerDeals.Available(off, npc), Does.Not.Contain(DealKind.FinalThree));

            var early = House(true, 7);
            Assert.That(PlayerDeals.CanPropose(early, Npcs(early)[0], DealKind.FinalThree, null, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("It is too early in the season to be talking about the final three."));

            foreach (int left in new[] { 6, 5, 4 })
            {
                var s = House(true, left);
                Assert.That(PlayerDeals.CanPropose(s, Npcs(s)[0], DealKind.FinalThree, null, out reason), Is.True, left + ": " + reason);
                Assert.That(PlayerDeals.Available(s, Npcs(s)[0]), Does.Contain(DealKind.FinalThree), left + " left.");
            }

            var three = House(true, 3);
            Assert.That(PlayerDeals.CanPropose(three, Npcs(three)[0], DealKind.FinalThree, null, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(PlayerDeals.FinalThreeHereRefusal));

            var draft = PlayerDeals.Draft(House(true, 5), npc, DealKind.FinalThree, null, "deal-player-1");
            Assert.That(draft.expiresWeek, Is.EqualTo(0), "Open-ended: it runs until the final three.");
            Assert.That(draft.trustImpact, Is.EqualTo(DealTrust.High));
        }

        [Test]
        public void ASeasonWithoutTheRulesHoldsNoFinalThreeDeal()
        {
            var off = House(false, 5);
            off.deals.Add(Deal("deal-final-three", DealKind.FinalThree, off.playerId, Npcs(off)[0], expires: 0));
            Assert.That(EpisodeValidation.TryValidate(off, out var error), Is.False);
            Assert.That(error, Is.EqualTo("A season without the commitment rules has none of their records."));

            var on = House(true, 5);
            on.deals.Add(Deal("deal-final-three", DealKind.FinalThree, on.playerId, Npcs(on)[0], expires: 0));
            Valid(on);
        }

        [Test]
        public void ItIsAskedAsTheOtherEndgameCommitmentIsAndShownAsTheRollReadsIt()
        {
            var s = House(true, 5);
            string npc = Npcs(s)[0];
            s.Find(npc).traits = new List<string>();
            SetScore(s, npc, s.playerId, 60);
            double warm = PlayerDeals.AcceptanceChance(s, npc, DealKind.FinalThree, null);
            Assert.That(warm, Is.EqualTo(PlayerDeals.RelationshipChance(60) - 10).Within(1e-9), "Its ask, as a final two's: ten off.");
            SetScore(s, npc, s.playerId, 20);
            Assert.That(PlayerDeals.AcceptanceChance(s, npc, DealKind.FinalThree, null),
                Is.EqualTo(PlayerDeals.RelationshipChance(20) - 10 - PlayerDeals.FinalThreeColdPenalty).Within(1e-9), "A stretch below the warm line.");
            s.Find(npc).traits = new List<string> { "Loyal" };
            Assert.That(PlayerDeals.TraitModifier(s.Find(npc).traits, DealKind.FinalThree), Is.EqualTo(PlayerDeals.TraitModifier(s.Find(npc).traits, DealKind.FinalTwo)),
                "The loyal want it as they want a final two.");
            // Where the player's own reading of them is their view of the player, the odds shown are the roll's.
            SetScore(s, s.playerId, npc, 20);
            s.ledger.standings.Add(new StandingRow { week = s.week, fromId = npc, toId = s.playerId, source = ClaimSource.Read, score = 20 });
            Assert.That(KnownOdds.Deal(s, npc, DealKind.FinalThree, null).chance, Is.EqualTo(PlayerDeals.AcceptanceChance(s, npc, DealKind.FinalThree, null)).Within(1e-9));
        }

        [Test]
        public void AHeadOfHouseholdWhoNominatesTheirPartnerBreaksIt()
        {
            // A houseguest at the head of the house puts the player up despite the deal: broken by them, on the record.
            var s = House(true, 6);
            string hoh = Npcs(s)[0];
            s.phase = EpisodePhase.Nomination;
            s.hohId = hoh;
            foreach (var actor in s.contestants.Where(c => c.id != hoh)) SetScore(s, hoh, actor.id, actor.isPlayer ? -90 : 60);
            s.deals.Add(Deal("deal-final-three", DealKind.FinalThree, s.playerId, hoh, expires: 0));
            Valid(s);
            Assert.That(StrategyRules.NominationReluctance(s, hoh, s.playerId), Is.EqualTo(-90 + StrategyRules.DealWeight(DealKind.SafetyAgreement)).Within(1e-9),
                "The deal holds them back as a safety pact would.");
            var after = Step(s);
            Assert.That(after.nominees, Does.Contain(after.playerId), "Not enough to hold them back here.");
            var deal = after.deals.Single(d => d.id == "deal-final-three");
            Assert.That(deal.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(deal.brokenById, Is.EqualTo(hoh), "Broken by them.");
            Assert.That(deal.settledWeek, Is.EqualTo(after.week));
            var page = CommitmentsRead.Of(after).Single(c => c.id == "deal-final-three");
            Assert.That(page.status, Is.EqualTo("broken by them"));
            Assert.That(page.binds, Is.EqualTo(CommitmentsRead.FinalThreeBinds));

            // The player at the head of the house is warned before nominating their partner.
            var mine = House(true, 6);
            mine.phase = EpisodePhase.Nomination;
            mine.hohId = mine.playerId;
            string partner = Npcs(mine)[0], other = Npcs(mine)[1];
            mine.deals.Add(Deal("deal-final-three", DealKind.FinalThree, mine.playerId, partner, expires: 0));
            Valid(mine);
            var breaches = CommitmentsRead.WouldBreak(mine, CommitmentsRead.Decision.Nominate(partner, other));
            Assert.That(breaches.Select(b => b.id), Is.EqualTo(new[] { "deal-final-three" }));
            Assert.That(CommitmentsRead.Warning(mine, breaches), Does.Contain("final three deal"));
            Assert.That(CommitmentsRead.WouldBreak(mine, CommitmentsRead.Decision.Nominate(other, Npcs(mine)[2])), Is.Empty, "Nominating anybody else breaks nothing.");
        }

        [Test]
        public void TwoWhoReachTheFinalThreeTogetherHaveKeptIt()
        {
            var s = House(true, 3);
            string partner = Npcs(s)[0], other = Npcs(s)[1];
            s.deals.Add(Deal("deal-final-three", DealKind.FinalThree, s.playerId, partner, expires: 0));
            s.deals.Add(Deal("deal-final-three-theirs", DealKind.FinalThree, partner, other, expires: 0));
            Valid(s);
            double before = s.Score(partner, s.playerId), mine = s.Score(s.playerId, partner);
            var after = Step(s);
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1), "The final three's Head of Household begins.");
            foreach (var id in new[] { "deal-final-three", "deal-final-three-theirs" })
            {
                var deal = after.deals.Single(d => d.id == id);
                Assert.That(deal.status, Is.EqualTo(DealStatus.Fulfilled), id + " kept.");
                Assert.That(deal.brokenById, Is.Null);
                Assert.That(deal.settledWeek, Is.EqualTo(after.week));
            }
            double kept = DealResolution.FulfilledBase * DealTrust.Weight(DealTrust.High);
            Assert.That(after.Score(partner, s.playerId), Is.EqualTo(before + kept).Within(1e-9), "Both of them kept it: both think the better of each other.");
            Assert.That(after.Score(after.playerId, partner), Is.EqualTo(mine + kept).Within(1e-9));
            Assert.That(after.events.Any(e => e.kind == "deal-outcome" && e.text.Contains(" held to their final three deal.")
                && e.audienceIds.Contains(after.playerId) && e.audienceIds.Contains(partner)), Is.True, "Told to both.");
            Assert.That(CommitmentsRead.Of(after).Single(c => c.id == "deal-final-three").status, Is.EqualTo("honoured"));
        }

        [Test]
        public void AFinalThreeDealEndsBlamingNobodyWithWhicheverOfThemLeaves()
        {
            var s = Campaign(true);
            var npcs = Npcs(s);
            string leaving = npcs[1], staying = npcs[2];
            foreach (var voter in new[] { npcs[3], npcs[4] }) { SetScore(s, voter, leaving, -100); SetScore(s, voter, staying, 100); }
            s.deals.Add(Deal("deal-final-three", DealKind.FinalThree, s.playerId, leaving, week: s.week, expires: 0));
            Valid(s);
            var engine = new EpisodeEngine(s);
            for (int i = 0; i < 40 && engine.Snapshot.phase != EpisodePhase.Social; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var after = engine.Snapshot;
            Assert.That(after.Find(leaving).status, Is.EqualTo(ContestantStatus.Jury), "The partner was evicted.");
            var deal = after.deals.Single(d => d.id == "deal-final-three");
            Assert.That(deal.status, Is.EqualTo(DealStatus.Expired), "Ended with them (X4).");
            Assert.That(deal.brokenById, Is.Null);
            Assert.That(after.relationships.SelectMany(r => r.events).Any(e => e.type == "deal_broken"), Is.False, "Nobody holds it against anybody.");
            Assert.That(CommitmentsRead.Of(after).Single(c => c.id == "deal-final-three").term, Does.StartWith("until they left"));
        }

        [Test]
        public void AFinalThreeDealKeptToTheFinalThreeIsAnObligationInTheFinalChoice()
        {
            var s = Final(true, out string hoh, out string other);
            s.deals.Add(Kept(Deal("deal-final-three", DealKind.FinalThree, hoh, s.playerId, expires: 0), s.week));
            Valid(s);
            var term = EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Single(t => t.code == "obligation");
            Assert.That(term.nomineeId, Is.EqualTo(s.playerId));
            Assert.That(term.value, Is.EqualTo(EpisodeEngine.FinalThreeObligation).Within(1e-9), "A kept final three has fixed strength, half a final two's.");
            Assert.That(term.evidenceIds, Does.Contain("deal-final-three"));
            Assert.That(EpisodeEngine.FinalChoice(s).selectedNomineeId, Is.EqualTo(other), "It keeps the player here.");

            SetScore(s, hoh, s.playerId, 50);
            Assert.That(EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Single(t => t.code == "obligation").value,
                Is.EqualTo(EpisodeEngine.FinalTwoObligation / 2).Within(1e-9), "Changing liking does not change the formally kept commitment.");
            s.Find(hoh).traits = new List<string> { "Sneaky" };
            Assert.That(EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Any(t => t.code == "obligation"), Is.False, "A Sneaky word is worth nothing.");

            // One that was broken, or never kept, weighs nothing.
            var broken = Final(true, out hoh, out other);
            var deal = Deal("deal-final-three", DealKind.FinalThree, hoh, broken.playerId, status: DealStatus.Broken, expires: 0);
            deal.brokenById = hoh; deal.settledWeek = broken.week;
            broken.deals.Add(deal);
            Valid(broken);
            Assert.That(EpisodeEngine.FinalChoiceTerms(broken, hoh, Finalists(broken)).Any(t => t.code == "obligation"), Is.False);
        }

        [Test]
        public void UnderTheRulesHouseguestsPutAFinalThreeDealAtTheEndgameAndNotBefore()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = House(rules, 6);
                string npc = Npcs(s)[0];
                foreach (var a in s.contestants) foreach (var b in s.contestants) if (a.id != b.id) SetScore(s, a.id, b.id, 0);
                SetScore(s, npc, s.playerId, 50);
                s.deals.Add(Deal("deal-partners", DealKind.Partnership, npc, s.playerId, expires: 0));
                Valid(s);
                string offer = NpcDeals.Offer(s, npc, s.playerId);
                if (rules) Assert.That(offer, Is.EqualTo(DealKind.FinalThree), "Partners at fifty, six left: a final three.");
                else Assert.That(offer, Is.Not.EqualTo(DealKind.FinalThree), "Without the rules the ladder is the reference's.");
                if (!rules) continue;
                SetScore(s, npc, s.playerId, NpcDeals.FinalThreeWarmth);
                Assert.That(NpcDeals.Offer(s, npc, s.playerId), Is.Not.EqualTo(DealKind.FinalThree), "Not at the rung's own line.");
                SetScore(s, npc, s.playerId, 50);
                s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, npc, s.playerId, expires: 0));
                Assert.That(NpcDeals.Offer(s, npc, s.playerId), Is.Not.EqualTo(DealKind.FinalThree), "Not to somebody a final two already binds them to.");
                var seven = House(true, 7);
                SetScore(seven, Npcs(seven)[0], seven.playerId, 50);
                seven.deals.Add(Deal("deal-partners", DealKind.Partnership, Npcs(seven)[0], seven.playerId, expires: 0));
                Assert.That(NpcDeals.Offer(seven, Npcs(seven)[0], seven.playerId), Is.Not.EqualTo(DealKind.FinalThree), "Not before the final six.");
            }
        }

        [Test]
        public void ABallotWeighsAFinalThreePartnerAsASafetyPartner()
        {
            var s = Campaign(true);
            var npcs = Npcs(s);
            string voter = npcs[3], nominee = npcs[1];
            s.deals.Add(Deal("deal-final-three", DealKind.FinalThree, voter, nominee, week: s.week, expires: 0));
            Valid(s);
            double three = Factor(WebEvictionVoting.EvaluateNative(s, voter), nominee, "deal");
            var safe = Campaign(true);
            safe.deals.Add(Deal("deal-safety", DealKind.SafetyAgreement, voter, nominee, week: safe.week, expires: safe.week));
            Valid(safe);
            Assert.That(three, Is.GreaterThan(0));
            Assert.That(three, Is.EqualTo(Factor(WebEvictionVoting.EvaluateNative(safe, voter), nominee, "deal")).Within(1e-9));
        }

        // ------------------------------------------------------------ the review's fixes

        [Test]
        public void TheJuryIsReadAsTheCutLeavesItAndAFinalTwoPartnerCutIsNoFriendlyJuror()
        {
            // Taylor holds a final two deal with Maya and likes her; Maya taking the player breaks it as Taylor is cut.
            var s = Final(true, out string hoh, out string other, viewOfPlayer: 20, viewOfOther: 20);
            s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, hoh, other, expires: 0));
            var jurors = s.contestants.Where(c => c.status == ContestantStatus.Jury).Select(c => c.id).ToList();
            SetScore(s, jurors[0], hoh, 100); SetScore(s, jurors[0], s.playerId, -100);
            foreach (var juror in jurors.Skip(1)) { SetScore(s, juror, hoh, -100); SetScore(s, juror, s.playerId, 100); }
            SetScore(s, other, hoh, 40); SetScore(s, other, s.playerId, 0);
            Valid(s);
            string before = Json(s);
            uint stream = s.randomState;
            int sequence = s.nextSequence;
            // Cut, Taylor's view of Maya falls by the broken deal's weight (-45, to -5) and the breach weighs against her
            // on the jury's own obligations (to nothing): a tie the cast order gives Maya now needs Taylor at 121 in 800.
            Assert.That(EpisodeEngine.FinalJuryChance(s, hoh, s.playerId), Is.EqualTo(121.0 / 800).Within(1e-9),
                "Read on the season as it stands, Taylor's warmth and her standing deal would make her Maya's vote: about 95 in 100.");
            Assert.That(Json(s), Is.EqualTo(before), "The season read is never written.");
            Assert.That((s.randomState, s.nextSequence), Is.EqualTo((stream, sequence)), "Its stream and its sequence never move: the copy draws on itself.");
        }

        [Test]
        public void ReadingTheFinalChoiceWritesNothingAndDrawsNothing()
        {
            var s = Final(true, out string hoh, out string other, viewOfPlayer: 30, viewOfOther: 20);
            Pact(s, "pact-final", "The Final Pact", hoh, s.playerId);
            s.deals.Add(Kept(Deal("deal-final-three", DealKind.FinalThree, hoh, s.playerId, expires: 0), s.week));
            s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, hoh, other, expires: 0));
            Valid(s);
            string before = Json(s);
            uint stream = s.randomState;
            int sequence = s.nextSequence;
            EpisodeEngine.FinalChoice(s);
            EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s));
            EpisodeEngine.FinalJuryChance(s, hoh, s.playerId);
            EpisodeEngine.FinalJuryChance(s, hoh, other);
            Assert.That(Json(s), Is.EqualTo(before));
            Assert.That(s.randomState, Is.EqualTo(stream));
            Assert.That(s.nextSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void AColdHeadOfHouseholdsPactWithTheOtherFinalistHoldsNothingEither()
        {
            var s = Final(true, out string hoh, out string other, viewOfPlayer: 20, viewOfOther: Allegiance.QuietLine - 5);
            Pact(s, "pact-theirs", "The Other Pact", hoh, other);
            Valid(s);
            Assert.That(EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Any(t => t.code == EpisodeEngine.PactFactor), Is.False,
                "Both finalists are held to the same cold line.");
            SetScore(s, hoh, other, Allegiance.QuietLine);
            Assert.That(EpisodeEngine.FinalChoiceTerms(s, hoh, Finalists(s)).Single(t => t.code == EpisodeEngine.PactFactor).nomineeId, Is.EqualTo(other), "At the line it holds.");
        }

        [Test]
        public void TheFinalChoicesOwnTermsArePrivateAndNeverAReasonThePlayerHears()
        {
            var s = Final(true, out string hoh, out string other, viewOfPlayer: 20, viewOfOther: 10);
            Pact(s, "pact-theirs", "The Other Pact", hoh, other);
            Valid(s);
            var evaluation = EpisodeEngine.FinalChoice(s);
            var mine = evaluation.nomineeEvaluations.SelectMany(n => n.factors).Where(f => f.code == EpisodeEngine.PactFactor || f.code == EpisodeEngine.JuryFactor).ToList();
            Assert.That(mine.Select(f => f.code).Distinct().Count(), Is.EqualTo(2), "Both terms are in the choice.");
            Assert.That(mine.All(f => f.visibility == "private"), Is.True, "Theirs alone.");
            Assert.That(evaluation.publicReasonCodes, Does.Not.Contain(EpisodeEngine.PactFactor));
            Assert.That(evaluation.publicReasonCodes, Does.Not.Contain(EpisodeEngine.JuryFactor));
            Assert.That(EpisodeEngine.FinalChoiceLeavesOut, Is.EqualTo(new[] { "threat", "strategicValue", "personality" }),
                "The lead's ruling: the traits two share are left out of the final choice too.");
        }

        [Test]
        public void AFinalThreeDealDoesNotStackOnAFinalTwoNorIsAFinalTwoItsPrice()
        {
            var s = House(true, 5);
            string npc = Npcs(s)[0];
            s.deals.Add(Deal("deal-final-two", DealKind.FinalTwo, s.playerId, npc, expires: 0));
            Valid(s);
            Assert.That(PlayerDeals.CanPropose(s, npc, DealKind.FinalThree, null, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo(PlayerDeals.FinalTwoBindsRefusal));
            Assert.That(PlayerDeals.Available(s, npc), Does.Not.Contain(DealKind.FinalThree));
            Assert.That(PlayerDeals.Available(s, Npcs(s)[1]), Does.Contain(DealKind.FinalThree), "With anybody else it is still on the table.");

            var counter = Negotiation.CounterPrice(House(true, 5), npc, DealKind.FinalThree, null);
            Assert.That(counter, Is.Not.Null);
            Assert.That(counter.kind, Is.Not.EqualTo(DealKind.FinalTwo), "The price never binds more than the deal it buys.");
            Assert.That(Negotiation.CounterPrice(House(true, 5), npc, DealKind.SafetyAgreement, null).kind, Is.EqualTo(DealKind.FinalTwo),
                "Control: a safety pact's counter at the endgame is a final two.");
        }

        [Test]
        public void AFinalThreeOfferLapsesAtTheFinalThreeAndCannotBeTakenThere()
        {
            // Taken at the final three: refused, in the player's own proposal's words.
            var three = House(true, 3);
            three.deals.Add(Offer(three, Npcs(three)[0]));
            Valid(three);
            var refused = new EpisodeEngine(three).Apply(Respond(three, "deal-ask-3"));
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo(PlayerDeals.FinalThreeHereRefusal));

            // Still unanswered as the final three's Head of Household begins: it lapses, and is not kept.
            var after = Step(three);
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1));
            Assert.That(after.deals.Single(d => d.id == "deal-ask-3").status, Is.EqualTo(DealStatus.Expired));
            Assert.That(after.events.Any(e => e.text != null && e.text.Contains("final three deal")), Is.False, "No line for it.");

            // And as the final four's eviction turns the house to three.
            var four = FourLeft(EpisodePhase.Campaign, out string hoh, out string leaving, out string staying);
            four.deals.Add(Offer(four, staying));
            Valid(four);
            var engine = new EpisodeEngine(four);
            for (int i = 0; i < 40 && engine.Snapshot.phase != EpisodePhase.Social; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var turned = engine.Snapshot;
            Assert.That(turned.Active.Count(), Is.EqualTo(3), "The final four's eviction.");
            Assert.That(turned.deals.Single(d => d.id == "deal-ask-3").status, Is.EqualTo(DealStatus.Expired), "Nothing left for it to bind.");
            Assert.That(turned.randomState, Is.EqualTo(Without(four, "deal-ask-3", engine.Snapshot.revision - four.revision).randomState),
                "No draw for it.");
        }

        [Test]
        public void TheFinalThreeDealsWindowClosesOnceTheFinalFoursBlockIsSet()
        {
            // The final four's free time, after the final five's eviction: still open.
            var open = FourLeft(EpisodePhase.Social, out string hoh, out _, out string staying);
            open.evictionResolved = true; open.vetoResolved = true;
            Valid(open);
            Assert.That(NpcDeals.FinalFourBlockSet(open), Is.False);
            Assert.That(PlayerDeals.CanPropose(open, staying, DealKind.FinalThree, null, out string reason), Is.True, reason);

            // The final four's veto meeting over, the eviction still to come: closed, to the player and to the house.
            var shut = FourLeft(EpisodePhase.Campaign, out hoh, out _, out staying);
            Valid(shut);
            Assert.That(NpcDeals.FinalFourBlockSet(shut), Is.True);
            Assert.That(PlayerDeals.CanPropose(shut, staying, DealKind.FinalThree, null, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(PlayerDeals.FinalFourBlockSetRefusal));
            // The Head of Household, the one houseguest off the block, partners with the player at fifty.
            foreach (var a in shut.contestants) foreach (var b in shut.contestants) if (a.id != b.id) SetScore(shut, a.id, b.id, 0);
            SetScore(shut, hoh, shut.playerId, 50);
            shut.deals.Add(Deal("deal-partners", DealKind.Partnership, hoh, shut.playerId, week: shut.week, expires: 0));
            Assert.That(NpcDeals.Offer(shut, hoh, shut.playerId), Is.Not.EqualTo(DealKind.FinalThree), "Nobody puts one.");
            shut.vetoResolved = false;
            Assert.That(NpcDeals.Offer(shut, hoh, shut.playerId), Is.EqualTo(DealKind.FinalThree), "Control: before the veto meeting, a final three.");
            shut.vetoResolved = true;
            shut.deals.Add(Offer(shut, staying));
            Valid(shut);
            var taken = new EpisodeEngine(shut).Apply(Respond(shut, "deal-ask-3"));
            Assert.That(taken.accepted, Is.False, "An offer still standing is no yes to give.");
            Assert.That(taken.reason, Is.EqualTo(PlayerDeals.FinalFourBlockSetRefusal));

            // An offer standing as the final four's veto is decided lapses with the decision.
            var meeting = FourLeft(EpisodePhase.VetoMeeting, out hoh, out _, out staying);
            meeting.deals.Add(Offer(meeting, staying));
            Valid(meeting);
            var decided = new EpisodeEngine(meeting).Apply(new EpisodeCommand
            {
                id = "c9-veto", actorId = meeting.playerId, kind = EpisodeCommandKind.ResolveVeto, useVeto = false,
                expectedRevision = meeting.revision, expectedPhase = meeting.phase,
            });
            Assert.That(decided.accepted, Is.True, decided.reason);
            Assert.That(decided.state.deals.Single(d => d.id == "deal-ask-3").status, Is.EqualTo(DealStatus.Expired));
        }

        [Test]
        public void NamingTheirPartnerAsTheReplacementBreaksAFinalThreeDeal()
        {
            var s = ContentCatalog.Create(7);
            s.week = 4;
            s.strategyRulesStartWeek = 1;
            var npcs = Npcs(s);
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = s.playerId;
            s.nominees = new List<string> { npcs[0], npcs[1] };
            foreach (var nominee in s.nominees) { s.Find(nominee).nominationWeeks.Add(s.week); s.Find(nominee).timesNominated = 1; }
            s.vetoHolderId = npcs[2];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            SetScore(s, npcs[2], npcs[0], 100); SetScore(s, npcs[2], npcs[1], -100);
            string partner = npcs[3];
            s.deals.Add(Deal("deal-final-three", DealKind.FinalThree, s.playerId, partner, week: s.week, expires: 0));
            EpisodeEngine.EnableCommitments(s);
            Valid(s);
            Assert.That(EpisodeEngine.NpcVetoSave(s), Is.EqualTo(npcs[0]), "The holder saves the one they like.");
            var result = new EpisodeEngine(s).Apply(new EpisodeCommand
            {
                id = "c9-replace", actorId = s.playerId, kind = EpisodeCommandKind.ResolveVeto, useVeto = true,
                targetId = npcs[0], secondTargetId = partner, expectedRevision = s.revision, expectedPhase = s.phase,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            var deal = result.state.deals.Single(d => d.id == "deal-final-three");
            Assert.That(deal.status, Is.EqualTo(DealStatus.Broken), "Naming them is a nomination.");
            Assert.That(deal.brokenById, Is.EqualTo(s.playerId));
            Assert.That(FinalistRead.DealBreaker(result.state, deal), Is.EqualTo(s.playerId), "The finalist read names who broke it, by the record.");
            Assert.That(CommitmentsRead.Of(result.state).Single(c => c.id == "deal-final-three").status, Is.EqualTo("broken by you"));
        }

        [Test]
        public void YourWeekReadsAFinalThreeDealsLines()
        {
            Assert.That(YourWeek.DealWords(DealKind.FinalThree), Is.EqualTo("final three deal"));
            var s = House(true, 3);
            string partner = Npcs(s)[0];
            s.deals.Add(Deal("deal-final-three", DealKind.FinalThree, s.playerId, partner, expires: 0));
            Valid(s);
            var after = Step(s);
            var line = after.events.Last(e => e.kind == "deal-outcome" && e.text.Contains("final three deal")).text;
            Assert.That(YourWeek.ReadDeal(after, line, partner, out string actor, out string type, out bool kept), Is.True, line);
            Assert.That((actor, type, kept), Is.EqualTo(((string)null, DealKind.FinalThree, true)), "Both kept it.");
        }

        [Test]
        public void ValidationRefusesAFinalThreeDealStruckBeforeTheRulesBegan()
        {
            var s = House(false, 5);
            EpisodeEngine.EnableCommitments(s, 3);
            Assert.That(s.commitmentRulesStartWeek, Is.EqualTo(3));
            s.deals.Add(Deal("deal-final-three", DealKind.FinalThree, s.playerId, Npcs(s)[0], week: 2, expires: 0));
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False);
            Assert.That(error, Is.EqualTo("A season without the commitment rules has none of their records."));
            s.deals.Single().week = 3;
            Valid(s);
        }

        // ------------------------------------------------------------ fixtures

        /// <summary>
        /// The final four, week 4: the player, Maya (Head of Household), Taylor and Jamie on the block, the player
        /// holding the veto, Casey and Riley on the jury, under the commitment rules. At the veto meeting, or with the
        /// veto decided (unused) for the campaign, Taylor the one the player's vote will evict.
        /// </summary>
        private static EpisodeState FourLeft(EpisodePhase phase, out string hoh, out string leaving, out string staying)
        {
            var s = ContentCatalog.Create(7);
            s.week = 4;
            s.strategyRulesStartWeek = 1;
            foreach (var actor in s.contestants.Skip(4)) actor.status = ContestantStatus.Jury;
            var npcs = Npcs(s);
            hoh = npcs[0]; leaving = npcs[1]; staying = npcs[2];
            s.phase = phase;
            if (phase != EpisodePhase.Social)
            {
                s.hohId = hoh;
                s.nominees = new List<string> { leaving, staying };
                foreach (var nominee in s.nominees) { s.Find(nominee).nominationWeeks.Add(s.week); s.Find(nominee).timesNominated = 1; }
                s.vetoHolderId = s.playerId;
                s.vetoPlayers = s.Active.Select(c => c.id).ToList();
                s.vetoResolved = phase == EpisodePhase.Campaign;
            }
            EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>An offer of a final three deal from <paramref name="npcId"/>, still unanswered, filed this week.</summary>
        private static DealState Offer(EpisodeState s, string npcId)
        {
            var offer = Deal("deal-ask-3", DealKind.FinalThree, npcId, s.playerId, status: DealStatus.Proposed, week: s.week, expires: s.week);
            return offer;
        }

        private static EpisodeCommand Respond(EpisodeState s, string dealId) => new EpisodeCommand
        {
            id = "c9-respond-" + dealId, actorId = s.playerId, kind = EpisodeCommandKind.RespondToDeal, targetId = dealId,
            text = EpisodeEngine.AcceptDeal, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>The same plain steps from a copy without the deal: where the season's stream ends up without it.</summary>
        private static EpisodeState Without(EpisodeState s, string dealId, int steps)
        {
            var copy = s.Clone();
            copy.deals.RemoveAll(d => d.id == dealId);
            var engine = new EpisodeEngine(copy);
            for (int i = 0; i < steps; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            return engine.Snapshot;
        }

        /// <summary>
        /// The final eviction: the player, Maya (the final Head of Household) and Taylor in the house,
        /// Jamie, Casey and Riley on the jury. Nobody's traits, so the evaluator's personality term and
        /// every word are plain, and the same strategic stat, so the jury's respect is the same for all
        /// three; Maya's view of the player and of Taylor as given, the two finalists' of each other
        /// nothing; every juror warm on Maya and cold on both finalists, and either finalist cut would vote
        /// for Maya, so the jury term is the same either way.
        /// </summary>
        private static EpisodeState Final(bool rules, out string hoh, out string other, double viewOfPlayer = 10, double viewOfOther = 20)
        {
            var s = ContentCatalog.Create(337);
            s.week = 4;
            s.phase = EpisodePhase.FinalEviction;
            foreach (var actor in s.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            hoh = s.contestants[1].id;
            other = s.contestants[2].id;
            s.hohId = hoh;
            s.finalPart1WinnerId = hoh;
            s.finalPart2WinnerId = other;
            foreach (var finalist in s.contestants.Take(3)) { finalist.traits = new List<string>(); finalist.stats.strategic = 6; }
            SetScore(s, hoh, s.playerId, viewOfPlayer);
            SetScore(s, hoh, other, viewOfOther);
            SetScore(s, s.playerId, other, 0); SetScore(s, other, s.playerId, 0);
            foreach (var juror in s.contestants.Skip(3))
            {
                SetScore(s, juror.id, hoh, 100);
                SetScore(s, juror.id, s.playerId, -100);
                SetScore(s, juror.id, other, -100);
            }
            SetScore(s, s.playerId, hoh, 100);
            SetScore(s, other, hoh, 100);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// Sets the jury so that <paramref name="winner"/> beats the Head of Household beside them, and the
        /// Head of Household beats <paramref name="andHohOver"/>: every juror, and either finalist cut, as sure as the scores allow.
        /// </summary>
        private static void JuryFor(EpisodeState s, string winner, string over, string andHohOver)
        {
            foreach (var juror in s.contestants.Where(c => c.status == ContestantStatus.Jury))
            {
                SetScore(s, juror.id, winner, 100);
                SetScore(s, juror.id, over, 0);
                SetScore(s, juror.id, andHohOver, -100);
            }
            // Cut beside the winner, the other finalist votes for the winner; cut beside the other, the winner votes for the Head of Household.
            SetScore(s, andHohOver, winner, 100); SetScore(s, andHohOver, over, -100);
            SetScore(s, winner, over, 100); SetScore(s, winner, andHohOver, -100);
        }

        /// <summary>The engine's final choice without anything of C9's: the evaluator as the step built it before the rules.</summary>
        private static WebVoteEvaluation TheEvaluator(EpisodeState s)
        {
            var context = s.Clone();
            context.nominees = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            var options = WebEvictionVoting.FromNative(context, s.hohId);
            options.memories.Clear(); options.playerPersonaLabel = null;
            return WebEvictionVoting.Evaluate(options);
        }

        /// <summary>A house of <paramref name="left"/> in the week's free time, the rest of the eight on the jury.</summary>
        private static EpisodeState House(bool rules, int left)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 41);
            s.week = 4;
            foreach (var actor in s.contestants.Where(c => !c.isPlayer).Skip(left - 1)) actor.status = ContestantStatus.Jury;
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>A campaign with the player voting, a houseguest at the head of the house and two on the block (C1's).</summary>
        private static EpisodeState Campaign(bool rules)
        {
            var s = ContentCatalog.Create(7);
            s.strategyRulesStartWeek = 1;
            var npcs = Npcs(s);
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

        private static List<string> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static List<string> Finalists(EpisodeState s) => s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();

        private static AllianceState Pact(EpisodeState s, string id, string name, params string[] members)
        {
            var pact = new AllianceState { id = id, name = name, members = members.ToList(), active = true };
            s.alliances.Add(pact);
            s.ledger.alliances.Add(new AllianceRow { id = id, startedWeek = s.week, why = "player" });
            return pact;
        }

        private static DealState Deal(string id, string type, string from, string to, string status = DealStatus.Active, int week = 3, int expires = 3) =>
            new DealState
            {
                id = id, type = type, proposerId = from, recipientId = to, status = status,
                week = week, expiresWeek = expires, trustImpact = DealKind.DefaultTrust(type),
            };

        private static DealState Kept(DealState deal, int week)
        {
            deal.status = DealStatus.Fulfilled;
            deal.settledWeek = week;
            return deal;
        }

        private static double Factor(WebVoteEvaluation evaluation, string nomineeId, string code) =>
            evaluation.nomineeEvaluations.Single(n => n.nomineeId == nomineeId).factors.Where(f => f.code == code).Sum(f => f.value);

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>One plain step from here.</summary>
        private static EpisodeState Step(EpisodeState s)
        {
            var engine = new EpisodeEngine(s);
            var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
    }
}
