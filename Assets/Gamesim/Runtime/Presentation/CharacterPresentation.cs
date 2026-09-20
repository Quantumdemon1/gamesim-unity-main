using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Stylized cast presentation. Prefers an authored rigged model from Resources and drives it
    /// through an Animator; falls back to the original articulated primitive recipe when no model
    /// is present, so the project still runs with art stripped out. Only this component's visual
    /// hierarchy moves; navigation stays authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPresentation : MonoBehaviour
    {
        private const string ModelResourceRoot = "GamesimCharacters/";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int SeatedParam = Animator.StringToHash("Seated");
        private static readonly int TalkingParam = Animator.StringToHash("Talking");
        private static readonly int ListeningParam = Animator.StringToHash("Listening");
        private static readonly int ArguingParam = Animator.StringToHash("Arguing");
        /// <summary>
        /// The ceremony beats a body can act out: one-shot clips the controller may declare as
        /// triggers. Appended to, never reordered: <c>AuthoredClipWiring.Reactions</c> and
        /// <see cref="ReactionParams"/> are both indexed by this order.
        /// </summary>
        public enum Reaction { Nominated, Saved, Evicted, Won, Cheered }
        private static readonly int[] ReactionParams =
        {
            Animator.StringToHash("ReactNominated"), Animator.StringToHash("ReactSaved"),
            Animator.StringToHash("ReactEvicted"), Animator.StringToHash("ReactWon"),
            Animator.StringToHash("ReactCheered"),
        };
        private int reactionParams;
        /// <summary>The last beat this body was asked to act out, whether or not it had a clip for it.</summary>
        public Reaction? LastReaction { get; private set; }

        // V6 (VISUAL-TARGET.md): a head that turns to look. The crowd at a ceremony turns to the
        // nominee, the evicted, the winner; a conversation partner is already faced by the whole
        // body, so the head adds nothing there. Applied as a world-space turn about the vertical and
        // the body's right axis on top of whatever the clip posed, so it works on any rig's head bone
        // whatever that bone's local axes are, and on the primitive head the same way.
        private Transform lookTarget;
        private float lookUntil, lookBlend;
        public const float LookYawLimit = 60f;
        public const float LookPitchLimit = 20f;
        /// <summary>What the head is turned toward, or null; for the tests and the director.</summary>
        public Transform LookTarget => lookTarget != null && Time.time < lookUntil ? lookTarget : null;

        /// <summary>Turns the head toward a target for a while. Null, or the time passing, lets it go.</summary>
        public void LookAt(Transform target, float seconds)
        {
            lookTarget = target;
            lookUntil = target != null ? Time.time + Mathf.Max(0f, seconds) : 0f;
        }

        private void ApplyLook(Transform headBone)
        {
            if (headBone == null) return;
            bool looking = lookTarget != null && Time.time < lookUntil && !reducedMotion;
            lookBlend = Mathf.Lerp(lookBlend, looking ? 1f : 0f, 1f - Mathf.Exp(-5f * Time.deltaTime));
            if (lookBlend < 0.002f) { lookBlend = 0f; return; }
            if (lookTarget == null) return;
            var to = lookTarget.position + Vector3.up * 1.5f - headBone.position;
            var flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, Vector3.up), -LookYawLimit, LookYawLimit);
            float pitch = Mathf.Clamp(Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -LookPitchLimit, LookPitchLimit);
            headBone.rotation = Quaternion.AngleAxis(yaw * lookBlend, Vector3.up)
                * Quaternion.AngleAxis(-pitch * lookBlend, transform.right) * headBone.rotation;
        }

        [SerializeField] private ContestantState definition;
        [SerializeField] private Color wardrobeColor = new Color(0.26f, 0.76f, 0.65f);
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Renderer> replacedRenderers = new List<Renderer>();
        private Material wardrobeMaterial, accentMaterial;
        private bool fixedGoldAccent;
        private Transform visual, chest, head, leftArm, rightArm, leftLeg, rightLeg, leftKnee, rightKnee;
        private Vector3 previousPosition;
        private float walkPhase, movementBlend, phaseOffset, heightScale = 1f;
        private bool reducedMotion, talking, speaking = true, seated, arguing, built;
        private float facingYaw = float.NaN;

        // Model-backed presentation. When animator is null the primitive rig above is in use.
        private Animator animator;
        private Transform modelHead;
        private Quaternion modelHeadRest;
        private CharacterBody providedBody;
        private Transform standIn;
        private RuntimeAnimatorController inspectedController;
        private bool hasSpeedParam, hasSeatedParam, hasTalkingParam, hasListeningParam, hasArguingParam;
        public string CharacterId { get; private set; }
        public string AppearanceKey { get; private set; }
        public CharacterAppearance AppearanceSnapshot => definition?.appearance?.Clone();
        /// <summary>The visual hierarchy may fit furniture while navigation continues to own the actor root.</summary>
        public Transform VisualRoot => visual;

        /// <summary>
        /// How many deferred bodies have finished assembling this session. Monotonic; readers keep
        /// their own last-seen value.
        /// </summary>
        public static int BodiesCompleted { get; private set; }
        private static int deferredCloneBuilds;

        /// <summary>
        /// Copies an authored actor's collision/label hierarchy without copying a generated body
        /// or starting a build for the old identity. Attach supplies the new identity afterward.
        /// The source's presentation is detached only during the synchronous Instantiate call;
        /// restoring it in finally preserves its transform and its provider's resource ownership.
        /// </summary>
        public static GameObject CloneUnbound(GameObject template, Transform parent)
        {
            var source = template.GetComponent<CharacterPresentation>();
            if (source == null) return Instantiate(template, parent);
            var detached = new List<(Transform child, Vector3 position, Quaternion rotation, Vector3 scale, int sibling)>();
            for (int i = 0; i < template.transform.childCount; i++)
            {
                var child = template.transform.GetChild(i);
                if (child != source.visual && child.name != "Gamesim Character Visual" && child.name != "Retired Gamesim Character Visual") continue;
                detached.Add((child,child.localPosition,child.localRotation,child.localScale,child.GetSiblingIndex()));
            }
            GameObject inactiveHolder = null;
            if (!template.activeInHierarchy)
            {
                inactiveHolder = new GameObject("Inactive character clone staging");
                inactiveHolder.SetActive(false);
            }
            deferredCloneBuilds++;
            try
            {
                foreach (var item in detached) item.child.SetParent(inactiveHolder != null ? inactiveHolder.transform : null,true);
                var clone = Instantiate(template,parent);
                // Inline Unity serialization can materialize null custom classes. Defer Awake
                // explicitly, then clear the copied identity before an inactive clone is enabled.
                clone.GetComponent<CharacterPresentation>().definition = null;
                return clone;
            }
            finally
            {
                deferredCloneBuilds--;
                foreach (var item in detached)
                {
                    if (item.child == null) continue;
                    item.child.SetParent(template.transform,false);
                    item.child.SetLocalPositionAndRotation(item.position,item.rotation);
                    item.child.localScale = item.scale;
                    item.child.SetSiblingIndex(item.sibling);
                }
                if (inactiveHolder != null) Destroy(inactiveHolder);
            }
        }

        public static CharacterPresentation Attach(GameObject root, ContestantState character, Color palette)
        {
            if (root == null || character == null) return null;
            var component = root.GetComponent<CharacterPresentation>();
            if (component == null) component = root.AddComponent<CharacterPresentation>();
            if (component.built && (component.CharacterId != ContentCatalog.CanonicalId(character.id)
                || component.AppearanceKey != AppearanceKeyFor(character)))
                component.ReleasePresentation();
            if (!component.built)
            {
                component.definition = character.Clone();
                component.wardrobeColor = palette;
                if (Application.isPlaying) component.Build(component.definition, palette);
            }
            else if (component.wardrobeColor != palette)
            {
                component.wardrobeColor = palette;
                component.ApplyWardrobe(palette);
            }
            component.enabled = true;
            // The face wears the simulation's own words, every time the director attaches.
            component.SetMood(character.mood, character.stressLevel);
            return component;
        }

        private FaceExpression face;
        private string mood = "Neutral", stress = "Normal";

        /// <summary>
        /// The houseguest's mood and stress level, for the eyes (MASTER-PLAN §3.B faces). Held
        /// until a body with a face exists, then pushed; a deferred body picks it up when it lands.
        /// </summary>
        public void SetMood(string moodWord, string stressWord)
        {
            mood = moodWord ?? "Neutral";
            stress = stressWord ?? "Normal";
            PushMood();
        }

        /// <summary>The face on this body, or null while there is none.</summary>
        public FaceExpression Face => face;

        /// <summary>
        /// The two words the face is wearing, and whether it is allowed to move.
        ///
        /// <para>The hook a body that grows its own face needs. <see cref="FaceExpression"/> is
        /// pushed to because it is a component this one attaches; a UMA body's expression player is
        /// not — it belongs to the body, is built by UMA, and lives in an assembly this one knows
        /// nothing about — so it reads the same three values instead. Nothing here is new state.</para>
        /// </summary>
        public string Mood => mood;
        public string Stress => stress;
        public bool ReducedMotion => reducedMotion;

        private void PushMood()
        {
            if (face == null && providedBody.Exists && standIn == null
                && providedBody.Root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                face = FaceExpression.Attach(providedBody.Root);
            if (face == null) return;
            face.ReducedMotion = reducedMotion;
            face.SetMood(mood, stress);
        }

        private void Awake()
        {
            if (!built && definition != null && deferredCloneBuilds == 0) Build(definition, wardrobeColor);
        }

        public void SetReducedMotion(bool value) { reducedMotion = value; if (face != null) face.ReducedMotion = value; }
        public void SetTalking(bool value) => talking = value;
        /// <summary>
        /// Within a conversation, whether this body has the floor. The director alternates it
        /// between the two; the one without it listens. Defaults to true so a body told only that
        /// it is talking (the player's conversation) talks.
        /// </summary>
        public void SetSpeaking(bool value) => speaking = value;
        public bool IsSpeaking => talking && speaking;
        public void SetSeated(bool value) => seated = value;
        /// <summary>
        /// Whether this body is in a tense conversation rather than an ordinary one. The director
        /// sets it for the pairs whose topic the caption calls a tense conversation; the controller
        /// answers with the emphatic standing loop where it declares the parameter.
        /// </summary>
        public void SetArguing(bool value) => arguing = value;
        /// <summary>
        /// What the body is actually doing, which is what the animator is told: an argument the
        /// director set, unless motion is reduced, which switches it off exactly as it does the
        /// talk loops. Arm-waving is the same kind of motion at a larger size.
        /// </summary>
        public bool IsArguing => arguing && !reducedMotion;
        /// <summary>
        /// The heading (yaw, degrees) to settle on once stopped, or NaN to leave the heading to
        /// whoever moves the body. A conversation sets it: into the chair, or toward the other speaker.
        /// </summary>
        public void SetFacing(float yaw) => facingYaw = yaw;
        public bool IsTalking => talking;

        /// <summary>
        /// Acts out a ceremony beat. Recorded always; played only when the controller declares the
        /// trigger, the body is standing, and motion is not reduced - a reaction is the largest
        /// motion a body makes, and the seated clips have no reaction to cut to.
        /// </summary>
        public void React(Reaction kind)
        {
            LastReaction = kind;
            if (animator == null || reducedMotion || seated) return;
            RefreshAnimatorParameters();
            if ((reactionParams & (1 << (int)kind)) != 0) animator.SetTrigger(ReactionParams[(int)kind]);
        }
        public bool IsSeated => seated;
        public float FacingYaw => facingYaw;

        private void Build(ContestantState character, Color palette)
        {
            CharacterId = ContentCatalog.CanonicalId(character.id);
            AppearanceKey = AppearanceKeyFor(character);
            var appearanceId = AppearanceId(character, CharacterId);
            bool diplomat = appearanceId == "maya-hassan";
            bool athlete = appearanceId == "taylor-kim";
            bool caregiver = appearanceId == "jamie-roberts";
            bool wildcard = appearanceId == "casey-wilson";
            bool analyst = appearanceId == "riley-johnson";
            heightScale = analyst ? 1.05f : athlete ? 1.03f : diplomat ? 1.01f : wildcard ? 0.96f : 1f;
            if (character.appearance != null) heightScale = 1f;
            phaseOffset = diplomat ? 0.4f : athlete ? 1.5f : caregiver ? 2.7f : wildcard ? 3.9f : analyst ? 5.1f : 0f;
            // V6: sixteen people are not five. Each body idles, nods and sways on its own phase, from
            // its id, so a room of houseguests never breathes in unison.
            phaseOffset += (Mathf.Abs(CharacterId.GetHashCode()) % 628) / 100f;

            // Tests and markers key off this exact name, whichever body is built underneath it.
            visual = Joint("Gamesim Character Visual", transform, Vector3.zero);
            visual.localScale = Vector3.one * heightScale;

            if (!TryBuildModel(appearanceId, palette))
                BuildPrimitiveBody(appearanceId, palette, diplomat, athlete, caregiver, wildcard, analyst);

            // U02's body/head are replaceable visual placeholders. Keep every collider and marker.
            foreach (string oldName in new[] { "Body", "Head" })
            {
                var old = transform.Find(oldName);
                var renderer = old != null ? old.GetComponent<Renderer>() : null;
                if (renderer != null && renderer.enabled)
                {
                    replacedRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }
            previousPosition = transform.position;
            built = true;
        }

        private bool TryBuildModel(string appearanceId, Color palette)
        {
            if (TryBuildProvidedBody(appearanceId, palette)) return true;

            var prefab = Resources.Load<GameObject>(ModelResourceRoot + appearanceId);
            if (prefab == null) return false;

            var instance = Instantiate(prefab, visual, false);
            instance.name = "Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;

            // Authored rigs carry their own colliders for nothing; navigation owns collision here.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }

            modelHead = FindBone(instance.transform, "Head");
            if (modelHead != null) modelHeadRest = modelHead.localRotation;

            ApplyWardrobe(palette);
            face = FaceExpression.Attach(instance);
            return true;
        }

        /// <summary>
        /// Asks the registered body provider — UMA, when a scene opts into it — before falling back
        /// to an authored prefab. Editor-time builds are skipped because a generated body has no
        /// business being written into a scene file.
        /// </summary>
        private bool TryBuildProvidedBody(string appearanceId, Color palette)
        {
            var provider = CharacterBodySource.Provider;
            if (provider == null || !Application.isPlaying) return false;
            if (!CharacterBodySource.TryCreate(new CharacterBodyRequest(CharacterId, appearanceId,
                    definition?.appearance), visual, palette, out var created) || !created.Exists)
                return false;

            providedBody = created;
            animator = created.Animator;
            if (animator != null) animator.applyRootMotion = false;

            // A deferred body has no skeleton yet; LateUpdate picks the head up once it exists.
            if (!created.Deferred) ResolveModelHead();
            else BuildStandIn(appearanceId);
            return true;
        }

        /// <summary>
        /// Puts an authored body in place for the half-second a provider takes to assemble a real
        /// one, so the houseguest is never simply absent. Without this a UMA cast pops in after the
        /// scene is already running, and anything that reasonably expects a houseguest to have a
        /// body — including the episode's own smoke test — is briefly right to complain.
        ///
        /// The stand-in is the same prefab the non-provider path would have used, posed and still.
        /// It is not animated, because it is on screen for less time than a stride.
        /// </summary>
        private void BuildStandIn(string appearanceId)
        {
            var prefab = Resources.Load<GameObject>(ModelResourceRoot + appearanceId);
            if (prefab == null) return;

            var instance = Instantiate(prefab, visual, false);
            instance.name = "Stand-in";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
            standIn = instance.transform;
        }

        /// <summary>Retires the stand-in the first frame the real body has something to draw.</summary>
        private void RetireStandIn()
        {
            if (standIn == null) return;
            if (!providedBody.Exists) return;
            var state = providedBody.Root.GetComponent<CharacterBodyBuildState>();
            if (state != null && !state.Ready) return;
            if (providedBody.Root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) return;

            if (Application.isPlaying) Destroy(standIn.gameObject); else DestroyImmediate(standIn.gameObject);
            standIn = null;
            PushMood();
            // The HUD photographs these bodies for its portraits, and until this moment there was
            // nothing to photograph — so anything already drawn is holding a fallback face. A
            // counter rather than an event: the director polls it, which cannot leave a subscription
            // behind on a scene that has been unloaded.
            BodiesCompleted++;
        }

        /// <summary>
        /// Caches which animation cues the current controller understands. Re-reads only when the
        /// controller itself changes, which a deferred body does once as UMA finishes assembling it.
        /// </summary>
        private void RefreshAnimatorParameters()
        {
            var controller = animator.runtimeAnimatorController;
            if (ReferenceEquals(controller, inspectedController)) return;
            if (controller == null)
            {
                inspectedController = null;
                hasSpeedParam = hasSeatedParam = hasTalkingParam = hasListeningParam = hasArguingParam = false;
                return;
            }

            // An Animator reports no parameters until it has initialised, which on a deferred body
            // is a frame or two after the controller lands. Leaving the cache unset retries next
            // frame rather than concluding the controller understands nothing.
            if (animator.parameterCount == 0) return;

            inspectedController = controller;
            hasSpeedParam = hasSeatedParam = hasTalkingParam = hasListeningParam = hasArguingParam = false;
            reactionParams = 0;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == SpeedParam && parameter.type == AnimatorControllerParameterType.Float)
                    hasSpeedParam = true;
                else if (parameter.nameHash == SeatedParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasSeatedParam = true;
                else if (parameter.nameHash == TalkingParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasTalkingParam = true;
                else if (parameter.nameHash == ListeningParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasListeningParam = true;
                else if (parameter.nameHash == ArguingParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasArguingParam = true;
                else if (parameter.type == AnimatorControllerParameterType.Trigger)
                    for (int i = 0; i < ReactionParams.Length; i++)
                        if (parameter.nameHash == ReactionParams[i]) reactionParams |= 1 << i;
            }
        }

        /// <summary>
        /// Finds the head so conversation can nod it. Prefers the humanoid rig mapping, which is
        /// O(1) and unambiguous, and only walks the hierarchy by name when there is no valid avatar.
        /// </summary>
        private void ResolveModelHead()
        {
            if (modelHead != null) return;

            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
                modelHead = animator.GetBoneTransform(HumanBodyBones.Head);

            if (modelHead == null)
            {
                var root = providedBody.Exists ? providedBody.Root.transform : null;
                if (root != null) modelHead = FindBone(root, "Head");
            }

            if (modelHead != null) modelHeadRest = modelHead.localRotation;
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;
            return null;
        }

        /// <summary>
        /// Tints only the wardrobe slot, leaving the shared skin/hair materials untouched. A property
        /// block keeps the imported materials shared instead of cloning one set per houseguest.
        /// </summary>
        private void ApplyWardrobe(Color palette)
        {
            if (providedBody.Exists)
            {
                (providedBody.Owner ?? CharacterBodySource.Provider)?.SetWardrobeColor(providedBody, palette);
                return;
            }

            if (animator == null)
            {
                if (wardrobeMaterial != null) wardrobeMaterial.color = palette;
                if (accentMaterial != null && !fixedGoldAccent)
                    accentMaterial.color = Color.Lerp(palette, Color.white, 0.55f);
                return;
            }

            var block = new MaterialPropertyBlock();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null || !slots[i].name.Contains("Shirt")) continue;
                    renderer.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, palette);
                    block.SetColor(LegacyColorId, palette);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        private void BuildPrimitiveBody(string appearanceId, Color palette,
            bool diplomat, bool athlete, bool caregiver, bool wildcard, bool analyst)
        {
            var skinColor = diplomat ? Hex("C4956A") : athlete ? Hex("E8C4A0") :
                caregiver ? Hex("E8C4A0") : wildcard ? Hex("FFD9B3") : analyst ? Hex("FFECD2") : Hex("B07A52");
            var skin = MakeMaterial("Skin", skinColor);
            var fabric = MakeMaterial("Wardrobe", palette);
            var dark = MakeMaterial("Ink tailoring", Hex("1F2937"));
            var linen = MakeMaterial("Linen", Hex("E8E4DC"));
            var hair = MakeMaterial("Hair", wildcard ? Hex("8A6239") : caregiver ? Hex("5D4226") : Hex("211916"));
            var eyes = MakeMaterial("Eyes", Hex("25252B"));
            var accent = MakeMaterial("Accent", diplomat ? Hex("CAA24A") : Color.Lerp(palette, Color.white, 0.55f));
            wardrobeMaterial = fabric;
            accentMaterial = accent;
            fixedGoldAccent = diplomat;

            float shoulderWidth = athlete ? 0.29f : analyst ? 0.24f : 0.255f;
            float bodyWidth = athlete ? 0.52f : analyst ? 0.42f : 0.46f;
            Part("Hips", visual, PrimitiveType.Sphere, new Vector3(0, 0.89f, 0), new Vector3(bodyWidth * 0.91f, 0.23f, 0.28f), dark);
            chest = Joint("Chest", visual, new Vector3(0, 1.02f, 0));
            Part("Torso", chest, PrimitiveType.Capsule, new Vector3(0, 0.20f, 0), new Vector3(bodyWidth, 0.26f, 0.28f), fabric);
            Part("Neck", chest, PrimitiveType.Cylinder, new Vector3(0, 0.48f, 0), new Vector3(0.13f, 0.06f, 0.13f), skin);

            head = Joint("Head pivot", chest, new Vector3(0, 0.68f, 0));
            Part("Face", head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, 0.41f, 0.34f), skin);
            Part("Hair crown", head, PrimitiveType.Sphere, new Vector3(0, 0.145f, -0.022f), new Vector3(0.37f, 0.17f, 0.35f), hair);
            Part("Nose", head, PrimitiveType.Sphere, new Vector3(0, -0.012f, 0.171f), new Vector3(0.055f, 0.074f, 0.062f), skin);
            foreach (float side in new[] { -1f, 1f })
            {
                Part("Eye white", head, PrimitiveType.Sphere, new Vector3(side * 0.071f, 0.042f, 0.150f), new Vector3(0.065f, 0.043f, 0.025f), linen);
                Part("Eye", head, PrimitiveType.Sphere, new Vector3(side * 0.071f, 0.042f, 0.165f), new Vector3(0.025f, 0.031f, 0.017f), eyes);
                Part("Brow", head, PrimitiveType.Cube, new Vector3(side * 0.071f, 0.079f, 0.154f), new Vector3(0.069f, 0.014f, 0.016f), hair);
                Part("Ear", head, PrimitiveType.Sphere, new Vector3(side * 0.172f, 0, 0), new Vector3(0.058f, 0.104f, 0.057f), skin);
            }
            Part("Mouth", head, PrimitiveType.Cube, new Vector3(0, -0.094f, 0.151f), new Vector3(0.076f, 0.014f, 0.019f), MakeMaterial("Lip", Color.Lerp(skinColor, Hex("813F3F"), 0.45f)));

            if (diplomat)
            {
                Part("Bun", head, PrimitiveType.Sphere, new Vector3(0, 0.105f, -0.19f), new Vector3(0.23f, 0.24f, 0.23f), hair);
                Part("Blouse", chest, PrimitiveType.Cube, new Vector3(0, 0.19f, 0.143f), new Vector3(0.12f, 0.39f, 0.018f), linen);
                Part("Gold pendant", chest, PrimitiveType.Sphere, new Vector3(0, 0.35f, 0.165f), new Vector3(0.037f, 0.037f, 0.025f), accent);
                var leftLapel = Part("Left lapel", chest, PrimitiveType.Cube, new Vector3(-0.09f, 0.28f, 0.157f), new Vector3(0.07f, 0.28f, 0.025f), dark);
                leftLapel.localRotation = Quaternion.Euler(0, 0, 18);
                var rightLapel = Part("Right lapel", chest, PrimitiveType.Cube, new Vector3(0.09f, 0.28f, 0.157f), new Vector3(0.07f, 0.28f, 0.025f), dark);
                rightLapel.localRotation = Quaternion.Euler(0, 0, -18);
            }
            else if (athlete)
            {
                Part("Ponytail", head, PrimitiveType.Capsule, new Vector3(0, -0.02f, -0.215f), new Vector3(0.13f, 0.20f, 0.15f), hair);
                Part("Sports stripe", chest, PrimitiveType.Cube, new Vector3(0, 0.20f, 0.151f), new Vector3(0.065f, 0.35f, 0.017f), accent);
                Part("Headband", head, PrimitiveType.Cube, new Vector3(0, 0.112f, 0.135f), new Vector3(0.28f, 0.035f, 0.028f), accent);
            }
            else if (caregiver)
            {
                foreach (float side in new[] { -1f, 1f })
                    Part("Bob side", head, PrimitiveType.Capsule, new Vector3(side * 0.158f, 0.014f, -0.05f), new Vector3(0.115f, 0.14f, 0.25f), hair);
                Part("Cardigan opening", chest, PrimitiveType.Cube, new Vector3(0, 0.20f, 0.144f), new Vector3(0.13f, 0.39f, 0.020f), linen);
                Part("Pocket", chest, PrimitiveType.Cube, new Vector3(-0.14f, 0.15f, 0.14f), new Vector3(0.1f, 0.11f, 0.024f), accent);
            }
            else if (wildcard)
            {
                for (int i = 0; i < 7; i++)
                {
                    float angle = i * Mathf.PI * 2f / 7f;
                    Part("Curl", head, PrimitiveType.Sphere, new Vector3(Mathf.Sin(angle) * 0.14f, 0.13f, Mathf.Cos(angle) * 0.12f - 0.025f), Vector3.one * 0.16f, hair);
                }
                Part("Shirt placket", chest, PrimitiveType.Cube, new Vector3(0, 0.20f, 0.149f), new Vector3(0.026f, 0.37f, 0.018f), accent);
                for (int i = 0; i < 3; i++)
                    Part("Shirt button", chest, PrimitiveType.Sphere, new Vector3(0, 0.11f + i * 0.1f, 0.168f), Vector3.one * 0.023f, linen);
            }
            else if (analyst)
            {
                Part("Hood", chest, PrimitiveType.Sphere, new Vector3(0, 0.38f, -0.1f), new Vector3(0.36f, 0.22f, 0.23f), fabric);
                Part("Hoodie pocket", chest, PrimitiveType.Cube, new Vector3(0, 0.10f, 0.149f), new Vector3(0.23f, 0.11f, 0.022f), accent);
                foreach (float side in new[] { -1f, 1f })
                {
                    Part("Glasses top", head, PrimitiveType.Cube, new Vector3(side * 0.076f, 0.069f, 0.181f), new Vector3(0.11f, 0.014f, 0.018f), dark);
                    Part("Glasses bottom", head, PrimitiveType.Cube, new Vector3(side * 0.076f, 0.01f, 0.181f), new Vector3(0.11f, 0.014f, 0.018f), dark);
                    Part("Glasses outside", head, PrimitiveType.Cube, new Vector3(side * 0.13f, 0.04f, 0.175f), new Vector3(0.012f, 0.065f, 0.018f), dark);
                }
                Part("Glasses bridge", head, PrimitiveType.Cube, new Vector3(0, 0.049f, 0.182f), new Vector3(0.048f, 0.014f, 0.018f), dark);
            }
            else
            {
                Part("Swept hair", head, PrimitiveType.Sphere, new Vector3(-0.06f, 0.175f, 0.04f), new Vector3(0.26f, 0.10f, 0.25f), hair);
                Part("Player badge", chest, PrimitiveType.Cube, new Vector3(-0.13f, 0.29f, 0.15f), new Vector3(0.065f, 0.075f, 0.023f), linen);
            }

            leftArm = Arm("Left arm", -1, shoulderWidth, chest, skin, athlete ? skin : fabric);
            rightArm = Arm("Right arm", 1, shoulderWidth, chest, skin, athlete ? skin : fabric);
            leftLeg = Leg("Left leg", -0.115f, visual, dark, linen, out leftKnee);
            rightLeg = Leg("Right leg", 0.115f, visual, dark, linen, out rightKnee);
        }

        private void LateUpdate()
        {
            if (!built || visual == null) return;
            var delta = transform.position - previousPosition;
            previousPosition = transform.position;
            delta.y = 0;
            float speed = Time.deltaTime > 0.001f ? delta.magnitude / Time.deltaTime : 0f;
            // Teleports reposition the actor without producing a false running animation.
            float target = speed > 8f || seated ? 0f : Mathf.Clamp01(speed / 2.5f);
            movementBlend = Mathf.Lerp(movementBlend, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (!float.IsNaN(facingYaw) && movementBlend < 0.02f) SettleFacing();

            if (providedBody.Exists) { AnimateProvidedBody(); return; }
            if (animator != null) { AnimateModel(); return; }
            AnimatePrimitives();
        }

        /// <summary>
        /// Drives a body a provider built. Kept separate from the authored-prefab path for one
        /// specific reason: UMA replaces the avatar's Animator while it assembles the character, so
        /// the reference captured when the body was handed over can be destroyed out from under us.
        /// Falling through to <see cref="AnimatePrimitives"/> on a null animator would then drive a
        /// primitive rig that was never built for this houseguest, which is a null reference every
        /// frame rather than a missing animation.
        /// </summary>
        private void AnimateProvidedBody()
        {
            RetireStandIn();
            if (animator == null)
            {
                animator = providedBody.Root.GetComponentInChildren<Animator>(true);
                if (animator == null) return;
                animator.applyRootMotion = false;
                inspectedController = null;
            }
            AnimateModel();
        }

        private void AnimateModel()
        {
            // Not every rig's controller carries both cues — UMA's shipped locomotion has Speed but
            // no Seated — and driving a parameter a controller does not declare warns once per call.
            RefreshAnimatorParameters();
            if (hasSpeedParam) animator.SetFloat(SpeedParam, movementBlend);
            if (hasSeatedParam) animator.SetBool(SeatedParam, seated);
            // Reduced motion keeps the authored talk loops off: the head-nod cue below is already
            // gated on it, and a gesturing body is the same kind of motion at a larger size.
            if (hasTalkingParam) animator.SetBool(TalkingParam, talking && speaking && !reducedMotion);
            if (hasListeningParam) animator.SetBool(ListeningParam, talking && !speaking && !reducedMotion);
            // The tense loop is the talk loop with the arms working, so it follows the same rule.
            if (hasArguingParam) animator.SetBool(ArguingParam, IsArguing);

            // A provided body streams its rig in over a few frames. Retry on a slow cadence so the
            // hierarchy walk behind ResolveModelHead cannot become a per-frame cost on a body that
            // genuinely has no head bone.
            if (modelHead == null && providedBody.Exists && (Time.frameCount & 7) == 0)
                ResolveModelHead();

            // The authored clips carry no dialogue pose, so conversation keeps the original
            // head-nod cue, layered over whatever state the Animator is playing.
            if (modelHead == null) return;
            if (talking && !reducedMotion)
            {
                float time = Time.time + phaseOffset;
                modelHead.localRotation = modelHeadRest * Quaternion.Euler(Mathf.Sin(time * 3f) * 4f, Mathf.Sin(time * 1.7f) * 3f, 0f);
            }
            ApplyLook(modelHead);
        }

        /// <summary>
        /// Turns a stopped body to its conversation heading. Only the root turns, and only while
        /// nothing is moving it: a navigating agent owns the heading until it arrives, and the
        /// movement blend is what says it has.
        /// </summary>
        private void SettleFacing()
        {
            var wanted = Quaternion.Euler(0f, facingYaw, 0f);
            transform.rotation = reducedMotion ? wanted
                : Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private void AnimatePrimitives()
        {
            walkPhase += Time.deltaTime * Mathf.Lerp(3f, 10f, movementBlend);
            float time = Time.time + phaseOffset;
            float swing = Mathf.Sin(walkPhase) * 27f * movementBlend;
            float gesture = talking && !reducedMotion && !seated ? Mathf.Sin(time * 2.8f) * 9f - 13f : 0f;
            float idle = reducedMotion || seated ? 0f : Mathf.Sin(time * 1.6f) * 0.004f * (1f - movementBlend);
            visual.localPosition = new Vector3(0, seated ? -0.32f : idle, 0);
            chest.localRotation = Quaternion.Euler(0, 0, reducedMotion || seated ? 0f : Mathf.Sin(time * 0.9f) * 0.65f);
            head.localRotation = Quaternion.Euler(talking && !reducedMotion ? Mathf.Sin(time * 3f) * 2f : 0f, 0f, 0f);
            ApplyLook(head);
            leftArm.localRotation = Quaternion.Euler(-swing + gesture, 0f, seated ? 0f : 7f);
            rightArm.localRotation = Quaternion.Euler(swing + gesture * 0.35f, 0f, seated ? 0f : -7f);
            leftLeg.localRotation = Quaternion.Euler(seated ? -82f : swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(seated ? -82f : -swing, 0f, 0f);
            leftKnee.localRotation = Quaternion.Euler(seated ? 82f : Mathf.Max(0f, -swing) * 0.8f, 0f, 0f);
            rightKnee.localRotation = Quaternion.Euler(seated ? 82f : Mathf.Max(0f, swing) * 0.8f, 0f, 0f);
        }

        private Transform Arm(string name, float side, float width, Transform parent, Material skin, Material sleeve)
        {
            var joint = Joint(name, parent, new Vector3(side * width, 0.34f, 0));
            Part("Upper arm", joint, PrimitiveType.Capsule, new Vector3(0, -0.15f, 0), new Vector3(0.13f, 0.15f, 0.13f), sleeve);
            Part("Forearm", joint, PrimitiveType.Capsule, new Vector3(0, -0.39f, 0.018f), new Vector3(0.10f, 0.12f, 0.10f), skin);
            Part("Hand", joint, PrimitiveType.Sphere, new Vector3(0, -0.535f, 0.022f), new Vector3(0.095f, 0.13f, 0.075f), skin);
            return joint;
        }

        private Transform Leg(string name, float x, Transform parent, Material pants, Material shoes, out Transform knee)
        {
            var joint = Joint(name, parent, new Vector3(x, 0.88f, 0));
            Part("Thigh", joint, PrimitiveType.Capsule, new Vector3(0, -0.18f, 0), new Vector3(0.17f, 0.19f, 0.18f), pants);
            knee = Joint("Knee", joint, new Vector3(0, -0.36f, 0));
            Part("Shin", knee, PrimitiveType.Capsule, new Vector3(0, -0.20f, 0), new Vector3(0.13f, 0.20f, 0.14f), pants);
            Part("Shoe", knee, PrimitiveType.Cube, new Vector3(0, -0.44f, 0.055f), new Vector3(0.16f, 0.12f, 0.28f), shoes);
            return joint;
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            return joint;
        }

        private static Transform Part(string name, Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Gamesim " + name, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
            material.enableInstancing = true;
            materials.Add(material);
            return material;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        /// <summary>
        /// Public so portraits resolve a houseguest to the same persona the in-world model uses;
        /// a portrait that disagreed with the character standing in the room would be worse than none.
        /// </summary>
        public static string AppearanceId(ContestantState character, string canonicalId)
        {
            string templateId = character.appearance?.presetId ?? character.sourceTemplateId;
            if (!string.IsNullOrEmpty(templateId) && templateId != ContentCatalog.PlayerId)
            {
                var template = CastTemplates.Find(templateId);
                if (template != null)
                {
                    var source = CastTemplates.ToContestant(template, false);
                    return LegacyAppearanceId(source, template.Id);
                }
            }
            if (!string.IsNullOrEmpty(character.appearance?.fallbackId)) return character.appearance.fallbackId;
            return LegacyAppearanceId(character, canonicalId);
        }

        private static string LegacyAppearanceId(ContestantState character, string canonicalId)
        {
            if (character.isPlayer) return ContentCatalog.PlayerId;
            switch (canonicalId)
            {
                case "maya-hassan": case "taylor-kim": case "jamie-roberts":
                case "casey-wilson": case "riley-johnson":
                // The All-Stars roster's authored body (ArtSource/characters/bb_char_dan_gheesling.py).
                case "dan-gheesling": return canonicalId;
            }
            // Imported identities retain their real IDs. Select a native visual recipe from traits,
            // with a deterministic ID-only variation; do not infer identity from a displayed name.
            if (character.traits.Contains("Competitive") || character.traits.Contains("Confrontational")) return "taylor-kim";
            if (character.traits.Contains("Emotional") || character.traits.Contains("Loyal")) return "jamie-roberts";
            if (character.traits.Contains("Analytical") || character.traits.Contains("Introverted")) return "riley-johnson";
            uint hash = 2166136261;
            foreach (char value in canonicalId ?? string.Empty) hash = (hash ^ value) * 16777619;
            return (hash & 1) == 0 ? "maya-hassan" : "casey-wilson";
        }

        public static string AppearanceKeyFor(ContestantState character) => character?.appearance?.ContentKey()
            ?? "legacy:" + (character?.sourceTemplateId ?? character?.id ?? ContentCatalog.PlayerId);

        private void OnDestroy()
        {
            ReleasePresentation();
        }

        private void ReleasePresentation()
        {
            face = null;
            foreach (var old in replacedRenderers) if (old != null) old.enabled = true;
            replacedRenderers.Clear();
            if (visual != null)
            {
                visual.name = "Retired Gamesim Character Visual";
                visual.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(visual.gameObject); else DestroyImmediate(visual.gameObject);
            }
            foreach (var material in materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            }
            materials.Clear();
            wardrobeMaterial = null;
            accentMaterial = null;
            visual = null;
            chest = head = leftArm = rightArm = leftLeg = rightLeg = leftKnee = rightKnee = null;
            animator = null;
            modelHead = null;
            providedBody = default;
            standIn = null; // destroyed with the visual root above
            inspectedController = null;
            hasSpeedParam = hasSeatedParam = hasTalkingParam = hasListeningParam = hasArguingParam = false;
            built = false;
            talking = seated = arguing = false; facingYaw = float.NaN; lookTarget = null; lookBlend = 0f;
            movementBlend = walkPhase = 0f;
            CharacterId = null;
        }
    }
}
