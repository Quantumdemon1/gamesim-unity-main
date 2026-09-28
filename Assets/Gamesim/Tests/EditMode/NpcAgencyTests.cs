using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// NPC agency (NPC-AGENCY-PLAN.md): first impressions by temperament, seeded once and never into
    /// a season under way; warmth that follows who takes to whom; the pact cap; the agendas and what
    /// they drive (the weekly turn, the campaign, the nominations, an invitation to the player); and
    /// a season without the boundary exactly as it was.
    /// </summary>
    public sealed class NpcAgencyTests
    {
        /// <summary>A season as the director ships one, short of agency.</summary>
        private static EpisodeState Fresh(uint seed, int size = 8)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            s.strategyRulesStartWeek = 1; s.blocRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s);
            return s;
        }

        /// <summary>Agency on with its first impressions, as the director ships it.</summary>
        private static EpisodeState Agency(uint seed, int size = 8) { var s = Fresh(seed, size); EpisodeEngine.EnableAgency(s); return s; }

        /// <summary>Agency on and every standing at nought, so a test can set the few it is about.</summary>
        private static EpisodeState Bare(uint seed, int size = 8) { var s = Fresh(seed, size); s.agencyRulesStartWeek = 1; return s; }

        private static void Set(EpisodeState s, string from, string to, double score) =>
            s.relationships.First(r => r.fromId == from && r.toId == to).score = score;

        private static ContestantState Npc(EpisodeState s, int index) => s.contestants.Where(c => !c.isPlayer).ElementAt(index);

        private static AllianceState Pact(EpisodeState s, params string[] members)
        {
            var pact = new AllianceState { id = "alliance-test-" + s.nextSequence++, name = "The Pact", members = members.ToList(), active = true };
            s.alliances.Add(pact);
            return pact;
        }

        // ---------------------------------------------------------------- first impressions

        [Test]
        public void FirstImpressionsSeedEveryHouseguestsViewButThePlayersOwn()
        {
            var s = Fresh(1, 12);
            s.Find(s.playerId).traits = new List<string> { "Funny", "Charming" };
            Assert.That(s.relationships.All(r => r.score == 0), Is.True, "Nought before the boundary.");
            EpisodeEngine.EnableAgency(s);
            Assert.That(EpisodeEngine.AgencyOn(s), Is.True);
            foreach (var a in s.contestants.Where(c => !c.isPlayer))
                foreach (var b in s.contestants.Where(c => c.id != a.id))
                    Assert.That(s.Score(a.id, b.id), Is.EqualTo(EpisodeEngine.FirstImpressionPerPoint * TraitAffinity.Compatibility(a, b)).Within(1e-9), a.name + " on " + b.name);
            Assert.That(s.contestants.Where(c => !c.isPlayer).All(c => s.Score(s.playerId, c.id) == 0), Is.True, "What you think of them is yours to decide.");
            Assert.That(s.contestants.Where(c => !c.isPlayer).All(c => s.Score(c.id, s.playerId) > 0), Is.True, "A funny, charming player is liked before they say a word.");
            Assert.That(s.relationships.All(r => r.events.Count == 0), Is.True, "Scores only: trust starts neutral.");

            var before = s.relationships.Select(r => r.score).ToList();
            EpisodeEngine.EnableAgency(s);
            Assert.That(s.relationships.Select(r => r.score), Is.EqualTo(before), "Never seeded twice.");
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }

        [Test]
        public void TheShippedHouseHasKindredPairsAndOilAndWater()
        {
            var s = Agency(2, 12);
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            var mutual = npcs.SelectMany(a => npcs.Where(b => string.CompareOrdinal(a.id, b.id) < 0)
                .Select(b => (a, b, low: System.Math.Min(s.Score(a.id, b.id), s.Score(b.id, a.id)), high: System.Math.Max(s.Score(a.id, b.id), s.Score(b.id, a.id))))).ToList();
            Assert.That(mutual.Any(p => p.low >= 10), Is.True, "Somebody starts close to somebody.");
            Assert.That(mutual.Any(p => p.high <= -10), Is.True, "and somebody starts at odds with somebody: the cold pairs the story casts on.");
        }

        [Test]
        public void ASeasonUnderWayIsNeverSeeded()
        {
            var engine = new EpisodeEngine(Fresh(3));
            Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot.Clone();
            var before = s.relationships.Select(r => r.score).ToList();
            EpisodeEngine.EnableAgency(s);
            Assert.That(s.relationships.Select(r => r.score), Is.EqualTo(before), "A command was accepted: the season has begun.");
            Assert.That(EpisodeEngine.AgencyOn(s), Is.True, "but the rules are on from this week.");

            var imported = Fresh(4);
            imported.week = 3;
            before = imported.relationships.Select(r => r.score).ToList();
            EpisodeEngine.EnableAgency(imported, imported.week + 1);
            Assert.That(imported.agencyRulesStartWeek, Is.EqualTo(4), "The importer's boundary: the week after the save's.");
            Assert.That(EpisodeEngine.AgencyOn(imported), Is.False, "This week plays as it was.");
            Assert.That(imported.relationships.Select(r => r.score), Is.EqualTo(before));
        }

        // ---------------------------------------------------------------- warmth

        [Test]
        public void DesireAddsTwiceTheCompatibility()
        {
            var s = Agency(5, 12);
            var a = Npc(s, 0); var b = Npc(s, 1);
            Set(s, a.id, b.id, 30); Set(s, b.id, a.id, 30);
            double with = NpcAlliances.Desire(s, a.id, b.id);
            s.agencyRulesStartWeek = 0;
            double without = NpcAlliances.Desire(s, a.id, b.id);
            Assert.That(with - without, Is.EqualTo(EpisodeEngine.DesirePerPoint * TraitAffinity.Compatibility(a, b)).Within(1e-9));
        }

        [Test]
        public void ATalkIsWorthMoreBetweenKindredSpiritsAndLessBetweenOilAndWater()
        {
            var s = Agency(6, 12);
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            var kindred = npcs.SelectMany(a => npcs.Where(b => b.id != a.id).Select(b => (a, b))).First(p => TraitAffinity.Compatibility(p.a, p.b) >= 4);
            var grating = npcs.SelectMany(a => npcs.Where(b => b.id != a.id).Select(b => (a, b))).First(p => TraitAffinity.Compatibility(p.a, p.b) <= -4);
            Assert.That(EpisodeEngine.TalkWarmth(s, kindred.a.id, kindred.b.id), Is.EqualTo(NpcSocialActions.TalkImpact + EpisodeEngine.TalkAffinityBound));
            Assert.That(EpisodeEngine.TalkWarmth(s, grating.a.id, grating.b.id), Is.EqualTo(NpcSocialActions.TalkImpact - EpisodeEngine.TalkAffinityBound));
            s.agencyRulesStartWeek = 0;
            Assert.That(EpisodeEngine.TalkWarmth(s, kindred.a.id, kindred.b.id), Is.EqualTo(NpcSocialActions.TalkImpact), "Without agency a talk is a talk.");
        }

        [Test]
        public void AConversationCarriesTheTemperamentsBias()
        {
            var s = Agency(7, 12);
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            int Mutual(ContestantState a, ContestantState b) => TraitAffinity.Compatibility(a, b) + TraitAffinity.Compatibility(b, a);
            var pairs = npcs.SelectMany(a => npcs.Where(b => string.CompareOrdinal(a.id, b.id) < 0).Select(b => (a, b))).ToList();
            var warm = pairs.First(p => Mutual(p.a, p.b) >= 6);
            var cold = pairs.First(p => Mutual(p.a, p.b) <= -6);
            Assert.That(EpisodeEngine.ConversationBias(s, warm.a.id, warm.b.id), Is.EqualTo(1));
            Assert.That(EpisodeEngine.ConversationBias(s, cold.a.id, cold.b.id), Is.EqualTo(-1));
            Assert.That(pairs.Where(p => Mutual(p.a, p.b) == 0).All(p => EpisodeEngine.ConversationBias(s, p.a.id, p.b.id) == 0), Is.True);
            s.agencyRulesStartWeek = 0;
            Assert.That(EpisodeEngine.ConversationBias(s, warm.a.id, warm.b.id), Is.EqualTo(0));
        }

        [Test]
        public void ThePactCapHoldsTheHouseToOnePactPerThreeAndOneEach()
        {
            var s = Bare(8, 8);
            var npcs = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            Assert.That(npcs.Count, Is.EqualTo(7)); Assert.That(NpcAlliances.PactCap(s), Is.EqualTo(2));
            foreach (var a in npcs) foreach (var b in npcs.Where(x => x != a)) Set(s, a, b, 80);
            var open = s.Clone(); open.agencyRulesStartWeek = 0;

            NpcAlliances.Settle(s); NpcAlliances.Settle(s);
            var pacts = NpcAlliances.NpcOnlyPacts(s);
            Assert.That(pacts.Count, Is.EqualTo(2), "Two pacts among seven, and no more the week after.");
            Assert.That(npcs.All(id => pacts.Count(p => p.members.Contains(id)) <= 1), Is.True, "Nobody in two.");
            Assert.That(npcs.Any(id => NpcAlliances.WouldPropose(s, id, npcs.First(o => o != id && !pacts.Any(p => p.members.Contains(o))))), Is.False, "The house is at its share.");

            NpcAlliances.Settle(open);
            Assert.That(NpcAlliances.NpcOnlyPacts(open).Count, Is.EqualTo(3), "Without agency a house that loves everybody pairs off entirely.");
        }

        // ---------------------------------------------------------------- agendas

        [Test]
        public void OnTheBlockTheyFightToStayAndCampaignToThePersuadable()
        {
            var s = Bare(9, 12);
            var hoh = Npc(s, 0); var first = Npc(s, 1); var second = Npc(s, 2);
            var allied = Npc(s, 3); var keeper = Npc(s, 4); var lost = Npc(s, 5); var torn = Npc(s, 6); var leaning = Npc(s, 7);
            s.phase = EpisodePhase.Campaign; s.hohId = hoh.id; s.nominees = new List<string> { first.id, second.id }; s.vetoResolved = true; s.evictionResolved = false;
            Pact(s, allied.id, second.id);
            Set(s, keeper.id, first.id, 40);
            Set(s, lost.id, first.id, -30);
            Set(s, torn.id, first.id, 5);
            Set(s, leaning.id, first.id, -10);

            var agenda = NpcAgendas.Of(s, first.id);
            Assert.That(agenda.kind, Is.EqualTo(Agendas.Survive));
            var persuadable = EpisodeEngine.PersuadableVoters(s, first.id).Select(v => v.id).ToList();
            Assert.That(persuadable, Does.Not.Contain(allied.id), "The other nominee's ally is not worth the visit.");
            Assert.That(persuadable, Does.Not.Contain(keeper.id), "nor somebody sure to keep them,");
            Assert.That(persuadable, Does.Not.Contain(lost.id), "nor somebody sure to evict them.");
            Assert.That(persuadable, Does.Contain(torn.id).And.Contain(leaning.id).And.Contain(s.playerId));
            Assert.That(persuadable.Take(2), Does.Not.Contain(leaning.id), "The closest to torn come first.");
            Assert.That(agenda.partnerId, Is.EqualTo(persuadable[0]));

            NpcSocialActions.Campaign(s);
            bool Visited(string voterId) => s.relationships.First(r => r.fromId == first.id && r.toId == voterId).events.Any(e => e.type == "campaign");
            foreach (var voter in persuadable.Take(EpisodeEngine.CampaignVisits)) Assert.That(Visited(voter), Is.True, s.Find(voter).name + " was visited.");
            Assert.That(Visited(keeper.id), Is.False, "A sure vote was not.");
            Assert.That(Visited(s.playerId) == s.replyCards.Any(c => c.kind == ReplyCards.Plea && c.fromId == first.id), Is.True, "The player gets the plea card exactly when visited.");
        }

        [Test]
        public void TheHeadOfHouseholdReignsAndTheUnalliedCourtThem()
        {
            var s = Bare(10, 12);
            var hoh = Npc(s, 0); var courtier = Npc(s, 1); var mate = Npc(s, 2);
            s.phase = EpisodePhase.Nomination; s.hohId = hoh.id; s.nominees.Clear(); s.evictionResolved = false;
            Pact(s, hoh.id, mate.id);
            Assert.That(NpcAgendas.Of(s, hoh.id).kind, Is.EqualTo(Agendas.Reign));
            Assert.That(NpcAgendas.Of(s, hoh.id).partnerId, Is.EqualTo(mate.id), "The Head of Household listens to their pact.");
            Assert.That(NpcAgendas.Of(s, courtier.id).kind, Is.EqualTo(Agendas.Court));
            Assert.That(NpcAgendas.Of(s, courtier.id).partnerId, Is.EqualTo(hoh.id));
            Assert.That(NpcAgendas.Of(s, mate.id).kind, Is.Not.EqualTo(Agendas.Court), "A pact-mate has their claim already.");
            s.phase = EpisodePhase.Campaign; s.nominees = new List<string> { Npc(s, 3).id, Npc(s, 4).id };
            Assert.That(NpcAgendas.Of(s, courtier.id).kind, Is.Not.EqualTo(Agendas.Court), "By the campaign the power is spent.");
        }

        [Test]
        public void APactHuntsACommonThreatAndOtherwiseHoldsTight()
        {
            var s = Bare(11, 12);
            var a = Npc(s, 1); var b = Npc(s, 2); var threat = Npc(s, 3);
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            Pact(s, a.id, b.id);
            Assert.That(NpcAgendas.Of(s, a.id).kind, Is.EqualTo(Agendas.Hold));
            Assert.That(NpcAgendas.Of(s, a.id).partnerId, Is.EqualTo(b.id));
            Set(s, a.id, threat.id, -15); Set(s, b.id, threat.id, -10);
            var hunt = NpcAgendas.Of(s, a.id);
            Assert.That(hunt.kind, Is.EqualTo(Agendas.Hunt));
            Assert.That(hunt.partnerId, Is.EqualTo(b.id)); Assert.That(hunt.targetId, Is.EqualTo(threat.id));
            Assert.That(NpcAgendas.PreferredPartner(s, a.id), Is.EqualTo(b.id));

            NpcSocialActions.Settle(s);
            var word = s.relationships.First(r => r.fromId == b.id && r.toId == threat.id).events.FirstOrDefault(e => e.type == "rumor" && e.impactScore == EpisodeEngine.HuntImpact);
            Assert.That(word, Is.Not.Null, "The hunt's turn: a word against the threat with the pact-mate.");
        }

        [Test]
        public void WithNoPactTheyBuildTowardWhoTheyMostWant()
        {
            var s = Bare(12, 12);
            var a = Npc(s, 1); var wanted = Npc(s, 4);
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            Set(s, a.id, wanted.id, 50);
            var agenda = NpcAgendas.Of(s, a.id);
            Assert.That(agenda.kind, Is.EqualTo(Agendas.Build));
            string expected = s.Active.Where(c => c.id != a.id && s.Score(a.id, c.id) >= 0).OrderByDescending(c => NpcAlliances.Desire(s, a.id, c.id)).First().id;
            Assert.That(agenda.partnerId, Is.EqualTo(expected).And.EqualTo(wanted.id));
            Assert.That(NpcAgendas.StillWorking(s, a.id, agenda), Is.False, "At fifty it is built: the pact pass takes it from here,");
            Assert.That(NpcAgendas.PreferredPartner(s, a.id), Is.Null, "so the week is spent as it always was, not piled on one person.");
            Set(s, a.id, wanted.id, NpcAlliances.MinimumRelationship - 1);
            Assert.That(NpcAgendas.StillWorking(s, a.id, NpcAgendas.Of(s, a.id)), Is.True, "Short of the pact floor there is building to do,");
            Assert.That(NpcAgendas.PreferredPartner(s, a.id), Is.EqualTo(wanted.id), "and the NPC world pairs them first.");
        }

        [Test]
        public void TheAgendaInWords()
        {
            var s = Bare(13, 12);
            var a = Npc(s, 0); var b = Npc(s, 1); var c = Npc(s, 2);
            Assert.That(NpcAgendas.Describe(s, a.id, new NpcAgenda { kind = Agendas.Survive, partnerId = s.playerId }), Is.EqualTo(a.name + " is fighting to stay, working on you"));
            Assert.That(NpcAgendas.Describe(s, a.id, new NpcAgenda { kind = Agendas.Reign, partnerId = b.id }), Is.EqualTo(a.name + " holds the power and is listening to " + b.name));
            Assert.That(NpcAgendas.Describe(s, a.id, new NpcAgenda { kind = Agendas.Court, partnerId = b.id }), Is.EqualTo(a.name + " is courting " + b.name));
            Assert.That(NpcAgendas.Describe(s, a.id, new NpcAgenda { kind = Agendas.Hunt, partnerId = b.id, targetId = c.id }), Is.EqualTo(a.name + " wants " + c.name + " out, with " + b.name));
            Assert.That(NpcAgendas.Describe(s, a.id, new NpcAgenda { kind = Agendas.Build, partnerId = s.playerId }), Is.EqualTo(a.name + " is looking for a partner, and it's you"));
            Assert.That(NpcAgendas.Describe(s, a.id, new NpcAgenda { kind = Agendas.Hold, partnerId = b.id }), Is.EqualTo(a.name + " is holding tight with " + b.name));
            Assert.That(NpcAgendas.Describe(s, a.id, new NpcAgenda { kind = Agendas.Drift }), Is.EqualTo(a.name + " is keeping their head down"));
            Assert.That(NpcAgendas.Describe(s, a.id, null), Is.Null);
        }

        // ---------------------------------------------------------------- intelligence

        [Test]
        public void TheHeadOfHouseholdWeighsThreatAndConsultsThePact()
        {
            var s = Bare(14, 12);
            var hoh = Npc(s, 0); var quiet = Npc(s, 1); var beast = Npc(s, 2); var mate = Npc(s, 3);
            s.phase = EpisodePhase.Nomination; s.hohId = hoh.id; s.nominees.Clear();
            beast.hohWins = 3;
            Assert.That(EpisodeEngine.ThreatTerm(s, hoh.id, beast.id), Is.EqualTo(EpisodeEngine.ThreatWeight * ThreatAssessment.Total(s, hoh.id, beast.id)).Within(1e-9));
            Assert.That(EpisodeEngine.NominationWeight(s, hoh.id, beast.id), Is.LessThan(EpisodeEngine.NominationWeight(s, hoh.id, quiet.id)), "The competition beast goes up first.");
            Pact(s, hoh.id, mate.id);
            Assert.That(EpisodeEngine.ThreatTerm(s, hoh.id, beast.id),
                Is.EqualTo(EpisodeEngine.ThreatWeight * ThreatAssessment.Total(s, hoh.id, beast.id) + EpisodeEngine.AlliesThreatWeight * ThreatAssessment.Total(s, mate.id, beast.id)).Within(1e-9),
                "The Head of Household consults their pact.");
            s.agencyRulesStartWeek = 0;
            Assert.That(EpisodeEngine.ThreatTerm(s, hoh.id, beast.id), Is.Zero);
            Assert.That(EpisodeEngine.NominationWeight(s, hoh.id, beast.id), Is.EqualTo(EpisodeEngine.NominationWeight(s, hoh.id, quiet.id)).Within(1e-9), "Without agency, warmth alone, as it was.");
        }

        [Test]
        public void ABuilderAsksThePlayerToWorkWithThemThroughTheLadder()
        {
            var s = Bare(15, 12);
            s.Find(s.playerId).traits = new List<string> { "Funny", "Charming" };
            var a = Npc(s, 1);
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            Set(s, a.id, s.playerId, 60);
            Assume.That(NpcAgendas.Of(s, a.id).kind, Is.EqualTo(Agendas.Build));
            Assume.That(NpcAgendas.Of(s, a.id).partnerId, Is.EqualTo(s.playerId));
            Assert.That(NpcAlliances.WouldPropose(s, a.id, s.playerId), Is.True, "Warm enough and wanting it enough.");
            Assert.That(NpcDeals.Offer(s, a.id, s.playerId), Is.EqualTo(DealKind.AllianceInvite));
            NpcDeals.Propose(s);
            Assert.That(NpcDeals.Pending(s).Any(d => d.type == DealKind.AllianceInvite && d.proposerId == a.id), Is.True, "and it is put to the player.");
            s.agencyRulesStartWeek = 0;
            Assert.That(NpcDeals.Offer(s, a.id, s.playerId), Is.EqualTo(DealKind.Partnership), "Without agency the ladder's own rung, as it was.");
        }

        [Test]
        public void CourtingTheHeadOfHouseholdWarmsThemBeforeTheNominations()
        {
            var s = Bare(16, 12);
            var hoh = Npc(s, 0); var courtier = Npc(s, 1);
            s.phase = EpisodePhase.Nomination; s.hohId = hoh.id; s.nominees.Clear();
            Assume.That(NpcAgendas.Of(s, courtier.id).kind, Is.EqualTo(Agendas.Court));
            double before = s.Score(courtier.id, hoh.id);
            NpcSocialActions.Court(s, courtier, hoh);
            Assert.That(s.Score(courtier.id, hoh.id) - before, Is.EqualTo(EpisodeEngine.TalkWarmth(s, courtier.id, hoh.id)).Within(1e-9));
            Assert.That(s.relationships.First(r => r.fromId == courtier.id && r.toId == hoh.id).events.Any(e => e.type == "talk" && e.description.Contains("courted")), Is.True);
        }

        [Test]
        public void WithoutTheBoundaryTheHouseIsExactlyWhatItWas()
        {
            var s = Fresh(17, 12);
            var a = Npc(s, 0); var b = Npc(s, 1);
            Assert.That(EpisodeEngine.AgencyOn(s), Is.False);
            Assert.That(NpcAgendas.Of(s, a.id), Is.Null);
            Assert.That(NpcAgendas.PreferredPartner(s, a.id), Is.Null);
            Assert.That(EpisodeEngine.TalkWarmth(s, a.id, b.id), Is.EqualTo(NpcSocialActions.TalkImpact));
            Assert.That(EpisodeEngine.ConversationBias(s, a.id, b.id), Is.Zero);
            Assert.That(EpisodeEngine.ThreatTerm(s, a.id, b.id), Is.Zero);
            s.phase = EpisodePhase.Campaign; s.hohId = a.id; s.nominees = new List<string> { b.id, Npc(s, 2).id };
            Assert.That(EpisodeEngine.PersuadableVoters(s, b.id), Is.Empty);
            Assert.That(s.relationships.All(r => r.score == 0), Is.True);
        }
    }
}
