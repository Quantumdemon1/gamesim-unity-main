using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamesim.Episode
{
    /// <summary>
    /// The profile's performance evidence beyond its percentiles: the startup before the steady sample
    /// (bootstrap, the season and its bodies, the verification's own captures), memory through the
    /// sample, the raw frame times, and what the build and its settings were. The verdict itself is
    /// <see cref="VerificationPerformance.Verdict"/>; this only gathers what it and the reader need.
    /// </summary>
    public sealed partial class PortVerification
    {
        public const string RawFrameTimesName = "profile-frame-times-ms.csv", RawStartupFrameTimesName = "startup-frame-times-ms.csv";

        /// <summary>--gamesim-stress-roster: the house is the verification-only stress house (SeasonBuilder.CreateVerificationStressHouse).</summary>
        private bool stressRoster;

        // The startup, a stage at a time: every frame from the runner's first until the steady sample.
        private readonly List<string> startupStageNames = new List<string>();
        private readonly List<List<float>> startupStageFrames = new List<List<float>>();
        private int startupStage = -1;
        private double readySeconds = -1, sampleStartSeconds = -1, bodyAssemblySeconds = -1;
        private int bodiesAssemblingAtSampleStart = -1;
        private bool bodiesAssembled, openingWasPlaying;

        private readonly List<VerificationMemorySample> memorySamples = new List<VerificationMemorySample>();
        private VerificationMemorySample memoryAtSampleStart, memoryAtSampleEnd;
        private ProfilerRecorder systemMemory, gfxMemory;
        private double nextMemorySample;
        private string rawFrameTimesPath, rawStartupFrameTimesPath;

        /// <summary>Starts the next stage of the startup; its frames are counted from the next one.</summary>
        private void BeginStartupStage(string name)
        {
            startupStageNames.Add(name);
            startupStageFrames.Add(new List<float>());
            startupStage = startupStageNames.Count - 1;
        }

        private void Update()
        {
            if (startupStage >= 0 && startupStage < startupStageFrames.Count)
                startupStageFrames[startupStage].Add(Time.unscaledDeltaTime * 1000f);
        }

        private static int BodiesAssembling() => FindObjectsByType<CharacterPresentation>().Count(body => body.IsBodyAssembling);

        /// <summary>
        /// Puts the opening and the tutorial away and waits for every body to finish building, so the
        /// steady sample is the house at play and the construction is counted in the startup.
        /// </summary>
        private IEnumerator SettleTheHouse()
        {
            var opening = FindAnyObjectByType<OpeningSequence>();
            openingWasPlaying = opening != null && opening.IsPlaying;
            yield return SkipOpening();
            for (int i = 0; i < 5; i++) yield return null;
            yield return SkipOpening();
            BeginStartupStage("bodies");
            double started = Time.realtimeSinceStartupAsDouble;
            double deadline = started + CharacterPresentation.AssemblyTimeoutSeconds + 15;
            while (BodiesAssembling() > 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            bodyAssemblySeconds = Time.realtimeSinceStartupAsDouble - started;
            bodiesAssembled = BodiesAssembling() == 0;
            if (!bodiesAssembled) errors.Add("Bodies were still assembling " + bodyAssemblySeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s after the season started.");
        }

        /// <summary>The startup ends and the steady sample begins: memory read at its start, counters running.</summary>
        private void BeginSteadySample(double started)
        {
            startupStage = -1;
            sampleStartSeconds = started;
            bodiesAssemblingAtSampleStart = BodiesAssembling();
            if (bodiesAssemblingAtSampleStart > 0)
                errors.Add(bodiesAssemblingAtSampleStart + " bodies were still assembling when the steady sample began; their construction is inside it.");
            systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory", 1);
            gfxMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory", 1);
            memoryAtSampleStart = SampleMemory();
            memorySamples.Add(memoryAtSampleStart);
            nextMemorySample = started + 1;
        }

        /// <summary>Memory once a second through the sample, for its high-water marks.</summary>
        private void SampleMemoryDuringSteadySample(double now)
        {
            if (now < nextMemorySample) return;
            memorySamples.Add(SampleMemory());
            nextMemorySample = now + 1;
        }

        private void EndSteadySample()
        {
            memoryAtSampleEnd = SampleMemory();
            memorySamples.Add(memoryAtSampleEnd);
            DisposeMemoryRecorders();
        }

        private void DisposeMemoryRecorders() { systemMemory.Dispose(); gfxMemory.Dispose(); }

        private VerificationMemorySample SampleMemory() => new VerificationMemorySample
        {
            atSeconds = Time.realtimeSinceStartupAsDouble,
            totalAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
            totalReservedBytes = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(),
            monoUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
            monoHeapBytes = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong(),
            gfxDriverBytes = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver(),
            systemUsedBytes = systemMemory.Valid ? systemMemory.LastValue : -1,
            gfxUsedBytes = gfxMemory.Valid ? gfxMemory.LastValue : -1,
        };

        /// <summary>
        /// The raw distributions beside the report, in the order measured: one steady frame a line, and
        /// one startup frame a line with its stage. A failure to write is a report error, not a crash.
        /// </summary>
        private void WriteRawFrameTimes()
        {
            if (string.IsNullOrEmpty(outputDirectory)) return;
            try
            {
                var steady = new StringBuilder("ms\n");
                foreach (float ms in frames) steady.Append(ms.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                string path = Path.Combine(outputDirectory, RawFrameTimesName);
                File.WriteAllText(path, steady.ToString());
                rawFrameTimesPath = path;

                var startup = new StringBuilder("stage,ms\n");
                for (int stage = 0; stage < startupStageFrames.Count; stage++)
                    foreach (float ms in startupStageFrames[stage])
                        startup.Append(startupStageNames[stage]).Append(',').Append(ms.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                path = Path.Combine(outputDirectory, RawStartupFrameTimesName);
                File.WriteAllText(path, startup.ToString());
                rawStartupFrameTimesPath = path;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { errors.Add("The raw frame times could not be written: " + error.Message); }
        }

        /// <summary>The performance sections of the report: distribution, startup, memory, identity, settings and the verdict.</summary>
        private void CompletePerformanceReport(VerificationReport report, VerificationFrameSummary steady, double measured, bool graphical, bool batchMode)
        {
            report.steady = steady;
            report.frameMeanMs = steady.meanMs; report.frameMinMs = steady.minMs; report.frameMaxMs = steady.maxMs;
            report.frameP90Ms = steady.p90Ms; report.frameP999Ms = steady.p999Ms;
            report.slowFramesOver16_7Ms = steady.framesOver16_7Ms;
            report.slowFramesOver33_3Ms = steady.framesOver33_3Ms;
            report.slowFramesOver50Ms = steady.framesOver50Ms;
            report.averageFps = measured > 0 ? frames.Count / measured : 0;
            report.frameHistogramUpperMs = (float[])VerificationPerformance.HistogramUpperMs.Clone();
            report.frameHistogramCounts = VerificationPerformance.Histogram(frames);
            report.rawFrameTimesFile = rawFrameTimesPath;
            report.rawStartupFrameTimesFile = rawStartupFrameTimesPath;

            report.startupStages = startupStageNames.Select((name, i) => VerificationFrameSummary.Of(name, startupStageFrames[i])).ToList();
            report.startup = VerificationFrameSummary.Of("startup", startupStageFrames.SelectMany(stage => stage));
            report.startupReadySeconds = readySeconds;
            report.startupSampleBeganSeconds = sampleStartSeconds;
            report.bodyAssemblySeconds = bodyAssemblySeconds;
            report.bodiesAssembled = bodiesAssembled;
            report.bodiesAssemblingAtSampleStart = bodiesAssemblingAtSampleStart;
            report.openingSkipped = openingWasPlaying;

            report.memoryAtSampleStart = memoryAtSampleStart ?? new VerificationMemorySample();
            report.memoryAtSampleEnd = memoryAtSampleEnd ?? new VerificationMemorySample();
            report.memoryPeak = VerificationMemorySample.Peak(memorySamples);
            report.memorySamples = memorySamples.Count;
            report.gfxDriverMemoryAvailable = memorySamples.Any(sample => sample.gfxDriverBytes > 0);

            report.buildVersion = Application.version;
            report.buildGuid = Application.buildGUID;
            report.productName = Application.productName;
            int level = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            report.qualityLevel = level;
            report.qualityLevelName = level >= 0 && level < names.Length ? names[level] : "";
            var pipeline = GraphicsSettings.currentRenderPipeline;
            report.renderPipeline = pipeline != null ? pipeline.name : "Built-in";
            report.renderScale = pipeline is UniversalRenderPipelineAsset universal ? universal.renderScale : -1f;
            var display = Screen.currentResolution;
            report.displayResolution = display.width + "x" + display.height;
            report.displayRefreshHz = display.refreshRateRatio.value;
            report.graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString();
            report.graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion;
            report.graphicsDeviceVendor = SystemInfo.graphicsDeviceVendor;
            report.operatingSystem = SystemInfo.operatingSystem;
            report.processorCount = SystemInfo.processorCount;
            report.processorFrequencyMHz = SystemInfo.processorFrequency;
            report.stressRoster = stressRoster;

            var evidence = new VerificationPerformance.Evidence
            {
                Graphical = graphical, BatchMode = batchMode, MeasuredSeconds = measured,
                RequestedWidth = ProfileWidth, RequestedHeight = ProfileHeight,
                RequestedFrameCap = ProfileFrameCap, RequestedVSyncCount = ProfileVSyncCount, RequestedWindowed = true,
                Frames = frames.Count, DisplaySamples = profileDisplaySampleCount,
                ResolutionMismatches = profileResolutionMismatchCount, FrameCapMismatches = profileFrameCapMismatchCount,
                DisplayModeMismatches = profileDisplayModeMismatchCount,
                P95Ms = steady.p95Ms, P99Ms = steady.p99Ms,
            };
            report.performanceAcceptance = VerificationPerformance.Verdict(evidence, out string basis);
            report.performanceAcceptanceBasis = basis;
            report.performanceP95LimitMs = VerificationPerformance.P95LimitMs;
            report.performanceP99LimitMs = VerificationPerformance.P99LimitMs;
            report.performanceMinimumSeconds = VerificationPerformance.MinimumSeconds;
        }
    }
}
