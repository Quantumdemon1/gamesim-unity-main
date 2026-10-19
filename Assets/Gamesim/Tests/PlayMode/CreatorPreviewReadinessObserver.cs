using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Temporary, opt-in first-preview evidence. Samples before normal Update; never drives the provider.
    /// The production failure snapshot, not this prior-frame sample, identifies the exact timeout state.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class CreatorPreviewReadinessObserver : MonoBehaviour, IDisposable
    {
        private const int MaximumSamples = 4096, HeadCount = 4, TailCount = 4;
        private static readonly object Unavailable = new object();
        private readonly Dictionary<Tuple<Type, string>, FieldInfo> fields = new Dictionary<Tuple<Type, string>, FieldInfo>();
        private readonly List<string> first = new List<string>();
        private readonly Queue<string> last = new Queue<string>();
        private CharacterCreator creator;
        private GameObject observedBody;
        private Component data, tint;
        private string previousState, lastOriginal, terminal, prepare;
        private int samples, transitions;
        private bool stopped, disposed;
        private long probeTicks;

        public static CreatorPreviewReadinessObserver Begin(CharacterCreator target)
        {
            var root = new GameObject("Creator readiness observation (test only)") { hideFlags = HideFlags.DontSave };
            var observation = root.AddComponent<CreatorPreviewReadinessObserver>();
            observation.creator = target;
            observation.Sample("before-open");
            return observation;
        }

        private void Update()
        {
            if (!stopped) Sample("pre-update");
        }

        public void MarkPreparation()
        {
            if (prepare == null)
                prepare = "frame=" + Time.frameCount + "; preview=" + Identity(creator == null ? null : creator.StudioPreview)
                    + "; first-request-observer-stopped=" + stopped;
        }

        private void Sample(string phase)
        {
            long began = Stopwatch.GetTimestamp();
            try
            {
                if (samples >= MaximumSamples)
                {
                    terminal = "Observation cap reached; later state is unobserved.";
                    stopped = true;
                    return;
                }
                samples++;
                var preview = creator == null ? null : creator.StudioPreview;
                string state;
                double? elapsed = null;
                bool original = false;
                if (preview == null) state = "preview=missing";
                else
                {
                    object subjectValue = Read(preview, "Gamesim.Presentation.CharacterStudioPreview", "subject");
                    var body = subjectValue as GameObject;
                    if (body != observedBody)
                    {
                        observedBody = body;
                        data = tint = null;
                    }
                    // The installed avatar inherits UMAData; recognize its base type on this body's
                    // existing components without invoking the avatar's data or generator getters.
                    if (body != null && (data == null || tint == null))
                        foreach (var component in body.GetComponentsInChildren<Component>(true))
                        {
                            if (component == null) continue;
                            for (var type = component.GetType(); type != null; type = type.BaseType)
                                if (type.FullName == "UMA.UMAData") { data = component; break; }
                            if (component.GetType().FullName == "Gamesim.Uma.UmaBodyTint") tint = component;
                        }
                    var buildState = body == null ? null : body.GetComponent<CharacterBodyBuildState>();
                    object started = Read(preview, "Gamesim.Presentation.CharacterStudioPreview", "buildStartedAt");
                    if (started is double start && body != null)
                        elapsed = (Time.realtimeSinceStartupAsDouble - start) * 1000;
                    original = body != null && !preview.CanRetry && buildState != null;
                    state = "preview=" + Identity(preview)
                        + "; provider=" + (CharacterBodySource.Provider?.GetType().FullName ?? "none")
                        + "; previewActive=" + preview.isActiveAndEnabled + "; building=" + preview.IsBuilding
                        + "; retry=" + preview.CanRetry
                        + "; revision=" + Value(Read(preview, "Gamesim.Presentation.CharacterStudioPreview", "revision"))
                        + "; body=" + (ReferenceEquals(subjectValue, Unavailable) ? "<field-unavailable>" : Identity(body))
                        + "; bodyActive=" + (body != null && body.activeInHierarchy)
                        + "; stateRevision=" + (buildState == null ? "missing" : buildState.Revision.ToString(CultureInfo.InvariantCulture))
                        + "; stateReady=" + (buildState == null ? "missing" : buildState.Ready.ToString())
                        + "; renderers=" + (body == null ? 0 : body.GetComponentsInChildren<Renderer>().Length)
                        + "; tint=" + Identity(tint) + "; tintActive=" + (tint is Behaviour behaviour && behaviour.isActiveAndEnabled)
                        + "; proportionsApplied=" + Value(Read(tint, "Gamesim.Uma.UmaBodyTint", "proportionsApplied"))
                        + "; readyAfterFrame=" + Value(Read(tint, "Gamesim.Uma.UmaBodyTint", "readyAfterFrame"))
                        + "; fitPending=" + Value(Read(tint, "Gamesim.Uma.UmaBodyTint", "fitPending"))
                        + "; data=" + Identity(data)
                        + "; requestVersion=" + Value(Read(data, "UMA.UMAData", "generationRequestVersion"))
                        + "; dirtyMesh=" + Value(Read(data, "UMA.UMAData", "isMeshDirty"))
                        + "; dirtyShape=" + Value(Read(data, "UMA.UMAData", "isShapeDirty"))
                        + "; dirtyTexture=" + Value(Read(data, "UMA.UMAData", "isTextureDirty"))
                        + GeneratorState();
                }
                string sample = phase + "; frame=" + Time.frameCount
                    + "; buildElapsedMs=" + (elapsed?.ToString("F3", CultureInfo.InvariantCulture) ?? "unobserved")
                    + "; " + state;
                if (original) lastOriginal = sample;
                if (state != previousState)
                {
                    transitions++;
                    if (first.Count < HeadCount) first.Add(sample);
                    else
                    {
                        if (last.Count == TailCount) last.Dequeue();
                        last.Enqueue(sample);
                    }
                    previousState = state;
                }
                if (preview != null && (preview.CanRetry || (!preview.IsBuilding && preview.CompletedKey != null)))
                {
                    terminal = sample + "; exactOriginalFailure=[" + (preview.LastFailureDiagnostic ?? "none") + "]";
                    stopped = true;
                }
            }
            finally { probeTicks += Stopwatch.GetTimestamp() - began; }
        }

        private string GeneratorState()
        {
            if (data == null) return "; generator=unobserved-no-current-UMAData";
            // UMAData.umaGenerator and indexer.Instance/Generator can create/consolidate objects.
            // Read only the already-existing indexer and its cached generator used by that UMAData implementation.
            var indexerType = data.GetType().Assembly.GetType("UMA.UMAAssetIndexer", false);
            object indexer = ReadField(null, indexerType, "theIndexer");
            object generatorValue = Read(indexer, "UMA.UMAAssetIndexer", "generator");
            var generator = generatorValue as Behaviour;
            if (generator == null)
                return "; generator=" + (ReferenceEquals(generatorValue, Unavailable) ? "<field-unavailable>" : "cached-none");
            object queueValue = Read(indexer, "UMA.UMAAssetIndexer", "dirtyList");
            var queue = queueValue as IList;
            object active = Read(generator, "UMA.UMAGeneratorBuiltin", "activeMultiStepGeneration");
            bool knownActive = !ReferenceEquals(active, Unavailable);
            bool current = knownActive && active != null
                && ReferenceEquals(Read(active, "UMA.UMAGeneratorBuiltin+MultiStepGenerationState", "Data"), data);
            return "; generator=" + Identity(generator) + "; generatorActive=" + generator.isActiveAndEnabled
                + "; generatorSceneValid=" + (generator.gameObject.scene.IsValid() && generator.gameObject.scene.isLoaded)
                + "; queueCount=" + (queue == null ? "unavailable" : queue.Count.ToString(CultureInfo.InvariantCulture))
                + "; currentQueueIndex=" + (queue == null ? "unavailable" : queue.IndexOf(data).ToString(CultureInfo.InvariantCulture))
                + "; activeOperation=" + (!knownActive ? "unavailable" : active == null ? "none" : current ? "current-body" : "other-body")
                + "; currentRequestVersion=" + (current ? Value(Read(active, "UMA.UMAGeneratorBuiltin+MultiStepGenerationState", "RequestVersion")) : "not-current")
                + "; currentRestart=" + (current ? Value(Read(active, "UMA.UMAGeneratorBuiltin+MultiStepGenerationState", "RestartRequested")) : "not-current")
                + "; currentDiscard=" + (current ? Value(Read(active, "UMA.UMAGeneratorBuiltin+MultiStepGenerationState", "DiscardRequested")) : "not-current")
                + "; currentCancelled=" + (current ? Value(Read(active, "UMA.UMAGeneratorBuiltin+MultiStepGenerationState", "CancellationIssued")) : "not-current")
                + "; generatorLastStatus=" + Value(Read(generator, "UMA.UMAGeneratorBuiltin", "lastMultiStepStatus"))
                + "; generatorWaitCount=" + Value(Read(generator, "UMA.UMAGeneratorBuiltin", "multiStepWaitingForAsyncCount"))
                + "; generatorRestartCount=" + Value(Read(generator, "UMA.UMAGeneratorBuiltin", "multiStepRestartCount"))
                + "; generatorCancelCount=" + Value(Read(generator, "UMA.UMAGeneratorBuiltin", "multiStepCancellationCount"))
                + "; generatorFailureCount=" + Value(Read(generator, "UMA.UMAGeneratorBuiltin", "multiStepFailureCount"));
        }

        // Exact field names at fixed call sites only. No property getter, method invocation, or field enumeration.
        private object Read(object instance, string declaringType, string name)
        {
            if (instance == null || ReferenceEquals(instance, Unavailable)) return Unavailable;
            var type = instance.GetType();
            while (type != null && type.FullName != declaringType) type = type.BaseType;
            return ReadField(instance, type, name);
        }

        private object ReadField(object instance, Type declaringType, string name)
        {
            if (declaringType == null) return Unavailable;
            var key = Tuple.Create(declaringType, name);
            if (!fields.TryGetValue(key, out var field))
            {
                field = declaringType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                fields.Add(key, field);
            }
            return field == null ? Unavailable : field.GetValue(instance);
        }

        private static string Identity(Object value) => value == null ? "none"
            : value.GetType().FullName + "#" + value.GetEntityId().ToString();

        private static string Value(object value)
        {
            if (ReferenceEquals(value, Unavailable)) return "unavailable";
            if (value == null) return "none";
            return value is bool || value is int || value is uint || value is long || value.GetType().IsEnum
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : "<non-scalar>";
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = stopped = true;
            TestContext.WriteLine("Creator first-preview observation: samples=" + samples + "; transitions=" + transitions
                + "; retained=head4/tail4/last-original/terminal; probeMs="
                + (probeTicks * 1000d / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture)
                + "\n" + string.Join("\n", first) + "\n" + string.Join("\n", last)
                + "\nlast-original: " + (lastOriginal ?? "unobserved")
                + "\nterminal: " + (terminal ?? "not observed before scope ended")
                + "\nfirst-prepare: " + (prepare ?? "not reached")
                + "\nSampling gap: original UMA state may change after pre-update, or a whole build/fallback may occur"
                + " in one Update. Exact original generic failure state is LastFailureDiagnostic; generator counters"
                + " are shared totals and are not proof that this body was cancelled. No timing acceptance is inferred.");
            Destroy(gameObject);
        }
    }
}
