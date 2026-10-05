using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Prospective Safety reader integration, not an enabled rule-1 season or native acceptance.</summary>
    public sealed class UnifiedSafetyStoryConsumerTests
    {
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void TheirWordNeedsTheFriendsIncomingWordNotThePlayersOutgoingWord(bool outgoing, bool promised)
        {
            var s = State(); string friend = OneFriend(s);
            s.unifiedCommitments.Add(Promise(s, "the-word", outgoing ? s.playerId : friend, outgoing ? friend : s.playerId));
            AssertTheirWord(s, friend, promised);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheirWordRecognizesTheReciprocalDealInEitherSourceDirection(bool reversed)
        {
            var s = State(); string friend = OneFriend(s);
            s.unifiedCommitments.Add(Deal(s, "the-deal", reversed ? friend : s.playerId, reversed ? s.playerId : friend));
            AssertTheirWord(s, friend, true);
        }

        [TestCase(DealStatus.Proposed, true)]
        [TestCase(DealStatus.Accepted, true)]
        [TestCase(DealStatus.Active, true)]
        [TestCase(DealStatus.Declined, false)]
        [TestCase(DealStatus.Expired, false)]
        public void TheirWordRetainsTheSourceBindingStatusPolicy(string status, bool promised)
        {
            var s = State(); string friend = OneFriend(s);
            var row = Deal(s, "status-control", friend, s.playerId); row.status = status; s.unifiedCommitments.Add(row);
            AssertTheirWord(s, friend, promised);
        }

        [TestCase(UnifiedCommitments.PromisePolicy, DealStatus.Broken)]
        [TestCase(UnifiedCommitments.PromisePolicy, DealStatus.Expired)]
        [TestCase(UnifiedCommitments.DealPolicy, DealStatus.Broken)]
        public void ASettledOrExpiredWordDoesNotWinTheirWord(string policy, string status)
        {
            var s = State(); string friend = OneFriend(s);
            var row = policy == UnifiedCommitments.PromisePolicy ? Promise(s, "old-word", friend, s.playerId)
                : Deal(s, "old-deal", friend, s.playerId);
            s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, friend, s.playerId); else row.status = status;
            AssertTheirWord(s, friend, false);
        }

        [TestCase(0, PromiseKind.Safety, true)]
        [TestCase(0, PromiseKind.Vote, true)]
        [TestCase(0, PromiseKind.FinalTwo, false)]
        [TestCase(1, PromiseKind.Vote, true)]
        [TestCase(1, PromiseKind.FinalTwo, false)]
        public void TheirWordKeepsLegacyPromiseFamilyPolicyInValidLegacyAndMixedStores(int rules, PromiseKind kind, bool promised)
        {
            var s = State(); string friend = OneFriend(s);
            s.unifiedCommitmentRulesVersion = rules;
            s.promises.Add(new PromiseState { id = "legacy-word", fromId = friend, toId = s.playerId,
                kind = kind, status = PromiseStatus.Active, week = 2, expiresWeek = 0 });
            if (rules == 1)
            {
                s.unifiedCommitments.Add(Deal(s, "unrelated-canonical-pair", Other(s, 1), Other(s, 2)));
                Assert.That(UnifiedCommitments.ValidateRecords(s, out string error), Is.True, error);
            }
            AssertTheirWord(s, friend, promised);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProspectiveTheirWordRejectsRawLegacySafetyPromiseAndDealMirrors(bool dealMirror)
        {
            var s = State(); string friend = OneFriend(s);
            s.unifiedCommitments.Add(Promise(s, "actual-canonical-word", friend, s.playerId));
            if (dealMirror) s.deals.Add(LegacyDeal(s, "forbidden-deal-mirror", friend, s.playerId, DealKind.SafetyAgreement));
            else s.promises.Add(new PromiseState { id = "forbidden-promise-mirror", fromId = friend, toId = s.playerId,
                kind = PromiseKind.Safety, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1 });
            Assert.That(UnifiedCommitments.ValidateRecords(s, out string error), Is.False);
            Assert.That(error, Does.Contain("must not mirror"));
            var cycle = new StorylineState { id = "reader-play", templateId = "their-word",
                cast = new List<StoryRoleState> { new StoryRoleState { role = "FRIEND", contestantId = friend } } };
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => EpisodeEngine.ProgressOf(s, cycle));
            Assert.Throws<ArgumentException>(() => Cast(s, "their-word"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void APrivateNpcPairCannotWinThePlayersTheirWord()
        {
            var s = State(); string friend = OneFriend(s);
            s.unifiedCommitments.Add(Deal(s, "private-pair", friend, Other(s, 1)));
            AssertTheirWord(s, friend, false);
        }

        [Test]
        public void RuleZeroTheirWordIgnoresCanonicalRowsWithoutNewValidation()
        {
            var s = State(); string friend = OneFriend(s);
            s.unifiedCommitments.Add(Promise(s, "disabled-word", friend, s.playerId));
            s.unifiedCommitmentRulesVersion = 0;
            AssertTheirWord(s, friend, false);
        }

        [TestCase(UnifiedCommitments.DealPolicy, true)]
        [TestCase(UnifiedCommitments.PromisePolicy, false)]
        public void ConfessionPrefersAnActualCollaboratingDealNotAUnilateralPromise(string policy, bool collaborates)
        {
            var s = ConfessionState(); string ally = Other(s), partner = Other(s, 2), close = Other(s, 1);
            s.unifiedCommitments.Add(policy == UnifiedCommitments.DealPolicy ? Deal(s, "collaborator", ally, partner)
                : Promise(s, "unilateral", ally, partner));
            AssertConfession(s, ally, collaborates ? partner : close);
        }

        [TestCase(DealStatus.Proposed, true)]
        [TestCase(DealStatus.Accepted, true)]
        [TestCase(DealStatus.Declined, false)]
        [TestCase(DealStatus.Expired, false)]
        [TestCase(DealStatus.Broken, false)]
        public void ConfessionRetainsBindingStatusesAndExistingFriendFallback(string status, bool collaborates)
        {
            var s = ConfessionState(); string ally = Other(s), partner = Other(s, 2), close = Other(s, 1);
            var row = Deal(s, "status-collaborator", ally, partner); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, ally, partner); else row.status = status;
            AssertConfession(s, ally, collaborates ? partner : close);
        }

        [Test]
        public void ConfessionKeepsLegacyNonSafetyCollaboratorsAndOrdinalTies()
        {
            var s = ConfessionState(); string ally = Other(s), first = Other(s, 1), second = Other(s, 2);
            Score(s, ally, first, 10); Score(s, ally, second, 10);
            s.deals.Add(LegacyDeal(s, "legacy-final-two", ally, first, DealKind.FinalTwo));
            s.unifiedCommitments.Add(Deal(s, "canonical-safety", ally, second));
            AssertConfession(s, ally, string.CompareOrdinal(first, second) < 0 ? first : second);
        }

        [Test]
        public void EavesdroppingAndCrisisUseTheActualActiveNpcDealWithoutPublishingPrivateTerms()
        {
            var s = State(); string first = Other(s, 3), second = Other(s, 4);
            s.unifiedCommitments.Add(Deal(s, "private-active-pair", first, second));
            string before = Json(s);
            AssertPair(s, first, second);
            var crisis = HouseEventSources.Crisis(s, .01, 912);
            Assert.That(crisis.narrative, Does.Not.Contain("private-active-pair").And.Not.Contain("safety_agreement"));
            Assert.That(StoryCatalog.Find("caught-eavesdropping").beats[0].text, Does.Not.Contain("safety"));
            Assert.That(Json(s), Is.EqualTo(before), "Casting and source event creation do not publish knowledge or mutate authority.");
        }

        [TestCase(DealStatus.Proposed)]
        [TestCase(DealStatus.Accepted)]
        [TestCase(DealStatus.Expired)]
        [TestCase(DealStatus.Broken)]
        [TestCase(DealStatus.Fulfilled)]
        public void EavesdroppingAndCrisisRequireActiveNotMerelyBindingOrHistoricalDeals(string status)
        {
            var s = State(); string first = Other(s, 3), second = Other(s, 4);
            var row = Deal(s, "inactive-private-pair", first, second); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, first, second);
            else if (status == DealStatus.Fulfilled)
            {
                s.hohId = first;
                ApplyChanges(s, UnifiedCommitments.EvaluateFinalVetoSpared(s, first, new[] { Other(s), Other(s, 1) }));
            }
            else row.status = status;
            Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(status));
            var control = s.Clone(); control.unifiedCommitments.Clear();
            AssertPairMatches(s, control);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EavesdroppingAndCrisisExcludePlayerPairsAndDepartedHouseguests(bool departed)
        {
            var s = State(); string first = Other(s, 3), second = departed ? Other(s, 4) : s.playerId;
            s.unifiedCommitments.Add(Deal(s, "not-overhearable", first, second));
            if (departed) s.Find(second).status = ContestantStatus.Jury;
            var control = s.Clone(); control.unifiedCommitments.Clear();
            AssertPairMatches(s, control);
        }

        [Test]
        public void EavesdroppingAndCrisisSortAcrossBothStoresRatherThanPreferStorageOrder()
        {
            var s = State(); string first = Other(s, 3), second = Other(s, 4);
            s.deals.Add(LegacyDeal(s, "z-legacy", Other(s), Other(s, 1), DealKind.Partnership));
            s.unifiedCommitments.Add(Deal(s, "m-canonical", Other(s, 1), Other(s, 2)));
            s.unifiedCommitments.Add(Deal(s, "a-canonical", first, second));
            string before = Json(s); AssertPair(s, first, second); Assert.That(Json(s), Is.EqualTo(before));
            s.unifiedCommitments.Reverse(); before = Json(s);
            AssertPair(s, first, second); Assert.That(Json(s), Is.EqualTo(before));
            s.deals[0].id = "0-legacy"; before = Json(s);
            AssertPair(s, Other(s), Other(s, 1)); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void EavesdroppingAndCrisisDoNotCastAnNpcPromiseAsAnAgreement()
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "private-word", Other(s, 3), Other(s, 4)));
            var control = s.Clone(); control.unifiedCommitments.Clear();
            AssertPairMatches(s, control);
        }

        [Test]
        public void RuleZeroKeepsFullCrisisPayloadAndStoryFallbackExactly()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            s.deals.Add(LegacyDeal(s, "legacy-pair", Other(s, 2), Other(s, 1), DealKind.Partnership));
            s.unifiedCommitments.Add(Deal(s, "0-disabled", Other(s, 3), Other(s, 4)));
            var control = s.Clone(); control.unifiedCommitments.Clear();
            AssertPairMatches(s, control);
            s.deals.Clear(); control.deals.Clear(); AssertPairMatches(s, control);
        }

        [TestCase(2, 3, true)]
        [TestCase(3, 3, true)]
        [TestCase(2, 2, false)]
        public void ConversationUsesRealCanonicalDealSettlementWeekNotItsCreationWeek(int created, int settled, bool starts)
        {
            var s = ConversationState(); string who = ConversationNpc(s);
            var row = Deal(s, "actual-breach", s.playerId, who); row.createdWeek = created;
            row.origin = UnifiedCommitments.Lobby; row.expiresWeek = created + 1; s.unifiedCommitments.Add(row);
            int current = s.week; s.week = settled; Break(s, s.playerId, who); s.week = current;
            ChooseRoll(s, who, .12, .37); AssertConversation(s, who, starts);
        }

        [Test]
        public void ConversationAlsoRecognizesTheNpcAsTheActualReciprocalDealBreaker()
        {
            var s = ConversationState(); string who = ConversationNpc(s);
            s.unifiedCommitments.Add(Deal(s, "their-breach", s.playerId, who)); Break(s, who, s.playerId);
            ChooseRoll(s, who, .12, .37); AssertConversation(s, who, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConversationDoesNotInventADealTriggerForAPromiseOrAnUnrelatedNpcPair(bool unrelatedPair)
        {
            var s = ConversationState(); string who = ConversationNpc(s);
            if (unrelatedPair)
            {
                var npcs = s.Active.Where(c => !c.isPlayer && c.id != who).ToList();
                s.unifiedCommitments.Add(Deal(s, "others-breach", npcs[0].id, npcs[1].id)); Break(s, npcs[0].id, npcs[1].id);
            }
            else { s.unifiedCommitments.Add(Promise(s, "promise-not-deal", s.playerId, who)); Break(s, s.playerId, who); }
            ChooseRoll(s, who, .12, .37); AssertConversation(s, who, false);
        }

        [Test]
        public void ConversationAddsOneBrokenDealTermEvenWhenSeveralAliasesShareTheActualIncident()
        {
            var s = ConversationState(); string who = ConversationNpc(s);
            s.unifiedCommitments.Add(Deal(s, "first-deal", s.playerId, who));
            s.unifiedCommitments.Add(Deal(s, "second-deal", who, s.playerId)); Break(s, s.playerId, who);
            Assert.That(UnifiedCommitmentHistory.Breaches(s).Count, Is.EqualTo(1));
            ChooseRoll(s, who, .37, .62); AssertConversation(s, who, false);
        }

        [TestCase(2, 3, false)]
        [TestCase(3, 2, true)]
        public void ConversationKeepsLegacyBrokenDealsOriginalWeekMeaning(int created, int settled, bool starts)
        {
            var s = ConversationState(); string who = ConversationNpc(s);
            var row = LegacyDeal(s, "legacy-breach", s.playerId, who, DealKind.Partnership);
            row.status = DealStatus.Broken; row.week = created; row.settledWeek = settled; row.brokenById = s.playerId; s.deals.Add(row);
            ChooseRoll(s, who, .12, .37); AssertConversation(s, who, starts);
        }

        [Test]
        public void ConversationRejectsACanonicalBrokenLabelWhoseKeyNamesTheWrongWeek()
        {
            var s = ConversationState(); string who = ConversationNpc(s);
            s.unifiedCommitments.Add(Deal(s, "malformed-receipt", s.playerId, who)); Break(s, s.playerId, who);
            var row = s.unifiedCommitments.Single(); row.settlementEffectKey = row.settlementEffectKey.Replace("safety:3:", "safety:2:");
            Assert.That(UnifiedCommitments.ValidateRecords(s, out string error), Is.True, error);
            ChooseRoll(s, who, .12, .37); string before = Json(s);
            Assert.Throws<ArgumentException>(() => Invoke("TryStartFromConversation", s, who, EpisodeCommandKind.PersonalChat, null));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void RuleZeroConversationIgnoresCanonicalHistoryAndAddsNoNewValidation()
        {
            var s = ConversationState(); string who = ConversationNpc(s);
            s.unifiedCommitments.Add(Deal(s, "disabled-breach", s.playerId, who)); Break(s, s.playerId, who);
            s.unifiedCommitments.Single().settlementEffectKey = "disabled-invalid-key"; s.unifiedCommitmentRulesVersion = 0;
            ChooseRoll(s, who, .12, .37); AssertConversation(s, who, false);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VoteFlipGrudgesStillRequireAVoteFamilyNotCanonicalSafety(bool voteFamily)
        {
            var s = State(); string hoh = Other(s), voter = Other(s, 1), target = Other(s, 2), evicted = Other(s, 3);
            s.hohId = hoh; s.nominees.Add(target); s.nominees.Add(evicted); Score(s, hoh, target, -80); Score(s, hoh, evicted, 0);
            s.votes.Add(new VoteState { voterId = voter, targetId = evicted });
            s.unifiedCommitments.Add(Deal(s, "safety-is-not-vote", hoh, voter));
            if (voteFamily) s.deals.Add(LegacyDeal(s, "vote-control", hoh, voter, DealKind.VoteTogether));
            string authority = Authority(s); uint random = s.randomState;
            Invoke("StoryVotesRevealed", s, evicted);
            Assert.That(Grudges.Severity(s, hoh, voter), Is.EqualTo(voteFamily ? 40 : 0));
            Assert.That(Authority(s), Is.EqualTo(authority)); Assert.That(s.randomState, Is.EqualTo(random));
        }

        private static EpisodeState State(bool authoredCast = false)
        {
            var s = authoredCast ? SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.Regular }, 17)
                : ContentCatalog.Create(17);
            s.week = 3; s.hohId = s.playerId;
            s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1; s.unifiedCommitmentRulesVersion = 1;
            EpisodeEngine.EnableStory(s); foreach (var relationship in s.relationships) relationship.score = 0;
            s.promises.Clear(); s.deals.Clear(); s.unifiedCommitments.Clear(); s.alliances.Clear(); s.relationshipArcs.Clear();
            return s;
        }

        private static string Other(EpisodeState s, int index = 0) => s.Active.Where(c => !c.isPlayer)
            .OrderBy(c => c.id, StringComparer.Ordinal).Skip(index).First().id;
        private static void Score(EpisodeState s, string from, string to, double score) =>
            s.relationships.Single(r => r.fromId == from && r.toId == to).score = score;
        private static string OneFriend(EpisodeState s) { string friend = Other(s); Score(s, friend, s.playerId, 30); return friend; }
        private static UnifiedCommitmentState Promise(EpisodeState s, string id, string from, string to) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy, origin = UnifiedCommitments.StoryPromise,
            makerId = from, beneficiaryId = to, status = DealStatus.Active, createdWeek = s.week, expiresWeek = s.week + 1, trustImpact = DealTrust.Medium };
        private static UnifiedCommitmentState Deal(EpisodeState s, string id, string from, string to) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy, origin = UnifiedCommitments.StoryDeal,
            makerId = from, beneficiaryId = to, status = DealStatus.Active, createdWeek = s.week, expiresWeek = s.week,
            reciprocal = true, trustImpact = DealTrust.Medium };
        private static DealState LegacyDeal(EpisodeState s, string id, string from, string to, string kind) => new DealState {
            id = id, type = kind, proposerId = from, recipientId = to, status = DealStatus.Active, week = s.week, trustImpact = DealTrust.Medium };

        private static void Break(EpisodeState s, string actor, string wronged)
        {
            s.hohId = actor;
            var result = UnifiedCommitments.EvaluateNomination(s, "nomination", actor, new[] { wronged });
            Assert.That(result.Changes, Is.Not.Empty, "The fixture settles an actual eligible gateway decision."); ApplyChanges(s, result);
        }
        private static void ApplyChanges(EpisodeState s, UnifiedCommitmentEvaluation result)
        {
            foreach (var change in result.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }
        private static ArcBinding Cast(EpisodeState s, string template) =>
            StoryCatalog.Find(template).cast(new StoryContext(s, StoryAnchors.EvictionNight));

        private static void AssertTheirWord(EpisodeState s, string friend, bool promised)
        {
            string before = Json(s);
            var cycle = new StorylineState { id = "reader-play", templateId = "their-word",
                cast = new List<StoryRoleState> { new StoryRoleState { role = "FRIEND", contestantId = friend } } };
            Assert.That(EpisodeEngine.ProgressOf(s, cycle).Met, Is.EqualTo(promised));
            var binding = Cast(s, "their-word");
            if (promised) Assert.That(binding, Is.Null, "A friend who already gave their word is not offered again.");
            else { Assert.That(binding, Is.Not.Null); Assert.That(binding.Get("FRIEND"), Is.EqualTo(friend)); }
            Assert.That(Json(s), Is.EqualTo(before));
        }
        private static EpisodeState ConfessionState()
        {
            var s = State(); string ally = Other(s);
            s.relationshipArcs.Add(new RelationshipArcState { npcId = ally, arcType = "friendship", intensity = 75 });
            Score(s, ally, Other(s, 1), 80); Score(s, ally, Other(s, 2), 10); return s;
        }
        private static void AssertConfession(EpisodeState s, string ally, string offerer)
        {
            string before = Json(s); var binding = Cast(s, "the-confession"); Assert.That(binding, Is.Not.Null);
            Assert.That(binding.Get("ALLY"), Is.EqualTo(ally)); Assert.That(binding.Get("OFFERER"), Is.EqualTo(offerer));
            Assert.That(Json(s), Is.EqualTo(before));
        }
        private static void AssertPair(EpisodeState s, string first, string second)
        {
            var binding = Cast(s, "caught-eavesdropping"); Assert.That(binding, Is.Not.Null);
            Assert.That(binding.Get("FIRST"), Is.EqualTo(first)); Assert.That(binding.Get("SECOND"), Is.EqualTo(second));
            var crisis = HouseEventSources.Crisis(s, .01, 912); Assert.That(crisis, Is.Not.Null);
            Assert.That(crisis.involvedIds, Is.EqualTo(new[] { first, second }));
        }
        private static void AssertPairMatches(EpisodeState s, EpisodeState control)
        {
            string before = Json(s), controlBefore = Json(control);
            Assert.That(Json(Cast(s, "caught-eavesdropping")), Is.EqualTo(Json(Cast(control, "caught-eavesdropping"))));
            foreach (double roll in new[] { .01, .49, .99 })
                Assert.That(Json(HouseEventSources.Crisis(s, roll, 912)), Is.EqualTo(Json(HouseEventSources.Crisis(control, roll, 912))));
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(Json(control), Is.EqualTo(controlBefore));
        }

        private static EpisodeState ConversationState()
        {
            var s = State(true); s.story.romanceStorylines = false;
            var who = s.Active.First(c => !c.isPlayer && !StoryPeople.IsRealPerson(s, c.id)
                && Lore.Facet(s, c.id, Lore.Facets.Secret) != null);
            Assert.That(Bonds.Form(s, s.playerId, who.id, BondKinds.Confidant, BondStatus.Private, null), Is.Not.Null);
            var binding = EpisodeEngine.Castable(s, new StoryContext(s, StoryAnchors.Conversation, who.id, EpisodeCommandKind.PersonalChat),
                StoryCatalog.Find("what-they-left-out"));
            Assert.That(binding, Is.Not.Null, "A real installed catalog story is eligible before the threshold draw."); return s;
        }
        private static string ConversationNpc(EpisodeState s) => s.story.bonds.Single(b => b.kind == BondKinds.Confidant).bId;
        private static void ChooseRoll(EpisodeState s, string who, double lower, double upper)
        {
            string key = "w" + s.week + ":talk:" + who + ":" + s.nextSequence;
            for (uint seed = 1; seed <= 10000; seed++)
            {
                s.seed = seed; double roll = StoryRandom.Unit(s, key);
                if (roll >= lower && roll < upper) return;
            }
            Assert.Fail("A bounded deterministic seed search must separate the actual conversation threshold terms.");
        }
        private static void AssertConversation(EpisodeState s, string who, bool starts)
        {
            string authority = Authority(s), facts = Json(s.story.facts), known = Json(s.story.knownFacts);
            string relationships = Json(s.relationships); uint random = s.randomState;
            Invoke("TryStartFromConversation", s, who, EpisodeCommandKind.PersonalChat, null);
            Assert.That(s.storylines.Count, Is.EqualTo(starts ? 1 : 0));
            if (starts) Assert.That(s.storylines.Single().templateId, Is.EqualTo("what-they-left-out"));
            Assert.That(Authority(s), Is.EqualTo(authority)); Assert.That(Json(s.relationships), Is.EqualTo(relationships));
            Assert.That(Json(s.story.facts), Is.EqualTo(facts)); Assert.That(Json(s.story.knownFacts), Is.EqualTo(known));
            Assert.That(s.randomState, Is.EqualTo(random), "The source hook uses keyed draws, never the season stream.");
        }
        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Exercise the actual production hook rather than an invented seam.");
            try { method.Invoke(null, arguments); } catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static string Authority(EpisodeState s) => Json(new { s.promises, s.deals, s.unifiedCommitments });
        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
