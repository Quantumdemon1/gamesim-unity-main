using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story catalogue's lint: what every arc, beat and option must be before it can ship. The
    /// catalogue is data, so these are the checks a content file cannot make of itself - every
    /// asking beat can lapse, every link leads somewhere, every name the text uses is a part the
    /// arc casts, every caption is a fixed constant, and nothing reads like a different show.
    /// </summary>
    public sealed class StoryCatalogLintTests
    {
        private static IEnumerable<(ArcTemplate arc, BeatTemplate beat)> Beats() =>
            StoryCatalog.All.SelectMany(a => a.beats.Select(b => (a, b)));

        private static IEnumerable<(ArcTemplate arc, BeatTemplate beat, OptionTemplate option)> Options() =>
            Beats().SelectMany(x => x.beat.options.Select(o => (x.arc, x.beat, o)));

        private static bool Leads(ArcTemplate arc, string next) =>
            next == null || next == EpisodeEngine.Waits || next.StartsWith("end:", StringComparison.Ordinal) || arc.Beat(next) != null;

        [Test]
        public void EveryArcIsWellFormed()
        {
            Assert.That(StoryCatalog.All.GroupBy(a => a.id).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, "Duplicate arc ids.");
            foreach (var arc in StoryCatalog.All)
            {
                Assert.That(arc.id, Does.Match("^[a-z0-9_-]+$"), arc.id);
                Assert.That(arc.origin, Is.Not.Null.And.Not.Empty, arc.id + " must say where it comes from.");
                Assert.That(arc.origin.StartsWith("web:") || arc.origin.StartsWith("native"), Is.True, arc.id + " origin is web: or native.");
                Assert.That(StoryLanes.IsKnown(arc.lane), Is.True, arc.id + " lane");
                Assert.That(arc.rulesVersion, Is.InRange(StoryRules.Spine, StoryRules.Current), arc.id);
                Assert.That(arc.beats, Is.Not.Empty, arc.id);
                Assert.That(arc.beats.GroupBy(b => b.id).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, arc.id + " duplicate beat ids");
                Assert.That(arc.startAnchors.All(StoryAnchors.IsKnown) && !arc.startAnchors.Contains(StoryAnchors.Conversation), Is.True, arc.id + " start anchors");
                if (arc.conversationTopics != null)
                    Assert.That(arc.conversationTopics.All(t => EpisodeEngine.TopicChance(t) > 0), Is.True, arc.id + " conversation topics");
                Assert.That(arc.roles.GroupBy(r => r.key).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, arc.id + " duplicate roles");
                Assert.That(arc.cooldownWeeks, Is.InRange(0, 100));
                if (arc.startAnchors.Length > 0 || arc.conversationTopics != null)
                    Assert.That(arc.cast, Is.Not.Null, arc.id + " starts from the pool, so it must cast itself.");
            }
        }

        [Test]
        public void EveryBeatThatAsksCanLapse()
        {
            foreach (var (arc, beat) in Beats())
            {
                Assert.That(StorySurfaces.IsKnown(beat.surface), Is.True, arc.id + ":" + beat.id + " surface");
                Assert.That(beat.options, Is.Not.Empty, arc.id + ":" + beat.id);
                if (beat.NpcHeld) continue;
                Assert.That(beat.lapse, Is.Not.Null, arc.id + ":" + beat.id + " needs a lapse option.");
                var lapse = beat.Option(beat.lapse);
                Assert.That(lapse, Is.Not.Null, arc.id + ":" + beat.id + " lapse names a missing option.");
                // Not answering must always be possible, and never need anything.
                Assert.That(lapse.check, Is.EqualTo(-1), arc.id + ":" + beat.id + " lapse is checked");
                Assert.That(lapse.conduct || lapse.costsAction || lapse.pick != null || lapse.needs != null
                            || lapse.lockUnless != null || lapse.onlyIf != null || lapse.showIf != null, Is.False,
                    arc.id + ":" + beat.id + " lapse is gated");
                // A lapse resolves inside an Advance: it may not open another card at once.
                if (lapse.next != null && arc.Beat(lapse.next) is BeatTemplate after)
                    Assert.That(after.anchor != null || after.NpcHeld, Is.True, arc.id + ":" + beat.id + " lapse chains into an asking beat");
                Assert.That(StoryText.Tokens(beat.summary), Is.Empty, arc.id + ":" + beat.id + " summary must name nobody.");
                Assert.That(beat.title, Is.Not.Null.And.Not.Empty, arc.id + ":" + beat.id + " title");
                Assert.That(beat.text, Is.Not.Null.And.Not.Empty, arc.id + ":" + beat.id + " text");
                if (beat.closes != null) Assert.That(StoryAnchors.IsKnown(beat.closes), Is.True);
                if (beat.anchor != null) Assert.That(StoryAnchors.IsKnown(beat.anchor), Is.True);
                if (beat.talkRole != null) Assert.That(arc.roles.Any(r => r.key == beat.talkRole), Is.True, arc.id + ":" + beat.id + " talk role");
            }
        }

        [Test]
        public void EveryOptionIsWellFormed()
        {
            foreach (var (arc, beat) in Beats())
            {
                Assert.That(beat.options.GroupBy(o => o.id).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, arc.id + ":" + beat.id + " duplicate option ids");
                if (!beat.NpcHeld)
                    Assert.That(beat.options.GroupBy(o => o.label).Where(g => g.Count() > 1 && g.All(o => o.showIf == null)).Select(g => g.Key), Is.Empty,
                        arc.id + ":" + beat.id + " duplicate captions");
            }
            foreach (var (arc, beat, option) in Options())
            {
                string where = arc.id + ":" + beat.id + ":" + option.id;
                Assert.That(option.id, Does.Match("^[a-z0-9-]+$"), where);
                Assert.That(option.label, Is.Not.Null.And.Not.Empty, where);
                // A caption is a constant: never composed with a name or a number.
                Assert.That(option.label.Contains("{"), Is.False, where + " caption names somebody");
                Assert.That(option.label.Length, Is.LessThanOrEqualTo(48), where + " caption is too long for a button");
                Assert.That(HouseEventRisk.IsKnown(option.risk), Is.True, where + " risk");
                Assert.That(option.approach == null || Personality.Approach.IsKnown(option.approach), Is.True, where + " approach");
                Assert.That(option.check == -1 || (option.check >= 0 && option.check <= 100), Is.True, where + " check");
                if (option.check >= 0 && option.checkRole != null)
                    Assert.That(arc.roles.Any(r => r.key == option.checkRole), Is.True, where + " checks an undeclared role");
                Assert.That(Leads(arc, option.next), Is.True, where + " next: " + option.next);
                Assert.That(Leads(arc, option.nextOnBackfire), Is.True, where + " next on backfire: " + option.nextOnBackfire);
                foreach (var fx in option.effects.Concat(option.backfire))
                {
                    Assert.That(StoryEffects.IsKnown(fx.kind), Is.True, where + " effect " + fx.kind);
                    foreach (var key in new[] { fx.from, fx.to, fx.third })
                        if (!string.IsNullOrEmpty(key) && key != Fx.Player && key != Fx.Pick && key[0] != '@')
                            Assert.That(arc.roles.Any(r => r.key == key), Is.True, where + " effect names an undeclared role " + key);
                    if (fx.kind == StoryEffects.Receipt) Assert.That(StoryReceipts.IsKnown(fx.type), Is.True, where + " receipt " + fx.type);
                    if (fx.kind == StoryEffects.Modifier) Assert.That(StoryModifierCatalog.Find(fx.type), Is.Not.Null, where + " modifier " + fx.type);
                    if (fx.kind == StoryEffects.Deal) Assert.That(DealKind.IsKnown(fx.type), Is.True, where + " deal " + fx.type);
                    if (fx.kind == StoryEffects.Bond || fx.kind == StoryEffects.BondEnd) Assert.That(BondKinds.IsKnown(fx.type), Is.True, where + " bond " + fx.type);
                    if (fx.kind == StoryEffects.Grudge) Assert.That(GrudgeCauses.IsKnown(fx.type), Is.True, where + " grudge cause " + fx.type);
                    if (fx.kind == StoryEffects.Reveal) Assert.That(Lore.Facets.All.Contains(fx.type), Is.True, where + " facet " + fx.type);
                    if (fx.kind == StoryEffects.Promise) Assert.That(Enum.TryParse(fx.type, out PromiseKind _), Is.True, where + " promise " + fx.type);
                }
                if (option.pick != null || option.effects.Concat(option.backfire).Any(fx => fx.from == Fx.Pick || fx.to == Fx.Pick || fx.third == Fx.Pick))
                    Assert.That(option.pick, Is.Not.Null, where + " names the pick but asks for nobody");
            }
        }

        [Test]
        public void EveryNameATextUsesIsAPartTheArcCasts()
        {
            foreach (var (arc, beat) in Beats())
            {
                var roles = new HashSet<string>(arc.roles.Select(r => r.key));
                var texts = new[] { beat.text, beat.summary }.Concat(beat.alternates ?? Array.Empty<string>())
                    .Concat(beat.options.SelectMany(o => new[] { o.description, o.outcome, o.backfireOutcome }));
                foreach (var text in texts.Where(t => t != null))
                    foreach (var token in StoryText.Tokens(text))
                        Assert.That(roles.Contains(token), Is.True, arc.id + ":" + beat.id + " uses {" + token + "} but casts no such part.");
            }
        }

        [Test]
        public void TheCatalogueSpeaksBigBrotherNotAnotherShow()
        {
            var banned = new[] { "tribe", "tribal", "castaway", "idol", "immunity", "island", "camp " };
            foreach (var (arc, beat) in Beats())
            {
                var texts = new[] { arc.title, arc.eyebrow, beat.title, beat.text, beat.summary }.Concat(beat.alternates ?? Array.Empty<string>())
                    .Concat(beat.options.SelectMany(o => new[] { o.label, o.description, o.outcome, o.backfireOutcome }));
                foreach (var text in texts.Where(t => t != null))
                    foreach (var word in banned)
                        Assert.That(text.ToLowerInvariant().Contains(word), Is.False, arc.id + ":" + beat.id + " says \"" + word.Trim() + "\": " + text);
            }
        }

        [Test]
        public void EveryLoreSheetAgreesWithItsCardAndKeepsRealPeopleInTheGameRegister()
        {
            foreach (var sheet in Lore.Authored.Values)
            {
                var card = CastTemplates.Find(sheet.key);
                Assert.That(card, Is.Not.Null, sheet.key);
                Assert.That(card.Roster, Is.EqualTo(CastTemplates.Roster.Regular), sheet.key + " is authored, so it must be fictional.");
                Assert.That(card.Traits[0], Is.EqualTo(sheet.primaryTrait), sheet.key + " primary trait");
                Assert.That(sheet.facts.Length, Is.InRange(6, 16), sheet.key);
                Assert.That(sheet.facts.Select(f => f.id).Distinct().Count(), Is.EqualTo(sheet.facts.Length), sheet.key);
                Assert.That(sheet.facts.Any(f => f.facet == Lore.Facets.Secret && f.depth == 4), Is.True, sheet.key + " needs a depth-four secret.");
            }
            foreach (var sheet in Lore.Legacy.Values)
            {
                var card = CastTemplates.Find(sheet.key);
                Assert.That(card, Is.Not.Null, sheet.key);
                Assert.That(card.Roster, Is.EqualTo(CastTemplates.Roster.AllStars), sheet.key);
                Assert.That(sheet.romance, Is.Null, sheet.key + ": no romance about a real person.");
                foreach (var fact in sheet.facts)
                {
                    Assert.That(fact.sensitivity, Is.EqualTo(StoryPeople.Sensitivity.Game), fact.id + " must be game register.");
                    Assert.That(new[] { Lore.Facets.Home, Lore.Facets.Romance, Lore.Facets.Secret, Lore.Facets.Unforgivable, Lore.Facets.Origin }
                        .Contains(fact.facet), Is.False, fact.id + " is a personal facet on a real person.");
                }
            }
            // Every card on both rosters is covered: fictional ones by an authored sheet, alumni by a legacy one.
            foreach (var card in CastTemplates.Everyone)
                Assert.That(card.Roster == CastTemplates.Roster.Regular ? Lore.Authored.ContainsKey(card.Id) : Lore.Legacy.ContainsKey(card.Id),
                    Is.True, card.Id + " has no sheet.");
        }

        [Test]
        public void EveryTraitHasPersonalityAxes()
        {
            foreach (var trait in WebTraits.Boosts.Keys)
                Assert.That(Personality.Table.ContainsKey(trait), Is.True, trait);
        }
    }
}
