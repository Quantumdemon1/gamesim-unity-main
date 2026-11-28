using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The week's events a free talk can be about (spec 5.3, a row's <c>event=&lt;kind&gt;</c> gate, <see cref="TalkGate.Event"/>;
    /// slice S5, talk rules 5). Each is read for one housemate from the season's committed state as it stands: nothing is saved
    /// for them, nothing is drawn from any stream, and below talk rules 5 none holds, so every earlier level reads nothing here.
    ///
    /// <para>The week's fields hold from the Head of Household's win until the week turns, which is after the after-eviction window
    /// (<see cref="EpisodeEngine"/>'s Advance clears them as the next week starts), so an event holds in each of the week's windows
    /// its field is set in: the Head of Household from the after-HoH window, the nominees from the after-nominations window (the
    /// veto meeting's replacement in), the veto holder once the veto is played, this week's evictee in the after-eviction window.</para>
    /// </summary>
    public static class TalkEvents
    {
        /// <summary>
        /// The kinds (spec 5.3): the player is this week's Head of Household; the player is on the block; the housemate is on the
        /// block; the housemate is this week's Head of Household; the housemate holds this week's veto; this week's evictee was in a
        /// pact with the housemate; the player's couple is known to the house and the housemate is not the other half of it.
        /// </summary>
        public const string PlayerHoH = "player-hoh", PlayerNominated = "player-nominated", NpcNominated = "npc-nominated",
            NpcHoH = "npc-hoh", NpcVeto = "npc-veto", AllyEvicted = "ally-evicted", CoupleNoticed = "couple-noticed";

        public static readonly string[] All = { PlayerHoH, PlayerNominated, NpcNominated, NpcHoH, NpcVeto, AllyEvicted, CoupleNoticed };

        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;

        /// <summary>Whether the season plays the event talks: talk rules 5 (<see cref="TalkRules.ComesToTalk"/>) or later.</summary>
        public static bool On(EpisodeState s) => TalkRules.At(s, TalkRules.ComesToTalk);

        /// <summary>
        /// Whether an event holds this week for a talk with a housemate. False below talk rules 5, for an unknown kind, and for
        /// anyone who is not a housemate still in the house.
        /// </summary>
        public static bool Holds(EpisodeState s, string npcId, string kind)
        {
            if (!On(s) || string.IsNullOrEmpty(npcId)) return false;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active) return false;
            switch (kind)
            {
                case PlayerHoH: return !string.IsNullOrEmpty(s.playerId) && s.hohId == s.playerId;
                case NpcHoH: return s.hohId == npcId;
                case PlayerNominated: return s.nominees != null && !string.IsNullOrEmpty(s.playerId) && s.nominees.Contains(s.playerId);
                case NpcNominated: return s.nominees != null && s.nominees.Contains(npcId);
                case NpcVeto: return s.vetoHolderId == npcId;
                case AllyEvicted: return LostAnAlly(s, npcId);
                case CoupleNoticed: return CoupleKnown(s, npcId);
                default: return false;
            }
        }

        /// <summary>The events that hold this week with a housemate, in <see cref="All"/>'s order (empty below talk rules 5).</summary>
        public static List<string> Of(EpisodeState s, string npcId) => All.Where(kind => Holds(s, npcId, kind)).ToList();

        /// <summary>
        /// This week's evictee: once the vote is resolved, the nominee who has left the house (the nominees stay the week's until
        /// the week turns); null before the vote and on move-in night.
        /// </summary>
        public static string EvictedThisWeek(EpisodeState s) =>
            s == null || !s.evictionResolved || s.nominees == null ? null
                : s.nominees.FirstOrDefault(id => s.Find(id) is ContestantState c && !c.isPlayer && c.status != ContestantStatus.Active);

        /// <summary>
        /// ally-evicted: this week's evictee shared a pact with the housemate that still stands or that has fewer than two members
        /// still in the house (the week's departures end such a pact), and the housemate's view of them has not soured past
        /// <see cref="NpcAlliances.SourLine"/>. A pact records no reason it ended, so one that broke earlier over another member's
        /// fallout counts too once fewer than two of it are left in the house, while that view holds.
        /// </summary>
        private static bool LostAnAlly(EpisodeState s, string npcId)
        {
            string gone = EvictedThisWeek(s);
            if (gone == null || gone == npcId || s.alliances == null) return false;
            if (s.Score(npcId, gone) < NpcAlliances.SourLine) return false;
            return s.alliances.Any(a => a?.members != null && a.members.Contains(npcId) && a.members.Contains(gone)
                && (a.active || a.members.Count(id => s.Find(id)?.status == ContestantStatus.Active) < 2));
        }

        /// <summary>
        /// couple-noticed: the player is a couple with a housemate still in the house (<see cref="TalkRomance.PartnerOf"/>), the
        /// couple's fact is public (<see cref="Knowledge"/>, as "Everyone's Talking" and the couple's going public write it), and
        /// this housemate is not the partner.
        /// </summary>
        private static bool CoupleKnown(EpisodeState s, string npcId)
        {
            string partner = TalkRomance.PartnerOf(s);
            if (partner == null || partner == npcId || s.Find(partner)?.status != ContestantStatus.Active) return false;
            string refId = StoryConsumers.CoupleRef(s, s.playerId, partner);
            if (refId == null) return false;
            var fact = Knowledge.Of(s, FactKinds.Couple, refId);
            return fact != null && fact.visibility == FactVisibility.Public;
        }
    }
}
