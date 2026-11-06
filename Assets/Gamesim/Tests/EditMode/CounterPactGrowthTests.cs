using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Alumni rules 5, <see cref="AlumniRules.GrowingPacts"/> (ALL-STAR-HISTORIES-PLAN §14.L, slice S3b): the owner's answer of
    /// 2026-10-29, "Yes, let outsider pacts grow past two". While a known pair of past games stands, a pact the houseguests
    /// formed themselves takes an outsider in (<see cref="NpcAlliances.TryJoin"/>): one a week, never in its founding week,
    /// while its fact is private to its members, while nobody in it stands in a live pair, and up to two plus the live pairs,
    /// never past four in the house (<see cref="KnownBlocs.CounterPactBound"/>). The recruit wants in with a sponsor, every
    /// member and the recruit are at −5 or more both ways with no grudge, and the join writes the record a birth writes and
    /// draws nothing. While private a grown pact sheds a joiner who sours; once known it ends whole; its members are never
    /// one another's common threat; and an invitation never takes a houseguests' four past four. Pacts are still born as
    /// pairs, the cap still counts pacts, and each houseguest is still in one at most.
    ///
    /// <para>Fixtures in the NPC-agency style, with no season walk: the shipped All-Stars season held at the value each test
    /// names, real pairs formed by <see cref="NpcAlliances.TryPropose"/> in an earlier week, scores, wins and facts written
    /// in, read through the public readers and, for an act, one beat of the all-week ladder at the recruit's pact window.
    /// Only public members and the test helpers of this assembly, so the file compiles in Unity's EditMode assembly and in
    /// the dotnet subset; the invitation is the engine's private writer, reached by reflection as
    /// GrowAndManageAlliancesTests reaches it.</para>
    /// </summary>
    public sealed class CounterPactGrowthTests
    {
        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Danielle = "danielle-reyes", Vanessa = "vanessa-rousso", Tyler = "tyler-crispen", Chelsie = "chelsie-baham",
            Rachel = "rachel-reilly", Xavier = "xavier-prather";

        private const int Five = AlumniRules.GrowingPacts, Four = AlumniRules.CounterBlocs;
        private const double Exact = 1e-9;

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The shipped All-Stars season held at <paramref name="value"/>, always named (§14.L.I): agency, the story's bonds,
        /// the read rules and the commitment rules run, as every rule here needs.
        /// </summary>
        private static EpisodeState Season(int size, uint seed, int value, string player = null)
        {
            var s = AlumniHistoriesTests.AllStars(size, seed, player, value);
            Assert.That(EpisodeEngine.AgencyOn(s) && EpisodeEngine.StoryAt(s, StoryRules.Bonds) && EpisodeEngine.ReadRulesOn(s)
                        && EpisodeEngine.CommitmentRulesOn(s), Is.True, "Agency, the bonds, the read rules and the commitment rules run.");
            return s;
        }

        private static string On(EpisodeState s, string templateId) => AlumniHistoriesTests.On(s, templateId);

        private static EpisodeState Held(EpisodeState s, int value)
        {
            var held = s.Clone();
            held.alumniRulesVersion = value;
            return held;
        }

        private static void Set(EpisodeState s, string from, string to, double score) => AllWeekCadenceTests.Score(s, from, to, score);

        private static void Both(EpisodeState s, string a, string b, double score)
        {
            Set(s, a, b, score);
            Set(s, b, a, score);
        }

        /// <summary>
        /// Every view in the house at nought and every houseguest without traits, so a desire is the relationship's 0.4,
        /// the strategic value, trust and the counter-bloc term alone, and nobody is warm to anybody until a fixture says so.
        /// </summary>
        private static void Plain(EpisodeState s)
        {
            foreach (var a in s.contestants)
            {
                a.traits = new List<string>();
                foreach (var b in s.contestants.Where(c => c.id != a.id)) Set(s, a.id, b.id, 0);
            }
        }

        /// <summary>
        /// A real pact of two, as the weekly pass forms one (<see cref="NpcAlliances.TryPropose"/>): the two warm to each
        /// other and nobody else warm to them. A houseguests' own pact, its fact private to the two and from this week.
        /// </summary>
        private static AllianceState Pair(EpisodeState s, string a, string b)
        {
            Both(s, a, b, 90);
            Assert.That(NpcAlliances.TryPropose(s, a, new HashSet<string>(StringComparer.Ordinal)), Is.True, "The pair forms.");
            var pact = s.alliances[s.alliances.Count - 1];
            Assert.That(pact.members, Is.EqualTo(new[] { a, b }));
            Assert.That(NpcAlliances.HouseguestsOwn(pact), Is.True);
            var fact = Fact(s, pact);
            Assert.That(fact, Is.Not.Null);
            Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Private));
            Assert.That(fact.week, Is.EqualTo(s.week));
            Assert.That(fact.knowers, Is.EquivalentTo(new[] { a, b }));
            return pact;
        }

        private static HouseFactState Fact(EpisodeState s, AllianceState pact) => Knowledge.Of(s, FactKinds.Alliance, pact.id);

        private static AllianceState Find(EpisodeState s, AllianceState pact) => s.alliances.Single(a => a.id == pact.id);

        /// <summary>The recruit warm to the sponsor (desire past 25 with no traits) and the sponsor at 30 toward them.</summary>
        private static void WantsIn(EpisodeState s, string recruitId, string sponsorId)
        {
            Set(s, recruitId, sponsorId, 60);
            Set(s, sponsorId, recruitId, 30);
        }

        private static bool Joins(EpisodeState s, string recruitId) => Joins(s, recruitId, out _);

        private static bool Joins(EpisodeState s, string recruitId, out string sponsorId) =>
            NpcAlliances.TryJoin(s, recruitId, new HashSet<string>(StringComparer.Ordinal), out sponsorId);

        /// <summary>A copy that tries the join, for a refusal: false, and nothing written.</summary>
        private static bool JoinsACopy(EpisodeState s, string recruitId)
        {
            var copy = s.Clone();
            string before = AlumniHistoriesTests.Json(copy);
            bool joined = Joins(copy, recruitId, out string sponsor);
            if (!joined)
            {
                Assert.That(sponsor, Is.Null);
                Assert.That(AlumniHistoriesTests.Json(copy), Is.EqualTo(before), "A refusal writes nothing.");
            }
            return joined;
        }

        /// <summary>A member written onto a pact as a join writes it, without the ledger: on the end, and a knower of its fact.</summary>
        private static void HandAdd(EpisodeState s, AllianceState pact, string id)
        {
            pact.members.Add(id);
            Knowledge.AddKnower(s, Fact(s, pact), id);
        }

        /// <summary>From this week on, the first week in which every one of these houseguests first beats after the Head of Household (<see cref="EpisodeEngine.PactWindow"/>).</summary>
        private static void UntilFirstBeatAfterHoH(EpisodeState s, params string[] ids)
        {
            for (int i = 0; i < 2 * Windows.Count && ids.Any(id => EpisodeEngine.PactWindow(s, id) != Windows.AfterHoH); i++) s.week++;
            Assert.That(ids.All(id => EpisodeEngine.PactWindow(s, id) == Windows.AfterHoH), Is.True, "A week in which they all first beat after the Head of Household.");
        }

        /// <summary>
        /// The season a week or more on, in the next week in which every one of these houseguests first beats after the
        /// Head of Household, in that window: its block is not yet set, so no known pair splits in public. A copy, as
        /// <see cref="AllWeekCadenceTests.InWindow"/> makes.
        /// </summary>
        private static EpisodeState NextWeek(EpisodeState s, params string[] beaters)
        {
            var later = s.Clone();
            later.week++;
            UntilFirstBeatAfterHoH(later, beaters);
            return AllWeekCadenceTests.InWindow(later, Windows.AfterHoH);
        }

        /// <summary>One beat of the all-week ladder, kept as the engine keeps it: this week, this window, an id of its own.</summary>
        private static NpcActState Beat(EpisodeState s, string npcId, string key)
        {
            var act = NpcSocialActions.Beat(s, npcId, StoryRandom.Stream(s, "test:grow:" + key), false, false);
            if (act != null)
            {
                act.id = s.week + "-" + EpisodeEngine.Window(s) + "-grow-" + key;
                act.week = s.week;
                act.window = EpisodeEngine.Window(s);
                s.npcSocial.acts.Add(act);
            }
            return act;
        }

        /// <summary>This week's pact acts, on either side, as the beat reads them.</summary>
        private static HashSet<string> PactSidesThisWeek(EpisodeState s) =>
            new HashSet<string>(s.npcSocial.acts.Where(a => a != null && a.week == s.week && a.kind == NpcActKinds.Pact)
                .SelectMany(a => new[] { a.actorId, a.partnerId }).Where(id => id != null), StringComparer.Ordinal);

        private static void AtMostOnePactActEach(EpisodeState s)
        {
            var acts = s.npcSocial.acts.Where(a => a != null && a.week == s.week && a.kind == NpcActKinds.Pact).ToList();
            foreach (var who in s.contestants)
                Assert.That(acts.Count(a => a.actorId == who.id || a.partnerId == who.id), Is.LessThanOrEqualTo(1),
                    who.id + ": at most one pact act a week, either side of it.");
        }

        /// <summary>The Hitmen's eight-house: Dan and Janelle paired in week 1 by the weekly rule, the week moved on, and Will wanting in through Janelle.</summary>
        private static (EpisodeState s, AllianceState pact, string dan, string janelle, string will) Hitmen(uint seed, int value)
        {
            var s = Season(8, seed, value);
            Plain(s);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will);
            var pact = Pair(s, dan, janelle);
            s.week++;
            WantsIn(s, will, janelle);
            return (s, pact, dan, janelle, will);
        }

        /// <summary>The same in the twelve-house, the Hitmen and the Committee both live.</summary>
        private static (EpisodeState s, AllianceState pact, string dan, string janelle, string will) Twelve(uint seed, int value)
        {
            var s = Season(12, seed, value);
            Plain(s);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will);
            var pact = Pair(s, dan, janelle);
            s.week++;
            WantsIn(s, will, janelle);
            return (s, pact, dan, janelle, will);
        }

        /// <summary>A public split at an earlier week's ceremony: <paramref name="hohId"/>, Head of Household, put <paramref name="upId"/> up, and <paramref name="evicteeId"/> went home.</summary>
        private static void PutUp(EpisodeState s, string hohId, string upId, string evicteeId)
        {
            s.ledger.power.Add(new PowerRow
            {
                week = s.week, hohId = hohId, nominees = new List<string> { upId, evicteeId }, tally = new List<int> { 1, 5 }, evicteeId = evicteeId,
            });
            s.Find(evicteeId).status = ContestantStatus.Evicted;
            s.week++;
        }

        private static (string kind, string partnerId, string targetId) Agenda(EpisodeState s, string npcId)
        {
            var agenda = NpcAgendas.Of(s, npcId);
            return (agenda?.kind, agenda?.partnerId, agenda?.targetId);
        }

        /// <summary>An invitation agreed, as the engine writes its pact (the private AllyThroughInvitation).</summary>
        private static void Invite(EpisodeState s, string npcId) =>
            typeof(EpisodeEngine).GetMethod("AllyThroughInvitation", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { s, npcId });

        // ------------------------------------------------------------------ the join (§14.L.B-D)

        [Test]
        public void AnOutsiderJoinsACounterPactAtFiveWhileAKnownPairIsLive()
        {
            Assert.That(AlumniRules.GrowingPacts, Is.EqualTo(5));
            Assert.That(AlumniRules.Latest, Is.GreaterThanOrEqualTo(AlumniRules.GrowingPacts), "Fresh All-Star seasons grow pacts (§14.L.A): 5 is a bound.");
            Assert.That(NpcAlliances.JoinWelcomeLine, Is.EqualTo(-5), "One rumour above the sour line.");
            Assert.That(KnownBlocs.CounterPactMargin, Is.EqualTo(1));

            // The default cast, its traits as they come: Dan and Janelle pair in week 1, and Will wants in through Janelle.
            var s = Season(8, 5101u, Five);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will);
            foreach (var who in s.contestants)
                foreach (var other in s.contestants.Where(c => c.id != who.id)) Set(s, who.id, other.id, 0);
            var pact = Pair(s, dan, janelle);
            EpisodeEngine.ReconcileAllianceRows(s);
            var row = s.ledger.alliances.Single(r => r.id == pact.id);
            Set(s, will, janelle, 90);
            Set(s, janelle, will, 40);
            int npcPacts = NpcAlliances.NpcOnlyPacts(s).Count;

            var at = NextWeek(s, will);
            Assert.That(KnownBlocs.LivePairs(at).Select(p => p.Fact.Id), Is.EqualTo(new[] { "bb16-hitmen" }), "The Hitmen stand.");
            Assert.That(KnownBlocs.CounterPactBound(at), Is.EqualTo(3), "One live pair: three.");
            Assert.That(NpcAlliances.Desire(at, will, janelle), Is.GreaterThan(NpcAlliances.ProposeThreshold));

            var act = Beat(at, will, "1");
            Assert.That((act?.kind, act?.actorId, act?.partnerId), Is.EqualTo((NpcActKinds.Pact, will, janelle)),
                "At his pact window Will joins, Janelle his sponsor.");
            var grown = Find(at, pact);
            Assert.That(grown.members, Is.EqualTo(new[] { dan, janelle, will }), "On the end: the founder stays first.");
            Assert.That((grown.id, grown.name, grown.active), Is.EqualTo((pact.id, pact.name, true)), "The same pact, name and all.");
            Assert.That(EpisodeEngine.Founder(grown), Is.EqualTo(dan));
            Assert.That(Fact(at, grown).knowers, Is.EquivalentTo(new[] { dan, janelle, will }), "The recruit learns the pact.");
            Assert.That(Fact(at, grown).visibility, Is.EqualTo(FactVisibility.Private));
            Assert.That((Fact(at, grown).actorId, Fact(at, grown).subjectId), Is.EqualTo((dan, janelle)), "Its founders stay its actor and subject.");
            Assert.That(NpcAlliances.NpcOnlyPacts(at).Count, Is.EqualTo(npcPacts), "No pact is new.");
            EpisodeEngine.ReconcileAllianceRows(at);
            var rows = at.ledger.alliances.Where(r => r.id == pact.id).ToList();
            Assert.That(rows, Has.Count.EqualTo(1), "No row is new.");
            Assert.That((rows[0].startedWeek, rows[0].endedWeek, rows[0].why), Is.EqualTo((row.startedWeek, 0, row.why)), "The row stays as it was.");
        }

        [TestCase(4)] [TestCase(3)] [TestCase(2)] [TestCase(1)] [TestCase(0)]
        public void BelowFiveNothingGrowsAndTheStateIsByteIdentical(int value)
        {
            var (s, pact, dan, janelle, will) = Hitmen(5102u, value);
            Assert.That(KnownBlocs.CounterPactBound(s), Is.Zero, "No bound below 5.");
            Assert.That(Joins(Held(s, Five), will), Is.True, "The same house at 5 takes Will in.");
            var off = Held(s, Five);
            off.agencyRulesStartWeek = 0;
            Assert.That(JoinsACopy(off, will), Is.False, "And not at 5 without agency, under which the cap exists.");

            string before = AlumniHistoriesTests.Json(s);
            Assert.That(NpcAlliances.TryJoin(s, will, new HashSet<string>(StringComparer.Ordinal), out string sponsor), Is.False);
            Assert.That(sponsor, Is.Null);
            Assert.That(AlumniHistoriesTests.Json(s), Is.EqualTo(before), "Nothing written, nothing read into the state.");

            // The rule alone is its old loop: Dissolve, then each houseguest's proposal, byte for byte.
            Assert.That(NpcSocialState.AutonomyHasBegun(s), Is.True);
            var rule = s.Clone();
            NpcAlliances.Settle(rule);
            var old = s.Clone();
            NpcAlliances.Dissolve(old);
            var joined = new HashSet<string>(StringComparer.Ordinal);
            foreach (var npc in old.contestants.Where(c => c.status == ContestantStatus.Active && !c.isPlayer).ToList())
                NpcAlliances.TryPropose(old, npc.id, joined);
            Assert.That(AlumniHistoriesTests.Json(rule), Is.EqualTo(AlumniHistoriesTests.Json(old)));
            Assert.That(Find(rule, pact).members, Is.EqualTo(new[] { dan, janelle }));

            // A trio written by hand, the shape a join would leave: its member is still their common threat, and a joiner's
            // sour edge still ends it whole.
            var trio = s.Clone();
            HandAdd(trio, Find(trio, pact), will);
            trio.Find(will).hohWins = 2;
            Set(trio, dan, will, 10);
            Assert.That(NpcDeals.CommonThreat(trio, dan, janelle), Is.EqualTo(will), "Below 5 a pact-mate with two wins is a common threat.");
            Assert.That(NpcDeals.Offer(trio, dan, janelle), Is.EqualTo(DealKind.TargetAgreement));
            Set(trio, dan, will, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(trio);
            Assert.That(Find(trio, pact).active, Is.False, "Below 5 the pact ends whole.");
            Assert.That(Find(trio, pact).members, Is.EqualTo(new[] { dan, janelle, will }));
        }

        [Test]
        public void ARegularSeasonNeverGrowsAPact()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.Regular }, 5103u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.alumniRulesVersion, Is.EqualTo(AlumniRules.Off), "A Regular season seats no alumnus.");
            Plain(s);
            var npcs = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            var pact = Pair(s, npcs[0], npcs[1]);
            s.week++;
            WantsIn(s, npcs[2], npcs[1]);
            Assert.That(KnownBlocs.LivePairs(s), Is.Empty);
            Assert.That(KnownBlocs.CounterPactBound(s), Is.Zero);
            Assert.That(JoinsACopy(s, npcs[2]), Is.False);
            NpcAlliances.Settle(s);
            Assert.That(Find(s, pact).members, Is.EqualTo(new[] { npcs[0], npcs[1] }));

            // The field alone, with no edition, is no alumni season.
            var held = Held(s, Five);
            Assert.That(AlumniRules.At(held, Five), Is.False);
            Assert.That(JoinsACopy(held, npcs[2]), Is.False);
        }

        [Test]
        public void NoPactGrowsOnceNoKnownPairIsLiveAndAGrownPactStands()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5104u, Five);
            string derrick = On(s, Derrick), cody = On(s, Cody), vanessa = On(s, Vanessa);
            Assert.That(KnownBlocs.CounterPactBound(s), Is.EqualTo(3));
            Assert.That(JoinsACopy(s, will), Is.True, "While the Hitmen stand, Will joins.");

            // Derrick leaves: the Hitmen end with him, and so does the bound.
            var gone = s.Clone();
            gone.Find(derrick).status = ContestantStatus.Evicted;
            Assert.That(KnownBlocs.LivePairs(gone), Is.Empty);
            Assert.That(KnownBlocs.CounterPactBound(gone), Is.Zero);
            Assert.That(JoinsACopy(gone, will), Is.False);

            // A public split, this week's block: Derrick, Head of Household, puts Cody up.
            var split = s.Clone();
            split.phase = EpisodePhase.Campaign;
            split.hohId = derrick;
            split.nominees = new List<string> { cody, vanessa };
            split.evictionResolved = false;
            Assert.That(KnownBlocs.CounterPactBound(split), Is.Zero);
            Assert.That(JoinsACopy(split, will), Is.False);

            // A grown pact in the twelve-house, and then Cody leaves: both pairs end, the pact stands as it is and takes nobody.
            var (t, tPact, tDan, tJanelle, tWill) = Twelve(5105u, Five);
            Assert.That(Joins(t, tWill), Is.True);
            t.week++;
            string tVanessa = On(t, Vanessa);
            WantsIn(t, tVanessa, tDan);
            Assert.That(KnownBlocs.CounterPactBound(t), Is.EqualTo(4));
            Assert.That(JoinsACopy(t, tVanessa), Is.True, "While both pairs stand, a fourth could join.");
            t.Find(On(t, Cody)).status = ContestantStatus.Evicted;
            Assert.That(KnownBlocs.CounterPactBound(t), Is.Zero);
            NpcAlliances.Dissolve(t);
            Assert.That(Find(t, tPact).active, Is.True);
            Assert.That(Find(t, tPact).members, Is.EqualTo(new[] { tDan, tJanelle, tWill }), "A grown pact is never trimmed back.");
            Assert.That(JoinsACopy(t, tVanessa), Is.False);
        }

        [Test]
        public void NoLivePairMemberJoinsSponsorsOrIsGrown()
        {
            var (s, pact, dan, janelle, will) = Twelve(5106u, Five);
            string derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler), vanessa = On(s, Vanessa), chelsie = On(s, Chelsie),
                rachel = On(s, Rachel);
            Assert.That(KnownBlocs.CounterPactBound(s), Is.EqualTo(4), "Two live pairs: four.");
            Assert.That(KnownBlocs.InLivePair(s, tyler) && KnownBlocs.InLivePair(s, cody) && KnownBlocs.InLivePair(s, derrick), Is.True);
            Assert.That(KnownBlocs.InLivePair(s, dan) || KnownBlocs.InLivePair(s, will), Is.False);

            // Tyler, of the Committee, wants in as much as Will: never.
            WantsIn(s, tyler, janelle);
            Set(s, janelle, tyler, 30);
            Assert.That(JoinsACopy(s, tyler), Is.False, "A live pair's member joins nothing.");

            // A pact that holds Derrick never grows, whoever sponsors.
            Set(s, derrick, vanessa, 90);
            Set(s, vanessa, derrick, 90);
            Assert.That(NpcAlliances.TryPropose(s, derrick, new HashSet<string>(StringComparer.Ordinal)), Is.True);
            var hitman = s.alliances[s.alliances.Count - 1];
            Assert.That(hitman.members, Is.EquivalentTo(new[] { derrick, vanessa }));
            s.week++;
            WantsIn(s, chelsie, vanessa);
            Set(s, chelsie, derrick, 60);
            Set(s, derrick, chelsie, 30);
            Assert.That(KnownBlocs.CounterPactBound(s), Is.EqualTo(4));
            Assert.That(JoinsACopy(s, chelsie), Is.False, "Wanting only Vanessa and Derrick, Chelsie joins nobody: their pact holds a Hitman.");
            // Wanting Janelle too, she joins Dan and Janelle's, and the pact with a Hitman in it stays a pair.
            var tried = s.Clone();
            WantsIn(tried, chelsie, janelle);
            Assert.That(Joins(tried, chelsie, out string sponsor), Is.True);
            Assert.That(sponsor, Is.EqualTo(janelle));
            Assert.That(Find(tried, pact).members, Is.EqualTo(new[] { dan, janelle, chelsie }));
            Assert.That(Find(tried, hitman).members, Has.Count.EqualTo(2));

            // Once the Committee has split in public, Tyler is in no live pair, and may join.
            var after = s.Clone();
            PutUp(after, cody, tyler, rachel);
            Assert.That(KnownBlocs.InLivePair(after, tyler), Is.False);
            Assert.That(KnownBlocs.CounterPactBound(after), Is.EqualTo(3), "The Hitmen alone: three.");
            Assert.That(Joins(after, tyler, out string tylersSponsor), Is.True);
            Assert.That(tylersSponsor, Is.EqualTo(janelle));
            Assert.That(Find(after, pact).members, Is.EqualTo(new[] { dan, janelle, tyler }));
        }

        [Test]
        public void APactGrowsToTheBoundAndNeverPastFour()
        {
            // The eight-house: three.
            var (s, pact, dan, janelle, will) = Hitmen(5107u, Five);
            Assert.That(Joins(s, will), Is.True);
            s.week++;
            string vanessa = On(s, Vanessa);
            WantsIn(s, vanessa, dan);
            Assert.That(EpisodeEngine.InTheHouse(s, Find(s, pact)), Is.EqualTo(KnownBlocs.CounterPactBound(s)));
            Assert.That(JoinsACopy(s, vanessa), Is.False, "At the bound, nobody else.");

            // The twelve-house: four, and never a fifth.
            var (t, tPact, tDan, tJanelle, tWill) = Twelve(5108u, Five);
            string tVanessa = On(t, Vanessa), tChelsie = On(t, Chelsie), tCody = On(t, Cody), tTyler = On(t, Tyler), tRachel = On(t, Rachel),
                tXavier = On(t, Xavier);
            Assert.That(Joins(t, tWill), Is.True);
            t.week++;
            WantsIn(t, tVanessa, tDan);
            // Of two members who would have her, the one she wants more brings her in.
            Set(t, tVanessa, tWill, 80);
            Set(t, tWill, tVanessa, 30);
            Assert.That(NpcAlliances.Desire(t, tVanessa, tWill), Is.GreaterThan(NpcAlliances.Desire(t, tVanessa, tDan)));
            Assert.That(Joins(t, tVanessa, out string hers), Is.True);
            Assert.That(hers, Is.EqualTo(tWill));
            Assert.That(Find(t, tPact).members, Is.EqualTo(new[] { tDan, tJanelle, tWill, tVanessa }));
            t.week++;
            WantsIn(t, tChelsie, tJanelle);
            Assert.That(KnownBlocs.CounterPactBound(t), Is.EqualTo(EpisodeEngine.LargestPact));
            Assert.That(JoinsACopy(t, tChelsie), Is.False, "Four in the house: the most a pact holds.");

            // The Committee splits in public: the bound falls to three, and the four stays a four.
            PutUp(t, tCody, tTyler, tRachel);
            Assert.That(KnownBlocs.CounterPactBound(t), Is.EqualTo(3));
            NpcAlliances.Dissolve(t);
            Assert.That(Find(t, tPact).members, Is.EqualTo(new[] { tDan, tJanelle, tWill, tVanessa }));
            Assert.That(Find(t, tPact).active, Is.True);
            WantsIn(t, tXavier, tDan);
            Assert.That(JoinsACopy(t, tChelsie) || JoinsACopy(t, tXavier), Is.False);
        }

        [Test]
        public void OneChangeAPactAWeekAndNoneInItsFoundingWeek()
        {
            var s = Season(12, 5109u, Five);
            Plain(s);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will), vanessa = On(s, Vanessa);
            UntilFirstBeatAfterHoH(s, will, vanessa);
            var founding = AllWeekCadenceTests.InWindow(s, Windows.AfterHoH);
            // Formed by the rule directly: no act records it, so only its fact's week stands in the way.
            var pact = Pair(founding, dan, janelle);
            Assert.That(founding.npcSocial.acts.Any(a => a != null && a.week == founding.week && a.kind == NpcActKinds.Pact), Is.False);
            WantsIn(founding, will, janelle);
            WantsIn(founding, vanessa, dan);
            Assert.That(KnownBlocs.CounterPactBound(founding), Is.EqualTo(4));
            Assert.That(JoinsACopy(founding, will), Is.False, "Never in its founding week.");
            Assert.That(NpcSocialActions.Beat(founding.Clone(), will, StoryRandom.Stream(founding, "test:grow:0"), false, false)?.kind,
                Is.Not.EqualTo(NpcActKinds.Pact), "Nor through a beat.");

            // The week after: Will joins at his first beat, and that is the pact's change for the week.
            var next = NextWeek(founding, will, vanessa);
            var rulePass = next.Clone();
            NpcAlliances.Settle(rulePass);
            Assert.That(Find(rulePass, pact).members, Has.Count.EqualTo(3), "The rule's pass makes one change a pact a week, too.");
            var act = Beat(next, will, "1");
            Assert.That((act?.kind, act?.actorId, act?.partnerId), Is.EqualTo((NpcActKinds.Pact, will, janelle)));
            Assert.That(Find(next, pact).members, Is.EqualTo(new[] { dan, janelle, will }));
            var sides = PactSidesThisWeek(next);
            Assert.That(NpcAlliances.TryJoin(next.Clone(), vanessa, sides, out _), Is.False, "One change a pact a week.");
            Assert.That(NpcSocialActions.Beat(next.Clone(), vanessa, StoryRandom.Stream(next, "test:grow:2"), false, false)?.kind,
                Is.Not.EqualTo(NpcActKinds.Pact), "Nor through her beat.");
            Assert.That(NpcAlliances.TryJoin(next.Clone(), vanessa, new HashSet<string>(StringComparer.Ordinal), out _), Is.True,
                "Only the week's change holds her back.");
            AtMostOnePactActEach(next);

            // And the week after that, Vanessa.
            var third = NextWeek(next, vanessa);
            var hers = Beat(third, vanessa, "3");
            Assert.That((hers?.kind, hers?.actorId, hers?.partnerId), Is.EqualTo((NpcActKinds.Pact, vanessa, dan)));
            Assert.That(Find(third, pact).members, Is.EqualTo(new[] { dan, janelle, will, vanessa }));
            AtMostOnePactActEach(third);
        }

        [Test]
        public void TheRecruitMustWantInAndEveryMemberMustWelcome()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5110u, Five);
            string vanessa = On(s, Vanessa), danielle = On(s, Danielle), derrick = On(s, Derrick), cody = On(s, Cody);
            // Janelle's strategic value at nought, so Will's desire for her is 0.4 of his view and the Hitmen's 7.5.
            var her = s.Find(janelle);
            her.stats.social = 0;
            her.hohWins = her.vetoWins = 0;
            Set(s, will, janelle, 50);
            Assert.That(NpcAlliances.Desire(s, will, janelle), Is.EqualTo(0.4 * 50 + KnownBlocs.CounterBlocDesire).Within(Exact));
            Assert.That(JoinsACopy(s, will), Is.True, "The baseline joins.");

            EpisodeState With(Action<EpisodeState> change)
            {
                var copy = s.Clone();
                change(copy);
                return copy;
            }

            // Desire exactly at 25 is not past it.
            var level = With(c => Set(c, will, janelle, 43.75));
            Assert.That(NpcAlliances.Desire(level, will, janelle), Is.EqualTo(NpcAlliances.ProposeThreshold));
            Assert.That(JoinsACopy(level, will), Is.False, "Desire at exactly 25.");
            Assert.That(JoinsACopy(With(c => Set(c, will, janelle, 44)), will), Is.True);

            // The recruit's floor, with desire to spare: a houseguest they both dislike.
            EpisodeState Threat(double view) => With(c =>
            {
                Set(c, will, danielle, -25);
                Set(c, janelle, danielle, -25);
                Set(c, will, janelle, view);
            });
            Assert.That(NpcAlliances.Desire(Threat(24), will, janelle), Is.GreaterThan(NpcAlliances.ProposeThreshold));
            Assert.That(JoinsACopy(Threat(24), will), Is.False, "The recruit at 24.");
            Assert.That(JoinsACopy(Threat(25), will), Is.True);

            // The sponsor's floor.
            Assert.That(JoinsACopy(With(c => Set(c, janelle, will, 24)), will), Is.False, "The sponsor at 24.");
            Assert.That(JoinsACopy(With(c => Set(c, janelle, will, 25)), will), Is.True);

            // Every member welcomes, both ways: −5 and no less.
            Assert.That(JoinsACopy(With(c => Set(c, dan, will, -6)), will), Is.False, "A member at −6.");
            Assert.That(JoinsACopy(With(c => Set(c, will, dan, -6)), will), Is.False, "The recruit at −6 toward a member.");
            Assert.That(JoinsACopy(With(c => Both(c, dan, will, NpcAlliances.JoinWelcomeLine)), will), Is.True, "−5 is welcome.");
            Assert.That(JoinsACopy(With(c => Both(c, dan, will, -10)), will), Is.False, "A cold pair is never joined.");

            // No grudge of forty, either way, with anybody in it.
            Assert.That(EpisodeEngine.StoryAt(s, StoryRules.Grudges), Is.True);
            EpisodeState Grudge(string holderId, string targetId, double severity) => With(c => c.story.grudges.Add(new GrudgeState
            {
                holderId = holderId, targetId = targetId, cause = GrudgeCauses.DealBroken, severity = severity, originWeek = 1, count = 1,
            }));
            Assert.That(JoinsACopy(Grudge(dan, will, EpisodeEngine.AllianceGrudgeLine), will), Is.False, "A member's grudge.");
            Assert.That(JoinsACopy(Grudge(will, dan, EpisodeEngine.AllianceGrudgeLine), will), Is.False, "The recruit's grudge against a member.");
            Assert.That(JoinsACopy(Grudge(will, janelle, EpisodeEngine.AllianceGrudgeLine), will), Is.False, "Against the sponsor.");
            Assert.That(JoinsACopy(Grudge(dan, will, EpisodeEngine.AllianceGrudgeLine - 1), will), Is.True);

            // Already allied with a member; at the most pacts anybody holds; already in a pact of houseguests. Each pact Will
            // carries costs him ten of desire, so two houseguests he and Janelle both dislike keep him wanting in throughout.
            AllianceState Written(EpisodeState c, string name, params string[] members)
            {
                var written = new AllianceState { id = "alliance-test-" + name, name = name, members = members.ToList(), active = true };
                c.alliances.Add(written);
                return written;
            }
            EpisodeState Carrying(Action<EpisodeState> pacts) => With(c =>
            {
                foreach (var other in new[] { danielle, vanessa })
                {
                    Set(c, will, other, -25);
                    Set(c, janelle, other, -25);
                }
                pacts(c);
                Assert.That(NpcAlliances.Desire(c, will, janelle), Is.GreaterThan(NpcAlliances.ProposeThreshold), "Will still wants in.");
            });
            Assert.That(JoinsACopy(Carrying(c => Written(c, "with-dan", c.playerId, will, dan)), will), Is.False, "Allied with a member already.");
            Assert.That(JoinsACopy(Carrying(c =>
            {
                Written(c, "one", c.playerId, will);
                Written(c, "two", c.playerId, will, derrick);
                Written(c, "three", c.playerId, will, cody);
            }), will), Is.False, "At the most pacts anybody holds.");
            Assert.That(JoinsACopy(Carrying(c =>
            {
                Written(c, "one", c.playerId, will);
                Written(c, "two", c.playerId, will, derrick);
            }), will), Is.True, "Two pacts with the player are room for one more.");
            Assert.That(JoinsACopy(Carrying(c => Written(c, "theirs", will, derrick)), will), Is.False, "In a pact of houseguests already: one each.");
        }

        [Test]
        public void TheRecruitsEdgesMatchABirths()
        {
            var s = Season(8, 5111u, Five);
            Plain(s);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will);
            var pact = Pair(s, dan, janelle);
            double birthTrust = ThreatAssessment.TrustScore(s, janelle, dan);
            Assert.That(birthTrust, Is.GreaterThan(ThreatAssessment.NeutralTrust));
            s.week++;
            WantsIn(s, will, janelle);
            var before = s.Clone();

            Assert.That(Joins(s, will), Is.True);
            string why = pact.name + " was joined";
            foreach (var (from, to) in new[] { (dan, will), (will, dan), (janelle, will), (will, janelle) })
            {
                var edge = s.relationships.Single(r => r.fromId == from && r.toId == to);
                var old = before.relationships.Single(r => r.fromId == from && r.toId == to);
                var added = edge.events.Skip(old.events.Count).ToList();
                Assert.That(added.Select(e => (e.type, e.impactScore, e.description, e.week)),
                    Is.EqualTo(new[] { ("alliance-formed", EpisodeEngine.AllianceFormedImpact, why, s.week) }), from + " -> " + to);
                Assert.That(ThreatAssessment.TrustScore(s, to, from), Is.EqualTo(birthTrust).Within(Exact), from + " -> " + to + ": a birth's trust.");
            }
            foreach (var edge in s.relationships)
            {
                var old = before.relationships.Single(r => r.fromId == edge.fromId && r.toId == edge.toId);
                Assert.That(edge.score, Is.EqualTo(old.score), "No score moves.");
                bool recruits = (edge.fromId == will && (edge.toId == dan || edge.toId == janelle)) || (edge.toId == will && (edge.fromId == dan || edge.fromId == janelle));
                if (!recruits) Assert.That(edge.events.Count, Is.EqualTo(old.events.Count), edge.fromId + " -> " + edge.toId + ": nothing on any other edge, the founders' included.");
            }
            Assert.That(s.events.Count, Is.EqualTo(before.events.Count), "No line.");
            Assert.That(s.memories.Count, Is.EqualTo(before.memories.Count), "No memory.");
        }

        [Test]
        public void AJoinDrawsNothingAndMintsTwoSequencesAMember()
        {
            var (s, pact, dan, janelle, will) = Twelve(5112u, Five);
            uint random = s.randomState;
            int sequence = s.nextSequence;
            Assert.That(Joins(s, will), Is.True);
            Assert.That(s.randomState, Is.EqualTo(random), "No draw.");
            Assert.That(s.nextSequence, Is.EqualTo(sequence + 4), "A third member: two receipts each for two in the house.");

            s.week++;
            string vanessa = On(s, Vanessa);
            WantsIn(s, vanessa, dan);
            random = s.randomState;
            sequence = s.nextSequence;
            Assert.That(Joins(s, vanessa), Is.True);
            Assert.That(s.randomState, Is.EqualTo(random));
            Assert.That(s.nextSequence, Is.EqualTo(sequence + 6), "A fourth: two each for three.");

            // Through a beat the stream the beat is handed is never asked either.
            var (b, bPact, bDan, bJanelle, bWill) = Hitmen(5113u, Five);
            var at = NextWeek(b, bWill);
            int draws = 0;
            random = at.randomState;
            var act = NpcSocialActions.Beat(at, bWill, () => { draws++; return 0.5; }, false, false);
            Assert.That(act?.kind, Is.EqualTo(NpcActKinds.Pact));
            Assert.That(draws, Is.Zero);
            Assert.That(at.randomState, Is.EqualTo(random));
        }

        [Test]
        public void TheCapAndOneEachStillHold()
        {
            var (s, pact, dan, janelle, will) = Twelve(5114u, Five);
            string danielle = On(s, Danielle), vanessa = On(s, Vanessa), chelsie = On(s, Chelsie), rachel = On(s, Rachel);
            var theirs = Pair(s, danielle, vanessa);
            Assert.That(NpcAlliances.PactCap(s), Is.EqualTo(3));
            Assert.That(NpcAlliances.NpcOnlyPacts(s), Has.Count.EqualTo(2));
            Both(s, chelsie, rachel, 90);
            Assert.That(NpcAlliances.PactRoom(s, chelsie, rachel), Is.True);

            Assert.That(Joins(s, will), Is.True);
            Assert.That(NpcAlliances.NpcOnlyPacts(s), Has.Count.EqualTo(2), "A join adds no pact.");
            Assert.That(NpcAlliances.PactRoom(s, chelsie, rachel), Is.True, "And takes no place under the cap.");
            Assert.That(NpcAlliances.NpcOnlyPacts(s).Count(p => p.members.Contains(will)), Is.EqualTo(1), "The recruit is in one.");

            s.week++;
            // One each: a member of one pact never joins another, nor does the recruit, however warm.
            WantsIn(s, vanessa, dan);
            WantsIn(s, will, danielle);
            Set(s, will, vanessa, 60);
            Assert.That(JoinsACopy(s, vanessa), Is.False, "Vanessa is in a pact of houseguests already.");
            Assert.That(JoinsACopy(s, will), Is.False, "So is Will, now.");
            Assert.That(Find(s, theirs).members, Is.EqualTo(new[] { danielle, vanessa }));

            // At the cap a new pair still cannot start, grown pacts or none.
            Assert.That(NpcAlliances.TryPropose(s, chelsie, new HashSet<string>(StringComparer.Ordinal)), Is.True);
            Assert.That(NpcAlliances.NpcOnlyPacts(s), Has.Count.EqualTo(3));
            string xavier = On(s, Xavier), tyler = On(s, Tyler);
            Both(s, xavier, tyler, 90);
            Assert.That(NpcAlliances.PactRoom(s, xavier, tyler), Is.False, "Three pacts for eleven: the house is at its share.");
        }

        [Test]
        public void APactAnyoneOutsideKnowsOfNeverGrows()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5115u, Five);
            string vanessa = On(s, Vanessa);
            Assert.That(JoinsACopy(s, will), Is.True, "Kept to itself, it grows.");

            EpisodeState Known(Action<EpisodeState, HouseFactState> learn)
            {
                var copy = s.Clone();
                learn(copy, Fact(copy, Find(copy, pact)));
                return copy;
            }
            Assert.That(JoinsACopy(Known((c, f) => Knowledge.AddKnower(c, f, c.playerId)), will), Is.False, "The player listened in.");
            Assert.That(JoinsACopy(Known((c, f) => { f.visibility = FactVisibility.Whispered; Knowledge.AddKnower(c, f, vanessa); }), will), Is.False,
                "Whispered to Vanessa.");
            Assert.That(JoinsACopy(Known((c, f) => f.visibility = FactVisibility.Whispered), will), Is.False, "Whispered at all.");
            Assert.That(JoinsACopy(Known((c, f) =>
            {
                f.visibility = FactVisibility.Public;
                foreach (var who in c.Active) Knowledge.AddKnower(c, f, who.id);
            }), will), Is.False, "Out in the open.");
            // A story leaver who knows: Vanessa in it, then out of it by a story's leave, still a knower.
            Assert.That(JoinsACopy(Known((c, f) =>
            {
                HandAdd(c, Find(c, pact), vanessa);
                Find(c, pact).members.Remove(vanessa);
            }), will), Is.False, "A story's leaver still knows it.");

            // After a join the knowers are exactly the members; and a shed member stays one, so it never grows again.
            Assert.That(Joins(s, will), Is.True);
            Assert.That(Fact(s, pact).knowers, Is.EquivalentTo(Find(s, pact).members));
            s.week++;
            Set(s, will, dan, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(s);
            Assert.That(Find(s, pact).members, Is.EqualTo(new[] { dan, janelle }), "Will is shed.");
            Assert.That(Fact(s, pact).knowers, Does.Contain(will));
            s.week++;
            WantsIn(s, vanessa, dan);
            Assert.That(KnownBlocs.CounterPactBound(s), Is.EqualTo(3));
            Assert.That(JoinsACopy(s, vanessa), Is.False, "A pact that has shed somebody never grows again.");
        }

        [Test]
        public void OnlyTheHouseguestsOwnPactsGrow()
        {
            var s = Season(8, 5116u, Five);
            Plain(s);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will);

            // A story's pact, kept to its members as well as any.
            var story = s.Clone();
            Both(story, dan, janelle, 90);
            var storyPact = NpcAlliances.FormFromStory(story, new List<string> { dan, janelle });
            Assert.That(storyPact.id, Does.StartWith("alliance-story-"));
            Assert.That(NpcAlliances.HouseguestsOwn(storyPact), Is.False);
            Assert.That(Fact(story, storyPact).knowers, Is.EquivalentTo(new[] { dan, janelle }));
            story.week++;
            WantsIn(story, will, janelle);
            Assert.That(JoinsACopy(story, will), Is.False, "A story's pact is its arc's.");

            // The houseguests' own, which the player joined and left: the player still knows it.
            var left = s.Clone();
            var theirs = Pair(left, dan, janelle);
            theirs.members.Add(left.playerId);
            theirs.playerJoined = true;
            Knowledge.AddKnower(left, Fact(left, theirs), left.playerId);
            left.week++;
            theirs.members.Remove(left.playerId);
            WantsIn(left, will, janelle);
            Assert.That(NpcAlliances.NpcOnlyPacts(left).Select(p => p.id), Does.Contain(theirs.id));
            Assert.That(JoinsACopy(left, will), Is.False, "A pact the player left is never the houseguests' alone again.");

            // The id's prefix alone: the same private pact of two under another id grows only as the houseguests'.
            foreach (var (prefix, grows) in new[] { ("alliance-", false), (NpcAlliances.HouseguestPactPrefix, true) })
            {
                var c = s.Clone();
                var written = new AllianceState { id = prefix + c.nextSequence++, name = "The Written Pact", members = new List<string> { dan, janelle }, active = true };
                c.alliances.Add(written);
                Knowledge.AllianceFormed(c, written);
                Both(c, dan, janelle, 90);
                c.week++;
                WantsIn(c, will, janelle);
                Assert.That(JoinsACopy(c, will), Is.EqualTo(grows), prefix);
            }
        }

        [Test]
        public void ThePlayerIsNeverRecruited()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5117u, Five);
            string player = s.playerId;
            Both(s, player, janelle, 90);
            Both(s, player, dan, 60);
            Assert.That(JoinsACopy(s, player), Is.False, "Never the player.");
            Assert.That(NpcSocialActions.Beat(s.Clone(), player, StoryRandom.Stream(s, "test:grow:player"), false, false), Is.Null);
            var rule = s.Clone();
            Set(rule, will, janelle, 0);
            NpcAlliances.Settle(rule);
            Assert.That(Find(rule, pact).members, Is.EqualTo(new[] { dan, janelle }), "The rule's pass leaves the player out.");

            // A pact the player is in is no pact of houseguests alone: it never grows.
            var invited = s.Clone();
            HandAdd(invited, Find(invited, pact), player);
            Assert.That(NpcAlliances.NpcOnlyPacts(invited).Any(p => p.id == pact.id), Is.False);
            Assert.That(JoinsACopy(invited, will), Is.False);

            // Nor is the player ever shed: a houseguests' pact the player is in goes by C5's rule, as before. Dan has turned
            // on the player, so Dan is out of it, and the player and Janelle hear it.
            Both(invited, player, dan, 0);
            Both(invited, player, janelle, 0);
            Set(invited, dan, player, NpcAlliances.SourLine - 1);
            int lines = invited.events.Count;
            NpcAlliances.Dissolve(invited);
            Assert.That(Find(invited, pact).members, Is.EqualTo(new[] { janelle, player }));
            Assert.That(invited.events.Count, Is.EqualTo(lines + 1), "C5's line.");
        }

        [Test]
        public void AnInvitationNeverTakesAHouseguestsFourPastFour()
        {
            // At 5 a grown four is passed over: the player and Dan start a pact of their own.
            var (s, pact, dan, janelle, will) = Twelve(5118u, Five);
            string vanessa = On(s, Vanessa);
            Assert.That(StrategyRules.Apply(s), Is.True);
            Assert.That(Joins(s, will), Is.True);
            s.week++;
            WantsIn(s, vanessa, dan);
            Assert.That(Joins(s, vanessa), Is.True);
            Assert.That(EpisodeEngine.InTheHouse(s, Find(s, pact)), Is.EqualTo(EpisodeEngine.LargestPact));
            int pacts = s.alliances.Count;
            Invite(s, dan);
            Assert.That(Find(s, pact).members, Is.EqualTo(new[] { dan, janelle, will, vanessa }), "The four stays four.");
            Assert.That(s.alliances, Has.Count.EqualTo(pacts + 1));
            var own = s.alliances[s.alliances.Count - 1];
            Assert.That(own.members, Is.EqualTo(new[] { s.playerId, dan }), "A pact of their own.");

            // The skip counts those in the house (§14.L.E): the same four with Vanessa gone holds three, and takes the player in.
            var (g, gPact, gDan, gJanelle, gWill) = Twelve(5133u, Five);
            string gVanessa = On(g, Vanessa);
            Assert.That(Joins(g, gWill), Is.True);
            g.week++;
            WantsIn(g, gVanessa, gDan);
            Assert.That(Joins(g, gVanessa), Is.True);
            g.Find(gVanessa).status = ContestantStatus.Evicted;
            g.week++;
            Assert.That((Find(g, gPact).members.Count, EpisodeEngine.InTheHouse(g, Find(g, gPact))), Is.EqualTo((4, 3)));
            Invite(g, gDan);
            Assert.That(Find(g, gPact).members, Is.EqualTo(new[] { gDan, gJanelle, gWill, gVanessa, g.playerId }), "Three in the house: the player is taken in.");

            // At 4 the same four, written by hand, takes the player in: the pass-over is growth's alone.
            var (f, fPact, fDan, fJanelle, fWill) = Twelve(5119u, Four);
            string fVanessa = On(f, Vanessa);
            HandAdd(f, Find(f, fPact), fWill);
            HandAdd(f, Find(f, fPact), fVanessa);
            Invite(f, fDan);
            Assert.That(Find(f, fPact).members, Is.EqualTo(new[] { fDan, fJanelle, fWill, fVanessa, f.playerId }));

            // A story's four takes the player in at 5 and at 4.
            foreach (int value in new[] { Five, Four })
            {
                var t = Season(12, 5120u, value);
                Plain(t);
                var four = new List<string> { On(t, Dan), On(t, Janelle), On(t, Will), On(t, Vanessa) };
                var storyPact = NpcAlliances.FormFromStory(t, four);
                Assert.That(storyPact, Is.Not.Null);
                Invite(t, four[0]);
                Assert.That(storyPact.members, Is.EqualTo(four.Concat(new[] { t.playerId })), "A story's four at " + value + ".");
            }
        }

        // ------------------------------------------------------------------ consumers (§14.L.F)

        [Test]
        public void AGrownPactShedsASourJoinerWhileKeptToItselfAndOtherwiseEndsWhole()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5121u, Five);
            Assert.That(Joins(s, will), Is.True);
            s.week++;
            EpisodeEngine.ReconcileAllianceRows(s);

            // One rumour on an edge at the welcome line takes it to the sour line, not past it: nothing ends.
            var rumour = s.Clone();
            Both(rumour, dan, will, NpcAlliances.JoinWelcomeLine + NpcSocialActions.RumorImpact);
            Assert.That(rumour.Score(dan, will), Is.EqualTo(NpcAlliances.SourLine));
            NpcAlliances.Dissolve(rumour);
            Assert.That(Find(rumour, pact).members, Is.EqualTo(new[] { dan, janelle, will }));
            Assert.That(Find(rumour, pact).active, Is.True);

            // Past it, the joiner is out: the founders stand, the row stays open, and no line or receipt says so.
            var shed = s.Clone();
            Set(shed, dan, will, NpcAlliances.SourLine - 1);
            int events = shed.events.Count, sequence = shed.nextSequence, receipts = shed.relationships.Sum(r => r.events.Count);
            NpcAlliances.Dissolve(shed);
            Assert.That(Find(shed, pact).members, Is.EqualTo(new[] { dan, janelle }), "The joiner who soured is out.");
            Assert.That(Find(shed, pact).active, Is.True, "The founders stand.");
            Assert.That((shed.events.Count, shed.nextSequence, shed.relationships.Sum(r => r.events.Count)), Is.EqualTo((events, sequence, receipts)));
            EpisodeEngine.ReconcileAllianceRows(shed);
            Assert.That(shed.ledger.alliances.Single(r => r.id == pact.id).endedWeek, Is.Zero, "The row stays open.");
            shed.week++;
            WantsIn(shed, On(shed, Vanessa), dan);
            Assert.That(JoinsACopy(shed, On(shed, Vanessa)), Is.False, "And it never grows again.");
            // At the jury a shed joiner shares nothing with the founders: C5's reading of a member taken out (§14.L.F).
            var juror = shed.Clone();
            juror.Find(will).status = ContestantStatus.Jury;
            foreach (var founder in new[] { dan, janelle })
            {
                Assert.That(Allegiance.JuryLoyalty(juror, will, founder), Is.Zero, founder + ": a member taken out shares no pact.");
                Assert.That(Allegiance.SharedToTheEnd(juror, will, founder), Is.False, founder);
            }

            // The founders' own souring ends it whole.
            var founders = s.Clone();
            Set(founders, dan, janelle, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(founders);
            Assert.That(Find(founders, pact).active, Is.False);
            Assert.That(Find(founders, pact).members, Is.EqualTo(new[] { dan, janelle, will }));
            EpisodeEngine.ReconcileAllianceRows(founders);
            Assert.That(founders.ledger.alliances.Single(r => r.id == pact.id).why, Is.EqualTo("npc/soured"));

            // Once whispered to the player, a joiner's souring ends it whole, and the player's card keeps its three faces and its dated line.
            var whispered = s.Clone();
            var fact = Fact(whispered, Find(whispered, pact));
            fact.visibility = FactVisibility.Whispered;
            Knowledge.AddKnower(whispered, fact, whispered.playerId);
            string line = AllianceLeaks.WhisperLine(whispered, Find(whispered, pact));
            whispered.events.Add(new EpisodeEvent
            {
                sequence = whispered.nextSequence++, week = whispered.week, phase = whispered.phase, kind = StoryLog.Whisper, text = line,
                audienceIds = new List<string> { whispered.playerId },
            });
            Set(whispered, dan, will, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(whispered);
            Assert.That(Find(whispered, pact).active, Is.False, "Known, it ends whole.");
            Assert.That(Find(whispered, pact).members, Is.EqualTo(new[] { dan, janelle, will }));
            var card = AllianceRead.Suspected(whispered).Single(c => c.memberIds.Contains(will));
            Assert.That(card.memberIds, Is.EquivalentTo(new[] { dan, janelle, will }), "Three faces.");
            Assert.That(card.evidence.Any(e => e.week == s.week && e.text == line), Is.True, "Its dated line.");

            // At 4 a trio written by hand ends whole on the same edge.
            var (f, fPact, fDan, fJanelle, fWill) = Hitmen(5122u, Four);
            HandAdd(f, Find(f, fPact), fWill);
            Set(f, fDan, fWill, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(f);
            Assert.That(Find(f, fPact).active, Is.False);
            Assert.That(Find(f, fPact).members, Is.EqualTo(new[] { fDan, fJanelle, fWill }));

            // A story's trio, kept to its members as well as any, ends whole at 5 too: only the houseguests' own shed.
            var story = Season(8, 5129u, Five);
            Plain(story);
            string sDan = On(story, Dan), sJanelle = On(story, Janelle), sWill = On(story, Will);
            var storyTrio = NpcAlliances.FormFromStory(story, new List<string> { sDan, sJanelle, sWill });
            Assert.That(Fact(story, storyTrio).knowers, Is.EquivalentTo(storyTrio.members));
            story.week++;
            Set(story, sDan, sWill, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(story);
            Assert.That(storyTrio.active, Is.False);
            Assert.That(storyTrio.members, Is.EqualTo(new[] { sDan, sJanelle, sWill }));

            // In a four, the latest joiner who soured goes first: Will and Vanessa fall out, Vanessa came last, and once she
            // is out Will is sour with nobody left in it.
            var (t, tPact, tDan, tJanelle, tWill) = Twelve(5130u, Five);
            string tVanessa = On(t, Vanessa);
            Assert.That(Joins(t, tWill), Is.True);
            t.week++;
            WantsIn(t, tVanessa, tDan);
            Assert.That(Joins(t, tVanessa), Is.True);
            t.week++;
            Both(t, tWill, tVanessa, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(t);
            Assert.That(Find(t, tPact).members, Is.EqualTo(new[] { tDan, tJanelle, tWill }));
            Assert.That(Find(t, tPact).active, Is.True);
        }

        [Test]
        public void AGrownPactVotesAsOneBlocWithItsFounderCalling()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5123u, Five);
            string vanessa = On(s, Vanessa), danielle = On(s, Danielle), derrick = On(s, Derrick);
            Assert.That(Joins(s, will), Is.True);
            var grown = Find(s, pact);
            Assert.That(EpisodeEngine.Founder(grown), Is.EqualTo(dan));
            Assert.That(AllianceLeaks.Odds(s, grown), Is.EqualTo(AllianceLeaks.BaseOdds + AllianceLeaks.PerMemberOdds).Within(Exact));
            Assert.That(AllianceLeaks.Odds(s, grown), Is.EqualTo(0.08).Within(Exact), "A trio's odds.");

            // The vote: Derrick's block, Vanessa and Danielle up. The founder calls, and all three are told.
            s.hohId = derrick;
            s.nominees = new List<string> { vanessa, danielle };
            var snapshot = WebVotingBlocs.FromNative(s);
            var bloc = snapshot.alliances.Single(a => a.id == pact.id);
            Assert.That(bloc.founderId, Is.EqualTo(dan));
            Assert.That(bloc.members, Is.EquivalentTo(new[] { dan, janelle, will }));
            var result = WebVotingBlocs.Resolve(snapshot, (_, __) => 50, () => 0).Single(r => r.allianceId == pact.id);
            Assert.That(result.shotCallerId, Is.EqualTo(dan));
            Assert.That(result.directives.Select(d => d.voterId), Is.EquivalentTo(new[] { dan, janelle, will }));

            // The threat a pact carries: four a head for whoever knows of it, and nothing for anybody else.
            Assert.That(ThreatAssessment.Assess(s, vanessa, will).Alliance, Is.Zero, "Vanessa knows of no pact.");
            Assert.That(ThreatAssessment.Assess(s, dan, will).Alliance, Is.EqualTo(12));
            Knowledge.AddKnower(s, Fact(s, grown), s.playerId);
            Assert.That(ThreatAssessment.Assess(s, s.playerId, will).Alliance, Is.EqualTo(12), "The player, a knower, reads twelve.");
            Assert.That(ThreatAssessment.Assess(s, vanessa, will).Alliance, Is.Zero);
        }

        [Test]
        public void TheWeeklyPassAndTheRuleAloneJoinAsTheBeatDoes()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5124u, Five);
            // Warm enough on every edge that a rumour or a hunt elsewhere in the week cannot spoil the join.
            Both(s, dan, will, 30);
            Set(s, will, janelle, 90);
            Set(s, janelle, will, 60);
            var at = NextWeek(s, will);
            var expected = new[] { dan, janelle, will };

            var beat = at.Clone();
            var act = Beat(beat, will, "beat");
            Assert.That((act?.kind, act?.actorId, act?.partnerId), Is.EqualTo((NpcActKinds.Pact, will, janelle)));
            Assert.That(Find(beat, pact).members, Is.EqualTo(expected), "The beat.");

            var pass = at.Clone();
            NpcSocialActions.Settle(pass);
            Assert.That(Find(pass, pact).members, Is.EqualTo(expected), "The weekly pass.");

            var rule = at.Clone();
            NpcAlliances.Settle(rule);
            Assert.That(Find(rule, pact).members, Is.EqualTo(expected), "The rule alone.");
            foreach (var path in new[] { beat, pass, rule })
                Assert.That(NpcAlliances.NpcOnlyPacts(path).Select(p => p.id), Is.EqualTo(new[] { pact.id }));

            // A birth comes first (§14.L.D.3): Will could join Dan and Janelle's or pair with a free outsider who wants him
            // as much as he wants her, and on all three paths he pairs. The outsider comes after him in the cast, so the
            // passes reach him first; the house holds one pact and has room for one more.
            var (b, bPact, bDan, bJanelle, bWill) = Hitmen(5134u, Five);
            string free = b.contestants.SkipWhile(c => c.id != bWill).Skip(1).Select(c => c.id)
                .FirstOrDefault(id => id != bDan && id != bJanelle && id != b.playerId && !KnownBlocs.InLivePair(b, id));
            Assert.That(free, Is.Not.Null, "A free outsider after Will in the cast.");
            Both(b, bDan, bWill, 30);
            Set(b, bWill, bJanelle, 90);
            Set(b, bJanelle, bWill, 60);
            Both(b, bWill, free, 90);
            Assert.That((NpcAlliances.PactCap(b), NpcAlliances.NpcOnlyPacts(b).Count), Is.EqualTo((2, 1)), "Room for one more pact.");
            var bAt = NextWeek(b, bWill);
            Assert.That(JoinsACopy(bAt, bWill), Is.True, "He could join.");

            var birthBeat = bAt.Clone();
            var birth = Beat(birthBeat, bWill, "birth");
            Assert.That((birth?.kind, birth?.actorId, birth?.partnerId), Is.EqualTo((NpcActKinds.Pact, bWill, free)), "His act names the outsider, not Janelle.");
            var birthPass = bAt.Clone();
            NpcSocialActions.Settle(birthPass);
            var birthRule = bAt.Clone();
            NpcAlliances.Settle(birthRule);
            foreach (var (path, name) in new[] { (birthBeat, "the beat"), (birthPass, "the weekly pass"), (birthRule, "the rule alone") })
            {
                Assert.That(Find(path, bPact).members, Is.EqualTo(new[] { bDan, bJanelle }), name + ": the pair stays a pair.");
                var pacts = NpcAlliances.NpcOnlyPacts(path);
                Assert.That(pacts, Has.Count.EqualTo(2), name + ": one pact more.");
                Assert.That(pacts.Single(p => p.id != bPact.id).members, Is.EquivalentTo(new[] { bWill, free }), name + ": Will and the outsider pair.");
            }
        }

        [Test]
        public void JoinsAreDeterministicAndReplayAcrossASave()
        {
            var (s, pact, dan, janelle, will) = Hitmen(5125u, Five);
            var at = NextWeek(s, will);
            var first = at.Clone();
            var second = at.Clone();
            var loaded = AlumniHistoriesTests.Load(AlumniHistoriesTests.Saved(at));
            var acts = new[] { Beat(first, will, "a"), Beat(second, will, "a"), Beat(loaded, will, "a") };
            Assert.That(acts.Select(a => (a?.kind, a?.actorId, a?.partnerId)).Distinct().ToList(),
                Is.EqualTo(new[] { ((string)NpcActKinds.Pact, will, janelle) }));
            Assert.That(AlumniHistoriesTests.Json(second), Is.EqualTo(AlumniHistoriesTests.Json(first)), "The same house joins the same way.");
            Assert.That(AlumniHistoriesTests.Json(loaded), Is.EqualTo(AlumniHistoriesTests.Json(first)), "And across a save.");

            // A grown pact saves and loads as it stands, and the next week reads it the same.
            var reloaded = AlumniHistoriesTests.Load(AlumniHistoriesTests.Saved(first));
            Assert.That(Find(reloaded, pact).members, Is.EqualTo(new[] { dan, janelle, will }));
            reloaded.week++;
            first.week++;
            string vanessa = On(first, Vanessa);
            WantsIn(first, vanessa, dan);
            WantsIn(reloaded, vanessa, dan);
            Assert.That(JoinsACopy(reloaded, vanessa), Is.EqualTo(JoinsACopy(first, vanessa)));
            NpcAlliances.Dissolve(first);
            NpcAlliances.Dissolve(reloaded);
            Assert.That(AlumniHistoriesTests.Json(reloaded), Is.EqualTo(AlumniHistoriesTests.Json(first)));
        }

        [Test]
        public void AGrownPactNeverHuntsOrTargetsItsOwnMember()
        {
            // The recruit has two wins, and the founder who did not sponsor them is at 10 toward them.
            var (s, pact, dan, janelle, will) = Hitmen(5126u, Five);
            Set(s, dan, will, 10);
            Assert.That(Joins(s, will, out string sponsor), Is.True);
            Assert.That(sponsor, Is.EqualTo(janelle));
            s.Find(will).hohWins = 2;
            Assert.That(NpcDeals.CommonThreat(s, dan, janelle), Is.Null, "At 5 a pact-mate of both is passed over.");
            Assert.That(NpcDeals.CommonThreat(s, janelle, dan), Is.Null);
            foreach (var member in new[] { dan, janelle, will })
                Assert.That(Agenda(s, member).targetId, Is.Not.EqualTo(will), member + ": no hunt names Will.");
            Assert.That(NpcDeals.Offer(s, dan, janelle), Is.Not.EqualTo(DealKind.TargetAgreement), "No target agreement against him.");
            Assert.That(NpcDeals.Offer(s, janelle, dan), Is.Not.EqualTo(DealKind.TargetAgreement));

            // Only a pact-mate of both is passed over (§14.L.F). A threat from outside the pact is still theirs to hunt.
            var outside = s.Clone();
            string vanessa = On(outside, Vanessa);
            outside.Find(vanessa).hohWins = 2;
            Set(outside, dan, vanessa, 10);
            Assert.That(NpcDeals.CommonThreat(outside, dan, janelle), Is.EqualTo(vanessa), "Vanessa, outside the pact, is still their threat.");
            Assert.That(Agenda(outside, dan), Is.EqualTo((Agendas.Hunt, janelle, vanessa)));
            // A pact that has ended shields nobody: the founders fall out, it ends whole, and Will is their threat again.
            var ended = s.Clone();
            Set(ended, dan, janelle, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(ended);
            Assert.That(Find(ended, pact).active, Is.False);
            Assert.That(NpcDeals.CommonThreat(ended, dan, janelle), Is.EqualTo(will), "No longer pact-mates.");
            // A pact-mate of one is still a threat to that one and an outsider: Janelle, with two wins, to Dan and Vanessa.
            var one = s.Clone();
            one.Find(will).hohWins = 0;
            one.Find(janelle).hohWins = 2;
            Set(one, dan, janelle, 10);
            Assert.That(NpcDeals.CommonThreat(one, dan, On(one, Vanessa)), Is.EqualTo(janelle), "A pact-mate of Dan's alone.");

            // The player in the pact with both is read as at 4: still their common threat.
            var player = s.Clone();
            player.Find(will).hohWins = 0;
            player.Find(player.playerId).hohWins = 2;
            Set(player, dan, player.playerId, 10);
            HandAdd(player, Find(player, pact), player.playerId);
            Assert.That(NpcDeals.CommonThreat(player, dan, janelle), Is.EqualTo(player.playerId));
            Assert.That(NpcDeals.CommonThreat(Held(player, Four), dan, janelle), Is.EqualTo(player.playerId));

            // At 4 a trio written by hand still names its member, as it always did.
            var (f, fPact, fDan, fJanelle, fWill) = Hitmen(5127u, Four);
            HandAdd(f, Find(f, fPact), fWill);
            f.Find(fWill).hohWins = 2;
            Set(f, fDan, fWill, 10);
            Assert.That(NpcDeals.CommonThreat(f, fDan, fJanelle), Is.EqualTo(fWill));
            Assert.That(Agenda(f, fDan), Is.EqualTo((Agendas.Hunt, fJanelle, fWill)));
            Assert.That(NpcDeals.Offer(f, fDan, fJanelle), Is.EqualTo(DealKind.TargetAgreement));

            // A story's trio keeps that reading at every value: it is the story session's to change, under its own gate.
            var story = Season(8, 5131u, Five);
            Plain(story);
            string sDan = On(story, Dan), sJanelle = On(story, Janelle), sWill = On(story, Will);
            Both(story, sDan, sJanelle, 90);
            Assert.That(NpcAlliances.FormFromStory(story, new List<string> { sDan, sJanelle, sWill }), Is.Not.Null);
            story.Find(sWill).hohWins = 2;
            Set(story, sDan, sWill, 10);
            Assert.That(NpcDeals.CommonThreat(story, sDan, sJanelle), Is.EqualTo(sWill));

            // The bloc hunt's route at 5: a pact of houseguests holding Cody, as a bring-in can leave one. The hunt passes its
            // own member over for the next of the known pairs, never Cody, whose risk is the highest.
            var t = Season(12, 5128u, Five);
            Plain(t);
            string tDan = On(t, Dan), tJanelle = On(t, Janelle), tCody = On(t, Cody), tDerrick = On(t, Derrick), tTyler = On(t, Tyler);
            var hub = Pair(t, tDan, tJanelle);
            HandAdd(t, hub, tCody);
            Set(t, tJanelle, tCody, 30);
            Set(t, tJanelle, tDerrick, 30);
            Set(t, tJanelle, tTyler, 30);
            Assert.That(KnownBlocs.RiskFor(t, tDan, tCody), Is.GreaterThan(KnownBlocs.RiskFor(t, tDan, tDerrick)), "Cody is the hub.");
            Assert.That(KnownBlocs.CounterTarget(t, tDan, tJanelle), Is.EqualTo(tDerrick), "Never its own member.");
            Assert.That(Agenda(t, tDan), Is.EqualTo((Agendas.Hunt, tJanelle, tDerrick)));
            Assert.That(Agenda(t, tJanelle).targetId, Is.Not.EqualTo(tCody));
        }

        // ------------------------------------------------------------------ found in S3b's review

        [Test]
        public void APactThatHasLostAMemberNeverGrowsAgain()
        {
            // The eight-house: Will joins, then Dan goes to the jury. Two are in the house, below the bound, and the fact is still
            // known only to the roster - but Dan is on that roster, and every jury reader matches juror and finalist by it.
            var (s, pact, dan, janelle, will) = Hitmen(5135u, Five);
            string vanessa = On(s, Vanessa);
            Assert.That(Joins(s, will), Is.True);
            s.week++;
            s.Find(dan).status = ContestantStatus.Jury;
            WantsIn(s, vanessa, janelle);
            Assert.That((EpisodeEngine.InTheHouse(s, Find(s, pact)), KnownBlocs.CounterPactBound(s)), Is.EqualTo((2, 3)), "Below the bound.");
            Assert.That(Fact(s, pact).knowers, Is.EquivalentTo(Find(s, pact).members), "Still known to its roster alone.");
            Assert.That(JoinsACopy(s, vanessa), Is.False, "A pact that has lost a member never grows again.");
            Assert.That(Joins(s, vanessa), Is.False);
            Assert.That(Find(s, pact).members, Is.EqualTo(new[] { dan, janelle, will }));
            // So the juror who left reads Vanessa as a stranger, as anybody they never shared a pact with.
            Assert.That(Allegiance.JuryLoyalty(s, dan, vanessa), Is.Zero);
            Assert.That(Allegiance.SharedToTheEnd(s, dan, vanessa), Is.False);
            Assert.That(WebJuryVoting.AllianceLoyalty(s, dan, vanessa), Is.Zero);
            Knowledge.AddKnower(s, Fact(s, pact), s.playerId);
            Assert.That(FinalistRead.Lean(s, dan, vanessa)?.reason, Is.Not.EqualTo("shared an alliance"));

            // The twelve-house, bound four: a four loses Vanessa to an eviction, and the three in the house take nobody.
            var (t, tPact, tDan, tJanelle, tWill) = Twelve(5136u, Five);
            string tVanessa = On(t, Vanessa), tChelsie = On(t, Chelsie);
            Assert.That(Joins(t, tWill), Is.True);
            t.week++;
            WantsIn(t, tVanessa, tDan);
            Assert.That(Joins(t, tVanessa), Is.True);
            t.week++;
            t.Find(tVanessa).status = ContestantStatus.Evicted;
            WantsIn(t, tChelsie, tJanelle);
            Assert.That((EpisodeEngine.InTheHouse(t, Find(t, tPact)), KnownBlocs.CounterPactBound(t)), Is.EqualTo((3, 4)));
            Assert.That(JoinsACopy(t, tChelsie), Is.False, "Three in the house, below the bound: still nobody.");
            // The shed reads the joiners in the house only: Vanessa, gone and soured with Dan, stays on the roster, and it stands.
            var sour = t.Clone();
            Both(sour, tVanessa, tDan, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(sour);
            Assert.That(Find(sour, tPact).members, Is.EqualTo(new[] { tDan, tJanelle, tWill, tVanessa }), "Nobody is shed.");
            Assert.That(Find(sour, tPact).active, Is.True);
        }

        [Test]
        public void NoJoinWhileTwoOfThePactToBeHaveAgreedToTargetTheThird()
        {
            // Will has two wins and Dan reads him at 10. As the week turns, before the pact window, the founders agree to target
            // him (NpcDeals.Settle), and the agreement runs to the end of the week: a join would leave it standing against a
            // pact-mate of both, pulling a founder's nomination toward him.
            var (s, pact, dan, janelle, will) = Hitmen(5137u, Five);
            s.Find(will).hohWins = 2;
            Set(s, dan, will, 10);
            Assert.That(NpcDeals.CommonThreat(s, dan, janelle), Is.EqualTo(will), "Not yet a pact-mate: their threat.");
            Assert.That(JoinsACopy(s, will), Is.True, "Without the agreement he would join.");
            NpcDeals.Settle(s);
            var agreements = CommitmentReferences.RawDeals(s).Where(d => d.status == DealStatus.Active && d.type == DealKind.TargetAgreement
                && d.targetId == will && new[] { dan, janelle }.Contains(d.proposerId) && new[] { dan, janelle }.Contains(d.recipientId)).ToList();
            Assert.That(agreements, Is.Not.Empty, "The founders agree to target Will.");
            var ids = agreements.Select(d => d.id).ToList();
            Assert.That(agreements.Select(d => d.expiresWeek), Is.All.EqualTo(s.week), "For this week.");
            Assert.That(JoinsACopy(s, will), Is.False, "No join while two of the pact-to-be have agreed to target the third.");

            // The next week it has run out, and with Dan warmer nobody strikes it again: Will joins.
            s.week++;
            Set(s, dan, will, 20);
            NpcDeals.Settle(s);
            Assert.That(CommitmentReferences.RawDeals(s).Where(d => ids.Contains(d.id)).Select(d => d.status), Is.All.EqualTo(DealStatus.Expired));
            Assert.That(CommitmentReferences.RawDeals(s).Any(d => d.status == DealStatus.Active && d.type == DealKind.TargetAgreement && d.targetId == will), Is.False);
            Assert.That(Joins(s, will), Is.True, "Once it has run out, Will joins.");
            Assert.That(Find(s, pact).members, Is.EqualTo(new[] { dan, janelle, will }));

            // Nor while the recruit and a member have agreed to target another member: Janelle has two wins and Dan reads her
            // at 10, and Dan and Will, at 30 to each other, agree to target her before Will would join through her.
            var (o, oPact, oDan, oJanelle, oWill) = Hitmen(5138u, Five);
            o.Find(oJanelle).hohWins = 2;
            Set(o, oDan, oJanelle, 10);
            Both(o, oDan, oWill, 30);
            Assert.That(JoinsACopy(o, oWill), Is.True, "Without the agreement he would join.");
            NpcDeals.Settle(o);
            Assert.That(CommitmentReferences.RawDeals(o).Count(d => d.status == DealStatus.Active && d.type == DealKind.TargetAgreement && d.targetId == oJanelle
                && new[] { oDan, oWill }.Contains(d.proposerId) && new[] { oDan, oWill }.Contains(d.recipientId)), Is.Positive, "Dan and Will agree to target Janelle.");
            Assert.That(JoinsACopy(o, oWill), Is.False, "He would be in a pact with the member he has agreed to target.");
            Assert.That(Find(o, oPact).members, Is.EqualTo(new[] { oDan, oJanelle }));
        }
    }
}
