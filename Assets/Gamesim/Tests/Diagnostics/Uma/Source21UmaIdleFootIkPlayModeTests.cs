#if UNITY_EDITOR && GAMESIM_UMA
namespace Gamesim.Uma.Tests.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using Gamesim.Presentation;
    using Gamesim.Simulation;
    using Gamesim.Tests.Diagnostics;
    using Gamesim.Uma;
    using NUnit.Framework;
    using UMA;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Source21's test-private, pre-construction comparison. It changes only the copied Idle state's
    /// Foot IK flag and deliberately retains the Source20 far-park/AlwaysAnimate fixture. The copy is
    /// built in memory (DiagnosticIdleFootIkController): no run writes a controller under Assets/, and no
    /// SHA, state count or IK-pass setting of the shipping controller is pinned. Moved from
    /// Gamesim.Uma.Tests to this GAMESIM_DIAGNOSTICS assembly. Its hypothesis is refuted, so its test
    /// asserts the drift it measures (see the test).
    /// </summary>
    public sealed class Source21UmaIdleFootIkPlayModeTests
    {
        private const string SourceControllerPath = "Assets/Gamesim/Art/Characters/GamesimHumanoid.controller";
        private const string HandlePath = "Assets/Gamesim/Resources/Animation/GamesimHumanoid.overrideController";
        // Where the copy used to be written; asserted absent, so a regression back to disk is caught.
        private const string FormerCandidatePath = "Assets/Gamesim/Uma/Tests/__Source21NormalIdleFootIkUma.controller";
        private const int MappingCap = 128;
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<RuntimeAnimatorController> ownedRuntimeControllers = new List<RuntimeAnimatorController>(4);
        private readonly Source21UmaReadyBody[] ready = new Source21UmaReadyBody[2];
        private GameObject cast, first, second, ground;
        private Material groundMaterial;
        private CharacterPresentation a, b;
        private GamesimUmaCast castComponent;
        private UmaBodyProvider provider;
        private DiagnosticIdleFootIkController controllerCopy;
        private AnimatorController source, candidate;
        private AnimatorOverrideController candidateRuntime;
        private AnimationClip expectedIdle, expectedRun;
        private string sourceHash, handleHash;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            PrepareCandidate();

            var indexer = UMAAssetIndexer.Instance;
            if (indexer == null || indexer.Generator == null)
                Assert.Fail("SOURCE21_HOLD_UMA_DEPENDENCY: real UMA indexer/generator unavailable");

            cast = new GameObject("Source21 Foot IK UMA cast", typeof(GamesimUmaCast));
            castComponent = cast.GetComponent<GamesimUmaCast>();
            yield return null;

            var providerField = typeof(GamesimUmaCast).GetField("provider", PrivateInstance);
            var resolve = typeof(UmaBodyProvider).GetMethod("ResolveController", PrivateInstance);
            var castField = typeof(UmaBodyProvider).GetField("cast", PrivateInstance);
            var resolvedField = typeof(UmaBodyProvider).GetField("castResolved", PrivateInstance);
            Assert.That(providerField, Is.Not.Null, "SOURCE21_UMA_PROVIDER_FIELD");
            Assert.That(resolve, Is.Not.Null, "SOURCE21_UMA_RESOLVE_METHOD");
            Assert.That(castField, Is.Not.Null, "SOURCE21_UMA_CAST_FIELD");
            Assert.That(resolvedField, Is.Not.Null, "SOURCE21_UMA_RESOLVED_FIELD");

            provider = providerField.GetValue(castComponent) as UmaBodyProvider;
            if (provider == null)
                Assert.Fail("SOURCE21_HOLD_UMA_DEPENDENCY: GamesimUmaCast did not register its real provider");

            var resolved = resolve.Invoke(provider, new object[] { indexer }) as RuntimeAnimatorController;
            var production = resolved as AnimatorOverrideController;
            if (production != null && !EditorUtility.IsPersistent(production))
                ownedRuntimeControllers.Add(production);
            if (production == null || EditorUtility.IsPersistent(production)
                || production.runtimeAnimatorController != source)
                Assert.Fail("SOURCE21_HOLD_UMA_DEPENDENCY: real source-base UMA override controller unavailable");

            candidateRuntime = new AnimatorOverrideController(candidate) { name = "Source21 UMA Idle Foot IK" };
            ownedRuntimeControllers.Add(candidateRuntime);
            CopyAndVerifyOverrides(production, candidateRuntime, out expectedIdle, out expectedRun);

            castField.SetValue(provider, candidateRuntime);
            resolvedField.SetValue(provider, true);
            Assert.That(castField.GetValue(provider), Is.SameAs(candidateRuntime), "SOURCE21_UMA_PRIVATE_CAST");
            Assert.That((bool)resolvedField.GetValue(provider), Is.True, "SOURCE21_UMA_PRIVATE_CAST_RESOLVED");

            first = new GameObject("Source21 Foot IK UMA alex-chen");
            second = new GameObject("Source21 Foot IK UMA emma-brown");
            first.transform.position = new Vector3(2000f, 0f, 2000f);
            second.transform.position = first.transform.position + Vector3.right * 3.34f;
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Source21 Foot IK UMA observed floor";
            ground.transform.position = (first.transform.position + second.transform.position) * .5f - Vector3.up * .005f;
            ground.transform.localScale = new Vector3(7f, .01f, 3f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "SOURCE21_UMA_FLOOR_SHADER");
            groundMaterial = new Material(shader);
            groundMaterial.color = new Color(.25f, .27f, .3f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            a = CharacterPresentation.Attach(first, Person("alex-chen"), Color.white);
            b = CharacterPresentation.Attach(second, Person("emma-brown"), Color.white);
            a.SetReducedMotion(false);
            b.SetReducedMotion(false);

            yield return Source21UmaReadiness.Wait(new[] { a, b }, candidateRuntime, expectedIdle,
                30f, 900, ready);
            Assert.That(a.Frame, Is.EqualTo(BodyFrame.Masculine), "SOURCE21_UMA_ALEX_FRAME");
            Assert.That(b.Frame, Is.EqualTo(BodyFrame.Feminine), "SOURCE21_UMA_EMMA_FRAME");
            Assert.That(HashAsset(SourceControllerPath), Is.EqualTo(sourceHash), "SOURCE21_SOURCE_DIRTIED_AFTER_ATTACH");
            Assert.That(HashAsset(HandlePath), Is.EqualTo(handleHash), "SOURCE21_HANDLE_DIRTIED_AFTER_ATTACH");
        }

        /// <summary>
        /// A refuted probe, pinned to what it measures (the lead's decision D1(b), r1 triage, 2026-10-16).
        ///
        /// <para>The probe asked whether the copied Idle's Foot IK alone keeps the four foot landmarks of
        /// two UMA bodies within 1 mm of one baseline for 2 s. It cannot: UMA's Idle itself moves a foot
        /// 21 mm over a loop (S5's measurement), and the goals report zero position and rotation weight, so
        /// Foot IK pins nothing. Every run on record failed with LANDMARK_DRIFT_ (r1: body 0 landmark 0,
        /// 1.0006 mm at 0.178 s); only which body and landmark crosses first varies with the Idle's phase.
        /// The farewell plants its feet with S5's stance hold instead.</para>
        ///
        /// <para>So this asserts that outcome: a LANDMARK_DRIFT_ failure inside the 2 s window, on the
        /// first landmark past 1 mm, with every foot goal unweighted. Any other code (SAME_FRAME_LATE,
        /// ROOT_DRIFT, a set-up's SOURCE21_ failure) stays red, and so does a window the landmarks hold or
        /// a goal Unity starts to weight.</para>
        /// </summary>
        [UnityTest]
        [Category("FarewellIdleFootIkDiagnostic")]
        [FarewellDiagnosticIkPass]
        public IEnumerator CopiedIdleFootIk_UmaBodiesDriftPastOneMillimetreWithTheFootGoalsUnweighted()
        {
            yield return Source21UmaIdleFootIkObserver.Observe(ready,
                "source21-uma-far-always-animate-idle-footik-only");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Source21UmaIdleFootIkObserver.DisarmAll(ready);
            if (first != null) Object.Destroy(first);
            if (second != null) Object.Destroy(second);
            if (ground != null) Object.Destroy(ground);
            if (groundMaterial != null) Object.Destroy(groundMaterial);
            if (cast != null) Object.Destroy(cast);
            yield return null; // GamesimUmaCast.OnDestroy unregisters its exact provider first.

            foreach (var controller in ownedRuntimeControllers)
                if (controller != null && !EditorUtility.IsPersistent(controller)) Object.Destroy(controller);
            yield return null;

            // The bodies and override that played the copy are gone; only now is the in-memory controller released.
            controllerCopy?.Dispose();
            bool providerReleased = provider == null || CharacterBodySource.Provider != provider;
            bool objectsReleased = first == null && second == null && cast == null && ground == null && groundMaterial == null;
            bool wrappersReleased = ownedRuntimeControllers.All(controller => controller == null);
            bool copyReleased = controllerCopy == null || controllerCopy.AllDestroyed;
            Assert.That(providerReleased, Is.True, "SOURCE21_CLEANUP_PROVIDER_RELEASED");
            Assert.That(objectsReleased, Is.True, "SOURCE21_CLEANUP_OBJECTS_RELEASED");
            Assert.That(wrappersReleased, Is.True, "SOURCE21_CLEANUP_WRAPPERS_RELEASED");
            Assert.That(copyReleased, Is.True, "SOURCE21_CLEANUP_COPY_RELEASED");
            Assert.That(File.Exists(Absolute(FormerCandidatePath)), Is.False, "SOURCE21_NOTHING_WRITTEN_UNDER_ASSETS");
            // Not pins: the test must leave the shipping controller and its handle as it found them.
            if (sourceHash != null) Assert.That(HashAsset(SourceControllerPath), Is.EqualTo(sourceHash), "SOURCE21_SOURCE_HASH_CHANGED");
            if (handleHash != null) Assert.That(HashAsset(HandlePath), Is.EqualTo(handleHash), "SOURCE21_HANDLE_HASH_CHANGED");
        }

        private void PrepareCandidate()
        {
            sourceHash = HashAsset(SourceControllerPath);
            handleHash = HashAsset(HandlePath);

            source = AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath);
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Assert.That(source, Is.Not.Null, "SOURCE21_SOURCE_CONTROLLER");
            Assert.That(handle, Is.Not.Null, "SOURCE21_RESOURCES_HANDLE");
            Assert.That(AssetDatabase.GetAssetPath(handle), Is.EqualTo(HandlePath));
            Assert.That(handle.runtimeAnimatorController, Is.SameAs(source), "SOURCE21_RESOURCES_BASE");

            controllerCopy = DiagnosticIdleFootIkController.Build(source, "Source21 UMA Idle Foot IK (in memory)");
            candidate = controllerCopy.Copy;
            Assert.That(HashAsset(SourceControllerPath), Is.EqualTo(sourceHash), "SOURCE21_SOURCE_DIRTIED");
            Assert.That(HashAsset(HandlePath), Is.EqualTo(handleHash), "SOURCE21_HANDLE_DIRTIED");
        }

        private static void CopyAndVerifyOverrides(AnimatorOverrideController production,
            AnimatorOverrideController copy, out AnimationClip idle, out AnimationClip run)
        {
            idle = run = null;
            Assert.That(production, Is.Not.Null);
            Assert.That(copy, Is.Not.Null);
            Assert.That(production.overridesCount, Is.LessThanOrEqualTo(MappingCap), "SOURCE21_UMA_OVERRIDE_CAP");
            var sourceRows = new List<KeyValuePair<AnimationClip, AnimationClip>>(production.overridesCount);
            production.GetOverrides(sourceRows);
            Assert.That(sourceRows.Count, Is.EqualTo(production.overridesCount), "SOURCE21_UMA_OVERRIDE_COUNT");
            var baseClips = production.runtimeAnimatorController.animationClips;
            Assert.That(baseClips.Length, Is.LessThanOrEqualTo(MappingCap), "SOURCE21_UMA_BASE_CLIP_CAP");
            var baseSet = new HashSet<AnimationClip>(baseClips);
            var seen = new HashSet<AnimationClip>();
            int changed = 0;
            foreach (var row in sourceRows)
            {
                Assert.That(row.Key, Is.Not.Null, "SOURCE21_UMA_OVERRIDE_KEY");
                Assert.That(seen.Add(row.Key), Is.True, "SOURCE21_UMA_OVERRIDE_DUPLICATE");
                Assert.That(baseSet.Contains(row.Key), Is.True, "SOURCE21_UMA_OVERRIDE_FOREIGN_KEY");
                if (row.Value == null || row.Value == row.Key) continue;
                changed++;
                if (row.Key.name == "Idle_Loop") idle = row.Value;
                else if (row.Key.name == "Jog_Fwd_Loop") run = row.Value;
                else Assert.Fail("SOURCE21_UMA_UNEXPECTED_OVERRIDE_" + row.Key.name);
            }
            Assert.That(changed, Is.EqualTo(2), "SOURCE21_UMA_BORROWED_OVERRIDE_COUNT");
            Assert.That(idle != null && run != null && idle != run, Is.True, "SOURCE21_UMA_IDLE_RUN_MAPPING");
            Assert.That(idle.humanMotion && idle.isLooping && Finite(idle.length) && idle.length > 0f, Is.True,
                "SOURCE21_UMA_IDLE_MAPPING_INVALID");
            Assert.That(run.humanMotion && run.isLooping && Finite(run.length) && run.length > 0f, Is.True,
                "SOURCE21_UMA_RUN_MAPPING_INVALID");

            copy.ApplyOverrides(sourceRows);
            Assert.That(copy.runtimeAnimatorController, Is.TypeOf<AnimatorController>());
            Assert.That(copy.overridesCount, Is.EqualTo(sourceRows.Count), "SOURCE21_UMA_COPIED_OVERRIDE_ADMISSION");
            Assert.That(copy.overridesCount, Is.LessThanOrEqualTo(MappingCap), "SOURCE21_UMA_COPIED_OVERRIDE_CAP");
            var copiedRows = new List<KeyValuePair<AnimationClip, AnimationClip>>(copy.overridesCount);
            copy.GetOverrides(copiedRows);
            Assert.That(copiedRows.Count, Is.EqualTo(sourceRows.Count), "SOURCE21_UMA_COPIED_OVERRIDE_COUNT");
            var proof = new BoundedProof();
            proof.Append("SOURCE21-OVERRIDES version=1 fixture=uma count=").Append(sourceRows.Count).Append('\n');
            for (int i = 0; i < sourceRows.Count; i++)
            {
                Assert.That(copiedRows[i].Key, Is.SameAs(sourceRows[i].Key), "SOURCE21_UMA_OVERRIDE_KEY_" + i);
                Assert.That(copiedRows[i].Value, Is.SameAs(sourceRows[i].Value), "SOURCE21_UMA_OVERRIDE_VALUE_" + i);
                var effective = sourceRows[i].Value != null ? sourceRows[i].Value : sourceRows[i].Key;
                var keyName = BoundedProof.Name(sourceRows[i].Key.name, "SOURCE21_UMA_OVERRIDE_KEY_NAME_CAP_" + i);
                var effectiveName = BoundedProof.Name(effective.name, "SOURCE21_UMA_OVERRIDE_EFFECTIVE_NAME_CAP_" + i);
                proof.Append("MAP ordinal=").Append(i).Append(" key=").Append(keyName)
                    .Append('#').Append(sourceRows[i].Key.GetEntityId().ToString()).Append(" effective=")
                    .Append(effectiveName).Append('#').Append(effective.GetEntityId().ToString()).Append('\n');
            }
            Debug.Log(proof.Finish());

            var copiedBase = (AnimatorController)copy.runtimeAnimatorController;
            var copiedIdleBase = (AnimationClip)State(copiedBase, "Idle").motion;
            var copiedRunBase = (AnimationClip)State(copiedBase, "Run").motion;
            Assert.That(copiedIdleBase.name, Is.EqualTo("Idle_Loop"), "SOURCE21_UMA_IDLE_STANDIN");
            Assert.That(copiedRunBase.name, Is.EqualTo("Jog_Fwd_Loop"), "SOURCE21_UMA_RUN_STANDIN");
            Assert.That(Effective(copy, copiedIdleBase), Is.SameAs(idle), "SOURCE21_UMA_EFFECTIVE_IDLE");
            Assert.That(Effective(copy, copiedRunBase), Is.SameAs(run), "SOURCE21_UMA_EFFECTIVE_RUN");
        }

        private static AnimationClip Effective(AnimatorOverrideController controller, AnimationClip original)
        {
            Assert.That(original, Is.Not.Null);
            var replacement = controller[original];
            return replacement != null ? replacement : original;
        }

        private static AnimatorState State(AnimatorController controller, string name) =>
            controller.layers[0].stateMachine.states.Select(child => child.state).Single(state => state.name == name);

        private static string Absolute(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            assetPath.Replace('/', Path.DirectorySeparatorChar)));

        private static string HashAsset(string assetPath)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(Absolute(assetPath)))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class BoundedProof
        {
            private const int MaxBytes = 64 * 1024, MaxNameBytes = 256;
            private readonly StringBuilder text = new StringBuilder(8192);
            private int bytes;

            internal BoundedProof Append(string value)
            {
                Assert.That(value, Is.Not.Null, "SOURCE21_PROOF_SEGMENT_NULL");
                int added = Encoding.UTF8.GetByteCount(value);
                Assert.That(added, Is.LessThanOrEqualTo(MaxBytes - bytes), "SOURCE21_PROOF_CAP");
                text.Append(value); bytes += added;
                return this;
            }

            internal BoundedProof Append(char value) => Append(value.ToString());
            internal BoundedProof Append(int value) => Append(value.ToString(CultureInfo.InvariantCulture));
            internal string Finish() => text.ToString();

            internal static string Name(string value, string code)
            {
                Assert.That(value, Is.Not.Null, code);
                Assert.That(Encoding.UTF8.GetByteCount(value), Is.LessThanOrEqualTo(MaxNameBytes), code);
                return value;
            }
        }

        private static ContestantState Person(string id)
        {
            var person = ContentCatalog.Create(1).contestants.First(value => !value.isPlayer);
            person.id = id;
            return person;
        }
    }

    internal sealed class Source21UmaReadyBody
    {
        internal CharacterPresentation body;
        internal Animator animator;
        internal RuntimeAnimatorController controller;
        internal AnimationClip idle;
        internal string characterId;
        internal object identity;
        internal MethodInfo holds;
        internal float phase;
    }

    internal static class Source21UmaReadiness
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly HumanBodyBones[] Bones =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };

        internal static IEnumerator Wait(CharacterPresentation[] bodies, RuntimeAnimatorController controller,
            AnimationClip idle, float seconds, int frameCap, Source21UmaReadyBody[] result)
        {
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            var holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            Assert.That(capture, Is.Not.Null); Assert.That(holds, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            var startPhase = new float[2];
            var captured = new bool[2];
            var selected = new bool[2];
            while (Time.realtimeSinceStartup < deadline && frames++ < frameCap)
            {
                bool all = true;
                for (int i = 0; i < 2; i++)
                {
                    if (!captured[i])
                    {
                        if (!TryCapture(bodies[i], controller, idle, capture, holds, out result[i]))
                        { all = false; continue; }
                        var expectedFrame = i == 0 ? BodyFrame.Masculine : BodyFrame.Feminine;
                        Assert.That(bodies[i].Frame, Is.EqualTo(expectedFrame), "SOURCE21_UMA_FRAME_" + i);
                        var renderers = bodies[i].VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
                        Assert.That(renderers.Any(renderer => renderer.enabled && renderer.sharedMesh != null
                            && renderer.sharedMaterials.Length > 0
                            && renderer.sharedMaterials.All(material => material != null && material.shader != null)),
                            Is.True, "SOURCE21_UMA_MATERIAL_RENDERER_" + i);
                        result[i].animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        captured[i] = true;
                        all = false;
                        continue;
                    }
                    Assert.That((bool)holds.Invoke(bodies[i], new[] { result[i].identity }), Is.True,
                        "SOURCE21_READINESS_IDENTITY_CHANGED_" + i);
                    if (!TrySelection(result[i].animator, idle, out float phase)) { all = false; continue; }
                    if (!selected[i])
                    {
                        startPhase[i] = result[i].phase = phase;
                        selected[i] = true;
                        all = false;
                    }
                    else if (phase <= startPhase[i]) all = false;
                    else result[i].phase = phase;
                }
                if (all) yield break;
                yield return null;
            }
            Assert.Fail("SOURCE21_READINESS_TIMEOUT");
        }

        private static bool TryCapture(CharacterPresentation body, RuntimeAnimatorController controller,
            AnimationClip idle, MethodInfo capture, MethodInfo holds, out Source21UmaReadyBody ready)
        {
            ready = null;
            if (body == null || body.IsPrimitiveBody || body.IsBodyAssembling || body.IsChangingOutfit) return false;
            var args = new object[] { null };
            if (!(bool)capture.Invoke(body, args) || args[0] == null) return false;
            var identityType = args[0].GetType();
            var animatorField = identityType.GetField("Animator", Flags);
            if (animatorField == null) return false;
            var animator = animatorField.GetValue(args[0]) as Animator;
            var selected = body.VisualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || selected != animator || animator.runtimeAnimatorController != controller || !animator.isActiveAndEnabled
                || !animator.isInitialized || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid
                || animator.applyRootMotion || animator.speed != 1f || animator.updateMode != AnimatorUpdateMode.Normal
                || animator.layerCount != 1) return false;
            foreach (var bone in Bones) if (animator.GetBoneTransform(bone) == null) return false;
            ready = new Source21UmaReadyBody
            {
                body = body, animator = animator, controller = controller, idle = idle,
                characterId = body.CharacterId, identity = args[0], holds = holds
            };
            return true;
        }

        private static bool TrySelection(Animator animator, AnimationClip idle, out float phase)
        {
            phase = 0f;
            if (animator == null || animator.cullingMode != AnimatorCullingMode.AlwaysAnimate || animator.IsInTransition(0)) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (!state.IsName("Idle") || clips.Length != 1 || clips[0].clip != idle || !Finite(clips[0].weight)
                || clips[0].weight <= 0f || clips[0].weight > 1f || !Finite(state.normalizedTime) || state.normalizedTime < 0f) return false;
            phase = state.normalizedTime;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Read-only Source21 observer. It owns no IK channel and never writes animation state.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class Source21UmaIdleFootIkObserver : MonoBehaviour
    {
        private const float Window = 2f, RowInterval = 1f / 60f;
        private const int CallbackCap = 32768, HostCap = 16;
        private static readonly HumanBodyBones[] BoneKinds =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        private static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };

        private readonly Transform[] bones = new Transform[4];
        private readonly Vector3[] pose = new Vector3[4], candidatePose = new Vector3[4], baselinePose = new Vector3[4];
        private readonly float[] landmarkDrift = new float[4];
        private readonly float[] maxLandmarkDrift = new float[4];
        private readonly Vector3[] goalPosition = new Vector3[2];
        private readonly Quaternion[] goalRotation = new Quaternion[2];
        private readonly float[] positionWeight = new float[2], rotationWeight = new float[2];
        private readonly float[] minPositionWeight = { float.PositiveInfinity, float.PositiveInfinity };
        private readonly float[] maxPositionWeight = { float.NegativeInfinity, float.NegativeInfinity };
        private readonly float[] minRotationWeight = { float.PositiveInfinity, float.PositiveInfinity };
        private readonly float[] maxRotationWeight = { float.NegativeInfinity, float.NegativeInfinity };
        private readonly bool[] nonzeroPositionWeight = new bool[2], nonzeroRotationWeight = new bool[2];
        private readonly int[] goalSamples = new int[2];
        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>(2);
        private readonly List<MonoBehaviour> currentHosts = new List<MonoBehaviour>(HostCap);
        private MonoBehaviour[] hostBehaviours;
        private bool[] hostEnabled, hostActive;
        private Source21UmaReadyBody ready;
        private Session session;
        private CharacterPresentation body;
        private Animator animator;
        private object identity;
        private int index, stateHash, armedFrame, callbackFrame = -1, lateFrame = -1, candidateFrame = -1;
        private int callbacks, completedLate, samples, endpointFrame = -1;
        private float initialPhase, baselinePhase, lastPhase, phase, callbackElapsed, endpointElapsed = -1f;
        private float lastRow = float.NegativeInfinity;
        private Vector3 root, candidateRoot, baselineRoot;
        private Quaternion heading, candidateHeading, baselineHeading;
        private BodyFrame bodyFrame;
        private AnimatorCullingMode culling;
        private CharacterPresentation.Reaction? reaction;
        private CharacterPresentation.Gesture? gesture;
        private CharacterPresentation.BodyActivity activity;
        private float timeScale, maxFootDrift, maxRootDrift, maxHeadingDrift;
        private bool armed, pending, baseline, complete;

        internal static void DisarmAll(Source21UmaReadyBody[] bodies)
        {
            if (bodies == null) return;
            foreach (var item in bodies)
                if (item?.animator != null)
                    foreach (var observer in item.animator.GetComponents<Source21UmaIdleFootIkObserver>())
                    { observer.armed = false; observer.enabled = false; }
        }

        internal static IEnumerator Observe(Source21UmaReadyBody[] bodies, string provenance)
        {
            var session = new Session(provenance);
            var readers = new Source21UmaIdleFootIkObserver[2];
            try
            {
                Assert.That(bodies, Is.Not.Null); Assert.That(bodies.Length, Is.EqualTo(2));
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(bodies[i]?.animator, Is.Not.Null);
                    Assert.That(bodies[i].animator.GetComponents<Source21UmaIdleFootIkObserver>(), Is.Empty,
                        "SOURCE21_FRESH_OBSERVER_HOST_" + i);
                    readers[i] = bodies[i].animator.gameObject.AddComponent<Source21UmaIdleFootIkObserver>();
                    readers[i].Bind(bodies[i], i, session);
                    session.Register(i, readers[i]);
                }
                int frame = Time.frameCount;
                float now = Time.realtimeSinceStartup;
                foreach (var reader in readers) reader.Arm(frame, now);
                float deadline = now + 8f;
                while (!session.Done && session.Failure == null && Time.realtimeSinceStartup < deadline) yield return null;
                if (!session.Done && session.Failure == null) session.Fail(-1, "OBSERVATION_TIMEOUT");
            }
            finally
            {
                foreach (var reader in readers)
                    if (reader != null) { reader.armed = false; reader.enabled = false; }
                Debug.Log(session.Finish(readers));
                foreach (var reader in readers) if (reader != null) Object.Destroy(reader);
            }
            // The refutation, pinned (the lead's decision D1(b), r1 triage, 2026-10-16): with only the
            // copied Idle's Foot IK on, the four landmarks do not hold within 1 mm of one baseline for 2 s.
            // The UMA Idle itself moves a foot (21 mm over a loop, S5), and the native goals report zero
            // position and rotation weight, so Foot IK pins nothing. The first sample in which a landmark
            // passes 1 mm fails with LANDMARK_DRIFT_<that landmark>; which body and landmark varies with the
            // Idle's phase. Every other code stays a failure.
            Assert.That(session.FailureCode, Does.Match("^LANDMARK_DRIFT_[0-3]$"),
                "SOURCE21_REFUTATION_CHANGED: " + (session.Failure ?? "every landmark held for the whole window"));
            Assert.That(session.FailureSampled, Is.True, "SOURCE21_REFUTATION_SAMPLE_MISSING");
            Assert.That(session.Started, Is.True, "SOURCE21_BASELINE_MISSING");
            Assert.That(session.FailureElapsed, Is.GreaterThanOrEqualTo(0f).And.LessThan(Window), "SOURCE21_REFUTATION_OUTSIDE_THE_WINDOW");
            var failing = readers[session.FailureBody];
            int landmark = session.FailureCode[session.FailureCode.Length - 1] - '0';
            Assert.That(failing.samples, Is.GreaterThan(0), "SOURCE21_NO_WINDOW_SAMPLES");
            Assert.That(failing.landmarkDrift[landmark], Is.GreaterThan(.001f), "SOURCE21_REFUTATION_LANDMARK_UNDER_1MM");
            for (int earlier = 0; earlier < landmark; earlier++)
                Assert.That(failing.landmarkDrift[earlier], Is.LessThanOrEqualTo(.001f), "SOURCE21_REFUTATION_NOT_THE_FIRST_LANDMARK_" + earlier);
            foreach (var reader in readers)
            {
                Assert.That(reader.baseline, Is.True, "SOURCE21_BASELINE_MISSING");
                Assert.That(reader.baselinePhase, Is.GreaterThan(reader.initialPhase), "SOURCE21_BASELINE_DID_NOT_ADVANCE");
                for (int goal = 0; goal < Goals.Length; goal++)
                {
                    Assert.That(reader.goalSamples[goal], Is.GreaterThan(0), "SOURCE21_GOAL_UNREAD_" + reader.index + "_" + goal);
                    Assert.That(reader.nonzeroPositionWeight[goal] || reader.nonzeroRotationWeight[goal], Is.False,
                        "SOURCE21_REFUTATION_CHANGED: Foot IK weighted goal " + goal + " of body " + reader.index);
                }
            }
            for (int goal = 0; goal < Goals.Length; goal++)
                Assert.That(failing.positionWeight[goal] == 0f && failing.rotationWeight[goal] == 0f, Is.True,
                    "SOURCE21_REFUTATION_CHANGED: the failing sample's goal " + goal + " is weighted");
        }

        private void Bind(Source21UmaReadyBody value, int bodyIndex, Session owner)
        {
            ready = value; session = owner; index = bodyIndex; body = value.body; animator = value.animator; identity = value.identity;
            if (!Need(body != null && animator != null && identity != null, "BIND_IDENTITY")) return;
            if (!Need((bool)value.holds.Invoke(body, new[] { identity }), "BIND_HELD")) return;
            if (!Need(body.IsStill && !body.IsSeated && !body.IsTalking && !body.IsArguing && !body.IsReacting,
                "BIND_STATIC_IDLE")) return;
            for (int i = 0; i < bones.Length; i++)
            { bones[i] = animator.GetBoneTransform(BoneKinds[i]); if (!Need(bones[i] != null, "BIND_BONE_" + i)) return; }
            var state = animator.GetCurrentAnimatorStateInfo(0);
            initialPhase = lastPhase = state.normalizedTime; stateHash = state.fullPathHash;
            Need(Finite(initialPhase) && initialPhase >= 0f && state.IsName("Idle"), "BIND_IDLE");
            bodyFrame = body.Frame; culling = animator.cullingMode; reaction = body.LastReaction; gesture = body.LastGesture;
            activity = body.Activity; timeScale = Time.timeScale;
            Need(culling == AnimatorCullingMode.AlwaysAnimate && Finite(timeScale) && timeScale > 0f, "BIND_RUNTIME");
            CaptureHosts();
        }

        private void CaptureHosts()
        {
            hostBehaviours = animator.gameObject.GetComponents<MonoBehaviour>();
            if (!Need(hostBehaviours.Length <= HostCap, "HOST_CAP")) return;
            hostEnabled = new bool[hostBehaviours.Length]; hostActive = new bool[hostBehaviours.Length];
            for (int i = 0; i < hostBehaviours.Length; i++)
            {
                var host = hostBehaviours[i]; if (!Need(host != null, "HOST_NULL")) return;
                hostEnabled[i] = host.enabled; hostActive[i] = host.isActiveAndEnabled;
                int callbacksFound = 0, depth = 0;
                for (var type = host.GetType(); type != null && typeof(MonoBehaviour).IsAssignableFrom(type); type = type.BaseType)
                {
                    if (!Need(++depth <= 16, "HOST_INHERITANCE_CAP")) return;
                    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.Name != "OnAnimatorIK") continue;
                        if (!Need(++callbacksFound <= 16, "HOST_CALLBACK_CAP")) return;
                    }
                }
                session.Line("HOST body=" + index + " ordinal=" + i + " type=" + host.GetType().FullName
                    + " enabled=" + Bool(hostEnabled[i]) + " active=" + Bool(hostActive[i])
                    + " callbackMethods=" + callbacksFound + " fixtureReader=" + Bool(host == this)
                    + " foreignOwnership=" + (host == this ? "not-applicable" : "unproven") + "\n", false);
            }
        }

        private void Arm(int frame, float now)
        { armedFrame = frame; session.ArmTime = now; armed = true; }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!armed || complete || session == null || session.Failure != null) return;
            if (++callbacks > CallbackCap) { session.Fail(index, "CALLBACK_CAP"); return; }
            if (!Need(layerIndex == 0 && !pending && callbackFrame != Time.frameCount, "CALLBACK_PAIRING")) return;
            for (int i = 0; i < Goals.Length; i++)
            {
                goalPosition[i] = animator.GetIKPosition(Goals[i]);
                goalRotation[i] = animator.GetIKRotation(Goals[i]);
                positionWeight[i] = animator.GetIKPositionWeight(Goals[i]);
                rotationWeight[i] = animator.GetIKRotationWeight(Goals[i]);
                if (!Need(Finite(goalPosition[i]) && Finite(goalRotation[i]) && Weight(positionWeight[i]) && Weight(rotationWeight[i]),
                    "CALLBACK_NONFINITE_" + i)) return;
                minPositionWeight[i] = Mathf.Min(minPositionWeight[i], positionWeight[i]);
                maxPositionWeight[i] = Mathf.Max(maxPositionWeight[i], positionWeight[i]);
                minRotationWeight[i] = Mathf.Min(minRotationWeight[i], rotationWeight[i]);
                maxRotationWeight[i] = Mathf.Max(maxRotationWeight[i], rotationWeight[i]);
                nonzeroPositionWeight[i] |= positionWeight[i] > 0f;
                nonzeroRotationWeight[i] |= rotationWeight[i] > 0f;
                goalSamples[i]++;
            }
            callbackElapsed = Time.realtimeSinceStartup - session.ArmTime;
            if (!Need(Finite(callbackElapsed) && callbackElapsed >= 0f, "CALLBACK_TIME")) return;
            callbackFrame = Time.frameCount; pending = true;
        }

        private void LateUpdate()
        {
            if (!armed || complete || session == null || session.Failure != null) return;
            if (Time.frameCount == armedFrame && !pending) return;
            if (!Need(pending && callbackFrame == Time.frameCount && lateFrame != Time.frameCount, "SAME_FRAME_LATE")) return;
            if (!CheckLeaseAndPose()) return;
            lateFrame = Time.frameCount; completedLate++; pending = false;

            if (!session.Started)
            {
                if (phase <= initialPhase) return;
                Array.Copy(pose, candidatePose, pose.Length);
                candidateRoot = root; candidateHeading = heading; candidateFrame = Time.frameCount;
                session.TryCommitBaseline(Time.frameCount);
                return;
            }
            if (Time.frameCount == session.BaselineFrame) return;

            float elapsed = Time.realtimeSinceStartup - session.StartedAt;
            if (!Need(Finite(elapsed) && elapsed >= 0f, "WINDOW_TIME")) return;
            float footDrift = 0f;
            string acceptanceFailure = null;
            for (int i = 0; i < pose.Length; i++)
            {
                landmarkDrift[i] = Vector3.Distance(baselinePose[i], pose[i]);
                if (!Need(Finite(landmarkDrift[i]), "LANDMARK_NONFINITE_" + i)) return;
                footDrift = Mathf.Max(footDrift, landmarkDrift[i]);
                maxLandmarkDrift[i] = Mathf.Max(maxLandmarkDrift[i], landmarkDrift[i]);
                if (acceptanceFailure == null && landmarkDrift[i] > .001f) acceptanceFailure = "LANDMARK_DRIFT_" + i;
            }
            float rootDrift = Vector3.Distance(baselineRoot, root);
            float headingDrift = Quaternion.Angle(baselineHeading, heading);
            if (!Need(Finite(rootDrift) && Finite(headingDrift), "ROOT_NONFINITE")) return;
            if (acceptanceFailure == null && rootDrift > .001f) acceptanceFailure = "ROOT_DRIFT";
            if (acceptanceFailure == null && headingDrift > .05f) acceptanceFailure = "HEADING_DRIFT";
            maxFootDrift = Mathf.Max(maxFootDrift, footDrift);
            maxRootDrift = Mathf.Max(maxRootDrift, rootDrift);
            maxHeadingDrift = Mathf.Max(maxHeadingDrift, headingDrift);
            samples++;
            if (acceptanceFailure != null)
            {
                session.FailSample(this, acceptanceFailure, elapsed, footDrift, rootDrift, headingDrift);
                return;
            }
            if (elapsed >= lastRow + RowInterval || elapsed >= Window)
            {
                lastRow = elapsed;
                session.Row(this, elapsed, footDrift, rootDrift, headingDrift);
            }
            if (elapsed >= Window)
            {
                complete = true; endpointFrame = Time.frameCount; endpointElapsed = elapsed; session.MarkComplete(index);
            }
        }

        private bool CheckLeaseAndPose()
        {
            if (!Need((bool)ready.holds.Invoke(body, new[] { identity }), "IDENTITY_CHANGED")) return false;
            currentHosts.Clear(); animator.GetComponents(currentHosts);
            if (!Need(currentHosts.Count == hostBehaviours.Length, "HOST_COUNT_CHANGED")) return false;
            for (int i = 0; i < hostBehaviours.Length; i++)
                if (!Need(currentHosts[i] == hostBehaviours[i] && currentHosts[i] != null
                    && currentHosts[i].enabled == hostEnabled[i] && currentHosts[i].isActiveAndEnabled == hostActive[i], "HOST_LEASE_CHANGED")) return false;
            if (!Need(body.isActiveAndEnabled && !body.ReducedMotion && body.IsStill
                && !body.IsSeated && !body.IsTalking && !body.IsArguing && !body.IsReacting
                && body.CharacterId == ready.characterId && body.Frame == bodyFrame
                && body.LastReaction == reaction && body.LastGesture == gesture && body.Activity == activity,
                "PRESENTATION_CHANGED")) return false;
            if (!Need(Time.timeScale == timeScale && animator.cullingMode == culling && animator.runtimeAnimatorController == ready.controller
                && animator.isActiveAndEnabled && animator.isInitialized && animator.isHuman && animator.avatar != null && animator.avatar.isValid
                && !animator.applyRootMotion && animator.speed == 1f && animator.updateMode == AnimatorUpdateMode.Normal, "ANIMATOR_CHANGED")) return false;
            if (!Need(!animator.IsInTransition(0), "IDLE_TRANSITION")) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0); clips.Clear(); animator.GetCurrentAnimatorClipInfo(0, clips);
            if (!Need(state.IsName("Idle") && state.fullPathHash == stateHash && clips.Count == 1 && clips[0].clip == ready.idle
                && Weight(clips[0].weight) && clips[0].weight > 0f && Finite(state.normalizedTime) && state.normalizedTime >= lastPhase,
                "IDLE_SELECTION")) return false;
            phase = state.normalizedTime; lastPhase = phase;
            root = body.transform.position; heading = body.transform.rotation;
            if (!Need(Finite(root) && ValidRotation(heading), "ROOT_POSE")) return false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (!Need(animator.GetBoneTransform(BoneKinds[i]) == bones[i], "BONE_MAPPING_CHANGED_" + i)) return false;
                pose[i] = bones[i].position;
                if (!Need(Finite(pose[i]), "LANDMARK_POSE")) return false;
            }
            return true;
        }

        private void CommitBaseline(int frame)
        {
            if (!Need(!baseline && candidateFrame == frame, "BASELINE_RECAPTURE")) return;
            Array.Copy(candidatePose, baselinePose, pose.Length);
            baselineRoot = candidateRoot; baselineHeading = candidateHeading; baselinePhase = phase; baseline = true;
        }

        private bool Need(bool condition, string code)
        {
            if (!condition) session?.Fail(index, code);
            return condition;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        private static bool ValidRotation(Quaternion value) => Finite(value)
            && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > .000001f;
        private static bool Weight(float value) => Finite(value) && value >= 0f && value <= 1f;
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Bool(bool value) => value ? "true" : "false";
        private static void Vector(StringBuilder text, Vector3 value) => text.Append(Number(value.x)).Append(',').Append(Number(value.y)).Append(',').Append(Number(value.z));
        private static void Rotation(StringBuilder text, Quaternion value) => text.Append(Number(value.x)).Append(',').Append(Number(value.y)).Append(',').Append(Number(value.z)).Append(',').Append(Number(value.w));

        private sealed class Session
        {
            private readonly Report report;
            private readonly Source21UmaIdleFootIkObserver[] readers = new Source21UmaIdleFootIkObserver[2];
            private readonly bool[] completed = new bool[2];
            private string failureSample;
            internal string Failure { get; private set; }
            internal float ArmTime, StartedAt;
            internal int BaselineFrame { get; private set; } = -1;
            internal bool Started => BaselineFrame >= 0;
            internal bool Done => completed[0] && completed[1];

            internal Session(string provenance)
            {
                report = new Report();
                report.Append("SOURCE21-IDLE-FOOT-IK version=1 fixture=uma provenance=" + provenance
                    + " durationRequested=2 culling=AlwaysAnimate farPark=true changedField=Idle.iKOnFeet"
                    + " fixtureSetterCount=0 fixtureOwnedMask=0 nativeWeightMustBeZero=false"
                    + " foreignWriterOwnership=unproven wholeFrameFinal=false shippingAcceptance=false\n", false);
            }

            internal void Register(int index, Source21UmaIdleFootIkObserver reader) { readers[index] = reader; }
            internal int FailureBody { get; private set; } = -1;
            internal string FailureCode { get; private set; }
            internal bool FailureSampled { get; private set; }
            internal float FailureElapsed { get; private set; } = -1f;
            internal void Fail(int body, string code)
            { if (Failure == null) { Failure = "body=" + body + " code=" + code; FailureBody = body; FailureCode = code; } }
            internal void Line(string value, bool row)
            { if (!report.Append(value, row)) Fail(-1, row ? "REPORT_ROW_OR_BYTE_CAP" : "REPORT_BYTE_CAP"); }

            internal void TryCommitBaseline(int frame)
            {
                if (Started || readers[0] == null || readers[1] == null
                    || readers[0].candidateFrame != frame || readers[1].candidateFrame != frame) return;
                readers[0].CommitBaseline(frame); readers[1].CommitBaseline(frame);
                if (Failure != null) return;
                BaselineFrame = frame; StartedAt = Time.realtimeSinceStartup;
                var baseline = new StringBuilder(1536);
                baseline.Append("BASELINE frame=").Append(frame)
                    .Append(" pairedSameLate=true naturallyAdvanced=true recapture=false");
                for (int body = 0; body < readers.Length; body++)
                {
                    var reader = readers[body];
                    baseline.Append(" body").Append(body).Append("Phase=").Append(Number(reader.baselinePhase))
                        .Append(" root="); Vector(baseline, reader.baselineRoot);
                    baseline.Append(" heading="); Rotation(baseline, reader.baselineHeading);
                    for (int landmark = 0; landmark < reader.baselinePose.Length; landmark++)
                    { baseline.Append(" landmark").Append(landmark).Append('='); Vector(baseline, reader.baselinePose[landmark]); }
                }
                baseline.Append('\n'); Line(baseline.ToString(), false);
            }

            internal void MarkComplete(int index) { completed[index] = true; }

            internal void FailSample(Source21UmaIdleFootIkObserver reader, string code,
                float elapsed, float feet, float root, float heading)
            {
                if (Failure != null) return;
                var row = new StringBuilder(2048);
                row.Append("FAIL_SAMPLE code=").Append(code).Append(" body=").Append(reader.index)
                    .Append(" callbackFrame=").Append(reader.callbackFrame).Append(" lateFrame=").Append(reader.lateFrame)
                    .Append(" elapsed=").Append(Number(elapsed)).Append(" callbackElapsed=").Append(Number(reader.callbackElapsed))
                    .Append(" phase=").Append(Number(reader.phase)).Append(" maxLandmarkDrift=").Append(Number(feet))
                    .Append(" rootDrift=").Append(Number(root)).Append(" headingDrift=").Append(Number(heading))
                    .Append(" fixtureSetterCount=0 fixtureOwnedMask=0");
                for (int i = 0; i < 2; i++)
                {
                    row.Append(" goal").Append(i).Append('='); Vector(row, reader.goalPosition[i]); row.Append(','); Rotation(row, reader.goalRotation[i]);
                    row.Append(",positionWeight=").Append(Number(reader.positionWeight[i]))
                        .Append(",rotationWeight=").Append(Number(reader.rotationWeight[i]));
                }
                for (int i = 0; i < 4; i++)
                {
                    row.Append(" landmark").Append(i).Append('='); Vector(row, reader.pose[i]);
                    row.Append(" drift=").Append(Number(reader.landmarkDrift[i]));
                }
                row.Append('\n');
                failureSample = Encoding.UTF8.GetByteCount(row.ToString()) <= 2048
                    ? row.ToString() : "FAIL_SAMPLE code=" + code + " body=" + reader.index + " detail=FAIL_SAMPLE_CAP\n";
                FailureSampled = true; FailureElapsed = elapsed;
                Fail(reader.index, code);
            }

            internal void Row(Source21UmaIdleFootIkObserver reader, float elapsed, float feet, float root, float heading)
            {
                var row = new StringBuilder(1536);
                row.Append("ROW body=").Append(reader.index).Append(" callbackFrame=").Append(reader.callbackFrame)
                    .Append(" lateFrame=").Append(reader.lateFrame).Append(" elapsed=").Append(Number(elapsed))
                    .Append(" callbackElapsed=").Append(Number(reader.callbackElapsed))
                    .Append(" phase=").Append(Number(reader.phase)).Append(" maxLandmarkDrift=").Append(Number(feet))
                    .Append(" rootDrift=").Append(Number(root)).Append(" headingDrift=").Append(Number(heading))
                    .Append(" fixtureSetterCount=0 fixtureOwnedMask=0");
                for (int i = 0; i < 2; i++)
                {
                    row.Append(" goal").Append(i).Append('='); Vector(row, reader.goalPosition[i]); row.Append(','); Rotation(row, reader.goalRotation[i]);
                    row.Append(",positionWeight=").Append(Number(reader.positionWeight[i]))
                        .Append(",rotationWeight=").Append(Number(reader.rotationWeight[i]));
                }
                for (int i = 0; i < 4; i++)
                {
                    row.Append(" landmark").Append(i).Append('='); Vector(row, reader.pose[i]);
                    row.Append(" drift=").Append(Number(reader.landmarkDrift[i]));
                }
                row.Append('\n'); Line(row.ToString(), true);
            }

            internal string Finish(Source21UmaIdleFootIkObserver[] values)
            {
                var tail = new StringBuilder(4096);
                if (failureSample != null) tail.Append(failureSample);
                tail.Append("SUMMARY passed=").Append(Failure == null && Done ? "true" : "false")
                    .Append(" failure=").Append(Failure ?? "none").Append(" baselineFrame=").Append(BaselineFrame)
                    .Append(" fixtureSetterCount=0 fixtureOwnedMask=0 nativeWeightMustBeZero=false\n");
                for (int b = 0; b < values.Length; b++)
                {
                    var r = values[b];
                    if (r == null) { tail.Append("BODY ").Append(b).Append(" absent=true\n"); continue; }
                    tail.Append("BODY ").Append(b).Append(" callbacks=").Append(r.callbacks).Append(" completedLate=").Append(r.completedLate)
                        .Append(" samples=").Append(r.samples).Append(" endpointFrame=").Append(r.endpointFrame)
                        .Append(" endpointElapsed=").Append(Number(r.endpointElapsed))
                        .Append(" initialPhase=").Append(Number(r.initialPhase)).Append(" baselinePhase=").Append(Number(r.baselinePhase))
                        .Append(" endpointPhase=").Append(Number(r.phase))
                        .Append(" maxFootDrift=").Append(Number(r.maxFootDrift)).Append(" maxRootDrift=").Append(Number(r.maxRootDrift))
                        .Append(" maxHeadingDrift=").Append(Number(r.maxHeadingDrift));
                    for (int i = 0; i < 4; i++)
                        tail.Append(" landmark").Append(i).Append("MaxDrift=").Append(Number(r.maxLandmarkDrift[i]));
                    for (int i = 0; i < 2; i++)
                    {
                        tail.Append(" goal").Append(i);
                        if (r.goalSamples[i] == 0) tail.Append("WeightsUnobserved=true samples=0");
                        else tail.Append("PositionWeightMin=").Append(Number(r.minPositionWeight[i]))
                            .Append(" max=").Append(Number(r.maxPositionWeight[i])).Append(" nonzero=").Append(Bool(r.nonzeroPositionWeight[i]))
                            .Append(" rotationWeightMin=").Append(Number(r.minRotationWeight[i]))
                            .Append(" max=").Append(Number(r.maxRotationWeight[i])).Append(" nonzero=").Append(Bool(r.nonzeroRotationWeight[i]))
                            .Append(" samples=").Append(r.goalSamples[i]);
                    }
                    tail.Append('\n');
                }
                report.AppendTail(tail.ToString());
                return report.Text;
            }
        }

        private sealed class Report
        {
            private const int MaxBytes = 4 * 1024 * 1024, MaxRows = 1024, Reserve = 4096;
            private readonly StringBuilder text = new StringBuilder(8192);
            private int bytes, rows;
            internal string Text => text.ToString();

            internal bool Append(string value, bool row)
            {
                int size = Encoding.UTF8.GetByteCount(value);
                if ((row && rows >= MaxRows) || size > MaxBytes - Reserve - bytes) return false;
                text.Append(value); bytes += size; if (row) rows++;
                return true;
            }

            internal void AppendTail(string value)
            {
                int size = Encoding.UTF8.GetByteCount(value);
                if (size > Reserve || bytes + size > MaxBytes)
                    value = "SUMMARY passed=false failure=REPORT_TAIL_CAP fixtureSetterCount=0 fixtureOwnedMask=0\n";
                text.Append(value);
            }
        }
    }
}
#endif
