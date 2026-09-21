using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Gamesim.Persistence
{
    /// <summary>A separate, bounded, atomic local library; never rewrites an active season.</summary>
    public sealed class CharacterProfileStore
    {
        private const string Format = "gamesim-houseguest";
        private const int MaximumBytes = 262144, MaximumProfiles = 200;
        private static readonly ConcurrentDictionary<string, object> Gates = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly string directory;
        private readonly object gate;
        public readonly List<string> ReadErrors = new List<string>();
        public string DirectoryPath => directory;

        public CharacterProfileStore(string directory = null)
        {
            this.directory = Path.GetFullPath(directory ?? Path.Combine(Application.persistentDataPath, "Houseguests"));
            gate = Gates.GetOrAdd(this.directory, _ => new object());
        }

        public List<CharacterProfile> List()
        {
            lock (gate)
            {
                ReadErrors.Clear();
                var profiles = new List<CharacterProfile>();
                if (!Directory.Exists(directory)) return profiles;
                string[] files;
                try { files = Directory.EnumerateFiles(directory, "*.json").OrderBy(x => x, StringComparer.Ordinal).Take(MaximumProfiles).ToArray(); }
                catch (Exception exception) when (Expected(exception))
                { ReadErrors.Add("The houseguest library could not be read: " + SaveJson.Explain(exception)); return profiles; }
                foreach (var file in files)
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    if (TryLoad(id, out var profile, out var error)) profiles.Add(profile);
                    if (!string.IsNullOrEmpty(error)) ReadErrors.Add(Path.GetFileName(file) + ": " + error);
                }
                return profiles.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
            }
        }

        public bool Save(CharacterProfile profile, out string error)
        {
            error = null;
            if (profile == null) { error = "A houseguest is required."; return false; }
            if (!profile.TryValidate(out error)) return false;
            lock (gate)
            {
                string temporary = null;
                try
                {
                    string path = PathFor(profile.id);
                    Directory.CreateDirectory(directory);
                    if (!File.Exists(path) && Directory.EnumerateFiles(directory, "*.json").Take(MaximumProfiles).Count() >= MaximumProfiles)
                        throw new InvalidDataException("The library holds up to 200 houseguests. Delete one before saving another.");
                    if (File.Exists(path) && !Read(path, profile.id, out _, out _))
                        throw new InvalidDataException("The existing profile is unreadable. Save a new copy to preserve it.");
                    var payload = JObject.FromObject(profile.Clone(), SaveJson.Serializer());
                    var envelope = new JObject { ["format"] = Format, ["version"] = 1,
                        ["checksum"] = SaveJson.Hash(SaveJson.Canonical(payload)), ["profile"] = payload };
                    var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented));
                    if (bytes.Length > MaximumBytes) throw new InvalidDataException("The houseguest exceeds the profile size limit.");
                    temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    SaveJson.WriteNewDurable(temporary, bytes);
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".backup");
                    else File.Move(temporary, path);
                    temporary = null;
                    return true;
                }
                catch (Exception exception) when (Expected(exception)) { error = exception.Message; return false; }
                finally { if (temporary != null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
            }
        }

        public bool TryLoad(string id, out CharacterProfile profile, out string error)
        {
            profile = null; error = null;
            lock (gate)
            {
                try
                {
                    string path = PathFor(id);
                    if (Read(path, id, out profile, out error)) return true;
                    if (Read(path + ".backup", id, out profile, out _))
                    { error = "Recovered the previous valid profile in memory. The original file was preserved."; return true; }
                    return false;
                }
                catch (Exception exception) when (Expected(exception)) { error = exception.Message; return false; }
            }
        }

        public bool Delete(string id, out string error)
        {
            error = null;
            lock (gate)
            {
                try { string path = PathFor(id); File.Delete(path); File.Delete(path + ".backup"); return true; }
                catch (Exception exception) when (Expected(exception)) { error = exception.Message; return false; }
            }
        }

        private string PathFor(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid houseguest profile identifier.");
            return Path.Combine(directory, id.ToLowerInvariant() + ".json");
        }

        private static bool Read(string path, string id, out CharacterProfile profile, out string error)
        {
            profile = null; error = null;
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Houseguest profile not found.");
                if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("The houseguest exceeds the profile size limit.");
                var envelope = SaveJson.ParseObject(File.ReadAllText(path));
                if (envelope.Properties().Count() != 4 || (string)envelope["format"] != Format
                    || envelope["version"]?.Type != JTokenType.Integer || (int)envelope["version"] != 1
                    || envelope["checksum"]?.Type != JTokenType.String || envelope["profile"] is not JObject payload)
                    throw new InvalidDataException("Unsupported houseguest profile format.");
                if ((string)envelope["checksum"] != SaveJson.Hash(SaveJson.Canonical(payload)))
                    throw new InvalidDataException("The houseguest profile checksum does not match.");
                SaveJson.CheckDtoShape(payload, typeof(CharacterProfile), "profile");
                var parsed = payload.ToObject<CharacterProfile>(SaveJson.Serializer());
                if (!parsed.TryValidate(out error)) return false;
                if (!string.Equals(parsed.id, id, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The profile file has a different identity.");
                profile = parsed;
                return true;
            }
            catch (Exception exception) when (Expected(exception)) { error = exception.Message; return false; }
        }
        private static bool Expected(Exception exception) => exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException
            || exception is JsonException || exception is ArgumentException || exception is OverflowException;
    }
}
