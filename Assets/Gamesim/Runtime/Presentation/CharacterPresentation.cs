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

        [SerializeField] private ContestantState definition;
        [SerializeField] private Color wardrobeColor = new Color(0.26f, 0.76f, 0.65f);
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Renderer> replacedRenderers = new List<Renderer>();
        private Material wardrobeMaterial, accentMaterial;
        private bool fixedGoldAccent;
        private Transform visual, chest, head, leftArm, rightArm, leftLeg, rightLeg, leftKnee, rightKnee;
        private Vector3 previousPosition;
        private float walkPhase, movementBlend, phaseOffset, heightScale = 1f;
        private bool reducedMotion, talking, seated, built;

        // Model-backed presentation. When animator is null the primitive rig above is in use.
        private Animator animator;
        private Transform modelHead;
        private Quaternion modelHeadRest;
        public string CharacterId { get; private set; }

        public static CharacterPresentation Attach(GameObject root, ContestantState character, Color palette)
        {
            if (root == null || character == null) return null;
            var component = root.GetComponent<CharacterPresentation>();
            if (component == null) component = root.AddComponent<CharacterPresentation>();
            if (component.built && component.CharacterId != ContentCatalog.CanonicalId(character.id))
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
            return component;
        }

        private void Awake()
        {
            if (!built && definition != null) Build(definition, wardrobeColor);
        }

        public void SetReducedMotion(bool value) => reducedMotion = value;
        public void SetTalking(bool value) => talking = value;
        public void SetSeated(bool value) => seated = value;

        private void Build(ContestantState character, Color palette)
        {
            CharacterId = ContentCatalog.CanonicalId(character.id);
            var appearanceId = AppearanceId(character, CharacterId);
            bool diplomat = appearanceId == "maya-hassan";
            bool athlete = appearanceId == "taylor-kim";
            bool caregiver = appearanceId == "jamie-roberts";
            bool wildcard = appearanceId == "casey-wilson";
            bool analyst = appearanceId == "riley-johnson";
            heightScale = analyst ? 1.05f : athlete ? 1.03f : diplomat ? 1.01f : wildcard ? 0.96f : 1f;
            phaseOffset = diplomat ? 0.4f : athlete ? 1.5f : caregiver ? 2.7f : wildcard ? 3.9f : analyst ? 5.1f : 0f;

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
            return true;
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

            if (animator != null) { AnimateModel(); return; }
            AnimatePrimitives();
        }

        private void AnimateModel()
        {
            animator.SetFloat(SpeedParam, movementBlend);
            animator.SetBool(SeatedParam, seated);

            // The authored clips carry no dialogue pose, so conversation keeps the original
            // head-nod cue, layered over whatever state the Animator is playing.
            if (modelHead == null) return;
            if (talking && !reducedMotion)
            {
                float time = Time.time + phaseOffset;
                modelHead.localRotation = modelHeadRest * Quaternion.Euler(Mathf.Sin(time * 3f) * 4f, Mathf.Sin(time * 1.7f) * 3f, 0f);
            }
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

        private static string AppearanceId(ContestantState character, string canonicalId)
        {
            if (character.isPlayer) return ContentCatalog.PlayerId;
            switch (canonicalId)
            {
                case "maya-hassan": case "taylor-kim": case "jamie-roberts":
                case "casey-wilson": case "riley-johnson": return canonicalId;
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

        private void OnDestroy()
        {
            ReleasePresentation();
        }

        private void ReleasePresentation()
        {
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
            built = false;
            talking = seated = false;
            movementBlend = walkPhase = 0f;
            CharacterId = null;
        }
    }
}
