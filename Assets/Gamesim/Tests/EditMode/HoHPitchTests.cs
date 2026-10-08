using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// E4's native incoming pitch adapter. These are simulation/JSON replay tests, not native
    /// SaveStore, Unity input, UI layout or shipping evidence. Legacy courtship remains the source
    /// action; the new cards and their free, per-card question are the approved native policy.
    /// </summary>
    public sealed class HoHPitchTests
    {
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static EpisodeState RoundTrip(EpisodeState state) => JsonConvert.DeserializeObject<EpisodeState>(Json(state),
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });

        private static EpisodeState House(int size = 8)
        {
            var s = EconomyRulesTests.Fresh(size);
            s.strategyRulesStartWeek = 1;
            s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableCommitments(s);
            EpisodeEngine.EnableRead(s);
            s.phase = EpisodePhase.Nomination;
            s.hohId = s.playerId;
            s.nominees.Clear();
            Valid(s);
            return s;
        }

        private static string Npc(EpisodeState s, int index = 0) => s.Active.Where(c => !c.isPlayer).ElementAt(index).id;
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        private static void Score(EpisodeState s, string from, string to, double value) =>
            RelationshipLedger.Move(s, from, to, value - s.Score(from, to));

        private static EpisodeState Crowned(EpisodeState s)
        {
            s.phase = EpisodePhase.HoH;
            s.competitionResolved = true;
            s.competitionScores = EpisodeEngine.CompetitionPlayers(s).Select(c =>
                new CompetitionScore { contestantId = c.id, score = c.id == s.hohId ? 10 : 1 }).ToList();
            Valid(s);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null,
            string text = null, string second = null) => new EpisodeCommand
        {
            id = "hoh-pitch-" + s.revision + "-" + kind, actorId = s.playerId, expectedRevision = s.revision,
            expectedPhase = s.phase, kind = kind, targetId = target, secondTargetId = second, text = text,
        };

        private static EpisodeState Apply(EpisodeState s, EpisodeCommandKind kind, string target = null,
            string text = null, string second = null)
        {
            var result = new EpisodeEngine(s).Apply(Command(s, kind, target, text, second));
            Assert.That(result.accepted, Is.True, result.reason);
            Valid(result.state);
            return result.state;
        }

        private static EpisodeState Answer(EpisodeState s, string key, string cardId = null) =>
            Apply(s, EpisodeCommandKind.ReplyToHouseguest, cardId ?? s.replyCards[0].id, key);

        private static EpisodeState Offered(int size = 8)
        {
            var s = House(size);
            NpcSocialActions.Court(s, s.Find(Npc(s)), s.Find(s.playerId));
            Assert.That(s.replyCards, Has.Count.EqualTo(1), "The real court hook must offer the pitch.");
            Valid(s);
            return s;
        }

        private static void Free(EpisodeState before, EpisodeState after)
        {
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints));
            Assert.That(after.moveInExtrasSpent, Is.EqualTo(before.moveInExtrasSpent));
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
        }

        private static void RejectedUnchanged(EpisodeState s, EpisodeCommand c)
        {
            var engine = new EpisodeEngine(s);
            string before = Json(engine.Snapshot), input = Json(s);
            var result = engine.Apply(c);
            Assert.That(result.accepted, Is.False);
            Assert.That(Json(result.state), Is.EqualTo(before));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(Json(s), Is.EqualTo(input));
        }

        [Test]
        public void AssessmentCopyUsesTheLearnedStandingNotAChangedHiddenView()
        {
            var s = Offered(); var card = s.replyCards[0];
            Score(s, card.fromId, card.aboutId, -60);
            Assert.That(HoHPitches.Assessment(s, card), Is.Null);
            var after = Answer(s, HoHPitches.FeelOutKey);
            string learned = HoHPitches.Assessment(after, after.replyCards[0]);
            Assert.That(learned, Does.Contain("do not trust"));
            uint random = after.randomState; string before = Json(after);
            Assert.That(HoHPitches.Assessment(after, after.replyCards[0]), Is.EqualTo(learned));
            Assert.That(Json(after), Is.EqualTo(before));
            Score(after, card.fromId, card.aboutId, 80);
            Assert.That(HoHPitches.Assessment(after, after.replyCards[0]), Is.EqualTo(learned));
            Assert.That(after.randomState, Is.EqualTo(random));
        }

        [Test]
        public void AnExpiredInformationRowNeverCausesThePitchReaderToRevealTheLiveScore()
        {
            var after = Answer(Offered(), HoHPitches.FeelOutKey);
            var card = after.replyCards[0];
            after.ledger.standings.Clear();
            Score(after, card.fromId, card.aboutId, -90);
            Valid(after);
            Assert.That(HoHPitches.Assessed(after, card), Is.True);
            Assert.That(HoHPitches.Assessment(after, card), Does.Contain("already felt out"));
            Assert.That(HoHPitches.Assessment(after, card), Does.Not.Contain("do not trust"));
            RejectedUnchanged(after, Command(after, EpisodeCommandKind.ReplyToHouseguest, card.id, HoHPitches.FeelOutKey));
        }

        [TestCase(8)] [TestCase(16)]
        public void ResolvedHohTransitionOffersExactlyOneCardPerCourtingNpc(int size)
        {
            var s = Crowned(House(size));
            var nomination = s.Clone(); nomination.phase = EpisodePhase.Nomination;
            string[] expected = nomination.Active.Where(c => !c.isPlayer && NpcAgendas.Of(nomination, c.id)?.kind == Agendas.Court)
                .Select(c => c.id).ToArray();
            Assert.That(expected.Length, Is.EqualTo(size - 1), "The plain fixture has no prior pact or safety deal.");
            string before = Json(s);
            var after = Apply(s, EpisodeCommandKind.Advance);
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(after.replyCards.Select(c => c.fromId), Is.EquivalentTo(expected));
            Assert.That(after.replyCards.All(c => c.kind == ReplyCards.Pitch && c.week == after.week), Is.True);
            Assert.That(after.replyCards.Select(c => c.id).Distinct().Count(), Is.EqualTo(expected.Length));
            Assert.That(Json(s), Is.EqualTo(before));
            var legacy = s.Clone(); legacy.economyRulesVersion = 0;
            var old = Apply(legacy, EpisodeCommandKind.Advance);
            Assert.That(old.replyCards, Is.Empty);
            Assert.That(after.randomState, Is.EqualTo(old.randomState), "Offering a pitch adds no courtship draw.");
            Assert.That(Json(after.relationships), Is.EqualTo(Json(old.relationships)));
        }

        [Test]
        public void ExistingAlliesAndSafetyPartnersDoNotAcquireCourtCardsAtTheTransition()
        {
            var s = House(); string ally = Npc(s), safe = Npc(s, 1);
            s.alliances.Add(new AllianceState { id = "pitch-existing-pact", name = "Existing pact", members = new List<string> { s.playerId, ally } });
            s.deals.Add(new DealState { id = "pitch-existing-safety", proposerId = safe, recipientId = s.playerId,
                type = DealKind.SafetyAgreement, status = DealStatus.Active, week = s.week, expiresWeek = s.week });
            var after = Apply(Crowned(s), EpisodeCommandKind.Advance);
            Assert.That(after.replyCards.Select(c => c.fromId), Is.EquivalentTo(s.Active.Where(c => !c.isPlayer && c.id != ally && c.id != safe).Select(c => c.id)));
        }

        [TestCase("legacy")] [TestCase("week-delayed")] [TestCase("strategy-off")] [TestCase("strategy-delayed")]
        [TestCase("agency-off")] [TestCase("agency-delayed")] [TestCase("not-hoh")] [TestCase("player-evicted")]
        [TestCase("social")] [TestCase("hoh")] [TestCase("veto-selection")] [TestCase("named")]
        public void AvailabilityAndOfferRequireTheExactIncomingPitchWindow(string boundary)
        {
            var s = House(); string speaker = Npc(s);
            Assert.That(HoHPitches.Available(s), Is.True);
            switch (boundary)
            {
                case "legacy": s.economyRulesVersion = 0; break;
                case "week-delayed": s.weekRulesStartWeek = 2; break;
                case "strategy-off": s.strategyRulesStartWeek = 0; break;
                case "strategy-delayed": s.strategyRulesStartWeek = 2; break;
                case "agency-off": s.agencyRulesStartWeek = 0; break;
                case "agency-delayed": s.agencyRulesStartWeek = 2; break;
                case "not-hoh": s.hohId = speaker; break;
                case "player-evicted": s.Find(s.playerId).status = ContestantStatus.Evicted; break;
                case "social": s.phase = EpisodePhase.Social; break;
                case "hoh": s.phase = EpisodePhase.HoH; break;
                case "veto-selection": s.phase = EpisodePhase.VetoSelection; break;
                case "named": s.nominees = new List<string> { speaker, Npc(s, 1) }; break;
            }
            // Availability is also queried while rendering incomplete fixtures; it must be read-only.
            string before = Json(s);
            Assert.That(HoHPitches.Available(s), Is.False);
            HoHPitches.Offer(s, speaker);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void NullAvailabilityAndInvalidSpeakersNeverOfferACard()
        {
            Assert.That(HoHPitches.Available(null), Is.False);
            var s = House(); string gone = Npc(s); s.Find(gone).status = ContestantStatus.Evicted;
            foreach (string speaker in new[] { null, "missing", s.playerId, gone })
            {
                string before = Json(s);
                HoHPitches.Offer(s, speaker);
                Assert.That(Json(s), Is.EqualTo(before), speaker ?? "null");
            }
        }

        [Test]
        public void OfferSelectsTheStrongestEligibleNonallyAndKeepsTheStoredSuggestion()
        {
            var s = House(); string speaker = Npc(s), ally = Npc(s, 1), desired = Npc(s, 2);
            s.alliances.Add(new AllianceState { id = "pitch-speaker-pact", name = "Their pact", members = new List<string> { speaker, ally } });
            s.Find(ally).hohWins = 5; s.Find(desired).hohWins = 4;
            string expected = ThreatAssessment.RankedTargets(s, speaker)
                .First(id => id != s.playerId && !s.Allied(speaker, id));
            HoHPitches.Offer(s, speaker);
            Assert.That(s.replyCards.Single().aboutId, Is.EqualTo(expected));
            Assert.That(expected, Is.Not.EqualTo(ally).And.Not.EqualTo(speaker).And.Not.EqualTo(s.playerId));
            string stored = s.replyCards.Single().aboutId;
            s.Find(Npc(s, 3)).hohWins = 100;
            string before = Json(s);
            HoHPitches.Offer(s, speaker);
            Assert.That(Json(s), Is.EqualTo(before), "An already offered card cannot reroll or acquire a new ID.");
            Assert.That(RoundTrip(s).replyCards.Single().aboutId, Is.EqualTo(stored));
        }

        [Test]
        public void EqualThreatsUseStableOrdinalIdentityRatherThanCastOrder()
        {
            var s = House(); string speaker = Npc(s);
            foreach (var c in s.contestants) { c.stats = new ContestantStats(); c.traits.Clear(); c.hohWins = c.vetoWins = 0; }
            foreach (var edge in s.relationships) { edge.score = 0; edge.events.Clear(); }
            var eligible = s.Active.Where(c => c.id != s.playerId && c.id != speaker).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Assert.That(eligible.Select(id => ThreatAssessment.Total(s, speaker, id)).Distinct().Count(), Is.EqualTo(1));
            var reverse = s.Clone(); reverse.contestants.Reverse(); reverse.relationships.Reverse();
            HoHPitches.Offer(s, speaker); HoHPitches.Offer(reverse, speaker);
            Assert.That(s.replyCards.Single().aboutId, Is.EqualTo(eligible[0]));
            Assert.That(reverse.replyCards.Single().aboutId, Is.EqualTo(eligible[0]));
        }

        [TestCase("promise-safety", 8)] [TestCase("hear", 0)] [TestCase("turn-down", -5)]
        public void FinalAnswersKeepTheirOwnEffectsAndSpendNoConversationEvenAtTheWindowLimit(string key, int impact)
        {
            var s = Offered(); var card = s.replyCards[0];
            s.windowActions[Windows.AfterHoH] = EpisodeEngine.SocialActionBudget(s);
            s.outOfPhaseSocialActions = s.windowActions[Windows.AfterHoH];
            string before = Json(s);
            var after = Answer(s, key);
            Assert.That(after.replyCards, Is.Empty);
            var row = after.ledger.replies.Single();
            Assert.That((row.week, row.cardId, row.kind, row.fromId, row.listenerId, row.replyKey, row.toThem, row.promised),
                Is.EqualTo((s.week, card.id, ReplyCards.Pitch, card.fromId, card.aboutId, key, (double)impact, false)));
            Assert.That(ReplyCards.Find(ReplyCards.Pitch, key).ToThem, Is.EqualTo(impact));
            double adjusted = WebRules.RelationshipDelta(impact, s.Find(s.playerId).stats.social, true);
            Assert.That(after.Score(s.playerId, card.fromId) - s.Score(s.playerId, card.fromId), Is.EqualTo(adjusted).Within(1e-9));
            Assert.That(after.ledger.claims, Is.Empty); Assert.That(after.ledger.ballots, Is.Empty);
            Assert.That(after.deals, Is.Empty, "A safety promise is not a negotiated deal.");
            Assert.That(after.promises.Count, Is.EqualTo(key == "promise-safety" ? 1 : 0));
            Free(s, after); Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(-50, KnownOdds.Cold)] [TestCase(0, KnownOdds.Unsure)] [TestCase(50, KnownOdds.Warm)]
        public void FeelingOutDisclosesOnlyTheStoredTargetsAssessmentWithoutRngOrWeeklyReadDebit(double score, string band)
        {
            var s = Offered(); var card = s.replyCards.Single(); Assert.That(card.aboutId, Is.Not.Null);
            Score(s, card.fromId, card.aboutId, score);
            string before = Json(s), relations = Json(s.relationships);
            var after = Answer(s, HoHPitches.FeelOutKey);
            Assert.That(Json(after.replyCards), Is.EqualTo(Json(s.replyCards)));
            Assert.That(HoHPitches.Assessed(after, after.replyCards.Single()), Is.True);
            Assert.That(HoHPitches.Assessed(s, card), Is.False);
            var receipt = after.ledger.replies.Single();
            Assert.That((receipt.week, receipt.cardId, receipt.kind, receipt.fromId, receipt.replyKey, receipt.toThem, receipt.promised),
                Is.EqualTo((s.week, card.id, ReplyCards.Pitch, card.fromId, HoHPitches.FeelOutKey, 0d, false)));
            var standing = after.ledger.standings.Single();
            Assert.That((standing.fromId, standing.toId, standing.source, standing.score), Is.EqualTo((card.fromId, card.aboutId, ClaimSource.Told, score)));
            var info = after.events.Last(e => e.kind == "information");
            Assert.That(info.audienceIds, Is.EquivalentTo(new[] { s.playerId, card.fromId }));
            Assert.That(info.text, Does.Contain(s.Find(card.aboutId).name));
            Assert.That(after.memories.Any(m => m.ownerId == s.playerId && m.subjectId == card.fromId && m.isPrivate && m.text == info.text), Is.True);
            Assert.That(KnownOdds.Band(after, card.fromId, card.aboutId), Is.EqualTo(band));
            Assert.That(EpisodeEngine.ReadThisWeek(after, card.fromId), Is.False);
            Assert.That(EpisodeEngine.AskedThisWeek(after, card.fromId), Is.False);
            Assert.That(after.randomState, Is.EqualTo(s.randomState));
            Assert.That(Json(after.relationships), Is.EqualTo(relations));
            Assert.That(after.ledger.claims, Is.Empty); Assert.That(after.promises, Is.Empty); Assert.That(after.deals, Is.Empty);
            Free(s, after); Assert.That(Json(s), Is.EqualTo(before));
            RejectedUnchanged(after, Command(after, EpisodeCommandKind.ReplyToHouseguest, card.id, HoHPitches.FeelOutKey));
        }

        [Test]
        public void ANoTargetPitchReadsTheSpeakersViewOfThePlayerWithoutConsumingTheWeeklyRead()
        {
            var s = House(); string speaker = Npc(s);
            s.alliances.Add(new AllianceState { id = "pitch-all-other-npcs", name = "Other people", members = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList() });
            HoHPitches.Offer(s, speaker);
            Assert.That(s.replyCards.Single().aboutId, Is.Null);
            Score(s, speaker, s.playerId, -50);
            var after = Answer(s, HoHPitches.FeelOutKey);
            var row = after.ledger.standings.Single();
            Assert.That((row.fromId, row.toId, row.source, row.score), Is.EqualTo((speaker, s.playerId, ClaimSource.Told, -50d)));
            Assert.That(EpisodeEngine.ReadThisWeek(after, speaker), Is.False);
            Assert.That(EpisodeEngine.AskedThisWeek(after, speaker), Is.False);
            Assert.That(after.randomState, Is.EqualTo(s.randomState));
            Free(s, after);
        }

        [Test]
        public void AnEarlierRealWeeklyReadDoesNotSpendTheIncomingCardsSeparateQuestion()
        {
            var s = House(); string speaker = Npc(s); s.phase = EpisodePhase.Social; s.hohId = null;
            var read = Apply(s, EpisodeCommandKind.ReadPerson, speaker);
            Assert.That(EpisodeEngine.ReadThisWeek(read, speaker), Is.True);
            read.hohId = read.playerId;
            var offered = Apply(Crowned(read), EpisodeCommandKind.Advance);
            var card = offered.replyCards.Single(c => c.fromId == speaker);
            int priorReads = offered.ledger.standings.Count(r => r.source == ClaimSource.Read || r.source == ClaimSource.Missed);
            var after = Answer(offered, HoHPitches.FeelOutKey, card.id);
            Assert.That(HoHPitches.Assessed(after, after.replyCards.Single(c => c.id == card.id)), Is.True);
            Assert.That(EpisodeEngine.ReadThisWeek(after, speaker), Is.True);
            Assert.That(after.ledger.standings.Count(r => r.source == ClaimSource.Read || r.source == ClaimSource.Missed), Is.EqualTo(priorReads));
            Assert.That(after.randomState, Is.EqualTo(offered.randomState));
            Free(offered, after);
        }

        [TestCase("promise-safety")] [TestCase("hear")] [TestCase("turn-down")] [TestCase("feel-out")]
        public void JsonRestorationReplaysTheExactCommittedResultAndKeepsPerCardInspectionState(string key)
        {
            var initial = Offered(); var inspected = Answer(initial, HoHPitches.FeelOutKey);
            var s = key == HoHPitches.FeelOutKey ? initial : inspected;
            var restored = RoundTrip(s); Valid(restored);
            Assert.That(Json(restored), Is.EqualTo(Json(s)));
            if (key != HoHPitches.FeelOutKey) Assert.That(HoHPitches.Assessed(restored, restored.replyCards.Single()), Is.True);
            Assert.That(Json(Answer(restored, key)), Is.EqualTo(Json(Answer(s, key))));
        }

        [Test]
        public void AFullLedgerCannotForgetAnInspectionBeforeItsBoundedLiveCardExpires()
        {
            var s = Apply(Crowned(House(16)), EpisodeCommandKind.Advance);
            var cards = s.replyCards.Select(c => c.Clone()).ToList();
            Assert.That(cards, Has.Count.EqualTo(15));
            for (int i = 0; i < SeasonLedger.MostRows; i++) s.ledger.replies.Add(new ReplyRow
            {
                week = s.week, cardId = "historical-reply-" + i, kind = ReplyCards.Confrontation,
                fromId = cards[0].fromId, replyKey = "apologize", toThem = 10,
            });
            Valid(s);
            var first = cards[0];
            foreach (var card in cards)
            {
                Assert.That(ReplyCards.Pending(s).id, Is.EqualTo(card.id), "Use the same FIFO authority as the visible card.");
                s = Answer(s, HoHPitches.FeelOutKey, card.id);
                var restored = RoundTrip(s); Valid(restored);
                RejectedUnchanged(restored, Command(restored, EpisodeCommandKind.ReplyToHouseguest, card.id, HoHPitches.FeelOutKey));
                Assert.That(HoHPitches.Assessed(restored, restored.replyCards[0]), Is.True);
                s = restored;
                s = Answer(s, "hear", card.id);
                Assert.That(HoHPitches.Assessed(s, first), Is.True, "Even the first receipt remains within this nomination's bounded lifetime.");
                Assert.That(s.ledger.replies.Count, Is.EqualTo(SeasonLedger.MostRows));
            }
            Assert.That(s.ledger.dropped, Is.EqualTo(2 * cards.Count));
            Assert.That(s.replyCards, Is.Empty);
            string finished = Json(s);
            foreach (var card in cards) HoHPitches.Offer(s, card.fromId);
            Assert.That(Json(s), Is.EqualTo(finished), "Retained receipts also prevent reoffering consumed pitches this week.");
        }

        [Test]
        public void SafetyAnswerUsesTheExistingPromiseAndNominationReallyBreaksIt()
        {
            var s = Offered(); string speaker = s.replyCards[0].fromId;
            var promised = Answer(s, "promise-safety"); var promise = promised.promises.Single();
            Assert.That((promise.fromId, promise.toId, promise.kind, promise.status, promise.week, promise.expiresWeek, promise.targetId),
                Is.EqualTo((s.playerId, speaker, PromiseKind.Safety, PromiseStatus.Active, s.week, s.week + 1, (string)null)));
            Assert.That(promised.ledger.replies.Single().promised, Is.False, "This field remains a vote-promise receipt.");
            string other = promised.Active.First(c => !c.isPlayer && c.id != speaker).id;
            var nominated = Apply(promised, EpisodeCommandKind.Nominate, speaker, second: other);
            var broken = nominated.promises.Single(p => p.id == promise.id);
            Assert.That((broken.status, broken.brokenById, broken.settledWeek), Is.EqualTo((PromiseStatus.Broken, s.playerId, s.week)));
            Assert.That(nominated.Score(speaker, s.playerId), Is.LessThan(promised.Score(speaker, s.playerId)));
        }

        [Test]
        public void ReassuringAnAlreadyPromisedSpeakerNeverDuplicatesOrExtendsThePromise()
        {
            var s = Offered(); string speaker = s.replyCards[0].fromId;
            s.promises.Add(new PromiseState { id = "pitch-prior-safety", kind = PromiseKind.Safety, fromId = s.playerId,
                toId = speaker, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1 });
            string prior = Json(s.promises);
            var after = Answer(s, "promise-safety");
            Assert.That(Json(after.promises), Is.EqualTo(prior));
            Assert.That(after.replyCards, Is.Empty); Assert.That(after.ledger.replies.Single().promised, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void AFullPromiseRecordRejectsOnlyNewSafetyAndKeepsAnExistingPromiseUsable(bool alreadyPromised)
        {
            var s = Offered(); string speaker = s.replyCards[0].fromId;
            for (int i = 0; i < 200; i++) s.promises.Add(new PromiseState
            {
                id = "pitch-cap-promise-" + i, fromId = s.playerId, toId = speaker, kind = PromiseKind.Safety,
                status = alreadyPromised && i == 0 ? PromiseStatus.Active : PromiseStatus.Expired,
                week = s.week, expiresWeek = s.week + 1,
            });
            Valid(s);
            if (alreadyPromised)
            {
                var after = Answer(s, "promise-safety");
                Assert.That(Json(after.promises), Is.EqualTo(Json(s.promises)));
                Assert.That(after.replyCards, Is.Empty);
            }
            else
            {
                RejectedUnchanged(s, Command(s, EpisodeCommandKind.ReplyToHouseguest, s.replyCards[0].id, "promise-safety"));
                var after = Answer(s, "hear");
                Assert.That(after.replyCards, Is.Empty, "A full promise record never forces the player to promise.");
                Assert.That(Json(after.promises), Is.EqualTo(Json(s.promises)));
            }
        }

        [TestCase("week")] [TestCase("card")] [TestCase("kind")] [TestCase("speaker")] [TestCase("key")]
        public void AnUnrelatedLedgerReceiptDoesNotSpendThisCardsInspection(string changed)
        {
            var s = Offered(); var card = s.replyCards.Single();
            var receipt = new ReplyRow { week = card.week, cardId = card.id, kind = ReplyCards.Pitch,
                fromId = card.fromId, replyKey = HoHPitches.FeelOutKey };
            s.ledger.replies.Add(receipt);
            Assert.That(HoHPitches.Assessed(s, card), Is.True);
            switch (changed)
            {
                case "week": receipt.week++; break;
                case "card": receipt.cardId += "-other"; break;
                case "kind": receipt.kind = ReplyCards.Confrontation; break;
                case "speaker": receipt.fromId = Npc(s, 1); break;
                case "key": receipt.replyKey = "hear"; break;
            }
            Assert.That(HoHPitches.Assessed(s, card), Is.False);
        }

        [TestCase("promise-safety")] [TestCase("hear")] [TestCase("turn-down")]
        public void AFinalAnswerCannotBeReplayedOrReofferedToFarmAnotherCard(string key)
        {
            var s = Offered(); var card = s.replyCards.Single(); var engine = new EpisodeEngine(s);
            var command = Command(s, EpisodeCommandKind.ReplyToHouseguest, card.id, key);
            Assert.That(engine.Apply(command).accepted, Is.True);
            string committed = Json(engine.Snapshot);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            var after = engine.Snapshot;
            RejectedUnchanged(after, Command(after, EpisodeCommandKind.ReplyToHouseguest, card.id, key));
            HoHPitches.Offer(after, card.fromId);
            Assert.That(Json(after), Is.EqualTo(committed), "A completed card is not an unbounded new incoming opportunity.");
        }

        [Test]
        public void ActualNominationsExpireEveryUnansweredPitchAndItsRetainedCommand()
        {
            var s = Apply(Crowned(House()), EpisodeCommandKind.Advance);
            var first = s.replyCards[0]; var inspected = Answer(s, HoHPitches.FeelOutKey, first.id);
            string firstPick = Npc(inspected), secondPick = Npc(inspected, 1);
            var nominated = Apply(inspected, EpisodeCommandKind.Nominate, firstPick, second: secondPick);
            Assert.That(nominated.replyCards, Is.Empty);
            Assert.That(HoHPitches.Available(nominated), Is.False);
            Assert.That(nominated.ledger.replies.Count, Is.EqualTo(1), "Ignored pitches do not fabricate an answer.");
            RejectedUnchanged(nominated, Command(nominated, EpisodeCommandKind.ReplyToHouseguest, first.id, "promise-safety"));
            Assert.That(nominated.promises, Is.Empty);
        }

        [Test]
        public void RejectedNominationDoesNotExpireCardsAndAnUnpresentedCardHasNoCommandAuthority()
        {
            var s = Apply(Crowned(House()), EpisodeCommandKind.Advance);
            string firstPick = Npc(s);
            RejectedUnchanged(s, Command(s, EpisodeCommandKind.Nominate, firstPick, second: firstPick));
            var later = s.replyCards[1];
            RejectedUnchanged(s, Command(s, EpisodeCommandKind.ReplyToHouseguest, later.id, "promise-safety"));
            RejectedUnchanged(s, Command(s, EpisodeCommandKind.ReplyToHouseguest, later.id, HoHPitches.FeelOutKey));
        }

        [Test]
        public void TwoPendingCardsFromTheSameSpeakerAreNotAValidSavedQueue()
        {
            var s = Offered(); var copy = s.replyCards.Single().Clone(); copy.id += "-duplicate";
            s.replyCards.Add(copy);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
        }

        [TestCase("vote-promise")] [TestCase("wrong-impact")] [TestCase("unknown-key")]
        [TestCase("speaker-player")] [TestCase("listener-self")] [TestCase("listener-player")]
        [TestCase("legacy")] [TestCase("before-rules")] [TestCase("duplicate-answer")]
        [TestCase("duplicate-inspection")] [TestCase("inspection-impact")]
        public void InvalidPitchReceiptsCannotLoadAsAuthorityOrInventARecordedOutcome(string boundary)
        {
            var s = Answer(Offered(), HoHPitches.FeelOutKey);
            s = Answer(s, "hear");
            Assert.That(s.replyCards, Is.Empty, "Only the historical ledger is under test, not pending-card eligibility.");
            var inspection = s.ledger.replies.Single(r => r.replyKey == HoHPitches.FeelOutKey);
            var answer = s.ledger.replies.Single(r => r.replyKey == "hear");
            switch (boundary)
            {
                case "vote-promise": answer.promised = true; break;
                case "wrong-impact": answer.toThem = 8; break;
                case "unknown-key": answer.replyKey = "invented-answer"; break;
                case "speaker-player": answer.fromId = s.playerId; break;
                case "listener-self": answer.listenerId = answer.fromId; break;
                case "listener-player": answer.listenerId = s.playerId; break;
                case "legacy": s.economyRulesVersion = 0; break;
                case "before-rules": s.weekRulesStartWeek = 2; break;
                case "duplicate-answer":
                    var duplicateAnswer = answer.Clone(); duplicateAnswer.cardId += "-other";
                    s.ledger.replies.Add(duplicateAnswer); break;
                case "duplicate-inspection":
                    var duplicateInspection = inspection.Clone(); duplicateInspection.cardId += "-other";
                    s.ledger.replies.Add(duplicateInspection); break;
                case "inspection-impact": inspection.toThem = 1; break;
            }
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False, boundary);
            Assert.That(error, Does.Contain("reply record"), "The ledger itself must reject this malformed receipt.");
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("missing-card")] [TestCase("unknown-key")] [TestCase("wrong-actor")]
        [TestCase("wrong-revision")] [TestCase("wrong-phase")]
        public void InvalidAnswerAuthorityRejectsWithoutChangingAnyState(string boundary)
        {
            var s = Offered(); var c = Command(s, EpisodeCommandKind.ReplyToHouseguest, s.replyCards[0].id, "hear");
            switch (boundary)
            {
                case "missing-card": c.targetId = "reply-does-not-exist"; break;
                case "unknown-key": c.text = "promise-to-evict"; break;
                case "wrong-actor": c.actorId = Npc(s); break;
                case "wrong-revision": c.expectedRevision++; break;
                case "wrong-phase": c.expectedPhase = EpisodePhase.Social; break;
            }
            RejectedUnchanged(s, c);
        }

        [TestCase("legacy")] [TestCase("agency-off")] [TestCase("strategy-off")] [TestCase("social")]
        [TestCase("npc-hoh")] [TestCase("nominees-set")] [TestCase("speaker-player")] [TestCase("speaker-missing")]
        [TestCase("speaker-evicted")] [TestCase("stale-week")] [TestCase("subject-self")] [TestCase("subject-hoh")]
        [TestCase("subject-missing")] [TestCase("subject-evicted")]
        public void InvalidPersistedPitchContextsFailValidationBeforeTheyCanAcquireAuthority(string boundary)
        {
            var s = Offered(); var card = s.replyCards.Single();
            switch (boundary)
            {
                case "legacy": s.economyRulesVersion = 0; break;
                case "agency-off": s.agencyRulesStartWeek = 0; break;
                case "strategy-off": s.strategyRulesStartWeek = 0; break;
                case "social": s.phase = EpisodePhase.Social; break;
                case "npc-hoh": s.hohId = Npc(s, 2); break;
                case "nominees-set": s.nominees = new List<string> { Npc(s), Npc(s, 1) }; break;
                case "speaker-player": card.fromId = s.playerId; break;
                case "speaker-missing": card.fromId = "missing"; break;
                case "speaker-evicted": s.Find(card.fromId).status = ContestantStatus.Evicted; break;
                case "stale-week": card.week++; break;
                case "subject-self": card.aboutId = card.fromId; break;
                case "subject-hoh": card.aboutId = s.playerId; break;
                case "subject-missing": card.aboutId = "missing"; break;
                case "subject-evicted": s.Find(card.aboutId).status = ContestantStatus.Evicted; break;
            }
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False, boundary);
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void NpcHohAndDisabledEconomyCourtshipKeepTheCompleteLegacyOutcome(bool npcHoh)
        {
            var old = House(); old.economyRulesVersion = 0;
            if (npcHoh) old.hohId = Npc(old, 1);
            else old.weekRulesStartWeek = 2;
            var unchanged = old.Clone(); unchanged.economyRulesVersion = 1;
            string speaker = Npc(old);
            NpcSocialActions.Court(old, old.Find(speaker), old.Find(old.hohId));
            NpcSocialActions.Court(unchanged, unchanged.Find(speaker), unchanged.Find(unchanged.hohId));
            Assert.That(old.replyCards, Is.Empty); Assert.That(unchanged.replyCards, Is.Empty);
            unchanged.economyRulesVersion = 0;
            Assert.That(Json(unchanged), Is.EqualTo(Json(old)));
        }
    }
}
