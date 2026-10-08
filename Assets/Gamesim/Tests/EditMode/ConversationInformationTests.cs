using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>E2's information payoffs, separate from the existing source warmth and random draws.</summary>
    public sealed class ConversationInformationTests
    {
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static IEnumerable<TestCaseData> CastOutcomes
        {
            get { foreach (int size in Enumerable.Range(3, 14)) foreach (bool lands in new[] { false, true }) yield return new TestCaseData(size, lands); }
        }
        private static uint Seed(bool lands, double floor)
        {
            for (uint seed = 1; seed < 1000; seed++)
                if ((new SeededRandom(seed).NextDouble() > floor) == lands) return seed;
            throw new InvalidOperationException("No branch seed found.");
        }
        private static EpisodeState House(int size, bool lands, bool airing = false)
        {
            var s = EconomyRulesTests.Fresh(size);
            s.randomState = Seed(lands, airing ? WebSocialVocabulary.MeetingFloor : WebSocialVocabulary.DiscussGameFloor);
            return s;
        }
        private static EpisodeCommand Command(EpisodeState s, bool airing = false) => new EpisodeCommand
        {
            id = "information-" + s.revision, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
            kind = airing ? EpisodeCommandKind.HouseMeeting : EpisodeCommandKind.DiscussGame,
            targetId = airing ? null : ContentCatalog.MayaId, text = airing ? EpisodeEngine.AirDirtyLaundry : null,
        };
        private static EpisodeState Apply(EpisodeState s, EpisodeCommand command)
        {
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            return result.state;
        }
        private static bool IsReaction(EpisodeEvent e) => e.kind == ConversationIntentRules.AiringBacked || e.kind == ConversationIntentRules.AiringOpposed;
        private static void Score(EpisodeState s, string from, string to, double score) =>
            RelationshipLedger.Move(s, from, to, score - s.Score(from, to));

        private static void SameSourceConsequences(EpisodeState before, EpisodeState after, EpisodeCommand command)
        {
            var old = before.Clone(); old.economyRulesVersion = 0;
            var historical = Apply(old, command);
            Assert.That(after.randomState, Is.EqualTo(historical.randomState), "No new season RNG draws.");
            foreach (var a in before.contestants) foreach (var b in before.contestants)
                Assert.That(after.Score(a.id, b.id), Is.EqualTo(historical.Score(a.id, b.id)), a.id + " -> " + b.id);
            Assert.That(after.socialActions, Is.EqualTo(historical.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(historical.outOfPhaseSocialActions));
            Assert.That(Json(after.windowActions), Is.EqualTo(Json(historical.windowActions)));
            Assert.That(Json(after.votes), Is.EqualTo(Json(historical.votes)));
            Assert.That(Json(after.ledger.claims), Is.EqualTo(Json(historical.ledger.claims)));
            Assert.That(Json(after.ledger.ballots), Is.EqualTo(Json(historical.ledger.ballots)));
            Assert.That(Json(after.memories.Where(m => m.ownerId != before.playerId)),
                Is.EqualTo(Json(historical.memories.Where(m => m.ownerId != before.playerId))));
        }

        [TestCaseSource(nameof(CastOutcomes))]
        public void OpenGameEarnsOnlyTheSpeakersDistinctOpinionsWhenItsExistingRollLands(int size, bool lands)
        {
            var s = House(size, lands); string speaker = ContentCatalog.MayaId;
            var others = s.Active.Where(c => !c.isPlayer && c.id != speaker).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < others.Length; i++) Score(s, speaker, others[i].id, i % 2 == 0 ? 50 : -50);
            var close = others.OrderByDescending(c => s.Score(speaker, c.id)).ThenBy(c => c.id, StringComparer.Ordinal).First();
            var distant = others.Where(c => c.id != close.id).OrderBy(c => s.Score(speaker, c.id)).ThenBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
            string before = Json(s); var command = Command(s); var after = Apply(s, command);
            var rows = after.ledger.standings.Skip(s.ledger.standings.Count).ToArray();
            var expected = lands ? new[] { close, distant }.Where(c => c != null).Select(c => c.id).ToArray() : Array.Empty<string>();
            Assert.That(rows.Select(r => r.toId), Is.EqualTo(expected));
            Assert.That(rows.Select(r => r.toId).Distinct().Count(), Is.EqualTo(rows.Length));
            foreach (var row in rows)
            {
                Assert.That(row.fromId, Is.EqualTo(speaker)); Assert.That(row.source, Is.EqualTo(ClaimSource.Told));
                Assert.That(row.score, Is.EqualTo(s.Score(speaker, row.toId)));
                Assert.That(VoteRead.StandingKnown(after, speaker, row.toId), Is.True);
                Assert.That(KnownOdds.Band(after, speaker, row.toId), Is.EqualTo(row.score >= 25 ? KnownOdds.Warm : KnownOdds.Cold));
            }
            var events = after.events.Skip(s.events.Count).Where(e => e.kind == "information").ToArray();
            Assert.That(events.Length, Is.EqualTo(expected.Length));
            Assert.That(events.All(e => e.audienceIds.SequenceEqual(new[] { s.playerId, speaker })), Is.True);
            var memories = after.memories.Where(m => m.ownerId == s.playerId).Skip(s.memories.Count(m => m.ownerId == s.playerId)).ToArray();
            Assert.That(memories.Length, Is.EqualTo(expected.Length));
            Assert.That(memories.All(m => m.subjectId == speaker && m.isPrivate), Is.True);
            Assert.That(Json(s), Is.EqualTo(before), "The input snapshot stays detached.");
            Assert.That(EpisodeEngine.ReadThisWeek(after, speaker), Is.False);
            Assert.That(EpisodeEngine.AskedThisWeek(after, speaker), Is.False);
            SameSourceConsequences(s, after, command);
        }

        [TestCaseSource(nameof(CastOutcomes))]
        public void AiringRecordsEachObservedSideAndActualResultingStandingWithoutInventingAVote(int size, bool lands)
        {
            var s = House(size, lands, true); var command = Command(s, true);
            var house = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
            foreach (var npc in house) Score(s, npc.id, s.playerId, 43);
            var after = Apply(s, command); var rng = new SeededRandom(s.randomState); rng.NextDouble();
            var reactions = after.events.Where(IsReaction).ToArray();
            Assert.That(reactions.Length, Is.EqualTo(lands ? house.Length : 0));
            Assert.That(after.ledger.standings.Count, Is.EqualTo(s.ledger.standings.Count + reactions.Length));
            foreach (var npc in house)
            {
                double impact = lands ? WebSocialVocabulary.MeetingAiring(rng.NextDouble()) : WebSocialVocabulary.MeetingFailure(rng.NextDouble());
                rng.NextDouble(); // Source reciprocal-score draw, separate from the reaction draw.
                Assert.That(EpisodeEngine.ReadThisWeek(after, npc.id), Is.False);
                Assert.That(EpisodeEngine.AskedThisWeek(after, npc.id), Is.False);
                if (!lands) { Assert.That(HouseguestNotes.For(after, npc.id).Where(n => n.kind == HouseguestNotes.Kinds.Read), Is.Empty); continue; }
                var reaction = reactions.Single(e => e.audienceIds.Contains(npc.id));
                Assert.That(reaction.kind, Is.EqualTo(impact > 0 ? ConversationIntentRules.AiringBacked : ConversationIntentRules.AiringOpposed));
                Assert.That(reaction.audienceIds, Is.EqualTo(new[] { s.playerId, npc.id }));
                var row = after.ledger.standings.Last(r => r.fromId == npc.id && r.toId == s.playerId);
                Assert.That(row.source, Is.EqualTo(ClaimSource.Overheard));
                Assert.That(row.score, Is.EqualTo(after.Score(npc.id, s.playerId)).And.Not.EqualTo(impact));
                Assert.That(VoteRead.StandingKnown(after, npc.id, s.playerId), Is.True);
                var note = HouseguestNotes.For(after, npc.id).Single(n => n.kind == HouseguestNotes.Kinds.Read);
                Assert.That(note.text, Is.EqualTo(reaction.text));
                Assert.That(note.text, Does.Contain("not a promise about the vote"));
            }
            Assert.That(after.randomState, Is.EqualTo(rng.State));
            SameSourceConsequences(s, after, command);
        }

        [Test]
        public void TiedOpinionsChooseTwoDifferentSubjectsInOrdinalOrder()
        {
            var s = House(8, true); string speaker = ContentCatalog.MayaId;
            var others = s.Active.Where(c => !c.isPlayer && c.id != speaker).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
            foreach (var other in others) Score(s, speaker, other.id, 0);
            var after = Apply(s, Command(s));
            Assert.That(after.ledger.standings.Select(r => r.toId), Is.EqualTo(others.Take(2).Select(c => c.id)));
            Assert.That(after.events.Where(e => e.kind == "information").All(e => e.text.Contains("still weighing up")), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void LegacyAndDelayedEconomyNeverEarnTheNewInformation(bool airing)
        {
            foreach (bool delayed in new[] { false, true })
            {
                var s = House(8, true, airing);
                if (delayed) s.weekRulesStartWeek = 2; else s.economyRulesVersion = 0;
                var after = Apply(s, Command(s, airing));
                Assert.That(Json(after.ledger.standings), Is.EqualTo(Json(s.ledger.standings)));
                Assert.That(after.events.Where(IsReaction), Is.Empty);
                Assert.That(after.events.Skip(s.events.Count).Where(e => e.kind == "information"), Is.Empty);
            }
        }

        [Test]
        public void TacticsAndRallyKeepTheirWarmthRoleWithoutTheseInformationRewards()
        {
            var s = House(8, true);
            var tactics = Command(s); tactics.kind = EpisodeCommandKind.StrategicDiscussion;
            var rally = Command(s, true); rally.text = null;
            foreach (var command in new[] { tactics, rally })
            {
                var after = Apply(s, command);
                Assert.That(after.ledger.standings, Is.Empty);
                Assert.That(after.events.Where(IsReaction), Is.Empty);
                SameSourceConsequences(s, after, command);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void SaveShapedReplayIsExactAndDuplicateOrStaleCommandsCannotEarnTwice(bool airing)
        {
            var s = House(8, true, airing); var command = Command(s, airing); var engine = new EpisodeEngine(s);
            var result = engine.Apply(command); Assert.That(result.accepted, Is.True, result.reason);
            var restored = JsonConvert.DeserializeObject<EpisodeState>(Json(s), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Assert.That(Json(Apply(restored, command)), Is.EqualTo(Json(result.state)));
            string committed = Json(engine.Snapshot);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            command.id += "-stale"; Assert.That(engine.Apply(command).accepted, Is.False);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
        }

        [TestCase(false)] [TestCase(true)]
        public void ExhaustionAndWrongActorOrPhaseRejectBeforeInformationOrRandomness(bool airing)
        {
            foreach (string refusal in new[] { "spent", "actor", "phase", "target" })
            {
                if (airing && refusal == "target") continue;
                var s = House(8, true, airing);
                if (refusal == "spent") { s.socialActions = 2; s.windowActions[Windows.AfterEviction] = 2; }
                var command = Command(s, airing);
                if (refusal == "actor") command.actorId = ContentCatalog.MayaId;
                if (refusal == "phase") command.expectedPhase = EpisodePhase.HoH;
                if (refusal == "target") command.targetId = s.playerId;
                var engine = new EpisodeEngine(s); string before = Json(engine.Snapshot);
                Assert.That(engine.Apply(command).accepted, Is.False, refusal);
                Assert.That(Json(engine.Snapshot), Is.EqualTo(before), refusal);
            }
        }

        [Test]
        public void ObservedSideSurvivesScoreClampingAndDoesNotBecomeGeneralLoyalty()
        {
            var s = House(8, true, true); var rng = new SeededRandom(s.randomState); rng.NextDouble();
            foreach (var npc in s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal))
            {
                bool backs = WebSocialVocabulary.MeetingAiring(rng.NextDouble()) > 0; rng.NextDouble();
                Score(s, s.playerId, npc.id, backs ? 100 : -100); Score(s, npc.id, s.playerId, backs ? 100 : -100);
            }
            var after = Apply(s, Command(s, true));
            foreach (var npc in s.Active.Where(c => !c.isPlayer))
            {
                Assert.That(after.Score(npc.id, s.playerId), Is.EqualTo(s.Score(npc.id, s.playerId)));
                Assert.That(after.events.Single(e => IsReaction(e) && e.audienceIds.Contains(npc.id)).kind,
                    Is.EqualTo(s.Score(npc.id, s.playerId) > 0 ? ConversationIntentRules.AiringBacked : ConversationIntentRules.AiringOpposed));
            }
        }

        [Test]
        public void ReadNotesAreCapturedReactionsNotLiveHiddenAgendaQueries()
        {
            var s = House(8, true, true); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableCommitments(s);
            var after = Apply(s, Command(s, true)); string npc = ContentCatalog.MayaId;
            var notes = HouseguestNotes.For(after, npc).Where(n => n.kind == HouseguestNotes.Kinds.Read).ToArray();
            Assert.That(notes.Length, Is.EqualTo(1));
            Assert.That(notes[0].text, Does.Not.Contain(NpcAgendas.Describe(after, npc, NpcAgendas.Of(after, npc))));
            foreach (var edge in after.relationships) { edge.score = -edge.score; edge.lastInteractionWeek = 0; }
            Assert.That(Json(HouseguestNotes.For(after, npc).Where(n => n.kind == HouseguestNotes.Kinds.Read)), Is.EqualTo(Json(notes)));
            Assert.That(Allegiance.CommitmentKnown(after, npc), Is.False, "An Overheard row alone grants no deliberate-read/contact privilege.");
            var reaction = after.events.Single(e => IsReaction(e) && e.audienceIds.Contains(npc));
            reaction.audienceIds.Remove(after.playerId);
            Assert.That(HouseguestNotes.For(after, npc).Where(n => n.kind == HouseguestNotes.Kinds.Read), Is.Empty);
            reaction.audienceIds.Add(after.playerId); reaction.audienceIds.Add(after.Active.Last(c => c.id != npc && !c.isPlayer).id);
            Assert.That(HouseguestNotes.For(after, npc).Where(n => n.kind == HouseguestNotes.Kinds.Read), Is.Empty, "A malformed broad audience cannot become a personal read.");
        }

        [TestCase(false)] [TestCase(true)]
        public void ExistingLedgerAndMemoryCapsStillApplyToTheAdditionalInformation(bool airing)
        {
            var s = House(16, true, airing);
            for (int i = 0; i < SeasonLedger.MostRows; i++) s.ledger.standings.Add(new StandingRow
                { week = s.week, fromId = ContentCatalog.MayaId, toId = s.playerId, source = ClaimSource.Overheard, score = 0 });
            s.memories.RemoveAll(m => m.ownerId == s.playerId);
            for (int i = 0; i < 30; i++) s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = ContentCatalog.MayaId, week = s.week, text = "old " + i, isPrivate = true });
            var after = Apply(s, Command(s, airing));
            Assert.That(after.ledger.standings.Count, Is.EqualTo(SeasonLedger.MostRows));
            Assert.That(after.ledger.dropped, Is.EqualTo(s.ledger.dropped + (airing ? 15 : 2)));
            Assert.That(after.memories.Count(m => m.ownerId == s.playerId), Is.EqualTo(30));
            if (!airing) Assert.That(after.memories.Any(m => m.ownerId == s.playerId && m.text == "old 0"), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void EvictedHouseguestsDoNotAttendOrBecomeNewOpinionSubjects(bool airing)
        {
            var s = House(8, true, airing); var gone = s.contestants.Last(); gone.status = ContestantStatus.Evicted;
            Score(s, ContentCatalog.MayaId, gone.id, 100);
            var after = Apply(s, Command(s, airing));
            Assert.That(after.ledger.standings.Any(r => r.fromId == gone.id || r.toId == gone.id), Is.False);
            Assert.That(after.events.Where(IsReaction).Any(e => e.audienceIds.Contains(gone.id)), Is.False);
            Assert.That(after.ledger.standings.Count, Is.EqualTo(airing ? 6 : 2));
        }

        [Test]
        public void EarnedBandsExpireAndNeverFollowLaterHiddenRelationshipChanges()
        {
            var s = House(8, true); var after = Apply(s, Command(s));
            var row = after.ledger.standings.First();
            string band = KnownOdds.Band(after, row.fromId, row.toId);
            Score(after, row.fromId, row.toId, row.score > 0 ? -100 : 100);
            Assert.That(KnownOdds.Band(after, row.fromId, row.toId), Is.EqualTo(band));
            after.week += VoteRead.StandingShelfLife + 1;
            Assert.That(KnownOdds.Band(after, row.fromId, row.toId), Is.Null);
            Assert.That(VoteRead.StandingKnown(after, row.fromId, row.toId), Is.False);
        }

        [Test]
        public void NarrowExpectedValueHarnessSeparatesInformationFromWarmthAndRisk()
        {
            // Exact equal-width midpoint integration over the source buckets, not a win-rate claim.
            double open = 0, tactics = 0, airing = 0, rally = 0, opinions = 0, reactions = 0;
            const int gates = 100, amounts = 280, others = 7;
            for (int g = 0; g < gates; g++) for (int a = 0; a < amounts; a++)
            {
                double gate = (g + .5) / gates, amount = (a + .5) / amounts;
                open += WebSocialVocabulary.DiscussGame(gate, amount);
                tactics += WebSocialVocabulary.StrategicDiscussion(amount);
                bool worked = gate > WebSocialVocabulary.MeetingFloor;
                airing += worked ? WebSocialVocabulary.MeetingAiring(amount) : WebSocialVocabulary.MeetingFailure(amount);
                rally += worked ? ((others - 1) * WebSocialVocabulary.MeetingRally(amount) + WebSocialVocabulary.MeetingSceptic) / others
                    : WebSocialVocabulary.MeetingFailure(amount);
                if (gate > WebSocialVocabulary.DiscussGameFloor) opinions += 2;
                if (worked) reactions += others;
            }
            double count = gates * amounts;
            Assert.That(open / count, Is.EqualTo(3.4).Within(.000001));
            Assert.That(tactics / count, Is.EqualTo(3.5).Within(.000001));
            Assert.That(airing / count, Is.EqualTo(-1.875).Within(.000001));
            Assert.That(rally / count, Is.EqualTo(1.560714285714).Within(.000001));
            Assert.That(opinions / count, Is.EqualTo(1.4).Within(.000001));
            Assert.That(reactions / count, Is.EqualTo(4.55).Within(.000001));
            TestContext.WriteLine("Base expected warmth: open game3.4 vs tactics3.5; airing-1.875 vs rally1.560714 per NPC in an eight-person house. New information: open game1.4 opinions, airing4.55 reactions; tactics/rally0. Source gate failures30%/35%. Excludes social-stat scaling, stories, repeated already-known facts and win rates; reply-card/all-verb balance remains open.");
        }

        [TestCase(3)] [TestCase(6)] [TestCase(8)] [TestCase(12)] [TestCase(16)]
        public void ScriptedSeasonsActuallyUseBothInformationActionsAndFinishAcrossSaveShapedCheckpoints(int size)
        {
            var s = House(size, true); EpisodeEngine.EnableStory(s); EpisodeEngine.EnableAgency(s);
            EpisodeEngine.EnableRead(s); EpisodeEngine.EnableCommitments(s);
            var engine = new EpisodeEngine(s); int open = 0, air = 0, reloads = 0;
            for (int step = 0; step < 650 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var before = engine.Snapshot; var command = EpisodeEngineTests.NextCommand(before);
                bool hasTime = before.Find(before.playerId).status == ContestantStatus.Active && before.Active.Count() > 2
                    && (before.phase == EpisodePhase.Social || before.phase == EpisodePhase.Campaign)
                    && EpisodeEngine.SocialActionsSpent(before) < EpisodeEngine.SocialActionBudget(before);
                if (hasTime && before.pendingDiary == null)
                {
                    bool airing = EpisodeEngine.SocialActionsSpent(before) % 2 != 0;
                    command = Command(before, airing);
                    if (!airing) command.targetId = before.Active.First(c => !c.isPlayer).id;
                    if (airing) air++; else open++;
                }
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "step " + step + " " + command.kind + ": " + result.reason);
                if (step % 12 == 0)
                {
                    var restored = JsonConvert.DeserializeObject<EpisodeState>(Json(before), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
                    var replay = new EpisodeEngine(restored).Apply(command);
                    Assert.That(replay.accepted, Is.True, replay.reason);
                    Assert.That(Json(replay.state), Is.EqualTo(Json(result.state)));
                    engine = new EpisodeEngine(replay.state); reloads++;
                }
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(open, Is.GreaterThan(0)); Assert.That(air, Is.GreaterThan(0)); Assert.That(reloads, Is.GreaterThan(0));
            TestContext.WriteLine("Stored cast " + size + ": open " + open + ", airing " + air + ", managed replay checkpoints " + reloads + ". Scripted simulation only, not UI/pacing acceptance.");
        }
    }
}
