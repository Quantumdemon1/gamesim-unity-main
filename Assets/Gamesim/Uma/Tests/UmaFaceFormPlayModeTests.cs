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
    /// the contract names). A runtime Face recipe in the DIRECT form (one slot entry naming the head slot
    /// UMA30_Body_UDIM1001_slot, as the stock Freckled_Light_U recipe has it) and one in the PLACEHOLDER form (a slot entry
    /// handed to the first built slot tagged Face, as the stock Adult_F recipe has it), each built on both races. Each
    /// case asserts what its form must do to be the contract's: the built recipe holds exactly one slot made from the head
    /// slot, the recipe's overlay is in that slot's list, and the generated head texture under it differs from a bare
    /// build's. A red case is information (that form does not work on that race), never something to weaken.
    ///
    /// <para>The overlay is a runtime copy of the stock one with a solid texture, so the paint is unmistakable; nothing
    /// is written to the project, and every runtime entry is taken out of the UMA index again.</para>
    /// </summary>
    public sealed class UmaFaceFormPlayModeTests
    {
        private const string HeadSlot = "UMA30_Body_UDIM1001_slot";
        private const string DirectSource = "Freckled_Light_U_Wardrobe", DirectOverlay = "Freckled_Light_U_Overlay";
        private const string PlaceholderSource = "Adult_F_Wardrobe", PlaceholderOverlay = "Adult_F_Overlay";

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

        [UnityTest] public IEnumerator DirectForm_PaintsTheOneHeadSlot_OnTheMaleBody() => Case(UmaCastLibrary.MaleRace, DirectSource, DirectOverlay);
        [UnityTest] public IEnumerator DirectForm_PaintsTheOneHeadSlot_OnTheFemaleBody() => Case(UmaCastLibrary.FemaleRace, DirectSource, DirectOverlay);
        [UnityTest] public IEnumerator PlaceholderForm_PaintsTheOneHeadSlot_OnTheMaleBody() => Case(UmaCastLibrary.MaleRace, PlaceholderSource, PlaceholderOverlay);
        [UnityTest] public IEnumerator PlaceholderForm_PaintsTheOneHeadSlot_OnTheFemaleBody() => Case(UmaCastLibrary.FemaleRace, PlaceholderSource, PlaceholderOverlay);

        private IEnumerator Case(string race, string sourceRecipe, string sourceOverlay)
        {
            yield return null;
            string form = sourceRecipe == DirectSource ? "direct" : "placeholder";
            var index = UMAAssetIndexer.Instance;
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Generator, Is.Not.Null, "UMA has a generator");

            // The overlay: the stock one's copy with a solid magenta texture, under a name of its own.
            var stockOverlay = index.GetAsset<OverlayDataAsset>(sourceOverlay);
            Assert.That(stockOverlay, Is.Not.Null, sourceOverlay + " is in the Global Library");
            string tag = Guid.NewGuid().ToString("N").Substring(0, 10);
            var overlay = UnityEngine.Object.Instantiate(stockOverlay);
            overlay.name = "GamesimFaceProbe_" + form + "_" + tag;
            overlay._oldOverlayName = overlay.name;
            var paint = new Texture2D(256, 256, TextureFormat.RGBA32, false) { name = overlay.name + "_Texture" };
            paint.SetPixels(Enumerable.Repeat(new Color(1f, 0f, 1f, 1f), 256 * 256).ToArray());
            paint.Apply();
            var textures = overlay.textureList;
            Assert.That(textures, Is.Not.Empty, sourceOverlay + " has a texture to replace");
            textures[0] = paint;
            made.Add(overlay); made.Add(paint);
            Assert.That(index.AddAssetItem(new AssetItem(typeof(OverlayDataAsset), overlay.name, "", overlay), noDirty: true), Is.True, "the probe overlay joins the index");
            indexed.Add((typeof(OverlayDataAsset), overlay.name));

            // The recipe: the stock one's copy, for this race, naming the probe overlay instead of its own.
            var stockRecipe = index.GetAsset<UMAWardrobeRecipe>(sourceRecipe);
            Assert.That(stockRecipe, Is.Not.Null, sourceRecipe + " is in the Global Library");
            Assert.That(stockRecipe.recipeString, Does.Contain("\"" + sourceOverlay + "\""), sourceRecipe + " names " + sourceOverlay);
            var recipe = UnityEngine.Object.Instantiate(stockRecipe);
            recipe.name = "GamesimFaceProbe_" + form + "_Recipe_" + tag;
            recipe.compatibleRaces = new List<string> { race };
            recipe.recipeString = stockRecipe.recipeString.Replace("\"" + sourceOverlay + "\"", "\"" + overlay.name + "\"");
            recipe.hideFlags = HideFlags.DontSave;
            made.Add(recipe);
            Assert.That(recipe.wardrobeSlot, Is.EqualTo("Face"));
            Assert.That(index.AddAssetItem(new AssetItem(typeof(UMAWardrobeRecipe), recipe.name, "", recipe), noDirty: true), Is.True, "the probe recipe joins the index");
            indexed.Add((typeof(UMAWardrobeRecipe), recipe.name));
            string formSlot = recipe.PackedLoad().slotsV3.First(slot => slot != null && !string.IsNullOrEmpty(slot.id)).id;
            Debug.Log("[face form] " + form + " form on " + race + ": the recipe's slot entry is '" + formSlot + "'");

            var bare = new Built();
            yield return Build(race, null, bare);
            var painted = new Built();
            yield return Build(race, recipe, painted);

            // Exactly one built slot made from the head slot (UMA's baked copy of it counts as it).
            var slots = painted.Avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).ToArray();
            var heads = slots.Where(slot => slot.slotName != null && (slot.slotName == HeadSlot || slot.slotName.StartsWith(HeadSlot + "_baked", StringComparison.Ordinal))).ToArray();
            Debug.Log("[face form] " + form + " form on " + race + ": built slots " + string.Join(", ", slots.Select(slot => slot.slotName + "[" + string.Join("+", slot.GetOverlayList().Where(o => o != null).Select(o => o.overlayName)) + "]")));
            Assert.That(heads.Length, Is.EqualTo(1), "the " + form + " form on " + race + " builds exactly one head slot; built: " + string.Join(", ", heads.Select(h => h.slotName)));
            var head = heads[0];
            Assert.That(head.GetOverlayList().Any(o => o != null && o.overlayName == overlay.name), Is.True,
                "the " + form + " form's overlay is on the head slot on " + race + "; there: " + string.Join(", ", head.GetOverlayList().Where(o => o != null).Select(o => o.overlayName)));

            // The generated head texture under it differs from a bare body's.
            var bareMean = HeadMean(bare.Avatar, out string bareWhere);
            var paintedMean = HeadMean(painted.Avatar, out string paintedWhere);
            float apart = Mathf.Abs(bareMean.r - paintedMean.r) + Mathf.Abs(bareMean.g - paintedMean.g) + Mathf.Abs(bareMean.b - paintedMean.b);
            Debug.Log("[face form] " + form + " form on " + race + ": head texture mean, bare " + bareMean + " (" + bareWhere + "), painted " + paintedMean + " (" + paintedWhere + "), apart " + apart);
            Assert.That(apart, Is.GreaterThan(.05f), "the " + form + " form's paint reaches the generated head texture on " + race);
        }

        private sealed class Built { public DynamicCharacterAvatar Avatar; }

        /// <summary>A bare body of the race (as the game sets an avatar up, no default recipes), wearing <paramref name="recipe"/> if one is given.</summary>
        private IEnumerator Build(string race, UMAWardrobeRecipe recipe, Built into)
        {
            var root = new GameObject("face form body " + race + (recipe != null ? " painted" : " bare"));
            root.transform.SetParent(stage.transform, false);
            root.transform.localPosition = new Vector3(recipe != null ? 2f : -2f, 0f, 0f);
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
