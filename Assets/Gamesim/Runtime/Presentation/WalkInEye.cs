using System.Collections.Generic;
using Gamesim.House;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Keeps the walk-in's view of the entrant clear. The push-in that opens the walk-in follows the
    /// player along an authored eye path through the house's yard opening. The camera rig does not pull
    /// a subject-framed shot in at walls (it would cut inside the moving body), so nothing else would
    /// notice if a prop, a panel or a wall came to stand between that eye and the player's head.
    ///
    /// <para>Each frame the eye the path gives is checked against the things that could stand in the
    /// way, read once when the walk-in is framed. While it is clear it is used exactly as it is. When it
    /// is not, the eye moves to the nearest of a few places around it (to either side, higher, closer)
    /// that sees the head with the fewest of them in the way, counted the way the introductions count
    /// the props across a face. It stays there while that place sees clearly, even once the authored
    /// eye clears again, and it eases there and back rather than cutting, so the eye never jumps
    /// mid-walk. Only the very first frame, taken under the house-entry card, is placed at once.</para>
    /// </summary>
    public sealed class WalkInEye
    {
        /// <summary>
        /// The places tried, nearest first, as metres to the right of the view, up, and toward the
        /// subject. The first is the authored eye itself.
        /// </summary>
        public static readonly Vector3[] Candidates =
        {
            Vector3.zero,
            new Vector3(.3f, 0f, 0f), new Vector3(-.3f, 0f, 0f), new Vector3(0f, .3f, 0f),
            new Vector3(0f, 0f, .4f),
            new Vector3(.3f, .3f, 0f), new Vector3(-.3f, .3f, 0f),
            new Vector3(0f, .3f, .4f),
            new Vector3(.6f, 0f, 0f), new Vector3(-.6f, 0f, 0f), new Vector3(0f, .6f, 0f),
            new Vector3(.6f, .3f, 0f), new Vector3(-.6f, .3f, 0f),
            new Vector3(0f, 0f, .8f),
        };

        /// <summary>How fast the eye may move off its path, in metres a second: about a walking pace.</summary>
        public const float MaxSpeed = 1.5f;

        /// <summary>How long the eye takes to settle on a new place, in seconds.</summary>
        public const float Ease = .3f;

        /// <summary>How far from the entrant's head things are read as possibly in the way, in metres.</summary>
        public const float SightRadius = 6f;

        private int chosen;
        private bool placed;
        private Vector3 applied, velocity;

        /// <summary>Which of <see cref="Candidates"/> the eye is on or moving to; 0 is the authored eye.</summary>
        public int Chosen => chosen;

        /// <summary>Back on the authored eye, to be placed at once on the next frame: a new walk-in.</summary>
        public void Reset()
        {
            chosen = 0;
            placed = false;
            applied = velocity = Vector3.zero;
        }

        /// <summary>
        /// Where the eye goes this frame: the authored eye while it sees <paramref name="head"/> clearly,
        /// otherwise on its way to the nearest place that does. <paramref name="seconds"/> is the frame's
        /// time, which the easing runs on.
        /// </summary>
        public Vector3 Place(Vector3 authored, Vector3 head, IReadOnlyList<Bounds> sight, float seconds)
        {
            if (sight != null && sight.Count > 0)
            {
                int current = Blockers(sight, Candidate(authored, head, chosen), head);
                if (current > 0)
                {
                    // Only a place strictly better than this one is worth moving to; a tie stays.
                    int best = chosen;
                    for (int i = 0; i < Candidates.Length && current > 0; i++)
                    {
                        if (i == chosen) continue;
                        int score = Blockers(sight, Candidate(authored, head, i), head);
                        if (score < current) { current = score; best = i; }
                    }
                    chosen = best;
                }
            }
            var target = Candidate(authored, head, chosen) - authored;
            if (!placed)
            {
                applied = target;
                velocity = Vector3.zero;
                placed = true;
            }
            else if (seconds > 0f)
            {
                var eased = Vector3.SmoothDamp(applied, target, ref velocity, Ease, MaxSpeed, seconds);
                applied = Vector3.MoveTowards(applied, eased, MaxSpeed * seconds);
            }
            return authored + applied;
        }

        /// <summary>
        /// Candidate <paramref name="index"/> for an eye at <paramref name="authored"/> looking at
        /// <paramref name="head"/>: right and toward are taken level, along the view.
        /// </summary>
        public static Vector3 Candidate(Vector3 authored, Vector3 head, int index)
        {
            var toward = head - authored;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, toward);
            var offset = Candidates[index];
            return authored + right * offset.x + Vector3.up * offset.y + toward * offset.z;
        }

        /// <summary>How many of the boxes stand between the eye and the head.</summary>
        public static int Blockers(IReadOnlyList<Bounds> sight, Vector3 eye, Vector3 head)
        {
            int count = 0;
            if (sight == null) return 0;
            for (int i = 0; i < sight.Count; i++)
                if (Crosses(sight[i], eye, head)) count++;
            return count;
        }

        /// <summary>Whether the segment from <paramref name="from"/> to <paramref name="to"/> enters the box, starting inside it included.</summary>
        public static bool Crosses(Bounds box, Vector3 from, Vector3 to)
        {
            Vector3 min = box.min, max = box.max, step = to - from;
            float enter = 0f, leave = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float start = from[axis], along = step[axis];
                if (Mathf.Abs(along) < 1e-6f)
                {
                    if (start < min[axis] || start > max[axis]) return false;
                    continue;
                }
                float a = (min[axis] - start) / along, b = (max[axis] - start) / along;
                if (a > b) { float swap = a; a = b; b = swap; }
                enter = Mathf.Max(enter, a);
                leave = Mathf.Min(leave, b);
                if (enter > leave) return false;
            }
            return true;
        }

        /// <summary>
        /// What could stand between the walk-in's eye and the entrant, within <paramref name="radius"/> of
        /// <paramref name="around"/>: every drawn prop, panel and sign above the knee, and the house's walls
        /// and fences by their colliders, since the authored shell draws them all as one mesh whose box
        /// covers the whole house. Left out: bodies, screen and world text, the door set's glow, and any
        /// mesh both wide and deep (a floor, a rug, a ceiling, the shell), whose box is not where it stands.
        /// </summary>
        public static List<Bounds> SightBoxes(Vector3 around, float radius)
        {
            var boxes = new List<Bounds>();
            float reach = radius * radius;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy
                    || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var box = renderer.bounds;
                if (!Near(box, around, reach) || !Standing(box)) continue;
                if (IsBody(renderer.transform) || renderer.GetComponentInParent<Canvas>() != null
                    || renderer.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (renderer.shadowCastingMode == ShadowCastingMode.Off && renderer.GetComponentInParent<OpeningDoorSet>() != null) continue;
                boxes.Add(box);
            }
            foreach (var wall in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                if (!wall.enabled || wall.isTrigger || !wall.gameObject.activeInHierarchy
                    || (HouseLayers.Sight & (1 << wall.gameObject.layer)) == 0) continue;
                var box = wall.bounds;
                var size = box.size;
                // The shell's own test for a wall it draws over: tall, thin and long.
                bool wallLike = size.y >= 1f && Mathf.Min(size.x, size.z) <= .3f && Mathf.Max(size.x, size.z) >= 1f;
                if (!wallLike || !Near(box, around, reach) || IsBody(wall.transform)) continue;
                boxes.Add(box);
            }
            return boxes;
        }

        private static bool Near(Bounds box, Vector3 point, float reach)
        {
            var closest = Vector3.Max(box.min, Vector3.Min(point, box.max));
            return (closest - point).sqrMagnitude <= reach;
        }

        private static bool Standing(Bounds box)
        {
            var size = box.size;
            return box.max.y > .9f && (Mathf.Max(size.x, size.z) < 1.6f || Mathf.Min(size.x, size.z) <= .3f);
        }

        private static bool IsBody(Transform node) =>
            node.GetComponentInParent<HouseNpc>() != null || node.GetComponentInParent<HousePlayerController>() != null
            || node.GetComponentInParent<CharacterPresentation>() != null;
    }
}
