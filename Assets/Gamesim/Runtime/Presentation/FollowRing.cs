using Gamesim.House;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The ring under whoever the camera is following: a flat gold disc at their feet that rides
    /// with them, and nothing when the camera is following nobody. Read off the rig every frame,
    /// so it agrees with every way a subject can be chosen - a click on a body, a portrait on the
    /// cast rail, Tab - and with every way one can be dropped. A marker, not motion: it does not
    /// pulse, so reduced motion has nothing to remove.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FollowRing : MonoBehaviour
    {
        public const string RingName = "Follow ring";
        private const float Radius = 0.55f;
        private HouseCameraRig rig;
        private Transform ring;

        public static FollowRing Attach(HouseCameraRig rig)
        {
            if (rig == null) return null;
            var existing = rig.GetComponent<FollowRing>();
            if (existing != null) return existing;
            var marker = rig.gameObject.AddComponent<FollowRing>();
            marker.rig = rig;
            return marker;
        }

        /// <summary>The body the ring is under this frame, or null.</summary>
        public Transform Under => ring != null && ring.gameObject.activeSelf ? rig.FocusedSubject : null;

        private void LateUpdate()
        {
            var subject = rig != null ? rig.FocusedSubject : null;
            if (subject == null)
            {
                if (ring != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                return;
            }
            if (ring == null) Build();
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            ring.position = subject.position + Vector3.up * 0.03f;
        }

        private void Build()
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = RingName;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var renderer = disc.GetComponent<Renderer>();
            if (shader != null)
            {
                var material = new Material(shader);
                var gold = new Color(1f, 0.78f, 0.15f);
                material.SetColor("_BaseColor", gold);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", gold * 2.2f);
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ring = disc.transform;
        }

        private void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }
    }
}
