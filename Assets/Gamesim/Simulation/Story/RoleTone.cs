using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// How a houseguest comes to the player in a role this week (ROLE-MOMENTS-PLAN R1, §3.0): the card's
    /// emotional state - its tone - with the situation word and the player's read beside it, and what the
    /// tone stakes.
    ///
    /// <para><b>Only what the player already sees.</b> The web took its tone from the houseguest's hidden
    /// view of the player, which told the player that view. This reads nothing the player does not already
    /// see (the rule at <see cref="ReplyCards.Message"/> and <see cref="KnownOdds"/>): the role, given by the
    /// moment; the ceremonies - who was on the block in which week (<see cref="ContestantState.nominationWeeks"/>)
    /// and who held the week's power (<see cref="SeasonLedger.power"/>); the size of the house; the houseguest's
    /// traits, which the conversation header names, as <see cref="Personality"/> axes; the player's own record
    /// with them before this week, their nominations and the words they broke as the Your word page lists them
    /// (<see cref="CommitmentsRead"/>); and the player's own reading of them (<see cref="ReplyCards.ReadsAsClose"/>).
    /// Never a houseguest's view of anybody, a grudge, a mood, a memory or a hidden score. Mood in particular
    /// is not an input: a nomination moves it two steps down, so nearly every nominee reads Angry, and no rule
    /// reads it.</para>
    ///
    /// <para><b>The ladder.</b> The first row that matches wins (<see cref="Ladder"/>): Bitter, Threatening,
    /// Desperate, Friendly, Earnest, Calculated, Pragmatic, each only for the roles it applies to
    /// (<see cref="AppliesTo"/>). The three tones that carry a rider (<see cref="RiderTones"/>) read only what
    /// cannot move inside the week - the record before this week, traits, the ceremonies, the house as the week
    /// began - and are checked first, so a card's rider never changes between the player's answer and the
    /// decision that settles it, and nothing has to be stored. Friendly reads the player's live reading, and
    /// carries only an answer-time cost.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator, and lives in the
    /// simulation so the Unity-free subset tests it. The offer board draws it (R2, <see cref="RoleOffers"/>), and
    /// the engine charges its stakes on the nominees' veto asks (R3), both under <see cref="EpisodeEngine.RolesOn"/>.</para>
    /// </summary>
    public static class RoleTone
    {
        public const string Bitter = "Bitter", Threatening = "Threatening", Desperate = "Desperate", Friendly = "Friendly",
            Earnest = "Earnest", Calculated = "Calculated", Pragmatic = "Pragmatic";

        /// <summary>The role a houseguest comes to the player in, this week.</summary>
        public static class Role
        {
            /// <summary>On the block: a nominee asking for the veto, or one the player finds in tears.</summary>
            public const string Nominee = "nominee";
            /// <summary>The Head of Household, asking the player to leave the block alone.</summary>
            public const string HeadOfHousehold = "hoh";
            /// <summary>Holding the veto, with an offer for a nominated player.</summary>
            public const string VetoHolder = "holder";
            /// <summary>Pitching to the player as Head of Household, before the nominations.</summary>
            public const string Courting = "courting";
            public static readonly string[] All = { Nominee, HeadOfHousehold, VetoHolder, Courting };
            public static bool IsKnown(string role) => role != null && Array.IndexOf(All, role) >= 0;
        }

        // ---------------------------------------------------------------- the thresholds (starting values, to tune)

        /// <summary>Threatening: at least this Bold and this Vengeful, for example Competitive with Stubborn.</summary>
        public const int ThreateningBold = 2, ThreateningVengeful = 2;

        /// <summary>Earnest: at least this Warm, for example Emotional, or Loyal with Social.</summary>
        public const int EarnestWarm = 2;

        /// <summary>Calculated: at least this Steady, or at most this Honest.</summary>
        public const int CalculatedSteady = 2, CalculatedHonest = -2;

        /// <summary>The endgame: this many or fewer in the house. The line the C7 price turns to a final two at.</summary>
        public const int EndgameSize = NpcDeals.EndgameSize;

        // ---------------------------------------------------------------- the ladder

        /// <summary>One row of the ladder: its tone, and the rule it reads, given the houseguest and the role.</summary>
        private static readonly (string tone, Func<EpisodeState, ContestantState, string, bool> rule)[] Rows =
        {
            (Bitter, (s, who, role) => YouNominatedThemBefore(s, who) || YouBrokeYourWordBefore(s, who.id)),
            (Threatening, (s, who, role) =>
            {
                var axes = Personality.Of(who);
                return axes.Bold >= ThreateningBold && axes.Vengeful >= ThreateningVengeful;
            }),
            (Desperate, (s, who, role) => Endgame(s) || OnTheBlockBefore(s, who)
                || (role == Role.Nominee && ReplacementThisWeek(s, who.id))),
            (Friendly, (s, who, role) => ReplyCards.ReadsAsClose(s, who.id)),
            (Earnest, (s, who, role) => Personality.Of(who).Warm >= EarnestWarm),
            (Calculated, (s, who, role) =>
            {
                var axes = Personality.Of(who);
                return axes.Steady >= CalculatedSteady || axes.Honest <= CalculatedHonest;
            }),
            (Pragmatic, (s, who, role) => true),
        };

        /// <summary>The tones in the ladder's order: the first that applies to the role and matches wins.</summary>
        public static readonly string[] Ladder = Rows.Select(row => row.tone).ToArray();

        /// <summary>The tones that carry a rider (<see cref="Stakes"/>), every one read from the week's fixed facts.</summary>
        public static readonly string[] RiderTones = { Bitter, Threatening, Desperate };

        /// <summary>
        /// The roles each tone applies to (§3.0's table). Threatening never reads on the veto holder, and
        /// Desperate only on a nominee or a houseguest courting the player.
        /// </summary>
        private static readonly Dictionary<string, string[]> Applies = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { Bitter, Role.All },
            { Threatening, new[] { Role.Nominee, Role.HeadOfHousehold, Role.Courting } },
            { Desperate, new[] { Role.Nominee, Role.Courting } },
            { Friendly, Role.All },
            { Earnest, Role.All },
            { Calculated, Role.All },
            { Pragmatic, Role.All },
        };

        /// <summary>Whether a tone can be read on a houseguest in this role at all.</summary>
        public static bool AppliesTo(string tone, string role) =>
            tone != null && Role.IsKnown(role) && Applies.TryGetValue(tone, out var roles) && Array.IndexOf(roles, role) >= 0;

        /// <summary>Every tone a houseguest in this role can wear, in the ladder's order.</summary>
        public static IEnumerable<string> TonesFor(string role) => Ladder.Where(tone => AppliesTo(tone, role));

        /// <summary>
        /// The tone a houseguest wears coming to the player in this role this week: the first row of the ladder
        /// that applies to the role and matches. Null for nobody, the player, or a role this does not know.
        /// </summary>
        public static string Of(EpisodeState s, string speakerId, string role)
        {
            var who = Speaker(s, speakerId);
            if (who == null || !Role.IsKnown(role)) return null;
            foreach (var row in Rows)
                if (AppliesTo(row.tone, role) && row.rule(s, who, role)) return row.tone;
            return Pragmatic;
        }

        // ---------------------------------------------------------------- the second label

        /// <summary>
        /// Where the houseguest stands this week, in words every houseguest can see: "First time on the block",
        /// "On the block again" or "Endgame: 6 left" for a nominee; "Head of Household"; "Holds the veto";
        /// "Never nominated", "Nominated before" or the endgame for one courting the player. The endgame comes
        /// first: it says the most. Null where <see cref="Of"/> is.
        /// </summary>
        public static string Situation(EpisodeState s, string speakerId, string role)
        {
            var who = Speaker(s, speakerId);
            if (who == null || !Role.IsKnown(role)) return null;
            switch (role)
            {
                case Role.HeadOfHousehold: return "Head of Household";
                case Role.VetoHolder: return "Holds the veto";
                case Role.Nominee:
                    return Endgame(s) ? EndgameWord(s) : OnTheBlockBefore(s, who) ? "On the block again" : "First time on the block";
                default:
                    return Endgame(s) ? EndgameWord(s) : OnTheBlockBefore(s, who) ? "Nominated before" : "Never nominated";
            }
        }

        public const string Close = "Close", Neutral = "Neutral", Hostile = "Hostile";

        /// <summary>
        /// The player's own reading of them in a word, on every card's lines (<see cref="ReplyCards.CloseReading"/>,
        /// <see cref="ReplyCards.HostileReading"/>): <see cref="Close"/>, <see cref="Hostile"/> or <see cref="Neutral"/>.
        /// It replaces the web's relationship band, which was the houseguest's hidden view. Null where <see cref="Of"/> is.
        /// </summary>
        public static string Read(EpisodeState s, string speakerId)
        {
            if (Speaker(s, speakerId) == null) return null;
            return ReplyCards.ReadsAsClose(s, speakerId) ? Close : ReplyCards.ReadsAsHostile(s, speakerId) ? Hostile : Neutral;
        }

        /// <summary>The read as the card says it: "Your read: Close".</summary>
        public static string ReadLine(EpisodeState s, string speakerId)
        {
            string read = Read(s, speakerId);
            return read == null ? null : "Your read: " + read;
        }

        // ---------------------------------------------------------------- what the tone stakes

        /// <summary>What a tone carries once its moment settles, beyond its decline and lapse costs.</summary>
        public static class Riders
        {
            /// <summary>Accepted and kept: their grudge against the player eases (<see cref="Grudges.Ease"/>).</summary>
            public const string EasesTheirGrudge = "eases-their-grudge";
            /// <summary>Turned down or left unanswered, and it goes against them: they hold a grudge against the player.</summary>
            public const string HoldsAGrudge = "holds-a-grudge";
            /// <summary>Accepted and kept: they owe the player a favour, a Hook (<see cref="Hooks.Create"/>).</summary>
            public const string OwesAFavour = "owes-a-favour";
        }

        /// <summary>How far a Bitter houseguest's grudge eases when the player takes their offer and keeps it.</summary>
        public const double BitterEase = 30;

        /// <summary>The grudge a Threatening houseguest holds when the player's no, or silence, goes against them.</summary>
        public const double ThreateningGrudge = 30;

        /// <summary>
        /// What a Head of Household who asked the player to leave the block alone, and was turned down or left hanging, holds
        /// against them once the veto is used anyway (ROLE-MOMENTS-PLAN R4, §3.3): this much less with the player, and a
        /// grudge this deep - a Threatening one's is their rider's, <see cref="ThreateningGrudge"/>. Starting values, to tune.
        /// </summary>
        public const double DefiedCost = -5, DefiedGrudge = 15;

        /// <summary>
        /// One tone's stakes (§3.0, "What the tone stakes"): what an honest no costs with them, what leaving them
        /// hanging costs once the decision lapses it, and the rider. Starting values, to tune. The engine applies it to
        /// the nominees' veto asks under Roles (R3, <c>EpisodeEngine.RoleMoments</c>); without Roles a no stays at -3
        /// and a lapse at 0, as before. A lapse is a change in how they feel, never a breach.
        /// </summary>
        public sealed class Stake
        {
            public readonly string tone;
            /// <summary>Turned down, "an honest no".</summary>
            public readonly double decline;
            /// <summary>Left unanswered, "left hanging".</summary>
            public readonly double lapse;
            /// <summary>A <see cref="Riders"/> value, or null for none.</summary>
            public readonly string rider;
            /// <summary>The rider's size: a grudge eased or held. Zero for a favour, which is one, and for no rider.</summary>
            public readonly double riderAmount;

            public Stake(string tone, double decline, double lapse, string rider = null, double riderAmount = 0)
            {
                this.tone = tone; this.decline = decline; this.lapse = lapse; this.rider = rider; this.riderAmount = riderAmount;
            }
        }

        private static readonly Stake[] StakeTable =
        {
            new Stake(Bitter, -3, -5, Riders.EasesTheirGrudge, BitterEase),
            new Stake(Threatening, -2, -4, Riders.HoldsAGrudge, ThreateningGrudge),
            new Stake(Desperate, -3, -5, Riders.OwesAFavour),
            new Stake(Friendly, -1, -3),
            new Stake(Earnest, -2, -4),
            new Stake(Calculated, 0, -1),
            new Stake(Pragmatic, -1, -3),
        };

        /// <summary>Every tone's stakes, in the ladder's order.</summary>
        public static IReadOnlyList<Stake> StakesTable => StakeTable;

        /// <summary>A tone's stakes, or null for a tone this does not know.</summary>
        public static Stake Stakes(string tone) => StakeTable.FirstOrDefault(row => row.tone == tone);

        // ---------------------------------------------------------------- the facts it reads

        private static ContestantState Speaker(EpisodeState s, string speakerId)
        {
            if (s == null || string.IsNullOrEmpty(speakerId) || speakerId == s.playerId) return null;
            return s.Find(speakerId);
        }

        /// <summary>
        /// Whether the player put them on the block in an earlier week, as Head of Household: nominated them, or
        /// named them the replacement. Read off the ceremonies - the weeks they were on the block, and who held
        /// each week's power - so this week's ceremony never counts.
        /// </summary>
        public static bool YouNominatedThemBefore(EpisodeState s, ContestantState who) =>
            s?.ledger?.power != null && who?.nominationWeeks != null && !string.IsNullOrEmpty(s.playerId)
            && s.ledger.power.Any(p => p != null && p.week < s.week && p.hohId == s.playerId && who.nominationWeeks.Contains(p.week));

        /// <summary>
        /// Whether the player broke their word to them in an earlier week: a deal, a promise or an oath the Your
        /// word page lists as broken by the player (<see cref="CommitmentsRead"/>), settled before this week. A
        /// word broken this week counts from the next, so answering a card or deciding the veto never turns one
        /// Bitter in the week it is up.
        /// </summary>
        public static bool YouBrokeYourWordBefore(EpisodeState s, string id)
        {
            if (s == null || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(s.playerId)) return false;
            return CommitmentsRead.With(s, id).Any(c => c.outcome == CommitmentsRead.Outcomes.Broken
                && c.brokenById == s.playerId && SettledIn(s, c) < s.week);
        }

        /// <summary>
        /// The week a settled commitment settled - a broken one, the week it broke: the page's own where it says,
        /// else the record's settlement (every deal and promise under the commitment rules, which Roles plays
        /// under), else the week it was made, for a record settled before the rules wrote one. The offer board's
        /// history line reads it too (<see cref="RoleOffers.History"/>).
        /// </summary>
        internal static int SettledIn(EpisodeState s, CommitmentsRead.Commitment c)
        {
            if (c.settledWeek > 0) return c.settledWeek;
            int settled = c.kind == CommitmentsRead.Kinds.Deal ? CommitmentReferences.FindDeal(s, c.id)?.settledWeek ?? 0
                : c.kind == CommitmentsRead.Kinds.Promise ? CommitmentReferences.FindPromise(s, c.id)?.settledWeek ?? 0 : 0;
            return settled > 0 ? settled : c.week;
        }

        /// <summary>Whether they were on the block in an earlier week, whoever put them there.</summary>
        public static bool OnTheBlockBefore(EpisodeState s, ContestantState who) =>
            s != null && who?.nominationWeeks != null && who.nominationWeeks.Any(week => week < s.week);

        /// <summary>Whether they went up this week as the replacement, once the veto was used.</summary>
        public static bool ReplacementThisWeek(EpisodeState s, string id)
        {
            var power = PowerThisWeek(s);
            return power != null && power.vetoUsed && !string.IsNullOrEmpty(id) && power.replacementId == id;
        }

        /// <summary>
        /// The house as the week began: everybody in it, and whoever left it this week - evicted, or removed by
        /// production. Every card of the week is up before its eviction, so this is the house each of them saw.
        /// </summary>
        public static int HouseThisWeek(EpisodeState s)
        {
            if (s == null) return 0;
            var power = PowerThisWeek(s);
            return s.contestants.Count(c => c != null && (c.status == ContestantStatus.Active
                || (power != null && c.id == power.evicteeId)
                || (s.story?.removals?.Any(r => r != null && r.week == s.week && r.contestantId == c.id) ?? false)));
        }

        /// <summary>Whether this is the endgame: <see cref="EndgameSize"/> or fewer in the house this week.</summary>
        public static bool Endgame(EpisodeState s) => s != null && HouseThisWeek(s) <= EndgameSize;

        private static string EndgameWord(EpisodeState s) => "Endgame: " + HouseThisWeek(s) + " left";

        private static PowerRow PowerThisWeek(EpisodeState s) => s?.ledger?.power?.LastOrDefault(p => p != null && p.week == s.week);
    }
}
