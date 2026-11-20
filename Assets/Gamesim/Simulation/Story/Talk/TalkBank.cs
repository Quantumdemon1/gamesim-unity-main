using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The talks the engine reads: the shipped tables (<see cref="TalkTables.All"/>), or, inside a test's <see cref="Use"/> scope on
    /// the same thread, the test's own table. Text and gates only (ruling R1): nothing the engine gains, reveals or costs comes from
    /// a row beyond its kind, layer, gates and reply tones.
    ///
    /// <para>The override is thread-static and null outside a scope, so it never outlives a test, never leaks into a play session
    /// (statics outlive play sessions only where they are set) and never races a test on another thread.</para>
    /// </summary>
    public static class TalkBank
    {
        [ThreadStatic] private static IReadOnlyList<TalkRow> scoped;

        /// <summary>The talks in force: a test's scope on this thread, else the shipped tables.</summary>
        public static IReadOnlyList<TalkRow> Rows => scoped ?? TalkTables.All;

        /// <summary>The talk with this id, or null (a spent id the tables no longer hold is ignored).</summary>
        public static TalkRow Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var rows = Rows;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null && string.Equals(rows[i].Id, id, StringComparison.Ordinal)) return rows[i];
            return null;
        }

        /// <summary>
        /// For tests only: the engine reads <paramref name="rows"/> instead of the shipped tables on this thread until the scope is
        /// disposed. Scopes nest; disposing restores the one before.
        /// </summary>
        public static IDisposable Use(IEnumerable<TalkRow> rows) => new Scope(rows);

        private sealed class Scope : IDisposable
        {
            private readonly IReadOnlyList<TalkRow> before;
            private bool disposed;

            public Scope(IEnumerable<TalkRow> rows)
            {
                before = scoped;
                scoped = (rows ?? Enumerable.Empty<TalkRow>()).ToArray();
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                scoped = before;
            }
        }
    }
}
