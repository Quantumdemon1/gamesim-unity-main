using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>The words for what a houseguest is trying to do this week (NPC-AGENCY-PLAN.md §4).</summary>
    public static class Agendas
    {
        public const string Survive = "survive", Reign = "reign", Court = "court", Hunt = "hunt", Build = "build", Hold = "hold", Drift = "drift";
        public static readonly string[] All = { Survive, Reign, Court, Hunt, Build, Hold, Drift };
    }

    /// <summary>One houseguest's agenda: what, with whom, and for a hunt against whom.</summary>
    public sealed class NpcAgenda
    {
        public string kind, partnerId, targetId;
    }

    /// <summary>
    /// What each houseguest is trying to do this week, read from the state and never stored: on the
    /// block they fight to stay; the Head of Household listens to their closest ally; somebody with
    /// no claim on the Head of Household courts them while the power is live; a pact with a common
    /// threat hunts it; a houseguest with no pact looks for the partner they most want; a pact-mate
    /// holds tight; and everybody else drifts, which is the weighted draw the house always made.
    /// Null without agency, so every consumer reduces to what it was.
    /// </summary>
    public static class NpcAgendas
    {
        public static NpcAgenda Of(EpisodeState s, string npcId)
        {
            var npc = s?.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active || !EpisodeEngine.AgencyOn(s)) return null;
            bool blockOpen = !s.evictionResolved && s.nominees.Count > 0;
            // The power is live from the crowning to the veto meeting; by the campaign it is spent.
            bool powerLive = !string.IsNullOrEmpty(s.hohId) && s.Find(s.hohId)?.status == ContestantStatus.Active
                && (s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.VetoSelection || s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting);

            if (blockOpen && s.nominees.Contains(npcId))
            {
                string voter = EpisodeEngine.PersuadableVoters(s, npcId).FirstOrDefault()?.id
                    ?? Closest(s, npcId, EpisodeEngine.Voters(s).Select(v => v.id));
                return new NpcAgenda { kind = Agendas.Survive, partnerId = voter };
            }
            if (powerLive && s.hohId == npcId)
                return new NpcAgenda { kind = Agendas.Reign, partnerId = Closest(s, npcId, PactMates(s, npcId)) ?? Closest(s, npcId, Others(s, npcId)) };
            if (powerLive && !s.Allied(npcId, s.hohId)
                && !NpcDeals.Between(s, npcId, s.hohId).Any(d => d.type == DealKind.SafetyAgreement))
                return new NpcAgenda { kind = Agendas.Court, partnerId = s.hohId };

            var mates = PactMates(s, npcId).ToList();
            if (mates.Count > 0)
            {
                foreach (var mate in mates.OrderByDescending(m => s.Score(npcId, m)).ThenBy(m => m, StringComparer.Ordinal))
                {
                    string threat = NpcDeals.CommonThreat(s, npcId, mate);
                    if (threat != null) return new NpcAgenda { kind = Agendas.Hunt, partnerId = mate, targetId = threat };
                }
                return new NpcAgenda { kind = Agendas.Hold, partnerId = Closest(s, npcId, mates) };
            }

            string prospect = Others(s, npcId)
                .Where(other => s.Score(npcId, other) >= 0)
                .Select(other => (id: other, desire: NpcAlliances.Desire(s, npcId, other)))
                .Where(x => x.desire > 0)
                .OrderByDescending(x => x.desire).ThenBy(x => x.id, StringComparer.Ordinal)
                .Select(x => x.id).FirstOrDefault();
            return prospect != null ? new NpcAgenda { kind = Agendas.Build, partnerId = prospect } : new NpcAgenda { kind = Agendas.Drift };
        }

        /// <summary>
        /// Whether the agenda still has work in it. A houseguest building toward somebody already
        /// warm enough for a pact (<see cref="NpcAlliances.MinimumRelationship"/>) has built it; the
        /// pact pass takes it from there when the house has room, and until then the weeks are
        /// spent as they always were rather than piling warmth on one person all season.
        /// </summary>
        public static bool StillWorking(EpisodeState s, string npcId, NpcAgenda agenda)
        {
            if (agenda == null) return false;
            if (agenda.kind == Agendas.Build)
                return agenda.partnerId != null && s.Score(npcId, agenda.partnerId) < NpcAlliances.MinimumRelationship;
            return agenda.partnerId != null;
        }

        /// <summary>The houseguest this one's agenda points at, for the NPC world's pairing: never the player, who is not a body the world pairs, and nobody once the agenda's work is done.</summary>
        public static string PreferredPartner(EpisodeState s, string npcId)
        {
            var agenda = Of(s, npcId);
            return agenda?.partnerId == null || agenda.partnerId == s.playerId || !StillWorking(s, npcId, agenda) ? null : agenda.partnerId;
        }

        /// <summary>The agenda in the read's words, or null.</summary>
        public static string Describe(EpisodeState s, string npcId, NpcAgenda agenda)
        {
            var npc = s?.Find(npcId);
            if (npc == null || agenda == null) return null;
            string Who(string id) => id == s.playerId ? "you" : s.Find(id)?.name ?? "somebody";
            switch (agenda.kind)
            {
                case Agendas.Survive: return npc.name + " is fighting to stay" + (agenda.partnerId != null ? ", working on " + Who(agenda.partnerId) : "");
                case Agendas.Reign: return npc.name + " holds the power" + (agenda.partnerId != null ? " and is listening to " + Who(agenda.partnerId) : "");
                case Agendas.Court: return npc.name + " is courting " + Who(agenda.partnerId);
                case Agendas.Hunt: return npc.name + " wants " + Who(agenda.targetId) + " out, with " + Who(agenda.partnerId);
                case Agendas.Build: return npc.name + " is looking for a partner, and it's " + Who(agenda.partnerId);
                case Agendas.Hold: return npc.name + " is holding tight with " + Who(agenda.partnerId);
                default: return npc.name + " is keeping their head down";
            }
        }

        /// <summary>Everyone active this houseguest shares an active pact with, the player included.</summary>
        public static IEnumerable<string> PactMates(EpisodeState s, string npcId) =>
            s.alliances.Where(a => a.active && a.members.Contains(npcId)).SelectMany(a => a.members)
                .Where(m => m != npcId && s.Find(m)?.status == ContestantStatus.Active).Distinct();

        private static IEnumerable<string> Others(EpisodeState s, string npcId) => s.Active.Where(c => c.id != npcId).Select(c => c.id);

        private static string Closest(EpisodeState s, string npcId, IEnumerable<string> ids) =>
            ids.Where(id => id != npcId).OrderByDescending(id => s.Score(npcId, id)).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
    }
}
