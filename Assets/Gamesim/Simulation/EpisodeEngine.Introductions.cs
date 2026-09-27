using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    // The first night's introductions: the player meets each houseguest once, Warm, Calculated or
    // Bold, and the houseguest's traits decide how it lands (WebIntroductions). This is the only
    // consequence the opening has; marking the meet-and-greet beat moves nobody.
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// The relationship event an introduction writes, one on each edge.
        ///
        /// <para>Not one of <see cref="RelationshipLedger"/>'s permanent acts, so it fades like any
        /// other social traffic: having been introduced is not something anybody did to anybody. It
        /// is also how the season knows who has been met (<see cref="HasIntroduced"/>), which is why
        /// progress through the meet-and-greet needs no saved field of its own.</para>
        /// </summary>
        public const string IntroducedEventType = "introduced";

        /// <summary>The kind of the season event an introduction logs.</summary>
        public const string IntroductionEventKind = "introduction";

        /// <summary>
        /// Whether the season is on its first night: the opening social window, before anybody has
        /// held Head of Household.
        ///
        /// <para>"Week one, social time" is not enough on its own. Week one has two social windows,
        /// the opening one and the one after the first eviction, because the week only turns on
        /// leaving the second. A Head of Household and a resolved eviction are what tell them
        /// apart.</para>
        /// </summary>
        public static bool IsFirstNight(EpisodeState s) => s != null && s.phase == EpisodePhase.Social && s.week == 1
            && string.IsNullOrEmpty(s.hohId) && !s.evictionResolved;

        /// <summary>
        /// Whether the player can still introduce themselves to anybody: it is the first night, the
        /// meet-and-greet has not been recorded, and the player is still in the house.
        /// </summary>
        public static bool IntroductionsOpen(EpisodeState s) => IsFirstNight(s)
            && !s.openingBeatsSeen.Contains(OpeningBeat.MeetAndGreet) && s.Find(s.playerId)?.status == ContestantStatus.Active;

        /// <summary>
        /// Whether the player has introduced themselves to <paramref name="npcId"/>, read from the
        /// ledger rather than stored, so a reload halfway through the meet-and-greet resumes at the
        /// first houseguest still to meet.
        /// </summary>
        public static bool HasIntroduced(EpisodeState s, string npcId) => s.relationships.Any(r =>
            r.fromId == s.playerId && r.toId == npcId && r.events.Any(e => e.type == IntroducedEventType));

        /// <summary>The houseguests the player has yet to meet, in cast order, which is the order they are met in.</summary>
        public static List<ContestantState> StillToMeet(EpisodeState s) =>
            s.Active.Where(c => !c.isPlayer && !HasIntroduced(s, c.id)).ToList();

        /// <summary>
        /// The player introduces themselves to one houseguest, in one of the three approaches.
        ///
        /// <para>Worth what the reference build gives: +3 for a match, +1 for a neutral answer and -3
        /// for a clash, to the player's side exactly. The social bonus is capped at a tenth, which
        /// rounds away on numbers this small, and a loss is never scaled. The houseguest hands back
        /// between 80% and 120% of it, as they would any change.</para>
        ///
        /// <para>It goes through <see cref="ChangeWithRoll"/>, so the arithmetic, the notes, the
        /// events and the arc are the same as every other change in the house, but with one roll
        /// injected. The roll is hashed from the season and the houseguest rather than drawn from the
        /// generator, so the opening cannot re-roll a season: watching the meet-and-greet or
        /// skipping it cannot change who wins the first competition. The same value answers both of
        /// the reducer's draws, so the impact recorded on the houseguest's edge is the change that
        /// was applied to it. The reference build drew twice there, recording one number and applying
        /// another.</para>
        ///
        /// <para>Free, because on the first night the house comes to the player; it must never fall
        /// through to <c>Social</c>, which charges an interaction. The log says who was met and never
        /// how it went: the meet-and-greet shows the houseguest's answer, not a number.</para>
        /// </summary>
        private static void Introduce(EpisodeState s, EpisodeCommand c)
        {
            Require(IsFirstNight(s), "Introductions happen on the first night, before the first competition.");
            Require(!s.openingBeatsSeen.Contains(OpeningBeat.MeetAndGreet), "The introductions are over.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var target = s.Find(c.targetId);
            Require(target != null && !target.isPlayer && target.status == ContestantStatus.Active, "Introduce yourself to an active houseguest.");
            var approach = WebIntroductions.Find(c.secondTargetId);
            Require(approach != null, "Introduce yourself Warm, Calculated or Bold.");
            Require(!HasIntroduced(s, target.id), "You have already met " + target.name + ".");

            var outcome = WebIntroductions.Judge(approach, target.traits);
            // WebIntroductions hashes an int seed and the season stores a uint: the same bits.
            double roll = WebIntroductions.ReciprocalRoll(unchecked((int)s.seed), target.id);
            ChangeWithRoll(s, s.playerId, target.id, WebIntroductions.Bonus(outcome), () => roll,
                approach.Label + " first impression (" + WebIntroductions.Word(outcome) + ")", IntroducedEventType);
            Log(s, IntroductionEventKind, "You introduced yourself to " + target.name + ".", s.playerId, target.id);
        }
    }
}
