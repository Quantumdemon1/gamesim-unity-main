using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Uma.Editor
{
    /// <summary>
    /// Authors project-owned clothing against the installed bodies. Only an explicitly named,
    /// isolated batch project may run this entry point; opening the live game cannot author assets.
    /// Geometry is generated from garment panels, never copied from an installed clothing slot.
    /// </summary>
    public static partial class HouseGarmentAuthoring
    {
        public const string ContentRoot = "Assets/Gamesim/Uma/Content/HouseGarments";
        public const string CatalogPath = "Assets/Gamesim/Uma/Resources/Gamesim/CharacterCatalog/HouseGarments.asset";
        public const string IndexPath = "Assets/UMAProjectData/Resources/AssetIndexerProject.asset";
        private const double TimeoutSeconds = 1200;
        // URP's nonmetal dielectric reflectance. These channels are linear material data, not dye.
        private const float FabricSpecularReflectance = .04f;
        private static IEnumerator routine;
        private static double started;
        private static Report report;
        private static string reportPath;
        private static UMAAssetIndexer index;
        private static bool verifying;
        private static readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();

        [Serializable] private sealed class Report
        {
            public string status = "running", stage, error, project, startedUtc, completedUtc;
            public string indexAssetPath, indexGuid;
            public string indexShaBefore, indexShaAfter;
            public List<string> writtenAssets = new List<string>();
            public List<FitReport> fits = new List<FitReport>();
        }
        [Serializable] private sealed class FitReport
        {
            public string id, race, recipe, slot, rootBone;
            public string geometryStatus;
            public bool upperBoundaryAdapted;
            public int vertices, triangles, maximumInfluences;
            public int coverageQueries, coverageTriangleTests, coverageMaximumCandidates, coverageLargeTrianglesRetained;
            public Vector3 hipsLandmark, neckLandmark, leftUpperArmLandmark, rightUpperArmLandmark;
            public Vector3 referencePatternBoundsCenter, referencePatternBoundsSize, meshBoundsCenter, meshBoundsSize;
            public float measuredNeckRadius, desiredNeckRadius, paddedNeckRadius, neckSectionCenterX, shoulderHalfWidth, torsoHeight;
            public float desiredLeftOuterX, desiredRightOuterX, leftOuterX, rightOuterX, leftStrapWidth, rightStrapWidth, leftAvailableSpan, rightAvailableSpan, minimumUsableStrapWidth;
            public string specularTexturePath, specularTextureGuid;
            public Color specularSample;
            public bool specularTextureLinear;
            public List<string> adjustmentBones = new List<string>();
            public List<string> maskTargets = new List<string>();
            public List<int> hiddenTriangles = new List<int>();
        }
        private sealed class Style
        {
            public string stem, id, label, group;
            public bool sleeves;
            public float clearance;
            public OverlayDataAsset overlay;
            public FitReport evidence;
        }
        private struct Triangle
        {
            public int a, b, c;
            public bool arm;
        }
        private struct Hit
        {
            public Triangle triangle;
            public Vector3 position, normal, barycentric;
            public float distance;
        }
        private sealed class Surface
        {
            public SkinnedMeshRenderer renderer;
            public UMAMeshData skin;
            public Vector3[] vertices, normals;
            public Matrix4x4[] skinMatrices;
            public int[] weightOffsets;
            public List<Triangle> triangles = new List<Triangle>();
            public List<SlotData> slots;
            public Animator animator;
            // Also permits independent, editor-only geometry fixtures without creating UMA assets.
            public readonly Dictionary<HumanBodyBones, Vector3> landmarks = new Dictionary<HumanBodyBones, Vector3>();
            public Vector3 Bone(HumanBodyBones bone)
            {
                if (landmarks.TryGetValue(bone, out Vector3 position)) return position;
                Transform transform = animator.GetBoneTransform(bone);
                Require(transform != null, "Missing humanoid landmark: " + bone);
                position = renderer.transform.InverseTransformPoint(transform.position);
                landmarks.Add(bone, position);
                return position;
            }
            public bool Ray(Vector3 origin, Vector3 direction, out Hit hit, bool? arm = null,
                float maximumY = float.PositiveInfinity)
            {
                hit = new Hit();
                float nearest = float.PositiveInfinity;
                foreach (Triangle triangle in triangles)
                {
                    if (arm.HasValue && triangle.arm != arm.Value) continue;
                    if (!Intersect(origin, direction, vertices[triangle.a], vertices[triangle.b], vertices[triangle.c],
                        out float distance, out Vector3 bary) || distance >= nearest) continue;
                    Vector3 position = origin + direction * distance;
                    if (position.y > maximumY) continue;
                    nearest = distance;
                    hit = new Hit { triangle = triangle, position = position, barycentric = bary,
                        normal = (normals[triangle.a] * bary.x + normals[triangle.b] * bary.y + normals[triangle.c] * bary.z).normalized };
                }
                return !float.IsInfinity(nearest);
            }
            public Hit Closest(Vector3 point, bool? arm = null)
            {
                Hit hit = new Hit { distance = float.PositiveInfinity };
                foreach (Triangle triangle in triangles)
                {
                    if (arm.HasValue && triangle.arm != arm.Value) continue;
                    Vector3 a = vertices[triangle.a], b = vertices[triangle.b], c = vertices[triangle.c];
                    Vector3 closest = ClothingConformerMeshUtility.ClosestPointOnTriangle(point, a, b, c);
                    float distance = (closest - point).sqrMagnitude;
                    if (distance >= hit.distance) continue;
                    Vector3 bary = ClothingConformerMeshUtility.CalculateBarycentric(closest, a, b, c);
                    Vector3 normal = (normals[triangle.a] * bary.x + normals[triangle.b] * bary.y + normals[triangle.c] * bary.z).normalized;
                    hit = new Hit { triangle = triangle, position = closest, normal = normal, barycentric = bary, distance = distance };
                }
                Require(!float.IsInfinity(hit.distance), "No compatible body surface for a pattern vertex.");
                return hit;
            }
            public Hit FrontBack(float x, float y, bool front)
            {
                Vector3 origin = new Vector3(x, y, front ? 3f : -3f);
                Vector3 direction = front ? Vector3.back : Vector3.forward;
                float nearest = float.PositiveInfinity;
                Hit hit = new Hit();
                foreach (Triangle triangle in triangles)
                {
                    if (triangle.arm) continue;
                    Vector3 a = vertices[triangle.a], e1 = vertices[triangle.b] - a, e2 = vertices[triangle.c] - a;
                    Vector3 h = Vector3.Cross(direction, e2);
                    float determinant = Vector3.Dot(e1, h);
                    if (Mathf.Abs(determinant) < 0.000001f) continue;
                    float inverse = 1f / determinant;
                    Vector3 s = origin - a;
                    float u = inverse * Vector3.Dot(s, h);
                    if (u < 0f || u > 1f) continue;
                    Vector3 q = Vector3.Cross(s, e1);
                    float v = inverse * Vector3.Dot(direction, q);
                    if (v < 0f || u + v > 1f) continue;
                    float t = inverse * Vector3.Dot(e2, q);
                    if (t < 0f || t >= nearest) continue;
                    nearest = t;
                    Vector3 bary = new Vector3(1f - u - v, u, v);
                    hit = new Hit { triangle = triangle, position = origin + direction * t, barycentric = bary,
                        normal = (normals[triangle.a] * bary.x + normals[triangle.b] * bary.y + normals[triangle.c] * bary.z).normalized };
                }
                return float.IsInfinity(nearest) ? Closest(new Vector3(x, y, front ? .25f : -.25f), false) : hit;
            }
        }
        private sealed class Pattern
        {
            public readonly List<Vector3> points = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> indices = new List<int>();
            public List<int> neckBoundary;
            public readonly List<List<int>> armBoundaries = new List<List<int>>();
            public float hem, shoulder, armpit;
            public int Add(Vector3 point, Vector2 uv) { points.Add(point); uvs.Add(uv); return points.Count - 1; }
            public void Quad(int a, int b, int c, int d, Vector3 outward)
            {
                Face(a, b, c, outward);
                Face(a, c, d, outward);
            }
            private void Face(int a, int b, int c, Vector3 outward)
            {
                Vector3 normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                if (normal.sqrMagnitude <= .000000000001f) return;
                if (Vector3.Dot(normal, outward) < 0) { int swap = b; b = c; c = swap; }
                indices.Add(a); indices.Add(b); indices.Add(c);
            }
        }

        /// <summary>Batch entry. Omit -quit: the bounded editor runner owns the exit and final report.</summary>
        public static void Build()
        {
            Start(false);
        }

        /// <summary>Run in a new batch process to prove assets resolve without the authoring cache.</summary>
        public static void Verify()
        {
            Start(true);
        }

        private static void Start(bool verify)
        {
            try
            {
                Require(Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode,
                    "Garment authoring requires an isolated editor batch process.");
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string authorized = Argument("-gamesimGarmentAuthoringRoot");
                Require(!string.IsNullOrWhiteSpace(authorized) && string.Equals(project.TrimEnd('\\', '/'),
                    Path.GetFullPath(authorized).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase),
                    "-gamesimGarmentAuthoringRoot must explicitly name this isolated project.");
                Require(Path.GetFileName(project).IndexOf("Acceptance", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Only an acceptance copy may author these assets.");
                reportPath = Path.Combine(project, "Logs", verify ? "house-garment-verification.json" : "house-garment-authoring.json");
                report = new Report { project = project, startedUtc = DateTime.UtcNow.ToString("o") };
                verifying = verify;
                if (verify) report.indexShaBefore = Hash(IndexPath);
                started = EditorApplication.timeSinceStartup;
                DynamicCharacterAvatar.EditorGenerationPaused = true;
                routine = verify ? VerifySaved() : Author();
                EditorApplication.update += Advance;
                Progress("scheduled");
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Advance()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup - started < TimeoutSeconds, "Authoring exceeded twenty minutes.");
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (!routine.MoveNext()) Finish(null);
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static IEnumerator Author()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Progress("project index");
            EnsureFolder(Path.GetDirectoryName(IndexPath).Replace('\\', '/'));
            UMAAssetIndexer installed = UMAAssetIndexer.Instance;
            Require(installed != null, "UMA Global Library is unavailable.");
            index = AssetDatabase.LoadAssetAtPath<UMAAssetIndexer>(IndexPath);
            if (index == null)
            {
                index = UnityEngine.Object.Instantiate(installed);
                index.name = "AssetIndexerProject";
                index.generator = null;
                AssetDatabase.CreateAsset(index, IndexPath);
                Written(IndexPath);
                EditorUtility.SetDirty(index);
                AssetDatabase.SaveAssetIfDirty(index);
            }
            UMAAssetIndexer.RuntimeInitializeOnLoad();
            Require(ReferenceEquals(UMAAssetIndexer.Instance, index), "UMA did not select the project indexer override.");
            report.indexAssetPath = AssetDatabase.GetAssetPath(index);
            report.indexGuid = AssetDatabase.AssetPathToGUID(IndexPath);
            Require(index.Generator != null, "UMA has no usable scene generator.");
            EnsureFolder(ContentRoot);
            EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
            yield return null;

            UMAMaterial material = CreateMaterial();
            Style[] styles = {
                new Style { stem = "Gamesim_KnitCrew", id = "gamesim.knit.crew", label = "Crew-neck knit", group = "gamesim-knit-crew", sleeves = true, clearance = .012f },
                new Style { stem = "Gamesim_CompetitionVest", id = "gamesim.vest.competition", label = "Competition vest", group = "gamesim-competition-vest", clearance = .008f }
            };
            foreach (Style style in styles) style.overlay = CreateOverlay(style, material);
            UmaWardrobeCatalog catalog = Asset<UmaWardrobeCatalog>(CatalogPath);
            catalog.version = 1;
            catalog.entries.Clear();
            string[] races = { UmaCastLibrary.FemaleRace, UmaCastLibrary.MaleRace };
            for (int body = 0; body < races.Length; body++)
            {
                string fit = body == 0 ? "A" : "B";
                Progress("reference body " + fit);
                DynamicCharacterAvatar avatar = Reference(races[body]);
                Surface surface = BodySurface(avatar);
                foreach (Style style in styles)
                {
                    FitReport evidence = new FitReport { id = style.id + "." + fit.ToLowerInvariant(), race = races[body],
                        slot = style.stem + "_" + fit + "_Slot", recipe = style.stem + "_" + fit + "_Recipe",
                        rootBone = surface.renderer.rootBone.name, geometryStatus = "sampling" };
                    style.evidence = evidence;
                    ValidateSpecularTexture(style.overlay, evidence);
                    // Keep a failed fit's measured geometry, rather than only the previously completed fits.
                    report.fits.Add(evidence);
                    Progress(style.stem + " " + fit + " panels");
                    Pattern pattern = Panels(surface, style);
                    Bounds referenceBounds = new Bounds(pattern.points[0], Vector3.zero);
                    foreach (Vector3 point in pattern.points) referenceBounds.Encapsulate(point);
                    evidence.referencePatternBoundsCenter = referenceBounds.center;
                    evidence.referencePatternBoundsSize = referenceBounds.size;
                    evidence.geometryStatus = "pattern ready";
                    yield return null;
                    Progress(style.stem + " " + fit + " skinning");
                    Mesh mesh = Skin(pattern, surface, evidence);
                    // Mesh.bounds is in the stored bind-pose mesh coordinates, not world/reference-pose coordinates.
                    evidence.meshBoundsCenter = mesh.bounds.center;
                    evidence.meshBoundsSize = mesh.bounds.size;
                    evidence.geometryStatus = "skinned";
                    string meshPath = ContentRoot + "/" + style.stem + "_" + fit + "_Mesh.asset";
                    Mesh savedMesh = SaveMesh(mesh, meshPath);
                    SkinnedMeshRenderer clothing = Renderer(savedMesh, surface.renderer);
                    SlotDataAsset slot = Asset<SlotDataAsset>(ContentRoot + "/" + evidence.slot + ".asset");
                    slot.PrepareForAssetPath(AssetDatabase.GetAssetPath(slot), evidence.slot);
                    slot.slotGroup = "Chest";
                    slot.tags = new[] { "GamesimHouseGarment", "Chest", style.group };
                    slot.Races = new[] { races[body] };
                    slot.isLegacySlot = false;
                    slot.UpdateMeshData(clothing, surface.renderer.rootBone.name, false, 0, false, false);
                    var reasons = new List<string>();
                    Require(slot.ValidateMeshData(reasons), "Invalid slot " + slot.slotName + ": " + string.Join("; ", reasons));
                    Save(slot);
                    Register(slot);
                    List<MeshHideAsset> masks = Masks(surface, pattern, style, evidence, fit);
                    UMAWardrobeRecipe recipe = Asset<UMAWardrobeRecipe>(ContentRoot + "/" + evidence.recipe + ".asset");
                    recipe.recipeType = "Wardrobe";
                    recipe.wardrobeSlot = "Chest";
                    recipe.DisplayValue = style.label;
                    recipe.compatibleRaces = new List<string> { races[body] };
                    recipe.Hides.Clear();
                    recipe.HideTags.Clear();
                    recipe.suppressWardrobeSlots.Clear();
                    recipe.MeshHideAssets = masks;
                    var packed = new UMAData.UMARecipe();
                    packed.SetRace(index.GetRace(races[body]));
                    SlotData garment = new SlotData(slot) { Races = new[] { races[body] } };
                    var overlay = new OverlayData(style.overlay);
                    overlay.colorData.name = OverlayColorData.UNSHARED;
                    overlay.EnsureChannels(material.channels.Length);
                    overlay.colorData.channelMask[0] = Color.white;
                    garment.AddOverlay(overlay);
                    packed.SetSlot(0, garment);
                    recipe.Save(packed);
                    Save(recipe);
                    Register(recipe);
                    yield return null;
                    Progress(style.stem + " " + fit + " thumbnail and masks");
                    Wear(avatar, recipe);
                    ValidateMasks(avatar, masks);
                    Sprite thumbnail = Thumbnail(avatar, style.stem + "_" + fit + "_Thumbnail");
                    recipe.wardrobeRecipeThumbs = new List<WardrobeRecipeThumb> { new WardrobeRecipeThumb(races[body], thumbnail) };
                    Save(recipe);
                    catalog.entries.Add(new UmaWardrobeEntry { id = evidence.id, recipeName = evidence.recipe,
                        aliases = new List<string> { evidence.recipe }, label = style.label, styleGroup = style.group,
                        tags = new List<string> { "chest", style.sleeves ? "knit" : "competition", style.sleeves ? "casual" : "athletic" }, fallbackPriority = 20 });
                    evidence.geometryStatus = "complete";
                    style.evidence = null;
                    UnityEngine.Object.DestroyImmediate(clothing.gameObject);
                    // Restore the whole, unmasked reference surface before the next garment.
                    avatar.preloadWardrobeRecipes.recipes.Clear();
                    avatar.ClearSlots();
                    avatar.GenerateNow();
                    surface = BodySurface(avatar);
                    yield return null;
                }
                UnityEngine.Object.DestroyImmediate(avatar.gameObject);
            }
            Save(catalog);
            Progress("portable index");
            RemoveTransientSlots();
            index.generator = null; // A portable asset cannot carry the temporary scene generator.
            Save(index);
            Require(report.fits.Count == 4 && report.fits.All(fit => fit.geometryStatus == "complete"), "Four garment fits were not completed.");
            // A second batch process must verify the saved index, recipe and masks without this cache.
            Progress("assets saved; fresh-process verification required");
        }

        private static IEnumerator VerifySaved()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UMAAssetIndexer.RuntimeInitializeOnLoad();
            index = UMAAssetIndexer.Instance;
            Require(index != null && AssetDatabase.GetAssetPath(index) == IndexPath,
                "Fresh process did not discover the project Global Library override.");
            report.indexAssetPath = AssetDatabase.GetAssetPath(index);
            report.indexGuid = AssetDatabase.AssetPathToGUID(IndexPath);
            UmaWardrobeCatalog catalog = AssetDatabase.LoadAssetAtPath<UmaWardrobeCatalog>(CatalogPath);
            Require(catalog != null && catalog.entries.Count == 4, "The four-item house garment catalog is absent.");
            var meshPaths = new HashSet<string>();
            foreach (UmaWardrobeEntry entry in catalog.entries)
            {
                Progress("fresh index " + entry.id);
                bool bodyA = entry.id.EndsWith(".a", StringComparison.Ordinal);
                string race = bodyA ? UmaCastLibrary.FemaleRace : UmaCastLibrary.MaleRace;
                UMAWardrobeRecipe recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName, recursionGuard: true);
                Require(recipe != null && AssetDatabase.GetAssetPath(recipe).StartsWith(ContentRoot + "/", StringComparison.Ordinal),
                    "Recipe cannot resolve through the saved Global Library: " + entry.recipeName);
                Require(recipe.wardrobeSlot == "Chest" && recipe.compatibleRaces.SequenceEqual(new[] { race }),
                    "Recipe has the wrong slot or body compatibility: " + entry.recipeName);
                Require(recipe.wardrobeRecipeThumbs != null && recipe.wardrobeRecipeThumbs.Count == 1,
                    "Recipe is missing its body-specific thumbnail: " + entry.recipeName);
                SlotData garment = recipe.GetCachedRecipe().slotDataList.FirstOrDefault(slot => slot != null && slot.asset != null);
                Require(garment != null && garment.slotName == entry.recipeName.Replace("_Recipe", "_Slot"),
                    "Recipe does not contain its own garment slot.");
                Require(index.GetAsset<SlotDataAsset>(garment.slotName, recursionGuard: true) == garment.asset, "Slot is not indexed.");
                string meshPath = ContentRoot + "/" + entry.recipeName.Replace("_Recipe", "_Mesh.asset");
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                Require(mesh != null && mesh.vertexCount > 0 && meshPaths.Add(meshPath), "Each fit needs its own persistent mesh.");
                var reasons = new List<string>();
                Require(garment.asset.ValidateMeshData(reasons), "Persistent garment mesh is invalid: " + string.Join("; ", reasons));
                OverlayData overlay = garment.GetOverlay(0);
                Require(overlay != null && overlay.colorData.name == OverlayColorData.UNSHARED
                    && overlay.colorData.channelMask[0] == Color.white, "Garment dye must start private and white.");
                Require(AssetDatabase.GetAssetPath(overlay.asset).StartsWith(ContentRoot + "/", StringComparison.Ordinal),
                    "Garment overlay must be project-owned.");
                DynamicCharacterAvatar avatar = Reference(race);
                Wear(avatar, recipe);
                Require(avatar.umaRecipe.slotDataList.Any(slot => slot != null && slot.slotName == garment.slotName),
                    "Fresh body did not wear the indexed recipe.");
                ValidateMasks(avatar, recipe.MeshHideAssets);
                Require(avatar.GetRenderers().All(renderer => renderer.sharedMesh.vertices.All(Finite)), "Fresh generated body has nonfinite vertices.");
                var fit = new FitReport { id = entry.id, race = race, recipe = entry.recipeName, slot = garment.slotName,
                    rootBone = garment.asset.meshData.RootBoneName, vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3,
                    maximumInfluences = garment.asset.meshData.ManagedBonesPerVertex.Max(value => (int)value),
                    maskTargets = recipe.MeshHideAssets.Select(mask => mask.AssetSlotName).ToList(),
                    hiddenTriangles = recipe.MeshHideAssets.Select(mask => mask.triangleFlags.Sum(flags => flags.Cast<bool>().Count(hidden => hidden))).ToList() };
                ValidateSpecularTexture(overlay.asset, fit);
                report.fits.Add(fit);
                UnityEngine.Object.DestroyImmediate(avatar.gameObject);
                yield return null;
            }
            Require(report.fits.Count == 4 && meshPaths.Count == 4, "Fresh-process verification did not cover all four fits.");
            // Deliberately do not persist the runtime-generated baked slots or generator reference.
            Progress("fresh project index, four recipes, private dyes and effective masks verified");
        }

        private static DynamicCharacterAvatar Reference(string race)
        {
            var root = new GameObject("__HouseGarmentReference");
            temporary.Add(root);
            root.AddComponent<Animator>();
            DynamicCharacterAvatar avatar = root.AddComponent<DynamicCharacterAvatar>();
            avatar.editorTimeGeneration = false;
            avatar.activeRace.name = race;
            avatar.preloadWardrobeRecipes.loadDefaultRecipes = false;
            avatar.preloadWardrobeRecipes.recipes.Clear();
            avatar.ClearSlots();
            avatar.BuildCharacterEnabled = true;
            avatar.GenerateNow();
            AssertBody(avatar);
            return avatar;
        }

        private static void Wear(DynamicCharacterAvatar avatar, UMAWardrobeRecipe recipe)
        {
            // GenerateNow builds the current wardrobe dictionary; it does not reload preload
            // settings after the avatar's first build when editorTimeGeneration is disabled.
            avatar.ClearSlots();
            Require(avatar.SetSlot(recipe), "The reference body refused its garment recipe: " + recipe.name);
            avatar.GenerateNow();
            AssertBody(avatar);
            Require(avatar.umaRecipe.slotDataList.Any(slot => slot != null && slot.slotName == recipe.name.Replace("_Recipe", "_Slot")),
                "GenerateNow did not dress the reference in its requested garment: " + recipe.name);
        }
        private static void AssertBody(DynamicCharacterAvatar avatar)
        {
            Require(avatar.umaRecipe != null && avatar.umaRecipe.raceData != null && avatar.skeleton != null,
                "GenerateNow did not produce a race and skeleton.");
            Require(!avatar.isMeshDirty && !avatar.isShapeDirty && !avatar.isTextureDirty, "UMA body is still dirty.");
            Animator animator = avatar.GetComponent<Animator>();
            Require(animator != null && animator.avatar != null && animator.isHuman, "UMA reference is not a generated humanoid.");
            Require(avatar.GetRenderers().Any(renderer => renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0),
                "UMA reference has no mesh.");
        }
        private static Surface BodySurface(DynamicCharacterAvatar avatar)
        {
            AssertBody(avatar);
            var slots = avatar.umaRecipe.slotDataList.Where(slot => slot != null && slot.asset != null
                && (slot.slotName.Contains("UMA30_Body_UDIM1002") || slot.slotName.Contains("UMA30_Body_UDIM1005"))).ToList();
            Require(slots.Any(slot => slot.slotName.Contains("1002")) && slots.Any(slot => slot.slotName.Contains("1005")),
                "Expected torso and arm body slots are absent; do not guess a replacement topology.");
            int rendererIndex = slots[0].skinnedMeshRenderer;
            Require(slots.All(slot => slot.skinnedMeshRenderer == rendererIndex), "Body slots use different renderers.");
            SkinnedMeshRenderer renderer = avatar.GetRenderers()[rendererIndex];
            Require(renderer.rootBone != null && renderer.bones.Length == renderer.sharedMesh.bindposes.Length, "Invalid reference skeleton binding.");
            var baked = new Mesh();
            renderer.BakeMesh(baked);
            var surface = new Surface { renderer = renderer, vertices = baked.vertices, normals = baked.normals,
                slots = slots, animator = avatar.GetComponent<Animator>(), skin = new UMAMeshData() };
            UnityEngine.Object.DestroyImmediate(baked);
            surface.skin.RootBoneName = renderer.rootBone.name;
            surface.skin.RetrieveDataFromUnityMesh(renderer, -1, false, false, false);
            surface.weightOffsets = new int[surface.skin.ManagedBonesPerVertex.Length];
            int weight = 0;
            for (int i = 0; i < surface.weightOffsets.Length; i++) { surface.weightOffsets[i] = weight; weight += surface.skin.ManagedBonesPerVertex[i]; }
            surface.skinMatrices = renderer.bones.Select((bone, i) => renderer.transform.worldToLocalMatrix * bone.localToWorldMatrix * renderer.sharedMesh.bindposes[i]).ToArray();
            foreach (SlotData slot in slots)
            {
                int[] triangles = slot.asset.meshData.submeshes[slot.asset.subMeshIndex].GetTriangles(0).ToArray();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var triangle = new Triangle { a = triangles[i] + slot.vertexOffset, b = triangles[i + 1] + slot.vertexOffset,
                        c = triangles[i + 2] + slot.vertexOffset, arm = slot.slotName.Contains("1005") };
                    Require(triangle.a < surface.vertices.Length && triangle.b < surface.vertices.Length && triangle.c < surface.vertices.Length,
                        "Body slot vertex mapping is invalid.");
                    surface.triangles.Add(triangle);
                }
            }
            return surface;
        }

        private static float TorsoHalfWidth(Surface surface, float y, float center, float fallback)
        {
            float width = 0;
            foreach (Triangle t in surface.triangles)
            {
                if (t.arm) continue;
                foreach (int vertex in new[] { t.a, t.b, t.c })
                    if (Mathf.Abs(surface.vertices[vertex].y - y) < .02f) width = Mathf.Max(width, Mathf.Abs(surface.vertices[vertex].x - center));
            }
            return width > .05f ? Mathf.Min(width * .92f, fallback * 1.12f) : fallback * .92f;
        }
        private static void Sleeve(Pattern pattern, Surface surface, List<int> opening, bool left, float clearance)
        {
            const int lengthSteps = 14;
            Vector3 elbow = surface.Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Vector3 hand = surface.Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Vector3 start = opening.Select(i => pattern.points[i]).Aggregate(Vector3.zero, (sum, point) => sum + point) / opening.Count;
            Vector3 axis = (elbow - start).normalized;
            Require(Mathf.Abs(axis.x) > .55f, "Reference arms must be in the neutral authoring pose.");
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            Vector3 forward = Vector3.Cross(axis, up).normalized;
            float[] angles = opening.Select(i => Mathf.Atan2(Vector3.Dot(pattern.points[i] - start, forward), Vector3.Dot(pattern.points[i] - start, up))).ToArray();
            int[] previous = opening.ToArray();
            for (int step = 1; step <= lengthSteps; step++)
            {
                float t = step / (float)lengthSteps;
                Vector3 center = t < .5f ? Vector3.Lerp(start, elbow, t * 2f) : Vector3.Lerp(elbow, hand, (t - .5f) * 1.9f);
                int[] ring = new int[opening.Count];
                for (int i = 0; i < ring.Length; i++)
                {
                    Vector3 radial = up * Mathf.Cos(angles[i]) + forward * Mathf.Sin(angles[i]);
                    // Cap rows can belong to either the torso or the proximal-arm slot.
                    Hit hit = surface.Closest(center + radial * .13f, step <= 2 ? (bool?)null : true);
                    float rib = step >= lengthSteps - 1 ? .005f : 0f;
                    ring[i] = pattern.Add(hit.position + hit.normal * (clearance + rib), new Vector2((left ? .02f : .28f) + .22f * i / (ring.Length - 1f), .7f + .26f * t));
                }
                for (int i = 0; i < ring.Length; i++)
                {
                    int n = (i + 1) % ring.Length;
                    Vector3 outward = (pattern.points[ring[i]] + pattern.points[ring[n]]) * .5f - center;
                    pattern.Quad(previous[i], previous[n], ring[n], ring[i], outward);
                }
                previous = ring;
            }
        }
        private static Mesh Skin(Pattern pattern, Surface surface, FitReport evidence)
        {
            var vertices = new Vector3[pattern.points.Count];
            var counts = new byte[vertices.Length];
            var weights = new List<BoneWeight1>();
            var used = new HashSet<int>();
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                Hit hit = surface.Closest(pattern.points[vertex]);
                Require(hit.distance < .12f * .12f, "Pattern vertex is too far from its reference body: " + vertex);
                var influences = new Dictionary<int, float>();
                int[] source = { hit.triangle.a, hit.triangle.b, hit.triangle.c };
                float[] factors = { hit.barycentric.x, hit.barycentric.y, hit.barycentric.z };
                for (int corner = 0; corner < 3; corner++)
                    for (int i = 0; i < surface.skin.ManagedBonesPerVertex[source[corner]]; i++)
                    {
                        BoneWeight1 weight = surface.skin.ManagedBoneWeights[surface.weightOffsets[source[corner]] + i];
                        if (!influences.ContainsKey(weight.boneIndex)) influences[weight.boneIndex] = 0f;
                        influences[weight.boneIndex] += weight.weight * factors[corner];
                    }
                var selected = influences.Where(pair => pair.Value > .000001f).OrderByDescending(pair => pair.Value).ToArray();
                Require(selected.Length > 0 && selected.Length <= byte.MaxValue, "Invalid garment weight count.");
                float sum = selected.Sum(pair => pair.Value);
                Matrix4x4 skin = new Matrix4x4();
                foreach (var pair in selected)
                {
                    Require(pair.Key >= 0 && pair.Key < surface.skinMatrices.Length, "Weight refers to an unknown skeleton bone.");
                    float weight = pair.Value / sum;
                    Matrix4x4 matrix = surface.skinMatrices[pair.Key];
                    for (int i = 0; i < 16; i++) skin[i] += matrix[i] * weight;
                    weights.Add(new BoneWeight1 { boneIndex = pair.Key, weight = weight });
                    used.Add(pair.Key);
                }
                Require(Mathf.Abs(skin.determinant) > .000001f, "Singular skinning matrix at garment vertex " + vertex);
                vertices[vertex] = skin.inverse.MultiplyPoint3x4(pattern.points[vertex]);
                counts[vertex] = (byte)selected.Length;
            }
            var data = new UMAMeshData { vertices = vertices, vertexCount = vertices.Length, uv = pattern.uvs.ToArray(),
                normals = new Vector3[vertices.Length], tangents = new Vector4[vertices.Length],
                bindPoses = (Matrix4x4[])surface.renderer.sharedMesh.bindposes.Clone(), bones = surface.renderer.bones,
                rootBone = surface.renderer.rootBone, RootBoneName = surface.renderer.rootBone.name,
                ManagedBonesPerVertex = counts, ManagedBoneWeights = weights.ToArray(), subMeshCount = 1,
                submeshes = new[] { new SubMeshTriangles() } };
            data.submeshes[0].SetTriangles(pattern.indices.ToArray());
            var mesh = new Mesh { name = evidence.slot.Replace("_Slot", "_Mesh"), indexFormat = IndexFormat.UInt32 };
            SkinnedMeshRenderer holder = Renderer(mesh, surface.renderer);
            data.CopyDataToUnityMesh(holder);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Require(mesh.triangles.Length > 0 && mesh.vertices.All(Finite), "Garment mesh is empty or nonfinite.");
            evidence.vertices = vertices.Length;
            evidence.triangles = pattern.indices.Count / 3;
            evidence.maximumInfluences = counts.Max(value => (int)value);
            evidence.adjustmentBones = used.Select(i => surface.renderer.bones[i].name).Where(name => name.Contains("Adjust") || name.Contains("Breast")).OrderBy(name => name).ToList();
            Require(evidence.adjustmentBones.Count > 0, "Garment lost the adjustment bones needed for physique DNA.");
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            return mesh;
        }
        private static List<MeshHideAsset> Masks(Surface surface, Pattern pattern, Style style, FitReport evidence, string fit)
        {
            var masks = new List<MeshHideAsset>();
            var coverage = new CoverageIndex(pattern);
            foreach (SlotData slot in surface.slots)
            {
                bool arms = slot.slotName.Contains("1005");
                if (arms && !style.sleeves) continue;
                string path = ContentRoot + "/" + style.stem + "_" + fit + (arms ? "_ArmsHide.asset" : "_TorsoHide.asset");
                MeshHideAsset mask = Asset<MeshHideAsset>(path);
                mask.asset = slot.asset;
                mask.Initialize();
                int[] indices = slot.asset.meshData.submeshes[slot.asset.subMeshIndex].GetTriangles(0).ToArray();
                int hidden = 0;
                for (int t = 0; t < indices.Length; t += 3)
                {
                    int a = indices[t] + slot.vertexOffset, b = indices[t + 1] + slot.vertexOffset, c = indices[t + 2] + slot.vertexOffset;
                    bool covered = coverage.CoversTriangle(surface.vertices[a], surface.vertices[b], surface.vertices[c],
                        surface.normals[a], surface.normals[b], surface.normals[c], style.clearance + .025f);
                    mask.triangleFlags[slot.asset.subMeshIndex][t / 3] = covered;
                    if (covered) hidden++;
                }
                Require(hidden > 0 && hidden < indices.Length / 3, "Mask must hide covered body and retain exposed body: " + mask.name);
                mask.UpdateEditorHashAndUVMaskFromFlags();
                Require(mask.IsCompatibleWithSlot(slot.asset, out string reason), "Invalid body mask: " + reason);
                Save(mask);
                masks.Add(mask);
                evidence.maskTargets.Add(mask.AssetSlotName);
                evidence.hiddenTriangles.Add(hidden);
            }
            evidence.coverageQueries = coverage.queries;
            evidence.coverageTriangleTests = coverage.triangleTests;
            evidence.coverageMaximumCandidates = coverage.maximumCandidates;
            evidence.coverageLargeTrianglesRetained = coverage.largeTrianglesRetained;
            return masks;
        }
        private static void ValidateMasks(DynamicCharacterAvatar avatar, List<MeshHideAsset> masks)
        {
            foreach (MeshHideAsset mask in masks)
            {
                SlotData target = avatar.umaRecipe.slotDataList.FirstOrDefault(slot => slot != null && slot.slotName == mask.AssetSlotName);
                Require(target != null, "Saved mask does not name an effective body slot: " + mask.AssetSlotName);
                Require(mask.IsCompatibleWithSlot(target.asset, out string reason), "Mask topology changed: " + reason);
                Require(target.meshHideMask != null && target.meshHideMask.Any(flags => flags != null && flags.Cast<bool>().Any(flag => flag)),
                    "Mask was not applied to the generated body: " + mask.name);
            }
        }

        private static UMAMaterial CreateMaterial()
        {
            UMAWardrobeRecipe template = index.GetAsset<UMAWardrobeRecipe>("tshirt_turquoise_Recipe");
            Require(template != null, "Installed clothing material template is missing.");
            SlotData slot = template.GetCachedRecipe().slotDataList.First(value => value != null && value.asset != null);
            UMAMaterial source = slot.GetOverlay(0)?.asset?.GetMaterial();
            Require(source != null && source.material != null && source.channels.Length > 0, "Installed clothing material has no shader channels.");
            Material drawing = AssetDatabase.LoadAssetAtPath<Material>(ContentRoot + "/Gamesim_HouseFabric.mat");
            if (drawing == null) { drawing = new Material(source.material); drawing.name = "Gamesim_HouseFabric"; AssetDatabase.CreateAsset(drawing, ContentRoot + "/Gamesim_HouseFabric.mat"); Written(AssetDatabase.GetAssetPath(drawing)); }
            else drawing.CopyPropertiesFromMaterial(source.material);
            Save(drawing);
            UMAMaterial material = AssetDatabase.LoadAssetAtPath<UMAMaterial>(ContentRoot + "/Gamesim_HouseFabric_UMAMaterial.asset");
            if (material == null) { material = UnityEngine.Object.Instantiate(source); material.name = "Gamesim_HouseFabric_UMAMaterial"; AssetDatabase.CreateAsset(material, ContentRoot + "/Gamesim_HouseFabric_UMAMaterial.asset"); Written(AssetDatabase.GetAssetPath(material)); }
            material.objectName = material.name;
            material.material = drawing;
            Save(material);
            Register(material);
            return material;
        }
        private static OverlayDataAsset CreateOverlay(Style style, UMAMaterial material)
        {
            Texture2D albedo = Texture(style.stem + "_Albedo", style.sleeves, false, false);
            Texture2D normal = Texture(style.stem + "_Normal", style.sleeves, true, false);
            Texture2D mask = Texture(style.stem + "_Mask", style.sleeves, false, true);
            Texture2D specular = Texture(style.stem + "_Specular", style.sleeves, false, false, specular: true);
            OverlayDataAsset overlay = Asset<OverlayDataAsset>(ContentRoot + "/" + style.stem + "_Overlay.asset");
            overlay._oldOverlayName = string.Empty;
            overlay.material = material;
            overlay.textureList = new UnityEngine.Texture[material.channels.Length];
            overlay.textureNames = new string[material.channels.Length];
            for (int i = 0; i < material.channels.Length; i++)
            {
                string property = (material.channels[i].materialPropertyName ?? string.Empty).ToLowerInvariant();
                bool normalChannel = material.channels[i].channelType == UMAMaterial.ChannelType.NormalMap
                    || material.channels[i].channelType == UMAMaterial.ChannelType.DetailNormalMap
                    || property.Contains("normal") || property.Contains("bump");
                // UMA3's specular workflow consumes RGB as F0. Albedo white removes diffuse dye,
                // and the packed metal/occlusion mask is not a specular reflectance texture.
                overlay.textureList[i] = normalChannel ? normal : property.Contains("specular") ? specular : property.Contains("metal") || property.Contains("occlusion")
                    || property.Contains("mask") || property.Contains("gloss") ? mask : albedo;
                overlay.textureNames[i] = overlay.textureList[i].name;
            }
            overlay.overlayBlend = new OverlayDataAsset.OverlayBlend[material.channels.Length];
            Save(overlay);
            Register(overlay);
            return overlay;
        }
        private static Texture2D Texture(string name, bool knit, bool normal, bool mask, bool specular = false)
        {
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, normal || mask || specular);
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float weave = Mathf.Sin(x * (knit ? .65f : 1.1f)) * Mathf.Sin(y * (knit ? .37f : 1.1f));
                // The installed graph multiplies SpecularMap alpha by _Smoothness. Preserve the
                // previous alpha of one so this correction does not also change fabric roughness.
                if (specular) colors[x + y * size] = new Color(FabricSpecularReflectance, FabricSpecularReflectance, FabricSpecularReflectance, 1f);
                else if (mask) colors[x + y * size] = new Color(0f, 1f, 0f, knit ? .12f : .28f);
                else if (normal) colors[x + y * size] = new Color(.5f + weave * .045f, .5f + Mathf.Cos(y * .37f) * .035f, 1f, 1f);
                else
                {
                    float value = 1f; // Dye starts white; detail lives in the normal map.
                    if (!knit && y > size * .7f) value = .55f;
                    colors[x + y * size] = new Color(value, value, value, 1f);
                }
            }
            texture.SetPixels32(colors);
            texture.Apply();
            string path = ContentRoot + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !mask && !specular;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Written(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static void ValidateSpecularTexture(OverlayDataAsset overlay, FitReport evidence)
        {
            int channel = Array.FindIndex(overlay.material.channels, value => value.materialPropertyName == "_SpecularMap");
            Require(channel >= 0 && channel < overlay.textureList.Length,
                "The installed fabric material must expose its specular texture channel.");
            Texture texture = overlay.textureList[channel];
            string path = AssetDatabase.GetAssetPath(texture);
            string expected = ContentRoot + "/" + overlay.name.Replace("_Overlay", "_Specular") + ".png";
            Require(texture != null && path == expected, "Specular reflectance must use its dedicated project texture: " + path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null && importer.textureType == TextureImporterType.Default && !importer.sRGBTexture
                && importer.alphaSource == TextureImporterAlphaSource.FromInput
                && importer.textureCompression == TextureImporterCompression.Uncompressed,
                "Specular reflectance must import as linear material data: " + path);
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Require(pixels.LoadImage(File.ReadAllBytes(path)), "Cannot read saved specular texture: " + path);
                Color32 expectedPixel = new Color(FabricSpecularReflectance, FabricSpecularReflectance, FabricSpecularReflectance, 1f);
                Color32[] savedPixels = pixels.GetPixels32();
                Require(savedPixels.Length > 0 && savedPixels.All(pixel => pixel.r == expectedPixel.r && pixel.g == expectedPixel.g
                    && pixel.b == expectedPixel.b && pixel.a == expectedPixel.a),
                    "Specular RGB must be neutral dielectric reflectance and alpha must preserve smoothness: " + path);
                evidence.specularTexturePath = path;
                evidence.specularTextureGuid = AssetDatabase.AssetPathToGUID(path);
                evidence.specularSample = savedPixels[0];
                evidence.specularTextureLinear = !importer.sRGBTexture;
                Require(!string.IsNullOrEmpty(evidence.specularTextureGuid), "Saved specular texture has no stable GUID: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }
        private static Sprite Thumbnail(DynamicCharacterAvatar avatar, string name)
        {
            var cameraObject = new GameObject("__GarmentThumbnailCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .15f, .20f, 1f);
            camera.orthographic = true;
            Vector3 hips = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).position;
            Vector3 neck = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Neck).position;
            Vector3 center = Vector3.Lerp(hips, neck, .55f);
            camera.orthographicSize = Mathf.Max(.4f, (neck.y - hips.y) * .8f);
            cameraObject.transform.position = center + new Vector3(.35f, .05f, 3f);
            cameraObject.transform.LookAt(center);
            var lightObject = new GameObject("__GarmentThumbnailLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            lightObject.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
            var target = new RenderTexture(256, 256, 24);
            var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                pixels.Apply();
                string path = ContentRoot + "/" + name + ".png";
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                Written(path);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        private static SkinnedMeshRenderer Renderer(Mesh mesh, SkinnedMeshRenderer reference)
        {
            var root = new GameObject("__GarmentMesh");
            root.transform.SetParent(reference.transform.parent, false);
            root.transform.localPosition = reference.transform.localPosition;
            root.transform.localRotation = reference.transform.localRotation;
            root.transform.localScale = reference.transform.localScale;
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = reference.bones;
            renderer.rootBone = reference.rootBone;
            renderer.enabled = false;
            return renderer;
        }
        private static Mesh SaveMesh(Mesh source, string path)
        {
            Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (target == null) { AssetDatabase.CreateAsset(source, path); target = source; }
            else { EditorUtility.CopySerialized(source, target); UnityEngine.Object.DestroyImmediate(source); }
            Written(path);
            Save(target);
            return target;
        }
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>();
            value.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(value, path);
            Written(path);
            return value;
        }
        private static void Register(UnityEngine.Object asset)
        {
            Require(AssetDatabase.GetAssetPath(index) == IndexPath, "Registration cannot write the installed UMA index.");
            // Re-authoring keeps the existing asset GUIDs and one persistent index item per name.
            foreach (AssetItem existing in index.GetAssetItems(asset.GetType()).Where(item => item.Item == asset).ToArray())
                index.RemoveAsset(asset.GetType(), existing._Name, false);
            Require(index.EvilAddAsset(asset.GetType(), asset), "Global Library registration failed: " + asset.name);
        }
        private static void RemoveTransientSlots()
        {
            foreach (AssetItem item in index.GetAssetItems<SlotDataAsset>().ToArray())
                if (item.Item != null && !AssetDatabase.Contains(item.Item)) index.RemoveAsset(typeof(SlotDataAsset), item._Name, false);
            index.RebuildIndex();
        }
        private static void Save(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            Require(path.StartsWith(ContentRoot + "/", StringComparison.Ordinal) || path == CatalogPath || path == IndexPath,
                "Refusing to save an asset outside garment ownership: " + path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Written(path);
        }
        private static void EnsureFolder(string path)
        {
            string parent = "Assets";
            foreach (string part in path.Split('/').Skip(1))
            {
                string next = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) { AssetDatabase.CreateFolder(parent, part); Written(next + ".meta"); }
                parent = next;
            }
        }
        private static void Written(string path)
        {
            if (!report.writtenAssets.Contains(path)) report.writtenAssets.Add(path);
            if (!path.EndsWith(".meta", StringComparison.Ordinal) && !report.writtenAssets.Contains(path + ".meta")) report.writtenAssets.Add(path + ".meta");
        }
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        private static void Progress(string stage)
        {
            report.stage = stage;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("[HouseGarmentAuthoring] " + stage);
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Advance;
            routine = null;
            foreach (UnityEngine.Object item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            temporary.Clear();
            if (verifying && report != null)
            {
                try
                {
                    if (index != null)
                    {
                        RemoveTransientSlots();
                        index.generator = null;
                        EditorUtility.ClearDirty(index);
                    }
                    report.indexShaAfter = Hash(IndexPath);
                    Require(report.indexShaBefore == report.indexShaAfter, "Verification changed the project indexer bytes.");
                }
                catch (Exception cleanupError) { if (error == null) error = cleanupError; }
            }
            if (report != null)
            {
                report.status = error == null ? "complete" : "failed";
                report.error = error == null ? null : error.ToString();
                report.completedUtc = DateTime.UtcNow.ToString("o");
                Progress(report.stage);
            }
            if (error != null) Debug.LogException(error);
            Debug.Log("[HouseGarmentAuthoring] " + (error == null ? "COMPLETE" : "FAILED"));
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static string Hash(string path)
        {
            using (SHA256 digest = SHA256.Create())
                return BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
