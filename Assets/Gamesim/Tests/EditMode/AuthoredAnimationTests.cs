using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The authored clips (MASTER-PLAN §4.5): baked on the shipped Quaternius skeleton, imported
    /// as Generic, the loops looping, and wired into the one controller the six bodies share so a
    /// seated houseguest has an idle and a talking one has a body that talks.
    /// </summary>
    public sealed class AuthoredAnimationTests
    {
        private static readonly string[] Loops = { "SitIdle_loop", "SitTalk_loop", "Talk_loop", "Listen_loop" };
        private static readonly string[] Reactions = { "React_nominated", "React_saved", "React_evicted", "React_won" };

        [Test]
        public void TheCasualTakesImportAsGenericClipsAndTheLoopsLoop()
        {
            var importer = AssetImporter.GetAtPath(AuthoredClipWiring.Clips) as ModelImporter;
            Assert.That(importer, Is.Not.Null, AuthoredClipWiring.Clips + " is exported by ArtSource/animation/bb_anim_casual.py");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic), "the Quaternius rig is Generic; its clips play by bone path");
            Assert.That(importer.importAnimation, Is.True);
            var clips = AssetDatabase.LoadAllAssetsAtPath(AuthoredClipWiring.Clips).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name, c => c);
            foreach (var name in Loops)
            {
                Assert.That(clips.ContainsKey(name), Is.True, name);
                Assert.That(clips[name].isLooping, Is.True, name + " loops");
                Assert.That(clips[name].length, Is.InRange(1.9f, 3.1f), name + " is two or three seconds");
            }
            foreach (var name in Reactions)
            {
                Assert.That(clips.ContainsKey(name), Is.True, name);
                Assert.That(clips[name].isLooping, Is.False, name + " is a one-shot");
                Assert.That(clips[name].length, Is.InRange(1.1f, 1.6f), name);
            }
            // The curves address the shipped bodies' bones, so the clips play on them unchanged.
            var bindings = AnimationUtility.GetCurveBindings(clips["SitIdle_loop"]);
            Assert.That(bindings.Any(b => b.path.EndsWith("Torso")), Is.True, "curves on CharacterArmature/.../Torso");
            Assert.That(bindings.All(b => b.path.StartsWith("CharacterArmature")), Is.True, "every curve path starts at the armature, as in the prefabs");
        }

        [Test]
        public void TheControllerHasTheSeatedIdleAndTheTalkStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AuthoredClipWiring.Controller);
            Assert.That(controller.parameters.Any(p => p.name == AuthoredClipWiring.TalkingParameter && p.type == AnimatorControllerParameterType.Bool), Is.True,
                "a Talking bool beside Speed and Seated");
            var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToDictionary(s => s.name, s => s);
            Assert.That(controller.parameters.Any(p => p.name == AuthoredClipWiring.ListeningParameter && p.type == AnimatorControllerParameterType.Bool), Is.True,
                "a Listening bool for the other half of a conversation");
            foreach (var name in new[] { "SitIdle", "SitTalk", "Talk", "Listen" })
            {
                Assert.That(states.ContainsKey(name), Is.True, name);
                Assert.That(states[name].motion, Is.Not.Null, name + " has its authored clip");
            }
            Assert.That(states["SitIdle"].motion.name, Is.EqualTo("SitIdle_loop"));
            Assert.That(states["SitDown"].transitions.Any(t => t.destinationState == states["SitIdle"] && t.hasExitTime), Is.True,
                "sitting down ends in the seated idle");
            Assert.That(states["SitIdle"].transitions.Any(t => t.destinationState == states["StandUp"]), Is.True, "and stands up from it");
            Assert.That(states["Idle"].transitions.Any(t => t.destinationState == states["Talk"]), Is.True, "idle talks");
            Assert.That(states["Talk"].transitions.Any(t => t.destinationState == states["Idle"]), Is.True, "and stops");
            Assert.That(states["Talk"].transitions.Any(t => t.destinationState == states["Listen"]), Is.True, "the floor changes hands");
            Assert.That(states["Listen"].transitions.Any(t => t.destinationState == states["Talk"]), Is.True, "and back");
            foreach (var (trigger, clip) in AuthoredClipWiring.Reactions)
            {
                Assert.That(controller.parameters.Any(p => p.name == trigger && p.type == AnimatorControllerParameterType.Trigger), Is.True, trigger);
                Assert.That(states.ContainsKey(clip) && states[clip].motion != null && states[clip].motion.name == clip, Is.True, clip);
                var any = controller.layers[0].stateMachine.anyStateTransitions.FirstOrDefault(t => t.destinationState == states[clip]);
                Assert.That(any, Is.Not.Null, clip + " plays from any state");
                Assert.That(any.conditions.Any(c => c.parameter == trigger), Is.True, clip + " on its trigger");
                Assert.That(any.conditions.Any(c => c.parameter == "Seated" && c.mode == AnimatorConditionMode.IfNot), Is.True, clip + " only while standing");
                Assert.That(states[clip].transitions.Any(t => t.destinationState == states["Idle"] && t.hasExitTime), Is.True, clip + " returns to idle");
            }
        }
    }
}
