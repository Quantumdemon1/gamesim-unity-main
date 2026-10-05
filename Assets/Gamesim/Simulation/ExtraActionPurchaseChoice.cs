using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Transient, read-only choice for the existing targeted BuyActionPoint command. It is not a
    /// saved rule or a substitute command authority. Opening/canceling it draws no roll and spends
    /// nothing. The engine retains its source-backed random fallback for other callers; the picker
    /// accepts only a named, still-active person from the revision the player actually saw.
    /// </summary>
    public sealed class ExtraActionPurchaseChoice
    {
        private readonly string session, player;
        private readonly int revision;
        private readonly EpisodePhase phase;
        public IReadOnlyList<string> Targets { get; }

        private ExtraActionPurchaseChoice(EpisodeState state)
        {
            session = state.sessionId;
            player = state.playerId;
            revision = state.revision;
            phase = state.phase;
            Targets = Array.AsReadOnly(state.Active.Where(c => c.id != player).Select(c => c.id).ToArray());
        }

        public static bool Available(EpisodeState state) => state != null
            && (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign)
            && state.Find(state.playerId)?.status == ContestantStatus.Active
            && state.boughtActionPoints < WebSocialVocabulary.PurchaseCeiling
            && state.Active.Any(c => c.id != state.playerId);

        public static ExtraActionPurchaseChoice Open(EpisodeState state) =>
            Available(state) ? new ExtraActionPurchaseChoice(state) : null;

        public bool IsCurrent(EpisodeState state) => Available(state)
            && state.sessionId == session && state.playerId == player
            && state.revision == revision && state.phase == phase;

        public bool CanChoose(EpisodeState state, string target) => IsCurrent(state)
            && target != null && target != player && Targets.Contains(target)
            && state.Find(target)?.status == ContestantStatus.Active;
    }
}
