using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Wires the Humanoid mocap takes under <c>Art/Authored/Animation/Humanoid/</c> into
    /// <c>GamesimHumanoid.controller</c>, the controller the UMA cast plays.
    ///
    /// <para>The shipped cast is UMA, which is Humanoid and is a gigabyte this repository
    /// deliberately does not track. Until now a UMA body was handed UMA's own <c>Locomotion</c>
    /// controller, fetched by name through the asset indexer, and that controller declares
    /// <c>Speed</c> and nothing else — so every other cue <c>CharacterPresentation</c> sends
    /// (<c>Seated</c>, <c>Talking</c>, <c>Listening</c>, <c>Arguing</c>, the five reaction
    /// triggers) was dropped on the floor and the bodies never sat, talked, argued or reacted.
    /// This builds a controller that declares the seven of them the mocap takes can honestly act
    /// out, on the same parameter names and the same shape as <see cref="AuthoredClipWiring"/>
    /// gives the Generic cast, so one presentation drives both skeletons.</para>
    ///
    /// <para>Three beats stay undeclared: a nomination, a veto save and an eviction. Each is felt
    /// by the houseguest it happens to, and no take here is that feeling - the nearest the library
    /// has is a shrug, which would have a houseguest shrug off their own eviction and would act a
    /// nomination and an eviction identically besides. <c>CharacterPresentation</c> sends a trigger
    /// only to a controller that declares it, so leaving them out costs the body its idle for a
    /// beat and nothing else - which is what a UMA body did before any of this, and is a smaller
    /// lie than the wrong emotion. The Generic cast acts all five out from authored takes.</para>
    ///
    /// <para><b>The save and the nomination must land together.</b> A veto ceremony fires both in
    /// the same instant - <c>Saved</c> at whoever came off the block and <c>Nominated</c> at
    /// whoever replaced them (<c>EpisodeDirector.Ceremony.ReactToCeremony</c>) - and one implies
    /// the other, because the block keeps its size. Acting only the save would leave the person
    /// just put up, who is what the scene is about, the one body in the room not moving. That is
    /// not half the scene; it is the scene inverted. So neither is wired until both have a take,
    /// and the take for the save has to change the silhouette: a ceremony is framed room-wide at
    /// <c>CeremonyFraming.CeremonyDistance</c>, where a breath and a shoulder drop read as
    /// nothing.</para>
    ///
    /// <para><b>No UMA asset may be referenced by GUID.</b> A clone without UMA must still open
    /// this project cleanly, so the committed controller points only at the twelve takes in this
    /// repository. The twelve are missing a standing idle and a walk, which is exactly what UMA's
    /// Locomotion already has — so <c>Idle</c> and <c>Walk</c> are wired here to the two takes no
    /// cue reaches (<c>Sleep_loop</c> and <c>SleepLying_loop</c>), purely as override keys, and
    /// <c>UmaBodyProvider</c> swaps them for UMA's own idle and run through an
    /// <see cref="AnimatorOverrideController"/> built at runtime from clips it reads off the
    /// Locomotion controller the indexer resolves by name. Nothing about UMA is serialised.</para>
    ///
    /// <para>Runtime cannot reach an asset outside <c>Resources</c>, and the controller lives in
    /// <c>Art/Characters</c> beside the Generic one — so this also maintains a handle at
    /// <c>Resources/Animation/GamesimHumanoid.overrideController</c>, an override controller with
    /// no overrides whose only job is to carry the real controller into a build the way a
    /// <c>Resources/GamesimCharacters</c> prefab carries the Generic one.</para>
    ///
    /// <para>Re-runnable: states are found by name, their transitions cleared and rebuilt in a
    /// fixed order, never duplicated, and the controller is created from scratch if it is not
    /// there at all.</para>
    /// </summary>
    public static class HumanoidClipWiring
    {
        public const string Controller = "Assets/Gamesim/Art/Characters/GamesimHumanoid.controller";
        public const string Folder = "Assets/Gamesim/Art/Characters";
        public const string ClipFolder = AuthoredAssetImporter.Root + "Animation/Humanoid/";

        /// <summary>The handle in Resources that carries the controller into a player build.</summary>
        public const string Handle = "Assets/Gamesim/Resources/Animation/GamesimHumanoid.overrideController";
        public const string HandleFolder = "Assets/Gamesim/Resources/Animation";
        /// <summary>What <c>Resources.Load</c> is given for <see cref="Handle"/>.</summary>
        public const string HandleResource = "Animation/GamesimHumanoid";

        /// <summary>
        /// The take standing in for a clip this rig has not been given. Never played: the provider
        /// overrides both, and falls back to UMA's Locomotion whole if it cannot.
        /// </summary>
        public const string IdleStandIn = "Sleep_loop";
        public const string WalkStandIn = "SleepLying_loop";

        public const string SpeedParameter = "Speed";
        public const string SeatedParameter = "Seated";
        public const string TalkingParameter = "Talking";
        public const string ListeningParameter = "Listening";
        public const string ArguingParameter = "Arguing";

        /// <summary>The twelve takes, one to a file, named by their file (bb_anim_&lt;take&gt;.fbx).</summary>
        public static readonly string[] Takes =
        {
            "SitIdle_loop", "SitTalk_loop", "Talk_loop", "TalkB_loop", "TalkC_loop", "Argue_loop",
            "Cheer_loop", "Clap_loop", "React_won", "React_shrug", "Sleep_loop", "SleepLying_loop",
        };

        /// <summary>The states the graph is built from, and the take each one plays.</summary>
        public static readonly (string state, string take, float x, float y)[] States =
        {
            ("Idle", IdleStandIn, 200, 0),
            ("Walk", WalkStandIn, 200, 120),
            ("SitIdle", "SitIdle_loop", 520, 0),
            ("SitTalk", "SitTalk_loop", 760, 0),
            ("Talk", "Talk_loop", 520, 180),
            ("TalkB", "TalkB_loop", 760, 180),
            ("TalkC", "TalkC_loop", 1000, 180),
            ("Argue", "Argue_loop", 520, 330),
            ("ReactWon", "React_won", 1000, 420),
            ("ReactCheered", "Cheer_loop", 1000, 490),
        };

        /// <summary>
        /// One row per beat of <c>CharacterPresentation.Reaction</c>, in its order, because the
        /// presentation indexes this by the enum. A row with no state is a beat this cast has no
        /// take for: the trigger is not declared, the state is not built, and the body holds its
        /// idle rather than acting out a feeling it was not captured doing. Filling one in is a
        /// take named <c>bb_anim_React_&lt;beat&gt;.fbx</c> and the state name here.
        /// </summary>
        public static readonly (string trigger, string state)[] Reactions =
        {
            ("ReactNominated", null), ("ReactSaved", null), ("ReactEvicted", null),
            ("ReactWon", "ReactWon"), ("ReactCheered", "ReactCheered"),
        };

        /// <summary>The beats this cast can act out - the rest are left to the body's idle.</summary>
        public static IEnumerable<(string trigger, string state)> WiredReactions =>
            Reactions.Where(r => r.state != null);

        /// <summary>The three standing talk takes, played in a ring so a long conversation varies.</summary>
        public static readonly string[] TalkRing = { "Talk", "TalkB", "TalkC" };

        private const float SeatedFade = 0.35f;   // no sit-down take on this rig; the cross-fade carries it
        private const float RingExit = 0.92f;
        private const float Still = 0.1f;

        [MenuItem("Gamesim/U07/Wire the Humanoid takes")]
        public static void Apply()
        {
            var clips = LoadTakes();
            var missing = Takes.Where(t => !clips.ContainsKey(t)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("No Humanoid take for " + string.Join(", ", missing)
                    + " under " + ClipFolder + "; each take is one FBX named bb_anim_<take>.fbx.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null)
            {
                if (!AssetDatabase.IsValidFolder(Folder))
                    throw new InvalidOperationException("No folder at " + Folder);
                controller = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            }

            Parameter(controller, SpeedParameter, AnimatorControllerParameterType.Float);
            Parameter(controller, SeatedParameter, AnimatorControllerParameterType.Bool);
            Parameter(controller, TalkingParameter, AnimatorControllerParameterType.Bool);
            Parameter(controller, ListeningParameter, AnimatorControllerParameterType.Bool);
            Parameter(controller, ArguingParameter, AnimatorControllerParameterType.Bool);
            foreach (var (trigger, state) in Reactions)
            {
                if (state != null) { Parameter(controller, trigger, AnimatorControllerParameterType.Trigger); continue; }
                // A beat whose take was taken away: undeclare it, so the presentation stops sending it.
                var stale = controller.parameters.FirstOrDefault(p => p.name == trigger);
                if (stale != null) controller.RemoveParameter(stale);
            }

            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            foreach (var (name, take, x, y) in States)
                states[name] = Ensure(machine, name, clips[take], new Vector3(x, y, 0));
            foreach (var orphan in machine.states.Select(s => s.state)
                         .Where(s => States.All(row => row.state != s.name)).ToArray())
            {
                foreach (var reaching in machine.anyStateTransitions
                             .Where(t => t.destinationState == orphan).ToArray())
                    machine.RemoveAnyStateTransition(reaching);
                machine.RemoveState(orphan);
            }
            machine.defaultState = states["Idle"];

            // A fixed order, rebuilt from nothing every run: the first transition whose conditions
            // hold is the one taken, so sitting and walking are listed before anything a
            // conversation asks for, and the talk ring - which only fires on exit time - is last.
            foreach (var state in states.Values) Clear(state);
            foreach (var stateName in Reactions.Select(r => r.state))
                foreach (var existing in machine.anyStateTransitions
                             .Where(t => t.destinationState != null && t.destinationState.name == stateName).ToArray())
                    machine.RemoveAnyStateTransition(existing);

            Go(states["Idle"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["Idle"], states["Walk"], 0.15f, Moving(true));
            Go(states["Idle"], states["Argue"], 0.15f, Bool(ArguingParameter, true), Moving(false));
            Go(states["Idle"], states["Talk"], 0.15f, Bool(TalkingParameter, true), Moving(false));

            Go(states["Walk"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["Walk"], states["Idle"], 0.15f, Moving(false));

            Go(states["SitIdle"], states["Idle"], SeatedFade, Seated(false));
            Go(states["SitIdle"], states["SitTalk"], 0.15f, Bool(TalkingParameter, true));

            Go(states["SitTalk"], states["Idle"], SeatedFade, Seated(false));
            Go(states["SitTalk"], states["SitIdle"], 0.15f, Bool(TalkingParameter, false));

            for (int i = 0; i < TalkRing.Length; i++)
            {
                var here = states[TalkRing[i]];
                var next = states[TalkRing[(i + 1) % TalkRing.Length]];
                Go(here, states["SitIdle"], SeatedFade, Seated(true));
                Go(here, states["Walk"], 0.15f, Moving(true));
                Go(here, states["Argue"], 0.15f, Bool(ArguingParameter, true));
                Go(here, states["Idle"], 0.15f, Bool(TalkingParameter, false));
                // The take plays out, then hands the floor to the next one rather than looping.
                var ring = Go(here, next, 0.25f, Bool(TalkingParameter, true));
                ring.hasExitTime = true;
                ring.exitTime = RingExit;
            }

            Go(states["Argue"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["Argue"], states["Walk"], 0.15f, Moving(true));
            Go(states["Argue"], states["Talk"], 0.15f, Bool(ArguingParameter, false), Bool(TalkingParameter, true));
            Go(states["Argue"], states["Idle"], 0.15f, Bool(ArguingParameter, false), Bool(TalkingParameter, false));

            // Reactions: one-shots from Any State on their trigger, only while standing, back to Idle.
            foreach (var (trigger, stateName) in WiredReactions)
            {
                var state = states[stateName];
                var back = Go(state, states["Idle"], 0.2f);
                back.hasExitTime = true;
                back.exitTime = 0.9f;

                var any = machine.AddAnyStateTransition(state);
                any.hasExitTime = false;
                any.exitTime = 0f;
                any.duration = 0.1f;
                any.hasFixedDuration = true;
                any.canTransitionToSelf = false;
                any.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                any.AddCondition(AnimatorConditionMode.IfNot, 0f, SeatedParameter);
            }

            EditorUtility.SetDirty(controller);
            WireHandle(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] humanoid · " + Takes.Length + " mocap takes wired into " + Controller
                + ": " + States.Length + " states, " + WiredReactions.Count() + " of " + Reactions.Length
                + " reaction beats acted (the rest hold the idle); Idle and Walk stand in on "
                + IdleStandIn + " and " + WalkStandIn + " until UmaBodyProvider overrides them.");
        }

        /// <summary>The twelve takes, by clip name. One clip a file, the file names the take.</summary>
        public static Dictionary<string, AnimationClip> LoadTakes()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var take in Takes)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Path(take)).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview", StringComparison.Ordinal));
                if (clip != null) clips[take] = clip;
            }
            return clips;
        }

        /// <summary>The file a take arrived in.</summary>
        public static string Path(string take) => ClipFolder + AuthoredAssetImporter.AnimationPrefix + take + ".fbx";

        /// <summary>
        /// Keeps the Resources handle pointing at the controller, creating it if a clone has the
        /// controller but not the handle. It carries no overrides: the runtime builds its own.
        /// </summary>
        private static void WireHandle(AnimatorController controller)
        {
            var handle = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Handle);
            if (handle == null)
            {
                if (!AssetDatabase.IsValidFolder(HandleFolder))
                    AssetDatabase.CreateFolder("Assets/Gamesim/Resources", "Animation");
                handle = new AnimatorOverrideController();
                AssetDatabase.CreateAsset(handle, Handle);
            }
            if (handle.runtimeAnimatorController != controller) handle.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(handle);
        }

        private static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == name);
            if (existing == null) { controller.AddParameter(name, type); return; }
            if (existing.type == type) return;
            controller.RemoveParameter(existing);
            controller.AddParameter(name, type);
        }

        private static AnimatorState Ensure(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name)
                        ?? machine.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = true;
            return state;
        }

        private static void Clear(AnimatorState state)
        {
            foreach (var transition in state.transitions) state.RemoveTransition(transition);
        }

        private static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float duration,
            params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.duration = duration;
            transition.hasFixedDuration = true;
            foreach (var (mode, threshold, parameter) in conditions)
                transition.AddCondition(mode, threshold, parameter);
            return transition;
        }

        private static (AnimatorConditionMode, float, string) Bool(string parameter, bool wanted) =>
            (wanted ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);

        private static (AnimatorConditionMode, float, string) Seated(bool wanted) => Bool(SeatedParameter, wanted);

        private static (AnimatorConditionMode, float, string) Moving(bool wanted) =>
            (wanted ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, Still, SpeedParameter);
    }
}
