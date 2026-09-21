using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The memory wall: a framed photograph of every houseguest, lit while they are in the game and
    /// darkened the moment they are voted out.
    ///
    /// <para>The format's one piece of set that is also a scoreboard. The cast rail already reports
    /// who is left, but it reports it in the HUD, where every other status lives — and an eviction
    /// that only changes a badge in a panel is a database update. Turning someone's portrait off on
    /// a wall the player walks past is the same information placed where it can land.</para>
    ///
    /// <para>Driven from committed state, never from the eviction event: a wall rebuilt from a
    /// reloaded save has to show the same thing as one that watched the eviction happen, and reading
    /// status is the only way both paths agree.</para>
    ///
    /// <para>Frames are bound by index against the committed cast order, which is stable for the
    /// life of a season — the engine appends to <c>contestants</c> and never reorders it, and
    /// eviction changes status rather than membership. Binding by position on the wall instead would
    /// reshuffle the photographs every time somebody left.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MemoryWall : MonoBehaviour
    {
        public const string FramePrefix = "Memory frame";
        public const string PortraitChild = "Portrait";
        public const string BorderChild = "Border";

        private static readonly Color Lit = Color.white;
        private static readonly Color Dimmed = new Color(0.22f, 0.23f, 0.26f);
        private static readonly Color BorderLit = new Color(1f, 0.78f, 0.15f);
        private static readonly Color BorderOut = new Color(0.16f, 0.17f, 0.20f);

        private readonly List<Transform> frames = new List<Transform>();
        private readonly Dictionary<string, Transform> bound = new Dictionary<string, Transform>();
        private bool collected;

        /// <summary>
        /// Repaints the wall from committed state. Safe to call every render; it assigns nothing that
        /// has not changed except the texture reference, which is a cached RenderTexture.
        /// </summary>
        public void Refresh(EpisodeState state)
        {
            if (state?.contestants == null) return;
            Collect();
            Arrange(state.contestants.Count);

            Bind(state);

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (frame == null) continue;

                // Looked up by the id this frame was bound to on the first refresh, not by position
                // in the list being rendered. Binding by index meant the wall was reading whichever
                // state the caller happened to pass, and a projection and the committed snapshot are
                // different objects — so the wrong photograph could go dark.
                var actor = Occupant(state, i);
                bool active = actor != null && actor.status == ContestantStatus.Active;

                var portrait = Find(frame, PortraitChild);
                if (portrait != null)
                {
                    var material = Instance(portrait);
                    if (material != null)
                    {
                        var face = actor == null ? null : CharacterPortraits.Get(actor);
                        SetTexture(material, face);
                        SetColour(material, active ? Lit : Dimmed);
                        if (actor != null) CharacterPortraits.Bind(portrait.GetComponent<Renderer>(), actor);
                    }
                }

                var border = Find(frame, BorderChild);
                if (border == null) continue;
                var borderMaterial = Instance(border);
                if (borderMaterial == null) continue;
                SetColour(borderMaterial, active ? BorderLit : BorderOut);
                // The emissive channel is what makes the frame read as lit at gameplay distance,
                // where the base colour is a handful of pixels.
                if (borderMaterial.HasProperty("_EmissionColor"))
                    borderMaterial.SetColor("_EmissionColor",
                        active ? BorderLit * 1.6f : Color.black);
            }
        }

        /// <summary>
        /// Assigns each frame a houseguest, once, in committed cast order. Frames beyond the cast
        /// stay unbound and render dark rather than repeating somebody.
        /// </summary>
        private void Bind(EpisodeState state)
        {
            if (bound.Count > 0) return;
            for (int i = 0; i < frames.Count && i < state.contestants.Count; i++)
                bound[state.contestants[i].id] = frames[i];
        }

        private ContestantState Occupant(EpisodeState state, int frameIndex)
        {
            foreach (var pair in bound)
                if (pair.Value == frames[frameIndex]) return state.Find(pair.Key);
            return null;
        }

        /// <summary>The frame showing a given houseguest, or null. For tests and tooling.</summary>
        public Transform FrameFor(string contestantId) =>
            contestantId != null && bound.TryGetValue(contestantId, out var frame) ? frame : null;

        /// <summary>
        /// Shows one frame per houseguest and centres them along the wall's rows. The authored wall
        /// is built for sixteen - two rows of eight - and a season of six lit at one end of it
        /// would read as ten empty keys; so the first <paramref name="count"/> frames are laid out
        /// as two centred rows (the primitive three-by-two wall is left as built), and the rest go
        /// dark and inactive. The pitch and the row heights are read from the placed frames, so the
        /// grid is stated once, in the export.
        /// </summary>
        private void Arrange(int count)
        {
            if (frames.Count < 16 || count <= 0 || arrangedFor == count) return;
            arrangedFor = count;
            int perRow = Mathf.Clamp(Mathf.CeilToInt(count / 2f), 1, 8);
            float pitch = Mathf.Abs(frames[1].localPosition.z - frames[0].localPosition.z);
            float topY = frames[0].localPosition.y, bottomY = frames[8].localPosition.y;
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (frame == null) continue;
                bool used = i < Mathf.Min(count, frames.Count);
                if (frame.gameObject.activeSelf != used) frame.gameObject.SetActive(used);
                if (!used) continue;
                int row = i / perRow, column = i % perRow;
                int inRow = Mathf.Min(perRow, count - row * perRow);
                float z = (column - (inRow - 1) * 0.5f) * pitch;
                float y = row == 0 ? topY : bottomY;
                frame.localPosition = new Vector3(frame.localPosition.x, y, z);
            }
        }

        private int arrangedFor;

        private void Collect()
        {
            if (collected) return;
            frames.Clear();
            foreach (Transform child in transform)
                if (child.name.StartsWith(FramePrefix, System.StringComparison.Ordinal)) frames.Add(child);
            // Name order, so the wall reads left to right the way it was built rather than in
            // whatever order the hierarchy happens to enumerate.
            frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            collected = true;
        }

        private static Transform Find(Transform frame, string child)
        {
            var found = frame.Find(child);
            return found;
        }

        /// <summary>
        /// The renderer's own per-instance material, so tinting one frame does not tint every object
        /// sharing the source — the borders share Neon Gold with 144 trim strips across the set.
        ///
        /// <para>Deliberately <c>renderer.material</c> rather than assigning a material this class
        /// constructs. Assigning to <c>.material</c> writes <c>sharedMaterial</c> without marking the
        /// renderer instanced, so the next read of <c>.material</c> anywhere forks a copy — and from
        /// that point the renderer draws the fork while this class keeps updating the orphan. The
        /// wall dimmed correctly and rendered lit, which is a difficult symptom to read backwards.</para>
        /// </summary>
        private static Material Instance(Transform node)
        {
            var renderer = node.GetComponent<Renderer>();
            return renderer == null ? null : renderer.material;
        }

        private static void SetTexture(Material material, Texture value)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", value);
            else if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", value);
        }

        private static void SetColour(Material material, Color value)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", value);
            if (material.HasProperty("_Color")) material.SetColor("_Color", value);
        }
    }
}
