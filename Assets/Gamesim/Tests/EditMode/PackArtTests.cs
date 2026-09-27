using System.Linq;
using System.Reflection;
using Gamesim.Editor;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Every pack sprite the HUD names resolves, and resolves to what the importer made of it.
    /// A path that does not load is not an error anywhere else: the caller draws its procedural
    /// fallback and the screen quietly stops matching its mockup.
    /// </summary>
    public sealed class PackArtTests
    {
        private static (string Name, string Path)[] Named() =>
            typeof(PackArt).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (field.Name, (string)field.GetRawConstantValue()))
                .ToArray();

        [Test]
        public void EveryNamedPackSpriteLoads()
        {
            var named = Named();
            Assert.That(named, Is.Not.Empty);
            var missing = named.Where(entry => UiTheme.Pack(entry.Path) == null)
                .Select(entry => entry.Name + " (" + entry.Path + ")").ToArray();
            Assert.That(missing, Is.Empty, "These do not load from Resources/Packs: " + string.Join(", ", missing));
        }

        /// <summary>
        /// A nine-slice name must have come through the importer as a sliced UI sprite with its
        /// authored border, or <see cref="UiTheme.PackSliced"/> stretches the corners.
        /// </summary>
        [Test]
        public void EveryNamedPackSpriteIsTheKindTheCatalogueSays()
        {
            foreach (var (name, path) in Named())
            {
                string asset = "Assets/Gamesim/Resources/Packs/" + path + ".png";
                Assert.That(UiPackCatalogue.TryGet(asset, out var entry), Is.True, name + " is not in the catalogue.");
                // Packs 1-5 name their 9-slices; Kit 6's manifest says which of its sprites are sliced,
                // and the catalogue carries that - so a Kit 6 name has only to be a UI sprite.
                bool kit6 = path.StartsWith("Kit6_Refinement/");
                bool sliced = kit6 ? entry.Kind == UiPackCatalogue.Kind.UiSliced : path.EndsWith("_9slice");
                Assert.That(entry.Kind, Is.EqualTo(sliced ? UiPackCatalogue.Kind.UiSliced : UiPackCatalogue.Kind.UiSprite), name);
                var sprite = UiTheme.Pack(path);
                if (sliced) Assert.That(sprite.border, Is.EqualTo(entry.Border), name + "'s slice border.");
            }
        }

        [Test]
        public void PackSlicedSolvesTheMultiplierFromTheBorderAsked()
        {
            var sprite = UiTheme.Pack(PackArt.ButtonSecondary);
            float authored = Mathf.Max(sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w);
            var image = new GameObject("Probe", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            try
            {
                Assert.That(UiTheme.PackSliced(image, PackArt.ButtonSecondary, 11f), Is.True);
                Assert.That(image.sprite, Is.SameAs(sprite));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                // What the canvas draws: the authored border over the multiplier, in canvas units.
                float drawn = authored / image.pixelsPerUnitMultiplier * (100f / sprite.pixelsPerUnit);
                Assert.That(drawn, Is.EqualTo(11f).Within(1e-3f));
                Assert.That(image.color, Is.EqualTo(Color.white));
            }
            finally { Object.DestroyImmediate(image.gameObject); }
        }

        [Test]
        public void AMissingPackSpriteLeavesTheImageAsItWas()
        {
            var image = new GameObject("Probe", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            try
            {
                image.color = Color.red;
                Assert.That(UiTheme.PackSliced(image, "Pack9_Nowhere/none_9slice", 10f), Is.False);
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.color, Is.EqualTo(Color.red));
                Assert.That(UiTheme.Pack(null), Is.Null);
            }
            finally { Object.DestroyImmediate(image.gameObject); }
        }
    }
}
