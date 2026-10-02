using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Grow and manage alliances (ACTIONS-DEALS-ALLIANCES-PLAN C5), under the commitment rules only:
    /// without them <see cref="EpisodeCommandKind.BringIntoAlliance"/> and
    /// <see cref="EpisodeCommandKind.RenameAlliance"/> are refused before anything is spent, and a leave
    /// is the one it always was, so a season without the rules plays roll for roll and line for line as
    /// it did.
    ///
    /// <para><b>"Bring {name} into {pact}".</b> A social action, said to the houseguest asked, and spent
    /// whatever the answer, as a proposal is (C4). What the player can see for themselves is refused
    /// first and spends nothing: a pact that is not theirs, somebody already in it, a pact already
    /// holding <see cref="LargestPact"/> in the house. Then one draw from the season's stream, always:
    /// the houseguest answers on the alliance invitation's own odds (<see cref="AllianceChance"/>, which
    /// the row shows as the player reads it, <see cref="KnownOdds.Alliance"/>), and a grudge of forty or
    /// more says no whatever it says (<see cref="GrudgeRefusesAlliance"/>) - exactly C4's question. And
    /// every member still in the house has their say, with no roll: each must welcome them
    /// (<see cref="NpcAlliances.WouldWelcome"/>, <see cref="NpcAlliances.WouldPropose"/>'s question asked
    /// of a pact that already exists, the invitee's own three included). One no is enough. A yes adds
    /// them at the end of the pact's members, so its founder - <c>members[0]</c> - never changes; it is
    /// a pact with the player, so it brings the warmth a proposal's yes does, with its reciprocal draw,
    /// and the ledger's permanent 'alliance-formed' between the newcomer and every member in the house,
    /// both ways (C4's record; between houseguests through the ledger, never <see cref="Change"/>). A no
    /// says who said it - each said it to the player's face - and moves nobody.</para>
    ///
    /// <para><b>"Rename {pact}".</b> The founder's to give, from the names on offer
    /// (<see cref="PactNames.For"/>), said to a member, free - it changes nothing anybody weighs: no
    /// view, no roll, no record - and once a pact a week, so the log is never a list of names.</para>
    ///
    /// <para><b>"Leave {pact}".</b> <see cref="EpisodeCommandKind.LeaveAlliance"/> names the pact it
    /// leaves (<c>secondTargetId</c>), or, from an older command that names none, the first the two
    /// share, as it always did. In a pact of three or more still in the house it takes only the player
    /// out and the others keep it - the story's defector rule (<c>StoryEffects.AllianceLeave</c>) and
    /// the free exit's cut (C2), <see cref="TakeOutOfPact"/> - with the defector's price: every member
    /// left behind holds the walk-out's eighty against them, the one they told thinks fifteen the less
    /// of them, and the action is spent. A pact of two ends, as leaving one always did. The week an ally
    /// turned on the pact, cutting ties with them is still C2's free exit.</para>
    ///
    /// <para><b>The founder.</b> <c>members[0]</c>: the player's own pacts open with them (a proposal, an
    /// invitation's pair, a story's pact around them), a pact the player joins keeps its founder first
    /// (<c>AllyThroughInvitation</c> appends), and so does one they grow. Under the rules the founder
    /// calls the bloc's vote (<see cref="WebVotingBlocs.FromNative"/>'s <c>founderId</c>) whenever they
    /// are in it, voting and still following it.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// The most a pact may hold in the house: four, the largest any pact is made with (a story's,
        /// <see cref="NpcAlliances.FormFromStory"/>'s two to four). Nobody is brought into one that holds
        /// four. Validation holds a pact only to the cast; the departed (X5) do not count.
        /// </summary>
        public const int LargestPact = 4;

        /// <summary>Why a grow or a rename is refused in a season that does not play the commitment rules.</summary>
        public const string CommitmentKindRefusal = "This season does not play the commitment rules.";

        /// <summary>Why a pact the player is not in cannot be grown, renamed or left.</summary>
        public const string NotYourPactRefusal = "You are not in that alliance.";

        /// <summary>Why a rename picks from a list: one of the names on offer, never the player's own words.</summary>
        public const string NameNotOfferedRefusal = "Choose one of the names on offer.";

        /// <summary>The kind of the line a rename logs: its own, so it is never read as a pact formed or ended, nor drawn with their handshake.</summary>
        public const string AllianceRenamedKind = "alliance-renamed";

        /// <summary>A pact's founder: its first member, or null for none.</summary>
        public static string Founder(AllianceState pact) => pact?.members == null || pact.members.Count == 0 ? null : pact.members[0];

        /// <summary>
        /// Whether the player founded this pact: they are its first member, and the record agrees - a
        /// pact the player made, or a story's that formed around them (<see cref="FinalistRead.FoundedByPlayer"/>).
        /// A pact of houseguests the player joined never is, even if its earlier members are cut away.
        /// </summary>
        public static bool PlayerFounded(EpisodeState s, AllianceState pact)
        {
            if (s == null || Founder(pact) != s.playerId) return false;
            var row = s.ledger?.alliances?.FirstOrDefault(r => r != null && r.id == pact.id);
            return row == null || FinalistRead.FoundedByPlayer(s, pact, row);
        }

        /// <summary>The standing pacts the player shares with this houseguest, in the house's order.</summary>
        public static List<AllianceState> SharedPacts(EpisodeState s, string npcId) =>
            s?.alliances == null || string.IsNullOrEmpty(npcId) ? new List<AllianceState>()
                : s.alliances.Where(a => a != null && a.active && a.members.Contains(s.playerId) && a.members.Contains(npcId)).ToList();

        /// <summary>How many of a pact are still in the house.</summary>
        public static int InTheHouse(EpisodeState s, AllianceState pact) =>
            pact?.members == null ? 0 : pact.members.Count(id => s.Find(id)?.status == ContestantStatus.Active);

        /// <summary>
        /// Why the player cannot ask this houseguest into this pact at all, or null when they can ask:
        /// what the player can see for themselves, so a press refused for it spends nothing. Whether
        /// the houseguest and the members say yes is not here: that is the asking.
        /// </summary>
        public static string BringInRefusal(EpisodeState s, string inviteeId, AllianceState pact)
        {
            if (s == null) return NotYourPactRefusal;
            if (!CommitmentRulesOn(s)) return CommitmentKindRefusal;
            if (pact == null || !pact.active || !pact.members.Contains(s.playerId)) return NotYourPactRefusal;
            var invitee = s.Find(inviteeId);
            if (invitee == null || invitee.isPlayer || invitee.status != ContestantStatus.Active) return "Approach an active housemate.";
            if (pact.members.Contains(inviteeId)) return invitee.name + " is already in " + pact.name + ".";
            if (InTheHouse(s, pact) >= LargestPact) return pact.name + " already has " + LargestPact + " in the house, the most a pact holds.";
            return null;
        }

        /// <summary>
        /// Why the player cannot rename this pact now, or null when they can: under the rules, a standing
        /// pact the player founded, not renamed yet this week. Said to a member, with a name on offer.
        /// </summary>
        public static string RenameRefusal(EpisodeState s, AllianceState pact)
        {
            if (s == null) return NotYourPactRefusal;
            if (!CommitmentRulesOn(s)) return CommitmentKindRefusal;
            if (pact == null || !pact.active || !pact.members.Contains(s.playerId)) return NotYourPactRefusal;
            if (!PlayerFounded(s, pact)) return "Only whoever founded " + pact.name + " names it.";
            if (RenamedThisWeek(s, pact)) return pact.name + " has had its new name this week.";
            return null;
        }

        /// <summary>A rename's line: "The Riley Pact is The Outsiders now."</summary>
        public static string RenamedLine(string from, string to) => from + " is " + to + " now.";

        /// <summary>Whether this pact has been renamed this week: its own line, under the name it has now.</summary>
        public static bool RenamedThisWeek(EpisodeState s, AllianceState pact) =>
            s?.events != null && pact != null && s.events.Any(e => e != null && e.kind == AllianceRenamedKind && e.week == s.week
                && e.text != null && e.text.EndsWith(" is " + pact.name + " now.", System.StringComparison.Ordinal));

        /// <summary>The line a houseguest brought into a pact is told in: "Maya Hassan joined The Riley Pact."</summary>
        public static string JoinedLine(string name, string pact) => name + " joined " + pact + ".";

        /// <summary>
        /// The line a pact the player leaves going on without them is told in: "You left the alliance
        /// with Riley Chen and Jo Park: The Riley Pact goes on without you." Every member in the house
        /// hears it.
        /// </summary>
        public static string LeftGoesOnLine(IList<string> names, string pact) =>
            "You left the alliance with " + Allegiance.Join(names) + ": " + pact + " goes on without you.";

        /// <summary>Whether a line says the player left this pact and it went on without them.</summary>
        public static bool IsLeftGoesOnLine(string text, string pact) =>
            text != null && pact != null && text.StartsWith("You left the alliance with ", System.StringComparison.Ordinal)
            && text.EndsWith(": " + pact + " goes on without you.", System.StringComparison.Ordinal);

        /// <summary>
        /// "Bring {name} into {pact}" (C5): the refusals the player can see spend nothing; then one draw,
        /// always, for the houseguest's answer on the invitation's odds; every member in the house
        /// welcomes them or not, with no roll; and the answer. The caller (<see cref="Social"/>) spends
        /// the action either way.
        /// </summary>
        private static void BringIntoAlliance(EpisodeState s, ContestantState invitee, EpisodeCommand c)
        {
            var pact = s.alliances.FirstOrDefault(a => a.id == c.secondTargetId);
            string refusal = BringInRefusal(s, invitee.id, pact);
            Require(refusal == null, refusal);
            double chance = AllianceChance(s, invitee.id);
            bool grudge = GrudgeRefusesAlliance(s, invitee.id);
            // The roll first and always: one per asking, whatever the grudge or the members say.
            bool willing = Roll(s) * 100 < chance && !grudge;
            var members = pact.members.Where(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active).ToList();
            var against = members.Where(id => !NpcAlliances.WouldWelcome(s, id, invitee.id)).ToList();
            if (willing && against.Count == 0)
            {
                // At the end: the founder stays first.
                pact.members.Add(invitee.id);
                // Joining is learning: the pact's fact, if it has one, gains them as a knower.
                if (ReadRulesOn(s)) Knowledge.AddKnower(s, Knowledge.Of(s, FactKinds.Alliance, pact.id), invitee.id);
                // A pact with the player: the warmth a proposal's yes brings, with its reciprocal draw.
                Change(s, s.playerId, invitee.id, 8);
                Log(s, "alliance", JoinedLine(invitee.name, pact.name),
                    new[] { s.playerId }.Concat(members).Concat(new[] { invitee.id }).ToArray());
                // On the record with every member in the house, both ways, as a pact's founding is: the
                // player's (C4) and, between houseguests, the ledger's alone.
                foreach (string member in new[] { s.playerId }.Concat(members))
                    RelationshipLedger.Record(s, member, invitee.id, "alliance-formed", AllianceFormedImpact, pact.name + " was joined");
                Remember(s, invitee.id, s.playerId, "Brought me into " + pact.name + ".", true);
                return;
            }
            Log(s, AllianceRefusedKind, BringInRefusedLine(s, invitee, pact, willing ? null : PlayerDeals.Reasoning(s, invitee.id, DealKind.AllianceInvite, false), against),
                new[] { s.playerId, invitee.id }.Concat(against).ToArray());
        }

        /// <summary>
        /// Who said no to a houseguest joining a pact, each in the player's hearing: the houseguest in
        /// their own words when they turned it down ("Maya Hassan turned down The Riley Pact. “…”"), and
        /// the members who would not have them ("Riley Chen won't have Maya Hassan in The Riley Pact.").
        /// </summary>
        public static string BringInRefusedLine(EpisodeState s, ContestantState invitee, AllianceState pact, string said, IList<string> against)
        {
            string members = Allegiance.Join((against ?? new List<string>()).Select(id => Name(s, id)));
            if (said == null) return members + " won't have " + invitee.name + " in " + pact.name + ".";
            string line = invitee.name + " turned down " + pact.name + ". “" + said + "”";
            if (string.IsNullOrEmpty(members)) return line;
            return line + " " + members + " wouldn't have had " + invitee.name.Split(' ')[0] + " in it either.";
        }

        /// <summary>
        /// "Rename {pact}" (C5): the founder names their pact from the names on offer, said to a member,
        /// in free time, the campaign or a window that lets them talk to that member. Free, and it moves
        /// nothing anybody weighs; the line goes to everyone in it still in the house.
        /// </summary>
        private static void RenameAlliance(EpisodeState s, EpisodeCommand c)
        {
            RequireConversationWindow(s, c);
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var pact = s.alliances.FirstOrDefault(a => a.id == c.secondTargetId);
            string refusal = RenameRefusal(s, pact);
            Require(refusal == null, refusal);
            var member = s.Find(c.targetId);
            Require(member != null && !member.isPlayer && member.status == ContestantStatus.Active && pact.members.Contains(member.id),
                "Say it to somebody in " + pact.name + ".");
            string name = (c.text ?? string.Empty).Trim();
            Require(PactNames.For(s, pact).Contains(name), NameNotOfferedRefusal);
            string was = pact.name;
            pact.name = name;
            Log(s, AllianceRenamedKind, RenamedLine(was, name),
                pact.members.Where(id => s.Find(id)?.status == ContestantStatus.Active).ToArray());
        }

        /// <summary>
        /// Leaving a pact under the commitment rules (C5), outside C2's free exit: the pact named, or the
        /// first the two share; in a pact of three or more still in the house only the player goes, and
        /// the rest keep it; a pact of two ends, as it always did. The walk-out's price either way: the
        /// one told thinks fifteen the less of the player, remembers it, and everybody left behind holds
        /// the eighty. The caller (<see cref="Social"/>) spends the action.
        /// </summary>
        private static void LeavePact(EpisodeState s, ContestantState target, EpisodeCommand c)
        {
            var pact = string.IsNullOrEmpty(c.secondTargetId)
                ? SharedPacts(s, target.id).FirstOrDefault()
                : s.alliances.FirstOrDefault(a => a.id == c.secondTargetId);
            Require(pact != null && pact.active && pact.members.Contains(s.playerId) && pact.members.Contains(target.id), "No shared alliance is active.");
            var behind = pact.members.Where(id => id != s.playerId).ToList();
            var here = behind.Where(id => s.Find(id)?.status == ContestantStatus.Active).ToList();
            bool ended = TakeOutOfPact(s, pact, s.playerId);
            Change(s, target.id, s.playerId, -15);
            Remember(s, target.id, s.playerId, "Left our alliance.", true);
            if (ended) Log(s, "alliance", "You left the alliance with " + target.name + ".", s.playerId, target.id);
            else Log(s, "alliance", LeftGoesOnLine(here.Select(id => Name(s, id)).ToList(), pact.name), new[] { s.playerId }.Concat(here).ToArray());
            foreach (var member in behind) StoryAllianceLeft(s, member, s.playerId);
        }

        /// <summary>
        /// Takes one member out of a pact: a pact with two or fewer of it in the house ends, and a bigger
        /// one goes on without them - the story's defector rule, and C2's cut. True when it ended.
        /// </summary>
        private static bool TakeOutOfPact(EpisodeState s, AllianceState pact, string memberId)
        {
            bool ends = InTheHouse(s, pact) <= 2;
            if (ends) pact.active = false;
            else pact.members.Remove(memberId);
            return ends;
        }
    }
}
