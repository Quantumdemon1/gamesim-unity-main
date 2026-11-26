using System;
using System.Numerics;

namespace Gamesim.Episode
{
    /// <summary>
    /// The conversation's soft key (presentation only): while a conversation's two-shot holds the camera, a
    /// small unshadowed spot stands on the camera's side of the pair, a little above and to the right, aimed
    /// at the midpoint of their two heads, its even part wide enough for both faces, so dark hair and dark
    /// clothes read against the room behind them. It eases in and out over <see cref="EaseSeconds"/>, and
    /// cuts under reduced motion. The director builds and moves it (EpisodeDirector.ShotLights.cs); this
    /// is the arithmetic, Unity-free on purpose, so the Unity-free suite checks where it stands, what it
    /// covers and how far it reaches. Units are metres, y up, in Unity's world axes.
    /// </summary>
    public static class ConversationKeyLight
    {
        /// <summary>The light's object name in the scene.</summary>
        public const string Name = "Conversation key light";

        /// <summary>Its full strength: a soft key, not a stage spot (the introduction's key is 6).</summary>
        public const float Intensity = 1.6f;

        /// <summary>Its colour: neutral, a touch warm.</summary>
        public const float Red = .97f, Green = .95f, Blue = .92f;

        /// <summary>How long it takes to come up, and to go down.</summary>
        public const float EaseSeconds = .3f;

        /// <summary>The furthest it ever reaches, so it lights the pair and not the room.</summary>
        public const float MaxRange = 4f;

        /// <summary>How far it reaches past the farther face.</summary>
        public const float Reach = 1.2f;

        /// <summary>Where it stands from the midpoint of the heads: this far toward the camera, along the floor; this far up; this far to the camera's right.</summary>
        public const float Toward = 1.5f, Lift = .35f, Side = .4f;

        /// <summary>A face's half-width, which the even part of the cone keeps inside it.</summary>
        public const float FaceRadius = .2f;

        /// <summary>The even part of the cone stays within these, and the soft edge outside it is this wide, up to the widest cone.</summary>
        public const float MinInnerAngle = 20f, MaxInnerAngle = 100f, Softness = 24f, MaxSpotAngle = 130f;

        /// <summary>Where the light stands and how it is shaped, for one frame.</summary>
        public readonly struct Placement
        {
            public readonly Vector3 Position, Forward;
            public readonly float InnerSpotAngle, SpotAngle, Range;

            public Placement(Vector3 position, Vector3 forward, float innerSpotAngle, float spotAngle, float range)
            {
                Position = position;
                Forward = forward;
                InnerSpotAngle = innerSpotAngle;
                SpotAngle = spotAngle;
                Range = range;
            }
        }

        /// <summary>
        /// Where the key stands for the pair's heads <paramref name="firstHead"/> and <paramref name="secondHead"/>,
        /// seen by a camera at <paramref name="eye"/>: on the camera's side along the floor, lifted and set to the
        /// camera's right, aimed at the midpoint of the heads, its even part covering both faces.
        /// </summary>
        public static Placement Place(Vector3 firstHead, Vector3 secondHead, Vector3 eye)
        {
            var middle = (firstHead + secondHead) * .5f;
            var toEye = eye - middle;
            var flat = new Vector3(toEye.X, 0f, toEye.Z);
            // A camera straight overhead has no side along the floor: any will do, so the key stands toward -z.
            var toward = flat.LengthSquared() > 1e-4f ? Vector3.Normalize(flat) : new Vector3(0f, 0f, -1f);
            // The camera's right as Unity's Vector3.Cross(up, forward) gives it, the camera looking back along toward.
            var right = Vector3.Cross(Vector3.UnitY, -toward);
            var position = middle + toward * Toward + Vector3.UnitY * Lift + right * Side;
            var forward = Vector3.Normalize(middle - position);
            float half = 0f, farthest = 0f;
            foreach (var head in new[] { firstHead, secondHead })
            {
                var to = head - position;
                float length = to.Length();
                farthest = Math.Max(farthest, length);
                if (length < 1e-3f) continue;
                half = Math.Max(half, AngleBetween(forward, to) + Degrees(MathF.Atan(FaceRadius / length)));
            }
            float inner = Math.Min(Math.Max(2f * half, MinInnerAngle), MaxInnerAngle);
            float outer = Math.Min(inner + Softness, MaxSpotAngle);
            return new Placement(position, forward, inner, outer, Math.Min(MaxRange, farthest + Reach));
        }

        /// <summary>
        /// The key's weight a frame on: toward 1 while the two-shot holds and toward 0 when it does not, over
        /// <see cref="EaseSeconds"/>; at once under reduced motion.
        /// </summary>
        public static float Step(float weight, bool held, float deltaSeconds, bool reducedMotion)
        {
            float target = held ? 1f : 0f;
            if (reducedMotion) return target;
            float step = Math.Max(0f, deltaSeconds) / EaseSeconds;
            return weight < target ? Math.Min(target, weight + step) : Math.Max(target, weight - step);
        }

        /// <summary>The key's intensity at a weight, eased at both ends.</summary>
        public static float IntensityAt(float weight)
        {
            float t = Math.Min(Math.Max(weight, 0f), 1f);
            return Intensity * t * t * (3f - 2f * t);
        }

        /// <summary>The angle, in degrees, between two directions.</summary>
        public static float AngleBetween(Vector3 a, Vector3 b)
        {
            float lengths = a.Length() * b.Length();
            if (lengths < 1e-6f) return 0f;
            return Degrees(MathF.Acos(Math.Min(Math.Max(Vector3.Dot(a, b) / lengths, -1f), 1f)));
        }

        private static float Degrees(float radians) => radians * (180f / MathF.PI);
    }
}
