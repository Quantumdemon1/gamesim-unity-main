using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Where the settings live (PLAN A, A13): the keys PlayerPrefs has always held, behind one face,
    /// so the director reads and writes them the same way whether they are the machine's
    /// (<see cref="PlayerPrefsStore"/>), an isolated playtest root's own file
    /// (<see cref="RootFileStore"/>), or nowhere at all - the defaults a test or the verifier runs on
    /// (<see cref="DefaultPreferences"/>).
    /// </summary>
    public interface IPreferenceStore
    {
        /// <summary>Which store, in words for the log.</summary>
        PreferenceRoute Route { get; }
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        string GetString(string key, string fallback);
        void SetInt(string key, int value);
        void SetString(string key, string value);
        /// <summary>Writes what was set since the last save, where the store writes anything.</summary>
        void Save();
    }

    /// <summary>The machine's PlayerPrefs: an ordinary install, as it has always been.</summary>
    public sealed class PlayerPrefsStore : IPreferenceStore
    {
        public static readonly PlayerPrefsStore Instance = new PlayerPrefsStore();
        private PlayerPrefsStore() { }
        public PreferenceRoute Route => PreferenceRoute.PlayerPrefs;
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>
    /// The defaults and nothing else: every read is its fallback and every write is dropped. What an
    /// isolated root that did not ask for its own preferences has always had.
    /// </summary>
    public sealed class DefaultPreferences : IPreferenceStore
    {
        public static readonly DefaultPreferences Instance = new DefaultPreferences();
        private DefaultPreferences() { }
        public PreferenceRoute Route => PreferenceRoute.Defaults;
        public bool HasKey(string key) => false;
        public int GetInt(string key, int fallback) => fallback;
        public string GetString(string key, string fallback) => fallback;
        public void SetInt(string key, int value) { }
        public void SetString(string key, string value) { }
        public void Save() { }
    }

    /// <summary>
    /// An isolated root's own preferences, in <c>&lt;root&gt;/preferences.json</c>
    /// (<see cref="PreferenceFile"/>). Read once as the store opens; written whole as a pending file
    /// and swapped into place the way the season's save is (<c>SaveJson.SwapIntoPlace</c>,
    /// which waits out another process's brief hold), and only when a value changed.
    /// </summary>
    public sealed class RootFileStore : IPreferenceStore
    {
        private readonly PreferenceFile file;
        private bool dirty, warned;

        public string Path { get; }

        /// <summary>Why the file read as no preferences, or null when it read well or did not exist.</summary>
        public string ReadProblem { get; }

        public RootFileStore(string root)
        {
            Path = System.IO.Path.Combine(root, PreferenceFile.FileName);
            file = PreferenceFile.Read(Path, out var reason, SaveJson.ReadText);
            ReadProblem = reason;
            if (reason != null) Debug.LogWarning("Gamesim settings file read as defaults: " + reason + ".");
        }

        public PreferenceRoute Route => PreferenceRoute.RootFile;
        public bool HasKey(string key) => file.HasKey(key);
        public int GetInt(string key, int fallback) => file.GetInt(key, fallback);
        public string GetString(string key, string fallback) => file.GetString(key, fallback);
        public void SetInt(string key, int value) => dirty |= file.SetInt(key, value);
        public void SetString(string key, string value) => dirty |= file.Set(key, value ?? "");

        public void Save()
        {
            if (!dirty) return;
            string pending = Path + ".pending";
            try
            {
                string directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(pending, file.ToJson(), new UTF8Encoding(false));
                SaveJson.SwapIntoPlace(pending, Path, null);
                dirty = false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                try { if (File.Exists(pending)) File.Delete(pending); } catch (Exception) { }
                // Once a session: the settings still apply, they are only not kept for the next.
                if (!warned) Debug.LogWarning("Gamesim settings could not be saved beside the saves (" + error.GetType().Name + "); they apply until the game closes.");
                warned = true;
            }
        }
    }

    /// <summary>Opens the store a route names.</summary>
    public static class PreferenceStores
    {
        public static IPreferenceStore Open(PreferenceRoute route, string root)
        {
            switch (route)
            {
                case PreferenceRoute.RootFile: return string.IsNullOrEmpty(root) ? (IPreferenceStore)DefaultPreferences.Instance : new RootFileStore(root);
                case PreferenceRoute.Defaults: return DefaultPreferences.Instance;
                default: return PlayerPrefsStore.Instance;
            }
        }
    }
}
