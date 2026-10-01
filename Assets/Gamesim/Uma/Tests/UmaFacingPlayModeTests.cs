using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// Every state of the shipped cast's controller faces the way its body is pointed, and the walk
    /// goes forwards.
    ///
    /// <para>The walk played backwards: a houseguest heading for the kitchen moonwalked there. So
    /// did every talk, every seat and every reaction - they all faced away - and nobody saw it
    /// from the sitters, because a half-turn in the seat code had been added to cover for it. The
    /// cause was the import, "Based Upon: Original", on files that store a heading about 180
    /// degrees from Unity's forward; the run faced forward only because it is UMA's own.</para>
    ///
    /// <para>Measured on a real UMA body, one state at a time, with that state's own parameters
    /// held. Playing a state without them is how the first measurement of this went wrong: the
    /// controller leaves a state whose conditions are false inside a third of a second, so what was
    /// sampled as "Talk" was mostly the idle.</para>
    /// </summary>
    public sealed class UmaFacingPlayModeTests
    {
        private const int BuildTimeoutFrames = 900;
        private const float FrameTime = 1f / 60f;

        /// <summary>How far a body may turn from its root and still be facing forward: a talk leans and turns a little.</summary>
        private const float FacingTolerance = 25f;

        /// <summary>
        /// Every state, with the parameters that hold it. <c>activity</c> is the activity cue it
        /// needs, if any, and <c>variant</c> which dance or pose that cue plays; <c>lying</c> marks a
        /// body with no upright forward, which is asked where its head is instead of which way its
        /// hips face.
        /// </summary>
        private static readonly (string state, float speed, bool running, bool seated, bool talking,
            bool listening, bool arguing, bool oneShot, string activity, bool lying, int variant)[] States =
        {
            ("Idle", 0f, false, false, false, false, false, false, null, false, 0),
            ("Walk", 1f, false, false, false, false, false, false, null, false, 0),
            ("Run", 1f, true, false, false, false, false, false, null, false, 0),
            ("WalkStop", 0f, false, false, false, false, false, true, null, false, 0),
            ("SitIdle", 0f, false, true, false, false, false, false, null, false, 0),
            ("SitTalk", 0f, false, true, true, false, false, false, null, false, 0),
            // The talk take captured sitting, which the seated talk ring plays second (PACK8-PASS-PLAN A2).
            ("SitTalkB", 0f, false, true, true, false, false, false, null, false, 0),
            ("SitClap", 0f, false, true, false, false, false, true, null, false, 0),
            ("SitVictory", 0f, false, true, false, false, false, true, null, false, 0),
            ("Talk", 0f, false, false, true, false, false, false, null, false, 0),
            ("TalkB", 0f, false, false, true, false, false, false, null, false, 0),
            ("TalkC", 0f, false, false, true, false, false, false, null, false, 0),
            ("Listen", 0f, false, false, false, true, false, false, null, false, 0),
            ("Argue", 0f, false, false, false, false, true, false, null, false, 0),
            ("ReactWon", 0f, false, false, false, false, false, true, null, false, 0),
            ("ReactCheered", 0f, false, false, false, false, false, true, null, false, 0),
            ("ReactNominated", 0f, false, false, false, false, false, true, null, false, 0),
            ("ReactSaved", 0f, false, false, false, false, false, true, null, false, 0),
            ("ReactEvicted", 0f, false, false, false, false, false, true, null, false, 0),
            ("Cheer", 0f, false, false, false, false, false, true, null, false, 0),
            ("Shrug", 0f, false, false, false, false, false, true, null, false, 0),
            ("Celebrate", 0f, false, false, false, false, false, true, null, false, 0),
            ("Cook", 0f, false, false, false, false, false, false, "Cooking", false, 0),
            ("Dance", 0f, false, false, false, false, false, false, "Dancing", false, 0),
            ("DanceSamba", 0f, false, false, false, false, false, false, "Dancing", false, 1),
            ("DanceHipHop", 0f, false, false, false, false, false, false, "Dancing", false, 2),
            ("DanceWave", 0f, false, false, false, false, false, false, "Dancing", false, 3),
            ("PoseHandBehindHead", 0f, false, false, false, false, false, false, "Posing", false, 0),
            ("PoseFootUp", 0f, false, false, false, false, false, false, "Posing", false, 1),
            ("PoseOverShoulder", 0f, false, false, false, false, false, false, "Posing", false, 2),
            ("PoseAtEase", 0f, false, false, false, false, false, false, "Posing", false, 3),
            ("PoseHandOnHip", 0f, false, false, false, false, false, false, "Posing", false, 4),
            ("PoseHandOnHipGlance", 0f, false, false, false, false, false, false, "Posing", false, 5),
            ("PosePowerStance", 0f, false, false, false, false, false, false, "Posing", false, 6),
            ("SwimIdle", 0f, false, false, false, false, false, false, "Swimming", false, 0),
            ("SwimForward", 1f, false, false, false, false, false, false, "Swimming", true, 0),
            ("Sleep", 0f, false, false, false, false, false, false, "Sleeping", true, 0),
        };

        private static readonly string[] Activities = { "Sleeping", "Swimming", "Cooking", "Dancing", "Posing" };

        private GameObject cast, actor;
        private Animator animator;
        private Transform root;

        [UnitySetUp]
        public IEnumerator BuildABody()
        {
            Time.captureDeltaTime = FrameTime;
            cast = new GameObject("Gamesim UMA cast", typeof(GamesimUmaCast));
            actor = new GameObject("UMA houseguest");
            yield return null;
            var provider = (IModularCharacterBodyProvider)CharacterBodySource.Provider;
            var appearance = provider.Catalog.Materialize(CharacterAppearance.Preset("player"));
            Assert.That(provider.TryCreate(new CharacterBodyRequest("player", "player", appearance,
                CharacterBuildPurpose.Studio, 1), actor.transform, Color.magenta, out var body), Is.True);
            var build = body.Root.GetComponent<CharacterBodyBuildState>();
            for (int frame = 0; frame < BuildTimeoutFrames && !build.Ready; frame++) yield return null;
            Assert.That(build.Ready, Is.True, "The UMA body never finished building.");
            animator = body.Root.GetComponentInChildren<Animator>();
            Assert.That(animator != null && animator.isHuman, Is.True, "The body has a Humanoid animator.");
            // Batchmode has no camera looking at the body, and a culled animator stands still.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            root = animator.transform;
        }

        [UnityTearDown]
        public IEnumerator DestroyCast()
        {
            Time.captureDeltaTime = 0f;
            if (actor != null) Object.Destroy(actor);
            if (cast != null) Object.Destroy(cast);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryStateFacesTheWayItsBodyIsPointed()
        {
            var wrong = new List<string>();
            foreach (var entry in States)
            {
                // A dance is measured from where the show starts it (CastMoves.Lively): the samba
                // swings through a whole turn over its eighteen seconds, so "which way does it face"
                // is only a question about the moment somebody is looking - the mark, the landed
                // introduction - and that moment is the one asked about.
                float? from = entry.activity == "Dancing" ? CastMoves.Lively((CharacterPresentation.DanceStyle)entry.variant) : (float?)null;
                Hold(entry.state, entry.speed, entry.running, entry.seated, entry.talking, entry.listening, entry.arguing, entry.oneShot,
                    entry.activity, entry.variant, from);
                var hips = Vector3.zero; var shoulders = Vector3.zero; var spine = Vector3.zero; int held = 0;
                for (int frame = 0; frame < 42; frame++)
                {
                    yield return null;
                    if (frame < 12) continue;
                    if (animator.GetCurrentAnimatorStateInfo(0).IsName(entry.state)) held++;
                    hips += Facing(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg);
                    shoulders += Facing(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm);
                    spine += Local(HumanBodyBones.Head) - Local(HumanBodyBones.Hips);
                }
                Assert.That(held, Is.EqualTo(30), entry.state + " was not the state playing while it was measured, "
                    + "so its measurement would be of something else.");
                if (entry.lying)
                {
                    // Lying down or face down in the water, the body has no forward of its own: the
                    // head is ahead of the hips along the root's forward, so whoever lays it on a
                    // bed or sends it up the pool turns the root and the head goes with it.
                    var axis = spine / 30f;
                    float along = Vector3.Dot(axis.normalized, Vector3.forward);
                    if (along < .75f)
                        wrong.Add(entry.state + " (head " + axis.ToString("0.00") + " from the hips; it should lie head-forward)");
                }
                else
                {
                    float hipYaw = Yaw(hips), shoulderYaw = Yaw(shoulders);
                    if (Mathf.Abs(hipYaw) > FacingTolerance || Mathf.Abs(shoulderYaw) > FacingTolerance)
                        wrong.Add(entry.state + " (hips " + hipYaw.ToString("0") + ", shoulders " + shoulderYaw.ToString("0") + " degrees)");
                }
                if (Application.isBatchMode) Photograph(entry.state, entry.lying);
            }
            if (sheet != null) SaveSheet();
            Assert.That(wrong, Is.Empty, "These states face away from the way the body is pointed: " + string.Join("; ", wrong));
        }

        // ------------------------------------------------------------------ the contact sheet

        /// <summary>
        /// Every state photographed from in front of the body, one tile each, into
        /// <c>uma-facing.png</c> beside the project. The numbers above describe bones; a take that
        /// turned the mesh and not the skeleton - or the reverse - would pass them and still show a
        /// back to the room. A face in every tile is the thing being claimed.
        /// </summary>
        private Texture2D sheet;
        private const int TileWidth = 200, TileHeight = 300, Columns = 8;

        private void Photograph(string state, bool lying)
        {
            int index = System.Array.FindIndex(States, entry => entry.state == state);
            int rows = (States.Length + Columns - 1) / Columns;
            if (sheet == null) sheet = new Texture2D(TileWidth * Columns, TileHeight * rows, TextureFormat.RGB24, false);
            var light = new GameObject("Contact sheet light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.LookRotation(-root.forward + Vector3.down * .6f);
            var camera = new GameObject("Contact sheet camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f, .2f, .24f);
            camera.fieldOfView = 34f;
            // A lying body from above and to its side, head to the top of the tile.
            var chest = root.position + Vector3.up * (lying ? .3f : 1.0f);
            camera.transform.position = lying ? chest + root.right * 2.2f + Vector3.up * 2.6f : chest + root.forward * 3.6f + Vector3.up * .25f;
            camera.transform.LookAt(chest, lying ? root.forward : Vector3.up);
            var target = new RenderTexture(TileWidth, TileHeight, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                int sheetRows = sheet.height / TileHeight;
                sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), (index % Columns) * TileWidth,
                    (sheetRows - 1 - index / Columns) * TileHeight);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(light.gameObject);
            }
        }

        private void SaveSheet()
        {
            sheet.Apply();
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "uma-facing.png"));
            System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            Debug.Log("[Gamesim] UMA facing contact sheet -> " + path);
            Object.DestroyImmediate(sheet);
            sheet = null;
        }

        /// <summary>
        /// A walk that faces the right way and plays in reverse would pass the test above, so this
        /// asks the feet. With the root fixed in place, a foot on the ground slides backwards under
        /// a body walking forwards - and forwards under one walking backwards.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingAndRunningPushTheGroundBackwards()
        {
            foreach (var (state, running) in new[] { ("Walk", false), ("Run", true) })
            {
                Hold(state, 1f, running, false, false, false, false, false, null);
                for (int frame = 0; frame < 12; frame++) yield return null;
                var left = new List<Vector3>(); var right = new List<Vector3>();
                for (int frame = 0; frame < 90; frame++)
                {
                    yield return null;
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, state + " stopped playing.");
                    left.Add(Local(HumanBodyBones.LeftFoot)); right.Add(Local(HumanBodyBones.RightFoot));
                }
                float slide = PlantedSlide(left, right);
                // The take's own ground speed: what CharacterPresentation's pace divides by.
                Debug.Log("[Gamesim] " + state + " planted foot moves " + slide.ToString("0.00") + " m/s");
                Assert.That(slide, Is.LessThan(-.4f), state + "'s planted foot moves at " + slide.ToString("0.00")
                    + " m/s along the body's forward. It has to go backwards under a body that walks forwards.");
            }
        }

        private void Hold(string state, float speed, bool running, bool seated, bool talking, bool listening, bool arguing, bool oneShot,
            string activity, int variant = 0, float? from = null)
        {
            animator.SetFloat("Speed", speed); animator.SetBool("Running", running); animator.SetBool("Seated", seated);
            animator.SetBool("Talking", talking); animator.SetBool("Listening", listening); animator.SetBool("Arguing", arguing);
            foreach (var cue in Activities) animator.SetBool(cue, cue == activity);
            // Which dance, which pose: without it the controller's own edge takes a dancing body
            // straight to the dance that is style nought, and the state asked for is never measured.
            animator.SetInteger("DanceStyle", variant); animator.SetInteger("Pose", variant);
            animator.Play(Animator.StringToHash(state), 0, from ?? (oneShot ? .05f : .1f));
        }

        private Vector3 Local(HumanBodyBones bone) => root.InverseTransformPoint(animator.GetBoneTransform(bone).position);

        /// <summary>Which way a pair of joints faces, on the ground plane of the root: forward is +z.</summary>
        private Vector3 Facing(HumanBodyBones leftBone, HumanBodyBones rightBone)
        {
            var across = Local(rightBone) - Local(leftBone); across.y = 0f;
            return Vector3.Cross(across, Vector3.up).normalized;
        }

        private static float Yaw(Vector3 facing) => Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;

        /// <summary>
        /// The mean speed along the root's forward of whichever foot is on the ground. A foot is on
        /// the ground while it is within three centimetres of the lowest either foot gets.
        /// </summary>
        private static float PlantedSlide(List<Vector3> left, List<Vector3> right)
        {
            float floor = left.Concat(right).Min(point => point.y);
            float travelled = 0f; int steps = 0;
            foreach (var foot in new[] { left, right })
                for (int frame = 1; frame < foot.Count; frame++)
                    if (foot[frame].y < floor + .03f && foot[frame - 1].y < floor + .03f)
                    { travelled += foot[frame].z - foot[frame - 1].z; steps++; }
            Assert.That(steps, Is.GreaterThan(10), "No foot stayed on the ground long enough to measure.");
            return travelled / (steps * FrameTime);
        }
    }
}
