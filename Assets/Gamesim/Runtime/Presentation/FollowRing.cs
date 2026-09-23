using Gamesim.House;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The marker on whoever the camera is following: a neon ring at their feet and a green
    /// diamond over their name plate, riding with them, and nothing when the camera is following
    /// nobody.
    /// Read off the rig every frame, so it agrees with every way a subject can be chosen - a click
    /// on a body, a portrait on the cast rail, Tab - and with every way one can be dropped.
    ///
    /// <para>The diamond is V2's (VISUAL-TARGET.md §5, mockup-01): the mockups mark the selected
    /// houseguest three times over - a diamond above the head, a name chip beside it and a coloured
    /// outline on the body - and a disc at the feet is the one of the three that reads worst. At the
    /// overview camera's height a floor ring is a small ellipse behind furniture, while something
    /// floating at head height clears the sofa backs and the kitchen island and says which of twelve
    /// people is meant from across the room.</para>
    ///
    /// <para>A marker, not motion: neither part pulses or spins, so reduced motion has nothing to
    /// remove. The diamond is drawn rather than animated for the same reason the ring is.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FollowRing : MonoBehaviour
    {
        public const string RingName = "Follow ring";
        /// <summary>The diamond's own name, so a test can find it without guessing.</summary>
        public const string DiamondName = "Follow diamond";

        private const float Radius = 0.55f;
        /// <summary>The ring's width: a line on the floor, as mockup-01 draws it, not a disc.</summary>
        private const float RingWidth = 0.07f;
        /// <summary>
        /// How far over the feet the diamond floats: over the name plate, as mockup-01 stacks them.
        /// The plate is centred at 2.4 m and reaches 0.13 m above it at full size; at 2.25 m the
        /// diamond stood across it and read as a white shape through the name.
        /// </summary>
        private const float DiamondHeight = 2.9f;
        /// <summary>The same, over a seated body's focus, where the name rides 0.35 m up.</summary>
        private const float SeatedDiamondLift = 0.85f;
        /// <summary>The diamond's half height, so a test can say what it clears.</summary>
        public const float DiamondHalfHeight = DiamondTall;
        private const float DiamondWidth = 0.17f;
        private const float DiamondTall = 0.26f;

        private HouseCameraRig rig;
        private Transform marker, ring, diamond;

        public static FollowRing Attach(HouseCameraRig rig)
        {
            if (rig == null) return null;
            var existing = rig.GetComponent<FollowRing>();
            if (existing != null) return existing;
            var component = rig.gameObject.AddComponent<FollowRing>();
            component.rig = rig;
            return component;
        }

        /// <summary>The body the marker is on this frame, or null.</summary>
        public Transform Under => ring != null && ring.gameObject.activeInHierarchy ? rig.FocusedSubject : null;

        private void LateUpdate()
        {
            var subject = rig != null ? rig.FocusedSubject : null;
            if (subject == null)
            {
                if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }
            if (marker == null) Build();
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            var seat=subject.GetComponent<HouseSeatPresentation>();
            marker.position = seat!=null && seat.Active ? seat.VisualFeet : subject.position;
            if(diamond!=null)diamond.position=seat!=null && seat.Active ? seat.VisualFocus+Vector3.up*SeatedDiamondLift
                : subject.position+Vector3.up*DiamondHeight;
        }

        private void Build()
        {
            marker = new GameObject("Follow marker").transform;

            var disc = new GameObject(RingName, typeof(MeshFilter), typeof(MeshRenderer));
            disc.transform.SetParent(marker, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.GetComponent<MeshFilter>().sharedMesh = Ring();
            // Following someone is navigation, not power. The ring and the diamond also used to
            // disagree with each other - gold underfoot, green overhead, one marker. A filled disc
            // at 2.2 times its colour bloomed to a white pool the size of a rug.
            Paint(disc.GetComponent<Renderer>(), UiTheme.Accent, 1.6f);
            ring = disc.transform;

            var gem = new GameObject(DiamondName, typeof(MeshFilter), typeof(MeshRenderer));
            gem.transform.SetParent(marker, false);
            gem.transform.localPosition = new Vector3(0f, DiamondHeight, 0f);
            diamond=gem.transform;
            gem.GetComponent<MeshFilter>().sharedMesh = Diamond();
            // Green: at 2.6 times its colour the bloom took it to white.
            Paint(gem.GetComponent<Renderer>(), UiTheme.Positive, 1.2f);
        }

        /// <summary>A flat band on the floor, facing up, <see cref="RingWidth"/> wide.</summary>
        private static Mesh Ring()
        {
            const int segments = 48;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var along = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = along * (Radius - RingWidth);
                vertices[i * 2 + 1] = along * Radius;
                int next = (i + 1) % segments;
                // Clockwise seen from above, which Unity treats as facing up.
                triangles[i * 6] = i * 2; triangles[i * 6 + 1] = i * 2 + 1; triangles[i * 6 + 2] = next * 2 + 1;
                triangles[i * 6 + 3] = i * 2; triangles[i * 6 + 4] = next * 2 + 1; triangles[i * 6 + 5] = next * 2;
            }
            var mesh = new Mesh { name = RingName, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// An octahedron, point up and point down, built rather than scaled from a primitive because
        /// Unity ships no cone and a squashed cube reads as a squashed cube.
        ///
        /// <para>The winding is clockwise seen from outside, which is what Unity treats as front
        /// facing: the top ring runs +z to +x rather than +x to +z, and the bottom runs the other
        /// way. Reversed, every face would point into the middle and the marker would be invisible
        /// from everywhere that matters.</para>
        /// </summary>
        private static Mesh Diamond()
        {
            float w = DiamondWidth, h = DiamondTall;
            var mesh = new Mesh { name = DiamondName };
            mesh.vertices = new[]
            {
                new Vector3(0f, h, 0f),     // 0 top
                new Vector3(0f, -h, 0f),    // 1 bottom
                new Vector3(w, 0f, 0f),     // 2 +x
                new Vector3(0f, 0f, w),     // 3 +z
                new Vector3(-w, 0f, 0f),    // 4 -x
                new Vector3(0f, 0f, -w),    // 5 -z
            };
            mesh.triangles = new[]
            {
                0, 3, 2,  0, 4, 3,  0, 5, 4,  0, 2, 5,
                1, 2, 3,  1, 3, 4,  1, 4, 5,  1, 5, 2,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Paint(Renderer renderer, Color colour, float glow)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", colour);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", colour * glow);
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
