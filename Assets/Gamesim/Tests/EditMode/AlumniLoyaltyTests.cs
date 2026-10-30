using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Alumni rules 2, <see cref="AlumniRules.Loyalty"/> (ALL-STAR-HISTORIES-PLAN §14.B, slice S1): the owner's direction of
    /// 2026-10-29, inherited loyalty that changes and decays through the house's acts. A past game's receipt counts for a
    /// graded share: this season's permanent wrongs on the holder's own edge and the public nominations of them erode it,
    /// pacts, words and kept deals repair it up to one span, a betrayal ends it for good, and an old grievance heals through
    /// play and never through time. Ballots, grudges and social traffic are never read. An edge with no receipt reads the
    /// ledger's own sum, bit for bit, and nothing here draws or writes. The one write between houseguests is the public
    /// split: one of a shared alliance's two houseguests puts the other up, and the standing the past game added is
    /// written off once. §14.B.3's worked values are asserted exactly, at alumni rules 1 and 2. Seasons at 0 and 1 hear
    /// nothing of any of it.
    ///
    /// <para>Fixtures in the NPC-agency style: ledger entries, power rows and the live block written into an enrolled
    /// season, read through the public readers; the split through <see cref="EpisodeEngine.Apply"/>. Only public members,
    /// so the file compiles in Unity's EditMode assembly and in the dotnet subset.</para>
    /// </summary>
    public sealed class AlumniLoyaltyTests
    {
        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Danielle = "danielle-reyes", Tyler = "tyler-crispen", Chelsie = "chelsie-baham", Rachel = "rachel-reilly",
            Xavier = "xavier-prather";

        private const int One = AlumniRules.Histories, Two = AlumniRules.Loyalty;
        private const double Exact = 1e-9, Shown = 0.005;

        /// <summary>
        /// The shipped All-Stars season held at <paramref name="value"/>: by default this slice's own value,
        /// <see cref="AlumniRules.Loyalty"/>, so a later value's terms never reach these fixtures (§14.I: every behaviour
        /// test names its value).
        /// </summary>
        private static EpisodeState AllStars(int size, uint seed, string player = null, int value = AlumniRules.Loyalty) =>
            AlumniHistoriesTests.AllStars(size, seed, player, value);

        private static string On(EpisodeState s, string templateId) => AlumniHistoriesTests.On(s, templateId);

        /// <summary>The same season held at another alumni rules value: the field alone (§14.G).</summary>
        private static EpisodeState Held(EpisodeState s, int value)
        {
            var held = s.Clone();
            held.alumniRulesVersion = value;
            return held;
        }

        /// <summary>The holder's trust sum about another (<see cref="RelationshipLedger.WeightedImpact"/>).</summary>
        private static double Impact(EpisodeState s, string holderId, string aboutId) => RelationshipLedger.WeightedImpact(s, holderId, aboutId);

        /// <summary>The holder's trust in another: 50 plus a third of the impact (§14.B.3).</summary>
        private static double Trust(EpisodeState s, string holderId, string aboutId) => ThreatAssessment.TrustScore(s, aboutId, holderId);

        private static double Goodwill(EpisodeState s, string holderId, string aboutId) => AlumniLoyalty.GoodwillShare(s, holderId, aboutId);

        private static double Grievance(EpisodeState s, string holderId, string aboutId) => AlumniLoyalty.GrievanceShare(s, holderId, aboutId);

        /// <summary>A permanent wrong on the holder's own edge, as the commitment rules' breach writer records it.</summary>
        private static void Wronged(EpisodeState s, string holderId, string aboutId, double impact, string type = "deal_broken") =>
            RelationshipLedger.RecordOneWay(s, holderId, aboutId, type, impact, "A wrong of " + impact + ".", permanent: true);

        /// <summary>An in-house pact between two: the ledger's permanent alliance-formed, both ways.</summary>
        private static void Allied(EpisodeState s, string a, string b) =>
            RelationshipLedger.Record(s, a, b, "alliance-formed", 30, "They formed a pact.");

        /// <summary>A word given between two: the ledger's permanent promise-made, both ways.</summary>
        private static void Promised(EpisodeState s, string a, string b) =>
            RelationshipLedger.Record(s, a, b, "promise-made", 15, "A word was given.");

        /// <summary>This week's live block: the Head of Household and their nominations, before the veto and the reveal.</summary>
        private static void Block(EpisodeState s, string hohId, params string[] nominees)
        {
            s.hohId = hohId;
            s.nominees = nominees.ToList();
            s.evictionResolved = false;
        }

        /// <summary>A week of an ordinary ceremony on the record: its Head of Household, the final block, the tally and who went.</summary>
        private static PowerRow Row(int week, string hohId, string[] block, string evicteeId, string savedId = null, string replacementId = null) =>
            new PowerRow
            {
                week = week, hohId = hohId, vetoHolderId = savedId ?? block[0], vetoUsed = savedId != null, savedId = savedId,
                replacementId = replacementId, nominees = block.ToList(), tally = new List<int> { 5, 3 }, evicteeId = evicteeId,
            };

        // ------------------------------------------------------------------ the graded share (§14.B.1-B.3)

        [Test]
        public void AWeekOnePastGameCountsInFull()
        {
            var s = AllStars(12, 1101u);
            string derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler), janelle = On(s, Janelle), danielle = On(s, Danielle), will = On(s, Will);
            foreach (int value in new[] { One, Two })
            {
                var held = Held(s, value);
                Assert.That(Impact(held, cody, derrick), Is.EqualTo(33).Within(Exact), value + ": the Hitmen, the past alliance and the handshake (§14.B.3).");
                Assert.That(Trust(held, cody, derrick), Is.EqualTo(61.00).Within(Shown));
                Assert.That(Impact(held, derrick, cody), Is.EqualTo(33).Within(Exact));
                Assert.That(Impact(held, tyler, cody), Is.EqualTo(15).Within(Exact), value + ": the Committee's receipt alone.");
                Assert.That(Impact(held, janelle, tyler), Is.EqualTo(-12.5).Within(Exact));
                Assert.That(Impact(held, janelle, danielle), Is.EqualTo(-12.5).Within(Exact));
                Assert.That(Impact(held, danielle, will), Is.EqualTo(-12.5).Within(Exact));
                Assert.That(Goodwill(held, cody, derrick), Is.EqualTo(1), value + ": nothing has happened in the house yet.");
                Assert.That(Grievance(held, janelle, tyler), Is.EqualTo(1));
            }
        }

        [Test]
        public void ABetrayalEndsInheritedGoodwillAndNoRepairUndoesIt()
        {
            var s = AllStars(8, 1102u);
            string derrick = On(s, Derrick), cody = On(s, Cody);
            // The final two broken at the final choice (-45), long after the handshake has faded (gone by week 7); a broken
            // vote promise (-31); a High deal broken (-30), on the line.
            foreach (var (wrong, week, handshake, trust) in new[] { (-45.0, 7, 0.0, 35.00), (-31.0, 1, 18.0, 45.67), (-30.0, 1, 18.0, 46.00) })
            {
                var broken = s.Clone();
                broken.week = week;
                Wronged(broken, cody, derrick, wrong);
                Assert.That(Goodwill(broken, cody, derrick), Is.Zero, wrong + " is a betrayal.");
                Assert.That(Impact(broken, cody, derrick), Is.EqualTo(handshake + wrong).Within(Exact), wrong + ": the receipt no longer counts.");
                Assert.That(Trust(broken, cody, derrick), Is.EqualTo(trust).Within(Shown));
                Assert.That(Impact(Held(broken, One), cody, derrick), Is.EqualTo(handshake + wrong).Within(Exact), wrong + ": as alumni rules 1 reads it.");
                var repaired = broken.Clone();
                Allied(repaired, cody, derrick);
                Promised(repaired, cody, derrick);
                Assert.That(Goodwill(repaired, cody, derrick), Is.Zero, wrong + ": no pact and no word undoes a betrayal.");
                Assert.That(Impact(repaired, cody, derrick), Is.EqualTo(handshake + wrong + 45).Within(Exact), "The pact and the word count for themselves.");
            }
            // Just inside the line, a wrong is a lesser one, and repair can work on it.
            var lesser = s.Clone();
            Wronged(lesser, cody, derrick, -29);
            Assert.That(Goodwill(lesser, cody, derrick), Is.EqualTo(1 / 30.0).Within(Exact));
            Allied(lesser, cody, derrick);
            Assert.That(Goodwill(lesser, cody, derrick), Is.EqualTo(1 - 14 / 30.0).Within(Exact), "29 less half of 30.");
        }

        [Test]
        public void ALesserWrongErodesGoodwillInProportion()
        {
            var s = AllStars(8, 1103u);
            string derrick = On(s, Derrick), cody = On(s, Cody);
            // A Low deal broken, H1b's bare test entry, an information promise broken and a Medium deal broken (§14.B.1, loyalty 5).
            foreach (var (wrong, type, share, trust, ruleOne) in new[]
                     {
                         (-15.0, "deal_broken", 0.5, 53.50, 51.00), (-20.0, "deal_broken", 1 / 3.0, 51.00, 49.33),
                         (-22.0, "promise-broken", 8 / 30.0, 50.00, 48.67), (-22.5, "deal_broken", 0.25, 49.75, 48.50),
                     })
            {
                var broken = s.Clone();
                Wronged(broken, cody, derrick, wrong, type);
                Assert.That(Goodwill(broken, cody, derrick), Is.EqualTo(share).Within(Exact), wrong + ": one less the wrong, in spans of 30.");
                Assert.That(Impact(broken, cody, derrick), Is.EqualTo(15 * share + 18 + wrong).Within(Exact));
                Assert.That(Trust(broken, cody, derrick), Is.EqualTo(trust).Within(Shown), wrong + " (§14.B.3)");
                var one = Held(broken, One);
                Assert.That(Impact(one, cody, derrick), Is.EqualTo(18 + wrong).Within(Exact), wrong + ": alumni rules 1 drops the receipt whole.");
                Assert.That(Trust(one, cody, derrick), Is.EqualTo(ruleOne).Within(Shown));
            }
            // Lesser wrongs add up: two Low deals broken are a span, so the share is gone, but it is no betrayal and play can mend it.
            var twice = s.Clone();
            Wronged(twice, cody, derrick, -15);
            Wronged(twice, cody, derrick, -15);
            Assert.That(Goodwill(twice, cody, derrick), Is.Zero);
            Allied(twice, cody, derrick);
            Assert.That(Goodwill(twice, cody, derrick), Is.EqualTo(0.5).Within(Exact), "30 less half of 30.");
            // A wrong that fades, or the breaker's own zero-weight record, erodes nothing.
            var faded = s.Clone();
            RelationshipLedger.RecordOneWay(faded, cody, derrick, "deal_broken", -20, "Recorded off the commitment rules.");
            RelationshipLedger.RecordOneWay(faded, cody, derrick, "deal_broken", 0, "The breaker's own record.", permanent: true);
            Assert.That(Goodwill(faded, cody, derrick), Is.EqualTo(1));
            Assert.That(Impact(faded, cody, derrick), Is.EqualTo(33 - 20).Within(Exact));
        }

        [Test]
        public void ANominationErodesGoodwillFromThePublicRecordAndReturnsAsItAges()
        {
            var s = AllStars(12, 1104u);
            string cody = On(s, Cody), tyler = On(s, Tyler), chelsie = On(s, Chelsie), rachel = On(s, Rachel), xavier = On(s, Xavier);
            s.week = 3;

            // The first nomination, from the moment the names are said: the live block.
            var named = s.Clone();
            Block(named, cody, tyler, chelsie);
            Assert.That(AlumniLoyalty.PutUpWeeks(named, cody, tyler, includeThisWeek: true), Is.EqualTo(new[] { 3 }));
            Assert.That(AlumniLoyalty.PutUpWeeks(named, cody, tyler, includeThisWeek: false), Is.Empty, "Earlier weeks only.");
            Assert.That(Goodwill(named, tyler, cody), Is.Zero, "35 at the act: more than a span.");
            Assert.That(Impact(named, tyler, cody), Is.EqualTo(0).Within(Exact));
            Assert.That(Trust(named, tyler, cody), Is.EqualTo(50.00).Within(Shown), "§14.B.3: Cody as Head of Household nominates Tyler in week 3.");
            Assert.That(Impact(Held(named, One), tyler, cody), Is.EqualTo(15).Within(Exact), "Alumni rules 1: a nomination writes nothing on the ledger.");
            Assert.That(Trust(Held(named, One), tyler, cody), Is.EqualTo(55.00).Within(Shown));
            Assert.That(Goodwill(named, cody, tyler), Is.EqualTo(1), "The nominator's own view is untouched.");

            // A nominee then saved was put up all the same, and so was the replacement. A week counts once.
            var saved = s.Clone();
            Block(saved, cody, chelsie, rachel);
            saved.ledger.power.Add(Row(3, cody, new[] { chelsie, rachel }, null, savedId: tyler, replacementId: rachel));
            saved.ledger.power.Last().tally.Clear();
            saved.ledger.power.Last().evicteeId = null;
            Assert.That(AlumniLoyalty.PutUpWeeks(saved, cody, tyler, includeThisWeek: true), Is.EqualTo(new[] { 3 }), "Saved by the veto, put up all the same.");
            Assert.That(AlumniLoyalty.PutUpWeeks(saved, cody, tyler, includeThisWeek: false), Is.Empty, "This week's row is this week's: earlier weeks only.");
            Assert.That(AlumniLoyalty.PublicSplitWeek(saved, cody, tyler, includeThisWeek: false), Is.Zero);
            Assert.That(Goodwill(saved, tyler, cody), Is.Zero);
            var replaced = s.Clone();
            Block(replaced, cody, rachel, tyler);
            replaced.ledger.power.Add(Row(3, cody, new[] { rachel, tyler }, null, savedId: chelsie, replacementId: tyler));
            replaced.ledger.power.Last().tally.Clear();
            replaced.ledger.power.Last().evicteeId = null;
            Assert.That(AlumniLoyalty.PutUpWeeks(replaced, cody, tyler, includeThisWeek: true), Is.EqualTo(new[] { 3 }), "The replacement.");
            Assert.That(Goodwill(replaced, tyler, cody), Is.Zero);
            // Counted once: with a pact the share is a third; counted twice it would be nothing.
            Allied(replaced, tyler, cody);
            Assert.That(Goodwill(replaced, tyler, cody), Is.EqualTo(1 / 3.0).Within(Exact), "35 less half of 30, once.");

            // Week by week, the nomination ages by one and the goodwill returns once the erosion is under a span.
            var aged = s.Clone();
            aged.ledger.power.Add(Row(3, cody, new[] { tyler, chelsie }, chelsie));
            for (int week = 3; week <= 40; week++)
            {
                aged.week = week;
                double erosion = Math.Max(0, 35 - (week - 3));
                double share = Math.Max(0, Math.Min(1, 1 - erosion / 30));
                Assert.That(Goodwill(aged, tyler, cody), Is.EqualTo(share).Within(Exact), "week " + week);
                Assert.That(Impact(aged, tyler, cody), Is.EqualTo(15 * share).Within(Exact), "week " + week);
                Assert.That(Impact(Held(aged, One), tyler, cody), Is.EqualTo(15).Within(Exact), "week " + week + " at alumni rules 1");
            }
            aged.week = 9;
            Assert.That(Goodwill(aged, tyler, cody), Is.EqualTo(1 / 30.0).Within(Exact), "§14.B.3: week 9 after that nomination, 29 left.");
            Assert.That(Trust(aged, tyler, cody), Is.EqualTo(50.17).Within(Shown));
            aged.week = 10;
            Assert.That(Goodwill(aged, tyler, cody), Is.EqualTo(2 / 30.0).Within(Exact), "Then a thirtieth more a week.");

            // Two weeks put up are two nominations.
            var again = s.Clone();
            again.week = 5;
            again.ledger.power.Add(Row(3, cody, new[] { tyler, chelsie }, chelsie));
            Block(again, cody, tyler, xavier);
            Assert.That(AlumniLoyalty.PutUpWeeks(again, cody, tyler, includeThisWeek: true), Is.EqualTo(new[] { 3, 5 }));
            Allied(again, tyler, cody);
            Assert.That(Goodwill(again, tyler, cody), Is.Zero, "33 and 35 against half of 30.");

            // The final eviction's row is the last Head of Household's choice, not a nomination: it never counts.
            var final = s.Clone();
            final.ledger.power.Add(new PowerRow { week = 3, hohId = cody, nominees = new List<string> { tyler, rachel }, evicteeId = rachel });
            Assert.That(AlumniLoyalty.PutUpWeeks(final, cody, tyler, includeThisWeek: true), Is.Empty);
            Assert.That(Goodwill(final, tyler, cody), Is.EqualTo(1));
            // Somebody else's nomination of Tyler is not Cody's act.
            var other = s.Clone();
            Block(other, chelsie, tyler, rachel);
            Assert.That(Goodwill(other, tyler, cody), Is.EqualTo(1));
        }

        [Test]
        public void ABallotNeverErodesGoodwill()
        {
            var s = AllStars(12, 1105u);
            string cody = On(s, Cody), tyler = On(s, Tyler), chelsie = On(s, Chelsie), rachel = On(s, Rachel);
            s.week = 3;
            double share = Goodwill(s, tyler, cody), impact = Impact(s, tyler, cody);
            Assert.That(share, Is.EqualTo(1));
            // Tyler on the block in week 2 under somebody else, and Cody's ballot against him; an ally's vote against him
            // and a flip against him as Head of Household, written to the grudge store at the reveal (48 and 40).
            s.ledger.power.Add(Row(2, chelsie, new[] { tyler, rachel }, rachel));
            s.votes.Add(new VoteState { voterId = cody, targetId = tyler, reason = "A ballot." });
            Grudges.Add(s, tyler, cody, 48, GrudgeCauses.VotedAgainst);
            Grudges.Add(s, tyler, cody, 40, GrudgeCauses.VotedAgainst);
            Assert.That(Grudges.Severity(s, tyler, cody), Is.GreaterThan(48), "The grudges are there.");
            Assert.That(Goodwill(s, tyler, cody), Is.EqualTo(share), "A ballot and a grudge are not the public record of a ceremony.");
            Assert.That(Impact(s, tyler, cody), Is.EqualTo(impact).Within(0));
        }

        [Test]
        public void AnInHousePactShieldsInheritedGoodwill()
        {
            // The Committee: a nomination in week 3, with a pact in the house.
            var committee = AllStars(12, 1106u);
            string cody = On(committee, Cody), tyler = On(committee, Tyler), chelsie = On(committee, Chelsie);
            committee.week = 3;
            Block(committee, cody, tyler, chelsie);
            Allied(committee, tyler, cody);
            Assert.That(Goodwill(committee, tyler, cody), Is.EqualTo(1 / 3.0).Within(Exact), "35 less half of 30, in spans.");
            Assert.That(Impact(committee, tyler, cody), Is.EqualTo(35).Within(Exact));
            Assert.That(Trust(committee, tyler, cody), Is.EqualTo(61.67).Within(Shown), "§14.B.3");
            Assert.That(Impact(Held(committee, One), tyler, cody), Is.EqualTo(45).Within(Exact));
            Assert.That(Trust(Held(committee, One), tyler, cody), Is.EqualTo(65.00).Within(Shown));

            // The Hitmen: a Medium deal broken, with a pact in the house.
            var hitmen = AllStars(8, 1106u);
            string derrick = On(hitmen, Derrick), hitman = On(hitmen, Cody);
            Wronged(hitmen, hitman, derrick, -22.5);
            Allied(hitmen, hitman, derrick);
            Assert.That(Goodwill(hitmen, hitman, derrick), Is.EqualTo(0.75).Within(Exact), "22.5 less half of 30.");
            Assert.That(Impact(hitmen, hitman, derrick), Is.EqualTo(36.75).Within(Exact));
            Assert.That(Trust(hitmen, hitman, derrick), Is.EqualTo(62.25).Within(Shown), "§14.B.3");
            Assert.That(Impact(Held(hitmen, One), hitman, derrick), Is.EqualTo(25.5).Within(Exact));
            Assert.That(Trust(Held(hitmen, One), hitman, derrick), Is.EqualTo(58.50).Within(Shown));
        }

        [Test]
        public void RepairIsCappedAtOneSpan()
        {
            var s = AllStars(12, 1107u);
            string cody = On(s, Cody), tyler = On(s, Tyler), chelsie = On(s, Chelsie);
            s.week = 3;
            var words = s.Clone();
            for (int i = 0; i < 4; i++) Promised(words, tyler, cody);
            Assert.That(Goodwill(words, tyler, cody), Is.EqualTo(1));
            Block(words, cody, tyler, chelsie);
            Assert.That(Goodwill(words, tyler, cody), Is.EqualTo(1 / 3.0).Within(Exact), "Four words bank 30, not 60: 35 less half of 30.");
            Assert.That(Impact(words, tyler, cody), Is.EqualTo(65).Within(Exact));
            Assert.That(Trust(words, tyler, cody), Is.EqualTo(71.67).Within(Shown), "§14.B.3");
            Assert.That(Impact(Held(words, One), tyler, cody), Is.EqualTo(75).Within(Exact));
            Assert.That(Trust(Held(words, One), tyler, cody), Is.EqualTo(75.00).Within(Shown));

            // A kept deal repairs at its fading weight: whole for two weeks, then a quarter less a week.
            var kept = s.Clone();
            kept.week = 1;
            RelationshipLedger.Record(kept, tyler, cody, "deal_fulfilled", 20, "They held to their deal.");
            foreach (var (week, weight) in new[] { (3, 1.0), (4, 0.75), (5, 0.5), (6, 0.25), (7, 0.0) })
            {
                kept.week = week;
                Block(kept, cody, tyler, chelsie);
                double erosion = 35 - 0.5 * 20 * weight;
                Assert.That(Goodwill(kept, tyler, cody), Is.EqualTo(Math.Max(0, 1 - erosion / 30)).Within(Exact), "week " + week);
                Assert.That(Impact(kept, tyler, cody), Is.EqualTo(15 * Math.Max(0, 1 - erosion / 30) + 20 * weight).Within(Exact), "week " + week);
            }
        }

        [Test]
        public void AnOldGrievanceHealsByPlayAndNotByTime()
        {
            var s = AllStars(12, 1108u);
            string janelle = On(s, Janelle), tyler = On(s, Tyler), danielle = On(s, Danielle), will = On(s, Will), chelsie = On(s, Chelsie);
            var allied = s.Clone();
            Allied(allied, janelle, tyler);
            Assert.That(Grievance(allied, janelle, tyler), Is.Zero, "A pact heals the old nomination in full.");
            Assert.That(Impact(allied, janelle, tyler), Is.EqualTo(30).Within(Exact));
            Assert.That(Trust(allied, janelle, tyler), Is.EqualTo(60.00).Within(Shown), "§14.B.3");
            Assert.That(Impact(Held(allied, One), janelle, tyler), Is.EqualTo(17.5).Within(Exact));
            Assert.That(Trust(Held(allied, One), janelle, tyler), Is.EqualTo(55.83).Within(Shown));
            Assert.That(Impact(allied, danielle, will), Is.EqualTo(-12.5).Within(Exact), "Only the edge that holds the play.");

            var word = s.Clone();
            Promised(word, janelle, tyler);
            Assert.That(Grievance(word, janelle, tyler), Is.EqualTo(0.5).Within(Exact), "A word heals half.");
            Assert.That(Impact(word, janelle, tyler), Is.EqualTo(8.75).Within(Exact));
            Assert.That(Trust(word, janelle, tyler), Is.EqualTo(52.92).Within(Shown), "§14.B.3");
            Assert.That(Impact(Held(word, One), janelle, tyler), Is.EqualTo(2.5).Within(Exact));
            Assert.That(Trust(Held(word, One), janelle, tyler), Is.EqualTo(50.83).Within(Shown));

            // Time alone heals nothing.
            var later = s.Clone();
            later.week = 40;
            Assert.That(Grievance(later, janelle, tyler), Is.EqualTo(1));
            Assert.That(Impact(later, janelle, tyler), Is.EqualTo(-12.5).Within(Exact));
            // And play set against a new wrong heals nothing: Tyler puts her up again, then they ally.
            var renewed = s.Clone();
            renewed.week = 3;
            Block(renewed, tyler, janelle, chelsie);
            Allied(renewed, janelle, tyler);
            Assert.That(Grievance(renewed, janelle, tyler), Is.EqualTo(1), "30 of repair against 35 of erosion.");
            Assert.That(Impact(renewed, janelle, tyler), Is.EqualTo(-12.5 + 30).Within(Exact));
        }

        [Test]
        public void SocialTrafficNeitherErodesNorRepairsAPastGame()
        {
            var s = AllStars(12, 1109u);
            string derrick = On(s, Derrick), cody = On(s, Cody), janelle = On(s, Janelle), tyler = On(s, Tyler);
            var traffic = new (string type, double impact)[]
            {
                ("talk", -20), ("talk", 12), ("rumor", -15), ("confrontation", -8), ("heard_about_betrayal", -10), ("deal_accepted", 18),
            };
            var cold = s.Clone();
            foreach (var (type, impact) in traffic)
            {
                RelationshipLedger.RecordOneWay(cold, cody, derrick, type, impact, "Traffic: " + type);
                RelationshipLedger.RecordOneWay(cold, janelle, tyler, type, -impact, "Traffic: " + type);
            }
            Assert.That(Goodwill(cold, cody, derrick), Is.EqualTo(1), "Talk, rumours, hunts, confrontations and hearsay fade, and erode nothing.");
            Assert.That(Impact(cold, cody, derrick), Is.EqualTo(33 + traffic.Sum(t => t.impact)).Within(Exact), "They count for themselves.");
            Assert.That(Grievance(cold, janelle, tyler), Is.EqualTo(1), "Nor do warm words repair a past game.");
            Assert.That(Impact(cold, janelle, tyler), Is.EqualTo(-12.5 - traffic.Sum(t => t.impact)).Within(Exact));
        }

        [Test]
        public void OnlyTheHoldersEdgeCounts()
        {
            var s = AllStars(8, 1110u);
            string derrick = On(s, Derrick), cody = On(s, Cody), dan = On(s, Dan);
            var broken = s.Clone();
            Wronged(broken, cody, derrick, -20);
            Assert.That(Goodwill(broken, cody, derrick), Is.EqualTo(1 / 3.0).Within(Exact));
            Assert.That(Goodwill(broken, derrick, cody), Is.EqualTo(1), "A wrong on Cody's edge is Cody's view only.");
            Assert.That(Impact(broken, derrick, cody), Is.EqualTo(33).Within(Exact));
            var named = s.Clone();
            named.week = 3;
            Block(named, derrick, cody, dan);
            Assert.That(Goodwill(named, cody, derrick), Is.Zero, "Derrick put Cody up: Cody's view erodes.");
            Assert.That(Goodwill(named, derrick, cody), Is.EqualTo(1), "Derrick's own view of Cody does not.");
            Assert.That(Impact(named, derrick, cody), Is.EqualTo(33).Within(Exact));

            // An NPC's receipt about the player on a card reads the player's breaches, nominations and pacts; the player's
            // own view holds no receipt and reads as it always did.
            var card = AllStars(8, 1110u, Derrick);
            string player = card.playerId, hitman = On(card, Cody), other = On(card, Dan);
            Assert.That(Impact(card, hitman, player), Is.EqualTo(15).Within(Exact));
            card.week = 3;
            Block(card, player, hitman, other);
            Assert.That(Goodwill(card, hitman, player), Is.Zero, "The player put Cody up.");
            Assert.That(Impact(card, hitman, player), Is.EqualTo(0).Within(Exact));
            Allied(card, player, hitman);
            Assert.That(Goodwill(card, hitman, player), Is.EqualTo(1 / 3.0).Within(Exact), "And the player's pact repairs it.");
            Assert.That(Impact(card, player, hitman), Is.EqualTo(Impact(Held(card, One), player, hitman)).Within(0), "The player's own view.");
            Assert.That(card.relationships.Single(r => r.fromId == player && r.toId == hitman).events.Any(AlumniHistories.IsReceipt), Is.False);
            Wronged(card, hitman, player, -20);
            Assert.That(Goodwill(card, hitman, player), Is.Zero, "A breach by the player on Cody's edge erodes it too: 20 + 35 less 15.");
        }

        [Test]
        public void AnEdgeWithNoReceiptReadsExactlyAsBefore()
        {
            var engine = new EpisodeEngine(AllStars(12, 1111u));
            for (int i = 0; i < 1200 && engine.Snapshot.week < 3; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var s = engine.Snapshot;
            Assert.That(s.week, Is.EqualTo(3));
            // A busy edge of every kind of entry, permanent and fading, between two houseguests with no past game.
            string dan = On(s, Dan), chelsie = On(s, Chelsie);
            Wronged(s, dan, chelsie, -50, "alliance-betrayed");
            Wronged(s, dan, chelsie, -0.1);
            Allied(s, dan, chelsie);
            RelationshipLedger.Record(s, dan, chelsie, "deal_fulfilled", 7.3, "Kept.");
            RelationshipLedger.RecordOneWay(s, dan, chelsie, "talk", 1.0 / 3, "A third.");
            Block(s, dan, chelsie, On(s, Xavier));
            int compared = 0;
            foreach (var edge in s.relationships.Where(r => !r.events.Any(AlumniHistories.IsReceipt)))
            {
                double plain = edge.events.Sum(entry => entry.impactScore * RelationshipLedger.Weight(entry, s.week));
                foreach (int value in new[] { AlumniRules.Off, One, Two })
                {
                    var held = Held(s, value);
                    if (value == AlumniRules.Off) held.alumniCatalogEdition = null;
                    Assert.That(BitConverter.DoubleToInt64Bits(Impact(held, edge.fromId, edge.toId)), Is.EqualTo(BitConverter.DoubleToInt64Bits(plain)),
                        edge.fromId + "->" + edge.toId + " at alumni rules " + value);
                }
                compared++;
            }
            Assert.That(compared, Is.GreaterThan(100), "Every edge of a twelve-house in week three that holds no receipt.");
        }

        [Test]
        public void TheShareDrawsNothingAndWritesNothing()
        {
            var s = AllStars(12, 1112u);
            string cody = On(s, Cody), tyler = On(s, Tyler), derrick = On(s, Derrick), chelsie = On(s, Chelsie);
            s.week = 4;
            s.ledger.power.Add(Row(3, cody, new[] { tyler, chelsie }, chelsie));
            Block(s, derrick, cody, chelsie);
            Wronged(s, cody, derrick, -20);
            Allied(s, tyler, cody);
            string before = AlumniHistoriesTests.Json(s);
            uint random = s.randomState;
            int sequence = s.nextSequence, events = s.events.Count;
            var ids = s.contestants.Select(c => c.id).ToList();
            int nonzero = 0;
            foreach (string a in ids)
                foreach (string b in ids)
                {
                    double share = Goodwill(s, a, b) + Grievance(s, a, b);
                    var weeks = AlumniLoyalty.PutUpWeeks(s, a, b, includeThisWeek: true);
                    int split = AlumniLoyalty.PublicSplitWeek(s, a, b, includeThisWeek: true);
                    double read = Impact(s, a, b) + Trust(s, a, b);
                    if (share < 2 || weeks.Count > 0 || split > 0) nonzero++;
                    Assert.That(double.IsNaN(share + read), Is.False);
                }
            Assert.That(nonzero, Is.GreaterThan(0), "The fixtures are read.");
            Assert.That(s.randomState, Is.EqualTo(random), "Nothing drawn.");
            Assert.That(s.nextSequence, Is.EqualTo(sequence), "Nothing minted.");
            Assert.That(s.events.Count, Is.EqualTo(events), "Nothing logged.");
            Assert.That(AlumniHistoriesTests.Json(s), Is.EqualTo(before), "Nothing written: the state is the same, field for field.");
        }

        // ------------------------------------------------------------------ the public split (§14.B.4)

        /// <summary>A season walked by the engine tests' walker until the predicate holds.</summary>
        private static EpisodeEngine Walk(EpisodeState start, Func<EpisodeState, bool> until)
        {
            var engine = new EpisodeEngine(start);
            for (int i = 0; i < 1500 && !until(engine.Snapshot); i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(until(engine.Snapshot), Is.True, "The walk reached its moment.");
            return engine;
        }

        /// <summary>
        /// The fixture's Head of Household for the week: the crown given to a houseguest, and the pitch cards made for
        /// the one who won it put away (the nominations put them away themselves).
        /// </summary>
        private static void Crown(EpisodeState s, string hohId)
        {
            s.hohId = hohId;
            s.replyCards.RemoveAll(card => card.kind == ReplyCards.Pitch);
        }

        private static bool AtTheNominations(EpisodeState s) => s.phase == EpisodePhase.Nomination && s.nominees.Count == 0;

        private static bool AtTheVeto(EpisodeState s) => s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved;

        private static RelationshipState Edge(EpisodeState s, string from, string to) => s.relationships.Single(r => r.fromId == from && r.toId == to);

        /// <summary>
        /// The window's beats, fired before the ceremony's command (which fires them first itself, and then finds none
        /// left), so that what is measured across the command is the ceremony alone.
        /// </summary>
        private static void BeatsFirst(EpisodeState at) => EpisodeEngine.Close(at);

        /// <summary>
        /// The veto meeting, and the same meeting with the season held at alumni rules 1, from one state. Naming a
        /// replacement moves the pair's standing for its own reasons (the replacement's -20 with the Head of Household
        /// under D2's rules, a word or a deal it breaks), so the split is read as the difference from the control, where
        /// no split is ever written; the two meetings must otherwise agree.
        /// </summary>
        private static (EpisodeState meeting, EpisodeState control) BesideAlumniRulesOne(EpisodeState at)
        {
            var meeting = Ceremony(at.Clone());
            var control = Ceremony(Held(at, One));
            Assert.That(meeting.nominees, Is.EqualTo(control.nominees), "The same block at both values.");
            return (meeting, control);
        }

        /// <summary>The ceremony's command, through <see cref="EpisodeEngine.Apply"/>. Returns the state after it.</summary>
        private static EpisodeState Ceremony(EpisodeState at)
        {
            var result = new EpisodeEngine(at).Apply(EpisodeEngineTests.NextCommand(at));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        [Test]
        public void APublicSplitWritesTheStandingOffOnceAndNeverOnThePlayersEdge()
        {
            foreach (int value in new[] { One, Two })
            {
                // Cody as Head of Household (the fixture), Tyler the one he is least reluctant to name.
                var at = Walk(AllStars(12, 1113u, value: value), AtTheNominations).Snapshot;
                var rivals = at.Clone();
                string cody = On(at, Cody), tyler = On(at, Tyler);
                Crown(at, cody);
                Edge(at, cody, tyler).score = -80;
                BeatsFirst(at);
                double there = Edge(at, cody, tyler).score, back = Edge(at, tyler, cody).score;
                var named = Ceremony(at);
                Assert.That(named.nominees, Does.Contain(tyler), "Cody nominates Tyler.");
                double written = value >= Two ? -AlumniEditions.AllianceStanding : 0;
                Assert.That(Edge(named, cody, tyler).score - there, Is.EqualTo(written).Within(Exact), value + ": the Committee's +10 is written off, Cody's way.");
                Assert.That(Edge(named, tyler, cody).score - back, Is.EqualTo(written).Within(Exact), value + ": and Tyler's.");
                if (value < Two) continue;
                Assert.That(Goodwill(named, tyler, cody), Is.Zero, "And Tyler's inherited goodwill erodes from the public record.");

                // Tyler holds the veto and saves himself; the week is played out, and in week two Tyler, as Head of
                // Household, puts Cody up: the pair has split already, so nothing more is written. Tyler plans a backdoor
                // on Cody (a schemer's move, StoryHooks.NpcBackdoorTarget), so he names two others and Cody goes up as the
                // replacement when a nominee saves themselves.
                var veto = Walk(named, AtTheVeto).Snapshot;
                Assert.That(veto.vetoPlayers, Does.Contain(tyler), "A nominee plays for the veto.");
                veto.vetoHolderId = tyler;
                BeatsFirst(veto);
                var vetoed = Ceremony(veto);
                Assert.That(vetoed.nominees, Does.Not.Contain(tyler));
                Assert.That(AlumniLoyalty.PutUpWeeks(vetoed, cody, tyler, includeThisWeek: true), Is.EqualTo(new[] { 1 }), "Saved by the veto, put up all the same.");
                var reverse = Walk(vetoed, s => s.week == 2 && AtTheNominations(s)).Snapshot;
                Assert.That(reverse.Find(tyler).status == ContestantStatus.Active && reverse.Find(cody).status == ContestantStatus.Active, Is.True);

                // Cody, crowned again in week two, puts Tyler up a second time: the pair split in week one, so nothing
                // more is written, whichever of them names the other.
                var twice = reverse.Clone();
                Crown(twice, cody);
                Edge(twice, cody, tyler).score = -80;
                BeatsFirst(twice);
                var (again, once) = BesideAlumniRulesOne(twice);
                Assert.That(again.nominees, Does.Contain(tyler), "Cody nominates Tyler again.");
                Assert.That(AlumniLoyalty.PutUpWeeks(again, cody, tyler, includeThisWeek: true), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(Edge(again, cody, tyler).score, Is.EqualTo(Edge(once, cody, tyler).score).Within(Exact), "Once a season: no second write, Cody's way.");
                Assert.That(Edge(again, tyler, cody).score, Is.EqualTo(Edge(once, tyler, cody).score).Within(Exact), "Nor Tyler's.");

                Crown(reverse, tyler);
                Edge(reverse, tyler, cody).score = -70;
                Edge(reverse, cody, tyler).score = 0;
                Assert.That(AlumniLoyalty.PublicSplitWeek(reverse, tyler, cody, includeThisWeek: false), Is.EqualTo(1));
                Assert.That(AlumniLoyalty.PublicSplitWeek(reverse, cody, tyler, includeThisWeek: false), Is.EqualTo(1), "Either way round.");
                BeatsFirst(reverse);
                there = Edge(reverse, cody, tyler).score; back = Edge(reverse, tyler, cody).score;
                var plotted = Ceremony(reverse);
                Assert.That(plotted.nominees, Does.Not.Contain(cody), "Tyler names two others, and plans Cody's backdoor.");
                Assert.That(Edge(plotted, cody, tyler).score - there, Is.EqualTo(0).Within(Exact));
                var backdoor = Walk(plotted, AtTheVeto).Snapshot;
                string saves = backdoor.nominees.First(id => id != backdoor.playerId);
                Assert.That(backdoor.vetoPlayers, Does.Contain(saves));
                backdoor.vetoHolderId = saves;
                BeatsFirst(backdoor);
                var (renamed, unsplit) = BesideAlumniRulesOne(backdoor);
                Assert.That(renamed.nominees, Does.Contain(cody), "Tyler puts Cody up as the replacement.");
                Assert.That(AlumniLoyalty.PutUpWeeks(renamed, tyler, cody, includeThisWeek: true), Is.EqualTo(new[] { 2 }));
                Assert.That(Edge(renamed, cody, tyler).score, Is.EqualTo(Edge(unsplit, cody, tyler).score).Within(Exact), "Once a season: no second write.");
                Assert.That(Edge(renamed, tyler, cody).score, Is.EqualTo(Edge(unsplit, tyler, cody).score).Within(Exact));
                Assert.That(Goodwill(renamed, cody, tyler), Is.LessThan(1), "Cody's view of Tyler erodes in turn.");

                // A seated pair whose past game was a nomination, not an alliance (a standing of 0), has nothing to write
                // off: Janelle, as Head of Household, puts Tyler up in week one, and no Move touches either edge between
                // them. Both edges are marked untouched before the ceremony, so a write would show as this week's mark.
                string janelle = On(rivals, Janelle);
                Crown(rivals, janelle);
                Edge(rivals, janelle, tyler).score = -80;
                BeatsFirst(rivals);
                Edge(rivals, janelle, tyler).lastInteractionWeek = 0;
                Edge(rivals, tyler, janelle).lastInteractionWeek = 0;
                var (rivalry, rivalryControl) = BesideAlumniRulesOne(rivals);
                Assert.That(rivalry.nominees, Does.Contain(tyler), "Janelle nominates Tyler.");
                foreach (var (holder, about) in new[] { (janelle, tyler), (tyler, janelle) })
                {
                    Assert.That(Edge(rivalry, holder, about).score, Is.EqualTo(Edge(rivalryControl, holder, about).score).Within(Exact), holder + "->" + about + ": no standing to write off.");
                    Assert.That(Edge(rivalry, holder, about).lastInteractionWeek, Is.EqualTo(Edge(rivalryControl, holder, about).lastInteractionWeek),
                        holder + "->" + about + ": and no Move marks it in touch.");
                }
            }

            // The replacement is a nomination too: Cody names Tyler in a saved nominee's place.
            {
                var start = Walk(AllStars(12, 1114u), AtTheNominations).Snapshot;
                string cody = On(start, Cody), tyler = On(start, Tyler);
                Crown(start, cody);
                BeatsFirst(start);
                var noms = Ceremony(start);
                Assert.That(noms.nominees, Does.Not.Contain(tyler), "Cody's first names are two others.");
                var veto = Walk(noms, AtTheVeto).Snapshot;
                string self = veto.nominees.First(id => id != veto.playerId);
                Assert.That(veto.vetoPlayers, Does.Contain(self));
                veto.vetoHolderId = self;
                Edge(veto, cody, tyler).score = -40;
                Edge(veto, tyler, cody).score = 0;
                BeatsFirst(veto);
                var (replaced, unsplit) = BesideAlumniRulesOne(veto);
                Assert.That(replaced.nominees, Does.Contain(tyler), "Tyler is the replacement.");
                Assert.That(Edge(replaced, cody, tyler).score - Edge(unsplit, cody, tyler).score, Is.EqualTo(-AlumniEditions.AllianceStanding).Within(Exact),
                    "The replacement splits the pair: the Committee's +10 is written off, Cody's way.");
                Assert.That(Edge(replaced, tyler, cody).score - Edge(unsplit, tyler, cody).score, Is.EqualTo(-AlumniEditions.AllianceStanding).Within(Exact), "And Tyler's.");
            }

            // The player on Tyler's card: Cody puts the player up, and nothing is written on the player's edge, either way.
            var card = Walk(AllStars(12, 1115u, Tyler), AtTheNominations).Snapshot;
            var crowned = card.Clone();
            string player = card.playerId, codyNpc = On(card, Cody);
            Crown(card, codyNpc);
            Edge(card, codyNpc, player).score = -80;
            BeatsFirst(card);
            double codysView = Edge(card, codyNpc, player).score;
            var playerEdges = card.relationships.Where(r => r.fromId == player || r.toId == player)
                .ToDictionary(r => r.fromId + ">" + r.toId, r => (r.score, r.lastInteractionWeek));
            var put = Ceremony(card);
            Assert.That(put.nominees, Does.Contain(player), "Cody nominates the player on Tyler's card.");
            Assert.That(Edge(put, codyNpc, player).score, Is.EqualTo(codysView).Within(Exact), "No standing is written off with the player (D-H3).");
            Assert.That(Edge(put, player, codyNpc).score, Is.EqualTo(playerEdges[player + ">" + codyNpc].score).Within(Exact), "The player's own view is theirs.");
            Assert.That(Edge(put, player, codyNpc).lastInteractionWeek, Is.EqualTo(playerEdges[player + ">" + codyNpc].lastInteractionWeek), "And no Move marks it in touch.");
            Assert.That(Goodwill(put, codyNpc, player), Is.EqualTo(1), "Cody's nomination of the player is no act of the player's: his receipt about them stands.");

            // And the other way: the player on Tyler's card, as Head of Household, puts Cody up. Nothing is written off on
            // the player's edges either (D-H3); both read as the same nominations do with the season held at alumni rules 1.
            Crown(crowned, player);
            BeatsFirst(crowned);
            string pawn = EpisodeEngine.NominationCandidates(crowned).First(c => c.id != codyNpc).id;
            var mine = PlayerNominates(crowned.Clone(), codyNpc, pawn);
            var mineAtOne = PlayerNominates(Held(crowned, One), codyNpc, pawn);
            Assert.That(mine.nominees, Does.Contain(codyNpc), "The player nominates Cody.");
            foreach (var (holder, about) in new[] { (player, codyNpc), (codyNpc, player) })
            {
                Assert.That(Edge(mine, holder, about).score, Is.EqualTo(Edge(mineAtOne, holder, about).score).Within(Exact),
                    holder + "->" + about + ": no standing is written off when the player splits the pair (D-H3).");
                Assert.That(Edge(mine, holder, about).lastInteractionWeek, Is.EqualTo(Edge(mineAtOne, holder, about).lastInteractionWeek), holder + "->" + about);
            }
        }

        /// <summary>The player's own nominations, as Head of Household, through <see cref="EpisodeEngine.Apply"/>. Returns the state after them.</summary>
        private static EpisodeState PlayerNominates(EpisodeState at, string first, string second)
        {
            var command = EpisodeEngineTests.Command(at, EpisodeCommandKind.Nominate);
            command.targetId = first;
            command.secondTargetId = second;
            var result = new EpisodeEngine(at).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        // ------------------------------------------------------------------ the opening and the off values

        [Test]
        public void TheOpeningIsTheSameAtEveryAlumniValue()
        {
            foreach (var (size, player) in new[] { (8, (string)null), (12, null), (8, Derrick) })
                foreach (uint seed in new[] { 1u, 77u })
                {
                    var shipped = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = player }, seed);
                    ShippedRules.ApplyFresh(shipped);
                    Assert.That(shipped.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest), "The writer writes the latest value.");
                    Assert.That(AlumniRules.Latest, Is.GreaterThanOrEqualTo(Two), "Values above 2 move no pin here (§14.G).");
                    for (int value = One; value <= AlumniRules.Latest; value++)
                    {
                        var held = AllStars(size, seed, player, value);
                        Assert.That(held.alumniRulesVersion, Is.EqualTo(value));
                        Assert.That(EpisodeValidation.TryValidate(held, out string error), Is.True, error);
                        held.alumniRulesVersion = AlumniRules.Latest;
                        Assert.That(AlumniHistoriesTests.Json(held), Is.EqualTo(AlumniHistoriesTests.Json(shipped)),
                            size + " seed " + seed + " " + (player ?? "newcomer") + ": a hold at " + value + " is the field alone.");
                    }
                }
        }

        [Test]
        public void AtZeroAndOneEveryNewReaderIsSilent()
        {
            // At 1: a nomination of an old partner, a wrong and a pact on the record, and every new reader says nothing.
            var one = AllStars(12, 1116u, value: One);
            string cody = On(one, Cody), tyler = On(one, Tyler), chelsie = On(one, Chelsie), derrick = On(one, Derrick);
            one.week = 4;
            one.ledger.power.Add(Row(3, cody, new[] { tyler, chelsie }, chelsie));
            Block(one, tyler, cody, chelsie);
            Allied(one, tyler, cody);
            Promised(one, On(one, Janelle), tyler);
            foreach (var (a, b) in new[] { (cody, tyler), (tyler, cody), (On(one, Janelle), tyler), (cody, derrick) })
            {
                Assert.That(Goodwill(one, a, b), Is.EqualTo(1));
                Assert.That(Grievance(one, a, b), Is.EqualTo(1));
                Assert.That(AlumniLoyalty.PutUpWeeks(one, a, b, includeThisWeek: true), Is.Empty);
                Assert.That(AlumniLoyalty.PublicSplitWeek(one, a, b, includeThisWeek: true), Is.Zero);
            }
            Assert.That(Impact(one, tyler, cody), Is.EqualTo(15 + 30).Within(Exact), "Alumni rules 1: a receipt counts whole until a permanent wrong.");
            Assert.That(Impact(one, On(one, Janelle), tyler), Is.EqualTo(-12.5 + 15).Within(Exact), "And an old grievance never heals.");
            Wronged(one, cody, derrick, -20);
            Assert.That(Impact(one, cody, derrick), Is.EqualTo(18 * 0.75 - 20).Within(Exact), "And a breach drops it whole (the handshake fading in week four).");

            // At 0: the same rows in a Regular season, and an entry of the receipt's type that means nothing.
            var zero = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1117u);
            ShippedRules.ApplyFresh(zero);
            Assert.That(zero.alumniRulesVersion, Is.EqualTo(AlumniRules.Off));
            var npcs = zero.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            zero.week = 4;
            zero.ledger.power.Add(Row(3, npcs[1], new[] { npcs[0], npcs[2] }, npcs[2]));
            Block(zero, npcs[1], npcs[0], npcs[3]);
            RelationshipLedger.RecordOneWay(zero, npcs[0], npcs[1], AlumniHistories.ReceiptType, 15, "Anything.", permanent: true);
            Wronged(zero, npcs[0], npcs[1], -20);
            Assert.That(Goodwill(zero, npcs[0], npcs[1]), Is.EqualTo(1));
            Assert.That(Grievance(zero, npcs[0], npcs[1]), Is.EqualTo(1));
            Assert.That(AlumniLoyalty.PutUpWeeks(zero, npcs[1], npcs[0], includeThisWeek: true), Is.Empty);
            Assert.That(AlumniLoyalty.PublicSplitWeek(zero, npcs[1], npcs[0], includeThisWeek: true), Is.Zero);
            Assert.That(Impact(zero, npcs[0], npcs[1]), Is.EqualTo(15 - 20).Within(Exact), "An ordinary entry and an ordinary sum.");
        }
    }
}
