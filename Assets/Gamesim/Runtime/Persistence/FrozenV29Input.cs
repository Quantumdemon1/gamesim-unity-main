// Frozen29 (SCH30): the schema-29 whole reader, carried from FrozenV28Input.cs and then by exactly the live
// validators' changes from 515246bf to b208d46d (ALL-STAR-HISTORIES-PLAN 8.3). Provenance comments below
// describe the schema-28 image this copy started from.
// UNWIRED supporting component. Shape/hydration success is NOT historical save validity.
// Historical serializer/parser anchor: final-native-graph-01/project/
// Assets/Gamesim/Runtime/Persistence/EpisodeSaveStore.cs, SaveJson.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Gamesim.Persistence.Frozen29Components
{
    internal static class FrozenV29Input
    {
        internal const int MaximumBytes = 8 * 1024 * 1024;
        internal const int MaximumDepth = 64;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        // Fixed reflection contracts only; every hydration still owns fresh serializer/settings.
        private static readonly LocalPublicFieldsResolver SharedLocalFields = new LocalPublicFieldsResolver();

        /// <summary>
        /// Parses one object without changing the supplied bytes. A leading UTF8 BOM is accepted,
        /// like the historical File.ReadAllText path; the ORIGINAL bytes, including that BOM,
        /// count toward the limit. Malformed UTF8 is refused rather than replacement-decoded.
        /// Historical ReadText used Encoding.UTF8/File.ReadAllText (including BOM detection), so
        /// strict UTF8 is an explicit supporting-component policy, not full old-store parity.
        /// JSON.NET's historical syntax surface is deliberately retained: this is not an RFC-only
        /// parser. Leading/interior comments, single quotes, unquoted names and trailing commas
        /// are not independently prohibited. No token after the object is accepted, even a comment.
        /// Dates stay strings; nonfinite numbers and constructor tokens are for hydration to refuse.
        /// </summary>
        internal static JObject ParseObject(byte[] bytes)
        {
            if (bytes == null || bytes.Length > MaximumBytes)
                throw new InvalidDataException("Frozen29 component input is missing or exceeds eight MiB.");

            string text;
            try
            {
                int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
                text = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("Frozen29 component input is not strict UTF8.");
            }

            try
            {
                using (var source = new StringReader(text))
                using (var reader = new JsonTextReader(source)
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Double,
                    MaxDepth = MaximumDepth,
                    SupportMultipleContent = false,
                    Culture = CultureInfo.InvariantCulture
                })
                {
                    var token = JToken.ReadFrom(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Ignore
                    });
                    var result = token as JObject;
                    if (result == null || reader.Read())
                        throw new InvalidDataException("Frozen29 component input must contain exactly one JSON object.");
                    return result;
                }
            }
            catch (JsonException)
            {
                // Do not expose an exception that can echo input text or machine-dependent detail.
                throw new InvalidDataException("Frozen29 component input is not one bounded JSON object.");
            }
        }

        /// <summary>No envelope, schema-version, cross-reference or historical-rule approval.</summary>
        internal static Frozen29Data.EpisodeState HydrateRootShapeOnly(JObject token)
        {
            return (Frozen29Data.EpisodeState)HydrateComponentShapeOnly(token, "EpisodeState");
        }

        /// <summary>
        /// Checks a literal component shape, then creates detached fixed local fields only.
        /// The named component is one of the explicit77 classes resolved by FrozenV29Types.
        /// Missing data cannot be repaired by constructors, defaults or a live schema factory.
        /// Direct-token inputs are budgeted BEFORE any recursive writer, shape or serializer.
        /// Their compact JSON encoding (not unavailable original text whitespace) is bounded.
        /// Only plain JSON token classes/backing values are accepted, not virtual token subclasses
        /// or Raw/Date/Bytes/Constructor/Undefined tokens constructed outside this byte parser.
        /// </summary>
        internal static object HydrateComponentShapeOnly(JToken token, string componentName)
        {
            Type type;
            try
            {
                type = FrozenV29Types.Resolve(componentName);
            }
            catch (ArgumentException)
            {
                throw new InvalidDataException("Frozen29 component type is not in the fixed local snapshot.");
            }
            if (type == null || type.Namespace != "Gamesim.Persistence.Frozen29Data" || !type.IsSealed || !type.IsClass)
                throw new InvalidDataException("Frozen29 component type is not in the fixed local snapshot.");
            if (token == null)
                throw new InvalidDataException("Frozen29 component token is missing.");

            CheckTokenBudget(token);
            FrozenV29Shape.CheckComponent(token, componentName);
            if (token.Type == JTokenType.Null) return null;

            try
            {
                var serializer = CreateSerializer();
                return token.ToObject(type, serializer);
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException
                || error is OverflowException || error is FormatException || error is InvalidCastException)
            {
                throw new InvalidDataException("Frozen29 component hydration failed.");
            }
        }

        // Create, not CreateDefault: JsonConvert.DefaultSettings never participates.
        private static JsonSerializer CreateSerializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = MaximumDepth,
            Culture = CultureInfo.InvariantCulture,
            ContractResolver = SharedLocalFields
        });

        private static void CheckTokenBudget(JToken root)
        {
            try
            {
                // Depth-first frames have only one next-child reference each. A wide object/array
                // cannot fill an unbounded work queue; property nodes do not add JSON depth.
                var frames = new Stack<ScanFrame>();
                long minimumBytes = 0;
                InspectNode(root, 0, frames, ref minimumBytes);
                while (frames.Count != 0)
                {
                    var frame = frames.Peek();
                    if (frame.Next == null)
                    {
                        frames.Pop();
                        continue;
                    }
                    var child = frame.Next;
                    frame.Next = child.Next;
                    if (frame.HasChild && frame.Commas) AddMinimum(ref minimumBytes, 1);
                    frame.HasChild = true;
                    InspectNode(child, frame.Depth, frames, ref minimumBytes);
                }

                // No full ToString/byte[] copy. Strings already have a <=8MiB raw lower bound;
                // integral BigInteger values are finite as Double (<=309 decimal digits).
                // After depth<=64 is proven, JSON.NET can recurse only within that fixed bound.
                using (var counter = new Utf8BudgetWriter())
                using (var writer = new JsonTextWriter(counter)
                {
                    Formatting = Formatting.None,
                    CloseOutput = false,
                    AutoCompleteOnClose = false
                })
                {
                    root.WriteTo(writer);
                    writer.Flush();
                    counter.Flush();
                }
            }
            catch (EncoderFallbackException)
            {
                throw new InvalidDataException("Frozen29 component token contains invalid Unicode.");
            }
            catch (JsonException)
            {
                throw new InvalidDataException("Frozen29 component token cannot be bounded as JSON.");
            }
        }

        private static void InspectNode(JToken token, int parentDepth, Stack<ScanFrame> frames, ref long minimumBytes)
        {
            var exactType = token.GetType();
            if (exactType == typeof(JObject) || exactType == typeof(JArray))
            {
                int depth = parentDepth + 1;
                if (depth > MaximumDepth)
                    throw new InvalidDataException("Frozen29 component token exceeds depth64.");
                AddMinimum(ref minimumBytes, 2);
                frames.Push(new ScanFrame(token.First, depth, true));
                return;
            }
            if (exactType == typeof(JProperty))
            {
                var property = (JProperty)token;
                if (property.Name.Length > MaximumBytes)
                    throw new InvalidDataException("Frozen29 component token exceeds eight MiB.");
                AddMinimum(ref minimumBytes, 3L + StrictUtf8.GetByteCount(property.Name));
                frames.Push(new ScanFrame(property.Value, parentDepth, false));
                return;
            }
            if (exactType != typeof(JValue))
                throw new InvalidDataException("Frozen29 component token is not a plain JSON value.");

            var value = ((JValue)token).Value;
            switch (token.Type)
            {
                case JTokenType.Null:
                    if (value != null) throw new InvalidDataException("Frozen29 component null has an unexpected backing value.");
                    AddMinimum(ref minimumBytes, 4);
                    return;
                case JTokenType.String:
                    if (!(value is string)) throw new InvalidDataException("Frozen29 component string has an unexpected backing value.");
                    if (((string)value).Length > MaximumBytes)
                        throw new InvalidDataException("Frozen29 component token exceeds eight MiB.");
                    AddMinimum(ref minimumBytes, 2L + StrictUtf8.GetByteCount((string)value));
                    return;
                case JTokenType.Boolean:
                    if (!(value is bool)) throw new InvalidDataException("Frozen29 component boolean has an unexpected backing value.");
                    AddMinimum(ref minimumBytes, (bool)value ? 4 : 5);
                    return;
                case JTokenType.Integer:
                    if (!(value is sbyte || value is byte || value is short || value is ushort
                        || value is int || value is uint || value is long || value is ulong || value is BigInteger))
                        throw new InvalidDataException("Frozen29 component integer has an unexpected backing value.");
                    if (value is BigInteger && double.IsInfinity((double)(BigInteger)value))
                        throw new InvalidDataException("Frozen29 component number is not finite.");
                    AddMinimum(ref minimumBytes, 1);
                    return;
                case JTokenType.Float:
                    if (!(value is float || value is double || value is decimal))
                        throw new InvalidDataException("Frozen29 component float has an unexpected backing value.");
                    double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    if (double.IsNaN(number) || double.IsInfinity(number))
                        throw new InvalidDataException("Frozen29 component number is not finite.");
                    AddMinimum(ref minimumBytes, 1);
                    return;
                default:
                    throw new InvalidDataException("Frozen29 component token is not a plain JSON value.");
            }
        }

        private static void AddMinimum(ref long count, long addition)
        {
            count += addition;
            if (count > MaximumBytes)
                throw new InvalidDataException("Frozen29 component token exceeds eight MiB.");
        }

        private sealed class ScanFrame
        {
            internal JToken Next;
            internal readonly int Depth;
            internal readonly bool Commas;
            internal bool HasChild;
            internal ScanFrame(JToken next, int depth, bool commas) { Next = next; Depth = depth; Commas = commas; }
        }

        private sealed class LocalPublicFieldsResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                if (type.Namespace != "Gamesim.Persistence.Frozen29Data" || !type.IsSealed)
                    throw new InvalidDataException("Frozen29 component hydration left the fixed local snapshot.");
                return type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
            }
        }

        /// <summary>Counts streaming strict UTF8 bytes without retaining serialized output.</summary>
        private sealed class Utf8BudgetWriter : TextWriter
        {
            private readonly Encoder encoder = StrictUtf8.GetEncoder();
            private readonly char[] characters = new char[4096];
            private readonly byte[] encoded = new byte[4096];
            private long count;
            public override Encoding Encoding { get { return StrictUtf8; } }
            public override void Write(char value) { characters[0] = value; WriteCore(characters, 0, 1, false); }
            public override void Write(char[] buffer, int index, int length) { WriteCore(buffer, index, length, false); }
            public override void Write(string value)
            {
                if (value == null) return;
                for (int offset = 0; offset < value.Length;)
                {
                    int length = Math.Min(characters.Length, value.Length - offset);
                    value.CopyTo(offset, characters, 0, length);
                    WriteCore(characters, 0, length, false);
                    offset += length;
                }
            }
            public override void Flush() { WriteCore(characters, 0, 0, true); }
            private void WriteCore(char[] buffer, int offset, int length, bool flush)
            {
                bool completed;
                do
                {
                    int used, produced;
                    encoder.Convert(buffer, offset, length, encoded, 0, encoded.Length, flush,
                        out used, out produced, out completed);
                    count += produced;
                    if (count > MaximumBytes)
                        throw new InvalidDataException("Frozen29 component token exceeds eight MiB.");
                    offset += used;
                    length -= used;
                    if (!completed && used == 0 && produced == 0)
                        throw new InvalidDataException("Frozen29 component UTF8 counting made no progress.");
                } while (!completed);
            }
        }
    }
}
