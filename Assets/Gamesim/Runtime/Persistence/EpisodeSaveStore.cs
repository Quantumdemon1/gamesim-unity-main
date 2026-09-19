using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Gamesim.Persistence
{
    /// <summary>Versioned local saves with explicit recovery and an atomically rotated good backup.</summary>
    public sealed class EpisodeSaveStore
    {
        private const string Format = "gamesim-unity-save";
        private static readonly ConcurrentDictionary<string, object> Gates = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly string path;
        private readonly object gate;
        public string SavePath => path;
        public string BackupPath => path + ".backup";

        public EpisodeSaveStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save file path is required.", nameof(path));
            this.path = Path.GetFullPath(path);
            gate = Gates.GetOrAdd(this.path, _ => new object());
        }

        public void Save(EpisodeState state)
        {
            EpisodeSaveValidation.Validate(state);
            var token = JObject.FromObject(state, SaveJson.Serializer());
            var envelope = new JObject
            {
                ["format"] = Format,
                ["version"] = 1,
                ["savedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["checksum"] = SaveJson.Hash(SaveJson.Canonical(token)),
                ["state"] = token
            };
            var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented));
            lock (gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path) && !TryRead(path, out _, out _))
                    throw new InvalidDataException("The current save is unreadable. Choose backup recovery or another slot before saving; existing copies were preserved.");
                Install(bytes, rotateBackup: true);
            }
        }

        public bool TryLoad(out EpisodeState state, out string message)
        {
            lock (gate)
            {
                if (TryRead(path, out state, out message))
                {
                    message = "Local episode loaded and validated." + (string.IsNullOrEmpty(message) ? "" : " " + message);
                    return true;
                }

                if (TryRead(BackupPath, out _, out _))
                    message += " A validated backup is available; choose Recover backup to restore it.";
                return false;
            }
        }

        public bool TryRecoverBackup(out EpisodeState state, out string message)
        {
            state = null;
            lock (gate)
            {
                if (!TryRead(BackupPath, out var recovered, out message))
                {
                    message = "Backup recovery unavailable: " + message;
                    return false;
                }

                try
                {
                    var backupBytes = File.ReadAllBytes(BackupPath);
                    string retained = null;
                    if (File.Exists(path))
                    {
                        retained = path + ".before-recovery-" + Guid.NewGuid().ToString("N") + ".json";
                        SaveJson.WriteNewDurable(retained, File.ReadAllBytes(path));
                    }

                    // The backup is intentionally not a replacement target, even if the primary was damaged.
                    Install(backupBytes, rotateBackup: false);
                    if (!TryRead(path, out state, out var installedError)) throw new InvalidDataException(installedError);
                    message = "Backup restored and validated. The backup remains available."
                        + (retained == null ? "" : " Previous primary preserved at " + retained)
                        + (string.IsNullOrEmpty(installedError) ? "" : " " + installedError);
                    return true;
                }
                catch (Exception error) when (SaveJson.IsExpected(error))
                {
                    message = "Backup recovery failed; existing backup was preserved. " + error.Message;
                    return false;
                }
            }
        }

        private void Install(byte[] bytes, bool rotateBackup)
        {
            var temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            try
            {
                SaveJson.WriteNewDurable(temporary, bytes);
                if (!TryRead(temporary, out _, out var reason)) throw new InvalidDataException(reason);
                if (File.Exists(path)) File.Replace(temporary, path, rotateBackup ? BackupPath : null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static bool TryRead(string file, out EpisodeState state, out string message)
        {
            state = null;
            message = "No local save exists in this slot.";
            try
            {
                if (!File.Exists(file)) return false;
                if (new FileInfo(file).Length > SaveJson.MaximumBytes) throw new InvalidDataException("Save exceeds the supported size limit.");
                var envelope = SaveJson.ParseObject(File.ReadAllText(file, Encoding.UTF8));
                if (envelope.Properties().Any(property => !new[] { "format", "version", "savedAt", "checksum", "state" }.Contains(property.Name))
                    || (string)envelope["format"] != Format || envelope["version"]?.Type != JTokenType.Integer
                    || (long)envelope["version"] != 1)
                    throw new InvalidDataException("Unknown or unsupported Unity save envelope.");
                if (envelope["state"] is not JObject payload || envelope["checksum"]?.Type != JTokenType.String
                    || envelope["savedAt"]?.Type != JTokenType.String
                    || !DateTimeOffset.TryParse((string)envelope["savedAt"], CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out _))
                    throw new InvalidDataException("Unity save metadata is invalid.");
                if (!string.Equals((string)envelope["checksum"], SaveJson.Hash(SaveJson.Canonical(payload)), StringComparison.Ordinal))
                    throw new InvalidDataException("Save checksum does not match; the file may be damaged.");
                var current = EpisodeSaveMigrations.PrepareCurrentPayload(payload, out var migrated);
                SaveJson.CheckDtoShape(current, typeof(EpisodeState), "state");
                var parsed = current.ToObject<EpisodeState>(SaveJson.Serializer());
                EpisodeSaveValidation.Validate(parsed);
                state = parsed;
                message = migrated ? "Schema " + (int)payload["schemaVersion"]
                    + " history was migrated to schema " + parsed.schemaVersion
                    + " in memory. Disk format changes only on the next save." : null;
                if (parsed.blocRulesStartWeek > parsed.week)
                    message = (string.IsNullOrEmpty(message) ? "" : message + " ")
                        + "Voting-bloc rules begin in week " + parsed.blocRulesStartWeek + "; the current week is unchanged.";
                if (parsed.npcSocial.rulesStartWeek > parsed.week)
                    message = (string.IsNullOrEmpty(message) ? "" : message + " ")
                        + "NPC conversations begin in week " + parsed.npcSocial.rulesStartWeek + "; the current week is unchanged.";
                return true;
            }
            catch (Exception error) when (SaveJson.IsExpected(error))
            {
                message = "Local save could not be loaded: " + error.Message;
                return false;
            }
        }
    }

    internal static class SaveJson
    {
        public const int MaximumBytes = 8 * 1024 * 1024;
        // Cache reflection contracts only. Configuration is fixed; each operation still gets its own serializer/settings.
        private static readonly PublicFieldsResolver SharedPublicFieldsResolver = new PublicFieldsResolver();
        public static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            // Constructors may contain nonempty defaults (persona score order).
            // Populate would append saved entries and corrupt otherwise valid state.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64,
            ContractResolver = SharedPublicFieldsResolver
        });

        public static JObject ParseObject(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new InvalidDataException("JSON is missing or exceeds the eight MiB size limit.");
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
            var token = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (token is not JObject result || reader.Read()) throw new InvalidDataException("Expected one JSON object.");
            return result;
        }

        public static string Canonical(JToken token)
        {
            JToken Ordered(JToken value)
            {
                if (value is JObject obj) return new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new JProperty(property.Name, Ordered(property.Value))));
                if (value is JArray array) return new JArray(array.Select(Ordered));
                return value.DeepClone();
            }

            return Ordered(token).ToString(Formatting.None);
        }

        public static string Hash(string value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        public static void CheckDtoShape(JToken token, Type type, string path)
        {
            if (token.Type == JTokenType.Null)
            {
                if (type.IsValueType) throw new InvalidDataException(path + " cannot be null.");
                return;
            }
            if (type == typeof(string))
            {
                if (token.Type != JTokenType.String) throw new InvalidDataException(path + " must be text.");
                return;
            }
            if (type == typeof(bool))
            {
                if (token.Type != JTokenType.Boolean) throw new InvalidDataException(path + " must be boolean.");
                return;
            }
            if (type.IsEnum || type == typeof(int) || type == typeof(uint) || type == typeof(long))
            {
                if (token.Type != JTokenType.Integer) throw new InvalidDataException(path + " must be an integer.");
                return;
            }
            if (type == typeof(double))
            {
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                    throw new InvalidDataException(path + " must be numeric.");
                var number = (double)token;
                if (double.IsNaN(number) || double.IsInfinity(number)) throw new InvalidDataException(path + " must be finite.");
                return;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (token is not JArray array) throw new InvalidDataException(path + " must be an array.");
                foreach (var element in array) CheckDtoShape(element, type.GetGenericArguments()[0], path + "[]");
                return;
            }
            if (token is not JObject obj) throw new InvalidDataException(path + " must be an object.");
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            if (obj.Properties().Count() != fields.Length || fields.Any(field => obj.Property(field.Name) == null))
                throw new InvalidDataException(path + " contains missing or unknown schema fields.");
            foreach (var field in fields) CheckDtoShape(obj[field.Name], field.FieldType, path + "." + field.Name);
        }

        public static void WriteNewDurable(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new IOException("Written bytes could not be verified.");
        }

        public static bool IsExpected(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException
            || error is JsonException || error is ArgumentException || error is OverflowException || error is FormatException
            || error is InvalidCastException;

        private sealed class PublicFieldsResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
            }
        }
    }
}
