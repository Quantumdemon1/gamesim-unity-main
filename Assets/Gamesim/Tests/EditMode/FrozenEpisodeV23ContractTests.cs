using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Native Edit tests for the frozen schema-23 contract. Synthetic payloads explicitly remove
    /// only schema27's proven zero markers, schema26's proven inert extensions, schema25's disabled/empty hearing and schema24's disabled/empty foundation
    /// from current reducer states before claiming23.
    /// No historical fixture bytes are changed; these are not captured shipping-save evidence.
    /// </summary>
    public sealed class FrozenEpisodeV23ContractTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type Contract = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV23", true);
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", Static).Invoke(null, null);
        private static JObject Payload(EpisodeState s)
        {
            EpisodeSaveValidation.Validate(s);
            Assert.That(s.schemaVersion, Is.EqualTo(27));
            Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedHearingEvidence, Is.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Empty);
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Empty);
            var historical = JObject.FromObject(s, Serializer());
            PersistenceMigrationTests.StripSchema26(historical);
            historical.Remove("unifiedHearingRulesVersion");
            historical.Remove("unifiedHearingEvidence");
            historical.Remove("unifiedHearingReceipts");
            historical.Remove("unifiedCommitmentRulesVersion");
            historical.Remove("unifiedCommitments");
            historical["schemaVersion"] = 23;
            return historical;
        }
        private static object Invoke(Type type, string name, JObject o)
        {
            try { return type.GetMethod(name, Static).Invoke(null, new object[] { o }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Validate(JObject o) => Invoke(Contract, "Validate", o);
        private static JObject Project(JObject o) => (JObject)Invoke(Contract, "ValidatedProjection", o);
        private static void Accept(JObject o)
        {
            string before = o.ToString(Formatting.None);
            Assert.DoesNotThrow(() => Validate(o));
            Assert.That(o.ToString(Formatting.None), Is.EqualTo(before), "Validation never mutates its caller's tree.");
        }
        private static void Reject(JObject o)
        {
            string before = o.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => Validate(o));
            Assert.That(o.ToString(Formatting.None), Is.EqualTo(before), "Rejected data is preserved, not repaired or discarded.");
        }
        private static EpisodeState Fresh(bool economy = true)
        {
            var s = EconomyRulesTests.Fresh(enable: economy);
            s.strategyRulesStartWeek = 1; s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableRead(s); EpisodeEngine.EnableCommitments(s);
            return s;
        }
        private static EpisodeState Apply(EpisodeState s, EpisodeCommandKind kind, string target = null, string text = null, string second = null)
        {
            var result = new EpisodeEngine(s).Apply(new EpisodeCommand { id = "frozen23-" + s.revision + "-" + kind,
                actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
                kind = kind, targetId = target, text = text, secondTargetId = second });
            Assert.That(result.accepted, Is.True, result.reason);
            EpisodeSaveValidation.Validate(result.state);
            return result.state;
        }
        private static EpisodeState Pending()
        {
            var s = Fresh(); s.phase = EpisodePhase.Nomination; s.hohId = s.playerId; s.nominees.Clear();
            NpcSocialActions.Court(s, s.Active.First(c => !c.isPlayer), s.Find(s.playerId));
            Assert.That(s.replyCards, Has.Count.EqualTo(1));
            Assert.That(s.replyCards[0].kind, Is.EqualTo("pitch"));
            return s;
        }
        private static EpisodeState Answered(string key = "hear")
        {
            var s = Pending(); string id = s.replyCards[0].id;
            s = Apply(s, EpisodeCommandKind.ReplyToHouseguest, id, "feel-out");
            return Apply(s, EpisodeCommandKind.ReplyToHouseguest, id, key);
        }
        private static EpisodeState Night()
        {
            var s = Fresh(); var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToArray();
            s.phase = EpisodePhase.Eviction; s.evictionStage = EvictionStage.Speeches;
            s.hohId = npcs[1]; s.vetoHolderId = s.hohId; s.vetoResolved = true;
            s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).Select(c => c.id).ToList();
            s.nominees = new List<string> { s.playerId, npcs[0] };
            return s;
        }
        private static EpisodeState Delivered(string approach = "emotional") => Apply(Night(),
            EpisodeCommandKind.SubmitEvictionSpeech, text: approach == "quiet" ? "" : "Please hear my complete case.\nThese are my own words.", second: approach);
        private static EpisodeState Campaign()
        {
            var s = Night(); s.phase = EpisodePhase.Campaign; s.evictionStage = EvictionStage.Interaction;
            string voter = EpisodeEngine.Voters(s).First(c => !c.isPlayer).id;
            return Apply(s, EpisodeCommandKind.Lobby, voter, LobbyAsk.Encode("vote", "emotional"), s.playerId);
        }
        private static JObject FirstReceipt(JObject o) => ((JArray)o["events"]).OfType<JObject>()
            .First(e => ((string)e["kind"]).StartsWith("block-speech:", StringComparison.Ordinal));
        private static JObject FirstReply(JObject o, bool inspection = false) => ((JArray)o["ledger"]["replies"])
            .OfType<JObject>().First(r => ((string)r["replyKey"] == "feel-out") == inspection);

        [TestCase(false)] [TestCase(true)]
        public void CurrentLegacyAndFreshOpeningContractsRemainLegal(bool economy) => Accept(Payload(Fresh(economy)));

        [Test]
        public void EmptyOptionalHoHRemainsAnOpeningWithoutNormalization()
        {
            var s = Fresh(); s.hohId = "";
            var o = Payload(s); // Current production validation proves this historical spelling is legal.
            Assert.That(EpisodeEngine.IsFirstNight(s), Is.True);
            Accept(o);
            Assert.That((string)Project(o)["hohId"], Is.EqualTo(""), "Do not normalize empty optional roles to null.");
        }

        [TestCase("promise-safety")] [TestCase("hear")] [TestCase("turn-down")]
        public void ActualPitchInspectionAndEachAnswerKeepAllHistory(string answer)
        {
            var pending = Pending(); Accept(Payload(pending));
            var inspected = Apply(pending, EpisodeCommandKind.ReplyToHouseguest, pending.replyCards[0].id, "feel-out");
            Accept(Payload(inspected));
            var answered = Apply(inspected, EpisodeCommandKind.ReplyToHouseguest, pending.replyCards[0].id, answer);
            var o = Payload(answered); Accept(o);
            Assert.That(JToken.DeepEquals(Project(o)["ledger"], o["ledger"]), Is.True);
            Assert.That(JToken.DeepEquals(Project(o)["promises"], o["promises"]), Is.True);
        }

        [TestCase("emotional")] [TestCase("strategic")] [TestCase("deal")] [TestCase("pressure")] [TestCase("quiet")]
        public void ActualSpeechCommandsRetainTheirExactTypedReceipt(string approach)
        {
            var o = Payload(Delivered(approach)); Accept(o);
            Assert.That(JToken.DeepEquals(Project(o)["events"], o["events"]), Is.True);
            Assert.That(JToken.DeepEquals(Project(o)["evictionSpeeches"], o["evictionSpeeches"]), Is.True);
        }

        [Test]
        public void RealCampaignVoteLobbyDoesNotBroadenTheOlderFrozenContract()
        {
            var o = Payload(Campaign()); Accept(o);
            Assert.That(((JArray)o["lobbies"]).Count, Is.EqualTo(1));
            var old = (JObject)o.DeepClone(); old.Remove("economyRulesVersion"); old.Remove("moveInExtrasSpent"); old["schemaVersion"] = 22;
            string before = old.ToString(Formatting.None);
            var v22 = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV22", true);
            Assert.Throws<InvalidDataException>(() => Invoke(v22, "Validate", old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        [TestCase("pitch")] [TestCase("lobby")] [TestCase("speech")]
        public void ValidationProjectionChangesOnlyThePreciselyNamedExtension(string fixture)
        {
            var o = Payload(fixture == "pitch" ? Pending() : fixture == "lobby" ? Campaign() : Delivered());
            string before = o.ToString(Formatting.None);
            var expected = (JObject)o.DeepClone();
            expected.Remove("economyRulesVersion"); expected.Remove("moveInExtrasSpent"); expected["schemaVersion"] = 22;
            foreach (var r in ((JArray)expected["replyCards"]).OfType<JObject>().Where(r => (string)r["kind"] == "pitch").ToArray()) r.Remove();
            foreach (var r in ((JArray)expected["lobbies"]).OfType<JObject>().Where(r => (int)r["phase"] == 6 || (string)r["ask"] == "vote").ToArray()) r.Remove();
            var projected = Project(o);
            Assert.That(JToken.DeepEquals(projected, expected), Is.True, "Every RNG bit, counter, record, ordinary card and lobby remains unchanged.");
            Assert.That(o.ToString(Formatting.None), Is.EqualTo(before));
            projected["seed"] = 123;
            Assert.That(o.ToString(Formatting.None), Is.EqualTo(before), "The validation projection is independently owned.");
        }

        [Test]
        public void OrdinaryBoundedProseDoesNotAcquireAClosedVocabularyOrReceiptRules()
        {
            var s = Fresh(); string npc = s.Active.First(c => !c.isPlayer).id;
            s.ledger.replies.Add(new ReplyRow { week = 1, cardId = "ordinary", kind = "historical-other",
                fromId = npc, listenerId = "", replyKey = "unreserved-answer", promised = true, toThem = 3 });
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = 1, phase = s.phase,
                kind = "airing-backed", text = "Historical ordinary prose", audienceIds = new List<string> { npc, npc } });
            Accept(Payload(s));
        }

        [TestCase("missing-economy")] [TestCase("missing-debit")] [TestCase("unknown-root")] [TestCase("schema24")]
        [TestCase("string-economy")] [TestCase("fraction-economy")] [TestCase("null-economy")] [TestCase("future-economy")]
        [TestCase("negative-debit")] [TestCase("excess-debit")] [TestCase("fraction-debit")] [TestCase("opening-debit")]
        [TestCase("disabled-debit")] [TestCase("later-debit")] [TestCase("inactive-week-debit")] [TestCase("huge-week")]
        public void EconomyAndRootCorruptionCannotBeErasedByProjection(string defect)
        {
            var o = Payload(Fresh());
            switch (defect)
            {
                case "missing-economy": o.Remove("economyRulesVersion"); break;
                case "missing-debit": o.Remove("moveInExtrasSpent"); break;
                case "unknown-root": o["futureAuthority"] = new JArray(); break;
                case "schema24": o["schemaVersion"] = 24; break;
                case "string-economy": o["economyRulesVersion"] = "1"; break;
                case "fraction-economy": o["economyRulesVersion"] = 1.0; break;
                case "null-economy": o["economyRulesVersion"] = JValue.CreateNull(); break;
                case "future-economy": o["economyRulesVersion"] = 2; break;
                case "negative-debit": o["moveInExtrasSpent"] = -1; break;
                case "excess-debit": o["moveInExtrasSpent"] = 25; break;
                case "fraction-debit": o["moveInExtrasSpent"] = 0.5; break;
                case "opening-debit": o["moveInExtrasSpent"] = 1; break;
                case "disabled-debit": o["moveInExtrasSpent"] = 1; o["economyRulesVersion"] = 0; o["phase"] = 1; break;
                case "later-debit": o["moveInExtrasSpent"] = 1; o["week"] = 2; break;
                case "inactive-week-debit": o["moveInExtrasSpent"] = 1; o["phase"] = 1; o["weekRulesStartWeek"] = 2; break;
                case "huge-week": o["week"] = long.MaxValue; break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Reject(o);
        }

        [TestCase("missing")] [TestCase("extra")] [TestCase("null-row")] [TestCase("wrong-type")] [TestCase("unknown-kind")]
        [TestCase("duplicate-id")] [TestCase("duplicate-speaker")] [TestCase("cap")] [TestCase("dangling")]
        [TestCase("self-target")] [TestCase("player-target")] [TestCase("inactive-speaker")] [TestCase("wrong-phase")]
        [TestCase("nominees-set")] [TestCase("other-hoh")] [TestCase("economy-off")] [TestCase("agency-off")]
        [TestCase("strategy-off")] [TestCase("week-off")] [TestCase("answered-already")]
        public void RemovedPitchCardsAreStrictlyCheckedInTheirOriginalContext(string defect)
        {
            var o = Payload(Pending()); var cards = (JArray)o["replyCards"]; var card = (JObject)cards[0];
            switch (defect)
            {
                case "missing": card.Remove("aboutId"); break;
                case "extra": card["future"] = 0; break;
                case "null-row": cards[0] = JValue.CreateNull(); break;
                case "wrong-type": card["week"] = "1"; break;
                case "unknown-kind": card["kind"] = "future-pitch"; break;
                case "duplicate-id": cards.Add(card.DeepClone()); break;
                case "duplicate-speaker": var copy = (JObject)card.DeepClone(); copy["id"] = "other-card"; cards.Add(copy); break;
                case "cap": while (cards.Count < 25) cards.Add(card.DeepClone()); break;
                case "dangling": card["aboutId"] = "unknown"; break;
                case "self-target": card["aboutId"] = card["fromId"].DeepClone(); break;
                case "player-target": card["aboutId"] = o["playerId"].DeepClone(); break;
                case "inactive-speaker": ((JArray)o["contestants"]).OfType<JObject>().Single(p => (string)p["id"] == (string)card["fromId"])["status"] = 1; break;
                case "wrong-phase": o["phase"] = 0; break;
                case "nominees-set": ((JArray)o["nominees"]).Add(card["fromId"].DeepClone()); break;
                case "other-hoh": o["hohId"] = card["fromId"].DeepClone(); break;
                case "economy-off": o["economyRulesVersion"] = 0; break;
                case "agency-off": o["agencyRulesStartWeek"] = 0; break;
                case "strategy-off": o["strategyRulesStartWeek"] = 0; break;
                case "week-off": o["weekRulesStartWeek"] = 0; break;
                case "answered-already": ((JArray)o["ledger"]["replies"]).Add(JObject.FromObject(new ReplyRow { week = 1,
                    cardId = (string)card["id"], kind = "pitch", fromId = (string)card["fromId"], replyKey = "hear" }, Serializer())); break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Reject(o);
        }

        [TestCase("missing")] [TestCase("extra")] [TestCase("null-row")] [TestCase("wrong-type")] [TestCase("unknown-ask")]
        [TestCase("unknown-approach")] [TestCase("unknown-response")] [TestCase("wrong-phase")] [TestCase("wrong-week")]
        [TestCase("cap")] [TestCase("dangling")] [TestCase("player-decider")] [TestCase("nonfinite")] [TestCase("over-influence")]
        [TestCase("strategy-off")]
        public void RemovedLobbyRowsCannotSmuggleUncheckedData(string defect)
        {
            var o = Payload(Campaign()); var rows = (JArray)o["lobbies"]; var row = (JObject)rows[0];
            switch (defect)
            {
                case "missing": row.Remove("response"); break;
                case "extra": row["future"] = false; break;
                case "null-row": rows[0] = JValue.CreateNull(); break;
                case "wrong-type": row["phase"] = "6"; break;
                case "unknown-ask": row["ask"] = "future"; break;
                case "unknown-approach": row["approach"] = "future"; break;
                case "unknown-response": row["response"] = "future"; break;
                case "wrong-phase": row["phase"] = 7; break;
                case "wrong-week": row["week"] = 2; break;
                case "cap": while (rows.Count < 33) rows.Add(row.DeepClone()); break;
                case "dangling": row["subjectId"] = "unknown"; break;
                case "player-decider": row["deciderId"] = o["playerId"].DeepClone(); break;
                case "nonfinite": row["influence"] = double.NaN; break;
                case "over-influence": row["influence"] = 101; break;
                case "strategy-off": o["strategyRulesStartWeek"] = 0; break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Reject(o);
        }

        [TestCase("missing")] [TestCase("extra")] [TestCase("null-row")] [TestCase("promised")]
        [TestCase("unknown-key")] [TestCase("wrong-payoff")] [TestCase("inspection-impact")] [TestCase("duplicate-final")]
        [TestCase("duplicate-inspection")] [TestCase("player-speaker")] [TestCase("listener-self")] [TestCase("listener-player")]
        [TestCase("dangling")] [TestCase("economy-off")] [TestCase("before-boundary")] [TestCase("cap")]
        public void PitchLedgerHistoryIsCheckedAndNeverRemoved(string defect)
        {
            var o = Payload(Answered()); var row = FirstReply(o); var rows = (JArray)o["ledger"]["replies"];
            switch (defect)
            {
                case "missing": row.Remove("promised"); break;
                case "extra": row["future"] = 1; break;
                case "null-row": rows[rows.IndexOf(row)] = JValue.CreateNull(); break;
                case "promised": row["promised"] = true; break;
                case "unknown-key": row["replyKey"] = "future"; break;
                case "wrong-payoff": row["toThem"] = 8; break;
                case "inspection-impact": FirstReply(o, true)["toThem"] = 1; break;
                case "duplicate-final": rows.Add(row.DeepClone()); break;
                case "duplicate-inspection": rows.Add(FirstReply(o, true).DeepClone()); break;
                case "player-speaker": row["fromId"] = o["playerId"].DeepClone(); break;
                case "listener-self": row["listenerId"] = row["fromId"].DeepClone(); break;
                case "listener-player": row["listenerId"] = o["playerId"].DeepClone(); break;
                case "dangling": row["fromId"] = "unknown"; break;
                case "economy-off": o["economyRulesVersion"] = 0; break;
                case "before-boundary": o["weekRulesStartWeek"] = 2; break;
                case "cap": while (rows.Count < 513) rows.Add(row.DeepClone()); break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Reject(o);
        }

        [TestCase("missing")] [TestCase("extra")] [TestCase("null-row")] [TestCase("wrong-type")]
        [TestCase("unknown-approach")] [TestCase("empty-audience")] [TestCase("duplicate-audience")] [TestCase("dangling-audience")]
        [TestCase("missing-player")] [TestCase("audience-order")] [TestCase("wrong-text")] [TestCase("quiet-with-words")]
        [TestCase("duplicate-speaker")] [TestCase("duplicate-sequence")] [TestCase("cap")] [TestCase("future-week")]
        [TestCase("wrong-event-phase")] [TestCase("before-speeches")] [TestCase("campaign-stage")]
        [TestCase("economy-off")] [TestCase("levers-off")] [TestCase("speech-missing")] [TestCase("speech-extra-field")]
        [TestCase("speech-wrong-author")] [TestCase("speech-duplicate")]
        public void ReservedSpeechReceiptsKeepTheirLiteralContract(string defect)
        {
            var o = Payload(Delivered()); var row = FirstReceipt(o); var rows = (JArray)o["events"];
            var speeches = (JArray)o["evictionSpeeches"];
            switch (defect)
            {
                case "missing": row.Remove("text"); break;
                case "extra": row["future"] = 1; break;
                case "null-row": rows[rows.IndexOf(row)] = JValue.CreateNull(); break;
                case "wrong-type": row["phase"] = "7"; break;
                case "unknown-approach": row["kind"] = "block-speech:future"; break;
                case "empty-audience": row["audienceIds"] = new JArray(); break;
                case "duplicate-audience": ((JArray)row["audienceIds"]).Add(row["audienceIds"][0].DeepClone()); break;
                case "dangling-audience": row["audienceIds"][1] = "unknown"; break;
                case "missing-player": ((JArray)row["audienceIds"]).RemoveAt(0); break;
                case "audience-order": var first = row["audienceIds"][1].DeepClone(); row["audienceIds"][1] = row["audienceIds"][2].DeepClone(); row["audienceIds"][2] = first; break;
                case "wrong-text": row["text"] = "Different words"; break;
                case "quiet-with-words": row["kind"] = "block-speech:quiet"; break;
                case "duplicate-speaker": var copy = (JObject)row.DeepClone(); copy["sequence"] = (int)o["nextSequence"]; o["nextSequence"] = (int)o["nextSequence"] + 1; rows.Add(copy); break;
                case "duplicate-sequence": rows.Add(row.DeepClone()); break;
                case "cap": while (rows.Count < 257) rows.Add(row.DeepClone()); break;
                case "future-week": row["week"] = 2; break;
                case "wrong-event-phase": row["phase"] = 6; break;
                case "before-speeches": o["evictionStage"] = 0; break;
                case "campaign-stage": o["phase"] = 6; o["evictionStage"] = 0; break;
                case "economy-off": o["economyRulesVersion"] = 0; break;
                case "levers-off": o["leverRulesStartWeek"] = 0; break;
                case "speech-missing": speeches.Clear(); break;
                case "speech-extra-field": ((JObject)speeches[0])["future"] = 1; break;
                case "speech-wrong-author": speeches[0]["isPlayerAuthored"] = false; break;
                case "speech-duplicate": speeches.Add(speeches[0].DeepClone()); break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Reject(o);
        }

        [Test]
        public void ARealSeasonKeepsReceiptsThroughResultsSocialAndTheNextWeek()
        {
            var engine = new EpisodeEngine(Delivered()); bool results = false, social = false, nextWeek = false;
            for (int step = 0; step < 80; step++)
            {
                var s = engine.Snapshot; Accept(Payload(s));
                results |= s.phase == EpisodePhase.Eviction && s.evictionResolved;
                social |= s.phase == EpisodePhase.Social && s.evictionResolved;
                if (s.week == 2) { nextWeek = true; break; }
                var r = engine.Apply(EpisodeEngineTests.NextCommand(s));
                Assert.That(r.accepted, Is.True, r.reason);
            }
            Assert.That(results && social && nextWeek, Is.True, "Exercise the real lifecycle, not fabricated phase-only history.");
            Assert.That(engine.Snapshot.events.Any(e => e.kind.StartsWith("block-speech:", StringComparison.Ordinal) && e.week == 1), Is.True);
        }

        [Test]
        public void FullCurrentSeasonValidatesWithoutChangingAnyPersistedValue()
        {
            var engine = new EpisodeEngine(Fresh()); bool finished = false;
            for (int step = 0; step < 500; step++)
            {
                var s = engine.Snapshot; Accept(Payload(s));
                if (s.phase == EpisodePhase.Finished) { finished = true; break; }
                var r = engine.Apply(EpisodeEngineTests.NextCommand(s)); Assert.That(r.accepted, Is.True, r.reason);
            }
            Assert.That(finished, Is.True);
        }
    }
}
