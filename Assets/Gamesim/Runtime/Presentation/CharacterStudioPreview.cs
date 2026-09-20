using System;
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
        private double requestedAt;
        private double buildStartedAt;
        public float BuildTimeoutSeconds { get; set; } = 20f;
        public bool CanRetry { get; private set; }
        public Texture Texture => texture;
        public bool IsBuilding => dirty || building;
        public string Status { get; private set; }
        public string CompletedKey => shownKey;
        public double LastBuildMilliseconds { get; private set; }

        public static CharacterStudioPreview Create(string name = "Character studio")
        {
            var root = new GameObject(name) { hideFlags = HideFlags.DontSave };
            root.transform.position = new Vector3(nextStage++ * 20f, -6500f, 0f);
            var preview = root.AddComponent<CharacterStudioPreview>();
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
            AddLight("Key", new Vector3(-2f, 3f, 3f), new Color(1f, .91f, .82f), 7f);
            AddLight("Fill", new Vector3(2f, 1.7f, 2f), new Color(.72f, .83f, 1f), 4f);
            AddLight("Rim", new Vector3(0f, 2f, -2f), new Color(.6f, .87f, 1f), 5f);
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

        public void Show(CharacterAppearance appearance)
        {
            appearance = appearance ?? new CharacterAppearance();
            string key = appearance.ContentKey();
            if (key == requestedKey) return;
            requestedKey = key;
            pending = appearance.Clone();
            revision++;
            requestedAt = Time.realtimeSinceStartupAsDouble;
            dirty = true;
            buildAt = Time.unscaledTime + .16f;
            Status = "Updating preview…";
            CanRetry = false;
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
            shownKey = requestedKey;
            LastBuildMilliseconds = (Time.realtimeSinceStartupAsDouble - requestedAt) * 1000;
            Status = state != null && !string.IsNullOrEmpty(state.Substitution) ? state.Substitution
                : fallbackStatus ?? "Preview ready";
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

        private void UseFallback(string reason)
        {
                ReleaseBody();
                buildRoot = new GameObject("Preview fallback " + revision);
                buildRoot.transform.SetParent(transform, false);
                var template = CastTemplates.Find(pending.presetId);
                string fallback = template == null ? pending.fallbackId
                    : CharacterPresentation.AppearanceId(CastTemplates.ToContestant(template, false), template.Id);
                var prefab = Resources.Load<GameObject>("GamesimCharacters/" + fallback)
                    ?? Resources.Load<GameObject>("GamesimCharacters/player");
                if (prefab != null) subject = Instantiate(prefab, buildRoot.transform, false);
                else
                {
                    subject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    subject.transform.SetParent(buildRoot.transform, false);
                    subject.transform.localPosition = Vector3.up;
                }
                fallbackStatus = reason + " Showing a fallback; your appearance is retained. Retry preview to try again.";
                CanRetry = true;
            foreach (var collider in subject.GetComponentsInChildren<Collider>()) Destroy(collider);
        }

        private void ReleaseBody()
        {
            if (buildRoot != null) { buildRoot.SetActive(false); Destroy(buildRoot); }
            buildRoot = null;
            subject = null;
        }

        private void OnDisable()
        {
            if (!building && !dirty) return;
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
            subject.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            var renderers = subject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var animator = subject.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 target = headShot
                ? (head != null ? head.position + Vector3.up * .08f : bounds.center + Vector3.up * bounds.size.y * .32f)
                : bounds.center;
            float height = headShot ? Mathf.Max(.48f, bounds.size.y * .33f) : Mathf.Max(1.4f, bounds.size.y) * 1.13f;
            float distance = height / (2f * Mathf.Tan(cameraRig.fieldOfView * Mathf.Deg2Rad * .5f)) / zoom;
            cameraRig.transform.position = target + new Vector3(0f, headShot ? .015f : .08f, distance);
            cameraRig.transform.LookAt(target);
            cameraRig.Render();
        }

        private void OnDestroy()
        {
            ReleaseBody();
            if (cameraRig != null) cameraRig.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
        }
    }
}
