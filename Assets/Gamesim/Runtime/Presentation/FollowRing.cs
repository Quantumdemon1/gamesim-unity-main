using Gamesim.House;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The marker on whoever the camera is following: the show's own language for "the cameras are
    /// on you" - a studio spotlight over them, pooling light on the floor where they stand, and a
    /// camera's focus reticle at their feet, a ring inside four brackets. Read off the rig every
    /// frame, so it agrees with every way a subject can be chosen - a click on a body, a portrait on
    /// the cast rail, Tab - and with every way one can be dropped. Nothing when the camera is
    /// following nobody.
    ///
    /// <para>It replaces a green diamond floating over the name plate, which looked like nothing so
    /// much as another game's marker. The web game marks its pick at the feet, never overhead; a
    /// spotlight keeps the one thing the diamond was for - saying which of twelve people is meant
    /// from across the room, over the sofa backs - because it lights the person themselves, head
    /// and shoulders first, and throws a bright pool round them that the overview camera sees.</para>
    ///
    /// <para>The reticle turns slowly and the light breathes a little, as a live camera's frame
    /// does; under reduced motion both hold still.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FollowRing : MonoBehaviour
    {
        public const string RingName = "Follow ring";
        /// <summary>The spotlight's own name, so a test can find it without guessing.</summary>
        public const string SpotlightName = "Follow spotlight";

        private const float Radius = 0.55f;
        /// <summary>The ring's width: a line on the floor, not a disc.</summary>
        private const float RingWidth = 0.05f;
        /// <summary>The brackets: four arcs outside the ring, a camera's focus corners.</summary>
        private const float BracketInner = 0.64f, BracketOuter = 0.7f, BracketSweep = 34f;

        /// <summary>
        /// How high the light hangs over the feet: over the name plate (its top is about 2.53 m)
        /// so it never stands in it, and near enough that the pool it throws is a body's width.
        /// </summary>
        public const float SpotlightHeight = 4.2f;
        /// <summary>The same over a seated body's focus, which is lower.</summary>
        private const float SeatedSpotlightLift = 3.1f;
        private const float SpotAngle = 30f, InnerSpotAngle = 14f, SpotIntensity = 10f;
        /// <summary>A warm stage white - the house's lamps are warm - so it reads as light, not as a colour.</summary>
        private static readonly Color SpotColour = new Color(1f, .95f, .86f);

        private const float TurnDegreesPerSecond = 14f;

        private HouseCameraRig rig;
        private Transform marker, ring, spotlight;
        private Light lamp;
        private Material ringMaterial;

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
            var seat = subject.GetComponent<HouseSeatPresentation>();
            bool seated = seat != null && seat.Active;
            marker.position = seated ? seat.VisualFeet : subject.position;
            if (spotlight != null)
            {
                spotlight.position = seated ? seat.VisualFocus + Vector3.up * SeatedSpotlightLift : subject.position + Vector3.up * SpotlightHeight;
                spotlight.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }

            bool still = rig.ReducedMotion;
            if (!still && ring != null) ring.Rotate(0f, TurnDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            float wave = still ? 0f : Mathf.Sin(Time.time * 2f);
            if (lamp != null) lamp.intensity = SpotIntensity * (1f + .08f * wave);
            if (ringMaterial != null) ringMaterial.SetColor("_EmissionColor", UiTheme.Accent * (1f + .2f * wave));
        }

        private void Build()
        {
            marker = new GameObject("Follow marker").transform;

            var disc = new GameObject(RingName, typeof(MeshFilter), typeof(MeshRenderer));
            disc.transform.SetParent(marker, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.GetComponent<MeshFilter>().sharedMesh = Reticle();
            // The house's accent blue: following someone is navigation, not power. A filled disc
            // bloomed to a white pool the size of a rug; thin lines keep their blue.
            ringMaterial = Paint(disc.GetComponent<Renderer>(), UiTheme.Accent, 1.0f);
            ring = disc.transform;

            var light = new GameObject(SpotlightName, typeof(Light));
            light.transform.SetParent(marker, false);
            light.transform.localPosition = new Vector3(0f, SpotlightHeight, 0f);
            light.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            lamp = light.GetComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.spotAngle = SpotAngle;
            lamp.innerSpotAngle = InnerSpotAngle;
            lamp.range = SpotlightHeight + 2.5f;
            lamp.intensity = SpotIntensity;
            lamp.color = SpotColour;
            lamp.shadows = LightShadows.None;
            // Per pixel, or it is one of the lights a busy room drops first.
            lamp.renderMode = LightRenderMode.ForcePixel;
            spotlight = light.transform;
        }

        /// <summary>
        /// The reticle on the floor, facing up: a thin ring, and outside it four short arcs at the
        /// diagonals, as a camera draws its focus corners.
        /// </summary>
        private static Mesh Reticle()
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            void Band(float inner, float outer, float from, float sweep, int segments)
            {
                int start = vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (from + sweep * i / segments) * Mathf.Deg2Rad;
                    var along = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    vertices.Add(along * inner);
                    vertices.Add(along * outer);
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2, b = a + 1, c = a + 3, d = a + 2;
                    // Clockwise seen from above, which Unity treats as facing up.
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
            }
            Band(Radius - RingWidth, Radius, 0f, 360f, 64);
            for (int corner = 0; corner < 4; corner++)
                Band(BracketInner, BracketOuter, 45f + corner * 90f - BracketSweep * .5f, BracketSweep, 8);
            var mesh = new Mesh { name = RingName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material Paint(Renderer renderer, Color colour, float glow)
        {
            Material material = null;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                material = new Material(shader);
                material.SetColor("_BaseColor", colour);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", colour * glow);
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return material;
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
