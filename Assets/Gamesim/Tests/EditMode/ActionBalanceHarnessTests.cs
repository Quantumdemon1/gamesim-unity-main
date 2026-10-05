using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// E2 measurement, not a weighted utility function or shipping-balance acceptance. All
    /// payoffs come from committed commands. In particular a duplicate assessment is NOT a
    /// second piece of information, and warmth means the NPC's view, not the player's view.
    /// </summary>
    public sealed class ActionBalanceHarnessTests
    {
        private const int Samples = 128;
        private static readonly EpisodeCommandKind[] Conversations =
        {
            EpisodeCommandKind.SmallTalk, EpisodeCommandKind.PersonalChat,
            EpisodeCommandKind.StrategicDiscussion, EpisodeCommandKind.DiscussGame,
            EpisodeCommandKind.ShareSecret,
        };

        private static IEnumerable<TestCaseData> Contexts
        {
            get
            {
                foreach (int size in new[] { 3, 8, 16 })
                foreach (int social in new[] { 0, 10 })
                foreach (int view in new[] { -50, 0, 95 })
                foreach (int rapport in new[] { 0, 3 })
                foreach (bool informed in new[] { false, true })
                    yield return new TestCaseData(size, social, view, rapport, informed);
            }
        }

        private sealed class Row
        {
            public string action;
            public int outcomes, lostWarmth;
            public double focusWarmth, houseWarmth, rapport, lore, goals, opinions, reactions, storyStarts;
            public double leastFocusWarmth = double.PositiveInfinity, mostFocusWarmth = double.NegativeInfinity;

            // Componentwise comparison only. No exchange rate between warmth, information,
            // risk and stories is invented. Extrema describe the observed sample, not its EV.
            public double[] Means() => new[] { focusWarmth / outcomes, houseWarmth / outcomes,
                rapport / outcomes, lore / outcomes, goals / outcomes, opinions / outcomes, reactions / outcomes,
                storyStarts / outcomes, -(double)lostWarmth / outcomes };
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static void Set(EpisodeState s, string from, string to, double value) =>
            RelationshipLedger.Move(s, from, to, value - s.Score(from, to));
        private static string Band(double value) => value >= 25 ? "warm" : value <= -25 ? "cold" : "uncertain";
        private static string OpinionKey(StandingRow row) => row.fromId + ":" + row.toId + ":" + Band(row.score);
        private static HashSet<string> Opinions(EpisodeState s) => new HashSet<string>(s.ledger.standings.Select(OpinionKey));
        private static HashSet<string> Reactions(EpisodeState s) => new HashSet<string>(s.events
            .Where(e => e.kind == ConversationIntentRules.AiringBacked || e.kind == ConversationIntentRules.AiringOpposed)
            .Select(e => e.kind + ":" + string.Join(",", e.audienceIds)));
        private static int Rapport(EpisodeState s, string id) => s.story.contacts.FirstOrDefault(c => c.npcId == id)?.rapport ?? 0;

        private static EpisodeState Context(int size, int social, int view, bool informed, int rapport = 0)
        {
            var s = EconomyRulesTests.Fresh(size);
            s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableCommitments(s);
            EpisodeEngine.EnableStory(s);
            s.story.contacts.Add(new ContactState { npcId = ContentCatalog.MayaId, rapport = rapport });
            s.Find(s.playerId).stats.social = social;
            foreach (var a in s.Active) foreach (var b in s.Active.Where(b => b.id != a.id))
                Set(s, a.id, b.id, a.isPlayer || b.isPlayer ? view : 0);
            if (informed)
            {
                foreach (var npc in s.Active.Where(c => !c.isPlayer))
                {
                    foreach (var fact in Lore.FactsOf(s, npc.id).Where(f => f.depth < 4 && f.facet != Lore.Facets.Secret))
                        Lore.Learn(s, fact.id);
                    foreach (var subject in s.Active.Where(c => c.id != npc.id))
                        s.ledger.standings.Add(new StandingRow { week = s.week, fromId = npc.id,
                            toId = subject.id, score = s.Score(npc.id, subject.id), source = ClaimSource.Told });
                }
            }
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null,
            string text = null, string second = null) => new EpisodeCommand
        {
            id = "balance-" + s.revision + "-" + kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
            targetId = target, text = text, secondTargetId = second,
        };

        private static EpisodeState Apply(EpisodeState s, EpisodeCommand command)
        {
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            return result.state;
        }

        private static void Measure(Row row, EpisodeState before, EpisodeState after, string focus)
        {
            row.outcomes++;
            double warmth = after.Score(focus, before.playerId) - before.Score(focus, before.playerId);
            row.focusWarmth += warmth;
            row.houseWarmth += before.Active.Where(c => !c.isPlayer)
                .Sum(c => after.Score(c.id, before.playerId) - before.Score(c.id, before.playerId));
            if (warmth < 0) row.lostWarmth++;
            row.leastFocusWarmth = Math.Min(row.leastFocusWarmth, warmth);
            row.mostFocusWarmth = Math.Max(row.mostFocusWarmth, warmth);
            row.rapport += Rapport(after, focus) - Rapport(before, focus);
            row.lore += after.story.knownFacts.Except(before.story.knownFacts).Count();
            row.goals += Lore.FactsOf(after, focus).Count(f => f.facet == Lore.Facets.Goal
                && Lore.Knows(after, f.id) && !Lore.Knows(before, f.id));
            row.opinions += Opinions(after).Except(Opinions(before)).Count();
            row.reactions += Reactions(after).Except(Reactions(before)).Count();
            row.storyStarts += after.storylines.Select(c => c.id).Except(before.storylines.Select(c => c.id)).Count();
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
        }

        [TestCaseSource(nameof(Contexts))]
        public void ReportCommittedE2ConversationsAndMeetingsIncludingAlreadyKnownInformation(int size, int social, int view, int rapport, bool informed)
        {
            var basis = Context(size, social, view, informed, rapport);
            string focus = ContentCatalog.MayaId, original = Json(basis);
            var rows = Conversations.Select(k => new Row { action = k.ToString() }).Concat(new[]
            { new Row { action = EpisodeEngine.RallyTroops }, new Row { action = EpisodeEngine.AirDirtyLaundry } }).ToArray();
            for (int sample = 0; sample < Samples; sample++)
            {
                // The same saved stream and story seed for every counterfactual in this sample.
                // Hashing a fixed label is reproducible and does not depend on test execution order.
                uint seed = SeededRandom.HashSeed("E2-actions-v1:" + sample);
                foreach (var row in rows)
                {
                    var s = basis.Clone(); s.randomState = seed; s.seed = seed;
                    bool meeting = row.action == EpisodeEngine.RallyTroops || row.action == EpisodeEngine.AirDirtyLaundry;
                    var command = Command(s, meeting ? EpisodeCommandKind.HouseMeeting :
                        (EpisodeCommandKind)Enum.Parse(typeof(EpisodeCommandKind), row.action), meeting ? null : focus,
                        meeting ? row.action : null);
                    Measure(row, s, Apply(s, command), focus);
                }
            }
            Assert.That(Json(basis), Is.EqualTo(original), "The counterfactual harness must not mutate its starting state.");
            Assert.That(rows.All(r => r.outcomes == Samples), Is.True);
            if (informed)
            {
                Assert.That(rows.Where(r => Conversations.Any(k => k.ToString() == r.action)).All(r => r.lore == 0), Is.True,
                    "Already learned lore is not a new payoff.");
                Assert.That(rows.Single(r => r.action == EpisodeCommandKind.DiscussGame.ToString()).opinions, Is.Zero,
                    "Repeating unchanged opinions does not count as new information.");
            }
            var dominated = new List<string>();
            foreach (var a in rows) foreach (var b in rows.Where(r => r != a))
            {
                var av = a.Means(); var bv = b.Means();
                if (av.Zip(bv, (x, y) => y >= x - 1e-9).All(x => x) && av.Zip(bv, (x, y) => y > x + 1e-9).Any(x => x))
                    dominated.Add(a.action + " < " + b.action);
            }
            TestContext.WriteLine(Json(new { scope = "E2-conversation-and-meeting-sample-v1", size, social, view, rapport, informed,
                samplesPerAction = Samples, rows, sampledMeanDominance = dominated,
                limits = "Not universal dominance or win rate. No scalar utility. Extrema and story counts are sampled. Room acts, bargains and reply cards have separate coverage." }));
            // Dominance findings are reported, not hidden by changing the comparator or failing
            // before the full matrix is retained. This measurement does NOT accept the E2 gate.
        }

        [Test]
        public void NoveltyCountsBandsAndIdentitiesNotRepeatedLedgerRowsOrPrivateMemories()
        {
            var before = Context(8, 5, 0, true); var after = before.Clone();
            var row = before.ledger.standings[0];
            after.ledger.standings.Add(new StandingRow { week = row.week, fromId = row.fromId, toId = row.toId,
                score = row.score + 1, source = ClaimSource.Overheard });
            after.memories.Add(new MemoryState { ownerId = after.Active.First(c => !c.isPlayer).id,
                subjectId = after.playerId, week = after.week, text = "Private information the player did not hear.", isPrivate = true });
            Assert.That(Opinions(after).Except(Opinions(before)), Is.Empty);
            after.ledger.standings.Last().score = 50;
            Assert.That(Opinions(after).Except(Opinions(before)).Count(), Is.EqualTo(1));
        }

        [Test]
        public void ExactFirstOpportunityMechanicalPayoffsHaveNoStrictlyDominatedCoreVerb()
        {
            // One explicit witness context, not a claim about every context or story's utility.
            // Rapport three opens goal lore. No opinion/lore is already known and no score is
            // near a clamp. Integrate source buckets exactly, then use committed commands to
            // measure the deterministic lore/rapport and conditional information components.
            var basis = Context(8, 0, 0, false, 3);
            var vectors = new Dictionary<string, double[]>();
            var warmth = new double[7];
            const int gates = 100, amounts = 252; // divisible by 3, 4, 6, 7 and 9
            for (int g = 0; g < gates; g++) for (int a = 0; a < amounts; a++)
            {
                double gate = (g + .5) / gates, amount = (a + .5) / amounts;
                warmth[0] += WebSocialVocabulary.SmallTalk(amount);
                warmth[1] += ConversationIntentRules.PersonalWarmth(basis, amount);
                warmth[2] += WebSocialVocabulary.StrategicDiscussion(amount);
                warmth[3] += WebSocialVocabulary.DiscussGame(gate, amount);
                warmth[4] += WebSocialVocabulary.ShareSecret(gate, amount);
                warmth[5] += gate > WebSocialVocabulary.MeetingFloor
                    ? (6 * WebSocialVocabulary.MeetingRally(amount) + WebSocialVocabulary.MeetingSceptic) / 7
                    : WebSocialVocabulary.MeetingFailure(amount);
                warmth[6] += gate > WebSocialVocabulary.MeetingFloor
                    ? WebSocialVocabulary.MeetingAiring(amount) : WebSocialVocabulary.MeetingFailure(amount);
            }
            var expectedWarmth = new[] { 4, 3, 3.5, 3.4, 3.85, 1.5607142857142857, -1.875 };
            for (int index = 0; index < warmth.Length; index++)
            {
                warmth[index] /= gates * amounts;
                Assert.That(warmth[index], Is.EqualTo(expectedWarmth[index]).Within(1e-9));
            }
            // ChangeWithRoll writes NPC warmth through this independent uniform factor.
            Assert.That(Enumerable.Range(0, 100).Average(i => WebRules.ReciprocalDelta(1, (i + .5) / 100)),
                Is.EqualTo(1).Within(1e-12));
            for (int index = 0; index < Conversations.Length; index++)
            {
                var kind = Conversations[index]; var vector = new double[8];
                vector[0] = warmth[index]; vector[1] = warmth[index];
                // Average hit/miss information with exact source gate probabilities, not the
                // frequency of seeds we happened to sample. Lore/rapport apply on either branch.
                double success = kind == EpisodeCommandKind.DiscussGame ? .7 : kind == EpisodeCommandKind.ShareSecret ? .65 : 1;
                foreach (bool hit in new[] { false, true })
                {
                    var s = basis.Clone(); double floor = 1 - success;
                    s.randomState = Enumerable.Range(1, 10000).Select(i => (uint)i)
                        .First(seed => (new SeededRandom(seed).NextDouble() > floor) == (success == 1 || hit));
                    var after = Apply(s, Command(s, kind, ContentCatalog.MayaId));
                    double p = hit ? success : 1 - success;
                    vector[2] += p * (Rapport(after, ContentCatalog.MayaId) - Rapport(s, ContentCatalog.MayaId));
                    vector[3] += p * after.story.knownFacts.Except(s.story.knownFacts).Count();
                    vector[4] += p * Lore.FactsOf(after, ContentCatalog.MayaId).Count(f => f.facet == Lore.Facets.Goal
                        && Lore.Knows(after, f.id) && !Lore.Knows(s, f.id));
                    vector[5] += p * Opinions(after).Except(Opinions(s)).Count();
                }
                vector[7] = success; // probability of no loss in the focus NPC's view
                vectors.Add(kind.ToString(), vector);
            }
            foreach (string meeting in new[] { EpisodeEngine.RallyTroops, EpisodeEngine.AirDirtyLaundry })
            foreach (bool hit in new[] { false, true })
            {
                var s = basis.Clone(); s.randomState = Enumerable.Range(1, 10000).Select(i => (uint)i)
                    .First(seed => (new SeededRandom(seed).NextDouble() > WebSocialVocabulary.MeetingFloor) == hit);
                var after = Apply(s, Command(s, EpisodeCommandKind.HouseMeeting, text: meeting));
                int informed = meeting == EpisodeEngine.AirDirtyLaundry && hit ? 7 : 0;
                Assert.That(Opinions(after).Except(Opinions(s)).Count(), Is.EqualTo(informed));
                Assert.That(Reactions(after).Except(Reactions(s)).Count(), Is.EqualTo(informed));
                Assert.That(after.story.knownFacts.Except(s.story.knownFacts), Is.Empty);
                Assert.That(Rapport(after, ContentCatalog.MayaId), Is.EqualTo(Rapport(s, ContentCatalog.MayaId)));
            }
            vectors.Add(EpisodeEngine.RallyTroops, new[] { warmth[5], 7 * warmth[5], 0, 0, 0, 0, 0, .65 * 6 / 7 });
            vectors.Add(EpisodeEngine.AirDirtyLaundry, new[] { warmth[6], 7 * warmth[6], 0, 0, 0, .65 * 7, .65 * 7, .65 * .5 });
            foreach (var a in vectors) foreach (var b in vectors.Where(r => r.Key != a.Key))
                Assert.That(a.Value.Zip(b.Value, (x, y) => y >= x - 1e-9).All(x => x)
                    && a.Value.Zip(b.Value, (x, y) => y > x + 1e-9).Any(x => x), Is.False,
                    b.Key + " strictly dominates " + a.Key + " in this first-opportunity mechanical vector.");
            TestContext.WriteLine(Json(new { scope = "exact-first-opportunity-mechanical-frontier", vectors,
                dimensions = new[] { "npcWarmthEV", "houseWarmthEV", "rapport", "novelLore", "novelGoals", "novelOpinionsEV", "witnessedReactionsEV", "probabilityNoFocusWarmthLoss" },
                limits = "Eight-person house, neutral views, rapport3, unknown lore/opinions. Not repeated-information, whole-season, story utility or human acceptance." }));
        }

        [TestCase(ReplyCards.Confrontation, false)] [TestCase(ReplyCards.Confrontation, true)]
        [TestCase(ReplyCards.Gossip, false)] [TestCase(ReplyCards.Gossip, true)]
        [TestCase(ReplyCards.Plea, false)] [TestCase(ReplyCards.Plea, true)]
        public void ReportReplyTradeoffsWithoutCountingRepeatedAssessmentsAsNewInformation(string kind, bool informed)
        {
            var basis = Context(8, 5, 0, informed);
            var npcs = basis.Active.Where(c => !c.isPlayer).ToArray();
            string speaker = npcs[0].id, subject = kind == ReplyCards.Gossip ? npcs[1].id : null;
            if (kind == ReplyCards.Plea)
            {
                basis.phase = EpisodePhase.Campaign; basis.hohId = npcs[0].id;
                basis.nominees = new List<string> { npcs[1].id, npcs[2].id };
                basis.vetoHolderId = basis.playerId; basis.vetoResolved = true;
                basis.vetoPlayers = new[] { basis.hohId, npcs[1].id, npcs[2].id, basis.playerId }
                    .Concat(basis.Active.Select(c => c.id)).Distinct().Take(EpisodeEngine.VetoPlayerCount(8)).ToList();
                speaker = npcs[1].id; subject = npcs[2].id;
            }
            var card = new ReplyCardState { id = "balance-reply", week = basis.week, kind = kind, fromId = speaker, aboutId = subject };
            basis.replyCards.Add(card);
            var rows = new Dictionary<string, double[]>();
            foreach (var reply in ReplyCards.Replies(kind))
            {
                var vector = new double[5]; rows.Add(reply.Key, vector);
                for (int sample = 0; sample < Samples; sample++)
                {
                    var s = basis.Clone(); s.randomState = SeededRandom.HashSeed("E2-replies-v1:" + sample);
                    var after = Apply(s, Command(s, EpisodeCommandKind.ReplyToHouseguest, card.id, reply.Key));
                    vector[0] += after.Score(speaker, s.playerId) - s.Score(speaker, s.playerId);
                    vector[1] += Opinions(after).Except(Opinions(s)).Count();
                    vector[2] += after.deals.Count(d => d.type == DealKind.SafetyAgreement && d.status == DealStatus.Active);
                    if (kind == ReplyCards.Gossip) vector[3] += s.Score(subject, speaker) - after.Score(subject, speaker);
                    vector[4] -= after.promises.Count(p => p.status == PromiseStatus.Active)
                        + after.deals.Count(d => d.status == DealStatus.Active);
                    Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s)), "Replies stay free.");
                }
                for (int index = 0; index < vector.Length; index++) vector[index] /= Samples;
            }
            string informationAnswer = kind == ReplyCards.Confrontation ? "deflect" : kind == ReplyCards.Gossip ? "confront" : "refuse";
            Assert.That(rows[informationAnswer][1], Is.EqualTo(informed ? 0 : 1));
            var dominated = new List<string>();
            foreach (var a in rows) foreach (var b in rows.Where(r => r.Key != a.Key))
                if (a.Value.Zip(b.Value, (x, y) => y >= x - 1e-9).All(x => x)
                    && a.Value.Zip(b.Value, (x, y) => y > x + 1e-9).Any(x => x)) dominated.Add(a.Key + " < " + b.Key);
            TestContext.WriteLine(Json(new { scope = "E2-reply-information-sample-v1", kind, informed,
                samplesPerAnswer = Samples, dimensions = new[] { "npcWarmth", "novelOpinionBands", "agreedSafety", "listenerDamage", "freedomFromObligations" },
                rows, sampledMeanDominance = dominated,
                limits = "Nominal available safety and actual unresolved campaign only. Repeated knowledge is measured, not claimed to make a choice balanced. Future strategy and capped/unavailable deals remain separate." }));
        }

        [TestCase(Negotiation.Remind, 21)]
        [TestCase(Negotiation.Demand, 21.25)]
        [TestCase(Negotiation.Threaten, 13)]
        public void CallInExpectedReluctanceIncludesItsCostAndTheActualDecisionConsumer(string approach, double expected)
        {
            var s = SafetyContext(50);
            double probability = Negotiation.Chance(s, s.hohId, approach, false) / 100;
            var landed = Call(s, approach, true); var refused = Call(s, approach, false);
            double actual = probability * EpisodeEngine.NominationWeight(landed, s.hohId, s.playerId)
                + (1 - probability) * EpisodeEngine.NominationWeight(refused, s.hohId, s.playerId)
                - EpisodeEngine.NominationWeight(s, s.hohId, s.playerId);
            Assert.That(actual, Is.EqualTo(expected).Within(1e-9));
            TestContext.WriteLine(approach + " exact expected change in nomination reluctance="
                + actual.ToString("R", CultureInfo.InvariantCulture) + "; not an expected survival probability.");
        }

        [TestCase(30, Negotiation.Remind)]
        [TestCase(40, Negotiation.Demand)]
        [TestCase(50, Negotiation.Threaten)]
        public void EveryCallInApproachCanHaveTheBestActualNominationSurvivalChance(int otherReluctance, string best)
        {
            var s = SafetyContext(otherReluctance);
            var probabilities = new Dictionary<string, double>();
            foreach (string approach in Negotiation.Approaches)
            {
                double chance = Negotiation.Chance(s, s.hohId, approach, false) / 100;
                var hit = Call(s, approach, true); var miss = Call(s, approach, false);
                var nominatedHit = Apply(hit, Command(hit, EpisodeCommandKind.Advance));
                var nominatedMiss = Apply(miss, Command(miss, EpisodeCommandKind.Advance));
                double survives = (nominatedHit.nominees.Contains(s.playerId) ? 0 : chance)
                    + (nominatedMiss.nominees.Contains(s.playerId) ? 0 : 1 - chance);
                probabilities.Add(approach, survives);
                Assert.That(nominatedHit.promises.Single().status, Is.EqualTo(nominatedHit.nominees.Contains(s.playerId)
                    ? PromiseStatus.Broken : PromiseStatus.Active), "Safety still covers later nominations until its expiry; breaking settles it now.");
                Assert.That(nominatedMiss.promises.Single().status, Is.EqualTo(nominatedMiss.nominees.Contains(s.playerId)
                    ? PromiseStatus.Broken : PromiseStatus.Active));
                if (nominatedHit.nominees.Contains(s.playerId))
                    Assert.That(nominatedHit.promises.Single().brokenById, Is.EqualTo(s.hohId));
            }
            Assert.That(probabilities[best], Is.GreaterThan(0));
            Assert.That(probabilities.Where(p => p.Key != best).All(p => p.Value < probabilities[best]), Is.True,
                Json(probabilities));
            TestContext.WriteLine("Other candidate reluctance " + otherReluctance + ": " + Json(probabilities));
        }

        private static EpisodeState SafetyContext(int otherReluctance)
        {
            var s = EconomyRulesTests.Fresh(6); s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableCommitments(s);
            // Isolate the documented call-in mechanism, not story/agency modifiers. This is a
            // valid stored rules configuration; other systems are covered separately.
            s.phase = EpisodePhase.Nomination; s.hohId = ContentCatalog.MayaId;
            foreach (var actor in s.Active) actor.traits.Clear();
            foreach (var actor in s.Active.Where(c => c.id != s.hohId))
                Set(s, s.hohId, actor.id, actor.isPlayer ? 0 : otherReluctance);
            s.promises.Add(new PromiseState { id = "balance-safety", fromId = s.hohId, toId = s.playerId,
                kind = PromiseKind.Safety, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1 });
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        private static EpisodeState Call(EpisodeState basis, string approach, bool lands)
        {
            var s = basis.Clone(); double threshold = Negotiation.Chance(s, s.hohId, approach, false) / 100;
            for (uint seed = 1; seed < 10000; seed++)
            {
                if ((new SeededRandom(seed).NextDouble() < threshold) != lands) continue;
                s.randomState = seed;
                var after = Apply(s, Command(s, EpisodeCommandKind.Negotiate, s.hohId,
                    Negotiation.CallIn(approach), s.promises.Single().id));
                Assert.That(Negotiation.HeldTo(after, after.promises.Single()), Is.EqualTo(lands ? Negotiation.Hold(approach) : 0));
                return after;
            }
            throw new InvalidOperationException("No seed for call-in branch.");
        }
    }
}
