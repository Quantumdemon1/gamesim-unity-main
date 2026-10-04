using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private UmaAppearanceCatalog catalog;
        private UmaBodyProvider provider;
        private string directory;
        private Manifest manifest;

        [Serializable] private sealed class Manifest
        {
            public string sourceRevision, providerAssemblyMvid, generatedUtc;
            public bool complete;
            public List<Photo> photos = new List<Photo>();
            public List<string> optionalNotes = new List<string>();
            public List<string> dyeFailures = new List<string>();
        }
        [Serializable] private sealed class Photo
        {
            public string file, item, recipe, body, shape, state, clip, view, appearanceKey;
            public bool headDetail;
            public float normalizedTime, cameraSize;
            public int sampleFrame, observedFrame, readFrame, torsoPixelCount, greenTorsoPixelCount;
            public float sampledPhase, observedPhase, maximumBoneDrift;
            public Vector3 cameraPosition, cameraUp, torsoUp, torsoForward, head, hips, leftHand, rightHand, leftShoulder, rightShoulder;
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
            public string name, shader, renderer, baseMap;
            public int materialIndex;
            public Color baseColor;
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
                .Select(value => new GarmentMaterial { name = value.material.name, shader = value.material.shader == null ? "missing" : value.material.shader.name,
                    renderer = value.skinnedMeshRenderer == null ? "missing" : value.skinnedMeshRenderer.name, materialIndex = value.materialIndex,
                    baseMap = value.material.HasProperty("_BaseMap") && value.material.GetTexture("_BaseMap") != null ? value.material.GetTexture("_BaseMap").name : "missing",
                    baseColor = value.material.HasProperty("_BaseColor") ? value.material.GetColor("_BaseColor") : Color.clear }).ToList();
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
