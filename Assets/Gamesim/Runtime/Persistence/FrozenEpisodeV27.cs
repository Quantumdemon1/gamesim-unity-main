// Fixed schema27 overlay at c188694f (claude/lead-integration), written for schema 28 (W28).
// Historical acceptance depends only on this overlay, fixed26 (and through it fixed25) and BCL/Newtonsoft.
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
    /// The complete former public27 contract: fixed26 plus the two zero chronology markers every
    /// canonical row carried. Never consults a current DTO, and never infers any of schema 28's
    /// Wave D storage. Every other member is left to fixed26 and the fixed25 shape beneath it,
    /// which already refuse anything schema 28 added. The caller verifies the original envelope
    /// checksum before historical dispatch.
    /// </summary>
    internal static class FrozenEpisodeV27
    {
        private const int MaximumBytes = 8 * 1024 * 1024;
        private const int MaximumDepth = 64;
        private const int MaximumRows = 400;
        // Every fixed numeric leaf is a bounded integer or a finite double; nothing past this can
        // satisfy one. Compared before an arbitrarily large integer is ever formatted.
        private static readonly BigInteger MaximumNumericMagnitude = (BigInteger.One << 1024) - BigInteger.One;
        private static readonly string[] Markers = { "voteBindingWeek", "voteFirstRevealWeek" };
        // Schema 28's additions, named so a literal 27 carrying any of them is refused here and not
        // only by the shape beneath. Literal strings: this contract never reads the growing DTO.
        private static readonly string[] Root28 = { "allianceLeakRulesStartWeek", "pactPlanRulesStartWeek", "allWeekRulesStartWeek" };
        private static readonly string[] NpcSocial28 = { "beatWeek", "beatWindow", "beatsFired", "beatSeats", "beatPlan", "acts" };
        private static readonly string[] Ledger28 = { "plans" };

        internal static void Validate(JObject original)
        {
            try
            {
                Preflight(original);
                if (original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 27)
                    throw Invalid("The historical schema27 header must be literal integer27.");
                if (Carries(original, Root28) || Carries(original["npcSocial"] as JObject, NpcSocial28)
                    || Carries(original["ledger"] as JObject, Ledger28))
                    throw Invalid("Historical schema27 cannot carry schema28 storage.");
                if (!(original["unifiedCommitments"] is JArray rows) || rows.Count > MaximumRows)
                    throw Invalid("Historical schema27 requires its bounded canonical list.");
                foreach (JToken token in rows)
                {
                    if (!(token is JObject row)) throw Invalid("Every historical schema27 canonical row is an object.");
                    foreach (string marker in Markers)
                    {
                        var property = row.Property(marker);
                        if (property == null || property.Value.Type != JTokenType.Integer || (long)property.Value != 0)
                            throw Invalid("Every historical schema27 canonical row requires present integer-zero chronology markers.");
                    }
                }

                // No input mutation: the former26 tree is a detached copy, and every other member is
                // left to the fixed26 contract's literal shape and complete semantic checks.
                var former26 = (JObject)original.DeepClone();
                foreach (JObject row in (JArray)former26["unifiedCommitments"])
                    foreach (string marker in Markers) row.Remove(marker);
                former26["schemaVersion"] = 26;
                FrozenEpisodeV26.Validate(former26);
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            {
                throw new InvalidDataException("Historical schema27 exceeds its fixed contract.", error);
            }
        }

        private static bool Carries(JObject owner, string[] names)
        {
            if (owner == null) return false;
            foreach (string name in names)
                if (owner.Property(name) != null) return true;
            return false;
        }

        /// <summary>
        /// A direct JObject caller need not have come through the bounded disk parser. Walk the tree
        /// without recursion or cloning, bounding its nodes, depth, text and integers, then count its
        /// compact UTF8 bytes without keeping a copy. Only then is it cloned.
        /// </summary>
        private static void Preflight(JObject original)
        {
            if (original == null) throw Invalid("Historical schema27 payload is missing.");
            var open = new Stack<IEnumerator<JToken>>();
            int nodes = 0;
            JToken next = original;
            try
            {
                while (true)
                {
                    while (next != null)
                    {
                        if (++nodes > MaximumBytes) throw Invalid("Historical schema27 tree exceeds its byte-derived token ceiling.");
                        if (next is JProperty property)
                        {
                            if (property.Name.Length > MaximumBytes) throw Invalid("Historical schema27 property name is too large.");
                            next = property.Value; // A property adds no container depth.
                            continue;
                        }
                        switch (next.Type)
                        {
                            case JTokenType.Object:
                            case JTokenType.Array:
                                if (open.Count + 1 > MaximumDepth) throw Invalid("Historical schema27 tree is too deeply nested.");
                                open.Push(next.Children().GetEnumerator());
                                break;
                            case JTokenType.String:
                                if (((string)next)?.Length > MaximumBytes) throw Invalid("Historical schema27 text is too large.");
                                break;
                            case JTokenType.Integer:
                                if (((JValue)next).Value is BigInteger integer
                                    && BigInteger.Abs(integer) > MaximumNumericMagnitude)
                                    throw Invalid("Historical schema27 integer exceeds every finite numeric contract.");
                                break;
                            case JTokenType.Float:
                            case JTokenType.Boolean:
                            case JTokenType.Null:
                                break;
                            default:
                                throw Invalid("Historical schema27 contains a non-JSON token.");
                        }
                        next = null;
                    }
                    while (open.Count != 0 && next == null)
                    {
                        var cursor = open.Peek();
                        if (cursor.MoveNext()) next = cursor.Current;
                        else { cursor.Dispose(); open.Pop(); }
                    }
                    if (next == null) break;
                }
            }
            finally
            {
                foreach (var cursor in open) cursor.Dispose();
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
                if (length > maximum - count) throw Invalid("Historical schema27 payload exceeds eight MiB.");
                count += length;
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        private static InvalidDataException Invalid(string message) => new InvalidDataException(message);
    }
}
