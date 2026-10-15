using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    /// <summary>Provided UMA take/ground/material evidence only. The real ceremony remains its separate staged-exit gate.
    /// The IK feasibility and Idle diagnostic probes live in Gamesim.Uma.DiagnosticsPlayModeTests (GAMESIM_DIAGNOSTICS).</summary>
    public sealed class UmaGroundedFarewellPlayModeTests
    {
        private GameObject cast, first, second, ground;
        private Material groundMaterial;
        private CharacterPresentation a, b;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            cast = new GameObject("Grounded farewell UMA cast", typeof(GamesimUmaCast));
            yield return null;
            first = new GameObject("Farewell alex-chen");
            second = new GameObject("Farewell emma-brown");
            first.transform.position = new Vector3(2000f, 0f, 2000f);
            second.transform.position = first.transform.position + Vector3.right * 3.34f;
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube); // Test-only actual floor plane, not a cast/body or production prop stand-in.
            ground.name = "Farewell observed floor";
            ground.transform.position = (first.transform.position + second.transform.position) * .5f - Vector3.up * .005f;
            ground.transform.localScale = new Vector3(7f, .01f, 3f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            groundMaterial = new Material(shader);
            groundMaterial.color = new Color(.25f, .27f, .3f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            a = CharacterPresentation.Attach(first, Person("alex-chen"), Color.white);
            b = CharacterPresentation.Attach(second, Person("emma-brown"), Color.white);
            a.SetReducedMotion(false); b.SetReducedMotion(false);
            float by = Time.realtimeSinceStartup + 30f;
            for (int frame = 0; frame < 900 && Time.realtimeSinceStartup < by; frame++)
            {
                if (a.TryReadFarewell(out var left) && left.Idle && b.TryReadFarewell(out var right) && right.Idle) break;
                yield return null;
            }
            Assert.That(a.TryReadFarewell(out var ar) && ar.Idle && b.TryReadFarewell(out var br) && br.Idle, Is.True,
                "Actual complete provided UMA bodies, not primitives, with the new imported exact motion.");
            foreach (var body in new[] { a, b })
            {
                var renderers = body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
                Assert.That(renderers.Any(r => r.enabled && r.sharedMesh != null && r.sharedMaterials.Length > 0
                    && r.sharedMaterials.All(m => m != null && m.shader != null)), Is.True, "Actual material-bearing UMA renderers.");
                body.VisualRoot.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            Debug.Log("[Gamesim] observed farewell body frames: " + a.Frame + " / " + b.Frame + "; no inferred gender/build provenance.");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (first != null) Object.Destroy(first);
            if (second != null) Object.Destroy(second);
            if (cast != null) Object.Destroy(cast);
            if (ground != null) Object.Destroy(ground);
            if (groundMaterial != null) Object.Destroy(groundMaterial);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothUmaBodiesGiveTheGroundedGestureAndReturnToIdle()
        {
            Assert.That(a.TryReadFarewell(out var baseA), Is.True);
            Assert.That(b.TryReadFarewell(out var baseB), Is.True);
            int enteredA = -1, enteredB = -1, lifted = 0;
            bool neutralA = false, neutralB = false, completed = false, photographed = false;
            a.React(CharacterPresentation.Reaction.Farewell); b.React(CharacterPresentation.Reaction.Farewell);
            float by = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < by)
            {
                yield return null;
                bool readA = a.TryReadFarewell(out var left), readB = b.TryReadFarewell(out var right);
                Assert.That(readA, Is.True, readA ? "" : FarewellNativeDiagnostic(a));
                Assert.That(readB, Is.True, readB ? "" : FarewellNativeDiagnostic(b));
                AssertObservedFarewell(a, left); AssertObservedFarewell(b, right);
                Planted(a, baseA, left); Planted(b, baseB, right);
                if (left.InTake && enteredA < 0) enteredA = Time.frameCount;
                if (right.InTake && enteredB < 0) enteredB = Time.frameCount;
                neutralA |= left.InTake && left.NormalizedTime >= CharacterPresentation.FarewellNeutral;
                neutralB |= right.InTake && right.NormalizedTime >= CharacterPresentation.FarewellNeutral;
                if (left.InTake && right.InTake && left.NormalizedTime >= 10f / 30f && left.NormalizedTime <= 17f / 30f
                    && right.NormalizedTime >= 10f / 30f && right.NormalizedTime <= 17f / 30f
                    && left.RightHand.y > baseA.RightHand.y + .03f && right.RightHand.y > baseB.RightHand.y + .03f)
                {
                    lifted++;
                    if (Application.isBatchMode && !photographed)
                    {
                        Photograph("front", new Vector3(0f, .5f, 6f));
                        Photograph("side-threequarter", new Vector3(5f, .5f, 5f));
                        Photograph("rear", new Vector3(0f, .5f, -6f));
                        photographed = true;
                    }
                }
                if (enteredA >= 0 && enteredB >= 0 && left.Idle && right.Idle) { completed = true; break; }
            }
            Assert.That(enteredA, Is.GreaterThanOrEqualTo(0));
            Assert.That(enteredB, Is.EqualTo(enteredA));
            Assert.That(neutralA && neutralB && completed, Is.True);
            Assert.That(lifted, Is.GreaterThan(0), "Real animated palms, no contact claim.");
            if (Application.isBatchMode) Assert.That(photographed, Is.True, "Three actual material-bearing native views were retained.");
        }

        [UnityTest]
        public IEnumerator CancelledUmaTakesDoNotRestartFromIdle()
        {
            a.React(CharacterPresentation.Reaction.Farewell); b.React(CharacterPresentation.Reaction.Farewell);
            float by = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < by)
            {
                if (a.TryReadFarewell(out var left) && left.InTake && b.TryReadFarewell(out var right) && right.InTake) break;
                yield return null;
            }
            Assert.That(a.TryReadFarewell(out var ar) && ar.InTake && b.TryReadFarewell(out var br) && br.InTake, Is.True);
            a.CancelReaction(); b.CancelReaction(); // Public playback cancellation, NOT ceremony-owner cancellation proof.
            by = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < by)
            {
                if (a.TryReadFarewell(out var left) && left.Idle && b.TryReadFarewell(out var right) && right.Idle) break;
                yield return null;
            }
            Assert.That(a.TryReadFarewell(out var ai) && ai.Idle && b.TryReadFarewell(out var bi) && bi.Idle, Is.True);
            by = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < by)
            {
                yield return null;
                Assert.That(a.TryReadFarewell(out var left) && !left.InTake && b.TryReadFarewell(out var right) && !right.InTake, Is.True);
            }
        }

        private static void AssertObservedFarewell(CharacterPresentation body, CharacterPresentation.FarewellObservation pose)
        {
            var live = body.VisualRoot.GetComponentInChildren<Animator>();
            Assert.That(live, Is.Not.Null);
            bool transition = live.IsInTransition(0);
            var current = live.GetCurrentAnimatorStateInfo(0);
            var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
            if (pose.Boundary != CharacterPresentation.FarewellBoundary.None)
            {
                Assert.That(pose.InTake || pose.Idle, Is.False, "A zero-contribution boundary is neither playback nor settled Idle.");
                Assert.That(pose.SelectedClipWeight, Is.EqualTo(0f));
                Assert.That(transition, Is.True);
                bool incoming = pose.Boundary == CharacterPresentation.FarewellBoundary.IncomingZeroWeight;
                Assert.That(incoming
                    ? current.IsName("Idle") && next.IsName(CharacterPresentation.FarewellState)
                    : pose.Boundary == CharacterPresentation.FarewellBoundary.OutgoingZeroWeight
                        && current.IsName(CharacterPresentation.FarewellState) && next.IsName("Idle"), Is.True);
                var selected = incoming ? live.GetNextAnimatorClipInfo(0) : live.GetCurrentAnimatorClipInfo(0);
                var other = incoming ? live.GetCurrentAnimatorClipInfo(0) : live.GetNextAnimatorClipInfo(0);
                Assert.That(selected.Length, Is.EqualTo(1));
                var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
                Assert.That(handle, Is.Not.Null);
                var sourceMotion = handle.runtimeAnimatorController.animationClips.Single(c => c.name == CharacterPresentation.FarewellTake);
                Assert.That(selected[0].clip, Is.SameAs(sourceMotion));
                Assert.That(selected[0].weight, Is.EqualTo(0f));
                Assert.That(other.Length, Is.EqualTo(1));
                Assert.That(other[0].clip, Is.Not.Null);
                Assert.That(other[0].weight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            }
            else if (pose.InTake)
            {
                Assert.That(pose.Idle, Is.False);
                Assert.That(pose.SelectedClipWeight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                var selected = current.IsName(CharacterPresentation.FarewellState)
                    ? live.GetCurrentAnimatorClipInfo(0) : live.GetNextAnimatorClipInfo(0);
                Assert.That(selected.Length, Is.EqualTo(1));
                Assert.That(selected[0].clip.name, Is.EqualTo(CharacterPresentation.FarewellTake));
                Assert.That(selected[0].weight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            }
            else
            {
                Assert.That(pose.Idle && !transition && current.IsName("Idle"), Is.True);
                var selected = live.GetCurrentAnimatorClipInfo(0);
                Assert.That(selected.Length, Is.EqualTo(1));
                Assert.That(selected[0].clip, Is.Not.Null);
                Assert.That(selected[0].weight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                Assert.That(pose.SelectedClipWeight, Is.EqualTo(selected[0].weight));
            }
        }

        private static string FarewellNativeDiagnostic(CharacterPresentation body)
        {
            var report = new System.Text.StringBuilder(512);
            string Float(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            string Name(Object value) => value == null ? "null" : value.name.Length <= 64 ? value.name : value.name.Substring(0, 64);
            try
            {
                report.Append("frame=").Append(Time.frameCount).Append(" delta=").Append(Float(Time.deltaTime));
                if (body == null) return report.Append(" body=null").ToString();
                report.Append(" id=").Append(body.CharacterId).Append(" frameKind=").Append(body.Frame)
                    .Append(" primitive=").Append(body.IsPrimitiveBody).Append(" building=").Append(body.IsBodyAssembling)
                    .Append(" changing=").Append(body.IsChangingOutfit).Append(" still=").Append(body.IsStill)
                    .Append(" seated=").Append(body.IsSeated).Append(" activity=").Append(body.Activity);
                var visual = body.VisualRoot;
                var live = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
                if (live == null) return report.Append(" animator=null").ToString();
                report.Append(" animator=").Append(live.GetEntityId().ToString()).Append(" enabled=").Append(live.isActiveAndEnabled)
                    .Append(" initialized=").Append(live.isInitialized).Append(" human=").Append(live.isHuman)
                    .Append(" controller=").Append(Name(live.runtimeAnimatorController)).Append("#")
                    .Append(live.runtimeAnimatorController != null ? live.runtimeAnimatorController.GetEntityId().ToString() : "0")
                    .Append(" avatar=").Append(Name(live.avatar)).Append("#").Append(live.avatar != null ? live.avatar.GetEntityId().ToString() : "0");
                if (live.isInitialized && live.runtimeAnimatorController != null)
                {
                    var current = live.GetCurrentAnimatorStateInfo(0);
                    bool transition = live.IsInTransition(0);
                    var next = transition ? live.GetNextAnimatorStateInfo(0) : default;
                    report.Append(" current=").Append(current.fullPathHash).Append("/").Append(Float(current.normalizedTime))
                        .Append(" currentIdle=").Append(current.IsName("Idle")).Append(" currentFarewell=").Append(current.IsName(CharacterPresentation.FarewellState))
                        .Append(" transition=").Append(transition).Append(" next=").Append(next.fullPathHash).Append("/").Append(Float(next.normalizedTime))
                        .Append(" nextIdle=").Append(next.IsName("Idle")).Append(" nextFarewell=").Append(next.IsName(CharacterPresentation.FarewellState));
                    void Clips(string label, AnimatorClipInfo[] rows)
                    {
                        report.Append(" ").Append(label).Append("Count=").Append(rows.Length);
                        for (int i = 0; i < rows.Length && i < 4; i++)
                            report.Append(" [").Append(Name(rows[i].clip)).Append("#")
                                .Append(rows[i].clip != null ? rows[i].clip.GetEntityId().ToString() : "0").Append(" weight=").Append(Float(rows[i].weight)).Append("]");
                        if (rows.Length > 4) report.Append(" [remaining clips omitted]");
                    }
                    Clips("currentClip", live.GetCurrentAnimatorClipInfo(0));
                    if (transition) Clips("nextClip", live.GetNextAnimatorClipInfo(0));
                }
            }
            catch (System.Exception error) { report.Append(" diagnostic unavailable: ").Append(error.GetType().Name); }
            return report.Length <= 2048 ? report.ToString() : report.ToString(0, 2048);
        }

        private static ContestantState Person(string id)
        {
            var person = ContentCatalog.Create(1).contestants.First(p => !p.isPlayer);
            person.id = id;
            return person;
        }

        private static void Planted(CharacterPresentation body, CharacterPresentation.FarewellObservation baseline,
            CharacterPresentation.FarewellObservation pose)
        {
            float root = Vector3.Distance(baseline.Root, pose.Root);
            Assert.That(root, Is.LessThanOrEqualTo(FarewellPair.RootTolerance), root <= FarewellPair.RootTolerance ? ""
                : PlantedDiagnostic(body, "Root", baseline.Root, pose.Root, root, pose));
            float heading = Quaternion.Angle(baseline.Heading, pose.Heading);
            Assert.That(heading, Is.LessThanOrEqualTo(FarewellPair.HeadingTolerance), heading <= FarewellPair.HeadingTolerance ? ""
                : PlantedDiagnostic(body, "Heading", baseline.Root, pose.Root, heading, pose,
                    " baselineHeading=" + baseline.Heading.ToString("R") + " actualHeading=" + pose.Heading.ToString("R")));
            var before = new[] { baseline.LeftFoot, baseline.RightFoot, baseline.LeftToes, baseline.RightToes };
            var now = new[] { pose.LeftFoot, pose.RightFoot, pose.LeftToes, pose.RightToes };
            var names = new[] { "LeftFoot", "RightFoot", "LeftToes", "RightToes" };
            var gaps = new[] { Vector3.Distance(baseline.LeftFoot, pose.LeftFoot), Vector3.Distance(baseline.RightFoot, pose.RightFoot),
                Vector3.Distance(baseline.LeftToes, pose.LeftToes), Vector3.Distance(baseline.RightToes, pose.RightToes) };
            for (int i = 0; i < before.Length; i++)
            {
                float gap = gaps[i];
                Assert.That(gap, Is.LessThanOrEqualTo(FarewellPair.FootTolerance), gap <= FarewellPair.FootTolerance ? ""
                    : PlantedDiagnostic(body, names[i], before[i], now[i], gap, pose));
            }
        }

        private static string PlantedDiagnostic(CharacterPresentation body, string landmark, Vector3 baseline, Vector3 actual,
            float gap, CharacterPresentation.FarewellObservation pose, string additional = "")
        {
            var report = "Actual pre-trigger Idle baseline; actor=" + (body != null ? body.CharacterId : "null")
                + " landmark=" + landmark + " baseline=" + baseline.ToString("R") + " actual=" + actual.ToString("R")
                + " gap=" + gap.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " frame=" + Time.frameCount + " inTake=" + pose.InTake + " idle=" + pose.Idle + " boundary=" + pose.Boundary
                + " progress=" + pose.NormalizedTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " selectedWeight=" + pose.SelectedClipWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + additional + " " + FarewellNativeDiagnostic(body);
            return report.Length <= 2048 ? report : report.Substring(0, 2048);
        }

        private void Photograph(string view, Vector3 eye)
        {
            var light = new GameObject("Farewell capture light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -.6f, .3f));
            var camera = new GameObject("Farewell capture camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f, .2f, .24f); camera.fieldOfView = 40f;
            var middle = (first.transform.position + second.transform.position) * .5f + Vector3.up * 1f;
            camera.transform.position = middle + eye; camera.transform.LookAt(middle);
            var target = new RenderTexture(960, 720, 24);
            var previous = RenderTexture.active;
            var shot = new Texture2D(960, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                shot.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); shot.Apply();
                byte[] bytes = shot.EncodeToPNG();
                Assert.That(bytes.Length, Is.InRange(1, 8 * 1024 * 1024));
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "uma-grounded-farewell-" + view + "-" + Guid.NewGuid().ToString("N") + ".png"));
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes, 0, bytes.Length);
                Debug.Log("[Gamesim] actual NONCONTACT farewell capture -> " + path);
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(target); Object.DestroyImmediate(shot);
                Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject);
            }
        }
    }
}
