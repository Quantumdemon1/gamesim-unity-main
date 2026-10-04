using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    /// <summary>Produces reviewable fit evidence from the real provider, DNA and shipped Humanoid controller.</summary>
    public sealed class UmaHouseGarmentFitCapturePlayModeTests
    {
        private const int Layer = 30, Width = 768, Height = 1024;
        private static readonly string[] Ids = { "gamesim.knit.crew.a", "gamesim.knit.crew.b", "gamesim.vest.competition.a", "gamesim.vest.competition.b" };
        private static readonly string[] Dna = { "height", "upperWeight", "lowerWeight", "armWidth" };
        private static readonly string[] States = { "Idle", "SitIdle", "Walk", "Run", "Cheer", "SwimForward" };
        private static readonly string[] Shapes = { "neutral", "limits-low", "limits-high" };
        private static readonly string[] Activities = { "Sleeping", "Swimming", "Cooking", "Dancing", "Posing" };
        private static readonly HumanBodyBones[] ObservedBones = { HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
        private GameObject cast, stage, subject;
        private Camera camera;
        private Light keyLight, fillLight;
        private UmaAppearanceCatalog catalog;
        private UmaBodyProvider provider;
        private string directory;
        private Manifest manifest;
        private readonly Dictionary<Texture, AtlasObservation> observedAtlases = new Dictionary<Texture, AtlasObservation>();
        private bool firstAtlasSaved;

        [Serializable] private sealed class Manifest
        {
            public string sourceRevision, providerAssemblyMvid, generatedUtc;
            public bool complete;
            public List<Photo> photos = new List<Photo>();
            public List<string> optionalNotes = new List<string>();
            public List<string> dyeFailures = new List<string>();
            public List<string> layerProbeFiles = new List<string>();
            public List<string> layerProbeFailures = new List<string>();
        }
        [Serializable] private sealed class Photo
        {
            public string file, item, recipe, body, shape, state, clip, view, appearanceKey;
            public bool headDetail;
            public float normalizedTime, cameraSize, keyLightIntensity, fillLightIntensity;
            public int sampleFrame, observedFrame, readFrame, torsoPixelCount, greenTorsoPixelCount;
            public float sampledPhase, observedPhase, maximumBoneDrift;
            public Vector3 cameraPosition, cameraUp, torsoUp, torsoForward, keyLightForward, fillLightForward, head, hips, leftHand, rightHand, leftShoulder, rightShoulder;
            public RectInt torsoPixelRegion;
            public Color meanTorsoColor;
            public List<GarmentOverlay> actualOverlays;
            public List<GarmentMaterial> actualMaterials;
            public List<AppearanceValue> requestedDna, builtDna;
            public List<AppearanceWardrobe> wardrobe;
            public List<AppearanceColor> outfitColors;
        }
        private sealed class PoseObservation
        {
            public int sampleFrame, observedFrame;
            public float sampledPhase, observedPhase, maximumBoneDrift;
            public Vector3[] sampledBones;
        }
        [Serializable] private sealed class GarmentOverlay
        {
            public string slot, overlay;
            public bool shared;
            public Color channelZero;
        }
        [Serializable] private sealed class GarmentMaterial
        {
            public string name, shader, renderer, baseMap, colorProperty, rendererOverrideMap, indexedOverrideMap;
            public int materialIndex;
            public Color baseColor;
            public bool rendererBlockEmpty, indexedBlockEmpty, rendererColorOverride, indexedColorOverride;
            public Color rendererOverrideColor, indexedOverrideColor;
            public Vector2 baseMapScale, baseMapOffset;
            public List<FragmentColor> fragments;
            public AtlasObservation atlas;
        }
        [Serializable] private sealed class FragmentColor
        {
            public string slot;
            public Color baseColor, multiplier, additive;
            public Rect atlasRegion;
        }
        [Serializable] private sealed class AtlasObservation
        {
            public string representativeFile, sourceTexture, encoding = "Linear Blit/readback";
            public int sourceWidth, sourceHeight, sampledPixels, visiblePixels, greenPixels;
            public Color meanVisibleColor;
        }

        // Geometric candidates explain existing pixels; a two-sided CPU ray is not a claim
        // that the shader drew that face (alpha, culling and lighting remain recorded limits).
        [Serializable] private sealed class LayerProbeReport
        {
            public string sourceRevision, providerAssemblyMvid, photo, photoSha256, state, appearanceKey, generatedUtc;
            public string coordinateOrigin = "PNG top-left; pixel centers; ray geometry in capture-camera local coordinates";
            public string limits = "Two-sided triangle intersections (determinant epsilon 1e-10, max distance 20m), not shader visibility; no texture alpha/cull simulation or additional render; nearest 16 candidates retained; source LOD0 ownership matches assigned physical submesh and vertex tuple, extra material slots can reuse that geometry, unmatched physical submeshes remain unattributed";
            public bool complete;
            public int width = Width, height = Height;
            public int frameBefore, frameAfter, rendererCount, vertexCount, triangleCount, sourceTriangleCount, triangleTests, unattributedTriangles;
            public long sourceBytes;
            public Vector3 cameraPosition, subjectPosition;
            public Quaternion cameraRotation, subjectRotation;
            public float animationPhaseBefore, animationPhaseAfter, maximumBoneDrift;
            public List<ProbeAsset> sources = new List<ProbeAsset>();
            public List<ProbeSlot> slots = new List<ProbeSlot>();
            public List<ProbePixel> pixels = new List<ProbePixel>();
            public string error;
        }
        [Serializable] private sealed class ProbeAsset { public string path, guid, sha256; }
        [Serializable] private sealed class ProbeSlot
        {
            public string slot, sourceAsset;
            public int rendererIndex, destinationSubmesh, vertexOffset, vertexCount;
            public List<string> overlayAssets = new List<string>();
        }
        [Serializable] private sealed class ProbeOwner { public int slot, sourceSubmesh, sourceTriangle; }
        [Serializable] private sealed class ProbePixel
        {
            public string region;
            public int x, y, geometricHitCount;
            public Color32 capturedRgb;
            public Vector3 rayOrigin, rayDirection;
            public List<ProbeHit> nearestHits = new List<ProbeHit>();
        }
        [Serializable] private sealed class ProbeHit
        {
            public string renderer, material, shader, baseMap, renderType, cullProperty;
            public int rendererIndex, submesh, materialIndex, triangle, renderQueue;
            public float distance, normalDotRay, cull, alphaClip;
            public Vector3 cameraLocalPoint, worldPoint, barycentric, a, b, c;
            public Vector2 atlasUv, baseMapScale, baseMapOffset;
            public List<ProbeOwner> owners;
        }
        private readonly struct ProbeTriangleKey : IEquatable<ProbeTriangleKey>
        {
            private readonly int renderer, submesh, a, b, c;
            public ProbeTriangleKey(int renderer, int submesh, int a, int b, int c)
            {
                this.renderer = renderer; this.submesh = submesh;
                this.a = Mathf.Min(a, Mathf.Min(b, c)); this.c = Mathf.Max(a, Mathf.Max(b, c));
                this.b = a + b + c - this.a - this.c;
            }
            public bool Equals(ProbeTriangleKey other) => renderer == other.renderer && submesh == other.submesh && a == other.a && b == other.b && c == other.c;
            public override bool Equals(object obj) => obj is ProbeTriangleKey other && Equals(other);
            public override int GetHashCode() { unchecked { return ((((renderer * 397) ^ submesh) * 397 ^ a) * 397 ^ b) * 397 ^ c; } }
        }
        private sealed class ProbeTriangle
        {
            public SkinnedMeshRenderer renderer;
            public Material material;
            public int rendererIndex, submesh, materialIndex, triangle;
            public Vector3 a, b, c;
            public Vector2 uvA, uvB, uvC;
            public Bounds bounds;
            public List<ProbeOwner> owners;
        }

        [UnityTest]
        public IEnumerator AuthoredGarmentsRenderAcrossPermittedShapesAndHumanoidFitPoses()
        {
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CharacterGarmentFit",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            Directory.CreateDirectory(directory);
            manifest = new Manifest { generatedUtc = DateTime.UtcNow.ToString("O"),
                sourceRevision = Environment.GetEnvironmentVariable("GAMESIM_SOURCE_SHA") ?? "not supplied by runner",
                providerAssemblyMvid = typeof(UmaBodyProvider).Assembly.ManifestModule.ModuleVersionId.ToString() };
            observedAtlases.Clear();
            firstAtlasSaved = false;
            Debug.Log("[Gamesim] Garment fit captures -> " + directory);
            cast = new GameObject("Garment fit provider", typeof(GamesimUmaCast));
            stage = new GameObject("Garment fit capture studio"); stage.transform.position = new Vector3(0f, -7200f, 0f);
            MakeCameraAndLights();
            yield return null;
            catalog = new UmaAppearanceCatalog(); provider = new UmaBodyProvider(catalog);
            Assert.That(catalog.Diagnostics, Is.Empty);
            foreach (string id in Ids) Assert.That(AppearanceEditing.Find(catalog, id), Is.Not.Null, id + " must be authored and indexed before capture.");
            foreach (string id in Ids)
            {
                var item = AppearanceEditing.Find(catalog, id);
                string body = item.CompatibleBodies.Single();
                foreach (string shape in Shapes)
                {
                    var appearance = Appearance(body, id, shape);
                    yield return Build(appearance);
                    var avatar = subject.GetComponent<DynamicCharacterAvatar>();
                    var animator = subject.GetComponentInChildren<Animator>();
                    AssertBody(avatar, animator, appearance, id);
                    foreach (string state in States)
                    {
                        var pose = Sample(animator, state);
                        // Animator.Update samples bones synchronously; the native skinning/render work
                        // and bone-parented accessories must also see that frozen pose in a real frame.
                        yield return null;
                        ObservePose(animator, state, pose);
                        Capture(avatar, animator, appearance, id, shape, state, "front", false, pose);
                        Capture(avatar, animator, appearance, id, shape, state, "back", false, pose);
                    }
                    SaveManifest();
                    Object.Destroy(subject); subject = null;
                    yield return null;
                }
            }
            Assert.That(manifest.photos.Count, Is.EqualTo(144), "Four fits x three supported shapes x six animation poses x two views.");

            // The same rig also supplies current head-detail evidence for the outstanding hair/headwear review.
            foreach (string body in new[] { UmaCastLibrary.FemaleRace, UmaCastLibrary.MaleRace })
            {
                string garment = body == UmaCastLibrary.FemaleRace ? Ids[0] : Ids[1];
                var afro = AppearanceEditing.Find(catalog, ProceduralHair.Prefix + "afro");
                var cap = AppearanceEditing.Find(catalog, ProceduralAccessories.Prefix + "cap");
                var sidePart = catalog.Items.FirstOrDefault(item => item.Slot == "Hair" && item.Fits(body)
                    && (item.Aliases.Contains("Hair_LeftPart_Recipe") || item.Label.IndexOf("side part", StringComparison.OrdinalIgnoreCase) >= 0
                        || item.Label.IndexOf("side-part", StringComparison.OrdinalIgnoreCase) >= 0));
                var looks = new List<(string name, AppearanceItem hair, AppearanceItem hat)>();
                if (afro != null && afro.Fits(body)) looks.Add(("afro", afro, null));
                if (afro != null && afro.Fits(body) && cap != null && cap.Fits(body)) looks.Add(("afro-cap", afro, cap));
                if (sidePart != null) looks.Add(("side-part", sidePart, null));
                else manifest.optionalNotes.Add(body + ": no compatible installed side-part Hair entry; no substitute was photographed as that style.");
                foreach (var look in looks)
                {
                    var appearance = Appearance(body, garment, "neutral");
                    AppearanceEditing.Wear(appearance, look.hair, catalog);
                    if (look.hat != null) AppearanceEditing.Wear(appearance, look.hat, catalog);
                    yield return Build(appearance);
                    var avatar = subject.GetComponent<DynamicCharacterAvatar>(); var animator = subject.GetComponentInChildren<Animator>();
                    AssertBody(avatar, animator, appearance, garment);
                    AssertHeadItem(avatar, look.hair);
                    if (look.hat != null) AssertHeadItem(avatar, look.hat);
                    var pose = Sample(animator, "Idle");
                    yield return null;
                    ObservePose(animator, "Idle", pose);
                    Capture(avatar, animator, appearance, garment, look.name, "Idle", "front", true, pose);
                    Capture(avatar, animator, appearance, garment, look.name, "Idle", "back", true, pose);
                    Object.Destroy(subject); subject = null; yield return null;
                    SaveManifest();
                }
            }
            // A horizontal swimming torso can have one side in shade. Require actual green
            // cloth in at least one of each pose's two views, while retaining both measurements.
            foreach (var pair in manifest.photos.Where(photo => !photo.headDetail).GroupBy(photo => photo.item + "/" + photo.shape + "/" + photo.state))
                if (!pair.Any(photo => photo.torsoPixelCount > 0 && photo.greenTorsoPixelCount >= Mathf.Max(1, photo.torsoPixelCount / 10)))
                    manifest.dyeFailures.Add(pair.Key + ": neither torso view contains at least 10% pixels with the requested green hue.");
            manifest.complete = true; SaveManifest();
            Assert.That(manifest.dyeFailures, Is.Empty, "The requested green Chest dye must exist in the built overlays and actual torso pixels; all images are retained for diagnosis.");
            Assert.That(manifest.layerProbeFiles.Count, Is.EqualTo(3), "Crew A neutral Idle, Cheer and Swim front retain their unchanged pixels plus bounded layer evidence.");
            Assert.That(manifest.layerProbeFailures, Is.Empty, "A failed layer probe is retained as an error, never accepted as pixel attribution.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manifest != null) SaveManifest(); // Keep metadata for any captures preceding an assertion failure.
            if (subject != null) Object.Destroy(subject);
            if (stage != null) Object.Destroy(stage);
            if (cast != null) Object.Destroy(cast);
            yield return null;
        }

        private CharacterAppearance Appearance(string body, string id, string shape)
        {
            var appearance = catalog.ChangeBody(catalog.Materialize(CharacterAppearance.Preset("emma-brown")), body);
            foreach (string dna in Dna)
            {
                var control = catalog.Controls.Single(value => value.Id == dna && value.Fits(body));
                if (shape != "neutral") AppearanceEditing.SetValue(appearance, dna, shape == "limits-low" ? control.Minimum : control.Maximum);
            }
            AppearanceEditing.Wear(appearance, AppearanceEditing.Find(catalog, id), catalog);
            AppearanceEditing.SetFabric(appearance, "Chest", new Color(.12f, .58f, .36f));
            return appearance;
        }
        private IEnumerator Build(CharacterAppearance appearance)
        {
            // UMA can recycle generated render textures after the previous body is destroyed.
            // Cached pixel observations belong only to this one body's completed build.
            observedAtlases.Clear();
            Assert.That(provider.TryCreate(new CharacterBodyRequest("garment-fit", "player", appearance, CharacterBuildPurpose.Studio, manifest.photos.Count + 1),
                stage.transform, Color.white, out var body), Is.True);
            subject = body.Root;
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            var state = subject.GetComponent<CharacterBodyBuildState>();
            while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(state.Ready, Is.True, "The real garment body did not finish its DNA and dye pass.");
            Assert.That(state.Substitution, Is.Null.Or.Empty, "Fit evidence must show the authored garment, never a fallback.");
            foreach (var child in subject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
            yield return null;
        }
        private void AssertBody(DynamicCharacterAvatar avatar, Animator animator, CharacterAppearance appearance, string id)
        {
            Assert.That(animator != null && animator.isHuman && animator.runtimeAnimatorController != null, Is.True, "The shipped Humanoid animation rig is required.");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion = false; animator.speed = 0f;
            // This owned studio body is outside every game camera. Animator.AlwaysAnimate alone
            // does not opt its skinned renderers into offscreen native deformation.
            foreach (var renderer in subject.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            Assert.That(avatar.GetWardrobeItem("Chest")?.name, Is.EqualTo(catalog.ResolveRecipeName(id)));
            var recipe = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(id));
            var parts = recipe.PackedLoad().slotsV3.Where(part => part != null).Select(part => part.id).ToArray();
            Assert.That(avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).Select(slot => slot.slotName).Intersect(parts), Is.Not.Empty);
            Assert.That(subject.GetComponentsInChildren<SkinnedMeshRenderer>().Any(renderer => renderer.enabled && renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0), Is.True);
            foreach (string dna in Dna)
                Assert.That(avatar.GetDNA()[dna].Value, Is.EqualTo(appearance.dna.Single(value => value.id == dna).value).Within(.001f), "Capture the requested " + dna + ", not the preset's default.");
        }
        private void AssertHeadItem(DynamicCharacterAvatar avatar, AppearanceItem item)
        {
            if (ProceduralHair.IsProcedural(item.Id) || ProceduralAccessories.IsProcedural(item.Id))
                Assert.That(subject.GetComponentsInChildren<GrownPiece>().Any(piece => piece.ItemId == item.Id
                    && piece.Mesh != null && piece.Mesh.vertexCount > 0 && piece.GetComponent<Renderer>().enabled), Is.True,
                    "The photographed head detail must contain its actual grown item: " + item.Id);
            else Assert.That(avatar.GetWardrobeItem(item.Slot)?.name, Is.EqualTo(catalog.ResolveRecipeName(item.Id)),
                "The photographed head detail must wear its exact installed recipe.");
        }
        private static PoseObservation Sample(Animator animator, string state)
        {
            animator.SetFloat("Speed", state == "Walk" || state == "Run" || state == "SwimForward" ? 1f : 0f);
            animator.SetBool("Running", state == "Run"); animator.SetBool("Seated", state == "SitIdle");
            animator.SetBool("Talking", false); animator.SetBool("Listening", false); animator.SetBool("Arguing", false);
            foreach (string cue in Activities) animator.SetBool(cue, state == "SwimForward" && cue == "Swimming");
            animator.SetInteger("DanceStyle", 0); animator.SetInteger("Pose", 0);
            Assert.That(animator.HasState(0, Animator.StringToHash(state)), Is.True, state + " must be a real controller state.");
            float phase = .35f;
            if (state == "Cheer")
            {
                // Select the actual clip's strongest two-arm raise before its 0.9 exit, rather than
                // labelling an arbitrary animation phase as raised arms. Both wrists must clear
                // their own shoulders, so this exercises the real underarm/sleeve fit.
                float highest = float.NegativeInfinity;
                for (int sample = 1; sample <= 17; sample++)
                {
                    float candidate = sample * .05f;
                    animator.Play(Animator.StringToHash(state), 0, candidate); animator.Update(0f);
                    float left = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.LeftHand).position
                        - animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position, animator.transform.up);
                    float right = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.RightHand).position
                        - animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position, animator.transform.up);
                    float raised = Mathf.Min(left, right);
                    if (raised > highest) { highest = raised; phase = candidate; }
                }
                Assert.That(highest, Is.GreaterThan(0f), "The shipped cheer must actually raise both wrists above their shoulders.");
            }
            animator.Play(Animator.StringToHash(state), 0, phase); animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, "The requested pose must actually be playing.");
            return new PoseObservation { sampleFrame = Time.frameCount, sampledPhase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                sampledBones = ObservedBones.Select(bone => animator.GetBoneTransform(bone).position).ToArray() };
        }
        private static void ObservePose(Animator animator, string state, PoseObservation pose)
        {
            pose.observedFrame = Time.frameCount;
            pose.observedPhase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Assert.That(pose.observedFrame, Is.GreaterThan(pose.sampleFrame), "Capture follows a native frame, not just a synchronous Animator.Update.");
            Assert.That(animator.speed, Is.Zero);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, "The native frame must retain the actual requested controller pose.");
            Assert.That(pose.observedPhase, Is.EqualTo(pose.sampledPhase).Within(.001f), "The sampled animation phase stays frozen while skinning catches up.");
            pose.maximumBoneDrift = ObservedBones.Select((bone, index) => Vector3.Distance(animator.GetBoneTransform(bone).position, pose.sampledBones[index])).Max();
            Assert.That(pose.maximumBoneDrift, Is.LessThanOrEqualTo(.001f), "The native frame must preserve the sampled Humanoid bones.");
        }
        private void MakeCameraAndLights()
        {
            var rig = new GameObject("Fit camera", typeof(Camera)); rig.transform.SetParent(stage.transform, false); camera = rig.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .12f); camera.cullingMask = 1 << Layer;
            foreach (var entry in new[] { ("Fit key", new Vector3(35f, -35f, 0f), 1.4f), ("Fit fill", new Vector3(30f, 145f, 0f), .9f) })
            {
                var lamp = new GameObject(entry.Item1, typeof(Light)); lamp.transform.SetParent(stage.transform, false); lamp.transform.localEulerAngles = entry.Item2;
                var light = lamp.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = entry.Item3; light.color = new Color(1f, .97f, .93f); light.cullingMask = 1 << Layer;
                if (entry.Item1 == "Fit key") keyLight = light; else fillLight = light;
            }
        }
        private void Capture(DynamicCharacterAvatar avatar, Animator animator, CharacterAppearance appearance, string id, string shape, string state, string view, bool headDetail, PoseObservation pose)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head).position; var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            // Use the torso, rather than a flexed head/neck, as the longitudinal axis. In Swim
            // this gives belly/back views of the horizontal garment instead of looking end-on.
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest).position;
            Vector3 up = (chest - hips).normalized;
            var across = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position - animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            Vector3 forward = Vector3.Cross(across, up).normalized;
            Assert.That(forward.sqrMagnitude, Is.GreaterThan(.9f));
            // These are studio fit images. Illuminate the photographed torso side at the same
            // angles even when the actual Humanoid turns horizontal, so a dark underside cannot
            // be mistaken for a hole. Both owned lights retain their original color/intensity.
            Vector3 viewedForward = forward * (view == "front" ? 1f : -1f);
            Vector3 viewedRight = Vector3.Cross(up, viewedForward).normalized;
            keyLight.transform.rotation = Quaternion.LookRotation(-viewedForward + viewedRight * .55f - up * .6f, up);
            fillLight.transform.rotation = Quaternion.LookRotation(-viewedForward - viewedRight * .45f + up * .25f, up);
            var points = PosedPoints();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            Vector3 target = headDetail ? head + up * .05f : bounds.center;
            camera.transform.position = target + forward * (view == "front" ? 6f : -6f);
            camera.transform.LookAt(target, up);
            var projected = points.Select(point => camera.transform.InverseTransformPoint(point)).ToArray();
            float vertical = projected.Max(point => point.y) - projected.Min(point => point.y);
            float horizontal = projected.Max(point => point.x) - projected.Min(point => point.x);
            if (!headDetail)
                camera.transform.position += camera.transform.right * ((projected.Max(point => point.x) + projected.Min(point => point.x)) / 2f)
                    + camera.transform.up * ((projected.Max(point => point.y) + projected.Min(point => point.y)) / 2f);
            camera.orthographicSize = headDetail ? .45f : Mathf.Max(vertical / 2f, horizontal / (2f * Width / Height)) * 1.12f;
            camera.aspect = (float)Width / Height;
            string file = id + "__" + shape + "__" + state + "__" + view + ".png";
            var render = new RenderTexture(Width, Height, 24); var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = render;
                var request = new RenderPipeline.StandardRequest { destination = render };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True, "The active render pipeline must support the actual fit camera request.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32(); var background = colors[0];
                Assert.That(colors.Count(color => Math.Abs(color.r - background.r) + Math.Abs(color.g - background.g) + Math.Abs(color.b - background.b) > 12),
                    Is.GreaterThan(Width * Height / 200), "A capture must contain rendered pixels, not an empty camera background.");
                File.WriteAllBytes(Path.Combine(directory, file), pixels.EncodeToPNG());
                var clip = animator.GetCurrentAnimatorClipInfo(0);
                Assert.That(clip, Is.Not.Empty, "The Humanoid pose must sample an actual clip.");
                var torsoRegion = TorsoRegion(chest, hips, across.magnitude);
                var torsoColors = colors.Where((color, index) => torsoRegion.Contains(new Vector2Int(index % Width, index / Width))).ToArray();
                int greenPixels = torsoColors.Count(color => color.g > 40 && color.g > color.r * 1.15f && color.g > color.b * 1.10f);
                var overlays = GarmentOverlays(avatar, id);
                if (!headDetail)
                {
                    Color expected = new Color(.12f, .58f, .36f);
                    if (overlays.Count == 0 || overlays.Any(value => value.shared || Mathf.Abs(value.channelZero.r - expected.r) > .001f
                        || Mathf.Abs(value.channelZero.g - expected.g) > .001f || Mathf.Abs(value.channelZero.b - expected.b) > .001f))
                        manifest.dyeFailures.Add(file + ": built garment overlays do not retain their requested private green channel zero.");
                }
                manifest.photos.Add(new Photo { file = file, item = id, recipe = catalog.ResolveRecipeName(id), body = appearance.bodyId, shape = shape,
                    state = state, clip = clip[0].clip.name, view = view, headDetail = headDetail, appearanceKey = appearance.ContentKey(),
                    sampleFrame = pose.sampleFrame, observedFrame = pose.observedFrame, readFrame = Time.frameCount,
                    sampledPhase = pose.sampledPhase, observedPhase = pose.observedPhase, maximumBoneDrift = pose.maximumBoneDrift,
                    torsoUp = up, torsoForward = forward, torsoPixelRegion = torsoRegion, torsoPixelCount = torsoColors.Length, greenTorsoPixelCount = greenPixels,
                    keyLightForward = keyLight.transform.forward, fillLightForward = fillLight.transform.forward,
                    keyLightIntensity = keyLight.intensity, fillLightIntensity = fillLight.intensity,
                    meanTorsoColor = torsoColors.Length == 0 ? Color.clear : new Color((float)torsoColors.Average(value => (int)value.r) / 255f,
                        (float)torsoColors.Average(value => (int)value.g) / 255f, (float)torsoColors.Average(value => (int)value.b) / 255f),
                    actualOverlays = overlays, actualMaterials = GarmentMaterials(avatar, id),
                    normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime, cameraSize = camera.orthographicSize, cameraPosition = camera.transform.position,
                    cameraUp = camera.transform.up, head = head, hips = hips, leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                    rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position, requestedDna = appearance.dna.Select(value => value.Clone()).ToList(),
                    leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position, rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position,
                    wardrobe = appearance.outfits.Single(outfit => outfit.id == appearance.activeOutfit).wardrobe.Select(value => value.Clone()).ToList(),
                    outfitColors = appearance.outfits.Single(outfit => outfit.id == appearance.activeOutfit).colors.Select(value => value.Clone()).ToList(),
                    builtDna = Dna.Select(dna => new AppearanceValue { id = dna, value = avatar.GetDNA()[dna].Value }).ToList() });
                if (!headDetail && id == Ids[0] && shape == "neutral" && view == "front"
                    && (state == "Idle" || state == "Cheer" || state == "SwimForward"))
                    ProbeLayers(avatar, animator, appearance, file, state, colors);
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; render.Release(); Object.Destroy(render); Object.Destroy(pixels); }
        }
        private RectInt TorsoRegion(Vector3 chest, Vector3 hips, float shoulderWidth)
        {
            var center = camera.WorldToViewportPoint(Vector3.Lerp(hips, chest, .65f));
            var side = camera.WorldToViewportPoint(Vector3.Lerp(hips, chest, .65f) + camera.transform.right * shoulderWidth * .12f);
            int radius = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(side.x - center.x) * Width));
            int x = Mathf.Clamp(Mathf.RoundToInt(center.x * Width) - radius, 0, Width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(center.y * Height) - radius, 0, Height - 1);
            return new RectInt(x, y, Mathf.Min(radius * 2 + 1, Width - x), Mathf.Min(radius * 2 + 1, Height - y));
        }
        private void ProbeLayers(DynamicCharacterAvatar avatar, Animator animator, CharacterAppearance appearance, string file, string state, Color32[] colors)
        {
            var report = new LayerProbeReport { sourceRevision = manifest.sourceRevision, providerAssemblyMvid = manifest.providerAssemblyMvid,
                photo = file, photoSha256 = ProbeHash(Path.Combine(directory, file)), state = state, appearanceKey = appearance.ContentKey(),
                generatedUtc = DateTime.UtcNow.ToString("O"), frameBefore = Time.frameCount,
                cameraPosition = camera.transform.position, cameraRotation = camera.transform.rotation,
                subjectPosition = subject.transform.position, subjectRotation = subject.transform.rotation,
                animationPhaseBefore = animator.GetCurrentAnimatorStateInfo(0).normalizedTime };
            var bones = ObservedBones.Select(bone => animator.GetBoneTransform(bone).position).ToArray();
            var owned = new List<Mesh>();
            try
            {
                var renderers = avatar.umaData.GetRenderers();
                var ownership = new Dictionary<ProbeTriangleKey, List<ProbeOwner>>();
                var activeSlots = new List<SlotData>();
                foreach (var fragment in avatar.umaData.generatedMaterials.materials.SelectMany(material => material.materialFragments))
                    if (fragment.slotData?.asset?.meshData != null && !activeSlots.Any(slot => ReferenceEquals(slot, fragment.slotData)))
                    {
                        Assert.That(activeSlots.Count, Is.LessThan(128));
                        activeSlots.Add(fragment.slotData);
                    }
                foreach (var slot in activeSlots)
                {
                    if (slot.skinnedMeshRenderer < 0 || slot.skinnedMeshRenderer >= renderers.Length
                        || renderers[slot.skinnedMeshRenderer] == null || !renderers[slot.skinnedMeshRenderer].enabled) continue;
                    // Only actual generated fragment slots were combined, avoiding stale
                    // recipe offsets after suppression or a different material selection.
                    Assert.That(report.slots.Count, Is.LessThan(128), "Layer diagnosis has a finite live slot budget.");
                    int slotIndex = report.slots.Count;
                    report.slots.Add(new ProbeSlot { slot = slot.slotName, sourceAsset = ProbeSource(report, slot.asset),
                        rendererIndex = slot.skinnedMeshRenderer, destinationSubmesh = slot.submeshIndex, vertexOffset = slot.vertexOffset, vertexCount = slot.asset.meshData.vertexCount,
                        overlayAssets = slot.GetOverlayList().Where(overlay => overlay?.asset != null).Select(overlay => ProbeSource(report, overlay.asset)).ToList() });
                    // The installed combiner maps only asset.subMeshIndex; unused source
                    // submeshes must not claim another material's drawn triangle.
                    int submesh = slot.asset.subMeshIndex;
                    Assert.That(submesh >= 0 && submesh < slot.asset.meshData.submeshes.Length && slot.submeshIndex >= 0, Is.True);
                    {
                        // This NativeArray belongs to UMA; it is only read, never disposed or modified.
                        var triangles = slot.asset.meshData.submeshes[submesh].GetTriangles(0);
                        Assert.That(triangles.Length % 3, Is.Zero);
                        report.sourceTriangleCount += triangles.Length / 3;
                        Assert.That(report.sourceTriangleCount, Is.LessThanOrEqualTo(200000), "No unbounded source triangle attribution scan.");
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            var key = new ProbeTriangleKey(slot.skinnedMeshRenderer, slot.submeshIndex, triangles[t] + slot.vertexOffset,
                                triangles[t + 1] + slot.vertexOffset, triangles[t + 2] + slot.vertexOffset);
                            if (!ownership.TryGetValue(key, out var owners)) ownership.Add(key, owners = new List<ProbeOwner>());
                            owners.Add(new ProbeOwner { slot = slotIndex, sourceSubmesh = submesh, sourceTriangle = t / 3 });
                        }
                    }
                }
                foreach (var entry in appearance.outfits.Single(outfit => outfit.id == appearance.activeOutfit).wardrobe)
                    ProbeSource(report, avatar.GetWardrobeItem(entry.slot));

                var surfaces = new List<ProbeTriangle>();
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    if (renderer == null || !renderer.enabled || renderer.sharedMesh == null || !renderer.gameObject.activeInHierarchy) continue;
                    Assert.That(++report.rendererCount, Is.LessThanOrEqualTo(16));
                    var baked = new Mesh { name = "Owned fit layer snapshot" }; owned.Add(baked);
                    renderer.BakeMesh(baked);
                    var vertices = baked.vertices; var uv = baked.uv;
                    report.vertexCount += vertices.Length;
                    Assert.That(report.vertexCount, Is.LessThanOrEqualTo(200000));
                    Assert.That(uv.Length, Is.EqualTo(vertices.Length), "Actual combined atlas UV must accompany the posed vertices.");
                    // Use one ordinary transform frame (positive view depth) for both rays
                    // and points; worldToCameraMatrix would reverse the mesh's Z instead.
                    var toCamera = camera.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        vertices[v] = toCamera.MultiplyPoint3x4(vertices[v]);
                        if (float.IsNaN(vertices[v].x) || float.IsInfinity(vertices[v].x) || float.IsNaN(vertices[v].y)
                            || float.IsInfinity(vertices[v].y) || float.IsNaN(vertices[v].z) || float.IsInfinity(vertices[v].z))
                            throw new InvalidOperationException("A posed diagnostic vertex is not finite.");
                    }
                    var materials = renderer.sharedMaterials;
                    int passes = Mathf.Max(baked.subMeshCount, materials.Length);
                    for (int materialIndex = 0; materialIndex < passes; materialIndex++)
                    {
                        int submesh = Mathf.Min(materialIndex, baked.subMeshCount - 1);
                        Assert.That(submesh, Is.GreaterThanOrEqualTo(0));
                        var indices = baked.GetTriangles(submesh);
                        Assert.That(indices.Length % 3, Is.Zero);
                        report.triangleCount += indices.Length / 3;
                        Assert.That(report.triangleCount, Is.LessThanOrEqualTo(200000), "No unbounded posed triangle scan.");
                        for (int t = 0; t < indices.Length; t += 3)
                        {
                            int ia = indices[t], ib = indices[t + 1], ic = indices[t + 2];
                            if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length)
                                throw new InvalidOperationException("A drawn diagnostic triangle references a missing posed vertex.");
                            ownership.TryGetValue(new ProbeTriangleKey(rendererIndex, submesh, ia, ib, ic), out var owners);
                            if (owners == null) report.unattributedTriangles++;
                            var bounds = new Bounds(vertices[ia], Vector3.zero); bounds.Encapsulate(vertices[ib]); bounds.Encapsulate(vertices[ic]);
                            surfaces.Add(new ProbeTriangle { renderer = renderer, rendererIndex = rendererIndex, submesh = submesh,
                                materialIndex = materialIndex, material = materialIndex < materials.Length ? materials[materialIndex] : null,
                                triangle = t / 3, a = vertices[ia], b = vertices[ib], c = vertices[ic], uvA = uv[ia], uvB = uv[ib], uvC = uv[ic], bounds = bounds, owners = owners });
                        }
                    }
                }
                Assert.That(surfaces, Is.Not.Empty);
                // These fixed, labelled regions come from retained 6c7e native PNGs. They are
                // observations, not pass/fail silhouettes; a changed composition stays explicit.
                var regions = state == "Idle" ? new[] { ("outer-breast", new RectInt(311, 284, 17, 20)), ("sternum", new RectInt(378, 294, 16, 14)) }
                    : state == "Cheer" ? new[] { ("left-inner-arm", new RectInt(305, 275, 34, 40)), ("right-inner-arm", new RectInt(465, 274, 33, 33)), ("sternum", new RectInt(396, 320, 18, 13)) }
                    : new[] { ("left-inner-arm", new RectInt(306, 311, 30, 60)), ("right-inner-arm", new RectInt(439, 311, 30, 60)), ("sternum", new RectInt(377, 371, 18, 14)) };
                foreach (var region in regions)
                    for (int row = 0; row < 7; row++)
                        for (int column = 0; column < 7; column++)
                        {
                            int x = region.Item2.x + Mathf.RoundToInt((region.Item2.width - 1) * column / 6f);
                            int y = region.Item2.y + Mathf.RoundToInt((region.Item2.height - 1) * row / 6f);
                            var worldRay = camera.ViewportPointToRay(new Vector3((x + .5f) / Width, (Height - y - .5f) / Height, 0));
                            var ray = new Ray(camera.transform.InverseTransformPoint(worldRay.origin), camera.transform.InverseTransformDirection(worldRay.direction));
                            var pixel = new ProbePixel { region = region.Item1, x = x, y = y, capturedRgb = colors[(Height - y - 1) * Width + x],
                                rayOrigin = ray.origin, rayDirection = ray.direction };
                            var hits = new List<ProbeHit>();
                            foreach (var triangle in surfaces)
                            {
                                if (++report.triangleTests > 20000000)
                                    throw new InvalidOperationException("Layer diagnosis exceeded its 20 million bounded triangle/bounds tests.");
                                if (!triangle.bounds.IntersectRay(ray, out float boundDistance) || boundDistance > camera.farClipPlane
                                    || !ProbeIntersection(ray, triangle, out float distance, out var barycentric)) continue;
                                pixel.geometricHitCount++;
                                var material = triangle.material;
                                var point = ray.GetPoint(distance);
                                string cullProperty = material != null && material.HasProperty("_Cull") ? "_Cull" : material != null && material.HasProperty("_CullMode") ? "_CullMode" : "missing";
                                hits.Add(new ProbeHit { renderer = triangle.renderer.name, rendererIndex = triangle.rendererIndex, submesh = triangle.submesh,
                                    materialIndex = triangle.materialIndex, triangle = triangle.triangle, owners = triangle.owners,
                                    distance = distance, barycentric = barycentric, cameraLocalPoint = point, worldPoint = camera.transform.TransformPoint(point),
                                    a = triangle.a, b = triangle.b, c = triangle.c,
                                    normalDotRay = Vector3.Dot(Vector3.Cross(triangle.b - triangle.a, triangle.c - triangle.a).normalized, ray.direction),
                                    atlasUv = triangle.uvA * barycentric.x + triangle.uvB * barycentric.y + triangle.uvC * barycentric.z,
                                    baseMapScale = material != null && material.HasProperty("_BaseMap") ? material.GetTextureScale("_BaseMap") : Vector2.one,
                                    baseMapOffset = material != null && material.HasProperty("_BaseMap") ? material.GetTextureOffset("_BaseMap") : Vector2.zero,
                                    material = material == null ? "missing" : material.name, shader = material?.shader == null ? "missing" : material.shader.name,
                                    baseMap = material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap").name : "missing",
                                    renderType = material == null ? "missing" : material.GetTag("RenderType", false, "missing"), renderQueue = material == null ? -1 : material.renderQueue,
                                    cullProperty = cullProperty, cull = cullProperty == "missing" ? -1f : material.GetFloat(cullProperty),
                                    alphaClip = material != null && material.HasProperty("_AlphaClip") ? material.GetFloat("_AlphaClip") : -1f });
                            }
                            pixel.nearestHits = hits.OrderBy(hit => hit.distance).Take(16).ToList();
                            report.pixels.Add(pixel);
                        }
                report.frameAfter = Time.frameCount;
                report.animationPhaseAfter = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                report.maximumBoneDrift = ObservedBones.Select((bone, index) => Vector3.Distance(animator.GetBoneTransform(bone).position, bones[index])).Max();
                Assert.That(report.frameAfter, Is.EqualTo(report.frameBefore));
                Assert.That(report.animationPhaseAfter, Is.EqualTo(report.animationPhaseBefore));
                Assert.That(report.maximumBoneDrift, Is.Zero);
                Assert.That(subject.transform.position == report.subjectPosition && subject.transform.rotation == report.subjectRotation
                    && camera.transform.position == report.cameraPosition && camera.transform.rotation == report.cameraRotation, Is.True, "Diagnosis cannot move actors or the capture lens.");
                Assert.That(ProbeHash(Path.Combine(directory, file)), Is.EqualTo(report.photoSha256), "The ordinary native PNG is retained byte-identically.");
                report.complete = true;
            }
            catch (Exception exception)
            {
                report.error = exception.GetType().Name + ": " + exception.Message;
                manifest.layerProbeFailures.Add(file + ": " + report.error);
            }
            finally
            {
                foreach (var mesh in owned) if (mesh != null) Object.Destroy(mesh);
                string path = "diagnostics/" + Path.GetFileNameWithoutExtension(file) + "-layers.json";
                Directory.CreateDirectory(Path.Combine(directory, "diagnostics"));
                File.WriteAllText(Path.Combine(directory, path), JsonUtility.ToJson(report, true));
                manifest.layerProbeFiles.Add(path);
            }
        }
        private static bool ProbeIntersection(Ray ray, ProbeTriangle triangle, out float distance, out Vector3 barycentric)
        {
            distance = 0; barycentric = Vector3.zero;
            var edge1 = triangle.b - triangle.a; var edge2 = triangle.c - triangle.a;
            var cross = Vector3.Cross(ray.direction, edge2);
            float determinant = Vector3.Dot(edge1, cross);
            if (Mathf.Abs(determinant) < 1e-10f) return false;
            float inverse = 1f / determinant; var from = ray.origin - triangle.a;
            float u = Vector3.Dot(from, cross) * inverse;
            if (u < 0 || u > 1) return false;
            var q = Vector3.Cross(from, edge1); float v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < 0 || u + v > 1) return false;
            distance = Vector3.Dot(edge2, q) * inverse;
            if (distance < 0 || distance > 20f) return false;
            barycentric = new Vector3(1f - u - v, u, v); return true;
        }
        private static string ProbeSource(LayerProbeReport report, Object asset)
        {
            if (asset == null) return "missing";
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return "runtime:" + asset.name;
            if (!report.sources.Any(source => source.path == path))
            {
                Assert.That(report.sources.Count, Is.LessThan(128));
                string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
                report.sourceBytes += new FileInfo(absolute).Length;
                Assert.That(report.sourceBytes, Is.LessThanOrEqualTo(32L * 1024 * 1024), "All source identity reads together remain bounded.");
                report.sources.Add(new ProbeAsset { path = path, guid = UnityEditor.AssetDatabase.AssetPathToGUID(path), sha256 = ProbeHash(absolute) });
            }
            return path;
#else
            return "runtime:" + asset.name + " (Editor source hashing unavailable)";
#endif
        }
        private static string ProbeHash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private List<GarmentOverlay> GarmentOverlays(DynamicCharacterAvatar avatar, string id)
        {
            var parts = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(id)).PackedLoad().slotsV3
                .Where(part => part != null).Select(part => part.id).ToArray();
            return avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null && parts.Contains(slot.slotName))
                .SelectMany(slot => slot.GetOverlayList().Where(overlay => overlay?.colorData != null).Select(overlay => new GarmentOverlay {
                    slot = slot.slotName, overlay = overlay.asset == null ? "missing" : overlay.asset.name, shared = overlay.colorData.IsASharedColor,
                    channelZero = overlay.colorData.channelMask != null && overlay.colorData.channelMask.Length > 0 ? overlay.colorData.channelMask[0] : Color.clear })).ToList();
        }
        private List<GarmentMaterial> GarmentMaterials(DynamicCharacterAvatar avatar, string id)
        {
            var slots = GarmentOverlays(avatar, id).Select(value => value.slot).ToArray();
            return avatar.umaData.generatedMaterials.materials.Where(value => value.material != null && value.materialFragments.Any(fragment => fragment.slotData != null && slots.Contains(fragment.slotData.slotName)))
                .Select(value => ObserveMaterial(value, slots)).ToList();
        }
        private GarmentMaterial ObserveMaterial(UMAData.GeneratedMaterial value, string[] slots)
        {
            string colorProperty = value.material.HasProperty("_BaseColor") ? "_BaseColor" : value.material.HasProperty("_Color") ? "_Color" : "missing";
            var rendererBlock = new MaterialPropertyBlock(); var indexedBlock = new MaterialPropertyBlock();
            if (value.skinnedMeshRenderer != null)
            {
                value.skinnedMeshRenderer.GetPropertyBlock(rendererBlock);
                value.skinnedMeshRenderer.GetPropertyBlock(indexedBlock, value.materialIndex);
            }
            var texture = value.material.HasProperty("_BaseMap") ? value.material.GetTexture("_BaseMap") : null;
            return new GarmentMaterial { name = value.material.name, shader = value.material.shader == null ? "missing" : value.material.shader.name,
                renderer = value.skinnedMeshRenderer == null ? "missing" : value.skinnedMeshRenderer.name, materialIndex = value.materialIndex,
                baseMap = texture == null ? "missing" : texture.name, colorProperty = colorProperty,
                baseColor = colorProperty == "missing" ? Color.clear : value.material.GetColor(colorProperty),
                rendererBlockEmpty = rendererBlock.isEmpty, indexedBlockEmpty = indexedBlock.isEmpty,
                rendererColorOverride = colorProperty != "missing" && rendererBlock.HasColor(colorProperty),
                indexedColorOverride = colorProperty != "missing" && indexedBlock.HasColor(colorProperty),
                rendererOverrideColor = colorProperty == "missing" ? Color.clear : rendererBlock.GetColor(colorProperty),
                indexedOverrideColor = colorProperty == "missing" ? Color.clear : indexedBlock.GetColor(colorProperty),
                rendererOverrideMap = rendererBlock.GetTexture("_BaseMap") == null ? "none" : rendererBlock.GetTexture("_BaseMap").name,
                indexedOverrideMap = indexedBlock.GetTexture("_BaseMap") == null ? "none" : indexedBlock.GetTexture("_BaseMap").name,
                baseMapScale = value.material.HasProperty("_BaseMap") ? value.material.GetTextureScale("_BaseMap") : Vector2.zero,
                baseMapOffset = value.material.HasProperty("_BaseMap") ? value.material.GetTextureOffset("_BaseMap") : Vector2.zero,
                fragments = value.materialFragments.Where(fragment => fragment.slotData != null && slots.Contains(fragment.slotData.slotName))
                    .Select(fragment => new FragmentColor { slot = fragment.slotData.slotName, baseColor = fragment.baseColor,
                        multiplier = fragment.GetMultiplier(0, 0), additive = fragment.GetAdditive(0, 0), atlasRegion = fragment.atlasRegion }).ToList(),
                atlas = ObserveAtlas(texture) };
        }
        private AtlasObservation ObserveAtlas(Texture texture)
        {
            if (texture == null) return null;
            if (observedAtlases.TryGetValue(texture, out var observation)) return observation;
            var previous = RenderTexture.active; bool previousWrite = GL.sRGBWrite;
            RenderTexture temporary = null; Texture2D readable = null;
            try
            {
                // Read the actual generated atlas without changing its material, sampling state,
                // texture or lifetime. A small copy is enough to separate a gray atlas from a green one.
                temporary = RenderTexture.GetTemporary(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                readable = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
                GL.sRGBWrite = false; Graphics.Blit(texture, temporary); RenderTexture.active = temporary;
                readable.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); readable.Apply();
                var pixels = readable.GetPixels(); var visible = pixels.Where(pixel => pixel.a > .1f && pixel.r + pixel.g + pixel.b > .03f).ToArray();
                observation = new AtlasObservation { sourceTexture = texture.name, sourceWidth = texture.width, sourceHeight = texture.height,
                    sampledPixels = pixels.Length, visiblePixels = visible.Length,
                    greenPixels = visible.Count(pixel => pixel.g > .03f && pixel.g > pixel.r * 1.15f && pixel.g > pixel.b * 1.10f),
                    meanVisibleColor = visible.Length == 0 ? Color.clear : new Color(visible.Average(pixel => pixel.r), visible.Average(pixel => pixel.g), visible.Average(pixel => pixel.b)) };
                if (!firstAtlasSaved)
                {
                    Directory.CreateDirectory(Path.Combine(directory, "diagnostics"));
                    observation.representativeFile = "diagnostics/garment-atlas-first-linear.png";
                    File.WriteAllBytes(Path.Combine(directory, observation.representativeFile), readable.EncodeToPNG());
                    firstAtlasSaved = true;
                }
                observedAtlases.Add(texture, observation);
                return observation;
            }
            finally
            {
                GL.sRGBWrite = previousWrite; RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) Object.Destroy(readable);
            }
        }
        private Vector3[] PosedPoints()
        {
            var points = new List<Vector3>();
            foreach (var renderer in subject.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled))
            {
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    var baked = new Mesh();
                    try { skin.BakeMesh(baked); points.AddRange(baked.vertices.Select(vertex => skin.transform.TransformPoint(vertex))); }
                    finally { Object.Destroy(baked); }
                }
                else { points.Add(renderer.bounds.min); points.Add(renderer.bounds.max); }
            }
            Assert.That(points, Is.Not.Empty, "The camera frames the actual posed body geometry.");
            return points.ToArray();
        }
        private void SaveManifest() => File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true));
    }
}
