using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Builds the controller shared by runtime Humanoid bodies. Twelve imported takes cover
    /// locomotion overrides, conversation, sitting and celebration; four authored muscle clips
    /// supply listening, nomination, relief and eviction. Every asset is project-owned so the
    /// controller remains importable without the optional UMA package.
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
        /// The clips standing in for UMA's own idle and run. The provider overrides both by name,
        /// and falls back to UMA's Locomotion whole if it cannot.
        ///
        /// <para>They are the library's idle and jog: a real idle and a real run, so an override
        /// that ever failed would still stand the body up and move it. Until the library arrived
        /// these places were held by the two Mixamo sleep takes - which left them unplayable as
        /// sleep, because an override replaces a clip everywhere it is used.</para>
        ///
        /// <para>The walk no longer needs one. It used to, and the cost was that every houseguest
        /// RAN everywhere: the provider filled the walk by asking UMA's Locomotion controller for a
        /// clip called "Walk" and then, failing that, one called "Run" - and that controller has
        /// Idle, Wave and Run in it, and no Walk at all. So the fallback was not a fallback, it was
        /// the only branch, and a borrowed run played whenever anyone moved a step. There is an
        /// authored walk now, and UMA's run stands in for the RUN state, where it belongs.</para>
        /// </summary>
        public const string IdleStandIn = "Idle_Loop";
        public const string RunStandIn = "Jog_Fwd_Loop";

        public const string SpeedParameter = "Speed";
        public const string SeatedParameter = "Seated";
        public const string TalkingParameter = "Talking";
        public const string ListeningParameter = "Listening";
        public const string ArguingParameter = "Arguing";

        /// <summary>
        /// How fast the walk and the run play: the body's ground speed over the take's own, so the
        /// feet keep up with the floor instead of skating over it. Defaults to one, which is the
        /// take as captured, for anything that never sets it.
        /// </summary>
        public const string PaceParameter = "Pace";

        /// <summary>
        /// What the body is doing at a piece of furniture, one cue each. Set by the pose owner every
        /// frame it holds the pose and cleared when it lets go; the controller takes each from Any
        /// State, so an activity begins from whatever the body was doing.
        /// </summary>
        public const string SleepingParameter = "Sleeping";
        public const string SwimmingParameter = "Swimming";
        public const string CookingParameter = "Cooking";
        public const string DancingParameter = "Dancing";
        /// <summary>Holding a pose for a camera, from <see cref="HumanoidPoseAuthoring.Poses"/>, chosen by <see cref="PoseParameter"/>.</summary>
        public const string PosingParameter = "Posing";

        /// <summary>The activity cues.</summary>
        public static readonly string[] ActivityParameters = { SleepingParameter, SwimmingParameter, CookingParameter, DancingParameter, PosingParameter };

        /// <summary>Which dance a dancing body dances: the row of <see cref="Dances"/>.</summary>
        public const string DanceStyleParameter = "DanceStyle";

        /// <summary>
        /// Where in its cycle a dance starts, normalized. A dance begun from its first frame opens on
        /// a wind-up, which is all a one-second beat on a mark would ever show, and two bodies that
        /// start together would move in step; whoever starts one says where.
        /// </summary>
        public const string DanceOffsetParameter = "DanceOffset";

        /// <summary>Which pose a posing body holds: the row of <see cref="HumanoidPoseAuthoring.Poses"/>.</summary>
        public const string PoseParameter = "Pose";

        /// <summary>
        /// The dances, in the order <see cref="DanceStyleParameter"/> counts them. Nought is the
        /// library's, which every body danced before the mocap dances arrived, so a body nobody gives
        /// a style dances as it always did.
        /// </summary>
        public static readonly (string state, string take)[] Dances =
        {
            ("Dance", "Dance_Loop"), ("DanceSamba", "DanceSamba_loop"), ("DanceHipHop", "DanceHipHop_loop"), ("DanceWave", "DanceWave_loop"),
        };

        /// <summary>The state each pose plays in: the living pose's take, less its loop suffix.</summary>
        public static string PoseState(string take) => take.EndsWith("_loop", StringComparison.Ordinal) ? take.Substring(0, take.Length - 5) : take;

        /// <summary>
        /// Seated gestures, each a trigger a sitting body answers from its seat: the clap and the
        /// fist pump were captured sitting, so they play where the standing reactions cannot.
        /// </summary>
        public const string SeatedClapTrigger = "SeatedClap";
        public const string SeatedVictoryTrigger = "SeatedVictory";

        /// <summary>
        /// Standing gestures that are not ceremony beats: one-shots a body makes because somebody
        /// asked it to - the player, mostly. Indexed by <c>CharacterPresentation.Gesture</c>.
        /// </summary>
        public static readonly (string trigger, string state, string take)[] Gestures =
        {
            ("GestureCheer", "Cheer", "Cheer_loop"),
            ("GestureShrug", "Shrug", "React_shrug"),
            ("GestureCelebrate", "Celebrate", "React_won"),
        };

        /// <summary>
        /// Whether this body is covering ground rather than crossing a room.
        ///
        /// <para>Set by whoever issued the move, not derived from speed: the agent runs at one speed
        /// whatever the distance, so speed cannot tell a trip to the yard from a step to the fridge.
        /// Only the player sets it: on a long route, on a double-click, and while chasing somebody.
        /// Houseguests always walk - nothing in their motion sets it, whatever this used to say.</para>
        /// </summary>
        public const string RunningParameter = "Running";

        /// <summary>
        /// The mocap takes, one to a file, named by their file (bb_anim_&lt;take&gt;.fbx). The
        /// standing poses arrive as stills - one frame each - and are played through the living
        /// poses <see cref="HumanoidPoseAuthoring"/> builds from them, never directly.
        /// </summary>
        public static readonly string[] Takes =
        {
            "SitIdle_loop", "SitTalk_loop", "Talk_loop", "TalkB_loop", "TalkC_loop", "Argue_loop",
            "Cheer_loop", "Clap_loop", "React_won", "React_shrug", "Sleep_loop", "SleepLying_loop",
            "Walk_loop", "WalkStop",
            "DanceSamba_loop", "DanceHipHop_loop", "DanceWave_loop", "SitLounge_loop", "SitClap", "SitVictory",
            "PoseHandBehindHead", "PoseFootUp", "PoseOverShoulder", "PoseAtEase",
            "PoseHandOnHip", "PoseHandOnHipGlance", "PosePowerStance",
        };

        /// <summary>The states the graph is built from, and the take each one plays.</summary>
        public static readonly (string state, string take, float x, float y)[] States = new (string, string, float, float)[]
        {
            ("Idle", IdleStandIn, 200, 0),
            ("Walk", "Walk_loop", 200, 120),
            ("Run", RunStandIn, 200, 240),
            ("WalkStop", "WalkStop", -60, 120),
            ("SitIdle", "SitIdle_loop", 520, 0),
            ("SitTalk", "SitTalk_loop", 760, 0),
            // Talk_loop was captured sitting (its hips average 63.6 cm, the standing takes' 101),
            // and played as the standing talk it sat every standing conversation on the air. It
            // talks from a seat now, as the seated ring's second take.
            ("SitTalkB", "Talk_loop", 1000, 0),
            ("SitClap", "SitClap", 640, -120),
            ("SitVictory", "SitVictory", 880, -120),
            // The standing talk's way in plays the ring's first take: see TalkEntry.
            ("Talk", "TalkB_loop", 520, 180),
            ("TalkB", "TalkB_loop", 760, 180),
            ("TalkC", "TalkC_loop", 1000, 180),
            ("Listen", "Listen_loop", 1240, 180),
            ("Argue", "Argue_loop", 520, 330),
            ("ReactWon", "React_won", 1000, 420),
            ("ReactCheered", "Cheer_loop", 1000, 490),
            ("ReactNominated", "React_nominated", 1240, 350),
            ("ReactSaved", "React_saved", 1240, 420),
            ("ReactEvicted", "React_evicted", 1240, 490),
            ("Sleep", "Sleep_loop", 1500, 0),
            ("SwimIdle", "Swim_Idle_Loop", 1500, 120),
            ("SwimForward", "Swim_Fwd_Loop", 1500, 240),
            ("Cook", "Cook_Loop", 1500, 360),
        }
            .Concat(Gestures.Select((g, i) => (g.state, g.take, 1000f, 560f + 70f * i)))
            .Concat(Dances.Select((d, i) => (d.state, d.take, 1500f, 480f + 70f * i)))
            .Concat(HumanoidPoseAuthoring.Poses.Select((p, i) => (PoseState(p.take), p.take, 1780f, 70f * i)))
            .ToArray();

        /// <summary>
        /// One row per beat of <c>CharacterPresentation.Reaction</c>, in its order, because the
        /// presentation indexes this by the enum. A row with no state is a beat this cast has no
        /// take for: the trigger is not declared, the state is not built, and the body holds its
        /// idle rather than acting out a feeling it was not captured doing. Filling one in is a
        /// take named <c>bb_anim_React_&lt;beat&gt;.fbx</c> and the state name here.
        /// </summary>
        public static readonly (string trigger, string state)[] Reactions =
        {
            ("ReactNominated", "ReactNominated"), ("ReactSaved", "ReactSaved"), ("ReactEvicted", "ReactEvicted"),
            ("ReactWon", "ReactWon"), ("ReactCheered", "ReactCheered"),
            // The story's five (plan §5.1): no takes yet, so the director plays a stand-in instead.
            ("ReactShocked", null), ("ReactTearful", null), ("ReactEmbrace", null), ("ReactFurious", null), ("ReactStormOff", null),
        };

        /// <summary>The beats this cast can act out - the rest are left to the body's idle.</summary>
        public static IEnumerable<(string trigger, string state)> WiredReactions =>
            Reactions.Where(r => r.state != null);

        /// <summary>The two standing talk takes, played in a ring so a long conversation varies.</summary>
        public static readonly string[] TalkRing = { "TalkB", "TalkC" };

        /// <summary>
        /// The standing talk's way in: the state a standing body that starts talking goes to, from
        /// idle, a listen or a row. It plays the ring's first take and hands the floor to the ring's
        /// second, so a standing conversation alternates the two standing takes from its first turn.
        /// It kept its name when its seated take left it, because every standing cue asks for it by
        /// that name.
        /// </summary>
        public const string TalkEntry = "Talk";

        /// <summary>
        /// The two seated talk takes, played in a ring the same way: the seated talk, then the talk
        /// take that was captured sitting (PACK8-PASS-PLAN A2).
        /// </summary>
        public static readonly string[] SeatedTalkRing = { "SitTalk", "SitTalkB" };

        /// <summary>
        /// What standing is, in the body's own terms: the height of its root (the body's centre,
        /// in its own heights, RootT.y) and the stretch of its straighter lower leg. Measured in the
        /// files: the standing takes carry their hips at 96 to 104 cm on average and the seated ones
        /// at 57 to 64; the standing poses sit their root at 0.95 to 1.02 with a leg stretched past
        /// 0.99, and the talk take captured sitting at 0.675 with its legs at 0.22 and 0.33.
        /// </summary>
        public const float StandingRootHeight = 0.85f, StraightLowerLeg = 0.7f;

        private const float SeatedFade = 0.35f;   // no sit-down take on this rig; the cross-fade carries it
        private const float RingExit = 0.92f;
        private const float Still = 0.1f;

        /// <summary>
        /// Cuts the library's clips again from <see cref="AuthoredAssetImporter.LibraryClips"/>,
        /// reimports the lying takes for their trim offsets, and rebuilds the controller over them. The table only takes effect on a reimport, and a
        /// version bump would reimport every model the importer handles, UMA's included.
        /// </summary>
        [MenuItem("Gamesim/Characters/Reimport the animation library")]
        public static void ReimportLibrary()
        {
            AssetDatabase.ImportAsset(AuthoredAssetImporter.LibraryPath, ImportAssetOptions.ForceUpdate);
            // The lying mocap takes carry trim offsets from the same table, and take them the same way.
            foreach (var take in Takes.Where(t => AuthoredAssetImporter.HorizontalTakes.ContainsKey(t)))
                AssetDatabase.ImportAsset(Path(take), ImportAssetOptions.ForceUpdate);
            Apply();
        }

        [MenuItem("Gamesim/U07/Wire the Humanoid takes")]
        public static void Apply()
        {
            var clips = LoadTakes();
            var missing = Takes.Concat(HumanoidReactionAuthoring.Takes).Concat(HumanoidPoseAuthoring.Takes)
                .Concat(AuthoredAssetImporter.LibraryClips.Select(c => c.clip)).Where(t => !clips.ContainsKey(t)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("No Humanoid take for " + string.Join(", ", missing)
                    + " under " + ClipFolder + "; each take is one FBX named bb_anim_<take>.fbx"
                    + (missing.Any(HumanoidPoseAuthoring.Takes.Contains)
                        ? ", and the living poses are built from the stills by Gamesim > Characters > Author the living poses."
                        : "."));

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
            Parameter(controller, RunningParameter, AnimatorControllerParameterType.Bool);
            Parameter(controller, PaceParameter, AnimatorControllerParameterType.Float, 1f);
            foreach (var activity in ActivityParameters) Parameter(controller, activity, AnimatorControllerParameterType.Bool);
            Parameter(controller, DanceStyleParameter, AnimatorControllerParameterType.Int);
            Parameter(controller, DanceOffsetParameter, AnimatorControllerParameterType.Float);
            Parameter(controller, PoseParameter, AnimatorControllerParameterType.Int);
            Parameter(controller, SeatedClapTrigger, AnimatorControllerParameterType.Trigger);
            Parameter(controller, SeatedVictoryTrigger, AnimatorControllerParameterType.Trigger);
            foreach (var (trigger, _, _) in Gestures) Parameter(controller, trigger, AnimatorControllerParameterType.Trigger);
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
            // Every Any State edge goes, and every one the graph needs is built again below. Clearing
            // only the edges into the states it knew about left any other edge to be doubled on the
            // next run, and to fire twice as often.
            foreach (var edge in machine.anyStateTransitions.ToArray()) machine.RemoveAnyStateTransition(edge);
            foreach (var orphan in machine.states.Select(s => s.state)
                         .Where(s => States.All(row => row.state != s.name)).ToArray())
                machine.RemoveState(orphan);
            machine.defaultState = states["Idle"];
            foreach (var paced in new[] { states["Walk"], states["Run"] })
            {
                paced.speedParameterActive = true;
                paced.speedParameter = PaceParameter;
            }

            // A fixed order, rebuilt from nothing every run: the first transition whose conditions
            // hold is the one taken, so sitting and walking are listed before anything a
            // conversation asks for, and the talk ring - which only fires on exit time - is last.
            foreach (var state in states.Values) Clear(state);

            Go(states["Idle"], states["SitIdle"], SeatedFade, Seated(true));
            // Before the walk, because the first transition whose conditions hold is the one taken:
            // setting off at a run should not spend a step walking first.
            Go(states["Idle"], states["Run"], 0.15f, Moving(true), Bool(RunningParameter, true));
            Go(states["Idle"], states["Walk"], 0.15f, Moving(true));
            Go(states["Idle"], states["Argue"], 0.15f, Bool(ArguingParameter, true), Moving(false));
            Go(states["Idle"], states[TalkEntry], 0.15f, Bool(TalkingParameter, true), Moving(false));

            // Walking stops through the stop take, so a body plants rather than snapping to idle.
            // Running exits straight to idle: a run that has to decelerate through a three-second
            // stop reads as a body that cannot be told what to do.
            Go(states["Walk"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["Walk"], states["Run"], 0.18f, Bool(RunningParameter, true), Moving(true));
            Go(states["Walk"], states["WalkStop"], 0.12f, Moving(false));

            Go(states["Run"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["Run"], states["Walk"], 0.20f, Bool(RunningParameter, false), Moving(true));
            Go(states["Run"], states["Idle"], 0.15f, Moving(false));

            // The stop take is three seconds of settling; nobody waits that long to be interrupted.
            // Any reason to move again beats it, and otherwise it falls to idle on its own.
            Go(states["WalkStop"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["WalkStop"], states["Run"], 0.12f, Bool(RunningParameter, true), Moving(true));
            Go(states["WalkStop"], states["Walk"], 0.12f, Moving(true));
            var settled = Go(states["WalkStop"], states["Idle"], 0.25f);
            settled.hasExitTime = true;
            settled.exitTime = 0.55f;

            // A seated gesture is asked for on a trigger and answered from the seat, ahead of talking:
            // a clap is over in seconds and the conversation picks up after it.
            foreach (var seat in new[] { "SitIdle" }.Concat(SeatedTalkRing).Select(name => states[name]))
            {
                Go(seat, states["Idle"], SeatedFade, Seated(false));
                Go(seat, states["SitClap"], 0.2f, Bool(SeatedClapTrigger, true));
                Go(seat, states["SitVictory"], 0.2f, Bool(SeatedVictoryTrigger, true));
            }
            Go(states["SitIdle"], states[SeatedTalkRing[0]], 0.15f, Bool(TalkingParameter, true));
            foreach (var talk in SeatedTalkRing) Go(states[talk], states["SitIdle"], 0.15f, Bool(TalkingParameter, false));
            // The seated talk hands the floor on the way the standing one does, after the take has played.
            for (int i = 0; i < SeatedTalkRing.Length; i++)
            {
                var ring = Go(states[SeatedTalkRing[i]], states[SeatedTalkRing[(i + 1) % SeatedTalkRing.Length]], 0.25f,
                    Bool(TalkingParameter, true));
                ring.hasExitTime = true;
                ring.exitTime = RingExit;
            }
            // The clap holds its hands up for five seconds and brings them down by 5.6 of its 6.5;
            // the fist pump is up and down by four of its 5.6 and sits still after.
            foreach (var (gesture, done) in new[] { ("SitClap", .9f), ("SitVictory", .72f) })
            {
                Go(states[gesture], states["Idle"], SeatedFade, Seated(false));
                var settle = Go(states[gesture], states["SitIdle"], 0.3f);
                settle.hasExitTime = true;
                settle.exitTime = done;
            }

            // The way in and the ring leave the same ways. The way in plays the ring's first take, so
            // it hands the floor to the ring's second; the ring's own takes hand it round. Past the
            // way in, speakers[i] is TalkRing[i - 1], so the take after it is TalkRing[i % length].
            var speakers = new[] { TalkEntry }.Concat(TalkRing).ToArray();
            for (int i = 0; i < speakers.Length; i++)
            {
                var here = states[speakers[i]];
                var next = states[TalkRing[(i == 0 ? 1 : i) % TalkRing.Length]];
                Go(here, states["SitIdle"], SeatedFade, Seated(true));
                Go(here, states["Walk"], 0.15f, Moving(true));
                Go(here, states["Argue"], 0.15f, Bool(ArguingParameter, true));
                Go(here, states["Idle"], 0.15f, Bool(TalkingParameter, false));
                // The take plays out, then hands the floor to the next one rather than looping.
                var ring = Go(here, next, 0.25f, Bool(TalkingParameter, true));
                ring.hasExitTime = true;
                ring.exitTime = RingExit;
            }

            Go(states["Idle"], states["Listen"], .18f, Bool(ListeningParameter, true), Bool(TalkingParameter, false), Moving(false));
            Go(states["Listen"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["Listen"], states["Walk"], .15f, Moving(true));
            Go(states["Listen"], states["Argue"], .15f, Bool(ArguingParameter, true));
            Go(states["Listen"], states[TalkEntry], .15f, Bool(TalkingParameter, true));
            Go(states["Listen"], states["Idle"], .18f, Bool(ListeningParameter, false));

            Go(states["Argue"], states["SitIdle"], SeatedFade, Seated(true));
            Go(states["Argue"], states["Walk"], 0.15f, Moving(true));
            Go(states["Argue"], states[TalkEntry], 0.15f, Bool(ArguingParameter, false), Bool(TalkingParameter, true));
            Go(states["Argue"], states["Idle"], 0.15f, Bool(ArguingParameter, false), Bool(TalkingParameter, false));

            // Reactions: one-shots from Any State on their trigger, only while standing, back to Idle.
            foreach (var (trigger, stateName) in WiredReactions)
            {
                var state = states[stateName];
                var back = Go(state, states["Idle"], 0.2f);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                OneShot(machine, state, trigger);
            }

            // Gestures are asked of a body that has stopped, and give way to a walk the moment it is
            // sent somewhere, rather than skating there with its arms in the air. The ceremony beats
            // keep playing over a step: they fire at a commit, which can land on a body still easing
            // to a halt, and a beat cut short by that would never be seen at all.
            foreach (var (trigger, stateName, _) in Gestures)
            {
                var state = states[stateName];
                Go(state, states["Walk"], 0.15f, Moving(true));
                var back = Go(state, states["Idle"], 0.2f);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                OneShot(machine, state, trigger, Moving(false));
            }

            // Activities, from Any State so they begin from whatever the body was doing - a walk
            // that arrives at the bed does not have to stop first. Each ends to idle when its cue
            // goes, and none can restart itself while it holds.
            Activity(machine, states["Sleep"], .35f, Bool(SleepingParameter, true));
            Activity(machine, states["SwimForward"], .35f, Bool(SwimmingParameter, true), Moving(true));
            Activity(machine, states["SwimIdle"], .35f, Bool(SwimmingParameter, true), Moving(false));
            Activity(machine, states["Cook"], .35f, Bool(CookingParameter, true));
            Go(states["Sleep"], states["Idle"], .45f, Bool(SleepingParameter, false));
            Go(states["SwimIdle"], states["Idle"], .3f, Bool(SwimmingParameter, false));
            Go(states["SwimForward"], states["Idle"], .3f, Bool(SwimmingParameter, false));
            Go(states["Cook"], states["Idle"], .25f, Bool(CookingParameter, false));

            // A dance is the style it is asked for, and changes style without stopping; each starts
            // where DanceOffset says, so two dancers who begin together are not in step.
            for (int style = 0; style < Dances.Length; style++)
            {
                var dance = states[Dances[style].state];
                dance.tag = DanceTag;
                dance.cycleOffsetParameterActive = true;
                dance.cycleOffsetParameter = DanceOffsetParameter;
                Activity(machine, dance, .35f, Bool(DancingParameter, true), Is(DanceStyleParameter, style));
                Go(dance, states["Idle"], .25f, Bool(DancingParameter, false));
            }

            // A pose is struck quickly and let go gently, and moves between poses the same way.
            for (int pose = 0; pose < HumanoidPoseAuthoring.Poses.Length; pose++)
            {
                var held = states[PoseState(HumanoidPoseAuthoring.Poses[pose].take)];
                held.tag = PoseTag;
                Activity(machine, held, .25f, Bool(PosingParameter, true), Is(PoseParameter, pose));
                Go(held, states["Idle"], .3f, Bool(PosingParameter, false));
            }

            EditorUtility.SetDirty(controller);
            WireHandle(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] humanoid · " + Takes.Length + " mocap takes wired into " + Controller
                + ": " + States.Length + " states, " + WiredReactions.Count() + " of " + Reactions.Length
                + " reaction beats acted; the walk is authored, and Idle and Run use override keys "
                + IdleStandIn + " and " + RunStandIn + " until UmaBodyProvider overrides them.");
        }

        /// <summary>
        /// Every take, by clip name: one clip a file for the mocap takes, where the file names the
        /// take, and the library's clips by the names the importer cut them under.
        /// </summary>
        public static Dictionary<string, AnimationClip> LoadTakes()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var take in Takes.Concat(HumanoidReactionAuthoring.Takes).Concat(HumanoidPoseAuthoring.Takes))
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Path(take)).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview", StringComparison.Ordinal));
                if (clip != null) clips[take] = clip;
            }
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(AuthoredAssetImporter.LibraryPath).OfType<AnimationClip>())
                if (AuthoredAssetImporter.LibraryClips.Any(row => row.clip == clip.name)) clips[clip.name] = clip;
            return clips;
        }

        private static void Activity(AnimatorStateMachine machine, AnimatorState state, float fade,
            params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
        {
            var any = machine.AddAnyStateTransition(state);
            any.hasExitTime = false;
            any.exitTime = 0f;
            any.duration = fade;
            any.hasFixedDuration = true;
            any.canTransitionToSelf = false;
            foreach (var (mode, threshold, parameter) in conditions) any.AddCondition(mode, threshold, parameter);
        }

        /// <summary>A one-shot from Any State on its trigger, only for a standing body at liberty.</summary>
        private static void OneShot(AnimatorStateMachine machine, AnimatorState state, string trigger,
            params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
        {
            var any = machine.AddAnyStateTransition(state);
            any.hasExitTime = false;
            any.exitTime = 0f;
            any.duration = 0.1f;
            any.hasFixedDuration = true;
            any.canTransitionToSelf = false;
            any.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            any.AddCondition(AnimatorConditionMode.IfNot, 0f, SeatedParameter);
            // A body asleep, in the water or at the stove does not jump up to cheer.
            foreach (var activity in ActivityParameters) any.AddCondition(AnimatorConditionMode.IfNot, 0f, activity);
            foreach (var (mode, threshold, parameter) in conditions) any.AddCondition(mode, threshold, parameter);
        }

        /// <summary>The tag every dance state carries, so a test can ask whether a body is dancing whichever dance it is.</summary>
        public const string DanceTag = "Dance";
        /// <summary>The tag every pose state carries.</summary>
        public const string PoseTag = "Pose";

        /// <summary>The file a take arrived in.</summary>
        public static string Path(string take) => AuthoredAssetImporter.LibraryClips.Any(row => row.clip == take)
            ? AuthoredAssetImporter.LibraryPath
            : ClipFolder + AuthoredAssetImporter.AnimationPrefix + take
                + (HumanoidReactionAuthoring.Takes.Contains(take) || HumanoidPoseAuthoring.Takes.Contains(take) ? ".anim" : ".fbx");

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

        private static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type,
            float initial = 0f)
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == name);
            if (existing != null && existing.type == type && Mathf.Approximately(existing.defaultFloat, initial)) return;
            if (existing != null) controller.RemoveParameter(existing);
            controller.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = initial });
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

        private static (AnimatorConditionMode, float, string) Is(string parameter, int value) =>
            (AnimatorConditionMode.Equals, value, parameter);

        private static (AnimatorConditionMode, float, string) Moving(bool wanted) =>
            (wanted ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, Still, SpeedParameter);
    }
}
