using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Engine integration tests; source arithmetic/catalog parity is covered independently.</summary>
    public sealed class EpisodeSocialHistoryTests
    {
        [Test]
        public void PostEvictionInvitationSurvivesJsonReloadAndViewingConsumesNoRandomness()
        {
            var initial = WeeklyEviction(ContentCatalog.Create(301), true);
            var engine = Engine(initial);
            var reveal = Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(reveal.state.pendingDiary, Is.Not.Null);
            var saved = reveal.state;
            Assert.That(saved.pendingDiary.id, Is.EqualTo("diary-post_eviction-1"));
            Assert.That(saved.pendingDiary.evictedId, Is.EqualTo(initial.nominees[0]));
            Assert.That(saved.pendingDiary.week, Is.EqualTo(saved.week));
            Assert.That(saved.randomState, Is.EqualTo(initial.randomState), "Committed ballots and guaranteed diary invitation need no extra draws.");
            var expectedLedger = WebJurySentiment.AddJuror(initial.jurySentiment, saved.pendingDiary.evictedId,
                saved.Find(saved.pendingDiary.evictedId).name, initial.Score(initial.playerId, saved.pendingDiary.evictedId));
            Assert.That(Json(saved.jurySentiment), Is.EqualTo(Json(expectedLedger)), "Entry uses player-to-evictee score.");

            engine = Reload(saved);
            string before = Json(engine.Snapshot);
            for (int count = 0; count < 5; count++)
            {
                var display = EpisodeEngine.CurrentDiary(engine.Snapshot);
                Assert.That(display.choices.Select(choice => choice.id), Is.EqualTo(new[] { "remorseful", "ruthless", "calculated" }));
                display.narrative = "mutated UI copy";
                display.choices[0].effects.juryDelta = 999;
            }
            var detached = engine.Snapshot;
            detached.pendingDiary.evictedId = "invalid external edit";
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(EpisodeEngine.CurrentDiary(engine.Snapshot).choices[0].effects.juryDelta, Is.EqualTo(5));
        }

        [Test]
        public void ReflectAppliesPersonaAndImpressionLedgerExactlyOnceWithoutTrustOrRandomEffects()
        {
            var engine = RevealedEpisode();
            var before = engine.Snapshot;
            var diary = EpisodeEngine.CurrentDiary(before);
            var choice = diary.choices.Single(item => item.id == "remorseful");
            var expectedPersona = WebDiaryRoom.ApplyPersonaChoice(before.playerPersona, choice, before.week);
            var expectedJury = WebJurySentiment.ShiftAllJurorSentiment(before.jurySentiment, 5, "Diary Room: Remorseful", before.week);
            var command = Command(before, EpisodeCommandKind.ReflectDiary, before.pendingDiary.id, "remorseful");
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(Json(result.state.playerPersona), Is.EqualTo(Json(expectedPersona)));
            Assert.That(Json(result.state.jurySentiment), Is.EqualTo(Json(expectedJury)));
            Assert.That(Json(result.state.relationships), Is.EqualTo(Json(before.relationships)), "Impression shifts are not directed trust changes.");
            Assert.That(Json(result.state.relationshipArcs), Is.EqualTo(Json(before.relationshipArcs)));
            Assert.That(result.state.randomState, Is.EqualTo(before.randomState));
            Assert.That(result.state.phaseEventSocialBonus, Is.EqualTo(before.phaseEventSocialBonus));
            Assert.That(result.state.phaseEventCompBonus, Is.EqualTo(before.phaseEventCompBonus));
            Assert.That(result.state.lastDiaryRoomWeek, Is.EqualTo(before.week));
            Assert.That(result.state.resolvedDiaryIds, Is.EqualTo(new[] { before.pendingDiary.id }));
            Assert.That(result.state.pendingDiary, Is.Null);
            Assert.That(result.state.events.Count(item => item.kind == "diary-room"), Is.EqualTo(1));
            Assert.That(result.state.events.Last(item => item.kind == "diary-room").audienceIds, Is.EqualTo(new[] { before.playerId }));
            string committed = Json(result.state);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            engine = Reload(result.state);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            AssertRejectedUnchanged(engine, Command(engine.Snapshot, EpisodeCommandKind.ReflectDiary, before.pendingDiary.id, "ruthless"));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
        }

        [Test]
        public void SkipHasNoPersonaOrImpressionEffectAndStaleOrReplayedCommandsCannotRepeatIt()
        {
            var engine = RevealedEpisode();
            var before = engine.Snapshot;
            var stale = Command(before, EpisodeCommandKind.ReflectDiary, before.pendingDiary.id, "ruthless");
            stale.id += "-distinct-stale";
            var skip = Command(before, EpisodeCommandKind.SkipDiary, before.pendingDiary.id);
            var result = engine.Apply(skip);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(Json(result.state.playerPersona), Is.EqualTo(Json(before.playerPersona)));
            Assert.That(Json(result.state.jurySentiment), Is.EqualTo(Json(before.jurySentiment)));
            Assert.That(Json(result.state.relationships), Is.EqualTo(Json(before.relationships)));
            Assert.That(result.state.randomState, Is.EqualTo(before.randomState));
            Assert.That(result.state.resolvedDiaryIds.Count, Is.EqualTo(1));
            Assert.That(result.state.events.Count(item => item.kind == "diary-skip"), Is.EqualTo(1));
            AssertRejectedUnchanged(engine, stale);
            var restored = Reload(result.state);
            Assert.That(restored.Apply(skip).duplicate, Is.True);
            AssertRejectedUnchanged(restored, Command(restored.Snapshot, EpisodeCommandKind.SkipDiary, before.pendingDiary.id));
        }

        [Test]
        public void AdvancingToSocialPreservesPendingPromptAndCannotAdvancePastIt()
        {
            var engine = RevealedEpisode();
            string pendingId = engine.Snapshot.pendingDiary.id;
            Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(engine.Snapshot.pendingDiary.id, Is.EqualTo(pendingId));
            AssertRejectedUnchanged(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
            var forgedActor = Command(engine.Snapshot, EpisodeCommandKind.ReflectDiary, pendingId, "remorseful");
            forgedActor.actorId = engine.Snapshot.Active.First(item => !item.isPlayer).id;
            AssertRejectedUnchanged(engine, forgedActor);
            AssertRejectedUnchanged(engine, Command(engine.Snapshot, EpisodeCommandKind.ReflectDiary, pendingId, "forged-choice"));
            Apply(engine, EpisodeCommandKind.SkipDiary, pendingId);
            Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.HoH));
            Assert.That(engine.Snapshot.week, Is.EqualTo(2));
            Assert.That(engine.Snapshot.pendingDiary, Is.Null);
            Assert.That(engine.Snapshot.phaseEventSocialBonus, Is.EqualTo(7), "Source counters do not reset at a phase/week boundary.");
            Assert.That(engine.Snapshot.phaseEventCompBonus, Is.EqualTo(11));
        }

        [Test]
        public void ValidSameWeekEvictionCannotOfferAnAlreadyResolvedWeeklyReflection()
        {
            var engine = RevealedEpisode();
            Apply(engine, EpisodeCommandKind.SkipDiary, engine.Snapshot.pendingDiary.id);
            // Controlled valid five-active snapshot models another resolved eviction in the same
            // week without manufacturing a second pending event or changing the completion receipt.
            var sameWeek = WeeklyEviction(engine.Snapshot, true);
            engine = Engine(sameWeek);
            Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(engine.Snapshot.week, Is.EqualTo(1));
            Assert.That(engine.Snapshot.pendingDiary, Is.Null);
            Assert.That(engine.Snapshot.resolvedDiaryIds, Is.EqualTo(new[] { "diary-post_eviction-1" }));
            Assert.That(engine.Snapshot.events.Count(item => item.kind == "diary-invitation"), Is.EqualTo(1));
        }

        [TestCase(70, false)]
        [TestCase(71, true)]
        [TestCase(75, false)]
        public void OnlyAnUpwardCrossingOfSeventyFiveCreatesAnOathOpportunity(double initialScore, bool expected)
        {
            var state = ContentCatalog.Create(501);
            state.Find(state.playerId).stats.social = 5;
            string target = state.Active.First(item => !item.isPlayer).id;
            Edge(state, state.playerId, target).score = initialScore;
            Edge(state, target, state.playerId).score = -50;
            var engine = Engine(state);
            Apply(engine, EpisodeCommandKind.Talk, target);
            Assert.That(engine.Snapshot.oathOpportunities.Contains(target), Is.EqualTo(expected));
            Assert.That(engine.Snapshot.shownOathMilestones.Contains(target), Is.EqualTo(expected));
            Assert.That(engine.Snapshot.events.Count(item => item.kind == "relationship-milestone"), Is.EqualTo(expected ? 1 : 0));
        }

        [Test]
        public void LoyaltyDeclarationIsOneWayFivePointsWithoutNpcConsentRandomnessAndKeepsLastTwentyNotes()
        {
            var state = ContentCatalog.Create(502);
            string target = state.Active.First(item => !item.isPlayer).id;
            state.Find(state.playerId).stats.social = 5;
            Edge(state, state.playerId, target).score = 71;
            Edge(state, target, state.playerId).score = -50;
            Edge(state, state.playerId, target).notes = Enumerable.Range(0, 25).Select(index => "old-note-" + index).ToList();
            var engine = Engine(state);
            Apply(engine, EpisodeCommandKind.Talk, target);
            var before = engine.Snapshot;
            var declaration = Command(before, EpisodeCommandKind.SwearLoyalty, target);
            var result = engine.Apply(declaration);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.Score(state.playerId, target), Is.EqualTo(80));
            Assert.That(result.state.Score(target, state.playerId), Is.EqualTo(before.Score(target, state.playerId)));
            Assert.That(result.state.randomState, Is.EqualTo(before.randomState));
            Assert.That(Json(result.state.relationshipArcs), Is.EqualTo(Json(before.relationshipArcs)));
            Assert.That(result.state.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(result.state.loyaltyOaths.Count, Is.EqualTo(1));
            Assert.That(result.state.loyaltyOaths[0].playerId, Is.EqualTo(state.playerId));
            Assert.That(result.state.loyaltyOaths[0].targetId, Is.EqualTo(target));
            Assert.That(result.state.oathOpportunities, Is.Empty);
            Assert.That(result.state.alliances, Is.Empty, "A declaration is not a mutual alliance.");
            Assert.That(result.state.promises, Is.Empty, "It does not fabricate a reciprocal NPC promise.");
            Assert.That(Edge(result.state, state.playerId, target).notes,
                Is.EqualTo(Enumerable.Range(6, 19).Select(index => "old-note-" + index).Concat(new[] { "loyalty-oath" })));
            Assert.That(Edge(result.state, state.playerId, target).events, Is.Empty);
            var restored = Reload(result.state);
            Assert.That(restored.Apply(declaration).duplicate, Is.True);
            AssertRejectedUnchanged(restored, Command(restored.Snapshot, EpisodeCommandKind.SwearLoyalty, target));
        }

        [Test]
        public void DeclinedOathIsNotReofferedAfterReloadAndAnotherCrossing()
        {
            var engine = CrossingEpisode(out var target);
            var before = engine.Snapshot;
            var decline = Command(before, EpisodeCommandKind.DeclineLoyalty, target);
            var result = engine.Apply(decline);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.loyaltyOaths, Is.Empty);
            Assert.That(result.state.oathOpportunities, Is.Empty);
            Assert.That(Json(result.state.relationships), Is.EqualTo(Json(before.relationships)));
            Assert.That(result.state.randomState, Is.EqualTo(before.randomState));
            Assert.That(result.state.alliances, Is.Empty);
            Assert.That(result.state.shownOathMilestones, Does.Contain(target));
            engine = Reload(result.state);
            Assert.That(engine.Apply(decline).duplicate, Is.True);
            var recross = engine.Snapshot;
            Edge(recross, recross.playerId, target).score = 74;
            engine = Engine(recross);
            Apply(engine, EpisodeCommandKind.Talk, target);
            Assert.That(engine.Snapshot.Score(recross.playerId, target), Is.GreaterThanOrEqualTo(75));
            Assert.That(engine.Snapshot.oathOpportunities, Is.Empty);
            Assert.That(engine.Snapshot.events.Count(item => item.kind == "relationship-milestone"), Is.EqualTo(1));
        }

        [Test]
        public void OathNominationOrdersMinusTwentyFiveThenMinusEightArcsAndAppliesWitnessAndMentalConsequences()
        {
            var engine = CrossingEpisode(out var victim);
            Apply(engine, EpisodeCommandKind.SwearLoyalty, victim);
            var state = engine.Snapshot;
            state.phase = EpisodePhase.Nomination; state.hohId = state.playerId;
            var witnesses = state.Active.Where(item => item.id != state.playerId && item.id != victim).ToArray();
            var feelings = new[] { 20d, -20d, 0d, 19d };
            for (int index = 0; index < witnesses.Length; index++)
            {
                Edge(state, witnesses[index].id, victim).score = feelings[index];
                Edge(state, witnesses[index].id, state.playerId).score = 10;
            }
            string otherNominee = witnesses[0].id;
            var random = new SeededRandom(state.randomState);
            var deltas = new[] { -15d, 3d, -(Math.Floor(random.NextDouble() * 6) + 5), -(Math.Floor(random.NextDouble() * 6) + 5) };
            engine = Engine(state);
            var nominate = Command(state, EpisodeCommandKind.Nominate, victim, otherNominee);
            var result = engine.Apply(nominate);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.loyaltyOaths, Is.Empty);
            Assert.That(result.state.relationshipArcs.Single(arc => arc.npcId == victim).weeklyHistory.TakeLast(2).Select(item => item.delta),
                Is.EqualTo(new[] { -25d, -8d }));
            Assert.That(result.state.randomState, Is.EqualTo(random.State), "Only neutral nomination witnesses consume the two source samples.");
            Assert.That(result.state.Score(state.playerId, victim), Is.EqualTo(state.Score(state.playerId, victim)));
            Assert.That(result.state.Score(victim, state.playerId), Is.EqualTo(state.Score(victim, state.playerId)), "Normal -8 is an arc change, not a trust penalty.");
            for (int index = 0; index < witnesses.Length; index++)
                Assert.That(result.state.Score(witnesses[index].id, state.playerId), Is.EqualTo(10 + deltas[index]), witnesses[index].name);
            foreach (string nominee in new[] { victim, otherNominee })
            {
                Assert.That(result.state.Find(nominee).mood, Is.EqualTo("Angry"));
                Assert.That(result.state.Find(nominee).stressLevel, Is.EqualTo("Stressed"));
                Assert.That(result.state.Find(nominee).timesNominated, Is.EqualTo(1));
                Assert.That(result.state.Find(nominee).nominationWeeks, Is.EqualTo(new[] { state.week }));
            }
            Assert.That(result.state.events.Count(item => item.kind == "loyalty_oath_broken"), Is.EqualTo(1));
            var restored = Reload(result.state);
            Assert.That(restored.Apply(nominate).duplicate, Is.True);
            AssertRejectedUnchanged(restored, Command(restored.Snapshot, EpisodeCommandKind.Nominate, victim, otherNominee));
        }

        [Test]
        public void OathVoteBreachIsDeferredUntilRevealWithoutEarlyPublicBallotOrTrustDisclosure()
        {
            var engine = CrossingEpisode(out var victim);
            Apply(engine, EpisodeCommandKind.SwearLoyalty, victim);
            var state = WeeklyEviction(engine.Snapshot, false, victim);
            state.votes.Clear();
            engine = Engine(state);
            var ballot = Command(state, EpisodeCommandKind.CastVote, victim);
            var cast = engine.Apply(ballot);
            Assert.That(cast.accepted, Is.True, cast.reason);
            Assert.That(cast.state.votes.Count, Is.EqualTo(1));
            Assert.That(Json(cast.state.loyaltyOaths), Is.EqualTo(Json(state.loyaltyOaths)));
            Assert.That(Json(cast.state.relationships), Is.EqualTo(Json(state.relationships)));
            Assert.That(Json(cast.state.relationshipArcs), Is.EqualTo(Json(state.relationshipArcs)));
            Assert.That(cast.state.randomState, Is.EqualTo(state.randomState));
            var newEvents = cast.state.events.Where(item => item.sequence >= state.nextSequence).ToArray();
            Assert.That(newEvents.Length, Is.EqualTo(1));
            Assert.That(newEvents[0].kind, Is.EqualTo("private-vote"));
            Assert.That(newEvents[0].audienceIds, Is.EqualTo(new[] { state.playerId }));
            Assert.That(newEvents[0].text, Does.Not.Contain(state.Find(victim).name));
            Assert.That(cast.state.events.Any(item => item.kind == "loyalty_oath_broken" || item.kind == "vote-reveal"), Is.False);

            engine = Reload(cast.state);
            Assert.That(engine.Apply(ballot).duplicate, Is.True);
            var reveal = Command(engine.Snapshot, EpisodeCommandKind.Advance);
            var result = engine.Apply(reveal);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.evictionResolved, Is.True);
            Assert.That(result.state.loyaltyOaths, Is.Empty);
            Assert.That(result.state.relationshipArcs.Single(arc => arc.npcId == victim).weeklyHistory.Last().delta, Is.EqualTo(-20));
            Assert.That(result.state.events.Count(item => item.kind == "loyalty_oath_broken"), Is.EqualTo(1));
            Assert.That(result.state.events.Count(item => item.kind == "vote-reveal"), Is.EqualTo(result.state.votes.Count));
            Assert.That(result.state.randomState, Is.EqualTo(state.randomState), "Vote oath witnesses use independent source-hashed samples, not season RNG.");
            engine = Reload(result.state);
            string committed = Json(engine.Snapshot);
            Assert.That(engine.Apply(reveal).duplicate, Is.True);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
        }

        [Test]
        public void NewSocialHistoryCollectionsAreDetachedInConstructorSnapshotsAndCommandResults()
        {
            var engine = RevealedEpisode();
            Apply(engine, EpisodeCommandKind.ReflectDiary, engine.Snapshot.pendingDiary.id, "remorseful");
            Apply(engine, EpisodeCommandKind.Advance);
            var controlled = engine.Snapshot;
            string target = controlled.Active.First(item => !item.isPlayer).id;
            controlled.Find(controlled.playerId).stats.social = 5;
            Edge(controlled, controlled.playerId, target).score = 71;
            engine = Engine(controlled);
            controlled.playerPersona.history[0].persona = "external mutation";
            Apply(engine, EpisodeCommandKind.Talk, target);
            var result = Apply(engine, EpisodeCommandKind.SwearLoyalty, target);
            string committed = Json(engine.Snapshot);
            MutateSocialHistory(result.state);
            MutateSocialHistory(engine.Snapshot);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
        }

        private static void MutateSocialHistory(EpisodeState state)
        {
            state.playerPersona.current = "external"; state.playerPersona.scores[0].score = 99; state.playerPersona.history[0].week = 99;
            state.jurySentiment.overallSentiment = 99; state.jurySentiment.jurors[0].sentiment = 99;
            state.jurySentiment.jurors[0].events[0].reason = "external";
            state.loyaltyOaths[0].timestamp = 999999; state.oathOpportunities.Add("external"); state.shownOathMilestones.Clear();
            state.resolvedDiaryIds.Clear(); state.phaseEventSocialBonus = 999; state.phaseEventCompBonus = 999;
            state.relationshipArcs[0].weeklyHistory[0].reason = "external";
        }

        private static EpisodeEngine RevealedEpisode()
        {
            var state = ContentCatalog.Create(401);
            state.phaseEventSocialBonus = 7; state.phaseEventCompBonus = 11;
            var engine = Engine(WeeklyEviction(state, true));
            Apply(engine, EpisodeCommandKind.Advance);
            return engine;
        }

        private static EpisodeEngine CrossingEpisode(out string target)
        {
            var state = ContentCatalog.Create(503);
            target = state.Active.First(item => !item.isPlayer).id;
            state.Find(state.playerId).stats.social = 5;
            Edge(state, state.playerId, target).score = 71;
            var engine = Engine(state);
            Apply(engine, EpisodeCommandKind.Talk, target);
            Assert.That(engine.Snapshot.oathOpportunities, Does.Contain(target));
            return engine;
        }

        private static EpisodeState WeeklyEviction(EpisodeState source, bool includePlayerVote, string preferredVictim = null)
        {
            var state = source.Clone();
            var npcs = state.Active.Where(item => !item.isPlayer).ToArray();
            string victim = preferredVictim ?? npcs[1].id;
            state.hohId = npcs.First(item => item.id != victim).id;
            string other = npcs.First(item => item.id != victim && item.id != state.hohId).id;
            state.phase = EpisodePhase.Eviction; state.vetoHolderId = state.hohId;
            state.nominees = new List<string> { victim, other };
            state.vetoPlayers = state.Active.Select(item => item.id).ToList();
            state.vetoResolved = true; state.evictionResolved = false; state.competitionResolved = false;
            state.competitionScores.Clear(); state.votes.Clear(); state.pendingDiary = null;
            foreach (var voter in EpisodeEngine.Voters(state).Where(item => includePlayerVote || !item.isPlayer))
                state.votes.Add(new VoteState { voterId = voter.id, targetId = victim, reason = "Controlled committed ballot" });
            return state;
        }

        private static RelationshipState Edge(EpisodeState state, string from, string to) => state.relationships.Single(item => item.fromId == from && item.toId == to);
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static EpisodeEngine Reload(EpisodeState state) => Engine(JsonConvert.DeserializeObject<EpisodeState>(Json(state),
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }));
        private static EpisodeEngine Engine(EpisodeState state)
        {
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, "Fixture must be valid: " + error);
            return new EpisodeEngine(state);
        }
        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind, string target = null, string second = null)
            => new EpisodeCommand { id = "social-history-" + state.revision, actorId = state.playerId, expectedRevision = state.revision,
                expectedPhase = state.phase, kind = kind, targetId = target, secondTargetId = second };
        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null)
        {
            var result = engine.Apply(Command(engine.Snapshot, kind, target, second));
            Assert.That(result.accepted, Is.True, result.reason); return result;
        }
        private static void AssertRejectedUnchanged(EpisodeEngine engine, EpisodeCommand command)
        {
            string before = Json(engine.Snapshot);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False, "Expected rejected command: " + command.kind);
            Assert.That(result.duplicate, Is.False, "This assertion is for a fresh rejected command, not a receipt replay.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), result.reason);
        }
    }
}
