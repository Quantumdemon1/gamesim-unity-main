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
            Assert.That(HumanoidClipWiring.Takes.Length, Is.EqualTo(12), "twelve takes arrived");
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

            // The reaction triggers are indexed by CharacterPresentation.Reaction, and both casts
            // answer to the same five names, so a beat added there is a beat both rigs can act out.
            var beats = Enum.GetNames(typeof(CharacterPresentation.Reaction));
            Assert.That(HumanoidClipWiring.Reactions.Length, Is.EqualTo(beats.Length),
                "one reaction state per ceremony beat");
            for (int i = 0; i < beats.Length; i++)
            {
                Assert.That(HumanoidClipWiring.Reactions[i].trigger, Is.EqualTo("React" + beats[i]));
                Assert.That(HumanoidClipWiring.Reactions[i].trigger,
                    Is.EqualTo(AuthoredClipWiring.Reactions[i].trigger), "both casts answer to the same trigger");
                Declares(HumanoidClipWiring.Reactions[i].trigger, AnimatorControllerParameterType.Trigger);
            }
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
                Assert.That(state.motion.name, Is.EqualTo(take), name + " plays " + take);
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

            Assert.That(Leads(idle, walk, HumanoidClipWiring.SpeedParameter), Is.True, "idle walks");
            Assert.That(Leads(walk, idle, HumanoidClipWiring.SpeedParameter), Is.True, "and stops");
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

            foreach (var (trigger, stateName) in HumanoidClipWiring.Reactions)
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
            foreach (var standIn in new[] { HumanoidClipWiring.IdleStandIn, HumanoidClipWiring.WalkStandIn })
            {
                var users = HumanoidClipWiring.States.Where(s => s.take == standIn).Select(s => s.state).ToArray();
                Assert.That(users.Length, Is.EqualTo(1), standIn + " stands in for one state only, or overriding it would move two");
            }
            Assert.That(HumanoidClipWiring.IdleStandIn, Is.Not.EqualTo(HumanoidClipWiring.WalkStandIn));
        }
    }
}
