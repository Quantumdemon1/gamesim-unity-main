using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What the player is shown of the past games, from alumni rules 3 (ALL-STAR-HISTORIES-PLAN §14.F, slice S5): the owner's
    /// direction of 2026-10-29, "their connections are known to other players", read back to the player. A houseguest's
    /// profile carries their career record and every seated past game that involves them, in the edition's words, with a
    /// shared alliance's status this season (<see cref="KnownBlocs.ProfileLines"/>); a past game with the player says so,
    /// and the empty line then speaks of this season (<see cref="KnownBlocs.ProfileEmptyLine"/>). The alliances page lists
    /// the seated shared alliances, live or not (<see cref="KnownBlocs.PastGames"/>), and leaves its own reader
    /// (<see cref="AllianceRead.Read"/>) byte for byte as it was. A player on an alumni card has their record in the
    /// diary (<see cref="KnownBlocs.PlayerRecord"/>). Never a number, a feeling or a pact of this season's; nothing below 3,
    /// and nothing in a Regular season.
    ///
    /// <para>Fixtures: the shipped All-Stars season held at this slice's value, with blocks, power rows and statuses
    /// written in, read through the public readers. Only public members (and the shared test helpers), so the file
    /// compiles in Unity's EditMode assembly and in the dotnet subset.</para>
    /// </summary>
    public sealed class PastGamesReadTests
    {
        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Danielle = "danielle-reyes", Vanessa = "vanessa-rousso", Tyler = "tyler-crispen", Chelsie = "chelsie-baham",
            Rachel = "rachel-reilly", Xavier = "xavier-prather";

        private const string Hitmen = "bb16-hitmen", Committee = "bb22-committee";

        private const string HitmenAct = "Derrick and Cody's Hitmen alliance reached the Big Brother 16 final two.";
        private const string CommitteeAct = "Cody and Tyler worked together in the Committee alliance in Big Brother 22.";
        private const string TylerNominatedJanelle = "Tyler nominated Janelle in Big Brother 22.";
        private const string DanielleNominatedJanelle = "Danielle nominated Janelle in Big Brother 7.";
        private const string Chilltown =
            "In Big Brother 7, Will's Chilltown helped persuade the Head of Household to nominate Danielle in Janelle's place before Danielle's eviction.";

        private const string Both = " · both in the house";

        /// <summary>The shipped All-Stars season held at <paramref name="value"/>: by default this slice's own, <see cref="AlumniRules.Reputation"/>.</summary>
        private static EpisodeState AllStars(int size, uint seed, string player = null, int value = AlumniRules.Reputation) =>
            AlumniHistoriesTests.AllStars(size, seed, player, value);

        /// <summary>The shipped All-Stars season built after a change to its cast, enrolled, and held at this slice's value.</summary>
        private static EpisodeState Built(int size, uint seed, Action<EpisodeState> cast, string player = null)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = player }, seed);
            cast(s);
            ShippedRules.ApplyFresh(s);
            Assert.That(AlumniRules.On(s) && s.alumniRulesVersion == AlumniRules.Latest, Is.True, "The season enrols: other alumni are seated.");
            s.alumniRulesVersion = AlumniRules.Reputation;
            return s;
        }

        private static string On(EpisodeState s, string templateId) => AlumniHistoriesTests.On(s, templateId);

        private static EpisodeState Held(EpisodeState s, int value)
        {
            var held = s.Clone();
            held.alumniRulesVersion = value;
            return held;
        }

        /// <summary>This week's live block: the Head of Household and their nominations, before the veto and the reveal.</summary>
        private static void Block(EpisodeState s, string hohId, params string[] nominees)
        {
            s.hohId = hohId;
            s.nominees = nominees.ToList();
            s.evictionResolved = false;
        }

        /// <summary>One week's ceremonies on the record: who was Head of Household, the final nominees, any save, the tally and who left.</summary>
        private static void Row(EpisodeState s, int week, string hohId, string[] nominees, string evicteeId, string savedId = null, string replacementId = null)
        {
            s.ledger.power.Add(new PowerRow
            {
                week = week, hohId = hohId, nominees = nominees.ToList(), tally = new List<int> { 5, 3 }, evicteeId = evicteeId,
                vetoHolderId = savedId, vetoUsed = savedId != null, savedId = savedId, replacementId = replacementId,
            });
        }

        private static string Record(string templateId) => AlumniEditions.Histories1.Career(templateId).Record;

        private static string BeforeThisSeason(string templateId) => "Before this season: " + Record(templateId);

        private static (string, int, string, string, string, string)[] Games(EpisodeState s) =>
            KnownBlocs.PastGames(s).Select(g => (g.FactId, g.Season, g.Act, g.FirstId, g.SecondId, g.Status)).ToArray();

        private static string Status(EpisodeState s, string factId) => KnownBlocs.PastGames(s).Single(g => g.FactId == factId).Status;

        // ------------------------------------------------------------------ the profile's lines (§14.F.1)

        [Test]
        public void TheProfileLinesAreTheEditionsWordsVerbatim()
        {
            var s = AllStars(12, 5101u);
            string before = JsonConvert.SerializeObject(s);
            Assert.That(AlumniHistories.SeatedPairs(s, AlumniEditions.Histories1).Count, Is.EqualTo(5), "The twelve-house seats all five facts.");

            // The words, spelled out: the record after its lead-in, then each past game's act, a shared alliance's with its status.
            Assert.That(KnownBlocs.ProfileLines(s, On(s, Derrick)), Is.EqualTo(new[]
            {
                "Before this season: Won Big Brother 16.",
                "Derrick and Cody's Hitmen alliance reached the Big Brother 16 final two. · both in the house",
            }));
            Assert.That(KnownBlocs.ProfileLines(s, On(s, Cody)), Is.EqualTo(new[]
            {
                "Before this season: Runner-up in Big Brother 16; won Big Brother 22.",
                HitmenAct + Both,
                "Cody and Tyler worked together in the Committee alliance in Big Brother 22. · both in the house",
            }), "Cody is in both shared alliances, in fact-id order.");
            Assert.That(KnownBlocs.ProfileLines(s, On(s, Janelle)), Is.EqualTo(new[]
            {
                "Before this season: Played Big Brother 6, 7, 14 and 22.",
                "Tyler nominated Janelle in Big Brother 22.",
                "Danielle nominated Janelle in Big Brother 7.",
            }), "The nomination facts show, with no status: they are on the record, and no pair.");
            Assert.That(KnownBlocs.ProfileLines(s, On(s, Tyler)), Is.EqualTo(new[] { BeforeThisSeason(Tyler), CommitteeAct + Both, TylerNominatedJanelle }));
            Assert.That(KnownBlocs.ProfileLines(s, On(s, Danielle)), Is.EqualTo(new[] { BeforeThisSeason(Danielle), Chilltown, DanielleNominatedJanelle }));
            Assert.That(KnownBlocs.ProfileLines(s, On(s, Will)), Is.EqualTo(new[] { "Before this season: Won Big Brother 2.", Chilltown }));
            foreach (string template in new[] { Dan, Vanessa, Chelsie, Rachel, Xavier })
                Assert.That(KnownBlocs.ProfileLines(s, On(s, template)), Is.EqualTo(new[] { BeforeThisSeason(template) }), template + ": a record, no past game.");
            Assert.That(KnownBlocs.ProfileLines(s, "vanessa-rousso"), Is.EqualTo(new[] { "Before this season: Third in Big Brother 17." }));
            Assert.That(KnownBlocs.ProfileLines(s, s.playerId), Is.Empty, "The newcomer has no record of past games.");
            Assert.That(KnownBlocs.ProfileLines(s, null), Is.Empty);

            // Every line is the edition's own record or act, verbatim, and only the shared alliances carry a status.
            var edition = AlumniEditions.Histories1;
            foreach (var who in s.contestants.Where(c => !c.isPlayer))
                foreach (string line in KnownBlocs.ProfileLines(s, who.id))
                {
                    bool words = line == BeforeThisSeason(who.sourceTemplateId ?? who.id)
                                 || edition.Pairs.Any(p => line == p.Act || (p.Standing > 0 && line == p.Act + Both));
                    Assert.That(words, Is.True, who.id + ": '" + line + "' is the edition's words.");
                }

            // The eight-house seats neither BB22 row: Janelle has Danielle's nomination alone.
            var eight = AllStars(8, 5102u);
            Assert.That(eight.contestants.Any(c => (c.sourceTemplateId ?? c.id) == Tyler), Is.False);
            Assert.That(KnownBlocs.ProfileLines(eight, On(eight, Janelle)), Is.EqualTo(new[] { BeforeThisSeason(Janelle), DanielleNominatedJanelle }));
            Assert.That(KnownBlocs.ProfileLines(eight, On(eight, Cody)), Is.EqualTo(new[] { BeforeThisSeason(Cody), HitmenAct + Both }));

            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "Reading writes nothing and draws nothing.");
        }

        // ------------------------------------------------------------------ the statuses (§14.F.1)

        [Test]
        public void TheStatusesAreThePublicRecordOfThisSeason()
        {
            var s = AllStars(12, 5201u);
            string derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler), dan = On(s, Dan), chelsie = On(s, Chelsie);
            Assert.That(s.week, Is.EqualTo(1));
            Assert.That((Status(s, Hitmen), Status(s, Committee)), Is.EqualTo(("both in the house", "both in the house")));

            // A departure, whatever kind, names who left; the profile line carries the same status.
            var evicted = s.Clone();
            evicted.Find(derrick).status = ContestantStatus.Evicted;
            Assert.That(Status(evicted, Hitmen), Is.EqualTo("Derrick has left the house"));
            Assert.That(Status(evicted, Committee), Is.EqualTo("both in the house"));
            Assert.That(KnownBlocs.ProfileLines(evicted, cody)[1], Is.EqualTo(HitmenAct + " · Derrick has left the house"));
            Assert.That(KnownBlocs.ProfileLines(evicted, derrick)[1], Is.EqualTo(HitmenAct + " · Derrick has left the house"),
                "The one who left reads it too.");
            var jury = s.Clone();
            jury.Find(tyler).status = ContestantStatus.Jury;
            Assert.That(Status(jury, Committee), Is.EqualTo("Tyler has left the house"));
            var removed = s.Clone();
            removed.Find(cody).status = ContestantStatus.Expelled;
            Assert.That((Status(removed, Hitmen), Status(removed, Committee)), Is.EqualTo(("Cody has left the house", "Cody has left the house")));

            // Both gone: the one who left first, by the week the record gives, either way round.
            var both = s.Clone();
            both.week = 5;
            Row(both, 2, dan, new[] { derrick, chelsie }, derrick);
            Row(both, 4, dan, new[] { cody, chelsie }, cody);
            both.Find(derrick).status = ContestantStatus.Jury;
            both.Find(cody).status = ContestantStatus.Jury;
            Assert.That(Status(both, Hitmen), Is.EqualTo("Derrick has left the house"));
            var reverse = s.Clone();
            reverse.week = 5;
            Row(reverse, 2, dan, new[] { cody, chelsie }, cody);
            Row(reverse, 4, dan, new[] { derrick, chelsie }, derrick);
            reverse.Find(derrick).status = ContestantStatus.Jury;
            reverse.Find(cody).status = ContestantStatus.Jury;
            Assert.That(Status(reverse, Hitmen), Is.EqualTo("Cody has left the house"));

            // The final two stayed in the house to the end.
            var finale = s.Clone();
            finale.Find(derrick).status = ContestantStatus.Winner;
            finale.Find(cody).status = ContestantStatus.RunnerUp;
            Assert.That(Status(finale, Hitmen), Is.EqualTo("both in the house"));

            // This week's block, from the moment it is said, either way round: who put whom up, by first name.
            var named = s.Clone();
            Block(named, tyler, cody, chelsie);
            Assert.That(Status(named, Committee), Is.EqualTo("Week 1: Tyler nominated Cody"));
            Assert.That(Status(named, Hitmen), Is.EqualTo("both in the house"), "Putting up a partner of a partner splits nothing.");
            Assert.That(KnownBlocs.ProfileLines(named, tyler)[1], Is.EqualTo(CommitteeAct + " · Week 1: Tyler nominated Cody"));
            var other = s.Clone();
            Block(other, cody, tyler, chelsie);
            Assert.That(Status(other, Committee), Is.EqualTo("Week 1: Cody nominated Tyler"));
            var elsewhere = s.Clone();
            Block(elsewhere, cody, chelsie, dan);
            Assert.That(Status(elsewhere, Committee), Is.EqualTo("both in the house"), "Putting up somebody else splits nothing.");

            // An earlier week's ceremony: the replacement, and a nominee the veto saved, were put up all the same.
            var replaced = s.Clone();
            replaced.week = 4;
            Row(replaced, 2, tyler, new[] { cody, dan }, dan, savedId: chelsie, replacementId: cody);
            Assert.That(Status(replaced, Committee), Is.EqualTo("Week 2: Tyler nominated Cody"));
            var saved = s.Clone();
            saved.week = 3;
            Row(saved, 1, derrick, new[] { chelsie, dan }, dan, savedId: cody, replacementId: chelsie);
            Assert.That(Status(saved, Hitmen), Is.EqualTo("Week 1: Derrick nominated Cody"));
            // The first week counts, and a split comes before a departure: it can only happen while both are in.
            var twice = s.Clone();
            twice.week = 6;
            Row(twice, 2, cody, new[] { tyler, dan }, dan);
            Row(twice, 4, tyler, new[] { cody, chelsie }, cody);
            twice.Find(cody).status = ContestantStatus.Jury;
            Assert.That(Status(twice, Committee), Is.EqualTo("Week 2: Cody nominated Tyler"));
            Assert.That(Status(twice, Hitmen), Is.EqualTo("Cody has left the house"));

            // The final eviction is the last Head of Household's choice, not a nomination.
            var final = s.Clone();
            final.week = 4;
            final.ledger.power.Add(new PowerRow { week = 3, hohId = derrick, nominees = new List<string> { cody, dan }, tally = new List<int>(), evicteeId = dan });
            Assert.That(Status(final, Hitmen), Is.EqualTo("both in the house"));

            // Nothing private reads here: a broken final two, a betrayal, both scores at the floor, ballots, a pact's end.
            var secret = s.Clone();
            secret.deals.Single(d => d.type == DealKind.FinalTwo).status = DealStatus.Broken;
            RelationshipLedger.RecordOneWay(secret, cody, derrick, "deal_broken", -45, "Broke the final two.", permanent: true);
            secret.relationships.Single(r => r.fromId == cody && r.toId == derrick).score = -100;
            secret.relationships.Single(r => r.fromId == derrick && r.toId == cody).score = -100;
            secret.votes.Add(new VoteState { voterId = derrick, targetId = cody });
            secret.alliances.Add(new AllianceState { id = "alliance-test-ended", name = "ended", members = new List<string> { derrick, cody }, active = false });
            Assert.That(Status(secret, Hitmen), Is.EqualTo("both in the house"));
        }

        // ------------------------------------------------------------------ the player's own past games (§14.F.1, §14.F.3)

        [Test]
        public void ThePlayersOwnPastGamesSayBetweenYouAndTheEmptyLineSaysThisSeason()
        {
            var s = AllStars(12, 5301u, Cody);
            string player = s.playerId, derrick = On(s, Derrick), tyler = On(s, Tyler), dan = On(s, Dan), janelle = On(s, Janelle);
            Assert.That(On(s, Cody), Is.EqualTo(player), "The player on Cody's card is Cody.");
            Assert.That(KnownBlocs.ProfileLines(s, derrick), Is.EqualTo(new[]
            {
                "Before this season: Won Big Brother 16.",
                "Between you, before this season: Derrick and Cody's Hitmen alliance reached the Big Brother 16 final two. · both in the house",
            }));
            Assert.That(KnownBlocs.ProfileLines(s, tyler), Is.EqualTo(new[]
            {
                BeforeThisSeason(Tyler),
                "Between you, before this season: " + CommitteeAct + Both,
                TylerNominatedJanelle,
            }), "Only the fact with the player says so.");
            Assert.That(KnownBlocs.ProfileLines(s, janelle), Is.EqualTo(new[] { BeforeThisSeason(Janelle), TylerNominatedJanelle, DanielleNominatedJanelle }));

            Assert.That(KnownBlocs.ProfileEmptyLine(s, derrick), Is.EqualTo("No promises, alliances or declarations between you this season."));
            Assert.That(KnownBlocs.ProfileEmptyLine(s, tyler), Is.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy));
            Assert.That(KnownBlocs.ProfileEmptyLine(s, dan), Is.EqualTo("No promises, alliances or declarations between you."), "No past game between them: as it always read.");
            Assert.That(KnownBlocs.ProfileEmptyLine(s, janelle), Is.EqualTo(KnownBlocs.NothingBetweenYouCopy));
            Assert.That(KnownBlocs.ProfileEmptyLine(s, player), Is.EqualTo(KnownBlocs.NothingBetweenYouCopy));
            // A status the player made, named as their card is: the act names them so too.
            var named = s.Clone();
            Block(named, player, tyler, dan);
            Assert.That(KnownBlocs.ProfileLines(named, tyler)[1], Is.EqualTo("Between you, before this season: " + CommitteeAct + " · Week 1: Cody nominated Tyler"));
            var gone = s.Clone();
            gone.Find(derrick).status = ContestantStatus.Evicted;
            Assert.That(KnownBlocs.ProfileEmptyLine(gone, derrick), Is.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy), "Gone or not, they played together.");

            // The diary's row: the career's word, then the record and the player's own acts.
            var record = KnownBlocs.PlayerRecord(s);
            Assert.That((record.Value, record.Note), Is.EqualTo(("Winner",
                "Runner-up in Big Brother 16; won Big Brother 22. Derrick and Cody's Hitmen alliance reached the Big Brother 16 final two. "
                + "Cody and Tyler worked together in the Committee alliance in Big Brother 22.")));

            // A nomination fact is a past game between them too.
            var onDanielle = AllStars(12, 5302u, Danielle);
            string will = On(onDanielle, Will), janelle2 = On(onDanielle, Janelle);
            Assert.That(KnownBlocs.ProfileLines(onDanielle, janelle2), Is.EqualTo(new[]
                { BeforeThisSeason(Janelle), TylerNominatedJanelle, "Between you, before this season: " + DanielleNominatedJanelle }));
            Assert.That(KnownBlocs.ProfileLines(onDanielle, will), Is.EqualTo(new[] { BeforeThisSeason(Will), "Between you, before this season: " + Chilltown }));
            Assert.That(KnownBlocs.ProfileEmptyLine(onDanielle, janelle2), Is.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy));
            Assert.That(KnownBlocs.ProfileEmptyLine(onDanielle, will), Is.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy));
            var finalist = KnownBlocs.PlayerRecord(onDanielle);
            Assert.That((finalist.Value, finalist.Note), Is.EqualTo(("Finalist", Record(Danielle) + " " + Chilltown + " " + DanielleNominatedJanelle)));
            var played = KnownBlocs.PlayerRecord(AllStars(12, 5303u, Janelle));
            Assert.That((played.Value, played.Note), Is.EqualTo(("Played", Record(Janelle) + " " + TylerNominatedJanelle + " " + DanielleNominatedJanelle)));
            var alone = KnownBlocs.PlayerRecord(AllStars(8, 5304u, Vanessa));
            Assert.That((alone.Value, alone.Note), Is.EqualTo(("Played", "Third in Big Brother 17.")), "A record with no past game.");

            // A newcomer, and a renamed card, have no record and no past game with anybody.
            var newcomer = AllStars(12, 5305u);
            Assert.That(KnownBlocs.PlayerRecord(newcomer), Is.Null);
            Assert.That(newcomer.contestants.Where(c => !c.isPlayer).Select(c => KnownBlocs.ProfileEmptyLine(newcomer, c.id)).Distinct(),
                Is.EqualTo(new[] { KnownBlocs.NothingBetweenYouCopy }));
            var renamed = Built(12, 5306u, house => house.contestants.Single(c => c.isPlayer).name = "Cody", Cody);
            Assert.That(KnownBlocs.PlayerRecord(renamed), Is.Null, "A renamed card is fiction.");
            Assert.That(KnownBlocs.ProfileLines(renamed, On(renamed, Derrick)), Is.EqualTo(new[] { BeforeThisSeason(Derrick) }));
            Assert.That(KnownBlocs.ProfileEmptyLine(renamed, On(renamed, Derrick)), Is.EqualTo(KnownBlocs.NothingBetweenYouCopy));
        }

        // ------------------------------------------------------------------ the alliances page's section (§14.F.2)

        [Test]
        public void ThePastGamesSectionHoldsEverySeatedSharedAllianceLiveOrNot()
        {
            var s = AllStars(12, 5401u);
            string derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler);
            Assert.That(Games(s), Is.EqualTo(new[]
            {
                (Hitmen, 16, HitmenAct, derrick, cody, "both in the house"),
                (Committee, 22, CommitteeAct, cody, tyler, "both in the house"),
            }), "The shared alliances only, in fact-id order; the nomination facts are no entry.");
            foreach (var game in KnownBlocs.PastGames(s))
                Assert.That(game.Act, Is.EqualTo(AlumniEditions.Histories1.Pairs.Single(p => p.Id == game.FactId).Act), "The act verbatim.");

            // Live or not: a pair that has ended stays on the record, with how it ended.
            var gone = s.Clone();
            gone.Find(derrick).status = ContestantStatus.Evicted;
            Assert.That(KnownBlocs.LivePairs(gone).Select(p => p.Fact.Id), Is.EqualTo(new[] { Committee }));
            Assert.That(Games(gone).Select(g => (g.Item1, g.Item6)), Is.EqualTo(new[] { (Hitmen, "Derrick has left the house"), (Committee, "both in the house") }));
            var split = s.Clone();
            Block(split, tyler, cody, On(s, Chelsie));
            Assert.That(Games(split).Select(g => (g.Item1, g.Item6)), Is.EqualTo(new[] { (Hitmen, "both in the house"), (Committee, "Week 1: Tyler nominated Cody") }));

            // The eight-house seats the Hitmen alone; the player on a card is on its entry.
            var eight = AllStars(8, 5402u);
            Assert.That(Games(eight).Select(g => g.Item1), Is.EqualTo(new[] { Hitmen }));
            var onCody = AllStars(8, 5403u, Cody);
            Assert.That(Games(onCody).Select(g => (g.Item1, g.Item4, g.Item5)), Is.EqualTo(new[]
            {
                (Hitmen, On(onCody, Derrick), onCody.playerId), (Committee, onCody.playerId, On(onCody, Tyler)),
            }));

            // The section is omitted when no shared alliance has both its cards seated: Cody renamed seats neither.
            var renamed = Built(12, 5404u, house => house.contestants.Single(c => c.id == Cody).name = "Cody");
            Assert.That(AlumniRules.At(renamed, AlumniRules.Reputation), Is.True);
            Assert.That(KnownBlocs.PastGames(renamed), Is.Empty);
            Assert.That(KnownBlocs.ProfileLines(renamed, On(renamed, Tyler)), Is.EqualTo(new[] { BeforeThisSeason(Tyler), TylerNominatedJanelle }),
                "The rest of the record still shows.");
            // Two houseguests on Tyler's card: neither is him, so the Committee is not on it.
            var repeated = Built(12, 5405u, house =>
            {
                var vanessa = house.contestants.Single(c => c.id == Vanessa);
                vanessa.sourceTemplateId = Tyler;
                vanessa.name = "Tyler Crispen";
            });
            Assert.That(Games(repeated).Select(g => g.Item1), Is.EqualTo(new[] { Hitmen }));
            Assert.That(KnownBlocs.ProfileLines(repeated, Tyler), Is.Empty, "Neither of two on one card is that alumnus.");
        }

        // ------------------------------------------------------------------ the page's own reader (§14.F.2)

        [Test]
        public void TheAlliancesPageReaderIsByteIdenticalAtEveryValue()
        {
            var s = AllStars(12, 5501u);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will), cody = On(s, Cody), derrick = On(s, Derrick);
            // A pact of the player's, a pact the player was told of, and the Hitmen re-formed in private.
            var ours = new AllianceState { id = "alliance-" + s.nextSequence++, name = "The Dan Pact", members = new List<string> { s.playerId, dan }, active = true };
            s.alliances.Add(ours);
            Knowledge.AllianceFormed(s, ours);
            s.ledger.alliances.Add(new AllianceRow { id = ours.id, why = "player", startedWeek = 1 });
            var told = new AllianceState { id = "alliance-npc-" + s.nextSequence++, name = "Told", members = new List<string> { janelle, will }, active = true };
            s.alliances.Add(told);
            Knowledge.AllianceFormed(s, told);
            var fact = Knowledge.Of(s, FactKinds.Alliance, told.id);
            Knowledge.AddKnower(s, fact, s.playerId);
            Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
            s.ledger.alliances.Add(new AllianceRow { id = told.id, why = "npc", startedWeek = 1 });
            var hitmen = new AllianceState { id = "alliance-npc-" + s.nextSequence++, name = "Hitmen again", members = new List<string> { derrick, cody }, active = true };
            s.alliances.Add(hitmen);
            Knowledge.AllianceFormed(s, hitmen);

            var page = AllianceRead.Read(s);
            Assert.That((page.yours.Count, page.suspected.Count), Is.EqualTo((1, 1)), "The page holds what the player was part of or told.");
            Assert.That(KnownBlocs.PastGames(s), Has.Count.EqualTo(2), "The past games are a reader of their own.");
            string three = JsonConvert.SerializeObject(page);
            Assert.That(three, Does.Not.Contain(Hitmen).And.Not.Contain("Big Brother 16"), "No past game is on the page's own record.");
            foreach (int value in new[] { AlumniRules.Off, AlumniRules.Histories, AlumniRules.Loyalty, AlumniRules.CounterBlocs, AlumniRules.GrowingPacts })
                Assert.That(JsonConvert.SerializeObject(AllianceRead.Read(Held(s, value))), Is.EqualTo(three), "Held at " + value + ": the same page, byte for byte.");
        }

        // ------------------------------------------------------------------ silence below 3, and in a Regular season

        [TestCase(AlumniRules.Loyalty)]
        [TestCase(AlumniRules.Histories)]
        [TestCase(AlumniRules.Off)]
        public void BelowReputationThePlayerIsShownNoPastGame(int value)
        {
            foreach (var (s, what) in new[] { (AllStars(12, 5601u, Cody), "the player on Cody's card"), (AllStars(12, 5602u), "a newcomer") })
            {
                var held = Held(s, value);
                Block(held, On(held, Tyler), On(held, Cody), On(held, Dan));
                foreach (var who in held.contestants)
                {
                    Assert.That(KnownBlocs.ProfileLines(held, who.id), Is.Empty, what + ", " + who.id + " at " + value);
                    Assert.That(KnownBlocs.ProfileEmptyLine(held, who.id), Is.EqualTo(KnownBlocs.NothingBetweenYouCopy), what + ", " + who.id + " at " + value);
                }
                Assert.That(KnownBlocs.PastGames(held), Is.Empty, what + " at " + value);
                Assert.That(KnownBlocs.PlayerRecord(held), Is.Null, what + " at " + value);
            }
            // The same season at 3 and later shows it: the value is the only difference.
            foreach (int shown in new[] { AlumniRules.Reputation, AlumniRules.CounterBlocs, AlumniRules.GrowingPacts })
            {
                var at = Held(AllStars(12, 5601u, Cody), shown);
                Assert.That(KnownBlocs.PastGames(at), Has.Count.EqualTo(2), "At " + shown);
                Assert.That(KnownBlocs.PlayerRecord(at), Is.Not.Null, "At " + shown);
                Assert.That(KnownBlocs.ProfileEmptyLine(at, On(at, Derrick)), Is.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy), "At " + shown);
            }
        }

        [TestCase(8)]
        [TestCase(12)]
        public void ARegularSeasonShowsNoPastGame(int size)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, 5700u + (uint)size);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.alumniRulesVersion, Is.EqualTo(AlumniRules.Off), "A Regular house seats no alumnus and does not enrol.");
            // Even read as if it had enrolled, nobody in it is an alumnus of the edition.
            var enrolled = s.Clone();
            enrolled.alumniRulesVersion = AlumniRules.Latest;
            enrolled.alumniCatalogEdition = AlumniEditions.Histories1Id;
            Assert.That(AlumniRules.At(enrolled, AlumniRules.Reputation), Is.True);
            foreach (var house in new[] { s, enrolled })
            {
                foreach (var who in house.contestants)
                {
                    Assert.That(KnownBlocs.ProfileLines(house, who.id), Is.Empty, who.id);
                    Assert.That(KnownBlocs.ProfileEmptyLine(house, who.id), Is.EqualTo(KnownBlocs.NothingBetweenYouCopy), who.id);
                }
                Assert.That(KnownBlocs.PastGames(house), Is.Empty);
                Assert.That(KnownBlocs.PlayerRecord(house), Is.Null);
            }
        }
    }
}
