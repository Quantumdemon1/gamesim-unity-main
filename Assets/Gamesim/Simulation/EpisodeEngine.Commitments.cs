using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0 and C0, schema 22): the one boundary
    /// every rule of that plan shares. Under them a study is one of the window's actions (X1), a
    /// pact-mate's word about a threat reaches the player as a line and leaves their view of the
    /// threat alone (X6), a whisper reaches the person the player is talking to (X7), a houseguest's
    /// rumour to the player says what was said and moves only the player's own view of its subject
    /// (X8), a promise to vote somebody out is never made to them, and a deal or a promise records who
    /// broke it and when (C0) - so every reader holds a breach against whoever broke it
    /// (<see cref="Breaches"/>), and the record of it never fades (X11).
    ///
    /// <para>Keyed to <see cref="EpisodeState.commitmentRulesStartWeek"/>: before it a season plays
    /// exactly as it did, roll for roll and line for line. The director starts every season under
    /// them, the importer from the week after the import; a season saved before they existed keeps
    /// playing without them to its end, and so does every season a test builds directly.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Switches the commitment rules on from a week, no later than the week after the season's own.</summary>
        public static void EnableCommitments(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.commitmentRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>Whether the season plays under the commitment rules this week.</summary>
        public static bool CommitmentRulesOn(EpisodeState s) =>
            s != null && s.commitmentRulesStartWeek >= 1 && s.week >= s.commitmentRulesStartWeek;

        /// <summary>
        /// What a houseguest told the player about somebody, under the commitment rules (X8): the
        /// line says what was said, the player remembers who said it, and only the player's own view
        /// of the one it was about moves - by the rumour's weight, one way, on the player's own
        /// record. Nothing moves in the view of the one it was about, who heard nothing. No roll.
        /// </summary>
        internal static void HeardFrom(EpisodeState s, string tellerId, string subjectId, double delta, string said)
        {
            var teller = s.Find(tellerId);
            var subject = s.Find(subjectId);
            if (teller == null || subject == null || subject.isPlayer) return;
            string line = teller.name + " told you " + said + ".";
            WriteScore(s, s.playerId, subjectId, delta);
            RelationshipLedger.RecordOneWay(s, s.playerId, subjectId, "rumor", delta, line);
            Remember(s, s.playerId, subjectId, teller.name + " told me in week " + s.week + " that " + said + ".", true);
            Log(s, "information", line, s.playerId);
        }

        /// <summary>
        /// A deal broken under the commitment rules, on the record (C0, X11): the one wronged holds it
        /// against the one who broke it, permanently, at the deal's weight; the one who broke it keeps
        /// a record that it happened, weighing nothing, so their own record still says what they did
        /// (<see cref="YourWeek"/> reads the player's) without the victim reading as untrusted. The
        /// same two entries, in the same order, as the two-way record the season used before.
        /// </summary>
        private static void RecordBreach(EpisodeState s, string wrongedId, string breakerId, double delta, string text)
        {
            RelationshipLedger.RecordOneWay(s, wrongedId, breakerId, "deal_broken", delta, text, permanent: true);
            RelationshipLedger.RecordOneWay(s, breakerId, wrongedId, "deal_broken", 0, text, permanent: true);
        }
    }
}
