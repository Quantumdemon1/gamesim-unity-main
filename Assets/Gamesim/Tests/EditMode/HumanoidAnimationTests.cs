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
            // Fourteen: the twelve mocap takes, plus the walk and its stop. The walk is the one
            // that matters - without an authored take the Walk state was a key for UMA to fill, and
            // UMA's Locomotion controller has no walk to fill it with, so it filled it with a run.
            Assert.That(HumanoidClipWiring.Takes.Length, Is.EqualTo(14), "fourteen takes arrived");
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
                    take + " keeps the talk take's lean and loses its turn; it was built facing " + heading + " degrees round");
                Assert.That(Value("RootT.x"), Is.Zero, take + " stands where its root is");
                Assert.That(Value("RootT.z"), Is.Zero, take + " stands where its root is");
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

            // The reaction triggers are indexed by CharacterPresentation.Reaction, and both casts
            // answer to the same five names, so a beat added there is a beat both rigs are asked
            // for. This cast has takes for two of them; the other three rows carry no state, and
            // the controller must NOT declare those, because an undeclared trigger is how the
            // presentation knows to leave the body in its idle instead of playing a wrong take.
            var beats = Enum.GetNames(typeof(CharacterPresentation.Reaction));
            Assert.That(HumanoidClipWiring.Reactions.Length, Is.EqualTo(beats.Length),
                "one row per ceremony beat, whether or not a take exists for it");
            for (int i = 0; i < beats.Length; i++)
            {
                Assert.That(HumanoidClipWiring.Reactions[i].trigger, Is.EqualTo("React" + beats[i]));
                Assert.That(HumanoidClipWiring.Reactions[i].trigger,
                    Is.EqualTo(AuthoredClipWiring.Reactions[i].trigger), "both casts answer to the same trigger");
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
                var expected = HumanoidReactionAuthoring.Takes.Contains(take) ? "bb_anim_" + take : take;
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

            // Three standing takes, played in a ring on exit time, so a long conversation varies
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
