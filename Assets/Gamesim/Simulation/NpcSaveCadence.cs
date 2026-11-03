namespace Gamesim.Simulation
{
    /// <summary>
    /// When the house's own operations reach disk (LIVE-SAVE-PLAN.md). The director installs each clock second,
    /// conversation start and cancellation in memory as it comes, and writes them together: once
    /// <see cref="Seconds"/> of free time have passed since the first one not yet saved, and before any decision,
    /// panel, pause, slot change or quit. This only counts and remembers; it never saves, and no engine code calls
    /// it, so no season digest moves. One a director, never static: statics outlive play sessions.
    /// </summary>
    public sealed class NpcSaveCadence
    {
        /// <summary>
        /// The owner's call, 2026-11-01: the most of the house's own life a crash, a kill or a power cut may cost -
        /// or a save held by another program past the store's wait, or refused on what it holds, both of which ask
        /// for a reload (LIVE-SAVE-PLAN.md section 4). Never a decision: each decision's write carries everything
        /// the house installed before it.
        /// </summary>
        public const double Seconds = 30;

        private EpisodeState onDisk;

        /// <summary>The operations installed since the last save.</summary>
        public int Unsaved { get; private set; }

        /// <summary>The free time since the first of them.</summary>
        public double UnsavedSeconds { get; private set; }

        public bool Clean => Unsaved == 0;

        /// <summary>The oldest unsaved operation is <see cref="Seconds"/> of free time old.</summary>
        public bool Due => Unsaved > 0 && UnsavedSeconds >= Seconds;

        /// <summary>
        /// The state on disk as a window opens, given before its first operation is counted. Taken while the
        /// cadence is clean and ignored once a window is open, so a window is judged against the state it began on.
        /// </summary>
        public void Begin(EpisodeState disk)
        {
            if (Clean) onDisk = disk;
        }

        /// <summary>One more operation installed and not yet saved.</summary>
        public void Installed() => Unsaved = checked(Unsaved + 1);

        /// <summary>Free time passing: it ages the window only while something is unsaved.</summary>
        public void Elapsed(double seconds)
        {
            if (Unsaved > 0 && seconds > 0 && !double.IsInfinity(seconds)) UnsavedSeconds += seconds;
        }

        /// <summary>Everything installed is on disk: <paramref name="disk"/> is what the store now holds.</summary>
        public void Saved(EpisodeState disk)
        {
            Unsaved = 0; UnsavedSeconds = 0; onDisk = disk;
        }

        /// <summary>A load, a recovery or a new slot: the installed state is the disk's, and nothing is known beyond it.</summary>
        public void Reset()
        {
            Unsaved = 0; UnsavedSeconds = 0; onDisk = null;
        }

        /// <summary>
        /// What the disk should hold: <paramref name="current"/> while nothing is owed, otherwise the state the
        /// window began on. A failed write is judged against this, so a session ahead of an untouched disk is a
        /// plain failure and not a different save.
        /// </summary>
        public EpisodeState KnownOnDisk(EpisodeState current) => Clean || onDisk == null ? current : onDisk;
    }
}
