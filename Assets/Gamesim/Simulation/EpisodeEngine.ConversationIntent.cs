using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// E2: a game conversation that landed earns two of the speaker's own opinions, not their
        /// ballot, agenda or alliance roster. Selection is private, inside the committed command;
        /// no preview reader exposes the hidden ranking. Existing warmth/gate draws are unchanged.
        /// </summary>
        private static void OpenGameOpinions(EpisodeState s, ContestantState speaker)
        {
            var others = s.Active.Where(c => c.id != s.playerId && c.id != speaker.id).ToList();
            if (others.Count == 0) return;
            var close = others.OrderByDescending(c => s.Score(speaker.id, c.id))
                .ThenBy(c => c.id, StringComparer.Ordinal).First();
            var distant = others.Where(c => c.id != close.id).OrderBy(c => s.Score(speaker.id, c.id))
                .ThenBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
            foreach (var subject in new[] { close, distant }.Where(c => c != null))
            {
                double score = s.Score(speaker.id, subject.id);
                AddStanding(s, speaker.id, subject.id, ClaimSource.Told, score);
                string opinion = score >= 25 ? "feels solid with" : score <= -25 ? "does not trust" : "is still weighing up";
                string line = speaker.name + " opened up about the game: they " + opinion + " " + subject.name + ".";
                Remember(s, s.playerId, speaker.id, line, true);
                Log(s, "information", line, s.playerId, speaker.id);
            }
        }

        /// <summary>
        /// An airing that landed makes each response legible. The event is the witnessed side;
        /// the standing is the resulting attitude, NOT the +/- impact substituted for a score.
        /// It is an overheard reaction, not a deliberate ReadPerson attempt: it consumes neither
        /// that free attempt nor its agenda-disclosure privilege. No NPC/ballot knowledge is copied.
        /// </summary>
        private static void AiringReaction(EpisodeState s, ContestantState guest, double impact)
        {
            double standing = s.Score(guest.id, s.playerId);
            AddStanding(s, guest.id, s.playerId, ClaimSource.Overheard, standing);
            string band = standing >= 25 ? "warm on you" : standing <= -25 ? "cold on you" : "not sure about you";
            bool backed = impact > 0;
            string line = guest.name + (backed ? " backed your airing." : " pushed back against your airing.")
                + " From their reaction, you read them as " + band + ". This is not a promise about the vote.";
            Log(s, backed ? ConversationIntentRules.AiringBacked : ConversationIntentRules.AiringOpposed,
                line, s.playerId, guest.id);
        }
    }
}
