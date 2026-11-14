using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The role moments' rule (ROLE-MOMENTS-PLAN R0, §4.2-4.3): story rules 11, <see cref="StoryRules.Roles"/>,
    /// defined and held to the validator and enrolled in every new season since R7, and <see cref="EpisodeEngine.RolesOn"/>,
    /// the one test every Roles path reads. Nothing here plays a moment. The frozen readers keep their own copy of
    /// the ladder and refuse the new milestone, and the story session's 10 below it, <see cref="StoryRules.Blame"/>.
    /// Pure simulation and JSON, so the Unity-free subset runs it as well as the editor; the schema-24 reader's
    /// refusal is FrozenEpisodeV24ContractTests' "roles-story" case, and the schema-29 reader's refusal of 10 is
    /// FrozenEpisodeV29IntegrationTests' StoryRulesTenAtTwentyNineIsRefusedInTheFrozenReadersWordsBeforeAnyLift.
    /// </summary>
    public sealed class RoleRulesTests
    {
        [Test]
        public void RolesIsClaimedAsElevenAndEveryNewSeasonPlaysIt()
        {
            Assert.That(StoryRules.Roles, Is.EqualTo(11), "Claimed as 11, with 10 kept for the story session's pariah fix.");
            Assert.That(StoryRules.Blame, Is.EqualTo(StoryRules.Roles - 1), "10 is the pariah fix, Blame, defined below Roles.");
            Assert.That(StoryRules.Latest, Is.EqualTo(StoryRules.Roles), "The bound is the last milestone this build defines.");
            // Story rules 11 is enrolled (ROLE-MOMENTS-PLAN R7; Blame's was STORY-RULES-10-PLAN §8): a season the game starts plays Roles.
            Assert.That(StoryRules.Current, Is.EqualTo(StoryRules.Roles), "A season the game starts moves only at an enrolment: R7's is made.");
            var shipped = Shipped();
            Assert.That(shipped.story.rulesVersion, Is.EqualTo(StoryRules.Roles));
            Assert.That(EpisodeEngine.RolesOn(shipped), Is.True, "A season the game starts today plays the role moments.");
            Assert.That(EpisodeEngine.RolesOn(null), Is.False);
        }

        /// <summary>
        /// The ladder is exact: off, then every milestone to Latest, with no gap. 10 was claimed before any code read it,
        /// and refused until then; it is Blame, the story session's pariah fix.
        /// </summary>
        [TestCase(-1, false)] [TestCase(0, true)] [TestCase(StoryRules.Spine, true)] [TestCase(StoryRules.Reach, true)]
        [TestCase(StoryRules.Threads, true)] [TestCase(StoryRules.Blame, true)] [TestCase(StoryRules.Roles, true)]
        [TestCase(StoryRules.Latest + 1, false)] [TestCase(int.MaxValue, false)]
        public void OnlyAMilestoneSomeCodeDefinesIsDefined(int version, bool defined) =>
            Assert.That(StoryRules.IsDefined(version), Is.EqualTo(defined));

        /// <summary>A live season is schema 30, so it may hold any defined milestone, Blame's 10 included; past Latest it is refused.</summary>
        [TestCase(StoryRules.Threads, true)] [TestCase(StoryRules.Roles, true)]
        [TestCase(StoryRules.Blame, true)] [TestCase(StoryRules.Latest + 1, false)]
        public void TheValidatorHoldsALiveSeasonToTheDefinedMilestones(int version, bool accepted)
        {
            var s = Shipped();
            s.story.rulesVersion = version;
            string before = Text(s);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.EqualTo(accepted), error);
            if (!accepted) Assert.That(error, Is.EqualTo("Unsupported story rules version."));
            Assert.That(Text(s), Is.EqualTo(before), "Validation repairs nothing.");
        }

        /// <summary>Every command a season at Roles takes is validated before it is installed, to the finish.</summary>
        [Test]
        public void ALiveSeasonAtRolesPlaysToItsFinishThroughThePublicEngine()
        {
            var engine = new EpisodeEngine(AtRoles());
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var at = engine.Snapshot;
                var result = engine.Apply(Next(at));
                Assert.That(result.accepted, Is.True, "week " + at.week + " " + at.phase + ": " + result.reason);
            }
            var end = engine.Snapshot;
            Assert.That(end.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(end.story.rulesVersion, Is.EqualTo(StoryRules.Roles), "Nothing moves a season's story rules.");
            Assert.That(EpisodeEngine.RolesOn(end), Is.True);
            Assert.That(EpisodeValidation.TryValidate(end, out var error), Is.True, error);
        }

        /// <summary>
        /// RolesOn wants the story at Roles this week and the strategy windows, the commitment rules and the levers
        /// the moments are built on. The economy and agency are the pitch board's own, through HoHPitches.Available.
        /// </summary>
        [TestCase("roles", true)] [TestCase("threads", false)] [TestCase("story-off", false)] [TestCase("story-next-week", false)]
        [TestCase("strategy-off", false)] [TestCase("strategy-next-week", false)] [TestCase("commitments-off", false)]
        [TestCase("commitments-next-week", false)] [TestCase("levers-off", false)] [TestCase("levers-next-week", false)]
        [TestCase("economy-off", true)] [TestCase("agency-off", true)] [TestCase("dialogue-on", true)]
        public void RolesOnWantsTheStoryAtRolesAndTheRulesTheMomentsAreBuiltOn(string boundary, bool on)
        {
            var s = AtRoles();
            switch (boundary)
            {
                case "roles": break;
                case "threads": s.story.rulesVersion = StoryRules.Threads; break;
                case "story-off": s.story.rulesStartWeek = 0; break;
                case "story-next-week": s.story.rulesStartWeek = s.week + 1; break;
                case "strategy-off": s.strategyRulesStartWeek = 0; break;
                case "strategy-next-week": s.strategyRulesStartWeek = s.week + 1; break;
                case "commitments-off": s.commitmentRulesStartWeek = 0; break;
                case "commitments-next-week": s.commitmentRulesStartWeek = s.week + 1; break;
                case "levers-off": s.leverRulesStartWeek = 0; break;
                case "levers-next-week": s.leverRulesStartWeek = s.week + 1; break;
                case "economy-off": s.economyRulesVersion = 0; break;
                case "agency-off": s.agencyRulesStartWeek = 0; break;
                case "dialogue-on": s.dialogueRulesVersion = 1; break;
                default: Assert.Fail("Unknown boundary " + boundary); break;
            }
            Assert.That(EpisodeEngine.RolesOn(s), Is.EqualTo(on), boundary);
        }

        /// <summary>PhaseMoment's milestone defaults to the grudge rules, so every web phase beat is the template it was.</summary>
        [TestCase("the-lobby")] [TestCase("tensions-rise")] [TestCase("veto-dilemma")] [TestCase("the-fallout")]
        [TestCase("final-pleas")] [TestCase("last-minute-campaign")] [TestCase("house-drama")]
        public void EveryWebPhaseBeatStillPlaysFromTheGrudgeRules(string id)
        {
            var arc = StoryCatalog.Find(id);
            Assert.That(arc, Is.Not.Null, id);
            Assert.That(arc.lane, Is.EqualTo(StoryLanes.Moment), id);
            Assert.That(arc.rulesVersion, Is.EqualTo(StoryRules.Grudges), id);
        }

        /// <summary>
        /// Each frozen reader past schema 24 keeps its own ladder, which ends at threads: a historical save can never
        /// hold Roles. The control is the same lawful witness at threads, accepted by the same reader; only the
        /// story version differs, and the refusal names it. Schema 26 and 27 reach the frozen 25 validator through
        /// their own projections, and schema 28 has its own read-only copy.
        /// </summary>
        [TestCase("FrozenEpisodeV25")] [TestCase("FrozenEpisodeV26")] [TestCase("FrozenEpisodeV27")] [TestCase("FrozenEpisodeV28")]
        public void EveryFrozenReaderRefusesAHistoricalSeasonAtRoles(string reader) => RefusedAt(reader, StoryRules.Roles);

        /// <summary>
        /// The same readers refuse Blame's 10, the pariah fix defined below Roles: their ladders end at threads, so a
        /// historical save can never hold it either, and a season at 10 is a schema-30 season with no older form. The
        /// same control at threads, and the same refusal in the readers' own words.
        /// </summary>
        [TestCase("FrozenEpisodeV25")] [TestCase("FrozenEpisodeV26")] [TestCase("FrozenEpisodeV27")] [TestCase("FrozenEpisodeV28")]
        public void EveryFrozenReaderRefusesAHistoricalSeasonAtBlame(string reader) => RefusedAt(reader, StoryRules.Blame);

        // ------------------------------------------------------------------ helpers

        /// <summary>The lawful witness at threads is the reader's to accept; at <paramref name="version"/> it is refused, unrepaired.</summary>
        private static void RefusedAt(string reader, int version)
        {
            var payload = Literal(reader);
            string control = Text(payload);
            Assert.DoesNotThrow(() => Contract(reader, payload), "The control at threads is the reader's to accept.");
            Assert.That(Text(payload), Is.EqualTo(control));
            Assert.That((int)payload["story"]["rulesVersion"], Is.EqualTo(StoryRules.Threads));

            payload["story"]["rulesVersion"] = version;
            string before = Text(payload);
            var refusal = Assert.Throws<InvalidDataException>(() => Contract(reader, payload));
            Assert.That(Messages(refusal), Has.Some.EqualTo("Unsupported story rules version."), string.Join(" | ", Messages(refusal)));
            Assert.That(Text(payload), Is.EqualTo(before), "A refused historical payload is preserved, not repaired.");
        }

        private static EpisodeState Shipped()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1111u);
            ShippedRules.ApplyFresh(s);
            return s;
        }

        /// <summary>A shipped season at Roles: opted in by name, as tests did until the enrolment (ROLE-MOMENTS-PLAN §4.2), and what ApplyFresh gives since R7.</summary>
        private static EpisodeState AtRoles()
        {
            var s = Shipped();
            s.story.rulesVersion = StoryRules.Roles;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }

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

        /// <summary>A lawful witness's field view at threads, taken to the reader's literal schema by the test-owned inverses.</summary>
        private static JObject Literal(string reader)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            Assert.That(state.story.rulesVersion, Is.EqualTo(StoryRules.Threads));
            var literal28 = LegacyDigestSchema27TestStates.WithoutGetters(LegacyDigestSchema27TestStates.Trace28(state));
            if (reader == "FrozenEpisodeV28") return literal28;
            var literal27 = LegacyDigestSchema27TestStates.Neutral27(literal28);
            if (reader == "FrozenEpisodeV27") return literal27;
            var literal26 = LegacyDigestSchema27TestStates.Neutral26(literal27);
            if (reader == "FrozenEpisodeV26") return literal26;
            Assert.That(reader, Is.EqualTo("FrozenEpisodeV25"));
            return Neutral25(literal26);
        }

        /// <summary>FrozenEpisodeV26's own step down: its empty archive and present-null Vote extensions removed, the header 25.</summary>
        private static JObject Neutral25(JObject literal26)
        {
            var result = (JObject)literal26.DeepClone();
            Assert.That(Text(result["unifiedVoteReveals"]), Is.EqualTo("[]"));
            result.Remove("unifiedVoteReveals");
            foreach (JObject row in (JArray)result["unifiedCommitments"])
            {
                Assert.That(row["targetId"].Type == JTokenType.Null && row["subtype"].Type == JTokenType.Null, Is.True);
                row.Remove("targetId"); row.Remove("subtype");
            }
            result["schemaVersion"] = 25;
            return result;
        }

        private static void Contract(string name, JObject payload)
        {
#if UNITY_5_3_OR_NEWER
            var historicalAssembly = typeof(Gamesim.Persistence.EpisodeSaveStore).Assembly;
#else
            var historicalAssembly = typeof(EpisodeState).Assembly;
#endif
            var contract = historicalAssembly.GetType("Gamesim.Persistence." + name, true);
            var validate = contract.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(JObject) }, null);
            Assert.That(validate, Is.Not.Null, name); Assert.That(validate.IsAssembly, Is.True, name);
            try { validate.Invoke(null, new object[] { payload }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static List<string> Messages(Exception error)
        {
            var messages = new List<string>();
            for (; error != null; error = error.InnerException) messages.Add(error.Message);
            return messages;
        }

        private static string Text(EpisodeState state) => JObject.FromObject(state).ToString(Formatting.None);
        private static string Text(JToken token) => token.ToString(Formatting.None);
    }
}
