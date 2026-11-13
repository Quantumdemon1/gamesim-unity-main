#if GAMESIM_UMA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The wrist and neck places on a real UMA body: a watch (Hands), an arm piece such as a bracelet
    /// or wristband (Arms) and a necklace (Shoulders, the UMA twin of the code-built Neckwear place).
    /// The data side of those places is pinned by WardrobeSlotsTests and AppearanceAccessoryPlaceTests;
    /// this fixture builds the bodies.
    ///
    /// <para>Content-free. Two kinds of runtime-only piece, removed again at the end of each test:
    /// a stock recipe re-filed under the place (as the contract fixture's Ears case does), and an
    /// authored part, made the way delivered metal is: a UseExistingMaterial UMAMaterial over a lit
    /// material carrying the finish mark and its own texture, on a stock garment slot's geometry. A
    /// slot is drawn with its first overlay's UMAMaterial, so no slot asset of its own is needed.
    /// No AssetDatabase write and no lasting index edit.</para>
    /// </summary>
    [TestFixture]
    public sealed class UmaWristAndNeckPlayModeTests
    {
        private const string CatalogFolder = "Gamesim/CharacterCatalog";
        private static readonly string[] Places = { "Hands", "Arms", "Shoulders" };
        /// <summary>Stock clothing whose geometry the runtime pieces borrow.</summary>
        private static readonly string[] SourceSlots = { "Chest", "Legs", "Feet" };

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseColor = Shader.PropertyToID("_BaseColor"),
            Metallic = Shader.PropertyToID("_Metallic"), Smoothness = Shader.PropertyToID("_Smoothness"),
            BumpScale = Shader.PropertyToID("_BumpScale"), Surface = Shader.PropertyToID("_Surface"),
            AlphaClip = Shader.PropertyToID("_AlphaClip"), Cutoff = Shader.PropertyToID("_Cutoff");

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwned()
        {
#if UNITY_EDITOR
            content?.Dispose();
            content = null;
#endif
            foreach (var value in owned) if (value != null) Object.Destroy(value);
            owned.Clear();
        }

        // ----------------------------------------------------------------------------------- helpers

        private static string[] SlotIds(UMAWardrobeRecipe recipe) => (recipe.PackedLoad().slotsV3 ?? Array.Empty<UMAPackedRecipeBase.PackedSlotDataV3>())
            .Where(part => part != null && !string.IsNullOrEmpty(part.id)).Select(part => part.id).Distinct().ToArray();

        /// <summary>The UMA slots the recipes of an outfit build: a runtime piece must share none of them (UMA merges slots of one name).</summary>
        private static HashSet<string> WornSlotIds(UmaAppearanceCatalog catalog, CharacterAppearance appearance)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var worn in AppearanceEditing.Outfit(appearance).wardrobe)
            {
                if (UmaAppearanceCatalog.IsGrown(worn.itemId)) continue;
                var recipe = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(worn.itemId));
                if (recipe != null) taken.UnionWith(SlotIds(recipe));
            }
            return taken;
        }

        /// <summary>
        /// <paramref name="count"/> stock clothing items the body can wear whose slots are free: worn by no recipe of
        /// the outfit and by no other item picked here, and items that suppress and conflict with nothing.
        /// </summary>
        private static List<AppearanceItem> FreeSources(UmaAppearanceCatalog installed, CharacterAppearance appearance, string race, int count)
        {
            var taken = WornSlotIds(installed, appearance);
            var picked = new List<AppearanceItem>();
            foreach (var item in installed.Items.Where(value => SourceSlots.Contains(value.Slot) && !UmaAppearanceCatalog.IsGrown(value.Id)
                && value.Fits(race) && !value.PickerHidden && value.SuppressedSlots.Length == 0 && value.Conflicts.Length == 0))
            {
                var recipe = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(installed.ResolveRecipeName(item.Id));
                if (recipe == null) continue;
                var slots = SlotIds(recipe);
                if (slots.Length == 0 || slots.Any(taken.Contains)) continue;
                if (!slots.Any(id => UMAAssetIndexer.Instance.GetAsset<SlotDataAsset>(id)?.meshData?.vertexCount > 0)) continue;
                taken.UnionWith(slots);
                picked.Add(item);
                if (picked.Count == count) break;
            }
            Assert.That(picked, Has.Count.EqualTo(count), "Stock clothing for " + race + " with slots no worn recipe uses.");
            return picked;
        }

        private static CharacterAppearance Player(UmaAppearanceCatalog catalog, string race) =>
            catalog.ChangeBody(catalog.Materialize(CharacterAppearance.Preset("player")), race);

        private sealed class BuiltBody { public CharacterBody Body; public DynamicCharacterAvatar Avatar; }

        private static IEnumerator Build(IModularCharacterBodyProvider provider, CharacterAppearance appearance, Transform parent, BuiltBody into)
        {
            Assert.That(provider.TryCreate(new CharacterBodyRequest("custom-wrist-neck", "player", appearance, CharacterBuildPurpose.Studio, 1),
                parent, Color.white, out var body), Is.True);
            var state = body.Root.GetComponent<CharacterBodyBuildState>();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(state.Ready, Is.True, "The body finished building.");
            Assert.That(string.IsNullOrEmpty(state.Substitution), Is.True, "Worn as saved, with no substitute: " + state.Substitution);
            // The code-built pieces are fitted once the body is up.
            yield return null; yield return null;
            into.Body = body;
            into.Avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
            Assert.That(into.Avatar, Is.Not.Null);
        }

        /// <summary>A colour change rebuilds the body; waits for the rebuild to land.</summary>
        private static IEnumerator Rebuild(DynamicCharacterAvatar avatar, Color skin)
        {
            int updates = 0;
            void Updated(UMAData data) => updates++;
            avatar.OnCharacterUpdated += Updated;
            avatar.SetRawColor("Skin", UmaBodyProvider.SkinColour(skin), false);
            avatar.UpdateColors(true);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (updates == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            avatar.OnCharacterUpdated -= Updated;
            Assert.That(updates, Is.GreaterThan(0), "The body rebuilt.");
            yield return null;
        }

        private static GameObject Actor(List<Object> owned, string name, int place)
        {
            var actor = new GameObject(name);
            actor.transform.position = new Vector3(place * 3f, 0f, 0f);
            owned.Add(actor);
            return actor;
        }

        /// <summary>Each slot of the recipe is built on the body, with vertices.</summary>
        private static void AssertBuilt(DynamicCharacterAvatar avatar, string place, UMAWardrobeRecipe recipe, string[] slots, string what)
        {
            Assert.That(avatar.GetWardrobeItem(place)?.name, Is.EqualTo(recipe.name), what + " is worn in " + place + ".");
            var built = avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).ToList();
            foreach (string id in slots)
            {
                var slot = built.FirstOrDefault(value => value.slotName == id);
                Assert.That(slot, Is.Not.Null, what + ": " + id + " is built.");
                Assert.That(slot.asset.meshData.vertexCount, Is.GreaterThan(0), what + ": " + id + " has vertices.");
            }
        }

        private static string[] CodeBuiltPieces(CharacterBody body) => body.Root.GetComponentsInChildren<GrownPiece>()
            .Select(piece => piece.ItemId).Where(ProceduralAccessories.IsProcedural).ToArray();

        /// <summary>Counts the game's own UMA warnings (a substitution, an unavailable recipe) while a test builds.</summary>
        private sealed class UmaWarnings : IDisposable
        {
            public readonly List<string> Seen = new List<string>();
            public UmaWarnings() { Application.logMessageReceived += Count; }
            private void Count(string message, string trace, LogType type)
            {
                if (type != LogType.Log && message.StartsWith("[Gamesim.Uma]", StringComparison.Ordinal)) Seen.Add(message);
            }
            public void Dispose() { Application.logMessageReceived -= Count; }
        }

        // ----------------------------------------------------------------- runtime-only content

#if UNITY_EDITOR
        /// <summary>
        /// Runtime-only pieces registered in UMA's index, with a catalog of their own (the contract fixture's
        /// RuntimeContent, plus authored parts).
        /// </summary>
        private sealed class Content : IDisposable
        {
            public readonly UMAAssetIndexer Index = UMAAssetIndexer.Instance;
            public readonly UmaWardrobeCatalog Extension = ScriptableObject.CreateInstance<UmaWardrobeCatalog>();
            private readonly List<(Type Type, string Name)> registered = new List<(Type Type, string Name)>();
            private readonly List<Object> made = new List<Object>();
            private readonly int originalCount;

            public Content()
            {
                Extension.name = "Wrist and neck fixture content";
                originalCount = Index.SerializedItems.Count;
            }

            private void Register(Type type, Object asset)
            {
                registered.Add((type, asset.name));
                Assert.That(Index.AddAssetItem(new AssetItem(type, asset.name, "", asset), noDirty: true), Is.True, asset.name);
            }

            private void Enter(string id, UMAWardrobeRecipe recipe) =>
                Extension.entries.Add(new UmaWardrobeEntry { id = id, recipeName = recipe.name, label = "Fixture " + id, styleGroup = id });

            /// <summary>A stock recipe copied and re-filed under <paramref name="place"/>: geometry the body build can dress.</summary>
            public UMAWardrobeRecipe Copy(UmaAppearanceCatalog installed, AppearanceItem source, string id, string place)
            {
                var recipe = Object.Instantiate(Index.GetAsset<UMAWardrobeRecipe>(installed.ResolveRecipeName(source.Id)));
                recipe.name = "GamesimWristNeck-" + place + "-" + Guid.NewGuid().ToString("N");
                recipe.hideFlags = HideFlags.DontSave;
                recipe.wardrobeSlot = place;
                made.Add(recipe);
                Register(typeof(UMAWardrobeRecipe), recipe);
                Enter(id, recipe);
                return recipe;
            }

            /// <summary>
            /// A part authored as delivered metal is: URP Lit with the finish mark, its own texture with coloured,
            /// partly transparent texels, alpha clipped, drawn through a UseExistingMaterial UMAMaterial with no
            /// texture channel and no shared colour, on the geometry of <paramref name="source"/>'s first slot.
            /// </summary>
            public Authored Author(UmaAppearanceCatalog installed, AppearanceItem source, string race, string id, string place, float metallic, float smoothness)
            {
                var stock = Index.GetAsset<UMAWardrobeRecipe>(installed.ResolveRecipeName(source.Id));
                var geometry = SlotIds(stock).Select(slot => Index.GetAsset<SlotDataAsset>(slot)).First(slot => slot?.meshData?.vertexCount > 0);
                string stem = "GamesimWristNeck-" + place + "-" + Guid.NewGuid().ToString("N");

                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = stem + "-albedo", hideFlags = HideFlags.DontSave };
                var texels = new Color32[16];
                for (int i = 0; i < texels.Length; i++)
                    texels[i] = i % 4 == 0 ? new Color32(200, 160, 40, 90) : i % 4 == 1 ? new Color32(30, 140, 220, 160) : new Color32(230, 230, 235, 255);
                texture.SetPixels32(texels);
                texture.Apply(false, false);
                made.Add(texture);

                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Assert.That(shader, Is.Not.Null, "The house renders with URP's Lit shader.");
                var drawing = new Material(shader) { name = stem + "-drawing", hideFlags = HideFlags.DontSave };
                made.Add(drawing);
                drawing.SetTexture(BaseMap, texture);
                drawing.SetColor(BaseColor, Color.white);
                drawing.SetFloat(Metallic, metallic);
                drawing.SetFloat(Smoothness, smoothness);
                drawing.SetFloat(BumpScale, 1f);
                // The clip form of a watch (a solid object; blended edges sort wrongly against hair and sleeves).
                drawing.SetFloat(Surface, 0f);
                drawing.SetFloat(AlphaClip, 1f);
                drawing.SetFloat(Cutoff, .5f);
                drawing.EnableKeyword("_ALPHATEST_ON");
                drawing.SetOverrideTag("RenderType", "TransparentCutout");
                drawing.renderQueue = (int)RenderQueue.AlphaTest;
                drawing.SetOverrideTag(UmaStylizer.AuthoredPbrTag, UmaStylizer.AuthoredPbrVersion);

                var material = ScriptableObject.CreateInstance<UMAMaterial>();
                material.name = stem + "-material";
                material.hideFlags = HideFlags.DontSave;
                material.material = drawing;
                material.materialType = UMAMaterial.MaterialType.UseExistingMaterial;
                material.channels = new UMAMaterial.MaterialChannel[0];
                made.Add(material);

                var overlay = ScriptableObject.CreateInstance<OverlayDataAsset>();
                overlay.name = stem + "-overlay";
                overlay.hideFlags = HideFlags.DontSave;
                overlay.material = material;
                overlay.textureList = new Texture[0];
                overlay.textureNames = new string[0];
                overlay.overlayBlend = new OverlayDataAsset.OverlayBlend[0];
                made.Add(overlay);
                Register(typeof(OverlayDataAsset), overlay);

                var recipe = ScriptableObject.CreateInstance<UMAWardrobeRecipe>();
                recipe.name = stem + "-recipe";
                recipe.hideFlags = HideFlags.DontSave;
                recipe.recipeType = "Wardrobe";
                recipe.wardrobeSlot = place;
                recipe.DisplayValue = "Fixture " + id;
                recipe.compatibleRaces = new List<string> { race };
                var packed = new UMAData.UMARecipe { sharedColors = new OverlayColorData[0] };
                packed.SetRace(Index.GetRace(race));
                var part = new SlotData(geometry);
                var paint = new OverlayData(overlay);
                paint.colorData.name = OverlayColorData.UNSHARED;
                part.AddOverlay(paint);
                packed.SetSlot(0, part);
                recipe.Save(packed);
                made.Add(recipe);
                Register(typeof(UMAWardrobeRecipe), recipe);
                Enter(id, recipe);
                return new Authored
                {
                    Id = id, Place = place, Recipe = recipe, Slot = geometry.slotName, Overlay = overlay,
                    Drawing = drawing, Texture = texture, Metallic = metallic, Smoothness = smoothness,
                };
            }

            public UmaAppearanceCatalog Catalog()
            {
                var catalog = new UmaAppearanceCatalog(Resources.LoadAll<UmaWardrobeCatalog>(CatalogFolder).Concat(new[] { Extension }));
                Assert.That(catalog.Diagnostics, Is.Empty);
                return catalog;
            }

            public void Dispose()
            {
                foreach (var entry in registered) Index.RemoveAsset(entry.Type, entry.Name, refresh: false);
                while (Index.SerializedItems.Count > originalCount && Index.SerializedItems.Last() == null)
                    Index.SerializedItems.RemoveAt(Index.SerializedItems.Count - 1);
                foreach (var value in made) if (value != null) Object.Destroy(value);
                Object.Destroy(Extension);
            }
        }

        private sealed class Authored
        {
            public string Id, Place, Slot;
            public UMAWardrobeRecipe Recipe;
            public OverlayDataAsset Overlay;
            public Material Drawing;
            public Texture2D Texture;
            public float Metallic, Smoothness;
        }

        private Content content;

        /// <summary>
        /// The part is drawn with its own material, unchanged: the very material object (no atlas, no copy) on a
        /// submesh with triangles, its own texture, a white base colour, the authored finish, the clip form, and
        /// an overlay colour still white (no dye reached it).
        /// </summary>
        private static void AssertDrawnAsAuthored(DynamicCharacterAvatar avatar, Authored part, string when)
        {
            string what = part.Id + " " + when;
            var drawnOn = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials.Select((material, index) => (Renderer: renderer, Material: material, Index: index)))
                .Where(value => ReferenceEquals(value.Material, part.Drawing)).ToList();
            Assert.That(drawnOn, Is.Not.Empty, what + ": its own material is on the body (UseExistingMaterial draws it, never a copy).");
            Assert.That(drawnOn.Any(value => value.Renderer.sharedMesh != null && value.Index < value.Renderer.sharedMesh.subMeshCount
                && value.Renderer.sharedMesh.GetSubMesh(value.Index).indexCount > 0), Is.True, what + ": its submesh has triangles.");

            var drawing = part.Drawing;
            Assert.That(drawing.GetTexture(BaseMap), Is.SameAs(part.Texture), what + ": drawn with its own texture, not an atlas.");
            Assert.That(drawing.GetColor(BaseColor), Is.EqualTo(Color.white), what + ": no tint on its base colour.");
            Assert.That(drawing.GetFloat(Metallic), Is.EqualTo(part.Metallic).Within(.001f), what + ": its metal.");
            Assert.That(drawing.GetFloat(Smoothness), Is.EqualTo(part.Smoothness).Within(.001f), what + ": its polish.");
            Assert.That(drawing.GetFloat(BumpScale), Is.EqualTo(1f).Within(.001f), what + ": its normal strength.");
            Assert.That(UmaStylizer.KeepsAuthoredFinish(drawing), Is.True, what + ": the finish mark.");
            Assert.That(drawing.IsKeywordEnabled("_ALPHATEST_ON"), Is.True, what + ": alpha clipped.");
            Assert.That(drawing.GetFloat(AlphaClip), Is.EqualTo(1f), what + ": alpha clipped.");
            Assert.That(drawing.GetFloat(Surface), Is.EqualTo(0f), what + ": an opaque surface, not a blended one.");
            Assert.That(drawing.GetFloat(Cutoff), Is.EqualTo(.5f).Within(.001f), what + ": its cutoff.");
            Assert.That(drawing.renderQueue, Is.EqualTo((int)RenderQueue.AlphaTest), what + ": drawn in the clip queue.");

            var overlays = avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null && slot.slotName == part.Slot)
                .SelectMany(slot => slot.GetOverlayList()).Where(overlay => overlay != null && overlay.asset == part.Overlay).ToList();
            Assert.That(overlays, Is.Not.Empty, what + ": its overlay is built.");
            foreach (var overlay in overlays)
                Assert.That(overlay.colorData.channelMask, Has.All.EqualTo(Color.white), what + ": its overlay colour is white.");
        }

        // ------------------------------------------------------------------------------------- tests

        /// <summary>A Hands piece and an Arms piece are worn together on either body, each slot built with vertices.</summary>
        [UnityTest]
        public IEnumerator AWatchAndAnArmPieceAreBuiltTogetherOnEitherRace()
        {
            owned.Add(new GameObject("Wrist and neck provider", typeof(GamesimUmaCast)));
            yield return null;
            using (var warnings = new UmaWarnings())
            {
                int place = 0;
                foreach (string race in new[] { UmaCastLibrary.FemaleRace, UmaCastLibrary.MaleRace })
                {
                    content = new Content();
                    var installed = new UmaAppearanceCatalog();
                    var appearance = Player(installed, race);
                    Assert.That(appearance.bodyId, Is.EqualTo(race));
                    var sources = FreeSources(installed, appearance, race, 2);
                    var watch = content.Copy(installed, sources[0], "contract-wrist-watch", "Hands");
                    var arm = content.Copy(installed, sources[1], "contract-wrist-arm", "Arms");
                    var catalog = content.Catalog();
                    foreach (string id in new[] { "contract-wrist-watch", "contract-wrist-arm" })
                        AppearanceEditing.Wear(appearance, AppearanceEditing.Find(catalog, id), catalog);
                    var outfit = AppearanceEditing.Outfit(appearance).wardrobe;
                    Assert.That(outfit.Single(worn => worn.slot == "Hands").itemId, Is.EqualTo("contract-wrist-watch"));
                    Assert.That(outfit.Single(worn => worn.slot == "Arms").itemId, Is.EqualTo("contract-wrist-arm"), "A watch and an arm piece are worn together.");
                    Assert.That(outfit.Where(worn => worn.slot.StartsWith("Missing-", StringComparison.Ordinal)), Is.Empty);
                    Assert.That(appearance.TryValidate(out var error), Is.True, error);

                    var built = new BuiltBody();
                    yield return Build(new UmaBodyProvider(catalog), appearance, Actor(owned, "Wrist actor " + race, place++).transform, built);
                    AssertBuilt(built.Avatar, "Hands", watch, SlotIds(watch), "The watch on " + race);
                    AssertBuilt(built.Avatar, "Arms", arm, SlotIds(arm), "The arm piece on " + race);
                    content.Dispose();
                    content = null;
                }
                Assert.That(warnings.Seen, Is.Empty, "No substitution or unavailable recipe: " + string.Join(" | ", warnings.Seen));
            }
            yield return null;
        }

        /// <summary>
        /// A Shoulders piece takes the place of the code-built necklace and of the bow tie, the Neckwear pieces it
        /// is the twin of, when a save names both; the saved choice is not rewritten.
        /// </summary>
        [UnityTest]
        public IEnumerator ANecklaceInShouldersTakesTheCodeBuiltNeckwearsPlace()
        {
            owned.Add(new GameObject("Wrist and neck provider", typeof(GamesimUmaCast)));
            yield return null;
            string glasses = ProceduralAccessories.Prefix + "glasses";
            int place = 0;
            foreach (string neckwear in new[] { ProceduralAccessories.Prefix + "chain", ProceduralAccessories.Prefix + "bowtie" })
            {
                Assert.That(ProceduralAccessories.Find(neckwear)?.Slot, Is.EqualTo("Neckwear"), neckwear);
                content = new Content();
                var installed = new UmaAppearanceCatalog();
                string female = UmaCastLibrary.FemaleRace;
                var appearance = Player(installed, female);
                var recipe = content.Copy(installed, FreeSources(installed, appearance, female, 1)[0], "contract-neck", "Shoulders");
                var catalog = content.Catalog();
                var necklace = AppearanceEditing.Find(catalog, "contract-neck");
                Assert.That(necklace?.Slot, Is.EqualTo("Shoulders"));

                // A save that names both pieces of one place, as a library look or an older save may. Written
                // directly: wearing either through the creator takes the other off.
                var outfit = AppearanceEditing.Outfit(appearance);
                outfit.wardrobe.RemoveAll(worn => WardrobeSlots.IsAccessory(worn.slot));
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Neckwear", itemId = neckwear });
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Eyewear", itemId = glasses });
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Shoulders", itemId = necklace.Id });
                Assert.That(appearance.TryValidate(out var error), Is.True, error);
                var loaded = JsonUtility.FromJson<CharacterAppearance>(JsonUtility.ToJson(appearance));
                string saved = JsonUtility.ToJson(loaded);

                var built = new BuiltBody();
                yield return Build(new UmaBodyProvider(catalog), loaded, Actor(owned, "Neck actor " + neckwear, place++).transform, built);
                AssertBuilt(built.Avatar, "Shoulders", recipe, SlotIds(recipe), "The Shoulders piece");
                var pieces = CodeBuiltPieces(built.Body);
                Assert.That(pieces, Does.Contain(glasses), "The code-built pieces were fitted; a place with no UMA twin keeps its own.");
                Assert.That(pieces, Does.Not.Contain(neckwear), neckwear + " is not worn under the UMA piece on the same place.");
                Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(saved), "Dressing never rewrites the saved choice.");
                content.Dispose();
                content = null;
            }
            yield return null;
        }

        /// <summary>
        /// Authored wrist and neck parts, one in each place, are worn after a save and a reload, and keep their own
        /// finish on the first build and after a rebuild (a skin colour change runs the flattening again).
        /// </summary>
        [UnityTest]
        public IEnumerator AWristOrNeckPieceSurvivesASaveReloadAndARebuild()
        {
            owned.Add(new GameObject("Wrist and neck provider", typeof(GamesimUmaCast)));
            yield return null;
            content = new Content();
            var installed = new UmaAppearanceCatalog();
            string female = UmaCastLibrary.FemaleRace;
            var appearance = Player(installed, female);
            var sources = FreeSources(installed, appearance, female, Places.Length);
            // A finish of its own for each, so no part's values can stand in for another's.
            var parts = Places.Select((place, i) => content.Author(installed, sources[i], female, "contract-authored-" + place.ToLowerInvariant(),
                place, 1f - i * .1f, .9f - i * .15f)).ToList();
            var catalog = content.Catalog();
            foreach (var part in parts)
            {
                var item = AppearanceEditing.Find(catalog, part.Id);
                Assert.That(item?.Slot, Is.EqualTo(part.Place), part.Id);
                Assert.That(item.ColorChannels, Is.Empty, part.Id + " declares no dye channel.");
                AppearanceEditing.Wear(appearance, item, catalog);
            }
            var loaded = JsonUtility.FromJson<CharacterAppearance>(JsonUtility.ToJson(appearance));
            Assert.That(loaded.ContentKey(), Is.EqualTo(appearance.ContentKey()), "The save reads back unchanged.");
            Assert.That(catalog.Materialize(loaded).ContentKey(), Is.EqualTo(loaded.ContentKey()), "Reading a save never rewrites it.");
            foreach (var part in parts)
                Assert.That(AppearanceEditing.Outfit(loaded).wardrobe.Single(worn => worn.slot == part.Place).itemId, Is.EqualTo(part.Id));

            var built = new BuiltBody();
            yield return Build(new UmaBodyProvider(catalog), loaded, Actor(owned, "Saved actor", 0).transform, built);
            foreach (var part in parts)
            {
                AssertBuilt(built.Avatar, part.Place, part.Recipe, new[] { part.Slot }, part.Id);
                AssertDrawnAsAuthored(built.Avatar, part, "on its first build");
            }
            yield return Rebuild(built.Avatar, new Color(.36f, .22f, .15f));
            foreach (var part in parts)
            {
                AssertBuilt(built.Avatar, part.Place, part.Recipe, new[] { part.Slot }, part.Id + " after a rebuild");
                AssertDrawnAsAuthored(built.Avatar, part, "after a rebuild");
            }
            yield return null;
        }

        /// <summary>
        /// A textured authored part is drawn with its own maps on either body: the same texture object, an
        /// untinted base colour, its finish and its clip form, after the build and after a rebuild. Identity,
        /// not pixel colour: the colour tolerance of the art is checked before the part arrives.
        /// </summary>
        [UnityTest]
        public IEnumerator AnAuthoredTexturedPartIsDrawnWithItsOwnMaps()
        {
            owned.Add(new GameObject("Wrist and neck provider", typeof(GamesimUmaCast)));
            yield return null;
            int place = 0;
            foreach (string race in new[] { UmaCastLibrary.FemaleRace, UmaCastLibrary.MaleRace })
            {
                content = new Content();
                var installed = new UmaAppearanceCatalog();
                var appearance = Player(installed, race);
                var watch = content.Author(installed, FreeSources(installed, appearance, race, 1)[0], race, "contract-textured-watch", "Hands", 1f, .85f);
                var catalog = content.Catalog();
                AppearanceEditing.Wear(appearance, AppearanceEditing.Find(catalog, watch.Id), catalog);
                Assert.That(AppearanceEditing.Outfit(appearance).wardrobe.Single(worn => worn.slot == "Hands").itemId, Is.EqualTo(watch.Id));
                var texture = watch.Texture;
                Assert.That(texture.GetPixels32().Any(texel => texel.a > 0 && texel.a < 255 && (texel.r != texel.g || texel.g != texel.b)), Is.True,
                    "The texture has coloured, partly transparent texels.");

                var built = new BuiltBody();
                yield return Build(new UmaBodyProvider(catalog), appearance, Actor(owned, "Textured actor " + race, place++).transform, built);
                AssertBuilt(built.Avatar, "Hands", watch.Recipe, new[] { watch.Slot }, watch.Id + " on " + race);
                AssertDrawnAsAuthored(built.Avatar, watch, "on " + race + " after the build");
                yield return Rebuild(built.Avatar, new Color(.78f, .6f, .48f));
                AssertDrawnAsAuthored(built.Avatar, watch, "on " + race + " after a rebuild");
                Assert.That(texture != null, Is.True, "Its texture outlives the builds: UMA destroys only what it generated.");
                content.Dispose();
                content = null;
            }
            yield return null;
        }

        /// <summary>
        /// Two bodies with their own skin and outfit colours wear the same authored watch: the watch is drawn as
        /// authored on both, and each body keeps its own skin (UMA hands matching slots one overlay list).
        /// </summary>
        [UnityTest]
        public IEnumerator TwoBodiesWearingOneWatchKeepTheirOwnColours()
        {
            owned.Add(new GameObject("Wrist and neck provider", typeof(GamesimUmaCast)));
            yield return null;
            content = new Content();
            var installed = new UmaAppearanceCatalog();
            string female = UmaCastLibrary.FemaleRace;
            var first = Player(installed, female);
            var second = Player(installed, female);
            var taken = WornSlotIds(installed, first);
            taken.UnionWith(WornSlotIds(installed, second));
            var watch = content.Author(installed, FreeSources(installed, first, female, 1)[0], female, "contract-shared-watch", "Hands", 1f, .8f);
            Assert.That(taken.Contains(watch.Slot), Is.False, "Neither body wears the watch's slot otherwise.");
            var catalog = content.Catalog();
            var skins = new[] { new Color(.93f, .78f, .66f), new Color(.30f, .19f, .13f) };
            var fabrics = new[] { new Color(.8f, .1f, .1f), new Color(.1f, .3f, .8f) };
            var bodies = new List<BuiltBody>();
            var looks = new[] { first, second };
            for (int i = 0; i < looks.Length; i++)
            {
                AppearanceEditing.SetColor(looks[i], "Skin", skins[i]);
                AppearanceEditing.SetFabric(looks[i], "Chest", fabrics[i]);
                AppearanceEditing.Wear(looks[i], AppearanceEditing.Find(catalog, watch.Id), catalog);
                var built = new BuiltBody();
                yield return Build(new UmaBodyProvider(catalog), looks[i], Actor(owned, "Shared watch actor " + i, i).transform, built);
                bodies.Add(built);
            }
            Assert.That(looks[0].ContentKey(), Is.Not.EqualTo(looks[1].ContentKey()), "Two different looks.");
            for (int i = 0; i < bodies.Count; i++)
            {
                AssertBuilt(bodies[i].Avatar, "Hands", watch.Recipe, new[] { watch.Slot }, "The watch on body " + i);
                AssertDrawnAsAuthored(bodies[i].Avatar, watch, "on body " + i);
                var skin = bodies[i].Avatar.GetColor("Skin");
                var wanted = UmaBodyProvider.SkinColour(skins[i]);
                Assert.That(skin, Is.Not.Null, "Body " + i + " has a skin colour.");
                for (int channel = 0; channel < 3; channel++)
                {
                    Assert.That(skin.channelMask[0][channel], Is.EqualTo(wanted.channelMask[0][channel]).Within(.002f), "Body " + i + " keeps its own skin.");
                    Assert.That(skin.channelAdditiveMask[0][channel], Is.EqualTo(wanted.channelAdditiveMask[0][channel]).Within(.002f), "Body " + i + " keeps its own skin.");
                }
            }
            yield return null;
        }

        /// <summary>
        /// The body built for the pool and for bed wears no watch, arm piece or necklace, and no code-built piece,
        /// while the everyday body wears all of them. The data side is WardrobeSlotsTests.EveryAccessoryComesOffForThePoolAndForBed.
        /// </summary>
        [UnityTest]
        public IEnumerator PoolAndBedBuildsLeaveTheWristAndNeckBare()
        {
            owned.Add(new GameObject("Wrist and neck provider", typeof(GamesimUmaCast)));
            yield return null;
            content = new Content();
            var installed = new UmaAppearanceCatalog();
            string female = UmaCastLibrary.FemaleRace;
            var appearance = Player(installed, female);
            // A swim or sleep set is derived only from a look that wears what goes underneath.
            foreach (string under in new[] { "TopUnderlayer", "BottomUnderlayer" })
            {
                if (AppearanceEditing.Outfit(appearance).wardrobe.Any(worn => worn.slot == under)) continue;
                var item = installed.Items.FirstOrDefault(value => value.Slot == under && value.Fits(female) && !value.PickerHidden
                    && !UmaAppearanceCatalog.IsGrown(value.Id));
                Assert.That(item, Is.Not.Null, "Underwear for " + under + ".");
                AppearanceEditing.Wear(appearance, item, installed);
            }
            var sources = FreeSources(installed, appearance, female, Places.Length);
            var recipes = Places.Select((place, i) => content.Copy(installed, sources[i], "contract-bare-" + place.ToLowerInvariant(), place)).ToList();
            var catalog = content.Catalog();
            for (int i = 0; i < Places.Length; i++)
                AppearanceEditing.Wear(appearance, AppearanceEditing.Find(catalog, "contract-bare-" + Places[i].ToLowerInvariant()), catalog);
            string glasses = ProceduralAccessories.Prefix + "glasses";
            AppearanceEditing.Wear(appearance, AppearanceEditing.Find(catalog, glasses), catalog);
            Assert.That(appearance.TryValidate(out var error), Is.True, error);
            var provider = new UmaBodyProvider(catalog);

            var everyday = new BuiltBody();
            yield return Build(provider, appearance, Actor(owned, "Everyday actor", 0).transform, everyday);
            for (int i = 0; i < Places.Length; i++)
                AssertBuilt(everyday.Avatar, Places[i], recipes[i], SlotIds(recipes[i]), Places[i] + " every day");
            Assert.That(CodeBuiltPieces(everyday.Body), Does.Contain(glasses), "The everyday body wears its code-built piece.");

            int place = 1;
            foreach (string context in new[] { CharacterOutfits.Swimwear, CharacterOutfits.Sleepwear })
            {
                var dressed = CharacterOutfits.ForActivity(appearance, context);
                Assert.That(dressed.activeOutfit, Is.EqualTo(context), "A derived " + context + " set.");
                var built = new BuiltBody();
                yield return Build(provider, dressed, Actor(owned, context + " actor", place++).transform, built);
                foreach (string slot in Places)
                    Assert.That(built.Avatar.GetWardrobeItem(slot), Is.Null, slot + " is bare in " + context + ".");
                var drawn = built.Avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).Select(slot => slot.slotName).ToArray();
                foreach (var recipe in recipes)
                    Assert.That(drawn.Intersect(SlotIds(recipe)), Is.Empty, recipe.wardrobeSlot + "'s geometry is not built in " + context + ".");
                Assert.That(CodeBuiltPieces(built.Body), Is.Empty, "No code-built piece in " + context + ".");
            }
            yield return null;
        }
#endif
    }
}
#endif
