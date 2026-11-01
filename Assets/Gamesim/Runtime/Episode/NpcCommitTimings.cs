using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// <b>Verification only:</b> how long each part of a house commit takes, a row for each house operation
    /// and each write (LIVE-SAVE-PLAN.md section 6), written beside the profile's frames when the player quits.
    /// The director holds one only when <see cref="PortVerification"/> asks for it; ordinary play and every
    /// Editor session leave its field null and pay one null check.
    ///
    /// <para>Deliberately not [Serializable], unlike the verification's report classes: Unity deserializes a
    /// serializable class in a MonoBehaviour's field into a live instance, and every ordinary session would
    /// record. Nothing here touches the engine's API either: the director passes the frame, the time and the
    /// revision in and writes <see cref="ToCsv"/> itself, so the format is testable without a scene.</para>
    /// </summary>
    public sealed class NpcCommitTimings
    {
        public const string FileName = "npc-commit-timing-ms.csv";
        public const string Header = "frame,seconds,kind,cause,revision,prepareMs,validateMs,engineMs,projectMs,planMs,persistMs,saved";
        /// <summary>The kind of a row that is a write rather than a house operation.</summary>
        public const string WriteKind = "write";

        private struct Row
        {
            public int frame;
            public double seconds;
            public string kind, cause;
            public long revision;
            // NaN: the part did not run, written as an empty field.
            public double prepare, validate, engine, project, plan, persist;
            // -1: no write was made in this row.
            public sbyte saved;
        }

        // Pre-sized: nothing is allocated per row until the list outgrows it, and nothing is written until the quit.
        private readonly List<Row> rows;

        public NpcCommitTimings(int capacity = 8192) { rows = new List<Row>(Math.Max(0, capacity)); }

        public int Count => rows.Count;

        /// <summary>A timestamp to time from, the same clock <see cref="Lap"/> reads.</summary>
        public static long Now() => Stopwatch.GetTimestamp();

        /// <summary>Milliseconds from <paramref name="mark"/> to now; <paramref name="mark"/> moves to now.</summary>
        public static double Lap(ref long mark)
        {
            long now = Stopwatch.GetTimestamp();
            double milliseconds = (now - mark) * 1000d / Stopwatch.Frequency;
            mark = now;
            return milliseconds;
        }

        public static string KindName(NpcOperationKind kind)
        {
            switch (kind)
            {
                case NpcOperationKind.Tick: return "tick";
                case NpcOperationKind.Start: return "start";
                case NpcOperationKind.Cancel: return "cancel";
                default: return "operation";
            }
        }

        /// <summary>
        /// One house operation: the parts that ran, in milliseconds, and NaN for a part that did not. A write
        /// made inside the operation is its <paramref name="persistMs"/> and <paramref name="saved"/>, under
        /// <paramref name="cause"/>; an operation that made no write leaves all three empty.
        /// </summary>
        public void Operation(int frame, double seconds, NpcOperationKind kind, long revision, double prepareMs, double validateMs,
            double engineMs, double projectMs, double planMs, string cause = null, double persistMs = double.NaN, bool? saved = null)
        {
            rows.Add(new Row
            {
                frame = frame, seconds = seconds, kind = KindName(kind), cause = cause, revision = revision,
                prepare = prepareMs, validate = validateMs, engine = engineMs, project = projectMs, plan = planMs,
                persist = persistMs, saved = saved.HasValue ? (sbyte)(saved.Value ? 1 : 0) : (sbyte)-1
            });
        }

        /// <summary>One write of the session, and why it was made.</summary>
        public void Write(int frame, double seconds, string cause, long revision, double persistMs, bool saved)
        {
            rows.Add(new Row
            {
                frame = frame, seconds = seconds, kind = WriteKind, cause = cause, revision = revision,
                prepare = double.NaN, validate = double.NaN, engine = double.NaN, project = double.NaN, plan = double.NaN,
                persist = persistMs, saved = (sbyte)(saved ? 1 : 0)
            });
        }

        /// <summary>The rows in the order recorded, under <see cref="Header"/>: invariant culture, round-trip numbers.</summary>
        public string ToCsv()
        {
            var text = new StringBuilder(Header.Length + 1 + rows.Count * 96).Append(Header).Append('\n');
            foreach (var row in rows)
            {
                text.Append(row.frame.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.seconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.kind).Append(',')
                    .Append(row.cause ?? "").Append(',')
                    .Append(row.revision.ToString(CultureInfo.InvariantCulture)).Append(',');
                Number(text, row.prepare).Append(',');
                Number(text, row.validate).Append(',');
                Number(text, row.engine).Append(',');
                Number(text, row.project).Append(',');
                Number(text, row.plan).Append(',');
                Number(text, row.persist).Append(',');
                text.Append(row.saved < 0 ? "" : row.saved == 1 ? "true" : "false").Append('\n');
            }
            return text.ToString();
        }

        private static StringBuilder Number(StringBuilder text, double value) =>
            double.IsNaN(value) ? text : text.Append(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
