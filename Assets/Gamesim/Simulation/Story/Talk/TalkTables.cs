using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Every free talk the game ships (ruling R1: the talks are C# tables, in <c>Story/Talk/Tables</c>). The tables builder owns this
    /// file and the Tables folder: each pool's table is a static array of <see cref="TalkRow"/>, and <see cref="All"/> joins them.
    /// The engine reads the talks through <see cref="TalkBank.Rows"/>, never this list directly, so a test can hold a table of its own.
    ///
    /// <para>Empty until the tables land: with no talk, nothing is ever on offer and the command is refused.</para>
    /// </summary>
    public static class TalkTables
    {
        /// <summary>Every talk, in table order. Read only.</summary>
        public static readonly IReadOnlyList<TalkRow> All = Array.Empty<TalkRow>();
    }
}
