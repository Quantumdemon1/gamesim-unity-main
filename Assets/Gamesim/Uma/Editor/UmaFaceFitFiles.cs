using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Gamesim.Uma.Editor
{
    // The face-fit export's file forms, with nothing of Unity's in them, so the bytes it writes can be
    // proved without the engine (Tools/SimulationTests compiles this file and numpy reads what it wrote).
    // Nothing here knows about UMA: arrays, a zip without compression, JSON, hashes, and files written
    // once under a temporary name and then renamed, so a half-written file is never taken for a finished one.

    /// <summary>One numpy array: its dtype (a numpy descr such as "&lt;f4"), its shape and its little-endian bytes.</summary>
    internal sealed class NpyArray
    {
        public string Descr;
        public int[] Shape;
        public byte[] Data;

        public NpyArray(string descr, int[] shape, byte[] data)
        {
            Descr = descr;
            Shape = shape ?? Array.Empty<int>();
            Data = data ?? Array.Empty<byte>();
            long items = Count(Shape);
            int size = ItemSize(descr);
            if (items * size != Data.LongLength)
                throw new ArgumentException("an array of shape (" + string.Join(", ", Shape) + ") and dtype " + descr
                    + " holds " + (items * size) + " bytes, not " + Data.LongLength);
        }

        public static long Count(int[] shape)
        {
            long n = 1;
            foreach (int d in shape)
            {
                if (d < 0) throw new ArgumentException("a negative dimension");
                n *= d;
            }
            return n;
        }

        /// <summary>The bytes of one item of a dtype this writer knows.</summary>
        public static int ItemSize(string descr)
        {
            switch (descr)
            {
                case "<f4": case "<i4": return 4;
                case "<f8": case "<i8": return 8;
                case "|i1": case "|u1": return 1;
            }
            if (descr != null && descr.StartsWith("<U", StringComparison.Ordinal)
                && int.TryParse(descr.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int chars) && chars > 0)
                return 4 * chars;
            throw new ArgumentException("an unsupported dtype '" + descr + "'");
        }

        private static int[] ShapeOr(int[] shape, long count) => shape != null && shape.Length > 0 ? shape : new[] { checked((int)count) };

        public static NpyArray Float32(float[] values, params int[] shape)
        {
            var data = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            if (!BitConverter.IsLittleEndian) Swap(data, 4);
            return new NpyArray("<f4", ShapeOr(shape, values.Length), data);
        }

        public static NpyArray Float64(double[] values, params int[] shape)
        {
            var data = new byte[values.Length * 8];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            if (!BitConverter.IsLittleEndian) Swap(data, 8);
            return new NpyArray("<f8", ShapeOr(shape, values.Length), data);
        }

        public static NpyArray Int32(int[] values, params int[] shape)
        {
            var data = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            if (!BitConverter.IsLittleEndian) Swap(data, 4);
            return new NpyArray("<i4", ShapeOr(shape, values.Length), data);
        }

        public static NpyArray Int8(sbyte[] values, params int[] shape)
        {
            var data = new byte[values.Length];
            Buffer.BlockCopy(values, 0, data, 0, data.Length);
            return new NpyArray("|i1", ShapeOr(shape, values.Length), data);
        }

        /// <summary>Strings as numpy's fixed-width unicode (UTF-32, as wide as the longest; at least one character).</summary>
        public static NpyArray Unicode(IList<string> values)
        {
            int width = 1;
            var encoded = new byte[values.Count][];
            for (int i = 0; i < values.Count; i++)
            {
                encoded[i] = Utf32(values[i] ?? string.Empty);
                width = Math.Max(width, encoded[i].Length / 4);
            }
            var data = new byte[values.Count * width * 4];
            for (int i = 0; i < values.Count; i++) Buffer.BlockCopy(encoded[i], 0, data, i * width * 4, encoded[i].Length);
            return new NpyArray("<U" + width.ToString(CultureInfo.InvariantCulture), new[] { values.Count }, data);
        }

        private static byte[] Utf32(string s)
        {
            var bytes = new UTF32Encoding(bigEndian: false, byteOrderMark: false).GetBytes(s);
            return bytes;
        }

        public float[] AsFloat32()
        {
            Expect("<f4");
            var copy = (byte[])Data.Clone();
            if (!BitConverter.IsLittleEndian) Swap(copy, 4);
            var values = new float[copy.Length / 4];
            Buffer.BlockCopy(copy, 0, values, 0, copy.Length);
            return values;
        }

        public double[] AsFloat64()
        {
            Expect("<f8");
            var copy = (byte[])Data.Clone();
            if (!BitConverter.IsLittleEndian) Swap(copy, 8);
            var values = new double[copy.Length / 8];
            Buffer.BlockCopy(copy, 0, values, 0, copy.Length);
            return values;
        }

        public int[] AsInt32()
        {
            Expect("<i4");
            var copy = (byte[])Data.Clone();
            if (!BitConverter.IsLittleEndian) Swap(copy, 4);
            var values = new int[copy.Length / 4];
            Buffer.BlockCopy(copy, 0, values, 0, copy.Length);
            return values;
        }

        public sbyte[] AsInt8()
        {
            Expect("|i1");
            var values = new sbyte[Data.Length];
            Buffer.BlockCopy(Data, 0, values, 0, Data.Length);
            return values;
        }

        public string[] AsUnicode()
        {
            if (Descr == null || !Descr.StartsWith("<U", StringComparison.Ordinal)) throw new InvalidDataException("not a unicode array: " + Descr);
            int width = ItemSize(Descr);
            var encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: false);
            var values = new string[Data.Length / width];
            for (int i = 0; i < values.Length; i++) values[i] = encoding.GetString(Data, i * width, width).TrimEnd('\0');
            return values;
        }

        private void Expect(string descr)
        {
            if (Descr != descr) throw new InvalidDataException("the array is " + Descr + ", not " + descr);
        }

        private static void Swap(byte[] data, int size)
        {
            for (int i = 0; i < data.Length; i += size) Array.Reverse(data, i, size);
        }

        /// <summary>The .npy header (format 1.0) for a dtype and a shape; the header and its padding end on a 64-byte boundary.</summary>
        public static byte[] Header(string descr, int[] shape)
        {
            string dims = shape.Length == 0 ? "()"
                : shape.Length == 1 ? "(" + shape[0].ToString(CultureInfo.InvariantCulture) + ",)"
                : "(" + string.Join(", ", Array.ConvertAll(shape, d => d.ToString(CultureInfo.InvariantCulture))) + ")";
            string dict = "{'descr': '" + descr + "', 'fortran_order': False, 'shape': " + dims + ", }";
            int unpadded = 10 + dict.Length + 1;
            int total = (unpadded + 63) / 64 * 64;
            string text = dict + new string(' ', total - unpadded) + "\n";
            if (text.Length > ushort.MaxValue) throw new ArgumentException("an .npy header longer than format 1.0 allows");
            var header = new byte[10 + text.Length];
            header[0] = 0x93;
            Encoding.ASCII.GetBytes("NUMPY", 0, 5, header, 1);
            header[6] = 1;
            header[7] = 0;
            header[8] = (byte)(text.Length & 0xff);
            header[9] = (byte)(text.Length >> 8);
            Encoding.ASCII.GetBytes(text, 0, text.Length, header, 10);
            return header;
        }

        public byte[] ToNpy()
        {
            var header = Header(Descr, Shape);
            var bytes = new byte[header.Length + Data.Length];
            Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            Buffer.BlockCopy(Data, 0, bytes, header.Length, Data.Length);
            return bytes;
        }

        /// <summary>Reads an .npy (format 1.0 or 2.0) of a dtype this file knows, C order only.</summary>
        public static NpyArray FromNpy(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 10 || bytes[0] != 0x93 || Encoding.ASCII.GetString(bytes, 1, 5) != "NUMPY")
                throw new InvalidDataException("not an .npy file");
            int major = bytes[6];
            int length, start;
            if (major == 1) { length = bytes[8] | (bytes[9] << 8); start = 10; }
            else if (major == 2 || major == 3) { length = BitConverter.ToInt32(bytes, 8); start = 12; }
            else throw new InvalidDataException("an .npy format " + major + " this reader does not know");
            string header = Encoding.ASCII.GetString(bytes, start, length);
            string descr = Between(header, "'descr':", ",").Trim().Trim('\'');
            if (Between(header, "'fortran_order':", ",").Trim() != "False") throw new InvalidDataException("a Fortran-order array");
            string dims = Between(header, "'shape':", ")").Trim().TrimStart('(');
            var shape = new List<int>();
            foreach (string part in dims.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = part.Trim();
                if (p.Length > 0) shape.Add(int.Parse(p, NumberStyles.None, CultureInfo.InvariantCulture));
            }
            int offset = start + length;
            var data = new byte[bytes.Length - offset];
            Buffer.BlockCopy(bytes, offset, data, 0, data.Length);
            return new NpyArray(descr, shape.ToArray(), data);
        }

        private static string Between(string text, string after, string until)
        {
            int at = text.IndexOf(after, StringComparison.Ordinal);
            if (at < 0) throw new InvalidDataException("an .npy header without " + after);
            at += after.Length;
            int end = text.IndexOf(until, at, StringComparison.Ordinal);
            if (end < 0) throw new InvalidDataException("an .npy header cut short after " + after);
            return text.Substring(at, end - at);
        }
    }

    /// <summary>
    /// An .npz: a zip of .npy members, STORED (no compression), as numpy.savez writes one. Each member is written
    /// whole: its CRC is taken first, so a member can be produced in chunks without holding it in memory.
    /// </summary>
    internal sealed class NpzWriter : IDisposable
    {
        private readonly Stream stream;
        private readonly List<(string name, uint crc, uint size, uint offset)> entries = new List<(string, uint, uint, uint)>();
        private bool finished;

        public NpzWriter(Stream stream) { this.stream = stream; }

        public void Add(string name, NpyArray array) => Add(name, array.Descr, array.Shape, () => new[] { array.Data });

        /// <summary>
        /// A member whose data comes in chunks, enumerated once: the local header is written first with the CRC and sizes
        /// left at zero, then patched once the data is written (the stream must be seekable, as a file is).
        /// </summary>
        public void Add(string name, string descr, int[] shape, Func<IEnumerable<byte[]>> chunks)
        {
            if (finished) throw new InvalidOperationException("the archive is finished");
            if (!stream.CanSeek) throw new InvalidOperationException("an .npz is written to a seekable stream");
            string member = name + ".npy";
            foreach (var entry in entries)
                if (entry.name == member) throw new ArgumentException("two members named " + member);
            byte[] header = NpyArray.Header(descr, shape);
            long expected = NpyArray.Count(shape) * NpyArray.ItemSize(descr);
            long size = header.Length + expected;
            if (size > uint.MaxValue || stream.Position + size + 1024 > uint.MaxValue)
                throw new InvalidDataException(member + " would take the archive past 4 GiB, which this writer (no Zip64) refuses");
            uint offset = (uint)stream.Position;
            byte[] nameBytes = Encoding.UTF8.GetBytes(member);
            var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            w.Write(0x04034b50u);
            w.Write((ushort)20); w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)0); w.Write((ushort)0x21); // 1980-01-01 00:00, so the bytes depend on the content only
            long patch = stream.Position;
            w.Write(0u); w.Write(0u); w.Write(0u);
            w.Write((ushort)nameBytes.Length); w.Write((ushort)0);
            w.Write(nameBytes);
            w.Write(header);
            uint crc = Crc32.Update(0xffffffffu, header, 0, header.Length);
            long written = 0;
            foreach (var chunk in chunks())
            {
                crc = Crc32.Update(crc, chunk, 0, chunk.Length);
                w.Write(chunk);
                written += chunk.Length;
            }
            crc ^= 0xffffffffu;
            if (written != expected)
                throw new InvalidDataException(member + " has " + written + " data bytes, its shape and dtype say " + expected);
            w.Flush();
            long end = stream.Position;
            stream.Position = patch;
            w.Write(crc); w.Write((uint)size); w.Write((uint)size);
            w.Flush();
            stream.Position = end;
            entries.Add((member, crc, (uint)size, offset));
        }

        public void Finish()
        {
            if (finished) return;
            finished = true;
            var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            uint start = (uint)stream.Position;
            foreach (var entry in entries)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(entry.name);
                w.Write(0x02014b50u);
                w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)0); w.Write((ushort)0x21);
                w.Write(entry.crc); w.Write(entry.size); w.Write(entry.size);
                w.Write((ushort)nameBytes.Length); w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)0); w.Write((ushort)0); w.Write(0u);
                w.Write(entry.offset);
                w.Write(nameBytes);
            }
            uint end = (uint)stream.Position;
            w.Write(0x06054b50u);
            w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)entries.Count); w.Write((ushort)entries.Count);
            w.Write(end - start); w.Write(start);
            w.Write((ushort)0);
            w.Flush();
        }

        public void Dispose() => Finish();

        /// <summary>The members of an .npz this writer (or numpy.savez) wrote: stored members only.</summary>
        public static Dictionary<string, NpyArray> Read(byte[] bytes)
        {
            var result = new Dictionary<string, NpyArray>(StringComparer.Ordinal);
            int at = 0;
            while (at + 30 <= bytes.Length && BitConverter.ToUInt32(bytes, at) == 0x04034b50u)
            {
                ushort flags = BitConverter.ToUInt16(bytes, at + 6);
                ushort method = BitConverter.ToUInt16(bytes, at + 8);
                uint crc = BitConverter.ToUInt32(bytes, at + 14);
                uint size = BitConverter.ToUInt32(bytes, at + 18);
                ushort nameLength = BitConverter.ToUInt16(bytes, at + 26);
                ushort extraLength = BitConverter.ToUInt16(bytes, at + 28);
                if (method != 0) throw new InvalidDataException("a compressed member: this reader takes stored members only");
                if ((flags & 8) != 0) throw new InvalidDataException("a member with a trailing data descriptor");
                string name = Encoding.UTF8.GetString(bytes, at + 30, nameLength);
                int dataAt = at + 30 + nameLength + extraLength;
                if (dataAt + (long)size > bytes.Length) throw new InvalidDataException(name + " runs past the end of the archive");
                var member = new byte[size];
                Buffer.BlockCopy(bytes, dataAt, member, 0, (int)size);
                if ((Crc32.Update(0xffffffffu, member, 0, member.Length) ^ 0xffffffffu) != crc)
                    throw new InvalidDataException(name + " fails its CRC");
                if (name.EndsWith(".npy", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 4);
                result[name] = NpyArray.FromNpy(member);
                at = dataAt + (int)size;
            }
            if (result.Count == 0) throw new InvalidDataException("no members: not an .npz this reader knows");
            return result;
        }
    }

    internal static class Crc32
    {
        private static readonly uint[] Table = Make();

        private static uint[] Make()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        /// <summary>Runs the CRC over a range; start from 0xffffffff and XOR the end result with 0xffffffff.</summary>
        public static uint Update(uint crc, byte[] data, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++) crc = Table[(crc ^ data[i]) & 0xff] ^ (crc >> 8);
            return crc;
        }
    }

    /// <summary>
    /// A JSON object that keeps its keys in the order they were added, so a manifest reads top to bottom as written.
    /// </summary>
    internal sealed class JsonObject : List<KeyValuePair<string, object>>
    {
        public JsonObject Set(string key, object value)
        {
            for (int i = 0; i < Count; i++)
                if (this[i].Key == key) { this[i] = new KeyValuePair<string, object>(key, value); return this; }
            Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public bool TryGet(string key, out object value)
        {
            foreach (var pair in this)
                if (pair.Key == key) { value = pair.Value; return true; }
            value = null;
            return false;
        }

        public object this[string key] => TryGet(key, out var value) ? value : null;
    }

    /// <summary>
    /// A small JSON reader and writer: objects come back as <see cref="JsonObject"/>, arrays as List&lt;object&gt;,
    /// numbers as double, plus string, bool and null. Written with LF line ends and invariant numbers.
    /// </summary>
    internal static class Json
    {
        public static object Parse(string text)
        {
            int at = 0;
            var value = ReadValue(text, ref at);
            SkipSpace(text, ref at);
            if (at != text.Length) throw Error(text, at, "text after the JSON value");
            return value;
        }

        private static FormatException Error(string text, int at, string what)
        {
            int line = 1, column = 1;
            for (int i = 0; i < at && i < text.Length; i++) { if (text[i] == '\n') { line++; column = 1; } else column++; }
            return new FormatException("JSON line " + line + ", column " + column + ": " + what);
        }

        private static void SkipSpace(string text, ref int at)
        {
            while (at < text.Length && (text[at] == ' ' || text[at] == '\t' || text[at] == '\r' || text[at] == '\n' || text[at] == (char)0xFEFF)) at++;
        }

        private static object ReadValue(string text, ref int at)
        {
            SkipSpace(text, ref at);
            if (at >= text.Length) throw Error(text, at, "the text ends where a value should be");
            char c = text[at];
            if (c == '{')
            {
                at++;
                var obj = new JsonObject();
                SkipSpace(text, ref at);
                if (at < text.Length && text[at] == '}') { at++; return obj; }
                while (true)
                {
                    SkipSpace(text, ref at);
                    if (at >= text.Length || text[at] != '"') throw Error(text, at, "a key in double quotes");
                    string key = ReadString(text, ref at);
                    if (obj.TryGet(key, out _)) throw Error(text, at, "the key \"" + key + "\" twice");
                    SkipSpace(text, ref at);
                    if (at >= text.Length || text[at] != ':') throw Error(text, at, "a colon after \"" + key + "\"");
                    at++;
                    obj.Add(new KeyValuePair<string, object>(key, ReadValue(text, ref at)));
                    SkipSpace(text, ref at);
                    if (at < text.Length && text[at] == ',') { at++; continue; }
                    if (at < text.Length && text[at] == '}') { at++; return obj; }
                    throw Error(text, at, "a comma or a closing brace");
                }
            }
            if (c == '[')
            {
                at++;
                var list = new List<object>();
                SkipSpace(text, ref at);
                if (at < text.Length && text[at] == ']') { at++; return list; }
                while (true)
                {
                    list.Add(ReadValue(text, ref at));
                    SkipSpace(text, ref at);
                    if (at < text.Length && text[at] == ',') { at++; continue; }
                    if (at < text.Length && text[at] == ']') { at++; return list; }
                    throw Error(text, at, "a comma or a closing bracket");
                }
            }
            if (c == '"') return ReadString(text, ref at);
            if (Word(text, ref at, "true")) return true;
            if (Word(text, ref at, "false")) return false;
            if (Word(text, ref at, "null")) return null;
            int start = at;
            while (at < text.Length && "+-0123456789.eE".IndexOf(text[at]) >= 0) at++;
            if (start == at) throw Error(text, at, "a value");
            if (!double.TryParse(text.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                throw Error(text, start, "a number");
            return number;
        }

        private static bool Word(string text, ref int at, string word)
        {
            if (string.CompareOrdinal(text, at, word, 0, word.Length) != 0) return false;
            at += word.Length;
            return true;
        }

        private static string ReadString(string text, ref int at)
        {
            at++;
            var sb = new StringBuilder();
            while (true)
            {
                if (at >= text.Length) throw Error(text, at, "an unterminated string");
                char c = text[at++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (at >= text.Length) throw Error(text, at, "an unterminated escape");
                char e = text[at++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (at + 4 > text.Length) throw Error(text, at, "a short \\u escape");
                        sb.Append((char)int.Parse(text.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        at += 4;
                        break;
                    default: throw Error(text, at, "an unknown escape \\" + e);
                }
            }
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void Indent(StringBuilder sb, int depth) => sb.Append('\n').Append(' ', depth * 2);

        private static void Write(StringBuilder sb, object value, int depth)
        {
            switch (value)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: WriteNumber(sb, d); return;
                case float f: WriteNumber(sb, f); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case uint u: sb.Append(u.ToString(CultureInfo.InvariantCulture)); return;
                case ulong ul: sb.Append(ul.ToString(CultureInfo.InvariantCulture)); return;
                case JsonObject obj:
                    if (obj.Count == 0) { sb.Append("{}"); return; }
                    sb.Append('{');
                    for (int k = 0; k < obj.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        Indent(sb, depth + 1);
                        WriteString(sb, obj[k].Key);
                        sb.Append(": ");
                        Write(sb, obj[k].Value, depth + 1);
                    }
                    Indent(sb, depth);
                    sb.Append('}');
                    return;
                case IDictionary<string, double> map:
                    var ordered = new JsonObject();
                    var keys = new List<string>(map.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    foreach (var key in keys) ordered.Set(key, map[key]);
                    Write(sb, ordered, depth);
                    return;
                case System.Collections.IEnumerable list:
                    var items = new List<object>();
                    foreach (var item in list) items.Add(item);
                    bool flat = items.TrueForAll(item => item == null || item is double || item is float || item is int || item is long || item is bool || item is string);
                    sb.Append('[');
                    for (int k = 0; k < items.Count; k++)
                    {
                        if (k > 0) sb.Append(flat ? ", " : ",");
                        if (!flat) Indent(sb, depth + 1);
                        Write(sb, items[k], depth + 1);
                    }
                    if (!flat && items.Count > 0) Indent(sb, depth);
                    sb.Append(']');
                    return;
            }
            throw new ArgumentException("JSON cannot hold a " + value.GetType().Name);
        }

        private static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteNumber(StringBuilder sb, float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) { sb.Append("null"); return; }
            sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Files written once: under a temporary ".part" name, then renamed; a finished file is never replaced.</summary>
    internal static class WriteOnce
    {
        public const string PartSuffix = ".part";

        public static void Bytes(string path, byte[] bytes) => Write(path, s => s.Write(bytes, 0, bytes.Length));

        public static void Text(string path, string text) => Bytes(path, new UTF8Encoding(false).GetBytes(text.Replace("\r\n", "\n")));

        public static void Write(string path, Action<Stream> write)
        {
            if (File.Exists(path)) throw new IOException("refusing to overwrite the finished file " + path);
            string part = path + PartSuffix;
            if (File.Exists(part)) File.Delete(part);
            using (var stream = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(true);
            }
            // File.Move refuses an existing target, so even a second writer racing this one cannot replace a finished file.
            File.Move(part, path);
        }

        /// <summary>Deletes every half-written file under a folder (a run stopped mid-write leaves one); returns how many.</summary>
        public static int DeleteParts(string folder)
        {
            if (!Directory.Exists(folder)) return 0;
            int deleted = 0;
            foreach (var part in Directory.GetFiles(folder, "*" + PartSuffix, SearchOption.AllDirectories)) { File.Delete(part); deleted++; }
            return deleted;
        }
    }

    internal static class Sha256
    {
        public static string Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static string File(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static string Text(string text) => Hex(new UTF8Encoding(false).GetBytes(text));
    }
}
