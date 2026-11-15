using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShippedRules"/> (BALANCE plan B0): the director's fresh-season setup, moved out of its
    /// StartSeason into the simulation so the director and every harness start a season the same way.
    /// A pure refactor - the season it gives is the one the director's inline lines gave, field for
    /// field - and the pin of what a shipped season plays under.
    /// </summary>
    public sealed class ShippedRulesTests
    {
        /// <summary>Every builder a season the game starts comes from: the quick start, the cast screen's rosters and sizes, the stress house.</summary>
        private static IEnumerable<TestCaseData> Builders()
        {
            yield return new TestCaseData("quick start", 0, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 6, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 8, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 12, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 8, CastTemplates.Roster.AllStars);
            yield return new TestCaseData("stress", 16, CastTemplates.Roster.Regular);
        }

        private static EpisodeState Build(string builder, int size, CastTemplates.Roster roster, uint seed)
        {
            switch (builder)
            {
                case "quick start": return ContentCatalog.Create(seed);
                case "stress": return SeasonBuilder.CreateVerificationStressHouse(size, seed);
                default: return SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = roster }, seed);
            }
        }

        /// <summary>What a season the game starts today plays under, every boundary by name.</summary>
        private static readonly Dictionary<string, int> Shipped = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "competitionRulesVersion", 4 },
            { "readRulesStartWeek", 1 },
            { "leverRulesStartWeek", 1 },
            { "weekRulesStartWeek", 1 },
            { "economyRulesVersion", 1 },
            { "agencyRulesStartWeek", 1 },
            { "finaleRulesStartWeek", 1 },
            { "commitmentRulesStartWeek", 1 },
            // Mode 2, the unified vote rules, since vote family V6 (it was mode 1).
            { "unifiedCommitmentRulesVersion", 2 },
            { "unifiedHearingRulesVersion", 1 },
            { "blocRulesStartWeek", 1 },
            { "socialBudgetRulesStartWeek", 1 },
            { "dealRulesStartWeek", 1 },
            { "eventRulesStartWeek", 1 },
            { "storyRulesStartWeek", 1 },
            { "haveNotRulesStartWeek", 1 },
            { "strategyRulesStartWeek", 1 },
            { "allianceLeakRulesStartWeek", 1 },
            { "pactPlanRulesStartWeek", 1 },
            { "allWeekRulesStartWeek", 1 },
            { "dialogueRulesVersion", 0 },
            // Schema 30's pooled rule fields (SCH30): the tuning rules at B9's balance bands (tuning 1, TuningRules.Shipped)
            // from B9. The alumni rules are the cast's: 0 here, for a season that seats no alumnus; a season that seats one
            // starts at 1 (H1b, AlumniShipped).
            { "alumniRulesVersion", 0 },
            { "tuningRulesVersion", 1 },
            { "npcSocial.rulesStartWeek", 1 },
            { "story.rulesStartWeek", 1 },
            // Story rules 11, Roles, from its enrolment (ROLE-MOMENTS-PLAN R7; it was 10, Blame, from STORY-RULES-10-PLAN §8, and 9, threads, before).
            { "story.rulesVersion", 11 },
        };

        /// <summary>
        /// Whether a builder's season seats an alumnus, and so starts on the alumni rules (H1b, ALL-STAR-HISTORIES-PLAN §6):
        /// the All-Stars roster, and the stress house, which seats both rosters. The Shipped map splits by cast there.
        /// </summary>
        private static bool SeatsAlumni(string builder, CastTemplates.Roster roster) => roster == CastTemplates.Roster.AllStars || builder == "stress";

        /// <summary>What a season that seats an alumnus plays under beside <see cref="Shipped"/>: the histories' rules value.</summary>
        private static readonly Dictionary<string, int> AlumniShipped = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "alumniRulesVersion", AlumniRules.Latest },
        };

        private static int ShippedValue(string builder, CastTemplates.Roster roster, string field) =>
            SeatsAlumni(builder, roster) && AlumniShipped.TryGetValue(field, out int alumni) ? alumni : Shipped[field];

        [TestCaseSource(nameof(Builders))]
        public void ApplyFreshSwitchesOnEveryShippedRuleFromWeekOne(string builder, int size, CastTemplates.Roster roster)
        {
            var s = Build(builder, size, roster, 4101u);
            ShippedRules.ApplyFresh(s);
            var fields = ShippedRules.Fields(s);
            Assert.That(fields.Select(f => f.Key), Is.EquivalentTo(Shipped.Keys), "Every boundary is pinned.");
            foreach (var field in fields)
                Assert.That(field.Value, Is.EqualTo(ShippedValue(builder, roster, field.Key)), builder + " " + size + ": " + field.Key);
            // The edition is content, not a rule field: checked beside the map.
            Assert.That(s.alumniCatalogEdition, Is.EqualTo(SeatsAlumni(builder, roster) ? AlumniEditions.Histories1Id : null), builder + " " + size + ": the edition.");
            Assert.That(s.competitionRulesVersion, Is.EqualTo(CompetitionRules.Current));
            Assert.That(s.story.rulesVersion, Is.EqualTo(StoryRules.Current));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version), "The unified vote rules (V6).");
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(UnifiedCommitmentHearings.ProspectiveVersion));
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            Assert.DoesNotThrow(() => new EpisodeEngine(s), "The public engine takes it.");
        }

        /// <summary>
        /// The refactor's proof: <see cref="ShippedRules.ApplyFresh"/> gives the season the director's inline
        /// setup gave at ca7f4da6 (EpisodeDirector.Season.cs StartSeason, copied below line for line), every
        /// public field of it, first impressions and lore included. A rule the shipped game gains later goes
        /// into ApplyFresh and is added to this copy in the same commit, so the two stay one list: D3's war
        /// rooms are the first, added as the two lanes landed together, D2's all-week beats the next, and the
        /// unified vote rules (vote family V6: mode 2 in place of mode 1) the third, and the All-Star histories
        /// (ALL-STAR-HISTORIES-PLAN H1b) the fourth, last.
        /// </summary>
        [TestCaseSource(nameof(Builders))]
        public void ApplyFreshGivesTheSeasonTheDirectorsInlineSetupGave(string builder, int size, CastTemplates.Roster roster)
        {
            foreach (uint seed in new[] { 1u, 77u, 4242u })
            {
                var shipped = Build(builder, size, roster, seed);
                ShippedRules.ApplyFresh(shipped);
                var inline = Build(builder, size, roster, seed);
                DirectorInlineSetupWithItsLaterRules(inline);
                Assert.That(Json(shipped), Is.EqualTo(Json(inline)), builder + " " + size + " seed " + seed);
            }
        }

        /// <summary>The director's inline setup at ca7f4da6, plus each rule the shipped game has gained since.</summary>
        private static void DirectorInlineSetupWithItsLaterRules(EpisodeState fresh)
        {
            fresh.competitionRulesVersion = CompetitionRules.Current;
            // B9's balance bands (B9-BALANCE-PLAN §4.3): tuning 1, a plain int that draws nothing.
            fresh.tuningRulesVersion = TuningRules.Shipped;
            fresh.haveNotRulesStartWeek = 1;
            fresh.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(fresh);
            EpisodeEngine.EnableRead(fresh);
            EpisodeEngine.EnableLevers(fresh);
            EpisodeEngine.EnableWeek(fresh);
            EpisodeEngine.EnableEconomy(fresh);
            EpisodeEngine.EnableAgency(fresh);
            EpisodeEngine.EnableFinale(fresh);
            EpisodeEngine.EnableCommitments(fresh);
            EpisodeEngine.EnableAllianceLeaks(fresh);
            EpisodeEngine.EnablePactPlans(fresh);
            EpisodeEngine.EnableAllWeek(fresh);
            fresh.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            fresh.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
            EpisodeEngine.EnableAlumniHistories(fresh);
        }

        [Test]
        public void ApplyFreshDrawsNothingFromTheSeasonsStreamAndCommitsNothing()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 515u);
            uint random = s.randomState;
            int sequence = s.nextSequence, events = s.events.Count;
            ShippedRules.ApplyFresh(s);
            Assert.That(s.randomState, Is.EqualTo(random), "No roll.");
            Assert.That(s.nextSequence, Is.EqualTo(sequence), "No id minted.");
            Assert.That(s.events.Count, Is.EqualTo(events), "Nothing logged.");
            Assert.That(s.revision, Is.Zero);
            Assert.That(s.acceptedCommandIds, Is.Empty);
        }

        /// <summary>
        /// The live save schema is 30 (SCH30, the pooled schema): every schema 1 to 29 save reaches it through
        /// EpisodeSaveMigrations.UpgradeV29ToV30, which runs the frozen schema-29 reader (FrozenEpisodeV29.Validate)
        /// first, as UpgradeV28ToV29 runs Frozen28's. A season the game starts today still plays on dialogue rules 0
        /// and tuning rules 0. From H1b a season that seats an alumnus (All-Stars, and the stress house) starts on alumni
        /// rules 1 with edition 1; a season that seats none stays at 0 with no edition. Earlier names:
        /// TheLiveSchemaStaysAt28UntilFrozenEpisodeV28FreezesTheModeTwoCore, TheLiveSchemaIs29AndAShippedSeasonStartsOnDialogueRules0,
        /// then TheLiveSchemaIs30AndAShippedSeasonStartsWithEveryPooledFieldOff (SCH30).
        /// </summary>
        [TestCaseSource(nameof(Builders))]
        public void TheLiveSchemaIs30AndOnlyASeasonThatSeatsAnAlumnusStartsOnTheAlumniRules(string builder, int size, CastTemplates.Roster roster)
        {
            Assert.That(new EpisodeState().schemaVersion, Is.EqualTo(30),
                "The live save schema is 30. Another persisted field needs the next schema, its migration and old-save fixtures.");
            Assert.That(EpisodeState.LiveSchema, Is.EqualTo(30));
            foreach (uint seed in new[] { 1u, 77u, 2929u })
            {
                var s = Build(builder, size, roster, seed);
                ShippedRules.ApplyFresh(s);
                Assert.That(s.schemaVersion, Is.EqualTo(30), "A shipped season saves at the live schema.");
                Assert.That(s.dialogueRulesVersion, Is.Zero, "A shipped season starts with the dialogue rules off.");
                bool alumni = SeatsAlumni(builder, roster);
                Assert.That(s.alumniRulesVersion, Is.EqualTo(alumni ? AlumniRules.Latest : AlumniRules.Off), builder + " " + size + " " + roster + ": the cast's alumni rules.");
                Assert.That(s.alumniCatalogEdition, Is.EqualTo(alumni ? AlumniEditions.Histories1Id : null), "Edition 1 with the alumni rules, none without.");
                Assert.That(s.tuningRulesVersion, Is.EqualTo(TuningRules.Bands), "A shipped season starts on B9's tuning (TuningRules.Shipped, a pinned 1).");
                Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            }
        }

        /// <summary>Fresh seasons only: the economy refuses a season that has been played, so ApplyFresh does too.</summary>
        [Test]
        public void ApplyFreshRefusesASeasonThatHasBeenPlayed()
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 516u));
            Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.Throws<ArgumentException>(() => ShippedRules.ApplyFresh(engine.Snapshot));
            Assert.Throws<ArgumentNullException>(() => ShippedRules.ApplyFresh(null));
            var preview = new SeasonBuilder.Choice { CampaignConversations = true };
            Assert.Throws<ArgumentException>(() => ShippedRules.ApplyFresh(engine.Snapshot, preview), "The preview opts in only a fresh season.");
            Assert.Throws<ArgumentNullException>(() => ShippedRules.ApplyFresh(null, preview));
        }

        // ------------------------------------------------------------------ the cast screen's options (dialogue D1)

        /// <summary>
        /// The cast screen's choice, handed on (DIALOGUE-PLAN D1): with nothing opted in - no choice at all, a
        /// default one, or one naming a roster, a size and a pick but not the preview - it gives exactly the season
        /// <see cref="ShippedRules.ApplyFresh(EpisodeState)"/> gives, every public field, so today's default is
        /// unchanged for every builder.
        /// </summary>
        [TestCaseSource(nameof(Builders))]
        public void ApplyFreshWithNothingOptedInGivesExactlyTheShippedSeason(string builder, int size, CastTemplates.Roster roster)
        {
            foreach (uint seed in new[] { 1u, 4242u })
            {
                var shipped = Build(builder, size, roster, seed);
                ShippedRules.ApplyFresh(shipped);
                foreach (var options in new[] { null, new SeasonBuilder.Choice(),
                    new SeasonBuilder.Choice { HouseSize = size, Roster = roster, PlayerTemplateId = "emma-brown", CampaignConversations = false } })
                {
                    var given = Build(builder, size, roster, seed);
                    ShippedRules.ApplyFresh(given, options);
                    Assert.That(Json(given), Is.EqualTo(Json(shipped)), builder + " " + size + " seed " + seed);
                    Assert.That(given.dialogueRulesVersion, Is.Zero);
                    Assert.That(DialogueRules.On(given), Is.False);
                }
            }
        }

        /// <summary>
        /// The preview chip: a season opted in plays the pinned dialogue rules 2 from week one, and is otherwise the
        /// shipped season to the field - exactly the season rules 2's own fixtures enrol by hand (ShippedRules then
        /// dialogueRulesVersion = 2, DialogueRules2Season.Start). It validates at the live schema 30, the dialogue rules
        /// are on, the engine takes it, and the opt-in drew nothing, minted nothing and logged nothing. The chip writes
        /// <see cref="DialogueRules.Offered"/>, pinned here by its literal, never the bound: when the bound moves this pin
        /// does not, and when the chip moves to a new version it is re-pinned with a reason (DIALOGUE-PLAN §2). Re-pinned
        /// from 1 to 2 when the chip moved from D1's pilot to D2's vote-word tree, which froze rules 2.
        /// </summary>
        [TestCaseSource(nameof(Builders))]
        public void ThePreviewStartsTheSeasonOnThePinnedDialogueRules2AndChangesNothingElse(string builder, int size, CastTemplates.Roster roster)
        {
            Assert.That(DialogueRules.Offered, Is.EqualTo(2), "The chip writes dialogue rules 2 since this change: D2's vote-word tree, frozen by the move.");
            foreach (uint seed in new[] { 1u, 77u })
            {
                var hand = Build(builder, size, roster, seed);
                ShippedRules.ApplyFresh(hand);
                hand.dialogueRulesVersion = 2;
                var chip = Build(builder, size, roster, seed);
                uint random = chip.randomState;
                int sequence = chip.nextSequence, events = chip.events.Count;
                ShippedRules.ApplyFresh(chip, new SeasonBuilder.Choice { HouseSize = size, Roster = roster, CampaignConversations = true });
                Assert.That(Json(chip), Is.EqualTo(Json(hand)), builder + " " + size + " seed " + seed + ": the shipped season with dialogue rules 2, and nothing else.");
                Assert.That(chip.dialogueRulesVersion, Is.EqualTo(2), "The chip writes rules 2 since this change.");
                Assert.That(chip.schemaVersion, Is.GreaterThanOrEqualTo(DialogueRules.Rules2Schema), "Rules 2 is defined from schema 30, which new seasons are written at.");
                foreach (var field in ShippedRules.Fields(chip))
                    Assert.That(field.Value, Is.EqualTo(field.Key == "dialogueRulesVersion" ? 2 : ShippedValue(builder, roster, field.Key)), builder + " " + size + ": " + field.Key);
                Assert.That(chip.alumniCatalogEdition, Is.EqualTo(SeatsAlumni(builder, roster) ? AlumniEditions.Histories1Id : null), builder + " " + size + ": the edition.");
                // Since H1b the shipped form itself mints ids in a season that seats an alumnus - the histories' receipts
                // and the Hitmen's deal - so there the chip's season mints exactly what the shipped season mints, no more.
                int minted = SeatsAlumni(builder, roster) ? hand.nextSequence : sequence;
                Assert.That((chip.randomState, chip.nextSequence, chip.events.Count, chip.revision), Is.EqualTo((random, minted, events, 0)),
                    "The opt-in draws, mints and logs nothing.");
                Assert.That(chip.dialogue.episodes, Is.Empty); Assert.That(chip.dialogue.grants, Is.Empty); Assert.That(chip.dialogue.vetoOccasions, Is.Empty);
                Assert.That(EpisodeValidation.TryValidate(chip, out var error), Is.True, error);
                Assert.That(DialogueRules.TryValidate(chip, out error), Is.True, error);
                Assert.That(DialogueRules.On(chip), Is.True, "The dialogue rules are on in a season opted in.");
                Assert.DoesNotThrow(() => new EpisodeEngine(chip), "The public engine takes it.");
            }
        }

        /// <summary>The preview is the player's choice and nobody else's: off by default, and carried by the copy the creator works on.</summary>
        [Test]
        public void ThePreviewIsOffByDefaultAndTheCreatorsCopyCarriesIt()
        {
            Assert.That(new SeasonBuilder.Choice().CampaignConversations, Is.False, "Off unless the player turns it on.");
            var on = new SeasonBuilder.Choice { HouseSize = 10, CampaignConversations = true };
            var copy = on.Copy();
            Assert.That(copy, Is.Not.SameAs(on));
            Assert.That(copy.CampaignConversations, Is.True, "The creator's copy of the choice still opts the season in.");
            Assert.That(new SeasonBuilder.Choice().Copy().CampaignConversations, Is.False);
            var fresh = SeasonBuilder.Create(copy, 3011u);
            ShippedRules.ApplyFresh(fresh, copy);
            Assert.That(fresh.dialogueRulesVersion, Is.EqualTo(2), "A season started from the creator's copy plays the preview: rules 2, which the chip writes since this change.");
        }

        /// <summary>
        /// A season opted in reaches the conversation it was opted in for: the first nominated campaign a plain walk
        /// meets in the first week opens one through the public entry command the HUD builds, paid with one action.
        /// Since the chip moved to rules 2 the witness is rules 2's (<see cref="DialogueRules2Season"/>, which enrols rules 2
        /// by its literal, DIALOGUE-PLAN §2): the chip's season for the witness's seed is that start, field for field -
        /// tuning 1 included, as every fresh season from B9 - so the walk it takes is the chip season's own, and the
        /// conversation it opens is D2's vote-word tree, not D1's pilot (DialoguePreviewWitness, rules 1 by its literal,
        /// is the rules-1 tests' and no longer the chip's). No nominee, counter or roll is assigned by hand.
        /// </summary>
        [Test]
        public void ASeasonStartedWithThePreviewOpensACampaignConversationThroughThePublicEntry()
        {
            var witness = DialogueRules2Season.FindCampaign();
            var chip = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, witness.Seed);
            ShippedRules.ApplyFresh(chip, new SeasonBuilder.Choice { HouseSize = 8, CampaignConversations = true });
            Assert.That(chip.tuningRulesVersion, Is.EqualTo(TuningRules.Shipped), "Tuning 1, as the rules-2 witness plays it: nothing is held by hand.");
            Assert.That(chip.story.rulesVersion, Is.EqualTo(StoryRules.Roles),
                "Story rules 11, as every new season since its enrolment (ROLE-MOMENTS-PLAN R7) and as the rules-2 witness plays it: nothing is held by hand.");
            Assert.That(Json(chip), Is.EqualTo(Json(DialogueRules2Season.Start(witness.Seed))),
                "seed " + witness.Seed + ": the chip's season is the rules-2 witness's start (the chip writes rules 2 since this change).");
            var before = witness.State;
            string voter = witness.Voters[0];
            Assert.That(before.dialogueRulesVersion, Is.EqualTo(2), "The season is the chip's own, on dialogue rules 2 since this change.");
            Assert.That(before.story.rulesVersion, Is.EqualTo(StoryRules.Roles), "And on story rules 11, the shipped season's since R7's enrolment.");
            Assert.That(before.dialogue.episodes, Is.Empty);
            var opened = DialogueRules2Season.Accepted(new EpisodeEngine(before.Clone()), DialogueRules2Season.Entry(before, voter));
            var episode = DialogueRules.Active(opened);
            Assert.That(episode?.npcId, Is.EqualTo(voter), "seed " + witness.Seed);
            Assert.That(DialogueRules.Cycle(opened, episode).templateId, Is.EqualTo(DialogueCampaignWord.TemplateId),
                "The chip's season opens rules 2's vote-word tree, never rules 1's pilot.");
            Assert.That(opened.revision, Is.EqualTo(before.revision + 1), "One command opened it.");
            Assert.That(EpisodeEngine.SocialActionsSpent(opened), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1), "One action, as the plea costs.");
            Assert.That(opened.dialogue.episodes, Has.Count.EqualTo(1));
            Assert.That(EpisodeValidation.TryValidate(opened, out var error), Is.True, error);
        }

        /// <summary>
        /// The merge of the histories with dialogue D1 (ALL-STAR-HISTORIES-PLAN 8.5, the trap): the cast screen's
        /// options form, which the director's StartSeason calls, runs the one body, so a season that seats an alumnus
        /// starts its histories whatever the player chose. The All-Stars roster of eight and the stress house, through
        /// <see cref="ShippedRules.ApplyFresh(EpisodeState, SeasonBuilder.Choice)"/>, build exactly the season
        /// <see cref="ShippedRules.ApplyFresh(EpisodeState)"/> builds, the latest alumni rules and edition 1 included; with the
        /// preview chip on as well, the same season on the chip's dialogue rules - 2 since the chip moved from D1's 1 -
        /// its histories still written last.
        /// </summary>
        [Test]
        public void TheCastScreensOptionsStartTheSameHistoriesAsTheShippedForm()
        {
            foreach (var (builder, size, roster) in new[] { ("regular", 8, CastTemplates.Roster.AllStars), ("stress", 16, CastTemplates.Roster.Regular) })
                foreach (uint seed in new[] { 1u, 77u, 4242u })
                {
                    var shipped = Build(builder, size, roster, seed);
                    ShippedRules.ApplyFresh(shipped);
                    Assert.That(shipped.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest), builder + " seed " + seed + ": the shipped form enrols the season.");
                    Assert.That(shipped.alumniCatalogEdition, Is.EqualTo(AlumniEditions.Histories1Id));
                    var given = Build(builder, size, roster, seed);
                    ShippedRules.ApplyFresh(given, new SeasonBuilder.Choice { Roster = roster, HouseSize = size });
                    Assert.That(Json(given), Is.EqualTo(Json(shipped)), builder + " seed " + seed + ": the options form is the shipped season, histories included.");
                    var chip = Build(builder, size, roster, seed);
                    ShippedRules.ApplyFresh(chip, new SeasonBuilder.Choice { Roster = roster, HouseSize = size, CampaignConversations = true });
                    shipped.dialogueRulesVersion = 2;
                    Assert.That(Json(chip), Is.EqualTo(Json(shipped)), builder + " seed " + seed
                        + ": the chip adds dialogue rules 2 (its version since this change) and nothing else.");
                    Assert.That(chip.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest));
                    Assert.That(EpisodeValidation.TryValidate(chip, out string error), Is.True, error);
                }
        }

        /// <summary>
        /// <see cref="ShippedRules.Fields"/> names every rule boundary the state has - each int field of the
        /// season, its NPC world and its story whose name ends in RulesStartWeek or RulesVersion, or is the
        /// nested rulesStartWeek / rulesVersion - and reads each from its own field.
        /// </summary>
        [Test]
        public void FieldsNamesEveryRuleBoundaryTheStateHasAndReadsEachFromItsOwnField()
        {
            var s = new EpisodeState();
            var expected = new List<string>();
            int next = 100;
            void Mark(object owner, string prefix)
            {
                foreach (var field in owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(f => f.FieldType == typeof(int) && (f.Name.EndsWith("RulesStartWeek", StringComparison.Ordinal)
                        || f.Name.EndsWith("RulesVersion", StringComparison.Ordinal) || f.Name == "rulesStartWeek" || f.Name == "rulesVersion")))
                {
                    field.SetValue(owner, next++);
                    expected.Add(prefix + field.Name + "=" + field.GetValue(owner));
                }
            }
            Mark(s, "");
            Mark(s.npcSocial, "npcSocial.");
            Mark(s.story, "story.");
            Assert.That(expected.Count, Is.GreaterThanOrEqualTo(23), "The state's boundaries were found.");
            Assert.That(ShippedRules.Fields(s).Select(f => f.Key + "=" + f.Value), Is.EquivalentTo(expected));
        }

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
