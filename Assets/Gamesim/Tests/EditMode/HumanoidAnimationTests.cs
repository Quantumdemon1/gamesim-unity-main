using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Editor;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The mocap takes for the cast that ships (MASTER-PLAN §4.5): twelve Humanoid clips, one to a
    /// file, and the controller <c>HumanoidClipWiring</c> builds from them so a UMA body finally
    /// sits, talks, argues and reacts instead of dropping every cue but <c>Speed</c>.
    ///
    /// <para>The last test is the one that matters most. UMA is a gigabyte this repository does not
    /// track, so a clone without it must still open the project: the committed controller may name
    /// nothing outside <c>Assets/Gamesim</c>, and the standing idle and the walk it therefore cannot
    /// have are borrowed at runtime instead, by name, through the asset indexer.</para>
    /// </summary>
    public sealed class HumanoidAnimationTests
    {
        private static AnimatorController Controller() =>
            AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanoidClipWiring.Controller);

        private static AnimatorStateMachine Machine(AnimatorController controller) =>
            controller.layers[0].stateMachine;

        private static AnimatorState State(AnimatorController controller, string name) =>
            Machine(controller).states.Select(s => s.state).FirstOrDefault(s => s.name == name);

        private static bool Leads(AnimatorState from, AnimatorState to, params string[] parameters) =>
            from.transitions.Any(t => t.destinationState == to
                && parameters.All(p => t.conditions.Any(c => c.parameter == p)));

        [Test]
        public void TheMocapTakesImportAsHumanoidAndTheLoopsLoop()
        {
            // Twenty-seven: the twelve mocap takes, the walk and its stop - the walk is the one that
            // mattered, because without an authored take the Walk state was a key for UMA to fill,
            // and UMA's Locomotion controller has no walk, so it filled it with a run - then three
            // dances, three seated takes and the seven standing stills the living poses come from.
            Assert.That(HumanoidClipWiring.Takes.Length, Is.EqualTo(27), "twenty-seven takes arrived");
            foreach (var take in HumanoidClipWiring.Takes)
            {
                var path = HumanoidClipWiring.Path(take);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.That(importer, Is.Not.Null, path + " is one mocap take, named by its file");
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human),
                    take + " is retargeted onto whatever UMA builds, so it imports as Human");
                Assert.That(importer.importAnimation, Is.True, take);
                Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel),
                    take + " carries the avatar it was captured on");

                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal)).ToArray();
                Assert.That(clips.Length, Is.EqualTo(1), path + " holds one take");
                Assert.That(clips[0].name, Is.EqualTo(take),
                    "the clip takes the file's name, not the service the mocap came from");
                Assert.That(clips[0].isLooping, Is.EqualTo(take.EndsWith("_loop", StringComparison.Ordinal)),
                    take + (take.EndsWith("_loop", StringComparison.Ordinal) ? " loops" : " is a one-shot"));
            }
        }

        /// <summary>
        /// Every upright take is turned to face the way its body faces, and the authored reactions
        /// carry no heading of their own.
        ///
        /// <para>The walk played backwards. The takes were imported "Based Upon: Original", which
        /// keeps the heading stored in the file, and the Mixamo files store one about 180 degrees
        /// from Unity's forward; the four authored clips copied the talk take's first frame, heading
        /// and all. Held here three ways: the rule the importer applies, the metas it wrote, and the
        /// root curves of the clips authored from the talk take.</para>
        /// </summary>
        [Test]
        public void EveryUprightTakeFacesTheWayItsBodyFaces()
        {
            foreach (var take in HumanoidClipWiring.Takes)
            {
                bool horizontal = AuthoredAssetImporter.HorizontalTakes.ContainsKey(take);
                var rule = new ModelImporterClipAnimation { name = take, keepOriginalOrientation = true, rotationOffset = 90f };
                AuthoredAssetImporter.ApplyTakeRules(rule, true);
                Assert.That(rule.keepOriginalOrientation, Is.EqualTo(horizontal), take + (horizontal
                    ? " lies down, so it has no body forward to turn to and keeps the file's heading"
                    : " is imported Based Upon Body Orientation - Original is the file's heading, which faces away"));
                Assert.That(rule.lockRootRotation, Is.True, take + " bakes its rotation into the pose");
                if (!horizontal) Assert.That(rule.rotationOffset, Is.Zero, take + " is turned by its body, not by a number");

                var written = ((ModelImporter)AssetImporter.GetAtPath(HumanoidClipWiring.Path(take))).clipAnimations;
                Assert.That(written.Length, Is.EqualTo(1), take);
                Assert.That(written[0].keepOriginalOrientation, Is.EqualTo(rule.keepOriginalOrientation),
                    take + "'s meta was written by an older rule - force-reimport the Humanoid folder");
                Assert.That(written[0].lockRootRotation, Is.True, take);
                Assert.That(written[0].rotationOffset, Is.EqualTo(rule.rotationOffset).Within(.01f),
                    take + "'s meta carries another trim offset than the table - force-reimport it");
            }

            var blender = new ModelImporterClipAnimation { name = "Walk_loop" };
            AuthoredAssetImporter.ApplyTakeRules(blender, false);
            Assert.That(blender.keepOriginalOrientation, Is.True,
                "the Blender takes' own export decides where they face; this rule is for the mocap rig");

            foreach (var take in HumanoidReactionAuthoring.Takes)
            {
                var path = HumanoidClipWiring.ClipFolder + "bb_anim_" + take + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.That(clip, Is.Not.Null, path);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                Assert.That(settings.keepOriginalOrientation, Is.False, take + " turns with its body");
                Assert.That(settings.loopBlendOrientation, Is.True, take + " bakes its rotation into the pose");

                float Value(string attribute) => AnimationUtility.GetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("", typeof(Animator), attribute)).Evaluate(0f);
                var body = new Quaternion(Value("RootQ.x"), Value("RootQ.y"), Value("RootQ.z"), Value("RootQ.w"));
                var ahead = body * Vector3.forward; ahead.y = 0f;
                float heading = Vector3.SignedAngle(Vector3.forward, ahead, Vector3.up);
                Assert.That(Mathf.Abs(heading), Is.LessThan(2f),
                    take + " keeps its source's lean and loses its turn; it was built facing " + heading + " degrees round");
                Assert.That(Value("RootT.x"), Is.Zero, take + " stands where its root is");
                Assert.That(Value("RootT.z"), Is.Zero, take + " stands where its root is");
            }
        }

        /// <summary>
        /// A take that walks keeps its travel off the body: its horizontal motion is root motion,
        /// which nothing applies, so the body stays where the agent stands. Baked into the pose like
        /// every other take's, the stop take carried each houseguest 0.8 m past where they had
        /// stopped - through the opening's front door while it was still shut - held them there,
        /// then snapped them back as it gave way to idle. Held two ways: the rule the importer
        /// applies, and the metas it wrote.
        /// </summary>
        [Test]
        public void ATakeThatWalksKeepsItsTravelOffTheBody()
        {
            Assert.That(AuthoredAssetImporter.TravellingTakes, Does.Contain("WalkStop"), "the stop take walks two steps as it stops");
            Assert.That(AuthoredAssetImporter.TravellingTakes, Does.Contain("DanceHipHop_loop"),
                "the hip-hop dance carries its hips 1.49 m forward a cycle and starts each one back where it began");
            Assert.That(AuthoredAssetImporter.TravellingTakes, Does.Contain("Walk_loop"),
                "the walk was never in place: its hips travel 1.78 m a cycle, and baked into the pose every body lurched ahead of its agent each second");
            foreach (var take in HumanoidClipWiring.Takes)
            {
                bool walks = AuthoredAssetImporter.TravellingTakes.Contains(take);
                var rule = new ModelImporterClipAnimation { name = take, lockRootPositionXZ = walks };
                AuthoredAssetImporter.ApplyTakeRules(rule, true);
                Assert.That(rule.lockRootPositionXZ, Is.EqualTo(!walks), take + (walks
                    ? " walks, so its travel is root motion rather than baked into where the body is drawn"
                    : " stays over its root, so its motion is baked into the pose"));
                var written = ((ModelImporter)AssetImporter.GetAtPath(HumanoidClipWiring.Path(take))).clipAnimations;
                Assert.That(written[0].lockRootPositionXZ, Is.EqualTo(rule.lockRootPositionXZ),
                    take + "'s meta was written by an older rule - force-reimport it");
            }
        }

        /// <summary>
        /// The library's clips arrive cut from their takes under the same rule as the mocap, and
        /// the activities they play start from anywhere and stop when their cue goes.
        /// </summary>
        [Test]
        public void TheLibraryClipsArriveAndEveryActivityPlaysFromAnyState()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(AuthoredAssetImporter.LibraryPath);
            Assert.That(importer, Is.Not.Null, AuthoredAssetImporter.LibraryPath);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human), "the library is retargeted like the mocap");
            var cut = importer.clipAnimations;
            Assert.That(cut.Select(c => c.name), Is.EqualTo(AuthoredAssetImporter.LibraryClips.Select(c => c.clip)),
                "exactly the clips the house uses, in the table's order - force-reimport the library after changing the table");
            foreach (var (name, take, loop) in AuthoredAssetImporter.LibraryClips)
            {
                var clip = cut.Single(c => c.name == name);
                Assert.That(clip.takeName, Does.EndWith(take), name + " is cut from " + take);
                Assert.That(clip.loopTime, Is.EqualTo(loop), name + (loop ? " loops" : " plays once"));
                Assert.That(clip.keepOriginalOrientation, Is.EqualTo(AuthoredAssetImporter.HorizontalTakes.ContainsKey(name)),
                    name + " is turned by its body unless it lies down");
                Assert.That(clip.rotationOffset, Is.EqualTo(AuthoredAssetImporter.HorizontalTakes.TryGetValue(name, out var trim) ? trim : 0f)
                    .Within(.01f), name + " carries the table's trim offset");
                Assert.That(clip.lockRootRotation && clip.lockRootPositionXZ && clip.lockRootHeightY, Is.True,
                    name + " stays where its root is");
                var imported = AssetDatabase.LoadAllAssetsAtPath(AuthoredAssetImporter.LibraryPath).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name == name);
                Assert.That(imported != null && imported.humanMotion, Is.True, name + " imported as Humanoid motion");
            }

            // The stand-ins UMA overrides are the library's idle and jog, not the sleep takes: an
            // override replaces a clip everywhere, and the sleep take now plays the Sleep state.
            Assert.That(new[] { HumanoidClipWiring.IdleStandIn, HumanoidClipWiring.RunStandIn },
                Is.SubsetOf(AuthoredAssetImporter.LibraryClips.Select(c => c.clip)));

            var controller = Controller();
            var machine = Machine(controller);
            var idle = State(controller, "Idle");
            foreach (var cue in HumanoidClipWiring.ActivityParameters)
                Assert.That(controller.parameters.Any(p => p.name == cue && p.type == AnimatorControllerParameterType.Bool), Is.True,
                    cue + " is declared, so CharacterPresentation drives it");
            foreach (var (stateName, cue) in new[] { ("Sleep", HumanoidClipWiring.SleepingParameter),
                         ("SwimIdle", HumanoidClipWiring.SwimmingParameter), ("SwimForward", HumanoidClipWiring.SwimmingParameter),
                         ("Cook", HumanoidClipWiring.CookingParameter), ("Dance", HumanoidClipWiring.DancingParameter) })
            {
                var state = State(controller, stateName);
                Assert.That(state, Is.Not.Null, stateName);
                var any = machine.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
                Assert.That(any, Is.Not.Null, stateName + " starts from whatever the body was doing");
                Assert.That(any.conditions.Any(c => c.parameter == cue && c.mode == AnimatorConditionMode.If), Is.True, stateName + " on " + cue);
                Assert.That(any.canTransitionToSelf, Is.False, stateName + " does not restart itself every frame its cue holds");
                Assert.That(state.transitions.Any(t => t.destinationState == idle
                    && t.conditions.Any(c => c.parameter == cue && c.mode == AnimatorConditionMode.IfNot)), Is.True,
                    stateName + " ends when " + cue + " goes");
            }
            foreach (var (trigger, stateName) in HumanoidClipWiring.WiredReactions)
            {
                var any = machine.anyStateTransitions.First(t => t.destinationState == State(controller, stateName));
                foreach (var cue in HumanoidClipWiring.ActivityParameters)
                    Assert.That(any.conditions.Any(c => c.parameter == cue && c.mode == AnimatorConditionMode.IfNot), Is.True,
                        stateName + " waits while the body is " + cue.ToLowerInvariant());
            }
        }

        /// <summary>
        /// The walk and the run play at the body's pace, so the feet keep up with the floor: a take
        /// that covers 1.7 m a second on a body moving 2.2 skates the difference.
        /// </summary>
        [Test]
        public void TheWalkAndTheRunPlayAtTheBodysPace()
        {
            var controller = Controller();
            var pace = controller.parameters.SingleOrDefault(p => p.name == HumanoidClipWiring.PaceParameter);
            Assert.That(pace, Is.Not.Null, "The controller declares Pace.");
            Assert.That(pace.type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(pace.defaultFloat, Is.EqualTo(1f), "Unset, a take plays as captured - not frozen at nought.");
            foreach (var state in Machine(controller).states.Select(s => s.state))
            {
                bool paced = state.name == "Walk" || state.name == "Run";
                Assert.That(state.speedParameterActive, Is.EqualTo(paced), state.name + (paced ? " follows the body's pace" : " plays at its own"));
                if (paced) Assert.That(state.speedParameter, Is.EqualTo(HumanoidClipWiring.PaceParameter), state.name);
            }
        }

        [Test]
        public void TheControllerDeclaresEveryCueThePresentationDrives()
        {
            var controller = Controller();
            Assert.That(controller, Is.Not.Null,
                HumanoidClipWiring.Controller + " is built by Gamesim > U07 > Wire the Humanoid takes");

            void Declares(string name, AnimatorControllerParameterType type) =>
                Assert.That(controller.parameters.Any(p => p.name == name && p.type == type), Is.True,
                    name + " is driven by CharacterPresentation, so the controller must declare it");

            Declares(HumanoidClipWiring.SpeedParameter, AnimatorControllerParameterType.Float);
            Declares(HumanoidClipWiring.SeatedParameter, AnimatorControllerParameterType.Bool);
            Declares(HumanoidClipWiring.TalkingParameter, AnimatorControllerParameterType.Bool);
            Declares(HumanoidClipWiring.ListeningParameter, AnimatorControllerParameterType.Bool);
            Declares(HumanoidClipWiring.ArguingParameter, AnimatorControllerParameterType.Bool);
            Declares(HumanoidClipWiring.RunningParameter, AnimatorControllerParameterType.Bool);

            // The reaction triggers are indexed by CharacterPresentation.Reaction, so a beat added
            // there is a beat the controller is asked for. Rows with no take carry no state, and the
            // controller must NOT declare those, because an undeclared trigger is how the
            // presentation knows to leave the body in its idle instead of playing a wrong take.
            var beats = Enum.GetNames(typeof(CharacterPresentation.Reaction));
            Assert.That(HumanoidClipWiring.Reactions.Length, Is.EqualTo(beats.Length),
                "one row per ceremony beat, whether or not a take exists for it");
            for (int i = 0; i < beats.Length; i++)
            {
                Assert.That(HumanoidClipWiring.Reactions[i].trigger, Is.EqualTo("React" + beats[i]));
                if (HumanoidClipWiring.Reactions[i].state != null)
                    Declares(HumanoidClipWiring.Reactions[i].trigger, AnimatorControllerParameterType.Trigger);
                else
                    Assert.That(controller.parameters.Any(p => p.name == HumanoidClipWiring.Reactions[i].trigger),
                        Is.False, HumanoidClipWiring.Reactions[i].trigger + " has no take on this rig, so the "
                        + "controller must not claim it: CharacterPresentation skips a cue the controller "
                        + "does not declare, and that is what keeps the body still rather than wrong");
            }
            Assert.That(HumanoidClipWiring.WiredReactions.Select(r => r.trigger).ToArray(),
                Is.EqualTo(new[] { "ReactNominated", "ReactSaved", "ReactEvicted", "ReactWon", "ReactCheered" }),
                "every beat now has a distinct authored or captured motion");
            Assert.That(HumanoidClipWiring.Reactions.Single(r => r.trigger == "ReactSaved").state,
                Is.EqualTo("ReactSaved"));
            Assert.That(HumanoidClipWiring.Reactions.Single(r => r.trigger == "ReactNominated").state,
                Is.EqualTo("ReactNominated"));
        }

        [Test]
        public void AuthoredReactionsHaveHumanoidMuscleMotionAndListeningHasATransition()
        {
            foreach (string take in HumanoidReactionAuthoring.Takes)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HumanoidClipWiring.Path(take));
                Assert.That(clip, Is.Not.Null, take);
                Assert.That(clip.humanMotion, Is.True, take);
                Assert.That(clip.isLooping, Is.EqualTo(take.EndsWith("_loop", StringComparison.Ordinal)));
                var muscle = AnimationUtility.GetCurveBindings(clip).Single(b => b.propertyName == "Head Nod Down-Up");
                var curve = AnimationUtility.GetEditorCurve(clip, muscle);
                Assert.That(Math.Abs(curve.Evaluate(clip.length * .28f) - curve.Evaluate(0)), Is.GreaterThan(.02f), take);
                Assert.That(curve.Evaluate(clip.length), Is.EqualTo(curve.Evaluate(0)).Within(.001f), take);
            }
            var controller = Controller();
            Assert.That(Leads(State(controller, "Idle"), State(controller, "Listen"), HumanoidClipWiring.ListeningParameter), Is.True);
            Assert.That(Leads(State(controller, "Listen"), State(controller, "Talk"), HumanoidClipWiring.TalkingParameter), Is.True);
        }

        [Test]
        public void EveryStateTheControllerCanReachHasAMotion()
        {
            var controller = Controller();
            var machine = Machine(controller);
            var states = machine.states.Select(s => s.state).ToArray();
            Assert.That(states.Length, Is.EqualTo(HumanoidClipWiring.States.Length));

            foreach (var (name, take, _x, _y) in HumanoidClipWiring.States)
            {
                var state = State(controller, name);
                Assert.That(state, Is.Not.Null, name);
                Assert.That(state.motion, Is.Not.Null, name + " has a take to play");
                var expected = HumanoidReactionAuthoring.Takes.Contains(take) || HumanoidPoseAuthoring.Takes.Contains(take)
                    ? "bb_anim_" + take : take;
                Assert.That(state.motion.name, Is.EqualTo(expected), name + " plays " + take);
                Assert.That(AssetDatabase.GetAssetPath(state.motion), Is.EqualTo(HumanoidClipWiring.Path(take)),
                    name + " references the authored motion for this beat");
            }

            // Reachable: the default state, anything an Any State transition fires, and anything
            // another state leads to. A state nothing can reach is a state that never plays.
            Assert.That(machine.defaultState, Is.SameAs(State(controller, "Idle")), "a body starts standing");
            foreach (var state in states)
            {
                bool reachable = state == machine.defaultState
                    || machine.anyStateTransitions.Any(t => t.destinationState == state)
                    || states.Any(other => other != state && other.transitions.Any(t => t.destinationState == state));
                Assert.That(reachable, Is.True, state.name + " is reachable");
            }
        }

        [Test]
        public void TheControllerHasTheShapeTheGenericOneHas()
        {
            var controller = Controller();
            var machine = Machine(controller);
            var idle = State(controller, "Idle");
            var walk = State(controller, "Walk");
            var sitIdle = State(controller, "SitIdle");
            var sitTalk = State(controller, "SitTalk");
            var argue = State(controller, "Argue");

            var walkStop = State(controller, "WalkStop");
            Assert.That(Leads(idle, walk, HumanoidClipWiring.SpeedParameter), Is.True, "idle walks");
            // Walking no longer snaps to idle. There is a stop take now, so a body that runs out of
            // path plants its weight and settles instead of teleporting into a standing pose - which
            // is what a walk cycle ending on whichever frame the agent happened to arrive on looked
            // like. The route is two edges, and both have to exist for the walk to be able to end.
            Assert.That(Leads(walk, walkStop, HumanoidClipWiring.SpeedParameter), Is.True, "and plants");
            var settles = walkStop.transitions.FirstOrDefault(t => t.destinationState == idle);
            Assert.That(settles, Is.Not.Null, "and settles into idle");
            Assert.That(settles.hasExitTime, Is.True, "after the stop take has played, not on a parameter");
            // Nothing waits three seconds to be told to move again: every reason to walk or run
            // beats the settle, which is why the settle is the only unconditional edge out.
            Assert.That(Leads(walkStop, walk, HumanoidClipWiring.SpeedParameter), Is.True,
                "a body asked to move again during the stop goes, rather than finishing the take");
            // No sit-down or stand-up take on this rig, so seating is a cross-fade, not a transition clip.
            Assert.That(Leads(idle, sitIdle, HumanoidClipWiring.SeatedParameter), Is.True, "idle sits");
            Assert.That(Leads(sitIdle, idle, HumanoidClipWiring.SeatedParameter), Is.True, "and stands up");
            Assert.That(Leads(sitIdle, sitTalk, HumanoidClipWiring.TalkingParameter), Is.True, "a seated body talks");
            Assert.That(Leads(sitTalk, sitIdle, HumanoidClipWiring.TalkingParameter), Is.True, "and stops");

            // The standing takes, played in a ring on exit time, so a long conversation varies
            // instead of looping the same gesture.
            for (int i = 0; i < HumanoidClipWiring.TalkRing.Length; i++)
            {
                var here = State(controller, HumanoidClipWiring.TalkRing[i]);
                var next = State(controller, HumanoidClipWiring.TalkRing[(i + 1) % HumanoidClipWiring.TalkRing.Length]);
                Assert.That(here, Is.Not.Null, HumanoidClipWiring.TalkRing[i]);
                var ring = here.transitions.FirstOrDefault(t => t.destinationState == next);
                Assert.That(ring, Is.Not.Null, here.name + " hands the floor to " + next.name);
                Assert.That(ring.hasExitTime, Is.True, here.name + " plays its take out first");
                Assert.That(Leads(here, idle, HumanoidClipWiring.TalkingParameter), Is.True, here.name + " stops talking");
                Assert.That(Leads(here, walk, HumanoidClipWiring.SpeedParameter), Is.True, "walking wins over talking");
                Assert.That(Leads(here, sitIdle, HumanoidClipWiring.SeatedParameter), Is.True, "and so does sitting");
                Assert.That(Leads(here, argue, HumanoidClipWiring.ArguingParameter), Is.True, "a row can start mid-sentence");
            }
            Assert.That(Leads(idle, State(controller, "Talk"), HumanoidClipWiring.TalkingParameter), Is.True, "idle talks");
            Assert.That(Leads(idle, argue, HumanoidClipWiring.ArguingParameter), Is.True, "idle argues");
            Assert.That(Leads(argue, idle, HumanoidClipWiring.ArguingParameter), Is.True, "and calms down");
            Assert.That(Leads(argue, walk, HumanoidClipWiring.SpeedParameter), Is.True, "or walks away");

            foreach (var (trigger, stateName) in HumanoidClipWiring.WiredReactions)
            {
                var state = State(controller, stateName);
                var any = machine.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
                Assert.That(any, Is.Not.Null, stateName + " plays from any state");
                Assert.That(any.conditions.Any(c => c.parameter == trigger), Is.True, stateName + " on its trigger");
                Assert.That(any.conditions.Any(c => c.parameter == HumanoidClipWiring.SeatedParameter
                    && c.mode == AnimatorConditionMode.IfNot), Is.True, stateName + " only while standing");
                Assert.That(state.transitions.Any(t => t.destinationState == idle && t.hasExitTime), Is.True,
                    stateName + " returns to idle");
            }
        }

        /// <summary>
        /// Every state stands unless it is a seat, and every seat sits (PACK8-PASS-PLAN A2).
        ///
        /// <para>The standing talk played a take captured sitting, and the listen and the three
        /// ceremony reactions were built on its first frame, so every standing conversation and
        /// every standing nominee sat down on nothing: "sitting in the middle of the room". Nothing
        /// looked at what a clip did with the body, only at which clip a state named. This reads
        /// each state's own curves: the root's height (RootT.y, the body's centre in its own
        /// heights) averaged over the clip, and how straight its lower legs are. A standing state
        /// keeps its root high and straightens a leg at some point; a seat keeps its root low and
        /// a knee bent. The bed and the pool lie and float, and are neither.</para>
        /// </summary>
        [Test]
        public void EveryStateStandsUnlessItIsASeat()
        {
            var controller = Controller();
            var machine = Machine(controller);
            var lying = machine.anyStateTransitions.Where(t => t.conditions.Any(c => c.mode == AnimatorConditionMode.If
                    && (c.parameter == HumanoidClipWiring.SleepingParameter || c.parameter == HumanoidClipWiring.SwimmingParameter)))
                .Select(t => t.destinationState.name).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.That(lying, Is.EqualTo(new[] { "Sleep", "SwimForward", "SwimIdle" }),
                "Only the bed and the pool are let off standing or sitting.");

            var wrong = new System.Collections.Generic.List<string>();
            int seats = 0, standing = 0;
            foreach (var state in machine.states.Select(s => s.state))
            {
                if (lying.Contains(state.name)) continue;
                var clip = state.motion as AnimationClip;
                if (clip == null || !clip.humanMotion) { wrong.Add(state.name + " plays no Humanoid clip"); continue; }
                if (!Stance(clip, out float height, out float straightest, out float bent))
                { wrong.Add(state.name + " (" + clip.name + ") has no root height or lower-leg curves"); continue; }
                string measured = state.name + " (" + clip.name + ": root " + height.ToString("0.000")
                    + ", straightest lower leg " + straightest.ToString("0.00") + ", most bent " + bent.ToString("0.00") + ")";
                if (state.name.StartsWith("Sit", StringComparison.Ordinal))
                {
                    seats++;
                    if (height >= HumanoidClipWiring.StandingRootHeight || bent >= HumanoidClipWiring.StraightLowerLeg)
                        wrong.Add(measured + " is a seat that does not sit");
                }
                else
                {
                    standing++;
                    if (height < HumanoidClipWiring.StandingRootHeight || straightest < HumanoidClipWiring.StraightLowerLeg)
                        wrong.Add(measured + " is not a seat and does not stand");
                }
            }
            Assert.That(wrong, Is.Empty, "The controller at " + HumanoidClipWiring.Controller + " needs regenerating "
                + "(-executeMethod Gamesim.Editor.HumanoidReactionAuthoring.BuildFromCommandLine) or a take is wired "
                + "to the wrong kind of state:\n" + string.Join("\n", wrong));
            Assert.That(seats, Is.EqualTo(5), "SitIdle, the two seated talks, the clap and the fist pump are measured as seats.");
            Assert.That(standing, Is.GreaterThan(20), "and everything else as standing.");
        }

        /// <summary>
        /// The talk take captured sitting talks only from a seat, as the seated ring's second take;
        /// the standing ring is the two standing takes; and the standing talk's way in keeps its
        /// name, plays the ring's first take and hands the floor to its second (PACK8-PASS-PLAN A2).
        /// </summary>
        [Test]
        public void TheTalkCapturedSittingIsPlayedOnlyFromASeat()
        {
            Assert.That(HumanoidClipWiring.States.Where(s => s.take == "Talk_loop").Select(s => s.state).ToArray(),
                Is.EqualTo(new[] { "SitTalkB" }), "Talk_loop was captured sitting: only a seat plays it.");
            Assert.That(HumanoidClipWiring.TalkRing, Is.EqualTo(new[] { "TalkB", "TalkC" }), "The standing ring is the two standing takes.");
            Assert.That(HumanoidClipWiring.SeatedTalkRing, Is.EqualTo(new[] { "SitTalk", "SitTalkB" }));
            Assert.That(HumanoidClipWiring.TalkEntry, Is.EqualTo("Talk"), "Every standing cue asks for the way in by this name.");
            Assert.That(HumanoidClipWiring.States.Single(s => s.state == HumanoidClipWiring.TalkEntry).take,
                Is.EqualTo(HumanoidClipWiring.States.Single(s => s.state == HumanoidClipWiring.TalkRing[0]).take),
                "The way in plays the ring's first take.");
            Assert.That(HumanoidPoseAuthoring.Takes, Does.Contain(HumanoidReactionAuthoring.Source),
                "The listen and the reactions are built on a standing living pose.");

            var controller = Controller();
            var idle = State(controller, "Idle");
            var sitIdle = State(controller, "SitIdle");
            var entry = State(controller, HumanoidClipWiring.TalkEntry);
            var second = State(controller, HumanoidClipWiring.TalkRing[1]);
            var handsOn = entry.transitions.FirstOrDefault(t => t.destinationState == second);
            Assert.That(handsOn, Is.Not.Null, "The way in hands the floor to the ring's second take.");
            Assert.That(handsOn.hasExitTime && handsOn.conditions.Any(c => c.parameter == HumanoidClipWiring.TalkingParameter), Is.True,
                "after its take has played, while the body still talks.");
            Assert.That(Leads(entry, idle, HumanoidClipWiring.TalkingParameter), Is.True, "The way in stops talking,");
            Assert.That(Leads(entry, State(controller, "Walk"), HumanoidClipWiring.SpeedParameter), Is.True, "walks,");
            Assert.That(Leads(entry, sitIdle, HumanoidClipWiring.SeatedParameter), Is.True, "sits");
            Assert.That(Leads(entry, State(controller, "Argue"), HumanoidClipWiring.ArguingParameter), Is.True, "and rows as the ring does.");

            var seatedRing = HumanoidClipWiring.SeatedTalkRing.Select(name => State(controller, name)).ToArray();
            Assert.That(seatedRing, Has.None.Null, "Every seated talk is a state of the controller - regenerate it "
                + "(-executeMethod Gamesim.Editor.HumanoidReactionAuthoring.BuildFromCommandLine).");
            Assert.That(Leads(sitIdle, seatedRing[0], HumanoidClipWiring.TalkingParameter), Is.True,
                "A seated body starts talking with the seated talk.");
            for (int i = 0; i < seatedRing.Length; i++)
            {
                var here = seatedRing[i];
                var next = seatedRing[(i + 1) % seatedRing.Length];
                var ring = here.transitions.FirstOrDefault(t => t.destinationState == next);
                Assert.That(ring, Is.Not.Null, here.name + " hands the floor to " + next.name);
                Assert.That(ring.hasExitTime, Is.True, here.name + " plays its take out first");
                Assert.That(Leads(here, sitIdle, HumanoidClipWiring.TalkingParameter), Is.True, here.name + " stops talking in the seat");
                Assert.That(Leads(here, idle, HumanoidClipWiring.SeatedParameter), Is.True, here.name + " stands up with the body");
                Assert.That(Leads(here, State(controller, "SitClap"), HumanoidClipWiring.SeatedClapTrigger), Is.True, here.name + " claps from the seat");
                Assert.That(Leads(here, State(controller, "SitVictory"), HumanoidClipWiring.SeatedVictoryTrigger), Is.True, here.name + " pumps a fist from the seat");
            }
        }

        /// <summary>
        /// A clip's stance: its root height averaged over the clip, the straightest either lower leg
        /// gets at any moment, and the more bent lower leg averaged over the clip. Read from the
        /// Humanoid curves the clip carries, as the reactions' authoring reads them.
        /// </summary>
        private static bool Stance(AnimationClip clip, out float height, out float straightest, out float bent)
        {
            height = 0f; straightest = float.NegativeInfinity; bent = 0f;
            var bindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Animator)).ToArray();
            AnimationCurve Curve(string property)
            {
                foreach (var binding in bindings)
                    if (binding.propertyName == property) return AnimationUtility.GetEditorCurve(clip, binding);
                return null;
            }
            var root = Curve("RootT.y");
            var left = Curve("Left Lower Leg Stretch");
            var right = Curve("Right Lower Leg Stretch");
            if (root == null || left == null || right == null) return false;
            const int Samples = 24;
            for (int i = 0; i < Samples; i++)
            {
                float time = clip.length * i / (Samples - 1);
                float l = left.Evaluate(time), r = right.Evaluate(time);
                height += root.Evaluate(time) / Samples;
                straightest = Mathf.Max(straightest, Mathf.Max(l, r));
                bent += Mathf.Min(l, r) / Samples;
            }
            return true;
        }

        /// <summary>
        /// A dancing body dances the style it is asked for and a posing body holds the pose it is
        /// numbered: each from Any State, each tagged, each let go when its cue goes. The lists the
        /// presentation counts in and the lists the controller is built from are the same lists in
        /// the same order, because the presentation sends a number and the controller reads it.
        /// </summary>
        [Test]
        public void EveryDanceIsItsStyleAndEveryPoseItsNumber()
        {
            var controller = Controller();
            var machine = Machine(controller);
            var idle = State(controller, "Idle");
            void Declares(string name, AnimatorControllerParameterType type) =>
                Assert.That(controller.parameters.Any(p => p.name == name && p.type == type), Is.True, name + " is declared as " + type);
            Declares(HumanoidClipWiring.DanceStyleParameter, AnimatorControllerParameterType.Int);
            Declares(HumanoidClipWiring.DanceOffsetParameter, AnimatorControllerParameterType.Float);
            Declares(HumanoidClipWiring.PoseParameter, AnimatorControllerParameterType.Int);
            Declares(HumanoidClipWiring.PosingParameter, AnimatorControllerParameterType.Bool);

            var styles = Enum.GetNames(typeof(CharacterPresentation.DanceStyle));
            Assert.That(styles.Length, Is.EqualTo(HumanoidClipWiring.Dances.Length), "a dance for every style the presentation can ask for");
            Assert.That(HumanoidClipWiring.Dances[0].take, Is.EqualTo("Dance_Loop"),
                "style nought is the library's dance, so a body nobody gives a style dances as it always did");
            for (int style = 0; style < styles.Length; style++)
            {
                var dance = State(controller, HumanoidClipWiring.Dances[style].state);
                Assert.That(dance, Is.Not.Null, styles[style]);
                Assert.That(dance.tag, Is.EqualTo(HumanoidClipWiring.DanceTag), dance.name + " is tagged a dance");
                Assert.That(dance.cycleOffsetParameterActive && dance.cycleOffsetParameter == HumanoidClipWiring.DanceOffsetParameter,
                    Is.True, dance.name + " starts where it is told to");
                var any = machine.anyStateTransitions.Where(t => t.destinationState == dance).ToArray();
                Assert.That(any.Length, Is.EqualTo(1), dance.name + " has one way in");
                Assert.That(any[0].conditions.Any(c => c.parameter == HumanoidClipWiring.DancingParameter && c.mode == AnimatorConditionMode.If)
                    && any[0].conditions.Any(c => c.parameter == HumanoidClipWiring.DanceStyleParameter
                        && c.mode == AnimatorConditionMode.Equals && Mathf.Approximately(c.threshold, style)), Is.True,
                    dance.name + " plays on Dancing with the style " + style + " (" + styles[style] + ")");
                Assert.That(any[0].canTransitionToSelf, Is.False, dance.name + " does not restart itself every frame");
                Assert.That(dance.transitions.Any(t => t.destinationState == idle && t.conditions.Any(c =>
                    c.parameter == HumanoidClipWiring.DancingParameter && c.mode == AnimatorConditionMode.IfNot)), Is.True,
                    dance.name + " ends when the dancing does");
            }

            var poses = Enum.GetNames(typeof(CharacterPresentation.Pose));
            Assert.That(poses.Length, Is.EqualTo(HumanoidPoseAuthoring.Poses.Length), "a living pose for every pose the presentation can ask for");
            for (int number = 0; number < poses.Length; number++)
            {
                string stateName = HumanoidClipWiring.PoseState(HumanoidPoseAuthoring.Poses[number].take);
                Assert.That(stateName, Is.EqualTo("Pose" + poses[number]), "pose " + number + " is " + poses[number] + " in both lists");
                var held = State(controller, stateName);
                Assert.That(held, Is.Not.Null, stateName);
                Assert.That(held.tag, Is.EqualTo(HumanoidClipWiring.PoseTag), stateName);
                var any = machine.anyStateTransitions.Where(t => t.destinationState == held).ToArray();
                Assert.That(any.Length, Is.EqualTo(1), stateName + " has one way in");
                Assert.That(any[0].conditions.Any(c => c.parameter == HumanoidClipWiring.PosingParameter && c.mode == AnimatorConditionMode.If)
                    && any[0].conditions.Any(c => c.parameter == HumanoidClipWiring.PoseParameter
                        && c.mode == AnimatorConditionMode.Equals && Mathf.Approximately(c.threshold, number)), Is.True,
                    stateName + " is held on Posing with the pose " + number);
                Assert.That(held.transitions.Any(t => t.destinationState == idle && t.conditions.Any(c =>
                    c.parameter == HumanoidClipWiring.PosingParameter && c.mode == AnimatorConditionMode.IfNot)), Is.True,
                    stateName + " is let go when the posing stops");
            }

            // Nothing is built twice. The rebuild used to clear only the Any State edges into the
            // states it knew, so any other edge doubled on every run of the menu item.
            var doubled = machine.anyStateTransitions.GroupBy(t => t.destinationState.name + ":" + string.Join(",",
                    t.conditions.Select(c => c.parameter + c.mode + c.threshold))).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToArray();
            Assert.That(doubled, Is.Empty, "an Any State edge is built once");
        }

        /// <summary>
        /// A gesture is asked of a body that is standing still and gives way to a walk the moment it
        /// is sent somewhere; a seated body claps and pumps its fist from its seat, and stands up out
        /// of either.
        /// </summary>
        [Test]
        public void GesturesGiveWayToAWalkAndASitterAnswersFromTheSeat()
        {
            var controller = Controller();
            var machine = Machine(controller);
            var idle = State(controller, "Idle");
            var walk = State(controller, "Walk");
            var gestures = Enum.GetNames(typeof(CharacterPresentation.Gesture));
            Assert.That(gestures.Length, Is.EqualTo(HumanoidClipWiring.Gestures.Length), "a take for every gesture the presentation can ask for");
            for (int i = 0; i < gestures.Length; i++)
            {
                var (trigger, stateName, _) = HumanoidClipWiring.Gestures[i];
                Assert.That(trigger, Is.EqualTo("Gesture" + gestures[i]), "gesture " + i + " is " + gestures[i] + " in both lists");
                Assert.That(controller.parameters.Any(p => p.name == trigger && p.type == AnimatorControllerParameterType.Trigger), Is.True, trigger);
                var state = State(controller, stateName);
                var any = machine.anyStateTransitions.Single(t => t.destinationState == state);
                Assert.That(any.conditions.Any(c => c.parameter == trigger && c.mode == AnimatorConditionMode.If), Is.True, stateName + " on its trigger");
                Assert.That(any.conditions.Any(c => c.parameter == HumanoidClipWiring.SeatedParameter && c.mode == AnimatorConditionMode.IfNot), Is.True,
                    stateName + " only while standing");
                Assert.That(any.conditions.Any(c => c.parameter == HumanoidClipWiring.SpeedParameter && c.mode == AnimatorConditionMode.Less), Is.True,
                    stateName + " only for a body that has stopped");
                foreach (var cue in HumanoidClipWiring.ActivityParameters)
                    Assert.That(any.conditions.Any(c => c.parameter == cue && c.mode == AnimatorConditionMode.IfNot), Is.True,
                        stateName + " waits while the body is " + cue.ToLowerInvariant());
                Assert.That(Leads(state, walk, HumanoidClipWiring.SpeedParameter), Is.True, stateName + " gives way to a walk");
                Assert.That(state.transitions.Any(t => t.destinationState == idle && t.hasExitTime), Is.True, stateName + " returns to idle");
            }
            // The ceremony beats keep playing over a step, and gain no walk: they fire at a commit,
            // on bodies that may still be easing to a halt.
            foreach (var (_, stateName) in HumanoidClipWiring.WiredReactions)
                Assert.That(Leads(State(controller, stateName), walk, HumanoidClipWiring.SpeedParameter), Is.False,
                    stateName + " plays out rather than being cut short by a body still coming to a stop");

            foreach (var seat in new[] { "SitIdle", "SitTalk" })
            {
                Assert.That(Leads(State(controller, seat), State(controller, "SitClap"), HumanoidClipWiring.SeatedClapTrigger), Is.True,
                    seat + " claps from the seat");
                Assert.That(Leads(State(controller, seat), State(controller, "SitVictory"), HumanoidClipWiring.SeatedVictoryTrigger), Is.True,
                    seat + " pumps a fist from the seat");
            }
            foreach (var gesture in new[] { "SitClap", "SitVictory" })
            {
                var state = State(controller, gesture);
                Assert.That(state.transitions.Any(t => t.destinationState == State(controller, "SitIdle") && t.hasExitTime), Is.True,
                    gesture + " settles back into the seat");
                Assert.That(Leads(state, idle, HumanoidClipWiring.SeatedParameter), Is.True, gesture + " stands up with the body");
            }
        }

        /// <summary>
        /// The standing poses are stills brought to life: each loops once a breath, the chest is
        /// somewhere else half a breath in and back where it began at the end, the body stands over
        /// its root facing the way it faces, and the head keeps only its row's share of the still's
        /// turn - a pose for the lens with the face turned from it is a photograph of an ear.
        /// </summary>
        [Test]
        public void EveryLivingPoseBreathesAndTurnsItsHeadAsItsRowSays()
        {
            float Muscle(AnimationClip clip, string name, float time) => AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Animator), name)).Evaluate(time);
            foreach (var (take, still, keepTurn) in HumanoidPoseAuthoring.Poses)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HumanoidClipWiring.Path(take));
                Assert.That(clip, Is.Not.Null, take + " is authored - Gamesim > Characters > Author the living poses");
                Assert.That(clip.humanMotion, Is.True, take);
                Assert.That(clip.isLooping, Is.True, take + " loops");
                Assert.That(clip.length, Is.EqualTo(HumanoidPoseAuthoring.Breath).Within(.05f), take + " is one breath long");

                float start = Muscle(clip, "Chest Front-Back", 0f);
                Assert.That(Mathf.Abs(Muscle(clip, "Chest Front-Back", clip.length * .5f) - start), Is.GreaterThan(.02f), take + " breathes");
                Assert.That(Muscle(clip, "Chest Front-Back", clip.length), Is.EqualTo(start).Within(.001f), take + " ends where it began");

                var source = AssetDatabase.LoadAllAssetsAtPath(HumanoidClipWiring.Path(still)).OfType<AnimationClip>()
                    .First(c => !c.name.StartsWith("__preview", StringComparison.Ordinal));
                var frame = HumanoidPoseAuthoring.Frame(source);
                foreach (var muscle in HumanoidPoseAuthoring.HeadTurn)
                    Assert.That(Muscle(clip, muscle, 0f), Is.EqualTo(frame[muscle] * keepTurn).Within(.001f),
                        take + " keeps " + keepTurn + " of " + still + "'s " + muscle);

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                Assert.That(settings.keepOriginalOrientation, Is.False, take + " turns with its body");
                var body = new Quaternion(Muscle(clip, "RootQ.x", 0f), Muscle(clip, "RootQ.y", 0f), Muscle(clip, "RootQ.z", 0f), Muscle(clip, "RootQ.w", 0f));
                var ahead = body * Vector3.forward; ahead.y = 0f;
                Assert.That(Mathf.Abs(Vector3.SignedAngle(Vector3.forward, ahead, Vector3.up)), Is.LessThan(2f), take + " faces the way its body does");
                Assert.That(Muscle(clip, "RootT.x", 0f), Is.Zero, take + " stands over its root");
                Assert.That(Muscle(clip, "RootT.z", 0f), Is.Zero, take + " stands over its root");
            }
        }

        [Test]
        public void NothingInTheCommittedControllerLeavesThisRepository()
        {
            // UMA is deliberately untracked, so a GUID naming one of its assets would leave every
            // clone without UMA holding a controller it cannot resolve. The idle and the walk this
            // rig has no take for are borrowed at runtime instead, never serialised.
            foreach (var path in new[] { HumanoidClipWiring.Controller, HumanoidClipWiring.Handle })
            {
                Assert.That(File.Exists(path), Is.True, path + " is committed, not built on first run");
                foreach (Match match in Regex.Matches(File.ReadAllText(path), @"guid: ([0-9a-f]{32})"))
                {
                    var guid = match.Groups[1].Value;
                    var referenced = AssetDatabase.GUIDToAssetPath(guid);
                    Assert.That(referenced, Is.Not.Empty, path + " names " + guid + ", which is in no clone");
                    Assert.That(referenced.StartsWith("Assets/Gamesim/", StringComparison.Ordinal), Is.True,
                        path + " names " + referenced + ", which is outside this repository");
                }
            }

            // And the handle is exactly that: the controller, carried into Resources so runtime can
            // find it, with no overrides baked in.
            var handle = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(HumanoidClipWiring.Handle);
            Assert.That(handle, Is.Not.Null, HumanoidClipWiring.Handle);
            Assert.That(handle.runtimeAnimatorController, Is.SameAs(Controller()), "the handle carries the controller");
            Assert.That(Resources.Load<AnimatorOverrideController>(HumanoidClipWiring.HandleResource),
                Is.SameAs(handle), "and UmaBodyProvider can load it by that name");

            // The two states UMA fills in are wired to takes nothing else plays, so overriding them
            // at runtime cannot disturb a state that has a take of its own.
            foreach (var standIn in new[] { HumanoidClipWiring.IdleStandIn, HumanoidClipWiring.RunStandIn })
            {
                var users = HumanoidClipWiring.States.Where(s => s.take == standIn).Select(s => s.state).ToArray();
                Assert.That(users.Length, Is.EqualTo(1), standIn + " stands in for one state only, or overriding it would move two");
            }
            Assert.That(HumanoidClipWiring.IdleStandIn, Is.Not.EqualTo(HumanoidClipWiring.RunStandIn));

            // The walk is authored and must NOT be a stand-in. It was one, and because UMA's
            // Locomotion controller carries Idle, Wave and Run - and no Walk at all - the clip
            // borrowed to fill it was the run, so every houseguest ran everywhere it went.
            var walk = HumanoidClipWiring.States.Single(s => s.state == "Walk");
            Assert.That(walk.take, Is.EqualTo("Walk_loop"),
                "the walk plays an authored take, not a key for UMA to fill");
            Assert.That(walk.take, Is.Not.EqualTo(HumanoidClipWiring.RunStandIn)
                .And.Not.EqualTo(HumanoidClipWiring.IdleStandIn));
        }
    }
}
