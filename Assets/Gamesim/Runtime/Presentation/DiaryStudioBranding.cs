using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The Diary Room's identity: the room that has to look different from ordinary gameplay the
    /// instant the camera cuts to it.
    ///
    /// <para>It was a chair in front of a dark upholstered wall, and that wall filled the frame
    /// behind the speaker's head. Now it is the art direction's confessional: violet rings - the
    /// pack's diary halo - behind the head, the room's name in neon over them, two vertical light
    /// strips framing the chair, and a violet wash lifting the wall out of black. Symmetric,
    /// because a confessional is shot square-on.</para>
    ///
    /// <para>Everything is built on the studio the seat pose already makes, in the studio's own
    /// space: the upholstered wall's face is at <see cref="WallFace"/>, facing the camera, which
    /// stands 2.6 m out at 1.45 m. Nothing has a collider and nothing casts a shadow - the key
    /// light on the face is the one that matters. It draws nothing at all when the palette has not
    /// been built, so a clone without it gets the room it had.</para>
    /// </summary>
    public static class DiaryStudioBranding
    {
        public const string HaloName = "Diary halo";
        public const string SignName = "Diary neon sign";
        public const string StripName = "Diary light strip";
        public const string WashName = "Diary violet wash";

        /// <summary>Where the upholstered wall's front face stands, in the studio's space.</summary>
        public const float WallFace = -0.80f;
        /// <summary>The height the halo is centred on: the middle of the seated figure, so the rings frame all of it.</summary>
        public const float HaloCentre = 1.00f;
        public const float HaloHeight = 2.1f;
        public const float SignCentre = 2.22f;
        public const float SignWidth = 1.9f;

        // Violet for the room, magenta for its name: the pack's diary palette. Above 1, so the bloom
        // reads them as light rather than paint.
        public static readonly Color HaloColour = new Color(0.54f, 0.36f, 0.96f) * 1.6f;
        public static readonly Color SignColour = new Color(1.00f, 0.35f, 0.85f) * 1.7f;
        public static readonly Color StripColour = new Color(0.62f, 0.40f, 1.00f) * 1.8f;
        public static readonly Color WashColour = new Color(0.55f, 0.36f, 0.96f);

        /// <summary>
        /// Dresses <paramref name="studio"/>. Every material made is added to <paramref name="owned"/>
        /// for the caller to destroy with the studio.
        /// </summary>
        public static void Dress(Transform studio, List<Material> owned)
        {
            var palette = HouseBrandingPalette.Current;
            if (studio == null || palette == null || palette.glowTemplate == null) return;

            // The halo image is 16:9 with its rings filling the height, so the quad keeps that aspect.
            float haloWidth = HaloHeight * AspectOf(palette.diaryHalo, 16f / 9f);
            Panel(studio, HaloName, palette.Glow(HaloName, palette.diaryHalo, HaloColour), owned,
                new Vector3(0f, HaloCentre, WallFace + 0.01f), new Vector2(haloWidth, HaloHeight));
            Panel(studio, SignName, palette.Glow(SignName, palette.diaryNeon, SignColour), owned,
                new Vector3(0f, SignCentre, WallFace + 0.015f),
                new Vector2(SignWidth, SignWidth / AspectOf(palette.diaryNeon, 3.2f)));
            foreach (float side in new[] { -1f, 1f })
                Panel(studio, StripName, palette.Glow(StripName, null, StripColour), owned,
                    new Vector3(side * 1.55f, 1.35f, WallFace + 0.01f), new Vector2(0.06f, 2.4f));

            var wash = new GameObject(WashName);
            wash.transform.SetParent(studio, false);
            wash.transform.localPosition = new Vector3(0f, 0.15f, -0.25f);
            wash.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 2.2f, WallFace) - wash.transform.localPosition, Vector3.up);
            var light = wash.AddComponent<Light>();
            light.type = LightType.Spot; light.spotAngle = 85f; light.range = 4f; light.intensity = 3f;
            light.color = WashColour; light.shadows = LightShadows.None;
        }

        private static float AspectOf(Texture texture, float fallback) =>
            texture != null && texture.height > 0 ? (float)texture.width / texture.height : fallback;

        /// <summary>A quad facing the camera (+z), with no collider and no shadow.</summary>
        private static void Panel(Transform studio, string name, Material material, List<Material> owned, Vector3 at, Vector2 size)
        {
            if (material == null) return;
            owned?.Add(material);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            var collider = quad.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Object.Destroy(collider); }
            quad.transform.SetParent(studio, false);
            quad.transform.localPosition = at;
            // A quad faces -z; turned half round it faces the camera and reads the right way round.
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
