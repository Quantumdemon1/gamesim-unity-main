namespace Gamesim.Simulation
{
    /// <summary>
    /// Your word in the house (ACTIONS-DEALS-ALLIANCES-PLAN C8): the engine's half. A deal the player
    /// breaks by an act the house watches is written as a fact the two of them know
    /// (<see cref="Knowledge.BrokenWord"/>, from the settlement's spread, <c>SpreadBetrayal</c>), and the
    /// house's own gossip carries it at the story's anchors (<see cref="Knowledge.Spread"/>). Here is
    /// what one hearing does. <see cref="YourWord"/> is the reading built from those facts.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// A houseguest the gossip has told of the player's broken word: they think less of the player
        /// for it - <see cref="YourWord.HeardImpact"/>, on their view of the player and their record of them
        /// (<see cref="YourWord.HeardType"/>, the house's own for hearing of a betrayal, one way and fading
        /// as it always did), into the arc the two of them share, with no roll, as talk about the player
        /// moves a listener (<see cref="HeardAbout"/>) - and the player hears it: a whispered line saying
        /// who heard what (<see cref="YourWord.HeardLine"/>), to them alone. Never a silent change: every
        /// houseguest who knows of it beyond the two it was struck between came by it this way, and was
        /// named to the player as they did.
        /// </summary>
        private static void HeardOfYourWord(EpisodeState s, HouseFactState fact, string listenerId)
        {
            var listener = s.Find(listenerId);
            if (listener == null || listener.isPlayer) return;
            if (UnifiedCommitmentHearings.WritesOn(s) && UnifiedCommitmentHearings.CanonicalLeaf(s, fact))
            {
                var staged = UnifiedCommitmentHearings.PrepareHearing(s, fact, listenerId, out bool added);
                if (added)
                {
                    HeardOfYourWordCore(staged, fact, listenerId);
                    // Exactly the existing HeardAbout/Log surfaces; no unrelated state is replaced.
                    s.relationships = staged.relationships;
                    s.relationshipArcs = staged.relationshipArcs;
                    s.events = staged.events;
                    s.nextSequence = staged.nextSequence;
                }
                UnifiedCommitmentHearings.Install(s, staged);
                return;
            }
            HeardOfYourWordCore(s, fact, listenerId);
        }

        private static void HeardOfYourWordCore(EpisodeState s, HouseFactState fact, string listenerId)
        {
            // The record the house's spread always wrote, in its own words.
            string heard = "Heard that " + Name(s, fact.actorId) + " broke a deal with " + Name(s, fact.subjectId) + ".";
            HeardAbout(s, listenerId, YourWord.HeardImpact, heard, YourWord.HeardType);
            Log(s, StoryLog.Whisper, YourWord.HeardLine(s, fact, listenerId), s.playerId);
        }
    }
}
