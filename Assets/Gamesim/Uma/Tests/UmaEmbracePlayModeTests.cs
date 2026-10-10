using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// Two UMA houseguests embrace (the art brief's R5): alex-chen and emma-brown, a man's body and a
    /// woman's, so the take is seen retargeted onto two different builds. Both start on the same
    /// frame, their heads go to opposite sides, each body comes home over its root within a
    /// millimetre when it is done, and the embrace lets go of both bodies at once when one of them
    /// is sent walking or motion is reduced. Photographed at the reach, the hold and the release
    /// into uma-embrace-*.png beside the project, for reading.
    /// </summary>
    public sealed class UmaEmbracePlayModeTests
    {
        private const int BuildTimeoutFrames = 900;
        private const float FrameTime = 1f / 60f;
        private GameObject cast, first, second;
        private CharacterPresentation a, b;

        [UnitySetUp]
        public IEnumerator BuildThePair()
        {
            Time.captureDeltaTime = FrameTime;
            cast = new GameObject("Gamesim UMA cast", typeof(GamesimUmaCast));
            yield return null;
            first = new GameObject("alex-chen");
            first.transform.SetPositionAndRotation(new Vector3(2000f, 0f, 2000f), Quaternion.identity);
            second = new GameObject("emma-brown");
            second.transform.SetPositionAndRotation(new Vector3(2000f, 0f, 2000.9f), Quaternion.Euler(0f, 180f, 0f));
            a = CharacterPresentation.Attach(first, Person("alex-chen"), Color.white);
            b = CharacterPresentation.Attach(second, Person("emma-brown"), Color.white);
            foreach (var body in new[] { a, b }) body.SetReducedMotion(false);
            b.SetFacing(180f);
            for (int frame = 0; frame < BuildTimeoutFrames && (a.IsBodyAssembling || b.IsBodyAssembling || !a.Supports(CharacterPresentation.Reaction.Embrace)
                     || !b.Supports(CharacterPresentation.Reaction.Embrace)); frame++) yield return null;
            Assert.That(a.IsBodyAssembling || b.IsBodyAssembling, Is.False, "Both UMA bodies finished assembling.");
            Assert.That(a.Supports(CharacterPresentation.Reaction.Embrace) && b.Supports(CharacterPresentation.Reaction.Embrace), Is.True,
                "Both bodies' controller has the embrace.");
            foreach (var animator in new[] { Animator(a), Animator(b) }) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int frame = 0; frame < 40; frame++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator DestroyThePair()
        {
            Time.captureDeltaTime = 0f;
            foreach (var pair in Object.FindObjectsByType<EmbracePair>(FindObjectsSortMode.None)) Object.Destroy(pair.gameObject);
            if (first != null) Object.Destroy(first);
            if (second != null) Object.Destroy(second);
            if (cast != null) Object.Destroy(cast);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoUmaBodiesEmbraceAndComeHome()
        {
            var headA = Animator(a).GetBoneTransform(HumanBodyBones.Head);
            var headB = Animator(b).GetBoneTransform(HumanBodyBones.Head);
            var pair = EmbracePair.TryBegin(a, b);
            Assert.That(pair, Is.Not.Null, EmbracePair.Refusal(a, b, null));
            int inA = -1, inB = -1;
            for (int frame = 0; frame < 120 && (inA < 0 || inB < 0); frame++)
            {
                yield return null;
                if (inA < 0 && a.IsReacting) inA = frame;
                if (inB < 0 && b.IsReacting) inB = frame;
            }
            Assert.That(inA, Is.GreaterThanOrEqualTo(0), "The embrace began.");
            Assert.That(inB, Is.EqualTo(inA), "Both bodies began it on the same frame.");

            yield return Until(a, 21f / 114f);
            if (Application.isBatchMode) Photograph("uma-embrace-reach");
            yield return Until(a, 40f / 114f);
            if (Application.isBatchMode) { Photograph("uma-embrace-hold"); Photograph("uma-embrace-hold-front", true); }
            // Each head turns to its own right and each body steps to its own right, which puts the
            // two heads on opposite sides of the pair's line.
            var line = (second.transform.position - first.transform.position); line.y = 0f; line.Normalize();
            var across = Vector3.Cross(Vector3.up, line);
            float sideA = Vector3.Dot(headA.position - first.transform.position, across);
            float sideB = Vector3.Dot(headB.position - second.transform.position, across);
            Debug.Log("[Gamesim] embrace heads across the pair's line: " + sideA.ToString("0.000") + " and " + sideB.ToString("0.000")
                + "; humanScale " + Animator(a).humanScale.ToString("0.000") + " and " + Animator(b).humanScale.ToString("0.000")
                + "; pair distance " + EmbracePairDistance().ToString("0.000")
                + "; head joints " + Vector3.Distance(headA.position, headB.position).ToString("0.000") + " apart");
            Assert.That(sideA * sideB, Is.LessThan(0f), "The heads go to opposite sides: " + sideA.ToString("0.000") + " and " + sideB.ToString("0.000"));
            Assert.That(Mathf.Abs(sideA - sideB), Is.GreaterThan(.15f), "and well apart.");
            yield return Until(a, 100f / 114f);
            if (Application.isBatchMode) Photograph("uma-embrace-release");

            for (int frame = 0; frame < 300 && pair != null && !pair.Released; frame++) yield return null;
            for (int frame = 0; frame < 40 && pair != null; frame++) yield return null;
            Assert.That(a.VisualRoot.localPosition.magnitude, Is.LessThan(.001f), "alex-chen is home over his root.");
            Assert.That(b.VisualRoot.localPosition.magnitude, Is.LessThan(.001f), "emma-brown is home over her root.");
        }

        [UnityTest]
        public IEnumerator SendingOneWalkingLetsBothGo()
        {
            var pair = EmbracePair.TryBegin(a, b);
            Assert.That(pair, Is.Not.Null, EmbracePair.Refusal(a, b, null));
            for (int frame = 0; frame < 120 && !(a.IsReacting && b.IsReacting); frame++) yield return null;
            yield return Until(a, 40f / 114f);
            int frames = 0;
            for (; frames < 21 && !pair.Released; frames++)
            {
                second.transform.position += Vector3.right * (2f * FrameTime);
                yield return null;
            }
            Assert.That(pair.Released, Is.True, "The embrace let go within 0.35 s of emma-brown being walked off (" + pair.ReleaseReason + ").");
            for (int frame = 0; frame < 30; frame++) yield return null;
            Assert.That(a.IsReacting, Is.False, "alex-chen stops embracing nobody,");
            Assert.That(a.VisualRoot.localPosition.magnitude, Is.LessThan(.001f), "and is home over his root.");
        }

        [UnityTest]
        public IEnumerator ReducedMotionLetsBothGo()
        {
            var pair = EmbracePair.TryBegin(a, b);
            Assert.That(pair, Is.Not.Null, EmbracePair.Refusal(a, b, null));
            for (int frame = 0; frame < 120 && !(a.IsReacting && b.IsReacting); frame++) yield return null;
            a.SetReducedMotion(true);
            int frames = 0;
            for (; frames < 21 && !pair.Released; frames++) yield return null;
            Assert.That(pair.Released, Is.True, "Reduced motion ends the embrace at once (" + pair.ReleaseReason + ").");
            for (int frame = 0; frame < 30; frame++) yield return null;
            Assert.That(b.IsReacting, Is.False);
            Assert.That(b.VisualRoot.localPosition.magnitude, Is.LessThan(.001f));
        }

        private static IEnumerator Until(CharacterPresentation body, float normalized)
        {
            var animator = Animator(body);
            for (int frame = 0; frame < 400; frame++)
            {
                var now = animator.GetCurrentAnimatorStateInfo(0);
                if (now.IsName("ReactEmbrace") && now.normalizedTime >= normalized) yield break;
                yield return null;
            }
        }

        private static Animator Animator(CharacterPresentation body) => body.VisualRoot.GetComponentInChildren<Animator>();

        private static ContestantState Person(string id)
        {
            var person = ContentCatalog.Create(1).contestants.First(contestant => !contestant.isPlayer);
            person.id = id;
            return person;
        }

        private static float EmbracePairDistance()
        {
            var pair = Object.FindObjectsByType<EmbracePair>(FindObjectsSortMode.None).FirstOrDefault();
            return pair != null ? pair.Distance : 0f;
        }

        /// <summary>The pair from the side (or over one shoulder, <paramref name="front"/>), at chest height: one PNG beside the project.</summary>
        private void Photograph(string name, bool front = false)
        {
            var light = new GameObject("Embrace light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -.6f, .3f));
            var camera = new GameObject("Embrace camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f, .2f, .24f);
            camera.fieldOfView = 34f;
            var middle = (first.transform.position + second.transform.position) * .5f + Vector3.up * 1.15f;
            camera.transform.position = middle + (front ? new Vector3(1.4f, .5f, -2.6f) : new Vector3(3.2f, .3f, -.6f));
            camera.transform.LookAt(middle);
            var target = new RenderTexture(640, 480, 24);
            var previous = RenderTexture.active;
            var shot = new Texture2D(640, 480, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                shot.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                shot.Apply();
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", name + ".png"));
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
                Debug.Log("[Gamesim] embrace capture -> " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(light.gameObject);
            }
        }
    }
}
