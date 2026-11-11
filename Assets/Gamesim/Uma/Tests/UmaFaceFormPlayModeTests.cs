using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// U5: which form of a Face recipe paints the head on each race (the face-surface route; the result picks the form
    /// the contract names). Both stock forms are tag placeholders: UMA unpacks a slot entry with isPlaceholderSlot true
    /// as a slot with no asset and hands its overlays to the built slots carrying its tag. The UDIM-id placeholder (one
    /// entry with the slot id UMA30_Body_UDIM1001_slot and the tag Face, as the stock Freckled_Light_U recipe has it) and
    /// the Face placeholder (the stock Adult_F recipe's 'Face Placeholder' entry) are each built on both races. Each case
    /// first asserts that its copied recipe's one entry is still a placeholder tagged Face (this fixture proves the
    /// placeholder form only; the asset form, isPlaceholderSlot false, is not offered and not tested), then what the
    /// form must do to be the contract's: the built recipe holds exactly one slot made from the head slot, the recipe's
    /// overlay is in that slot's list, and the generated head texture under it differs from a control build that wears
    /// the same recipe with a transparent overlay (so the recipe's shared colours cannot pass for the paint), in the
    /// paint's own direction. A red case is information (that form does not work on that race), never something to weaken.
    ///
    /// <para>The overlay is a runtime copy of the stock one with a solid texture, so the paint is unmistakable; nothing
    /// is written to the project, and every runtime entry is taken out of the UMA index again.</para>
    /// </summary>
    public sealed class UmaFaceFormPlayModeTests
    {
        private const string HeadSlot = "UMA30_Body_UDIM1001_slot";
        private const string UdimPlaceholderSource = "Freckled_Light_U_Wardrobe", UdimPlaceholderOverlay = "Freckled_Light_U_Overlay";
        private const string FacePlaceholderSource = "Adult_F_Wardrobe", FacePlaceholderOverlay = "Adult_F_Overlay";
        private static readonly Color Paint = new Color(1f, 0f, 1f, 1f), Clear = new Color(0f, 0f, 0f, 0f);

        private GameObject stage;
        private readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();
        private readonly List<(Type type, string name)> indexed = new List<(Type, string)>();
        private int indexCount;

        [SetUp]
        public void SetUp()
        {
            // UMA warns about bones some parts lack; that is UMA's.
            LogAssert.ignoreFailingMessages = true;
            stage = new GameObject("face form stage");
            indexCount = UMAAssetIndexer.Instance.SerializedItems.Count;
        }

        [TearDown]
        public void TearDown()
        {
            var index = UMAAssetIndexer.Instance;
            foreach (var (type, name) in indexed) index.RemoveAsset(type, name, refresh: false);
            while (index.SerializedItems.Count > indexCount && index.SerializedItems.Last() == null) index.SerializedItems.RemoveAt(index.SerializedItems.Count - 1);
            indexed.Clear();
            if (stage != null) UnityEngine.Object.Destroy(stage);
            foreach (var item in made) if (item != null) UnityEngine.Object.Destroy(item);
            made.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest] public IEnumerator UdimIdPlaceholderForm_PaintsTheOneHeadSlot_OnTheMaleBody() => Case(UmaCastLibrary.MaleRace, UdimPlaceholderSource, UdimPlaceholderOverlay);
        [UnityTest] public IEnumerator UdimIdPlaceholderForm_PaintsTheOneHeadSlot_OnTheFemaleBody() => Case(UmaCastLibrary.FemaleRace, UdimPlaceholderSource, UdimPlaceholderOverlay);
        [UnityTest] public IEnumerator FacePlaceholderForm_PaintsTheOneHeadSlot_OnTheMaleBody() => Case(UmaCastLibrary.MaleRace, FacePlaceholderSource, FacePlaceholderOverlay);
        [UnityTest] public IEnumerator FacePlaceholderForm_PaintsTheOneHeadSlot_OnTheFemaleBody() => Case(UmaCastLibrary.FemaleRace, FacePlaceholderSource, FacePlaceholderOverlay);

        private IEnumerator Case(string race, string sourceRecipe, string sourceOverlay)
        {
            yield return null;
            string form = sourceRecipe == UdimPlaceholderSource ? "udim-id placeholder" : "face placeholder";
            var index = UMAAssetIndexer.Instance;
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Generator, Is.Not.Null, "UMA has a generator");

            // The probe recipe paints solid magenta; the control wears the same recipe with a transparent overlay, so the
            // two builds differ in the paint alone (the copied recipe's shared colours are on both).
            var probe = MakeRecipe(race, sourceRecipe, sourceOverlay, Paint, "paint");
            var control = MakeRecipe(race, sourceRecipe, sourceOverlay, Clear, "clear");

            // The precondition, read as UMA reads it (the unpacked slot list): the one entry is a placeholder tagged Face.
            var entries = probe.recipe.PackedLoad().slotsV3.Where(slot => slot != null && !string.IsNullOrEmpty(slot.id)).ToArray();
            string described = string.Join("; ", entries.Select(e => "'" + e.id + "' isPlaceholderSlot " + e.isPlaceholderSlot + " Tags [" + string.Join(", ", e.Tags ?? new string[0]) + "]"));
            Assert.That(entries.Length == 1 && entries[0].isPlaceholderSlot && entries[0].Tags != null && entries[0].Tags.Contains("Face"), Is.True,
                "the stock recipe " + sourceRecipe + "'s form changed: U5 proves the placeholder form only (one slot entry, isPlaceholderSlot true, tagged Face); it has " + described);

            var plain = new Built();
            yield return Build(race, control.recipe, plain);
            var painted = new Built();
            yield return Build(race, probe.recipe, painted);

            // Exactly one built slot made from the head slot (UMA's baked copy of it counts as it).
            var slots = painted.Avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).ToArray();
            var heads = slots.Where(slot => slot.slotName != null && (slot.slotName == HeadSlot || slot.slotName.StartsWith(HeadSlot + "_baked", StringComparison.Ordinal))).ToArray();
            var receivers = slots.Where(slot => slot.GetOverlayList().Any(o => o != null && o.overlayName == probe.overlay.name)).Select(slot => slot.slotName).ToArray();
            Debug.Log("[face form] " + form + " form on " + race + ": entry " + described + "; the overlay went to [" + string.Join(", ", receivers) + "]");
            Debug.Log("[face form] " + form + " form on " + race + ": built slots " + string.Join(", ", slots.Select(slot => slot.slotName + "[" + string.Join("+", slot.GetOverlayList().Where(o => o != null).Select(o => o.overlayName)) + "]")));
            Assert.That(heads.Length, Is.EqualTo(1), "the " + form + " form on " + race + " builds exactly one head slot; built: " + string.Join(", ", heads.Select(h => h.slotName)));
            var head = heads[0];
            Assert.That(head.GetOverlayList().Any(o => o != null && o.overlayName == probe.overlay.name), Is.True,
                "the " + form + " form's overlay is on the head slot on " + race + "; there: " + string.Join(", ", head.GetOverlayList().Where(o => o != null).Select(o => o.overlayName)));

            // The generated head texture under it differs from the control's, and toward the paint: magenta lifts red and
            // blue against green.
            var plainMean = HeadMean(plain.Avatar, out string plainWhere);
            var paintedMean = HeadMean(painted.Avatar, out string paintedWhere);
            float apart = Mathf.Abs(plainMean.r - paintedMean.r) + Mathf.Abs(plainMean.g - paintedMean.g) + Mathf.Abs(plainMean.b - paintedMean.b);
            float Magenta(Color c) => (c.r + c.b) / 2f - c.g;
            Debug.Log("[face form] " + form + " form on " + race + ": head texture mean, control (transparent overlay) " + plainMean + " (" + plainWhere + "), painted " + paintedMean
                + " (" + paintedWhere + "), apart " + apart + ", magenta " + Magenta(plainMean) + " -> " + Magenta(paintedMean));
            Assert.That(apart, Is.GreaterThan(.05f), "the " + form + " form's paint reaches the generated head texture on " + race);
            Assert.That(Magenta(paintedMean) - Magenta(plainMean), Is.GreaterThan(.02f), "and it is the magenta paint that reaches it on " + race);
        }

        /// <summary>A runtime copy of a stock Face recipe for one race, naming a copy of its overlay filled with one colour; both join the index.</summary>
        private (UMAWardrobeRecipe recipe, OverlayDataAsset overlay) MakeRecipe(string race, string sourceRecipe, string sourceOverlay, Color fill, string what)
        {
            var index = UMAAssetIndexer.Instance;
            var stockOverlay = index.GetAsset<OverlayDataAsset>(sourceOverlay);
            Assert.That(stockOverlay, Is.Not.Null, sourceOverlay + " is in the Global Library");
            string tag = what + "_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var overlay = UnityEngine.Object.Instantiate(stockOverlay);
            overlay.name = "GamesimFaceProbe_" + tag;
            overlay._oldOverlayName = overlay.name;
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false) { name = overlay.name + "_Texture" };
            texture.SetPixels(Enumerable.Repeat(fill, 256 * 256).ToArray());
            texture.Apply();
            var textures = overlay.textureList;
            Assert.That(textures, Is.Not.Empty, sourceOverlay + " has a texture to replace");
            textures[0] = texture;
            made.Add(overlay); made.Add(texture);
            Assert.That(index.AddAssetItem(new AssetItem(typeof(OverlayDataAsset), overlay.name, "", overlay), noDirty: true), Is.True, "the probe overlay joins the index");
            indexed.Add((typeof(OverlayDataAsset), overlay.name));

            var stockRecipe = index.GetAsset<UMAWardrobeRecipe>(sourceRecipe);
            Assert.That(stockRecipe, Is.Not.Null, sourceRecipe + " is in the Global Library");
            Assert.That(stockRecipe.recipeString, Does.Contain("\"" + sourceOverlay + "\""), sourceRecipe + " names " + sourceOverlay);
            var recipe = UnityEngine.Object.Instantiate(stockRecipe);
            recipe.name = "GamesimFaceProbe_Recipe_" + tag;
            recipe.compatibleRaces = new List<string> { race };
            recipe.recipeString = stockRecipe.recipeString.Replace("\"" + sourceOverlay + "\"", "\"" + overlay.name + "\"");
            recipe.hideFlags = HideFlags.DontSave;
            made.Add(recipe);
            Assert.That(recipe.wardrobeSlot, Is.EqualTo("Face"));
            Assert.That(index.AddAssetItem(new AssetItem(typeof(UMAWardrobeRecipe), recipe.name, "", recipe), noDirty: true), Is.True, "the probe recipe joins the index");
            indexed.Add((typeof(UMAWardrobeRecipe), recipe.name));
            return (recipe, overlay);
        }

        private sealed class Built { public DynamicCharacterAvatar Avatar; }

        /// <summary>A bare body of the race (as the game sets an avatar up, no default recipes), wearing <paramref name="recipe"/> if one is given.</summary>
        private IEnumerator Build(string race, UMAWardrobeRecipe recipe, Built into)
        {
            var root = new GameObject("face form body " + race + " " + (recipe != null ? recipe.name : "bare"));
            root.transform.SetParent(stage.transform, false);
            root.transform.localPosition = new Vector3(2f * stage.transform.childCount, 0f, 0f);
            root.AddComponent<Animator>();
            var avatar = root.AddComponent<DynamicCharacterAvatar>();
            avatar.activeRace.name = race;
            avatar.editorTimeGeneration = false;
            avatar.BuildCharacterEnabled = true;
            avatar.preloadWardrobeRecipes.loadDefaultRecipes = false;
            avatar.preloadWardrobeRecipes.recipes.Clear();
            if (recipe != null) avatar.preloadWardrobeRecipes.recipes.Add(new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe));
            int updates = 0;
            void Count(UMAData data) => updates++;
            avatar.OnCharacterUpdated += Count;
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (updates == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            avatar.OnCharacterUpdated -= Count;
            Assert.That(updates, Is.GreaterThan(0), "a " + race + " body (" + (recipe != null ? recipe.name : "bare") + ") raised CharacterUpdated within 60 s of real time");
            // The generated textures are drawn by the end of the frame the build finishes in.
            yield return null;
            into.Avatar = avatar;
        }

        /// <summary>The mean colour of the generated albedo inside the head slot's atlas rectangle (the whole texture if none is found).</summary>
        private static Color HeadMean(DynamicCharacterAvatar avatar, out string where)
        {
            foreach (var generated in avatar.umaData.generatedMaterials.materials)
            {
                if (generated?.materialFragments == null) continue;
                var fragment = generated.materialFragments.FirstOrDefault(f => f?.slotData?.slotName != null
                    && (f.slotData.slotName == HeadSlot || f.slotData.slotName.StartsWith(HeadSlot + "_baked", StringComparison.Ordinal)));
                if (fragment == null) continue;
                Texture texture = generated.resultingAtlasList != null && generated.resultingAtlasList.Length > 0 ? generated.resultingAtlasList[0] : null;
                if (texture == null && generated.material != null && generated.material.HasProperty("_BaseMap")) texture = generated.material.GetTexture("_BaseMap");
                Assert.That(texture, Is.Not.Null, "the head slot's generated material has an albedo");
                var pixels = Read(texture);
                var region = fragment.atlasRegion;
                int x0 = 0, y0 = 0, x1 = texture.width, y1 = texture.height;
                if (region.width > 0 && region.height > 0 && region.xMax <= texture.width + 1 && region.yMax <= texture.height + 1)
                {
                    x0 = Mathf.Clamp((int)region.xMin, 0, texture.width); x1 = Mathf.Clamp((int)region.xMax, 0, texture.width);
                    y0 = Mathf.Clamp((int)region.yMin, 0, texture.height); y1 = Mathf.Clamp((int)region.yMax, 0, texture.height);
                }
                where = texture.width + "x" + texture.height + " rect " + x0 + "," + y0 + " to " + x1 + "," + y1;
                double r = 0, g = 0, b = 0;
                int counted = 0;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        var pixel = pixels[y * texture.width + x];
                        if (pixel.r + pixel.g + pixel.b < .03f) continue;
                        r += pixel.r; g += pixel.g; b += pixel.b; counted++;
                    }
                Assert.That(counted, Is.GreaterThan(100), "the head's generated texture was read back (" + where + ")");
                return new Color((float)(r / counted), (float)(g / counted), (float)(b / counted), 1f);
            }
            Assert.Fail("no generated material holds a fragment of " + HeadSlot);
            where = null;
            return Color.clear;
        }

        /// <summary>A texture's pixels read back from the GPU, the active render target restored after.</summary>
        private static Color[] Read(Texture texture)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply();
                return readable.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }
    }
}
