using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed partial class FlightCaseAssetTests
    {
        // Reader for this ONE pinned FBX7400, not a generic importer or alternate asset producer.
        // Unknown tags/encodings/shape refuse. No asset settings, normals or source bytes are changed.
        private static class FbxReference
        {
            private sealed class Node
            {
                internal string Name;
                internal readonly List<object> Values = new List<object>();
                internal readonly List<Node> Children = new List<Node>();
                internal Node One(string name) => Children.Single(n => n.Name == name);
                internal object Value(string name) => One(name).Values.Single();
            }
            private sealed class Reader : IDisposable
            {
                private readonly BinaryReader input;
                private readonly int length;
                private int nodes;
                private long decoded;
                internal Reader(byte[] bytes)
                {
                    input = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
                    length = bytes.Length;
                    Need(Encoding.ASCII.GetString(input.ReadBytes(23)) == "Kaydara FBX Binary  \0\x1a\0", "Binary header");
                    Need(input.ReadUInt32() == 7400, "Pinned version only");
                }
                internal List<Node> Roots()
                {
                    var result = new List<Node>();
                    while (true)
                    {
                        var next = ReadNode(0);
                        if (next == null) break;
                        result.Add(next);
                    }
                    return result;
                }
                private Node ReadNode(int depth)
                {
                    Need(depth <= 24 && input.BaseStream.Position + 13 <= length, "Node depth/header");
                    uint end = input.ReadUInt32(), count = input.ReadUInt32(), propertyLength = input.ReadUInt32();
                    byte nameLength = input.ReadByte();
                    if (end == 0) { Need(count == 0 && propertyLength == 0 && nameLength == 0, "Null node"); return null; }
                    Need(++nodes <= 2048 && end <= length && end > input.BaseStream.Position && count <= 64, "Node bound");
                    var node = new Node { Name = Encoding.UTF8.GetString(Exact(nameLength)) };
                    long start = input.BaseStream.Position;
                    Need(start + propertyLength <= end, "Property list bound");
                    for (int i = 0; i < count; i++) node.Values.Add(Property());
                    Need(input.BaseStream.Position == start + propertyLength, "Property byte closure");
                    while (input.BaseStream.Position < end)
                    {
                        var child = ReadNode(depth + 1);
                        if (child == null) break;
                        node.Children.Add(child);
                    }
                    Need(input.BaseStream.Position == end, "Node byte closure");
                    return node;
                }
                private object Property()
                {
                    char tag = (char)input.ReadByte();
                    switch (tag)
                    {
                        case 'Y': return input.ReadInt16();
                        case 'C': return input.ReadByte();
                        case 'I': return input.ReadInt32();
                        case 'L': return input.ReadInt64();
                        case 'F': return input.ReadSingle();
                        case 'D': return input.ReadDouble();
                        case 'S': case 'R':
                            uint count = input.ReadUInt32(); Need(count <= 2 * 1024 * 1024, "String/blob bound");
                            byte[] bytes = Exact(checked((int)count));
                            return tag == 'S' ? (object)Encoding.UTF8.GetString(bytes) : bytes;
                        case 'i': case 'd': case 'f': case 'l': case 'b': case 'c':
                            return Array(tag);
                        default: throw new InvalidDataException("Unreviewed FBX property tag: " + tag);
                    }
                }
                private object Array(char tag)
                {
                    uint count = input.ReadUInt32(), encoding = input.ReadUInt32(), stored = input.ReadUInt32();
                    int width = tag == 'd' || tag == 'l' ? 8 : tag == 'i' || tag == 'f' ? 4 : 1;
                    long expected = (long)count * width;
                    Need(count <= 150000 && expected <= 4 * 1024 * 1024 && stored <= 2 * 1024 * 1024 &&
                        (encoding == 0 || encoding == 1), "Array cap/encoding");
                    decoded += expected; Need(decoded <= 16 * 1024 * 1024, "Total decoded array bound");
                    var bytes = Exact(checked((int)stored));
                    if (encoding == 1) bytes = Inflate(bytes, checked((int)expected));
                    Need(bytes.Length == expected, "Exact array length");
                    using (var array = new BinaryReader(new MemoryStream(bytes, false)))
                    {
                        if (tag == 'd')
                        {
                            var values = new double[count];
                            for (int i = 0; i < values.Length; i++) { values[i] = array.ReadDouble(); Need(!double.IsNaN(values[i]) && !double.IsInfinity(values[i]), "Finite doubles"); }
                            return values;
                        }
                        if (tag == 'i')
                        {
                            var values = new int[count]; for (int i = 0; i < values.Length; i++) values[i] = array.ReadInt32();
                            return values;
                        }
                        // Other known arrays are retained only to advance the bounded raw structure.
                        return bytes;
                    }
                }
                private static byte[] Inflate(byte[] stored, int count)
                {
                    Need(stored.Length >= 6 && (stored[0] & 15) == 8 && stored[0] >> 4 <= 7 &&
                        (((int)stored[0] << 8) + stored[1]) % 31 == 0 && (stored[1] & 32) == 0, "Exact non-dictionary zlib wrapper");
                    var bytes = new byte[count];
                    using (var compressed = new MemoryStream(stored, 2, stored.Length - 6, false))
                    using (var stream = new DeflateStream(compressed, CompressionMode.Decompress))
                    {
                        int offset = 0;
                        while (offset < count)
                        {
                            int read = stream.Read(bytes, offset, count - offset);
                            Need(read > 0, "Decoded short read"); offset += read;
                        }
                        Need(stream.ReadByte() == -1, "Decoded array growth");
                    }
                    uint a = 1, b = 0;
                    foreach (byte value in bytes) { a = (a + value) % 65521; b = (b + a) % 65521; }
                    int tail = stored.Length - 4;
                    uint checksum = ((uint)stored[tail] << 24) | ((uint)stored[tail+1] << 16) | ((uint)stored[tail+2] << 8) | stored[tail+3];
                    Need(checksum == (b << 16 | a), "Zlib checksum");
                    return bytes;
                }
                private byte[] Exact(int count)
                {
                    Need(count >= 0 && input.BaseStream.Position + count <= length, "Read bound");
                    var bytes = input.ReadBytes(count); Need(bytes.Length == count, "Raw short read"); return bytes;
                }
                public void Dispose() => input.Dispose();
            }
            internal static Corner[][] Read(int level)
            {
                var bytes = ReadPinned(ModelPath, ModelSha256, 2 * 1024 * 1024);
                List<Node> roots;
                using (var reader = new Reader(bytes)) roots = reader.Roots();
                var settings = roots.Single(n => n.Name == "GlobalSettings").One("Properties70");
                double Field(string name) => Convert.ToDouble(settings.Children.Single(n => n.Name == "P" && (string)n.Values[0] == name).Values.Last(),
                    System.Globalization.CultureInfo.InvariantCulture);
                // bb_export.export_collection's basis: -Z forward / Y up with the transform baked, so
                // the file already holds Blender (x, z, -y) and Unity's only step is its handedness flip.
                Need(Field("UpAxis") == 1 && Field("UpAxisSign") == 1 && Field("FrontAxis") == 2 && Field("FrontAxisSign") == 1 &&
                    Field("CoordAxis") == 0 && Field("CoordAxisSign") == 1 && Field("UnitScaleFactor") == 100, "House Y-up/metre metadata");
                var geometries = roots.Single(n => n.Name == "Objects").Children.Where(n => n.Name == "Geometry").ToArray();
                Need(geometries.Length == 2, "Two original geometries");
                string name = "bb_set_flight_case_LOD" + level;
                var node = geometries.Single(n => ((string)n.Values[1]).Split('\0')[0] == name + ".001");
                var vertices = (double[])node.Value("Vertices");
                var indices = (int[])node.Value("PolygonVertexIndex");
                var normal = node.One("LayerElementNormal"); var uv = node.One("LayerElementUV");
                foreach (var layer in new[] { normal, uv })
                    Need((string)layer.Value("MappingInformationType") == "ByPolygonVertex" &&
                        (string)layer.Value("ReferenceInformationType") == "IndexToDirect", "Original corner mapping only");
                var normals = (double[])normal.Value("Normals"); var normalIndices = (int[])normal.Value("NormalsIndex");
                var uvs = (double[])uv.Value("UV"); var uvIndices = (int[])uv.Value("UVIndex");
                Need(vertices.Length % 3 == 0 && normals.Length % 3 == 0 && uvs.Length % 2 == 0 &&
                    indices.Length == (level == 0 ? 35376 : 7944) && normalIndices.Length == indices.Length &&
                    uvIndices.Length == indices.Length, "Full corner cardinality");
                var result = new Corner[indices.Length / 3][];
                // Unity takes a right-handed Y-up file into its left-handed space by negating z:
                // H(x,y,z)=(x,y,-z) on the file's baked data, which is M(x,y,z)=(x,z,y) on Blender's
                // own coordinates - Blender's -y front facing Unity -z, as every set piece does.
                Vector3 H(double[] source, int index)
                {
                    Need(index >= 0 && index * 3 + 2 < source.Length, "Vector reference");
                    return new Vector3((float)source[index*3], (float)source[index*3+1], (float)-source[index*3+2]);
                }
                for (int face = 0; face < result.Length; face++)
                {
                    var original = new Corner[3];
                    for (int c = 0; c < 3; c++)
                    {
                        int i = face * 3 + c;
                        Need(c == 2 ? indices[i] < 0 : indices[i] >= 0, "Exact triangular polygon termination");
                        int vertex = c == 2 ? -indices[i] - 1 : indices[i];
                        int u = uvIndices[i]; Need(u >= 0 && u * 2 + 1 < uvs.Length, "UV reference");
                        original[c] = new Corner(H(vertices, vertex), H(normals, normalIndices[i]),
                            new Vector2((float)uvs[u*2], (float)uvs[u*2+1]));
                    }
                    // det H = -1: one and ONLY one reversal. Cyclic reordering is allowed later.
                    result[face] = new[] { original[0], original[2], original[1] };
                }
                return result;
            }
            private static void Need(bool condition, string why)
            {
                if (!condition) throw new InvalidDataException("Pinned flight-case reference refused: " + why);
            }
        }
    }
}
