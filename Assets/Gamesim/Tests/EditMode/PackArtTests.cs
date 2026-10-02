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
                // and the catalogue carries that - so a Kit 6 name has only to be a UI sprite. Pack 9
                // slices every edge and fill of a frame (card_edge_rest, panel_fill, action_tile_focus_edge).
                bool kit6 = path.StartsWith("Kit6_Refinement/");
                bool sliced = kit6 ? entry.Kind == UiPackCatalogue.Kind.UiSliced
                    : path.StartsWith(Pack9Root) ? Pack9Frame(path)
                    : path.EndsWith("_9slice");
                Assert.That(entry.Kind, Is.EqualTo(sliced ? UiPackCatalogue.Kind.UiSliced : UiPackCatalogue.Kind.UiSprite), name);
                var sprite = UiTheme.Pack(path);
                if (sliced) Assert.That(sprite.border, Is.EqualTo(entry.Border), name + "'s slice border.");
            }
        }

        /// <summary>
        /// <see cref="EndScreenKit.Frame"/> draws a frame out past its rect by the glow baked around
        /// it, and it knows which frames glow only from its own list. A frame the list misses is drawn
        /// 30 px inside its rect - a card a size too small - and nothing reports it; a frame it holds
        /// wrongly is drawn 30 px too large. So the list is every named frame the catalogue measured
        /// inside a 38 px glow, and nothing else, and every other Pack 7 or Pack 8 frame sits 8 px in,
        /// the one other depth the kit allows for.
        /// </summary>
        [Test]
        public void TheGlowSetIsEveryNamedFrameTheCatalogueMeasuredInsideAGlow()
        {
            var glow = new Vector4(38f, 38f, 38f, 38f);
            var deep = new System.Collections.Generic.List<string>();
            foreach (var (name, path) in Named())
            {
                if (!UiPackCatalogue.TryGet(Asset(path), out var entry) || entry.Kind != UiPackCatalogue.Kind.UiSliced) continue;
                if (entry.BodyInset == glow) { deep.Add(path); continue; }
                if (path.StartsWith("Pack7_") || path.StartsWith("Pack8_"))
                    Assert.That(entry.BodyInset, Is.EqualTo(new Vector4(8f, 8f, 8f, 8f)), name + " sits at a depth the frame does not allow for.");
            }
            Assert.That(EndScreenKit.GlowingFrames, Is.EquivalentTo(deep), "The frames drawn out past their rect by a glow.");
            Assert.That(deep.Count(path => path.StartsWith("Pack7_")), Is.EqualTo(6), "Pack 7's six glowing frames.");
            Assert.That(deep.Count(path => path.StartsWith("Pack8_")), Is.EqualTo(11), "Pack 8's eleven.");
        }

        /// <summary>
        /// Pack 8 is imported whole and named once per image (PACK8-PASS-PLAN decision 9): the Icons/
        /// folder repeats Common/'s icons byte for byte and six more files repeat another, so two names
        /// for one picture would let two screens drift apart while looking alike. The relationship bars
        /// bake a fill into their pixels and are never named.
        /// </summary>
        [Test]
        public void Pack8NamesEachOfItsImagesOnceAndNoneOfItsBakedBars()
        {
            var named = Named().Where(entry => entry.Path.StartsWith("Pack8_")).ToArray();
            Assert.That(named, Has.Length.EqualTo(73), "Pack 8's names.");
            Assert.That(named.Where(entry => entry.Path.Contains("/Relationship/") || entry.Path.Contains("/Icons/")).Select(entry => entry.Name),
                Is.Empty, "A baked bar or an Icons/ twin is named.");
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var twins = named.GroupBy(entry => System.BitConverter.ToString(md5.ComputeHash(System.IO.File.ReadAllBytes(Asset(entry.Path)))))
                    .Where(group => group.Count() > 1).Select(group => string.Join(" = ", group.Select(entry => entry.Name))).ToArray();
                Assert.That(twins, Is.Empty, "One picture under two names: " + string.Join("; ", twins));
            }
        }

        private const string Packs = "Assets/Gamesim/Resources/Packs/";
        private const string Pack9Root = "Pack9_CeremonyFinale/";

        private static string Asset(string path) => Packs + path + ".png";

        /// <summary>Pack 9's frames, by the segment of the name (bb_ui_pack9.kind_for): an edge or a fill.</summary>
        private static bool Pack9Frame(string path)
        {
            var segments = path.Substring(path.LastIndexOf('/') + 1).Split('_');
            return segments.Contains("edge") || segments.Contains("fill");
        }

        /// <summary>
        /// Pack 9 is imported whole and every one of its 62 images is named exactly once, by the rule
        /// Pack9{Category}{File} (UI-UX-PASS-PLAN P0), so the slices built against the names in parallel
        /// can write them without looking. Unlike Pack 8, the pack's byte-for-byte twins are each named:
        /// the pack is laid out by screen and each screen's slice draws its own folder's copy. The
        /// twins are pinned here - 62 files, 33 pictures, 23 groups - so a later pass that folds them,
        /// or a re-export that changes one, is seen.
        /// </summary>
        [Test]
        public void Pack9NamesEachOfItsImagesOnce()
        {
            var named = Named().Where(entry => entry.Path.StartsWith(Pack9Root)).ToArray();
            var listed = UiPackCatalogue.All.Select(entry => entry.Path)
                .Where(path => path.StartsWith(Packs + Pack9Root))
                .Select(path => path.Substring(Packs.Length, path.Length - Packs.Length - ".png".Length)).ToArray();
            Assert.That(listed, Has.Length.EqualTo(62), "The catalogue's Pack 9.");
            Assert.That(named.Select(entry => entry.Path), Is.EquivalentTo(listed), "Every image of Pack 9 named, and none twice.");
            foreach (var (name, path) in named)
            {
                var parts = path.Substring(Pack9Root.Length).Split('/');
                Assert.That(parts, Has.Length.EqualTo(2), path + " sits one folder under the pack.");
                string expected = "Pack9" + parts[0] + string.Concat(parts[1].Split('_').Select(piece => char.ToUpperInvariant(piece[0]) + piece.Substring(1)));
                Assert.That(name, Is.EqualTo(expected), path + " is named by the rule Pack9{Category}{File}.");
            }
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var pictures = named.GroupBy(entry => System.BitConverter.ToString(md5.ComputeHash(System.IO.File.ReadAllBytes(Asset(entry.Path))))).ToArray();
                var twins = pictures.Where(group => group.Count() > 1)
                    .Select(group => string.Join(" = ", group.Select(entry => entry.Name).OrderBy(entry => entry, System.StringComparer.Ordinal)))
                    .OrderBy(group => group, System.StringComparer.Ordinal).ToArray();
                Assert.That(pictures, Has.Length.EqualTo(33), "The pack's 33 pictures under 62 names: " + string.Join("; ", twins));
                Assert.That(twins, Has.Length.EqualTo(23), "Eleven Icons/ twins of the per-screen icons (ic_key is its own drawing), and twelve frame or ring twins.");
                Assert.That(twins.Count(group => group.Contains("Pack9Icons")), Is.EqualTo(11), string.Join("; ", twins));
            }
        }

        /// <summary>
        /// Pack 8's draw slots are named _9slice and import sliced, but they are circles with a stretch
        /// of 12 to 14 px: a Sliced Image draws them as pills. They are drawn whole, their aspect
        /// kept, and the empty slot and the lit one - one padded 8 px, one glowing 38 - show a
        /// circle of the same size.
        /// </summary>
        [Test]
        public void TheDrawSlotsAreDrawnWholeAtTheSameSize()
        {
            var parent = new GameObject("Probe", typeof(RectTransform)).GetComponent<RectTransform>();
            try
            {
                foreach (var path in new[] { PackArt.Pack8DrawSlotEmpty, PackArt.Pack8DrawSlotFilled })
                {
                    var slot = EndScreenKit.Whole("Slot", parent, path, Vector2.zero, 60f);
                    Assert.That(slot, Is.Not.Null, path);
                    Assert.That(slot.sprite, Is.SameAs(UiTheme.Pack(path)));
                    Assert.That(slot.type, Is.EqualTo(Image.Type.Simple), path + " is drawn whole, not sliced.");
                    Assert.That(slot.preserveAspect, Is.True);
                    Assert.That(slot.raycastTarget, Is.False, "A slot is not a control.");
                    Assert.That(UiPackCatalogue.TryGet(Asset(path), out var entry), Is.True);
                    float body = slot.rectTransform.sizeDelta.x * (entry.Width - entry.BodyInset.x - entry.BodyInset.z) / entry.Width;
                    Assert.That(body, Is.EqualTo(60f).Within(.01f), path + "'s circle is the size asked.");
                }
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
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
