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
        /// <summary>The interview wall <c>DiarySeatPose</c> builds, which the pack-6 padding is laid on.</summary>
        public const string WallName = "Upholstered interview wall";
        public const string SlatName = "Diary slat panel";
        public const string OnCameraName = "Diary on-camera sign";
        public const string TallyName = "Diary tally light";

        /// <summary>The interview wall's size, as DiarySeatPose scales it: what the padding tiles across.</summary>
        public const float WallWidth = 4.6f, WallHeight = 2.9f;
        /// <summary>Metres per repeat of the padded wall and of the slats.</summary>
        public const float PadTile = 1.2f, SlatTile = 1.0f;
        /// <summary>The slat panels' width, from each edge of the wall inward; the strips stand just inside them.</summary>
        public const float SlatWidth = 0.7f;
        /// <summary>Where the sign and the tally hang: low beside the seat, inside the shot's edges.</summary>
        public const float AccentX = 1.9f, AccentHeight = 1.05f;
        public const float OnCameraWidth = 0.55f, TallyWidth = 0.30f;

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
        // The padding is authored violet already; the tint only lifts it out of the wall's old navy.
        public static readonly Color PadTint = new Color(0.72f, 0.68f, 0.84f);
        public static readonly Color OnCameraColour = new Color(0.85f, 0.80f, 1.00f) * 1.3f;
        public static readonly Color TallyColour = new Color(1.00f, 0.25f, 0.20f) * 2.0f;

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

            // Pack 6 (ROOM-FINISH-PLAN.md §5.1): the wall is padded violet with dark slats at its
            // edges, and the seat is flanked low by the ON CAMERA sign and a red tally - the two
            // small things that say "you are being filmed" without a camera prop in the way.
            Pad(studio, palette);
            foreach (float side in new[] { -1f, 1f })
                Panel(studio, SlatName, Slats(palette), owned,
                    new Vector3(side * (WallWidth - SlatWidth) * 0.5f, WallHeight * 0.5f, WallFace + 0.008f), new Vector2(SlatWidth, WallHeight));
            if (palette.diaryOnCamera != null)
                Panel(studio, OnCameraName, palette.Glow(OnCameraName, palette.diaryOnCamera, OnCameraColour), owned,
                    new Vector3(-AccentX, AccentHeight, WallFace + 0.015f),
                    new Vector2(OnCameraWidth, OnCameraWidth / AspectOf(palette.diaryOnCamera, 1200f / 420f)));
            if (palette.diaryTally != null)
                Panel(studio, TallyName, palette.Glow(TallyName, palette.diaryTally, TallyColour), owned,
                    new Vector3(AccentX, AccentHeight, WallFace + 0.015f),
                    new Vector2(TallyWidth, TallyWidth / AspectOf(palette.diaryTally, 2f)));

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

        /// <summary>
        /// Lays the padded violet on the interview wall's own material - the instance DiarySeatPose
        /// made and owns - tiled to the wall's metres, so the tufts are tufts and not one stretched grid.
        /// </summary>
        private static void Pad(Transform studio, HouseBrandingPalette palette)
        {
            if (palette.diaryPaddedWall == null) return;
            Renderer wall = null;
            foreach (var renderer in studio.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == WallName) { wall = renderer; break; }
            if (wall == null || wall.sharedMaterial == null) return;
            var material = wall.sharedMaterial;
            material.SetTexture("_BaseMap", palette.diaryPaddedWall);
            material.SetTextureScale("_BaseMap", new Vector2(WallWidth / PadTile, WallHeight / PadTile));
            material.SetColor("_BaseColor", PadTint);
        }

        /// <summary>A lit (not glowing) material for the slats: wood reads as wood under the key light.</summary>
        private static Material Slats(HouseBrandingPalette palette)
        {
            if (palette.diarySlats == null) return null;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;
            var material = new Material(shader) { name = SlatName };
            material.SetTexture("_BaseMap", palette.diarySlats);
            material.SetTextureScale("_BaseMap", new Vector2(SlatWidth / SlatTile, WallHeight / SlatTile));
            material.SetFloat("_Smoothness", 0.3f);
            return material;
        }

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
