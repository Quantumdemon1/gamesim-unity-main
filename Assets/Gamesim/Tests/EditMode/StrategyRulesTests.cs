using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The strategy windows: who the player can reach before the week's decisions, what a plea is
    /// and what it moves, decisions that weigh deals, alliances and pleas, reply cards, and the two
    /// deal fixes - and that a season without the windows plays exactly as it did.
    /// </summary>
    public sealed class StrategyRulesTests
    {
        // ---------------------------------------------------------------- who can be reached

        [Test]
        public void NewSeasonsOpenTheWindowsAndTheFixtureSeasonsDoNot()
        {
            Assert.That(Season(5).strategyRulesStartWeek, Is.EqualTo(1));
            var fixture = ContentCatalog.Create(5);
            Assert.That(fixture.strategyRulesStartWeek, Is.Zero, "The default cast's pinned seasons play as they always did.");
            AtNomination(fixture);
            Assert.That(StrategyRules.Apply(fixture), Is.False);
            Assert.That(StrategyRules.Deciders(fixture), Is.Empty);
        }

        [Test]
        public void TheHeadOfHouseholdHasTimeForAWordBeforeNominationsAndNobodyElseDoes()
        {
            var s = AtNomination(Season(11));
            string hoh = s.hohId, other = Npcs(s).First(c => c.id != hoh).id;
            Assert.That(StrategyRules.Deciders(s), Is.EqualTo(new[] { hoh }));

            var talk = Run(s, EpisodeCommandKind.SmallTalk, hoh);
            Assert.That(talk.accepted, Is.True, talk.reason);
            Assert.That(talk.state.outOfPhaseSocialActions, Is.EqualTo(1), "A word before the ceremony is one of the week's conversations.");

            var elsewhere = Run(s, EpisodeCommandKind.SmallTalk, other);
            Assert.That(elsewhere.accepted, Is.False);
            Assert.That(elsewhere.reason, Does.Contain("only the Head of Household"));
            Assert.That(Run(s, EpisodeCommandKind.Eavesdrop).reason, Does.Contain("wait for free time"), "Listening in waits for free time.");
            Assert.That(Run(s, EpisodeCommandKind.SchemeAgainst, hoh).accepted, Is.False, "So does scheming.");

            var nominated = Run(s, EpisodeCommandKind.Advance);
            Assert.That(nominated.accepted, Is.True, nominated.reason);
            Assert.That(nominated.state.nominees, Has.Count.EqualTo(2));
            Assert.That(StrategyRules.Deciders(nominated.state), Is.Empty, "Once the names are called, the window is shut.");
            Assert.That(Run(nominated.state, EpisodeCommandKind.SmallTalk, hoh).accepted, Is.False);

            var old = AtNomination(Season(11)); old.strategyRulesStartWeek = 0;
            Assert.That(Run(old, EpisodeCommandKind.SmallTalk, old.hohId).accepted, Is.False, "An older season sends the player away, as it always did.");
        }

        [Test]
        public void TheVetoHolderAndTheHeadOfHouseholdHaveTimeBeforeTheMeeting()
        {
            var s = AtMeeting(Season(12));
            Assert.That(StrategyRules.Deciders(s), Is.EquivalentTo(new[] { s.vetoHolderId, s.hohId }));
            Assert.That(Run(s, EpisodeCommandKind.SmallTalk, s.vetoHolderId).accepted, Is.True);
            Assert.That(Run(s, EpisodeCommandKind.SmallTalk, s.nominees[0]).reason, Does.Contain("only the veto holder and the Head of Household"));

            var selfSave = AtMeeting(Season(12), holder: 1);
            Assert.That(StrategyRules.Deciders(selfSave), Is.EqualTo(new[] { selfSave.hohId }),
                "A nominated holder saves themselves: only the replacement is still open.");

            var playerHolds = AtMeeting(Season(12), holder: -1);
            Assert.That(StrategyRules.Deciders(playerHolds), Is.EqualTo(new[] { playerHolds.hohId }));

            var decided = AtMeeting(Season(12)); decided.vetoResolved = true;
            Assert.That(StrategyRules.Deciders(decided), Is.Empty);

            var four = AtMeeting(Season(12), holder: -1, active: 4);
            Assert.That(EpisodeEngine.VetoIsLockedAtFinalFour(four), Is.True);
            Assert.That(StrategyRules.Deciders(four), Is.Empty, "A veto that cannot be used has nothing to lobby.");
        }

        // ---------------------------------------------------------------- a plea

        [Test]
        public void APleasChanceIsTheReferencesLobbyingFormula()
        {
            var s = AtNomination(Season(13));
            var hoh = s.Find(s.hohId);
            hoh.traits = new List<string> { "Loyal", "Stubborn" };
            Set(s, hoh.id, s.playerId, 23);  // a fifth of 23 rounds to 5
            string spare = LobbyAsk.Spare, me = s.playerId;
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Emotional), Is.EqualTo(55 + 15 - 10 + 5));
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Strategic), Is.EqualTo(50 - 10 + 5));
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Deal), Is.EqualTo(60 - 10 - 10 + 5));
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Pressure), Is.EqualTo(35 - 10 + 5));

            Set(s, hoh.id, s.playerId, 12.5);
            hoh.traits = new List<string>();
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Emotional), Is.EqualTo(58),
                "Halves round up, as JavaScript rounds them, not to even.");

            s.deals.Add(Deal(me, hoh.id, DealKind.Partnership, "deal-p"));
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Emotional), Is.EqualTo(68), "A deal already between them.");
            s.nominees = new List<string> { me, Npcs(s)[1].id };
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Emotional), Is.EqualTo(78), "Pleading from the block.");
            s.nominees.Clear();

            Set(s, hoh.id, s.playerId, 100);
            hoh.traits = new List<string> { "Emotional", "Loyal", "Social", "Charming" };
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Emotional), Is.EqualTo(95), "Nothing is certain.");
            Set(s, hoh.id, s.playerId, -100);
            s.deals.Clear();
            hoh.traits = new List<string> { "Stubborn", "Confrontational", "Strategic", "Analytical" };
            Assert.That(StrategyRules.Chance(s, hoh.id, spare, me, LobbyApproach.Pressure), Is.EqualTo(5), "Nor hopeless.");
        }

        [Test]
        public void WhatThePleaAsksIsWeighedAsTheDealItResembles()
        {
            var s = AtMeeting(Season(14));
            var hoh = s.Find(s.hohId); hoh.traits = new List<string>();
            var holder = s.Find(s.vetoHolderId); holder.traits = new List<string>();
            Set(s, hoh.id, s.playerId, 0); Set(s, holder.id, s.playerId, 0);
            string x = EpisodeEngine.ReplacementCandidates(s).First(c => !c.isPlayer).id;

            Set(s, hoh.id, x, 0);
            Assert.That(StrategyRules.Chance(s, hoh.id, LobbyAsk.Target, x, LobbyApproach.Strategic), Is.EqualTo(50));
            Set(s, hoh.id, x, -30);
            Assert.That(StrategyRules.Chance(s, hoh.id, LobbyAsk.Target, x, LobbyApproach.Strategic), Is.EqualTo(70), "Somebody they already dislike.");
            Set(s, hoh.id, x, 40);
            Assert.That(StrategyRules.Chance(s, hoh.id, LobbyAsk.Target, x, LobbyApproach.Strategic), Is.EqualTo(20), "Somebody they like.");
            Set(s, hoh.id, x, 0);
            s.alliances.Add(new AllianceState { id = "a-x", name = "The X Pact", members = new List<string> { hoh.id, x } });
            Assert.That(StrategyRules.Chance(s, hoh.id, LobbyAsk.Target, x, LobbyApproach.Strategic), Is.EqualTo(10), "Their own ally.");
            s.alliances.Clear();

            string nominee = s.nominees[0];
            Set(s, holder.id, nominee, 60);
            Assert.That(StrategyRules.Chance(s, holder.id, LobbyAsk.Save, nominee, LobbyApproach.Strategic), Is.EqualTo(70));
            Set(s, holder.id, nominee, -5);
            Assert.That(StrategyRules.Chance(s, holder.id, LobbyAsk.Save, nominee, LobbyApproach.Strategic), Is.EqualTo(30));

            Assert.That(StrategyRules.Chance(s, holder.id, LobbyAsk.Keep, null, LobbyApproach.Strategic), Is.EqualTo(50));
            s.alliances.Add(new AllianceState { id = "a-n", name = "The N Pact", members = new List<string> { holder.id, nominee } });
            Assert.That(StrategyRules.Chance(s, holder.id, LobbyAsk.Keep, null, LobbyApproach.Strategic), Is.EqualTo(20),
                "Asking a holder to leave their own ally on the block.");
        }

        [Test]
        public void TheFourAnswersAreTheReferencesBands()
        {
            int sized = 0;
            Func<double> size = () => { sized++; return .5; };
            Check(StrategyRules.Respond(60, .30, size), LobbyResponse.Receptive, 50, 5);
            Check(StrategyRules.Respond(60, .359, size), LobbyResponse.Receptive, 50, 5);
            Assert.That(sized, Is.EqualTo(2));
            Check(StrategyRules.Respond(60, .36, size), LobbyResponse.Open, 15, 3);
            Check(StrategyRules.Respond(60, .599, size), LobbyResponse.Open, 15, 3);
            Check(StrategyRules.Respond(60, .60, size), LobbyResponse.Skeptical, -5, 0);
            Check(StrategyRules.Respond(60, .799, size), LobbyResponse.Skeptical, -5, 0);
            Assert.That(sized, Is.EqualTo(2), "An open or skeptical answer is not sized.");
            Check(StrategyRules.Respond(60, .80, size), LobbyResponse.Hostile, -40, -8);
            Assert.That(sized, Is.EqualTo(3));
            Check(StrategyRules.Respond(60, .10, () => 0), LobbyResponse.Receptive, 40, 5);
            Check(StrategyRules.Respond(60, .10, () => 1 - 1e-12), LobbyResponse.Receptive, 60, 5);
            Check(StrategyRules.Respond(60, .99, () => 1 - 1e-12), LobbyResponse.Hostile, -50, -8);
            Assert.That(StrategyRules.Landed(LobbyResponse.Receptive) && StrategyRules.Landed(LobbyResponse.Open), Is.True);
            Assert.That(StrategyRules.Landed(LobbyResponse.Skeptical) || StrategyRules.Landed(LobbyResponse.Hostile), Is.False);
        }

        [Test]
        public void APleaCostsAConversationIsRememberedAndIsHeardOnce()
        {
            var s = AtNomination(Season(15));
            string hoh = s.hohId, name = s.Find(hoh).name;
            var result = Run(s, EpisodeCommandKind.Lobby, hoh, s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Emotional));
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            var plea = after.lobbies.Single();
            Assert.That((plea.week, plea.phase, plea.deciderId, plea.ask, plea.subjectId, plea.approach),
                Is.EqualTo((s.week, EpisodePhase.Nomination, hoh, LobbyAsk.Spare, s.playerId, LobbyApproach.Emotional)));
            Assert.That(InBand(plea.response, plea.influence), Is.True, plea.response + " " + plea.influence);
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(1));
            Assert.That(after.memories.Any(m => m.ownerId == hoh && m.subjectId == s.playerId
                && m.text == "Asked me to leave you off the block in week " + s.week + ". I was " + plea.response + "."), Is.True);
            var line = after.events.Last(e => e.kind == "lobby");
            Assert.That(line.text, Does.StartWith("You asked " + name + " to leave you off the block: “Please don't put me up."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, hoh }), "Between the two of them.");

            var again = Run(after, EpisodeCommandKind.Lobby, hoh, s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Strategic));
            Assert.That(again.accepted, Is.False);
            Assert.That(again.reason, Does.Contain("already heard you out"));
            Assert.That(Run(s, EpisodeCommandKind.Lobby, hoh, s.playerId, "spare").reason, Does.Contain("how to ask"));
            Assert.That(Run(s, EpisodeCommandKind.Lobby, hoh, hoh, LobbyAsk.Encode(LobbyAsk.Target, LobbyApproach.Deal)).accepted, Is.False,
                "Nobody nominates themselves.");
            Assert.That(Run(s, EpisodeCommandKind.Lobby, hoh, Npcs(s).Last().id, LobbyAsk.Encode(LobbyAsk.Save, LobbyApproach.Deal)).accepted, Is.False,
                "There is no veto to ask for before nominations.");
            var spent = s.Clone(); spent.outOfPhaseSocialActions = EpisodeEngine.SocialActionBudget(spent);
            Assert.That(Run(spent, EpisodeCommandKind.Lobby, hoh, s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Emotional)).reason,
                Does.Contain("no conversations left"));
            var old = s.Clone(); old.strategyRulesStartWeek = 0;
            Assert.That(Run(old, EpisodeCommandKind.Lobby, hoh, s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Emotional)).accepted, Is.False);
        }

        [Test]
        public void APleaMadeWithADealBindsThePlayerWhenItLands()
        {
            bool landed = false, refused = false;
            for (uint roll = 1; roll < 400 && !(landed && refused); roll++)
            {
                var s = AtNomination(Season(16));
                s.randomState = roll * 2654435761u;
                var after = Run(s, EpisodeCommandKind.Lobby, s.hohId, s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Deal)).state;
                var plea = after.lobbies.Single();
                var deal = after.deals.SingleOrDefault(d => d.id.StartsWith("deal-lobby-", StringComparison.Ordinal));
                if (StrategyRules.Landed(plea.response))
                {
                    landed = true;
                    Assert.That(deal, Is.Not.Null, "A deal made in a plea that landed is a deal.");
                    Assert.That((deal.type, deal.proposerId, deal.recipientId, deal.status, deal.expiresWeek),
                        Is.EqualTo((DealKind.SafetyAgreement, s.playerId, s.hohId, DealStatus.Active, s.week + 1)), "It binds the player next week too.");
                }
                else
                {
                    refused = true;
                    Assert.That(deal, Is.Null, "One that did not land made nothing.");
                }
            }
            Assert.That(landed && refused, Is.True, "Both answers came up.");
            var emotional = AtNomination(Season(16));
            for (uint roll = 1; roll < 60; roll++)
            {
                emotional.randomState = roll * 2654435761u;
                var after = Run(emotional, EpisodeCommandKind.Lobby, emotional.hohId, emotional.playerId,
                    LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Emotional)).state;
                Assert.That(after.deals.Any(d => d.id.StartsWith("deal-lobby-", StringComparison.Ordinal)), Is.False, "Only a deal makes a deal.");
            }
        }

        // ---------------------------------------------------------------- decisions that listen

        [Test]
        public void APleaThatLandsKeepsThePlayerOffTheBlockAndAHostileOneBackfires()
        {
            var s = AtNomination(Season(17));
            var others = Npcs(s).Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            foreach (var id in others) Set(s, s.hohId, id, 50);
            Set(s, s.hohId, s.playerId, -20);
            Set(s, s.hohId, others[0], 0);
            Set(s, s.hohId, others[1], 10);
            Assert.That(Nominees(s), Does.Contain(s.playerId), "Without a word, the least liked goes up.");
            var spared = s.Clone(); spared.lobbies.Add(Plea(s, s.hohId, LobbyAsk.Spare, s.playerId, 50));
            Assert.That(Nominees(spared), Is.EquivalentTo(new[] { others[0], others[1] }));

            var safe = s.Clone(); Set(safe, safe.hohId, safe.playerId, 30);
            Assert.That(Nominees(safe), Does.Not.Contain(safe.playerId));
            safe.lobbies.Add(Plea(safe, safe.hohId, LobbyAsk.Spare, safe.playerId, -40));
            Assert.That(Nominees(safe), Does.Contain(safe.playerId), "Pushed away, the Head of Household puts the player up.");

            var pushed = s.Clone(); pushed.lobbies.Add(Plea(pushed, pushed.hohId, LobbyAsk.Target, others[2], 70));
            Assert.That(Nominees(pushed), Does.Contain(others[2]), "A plea that lands puts its target up.");
            var shielded = s.Clone(); shielded.lobbies.Add(Plea(shielded, shielded.hohId, LobbyAsk.Target, others[0], -40));
            Assert.That(Nominees(shielded), Does.Not.Contain(others[0]), "A hostile one protects them.");

            var old = spared.Clone(); old.strategyRulesStartWeek = 0; old.lobbies.Clear();
            Assert.That(Nominees(old), Does.Contain(s.playerId));
        }

        [Test]
        public void DealsAndAlliancesHoldTheHeadOfHouseholdBackAndATargetPullsThemOn()
        {
            var s = AtNomination(Season(18));
            var others = Npcs(s).Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            foreach (var id in others) Set(s, s.hohId, id, 50);
            Set(s, s.hohId, s.playerId, 50);
            Set(s, s.hohId, others[0], -20);
            Set(s, s.hohId, others[1], 0);
            Set(s, s.hohId, others[2], 10);
            Assert.That(Nominees(s), Is.EquivalentTo(new[] { others[0], others[1] }));

            var safety = s.Clone(); safety.deals.Add(Deal(safety.hohId, others[0], DealKind.SafetyAgreement, "deal-s"));
            Assert.That(Nominees(safety), Is.EquivalentTo(new[] { others[1], others[2] }), "A safety agreement is 35 points of reluctance.");

            var allied = s.Clone();
            allied.alliances.Add(new AllianceState { id = "a-1", name = "The One Pact", members = new List<string> { allied.hohId, others[1] } });
            Assert.That(Nominees(allied), Is.EquivalentTo(new[] { others[0], others[2] }), "Nobody puts an ally up lightly.");

            var broken = s.Clone();
            var betrayal = Deal(broken.hohId, others[3], DealKind.Partnership, "deal-b"); betrayal.status = DealStatus.Broken;
            broken.deals.Add(betrayal);
            Set(broken, broken.hohId, others[3], 30);
            Assert.That(Nominees(broken), Is.EquivalentTo(new[] { others[0], others[3] }), "A broken deal counts against them.");

            var target = s.Clone();
            var agreement = Deal(target.hohId, others[4], DealKind.TargetAgreement, "deal-t"); agreement.targetId = others[3];
            target.deals.Add(agreement);
            Set(target, target.hohId, others[3], 20);
            Assert.That(Nominees(target), Does.Contain(others[3]), "The Head of Household follows a target agreement.");

            var old = safety.Clone(); old.strategyRulesStartWeek = 0;
            Assert.That(Nominees(old), Is.EquivalentTo(new[] { others[0], others[1] }), "An older season reads warmth alone.");
            Assert.That(StrategyRules.DealWeight(DealKind.FinalTwo), Is.EqualTo(50));
            Assert.That(StrategyRules.DealWeight(DealKind.VoteSave), Is.Zero);
        }

        [Test]
        public void TheVetoHolderHonoursACommitmentAnAllyAndThePleas()
        {
            var s = AtMeeting(Season(19));
            string holder = s.vetoHolderId, first = s.nominees[0], second = s.nominees[1];
            Set(s, holder, first, 10); Set(s, holder, second, 20);
            Assert.That(EpisodeEngine.NpcVetoSave(s), Is.Null, "Nobody is warm enough to save.");

            var committed = s.Clone(); committed.deals.Add(Deal(first, holder, DealKind.VetoUse, "deal-v"));
            Assert.That(EpisodeEngine.NpcVetoSave(committed), Is.EqualTo(first), "A veto commitment is honoured.");

            var pleaded = s.Clone(); pleaded.lobbies.Add(Plea(pleaded, holder, LobbyAsk.Save, second, 15, EpisodePhase.VetoMeeting));
            Assert.That(EpisodeEngine.NpcVetoSave(pleaded), Is.EqualTo(second), "Twenty and a plea's fifteen pass thirty.");

            var ally = s.Clone(); Set(ally, holder, first, 11);
            ally.alliances.Add(new AllianceState { id = "a-2", name = "The Two Pact", members = new List<string> { holder, first } });
            Assert.That(EpisodeEngine.NpcVetoSave(ally), Is.EqualTo(first), "Eleven and an ally's twenty pass thirty.");
            Set(ally, holder, first, 10);
            Assert.That(EpisodeEngine.NpcVetoSave(ally), Is.Null, "Thirty is not past thirty.");

            var kept = s.Clone(); Set(kept, holder, second, 60);
            Assert.That(EpisodeEngine.NpcVetoSave(kept), Is.EqualTo(second));
            kept.lobbies.Add(Plea(kept, holder, LobbyAsk.Keep, null, 50, EpisodePhase.VetoMeeting));
            Assert.That(EpisodeEngine.NpcVetoSave(kept), Is.Null, "Asked to leave it, they need eighty.");

            var old = committed.Clone(); old.strategyRulesStartWeek = 0;
            Assert.That(EpisodeEngine.NpcVetoSave(old), Is.Null, "An older season reads warmth against thirty alone.");
        }

        [Test]
        public void TheReplacementIsChosenTheSameWay()
        {
            var s = AtMeeting(Season(20));
            Set(s, s.vetoHolderId, s.nominees[0], 80);
            var candidates = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
            foreach (var id in candidates) Set(s, s.hohId, id, 50);
            Set(s, s.hohId, candidates[0], 0);
            Assert.That(Replacement(s), Is.EqualTo(candidates[0]));
            var named = s.Clone(); named.lobbies.Add(Plea(named, named.hohId, LobbyAsk.Target, candidates[1], 60, EpisodePhase.VetoMeeting));
            Assert.That(Replacement(named), Is.EqualTo(candidates[1]), "Asked to name somebody, the Head of Household can.");
            var spared = s.Clone(); spared.lobbies.Add(Plea(spared, spared.hohId, LobbyAsk.Spare, candidates[0], 60, EpisodePhase.VetoMeeting));
            Assert.That(Replacement(spared), Is.Not.EqualTo(candidates[0]));
        }

        // ---------------------------------------------------------------- the deal fixes

        [Test]
        public void NobodyAsksForTheVetoOnceItIsDecided()
        {
            var s = AtMeeting(Season(21));
            string holder = s.vetoHolderId, nominee = s.nominees[0];
            Set(s, nominee, holder, 30);
            Assert.That(NpcDeals.Offer(s, nominee, holder), Is.EqualTo(DealKind.VetoUse));
            s.vetoResolved = true;
            Assert.That(NpcDeals.Offer(s, nominee, holder), Is.Not.EqualTo(DealKind.VetoUse), "Not after the meeting.");
            var old = s.Clone(); old.strategyRulesStartWeek = 0;
            Assert.That(NpcDeals.Offer(old, nominee, holder), Is.EqualTo(DealKind.VetoUse), "An older season still asks, as it always did.");

            Assert.That(PlayerDeals.CanPropose(s, holder, DealKind.VetoUse, null, out var reason), Is.False);
            Assert.That(reason, Does.Contain("already been decided"));
            Assert.That(PlayerDeals.CanPropose(old, holder, DealKind.VetoUse, null, out _), Is.True);
            s.vetoResolved = false;
            Assert.That(PlayerDeals.CanPropose(s, holder, DealKind.VetoUse, null, out _), Is.True, "Before the meeting it is the question that matters.");
        }

        [Test]
        public void NomineesAskThePlayerForTheVetoWhileItIsStillTheirs()
        {
            var s = AtVetoWon(Season(22));
            string warm = s.nominees[0], cold = s.nominees[1];
            Set(s, warm, s.playerId, 30); Set(s, cold, s.playerId, 0);
            var meeting = Run(s, EpisodeCommandKind.Advance);
            Assert.That(meeting.accepted, Is.True, meeting.reason);
            var asked = meeting.state.deals.Where(d => d.id.StartsWith("deal-veto-", StringComparison.Ordinal)).ToList();
            Assert.That(asked.Select(d => (d.proposerId, d.recipientId, d.type, d.status)),
                Is.EqualTo(new[] { (warm, s.playerId, DealKind.VetoUse, DealStatus.Proposed) }), "The warm nominee asks; the cold one does not bargain.");
            Assert.That(meeting.state.events.Any(e => e.kind == "deal" && e.text.Contains("wants your word on the veto")), Is.True);

            var declined = Run(meeting.state, EpisodeCommandKind.ResolveVeto).state;
            Assert.That(declined.deals.Single(d => d.id == asked[0].id).status, Is.EqualTo(DealStatus.Expired),
                "A question about a decision already taken is off the table.");

            var old = AtVetoWon(Season(22)); old.strategyRulesStartWeek = 0;
            Set(old, warm, old.playerId, 30);
            Assert.That(Run(old, EpisodeCommandKind.Advance).state.deals.Any(d => d.id.StartsWith("deal-veto-", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void AnAcceptedAllianceInvitationMakesAnAlliance()
        {
            var s = Season(23);
            var npcs = Npcs(s);
            string inviter = npcs[0].id, friend = npcs[1].id;
            s.deals.Add(Offer(inviter, s.playerId, DealKind.AllianceInvite, "deal-ask-9", s.week));
            var formed = Run(s, EpisodeCommandKind.RespondToDeal, "deal-ask-9", text: EpisodeEngine.AcceptDeal).state;
            Assert.That(formed.Allied(formed.playerId, inviter), Is.True, "Accepting an invitation allies the player.");
            Assert.That(formed.alliances.Single(a => a.active && a.members.Contains(formed.playerId)).members, Has.Count.EqualTo(2));

            var theirs = s.Clone();
            theirs.alliances.Add(new AllianceState { id = "alliance-npc", name = "The Inside Pact", members = new List<string> { inviter, friend } });
            Set(theirs, friend, theirs.playerId, 0); Set(theirs, theirs.playerId, friend, 0);
            var joined = Run(theirs, EpisodeCommandKind.RespondToDeal, "deal-ask-9", text: EpisodeEngine.AcceptDeal).state;
            Assert.That(joined.alliances.Single(a => a.id == "alliance-npc").members, Is.EqualTo(new[] { inviter, friend, joined.playerId }),
                "They bring the player into their own.");
            Assert.That(joined.events.Any(e => e.text == joined.Find(inviter).name + " brought you into The Inside Pact."), Is.True);

            var hostile = theirs.Clone(); Set(hostile, friend, hostile.playerId, -11);
            var paired = Run(hostile, EpisodeCommandKind.RespondToDeal, "deal-ask-9", text: EpisodeEngine.AcceptDeal).state;
            Assert.That(paired.alliances.Single(a => a.id == "alliance-npc").members, Has.No.Member(paired.playerId),
                "Somebody in it will not have the player.");
            Assert.That(paired.Allied(paired.playerId, inviter), Is.True, "So the two of them start their own.");

            var old = s.Clone(); old.strategyRulesStartWeek = 0;
            Assert.That(Run(old, EpisodeCommandKind.RespondToDeal, "deal-ask-9", text: EpisodeEngine.AcceptDeal).state.Allied(old.playerId, inviter), Is.False,
                "An older season keeps the invitation a deal, as it was.");
        }

        [Test]
        public void AnInvitationThePlayerMakesAllyThemWhenItIsAccepted()
        {
            bool accepted = false;
            for (uint roll = 1; roll < 200 && !accepted; roll++)
            {
                var s = Season(24);
                string npc = Npcs(s)[0].id;
                Set(s, npc, s.playerId, 90); Set(s, s.playerId, npc, 90);
                s.randomState = roll * 2654435761u;
                var after = Run(s, EpisodeCommandKind.ProposeDeal, npc, text: DealKind.AllianceInvite).state;
                bool agreed = after.deals.Any(d => d.type == DealKind.AllianceInvite && d.status == DealStatus.Active);
                Assert.That(after.Allied(after.playerId, npc), Is.EqualTo(agreed));
                accepted |= agreed;
            }
            Assert.That(accepted, Is.True);
        }

        // ---------------------------------------------------------------- reply cards

        [Test]
        public void AConfrontationPutsACardInFrontOfThePlayer()
        {
            var s = Season(25);
            var npc = Npcs(s)[0].id;
            foreach (var other in s.Active.Where(c => c.id != npc)) Set(s, npc, other.id, 50);
            Set(s, npc, s.playerId, -30);
            Assert.That(NpcSocialActions.Perform(s, npc, NpcActionKind.Confront), Is.True);
            var card = s.replyCards.Single();
            Assert.That((card.kind, card.fromId, card.aboutId, card.week), Is.EqualTo((ReplyCards.Confrontation, npc, (string)null, s.week)));
            Assert.That(ReplyCards.Title(s, card), Is.EqualTo(s.Find(npc).name + " is confronting you!"));
            Assert.That(NpcSocialActions.Perform(s, npc, NpcActionKind.Confront), Is.True);
            Assert.That(s.replyCards, Has.Count.EqualTo(1), "One card per houseguest per kind at a time.");

            var old = Season(25); old.strategyRulesStartWeek = 0;
            foreach (var other in old.Active.Where(c => c.id != npc)) Set(old, npc, other.id, 50);
            Set(old, npc, old.playerId, -30);
            NpcSocialActions.Perform(old, npc, NpcActionKind.Confront);
            Assert.That(old.replyCards, Is.Empty);
        }

        [Test]
        public void NomineesPleadingWithThePlayerPutCardsInFrontOfThem()
        {
            var s = AtCampaign(Season(26));
            NpcSocialActions.Campaign(s);
            var cards = s.replyCards.OrderBy(c => c.fromId, StringComparer.Ordinal).ToList();
            var nominees = s.nominees.OrderBy(id => id, StringComparer.Ordinal).ToList();
            Assert.That(cards.Select(c => (c.kind, c.fromId)), Is.EqualTo(nominees.Select(id => (ReplyCards.Plea, id))));
            Assert.That(cards.All(c => c.aboutId == s.nominees.Single(id => id != c.fromId)), Is.True, "Each is about the other nominee.");
        }

        [Test]
        public void GossipAboutThePlayerSometimesReachesThem()
        {
            int found = 0, missed = 0, toldThem = 0;
            for (uint roll = 1; roll < 800 && (found < 3 || missed < 3 || toldThem < 20); roll++)
            {
                var s = Gossiping(roll, out string gossip);
                var old = s.Clone(); old.strategyRulesStartWeek = 0;
                NpcSocialActions.Perform(s, gossip, NpcActionKind.SpreadInfo);
                NpcSocialActions.Perform(old, gossip, NpcActionKind.SpreadInfo);
                Assert.That(old.replyCards, Is.Empty, "An older season never finds out.");
                // Told to the player, the rumour was about somebody else, and is not the player's to answer.
                if (s.events.Any(e => e.kind == "information")) { toldThem++; Assert.That(s.replyCards, Is.Empty); continue; }
                Assert.That(s.randomState, Is.Not.EqualTo(old.randomState), "The discovery roll is drawn only where the windows are.");
                var card = s.replyCards.SingleOrDefault();
                if (card == null) { missed++; continue; }
                found++;
                Assert.That((card.kind, card.fromId), Is.EqualTo((ReplyCards.Gossip, gossip)));
                Assert.That(s.Find(card.aboutId).isPlayer, Is.False, "It names who they were talking to.");
                Assert.That(ReplyCards.Message(s, card), Does.Contain("talking behind your back to " + s.Find(card.aboutId).name));
            }
            Assert.That(found, Is.GreaterThan(0));
            Assert.That(missed, Is.GreaterThan(found), "Three times in ten, not every time.");
            Assert.That(toldThem, Is.GreaterThanOrEqualTo(20), "Enough rumours about somebody else were told to the player to be sure none asks for an answer.");
        }

        [TestCase(ReplyCards.Confrontation, "apologize", 1)]
        [TestCase(ReplyCards.Confrontation, "deflect", -1)]
        [TestCase(ReplyCards.Confrontation, "escalate", -1)]
        [TestCase(ReplyCards.Gossip, "confront", -1)]
        [TestCase(ReplyCards.Gossip, "slide", -1)]
        [TestCase(ReplyCards.Gossip, "gossip-back", -1)]
        [TestCase(ReplyCards.Plea, "promise", 1)]
        [TestCase(ReplyCards.Plea, "noncommittal", 0)]
        [TestCase(ReplyCards.Plea, "refuse", -1)]
        public void AnsweringACardMovesThemAndIsRemembered(string kind, string key, int direction)
        {
            var s = kind == ReplyCards.Plea ? AtCampaign(Season(27)) : Season(27);
            string from = kind == ReplyCards.Plea ? s.nominees[0] : Npcs(s)[0].id;
            string about = kind == ReplyCards.Plea ? s.nominees[1] : kind == ReplyCards.Gossip ? Npcs(s).First(c => c.id != from).id : null;
            Set(s, s.playerId, from, 0); Set(s, from, s.playerId, 0);
            if (about != null) Set(s, about, from, 0);
            s.replyCards.Add(new ReplyCardState { id = "reply-1", week = s.week, kind = kind, fromId = from, aboutId = about });

            var result = Run(s, EpisodeCommandKind.ReplyToHouseguest, "reply-1", text: key);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.replyCards, Is.Empty, "Answered, the card is gone.");
            Assert.That(Math.Sign(after.Score(after.playerId, from)), Is.EqualTo(direction));
            Assert.That(after.memories.Any(m => m.ownerId == from && m.subjectId == after.playerId && m.text == ReplyCards.Memory(kind, key, s.week)), Is.True);
            Assert.That(after.events.Last(e => e.kind == "reply").text, Is.EqualTo(ReplyCards.Outcome(after, s.replyCards[0], ReplyCards.Find(kind, key))));
            Assert.That(after.socialActions + after.outOfPhaseSocialActions, Is.Zero, "Answering is free.");
            if (key == "gossip-back")
                Assert.That(after.Score(about, from), Is.EqualTo(ReplyCards.GossipBackReach), "Their listener hears the player's side.");
            bool promised = after.promises.Any(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.Vote
                && p.fromId == after.playerId && p.toId == from && p.targetId == about);
            Assert.That(promised, Is.EqualTo(key == "promise"), "Promising support is a vote promise, and only that is.");
        }

        [Test]
        public void ACardTakesOnlyItsOwnAnswersAndLastsOnlyItsPhase()
        {
            var s = Season(28);
            string from = Npcs(s)[0].id;
            s.replyCards.Add(new ReplyCardState { id = "reply-1", week = s.week, kind = ReplyCards.Confrontation, fromId = from });
            Assert.That(Run(s, EpisodeCommandKind.ReplyToHouseguest, "reply-1", text: "promise").accepted, Is.False, "A plea's answer to a confrontation.");
            Assert.That(Run(s, EpisodeCommandKind.ReplyToHouseguest, "reply-2", text: "apologize").reason, Does.Contain("passed"));
            var next = Run(s, EpisodeCommandKind.Advance);
            Assert.That(next.accepted, Is.True, next.reason);
            Assert.That(next.state.replyCards, Is.Empty, "Free time over, the moment has passed.");

            var campaign = AtCampaign(Season(28));
            campaign.replyCards.Add(new ReplyCardState { id = "reply-1", week = campaign.week, kind = ReplyCards.Plea, fromId = campaign.nominees[0], aboutId = campaign.nominees[1] });
            var night = Run(campaign, EpisodeCommandKind.Advance);
            Assert.That(night.accepted, Is.True, night.reason);
            Assert.That(night.state.replyCards, Is.Empty, "Campaigning closed, so has the plea.");
        }

        // ---------------------------------------------------------------- the save

        [TestCase("week")]
        [TestCase("phase")]
        [TestCase("player")]
        [TestCase("ask")]
        [TestCase("approach")]
        [TestCase("response")]
        [TestCase("influence")]
        [TestCase("card-phase")]
        [TestCase("card-twice")]
        [TestCase("card-kind")]
        [TestCase("card-self")]
        [TestCase("no-windows")]
        public void ASaveHoldsOnlyThisWeeksPleasAndCards(string damage)
        {
            var s = AtNomination(Season(29));
            var plea = Plea(s, s.hohId, LobbyAsk.Spare, s.playerId, 20);
            s.lobbies.Add(plea);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            var card = new ReplyCardState { id = "reply-1", week = s.week, kind = ReplyCards.Confrontation, fromId = Npcs(s)[1].id };
            var social = s.Clone(); social.phase = EpisodePhase.Social; social.replyCards.Add(card.Clone());
            Assert.That(EpisodeValidation.TryValidate(social, out error), Is.True, "A card in free time: " + error);
            switch (damage)
            {
                case "week": plea.week = s.week + 1; break;
                case "phase": plea.phase = EpisodePhase.Social; break;
                case "player": plea.deciderId = s.playerId; break;
                case "ask": plea.ask = "beg"; break;
                case "approach": plea.approach = "bribe"; break;
                case "response": plea.response = "delighted"; break;
                case "influence": plea.influence = 101; break;
                case "card-phase": s.replyCards.Add(card); break;
                case "card-twice": s.phase = EpisodePhase.Social; s.replyCards.Add(card); s.replyCards.Add(card.Clone()); break;
                case "card-kind": s.phase = EpisodePhase.Social; card.kind = "hug"; s.replyCards.Add(card); break;
                case "card-self": s.phase = EpisodePhase.Social; card.fromId = s.playerId; s.replyCards.Add(card); break;
                case "no-windows": s.strategyRulesStartWeek = 0; break;
            }
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False, damage);
        }

        [Test]
        public void TheWeekTurningForgetsThePleas()
        {
            var s = Season(30);
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            s.hohId = Npcs(s)[0].id;
            s.lobbies.Add(new LobbyState { week = s.week, phase = EpisodePhase.Nomination, deciderId = s.hohId, ask = LobbyAsk.Spare,
                subjectId = s.playerId, approach = LobbyApproach.Emotional, response = LobbyResponse.Receptive, influence = 50 });
            var next = Run(s, EpisodeCommandKind.Advance);
            Assert.That(next.accepted, Is.True, next.reason);
            Assert.That(next.state.week, Is.EqualTo(s.week + 1));
            Assert.That(next.state.lobbies, Is.Empty, "Last week's pleas were about last week's decisions.");
        }

        [Test]
        public void WhoeverIsDecidingHasTimeForThePlayer()
        {
            var s = AtNomination(Season(31));
            string hoh = s.hohId, other = Npcs(s).First(c => c.id != hoh).id;
            Assert.That(HouseDialogue.Greeting(s, hoh), Does.Not.Contain("ceremony").And.Not.Contain("after"));
            Assert.That(HouseDialogue.Greeting(s, other), Is.Not.EqualTo(HouseDialogue.Greeting(s, hoh)),
                "Everybody else still has a ceremony to get through.");
        }

        // ---------------------------------------------------------------- helpers

        private static EpisodeState Season(uint seed, int house = 8) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = house }, seed);

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        private static EpisodeState AtNomination(EpisodeState s)
        {
            s.phase = EpisodePhase.Nomination;
            s.hohId = Npcs(s)[0].id;
            s.nominees.Clear();
            return s;
        }

        /// <summary>
        /// The veto meeting: the first NPC is Head of Household and the next two are nominated. The
        /// holder is the fourth NPC, or the nominee at <paramref name="holder"/> 0 or 1, or the player at -1.
        /// </summary>
        private static EpisodeState AtMeeting(EpisodeState s, int holder = 3, int active = 0)
        {
            if (active > 0) foreach (var extra in Npcs(s).Skip(active - 1)) extra.status = ContestantStatus.Evicted;
            var npcs = Npcs(s).Select(c => c.id).ToList();
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = holder == -1 ? s.playerId : holder < 2 ? s.nominees[holder] : npcs[holder];
            int seats = EpisodeEngine.VetoPlayerCount(s.Active.Count());
            s.vetoPlayers = new[] { s.hohId, s.nominees[0], s.nominees[1], s.vetoHolderId }
                .Concat(s.Active.Select(c => c.id)).Distinct().Take(seats).ToList();
            s.vetoResolved = false;
            return s;
        }

        /// <summary>The veto competition just won by the player, with the meeting still to come.</summary>
        private static EpisodeState AtVetoWon(EpisodeState s)
        {
            AtMeeting(s, holder: -1);
            s.phase = EpisodePhase.Veto;
            s.competitionResolved = true;
            s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
            return s;
        }

        /// <summary>Campaigning, with the player holding the veto they did not use, so both nominees come to them.</summary>
        private static EpisodeState AtCampaign(EpisodeState s)
        {
            AtMeeting(s, holder: -1);
            s.phase = EpisodePhase.Campaign;
            s.vetoResolved = true;
            return s;
        }

        /// <summary>A house where one houseguest's biggest worry is the player, and a roll to vary who hears about it.</summary>
        private static EpisodeState Gossiping(uint roll, out string gossip)
        {
            var s = Season(32);
            s.phase = EpisodePhase.Social;
            var player = s.Find(s.playerId);
            player.hohWins = 4; player.vetoWins = 4;
            gossip = Npcs(s)[0].id;
            s.randomState = roll * 2654435761u;
            return s;
        }

        private static CommandResult Run(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeEngine(s).Apply(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = s.playerId, expectedPhase = s.phase, expectedRevision = s.revision,
                kind = kind, targetId = target, secondTargetId = second, text = text,
            });

        private static List<string> Nominees(EpisodeState s)
        {
            var result = Run(s, EpisodeCommandKind.Advance);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state.nominees;
        }

        private static string Replacement(EpisodeState s)
        {
            var result = Run(s, EpisodeCommandKind.Advance);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state.nominees.Single(id => !s.nominees.Contains(id));
        }

        private static LobbyState Plea(EpisodeState s, string decider, string ask, string subject, double influence,
            EpisodePhase phase = EpisodePhase.Nomination) => new LobbyState
        {
            week = s.week, phase = phase, deciderId = decider, ask = ask, subjectId = subject,
            approach = LobbyApproach.Emotional, response = influence >= 40 ? LobbyResponse.Receptive
                : influence > 0 ? LobbyResponse.Open : influence > -30 ? LobbyResponse.Skeptical : LobbyResponse.Hostile,
            influence = influence,
        };

        private static DealState Deal(string a, string b, string kind, string id) => new DealState
        {
            id = id, type = kind, proposerId = a, recipientId = b, status = DealStatus.Active,
            week = 1, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(kind),
        };

        private static DealState Offer(string from, string to, string kind, string id, int week) => new DealState
        {
            id = id, type = kind, proposerId = from, recipientId = to, status = DealStatus.Proposed,
            week = week, expiresWeek = week, trustImpact = DealKind.DefaultTrust(kind),
        };

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static bool InBand(string response, double influence) =>
            response == LobbyResponse.Receptive ? influence >= 40 && influence <= 60
            : response == LobbyResponse.Open ? influence == 15
            : response == LobbyResponse.Skeptical ? influence == -5
            : response == LobbyResponse.Hostile && influence >= -50 && influence <= -30;

        private static void Check((string response, double influence, double impact) answer, string response, double influence, double impact)
        {
            Assert.That(answer.response, Is.EqualTo(response));
            Assert.That(answer.influence, Is.EqualTo(influence).Within(1e-6));
            Assert.That(answer.impact, Is.EqualTo(impact));
        }
    }
}
