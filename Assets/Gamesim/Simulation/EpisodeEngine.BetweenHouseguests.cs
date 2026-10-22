namespace Gamesim.Simulation
{
    /// <summary>
    /// The commands that move two houseguests' standing with each other: a veto used and its replacement named, a lie
    /// told, a scheme. Neither houseguest's standing with the other is the player's own relationship, and under D2's
    /// all-week rules none of them writes the player's arcs (the arcs review's finding 8; the house's own conversations
    /// were settled the same way after the balance review's finding 3).
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// The ledger's types for a veto's moves between two houseguests under D2's rules (<see cref="ChangeBetween"/>): the
        /// one saved and the holder who saved them, the replacement and the Head of Household who named them, and the
        /// replacement and the holder whose veto put them up. All three fade (<see cref="RelationshipLedger.Decays"/>) and
        /// nothing reads them by name, so a veto between two houseguests counts for their trust and nothing more. The engine's
        /// own path recorded nothing for a veto, so before the rules no season holds one (EpisodeValidation's Wave D check).
        /// </summary>
        public const string VetoSaveEvent = "veto-save", VetoReplacementEvent = "veto-replacement", VetoForcedEvent = "veto-forced";

        /// <summary>
        /// Whether a ledger record is of a type only D2's all-week rules write: the house's talk (<see cref="NpcConversationEvent"/>,
        /// <see cref="NpcGossipEvent"/>) and a veto's moves between two houseguests. A season without the rules holds none,
        /// and none is dated before them (EpisodeValidation's Wave D check).
        /// </summary>
        public static bool AllWeekRecord(string type) =>
            type == NpcConversationEvent || type == NpcGossipEvent || type == VetoSaveEvent || type == VetoReplacementEvent || type == VetoForcedEvent;

        /// <summary>
        /// A change one of the player's commands makes between two of the house.
        ///
        /// <para><b>Before D2's rules</b> (<see cref="AllWeekOn"/> false: every season saved before them) the engine's own
        /// path, <see cref="Change"/>, exactly as it always ran. That path also writes <c>relationshipArcs</c> for both
        /// houseguests, and an arc is the player's own: it is keyed by one houseguest, reads "your rivalry with" them, and
        /// feeds how they vote on the player (<see cref="WebEvictionVoting"/>) and the threat ranking
        /// (<see cref="ThreatAssessment"/>). So in those seasons a veto two houseguests used on each other, or a lie told one
        /// about another, moved how both of them voted on the player.</para>
        ///
        /// <para><b>Under them</b> the pair moves as <see cref="NpcSocialActions"/> and a completed conversation
        /// (<see cref="CompletionChange"/>) move one: <see cref="RelationshipLedger.Move"/>, by the act's own amount both
        /// ways (not weighed by the first's social stat, no rolled reciprocal), and <see cref="RelationshipLedger.Record"/>,
        /// both ways, under <paramref name="eventType"/> where the engine's path had one (a lie, a scheme) or
        /// <paramref name="recordType"/> where it had none (a veto's three moves); no arc. A change with the player as one of
        /// the two keeps the engine's path and its arc, as before; a houseguest's change with themselves (a holder saving
        /// themselves) is nothing on either path.</para>
        ///
        /// <para><b>The season's stream</b> advances exactly as the engine's path advanced it: that path rolls a reciprocal,
        /// and a second for the reverse record when it has a type, and the ledger's move rolls nothing, so those draws are
        /// drawn and set aside and every later draw is the one it would have been. <b>Its sequence ids</b>: a lie's and a
        /// scheme's records take the two ids the engine's two records took; a veto's take two for each move between two
        /// houseguests, where the engine's path took none, so what is keyed to <c>nextSequence</c> after it - house events,
        /// deals and promises, the talk story hooks - lands differently in such a season, as it may under a new rule. These
        /// are commands' records, never free roam's: <see cref="HouseTalkRecords"/> and the autonomy QA, which admit no
        /// command, never see them. The relationship's free-text notes, which only the player's own edges are read for, are
        /// not written.</para>
        /// </summary>
        private static void ChangeBetween(EpisodeState s, string from, string to, double delta, string note, string eventType, string recordType = null)
        {
            if (!AllWeekOn(s) || from == s.playerId || to == s.playerId)
            {
                Change(s, from, to, delta, note, eventType);
                return;
            }
            if (from == to) return;
            RelationshipLedger.Move(s, from, to, delta);
            RelationshipLedger.Record(s, from, to, eventType ?? recordType, delta, note);
            Roll(s);
            if (eventType != null) Roll(s);
        }
    }
}
