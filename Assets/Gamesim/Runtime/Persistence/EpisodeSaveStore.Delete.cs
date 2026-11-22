using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gamesim.Persistence
{
    /// <summary>What <see cref="EpisodeSaveStore.DeleteSeason"/> did: deleted, or why it deleted nothing.</summary>
    public enum SeasonDeleteResult { Deleted, NotInFolder, NotASeason, Playing, Link, InUse, Missing, Failed }

    /// <summary>
    /// Which files in a save folder are seasons, and deleting one season (menu slice 4, the review's
    /// change 2: the list of saves with Load and Delete). Nothing here reads what a save holds, so
    /// the Unity-free suite compiles this file by itself.
    /// </summary>
    public sealed partial class EpisodeSaveStore
    {
        /// <summary>
        /// The names the game gives a season's save: "episode.json" (the first), "episode-&lt;32 hex&gt;.json"
        /// (a new season) and "import-&lt;32 hex&gt;.json" (a season brought in from the web version). The
        /// career record, the preferences, the houseguest library, a backup, a write in progress and a
        /// copy Recover backup kept are none of these.
        /// </summary>
        private static readonly Regex SeasonName = new Regex(@"^(?:episode|(?:episode|import)-[0-9a-f]{32})\.json$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Whether <paramref name="fileName"/> (a name, not a path) is a season's save.</summary>
        public static bool IsSeasonFileName(string fileName) =>
            !string.IsNullOrEmpty(fileName) && fileName == Path.GetFileName(fileName) && SeasonName.IsMatch(fileName);

        /// <summary>
        /// Every season's save directly inside <paramref name="folder"/> (full paths), in no order;
        /// none when the folder is missing or cannot be read. Reads the folder's names only.
        /// </summary>
        public static IReadOnlyList<string> SeasonFiles(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Array.Empty<string>();
                return Directory.GetFiles(folder).Where(file => IsSeasonFileName(Path.GetFileName(file))).Select(Path.GetFullPath).ToArray();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>Whether a path is a link (a symbolic link or a junction) rather than the file or folder itself.</summary>
        public static bool IsLink(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

        /// <summary>
        /// Deletes one season: its save and the files this store keeps beside that save - the backup
        /// (&lt;save&gt;.backup), a write left in progress (&lt;save&gt;.pending-*) and the copies Recover
        /// backup kept (&lt;save&gt;.before-recovery-*.json). Nothing else: not another season, not the
        /// career record, not the houseguest library.
        ///
        /// <para>It deletes nothing when <paramref name="savePath"/>, resolved, is not directly inside
        /// <paramref name="folder"/> or is not named as a season's save; when it is the season being
        /// played (<paramref name="playingSavePath"/>); when the folder or any of the files is a link;
        /// or when any of the files is held by another program. A held file is found before anything
        /// is touched, and the files are then renamed aside one by one - a rename a hold refuses puts
        /// back those already moved - and only once all are aside are they deleted, by the store's one
        /// remover (<see cref="RemoveSetAside"/>); any it could not delete then are removed by
        /// <see cref="SweepInterruptedDeletes"/> the next time the list looks.</para>
        ///
        /// <para><paramref name="isLink"/> is <see cref="IsLink"/> unless a test names another: making a
        /// real link on Windows needs a privilege a test run does not have.</para>
        /// </summary>
        public static SeasonDeleteResult DeleteSeason(string folder, string savePath, string playingSavePath, Func<string, bool> isLink = null)
        {
            if (isLink == null) isLink = IsLink;
            string root, target;
            try
            {
                if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(savePath)) return SeasonDeleteResult.NotInFolder;
                root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                target = Path.GetFullPath(Path.IsPathRooted(savePath) ? savePath : Path.Combine(root, savePath));
            }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
            {
                return SeasonDeleteResult.NotInFolder;
            }
            if (!string.Equals(Path.GetDirectoryName(target), root, StringComparison.OrdinalIgnoreCase)) return SeasonDeleteResult.NotInFolder;
            string name = Path.GetFileName(target);
            if (!IsSeasonFileName(name)) return SeasonDeleteResult.NotASeason;
            if (!string.IsNullOrEmpty(playingSavePath))
            {
                try
                {
                    if (string.Equals(Path.GetFullPath(playingSavePath), target, StringComparison.OrdinalIgnoreCase)) return SeasonDeleteResult.Playing;
                }
                catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
                {
                    return SeasonDeleteResult.Failed;
                }
            }

            List<string> files;
            try
            {
                if (!Directory.Exists(root) || !File.Exists(target)) return SeasonDeleteResult.Missing;
                if (isLink(root)) return SeasonDeleteResult.Link;
                files = FilesOfSeason(root, name);
                if (files.Any(isLink)) return SeasonDeleteResult.Link;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return SeasonDeleteResult.Failed;
            }

            // Held? Opening each alone finds any other program's handle before anything moves.
            foreach (var file in files)
            {
                try { using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)) { } }
                catch (FileNotFoundException) { }
                catch (IOException) { return SeasonDeleteResult.InUse; }
                catch (UnauthorizedAccessException) { return SeasonDeleteResult.Failed; }
            }

            // Aside, all or none.
            var moved = new List<(string from, string to)>();
            string token = ".deleting-" + Guid.NewGuid().ToString("N");
            foreach (var file in files)
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    File.Move(file, file + token);
                    moved.Add((file, file + token));
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    foreach (var (from, to) in Enumerable.Reverse(moved))
                    {
                        try { File.Move(to, from); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                    return error is UnauthorizedAccessException || error.GetType() != typeof(IOException) ? SeasonDeleteResult.Failed : SeasonDeleteResult.InUse;
                }
            }
            RemoveSetAside(moved.Select(pair => pair.to));
            return SeasonDeleteResult.Deleted;
        }

        /// <summary>
        /// The store's one remover: deletes files <see cref="DeleteSeason"/> set aside, and nothing
        /// else: a file without the ".deleting-&lt;32 hex&gt;" a delete gives the files it sets aside, which
        /// no season's save, backup or career file ever has, is passed over. A
        /// file it cannot delete (held, denied) is left for the next look. How many it deleted.
        /// </summary>
        private static int RemoveSetAside(IEnumerable<string> aside)
        {
            int removed = 0;
            foreach (var file in aside)
            {
                if (!DeletingMark.IsMatch(Path.GetFileName(file) ?? "")) continue;
                try { File.Delete(file); removed++; }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            }
            return removed;
        }

        /// <summary>The mark a delete puts after the name of each file it sets aside.</summary>
        private static readonly Regex DeletingMark = new Regex(@"\.deleting-[0-9a-f]{32}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// A file a delete set aside: one of a season's own files (its save, its backup, a write left in
        /// progress, a copy Recover backup kept) with ".deleting-&lt;32 hex&gt;" after its name. Group 1 is
        /// the season's save name.
        /// </summary>
        private static readonly Regex SetAsideName = new Regex(
            @"^((?:episode|(?:episode|import)-[0-9a-f]{32})\.json)(?:\.backup|\.pending-[0-9a-f]{32}|\.before-recovery-[0-9a-f]{32}\.json)?\.deleting-[0-9a-f]{32}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Files an interrupted delete left behind (a crash between setting a season's files aside and
        /// deleting them, or a delete a hold refused) are removed: what the player already confirmed.
        /// The season list calls this each time it looks at the folder.
        ///
        /// <para>Only files named as <see cref="DeleteSeason"/> names what it sets aside, directly inside
        /// <paramref name="folder"/>, and only when the season they belonged to has no save under its
        /// own name any more: a season whose save is there (one by the same name started since, or a
        /// put-back that half failed) keeps every file beside it. Never a file that is a save, a backup,
        /// the career record or anything else; never through a link (the folder or the file). A file
        /// that cannot be deleted now is left for the next look. How many were removed.</para>
        /// </summary>
        public static int SweepInterruptedDeletes(string folder, Func<string, bool> isLink = null)
        {
            if (isLink == null) isLink = IsLink;
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return 0;
                string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (isLink(root)) return 0;
                var leftovers = new List<string>();
                foreach (var file in Directory.GetFiles(root))
                {
                    var match = SetAsideName.Match(Path.GetFileName(file));
                    if (!match.Success) continue;
                    string path = Path.Combine(root, match.Value);
                    if (File.Exists(Path.Combine(root, match.Groups[1].Value)) || isLink(path)) continue;
                    leftovers.Add(path);
                }
                return leftovers.Count == 0 ? 0 : RemoveSetAside(leftovers);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException
                || error is NotSupportedException)
            {
                return 0;
            }
        }

        /// <summary>
        /// Whether a failure to open a file is another program holding it for a moment - a backup or
        /// sync client, an indexer - rather than the file being missing, denied or damaged: exactly a
        /// plain IOException (a sharing violation), never one of its subclasses. Such a file may be a
        /// perfectly good save, and looking again shortly is the answer.
        /// </summary>
        public static bool IsHeld(Exception error) => error != null && error.GetType() == typeof(IOException);

        /// <summary>
        /// The files that are one season's, in <paramref name="root"/>: the save named
        /// <paramref name="saveName"/> and what this store keeps beside it, matched by name, the save first.
        /// </summary>
        public static List<string> FilesOfSeason(string root, string saveName)
        {
            string save = Path.Combine(root, saveName);
            var files = new List<string> { save };
            string backup = save + ".backup", pending = saveName + ".pending-", kept = saveName + ".before-recovery-";
            foreach (var file in Directory.GetFiles(root))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(Path.Combine(root, name), backup, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(pending, StringComparison.OrdinalIgnoreCase)
                    || (name.StartsWith(kept, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                    files.Add(Path.Combine(root, name));
            }
            return files;
        }
    }
}
