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
            // Slice S3 (talk rules 3): the romance talks, the class pools' and each All-Star template's own.
            TalkTableSparkRomance.Rows, TalkTableSteadyRomance.Rows, TalkTableGuardedRomance.Rows,
            TalkTableStarDanGheesling.Rows, TalkTableStarDrWillKirby.Rows, TalkTableStarDerrickLevasseur.Rows,
            TalkTableStarJanellePierzina.Rows, TalkTableStarCodyCalafiore.Rows, TalkTableStarDanielleReyes.Rows,
            TalkTableStarVanessaRousso.Rows, TalkTableStarTylerCrispen.Rows, TalkTableStarChelsieBaham.Rows,
            TalkTableStarRachelReilly.Rows, TalkTableStarXavierPrather.Rows, TalkTableStarJunSong.Rows,
            // Slice S5 (talk rules 5): the class pools' week-event talks, every row written for level 5 (TalkRow.FromRules).
            TalkTableEventsSpark.Rows, TalkTableEventsSteady.Rows, TalkTableEventsGuarded.Rows,
            // Slice S5 (talk rules 5): each regular's own talks (spec 5.4's person tables), every row written for level 5.
            TalkTableAlexChen.Rows, TalkTableEmmaBrown.Rows, TalkTableJordanTaylor.Rows, TalkTableCaseyWilson.Rows,
            TalkTableRileyJohnson.Rows, TalkTableJamieRoberts.Rows, TalkTableQuinnMartinez.Rows, TalkTableAveryThompson.Rows,
            TalkTableTaylorKim.Rows, TalkTableSamWilliams.Rows, TalkTableBlakePeterson.Rows, TalkTableMayaHassan.Rows,
        };

        /// <summary>
        /// The regulars' own talks by template id (slice S5, talk rules 5): every Regular Season template has one, and no All-Star
        /// does. Each row's <see cref="TalkRow.Owner"/> is its key and its <see cref="TalkRow.FromRules"/> is talk rules 5.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, TalkRow[]> PersonTables = new Dictionary<string, TalkRow[]>
        {
            { TalkTableAlexChen.Owner, TalkTableAlexChen.Rows },
            { TalkTableEmmaBrown.Owner, TalkTableEmmaBrown.Rows },
            { TalkTableJordanTaylor.Owner, TalkTableJordanTaylor.Rows },
            { TalkTableCaseyWilson.Owner, TalkTableCaseyWilson.Rows },
            { TalkTableRileyJohnson.Owner, TalkTableRileyJohnson.Rows },
            { TalkTableJamieRoberts.Owner, TalkTableJamieRoberts.Rows },
            { TalkTableQuinnMartinez.Owner, TalkTableQuinnMartinez.Rows },
            { TalkTableAveryThompson.Owner, TalkTableAveryThompson.Rows },
            { TalkTableTaylorKim.Owner, TalkTableTaylorKim.Rows },
            { TalkTableSamWilliams.Owner, TalkTableSamWilliams.Rows },
            { TalkTableBlakePeterson.Owner, TalkTableBlakePeterson.Rows },
            { TalkTableMayaHassan.Owner, TalkTableMayaHassan.Rows },
        };

        /// <summary>The All-Star templates' own romance tables by template id (slice S3): every All-Stars template has one.</summary>
        public static readonly IReadOnlyDictionary<string, TalkRow[]> StarTables = new Dictionary<string, TalkRow[]>
        {
            { TalkTableStarDanGheesling.Owner, TalkTableStarDanGheesling.Rows },
            { TalkTableStarDrWillKirby.Owner, TalkTableStarDrWillKirby.Rows },
            { TalkTableStarDerrickLevasseur.Owner, TalkTableStarDerrickLevasseur.Rows },
            { TalkTableStarJanellePierzina.Owner, TalkTableStarJanellePierzina.Rows },
            { TalkTableStarCodyCalafiore.Owner, TalkTableStarCodyCalafiore.Rows },
            { TalkTableStarDanielleReyes.Owner, TalkTableStarDanielleReyes.Rows },
            { TalkTableStarVanessaRousso.Owner, TalkTableStarVanessaRousso.Rows },
            { TalkTableStarTylerCrispen.Owner, TalkTableStarTylerCrispen.Rows },
            { TalkTableStarChelsieBaham.Owner, TalkTableStarChelsieBaham.Rows },
            { TalkTableStarRachelReilly.Owner, TalkTableStarRachelReilly.Rows },
            { TalkTableStarXavierPrather.Owner, TalkTableStarXavierPrather.Rows },
            { TalkTableStarJunSong.Owner, TalkTableStarJunSong.Rows },
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
