using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Wires the authored Generic clips (<c>ArtSource/animation/bb_anim_casual.py</c>) into
    /// <c>GamesimCharacter.controller</c>, which has had <c>Idle</c>, <c>Walk</c>, <c>SitDown</c> and
    /// <c>StandUp</c> since the Quaternius bodies arrived and nothing to play while seated or talking.
    ///
    /// <para>Adds a <c>Talking</c> bool beside <c>Speed</c> and <c>Seated</c>, and four states:
    /// <c>SitIdle</c> after <c>SitDown</c> finishes (the seated idle the plan asked for), <c>SitTalk</c>
    /// while seated and talking, <c>Talk</c> while standing and talking, and <c>Listen</c> is left for
    /// the presentation to blend later. Re-runnable: states and transitions are found by name and
    /// rebuilt, never duplicated. The presentation drives <c>Talking</c> only when the controller
    /// declares it, the same rule <c>Seated</c> follows.</para>
    /// </summary>
    public static class AuthoredClipWiring
    {
        public const string Controller = "Assets/Gamesim/Art/Characters/GamesimCharacter.controller";
        public const string Clips = AuthoredAssetImporter.Root + "Animation/Generic/bb_anim_casual.fbx";
        public const string TalkingParameter = "Talking";
        public const string ListeningParameter = "Listening";

        [MenuItem("Gamesim/U07/Wire the authored clips")]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null) throw new InvalidOperationException("No controller at " + Controller);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Clips).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview", StringComparison.Ordinal))
                .ToDictionary(clip => clip.name, clip => clip);
            if (clips.Count == 0) throw new InvalidOperationException("No authored clips at " + Clips + "; run bb_anim_casual.py first.");

            if (controller.parameters.All(p => p.name != TalkingParameter))
                controller.AddParameter(TalkingParameter, AnimatorControllerParameterType.Bool);
            if (controller.parameters.All(p => p.name != ListeningParameter))
                controller.AddParameter(ListeningParameter, AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;
            var idle = State(machine, "Idle");
            var sitDown = State(machine, "SitDown");
            var standUp = State(machine, "StandUp");
            if (idle == null || sitDown == null || standUp == null)
                throw new InvalidOperationException("The controller must still have Idle, SitDown and StandUp.");

            var sitIdle = Ensure(machine, "SitIdle", clips["SitIdle_loop"], new Vector3(300, 50, 0));
            var sitTalk = Ensure(machine, "SitTalk", clips["SitTalk_loop"], new Vector3(550, 50, 0));
            var talk = Ensure(machine, "Talk", clips["Talk_loop"], new Vector3(300, 250, 0));
            var listen = Ensure(machine, "Listen", clips["Listen_loop"], new Vector3(550, 250, 0));

            // Sitting down ends in the seated idle; standing up leaves from either seated state.
            Rebuild(sitDown, sitIdle, t => { t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.15f; });
            Rebuild(sitIdle, standUp, t => Condition(t, AnimatorConditionMode.IfNot, "Seated"));
            Rebuild(sitIdle, sitTalk, t => Condition(t, AnimatorConditionMode.If, TalkingParameter));
            Rebuild(sitTalk, sitIdle, t => Condition(t, AnimatorConditionMode.IfNot, TalkingParameter));
            Rebuild(sitTalk, standUp, t => Condition(t, AnimatorConditionMode.IfNot, "Seated"));
            // Standing: talk while talking and still; walking or sitting wins over talking.
            Rebuild(idle, talk, t => { Condition(t, AnimatorConditionMode.If, TalkingParameter); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
            Rebuild(talk, idle, t => Condition(t, AnimatorConditionMode.IfNot, TalkingParameter));
            Rebuild(talk, sitDown, t => Condition(t, AnimatorConditionMode.If, "Seated"));
            var walk = State(machine, "Walk");
            if (walk != null) Rebuild(talk, walk, t => t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"));
            // Listening: the other half of a standing conversation, swapping with Talk as the floor changes hands.
            Rebuild(idle, listen, t => { Condition(t, AnimatorConditionMode.If, ListeningParameter); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
            Rebuild(listen, idle, t => { Condition(t, AnimatorConditionMode.IfNot, ListeningParameter); Condition(t, AnimatorConditionMode.IfNot, TalkingParameter); });
            Rebuild(listen, talk, t => Condition(t, AnimatorConditionMode.If, TalkingParameter));
            Rebuild(talk, listen, t => Condition(t, AnimatorConditionMode.If, ListeningParameter));
            Rebuild(listen, sitDown, t => Condition(t, AnimatorConditionMode.If, "Seated"));
            if (walk != null) Rebuild(listen, walk, t => t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"));

            // Reactions: one-shots from Any State on a trigger, only while standing, back to Idle.
            for (int i = 0; i < Reactions.Length; i++)
            {
                var (trigger, clip) = Reactions[i];
                if (controller.parameters.All(p => p.name != trigger))
                    controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = Ensure(machine, clip, clips[clip], new Vector3(800, 50 + 80 * i, 0));
                foreach (var existing in machine.anyStateTransitions.Where(t => t.destinationState == state).ToArray())
                    machine.RemoveAnyStateTransition(existing);
                var any = machine.AddAnyStateTransition(state);
                any.hasExitTime = false;
                any.duration = 0.1f;
                any.canTransitionToSelf = false;
                any.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                any.AddCondition(AnimatorConditionMode.IfNot, 0f, "Seated");
                Rebuild(state, idle, t => { t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.2f; });
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] clips · " + clips.Count + " authored takes wired: SitIdle, SitTalk, Talk, Listen, four reactions; parameters " + TalkingParameter + ", " + ListeningParameter);
        }

        /// <summary>The reaction triggers and the clips they play, in CharacterPresentation.Reaction order.</summary>
        public static readonly (string trigger, string clip)[] Reactions =
        {
            ("ReactNominated", "React_nominated"), ("ReactSaved", "React_saved"),
            ("ReactEvicted", "React_evicted"), ("ReactWon", "React_won"),
        };

        private static AnimatorState State(AnimatorStateMachine machine, string name) =>
            machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

        private static AnimatorState Ensure(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
        {
            var state = State(machine, name) ?? machine.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = true;
            return state;
        }

        private static void Rebuild(AnimatorState from, AnimatorState to, Action<AnimatorStateTransition> configure)
        {
            foreach (var existing in from.transitions.Where(t => t.destinationState == to).ToArray())
                from.RemoveTransition(existing);
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.duration = 0.15f;
            transition.hasFixedDuration = true;
            configure(transition);
        }

        private static void Condition(AnimatorStateTransition transition, AnimatorConditionMode mode, string parameter) =>
            transition.AddCondition(mode, 0f, parameter);
    }
}
