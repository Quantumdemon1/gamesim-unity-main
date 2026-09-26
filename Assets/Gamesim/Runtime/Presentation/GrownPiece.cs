using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A piece built in code and fitted to one body - grown hair, glasses, earrings, a cap - carrying
    /// the mesh it was built with, so the mesh goes when the piece does. Marks the piece, too: the
    /// UMA stylizer flattens every generated material on a body, and these are authored already.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrownPiece : MonoBehaviour
    {
        /// <summary>The wardrobe item this piece is, by id.</summary>
        public string ItemId { get; private set; }
        private Mesh mesh;

        /// <summary>
        /// Hangs a built mesh on <paramref name="parent"/>. The geometry arrives in the head scan's
        /// frame and is carried into the parent's own space, so the piece follows that bone.
        /// </summary>
        internal static GrownPiece Attach(HeadScan scan, Transform parent, string name, string itemId, MeshParts parts, Material[] materials)
        {
            if (parts == null || parts.Vertices.Count == 0 || parent == null) return null;
            parts.Orient();
            var holder = new GameObject(name) { layer = parent.gameObject.layer };
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;
            var vertices = new List<Vector3>(parts.Vertices.Count);
            var normals = new List<Vector3>(parts.Normals.Count);
            for (int i = 0; i < parts.Vertices.Count; i++)
            {
                vertices.Add(parent.InverseTransformPoint(scan.ToWorld(parts.Vertices[i])));
                normals.Add(parent.InverseTransformDirection(scan.ToWorldDirection(parts.Normals[i])).normalized);
            }
            var mesh = new Mesh
            {
                name = itemId,
                indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, parts.Uvs);
            int used = 0;
            foreach (var list in parts.Submeshes) if (list.Count > 0) used++;
            mesh.subMeshCount = used;
            var worn = new List<Material>();
            int index = 0;
            for (int s = 0; s < parts.Submeshes.Count; s++)
            {
                if (parts.Submeshes[s].Count == 0) continue;
                mesh.SetTriangles(parts.Submeshes[s], index++, false);
                worn.Add(materials[Mathf.Min(s, materials.Length - 1)]);
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = worn.ToArray();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var piece = holder.AddComponent<GrownPiece>();
            piece.mesh = mesh;
            piece.ItemId = itemId;
            return piece;
        }

        /// <summary>Takes every piece off a body, or only those under the given name.</summary>
        public static void RemoveAll(GameObject body, string name = null)
        {
            if (body == null) return;
            foreach (var piece in body.GetComponentsInChildren<GrownPiece>(true))
            {
                if (piece == null || (name != null && piece.name != name)) continue;
                piece.gameObject.SetActive(false);
                Dispose(piece.gameObject);
            }
        }

        internal static void Dispose(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private void OnDestroy() => Dispose(mesh);
    }

    /// <summary>
    /// Geometry under construction, in the head scan's frame: vertices with their own normals and
    /// texture coordinates, and a triangle list for each material.
    /// </summary>
    internal sealed class MeshParts
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(), Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<List<int>> Submeshes = new List<List<int>>();

        public List<int> Submesh(int index)
        {
            while (Submeshes.Count <= index) Submeshes.Add(new List<int>());
            return Submeshes[index];
        }

        public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            Vertices.Add(position); Normals.Add(normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up); Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        public void Triangle(int submesh, int a, int b, int c)
        {
            var list = Submesh(submesh);
            list.Add(a); list.Add(b); list.Add(c);
        }

        /// <summary>
        /// Turns every triangle to face the way its vertices' normals say. The builders here give
        /// every vertex an outward normal and leave winding to this, so no primitive can be built
        /// inside out and vanish under back-face culling.
        /// </summary>
        public void Orient()
        {
            foreach (var list in Submeshes)
                for (int t = 0; t + 2 < list.Count; t += 3)
                {
                    int a = list[t], b = list[t + 1], c = list[t + 2];
                    var face = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
                    if (Vector3.Dot(face, Normals[a] + Normals[b] + Normals[c]) < 0f) { list[t + 1] = c; list[t + 2] = b; }
                }
        }

        /// <summary>
        /// A tube along a path, its radius tapering to <paramref name="tipScale"/> of itself at the
        /// end; a closed path joins its end to its start. The ring frame is carried along the path
        /// rather than rebuilt at each point, so the tube never twists.
        /// </summary>
        public void Tube(int submesh, IList<Vector3> path, float radius, int sides = 8, bool closed = false, float tipScale = 1f,
            float uvAlong = 40f, Vector3? up = null)
        {
            int count = path.Count;
            if (count < 2) return;
            int rings = closed ? count + 1 : count;
            var reference = up ?? Vector3.up;
            Vector3 Forward(int i)
            {
                int prev = closed ? (i - 1 + count) % count : Mathf.Max(0, i - 1);
                int next = closed ? (i + 1) % count : Mathf.Min(count - 1, i + 1);
                var d = path[next] - path[prev];
                return d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.forward;
            }
            var forward0 = Forward(0);
            var side = Vector3.Cross(forward0, reference);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(forward0, Vector3.right);
            side.Normalize();
            float along = 0f;
            int start = Vertices.Count;
            for (int r = 0; r < rings; r++)
            {
                int i = r % count;
                var forward = Forward(i);
                side = Vector3.ProjectOnPlane(side, forward);
                if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(forward, Vector3.right);
                side.Normalize();
                var over = Vector3.Cross(side, forward);
                if (r > 0) along += Vector3.Distance(path[i], path[(r - 1) % count]);
                float t = closed ? 0f : r / (float)(rings - 1);
                float size = radius * Mathf.Lerp(1f, tipScale, t);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    var normal = side * Mathf.Cos(a) + over * Mathf.Sin(a);
                    Vertex(path[i] + normal * size, normal, new Vector2(k / (float)sides, along * uvAlong));
                }
            }
            for (int r = 0; r < rings - 1; r++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + r * (sides + 1) + k, b = a + 1, c = a + sides + 1, d = c + 1;
                    Triangle(submesh, a, c, b);
                    Triangle(submesh, b, c, d);
                }
            if (!closed)
            {
                Cap(submesh, path[0], -Forward(0), start, sides);
                Cap(submesh, path[count - 1], Forward(count - 1), start + (rings - 1) * (sides + 1), sides);
            }
        }

        private void Cap(int submesh, Vector3 centre, Vector3 normal, int ring, int sides)
        {
            int middle = Vertex(centre, normal, new Vector2(.5f, .5f));
            for (int k = 0; k < sides; k++)
            {
                int a = Vertex(Vertices[ring + k], normal, Uvs[ring + k]);
                int b = Vertex(Vertices[ring + k + 1], normal, Uvs[ring + k + 1]);
                Triangle(submesh, middle, a, b);
            }
        }

        /// <summary>An ellipsoid, as a latitude-longitude sphere scaled to <paramref name="radii"/> along the frame's axes.</summary>
        public void Ellipsoid(int submesh, Vector3 centre, Vector3 radii, int rings = 8, int segments = 12, Quaternion? rotation = null)
        {
            var turn = rotation ?? Quaternion.identity;
            int start = Vertices.Count;
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings, phi = v * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments, theta = u * Mathf.PI * 2f;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    var normal = turn * new Vector3(unit.x / Mathf.Max(radii.x, 1e-5f), unit.y / Mathf.Max(radii.y, 1e-5f), unit.z / Mathf.Max(radii.z, 1e-5f));
                    Vertex(centre + turn * Vector3.Scale(unit, radii), normal, new Vector2(u, v));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = start + r * (segments + 1) + s, b = a + 1, c = a + segments + 1, d = c + 1;
                    Triangle(submesh, a, c, b);
                    Triangle(submesh, b, c, d);
                }
        }

        /// <summary>A six-sided solid from its eight corners - near face then far face, each counter-clockwise - flat-shaded.</summary>
        public void Hexahedron(int submesh, Vector3[] corners)
        {
            var centre = Vector3.zero;
            foreach (var corner in corners) centre += corner;
            centre /= corners.Length;
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            };
            foreach (var face in faces)
            {
                var mid = (corners[face[0]] + corners[face[1]] + corners[face[2]] + corners[face[3]]) * .25f;
                var normal = Vector3.Cross(corners[face[1]] - corners[face[0]], corners[face[2]] - corners[face[0]]);
                if (Vector3.Dot(normal, mid - centre) < 0f) normal = -normal;
                int a = Vertex(corners[face[0]], normal, new Vector2(0, 0)), b = Vertex(corners[face[1]], normal, new Vector2(1, 0));
                int c = Vertex(corners[face[2]], normal, new Vector2(1, 1)), d = Vertex(corners[face[3]], normal, new Vector2(0, 1));
                Triangle(submesh, a, b, c);
                Triangle(submesh, a, c, d);
            }
        }

        /// <summary>
        /// A sheet seen from both sides - every vertex twice, once for each face - its outer face
        /// turned away from <paramref name="inside"/>. Texture coordinates run across the grid
        /// unless given.
        /// </summary>
        public void Sheet(int submesh, Vector3[,] grid, Vector3 inside, Vector2[,] uvs = null)
        {
            int rows = grid.GetLength(0), columns = grid.GetLength(1);
            for (int face = 0; face < 2; face++)
            {
                int start = Vertices.Count;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        var du = grid[r, Mathf.Min(c + 1, columns - 1)] - grid[r, Mathf.Max(c - 1, 0)];
                        var dv = grid[Mathf.Min(r + 1, rows - 1), c] - grid[Mathf.Max(r - 1, 0), c];
                        var normal = Vector3.Cross(du, dv);
                        if (normal.sqrMagnitude < 1e-14f) normal = grid[r, c] - inside;
                        if (Vector3.Dot(normal, grid[r, c] - inside) < 0f) normal = -normal;
                        if (face == 1) normal = -normal;
                        var uv = uvs != null ? uvs[r, c] : new Vector2(c / (float)Mathf.Max(1, columns - 1), r / (float)Mathf.Max(1, rows - 1));
                        Vertex(grid[r, c], normal, uv);
                    }
                for (int r = 0; r < rows - 1; r++)
                    for (int c = 0; c < columns - 1; c++)
                    {
                        int a = start + r * columns + c, b = a + 1, d = a + columns, e = d + 1;
                        Triangle(submesh, a, b, e);
                        Triangle(submesh, a, e, d);
                    }
            }
        }
    }
}
