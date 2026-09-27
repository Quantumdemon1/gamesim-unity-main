using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Stylized cast presentation. The houseguest's body is the one the registered provider - UMA -
    /// builds, driven through an Animator; with no provider, which is a clone without the UMA package,
    /// the original articulated primitive recipe stands in, so the project still runs. There is no
    /// other cast. Only this component's visual hierarchy moves; navigation stays authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPresentation : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int SeatedParam = Animator.StringToHash("Seated");
        private static readonly int RunningParam = Animator.StringToHash("Running");
        private static readonly int TalkingParam = Animator.StringToHash("Talking");
        private static readonly int ListeningParam = Animator.StringToHash("Listening");
        private static readonly int ArguingParam = Animator.StringToHash("Arguing");
        private static readonly int PaceParam = Animator.StringToHash("Pace");

        /// <summary>
        /// The ground each take covers in a second as captured: what a body's own speed is divided
        /// by, so a walk at the house's 2.2 m/s plays its steps fast enough to cover 2.2 m.
        /// Measured by <c>UmaFacingPlayModeTests</c> from the planted foot: 1.36 and 4.83 m/s. The
        /// plan's 1.7 was read off a probe that averaged the lifted foot in with the planted one.
        /// </summary>
        public const float WalkTakeSpeed = 1.36f, RunTakeSpeed = 4.83f;
        /// <summary>
        /// How far a take may be sped up or slowed down. The house walks briskly - 2.2 m/s against
        /// the take's 1.36 - so the walk is let run to 1.65, which covers it with steps that still
        /// read as a walk; slower than three quarters and a run reads as wading.
        /// </summary>
        public const float SlowestPace = .75f, FastestPace = 1.65f;
        private bool hasPaceParam;
        private float groundSpeed;

        /// <summary>How fast the walk or run is playing, as the animator was last told.</summary>
        public float Pace
        {
            get
            {
                if (groundSpeed < .3f) return 1f;
                return Mathf.Clamp(groundSpeed / (running && !seated ? RunTakeSpeed : WalkTakeSpeed), SlowestPace, FastestPace);
            }
        }

        /// <summary>
        /// What a body is doing at a piece of furniture, beyond sitting. Appended to, never
        /// reordered: <see cref="ActivityParams"/> is indexed by it, less one.
        /// </summary>
        public enum BodyActivity { None, Sleeping, Swimming, Cooking, Dancing, Posing }
        private static readonly int[] ActivityParams =
        {
            Animator.StringToHash("Sleeping"), Animator.StringToHash("Swimming"),
            Animator.StringToHash("Cooking"), Animator.StringToHash("Dancing"),
            Animator.StringToHash("Posing"),
        };
        private BodyActivity activity;
        private bool activityMoving;
        private int activityParams;

        /// <summary>
        /// Which dance a dancing body dances. Appended to, never reordered: the controller counts its
        /// dances in this order (<c>HumanoidClipWiring.Dances</c>). House is the library's, which
        /// every body danced first, so a body nobody gives a style dances as it always did.
        /// </summary>
        public enum DanceStyle { House, Samba, HipHop, Wave }

        /// <summary>
        /// A pose held for a camera, in the order the controller counts them
        /// (<c>HumanoidPoseAuthoring.Poses</c>). Appended to, never reordered. Each is a still
        /// brought to life - it breathes - and all but the look over the shoulder face the way the
        /// chest does. The first four were captured on a man, the last three on a woman; which a
        /// body strikes is <see cref="CastMoves"/>'s business, by the body's frame.
        /// </summary>
        public enum Pose { HandBehindHead, FootUp, OverShoulder, AtEase, HandOnHip, HandOnHipGlance, PowerStance }

        /// <summary>
        /// A standing one-shot a body makes because it was asked to - by the player, mostly - rather
        /// than a ceremony beat. Given way to a walk the moment the body moves. Appended to, never
        /// reordered: <c>HumanoidClipWiring.Gestures</c> is indexed by it.
        /// </summary>
        public enum Gesture { Cheer, Shrug, Celebrate }
        private static readonly int[] GestureParams =
        {
            Animator.StringToHash("GestureCheer"), Animator.StringToHash("GestureShrug"), Animator.StringToHash("GestureCelebrate"),
        };
        private int gestureParams;

        private static readonly int DanceStyleParam = Animator.StringToHash("DanceStyle");
        private static readonly int DanceOffsetParam = Animator.StringToHash("DanceOffset");
        private static readonly int PoseParam = Animator.StringToHash("Pose");
        private static readonly int SeatedClapParam = Animator.StringToHash("SeatedClap");
        private static readonly int SeatedVictoryParam = Animator.StringToHash("SeatedVictory");
        private bool hasDanceStyleParam, hasDanceOffsetParam, hasPoseParam, hasSeatedClapParam, hasSeatedVictoryParam;
        private DanceStyle danceStyle;
        private float danceStart = float.NaN;
        private Pose pose;
        private bool seatedGesturePending;
        /// <summary>
        /// A beat a body acts out: a one-shot clip the controller may declare as a trigger. Appended
        /// to, never reordered: <c>HumanoidClipWiring.Reactions</c> and <see cref="ReactionParams"/>
        /// are both indexed by this order. The story's five (plan §5.1) are appended, so no value
        /// moves: until a controller declares their triggers a body skips them like any clip it
        /// lacks, and the director plays a ceremony stand-in instead (see <see cref="Supports"/>).
        /// </summary>
        public enum Reaction { Nominated, Saved, Evicted, Won, Cheered, Shocked, Tearful, Embrace, Furious, StormOff }
        private static readonly int[] ReactionParams =
        {
            Animator.StringToHash("ReactNominated"), Animator.StringToHash("ReactSaved"),
            Animator.StringToHash("ReactEvicted"), Animator.StringToHash("ReactWon"),
            Animator.StringToHash("ReactCheered"),
            Animator.StringToHash("ReactShocked"), Animator.StringToHash("ReactTearful"),
            Animator.StringToHash("ReactEmbrace"), Animator.StringToHash("ReactFurious"),
            Animator.StringToHash("ReactStormOff"),
        };
        private int reactionParams;
        /// <summary>The last beat this body was asked to act out, whether or not it had a clip for it.</summary>
        public Reaction? LastReaction { get; private set; }

        // V6 (VISUAL-TARGET.md): a head that turns to look. The crowd at a ceremony turns to the
        // nominee, the evicted, the winner; a conversation partner is already faced by the whole
        // body, so the head adds nothing there. Applied as a world-space turn about the vertical and
        // the body's right axis on top of whatever the clip posed, so it works on any rig's head bone
        // whatever that bone's local axes are, and on the primitive head the same way.
        private Transform lookTarget;
        private float lookUntil, lookBlend;
        public const float LookYawLimit = 60f;
        public const float LookPitchLimit = 20f;
        /// <summary>What the head is turned toward, or null; for the tests and the director.</summary>
        public Transform LookTarget => lookTarget != null && Time.time < lookUntil ? lookTarget : null;

        /// <summary>Turns the head toward a target for a while. Null, or the time passing, lets it go.</summary>
        public void LookAt(Transform target, float seconds) => Look(target, seconds, PersonEyeHeight);

        /// <summary>
        /// The same, at a point: the target is where the eyes go, not a person whose face is a
        /// standing height above their transform. The diary's camera is at eye height already, and a
        /// head told to look 1.5 m above it put its chin at the ceiling and its eyes over the lens -
        /// a gape at nothing (playtest, 2026-09-27).
        /// </summary>
        public void LookAtPoint(Transform target, float seconds) => Look(target, seconds, 0f);

        /// <summary>Where a person's face is above their transform, which is at their feet.</summary>
        private const float PersonEyeHeight = 1.5f;
        private float lookLift = PersonEyeHeight;

        /// <summary>The pitch the head last turned by to look, in degrees, up positive: for the tests.</summary>
        public float LookPitch { get; private set; }

        private void Look(Transform target, float seconds, float lift)
        {
            lookTarget = target;
            lookLift = lift;
            lookUntil = target != null ? Time.time + Mathf.Max(0f, seconds) : 0f;
        }

        private void ApplyLook(Transform headBone)
        {
            if (headBone == null) return;
            bool looking = lookTarget != null && Time.time < lookUntil && !reducedMotion;
            lookBlend = Mathf.Lerp(lookBlend, looking ? 1f : 0f, 1f - Mathf.Exp(-5f * Time.deltaTime));
            if (lookBlend < 0.002f) { lookBlend = 0f; return; }
            if (lookTarget == null) return;
            var to = lookTarget.position + Vector3.up * lookLift - headBone.position;
            var flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, Vector3.up), -LookYawLimit, LookYawLimit);
            float pitch = Mathf.Clamp(Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -LookPitchLimit, LookPitchLimit);
            LookPitch = pitch;
            headBone.rotation = Quaternion.AngleAxis(yaw * lookBlend, Vector3.up)
                * Quaternion.AngleAxis(-pitch * lookBlend, transform.right) * headBone.rotation;
        }

        [SerializeField] private ContestantState definition;
        [SerializeField] private Color wardrobeColor = new Color(0.26f, 0.76f, 0.65f);
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Renderer> replacedRenderers = new List<Renderer>();
        private Material wardrobeMaterial, accentMaterial;
        private bool fixedGoldAccent;
        private Transform visual, chest, head, leftArm, rightArm, leftLeg, rightLeg, leftKnee, rightKnee;
        private Vector3 previousPosition;
        private float walkPhase, movementBlend, phaseOffset, heightScale = 1f;
        private bool reducedMotion, talking, speaking = true, seated, arguing, built;
        private float facingYaw = float.NaN;

        // Model-backed presentation. When animator is null the primitive rig above is in use.
        private Animator animator;
        private Transform modelHead;
        private Quaternion modelHeadRest;
        private CharacterBody providedBody;
        /// <summary>A provider's body asked for and not yet drawable: see <see cref="BodyArrived"/>.</summary>
        private bool awaitingBody;
        private RuntimeAnimatorController inspectedController;
        private bool hasSpeedParam, hasSeatedParam, hasTalkingParam, hasListeningParam, hasArguingParam;
        private bool hasRunningParam;
        private bool running;
        public string CharacterId { get; private set; }
        public string AppearanceKey { get; private set; }
        public CharacterAppearance AppearanceSnapshot => definition?.appearance?.Clone();
        /// <summary>The visual hierarchy may fit furniture while navigation continues to own the actor root.</summary>
        public Transform VisualRoot => visual;

        /// <summary>
        /// How many deferred bodies have finished assembling this session. Monotonic; readers keep
        /// their own last-seen value.
        /// </summary>
        public static int BodiesCompleted { get; private set; }

        /// <summary>
        /// True while the provider is still assembling this body, which is not drawn until it is.
        /// Read-only proof for tests that must not be interrupted by the render a finished body
        /// triggers; never a source of game knowledge and never a command.
        /// </summary>
        public bool IsBodyAssembling => awaitingBody;
        private static int deferredCloneBuilds;

        /// <summary>
        /// Copies an authored actor's collision/label hierarchy without copying a generated body
        /// or starting a build for the old identity. Attach supplies the new identity afterward.
        /// The source's presentation is detached only during the synchronous Instantiate call;
        /// restoring it in finally preserves its transform and its provider's resource ownership.
        /// </summary>
        public static GameObject CloneUnbound(GameObject template, Transform parent)
        {
            var source = template.GetComponent<CharacterPresentation>();
            if (source == null) return Instantiate(template, parent);
            var detached = new List<(Transform child, Vector3 position, Quaternion rotation, Vector3 scale, int sibling)>();
            for (int i = 0; i < template.transform.childCount; i++)
            {
                var child = template.transform.GetChild(i);
                if (child != source.visual && child.name != "Gamesim Character Visual" && child.name != "Retired Gamesim Character Visual") continue;
                detached.Add((child,child.localPosition,child.localRotation,child.localScale,child.GetSiblingIndex()));
            }
            GameObject inactiveHolder = null;
            if (!template.activeInHierarchy)
            {
                inactiveHolder = new GameObject("Inactive character clone staging");
                inactiveHolder.SetActive(false);
            }
            deferredCloneBuilds++;
            try
            {
                foreach (var item in detached) item.child.SetParent(inactiveHolder != null ? inactiveHolder.transform : null,true);
                var clone = Instantiate(template,parent);
                // Inline Unity serialization can materialize null custom classes. Defer Awake
                // explicitly, then clear the copied identity before an inactive clone is enabled.
                clone.GetComponent<CharacterPresentation>().definition = null;
                return clone;
            }
            finally
            {
                deferredCloneBuilds--;
                foreach (var item in detached)
                {
                    if (item.child == null) continue;
                    item.child.SetParent(template.transform,false);
                    item.child.SetLocalPositionAndRotation(item.position,item.rotation);
                    item.child.localScale = item.scale;
                    item.child.SetSiblingIndex(item.sibling);
                }
                if (inactiveHolder != null) Destroy(inactiveHolder);
            }
        }

        public static CharacterPresentation Attach(GameObject root, ContestantState character, Color palette)
        {
            if (root == null || character == null) return null;
            var component = root.GetComponent<CharacterPresentation>();
            if (component == null) component = root.AddComponent<CharacterPresentation>();
            if (component.built && (component.CharacterId != ContentCatalog.CanonicalId(character.id)
                || component.AppearanceKey != AppearanceKeyFor(character)))
                component.ReleasePresentation();
            if (!component.built)
            {
                component.definition = character.Clone();
                component.wardrobeColor = palette;
                if (Application.isPlaying) component.Build(component.definition, palette);
            }
            else if (component.wardrobeColor != palette)
            {
                component.wardrobeColor = palette;
                component.ApplyWardrobe(palette);
            }
            component.enabled = true;
            // The face wears the simulation's own words, every time the director attaches.
            component.SetMood(character.mood, character.stressLevel);
            return component;
        }

        /// <summary>
        /// Dresses a houseguest who is already standing in the house: into swimwear at the pool,
        /// into nightwear at the bed, back into the day's clothes after.
        ///
        /// <para>A new look is a new body, and <see cref="Attach"/> builds one by taking the old
        /// one away first - which leaves the houseguest missing for the half-second UMA takes to
        /// make them again. Here the new body is built out of
        /// sight, at no size, beside the old one; the old one goes on walking, sitting and being
        /// looked at until the frame the new one is ready, and then they change places.</para>
        ///
        /// <para>Anything with nothing to hide behind - a body not yet built, a different person, the
        /// primitive rig - is attached the ordinary way.</para>
        /// </summary>
        public static CharacterPresentation Dress(GameObject root, ContestantState character, Color palette)
        {
            if (root == null || character == null) return null;
            var component = root.GetComponent<CharacterPresentation>();
            if (component == null || !component.built || !Application.isPlaying || !component.providedBody.Exists
                || component.awaitingBody || component.CharacterId != ContentCatalog.CanonicalId(character.id))
            {
                component?.CancelDressing();
                return Attach(root, character, palette);
            }
            component.Redress(character, palette);
            component.enabled = true;
            component.SetMood(character.mood, character.stressLevel);
            return component;
        }

        /// <summary>Whether a new look is being built behind this body.</summary>
        public bool IsChangingOutfit => dressingRoom != null;

        private Transform dressingRoom;
        private CharacterBody dressing;
        private ContestantState dressingAs;
        private string dressingKey;

        private void Redress(ContestantState character, Color palette)
        {
            string key = AppearanceKeyFor(character);
            if (key == AppearanceKey) { CancelDressing(); return; }
            if (dressingRoom != null && key == dressingKey) return;
            CancelDressing();
            var room = Joint("Gamesim Character Wardrobe", transform, Vector3.zero);
            room.localScale = Vector3.zero;
            if (!CharacterBodySource.TryCreate(new CharacterBodyRequest(CharacterId, AppearanceId(character, CharacterId),
                    character.appearance), room, palette, out var created) || !created.Exists)
            {
                Destroy(room.gameObject);
                return;
            }
            dressingRoom = room; dressing = created; dressingAs = character.Clone(); dressingKey = key;
        }

        /// <summary>Copies every parameter the shown body's animator holds onto the one being made.</summary>
        private static void MirrorCues(Animator from, Animator to)
        {
            if (from == null || to == null || from == to || !from.isActiveAndEnabled || !to.isActiveAndEnabled
                || from.runtimeAnimatorController != to.runtimeAnimatorController || to.parameterCount == 0) return;
            foreach (var parameter in from.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float: to.SetFloat(parameter.nameHash, from.GetFloat(parameter.nameHash)); break;
                    case AnimatorControllerParameterType.Int: to.SetInteger(parameter.nameHash, from.GetInteger(parameter.nameHash)); break;
                    case AnimatorControllerParameterType.Bool: to.SetBool(parameter.nameHash, from.GetBool(parameter.nameHash)); break;
                }
            }
        }

        private void CancelDressing()
        {
            if (dressingRoom != null)
            {
                dressingRoom.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(dressingRoom.gameObject); else DestroyImmediate(dressingRoom.gameObject);
            }
            dressingRoom = null; dressing = default; dressingAs = null; dressingKey = null;
        }

        /// <summary>The frame the new look is ready, it takes the old one's place.</summary>
        private void TickDressing()
        {
            if (dressingRoom == null) return;
            if (!dressing.Exists) { CancelDressing(); return; }
            // The Animator the new body has now, looked up every time: UMA replaces the one the
            // body was created with while it assembles, so the handle taken at creation is a
            // destroyed object within a frame - and cues sent to it, and the hand-over below,
            // went nowhere. The body took over standing in Idle and sat down again.
            var hidden = dressing.Root.GetComponentInChildren<Animator>(true);
            // The body being made hears every cue the one on show does, so it is in the same
            // state - sitting, swimming, walking - when it takes over, not standing up out of Idle.
            MirrorCues(animator, hidden);
            var state = dressing.Root.GetComponent<CharacterBodyBuildState>();
            if (state != null && !state.Ready) return;
            if (dressing.Root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) return;

            // And exactly where in it: the same state at the same moment.
            bool aligned = animator != null && hidden != null && animator.isActiveAndEnabled
                && animator.runtimeAnimatorController == hidden.runtimeAnimatorController;
            var playing = aligned ? animator.GetCurrentAnimatorStateInfo(0) : default;
            var old = visual;
            if (old != null)
            {
                old.name = "Retired Gamesim Character Visual";
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            visual = dressingRoom;
            visual.name = "Gamesim Character Visual";
            visual.localScale = Vector3.one * heightScale;
            providedBody = dressing;
            animator = hidden;
            if (animator != null) animator.applyRootMotion = false;
            if (aligned && animator != null)
            {
                animator.Play(playing.fullPathHash, 0, playing.normalizedTime);
                animator.Update(0f);
            }
            inspectedController = null;
            hasSpeedParam = hasSeatedParam = hasTalkingParam = hasListeningParam = hasArguingParam = false;
            hasRunningParam = hasPaceParam = false; activityParams = 0;
            modelHead = null;
            definition = dressingAs; AppearanceKey = dressingKey;
            dressingRoom = null; dressing = default; dressingAs = null; dressingKey = null;
        }

        private string mood = "Neutral", stress = "Normal";

        /// <summary>The houseguest's mood and stress level, for the face (MASTER-PLAN §3.B faces).</summary>
        public void SetMood(string moodWord, string stressWord)
        {
            mood = moodWord ?? "Neutral";
            stress = stressWord ?? "Normal";
        }

        /// <summary>
        /// The two words the face is wearing, and whether it is allowed to move. A UMA body's
        /// expression player belongs to the body, is built by UMA, and lives in an assembly this one
        /// knows nothing about, so it reads these three values every frame; nothing is pushed to it.
        /// </summary>
        public string Mood => mood;
        public string Stress => stress;
        public bool ReducedMotion => reducedMotion;

        private void Awake()
        {
            if (!built && definition != null && deferredCloneBuilds == 0) Build(definition, wardrobeColor);
        }

        public void SetReducedMotion(bool value) => reducedMotion = value;
        public void SetTalking(bool value) => talking = value;
        /// <summary>
        /// Within a conversation, whether this body has the floor. The director alternates it
        /// between the two; the one without it listens. Defaults to true so a body told only that
        /// it is talking (the player's conversation) talks.
        /// </summary>
        public void SetSpeaking(bool value) => speaking = value;
        public bool IsSpeaking => talking && speaking;
        public void SetSeated(bool value) => seated = value;

        /// <summary>
        /// Whether this body is covering ground rather than crossing a room.
        ///
        /// <para>Set by whoever chose the route, not read off the speed. Speed cannot tell the two
        /// apart: the movement blend saturates at <c>speed / 2.5</c>, so a houseguest at a walking
        /// 2.2 m/s already reads 0.88 and the player's authored 4 reads a flat 1.0. A run is a
        /// decision about distance - or a double-click - and it arrives as one.</para>
        /// </summary>
        public void SetRunning(bool value) => running = value;
        public bool IsRunning => running;

        /// <summary>
        /// Plays an activity: lying asleep, swimming, working at the stove, dancing. The pose owner
        /// sets it every frame it holds the body and sets it back to None when it lets go.
        /// <paramref name="moving"/> is for a swimmer whose visual body is doing a length while the
        /// root waits at the side: the root is still, the stroke is not.
        /// </summary>
        public void SetActivity(BodyActivity value, bool moving = false) { activity = value; activityMoving = moving; }
        public BodyActivity Activity => activity;

        /// <summary>
        /// Which dance this body dances when it dances, and where in the dance it starts, normalized;
        /// NaN starts it at this body's own place, so two dancers side by side are never in step.
        /// Set it before the dance begins: the start is read as the dance is entered.
        /// </summary>
        public void SetDanceStyle(DanceStyle style, float startAt = float.NaN) { danceStyle = style; danceStart = startAt; }
        public DanceStyle Dance => danceStyle;

        /// <summary>Whether this body can dance the style: any dancer the library's steps, and the mocap dances only on a controller that has them.</summary>
        public bool CanDance(DanceStyle style)
        {
            if (!CanAct(BodyActivity.Dancing)) return false;
            return style == DanceStyle.House || hasDanceStyleParam;
        }

        /// <summary>Whether the body has come to a stop - its movement blend all but gone.</summary>
        public bool IsStill => movementBlend < .1f;

        /// <summary>Which pose this body strikes when it is <see cref="BodyActivity.Posing"/>.</summary>
        public void SetPose(Pose value) => pose = value;
        public Pose HeldPose => pose;

        /// <summary>
        /// Makes a gesture, if the body can: standing, at liberty and still, with a controller that
        /// has the take, and with motion not reduced. Returns whether it was asked of the animator.
        /// </summary>
        public bool MakeGesture(Gesture kind)
        {
            LastGesture = kind;
            if (animator == null || reducedMotion || seated || activity != BodyActivity.None || movementBlend > .1f) return false;
            RefreshAnimatorParameters();
            if ((gestureParams & (1 << (int)kind)) == 0) return false;
            animator.SetTrigger(GestureParams[(int)kind]);
            return true;
        }

        /// <summary>The last gesture this body was asked for, whether or not it could make it.</summary>
        public Gesture? LastGesture { get; private set; }

        /// <summary>Whether this body's controller has a take for the gesture.</summary>
        public bool Supports(Gesture kind)
        {
            if (animator == null) return false;
            RefreshAnimatorParameters();
            return (gestureParams & (1 << (int)kind)) != 0;
        }

        /// <summary>
        /// The body this houseguest was built on, as its provider reported it; Unknown for a body
        /// that did not say, which is any body that is not a provider's.
        /// </summary>
        public BodyFrame Frame
        {
            get
            {
                if (!providedBody.Exists) return BodyFrame.Unknown;
                var state = providedBody.Root.GetComponent<CharacterBodyBuildState>();
                return state == null ? BodyFrame.Unknown : state.Frame;
            }
        }

        /// <summary>
        /// Whether this body's controller can act the activity out. A body that cannot is posed by
        /// its owner instead, or left standing - never handed a cue it has no state for.
        /// </summary>
        public bool CanAct(BodyActivity value)
            => value != BodyActivity.None && animator != null && inspectedController != null
               && (activityParams & (1 << ((int)value - 1))) != 0;
        /// <summary>
        /// Whether this body is in a tense conversation rather than an ordinary one. The director
        /// sets it for the pairs whose topic the caption calls a tense conversation; the controller
        /// answers with the emphatic standing loop where it declares the parameter.
        /// </summary>
        public void SetArguing(bool value) => arguing = value;
        /// <summary>
        /// What the body is actually doing, which is what the animator is told: an argument the
        /// director set, unless motion is reduced, which switches it off exactly as it does the
        /// talk loops. Arm-waving is the same kind of motion at a larger size.
        /// </summary>
        public bool IsArguing => arguing && !reducedMotion;
        /// <summary>
        /// The heading (yaw, degrees) to settle on once stopped, or NaN to leave the heading to
        /// whoever moves the body. A conversation sets it: into the chair, or toward the other speaker.
        /// </summary>
        public void SetFacing(float yaw) => facingYaw = yaw;
        public bool IsTalking => talking;

        /// <summary>
        /// Acts out a ceremony beat. Recorded always; played only when the controller declares the
        /// trigger and motion is not reduced - a reaction is the largest motion a body makes. A
        /// seated body answers from its seat where it has a take captured sitting: a clap for a
        /// cheer, a fist pump for a win or a save. The standing beats have nothing to cut to from a
        /// chair, so the rest leave a sitter sitting.
        /// </summary>
        public void React(Reaction kind)
        {
            LastReaction = kind;
            if (animator == null || reducedMotion) return;
            RefreshAnimatorParameters();
            if (seated)
            {
                if (kind == Reaction.Cheered && hasSeatedClapParam) { animator.SetTrigger(SeatedClapParam); seatedGesturePending = true; }
                else if ((kind == Reaction.Won || kind == Reaction.Saved) && hasSeatedVictoryParam)
                { animator.SetTrigger(SeatedVictoryParam); seatedGesturePending = true; }
                return;
            }
            if ((reactionParams & (1 << (int)kind)) != 0) animator.SetTrigger(ReactionParams[(int)kind]);
        }

        /// <summary>Whether a seated body can answer the reaction from its seat.</summary>
        public bool SupportsSeated(Reaction kind)
        {
            if (animator == null) return false;
            RefreshAnimatorParameters();
            return kind == Reaction.Cheered ? hasSeatedClapParam : (kind == Reaction.Won || kind == Reaction.Saved) && hasSeatedVictoryParam;
        }
        /// <summary>Whether this body's controller has a clip for the reaction: what the director asks before choosing a stand-in.</summary>
        public bool Supports(Reaction kind)
        {
            if (animator == null) return false;
            RefreshAnimatorParameters();
            return (reactionParams & (1 << (int)kind)) != 0;
        }
        public bool IsSeated => seated;
        public float FacingYaw => facingYaw;

        private void Build(ContestantState character, Color palette)
        {
            CharacterId = ContentCatalog.CanonicalId(character.id);
            AppearanceKey = AppearanceKeyFor(character);
            var appearanceId = AppearanceId(character, CharacterId);
            bool diplomat = appearanceId == "maya-hassan";
            bool athlete = appearanceId == "taylor-kim";
            bool caregiver = appearanceId == "jamie-roberts";
            bool wildcard = appearanceId == "casey-wilson";
            bool analyst = appearanceId == "riley-johnson";
            heightScale = analyst ? 1.05f : athlete ? 1.03f : diplomat ? 1.01f : wildcard ? 0.96f : 1f;
            if (character.appearance != null) heightScale = 1f;
            phaseOffset = diplomat ? 0.4f : athlete ? 1.5f : caregiver ? 2.7f : wildcard ? 3.9f : analyst ? 5.1f : 0f;
            // V6: sixteen people are not five. Each body idles, nods and sways on its own phase, from
            // its id, so a room of houseguests never breathes in unison.
            phaseOffset += (Mathf.Abs(CharacterId.GetHashCode()) % 628) / 100f;

            // Tests and markers key off this exact name, whichever body is built underneath it.
            visual = Joint("Gamesim Character Visual", transform, Vector3.zero);
            visual.localScale = Vector3.one * heightScale;

            if (!TryBuildProvidedBody(appearanceId, palette))
                BuildPrimitiveBody(appearanceId, palette, diplomat, athlete, caregiver, wildcard, analyst);

            // U02's body/head are replaceable visual placeholders. Keep every collider and marker.
            foreach (string oldName in new[] { "Body", "Head" })
            {
                var old = transform.Find(oldName);
                var renderer = old != null ? old.GetComponent<Renderer>() : null;
                if (renderer != null && renderer.enabled)
                {
                    replacedRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }
            previousPosition = transform.position;
            built = true;
        }

        /// <summary>
        /// Asks the registered body provider - UMA, when a scene opts into it - for the houseguest's
        /// body. There is no other cast: without a provider, which is a clone without the UMA
        /// package, the primitive rig stands in. Editor-time builds are skipped because a generated
        /// body has no business being written into a scene file.
        /// </summary>
        private bool TryBuildProvidedBody(string appearanceId, Color palette)
        {
            var provider = CharacterBodySource.Provider;
            if (provider == null || !Application.isPlaying) return false;
            if (!CharacterBodySource.TryCreate(new CharacterBodyRequest(CharacterId, appearanceId,
                    definition?.appearance), visual, palette, out var created) || !created.Exists)
                return false;

            providedBody = created;
            animator = created.Animator;
            if (animator != null) animator.applyRootMotion = false;

            // A deferred body has no skeleton yet; LateUpdate picks the head up once it exists.
            if (!created.Deferred) ResolveModelHead();
            else awaitingBody = true;
            return true;
        }

        /// <summary>
        /// Notes the first frame a provider's body has something to draw. Until then the houseguest
        /// is not drawn at all, for the half-second UMA takes to assemble them. Nothing stands in:
        /// the stand-in was a body from another cast, a different person on screen for that
        /// half-second (2026-09-27, the cast is UMA only).
        /// </summary>
        private void BodyArrived()
        {
            if (!awaitingBody || !providedBody.Exists) return;
            var state = providedBody.Root.GetComponent<CharacterBodyBuildState>();
            if (state != null && !state.Ready) return;
            if (providedBody.Root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) return;

            awaitingBody = false;
            // The director redraws the HUD when a body arrives, for what it can show of a houseguest
            // who is finally there. A counter rather than an event: the director polls it, which
            // cannot leave a subscription behind on a scene that has been unloaded.
            BodiesCompleted++;
        }

        /// <summary>
        /// Caches which animation cues the current controller understands. Re-reads only when the
        /// controller itself changes, which a deferred body does once as UMA finishes assembling it.
        /// </summary>
        private void RefreshAnimatorParameters()
        {
            var controller = animator.runtimeAnimatorController;
            if (ReferenceEquals(controller, inspectedController)) return;
            if (controller == null)
            {
                inspectedController = null;
                hasSpeedParam = hasSeatedParam = hasTalkingParam = hasListeningParam = hasArguingParam = false;
                hasRunningParam = hasPaceParam = false;
                hasDanceStyleParam = hasDanceOffsetParam = hasPoseParam = hasSeatedClapParam = hasSeatedVictoryParam = false;
                activityParams = 0;
                gestureParams = 0;
                return;
            }

            // An Animator reports no parameters until it has initialised, which on a deferred body
            // is a frame or two after the controller lands. Leaving the cache unset retries next
            // frame rather than concluding the controller understands nothing.
            if (animator.parameterCount == 0) return;

            inspectedController = controller;
            hasSpeedParam = hasSeatedParam = hasTalkingParam = hasListeningParam = hasArguingParam = false;
            hasRunningParam = hasPaceParam = false;
            hasDanceStyleParam = hasDanceOffsetParam = hasPoseParam = hasSeatedClapParam = hasSeatedVictoryParam = false;
            reactionParams = 0;
            activityParams = 0;
            gestureParams = 0;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == DanceStyleParam && parameter.type == AnimatorControllerParameterType.Int) hasDanceStyleParam = true;
                else if (parameter.nameHash == DanceOffsetParam && parameter.type == AnimatorControllerParameterType.Float) hasDanceOffsetParam = true;
                else if (parameter.nameHash == PoseParam && parameter.type == AnimatorControllerParameterType.Int) hasPoseParam = true;
                else if (parameter.nameHash == SeatedClapParam && parameter.type == AnimatorControllerParameterType.Trigger) hasSeatedClapParam = true;
                else if (parameter.nameHash == SeatedVictoryParam && parameter.type == AnimatorControllerParameterType.Trigger) hasSeatedVictoryParam = true;
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                    for (int i = 0; i < GestureParams.Length; i++)
                        if (parameter.nameHash == GestureParams[i]) gestureParams |= 1 << i;
                if (parameter.nameHash == SpeedParam && parameter.type == AnimatorControllerParameterType.Float)
                    hasSpeedParam = true;
                else if (parameter.nameHash == SeatedParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasSeatedParam = true;
                else if (parameter.nameHash == RunningParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasRunningParam = true;
                else if (parameter.nameHash == PaceParam && parameter.type == AnimatorControllerParameterType.Float)
                    hasPaceParam = true;
                else if (parameter.nameHash == TalkingParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasTalkingParam = true;
                else if (parameter.nameHash == ListeningParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasListeningParam = true;
                else if (parameter.nameHash == ArguingParam && parameter.type == AnimatorControllerParameterType.Bool)
                    hasArguingParam = true;
                else if (parameter.type == AnimatorControllerParameterType.Trigger)
                    for (int i = 0; i < ReactionParams.Length; i++)
                        if (parameter.nameHash == ReactionParams[i]) reactionParams |= 1 << i;
                if (parameter.type == AnimatorControllerParameterType.Bool)
                    for (int i = 0; i < ActivityParams.Length; i++)
                        if (parameter.nameHash == ActivityParams[i]) activityParams |= 1 << i;
            }
        }

        /// <summary>
        /// Finds the head so conversation can nod it. Prefers the humanoid rig mapping, which is
        /// O(1) and unambiguous, and only walks the hierarchy by name when there is no valid avatar.
        /// </summary>
        private void ResolveModelHead()
        {
            if (modelHead != null) return;

            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
                modelHead = animator.GetBoneTransform(HumanBodyBones.Head);

            if (modelHead == null)
            {
                var root = providedBody.Exists ? providedBody.Root.transform : null;
                if (root != null) modelHead = FindBone(root, "Head");
            }

            if (modelHead != null) modelHeadRest = modelHead.localRotation;
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;
            return null;
        }

        /// <summary>
        /// Tints only the wardrobe: a provider's body through its provider, which knows which of its
        /// overlays are clothes, and the primitive rig through its own fabric and accent.
        /// </summary>
        private void ApplyWardrobe(Color palette)
        {
            if (providedBody.Exists)
            {
                (providedBody.Owner ?? CharacterBodySource.Provider)?.SetWardrobeColor(providedBody, palette);
                return;
            }
            if (wardrobeMaterial != null) wardrobeMaterial.color = palette;
            if (accentMaterial != null && !fixedGoldAccent)
                accentMaterial.color = Color.Lerp(palette, Color.white, 0.55f);
        }

        private void BuildPrimitiveBody(string appearanceId, Color palette,
            bool diplomat, bool athlete, bool caregiver, bool wildcard, bool analyst)
        {
            var skinColor = diplomat ? Hex("C4956A") : athlete ? Hex("E8C4A0") :
                caregiver ? Hex("E8C4A0") : wildcard ? Hex("FFD9B3") : analyst ? Hex("FFECD2") : Hex("B07A52");
            var skin = MakeMaterial("Skin", skinColor);
            var fabric = MakeMaterial("Wardrobe", palette);
            var dark = MakeMaterial("Ink tailoring", Hex("1F2937"));
            var linen = MakeMaterial("Linen", Hex("E8E4DC"));
            var hair = MakeMaterial("Hair", wildcard ? Hex("8A6239") : caregiver ? Hex("5D4226") : Hex("211916"));
            var eyes = MakeMaterial("Eyes", Hex("25252B"));
            var accent = MakeMaterial("Accent", diplomat ? Hex("CAA24A") : Color.Lerp(palette, Color.white, 0.55f));
            wardrobeMaterial = fabric;
            accentMaterial = accent;
            fixedGoldAccent = diplomat;

            float shoulderWidth = athlete ? 0.29f : analyst ? 0.24f : 0.255f;
            float bodyWidth = athlete ? 0.52f : analyst ? 0.42f : 0.46f;
            Part("Hips", visual, PrimitiveType.Sphere, new Vector3(0, 0.89f, 0), new Vector3(bodyWidth * 0.91f, 0.23f, 0.28f), dark);
            chest = Joint("Chest", visual, new Vector3(0, 1.02f, 0));
            Part("Torso", chest, PrimitiveType.Capsule, new Vector3(0, 0.20f, 0), new Vector3(bodyWidth, 0.26f, 0.28f), fabric);
            Part("Neck", chest, PrimitiveType.Cylinder, new Vector3(0, 0.48f, 0), new Vector3(0.13f, 0.06f, 0.13f), skin);

            head = Joint("Head pivot", chest, new Vector3(0, 0.68f, 0));
            Part("Face", head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, 0.41f, 0.34f), skin);
            Part("Hair crown", head, PrimitiveType.Sphere, new Vector3(0, 0.145f, -0.022f), new Vector3(0.37f, 0.17f, 0.35f), hair);
            Part("Nose", head, PrimitiveType.Sphere, new Vector3(0, -0.012f, 0.171f), new Vector3(0.055f, 0.074f, 0.062f), skin);
            foreach (float side in new[] { -1f, 1f })
            {
                Part("Eye white", head, PrimitiveType.Sphere, new Vector3(side * 0.071f, 0.042f, 0.150f), new Vector3(0.065f, 0.043f, 0.025f), linen);
                Part("Eye", head, PrimitiveType.Sphere, new Vector3(side * 0.071f, 0.042f, 0.165f), new Vector3(0.025f, 0.031f, 0.017f), eyes);
                Part("Brow", head, PrimitiveType.Cube, new Vector3(side * 0.071f, 0.079f, 0.154f), new Vector3(0.069f, 0.014f, 0.016f), hair);
                Part("Ear", head, PrimitiveType.Sphere, new Vector3(side * 0.172f, 0, 0), new Vector3(0.058f, 0.104f, 0.057f), skin);
            }
            Part("Mouth", head, PrimitiveType.Cube, new Vector3(0, -0.094f, 0.151f), new Vector3(0.076f, 0.014f, 0.019f), MakeMaterial("Lip", Color.Lerp(skinColor, Hex("813F3F"), 0.45f)));

            if (diplomat)
            {
                Part("Bun", head, PrimitiveType.Sphere, new Vector3(0, 0.105f, -0.19f), new Vector3(0.23f, 0.24f, 0.23f), hair);
                Part("Blouse", chest, PrimitiveType.Cube, new Vector3(0, 0.19f, 0.143f), new Vector3(0.12f, 0.39f, 0.018f), linen);
                Part("Gold pendant", chest, PrimitiveType.Sphere, new Vector3(0, 0.35f, 0.165f), new Vector3(0.037f, 0.037f, 0.025f), accent);
                var leftLapel = Part("Left lapel", chest, PrimitiveType.Cube, new Vector3(-0.09f, 0.28f, 0.157f), new Vector3(0.07f, 0.28f, 0.025f), dark);
                leftLapel.localRotation = Quaternion.Euler(0, 0, 18);
                var rightLapel = Part("Right lapel", chest, PrimitiveType.Cube, new Vector3(0.09f, 0.28f, 0.157f), new Vector3(0.07f, 0.28f, 0.025f), dark);
                rightLapel.localRotation = Quaternion.Euler(0, 0, -18);
            }
            else if (athlete)
            {
                Part("Ponytail", head, PrimitiveType.Capsule, new Vector3(0, -0.02f, -0.215f), new Vector3(0.13f, 0.20f, 0.15f), hair);
                Part("Sports stripe", chest, PrimitiveType.Cube, new Vector3(0, 0.20f, 0.151f), new Vector3(0.065f, 0.35f, 0.017f), accent);
                Part("Headband", head, PrimitiveType.Cube, new Vector3(0, 0.112f, 0.135f), new Vector3(0.28f, 0.035f, 0.028f), accent);
            }
            else if (caregiver)
            {
                foreach (float side in new[] { -1f, 1f })
                    Part("Bob side", head, PrimitiveType.Capsule, new Vector3(side * 0.158f, 0.014f, -0.05f), new Vector3(0.115f, 0.14f, 0.25f), hair);
                Part("Cardigan opening", chest, PrimitiveType.Cube, new Vector3(0, 0.20f, 0.144f), new Vector3(0.13f, 0.39f, 0.020f), linen);
                Part("Pocket", chest, PrimitiveType.Cube, new Vector3(-0.14f, 0.15f, 0.14f), new Vector3(0.1f, 0.11f, 0.024f), accent);
            }
            else if (wildcard)
            {
                for (int i = 0; i < 7; i++)
                {
                    float angle = i * Mathf.PI * 2f / 7f;
                    Part("Curl", head, PrimitiveType.Sphere, new Vector3(Mathf.Sin(angle) * 0.14f, 0.13f, Mathf.Cos(angle) * 0.12f - 0.025f), Vector3.one * 0.16f, hair);
                }
                Part("Shirt placket", chest, PrimitiveType.Cube, new Vector3(0, 0.20f, 0.149f), new Vector3(0.026f, 0.37f, 0.018f), accent);
                for (int i = 0; i < 3; i++)
                    Part("Shirt button", chest, PrimitiveType.Sphere, new Vector3(0, 0.11f + i * 0.1f, 0.168f), Vector3.one * 0.023f, linen);
            }
            else if (analyst)
            {
                Part("Hood", chest, PrimitiveType.Sphere, new Vector3(0, 0.38f, -0.1f), new Vector3(0.36f, 0.22f, 0.23f), fabric);
                Part("Hoodie pocket", chest, PrimitiveType.Cube, new Vector3(0, 0.10f, 0.149f), new Vector3(0.23f, 0.11f, 0.022f), accent);
                foreach (float side in new[] { -1f, 1f })
                {
                    Part("Glasses top", head, PrimitiveType.Cube, new Vector3(side * 0.076f, 0.069f, 0.181f), new Vector3(0.11f, 0.014f, 0.018f), dark);
                    Part("Glasses bottom", head, PrimitiveType.Cube, new Vector3(side * 0.076f, 0.01f, 0.181f), new Vector3(0.11f, 0.014f, 0.018f), dark);
                    Part("Glasses outside", head, PrimitiveType.Cube, new Vector3(side * 0.13f, 0.04f, 0.175f), new Vector3(0.012f, 0.065f, 0.018f), dark);
                }
                Part("Glasses bridge", head, PrimitiveType.Cube, new Vector3(0, 0.049f, 0.182f), new Vector3(0.048f, 0.014f, 0.018f), dark);
            }
            else
            {
                Part("Swept hair", head, PrimitiveType.Sphere, new Vector3(-0.06f, 0.175f, 0.04f), new Vector3(0.26f, 0.10f, 0.25f), hair);
                Part("Player badge", chest, PrimitiveType.Cube, new Vector3(-0.13f, 0.29f, 0.15f), new Vector3(0.065f, 0.075f, 0.023f), linen);
            }

            leftArm = Arm("Left arm", -1, shoulderWidth, chest, skin, athlete ? skin : fabric);
            rightArm = Arm("Right arm", 1, shoulderWidth, chest, skin, athlete ? skin : fabric);
            leftLeg = Leg("Left leg", -0.115f, visual, dark, linen, out leftKnee);
            rightLeg = Leg("Right leg", 0.115f, visual, dark, linen, out rightKnee);
        }

        private void LateUpdate()
        {
            if (!built || visual == null) return;
            TickDressing();
            var delta = transform.position - previousPosition;
            previousPosition = transform.position;
            delta.y = 0;
            float speed = Time.deltaTime > 0.001f ? delta.magnitude / Time.deltaTime : 0f;
            // Teleports reposition the actor without producing a false running animation.
            float target = speed > 8f || seated ? 0f : Mathf.Clamp01(speed / 2.5f);
            groundSpeed = Mathf.Lerp(groundSpeed, speed > 8f || seated ? 0f : speed, 1f - Mathf.Exp(-8f * Time.deltaTime));
            movementBlend = Mathf.Lerp(movementBlend, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (!float.IsNaN(facingYaw) && movementBlend < 0.02f) SettleFacing();

            if (providedBody.Exists) { AnimateProvidedBody(); return; }
            AnimatePrimitives();
        }

        /// <summary>
        /// Drives a body a provider built. The animator is looked up again here for one specific
        /// reason: UMA replaces the avatar's Animator while it assembles the character, so
        /// the reference captured when the body was handed over can be destroyed out from under us.
        /// Falling through to <see cref="AnimatePrimitives"/> on a null animator would then drive a
        /// primitive rig that was never built for this houseguest, which is a null reference every
        /// frame rather than a missing animation.
        /// </summary>
        private void AnimateProvidedBody()
        {
            BodyArrived();
            if (animator == null)
            {
                animator = providedBody.Root.GetComponentInChildren<Animator>(true);
                if (animator == null) return;
                animator.applyRootMotion = false;
                inspectedController = null;
            }
            AnimateModel();
        }

        private void AnimateModel()
        {
            // Not every rig's controller carries both cues — UMA's shipped locomotion has Speed but
            // no Seated — and driving a parameter a controller does not declare warns once per call.
            RefreshAnimatorParameters();
            // A swimmer doing a length strokes while the root waits at the side, so the speed the
            // stroke is chosen by comes from the activity, not from a root that is not moving.
            if (hasSpeedParam) animator.SetFloat(SpeedParam, activity == BodyActivity.Swimming ? (activityMoving ? 1f : 0f) : movementBlend);
            if (hasSeatedParam) animator.SetBool(SeatedParam, seated);
            if (hasRunningParam) animator.SetBool(RunningParam, running && !seated);
            if (hasPaceParam) animator.SetFloat(PaceParam, Pace);
            // Reduced motion keeps the authored talk loops off: the head-nod cue below is already
            // gated on it, and a gesturing body is the same kind of motion at a larger size.
            if (hasTalkingParam) animator.SetBool(TalkingParam, talking && speaking && !reducedMotion);
            if (hasListeningParam) animator.SetBool(ListeningParam, talking && !speaking && !reducedMotion);
            // The tense loop is the talk loop with the arms working, so it follows the same rule.
            if (hasArguingParam) animator.SetBool(ArguingParam, IsArguing);
            // Lying and floating are where a body is, and a pose is a body holding still, so they
            // hold under reduced motion; the stove and the dance floor are motion, and follow the
            // talk loops' rule.
            for (int i = 0; i < ActivityParams.Length; i++)
                if ((activityParams & (1 << i)) != 0)
                {
                    var cue = (BodyActivity)(i + 1);
                    bool still = cue == BodyActivity.Sleeping || cue == BodyActivity.Swimming || cue == BodyActivity.Posing;
                    animator.SetBool(ActivityParams[i], activity == cue && (still || !reducedMotion));
                }
            if (hasDanceStyleParam) animator.SetInteger(DanceStyleParam, (int)danceStyle);
            // The same start for as long as the body dances; only the next dance takes a new one.
            if (hasDanceOffsetParam) animator.SetFloat(DanceOffsetParam, float.IsNaN(danceStart) ? Mathf.Repeat(phaseOffset / 6.28f, 1f) : danceStart);
            if (hasPoseParam) animator.SetInteger(PoseParam, (int)pose);
            // A seated gesture is answered from the chair; one still waiting when the body stands
            // would fire the next time it sat down, at whatever was happening then.
            if (seatedGesturePending && !seated)
            {
                if (hasSeatedClapParam) animator.ResetTrigger(SeatedClapParam);
                if (hasSeatedVictoryParam) animator.ResetTrigger(SeatedVictoryParam);
                seatedGesturePending = false;
            }

            // A provided body streams its rig in over a few frames. Retry on a slow cadence so the
            // hierarchy walk behind ResolveModelHead cannot become a per-frame cost on a body that
            // genuinely has no head bone.
            if (modelHead == null && providedBody.Exists && (Time.frameCount & 7) == 0)
                ResolveModelHead();

            // The authored clips carry no dialogue pose, so conversation keeps the original
            // head-nod cue, layered over whatever state the Animator is playing.
            if (modelHead == null) return;
            if (talking && !reducedMotion)
            {
                float time = Time.time + phaseOffset;
                modelHead.localRotation = modelHeadRest * Quaternion.Euler(Mathf.Sin(time * 3f) * 4f, Mathf.Sin(time * 1.7f) * 3f, 0f);
            }
            ApplyLook(modelHead);
        }

        /// <summary>
        /// Turns a stopped body to its conversation heading. Only the root turns, and only while
        /// nothing is moving it: a navigating agent owns the heading until it arrives, and the
        /// movement blend is what says it has.
        /// </summary>
        private void SettleFacing()
        {
            var wanted = Quaternion.Euler(0f, facingYaw, 0f);
            transform.rotation = reducedMotion ? wanted
                : Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private void AnimatePrimitives()
        {
            walkPhase += Time.deltaTime * Mathf.Lerp(3f, 10f, movementBlend);
            float time = Time.time + phaseOffset;
            float swing = Mathf.Sin(walkPhase) * 27f * movementBlend;
            float gesture = talking && !reducedMotion && !seated ? Mathf.Sin(time * 2.8f) * 9f - 13f : 0f;
            float idle = reducedMotion || seated ? 0f : Mathf.Sin(time * 1.6f) * 0.004f * (1f - movementBlend);
            visual.localPosition = new Vector3(0, seated ? -0.32f : idle, 0);
            chest.localRotation = Quaternion.Euler(0, 0, reducedMotion || seated ? 0f : Mathf.Sin(time * 0.9f) * 0.65f);
            head.localRotation = Quaternion.Euler(talking && !reducedMotion ? Mathf.Sin(time * 3f) * 2f : 0f, 0f, 0f);
            ApplyLook(head);
            leftArm.localRotation = Quaternion.Euler(-swing + gesture, 0f, seated ? 0f : 7f);
            rightArm.localRotation = Quaternion.Euler(swing + gesture * 0.35f, 0f, seated ? 0f : -7f);
            leftLeg.localRotation = Quaternion.Euler(seated ? -82f : swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(seated ? -82f : -swing, 0f, 0f);
            leftKnee.localRotation = Quaternion.Euler(seated ? 82f : Mathf.Max(0f, -swing) * 0.8f, 0f, 0f);
            rightKnee.localRotation = Quaternion.Euler(seated ? 82f : Mathf.Max(0f, swing) * 0.8f, 0f, 0f);
        }

        private Transform Arm(string name, float side, float width, Transform parent, Material skin, Material sleeve)
        {
            var joint = Joint(name, parent, new Vector3(side * width, 0.34f, 0));
            Part("Upper arm", joint, PrimitiveType.Capsule, new Vector3(0, -0.15f, 0), new Vector3(0.13f, 0.15f, 0.13f), sleeve);
            Part("Forearm", joint, PrimitiveType.Capsule, new Vector3(0, -0.39f, 0.018f), new Vector3(0.10f, 0.12f, 0.10f), skin);
            Part("Hand", joint, PrimitiveType.Sphere, new Vector3(0, -0.535f, 0.022f), new Vector3(0.095f, 0.13f, 0.075f), skin);
            return joint;
        }

        private Transform Leg(string name, float x, Transform parent, Material pants, Material shoes, out Transform knee)
        {
            var joint = Joint(name, parent, new Vector3(x, 0.88f, 0));
            Part("Thigh", joint, PrimitiveType.Capsule, new Vector3(0, -0.18f, 0), new Vector3(0.17f, 0.19f, 0.18f), pants);
            knee = Joint("Knee", joint, new Vector3(0, -0.36f, 0));
            Part("Shin", knee, PrimitiveType.Capsule, new Vector3(0, -0.20f, 0), new Vector3(0.13f, 0.20f, 0.14f), pants);
            Part("Shoe", knee, PrimitiveType.Cube, new Vector3(0, -0.44f, 0.055f), new Vector3(0.16f, 0.12f, 0.28f), shoes);
            return joint;
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            return joint;
        }

        private static Transform Part(string name, Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Gamesim " + name, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
            material.enableInstancing = true;
            materials.Add(material);
            return material;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        /// <summary>
        /// Public so portraits resolve a houseguest to the same persona the in-world model uses;
        /// a portrait that disagreed with the character standing in the room would be worse than none.
        /// </summary>
        public static string AppearanceId(ContestantState character, string canonicalId)
        {
            string templateId = character.appearance?.presetId ?? character.sourceTemplateId;
            if (!string.IsNullOrEmpty(templateId) && templateId != ContentCatalog.PlayerId)
            {
                var template = CastTemplates.Find(templateId);
                if (template != null)
                {
                    var source = CastTemplates.ToContestant(template, false);
                    return LegacyAppearanceId(source, template.Id);
                }
            }
            if (!string.IsNullOrEmpty(character.appearance?.fallbackId)) return character.appearance.fallbackId;
            return LegacyAppearanceId(character, canonicalId);
        }

        private static string LegacyAppearanceId(ContestantState character, string canonicalId)
        {
            if (character.isPlayer) return ContentCatalog.PlayerId;
            switch (canonicalId)
            {
                case "maya-hassan": case "taylor-kim": case "jamie-roberts":
                case "casey-wilson": case "riley-johnson":
                // The All-Stars roster's own look key: UMA's cast library has his look under it.
                case "dan-gheesling": return canonicalId;
            }
            // Imported identities retain their real IDs. Select a native visual recipe from traits,
            // with a deterministic ID-only variation; do not infer identity from a displayed name.
            if (character.traits.Contains("Competitive") || character.traits.Contains("Confrontational")) return "taylor-kim";
            if (character.traits.Contains("Emotional") || character.traits.Contains("Loyal")) return "jamie-roberts";
            if (character.traits.Contains("Analytical") || character.traits.Contains("Introverted")) return "riley-johnson";
            uint hash = 2166136261;
            foreach (char value in canonicalId ?? string.Empty) hash = (hash ^ value) * 16777619;
            return (hash & 1) == 0 ? "maya-hassan" : "casey-wilson";
        }

        public static string AppearanceKeyFor(ContestantState character) => character?.appearance?.ContentKey()
            ?? "legacy:" + (character?.sourceTemplateId ?? character?.id ?? ContentCatalog.PlayerId);

        private void OnDestroy()
        {
            ReleasePresentation();
        }

        private void ReleasePresentation()
        {
            CancelDressing();
            foreach (var old in replacedRenderers) if (old != null) old.enabled = true;
            replacedRenderers.Clear();
            if (visual != null)
            {
                visual.name = "Retired Gamesim Character Visual";
                visual.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(visual.gameObject); else DestroyImmediate(visual.gameObject);
            }
            foreach (var material in materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            }
            materials.Clear();
            wardrobeMaterial = null;
            accentMaterial = null;
            visual = null;
            chest = head = leftArm = rightArm = leftLeg = rightLeg = leftKnee = rightKnee = null;
            animator = null;
            modelHead = null;
            providedBody = default;
            awaitingBody = false;
            inspectedController = null;
            hasSpeedParam = hasSeatedParam = hasTalkingParam = hasListeningParam = hasArguingParam = false;
            hasRunningParam = hasPaceParam = false;
            built = false;
            talking = seated = arguing = false; facingYaw = float.NaN; lookTarget = null; lookBlend = 0f;
            activity = BodyActivity.None; activityMoving = false; activityParams = 0;
            movementBlend = walkPhase = 0f;
            CharacterId = null;
        }
    }
}
