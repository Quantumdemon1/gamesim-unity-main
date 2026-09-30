using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 21's finale rules (ENDGAME-PLAN F4b/F5b): questions from the season's history with
    /// their receipts, the five responses and the sign by fit, the draws kept where they were, the
    /// final argument's lock and its capped jury term. Unity-free, so the dotnet subset runs it.
    /// </summary>
    public sealed class FinaleRulesTests
    {
        /// <summary>The finale fixture EpisodeFinaleTests uses: the player the final Head of Household, three jurors.</summary>
        private static EpisodeState FinalThree(bool rules = true)
        {
            var state = ContentCatalog.Create(337); state.week = 4; state.phase = EpisodePhase.FinalEviction;
            foreach (var actor in state.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            state.hohId = state.playerId; state.finalPart1WinnerId = state.playerId; state.finalPart2WinnerId = state.contestants[1].id;
            if (rules) EpisodeEngine.EnableFinale(state);
            return state;
        }

        /// <summary>
        /// The first juror to ask: the jury in cast order, as the engine takes them, and the third
        /// finalist the final eviction sends there sits first in it.
        /// </summary>
        private static ContestantState FirstJuror(EpisodeState state) => state.contestants[2];

        private static EpisodeState EnterQuestioning(EpisodeState state)
        {
            var command = EpisodeEngineTests.Command(state, EpisodeCommandKind.FinalEvict); command.targetId = state.contestants[2].id;
            var result = new EpisodeEngine(state).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static CommandResult Answer(EpisodeEngine engine, string response)
        {
            var state = engine.Snapshot;
            var command = EpisodeEngineTests.Command(state, EpisodeCommandKind.AnswerJury);
            command.targetId = state.juryExchanges[state.juryQuestionIndex].questionerId;
            command.secondTargetId = response;
            return engine.Apply(command);
        }

        private static CommandResult Lock(EpisodeEngine engine, string theme, params string[] references)
        {
            var command = EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.LockFinalArgument);
            command.secondTargetId = theme;
            command.text = FinalArgument.JoinReferences(references);
            return engine.Apply(command);
        }

        /// <summary>A record with something in it: two wins, a kept promise, and never nominated.</summary>
        private static EpisodeState WithMoments(EpisodeState state)
        {
            state.ledger.competitions.Add(new CompetitionRow { week = 2, kind = "HoH", entry = CompetitionEntry.Played, field = 6, placement = 1, performance = 1, expectedWin = .2 });
            state.ledger.competitions.Add(new CompetitionRow { week = 3, kind = "Veto", entry = CompetitionEntry.Played, field = 6, placement = 1, performance = 1, expectedWin = .2 });
            state.promises.Add(new PromiseState { id = "kept", fromId = state.playerId, toId = FirstJuror(state).id,
                kind = PromiseKind.Safety, status = PromiseStatus.Fulfilled, week = 2, expiresWeek = 3 });
            return state;
        }

        // ------------------------------------------------------------ questions

        [Test]
        public void AHistoryQuestionTakesTodaysTwoDrawsAndSurvivesAReload()
        {
            var state = FinalThree(); var rng = new SeededRandom(state.randomState);
            rng.NextDouble(); rng.NextDouble();
            var result = EnterQuestioning(state);
            Assert.That(result.randomState, Is.EqualTo(rng.State), "The category, then the wording: the two draws the catalogue's tone and order took.");
            var q = result.juryExchanges.Single();
            Assert.That(FinaleQuestions.Categories, Does.Contain(q.category));
            Assert.That(FinaleQuestions.Questions(q.category), Does.Contain(q.question));
            Assert.That(q.tone, Is.EqualTo(FinaleQuestions.Tone(q.category)));
            Assert.That(new[] { q.optionA, q.optionB, q.correctChoice }, Is.All.Null, "No answer key under the finale rules.");
            Assert.That(EpisodeValidation.TryValidate(result, out var error), Is.True, error);
            var reloaded = new EpisodeEngine(JsonConvert.DeserializeObject<EpisodeState>(JsonConvert.SerializeObject(result),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }));
            Assert.That(JsonConvert.SerializeObject(reloaded.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(result)));
        }

        [Test]
        public void AJurorWithNoReceiptAsksAComparisonAndIsOfferedFourResponses()
        {
            var state = EnterQuestioning(FinalThree());
            var first = state.juryExchanges.Single();
            Assert.That(first.category, Is.EqualTo(FinaleQuestions.Ownership), "The one the player just sent to the jury asks about it.");
            Assert.That(first.receiptKind, Is.EqualTo(FinaleQuestions.PowerReceipt));
            // A juror the player never touched shares only the player's own record - here the final
            // Head of Household, a strategy receipt - and one who asks about the heart, not the
            // game, has nothing to ask about: they weigh the player against the other finalist.
            var stranger = state.contestants[4];
            stranger.traits = new List<string> { "Emotional" };
            Assert.That(FinaleQuestions.Receipts(state, stranger.id).Select(r => r.category), Is.EqualTo(new[] { FinaleQuestions.Strategy }));
            var q = new JuryExchangeState();
            FinaleQuestions.Prepare(state, stranger, 1, q, () => .5);
            Assert.That(q.category, Is.EqualTo(FinaleQuestions.Comparison));
            Assert.That(q.receiptKind, Is.Null);
            Assert.That(q.receiptId, Is.Null);
            Assert.That(FinaleQuestions.Offered(q.category, q.receiptKind), Is.EqualTo(new[] { "own", "explain", "loyalty", "deflect" }));
        }

        [Test]
        public void APromiseThePlayerBrokeYieldsAnAccountabilityQuestionWithItsReceipt()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            juror.traits = new List<string> { "Loyal" };
            state.promises.Add(new PromiseState { id = "broken", fromId = state.playerId, toId = juror.id,
                kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 3, expiresWeek = 3 });
            var result = EnterQuestioning(state);
            var q = result.juryExchanges.Single();
            Assert.That(q.category, Is.EqualTo(FinaleQuestions.Accountability));
            Assert.That(q.receiptKind, Is.EqualTo(FinaleQuestions.PromiseReceipt));
            Assert.That(q.receiptId, Is.EqualTo("broken"));
            Assert.That(q.tone, Is.EqualTo("bitter"));
            Assert.That(FinaleQuestions.ReceiptLine(result, q), Is.EqualTo("Week 3 · you gave them your word, and broke it."));
        }

        [Test]
        public void ADealThePlayerDidNotBreakIsNoAccountability()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            juror.traits = new List<string> { "Loyal" };
            // The juror, as Head of Household, put the player up: their doing, not the player's.
            state.ledger.power.Add(new PowerRow { week = 2, hohId = juror.id, nominees = new List<string> { state.playerId, state.contestants[1].id },
                evicteeId = state.contestants[5].id, tally = new List<int> { 2, 1 } });
            state.deals.Add(new DealState { id = "safety", type = DealKind.SafetyAgreement, proposerId = state.playerId, recipientId = juror.id,
                status = DealStatus.Broken, week = 2, expiresWeek = 3 });
            Assert.That(FinaleQuestions.Receipts(state, juror.id).Any(r => r.category == FinaleQuestions.Accountability), Is.False);
            state.ledger.power[0].hohId = state.playerId;
            state.ledger.power[0].nominees = new List<string> { juror.id, state.contestants[1].id };
            var receipts = FinaleQuestions.Receipts(state, juror.id);
            Assert.That(receipts.Single(r => r.category == FinaleQuestions.Accountability).id, Is.EqualTo("safety"), "The player put them up: the player broke it.");
            Assert.That(receipts.Single(r => r.category == FinaleQuestions.Ownership).id, Is.EqualTo("2"));
        }

        [TestCase("own", true)] [TestCase("deflect", false)]
        public void TheSignIsTheFitAndTheAnswerTakesTodaysTwoDraws(string response, bool lands)
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            juror.traits = new List<string> { "Loyal" };
            state.promises.Add(new PromiseState { id = "broken", fromId = state.playerId, toId = juror.id,
                kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 3, expiresWeek = 3 });
            state = EnterQuestioning(state);
            var q = state.juryExchanges[0];
            double delta = lands ? 10 : -10;
            var rng = new SeededRandom(state.randomState);
            WebRules.ReciprocalDelta(delta, rng.NextDouble());
            WebRules.ReciprocalDelta(delta, rng.NextDouble());
            var engine = new EpisodeEngine(state);
            var result = Answer(engine, response);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.randomState, Is.EqualTo(rng.State), "Today's two: the reciprocal event and score.");
            Assert.That(result.state.Score(q.questionerId, state.playerId), Is.EqualTo(WebRules.ClampScore(state.Score(q.questionerId, state.playerId) + delta)));
            var answered = result.state.juryExchanges[0];
            Assert.That(answered.answerChoice, Is.EqualTo(response));
            Assert.That(answered.answer, Is.EqualTo(FinaleQuestions.Line(FinaleQuestions.Accountability, response)));
            string note = juror.name + (lands ? " took your answer well." : " was not moved by your answer.");
            Assert.That(result.state.events.Last(e => e.kind == "jury-answer").text, Is.EqualTo(note));
            Assert.That(FinaleQuestions.Note(juror.name, FinaleQuestions.Landed(result.state, answered)), Is.EqualTo(note), "The reaction rebuilt from the save.");
            Assert.That(EpisodeValidation.TryValidate(result.state, out var error), Is.True, error);
        }

        [Test]
        public void TheCatalogueAndAnUnofferedResponseAreRefusedUnderTheRulesAndChangeNothing()
        {
            var state = EnterQuestioning(FinalThree());
            var engine = new EpisodeEngine(state);
            string before = JsonConvert.SerializeObject(engine.Snapshot);
            foreach (var response in new[] { "A", "B", "truth", "shrug", null })
            {
                Assert.That(Answer(engine, response).accepted, Is.False, response ?? "null");
                Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before));
            }
        }

        [Test]
        public void ACallTheJurorNeverSawIsStrategyAndOffersTheTruth()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            juror.traits = new List<string> { "Strategic" };
            state.ledger.power.Add(new PowerRow { week = 3, hohId = state.contestants[1].id, nominees = new List<string> { state.contestants[5].id, state.contestants[4].id },
                evicteeId = state.contestants[5].id, tally = new List<int> { 2, 1 } });
            state.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "a1", callerId = state.playerId, targetId = state.contestants[5].id,
                followed = new List<string> { state.contestants[1].id } });
            state = EnterQuestioning(state);
            var q = state.juryExchanges[0];
            Assert.That(q.category, Is.EqualTo(FinaleQuestions.Strategy));
            Assert.That(q.receiptKind, Is.EqualTo(FinaleQuestions.CallReceipt));
            Assert.That(q.receiptId, Is.EqualTo("a1:3"));
            Assert.That(FinaleQuestions.Offered(q.category, q.receiptKind), Does.Contain("truth"));
            var result = Answer(new EpisodeEngine(state), "truth");
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(FinaleQuestions.Landed(result.state, result.state.juryExchanges[0]), Is.True);
        }

        [Test]
        public void TheFitTableIsFixed()
        {
            // Grievances: owning lands, deflecting costs, whoever asks.
            foreach (var theme in FinalArgument.Themes)
            {
                Assert.That(FinaleQuestions.Lands(theme, FinaleQuestions.Ownership, "own"), Is.True, theme);
                Assert.That(FinaleQuestions.Lands(theme, FinaleQuestions.Accountability, "deflect"), Is.False, theme);
                Assert.That(FinaleQuestions.Lands(theme, FinaleQuestions.Strategy, "explain"), Is.True, theme);
                Assert.That(FinaleQuestions.Lands(theme, FinaleQuestions.Personal, "loyalty"), Is.True, theme);
                Assert.That(FinaleQuestions.Lands(theme, FinaleQuestions.Comparison, "truth"), Is.True, theme);
            }
            // Otherwise what the juror values.
            Assert.That(FinaleQuestions.Lands(FinalArgument.Cerebral, FinaleQuestions.Comparison, "explain"), Is.True);
            Assert.That(FinaleQuestions.Lands(FinalArgument.Cerebral, FinaleQuestions.Comparison, "loyalty"), Is.False);
            Assert.That(FinaleQuestions.Lands(FinalArgument.Aggressive, FinaleQuestions.Social, "own"), Is.True);
            Assert.That(FinaleQuestions.Lands(FinalArgument.Sneaky, FinaleQuestions.Comparison, "deflect"), Is.True);
            Assert.That(FinaleQuestions.Lands(FinalArgument.Emotional, FinaleQuestions.Comparison, "own"), Is.False);
        }

        [Test]
        public void OldSeasonsKeepTheCatalogueAndRefuseAHistoryField()
        {
            var state = EnterQuestioning(FinalThree(rules: false));
            var q = state.juryExchanges.Single();
            Assert.That(q.optionA, Is.Not.Null);
            Assert.That(new[] { q.category, q.receiptKind, q.receiptId }, Is.All.Null);
            q.category = FinaleQuestions.Comparison;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, "Only a history question saves a category.");
        }

        // ------------------------------------------------------------ the argument

        [Test]
        public void TheLockIsAcceptedOnceSpendsNoDrawAndSurvivesAReload()
        {
            var state = EnterQuestioning(WithMoments(FinalThree()));
            var moments = FinalArgument.Moments(state).Select(m => m.reference).ToList();
            Assert.That(moments, Is.SupersetOf(new[] { "win:2:HoH", "win:3:Veto", "promise:kept", "hoh:4", "record:unnominated" }));
            var engine = new EpisodeEngine(state);
            var result = Lock(engine, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto", "hoh:4");
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.randomState, Is.EqualTo(state.randomState), "The lock draws nothing.");
            Assert.That(result.state.relationships.Select(r => r.score), Is.EqualTo(state.relationships.Select(r => r.score)), "and moves nobody.");
            Assert.That(result.state.finalArgument.theme, Is.EqualTo(FinalArgument.Aggressive));
            Assert.That(result.state.finalArgument.momentRefs, Is.EqualTo(new[] { "win:2:HoH", "win:3:Veto", "hoh:4" }));
            Assert.That(EpisodeValidation.TryValidate(result.state, out var error), Is.True, error);
            string after = JsonConvert.SerializeObject(engine.Snapshot);
            Assert.That(Lock(engine, FinalArgument.Cerebral, "win:2:HoH", "win:3:Veto", "hoh:4").accepted, Is.False, "Once.");
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(after));
            var reloaded = JsonConvert.DeserializeObject<EpisodeState>(after, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Assert.That(JsonConvert.SerializeObject(reloaded), Is.EqualTo(after));
        }

        [Test]
        public void TheLockIsRefusedOutsideItsRulesAndChangesNothing()
        {
            var state = EnterQuestioning(WithMoments(FinalThree()));
            var engine = new EpisodeEngine(state);
            string before = JsonConvert.SerializeObject(state);
            Assert.That(Lock(engine, "bravado", "win:2:HoH", "win:3:Veto", "hoh:4").accepted, Is.False, "A theme of the five.");
            Assert.That(Lock(engine, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto").accepted, Is.False, "Three moments.");
            Assert.That(Lock(engine, FinalArgument.Aggressive, "win:2:HoH", "win:2:HoH", "hoh:4").accepted, Is.False, "Three different ones.");
            Assert.That(Lock(engine, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto", "win:9:HoH").accepted, Is.False, "Moments of the record.");
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before));

            var old = new EpisodeEngine(EnterQuestioning(WithMoments(FinalThree(rules: false))));
            Assert.That(Lock(old, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto", "hoh:4").accepted, Is.False, "Not under the old rules.");

            var early = new EpisodeEngine(WithMoments(FinalThree()));
            Assert.That(Lock(early, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto", "promise:kept").accepted, Is.False, "Not before the final eviction.");
        }

        [Test]
        public void TheLockIsRefusedOnceTheSpeechIsIn()
        {
            var state = EnterQuestioning(WithMoments(FinalThree()));
            var engine = new EpisodeEngine(state);
            Assert.That(engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.SkipQuestioning)).accepted, Is.True);
            var speech = EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.SubmitSpeech); speech.text = "Mine.";
            Assert.That(engine.Apply(speech).accepted, Is.True);
            Assert.That(Lock(engine, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto", "hoh:4").accepted, Is.False);
        }

        [Test]
        public void TheArgumentCountsOnlyItsBackingMomentsWithTheJurorsWhoValueIt()
        {
            var state = EnterQuestioning(WithMoments(FinalThree()));
            var engine = new EpisodeEngine(state);
            Assert.That(Lock(engine, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto", "promise:kept").accepted, Is.True);
            state = engine.Snapshot;
            var competitor = state.contestants[3];
            competitor.traits = new List<string> { "Competitive" };
            var strategist = state.contestants[4];
            strategist.traits = new List<string> { "Strategic" };
            var other = state.Active.First(c => !c.isPlayer);
            Assert.That(FinalArgument.Term(state, competitor.id, state.playerId), Is.EqualTo(2 * FinalArgument.PerMoment), "Two wins back a competitor's theme; the promise does not.");
            Assert.That(FinalArgument.Term(state, strategist.id, state.playerId), Is.Zero, "A strategist values another theme.");
            Assert.That(FinalArgument.Term(state, competitor.id, other.id), Is.Zero, "Only the player argues.");
            double with = WebJuryVoting.Score(state, competitor.id, state.playerId);
            state.finalArgument.momentRefs = new List<string> { "win:2:HoH", "win:3:Veto", "hoh:4" };
            Assert.That(FinalArgument.Term(state, competitor.id, state.playerId), Is.EqualTo(2 * FinalArgument.PerMoment),
                "The final eviction was a strategist's move, so still two.");
            state.finalArgument.theme = FinalArgument.Cerebral;
            Assert.That(FinalArgument.Term(state, strategist.id, state.playerId), Is.EqualTo(FinalArgument.PerMoment));
            state.finalArgument.theme = FinalArgument.Aggressive;
            state.finalArgument.momentRefs = new List<string> { "win:2:HoH", "win:3:Veto", "promise:kept" };
            state.finaleRulesStartWeek = 0;
            Assert.That(FinalArgument.Term(state, competitor.id, state.playerId), Is.Zero, "No effect under the old rules.");
            Assert.That(WebJuryVoting.Score(state, competitor.id, state.playerId), Is.EqualTo(with - 2 * FinalArgument.PerMoment).Within(1e-9),
                "The term is the score's, and all of the difference.");
            Assert.That(FinalArgument.Cap, Is.LessThan(WebJuryVoting.FinalImpression), "Capped under the final impression.");
        }

        [Test]
        public void TheSpeechTemplatesTheThemeAndMomentsInsideTheEditorsLimit()
        {
            var state = EnterQuestioning(WithMoments(FinalThree()));
            var engine = new EpisodeEngine(state);
            Assert.That(Lock(engine, FinalArgument.Emotional, "promise:kept", "win:2:HoH", "record:unnominated").accepted, Is.True);
            string speech = FinalArgument.Speech(engine.Snapshot);
            var moments = FinalArgument.Moments(engine.Snapshot);
            Assert.That(speech, Does.StartWith("I played this game with my heart"));
            foreach (var reference in new[] { "promise:kept", "win:2:HoH", "record:unnominated" })
                Assert.That(speech, Does.Contain(moments.Single(m => m.reference == reference).said));
            Assert.That(speech.Length, Is.LessThanOrEqualTo(2000));
            Assert.That(speech.Any(ch => char.IsControl(ch) && ch != '\n'), Is.False);
            var submit = EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.SkipQuestioning);
            Assert.That(engine.Apply(submit).accepted, Is.True);
            var said = EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.SubmitSpeech); said.text = speech;
            Assert.That(engine.Apply(said).accepted, Is.True, "The template goes in through the speech's own command.");
        }

        [Test]
        public void AnArgumentBeforeTheFinalEvictionOrOnAnUnknownRowIsInvalid()
        {
            var state = WithMoments(FinalThree());
            state.finalArgument = new FinalArgumentState { theme = FinalArgument.Aggressive, momentRefs = new List<string> { "win:2:HoH" } };
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, "Not before the final eviction.");
            var questioning = EnterQuestioning(WithMoments(FinalThree()));
            questioning.finalArgument = new FinalArgumentState { theme = FinalArgument.Aggressive, momentRefs = new List<string> { "win:7:HoH" } };
            Assert.That(EpisodeValidation.TryValidate(questioning, out _), Is.False, "A reference names a row of the player's.");
            questioning.finalArgument.momentRefs = new List<string> { "win:2:HoH" };
            Assert.That(EpisodeValidation.TryValidate(questioning, out var error), Is.True, error);
        }

        [Test]
        public void ACallInTheJurorsOwnAllianceWasTheirsToHear()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            state.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "a1", callerId = state.playerId, targetId = state.contestants[5].id,
                followed = new List<string> { state.contestants[1].id } });
            Assert.That(FinaleQuestions.Receipts(state, juror.id).Select(r => r.kind), Does.Contain(FinaleQuestions.CallReceipt), "Outside the alliance, never seen.");
            state.alliances.Add(new AllianceState { id = "a1", name = "The Core", members = new List<string> { state.playerId, state.contestants[1].id, juror.id } });
            Assert.That(FinaleQuestions.Receipts(state, juror.id).Select(r => r.kind), Does.Not.Contain(FinaleQuestions.CallReceipt),
                "A member heard the call, even when they were left off its ballot.");
        }

        [Test]
        public void OnlyAPleaThePlayerRefusedIsJuryManagement()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            state.ledger.replies.Add(new ReplyRow { week = 2, cardId = "cold", kind = ReplyCards.Confrontation, fromId = juror.id, listenerId = state.playerId, replyKey = "deny", toThem = -6 });
            Assert.That(FinaleQuestions.Receipts(state, juror.id).Any(r => r.category == FinaleQuestions.JuryManagement), Is.False,
                "A confrontation answered coldly is not them coming to the player for help.");
            state.ledger.replies.Add(new ReplyRow { week = 3, cardId = "plea", kind = ReplyCards.Plea, fromId = juror.id, listenerId = state.playerId, replyKey = "refuse", toThem = -3 });
            var receipt = FinaleQuestions.Receipts(state, juror.id).Single(r => r.category == FinaleQuestions.JuryManagement);
            Assert.That(receipt.id, Is.EqualTo("plea"));
            var exchange = new JuryExchangeState { questionerId = juror.id, category = receipt.category, receiptKind = receipt.kind, receiptId = receipt.id };
            Assert.That(FinaleQuestions.ReceiptLine(state, exchange), Is.EqualTo("Week 3 · they asked you for your vote, and you said no."));
        }

        [Test]
        public void AQuestionClaimsNoMoreThanItsReceiptShows()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            var ownership = FinaleQuestions.Questions(FinaleQuestions.Ownership);
            var personal = FinaleQuestions.Questions(FinaleQuestions.Personal);
            state.ledger.power.Add(new PowerRow { week = 2, hohId = state.playerId, nominees = new List<string> { juror.id, state.contestants[5].id },
                evicteeId = state.contestants[5].id, tally = new List<int> { 2, 1 } });
            var putUp = new FinaleQuestions.Receipt { category = FinaleQuestions.Ownership, kind = FinaleQuestions.PowerReceipt, id = "2", week = 2 };
            Assert.That(FinaleQuestions.QuestionsFor(state, FinaleQuestions.Ownership, putUp, juror.id), Is.EqualTo(new[] { ownership[1] }),
                "Put up and survived: not the reason they sit on the jury.");
            state.ledger.power[0].evicteeId = juror.id;
            Assert.That(FinaleQuestions.QuestionsFor(state, FinaleQuestions.Ownership, putUp, juror.id), Is.EqualTo(ownership), "The week they went home.");
            var allies = new FinaleQuestions.Receipt { category = FinaleQuestions.Personal, kind = FinaleQuestions.AllianceReceipt, id = "a1", week = 1 };
            Assert.That(FinaleQuestions.QuestionsFor(state, FinaleQuestions.Personal, allies, juror.id), Is.EqualTo(new[] { personal[1] }),
                "Keeping faith is a promise's word.");
            var kept = new FinaleQuestions.Receipt { category = FinaleQuestions.Personal, kind = FinaleQuestions.PromiseReceipt, id = "kept", week = 2 };
            Assert.That(FinaleQuestions.QuestionsFor(state, FinaleQuestions.Personal, kept, juror.id), Is.EqualTo(personal));
        }

        [Test]
        public void AnAllianceThatBrokeIsNoBondToAskAbout()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            state.alliances.Add(new AllianceState { id = "a1", name = "The Core", active = false, members = new List<string> { state.playerId, juror.id } });
            state.ledger.alliances.Add(new AllianceRow { id = "a1", why = "a1/broken", startedWeek = 1, endedWeek = 2 });
            Assert.That(FinaleQuestions.Receipts(state, juror.id).Any(r => r.kind == FinaleQuestions.AllianceReceipt), Is.False);
            state.alliances[0].active = true;
            Assert.That(FinaleQuestions.Receipts(state, juror.id).Single(r => r.category == FinaleQuestions.Personal).id, Is.EqualTo("a1"), "One that still stands.");
        }

        [Test]
        public void TheQuietGameHasBothItsRecordsAndEveryMomentReadsApart()
        {
            var state = FinalThree();
            foreach (int week in new[] { 1, 2, 3 })
                state.ledger.power.Add(new PowerRow { week = week, hohId = state.contestants[1].id, nominees = new List<string> { state.contestants[3 + week % 3].id, state.contestants[5].id },
                    evicteeId = state.contestants[5].id, tally = new List<int> { 2, 1 } });
            var references = FinalArgument.Moments(state).Select(m => m.reference).ToList();
            Assert.That(references, Does.Contain("record:unnominated").And.Contain("record:off-the-block"),
                "Never nominated and three weeks clear are two moments, not one.");
            // Two alliances called the same vote the same week, and the week went their way.
            state.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "a1", callerId = state.playerId, targetId = state.contestants[5].id });
            state.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "a2", callerId = state.playerId, targetId = state.contestants[5].id });
            var texts = FinalArgument.Moments(state).Select(m => m.text).ToList();
            Assert.That(texts, Is.Unique, "Unnamed alliances are still told apart.");
            state.alliances.Add(new AllianceState { id = "a1", name = "The Core", members = new List<string> { state.playerId, state.contestants[1].id } });
            state.alliances.Add(new AllianceState { id = "a2", name = "Night Shift", members = new List<string> { state.playerId, state.contestants[3].id } });
            texts = FinalArgument.Moments(state).Select(m => m.text).ToList();
            Assert.That(texts, Does.Contain("Week 3: you called the vote in The Core, and " + state.contestants[5].name + " went."));
            Assert.That(texts, Is.Unique);
        }

        [Test]
        public void AJurorSwayedWithoutASpeechSaysTheSeasonMadeTheCase()
        {
            var state = EnterQuestioning(WithMoments(FinalThree()));
            var engine = new EpisodeEngine(state);
            Assert.That(Lock(engine, FinalArgument.Aggressive, "win:2:HoH", "win:3:Veto", "promise:kept").accepted, Is.True);
            state = engine.Snapshot;
            var competitor = state.contestants[3];
            competitor.traits = new List<string> { "Competitive" };
            state.relationships.RemoveAll(r => r.fromId == competitor.id);
            var player = state.Find(state.playerId);
            var other = state.Active.First(c => !c.isPlayer);
            Assert.That(FinalArgument.Term(state, competitor.id, player.id), Is.EqualTo(2 * FinalArgument.PerMoment));
            string silent = WebJuryVoting.Reason(state, competitor.id, player, other);
            Assert.That(new[] { "Their season was the kind of game I value.", "The game they played is the one I respect.", "Their record made the case on its own." },
                Does.Contain(silent), "No words about a speech nobody gave.");
            state.finalSpeeches.Add(new FinalSpeechState { speakerId = player.id, text = FinalArgument.Speech(state), isPlayerAuthored = true });
            Assert.That(new[] { "Their final argument was about what I value in this game.", "They made the case I came here to hear.", "What they said at the end spoke to me." },
                Does.Contain(WebJuryVoting.Reason(state, competitor.id, player, other)));
        }

        [Test]
        public void EveryThemeHasItsWordsAndAJurorsThemeIsTheirLeadTrait()
        {
            foreach (var theme in FinalArgument.Themes)
            {
                Assert.That(FinalArgument.Label(theme), Is.Not.Empty);
                Assert.That(FinalArgument.Claim(theme), Is.Not.Empty);
            }
            Assert.That(FinalArgument.Themes.Select(FinalArgument.Label).Distinct().Count(), Is.EqualTo(5));
            var juror = new ContestantState { traits = new List<string> { "Competitive", "Loyal" } };
            Assert.That(FinalArgument.ThemeOf(juror), Is.EqualTo(FinalArgument.Aggressive));
            juror.traits = new List<string>();
            Assert.That(FinalArgument.ThemeOf(juror), Is.EqualTo(FinalArgument.Cerebral), "No traits reads as Strategic, as the questioning reads it.");
            foreach (var category in FinaleQuestions.Categories)
                foreach (var response in FinaleQuestions.Offered(category, category == FinaleQuestions.Strategy ? FinaleQuestions.CallReceipt : null))
                    Assert.That(FinaleQuestions.Line(category, response), Is.Not.Null.And.Not.Empty, category + "/" + response);
            Assert.That(FinaleQuestions.Responses.Select(FinaleQuestions.Caption).Distinct().Count(), Is.EqualTo(5));
        }

        // ------------------------------------------------------------ the receipt on the screen (MOCKUP-PASS M11)

        private static JuryExchangeState Asked(ContestantState juror, string category, string kind, string id) =>
            new JuryExchangeState { questionerId = juror.id, category = category, receiptKind = kind, receiptId = id };

        [Test]
        public void ABallotReceiptReadsItsWeekItsKickerAndTheWeeksCount()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            var other = state.contestants[5];
            state.ledger.power.Add(new PowerRow { week = 3, hohId = state.contestants[1].id, nominees = new List<string> { juror.id, other.id },
                evicteeId = other.id, tally = new List<int> { 1, 4 } });
            state.ledger.ballots.Add(new BallotRow { week = 3, voterId = state.playerId, targetId = juror.id });
            var exchange = Asked(juror, FinaleQuestions.Ownership, FinaleQuestions.BallotReceipt, "3");
            Assert.That(FinaleQuestions.ReceiptLine(state, exchange), Is.EqualTo("Week 3 · you voted to evict " + juror.name + "."));
            Assert.That(FinaleQuestions.ReceiptWeek(state, exchange), Is.EqualTo(3));
            Assert.That(FinaleQuestions.Kicker(state, exchange), Is.EqualTo("Week 3 · you voted to evict them"), "The juror asking is them.");
            Assert.That(FinaleQuestions.ReceiptTally(state, exchange), Is.EqualTo("(4–1)"), "The evictee's votes first.");
            Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, exchange), Is.False, "They stayed: who went is the recap headline's to add.");
            Assert.That(FinaleQuestions.RecapAdds(state, exchange), Is.True);
            // The week they went: the line already says who.
            state.ledger.power[0].evicteeId = juror.id;
            Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, exchange), Is.True);
            Assert.That(FinaleQuestions.RecapAdds(state, exchange), Is.False);
            // A ballot that went against the house names somebody else.
            state.ledger.ballots[0].targetId = other.id;
            exchange.category = FinaleQuestions.Mistake;
            Assert.That(FinaleQuestions.Kicker(state, exchange), Is.EqualTo("Week 3 · you voted to evict " + other.name));
            Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, exchange), Is.False);
            Assert.That(FinaleQuestions.RecapAdds(state, exchange), Is.True);
        }

        [Test]
        public void APowerReceiptSaysWhoWentOnlyWhereItsLineDoes()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            state.ledger.power.Add(new PowerRow { week = 2, hohId = state.playerId, nominees = new List<string> { juror.id, state.contestants[5].id },
                evicteeId = state.contestants[5].id, tally = new List<int> { 3, 1 } });
            state.ledger.power.Add(new PowerRow { week = 3, hohId = state.contestants[1].id, nominees = new List<string> { state.playerId, state.contestants[4].id },
                evicteeId = state.contestants[4].id, tally = new List<int> { 1, 3 } });
            var putUp = Asked(juror, FinaleQuestions.Ownership, FinaleQuestions.PowerReceipt, "2");
            Assert.That(FinaleQuestions.ReceiptLine(state, putUp), Is.EqualTo("Week 2 · you put them on the block."));
            Assert.That(FinaleQuestions.Kicker(state, putUp), Is.EqualTo("Week 2 · you put them on the block"));
            Assert.That(FinaleQuestions.ReceiptTally(state, putUp), Is.EqualTo("(3–1)"));
            Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, putUp), Is.False);
            Assert.That(FinaleQuestions.RecapAdds(state, putUp), Is.True);
            var survived = Asked(juror, FinaleQuestions.Social, FinaleQuestions.PowerReceipt, "3");
            Assert.That(FinaleQuestions.ReceiptLine(state, survived), Is.EqualTo("Week 3 · you sat on the block, and " + state.contestants[4].name + " went."));
            Assert.That(FinaleQuestions.Kicker(state, survived), Is.EqualTo("Week 3 · you sat on the block and stayed"));
            Assert.That(FinaleQuestions.ReceiptTally(state, survived), Is.EqualTo("(3–1)"));
            Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, survived), Is.True);
            Assert.That(FinaleQuestions.RecapAdds(state, survived), Is.False);

            // The final eviction is the player's choice, not a vote: no count, and the line says they left.
            var final = EnterQuestioning(FinalThree());
            var sent = final.juryExchanges.Single();
            Assert.That(sent.receiptKind, Is.EqualTo(FinaleQuestions.PowerReceipt));
            Assert.That(FinaleQuestions.ReceiptWeek(final, sent), Is.EqualTo(4));
            Assert.That(FinaleQuestions.Kicker(final, sent), Is.EqualTo("Week 4 · you sent them to the jury"));
            Assert.That(FinaleQuestions.ReceiptTally(final, sent), Is.Null);
            Assert.That(FinaleQuestions.ReceiptSaysWhoWent(final, sent), Is.True);
            Assert.That(FinaleQuestions.RecapAdds(final, sent), Is.False);
        }

        [Test]
        public void ReceiptsWithNoVoteHaveKickersAndNoCount()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            // Both weeks had a count and somebody went, so nothing below is missing for want of a vote.
            state.ledger.power.Add(new PowerRow { week = 2, hohId = state.contestants[1].id, nominees = new List<string> { state.contestants[4].id, state.contestants[5].id },
                evicteeId = state.contestants[5].id, tally = new List<int> { 4, 1 } });
            state.ledger.power.Add(new PowerRow { week = 3, hohId = state.contestants[1].id, nominees = new List<string> { juror.id, state.contestants[4].id },
                evicteeId = state.contestants[4].id, tally = new List<int> { 1, 3 } });
            state.promises.Add(new PromiseState { id = "broken", fromId = state.playerId, toId = juror.id,
                kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 3, expiresWeek = 3 });
            state.deals.Add(new DealState { id = "safety", type = DealKind.SafetyAgreement, proposerId = state.playerId, recipientId = juror.id,
                status = DealStatus.Broken, week = 2, expiresWeek = 3 });
            state.alliances.Add(new AllianceState { id = "a1", name = "The Core", active = true, members = new List<string> { state.playerId, juror.id } });
            var promise = Asked(juror, FinaleQuestions.Accountability, FinaleQuestions.PromiseReceipt, "broken");
            var deal = Asked(juror, FinaleQuestions.Accountability, FinaleQuestions.DealReceipt, "safety");
            var allies = Asked(juror, FinaleQuestions.Personal, FinaleQuestions.AllianceReceipt, "a1");
            string pact = DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant();
            // A promise and a deal are dated by their making. They are kept or broken later, in a
            // week the record does not hold, so the week never stands on the break.
            Assert.That(FinaleQuestions.Kicker(state, promise), Is.EqualTo("Week 3 · you gave them your word, and broke it"));
            Assert.That(FinaleQuestions.Kicker(state, deal), Is.EqualTo("Week 2 · your " + pact + " with them, broken"));
            Assert.That(FinaleQuestions.ReceiptWeek(state, allies), Is.Null, "The record never wrote when the alliance began.");
            Assert.That(FinaleQuestions.Kicker(state, allies), Is.EqualTo("You were allies"));
            state.ledger.alliances.Add(new AllianceRow { id = "a1", startedWeek = 2 });
            Assert.That(FinaleQuestions.Kicker(state, allies), Is.EqualTo("Week 2 · you were allies"));
            foreach (var exchange in new[] { promise, deal, allies })
            {
                Assert.That(FinaleQuestions.ReceiptTally(state, exchange), Is.Null, exchange.receiptKind + " is not about a vote.");
                Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, exchange), Is.False, exchange.receiptKind);
                Assert.That(FinaleQuestions.RecapAdds(state, exchange), Is.False, exchange.receiptKind + ": who went the week it began is not part of it.");
            }
            state.promises[0].status = PromiseStatus.Fulfilled; state.deals[0].status = DealStatus.Fulfilled;
            Assert.That(FinaleQuestions.Kicker(state, promise), Is.EqualTo("Week 3 · you gave them your word, and kept it"));
            Assert.That(FinaleQuestions.Kicker(state, deal), Is.EqualTo("Week 2 · your " + pact + " with them, kept"));
            state.promises[0].status = PromiseStatus.Active; state.deals[0].status = DealStatus.Active;
            Assert.That(FinaleQuestions.Kicker(state, promise), Is.EqualTo("Week 3 · you gave them your word"));
            Assert.That(FinaleQuestions.Kicker(state, deal), Is.EqualTo("Week 2 · your " + pact + " with them"));

            // A comparison has no receipt, and a row the record lost has nothing to read.
            foreach (var none in new[] { Asked(juror, FinaleQuestions.Comparison, null, null), Asked(juror, FinaleQuestions.Ownership, FinaleQuestions.BallotReceipt, "7") })
            {
                Assert.That(FinaleQuestions.ReceiptWeek(state, none), Is.Null);
                Assert.That(FinaleQuestions.Kicker(state, none), Is.Null);
                Assert.That(FinaleQuestions.ReceiptTally(state, none), Is.Null);
                Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, none), Is.False);
                Assert.That(FinaleQuestions.RecapAdds(state, none), Is.False);
            }
        }

        /// <summary>
        /// A plea is a nominee asking for the player's vote. The house offers one only while the
        /// nominees campaign and clears it when campaigning closes, so its week is the vote's: the
        /// week's count stands beside it, and who went, which its line does not say, under it.
        /// </summary>
        [Test]
        public void APleaIsAboutItsWeeksVote()
        {
            var state = FinalThree();
            var juror = FirstJuror(state);
            var other = state.contestants[4];
            state.ledger.power.Add(new PowerRow { week = 3, hohId = state.contestants[1].id, nominees = new List<string> { juror.id, other.id },
                evicteeId = other.id, tally = new List<int> { 1, 3 } });
            state.ledger.replies.Add(new ReplyRow { week = 3, cardId = "plea", kind = ReplyCards.Plea, fromId = juror.id, listenerId = other.id, replyKey = "refuse", toThem = -5 });
            var plea = Asked(juror, FinaleQuestions.JuryManagement, FinaleQuestions.ReplyReceipt, "plea");
            Assert.That(FinaleQuestions.ReceiptLine(state, plea), Is.EqualTo("Week 3 · they asked you for your vote, and you said no."));
            Assert.That(FinaleQuestions.ReceiptWeek(state, plea), Is.EqualTo(3));
            Assert.That(FinaleQuestions.Kicker(state, plea), Is.EqualTo("Week 3 · you turned down their plea"));
            Assert.That(FinaleQuestions.ReceiptTally(state, plea), Is.EqualTo("(3–1)"));
            Assert.That(FinaleQuestions.ReceiptSaysWhoWent(state, plea), Is.False, "The line does not say who went,");
            Assert.That(FinaleQuestions.RecapAdds(state, plea), Is.True, "so the week's headline adds it.");
        }

        // ------------------------------------------------------------ the final case's screen (MOCKUP-PASS M15)

        /// <summary>
        /// A season with a moment of every kind a row can give: two wins and a kept promise, a week
        /// holding the house on a call in The Core, a veto used on somebody else, a whip count read
        /// right, a kept deal, and an alliance still standing.
        /// </summary>
        private static EpisodeState EveryKindOfMoment()
        {
            var state = WithMoments(FinalThree());
            string player = state.playerId;
            var c = state.contestants;
            state.ledger.power.Add(new PowerRow { week = 2, hohId = player, nominees = new List<string> { c[4].id, c[5].id },
                evicteeId = c[5].id, tally = new List<int> { 2, 1 } });
            state.ledger.power.Add(new PowerRow { week = 3, hohId = c[1].id, vetoHolderId = player, vetoUsed = true, savedId = c[4].id,
                nominees = new List<string> { c[4].id, c[3].id }, evicteeId = c[3].id, tally = new List<int> { 2, 0 } });
            state.ledger.ballots.Add(new BallotRow { week = 3, voterId = player, targetId = c[3].id, readBefore = c[3].id, correct = true });
            state.ledger.calls.Add(new BlocCallRow { week = 2, allianceId = "a1", callerId = player, targetId = c[5].id });
            state.alliances.Add(new AllianceState { id = "a1", name = "The Core", members = new List<string> { player, c[1].id, c[4].id } });
            state.ledger.alliances.Add(new AllianceRow { id = "a1", startedWeek = 1 });
            state.deals.Add(new DealState { id = "d1", type = DealKind.SafetyAgreement, proposerId = player, recipientId = c[1].id,
                status = DealStatus.Fulfilled, week = 2, expiresWeek = 3 });
            return state;
        }

        [Test]
        public void EveryMomentHasATitleAndTheFaceItIsAbout()
        {
            var state = EveryKindOfMoment();
            var c = state.contestants;
            string player = state.playerId;
            var faces = new Dictionary<string, string>
            {
                ["win:2:HoH"] = player, ["win:3:Veto"] = player, ["hoh:2"] = c[5].id, ["veto:3"] = c[4].id, ["whip:3"] = c[3].id,
                ["call:a1:2"] = c[5].id, ["promise:kept"] = c[2].id, ["deal:d1"] = c[1].id, ["alliance:a1"] = c[1].id,
                ["record:unnominated"] = player,
            };
            var moments = FinalArgument.Moments(state);
            Assert.That(moments.Select(m => m.reference), Is.EquivalentTo(faces.Keys), "A moment of every kind the fixture writes.");
            foreach (var moment in moments)
            {
                Assert.That(FinalArgument.SubjectOf(state, moment.reference), Is.EqualTo(faces[moment.reference]), moment.reference);
                string title = FinalArgument.Title(moment.reference);
                Assert.That(title, Is.Not.Null.And.Not.Empty, moment.reference);
                Assert.That(title, Is.Not.EqualTo(moment.text), "A card's title never reads as its caption.");
            }
            Assert.That(FinalArgument.Title("win:2:HoH"), Is.EqualTo("Won Head of Household"));
            Assert.That(FinalArgument.Title("win:5:FinalHoHPart2"), Is.EqualTo("Won Final HoH Part 2"));
            Assert.That(FinalArgument.Title("record:off-the-block"), Is.EqualTo("Stayed off the block"));
            Assert.That(FinalArgument.Title("nonsense"), Is.Null);
            Assert.That(FinalArgument.SubjectOf(state, "hoh:7"), Is.Null, "A week the record does not hold has no face.");
            Assert.That(FinalArgument.SubjectOf(state, "win:7:HoH"), Is.Null);
            Assert.That(FinalArgument.SubjectOf(state, "promise:nobody"), Is.Null);
            Assert.That(FinalArgument.SubjectOf(state, "nonsense"), Is.Null);
            // Only the player's own rows: a promise somebody else kept is nobody's face here.
            state.promises.Add(new PromiseState { id = "theirs", fromId = c[1].id, toId = c[4].id, kind = PromiseKind.Safety,
                status = PromiseStatus.Fulfilled, week = 2, expiresWeek = 3 });
            Assert.That(FinalArgument.SubjectOf(state, "promise:theirs"), Is.Null);
        }

        [Test]
        public void TheResumeReadsThePlayersOwnRecordAndNeverDatesABreak()
        {
            var state = EnterQuestioning(EveryKindOfMoment());
            var c = state.contestants;
            state.promises.Add(new PromiseState { id = "broken", fromId = state.playerId, toId = c[4].id, kind = PromiseKind.Safety,
                status = PromiseStatus.Broken, week = 3, expiresWeek = 3 });
            state.promises.Add(new PromiseState { id = "theirs", fromId = c[1].id, toId = state.playerId, kind = PromiseKind.Safety,
                status = PromiseStatus.Broken, week = 2, expiresWeek = 3 });
            var you = state.Find(state.playerId);
            var read = FinalCaseResume.Read(state);
            Assert.That(read.wins, Is.EqualTo(FinalistRead.Wins(state, you)));
            Assert.That(read.nominationsSurvived, Is.EqualTo(you.timesNominated));
            Assert.That(read.weeks, Is.EqualTo(state.week));
            Assert.That(read.weeksInPower, Is.EqualTo(state.ledger.power.Count(p => p.hohId == state.playerId)));

            // Seven moves on the record, three shown: holding the house twice and the call, in week order.
            Assert.That(read.majorMoves.Select(m => m.text), Is.EqualTo(new[] { "Held the house", "Called the vote", "Held the house" }));
            Assert.That(read.majorMoves.Select(m => m.week), Is.EqualTo(new[] { 2, 2, state.week }));
            Assert.That(read.alliances, Is.EqualTo(new[] { "The Core with " + FinalistRead.FirstName(c[1].name) + ", " + FinalistRead.FirstName(c[4].name)
                + " · weeks 1–" + state.week }), "Its name, first names and weeks - never why it began or ended.");
            Assert.That(read.moreAlliances, Is.Zero);

            Assert.That(read.brokenByYou, Is.EqualTo(1));
            Assert.That(read.brokenAgainstYou, Is.EqualTo(1));
            Assert.That(read.betrayals, Is.EqualTo(new[] { "You broke your word to " + c[4].name + ".", c[1].name + " broke their word to you." }),
                "The most recently made first.");
            Assert.That(read.betrayals.Any(line => line.IndexOf("week", System.StringComparison.OrdinalIgnoreCase) >= 0), Is.False, "Never a break week.");

            // The weeks the moves did not name, one a week, and last the week the player got here.
            Assert.That(read.keyWeeks.Select(k => k.week), Is.EqualTo(new[] { 1, 2, 3, state.week }));
            Assert.That(read.keyWeeks.Select(k => k.text), Is.EqualTo(new[] { "Built an alliance", "Kept a deal", "Used the veto", "Reached the Final 2" }));
            Assert.That(read.keyWeeks, Has.Count.LessThanOrEqualTo(FinalCaseResume.MostKeyWeeks));

            // At three, before the final eviction, the résumé says how far the player has come so far.
            Assert.That(FinalCaseResume.Read(EveryKindOfMoment()).keyWeeks.Last().text, Is.EqualTo("Reached the Final 3"));
            Assert.That(FinalCaseResume.Read(null).keyWeeks, Is.Empty);
        }

        [Test]
        public void TheSpeechOpensWithTheThemesOpening()
        {
            var state = EnterQuestioning(WithMoments(FinalThree()));
            var engine = new EpisodeEngine(state);
            Assert.That(Lock(engine, FinalArgument.Social, "promise:kept", "win:2:HoH", "record:unnominated").accepted, Is.True);
            foreach (var theme in FinalArgument.Themes) Assert.That(FinalArgument.Opening(theme), Is.Not.Null.And.Not.Empty, theme);
            Assert.That(FinalArgument.Speech(engine.Snapshot), Does.StartWith(FinalArgument.Opening(FinalArgument.Social)),
                "The résumé's quote slot reads back the speech's own first line.");
        }
    }
}
