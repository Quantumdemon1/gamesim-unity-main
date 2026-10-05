using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// E3's question about a particular nominee. This reader returns public identities only,
    /// never their relationships or a projected ballot. The answer is earned by AskForIntel.
    /// The fresh-season Wave C boundary keeps previously ignored payloads in old saves ignored.
    /// </summary>
    public static class NomineeIntel
    {
        public static List<string> Targets(EpisodeState s, string listenerId)
        {
            var result = new List<string>();
            if (!EpisodeEngine.EconomyRulesOn(s) || s.Find(s.playerId)?.status != ContestantStatus.Active
                || s.Find(listenerId)?.status != ContestantStatus.Active || listenerId == s.playerId
                || s.nominees == null) return result;
            // A previous week's nominees are not a current block during free time or nominations.
            if (s.phase != EpisodePhase.VetoSelection && s.phase != EpisodePhase.Veto
                && s.phase != EpisodePhase.VetoMeeting && s.phase != EpisodePhase.Campaign) return result;
            foreach (string id in s.nominees)
                if (id != listenerId && id != s.playerId && s.Find(id)?.status == ContestantStatus.Active
                    && !result.Contains(id)) result.Add(id);
            return result;
        }
    }
}
