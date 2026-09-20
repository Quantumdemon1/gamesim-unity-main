using Gamesim.House;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The marker on whoever the camera is following: a flat gold disc at their feet and a green
    /// diamond over their head, riding with them, and nothing when the camera is following nobody.
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
        /// <summary>How far over the feet the diamond floats: clear of a 1.8 m houseguest's head.</summary>
        private const float DiamondHeight = 2.25f;
        private const float DiamondWidth = 0.17f;
        private const float DiamondTall = 0.26f;

        private HouseCameraRig rig;
        private Transform marker, ring;

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
            marker.position = subject.position;
        }

        private void Build()
        {
            marker = new GameObject("Follow marker").transform;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = RingName;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(marker, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.transform.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);
            Paint(disc.GetComponent<Renderer>(), new Color(1f, 0.78f, 0.15f), 2.2f);
            ring = disc.transform;

            var gem = new GameObject(DiamondName, typeof(MeshFilter), typeof(MeshRenderer));
            gem.transform.SetParent(marker, false);
            gem.transform.localPosition = new Vector3(0f, DiamondHeight, 0f);
            gem.GetComponent<MeshFilter>().sharedMesh = Diamond();
            Paint(gem.GetComponent<Renderer>(), UiTheme.Positive, 2.6f);
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
