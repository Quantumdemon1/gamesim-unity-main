using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Darkens the set's shell so the neon reads as neon.
    ///
    /// <para>The trim, the ring motifs and the lamp glows were always there; they simply had nothing
    /// to glow against. Walls at 0.80 luminance and floors at pastel mid-tones put the emissive strips
    /// in competition with the surfaces carrying them, which is why the set read as a bright toy
    /// house with some yellow lines on it rather than as a lit stage.</para>
    ///
    /// <para>Only the shell moves — walls and floors. <c>Linen</c>, <c>Mint</c>, <c>Coral</c> and
    /// <c>Warm Oak</c> are furniture and stay exactly as bright as they are, because the look being
    /// aimed at is a dark room full of saturated props, not a dark room.</para>
    ///
    /// <para>Each room keeps its hue, at a fraction of its value. Five identical black floors would
    /// lose the room-reading the colour coding provides, and the HUD names rooms that a player is
    /// expected to tell apart on sight.</para>
    ///
    /// <para>The previous colour of every material is written to the log before it is replaced, so
    /// this is reversible from the record rather than from memory.</para>
    /// </summary>
    public static class HouseBroadcastPalette
    {
        private const string Root = "Assets/Gamesim/Art/Prototype/";

        /// <summary>Shell materials and the tone each one moves to. Hue preserved, value cut.</summary>
        private static readonly (string Material, Color Target)[] Shell =
        {
            ("Walls",              new Color(0.105f, 0.115f, 0.125f)),
            ("Bedroom Floor",      new Color(0.085f, 0.085f, 0.130f)),
            ("Private Room Floor", new Color(0.070f, 0.105f, 0.095f)),
            ("Kitchen Stone",      new Color(0.080f, 0.105f, 0.115f)),
            ("Yard",               new Color(0.055f, 0.110f, 0.080f)),
            // Created on first run. The living room previously shared Warm Oak with the
            // bed frames and tables, so darkening the floor would have darkened the
            // furniture with it — the opposite of the look, which is bright props on a
            // dark shell. It keeps the warm hue so the room still reads as the warm one.
            ("Living Room Floor",  new Color(0.105f, 0.080f, 0.060f)),
        };

        [MenuItem("Gamesim/U07/Darken the set shell for broadcast")]
        public static void Apply()
        {
            int changed = 0;
            foreach (var (name, target) in Shell)
            {
                var path = Root + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                    {
                        Debug.LogWarning("[Gamesim] palette · URP/Lit unavailable, skipped: " + name);
                        continue;
                    }
                    material = new Material(shader) { name = name };
                    material.SetFloat("_Smoothness", 0.18f);
                    AssetDatabase.CreateAsset(material, path);
                    Debug.Log("[Gamesim] palette · created " + path);
                }

                var before = Read(material);
                Write(material, target);
                EditorUtility.SetDirty(material);
                changed++;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Gamesim] palette · {0}: {1} -> {2}", name, Describe(before), Describe(target)));
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] palette · " + changed + " shell materials darkened; furniture left alone.");
        }

        private static Color Read(Material material)
        {
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.magenta;
        }

        /// <summary>
        /// Sets both colour properties when both exist. URP Lit reads <c>_BaseColor</c>, but the
        /// legacy <c>_Color</c> is still what some inspectors and shader variants sample, and a
        /// material left disagreeing with itself is a bug that only shows up on one platform.
        /// </summary>
        private static void Write(Material material, Color value)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", value);
            if (material.HasProperty("_Color")) material.SetColor("_Color", value);
        }

        private static string Describe(Color value) => string.Format(CultureInfo.InvariantCulture,
            "({0:0.###}, {1:0.###}, {2:0.###})", value.r, value.g, value.b);
    }
}
