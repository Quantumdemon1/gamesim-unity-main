using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>One finished season, as the career remembers it.</summary>
    [Serializable]
    public sealed class CareerSeason
    {
        public string sessionId;
        public uint seed;
        public string finishedAt;
        public string playerName;
        public List<string> cast = new List<string>();
        public int houseSize, weeks;
        /// <summary>1 for the winner, 2 for the runner-up, then the order the house emptied in.</summary>
        public int placement;
        /// <summary>Winner, Runner-up, Jury or Pre-jury: the word the report uses.</summary>
        public string outcome;
        /// <summary>True when the season went on without the player in it.</summary>
        public bool spectated;
        public int hohWins, vetoWins, timesNominated, weeksOnBlock, juryVotesReceived;
        public string winnerName;
    }

    /// <summary>The file's content. Versioned like a save: a field added later needs a new schema.</summary>
    [Serializable]
    public sealed class CareerRecord
    {
        public const int CurrentSchema = 1;
        public int schemaVersion = CurrentSchema;
        public List<CareerSeason> seasons = new List<CareerSeason>();
    }

    /// <summary>
    /// What a career adds up to. Derived on every read rather than stored, so the file holds
    /// only what happened and nothing that could drift from it.
    /// </summary>
    public sealed class CareerSummary
    {
        public int Seasons, Wins, RunnerUps, JuryFinishes, PreJuryFinishes;
        public int HohWins, VetoWins, TimesNominated, BestPlacement;
        public double MedianPlacement, WinRate;

        public static CareerSummary Of(CareerRecord record)
        {
            var summary = new CareerSummary();
            var seasons = record?.seasons ?? new List<CareerSeason>();
            summary.Seasons = seasons.Count;
            if (seasons.Count == 0) return summary;
            summary.Wins = seasons.Count(s => s.placement == 1);
            summary.RunnerUps = seasons.Count(s => s.placement == 2);
            summary.JuryFinishes = seasons.Count(s => s.outcome == "Jury");
            summary.PreJuryFinishes = seasons.Count(s => s.outcome == "Pre-jury");
            summary.HohWins = seasons.Sum(s => s.hohWins);
            summary.VetoWins = seasons.Sum(s => s.vetoWins);
            summary.TimesNominated = seasons.Sum(s => s.timesNominated);
            summary.BestPlacement = seasons.Where(s => s.placement > 0).Select(s => s.placement).DefaultIfEmpty(0).Min();
            summary.MedianPlacement = Median(seasons.Where(s => s.placement > 0).Select(s => (double)s.placement).ToList());
            summary.WinRate = (double)summary.Wins / seasons.Count;
            return summary;
        }

        /// <summary>The middle value, or the mean of the two middle values when there is no middle.</summary>
        public static double Median(List<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int half = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[half] : (sorted[half - 1] + sorted[half]) / 2.0;
        }

        /// <summary>"4th" for a whole placement, "4.5" when the median falls between two.</summary>
        public static string PlaceWord(double placement)
        {
            if (placement <= 0) return "—";
            if (Math.Abs(placement - Math.Round(placement)) > 0.001)
                return placement.ToString("0.#", CultureInfo.InvariantCulture);
            int place = (int)Math.Round(placement);
            return place + Ordinal(place);
        }

        public static string Ordinal(int value)
        {
            if (value % 100 >= 11 && value % 100 <= 13) return "th";
            switch (value % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }

        /// <summary>One line for the front door and the settings panel.</summary>
        public string Line()
        {
            if (Seasons == 0) return "No finished seasons yet";
            return Seasons + (Seasons == 1 ? " season" : " seasons")
                + " · " + Wins + (Wins == 1 ? " win" : " wins")
                + " · median finish " + PlaceWord(MedianPlacement)
                + " · best " + PlaceWord(BestPlacement)
                + " · " + HohWins + " HoH, " + VetoWins + " veto";
        }

        /// <summary>The line under the report's career card.</summary>
        public string Note()
        {
            if (Seasons == 0) return "No finished seasons yet.";
            return "Win rate " + Math.Round(WinRate * 100) + "% · "
                + RunnerUps + (RunnerUps == 1 ? " runner-up" : " runner-ups") + " · "
                + JuryFinishes + (JuryFinishes == 1 ? " jury finish" : " jury finishes") + " · "
                + PreJuryFinishes + " pre-jury · nominated " + TimesNominated
                + (TimesNominated == 1 ? " time" : " times") + " in all.";
        }
    }

    /// <summary>
    /// The player's record across seasons: one JSON file beside the saves that gains an entry
    /// when a season reaches its finale, and is read back for the main menu, the settings panel
    /// and the season report.
    ///
    /// <para>The web reference kept these behind sign-in as a cloud leaderboard, which the port
    /// deliberately did not carry over. This is the offline replacement: no account, no upload,
    /// one file per save root, so an isolated root — a test, a QA run — keeps its own.</para>
    ///
    /// <para>Entries are keyed by the season's <c>sessionId</c>, so recording the same finale twice
    /// — once when it is committed and again when the save is reloaded — adds one entry. A file
    /// that cannot be read is set aside under a dated name rather than overwritten, in the same
    /// spirit as the save store: nothing here deletes what a player played.</para>
    /// </summary>
    public sealed class CareerLedger
    {
        public const string FileName = "career.json";
        private const string Format = "gamesim-unity-career";
        private static readonly string[] EnvelopeFields = { "format", "version", "savedAt", "checksum", "record" };
        private readonly object gate = new object();
        private bool blocked;

        public string FilePath { get; }

        /// <summary>What the last load had to say when the file was not simply fine or absent.</summary>
        public string Notice { get; private set; }

        public CareerLedger(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A directory is required.", nameof(directory));
            FilePath = Path.GetFullPath(Path.Combine(directory, FileName));
        }

        /// <summary>
        /// The record on disk, or an empty one. A damaged file is moved aside and reported through
        /// <see cref="Notice"/>; it is never trusted and never silently replaced.
        /// </summary>
        public CareerRecord Load()
        {
            lock (gate)
            {
                Notice = null;
                blocked = false;
                if (!File.Exists(FilePath)) return new CareerRecord();
                try
                {
                    return Parse(File.ReadAllText(FilePath, Encoding.UTF8));
                }
                catch (Exception error) when (SaveJson.IsExpected(error))
                {
                    var archived = Archive("damaged");
                    blocked = archived == null;
                    Notice = archived != null
                        ? "The career record could not be read and was set aside as " + Path.GetFileName(archived) + ". " + error.Message
                        : "The career record could not be read or set aside; it is left untouched. " + error.Message;
                    return new CareerRecord();
                }
            }
        }

        /// <summary>
        /// Adds a finished season. Returns false when the season is not finished, was already
        /// recorded, or the file on disk is unreadable and could not be moved out of the way.
        /// </summary>
        public bool Record(EpisodeState state)
        {
            if (state == null || state.phase != EpisodePhase.Finished || string.IsNullOrEmpty(state.sessionId)) return false;
            lock (gate)
            {
                var record = Load();
                if (blocked) return false;
                if (record.seasons.Any(season => season.sessionId == state.sessionId)) return false;
                record.seasons.Add(Entry(state));
                Write(record);
                return true;
            }
        }

        /// <summary>
        /// Starts a fresh record. The old one is moved aside under a dated name and the path is
        /// returned; null when there was nothing to reset.
        /// </summary>
        public string Reset()
        {
            lock (gate)
            {
                Notice = null;
                return File.Exists(FilePath) ? Archive("reset") : null;
            }
        }

        // ---------------------------------------------------------------- derivation

        /// <summary>The season as the career will remember it, read from the finished state.</summary>
        public static CareerSeason Entry(EpisodeState state)
        {
            var you = state.Find(state.playerId);
            var winner = state.Find(state.winnerId);
            var status = you?.status ?? ContestantStatus.Evicted;
            return new CareerSeason
            {
                sessionId = state.sessionId,
                seed = state.seed,
                finishedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                playerName = you?.name,
                cast = state.contestants.Select(c => c.name).ToList(),
                houseSize = state.contestants.Count,
                weeks = state.week,
                placement = Placement(state, you),
                outcome = Outcome(status),
                spectated = status != ContestantStatus.Winner && status != ContestantStatus.RunnerUp,
                hohWins = you?.hohWins ?? 0,
                vetoWins = you?.vetoWins ?? 0,
                timesNominated = you?.timesNominated ?? 0,
                weeksOnBlock = you?.nominationWeeks?.Count ?? 0,
                juryVotesReceived = you == null ? 0 : state.votes.Count(v => v.targetId == you.id),
                winnerName = winner?.name,
            };
        }

        /// <summary>
        /// Where the player finished, exactly.
        ///
        /// <para>The finalists are first and second. Everyone else left in an order the state
        /// keeps: every evictee is added to the jury sentiment ledger as they go, including the
        /// one the final eviction sends out, so the position in that list is the eviction order
        /// without reading the event log — which is capped and can have lost the early weeks of a
        /// long season. A state without the ledger falls back to the report's coarser count.</para>
        /// </summary>
        public static int Placement(EpisodeState state, ContestantState you)
        {
            if (you == null) return 0;
            switch (you.status)
            {
                case ContestantStatus.Winner: return 1;
                case ContestantStatus.RunnerUp: return 2;
            }
            var jurors = state.jurySentiment?.jurors;
            int index = jurors == null ? -1 : jurors.FindIndex(juror => juror.jurorId == you.id);
            if (index >= 0) return state.contestants.Count - index;
            int below = state.contestants.Count(c => c.status == ContestantStatus.Evicted)
                        - (you.status == ContestantStatus.Evicted ? 1 : 0);
            return state.contestants.Count - below;
        }

        public static string Outcome(ContestantStatus status)
        {
            switch (status)
            {
                case ContestantStatus.Winner: return "Winner";
                case ContestantStatus.RunnerUp: return "Runner-up";
                case ContestantStatus.Jury: return "Jury";
                case ContestantStatus.Evicted: return "Pre-jury";
                default: return "Unfinished";
            }
        }

        // ---------------------------------------------------------------- file

        private static CareerRecord Parse(string json)
        {
            var envelope = SaveJson.ParseObject(json);
            if (envelope.Properties().Any(property => !EnvelopeFields.Contains(property.Name))
                || (string)envelope["format"] != Format || envelope["version"]?.Type != JTokenType.Integer
                || (long)envelope["version"] != 1)
                throw new InvalidDataException("Unknown or unsupported career file envelope.");
            if (envelope["record"] is not JObject payload || envelope["checksum"]?.Type != JTokenType.String)
                throw new InvalidDataException("Career file metadata is invalid.");
            if (!string.Equals((string)envelope["checksum"], SaveJson.Hash(SaveJson.Canonical(payload)), StringComparison.Ordinal))
                throw new InvalidDataException("Career file checksum does not match; the file may be damaged.");
            if (payload["schemaVersion"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("Career file has no schema version.");
            int schema = (int)payload["schemaVersion"];
            if (schema != CareerRecord.CurrentSchema)
                throw new InvalidDataException("Career file schema " + schema + " is not one this build reads.");
            SaveJson.CheckDtoShape(payload, typeof(CareerRecord), "career");
            var record = payload.ToObject<CareerRecord>(SaveJson.Serializer());
            if (record.seasons == null) throw new InvalidDataException("Career file has no seasons list.");
            foreach (var season in record.seasons)
                if (season == null || string.IsNullOrEmpty(season.sessionId) || season.placement < 0)
                    throw new InvalidDataException("Career file holds an invalid season.");
            return record;
        }

        private void Write(CareerRecord record)
        {
            var token = JObject.FromObject(record, SaveJson.Serializer());
            var envelope = new JObject
            {
                ["format"] = Format,
                ["version"] = 1,
                ["savedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["checksum"] = SaveJson.Hash(SaveJson.Canonical(token)),
                ["record"] = token,
            };
            var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented));
            var temporary = FilePath + ".pending-" + Guid.NewGuid().ToString("N");
            try
            {
                SaveJson.WriteNewDurable(temporary, bytes);
                Parse(File.ReadAllText(temporary, Encoding.UTF8));
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
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

        private string Archive(string why)
        {
            var target = FilePath + "." + why + "-"
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                File.Move(FilePath, target);
                return target;
            }
            catch (Exception error) when (SaveJson.IsExpected(error))
            {
                return null;
            }
        }
    }
}
