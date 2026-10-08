using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The pure digest's separate field observer, not a storage serializer. Lawful witnesses
    /// select only fresh supported flags and use public Apply afterward. Altered JSON/state
    /// controls are detached diagnostics after a proved valid baseline, never installed saves.
    /// Real SaveJson, storage and migration have separate runtime companion cases.
    /// </summary>
    public sealed class LegacyDigestSchema27ObserverTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void FreshSupportedModesKeepTheirDefaultTraceAndReturnDetachedFieldViews(int mode)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, "fresh");
            var trace = LegacyDigestSchema27TestStates.Trace(state);
            var field = Accept(state, trace);
            Assert.That(field["schemaVersion"].Value<int>(), Is.EqualTo(27));
            Assert.That(field.Property("Active"), Is.Null);
            Assert.That(trace["Active"], Is.InstanceOf<JArray>());
            Assert.That(field["unifiedCommitmentRulesVersion"].Value<int>(), Is.EqualTo(mode == 0 ? 0 : 1));
            Assert.That(field["unifiedHearingRulesVersion"].Value<int>(), Is.EqualTo(mode == 2 ? 1 : 0));
            string stateBefore = LegacyDigestSchema27TestStates.Text(state), traceBefore = Text(trace);
            Assert.That(field, Is.Not.SameAs(trace));
            Assert.That(field["contestants"][0], Is.Not.SameAs(trace["contestants"][0]));
            field["contestants"][0]["name"] = "Detached field observer";
            ((JArray)field["contestants"]).RemoveAt(0);
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(stateBefore));
        }

        [TestCase(1, "promise")] [TestCase(2, "promise")]
        [TestCase(1, "deal")] [TestCase(2, "deal")]
        [TestCase(1, "broken-promise")] [TestCase(2, "broken-promise")]
        [TestCase(1, "broken-deal")] [TestCase(2, "broken-deal")]
        public void ActualPublicCanonicalOwnersKeepZeroChronologyAndTheirRealSourcePolicy(int mode, string kind)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, kind);
            var trace = LegacyDigestSchema27TestStates.Trace(state);
            var field = Accept(state, trace);
            var rows = (JArray)field["unifiedCommitments"];
            Assert.That(rows.Count, Is.GreaterThan(0));
            bool promise = kind.Contains("promise");
            var owner = LegacyDigestSchema27TestStates.Owner(state, mode, kind);
            var row = rows.OfType<JObject>().Single(r => (string)r["id"] == owner.id);
            Assert.That((string)row["sourcePolicy"], Is.EqualTo(promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy));
            Assert.That(row["voteBindingWeek"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(row["voteFirstRevealWeek"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)row["voteBindingWeek"], Is.Zero);
            Assert.That((int)row["voteFirstRevealWeek"], Is.Zero);
            Assert.That(row["targetId"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(row["subtype"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(JToken.DeepEquals(row, trace["unifiedCommitments"].Single(r => (string)r["id"] == owner.id)), Is.True);
            if (kind.StartsWith("broken-", StringComparison.Ordinal))
            {
                Assert.That(owner.status, Is.EqualTo(DealStatus.Broken));
                Assert.That(owner.brokenById, Is.EqualTo(state.playerId));
                Assert.That(owner.settledWeek, Is.EqualTo(state.week));
                if (!promise && mode == 2)
                {
                    Assert.That(state.unifiedHearingEvidence.Any(e => e.fact.refId == owner.id), Is.True);
                    Assert.That(state.unifiedHearingReceipts.Any(r => r.incidentKey == owner.settlementEffectKey && r.kind == UnifiedCommitmentHearings.Initial), Is.True);
                }
            }
        }

        [TestCase("active-missing")] [TestCase("active-null")] [TestCase("active-bool")]
        [TestCase("active-row")] [TestCase("active-order")]
        [TestCase("story-missing")] [TestCase("story-null")] [TestCase("story-string")] [TestCase("story-opposite")]
        public void OnlyTheTwoActualGetterValuesAndTypesMayBeExcluded(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "events");
            var trace = LegacyDigestSchema27TestStates.Trace(state);
            Accept(state, trace);
            var firstEvent = (JObject)trace["houseEvents"][0];
            switch (defect)
            {
                case "active-missing": trace.Remove("Active"); break;
                case "active-null": trace["Active"] = JValue.CreateNull(); break;
                case "active-bool": trace["Active"] = true; break;
                case "active-row": trace["Active"][0]["name"] = "Not its actual contestant row"; break;
                case "active-order":
                    var people = (JArray)trace["Active"];
                    Assert.That(people.Count, Is.GreaterThan(1));
                    var first = people[0]; first.Remove(); people.Add(first); break;
                case "story-missing": firstEvent.Remove("IsStory"); break;
                case "story-null": firstEvent["IsStory"] = JValue.CreateNull(); break;
                case "story-string": firstEvent["IsStory"] = "story"; break;
                case "story-opposite": firstEvent["IsStory"] = !firstEvent["IsStory"].Value<bool>(); break;
                default: Assert.Fail("Unknown getter control."); break;
            }
            RefusedUnchanged(state, trace);
        }

        [TestCase("Active-contestant")] [TestCase("IsStory-contestant")]
        [TestCase("IsStory-root")] [TestCase("Active-event")]
        [TestCase("unknown-root")] [TestCase("unknown-event")]
        public void GetterNamesAtOtherPathsAndUnknownMembersAreNeverPruned(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "events");
            var trace = LegacyDigestSchema27TestStates.Trace(state); Accept(state, trace);
            switch (defect)
            {
                case "Active-contestant": trace["contestants"][0]["Active"] = new JArray(); break;
                case "IsStory-contestant": trace["contestants"][0]["IsStory"] = false; break;
                case "IsStory-root": trace["IsStory"] = false; break;
                case "Active-event": trace["houseEvents"][0]["Active"] = new JArray(); break;
                case "unknown-root": trace["futureObserverMember"] = JValue.CreateNull(); break;
                case "unknown-event": trace["houseEvents"][0]["futureObserverMember"] = JValue.CreateNull(); break;
                default: Assert.Fail("Unknown path control."); break;
            }
            RefusedUnchanged(state, trace);
        }

        [TestCase("voteBindingWeek", "missing")] [TestCase("voteFirstRevealWeek", "missing")]
        [TestCase("voteBindingWeek", "null")] [TestCase("voteFirstRevealWeek", "null")]
        [TestCase("voteBindingWeek", "string")] [TestCase("voteFirstRevealWeek", "string")]
        [TestCase("voteBindingWeek", "bool")] [TestCase("voteFirstRevealWeek", "bool")]
        [TestCase("voteBindingWeek", "fractional")] [TestCase("voteFirstRevealWeek", "fractional")]
        [TestCase("voteBindingWeek", "nonzero")] [TestCase("voteFirstRevealWeek", "nonzero")]
        public void ChronologyProjectionRequiresBothPresentLiteralIntegerZeros(string name, string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var trace = LegacyDigestSchema27TestStates.Trace(state); Accept(state, trace);
            var row = (JObject)trace["unifiedCommitments"][0];
            switch (defect)
            {
                case "missing": row.Remove(name); break;
                case "null": row[name] = JValue.CreateNull(); break;
                case "string": row[name] = "0"; break;
                case "bool": row[name] = false; break;
                case "fractional": row[name] = 0.5; break;
                case "nonzero": row[name] = 1; break;
                default: Assert.Fail("Unknown marker control."); break;
            }
            RefusedUnchanged(state, trace);
        }

        [TestCase("target-missing")] [TestCase("target-value")]
        [TestCase("subtype-missing")] [TestCase("subtype-value")]
        [TestCase("archive-missing")] [TestCase("archive-null")] [TestCase("archive-row")]
        [TestCase("authority-two")] [TestCase("hearing-two")] [TestCase("future-header")]
        public void EarlierNeutralExtensionsAndUnknownAuthoritiesCannotBeErased(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var trace = LegacyDigestSchema27TestStates.Trace(state); Accept(state, trace);
            var row = (JObject)trace["unifiedCommitments"][0];
            switch (defect)
            {
                case "target-missing": row.Remove("targetId"); break;
                case "target-value": row["targetId"] = state.playerId; break;
                case "subtype-missing": row.Remove("subtype"); break;
                case "subtype-value": row["subtype"] = "vote_together"; break;
                case "archive-missing": trace.Remove("unifiedVoteReveals"); break;
                case "archive-null": trace["unifiedVoteReveals"] = JValue.CreateNull(); break;
                case "archive-row": trace["unifiedVoteReveals"] = new JArray(new JObject()); break;
                case "authority-two": trace["unifiedCommitmentRulesVersion"] = 2; break;
                case "hearing-two": trace["unifiedHearingRulesVersion"] = 2; break;
                case "future-header": trace["schemaVersion"] = 28; break;
                default: Assert.Fail("Unknown neutral-state control."); break;
            }
            RefusedUnchanged(state, trace);
        }

        [TestCase("commitments-off")] [TestCase("knowledge-off")]
        [TestCase("broken-attribution")] [TestCase("missing-initial")]
        public void ActualCurrentValidationCannotBeReplacedByHistoricalJsonShape(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "broken-deal");
            Accept(state, LegacyDigestSchema27TestStates.Trace(state));
            var owner = LegacyDigestSchema27TestStates.Owner(state, 2, "broken-deal");
            switch (defect)
            {
                case "commitments-off": state.commitmentRulesStartWeek = 0; break;
                case "knowledge-off": state.story.rulesStartWeek = 0; break;
                case "broken-attribution": owner.brokenById = owner.beneficiaryId; break;
                case "missing-initial":
                    int removed = state.unifiedHearingReceipts.RemoveAll(r => r.incidentKey == owner.settlementEffectKey && r.kind == UnifiedCommitmentHearings.Initial);
                    Assert.That(removed, Is.EqualTo(1)); break;
                default: Assert.Fail("Unknown current-core control."); break;
            }
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.False, defect);
            Assert.That(reason, Is.Not.Empty);
            RefusedUnchanged(state, LegacyDigestSchema27TestStates.Trace(state));
        }

        [TestCase("session")] [TestCase("rng")]
        public void AValidTraceFromAnotherStateIsNotThisStatesObserver(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            Accept(state, LegacyDigestSchema27TestStates.Trace(state));
            var alternative = state.Clone();
            if (defect == "session") alternative.sessionId += "-other-valid-session";
            else alternative.randomState = alternative.randomState == uint.MaxValue ? 0 : alternative.randomState + 1;
            LegacyDigestSchema27TestStates.Valid(alternative);
            var otherTrace = LegacyDigestSchema27TestStates.Trace(alternative);
            Accept(alternative, otherTrace);
            Assert.That(Text(otherTrace), Is.Not.EqualTo(LegacyDigestSchema27TestStates.Text(state)));
            RefusedUnchanged(state, otherTrace);
        }

        [TestCase(1)] [TestCase(2)]
        public void ProjectTraceRetainsGettersAndChangesOnlyHeaderAndTwoZeros(int mode)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, "broken-deal");
            var trace = LegacyDigestSchema27TestStates.Trace(state); Accept(state, trace);
            string stateBefore = LegacyDigestSchema27TestStates.Text(state), before = Text(trace);
            var projected = LegacyDigestSchema27Observer.ProjectTrace(state, trace);
            var expected = LegacyDigestSchema27TestStates.Neutral26(trace);
            Assert.That(Text(projected), Is.EqualTo(Text(expected)), "Compact JSON also pins every retained property/list position.");
            Assert.That(projected["Active"], Is.InstanceOf<JArray>());
            Assert.That(JToken.DeepEquals(projected["Active"], trace["Active"]), Is.True);
            for (int i = 0; i < ((JArray)trace["houseEvents"]).Count; i++)
                Assert.That(JToken.DeepEquals(projected["houseEvents"][i]["IsStory"], trace["houseEvents"][i]["IsStory"]), Is.True);
            var inverse = LegacyDigestSchema27TestStates.Restore27(projected);
            Assert.That(Text(inverse), Is.EqualTo(before), "The neutral additions restore original compact property order too.");
            projected["Active"][0]["name"] = "Detached getter trace";
            ((JArray)projected["unifiedCommitments"]).Clear();
            Assert.That(Text(trace), Is.EqualTo(before));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(stateBefore));
        }

        [TestCase("root-property")] [TestCase("event-property")] [TestCase("canonical-property")]
        public void TokenEqualityDoesNotWaiveDefaultTracePropertyOrder(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, defect == "event-property" ? "events" : "promise");
            var trace = LegacyDigestSchema27TestStates.Trace(state); Accept(state, trace);
            var original = (JObject)trace.DeepClone();
            JObject target = defect == "root-property" ? trace : (JObject)trace[defect == "event-property" ? "houseEvents" : "unifiedCommitments"][0];
            var first = target.Properties().First(); first.Remove(); target.Add(first);
            Assert.That(JToken.DeepEquals(trace, original), Is.True, "Only object-property order changed.");
            Assert.That(Text(trace), Is.Not.EqualTo(Text(original)));
            RefusedUnchanged(state, trace);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void RealPublicFinishedSeasonKeepsTerminalStatusesAndGetterRowOrder(int mode)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, "finished");
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(state.contestants.Any(c => c.status == ContestantStatus.Winner), Is.True);
            var trace = LegacyDigestSchema27TestStates.Trace(state); var field = Accept(state, trace);
            Assert.That(((JArray)trace["Active"]).Count, Is.EqualTo(state.Active.Count()));
            Assert.That(Text(field["contestants"]), Is.EqualTo(Text(trace["contestants"])));
            var projected = LegacyDigestSchema27Observer.ProjectTrace(state, trace);
            Assert.That(Text(projected), Is.EqualTo(Text(LegacyDigestSchema27TestStates.Neutral26(trace))));
        }

        private static JObject Accept(EpisodeState state, JObject trace)
        {
            LegacyDigestSchema27TestStates.Valid(state);
            string stateBefore = LegacyDigestSchema27TestStates.Text(state), traceBefore = Text(trace);
            var field = LegacyDigestSchema27Observer.CheckFieldObserver(state, trace);
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(stateBefore), "Includes RNG, receipts and default getter/list order.");
            var expected = LegacyDigestSchema27TestStates.WithoutGetters(trace);
            Assert.That(Text(field), Is.EqualTo(Text(expected)));
            Assert.That(field, Is.Not.SameAs(trace));
            return field;
        }

        private static void RefusedUnchanged(EpisodeState state, JObject trace)
        {
            string stateBefore = LegacyDigestSchema27TestStates.Text(state), traceBefore = Text(trace);
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema27Observer.CheckFieldObserver(state, trace));
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema27Observer.ProjectTrace(state, trace));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(stateBefore));
        }

        private static string Text(JToken value) => LegacyDigestSchema27TestStates.Text(value);
    }

    /// <summary>Test-owned legal public witnesses shared only by the two new companion fixtures.</summary>
    internal static class LegacyDigestSchema27TestStates
    {
        private sealed class WitnessRecord
        {
            internal EpisodeState State;
            internal string OwnerId;
        }

        private static readonly Dictionary<string, WitnessRecord> Witnesses = new Dictionary<string, WitnessRecord>(StringComparer.Ordinal);
        private const uint FirstSeed = 2505, EndSeed = 2537;

        internal static EpisodeState Witness(int mode, string kind)
        {
            string key = mode + ":" + kind;
            lock (Witnesses)
            {
                if (!Witnesses.TryGetValue(key, out var witness))
                {
                    var state = Build(mode, kind, out string ownerId); Valid(state);
                    witness = new WitnessRecord { State = state.Clone(), OwnerId = ownerId };
                    Witnesses.Add(key, witness);
                }
                return witness.State.Clone();
            }
        }

        internal static UnifiedCommitmentState Owner(EpisodeState state, int mode, string kind)
        {
            lock (Witnesses)
            {
                Assert.That(Witnesses.TryGetValue(mode + ":" + kind, out var witness), Is.True);
                Assert.That(witness.OwnerId, Is.Not.Null.And.Not.Empty);
                return state.unifiedCommitments.Single(row => row.id == witness.OwnerId);
            }
        }

        private static EpisodeState Build(int mode, string kind, out string ownerId)
        {
            ownerId = null;
            if (kind == "fresh") return Fresh(mode, FirstSeed);
            if (kind == "promise")
            {
                var engine = new EpisodeEngine(Fresh(mode, FirstSeed));
                var command = Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety);
                command.targetId = engine.Snapshot.Active.First(c => !c.isPlayer).id;
                Commit(engine, command);
                ownerId = engine.Snapshot.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise).id;
                return engine.Snapshot;
            }
            if (kind == "events" || kind == "finished")
            {
                var engine = new EpisodeEngine(Fresh(mode, FirstSeed));
                for (int step = 0; step < 512; step++)
                {
                    var state = engine.Snapshot;
                    if (kind == "events" && state.houseEvents.Count > 0) return state;
                    if (state.phase == EpisodePhase.Finished)
                    {
                        Assert.That(kind, Is.EqualTo("finished"), "Real source progression must actually produce its event witness.");
                        return state;
                    }
                    Commit(engine, Next(state));
                }
                Assert.Fail("Actual public " + kind + " boundary was not reached within 512 steps.");
            }
            Assert.That(mode, Is.GreaterThan(0));
            Assert.That(new[] { "deal", "broken-promise", "broken-deal" }, Has.Member(kind));
            bool promise = kind == "broken-promise";
            for (uint seed = FirstSeed; seed < EndSeed; seed++)
            {
                var engine = new EpisodeEngine(Fresh(mode, seed));
                for (int step = 0; step < 512 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                {
                    var context = engine.Snapshot;
                    if (context.phase == EpisodePhase.Nomination && context.hohId == context.playerId && context.nominees.Count == 0 && context.pendingDiary == null)
                    {
                        foreach (var partner in EpisodeEngine.NominationCandidates(context).Where(c => UnifiedCommitments.Binding(context, context.playerId, c.id).Count == 0))
                        {
                            if (!promise && !PlayerDeals.CanPropose(context, partner.id, DealKind.SafetyAgreement, null, out _)) continue;
                            var attempt = new EpisodeEngine(context);
                            var command = Command(context, promise ? EpisodeCommandKind.PromiseSafety : EpisodeCommandKind.ProposeDeal);
                            command.targetId = partner.id; if (!promise) command.text = DealKind.SafetyAgreement;
                            Commit(attempt, command);
                            var after = attempt.Snapshot;
                            var row = after.unifiedCommitments.SingleOrDefault(r => !context.unifiedCommitments.Any(old => old.id == r.id)
                                && r.origin == (promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.PlayerDeal)
                                && r.makerId == context.playerId && r.beneficiaryId == partner.id);
                            if (row == null) { Assert.That(promise, Is.False, "Only a genuine proposal may have been declined."); continue; }
                            ownerId = row.id;
                            if (kind == "deal") return after;
                            var nominate = Command(after, EpisodeCommandKind.Nominate);
                            nominate.targetId = partner.id;
                            nominate.secondTargetId = EpisodeEngine.NominationCandidates(after).First(c => c.id != partner.id).id;
                            Commit(attempt, nominate);
                            var result = attempt.Snapshot;
                            var settled = result.unifiedCommitments.Single(r => r.id == row.id);
                            Assert.That(settled.status, Is.EqualTo(DealStatus.Broken));
                            Assert.That(settled.brokenById, Is.EqualTo(result.playerId));
                            Assert.That(result.acceptedCommandIds.Last(), Is.EqualTo(nominate.id));
                            return result;
                        }
                    }
                    Commit(engine, Next(context));
                }
            }
            Assert.Fail("No actual public " + kind + " owner in the fixed 32-seed, 512-step source search."); return null;
        }

        private static EpisodeState Fresh(int mode, uint seed)
        {
            Assert.That(mode, Is.InRange(0, 2));
            var state = ContentCatalog.Create(seed);
            Assert.That(state.schemaVersion, Is.EqualTo(27));
            Assert.That(state.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(state.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(state.unifiedCommitments, Is.Empty);
            Assert.That(state.unifiedHearingEvidence, Is.Empty);
            Assert.That(state.unifiedHearingReceipts, Is.Empty);
            Assert.That(state.unifiedVoteReveals, Is.Empty);
            Assert.That(state.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(state.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            state.competitionRulesVersion = CompetitionRules.Current;
            state.haveNotRulesStartWeek = state.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(state); EpisodeEngine.EnableRead(state); EpisodeEngine.EnableLevers(state);
            EpisodeEngine.EnableWeek(state); EpisodeEngine.EnableAgency(state); EpisodeEngine.EnableFinale(state); EpisodeEngine.EnableCommitments(state);
            if (mode > 0) EpisodeEngine.EnableEconomy(state);
            // Test-only fresh selection precedes the constructor. No existing row, role,
            // phase, random stream, audience, receipt or history is converted or cleared.
            state.unifiedCommitmentRulesVersion = mode == 0 ? 0 : 1;
            state.unifiedHearingRulesVersion = mode == 2 ? 1 : 0;
            Valid(state); return state;
        }

        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind) => new EpisodeCommand {
            id = "digest27-observer-public-" + state.revision + "-" + kind, actorId = state.playerId,
            expectedRevision = state.revision, expectedPhase = state.phase, kind = kind };

        private static EpisodeCommand Next(EpisodeState state)
        {
            var command = EpisodeEngineTests.NextCommand(state);
            command.id = "digest27-observer-public-" + state.revision + "-" + command.kind;
            if (command.kind == EpisodeCommandKind.Compete) command.performance = 1;
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(state))
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.finalistId == state.playerId)
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
            }
            return command;
        }

        private static void Commit(EpisodeEngine engine, EpisodeCommand command)
        {
            var before = engine.Snapshot; string beforeText = Text(before);
            string commandBefore = JsonConvert.SerializeObject(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, "Actual seed " + before.seed + ", " + before.phase + ", " + command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
            Assert.That(Text(before), Is.EqualTo(beforeText));
            Assert.That(JsonConvert.SerializeObject(command), Is.EqualTo(commandBefore));
            Assert.That(Text(result.state), Is.EqualTo(Text(engine.Snapshot)));
            Valid(result.state);
        }

        internal static void Valid(EpisodeState state)
        {
            string before = Text(state);
            Assert.That(state.schemaVersion, Is.EqualTo(27));
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            Assert.That(state.unifiedVoteReveals, Is.Empty);
            Assert.That(state.unifiedCommitments.All(r => r.voteBindingWeek == 0 && r.voteFirstRevealWeek == 0 && r.targetId == null && r.subtype == null), Is.True);
            Assert.That(Text(state), Is.EqualTo(before));
        }

        internal static JObject Trace(EpisodeState state) => JObject.FromObject(state);
        internal static string Text(EpisodeState state) => Trace(state).ToString(Formatting.None);
        internal static string Text(JToken token) => token.ToString(Formatting.None);

        internal static JObject WithoutGetters(JObject trace)
        {
            var result = (JObject)trace.DeepClone(); result.Remove("Active");
            foreach (JObject row in (JArray)result["houseEvents"]) row.Remove("IsStory");
            return result;
        }

        internal static JObject Neutral26(JObject source)
        {
            var result = (JObject)source.DeepClone(); result["schemaVersion"] = 26;
            foreach (JObject row in (JArray)result["unifiedCommitments"])
            { row.Remove("voteBindingWeek"); row.Remove("voteFirstRevealWeek"); }
            return result;
        }

        internal static JObject Restore27(JObject neutral)
        {
            var result = (JObject)neutral.DeepClone(); result["schemaVersion"] = 27;
            foreach (JObject row in (JArray)result["unifiedCommitments"])
            { row.Add("voteBindingWeek", 0); row.Add("voteFirstRevealWeek", 0); }
            return result;
        }
    }
}
