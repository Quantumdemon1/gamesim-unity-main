using System;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0 and C0, schema 22): the one boundary
    /// every rule of that plan shares. Under them a study is one of the window's actions (X1), a
    /// pact-mate's word about a threat reaches the player as a line and leaves their view of the
    /// threat alone (X6), a whisper reaches the person the player is talking to (X7), a houseguest's
    /// rumour to the player says what was said and moves only the player's own view of its subject
    /// (X8), talk about the player that the player never heard moves only the listener's view of
    /// them, a promise to vote somebody out is never made to them, and a deal or a promise records
    /// who broke it and when (C0) - so every reader, the eviction vote's included, holds a breach
    /// against whoever broke it (<see cref="Breaches"/>), and the record of it never fades (X11).
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
        ///
        /// <para>Except toward somebody the player is in a pact with: their view of a pact-mate is
        /// theirs to change, and a rumour's weight could take it under the line a pact sours at, so
        /// the pact would end without a word (<see cref="NpcAlliances.Dissolve"/>). The line and the
        /// memory say it was said; the record holds it at nothing, and the view does not move.</para>
        /// </summary>
        internal static void HeardFrom(EpisodeState s, string tellerId, string subjectId, double delta, string said)
        {
            var teller = s.Find(tellerId);
            var subject = s.Find(subjectId);
            if (teller == null || subject == null || subject.isPlayer) return;
            string line = teller.name + " told you " + said + ".";
            bool pactMate = s.Allied(s.playerId, subjectId);
            if (!pactMate) WriteScore(s, s.playerId, subjectId, delta);
            RelationshipLedger.RecordOneWay(s, s.playerId, subjectId, "rumor", pactMate ? 0 : delta, line);
            Remember(s, s.playerId, subjectId, teller.name + " told me in week " + s.week + " that " + said + ".", true);
            Log(s, "information", line, s.playerId);
        }

        /// <summary>
        /// Talk about the player that the player never heard, under the commitment rules: a rumour
        /// about them, or a hunt with them as its threat. The listener's view of the player moves as
        /// the engine's own path moves it - weighed by the listener's social stat, on the listener's
        /// record, into the arc the two of them share - and the player's view of the listener does
        /// not: the player was not in the room. The two-way path also wrote the player's half, with a
        /// roll; this draws none.
        /// </summary>
        internal static void HeardAbout(EpisodeState s, string listenerId, double delta, string note, string type)
        {
            var listener = s.Find(listenerId);
            if (listener == null || listener.isPlayer) return;
            double adjusted = WebRules.RelationshipDelta(delta, listener.stats.social, false);
            string reason = note ?? "Relationship changed by " + adjusted.ToString(CultureInfo.InvariantCulture);
            if (type != null) AddRelationshipEvent(s, listenerId, s.playerId, adjusted, reason, type);
            WriteScore(s, listenerId, s.playerId, adjusted);
            var relation = s.relationships.Single(r => r.fromId == listenerId && r.toId == s.playerId);
            relation.lastInteractionWeek = s.week;
            if (note != null) { relation.notes.Add(note); if (relation.notes.Count > 256) relation.notes.RemoveAt(0); }
            Arc(s, listenerId, adjusted, reason);
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
