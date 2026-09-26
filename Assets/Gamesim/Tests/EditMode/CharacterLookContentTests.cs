using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The colours a houseguest is made of, and the hair and accessories built in code: named and
    /// distinct, covering every complexion, each with a picture for the creator.
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
