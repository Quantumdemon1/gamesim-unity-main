using Gamesim.House;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Two houseguests embrace (the art brief's R5): both bodies play React_embrace, facing, the
    /// take's own distance apart. Presentation only - nothing here is saved or read by the engine,
    /// and nothing here chooses who: the director asks for a pair the player saw make peace.
    ///
    /// <para>The steps: each turns to face the other, waiting at most <see cref="TurnSeconds"/>
    /// for the bodies to come round; both agents park, so neither has to step round the other; each
    /// visual body eases toward the other by half the gap beyond the take's distance, and
    /// <see cref="SideStep"/> to its own right, over <see cref="CloseSeconds"/>; and both beats are asked for in the same frame. The roots never
    /// move - the agents keep their places on the floor - and only the visual bodies close in.</para>
    ///
    /// <para>One release path, for both bodies, on anything that ends it: the beat played out, a
    /// body gone, re-dressed or assembling, a root moved, a body seated, given an activity or set
    /// walking, motion reduced, this disabled, or the director's word (a ceremony, an opening, a
    /// competition, a departure, somebody leaving the house). On release each beat is cancelled,
    /// each visual body eases home within <see cref="ReleaseSeconds"/>, the agents unpark and each
    /// body is given back the heading it had.</para>
    /// </summary>
    public sealed class EmbracePair : MonoBehaviour
    {
        /// <summary>
        /// The take's distance, root to root, on the Mixamo skeleton it was authored on
        /// (ArtSource/animation/bb_anim_react_embrace.py's D_STAR). The brief's 0.46 left the back
        /// targets at arm's length unless the bodies leaned in far enough to carry their centre of
        /// mass 5 cm off the root; 0.40 still keeps the at-ease stances' toes 0.16 m apart.
        /// </summary>
        public const float PairDistance = .40f;
        /// <summary>
        /// How far each visual body also steps to its own right of the line between the roots, on
        /// the Mixamo skeleton (the take's SIDE_STEP): the two heads pass each other. The take's
        /// lean alone, retargeted onto two UMA bodies, put their head joints 0.092 m apart across
        /// that line, the heads touching in the capture.
        /// </summary>
        public const float SideStep = .035f;
        /// <summary>The Mixamo skeleton's own humanScale: the pair distance is scaled by the pair's mean over this.</summary>
        public const float SkeletonHumanScale = 1.0419588f;
        /// <summary>How far apart two roots may stand to begin: nearer and the bodies are already in each other; farther and the visual bodies would leave their roots too far behind.</summary>
        public const float NearestApart = .6f, FarthestApart = 1.2f;
        public const float TurnSeconds = .6f, TurnTolerance = 5f, CloseSeconds = .35f, ReleaseSeconds = .3f;
        /// <summary>How far a root may move before the embrace lets go.</summary>
        public const float RootDrift = .25f;

        public enum Phase { Turning, Closing, Holding, Releasing, Done }

        public CharacterPresentation First { get; private set; }
        public CharacterPresentation Second { get; private set; }
        public Phase Stage { get; private set; }
        /// <summary>While the two are turning to, closing on or holding each other.</summary>
        public bool Active => Stage == Phase.Turning || Stage == Phase.Closing || Stage == Phase.Holding;
        /// <summary>Once it has let go, for whatever reason.</summary>
        public bool Released => Stage == Phase.Releasing || Stage == Phase.Done;
        public string ReleaseReason { get; private set; }
        /// <summary>The pair's distance, root to root, for these two bodies.</summary>
        public float Distance { get; private set; }
        /// <summary>Each body's step to its own right, for these two bodies.</summary>
        public float Step { get; private set; }

        private sealed class Side
        {
            public CharacterPresentation body;
            public Transform visual;
            public HouseNpcMotion motion;
            public Vector3 rootAt, visualHome, offset;
            public float facingBefore, closeBy;
            public bool parked;
        }

        private Side a, b;
        private float clock, releaseFrom;
        private bool fired, reacted;

        /// <summary>
        /// Why these two cannot embrace now, or null when they can: both bodies have the take, stand,
        /// are at liberty and still, are whole and dressed, are not the player, and stand between
        /// <see cref="NearestApart"/> and <see cref="FarthestApart"/> apart; and motion is not reduced.
        /// </summary>
        public static string Refusal(CharacterPresentation first, CharacterPresentation second, string playerId)
        {
            if (first == null || second == null || first == second) return "two bodies are needed";
            foreach (var body in new[] { first, second })
            {
                if (!string.IsNullOrEmpty(playerId) && body.CharacterId == playerId) return body.CharacterId + " is the player, whose body acts only on the player's word";
                if (!body.Supports(CharacterPresentation.Reaction.Embrace)) return body.CharacterId + " has no embrace to play";
                if (body.ReducedMotion) return "motion is reduced";
                if (body.IsSeated) return body.CharacterId + " is seated";
                if (body.Activity != CharacterPresentation.BodyActivity.None) return body.CharacterId + " is busy";
                if (!body.IsStill) return body.CharacterId + " is moving";
                if (body.IsBodyAssembling || body.IsChangingOutfit) return body.CharacterId + " is being dressed";
                if (body.VisualRoot == null) return body.CharacterId + " has no body";
            }
            float apart = Flat(first.transform.position - second.transform.position).magnitude;
            if (apart < NearestApart || apart > FarthestApart) return "they stand " + apart.ToString("0.00") + " m apart";
            return null;
        }

        /// <summary>
        /// Begins an embrace between two bodies, or returns null - having turned their heads to each
        /// other instead - when <see cref="Refusal"/> says they cannot.
        /// </summary>
        public static EmbracePair TryBegin(CharacterPresentation first, CharacterPresentation second, string playerId = null)
        {
            string refusal = Refusal(first, second, playerId);
            if (refusal != null)
            {
                if (first != null && second != null && first != second)
                {
                    first.LookAt(second.transform, 4f);
                    second.LookAt(first.transform, 4f);
                }
                return null;
            }
            var holder = new GameObject("Embrace " + first.CharacterId + " + " + second.CharacterId);
            var pair = holder.AddComponent<EmbracePair>();
            pair.Begin(first, second);
            return pair;
        }

        private void Begin(CharacterPresentation first, CharacterPresentation second)
        {
            First = first; Second = second;
            a = Make(first); b = Make(second);
            float scale = (HumanScale(first) + HumanScale(second)) * .5f / SkeletonHumanScale;
            Distance = PairDistance * scale;
            Step = SideStep * scale;
            Face(a, b); Face(b, a);
            Stage = Phase.Turning;
            clock = 0f;
        }

        private static Side Make(CharacterPresentation body) => new Side
        {
            body = body, visual = body.VisualRoot, motion = body.GetComponent<HouseNpcMotion>(),
            rootAt = body.transform.position, visualHome = body.VisualRoot.localPosition, facingBefore = body.FacingYaw,
        };

        private static float HumanScale(CharacterPresentation body)
        {
            var animator = body.VisualRoot != null ? body.VisualRoot.GetComponentInChildren<Animator>() : null;
            return animator != null && animator.isHuman && animator.humanScale > .1f ? animator.humanScale : SkeletonHumanScale;
        }

        private static void Face(Side side, Side toward)
        {
            var offset = Flat(toward.body.transform.position - side.body.transform.position);
            if (offset.sqrMagnitude > 1e-4f) side.body.SetFacing(Quaternion.LookRotation(offset).eulerAngles.y);
        }

        /// <summary>Lets go: the single path every ending takes. Safe to call more than once.</summary>
        public void Release(string reason)
        {
            if (Released) return;
            ReleaseReason = reason;
            Stage = Phase.Releasing;
            releaseFrom = clock = 0f;
            foreach (var side in new[] { a, b })
            {
                if (side == null || side.body == null) continue;
                side.body.CancelReaction();
                side.body.SetFacing(side.facingBefore);
                if (side.parked && side.motion != null) side.motion.SetParked(false);
                side.parked = false;
            }
        }

        private void LateUpdate()
        {
            if (Stage == Phase.Done) return;
            float dt = Time.deltaTime;
            clock += dt;
            if (Stage == Phase.Releasing) { EaseHome(dt); return; }
            string broken = Broken();
            if (broken != null) { Release(broken); EaseHome(dt); return; }
            switch (Stage)
            {
                case Phase.Turning:
                    if ((Facing(a, b) && Facing(b, a)) || clock >= TurnSeconds)
                    {
                        foreach (var side in new[] { a, b })
                            if (side.motion != null && !side.motion.IsParked) { side.motion.SetParked(true); side.parked = true; }
                        float gap = Flat(a.body.transform.position - b.body.transform.position).magnitude;
                        a.closeBy = b.closeBy = Mathf.Max(0f, (gap - Distance) * .5f);
                        Stage = Phase.Closing;
                        clock = 0f;
                    }
                    break;
                case Phase.Closing:
                {
                    float t = Mathf.Clamp01(clock / CloseSeconds);
                    float eased = t * t * (3f - 2f * t);
                    Place(a, b, a.closeBy * eased, Step * eased);
                    Place(b, a, b.closeBy * eased, Step * eased);
                    if (t >= 1f)
                    {
                        // Both beats in the same frame, so the two bodies move as one.
                        a.body.React(CharacterPresentation.Reaction.Embrace);
                        b.body.React(CharacterPresentation.Reaction.Embrace);
                        fired = true;
                        Stage = Phase.Holding;
                        clock = 0f;
                    }
                    break;
                }
                case Phase.Holding:
                    Place(a, b, a.closeBy, Step);
                    Place(b, a, b.closeBy, Step);
                    if (a.body.IsReacting && b.body.IsReacting) reacted = true;
                    // The take has played out and handed back to idle, or never started at all.
                    if ((reacted && !a.body.IsReacting && !b.body.IsReacting) || (!reacted && clock > 1f)) Release(reacted ? "done" : "the beat never began");
                    break;
            }
        }

        /// <summary>Why the embrace must let go now, or null.</summary>
        private string Broken()
        {
            foreach (var side in new[] { a, b })
            {
                if (side.body == null || side.visual == null) return "a body is gone";
                if (side.body.VisualRoot != side.visual) return side.body.CharacterId + " was dressed again";
                if (side.body.IsBodyAssembling || side.body.IsChangingOutfit) return side.body.CharacterId + " is being dressed";
                if (Flat(side.body.transform.position - side.rootAt).magnitude > RootDrift) return side.body.CharacterId + " moved";
                if (side.body.IsSeated) return side.body.CharacterId + " sat down";
                if (side.body.Activity != CharacterPresentation.BodyActivity.None) return side.body.CharacterId + " is busy";
                if (!side.body.IsStill) return side.body.CharacterId + " is walking";
                if (side.body.ReducedMotion) return "motion is reduced";
            }
            return null;
        }

        private bool Facing(Side side, Side toward)
        {
            var offset = Flat(toward.body.transform.position - side.body.transform.position);
            if (offset.sqrMagnitude < 1e-4f) return true;
            return Vector3.Angle(Flat(side.body.transform.forward), offset) <= TurnTolerance;
        }

        /// <summary>Moves a visual body toward its partner by a distance and to its own right by another, in its root's frame, re-reading the visual root each frame.</summary>
        private static void Place(Side side, Side toward, float by, float aside)
        {
            if (side.body.VisualRoot != side.visual || side.visual == null) return;
            var direction = Flat(toward.body.transform.position - side.body.transform.position);
            if (direction.sqrMagnitude < 1e-4f) return;
            direction.Normalize();
            var world = direction * by + Vector3.Cross(Vector3.up, direction) * aside;
            var parent = side.visual.parent;
            side.offset = parent != null ? parent.InverseTransformVector(world) : world;
            side.visual.localPosition = side.visualHome + side.offset;
        }

        private void EaseHome(float dt)
        {
            releaseFrom += dt;
            float t = Mathf.Clamp01(releaseFrom / ReleaseSeconds);
            bool home = true;
            foreach (var side in new[] { a, b })
            {
                if (side == null || side.visual == null) continue;
                side.visual.localPosition = side.visualHome + side.offset * (1f - t);
                home &= t >= 1f;
            }
            if (!home) return;
            Stage = Phase.Done;
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (Stage == Phase.Done) return;
            Release(ReleaseReason ?? "disabled");
            // No frame is coming to ease them home: home now.
            foreach (var side in new[] { a, b })
                if (side != null && side.visual != null) side.visual.localPosition = side.visualHome;
            Stage = Phase.Done;
        }

        /// <summary>The visual body's offset from home, metres, for the tests.</summary>
        public float OffsetOf(CharacterPresentation body)
        {
            var side = a != null && a.body == body ? a : b != null && b.body == body ? b : null;
            return side == null || side.visual == null ? 0f : (side.visual.localPosition - side.visualHome).magnitude;
        }

        /// <summary>Whether both beats were asked for in one frame.</summary>
        public bool Fired => fired;

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
