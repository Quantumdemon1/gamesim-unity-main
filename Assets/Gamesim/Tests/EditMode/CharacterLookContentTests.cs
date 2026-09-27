using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The colours a houseguest is made of, and the hair and accessories built in code: named and
    /// distinct, covering every complexion and undertone, each with a picture for the creator; a
    /// random houseguest coloured as their skin makes likely; and the face's details worn as the
    /// person's own.
    /// </summary>
    public sealed class CharacterLookContentTests
    {
        private static IEnumerable<CharacterPalettes.Group[]> Palettes()
        {
            yield return CharacterPalettes.Skin;
            yield return CharacterPalettes.Hair;
            yield return CharacterPalettes.Eyes;
        }

        [Test]
        public void EveryPaletteNamesEachSwatchOnceAndFindsItByName()
        {
            foreach (var palette in Palettes())
            {
                var swatches = CharacterPalettes.All(palette).ToList();
                Assert.That(swatches.Select(swatch => swatch.Name).Distinct().Count(), Is.EqualTo(swatches.Count), "Names are unique.");
                Assert.That(swatches.Select(swatch => swatch.Hex).Distinct().Count(), Is.EqualTo(swatches.Count), "Colours are distinct.");
                foreach (var swatch in swatches)
                {
                    Assert.That(ColorUtility.TryParseHtmlString("#" + swatch.Hex, out _), Is.True, swatch.Name);
                    Assert.That(CharacterPalettes.Named(palette, swatch.Name), Is.EqualTo(swatch.Colour));
                    Assert.That(CharacterPalettes.NameOf(palette, swatch.Colour), Is.EqualTo(swatch.Name));
                }
            }
            Assert.Throws<System.ArgumentException>(() => CharacterPalettes.Named(CharacterPalettes.Skin, "Not a tone"),
                "A look naming a tone the palette lacks fails loudly rather than drifting.");
        }

        /// <summary>
        /// The skin tones run the whole range, deep tones as finely as light ones: the old palette's
        /// darkest was a light tan, and no Black houseguest could be made.
        /// </summary>
        [Test]
        public void SkinTonesCoverTheRangeEvenlyDeepestIncluded()
        {
            var tones = CharacterPalettes.All(CharacterPalettes.Skin).Select(swatch => swatch.Colour).ToList();
            float Luminance(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
            Assert.That(tones.Min(Luminance), Is.LessThan(.15f), "A truly deep tone.");
            Assert.That(tones.Max(Luminance), Is.GreaterThan(.85f), "A truly fair one.");
            Assert.That(tones.Count(tone => Luminance(tone) < .4f), Is.GreaterThanOrEqualTo(8), "As many deep and brown tones as fair ones.");
            // Past about 1.45 times the green, a lit deep brown reads as maroon.
            foreach (var swatch in CharacterPalettes.Skin.Last().Swatches)
                Assert.That(swatch.Colour.r / swatch.Colour.g, Is.LessThanOrEqualTo(1.47f), swatch.Name);
        }

        /// <summary>
        /// The tan and deep rows offer an undertone as the fair ones do, not only a darker brown: a
        /// golden tone, a red one up against the maroon cap, and a neutral one, told apart by hue and
        /// saturation rather than by more red. Both rows used to be one hue getting darker, while the
        /// page promised golden, olive and rosy at every depth.
        /// </summary>
        [Test]
        public void TheDeepRowsOfferUndertonesNotOnlyDepth()
        {
            float Hue(Color c) { Color.RGBToHSV(c, out var hue, out _, out _); return hue * 360f; }
            float Red(Color c) => c.r / c.g;
            foreach (var row in CharacterPalettes.Skin.Skip(2))
            {
                var tones = row.Swatches.ToDictionary(swatch => swatch.Name, swatch => swatch.Colour);
                foreach (var tone in tones)
                    Assert.That(Red(tone.Value), Is.LessThanOrEqualTo(1.47f), row.Label + " / " + tone.Key + " stays out of maroon.");
                Assert.That(tones.Values.Any(c => Hue(c) >= 28f), Is.True, row.Label + " has a golden tone.");
                Assert.That(tones.Values.Any(c => Hue(c) <= 19f && Red(c) >= 1.4f), Is.True, row.Label + " has a red tone.");
                Assert.That(tones.Values.Any(c => Red(c) <= 1.3f), Is.True, row.Label + " has a neutral tone.");
                Assert.That(tones.Values.Max(Hue) - tones.Values.Min(Hue), Is.GreaterThan(10f), row.Label + " varies in undertone, not only in depth.");
            }
        }

        /// <summary>
        /// A random houseguest takes hair and eyes from their skin. On tan and deep skin: black to
        /// dark brown hair and brown eyes nearly always, blonde or red hair and light eyes rarely.
        /// Grey and white hair rarely at any depth, and never a dyed colour. Drawn apart, half the
        /// deep-skinned houseguests Randomize made had light eyes and most had blonde, red or white
        /// hair.
        /// </summary>
        [Test]
        public void ARandomHouseguestsHairAndEyesFollowTheirSkin()
        {
            var random = new System.Random(20260925);
            var lightEyes = new[] { "Green", "Grey-green", "Blue", "Light blue", "Grey-blue", "Grey" };
            var brownEyes = new[] { "Black-brown", "Dark brown", "Brown", "Light brown" };
            var darkHair = new[] { "Jet black", "Black", "Soft black", "Dark brown" };
            var blondeOrRed = new[] { "Auburn", "Copper", "Strawberry", "Dark blonde", "Honey blonde", "Golden blonde", "Platinum" };
            var grey = new[] { "Silver", "White", "Salt and pepper" };
            var natural = CharacterPalettes.Hair[0].Swatches.Select(swatch => swatch.Name).ToList();
            float Share(List<string> drawn, string[] names) => drawn.Count(name => names.Contains(name)) / (float)drawn.Count;
            for (int depth = 0; depth < CharacterPalettes.Skin.Length; depth++)
            {
                string row = CharacterPalettes.Skin[depth].Label;
                var hair = new List<string>();
                var eyes = new List<string>();
                foreach (var tone in CharacterPalettes.Skin[depth].Swatches)
                {
                    Assert.That(CharacterPalettes.DepthOf(tone.Colour), Is.EqualTo(depth), tone.Name + " is its row's depth.");
                    for (int draw = 0; draw < 500; draw++)
                    {
                        hair.Add(CharacterPalettes.RandomHair(tone.Colour, random).Name);
                        eyes.Add(CharacterPalettes.RandomEyes(tone.Colour, random).Name);
                    }
                }
                Assert.That(hair.All(name => natural.Contains(name)), Is.True, row + ": natural hair only, never a dye.");
                Assert.That(Share(hair, grey), Is.LessThan(.04f), row + ": grey or white hair is rare.");
                if (depth == 0)
                {
                    // Everything can come up somewhere, so a swatch added without a weight is noticed.
                    Assert.That(natural.Except(hair), Is.Empty, "Every natural shade is drawn.");
                    Assert.That(CharacterPalettes.Eyes[0].Swatches.Select(swatch => swatch.Name).Except(eyes), Is.Empty, "Every eye colour is drawn.");
                }
                if (depth < 2) continue;
                Assert.That(Share(eyes, lightEyes), Is.LessThan(.06f), row + ": light eyes are rare.");
                Assert.That(Share(eyes, brownEyes), Is.GreaterThan(.85f), row + ": brown eyes nearly always.");
                Assert.That(Share(hair, blondeOrRed), Is.LessThan(.06f), row + ": blonde or red hair is rare.");
                Assert.That(Share(hair, darkHair), Is.GreaterThan(.75f), row + ": black to dark brown hair mostly.");
            }
            // The skin itself is drawn evenly, the deep as often as the fair.
            var depths = Enumerable.Range(0, 4000).Select(_ => CharacterPalettes.DepthOf(CharacterPalettes.RandomSkin(random).Colour)).ToList();
            for (int depth = 0; depth < CharacterPalettes.Skin.Length; depth++)
                Assert.That(depths.Count(value => value == depth) / 4000f, Is.InRange(.2f, .3f), CharacterPalettes.Skin[depth].Label);
        }

        /// <summary>
        /// A face detail - freckles, makeup, an older face - is the person's, not the outfit's: worn
        /// on every outfit at once, and kept on in the water and in bed when the outer layers come off.
        /// </summary>
        [Test]
        public void FaceDetailsAreThePersonsOnEveryOutfitInTheWaterAndInBed()
        {
            var freckles = new AppearanceItem { Id = "uma-freckles", Label = "Freckles", Slot = "Face" };
            var catalog = new ItemCatalog(freckles);
            var appearance = new CharacterAppearance { provider = "uma", bodyId = "HumanMaleDCS" };
            foreach (string id in new[] { CharacterOutfits.Everyday, CharacterOutfits.Competition })
                appearance.outfits.Add(new CharacterOutfit
                {
                    id = id,
                    wardrobe = new[] { "Hair", "BottomUnderlayer", "Chest", "Legs" }
                        .Select(slot => new AppearanceWardrobe { slot = slot, itemId = slot.ToLowerInvariant() }).ToList(),
                });
            AppearanceEditing.Wear(appearance, freckles, catalog);
            foreach (var outfit in appearance.outfits)
                Assert.That(outfit.wardrobe.Count(item => item.slot == "Face" && item.itemId == freckles.Id), Is.EqualTo(1), outfit.id + " wears the freckles.");
            foreach (string context in new[] { CharacterOutfits.Swimwear, CharacterOutfits.Sleepwear })
            {
                var dressed = CharacterOutfits.ForActivity(appearance, context);
                Assert.That(dressed.activeOutfit, Is.EqualTo(context), context + " is derived from the everyday set.");
                var slots = dressed.outfits.Single(outfit => outfit.id == context).wardrobe.Select(item => item.slot).ToList();
                Assert.That(slots, Does.Contain("Face"), context + " keeps the face's details.");
                Assert.That(slots, Does.Not.Contain("Legs"), context + " takes the outer layers off.");
            }
        }

        private sealed class ItemCatalog : ICharacterAppearanceCatalog
        {
            private readonly List<AppearanceItem> items;
            public ItemCatalog(params AppearanceItem[] items) { this.items = items.ToList(); }
            public IReadOnlyList<AppearanceBodyOption> Bodies { get; } = new AppearanceBodyOption[0];
            public IReadOnlyList<AppearanceControl> Controls { get; } = new AppearanceControl[0];
            public IReadOnlyList<AppearanceItem> Items => items;
            public CharacterAppearance Materialize(CharacterAppearance appearance) => appearance.Clone();
            public CharacterAppearance ChangeBody(CharacterAppearance appearance, string bodyId) => appearance.Clone();
        }

        [Test]
        public void BuiltHairAndAccessoriesHaveDistinctIdsSlotsAndPictures()
        {
            var ids = ProceduralHair.Styles.Select(style => style.Id).Concat(ProceduralAccessories.Items.Select(item => item.Id)).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            foreach (var style in ProceduralHair.Styles)
            {
                Assert.That(ProceduralHair.IsProcedural(style.Id), Is.True);
                Assert.That(ProceduralAccessories.IsProcedural(style.Id), Is.False);
            }
            foreach (var item in ProceduralAccessories.Items)
            {
                Assert.That(ProceduralAccessories.Slots, Does.Contain(item.Slot), item.Id);
                Assert.That(ProceduralHair.IsProcedural(item.Id), Is.False);
            }
            foreach (string slot in ProceduralAccessories.Slots)
                Assert.That(ProceduralAccessories.Items.Any(item => item.Slot == slot), Is.True, slot + " has something to wear.");
            // Textured hair the library lacked: close crops through to long protective styles.
            foreach (string wanted in new[] { "gs-hair-fade", "gs-hair-afro", "gs-hair-locs", "gs-hair-braids", "gs-hair-cornrows" })
                Assert.That(ProceduralHair.Find(wanted), Is.Not.Null, wanted);

            var pictures = new List<Color[]>();
            foreach (string id in ids)
            {
                var sprite = GrownThumbnails.For(id);
                Assert.That(sprite, Is.Not.Null, id + " has a picture.");
                Assert.That(GrownThumbnails.For(id), Is.SameAs(sprite), "Drawn once and shared.");
                var pixels = sprite.texture.GetPixels();
                Assert.That(pixels.Count(pixel => pixel.a > .5f), Is.GreaterThan(pixels.Length / 8), id + " is drawn, not blank.");
                foreach (var other in pictures)
                    Assert.That(Differs(other, pixels), Is.True, id + " looks like another item.");
                pictures.Add(pixels);
            }
            Assert.That(GrownThumbnails.For("uma-anything"), Is.Null);
        }

        /// <summary>
        /// Every houseguest on both rosters has their glamour photo in the build, square as the ring
        /// needs it, and every kind of player a meaning to show beside the filter.
        /// </summary>
        [Test]
        public void EveryHouseguestHasAGlamourPhotoAndEveryKindOfPlayerAMeaning()
        {
            foreach (var roster in new[] { Simulation.CastTemplates.Roster.Regular, Simulation.CastTemplates.Roster.AllStars })
                foreach (var template in Simulation.CastTemplates.In(roster))
                {
                    var photo = Resources.Load<Texture2D>("Portraits/Glamour/" + template.Id);
                    Assert.That(photo, Is.Not.Null, template.Name + " has a glamour photo.");
                    Assert.That(photo.width, Is.EqualTo(photo.height), template.Name + "'s photo is square for the ring.");
                    Assert.That(photo.width, Is.GreaterThanOrEqualTo(256));
                }
            foreach (string kind in Simulation.CastTemplates.Categories)
                Assert.That(Simulation.CastTemplates.CategoryDescription(kind), Is.Not.Empty, kind);
            Assert.That(Simulation.CastTemplates.CategoryDescription(Simulation.CastTemplates.AllCategories), Is.Null);
        }

        private static bool Differs(Color[] a, Color[] b)
        {
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) + Mathf.Abs(a[i].a - b[i].a) > .1f) changed++;
            return changed > 40;
        }
    }
}
