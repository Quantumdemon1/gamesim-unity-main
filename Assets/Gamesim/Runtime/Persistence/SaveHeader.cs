using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Gamesim.Persistence
{
    /// <summary>
    /// What the main menu's season card says about the save "Continue your season" opens: its week,
    /// how many houseguests are still in the house and when it was saved, read from the top of the
    /// file only.
    ///
    /// <para>This is a look, not a load. The file is opened for reading with <see cref="FileShare.Read"/>
    /// and read as a stream of tokens: the envelope's <c>format</c>, <c>version</c> and
    /// <c>savedAt</c>, then the season's <c>week</c> and each houseguest's <c>status</c>, and the read
    /// stops there. Nothing is checked against the checksum, nothing is migrated, no season is built
    /// and nothing is written, so a look can never change a save or how one is loaded, held or
    /// written. A save the load would refuse may still show its week here: the card is a label,
    /// and Continue still goes through the full check.</para>
    ///
    /// <para>It never throws. A save that is missing, held by another program, too large or not a
    /// save at all gives a header without facts, and the card shows without them.</para>
    /// </summary>
    public sealed class SaveHeader
    {
        /// <summary>The envelope's format word, as the save store writes it.</summary>
        public const string Format = "gamesim-unity-save";
        /// <summary>The largest file looked at: the save store's own limit.</summary>
        public const long MaximumBytes = 8 * 1024 * 1024;

        /// <summary>The season's week, or null when it was not read.</summary>
        public int? Week { get; private set; }
        /// <summary>How many houseguests are still in the house, or null when they were not read.</summary>
        public int? Left { get; private set; }
        /// <summary>When the save was written (UTC), or null when it was not read.</summary>
        public DateTime? SavedUtc { get; private set; }
        /// <summary>Whether another program held the file as it was looked at (a plain IOException).</summary>
        public bool Held { get; private set; }

        /// <summary>Whether the week and the houseguests were both read: what the card needs to say them.</summary>
        public bool HasFacts => Week.HasValue && Left.HasValue;

        /// <summary>A header with no facts: the card shows without them.</summary>
        public static SaveHeader None() => new SaveHeader();

        /// <summary>Looks at the top of the save at <paramref name="path"/>. Never throws, never writes.</summary>
        public static SaveHeader Peek(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return None();
            try
            {
                if (!File.Exists(path)) return None();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                {
                    if (stream.Length > MaximumBytes) return None();
                    using (var text = new StreamReader(stream, new UTF8Encoding(false), true))
                    using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64, CloseInput = false })
                        return Read(reader);
                }
            }
            catch (Exception error)
            {
                // A label must never stop the front door from opening: every failure is the plain card.
                // Only the exact IOException is a hold (EpisodeSaveStore.IsHeld); its subclasses are a
                // missing folder or file, which say the season is not there.
                return new SaveHeader { Held = error.GetType() == typeof(IOException) };
            }
        }

        private static SaveHeader Read(JsonTextReader reader)
        {
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return None();
            bool formatRead = false;
            DateTime? saved = null;
            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                string name = (string)reader.Value;
                if (!reader.Read()) return None();
                switch (name)
                {
                    case "format":
                        if (reader.TokenType != JsonToken.String || (string)reader.Value != Format) return None();
                        formatRead = true;
                        break;
                    case "version":
                        if (reader.TokenType != JsonToken.Integer || Convert.ToInt64(reader.Value) != 1) return None();
                        break;
                    case "savedAt":
                        if (reader.TokenType == JsonToken.String
                            && DateTimeOffset.TryParse((string)reader.Value, System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.RoundtripKind, out var at))
                            saved = at.UtcDateTime;
                        break;
                    case "state":
                        // The facts are inside the season: the envelope's format must have come first.
                        if (!formatRead || reader.TokenType != JsonToken.StartObject) return None();
                        var header = ReadSeason(reader);
                        if (header == null) return None();
                        header.SavedUtc = saved;
                        return header;
                    default:
                        reader.Skip();
                        break;
                }
            }
            return None();
        }

        /// <summary>Reads the season object's week and houseguests, and stops as soon as it has both.</summary>
        private static SaveHeader ReadSeason(JsonTextReader reader)
        {
            int? week = null, left = null;
            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                string name = (string)reader.Value;
                if (!reader.Read()) return null;
                if (name == "week")
                {
                    if (reader.TokenType != JsonToken.Integer) return null;
                    long value = Convert.ToInt64(reader.Value);
                    if (value < 1 || value > 1000) return null;
                    week = (int)value;
                }
                else if (name == "contestants")
                {
                    left = CountActive(reader);
                    if (left == null) return null;
                }
                else reader.Skip();
                if (week.HasValue && left.HasValue) return new SaveHeader { Week = week, Left = left };
            }
            return null;
        }

        /// <summary>How many houseguests in the array are still in the house (status Active, 0), or null for no array.</summary>
        private static int? CountActive(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.StartArray) return null;
            int active = 0;
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                if (reader.TokenType != JsonToken.StartObject) return null;
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    string name = (string)reader.Value;
                    if (!reader.Read()) return null;
                    if (name == "status" && (reader.TokenType == JsonToken.Integer ? Convert.ToInt64(reader.Value) == 0
                            : reader.TokenType == JsonToken.String && (string)reader.Value == "Active"))
                        active++;
                    // A value of any other shape is stepped over whole (Skip does nothing on a plain value).
                    reader.Skip();
                }
                if (reader.TokenType != JsonToken.EndObject) return null;
            }
            return reader.TokenType == JsonToken.EndArray ? active : (int?)null;
        }
    }
}
