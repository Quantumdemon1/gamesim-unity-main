using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Faces from <c>mood</c> × <c>stressLevel</c> (MASTER-PLAN §3.B): the two fields the simulation
    /// keeps per houseguest reach a body through five blend shapes built at runtime on the shipped
    /// mesh.
    ///
    /// <para>The Quaternius bodies have no mouth and no brows: the face is two white eye shapes on a
    /// dark head, one "Face" material. That is the emoticon's vocabulary, and it reads at a
    /// portrait's size - inner corners down is angry, up is sad, a squint toward the lid is a
    /// smile, narrow is tense, wide is overwhelmed. Each shape is a rigid move of one eye's vertices
    /// about that eye's own centre, so no artist-authored key is needed and every body the pack
    /// ships gets the same five shapes by the same rule. A body whose mesh has no "Face" material
    /// (UMA, a stand-in) simply has no expression.</para>
    ///
    /// <para>The mesh with the shapes is built once per source mesh and shared: weights live on the
    /// renderer, not the mesh, so six bodies of one kind cost one extra mesh.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FaceExpression : MonoBehaviour
    {
        public const string Angry = "EyesAngry", Sad = "EyesSad", Happy = "EyesHappy", Narrow = "EyesNarrow", Wide = "EyesWide";
        public static readonly string[] Shapes = { Angry, Sad, Happy, Narrow, Wide };

        private static readonly Dictionary<Mesh, Mesh> withShapes = new Dictionary<Mesh, Mesh>();

        private SkinnedMeshRenderer renderer;
        private readonly int[] indices = new int[5];
        private readonly float[] target = new float[5];
        private readonly float[] current = new float[5];
        private bool immediate = true;

        public string Mood { get; private set; } = "Neutral";
        public string Stress { get; private set; } = "Normal";
        public bool HasFace => renderer != null && indices[0] >= 0;
        /// <summary>Why the last bind found, or did not find, a face - for a test's failure message.</summary>
        public string BindReport { get; private set; } = "not bound";
        public bool ReducedMotion { get; set; }

        /// <summary>
        /// Gives <paramref name="bodyRoot"/> a face if its skinned mesh carries a "Face" material.
        /// Safe to call again: a body that already has one keeps it.
        /// </summary>
        public static FaceExpression Attach(GameObject bodyRoot)
        {
            if (bodyRoot == null) return null;
            var existing = bodyRoot.GetComponent<FaceExpression>();
            if (existing != null) { existing.Bind(); return existing; }
            var face = bodyRoot.AddComponent<FaceExpression>();
            face.Bind();
            return face;
        }

        private void Bind()
        {
            for (int i = 0; i < indices.Length; i++) indices[i] = -1;
            renderer = null;
            var report = new System.Text.StringBuilder();
            foreach (var candidate in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                report.Append(candidate.name).Append(": ");
                if (candidate.sharedMesh == null) { report.Append("no mesh; "); continue; }
                int faceMaterial = FaceMaterialIndex(candidate);
                if (faceMaterial < 0)
                {
                    report.Append("no Face material among ").Append(string.Join(",", System.Array.ConvertAll(candidate.sharedMaterials, m => m == null ? "null" : m.name))).Append("; ");
                    continue;
                }
                var source = candidate.sharedMesh;
                var mesh = WithShapes(source, faceMaterial, candidate.transform);
                if (mesh == null)
                {
                    report.Append("no shapes: readable=").Append(source.isReadable).Append(" submeshes=").Append(source.subMeshCount)
                        .Append(" face=").Append(faceMaterial).Append(" tris=").Append(faceMaterial < source.subMeshCount ? source.GetTriangles(faceMaterial).Length : -1).Append("; ");
                    continue;
                }
                report.Append("bound ").Append(mesh.blendShapeCount).Append(" shapes; ");
                if (candidate.sharedMesh != mesh) candidate.sharedMesh = mesh;
                renderer = candidate;
                for (int i = 0; i < Shapes.Length; i++) indices[i] = mesh.GetBlendShapeIndex(Shapes[i]);
                break;
            }
            BindReport = report.Length == 0 ? "no skinned mesh under " + name : report.ToString();
            immediate = true;
            Apply(Mood, Stress);
        }

        /// <summary>The simulation's words for the face: one of five moods and five stress levels.</summary>
        public void SetMood(string mood, string stress)
        {
            if (mood == Mood && stress == Stress) return;
            Mood = mood ?? "Neutral";
            Stress = stress ?? "Normal";
            Apply(Mood, Stress);
        }

        private void Apply(string mood, string stress)
        {
            for (int i = 0; i < target.Length; i++) target[i] = 0f;
            switch (mood)
            {
                case "Angry": target[0] = 100f; break;
                case "Upset": target[1] = 100f; break;
                case "Content": target[2] = 45f; break;
                case "Happy": target[2] = 100f; break;
            }
            switch (stress)
            {
                case "Relaxed": target[3] += 20f; break;
                case "Tense": target[3] += 45f; break;
                case "Stressed": target[0] += 35f; target[3] += 55f; break;
                case "Overwhelmed": target[4] += 100f; break;
            }
            for (int i = 0; i < target.Length; i++) target[i] = Mathf.Clamp(target[i], 0f, 100f);
            if (immediate || ReducedMotion) Push(1f);
        }

        private void Update()
        {
            if (renderer == null) return;
            bool settled = true;
            for (int i = 0; i < target.Length; i++) if (Mathf.Abs(target[i] - current[i]) > 0.01f) settled = false;
            if (settled) return;
            Push(ReducedMotion ? 1f : 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        }

        private void Push(float blend)
        {
            immediate = false;
            if (renderer == null) return;
            for (int i = 0; i < target.Length; i++)
            {
                current[i] = blend >= 1f ? target[i] : Mathf.Lerp(current[i], target[i], blend);
                if (indices[i] >= 0) renderer.SetBlendShapeWeight(indices[i], current[i]);
            }
        }

        /// <summary>The blend-shape weight the renderer currently shows for a shape, or -1 without a face.</summary>
        public float Weight(string shape)
        {
            int at = System.Array.IndexOf(Shapes, shape);
            if (renderer == null || at < 0 || indices[at] < 0) return -1f;
            return renderer.GetBlendShapeWeight(indices[at]);
        }

        private static int FaceMaterialIndex(SkinnedMeshRenderer candidate)
        {
            var materials = candidate.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].name.StartsWith("Face")) return i;
            return -1;
        }

        /// <summary>
        /// The mesh with the five shapes, built once per source mesh. Needs a readable mesh: the
        /// character models are imported Read/Write for exactly this.
        /// </summary>
        public static Mesh WithShapes(Mesh source, int faceSubMesh, Transform space)
        {
            if (source == null) return null;
            if (withShapes.TryGetValue(source, out var built) && built != null) return built;
            if (source.GetBlendShapeIndex(Angry) >= 0) { withShapes[source] = source; return source; }
            if (!source.isReadable || faceSubMesh < 0 || faceSubMesh >= source.subMeshCount) return null;

            var vertices = source.vertices;
            var triangles = source.GetTriangles(faceSubMesh);
            if (triangles.Length == 0) return null;
            var faceVerts = new HashSet<int>(triangles);
            // Mesh-space axes for the body's up and right, from the renderer's transform at rest.
            var up = space.InverseTransformDirection(Vector3.up).normalized;
            var right = space.InverseTransformDirection(Vector3.right).normalized;
            var centre = Vector3.zero;
            foreach (var v in faceVerts) centre += vertices[v];
            centre /= faceVerts.Count;
            var eyes = new[] { new List<int>(), new List<int>() };
            foreach (var v in faceVerts) eyes[Vector3.Dot(vertices[v] - centre, right) < 0f ? 0 : 1].Add(v);
            if (eyes[0].Count == 0 || eyes[1].Count == 0) return null;

            var mesh = Object.Instantiate(source);
            mesh.name = source.name + " (faces)";
            mesh.hideFlags = HideFlags.HideAndDontSave;
            int count = vertices.Length;
            foreach (var shape in Shapes)
            {
                var delta = new Vector3[count];
                for (int side = 0; side < 2; side++)
                {
                    var eye = eyes[side];
                    var eyeCentre = Vector3.zero;
                    float top = float.MinValue, bottom = float.MaxValue;
                    foreach (var v in eye)
                    {
                        eyeCentre += vertices[v];
                        float h = Vector3.Dot(vertices[v], up);
                        top = Mathf.Max(top, h); bottom = Mathf.Min(bottom, h);
                    }
                    eyeCentre /= eye.Count;
                    float height = Mathf.Max(top - bottom, 0.001f);
                    // Toward the nose: the left eye's inner side is +right, the right eye's is -right.
                    float inward = side == 0 ? 1f : -1f;
                    float topRelative = top - Vector3.Dot(eyeCentre, up);
                    foreach (var v in eye)
                    {
                        var offset = vertices[v] - eyeCentre;
                        float across = Vector3.Dot(offset, right) * inward;   // + toward the nose
                        float rise = Vector3.Dot(offset, up);
                        Vector3 moved;
                        switch (shape)
                        {
                            case Angry: moved = offset - up * (across * 0.55f); break;           // inner corner down
                            case Sad: moved = offset + up * (across * 0.55f); break;             // inner corner up
                            case Happy: moved = offset + up * ((topRelative - rise) * 0.55f); break;  // squashed up to the lid
                            case Narrow: moved = offset - up * (rise * 0.5f); break;             // half the height
                            default: moved = offset + up * (rise * 0.35f); break;                // Wide: a third taller
                        }
                        delta[v] = moved - offset;
                    }
                }
                mesh.AddBlendShapeFrame(shape, 100f, delta, null, null);
            }
            withShapes[source] = mesh;
            return mesh;
        }
    }
}
