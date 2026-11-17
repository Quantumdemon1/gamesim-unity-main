using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Persistence
{
    /// <summary>One saved season as its card shows it (menu slice 4): whose it is, how far it got, when it was saved.</summary>
    public sealed class SavedSeason
    {
        /// <summary>The save's full path.</summary>
        public string Path;
        /// <summary>The save's file name without ".json": what names the card's controls.</summary>
        public string Key;
        /// <summary>Whether the game can open it: the same check a load makes.</summary>
        public bool Readable;
        /// <summary>The player's houseguest, or null.</summary>
        public string PlayerName;
        /// <summary>The season's week.</summary>
        public int Week;
        /// <summary>How many houseguests are still in the house.</summary>
        public int Left;
        /// <summary>When the file was last written (UTC), or null for a season never written.</summary>
        public DateTime? SavedUtc;

        /// <summary>A season as <paramref name="state"/> holds it: the running one, read from memory.</summary>
        public static SavedSeason Of(EpisodeState state, string path, DateTime? savedUtc) => new SavedSeason
        {
            Path = path,
            Key = path == null ? "" : System.IO.Path.GetFileNameWithoutExtension(path),
            Readable = state != null,
            PlayerName = state?.Find(state.playerId)?.name,
            Week = state != null ? state.week : 0,
            Left = state?.contestants == null ? 0 : state.contestants.Count(actor => actor != null && actor.status == ContestantStatus.Active),
            SavedUtc = savedUtc,
        };
    }

    /// <summary>
    /// The saved seasons in a save folder, newest first (menu slice 4, the review's change 2: "older
    /// seasons can never be opened again"). Each save is read with the check a load makes - old
    /// versions are brought up to date in memory only - and nothing is ever written: no file, no
    /// file time, no migration on disk. A save is read again only when its size or its time
    /// changes, so drawing the list on every render costs a look at the folder.
    /// </summary>
    public sealed class SeasonShelf
    {
        private readonly Dictionary<string, (long length, DateTime written, SavedSeason season)> known =
            new Dictionary<string, (long, DateTime, SavedSeason)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>How many saves the last <see cref="Read"/> opened (a test's window on the cache).</summary>
        public int LastOpened { get; private set; }

        /// <summary>Every season's save in <paramref name="folder"/>, newest first.</summary>
        public IReadOnlyList<SavedSeason> Read(string folder)
        {
            LastOpened = 0;
            var seasons = new List<SavedSeason>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in EpisodeSaveStore.SeasonFiles(folder))
            {
                long length; DateTime written;
                try
                {
                    var info = new FileInfo(file);
                    if (!info.Exists) continue;
                    length = info.Length; written = info.LastWriteTimeUtc;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { continue; }
                seen.Add(file);
                if (known.TryGetValue(file, out var held) && held.length == length && held.written == written) { seasons.Add(held.season); continue; }
                LastOpened++;
                bool readable = EpisodeSaveStore.TryPeek(file, out var state, out bool unopened);
                var season = readable ? SavedSeason.Of(state, file, written)
                    : new SavedSeason { Path = file, Key = Path.GetFileNameWithoutExtension(file), Readable = false, SavedUtc = written };
                // A file another program held for a moment is looked at again next time.
                if (readable || !unopened) known[file] = (length, written, season);
                else known.Remove(file);
                seasons.Add(season);
            }
            foreach (var gone in known.Keys.Where(file => !seen.Contains(file)).ToList()) known.Remove(gone);
            return Newest(seasons);
        }

        /// <summary>Newest first, by when each was saved; a tie by name, so the order never shuffles.</summary>
        public static IReadOnlyList<SavedSeason> Newest(IEnumerable<SavedSeason> seasons) =>
            seasons.OrderByDescending(season => season.SavedUtc ?? DateTime.MinValue)
                .ThenBy(season => season.Key, StringComparer.Ordinal).ToList();
    }

    public sealed partial class EpisodeSaveStore
    {
        /// <summary>
        /// Reads <paramref name="file"/> as a load would - its envelope, its checksum, its version
        /// brought up to date in memory, its rules - and writes nothing. <paramref name="unopened"/>
        /// says the file could not be opened at all (held, or denied), which says nothing about it.
        /// </summary>
        internal static bool TryPeek(string file, out EpisodeState state, out bool unopened)
        {
            bool read = TryRead(file, null, out state, out _, out var error, out _);
            unopened = error != null;
            return read;
        }
    }
}
