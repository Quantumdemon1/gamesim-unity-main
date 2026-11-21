using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Every free talk the game ships (ruling R1: the talks are C# tables, in <c>Story/Talk/Tables</c>). The tables builder owns this
    /// file and the Tables folder: each pool's table is a static array of <see cref="TalkRow"/>, and <see cref="All"/> joins them.
    /// The engine reads the talks through <see cref="TalkBank.Rows"/>, never this list directly, so a test can hold a table of its own.
    ///
    /// <para>Slice 1: the three class pools (spark, steady, guarded) and the All-Stars' game-register star pool, each as layer
    /// frames, house-life rhythm and the weekly standing talk. An All-Star's talks come only from the star pool (ruling R7).</para>
    /// </summary>
    public static class TalkTables
    {
        /// <summary>The tables in the order <see cref="All"/> joins them: per pool, layers, rhythm, standing. Read only.</summary>
        public static readonly IReadOnlyList<TalkRow[]> Tables = new[]
        {
            TalkTableSparkLayers.Rows, TalkTableSparkRhythm.Rows, TalkTableSparkStanding.Rows,
            TalkTableSteadyLayers.Rows, TalkTableSteadyRhythm.Rows, TalkTableSteadyStanding.Rows,
            TalkTableGuardedLayers.Rows, TalkTableGuardedRhythm.Rows, TalkTableGuardedStanding.Rows,
            TalkTableStarLayers.Rows, TalkTableStarRhythm.Rows, TalkTableStarStanding.Rows,
            TalkTableSparkStages.Rows, TalkTableSteadyStages.Rows, TalkTableGuardedStages.Rows, TalkTableStarStages.Rows,
        };

        /// <summary>Every talk, in table order. Read only.</summary>
        public static readonly IReadOnlyList<TalkRow> All = Join(Tables);

        static TalkRow[] Join(IReadOnlyList<TalkRow[]> tables)
        {
            var rows = new List<TalkRow>();
            foreach (var table in tables) rows.AddRange(table);
            return rows.ToArray();
        }
    }
}
