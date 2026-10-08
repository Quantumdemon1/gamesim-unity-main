using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 28 (WAVE-D-NPC-PACTS-PLAN §0.3): Wave D's joint storage, inert. Every new field is born at
    /// its inert value, clones deep, and validation keeps each design's fields, lines and receipt out of
    /// a season until its own start week; the two new kinds are refused before anything is spent,
    /// drawn or logged; and nothing in this build writes any of it. Pure in-memory controls, run by
    /// the Unity-free subset; the save's half is PersistenceV28MigrationTests.
    /// </summary>
    public sealed class WaveDInertSchema28Tests
    {
        private static readonly string[] StartWeeks = { "allianceLeakRulesStartWeek", "pactPlanRulesStartWeek", "allWeekRulesStartWeek" };

        // ------------------------------------------------------------------ storage

        [TestCase(false)] [TestCase(true)]
        public void FactoriesCreateEveryWaveDFieldAtItsInertValue(bool builder)
        {
            var s = builder ? SeasonBuilder.Create(new SeasonBuilder.Choice(), 2805) : ContentCatalog.Create(2805);
            Accepted(s);
            Assert.That(s.schemaVersion, Is.EqualTo(28));
            Inert(s);
        }

        [Test]
        public void NewOwnersStartInert()
        {
            var social = NpcSocialState.Create(7);
            Assert.That(social.beatWeek, Is.Zero); Assert.That(social.beatWindow, Is.EqualTo(Windows.None));
            Assert.That(social.beatsFired, Is.Zero); Assert.That(social.beatSeats, Is.Zero);
            Assert.That(social.beatPlan, Is.Not.Null.And.Empty); Assert.That(social.acts, Is.Not.Null.And.Empty);
            Assert.That(new SeasonLedger().plans, Is.Not.Null.And.Empty);
            var row = new PactPlanRow();
            Assert.That(row.stance, Is.EqualTo(PactPlanStance.Open));
            Assert.That(row.present, Is.Not.Null.And.Empty); Assert.That(row.cameRound, Is.Not.Null.And.Empty);
            Assert.That(row.followed, Is.Not.Null.And.Empty); Assert.That(row.says, Is.Not.Null.And.Empty);
            Assert.That(PactPlanStance.All, Is.EqualTo(new[] { "open", "agreed", "countered", "low", "lapsed", "void" }));
        }

        /// <summary>The shapes the plan names, declared last in each owner so the migration's appended literals are the serializer's order.</summary>
        [Test]
        public void TheStoredShapesAreThePlansAndStandLastInTheirOwners()
        {
            Assert.That(Fields(typeof(PactPlanRow)), Is.EqualTo(new[]
                { "week", "allianceId", "throughId", "stance", "present", "cameRound", "followed", "says", "counterId", "targetId", "callerId" }));
            Assert.That(Fields(typeof(PlanSay)), Is.EqualTo(new[] { "memberId", "targetId" }));
            Assert.That(Fields(typeof(NpcActState)), Is.EqualTo(new[]
                { "id", "kind", "actorId", "partnerId", "subjectId", "room", "week", "window", "firedTick", "sighted", "overheard" }));
            Assert.That(Fields(typeof(EpisodeState)).Skip(Fields(typeof(EpisodeState)).Length - 3), Is.EqualTo(StartWeeks));
            Assert.That(Fields(typeof(NpcSocialState)).Skip(Fields(typeof(NpcSocialState)).Length - 6),
                Is.EqualTo(new[] { "beatWeek", "beatWindow", "beatsFired", "beatSeats", "beatPlan", "acts" }));
            Assert.That(Fields(typeof(SeasonLedger)).Last(), Is.EqualTo("plans"));
            foreach (string name in StartWeeks) Assert.That(typeof(EpisodeState).GetField(name).FieldType, Is.EqualTo(typeof(int)));
            Assert.That(typeof(NpcSocialState).GetField("beatPlan").FieldType, Is.EqualTo(typeof(List<string>)));
            Assert.That(typeof(NpcSocialState).GetField("acts").FieldType, Is.EqualTo(typeof(List<NpcActState>)));
            Assert.That(typeof(SeasonLedger).GetField("plans").FieldType, Is.EqualTo(typeof(List<PactPlanRow>)));
        }

        [Test]
        public void ClonesAreDeepAcrossEveryNewList()
        {
            var s = ContentCatalog.Create(2806);
            string npc = s.contestants.First(c => !c.isPlayer).id, other = s.contestants.Last(c => !c.isPlayer).id;
            s.npcSocial.beatPlan.Add(npc);
            s.npcSocial.acts.Add(new NpcActState { id = "1-3-0", kind = "talk", actorId = npc, partnerId = other, room = "Kitchen", week = 1, window = 3 });
            s.ledger.plans.Add(new PactPlanRow
            {
                week = 1, allianceId = "pact-x", throughId = npc, stance = PactPlanStance.Agreed,
                present = new List<string> { npc, other }, cameRound = new List<string> { npc }, followed = new List<string> { other },
                says = new List<PlanSay> { new PlanSay { memberId = npc, targetId = other } },
            });
            string before = Json(s);
            var copy = s.Clone();
            Assert.That(Json(copy), Is.EqualTo(before));
            Assert.That(copy.npcSocial.beatPlan, Is.Not.SameAs(s.npcSocial.beatPlan));
            Assert.That(copy.npcSocial.acts, Is.Not.SameAs(s.npcSocial.acts));
            Assert.That(copy.npcSocial.acts[0], Is.Not.SameAs(s.npcSocial.acts[0]));
            Assert.That(copy.ledger.plans, Is.Not.SameAs(s.ledger.plans));
            var row = copy.ledger.plans[0]; var source = s.ledger.plans[0];
            Assert.That(row, Is.Not.SameAs(source));
            Assert.That(row.present, Is.Not.SameAs(source.present)); Assert.That(row.cameRound, Is.Not.SameAs(source.cameRound));
            Assert.That(row.followed, Is.Not.SameAs(source.followed)); Assert.That(row.says, Is.Not.SameAs(source.says));
            Assert.That(row.says[0], Is.Not.SameAs(source.says[0]));
            copy.npcSocial.beatPlan.Clear(); copy.npcSocial.acts[0].sighted = true; copy.npcSocial.beatWindow = 2;
            row.present.Add(s.playerId); row.cameRound.Clear(); row.followed.Clear(); row.says[0].targetId = npc; row.stance = PactPlanStance.Void;
            copy.allWeekRulesStartWeek = 1;
            Assert.That(Json(s), Is.EqualTo(before), "A rejected candidate leaves nothing behind.");
        }

        // ------------------------------------------------------------------ the two kinds

        [Test]
        public void TheTwoKindsAreAppendedAfterNegotiate()
        {
            Assert.That((int)EpisodeCommandKind.Negotiate, Is.EqualTo(61));
            Assert.That((int)EpisodeCommandKind.AnswerPactPlan, Is.EqualTo(62));
            Assert.That((int)EpisodeCommandKind.WitnessNpcAct, Is.EqualTo(63));
            Assert.That(Enum.GetValues(typeof(EpisodeCommandKind)).Cast<int>().Max(), Is.EqualTo(63));
        }

        /// <summary>
        /// Refused at every step of a played season, before anything is spent, drawn or logged: the
        /// state is byte-identical after the refusal. A start week set changes nothing until the rule
        /// slice replaces the refusal.
        /// </summary>
        [TestCase(EpisodeCommandKind.AnswerPactPlan, false)] [TestCase(EpisodeCommandKind.WitnessNpcAct, false)]
        [TestCase(EpisodeCommandKind.AnswerPactPlan, true)] [TestCase(EpisodeCommandKind.WitnessNpcAct, true)]
        public void EachKindIsRefusedBeforeAnythingIsSpentDrawnOrLogged(EpisodeCommandKind kind, bool started)
        {
            var s = Played(2807);
            if (started) foreach (string name in StartWeeks) Set(s, name, 1);
            Accepted(s);
            var engine = new EpisodeEngine(s);
            int refusals = 0;
            for (int step = 0; step < 400 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var state = engine.Snapshot; string before = Json(state);
                var npcs = state.Active.Where(c => !c.isPlayer).ToList();
                var command = EpisodeEngineTests.Command(state, kind);
                command.id = "waved-" + kind + "-" + state.revision;
                command.targetId = npcs[0].id; command.secondTargetId = npcs.Count > 1 ? npcs[1].id : null;
                command.text = state.alliances.Select(a => a.id).FirstOrDefault() ?? "1-3-0";
                var refused = engine.Apply(command);
                Assert.That(refused.accepted, Is.False, state.phase + ": " + kind);
                Assert.That(refused.reason, Is.EqualTo(EpisodeEngine.WaveDKindRefusal));
                Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "Nothing spent, drawn, logged or received.");
                refusals++;
                var next = engine.Apply(Next(state));
                Assert.That(next.accepted, Is.True, next.reason);
            }
            Assert.That(refusals, Is.GreaterThan(20));
        }

        // ------------------------------------------------------------------ start weeks

        [TestCase("allianceLeakRulesStartWeek")] [TestCase("pactPlanRulesStartWeek")] [TestCase("allWeekRulesStartWeek")]
        public void EachStartWeekIsNeverOrWithinTheSeasonBoundary(string name)
        {
            var s = ContentCatalog.Create(2808);
            Assert.That(s.week, Is.EqualTo(1));
            foreach (int week in new[] { 0, 1, 2 }) { Set(s, name, week); Accepted(s); }
            foreach (int week in new[] { -1, 3, 101, 102, int.MinValue, int.MaxValue })
            { Set(s, name, week); Refused(s, "Wave D activation weeks"); }
        }

        // ------------------------------------------------------------------ D2 while off

        [TestCase("beatWeek", 1)] [TestCase("beatWeek", -1)]
        [TestCase("beatWindow", 0)] [TestCase("beatWindow", 3)] [TestCase("beatWindow", -2)]
        [TestCase("beatsFired", 1)] [TestCase("beatsFired", -1)] [TestCase("beatSeats", 1)] [TestCase("beatSeats", -1)]
        public void WithoutTheAllWeekRulesEveryBeatScalarIsInert(string field, int value)
        {
            var s = ContentCatalog.Create(2809);
            typeof(NpcSocialState).GetField(field).SetValue(s.npcSocial, value);
            Refused(s, "all-week rules");
        }

        [TestCase("plan")] [TestCase("act")]
        public void WithoutTheAllWeekRulesNothingHasPlannedOrActed(string what)
        {
            var s = ContentCatalog.Create(2810); string npc = s.contestants.First(c => !c.isPlayer).id;
            if (what == "plan") s.npcSocial.beatPlan.Add(npc);
            else s.npcSocial.acts.Add(Act(s, "1-3-0", npc));
            Refused(s, "all-week rules");
        }

        [TestCase("null-plan")] [TestCase("null-acts")] [TestCase("null-act")]
        public void MissingBeatCollectionsAreRefusedWhateverTheStartWeek(string defect)
        {
            foreach (int start in new[] { 0, 1 })
            {
                var s = ContentCatalog.Create(2811); s.allWeekRulesStartWeek = start;
                if (defect == "null-plan") s.npcSocial.beatPlan = null;
                else if (defect == "null-acts") s.npcSocial.acts = null;
                else s.npcSocial.acts.Add(null);
                Refused(s, "NPC beat collections");
            }
        }

        // ------------------------------------------------------------------ D2's storage bounds once on

        [Test]
        public void UnderTheAllWeekRulesAPlanAndItsActsAreBoundedStorage()
        {
            var s = AllWeek(); Accepted(s);
            string npc = s.contestants.First(c => !c.isPlayer).id;
            var defects = new Dictionary<string, Action<EpisodeState>>
            {
                ["player-in-plan"] = x => x.npcSocial.beatPlan.Add(x.playerId),
                ["unknown-in-plan"] = x => x.npcSocial.beatPlan.Add("nobody"),
                ["duplicate-in-plan"] = x => x.npcSocial.beatPlan.Add(x.npcSocial.beatPlan[0]),
                ["fired-past-plan"] = x => x.npcSocial.beatsFired = x.npcSocial.beatPlan.Count + 1,
                ["window-four"] = x => x.npcSocial.beatWindow = 4,
                ["future-plan-week"] = x => x.npcSocial.beatWeek = x.week + 1,
                ["plan-before-start"] = x => { x.allWeekRulesStartWeek = x.week + 1; },
                ["planless-window-with-beats"] = x => { x.npcSocial.beatWindow = Windows.None; x.npcSocial.beatWeek = 0; },
                ["seats-past-the-week"] = x => x.npcSocial.beatSeats = 1000,
                ["act-another-week"] = x => x.npcSocial.acts[0].week = x.week + 1,
                ["act-unknown-actor"] = x => x.npcSocial.acts[0].actorId = "nobody",
                ["act-unknown-partner"] = x => x.npcSocial.acts[0].partnerId = "nobody",
                ["act-no-kind"] = x => x.npcSocial.acts[0].kind = " ",
                ["act-window-four"] = x => x.npcSocial.acts[0].window = 4,
                ["act-negative-tick"] = x => x.npcSocial.acts[0].firedTick = -1,
                ["act-duplicate-id"] = x => x.npcSocial.acts.Add(x.npcSocial.acts[0].Clone()),
                ["overheard-unseen"] = x => x.npcSocial.acts[0].overheard = true,
                ["four-sighted-in-a-window"] = x =>
                {
                    for (int k = 0; k < 4; k++) { var act = Act(x, "1-3-" + (k + 10), npc); act.sighted = true; x.npcSocial.acts.Add(act); }
                },
            };
            foreach (var defect in defects)
            {
                var copy = s.Clone(); defect.Value(copy);
                Assert.That(EpisodeValidation.TryValidate(copy, out string error), Is.False, defect.Key);
                Assert.That(error.Contains("NPC beat plan") || error.Contains("NPC acts"), Is.True, defect.Key + ": " + error);
            }
            var three = s.Clone();
            for (int k = 0; k < 3; k++) { var act = Act(three, "1-3-" + (k + 10), npc); act.sighted = true; act.overheard = k == 0; three.npcSocial.acts.Add(act); }
            Accepted(three);
        }

        // ------------------------------------------------------------------ D3

        [Test]
        public void WithoutTheWarRoomsThereAreNoPlans()
        {
            var s = ContentCatalog.Create(2812);
            s.ledger.plans.Add(Plan(s, PactPlanStance.Agreed));
            Refused(s, "war rooms");
            s.ledger.plans = null; Refused(s, "pact plan");
            s = ContentCatalog.Create(2812); s.pactPlanRulesStartWeek = 1; s.ledger.plans.Add(null); Refused(s, "pact plan");
        }

        [Test]
        public void UnderTheWarRoomsAPlanIsBoundedStorage()
        {
            var s = ContentCatalog.Create(2813); s.pactPlanRulesStartWeek = 1;
            s.ledger.plans.Add(Plan(s, PactPlanStance.Agreed)); Accepted(s);
            string npc = s.contestants.First(c => !c.isPlayer).id;
            var defects = new Dictionary<string, Action<EpisodeState>>
            {
                ["unknown-stance"] = x => x.ledger.plans[0].stance = "maybe",
                ["open-outside-the-campaign"] = x => x.ledger.plans[0].stance = PactPlanStance.Open,
                ["no-pact"] = x => x.ledger.plans[0].allianceId = "",
                ["unknown-through"] = x => x.ledger.plans[0].throughId = "nobody",
                ["future-week"] = x => x.ledger.plans[0].week = x.week + 1,
                ["before-start"] = x => x.pactPlanRulesStartWeek = x.week + 1,
                ["duplicate-present"] = x => x.ledger.plans[0].present.Add(x.ledger.plans[0].present[0]),
                ["came-round-absent"] = x => x.ledger.plans[0].cameRound.Add(x.contestants.Last().id),
                ["followed-absent"] = x => x.ledger.plans[0].followed.Add(x.contestants.Last().id),
                ["said-twice"] = x => x.ledger.plans[0].says.Add(x.ledger.plans[0].says[0].Clone()),
                ["say-about-nobody"] = x => x.ledger.plans[0].says[0].targetId = "nobody",
                ["null-say"] = x => x.ledger.plans[0].says.Add(null),
                ["null-followed"] = x => x.ledger.plans[0].followed = null,
                ["unknown-target"] = x => x.ledger.plans[0].targetId = "nobody",
                ["unknown-caller"] = x => x.ledger.plans[0].callerId = "nobody",
                ["unknown-counter"] = x => x.ledger.plans[0].counterId = "nobody",
                ["two-for-one-pact-week"] = x => x.ledger.plans.Add(x.ledger.plans[0].Clone()),
            };
            foreach (var defect in defects)
            {
                var copy = s.Clone(); defect.Value(copy);
                Assert.That(EpisodeValidation.TryValidate(copy, out string error), Is.False, defect.Key);
                Assert.That(error.Contains("pact plan") || error.Contains("war rooms"), Is.True, defect.Key + ": " + error);
            }
            var another = s.Clone(); var second = Plan(another, PactPlanStance.Low); second.allianceId = "pact-y"; another.ledger.plans.Add(second);
            Accepted(another);
            Assert.That(npc, Is.Not.Null);
        }

        [Test]
        public void AnOpenPlanIsOnlyThisWeeksCampaigns()
        {
            // Week 1's first Campaign cannot hold an earlier week's plan: the week turns only after an
            // eviction, so play on to week 2's.
            var engine = new EpisodeEngine(Played(2814));
            for (int step = 0; step < 3000 && !(engine.Snapshot.phase == EpisodePhase.Campaign && engine.Snapshot.week >= 2); step++)
                Assert.That(engine.Apply(Next(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(s.week, Is.GreaterThan(1));
            s.pactPlanRulesStartWeek = s.week;
            s.ledger.plans.Add(Plan(s, PactPlanStance.Open)); Accepted(s);
            var earlier = s.Clone(); earlier.pactPlanRulesStartWeek = 1; earlier.ledger.plans[0].week = s.week - 1;
            Refused(earlier, "Invalid pact plan data.");
            // The same earlier row, settled, is lawful history: the refusal is the open plan's week.
            var settled = earlier.Clone(); settled.ledger.plans[0].stance = PactPlanStance.Agreed;
            Accepted(settled);
        }

        // ------------------------------------------------------------------ lines and the receipt

        [TestCase("sighting", "allWeekRulesStartWeek")] [TestCase("overheard", "allWeekRulesStartWeek")]
        [TestCase("pact-plan", "pactPlanRulesStartWeek")] [TestCase("double-dealing", "allianceLeakRulesStartWeek")]
        public void EachLineKindBelongsToItsOwnDesignFromItsStartWeek(string kind, string owner)
        {
            Assert.That(WaveDEventKinds.All, Has.Member(kind));
            var s = ContentCatalog.Create(2815);
            Line(s, kind);
            Refused(s, "none of their lines");
            foreach (string other in StartWeeks.Where(name => name != owner)) Set(s, other, 1);
            Refused(s, "none of their lines");
            Set(s, owner, 2); Refused(s, "none of their lines");
            Set(s, owner, 1); Accepted(s);
        }

        [Test]
        public void TheDoubleDealtReceiptBelongsToTheLeakRulesFromTheirStartWeek()
        {
            var s = ContentCatalog.Create(2816);
            var edge = s.relationships.First();
            edge.events.Add(new RelationshipEventState
            {
                sequence = s.nextSequence++, week = s.week, type = StoryReceipts.DoubleDealt,
                description = StoryReceipts.Describe(StoryReceipts.DoubleDealt, "Maya", true), impactScore = -10, decayable = false,
            });
            Refused(s, "none of their receipts");
            s.pactPlanRulesStartWeek = s.allWeekRulesStartWeek = 1; Refused(s, "none of their receipts");
            s.allianceLeakRulesStartWeek = 2; Refused(s, "none of their receipts");
            s.allianceLeakRulesStartWeek = 1; Accepted(s);
        }

        [Test]
        public void TheVocabularyIsConstantsOnly()
        {
            Assert.That(WaveDEventKinds.All, Is.EqualTo(new[] { "sighting", "overheard", "pact-plan", "double-dealing" }));
            Assert.That(StoryReceipts.DoubleDealt, Is.EqualTo("story:double-dealt"));
            Assert.That(StoryReceipts.Permanent, Has.Member(StoryReceipts.DoubleDealt));
            Assert.That(StoryReceipts.IsKnown(StoryReceipts.DoubleDealt), Is.True);
            Assert.That(StoryReceipts.Impact(StoryReceipts.DoubleDealt), Is.EqualTo(-10));
            Assert.That(RelationshipLedger.Decays(StoryReceipts.DoubleDealt), Is.False, "Permanent, as the plan's receipt is.");
            Assert.That(StoryReceipts.Describe(StoryReceipts.DoubleDealt, "Maya", true), Does.Contain("Maya"));
            Assert.That(StoryReceipts.Describe(StoryReceipts.DoubleDealt, "Maya", false), Does.Contain("Maya"));
            Assert.That(EpisodeEngine.WaveDKindRefusal, Is.EqualTo("Not available in this season."));
        }

        // ------------------------------------------------------------------ nothing writes it

        /// <summary>
        /// Whole seasons, with every start week 0 and with every one set: nothing in this build plans a
        /// beat, records an act, writes a plan, logs a Wave D line or gives the receipt.
        /// </summary>
        [TestCase(2817u, false)] [TestCase(2818u, true)]
        public void NothingInThisBuildWritesWaveDStorage(uint seed, bool started)
        {
            var s = Played(seed);
            if (started) foreach (string name in StartWeeks) Set(s, name, 1);
            var engine = new EpisodeEngine(s);
            int steps = 0;
            while (engine.Snapshot.phase != EpisodePhase.Finished && steps < 600)
            {
                var result = engine.Apply(Next(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                var state = engine.Snapshot; steps++;
                Assert.That(state.npcSocial.beatWindow, Is.EqualTo(Windows.None));
                Assert.That(state.npcSocial.beatWeek + state.npcSocial.beatsFired + state.npcSocial.beatSeats, Is.Zero);
                Assert.That(state.npcSocial.beatPlan, Is.Empty); Assert.That(state.npcSocial.acts, Is.Empty);
                Assert.That(state.ledger.plans, Is.Empty);
                Assert.That(state.events.Any(e => WaveDEventKinds.All.Contains(e.kind)), Is.False);
                Assert.That(state.relationships.Any(r => r.events.Any(e => e.type == StoryReceipts.DoubleDealt)), Is.False);
                foreach (string name in StartWeeks) Assert.That(Get(state, name), Is.EqualTo(started ? 1 : 0));
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A fresh season with the rules a director season plays (none of Wave D's).</summary>
        private static EpisodeState Played(uint seed)
        {
            var s = ContentCatalog.Create(seed);
            s.competitionRulesVersion = CompetitionRules.Current; s.haveNotRulesStartWeek = s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableFinale(s); EpisodeEngine.EnableCommitments(s);
            Accepted(s); Inert(s);
            return s;
        }

        /// <summary>Week 1's free time with the all-week rules on and a lawful plan with one act.</summary>
        private static EpisodeState AllWeek()
        {
            var s = ContentCatalog.Create(2819); s.allWeekRulesStartWeek = 1;
            var npcs = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.npcSocial.beatWeek = 1; s.npcSocial.beatWindow = Windows.AfterEviction; s.npcSocial.beatSeats = 2;
            s.npcSocial.beatPlan.AddRange(npcs.Take(3)); s.npcSocial.beatsFired = 1;
            s.npcSocial.acts.Add(Act(s, "1-3-0", npcs[0]));
            return s;
        }

        /// <summary>The engine tests' next lawful command, answering a finale question with an offered response.</summary>
        private static EpisodeCommand Next(EpisodeState state)
        {
            var command = EpisodeEngineTests.NextCommand(state);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(state))
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.finalistId == state.playerId)
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
            }
            return command;
        }

        private static NpcActState Act(EpisodeState s, string id, string actor) => new NpcActState
        {
            id = id, kind = "talk", actorId = actor, partnerId = s.contestants.Last(c => !c.isPlayer && c.id != actor).id,
            room = "Kitchen", week = s.week, window = Windows.AfterEviction, firedTick = 0,
        };

        private static PactPlanRow Plan(EpisodeState s, string stance)
        {
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            return new PactPlanRow
            {
                week = s.week, allianceId = "pact-x", throughId = npcs[0], stance = stance,
                present = new List<string> { s.playerId, npcs[0], npcs[1] }, cameRound = new List<string> { npcs[0] },
                followed = new List<string> { npcs[0], npcs[1] },
                says = new List<PlanSay> { new PlanSay { memberId = npcs[0], targetId = npcs[2] }, new PlanSay { memberId = npcs[1], targetId = npcs[2] } },
                targetId = npcs[2], callerId = s.playerId,
            };
        }

        private static void Line(EpisodeState s, string kind)
        {
            if (s.events.Count >= 256) s.events.RemoveAt(0);
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = s.week, phase = s.phase, kind = kind, text = "A Wave D line.", audienceIds = new List<string> { s.playerId } });
        }

        private static void Inert(EpisodeState s)
        {
            foreach (string name in StartWeeks) Assert.That(Get(s, name), Is.Zero, name);
            Assert.That(s.npcSocial.beatWeek, Is.Zero); Assert.That(s.npcSocial.beatWindow, Is.EqualTo(-1));
            Assert.That(s.npcSocial.beatsFired, Is.Zero); Assert.That(s.npcSocial.beatSeats, Is.Zero);
            Assert.That(s.npcSocial.beatPlan, Is.Not.Null.And.Empty); Assert.That(s.npcSocial.acts, Is.Not.Null.And.Empty);
            Assert.That(s.ledger.plans, Is.Not.Null.And.Empty);
        }

        private static string[] Fields(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name).ToArray();
        private static void Set(EpisodeState s, string name, int value) => typeof(EpisodeState).GetField(name).SetValue(s, value);
        private static int Get(EpisodeState s, string name) => (int)typeof(EpisodeState).GetField(name).GetValue(s);

        private static void Accepted(EpisodeState s)
        { string before = Json(s); Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error); Assert.That(Json(s), Is.EqualTo(before)); }

        private static void Refused(EpisodeState s, string reason)
        {
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Does.Contain(reason));
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s);
    }
}
