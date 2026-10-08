using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The verification-only stress house (<see cref="SeasonBuilder.CreateVerificationStressHouse"/>):
    /// thirteen to sixteen houseguests from both rosters, for the standalone profile's sixteen-person
    /// stress run and nothing else. A roster seats twelve and validation stores sixteen; before this a
    /// sixteen-person profile request was silently measured at twelve.
    ///
    /// <para>The claim that matters is that the simulation plays such a house: a sixteen-person season
    /// under the rules a director season starts with validates and reaches its finale through the
    /// public engine. The capacities a sixteen-person season leans on - the house's 128 facts, its
    /// event list, the save's eight MiB - are measured on the way and printed.</para>
    /// </summary>
    public sealed class StressHouseTests
    {
        [Test]
        public void TheStressHouseStartsAboveEveryRosterAndStopsAtWhatValidationStores()
        {
            Assert.That(SeasonBuilder.LargestRosterHouse, Is.EqualTo(12));
            Assert.That(SeasonBuilder.LargestStressHouse, Is.EqualTo(EpisodeValidation.MaximumCast).And.EqualTo(16));
            foreach (CastTemplates.Roster roster in Enum.GetValues(typeof(CastTemplates.Roster)))
                Assert.That(SeasonBuilder.Create(new SeasonBuilder.Choice { Roster = roster, HouseSize = 16 }, 1u).contestants,
                    Has.Count.EqualTo(12), "A playable season still never pads one roster with the other.");
        }

        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        public void TheStressHouseSeatsBothRostersWithNobodyTwice(int size)
        {
            const uint seed = 4242u;
            var s = SeasonBuilder.CreateVerificationStressHouse(size, seed);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            Assert.That(s.contestants, Has.Count.EqualTo(size));
            Assert.That(s.contestants.Select(c => c.id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(size), "Unique ids.");
            Assert.That(s.contestants.Select(c => c.name).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(size), "Unique names.");
            var you = s.Find(s.playerId);
            Assert.That(you.isPlayer, Is.True);
            Assert.That(you.name, Is.EqualTo("You"), "The player is the unaffiliated newcomer, as on a default choice.");
            Assert.That(s.contestants.Count(c => c.isPlayer), Is.EqualTo(1));
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            Assert.That(npcs.All(c => CastTemplates.Find(c.sourceTemplateId) != null && c.id == c.sourceTemplateId), Is.True, "Every houseguest is a template.");
            var rosters = npcs.Select(c => CastTemplates.Find(c.sourceTemplateId).Roster).Distinct().ToList();
            Assert.That(rosters, Is.EquivalentTo(new[] { CastTemplates.Roster.Regular, CastTemplates.Roster.AllStars }), "Both rosters are in the house.");
            Assert.That(s.relationships, Has.Count.EqualTo(size * (size - 1)), "Every ordered pair has its relationship row.");
            Assert.That(s.randomState, Is.EqualTo(seed), "Casting draws nothing from the season's generator.");
            Assert.That(s.seed, Is.EqualTo(seed));
            Assert.That(s.events.Single(e => e.kind == "arrival").audienceIds, Has.Count.EqualTo(size));
        }

        [Test]
        public void TheStressCastTakesTheRostersInTurnInTableOrder()
        {
            var regular = CastTemplates.In(CastTemplates.Roster.Regular).Select(t => t.Id).ToList();
            var allStars = CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id).ToList();
            var expected = new List<string>();
            for (int i = 0; expected.Count < 15; i++) { expected.Add(regular[i]); expected.Add(allStars[i]); }
            var house = SeasonBuilder.CreateVerificationStressHouse(16, 7u);
            Assert.That(house.contestants.Where(c => !c.isPlayer).Select(c => c.id), Is.EqualTo(expected.Take(15)));
            Assert.That(house.events.Single(e => e.kind == "arrival").text, Does.StartWith("Sixteen houseguests, one new game."));
        }

        [Test]
        public void TheSameSizeAndSeedBuildTheSameHouse()
        {
            var first = SeasonBuilder.CreateVerificationStressHouse(16, 99u);
            var second = SeasonBuilder.CreateVerificationStressHouse(16, 99u);
            Assert.That(Json(first), Is.EqualTo(Json(second)));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(3)]
        [TestCase(12)]
        [TestCase(17)]
        public void ASizeARosterSeatsOrValidationRefusesIsNoStressHouse(int size)
            => Assert.Throws<ArgumentOutOfRangeException>(() => SeasonBuilder.CreateVerificationStressHouse(size, 1u));

        // ------------------------------------------------------------------ it plays

        /// <summary>
        /// The sixteen-person house with every rule a season the director starts has (EpisodeDirector.StartSeason
        /// at 2ea986df): competitions, have-nots and strategy from week one, the story, the read, the levers, the
        /// week, the economy, NPC agency, the finale, the commitment rules, the leak rules and the prospective
        /// unified versions.
        /// A hand copy, held to the director's by <see cref="TheStressSeasonSwitchesOnEveryRuleADirectorSeasonDoes"/>:
        /// a rule the director's start gains fails that test until it is added here.
        /// </summary>
        private static EpisodeState DirectorStressSeason(uint seed)
        {
            var s = SeasonBuilder.CreateVerificationStressHouse(16, seed);
            s.competitionRulesVersion = CompetitionRules.Current;
            s.haveNotRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableFinale(s); EpisodeEngine.EnableCommitments(s);
            EpisodeEngine.EnableAllianceLeaks(s);
            s.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            s.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
            return s;
        }

        /// <summary>
        /// Two players: one who only does what each phase asks, and a busy one who spends every free action -
        /// answering the story's beats, forming pacts with whoever is warm to them, talking to whoever is coldest -
        /// which is what fills the house's facts and the save.
        /// </summary>
        [TestCase(1601u, false)]
        [TestCase(1601u, true)]
#if !UNITY_5_3_OR_NEWER
        // Three more seeds in Tools/SimulationTests only: each season is seconds there and several times that under Unity's Mono.
        [TestCase(1602u, false)]
        [TestCase(1603u, false)]
        [TestCase(1602u, true)]
#endif
        public void ASixteenPersonStressSeasonUnderTheDirectorsRulesPlaysToTheFinale(uint seed, bool busy)
        {
            var fresh = DirectorStressSeason(seed);
            Assert.That(EpisodeValidation.TryValidate(fresh, out var freshError), Is.True, freshError);
            var engine = new EpisodeEngine(fresh);
            int commands = 0, own = 0, peakFacts = 0, peakHouseEvents = 0, peakSaveBytes = 0, weeks = 0, logFullFromWeek = 0, playerDealsClosedFromWeek = 0;
            int peakMemories = 0, peakAlliances = 0, peakPromises = 0, peakDeals = 0, peakStorylines = 0;
            while (engine.Snapshot.phase != EpisodePhase.Finished && commands < 8000)
            {
                var s = engine.Snapshot;
                var mine = busy ? Busy(s) : null;
                var command = mine ?? Next(s);
                var result = engine.Apply(command);
                if (mine != null && !result.accepted) { command = Next(s); result = engine.Apply(command); }
                else if (mine != null) own++;
                Assert.That(result.accepted, Is.True, "seed " + seed + " week " + s.week + " " + s.phase + " " + command.kind + ": " + result.reason);
                commands++;
                var after = engine.Snapshot;
                peakFacts = Math.Max(peakFacts, after.story?.facts?.Count ?? 0);
                peakHouseEvents = Math.Max(peakHouseEvents, after.houseEvents?.Count ?? 0);
                peakMemories = Math.Max(peakMemories, after.memories.Count);
                peakAlliances = Math.Max(peakAlliances, after.alliances.Count);
                peakPromises = Math.Max(peakPromises, after.promises.Count);
                peakDeals = Math.Max(peakDeals, after.deals.Count);
                peakStorylines = Math.Max(peakStorylines, after.storylines?.Count ?? 0);
                if (logFullFromWeek == 0 && after.events.Count >= 256) logFullFromWeek = after.week;
                if (playerDealsClosedFromWeek == 0 && PlayerDealsAtTheCeiling(after)) playerDealsClosedFromWeek = after.week;
                weeks = Math.Max(weeks, after.week);
                if (after.week != s.week || after.phase == EpisodePhase.Finished)
                {
                    Assert.That(EpisodeValidation.TryValidate(after, out var weekError), Is.True, "seed " + seed + " week " + after.week + ": " + weekError);
                    peakSaveBytes = Math.Max(peakSaveBytes, SaveBytes(after));
                }
            }
            var finished = engine.Snapshot;
            Assert.That(finished.phase, Is.EqualTo(EpisodePhase.Finished), "seed " + seed + " did not finish in " + commands + " commands.");
            Assert.That(finished.Find(finished.winnerId), Is.Not.Null, "A winner.");
            Assert.That(EpisodeValidation.TryValidate(finished, out var error), Is.True, error);
            int jurors = finished.contestants.Count(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted);
            Assert.That(jurors, Is.EqualTo(14), "Fourteen leave; the final two stay.");
            Assert.That(peakFacts, Is.LessThanOrEqualTo(Knowledge.Ceiling));
            Assert.That(peakSaveBytes, Is.LessThan(8 * 1024 * 1024), "The save store's eight MiB limit.");
            if (busy) Assert.That(own, Is.GreaterThan(20), "The busy player acted.");
            TestContext.WriteLine("Stress house seed " + seed + (busy ? " (busy, " + own + " own actions)" : " (passive)") + ": " + commands + " commands, " + weeks + " weeks, winner "
                + finished.Find(finished.winnerId).name + ", jurors " + jurors + "; peaks: story facts " + peakFacts + " of " + Knowledge.Ceiling
                + ", house events " + peakHouseEvents + " of " + HouseEvents.Ceiling + ", memories " + peakMemories + " of " + (30 * 16)
                + ", alliances " + peakAlliances + " of 100, promises " + peakPromises + " of 200, deals " + peakDeals + " of 200, storylines "
                + peakStorylines + " of 100; the 256-event log full from week " + logFullFromWeek + "; the house's deals at the player's ceiling of "
                + PlayerDeals.PlayerDealCeiling + " from week " + playerDealsClosedFromWeek + "; largest week-end save "
                + (peakSaveBytes / 1024) + " KiB of 8192 KiB.");
        }

        [Test]
        public void ASixteenPersonStressSeasonUnderTheBaseRulesPlaysToAWinner()
        {
            var engine = new EpisodeEngine(SeasonBuilder.CreateVerificationStressHouse(16, 808u));
            int guard = 0;
            while (engine.Snapshot.phase != EpisodePhase.Finished && guard++ < 2000)
            {
                var command = EpisodeEngineTests.NextCommand(engine.Snapshot);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, command.expectedPhase + ", " + command.kind + ": " + result.reason);
            }
            var finished = engine.Snapshot;
            Assert.That(finished.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(finished.Find(finished.winnerId), Is.Not.Null);
            Assert.That(EpisodeValidation.TryValidate(finished, out var error), Is.True, error);
        }

        /// <summary>
        /// Whether the deal table refuses the player for its ceiling: asked of <see cref="PlayerDeals.CanPropose"/>
        /// itself, so the diagnostic counts whatever the gate counts, however that count is kept.
        /// </summary>
        private static bool PlayerDealsAtTheCeiling(EpisodeState s)
        {
            var someone = s.Active.FirstOrDefault(c => !c.isPlayer);
            return someone != null && !PlayerDeals.CanPropose(s, someone.id, DealKind.InformationSharing, null, out string reason)
                && reason == Negotiation.TooManyArrangements;
        }

        /// <summary>The busy player's next free action, or null when the walk should take the phase's own step.</summary>
        private static EpisodeCommand Busy(EpisodeState s)
        {
            if (s.pendingDiary != null || s.Find(s.playerId).status != ContestantStatus.Active) return null;
            bool actionsLeft = EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);
            var item = HouseEvents.Pending(s);
            if (item != null && item.IsStory)
            {
                var choice = item.choices.FirstOrDefault(c => !c.lapse && !c.locked && !c.pickPerson && !c.conduct && (!c.costsAction || actionsLeft))
                    ?? item.choices.FirstOrDefault(c => c.lapse);
                if (choice != null) return Act(s, EpisodeCommandKind.ProgressStoryline, item.id, choice.optionId);
            }
            if ((s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign) || !actionsLeft) return null;
            var warm = s.Active.Where(c => !c.isPlayer && !s.Allied(s.playerId, c.id))
                .OrderByDescending(c => s.Score(c.id, s.playerId)).ThenBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
            if (warm != null && s.Score(warm.id, s.playerId) >= 8 && s.alliances.Count(a => a.active && a.members.Contains(s.playerId)) < 3)
                return Act(s, EpisodeCommandKind.FormAlliance, warm.id);
            var cold = s.Active.Where(c => !c.isPlayer).OrderBy(c => s.Score(c.id, s.playerId)).ThenBy(c => c.id, StringComparer.Ordinal).First();
            return Act(s, EpisodeCommandKind.Talk, cold.id);
        }

        private static EpisodeCommand Act(EpisodeState s, EpisodeCommandKind kind, string target, string second = null)
        {
            var command = EpisodeEngineTests.Command(s, kind);
            command.id = "stress-" + s.revision + "-" + kind;
            command.targetId = target; command.secondTargetId = second;
            return command;
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

        // ------------------------------------------------------------------ nothing else reaches it

        /// <summary>
        /// The stress house is reachable from the verification profile alone: the builder is called by the
        /// director's internal stress start only, that start by PortVerification only, and the flag's text
        /// lives in the one rule that admits it. The cast screen, the creator, the importer and every other
        /// runtime path name none of them.
        /// </summary>
        [Test]
        public void OnlyTheVerificationProfileCanBuildTheStressHouse()
        {
            string root = SourceRoot();
            var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/Assets/Gamesim/Tests/")).ToList();
            Assert.That(sources.Count, Is.GreaterThan(100), "The production sources were found.");
            List<string> Naming(string text) => sources.Where(path => File.ReadAllText(path).Contains(text))
                .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToList();
            Assert.That(Naming("CreateVerificationStressHouse("), Is.EqualTo(new[] { "EpisodeDirector.Season.cs", "SeasonBuilder.cs" }));
            Assert.That(Naming("StartVerificationStressSeason("), Is.EqualTo(new[] { "EpisodeDirector.Season.cs", "PortVerification.cs" }));
            Assert.That(Naming("\"--gamesim-stress-roster\""), Is.EqualTo(new[] { "VerificationPerformance.cs" }));
            Assert.That(Naming("StressRosterArgument"), Is.EqualTo(new[] { "PortVerification.cs", "VerificationPerformance.cs" }));
        }

        /// <summary>
        /// The stress seasons claim the director's rules, and <see cref="DirectorStressSeason"/> is a hand copy of
        /// them: every <c>EpisodeEngine.Enable*(fresh)</c> and every rule field the director's StartSeason sets on a
        /// fresh season (its session id aside) must appear there too. A rule the director gains - a later wave's
        /// enable line - fails here until the copy has it, rather than leaving the stress seasons on yesterday's rules.
        /// </summary>
        [Test]
        public void TheStressSeasonSwitchesOnEveryRuleADirectorSeasonDoes()
        {
            string root = SourceRoot();
            string director = File.ReadAllText(Path.Combine(root, "Runtime", "Episode", "EpisodeDirector.Season.cs"));
            int start = director.IndexOf("Func<uint, EpisodeState> build)", StringComparison.Ordinal);
            int end = start < 0 ? -1 : director.IndexOf("nextStore.Save(fresh)", start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The director's StartSeason body was found.");
            Assert.That(end, Is.GreaterThan(start), "...up to where it stages the season.");
            string body = director.Substring(start, end - start);
            var rules = System.Text.RegularExpressions.Regex.Matches(body,
                    @"EpisodeEngine\.Enable\w+\(fresh\);|fresh\.(?!sessionId\b)\w+\s*=\s*[^;]+;")
                .Cast<System.Text.RegularExpressions.Match>().Select(match => Normalise(match.Value.Replace("(fresh)", "(s)").Replace("fresh.", "s."))).ToList();
            Assert.That(rules.Count(rule => rule.StartsWith("EpisodeEngine.Enable", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(8), "The director's enable lines were read.");

            string tests = File.ReadAllText(Path.Combine(root, "Tests", "EditMode", "StressHouseTests.cs"));
            int copyStart = tests.IndexOf("private static EpisodeState DirectorStressSeason(uint seed)", StringComparison.Ordinal);
            int copyEnd = copyStart < 0 ? -1 : tests.IndexOf("return s;", copyStart, StringComparison.Ordinal);
            Assert.That(copyEnd, Is.GreaterThan(copyStart).And.GreaterThan(0), "The stress season's rules were found.");
            string copy = Normalise(tests.Substring(copyStart, copyEnd - copyStart));
            var missing = rules.Where(rule => !copy.Contains(rule)).ToList();
            Assert.That(missing, Is.Empty, "DirectorStressSeason lacks the director's: " + string.Join(" ", missing));
        }

        private static string Normalise(string code) => System.Text.RegularExpressions.Regex.Replace(code, @"\s+", "");

        /// <summary>Assets/Gamesim, found upward from the working directory (the Unity project) or the test binary (Tools/SimulationTests).</summary>
        private static string SourceRoot()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // An assembly loaded from bytes has no location, and Path.GetDirectoryName("") throws on Mono.
            string binary = typeof(StressHouseTests).Assembly.Location;
            var starts = new List<string> { Directory.GetCurrentDirectory() };
            if (!string.IsNullOrEmpty(binary)) starts.Add(Path.GetDirectoryName(binary));
            foreach (string start in starts)
            {
                string at = start;
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
                {
                    string candidate = Path.Combine(at, "Assets", "Gamesim");
                    if (File.Exists(Path.Combine(candidate, "Simulation", "SeasonBuilder.cs"))) { found.Add(Path.GetFullPath(candidate)); break; }
                    string next = Path.GetDirectoryName(at);
                    if (next == at) break;
                    at = next;
                }
            }
            Assert.That(found, Has.Count.EqualTo(1), "Exactly one Assets/Gamesim source tree above the test: " + string.Join(", ", found));
            return found.Single();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The season as the save store writes it: public fields, indented (EpisodeSaveStore's SaveJson). The envelope adds a few hundred bytes.</summary>
        private static int SaveBytes(EpisodeState state)
            => Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(state, Formatting.Indented,
                new JsonSerializerSettings { ContractResolver = new PublicFields() }));

        private static string Json(EpisodeState state)
            => JsonConvert.SerializeObject(state, new JsonSerializerSettings { ContractResolver = new PublicFields() });

        private sealed class PublicFields : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
                => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
        }
    }
}
