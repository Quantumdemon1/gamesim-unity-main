#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.Diagnostics
{
    /// <summary>
    /// An in-memory copy of the shipping humanoid controller in which only the Idle state has Foot IK on,
    /// for the Source21 Idle Foot IK probes. Nothing touches the disk: the controller and every state
    /// machine, state, transition and behaviour in it are non-persistent objects this copy owns and
    /// destroys. An aborted run therefore leaves nothing under Assets/ for the next run to trip over, and a
    /// legitimate edit to the shipping controller (a new take, a new state) changes the copy with it -
    /// nothing here pins the shipping assets' bytes, state count or settings.
    /// </summary>
    public sealed class DiagnosticIdleFootIkController : IDisposable
    {
        public const int TransitionCap = 256, ParameterCap = 128;
        private readonly List<Object> owned = new List<Object>(256);

        public AnimatorController Source { get; }
        public AnimatorController Copy { get; private set; }
        public bool Released { get; private set; }

        private DiagnosticIdleFootIkController(AnimatorController source) { Source = source; }

        /// <summary>Copies the controller, verifies the copy equals it with Foot IK off everywhere, turns
        /// Foot IK on for Idle alone and verifies again.</summary>
        public static DiagnosticIdleFootIkController Build(AnimatorController source, string name)
        {
            Assert.That(source, Is.Not.Null, "SOURCE21_SOURCE_CONTROLLER");
            Assert.That(EditorUtility.IsPersistent(source), Is.True, "SOURCE21_SOURCE_IS_THE_SHIPPING_ASSET");
            var result = new DiagnosticIdleFootIkController(source);
            try
            {
                result.Copy = result.Clone(name);
                result.Verify(false);
                State(result.Copy, "Idle").iKOnFeet = true;
                result.Verify(true);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        public static AnimatorState State(AnimatorController controller, string name) =>
            controller.layers[0].stateMachine.states.Select(child => child.state).Single(state => state.name == name);

        private T Own<T>(T value) where T : Object
        {
            value.hideFlags = HideFlags.HideAndDontSave;
            owned.Add(value);
            return value;
        }

        private AnimatorController Clone(string name)
        {
            Assert.That(Source.layers.Length, Is.EqualTo(1), "SOURCE21_SOURCE_LAYERS");
            Assert.That(Source.parameters.Length, Is.LessThanOrEqualTo(ParameterCap), "SOURCE21_PARAMETER_CAP");
            var copy = Own(new AnimatorController { name = name });
            copy.parameters = Source.parameters.Select(p => new AnimatorControllerParameter
            {
                name = p.name, type = p.type, defaultBool = p.defaultBool, defaultInt = p.defaultInt, defaultFloat = p.defaultFloat
            }).ToArray();

            var sourceLayer = Source.layers[0];
            var sourceMachine = sourceLayer.stateMachine;
            var machine = Own(new AnimatorStateMachine
            {
                name = sourceMachine.name,
                anyStatePosition = sourceMachine.anyStatePosition,
                entryPosition = sourceMachine.entryPosition,
                exitPosition = sourceMachine.exitPosition,
                parentStateMachinePosition = sourceMachine.parentStateMachinePosition
            });

            var map = new Dictionary<AnimatorState, AnimatorState>();
            var children = new ChildAnimatorState[sourceMachine.states.Length];
            for (int i = 0; i < children.Length; i++)
            {
                var a = sourceMachine.states[i].state;
                Assert.That(a, Is.Not.Null, "SOURCE21_SOURCE_STATE_NULL");
                var b = Own(new AnimatorState
                {
                    name = a.name, motion = a.motion, speed = a.speed, cycleOffset = a.cycleOffset, mirror = a.mirror,
                    iKOnFeet = a.iKOnFeet, writeDefaultValues = a.writeDefaultValues, tag = a.tag,
                    speedParameterActive = a.speedParameterActive, speedParameter = a.speedParameter,
                    timeParameterActive = a.timeParameterActive, timeParameter = a.timeParameter,
                    mirrorParameterActive = a.mirrorParameterActive, mirrorParameter = a.mirrorParameter,
                    cycleOffsetParameterActive = a.cycleOffsetParameterActive, cycleOffsetParameter = a.cycleOffsetParameter
                });
                foreach (var behaviour in a.behaviours)
                {
                    var added = Own(b.AddStateMachineBehaviour(behaviour.GetType()));
                    EditorUtility.CopySerialized(behaviour, added);
                }
                map.Add(a, b);
                children[i] = new ChildAnimatorState { state = b, position = sourceMachine.states[i].position };
            }
            machine.states = children;
            machine.defaultState = sourceMachine.defaultState != null ? map[sourceMachine.defaultState] : null;

            int transitions = 0;
            AnimatorStateTransition[] Transitions(AnimatorStateTransition[] source, string owner)
            {
                transitions = checked(transitions + source.Length);
                Assert.That(transitions, Is.LessThanOrEqualTo(TransitionCap), "SOURCE21_TRANSITION_CAP_" + owner);
                return source.Select(a =>
                {
                    Assert.That(a.destinationStateMachine, Is.Null, "SOURCE21_SOURCE_NESTED_DESTINATION");
                    Assert.That(a.destinationState == null || map.ContainsKey(a.destinationState), Is.True,
                        "SOURCE21_TRANSITION_DESTINATION_OUTSIDE_ROOT");
                    return Own(new AnimatorStateTransition
                    {
                        name = a.name, destinationState = a.destinationState != null ? map[a.destinationState] : null,
                        isExit = a.isExit, hasExitTime = a.hasExitTime, hasFixedDuration = a.hasFixedDuration,
                        exitTime = a.exitTime, duration = a.duration, offset = a.offset,
                        interruptionSource = a.interruptionSource, orderedInterruption = a.orderedInterruption,
                        canTransitionToSelf = a.canTransitionToSelf, mute = a.mute, solo = a.solo,
                        conditions = a.conditions.Select(c => new AnimatorCondition
                            { mode = c.mode, parameter = c.parameter, threshold = c.threshold }).ToArray()
                    });
                }).ToArray();
            }
            foreach (var pair in map) pair.Value.transitions = Transitions(pair.Key.transitions, pair.Key.name);
            machine.anyStateTransitions = Transitions(sourceMachine.anyStateTransitions, "AnyState");

            copy.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = sourceLayer.name, defaultWeight = sourceLayer.defaultWeight, blendingMode = sourceLayer.blendingMode,
                    iKPass = sourceLayer.iKPass, avatarMask = sourceLayer.avatarMask, syncedLayerIndex = sourceLayer.syncedLayerIndex,
                    syncedLayerAffectsTiming = sourceLayer.syncedLayerAffectsTiming, stateMachine = machine
                }
            };
            return copy;
        }

        // The copy equals the source in every field the probe depends on, Foot IK aside, and every
        // object in it is one this copy owns: none persistent, none shared with the shipping asset.
        private void Verify(bool idleEnabled)
        {
            var source = Source; var copy = Copy;
            Assert.That(copy.layers.Length, Is.EqualTo(1), "SOURCE21_COPY_LAYERS");
            var sl = source.layers[0]; var cl = copy.layers[0];
            Assert.That((cl.name, cl.defaultWeight, cl.blendingMode, cl.iKPass, cl.syncedLayerIndex, cl.syncedLayerAffectsTiming),
                Is.EqualTo((sl.name, sl.defaultWeight, sl.blendingMode, sl.iKPass, sl.syncedLayerIndex, sl.syncedLayerAffectsTiming)),
                "SOURCE21_LAYER_VALUES");
            Assert.That(cl.avatarMask, Is.SameAs(sl.avatarMask), "SOURCE21_LAYER_MASK");
            var sm = sl.stateMachine; var cm = cl.stateMachine;
            Assert.That(sm.stateMachines.Length, Is.Zero, "SOURCE21_SOURCE_NESTED_MACHINE");
            Assert.That(cm.stateMachines.Length, Is.Zero, "SOURCE21_COPY_NESTED_MACHINE");
            Assert.That(sm.entryTransitions.Length, Is.Zero, "SOURCE21_SOURCE_ENTRY_TRANSITIONS");
            Assert.That(cm.entryTransitions.Length, Is.Zero, "SOURCE21_COPY_ENTRY_TRANSITIONS");
            Assert.That(sm.behaviours.Length, Is.Zero, "SOURCE21_SOURCE_MACHINE_BEHAVIOURS");
            Assert.That(cm.behaviours.Length, Is.Zero, "SOURCE21_COPY_MACHINE_BEHAVIOURS");
            Assert.That((cm.name, cm.anyStatePosition, cm.entryPosition, cm.exitPosition, cm.parentStateMachinePosition),
                Is.EqualTo((sm.name, sm.anyStatePosition, sm.entryPosition, sm.exitPosition, sm.parentStateMachinePosition)),
                "SOURCE21_MACHINE_LAYOUT");
            Assert.That(copy.parameters.Length, Is.EqualTo(source.parameters.Length), "SOURCE21_PARAMETER_COUNT");
            for (int i = 0; i < source.parameters.Length; i++)
            {
                var a = source.parameters[i]; var b = copy.parameters[i];
                Assert.That((b.name, b.type, b.defaultBool, b.defaultInt, b.defaultFloat),
                    Is.EqualTo((a.name, a.type, a.defaultBool, a.defaultInt, a.defaultFloat)), "SOURCE21_PARAMETER_" + i);
            }

            var ss = sm.states; var cs = cm.states;
            Assert.That(cs.Length, Is.EqualTo(ss.Length), "SOURCE21_STATE_COUNT");
            Assert.That(StateIndex(cs, cm.defaultState), Is.EqualTo(StateIndex(ss, sm.defaultState)), "SOURCE21_DEFAULT_STATE");
            var reached = new HashSet<Object> { copy, cm };
            var names = new HashSet<string>(StringComparer.Ordinal);
            int transitions = 0;
            for (int i = 0; i < ss.Length; i++)
            {
                var a = ss[i].state; var b = cs[i].state;
                Assert.That(b, Is.Not.Null, "SOURCE21_COPY_STATE_NULL");
                Assert.That(names.Add(a.name), Is.True, "SOURCE21_DUPLICATE_STATE_" + a.name);
                Assert.That(b.name, Is.EqualTo(a.name), "SOURCE21_STATE_NAME_" + i);
                Assert.That(a.iKOnFeet, Is.False, "SOURCE21_SOURCE_FOOT_IK_" + a.name);
                Assert.That(b.iKOnFeet, Is.EqualTo(idleEnabled && b.name == "Idle"), "SOURCE21_COPY_FOOT_IK_" + b.name);
                Assert.That(b.motion, Is.SameAs(a.motion), "SOURCE21_MOTION_" + b.name);
                Assert.That(b.motion, Is.TypeOf<AnimationClip>(), "SOURCE21_DIRECT_CLIP_" + b.name);
                Assert.That(cs[i].position, Is.EqualTo(ss[i].position), "SOURCE21_STATE_POSITION_" + b.name);
                Assert.That((b.speed, b.cycleOffset, b.mirror, b.writeDefaultValues, b.tag),
                    Is.EqualTo((a.speed, a.cycleOffset, a.mirror, a.writeDefaultValues, a.tag)), "SOURCE21_STATE_VALUES_" + b.name);
                Assert.That((b.speedParameterActive, b.speedParameter, b.timeParameterActive, b.timeParameter,
                        b.mirrorParameterActive, b.mirrorParameter, b.cycleOffsetParameterActive, b.cycleOffsetParameter),
                    Is.EqualTo((a.speedParameterActive, a.speedParameter, a.timeParameterActive, a.timeParameter,
                        a.mirrorParameterActive, a.mirrorParameter, a.cycleOffsetParameterActive, a.cycleOffsetParameter)),
                    "SOURCE21_STATE_PARAMETERS_" + b.name);
                Assert.That(reached.Add(b), Is.True, "SOURCE21_STATE_SHARED_" + b.name);
                Assert.That(b.behaviours.Length, Is.EqualTo(a.behaviours.Length), "SOURCE21_BEHAVIOURS_" + b.name);
                for (int behaviour = 0; behaviour < b.behaviours.Length; behaviour++)
                {
                    Assert.That(b.behaviours[behaviour].GetType(), Is.EqualTo(a.behaviours[behaviour].GetType()));
                    Assert.That(reached.Add(b.behaviours[behaviour]), Is.True, "SOURCE21_BEHAVIOUR_SHARED_" + b.name);
                }
                AssertTransitions(a.transitions, b.transitions, ss, cs, reached, ref transitions, b.name);
            }
            AssertTransitions(sm.anyStateTransitions, cm.anyStateTransitions, ss, cs, reached, ref transitions, "AnyState");

            foreach (var value in reached)
            {
                Assert.That(EditorUtility.IsPersistent(value), Is.False, "SOURCE21_COPY_PERSISTENT_" + value.name);
                Assert.That(AssetDatabase.Contains(value), Is.False, "SOURCE21_COPY_IN_ASSET_DATABASE_" + value.name);
            }
            Assert.That(reached.SetEquals(owned), Is.True, "SOURCE21_OWNED_SET");
            if (idleEnabled)
                Debug.Log("[Gamesim] SOURCE21-GRAPH version=2 candidate=in-memory states=" + cs.Length + " transitions=" + transitions
                    + " owned=" + owned.Count + " idleFootIk=true otherFootIk=false persisted=false");
        }

        private static void AssertTransitions(AnimatorStateTransition[] source, AnimatorStateTransition[] copy,
            ChildAnimatorState[] sourceStates, ChildAnimatorState[] copyStates, HashSet<Object> reached, ref int total, string owner)
        {
            Assert.That(copy.Length, Is.EqualTo(source.Length), "SOURCE21_TRANSITIONS_" + owner);
            total = checked(total + source.Length);
            Assert.That(total, Is.LessThanOrEqualTo(TransitionCap), "SOURCE21_TRANSITION_CAP_" + owner);
            for (int i = 0; i < source.Length; i++)
            {
                var a = source[i]; var b = copy[i];
                Assert.That(reached.Add(b), Is.True, "SOURCE21_TRANSITION_SHARED_" + owner + "_" + i);
                Assert.That(b.name, Is.EqualTo(a.name));
                Assert.That(StateIndex(copyStates, b.destinationState), Is.EqualTo(StateIndex(sourceStates, a.destinationState)),
                    "SOURCE21_TRANSITION_DESTINATION_" + owner + "_" + i);
                Assert.That(b.destinationStateMachine, Is.Null, "SOURCE21_COPY_NESTED_DESTINATION");
                Assert.That((b.isExit, b.hasExitTime, b.hasFixedDuration, b.exitTime, b.duration, b.offset,
                        b.interruptionSource, b.orderedInterruption, b.canTransitionToSelf, b.mute, b.solo),
                    Is.EqualTo((a.isExit, a.hasExitTime, a.hasFixedDuration, a.exitTime, a.duration, a.offset,
                        a.interruptionSource, a.orderedInterruption, a.canTransitionToSelf, a.mute, a.solo)),
                    "SOURCE21_TRANSITION_VALUES_" + owner + "_" + i);
                Assert.That(b.conditions.Length, Is.EqualTo(a.conditions.Length), "SOURCE21_CONDITIONS_" + owner + "_" + i);
                for (int c = 0; c < a.conditions.Length; c++)
                    Assert.That((b.conditions[c].mode, b.conditions[c].parameter, b.conditions[c].threshold),
                        Is.EqualTo((a.conditions[c].mode, a.conditions[c].parameter, a.conditions[c].threshold)),
                        "SOURCE21_CONDITION_" + owner + "_" + i + "_" + c);
            }
        }

        private static int StateIndex(ChildAnimatorState[] states, AnimatorState state)
        {
            if (state == null) return -1;
            for (int i = 0; i < states.Length; i++)
                if (states[i].state == state) return i;
            Assert.Fail("SOURCE21_TRANSITION_DESTINATION_OUTSIDE_ROOT");
            return -2;
        }

        /// <summary>Destroys every object of the copy. Call once nothing plays it any more.</summary>
        public void Dispose()
        {
            if (Released) return;
            Released = true;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            Copy = null;
        }

        /// <summary>True when every object the copy owned is gone.</summary>
        public bool AllDestroyed => Released && owned.All(value => value == null);
    }
}
#endif
