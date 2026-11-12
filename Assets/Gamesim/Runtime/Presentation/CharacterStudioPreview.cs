using System;
using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>One isolated body and camera. Pending edits coalesce; old pixels stay until the final build is ready.</summary>
    public sealed class CharacterStudioPreview : MonoBehaviour
    {
        private static int nextStage;
        private Camera cameraRig;
        private GameObject subject;
        private GameObject buildRoot;
        private RenderTexture texture;
        private CharacterAppearance pending;
        private string shownKey, requestedKey, fallbackStatus;
        private float buildAt, turn, zoom = 1f;
        private int revision;
        private bool dirty, building, headShot;
        private bool fitDeformedBody, settleRenders;
        private bool renderPending, posePending, publishBuild;
        private int renderAtFrame, renderRevision;
        private string readyStatus;
        private Renderer[] framingRenderers;
        private Mesh framingMesh;
        private readonly List<Vector3> framingVertices = new List<Vector3>();
        private double requestedAt;
        private double buildStartedAt;
        public float BuildTimeoutSeconds { get; set; } = 20f;
        public bool CanRetry { get; private set; }
        /// <summary>Original body state immediately before fallback releases it; null after a new request.</summary>
        public string LastFailureDiagnostic { get; private set; }
        public Texture Texture => texture;
        /// <summary>Includes a requested creator view whose old pixels are still displayed.</summary>
        public bool IsBuilding => dirty || building || renderPending;
        public string Status { get; private set; }
        public event Action StatusChanged;
        public const string FallbackDisplayStatus = "Preview unavailable.\nYour look is kept.";
        /// <summary>The compact screen copy; Status retains the detailed failure reason for diagnostics.</summary>
        public string DisplayStatus => CanRetry ? FallbackDisplayStatus : Status;
        /// <summary>The last published image's key. A requested view is ready only when !IsBuilding too.</summary>
        public string CompletedKey => shownKey;
        /// <summary>How far round the body is turned, in degrees: what a drag or a slow turn has done to it.</summary>
        public float Turn => turn;
        public double LastBuildMilliseconds { get; private set; }

        public static CharacterStudioPreview Create(string name = "Character studio", bool fitDeformedBody = false, bool settleRenders = false)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.DontSave };
            root.transform.position = new Vector3(nextStage++ * 20f, -6500f, 0f);
            var preview = root.AddComponent<CharacterStudioPreview>();
            preview.fitDeformedBody = fitDeformedBody;
            preview.settleRenders = settleRenders;
            preview.Initialise();
            return preview;
        }

        private void Initialise()
        {
            var cameraObject = new GameObject("Studio camera");
            cameraObject.transform.SetParent(transform, false);
            cameraRig = cameraObject.AddComponent<Camera>();
            cameraRig.enabled = false;
            cameraRig.clearFlags = CameraClearFlags.SolidColor;
            cameraRig.backgroundColor = new Color(.035f, .055f, .075f);
            cameraRig.nearClipPlane = .03f;
            cameraRig.farClipPlane = 12f;
            cameraRig.fieldOfView = 32f;
            texture = new RenderTexture(640, 800, 24, RenderTextureFormat.ARGB32)
            { name = "Character studio preview", antiAliasing = 2, hideFlags = HideFlags.DontSave };
            texture.Create();
            cameraRig.targetTexture = texture;
            // A portrait studio's lights: a warm-white key, a neutral fill and a soft bounce from
            // below, with the house's cool colour kept to the rim. The fill used to be blue and
            // nearly as strong as the key, and blue light on a brown face goes grey: deep skin
            // came out of the studio ashen whatever its colour.
            AddLight("Key", new Vector3(-2f, 3f, 3f), new Color(1f, .95f, .9f), 6.5f);
            AddLight("Fill", new Vector3(2.2f, 1.6f, 2.4f), new Color(1f, .97f, .94f), 3.2f);
            AddLight("Bounce", new Vector3(0f, -.4f, 2.2f), new Color(1f, .9f, .82f), 1.3f);
            AddLight("Rim", new Vector3(0f, 2.2f, -2.2f), new Color(.72f, .87f, 1f), 4f);
        }

        private void AddLight(string name, Vector3 position, Color color, float intensity)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            var lamp = root.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 8f; lamp.color = color;
            lamp.intensity = intensity; lamp.shadows = LightShadows.None;
        }

        /// <summary>
        /// Clears to nothing rather than the studio's dark blue, so the houseguest stands on whatever
        /// the picture is laid over - the creator's platform. Portraits keep their backdrop.
        /// </summary>
        public bool Transparent
        {
            set
            {
                var clear = cameraRig.backgroundColor;
                clear.a = value ? 0f : 1f;
                cameraRig.backgroundColor = clear;
                RenderCompleted();
            }
        }

        public void Show(CharacterAppearance appearance)
        {
            appearance = appearance ?? new CharacterAppearance();
            string key = appearance.ContentKey();
            if (key == requestedKey) return;
            requestedKey = key;
            LastFailureDiagnostic = null;
            pending = appearance.Clone();
            revision++;
            renderPending = posePending = publishBuild = false;
            requestedAt = Time.realtimeSinceStartupAsDouble;
            dirty = true;
            buildAt = Time.unscaledTime + .16f;
            Status = "Updating preview…";
            CanRetry = false;
            StatusChanged?.Invoke();
        }

        public void Retry()
        {
            if (pending == null || !CanRetry) return;
            requestedKey = null;
            Show(pending);
        }

        public void Rotate(float degrees) { turn += degrees; RenderCompleted(); }
        public void View(float degrees) { turn = degrees; RenderCompleted(); }
        public void Zoom(float amount) { zoom = Mathf.Clamp(zoom + amount, .7f, 1.4f); RenderCompleted(); }
        public void FocusFace(bool value) { headShot = value; RenderCompleted(); }

        private void Update()
        {
            if (dirty && Time.unscaledTime >= buildAt) Build();
            // Provider completion and image publication are separate. IsBuilding also includes this queue.
            if (settleRenders && renderPending && !building && !dirty) AdvanceRender();
            if (!building || dirty) return;
            if (subject == null || Time.realtimeSinceStartupAsDouble - buildStartedAt >= Mathf.Clamp(BuildTimeoutSeconds, .05f, 60f))
            {
                UseFallback(subject == null ? "The character preview stopped building." : "The character preview took too long.");
                Complete(null);
                return;
            }
            var state = subject.GetComponent<CharacterBodyBuildState>();
            if (state != null && (state.Revision != revision || !state.Ready)) return;
            var renderers = subject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Complete(state);
        }

        private void Complete(CharacterBodyBuildState state)
        {
            building = false;
            // Only the creator opts in. The cast's slow turn renders every frame and must not bake.
            framingRenderers = fitDeformedBody ? subject.GetComponentsInChildren<Renderer>() : null;
            if (settleRenders)
            {
                readyStatus = state != null && !string.IsNullOrEmpty(state.Substitution) ? state.Substitution
                    : fallbackStatus ?? "Preview ready";
                publishBuild = true;
                RenderCompleted();
                return;
            }
            shownKey = requestedKey;
            LastBuildMilliseconds = (Time.realtimeSinceStartupAsDouble - requestedAt) * 1000;
            Status = state != null && !string.IsNullOrEmpty(state.Substitution) ? state.Substitution
                : fallbackStatus ?? "Preview ready";
            StatusChanged?.Invoke();
            RenderCompleted();
        }

        private void Build()
        {
            dirty = false;
            fallbackStatus = null;
            ReleaseBody();
            buildRoot = new GameObject("Preview revision " + revision);
            buildRoot.transform.SetParent(transform, false);
            buildStartedAt = Time.realtimeSinceStartupAsDouble;
            try
            {
                var request = new CharacterBodyRequest("studio", pending.fallbackId, pending, CharacterBuildPurpose.Studio, revision);
                if (CharacterBodySource.TryCreate(request, buildRoot.transform, Color.white, out var body) && body.Exists)
                    subject = body.Root;
                else UseFallback("Modular content is unavailable.");
            }
            catch (Exception exception)
            {
                // A provider may allocate children before failing. The revision container owns all of them.
                UseFallback("The character could not be built (" + exception.GetType().Name + ").");
            }
            foreach (var collider in subject.GetComponentsInChildren<Collider>()) Destroy(collider);
            building = true;
        }

        /// <summary>
        /// A body that could not be built is a plain placeholder, never somebody else's body: the
        /// cast is UMA's and there is no other (2026-09-27).
        /// </summary>
        private void UseFallback(string reason)
        {
            // Capture before disabling the original: disabling can cancel provider work and destroy its evidence.
            var state = subject == null ? null : subject.GetComponent<CharacterBodyBuildState>();
            LastFailureDiagnostic = "frame=" + Time.frameCount + "; revision=" + revision
                + "; body=" + (subject == null ? "0" : subject.GetEntityId().ToString())
                + "; bodyActive=" + (subject != null && subject.activeInHierarchy)
                + "; stateRevision=" + (state == null ? "missing" : state.Revision.ToString())
                + "; stateReady=" + (state == null ? "missing" : state.Ready.ToString())
                + "; renderers=" + (subject == null ? 0 : subject.GetComponentsInChildren<Renderer>().Length)
                + "; buildElapsedMs=" + ((Time.realtimeSinceStartupAsDouble - buildStartedAt) * 1000)
                    .ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            ReleaseBody();
            buildRoot = new GameObject("Preview fallback " + revision);
            buildRoot.transform.SetParent(transform, false);
            subject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            subject.transform.SetParent(buildRoot.transform, false);
            subject.transform.localPosition = Vector3.up;
            fallbackStatus = reason + " Showing a fallback; your appearance is retained. Retry preview to try again.";
            CanRetry = true;
            StatusChanged?.Invoke();
            foreach (var collider in subject.GetComponentsInChildren<Collider>()) Destroy(collider);
        }

        private void ReleaseBody()
        {
            renderPending = posePending = publishBuild = false;
            framingRenderers = null;
            framingVertices.Clear();
            if (buildRoot != null) { buildRoot.SetActive(false); DestroyOwned(buildRoot); }
            buildRoot = null;
            subject = null;
        }

        private void OnDisable()
        {
            if (!building && !dirty && !renderPending) return;
            ReleaseBody();
            dirty = building = false;
            requestedKey = null;
        }

        private void OnEnable()
        {
            if (pending != null && subject == null) { requestedKey = null; Show(pending); }
        }

        private void RenderCompleted()
        {
            if (building || dirty || subject == null || cameraRig == null) return;
            if (settleRenders)
            {
                renderPending = posePending = true;
                renderRevision = revision;
                Status = "Updating preview…";
                StatusChanged?.Invoke();
                return;
            }
            subject.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            RenderCurrent();
        }

        private void AdvanceRender()
        {
            if (renderRevision != revision) { renderPending = posePending = publishBuild = false; return; }
            try
            {
                if (subject == null) throw new InvalidOperationException("The preview body was released.");
                if (posePending)
                {
                    // Apply in Update, before the two intervening ordinary animation/render cycles.
                    subject.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
                    posePending = false;
                    renderAtFrame = Time.frameCount + 2;
                    return;
                }
                if (Time.frameCount < renderAtFrame) return;
                if (!RenderCurrent()) throw new InvalidOperationException("The preview has no renderers.");
                if (!renderPending || posePending || renderRevision != revision) return;
                renderPending = false;
                shownKey = requestedKey;
                if (publishBuild) LastBuildMilliseconds = (Time.realtimeSinceStartupAsDouble - requestedAt) * 1000;
                publishBuild = false;
                Status = readyStatus ?? "Preview ready";
                StatusChanged?.Invoke();
            }
            catch (Exception error)
            {
                renderPending = posePending = publishBuild = false;
                CanRetry = true;
                Status = "The preview could not be rendered (" + error.GetType().Name + "). Your look is kept. Retry preview to try again.";
                StatusChanged?.Invoke();
            }
        }

        private bool RenderCurrent()
        {
            var renderers = framingRenderers ?? subject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (fitDeformedBody && !headShot) bounds = DeformedBounds(renderers);
            var animator = subject.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 target = headShot
                ? (head != null ? head.position + Vector3.up * .08f : bounds.center + Vector3.up * bounds.size.y * .32f)
                : bounds.center;
            float height = headShot ? Mathf.Max(.48f, bounds.size.y * .33f) : Mathf.Max(1.4f, bounds.size.y) * 1.13f;
            float distance = height / (2f * Mathf.Tan(cameraRig.fieldOfView * Mathf.Deg2Rad * .5f)) / zoom;
            if (fitDeformedBody && !headShot)
                distance = FittedDistance(bounds, target, distance * zoom) / zoom;
            cameraRig.transform.position = target + new Vector3(0f, headShot ? .015f : .08f, distance);
            cameraRig.transform.LookAt(target);
            cameraRig.Render();
            return true;
        }

        private Bounds DeformedBounds(Renderer[] renderers)
        {
            bool found = false;
            var bounds = new Bounds();
            foreach (var renderer in renderers)
            {
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null && skin.sharedMesh.vertexCount > 0)
                {
                    if (framingMesh == null) framingMesh = new Mesh
                    { name = "Creator framing sample", hideFlags = HideFlags.DontSave };
                    // Bake without transform scale; the full matrix below applies it exactly once,
                    // including a non-uniformly scaled or rotated hierarchy. Never alter culling bounds.
                    skin.BakeMesh(framingMesh, false);
                    framingMesh.GetVertices(framingVertices);
                    var toWorld = skin.transform.localToWorldMatrix;
                    foreach (var vertex in framingVertices)
                    {
                        var world = toWorld.MultiplyPoint3x4(vertex);
                        if (!found) { bounds = new Bounds(world, Vector3.zero); found = true; }
                        else bounds.Encapsulate(world);
                    }
                }
                else
                {
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        private float FittedDistance(Bounds bounds, Vector3 target, float minimum)
        {
            const float padding = 1.13f;
            float vertical = Mathf.Tan(cameraRig.fieldOfView * Mathf.Deg2Rad * .5f) / padding;
            float horizontal = vertical * cameraRig.aspect;
            // A containing sphere supplies a guaranteed upper bracket. The exact box projection
            // then keeps the existing height floor/padding without paying for that sphere's slack.
            float halfAngle = Mathf.Atan(Mathf.Min(vertical, horizontal));
            float maximum = Mathf.Max(minimum, bounds.extents.magnitude / Mathf.Sin(halfAngle));
            if (Fits(minimum)) return minimum;
            for (int i = 0; i < 20; i++)
            {
                float middle = (minimum + maximum) * .5f;
                if (Fits(middle)) maximum = middle;
                else minimum = middle;
            }
            return maximum;

            bool Fits(float distance)
            {
                var position = target + new Vector3(0f, .08f, distance);
                var rotation = Quaternion.Inverse(Quaternion.LookRotation(target - position, Vector3.up));
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var local = rotation * (point - position);
                    if (local.z < cameraRig.nearClipPlane || Mathf.Abs(local.x) > local.z * horizontal
                        || Mathf.Abs(local.y) > local.z * vertical) return false;
                }
                return true;
            }
        }

        private void OnDestroy()
        {
            ReleaseBody();
            if (framingMesh != null) { DestroyOwned(framingMesh); framingMesh = null; }
            if (cameraRig != null) cameraRig.targetTexture = null;
            if (texture != null) { texture.Release(); DestroyOwned(texture); texture = null; }
        }

        /// <summary>Only the revision root and target created by this studio; never a borrowed asset.</summary>
        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (owned == null) return;
            // During Play, preserve deferred cleanup. After Play exits there is no player loop
            // to drain Destroy, so owned transient objects must be released immediately.
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }
    }
}
