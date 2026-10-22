#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Gamesim.Editor;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class GroundedFarewellAnimationTests
    {
        private static AnimationClip Clip() => AssetDatabase.LoadAllAssetsAtPath(HumanoidClipWiring.Path("React_farewell"))
            .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview", StringComparison.Ordinal));

        [Test]
        public void FarewellBaseLayerRunsNoIkPassAndNoStateFootIk()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanoidClipWiring.Controller);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers.Length, Is.EqualTo(1), "Exactly the real single base layer.");
            Assert.That(controller.layers[0].iKPass, Is.False,
                "The farewell plants its feet by holding its stance; an IK pass on every humanoid is a cost nothing needs.");
            Assert.That(controller.layers[0].stateMachine.states.All(row => !row.state.iKOnFeet), Is.True,
                "No state Foot IK, including Idle and the protected full Embrace.");
        }

        [Test]
        public void FarewellIsOneNamedHumanSecondWithOriginalRootLocks()
        {
            var clip = Clip();
            Assert.That(clip.name, Is.EqualTo(CharacterPresentation.FarewellTake));
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(clip.isLooping, Is.False);
            Assert.That(clip.length, Is.EqualTo(1f).Within(.001f));
            var importer = (ModelImporter)AssetImporter.GetAtPath(HumanoidClipWiring.Path("React_farewell"));
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.animationCompression, Is.EqualTo(ModelImporterAnimationCompression.Off));
            var rule = importer.clipAnimations.Single();
            Assert.That(rule.name, Is.EqualTo("React_farewell"), "The actual importer names the filename, not Blender's global Scene stack.");
            Assert.That(rule.lockRootRotation && rule.lockRootHeightY && rule.lockRootPositionXZ, Is.True);
            Assert.That(rule.keepOriginalOrientation, Is.False);
        }

        [Test]
        public void FarewellIsAppendOnlyAndHasItsOwnNeutralExit()
        {
            Assert.That((int)CharacterPresentation.Reaction.StormOff, Is.EqualTo(9));
            Assert.That((int)CharacterPresentation.Reaction.Farewell, Is.EqualTo(10));
            Assert.That(HumanoidClipWiring.Takes.Last(), Is.EqualTo("React_farewell"));
            Assert.That(HumanoidClipWiring.Reactions.Last().trigger, Is.EqualTo("ReactFarewell"));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanoidClipWiring.Controller);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Single(s => s.state.name == "ReactFarewell").state;
            var idle = machine.states.Single(s => s.state.name == "Idle").state;
            Assert.That(state.motion, Is.SameAs(Clip()));
            Assert.That(state.speed, Is.EqualTo(1f));
            Assert.That(state.tag, Is.EqualTo(CharacterPresentation.ReactionStateTag));
            var exit = state.transitions.Single(t => t.destinationState == idle && t.hasExitTime);
            Assert.That(exit.exitTime, Is.EqualTo(29f / 30f).Within(.00001f));
            var embrace = machine.states.Single(s => s.state.name == "ReactEmbrace").state;
            Assert.That(embrace.transitions.Single(t => t.destinationState == idle && t.hasExitTime).exitTime,
                Is.EqualTo(.97f), "Full Embrace remains its original independent variant.");
        }

        [Test]
        public void ImportedFarewellReturnsItsMusclesToTheAuthoredBase()
        {
            var clip = Clip();
            var bindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Animator)
                && !b.propertyName.StartsWith("Root", StringComparison.Ordinal)).ToArray();
            Assert.That(bindings.Length, Is.GreaterThan(0), "Real imported Human muscle curves, not trigger metadata.");
            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                Assert.That(curve.Evaluate(29f / 30f), Is.EqualTo(curve.Evaluate(0f)).Within(.001f), binding.propertyName + " neutral at29/30");
                Assert.That(curve.Evaluate(1f), Is.EqualTo(curve.Evaluate(0f)).Within(.001f), binding.propertyName + " base endpoint");
            }
        }

        [Test]
        public void FarewellReadinessUsesTheExactGeneratedIdleAndWalkStopMotionsWithoutChangingTheGlobalExit()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanoidClipWiring.Controller);
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.runtimeAnimatorController, Is.SameAs(controller));
            var machine = controller.layers.Single().stateMachine;
            AnimationClip Imported(string take) => AssetDatabase.LoadAllAssetsAtPath(HumanoidClipWiring.Path(take))
                .OfType<AnimationClip>().Single(c => c.name == take);
            var idleClip = Imported(HumanoidClipWiring.IdleStandIn);
            var stopClip = Imported("WalkStop");
            var runClip = Imported(HumanoidClipWiring.RunStandIn);
            var idle = machine.states.Single(s => s.state.name == "Idle").state;
            var stop = machine.states.Single(s => s.state.name == "WalkStop").state;
            Assert.That(idle.motion, Is.SameAs(idleClip));
            Assert.That(stop.motion, Is.SameAs(stopClip));
            Assert.That(machine.states.Single(s => s.state.name == "Run").state.motion, Is.SameAs(runClip));
            Assert.That(idleClip.humanMotion && idleClip.isLooping && stopClip.humanMotion && !stopClip.isLooping, Is.True);
            Assert.That(stopClip.length, Is.GreaterThan(0f));
            Debug.Log("[Gamesim] actual imported WalkStop seconds=" + stopClip.length.ToString("R",
                System.Globalization.CultureInfo.InvariantCulture));
            var edge = stop.transitions.Single(t => t.destinationState == idle);
            Assert.That(edge.hasExitTime && edge.hasFixedDuration, Is.True);
            Assert.That(edge.conditions, Is.Empty);
            Assert.That(edge.exitTime, Is.EqualTo(.55f));
            Assert.That(edge.duration, Is.EqualTo(.25f));
            Assert.That(idle.speed, Is.EqualTo(1f));
            Assert.That(stop.speed, Is.EqualTo(1f));
            var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            handle.GetOverrides(overrides);
            Assert.That(overrides.All(row => row.Value == null || row.Value == row.Key), Is.True,
                "The persistent Resources handle is the exact base graph, not a hidden motion substitution.");
        }

        [Test]
        public void FarewellSourceIsTheSeparatePurposefulExport()
        {
            string file = Path.Combine(Directory.GetCurrentDirectory(), "ArtSource", "animation", "bb_anim_react_farewell.py");
            Assert.That(File.Exists(file), Is.True);
            string text = File.ReadAllText(file);
            Assert.That(text, Does.Contain("React_farewell"));
            Assert.That(text, Does.Contain("rig.check("));
            Assert.That(text, Does.Not.Contain("React_embrace"), "Not a cut or retiming of the preserved full take.");
        }
    }
}
#endif
