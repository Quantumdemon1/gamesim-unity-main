#if UNITY_EDITOR
namespace Gamesim.Tests.Diagnostics
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
    using NUnit.Framework;
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
    /// Gamesim.Tests.PlayMode to this GAMESIM_DIAGNOSTICS assembly.
    /// </summary>
    public sealed class Source21ProvidedIdleFootIkPlayModeTests
    {
        private const string SkeletonPath = "Assets/Gamesim/Art/Authored/Animation/Humanoid/bb_anim_PoseAtEase.fbx";
        private const string SourceControllerPath = "Assets/Gamesim/Art/Characters/GamesimHumanoid.controller";
        private const string HandlePath = "Assets/Gamesim/Resources/Animation/GamesimHumanoid.overrideController";
        // Where the copy used to be written; asserted absent, so a regression back to disk is caught.
        private const string FormerCandidatePath = "Assets/Gamesim/Tests/PlayMode/__Source21NormalIdleFootIkProvided.controller";
        private const int MappingCap = 128;

        private readonly List<RuntimeAnimatorController> ownedRuntimeControllers = new List<RuntimeAnimatorController>(4);
        private readonly Source21ProvidedReadyBody[] ready = new Source21ProvidedReadyBody[2];
        private GameObject first, second;
        private CharacterPresentation a, b;
        private Source21ProvidedBodyProvider provider;
        private DiagnosticIdleFootIkController controllerCopy;
        private AnimatorController candidate;
        private AnimatorOverrideController candidateRuntime;
        private AnimationClip expectedIdle;
        private bool providerRegistered;
        private string sourceHash, handleHash;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            PrepareCandidate();
            provider = new Source21ProvidedBodyProvider(candidateRuntime);
            CharacterBodySource.Register(provider);
            providerRegistered = true;

            first = new GameObject("Source21 Foot IK provided first");
            second = new GameObject("Source21 Foot IK provided second");
            first.transform.position = new Vector3(1600f, 0f, 1600f);
            second.transform.position = first.transform.position + Vector3.right * 3.34f;
            a = CharacterPresentation.Attach(first, ContentCatalog.Create(91).Find(ContentCatalog.PlayerId), Color.white);
            b = CharacterPresentation.Attach(second, ContentCatalog.Create(92).contestants.First(person => !person.isPlayer), Color.white);
            a.SetReducedMotion(false);
            b.SetReducedMotion(false);

            yield return Source21ProvidedReadiness.Wait(new[] { a, b }, candidateRuntime, expectedIdle,
                AnimatorCullingMode.AlwaysAnimate, 3f, int.MaxValue, ready);
        }

        [UnityTest]
        [Category("FarewellIdleFootIkDiagnostic")]
        public IEnumerator CopiedIdleFootIk_ProvidedSkeletonsKeepFourLandmarksAgainstOneBaseline()
        {
            yield return Source21ProvidedIdleFootIkObserver.Observe(ready,
                "source21-provided-far-always-animate-idle-footik-only");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Source21ProvidedIdleFootIkObserver.DisarmAll(ready);
            if (providerRegistered && provider != null)
            {
                CharacterBodySource.Unregister(provider);
                providerRegistered = false;
            }
            if (first != null) Object.Destroy(first);
            if (second != null) Object.Destroy(second);
            foreach (var controller in ownedRuntimeControllers)
                if (controller != null && !EditorUtility.IsPersistent(controller)) Object.Destroy(controller);
            yield return null;

            // The bodies that played the copy are gone; only now is the in-memory controller released.
            controllerCopy?.Dispose();
            bool providerReleased = provider == null || CharacterBodySource.Provider != provider;
            bool objectsReleased = first == null && second == null;
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

            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath);
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Assert.That(source, Is.Not.Null, "SOURCE21_SOURCE_CONTROLLER");
            Assert.That(handle, Is.Not.Null, "SOURCE21_RESOURCES_HANDLE");
            Assert.That(AssetDatabase.GetAssetPath(handle), Is.EqualTo(HandlePath));
            Assert.That(handle.runtimeAnimatorController, Is.SameAs(source), "SOURCE21_RESOURCES_BASE");

            controllerCopy = DiagnosticIdleFootIkController.Build(source, "Source21 provided Idle Foot IK (in memory)");
            candidate = controllerCopy.Copy;
            candidateRuntime = new AnimatorOverrideController(candidate) { name = "Source21 provided Idle Foot IK" };
            ownedRuntimeControllers.Add(candidateRuntime);
            CopyAndVerifyOverrides(handle, candidateRuntime);
            expectedIdle = Effective(candidateRuntime, (AnimationClip)State(candidate, "Idle").motion);
            Assert.That(expectedIdle, Is.SameAs(Effective(handle, (AnimationClip)State(source, "Idle").motion)),
                "SOURCE21_EFFECTIVE_IDLE_CHANGED");
            Assert.That(expectedIdle != null && expectedIdle.humanMotion && expectedIdle.isLooping
                && Finite(expectedIdle.length) && expectedIdle.length > 0f, Is.True, "SOURCE21_EFFECTIVE_IDLE_INVALID");
            Assert.That(HashAsset(SourceControllerPath), Is.EqualTo(sourceHash), "SOURCE21_SOURCE_DIRTIED");
            Assert.That(HashAsset(HandlePath), Is.EqualTo(handleHash), "SOURCE21_HANDLE_DIRTIED");
        }

        private static void CopyAndVerifyOverrides(AnimatorOverrideController source, AnimatorOverrideController copy)
        {
            Assert.That(source.overridesCount, Is.LessThanOrEqualTo(MappingCap), "SOURCE21_OVERRIDE_CAP");
            var sourceRows = new List<KeyValuePair<AnimationClip, AnimationClip>>(source.overridesCount);
            source.GetOverrides(sourceRows);
            Assert.That(sourceRows.Count, Is.EqualTo(source.overridesCount), "SOURCE21_OVERRIDE_SOURCE_COUNT");
            var keys = new HashSet<AnimationClip>();
            foreach (var row in sourceRows)
            {
                Assert.That(row.Key, Is.Not.Null, "SOURCE21_OVERRIDE_KEY_NULL");
                Assert.That(keys.Add(row.Key), Is.True, "SOURCE21_OVERRIDE_KEY_DUPLICATE");
            }
            copy.ApplyOverrides(sourceRows);
            Assert.That(copy.overridesCount, Is.EqualTo(sourceRows.Count), "SOURCE21_OVERRIDE_COPY_ADMISSION");
            Assert.That(copy.overridesCount, Is.LessThanOrEqualTo(MappingCap), "SOURCE21_OVERRIDE_COPY_CAP");
            var copiedRows = new List<KeyValuePair<AnimationClip, AnimationClip>>(copy.overridesCount);
            copy.GetOverrides(copiedRows);
            Assert.That(copiedRows.Count, Is.EqualTo(sourceRows.Count), "SOURCE21_OVERRIDE_COUNT");
            var proof = new BoundedProof();
            proof.Append("SOURCE21-OVERRIDES version=1 fixture=provided count=").Append(sourceRows.Count).Append('\n');
            for (int i = 0; i < sourceRows.Count; i++)
            {
                Assert.That(copiedRows[i].Key, Is.SameAs(sourceRows[i].Key), "SOURCE21_OVERRIDE_KEY_" + i);
                Assert.That(copiedRows[i].Value, Is.SameAs(sourceRows[i].Value), "SOURCE21_OVERRIDE_VALUE_" + i);
                var effective = sourceRows[i].Value != null ? sourceRows[i].Value : sourceRows[i].Key;
                var keyName = BoundedProof.Name(sourceRows[i].Key.name, "SOURCE21_OVERRIDE_KEY_NAME_CAP_" + i);
                var effectiveName = BoundedProof.Name(effective.name, "SOURCE21_OVERRIDE_EFFECTIVE_NAME_CAP_" + i);
                proof.Append("MAP ordinal=").Append(i).Append(" key=").Append(keyName)
                    .Append('#').Append(sourceRows[i].Key.GetEntityId().ToString()).Append(" effective=")
                    .Append(effectiveName).Append('#').Append(effective.GetEntityId().ToString()).Append('\n');
            }
            Debug.Log(proof.Finish());
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

        private sealed class Source21ProvidedBodyProvider : IModularCharacterBodyProvider
        {
            private readonly RuntimeAnimatorController controller;
            internal Source21ProvidedBodyProvider(RuntimeAnimatorController value) { controller = value; }
            public ICharacterAppearanceCatalog Catalog => null;

            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badge, out CharacterBody body)
            {
                body = default;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(SkeletonPath).OfType<Avatar>().FirstOrDefault();
                if (model == null || avatar == null || controller == null) return false;
                var root = Object.Instantiate(model, parent, false);
                // Explicit == null: GetComponent returns Unity's fake null in the editor, which ?? never replaces.
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                body = new CharacterBody(root, animator, false, this);
                return true;
            }

            public bool TryCreate(string id, Transform parent, Color colour, out CharacterBody body)
            { body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color colour) { }
        }
    }

    internal sealed class Source21ProvidedReadyBody
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

    internal static class Source21ProvidedReadiness
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly HumanBodyBones[] Bones =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };

        internal static IEnumerator Wait(CharacterPresentation[] bodies, RuntimeAnimatorController controller,
            AnimationClip idle, AnimatorCullingMode culling, float seconds, int frameCap, Source21ProvidedReadyBody[] result)
        {
            var capture = typeof(CharacterPresentation).GetMethod("TryGetFarewellBody", Flags);
            var holds = typeof(CharacterPresentation).GetMethod("HoldsFarewellBody", Flags);
            Assert.That(capture, Is.Not.Null); Assert.That(holds, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            var startPhase = new float[2];
            var captured = new bool[2];
            while (Time.realtimeSinceStartup < deadline && frames++ < frameCap)
            {
                bool all = true;
                for (int i = 0; i < 2; i++)
                {
                    if (!captured[i])
                    {
                        if (!TryCapture(bodies[i], controller, idle, culling, capture, holds, out result[i]))
                        { all = false; continue; }
                        startPhase[i] = result[i].phase;
                        captured[i] = true;
                        all = false;
                        continue;
                    }
                    Assert.That((bool)holds.Invoke(bodies[i], new[] { result[i].identity }), Is.True,
                        "SOURCE21_READINESS_IDENTITY_CHANGED_" + i);
                    if (!TrySelection(result[i].animator, idle, culling, out float phase) || phase <= startPhase[i]) all = false;
                    else result[i].phase = phase;
                }
                if (all) yield break;
                yield return null;
            }
            Assert.Fail("SOURCE21_READINESS_TIMEOUT");
        }

        private static bool TryCapture(CharacterPresentation body, RuntimeAnimatorController controller, AnimationClip idle,
            AnimatorCullingMode culling, MethodInfo capture, MethodInfo holds, out Source21ProvidedReadyBody ready)
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
                || animator.cullingMode != culling || animator.layerCount != 1) return false;
            if (!TrySelection(animator, idle, culling, out float phase)) return false;
            foreach (var bone in Bones) if (animator.GetBoneTransform(bone) == null) return false;
            ready = new Source21ProvidedReadyBody
            {
                body = body, animator = animator, controller = controller, idle = idle,
                characterId = body.CharacterId, identity = args[0], holds = holds, phase = phase
            };
            return true;
        }

        private static bool TrySelection(Animator animator, AnimationClip idle, AnimatorCullingMode culling, out float phase)
        {
            phase = 0f;
            if (animator == null || animator.cullingMode != culling || animator.IsInTransition(0)) return false;
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
    public sealed class Source21ProvidedIdleFootIkObserver : MonoBehaviour
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
        private Source21ProvidedReadyBody ready;
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

        internal static void DisarmAll(Source21ProvidedReadyBody[] bodies)
        {
            if (bodies == null) return;
            foreach (var item in bodies)
                if (item?.animator != null)
                    foreach (var observer in item.animator.GetComponents<Source21ProvidedIdleFootIkObserver>())
                    { observer.armed = false; observer.enabled = false; }
        }

        internal static IEnumerator Observe(Source21ProvidedReadyBody[] bodies, string provenance)
        {
            var session = new Session(provenance);
            var readers = new Source21ProvidedIdleFootIkObserver[2];
            try
            {
                Assert.That(bodies, Is.Not.Null); Assert.That(bodies.Length, Is.EqualTo(2));
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(bodies[i]?.animator, Is.Not.Null);
                    Assert.That(bodies[i].animator.GetComponents<Source21ProvidedIdleFootIkObserver>(), Is.Empty,
                        "SOURCE21_FRESH_OBSERVER_HOST_" + i);
                    readers[i] = bodies[i].animator.gameObject.AddComponent<Source21ProvidedIdleFootIkObserver>();
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
            Assert.That(session.Failure, Is.Null, session.Failure);
            foreach (var reader in readers)
            {
                Assert.That(reader.baseline, Is.True, "SOURCE21_BASELINE_MISSING");
                Assert.That(reader.complete, Is.True, "SOURCE21_ENDPOINT_MISSING");
                Assert.That(reader.samples, Is.GreaterThan(0), "SOURCE21_NO_WINDOW_SAMPLES");
                Assert.That(reader.baselinePhase, Is.GreaterThan(reader.initialPhase), "SOURCE21_BASELINE_DID_NOT_ADVANCE");
                Assert.That(reader.phase, Is.GreaterThan(reader.baselinePhase), "SOURCE21_WINDOW_IDLE_DID_NOT_ADVANCE");
                Assert.That(reader.endpointFrame, Is.GreaterThan(session.BaselineFrame));
                Assert.That(reader.endpointElapsed, Is.GreaterThanOrEqualTo(Window), "SOURCE21_ENDPOINT_BEFORE_WINDOW");
            }
        }

        private void Bind(Source21ProvidedReadyBody value, int bodyIndex, Session owner)
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
            private readonly Source21ProvidedIdleFootIkObserver[] readers = new Source21ProvidedIdleFootIkObserver[2];
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
                report.Append("SOURCE21-IDLE-FOOT-IK version=1 fixture=provided provenance=" + provenance
                    + " durationRequested=2 culling=AlwaysAnimate farPark=true changedField=Idle.iKOnFeet"
                    + " fixtureSetterCount=0 fixtureOwnedMask=0 nativeWeightMustBeZero=false"
                    + " foreignWriterOwnership=unproven wholeFrameFinal=false shippingAcceptance=false\n", false);
            }

            internal void Register(int index, Source21ProvidedIdleFootIkObserver reader) { readers[index] = reader; }
            internal void Fail(int body, string code) { if (Failure == null) Failure = "body=" + body + " code=" + code; }
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

            internal void FailSample(Source21ProvidedIdleFootIkObserver reader, string code,
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
                Fail(reader.index, code);
            }

            internal void Row(Source21ProvidedIdleFootIkObserver reader, float elapsed, float feet, float root, float heading)
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

            internal string Finish(Source21ProvidedIdleFootIkObserver[] values)
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
