// Staged fixed schema26 overlay at c15eb54fe0b3428633571cc034d99ec7199efffe.
// Historical acceptance depends only on this overlay, fixed25 and BCL/Newtonsoft.
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// The complete former public26 contract: fixed25 plus its strictly inert
    /// extensions. Never consult a current DTO or infer Vote authority/history.
    /// Caller verifies the original envelope checksum before historical dispatch.
    /// </summary>
    internal static class FrozenEpisodeV26
    {
        private const int MaximumBytes = 8 * 1024 * 1024;
        private const int MaximumDepth = 64;
        private const int MaximumRows = 400;
        // Every fixed25 numeric leaf is a bounded integer or finite float/double.
        // Anything outside this range cannot satisfy any original numeric leaf.
        // Compare magnitude before formatting an arbitrarily large direct JValue.
        private static readonly BigInteger MaximumNumericMagnitude = (BigInteger.One << 1024) - BigInteger.One;

        internal static void Validate(JObject original)
        {
            try
            {
                Preflight(original);
                if (original["schemaVersion"]?.Type != JTokenType.Integer
                    || (long)original["schemaVersion"] != 26)
                    throw Invalid("The historical schema26 header must be literal integer26.");
                if (!(original["unifiedVoteReveals"] is JArray archive) || archive.Count != 0)
                    throw Invalid("Historical schema26 requires a present empty Vote archive.");
                if (!(original["unifiedCommitments"] is JArray rows) || rows.Count > MaximumRows)
                    throw Invalid("Historical schema26 requires its bounded canonical list.");
                foreach (JToken token in rows)
                {
                    if (!(token is JObject row)
                        || row.Property("targetId") == null || row["targetId"].Type != JTokenType.Null
                        || row.Property("subtype") == null || row["subtype"].Type != JTokenType.Null)
                        throw Invalid("Every historical schema26 canonical row requires present null Vote extensions.");
                }

                // No input mutation. Every OTHER root/nested member is left for
                // the fixed25 literal shape and complete semantic/storage checks.
                var former25 = (JObject)original.DeepClone();
                former25.Remove("unifiedVoteReveals");
                foreach (JObject row in (JArray)former25["unifiedCommitments"])
                {
                    row.Remove("targetId");
                    row.Remove("subtype");
                }
                former25["schemaVersion"] = 25;
                FrozenEpisodeV25.Validate(former25);
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            {
                throw new InvalidDataException("Historical schema26 exceeds its fixed contract.", error);
            }
        }

        /// <summary>
        /// Direct JObject callers do not necessarily pass the bounded disk
        /// parser. Inspect without recursive cloning or an unbounded child stack,
        /// then count the actual compact UTF8 output without retaining a copy.
        /// </summary>
        private static void Preflight(JObject original)
        {
            if (original == null) throw Invalid("Historical schema26 payload is missing.");
            var cursors = new Stack<IEnumerator<JToken>>();
            int nodes = 0, containerDepth = 0;
            JToken next = original;
            try
            {
                while (true)
                {
                    if (next != null)
                    {
                        if (++nodes > MaximumBytes) throw Invalid("Historical schema26 tree exceeds its byte-derived token ceiling.");
                        switch (next.Type)
                        {
                            case JTokenType.Object:
                            case JTokenType.Array:
                                if (++containerDepth > MaximumDepth) throw Invalid("Historical schema26 tree is too deeply nested.");
                                cursors.Push(next.Children().GetEnumerator());
                                break;
                            case JTokenType.Property:
                                var property = (JProperty)next;
                                if (property.Name.Length > MaximumBytes) throw Invalid("Historical schema26 property name is too large.");
                                // Properties do not add JSON container depth.
                                next = property.Value;
                                continue;
                            case JTokenType.String:
                                if (((string)next)?.Length > MaximumBytes) throw Invalid("Historical schema26 text is too large.");
                                break;
                            case JTokenType.Integer:
                                if (((JValue)next).Value is BigInteger integer
                                    && (integer > MaximumNumericMagnitude || integer < -MaximumNumericMagnitude))
                                    throw Invalid("Historical schema26 integer exceeds every finite numeric contract.");
                                break;
                            case JTokenType.Float:
                            case JTokenType.Boolean:
                            case JTokenType.Null:
                                break;
                            default:
                                throw Invalid("Historical schema26 contains a non-JSON token.");
                        }
                    }
                    next = null;
                    while (cursors.Count != 0)
                    {
                        var cursor = cursors.Peek();
                        if (cursor.MoveNext()) { next = cursor.Current; break; }
                        cursor.Dispose();
                        cursors.Pop();
                        containerDepth--;
                    }
                    if (next == null) break;
                }
            }
            finally
            {
                foreach (var cursor in cursors) cursor.Dispose();
            }

            using var counter = new ByteCounter(MaximumBytes);
            using var text = new StreamWriter(counter, new UTF8Encoding(false), 1024, true);
            using var json = new JsonTextWriter(text) { Formatting = Formatting.None, CloseOutput = false };
            original.WriteTo(json);
            json.Flush();
            text.Flush();
        }

        private sealed class ByteCounter : Stream
        {
            private readonly long maximum;
            private long count;
            internal ByteCounter(long maximum) { this.maximum = maximum; }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => count;
            public override long Position { get => count; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override void Write(byte[] buffer, int offset, int length)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || length < 0 || offset > buffer.Length - length) throw new ArgumentOutOfRangeException();
                if (length > maximum - count) throw Invalid("Historical schema26 payload exceeds eight MiB.");
                count += length;
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        private static InvalidDataException Invalid(string message) => new InvalidDataException(message);
    }
}
