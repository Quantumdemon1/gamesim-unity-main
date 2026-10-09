using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Who the NPC world tries to pair when a scan comes due (BALANCE plan B5a): the director's route
    /// planner's loop, pure, so the house's own runtime and the balance lab's synthetic world pair the
    /// house the same way.
    ///
    /// <para><b>The loop.</b> Every idle houseguest in cast order - active, not the player, and not busy -
    /// tries first the one their agenda points at (<see cref="NpcAgendas.PreferredPartner"/>), then each
    /// idle body after them in cast order, until a reservation holds; a pair that holds is busy for the
    /// rest of the scan. Each try is one call of the caller's reserve, in that order: the director mints
    /// one lease token a call and asks its meeting coordinator; the lab asks its synthetic one.</para>
    ///
    /// <para><b>Pure.</b> It reads the state and changes nothing: no draw, no id, no field. What a held
    /// reservation does is the caller's.</para>
    /// </summary>
    public static class NpcPairing
    {
        /// <summary>The authored pair rendezvous, in their authored order: the ids <see cref="NpcSocialState.IsKnownRendezvous"/> knows.</summary>
        public static readonly IReadOnlyList<string> KnownRendezvous = new[]
        {
            "living-east-chat", "kitchen-west-chat", "bedroom-south-chat", "yard-south-chat", "kitchen-table-chat", "yard-lounger-chat",
        };

        /// <summary>The houseguests the saved world holds back from a new pairing: both of every pending pair, and everybody on a conversation cooldown.</summary>
        public static HashSet<string> Busy(EpisodeState s)
        {
            if (s?.npcSocial == null) throw new ArgumentException("Pairing reads a season with an NPC world.", nameof(s));
            var busy = new HashSet<string>(s.npcSocial.pending.SelectMany(row => new[] { row.firstId, row.secondId }));
            foreach (var cooldown in s.npcSocial.cooldowns.Where(row => row.untilTick > s.npcSocial.clockTick)) busy.Add(cooldown.npcId);
            return busy;
        }

        /// <summary>
        /// One scan's pairings: each try is a call of <paramref name="tryReserve"/> (first, second), in the
        /// director's order; a held one marks both busy. <paramref name="busy"/> is everybody not free to be
        /// paired - <see cref="Busy"/> and whatever the caller holds besides (approaches under way, somebody
        /// on their way to the player). Returns the pairs that held, in order.
        /// </summary>
        public static List<(string first, string second)> Plan(EpisodeState s, IEnumerable<string> busy, Func<string, string, bool> tryReserve)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (tryReserve == null) throw new ArgumentNullException(nameof(tryReserve));
            var held = new List<(string, string)>();
            var unavailable = new HashSet<string>(busy ?? Enumerable.Empty<string>());
            var state = s;
            var idle = state.Active.Where(actor => !actor.isPlayer && !unavailable.Contains(actor.id)).ToArray();
            for (int first = 0; first < idle.Length; first++)
            {
                if (unavailable.Contains(idle[first].id)) continue;
                // Under agency each houseguest tries the one their agenda points at before the next
                // idle body in cast order (NPC-AGENCY-PLAN.md §4); without it, cast order as always.
                var order = new List<int>();
                string wanted = NpcAgendas.PreferredPartner(state, idle[first].id);
                int at = wanted == null ? -1 : Array.FindIndex(idle, actor => actor.id == wanted);
                if (at >= 0 && at != first) order.Add(at);
                for (int second = first + 1; second < idle.Length; second++) if (second != at) order.Add(second);
                foreach (int second in order)
                {
                    if (unavailable.Contains(idle[second].id)) continue;
                    if (!tryReserve(idle[first].id, idle[second].id)) continue;
                    held.Add((idle[first].id, idle[second].id));
                    unavailable.Add(idle[first].id); unavailable.Add(idle[second].id); break;
                }
            }
            return held;
        }
    }
}
