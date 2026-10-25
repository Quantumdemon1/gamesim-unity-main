using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The cast pool and the seasons built from it.
    ///
    /// <para>Two things matter more than the rest here. A trait that is not in
    /// <see cref="WebTraits"/> boosts nothing, so a typo in the table produces a houseguest who is
    /// quietly worse than everyone else and no error anywhere. And five of these people also ship in
    /// <see cref="ContentCatalog"/>'s scenario — if the two descriptions of Maya Hassan drift, the
    /// same name means two different players depending on how the season started.</para>
    /// </summary>
    public sealed class CastTemplateTests
    {
        [Test]
        public void EveryTraitIsOneTheTraitTableKnows()
        {
            foreach (var template in CastTemplates.Everyone)
            {
                Assert.That(template.Traits, Is.Not.Null.And.Length.EqualTo(WebTraits.MaximumTraits),
                    template.Name + " must carry exactly two traits.");
                foreach (var trait in template.Traits)
                    Assert.That(WebTraits.Known(trait), Is.True,
                        template.Name + " carries \"" + trait + "\", which boosts nothing.");
            }
        }

        [Test]
        public void EveryTemplateIsCompleteAndUniquelyIdentified()
        {
            var ids = CastTemplates.Everyone.Select(t => t.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "Template ids must be unique.");

            foreach (var template in CastTemplates.Everyone)
            {
                Assert.That(template.Name, Is.Not.Null.And.Not.Empty);
                Assert.That(template.Archetype, Is.Not.Null.And.Not.Empty);
                Assert.That(template.Motive, Is.Not.Null.And.Not.Empty, template.Name + " needs a motive: it is the line they open a conversation with.");
                Assert.That(template.Pronouns, Is.Not.Null.And.Not.Empty);
                Assert.That(template.HomeRoom, Is.Not.Null.And.Not.Empty);
                Assert.That(template.Age, Is.InRange(18, 99), template.Name + "'s age is outside the range a card can show.");
                Assert.That(CastTemplates.Categories, Contains.Item(template.Category),
                    template.Name + " is filed under a category no filter chip offers.");
            }
        }

        /// <summary>
        /// The five shared with the shipped scenario are the same people either way — same room,
        /// same pronouns, same motive, same traits, same stats.
        /// </summary>
        [Test]
        public void SharedHouseguestsMatchTheShippedScenarioExactly()
        {
            var scenario = ContentCatalog.Create(7u);
            var shared = scenario.contestants.Where(c => !c.isPlayer).ToList();
            Assert.That(shared, Is.Not.Empty);

            int compared = 0;
            foreach (var authored in shared)
            {
                var template = CastTemplates.Find(authored.id);
                if (template == null) continue;
                compared++;

                var built = CastTemplates.ToContestant(template, false);
                Assert.That(built.name, Is.EqualTo(authored.name));
                Assert.That(built.pronouns, Is.EqualTo(authored.pronouns), authored.name + "'s pronouns differ between the pool and the scenario.");
                Assert.That(built.homeRoom, Is.EqualTo(authored.homeRoom), authored.name + "'s room differs.");
                Assert.That(built.motive, Is.EqualTo(authored.motive), authored.name + "'s motive differs.");
                Assert.That(built.archetype, Is.EqualTo(authored.archetype));
                Assert.That(built.age, Is.EqualTo(authored.age));
                Assert.That(built.occupation, Is.EqualTo(authored.occupation));
                CollectionAssert.AreEqual(authored.traits, built.traits, authored.name + "'s traits differ.");
                foreach (var stat in WebTraits.StatNames)
                    Assert.That(WebTraits.Get(built.stats, stat), Is.EqualTo(WebTraits.Get(authored.stats, stat)),
                        authored.name + "'s " + stat + " differs between the pool and the scenario.");
            }
            Assert.That(compared, Is.EqualTo(5), "The scenario's five named houseguests should all be in the pool.");
        }

        [Test]
        public void FilteringNarrowsToOneRosterAndOneCategory()
        {
            foreach (CastTemplates.Roster roster in System.Enum.GetValues(typeof(CastTemplates.Roster)))
            {
                var everyone = CastTemplates.Filter(roster, CastTemplates.AllCategories).ToList();
                CollectionAssert.AreEqual(CastTemplates.In(roster).ToList(), everyone,
                    "\"All\" must not drop anyone from " + CastTemplates.RosterName(roster) + ".");

                int counted = 0;
                foreach (var category in CastTemplates.Categories)
                {
                    var slice = CastTemplates.Filter(roster, category).ToList();
                    Assert.That(slice.All(t => t.Roster == roster && t.Category == category), Is.True);
                    counted += slice.Count;
                }
                Assert.That(counted, Is.EqualTo(everyone.Count),
                    "The chips must partition " + CastTemplates.RosterName(roster) + " exactly.");
            }
        }

        // ---------------------------------------------------------------- built seasons

        [Test]
        public void EverySizeOnEveryRosterBuildsAValidSeason()
        {
            foreach (CastTemplates.Roster roster in System.Enum.GetValues(typeof(CastTemplates.Roster)))
                for (int size = SeasonBuilder.MinimumHouse; size <= SeasonBuilder.LargestHouse(roster); size++)
                {
                    var state = SeasonBuilder.Create(
                        new SeasonBuilder.Choice { Roster = roster, HouseSize = size }, 4242u);

                    Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True,
                        CastTemplates.RosterName(roster) + " at " + size + ": " + error);
                    Assert.That(state.contestants.Count, Is.EqualTo(size));
                    Assert.That(state.contestants.Count(c => c.isPlayer), Is.EqualTo(1));
                    Assert.That(state.relationships.Count, Is.EqualTo(size * (size - 1)),
                        "Every ordered pair needs a relationship row.");
                }
        }

        [Test]
        public void AHouseSizeOutsideTheRosterIsClampedRatherThanRefused()
        {
            var roster = CastTemplates.Roster.Regular;
            int largest = SeasonBuilder.LargestHouse(roster);

            var tooBig = SeasonBuilder.Create(new SeasonBuilder.Choice { Roster = roster, HouseSize = 99 }, 1u);
            Assert.That(tooBig.contestants.Count, Is.EqualTo(largest),
                "A house larger than the roster is filled to the roster, never padded from the other one.");

            var tooSmall = SeasonBuilder.Create(new SeasonBuilder.Choice { Roster = roster, HouseSize = 0 }, 1u);
            Assert.That(tooSmall.contestants.Count, Is.EqualTo(SeasonBuilder.MinimumHouse));
            Assert.That(EpisodeValidation.TryValidate(tooSmall, out _), Is.True);
        }

        /// <summary>
        /// Playing as someone does not put two of them in the house, and does not change which
        /// record the engine treats as the player's.
        /// </summary>
        [Test]
        public void PlayingAsATemplateReplacesThemRatherThanDuplicatingThem()
        {
            var choice = new SeasonBuilder.Choice
            {
                Roster = CastTemplates.Roster.AllStars,
                PlayerTemplateId = "dan-gheesling",
                HouseSize = 8,
            };
            var state = SeasonBuilder.Create(choice, 99u);

            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(state.playerId, Is.EqualTo(ContentCatalog.PlayerId));

            var you = state.Find(state.playerId);
            Assert.That(you.isPlayer, Is.True);
            Assert.That(you.name, Is.EqualTo("Dan Gheesling"));
            Assert.That(you.archetype, Is.EqualTo("The Funeral Director"));
            Assert.That(state.contestants.Count(c => c.name == "Dan Gheesling"), Is.EqualTo(1),
                "The chosen houseguest must not also be cast as an NPC.");
            Assert.That(state.contestants.Count, Is.EqualTo(8));
        }

        [Test]
        public void NoChoiceStillProducesTheUnaffiliatedNewcomer()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 5u);
            var you = state.Find(state.playerId);
            Assert.That(you.name, Is.EqualTo("You"));
            Assert.That(you.archetype, Is.EqualTo("The Newcomer"));
            foreach (var stat in WebTraits.StatNames)
                Assert.That(WebTraits.Get(you.stats, stat), Is.EqualTo(6d),
                    "The default player starts balanced, as the shipped scenario's does.");
        }

        /// <summary>
        /// The cast is chosen without touching the generator. Consuming <c>randomState</c> before
        /// week one would shift every seeded result in the season, which is what every replay
        /// fixture in this project is anchored to.
        /// </summary>
        [Test]
        public void BuildingASeasonConsumesNoRandomness()
        {
            const uint seed = 20260917u;
            var state = SeasonBuilder.Create(
                new SeasonBuilder.Choice { HouseSize = 10, PlayerTemplateId = "maya-hassan" }, seed);

            Assert.That(state.randomState, Is.EqualTo(seed));
            Assert.That(state.seed, Is.EqualTo(seed));
        }

        [Test]
        public void TheSameChoiceAndSeedBuildTheSameHouse()
        {
            var choice = new SeasonBuilder.Choice
            {
                Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = "janelle-pierzina", HouseSize = 9,
            };
            var first = SeasonBuilder.Create(choice, 11u);
            var second = SeasonBuilder.Create(choice.Copy(), 11u);

            CollectionAssert.AreEqual(
                first.contestants.Select(c => c.id).ToList(),
                second.contestants.Select(c => c.id).ToList());
            Assert.That(first.events[0].text, Is.EqualTo(second.events[0].text));
        }

        /// <summary>
        /// The arrival line must not be the shipped scenario's, which names six and is recorded
        /// verbatim in a frozen replay fixture.
        /// </summary>
        [Test]
        public void ABuiltSeasonWritesItsOwnArrivalLine()
        {
            var scenario = ContentCatalog.Create(1u);
            var authored = scenario.events.First(e => e.kind == "arrival").text;

            var built = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1u);
            var line = built.events.First(e => e.kind == "arrival").text;

            Assert.That(line, Is.Not.EqualTo(authored));
            Assert.That(line, Does.StartWith("Eight houseguests"), "In the house's own word, as every card of the opening says it.");
            Assert.That(line, Does.Not.Contain("housemates"));
            Assert.That(line.Length, Is.LessThanOrEqualTo(4000));

            var allStars = SeasonBuilder.Create(
                new SeasonBuilder.Choice { Roster = CastTemplates.Roster.AllStars, HouseSize = 12 }, 1u);
            Assert.That(allStars.events.First(e => e.kind == "arrival").text,
                Does.StartWith("Twelve houseguests").And.Contains("played before"));
        }

        /// <summary>
        /// A built season is a season: every size on every roster plays through to a winner.
        ///
        /// <para>This is the claim that matters. A house that validates at week one but wedges at
        /// the Final Three is worse than one that was refused outright, and the sizes this screen
        /// now offers have never been played end to end — the whole project has only ever finished
        /// seasons of six.</para>
        /// </summary>
        [Test]
        public void EverySizeOnEveryRosterPlaysThroughToAWinner()
        {
            foreach (CastTemplates.Roster roster in System.Enum.GetValues(typeof(CastTemplates.Roster)))
                for (int size = SeasonBuilder.MinimumHouse; size <= SeasonBuilder.LargestHouse(roster); size++)
                {
                    string where = CastTemplates.RosterName(roster) + " at " + size;
                    var engine = new EpisodeEngine(
                        SeasonBuilder.Create(new SeasonBuilder.Choice { Roster = roster, HouseSize = size }, 808u));

                    int guard = 0;
                    while (engine.Snapshot.phase != EpisodePhase.Finished && guard++ < 600)
                    {
                        var command = EpisodeEngineTests.NextCommand(engine.Snapshot);
                        var result = engine.Apply(command);
                        Assert.That(result.accepted, Is.True,
                            where + ", " + command.expectedPhase + ", " + command.kind + ": " + result.reason);
                    }

                    var finished = engine.Snapshot;
                    Assert.That(finished.phase, Is.EqualTo(EpisodePhase.Finished), where + " did not finish.");
                    Assert.That(finished.Find(finished.winnerId), Is.Not.Null, where + " finished without a winner.");
                    Assert.That(EpisodeValidation.TryValidate(finished, out var error), Is.True, where + ": " + error);
                }
        }

        /// <summary>
        /// The alumni audit (story plan 20 §3.10, decision 10). Three bios said more than the record
        /// holds: Janelle's count of seasons, Danielle's jury and its footage, and Rachel's showmance
        /// catchphrase. Each now says only what the project's record holds (ALL-STAR-HISTORIES-PLAN 3.4).
        /// </summary>
        [Test]
        public void TheAuditedAlumniBiosSayOnlyWhatTheRecordHolds()
        {
            Assert.That(CastTemplates.Find("janelle-pierzina").Bio, Is.EqualTo(
                "Four-time player and undisputed competition queen. Her fierce loyalty and athletic dominance make her the ultimate competitor."));
            Assert.That(CastTemplates.Find("danielle-reyes").Bio, Is.EqualTo(
                "Pioneer strategist who reached the final two in her first season and came back for All-Stars. Her intuitive reads on people are legendary."));
            Assert.That(CastTemplates.Find("rachel-reilly").Bio, Is.EqualTo(
                "Brash, confrontational, and fiercely competitive. Came back for a second season and won it."));
            foreach (var card in CastTemplates.In(CastTemplates.Roster.AllStars))
                foreach (var claim in new[] { "my man", "between me and", "footage", "Three-time" })
                    Assert.That(card.Bio, Does.Not.Contain(claim).IgnoreCase, card.Name + "'s bio");
        }

        /// <summary>
        /// The three audited cards' words before the audit, exactly as the older build cast them
        /// into its seasons. They are kept here only so a replay can stand in for that build; no
        /// card and no line says them any more.
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string> BiosBeforeTheAudit = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["janelle-pierzina"] = "Three-time player and undisputed competition queen. Her fierce loyalty and athletic dominance make her the ultimate competitor.",
            ["danielle-reyes"] = "Pioneer strategist who played before the jury could watch footage. Her intuitive reads on people are legendary.",
            ["rachel-reilly"] = "Brash, confrontational, and fiercely competitive. Her catchphrase \"No one gets between me and my man!\" echoes through BB history.",
        };

        /// <summary>A fresh twelve-house All-Stars season: eleven alumni and the newcomer.</summary>
        internal static EpisodeState CastAllStars()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { Roster = CastTemplates.Roster.AllStars, HouseSize = 12 }, 4242u);
            ShippedRules.ApplyFresh(s);
            return s;
        }

        internal static List<ContestantState> AlumniOf(EpisodeState s) =>
            s.contestants.Where(c => StoryPeople.TemplateOf(c)?.Roster == CastTemplates.Roster.AllStars).ToList();

        /// <summary>Plays a season out with the engine tests' scripted player.</summary>
        internal static EpisodeState PlayOut(EpisodeState start)
        {
            var engine = new EpisodeEngine(start);
            for (int guard = 0; engine.Snapshot.phase != EpisodePhase.Finished && guard < 4000; guard++)
            {
                var command = EpisodeEngineTests.NextCommand(engine.Snapshot);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, command.expectedPhase + ", " + command.kind + ": " + result.reason);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            return engine.Snapshot;
        }

        internal static string Record(EpisodeState end) =>
            "winner " + end.winnerId + "\n" + string.Join("\n", end.events.Select(e => e.sequence + " " + e.kind + " " + e.text));

        /// <summary>
        /// Runs <paramref name="play"/> while the audited cards hold their words from before the
        /// audit, as the older build's did, and puts the audited words back afterwards. The cards
        /// are shared statics, so this fixture must not run in parallel with another.
        /// </summary>
        internal static T OnTheCardsBeforeTheAudit<T>(System.Func<T> play)
        {
            var audited = BiosBeforeTheAudit.Keys.ToDictionary(id => id, id => CastTemplates.Find(id).Bio, System.StringComparer.Ordinal);
            try
            {
                foreach (var pair in BiosBeforeTheAudit) CastTemplates.Find(pair.Key).Bio = pair.Value;
                return play();
            }
            finally
            {
                foreach (var pair in audited) CastTemplates.Find(pair.Key).Bio = pair.Value;
            }
        }

        /// <summary>
        /// A season keeps the bio it was cast with. <see cref="CastTemplates.ToContestant"/> copies
        /// the card's words into the season, and nothing reads an All-Star's bio back against the
        /// card, or reads the card's own words during play: who somebody is - the real-alumni rule,
        /// their lore sheet, their own lines - goes by template and name. So a season cast before
        /// the audit, its alumni carrying other words, is still valid, still knows every one of
        /// them, and plays exactly as the same season with the audited words does - whether the
        /// cards hold the audited words or, as in the older build, the words before the audit.
        /// The save store's side of this is <c>AlumniAuditSaveTests</c>, which runs in
        /// Unity only.
        /// </summary>
        [Test]
        public void ASeasonCastBeforeTheAuditKeepsItsBiosAndPlaysTheSame()
        {
            string Older(string name) => "An older build's words about " + name + ".";

            var audited = CastAllStars();
            var older = CastAllStars();
            var alumni = AlumniOf(older);
            Assert.That(alumni, Has.Count.EqualTo(11), "Eleven alumni and the newcomer.");
            Assert.That(BiosBeforeTheAudit.Keys.All(id => alumni.Any(who => StoryPeople.TemplateOf(who).Id == id)), Is.True, "The house seats every audited card.");
            foreach (var who in alumni)
            {
                var card = StoryPeople.TemplateOf(who);
                Assert.That(audited.Find(who.id).bio, Is.EqualTo(card.Bio), "A new season is cast with the audited words.");
                who.bio = Older(who.name);
                Assert.That(StoryPeople.IsRealPerson(who), Is.True, who.id);
                Assert.That(WebIntroductions.AlumnusOf(who), Is.SameAs(card), who.id);
                var sheet = Lore.SheetFor(who);
                Assert.That(sheet?.source, Is.EqualTo(LoreSources.Legacy), who.id);
                Assert.That(sheet.key, Is.EqualTo(card.Id), who.id);
            }
            Assert.That(EpisodeValidation.TryValidate(older, out var invalid), Is.True, invalid);

            var playedOlder = PlayOut(older);
            var playedAudited = PlayOut(audited);
            Assert.That(Record(playedOlder), Is.EqualTo(Record(playedAudited)), "The words on a season move nothing in it.");
            foreach (var who in alumni)
            {
                Assert.That(playedOlder.Find(who.id).bio, Is.EqualTo(Older(who.name)), "The season plays on with the words it was cast with.");
                Assert.That(playedAudited.Find(who.id).bio, Is.EqualTo(StoryPeople.TemplateOf(who).Bio));
            }

            var onTheOlderCards = OnTheCardsBeforeTheAudit(() =>
            {
                foreach (var pair in BiosBeforeTheAudit)
                    Assert.That(CastTemplates.Find(pair.Key).Bio, Is.EqualTo(pair.Value));
                return PlayOut(older);
            });
            foreach (var pair in BiosBeforeTheAudit)
                Assert.That(CastTemplates.Find(pair.Key).Bio, Is.Not.EqualTo(pair.Value), "The audited words are back on the card.");
            Assert.That(Record(onTheOlderCards), Is.EqualTo(Record(playedAudited)),
                "The words on a card move nothing: the older build plays this season as this one does.");
            foreach (var who in alumni)
                Assert.That(onTheOlderCards.Find(who.id).bio, Is.EqualTo(Older(who.name)));
        }
    }
}
