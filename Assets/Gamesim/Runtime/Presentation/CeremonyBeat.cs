namespace Gamesim.Presentation
{
    /// <summary>The beats a ceremony card reaches as it plays, in the order a broadcast reaches them.</summary>
    public enum CeremonyBeatKind
    {
        /// <summary>The card is up: its title and the line before the first name.</summary>
        Opened,
        /// <summary>A key handed out: <see cref="CeremonyBeat.Index"/> is which, <see cref="CeremonyBeat.SubjectId"/> who is safe.</summary>
        KeyShown,
        /// <summary>The beat before the last key, when everybody still waiting is waiting on one name.</summary>
        LastKeyPending,
        /// <summary>The block: the keys are out and the nominees are on the screen.</summary>
        BlockShown,
        /// <summary>A vote on the board: <see cref="CeremonyBeat.Index"/> is which, <see cref="CeremonyBeat.SubjectId"/> who it went against.</summary>
        VoteShown,
        /// <summary>The beat before the house's last vote.</summary>
        LastVotePending,
        /// <summary>A tie announced, before the Head of Household breaks it.</summary>
        TieCalled,
        /// <summary>The Head of Household's deciding vote on the board.</summary>
        TieBroken,
        /// <summary>The result read: <see cref="CeremonyBeat.SubjectId"/> is who is leaving, or who won.</summary>
        ResultShown,
        /// <summary>The card is down, on its own clock or skipped.</summary>
        Closed,
    }

    /// <summary>
    /// One beat of a ceremony card, raised by the card as it reaches it, so a stage in the house can
    /// cut its camera and act the beat out on the bodies in time with the screen. The card stays the
    /// authority on the order and the timing; a beat is a report, never a request.
    /// </summary>
    public readonly struct CeremonyBeat
    {
        public readonly CeremonyBeatKind Kind;
        /// <summary>Which key or vote, counting from zero; -1 for a beat that is not one of a series.</summary>
        public readonly int Index;
        /// <summary>The houseguest the beat is about, or null.</summary>
        public readonly string SubjectId;
        /// <summary>Whether the beat was reached by a skip, all at once, rather than on the card's clock.</summary>
        public readonly bool Skipped;

        public CeremonyBeat(CeremonyBeatKind kind, int index = -1, string subjectId = null, bool skipped = false)
        {
            Kind = kind; Index = index; SubjectId = subjectId; Skipped = skipped;
        }

        public override string ToString() => Kind + (Index >= 0 ? " " + (Index + 1) : "") + (SubjectId != null ? " (" + SubjectId + ")" : "") + (Skipped ? " skipped" : "");
    }
}
