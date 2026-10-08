using System;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        // View authority, never saved. All three card surfaces share one answer generation.
        private object replyCardView;
        private object BeginReplyChoices() => replyCardView = new object();

        private Action ReplyChoice(EpisodeState state, string cardId, string key, object view)
        {
            long generation = loadGeneration;
            return () =>
            {
                if (this == null || !isActiveAndEnabled || !IsPanelOpen || challengeActive
                    || !ReferenceEquals(replyCardView, view) || generation != loadGeneration
                    || !IsCurrentDiaryRevision(state) || ReplyCards.Pending(projected)?.id != cardId) return;
                // Consume before the durable command. A refused write needs a new deliberate press
                // on the newly rendered view, not a retry from a retained old callback.
                replyCardView = null;
                Commit(state, EpisodeCommandKind.ReplyToHouseguest, cardId, text: key);
            };
        }
    }
}
